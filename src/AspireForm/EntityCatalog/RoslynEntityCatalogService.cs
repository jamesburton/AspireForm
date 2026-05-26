namespace AspireForm.EntityCatalog;

/// <summary>Default <see cref="IEntityCatalogService"/> backed by Roslyn. Caches one scanner per service instance and the last scanned snapshot per csproj path. Mutator is stateless.</summary>
public sealed class RoslynEntityCatalogService : IEntityCatalogService, IAsyncDisposable
{
    private readonly RoslynEntityScanner _scanner = new();
    private readonly RoslynEntityMutator _mutator = new();
    private readonly Dictionary<string, EntityCatalog> _snapshots = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _snapshotLock = new();

    /// <inheritdoc />
    public async Task<EntityCatalog> ScanAsync(string csprojPath, CancellationToken ct)
    {
        var snapshot = await _scanner.ScanAsync(csprojPath, ct);
        var absolute = Path.GetFullPath(csprojPath);
        lock (_snapshotLock) { _snapshots[absolute] = snapshot; }
        return snapshot;
    }

    /// <summary>Returns the most recently scanned snapshot for <paramref name="csprojPath"/>, or null if no scan has run in this session. Used by pages (e.g. <c>/diagnostics</c>) that want the latest state without triggering a re-scan.</summary>
    public EntityCatalog? GetLastSnapshot(string csprojPath)
    {
        var absolute = Path.GetFullPath(csprojPath);
        lock (_snapshotLock) { return _snapshots.GetValueOrDefault(absolute); }
    }

    /// <inheritdoc />
    public async Task<MutationResult> MutateAsync(string csprojPath, EntityChangeRequest request, CancellationToken ct)
    {
        var result = await _mutator.ApplyAsync(csprojPath, request, ct);
        // Invalidate cached snapshot for this project — the next ScanAsync will re-scan.
        if (result.Success)
        {
            var absolute = Path.GetFullPath(csprojPath);
            lock (_snapshotLock) { _snapshots.Remove(absolute); }
        }
        return result;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _scanner.DisposeAsync();
}
