using AspireForm.Cli;
using AspireForm.Plugins;
using AwesomeAssertions;
using Spectre.Console.Cli;
using Xunit;

namespace AspireForm.Tests.Cli;

[Collection(nameof(ConsoleCaptureCollection))]
public sealed class PluginListCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-plugin-list").FullName;

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
            app.Configure(c => c.AddCommand<PluginListCommand>("list"));
            return (app.Run(["list", .. args]), stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void Plugin_list_reports_no_plugins_when_lockfile_is_empty()
    {
        var (exitCode, stdout, _) = RunList("--project-dir", _dir);
        exitCode.Should().Be(0);
        stdout.Should().Contain("No plugins");
    }

    [Fact]
    public void Plugin_list_prints_each_locked_plugin_with_its_version()
    {
        var lockfile = new PluginLockfile();
        lockfile.Plugins.Add(new PluginLockEntry
        {
            Name = "Redis",
            Package = "AspireForm.Plugin.Redis",
            Version = "0.1.0",
            Source = "https://api.nuget.org/v3/index.json",
        });
        PluginLockfile.Save(_dir, lockfile);

        var (exitCode, stdout, _) = RunList("--project-dir", _dir);
        exitCode.Should().Be(0);
        stdout.Should().Contain("Redis").And.Contain("0.1.0");
    }
}
