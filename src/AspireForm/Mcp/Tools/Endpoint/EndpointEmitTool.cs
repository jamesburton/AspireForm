using System.Text.Json.Nodes;
using AspireForm.ApiCatalog;

namespace AspireForm.Mcp.Tools.Endpoint;

/// <summary>MCP tool: scan a project and emit <c>_Endpoints.g.cs</c>.</summary>
public sealed class EndpointEmitTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool bound to <paramref name="defaultProjectDir"/>.</summary>
    public EndpointEmitTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_endpoint_emit";

    /// <inheritdoc />
    public string Description => "Scan the Web project for [ApiEndpoint] methods and emit _Endpoints.g.cs. Returns the emitted file content.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["projectPath"] = ToolBase.Str("Absolute or relative path to the Web project's .csproj file."),
        ["outputPath"] = ToolBase.Str("Optional: absolute path for the output file. Defaults to {projectDir}/Generated/_Endpoints.g.cs."),
    }, "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var path = args["projectPath"]?.GetValue<string>();
        var outputPath = args["outputPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(path))
            return ToolResult.Fail("aspireform_endpoint_emit requires 'projectPath'.");

        try
        {
            await using var svc = new RoslynEndpointCatalogService();
            var catalog = await svc.ScanAsync(path, ct);
            var rootNamespace = Path.GetFileNameWithoutExtension(Path.GetFullPath(path));
            var content = EndpointEmitter.Render(catalog, rootNamespace);

            var absoluteOutput = !string.IsNullOrWhiteSpace(outputPath)
                ? Path.GetFullPath(outputPath)
                : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, "Generated", "_Endpoints.g.cs");

            Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutput)!);
            File.WriteAllText(absoluteOutput, content);

            return ToolResult.Ok($"Emitted {absoluteOutput}\n\n{content}");
        }
        catch (EndpointCatalogException ex) { return ToolResult.Fail(ex.Message); }
    }
}
