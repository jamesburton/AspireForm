namespace AspireForm.ApiCatalog;

/// <summary>Raised by the endpoint catalog scanner or mutator when an operation cannot be completed cleanly.</summary>
public sealed class EndpointCatalogException : Exception
{
    /// <summary>Initialises the exception with a message.</summary>
    /// <param name="message">A message describing the failure.</param>
    public EndpointCatalogException(string message) : base(message) { }

    /// <summary>Initialises the exception with a message and an inner exception.</summary>
    /// <param name="message">A message describing the failure.</param>
    /// <param name="inner">The exception that caused this failure.</param>
    public EndpointCatalogException(string message, Exception inner) : base(message, inner) { }
}
