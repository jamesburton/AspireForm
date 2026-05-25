using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.State;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: dumps one tracked block as indented JSON.</summary>
public sealed class StateShowTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    private static readonly JsonSerializerOptions PrettyOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Creates the tool with a default project directory.</summary>
    public StateShowTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "aspireform_state_show";

    /// <inheritdoc />
    public string Description => "Show one tracked block's state as indented JSON.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["block"] = ToolBase.Str("Block name to show (required)."),
        ["projectDir"] = ToolBase.Str("Project directory; defaults to the server's --project-dir."),
    }, "block");

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
        ToolBase.CatchKnownAsync(() =>
        {
            var blockName = args["block"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(blockName))
            {
                return Task.FromResult(ToolResult.Fail("aspireform_state_show requires 'block'."));
            }

            var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
            var state = new StateStore().Load(projectDir);
            if (!state.Blocks.TryGetValue(blockName, out var block))
            {
                return Task.FromResult(ToolResult.Fail($"Block '{blockName}' is not tracked in state."));
            }

            return Task.FromResult(ToolResult.Ok(JsonSerializer.Serialize(block, PrettyOptions)));
        });
}
