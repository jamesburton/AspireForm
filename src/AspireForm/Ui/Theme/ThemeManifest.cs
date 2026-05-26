using System.Text.Json.Serialization;

namespace AspireForm.Ui.Theme;

/// <summary>Persisted active-theme pointer. Written to <c>.aspireform/themes/_active.json</c>.</summary>
public sealed class ThemeManifest
{
    /// <summary>Name of the currently active theme.</summary>
    [JsonPropertyName("active")]
    public string Active { get; set; } = "AspireForm Light";

    /// <summary>Whether dark mode is currently enabled.</summary>
    [JsonPropertyName("darkMode")]
    public bool DarkMode { get; set; }
}
