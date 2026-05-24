# AspireForm Vertical Catalog — Plan 2.4: AspireForm.Plugin.Reporting

**Goal:** Ship `AspireForm.Plugin.Reporting 0.1.0` — Module provider that turns declared database **views** into curated read-only REST/GraphQL endpoints by emitting a sibling `dab-reports.json` config file that DAB consumes. Depends on the DAB plugin (separate config file added to `AddDataAPIBuilder`'s configFiles).

**Plan position:** 2.4 of 10.

## Locked decisions

- **Block type:** `reporting` (Module).
- **Inputs:**
  - `dependsOn` (string[], required) — must reference the DAB block.
  - `views` (object[], required) — each: `{ "name": "...", "source": "dbo.MyView", "permissions": [{"role":"anonymous","actions":["read"]}] }`. The `permissions` default is anonymous-read.
- **No CLI action** in v1.
- **One scaffold file:** `<apphost>/dab-reports.json` — a DAB-config-format file with `data-source` placeholder + an `entities` map built from `views[]`. User adds it to their DAB block as a secondary config file (manually for v1).

## Provider sketch

```csharp
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.Providers;

namespace AspireForm.Plugin.Reporting;

/// <summary>External Module provider for DAB-curated reports (read-only views exposed as REST/GraphQL).</summary>
public sealed class ReportingModuleProvider : IProvider
{
    public string Type => "reporting";
    public BlockKind Kind => BlockKind.Module;

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
```

## Tests (4)

1. `Type_and_kind_are_correct` → reporting, Module.
2. `Plan_emits_scaffold_dab_reports_json` → 1 file action, scaffold, path ends with `dab-reports.json`.
3. `Plan_renders_each_view_as_a_dab_entity` → views=[{name=Sales,source=dbo.Sales}] → content contains `"Sales"` entity with `"source": "dbo.Sales"` and default anonymous-read permission.
4. `Plan_emits_no_CLI_actions` → empty CliActions.

## Standard layout + commit

Same template. Single commit:
```bash
git add src/Plugins/AspireForm.Plugin.Reporting/ tests/Plugins/AspireForm.Plugin.Reporting.Tests/ AspireForm.slnx
git commit -m "feat(reporting): add AspireForm.Plugin.Reporting (DAB-curated views Module)"
```
