namespace AspireForm.ApiCatalog;

/// <summary>Default <see cref="IEndpointCatalogService"/> backed by Roslyn. Caches one scanner per service instance; mutator is stateless.</summary>
public sealed class RoslynEndpointCatalogService : IEndpointCatalogService, IAsyncDisposable
{
    private readonly RoslynEndpointScanner _scanner = new();
    private readonly RoslynEndpointMutator _mutator = new();

    /// <inheritdoc />
    public Task<EndpointCatalog> ScanAsync(string csprojPath, CancellationToken ct) =>
        _scanner.ScanAsync(csprojPath, ct);

    /// <inheritdoc />
    public Task<EndpointMutationResult> MutateAsync(string csprojPath, EndpointChangeRequest request, CancellationToken ct) =>
        _mutator.ApplyAsync(csprojPath, request, ct);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _scanner.DisposeAsync();
}
