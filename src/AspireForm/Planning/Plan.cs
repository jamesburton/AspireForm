using AspireForm.Providers;

namespace AspireForm.Planning;

/// <summary>The action the planner intends to take against one block.</summary>
public enum BlockActionKind
{
    /// <summary>Block is in config but not in state; create it.</summary>
    Create,

    /// <summary>Block is in both config and state; inputs (or files) changed.</summary>
    Update,

    /// <summary>Block is in state but not in config; remove it.</summary>
    Delete,

    /// <summary>Block matches state and disk exactly; nothing to do.</summary>
    Noop,
}

/// <summary>The action the planner intends to take against one file inside a block.</summary>
public enum FileActionKind
{
    /// <summary>File does not exist on disk; will be written.</summary>
    Create,

    /// <summary>File exists; will be updated (Managed region replaced, full re-render, or merge).</summary>
    Modify,

    /// <summary>File exists; tool will not touch it (Scaffold mode + file already present).</summary>
    Skip,

    /// <summary>File previously tracked; will be removed (block delete).</summary>
    Remove,

    /// <summary>Drift requires human attention before apply can proceed.</summary>
    DriftBlocked,
}

/// <summary>One file's planned action.</summary>
/// <param name="Path">Repo-relative file path.</param>
/// <param name="OwnershipMode">The file's ownership mode.</param>
/// <param name="BlockMarker">Marker name (for Managed regions).</param>
/// <param name="Kind">The action that will be taken.</param>
/// <param name="DriftDetected">True when the file's on-disk checksum has diverged from the state baseline.</param>
/// <param name="BeforeContent">Current on-disk content (or null when the file is absent).</param>
/// <param name="AfterContent">Content that would be written (or null when the action is Skip / Remove).</param>
public sealed record FileActionPlan(
    string Path,
    OwnershipMode OwnershipMode,
    string BlockMarker,
    FileActionKind Kind,
    bool DriftDetected,
    string? BeforeContent,
    string? AfterContent);

/// <summary>One block's planned action.</summary>
public sealed record BlockAction(
    string BlockName,
    BlockKind BlockKind,
    BlockActionKind Kind,
    IReadOnlyList<FileActionPlan> FileActions)
{
    /// <summary>CLI invocations planned for this block (from the provider).</summary>
    public IReadOnlyList<PlannedCliAction> CliActions { get; init; } = [];
}

/// <summary>An ordered list of block actions — the full reconciliation plan.</summary>
public sealed class Plan
{
    /// <summary>Block actions in topological order.</summary>
    public IReadOnlyList<BlockAction> Blocks { get; init; } = [];

    /// <summary>True when any block action would actually change something.</summary>
    public bool HasChanges => Blocks.Any(b => b.Kind != BlockActionKind.Noop);
}
