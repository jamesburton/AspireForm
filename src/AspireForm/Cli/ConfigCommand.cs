using System.ComponentModel;
using System.Text.Json;
using AspireForm.Configuration;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>config</c> (alias <c>show</c>) command: prints the resolved desired-state configuration.</summary>
public sealed class ConfigCommand : Command<ConfigCommand.Settings>
{
    /// <summary>Options for the <c>config</c> command.</summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>The project directory containing the AspireForm configuration. Defaults to the current directory.</summary>
        [CommandOption("-p|--project-dir <DIR>")]
        [Description("The project directory containing the AspireForm configuration.")]
        public string ProjectDir { get; init; } = ".";

        /// <summary>An optional environment whose override file is layered over the base configuration.</summary>
        [CommandOption("-e|--env <ENV>")]
        [Description("Environment whose override file (aspireform.<env>.*) is layered over the base.")]
        public string? Env { get; init; }
    }

    private static readonly JsonSerializerOptions OutputOptions = new() { WriteIndented = true };

    /// <inheritdoc />
    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            var loaded = new ConfigLoader().Load(Path.GetFullPath(settings.ProjectDir), settings.Env);
            Console.Out.WriteLine(loaded.Resolved.ToJsonString(OutputOptions));
            return 0;
        }
        catch (ConfigValidationException ex)
        {
            Console.Error.WriteLine($"Configuration error: {ex.Message}");
            return 1;
        }
    }
}
