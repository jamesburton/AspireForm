using AspireForm.Aspire;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Aspire;

public sealed class AspireCliRunAsyncTests
{
    [Fact]
    public async Task RunAsync_returns_failure_when_the_executable_does_not_exist()
    {
        var cli = new AspireCli(executablePath: "definitely-not-a-real-command-xyz");
        var result = await cli.RunAsync(args: ["--version"], workingDirectory: Environment.CurrentDirectory, TestContext.Current.CancellationToken);
        result.ExitCode.Should().NotBe(0);
    }

    [Fact]
    public async Task RunAsync_captures_stdout_when_invoking_a_real_command()
    {
        // 'dotnet --version' is guaranteed available on this machine.
        var cli = new AspireCli(executablePath: "dotnet");
        var result = await cli.RunAsync(args: ["--version"], workingDirectory: Environment.CurrentDirectory, TestContext.Current.CancellationToken);
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Trim().Should().MatchRegex(@"^\d+\.\d+\.\d+");
    }
}
