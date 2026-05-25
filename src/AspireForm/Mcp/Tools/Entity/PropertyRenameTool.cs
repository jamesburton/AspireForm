using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;

namespace AspireForm.Mcp.Tools.Entity;

/// <summary>MCP tool: rename a property (semantic-safe via Roslyn rename across the workspace).</summary>
public sealed class PropertyRenameTool : IToolHandler
{
    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public PropertyRenameTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_property_rename";

    /// <inheritdoc />
    public string Description => "Rename a property on an entity (semantic-safe across the whole workspace).";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["entity"] = ToolBase.Str("Entity class name."),
        ["oldName"] = ToolBase.Str("Current property name."),
        ["newName"] = ToolBase.Str("New property name."),
        ["projectPath"] = ToolBase.Str("Path to the entity project's .csproj."),
    }, "entity", "oldName", "newName", "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var entity = args["entity"]?.GetValue<string>();
        var oldName = args["oldName"]?.GetValue<string>();
        var newName = args["newName"]?.GetValue<string>();
        var projectPath = args["projectPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(entity) || string.IsNullOrWhiteSpace(oldName)
            || string.IsNullOrWhiteSpace(newName) || string.IsNullOrWhiteSpace(projectPath))
            return ToolResult.Fail("aspireform_property_rename requires 'entity', 'oldName', 'newName', 'projectPath'.");

        await using var svc = new RoslynEntityCatalogService();
        var result = await svc.MutateAsync(projectPath, new RenameProperty(entity, oldName, newName), ct);
        var json = JsonSerializer.Serialize(result, PrettyOptions);
        return result.Success ? ToolResult.Ok(json) : ToolResult.Fail(json);
    }
}
