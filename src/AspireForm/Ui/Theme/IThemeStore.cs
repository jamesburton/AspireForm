namespace AspireForm.Ui.Theme;

/// <summary>Reads and writes the active theme token values for the current AspireForm project.</summary>
public interface IThemeStore
{
    /// <summary>Returns the merged token map: default values overridden by any persisted values.
    /// Keys are token names (e.g., <c>"color-primary"</c>); values are hex strings.</summary>
    IReadOnlyDictionary<string, string> GetTokens();

    /// <summary>Persists a single token override to <c>.aspireform/theme.json</c>.</summary>
    /// <param name="name">Token name (must match a name in <see cref="ThemeDefaults.Tokens"/>).</param>
    /// <param name="value">Hex color value (e.g., <c>"#1a73e8"</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    Task SaveTokenAsync(string name, string value, CancellationToken ct = default);

    /// <summary>Deletes all persisted overrides, restoring all tokens to their defaults.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task ResetToDefaultsAsync(CancellationToken ct = default);
}
