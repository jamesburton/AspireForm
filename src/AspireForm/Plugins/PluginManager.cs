using AspireForm.Configuration;
using AspireForm.Providers;

namespace AspireForm.Plugins;

/// <summary>
/// Orchestrates plugin discovery, restore, and load. Run between <see cref="ConfigLoader.Load"/> and the
/// planner: produces a <see cref="ProviderRegistry"/> enriched with discovered plugin providers.
/// </summary>
public sealed class PluginManager
{
    private readonly PluginRestorer _restorer;
    private readonly PluginAssemblyLoader _loader;

    /// <summary>Initialises the manager with default restorer and loader implementations.</summary>
    public PluginManager()
    {
        _restorer = new PluginRestorer();
        _loader = new PluginAssemblyLoader();
    }

    /// <summary>
    /// Walks <paramref name="model"/> for block types unknown to the built-in registry; resolves each
    /// against the lockfile (or restores from NuGet if absent); loads and instantiates providers; updates
    /// the lockfile; returns a <see cref="ProviderRegistry"/> combining built-ins with loaded plugins.
    /// </summary>
    /// <param name="model">The parsed project model describing all resource and module blocks.</param>
    /// <param name="projectDir">The root directory of the AspireForm project (where <c>.aspireform/</c> lives).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="ProviderRegistry"/> containing built-in providers plus any loaded plugin providers.</returns>
    public async Task<ProviderRegistry> DiscoverAndLoadAsync(
        ProjectModel model, string projectDir, CancellationToken cancellationToken = default)
    {
        var builtIn = ProviderRegistry.Default();
        var knownTypes = builtIn.AllProviders().Select(p => p.Type).ToHashSet(StringComparer.Ordinal);

        var unknownTypes = model.Resources.Values.Select(r => r.Type)
            .Concat(model.Modules.Values.Select(m => m.Type))
            .Where(t => !knownTypes.Contains(t))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Fast path: all block types are built-in — no lockfile touched.
        if (unknownTypes.Count == 0)
        {
            return builtIn;
        }

        var lockfile = PluginLockfile.Load(projectDir);
        var pluginProviders = new List<IProvider>();
        var lockfileDirty = false;

        foreach (var type in unknownTypes)
        {
            var packageId = ResolvePackageIdFromType(type);
            var existing = lockfile.Plugins.FirstOrDefault(p =>
                p.Package.Equals(packageId, StringComparison.OrdinalIgnoreCase));

            PluginLockEntry entry;
            if (existing is not null)
            {
                entry = existing;
            }
            else
            {
                entry = await ResolveAndLockAsync(type, lockfile, projectDir, cancellationToken);
                lockfileDirty = true;
            }

            var packageDir = Path.Combine(PluginRestorer.GetGlobalPackagesPath(),
                entry.Package.ToLowerInvariant(), entry.Version);

            if (!Directory.Exists(packageDir))
            {
                /* Package is in the lockfile but not in the local NuGet cache — restore it. */
                var result = await _restorer.RestoreAsync(entry.Package, entry.Version, projectDir, cancellationToken);
                if (!result.Success)
                {
                    throw new PluginContractException(
                        $"Plugin '{entry.Name}' ({entry.Package} {entry.Version}) could not be restored: {result.ErrorMessage}");
                }

                packageDir = result.PackageDirectory!;
            }

            var manifestPath = Path.Combine(packageDir, "aspireform-plugin.json");
            if (!File.Exists(manifestPath))
            {
                throw new PluginContractException(
                    $"Plugin '{entry.Name}' is missing 'aspireform-plugin.json' at the package root.");
            }

            var manifest = PluginManifest.Parse(await File.ReadAllTextAsync(manifestPath, cancellationToken));
            CheckContractCompatibility(manifest);
            pluginProviders.AddRange(_loader.LoadProviders(packageDir, manifest));
        }

        if (lockfileDirty)
        {
            PluginLockfile.Save(projectDir, lockfile);
        }

        return ProviderRegistry.Combine(builtIn.AllProviders(), pluginProviders);
    }

    private async Task<PluginLockEntry> ResolveAndLockAsync(
        string type, PluginLockfile lockfile, string projectDir, CancellationToken cancellationToken)
    {
        var packageId = ResolvePackageIdFromType(type);

        /* Use floating "*" so dotnet restore resolves to the latest stable version. */
        var result = await _restorer.RestoreAsync(packageId, "*", projectDir, cancellationToken);
        if (!result.Success)
        {
            throw new PluginContractException(
                $"No plugin found for block type '{type}'. Tried package id '{packageId}': {result.ErrorMessage}");
        }

        // The restored directory's name is the resolved version (e.g. "<globalPackages>/<id>/<version>/").
        var resolvedVersion = new DirectoryInfo(result.PackageDirectory!).Name;
        var displayName = packageId.StartsWith("AspireForm.Plugin.", StringComparison.OrdinalIgnoreCase)
            ? packageId["AspireForm.Plugin.".Length..]
            : packageId;

        var entry = new PluginLockEntry
        {
            Name = displayName,
            Package = packageId,
            Version = resolvedVersion,
        };

        lockfile.Plugins.Add(entry);
        lockfile.Plugins.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return entry;
    }

    /* Convention: type 'foo-bar' maps to package 'AspireForm.Plugin.FooBar' (PascalCase each hyphen-delimited segment). */
    private static string ResolvePackageIdFromType(string type)
    {
        var pascal = string.Concat(
            type.Split('-').Select(p => p.Length > 0 ? char.ToUpperInvariant(p[0]) + p[1..] : p));
        return $"AspireForm.Plugin.{pascal}";
    }

    /// <summary>
    /// Checks that the running AspireForm version satisfies the plugin's <c>minAspireFormVersion</c>
    /// constraint. Throws <see cref="PluginContractException"/> when the version is too old or
    /// when the constraint cannot be parsed.
    /// </summary>
    /// <param name="manifest">The plugin manifest to validate.</param>
    /// <exception cref="PluginContractException">
    /// Thrown when <see cref="PluginManifest.MinAspireFormVersion"/> is not a valid version string,
    /// or when the running AspireForm version is older than the minimum required.
    /// </exception>
    public static void CheckContractCompatibility(PluginManifest manifest)
    {
        var running = typeof(PluginManager).Assembly.GetName().Version
            ?? throw new InvalidOperationException("Cannot determine running AspireForm version.");

        if (!Version.TryParse(manifest.MinAspireFormVersion, out var min))
        {
            throw new PluginContractException(
                $"Plugin '{manifest.Name}' declares an unparseable minAspireFormVersion '{manifest.MinAspireFormVersion}'.");
        }

        // Compare only major.minor to avoid patch-level churn blocking valid plugins.
        var runningMm = new Version(running.Major, running.Minor);
        var minMm = new Version(min.Major, min.Minor);
        if (runningMm < minMm)
        {
            throw new PluginContractException(
                $"Plugin '{manifest.Name}' requires AspireForm >= {manifest.MinAspireFormVersion}; running {running}.");
        }
    }
}
