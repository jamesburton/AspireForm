using System.ComponentModel;
using AspireForm.Ui;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>ui</c> command: starts a Kestrel-hosted Blazor Server app on localhost for the EF model builder.</summary>
public sealed class UiCommand : AsyncCommand<UiCommand.Settings>
{
    /// <summary>Options for <c>ui</c>.</summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>Default AspireForm project directory.</summary>
        [CommandOption("-p|--project-dir <DIR>")]
        [Description("Default AspireForm project directory.")]
        public string ProjectDir { get; init; } = ".";

        /// <summary>Port to bind. Defaults to 5050.</summary>
        [CommandOption("--port <PORT>")]
        [Description("Port to bind (default 5050).")]
        public int Port { get; init; } = 5050;

        /// <summary>When true, suppresses the browser auto-launch.</summary>
        [CommandOption("--no-launch")]
        [Description("Don't open the default browser on startup.")]
        public bool NoLaunch { get; init; }
    }

    /// <inheritdoc />
    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var opts = new UiOptions
        {
            ProjectDir = Path.GetFullPath(settings.ProjectDir),
            Port = settings.Port,
            LaunchBrowser = !settings.NoLaunch,
        };
        try
        {
            await UiHost.RunAsync(opts, cancellationToken);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
    }
}
