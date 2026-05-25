using AspireForm.Cli;
using AwesomeAssertions;
using Spectre.Console.Cli;
using Xunit;

namespace AspireForm.Tests.Cli;

[Collection(nameof(ConsoleCaptureCollection))]
public sealed class ApplyCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-apply-cmd").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private (int ExitCode, string StdOut, string StdErr) RunApply(params string[] args)
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
            app.Configure(c => c.AddCommand<ApplyCommand>("apply"));
            return (app.Run(["apply", .. args]), stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void Apply_with_yes_writes_files_and_persists_state()
    {
        // Use ef-data only: zero CLI actions, no aspire dependency at test time.
        // Create a minimal entity csproj so the ef-data provider can scan it.
        var entityDir = Directory.CreateDirectory(Path.Combine(_dir, "SampleApp.Entities")).FullName;
        var entityCsproj = Path.Combine(entityDir, "SampleApp.Entities.csproj");
        File.WriteAllText(entityCsproj, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
              </PropertyGroup>
            </Project>
            """);

        File.WriteAllText(Path.Combine(_dir, "aspireform.yaml"), $"""
            aspireform:
              version: 1
              project: SampleApp
              apphost: SampleApp.AppHost
            modules:
              data:
                type: ef-data
                projectPath: {entityCsproj.Replace("\\", "/")}
            """);

        var (exitCode, stdout, _) = RunApply("--project-dir", _dir, "--yes");

        exitCode.Should().Be(0);
        stdout.Should().Contain("Applied");

        // The DbContext is emitted into the entity project directory (not AppHost/Data) in 0.5.0+.
        File.Exists(Path.Combine(entityDir, "AppDbContext.cs")).Should().BeTrue();
        File.Exists(Path.Combine(_dir, ".aspireform", "state.json")).Should().BeTrue();
    }

    [Fact]
    public void Apply_exits_nonzero_when_no_config_exists()
    {
        var (exitCode, _, stderr) = RunApply("--project-dir", _dir, "--yes");
        exitCode.Should().Be(1);
        stderr.Should().Contain("No AspireForm configuration");
    }
}
