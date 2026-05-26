using System.Text;
using System.Text.Json.Nodes;
using AspireForm.Ui.Theme;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: lists all available themes, marking the currently active one.</summary>
public sealed class ThemeListTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public ThemeListTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "aspireform_theme_list";

    /// <inheritdoc />
    public string Description =>
        "Lists all AspireForm UI themes with their names, descriptions, and active status.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["projectDir"] = ToolBase.Str("Project directory; defaults to the server's --project-dir."),
    });

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
        ToolBase.CatchKnownAsync(async () =>
        {
            var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
            var store = new ThemeStore(projectDir);
            var themes = await store.ListAsync(ct);

            if (themes.Count == 0)
                return ToolResult.Ok("No themes found.");

            var sb = new StringBuilder();
            sb.AppendLine($"{"Name",-30} {"Active",-6} Description");
            sb.AppendLine($"{"----",-30} {"------",-6} -----------");
            foreach (var t in themes)
            {
                sb.AppendLine($"{Pad(t.Name, 30)} {(t.IsActive ? "✓" : ""),-6} {t.Description}");
            }

            return ToolResult.Ok(sb.ToString());

            static string Pad(string s, int width) => s.Length >= width ? s : s.PadRight(width);
        });
}
