using AspireForm.ApiCatalog;

namespace AspireForm.Providers.ApiEndpoints;

/// <summary>Built-in Module provider for code-first Minimal API endpoint registration.
/// Reads <c>[ApiEndpoint]</c>-decorated methods from a Web project via Roslyn, then emits a managed <c>_Endpoints.g.cs</c>.</summary>
public sealed class ApiEndpointsModuleProvider : IProvider
{
    private readonly IEndpointCatalogService? _catalog;

    /// <summary>Creates the provider with the default Roslyn-backed catalog service.</summary>
    public ApiEndpointsModuleProvider() { }

    /// <summary>Creates the provider with a supplied catalog service (used by tests).</summary>
    /// <param name="catalog">The endpoint catalog service to use instead of the default Roslyn-backed one.</param>
    public ApiEndpointsModuleProvider(IEndpointCatalogService catalog) { _catalog = catalog; }

    /// <inheritdoc />
    public string Type => "api-endpoints";

    /// <inheritdoc />
    public BlockKind Kind => BlockKind.Module;

    /// <inheritdoc />
    public ProviderPlan Plan(PlanContext context)
    {
        var projectPath = context.Inputs["projectPath"]?.GetValue<string>()
            ?? throw new InvalidOperationException(
                "api-endpoints: 'projectPath' input is required (path to Web project's .csproj).");

        var absoluteProject = Path.IsPathRooted(projectPath)
            ? projectPath
            : Path.GetFullPath(Path.Combine(context.AppHostDirectory, projectPath));

        if (!File.Exists(absoluteProject))
        {
            throw new InvalidOperationException(
                $"api-endpoints: project file not found at '{absoluteProject}'.");
        }

        // Resolve output path — default is Generated/_Endpoints.g.cs next to the csproj.
        var outputPathInput = context.Inputs["outputPath"]?.GetValue<string>();
        var projectDir = Path.GetDirectoryName(absoluteProject)!;
        var absoluteOutput = outputPathInput is not null
            ? (Path.IsPathRooted(outputPathInput) ? outputPathInput : Path.GetFullPath(Path.Combine(context.AppHostDirectory, outputPathInput)))
            : Path.Combine(projectDir, "Generated", "_Endpoints.g.cs");

        // Infer root namespace from the csproj file name (reliable fall-back; MSBuild property read would require loading the workspace first).
        var rootNamespace = Path.GetFileNameWithoutExtension(absoluteProject);

        // Snapshot catalog at plan time — fresh scan per plan invocation (no caching across plan calls).
        var svc = _catalog ?? new RoslynEndpointCatalogService();
        EndpointCatalog catalog;
        try
        {
            catalog = svc.ScanAsync(absoluteProject, CancellationToken.None).GetAwaiter().GetResult();
        }
        finally
        {
            if (_catalog is null && svc is IAsyncDisposable d)
                d.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        // Surface plan-time diagnostics.
        var planDiagnostics = new List<string>();
        if (catalog.Endpoints.Count == 0)
        {
            planDiagnostics.Add($"api-endpoints: no [ApiEndpoint]-decorated methods found in '{absoluteProject}'. Emitting empty MapAspireFormEndpoints body.");
        }
        foreach (var diag in catalog.Diagnostics.Where(d => d.Severity == "warning"))
        {
            planDiagnostics.Add($"api-endpoints: {diag.Message}");
        }

        // Emit the file action — always emits the file even with zero endpoints.
        var capturedCatalog = catalog;
        var capturedNamespace = rootNamespace;
        var fileAction = new PlannedFileAction(
            Path: absoluteOutput,
            OwnershipMode: OwnershipMode.Managed,
            BlockMarker: context.BlockName,
            RenderContent: () => EndpointEmitter.Render(capturedCatalog, capturedNamespace));

        return new ProviderPlan { FileActions = [fileAction] };
    }
}
