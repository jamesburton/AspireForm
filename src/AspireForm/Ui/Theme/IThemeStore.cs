namespace AspireForm.Ui.Theme;

/// <summary>Multi-theme store for AspireForm. Persists themes under <c>.aspireform/themes/</c> in the project directory.</summary>
public interface IThemeStore
{
    /// <summary>Returns summaries of all available themes, with <see cref="ThemeSummary.IsActive"/> set correctly.</summary>
    Task<IReadOnlyList<ThemeSummary>> ListAsync(CancellationToken ct = default);

    /// <summary>Returns the full <see cref="ThemeDefinition"/> for the named theme.</summary>
    /// <param name="name">Theme name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ThemeLoadException">If the theme file is missing or malformed.</exception>
    Task<ThemeDefinition> GetAsync(string name, CancellationToken ct = default);

    /// <summary>Saves (upserts) a theme. Creates or overwrites the file named for <paramref name="theme"/>.</summary>
    Task SaveAsync(ThemeDefinition theme, CancellationToken ct = default);

    /// <summary>Deletes the named theme. If it was active, activates the first available theme.</summary>
    Task DeleteAsync(string name, CancellationToken ct = default);

    /// <summary>Duplicates a theme under a new name. Returns the new name.</summary>
    Task<string> DuplicateAsync(string sourceName, string newName, CancellationToken ct = default);

    /// <summary>Renames a theme (renames the backing file and updates the active pointer if needed).</summary>
    Task RenameAsync(string oldName, string newName, CancellationToken ct = default);

    /// <summary>Returns the current active-theme name and dark-mode flag.</summary>
    Task<ThemeActivation> GetActiveAsync(CancellationToken ct = default);

    /// <summary>Sets the active theme by name. Throws <see cref="ThemeLoadException"/> if the theme doesn't exist.</summary>
    Task SetActiveAsync(string name, CancellationToken ct = default);

    /// <summary>Toggles dark mode (does not change the active theme).</summary>
    Task SetDarkModeAsync(bool dark, CancellationToken ct = default);

    /// <summary>Resets all themes to the built-in defaults. Overwrites all existing theme files.</summary>
    Task ResetToDefaultsAsync(CancellationToken ct = default);
}
