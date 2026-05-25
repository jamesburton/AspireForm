using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;

namespace AspireForm.Mcp.Tools.Entity;

/// <summary>MCP tool: remove a relationship's navigation property from the originating entity.</summary>
public sealed class RelationshipRemoveTool : IToolHandler
{
    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public RelationshipRemoveTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_relationship_remove";

    /// <inheritdoc />
    public string Description => "Remove a relationship's navigation property from the originating entity. (v1: only removes the named nav; FK + reverse nav need manual cleanup.)";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["fromEntity"] = ToolBase.Str("Entity that the relationship originates from."),
        ["relationshipName"] = ToolBase.Str("Navigation property name to remove."),
        ["projectPath"] = ToolBase.Str("Path to the entity project's .csproj."),
    }, "fromEntity", "relationshipName", "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var fromEntity = args["fromEntity"]?.GetValue<string>();
        var rel = args["relationshipName"]?.GetValue<string>();
        var projectPath = args["projectPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(fromEntity) || string.IsNullOrWhiteSpace(rel) || string.IsNullOrWhiteSpace(projectPath))
            return ToolResult.Fail("aspireform_relationship_remove requires 'fromEntity', 'relationshipName', 'projectPath'.");

        await using var svc = new RoslynEntityCatalogService();
        var result = await svc.MutateAsync(projectPath, new RemoveRelationship(fromEntity, rel), ct);
        var json = JsonSerializer.Serialize(result, PrettyOptions);
        return result.Success ? ToolResult.Ok(json) : ToolResult.Fail(json);
    }
}
