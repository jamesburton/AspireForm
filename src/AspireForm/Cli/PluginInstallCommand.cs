using System.ComponentModel;
using AspireForm.Plugins;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>plugin install</c> command: explicit install of a plugin by package id, with optional version pin.</summary>
public sealed class PluginInstallCommand : AsyncCommand<PluginInstallCommand.Settings>
{
    /// <summary>Options for <c>plugin install</c>.</summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>The plugin name or package id (optionally <c>name@version</c>).</summary>
        [CommandArgument(0, "<NAME>")]
        [Description("Plugin name or package id (e.g. 'Redis' or 'AspireForm.Plugin.Redis@0.1.0').")]
        public required string Name { get; init; }

        /// <summary>The project directory.</summary>
        [CommandOption("-p|--project-dir <DIR>")]
        [Description("The project directory.")]
        public string ProjectDir { get; init; } = ".";
    }

    /// <inheritdoc />
    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var projectDir = Path.GetFullPath(settings.ProjectDir);
        var (packageId, version) = ParseNameAndVersion(settings.Name);

        var restorer = new PluginRestorer();
        PluginRestoreResult result;
        try
        {
            result = await restorer.RestoreAsync(packageId, version, projectDir, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Plugin install error: {ex.Message}");
            return 1;
        }

        if (!result.Success)
        {
            Console.Error.WriteLine($"Plugin install error: {result.ErrorMessage}");
            return 1;
        }

        var resolvedVersion = new DirectoryInfo(result.PackageDirectory!).Name;
        var displayName = packageId.StartsWith("AspireForm.Plugin.", StringComparison.OrdinalIgnoreCase)
            ? packageId["AspireForm.Plugin.".Length..]
            : packageId;

        var lockfile = PluginLockfile.Load(projectDir);
        lockfile.Plugins.RemoveAll(p =>
            string.Equals(p.Package, packageId, StringComparison.OrdinalIgnoreCase));
        lockfile.Plugins.Add(new PluginLockEntry
        {
            Name = displayName,
            Package = packageId,
            Version = resolvedVersion,
        });
        lockfile.Plugins.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        PluginLockfile.Save(projectDir, lockfile);

        Console.Out.WriteLine($"Installed {displayName} ({packageId} {resolvedVersion}).");
        return 0;
    }

    internal static (string PackageId, string Version) ParseNameAndVersion(string input)
    {
        var at = input.IndexOf('@');
        var packageId = at < 0 ? input : input[..at];
        var version = at < 0 ? "*" : input[(at + 1)..];

        // Treat empty version (e.g. "Redis@") the same as no version — float to latest.
        if (string.IsNullOrWhiteSpace(version))
        {
            version = "*";
        }

        if (!packageId.Contains('.'))
        {
            packageId = $"AspireForm.Plugin.{packageId}";
        }

        return (packageId, version);
    }
}
