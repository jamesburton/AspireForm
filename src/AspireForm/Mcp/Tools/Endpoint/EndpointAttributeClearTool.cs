using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.ApiCatalog;

namespace AspireForm.Mcp.Tools.Endpoint;

/// <summary>MCP tool: clear (remove) an attribute from an endpoint handler method.</summary>
public sealed class EndpointAttributeClearTool : IToolHandler
{
    private readonly string _defaultProjectDir;
    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };

    /// <summary>Creates the tool bound to <paramref name="defaultProjectDir"/>.</summary>
    public EndpointAttributeClearTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_endpoint_attribute_clear";

    /// <inheritdoc />
    public string Description => "Clear (remove) an attribute from an endpoint handler method by its full type name.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["methodName"] = ToolBase.Str("Handler method name."),
        ["attributeFullName"] = ToolBase.Str("Full type name of the attribute to remove (e.g. 'AspireForm.Annotations.ApiAuthAttribute')."),
        ["projectPath"] = ToolBase.Str("Absolute or relative path to the Web project's .csproj file."),
        ["typeName"] = ToolBase.Str("Optional: handler class name to disambiguate."),
    }, "methodName", "attributeFullName", "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var methodName = args["methodName"]?.GetValue<string>();
        var attributeFullName = args["attributeFullName"]?.GetValue<string>();
        var projectPath = args["projectPath"]?.GetValue<string>();
        var typeName = args["typeName"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(methodName) || string.IsNullOrWhiteSpace(attributeFullName)
            || string.IsNullOrWhiteSpace(projectPath))
            return ToolResult.Fail("aspireform_endpoint_attribute_clear requires 'methodName', 'attributeFullName', 'projectPath'.");

        await using var svc = new RoslynEndpointCatalogService();
        var result = await svc.MutateAsync(projectPath,
            new ClearEndpointAttribute(methodName, typeName, attributeFullName),
            ct);
        var json = JsonSerializer.Serialize(result, PrettyOptions);
        return result.Success ? ToolResult.Ok(json) : ToolResult.Fail(json);
    }
}
