# AspireForm Vertical Catalog — Plan 2.1: AspireForm.Plugin.Mailpit

> **For agentic workers:** Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Ship `AspireForm.Plugin.Mailpit 0.1.0` — a Resource provider for Mailpit (local SMTP test mail server), built on the loader infrastructure from Plans 2.0/2.0.5. Templates `var mailpit = builder.AddMailPit("mailpit");` in `AppHost.cs`. Delegates package add to `aspire add mailpit`.

**Architecture:** Identical to Redis plugin pattern. New `src/Plugins/AspireForm.Plugin.Mailpit/` with csproj (`<PackageType>AspireFormPlugin</PackageType>`), `aspireform-plugin.json` manifest, `MailpitResourceProvider` implementing `IProvider`. Dedicated test project at `tests/Plugins/AspireForm.Plugin.Mailpit.Tests/`.

**Tech Stack:** Same as Redis plugin — net10.0, references AspireForm as PrivateAssets="all", no extra deps.

**Plan position:** Plan 2.1 of 10. Plans 2.2–2.9 follow the same template.

---

## Locked decisions

- **Block type:** `mailpit` (Resource).
- **CLI action:** `aspire add mailpit` (community-toolkit hosting integration).
- **AppHost managed region content:** `var <block> = builder.AddMailPit("<aspireName>");` plus `.WithDataVolume()` when `withDataVolume: true`.
- **Inputs:**
  - `aspireName` (string, default = block name)
  - `withDataVolume` (bool, default = false)

---

## Task 1: Plugin csproj + manifest

**Files:**
- Create: `src/Plugins/AspireForm.Plugin.Mailpit/AspireForm.Plugin.Mailpit.csproj`
- Create: `src/Plugins/AspireForm.Plugin.Mailpit/aspireform-plugin.json`
- Create: `src/Plugins/AspireForm.Plugin.Mailpit/README.md`
- Create: `src/Plugins/AspireForm.Plugin.Mailpit/CHANGELOG.md`

- [ ] **Step 1: csproj** (model on `src/Plugins/AspireForm.Plugin.Redis/AspireForm.Plugin.Redis.csproj` verbatim, substitute names):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>AspireForm.Plugin.Mailpit</RootNamespace>
    <AssemblyName>AspireForm.Plugin.Mailpit</AssemblyName>
    <PackageId>AspireForm.Plugin.Mailpit</PackageId>
    <Version>0.1.0</Version>
    <Authors>James Burton</Authors>
    <Description>AspireForm plugin: Mailpit (local SMTP test mail server) Resource provider.</Description>
    <PackageProjectUrl>https://github.com/jamesburton/AspireForm</PackageProjectUrl>
    <RepositoryUrl>https://github.com/jamesburton/AspireForm</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageType>AspireFormPlugin</PackageType>
    <PackageTags>aspireform;aspireform-plugin;mailpit;smtp;aspire</PackageTags>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../../AspireForm/AspireForm.csproj" PrivateAssets="all" />
  </ItemGroup>
  <ItemGroup>
    <Content Include="aspireform-plugin.json" Pack="true" PackagePath="\" CopyToOutputDirectory="PreserveNewest" />
    <None Include="README.md" Pack="true" PackagePath="\" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: aspireform-plugin.json**

```json
{
  "name": "Mailpit",
  "version": "0.1.0",
  "minAspireFormVersion": "0.3.0",
  "assemblyName": "AspireForm.Plugin.Mailpit",
  "providers": [
    { "type": "mailpit", "kind": "resource", "className": "AspireForm.Plugin.Mailpit.MailpitResourceProvider" }
  ]
}
```

- [ ] **Step 3: README.md**

```markdown
# AspireForm.Plugin.Mailpit

Mailpit Resource provider for [AspireForm](https://github.com/jamesburton/AspireForm).
Mailpit is a local SMTP test mail server that catches outgoing email and presents it in a web UI.

## Block type
`mailpit` (Resource)

## Inputs
| Input | Type | Default | Description |
|---|---|---|---|
| `aspireName` | string | block name | Name passed to `builder.AddMailPit(...)`. |
| `withDataVolume` | bool | `false` | When true, appends `.WithDataVolume()`. |

## Example
```yaml
resources:
  mail:
    type: mailpit
    aspireName: mail
    withDataVolume: true
```
```

- [ ] **Step 4: CHANGELOG.md**

```markdown
# Changelog

## [0.1.0] - 2026-05-24

Initial release. Mailpit Resource provider for AspireForm.

### Added
- `mailpit` block type emitting `aspire add mailpit` + managed AppHost region with `builder.AddMailPit(...)`.
- Optional `withDataVolume` input.
```

- [ ] **Step 5: Commit**

```bash
git add src/Plugins/AspireForm.Plugin.Mailpit/
git commit -m "feat(mailpit): scaffold plugin csproj + manifest + docs"
```

---

## Task 2: MailpitResourceProvider

**Files:**
- Create: `src/Plugins/AspireForm.Plugin.Mailpit/MailpitResourceProvider.cs`

- [ ] **Step 1: Implement (model on RedisResourceProvider verbatim)**

```csharp
using System.Text;
using System.Text.Json.Nodes;
using AspireForm.Providers;

namespace AspireForm.Plugin.Mailpit;

/// <summary>External Resource provider for Mailpit (local SMTP test mail server). Delegates package add to <c>aspire add mailpit</c>; owns the AppHost resource declaration in a managed region.</summary>
public sealed class MailpitResourceProvider : IProvider
{
    /// <inheritdoc />
    public string Type => "mailpit";

    /// <inheritdoc />
    public BlockKind Kind => BlockKind.Resource;

    /// <inheritdoc />
    public ProviderPlan Plan(PlanContext context)
    {
        var aspireName = context.Inputs["aspireName"]?.GetValue<string>() ?? context.BlockName;
        var withDataVolume = context.Inputs["withDataVolume"]?.GetValue<bool>() ?? false;

        var appHostFile = Path.Combine(context.AppHostDirectory, "AppHost.cs");

        return new ProviderPlan
        {
            CliActions = [new PlannedCliAction("aspire", ["add", "mailpit"])],
            FileActions =
            [
                new PlannedFileAction(
                    Path: appHostFile,
                    OwnershipMode: OwnershipMode.Managed,
                    BlockMarker: context.BlockName,
                    RenderContent: () => RenderInner(aspireName, withDataVolume, context.BlockName)),
            ],
        };
    }

    private static string RenderInner(string aspireName, bool withDataVolume, string blockName)
    {
        var sb = new StringBuilder();
        sb.Append("var ").Append(blockName).Append(" = builder.AddMailPit(\"").Append(aspireName).Append("\")");
        if (withDataVolume)
        {
            sb.Append(".WithDataVolume()");
        }
        sb.Append(';');
        return sb.ToString();
    }
}
```

- [ ] **Step 2: Build to confirm**

```bash
dotnet build
```

- [ ] **Step 3: Commit**

```bash
git add src/Plugins/AspireForm.Plugin.Mailpit/MailpitResourceProvider.cs
git commit -m "feat(mailpit): add MailpitResourceProvider"
```

---

## Task 3: Unit test project

**Files:**
- Create: `tests/Plugins/AspireForm.Plugin.Mailpit.Tests/AspireForm.Plugin.Mailpit.Tests.csproj`
- Create: `tests/Plugins/AspireForm.Plugin.Mailpit.Tests/MailpitResourceProviderTests.cs`

- [ ] **Step 1: csproj** (model on Redis Tests csproj verbatim, substitute names)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Exe</OutputType>
    <RootNamespace>AspireForm.Plugin.Mailpit.Tests</RootNamespace>
    <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit.v3.mtp-v2" Version="3.2.2" />
    <PackageReference Include="AwesomeAssertions" Version="9.4.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../../src/Plugins/AspireForm.Plugin.Mailpit/AspireForm.Plugin.Mailpit.csproj" />
    <ProjectReference Include="../../../src/AspireForm/AspireForm.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Tests**

```csharp
using System.Text.Json.Nodes;
using AspireForm.Plugin.Mailpit;
using AspireForm.Providers;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Plugin.Mailpit.Tests;

public sealed class MailpitResourceProviderTests
{
    private readonly MailpitResourceProvider _provider = new();

    private static PlanContext Ctx(JsonObject inputs) =>
        new("mail", inputs, AppHostDirectory: "./MyApp.AppHost", ProjectName: "MyApp");

    [Fact]
    public void Type_and_kind_are_correct()
    {
        _provider.Type.Should().Be("mailpit");
        _provider.Kind.Should().Be(BlockKind.Resource);
    }

    [Fact]
    public void Plan_emits_aspire_add_mailpit_and_managed_AppHost_region()
    {
        var plan = _provider.Plan(Ctx(new JsonObject { ["aspireName"] = "mail" }));

        plan.CliActions.Should().ContainSingle(c => c.Tool == "aspire");
        plan.CliActions[0].Args.Should().ContainInOrder("add", "mailpit");

        plan.FileActions.Should().ContainSingle();
        plan.FileActions[0].OwnershipMode.Should().Be(OwnershipMode.Managed);
        plan.FileActions[0].RenderContent().Should().Contain("builder.AddMailPit(\"mail\")");
    }

    [Fact]
    public void Plan_appends_WithDataVolume_when_withDataVolume_is_true()
    {
        var inputs = new JsonObject { ["aspireName"] = "mail", ["withDataVolume"] = true };
        _provider.Plan(Ctx(inputs)).FileActions[0].RenderContent()
            .Should().Contain(".WithDataVolume()");
    }

    [Fact]
    public void Plan_defaults_aspireName_to_block_name()
    {
        _provider.Plan(Ctx(new JsonObject())).FileActions[0].RenderContent()
            .Should().Contain("builder.AddMailPit(\"mail\")");
    }
}
```

- [ ] **Step 3: Add both csproj to solution + run tests**

```bash
dotnet sln add src/Plugins/AspireForm.Plugin.Mailpit/AspireForm.Plugin.Mailpit.csproj
dotnet sln add tests/Plugins/AspireForm.Plugin.Mailpit.Tests/AspireForm.Plugin.Mailpit.Tests.csproj
dotnet build
dotnet run --project tests/Plugins/AspireForm.Plugin.Mailpit.Tests
```

Expected: 4 plugin tests passing.

- [ ] **Step 4: Commit**

```bash
git add tests/Plugins/AspireForm.Plugin.Mailpit.Tests/ AspireForm.slnx
git commit -m "feat(mailpit): add unit tests + register in solution"
```

---

## Definition of done

- AspireForm.Plugin.Mailpit packs cleanly.
- 4 plugin unit tests passing.
- Solution builds clean.
- Ready to ship via `git tag -a plugin/Mailpit/v0.1.0`.
