using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;

namespace AspireForm.Mcp.Tools.Entity;

/// <summary>MCP tool: dump one entity's full record as indented JSON.</summary>
public sealed class EntityShowTool : IToolHandler
{
    private readonly string _defaultProjectDir;
    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };

    /// <summary>Creates the tool with a default project directory (currently unused).</summary>
    public EntityShowTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_entity_show";

    /// <inheritdoc />
    public string Description => "Show one entity's full record (properties, relationships, attributes) as indented JSON.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["entity"] = ToolBase.Str("Entity name."),
        ["projectPath"] = ToolBase.Str("Absolute or relative path to the entity project's .csproj file."),
    }, "entity", "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var name = args["entity"]?.GetValue<string>();
        var path = args["projectPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name)) return ToolResult.Fail("aspireform_entity_show requires 'entity'.");
        if (string.IsNullOrWhiteSpace(path)) return ToolResult.Fail("aspireform_entity_show requires 'projectPath'.");

        try
        {
            await using var svc = new RoslynEntityCatalogService();
            var catalog = await svc.ScanAsync(path, ct);
            var entity = catalog.Entities.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal));
            if (entity is null) return ToolResult.Fail($"Entity '{name}' not found.");
            return ToolResult.Ok(JsonSerializer.Serialize(entity, PrettyOptions));
        }
        catch (EntityCatalogException ex) { return ToolResult.Fail(ex.Message); }
    }
}
