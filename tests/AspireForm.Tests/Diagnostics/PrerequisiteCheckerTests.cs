using AspireForm.Aspire;
using AspireForm.Diagnostics;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Diagnostics;

public sealed class PrerequisiteCheckerTests
{
    private sealed class FakeAspireCli(string? version) : IAspireCli
    {
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(version is not null);

        public Task<string?> GetVersionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(version);
    }

    [Fact]
    public async Task Reports_aspire_cli_as_ok_when_available()
    {
        var checker = new PrerequisiteChecker(new FakeAspireCli("13.3.4"));
        var report = await checker.RunAsync();

        var aspireCheck = report.Checks.Single(c => c.Name == "aspire CLI");
        aspireCheck.Ok.Should().BeTrue();
        aspireCheck.Detail.Should().Contain("13.3.4");
    }

    [Fact]
    public async Task Reports_aspire_cli_as_failed_with_a_remedy_when_missing()
    {
        var checker = new PrerequisiteChecker(new FakeAspireCli(version: null));
        var report = await checker.RunAsync();

        var aspireCheck = report.Checks.Single(c => c.Name == "aspire CLI");
        aspireCheck.Ok.Should().BeFalse();
        aspireCheck.Remedy.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Includes_a_dotnet_sdk_check()
    {
        var checker = new PrerequisiteChecker(new FakeAspireCli("13.3.4"));
        var report = await checker.RunAsync();
        report.Checks.Should().Contain(c => c.Name == ".NET SDK");
    }

    [Fact]
    public async Task AllPassed_is_false_when_any_required_check_fails()
    {
        var checker = new PrerequisiteChecker(new FakeAspireCli(version: null));
        var report = await checker.RunAsync();
        report.AllPassed.Should().BeFalse();
    }
}
