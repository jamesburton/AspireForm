using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>Stub for the <c>doctor</c> command; the real implementation arrives in Task 15.</summary>
public sealed class DoctorCommand : Command
{
    /// <inheritdoc />
    protected override int Execute(CommandContext context, CancellationToken cancellationToken) => 0;
}
