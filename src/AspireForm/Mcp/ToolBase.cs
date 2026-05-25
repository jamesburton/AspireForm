using System.Text.Json.Nodes;
using AspireForm.Configuration;
using AspireForm.Planning;
using AspireForm.Plugins;
using AspireForm.Providers;
using AspireForm.State;

namespace AspireForm.Mcp;

/// <summary>Shared helpers for tool handlers: project-dir resolution, input-schema building, and exception → ToolResult mapping.</summary>
internal static class ToolBase
{
    /// <summary>Resolves the project directory from the tool args, falling back to the supplied default.</summary>
    /// <param name="args">Tool arguments.</param>
    /// <param name="defaultDir">Default project dir, supplied by the server (usually the value from <c>--project-dir</c>).</param>
    /// <returns>An absolute, normalised path.</returns>
    public static string ResolveProjectDir(JsonObject args, string defaultDir)
    {
        var supplied = args["projectDir"]?.GetValue<string>();
        var dir = string.IsNullOrWhiteSpace(supplied) ? defaultDir : supplied;
        return Path.GetFullPath(dir);
    }

    /// <summary>Catches the common AspireForm exception set and converts to a tool-level failure result.</summary>
    /// <param name="action">The body to execute.</param>
    /// <returns>The result, or a tool-level failure result when a known exception is thrown.</returns>
    public static async Task<ToolResult> CatchKnownAsync(Func<Task<ToolResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ConfigValidationException ex) { return ToolResult.Fail($"Configuration error: {ex.Message}"); }
        catch (DependencyCycleException ex)   { return ToolResult.Fail($"Plan error: {ex.Message}"); }
        catch (ProviderNotFoundException ex)  { return ToolResult.Fail($"Plan error: {ex.Message}"); }
        catch (PluginContractException ex)    { return ToolResult.Fail($"Plugin error: {ex.Message}"); }
        catch (StateException ex)             { return ToolResult.Fail($"State error: {ex.Message}"); }
    }

    /// <summary>Builds a JSON-Schema "object" schema with the supplied properties and required keys.</summary>
    /// <param name="properties">Property name → schema object map.</param>
    /// <param name="required">Optional list of required property names.</param>
    public static JsonObject ObjectSchema(IDictionary<string, JsonObject> properties, params string[] required)
    {
        var props = new JsonObject();
        foreach (var (k, v) in properties)
        {
            props[k] = v.DeepClone();
        }

        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = props,
        };

        if (required.Length > 0)
        {
            schema["required"] = new JsonArray(required.Select(r => (JsonNode)JsonValue.Create(r)!).ToArray());
        }

        return schema;
    }

    /// <summary>JSON-Schema primitive: string with optional description.</summary>
    public static JsonObject Str(string? description = null) =>
        description is null ? new JsonObject { ["type"] = "string" } :
                              new JsonObject { ["type"] = "string", ["description"] = description };

    /// <summary>JSON-Schema primitive: boolean with optional description.</summary>
    public static JsonObject Bool(string? description = null) =>
        description is null ? new JsonObject { ["type"] = "boolean" } :
                              new JsonObject { ["type"] = "boolean", ["description"] = description };

    /// <summary>JSON-Schema primitive: array of strings.</summary>
    public static JsonObject StrArray(string? description = null) =>
        description is null ? new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } } :
                              new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" }, ["description"] = description };
}
