using AspireForm.Ui.Theme;
using AspireForm.Ui.Components.Theme;
using AwesomeAssertions;
using BlazorBlueprint.Components;
using Bunit;
using Xunit;

// Alias to avoid ambiguity with xunit.v3's static Xunit.TestContext.
using BunitTestContext = Bunit.TestContext;

namespace AspireForm.Tests.Ui.Theme;

public sealed class TokenBucketEditorTests
{
    private static Dictionary<string, string> MakeTokens() =>
        ThemeTokenNames.All.ToDictionary(t => t, _ => "0 0% 100%");

    [Fact]
    public void TokenBucketEditor_renders_row_for_each_token()
    {
        using var ctx = new BunitTestContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.JSInterop.SetupModule("./_content/BlazorBlueprint.Components/js/text-input.js");
        ctx.Services.AddBlazorBlueprintComponents();
        var tokens = MakeTokens();
        bool saved = false;

        var cut = ctx.RenderComponent<TokenBucketEditor>(p => p
            .Add(c => c.Tokens, tokens)
            .Add(c => c.OnSave, () => { saved = true; return Task.CompletedTask; }));

        // Each token name should appear as a CSS var label.
        cut.Markup.Should().Contain("--background");
        cut.Markup.Should().Contain("--primary");
        cut.Markup.Should().Contain("--foreground");
    }

    [Fact]
    public void TokenBucketEditor_shows_save_button()
    {
        using var ctx = new BunitTestContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.JSInterop.SetupModule("./_content/BlazorBlueprint.Components/js/text-input.js");
        ctx.Services.AddBlazorBlueprintComponents();
        var tokens = MakeTokens();
        var cut = ctx.RenderComponent<TokenBucketEditor>(p => p
            .Add(c => c.Tokens, tokens)
            .Add(c => c.OnSave, () => Task.CompletedTask));

        cut.Markup.Should().Contain("Save tokens");
    }

    [Fact]
    public async Task TokenBucketEditor_calls_OnSave_when_button_clicked()
    {
        using var ctx = new BunitTestContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.JSInterop.SetupModule("./_content/BlazorBlueprint.Components/js/text-input.js");
        ctx.Services.AddBlazorBlueprintComponents();
        var tokens = MakeTokens();
        var saveCount = 0;

        var cut = ctx.RenderComponent<TokenBucketEditor>(p => p
            .Add(c => c.Tokens, tokens)
            .Add(c => c.OnSave, () => { saveCount++; return Task.CompletedTask; }));

        var saveBtn = cut.Find("button");
        await cut.InvokeAsync(() => saveBtn.Click());

        saveCount.Should().Be(1);
    }
}
