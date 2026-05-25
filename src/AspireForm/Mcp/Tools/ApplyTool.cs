using System.Text;
using System.Text.Json.Nodes;
using AspireForm.Aspire;
using AspireForm.Configuration;
using AspireForm.Execution;
using AspireForm.Planning;
using AspireForm.Plugins;
using AspireForm.State;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: executes the plan. Always auto-approves (no interactive prompt over MCP).</summary>
public sealed class ApplyTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public ApplyTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "aspireform_apply";

    /// <inheritdoc />
    public string Description => "Execute the plan. Auto-approves (no interactive prompt over MCP).";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["env"] = ToolBase.Str("Environment whose override file is layered over the base."),
        ["projectDir"] = ToolBase.Str("Project directory; defaults to the server's --project-dir."),
        ["forceDrift"] = ToolBase.Bool("Apply even when drift has been detected on tracked files."),
    });

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
        ToolBase.CatchKnownAsync(async () =>
        {
            var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
            var env = args["env"]?.GetValue<string>();
            var forceDrift = args["forceDrift"]?.GetValue<bool>() ?? false;

            var loaded = new ConfigLoader().Load(projectDir, env);
            var stateStore = new StateStore();
            var prevState = stateStore.Load(projectDir);
            var registry = await new PluginManager().DiscoverAndLoadAsync(loaded.Model, projectDir, ct);
            var plan = new Planner(registry).Plan(loaded.Model, prevState, projectDir);

            var sb = new StringBuilder();
            sb.Append(PlanRenderer.Render(plan));

            if (!plan.HasChanges)
            {
                sb.AppendLine("No changes.");
                return ToolResult.Ok(sb.ToString());
            }

            var executor = new Executor(new AspireCli(), stateStore);
            var result = await executor.ApplyAsync(plan, loaded.Model, prevState, projectDir,
                new ExecuteOptions { AutoApprove = true, ForceDrift = forceDrift }, ct);

            if (!result.Success)
            {
                return ToolResult.Fail(sb + Environment.NewLine + $"Apply failed: {result.FailureMessage}");
            }

            sb.AppendLine($"Applied {result.BlocksApplied} block(s).");
            return ToolResult.Ok(sb.ToString());
        });
}
