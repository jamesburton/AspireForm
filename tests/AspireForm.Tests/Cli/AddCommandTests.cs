using AspireForm.Cli;
using AspireForm.Configuration;
using AwesomeAssertions;
using Spectre.Console.Cli;
using Xunit;

namespace AspireForm.Tests.Cli;

[Collection(nameof(ConsoleCaptureCollection))]
public sealed class AddCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-add-cmd").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private (int ExitCode, string StdOut, string StdErr) RunAdd(params string[] args)
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
            app.Configure(c => c.AddCommand<AddCommand>("add"));
            return (app.Run(["add", .. args]), stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    private void SeedConfig() => File.WriteAllText(Path.Combine(_dir, "aspireform.yaml"), """
        aspireform:
          version: 1
          project: Demo
          apphost: Demo.AppHost
        """);

    [Fact]
    public void Add_inserts_a_resource_block_into_aspireform_yaml()
    {
        SeedConfig();

        var (exitCode, _, _) = RunAdd("sqlserver", "sql", "--project-dir", _dir);

        exitCode.Should().Be(0);
        var loaded = new ConfigLoader().Load(_dir, env: null);
        loaded.Model.Resources.Should().ContainKey("sql");
        loaded.Model.Resources["sql"].Type.Should().Be("sqlserver");
    }

    [Fact]
    public void Add_inserts_a_module_block_with_dependsOn_when_kind_is_module()
    {
        SeedConfig();
        // Add sql first so dependsOn resolves at load time.
        RunAdd("sqlserver", "sql", "--project-dir", _dir);

        var (exitCode, _, _) = RunAdd("ef-data", "data",
            "--project-dir", _dir,
            "--module",
            "--depends-on", "sql");

        exitCode.Should().Be(0);

        var loaded = new ConfigLoader().Load(_dir, env: null);
        loaded.Model.Modules["data"].DependsOn.Should().Contain("sql");
    }

    [Fact]
    public void Add_refuses_when_a_block_with_the_same_name_already_exists()
    {
        SeedConfig();
        RunAdd("sqlserver", "sql", "--project-dir", _dir);

        var (exitCode, _, stderr) = RunAdd("sqlserver", "sql", "--project-dir", _dir);
        exitCode.Should().Be(1);
        stderr.Should().Contain("already exists");
    }
}
