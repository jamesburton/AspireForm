namespace AspireForm.Diagnostics;

/// <summary>The outcome of a single prerequisite check.</summary>
/// <param name="Name">The check's display name.</param>
/// <param name="Ok">True when the prerequisite is satisfied.</param>
/// <param name="Detail">A human-readable detail line (e.g. the detected version).</param>
/// <param name="Remedy">Guidance for fixing a failed check; null when <paramref name="Ok"/> is true.</param>
public sealed record PrerequisiteCheck(string Name, bool Ok, string Detail, string? Remedy);

/// <summary>The aggregate result of all prerequisite checks.</summary>
public sealed class PrerequisiteReport
{
    /// <summary>The individual check results.</summary>
    public required IReadOnlyList<PrerequisiteCheck> Checks { get; init; }

    /// <summary>True when every check passed.</summary>
    public bool AllPassed => Checks.All(c => c.Ok);
}
