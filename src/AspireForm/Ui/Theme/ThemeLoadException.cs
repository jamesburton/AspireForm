namespace AspireForm.Ui.Theme;

/// <summary>Raised when a theme file cannot be loaded or parsed.</summary>
public sealed class ThemeLoadException : Exception
{
    /// <summary>Initialises with a message.</summary>
    public ThemeLoadException(string message) : base(message) { }

    /// <summary>Initialises with a message and inner exception.</summary>
    public ThemeLoadException(string message, Exception inner) : base(message, inner) { }
}
