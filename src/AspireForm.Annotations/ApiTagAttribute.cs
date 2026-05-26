namespace AspireForm.Annotations;

/// <summary>Assigns one or more OpenAPI tags to an endpoint for grouping in generated documentation. Repeatable.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class ApiTagAttribute : Attribute
{
    /// <summary>Initialises the attribute with a tag name.</summary>
    /// <param name="tag">The OpenAPI tag to apply to the endpoint.</param>
    public ApiTagAttribute(string tag) { Tag = tag; }

    /// <summary>The OpenAPI tag name.</summary>
    public string Tag { get; }
}
