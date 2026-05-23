using AspireForm.Cli;
using AwesomeAssertions;
using Spectre.Console.Cli;
using Xunit;

namespace AspireForm.Tests.Cli;

[Collection(nameof(ConsoleCaptureCollection))]
public sealed class PlanCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-plan-cmd").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private (int ExitCode, string StdOut, string StdErr) RunPlan(params string[] args)
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
            app.Configure(c => c.AddCommand<PlanCommand>("plan"));
            return (app.Run(["plan", .. args]), stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void Plan_against_sample_config_renders_create_actions()
    {
        File.WriteAllText(Path.Combine(_dir, "aspireform.yaml"), """
            aspireform:
              version: 1
              project: SampleApp
              apphost: ./SampleApp.AppHost
            resources:
              sql:
                type: sqlserver
                aspireName: sql
                databases: [appdb]
            modules:
              data:
                type: ef-data
                dependsOn: [sql]
                database: appdb
                contextName: AppDbContext
            """);

        var (exitCode, stdout, _) = RunPlan("--project-dir", _dir);

        exitCode.Should().Be(0);
        stdout.Should().Contain("+ sql").And.Contain("+ data");
        stdout.Should().Contain("CREATE");
        stdout.Should().Contain("aspire add sqlserver");
    }

    [Fact]
    public void Plan_exits_nonzero_with_an_error_when_no_config_exists()
    {
        var (exitCode, _, stderr) = RunPlan("--project-dir", _dir);
        exitCode.Should().Be(1);
        stderr.Should().Contain("No AspireForm configuration");
    }

    [Fact]
    public void Plan_exits_nonzero_with_an_error_when_state_file_is_corrupt()
    {
        File.WriteAllText(Path.Combine(_dir, "aspireform.yaml"), """
            aspireform:
              version: 1
              project: X
              apphost: ./X.AppHost
            """);

        var stateDir = Directory.CreateDirectory(Path.Combine(_dir, ".aspireform"));
        File.WriteAllText(Path.Combine(stateDir.FullName, "state.json"), "{ not json");

        var (exitCode, _, stderr) = RunPlan("--project-dir", _dir);

        exitCode.Should().Be(1);
        stderr.Should().Contain("State error");
    }
}
