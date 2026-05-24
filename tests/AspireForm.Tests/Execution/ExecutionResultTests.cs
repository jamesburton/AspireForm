using AspireForm.Execution;
using AspireForm.State;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Execution;

public sealed class ExecutionResultTests
{
    [Fact]
    public void Default_ExecuteOptions_does_not_auto_approve_or_force_drift()
    {
        var opts = new ExecuteOptions();
        opts.AutoApprove.Should().BeFalse();
        opts.ForceDrift.Should().BeFalse();
    }

    [Fact]
    public void Success_result_reports_no_failure_and_carries_a_state()
    {
        var result = new ExecutionResult
        {
            Success = true,
            BlocksApplied = 3,
            NewState = new AspireFormState(),
        };

        result.Success.Should().BeTrue();
        result.FailureMessage.Should().BeNull();
        result.BlocksApplied.Should().Be(3);
        result.NewState.Should().NotBeNull();
    }
}
