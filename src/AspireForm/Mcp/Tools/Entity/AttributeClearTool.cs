using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;

namespace AspireForm.Mcp.Tools.Entity;

/// <summary>MCP tool: clear an attribute (by full type name) from an entity class or one of its properties.</summary>
public sealed class AttributeClearTool : IToolHandler
{
    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public AttributeClearTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_attribute_clear";

    /// <inheritdoc />
    public string Description => "Clear an attribute (by full type name) from an entity class or property.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["entity"] = ToolBase.Str("Entity class name."),
        ["property"] = ToolBase.Str("Optional property name; omit to clear at the class level."),
        ["attributeFullName"] = ToolBase.Str("Full attribute type name to remove."),
        ["projectPath"] = ToolBase.Str("Path to the entity project's .csproj."),
    }, "entity", "attributeFullName", "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var entity = args["entity"]?.GetValue<string>();
        var attrName = args["attributeFullName"]?.GetValue<string>();
        var projectPath = args["projectPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(entity) || string.IsNullOrWhiteSpace(attrName) || string.IsNullOrWhiteSpace(projectPath))
            return ToolResult.Fail("aspireform_attribute_clear requires 'entity', 'attributeFullName', 'projectPath'.");

        var property = args["property"]?.GetValue<string>();
        await using var svc = new RoslynEntityCatalogService();
        var result = await svc.MutateAsync(projectPath, new ClearAttribute(entity, property, attrName), ct);
        var json = JsonSerializer.Serialize(result, PrettyOptions);
        return result.Success ? ToolResult.Ok(json) : ToolResult.Fail(json);
    }
}
