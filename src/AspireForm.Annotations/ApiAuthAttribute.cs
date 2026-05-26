namespace AspireForm.Annotations;

/// <summary>Declares the authorization policy for an <see cref="ApiEndpointAttribute"/>-decorated method.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ApiAuthAttribute : Attribute
{
    /// <summary>Initialises with a policy name. Use <c>"anonymous"</c> to allow unauthenticated access.</summary>
    /// <param name="policy">The authorization policy name, or <c>"anonymous"</c> to allow unauthenticated access.</param>
    public ApiAuthAttribute(string policy) { Policy = policy; }

    /// <summary>The authorization policy name. Use <c>"anonymous"</c> to allow unauthenticated access.</summary>
    public string Policy { get; }
}
