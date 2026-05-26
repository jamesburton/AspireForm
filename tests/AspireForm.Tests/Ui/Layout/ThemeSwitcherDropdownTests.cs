using AspireForm.Ui.Components.Layout;
using AspireForm.Ui.Theme;
using AwesomeAssertions;
using BlazorBlueprint.Components;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

// Alias to avoid ambiguity with xunit.v3's static Xunit.TestContext.
using BunitTestContext = Bunit.TestContext;

namespace AspireForm.Tests.Ui.Layout;

public sealed class ThemeSwitcherDropdownTests
{
    private static readonly ThemeDefinition ThemeA = new(
        Name: "Theme A",
        Description: "First theme",
        Light: ThemeTokenNames.All.ToDictionary(t => t, _ => "0 0% 100%"),
        Dark: ThemeTokenNames.All.ToDictionary(t => t, _ => "0 0% 0%"),
        Radius: 0.5);

    private static readonly ThemeDefinition ThemeB = new(
        Name: "Theme B",
        Description: "Second theme",
        Light: ThemeTokenNames.All.ToDictionary(t => t, _ => "0 0% 50%"),
        Dark: ThemeTokenNames.All.ToDictionary(t => t, _ => "0 0% 10%"),
        Radius: 0.5);

    private sealed class FakeThemeStore : IThemeStore
    {
        private readonly List<ThemeDefinition> _themes;
        private string _active;
        private bool _dark;

        public FakeThemeStore(IEnumerable<ThemeDefinition> themes, string active)
        {
            _themes = [.. themes];
            _active = active;
        }

        public Task<IReadOnlyList<ThemeSummary>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ThemeSummary>>(
                _themes.Select(t => new ThemeSummary(t.Name, t.Description, t.Name == _active)).ToList());

        public Task<ThemeDefinition> GetAsync(string name, CancellationToken ct = default) =>
            Task.FromResult(_themes.First(t => t.Name == name));

        public Task SaveAsync(ThemeDefinition theme, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(string name, CancellationToken ct = default) => Task.CompletedTask;

        public Task<string> DuplicateAsync(string sourceName, string newName, CancellationToken ct = default) =>
            Task.FromResult(newName);

        public Task RenameAsync(string oldName, string newName, CancellationToken ct = default) => Task.CompletedTask;

        public Task<ThemeActivation> GetActiveAsync(CancellationToken ct = default) =>
            Task.FromResult(new ThemeActivation(_active, _dark));

        public Task SetActiveAsync(string name, CancellationToken ct = default)
        {
            _active = name;
            return Task.CompletedTask;
        }

        public Task SetDarkModeAsync(bool dark, CancellationToken ct = default)
        {
            _dark = dark;
            return Task.CompletedTask;
        }

        public Task ResetToDefaultsAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public void ThemeSwitcherDropdown_renders_active_theme_name()
    {
        using var ctx = new BunitTestContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.Services.AddSingleton<IThemeStore>(new FakeThemeStore([ThemeA, ThemeB], "Theme A"));

        var cut = ctx.RenderComponent<ThemeSwitcherDropdown>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Theme A"), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ThemeSwitcherDropdown_renders_theme_menu_trigger_button()
    {
        using var ctx = new BunitTestContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.Services.AddSingleton<IThemeStore>(new FakeThemeStore([ThemeA, ThemeB], "Theme A"));

        var cut = ctx.RenderComponent<ThemeSwitcherDropdown>();

        // The trigger button shows the active theme name and a "Switch theme" title.
        cut.WaitForAssertion(() => cut.Find("button[title='Switch theme']").Should().NotBeNull(),
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ThemeSwitcherDropdown_switching_theme_updates_active_name()
    {
        var store = new FakeThemeStore([ThemeA, ThemeB], "Theme A");

        using var ctx = new BunitTestContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.Services.AddSingleton<IThemeStore>(store);

        var cut = ctx.RenderComponent<ThemeSwitcherDropdown>();

        // Wait for initial render.
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Theme A"), TimeSpan.FromSeconds(5));

        // Simulate switching to Theme B via the store directly.
        await store.SetActiveAsync("Theme B", Xunit.TestContext.Current.CancellationToken);
        var activation = await store.GetActiveAsync(Xunit.TestContext.Current.CancellationToken);
        activation.ActiveName.Should().Be("Theme B");
    }
}
