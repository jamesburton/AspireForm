using System.ComponentModel;
using System.Diagnostics;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>new</c> command: scaffolds a new Aspire solution + a starter <c>aspireform.yaml</c>.</summary>
public sealed class NewCommand : AsyncCommand<NewCommand.Settings>
{
    /// <summary>Options for <c>new</c>.</summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>The project name.</summary>
        [CommandArgument(0, "<NAME>")]
        [Description("The project name.")]
        public required string Name { get; init; }

        /// <summary>Output directory; defaults to the current directory.</summary>
        [CommandOption("-o|--output <DIR>")]
        [Description("Output directory (defaults to current directory).")]
        public string Output { get; init; } = ".";
    }

    /// <inheritdoc />
    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var projectRoot = Path.GetFullPath(Path.Combine(settings.Output, settings.Name));
        var appHostName = $"{settings.Name}.AppHost";

        if (Directory.Exists(projectRoot))
        {
            Console.Error.WriteLine($"Refusing to scaffold into existing directory '{projectRoot}'.");
            return 1;
        }

        Directory.CreateDirectory(projectRoot);

        var result = await RunDotnetNewAsync(appHostName, projectRoot, cancellationToken);
        if (result.ExitCode != 0)
        {
            Console.Error.WriteLine(
                $"dotnet new aspire-apphost failed (exit {result.ExitCode}): {result.StandardError}");
            return 1;
        }

        WriteStarterYaml(projectRoot, settings.Name, appHostName);

        Console.Out.WriteLine($"Created {projectRoot}");
        Console.Out.WriteLine($"  - {appHostName}/ (Aspire AppHost project)");
        Console.Out.WriteLine($"  - aspireform.yaml (starter)");
        return 0;
    }

    private static async Task<(int ExitCode, string StandardError)> RunDotnetNewAsync(
        string appHostName,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("new");
        startInfo.ArgumentList.Add("aspire-apphost");
        startInfo.ArgumentList.Add("-n");
        startInfo.ArgumentList.Add(appHostName);

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return (-1, "Failed to start dotnet.");
        }

        // Read both streams concurrently to avoid deadlock if either buffer fills.
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        await Task.WhenAll(stderrTask, stdoutTask);

        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, await stderrTask);
    }

    private static void WriteStarterYaml(string projectRoot, string projectName, string appHostName)
    {
        var content = $$"""
            aspireform:
              version: 1
              project: {{projectName}}
              apphost: {{appHostName}}
            resources: {}
            modules: {}
            """;
        File.WriteAllText(Path.Combine(projectRoot, "aspireform.yaml"), content);
    }
}
