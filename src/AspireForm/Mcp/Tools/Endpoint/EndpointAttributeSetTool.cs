using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.ApiCatalog;
using AspireForm.EntityCatalog;

namespace AspireForm.Mcp.Tools.Endpoint;

/// <summary>MCP tool: set (add or replace) an attribute on an endpoint handler method.</summary>
public sealed class EndpointAttributeSetTool : IToolHandler
{
    private readonly string _defaultProjectDir;
    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };

    /// <summary>Creates the tool bound to <paramref name="defaultProjectDir"/>.</summary>
    public EndpointAttributeSetTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_endpoint_attribute_set";

    /// <inheritdoc />
    public string Description => "Set (add or replace) an attribute on an endpoint handler method. The attribute is identified by its full type name.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["methodName"] = ToolBase.Str("Handler method name."),
        ["attributeFullName"] = ToolBase.Str("Full type name of the attribute (e.g. 'AspireForm.Annotations.ApiTagAttribute')."),
        ["projectPath"] = ToolBase.Str("Absolute or relative path to the Web project's .csproj file."),
        ["typeName"] = ToolBase.Str("Optional: handler class name to disambiguate."),
        ["ctorArgs"] = ToolBase.StrArray("Optional: constructor argument values."),
        ["namedArgs"] = ToolBase.Str("Optional: JSON object of named argument key-value pairs."),
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
            return ToolResult.Fail("aspireform_endpoint_attribute_set requires 'methodName', 'attributeFullName', 'projectPath'.");

        var ctorArgs = (args["ctorArgs"] as JsonArray)
            ?.Select(n => (object?)n?.GetValue<string>())
            .ToList() ?? (IReadOnlyList<object?>)[];

        var namedArgsJson = args["namedArgs"]?.AsObject();
        var namedArgs = namedArgsJson is null
            ? (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>()
            : namedArgsJson.ToDictionary(kv => kv.Key, kv => (object?)kv.Value?.GetValue<string>());

        var attr = new AttributeInstance(attributeFullName, ctorArgs, namedArgs);

        await using var svc = new RoslynEndpointCatalogService();
        var result = await svc.MutateAsync(projectPath, new SetEndpointAttribute(methodName, typeName, attr), ct);
        var json = JsonSerializer.Serialize(result, PrettyOptions);
        return result.Success ? ToolResult.Ok(json) : ToolResult.Fail(json);
    }
}
