using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Ui;

/// <summary>
/// File-level regression checks for the AspireForm CSS bundle (wwwroot/app.css).
/// Faster than spinning up Kestrel and isolates "the source contains the right rules"
/// from "the static-assets pipeline serves them correctly".
/// </summary>
public sealed class CssBundleTests
{
    private static string AppCssPath()
    {
        // Walk up from the test bin folder until we find the source repo root, then resolve
        // the wwwroot path relative to that. This avoids depending on the absolute path.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AspireForm.slnx")))
            dir = dir.Parent;
        dir.Should().NotBeNull("the test must be runnable from anywhere inside the repo");
        return Path.Combine(dir!.FullName, "src", "AspireForm", "wwwroot", "app.css");
    }

    [Fact]
    public void AppCss_exists_at_conventional_web_sdk_location()
    {
        // Regression for the wwwroot-misplaced bug: app.css must live at the Web SDK's
        // canonical location (src/AspireForm/wwwroot/) so the static-assets manifest
        // picks it up automatically. The pre-bug location (Ui/wwwroot/) is NOT served.
        File.Exists(AppCssPath()).Should().BeTrue();
    }

    [Theory]
    [InlineData(".w-56", "sidebar width — without this AppSidebar has no fixed width")]
    [InlineData(".w-24", "narrow column width used by entity property editors")]
    [InlineData(".w-32", "medium column width used by relationship editors")]
    [InlineData(".h-32", "fixed height used by sticky note components")]
    [InlineData(".max-w-xs", "small panel max-width used by Theme page")]
    [InlineData(".max-w-xl", "medium panel max-width used by dialogs")]
    [InlineData(".ring-1", "1px ring used to highlight selected entities/endpoints")]
    [InlineData(".tracking-wide", "letter-spacing utility used in muted labels")]
    [InlineData(".break-all", "word-break utility used for long route paths")]
    [InlineData(".border-b-0", "border-bottom suppressor used in tab headers")]
    [InlineData(".space-y-0", "vertical-space reset used in tight lists")]
    public void AppCss_contains_tailwind_utility_missing_from_blueprint_bundle(string selector, string usage)
    {
        // Regression for the missing-utility bug: BlazorBlueprint ships a purged Tailwind
        // bundle that only includes utilities its own components reference. AspireForm uses
        // additional utilities for layout (sidebar width, content widths, etc.) which must
        // be supplied via app.css until we add a proper Tailwind build pipeline.
        var css = File.ReadAllText(AppCssPath());
        css.Should().Contain(selector, $"app.css must define {selector} ({usage})");
    }
}
