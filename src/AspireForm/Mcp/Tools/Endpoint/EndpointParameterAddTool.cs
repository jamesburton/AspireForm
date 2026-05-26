using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.ApiCatalog;

namespace AspireForm.Mcp.Tools.Endpoint;

/// <summary>MCP tool: add a typed parameter to an endpoint handler method's signature.</summary>
public sealed class EndpointParameterAddTool : IToolHandler
{
    private readonly string _defaultProjectDir;
    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };

    /// <summary>Creates the tool bound to <paramref name="defaultProjectDir"/>.</summary>
    public EndpointParameterAddTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_endpoint_parameter_add";

    /// <inheritdoc />
    public string Description => "Add a typed parameter to an endpoint handler method signature.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["methodName"] = ToolBase.Str("Handler method name."),
        ["paramName"] = ToolBase.Str("Parameter name to add."),
        ["clrType"] = ToolBase.Str("CLR type name (e.g. 'int', 'string', 'HttpContext')."),
        ["projectPath"] = ToolBase.Str("Absolute or relative path to the Web project's .csproj file."),
        ["typeName"] = ToolBase.Str("Optional: handler class name to disambiguate."),
    }, "methodName", "paramName", "clrType", "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var methodName = args["methodName"]?.GetValue<string>();
        var paramName = args["paramName"]?.GetValue<string>();
        var clrType = args["clrType"]?.GetValue<string>();
        var projectPath = args["projectPath"]?.GetValue<string>();
        var typeName = args["typeName"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(methodName) || string.IsNullOrWhiteSpace(paramName)
            || string.IsNullOrWhiteSpace(clrType) || string.IsNullOrWhiteSpace(projectPath))
            return ToolResult.Fail("aspireform_endpoint_parameter_add requires 'methodName', 'paramName', 'clrType', 'projectPath'.");

        await using var svc = new RoslynEndpointCatalogService();
        var result = await svc.MutateAsync(projectPath, new AddParameter(methodName, typeName, paramName, clrType), ct);
        var json = JsonSerializer.Serialize(result, PrettyOptions);
        return result.Success ? ToolResult.Ok(json) : ToolResult.Fail(json);
    }
}
