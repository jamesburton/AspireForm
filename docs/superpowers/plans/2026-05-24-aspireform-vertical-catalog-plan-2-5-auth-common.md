# AspireForm Vertical Catalog — Plan 2.5: AspireForm.Plugin.Auth.Common

**Goal:** Ship `AspireForm.Plugin.Auth.Common 0.1.0` — shared substrate library consumed by the three auth plugin implementations (ApiKey, MagicLink, Entra). Exports common helpers + marker conventions. **Not itself an AspireForm plugin** — just a regular NuGet library; the auth plugins reference it as a regular transitive dep that the loader's AssemblyDependencyResolver handles.

**Plan position:** 2.5 of 10.

## Locked decisions

- **Not an AspireForm plugin.** Regular `net10.0` class library; **no** `<PackageType>AspireFormPlugin</PackageType>`, no `aspireform-plugin.json` manifest.
- **Consumers reference it via PackageReference** (NOT PrivateAssets), so it propagates as a transitive dep into auth plugins' `.deps.json`. PluginAssemblyLoader's resolver finds it.
- **Initial scope (v1):** thin substrate.
  - `AuthScaffold` static class with one helper: `RenderRegistrationComment(string variant, string projectName)` → returns a multi-line comment block describing where + how to wire `AddAuthentication` / `UseAuthentication` for the given variant.
  - `AuthMarkerNames` static class with a `Marker(string variant)` returning the auth-region block name pattern (e.g. `auth-apikey`, `auth-magiclink`, `auth-entra`).

## File layout

```
src/Plugins/AspireForm.Plugin.Auth.Common/
  AspireForm.Plugin.Auth.Common.csproj    — net10.0 class lib, NOT an AspireFormPlugin
  AuthScaffold.cs
  AuthMarkerNames.cs
  README.md
  CHANGELOG.md
tests/Plugins/AspireForm.Plugin.Auth.Common.Tests/
  AspireForm.Plugin.Auth.Common.Tests.csproj
  AuthScaffoldTests.cs
```

## csproj (no PackageType=AspireFormPlugin)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>AspireForm.Plugin.Auth.Common</RootNamespace>
    <AssemblyName>AspireForm.Plugin.Auth.Common</AssemblyName>
    <PackageId>AspireForm.Plugin.Auth.Common</PackageId>
    <Version>0.1.0</Version>
    <Authors>James Burton</Authors>
    <Description>Shared substrate library for AspireForm auth plugins (ApiKey, MagicLink, Entra).</Description>
    <PackageProjectUrl>https://github.com/jamesburton/AspireForm</PackageProjectUrl>
    <RepositoryUrl>https://github.com/jamesburton/AspireForm</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageTags>aspireform;auth;substrate</PackageTags>
  </PropertyGroup>
  <ItemGroup>
    <None Include="README.md" Pack="true" PackagePath="\" />
  </ItemGroup>
</Project>
```

Note: NO ProjectReference to AspireForm. Auth.Common only knows itself.

## AuthScaffold.cs

```csharp
namespace AspireForm.Plugin.Auth.Common;

/// <summary>Helpers shared across AspireForm auth plugins for rendering scaffold + managed content consistently.</summary>
public static class AuthScaffold
{
    /// <summary>Renders a multi-line comment block describing where + how to wire AddAuthentication / UseAuthentication for the given auth variant.</summary>
    public static string RenderRegistrationComment(string variant, string projectName) => $$"""
        // {{variant}} auth scaffolded by AspireForm.
        // In your service project's Program.cs, add:
        //   builder.Services.AddAuthentication(...).Add{{Capitalise(variant)}}(/* options */);
        //   app.UseAuthentication();
        //   app.UseAuthorization();
        // See the {{variant}}Setup.cs scaffold in the same directory for a starter helper.
        // Wire this auth block to your service project from {{projectName}}.AppHost.
        """;

    private static string Capitalise(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
```

## AuthMarkerNames.cs

```csharp
namespace AspireForm.Plugin.Auth.Common;

/// <summary>Convention for marker-region names used by AspireForm auth plugins.</summary>
public static class AuthMarkerNames
{
    /// <summary>Returns the marker block name for the given auth variant (e.g. <c>"apikey"</c> -> <c>"auth-apikey"</c>).</summary>
    public static string Marker(string variant) => $"auth-{variant.ToLowerInvariant()}";
}
```

## Tests (3)

```csharp
using AspireForm.Plugin.Auth.Common;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Plugin.Auth.Common.Tests;

public sealed class AuthScaffoldTests
{
    [Fact]
    public void RenderRegistrationComment_includes_variant_and_project_name()
    {
        var content = AuthScaffold.RenderRegistrationComment("apikey", "MyApp");
        content.Should().Contain("apikey").And.Contain("MyApp");
        content.Should().Contain("AddAuthentication").And.Contain("UseAuthentication");
    }

    [Fact]
    public void RenderRegistrationComment_capitalises_variant_in_AddXyz_call()
    {
        var content = AuthScaffold.RenderRegistrationComment("magiclink", "X");
        content.Should().Contain("AddMagiclink");
    }
}

public sealed class AuthMarkerNamesTests
{
    [Fact]
    public void Marker_prefixes_variant_with_auth_dash()
    {
        AuthMarkerNames.Marker("ApiKey").Should().Be("auth-apikey");
        AuthMarkerNames.Marker("magiclink").Should().Be("auth-magiclink");
        AuthMarkerNames.Marker("entra").Should().Be("auth-entra");
    }
}
```

## Standard layout + commit

```bash
git add src/Plugins/AspireForm.Plugin.Auth.Common/ tests/Plugins/AspireForm.Plugin.Auth.Common.Tests/ AspireForm.slnx
git commit -m "feat(auth-common): add AspireForm.Plugin.Auth.Common substrate library"
```

## Definition of done
Library packs cleanly; 3 unit tests passing. Ready to ship via `plugin/Auth.Common/v0.1.0`.
