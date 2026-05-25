namespace AspireForm.EntityCatalog;

/// <summary>Default <see cref="IEntityCatalogService"/> backed by Roslyn. Caches one scanner per service instance; mutator is stateless.</summary>
public sealed class RoslynEntityCatalogService : IEntityCatalogService, IAsyncDisposable
{
    private readonly RoslynEntityScanner _scanner = new();
    private readonly RoslynEntityMutator _mutator = new();

    /// <inheritdoc />
    public Task<EntityCatalog> ScanAsync(string csprojPath, CancellationToken ct) =>
        _scanner.ScanAsync(csprojPath, ct);

    /// <inheritdoc />
    public Task<MutationResult> MutateAsync(string csprojPath, EntityChangeRequest request, CancellationToken ct) =>
        _mutator.ApplyAsync(csprojPath, request, ct);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _scanner.DisposeAsync();
}
