namespace AspireForm.Plugins;

/// <summary>Parses <c>#:</c> directives at the top of a script plugin (.NET 10 file-based-app convention).</summary>
public static class ScriptDirectiveParser
{
    /// <summary>
    /// Returns directives parsed from the leading <c>#:</c>-prefixed lines of <paramref name="source"/>.
    /// Blank lines at the top are skipped; parsing stops at the first non-blank, non-directive line.
    /// </summary>
    public static IEnumerable<ScriptDirective> Parse(string source)
    {
        foreach (var rawLine in source.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r').Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (!line.StartsWith("#:", StringComparison.Ordinal))
            {
                yield break;
            }

            var rest = line[2..].Trim();
            if (rest.StartsWith("package ", StringComparison.OrdinalIgnoreCase))
            {
                var arg = rest["package ".Length..].Trim();
                var (id, version) = SplitIdVersion(arg);
                yield return new ScriptDirective(ScriptDirectiveKind.Package, id, version);
            }

            // Other directive kinds (#:sdk, #:property) are ignored in v1.
        }
    }

    private static (string Id, string Version) SplitIdVersion(string arg)
    {
        var at = arg.IndexOf('@');
        if (at < 0)
        {
            return (arg, "*");
        }

        var id = arg[..at].Trim();
        var version = arg[(at + 1)..].Trim();
        return (id, string.IsNullOrEmpty(version) ? "*" : version);
    }
}
