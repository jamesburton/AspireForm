using AspireForm.EntityCatalog;

namespace AspireForm.ApiCatalog;

/// <summary>Sealed-hierarchy DSL for one endpoint-graph mutation.</summary>
public abstract record EndpointChangeRequest;

/// <summary>Create a new static class + method in a new <c>.cs</c> file (or an existing class).</summary>
public sealed record CreateEndpoint(
    string MethodName,
    string TypeName,
    string Route,
    string HttpMethod,
    string FilePath,
    string Namespace) : EndpointChangeRequest;

/// <summary>Delete the endpoint method (and its <c>[ApiEndpoint]</c> attribute); remove the class if it becomes empty.</summary>
public sealed record DeleteEndpoint(
    string MethodName,
    string? TypeName) : EndpointChangeRequest;

/// <summary>Append a typed parameter to the endpoint method's signature.</summary>
public sealed record AddParameter(
    string MethodName,
    string? TypeName,
    string ParamName,
    string ClrType) : EndpointChangeRequest;

/// <summary>Remove a parameter from the endpoint method's signature.</summary>
public sealed record RemoveParameter(
    string MethodName,
    string? TypeName,
    string ParamName) : EndpointChangeRequest;

/// <summary>Set (replace if present) an attribute on the endpoint method.</summary>
public sealed record SetEndpointAttribute(
    string MethodName,
    string? TypeName,
    AttributeInstance Attribute) : EndpointChangeRequest;

/// <summary>Clear an attribute (by full type name) from the endpoint method.</summary>
public sealed record ClearEndpointAttribute(
    string MethodName,
    string? TypeName,
    string AttributeFullTypeName) : EndpointChangeRequest;

/// <summary>Shorthand for setting <c>[ApiAuth(policy)]</c> on the endpoint method.</summary>
public sealed record SetAuthPolicy(
    string MethodName,
    string? TypeName,
    string Policy) : EndpointChangeRequest;
