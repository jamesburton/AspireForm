namespace AspireForm.Ui.Theme;

/// <summary>The complete set of CSS design tokens defined for AspireForm v1.</summary>
public static class ThemeDefaults
{
    /// <summary>All 14 tokens in display order.</summary>
    public static readonly IReadOnlyList<ThemeToken> Tokens =
    [
        new("color-primary",       "--af-color-primary",       "#1a73e8", "Primary accent / links"),
        new("color-primary-light", "--af-color-primary-light", "#e8f0fe", "Selected item highlight"),
        new("color-text",          "--af-color-text",          "#222222", "Default body text"),
        new("color-text-muted",    "--af-color-text-muted",    "#888888", "De-emphasised text"),
        new("color-text-sub",      "--af-color-text-sub",      "#666666", "Topbar sub-label"),
        new("color-bg",            "--af-color-bg",            "#ffffff", "Page background"),
        new("color-bg-surface",    "--af-color-bg-surface",    "#fafafa", "Topbar / tab-bar background"),
        new("color-bg-sidebar",    "--af-color-bg-sidebar",    "#fcfcfc", "Sidebar background"),
        new("color-bg-hover",      "--af-color-bg-hover",      "#f4f4f4", "Hover state background"),
        new("color-border",        "--af-color-border",        "#dddddd", "Main borders"),
        new("color-border-light",  "--af-color-border-light",  "#eeeeee", "Lighter borders"),
        new("color-danger-bg",     "--af-color-danger-bg",     "#ffeeee", "Danger button background"),
        new("color-danger-text",   "--af-color-danger-text",   "#aa0000", "Danger button text"),
        new("color-banner-bg",     "--af-color-banner-bg",     "#fff3cd", "Warning banner background"),
    ];
}
