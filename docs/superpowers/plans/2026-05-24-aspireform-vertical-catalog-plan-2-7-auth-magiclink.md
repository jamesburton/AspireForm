# AspireForm Vertical Catalog — Plan 2.7: AspireForm.Plugin.Auth.MagicLink

**Goal:** Ship `AspireForm.Plugin.Auth.MagicLink 0.1.0` — passwordless email sign-in Module. Scaffolds `MagicLinkAuthSetup.cs` with helper + a managed AppHost-region comment. Depends on Auth.Common + Mailpit (for SMTP) + a database block (token storage).

**Plan position:** 2.7 of 10.

## Locked decisions

- **Block type:** `auth-magiclink` (Module).
- **Inputs:**
  - `dependsOn[]` — should include the Mailpit block + a database block.
  - `fromAddress` (string, required) — `MAILER@example.com`.
  - `tokenLifetimeMinutes` (int, default 15).
- **No CLI action.**
- **File actions:**
  1. Scaffold `<apphost>/MagicLinkAuthSetup.cs` — helper exposing `MagicLinkOptions` static class with the configured `FromAddress` + `TokenLifetimeMinutes`.
  2. Managed marker `auth-magiclink` in `AppHost.cs` via `AuthScaffold.RenderRegistrationComment("magiclink", ProjectName)`.

## csproj — Auth.Common transitive reference

Same pattern as Auth.ApiKey: ProjectReference to AspireForm with `PrivateAssets="all"` AND ProjectReference to Auth.Common WITHOUT PrivateAssets.

## Provider

```csharp
using AspireForm.Plugin.Auth.Common;
using AspireForm.Providers;

namespace AspireForm.Plugin.Auth.MagicLink;

/// <summary>External Module provider for passwordless email magic-link authentication.</summary>
public sealed class MagicLinkAuthModuleProvider : IProvider
{
    public string Type => "auth-magiclink";
    public BlockKind Kind => BlockKind.Module;

    public ProviderPlan Plan(PlanContext context)
    {
        var fromAddress = context.Inputs["fromAddress"]?.GetValue<string>() ?? "noreply@example.com";
        var tokenLifetime = context.Inputs["tokenLifetimeMinutes"]?.GetValue<int>() ?? 15;

        var appHostFile = Path.Combine(context.AppHostDirectory, "AppHost.cs");
        var setupFile = Path.Combine(context.AppHostDirectory, "MagicLinkAuthSetup.cs");

        return new ProviderPlan
        {
            FileActions =
            [
                new PlannedFileAction(
                    Path: setupFile,
                    OwnershipMode: OwnershipMode.Scaffold,
                    BlockMarker: context.BlockName,
                    RenderContent: () => RenderSetup(context.ProjectName, fromAddress, tokenLifetime)),

                new PlannedFileAction(
                    Path: appHostFile,
                    OwnershipMode: OwnershipMode.Managed,
                    BlockMarker: AuthMarkerNames.Marker("magiclink"),
                    RenderContent: () => AuthScaffold.RenderRegistrationComment("magiclink", context.ProjectName)),
            ],
        };
    }

    private static string RenderSetup(string projectName, string fromAddress, int tokenLifetime) => $$"""
        namespace {{projectName}}.AppHost;

        /// <summary>Magic-link auth scaffolded by AspireForm. Copy/adapt into your service project.</summary>
        public static class MagicLinkAuthSetup
        {
            /// <summary>Email "From" address for outgoing magic-link emails.</summary>
            public const string FromAddress = "{{fromAddress}}";

            /// <summary>Magic-link token lifetime in minutes.</summary>
            public const int TokenLifetimeMinutes = {{tokenLifetime}};

            // TODO: wire IEmailSender + token storage (SQL or Redis) in your service project.
        }
        """;
}
```

## Tests (4)

- type+kind (auth-magiclink, Module)
- Plan emits scaffold setup file + managed `auth-magiclink` region
- Setup file embeds configured FromAddress
- No CLI actions

## Commit
```bash
git add src/Plugins/AspireForm.Plugin.Auth.MagicLink/ tests/Plugins/AspireForm.Plugin.Auth.MagicLink.Tests/ AspireForm.slnx
git commit -m "feat(auth-magiclink): add AspireForm.Plugin.Auth.MagicLink"
```
