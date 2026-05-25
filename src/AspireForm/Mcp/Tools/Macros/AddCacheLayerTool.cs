using System.Text;
using System.Text.Json.Nodes;

namespace AspireForm.Mcp.Tools.Macros;

/// <summary>MCP macro: adds a Redis Resource to an existing AspireForm project, plans, and applies.</summary>
public sealed class AddCacheLayerTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the macro with a default project directory.</summary>
    public AddCacheLayerTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "add_cache_layer";

    /// <inheritdoc />
    public string Description =>
        "Adds a Redis Resource to an existing AspireForm project, then plans and applies.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["projectDir"] = ToolBase.Str("Project directory containing aspireform.yaml."),
        ["name"] = ToolBase.Str("Cache block name (default 'cache')."),
    });

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
        var name = args["name"]?.GetValue<string>() ?? "cache";

        var summary = new StringBuilder();
        summary.AppendLine($"Macro: add_cache_layer(name={name})");

        var addResult = await new AddTool(projectDir).ExecuteAsync(
            new JsonObject { ["type"] = "redis", ["name"] = name }, ct);
        summary.AppendLine($"  [1/3] add redis : {Summarise(addResult)}");
        if (addResult.IsError) return ToolResult.Fail(summary.ToString());

        var planResult = await new PlanTool(projectDir).ExecuteAsync([], ct);
        summary.AppendLine($"  [2/3] plan      : {(planResult.IsError ? "FAIL" : "ok")}");
        if (planResult.IsError) return ToolResult.Fail(summary + Environment.NewLine + planResult.Content[0].Text);

        var applyResult = await new ApplyTool(projectDir).ExecuteAsync([], ct);
        summary.AppendLine($"  [3/3] apply     : {(applyResult.IsError ? "FAIL" : "ok")}");
        summary.AppendLine();
        summary.AppendLine("Plan output:");
        summary.AppendLine(planResult.Content[0].Text);
        summary.AppendLine("Apply output:");
        summary.AppendLine(applyResult.Content[0].Text);

        return applyResult.IsError ? ToolResult.Fail(summary.ToString()) : ToolResult.Ok(summary.ToString());
    }

    private static string Summarise(ToolResult r) =>
        r.Content.Count > 0 ? r.Content[0].Text.Split('\n', 2)[0] : (r.IsError ? "FAIL" : "ok");
}
