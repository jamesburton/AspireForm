using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;
using AspireForm.Providers.EfData;

namespace AspireForm.Providers;

/// <summary>Built-in Module provider for code-first EF Core data access. Reads entity classes from a user csproj via Roslyn, then emits a DbContext and (when entities carry <c>[DabExpose]</c>) a sibling <c>dab-config.json</c>.</summary>
public sealed class EfDataModuleProvider : IProvider
{
    private readonly IEntityCatalogService _catalog;

    /// <summary>Creates the provider with the default Roslyn-backed catalog service.</summary>
    public EfDataModuleProvider() : this(new RoslynEntityCatalogService()) { }

    /// <summary>Creates the provider with a supplied catalog service (used by tests).</summary>
    public EfDataModuleProvider(IEntityCatalogService catalog) { _catalog = catalog; }

    /// <inheritdoc />
    public string Type => "ef-data";

    /// <inheritdoc />
    public BlockKind Kind => BlockKind.Module;

    /// <inheritdoc />
    public ProviderPlan Plan(PlanContext context)
    {
        // Reject the legacy 0.4.0 input shape with a clear migration hint.
        if (context.Inputs["database"] is not null || context.Inputs["contextName"] is not null)
        {
            throw new InvalidOperationException(
                "ef-data: the 'database' and 'contextName' inputs were removed in AspireForm 0.5.0. " +
                "Replace them with 'projectPath' (required) pointing at your entity project's .csproj. " +
                "See AspireForm CHANGELOG [0.5.0] for the migration diff.");
        }

        var projectPath = context.Inputs["projectPath"]?.GetValue<string>()
            ?? throw new InvalidOperationException("ef-data: 'projectPath' input is required (path to entity project's .csproj).");

        var explicitDbContext = context.Inputs["dbContext"]?.GetValue<string>();
        var emitDabExplicit = context.Inputs["emitDabConfig"]?.GetValue<bool>();
        var dabConfigPath = context.Inputs["dabConfigPath"]?.GetValue<string>()
            ?? Path.Combine(context.AppHostDirectory, "dab-config.json");

        var absoluteProject = Path.IsPathRooted(projectPath)
            ? projectPath
            : Path.GetFullPath(Path.Combine(context.AppHostDirectory, projectPath));

        var catalog = _catalog.ScanAsync(absoluteProject, CancellationToken.None).GetAwaiter().GetResult();

        DbContextInfo? targetContext;
        if (explicitDbContext is not null)
        {
            targetContext = catalog.DbContexts.FirstOrDefault(c =>
                string.Equals($"{c.Namespace}.{c.Name}", explicitDbContext, StringComparison.Ordinal)
                || string.Equals(c.Name, explicitDbContext, StringComparison.Ordinal));
            if (targetContext is null)
            {
                throw new InvalidOperationException(
                    $"ef-data: dbContext '{explicitDbContext}' not found in project '{absoluteProject}'.");
            }
        }
        else
        {
            if (catalog.DbContexts.Count > 1)
            {
                throw new InvalidOperationException(
                    $"ef-data: {catalog.DbContexts.Count} DbContext classes found in '{absoluteProject}'. " +
                    "Set 'dbContext' input to disambiguate (e.g., 'Demo.Data.AppDbContext').");
            }
            targetContext = catalog.DbContexts.FirstOrDefault()
                ?? new DbContextInfo("AppDbContext", DefaultNamespaceFromProject(absoluteProject),
                    Path.Combine(Path.GetDirectoryName(absoluteProject)!, "AppDbContext.cs"),
                    []);
        }

        var dbContextFile = targetContext.FilePath.Length > 0
            ? targetContext.FilePath
            : Path.Combine(Path.GetDirectoryName(absoluteProject)!, $"{targetContext.Name}.cs");

        var fileActions = new List<PlannedFileAction>
        {
            new(
                Path: dbContextFile,
                OwnershipMode: OwnershipMode.Managed,
                BlockMarker: context.BlockName,
                RenderContent: () => DbContextEmitter.Render(targetContext.Name, targetContext.Namespace, catalog)),
        };

        var anyDabExposed = catalog.Entities.Any(e =>
            e.Attributes.Any(a => a.FullTypeName == "AspireForm.Annotations.DabExposeAttribute"));
        var shouldEmitDab = emitDabExplicit ?? anyDabExposed;

        if (shouldEmitDab && anyDabExposed)
        {
            var firstDepends = (context.Inputs["dependsOn"] as JsonArray)?
                .Select(n => n?.GetValue<string>())
                .FirstOrDefault(s => !string.IsNullOrEmpty(s))
                ?? "default";

            var diag = new List<CatalogDiagnostic>();
            var dabContent = DabConfigEmitter.Render(catalog, firstDepends!, diag);
            if (dabContent is not null)
            {
                fileActions.Add(new PlannedFileAction(
                    Path: dabConfigPath,
                    OwnershipMode: OwnershipMode.Managed,
                    BlockMarker: context.BlockName,
                    RenderContent: () => dabContent));
            }
        }

        return new ProviderPlan { FileActions = fileActions };
    }

    private static string DefaultNamespaceFromProject(string csprojPath) =>
        Path.GetFileNameWithoutExtension(csprojPath);
}
