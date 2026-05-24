namespace AspireForm.Execution;

/// <summary>Path conversion helpers used by the executor for state portability.</summary>
public static class PathUtilities
{
    /// <summary>
    /// Returns a forward-slash-normalised path relative to <paramref name="projectDir"/>.
    /// When the path lies outside <paramref name="projectDir"/>, returns the absolute path
    /// with forward-slash normalisation (state keys remain unique and recoverable).
    /// </summary>
    public static string ToRepoRelative(string absolutePath, string projectDir)
    {
        var normalisedProject = Path.GetFullPath(projectDir).TrimEnd(Path.DirectorySeparatorChar);
        var normalisedPath = Path.GetFullPath(absolutePath);

        var relative = Path.GetRelativePath(normalisedProject, normalisedPath);

        // If the relative path escapes the project directory, fall back to the absolute form.
        if (relative.StartsWith("..", StringComparison.Ordinal))
        {
            return normalisedPath.Replace('\\', '/');
        }

        return relative.Replace('\\', '/');
    }

    /// <summary>
    /// Inverse of <see cref="ToRepoRelative"/>. Combines <paramref name="repoRelative"/> with
    /// <paramref name="projectDir"/> and returns an absolute path; already-absolute inputs pass through.
    /// </summary>
    public static string FromRepoRelative(string repoRelative, string projectDir) =>
        Path.IsPathRooted(repoRelative)
            ? Path.GetFullPath(repoRelative)
            : Path.GetFullPath(Path.Combine(projectDir, repoRelative));
}
