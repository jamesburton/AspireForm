using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.Configuration;
using YamlDotNet.Serialization;

namespace AspireForm.Mcp.Tools;

/// <summary>MCP tool: appends a Resource (default) or Module block to the AspireForm config file.</summary>
public sealed class AddTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public AddTool(string defaultProjectDir)
    {
        _defaultProjectDir = defaultProjectDir;
    }

    /// <inheritdoc />
    public string Name => "aspireform_add";

    /// <inheritdoc />
    public string Description => "Append a Resource (default) or Module block to the AspireForm config file.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["type"] = ToolBase.Str("Provider type (e.g. 'sqlserver', 'ef-data')."),
        ["name"] = ToolBase.Str("Block name (defaults to the provider type)."),
        ["module"] = ToolBase.Bool("Treat this block as a Module (default is Resource)."),
        ["dependsOn"] = ToolBase.StrArray("Block names this module depends on."),
        ["projectDir"] = ToolBase.Str("Project directory; defaults to the server's --project-dir."),
    }, "type");

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct) =>
        ToolBase.CatchKnownAsync(() =>
        {
            var type = args["type"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(type))
            {
                return Task.FromResult(ToolResult.Fail("aspireform_add requires 'type'."));
            }

            var projectDir = ToolBase.ResolveProjectDir(args, _defaultProjectDir);
            var module = args["module"]?.GetValue<bool>() ?? false;
            var name = args["name"]?.GetValue<string>() ?? type;
            var dependsOn = (args["dependsOn"] as JsonArray)?
                .Select(n => n?.GetValue<string>())
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(s => s!)
                .ToArray() ?? [];

            var configPath = FindConfigPath(projectDir);
            var format = ConfigFormatDetector.FromPath(configPath)
                ?? throw new ConfigValidationException($"Unrecognized configuration file: '{configPath}'.");
            IConfigParser parser = format == ConfigFormat.Yaml ? new YamlConfigParser() : new JsoncConfigParser();
            var dom = parser.Parse(File.ReadAllText(configPath));

            var section = module ? "modules" : "resources";
            if (dom[section] is not JsonObject blocks)
            {
                blocks = [];
                dom[section] = blocks;
            }

            if (blocks.ContainsKey(name))
            {
                return Task.FromResult(ToolResult.Fail($"Block '{name}' already exists in {section}."));
            }

            var newBlock = new JsonObject { ["type"] = type };
            if (module && dependsOn.Length > 0)
            {
                newBlock["dependsOn"] = new JsonArray(dependsOn.Select(d => (JsonNode)JsonValue.Create(d)!).ToArray());
            }
            blocks[name] = newBlock;

            File.WriteAllText(configPath, Serialise(dom, format));
            return Task.FromResult(ToolResult.Ok(
                $"Added {section[..^1]} '{name}' ({type}) to {Path.GetFileName(configPath)}."));
        });

    private static string FindConfigPath(string projectDir)
    {
        string[] candidates = ["aspireform.yaml", "aspireform.yml", "aspireform.jsonc", "aspireform.json"];
        foreach (var n in candidates)
        {
            var path = Path.Combine(projectDir, n);
            if (File.Exists(path))
            {
                return path;
            }
        }
        throw new ConfigValidationException($"No AspireForm configuration file found in '{projectDir}'.");
    }

    private static string Serialise(JsonObject dom, ConfigFormat format) => format switch
    {
        ConfigFormat.Jsonc => dom.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n",
        ConfigFormat.Yaml => new SerializerBuilder().Build().Serialize(DomToPlain(dom)),
        _ => throw new InvalidOperationException(),
    };

    private static object? DomToPlain(JsonNode? node) => node switch
    {
        JsonObject obj => obj.ToDictionary(kvp => kvp.Key, kvp => DomToPlain(kvp.Value)),
        JsonArray arr => arr.Select(DomToPlain).ToList(),
        JsonValue v when v.TryGetValue(out string? s) => s,
        JsonValue v when v.TryGetValue(out bool b) => b,
        JsonValue v when v.TryGetValue(out long l) => l,
        JsonValue v when v.TryGetValue(out double d) => d,
        null => null,
        _ => node.ToString(),
    };
}
