using AspireForm.EntityCatalog;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace AspireForm.ApiCatalog;

/// <summary>Roslyn-backed mutator for endpoint handler <c>.cs</c> files. Each <see cref="EndpointChangeRequest"/> applies transactionally.</summary>
public sealed class RoslynEndpointMutator
{
    private const string ApiEndpointAttributeFullName = "AspireForm.Annotations.ApiEndpointAttribute";

    /// <summary>Applies one mutation request transactionally against the project at <paramref name="csprojPath"/>.</summary>
    /// <param name="csprojPath">Absolute or relative path to the Web project's csproj file.</param>
    /// <param name="request">The mutation to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<EndpointMutationResult> ApplyAsync(string csprojPath, EndpointChangeRequest request, CancellationToken ct)
    {
        MSBuildBootstrap.EnsureRegistered();
        var absolute = Path.GetFullPath(csprojPath);
        if (!File.Exists(absolute))
        {
            return EndpointMutationResult.Fail($"Project file not found: '{absolute}'.", absolute);
        }

        // Buffered writes: path → new content (null means delete).
        var pending = new Dictionary<string, string?>(StringComparer.Ordinal);
        var diagnostics = new List<CatalogDiagnostic>();

        switch (request)
        {
            case CreateEndpoint create:
            {
                if (File.Exists(create.FilePath))
                {
                    return EndpointMutationResult.Fail($"Refusing to overwrite existing file '{create.FilePath}'.", create.FilePath);
                }
                pending[create.FilePath] = RenderNewEndpointFile(create);
                break;
            }

            case DeleteEndpoint delete:
            {
                using var ws = MSBuildWorkspace.Create();
                var project = await ws.OpenProjectAsync(absolute, cancellationToken: ct);
                var doc = await FindEndpointDocumentAsync(project, delete.MethodName, delete.TypeName, ct);
                if (doc is null)
                    return EndpointMutationResult.Fail($"Endpoint method '{delete.MethodName}' not found.");

                var tree = await doc.GetSyntaxTreeAsync(ct);
                var root = (CompilationUnitSyntax)(await tree!.GetRootAsync(ct));
                var methodNode = FindMethodNode(root, delete.MethodName, delete.TypeName);
                if (methodNode is null)
                    return EndpointMutationResult.Fail($"Method '{delete.MethodName}' not found in {doc.FilePath}.");

                var classNode = methodNode.Parent as ClassDeclarationSyntax;
                if (classNode is not null && classNode.Members.Count == 1)
                {
                    // Class becomes empty — delete the file.
                    pending[doc.FilePath!] = null;
                }
                else
                {
                    var newRoot = root.RemoveNode(methodNode, SyntaxRemoveOptions.KeepNoTrivia)!;
                    pending[doc.FilePath!] = newRoot.ToFullString();
                }
                break;
            }

            case AddParameter add:
            {
                using var ws = MSBuildWorkspace.Create();
                var project = await ws.OpenProjectAsync(absolute, cancellationToken: ct);
                var doc = await FindEndpointDocumentAsync(project, add.MethodName, add.TypeName, ct);
                if (doc is null)
                    return EndpointMutationResult.Fail($"Endpoint method '{add.MethodName}' not found.");

                var tree = await doc.GetSyntaxTreeAsync(ct);
                var root = (CompilationUnitSyntax)(await tree!.GetRootAsync(ct));
                var methodNode = FindMethodNode(root, add.MethodName, add.TypeName);
                if (methodNode is null)
                    return EndpointMutationResult.Fail($"Method '{add.MethodName}' not found in {doc.FilePath}.");

                if (IsExpressionBodied(methodNode))
                    return EndpointMutationResult.Fail("Expression-bodied methods are not supported for mutation in v1.", doc.FilePath);

                var newParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier(add.ParamName))
                    .WithType(SyntaxFactory.ParseTypeName(add.ClrType + " "));
                var newParams = methodNode.ParameterList.AddParameters(newParam);
                var newMethod = methodNode.WithParameterList(newParams);
                var newRoot = root.ReplaceNode(methodNode, newMethod);
                pending[doc.FilePath!] = newRoot.ToFullString();
                break;
            }

            case RemoveParameter remove:
            {
                using var ws = MSBuildWorkspace.Create();
                var project = await ws.OpenProjectAsync(absolute, cancellationToken: ct);
                var doc = await FindEndpointDocumentAsync(project, remove.MethodName, remove.TypeName, ct);
                if (doc is null)
                    return EndpointMutationResult.Fail($"Endpoint method '{remove.MethodName}' not found.");

                var tree = await doc.GetSyntaxTreeAsync(ct);
                var root = (CompilationUnitSyntax)(await tree!.GetRootAsync(ct));
                var methodNode = FindMethodNode(root, remove.MethodName, remove.TypeName);
                if (methodNode is null)
                    return EndpointMutationResult.Fail($"Method '{remove.MethodName}' not found in {doc.FilePath}.");

                if (IsExpressionBodied(methodNode))
                    return EndpointMutationResult.Fail("Expression-bodied methods are not supported for mutation in v1.", doc.FilePath);

                var paramNode = methodNode.ParameterList.Parameters
                    .FirstOrDefault(p => p.Identifier.Text == remove.ParamName);
                if (paramNode is null)
                    return EndpointMutationResult.Fail($"Parameter '{remove.ParamName}' not found on method '{remove.MethodName}'.");

                var newParams = methodNode.ParameterList.RemoveNode(paramNode, SyntaxRemoveOptions.KeepNoTrivia)!;
                var newMethod = methodNode.WithParameterList(newParams);
                var newRoot = root.ReplaceNode(methodNode, newMethod);
                pending[doc.FilePath!] = newRoot.ToFullString();
                break;
            }

            case SetEndpointAttribute setAttr:
            {
                using var ws = MSBuildWorkspace.Create();
                var project = await ws.OpenProjectAsync(absolute, cancellationToken: ct);
                var doc = await FindEndpointDocumentAsync(project, setAttr.MethodName, setAttr.TypeName, ct);
                if (doc is null)
                    return EndpointMutationResult.Fail($"Endpoint method '{setAttr.MethodName}' not found.");

                var tree = await doc.GetSyntaxTreeAsync(ct);
                var root = (CompilationUnitSyntax)(await tree!.GetRootAsync(ct));
                var methodNode = FindMethodNode(root, setAttr.MethodName, setAttr.TypeName);
                if (methodNode is null)
                    return EndpointMutationResult.Fail($"Method '{setAttr.MethodName}' not found in {doc.FilePath}.");

                // Remove any existing attribute of the same type, then add the new one.
                var cleaned = WithoutAttributeOnMethod(methodNode, setAttr.Attribute.FullTypeName);
                var attrSrc = RenderAttribute(setAttr.Attribute);
                var attrList = SyntaxFactory.ParseCompilationUnit($"{attrSrc}\nvoid Dummy() {{ }}").Members
                    .OfType<GlobalStatementSyntax>().FirstOrDefault()?.Statement
                    is null
                        // Fall back: parse as attribute list directly.
                        ? (AttributeListSyntax)SyntaxFactory.ParseMemberDeclaration($"{attrSrc}\nvoid D(){{}}")!
                            .DescendantNodes().OfType<AttributeListSyntax>().First()
                        : (AttributeListSyntax)SyntaxFactory.ParseMemberDeclaration($"{attrSrc}\nvoid D(){{}}")!
                            .DescendantNodes().OfType<AttributeListSyntax>().First();
                var newMethod = cleaned.AddAttributeLists(attrList);
                var newRoot = root.ReplaceNode(methodNode, newMethod);
                pending[doc.FilePath!] = newRoot.ToFullString();
                break;
            }

            case ClearEndpointAttribute clearAttr:
            {
                using var ws = MSBuildWorkspace.Create();
                var project = await ws.OpenProjectAsync(absolute, cancellationToken: ct);
                var doc = await FindEndpointDocumentAsync(project, clearAttr.MethodName, clearAttr.TypeName, ct);
                if (doc is null)
                    return EndpointMutationResult.Fail($"Endpoint method '{clearAttr.MethodName}' not found.");

                var tree = await doc.GetSyntaxTreeAsync(ct);
                var root = (CompilationUnitSyntax)(await tree!.GetRootAsync(ct));
                var methodNode = FindMethodNode(root, clearAttr.MethodName, clearAttr.TypeName);
                if (methodNode is null)
                    return EndpointMutationResult.Fail($"Method '{clearAttr.MethodName}' not found in {doc.FilePath}.");

                var newMethod = WithoutAttributeOnMethod(methodNode, clearAttr.AttributeFullTypeName);
                var newRoot = root.ReplaceNode(methodNode, newMethod);
                pending[doc.FilePath!] = newRoot.ToFullString();
                break;
            }

            case SetAuthPolicy setAuth:
            {
                // Shorthand: delegate to SetEndpointAttribute with ApiAuthAttribute.
                var attr = new AttributeInstance(
                    "AspireForm.Annotations.ApiAuthAttribute",
                    [setAuth.Policy],
                    new Dictionary<string, object?>());
                return await ApplyAsync(csprojPath, new SetEndpointAttribute(setAuth.MethodName, setAuth.TypeName, attr), ct);
            }

            default:
                return EndpointMutationResult.Fail($"Mutation '{request.GetType().Name}' is not implemented.");
        }

        return CommitWrites(pending, diagnostics);
    }

    private static EndpointMutationResult CommitWrites(IDictionary<string, string?> pending, List<CatalogDiagnostic> diagnostics)
    {
        var changed = new List<string>();
        try
        {
            foreach (var (path, content) in pending)
            {
                if (content is null)
                {
                    if (File.Exists(path)) File.Delete(path);
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, content);
                }
                changed.Add(path);
            }
            return new EndpointMutationResult(true, changed, diagnostics);
        }
        catch (Exception ex)
        {
            return EndpointMutationResult.Fail($"Commit failed after {changed.Count} file(s): {ex.Message}");
        }
    }

    private static async Task<Document?> FindEndpointDocumentAsync(Project project, string methodName, string? typeName, CancellationToken ct)
    {
        var compilation = await project.GetCompilationAsync(ct);
        if (compilation is null) return null;

        foreach (var type in compilation.Assembly.GlobalNamespace.GetAllTypes())
        {
            if (typeName is not null && type.Name != typeName) continue;
            var method = type.GetMembers().OfType<IMethodSymbol>()
                .FirstOrDefault(m => m.Name == methodName);
            if (method is null) continue;
            var path = method.Locations.FirstOrDefault()?.SourceTree?.FilePath;
            if (path is null) continue;
            return project.Documents.FirstOrDefault(d => string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase));
        }
        return null;
    }

    private static MethodDeclarationSyntax? FindMethodNode(CompilationUnitSyntax root, string methodName, string? typeName)
    {
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.Text == methodName);
        if (typeName is not null)
        {
            methods = methods.Where(m => m.Parent is ClassDeclarationSyntax c && c.Identifier.Text == typeName);
        }
        return methods.FirstOrDefault();
    }

    private static bool IsExpressionBodied(MethodDeclarationSyntax method) =>
        method.ExpressionBody is not null;

    private static MethodDeclarationSyntax WithoutAttributeOnMethod(MethodDeclarationSyntax method, string attributeFullTypeName)
    {
        var shortName = attributeFullTypeName.Split('.').Last();
        if (shortName.EndsWith("Attribute", StringComparison.Ordinal))
            shortName = shortName[..^"Attribute".Length];

        foreach (var list in method.AttributeLists.ToList())
        {
            var keep = list.Attributes.Where(a =>
            {
                var name = a.Name.ToString().Split('.').Last();
                return name != shortName && name != shortName + "Attribute";
            }).ToList();

            if (keep.Count == list.Attributes.Count) continue;

            if (keep.Count == 0)
                method = method.RemoveNode(list, SyntaxRemoveOptions.KeepNoTrivia)!;
            else
                method = method.ReplaceNode(list, list.WithAttributes(SyntaxFactory.SeparatedList(keep)));
        }
        return method;
    }

    private static string RenderNewEndpointFile(CreateEndpoint req) => $$"""
        using AspireForm.Annotations;
        using Microsoft.AspNetCore.Http;

        namespace {{req.Namespace}};

        public static class {{req.TypeName}}
        {
            [ApiEndpoint("{{req.Route}}", "{{req.HttpMethod}}")]
            public static IResult {{req.MethodName}}(HttpContext ctx) => Results.Ok();
        }
        """;

    private static string RenderAttribute(AttributeInstance a)
    {
        var shortName = a.FullTypeName.Split('.').Last();
        if (shortName.EndsWith("Attribute", StringComparison.Ordinal))
            shortName = shortName[..^"Attribute".Length];
        var args = new List<string>();
        foreach (var ctor in a.ConstructorArgs)
            args.Add(FormatLiteral(ctor));
        foreach (var (k, v) in a.NamedArgs)
            args.Add($"{k} = {FormatLiteral(v)}");
        var body = args.Count == 0 ? "" : $"({string.Join(", ", args)})";
        return $"[{shortName}{body}]";
    }

    private static string FormatLiteral(object? v) => v switch
    {
        null => "null",
        string s => $"\"{s.Replace("\"", "\\\"")}\"",
        bool b => b ? "true" : "false",
        char c => $"'{c}'",
        _ => v?.ToString() ?? "null",
    };
}
