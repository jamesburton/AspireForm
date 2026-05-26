using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.ApiCatalog;

namespace AspireForm.Mcp.Tools.Endpoint;

/// <summary>MCP tool: dump one endpoint's full record as indented JSON.</summary>
public sealed class EndpointShowTool : IToolHandler
{
    private readonly string _defaultProjectDir;
    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };

    /// <summary>Creates the tool bound to <paramref name="defaultProjectDir"/>.</summary>
    public EndpointShowTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_endpoint_show";

    /// <inheritdoc />
    public string Description => "Show one endpoint's full record (route, method, parameters, auth, attributes) as indented JSON.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["methodName"] = ToolBase.Str("Handler method name (e.g. 'GetBooks')."),
        ["projectPath"] = ToolBase.Str("Absolute or relative path to the Web project's .csproj file."),
        ["typeName"] = ToolBase.Str("Optional: handler class name to disambiguate if multiple classes have the same method name."),
    }, "methodName", "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var methodName = args["methodName"]?.GetValue<string>();
        var path = args["projectPath"]?.GetValue<string>();
        var typeName = args["typeName"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(methodName)) return ToolResult.Fail("aspireform_endpoint_show requires 'methodName'.");
        if (string.IsNullOrWhiteSpace(path)) return ToolResult.Fail("aspireform_endpoint_show requires 'projectPath'.");

        try
        {
            await using var svc = new RoslynEndpointCatalogService();
            var catalog = await svc.ScanAsync(path, ct);
            var ep = catalog.Endpoints.FirstOrDefault(e =>
                string.Equals(e.MethodName, methodName, StringComparison.Ordinal)
                && (typeName is null || string.Equals(e.HandlerTypeName, typeName, StringComparison.Ordinal)));
            if (ep is null) return ToolResult.Fail($"Endpoint '{methodName}' not found.");
            return ToolResult.Ok(JsonSerializer.Serialize(ep, PrettyOptions));
        }
        catch (EndpointCatalogException ex) { return ToolResult.Fail(ex.Message); }
    }
}
