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

public sealed class AppTopBarTests
{
    [Fact]
    public void AppTopBar_renders_theme_switcher_and_dark_mode_toggle()
    {
        using var ctx = CreateContext();

        var cut = ctx.RenderComponent<AppTopBar>();

        // The theme-switcher trigger button is keyed by title="Switch theme".
        cut.WaitForAssertion(() => cut.Find("button[title='Switch theme']").Should().NotBeNull(),
            TimeSpan.FromSeconds(5));
        // Dark-mode toggle is also a button — its accessible label / title pins it down.
        cut.FindAll("button").Should().HaveCountGreaterThan(1, "both ThemeSwitcherDropdown and DarkModeToggle render buttons");
    }

    [Fact]
    public void AppTopBar_renders_provided_page_title()
    {
        using var ctx = CreateContext();

        var cut = ctx.RenderComponent<AppTopBar>(p => p.Add(x => x.PageTitle, "Entities"));

        cut.Markup.Should().Contain("Entities");
    }

    [Fact]
    public void AppTopBar_omits_title_span_when_page_title_is_null()
    {
        using var ctx = CreateContext();

        var cut = ctx.RenderComponent<AppTopBar>();

        // No PageTitle parameter → the conditional <span> should not appear.
        // We check by counting <span> elements; the topbar has none of its own,
        // so the only spans present come from the theme switcher trigger.
        var titleSpans = cut.FindAll("header > div > span");
        titleSpans.Should().BeEmpty();
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

        public Task<ThemeDefinition> GetAsync(string name, CancellationToken ct = default) => Task.FromResult(DefaultTheme);
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
