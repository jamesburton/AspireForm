using System.ComponentModel;
using System.Diagnostics;

namespace AspireForm.Aspire;

/// <summary>An <see cref="IAspireCli"/> that shells out to the <c>aspire</c> executable on PATH.</summary>
public sealed class AspireCli : IAspireCli
{
    private readonly string _executablePath;

    /// <summary>Initializes the CLI wrapper, optionally overriding the executable name (used by tests).</summary>
    public AspireCli(string executablePath = "aspire") => _executablePath = executablePath;

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
        await GetVersionAsync(cancellationToken) is not null;

    /// <inheritdoc />
    public async Task<string?> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["--version"], workingDirectory: Environment.CurrentDirectory, cancellationToken);
        return result.ExitCode == 0 ? result.StandardOutput.Trim() : null;
    }

    /// <inheritdoc />
    public async Task<CliResult> RunAsync(
        IReadOnlyList<string> args,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _executablePath,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new CliResult(ExitCode: -1, StandardOutput: string.Empty, StandardError: "Failed to start process.");
            }

            // Read both streams in parallel so neither blocks the other.
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            return new CliResult(process.ExitCode, stdout, stderr);
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            return new CliResult(ExitCode: -1, StandardOutput: string.Empty, StandardError: ex.Message);
        }
    }
}
