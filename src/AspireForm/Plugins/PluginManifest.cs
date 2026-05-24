using System.Text.Json;
using System.Text.Json.Serialization;

namespace AspireForm.Plugins;

/// <summary>One provider entry inside a plugin manifest.</summary>
/// <param name="Type">The provider's block type (e.g. <c>redis</c>).</param>
/// <param name="Kind">Either <c>resource</c> or <c>module</c>.</param>
/// <param name="ClassName">Fully-qualified type name of the <see cref="Providers.IProvider"/> implementation inside the plugin assembly.</param>
public sealed record PluginManifestProvider(string Type, string Kind, string ClassName);

/// <summary>The contract a plugin package publishes via its embedded <c>aspireform-plugin.json</c>.</summary>
public sealed class PluginManifest
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>The plugin's display name (also used in the lockfile and CLI).</summary>
    public required string Name { get; init; }

    /// <summary>The plugin's package version (informational; the NuGet package version is authoritative).</summary>
    public required string Version { get; init; }

    /// <summary>The minimum AspireForm version this plugin requires (SemVer).</summary>
    public required string MinAspireFormVersion { get; init; }

    /// <summary>The providers this plugin contributes.</summary>
    public required IReadOnlyList<PluginManifestProvider> Providers { get; init; }

    /// <summary>The assembly name to load (without <c>.dll</c>); defaults to <c>AspireForm.Plugin.&lt;Name&gt;</c> when omitted.</summary>
    public string? AssemblyName { get; init; }

    /// <summary>Parses a manifest JSON document. Throws <see cref="PluginContractException"/> on any issue.</summary>
    public static PluginManifest Parse(string json)
    {
        PluginManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<PluginManifest>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new PluginContractException($"Plugin manifest is not valid JSON: {ex.Message}", ex);
        }

        if (manifest is null)
        {
            throw new PluginContractException("Plugin manifest is empty.");
        }

        Validate(manifest);
        return manifest;
    }

    private static void Validate(PluginManifest m)
    {
        if (string.IsNullOrWhiteSpace(m.Name))
        {
            throw new PluginContractException("Plugin manifest is missing the required 'name' field.");
        }

        if (string.IsNullOrWhiteSpace(m.Version))
        {
            throw new PluginContractException("Plugin manifest is missing the required 'version' field.");
        }

        if (string.IsNullOrWhiteSpace(m.MinAspireFormVersion))
        {
            throw new PluginContractException("Plugin manifest is missing the required 'minAspireFormVersion' field.");
        }

        if (m.Providers is null)
        {
            throw new PluginContractException("Plugin manifest is missing the required 'providers' field.");
        }
    }
}
