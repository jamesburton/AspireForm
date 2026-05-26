using AspireForm.Ui.Theme;
using AspireForm.Ui.Components.Theme;
using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

// Alias to avoid ambiguity with xunit.v3's static Xunit.TestContext.
using BunitTestContext = Bunit.TestContext;

namespace AspireForm.Tests.Ui.Theme;

public sealed class ThemeTokenEditorTests
{
    private sealed class FakeThemeStore : IThemeStore
    {
        private readonly Dictionary<string, string> _data = [];

        public IReadOnlyDictionary<string, string> GetTokens()
        {
            var result = ThemeDefaults.Tokens.ToDictionary(t => t.Name, t => t.DefaultValue);
            foreach (var kv in _data) result[kv.Key] = kv.Value;
            return result;
        }

        public Task SaveTokenAsync(string name, string value, CancellationToken ct = default)
        {
            _data[name] = value;
            return Task.CompletedTask;
        }

        public Task ResetToDefaultsAsync(CancellationToken ct = default)
        {
            _data.Clear();
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void ThemeTokenEditor_renders_row_for_each_token()
    {
        using var ctx = new BunitTestContext();
        ctx.Services.AddSingleton<IThemeStore>(new FakeThemeStore());
        ctx.Services.AddSingleton<IJSRuntime>(new BunitJSInterop().JSRuntime);

        var cut = ctx.RenderComponent<ThemeTokenEditor>();

        // 14 token rows — check a sample of label text.
        cut.Markup.Should().Contain("Primary accent / links");
        cut.Markup.Should().Contain("Page background");
        cut.Markup.Should().Contain("Main borders");
    }

    [Fact]
    public void ThemeTokenEditor_shows_default_color_values()
    {
        using var ctx = new BunitTestContext();
        ctx.Services.AddSingleton<IThemeStore>(new FakeThemeStore());
        ctx.Services.AddSingleton<IJSRuntime>(new BunitJSInterop().JSRuntime);

        var cut = ctx.RenderComponent<ThemeTokenEditor>();
        cut.Markup.Should().Contain("#1a73e8"); // default primary color
    }
}
