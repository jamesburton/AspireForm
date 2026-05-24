using AspireForm.Cli;
using AwesomeAssertions;
using Spectre.Console.Cli;
using Xunit;

namespace AspireForm.Tests.Cli;

[Collection(nameof(ConsoleCaptureCollection))]
public sealed class NewCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-new-cmd").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private (int ExitCode, string StdOut, string StdErr) RunNew(params string[] args)
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
            app.Configure(c => c.AddCommand<NewCommand>("new"));
            return (app.Run(["new", .. args]), stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void New_creates_an_AppHost_project_and_a_starter_aspireform_yaml()
    {
        var (exitCode, stdout, _) = RunNew("MyDemoApp", "--output", _dir);

        exitCode.Should().Be(0);
        stdout.Should().Contain("Created");

        var apphostDir = Path.Combine(_dir, "MyDemoApp", "MyDemoApp.AppHost");
        Directory.Exists(apphostDir).Should().BeTrue();
        File.Exists(Path.Combine(apphostDir, "AppHost.cs")).Should().BeTrue();
        File.Exists(Path.Combine(_dir, "MyDemoApp", "aspireform.yaml")).Should().BeTrue();
    }
}
