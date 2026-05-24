using System.Text.Json.Nodes;
using AspireForm.Plugin.Auth.ApiKey;
using AspireForm.Providers;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Plugin.Auth.ApiKey.Tests;

/// <summary>Unit tests for <see cref="ApiKeyAuthModuleProvider"/>.</summary>
public sealed class ApiKeyAuthModuleProviderTests
{
    private readonly ApiKeyAuthModuleProvider _provider = new();

    private static PlanContext Ctx(JsonObject inputs) =>
        new("auth", inputs, AppHostDirectory: "./MyApp.AppHost", ProjectName: "MyApp");

    [Fact]
    public void Type_and_kind_are_correct()
    {
        _provider.Type.Should().Be("auth-apikey");
        _provider.Kind.Should().Be(BlockKind.Module);
    }

    [Fact]
    public void Plan_emits_scaffold_setup_file_and_managed_region()
    {
        var plan = _provider.Plan(Ctx(new JsonObject()));
        plan.FileActions.Should().HaveCount(2);
        plan.FileActions.Single(f => f.OwnershipMode == OwnershipMode.Scaffold)
            .Path.Replace('\\', '/').Should().EndWith("ApiKeyAuthSetup.cs");
        plan.FileActions.Single(f => f.OwnershipMode == OwnershipMode.Managed)
            .BlockMarker.Should().Be("auth-apikey");
    }

    [Fact]
    public void Plan_setup_file_includes_configured_header_name()
    {
        var plan = _provider.Plan(Ctx(new JsonObject { ["headerName"] = "X-Custom-Key" }));
        var scaffold = plan.FileActions.Single(f => f.OwnershipMode == OwnershipMode.Scaffold);
        scaffold.RenderContent().Should().Contain("\"X-Custom-Key\"");
    }

    [Fact]
    public void Plan_emits_no_CLI_actions()
    {
        _provider.Plan(Ctx(new JsonObject())).CliActions.Should().BeEmpty();
    }
}
