namespace AspireForm.Annotations;

/// <summary>Marks a static method as a Minimal API endpoint. The method body is the handler;
/// AspireForm discovers it and emits a <c>MapAspireFormEndpoints()</c> call for it.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ApiEndpointAttribute : Attribute
{
    /// <summary>Initialises the endpoint with the route pattern (e.g. <c>/books/{id}</c>) and HTTP method.</summary>
    /// <param name="route">Route pattern (e.g. <c>/books/{id}</c>).</param>
    /// <param name="method">HTTP method: GET, POST, PUT, PATCH, DELETE. Default is GET.</param>
    public ApiEndpointAttribute(string route, string method = "GET")
    {
        Route = route;
        Method = method;
    }

    /// <summary>Route pattern (e.g. <c>/books/{id}</c>).</summary>
    public string Route { get; }

    /// <summary>HTTP method: GET, POST, PUT, PATCH, DELETE. Default is GET.</summary>
    public string Method { get; }
}
