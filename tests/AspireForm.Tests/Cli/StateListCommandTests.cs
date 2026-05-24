using AspireForm.Cli;
using AspireForm.State;
using AwesomeAssertions;
using Spectre.Console.Cli;
using Xunit;

namespace AspireForm.Tests.Cli;

[Collection(nameof(ConsoleCaptureCollection))]
public sealed class StateListCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-state-list").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private (int ExitCode, string StdOut, string StdErr) RunList(params string[] args)
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
            app.Configure(c => c.AddCommand<StateListCommand>("list"));
            return (app.Run(["list", .. args]), stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void State_list_prints_each_block_with_its_kind_and_type()
    {
        var state = new AspireFormState();
        state.Blocks["sql"] = new BlockState { Type = "sqlserver", Kind = "resource" };
        state.Blocks["data"] = new BlockState { Type = "ef-data", Kind = "module" };
        new StateStore().Save(_dir, state);

        var (exitCode, stdout, _) = RunList("--project-dir", _dir);

        exitCode.Should().Be(0);
        stdout.Should().Contain("sql").And.Contain("sqlserver").And.Contain("resource");
        stdout.Should().Contain("data").And.Contain("ef-data").And.Contain("module");
    }

    [Fact]
    public void State_list_reports_empty_when_state_is_absent()
    {
        var (exitCode, stdout, _) = RunList("--project-dir", _dir);
        exitCode.Should().Be(0);
        stdout.Should().Contain("No tracked blocks");
    }
}
