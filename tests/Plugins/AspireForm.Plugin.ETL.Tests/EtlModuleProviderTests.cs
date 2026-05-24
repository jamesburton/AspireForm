using System.Text.Json.Nodes;
using AspireForm.Plugin.ETL;
using AspireForm.Providers;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Plugin.ETL.Tests;

public sealed class EtlModuleProviderTests
{
    private readonly EtlModuleProvider _provider = new();

    private static PlanContext Ctx(JsonObject inputs) =>
        new("etl", inputs, AppHostDirectory: "./MyApp.AppHost", ProjectName: "MyApp");

    [Fact]
    public void Type_and_kind_are_correct()
    {
        _provider.Type.Should().Be("etl");
        _provider.Kind.Should().Be(BlockKind.Module);
    }

    [Fact]
    public void Plan_emits_scaffold_setup_and_managed_region()
    {
        var plan = _provider.Plan(Ctx(new JsonObject { ["watchDirectory"] = "./drop" }));
        plan.FileActions.Should().HaveCount(2);
        plan.FileActions.Single(f => f.OwnershipMode == OwnershipMode.Scaffold)
            .Path.Replace('\\', '/').Should().EndWith("EtlSetup.cs");
        plan.FileActions.Single(f => f.OwnershipMode == OwnershipMode.Managed)
            .BlockMarker.Should().Be("etl");
    }

    [Fact]
    public void Plan_setup_embeds_configured_watch_directory_and_parsers()
    {
        var plan = _provider.Plan(Ctx(new JsonObject
        {
            ["watchDirectory"] = "./drop",
            ["parsers"] = new JsonArray("csv"),
        }));
        var content = plan.FileActions.Single(f => f.OwnershipMode == OwnershipMode.Scaffold).RenderContent();
        content.Should().Contain("\"./drop\"").And.Contain("\"csv\"");
    }

    [Fact]
    public void Plan_emits_no_CLI_actions()
    {
        _provider.Plan(Ctx(new JsonObject())).CliActions.Should().BeEmpty();
    }
}
