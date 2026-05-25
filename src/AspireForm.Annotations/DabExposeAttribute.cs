namespace AspireForm.Annotations;

/// <summary>Marks an entity as exposed via Data API Builder. Default: REST + GraphQL, anonymous read.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DabExposeAttribute : Attribute { }
