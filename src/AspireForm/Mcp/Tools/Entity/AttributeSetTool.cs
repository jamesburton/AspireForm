using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;

namespace AspireForm.Mcp.Tools.Entity;

/// <summary>MCP tool: set (replace if present) an attribute on an entity class or one of its properties.</summary>
public sealed class AttributeSetTool : IToolHandler
{
    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public AttributeSetTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_attribute_set";

    /// <inheritdoc />
    public string Description => "Set (or replace) an attribute on an entity class or one of its properties.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["entity"] = ToolBase.Str("Entity class name."),
        ["property"] = ToolBase.Str("Optional property name; omit to apply at the class level."),
        ["attributeFullName"] = ToolBase.Str("Full attribute type name (e.g., 'AspireForm.Annotations.DabExposeAttribute')."),
        ["ctorArgs"] = new JsonObject { ["type"] = "array", ["description"] = "Positional constructor args (strings, numbers, booleans)." },
        ["namedArgs"] = new JsonObject { ["type"] = "object", ["description"] = "Named constructor args (name → value map)." },
        ["projectPath"] = ToolBase.Str("Path to the entity project's .csproj."),
    }, "entity", "attributeFullName", "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var entity = args["entity"]?.GetValue<string>();
        var attrName = args["attributeFullName"]?.GetValue<string>();
        var projectPath = args["projectPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(entity) || string.IsNullOrWhiteSpace(attrName) || string.IsNullOrWhiteSpace(projectPath))
            return ToolResult.Fail("aspireform_attribute_set requires 'entity', 'attributeFullName', 'projectPath'.");

        var property = args["property"]?.GetValue<string>();
        var ctorArgs = (args["ctorArgs"] as JsonArray)?
            .Select(n => (object?)UnwrapJsonScalar(n))
            .ToList() ?? [];
        var namedArgs = (args["namedArgs"] as JsonObject)?
            .ToDictionary(kv => kv.Key, kv => (object?)UnwrapJsonScalar(kv.Value))
            ?? new Dictionary<string, object?>();

        var attr = new AttributeInstance(attrName, ctorArgs, namedArgs);
        await using var svc = new RoslynEntityCatalogService();
        var result = await svc.MutateAsync(projectPath, new SetAttribute(entity, property, attr), ct);
        var json = JsonSerializer.Serialize(result, PrettyOptions);
        return result.Success ? ToolResult.Ok(json) : ToolResult.Fail(json);
    }

    private static object? UnwrapJsonScalar(JsonNode? n) => n switch
    {
        null => null,
        JsonValue v when v.TryGetValue(out bool b) => b,
        JsonValue v when v.TryGetValue(out int i) => i,
        JsonValue v when v.TryGetValue(out long l) => l,
        JsonValue v when v.TryGetValue(out double d) => d,
        JsonValue v when v.TryGetValue(out string? s) => s,
        _ => n.ToString(),
    };
}
