namespace AspireForm.Plugins;

/// <summary>Raised when a plugin's manifest is malformed, incompatible, or fails to load.</summary>
public sealed class PluginContractException : Exception
{
    /// <summary>Initialises the exception with a human-readable message.</summary>
    public PluginContractException(string message) : base(message) { }

    /// <summary>Initialises the exception with a message and an inner cause.</summary>
    public PluginContractException(string message, Exception inner) : base(message, inner) { }
}
