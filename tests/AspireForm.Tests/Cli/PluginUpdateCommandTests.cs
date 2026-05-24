using AspireForm.Cli;
using AspireForm.Plugins;
using AwesomeAssertions;
using Spectre.Console.Cli;
using Xunit;

namespace AspireForm.Tests.Cli;

[Collection(nameof(ConsoleCaptureCollection))]
public sealed class PluginUpdateCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-plugin-update").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private (int ExitCode, string StdOut, string StdErr) RunUpdate(params string[] args)
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
            app.Configure(c => c.AddCommand<PluginUpdateCommand>("update"));
            return (app.Run(["update", .. args]), stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void Plugin_update_refuses_unknown_plugin()
    {
        var (exitCode, _, stderr) = RunUpdate("Ghost", "--project-dir", _dir);
        exitCode.Should().Be(1);
        stderr.Should().Contain("Ghost").And.Contain("not installed");
    }
}
