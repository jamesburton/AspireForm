using System.Text;
using System.Text.Json.Nodes;
using AspireForm.Plugins;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: lists installed plugins from the lockfile. Mirrors PluginListCommand's table output.</summary>
public sealed class PluginListTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public PluginListTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "aspireform_plugin_list";

    /// <inheritdoc />
    public string Description => "List installed plugins from .aspireform/plugins.lock.yaml.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["projectDir"] = ToolBase.Str("Project directory; defaults to the server's --project-dir."),
    });

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
        var lockfile = PluginLockfile.Load(projectDir);
        if (lockfile.Plugins.Count == 0)
        {
            return Task.FromResult(ToolResult.Ok("No plugins installed."));
        }

        var nameW = Math.Max(4, lockfile.Plugins.Max(p => p.Name.Length));
        var packageW = Math.Max(7, lockfile.Plugins.Max(p => p.Package.Length));
        var versionW = Math.Max(7, lockfile.Plugins.Max(p => p.Version.Length));

        var sb = new StringBuilder();
        sb.AppendLine($"{"Name".PadRight(nameW)} {"Package".PadRight(packageW)} Version");
        sb.AppendLine($"{"----".PadRight(nameW)} {"-------".PadRight(packageW)} {"-------".PadRight(versionW)}");
        foreach (var p in lockfile.Plugins)
        {
            sb.AppendLine($"{p.Name.PadRight(nameW)} {p.Package.PadRight(packageW)} {p.Version}");
        }

        return Task.FromResult(ToolResult.Ok(sb.ToString()));
    }
}
