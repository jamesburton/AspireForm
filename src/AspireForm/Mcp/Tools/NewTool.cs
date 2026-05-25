using System.Diagnostics;
using System.Text.Json.Nodes;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: scaffolds a new Aspire AppHost project + starter <c>aspireform.yaml</c>. Mirrors the CLI <c>new</c> verb.</summary>
public sealed class NewTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory (used as the default output root).</summary>
    public NewTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "aspireform_new";

    /// <inheritdoc />
    public string Description => "Scaffold a new Aspire AppHost project and starter aspireform.yaml in <output>/<name>.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["name"] = ToolBase.Str("The project name (required)."),
        ["output"] = ToolBase.Str("Output directory (defaults to the server's --project-dir)."),
    }, "name");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var name = args["name"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ToolResult.Fail("aspireform_new requires 'name'.");
        }

        var outputDir = args["output"]?.GetValue<string>() ?? _defaultProjectDir;
        var projectRoot = Path.GetFullPath(Path.Combine(outputDir, name));
        var appHostName = $"{name}.AppHost";

        if (Directory.Exists(projectRoot))
        {
            return ToolResult.Fail($"Refusing to scaffold into existing directory '{projectRoot}'.");
        }

        Directory.CreateDirectory(projectRoot);

        var (exitCode, stderr) = await RunDotnetNewAsync(appHostName, projectRoot, ct);
        if (exitCode != 0)
        {
            return ToolResult.Fail($"dotnet new aspire-apphost failed (exit {exitCode}): {stderr}");
        }

        WriteStarterYaml(projectRoot, name, appHostName);

        var summary =
            $"Created {projectRoot}{Environment.NewLine}" +
            $"  - {appHostName}/ (Aspire AppHost project){Environment.NewLine}" +
            $"  - aspireform.yaml (starter)";
        return ToolResult.Ok(summary);
    }

    private static async Task<(int ExitCode, string StandardError)> RunDotnetNewAsync(
        string appHostName, string workingDirectory, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("new");
        startInfo.ArgumentList.Add("aspire-apphost");
        startInfo.ArgumentList.Add("-n");
        startInfo.ArgumentList.Add(appHostName);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start dotnet.");
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        await Task.WhenAll(stderrTask, stdoutTask);
        await process.WaitForExitAsync(ct);
        return (process.ExitCode, await stderrTask);
    }

    private static void WriteStarterYaml(string projectRoot, string projectName, string appHostName)
    {
        var content = $$"""
            aspireform:
              version: 1
              project: {{projectName}}
              apphost: {{appHostName}}
            resources: {}
            modules: {}
            """;
        File.WriteAllText(Path.Combine(projectRoot, "aspireform.yaml"), content);
    }
}
