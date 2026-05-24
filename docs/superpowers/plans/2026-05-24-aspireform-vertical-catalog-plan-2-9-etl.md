# AspireForm Vertical Catalog — Plan 2.9: AspireForm.Plugin.ETL (final plan)

**Goal:** Ship `AspireForm.Plugin.ETL 0.1.0` — Module provider for CSV/Excel file import + Hangfire-driven directory watcher. Depends on Hangfire (for the watcher) + a database/ef-data block (for the destination). v1 scaffolds the import controller + watcher setup; the user wires concrete parsing/destination logic.

**Plan position:** 2.9 of 10 — last plan in sub-project #2.

## Locked decisions

- **Block type:** `etl` (Module).
- **Inputs:**
  - `dependsOn[]` — should include the Hangfire block + a database block.
  - `watchDirectory` (string, default `"./incoming"`).
  - `parsers[]` (string array, default `["csv", "excel"]`) — informational.
- **No CLI action.**
- **File actions (two):**
  1. Scaffold `<apphost>/EtlSetup.cs` — static helper with `AddEtl(IServiceCollection, IConfiguration)` extension method and a `WatchDirectory` constant.
  2. Managed marker `etl` in `AppHost.cs` — comment block recording dependencies + watch directory.

## Provider

```csharp
using System.Text.Json.Nodes;
using AspireForm.Providers;

namespace AspireForm.Plugin.ETL;

/// <summary>External Module provider for CSV/Excel file ETL import (Hangfire-driven directory watcher).</summary>
public sealed class EtlModuleProvider : IProvider
{
    public string Type => "etl";
    public BlockKind Kind => BlockKind.Module;

    public ProviderPlan Plan(PlanContext context)
    {
        var watchDirectory = context.Inputs["watchDirectory"]?.GetValue<string>() ?? "./incoming";
        var parsersArr = context.Inputs["parsers"] as JsonArray;
        var parsers = parsersArr is null
            ? new[] { "csv", "excel" }
            : parsersArr.Select(p => p?.GetValue<string>() ?? string.Empty).Where(s => s.Length > 0).ToArray();

        var appHostFile = Path.Combine(context.AppHostDirectory, "AppHost.cs");
        var setupFile = Path.Combine(context.AppHostDirectory, "EtlSetup.cs");

        return new ProviderPlan
        {
            FileActions =
            [
                new PlannedFileAction(
                    Path: setupFile,
                    OwnershipMode: OwnershipMode.Scaffold,
                    BlockMarker: context.BlockName,
                    RenderContent: () => RenderSetup(context.ProjectName, watchDirectory, parsers)),

                new PlannedFileAction(
                    Path: appHostFile,
                    OwnershipMode: OwnershipMode.Managed,
                    BlockMarker: context.BlockName,
                    RenderContent: () => RenderAppHostRegion(watchDirectory, parsers, context.BlockName)),
            ],
        };
    }

    private static string RenderSetup(string projectName, string watchDirectory, string[] parsers)
    {
        var parserList = string.Join(", ", parsers.Select(p => $"\"{p}\""));
        return $$"""
            using Microsoft.Extensions.Configuration;
            using Microsoft.Extensions.DependencyInjection;

            namespace {{projectName}}.AppHost;

            /// <summary>ETL setup scaffolded by AspireForm. Copy/adapt into your worker or web project.</summary>
            public static class EtlSetup
            {
                /// <summary>Directory the file watcher polls for new files.</summary>
                public const string WatchDirectory = "{{watchDirectory}}";

                /// <summary>Enabled file format parsers.</summary>
                public static readonly string[] EnabledParsers = [ {{parserList}} ];

                /// <summary>Registers ETL services. Wire CsvHelper / ExcelDataReader + SqlBulkCopy in your worker project.</summary>
                public static IServiceCollection AddEtl(this IServiceCollection services, IConfiguration configuration)
                {
                    // TODO: register IFileWatcher, IImporter, and Hangfire recurring job in your service project.
                    return services;
                }
            }
            """;
    }

    private static string RenderAppHostRegion(string watchDirectory, string[] parsers, string blockName)
    {
        var parserList = string.Join(",", parsers);
        return $"""
            // etl module ({blockName}): watch={watchDirectory}, parsers=[{parserList}].
            // Wire your worker project here (e.g. .WithReference(<db>).WithReference(<hangfire>)).
            // See EtlSetup.cs in this directory for a sample DI registration helper.
            """;
    }
}
```

## Tests (4)

- type+kind (etl, Module)
- file actions: scaffold + managed
- setup file contains watchDirectory + parsers
- no CLI actions

## Commit
```bash
git add src/Plugins/AspireForm.Plugin.ETL/ tests/Plugins/AspireForm.Plugin.ETL.Tests/ AspireForm.slnx
git commit -m "feat(etl): add AspireForm.Plugin.ETL (CSV/Excel + Hangfire-driven watcher Module)"
```

## Definition of done
- 4 tests pass.
- Ready to ship via `plugin/ETL/v0.1.0`.
- **Sub-project #2 complete.**
