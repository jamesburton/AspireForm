using System.Reflection;
using System.Runtime.Loader;
using AspireForm.Providers;

namespace AspireForm.Plugins;

/// <summary>Loads a plugin assembly into an isolated <see cref="AssemblyLoadContext"/> and instantiates its declared providers.</summary>
public sealed class PluginAssemblyLoader
{
    private static readonly AssemblyLoadContext Context = new("AspireFormPlugins", isCollectible: false);

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
