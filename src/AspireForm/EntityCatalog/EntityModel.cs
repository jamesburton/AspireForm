namespace AspireForm.EntityCatalog;

/// <summary>Immutable snapshot of the entity graph in a user's project.</summary>
public sealed record EntityCatalog(
    IReadOnlyList<Entity> Entities,
    IReadOnlyList<DbContextInfo> DbContexts,
    IReadOnlyList<CatalogDiagnostic> Diagnostics);

/// <summary>Information about a discovered DbContext-derived class.</summary>
public sealed record DbContextInfo(
    string Name,
    string Namespace,
    string FilePath,
    IReadOnlyList<string> DbSetEntityNames);

/// <summary>One entity class discovered in the user's project.</summary>
public sealed record Entity(
    string Name,
    string Namespace,
    string FilePath,
    IReadOnlyList<Property> Properties,
    IReadOnlyList<Relationship> Relationships,
    IReadOnlyList<AttributeInstance> Attributes);

/// <summary>One declared property on an entity.</summary>
public sealed record Property(
    string Name,
    string ClrType,
    bool IsNullable,
    bool IsPrimaryKey,
    IReadOnlyList<AttributeInstance> Attributes);

/// <summary>One navigation relationship from an entity to another entity.</summary>
public sealed record Relationship(
    string Name,
    string TargetEntity,
    RelationshipCardinality Cardinality,
    string? ForeignKeyProperty);

/// <summary>Cardinality of a navigation relationship.</summary>
public enum RelationshipCardinality { OneToOne, OneToMany, ManyToOne, ManyToMany }

/// <summary>One attribute applied to an entity class or property.</summary>
public sealed record AttributeInstance(
    string FullTypeName,
    IReadOnlyList<object?> ConstructorArgs,
    IReadOnlyDictionary<string, object?> NamedArgs);

/// <summary>A diagnostic emitted during catalog scan or mutation.</summary>
public sealed record CatalogDiagnostic(
    string Severity,
    string Message,
    string? FilePath,
    int? Line);

/// <summary>Result of an entity-mutation operation.</summary>
public sealed record MutationResult(
    bool Success,
    IReadOnlyList<string> ChangedFiles,
    IReadOnlyList<CatalogDiagnostic> Diagnostics)
{
    /// <summary>Convenience for successful mutations.</summary>
    public static MutationResult Ok(IReadOnlyList<string> changedFiles, IReadOnlyList<CatalogDiagnostic>? diagnostics = null) =>
        new(true, changedFiles, diagnostics ?? []);

    /// <summary>Convenience for failed mutations.</summary>
    public static MutationResult Fail(string message, string? filePath = null) =>
        new(false, [], [new CatalogDiagnostic("error", message, filePath, null)]);
}
