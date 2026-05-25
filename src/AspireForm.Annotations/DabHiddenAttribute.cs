namespace AspireForm.Annotations;

/// <summary>Marks an entity as present in EF but explicitly hidden from DAB. Overrides <see cref="DabExposeAttribute"/> if both are present.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DabHiddenAttribute : Attribute { }
