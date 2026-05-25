using System.Text;
using System.Text.Json.Nodes;

namespace AspireForm.Mcp.Tools.Macros;

/// <summary>MCP macro: scaffolds a new project, adds a SQL Server Resource and an ef-data Module, then plans and applies.</summary>
public sealed class ScaffoldAspireAppWithDataTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the macro with a default project directory.</summary>
    public ScaffoldAspireAppWithDataTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "scaffold_aspire_app_with_data";

    /// <inheritdoc />
    public string Description =>
        "End-to-end recipe: create a new Aspire app, add a SQL Server Resource and an ef-data Module, then plan and apply.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["name"] = ToolBase.Str("Project name (required)."),
        ["output"] = ToolBase.Str("Output directory (defaults to the server's --project-dir)."),
        ["databaseName"] = ToolBase.Str("Database block name (defaults to 'appdb')."),
    }, "name");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var name = args["name"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ToolResult.Fail("scaffold_aspire_app_with_data requires 'name'.");
        }

        var output = args["output"]?.GetValue<string>() ?? _defaultProjectDir;
        var dbName = args["databaseName"]?.GetValue<string>() ?? "appdb";
        var projectRoot = Path.GetFullPath(Path.Combine(output, name));

        var summary = new StringBuilder();
        summary.AppendLine($"Macro: scaffold_aspire_app_with_data(name={name}, databaseName={dbName})");

        var newResult = await new NewTool(_defaultProjectDir).ExecuteAsync(
            new JsonObject { ["name"] = name, ["output"] = output }, ct);
        summary.AppendLine($"  [1/5] new       : {Summarise(newResult)}");
        if (newResult.IsError) return ToolResult.Fail(summary.ToString());

        var addSqlResult = await new AddTool(projectRoot).ExecuteAsync(
            new JsonObject { ["type"] = "sqlserver", ["name"] = dbName }, ct);
        summary.AppendLine($"  [2/5] add sql   : {Summarise(addSqlResult)}");
        if (addSqlResult.IsError) return ToolResult.Fail(summary.ToString());

        var addEfResult = await new AddTool(projectRoot).ExecuteAsync(
            new JsonObject
            {
                ["type"] = "ef-data",
                ["name"] = "data",
                ["module"] = true,
                ["dependsOn"] = new JsonArray(dbName),
            }, ct);
        summary.AppendLine($"  [3/5] add ef-data: {Summarise(addEfResult)}");
        if (addEfResult.IsError) return ToolResult.Fail(summary.ToString());

        var planResult = await new PlanTool(projectRoot).ExecuteAsync([], ct);
        summary.AppendLine($"  [4/5] plan      : {(planResult.IsError ? "FAIL" : "ok")}");
        if (planResult.IsError) return ToolResult.Fail(summary + Environment.NewLine + planResult.Content[0].Text);

        var applyResult = await new ApplyTool(projectRoot).ExecuteAsync([], ct);
        summary.AppendLine($"  [5/5] apply     : {(applyResult.IsError ? "FAIL" : "ok")}");
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
