using System.Text;
using System.Text.Json.Nodes;
using AspireForm.Providers;

namespace AspireForm.Plugin.DAB;

/// <summary>External Resource provider for Microsoft Data API Builder (REST/GraphQL over a database). Delegates package add to <c>aspire add dab</c>; owns the AppHost resource declaration in a managed region and scaffolds a minimal <c>dab-config.json</c>.</summary>
public sealed class DabResourceProvider : IProvider
{
    /// <inheritdoc />
    public string Type => "dab";

    /// <inheritdoc />
    public BlockKind Kind => BlockKind.Resource;

    /// <inheritdoc />
    public ProviderPlan Plan(PlanContext context)
    {
        var aspireName = context.Inputs["aspireName"]?.GetValue<string>() ?? context.BlockName;
        var databaseRef = context.Inputs["databaseReference"]?.GetValue<string>();

        var appHostFile = Path.Combine(context.AppHostDirectory, "AppHost.cs");
        var configFile = Path.Combine(context.AppHostDirectory, "dab-config.json");

        return new ProviderPlan
        {
            CliActions = [new PlannedCliAction("aspire", ["add", "dab"])],
            FileActions =
            [
                new PlannedFileAction(
                    Path: appHostFile,
                    OwnershipMode: OwnershipMode.Managed,
                    BlockMarker: context.BlockName,
                    RenderContent: () => RenderAppHostRegion(aspireName, databaseRef, context.BlockName)),

                new PlannedFileAction(
                    Path: configFile,
                    OwnershipMode: OwnershipMode.Scaffold,
                    BlockMarker: context.BlockName,
                    RenderContent: () => RenderConfigJson(databaseRef)),
            ],
        };
    }

    private static string RenderAppHostRegion(string aspireName, string? databaseRef, string blockName)
    {
        var sb = new StringBuilder();
        sb.Append("var ").Append(blockName).Append(" = builder.AddDataAPIBuilder(\"").Append(aspireName).Append("\")");
        if (!string.IsNullOrEmpty(databaseRef))
        {
            sb.Append(".WithReference(").Append(databaseRef).Append(')');
        }

        sb.Append(';');
        return sb.ToString();
    }

    private static string RenderConfigJson(string? databaseRef)
    {
        var connName = databaseRef ?? "default";
        return $$"""
            {
              "$schema": "https://github.com/Azure/data-api-builder/releases/latest/download/dab.draft.schema.json",
              "data-source": {
                "database-type": "mssql",
                "connection-string": "@env('ConnectionStrings__{{connName}}')"
              },
              "runtime": {
                "rest": { "enabled": true, "path": "/api" },
                "graphql": { "enabled": true, "path": "/graphql" },
                "authentication": { "provider": "StaticWebApps" }
              },
              "entities": {}
            }
            """;
    }
}
