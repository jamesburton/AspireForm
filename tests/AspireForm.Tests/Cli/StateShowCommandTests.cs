using AspireForm.Cli;
using AspireForm.State;
using AwesomeAssertions;
using Spectre.Console.Cli;
using Xunit;

namespace AspireForm.Tests.Cli;

[Collection(nameof(ConsoleCaptureCollection))]
public sealed class StateShowCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-state-show").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private (int ExitCode, string StdOut, string StdErr) RunShow(params string[] args)
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
            app.Configure(c => c.AddCommand<StateShowCommand>("show"));
            return (app.Run(["show", .. args]), stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void State_show_prints_a_block_record_as_indented_json()
    {
        var state = new AspireFormState();
        state.Blocks["sql"] = new BlockState
        {
            Type = "sqlserver", Kind = "resource",
            Files = { ["AppHost.cs"] = new FileState { OwnershipMode = "managed", Checksum = "abc" } },
        };
        new StateStore().Save(_dir, state);

        var (exitCode, stdout, _) = RunShow("sql", "--project-dir", _dir);

        exitCode.Should().Be(0);
        stdout.Should().Contain("\"sqlserver\"").And.Contain("\"AppHost.cs\"").And.Contain("\"abc\"");
    }

    [Fact]
    public void State_show_reports_missing_block()
    {
        new StateStore().Save(_dir, new AspireFormState());
        var (exitCode, _, stderr) = RunShow("ghost", "--project-dir", _dir);
        exitCode.Should().Be(1);
        stderr.Should().Contain("ghost").And.Contain("not tracked");
    }
}
