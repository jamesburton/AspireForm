using System.Text.RegularExpressions;
using AspireForm.EntityCatalog;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace AspireForm.ApiCatalog;

/// <summary>Scans a user Web project csproj for <c>[ApiEndpoint]</c>-decorated methods via Roslyn.</summary>
public sealed class RoslynEndpointScanner : IAsyncDisposable
{
    private MSBuildWorkspace? _workspace;
    private string? _projectPath;
    private Project? _project;

    private const string ApiEndpointAttributeFullName = "AspireForm.Annotations.ApiEndpointAttribute";
    private const string ApiAuthAttributeFullName = "AspireForm.Annotations.ApiAuthAttribute";
    private const string ApiTagAttributeFullName = "AspireForm.Annotations.ApiTagAttribute";
    private const string ApiSummaryAttributeFullName = "AspireForm.Annotations.ApiSummaryAttribute";

    /// <summary>Opens the supplied csproj as a Roslyn <see cref="MSBuildWorkspace"/> and scans for <c>[ApiEndpoint]</c>-decorated methods.
    /// The workspace is cached for subsequent scans against the same path.</summary>
    /// <param name="csprojPath">Absolute or relative path to the Web project's .csproj file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An immutable <see cref="EndpointCatalog"/> snapshot.</returns>
    public async Task<EndpointCatalog> ScanAsync(string csprojPath, CancellationToken ct)
    {
        MSBuildBootstrap.EnsureRegistered();

        var absolute = Path.GetFullPath(csprojPath);
        if (!File.Exists(absolute))
        {
            throw new EndpointCatalogException($"Project file not found: '{absolute}'.");
        }

        if (_workspace is null || _projectPath != absolute)
        {
            _workspace?.Dispose();
            _workspace = MSBuildWorkspace.Create();
            _projectPath = absolute;
            _project = await _workspace.OpenProjectAsync(absolute, cancellationToken: ct);
        }
        else
        {
            // Force a fresh re-parse of the existing project's documents.
            _project = _workspace.CurrentSolution.GetProject(_project!.Id);
        }

        var compilation = await _project!.GetCompilationAsync(ct)
            ?? throw new EndpointCatalogException("Roslyn returned a null Compilation.");

        var workspaceDiagnostics = _workspace.Diagnostics
            .Select(d => new CatalogDiagnostic(
                MapWorkspaceDiagnosticSeverity(d.Kind),
                d.Message,
                FilePath: null,
                Line: null))
            .ToList();

        var allTypes = CollectAllTypes(compilation.Assembly.GlobalNamespace);
        var endpoints = new List<EndpointInfo>();
        var routeSeen = new Dictionary<string, string>(StringComparer.Ordinal); // key: "METHOD:route", value: first method name

        foreach (var type in allTypes)
        {
            foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
            {
                var endpointAttr = method.GetAttributes()
                    .FirstOrDefault(a => FullName(a.AttributeClass) == ApiEndpointAttributeFullName);
                if (endpointAttr is null) continue;

                var route = endpointAttr.ConstructorArguments.Length > 0
                    ? endpointAttr.ConstructorArguments[0].Value as string ?? ""
                    : "";
                var httpMethod = endpointAttr.ConstructorArguments.Length > 1
                    ? endpointAttr.ConstructorArguments[1].Value as string ?? "GET"
                    : "GET";

                // Ambiguous route detection
                var routeKey = $"{httpMethod.ToUpperInvariant()}:{route}";
                if (routeSeen.TryGetValue(routeKey, out var firstMethod))
                {
                    workspaceDiagnostics.Add(new CatalogDiagnostic(
                        "warning",
                        $"Ambiguous route '{httpMethod} {route}': already registered by '{firstMethod}'. Method '{method.Name}' on '{type.Name}' will be skipped.",
                        type.Locations.FirstOrDefault()?.SourceTree?.FilePath,
                        null));
                    continue;
                }
                routeSeen[routeKey] = $"{type.Name}.{method.Name}";

                // Extract sibling attributes
                string? summary = null;
                string? authPolicy = null;
                var tags = new List<string>();
                var allAttrs = new List<AttributeInstance>();

                foreach (var attr in method.GetAttributes())
                {
                    var attrFullName = FullName(attr.AttributeClass);
                    allAttrs.Add(MapAttribute(attr));

                    if (attrFullName == ApiSummaryAttributeFullName && attr.ConstructorArguments.Length > 0)
                    {
                        summary = attr.ConstructorArguments[0].Value as string;
                    }
                    else if (attrFullName == ApiAuthAttributeFullName && attr.ConstructorArguments.Length > 0)
                    {
                        authPolicy = attr.ConstructorArguments[0].Value as string;
                    }
                    else if (attrFullName == ApiTagAttributeFullName && attr.ConstructorArguments.Length > 0)
                    {
                        if (attr.ConstructorArguments[0].Value is string tag)
                            tags.Add(tag);
                    }
                }

                var parameters = ParseRouteParameters(route);
                var filePath = method.Locations.FirstOrDefault()?.SourceTree?.FilePath ?? "";

                endpoints.Add(new EndpointInfo(
                    HandlerTypeName: type.Name,
                    MethodName: method.Name,
                    Route: route,
                    HttpMethod: httpMethod.ToUpperInvariant(),
                    Summary: summary,
                    AuthPolicy: authPolicy,
                    Tags: tags,
                    Parameters: parameters,
                    Attributes: allAttrs,
                    FilePath: filePath));
            }
        }

        return new EndpointCatalog(endpoints, workspaceDiagnostics);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _workspace?.Dispose();
        _workspace = null;
        return ValueTask.CompletedTask;
    }

    private static string MapWorkspaceDiagnosticSeverity(WorkspaceDiagnosticKind kind) => kind switch
    {
        WorkspaceDiagnosticKind.Failure => "error",
        WorkspaceDiagnosticKind.Warning => "warning",
        _ => "info",
    };

    private static List<INamedTypeSymbol> CollectAllTypes(INamespaceSymbol root)
    {
        var result = new List<INamedTypeSymbol>();
        Walk(root);
        return result;

        void Walk(INamespaceSymbol ns)
        {
            foreach (var t in ns.GetTypeMembers())
                result.Add(t);
            foreach (var child in ns.GetNamespaceMembers())
                Walk(child);
        }
    }

    private static string FullName(INamedTypeSymbol? symbol)
    {
        if (symbol is null) return "";
        var ns = symbol.ContainingNamespace?.ToDisplayString() ?? "";
        return string.IsNullOrEmpty(ns) ? symbol.Name : $"{ns}.{symbol.Name}";
    }

    private static AttributeInstance MapAttribute(AttributeData a)
    {
        var ns = a.AttributeClass?.ContainingNamespace?.ToDisplayString() ?? "";
        var name = a.AttributeClass?.Name ?? "Unknown";
        var fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        var ctorArgs = a.ConstructorArguments.Select(c => (object?)c.Value).ToList();
        var named = a.NamedArguments.ToDictionary(kv => kv.Key, kv => (object?)kv.Value.Value);
        return new AttributeInstance(fullName, ctorArgs, named);
    }

    /// <summary>Parses route parameters from patterns like <c>{id}</c>, <c>{id:int}</c>, <c>{name?}</c>, <c>{id:int?}</c>.</summary>
    private static IReadOnlyList<RouteParameter> ParseRouteParameters(string route)
    {
        var result = new List<RouteParameter>();

        // Match {name}, {name:constraint}, {name?}, {name:constraint?}
        foreach (Match m in Regex.Matches(route, @"\{([^}]+)\}"))
        {
            var token = m.Groups[1].Value;
            bool isOptional = token.EndsWith('?');
            if (isOptional) token = token[..^1];

            var colonIdx = token.IndexOf(':');
            string paramName, constraint;
            if (colonIdx >= 0)
            {
                paramName = token[..colonIdx];
                constraint = token[(colonIdx + 1)..];
            }
            else
            {
                paramName = token;
                constraint = "";
            }

            result.Add(new RouteParameter(
                Name: paramName,
                Constraint: string.IsNullOrEmpty(constraint) ? null : constraint,
                IsOptional: isOptional));
        }

        return result;
    }
}
