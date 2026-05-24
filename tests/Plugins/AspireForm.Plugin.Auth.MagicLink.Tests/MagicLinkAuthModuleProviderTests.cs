using System.Text.Json.Nodes;
using AspireForm.Plugin.Auth.MagicLink;
using AspireForm.Providers;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Plugin.Auth.MagicLink.Tests;

/// <summary>Unit tests for <see cref="MagicLinkAuthModuleProvider"/>.</summary>
public sealed class MagicLinkAuthModuleProviderTests
{
    private readonly MagicLinkAuthModuleProvider _provider = new();

    private static PlanContext Ctx(JsonObject inputs) =>
        new("auth", inputs, AppHostDirectory: "./MyApp.AppHost", ProjectName: "MyApp");

    [Fact]
    public void Type_and_kind_are_correct()
    {
        _provider.Type.Should().Be("auth-magiclink");
        _provider.Kind.Should().Be(BlockKind.Module);
    }

    [Fact]
    public void Plan_emits_scaffold_setup_and_managed_region()
    {
        var plan = _provider.Plan(Ctx(new JsonObject { ["fromAddress"] = "noreply@x.com" }));
        plan.FileActions.Should().HaveCount(2);
        plan.FileActions.Single(f => f.OwnershipMode == OwnershipMode.Scaffold)
            .Path.Replace('\\', '/').Should().EndWith("MagicLinkAuthSetup.cs");
        plan.FileActions.Single(f => f.OwnershipMode == OwnershipMode.Managed)
            .BlockMarker.Should().Be("auth-magiclink");
    }

    [Fact]
    public void Plan_setup_embeds_configured_fromAddress_and_lifetime()
    {
        var plan = _provider.Plan(Ctx(new JsonObject
        {
            ["fromAddress"] = "MAILER@example.com",
            ["tokenLifetimeMinutes"] = 30,
        }));
        var scaffold = plan.FileActions.Single(f => f.OwnershipMode == OwnershipMode.Scaffold);
        scaffold.RenderContent().Should().Contain("\"MAILER@example.com\"").And.Contain("= 30");
    }

    [Fact]
    public void Plan_emits_no_CLI_actions()
    {
        _provider.Plan(Ctx(new JsonObject())).CliActions.Should().BeEmpty();
    }
}
