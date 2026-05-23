using AspireForm.Cli;
using AwesomeAssertions;
using Spectre.Console.Cli;
using Xunit;

namespace AspireForm.Tests.Cli;

[Collection(nameof(ConsoleCaptureCollection))]
public sealed class DoctorCommandTests
{
    private static (int ExitCode, string StdOut, string StdErr) RunDoctor()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        Console.SetOut(stdout);
        Console.SetError(stderr);
        try
        {
            var app = new CommandApp();
            app.Configure(config => config.AddCommand<DoctorCommand>("doctor"));
            var exit = app.Run(new[] { "doctor" });
            return (exit, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void Runs_and_prints_each_prerequisite_check()
    {
        var (exitCode, stdout, _) = RunDoctor();

        // The .NET SDK and aspire CLI checks always run, regardless of environment.
        stdout.Should().Contain(".NET SDK");
        stdout.Should().Contain("aspire CLI");

        /* Exit code is 0 when every check passes, 1 otherwise — both are valid here. */
        exitCode.Should().BeOneOf(0, 1);
    }
}
