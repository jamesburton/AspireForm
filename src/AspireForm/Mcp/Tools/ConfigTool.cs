using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.Configuration;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: prints the fully merged + interpolated AspireForm configuration as indented JSON.</summary>
public sealed class ConfigTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory (used when args omit <c>projectDir</c>).</summary>
    public ConfigTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "aspireform_config";

    /// <inheritdoc />
    public string Description => "Print the fully merged and interpolated desired-state configuration as JSON.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["env"] = ToolBase.Str("Environment whose override file is layered over the base."),
        ["projectDir"] = ToolBase.Str("Project directory; defaults to the server's --project-dir."),
    });

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
        ToolBase.CatchKnownAsync(() =>
        {
            var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
            var env = args["env"]?.GetValue<string>();
            var loaded = new ConfigLoader().Load(projectDir, env);
            var json = loaded.Resolved.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            return Task.FromResult(ToolResult.Ok(json));
        });
}
