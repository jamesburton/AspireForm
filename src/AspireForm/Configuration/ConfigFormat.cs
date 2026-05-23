namespace AspireForm.Configuration;

/// <summary>The on-disk format of a configuration file.</summary>
public enum ConfigFormat
{
    /// <summary>YAML (<c>.yaml</c> / <c>.yml</c>).</summary>
    Yaml,

    /// <summary>JSON with comments (<c>.jsonc</c> / <c>.json</c>).</summary>
    Jsonc,
}

/// <summary>Maps file extensions to <see cref="ConfigFormat"/>.</summary>
public static class ConfigFormatDetector
{
    /// <summary>Determines the format from a file path's extension, or null when unrecognized.</summary>
    public static ConfigFormat? FromPath(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".yaml" or ".yml" => ConfigFormat.Yaml,
            ".jsonc" or ".json" => ConfigFormat.Jsonc,
            _ => null,
        };
}
