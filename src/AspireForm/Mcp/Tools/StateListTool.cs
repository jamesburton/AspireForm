using System.Text;
using System.Text.Json.Nodes;
using AspireForm.State;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: lists all tracked blocks. Mirrors StateListCommand's table output.</summary>
public sealed class StateListTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public StateListTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "aspireform_state_list";

    /// <inheritdoc />
    public string Description => "List all tracked blocks (block, kind, type, file count).";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["projectDir"] = ToolBase.Str("Project directory; defaults to the server's --project-dir."),
    });

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
        ToolBase.CatchKnownAsync(() =>
        {
            var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
            var state = new StateStore().Load(projectDir);
            if (state.Blocks.Count == 0)
            {
                return Task.FromResult(ToolResult.Ok("No tracked blocks."));
            }

            var sb = new StringBuilder();
            sb.AppendLine("Block        Kind      Type          Files");
            sb.AppendLine("-----        ----      ----          -----");
            foreach (var (name, block) in state.Blocks.OrderBy(b => b.Key, StringComparer.Ordinal))
            {
                sb.AppendLine($"{Pad(name, 12)} {Pad(block.Kind, 9)} {Pad(block.Type, 13)} {block.Files.Count}");
            }

            return Task.FromResult(ToolResult.Ok(sb.ToString()));

            static string Pad(string s, int width) => s.PadRight(width)[..Math.Max(width, s.Length)];
        });
}
