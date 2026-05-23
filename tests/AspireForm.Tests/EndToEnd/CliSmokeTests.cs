using System.Diagnostics;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.EndToEnd;

/// <summary>Builds and runs the real AspireForm tool against the sample fixture.</summary>
public sealed class CliSmokeTests
{
    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null
               && !File.Exists(Path.Combine(dir, "AspireForm.sln"))
               && !File.Exists(Path.Combine(dir, "AspireForm.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException("Could not locate the repository root.");
    }

    /// <summary>
    /// The build configuration the test assembly was built with — derived from its own bin path
    /// (<c>.../bin/&lt;Config&gt;/&lt;TFM&gt;/</c>). The smoke tests pass this through to <c>dotnet run</c>
    /// so the subprocess looks in the same Debug/Release output as the test was built into. Without it,
    /// CI building <c>--configuration Release</c> would have <c>dotnet run</c> default to Debug and
    /// fail with "no such file or directory".
    /// </summary>
    private static string BuildConfiguration() =>
        new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Debug";

    private static (int ExitCode, string Output) RunTool(params string[] args)
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
            WorkingDirectory = root,
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
    public void Config_command_prints_the_sample_project()
    {
        var sampleDir = Path.Combine(RepoRoot(), "examples", "sample");
        var (exitCode, output) = RunTool("config", "--project-dir", sampleDir);

        exitCode.Should().Be(0);
        output.Should().Contain("SampleApp");
    }

    [Fact]
    public void Config_command_applies_the_dev_override()
    {
        var sampleDir = Path.Combine(RepoRoot(), "examples", "sample");
        var (exitCode, output) = RunTool("config", "--project-dir", sampleDir, "--env", "dev");

        exitCode.Should().Be(0);
        output.Should().Contain("sql-dev");
    }

    [Fact]
    public void Doctor_command_runs()
    {
        var (exitCode, output) = RunTool("doctor");

        exitCode.Should().BeOneOf(0, 1);
        output.Should().Contain(".NET SDK");
    }
}
