namespace AspireForm.Annotations;

/// <summary>Declares a DAB permission for an entity. Repeatable; one instance per role.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class DabPermissionAttribute : Attribute
{
    /// <summary>Initialises the permission for <paramref name="role"/> with comma-separated <paramref name="actions"/>.</summary>
    public DabPermissionAttribute(string role, string actions)
    {
        Role = role;
        Actions = actions;
    }

    /// <summary>Role name. Use <c>"anonymous"</c>, <c>"authenticated"</c>, or a custom role.</summary>
    public string Role { get; }

    /// <summary>Comma-separated action list (e.g. <c>"read"</c>, <c>"create,update,delete"</c>, or <c>"*"</c>).</summary>
    public string Actions { get; }
}
