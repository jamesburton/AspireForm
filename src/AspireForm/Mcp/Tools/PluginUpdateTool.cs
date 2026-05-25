using System.Text.Json.Nodes;
using AspireForm.Plugins;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: updates an installed plugin to the latest version. Mirrors PluginUpdateCommand — looks up by display Name (not Package).</summary>
public sealed class PluginUpdateTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public PluginUpdateTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "aspireform_plugin_update";

    /// <inheritdoc />
    public string Description => "Update an installed plugin to the latest version.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["name"] = ToolBase.Str("Plugin display name as recorded in the lockfile."),
        ["projectDir"] = ToolBase.Str("Project directory; defaults to the server's --project-dir."),
    }, "name");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var name = args["name"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ToolResult.Fail("aspireform_plugin_update requires 'name'.");
        }

        var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
        var lockfile = PluginLockfile.Load(projectDir);

        var entry = lockfile.Plugins.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return ToolResult.Fail($"Plugin '{name}' is not installed.");
        }

        var oldVersion = entry.Version;
        var restorer = new PluginRestorer();
        PluginRestoreResult result;
        try
        {
            result = await restorer.RestoreAsync(entry.Package, "*", projectDir, ct);
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Plugin update error: {ex.Message}");
        }

        if (!result.Success)
        {
            return ToolResult.Fail($"Plugin update error: {result.ErrorMessage}");
        }

        var newVersion = new DirectoryInfo(result.PackageDirectory!).Name;
        entry.Version = newVersion;
        PluginLockfile.Save(projectDir, lockfile);

        return ToolResult.Ok(
            string.Equals(oldVersion, newVersion, StringComparison.Ordinal)
                ? $"{entry.Name} already at {newVersion}."
                : $"Updated {entry.Name}: {oldVersion} -> {newVersion}.");
    }
}
