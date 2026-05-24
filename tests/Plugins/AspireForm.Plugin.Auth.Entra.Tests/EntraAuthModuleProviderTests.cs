using System.Text.Json.Nodes;
using AspireForm.Plugin.Auth.Entra;
using AspireForm.Providers;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Plugin.Auth.Entra.Tests;

/// <summary>Unit tests for <see cref="EntraAuthModuleProvider"/>.</summary>
public sealed class EntraAuthModuleProviderTests
{
    private readonly EntraAuthModuleProvider _provider = new();

    private static PlanContext Ctx(JsonObject inputs) =>
        new("auth", inputs, AppHostDirectory: "./MyApp.AppHost", ProjectName: "MyApp");

    [Fact]
    public void Type_and_kind_are_correct()
    {
        _provider.Type.Should().Be("auth-entra");
        _provider.Kind.Should().Be(BlockKind.Module);
    }

    [Fact]
    public void Plan_emits_scaffold_setup_and_managed_region()
    {
        var plan = _provider.Plan(Ctx(new JsonObject
        {
            ["tenantId"] = "tid", ["clientId"] = "cid",
        }));
        plan.FileActions.Should().HaveCount(2);
        plan.FileActions.Single(f => f.OwnershipMode == OwnershipMode.Scaffold)
            .Path.Replace('\\', '/').Should().EndWith("EntraAuthSetup.cs");
        plan.FileActions.Single(f => f.OwnershipMode == OwnershipMode.Managed)
            .BlockMarker.Should().Be("auth-entra");
    }

    [Fact]
    public void Plan_setup_embeds_tenant_client_and_audience()
    {
        var plan = _provider.Plan(Ctx(new JsonObject
        {
            ["tenantId"] = "my-tenant",
            ["clientId"] = "my-client",
            ["audience"] = "my-audience",
        }));
        var scaffold = plan.FileActions.Single(f => f.OwnershipMode == OwnershipMode.Scaffold);
        var content = scaffold.RenderContent();
        content.Should().Contain("\"my-tenant\"").And.Contain("\"my-client\"").And.Contain("\"my-audience\"");
    }

    [Fact]
    public void Plan_emits_no_CLI_actions()
    {
        _provider.Plan(Ctx(new JsonObject())).CliActions.Should().BeEmpty();
    }
}
