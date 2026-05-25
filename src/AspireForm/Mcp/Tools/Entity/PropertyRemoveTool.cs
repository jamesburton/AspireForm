using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;

namespace AspireForm.Mcp.Tools.Entity;

/// <summary>MCP tool: remove a property from an entity class.</summary>
public sealed class PropertyRemoveTool : IToolHandler
{
    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public PropertyRemoveTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_property_remove";

    /// <inheritdoc />
    public string Description => "Remove a property from an entity class.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["entity"] = ToolBase.Str("Entity class name."),
        ["property"] = ToolBase.Str("Property name to remove."),
        ["projectPath"] = ToolBase.Str("Path to the entity project's .csproj."),
    }, "entity", "property", "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var entity = args["entity"]?.GetValue<string>();
        var property = args["property"]?.GetValue<string>();
        var projectPath = args["projectPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(entity) || string.IsNullOrWhiteSpace(property) || string.IsNullOrWhiteSpace(projectPath))
            return ToolResult.Fail("aspireform_property_remove requires 'entity', 'property', 'projectPath'.");

        await using var svc = new RoslynEntityCatalogService();
        var result = await svc.MutateAsync(projectPath, new RemoveProperty(entity, property), ct);
        var json = JsonSerializer.Serialize(result, PrettyOptions);
        return result.Success ? ToolResult.Ok(json) : ToolResult.Fail(json);
    }
}
