using AspireForm.Ui.Components.Layout;
using AwesomeAssertions;
using BlazorBlueprint.Components;
using Bunit;
using Xunit;

// Alias to avoid ambiguity with xunit.v3's static Xunit.TestContext.
using BunitTestContext = Bunit.TestContext;

namespace AspireForm.Tests.Ui.Layout;

public sealed class AppSidebarTests
{
    [Fact]
    public void AppSidebar_renders_brand_name()
    {
        using var ctx = new BunitTestContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddBlazorBlueprintComponents();

        var cut = ctx.RenderComponent<AppSidebar>();

        cut.Markup.Should().Contain("AspireForm");
    }

    [Fact]
    public void AppSidebar_renders_all_navigation_links()
    {
        using var ctx = new BunitTestContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddBlazorBlueprintComponents();

        var cut = ctx.RenderComponent<AppSidebar>();

        // Primary navigation items.
        cut.Markup.Should().Contain("Home");
        cut.Markup.Should().Contain("Entities");
        cut.Markup.Should().Contain("Endpoints");
        cut.Markup.Should().Contain("Theme");
        cut.Markup.Should().Contain("Diagnostics");
        // Footer link.
        cut.Markup.Should().Contain("About");
    }

    [Fact]
    public void AppSidebar_renders_version_badge()
    {
        using var ctx = new BunitTestContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddBlazorBlueprintComponents();

        var cut = ctx.RenderComponent<AppSidebar>();

        cut.Markup.Should().Contain("v1.0");
    }

    [Fact]
    public void AppSidebar_nav_links_have_correct_hrefs()
    {
        using var ctx = new BunitTestContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddBlazorBlueprintComponents();

        var cut = ctx.RenderComponent<AppSidebar>();

        var anchors = cut.FindAll("a");
        var hrefs = anchors.Select(a => a.GetAttribute("href")).Where(h => h is not null).ToList();

        hrefs.Should().Contain("/entities");
        hrefs.Should().Contain("/endpoints");
        hrefs.Should().Contain("/theme");
        hrefs.Should().Contain("/diagnostics");
        hrefs.Should().Contain("/about");
    }
}
