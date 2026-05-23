using AspireForm.Planning;
using AspireForm.Providers;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Planning;

public sealed class PlanModelTests
{
    [Fact]
    public void Empty_plan_is_a_noop()
    {
        var plan = new Plan();
        plan.Blocks.Should().BeEmpty();
        plan.HasChanges.Should().BeFalse();
    }

    [Fact]
    public void HasChanges_is_true_when_any_block_action_is_not_noop()
    {
        var plan = new Plan
        {
            Blocks =
            [
                new BlockAction("sql", BlockKind.Resource, BlockActionKind.Create, []),
            ],
        };

        plan.HasChanges.Should().BeTrue();
    }

    [Fact]
    public void FileActionPlan_carries_path_mode_marker_and_resolved_kind()
    {
        var fa = new FileActionPlan(
            Path: "MyApp.AppHost/AppHost.cs",
            OwnershipMode: OwnershipMode.Managed,
            BlockMarker: "sql",
            Kind: FileActionKind.Create,
            DriftDetected: false,
            BeforeContent: null,
            AfterContent: "rendered");

        fa.Kind.Should().Be(FileActionKind.Create);
        fa.DriftDetected.Should().BeFalse();
    }
}
