using AspireForm.Planning;
using AspireForm.Providers;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Planning;

public sealed class PlanRendererTests
{
    [Fact]
    public void Render_of_empty_plan_says_nothing_to_do()
    {
        var output = PlanRenderer.Render(new Plan());
        output.Should().Contain("No changes");
    }

    [Fact]
    public void Render_of_create_block_includes_block_header_and_file_actions()
    {
        var plan = new Plan
        {
            Blocks =
            [
                new BlockAction("sql", BlockKind.Resource, BlockActionKind.Create,
                [
                    new FileActionPlan(
                        Path: "MyApp.AppHost/AppHost.cs",
                        OwnershipMode: OwnershipMode.Managed, BlockMarker: "sql",
                        Kind: FileActionKind.Create,
                        DriftDetected: false,
                        BeforeContent: null,
                        AfterContent: "var sql = builder.AddSqlServer(\"sql\");"),
                ])
                {
                    CliActions = [new PlannedCliAction("aspire", ["add", "sqlserver"])],
                },
            ],
        };

        var output = PlanRenderer.Render(plan);

        output.Should().Contain("+ sql").And.Contain("CREATE").And.Contain("sqlserver");
        output.Should().Contain("MyApp.AppHost/AppHost.cs");
        output.Should().Contain("aspire add sqlserver");
        output.Should().Contain("+ var sql = builder.AddSqlServer");
    }

    [Fact]
    public void Render_marks_drift_for_each_drifted_file()
    {
        var plan = new Plan
        {
            Blocks =
            [
                new BlockAction("sql", BlockKind.Resource, BlockActionKind.Update,
                [
                    new FileActionPlan(
                        Path: "AppHost.cs",
                        OwnershipMode: OwnershipMode.Managed, BlockMarker: "sql",
                        Kind: FileActionKind.Modify,
                        DriftDetected: true,
                        BeforeContent: "old\n", AfterContent: "new\n"),
                ]),
            ],
        };

        var output = PlanRenderer.Render(plan);
        output.Should().Contain("DRIFT");
    }

    [Fact]
    public void Render_of_delete_block_shows_minus_marker()
    {
        var plan = new Plan
        {
            Blocks =
            [
                new BlockAction("sql", BlockKind.Resource, BlockActionKind.Delete,
                [
                    new FileActionPlan(
                        Path: "AppHost.cs",
                        OwnershipMode: OwnershipMode.Managed, BlockMarker: "sql",
                        Kind: FileActionKind.Remove,
                        DriftDetected: false,
                        BeforeContent: "x\n", AfterContent: null),
                ]),
            ],
        };

        var output = PlanRenderer.Render(plan);
        output.Should().Contain("- sql").And.Contain("DELETE");
    }
}
