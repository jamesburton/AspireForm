using System.Text.Json.Nodes;
using AspireForm.Ui.Theme;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: sets the active AspireForm UI theme by name.</summary>
public sealed class ThemeActivateTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public ThemeActivateTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "aspireform_theme_activate";

    /// <inheritdoc />
    public string Description =>
        "Sets the active AspireForm UI theme by name. Use aspireform_theme_list to see available themes.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(
        new Dictionary<string, JsonObject>
        {
            ["name"]       = ToolBase.Str("Name of the theme to activate (case-sensitive)."),
            ["projectDir"] = ToolBase.Str("Project directory; defaults to the server's --project-dir."),
        },
        "name");

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
        ToolBase.CatchKnownAsync(async () =>
        {
            var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
            var name = args["name"]?.GetValue<string>()
                       ?? throw new ArgumentException("'name' is required.");

            var store = new ThemeStore(projectDir);
            await store.SetActiveAsync(name, ct);
            return ToolResult.Ok($"Theme '{name}' activated.");
        });
}
