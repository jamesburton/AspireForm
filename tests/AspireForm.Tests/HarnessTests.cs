using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests;

/// <summary>Confirms the xUnit v3 / MTP harness and AwesomeAssertions are wired up.</summary>
public sealed class HarnessTests
{
    [Fact]
    public void Harness_runs_and_assertions_work()
    {
        const int answer = 42;
        answer.Should().Be(42);
    }
}
