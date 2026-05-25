using System.Text.Json.Nodes;
using AspireForm.Configuration;
using AspireForm.Planning;
using AspireForm.Plugins;
using AspireForm.State;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: renders the reconciliation diff between desired and current state.</summary>
public sealed class PlanTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public PlanTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "aspireform_plan";

    /// <inheritdoc />
    public string Description => "Show the reconciliation diff between desired and current state (unified diffs).";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["env"] = ToolBase.Str("Environment whose override file is layered over the base."),
        ["projectDir"] = ToolBase.Str("Project directory; defaults to the server's --project-dir."),
    });

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
        ToolBase.CatchKnownAsync(async () =>
        {
            var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
            var env = args["env"]?.GetValue<string>();
            var loaded = new ConfigLoader().Load(projectDir, env);
            var state = new StateStore().Load(projectDir);
            var registry = await new PluginManager().DiscoverAndLoadAsync(loaded.Model, projectDir, ct);
            var plan = new Planner(registry).Plan(loaded.Model, state, projectDir);
            return ToolResult.Ok(PlanRenderer.Render(plan));
        });
}
