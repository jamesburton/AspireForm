using System.Text.Json.Nodes;
using AspireForm.Plugins;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: removes a plugin from the lockfile. Mirrors PluginRemoveCommand — looks up by display Name.</summary>
public sealed class PluginRemoveTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public PluginRemoveTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "aspireform_plugin_remove";

    /// <inheritdoc />
    public string Description => "Remove a plugin from the lockfile.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["name"] = ToolBase.Str("Plugin display name as recorded in the lockfile."),
        ["projectDir"] = ToolBase.Str("Project directory; defaults to the server's --project-dir."),
    }, "name");

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var name = args["name"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name))
        {
            return Task.FromResult(ToolResult.Fail("aspireform_plugin_remove requires 'name'."));
        }

        var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
        var lockfile = PluginLockfile.Load(projectDir);

        var removed = lockfile.Plugins.RemoveAll(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (removed == 0)
        {
            return Task.FromResult(ToolResult.Fail($"Plugin '{name}' is not installed."));
        }

        PluginLockfile.Save(projectDir, lockfile);
        return Task.FromResult(ToolResult.Ok(
            $"Removed plugin '{name}' from the lockfile. Already-loaded plugins remain active until next run."));
    }
}
