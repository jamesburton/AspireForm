using System.Diagnostics;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.EndToEnd;

/// <summary>Runs the real AspireForm tool's apply verb against a fresh scaffold and asserts the on-disk output.</summary>
public sealed class ApplySnapshotTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-apply-snapshot").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null
               && !File.Exists(Path.Combine(dir, "AspireForm.sln"))
               && !File.Exists(Path.Combine(dir, "AspireForm.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException("Repository root not found.");
    }

    /// <summary>
    /// The build configuration the test assembly was built with — derived from its own bin path
    /// (<c>.../bin/&lt;Config&gt;/&lt;TFM&gt;/</c>). The snapshot tests pass this through to <c>dotnet run</c>
    /// so the subprocess looks in the same Debug/Release output as the test was built into. Without it,
    /// CI building <c>--configuration Release</c> would have <c>dotnet run</c> default to Debug and
    /// fail with "no such file or directory".
    /// </summary>
    private static string BuildConfiguration() =>
        new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Debug";

    private static (int ExitCode, string Output) RunTool(string workingDirectory, params string[] args)
    {
        var root = RepoRoot();
        var allArgs = new List<string>
        {
            "run",
            "--configuration", BuildConfiguration(),
            "--no-build",
            "--project", Path.Combine(root, "src", "AspireForm"),
            "--",
        };
        allArgs.AddRange(args);

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
        };
        foreach (var arg in allArgs)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }

    [Fact]
    public void Apply_against_an_ef_data_only_config_writes_dbcontext_and_state()
    {
        // ef-data only — no CLI actions, so no aspire dependency required at test time.
        File.WriteAllText(Path.Combine(_dir, "aspireform.yaml"), """
            aspireform:
              version: 1
              project: Snapshot
              apphost: Snapshot.AppHost
            modules:
              data:
                type: ef-data
                database: appdb
                contextName: AppDbContext
            """);

        var (exitCode, output) = RunTool(_dir, "apply", "--project-dir", _dir, "--yes");

        exitCode.Should().Be(0, output);
        File.Exists(Path.Combine(_dir, "Snapshot.AppHost", "Data", "AppDbContext.cs")).Should().BeTrue();
        File.Exists(Path.Combine(_dir, ".aspireform", "state.json")).Should().BeTrue();
        File.ReadAllText(Path.Combine(_dir, "Snapshot.AppHost", "Data", "AppDbContext.cs"))
            .Should().Contain("class AppDbContext : DbContext");
    }
}
