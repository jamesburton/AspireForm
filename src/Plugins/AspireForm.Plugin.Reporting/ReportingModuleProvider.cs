using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.Providers;

namespace AspireForm.Plugin.Reporting;

/// <summary>External Module provider for DAB-curated reports (read-only views exposed as REST/GraphQL).</summary>
public sealed class ReportingModuleProvider : IProvider
{
    /// <inheritdoc/>
    public string Type => "reporting";

    /// <inheritdoc/>
    public BlockKind Kind => BlockKind.Module;

    /// <inheritdoc/>
    public ProviderPlan Plan(PlanContext context)
    {
        var configFile = Path.Combine(context.AppHostDirectory, "dab-reports.json");

        return new ProviderPlan
        {
            FileActions =
            [
                new PlannedFileAction(
                    Path: configFile,
                    OwnershipMode: OwnershipMode.Scaffold,
                    BlockMarker: context.BlockName,
                    RenderContent: () => RenderReportsConfig(context.Inputs)),
            ],
        };
    }

    private static string RenderReportsConfig(JsonObject inputs)
    {
        var views = inputs["views"] as JsonArray ?? [];
        var entities = new JsonObject();

        foreach (var v in views)
        {
            if (v is not JsonObject view) continue;
            var name = view["name"]?.GetValue<string>();
            var source = view["source"]?.GetValue<string>();
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(source)) continue;

            var permissions = view["permissions"] as JsonArray
                ?? new JsonArray(new JsonObject
                {
                    ["role"] = "anonymous",
                    ["actions"] = new JsonArray("read"),
                });

            entities[name] = new JsonObject
            {
                ["source"] = source,
                ["permissions"] = (JsonNode)permissions.DeepClone(),
            };
        }

        var root = new JsonObject
        {
            ["$schema"] = "https://github.com/Azure/data-api-builder/releases/latest/download/dab.draft.schema.json",
            ["data-source"] = new JsonObject
            {
                ["database-type"] = "mssql",
                ["connection-string"] = "@env('ConnectionStrings__default')",
            },
            ["runtime"] = new JsonObject
            {
                ["rest"] = new JsonObject { ["enabled"] = true, ["path"] = "/api/reports" },
                ["graphql"] = new JsonObject { ["enabled"] = true, ["path"] = "/graphql/reports" },
            },
            ["entities"] = entities,
        };

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}
