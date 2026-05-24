using System.Text.Json.Nodes;
using AspireForm.Plugin.Reporting;
using AspireForm.Providers;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Plugin.Reporting.Tests;

public sealed class ReportingModuleProviderTests
{
    private readonly ReportingModuleProvider _provider = new();

    private static PlanContext Ctx(JsonObject inputs) =>
        new("reports", inputs, AppHostDirectory: "./MyApp.AppHost", ProjectName: "MyApp");

    [Fact]
    public void Type_and_kind_are_correct()
    {
        _provider.Type.Should().Be("reporting");
        _provider.Kind.Should().Be(BlockKind.Module);
    }

    [Fact]
    public void Plan_emits_scaffold_dab_reports_json()
    {
        var plan = _provider.Plan(Ctx(new JsonObject { ["views"] = new JsonArray() }));
        plan.FileActions.Should().ContainSingle();
        plan.FileActions[0].OwnershipMode.Should().Be(OwnershipMode.Scaffold);
        plan.FileActions[0].Path.Replace('\\', '/').Should().EndWith("dab-reports.json");
    }

    [Fact]
    public void Plan_renders_each_view_as_a_dab_entity()
    {
        var inputs = new JsonObject
        {
            ["views"] = new JsonArray(
                new JsonObject { ["name"] = "Sales", ["source"] = "dbo.Sales" }),
        };

        var content = _provider.Plan(Ctx(inputs)).FileActions[0].RenderContent();

        content.Should().Contain("\"Sales\"");
        content.Should().Contain("\"dbo.Sales\"");
        content.Should().Contain("\"anonymous\"");
        content.Should().Contain("\"read\"");
    }

    [Fact]
    public void Plan_emits_no_CLI_actions()
    {
        _provider.Plan(Ctx(new JsonObject { ["views"] = new JsonArray() })).CliActions.Should().BeEmpty();
    }
}
