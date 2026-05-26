using AspireForm.Ui.Theme;
using AwesomeAssertions;
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
    private sealed class FakeThemeStore : IThemeStore
    {
        public IReadOnlyDictionary<string, string> GetTokens() =>
            ThemeDefaults.Tokens.ToDictionary(t => t.Name, t => t.DefaultValue);

        public Task SaveTokenAsync(string name, string value, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task ResetToDefaultsAsync(CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    [Fact]
    public void ThemePage_renders_heading_and_editor()
    {
        using var ctx = new BunitTestContext();
        ctx.Services.AddSingleton<IThemeStore>(new FakeThemeStore());
        ctx.Services.AddSingleton<IJSRuntime>(new BunitJSInterop().JSRuntime);

        var cut = ctx.RenderComponent<ThemePage>();
        cut.Markup.Should().Contain("Theme Editor");
        cut.Markup.Should().Contain("theme.json");
    }
}
