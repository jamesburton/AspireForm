using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;

namespace AspireForm.Mcp.Tools.Entity;

/// <summary>MCP tool: add a relationship between two entities. v1 supports OneToOne, OneToMany, ManyToOne; ManyToMany is reserved for #4a.1.</summary>
public sealed class RelationshipAddTool : IToolHandler
{
    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public RelationshipAddTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_relationship_add";

    /// <inheritdoc />
    public string Description => "Add a relationship from one entity to another. cardinality must be OneToOne | OneToMany | ManyToOne (ManyToMany is reserved for #4a.1).";

    /// <inheritdoc />
    public JsonObject InputSchema
    {
        get
        {
            var card = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray("OneToOne", "OneToMany", "ManyToOne", "ManyToMany"),
                ["description"] = "Cardinality of the relationship from the 'fromEntity' side.",
            };
            return ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
            {
                ["fromEntity"] = ToolBase.Str("Entity that the relationship originates from."),
                ["toEntity"] = ToolBase.Str("Entity that the relationship targets."),
                ["cardinality"] = card,
                ["foreignKeyProperty"] = ToolBase.Str("Optional explicit FK property name; v1 falls back to convention <ToEntity>Id."),
                ["projectPath"] = ToolBase.Str("Path to the entity project's .csproj."),
            }, "fromEntity", "toEntity", "cardinality", "projectPath");
        }
    }

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var fromEntity = args["fromEntity"]?.GetValue<string>();
        var toEntity = args["toEntity"]?.GetValue<string>();
        var cardStr = args["cardinality"]?.GetValue<string>();
        var projectPath = args["projectPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(fromEntity) || string.IsNullOrWhiteSpace(toEntity)
            || string.IsNullOrWhiteSpace(cardStr) || string.IsNullOrWhiteSpace(projectPath))
            return ToolResult.Fail("aspireform_relationship_add requires 'fromEntity', 'toEntity', 'cardinality', 'projectPath'.");

        if (!Enum.TryParse<RelationshipCardinality>(cardStr, out var card))
            return ToolResult.Fail($"Unknown cardinality '{cardStr}'. Allowed: OneToOne, OneToMany, ManyToOne, ManyToMany.");

        var fk = args["foreignKeyProperty"]?.GetValue<string>();
        await using var svc = new RoslynEntityCatalogService();
        var result = await svc.MutateAsync(projectPath, new AddRelationship(fromEntity, toEntity, card, fk), ct);
        var json = JsonSerializer.Serialize(result, PrettyOptions);
        return result.Success ? ToolResult.Ok(json) : ToolResult.Fail(json);
    }
}
