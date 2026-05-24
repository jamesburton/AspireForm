# AspireForm Vertical Catalog — Plan 2.6: AspireForm.Plugin.Auth.ApiKey

**Goal:** Ship `AspireForm.Plugin.Auth.ApiKey 0.1.0` — Module provider for API-key authentication. Scaffolds an `ApiKeyAuthSetup.cs` helper + a managed AppHost-region comment using `Auth.Common`'s scaffolder helpers.

**Plan position:** 2.6 of 10. Depends on Auth.Common (transitive runtime dep via PackageReference, not PrivateAssets).

## Locked decisions

- **Block type:** `auth-apikey` (Module).
- **No CLI action** (consumer wires Microsoft.AspNetCore.Authentication packages themselves).
- **Inputs:**
  - `headerName` (string, default `"X-API-Key"`).
  - `keysSource` (string, default `"config"`, accepts `"config"` | `"db"`).
- **File actions (two):**
  1. **Scaffold** `<apphost>/ApiKeyAuthSetup.cs` — static helper with `AddApiKeyAuth(IServiceCollection)` and `AddApiKeyAuthentication(AuthenticationBuilder)` extension methods.
  2. **Managed** marker region in `AppHost.cs` named via `AuthMarkerNames.Marker("apikey")` (= `auth-apikey`), content from `AuthScaffold.RenderRegistrationComment("apikey", projectName)`.

## csproj

Same template as Mailpit but with **non-PrivateAssets** reference to Auth.Common so it ships as a transitive dep:

```xml
<ItemGroup>
  <ProjectReference Include="../../AspireForm/AspireForm.csproj" PrivateAssets="all" />
  <ProjectReference Include="../AspireForm.Plugin.Auth.Common/AspireForm.Plugin.Auth.Common.csproj" />
</ItemGroup>
```

The Auth.Common reference has no `PrivateAssets="all"` — it propagates as a runtime dep into the plugin's deps.json so PluginAssemblyLoader's resolver finds it.

Also add this for the NuGet pack:
```xml
<PropertyGroup>
  <PackageId>AspireForm.Plugin.Auth.ApiKey</PackageId>
  <Version>0.1.0</Version>
  ...
  <PackageType>AspireFormPlugin</PackageType>
</PropertyGroup>
```

## aspireform-plugin.json

```json
{
  "name": "Auth.ApiKey",
  "version": "0.1.0",
  "minAspireFormVersion": "0.3.0",
  "assemblyName": "AspireForm.Plugin.Auth.ApiKey",
  "providers": [
    {
      "type": "auth-apikey",
      "kind": "module",
      "className": "AspireForm.Plugin.Auth.ApiKey.ApiKeyAuthModuleProvider"
    }
  ]
}
```

## Provider

```csharp
using System.Text.Json.Nodes;
using AspireForm.Plugin.Auth.Common;
using AspireForm.Providers;

namespace AspireForm.Plugin.Auth.ApiKey;

/// <summary>External Module provider for API-key authentication.</summary>
public sealed class ApiKeyAuthModuleProvider : IProvider
{
    public string Type => "auth-apikey";
    public BlockKind Kind => BlockKind.Module;

    public ProviderPlan Plan(PlanContext context)
    {
        var headerName = context.Inputs["headerName"]?.GetValue<string>() ?? "X-API-Key";
        var keysSource = context.Inputs["keysSource"]?.GetValue<string>() ?? "config";

        var appHostFile = Path.Combine(context.AppHostDirectory, "AppHost.cs");
        var setupFile = Path.Combine(context.AppHostDirectory, "ApiKeyAuthSetup.cs");

        return new ProviderPlan
        {
            FileActions =
            [
                new PlannedFileAction(
                    Path: setupFile,
                    OwnershipMode: OwnershipMode.Scaffold,
                    BlockMarker: context.BlockName,
                    RenderContent: () => RenderSetup(context.ProjectName, headerName, keysSource)),

                new PlannedFileAction(
                    Path: appHostFile,
                    OwnershipMode: OwnershipMode.Managed,
                    BlockMarker: AuthMarkerNames.Marker("apikey"),
                    RenderContent: () => AuthScaffold.RenderRegistrationComment("apikey", context.ProjectName)),
            ],
        };
    }

    private static string RenderSetup(string projectName, string headerName, string keysSource) => $$"""
        using Microsoft.AspNetCore.Authentication;
        using Microsoft.Extensions.Configuration;
        using Microsoft.Extensions.DependencyInjection;

        namespace {{projectName}}.AppHost;

        /// <summary>API-key auth setup scaffolded by AspireForm. Copy/adapt into your service project.</summary>
        public static class ApiKeyAuthSetup
        {
            /// <summary>The HTTP header name carrying the API key.</summary>
            public const string HeaderName = "{{headerName}}";

            /// <summary>The configured source for valid keys (<c>config</c> or <c>db</c>).</summary>
            public const string KeysSource = "{{keysSource}}";

            /// <summary>Registers API-key auth services. Wire your own AuthenticationHandler in your service project.</summary>
            public static IServiceCollection AddApiKeyAuth(this IServiceCollection services, IConfiguration configuration)
            {
                // TODO: wire your ApiKeyAuthenticationHandler here. KeysSource = "{{keysSource}}".
                return services;
            }
        }
        """;
}
```

## Tests (4)

```csharp
using System.Text.Json.Nodes;
using AspireForm.Plugin.Auth.ApiKey;
using AspireForm.Providers;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Plugin.Auth.ApiKey.Tests;

public sealed class ApiKeyAuthModuleProviderTests
{
    private readonly ApiKeyAuthModuleProvider _provider = new();

    private static PlanContext Ctx(JsonObject inputs) =>
        new("auth", inputs, AppHostDirectory: "./MyApp.AppHost", ProjectName: "MyApp");

    [Fact]
    public void Type_and_kind_are_correct()
    {
        _provider.Type.Should().Be("auth-apikey");
        _provider.Kind.Should().Be(BlockKind.Module);
    }

    [Fact]
    public void Plan_emits_scaffold_setup_file_and_managed_region()
    {
        var plan = _provider.Plan(Ctx(new JsonObject()));
        plan.FileActions.Should().HaveCount(2);
        plan.FileActions.Single(f => f.OwnershipMode == OwnershipMode.Scaffold)
            .Path.Replace('\', '/').Should().EndWith("ApiKeyAuthSetup.cs");
        plan.FileActions.Single(f => f.OwnershipMode == OwnershipMode.Managed)
            .BlockMarker.Should().Be("auth-apikey");
    }

    [Fact]
    public void Plan_setup_file_includes_configured_header_name()
    {
        var plan = _provider.Plan(Ctx(new JsonObject { ["headerName"] = "X-Custom-Key" }));
        var scaffold = plan.FileActions.Single(f => f.OwnershipMode == OwnershipMode.Scaffold);
        scaffold.RenderContent().Should().Contain("\"X-Custom-Key\"");
    }

    [Fact]
    public void Plan_emits_no_CLI_actions()
    {
        _provider.Plan(Ctx(new JsonObject())).CliActions.Should().BeEmpty();
    }
}
```

## Commit
```bash
git add src/Plugins/AspireForm.Plugin.Auth.ApiKey/ tests/Plugins/AspireForm.Plugin.Auth.ApiKey.Tests/ AspireForm.slnx
git commit -m "feat(auth-apikey): add AspireForm.Plugin.Auth.ApiKey (API key auth Module)"
```
