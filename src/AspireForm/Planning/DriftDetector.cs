using System.Security.Cryptography;

namespace AspireForm.Planning;

/// <summary>Filesystem-checksum drift detection for tracked files.</summary>
public static class DriftDetector
{
    /// <summary>SHA-256 hex digest of the file at <paramref name="path"/>.</summary>
    public static string ComputeChecksum(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>SHA-256 hex digest of an in-memory string (used to checksum freshly-rendered content).</summary>
    public static string ComputeChecksum(ReadOnlySpan<char> text)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text.ToArray());
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    /// <summary>True when no file exists at <paramref name="path"/>.</summary>
    public static bool IsAbsent(string path) => !File.Exists(path);

    /// <summary>True when the file is missing or its on-disk checksum differs from <paramref name="baselineChecksum"/>.</summary>
    public static bool HasDrifted(string path, string baselineChecksum) =>
        IsAbsent(path) || !string.Equals(ComputeChecksum(path), baselineChecksum, StringComparison.Ordinal);
}
