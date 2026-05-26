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
        "Returns the active AspireForm UI theme token map as a JSON object. " +
        "Keys are token names (e.g. \"color-primary\"); values are hex color strings. " +
        "Reads from .aspireform/theme.json in the project directory; returns defaults when no override file exists.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["projectDir"] = ToolBase.Str("Project directory; defaults to the server's --project-dir."),
    });

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
        ToolBase.CatchKnownAsync(() =>
        {
            var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
            var store = new ThemeStore(projectDir);
            var tokens = store.GetTokens();
            var json = JsonSerializer.Serialize(tokens, new JsonSerializerOptions { WriteIndented = true });
            return Task.FromResult(ToolResult.Ok(json));
        });
}
