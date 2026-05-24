using System.ComponentModel;
using AspireForm.Aspire;
using AspireForm.Configuration;
using AspireForm.Execution;
using AspireForm.Planning;
using AspireForm.Providers;
using AspireForm.State;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>apply</c> command: executes the plan after an approval gate.</summary>
public sealed class ApplyCommand : AsyncCommand<ApplyCommand.Settings>
{
    /// <summary>Options for <c>apply</c>.</summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>The project directory containing the AspireForm configuration.</summary>
        [CommandOption("-p|--project-dir <DIR>")]
        [Description("The project directory containing the AspireForm configuration.")]
        public string ProjectDir { get; init; } = ".";

        /// <summary>Environment whose override file is layered over the base config.</summary>
        [CommandOption("-e|--env <ENV>")]
        [Description("Environment whose override file (aspireform.<env>.*) is layered over the base.")]
        public string? Env { get; init; }

        /// <summary>Skip the interactive approval prompt.</summary>
        [CommandOption("-y|--yes")]
        [Description("Skip the interactive approval prompt and apply immediately.")]
        public bool Yes { get; init; }

        /// <summary>Proceed even when drift is detected.</summary>
        [CommandOption("--force-drift")]
        [Description("Apply even when drift has been detected on tracked files.")]
        public bool ForceDrift { get; init; }
    }

    /// <inheritdoc />
    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            var projectDir = Path.GetFullPath(settings.ProjectDir);
            var loaded = new ConfigLoader().Load(projectDir, settings.Env);
            var stateStore = new StateStore();
            var prevState = stateStore.Load(projectDir);
            var plan = new Planner(ProviderRegistry.Default()).Plan(loaded.Model, prevState, projectDir);

            Console.Out.Write(PlanRenderer.Render(plan));

            if (!plan.HasChanges)
            {
                return 0;
            }

            if (!settings.Yes && !PromptForApproval())
            {
                Console.Out.WriteLine("Aborted.");
                return 1;
            }

            var executor = new Executor(new AspireCli(), stateStore);
            var result = await executor.ApplyAsync(plan, loaded.Model, prevState, projectDir,
                new ExecuteOptions { AutoApprove = true, ForceDrift = settings.ForceDrift }, cancellationToken);

            if (!result.Success)
            {
                Console.Error.WriteLine($"Apply failed: {result.FailureMessage}");
                return 1;
            }

            Console.Out.WriteLine($"Applied {result.BlocksApplied} block(s).");
            return 0;
        }
        catch (ConfigValidationException ex) { return Fail("Configuration error", ex); }
        catch (StateException ex)             { return Fail("State error", ex); }
        catch (DependencyCycleException ex)   { return Fail("Plan error", ex); }
        catch (ProviderNotFoundException ex)  { return Fail("Plan error", ex); }
    }

    private static int Fail(string prefix, Exception ex)
    {
        Console.Error.WriteLine($"{prefix}: {ex.Message}");
        return 1;
    }

    private static bool PromptForApproval()
    {
        Console.Out.Write("Apply this plan? [y/N]: ");
        var line = Console.In.ReadLine();
        return string.Equals(line?.Trim(), "y", StringComparison.OrdinalIgnoreCase);
    }
}
