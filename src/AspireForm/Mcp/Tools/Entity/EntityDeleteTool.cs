using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;

namespace AspireForm.Mcp.Tools.Entity;

/// <summary>MCP tool: delete an entity class file. DbSet&lt;T&gt; + reverse navigations must be cleaned up manually in v1 (a warning is included in the result diagnostics).</summary>
public sealed class EntityDeleteTool : IToolHandler
{
    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public EntityDeleteTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_entity_delete";

    /// <inheritdoc />
    public string Description => "Delete an entity class (.cs file). DbSet<T> + reverse navigations must be cleaned up manually in v1.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["entity"] = ToolBase.Str("Entity class name to delete."),
        ["projectPath"] = ToolBase.Str("Path to the entity project's .csproj."),
    }, "entity", "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var name = args["entity"]?.GetValue<string>();
        var projectPath = args["projectPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(projectPath))
            return ToolResult.Fail("aspireform_entity_delete requires 'entity', 'projectPath'.");

        await using var svc = new RoslynEntityCatalogService();
        var result = await svc.MutateAsync(projectPath, new DeleteEntity(name), ct);
        var json = JsonSerializer.Serialize(result, PrettyOptions);
        return result.Success ? ToolResult.Ok(json) : ToolResult.Fail(json);
    }
}
