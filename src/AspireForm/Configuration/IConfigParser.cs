using System.Text.Json.Nodes;

namespace AspireForm.Configuration;

/// <summary>Parses a configuration file's text into a normalized <see cref="JsonObject"/> DOM.</summary>
public interface IConfigParser
{
    /// <summary>Parses configuration text. Throws <see cref="ConfigValidationException"/> on malformed input or a non-object root.</summary>
    JsonObject Parse(string text);
}
