using AspireForm.Cli;
using AwesomeAssertions;
using Spectre.Console.Cli;
using Xunit;

namespace AspireForm.Tests.Cli;

public sealed class ConfigCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-config-cmd").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static (int ExitCode, string StdOut, string StdErr) RunConfig(params string[] args)
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
            app.Configure(config => config.AddCommand<ConfigCommand>("config"));
            var exit = app.Run(args);
            return (exit, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void Prints_resolved_config_and_exits_zero()
    {
        File.WriteAllText(Path.Combine(_dir, "aspireform.yaml"), """
            aspireform:
              version: 1
              project: MyApp
              apphost: ./MyApp.AppHost
            """);

        var (exitCode, stdout, _) = RunConfig("config", "--project-dir", _dir);

        exitCode.Should().Be(0);
        stdout.Should().Contain("MyApp");
    }

    [Fact]
    public void Exits_nonzero_with_an_error_when_no_config_exists()
    {
        var (exitCode, _, stderr) = RunConfig("config", "--project-dir", _dir);

        exitCode.Should().Be(1);
        stderr.Should().Contain("No AspireForm configuration");
    }
}
