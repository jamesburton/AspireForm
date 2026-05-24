namespace AspireForm.Aspire;

/// <summary>The captured outcome of a CLI subprocess invocation.</summary>
/// <param name="ExitCode">The process exit code (0 == success).</param>
/// <param name="StandardOutput">Everything the subprocess wrote to stdout.</param>
/// <param name="StandardError">Everything the subprocess wrote to stderr.</param>
public sealed record CliResult(int ExitCode, string StandardOutput, string StandardError);
