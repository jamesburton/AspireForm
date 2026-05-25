namespace AspireForm.Annotations;

/// <summary>Specifies the EF Core delete behavior for a relationship navigation property.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class OnDeleteAttribute : Attribute
{
    /// <summary>Initialises the attribute with the requested behavior.</summary>
    /// <param name="behavior">One of: <c>Cascade</c>, <c>Restrict</c>, <c>SetNull</c>, <c>NoAction</c>, <c>ClientCascade</c>, <c>ClientSetNull</c>.</param>
    public OnDeleteAttribute(string behavior) { Behavior = behavior; }

    /// <summary>The configured delete behavior.</summary>
    public string Behavior { get; }
}
