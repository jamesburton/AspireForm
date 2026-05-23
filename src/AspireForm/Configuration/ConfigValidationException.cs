namespace AspireForm.Configuration;

/// <summary>Raised when a configuration file is malformed, invalid, or fails schema validation.</summary>
public sealed class ConfigValidationException : Exception
{
    /// <summary>Initializes the exception with a human-readable message.</summary>
    public ConfigValidationException(string message) : base(message) { }

    /// <summary>Initializes the exception with a message and an inner cause.</summary>
    public ConfigValidationException(string message, Exception inner) : base(message, inner) { }
}
