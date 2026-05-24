using System.ComponentModel;
using AspireForm.Configuration;
using AspireForm.Planning;
using AspireForm.Providers;
using AspireForm.State;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>plan</c> command: shows the reconciliation diff. Pure; no side effects.</summary>
public sealed class PlanCommand : AsyncCommand<PlanCommand.Settings>
{
    /// <summary>Options for the <c>plan</c> command.</summary>
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
    }

    /// <inheritdoc />
    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            var projectDir = Path.GetFullPath(settings.ProjectDir);
            var loaded = new ConfigLoader().Load(projectDir, settings.Env);
            var state = new StateStore().Load(projectDir);
            var registry = await new AspireForm.Plugins.PluginManager().DiscoverAndLoadAsync(loaded.Model, projectDir, cancellationToken);
            var plan = new Planner(registry).Plan(loaded.Model, state, projectDir);

            Console.Out.Write(PlanRenderer.Render(plan));
            return 0;
        }
        catch (ConfigValidationException ex)
        {
            Console.Error.WriteLine($"Configuration error: {ex.Message}");
            return 1;
        }
        catch (DependencyCycleException ex)
        {
            Console.Error.WriteLine($"Plan error: {ex.Message}");
            return 1;
        }
        catch (ProviderNotFoundException ex)
        {
            Console.Error.WriteLine($"Plan error: {ex.Message}");
            return 1;
        }
        catch (AspireForm.Plugins.PluginContractException ex)
        {
            Console.Error.WriteLine($"Plugin error: {ex.Message}");
            return 1;
        }
        catch (AspireForm.State.StateException ex)
        {
            Console.Error.WriteLine($"State error: {ex.Message}");
            return 1;
        }
    }
}
