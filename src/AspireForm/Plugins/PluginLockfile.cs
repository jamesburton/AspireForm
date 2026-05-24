using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AspireForm.Plugins;

/// <summary>One entry in the plugin lockfile.</summary>
public sealed class PluginLockEntry
{
    /// <summary>The plugin's display name (matches the manifest).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The NuGet package id.</summary>
    public string Package { get; set; } = string.Empty;

    /// <summary>The pinned package version.</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>The NuGet feed source the plugin was restored from.</summary>
    public string Source { get; set; } = string.Empty;
}

/// <summary>The persisted set of plugins this project has resolved. Committed to git.</summary>
public sealed class PluginLockfile
{
    /// <summary>The lockfile schema version.</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>The locked plugins, ordered by name.</summary>
    public List<PluginLockEntry> Plugins { get; set; } = [];

    private const string DirName = ".aspireform";
    private const string FileName = "plugins.lock.yaml";

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Loads the lockfile from <paramref name="projectDir"/>, returning an empty instance when no file exists.
    /// </summary>
    /// <param name="projectDir">The root directory of the AspireForm project.</param>
    /// <returns>The deserialized <see cref="PluginLockfile"/>, or a new empty instance if the file is absent.</returns>
    public static PluginLockfile Load(string projectDir)
    {
        var path = Path.Combine(projectDir, DirName, FileName);
        if (!File.Exists(path))
        {
            return new PluginLockfile();
        }

        return Deserializer.Deserialize<PluginLockfile>(File.ReadAllText(path)) ?? new PluginLockfile();
    }

    /// <summary>
    /// Writes <paramref name="lockfile"/> to <c>.aspireform/plugins.lock.yaml</c> under <paramref name="projectDir"/>.
    /// </summary>
    /// <param name="projectDir">The root directory of the AspireForm project.</param>
    /// <param name="lockfile">The lockfile instance to persist.</param>
    public static void Save(string projectDir, PluginLockfile lockfile)
    {
        var lockDir = Path.Combine(projectDir, DirName);
        Directory.CreateDirectory(lockDir);
        File.WriteAllText(Path.Combine(lockDir, FileName), Serializer.Serialize(lockfile));
    }
}
