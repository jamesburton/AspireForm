using System.Diagnostics;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.EndToEnd;

/// <summary>
/// End-to-end test of the plugin loader: packs AspireForm.Plugin.Redis to a temp dir,
/// configures a NuGet.config pointing at it as a local feed, then runs the real tool's
/// 'plan' verb against a fixture referencing `type: redis` and asserts the plugin loaded.
/// Slow (packs + restores once); intentional — this is the gate that proves the loader works end-to-end.
/// </summary>
public sealed class PluginLoaderE2ETests : IDisposable
{
    private readonly string _projectDir = Directory.CreateTempSubdirectory("aspireform-plugin-e2e").FullName;

    /// <inheritdoc />
    public void Dispose()
    {
        try { Directory.Delete(_projectDir, recursive: true); }
        catch (IOException) { /* AssemblyLoadContext file locks may prevent cleanup */ }
        catch (UnauthorizedAccessException) { /* same */ }
    }

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null
               && !File.Exists(Path.Combine(dir, "AspireForm.sln"))
               && !File.Exists(Path.Combine(dir, "AspireForm.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException("Repo root not found.");
    }

    private static string BuildConfiguration() =>
        new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Debug";

    [Fact]
    public void Plan_against_fixture_using_redis_loads_the_plugin_and_renders_block()
    {
        var root = RepoRoot();
        var feedDir = Path.Combine(_projectDir, ".local-feed");
        Directory.CreateDirectory(feedDir);

        // 1. Pack AspireForm.Plugin.Redis into the local feed directory.
        var pack = Run("dotnet", workingDirectory: root,
            "pack", Path.Combine("src", "Plugins", "AspireForm.Plugin.Redis"),
            "-c", BuildConfiguration(),
            "-o", feedDir,
            "--no-build", "--nologo");
        pack.ExitCode.Should().Be(0, pack.Output);

        // 2. Write a NuGet.config in the project dir that adds the local feed at top priority.
        File.WriteAllText(Path.Combine(_projectDir, "NuGet.config"), $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="local" value="{{feedDir.Replace('\\', '/')}}" />
                <add key="nuget" value="https://api.nuget.org/v3/index.json" />
              </packageSources>
            </configuration>
            """);

        // 3. Write a fixture aspireform.yaml that references the new 'redis' block type.
        File.WriteAllText(Path.Combine(_projectDir, "aspireform.yaml"), """
            aspireform:
              version: 1
              project: Fixture
              apphost: Fixture.AppHost
            resources:
              cache:
                type: redis
                aspireName: cache
            """);

        // 4. Run the real tool's plan verb.
        var plan = Run("dotnet", workingDirectory: _projectDir,
            "run", "--configuration", BuildConfiguration(), "--no-build",
            "--project", Path.Combine(root, "src", "AspireForm"),
            "--", "plan", "--project-dir", _projectDir);

        plan.ExitCode.Should().Be(0, plan.Output);
        plan.Output.Should().Contain("+ cache").And.Contain("redis");
        plan.Output.Should().Contain("aspire add redis");
        plan.Output.Should().Contain("builder.AddRedis(\"cache\")");

        // 5. Verify the lockfile was written with the resolved Redis plugin entry.
        var lockPath = Path.Combine(_projectDir, ".aspireform", "plugins.lock.yaml");
        File.Exists(lockPath).Should().BeTrue();
        File.ReadAllText(lockPath).Should().Contain("AspireForm.Plugin.Redis");
    }

    private static (int ExitCode, string Output) Run(string fileName, string workingDirectory, params string[] args)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args)
        {
            startInfo.ArgumentList.Add(a);
        }

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }
}
