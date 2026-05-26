namespace AspireForm.Ui.Theme;

/// <summary>A single CSS design token managed by the theme editor.</summary>
/// <param name="Name">The token name used as a key in <c>theme.json</c> (e.g., <c>"color-primary"</c>).</param>
/// <param name="CssVar">The CSS custom property name (e.g., <c>"--af-color-primary"</c>).</param>
/// <param name="DefaultValue">The fallback hex color value when no override is stored.</param>
/// <param name="Label">Human-readable display label shown in the editor.</param>
public sealed record ThemeToken(string Name, string CssVar, string DefaultValue, string Label);
