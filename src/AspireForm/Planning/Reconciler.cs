using AspireForm.Providers;
using AspireForm.State;

namespace AspireForm.Planning;

/// <summary>The per-block result of reconciliation: ordered file actions + CLI invocations.</summary>
/// <param name="FileActions">Resolved per-file actions.</param>
/// <param name="CliActions">CLI invocations the provider wanted to perform.</param>
public sealed record BlockReconcileResult(
    IReadOnlyList<FileActionPlan> FileActions,
    IReadOnlyList<PlannedCliAction> CliActions);

/// <summary>
/// Three-way reconciler: combines a provider's <see cref="ProviderPlan"/> with the prior
/// <see cref="BlockState"/> and on-disk filesystem state, producing the resolved
/// <see cref="FileActionPlan"/> list for one block.
/// </summary>
public sealed class Reconciler
{
    /// <summary>
    /// Reconciles one block. Pure with respect to its inputs except that it reads files from
    /// <paramref name="projectDir"/>.
    /// </summary>
    /// <param name="blockName">The block's name in the config (e.g. <c>sql</c>).</param>
    /// <param name="blockKind">Whether this is a Resource or Module block.</param>
    /// <param name="blockKindAction">The high-level action determined by the planner.</param>
    /// <param name="providerPlan">The provider's declared file and CLI intents.</param>
    /// <param name="previousState">The block's last-known state, or <see langword="null"/> for new blocks.</param>
    /// <param name="projectDir">The project root used to resolve on-disk content.</param>
    /// <returns>A <see cref="BlockReconcileResult"/> containing resolved file and CLI actions.</returns>
    public BlockReconcileResult Reconcile(
        string blockName,
        BlockKind blockKind,
        BlockActionKind blockKindAction,
        ProviderPlan providerPlan,
        BlockState? previousState,
        string projectDir)
    {
        /* blockKind is reserved for future policy differences between Resource and Module blocks. */
        _ = blockKind;

        if (blockKindAction == BlockActionKind.Noop)
        {
            return new BlockReconcileResult([], []);
        }

        if (blockKindAction == BlockActionKind.Delete)
        {
            return new BlockReconcileResult(BuildRemoveActions(previousState, projectDir), []);
        }

        // CREATE or UPDATE: walk the provider's file actions and resolve each one.
        var resolved = new List<FileActionPlan>(providerPlan.FileActions.Count);
        foreach (var planned in providerPlan.FileActions)
        {
            resolved.Add(ResolveFileAction(planned, blockName, previousState, projectDir));
        }

        return new BlockReconcileResult(resolved, providerPlan.CliActions);
    }

    /// <summary>Resolves a relative or rooted path against <paramref name="projectDir"/>.</summary>
    private static string ResolvePath(string path, string projectDir) =>
        Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(projectDir, path));

    // Candidate AppHost anchors tried in order when inserting a managed region into an AppHost file.
    private static readonly string[] AppHostAnchors =
    [
        "builder.Build().Run();",
        "await builder.Build().RunAsync();",
        "builder.Build().RunAsync();",
    ];

    private static FileActionPlan ResolveFileAction(
        PlannedFileAction planned, string blockName, BlockState? previousState, string projectDir)
    {
        // Resolve relative paths against the project directory so drift detection works regardless
        // of the process working directory when aspireform plan is invoked.
        var path = ResolvePath(planned.Path, projectDir);
        var exists = File.Exists(path);
        var beforeContent = exists ? File.ReadAllText(path) : null;

        // State may have been written with either the resolved or relative key; try both.
        var previousFile = previousState?.Files.GetValueOrDefault(path)
            ?? previousState?.Files.GetValueOrDefault(planned.Path);

        /* Drift is only meaningful when a prior state record exists for this file. */
        var driftDetected = previousFile is not null && exists
            && !string.Equals(DriftDetector.ComputeChecksum(path), previousFile.Checksum, StringComparison.Ordinal);

        switch (planned.OwnershipMode)
        {
            case OwnershipMode.Scaffold:
                /* Scaffold files are written once. If the file already exists the developer owns it. */
                return exists
                    ? new FileActionPlan(path, planned.OwnershipMode, planned.BlockMarker,
                        Kind: FileActionKind.Skip, DriftDetected: driftDetected,
                        BeforeContent: beforeContent, AfterContent: null)
                    : new FileActionPlan(path, planned.OwnershipMode, planned.BlockMarker,
                        Kind: FileActionKind.Create, DriftDetected: false,
                        BeforeContent: null, AfterContent: planned.RenderContent());

            case OwnershipMode.Managed:
            {
                var inner = planned.RenderContent();

                if (exists)
                {
                    /* Upsert the marker region inside the existing file using whichever anchor is present. */
                    var after = MarkerRegion.UpsertBeforeAnchor(beforeContent!, blockName, inner,
                        anchors: AppHostAnchors);
                    return new FileActionPlan(path, planned.OwnershipMode, planned.BlockMarker,
                        Kind: FileActionKind.Modify, DriftDetected: driftDetected,
                        BeforeContent: beforeContent, AfterContent: after);
                }
                else
                {
                    /* File doesn't exist yet — emit only the marker region; don't synthesise a fake host. */
                    var region = $"{MarkerRegion.OpenMarker(blockName)}\n{inner}\n{MarkerRegion.CloseMarker(blockName)}\n";
                    return new FileActionPlan(path, planned.OwnershipMode, planned.BlockMarker,
                        Kind: FileActionKind.Create, DriftDetected: false,
                        BeforeContent: null, AfterContent: region);
                }
            }

            case OwnershipMode.Merge:
                /* Merge mode is planned but not implemented in this phase; fall through to Managed. */
                goto case OwnershipMode.Managed;

            default:
                throw new InvalidOperationException($"Unknown ownership mode: {planned.OwnershipMode}.");
        }
    }

    private static IReadOnlyList<FileActionPlan> BuildRemoveActions(BlockState? previousState, string projectDir)
    {
        if (previousState is null)
        {
            return [];
        }

        var removals = new List<FileActionPlan>(previousState.Files.Count);
        foreach (var (relativePath, fileState) in previousState.Files)
        {
            // Resolve the stored path (which may be relative or absolute) to an absolute path.
            var path = ResolvePath(relativePath, projectDir);

            var mode = Enum.TryParse<OwnershipMode>(fileState.OwnershipMode, ignoreCase: true, out var parsed)
                ? parsed : OwnershipMode.Managed;

            // Scaffold files are developer-owned: deleting them on block-delete is surprising.
            var kind = mode == OwnershipMode.Scaffold ? FileActionKind.Skip : FileActionKind.Remove;

            removals.Add(new FileActionPlan(
                Path: path, OwnershipMode: mode, BlockMarker: string.Empty,
                Kind: kind, DriftDetected: false,
                BeforeContent: File.Exists(path) ? File.ReadAllText(path) : null,
                AfterContent: null));
        }

        return removals;
    }
}
