using System.Text.Json.Nodes;
using AspireForm.Providers;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Providers;

public sealed class SqlServerResourceProviderTests
{
    private readonly SqlServerResourceProvider _provider = new();

    private static PlanContext Ctx(JsonObject inputs) =>
        new("sql", inputs, AppHostDirectory: "./MyApp.AppHost", ProjectName: "MyApp");

    [Fact]
    public void Type_and_kind_are_correct()
    {
        _provider.Type.Should().Be("sqlserver");
        _provider.Kind.Should().Be(BlockKind.Resource);
    }

    [Fact]
    public void Plan_emits_an_aspire_add_sqlserver_cli_action()
    {
        var plan = _provider.Plan(Ctx(new JsonObject { ["aspireName"] = "sql" }));

        plan.CliActions.Should().ContainSingle(c => c.Tool == "aspire");
        plan.CliActions[0].Args.Should().ContainInOrder("add", "sqlserver");
    }

    [Fact]
    public void Plan_emits_a_managed_apphost_region_with_the_resource_declaration()
    {
        var inputs = new JsonObject
        {
            ["aspireName"] = "sql",
            ["databases"] = new JsonArray("appdb", "reportdb"),
        };

        var plan = _provider.Plan(Ctx(inputs));

        plan.FileActions.Should().ContainSingle();
        var file = plan.FileActions[0];
        file.OwnershipMode.Should().Be(OwnershipMode.Managed);
        file.BlockMarker.Should().Be("sql");
        file.Path.Replace('\\', '/').Should().Be("./MyApp.AppHost/AppHost.cs");

        var content = file.RenderContent();
        content.Should().Contain("builder.AddSqlServer(\"sql\")");
        content.Should().Contain("AddDatabase(\"appdb\")");
        content.Should().Contain("AddDatabase(\"reportdb\")");
    }

    [Fact]
    public void Plan_uses_block_name_when_aspireName_is_absent()
    {
        var plan = _provider.Plan(Ctx(new JsonObject()));
        plan.FileActions[0].RenderContent().Should().Contain("builder.AddSqlServer(\"sql\")");
    }

    [Fact]
    public void Plan_emits_no_database_calls_when_databases_array_is_absent_or_empty()
    {
        var planEmpty = _provider.Plan(Ctx(new JsonObject { ["databases"] = new JsonArray() }));
        planEmpty.FileActions[0].RenderContent().Should().NotContain(".AddDatabase(");

        var planMissing = _provider.Plan(Ctx(new JsonObject()));
        planMissing.FileActions[0].RenderContent().Should().NotContain(".AddDatabase(");
    }
}
