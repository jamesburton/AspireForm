using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.ApiCatalog;

namespace AspireForm.Mcp.Tools.Endpoint;

/// <summary>MCP tool: set the <c>[ApiAuth]</c> authorization policy on an endpoint handler method.</summary>
public sealed class EndpointAuthSetTool : IToolHandler
{
    private readonly string _defaultProjectDir;
    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };

    /// <summary>Creates the tool bound to <paramref name="defaultProjectDir"/>.</summary>
    public EndpointAuthSetTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_endpoint_auth_set";

    /// <inheritdoc />
    public string Description => "Set (or replace) the [ApiAuth] authorization policy on an endpoint handler method. Use 'anonymous' to allow unauthenticated access.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["methodName"] = ToolBase.Str("Handler method name."),
        ["policy"] = ToolBase.Str("Authorization policy name. Use 'anonymous' to allow unauthenticated access."),
        ["projectPath"] = ToolBase.Str("Absolute or relative path to the Web project's .csproj file."),
        ["typeName"] = ToolBase.Str("Optional: handler class name to disambiguate."),
    }, "methodName", "policy", "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var methodName = args["methodName"]?.GetValue<string>();
        var policy = args["policy"]?.GetValue<string>();
        var projectPath = args["projectPath"]?.GetValue<string>();
        var typeName = args["typeName"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(methodName) || string.IsNullOrWhiteSpace(policy)
            || string.IsNullOrWhiteSpace(projectPath))
            return ToolResult.Fail("aspireform_endpoint_auth_set requires 'methodName', 'policy', 'projectPath'.");

        await using var svc = new RoslynEndpointCatalogService();
        var result = await svc.MutateAsync(projectPath, new SetAuthPolicy(methodName, typeName, policy), ct);
        var json = JsonSerializer.Serialize(result, PrettyOptions);
        return result.Success ? ToolResult.Ok(json) : ToolResult.Fail(json);
    }
}
