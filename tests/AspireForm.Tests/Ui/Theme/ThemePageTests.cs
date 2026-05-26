using AspireForm.Ui.Theme;
using AwesomeAssertions;
using BlazorBlueprint.Components;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

// Alias to avoid ambiguity with xunit.v3's static Xunit.TestContext.
using BunitTestContext = Bunit.TestContext;

// Alias because 'Theme' is ambiguous with the 'AspireForm.Ui.Theme' namespace.
using ThemePage = AspireForm.Ui.Components.Pages.Theme;

namespace AspireForm.Tests.Ui.Theme;

public sealed class ThemePageTests
{
    private static readonly ThemeDefinition DefaultTheme = new(
        Name: "Test Theme",
        Description: "A test theme",
        Light: ThemeTokenNames.All.ToDictionary(t => t, _ => "0 0% 100%"),
        Dark: ThemeTokenNames.All.ToDictionary(t => t, _ => "0 0% 0%"),
        Radius: 0.5);

    private sealed class FakeThemeStore : IThemeStore
    {
        private readonly List<ThemeDefinition> _themes = [DefaultTheme];
        private string _active = "Test Theme";
        private bool _dark;

        public Task<IReadOnlyList<ThemeSummary>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ThemeSummary>>(
                _themes.Select(t => new ThemeSummary(t.Name, t.Description, t.Name == _active)).ToList());

        public Task<ThemeDefinition> GetAsync(string name, CancellationToken ct = default)
        {
            var theme = _themes.FirstOrDefault(t => t.Name == name)
                ?? throw new ThemeLoadException($"Theme '{name}' not found.");
            return Task.FromResult(theme);
        }

        public Task SaveAsync(ThemeDefinition theme, CancellationToken ct = default)
        {
            var idx = _themes.FindIndex(t => t.Name == theme.Name);
            if (idx >= 0) _themes[idx] = theme; else _themes.Add(theme);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string name, CancellationToken ct = default)
        {
            _themes.RemoveAll(t => t.Name == name);
            return Task.CompletedTask;
        }

        public Task<string> DuplicateAsync(string sourceName, string newName, CancellationToken ct = default)
        {
            var src = _themes.First(t => t.Name == sourceName);
            _themes.Add(src with { Name = newName });
            return Task.FromResult(newName);
        }

        public Task RenameAsync(string oldName, string newName, CancellationToken ct = default)
        {
            var idx = _themes.FindIndex(t => t.Name == oldName);
            if (idx >= 0) _themes[idx] = _themes[idx] with { Name = newName };
            return Task.CompletedTask;
        }

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

        public Task ResetToDefaultsAsync(CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    [Fact]
    public void ThemePage_renders_theme_sidebar_and_detail_panel()
    {
        using var ctx = new BunitTestContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.JSInterop.SetupModule("./_content/BlazorBlueprint.Components/js/text-input.js");
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.Services.AddSingleton<IThemeStore>(new FakeThemeStore());

        var cut = ctx.RenderComponent<ThemePage>();

        // Theme name should appear in sidebar.
        cut.Markup.Should().Contain("Test Theme");
        // Should render tab triggers.
        cut.Markup.Should().Contain("Light tokens");
        cut.Markup.Should().Contain("Dark tokens");
    }

    [Fact]
    public void ThemePage_renders_import_export_section()
    {
        using var ctx = new BunitTestContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.JSInterop.SetupModule("./_content/BlazorBlueprint.Components/js/text-input.js");
        ctx.Services.AddBlazorBlueprintComponents();
        ctx.Services.AddSingleton<IThemeStore>(new FakeThemeStore());

        var cut = ctx.RenderComponent<ThemePage>();
        cut.Markup.Should().Contain("Import");
        cut.Markup.Should().Contain("Export");
    }
}
