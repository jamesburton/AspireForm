namespace AspireForm.Ui.Theme;

/// <summary>The full set of design tokens for one theme (light + dark variants + radius).</summary>
/// <param name="Name">Theme display name (e.g., "Slate Blue").</param>
/// <param name="Description">Short description shown in the theme picker.</param>
/// <param name="Light">Light-mode token values keyed by token name.</param>
/// <param name="Dark">Dark-mode token values keyed by token name.</param>
/// <param name="Radius">Border radius in rem (0–1, step 0.25).</param>
public sealed record ThemeDefinition(
    string Name,
    string Description,
    IReadOnlyDictionary<string, string> Light,
    IReadOnlyDictionary<string, string> Dark,
    double Radius);

/// <summary>Summary row shown in the theme picker list.</summary>
/// <param name="Name">Theme name (also used as file key).</param>
/// <param name="Description">Short description.</param>
/// <param name="IsActive">Whether this theme is currently active.</param>
public sealed record ThemeSummary(string Name, string Description, bool IsActive);

/// <summary>Pointer to which theme is currently active and whether dark mode is on.</summary>
/// <param name="ActiveName">Name of the active theme.</param>
/// <param name="DarkMode">True if the dark token bucket is applied.</param>
public sealed record ThemeActivation(string ActiveName, bool DarkMode);

/// <summary>Known token names in display order, matching the tweakcn/shadcn vocabulary.</summary>
public static class ThemeTokenNames
{
    /// <summary>All token names in group order.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        "background", "foreground",
        "primary", "primary-foreground",
        "secondary", "secondary-foreground",
        "muted", "muted-foreground",
        "accent", "accent-foreground",
        "destructive", "destructive-foreground",
        "border", "input", "ring",
        "card", "card-foreground",
        "popover", "popover-foreground",
    ];
}
