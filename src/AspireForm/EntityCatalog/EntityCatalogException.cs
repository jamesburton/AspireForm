namespace AspireForm.EntityCatalog;

/// <summary>Raised by the entity catalog scanner or mutator when an operation cannot be completed cleanly.</summary>
public sealed class EntityCatalogException : Exception
{
    /// <summary>Initialises the exception with a message.</summary>
    public EntityCatalogException(string message) : base(message) { }

    /// <summary>Initialises the exception with a message and inner exception.</summary>
    public EntityCatalogException(string message, Exception inner) : base(message, inner) { }
}
