using AspireForm.Cli;
using AspireForm.State;
using AwesomeAssertions;
using Spectre.Console.Cli;
using Xunit;

namespace AspireForm.Tests.Cli;

[Collection(nameof(ConsoleCaptureCollection))]
public sealed class ImportCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-import-cmd").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private (int ExitCode, string StdOut, string StdErr) RunImport(params string[] args)
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
            app.Configure(c => c.AddCommand<ImportCommand>("import"));
            return (app.Run(["import", .. args]), stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void Import_records_sql_block_into_state_with_checksum_of_existing_apphost()
    {
        File.WriteAllText(Path.Combine(_dir, "aspireform.yaml"), """
            aspireform:
              version: 1
              project: Demo
              apphost: Demo.AppHost
            resources:
              sql:
                type: sqlserver
                aspireName: sql
            """);
        var apphostDir = Directory.CreateDirectory(Path.Combine(_dir, "Demo.AppHost"));
        File.WriteAllText(Path.Combine(apphostDir.FullName, "AppHost.cs"),
            "var builder = DistributedApplication.CreateBuilder(args);\nbuilder.AddSqlServer(\"sql\");\nbuilder.Build().Run();\n");

        var (exitCode, stdout, _) = RunImport("sql", "--project-dir", _dir);

        exitCode.Should().Be(0);
        stdout.Should().Contain("Imported");
        var loaded = new StateStore().Load(_dir);
        loaded.Blocks.Should().ContainKey("sql");
        loaded.Blocks["sql"].Files.Keys.Should().Contain(p => p.EndsWith("AppHost.cs"));
        loaded.Blocks["sql"].Files.Values.First().Checksum.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Import_refuses_when_block_is_not_in_config()
    {
        File.WriteAllText(Path.Combine(_dir, "aspireform.yaml"), """
            aspireform:
              version: 1
              project: Demo
              apphost: Demo.AppHost
            """);

        var (exitCode, _, stderr) = RunImport("ghost", "--project-dir", _dir);
        exitCode.Should().Be(1);
        stderr.Should().Contain("ghost");
    }
}
