using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;

namespace AspireForm.Mcp.Tools.Entity;

/// <summary>MCP tool: create a new entity class in the user project.</summary>
public sealed class EntityCreateTool : IToolHandler
{
    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory (currently unused).</summary>
    public EntityCreateTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_entity_create";

    /// <inheritdoc />
    public string Description => "Create a new entity class file in the user project.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["name"] = ToolBase.Str("Entity class name (e.g., 'Book')."),
        ["namespace"] = ToolBase.Str("Target C# namespace."),
        ["filePath"] = ToolBase.Str("Absolute or project-relative path to the new .cs file."),
        ["projectPath"] = ToolBase.Str("Path to the entity project's .csproj."),
    }, "name", "namespace", "filePath", "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var name = args["name"]?.GetValue<string>();
        var ns = args["namespace"]?.GetValue<string>();
        var filePath = args["filePath"]?.GetValue<string>();
        var projectPath = args["projectPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(ns)
            || string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(projectPath))
            return ToolResult.Fail("aspireform_entity_create requires 'name', 'namespace', 'filePath', 'projectPath'.");

        await using var svc = new RoslynEntityCatalogService();
        var result = await svc.MutateAsync(projectPath, new CreateEntity(name, ns, filePath), ct);
        var json = JsonSerializer.Serialize(result, PrettyOptions);
        return result.Success ? ToolResult.Ok(json) : ToolResult.Fail(json);
    }
}
