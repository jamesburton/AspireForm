using System.Text.Json.Nodes;
using AspireForm.Plugin.DAB;
using AspireForm.Providers;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Plugin.DAB.Tests;

public sealed class DabResourceProviderTests
{
    private readonly DabResourceProvider _provider = new();

    private static PlanContext Ctx(JsonObject inputs) =>
        new("dab", inputs, AppHostDirectory: "./MyApp.AppHost", ProjectName: "MyApp");

    [Fact]
    public void Type_and_kind_are_correct()
    {
        _provider.Type.Should().Be("dab");
        _provider.Kind.Should().Be(BlockKind.Resource);
    }

    [Fact]
    public void Plan_emits_aspire_add_dab_and_managed_region()
    {
        var plan = _provider.Plan(Ctx(new JsonObject { ["aspireName"] = "dab" }));
        plan.CliActions.Should().ContainSingle(c => c.Tool == "aspire");
        plan.CliActions[0].Args.Should().ContainInOrder("add", "dab");
        var managed = plan.FileActions.Single(f => f.OwnershipMode == OwnershipMode.Managed);
        managed.RenderContent().Should().Contain("builder.AddDataAPIBuilder(\"dab\")");
    }

    [Fact]
    public void Plan_with_databaseReference_appends_WithReference()
    {
        var plan = _provider.Plan(Ctx(new JsonObject { ["aspireName"] = "dab", ["databaseReference"] = "sql" }));
        var managed = plan.FileActions.Single(f => f.OwnershipMode == OwnershipMode.Managed);
        managed.RenderContent().Should().Contain(".WithReference(sql)");
    }

    [Fact]
    public void Plan_emits_scaffold_dab_config_json()
    {
        var plan = _provider.Plan(Ctx(new JsonObject { ["aspireName"] = "dab" }));
        var scaffold = plan.FileActions.Single(f => f.OwnershipMode == OwnershipMode.Scaffold);
        scaffold.Path.Replace('\\', '/').Should().EndWith("dab-config.json");
        scaffold.RenderContent().Should().Contain("$schema").And.Contain("data-source").And.Contain("\"entities\": {}");
    }
}
