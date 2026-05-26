namespace AspireForm.Annotations;

/// <summary>Provides a human-readable summary for the endpoint, emitted as an OpenAPI operation summary.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ApiSummaryAttribute : Attribute
{
    /// <summary>Initialises the attribute with a summary string.</summary>
    /// <param name="summary">The OpenAPI operation summary for the endpoint.</param>
    public ApiSummaryAttribute(string summary) { Summary = summary; }

    /// <summary>The OpenAPI operation summary.</summary>
    public string Summary { get; }
}
