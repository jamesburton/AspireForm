namespace AspireForm.EntityCatalog;

/// <summary>The single DI seam over the entity catalog. Used by Blazor pages, MCP tools, and the <c>ef-data</c> provider.</summary>
public interface IEntityCatalogService
{
    /// <summary>Scans the supplied csproj and returns an immutable <see cref="EntityCatalog"/> snapshot.</summary>
    /// <param name="csprojPath">Absolute or relative path to the entity project's csproj file.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<EntityCatalog> ScanAsync(string csprojPath, CancellationToken ct);

    /// <summary>Applies one <see cref="EntityChangeRequest"/> transactionally. Returns success + changed files.</summary>
    /// <param name="csprojPath">Absolute or relative path to the entity project's csproj file.</param>
    /// <param name="request">The mutation to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<MutationResult> MutateAsync(string csprojPath, EntityChangeRequest request, CancellationToken ct);
}
