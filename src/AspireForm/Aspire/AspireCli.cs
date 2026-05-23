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
        try
        {
            var startInfo = new ProcessStartInfo(_executablePath, "--version")
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

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            return process.ExitCode == 0 ? output.Trim() : null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            // The executable is not installed or not on PATH.
            return null;
        }
    }
}
