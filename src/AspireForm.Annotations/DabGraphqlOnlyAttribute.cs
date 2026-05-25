namespace AspireForm.Annotations;

/// <summary>When applied alongside <see cref="DabExposeAttribute"/>, restricts exposure to GraphQL (no REST).</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DabGraphqlOnlyAttribute : Attribute { }
