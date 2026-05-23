using System.Diagnostics;
using AspireForm.Aspire;

namespace AspireForm.Diagnostics;

/// <summary>Checks that the prerequisites for running AspireForm are present.</summary>
public sealed class PrerequisiteChecker
{
    private const int MinimumDotnetMajorVersion = 10;

    private readonly IAspireCli _aspireCli;

    /// <summary>Initializes the checker with the <c>aspire</c> CLI seam.</summary>
    public PrerequisiteChecker(IAspireCli aspireCli) => _aspireCli = aspireCli;

    /// <summary>Runs every prerequisite check and returns the aggregate report.</summary>
    public async Task<PrerequisiteReport> RunAsync(CancellationToken cancellationToken = default)
    {
        var checks = new List<PrerequisiteCheck>
        {
            CheckDotnetSdk(),
            await CheckAspireCliAsync(cancellationToken),
        };

        return new PrerequisiteReport { Checks = checks };
    }

    private static PrerequisiteCheck CheckDotnetSdk()
    {
        var version = TryRun("dotnet", "--version");
        if (version is null)
        {
            return new PrerequisiteCheck(
                ".NET SDK", Ok: false,
                Detail: "The 'dotnet' command was not found.",
                Remedy: "Install the .NET 10 SDK from https://dotnet.microsoft.com/download.");
        }

        var major = ParseMajorVersion(version);
        var ok = major >= MinimumDotnetMajorVersion;
        return new PrerequisiteCheck(
            ".NET SDK", ok,
            Detail: $"Detected {version}.",
            Remedy: ok ? null : $"AspireForm requires the .NET {MinimumDotnetMajorVersion} SDK or later.");
    }

    private async Task<PrerequisiteCheck> CheckAspireCliAsync(CancellationToken cancellationToken)
    {
        var version = await _aspireCli.GetVersionAsync(cancellationToken);
        return version is not null
            ? new PrerequisiteCheck("aspire CLI", Ok: true, Detail: $"Detected {version}.", Remedy: null)
            : new PrerequisiteCheck(
                "aspire CLI", Ok: false,
                Detail: "The 'aspire' CLI was not found on PATH.",
                Remedy: "Install it with: dotnet tool install -g Aspire.Cli  (or run AspireForm anyway — "
                        + "it will fall back to 'dnx Aspire.Cli').");
    }

    private static int ParseMajorVersion(string version)
    {
        var firstSegment = version.Split('.', '-')[0];
        return int.TryParse(firstSegment, out var major) ? major : 0;
    }

    private static string? TryRun(string fileName, string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo(fileName, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output.Trim() : null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return null;
        }
    }
}
