using System.Text.Json.Nodes;

namespace AspireForm.Providers;

/// <summary>Whether a block describes infrastructure (Resource) or a feature-slice (Module).</summary>
public enum BlockKind
{
    /// <summary>Infrastructure (e.g. SQL Server); managed, destroyable.</summary>
    Resource,

    /// <summary>Feature slice that scaffolds cross-layer code; destroy-protected by default.</summary>
    Module,
}

/// <summary>How AspireForm owns a generated file across re-applies.</summary>
public enum OwnershipMode
{
    /// <summary>Re-rendered every apply via structured/marker-region edits.</summary>
    Managed,

    /// <summary>Generated once; never re-touched (developer owns subsequent edits).</summary>
    Scaffold,

    /// <summary>3-way merge: state baseline vs on-disk vs newly-rendered.</summary>
    Merge,
}

/// <summary>One file that a provider intends to write or update.</summary>
/// <param name="Path">Repo-relative target path.</param>
/// <param name="OwnershipMode">How re-applies should treat the file.</param>
/// <param name="BlockMarker">The marker name used inside the file for Managed regions (e.g. <c>sql</c>); ignored for other modes.</param>
/// <param name="RenderContent">Produces the full rendered file content (or, for Managed regions, the content that belongs <em>inside</em> the marker region).</param>
public sealed record PlannedFileAction(
    string Path,
    OwnershipMode OwnershipMode,
    string BlockMarker,
    Func<string> RenderContent);

/// <summary>One CLI invocation a provider intends to make (e.g. <c>aspire add sqlserver</c>).</summary>
/// <param name="Tool">The executable name (e.g. <c>aspire</c>, <c>dotnet</c>).</param>
/// <param name="Args">The arguments to pass.</param>
public sealed record PlannedCliAction(string Tool, IReadOnlyList<string> Args);

/// <summary>A provider's description of what it would do for a single block. Pure data; no I/O.</summary>
public sealed class ProviderPlan
{
    /// <summary>File-level intents.</summary>
    public IReadOnlyList<PlannedFileAction> FileActions { get; init; } = [];

    /// <summary>CLI invocation intents.</summary>
    public IReadOnlyList<PlannedCliAction> CliActions { get; init; } = [];
}

/// <summary>Inputs passed to <see cref="IProvider.Plan(PlanContext)"/>.</summary>
/// <param name="BlockName">The block's name in the config (e.g. <c>sql</c>).</param>
/// <param name="Inputs">Provider-specific inputs from the config.</param>
/// <param name="AppHostDirectory">Repo-relative path to the AppHost project directory.</param>
/// <param name="ProjectName">The project name from the <c>aspireform</c> header.</param>
public sealed record PlanContext(
    string BlockName,
    JsonObject Inputs,
    string AppHostDirectory,
    string ProjectName);

/// <summary>A built-in or plug-in provider for one Resource or Module type.</summary>
public interface IProvider
{
    /// <summary>The block type this provider handles (e.g. <c>sqlserver</c>).</summary>
    string Type { get; }

    /// <summary>Whether this is a Resource or a Module.</summary>
    BlockKind Kind { get; }

    /// <summary>Describes what this provider would do for the given block. Pure; no I/O.</summary>
    ProviderPlan Plan(PlanContext context);
}
