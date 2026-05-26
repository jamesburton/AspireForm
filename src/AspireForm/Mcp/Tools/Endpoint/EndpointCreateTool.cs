using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.ApiCatalog;

namespace AspireForm.Mcp.Tools.Endpoint;

/// <summary>MCP tool: create a new <c>[ApiEndpoint]</c>-decorated handler method in a new file.</summary>
public sealed class EndpointCreateTool : IToolHandler
{
    private readonly string _defaultProjectDir;
    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };

    /// <summary>Creates the tool bound to <paramref name="defaultProjectDir"/>.</summary>
    public EndpointCreateTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_endpoint_create";

    /// <inheritdoc />
    public string Description => "Create a new [ApiEndpoint]-decorated handler method in the user's Web project.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["methodName"] = ToolBase.Str("Handler method name (e.g. 'GetBooks')."),
        ["typeName"] = ToolBase.Str("Handler class name (e.g. 'BooksHandler')."),
        ["route"] = ToolBase.Str("Route pattern (e.g. '/books/{id:int}')."),
        ["projectPath"] = ToolBase.Str("Absolute or relative path to the Web project's .csproj file."),
        ["httpMethod"] = ToolBase.Str("HTTP method: GET, POST, PUT, PATCH, DELETE. Default: GET."),
        ["filePath"] = ToolBase.Str("Optional: absolute path for the new .cs file."),
        ["namespace"] = ToolBase.Str("Optional: C# namespace for the handler class. Defaults to the project root namespace."),
    }, "methodName", "typeName", "route", "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var methodName = args["methodName"]?.GetValue<string>();
        var typeName = args["typeName"]?.GetValue<string>();
        var route = args["route"]?.GetValue<string>();
        var projectPath = args["projectPath"]?.GetValue<string>();
        var httpMethod = args["httpMethod"]?.GetValue<string>() ?? "GET";
        var filePath = args["filePath"]?.GetValue<string>();
        var ns = args["namespace"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(methodName) || string.IsNullOrWhiteSpace(typeName)
            || string.IsNullOrWhiteSpace(route) || string.IsNullOrWhiteSpace(projectPath))
            return ToolResult.Fail("aspireform_endpoint_create requires 'methodName', 'typeName', 'route', 'projectPath'.");

        var absProject = Path.GetFullPath(projectPath);
        var resolvedNs = ns ?? Path.GetFileNameWithoutExtension(absProject);
        var resolvedFile = filePath ?? Path.Combine(Path.GetDirectoryName(absProject)!, "Endpoints", $"{typeName}.cs");

        await using var svc = new RoslynEndpointCatalogService();
        var result = await svc.MutateAsync(absProject,
            new CreateEndpoint(methodName, typeName, route, httpMethod, resolvedFile, resolvedNs),
            ct);
        var json = JsonSerializer.Serialize(result, PrettyOptions);
        return result.Success ? ToolResult.Ok(json) : ToolResult.Fail(json);
    }
}
