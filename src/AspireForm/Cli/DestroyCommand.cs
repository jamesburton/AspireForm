using System.ComponentModel;
using AspireForm.Aspire;
using AspireForm.Configuration;
using AspireForm.Execution;
using AspireForm.Planning;
using AspireForm.Providers;
using AspireForm.State;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>destroy</c> command: removes one or all blocks currently in state.</summary>
public sealed class DestroyCommand : AsyncCommand<DestroyCommand.Settings>
{
    /// <summary>Options for <c>destroy</c>.</summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>Optional block name. When omitted, every block in state is destroyed.</summary>
        [CommandArgument(0, "[BLOCK]")]
        [Description("Optional block name. When omitted, every block in state is destroyed.")]
        public string? BlockName { get; init; }

        /// <summary>The project directory.</summary>
        [CommandOption("-p|--project-dir <DIR>")]
        [Description("The project directory.")]
        public string ProjectDir { get; init; } = ".";

        /// <summary>Skip the interactive approval prompt.</summary>
        [CommandOption("-y|--yes")]
        [Description("Skip the interactive approval prompt and destroy immediately.")]
        public bool Yes { get; init; }

        /// <summary>Allow destroying Module blocks (otherwise refused due to destroy-protection).</summary>
        [CommandOption("--allow-module-destroy")]
        [Description("Allow destroying Module blocks (otherwise refused due to destroy-protection).")]
        public bool AllowModuleDestroy { get; init; }
    }

    /// <inheritdoc />
    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            var projectDir = Path.GetFullPath(settings.ProjectDir);
            var loaded = new ConfigLoader().Load(projectDir, env: null);
            var stateStore = new StateStore();
            var prevState = stateStore.Load(projectDir);

            // Decide which blocks to destroy.
            var targets = settings.BlockName is null
                ? prevState.Blocks.Keys.ToList()
                : [settings.BlockName];

            foreach (var name in targets)
            {
                if (!prevState.Blocks.TryGetValue(name, out var blockState))
                {
                    Console.Error.WriteLine($"Block '{name}' is not tracked in state.");
                    return 1;
                }

                if (!settings.AllowModuleDestroy
                    && string.Equals(blockState.Kind, "module", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine(
                        $"Refusing to destroy module block '{name}': pass --allow-module-destroy to override.");
                    return 1;
                }
            }

            // Build an "empty desired state" with the targets removed but other blocks preserved.
            var pseudoModel = BuildPseudoModelExcluding(loaded.Model, targets);
            var plan = new Planner(ProviderRegistry.Default()).Plan(pseudoModel, prevState, projectDir);

            Console.Out.Write(PlanRenderer.Render(plan));

            if (!plan.Blocks.Any(b => b.Kind == BlockActionKind.Delete))
            {
                Console.Out.WriteLine("Nothing to destroy.");
                return 0;
            }

            if (!settings.Yes && !PromptForApproval())
            {
                Console.Out.WriteLine("Aborted.");
                return 1;
            }

            var executor = new Executor(new AspireCli(), stateStore);
            var result = await executor.ApplyAsync(plan, pseudoModel, prevState, projectDir,
                new ExecuteOptions { AutoApprove = true, ForceDrift = true }, cancellationToken);

            if (!result.Success)
            {
                Console.Error.WriteLine($"Destroy failed: {result.FailureMessage}");
                return 1;
            }

            Console.Out.WriteLine($"Destroyed {targets.Count} block(s).");
            return 0;
        }
        catch (ConfigValidationException ex)    { return Fail("Configuration error", ex); }
        catch (StateException ex)               { return Fail("State error", ex); }
        catch (DependencyCycleException ex)     { return Fail("Plan error", ex); }
        catch (ProviderNotFoundException ex)    { return Fail("Plan error", ex); }
    }

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

    private static int Fail(string prefix, Exception ex)
    {
        Console.Error.WriteLine($"{prefix}: {ex.Message}");
        return 1;
    }

    private static bool PromptForApproval()
    {
        Console.Out.Write("Destroy? [y/N]: ");
        var line = Console.In.ReadLine();
        return string.Equals(line?.Trim(), "y", StringComparison.OrdinalIgnoreCase);
    }
}
