using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.ApiCatalog;

namespace AspireForm.Mcp.Tools.Endpoint;

/// <summary>MCP tool: delete an <c>[ApiEndpoint]</c>-decorated handler method from the project.</summary>
public sealed class EndpointDeleteTool : IToolHandler
{
    private readonly string _defaultProjectDir;
    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };

    /// <summary>Creates the tool bound to <paramref name="defaultProjectDir"/>.</summary>
    public EndpointDeleteTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_endpoint_delete";

    /// <inheritdoc />
    public string Description => "Delete an [ApiEndpoint]-decorated handler method from the user's Web project.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["methodName"] = ToolBase.Str("Handler method name to delete."),
        ["projectPath"] = ToolBase.Str("Absolute or relative path to the Web project's .csproj file."),
        ["typeName"] = ToolBase.Str("Optional: handler class name to disambiguate."),
    }, "methodName", "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var methodName = args["methodName"]?.GetValue<string>();
        var projectPath = args["projectPath"]?.GetValue<string>();
        var typeName = args["typeName"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(methodName)) return ToolResult.Fail("aspireform_endpoint_delete requires 'methodName'.");
        if (string.IsNullOrWhiteSpace(projectPath)) return ToolResult.Fail("aspireform_endpoint_delete requires 'projectPath'.");

        await using var svc = new RoslynEndpointCatalogService();
        var result = await svc.MutateAsync(projectPath,
            new DeleteEndpoint(methodName, typeName),
            ct);
        var json = JsonSerializer.Serialize(result, PrettyOptions);
        return result.Success ? ToolResult.Ok(json) : ToolResult.Fail(json);
    }
}
