using AspireForm.Ui.Theme;
using AspireForm.Ui.Components.Layout;
using AwesomeAssertions;
using BlazorBlueprint.Components;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

// Alias to avoid ambiguity with xunit.v3's static Xunit.TestContext.
using BunitTestContext = Bunit.TestContext;

namespace AspireForm.Tests.Ui.Layout;

public sealed class MainLayoutTests
{
    [Fact]
    public void MainLayout_renders_sidebar_topbar_and_body_content()
    {
        using var ctx = CreateContext();

        var cut = ctx.RenderComponent<MainLayout>(parameters => parameters
            .Add(p => p.Body, builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "id", "body-marker");
                builder.AddContent(2, "body-content-marker");
                builder.CloseElement();
            }));

        // The sidebar brand text + the user-supplied body marker should both render.
        cut.Markup.Should().Contain("AspireForm");
        cut.Markup.Should().Contain("body-content-marker");
        // Sidebar = <aside>, top bar = <header>, page content slot = <main>.
        cut.FindAll("aside").Should().NotBeEmpty();
        cut.FindAll("header").Should().NotBeEmpty();
        cut.FindAll("main").Should().NotBeEmpty();
    }

    [Fact]
    public void MainLayout_does_not_render_vestigial_bbportalhost_element()
    {
        // Regression: <BbPortalHost /> is not a real component in BlazorBlueprint.Components
        // (only BbDialogPortal / BbAlertDialogPortal / BbSheetPortal exist, each self-portaling).
        // It was previously rendering as a literal <bbportalhost> tag, producing an RZ10012
        // warning and serving no purpose.
        using var ctx = CreateContext();

        var cut = ctx.RenderComponent<MainLayout>(parameters => parameters
            .Add(p => p.Body, builder => builder.AddContent(0, "")));

        cut.Markup.Should().NotContain("bbportalhost", "the fake BbPortalHost element was removed");
        cut.Markup.Should().NotContain("BbPortalHost");
    }

    private static BunitTestContext CreateContext()
    {
        var ctx = new BunitTestContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.Services.AddSingleton<IThemeStore>(new FakeThemeStore());
        return ctx;
    }

    private sealed class FakeThemeStore : IThemeStore
    {
        private static readonly ThemeDefinition DefaultTheme = ThemeDefaults.BuiltIn()[0];

        public Task<IReadOnlyList<ThemeSummary>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ThemeSummary>>([new ThemeSummary(DefaultTheme.Name, DefaultTheme.Description, true)]);

        public Task<ThemeDefinition> GetAsync(string name, CancellationToken ct = default) =>
            Task.FromResult(DefaultTheme);

        public Task SaveAsync(ThemeDefinition theme, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(string name, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string> DuplicateAsync(string sourceName, string newName, CancellationToken ct = default) => Task.FromResult(newName);
        public Task RenameAsync(string oldName, string newName, CancellationToken ct = default) => Task.CompletedTask;
        public Task<ThemeActivation> GetActiveAsync(CancellationToken ct = default) => Task.FromResult(new ThemeActivation(DefaultTheme.Name, false));
        public Task SetActiveAsync(string name, CancellationToken ct = default) => Task.CompletedTask;
        public Task SetDarkModeAsync(bool dark, CancellationToken ct = default) => Task.CompletedTask;
        public Task ResetToDefaultsAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
