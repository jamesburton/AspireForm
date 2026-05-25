namespace AspireForm.Annotations;

/// <summary>Overrides the DAB REST path for an entity. Default is <c>/{EntityName}</c>.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DabPathAttribute : Attribute
{
    /// <summary>Initialises the attribute with the REST path (e.g. <c>/books</c>).</summary>
    public DabPathAttribute(string path) { Path = path; }

    /// <summary>The REST path.</summary>
    public string Path { get; }
}
