using System.Text.Json.Nodes;

namespace AspireForm.Providers;

/// <summary>Built-in Module provider for EF Core data access. v1 scaffolds a DbContext and records the dependency in a managed AppHost region.</summary>
public sealed class EfDataModuleProvider : IProvider
{
    /// <inheritdoc />
    public string Type => "ef-data";

    /// <inheritdoc />
    public BlockKind Kind => BlockKind.Module;

    /// <inheritdoc />
    public ProviderPlan Plan(PlanContext context)
    {
        var database = context.Inputs["database"]?.GetValue<string>() ?? "appdb";
        var contextName = context.Inputs["contextName"]?.GetValue<string>() ?? "AppDbContext";

        var contextFile = Path.Combine(context.AppHostDirectory, "Data", $"{contextName}.cs");
        var appHostFile = Path.Combine(context.AppHostDirectory, "AppHost.cs");

        return new ProviderPlan
        {
            FileActions =
            [
                new PlannedFileAction(
                    Path: contextFile,
                    OwnershipMode: OwnershipMode.Scaffold,
                    BlockMarker: context.BlockName,
                    RenderContent: () => RenderDbContext(contextName, context.ProjectName)),

                new PlannedFileAction(
                    Path: appHostFile,
                    OwnershipMode: OwnershipMode.Managed,
                    BlockMarker: context.BlockName,
                    RenderContent: () => RenderAppHostRegion(database, contextName)),
            ],
        };
    }

    private static string RenderDbContext(string contextName, string projectName) => $$"""
        using Microsoft.EntityFrameworkCore;

        namespace {{projectName}}.AppHost.Data;

        /// <summary>EF Core DbContext scaffolded by AspireForm (ef-data module). Add DbSet&lt;T&gt; properties as your model grows.</summary>
        public class {{contextName}} : DbContext
        {
            /// <summary>Initialises the context with the runtime-injected options.</summary>
            public {{contextName}}(DbContextOptions<{{contextName}}> options) : base(options) { }
        }
        """;

    private static string RenderAppHostRegion(string database, string contextName) => $"""
        // ef-data module: {contextName} bound to database '{database}'.
        // Wire your service project here (e.g. .WithReference({database})).
        """;
}
