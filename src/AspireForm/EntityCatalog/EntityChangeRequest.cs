namespace AspireForm.EntityCatalog;

/// <summary>Sealed-hierarchy DSL for one entity-graph mutation.</summary>
public abstract record EntityChangeRequest;

/// <summary>Create a new entity class in a new <c>.cs</c> file.</summary>
public sealed record CreateEntity(string Name, string Namespace, string FilePath) : EntityChangeRequest;

/// <summary>Delete an entity class and remove it from the DbContext + dependent navigations.</summary>
public sealed record DeleteEntity(string EntityName) : EntityChangeRequest;

/// <summary>Append a new property to an entity's class body.</summary>
public sealed record AddProperty(string EntityName, Property Property) : EntityChangeRequest;

/// <summary>Remove an existing property from an entity.</summary>
public sealed record RemoveProperty(string EntityName, string PropertyName) : EntityChangeRequest;

/// <summary>Rename a property; semantic-safe across the whole workspace.</summary>
public sealed record RenameProperty(string EntityName, string OldName, string NewName) : EntityChangeRequest;

/// <summary>Set (replace if present) an attribute on an entity class or one of its properties.</summary>
public sealed record SetAttribute(string EntityName, string? PropertyName, AttributeInstance Attribute) : EntityChangeRequest;

/// <summary>Clear an attribute (by full type name) from an entity class or one of its properties.</summary>
public sealed record ClearAttribute(string EntityName, string? PropertyName, string AttributeFullTypeName) : EntityChangeRequest;

/// <summary>Add a relationship between two entities. v1 supports OneToOne, OneToMany, ManyToOne; ManyToMany is reserved for #4a.1.</summary>
public sealed record AddRelationship(
    string FromEntity, string ToEntity,
    RelationshipCardinality Cardinality,
    string? ForeignKeyProperty) : EntityChangeRequest;

/// <summary>Remove a relationship (by navigation property name) from the originating entity, including its reverse side and any FK property.</summary>
public sealed record RemoveRelationship(string FromEntity, string RelationshipName) : EntityChangeRequest;
