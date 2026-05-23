using System.Text.Json.Nodes;

namespace AspireForm.Configuration;

/// <summary>Binds a merged, interpolated configuration DOM into a validated <see cref="ProjectModel"/>.</summary>
public static class ConfigModelBinder
{
    private const int SupportedSchemaVersion = 1;

    private static readonly string[] ResourceReservedKeys = ["type"];
    private static readonly string[] ModuleReservedKeys = ["type", "dependsOn", "preventDestroy"];

    /// <summary>Binds and validates the DOM. Throws <see cref="ConfigValidationException"/> on any violation.</summary>
    public static ProjectModel Bind(JsonObject dom)
    {
        var header = BindHeader(dom);
        var resources = BindResources(dom);
        var modules = BindModules(dom);
        var profiles = BindProfiles(dom);

        ValidateDependencies(resources, modules);

        return new ProjectModel
        {
            AspireForm = header,
            Resources = resources,
            Modules = modules,
            Profiles = profiles,
        };
    }

    private static AspireFormHeader BindHeader(JsonObject dom)
    {
        if (dom["aspireform"] is not JsonObject section)
        {
            throw new ConfigValidationException("The configuration is missing the required 'aspireform' section.");
        }

        var version = TryGetValueStruct<int>(section["version"], "aspireform.version")
            ?? throw new ConfigValidationException("'aspireform.version' is required.");
        if (version != SupportedSchemaVersion)
        {
            throw new ConfigValidationException(
                $"Unsupported schema version {version}; this tool supports version {SupportedSchemaVersion}.");
        }

        var project = RequireNonEmptyString(section, "project", "aspireform.project");
        var appHost = RequireNonEmptyString(section, "apphost", "aspireform.apphost");

        return new AspireFormHeader { Version = version, Project = project, AppHost = appHost };
    }

    private static Dictionary<string, ResourceBlock> BindResources(JsonObject dom)
    {
        var result = new Dictionary<string, ResourceBlock>();
        if (dom["resources"] is not JsonObject resources)
        {
            return result;
        }

        foreach (var (name, value) in resources)
        {
            var block = RequireObject(value, $"resources.{name}");
            result[name] = new ResourceBlock
            {
                Name = name,
                Type = RequireNonEmptyString(block, "type", $"resources.{name}.type"),
                Inputs = ExtractInputs(block, ResourceReservedKeys),
            };
        }

        return result;
    }

    private static Dictionary<string, ModuleBlock> BindModules(JsonObject dom)
    {
        var result = new Dictionary<string, ModuleBlock>();
        if (dom["modules"] is not JsonObject modules)
        {
            return result;
        }

        foreach (var (name, value) in modules)
        {
            var block = RequireObject(value, $"modules.{name}");
            List<string> dependsOn;
            if (block["dependsOn"] is not null)
            {
                if (block["dependsOn"] is not JsonArray dependsOnArray)
                {
                    throw new ConfigValidationException(
                        $"'modules.{name}.dependsOn' must be an array.");
                }

                dependsOn = [];
                for (var i = 0; i < dependsOnArray.Count; i++)
                {
                    var elem = TryGetValue<string>(dependsOnArray[i], $"modules.{name}.dependsOn[{i}]");
                    if (string.IsNullOrWhiteSpace(elem))
                    {
                        throw new ConfigValidationException(
                            $"'modules.{name}.dependsOn[{i}]' must be a non-empty string.");
                    }

                    dependsOn.Add(elem);
                }
            }
            else
            {
                dependsOn = [];
            }

            result[name] = new ModuleBlock
            {
                Name = name,
                Type = RequireNonEmptyString(block, "type", $"modules.{name}.type"),
                DependsOn = dependsOn,
                PreventDestroy = TryGetValueStruct<bool>(block["preventDestroy"], $"modules.{name}.preventDestroy") ?? true,
                Inputs = ExtractInputs(block, ModuleReservedKeys),
            };
        }

        return result;
    }

    private static Dictionary<string, JsonObject> BindProfiles(JsonObject dom)
    {
        var result = new Dictionary<string, JsonObject>();
        if (dom["profiles"] is not JsonObject profiles)
        {
            return result;
        }

        foreach (var (name, value) in profiles)
        {
            if (value is JsonObject obj)
            {
                result[name] = (JsonObject)obj.DeepClone();
            }
        }

        return result;
    }

    private static void ValidateDependencies(
        IReadOnlyDictionary<string, ResourceBlock> resources,
        IReadOnlyDictionary<string, ModuleBlock> modules)
    {
        var declared = new HashSet<string>(resources.Keys);
        declared.UnionWith(modules.Keys);

        foreach (var module in modules.Values)
        {
            foreach (var dependency in module.DependsOn)
            {
                if (!declared.Contains(dependency))
                {
                    throw new ConfigValidationException(
                        $"Module '{module.Name}' declares dependsOn '{dependency}', which is not a declared block.");
                }
            }
        }
    }

    private static JsonObject ExtractInputs(JsonObject block, IEnumerable<string> reservedKeys)
    {
        var inputs = (JsonObject)block.DeepClone();
        foreach (var key in reservedKeys)
        {
            inputs.Remove(key);
        }

        return inputs;
    }

    private static JsonObject RequireObject(JsonNode? node, string path) =>
        node as JsonObject
        ?? throw new ConfigValidationException($"'{path}' must be an object.");

    private static string RequireNonEmptyString(JsonObject obj, string key, string path)
    {
        var value = TryGetValue<string>(obj[key], path);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ConfigValidationException($"'{path}' is required and must be a non-empty string.");
        }

        return value;
    }

    /// <summary>
    /// Wraps <see cref="JsonNode.GetValue{T}"/> for reference types and rethrows any
    /// <see cref="InvalidOperationException"/> as a <see cref="ConfigValidationException"/> with the config path.
    /// </summary>
    private static T? TryGetValue<T>(JsonNode? node, string path) where T : class
    {
        if (node is null)
        {
            return default;
        }

        try
        {
            return node.GetValue<T>();
        }
        catch (InvalidOperationException ex)
        {
            throw new ConfigValidationException(
                $"'{path}' could not be read as {typeof(T).Name}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Wraps <see cref="JsonNode.GetValue{T}"/> for value types and rethrows any
    /// <see cref="InvalidOperationException"/> as a <see cref="ConfigValidationException"/> with the config path.
    /// Returns <see langword="null"/> when <paramref name="node"/> is <see langword="null"/>.
    /// </summary>
    private static T? TryGetValueStruct<T>(JsonNode? node, string path) where T : struct
    {
        if (node is null)
        {
            return null;
        }

        try
        {
            return node.GetValue<T>();
        }
        catch (InvalidOperationException ex)
        {
            throw new ConfigValidationException(
                $"'{path}' could not be read as {typeof(T).Name}: {ex.Message}", ex);
        }
    }
}
