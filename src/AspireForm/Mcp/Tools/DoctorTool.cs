using System.Text;
using System.Text.Json.Nodes;
using AspireForm.Aspire;
using AspireForm.Diagnostics;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: runs the prerequisite checker and returns the report.</summary>
public sealed class DoctorTool : IToolHandler
{
    /// <inheritdoc />
    public string Name => "aspireform_doctor";

    /// <inheritdoc />
    public string Description => "Check that AspireForm's prerequisites are installed.";

    /// <inheritdoc />
    public JsonObject InputSchema => new() { ["type"] = "object", ["properties"] = new JsonObject() };

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var report = await new PrerequisiteChecker(new AspireCli()).RunAsync(ct);
        var sb = new StringBuilder();
        foreach (var check in report.Checks)
        {
            var status = check.Ok ? "OK    " : "FAILED";
            sb.AppendLine($"[{status}] {check.Name}: {check.Detail}");
        }

        foreach (var failed in report.Checks.Where(c => !c.Ok && c.Remedy is not null))
        {
            sb.AppendLine($"  -> {failed.Name}: {failed.Remedy}");
        }

        return report.AllPassed ? ToolResult.Ok(sb.ToString()) : ToolResult.Fail(sb.ToString());
    }
}
