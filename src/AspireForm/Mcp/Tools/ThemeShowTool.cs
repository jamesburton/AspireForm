using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.Ui.Theme;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: returns the currently active theme token map as a JSON object.
/// Read-only — use <c>aspireform ui</c> to edit tokens interactively.</summary>
public sealed class ThemeShowTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public ThemeShowTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "aspireform_theme_show";

    /// <inheritdoc />
    public string Description =>
        "Returns the active AspireForm UI theme. " +
        "Includes active theme name, dark-mode flag, all theme names, full token set (light + dark), and radius.";

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
            var activation = await store.GetActiveAsync(ct);
            var theme = await store.GetAsync(activation.ActiveName, ct);
            var themes = await store.ListAsync(ct);
            var result = new
            {
                activeName = activation.ActiveName,
                darkMode = activation.DarkMode,
                allThemes = themes.Select(t => t.Name).ToArray(),
                tokens = new { light = theme.Light, dark = theme.Dark },
                radius = theme.Radius,
            };
            return ToolResult.Ok(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        });
}
