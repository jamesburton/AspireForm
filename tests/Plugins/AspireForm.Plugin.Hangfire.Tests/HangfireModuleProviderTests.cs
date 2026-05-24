using System.Text.Json.Nodes;
using AspireForm.Plugin.Hangfire;
using AspireForm.Providers;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Plugin.Hangfire.Tests;

public sealed class HangfireModuleProviderTests
{
    private readonly HangfireModuleProvider _provider = new();

    private static PlanContext Ctx(JsonObject inputs) =>
        new("jobs", inputs, AppHostDirectory: "./MyApp.AppHost", ProjectName: "MyApp");

    [Fact]
    public void Type_and_kind_are_correct()
    {
        _provider.Type.Should().Be("hangfire");
        _provider.Kind.Should().Be(BlockKind.Module);
    }

    [Fact]
    public void Plan_with_sql_storage_emits_scaffold_and_managed_actions()
    {
        var plan = _provider.Plan(Ctx(new JsonObject { ["storage"] = "sql", ["dashboardPath"] = "/jobs" }));
        plan.FileActions.Should().HaveCount(2);
        var scaffold = plan.FileActions.Single(f => f.OwnershipMode == OwnershipMode.Scaffold);
        scaffold.RenderContent().Should().Contain("UseSqlServerStorage");
        var managed = plan.FileActions.Single(f => f.OwnershipMode == OwnershipMode.Managed);
        managed.RenderContent().Should().Contain("storage=sql").And.Contain("dashboard=/jobs");
    }

    [Fact]
    public void Plan_with_redis_storage_uses_redis_helper()
    {
        var plan = _provider.Plan(Ctx(new JsonObject { ["storage"] = "redis" }));
        var scaffold = plan.FileActions.Single(f => f.OwnershipMode == OwnershipMode.Scaffold);
        scaffold.RenderContent().Should().Contain("UseRedisStorage");
        var managed = plan.FileActions.Single(f => f.OwnershipMode == OwnershipMode.Managed);
        managed.RenderContent().Should().Contain("storage=redis");
    }

    [Fact]
    public void Plan_emits_no_CLI_actions_in_v1()
    {
        _provider.Plan(Ctx(new JsonObject { ["storage"] = "sql" })).CliActions.Should().BeEmpty();
    }
}
