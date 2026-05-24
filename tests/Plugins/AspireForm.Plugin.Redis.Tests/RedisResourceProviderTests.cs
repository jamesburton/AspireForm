using System.Text.Json.Nodes;
using AspireForm.Plugin.Redis;
using AspireForm.Providers;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Plugin.Redis.Tests;

public sealed class RedisResourceProviderTests
{
    private readonly RedisResourceProvider _provider = new();

    private static PlanContext Ctx(JsonObject inputs) =>
        new("cache", inputs, AppHostDirectory: "./MyApp.AppHost", ProjectName: "MyApp");

    [Fact]
    public void Type_and_kind_are_correct()
    {
        _provider.Type.Should().Be("redis");
        _provider.Kind.Should().Be(BlockKind.Resource);
    }

    [Fact]
    public void Plan_emits_aspire_add_redis_and_managed_AppHost_region()
    {
        var plan = _provider.Plan(Ctx(new JsonObject { ["aspireName"] = "cache" }));

        plan.CliActions.Should().ContainSingle(c => c.Tool == "aspire");
        plan.CliActions[0].Args.Should().ContainInOrder("add", "redis");

        plan.FileActions.Should().ContainSingle();
        plan.FileActions[0].OwnershipMode.Should().Be(OwnershipMode.Managed);
        plan.FileActions[0].RenderContent().Should().Contain("builder.AddRedis(\"cache\")");
    }

    [Fact]
    public void Plan_appends_WithDataVolume_when_withDataVolume_is_true()
    {
        var inputs = new JsonObject { ["aspireName"] = "cache", ["withDataVolume"] = true };
        _provider.Plan(Ctx(inputs)).FileActions[0].RenderContent()
            .Should().Contain(".WithDataVolume()");
    }

    [Fact]
    public void Plan_defaults_aspireName_to_block_name()
    {
        _provider.Plan(Ctx(new JsonObject())).FileActions[0].RenderContent()
            .Should().Contain("builder.AddRedis(\"cache\")");
    }
}
