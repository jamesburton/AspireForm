using System.ComponentModel;
using System.Text;
using AspireForm.Plugins;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>plugin list</c> command: prints every plugin recorded in the lockfile.</summary>
public sealed class PluginListCommand : AsyncCommand<PluginListCommand.Settings>
{
    /// <summary>Options for <c>plugin list</c>.</summary>
    public sealed class Settings : CommandSettings
    {
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
        var lockfile = PluginLockfile.Load(Path.GetFullPath(settings.ProjectDir));
        if (lockfile.Plugins.Count == 0)
        {
            Console.Out.WriteLine("No plugins installed.");
            return 0;
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

        Console.Out.Write(sb.ToString());
        return 0;
    }
}
