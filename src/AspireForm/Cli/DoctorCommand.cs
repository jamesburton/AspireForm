using AspireForm.Aspire;
using AspireForm.Diagnostics;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>doctor</c> command: checks AspireForm's prerequisites and prints a report.</summary>
public sealed class DoctorCommand : AsyncCommand
{
    /// <inheritdoc />
    protected override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var report = await new PrerequisiteChecker(new AspireCli()).RunAsync(cancellationToken);

        foreach (var check in report.Checks)
        {
            var status = check.Ok ? "OK    " : "FAILED";
            Console.Out.WriteLine($"[{status}] {check.Name}: {check.Detail}");
        }

        foreach (var failed in report.Checks.Where(c => !c.Ok && c.Remedy is not null))
        {
            Console.Out.WriteLine($"  -> {failed.Name}: {failed.Remedy}");
        }

        return report.AllPassed ? 0 : 1;
    }
}
