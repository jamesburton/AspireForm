using AspireForm.Providers.ApiEndpoints;

namespace AspireForm.Providers;

/// <summary>Raised when a config block references a provider type that is not registered.</summary>
public sealed class ProviderNotFoundException : Exception
{
    /// <summary>Initialises the exception with a message naming the missing type.</summary>
    public ProviderNotFoundException(string type)
        : base($"No provider is registered for block type '{type}'.") { }
}

/// <summary>Resolves a block's <c>type</c> to its <see cref="IProvider"/>.</summary>
public sealed class ProviderRegistry
{
    private readonly Dictionary<string, IProvider> _byType;

    /// <summary>Creates a registry from an explicit list of providers. Throws on duplicate types.</summary>
    public ProviderRegistry(IEnumerable<IProvider> providers)
    {
        _byType = new(StringComparer.Ordinal);
        foreach (var provider in providers)
        {
            if (!_byType.TryAdd(provider.Type, provider))
            {
                throw new ArgumentException(
                    $"Duplicate provider registration for type '{provider.Type}'.",
                    nameof(providers));
            }
        }
    }

    /// <summary>Returns the registry containing the v1 built-in providers (<c>sqlserver</c>, <c>ef-data</c>, and <c>api-endpoints</c>).</summary>
    public static ProviderRegistry Default() =>
        new([new SqlServerResourceProvider(), new EfDataModuleProvider(), new ApiEndpointsModuleProvider()]);

    /// <summary>Returns the provider for <paramref name="type"/>, or throws <see cref="ProviderNotFoundException"/>.</summary>
    public IProvider Get(string type) =>
        _byType.TryGetValue(type, out var provider)
            ? provider
            : throw new ProviderNotFoundException(type);

    /// <summary>Returns every provider registered with this registry.</summary>
    public IEnumerable<IProvider> AllProviders() => _byType.Values;

    /// <summary>Builds a new registry that contains every provider from each source. Throws on duplicate types.</summary>
    public static ProviderRegistry Combine(params IEnumerable<IProvider>[] sources) =>
        new(sources.SelectMany(s => s));
}
