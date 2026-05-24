# AspireForm Vertical Catalog — Plan 2.3: AspireForm.Plugin.DAB

**Goal:** Ship `AspireForm.Plugin.DAB 0.1.0` — Resource provider for Microsoft Data API Builder (REST/GraphQL endpoints from database config). Wraps the CommunityToolkit hosting integration.

**Plan position:** 2.3 of 10.

## Locked decisions

- **Block type:** `dab` (Resource, hybrid — Aspire container + config scaffolding).
- **CLI action:** `aspire add dab` (CommunityToolkit short name).
- **Inputs:**
  - `aspireName` (string, default = block name).
  - `databaseReference` (string, optional) — block name of database for `.WithReference(...)`.
  - `dependsOn` (string[]) — handled by planner; included in inputs for clarity.
- **File actions:**
  1. **Managed:** `<apphost>/AppHost.cs` — `var <block> = builder.AddDataAPIBuilder("<aspireName>"){.WithReference(<dbBlock>)};`.
  2. **Scaffold:** `<apphost>/dab-config.json` — minimal dab-config skeleton (data-source pointer, runtime block, empty entities map). Generated once.

## Provider sketch

```csharp
using System.Text;
using System.Text.Json.Nodes;
using AspireForm.Providers;

namespace AspireForm.Plugin.DAB;

/// <summary>External Resource provider for Microsoft Data API Builder (REST/GraphQL over a database).</summary>
public sealed class DabResourceProvider : IProvider
{
    public string Type => "dab";
    public BlockKind Kind => BlockKind.Resource;

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
```

## Tests (4)

1. `Type_and_kind_are_correct` → type=dab, kind=Resource.
2. `Plan_emits_aspire_add_dab_and_managed_region` → CLI `aspire add dab`; managed region contains `AddDataAPIBuilder("dab")`.
3. `Plan_with_databaseReference_appends_WithReference` → input `databaseReference: "sql"` → region contains `.WithReference(sql)`.
4. `Plan_emits_scaffold_dab_config_json` → scaffold file with `$schema` + `data-source` + empty `entities`.

## Standard plugin layout

Same template as Mailpit/Hangfire. Single commit:
```bash
git add src/Plugins/AspireForm.Plugin.DAB/ tests/Plugins/AspireForm.Plugin.DAB.Tests/ AspireForm.slnx
git commit -m "feat(dab): add AspireForm.Plugin.DAB (Data API Builder Resource provider)"
```

## Definition of done
Plugin packs + 4 tests pass. Ready to ship via `plugin/DAB/v0.1.0`.
