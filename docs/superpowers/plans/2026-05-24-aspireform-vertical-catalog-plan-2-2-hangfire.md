# AspireForm Vertical Catalog — Plan 2.2: AspireForm.Plugin.Hangfire

**Goal:** Ship `AspireForm.Plugin.Hangfire 0.1.0` — a Module provider for Hangfire background jobs. Like ef-data, this is a minimal v1 Module: scaffolds a `HangfireSetup.cs` placeholder + records the storage dependency in a managed AppHost region. Full DI/middleware wiring is left to the user (no service project to wire into in v1 reference).

**Plan position:** 2.2 of 10.

## Locked decisions

- **Block type:** `hangfire` (Module).
- **Inputs:**
  - `storage` (string, required, "sql"|"redis") — which storage backend.
  - `dependsOn` (string[], required) — the storage block name(s).
  - `dashboardPath` (string, default "/hangfire").
- **No CLI action** in v1 (Hangfire NuGet packages get added when the user wires the actual service project).
- **AppHost managed region:** comment block recording: storage choice + dependency block + dashboard path. Same minimal-comment shape as `ef-data`.
- **Scaffold file:** `<apphost>/HangfireSetup.cs` — scaffold mode, contains a static class with sample `AddHangfireWithStorage(IServiceCollection, IConfiguration, string)` extension method showing the storage-specific config snippet. User copies this into their service project.

## Files / structure

Follow the templated pattern from Plans 2.0 (Redis) and 2.1 (Mailpit):

```
src/Plugins/AspireForm.Plugin.Hangfire/
  AspireForm.Plugin.Hangfire.csproj
  aspireform-plugin.json                  — name=Hangfire, type=hangfire, kind=module
  HangfireModuleProvider.cs
  README.md
  CHANGELOG.md
tests/Plugins/AspireForm.Plugin.Hangfire.Tests/
  AspireForm.Plugin.Hangfire.Tests.csproj
  HangfireModuleProviderTests.cs
```

## Provider implementation sketch

```csharp
using System.Text;
using System.Text.Json.Nodes;
using AspireForm.Providers;

namespace AspireForm.Plugin.Hangfire;

/// <summary>External Module provider for Hangfire background jobs.</summary>
public sealed class HangfireModuleProvider : IProvider
{
    public string Type => "hangfire";
    public BlockKind Kind => BlockKind.Module;

    public ProviderPlan Plan(PlanContext context)
    {
        var storage = context.Inputs["storage"]?.GetValue<string>() ?? "sql";
        var dashboardPath = context.Inputs["dashboardPath"]?.GetValue<string>() ?? "/hangfire";

        var appHostFile = Path.Combine(context.AppHostDirectory, "AppHost.cs");
        var scaffoldFile = Path.Combine(context.AppHostDirectory, "HangfireSetup.cs");

        return new ProviderPlan
        {
            FileActions =
            [
                new PlannedFileAction(
                    Path: scaffoldFile,
                    OwnershipMode: OwnershipMode.Scaffold,
                    BlockMarker: context.BlockName,
                    RenderContent: () => RenderSetupFile(context.ProjectName, storage)),

                new PlannedFileAction(
                    Path: appHostFile,
                    OwnershipMode: OwnershipMode.Managed,
                    BlockMarker: context.BlockName,
                    RenderContent: () => RenderAppHostRegion(storage, dashboardPath, context.BlockName)),
            ],
        };
    }

    private static string RenderSetupFile(string projectName, string storage)
    {
        var storageBlock = storage switch
        {
            "redis" => """
                services.AddHangfire(cfg => cfg.UseRedisStorage(configuration.GetConnectionString(connectionName)));
                """,
            _ => """
                services.AddHangfire(cfg => cfg.UseSqlServerStorage(configuration.GetConnectionString(connectionName)));
                """,
        };

        return $$"""
            using Hangfire;
            using Microsoft.Extensions.Configuration;
            using Microsoft.Extensions.DependencyInjection;

            namespace {{projectName}}.AppHost;

            /// <summary>Hangfire setup scaffolded by AspireForm. Copy/adapt into your worker or web project.</summary>
            public static class HangfireSetup
            {
                /// <summary>Registers Hangfire with the configured storage. Call <c>services.AddHangfireServer()</c> in your worker project.</summary>
                public static IServiceCollection AddHangfireWithStorage(
                    this IServiceCollection services, IConfiguration configuration, string connectionName)
                {
                    {{storageBlock}}
                    return services;
                }
            }
            """;
    }

    private static string RenderAppHostRegion(string storage, string dashboardPath, string blockName)
    {
        return $"""
            // hangfire module ({blockName}): storage={storage}, dashboard={dashboardPath}.
            // Wire your worker / web project here (e.g. .WithReference({storage})).
            // See HangfireSetup.cs in this directory for a sample DI registration helper.
            """;
    }
}
```

## Tests (4 cases)

1. `Type_and_kind_are_correct` — type=hangfire, kind=Module.
2. `Plan_with_sql_storage_emits_scaffold_and_managed_actions` — 2 file actions; scaffold contains `UseSqlServerStorage`; managed comment contains `storage=sql`.
3. `Plan_with_redis_storage_uses_redis_helper` — scaffold contains `UseRedisStorage`; managed contains `storage=redis`.
4. `Plan_emits_no_CLI_actions_in_v1` — `plan.CliActions.Should().BeEmpty()`.

## Steps

Single combined commit (this is templated boilerplate):

1. Create plugin csproj (substitute names, `PackageTags=aspireform;aspireform-plugin;hangfire;background-jobs`).
2. Create manifest JSON.
3. Create README+CHANGELOG.
4. Create HangfireModuleProvider.cs.
5. Create test csproj + tests.
6. `dotnet sln add` both csproj.
7. Build + run tests (4 plugin tests should pass).
8. Commit:
```bash
git add src/Plugins/AspireForm.Plugin.Hangfire/ tests/Plugins/AspireForm.Plugin.Hangfire.Tests/ AspireForm.slnx
git commit -m "feat(hangfire): add AspireForm.Plugin.Hangfire (Hangfire Module provider, v1 minimal)"
```

## Definition of done

- Plugin packs cleanly; 4 unit tests passing.
- Ready to ship via `plugin/Hangfire/v0.1.0`.
