using System.ComponentModel;
using AspireForm.Plugins;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>plugin update</c> command: re-resolves the latest version of an installed plugin and updates the lockfile.</summary>
public sealed class PluginUpdateCommand : AsyncCommand<PluginUpdateCommand.Settings>
{
    /// <summary>Options for <c>plugin update</c>.</summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>The plugin name.</summary>
        [CommandArgument(0, "<NAME>")]
        [Description("Plugin name (as recorded in the lockfile).")]
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
        var lockfile = PluginLockfile.Load(projectDir);

        var entry = lockfile.Plugins.FirstOrDefault(p =>
            string.Equals(p.Name, settings.Name, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            Console.Error.WriteLine($"Plugin '{settings.Name}' is not installed.");
            return 1;
        }

        var restorer = new PluginRestorer();
        var result = await restorer.RestoreAsync(entry.Package, "*", projectDir, cancellationToken);
        if (!result.Success)
        {
            Console.Error.WriteLine($"Plugin update error: {result.ErrorMessage}");
            return 1;
        }

        var newVersion = new DirectoryInfo(result.PackageDirectory!).Name;
        var oldVersion = entry.Version;
        entry.Version = newVersion;
        PluginLockfile.Save(projectDir, lockfile);

        Console.Out.WriteLine(
            string.Equals(oldVersion, newVersion, StringComparison.Ordinal)
                ? $"{entry.Name} already at {newVersion}."
                : $"Updated {entry.Name}: {oldVersion} -> {newVersion}.");
        return 0;
    }
}
