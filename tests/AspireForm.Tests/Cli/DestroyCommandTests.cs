using AspireForm.Cli;
using AspireForm.State;
using AwesomeAssertions;
using Spectre.Console.Cli;
using Xunit;

namespace AspireForm.Tests.Cli;

[Collection(nameof(ConsoleCaptureCollection))]
public sealed class DestroyCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-destroy-cmd").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private (int ExitCode, string StdOut, string StdErr) RunDestroy(params string[] args)
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
            app.Configure(c => c.AddCommand<DestroyCommand>("destroy"));
            return (app.Run(["destroy", .. args]), stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void Destroy_refuses_module_block_without_allow_module_destroy_flag()
    {
        SeedConfig();
        SeedStateWith("data", kind: "module");
        File.WriteAllText(Path.Combine(_dir, "tracked.cs"), "x");

        var (exitCode, _, stderr) = RunDestroy("data", "--project-dir", _dir, "--yes");

        exitCode.Should().Be(1);
        stderr.Should().Contain("module").And.Contain("--allow-module-destroy");
    }

    [Fact]
    public void Destroy_removes_resource_block_files_and_state_entry()
    {
        SeedConfig();
        SeedStateWith("sql", kind: "resource");
        File.WriteAllText(Path.Combine(_dir, "tracked.cs"), "x");

        var (exitCode, stdout, _) = RunDestroy("sql", "--project-dir", _dir, "--yes");

        exitCode.Should().Be(0);
        stdout.Should().Contain("Destroyed");
        new StateStore().Load(_dir).Blocks.Should().NotContainKey("sql");
    }

    private void SeedConfig() => File.WriteAllText(Path.Combine(_dir, "aspireform.yaml"), """
        aspireform:
          version: 1
          project: SampleApp
          apphost: SampleApp.AppHost
        """);

    private void SeedStateWith(string blockName, string kind)
    {
        var state = new AspireFormState();
        state.Blocks[blockName] = new BlockState
        {
            Type = kind == "module" ? "ef-data" : "sqlserver",
            Kind = kind,
            Files = { ["tracked.cs"] = new FileState { OwnershipMode = "managed", Checksum = "x" } },
        };
        new StateStore().Save(_dir, state);
    }
}
