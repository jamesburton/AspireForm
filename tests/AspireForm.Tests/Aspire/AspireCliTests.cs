using AspireForm.Aspire;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Aspire;

public sealed class AspireCliTests
{
    [Fact]
    public async Task IsAvailableAsync_returns_false_when_the_executable_does_not_exist()
    {
        var cli = new AspireCli(executablePath: "definitely-not-a-real-command-xyz");
        (await cli.IsAvailableAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task GetVersionAsync_returns_null_when_the_executable_does_not_exist()
    {
        var cli = new AspireCli(executablePath: "definitely-not-a-real-command-xyz");
        (await cli.GetVersionAsync()).Should().BeNull();
    }
}
