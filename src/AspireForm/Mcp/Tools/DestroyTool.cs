using System.Text.Json.Nodes;
using AspireForm.Aspire;
using AspireForm.Configuration;
using AspireForm.Execution;
using AspireForm.Planning;
using AspireForm.Plugins;
using AspireForm.Providers;
using AspireForm.State;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: destroys one block (when <c>block</c> is supplied) or all blocks (when omitted). Mirrors <c>DestroyCommand</c> — builds a pseudo-model with the targets removed from desired state so the standard planner emits Delete actions.</summary>
public sealed class DestroyTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public DestroyTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "aspireform_destroy";

    /// <inheritdoc />
    public string Description => "Destroy one block (when 'block' is supplied) or all blocks.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["block"] = ToolBase.Str("Block name to destroy; omit to destroy all tracked blocks."),
        ["projectDir"] = ToolBase.Str("Project directory; defaults to the server's --project-dir."),
        ["allowModuleDestroy"] = ToolBase.Bool("Permit destroying Module blocks (which are destroy-protected by default)."),
    });

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
        ToolBase.CatchKnownAsync(async () =>
        {
            var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
            var block = args["block"]?.GetValue<string>();
            var allowModuleDestroy = args["allowModuleDestroy"]?.GetValue<bool>() ?? false;

            var loaded = new ConfigLoader().Load(projectDir, env: null);
            var stateStore = new StateStore();
            var prevState = stateStore.Load(projectDir);

            // Decide which blocks to destroy.
            var targets = string.IsNullOrEmpty(block)
                ? prevState.Blocks.Keys.ToList()
                : [block];

            foreach (var name in targets)
            {
                if (!prevState.Blocks.TryGetValue(name, out var blockState))
                {
                    return ToolResult.Fail($"Block '{name}' is not tracked in state.");
                }

                if (!allowModuleDestroy
                    && string.Equals(blockState.Kind, "module", StringComparison.OrdinalIgnoreCase))
                {
                    return ToolResult.Fail(
                        $"Refusing to destroy module block '{name}': pass allowModuleDestroy=true to override.");
                }
            }

            var pseudoModel = BuildPseudoModelExcluding(loaded.Model, targets);
            var registry = await new PluginManager().DiscoverAndLoadAsync(loaded.Model, projectDir, ct);
            var plan = new Planner(registry).Plan(pseudoModel, prevState, projectDir);

            if (!plan.Blocks.Any(b => b.Kind == BlockActionKind.Delete))
            {
                return ToolResult.Ok("Nothing to destroy.");
            }

            var executor = new Executor(new AspireCli(), stateStore);
            var result = await executor.ApplyAsync(plan, pseudoModel, prevState, projectDir,
                new ExecuteOptions { AutoApprove = true, ForceDrift = true }, ct);

            return result.Success
                ? ToolResult.Ok($"Destroyed {targets.Count} block(s).")
                : ToolResult.Fail($"Destroy failed: {result.FailureMessage}");
        });

    private static ProjectModel BuildPseudoModelExcluding(ProjectModel original, IReadOnlyList<string> exclude)
    {
        var ex = exclude.ToHashSet(StringComparer.Ordinal);
        return new ProjectModel
        {
            AspireForm = original.AspireForm,
            Resources = original.Resources.Where(r => !ex.Contains(r.Key)).ToDictionary(),
            Modules = original.Modules.Where(m => !ex.Contains(m.Key)).ToDictionary(),
            Profiles = original.Profiles,
        };
    }
}
