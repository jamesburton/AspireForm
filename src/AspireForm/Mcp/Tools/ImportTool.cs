using System.Text.Json.Nodes;
using AspireForm.Configuration;
using AspireForm.Execution;
using AspireForm.Planning;
using AspireForm.Plugins;
using AspireForm.Providers;
using AspireForm.State;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: adopts an existing block into AspireForm state without executing. The block must already be declared in the AspireForm config file.</summary>
public sealed class ImportTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public ImportTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "aspireform_import";

    /// <inheritdoc />
    public string Description => "Adopt an existing block (declared in the config file) into AspireForm state without executing.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["block"] = ToolBase.Str("Block name to import (required). Must already be declared in the config file."),
        ["projectDir"] = ToolBase.Str("Project directory; defaults to the server's --project-dir."),
    }, "block");

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
        ToolBase.CatchKnownAsync(async () =>
        {
            var blockName = args["block"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(blockName))
            {
                return ToolResult.Fail("aspireform_import requires 'block'.");
            }

            var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
            var loaded = new ConfigLoader().Load(projectDir, env: null);

            BlockKind blockKind;
            string blockType;
            JsonObject inputs;

            if (loaded.Model.Resources.TryGetValue(blockName, out var r))
            {
                blockKind = BlockKind.Resource;
                blockType = r.Type;
                inputs = r.Inputs;
            }
            else if (loaded.Model.Modules.TryGetValue(blockName, out var m))
            {
                blockKind = BlockKind.Module;
                blockType = m.Type;
                inputs = m.Inputs;
            }
            else
            {
                return ToolResult.Fail($"Block '{blockName}' is not declared in the config file.");
            }

            var registry = await new PluginManager().DiscoverAndLoadAsync(loaded.Model, projectDir, ct);
            var provider = registry.Get(blockType);
            var providerCtx = new PlanContext(
                BlockName: blockName,
                Inputs: inputs,
                AppHostDirectory: loaded.Model.AspireForm.AppHost,
                ProjectName: loaded.Model.AspireForm.Project);
            var providerPlan = provider.Plan(providerCtx);

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

            state.Blocks[blockName] = new BlockState
            {
                Type = blockType,
                Kind = blockKind == BlockKind.Module ? "module" : "resource",
                Files = files,
                Inputs = inputs,
            };

            stateStore.Save(projectDir, state);
            return ToolResult.Ok($"Imported '{blockName}' ({blockType}, {files.Count} file(s)).");
        });
}
