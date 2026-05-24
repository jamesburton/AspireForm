using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using AspireForm.Providers;

namespace AspireForm.Plugins;

/// <summary>Loads a plugin assembly into an isolated <see cref="AssemblyLoadContext"/> and instantiates its declared providers.</summary>
public sealed class PluginAssemblyLoader
{
    private static readonly AssemblyLoadContext Context = new("AspireFormPlugins", isCollectible: false);

    /* Keyed on the plugin's assembly path; used by the Resolving handler for .deps.json-aware resolution. */
    private static readonly ConcurrentDictionary<string, AssemblyDependencyResolver> _resolvers = new();

    /* Directories of every plugin assembly that has been loaded; used as filename-match fallback. */
    private static readonly ConcurrentBag<string> _pluginDirectories = [];

    /* Guards one-time attachment of the Resolving handler to Context. 0 = not attached, 1 = attached. */
    private static int _handlerAttached;

    /// <summary>
    /// Loads the plugin assembly from <paramref name="packageDirectory"/> (a NuGet cache directory containing
    /// <c>lib/&lt;tfm&gt;/&lt;assembly&gt;.dll</c>) and returns instances of the providers declared in
    /// <paramref name="manifest"/>.
    /// </summary>
    /// <param name="packageDirectory">The package directory containing <c>lib/&lt;tfm&gt;/</c> and the manifest.</param>
    /// <param name="manifest">The parsed plugin manifest describing which assembly and providers to load.</param>
    /// <returns>A list of instantiated <see cref="IProvider"/> implementations, one per manifest entry.</returns>
    /// <exception cref="PluginContractException">
    /// Thrown when the assembly cannot be located or loaded, or when a declared provider class is missing or
    /// does not implement <see cref="IProvider"/>.
    /// </exception>
    public IReadOnlyList<IProvider> LoadProviders(string packageDirectory, PluginManifest manifest)
    {
        var assemblyName = manifest.AssemblyName ?? $"AspireForm.Plugin.{manifest.Name}";
        var assemblyPath = LocateAssembly(packageDirectory, assemblyName)
            ?? throw new PluginContractException(
                $"Plugin '{manifest.Name}': could not locate '{assemblyName}.dll' under '{packageDirectory}/lib/'.");

        EnsureResolvingHandlerAttached();

        // Register an AssemblyDependencyResolver for this plugin (uses <assembly>.deps.json when present).
        _resolvers.TryAdd(assemblyPath, new AssemblyDependencyResolver(assemblyPath));
        _pluginDirectories.Add(Path.GetDirectoryName(assemblyPath)!);

        Assembly assembly;
        try
        {
            assembly = Context.LoadFromAssemblyPath(assemblyPath);
        }
        catch (Exception ex)
        {
            throw new PluginContractException(
                $"Plugin '{manifest.Name}': failed to load assembly '{assemblyPath}': {ex.Message}", ex);
        }

        var providers = new List<IProvider>(manifest.Providers.Count);
        foreach (var entry in manifest.Providers)
        {
            var type = assembly.GetType(entry.ClassName, throwOnError: false);
            if (type is null)
            {
                throw new PluginContractException(
                    $"Plugin '{manifest.Name}': declared provider class '{entry.ClassName}' was not found in '{assemblyName}'.");
            }

            if (!typeof(IProvider).IsAssignableFrom(type))
            {
                throw new PluginContractException(
                    $"Plugin '{manifest.Name}': class '{entry.ClassName}' does not implement IProvider.");
            }

            var instance = (IProvider)(Activator.CreateInstance(type)
                ?? throw new PluginContractException(
                    $"Plugin '{manifest.Name}': failed to instantiate '{entry.ClassName}'."));
            providers.Add(instance);
        }

        return providers;
    }

    /* Attaches the Resolving handler to Context exactly once per process. */
    private static void EnsureResolvingHandlerAttached()
    {
        if (Interlocked.CompareExchange(ref _handlerAttached, 1, 0) == 0)
        {
            Context.Resolving += ResolveFromAnyPlugin;
        }
    }

    /* Resolving handler: tries each plugin's AssemblyDependencyResolver first (honours .deps.json),
       then falls back to a filename-match across all registered plugin directories. */
    private static Assembly? ResolveFromAnyPlugin(AssemblyLoadContext ctx, AssemblyName name)
    {
        // 1. .deps.json-aware resolution via registered resolvers.
        foreach (var (_, resolver) in _resolvers)
        {
            var path = resolver.ResolveAssemblyToPath(name);
            if (path is not null && File.Exists(path))
            {
                return ctx.LoadFromAssemblyPath(path);
            }
        }

        // 2. Filename-match fallback: look for <assemblyName>.dll in any plugin directory.
        //    Covers synthesised test plugins (no .deps.json) and edge-case package layouts.
        if (name.Name is null)
        {
            return null;
        }

        var fileName = $"{name.Name}.dll";
        foreach (var dir in _pluginDirectories)
        {
            var candidate = Path.Combine(dir, fileName);
            if (File.Exists(candidate))
            {
                return ctx.LoadFromAssemblyPath(candidate);
            }
        }

        return null;
    }

    /* Walks lib/<tfm>/<assembly>.dll, preferring net10.0 but falling back to any TFM found. */
    private static string? LocateAssembly(string packageDirectory, string assemblyName)
    {
        var libDir = Path.Combine(packageDirectory, "lib");
        if (!Directory.Exists(libDir))
        {
            return null;
        }

        var fileName = $"{assemblyName}.dll";
        var preferred = Path.Combine(libDir, "net10.0", fileName);
        if (File.Exists(preferred))
        {
            return preferred;
        }

        return Directory.GetFiles(libDir, fileName, SearchOption.AllDirectories).FirstOrDefault();
    }
}
