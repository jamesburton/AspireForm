using System.Text.RegularExpressions;

namespace AspireForm.Planning;

/// <summary>
/// Reads, inserts, and replaces AspireForm-owned regions in a file's text, demarcated by
/// <c>// &lt;aspireform:block=NAME&gt;</c> ... <c>// &lt;/aspireform:block=NAME&gt;</c> lines.
/// </summary>
public static class MarkerRegion
{
    /// <summary>Builds the opening marker line for a block.</summary>
    public static string OpenMarker(string blockName) => $"// <aspireform:block={blockName}>";

    /// <summary>Builds the closing marker line for a block.</summary>
    public static string CloseMarker(string blockName) => $"// </aspireform:block={blockName}>";

    private static Regex RegionRegex(string blockName) => new(
        $@"^[ \t]*{Regex.Escape(OpenMarker(blockName))}\r?\n(?<inner>.*?)(?:\r?\n)?[ \t]*{Regex.Escape(CloseMarker(blockName))}[ \t]*\r?\n?",
        RegexOptions.Multiline | RegexOptions.Singleline);

    /// <summary>
    /// Inserts or replaces the named region inside <paramref name="text"/>. If the region exists,
    /// its inner content is replaced with <paramref name="innerContent"/>. Otherwise a new region
    /// is inserted immediately before the first line containing <paramref name="anchor"/>.
    /// Throws <see cref="InvalidOperationException"/> when no existing region is present and the
    /// anchor cannot be located.
    /// </summary>
    /// <param name="text">The full file text to operate on.</param>
    /// <param name="blockName">The name of the block region to upsert.</param>
    /// <param name="innerContent">The content to place between the open and close marker lines.</param>
    /// <param name="anchor">A line fragment used to locate the insertion point when no region exists yet.</param>
    /// <returns>The updated file text.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the block region does not already exist and <paramref name="anchor"/> cannot be
    /// found in <paramref name="text"/>.
    /// </exception>
    public static string UpsertBeforeAnchor(string text, string blockName, string innerContent, string anchor)
    {
        var match = RegionRegex(blockName).Match(text);
        if (match.Success)
        {
            var newRegion = $"{OpenMarker(blockName)}\n{innerContent}\n{CloseMarker(blockName)}\n";
            return string.Concat(text.AsSpan(0, match.Index), newRegion, text.AsSpan(match.Index + match.Length));
        }

        var anchorIndex = text.IndexOf(anchor, StringComparison.Ordinal);
        if (anchorIndex < 0)
        {
            throw new InvalidOperationException(
                $"Cannot insert region '{blockName}': anchor '{anchor}' not found in file content.");
        }

        // Insert at the start of the anchor's line, with a trailing blank line for readability.
        var lineStart = text.LastIndexOf('\n', Math.Max(0, anchorIndex - 1)) + 1;
        var newRegionWithGap = $"{OpenMarker(blockName)}\n{innerContent}\n{CloseMarker(blockName)}\n\n";
        return string.Concat(text.AsSpan(0, lineStart), newRegionWithGap, text.AsSpan(lineStart));
    }

    /// <summary>Removes the named region if present; otherwise returns <paramref name="text"/> unchanged.</summary>
    /// <param name="text">The full file text to operate on.</param>
    /// <param name="blockName">The name of the block region to remove.</param>
    /// <returns>The updated file text with the region stripped, or the original text if the region was absent.</returns>
    public static string Remove(string text, string blockName) =>
        RegionRegex(blockName).Replace(text, string.Empty);

    /// <summary>Extracts the inner content of an existing region; returns false when the region is absent.</summary>
    /// <param name="text">The full file text to search.</param>
    /// <param name="blockName">The name of the block region to read.</param>
    /// <param name="innerContent">
    /// When the method returns <see langword="true"/>, contains the text between the open and close
    /// marker lines. Set to <see cref="string.Empty"/> when the region is absent.
    /// </param>
    /// <returns><see langword="true"/> when the region was found; otherwise <see langword="false"/>.</returns>
    public static bool TryReadInner(string text, string blockName, out string innerContent)
    {
        var match = RegionRegex(blockName).Match(text);
        if (!match.Success)
        {
            innerContent = string.Empty;
            return false;
        }

        innerContent = match.Groups["inner"].Value;
        return true;
    }
}
