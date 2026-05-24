using System.ComponentModel;
using System.Text.Json.Nodes;
using AspireForm.Configuration;
using Spectre.Console.Cli;
using YamlDotNet.Serialization;

namespace AspireForm.Cli;

/// <summary>The <c>add</c> command: appends a Resource (default) or Module block to the AspireForm config file.</summary>
public sealed class AddCommand : AsyncCommand<AddCommand.Settings>
{
    /// <summary>Options for <c>add</c>.</summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>Provider type (e.g. <c>sqlserver</c>, <c>ef-data</c>).</summary>
        [CommandArgument(0, "<TYPE>")]
        [Description("Provider type (e.g. sqlserver, ef-data).")]
        public required string Type { get; init; }

        /// <summary>Block name. Defaults to the provider type when omitted.</summary>
        [CommandArgument(1, "[NAME]")]
        [Description("Block name (defaults to the provider type).")]
        public string? Name { get; init; }

        /// <summary>The project directory.</summary>
        [CommandOption("-p|--project-dir <DIR>")]
        [Description("The project directory.")]
        public string ProjectDir { get; init; } = ".";

        /// <summary>Treat this block as a Module (default is Resource).</summary>
        [CommandOption("-m|--module")]
        [Description("Treat this block as a Module (default is Resource).")]
        public bool Module { get; init; }

        /// <summary>Block names this module depends on (may be repeated).</summary>
        [CommandOption("--depends-on <BLOCK>")]
        [Description("Block this module depends on (may be repeated).")]
        public string[] DependsOn { get; init; } = [];
    }

    /// <inheritdoc />
    protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        return Task.FromResult(ExecuteCore(settings));
    }

    private static int ExecuteCore(Settings settings)
    {
        try
        {
            var projectDir = Path.GetFullPath(settings.ProjectDir);
            var configPath = FindConfigPath(projectDir);
            var blockName = settings.Name ?? settings.Type;

            // Load as a DOM (not via ConfigLoader — we want to mutate, not validate fully).
            var format = ConfigFormatDetector.FromPath(configPath)
                ?? throw new ConfigValidationException($"Unrecognized configuration file: '{configPath}'.");
            IConfigParser parser = format == ConfigFormat.Yaml ? new YamlConfigParser() : new JsoncConfigParser();
            var dom = parser.Parse(File.ReadAllText(configPath));

            var section = settings.Module ? "modules" : "resources";
            if (dom[section] is not JsonObject blocks)
            {
                blocks = [];
                dom[section] = blocks;
            }

            if (blocks.ContainsKey(blockName))
            {
                Console.Error.WriteLine($"Block '{blockName}' already exists in {section}.");
                return 1;
            }

            var newBlock = new JsonObject { ["type"] = settings.Type };
            if (settings.Module && settings.DependsOn.Length > 0)
            {
                newBlock["dependsOn"] = new JsonArray(settings.DependsOn.Select(d => (JsonNode)JsonValue.Create(d)!).ToArray());
            }

            blocks[blockName] = newBlock;

            File.WriteAllText(configPath, Serialise(dom, format));
            Console.Out.WriteLine($"Added {section[..^1]} '{blockName}' ({settings.Type}) to {Path.GetFileName(configPath)}.");
            return 0;
        }
        catch (ConfigValidationException ex)
        {
            Console.Error.WriteLine($"Configuration error: {ex.Message}");
            return 1;
        }
    }

    private static string FindConfigPath(string projectDir)
    {
        string[] candidates = ["aspireform.yaml", "aspireform.yml", "aspireform.jsonc", "aspireform.json"];
        foreach (var name in candidates)
        {
            var path = Path.Combine(projectDir, name);
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new ConfigValidationException($"No AspireForm configuration file found in '{projectDir}'.");
    }

    private static string Serialise(JsonObject dom, ConfigFormat format) => format switch
    {
        ConfigFormat.Jsonc => dom.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n",
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
