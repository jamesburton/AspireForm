using System.Text.Json.Nodes;
using AspireForm.Plugins;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: installs a plugin by name (or <c>name@version</c>) and records it in the lockfile.</summary>
public sealed class PluginInstallTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public PluginInstallTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "aspireform_plugin_install";

    /// <inheritdoc />
    public string Description => "Install a plugin by name (or 'name@version') and record it in the lockfile.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["name"] = ToolBase.Str("Plugin name or package id (e.g. 'Redis' or 'AspireForm.Plugin.Redis@0.1.0')."),
        ["projectDir"] = ToolBase.Str("Project directory; defaults to the server's --project-dir."),
    }, "name");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var name = args["name"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ToolResult.Fail("aspireform_plugin_install requires 'name'.");
        }

        var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
        var (packageId, version) = AspireForm.Cli.PluginInstallCommand.ParseNameAndVersion(name);

        var restorer = new PluginRestorer();
        PluginRestoreResult result;
        try
        {
            result = await restorer.RestoreAsync(packageId, version, projectDir, ct);
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Plugin install error: {ex.Message}");
        }

        if (!result.Success)
        {
            return ToolResult.Fail($"Plugin install error: {result.ErrorMessage}");
        }

        var resolvedVersion = new DirectoryInfo(result.PackageDirectory!).Name;
        var displayName = packageId.StartsWith("AspireForm.Plugin.", StringComparison.OrdinalIgnoreCase)
            ? packageId["AspireForm.Plugin.".Length..]
            : packageId;

        var lockfile = PluginLockfile.Load(projectDir);
        lockfile.Plugins.RemoveAll(p => string.Equals(p.Package, packageId, StringComparison.OrdinalIgnoreCase));
        lockfile.Plugins.Add(new PluginLockEntry
        {
            Name = displayName,
            Package = packageId,
            Version = resolvedVersion,
        });
        lockfile.Plugins.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        PluginLockfile.Save(projectDir, lockfile);

        return ToolResult.Ok($"Installed {displayName} ({packageId} {resolvedVersion}).");
    }
}
