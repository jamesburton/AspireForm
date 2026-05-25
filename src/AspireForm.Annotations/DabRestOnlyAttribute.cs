namespace AspireForm.Annotations;

/// <summary>When applied alongside <see cref="DabExposeAttribute"/>, restricts exposure to REST (no GraphQL).</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DabRestOnlyAttribute : Attribute { }
