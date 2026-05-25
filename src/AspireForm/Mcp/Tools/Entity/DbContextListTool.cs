using System.Text;
using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;

namespace AspireForm.Mcp.Tools.Entity;

/// <summary>MCP tool: list all DbContext-derived classes discovered in the user's csproj.</summary>
public sealed class DbContextListTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory (currently unused).</summary>
    public DbContextListTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_dbcontext_list";

    /// <inheritdoc />
    public string Description => "List all DbContext-derived classes in the user project, with their DbSet<T> entity names.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["projectPath"] = ToolBase.Str("Absolute or relative path to the entity project's .csproj file."),
    }, "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var path = args["projectPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(path)) return ToolResult.Fail("aspireform_dbcontext_list requires 'projectPath'.");

        try
        {
            await using var svc = new RoslynEntityCatalogService();
            var catalog = await svc.ScanAsync(path, ct);
            var sb = new StringBuilder();
            sb.AppendLine($"DbContext                   Namespace                   DbSet<T> entities");
            sb.AppendLine($"---------                   ---------                   -----------------");
            foreach (var c in catalog.DbContexts.OrderBy(c => c.Name, StringComparer.Ordinal))
            {
                sb.AppendLine($"{c.Name,-28}{c.Namespace,-28}{string.Join(", ", c.DbSetEntityNames)}");
            }
            if (catalog.DbContexts.Count == 0) sb.AppendLine("(no DbContext detected)");
            return ToolResult.Ok(sb.ToString());
        }
        catch (EntityCatalogException ex) { return ToolResult.Fail(ex.Message); }
    }
}
