using System.Text.Json.Nodes;

namespace AspireForm.Configuration;

/// <summary>The result of loading configuration: the resolved DOM and the bound model.</summary>
/// <param name="Resolved">The merged and interpolated configuration DOM.</param>
/// <param name="Model">The validated, bound project model.</param>
public sealed record LoadedConfig(JsonObject Resolved, ProjectModel Model);

/// <summary>Discovers, parses, layers, interpolates, and binds AspireForm configuration files.</summary>
public sealed class ConfigLoader
{
    private static readonly string[] BaseNames = ["aspireform.yaml", "aspireform.yml", "aspireform.jsonc", "aspireform.json"];

    /// <summary>
    /// Loads the configuration from <paramref name="projectDir"/>. When <paramref name="env"/> is supplied,
    /// an <c>aspireform.&lt;env&gt;.*</c> override file (if present) is deep-merged over the base.
    /// </summary>
    /// <param name="projectDir">The directory to search for configuration files.</param>
    /// <param name="env">The optional environment name (e.g. <c>"dev"</c>, <c>"prod"</c>).</param>
    /// <returns>A <see cref="LoadedConfig"/> containing the resolved DOM and bound model.</returns>
    /// <exception cref="ConfigValidationException">
    /// Thrown when no configuration file is found, multiple base files exist, or the configuration is invalid.
    /// </exception>
    public LoadedConfig Load(string projectDir, string? env)
    {
        var basePath = FindBaseConfig(projectDir);
        var dom = ParseFile(basePath);

        if (env is not null)
        {
            var overridePath = FindOverrideConfig(projectDir, env);
            if (overridePath is not null)
            {
                dom = JsonObjectMerge.Merge(dom, ParseFile(overridePath));
            }
        }

        var envFile = EnvFile.Load(Path.Combine(projectDir, ".env"));
        var variables = Interpolator.BuildVariables(envFile);
        var resolved = Interpolator.Apply(dom, variables);

        var model = ConfigModelBinder.Bind(resolved);
        return new LoadedConfig(resolved, model);
    }

    private static string FindBaseConfig(string projectDir)
    {
        var present = BaseNames
            .Select(name => Path.Combine(projectDir, name))
            .Where(File.Exists)
            .ToList();

        return present switch
        {
            { Count: 0 } => throw new ConfigValidationException(
                $"No AspireForm configuration file found in '{projectDir}' (expected one of: {string.Join(", ", BaseNames)})."),
            { Count: > 1 } => throw new ConfigValidationException(
                $"Multiple AspireForm configuration files found in '{projectDir}': {string.Join(", ", present.Select(Path.GetFileName))}. Keep exactly one."),
            _ => present[0],
        };
    }

    private static string? FindOverrideConfig(string projectDir, string env)
    {
        string[] candidates =
        [
            $"aspireform.{env}.yaml", $"aspireform.{env}.yml",
            $"aspireform.{env}.jsonc", $"aspireform.{env}.json",
        ];

        return candidates
            .Select(name => Path.Combine(projectDir, name))
            .FirstOrDefault(File.Exists);
    }

    private static JsonObject ParseFile(string path)
    {
        var format = ConfigFormatDetector.FromPath(path)
            ?? throw new ConfigValidationException($"Unrecognized configuration file extension: '{path}'.");

        IConfigParser parser = format switch
        {
            ConfigFormat.Yaml => new YamlConfigParser(),
            ConfigFormat.Jsonc => new JsoncConfigParser(),
            _ => throw new ConfigValidationException($"Unsupported configuration format for '{path}'."),
        };

        return parser.Parse(File.ReadAllText(path));
    }
}
