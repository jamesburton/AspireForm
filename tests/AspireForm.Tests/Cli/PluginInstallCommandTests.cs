using AspireForm.Cli;
using AspireForm.Plugins;
using AwesomeAssertions;
using Spectre.Console.Cli;
using Xunit;

namespace AspireForm.Tests.Cli;

[Collection(nameof(ConsoleCaptureCollection))]
public sealed class PluginInstallCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-plugin-install").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private (int ExitCode, string StdOut, string StdErr) RunInstall(params string[] args)
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
            app.Configure(c => c.AddCommand<PluginInstallCommand>("install"));
            return (app.Run(["install", .. args]), stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void Plugin_install_fails_for_nonexistent_package()
    {
        var (exitCode, _, stderr) = RunInstall("This.Does.Not.Exist.AspireForm.Test", "--project-dir", _dir);
        exitCode.Should().Be(1);
        stderr.Should().NotBeNullOrEmpty();
    }
}
