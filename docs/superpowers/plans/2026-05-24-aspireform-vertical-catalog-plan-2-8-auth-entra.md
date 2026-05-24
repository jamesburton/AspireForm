# AspireForm Vertical Catalog — Plan 2.8: AspireForm.Plugin.Auth.Entra

**Goal:** Ship `AspireForm.Plugin.Auth.Entra 0.1.0` — Microsoft Entra External ID OIDC auth Module. Scaffolds `EntraAuthSetup.cs` with tenant + client constants. Depends on Auth.Common.

**Plan position:** 2.8 of 10.

## Locked decisions

- **Block type:** `auth-entra` (Module).
- **Inputs:**
  - `tenantId` (string, required) — Entra tenant id.
  - `clientId` (string, required) — App registration client id.
  - `audience` (string, optional) — JWT audience (defaults to clientId).
- **No CLI action.** Microsoft.Identity.Web is referenced from the user's service project, not the AppHost.
- **File actions (two):**
  1. Scaffold `<apphost>/EntraAuthSetup.cs` with tenant/client/audience constants + a stub `AddEntraAuth` extension method.
  2. Managed marker `auth-entra` in `AppHost.cs` via `AuthScaffold.RenderRegistrationComment("entra", ProjectName)`.

## Provider

```csharp
using AspireForm.Plugin.Auth.Common;
using AspireForm.Providers;

namespace AspireForm.Plugin.Auth.Entra;

/// <summary>External Module provider for Microsoft Entra External ID OIDC authentication.</summary>
public sealed class EntraAuthModuleProvider : IProvider
{
    public string Type => "auth-entra";
    public BlockKind Kind => BlockKind.Module;

    public ProviderPlan Plan(PlanContext context)
    {
        var tenantId = context.Inputs["tenantId"]?.GetValue<string>() ?? "<tenant-id>";
        var clientId = context.Inputs["clientId"]?.GetValue<string>() ?? "<client-id>";
        var audience = context.Inputs["audience"]?.GetValue<string>() ?? clientId;

        var appHostFile = Path.Combine(context.AppHostDirectory, "AppHost.cs");
        var setupFile = Path.Combine(context.AppHostDirectory, "EntraAuthSetup.cs");

        return new ProviderPlan
        {
            FileActions =
            [
                new PlannedFileAction(
                    Path: setupFile,
                    OwnershipMode: OwnershipMode.Scaffold,
                    BlockMarker: context.BlockName,
                    RenderContent: () => RenderSetup(context.ProjectName, tenantId, clientId, audience)),

                new PlannedFileAction(
                    Path: appHostFile,
                    OwnershipMode: OwnershipMode.Managed,
                    BlockMarker: AuthMarkerNames.Marker("entra"),
                    RenderContent: () => AuthScaffold.RenderRegistrationComment("entra", context.ProjectName)),
            ],
        };
    }

    private static string RenderSetup(string projectName, string tenantId, string clientId, string audience) => $$"""
        namespace {{projectName}}.AppHost;

        /// <summary>Entra External ID auth scaffolded by AspireForm. Copy/adapt into your service project.</summary>
        public static class EntraAuthSetup
        {
            /// <summary>Entra tenant id.</summary>
            public const string TenantId = "{{tenantId}}";

            /// <summary>Entra app registration client id.</summary>
            public const string ClientId = "{{clientId}}";

            /// <summary>JWT audience.</summary>
            public const string Audience = "{{audience}}";

            // TODO: wire Microsoft.Identity.Web in your service project.
        }
        """;
}
```

## Tests (4)

- type+kind
- file actions: scaffold + managed `auth-entra`
- setup file contains tenantId, clientId, audience constants
- no CLI actions

## Commit
```bash
git add src/Plugins/AspireForm.Plugin.Auth.Entra/ tests/Plugins/AspireForm.Plugin.Auth.Entra.Tests/ AspireForm.slnx
git commit -m "feat(auth-entra): add AspireForm.Plugin.Auth.Entra (Entra External ID OIDC Module)"
```
