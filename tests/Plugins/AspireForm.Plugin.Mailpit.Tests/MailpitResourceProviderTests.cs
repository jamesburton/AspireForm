using System.Text.Json.Nodes;
using AspireForm.Plugin.Mailpit;
using AspireForm.Providers;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Plugin.Mailpit.Tests;

public sealed class MailpitResourceProviderTests
{
    private readonly MailpitResourceProvider _provider = new();

    private static PlanContext Ctx(JsonObject inputs) =>
        new("mail", inputs, AppHostDirectory: "./MyApp.AppHost", ProjectName: "MyApp");

    [Fact]
    public void Type_and_kind_are_correct()
    {
        _provider.Type.Should().Be("mailpit");
        _provider.Kind.Should().Be(BlockKind.Resource);
    }

    [Fact]
    public void Plan_emits_aspire_add_mailpit_and_managed_AppHost_region()
    {
        var plan = _provider.Plan(Ctx(new JsonObject { ["aspireName"] = "mail" }));

        plan.CliActions.Should().ContainSingle(c => c.Tool == "aspire");
        plan.CliActions[0].Args.Should().ContainInOrder("add", "mailpit");

        plan.FileActions.Should().ContainSingle();
        plan.FileActions[0].OwnershipMode.Should().Be(OwnershipMode.Managed);
        plan.FileActions[0].RenderContent().Should().Contain("builder.AddMailPit(\"mail\")");
    }

    [Fact]
    public void Plan_appends_WithDataVolume_when_withDataVolume_is_true()
    {
        var inputs = new JsonObject { ["aspireName"] = "mail", ["withDataVolume"] = true };
        _provider.Plan(Ctx(inputs)).FileActions[0].RenderContent()
            .Should().Contain(".WithDataVolume()");
    }

    [Fact]
    public void Plan_defaults_aspireName_to_block_name()
    {
        _provider.Plan(Ctx(new JsonObject())).FileActions[0].RenderContent()
            .Should().Contain("builder.AddMailPit(\"mail\")");
    }
}
