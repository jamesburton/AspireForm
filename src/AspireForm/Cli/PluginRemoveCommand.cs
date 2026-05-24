using System.ComponentModel;
using AspireForm.Plugins;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>plugin remove</c> command: drops a plugin from the lockfile.</summary>
public sealed class PluginRemoveCommand : AsyncCommand<PluginRemoveCommand.Settings>
{
    /// <summary>Options for <c>plugin remove</c>.</summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>The plugin name to remove.</summary>
        [CommandArgument(0, "<NAME>")]
        [Description("Plugin name (as recorded in the lockfile).")]
        public required string Name { get; init; }

        /// <summary>The project directory.</summary>
        [CommandOption("-p|--project-dir <DIR>")]
        [Description("The project directory.")]
        public string ProjectDir { get; init; } = ".";
    }

    /// <inheritdoc />
    protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken) =>
        Task.FromResult(ExecuteCore(settings));

    private static int ExecuteCore(Settings settings)
    {
        var projectDir = Path.GetFullPath(settings.ProjectDir);
        var lockfile = PluginLockfile.Load(projectDir);

        var removed = lockfile.Plugins.RemoveAll(p =>
            string.Equals(p.Name, settings.Name, StringComparison.OrdinalIgnoreCase));

        if (removed == 0)
        {
            Console.Error.WriteLine($"Plugin '{settings.Name}' is not installed.");
            return 1;
        }

        PluginLockfile.Save(projectDir, lockfile);
        Console.Out.WriteLine($"Removed plugin '{settings.Name}' from the lockfile. Already-loaded plugins remain active until next run.");
        return 0;
    }
}
