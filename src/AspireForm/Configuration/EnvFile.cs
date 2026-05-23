namespace AspireForm.Configuration;

/// <summary>Reads <c>.env</c>-style files into a dictionary of environment values.</summary>
public static class EnvFile
{
    /// <summary>Parses <c>.env</c> text. Lines without <c>=</c>, blank lines, and <c>#</c> comments are ignored. Surrounding quotes on values are stripped.</summary>
    public static IReadOnlyDictionary<string, string> Parse(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();

            if ((value.StartsWith('"') && value.EndsWith('"') && value.Length >= 2)
                || (value.StartsWith('\'') && value.EndsWith('\'') && value.Length >= 2))
            {
                value = value[1..^1];
            }

            result[key] = value;
        }

        return result;
    }

    /// <summary>Reads and parses a <c>.env</c> file if it exists; returns an empty map when it does not.</summary>
    public static IReadOnlyDictionary<string, string> Load(string path) =>
        File.Exists(path) ? Parse(File.ReadAllText(path)) : new Dictionary<string, string>(StringComparer.Ordinal);
}
