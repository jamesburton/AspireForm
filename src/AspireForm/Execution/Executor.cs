using AspireForm.Aspire;
using AspireForm.Configuration;
using AspireForm.Planning;
using AspireForm.Providers;
using AspireForm.State;

namespace AspireForm.Execution;

/// <summary>
/// Executes a <see cref="Plan"/>: runs <see cref="PlannedCliAction"/>s via <see cref="IAspireCli"/>,
/// applies each <see cref="FileActionPlan"/> by writing/removing files, and persists the resulting
/// <see cref="AspireFormState"/> to disk after each successful block.
/// </summary>
public sealed class Executor
{
    private readonly IAspireCli _aspireCli;
    private readonly StateStore _stateStore;

    /// <summary>Initialises the executor with its CLI seam and state store.</summary>
    public Executor(IAspireCli aspireCli, StateStore stateStore)
    {
        _aspireCli = aspireCli;
        _stateStore = stateStore;
    }

    /// <summary>Applies <paramref name="plan"/> against <paramref name="projectDir"/>, persisting state per block.</summary>
    public async Task<ExecutionResult> ApplyAsync(
        Plan plan,
        ProjectModel model,
        AspireFormState prevState,
        string projectDir,
        ExecuteOptions options,
        CancellationToken cancellationToken = default)
    {
        // Clone first — NewState in any return path is the executor's owned copy, never aliasing the caller's input.
        var state = CloneState(prevState);

        // Drift gate: refuse if any file has drifted and ForceDrift is not set.
        if (!options.ForceDrift)
        {
            var drifted = plan.Blocks.SelectMany(b => b.FileActions).Where(f => f.DriftDetected).ToList();
            if (drifted.Count > 0)
            {
                var paths = string.Join(", ", drifted.Select(f => f.Path));
                return new ExecutionResult
                {
                    Success = false,
                    FailureMessage = $"Refusing to apply: drift detected on {drifted.Count} file(s): {paths}. Re-run with --force-drift to override.",
                    NewState = state,
                };
            }
        }

        var blocksApplied = 0;

        foreach (var block in plan.Blocks)
        {
            try
            {
                await ApplyBlockAsync(block, model, projectDir, state, cancellationToken);

                // Persist after each successful block so partial progress survives later failures.
                _stateStore.Save(projectDir, state);
                blocksApplied++;
            }
            catch (Exception ex)
            {
                return new ExecutionResult
                {
                    Success = false,
                    FailureMessage = $"Block '{block.BlockName}' failed: {ex.Message}",
                    BlocksApplied = blocksApplied,
                    BlocksFailed = 1,
                    NewState = state,
                };
            }
        }

        return new ExecutionResult
        {
            Success = true,
            BlocksApplied = blocksApplied,
            NewState = state,
        };
    }

    private async Task ApplyBlockAsync(
        BlockAction block,
        ProjectModel model,
        string projectDir,
        AspireFormState state,
        CancellationToken cancellationToken)
    {
        // CLI actions first (e.g. aspire add must run before file edits that assume the package is referenced).
        var appHostWorkingDir = Path.GetFullPath(Path.Combine(projectDir, model.AspireForm.AppHost));
        foreach (var cli in block.CliActions)
        {
            if (!string.Equals(cli.Tool, "aspire", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Unsupported CLI tool '{cli.Tool}' in v1 (only 'aspire' is wired through IAspireCli).");
            }

            var result = await _aspireCli.RunAsync(cli.Args, appHostWorkingDir, cancellationToken);
            if (result.ExitCode != 0)
            {
                var details = string.IsNullOrWhiteSpace(result.StandardError)
                    ? result.StandardOutput : result.StandardError;
                throw new InvalidOperationException(
                    $"aspire {string.Join(' ', cli.Args)} exited with {result.ExitCode}: {details}");
            }
        }

        // File actions.
        var blockFiles = new Dictionary<string, FileState>(StringComparer.Ordinal);
        foreach (var file in block.FileActions)
        {
            ApplyFileAction(file, projectDir, blockFiles);
        }

        // Update state for this block.
        if (block.Kind == BlockActionKind.Delete)
        {
            state.Blocks.Remove(block.BlockName);
        }
        else
        {
            var blockType = LookupBlockType(model, block.BlockName);
            state.Blocks[block.BlockName] = new BlockState
            {
                Type = blockType,
                Kind = block.BlockKind == BlockKind.Module ? "module" : "resource",
                Files = blockFiles,
                Inputs = LookupBlockInputs(model, block.BlockName),
            };
        }
    }

    private static void ApplyFileAction(FileActionPlan file, string projectDir, Dictionary<string, FileState> blockFiles)
    {
        switch (file.Kind)
        {
            case FileActionKind.Create:
            case FileActionKind.Modify:
            {
                if (file.AfterContent is null)
                {
                    throw new InvalidOperationException(
                        $"File action {file.Kind} on '{file.Path}' has no AfterContent.");
                }

                var dir = Path.GetDirectoryName(file.Path);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(file.Path, file.AfterContent);

                /* Store the key as a repo-relative forward-slash path for cross-platform portability. */
                blockFiles[PathUtilities.ToRepoRelative(file.Path, projectDir)] = new FileState
                {
                    OwnershipMode = file.OwnershipMode.ToString().ToLowerInvariant(),
                    Checksum = DriftDetector.ComputeChecksum(file.AfterContent.AsSpan()),
                    Baseline = file.OwnershipMode == OwnershipMode.Merge ? file.AfterContent : null,
                };
                break;
            }

            case FileActionKind.Skip:
            {
                // Scaffold mode + file already present: keep state entry pointing at the existing file's checksum.
                if (File.Exists(file.Path))
                {
                    blockFiles[PathUtilities.ToRepoRelative(file.Path, projectDir)] = new FileState
                    {
                        OwnershipMode = file.OwnershipMode.ToString().ToLowerInvariant(),
                        Checksum = DriftDetector.ComputeChecksum(file.Path),
                    };
                }
                break;
            }

            case FileActionKind.Remove:
            {
                if (File.Exists(file.Path))
                {
                    File.Delete(file.Path);
                }
                // Don't add to blockFiles — the block-level state entry will be removed for Delete blocks.
                break;
            }

            case FileActionKind.DriftBlocked:
                throw new InvalidOperationException(
                    $"Refusing to apply '{file.Path}': drift detected. Re-run with --force-drift to override.");
        }
    }

    private static string LookupBlockType(ProjectModel model, string blockName)
    {
        if (model.Resources.TryGetValue(blockName, out var r)) return r.Type;
        if (model.Modules.TryGetValue(blockName, out var m)) return m.Type;
        // Unreachable: caller only invokes this for blocks present in the desired model (Create/Update).
        // Failing loud here protects future refactors from silently writing corrupt state records.
        throw new InvalidOperationException($"Block '{blockName}' is not declared in the project model.");
    }

    private static System.Text.Json.Nodes.JsonObject LookupBlockInputs(ProjectModel model, string blockName)
    {
        if (model.Resources.TryGetValue(blockName, out var r)) return r.Inputs;
        if (model.Modules.TryGetValue(blockName, out var m)) return m.Inputs;
        return new System.Text.Json.Nodes.JsonObject();
    }

    private static AspireFormState CloneState(AspireFormState prev)
    {
        // Round-trip through STJ to deep-clone cleanly (including JsonObject Inputs).
        var json = System.Text.Json.JsonSerializer.Serialize(prev);
        return System.Text.Json.JsonSerializer.Deserialize<AspireFormState>(json) ?? new AspireFormState();
    }
}
