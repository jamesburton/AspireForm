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
        _ = projectDir;

        if (blockKindAction == BlockActionKind.Noop)
        {
            return new BlockReconcileResult([], []);
        }

        if (blockKindAction == BlockActionKind.Delete)
        {
            return new BlockReconcileResult(BuildRemoveActions(previousState), []);
        }

        // CREATE or UPDATE: walk the provider's file actions and resolve each one.
        var resolved = new List<FileActionPlan>(providerPlan.FileActions.Count);
        foreach (var planned in providerPlan.FileActions)
        {
            resolved.Add(ResolveFileAction(planned, blockName, previousState));
        }

        return new BlockReconcileResult(resolved, providerPlan.CliActions);
    }

    private static FileActionPlan ResolveFileAction(
        PlannedFileAction planned, string blockName, BlockState? previousState)
    {
        var path = planned.Path;
        var exists = File.Exists(path);
        var beforeContent = exists ? File.ReadAllText(path) : null;
        var previousFile = previousState?.Files.GetValueOrDefault(path);

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
                string after;

                if (exists)
                {
                    /* Upsert the marker region inside the existing file. */
                    after = MarkerRegion.UpsertBeforeAnchor(beforeContent!, blockName, inner,
                        anchor: "builder.Build().Run();");
                }
                else
                {
                    /* No file yet — synthesise a minimal AppHost scaffold to host the region. */
                    const string hostScaffold =
                        "var builder = DistributedApplication.CreateBuilder(args);\n\nbuilder.Build().Run();\n";
                    after = MarkerRegion.UpsertBeforeAnchor(hostScaffold, blockName, inner,
                        anchor: "builder.Build().Run();");
                }

                return new FileActionPlan(path, planned.OwnershipMode, planned.BlockMarker,
                    Kind: exists ? FileActionKind.Modify : FileActionKind.Create,
                    DriftDetected: driftDetected,
                    BeforeContent: beforeContent, AfterContent: after);
            }

            case OwnershipMode.Merge:
                /* Merge mode is planned but not implemented in this phase; fall through to Managed. */
                goto case OwnershipMode.Managed;

            default:
                throw new InvalidOperationException($"Unknown ownership mode: {planned.OwnershipMode}.");
        }
    }

    private static IReadOnlyList<FileActionPlan> BuildRemoveActions(BlockState? previousState)
    {
        if (previousState is null)
        {
            return [];
        }

        var removals = new List<FileActionPlan>(previousState.Files.Count);
        foreach (var (path, fileState) in previousState.Files)
        {
            var mode = Enum.TryParse<OwnershipMode>(fileState.OwnershipMode, ignoreCase: true, out var parsed)
                ? parsed : OwnershipMode.Managed;

            removals.Add(new FileActionPlan(
                Path: path, OwnershipMode: mode, BlockMarker: string.Empty,
                Kind: FileActionKind.Remove, DriftDetected: false,
                BeforeContent: File.Exists(path) ? File.ReadAllText(path) : null,
                AfterContent: null));
        }

        return removals;
    }
}
