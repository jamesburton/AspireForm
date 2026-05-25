using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;

namespace AspireForm.Mcp.Tools.Entity;

/// <summary>MCP tool: append a new property to an entity class.</summary>
public sealed class PropertyAddTool : IToolHandler
{
    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public PropertyAddTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_property_add";

    /// <inheritdoc />
    public string Description => "Add a new property to an entity class.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["entity"] = ToolBase.Str("Entity class name."),
        ["name"] = ToolBase.Str("New property name."),
        ["clrType"] = ToolBase.Str("CLR type (e.g., 'int', 'string', 'DateOnly')."),
        ["isNullable"] = ToolBase.Bool("Whether the property is nullable (default: false)."),
        ["isPrimaryKey"] = ToolBase.Bool("Whether the property is the primary key (default: false)."),
        ["projectPath"] = ToolBase.Str("Path to the entity project's .csproj."),
    }, "entity", "name", "clrType", "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var entity = args["entity"]?.GetValue<string>();
        var name = args["name"]?.GetValue<string>();
        var clrType = args["clrType"]?.GetValue<string>();
        var projectPath = args["projectPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(entity) || string.IsNullOrWhiteSpace(name)
            || string.IsNullOrWhiteSpace(clrType) || string.IsNullOrWhiteSpace(projectPath))
            return ToolResult.Fail("aspireform_property_add requires 'entity', 'name', 'clrType', 'projectPath'.");

        var prop = new Property(
            Name: name,
            ClrType: clrType,
            IsNullable: args["isNullable"]?.GetValue<bool>() ?? false,
            IsPrimaryKey: args["isPrimaryKey"]?.GetValue<bool>() ?? false,
            Attributes: []);

        await using var svc = new RoslynEntityCatalogService();
        var result = await svc.MutateAsync(projectPath, new AddProperty(entity, prop), ct);
        var json = JsonSerializer.Serialize(result, PrettyOptions);
        return result.Success ? ToolResult.Ok(json) : ToolResult.Fail(json);
    }
}
