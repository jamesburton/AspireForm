using System.Text;
using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;

namespace AspireForm.Mcp.Tools.Entity;

/// <summary>MCP tool: list all entities discovered in a user project's csproj.</summary>
public sealed class EntityListTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory (currently unused — entity tools require projectPath explicitly).</summary>
    public EntityListTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_entity_list";

    /// <inheritdoc />
    public string Description => "List all entities discovered by Roslyn in the user project's csproj.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["projectPath"] = ToolBase.Str("Absolute or relative path to the entity project's .csproj file."),
    }, "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var path = args["projectPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(path))
            return ToolResult.Fail("aspireform_entity_list requires 'projectPath'.");

        try
        {
            await using var svc = new RoslynEntityCatalogService();
            var catalog = await svc.ScanAsync(path, ct);
            var sb = new StringBuilder();
            sb.AppendLine($"Entity                Namespace                   Properties  Relationships  DabExposed");
            sb.AppendLine($"------                ---------                   ----------  -------------  ----------");
            foreach (var e in catalog.Entities.OrderBy(e => e.Name, StringComparer.Ordinal))
            {
                var dabExposed = e.Attributes.Any(a => a.FullTypeName == "AspireForm.Annotations.DabExposeAttribute") ? "yes" : "no";
                sb.AppendLine($"{e.Name,-22}{e.Namespace,-28}{e.Properties.Count,-12}{e.Relationships.Count,-15}{dabExposed}");
            }
            if (catalog.Entities.Count == 0) sb.AppendLine("(no entities found)");
            if (catalog.Diagnostics.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"{catalog.Diagnostics.Count} diagnostic(s) — call aspireform_entity_show or check /diagnostics for detail.");
            }
            return ToolResult.Ok(sb.ToString());
        }
        catch (EntityCatalogException ex) { return ToolResult.Fail(ex.Message); }
    }
}
