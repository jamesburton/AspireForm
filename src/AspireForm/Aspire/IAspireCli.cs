namespace AspireForm.Aspire;

/// <summary>The single seam through which AspireForm interacts with the official <c>aspire</c> CLI.</summary>
public interface IAspireCli
{
    /// <summary>Returns true when the <c>aspire</c> CLI can be invoked.</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the installed <c>aspire</c> CLI version string, or null when it is unavailable.</summary>
    Task<string?> GetVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes the underlying executable with <paramref name="args"/> from <paramref name="workingDirectory"/>,
    /// capturing stdout and stderr. Returns a <see cref="CliResult"/> with the captured output and exit code.
    /// Never throws on non-zero exit; failures are reported via <see cref="CliResult.ExitCode"/>.
    /// </summary>
    Task<CliResult> RunAsync(
        IReadOnlyList<string> args,
        string workingDirectory,
        CancellationToken cancellationToken = default);
}
