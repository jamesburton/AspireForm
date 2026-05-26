using System.Text;
using System.Text.Json.Nodes;
using AspireForm.ApiCatalog;

namespace AspireForm.Mcp.Tools.Endpoint;

/// <summary>MCP tool: list all <c>[ApiEndpoint]</c>-decorated methods discovered in a user project.</summary>
public sealed class EndpointListTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool bound to <paramref name="defaultProjectDir"/>.</summary>
    public EndpointListTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_endpoint_list";

    /// <inheritdoc />
    public string Description => "List all [ApiEndpoint]-decorated methods discovered by Roslyn in the user's Web project csproj.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["projectPath"] = ToolBase.Str("Absolute or relative path to the Web project's .csproj file."),
    }, "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var path = args["projectPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(path))
            return ToolResult.Fail("aspireform_endpoint_list requires 'projectPath'.");

        try
        {
            await using var svc = new RoslynEndpointCatalogService();
            var catalog = await svc.ScanAsync(path, ct);
            var sb = new StringBuilder();
            sb.AppendLine($"Method               Type                         HttpMethod  Route");
            sb.AppendLine($"------               ----                         ----------  -----");
            foreach (var ep in catalog.Endpoints.OrderBy(e => e.Route, StringComparer.Ordinal))
            {
                sb.AppendLine($"{ep.MethodName,-21}{ep.HandlerTypeName,-29}{ep.HttpMethod,-12}{ep.Route}");
            }
            if (catalog.Endpoints.Count == 0) sb.AppendLine("(no endpoints found)");
            if (catalog.Diagnostics.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"{catalog.Diagnostics.Count} diagnostic(s) — check /diagnostics for detail.");
            }
            return ToolResult.Ok(sb.ToString());
        }
        catch (EndpointCatalogException ex) { return ToolResult.Fail(ex.Message); }
    }
}
