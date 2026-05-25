using AspireForm.Configuration;
using AspireForm.Providers;
using AspireForm.State;

namespace AspireForm.Planning;

/// <summary>The public planner. Builds a <see cref="Plan"/> from a <see cref="ProjectModel"/>, a <see cref="AspireFormState"/>, and the project directory.</summary>
public sealed class Planner
{
    private readonly ProviderRegistry _providers;
    private readonly Reconciler _reconciler = new();

    /// <summary>Initialises the planner with an explicit provider registry. Use <see cref="ProviderRegistry.Default"/> for the v1 built-ins.</summary>
    public Planner(ProviderRegistry providers) => _providers = providers;

    /// <summary>Builds a <see cref="Plan"/>. Reads files under <paramref name="projectDir"/> but writes nothing.</summary>
    public Plan Plan(ProjectModel model, AspireFormState state, string projectDir)
    {
        // Block-level diff
        var desired = model.Resources.Keys.Concat(model.Modules.Keys).ToHashSet(StringComparer.Ordinal);
        var stateBlocks = state.Blocks.Keys.ToHashSet(StringComparer.Ordinal);

        var creates = desired.Except(stateBlocks).ToHashSet(StringComparer.Ordinal);
        var updates = desired.Intersect(stateBlocks).ToHashSet(StringComparer.Ordinal);
        var deletes = stateBlocks.Except(desired).ToHashSet(StringComparer.Ordinal);

        // Build the dependency graph for desired blocks (deletes are appended unordered to the end).
        var edges = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var r in model.Resources.Values)
        {
            edges[r.Name] = [];
        }

        foreach (var m in model.Modules.Values)
        {
            edges[m.Name] = m.DependsOn;
        }

        var order = DependencyGraph.TopologicallySort(edges);

        var blocks = new List<BlockAction>();
        foreach (var name in order)
        {
            var (kind, providerType, inputs) = LookupDesired(model, name);
            var provider = _providers.Get(providerType);
            var ctx = new PlanContext(
                BlockName: name,
                Inputs: inputs,
                AppHostDirectory: Path.IsPathRooted(model.AspireForm.AppHost)
                    ? model.AspireForm.AppHost
                    : Path.GetFullPath(Path.Combine(projectDir, model.AspireForm.AppHost)),
                ProjectName: model.AspireForm.Project);

            var providerPlan = provider.Plan(ctx);
            var blockActionKind = creates.Contains(name) ? BlockActionKind.Create
                : updates.Contains(name) ? BlockActionKind.Update
                : BlockActionKind.Noop;

            var previousState = state.Blocks.GetValueOrDefault(name);
            var result = _reconciler.Reconcile(name, kind, blockActionKind, providerPlan, previousState, projectDir);

            blocks.Add(new BlockAction(name, kind, blockActionKind, result.FileActions)
            {
                CliActions = result.CliActions,
            });
        }

        // Deletes — pull from state, no provider needed.
        foreach (var name in deletes.OrderBy(n => n, StringComparer.Ordinal))
        {
            var previous = state.Blocks[name];
            var blockKind = string.Equals(previous.Kind, "module", StringComparison.OrdinalIgnoreCase)
                ? BlockKind.Module : BlockKind.Resource;
            var result = _reconciler.Reconcile(name, blockKind, BlockActionKind.Delete,
                providerPlan: new ProviderPlan(), previousState: previous, projectDir: projectDir);
            blocks.Add(new BlockAction(name, blockKind, BlockActionKind.Delete, result.FileActions));
        }

        return new Plan { Blocks = blocks };
    }

    private static (BlockKind Kind, string ProviderType, System.Text.Json.Nodes.JsonObject Inputs) LookupDesired(
        ProjectModel model, string name)
    {
        if (model.Resources.TryGetValue(name, out var r))
        {
            return (BlockKind.Resource, r.Type, r.Inputs);
        }

        var m = model.Modules[name];
        return (BlockKind.Module, m.Type, m.Inputs);
    }
}
