using AspireForm.Cli;
using AspireForm.Plugins;
using AwesomeAssertions;
using Spectre.Console.Cli;
using Xunit;

namespace AspireForm.Tests.Cli;

[Collection(nameof(ConsoleCaptureCollection))]
public sealed class PluginRemoveCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-plugin-remove").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private (int ExitCode, string StdOut, string StdErr) RunRemove(params string[] args)
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
            app.Configure(c => c.AddCommand<PluginRemoveCommand>("remove"));
            return (app.Run(["remove", .. args]), stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void Plugin_remove_drops_the_lockfile_entry()
    {
        var lockfile = new PluginLockfile();
        lockfile.Plugins.Add(new PluginLockEntry
        {
            Name = "Redis", Package = "AspireForm.Plugin.Redis", Version = "0.1.0",
            Source = "https://api.nuget.org/v3/index.json",
        });
        PluginLockfile.Save(_dir, lockfile);

        var (exitCode, stdout, _) = RunRemove("Redis", "--project-dir", _dir);

        exitCode.Should().Be(0);
        stdout.Should().Contain("Removed");
        PluginLockfile.Load(_dir).Plugins.Should().BeEmpty();
    }

    [Fact]
    public void Plugin_remove_refuses_unknown_plugin()
    {
        var (exitCode, _, stderr) = RunRemove("Ghost", "--project-dir", _dir);
        exitCode.Should().Be(1);
        stderr.Should().Contain("Ghost");
    }
}
