using System.ComponentModel;
using AspireForm.Configuration;
using AspireForm.Execution;
using AspireForm.Planning;
using AspireForm.Providers;
using AspireForm.State;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>import</c> command: records a block into state without running anything (adopts an existing setup).</summary>
public sealed class ImportCommand : AsyncCommand<ImportCommand.Settings>
{
    /// <summary>Options for <c>import</c>.</summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>The block name (must already exist in the config file).</summary>
        [CommandArgument(0, "<BLOCK>")]
        [Description("The block name (must already exist in the config file).")]
        public required string BlockName { get; init; }

        /// <summary>The project directory.</summary>
        [CommandOption("-p|--project-dir <DIR>")]
        [Description("The project directory.")]
        public string ProjectDir { get; init; } = ".";
    }

    /// <inheritdoc />
    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            var projectDir = Path.GetFullPath(settings.ProjectDir);
            var loaded = new ConfigLoader().Load(projectDir, env: null);

            BlockKind blockKind;
            string blockType;
            System.Text.Json.Nodes.JsonObject inputs;

            if (loaded.Model.Resources.TryGetValue(settings.BlockName, out var r))
            {
                blockKind = BlockKind.Resource;
                blockType = r.Type;
                inputs = r.Inputs;
            }
            else if (loaded.Model.Modules.TryGetValue(settings.BlockName, out var m))
            {
                blockKind = BlockKind.Module;
                blockType = m.Type;
                inputs = m.Inputs;
            }
            else
            {
                Console.Error.WriteLine($"Block '{settings.BlockName}' is not declared in the config file.");
                return 1;
            }

            var registry = await new AspireForm.Plugins.PluginManager().DiscoverAndLoadAsync(loaded.Model, projectDir, cancellationToken);
            var provider = registry.Get(blockType);
            var ctx = new PlanContext(
                BlockName: settings.BlockName,
                Inputs: inputs,
                AppHostDirectory: loaded.Model.AspireForm.AppHost,
                ProjectName: loaded.Model.AspireForm.Project);
            var providerPlan = provider.Plan(ctx);

            var stateStore = new StateStore();
            var state = stateStore.Load(projectDir);

            var files = new Dictionary<string, FileState>(StringComparer.Ordinal);
            foreach (var planned in providerPlan.FileActions)
            {
                var absolute = Path.IsPathRooted(planned.Path)
                    ? planned.Path
                    : Path.GetFullPath(Path.Combine(projectDir, planned.Path));
                var checksum = File.Exists(absolute) ? DriftDetector.ComputeChecksum(absolute) : string.Empty;
                files[PathUtilities.ToRepoRelative(absolute, projectDir)] = new FileState
                {
                    OwnershipMode = planned.OwnershipMode.ToString().ToLowerInvariant(),
                    Checksum = checksum,
                };
            }

            state.Blocks[settings.BlockName] = new BlockState
            {
                Type = blockType,
                Kind = blockKind == BlockKind.Module ? "module" : "resource",
                Files = files,
                Inputs = inputs,
            };

            stateStore.Save(projectDir, state);
            Console.Out.WriteLine($"Imported '{settings.BlockName}' ({blockType}, {files.Count} file(s)).");
            return 0;
        }
        catch (ConfigValidationException ex) { return Fail("Configuration error", ex); }
        catch (StateException ex)             { return Fail("State error", ex); }
        catch (ProviderNotFoundException ex)  { return Fail("Import error", ex); }
        catch (AspireForm.Plugins.PluginContractException ex) { return Fail("Plugin error", ex); }
    }

    private static int Fail(string prefix, Exception ex)
    {
        Console.Error.WriteLine($"{prefix}: {ex.Message}");
        return 1;
    }
}
