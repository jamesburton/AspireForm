using System.Text.Json.Nodes;

namespace AspireForm.Configuration;

/// <summary>The canonical, format-agnostic representation of an AspireForm project configuration.</summary>
public sealed class ProjectModel
{
    /// <summary>The top-level <c>aspireform</c> header section.</summary>
    public required AspireFormHeader AspireForm { get; init; }

    /// <summary>Declared infrastructure resources, keyed by block name.</summary>
    public IReadOnlyDictionary<string, ResourceBlock> Resources { get; init; }
        = new Dictionary<string, ResourceBlock>();

    /// <summary>Declared feature-slice modules, keyed by block name.</summary>
    public IReadOnlyDictionary<string, ModuleBlock> Modules { get; init; }
        = new Dictionary<string, ModuleBlock>();

    /// <summary>Reserved profile definitions. Parsed and validated but with no behaviour in v1.</summary>
    public IReadOnlyDictionary<string, JsonObject> Profiles { get; init; }
        = new Dictionary<string, JsonObject>();
}

/// <summary>The <c>aspireform</c> header: schema version and project identity.</summary>
public sealed class AspireFormHeader
{
    /// <summary>The configuration schema version. Only version 1 is supported.</summary>
    public required int Version { get; init; }

    /// <summary>The project name.</summary>
    public required string Project { get; init; }

    /// <summary>Relative path to the Aspire AppHost project.</summary>
    public required string AppHost { get; init; }
}

/// <summary>An infrastructure resource block (managed, destroyable).</summary>
public sealed class ResourceBlock
{
    /// <summary>The block name (its key under <c>resources</c>).</summary>
    public required string Name { get; init; }

    /// <summary>The provider type, e.g. <c>sqlserver</c>.</summary>
    public required string Type { get; init; }

    /// <summary>Provider-specific inputs. Bound and validated by the provider, not the loader.</summary>
    public JsonObject Inputs { get; init; } = new();
}

/// <summary>A feature-slice module block (scaffolds cross-layer code, destroy-protected by default).</summary>
public sealed class ModuleBlock
{
    /// <summary>The block name (its key under <c>modules</c>).</summary>
    public required string Name { get; init; }

    /// <summary>The provider type, e.g. <c>ef-data</c>.</summary>
    public required string Type { get; init; }

    /// <summary>Names of blocks this module depends on.</summary>
    public IReadOnlyList<string> DependsOn { get; init; } = [];

    /// <summary>When true (the default), <c>destroy</c> refuses to remove this module without an explicit override.</summary>
    public bool PreventDestroy { get; init; } = true;

    /// <summary>Provider-specific inputs. Bound and validated by the provider, not the loader.</summary>
    public JsonObject Inputs { get; init; } = new();
}
