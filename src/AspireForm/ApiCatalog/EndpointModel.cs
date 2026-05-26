using AspireForm.EntityCatalog;

namespace AspireForm.ApiCatalog;

/// <summary>Immutable snapshot of the API endpoint graph in a user's project.</summary>
public sealed record EndpointCatalog(
    IReadOnlyList<EndpointInfo> Endpoints,
    IReadOnlyList<CatalogDiagnostic> Diagnostics);

/// <summary>One Minimal API endpoint discovered in the user's Web project.</summary>
public sealed record EndpointInfo(
    string HandlerTypeName,
    string MethodName,
    string Route,
    string HttpMethod,
    string? Summary,
    string? AuthPolicy,
    IReadOnlyList<string> Tags,
    IReadOnlyList<RouteParameter> Parameters,
    IReadOnlyList<AttributeInstance> Attributes,
    string FilePath);

/// <summary>A route parameter extracted from the route pattern (e.g. <c>{id:int}</c>).</summary>
public sealed record RouteParameter(
    string Name,
    string? Constraint,
    bool IsOptional);

/// <summary>Result of an endpoint-mutation operation.</summary>
public sealed record EndpointMutationResult(
    bool Success,
    IReadOnlyList<string> ChangedFiles,
    IReadOnlyList<CatalogDiagnostic> Diagnostics)
{
    /// <summary>Convenience factory for successful mutations.</summary>
    /// <param name="changedFiles">The files that were modified.</param>
    /// <param name="diagnostics">Optional diagnostics to include.</param>
    public static EndpointMutationResult Ok(
        IReadOnlyList<string> changedFiles,
        IReadOnlyList<CatalogDiagnostic>? diagnostics = null) =>
        new(true, changedFiles, diagnostics ?? []);

    /// <summary>Convenience factory for failed mutations.</summary>
    /// <param name="message">Error message describing the failure.</param>
    /// <param name="filePath">Optional file path associated with the failure.</param>
    public static EndpointMutationResult Fail(string message, string? filePath = null) =>
        new(false, [], [new CatalogDiagnostic("error", message, filePath, null)]);
}
