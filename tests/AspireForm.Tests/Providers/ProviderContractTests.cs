using System.Text.Json.Nodes;
using AspireForm.Providers;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Providers;

public sealed class ProviderContractTests
{
    [Fact]
    public void PlannedFileAction_carries_path_mode_and_content_renderer()
    {
        var action = new PlannedFileAction(
            Path: "MyApp.AppHost/AppHost.cs",
            OwnershipMode: OwnershipMode.Managed,
            BlockMarker: "sql",
            RenderContent: () => "rendered");

        action.OwnershipMode.Should().Be(OwnershipMode.Managed);
        action.RenderContent().Should().Be("rendered");
    }

    [Fact]
    public void PlannedCliAction_carries_tool_and_args()
    {
        var action = new PlannedCliAction("aspire", new[] { "add", "sqlserver" });
        action.Tool.Should().Be("aspire");
        action.Args.Should().Equal("add", "sqlserver");
    }

    [Fact]
    public void ProviderPlan_defaults_both_collections_to_empty()
    {
        var plan = new ProviderPlan();
        plan.FileActions.Should().BeEmpty();
        plan.CliActions.Should().BeEmpty();
    }

    [Fact]
    public void PlanContext_exposes_block_name_inputs_and_apphost_dir()
    {
        var ctx = new PlanContext(
            BlockName: "sql",
            Inputs: new JsonObject { ["aspireName"] = "sql" },
            AppHostDirectory: "./MyApp.AppHost",
            ProjectName: "MyApp");

        ctx.BlockName.Should().Be("sql");
        ctx.Inputs["aspireName"]!.GetValue<string>().Should().Be("sql");
        ctx.AppHostDirectory.Should().Be("./MyApp.AppHost");
        ctx.ProjectName.Should().Be("MyApp");
    }
}
