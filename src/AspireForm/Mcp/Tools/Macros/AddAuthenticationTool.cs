using System.Text;
using System.Text.Json.Nodes;
using AspireForm.Plugins;

namespace AspireForm.Mcp.Tools.Macros;

/// <summary>MCP macro: adds an authentication variant (apikey / magiclink / entra) to an existing project. Auto-installs the relevant plugin if missing, then adds + plans + applies.</summary>
public sealed class AddAuthenticationTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the macro with a default project directory.</summary>
    public AddAuthenticationTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "add_authentication";

    /// <inheritdoc />
    public string Description =>
        "Adds an authentication variant (apikey/magiclink/entra) to an AspireForm project. " +
        "Auto-installs the matching plugin if missing.";

    /// <inheritdoc />
    public JsonObject InputSchema
    {
        get
        {
            var variant = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray("apikey", "magiclink", "entra"),
                ["description"] = "Auth variant.",
            };
            return ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
            {
                ["projectDir"] = ToolBase.Str("Project directory containing aspireform.yaml."),
                ["name"] = ToolBase.Str("Auth block name (default 'auth')."),
                ["variant"] = variant,
                ["inputs"] = new JsonObject { ["type"] = "object", ["description"] = "Variant-specific inputs to inject under the block." },
            }, "variant");
        }
    }

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var variant = args["variant"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(variant))
        {
            return ToolResult.Fail("add_authentication requires 'variant'.");
        }
        if (variant is not ("apikey" or "magiclink" or "entra"))
        {
            return ToolResult.Fail($"Unknown variant '{variant}'. Allowed: apikey, magiclink, entra.");
        }

        var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
        var name = args["name"]?.GetValue<string>() ?? "auth";
        var blockType = $"auth-{variant}";
        var pluginName = variant switch
        {
            "apikey" => "Auth.ApiKey",
            "magiclink" => "Auth.MagicLink",
            "entra" => "Auth.Entra",
            _ => throw new InvalidOperationException(),
        };
        var packageId = $"AspireForm.Plugin.{pluginName}";

        var summary = new StringBuilder();
        summary.AppendLine($"Macro: add_authentication(variant={variant}, name={name})");

        var lockfile = PluginLockfile.Load(projectDir);
        if (!lockfile.Plugins.Any(p => string.Equals(p.Package, packageId, StringComparison.OrdinalIgnoreCase)))
        {
            var installResult = await new Tools.PluginInstallTool(projectDir).ExecuteAsync(
                new JsonObject { ["name"] = pluginName }, ct);
            summary.AppendLine($"  [1/4] install   : {Summarise(installResult)}");
            if (installResult.IsError) return ToolResult.Fail(summary.ToString());
        }
        else
        {
            summary.AppendLine($"  [1/4] install   : already installed");
        }

        var addResult = await new AddTool(projectDir).ExecuteAsync(
            new JsonObject { ["type"] = blockType, ["name"] = name }, ct);
        summary.AppendLine($"  [2/4] add       : {Summarise(addResult)}");
        if (addResult.IsError) return ToolResult.Fail(summary.ToString());

        var planResult = await new PlanTool(projectDir).ExecuteAsync([], ct);
        summary.AppendLine($"  [3/4] plan      : {(planResult.IsError ? "FAIL" : "ok")}");
        if (planResult.IsError) return ToolResult.Fail(summary + Environment.NewLine + planResult.Content[0].Text);

        var applyResult = await new ApplyTool(projectDir).ExecuteAsync([], ct);
        summary.AppendLine($"  [4/4] apply     : {(applyResult.IsError ? "FAIL" : "ok")}");
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
