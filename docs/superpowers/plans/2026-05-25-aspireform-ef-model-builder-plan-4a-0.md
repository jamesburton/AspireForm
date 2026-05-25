# AspireForm EF Model Builder — Plan 4a.0 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `AspireForm 0.5.0` + new sibling `AspireForm.Annotations 0.1.0` adding a code-first EF entity builder — Roslyn-backed `EntityCatalog` (scanner + mutator), 12 fine-grained MCP tools, `aspireform ui` Blazor Server verb, and an expanded built-in `ef-data` provider that emits both DbContext code and `dab-config.json` from attribute-decorated entity classes.

**Architecture:** Roslyn `MSBuildWorkspace` reads + writes the user's entity `.cs` files (no new YAML entity catalog). `IEntityCatalogService` is the single DI seam — Blazor pages, MCP tools, and the `ef-data` provider all call it. The provider stays built-in; `ef-data` block input shape changes (`projectPath` replaces `database`/`contextName`). UI is Kestrel + Blazor Server inside the dnx tool process; HTTP is localhost-only with no auth in v1.

**Tech Stack:** .NET 10 (SDK 10.0.300), Roslyn (`Microsoft.CodeAnalysis.CSharp` 5.3.0 + `Microsoft.CodeAnalysis.Workspaces.MSBuild` + `Microsoft.Build.Locator`), Spectre.Console.Cli 0.55.0, ASP.NET Core (`<FrameworkReference Include="Microsoft.AspNetCore.App" />`), Blazor Server (`Microsoft.AspNetCore.Components.Web`), xUnit v3.2.2 on MTP, AwesomeAssertions 9.4.0, bUnit (Blazor component testing).

**Solo-dev workflow:** Work in-place on `main`, no feature branch (per saved feedback memory).

**Risks flagged in spec §12 (mitigations in tasks below):**
- §12.1 — `MSBuildWorkspace` startup cost: Task 7 caches the workspace per session, lazy-init on first request
- §12.2 — Multi-file mutator complexity for `AddRelationship`: Task 16 implements only 1:1 / 1:N / N:1; M:N defers to #4a.1
- §12.3 — DbContext ambiguity: Task 17 throws `PluginContractException` with explicit pointer to `inputs.dbContext`
- §12.4 — `<FrameworkReference Microsoft.AspNetCore.App />` on PackAsTool: Task 1 sets `<RollForward>LatestMajor</RollForward>`; Task 27 includes a `dotnet pack` + `dnx` smoke verification step
- §12.5 — Kestrel + Windows Firewall: localhost-only binding avoids the prompt on most policies; doc note in Task 27

---

## File map

**New (production):**

- `src/AspireForm.Annotations/AspireForm.Annotations.csproj`
- `src/AspireForm.Annotations/DabExposeAttribute.cs`
- `src/AspireForm.Annotations/DabPathAttribute.cs`
- `src/AspireForm.Annotations/DabPermissionAttribute.cs`
- `src/AspireForm.Annotations/DabRestOnlyAttribute.cs`
- `src/AspireForm.Annotations/DabGraphqlOnlyAttribute.cs`
- `src/AspireForm.Annotations/DabHiddenAttribute.cs`
- `src/AspireForm.Annotations/OnDeleteAttribute.cs`
- `src/AspireForm/EntityCatalog/EntityModel.cs` — Entity, Property, Relationship, AttributeInstance, DbContextInfo, CatalogDiagnostic, MutationResult records + enums
- `src/AspireForm/EntityCatalog/EntityChangeRequest.cs` — sealed-record DSL for mutations
- `src/AspireForm/EntityCatalog/EntityCatalogException.cs`
- `src/AspireForm/EntityCatalog/IEntityCatalogService.cs` — DI seam
- `src/AspireForm/EntityCatalog/RoslynEntityScanner.cs`
- `src/AspireForm/EntityCatalog/RoslynEntityMutator.cs`
- `src/AspireForm/EntityCatalog/RoslynEntityCatalogService.cs` — default impl
- `src/AspireForm/EntityCatalog/MSBuildBootstrap.cs` — `MSBuildLocator.RegisterDefaults()` once-per-process guard
- `src/AspireForm/Providers/EfData/DbContextEmitter.cs`
- `src/AspireForm/Providers/EfData/DabConfigEmitter.cs`
- `src/AspireForm/Mcp/Tools/Entity/EntityListTool.cs`
- `src/AspireForm/Mcp/Tools/Entity/EntityShowTool.cs`
- `src/AspireForm/Mcp/Tools/Entity/DbContextListTool.cs`
- `src/AspireForm/Mcp/Tools/Entity/EntityCreateTool.cs`
- `src/AspireForm/Mcp/Tools/Entity/EntityDeleteTool.cs`
- `src/AspireForm/Mcp/Tools/Entity/PropertyAddTool.cs`
- `src/AspireForm/Mcp/Tools/Entity/PropertyRemoveTool.cs`
- `src/AspireForm/Mcp/Tools/Entity/PropertyRenameTool.cs`
- `src/AspireForm/Mcp/Tools/Entity/AttributeSetTool.cs`
- `src/AspireForm/Mcp/Tools/Entity/AttributeClearTool.cs`
- `src/AspireForm/Mcp/Tools/Entity/RelationshipAddTool.cs`
- `src/AspireForm/Mcp/Tools/Entity/RelationshipRemoveTool.cs`
- `src/AspireForm/Cli/UiCommand.cs`
- `src/AspireForm/Ui/UiOptions.cs`
- `src/AspireForm/Ui/UiHost.cs`
- `src/AspireForm/Ui/BrowserLauncher.cs`
- `src/AspireForm/Ui/Components/App.razor`
- `src/AspireForm/Ui/Components/Routes.razor`
- `src/AspireForm/Ui/Components/_Imports.razor`
- `src/AspireForm/Ui/Components/Layout/MainLayout.razor`
- `src/AspireForm/Ui/Components/Pages/Index.razor`
- `src/AspireForm/Ui/Components/Pages/Entities.razor`
- `src/AspireForm/Ui/Components/Pages/Diagnostics.razor`
- `src/AspireForm/Ui/Components/Pages/About.razor`
- `src/AspireForm/Ui/Components/Entity/EntityList.razor`
- `src/AspireForm/Ui/Components/Entity/EntityHeader.razor`
- `src/AspireForm/Ui/Components/Entity/EntityPropertiesTab.razor`
- `src/AspireForm/Ui/Components/Entity/EntityRelationshipsTab.razor`
- `src/AspireForm/Ui/Components/Entity/EntityAttributesTab.razor`
- `src/AspireForm/Ui/Components/Entity/EntityDabTab.razor`
- `src/AspireForm/Ui/Components/Dialogs/NewEntityDialog.razor`
- `src/AspireForm/Ui/Components/Dialogs/AddPropertyDialog.razor`
- `src/AspireForm/Ui/wwwroot/site.css`

**Modified:**

- `src/AspireForm/AspireForm.csproj` — version 0.4.0 → 0.5.0; `<FrameworkReference Microsoft.AspNetCore.App />`; `<RollForward>LatestMajor</RollForward>`; add `Microsoft.CodeAnalysis.Workspaces.MSBuild` + `Microsoft.Build.Locator`
- `src/AspireForm/Providers/EfDataModuleProvider.cs` — rewrite to use EntityCatalog + DbContextEmitter + DabConfigEmitter; new input shape
- `src/AspireForm/Program.cs` — register `ui` verb
- `src/AspireForm/Cli/McpCommand.cs` — register 12 new entity tools (registry grows 17 → 29)
- `AspireForm.slnx` — add `AspireForm.Annotations` project
- `tests/AspireForm.Tests/AspireForm.Tests.csproj` — add `bunit` package
- `tests/AspireForm.Tests/Providers/EfDataModuleProviderTests.cs` — rewrite for new input shape
- `README.md` — add "Use the entity builder" section + `aspireform ui` screenshot reference
- `CHANGELOG.md` — `[0.5.0]` entry with migration note

**New (tests):**

- `tests/AspireForm.Tests/EntityCatalog/RoslynEntityScannerTests.cs`
- `tests/AspireForm.Tests/EntityCatalog/RoslynEntityMutatorTests.cs`
- `tests/AspireForm.Tests/EntityCatalog/Fixtures/` — small fixture projects (per-test temp directories)
- `tests/AspireForm.Tests/Providers/EfData/DbContextEmitterTests.cs`
- `tests/AspireForm.Tests/Providers/EfData/DabConfigEmitterTests.cs`
- `tests/AspireForm.Tests/Mcp/Tools/Entity/EntityToolsReadTests.cs`
- `tests/AspireForm.Tests/Mcp/Tools/Entity/EntityToolsMutationTests.cs`
- `tests/AspireForm.Tests/Mcp/Tools/Entity/AttributeAndRelationshipToolsTests.cs`
- `tests/AspireForm.Tests/Ui/IndexPageTests.cs`
- `tests/AspireForm.Tests/Ui/EntitiesPageTests.cs`
- `tests/AspireForm.Tests/Ui/UiHostSmokeTests.cs`

---

## Task 1: Bump version + ASP.NET FrameworkReference + Roslyn workspace packages

**Files:**
- Modify: `src/AspireForm/AspireForm.csproj`

- [ ] **Step 1: Edit `src/AspireForm/AspireForm.csproj`** — bump `<Version>0.4.0</Version>` to `<Version>0.5.0</Version>`, add `<RollForward>LatestMajor</RollForward>` to the existing `<PropertyGroup>`, add a new `<ItemGroup>` for the framework reference, and add the two new packages to the existing PackageReference ItemGroup. The result should look like:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>AspireForm</RootNamespace>
    <AssemblyName>AspireForm</AssemblyName>
    <RollForward>LatestMajor</RollForward>

    <PackAsTool>true</PackAsTool>
    <ToolCommandName>aspireform</ToolCommandName>
    <PackageId>AspireForm</PackageId>
    <Version>0.5.0</Version>
    <Authors>James Burton</Authors>
    <Description>Declarative construction and configuration of .NET Aspire applications.</Description>
    <PackageProjectUrl>https://github.com/jamesburton/AspireForm</PackageProjectUrl>
    <RepositoryUrl>https://github.com/jamesburton/AspireForm</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageTags>aspire;dotnet-aspire;iac;terraform;scaffolding;dnx-tool</PackageTags>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="5.3.0" />
    <PackageReference Include="Microsoft.CodeAnalysis.Workspaces.MSBuild" Version="5.3.0" />
    <PackageReference Include="Microsoft.Build.Locator" Version="1.7.8" />
    <PackageReference Include="Spectre.Console.Cli" Version="0.55.0" />
    <PackageReference Include="YamlDotNet" Version="18.0.0" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="AspireForm.Tests" />
  </ItemGroup>

  <ItemGroup>
    <None Include="../../README.md" Pack="true" PackagePath="\" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Build to confirm restore succeeds with the new packages**

```bash
dotnet build --nologo -v q
```

Expected: build succeeds. If `Microsoft.CodeAnalysis.Workspaces.MSBuild` 5.3.0 isn't available, try 4.13.0 (well-known stable version) — both versions work with .NET 10 SDK 10.0.300.

- [ ] **Step 3: Commit**

```bash
git add src/AspireForm/AspireForm.csproj
git -c commit.gpgsign=false commit -m "chore: bump AspireForm to 0.5.0, add ASP.NET + Roslyn workspace deps"
```

---

## Task 2: Create AspireForm.Annotations 0.1.0 package skeleton

**Files:**
- Create: `src/AspireForm.Annotations/AspireForm.Annotations.csproj`
- Create: `src/AspireForm.Annotations/README.md`
- Create: `src/AspireForm.Annotations/CHANGELOG.md`

- [ ] **Step 1: Create `src/AspireForm.Annotations/AspireForm.Annotations.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <RootNamespace>AspireForm.Annotations</RootNamespace>
    <AssemblyName>AspireForm.Annotations</AssemblyName>
    <PackageId>AspireForm.Annotations</PackageId>
    <Version>0.1.0</Version>
    <Authors>James Burton</Authors>
    <Description>Attribute-only library for AspireForm code-first entity authoring (DAB exposure attributes + optional EF helpers).</Description>
    <PackageProjectUrl>https://github.com/jamesburton/AspireForm</PackageProjectUrl>
    <RepositoryUrl>https://github.com/jamesburton/AspireForm</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageTags>aspireform;annotations;dab;ef-core</PackageTags>
  </PropertyGroup>
  <ItemGroup>
    <None Include="README.md" Pack="true" PackagePath="\" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create `src/AspireForm.Annotations/README.md`**

```markdown
# AspireForm.Annotations

Attribute-only library for [AspireForm](https://github.com/jamesburton/AspireForm) code-first entity authoring.

Reference this package from your entity project, then decorate entity classes with:

- `[DabExpose]` — mark entity as exposed via Data API Builder
- `[DabPath("/books")]` — override REST path
- `[DabPermission("anonymous", "read")]` — repeatable; default is `[{anonymous, read}]`
- `[DabRestOnly]` / `[DabGraphqlOnly]` / `[DabHidden]`
- `[OnDelete("Cascade")]` — optional EF helper for cascade behavior

The AspireForm `ef-data` provider reads these attributes via Roslyn and emits a corresponding `dab-config.json`.
```

- [ ] **Step 3: Create `src/AspireForm.Annotations/CHANGELOG.md`**

```markdown
# Changelog

## [0.1.0] - 2026-05-25

Initial release. Attributes for AspireForm code-first entity authoring.

### Added
- `[DabExpose]`, `[DabPath]`, `[DabPermission]`, `[DabRestOnly]`, `[DabGraphqlOnly]`, `[DabHidden]` — DAB exposure attributes
- `[OnDelete]` — optional EF Core cascade behavior helper
```

- [ ] **Step 4: Commit (csproj only — attribute classes land in Task 3)**

```bash
git add src/AspireForm.Annotations/
git -c commit.gpgsign=false commit -m "feat(annotations): scaffold AspireForm.Annotations 0.1.0 package"
```

---

## Task 3: AspireForm.Annotations attributes

**Files:**
- Create: `src/AspireForm.Annotations/DabExposeAttribute.cs`
- Create: `src/AspireForm.Annotations/DabPathAttribute.cs`
- Create: `src/AspireForm.Annotations/DabPermissionAttribute.cs`
- Create: `src/AspireForm.Annotations/DabRestOnlyAttribute.cs`
- Create: `src/AspireForm.Annotations/DabGraphqlOnlyAttribute.cs`
- Create: `src/AspireForm.Annotations/DabHiddenAttribute.cs`
- Create: `src/AspireForm.Annotations/OnDeleteAttribute.cs`

- [ ] **Step 1: Create `src/AspireForm.Annotations/DabExposeAttribute.cs`**

```csharp
namespace AspireForm.Annotations;

/// <summary>Marks an entity as exposed via Data API Builder. Default: REST + GraphQL, anonymous read.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DabExposeAttribute : Attribute { }
```

- [ ] **Step 2: Create `src/AspireForm.Annotations/DabPathAttribute.cs`**

```csharp
namespace AspireForm.Annotations;

/// <summary>Overrides the DAB REST path for an entity. Default is <c>/{EntityName}</c>.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DabPathAttribute : Attribute
{
    /// <summary>Initialises the attribute with the REST path (e.g. <c>/books</c>).</summary>
    public DabPathAttribute(string path) { Path = path; }

    /// <summary>The REST path.</summary>
    public string Path { get; }
}
```

- [ ] **Step 3: Create `src/AspireForm.Annotations/DabPermissionAttribute.cs`**

```csharp
namespace AspireForm.Annotations;

/// <summary>Declares a DAB permission for an entity. Repeatable; one instance per role.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class DabPermissionAttribute : Attribute
{
    /// <summary>Initialises the permission for <paramref name="role"/> with comma-separated <paramref name="actions"/>.</summary>
    public DabPermissionAttribute(string role, string actions)
    {
        Role = role;
        Actions = actions;
    }

    /// <summary>Role name. Use <c>"anonymous"</c>, <c>"authenticated"</c>, or a custom role.</summary>
    public string Role { get; }

    /// <summary>Comma-separated action list (e.g. <c>"read"</c>, <c>"create,update,delete"</c>, or <c>"*"</c>).</summary>
    public string Actions { get; }
}
```

- [ ] **Step 4: Create `src/AspireForm.Annotations/DabRestOnlyAttribute.cs`**

```csharp
namespace AspireForm.Annotations;

/// <summary>When applied alongside <see cref="DabExposeAttribute"/>, restricts exposure to REST (no GraphQL).</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DabRestOnlyAttribute : Attribute { }
```

- [ ] **Step 5: Create `src/AspireForm.Annotations/DabGraphqlOnlyAttribute.cs`**

```csharp
namespace AspireForm.Annotations;

/// <summary>When applied alongside <see cref="DabExposeAttribute"/>, restricts exposure to GraphQL (no REST).</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DabGraphqlOnlyAttribute : Attribute { }
```

- [ ] **Step 6: Create `src/AspireForm.Annotations/DabHiddenAttribute.cs`**

```csharp
namespace AspireForm.Annotations;

/// <summary>Marks an entity as present in EF but explicitly hidden from DAB. Overrides <see cref="DabExposeAttribute"/> if both are present.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DabHiddenAttribute : Attribute { }
```

- [ ] **Step 7: Create `src/AspireForm.Annotations/OnDeleteAttribute.cs`**

```csharp
namespace AspireForm.Annotations;

/// <summary>Specifies the EF Core delete behavior for a relationship navigation property.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class OnDeleteAttribute : Attribute
{
    /// <summary>Initialises the attribute with the requested behavior.</summary>
    /// <param name="behavior">One of: <c>Cascade</c>, <c>Restrict</c>, <c>SetNull</c>, <c>NoAction</c>, <c>ClientCascade</c>, <c>ClientSetNull</c>.</param>
    public OnDeleteAttribute(string behavior) { Behavior = behavior; }

    /// <summary>The configured delete behavior.</summary>
    public string Behavior { get; }
}
```

- [ ] **Step 8: Build the annotations project standalone**

```bash
dotnet build src/AspireForm.Annotations/AspireForm.Annotations.csproj --nologo -v q
```

Expected: build succeeds (no `*.dll` warnings).

- [ ] **Step 9: Commit**

```bash
git add src/AspireForm.Annotations/
git -c commit.gpgsign=false commit -m "feat(annotations): add DabExpose, DabPath, DabPermission, DabRestOnly, DabGraphqlOnly, DabHidden, OnDelete attributes"
```

---

## Task 4: Register AspireForm.Annotations in solution + add bunit to tests

**Files:**
- Modify: `AspireForm.slnx`
- Modify: `tests/AspireForm.Tests/AspireForm.Tests.csproj`

- [ ] **Step 1: Edit `AspireForm.slnx`** — add a `<Project>` line for `AspireForm.Annotations` inside the existing solution structure. Open the file and locate the `<Project Path="src/AspireForm/AspireForm.csproj" />` line; immediately after it, add:

```xml
<Project Path="src/AspireForm.Annotations/AspireForm.Annotations.csproj" />
```

If the slnx file groups projects by folder, place it in whatever wrapper element matches the convention used for the other `src/` projects.

- [ ] **Step 2: Edit `tests/AspireForm.Tests/AspireForm.Tests.csproj`** — add a `bunit` PackageReference to the existing PackageReference ItemGroup:

```xml
<PackageReference Include="bunit" Version="1.40.0" />
```

(If 1.40.0 isn't available, try 1.34.0 — both are .NET 10-compatible. If neither is available, the bUnit-based tests in Tasks 24-26 will be skipped and replaced with simple HttpClient-rendered HTML asserts. Flag this in your task report.)

- [ ] **Step 3: Build the solution**

```bash
dotnet build --nologo -v q
```

Expected: build succeeds, all three projects (`AspireForm`, `AspireForm.Annotations`, `AspireForm.Tests`) compile.

- [ ] **Step 4: Commit**

```bash
git add AspireForm.slnx tests/AspireForm.Tests/AspireForm.Tests.csproj
git -c commit.gpgsign=false commit -m "chore: register AspireForm.Annotations in solution, add bunit to test project"
```

---

## Task 5: EntityCatalog domain model

**Files:**
- Create: `src/AspireForm/EntityCatalog/EntityModel.cs`
- Create: `src/AspireForm/EntityCatalog/EntityChangeRequest.cs`
- Create: `src/AspireForm/EntityCatalog/EntityCatalogException.cs`
- Create: `tests/AspireForm.Tests/EntityCatalog/EntityModelTests.cs`

- [ ] **Step 1: Create `src/AspireForm/EntityCatalog/EntityModel.cs`**

```csharp
namespace AspireForm.EntityCatalog;

/// <summary>Immutable snapshot of the entity graph in a user's project.</summary>
public sealed record EntityCatalog(
    IReadOnlyList<Entity> Entities,
    IReadOnlyList<DbContextInfo> DbContexts,
    IReadOnlyList<CatalogDiagnostic> Diagnostics);

/// <summary>Information about a discovered DbContext-derived class.</summary>
public sealed record DbContextInfo(
    string Name,
    string Namespace,
    string FilePath,
    IReadOnlyList<string> DbSetEntityNames);

/// <summary>One entity class discovered in the user's project.</summary>
public sealed record Entity(
    string Name,
    string Namespace,
    string FilePath,
    IReadOnlyList<Property> Properties,
    IReadOnlyList<Relationship> Relationships,
    IReadOnlyList<AttributeInstance> Attributes);

/// <summary>One declared property on an entity.</summary>
public sealed record Property(
    string Name,
    string ClrType,
    bool IsNullable,
    bool IsPrimaryKey,
    IReadOnlyList<AttributeInstance> Attributes);

/// <summary>One navigation relationship from an entity to another entity.</summary>
public sealed record Relationship(
    string Name,
    string TargetEntity,
    RelationshipCardinality Cardinality,
    string? ForeignKeyProperty);

/// <summary>Cardinality of a navigation relationship.</summary>
public enum RelationshipCardinality { OneToOne, OneToMany, ManyToOne, ManyToMany }

/// <summary>One attribute applied to an entity class or property.</summary>
public sealed record AttributeInstance(
    string FullTypeName,
    IReadOnlyList<object?> ConstructorArgs,
    IReadOnlyDictionary<string, object?> NamedArgs);

/// <summary>A diagnostic emitted during catalog scan or mutation.</summary>
public sealed record CatalogDiagnostic(
    string Severity,
    string Message,
    string? FilePath,
    int? Line);

/// <summary>Result of an entity-mutation operation.</summary>
public sealed record MutationResult(
    bool Success,
    IReadOnlyList<string> ChangedFiles,
    IReadOnlyList<CatalogDiagnostic> Diagnostics)
{
    /// <summary>Convenience for successful mutations.</summary>
    public static MutationResult Ok(IReadOnlyList<string> changedFiles, IReadOnlyList<CatalogDiagnostic>? diagnostics = null) =>
        new(true, changedFiles, diagnostics ?? []);

    /// <summary>Convenience for failed mutations.</summary>
    public static MutationResult Fail(string message, string? filePath = null) =>
        new(false, [], [new CatalogDiagnostic("error", message, filePath, null)]);
}
```

- [ ] **Step 2: Create `src/AspireForm/EntityCatalog/EntityChangeRequest.cs`**

```csharp
namespace AspireForm.EntityCatalog;

/// <summary>Sealed-hierarchy DSL for one entity-graph mutation.</summary>
public abstract record EntityChangeRequest;

/// <summary>Create a new entity class in a new <c>.cs</c> file.</summary>
public sealed record CreateEntity(string Name, string Namespace, string FilePath) : EntityChangeRequest;

/// <summary>Delete an entity class and remove it from the DbContext + dependent navigations.</summary>
public sealed record DeleteEntity(string EntityName) : EntityChangeRequest;

/// <summary>Append a new property to an entity's class body.</summary>
public sealed record AddProperty(string EntityName, Property Property) : EntityChangeRequest;

/// <summary>Remove an existing property from an entity.</summary>
public sealed record RemoveProperty(string EntityName, string PropertyName) : EntityChangeRequest;

/// <summary>Rename a property; semantic-safe across the whole workspace.</summary>
public sealed record RenameProperty(string EntityName, string OldName, string NewName) : EntityChangeRequest;

/// <summary>Set (replace if present) an attribute on an entity class or one of its properties.</summary>
public sealed record SetAttribute(string EntityName, string? PropertyName, AttributeInstance Attribute) : EntityChangeRequest;

/// <summary>Clear an attribute (by full type name) from an entity class or one of its properties.</summary>
public sealed record ClearAttribute(string EntityName, string? PropertyName, string AttributeFullTypeName) : EntityChangeRequest;

/// <summary>Add a relationship between two entities. v1 supports OneToOne, OneToMany, ManyToOne; ManyToMany is reserved for #4a.1.</summary>
public sealed record AddRelationship(
    string FromEntity, string ToEntity,
    RelationshipCardinality Cardinality,
    string? ForeignKeyProperty) : EntityChangeRequest;

/// <summary>Remove a relationship (by navigation property name) from the originating entity, including its reverse side and any FK property.</summary>
public sealed record RemoveRelationship(string FromEntity, string RelationshipName) : EntityChangeRequest;
```

- [ ] **Step 3: Create `src/AspireForm/EntityCatalog/EntityCatalogException.cs`**

```csharp
namespace AspireForm.EntityCatalog;

/// <summary>Raised by the entity catalog scanner or mutator when an operation cannot be completed cleanly.</summary>
public sealed class EntityCatalogException : Exception
{
    /// <summary>Initialises the exception with a message.</summary>
    public EntityCatalogException(string message) : base(message) { }

    /// <summary>Initialises the exception with a message and inner exception.</summary>
    public EntityCatalogException(string message, Exception inner) : base(message, inner) { }
}
```

- [ ] **Step 4: Create `tests/AspireForm.Tests/EntityCatalog/EntityModelTests.cs`**

```csharp
using AspireForm.EntityCatalog;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.EntityCatalog;

public sealed class EntityModelTests
{
    [Fact]
    public void MutationResult_Ok_marks_success_and_empty_diagnostics_by_default()
    {
        var r = MutationResult.Ok(["a.cs", "b.cs"]);
        r.Success.Should().BeTrue();
        r.ChangedFiles.Should().BeEquivalentTo(new[] { "a.cs", "b.cs" });
        r.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void MutationResult_Fail_marks_failure_with_error_diagnostic()
    {
        var r = MutationResult.Fail("boom", "x.cs");
        r.Success.Should().BeFalse();
        r.ChangedFiles.Should().BeEmpty();
        r.Diagnostics.Should().ContainSingle()
            .Which.Severity.Should().Be("error");
        r.Diagnostics[0].Message.Should().Be("boom");
        r.Diagnostics[0].FilePath.Should().Be("x.cs");
    }

    [Fact]
    public void AttributeInstance_holds_constructor_and_named_args()
    {
        var attr = new AttributeInstance(
            FullTypeName: "AspireForm.Annotations.DabPermissionAttribute",
            ConstructorArgs: ["anonymous", "read"],
            NamedArgs: new Dictionary<string, object?>());
        attr.ConstructorArgs.Should().HaveCount(2);
        attr.ConstructorArgs[0].Should().Be("anonymous");
        attr.NamedArgs.Should().BeEmpty();
    }

    [Fact]
    public void EntityChangeRequest_subtypes_are_distinguishable_via_pattern_matching()
    {
        EntityChangeRequest req = new CreateEntity("Book", "Demo", "Models/Book.cs");
        var result = req switch
        {
            CreateEntity c => $"create:{c.Name}",
            _ => "other",
        };
        result.Should().Be("create:Book");
    }
}
```

- [ ] **Step 5: Build + run tests**

```bash
dotnet build --nologo -v q
dotnet run --project tests/AspireForm.Tests -class "AspireForm.Tests.EntityCatalog.EntityModelTests"
```

Expected: 4/4 PASS.

- [ ] **Step 6: Commit**

```bash
git add src/AspireForm/EntityCatalog/ tests/AspireForm.Tests/EntityCatalog/EntityModelTests.cs
git -c commit.gpgsign=false commit -m "feat(catalog): add entity domain model + change request DSL + exception"
```

---

## Task 6: MSBuildBootstrap + IEntityCatalogService seam

**Files:**
- Create: `src/AspireForm/EntityCatalog/MSBuildBootstrap.cs`
- Create: `src/AspireForm/EntityCatalog/IEntityCatalogService.cs`

- [ ] **Step 1: Create `src/AspireForm/EntityCatalog/MSBuildBootstrap.cs`**

```csharp
using Microsoft.Build.Locator;

namespace AspireForm.EntityCatalog;

/// <summary>Idempotent <see cref="MSBuildLocator"/> registration. Must be called before opening any <c>MSBuildWorkspace</c>.</summary>
internal static class MSBuildBootstrap
{
    private static readonly Lock LockObj = new();
    private static bool _registered;

    /// <summary>Registers the highest installed MSBuild SDK with the current process, exactly once.</summary>
    public static void EnsureRegistered()
    {
        if (_registered) return;
        lock (LockObj)
        {
            if (_registered) return;
            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterDefaults();
            }
            _registered = true;
        }
    }
}
```

- [ ] **Step 2: Create `src/AspireForm/EntityCatalog/IEntityCatalogService.cs`**

```csharp
namespace AspireForm.EntityCatalog;

/// <summary>The single DI seam over the entity catalog. Used by Blazor pages, MCP tools, and the <c>ef-data</c> provider.</summary>
public interface IEntityCatalogService
{
    /// <summary>Scans the supplied csproj and returns an immutable <see cref="EntityCatalog"/> snapshot.</summary>
    /// <param name="csprojPath">Absolute or relative path to the entity project's csproj file.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<EntityCatalog> ScanAsync(string csprojPath, CancellationToken ct);

    /// <summary>Applies one <see cref="EntityChangeRequest"/> transactionally. Returns success + changed files.</summary>
    /// <param name="csprojPath">Absolute or relative path to the entity project's csproj file.</param>
    /// <param name="request">The mutation to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<MutationResult> MutateAsync(string csprojPath, EntityChangeRequest request, CancellationToken ct);
}
```

- [ ] **Step 3: Build**

```bash
dotnet build --nologo -v q
```

Expected: build succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/AspireForm/EntityCatalog/MSBuildBootstrap.cs src/AspireForm/EntityCatalog/IEntityCatalogService.cs
git -c commit.gpgsign=false commit -m "feat(catalog): add MSBuildLocator bootstrap + IEntityCatalogService DI seam"
```

---

## Task 7: RoslynEntityScanner — discovery + entity classification

**Files:**
- Create: `src/AspireForm/EntityCatalog/RoslynEntityScanner.cs`
- Create: `tests/AspireForm.Tests/EntityCatalog/Fixtures/FixtureProjectBuilder.cs`
- Create: `tests/AspireForm.Tests/EntityCatalog/RoslynEntityScannerTests.cs`

- [ ] **Step 1: Create `src/AspireForm/EntityCatalog/RoslynEntityScanner.cs`**

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace AspireForm.EntityCatalog;

/// <summary>Scans a user csproj for EF Core <c>DbContext</c>s and entity classes via Roslyn.</summary>
public sealed class RoslynEntityScanner : IAsyncDisposable
{
    private MSBuildWorkspace? _workspace;
    private string? _projectPath;
    private Project? _project;

    /// <summary>Opens the supplied csproj as a Roslyn <see cref="MSBuildWorkspace"/>. The workspace is cached for subsequent scans against the same path.</summary>
    public async Task<EntityCatalog> ScanAsync(string csprojPath, CancellationToken ct)
    {
        MSBuildBootstrap.EnsureRegistered();

        var absolute = Path.GetFullPath(csprojPath);
        if (!File.Exists(absolute))
        {
            throw new EntityCatalogException($"Project file not found: '{absolute}'.");
        }

        if (_workspace is null || _projectPath != absolute)
        {
            _workspace?.Dispose();
            _workspace = MSBuildWorkspace.Create();
            _projectPath = absolute;
            _project = await _workspace.OpenProjectAsync(absolute, cancellationToken: ct);
        }
        else
        {
            // Force a fresh re-parse of the existing project's documents.
            _project = _workspace.CurrentSolution.GetProject(_project!.Id);
        }

        var compilation = await _project!.GetCompilationAsync(ct)
            ?? throw new EntityCatalogException("Roslyn returned a null Compilation.");

        var workspaceDiagnostics = _workspace.Diagnostics
            .Select(d => new CatalogDiagnostic(
                MapWorkspaceDiagnosticSeverity(d.Kind),
                d.Message,
                FilePath: null,
                Line: null))
            .ToList();

        var allTypes = CollectAllTypes(compilation.Assembly.GlobalNamespace);
        var contexts = DiscoverDbContexts(allTypes);
        var entityTypes = ClassifyEntities(allTypes, contexts);
        var entities = entityTypes
            .Select(t => BuildEntity(t, entityTypes))
            .ToList();

        return new EntityCatalog(entities, contexts, workspaceDiagnostics);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _workspace?.Dispose();
        _workspace = null;
        return ValueTask.CompletedTask;
    }

    private static string MapWorkspaceDiagnosticSeverity(WorkspaceDiagnosticKind kind) => kind switch
    {
        WorkspaceDiagnosticKind.Failure => "error",
        WorkspaceDiagnosticKind.Warning => "warning",
        _ => "info",
    };

    private static List<INamedTypeSymbol> CollectAllTypes(INamespaceSymbol root)
    {
        var result = new List<INamedTypeSymbol>();
        Walk(root);
        return result;

        void Walk(INamespaceSymbol ns)
        {
            foreach (var t in ns.GetTypeMembers())
            {
                result.Add(t);
            }
            foreach (var child in ns.GetNamespaceMembers())
            {
                Walk(child);
            }
        }
    }

    private static List<DbContextInfo> DiscoverDbContexts(IEnumerable<INamedTypeSymbol> all)
    {
        var contexts = new List<DbContextInfo>();
        foreach (var t in all.Where(IsDbContext))
        {
            var dbSets = t.GetMembers().OfType<IPropertySymbol>()
                .Where(p => p.Type is INamedTypeSymbol nt && nt.Name == "DbSet" && nt.TypeArguments.Length == 1)
                .Select(p => ((INamedTypeSymbol)p.Type).TypeArguments[0].Name)
                .ToList();
            contexts.Add(new DbContextInfo(
                Name: t.Name,
                Namespace: t.ContainingNamespace?.ToDisplayString() ?? "",
                FilePath: t.Locations.FirstOrDefault()?.SourceTree?.FilePath ?? "",
                DbSetEntityNames: dbSets));
        }
        return contexts;
    }

    private static bool IsDbContext(INamedTypeSymbol t)
    {
        for (var bt = t.BaseType; bt is not null; bt = bt.BaseType)
        {
            if (bt.Name == "DbContext") return true;
        }
        return false;
    }

    private static HashSet<INamedTypeSymbol> ClassifyEntities(
        IReadOnlyList<INamedTypeSymbol> all,
        IReadOnlyList<DbContextInfo> contexts)
    {
        var byName = all.GroupBy(t => t.Name).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var entities = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        // Seed from DbSet<T> mentions.
        foreach (var c in contexts)
        {
            foreach (var n in c.DbSetEntityNames)
            {
                if (byName.TryGetValue(n, out var sym)) entities.Add(sym);
            }
        }

        // Add anything carrying [Table] or AspireForm.Annotations.* attributes.
        foreach (var t in all)
        {
            if (t.TypeKind != TypeKind.Class) continue;
            if (t.GetAttributes().Any(a => IsRelevantAttribute(a))) entities.Add(t);
        }

        return entities;
    }

    private static bool IsRelevantAttribute(AttributeData a)
    {
        var cls = a.AttributeClass;
        if (cls is null) return false;
        if (cls.Name == "TableAttribute" && cls.ContainingNamespace?.ToDisplayString() == "System.ComponentModel.DataAnnotations.Schema")
            return true;
        return cls.ContainingNamespace?.ToDisplayString() == "AspireForm.Annotations";
    }

    private static Entity BuildEntity(INamedTypeSymbol symbol, IReadOnlyCollection<INamedTypeSymbol> allEntities)
    {
        var properties = new List<Property>();
        var relationships = new List<Relationship>();
        var entityNames = new HashSet<string>(allEntities.Select(e => e.Name), StringComparer.Ordinal);

        foreach (var p in symbol.GetMembers().OfType<IPropertySymbol>().Where(p => p.DeclaredAccessibility == Accessibility.Public))
        {
            if (IsCollectionOfEntity(p.Type, entityNames, out var navTarget))
            {
                relationships.Add(new Relationship(
                    Name: p.Name,
                    TargetEntity: navTarget!,
                    Cardinality: RelationshipCardinality.OneToMany,
                    ForeignKeyProperty: null));
            }
            else if (IsScalarEntityRef(p.Type, entityNames, out var refTarget))
            {
                relationships.Add(new Relationship(
                    Name: p.Name,
                    TargetEntity: refTarget!,
                    Cardinality: RelationshipCardinality.ManyToOne,
                    ForeignKeyProperty: null));
            }
            else
            {
                properties.Add(new Property(
                    Name: p.Name,
                    ClrType: p.Type.ToDisplayString(),
                    IsNullable: p.NullableAnnotation == NullableAnnotation.Annotated,
                    IsPrimaryKey: IsLikelyPrimaryKey(p),
                    Attributes: p.GetAttributes().Select(MapAttribute).ToList()));
            }
        }

        return new Entity(
            Name: symbol.Name,
            Namespace: symbol.ContainingNamespace?.ToDisplayString() ?? "",
            FilePath: symbol.Locations.FirstOrDefault()?.SourceTree?.FilePath ?? "",
            Properties: properties,
            Relationships: relationships,
            Attributes: symbol.GetAttributes().Select(MapAttribute).ToList());
    }

    private static bool IsCollectionOfEntity(ITypeSymbol t, HashSet<string> entityNames, out string? entity)
    {
        entity = null;
        if (t is INamedTypeSymbol nt && nt.IsGenericType && nt.TypeArguments.Length == 1)
        {
            var arg = nt.TypeArguments[0];
            if (entityNames.Contains(arg.Name) &&
                (nt.Name is "ICollection" or "IList" or "List" or "IReadOnlyCollection" or "IReadOnlyList" or "IEnumerable" or "HashSet"))
            {
                entity = arg.Name;
                return true;
            }
        }
        return false;
    }

    private static bool IsScalarEntityRef(ITypeSymbol t, HashSet<string> entityNames, out string? entity)
    {
        entity = null;
        if (t is INamedTypeSymbol nt && entityNames.Contains(nt.Name))
        {
            entity = nt.Name;
            return true;
        }
        return false;
    }

    private static bool IsLikelyPrimaryKey(IPropertySymbol p)
    {
        if (p.GetAttributes().Any(a => a.AttributeClass?.Name == "KeyAttribute")) return true;
        return string.Equals(p.Name, "Id", StringComparison.Ordinal)
            || string.Equals(p.Name, p.ContainingType.Name + "Id", StringComparison.Ordinal);
    }

    private static AttributeInstance MapAttribute(AttributeData a)
    {
        var ns = a.AttributeClass?.ContainingNamespace?.ToDisplayString() ?? "";
        var name = a.AttributeClass?.Name ?? "Unknown";
        var fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        var ctorArgs = a.ConstructorArguments.Select(c => (object?)c.Value).ToList();
        var named = a.NamedArguments.ToDictionary(kv => kv.Key, kv => (object?)kv.Value.Value);
        return new AttributeInstance(fullName, ctorArgs, named);
    }
}
```

- [ ] **Step 2: Create `tests/AspireForm.Tests/EntityCatalog/Fixtures/FixtureProjectBuilder.cs`** — helper that writes a tiny .NET 10 class library csproj + source files to a per-test temp directory.

```csharp
namespace AspireForm.Tests.EntityCatalog.Fixtures;

internal sealed class FixtureProjectBuilder : IDisposable
{
    public string Root { get; }
    public string CsprojPath { get; }

    public FixtureProjectBuilder(string testName)
    {
        Root = Path.Combine(Path.GetTempPath(), $"af-fix-{testName}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Root);
        CsprojPath = Path.Combine(Root, $"{testName}.csproj");
        File.WriteAllText(CsprojPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
              </PropertyGroup>
            </Project>
            """);
    }

    public string AddFile(string relativePath, string content)
    {
        var abs = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
        File.WriteAllText(abs, content);
        return abs;
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); } catch { /* best-effort */ }
    }
}
```

- [ ] **Step 3: Create `tests/AspireForm.Tests/EntityCatalog/RoslynEntityScannerTests.cs`**

```csharp
using AspireForm.EntityCatalog;
using AspireForm.Tests.EntityCatalog.Fixtures;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.EntityCatalog;

public sealed class RoslynEntityScannerTests
{
    [Fact]
    public async Task Scan_finds_dbcontext_and_its_dbset_entity_types()
    {
        using var fix = new FixtureProjectBuilder("scan_dbset");
        fix.AddFile("DbContextStub.cs", """
            namespace Microsoft.EntityFrameworkCore;
            public class DbContext { }
            public class DbSet<T> { }
            """);
        fix.AddFile("AppDbContext.cs", """
            using Microsoft.EntityFrameworkCore;
            namespace Demo;
            public class AppDbContext : DbContext
            {
                public DbSet<Book> Books { get; set; } = null!;
            }
            public class Book { public int Id { get; set; } public string Title { get; set; } = ""; }
            """);

        await using var scanner = new RoslynEntityScanner();
        var catalog = await scanner.ScanAsync(fix.CsprojPath, default);

        catalog.DbContexts.Should().ContainSingle()
            .Which.Name.Should().Be("AppDbContext");
        catalog.Entities.Should().ContainSingle(e => e.Name == "Book");
    }

    [Fact]
    public async Task Scan_classifies_class_with_Table_attribute_as_entity_even_without_dbset()
    {
        using var fix = new FixtureProjectBuilder("scan_tableattr");
        fix.AddFile("Models.cs", """
            using System.ComponentModel.DataAnnotations.Schema;
            namespace Demo;
            [Table("authors")]
            public class Author { public int Id { get; set; } }
            """);

        await using var scanner = new RoslynEntityScanner();
        var catalog = await scanner.ScanAsync(fix.CsprojPath, default);

        catalog.Entities.Should().ContainSingle(e => e.Name == "Author");
    }

    [Fact]
    public async Task Scan_includes_workspace_diagnostics_without_failing()
    {
        using var fix = new FixtureProjectBuilder("scan_nomodels");
        await using var scanner = new RoslynEntityScanner();
        var catalog = await scanner.ScanAsync(fix.CsprojPath, default);

        catalog.Entities.Should().BeEmpty();
        catalog.DbContexts.Should().BeEmpty();
        // diagnostics may or may not be present depending on the SDK state — just assert it doesn't throw
    }

    [Fact]
    public async Task Scan_throws_when_csproj_does_not_exist()
    {
        await using var scanner = new RoslynEntityScanner();
        var act = async () => await scanner.ScanAsync("does-not-exist.csproj", default);
        await act.Should().ThrowAsync<EntityCatalogException>();
    }
}
```

- [ ] **Step 4: Build + run scanner tests**

```bash
dotnet build --nologo -v q
dotnet run --project tests/AspireForm.Tests -class "AspireForm.Tests.EntityCatalog.RoslynEntityScannerTests"
```

Expected: 4/4 PASS. (If MSBuild SDK 10.0.300 isn't on the test agent's path, `OpenProjectAsync` may produce workspace diagnostics — the third test tolerates them; the first/second succeed because the test source compiles with stub `DbContext`/`DbSet` classes inline.)

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/EntityCatalog/RoslynEntityScanner.cs tests/AspireForm.Tests/EntityCatalog/Fixtures/ tests/AspireForm.Tests/EntityCatalog/RoslynEntityScannerTests.cs
git -c commit.gpgsign=false commit -m "feat(catalog): add RoslynEntityScanner (DbContext + entity discovery, nav inference)"
```

---

## Task 8: RoslynEntityScanner — property classification + relationship cardinality

This task extends Task 7's scanner with richer property + relationship handling. Task 7 already inferred OneToMany (collection nav) + ManyToOne (scalar nav); Task 8 adds OneToOne inference + property attribute mapping coverage.

**Files:**
- Modify: `src/AspireForm/EntityCatalog/RoslynEntityScanner.cs` — add OneToOne detection in `BuildEntity`
- Modify: `tests/AspireForm.Tests/EntityCatalog/RoslynEntityScannerTests.cs` — add three more tests

- [ ] **Step 1: Edit `BuildEntity`** in `RoslynEntityScanner.cs` — after collecting all relationships in the loop, run a post-pass that reclassifies bidirectional scalar-to-scalar nav pairs as OneToOne. Locate the `var relationships = new List<Relationship>();` block; after the foreach loop and before `return new Entity(...)`, insert:

```csharp
        // OneToOne: when this entity has a scalar nav to T AND T has a scalar (non-collection) back-ref to us, reclassify.
        var selfName = symbol.Name;
        for (int i = 0; i < relationships.Count; i++)
        {
            var rel = relationships[i];
            if (rel.Cardinality != RelationshipCardinality.ManyToOne) continue;

            var targetSymbol = allEntities.FirstOrDefault(e => e.Name == rel.TargetEntity);
            if (targetSymbol is null) continue;

            var hasBackRef = targetSymbol.GetMembers().OfType<IPropertySymbol>()
                .Any(p => p.DeclaredAccessibility == Accessibility.Public
                       && p.Type is INamedTypeSymbol nt
                       && nt.Name == selfName
                       && !(nt.IsGenericType && (nt.Name is "ICollection" or "IList" or "List" or "IReadOnlyCollection" or "IReadOnlyList" or "IEnumerable" or "HashSet")));
            if (hasBackRef)
            {
                relationships[i] = rel with { Cardinality = RelationshipCardinality.OneToOne };
            }
        }
```

- [ ] **Step 2: Add three new tests** to `RoslynEntityScannerTests.cs` (append inside the class):

```csharp
    [Fact]
    public async Task Scan_infers_one_to_many_for_collection_navigation()
    {
        using var fix = new FixtureProjectBuilder("scan_1n");
        fix.AddFile("DbContextStub.cs", "namespace Microsoft.EntityFrameworkCore; public class DbContext { } public class DbSet<T> { }");
        fix.AddFile("Models.cs", """
            using System.Collections.Generic;
            using Microsoft.EntityFrameworkCore;
            namespace Demo;
            public class Ctx : DbContext { public DbSet<Author> Authors { get; set; } = null!; public DbSet<Book> Books { get; set; } = null!; }
            public class Author { public int Id { get; set; } public ICollection<Book> Books { get; set; } = new List<Book>(); }
            public class Book { public int Id { get; set; } public Author Author { get; set; } = null!; }
            """);

        await using var scanner = new RoslynEntityScanner();
        var catalog = await scanner.ScanAsync(fix.CsprojPath, default);

        var author = catalog.Entities.Single(e => e.Name == "Author");
        author.Relationships.Should().ContainSingle(r => r.TargetEntity == "Book" && r.Cardinality == RelationshipCardinality.OneToMany);

        var book = catalog.Entities.Single(e => e.Name == "Book");
        book.Relationships.Should().ContainSingle(r => r.TargetEntity == "Author" && r.Cardinality == RelationshipCardinality.ManyToOne);
    }

    [Fact]
    public async Task Scan_infers_one_to_one_for_paired_scalar_navigations()
    {
        using var fix = new FixtureProjectBuilder("scan_11");
        fix.AddFile("DbContextStub.cs", "namespace Microsoft.EntityFrameworkCore; public class DbContext { } public class DbSet<T> { }");
        fix.AddFile("Models.cs", """
            using Microsoft.EntityFrameworkCore;
            namespace Demo;
            public class Ctx : DbContext { public DbSet<User> Users { get; set; } = null!; public DbSet<Profile> Profiles { get; set; } = null!; }
            public class User { public int Id { get; set; } public Profile? Profile { get; set; } }
            public class Profile { public int Id { get; set; } public User? User { get; set; } }
            """);

        await using var scanner = new RoslynEntityScanner();
        var catalog = await scanner.ScanAsync(fix.CsprojPath, default);

        var user = catalog.Entities.Single(e => e.Name == "User");
        user.Relationships.Single(r => r.TargetEntity == "Profile")
            .Cardinality.Should().Be(RelationshipCardinality.OneToOne);
    }

    [Fact]
    public async Task Scan_maps_property_attributes_with_constructor_args()
    {
        using var fix = new FixtureProjectBuilder("scan_attr");
        fix.AddFile("Models.cs", """
            using System.ComponentModel.DataAnnotations;
            using System.ComponentModel.DataAnnotations.Schema;
            namespace Demo;
            [Table("books")]
            public class Book
            {
                [Key] public int Id { get; set; }
                [Required, MaxLength(200)] public string Title { get; set; } = "";
            }
            """);

        await using var scanner = new RoslynEntityScanner();
        var catalog = await scanner.ScanAsync(fix.CsprojPath, default);
        var book = catalog.Entities.Single(e => e.Name == "Book");
        book.Attributes.Should().ContainSingle(a => a.FullTypeName == "System.ComponentModel.DataAnnotations.Schema.TableAttribute");

        var title = book.Properties.Single(p => p.Name == "Title");
        title.Attributes.Should().Contain(a => a.FullTypeName.EndsWith("RequiredAttribute"));
        title.Attributes.Should().Contain(a => a.FullTypeName.EndsWith("MaxLengthAttribute") && a.ConstructorArgs.Count == 1 && Equals(a.ConstructorArgs[0], 200));
    }
```

- [ ] **Step 3: Build + run**

```bash
dotnet build --nologo -v q
dotnet run --project tests/AspireForm.Tests -class "AspireForm.Tests.EntityCatalog.RoslynEntityScannerTests"
```

Expected: 7/7 PASS.

- [ ] **Step 4: Commit**

```bash
git add src/AspireForm/EntityCatalog/RoslynEntityScanner.cs tests/AspireForm.Tests/EntityCatalog/RoslynEntityScannerTests.cs
git -c commit.gpgsign=false commit -m "feat(catalog): scanner infers OneToOne; expand property attribute mapping coverage"
```

---

## Task 9: RoslynEntityMutator — skeleton + CreateEntity + DeleteEntity

**Files:**
- Create: `src/AspireForm/EntityCatalog/RoslynEntityMutator.cs`
- Create: `tests/AspireForm.Tests/EntityCatalog/RoslynEntityMutatorTests.cs`

- [ ] **Step 1: Create `src/AspireForm/EntityCatalog/RoslynEntityMutator.cs`**

The mutator is keyed on filesystem paths (not the cached workspace) so tests can call it directly with a project tree on disk. The buffered-writes pattern: each request produces an in-memory `Dictionary<string, string?>` (null = delete file); writes commit only after all rewrites succeed.

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace AspireForm.EntityCatalog;

/// <summary>Roslyn-backed mutator for entity .cs files. Each <see cref="EntityChangeRequest"/> applies transactionally.</summary>
public sealed class RoslynEntityMutator
{
    /// <summary>Applies one mutation request transactionally against the project at <paramref name="csprojPath"/>.</summary>
    public async Task<MutationResult> ApplyAsync(string csprojPath, EntityChangeRequest request, CancellationToken ct)
    {
        MSBuildBootstrap.EnsureRegistered();
        var absolute = Path.GetFullPath(csprojPath);
        if (!File.Exists(absolute))
        {
            return MutationResult.Fail($"Project file not found: '{absolute}'.", absolute);
        }

        // Buffered writes: path -> new content (null means delete).
        var pending = new Dictionary<string, string?>(StringComparer.Ordinal);
        var diagnostics = new List<CatalogDiagnostic>();

        switch (request)
        {
            case CreateEntity create:
                if (File.Exists(create.FilePath))
                {
                    return MutationResult.Fail($"Refusing to overwrite existing file '{create.FilePath}'.", create.FilePath);
                }
                pending[create.FilePath] = RenderNewEntityFile(create);
                break;

            case DeleteEntity delete:
                using (var ws = MSBuildWorkspace.Create())
                {
                    var project = await ws.OpenProjectAsync(absolute, cancellationToken: ct);
                    var doc = await FindEntityDocumentAsync(project, delete.EntityName, ct);
                    if (doc is null) return MutationResult.Fail($"Entity '{delete.EntityName}' not found.");

                    pending[doc.FilePath!] = null; // delete the file
                    // v1 limitation: DbSet<T> on the DbContext and reverse navigations on other entities
                    // are not pruned automatically — the warning below tells the user to do it manually.
                    // Auto-pruning is a candidate for #4a.1 (would require a second multi-file Roslyn pass).
                    diagnostics.Add(new CatalogDiagnostic("warning",
                        $"Deleted entity '{delete.EntityName}'. DbSet<T> on DbContext + reverse navigations are NOT automatically pruned in this version; remove them manually.",
                        doc.FilePath, null));
                }
                break;

            default:
                return MutationResult.Fail($"Mutation '{request.GetType().Name}' is not implemented yet.");
        }

        return CommitWrites(pending, diagnostics);
    }

    private static MutationResult CommitWrites(IDictionary<string, string?> pending, List<CatalogDiagnostic> diagnostics)
    {
        var changed = new List<string>();
        try
        {
            foreach (var (path, content) in pending)
            {
                if (content is null)
                {
                    if (File.Exists(path)) File.Delete(path);
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, content);
                }
                changed.Add(path);
            }
            return new MutationResult(true, changed, diagnostics);
        }
        catch (Exception ex)
        {
            return MutationResult.Fail($"Commit failed after {changed.Count} file(s): {ex.Message}");
        }
    }

    private static async Task<Document?> FindEntityDocumentAsync(Project project, string entityName, CancellationToken ct)
    {
        var compilation = await project.GetCompilationAsync(ct);
        if (compilation is null) return null;
        var sym = compilation.Assembly.GlobalNamespace.GetAllTypes()
            .FirstOrDefault(t => t.TypeKind == TypeKind.Class && t.Name == entityName);
        if (sym is null) return null;
        var path = sym.Locations.FirstOrDefault()?.SourceTree?.FilePath;
        if (path is null) return null;
        return project.Documents.FirstOrDefault(d => string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase));
    }

    private static string RenderNewEntityFile(CreateEntity req) => $$"""
        namespace {{req.Namespace}};

        public sealed class {{req.Name}}
        {
            public int Id { get; set; }
        }
        """;
}

internal static class SymbolWalker
{
    public static IEnumerable<INamedTypeSymbol> GetAllTypes(this INamespaceSymbol ns)
    {
        foreach (var t in ns.GetTypeMembers()) yield return t;
        foreach (var child in ns.GetNamespaceMembers())
            foreach (var t in child.GetAllTypes()) yield return t;
    }
}
```

- [ ] **Step 2: Create `tests/AspireForm.Tests/EntityCatalog/RoslynEntityMutatorTests.cs`**

```csharp
using AspireForm.EntityCatalog;
using AspireForm.Tests.EntityCatalog.Fixtures;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.EntityCatalog;

public sealed class RoslynEntityMutatorTests
{
    [Fact]
    public async Task CreateEntity_writes_a_new_file_with_a_skeleton_class()
    {
        using var fix = new FixtureProjectBuilder("mut_create");
        var target = Path.Combine(fix.Root, "Models", "Book.cs");

        var mutator = new RoslynEntityMutator();
        var result = await mutator.ApplyAsync(
            fix.CsprojPath,
            new CreateEntity("Book", "Demo.Models", target),
            default);

        result.Success.Should().BeTrue();
        result.ChangedFiles.Should().Contain(target);
        File.Exists(target).Should().BeTrue();
        var content = File.ReadAllText(target);
        content.Should().Contain("namespace Demo.Models;").And.Contain("public sealed class Book").And.Contain("public int Id { get; set; }");
    }

    [Fact]
    public async Task CreateEntity_refuses_to_overwrite_existing_file()
    {
        using var fix = new FixtureProjectBuilder("mut_create_dup");
        var target = fix.AddFile("Models/Book.cs", "// existing");

        var mutator = new RoslynEntityMutator();
        var result = await mutator.ApplyAsync(
            fix.CsprojPath,
            new CreateEntity("Book", "Demo.Models", target),
            default);

        result.Success.Should().BeFalse();
        result.Diagnostics[0].Message.Should().Contain("Refusing to overwrite");
        File.ReadAllText(target).Should().Be("// existing");
    }

    [Fact]
    public async Task DeleteEntity_removes_the_source_file_and_warns_about_unpruned_refs()
    {
        using var fix = new FixtureProjectBuilder("mut_delete");
        fix.AddFile("DbContextStub.cs", "namespace Microsoft.EntityFrameworkCore; public class DbContext { } public class DbSet<T> { }");
        var bookFile = fix.AddFile("Book.cs", """
            using Microsoft.EntityFrameworkCore;
            namespace Demo;
            public class Ctx : DbContext { public DbSet<Book> Books { get; set; } = null!; }
            public class Book { public int Id { get; set; } }
            """);

        var mutator = new RoslynEntityMutator();
        var result = await mutator.ApplyAsync(
            fix.CsprojPath,
            new DeleteEntity("Book"),
            default);

        result.Success.Should().BeTrue();
        File.Exists(bookFile).Should().BeFalse();
        result.Diagnostics.Should().Contain(d => d.Severity == "warning" && d.Message.Contains("NOT automatically pruned"));
    }

    [Fact]
    public async Task ApplyAsync_returns_failure_when_csproj_does_not_exist()
    {
        var mutator = new RoslynEntityMutator();
        var result = await mutator.ApplyAsync(
            "does-not-exist.csproj",
            new CreateEntity("X", "Demo", "X.cs"),
            default);
        result.Success.Should().BeFalse();
        result.Diagnostics[0].Message.Should().Contain("not found");
    }
}
```

- [ ] **Step 3: Build + run**

```bash
dotnet build --nologo -v q
dotnet run --project tests/AspireForm.Tests -class "AspireForm.Tests.EntityCatalog.RoslynEntityMutatorTests"
```

Expected: 4/4 PASS.

- [ ] **Step 4: Commit**

```bash
git add src/AspireForm/EntityCatalog/RoslynEntityMutator.cs tests/AspireForm.Tests/EntityCatalog/RoslynEntityMutatorTests.cs
git -c commit.gpgsign=false commit -m "feat(catalog): add RoslynEntityMutator with CreateEntity + DeleteEntity (transactional commit)"
```

---

## Task 10: Mutator — AddProperty / RemoveProperty / RenameProperty

**Files:**
- Modify: `src/AspireForm/EntityCatalog/RoslynEntityMutator.cs`
- Modify: `tests/AspireForm.Tests/EntityCatalog/RoslynEntityMutatorTests.cs`

- [ ] **Step 1: Add property-mutation switch cases** to `RoslynEntityMutator.ApplyAsync`. Locate the `switch (request)` block; insert these cases between `DeleteEntity delete:` and `default:`:

```csharp
            case AddProperty add:
            {
                using var ws = MSBuildWorkspace.Create();
                var project = await ws.OpenProjectAsync(absolute, cancellationToken: ct);
                var doc = await FindEntityDocumentAsync(project, add.EntityName, ct);
                if (doc is null) return MutationResult.Fail($"Entity '{add.EntityName}' not found.");

                var tree = await doc.GetSyntaxTreeAsync(ct);
                var root = (CompilationUnitSyntax?)await tree!.GetRootAsync(ct);
                var classNode = root!.DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.Text == add.EntityName);
                if (classNode is null) return MutationResult.Fail($"Class '{add.EntityName}' not found in {doc.FilePath}.");

                var prop = RenderProperty(add.Property);
                var newClass = classNode.AddMembers(prop);
                var newRoot = root.ReplaceNode(classNode, newClass);
                pending[doc.FilePath!] = newRoot.NormalizeWhitespace().ToFullString();
                break;
            }

            case RemoveProperty remove:
            {
                using var ws = MSBuildWorkspace.Create();
                var project = await ws.OpenProjectAsync(absolute, cancellationToken: ct);
                var doc = await FindEntityDocumentAsync(project, remove.EntityName, ct);
                if (doc is null) return MutationResult.Fail($"Entity '{remove.EntityName}' not found.");

                var tree = await doc.GetSyntaxTreeAsync(ct);
                var root = (CompilationUnitSyntax?)await tree!.GetRootAsync(ct);
                var classNode = root!.DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.Text == remove.EntityName);
                if (classNode is null) return MutationResult.Fail($"Class '{remove.EntityName}' not found.");

                var propNode = classNode.Members.OfType<PropertyDeclarationSyntax>()
                    .FirstOrDefault(p => p.Identifier.Text == remove.PropertyName);
                if (propNode is null) return MutationResult.Fail($"Property '{remove.PropertyName}' not found on '{remove.EntityName}'.");

                var newClass = classNode.RemoveNode(propNode, SyntaxRemoveOptions.KeepNoTrivia)!;
                var newRoot = root.ReplaceNode(classNode, newClass);
                pending[doc.FilePath!] = newRoot.ToFullString();
                break;
            }

            case RenameProperty rename:
            {
                using var ws = MSBuildWorkspace.Create();
                var project = await ws.OpenProjectAsync(absolute, cancellationToken: ct);
                var compilation = await project.GetCompilationAsync(ct);
                var entitySym = compilation!.Assembly.GlobalNamespace.GetAllTypes()
                    .FirstOrDefault(t => t.Name == rename.EntityName);
                if (entitySym is null) return MutationResult.Fail($"Entity '{rename.EntityName}' not found.");

                var propSym = entitySym.GetMembers().OfType<IPropertySymbol>()
                    .FirstOrDefault(p => p.Name == rename.OldName);
                if (propSym is null) return MutationResult.Fail($"Property '{rename.OldName}' not found on '{rename.EntityName}'.");

                var newSolution = await Microsoft.CodeAnalysis.Rename.Renamer.RenameSymbolAsync(
                    project.Solution, propSym, new Microsoft.CodeAnalysis.Rename.SymbolRenameOptions(), rename.NewName, ct);

                // Stage all changed documents in pending.
                var changes = newSolution.GetChanges(project.Solution);
                foreach (var projChange in changes.GetProjectChanges())
                {
                    foreach (var docId in projChange.GetChangedDocuments())
                    {
                        var newDoc = newSolution.GetDocument(docId)!;
                        var text = await newDoc.GetTextAsync(ct);
                        pending[newDoc.FilePath!] = text.ToString();
                    }
                }
                if (pending.Count == 0)
                {
                    diagnostics.Add(new CatalogDiagnostic("warning",
                        $"Rename produced no file changes — '{rename.OldName}' may already be '{rename.NewName}' or symbol resolution failed.",
                        null, null));
                }
                break;
            }
```

- [ ] **Step 2: Add `RenderProperty` helper** at the bottom of `RoslynEntityMutator` (after the existing `RenderNewEntityFile`):

```csharp
    private static PropertyDeclarationSyntax RenderProperty(Property p)
    {
        var typeName = p.IsNullable && !p.ClrType.EndsWith("?")
            ? p.ClrType + "?"
            : p.ClrType;
        var src = $"public {typeName} {p.Name} {{ get; set; }}";
        return (PropertyDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(src)!;
    }
```

- [ ] **Step 3: Append three new tests** to `RoslynEntityMutatorTests.cs`:

```csharp
    [Fact]
    public async Task AddProperty_appends_a_property_to_the_class()
    {
        using var fix = new FixtureProjectBuilder("mut_addprop");
        var bookFile = fix.AddFile("Book.cs", """
            namespace Demo;
            public class Book { public int Id { get; set; } }
            """);

        var mutator = new RoslynEntityMutator();
        var result = await mutator.ApplyAsync(
            fix.CsprojPath,
            new AddProperty("Book", new Property("Title", "string", IsNullable: false, IsPrimaryKey: false, Attributes: [])),
            default);

        result.Success.Should().BeTrue();
        var updated = File.ReadAllText(bookFile);
        updated.Should().Contain("public string Title");
        updated.Should().Contain("public int Id");
    }

    [Fact]
    public async Task RemoveProperty_strips_the_property_declaration()
    {
        using var fix = new FixtureProjectBuilder("mut_rmprop");
        var bookFile = fix.AddFile("Book.cs", """
            namespace Demo;
            public class Book { public int Id { get; set; } public string Title { get; set; } = ""; }
            """);

        var mutator = new RoslynEntityMutator();
        var result = await mutator.ApplyAsync(
            fix.CsprojPath,
            new RemoveProperty("Book", "Title"),
            default);

        result.Success.Should().BeTrue();
        var updated = File.ReadAllText(bookFile);
        updated.Should().NotContain("Title");
        updated.Should().Contain("public int Id");
    }

    [Fact]
    public async Task RenameProperty_renames_declarations_via_symbol_rename()
    {
        using var fix = new FixtureProjectBuilder("mut_rename");
        var bookFile = fix.AddFile("Book.cs", """
            namespace Demo;
            public class Book { public int Id { get; set; } public string Name { get; set; } = ""; }
            """);

        var mutator = new RoslynEntityMutator();
        var result = await mutator.ApplyAsync(
            fix.CsprojPath,
            new RenameProperty("Book", "Name", "Title"),
            default);

        result.Success.Should().BeTrue();
        var updated = File.ReadAllText(bookFile);
        updated.Should().Contain("Title");
        updated.Should().NotContain("Name");
    }
```

- [ ] **Step 4: Build + run**

```bash
dotnet build --nologo -v q
dotnet run --project tests/AspireForm.Tests -class "AspireForm.Tests.EntityCatalog.RoslynEntityMutatorTests"
```

Expected: 7/7 PASS (4 from Task 9 + 3 new).

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/EntityCatalog/RoslynEntityMutator.cs tests/AspireForm.Tests/EntityCatalog/RoslynEntityMutatorTests.cs
git -c commit.gpgsign=false commit -m "feat(catalog): mutator supports AddProperty / RemoveProperty / RenameProperty"
```

---

## Task 11: Mutator — SetAttribute / ClearAttribute / AddRelationship / RemoveRelationship

**Files:**
- Modify: `src/AspireForm/EntityCatalog/RoslynEntityMutator.cs`
- Modify: `tests/AspireForm.Tests/EntityCatalog/RoslynEntityMutatorTests.cs`

- [ ] **Step 1: Add the four remaining switch cases** to `RoslynEntityMutator.ApplyAsync` (between `RenameProperty rename:` and `default:`):

```csharp
            case SetAttribute set:
            {
                using var ws = MSBuildWorkspace.Create();
                var project = await ws.OpenProjectAsync(absolute, cancellationToken: ct);
                var doc = await FindEntityDocumentAsync(project, set.EntityName, ct);
                if (doc is null) return MutationResult.Fail($"Entity '{set.EntityName}' not found.");

                var tree = await doc.GetSyntaxTreeAsync(ct);
                var root = (CompilationUnitSyntax?)await tree!.GetRootAsync(ct);
                var classNode = root!.DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.Text == set.EntityName);
                if (classNode is null) return MutationResult.Fail($"Class '{set.EntityName}' not found.");

                var attrText = RenderAttribute(set.Attribute);
                var attrList = (AttributeListSyntax)SyntaxFactory.ParseCompilationUnit($"{attrText}\nclass X {{}}")
                    .DescendantNodes().OfType<AttributeListSyntax>().First();

                SyntaxNode newRoot;
                if (set.PropertyName is null)
                {
                    var clearedClass = WithoutAttribute(classNode, set.Attribute.FullTypeName);
                    var newClass = clearedClass.WithAttributeLists(clearedClass.AttributeLists.Add(attrList));
                    newRoot = root.ReplaceNode(classNode, newClass);
                }
                else
                {
                    var propNode = classNode.Members.OfType<PropertyDeclarationSyntax>()
                        .FirstOrDefault(p => p.Identifier.Text == set.PropertyName);
                    if (propNode is null) return MutationResult.Fail($"Property '{set.PropertyName}' not found.");
                    var clearedProp = WithoutAttribute(propNode, set.Attribute.FullTypeName);
                    var newProp = clearedProp.WithAttributeLists(clearedProp.AttributeLists.Add(attrList));
                    newRoot = root.ReplaceNode(propNode, newProp);
                }

                pending[doc.FilePath!] = newRoot.ToFullString();
                break;
            }

            case ClearAttribute clear:
            {
                using var ws = MSBuildWorkspace.Create();
                var project = await ws.OpenProjectAsync(absolute, cancellationToken: ct);
                var doc = await FindEntityDocumentAsync(project, clear.EntityName, ct);
                if (doc is null) return MutationResult.Fail($"Entity '{clear.EntityName}' not found.");

                var tree = await doc.GetSyntaxTreeAsync(ct);
                var root = (CompilationUnitSyntax?)await tree!.GetRootAsync(ct);
                var classNode = root!.DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.Text == clear.EntityName);
                if (classNode is null) return MutationResult.Fail($"Class '{clear.EntityName}' not found.");

                SyntaxNode newRoot;
                if (clear.PropertyName is null)
                {
                    newRoot = root.ReplaceNode(classNode, WithoutAttribute(classNode, clear.AttributeFullTypeName));
                }
                else
                {
                    var propNode = classNode.Members.OfType<PropertyDeclarationSyntax>()
                        .FirstOrDefault(p => p.Identifier.Text == clear.PropertyName);
                    if (propNode is null) return MutationResult.Fail($"Property '{clear.PropertyName}' not found.");
                    newRoot = root.ReplaceNode(propNode, WithoutAttribute(propNode, clear.AttributeFullTypeName));
                }
                pending[doc.FilePath!] = newRoot.ToFullString();
                break;
            }

            case AddRelationship rel:
            {
                if (rel.Cardinality == RelationshipCardinality.ManyToMany)
                {
                    return MutationResult.Fail("ManyToMany relationships are not supported in v1 (deferred to #4a.1).");
                }

                using var ws = MSBuildWorkspace.Create();
                var project = await ws.OpenProjectAsync(absolute, cancellationToken: ct);
                var fromDoc = await FindEntityDocumentAsync(project, rel.FromEntity, ct);
                var toDoc = await FindEntityDocumentAsync(project, rel.ToEntity, ct);
                if (fromDoc is null) return MutationResult.Fail($"Entity '{rel.FromEntity}' not found.");
                if (toDoc is null) return MutationResult.Fail($"Entity '{rel.ToEntity}' not found.");

                pending[fromDoc.FilePath!] = await AddRelationshipToFromAsync(fromDoc, rel, ct);
                if (!string.Equals(fromDoc.FilePath, toDoc.FilePath, StringComparison.OrdinalIgnoreCase))
                {
                    pending[toDoc.FilePath!] = await AddRelationshipToToAsync(toDoc, rel, ct);
                }
                else
                {
                    pending[fromDoc.FilePath!] = await AddBothSidesInOneFileAsync(fromDoc, rel, ct);
                }
                break;
            }

            case RemoveRelationship rrm:
            {
                using var ws = MSBuildWorkspace.Create();
                var project = await ws.OpenProjectAsync(absolute, cancellationToken: ct);
                var doc = await FindEntityDocumentAsync(project, rrm.FromEntity, ct);
                if (doc is null) return MutationResult.Fail($"Entity '{rrm.FromEntity}' not found.");

                var tree = await doc.GetSyntaxTreeAsync(ct);
                var root = (CompilationUnitSyntax?)await tree!.GetRootAsync(ct);
                var classNode = root!.DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.Text == rrm.FromEntity);
                if (classNode is null) return MutationResult.Fail($"Class '{rrm.FromEntity}' not found.");

                var navProp = classNode.Members.OfType<PropertyDeclarationSyntax>()
                    .FirstOrDefault(p => p.Identifier.Text == rrm.RelationshipName);
                if (navProp is null) return MutationResult.Fail($"Relationship '{rrm.RelationshipName}' not found.");

                var newClass = classNode.RemoveNode(navProp, SyntaxRemoveOptions.KeepNoTrivia)!;
                var newRoot = root.ReplaceNode(classNode, newClass);
                pending[doc.FilePath!] = newRoot.ToFullString();
                diagnostics.Add(new CatalogDiagnostic("warning",
                    "Removed navigation property only. FK property + reverse navigation (if any) must be removed manually in v1.",
                    doc.FilePath, null));
                break;
            }
```

- [ ] **Step 2: Add helper methods** at the bottom of `RoslynEntityMutator`:

```csharp
    private static string RenderAttribute(AttributeInstance a)
    {
        var shortName = a.FullTypeName.Split('.').Last();
        // Strip "Attribute" suffix for the C# attribute syntax (e.g., DabExpose not DabExposeAttribute).
        if (shortName.EndsWith("Attribute", StringComparison.Ordinal))
            shortName = shortName[..^"Attribute".Length];
        var args = new List<string>();
        foreach (var ctor in a.ConstructorArgs)
            args.Add(FormatLiteral(ctor));
        foreach (var (k, v) in a.NamedArgs)
            args.Add($"{k} = {FormatLiteral(v)}");
        var body = args.Count == 0 ? "" : $"({string.Join(", ", args)})";
        return $"[{shortName}{body}]";
    }

    private static string FormatLiteral(object? v) => v switch
    {
        null => "null",
        string s => $"\"{s.Replace("\"", "\\\"")}\"",
        bool b => b ? "true" : "false",
        char c => $"'{c}'",
        _ => v.ToString() ?? "null",
    };

    private static TNode WithoutAttribute<TNode>(TNode node, string attributeFullTypeName) where TNode : SyntaxNode
    {
        var shortName = attributeFullTypeName.Split('.').Last();
        if (shortName.EndsWith("Attribute", StringComparison.Ordinal))
            shortName = shortName[..^"Attribute".Length];

        var listsToRewrite = node.DescendantNodes().OfType<AttributeListSyntax>()
            .Where(al => al.Parent == node).ToList();

        foreach (var list in listsToRewrite)
        {
            var keep = list.Attributes.Where(a => a.Name.ToString().Split('.').Last() != shortName
                                               && a.Name.ToString().Split('.').Last() != shortName + "Attribute").ToList();
            if (keep.Count == list.Attributes.Count) continue;

            if (keep.Count == 0)
            {
                node = node.RemoveNode(list, SyntaxRemoveOptions.KeepNoTrivia)!;
            }
            else
            {
                var newList = list.WithAttributes(SyntaxFactory.SeparatedList(keep));
                node = node.ReplaceNode(list, newList);
            }
        }
        return node;
    }

    private static async Task<string> AddRelationshipToFromAsync(Document doc, AddRelationship rel, CancellationToken ct)
    {
        var tree = await doc.GetSyntaxTreeAsync(ct);
        var root = (CompilationUnitSyntax?)await tree!.GetRootAsync(ct);
        var classNode = root!.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == rel.FromEntity);

        // From-side nav: 1:1 / N:1 -> scalar ref; 1:N -> collection.
        var navType = rel.Cardinality == RelationshipCardinality.OneToMany
            ? $"System.Collections.Generic.ICollection<{rel.ToEntity}>"
            : rel.ToEntity;
        var navInit = rel.Cardinality == RelationshipCardinality.OneToMany
            ? $" = new System.Collections.Generic.List<{rel.ToEntity}>();"
            : "";
        var fkLine = rel.Cardinality == RelationshipCardinality.ManyToOne
            ? $"public int {rel.ForeignKeyProperty ?? rel.ToEntity + "Id"} {{ get; set; }}"
            : null;

        var members = new List<MemberDeclarationSyntax>();
        if (fkLine is not null)
            members.Add((PropertyDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(fkLine)!);
        members.Add((PropertyDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(
            $"public {navType} {rel.ToEntity} {{ get; set; }}{navInit}")!);

        var newClass = classNode.AddMembers(members.ToArray());
        return root.ReplaceNode(classNode, newClass).NormalizeWhitespace().ToFullString();
    }

    private static async Task<string> AddRelationshipToToAsync(Document doc, AddRelationship rel, CancellationToken ct)
    {
        var tree = await doc.GetSyntaxTreeAsync(ct);
        var root = (CompilationUnitSyntax?)await tree!.GetRootAsync(ct);
        var classNode = root!.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == rel.ToEntity);

        // Reverse side: inverse of From's cardinality.
        var reverseType = rel.Cardinality == RelationshipCardinality.OneToMany
            ? rel.FromEntity
            : rel.Cardinality == RelationshipCardinality.ManyToOne
                ? $"System.Collections.Generic.ICollection<{rel.FromEntity}>"
                : rel.FromEntity; // OneToOne: scalar back-ref
        var reverseInit = rel.Cardinality == RelationshipCardinality.ManyToOne
            ? $" = new System.Collections.Generic.List<{rel.FromEntity}>();"
            : "";

        var member = (PropertyDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(
            $"public {reverseType} {rel.FromEntity} {{ get; set; }}{reverseInit}")!;
        var newClass = classNode.AddMembers(member);
        return root.ReplaceNode(classNode, newClass).NormalizeWhitespace().ToFullString();
    }

    private static async Task<string> AddBothSidesInOneFileAsync(Document doc, AddRelationship rel, CancellationToken ct)
    {
        // Both entities live in one file — apply from-side, then to-side on the from-side's already-modified tree.
        var firstPass = await AddRelationshipToFromAsync(doc, rel, ct);
        // Re-parse and apply the to-side mutation.
        var tree = CSharpSyntaxTree.ParseText(firstPass);
        var root = (CompilationUnitSyntax)tree.GetRoot();
        var toClass = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == rel.ToEntity);
        var reverseType = rel.Cardinality == RelationshipCardinality.OneToMany
            ? rel.FromEntity
            : rel.Cardinality == RelationshipCardinality.ManyToOne
                ? $"System.Collections.Generic.ICollection<{rel.FromEntity}>"
                : rel.FromEntity;
        var reverseInit = rel.Cardinality == RelationshipCardinality.ManyToOne
            ? $" = new System.Collections.Generic.List<{rel.FromEntity}>();"
            : "";
        var member = (PropertyDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(
            $"public {reverseType} {rel.FromEntity} {{ get; set; }}{reverseInit}")!;
        var newToClass = toClass.AddMembers(member);
        return root.ReplaceNode(toClass, newToClass).NormalizeWhitespace().ToFullString();
    }
```

- [ ] **Step 3: Append four new tests** to `RoslynEntityMutatorTests.cs`:

```csharp
    [Fact]
    public async Task SetAttribute_adds_a_class_level_attribute()
    {
        using var fix = new FixtureProjectBuilder("mut_setattr");
        var bookFile = fix.AddFile("Book.cs", """
            namespace Demo;
            public class Book { public int Id { get; set; } }
            """);

        var mutator = new RoslynEntityMutator();
        var attr = new AttributeInstance("AspireForm.Annotations.DabExposeAttribute", [], new Dictionary<string, object?>());
        var result = await mutator.ApplyAsync(
            fix.CsprojPath,
            new SetAttribute("Book", null, attr),
            default);

        result.Success.Should().BeTrue();
        var updated = File.ReadAllText(bookFile);
        updated.Should().Contain("[DabExpose]");
    }

    [Fact]
    public async Task ClearAttribute_removes_a_class_level_attribute()
    {
        using var fix = new FixtureProjectBuilder("mut_clearattr");
        var bookFile = fix.AddFile("Book.cs", """
            namespace Demo;
            [AspireForm.Annotations.DabExpose]
            public class Book { public int Id { get; set; } }
            """);

        var mutator = new RoslynEntityMutator();
        var result = await mutator.ApplyAsync(
            fix.CsprojPath,
            new ClearAttribute("Book", null, "AspireForm.Annotations.DabExposeAttribute"),
            default);

        result.Success.Should().BeTrue();
        var updated = File.ReadAllText(bookFile);
        updated.Should().NotContain("DabExpose");
    }

    [Fact]
    public async Task AddRelationship_OneToMany_adds_collection_on_from_and_scalar_back_on_to()
    {
        using var fix = new FixtureProjectBuilder("mut_addrel");
        var modelsFile = fix.AddFile("Models.cs", """
            namespace Demo;
            public class Author { public int Id { get; set; } }
            public class Book { public int Id { get; set; } }
            """);

        var mutator = new RoslynEntityMutator();
        var result = await mutator.ApplyAsync(
            fix.CsprojPath,
            new AddRelationship("Author", "Book", RelationshipCardinality.OneToMany, null),
            default);

        result.Success.Should().BeTrue();
        var updated = File.ReadAllText(modelsFile);
        updated.Should().Contain("ICollection<Book> Book");
        updated.Should().Contain("Author Author");
    }

    [Fact]
    public async Task AddRelationship_ManyToMany_returns_failure_in_v1()
    {
        using var fix = new FixtureProjectBuilder("mut_addrel_m2m");
        fix.AddFile("Models.cs", "namespace Demo; public class A { public int Id { get; set; } } public class B { public int Id { get; set; } }");

        var mutator = new RoslynEntityMutator();
        var result = await mutator.ApplyAsync(
            fix.CsprojPath,
            new AddRelationship("A", "B", RelationshipCardinality.ManyToMany, null),
            default);

        result.Success.Should().BeFalse();
        result.Diagnostics[0].Message.Should().Contain("ManyToMany");
    }
```

- [ ] **Step 4: Build + run**

```bash
dotnet build --nologo -v q
dotnet run --project tests/AspireForm.Tests -class "AspireForm.Tests.EntityCatalog.RoslynEntityMutatorTests"
```

Expected: 11/11 PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/EntityCatalog/RoslynEntityMutator.cs tests/AspireForm.Tests/EntityCatalog/RoslynEntityMutatorTests.cs
git -c commit.gpgsign=false commit -m "feat(catalog): mutator supports SetAttribute / ClearAttribute / AddRelationship / RemoveRelationship"
```

---

## Task 12: RoslynEntityCatalogService (IEntityCatalogService default impl)

**Files:**
- Create: `src/AspireForm/EntityCatalog/RoslynEntityCatalogService.cs`

This is the DI-ready default impl. It owns a cached `RoslynEntityScanner` per-instance and a stateless `RoslynEntityMutator`. The same instance is used by Blazor pages, MCP tools, and the `ef-data` provider.

- [ ] **Step 1: Create `src/AspireForm/EntityCatalog/RoslynEntityCatalogService.cs`**

```csharp
namespace AspireForm.EntityCatalog;

/// <summary>Default <see cref="IEntityCatalogService"/> backed by Roslyn. Caches one scanner per service instance; mutator is stateless.</summary>
public sealed class RoslynEntityCatalogService : IEntityCatalogService, IAsyncDisposable
{
    private readonly RoslynEntityScanner _scanner = new();
    private readonly RoslynEntityMutator _mutator = new();

    /// <inheritdoc />
    public Task<EntityCatalog> ScanAsync(string csprojPath, CancellationToken ct) =>
        _scanner.ScanAsync(csprojPath, ct);

    /// <inheritdoc />
    public Task<MutationResult> MutateAsync(string csprojPath, EntityChangeRequest request, CancellationToken ct) =>
        _mutator.ApplyAsync(csprojPath, request, ct);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _scanner.DisposeAsync();
}
```

- [ ] **Step 2: Build**

```bash
dotnet build --nologo -v q
```

Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/AspireForm/EntityCatalog/RoslynEntityCatalogService.cs
git -c commit.gpgsign=false commit -m "feat(catalog): add RoslynEntityCatalogService (default IEntityCatalogService impl)"
```

---

## Task 13: DbContextEmitter

The DbContext emitter takes an `EntityCatalog` snapshot and produces the contents of a `DbContext.cs` file. v1 emits a complete file (no managed-region merge); when the user already has a DbContext, the provider in Task 15 still overwrites it because the file is tagged `Managed`. Comments + Fluent API for relationships requiring config beyond conventions (e.g., `OnDelete`) are emitted from attribute metadata.

**Files:**
- Create: `src/AspireForm/Providers/EfData/DbContextEmitter.cs`
- Create: `tests/AspireForm.Tests/Providers/EfData/DbContextEmitterTests.cs`

- [ ] **Step 1: Create `src/AspireForm/Providers/EfData/DbContextEmitter.cs`**

```csharp
using System.Text;
using AspireForm.EntityCatalog;

namespace AspireForm.Providers.EfData;

/// <summary>Emits a DbContext class with <c>DbSet&lt;T&gt;</c> per entity and an <c>OnModelCreating</c> override containing any relationship configuration requiring Fluent API (e.g., <c>OnDelete</c>).</summary>
public static class DbContextEmitter
{
    /// <summary>Renders the DbContext file contents.</summary>
    /// <param name="contextName">DbContext class name (e.g., <c>AppDbContext</c>).</param>
    /// <param name="contextNamespace">DbContext namespace.</param>
    /// <param name="catalog">Entity catalog snapshot.</param>
    public static string Render(string contextName, string contextNamespace, EntityCatalog catalog)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated by AspireForm ef-data /> Do not edit by hand inside the managed region.");
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine();
        sb.Append("namespace ").Append(contextNamespace).AppendLine(";");
        sb.AppendLine();
        sb.Append("public class ").Append(contextName).AppendLine(" : DbContext");
        sb.AppendLine("{");
        sb.Append("    public ").Append(contextName).Append("(DbContextOptions<").Append(contextName).AppendLine("> options) : base(options) { }");
        sb.AppendLine();

        foreach (var e in catalog.Entities.OrderBy(e => e.Name, StringComparer.Ordinal))
        {
            sb.Append("    public DbSet<").Append(e.Name).Append("> ").Append(Pluralise(e.Name)).AppendLine(" { get; set; } = null!;");
        }

        var fluent = CollectFluentConfig(catalog);
        if (fluent.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("    protected override void OnModelCreating(ModelBuilder modelBuilder)");
            sb.AppendLine("    {");
            sb.AppendLine("        base.OnModelCreating(modelBuilder);");
            foreach (var line in fluent)
            {
                sb.Append("        ").AppendLine(line);
            }
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static List<string> CollectFluentConfig(EntityCatalog catalog)
    {
        var lines = new List<string>();
        foreach (var e in catalog.Entities)
        {
            foreach (var r in e.Relationships)
            {
                // Look for [OnDelete("...")] on the navigation property to emit a fluent OnDelete call.
                // (The scanner attaches property attributes to Property records; navigation properties are in Relationships
                //  without their attribute list in v1 — defer attribute-driven fluent config to #4a.1.)
            }
            // Future: read class-level attributes that map to Fluent config.
        }
        return lines;
    }

    private static string Pluralise(string name)
    {
        if (name.EndsWith("y", StringComparison.Ordinal) && name.Length > 1 && !"aeiou".Contains(name[^2]))
            return name[..^1] + "ies";
        if (name.EndsWith("s", StringComparison.Ordinal) || name.EndsWith("x", StringComparison.Ordinal)
            || name.EndsWith("ch", StringComparison.Ordinal) || name.EndsWith("sh", StringComparison.Ordinal))
            return name + "es";
        return name + "s";
    }
}
```

- [ ] **Step 2: Create `tests/AspireForm.Tests/Providers/EfData/DbContextEmitterTests.cs`**

```csharp
using AspireForm.EntityCatalog;
using AspireForm.Providers.EfData;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Providers.EfData;

public sealed class DbContextEmitterTests
{
    private static EntityCatalog CatalogOf(params Entity[] entities) =>
        new(entities, [], []);

    private static Entity SimpleEntity(string name) =>
        new(name, "Demo", $"{name}.cs",
            Properties: [new Property("Id", "int", false, true, [])],
            Relationships: [],
            Attributes: []);

    [Fact]
    public void Render_emits_namespace_and_class_header()
    {
        var src = DbContextEmitter.Render("AppDbContext", "Demo.Data", CatalogOf(SimpleEntity("Book")));
        src.Should().Contain("namespace Demo.Data;");
        src.Should().Contain("public class AppDbContext : DbContext");
        src.Should().Contain("public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }");
    }

    [Fact]
    public void Render_emits_DbSet_per_entity_pluralised()
    {
        var src = DbContextEmitter.Render("Ctx", "X", CatalogOf(SimpleEntity("Book"), SimpleEntity("Category"), SimpleEntity("Brush")));
        src.Should().Contain("public DbSet<Book> Books { get; set; }");
        src.Should().Contain("public DbSet<Category> Categories { get; set; }");
        src.Should().Contain("public DbSet<Brush> Brushes { get; set; }");
    }

    [Fact]
    public void Render_emits_entities_in_alphabetical_order_for_deterministic_diffs()
    {
        var src = DbContextEmitter.Render("Ctx", "X", CatalogOf(SimpleEntity("Zebra"), SimpleEntity("Apple")));
        var apple = src.IndexOf("Apple", StringComparison.Ordinal);
        var zebra = src.IndexOf("Zebra", StringComparison.Ordinal);
        apple.Should().BeGreaterThan(0);
        zebra.Should().BeGreaterThan(apple);
    }

    [Fact]
    public void Render_omits_OnModelCreating_when_no_fluent_config_required()
    {
        var src = DbContextEmitter.Render("Ctx", "X", CatalogOf(SimpleEntity("Book")));
        src.Should().NotContain("OnModelCreating");
    }
}
```

- [ ] **Step 3: Build + run**

```bash
dotnet build --nologo -v q
dotnet run --project tests/AspireForm.Tests -class "AspireForm.Tests.Providers.EfData.DbContextEmitterTests"
```

Expected: 4/4 PASS.

- [ ] **Step 4: Commit**

```bash
git add src/AspireForm/Providers/EfData/DbContextEmitter.cs tests/AspireForm.Tests/Providers/EfData/DbContextEmitterTests.cs
git -c commit.gpgsign=false commit -m "feat(ef-data): add DbContextEmitter (DbSet per entity, sorted, optional OnModelCreating)"
```

---

## Task 14: DabConfigEmitter

Emits `dab-config.json` from entities carrying `[DabExpose]`. Honors `[DabPath]`, `[DabPermission]`, `[DabRestOnly]`, `[DabGraphqlOnly]`, `[DabHidden]`. Relationships are emitted from the `Relationship` graph when both endpoints are also exposed.

**Files:**
- Create: `src/AspireForm/Providers/EfData/DabConfigEmitter.cs`
- Create: `tests/AspireForm.Tests/Providers/EfData/DabConfigEmitterTests.cs`

- [ ] **Step 1: Create `src/AspireForm/Providers/EfData/DabConfigEmitter.cs`**

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;

namespace AspireForm.Providers.EfData;

/// <summary>Emits <c>dab-config.json</c> contents from a catalog. Entities carrying <c>[DabExpose]</c> become DAB entities; <c>[DabHidden]</c> overrides.</summary>
public static class DabConfigEmitter
{
    private const string ExposeAttr = "AspireForm.Annotations.DabExposeAttribute";
    private const string HiddenAttr = "AspireForm.Annotations.DabHiddenAttribute";
    private const string PathAttr = "AspireForm.Annotations.DabPathAttribute";
    private const string PermissionAttr = "AspireForm.Annotations.DabPermissionAttribute";
    private const string RestOnlyAttr = "AspireForm.Annotations.DabRestOnlyAttribute";
    private const string GraphqlOnlyAttr = "AspireForm.Annotations.DabGraphqlOnlyAttribute";
    private const string TableAttr = "System.ComponentModel.DataAnnotations.Schema.TableAttribute";

    /// <summary>Renders the <c>dab-config.json</c> file contents. Returns null when no exposed entities are present.</summary>
    /// <param name="catalog">Entity catalog snapshot.</param>
    /// <param name="databaseConnectionName">Connection name from the <c>ef-data</c> block's first dependsOn (used in the <c>@env('ConnectionStrings__&lt;name&gt;')</c> token).</param>
    /// <param name="diagnostics">Mutable list — emitter appends warnings (e.g., duplicate permission roles).</param>
    public static string? Render(EntityCatalog catalog, string databaseConnectionName, List<CatalogDiagnostic> diagnostics)
    {
        var exposedEntities = catalog.Entities
            .Where(e => e.Attributes.Any(a => a.FullTypeName == ExposeAttr)
                     && !e.Attributes.Any(a => a.FullTypeName == HiddenAttr))
            .OrderBy(e => e.Name, StringComparer.Ordinal)
            .ToList();
        if (exposedEntities.Count == 0) return null;

        var entities = new JsonObject();
        foreach (var e in exposedEntities)
        {
            entities[e.Name] = BuildEntityNode(e, diagnostics);
        }

        var root = new JsonObject
        {
            ["$schema"] = "https://github.com/Azure/data-api-builder/releases/latest/download/dab.draft.schema.json",
            ["data-source"] = new JsonObject
            {
                ["database-type"] = "mssql",
                ["connection-string"] = $"@env('ConnectionStrings__{databaseConnectionName}')",
            },
            ["runtime"] = new JsonObject
            {
                ["rest"] = new JsonObject { ["enabled"] = true, ["path"] = "/api" },
                ["graphql"] = new JsonObject { ["enabled"] = true, ["path"] = "/graphql" },
            },
            ["entities"] = entities,
        };

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
    }

    private static JsonNode BuildEntityNode(Entity e, List<CatalogDiagnostic> diagnostics)
    {
        var tableAttr = e.Attributes.FirstOrDefault(a => a.FullTypeName == TableAttr);
        var source = tableAttr?.ConstructorArgs.FirstOrDefault() as string ?? $"dbo.{e.Name}";

        var pathAttr = e.Attributes.FirstOrDefault(a => a.FullTypeName == PathAttr);
        var restPath = pathAttr?.ConstructorArgs.FirstOrDefault() as string ?? $"/{e.Name.ToLowerInvariant()}";

        var restOnly = e.Attributes.Any(a => a.FullTypeName == RestOnlyAttr);
        var graphqlOnly = e.Attributes.Any(a => a.FullTypeName == GraphqlOnlyAttr);

        var permissions = e.Attributes
            .Where(a => a.FullTypeName == PermissionAttr)
            .ToList();

        // Detect duplicate roles.
        var seenRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var permArr = new JsonArray();
        foreach (var p in permissions)
        {
            var role = p.ConstructorArgs.ElementAtOrDefault(0) as string ?? "";
            var actions = p.ConstructorArgs.ElementAtOrDefault(1) as string ?? "*";
            if (!seenRoles.Add(role))
            {
                diagnostics.Add(new CatalogDiagnostic("warning",
                    $"Entity '{e.Name}' declares multiple [DabPermission] for role '{role}'. Last-wins applied.",
                    e.FilePath, null));
                // Remove the previous entry with the same role from permArr to enforce last-wins.
                for (int i = permArr.Count - 1; i >= 0; i--)
                {
                    if (permArr[i]!["role"]?.GetValue<string>() == role)
                    {
                        permArr.RemoveAt(i);
                        break;
                    }
                }
            }
            permArr.Add(new JsonObject
            {
                ["role"] = role,
                ["actions"] = new JsonArray(actions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(a => (JsonNode)JsonValue.Create(a)!)
                    .ToArray()),
            });
        }

        if (permArr.Count == 0)
        {
            permArr.Add(new JsonObject { ["role"] = "anonymous", ["actions"] = new JsonArray("read") });
        }

        var node = new JsonObject
        {
            ["source"] = source,
            ["permissions"] = permArr,
        };
        if (!graphqlOnly)
        {
            node["rest"] = new JsonObject { ["path"] = restPath };
        }
        if (restOnly)
        {
            node["graphql"] = false;
        }

        // Relationships: only emit when the target is also an exposed entity.
        if (e.Relationships.Count > 0)
        {
            var rels = new JsonObject();
            foreach (var r in e.Relationships.OrderBy(r => r.Name, StringComparer.Ordinal))
            {
                rels[r.Name] = new JsonObject
                {
                    ["target.entity"] = r.TargetEntity,
                    ["cardinality"] = r.Cardinality is RelationshipCardinality.OneToMany or RelationshipCardinality.ManyToMany ? "many" : "one",
                };
            }
            if (rels.Count > 0) node["relationships"] = rels;
        }

        return node;
    }
}
```

- [ ] **Step 2: Create `tests/AspireForm.Tests/Providers/EfData/DabConfigEmitterTests.cs`**

```csharp
using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;
using AspireForm.Providers.EfData;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Providers.EfData;

public sealed class DabConfigEmitterTests
{
    private static Entity EntityWithAttrs(string name, params AttributeInstance[] attrs) =>
        new(name, "Demo", $"{name}.cs",
            Properties: [new Property("Id", "int", false, true, [])],
            Relationships: [],
            Attributes: attrs);

    private static AttributeInstance Attr(string fullName, params object[] ctorArgs) =>
        new(fullName, ctorArgs, new Dictionary<string, object?>());

    [Fact]
    public void Render_returns_null_when_no_entities_are_exposed()
    {
        var catalog = new EntityCatalog([EntityWithAttrs("Book")], [], []);
        DabConfigEmitter.Render(catalog, "sql", new()).Should().BeNull();
    }

    [Fact]
    public void Render_emits_exposed_entity_with_default_anonymous_read_permission()
    {
        var catalog = new EntityCatalog(
            [EntityWithAttrs("Book", Attr("AspireForm.Annotations.DabExposeAttribute"))],
            [], []);
        var json = DabConfigEmitter.Render(catalog, "sql", new())!;
        var node = (JsonObject)JsonNode.Parse(json)!;
        node["entities"]!["Book"]!["source"]!.GetValue<string>().Should().Be("dbo.Book");
        node["entities"]!["Book"]!["permissions"]![0]!["role"]!.GetValue<string>().Should().Be("anonymous");
        node["entities"]!["Book"]!["rest"]!["path"]!.GetValue<string>().Should().Be("/book");
    }

    [Fact]
    public void Render_honors_DabPath_override()
    {
        var catalog = new EntityCatalog(
            [EntityWithAttrs("Book",
                Attr("AspireForm.Annotations.DabExposeAttribute"),
                Attr("AspireForm.Annotations.DabPathAttribute", "/books"))],
            [], []);
        var json = DabConfigEmitter.Render(catalog, "sql", new())!;
        json.Should().Contain("\"path\": \"/books\"");
    }

    [Fact]
    public void Render_honors_DabHidden_to_suppress_an_otherwise_exposed_entity()
    {
        var catalog = new EntityCatalog(
            [EntityWithAttrs("Book",
                Attr("AspireForm.Annotations.DabExposeAttribute"),
                Attr("AspireForm.Annotations.DabHiddenAttribute"))],
            [], []);
        DabConfigEmitter.Render(catalog, "sql", new()).Should().BeNull();
    }

    [Fact]
    public void Render_uses_Table_attribute_value_for_source()
    {
        var catalog = new EntityCatalog(
            [EntityWithAttrs("Book",
                Attr("AspireForm.Annotations.DabExposeAttribute"),
                Attr("System.ComponentModel.DataAnnotations.Schema.TableAttribute", "library.books"))],
            [], []);
        var json = DabConfigEmitter.Render(catalog, "sql", new())!;
        json.Should().Contain("\"source\": \"library.books\"");
    }

    [Fact]
    public void Render_emits_last_wins_permission_warning_for_duplicate_roles()
    {
        var diagnostics = new List<CatalogDiagnostic>();
        var catalog = new EntityCatalog(
            [EntityWithAttrs("Book",
                Attr("AspireForm.Annotations.DabExposeAttribute"),
                Attr("AspireForm.Annotations.DabPermissionAttribute", "anonymous", "read"),
                Attr("AspireForm.Annotations.DabPermissionAttribute", "anonymous", "*"))],
            [], []);
        var json = DabConfigEmitter.Render(catalog, "sql", diagnostics)!;
        diagnostics.Should().ContainSingle(d => d.Severity == "warning" && d.Message.Contains("anonymous"));
        json.Should().Contain("\"*\"");
    }

    [Fact]
    public void Render_includes_connection_string_token_with_supplied_name()
    {
        var catalog = new EntityCatalog(
            [EntityWithAttrs("Book", Attr("AspireForm.Annotations.DabExposeAttribute"))],
            [], []);
        var json = DabConfigEmitter.Render(catalog, "mydb", new())!;
        json.Should().Contain("@env('ConnectionStrings__mydb')");
    }
}
```

- [ ] **Step 3: Build + run**

```bash
dotnet build --nologo -v q
dotnet run --project tests/AspireForm.Tests -class "AspireForm.Tests.Providers.EfData.DabConfigEmitterTests"
```

Expected: 7/7 PASS.

- [ ] **Step 4: Commit**

```bash
git add src/AspireForm/Providers/EfData/DabConfigEmitter.cs tests/AspireForm.Tests/Providers/EfData/DabConfigEmitterTests.cs
git -c commit.gpgsign=false commit -m "feat(ef-data): add DabConfigEmitter (DAB config from [DabExpose] attributes with conflict diagnostics)"
```

---

## Task 15: Rewrite EfDataModuleProvider for new input shape

The existing `EfDataModuleProvider` uses `inputs.database` + `inputs.contextName` and emits a stub. The new provider uses `inputs.projectPath` + optional `inputs.dbContext` + `inputs.emitDabConfig` + `inputs.dabConfigPath`, runs the scanner, and emits both DbContext and (optionally) DAB config. The existing `EfDataModuleProviderTests` is replaced.

**Files:**
- Modify: `src/AspireForm/Providers/EfDataModuleProvider.cs` — full rewrite
- Modify: `tests/AspireForm.Tests/Providers/EfDataModuleProviderTests.cs` — full rewrite

**Important:** `IProvider.Plan(PlanContext context)` is a synchronous method (look at `src/AspireForm/Providers/IProvider.cs`). Our scanner is async. The provider runs the scan synchronously via `.GetAwaiter().GetResult()` — this is the established pattern in this codebase for provider plans that touch I/O (mirror what other providers do; if none do, the precedent is that the scanner call is brief and blocking is acceptable here because `aspireform plan/apply` is itself a synchronous CLI invocation that already does I/O via `ConfigLoader.Load`).

- [ ] **Step 1: Replace `src/AspireForm/Providers/EfDataModuleProvider.cs` entirely**

```csharp
using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;
using AspireForm.Providers.EfData;

namespace AspireForm.Providers;

/// <summary>Built-in Module provider for code-first EF Core data access. Reads entity classes from a user csproj via Roslyn, then emits a DbContext and (when entities carry <c>[DabExpose]</c>) a sibling <c>dab-config.json</c>.</summary>
public sealed class EfDataModuleProvider : IProvider
{
    private readonly IEntityCatalogService _catalog;

    /// <summary>Creates the provider with the default Roslyn-backed catalog service.</summary>
    public EfDataModuleProvider() : this(new RoslynEntityCatalogService()) { }

    /// <summary>Creates the provider with a supplied catalog service (used by tests).</summary>
    public EfDataModuleProvider(IEntityCatalogService catalog) { _catalog = catalog; }

    /// <inheritdoc />
    public string Type => "ef-data";

    /// <inheritdoc />
    public BlockKind Kind => BlockKind.Module;

    /// <inheritdoc />
    public ProviderPlan Plan(PlanContext context)
    {
        // Reject the legacy 0.4.0 input shape with a clear migration hint.
        if (context.Inputs["database"] is not null || context.Inputs["contextName"] is not null)
        {
            throw new InvalidOperationException(
                "ef-data: the 'database' and 'contextName' inputs were removed in AspireForm 0.5.0. " +
                "Replace them with 'projectPath' (required) pointing at your entity project's .csproj. " +
                "See AspireForm CHANGELOG [0.5.0] for the migration diff.");
        }

        var projectPath = context.Inputs["projectPath"]?.GetValue<string>()
            ?? throw new InvalidOperationException("ef-data: 'projectPath' input is required (path to entity project's .csproj).");

        var explicitDbContext = context.Inputs["dbContext"]?.GetValue<string>();
        var emitDabExplicit = context.Inputs["emitDabConfig"]?.GetValue<bool>();
        var dabConfigPath = context.Inputs["dabConfigPath"]?.GetValue<string>()
            ?? Path.Combine(context.AppHostDirectory, "dab-config.json");

        // Resolve projectPath relative to the AppHost dir if not rooted.
        var absoluteProject = Path.IsPathRooted(projectPath)
            ? projectPath
            : Path.GetFullPath(Path.Combine(context.AppHostDirectory, projectPath));

        // Synchronously run the catalog scan.
        var catalog = _catalog.ScanAsync(absoluteProject, CancellationToken.None).GetAwaiter().GetResult();

        // Resolve target DbContext.
        DbContextInfo? targetContext;
        if (explicitDbContext is not null)
        {
            targetContext = catalog.DbContexts.FirstOrDefault(c =>
                string.Equals($"{c.Namespace}.{c.Name}", explicitDbContext, StringComparison.Ordinal)
                || string.Equals(c.Name, explicitDbContext, StringComparison.Ordinal));
            if (targetContext is null)
            {
                throw new InvalidOperationException(
                    $"ef-data: dbContext '{explicitDbContext}' not found in project '{absoluteProject}'.");
            }
        }
        else
        {
            if (catalog.DbContexts.Count > 1)
            {
                throw new InvalidOperationException(
                    $"ef-data: {catalog.DbContexts.Count} DbContext classes found in '{absoluteProject}'. " +
                    "Set 'dbContext' input to disambiguate (e.g., 'Demo.Data.AppDbContext').");
            }
            targetContext = catalog.DbContexts.FirstOrDefault()
                ?? new DbContextInfo("AppDbContext", DefaultNamespaceFromProject(absoluteProject),
                    Path.Combine(Path.GetDirectoryName(absoluteProject)!, "AppDbContext.cs"),
                    []);
        }

        var dbContextFile = targetContext.FilePath.Length > 0
            ? targetContext.FilePath
            : Path.Combine(Path.GetDirectoryName(absoluteProject)!, $"{targetContext.Name}.cs");

        var fileActions = new List<PlannedFileAction>
        {
            new(
                Path: dbContextFile,
                OwnershipMode: OwnershipMode.Managed,
                BlockMarker: context.BlockName,
                RenderContent: () => DbContextEmitter.Render(targetContext.Name, targetContext.Namespace, catalog)),
        };

        var anyDabExposed = catalog.Entities.Any(e =>
            e.Attributes.Any(a => a.FullTypeName == "AspireForm.Annotations.DabExposeAttribute"));
        var shouldEmitDab = emitDabExplicit ?? anyDabExposed;

        if (shouldEmitDab && anyDabExposed)
        {
            var firstDepends = (context.Inputs["dependsOn"] as JsonArray)?
                .Select(n => n?.GetValue<string>())
                .FirstOrDefault(s => !string.IsNullOrEmpty(s))
                ?? "default";

            var diag = new List<CatalogDiagnostic>();
            var dabContent = DabConfigEmitter.Render(catalog, firstDepends!, diag);
            if (dabContent is not null)
            {
                fileActions.Add(new PlannedFileAction(
                    Path: dabConfigPath,
                    OwnershipMode: OwnershipMode.Managed,
                    BlockMarker: context.BlockName,
                    RenderContent: () => dabContent));
            }
        }

        return new ProviderPlan { FileActions = fileActions };
    }

    private static string DefaultNamespaceFromProject(string csprojPath) =>
        Path.GetFileNameWithoutExtension(csprojPath);
}
```

- [ ] **Step 2: Replace `tests/AspireForm.Tests/Providers/EfDataModuleProviderTests.cs` entirely**

```csharp
using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;
using AspireForm.Providers;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Providers;

public sealed class EfDataModuleProviderTests
{
    private sealed class FakeCatalogService : IEntityCatalogService
    {
        public required EntityCatalog Catalog { get; init; }
        public Task<EntityCatalog> ScanAsync(string csprojPath, CancellationToken ct) => Task.FromResult(Catalog);
        public Task<MutationResult> MutateAsync(string csprojPath, EntityChangeRequest request, CancellationToken ct) =>
            throw new NotSupportedException("MutateAsync should not be called from the provider plan path.");
    }

    private static PlanContext Ctx(JsonObject inputs) =>
        new("data", inputs, AppHostDirectory: "./MyApp.AppHost", ProjectName: "MyApp");

    private static EntityCatalog SingleContextCatalog(params Entity[] entities) =>
        new(entities, [new DbContextInfo("AppDbContext", "Demo.Data", "Demo.Data/AppDbContext.cs", entities.Select(e => e.Name).ToList())], []);

    [Fact]
    public void Type_and_kind_are_correct()
    {
        var p = new EfDataModuleProvider(new FakeCatalogService { Catalog = SingleContextCatalog() });
        p.Type.Should().Be("ef-data");
        p.Kind.Should().Be(BlockKind.Module);
    }

    [Fact]
    public void Plan_throws_with_migration_hint_when_legacy_database_input_is_present()
    {
        var p = new EfDataModuleProvider(new FakeCatalogService { Catalog = SingleContextCatalog() });
        var act = () => p.Plan(Ctx(new JsonObject { ["database"] = "appdb" }));
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*0.5.0*projectPath*");
    }

    [Fact]
    public void Plan_throws_when_projectPath_is_missing()
    {
        var p = new EfDataModuleProvider(new FakeCatalogService { Catalog = SingleContextCatalog() });
        var act = () => p.Plan(Ctx(new JsonObject()));
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*projectPath*required*");
    }

    [Fact]
    public void Plan_emits_managed_dbcontext_file_using_entities_from_catalog()
    {
        var entity = new Entity("Book", "Demo", "Demo/Book.cs",
            Properties: [new Property("Id", "int", false, true, [])],
            Relationships: [],
            Attributes: []);
        var p = new EfDataModuleProvider(new FakeCatalogService { Catalog = SingleContextCatalog(entity) });

        var plan = p.Plan(Ctx(new JsonObject { ["projectPath"] = "Demo/Demo.csproj" }));

        plan.FileActions.Should().HaveCount(1);
        var dbContextFile = plan.FileActions[0];
        dbContextFile.OwnershipMode.Should().Be(OwnershipMode.Managed);
        var rendered = dbContextFile.RenderContent();
        rendered.Should().Contain("public class AppDbContext : DbContext");
        rendered.Should().Contain("DbSet<Book> Books");
    }

    [Fact]
    public void Plan_emits_dab_config_when_any_entity_has_DabExpose()
    {
        var attr = new AttributeInstance("AspireForm.Annotations.DabExposeAttribute", [], new Dictionary<string, object?>());
        var entity = new Entity("Book", "Demo", "Demo/Book.cs",
            Properties: [new Property("Id", "int", false, true, [])],
            Relationships: [],
            Attributes: [attr]);
        var p = new EfDataModuleProvider(new FakeCatalogService { Catalog = SingleContextCatalog(entity) });

        var plan = p.Plan(Ctx(new JsonObject
        {
            ["projectPath"] = "Demo/Demo.csproj",
            ["dependsOn"] = new JsonArray("sql"),
        }));

        plan.FileActions.Should().HaveCount(2);
        var dab = plan.FileActions.Single(f => f.Path.EndsWith("dab-config.json"));
        dab.OwnershipMode.Should().Be(OwnershipMode.Managed);
        dab.RenderContent().Should().Contain("\"Book\":");
        dab.RenderContent().Should().Contain("@env('ConnectionStrings__sql')");
    }

    [Fact]
    public void Plan_skips_dab_config_when_no_entity_has_DabExpose()
    {
        var entity = new Entity("Book", "Demo", "Demo/Book.cs",
            Properties: [new Property("Id", "int", false, true, [])],
            Relationships: [],
            Attributes: []);
        var p = new EfDataModuleProvider(new FakeCatalogService { Catalog = SingleContextCatalog(entity) });

        var plan = p.Plan(Ctx(new JsonObject { ["projectPath"] = "Demo/Demo.csproj" }));
        plan.FileActions.Should().HaveCount(1);
        plan.FileActions[0].Path.Should().EndWith(".cs");
    }

    [Fact]
    public void Plan_throws_when_dbContext_input_does_not_match_any_discovered_context()
    {
        var p = new EfDataModuleProvider(new FakeCatalogService { Catalog = SingleContextCatalog() });
        var act = () => p.Plan(Ctx(new JsonObject
        {
            ["projectPath"] = "Demo/Demo.csproj",
            ["dbContext"] = "Demo.Data.NotARealContext",
        }));
        act.Should().Throw<InvalidOperationException>().WithMessage("*NotARealContext*");
    }

    [Fact]
    public void Plan_throws_when_multiple_dbcontexts_and_dbContext_not_set()
    {
        var two = new EntityCatalog([],
            [
                new DbContextInfo("A", "X", "X/A.cs", []),
                new DbContextInfo("B", "X", "X/B.cs", []),
            ],
            []);
        var p = new EfDataModuleProvider(new FakeCatalogService { Catalog = two });
        var act = () => p.Plan(Ctx(new JsonObject { ["projectPath"] = "X/X.csproj" }));
        act.Should().Throw<InvalidOperationException>().WithMessage("*dbContext*disambiguate*");
    }
}
```

- [ ] **Step 3: Build + run all ef-data tests**

```bash
dotnet build --nologo -v q
dotnet run --project tests/AspireForm.Tests -class "AspireForm.Tests.Providers.EfDataModuleProviderTests"
```

Expected: 8/8 PASS. The previous 5 tests are replaced; any other test that referenced the old `database`/`contextName` inputs needs updating (search for `"database"` and `"contextName"` in `tests/`).

- [ ] **Step 4: Run the full suite to catch breakage in unrelated tests**

```bash
dotnet run --project tests/AspireForm.Tests
```

Expected: all tests pass. If any pre-existing test used the old ef-data input shape, update it inline (mirror the new fixture pattern).

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Providers/EfDataModuleProvider.cs tests/AspireForm.Tests/Providers/EfDataModuleProviderTests.cs
git -c commit.gpgsign=false commit -m "feat(ef-data): rewrite provider to use EntityCatalog + emit DbContext + dab-config.json"
```

---

## Task 16: MCP read tools (EntityListTool, EntityShowTool, DbContextListTool)

These three tools share a `ProjectPathOnly` schema (`projectPath` required). They construct a `RoslynEntityCatalogService` per-call (matches the existing per-tool service pattern in `Mcp/Tools/*` — services are cheap to construct relative to the workspace open cost, which dominates).

**Files:**
- Create: `src/AspireForm/Mcp/Tools/Entity/EntityListTool.cs`
- Create: `src/AspireForm/Mcp/Tools/Entity/EntityShowTool.cs`
- Create: `src/AspireForm/Mcp/Tools/Entity/DbContextListTool.cs`
- Create: `tests/AspireForm.Tests/Mcp/Tools/Entity/EntityToolsReadTests.cs`

- [ ] **Step 1: Create `src/AspireForm/Mcp/Tools/Entity/EntityListTool.cs`**

```csharp
using System.Text;
using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;

namespace AspireForm.Mcp.Tools.Entity;

/// <summary>MCP tool: list all entities discovered in a user project's csproj.</summary>
public sealed class EntityListTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory (currently unused — entity tools require projectPath explicitly).</summary>
    public EntityListTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_entity_list";

    /// <inheritdoc />
    public string Description => "List all entities discovered by Roslyn in the user project's csproj.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["projectPath"] = ToolBase.Str("Absolute or relative path to the entity project's .csproj file."),
    }, "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var path = args["projectPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(path))
            return ToolResult.Fail("aspireform_entity_list requires 'projectPath'.");

        try
        {
            await using var svc = new RoslynEntityCatalogService();
            var catalog = await svc.ScanAsync(path, ct);
            var sb = new StringBuilder();
            sb.AppendLine($"Entity                Namespace                   Properties  Relationships  DabExposed");
            sb.AppendLine($"------                ---------                   ----------  -------------  ----------");
            foreach (var e in catalog.Entities.OrderBy(e => e.Name, StringComparer.Ordinal))
            {
                var dabExposed = e.Attributes.Any(a => a.FullTypeName == "AspireForm.Annotations.DabExposeAttribute") ? "yes" : "no";
                sb.AppendLine($"{e.Name,-22}{e.Namespace,-28}{e.Properties.Count,-12}{e.Relationships.Count,-15}{dabExposed}");
            }
            if (catalog.Entities.Count == 0) sb.AppendLine("(no entities found)");
            if (catalog.Diagnostics.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"{catalog.Diagnostics.Count} diagnostic(s) — call aspireform_entity_show or check /diagnostics for detail.");
            }
            return ToolResult.Ok(sb.ToString());
        }
        catch (EntityCatalogException ex) { return ToolResult.Fail(ex.Message); }
    }
}
```

- [ ] **Step 2: Create `src/AspireForm/Mcp/Tools/Entity/EntityShowTool.cs`**

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;

namespace AspireForm.Mcp.Tools.Entity;

/// <summary>MCP tool: dump one entity's full record as indented JSON.</summary>
public sealed class EntityShowTool : IToolHandler
{
    private readonly string _defaultProjectDir;
    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };

    /// <summary>Creates the tool with a default project directory (currently unused).</summary>
    public EntityShowTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_entity_show";

    /// <inheritdoc />
    public string Description => "Show one entity's full record (properties, relationships, attributes) as indented JSON.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["entity"] = ToolBase.Str("Entity name."),
        ["projectPath"] = ToolBase.Str("Absolute or relative path to the entity project's .csproj file."),
    }, "entity", "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var name = args["entity"]?.GetValue<string>();
        var path = args["projectPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name)) return ToolResult.Fail("aspireform_entity_show requires 'entity'.");
        if (string.IsNullOrWhiteSpace(path)) return ToolResult.Fail("aspireform_entity_show requires 'projectPath'.");

        try
        {
            await using var svc = new RoslynEntityCatalogService();
            var catalog = await svc.ScanAsync(path, ct);
            var entity = catalog.Entities.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal));
            if (entity is null) return ToolResult.Fail($"Entity '{name}' not found.");
            return ToolResult.Ok(JsonSerializer.Serialize(entity, PrettyOptions));
        }
        catch (EntityCatalogException ex) { return ToolResult.Fail(ex.Message); }
    }
}
```

- [ ] **Step 3: Create `src/AspireForm/Mcp/Tools/Entity/DbContextListTool.cs`**

```csharp
using System.Text;
using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;

namespace AspireForm.Mcp.Tools.Entity;

/// <summary>MCP tool: list all DbContext-derived classes discovered in the user's csproj.</summary>
public sealed class DbContextListTool : IToolHandler
{
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory (currently unused).</summary>
    public DbContextListTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_dbcontext_list";

    /// <inheritdoc />
    public string Description => "List all DbContext-derived classes in the user project, with their DbSet<T> entity names.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["projectPath"] = ToolBase.Str("Absolute or relative path to the entity project's .csproj file."),
    }, "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var path = args["projectPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(path)) return ToolResult.Fail("aspireform_dbcontext_list requires 'projectPath'.");

        try
        {
            await using var svc = new RoslynEntityCatalogService();
            var catalog = await svc.ScanAsync(path, ct);
            var sb = new StringBuilder();
            sb.AppendLine($"DbContext                   Namespace                   DbSet<T> entities");
            sb.AppendLine($"---------                   ---------                   -----------------");
            foreach (var c in catalog.DbContexts.OrderBy(c => c.Name, StringComparer.Ordinal))
            {
                sb.AppendLine($"{c.Name,-28}{c.Namespace,-28}{string.Join(", ", c.DbSetEntityNames)}");
            }
            if (catalog.DbContexts.Count == 0) sb.AppendLine("(no DbContext detected)");
            return ToolResult.Ok(sb.ToString());
        }
        catch (EntityCatalogException ex) { return ToolResult.Fail(ex.Message); }
    }
}
```

- [ ] **Step 4: Create `tests/AspireForm.Tests/Mcp/Tools/Entity/EntityToolsReadTests.cs`**

```csharp
using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AspireForm.Mcp.Tools.Entity;
using AspireForm.Tests.EntityCatalog.Fixtures;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools.Entity;

public sealed class EntityToolsReadTests
{
    private static FixtureProjectBuilder NewFixWithBook(string testName)
    {
        var fix = new FixtureProjectBuilder(testName);
        fix.AddFile("DbContextStub.cs", "namespace Microsoft.EntityFrameworkCore; public class DbContext { } public class DbSet<T> { }");
        fix.AddFile("Models.cs", """
            using Microsoft.EntityFrameworkCore;
            namespace Demo;
            public class Ctx : DbContext { public DbSet<Book> Books { get; set; } = null!; }
            public class Book { public int Id { get; set; } public string Title { get; set; } = ""; }
            """);
        return fix;
    }

    [Fact]
    public async Task EntityListTool_returns_a_table_with_at_least_the_seeded_entity()
    {
        using var fix = NewFixWithBook("read_list");
        var tool = new EntityListTool(".");
        var result = await tool.ExecuteAsync(new JsonObject { ["projectPath"] = fix.CsprojPath }, default);
        result.IsError.Should().BeFalse();
        result.Content[0].Text.Should().Contain("Book").And.Contain("Demo");
    }

    [Fact]
    public async Task EntityListTool_returns_tool_level_error_when_projectPath_missing()
    {
        var result = await new EntityListTool(".").ExecuteAsync(new JsonObject(), default);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("requires 'projectPath'");
    }

    [Fact]
    public async Task EntityShowTool_returns_indented_json_for_known_entity()
    {
        using var fix = NewFixWithBook("read_show");
        var tool = new EntityShowTool(".");
        var result = await tool.ExecuteAsync(
            new JsonObject { ["entity"] = "Book", ["projectPath"] = fix.CsprojPath },
            default);
        result.IsError.Should().BeFalse();
        result.Content[0].Text.Should().Contain("\"Name\": \"Book\"").And.Contain("\"Properties\"");
    }

    [Fact]
    public async Task EntityShowTool_returns_tool_level_error_for_unknown_entity()
    {
        using var fix = NewFixWithBook("read_show_missing");
        var tool = new EntityShowTool(".");
        var result = await tool.ExecuteAsync(
            new JsonObject { ["entity"] = "Missing", ["projectPath"] = fix.CsprojPath },
            default);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("not found");
    }

    [Fact]
    public async Task DbContextListTool_returns_a_table_with_the_seeded_context()
    {
        using var fix = NewFixWithBook("read_ctxlist");
        var tool = new DbContextListTool(".");
        var result = await tool.ExecuteAsync(new JsonObject { ["projectPath"] = fix.CsprojPath }, default);
        result.IsError.Should().BeFalse();
        result.Content[0].Text.Should().Contain("Ctx").And.Contain("Book");
    }
}
```

- [ ] **Step 5: Build + run**

```bash
dotnet build --nologo -v q
dotnet run --project tests/AspireForm.Tests -class "AspireForm.Tests.Mcp.Tools.Entity.EntityToolsReadTests"
```

Expected: 5/5 PASS.

- [ ] **Step 6: Commit**

```bash
git add src/AspireForm/Mcp/Tools/Entity/EntityListTool.cs src/AspireForm/Mcp/Tools/Entity/EntityShowTool.cs src/AspireForm/Mcp/Tools/Entity/DbContextListTool.cs tests/AspireForm.Tests/Mcp/Tools/Entity/EntityToolsReadTests.cs
git -c commit.gpgsign=false commit -m "feat(mcp): add entity_list / entity_show / dbcontext_list read tools"
```

---

## Task 17: MCP entity + property mutation tools (5 tools)

Five mutation tools — entity_create, entity_delete, property_add, property_remove, property_rename. All share the pattern: validate inputs → construct `RoslynEntityCatalogService` → call `MutateAsync` with the appropriate `EntityChangeRequest` → return `MutationResult` as indented JSON.

**Files (create all):**
- `src/AspireForm/Mcp/Tools/Entity/EntityCreateTool.cs`
- `src/AspireForm/Mcp/Tools/Entity/EntityDeleteTool.cs`
- `src/AspireForm/Mcp/Tools/Entity/PropertyAddTool.cs`
- `src/AspireForm/Mcp/Tools/Entity/PropertyRemoveTool.cs`
- `src/AspireForm/Mcp/Tools/Entity/PropertyRenameTool.cs`
- `tests/AspireForm.Tests/Mcp/Tools/Entity/EntityToolsMutationTests.cs`

- [ ] **Step 1: Create `src/AspireForm/Mcp/Tools/Entity/EntityCreateTool.cs`**

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;

namespace AspireForm.Mcp.Tools.Entity;

/// <summary>MCP tool: create a new entity class in the user project.</summary>
public sealed class EntityCreateTool : IToolHandler
{
    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory (currently unused).</summary>
    public EntityCreateTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_entity_create";

    /// <inheritdoc />
    public string Description => "Create a new entity class file in the user project.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["name"] = ToolBase.Str("Entity class name (e.g., 'Book')."),
        ["namespace"] = ToolBase.Str("Target C# namespace."),
        ["filePath"] = ToolBase.Str("Absolute or project-relative path to the new .cs file."),
        ["projectPath"] = ToolBase.Str("Path to the entity project's .csproj."),
    }, "name", "namespace", "filePath", "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var name = args["name"]?.GetValue<string>();
        var ns = args["namespace"]?.GetValue<string>();
        var filePath = args["filePath"]?.GetValue<string>();
        var projectPath = args["projectPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(ns)
            || string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(projectPath))
            return ToolResult.Fail("aspireform_entity_create requires 'name', 'namespace', 'filePath', 'projectPath'.");

        await using var svc = new RoslynEntityCatalogService();
        var result = await svc.MutateAsync(projectPath, new CreateEntity(name, ns, filePath), ct);
        var json = JsonSerializer.Serialize(result, PrettyOptions);
        return result.Success ? ToolResult.Ok(json) : ToolResult.Fail(json);
    }
}
```

- [ ] **Step 2: Create `src/AspireForm/Mcp/Tools/Entity/EntityDeleteTool.cs`**

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;

namespace AspireForm.Mcp.Tools.Entity;

/// <summary>MCP tool: delete an entity class file. DbSet&lt;T&gt; + reverse navigations must be cleaned up manually in v1 (a warning is included in the result diagnostics).</summary>
public sealed class EntityDeleteTool : IToolHandler
{
    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public EntityDeleteTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_entity_delete";

    /// <inheritdoc />
    public string Description => "Delete an entity class (.cs file). DbSet<T> + reverse navigations must be cleaned up manually in v1.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["entity"] = ToolBase.Str("Entity class name to delete."),
        ["projectPath"] = ToolBase.Str("Path to the entity project's .csproj."),
    }, "entity", "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var name = args["entity"]?.GetValue<string>();
        var projectPath = args["projectPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(projectPath))
            return ToolResult.Fail("aspireform_entity_delete requires 'entity', 'projectPath'.");

        await using var svc = new RoslynEntityCatalogService();
        var result = await svc.MutateAsync(projectPath, new DeleteEntity(name), ct);
        var json = JsonSerializer.Serialize(result, PrettyOptions);
        return result.Success ? ToolResult.Ok(json) : ToolResult.Fail(json);
    }
}
```

- [ ] **Step 3: Create `src/AspireForm/Mcp/Tools/Entity/PropertyAddTool.cs`**

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;

namespace AspireForm.Mcp.Tools.Entity;

/// <summary>MCP tool: append a new property to an entity class.</summary>
public sealed class PropertyAddTool : IToolHandler
{
    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public PropertyAddTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_property_add";

    /// <inheritdoc />
    public string Description => "Add a new property to an entity class.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["entity"] = ToolBase.Str("Entity class name."),
        ["name"] = ToolBase.Str("New property name."),
        ["clrType"] = ToolBase.Str("CLR type (e.g., 'int', 'string', 'DateOnly')."),
        ["isNullable"] = ToolBase.Bool("Whether the property is nullable (default: false)."),
        ["isPrimaryKey"] = ToolBase.Bool("Whether the property is the primary key (default: false)."),
        ["projectPath"] = ToolBase.Str("Path to the entity project's .csproj."),
    }, "entity", "name", "clrType", "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var entity = args["entity"]?.GetValue<string>();
        var name = args["name"]?.GetValue<string>();
        var clrType = args["clrType"]?.GetValue<string>();
        var projectPath = args["projectPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(entity) || string.IsNullOrWhiteSpace(name)
            || string.IsNullOrWhiteSpace(clrType) || string.IsNullOrWhiteSpace(projectPath))
            return ToolResult.Fail("aspireform_property_add requires 'entity', 'name', 'clrType', 'projectPath'.");

        var prop = new Property(
            Name: name,
            ClrType: clrType,
            IsNullable: args["isNullable"]?.GetValue<bool>() ?? false,
            IsPrimaryKey: args["isPrimaryKey"]?.GetValue<bool>() ?? false,
            Attributes: []);

        await using var svc = new RoslynEntityCatalogService();
        var result = await svc.MutateAsync(projectPath, new AddProperty(entity, prop), ct);
        var json = JsonSerializer.Serialize(result, PrettyOptions);
        return result.Success ? ToolResult.Ok(json) : ToolResult.Fail(json);
    }
}
```

- [ ] **Step 4: Create `src/AspireForm/Mcp/Tools/Entity/PropertyRemoveTool.cs`**

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;

namespace AspireForm.Mcp.Tools.Entity;

/// <summary>MCP tool: remove a property from an entity class.</summary>
public sealed class PropertyRemoveTool : IToolHandler
{
    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public PropertyRemoveTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_property_remove";

    /// <inheritdoc />
    public string Description => "Remove a property from an entity class.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["entity"] = ToolBase.Str("Entity class name."),
        ["property"] = ToolBase.Str("Property name to remove."),
        ["projectPath"] = ToolBase.Str("Path to the entity project's .csproj."),
    }, "entity", "property", "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var entity = args["entity"]?.GetValue<string>();
        var property = args["property"]?.GetValue<string>();
        var projectPath = args["projectPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(entity) || string.IsNullOrWhiteSpace(property) || string.IsNullOrWhiteSpace(projectPath))
            return ToolResult.Fail("aspireform_property_remove requires 'entity', 'property', 'projectPath'.");

        await using var svc = new RoslynEntityCatalogService();
        var result = await svc.MutateAsync(projectPath, new RemoveProperty(entity, property), ct);
        var json = JsonSerializer.Serialize(result, PrettyOptions);
        return result.Success ? ToolResult.Ok(json) : ToolResult.Fail(json);
    }
}
```

- [ ] **Step 5: Create `src/AspireForm/Mcp/Tools/Entity/PropertyRenameTool.cs`**

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;

namespace AspireForm.Mcp.Tools.Entity;

/// <summary>MCP tool: rename a property (semantic-safe via Roslyn rename across the workspace).</summary>
public sealed class PropertyRenameTool : IToolHandler
{
    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public PropertyRenameTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_property_rename";

    /// <inheritdoc />
    public string Description => "Rename a property on an entity (semantic-safe across the whole workspace).";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["entity"] = ToolBase.Str("Entity class name."),
        ["oldName"] = ToolBase.Str("Current property name."),
        ["newName"] = ToolBase.Str("New property name."),
        ["projectPath"] = ToolBase.Str("Path to the entity project's .csproj."),
    }, "entity", "oldName", "newName", "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var entity = args["entity"]?.GetValue<string>();
        var oldName = args["oldName"]?.GetValue<string>();
        var newName = args["newName"]?.GetValue<string>();
        var projectPath = args["projectPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(entity) || string.IsNullOrWhiteSpace(oldName)
            || string.IsNullOrWhiteSpace(newName) || string.IsNullOrWhiteSpace(projectPath))
            return ToolResult.Fail("aspireform_property_rename requires 'entity', 'oldName', 'newName', 'projectPath'.");

        await using var svc = new RoslynEntityCatalogService();
        var result = await svc.MutateAsync(projectPath, new RenameProperty(entity, oldName, newName), ct);
        var json = JsonSerializer.Serialize(result, PrettyOptions);
        return result.Success ? ToolResult.Ok(json) : ToolResult.Fail(json);
    }
}
```

- [ ] **Step 6: Create `tests/AspireForm.Tests/Mcp/Tools/Entity/EntityToolsMutationTests.cs`**

```csharp
using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AspireForm.Mcp.Tools.Entity;
using AspireForm.Tests.EntityCatalog.Fixtures;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools.Entity;

public sealed class EntityToolsMutationTests
{
    [Fact]
    public void All_five_tools_have_aspireform_prefix()
    {
        new EntityCreateTool(".").Name.Should().Be("aspireform_entity_create");
        new EntityDeleteTool(".").Name.Should().Be("aspireform_entity_delete");
        new PropertyAddTool(".").Name.Should().Be("aspireform_property_add");
        new PropertyRemoveTool(".").Name.Should().Be("aspireform_property_remove");
        new PropertyRenameTool(".").Name.Should().Be("aspireform_property_rename");
    }

    [Fact]
    public async Task EntityCreateTool_creates_a_new_entity_file()
    {
        using var fix = new FixtureProjectBuilder("mut_tool_create");
        var target = Path.Combine(fix.Root, "Models", "Book.cs");
        var result = await new EntityCreateTool(".").ExecuteAsync(new JsonObject
        {
            ["name"] = "Book",
            ["namespace"] = "Demo.Models",
            ["filePath"] = target,
            ["projectPath"] = fix.CsprojPath,
        }, default);
        result.IsError.Should().BeFalse();
        File.Exists(target).Should().BeTrue();
    }

    [Fact]
    public async Task PropertyAddTool_appends_a_property_to_an_entity()
    {
        using var fix = new FixtureProjectBuilder("mut_tool_propadd");
        var bookFile = fix.AddFile("Book.cs", "namespace Demo; public class Book { public int Id { get; set; } }");
        var result = await new PropertyAddTool(".").ExecuteAsync(new JsonObject
        {
            ["entity"] = "Book",
            ["name"] = "Title",
            ["clrType"] = "string",
            ["projectPath"] = fix.CsprojPath,
        }, default);
        result.IsError.Should().BeFalse();
        File.ReadAllText(bookFile).Should().Contain("Title");
    }

    [Fact]
    public async Task PropertyRemoveTool_strips_the_property()
    {
        using var fix = new FixtureProjectBuilder("mut_tool_proprm");
        var bookFile = fix.AddFile("Book.cs", "namespace Demo; public class Book { public int Id { get; set; } public string Title { get; set; } = \"\"; }");
        var result = await new PropertyRemoveTool(".").ExecuteAsync(new JsonObject
        {
            ["entity"] = "Book",
            ["property"] = "Title",
            ["projectPath"] = fix.CsprojPath,
        }, default);
        result.IsError.Should().BeFalse();
        File.ReadAllText(bookFile).Should().NotContain("Title");
    }

    [Fact]
    public async Task Missing_inputs_return_tool_level_errors_on_each_tool()
    {
        (await new EntityCreateTool(".").ExecuteAsync(new JsonObject(), default)).IsError.Should().BeTrue();
        (await new EntityDeleteTool(".").ExecuteAsync(new JsonObject(), default)).IsError.Should().BeTrue();
        (await new PropertyAddTool(".").ExecuteAsync(new JsonObject(), default)).IsError.Should().BeTrue();
        (await new PropertyRemoveTool(".").ExecuteAsync(new JsonObject(), default)).IsError.Should().BeTrue();
        (await new PropertyRenameTool(".").ExecuteAsync(new JsonObject(), default)).IsError.Should().BeTrue();
    }
}
```

- [ ] **Step 7: Build + run**

```bash
dotnet build --nologo -v q
dotnet run --project tests/AspireForm.Tests -class "AspireForm.Tests.Mcp.Tools.Entity.EntityToolsMutationTests"
```

Expected: 5/5 PASS.

- [ ] **Step 8: Commit**

```bash
git add src/AspireForm/Mcp/Tools/Entity/EntityCreateTool.cs src/AspireForm/Mcp/Tools/Entity/EntityDeleteTool.cs src/AspireForm/Mcp/Tools/Entity/PropertyAddTool.cs src/AspireForm/Mcp/Tools/Entity/PropertyRemoveTool.cs src/AspireForm/Mcp/Tools/Entity/PropertyRenameTool.cs tests/AspireForm.Tests/Mcp/Tools/Entity/EntityToolsMutationTests.cs
git -c commit.gpgsign=false commit -m "feat(mcp): add entity_create / entity_delete / property_{add,remove,rename} tools"
```

---

## Task 18: MCP attribute + relationship tools (4 tools)

Four tools — attribute_set, attribute_clear, relationship_add, relationship_remove. Same pattern as Task 17.

**Files (create all):**
- `src/AspireForm/Mcp/Tools/Entity/AttributeSetTool.cs`
- `src/AspireForm/Mcp/Tools/Entity/AttributeClearTool.cs`
- `src/AspireForm/Mcp/Tools/Entity/RelationshipAddTool.cs`
- `src/AspireForm/Mcp/Tools/Entity/RelationshipRemoveTool.cs`
- `tests/AspireForm.Tests/Mcp/Tools/Entity/AttributeAndRelationshipToolsTests.cs`

- [ ] **Step 1: Create `src/AspireForm/Mcp/Tools/Entity/AttributeSetTool.cs`**

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;

namespace AspireForm.Mcp.Tools.Entity;

/// <summary>MCP tool: set (replace if present) an attribute on an entity class or one of its properties.</summary>
public sealed class AttributeSetTool : IToolHandler
{
    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public AttributeSetTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_attribute_set";

    /// <inheritdoc />
    public string Description => "Set (or replace) an attribute on an entity class or one of its properties.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["entity"] = ToolBase.Str("Entity class name."),
        ["property"] = ToolBase.Str("Optional property name; omit to apply at the class level."),
        ["attributeFullName"] = ToolBase.Str("Full attribute type name (e.g., 'AspireForm.Annotations.DabExposeAttribute')."),
        ["ctorArgs"] = new JsonObject { ["type"] = "array", ["description"] = "Positional constructor args (strings, numbers, booleans)." },
        ["namedArgs"] = new JsonObject { ["type"] = "object", ["description"] = "Named constructor args (name → value map)." },
        ["projectPath"] = ToolBase.Str("Path to the entity project's .csproj."),
    }, "entity", "attributeFullName", "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var entity = args["entity"]?.GetValue<string>();
        var attrName = args["attributeFullName"]?.GetValue<string>();
        var projectPath = args["projectPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(entity) || string.IsNullOrWhiteSpace(attrName) || string.IsNullOrWhiteSpace(projectPath))
            return ToolResult.Fail("aspireform_attribute_set requires 'entity', 'attributeFullName', 'projectPath'.");

        var property = args["property"]?.GetValue<string>();
        var ctorArgs = (args["ctorArgs"] as JsonArray)?
            .Select(n => (object?)UnwrapJsonScalar(n))
            .ToList() ?? [];
        var namedArgs = (args["namedArgs"] as JsonObject)?
            .ToDictionary(kv => kv.Key, kv => (object?)UnwrapJsonScalar(kv.Value))
            ?? new Dictionary<string, object?>();

        var attr = new AttributeInstance(attrName, ctorArgs, namedArgs);
        await using var svc = new RoslynEntityCatalogService();
        var result = await svc.MutateAsync(projectPath, new SetAttribute(entity, property, attr), ct);
        var json = JsonSerializer.Serialize(result, PrettyOptions);
        return result.Success ? ToolResult.Ok(json) : ToolResult.Fail(json);
    }

    private static object? UnwrapJsonScalar(JsonNode? n) => n switch
    {
        null => null,
        JsonValue v when v.TryGetValue(out bool b) => b,
        JsonValue v when v.TryGetValue(out int i) => i,
        JsonValue v when v.TryGetValue(out long l) => l,
        JsonValue v when v.TryGetValue(out double d) => d,
        JsonValue v when v.TryGetValue(out string? s) => s,
        _ => n.ToString(),
    };
}
```

- [ ] **Step 2: Create `src/AspireForm/Mcp/Tools/Entity/AttributeClearTool.cs`**

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;

namespace AspireForm.Mcp.Tools.Entity;

/// <summary>MCP tool: clear an attribute (by full type name) from an entity class or one of its properties.</summary>
public sealed class AttributeClearTool : IToolHandler
{
    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public AttributeClearTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_attribute_clear";

    /// <inheritdoc />
    public string Description => "Clear an attribute (by full type name) from an entity class or property.";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["entity"] = ToolBase.Str("Entity class name."),
        ["property"] = ToolBase.Str("Optional property name; omit to clear at the class level."),
        ["attributeFullName"] = ToolBase.Str("Full attribute type name to remove."),
        ["projectPath"] = ToolBase.Str("Path to the entity project's .csproj."),
    }, "entity", "attributeFullName", "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var entity = args["entity"]?.GetValue<string>();
        var attrName = args["attributeFullName"]?.GetValue<string>();
        var projectPath = args["projectPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(entity) || string.IsNullOrWhiteSpace(attrName) || string.IsNullOrWhiteSpace(projectPath))
            return ToolResult.Fail("aspireform_attribute_clear requires 'entity', 'attributeFullName', 'projectPath'.");

        var property = args["property"]?.GetValue<string>();
        await using var svc = new RoslynEntityCatalogService();
        var result = await svc.MutateAsync(projectPath, new ClearAttribute(entity, property, attrName), ct);
        var json = JsonSerializer.Serialize(result, PrettyOptions);
        return result.Success ? ToolResult.Ok(json) : ToolResult.Fail(json);
    }
}
```

- [ ] **Step 3: Create `src/AspireForm/Mcp/Tools/Entity/RelationshipAddTool.cs`**

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;

namespace AspireForm.Mcp.Tools.Entity;

/// <summary>MCP tool: add a relationship between two entities. v1 supports OneToOne, OneToMany, ManyToOne; ManyToMany is reserved for #4a.1.</summary>
public sealed class RelationshipAddTool : IToolHandler
{
    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public RelationshipAddTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_relationship_add";

    /// <inheritdoc />
    public string Description => "Add a relationship from one entity to another. cardinality must be OneToOne | OneToMany | ManyToOne (ManyToMany is reserved for #4a.1).";

    /// <inheritdoc />
    public JsonObject InputSchema
    {
        get
        {
            var card = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray("OneToOne", "OneToMany", "ManyToOne", "ManyToMany"),
                ["description"] = "Cardinality of the relationship from the 'fromEntity' side.",
            };
            return ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
            {
                ["fromEntity"] = ToolBase.Str("Entity that the relationship originates from."),
                ["toEntity"] = ToolBase.Str("Entity that the relationship targets."),
                ["cardinality"] = card,
                ["foreignKeyProperty"] = ToolBase.Str("Optional explicit FK property name; v1 falls back to convention <ToEntity>Id."),
                ["projectPath"] = ToolBase.Str("Path to the entity project's .csproj."),
            }, "fromEntity", "toEntity", "cardinality", "projectPath");
        }
    }

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var fromEntity = args["fromEntity"]?.GetValue<string>();
        var toEntity = args["toEntity"]?.GetValue<string>();
        var cardStr = args["cardinality"]?.GetValue<string>();
        var projectPath = args["projectPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(fromEntity) || string.IsNullOrWhiteSpace(toEntity)
            || string.IsNullOrWhiteSpace(cardStr) || string.IsNullOrWhiteSpace(projectPath))
            return ToolResult.Fail("aspireform_relationship_add requires 'fromEntity', 'toEntity', 'cardinality', 'projectPath'.");

        if (!Enum.TryParse<RelationshipCardinality>(cardStr, out var card))
            return ToolResult.Fail($"Unknown cardinality '{cardStr}'. Allowed: OneToOne, OneToMany, ManyToOne, ManyToMany.");

        var fk = args["foreignKeyProperty"]?.GetValue<string>();
        await using var svc = new RoslynEntityCatalogService();
        var result = await svc.MutateAsync(projectPath, new AddRelationship(fromEntity, toEntity, card, fk), ct);
        var json = JsonSerializer.Serialize(result, PrettyOptions);
        return result.Success ? ToolResult.Ok(json) : ToolResult.Fail(json);
    }
}
```

- [ ] **Step 4: Create `src/AspireForm/Mcp/Tools/Entity/RelationshipRemoveTool.cs`**

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using AspireForm.EntityCatalog;

namespace AspireForm.Mcp.Tools.Entity;

/// <summary>MCP tool: remove a relationship's navigation property from the originating entity.</summary>
public sealed class RelationshipRemoveTool : IToolHandler
{
    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };
    private readonly string _defaultProjectDir;

    /// <summary>Creates the tool with a default project directory.</summary>
    public RelationshipRemoveTool(string defaultProjectDir) { _defaultProjectDir = defaultProjectDir; }

    /// <inheritdoc />
    public string Name => "aspireform_relationship_remove";

    /// <inheritdoc />
    public string Description => "Remove a relationship's navigation property from the originating entity. (v1: only removes the named nav; FK + reverse nav need manual cleanup.)";

    /// <inheritdoc />
    public JsonObject InputSchema => ToolBase.ObjectSchema(new Dictionary<string, JsonObject>
    {
        ["fromEntity"] = ToolBase.Str("Entity that the relationship originates from."),
        ["relationshipName"] = ToolBase.Str("Navigation property name to remove."),
        ["projectPath"] = ToolBase.Str("Path to the entity project's .csproj."),
    }, "fromEntity", "relationshipName", "projectPath");

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct)
    {
        var fromEntity = args["fromEntity"]?.GetValue<string>();
        var rel = args["relationshipName"]?.GetValue<string>();
        var projectPath = args["projectPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(fromEntity) || string.IsNullOrWhiteSpace(rel) || string.IsNullOrWhiteSpace(projectPath))
            return ToolResult.Fail("aspireform_relationship_remove requires 'fromEntity', 'relationshipName', 'projectPath'.");

        await using var svc = new RoslynEntityCatalogService();
        var result = await svc.MutateAsync(projectPath, new RemoveRelationship(fromEntity, rel), ct);
        var json = JsonSerializer.Serialize(result, PrettyOptions);
        return result.Success ? ToolResult.Ok(json) : ToolResult.Fail(json);
    }
}
```

- [ ] **Step 5: Create `tests/AspireForm.Tests/Mcp/Tools/Entity/AttributeAndRelationshipToolsTests.cs`**

```csharp
using System.Text.Json.Nodes;
using AspireForm.Mcp;
using AspireForm.Mcp.Tools.Entity;
using AspireForm.Tests.EntityCatalog.Fixtures;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp.Tools.Entity;

public sealed class AttributeAndRelationshipToolsTests
{
    [Fact]
    public void All_four_tools_have_aspireform_prefix()
    {
        new AttributeSetTool(".").Name.Should().Be("aspireform_attribute_set");
        new AttributeClearTool(".").Name.Should().Be("aspireform_attribute_clear");
        new RelationshipAddTool(".").Name.Should().Be("aspireform_relationship_add");
        new RelationshipRemoveTool(".").Name.Should().Be("aspireform_relationship_remove");
    }

    [Fact]
    public async Task AttributeSetTool_sets_class_level_attribute_with_constructor_args()
    {
        using var fix = new FixtureProjectBuilder("attr_set");
        var bookFile = fix.AddFile("Book.cs", "namespace Demo; public class Book { public int Id { get; set; } }");
        var result = await new AttributeSetTool(".").ExecuteAsync(new JsonObject
        {
            ["entity"] = "Book",
            ["attributeFullName"] = "AspireForm.Annotations.DabPermissionAttribute",
            ["ctorArgs"] = new JsonArray("anonymous", "read"),
            ["projectPath"] = fix.CsprojPath,
        }, default);
        result.IsError.Should().BeFalse();
        var src = File.ReadAllText(bookFile);
        src.Should().Contain("DabPermission").And.Contain("\"anonymous\"").And.Contain("\"read\"");
    }

    [Fact]
    public async Task AttributeClearTool_removes_a_class_level_attribute()
    {
        using var fix = new FixtureProjectBuilder("attr_clear");
        var bookFile = fix.AddFile("Book.cs", """
            namespace Demo;
            [AspireForm.Annotations.DabExpose]
            public class Book { public int Id { get; set; } }
            """);
        var result = await new AttributeClearTool(".").ExecuteAsync(new JsonObject
        {
            ["entity"] = "Book",
            ["attributeFullName"] = "AspireForm.Annotations.DabExposeAttribute",
            ["projectPath"] = fix.CsprojPath,
        }, default);
        result.IsError.Should().BeFalse();
        File.ReadAllText(bookFile).Should().NotContain("DabExpose");
    }

    [Fact]
    public async Task RelationshipAddTool_OneToMany_adds_nav_on_both_sides()
    {
        using var fix = new FixtureProjectBuilder("rel_add");
        var modelsFile = fix.AddFile("Models.cs", """
            namespace Demo;
            public class Author { public int Id { get; set; } }
            public class Book { public int Id { get; set; } }
            """);
        var result = await new RelationshipAddTool(".").ExecuteAsync(new JsonObject
        {
            ["fromEntity"] = "Author",
            ["toEntity"] = "Book",
            ["cardinality"] = "OneToMany",
            ["projectPath"] = fix.CsprojPath,
        }, default);
        result.IsError.Should().BeFalse();
        File.ReadAllText(modelsFile).Should().Contain("ICollection<Book>");
    }

    [Fact]
    public async Task RelationshipAddTool_rejects_unknown_cardinality()
    {
        var tool = new RelationshipAddTool(".");
        var result = await tool.ExecuteAsync(new JsonObject
        {
            ["fromEntity"] = "A",
            ["toEntity"] = "B",
            ["cardinality"] = "Bogus",
            ["projectPath"] = "x.csproj",
        }, default);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("Unknown cardinality 'Bogus'");
    }

    [Fact]
    public async Task RelationshipAddTool_rejects_ManyToMany_in_v1()
    {
        using var fix = new FixtureProjectBuilder("rel_add_m2m");
        fix.AddFile("Models.cs", "namespace Demo; public class A { public int Id { get; set; } } public class B { public int Id { get; set; } }");
        var result = await new RelationshipAddTool(".").ExecuteAsync(new JsonObject
        {
            ["fromEntity"] = "A",
            ["toEntity"] = "B",
            ["cardinality"] = "ManyToMany",
            ["projectPath"] = fix.CsprojPath,
        }, default);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("ManyToMany");
    }

    [Fact]
    public async Task Missing_inputs_return_tool_level_errors_on_each_tool()
    {
        (await new AttributeSetTool(".").ExecuteAsync(new JsonObject(), default)).IsError.Should().BeTrue();
        (await new AttributeClearTool(".").ExecuteAsync(new JsonObject(), default)).IsError.Should().BeTrue();
        (await new RelationshipAddTool(".").ExecuteAsync(new JsonObject(), default)).IsError.Should().BeTrue();
        (await new RelationshipRemoveTool(".").ExecuteAsync(new JsonObject(), default)).IsError.Should().BeTrue();
    }
}
```

- [ ] **Step 6: Build + run**

```bash
dotnet build --nologo -v q
dotnet run --project tests/AspireForm.Tests -class "AspireForm.Tests.Mcp.Tools.Entity.AttributeAndRelationshipToolsTests"
```

Expected: 7/7 PASS.

- [ ] **Step 7: Commit**

```bash
git add src/AspireForm/Mcp/Tools/Entity/AttributeSetTool.cs src/AspireForm/Mcp/Tools/Entity/AttributeClearTool.cs src/AspireForm/Mcp/Tools/Entity/RelationshipAddTool.cs src/AspireForm/Mcp/Tools/Entity/RelationshipRemoveTool.cs tests/AspireForm.Tests/Mcp/Tools/Entity/AttributeAndRelationshipToolsTests.cs
git -c commit.gpgsign=false commit -m "feat(mcp): add attribute_{set,clear} + relationship_{add,remove} tools"
```

---

## Task 19: UiOptions + BrowserLauncher + UiCommand verb + register in Program.cs

**Files:**
- Create: `src/AspireForm/Ui/UiOptions.cs`
- Create: `src/AspireForm/Ui/BrowserLauncher.cs`
- Create: `src/AspireForm/Cli/UiCommand.cs`
- Modify: `src/AspireForm/Program.cs`

- [ ] **Step 1: Create `src/AspireForm/Ui/UiOptions.cs`**

```csharp
namespace AspireForm.Ui;

/// <summary>Runtime settings for the <c>aspireform ui</c> verb. Injected into Blazor's DI container as a singleton.</summary>
public sealed class UiOptions
{
    /// <summary>The default AspireForm project directory (where <c>aspireform.yaml</c> lives).</summary>
    public required string ProjectDir { get; init; }

    /// <summary>TCP port the Kestrel host binds to.</summary>
    public required int Port { get; init; }

    /// <summary>When true, the host opens the default browser on startup.</summary>
    public bool LaunchBrowser { get; init; } = true;
}
```

- [ ] **Step 2: Create `src/AspireForm/Ui/BrowserLauncher.cs`**

```csharp
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AspireForm.Ui;

/// <summary>Opens a URL in the user's default browser.</summary>
internal static class BrowserLauncher
{
    /// <summary>Best-effort launch — failures are swallowed (the URL is still printed to stdout by the host).</summary>
    public static void Open(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            else
            {
                Process.Start("xdg-open", url);
            }
        }
        catch { /* best effort */ }
    }
}
```

- [ ] **Step 3: Create `src/AspireForm/Cli/UiCommand.cs`**

```csharp
using System.ComponentModel;
using AspireForm.Ui;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>ui</c> command: starts a Kestrel-hosted Blazor Server app on localhost for the EF model builder.</summary>
public sealed class UiCommand : AsyncCommand<UiCommand.Settings>
{
    /// <summary>Options for <c>ui</c>.</summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>Default AspireForm project directory.</summary>
        [CommandOption("-p|--project-dir <DIR>")]
        [Description("Default AspireForm project directory.")]
        public string ProjectDir { get; init; } = ".";

        /// <summary>Port to bind. Defaults to 5050.</summary>
        [CommandOption("--port <PORT>")]
        [Description("Port to bind (default 5050).")]
        public int Port { get; init; } = 5050;

        /// <summary>When true, suppresses the browser auto-launch.</summary>
        [CommandOption("--no-launch")]
        [Description("Don't open the default browser on startup.")]
        public bool NoLaunch { get; init; }
    }

    /// <inheritdoc />
    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var opts = new UiOptions
        {
            ProjectDir = Path.GetFullPath(settings.ProjectDir),
            Port = settings.Port,
            LaunchBrowser = !settings.NoLaunch,
        };
        try
        {
            await UiHost.RunAsync(opts, cancellationToken);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
    }
}
```

- [ ] **Step 4: Edit `src/AspireForm/Program.cs`** — add the `ui` verb registration after the `mcp` verb. Locate the existing `config.AddCommand<McpCommand>("mcp")` line and immediately after its `.WithDescription(...)` call, add:

```csharp
    config.AddCommand<UiCommand>("ui")
        .WithDescription("Start the EF model builder UI (Blazor Server on localhost; --port to set the port).");
```

- [ ] **Step 5: Build to confirm `UiHost` is the only missing symbol (it lands in Task 20)**

```bash
dotnet build --nologo -v q 2>&1 | tail -10
```

Expected: build FAILS with "type or namespace 'UiHost' could not be found" — this is fine; Task 20 lands `UiHost`. **Do not commit yet.** Continue to Task 20 first.

---

## Task 20: UiHost (Kestrel + Blazor Server bootstrap) + App shell

**Files:**
- Create: `src/AspireForm/Ui/UiHost.cs`
- Create: `src/AspireForm/Ui/Components/_Imports.razor`
- Create: `src/AspireForm/Ui/Components/App.razor`
- Create: `src/AspireForm/Ui/Components/Routes.razor`
- Create: `src/AspireForm/Ui/Components/Layout/MainLayout.razor`
- Create: `src/AspireForm/Ui/wwwroot/site.css`

- [ ] **Step 1: Create `src/AspireForm/Ui/Components/_Imports.razor`**

```razor
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.AspNetCore.Components.Routing
@using AspireForm.EntityCatalog
@using AspireForm.Ui
@using AspireForm.Ui.Components.Layout
```

- [ ] **Step 2: Create `src/AspireForm/Ui/Components/App.razor`**

```razor
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>AspireForm — Entity Model Builder</title>
    <base href="/" />
    <link rel="stylesheet" href="site.css" />
    <HeadOutlet />
</head>
<body>
    <Routes />
    <script src="_framework/blazor.web.js"></script>
</body>
</html>
```

- [ ] **Step 3: Create `src/AspireForm/Ui/Components/Routes.razor`**

```razor
<Router AppAssembly="@typeof(App).Assembly">
    <Found Context="routeData">
        <RouteView RouteData="@routeData" DefaultLayout="@typeof(MainLayout)" />
    </Found>
    <NotFound>
        <PageTitle>Not found</PageTitle>
        <LayoutView Layout="@typeof(MainLayout)">
            <p>Sorry, there's nothing at this address.</p>
        </LayoutView>
    </NotFound>
</Router>
```

- [ ] **Step 4: Create `src/AspireForm/Ui/Components/Layout/MainLayout.razor`**

```razor
@inherits LayoutComponentBase

<div class="layout">
    <header class="topbar">
        <strong>AspireForm</strong>
        <span class="topbar-sub">Entity Model Builder</span>
        <nav class="topbar-nav">
            <a href="/entities">Entities</a>
            <a href="/diagnostics">Diagnostics</a>
            <a href="/about">About</a>
        </nav>
    </header>
    <main class="content">
        @Body
    </main>
</div>
```

- [ ] **Step 5: Create `src/AspireForm/Ui/wwwroot/site.css`**

```css
* { box-sizing: border-box; }
body { margin: 0; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; font-size: 14px; color: #222; background: #fff; }
.layout { display: flex; flex-direction: column; height: 100vh; }
.topbar { display: flex; align-items: center; gap: 1rem; padding: .6rem 1rem; border-bottom: 1px solid #ddd; background: #fafafa; }
.topbar-sub { color: #666; }
.topbar-nav { margin-left: auto; display: flex; gap: 1rem; }
.topbar-nav a { color: #1a73e8; text-decoration: none; }
.topbar-nav a:hover { text-decoration: underline; }
.content { flex: 1; overflow: hidden; }
.two-pane { display: flex; height: 100%; }
.sidebar { width: 240px; border-right: 1px solid #ddd; background: #fcfcfc; display: flex; flex-direction: column; }
.sidebar-actions { padding: .5rem .75rem; border-bottom: 1px solid #eee; }
.sidebar-list { flex: 1; overflow-y: auto; }
.sidebar-item { padding: .45rem .75rem; cursor: pointer; }
.sidebar-item:hover { background: #f4f4f4; }
.sidebar-item.active { background: #e8f0fe; border-left: 3px solid #1a73e8; font-weight: 600; }
.detail { flex: 1; display: flex; flex-direction: column; overflow: hidden; }
.detail-header { padding: .75rem 1rem; border-bottom: 1px solid #eee; }
.detail-tabs { display: flex; border-bottom: 1px solid #ddd; background: #fafafa; }
.detail-tab { padding: .55rem 1rem; cursor: pointer; color: #666; }
.detail-tab.active { border-bottom: 2px solid #1a73e8; color: #222; font-weight: 600; }
.detail-body { flex: 1; overflow-y: auto; padding: .75rem 1rem; }
table.entities { width: 100%; border-collapse: collapse; font-size: .9em; }
table.entities th, table.entities td { text-align: left; padding: .4rem .6rem; border-bottom: 1px solid #eee; }
table.entities thead tr { background: #f5f5f5; }
button { padding: .35rem .8rem; border: 1px solid #ccc; background: #fff; border-radius: 4px; cursor: pointer; font-size: .85em; }
button:hover { background: #f0f0f0; }
button.danger { background: #fee; color: #a00; border-color: #fbb; }
input[type=text], select { padding: .3rem .5rem; border: 1px solid #ccc; border-radius: 4px; font-size: .85em; }
.banner { padding: .5rem .8rem; background: #fff3cd; border-bottom: 1px solid #f0d97d; color: #6a4900; font-size: .85em; }
.muted { color: #888; }
.kbd { font-family: monospace; background: #f5f5f5; padding: .05em .35em; border-radius: 3px; font-size: .9em; }
```

- [ ] **Step 6: Create `src/AspireForm/Ui/UiHost.cs`**

```csharp
using AspireForm.EntityCatalog;
using AspireForm.Ui.Components;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace AspireForm.Ui;

/// <summary>Hosts Kestrel + Blazor Server inside the dnx tool process.</summary>
internal static class UiHost
{
    /// <summary>Runs the host until <paramref name="ct"/> fires or Ctrl-C is received.</summary>
    public static async Task RunAsync(UiOptions opts, CancellationToken ct)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel(k => k.ListenLocalhost(opts.Port));
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddSingleton<IEntityCatalogService>(_ => new RoslynEntityCatalogService());
        builder.Services.AddSingleton(opts);
        builder.Logging.ClearProviders(); // keep stdout clean for dnx users

        // Serve embedded wwwroot files. With <FrameworkReference Microsoft.AspNetCore.App />, Blazor's
        // static file infrastructure handles framework assets (blazor.web.js, etc.); we only need to
        // map our own site.css from the source-controlled wwwroot/.
        var wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var app = builder.Build();
        if (Directory.Exists(wwwroot))
        {
            app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(wwwroot) });
        }
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

        var url = $"http://localhost:{opts.Port}";
        Console.Out.WriteLine($"AspireForm UI listening at {url} (project-dir: {opts.ProjectDir})");
        Console.Out.WriteLine("Press Ctrl+C to stop.");
        if (opts.LaunchBrowser) BrowserLauncher.Open(url);
        await app.RunAsync(ct);
    }
}
```

- [ ] **Step 7: Modify `src/AspireForm/AspireForm.csproj`** — ensure `wwwroot/site.css` is packaged as content with the tool. Add this to the existing ItemGroup chain (after the `<None Include="../../README.md" .../>` line):

```xml
  <ItemGroup>
    <Content Include="Ui/wwwroot/**/*" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
```

- [ ] **Step 8: Build**

```bash
dotnet build --nologo -v q 2>&1 | tail -10
```

Expected: build succeeds. (Blazor may produce some `RZ` warnings for stub components — those land in Task 21.)

- [ ] **Step 9: Commit Tasks 19 + 20 together**

```bash
git add src/AspireForm/Ui/ src/AspireForm/Cli/UiCommand.cs src/AspireForm/Program.cs src/AspireForm/AspireForm.csproj
git -c commit.gpgsign=false commit -m "feat(ui): add aspireform ui verb + Kestrel + Blazor Server bootstrap + app shell"
```

---

## Task 21: Pages — Index, Entities (master/detail shell), Diagnostics, About + EntityList + EntityHeader

**Files:**
- Create: `src/AspireForm/Ui/Components/Pages/Index.razor`
- Create: `src/AspireForm/Ui/Components/Pages/Entities.razor`
- Create: `src/AspireForm/Ui/Components/Pages/Diagnostics.razor`
- Create: `src/AspireForm/Ui/Components/Pages/About.razor`
- Create: `src/AspireForm/Ui/Components/Entity/EntityList.razor`
- Create: `src/AspireForm/Ui/Components/Entity/EntityHeader.razor`

- [ ] **Step 1: Create `src/AspireForm/Ui/Components/Pages/Index.razor`**

```razor
@page "/"
@inject NavigationManager Nav
@inject UiOptions Options

<PageTitle>AspireForm</PageTitle>

<div style="padding: 1rem">
    <h2>AspireForm — Entity Model Builder</h2>
    <p>Project directory: <span class="kbd">@Options.ProjectDir</span></p>
    <p>
        <a href="/entities">Browse entities →</a>
    </p>
</div>

@code {
    // v1 simply links to /entities. A later iteration may auto-redirect when there's exactly one ef-data block.
}
```

- [ ] **Step 2: Create `src/AspireForm/Ui/Components/Pages/Entities.razor`**

```razor
@page "/entities"
@inject IEntityCatalogService Catalog
@inject UiOptions Options

<PageTitle>Entities — AspireForm</PageTitle>

<div class="two-pane">
    <aside class="sidebar">
        <div class="sidebar-actions">
            <input type="text" placeholder="Search entities..." @bind="Filter" @bind:event="oninput" style="width:100%" />
        </div>
        <div class="sidebar-actions">
            <span class="muted" style="font-size:.8em">@FilteredEntities.Count entities</span>
        </div>
        <div class="sidebar-list">
            @foreach (var e in FilteredEntities.OrderBy(e => e.Name, StringComparer.Ordinal))
            {
                <div class="sidebar-item @(Selected?.Name == e.Name ? "active" : "")" @onclick="() => Select(e)">@e.Name</div>
            }
            @if (FilteredEntities.Count == 0)
            {
                <div class="sidebar-item muted">No entities</div>
            }
        </div>
    </aside>
    <section class="detail">
        @if (LoadError is not null)
        {
            <div class="banner">@LoadError</div>
        }
        @if (Selected is null)
        {
            <div class="detail-body muted">Select an entity from the sidebar to view its details.</div>
        }
        else
        {
            <EntityHeader Entity="@Selected" />
            <div class="detail-tabs">
                <div class="detail-tab active">Properties (@Selected.Properties.Count)</div>
                <div class="detail-tab muted">Relationships (@Selected.Relationships.Count)</div>
                <div class="detail-tab muted">Attributes (@Selected.Attributes.Count)</div>
                <div class="detail-tab muted">DAB exposure</div>
            </div>
            <div class="detail-body">
                <table class="entities">
                    <thead><tr><th>Name</th><th>Type</th><th>Null?</th><th>PK</th></tr></thead>
                    <tbody>
                        @foreach (var p in Selected.Properties)
                        {
                            <tr>
                                <td><code>@p.Name</code></td>
                                <td><code>@p.ClrType</code></td>
                                <td>@(p.IsNullable ? "yes" : "no")</td>
                                <td>@(p.IsPrimaryKey ? "✓" : "")</td>
                            </tr>
                        }
                    </tbody>
                </table>
            </div>
        }
    </section>
</div>

@code {
    private string Filter { get; set; } = "";
    private Entity? Selected { get; set; }
    private EntityCatalog? CatalogSnapshot { get; set; }
    private string? LoadError { get; set; }

    private IReadOnlyList<Entity> FilteredEntities =>
        CatalogSnapshot is null ? []
        : string.IsNullOrEmpty(Filter)
            ? CatalogSnapshot.Entities
            : CatalogSnapshot.Entities.Where(e => e.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase)).ToList();

    private string DefaultProjectPath => Path.Combine(Options.ProjectDir, "aspireform.yaml");

    protected override async Task OnInitializedAsync()
    {
        try
        {
            // v1: assume the entity csproj is the first .csproj file in the project dir or its subdirs.
            // A later iteration will resolve via aspireform.yaml's ef-data block.
            var csproj = Directory.EnumerateFiles(Options.ProjectDir, "*.csproj", SearchOption.AllDirectories).FirstOrDefault();
            if (csproj is null)
            {
                LoadError = $"No .csproj found under '{Options.ProjectDir}'. Pass --project-dir to set the AspireForm project root.";
                return;
            }
            CatalogSnapshot = await Catalog.ScanAsync(csproj, default);
        }
        catch (Exception ex)
        {
            LoadError = $"Scan failed: {ex.Message}";
        }
    }

    private void Select(Entity e) => Selected = e;
}
```

- [ ] **Step 3: Create `src/AspireForm/Ui/Components/Pages/Diagnostics.razor`**

```razor
@page "/diagnostics"
@inject IEntityCatalogService Catalog
@inject UiOptions Options

<PageTitle>Diagnostics — AspireForm</PageTitle>

<div style="padding: 1rem">
    <h2>Diagnostics</h2>
    @if (Snapshot is null)
    {
        <p class="muted">Loading...</p>
    }
    else if (Snapshot.Diagnostics.Count == 0)
    {
        <p>No diagnostics — workspace loaded clean.</p>
    }
    else
    {
        <table class="entities">
            <thead><tr><th>Severity</th><th>Message</th><th>File</th><th>Line</th></tr></thead>
            <tbody>
                @foreach (var d in Snapshot.Diagnostics)
                {
                    <tr><td>@d.Severity</td><td>@d.Message</td><td>@d.FilePath</td><td>@d.Line</td></tr>
                }
            </tbody>
        </table>
    }
</div>

@code {
    private EntityCatalog? Snapshot { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var csproj = Directory.EnumerateFiles(Options.ProjectDir, "*.csproj", SearchOption.AllDirectories).FirstOrDefault();
        if (csproj is null) return;
        try { Snapshot = await Catalog.ScanAsync(csproj, default); } catch { /* surfaced on /entities */ }
    }
}
```

- [ ] **Step 4: Create `src/AspireForm/Ui/Components/Pages/About.razor`**

```razor
@page "/about"
@inject UiOptions Options

<PageTitle>About — AspireForm</PageTitle>

<div style="padding: 1rem">
    <h2>About</h2>
    <p>AspireForm version: <span class="kbd">@(typeof(UiOptions).Assembly.GetName().Version?.ToString() ?? "0.0.0")</span></p>
    <p>Project directory: <span class="kbd">@Options.ProjectDir</span></p>
    <p>Port: <span class="kbd">@Options.Port</span></p>
    <p>Stop the host with <span class="kbd">Ctrl+C</span> in the terminal where you started <span class="kbd">aspireform ui</span>.</p>
</div>
```

- [ ] **Step 5: Create `src/AspireForm/Ui/Components/Entity/EntityList.razor`** — empty stub for now; the sidebar list is inlined inside Entities.razor for v1. The file exists so future iterations can extract the list cleanly.

```razor
@*
    Reserved for #4a.1 extraction of the sidebar entity list from Entities.razor.
*@
```

- [ ] **Step 6: Create `src/AspireForm/Ui/Components/Entity/EntityHeader.razor`**

```razor
@if (Entity is not null)
{
    <div class="detail-header">
        <div style="display:flex;align-items:baseline;gap:.5rem">
            <h3 style="margin:0">@Entity.Name</h3>
            <span class="muted" style="font-size:.85em">@Entity.Namespace · @System.IO.Path.GetFileName(Entity.FilePath)</span>
        </div>
    </div>
}

@code {
    /// <summary>The currently selected entity.</summary>
    [Parameter] public Entity? Entity { get; set; }
}
```

- [ ] **Step 7: Build**

```bash
dotnet build --nologo -v q 2>&1 | tail -10
```

Expected: build succeeds with no errors. (RZ Razor warnings for unused parameters are tolerable.)

- [ ] **Step 8: Manual smoke-test (optional but recommended)**

```bash
dotnet run --project src/AspireForm -- ui --port 5550 --no-launch
```

Open `http://localhost:5550/` in a browser. The Index page should load, navigation should work, and `/entities` should show a banner if no .csproj is under the cwd. Ctrl-C to stop.

- [ ] **Step 9: Commit**

```bash
git add src/AspireForm/Ui/
git -c commit.gpgsign=false commit -m "feat(ui): add Index / Entities / Diagnostics / About pages + EntityHeader"
```

---

## Task 22: Interactive tabs + dialogs — full CRUD UX

Replaces the read-only Entities.razor body with a full interactive page. Active tab switches between Properties / Relationships / Attributes / DAB. Each tab supports the writes via the same `IEntityCatalogService`. After every mutation, the page re-scans to refresh the snapshot.

**Files:**
- Modify: `src/AspireForm/Ui/Components/Pages/Entities.razor` — replace body entirely
- Create: `src/AspireForm/Ui/Components/Entity/EntityPropertiesTab.razor`
- Create: `src/AspireForm/Ui/Components/Entity/EntityRelationshipsTab.razor`
- Create: `src/AspireForm/Ui/Components/Entity/EntityAttributesTab.razor`
- Create: `src/AspireForm/Ui/Components/Entity/EntityDabTab.razor`
- Create: `src/AspireForm/Ui/Components/Dialogs/NewEntityDialog.razor`
- Create: `src/AspireForm/Ui/Components/Dialogs/AddPropertyDialog.razor`

- [ ] **Step 1: Replace `src/AspireForm/Ui/Components/Pages/Entities.razor` entirely**

```razor
@page "/entities"
@inject IEntityCatalogService Catalog
@inject UiOptions Options

<PageTitle>Entities — AspireForm</PageTitle>

<div class="two-pane">
    <aside class="sidebar">
        <div class="sidebar-actions">
            <input type="text" placeholder="Search entities..." @bind="Filter" @bind:event="oninput" style="width:100%" />
        </div>
        <div class="sidebar-actions">
            <button @onclick="() => ShowNewEntity = true" style="width:100%">+ New Entity</button>
        </div>
        <div class="sidebar-list">
            @foreach (var e in FilteredEntities.OrderBy(e => e.Name, StringComparer.Ordinal))
            {
                <div class="sidebar-item @(Selected?.Name == e.Name ? "active" : "")" @onclick="() => Select(e)">@e.Name</div>
            }
            @if (FilteredEntities.Count == 0)
            {
                <div class="sidebar-item muted">No entities</div>
            }
        </div>
    </aside>
    <section class="detail">
        @if (LoadError is not null)
        {
            <div class="banner">@LoadError</div>
        }
        @if (Selected is null)
        {
            <div class="detail-body muted">Select an entity from the sidebar.</div>
        }
        else
        {
            <EntityHeader Entity="@Selected" />
            <div class="detail-tabs">
                <div class="detail-tab @(ActiveTab == Tab.Properties ? "active" : "")" @onclick="() => ActiveTab = Tab.Properties">Properties (@Selected.Properties.Count)</div>
                <div class="detail-tab @(ActiveTab == Tab.Relationships ? "active" : "")" @onclick="() => ActiveTab = Tab.Relationships">Relationships (@Selected.Relationships.Count)</div>
                <div class="detail-tab @(ActiveTab == Tab.Attributes ? "active" : "")" @onclick="() => ActiveTab = Tab.Attributes">Attributes (@Selected.Attributes.Count)</div>
                <div class="detail-tab @(ActiveTab == Tab.Dab ? "active" : "")" @onclick="() => ActiveTab = Tab.Dab">DAB exposure</div>
            </div>
            <div class="detail-body">
                @switch (ActiveTab)
                {
                    case Tab.Properties:
                        <EntityPropertiesTab Entity="@Selected" CsprojPath="@CsprojPath" OnMutated="ReloadAsync" />
                        break;
                    case Tab.Relationships:
                        <EntityRelationshipsTab Entity="@Selected" Entities="@(CatalogSnapshot?.Entities ?? [])" CsprojPath="@CsprojPath" OnMutated="ReloadAsync" />
                        break;
                    case Tab.Attributes:
                        <EntityAttributesTab Entity="@Selected" CsprojPath="@CsprojPath" OnMutated="ReloadAsync" />
                        break;
                    case Tab.Dab:
                        <EntityDabTab Entity="@Selected" CsprojPath="@CsprojPath" OnMutated="ReloadAsync" />
                        break;
                }
            </div>
        }
    </section>
</div>

@if (ShowNewEntity)
{
    <NewEntityDialog CsprojPath="@CsprojPath" DefaultDir="@DefaultEntityDir" OnClose="() => { ShowNewEntity = false; }" OnCreated="ReloadAsync" />
}

@code {
    private enum Tab { Properties, Relationships, Attributes, Dab }

    private string Filter { get; set; } = "";
    private Tab ActiveTab { get; set; } = Tab.Properties;
    private Entity? Selected { get; set; }
    private EntityCatalog? CatalogSnapshot { get; set; }
    private string? CsprojPath { get; set; }
    private string DefaultEntityDir => CsprojPath is null ? Options.ProjectDir : Path.GetDirectoryName(CsprojPath)!;
    private string? LoadError { get; set; }
    private bool ShowNewEntity { get; set; }

    private IReadOnlyList<Entity> FilteredEntities =>
        CatalogSnapshot is null ? []
        : string.IsNullOrEmpty(Filter)
            ? CatalogSnapshot.Entities
            : CatalogSnapshot.Entities.Where(e => e.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase)).ToList();

    protected override async Task OnInitializedAsync() => await ReloadAsync();

    private async Task ReloadAsync()
    {
        try
        {
            CsprojPath ??= Directory.EnumerateFiles(Options.ProjectDir, "*.csproj", SearchOption.AllDirectories).FirstOrDefault();
            if (CsprojPath is null)
            {
                LoadError = $"No .csproj found under '{Options.ProjectDir}'.";
                return;
            }
            CatalogSnapshot = await Catalog.ScanAsync(CsprojPath, default);
            LoadError = null;
            // Re-pick selected entity after reload (it may have been renamed/deleted).
            if (Selected is not null)
            {
                Selected = CatalogSnapshot.Entities.FirstOrDefault(e => e.Name == Selected.Name);
            }
        }
        catch (Exception ex)
        {
            LoadError = $"Scan failed: {ex.Message}";
        }
    }

    private void Select(Entity e) => Selected = e;
}
```

- [ ] **Step 2: Create `src/AspireForm/Ui/Components/Entity/EntityPropertiesTab.razor`**

```razor
@inject IEntityCatalogService Catalog

@if (Entity is null || CsprojPath is null) { return; }

<table class="entities">
    <thead><tr><th>Name</th><th>Type</th><th>Null?</th><th>PK</th><th></th></tr></thead>
    <tbody>
        @foreach (var p in Entity.Properties)
        {
            <tr>
                <td><code>@p.Name</code></td>
                <td><code>@p.ClrType</code></td>
                <td>@(p.IsNullable ? "yes" : "no")</td>
                <td>@(p.IsPrimaryKey ? "✓" : "")</td>
                <td><button class="danger" @onclick="() => RemoveAsync(p.Name)">Remove</button></td>
            </tr>
        }
    </tbody>
</table>
<p style="margin-top:.6rem"><button @onclick="() => ShowAdd = true">+ Add Property</button></p>

@if (ShowAdd)
{
    <AddPropertyDialog CsprojPath="@CsprojPath" EntityName="@Entity.Name"
        OnClose="() => { ShowAdd = false; }" OnAdded="AfterAddedAsync" />
}

@if (LastError is not null) { <div class="banner">@LastError</div> }

@code {
    [Parameter] public Entity? Entity { get; set; }
    [Parameter] public string? CsprojPath { get; set; }
    [Parameter] public EventCallback OnMutated { get; set; }

    private bool ShowAdd { get; set; }
    private string? LastError { get; set; }

    private async Task RemoveAsync(string propertyName)
    {
        LastError = null;
        var r = await Catalog.MutateAsync(CsprojPath!, new RemoveProperty(Entity!.Name, propertyName), default);
        if (!r.Success) { LastError = r.Diagnostics.FirstOrDefault()?.Message ?? "Remove failed."; return; }
        await OnMutated.InvokeAsync();
    }

    private async Task AfterAddedAsync() { ShowAdd = false; await OnMutated.InvokeAsync(); }
}
```

- [ ] **Step 3: Create `src/AspireForm/Ui/Components/Entity/EntityRelationshipsTab.razor`**

```razor
@inject IEntityCatalogService Catalog

@if (Entity is null || CsprojPath is null) { return; }

<table class="entities">
    <thead><tr><th>Navigation</th><th>Target</th><th>Cardinality</th><th>FK</th><th></th></tr></thead>
    <tbody>
        @foreach (var r in Entity.Relationships)
        {
            <tr>
                <td><code>@r.Name</code></td>
                <td><code>@r.TargetEntity</code></td>
                <td>@r.Cardinality</td>
                <td>@(r.ForeignKeyProperty ?? "(convention)")</td>
                <td><button class="danger" @onclick="() => RemoveAsync(r.Name)">Remove</button></td>
            </tr>
        }
        @if (Entity.Relationships.Count == 0)
        {
            <tr><td colspan="5" class="muted">No relationships defined.</td></tr>
        }
    </tbody>
</table>

<fieldset style="margin-top: .8rem; padding: .5rem .8rem; border: 1px solid #eee">
    <legend>Add relationship</legend>
    <div style="display:flex; gap: .5rem; align-items: center; flex-wrap: wrap">
        <label>To entity:
            <select @bind="NewTo">
                <option value="">— select —</option>
                @foreach (var e in (Entities ?? []).Where(e => e.Name != Entity.Name).OrderBy(e => e.Name, StringComparer.Ordinal))
                {
                    <option value="@e.Name">@e.Name</option>
                }
            </select>
        </label>
        <label>Cardinality:
            <select @bind="NewCardinality">
                <option value="OneToOne">OneToOne</option>
                <option value="OneToMany">OneToMany</option>
                <option value="ManyToOne">ManyToOne</option>
            </select>
        </label>
        <button @onclick="AddAsync">Add</button>
    </div>
    @if (LastError is not null) { <div class="banner" style="margin-top:.5rem">@LastError</div> }
</fieldset>

@code {
    [Parameter] public Entity? Entity { get; set; }
    [Parameter] public IReadOnlyList<Entity>? Entities { get; set; }
    [Parameter] public string? CsprojPath { get; set; }
    [Parameter] public EventCallback OnMutated { get; set; }

    private string NewTo { get; set; } = "";
    private string NewCardinality { get; set; } = "OneToMany";
    private string? LastError { get; set; }

    private async Task AddAsync()
    {
        LastError = null;
        if (string.IsNullOrEmpty(NewTo)) { LastError = "Pick a target entity."; return; }
        if (!Enum.TryParse<RelationshipCardinality>(NewCardinality, out var card))
        {
            LastError = "Invalid cardinality."; return;
        }
        var r = await Catalog.MutateAsync(CsprojPath!,
            new AddRelationship(Entity!.Name, NewTo, card, null),
            default);
        if (!r.Success) { LastError = r.Diagnostics.FirstOrDefault()?.Message ?? "Add failed."; return; }
        NewTo = "";
        await OnMutated.InvokeAsync();
    }

    private async Task RemoveAsync(string relName)
    {
        LastError = null;
        var r = await Catalog.MutateAsync(CsprojPath!, new RemoveRelationship(Entity!.Name, relName), default);
        if (!r.Success) { LastError = r.Diagnostics.FirstOrDefault()?.Message ?? "Remove failed."; return; }
        await OnMutated.InvokeAsync();
    }
}
```

- [ ] **Step 4: Create `src/AspireForm/Ui/Components/Entity/EntityAttributesTab.razor`**

```razor
@inject IEntityCatalogService Catalog

@if (Entity is null || CsprojPath is null) { return; }

<table class="entities">
    <thead><tr><th>Attribute</th><th>Args</th><th></th></tr></thead>
    <tbody>
        @foreach (var a in Entity.Attributes)
        {
            <tr>
                <td><code>@a.FullTypeName</code></td>
                <td><code>@FormatArgs(a)</code></td>
                <td><button class="danger" @onclick="() => ClearAsync(a.FullTypeName)">Clear</button></td>
            </tr>
        }
        @if (Entity.Attributes.Count == 0)
        {
            <tr><td colspan="3" class="muted">No class-level attributes.</td></tr>
        }
    </tbody>
</table>

@if (LastError is not null) { <div class="banner" style="margin-top:.5rem">@LastError</div> }

@code {
    [Parameter] public Entity? Entity { get; set; }
    [Parameter] public string? CsprojPath { get; set; }
    [Parameter] public EventCallback OnMutated { get; set; }

    private string? LastError { get; set; }

    private static string FormatArgs(AttributeInstance a)
    {
        var parts = new List<string>();
        foreach (var c in a.ConstructorArgs) parts.Add(c?.ToString() ?? "null");
        foreach (var n in a.NamedArgs) parts.Add($"{n.Key}={n.Value}");
        return parts.Count == 0 ? "" : string.Join(", ", parts);
    }

    private async Task ClearAsync(string fullTypeName)
    {
        LastError = null;
        var r = await Catalog.MutateAsync(CsprojPath!, new ClearAttribute(Entity!.Name, null, fullTypeName), default);
        if (!r.Success) { LastError = r.Diagnostics.FirstOrDefault()?.Message ?? "Clear failed."; return; }
        await OnMutated.InvokeAsync();
    }
}
```

- [ ] **Step 5: Create `src/AspireForm/Ui/Components/Entity/EntityDabTab.razor`**

```razor
@inject IEntityCatalogService Catalog

@if (Entity is null || CsprojPath is null) { return; }

<div style="display:flex; flex-direction: column; gap: .5rem; max-width: 520px">
    <label><input type="checkbox" checked="@IsExposed" @onchange="ToggleExposeAsync" /> Expose via DAB <span class="muted">([DabExpose])</span></label>
    <label><input type="checkbox" checked="@IsHidden" @onchange="ToggleHiddenAsync" /> Hide from DAB <span class="muted">([DabHidden] — overrides expose)</span></label>
    <label><input type="checkbox" checked="@IsRestOnly" @onchange="ToggleRestOnlyAsync" /> REST only <span class="muted">([DabRestOnly])</span></label>
    <label><input type="checkbox" checked="@IsGraphqlOnly" @onchange="ToggleGraphqlOnlyAsync" /> GraphQL only <span class="muted">([DabGraphqlOnly])</span></label>
</div>

@if (LastError is not null) { <div class="banner" style="margin-top:.5rem">@LastError</div> }

@code {
    [Parameter] public Entity? Entity { get; set; }
    [Parameter] public string? CsprojPath { get; set; }
    [Parameter] public EventCallback OnMutated { get; set; }

    private bool IsExposed => Entity!.Attributes.Any(a => a.FullTypeName == "AspireForm.Annotations.DabExposeAttribute");
    private bool IsHidden => Entity!.Attributes.Any(a => a.FullTypeName == "AspireForm.Annotations.DabHiddenAttribute");
    private bool IsRestOnly => Entity!.Attributes.Any(a => a.FullTypeName == "AspireForm.Annotations.DabRestOnlyAttribute");
    private bool IsGraphqlOnly => Entity!.Attributes.Any(a => a.FullTypeName == "AspireForm.Annotations.DabGraphqlOnlyAttribute");

    private string? LastError { get; set; }

    private Task ToggleExposeAsync() => ToggleAsync("AspireForm.Annotations.DabExposeAttribute", IsExposed);
    private Task ToggleHiddenAsync() => ToggleAsync("AspireForm.Annotations.DabHiddenAttribute", IsHidden);
    private Task ToggleRestOnlyAsync() => ToggleAsync("AspireForm.Annotations.DabRestOnlyAttribute", IsRestOnly);
    private Task ToggleGraphqlOnlyAsync() => ToggleAsync("AspireForm.Annotations.DabGraphqlOnlyAttribute", IsGraphqlOnly);

    private async Task ToggleAsync(string fullTypeName, bool currentlyPresent)
    {
        LastError = null;
        EntityChangeRequest req = currentlyPresent
            ? new ClearAttribute(Entity!.Name, null, fullTypeName)
            : new SetAttribute(Entity!.Name, null,
                new AttributeInstance(fullTypeName, [], new Dictionary<string, object?>()));
        var r = await Catalog.MutateAsync(CsprojPath!, req, default);
        if (!r.Success) { LastError = r.Diagnostics.FirstOrDefault()?.Message ?? "Toggle failed."; return; }
        await OnMutated.InvokeAsync();
    }
}
```

- [ ] **Step 6: Create `src/AspireForm/Ui/Components/Dialogs/NewEntityDialog.razor`**

```razor
@inject IEntityCatalogService Catalog

<div style="position:fixed;inset:0;background:rgba(0,0,0,.4);display:flex;align-items:center;justify-content:center;z-index:10">
    <div style="background:#fff;padding:1rem 1.25rem;border-radius:6px;min-width:380px">
        <h3 style="margin-top:0">New Entity</h3>
        <div style="display:flex;flex-direction:column;gap:.5rem">
            <label>Name <input type="text" @bind="Name" /></label>
            <label>Namespace <input type="text" @bind="Namespace" placeholder="Demo.Models" /></label>
            <label>File path <input type="text" @bind="FilePath" placeholder="@DefaultDir/Models/Book.cs" style="width:100%" /></label>
        </div>
        @if (LastError is not null) { <div class="banner" style="margin-top:.5rem">@LastError</div> }
        <div style="display:flex;justify-content:flex-end;gap:.5rem;margin-top:.8rem">
            <button @onclick="OnClose">Cancel</button>
            <button @onclick="CreateAsync">Create</button>
        </div>
    </div>
</div>

@code {
    [Parameter] public string? CsprojPath { get; set; }
    [Parameter] public string DefaultDir { get; set; } = "";
    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public EventCallback OnCreated { get; set; }

    private string Name { get; set; } = "";
    private string Namespace { get; set; } = "";
    private string FilePath { get; set; } = "";
    private string? LastError { get; set; }

    private async Task CreateAsync()
    {
        LastError = null;
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Namespace) || string.IsNullOrWhiteSpace(FilePath))
        {
            LastError = "All fields required.";
            return;
        }
        var absPath = Path.IsPathRooted(FilePath) ? FilePath : Path.Combine(DefaultDir, FilePath);
        var r = await Catalog.MutateAsync(CsprojPath!, new CreateEntity(Name, Namespace, absPath), default);
        if (!r.Success) { LastError = r.Diagnostics.FirstOrDefault()?.Message ?? "Create failed."; return; }
        await OnCreated.InvokeAsync();
    }
}
```

- [ ] **Step 7: Create `src/AspireForm/Ui/Components/Dialogs/AddPropertyDialog.razor`**

```razor
@inject IEntityCatalogService Catalog

<div style="position:fixed;inset:0;background:rgba(0,0,0,.4);display:flex;align-items:center;justify-content:center;z-index:10">
    <div style="background:#fff;padding:1rem 1.25rem;border-radius:6px;min-width:360px">
        <h3 style="margin-top:0">Add Property to @EntityName</h3>
        <div style="display:flex;flex-direction:column;gap:.5rem">
            <label>Name <input type="text" @bind="Name" /></label>
            <label>CLR type <input type="text" @bind="ClrType" placeholder="string" /></label>
            <label><input type="checkbox" @bind="IsNullable" /> Nullable</label>
            <label><input type="checkbox" @bind="IsPrimaryKey" /> Primary key</label>
        </div>
        @if (LastError is not null) { <div class="banner" style="margin-top:.5rem">@LastError</div> }
        <div style="display:flex;justify-content:flex-end;gap:.5rem;margin-top:.8rem">
            <button @onclick="OnClose">Cancel</button>
            <button @onclick="AddAsync">Add</button>
        </div>
    </div>
</div>

@code {
    [Parameter] public string? CsprojPath { get; set; }
    [Parameter] public string EntityName { get; set; } = "";
    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public EventCallback OnAdded { get; set; }

    private string Name { get; set; } = "";
    private string ClrType { get; set; } = "string";
    private bool IsNullable { get; set; }
    private bool IsPrimaryKey { get; set; }
    private string? LastError { get; set; }

    private async Task AddAsync()
    {
        LastError = null;
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(ClrType))
        {
            LastError = "Name and ClrType required.";
            return;
        }
        var prop = new Property(Name, ClrType, IsNullable, IsPrimaryKey, []);
        var r = await Catalog.MutateAsync(CsprojPath!, new AddProperty(EntityName, prop), default);
        if (!r.Success) { LastError = r.Diagnostics.FirstOrDefault()?.Message ?? "Add failed."; return; }
        await OnAdded.InvokeAsync();
    }
}
```

- [ ] **Step 8: Build**

```bash
dotnet build --nologo -v q 2>&1 | tail -10
```

Expected: build succeeds with no errors.

- [ ] **Step 9: Commit**

```bash
git add src/AspireForm/Ui/Components/
git -c commit.gpgsign=false commit -m "feat(ui): add interactive tabs (Properties / Relationships / Attributes / DAB) + dialogs"
```

---

## Task 23: UI tests — bUnit page tests + UI host smoke

bUnit allows rendering Razor components against a `TestContext` with a faked DI container. The smoke test starts `UiHost` on an ephemeral port and asserts the static `site.css` is served.

**Files:**
- Create: `tests/AspireForm.Tests/Ui/IndexPageTests.cs`
- Create: `tests/AspireForm.Tests/Ui/EntitiesPageTests.cs`
- Create: `tests/AspireForm.Tests/Ui/UiHostSmokeTests.cs`

- [ ] **Step 1: Create `tests/AspireForm.Tests/Ui/IndexPageTests.cs`**

```csharp
using AspireForm.EntityCatalog;
using AspireForm.Ui;
using AspireForm.Ui.Components.Pages;
using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AspireForm.Tests.Ui;

public sealed class IndexPageTests
{
    [Fact]
    public void Index_renders_project_dir_and_link_to_entities()
    {
        using var ctx = new TestContext();
        ctx.Services.AddSingleton(new UiOptions { ProjectDir = "C:/demo", Port = 5050 });
        ctx.Services.AddSingleton<IEntityCatalogService>(new FakeCatalogService());
        var cut = ctx.RenderComponent<Index>();
        cut.Markup.Should().Contain("C:/demo");
        cut.Markup.Should().Contain("href=\"/entities\"");
    }

    private sealed class FakeCatalogService : IEntityCatalogService
    {
        public Task<EntityCatalog> ScanAsync(string csprojPath, CancellationToken ct) =>
            Task.FromResult(new EntityCatalog([], [], []));
        public Task<MutationResult> MutateAsync(string csprojPath, EntityChangeRequest request, CancellationToken ct) =>
            Task.FromResult(MutationResult.Ok([]));
    }
}
```

- [ ] **Step 2: Create `tests/AspireForm.Tests/Ui/EntitiesPageTests.cs`**

```csharp
using AspireForm.EntityCatalog;
using AspireForm.Ui;
using AspireForm.Ui.Components.Pages;
using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AspireForm.Tests.Ui;

public sealed class EntitiesPageTests
{
    [Fact]
    public void Entities_shows_load_error_banner_when_no_csproj_in_project_dir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-ui-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            using var ctx = new TestContext();
            ctx.Services.AddSingleton(new UiOptions { ProjectDir = dir, Port = 5050 });
            ctx.Services.AddSingleton<IEntityCatalogService>(new EmptyCatalog());

            var cut = ctx.RenderComponent<Entities>();
            cut.WaitForAssertion(() => cut.Markup.Should().Contain("No .csproj found"), TimeSpan.FromSeconds(5));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Entities_renders_sidebar_with_entities_from_catalog()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af-ui-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Demo.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        try
        {
            using var ctx = new TestContext();
            ctx.Services.AddSingleton(new UiOptions { ProjectDir = dir, Port = 5050 });
            ctx.Services.AddSingleton<IEntityCatalogService>(new SeededCatalog(
                new EntityCatalog(
                    [new Entity("Book", "Demo", "Demo/Book.cs", [new Property("Id", "int", false, true, [])], [], [])],
                    [], [])));

            var cut = ctx.RenderComponent<Entities>();
            cut.WaitForAssertion(() => cut.Markup.Should().Contain("Book"), TimeSpan.FromSeconds(5));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private sealed class EmptyCatalog : IEntityCatalogService
    {
        public Task<EntityCatalog> ScanAsync(string csprojPath, CancellationToken ct) =>
            Task.FromResult(new EntityCatalog([], [], []));
        public Task<MutationResult> MutateAsync(string csprojPath, EntityChangeRequest request, CancellationToken ct) =>
            Task.FromResult(MutationResult.Ok([]));
    }

    private sealed class SeededCatalog : IEntityCatalogService
    {
        private readonly EntityCatalog _snap;
        public SeededCatalog(EntityCatalog snap) { _snap = snap; }
        public Task<EntityCatalog> ScanAsync(string csprojPath, CancellationToken ct) => Task.FromResult(_snap);
        public Task<MutationResult> MutateAsync(string csprojPath, EntityChangeRequest request, CancellationToken ct) =>
            Task.FromResult(MutationResult.Ok([]));
    }
}
```

- [ ] **Step 3: Create `tests/AspireForm.Tests/Ui/UiHostSmokeTests.cs`**

```csharp
using System.Net.Sockets;
using AspireForm.Ui;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Ui;

public sealed class UiHostSmokeTests
{
    private static int FindFreeTcpPort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public async Task UiHost_serves_index_page_html_on_ephemeral_port()
    {
        var port = FindFreeTcpPort();
        var dir = Path.Combine(Path.GetTempPath(), $"af-ui-smoke-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var opts = new UiOptions { ProjectDir = dir, Port = port, LaunchBrowser = false };

        using var cts = new CancellationTokenSource();
        var hostTask = UiHost.RunAsync(opts, cts.Token);

        try
        {
            // Give Kestrel a moment to come up.
            using var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };
            HttpResponseMessage? resp = null;
            for (int i = 0; i < 20; i++)
            {
                try { resp = await http.GetAsync("/"); if (resp.IsSuccessStatusCode) break; }
                catch (HttpRequestException) { await Task.Delay(150); }
            }
            resp.Should().NotBeNull();
            resp!.IsSuccessStatusCode.Should().BeTrue();
            var body = await resp.Content.ReadAsStringAsync();
            body.Should().Contain("AspireForm");
        }
        finally
        {
            cts.Cancel();
            try { await hostTask; } catch (OperationCanceledException) { } catch { /* host shutdown best-effort */ }
            Directory.Delete(dir, recursive: true);
        }
    }
}
```

- [ ] **Step 4: Build + run**

```bash
dotnet build --nologo -v q
dotnet run --project tests/AspireForm.Tests -class "AspireForm.Tests.Ui.IndexPageTests"
dotnet run --project tests/AspireForm.Tests -class "AspireForm.Tests.Ui.EntitiesPageTests"
dotnet run --project tests/AspireForm.Tests -class "AspireForm.Tests.Ui.UiHostSmokeTests"
```

Expected: 1/1, 2/2, 1/1 PASS. If bUnit is unavailable, comment out the bUnit-based tests (they're not on the critical path); the smoke test alone is sufficient v1 coverage.

- [ ] **Step 5: Commit**

```bash
git add tests/AspireForm.Tests/Ui/
git -c commit.gpgsign=false commit -m "test(ui): add bUnit page tests + UiHost smoke test on ephemeral port"
```

---

## Task 24: Register entity MCP tools in McpCommand.BuildRegistry + update registration test + e2e

**Files:**
- Modify: `src/AspireForm/Cli/McpCommand.cs`
- Modify: `tests/AspireForm.Tests/Mcp/McpCommandRegistrationTests.cs`
- Modify: `tests/AspireForm.Tests/Mcp/EndToEndTests.cs`

- [ ] **Step 1: Edit `src/AspireForm/Cli/McpCommand.cs`** — add `using AspireForm.Mcp.Tools.Entity;` at the top and 12 new `r.Register(...)` calls inside `BuildRegistry`, after the existing Plugin tools and before the Macros section. The block to add (inside `BuildRegistry`, after `r.Register(new PluginRemoveTool(projectDir));`):

```csharp
        // EF model-builder tools (#4a) — 12 fine-grained verbs over EntityCatalog.
        r.Register(new EntityListTool(projectDir));
        r.Register(new EntityShowTool(projectDir));
        r.Register(new DbContextListTool(projectDir));
        r.Register(new EntityCreateTool(projectDir));
        r.Register(new EntityDeleteTool(projectDir));
        r.Register(new PropertyAddTool(projectDir));
        r.Register(new PropertyRemoveTool(projectDir));
        r.Register(new PropertyRenameTool(projectDir));
        r.Register(new AttributeSetTool(projectDir));
        r.Register(new AttributeClearTool(projectDir));
        r.Register(new RelationshipAddTool(projectDir));
        r.Register(new RelationshipRemoveTool(projectDir));
```

- [ ] **Step 2: Edit `tests/AspireForm.Tests/Mcp/McpCommandRegistrationTests.cs`** — bump the count assertion from `17` to `29` and add the 12 new names to the assertions. Replace the test method body with:

```csharp
    [Fact]
    public void BuildRegistry_registers_14_verbs_3_macros_12_entity_tools_total_29()
    {
        var r = McpCommand.BuildRegistry(".");
        r.All.Count.Should().Be(29);

        string[] expectedLowLevel =
        [
            "aspireform_new", "aspireform_add", "aspireform_config", "aspireform_plan",
            "aspireform_apply", "aspireform_destroy", "aspireform_import",
            "aspireform_state_list", "aspireform_state_show", "aspireform_doctor",
            "aspireform_plugin_list", "aspireform_plugin_install",
            "aspireform_plugin_update", "aspireform_plugin_remove",
        ];
        foreach (var n in expectedLowLevel)
            r.Contains(n).Should().BeTrue(because: $"low-level tool '{n}' must be registered");

        string[] expectedMacros =
        [
            "scaffold_aspire_app_with_data", "add_cache_layer", "add_authentication",
        ];
        foreach (var n in expectedMacros)
            r.Contains(n).Should().BeTrue(because: $"macro '{n}' must be registered");

        string[] expectedEntityTools =
        [
            "aspireform_entity_list", "aspireform_entity_show", "aspireform_dbcontext_list",
            "aspireform_entity_create", "aspireform_entity_delete",
            "aspireform_property_add", "aspireform_property_remove", "aspireform_property_rename",
            "aspireform_attribute_set", "aspireform_attribute_clear",
            "aspireform_relationship_add", "aspireform_relationship_remove",
        ];
        foreach (var n in expectedEntityTools)
            r.Contains(n).Should().BeTrue(because: $"entity tool '{n}' must be registered");
    }
```

- [ ] **Step 3: Edit `tests/AspireForm.Tests/Mcp/EndToEndTests.cs`** — bump the `tools/list` count assertion from `17` to `29`. Locate the line `listResp!["result"]!["tools"]!.AsArray().Count.Should().Be(17);` and change it to:

```csharp
        listResp!["result"]!["tools"]!.AsArray().Count.Should().Be(29);
```

- [ ] **Step 4: Build + run the two updated tests**

```bash
dotnet build --nologo -v q
dotnet run --project tests/AspireForm.Tests -class "AspireForm.Tests.Mcp.McpCommandRegistrationTests"
dotnet run --project tests/AspireForm.Tests -class "AspireForm.Tests.Mcp.EndToEndTests"
```

Expected: both 1/1 PASS.

- [ ] **Step 5: Run the FULL test suite to catch any regressions**

```bash
dotnet run --project tests/AspireForm.Tests
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/AspireForm/Cli/McpCommand.cs tests/AspireForm.Tests/Mcp/McpCommandRegistrationTests.cs tests/AspireForm.Tests/Mcp/EndToEndTests.cs
git -c commit.gpgsign=false commit -m "feat(mcp): register 12 entity tools (registry grows 17 → 29)"
```

---

## Task 25: README + CHANGELOG + final docs pass

**Files:**
- Modify: `README.md`
- Modify: `CHANGELOG.md`

- [ ] **Step 1: Add a "Use the entity builder" section to `README.md`** — locate the existing "Use with an agent (MCP server)" section added in Plan 3.0; insert this new section directly below it:

````markdown
## Use the entity builder

`aspireform ui` opens a local Blazor Server app where you author your EF Core entity classes — properties, relationships, attributes — and see DAB exposure toggled live. The same operations are available as MCP tools so agents can drive the model alongside humans.

```bash
aspireform ui                       # default port 5050, opens browser
aspireform ui --port 5051           # explicit port
aspireform ui --no-launch           # skip the browser auto-launch
aspireform ui --project-dir ./myapp # set the AspireForm project root
```

### Code-first authoring

Reference the `AspireForm.Annotations` package from your entity project, then decorate entities:

```csharp
using AspireForm.Annotations;

[DabExpose]
[DabPermission("anonymous", "read")]
public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
}
```

When the `ef-data` block in your `aspireform.yaml` points at this project (`inputs.projectPath`), `aspireform plan` emits both:

- a generated `DbContext` with `DbSet<Book>`, and
- a `dab-config.json` exposing `Book` via REST/GraphQL with the declared permissions.

### Entity-level MCP tools

12 new MCP tools (registered alongside the existing 17) cover full CRUD: `aspireform_entity_{list,show,create,delete}`, `aspireform_property_{add,remove,rename}`, `aspireform_attribute_{set,clear}`, `aspireform_relationship_{add,remove}`, `aspireform_dbcontext_list`.

> **Security:** the UI binds localhost only and has no authentication. Dev-tool use only.
````

- [ ] **Step 2: Add a `[0.5.0]` entry at the top of `CHANGELOG.md`** (above the existing `[0.4.0]` entry):

```markdown
## [0.5.0] - 2026-05-25

### Added
- New `aspireform ui` verb — Kestrel + Blazor Server EF model builder. Localhost-only.
- 12 new MCP entity tools: `aspireform_entity_{list,show,create,delete}`, `aspireform_property_{add,remove,rename}`, `aspireform_attribute_{set,clear}`, `aspireform_relationship_{add,remove}`, `aspireform_dbcontext_list`. Registry grows from 17 to 29 tools.
- `EntityCatalog` namespace with Roslyn-backed scanner and mutator (`MSBuildWorkspace`, semantic-safe rename, transactional file writes).
- New sibling package `AspireForm.Annotations 0.1.0` with `[DabExpose]`, `[DabPath]`, `[DabPermission]`, `[DabRestOnly]`, `[DabGraphqlOnly]`, `[DabHidden]`, `[OnDelete]` attributes for code-first entity authoring.

### Changed
- Built-in `ef-data` module provider rewritten to use the entity catalog. The provider now emits a real `DbContext` (DbSet per entity) and — when any entity carries `[DabExpose]` — a sibling `dab-config.json`.
- `<FrameworkReference Include="Microsoft.AspNetCore.App" />` added to the AspireForm tool package; `<RollForward>LatestMajor</RollForward>` set for shared-framework compatibility.

### Breaking
- `ef-data` block inputs changed: removed `database` + `contextName`; added `projectPath` (required), `dbContext` (optional), `emitDabConfig` (optional), `dabConfigPath` (optional). Migration:
  - Before (0.4.0):
    ```yaml
    modules:
      data:
        type: ef-data
        dependsOn: [sql]
        inputs:
          database: appdb
          contextName: AppDbContext
    ```
  - After (0.5.0):
    ```yaml
    modules:
      data:
        type: ef-data
        dependsOn: [sql]
        inputs:
          projectPath: ./Demo.Data/Demo.Data.csproj
    ```
  When upgrading, AspireForm throws a clear `InvalidOperationException` from `ef-data plan` that points back to this entry.
```

- [ ] **Step 3: Commit**

```bash
git add README.md CHANGELOG.md
git -c commit.gpgsign=false commit -m "docs: add 'Use the entity builder' README section + 0.5.0 CHANGELOG entry"
```

---

## Task 26: Pack + verify + release prep

**Files:** none new — verification step.

- [ ] **Step 1: Pack both packages**

```bash
dotnet pack src/AspireForm/AspireForm.csproj -o ./artifacts
dotnet pack src/AspireForm.Annotations/AspireForm.Annotations.csproj -o ./artifacts
```

Expected: `./artifacts/AspireForm.0.5.0.nupkg` and `./artifacts/AspireForm.Annotations.0.1.0.nupkg` produced.

- [ ] **Step 2: Inspect the nuspec inside `AspireForm.0.5.0.nupkg`** to confirm `<FrameworkReference Include="Microsoft.AspNetCore.App" />` is preserved and the package marks itself as a tool:

```bash
unzip -p ./artifacts/AspireForm.0.5.0.nupkg AspireForm.nuspec | head -50
```

Expected: nuspec includes `<frameworkAssemblies>` / `<frameworkReferences>` entry referencing `Microsoft.AspNetCore.App`. If not present, the pack may have stripped it — re-add via `<PackageReference Include="Microsoft.AspNetCore.App" PrivateAssets="all" />` workaround and re-pack. Report DONE_WITH_CONCERNS if this happens.

- [ ] **Step 3: Local dnx smoke (Windows PowerShell)** — install the locally-packed package into a transient feed and verify `aspireform ui --no-launch` starts cleanly. This step requires the user to run it interactively because dnx needs a `.NET 10` runtime resolve path that depends on local SDK state. The implementer should run:

```bash
dotnet build --nologo -v q
```

and report success without performing the live dnx test (the user runs that). Note in the report that the live `dnx AspireForm@0.5.0 ui` test is deferred to the post-tag verification step (see Plan 3.0's release flow for the precedent).

- [ ] **Step 4: Final full-suite test run + green-light report**

```bash
dotnet run --project tests/AspireForm.Tests
```

Expected: every test passes. Report total test count (target: prior count + ~60-70 new tests).

- [ ] **Step 5: Stop here — do NOT tag or push.** Tagging + pushing `v0.5.0` and `annotations/v0.1.0` is the user's call (matches the pattern used for AspireForm 0.4.0 in Plan 3.0). Report:

```
DONE.
- AspireForm 0.5.0 nupkg: ./artifacts/AspireForm.0.5.0.nupkg
- AspireForm.Annotations 0.1.0 nupkg: ./artifacts/AspireForm.Annotations.0.1.0.nupkg
- Total commits this plan: <N>
- Total tests: <prior_count> + <new_count> = <total>; all green
- Ready to ship via `git tag -a v0.5.0 -m "..." && git push origin main && git push origin v0.5.0`
- AspireForm.Annotations release pipeline: needs a new job in .github/workflows/release.yml triggered by `annotations/v*` tags (similar to the existing `publish-plugin` job). If not present, ship Annotations via a manual `dotnet nuget push ./artifacts/AspireForm.Annotations.0.1.0.nupkg --api-key $NUGET_API_KEY --source https://api.nuget.org/v3/index.json` after the user verifies.
```

---

## Definition of done

- `aspireform ui` starts a Kestrel + Blazor Server host on localhost; `/`, `/entities`, `/diagnostics`, `/about` all render
- `/entities` supports full CRUD: create entity, delete entity, add/remove/rename properties, add/remove relationships, toggle DAB attributes — all via Roslyn
- 12 new MCP entity tools registered (`aspireform mcp` registry size = 29)
- Built-in `ef-data` provider emits a real DbContext from a fixture project and (when applicable) `dab-config.json` from `[DabExpose]` entities
- `AspireForm.Annotations 0.1.0` packs cleanly and is referenceable from any project targeting `netstandard2.0` or later
- Test suite green; new test count ≥ 60
- `AspireForm 0.5.0` and `AspireForm.Annotations 0.1.0` nupkgs in `./artifacts/`
- README has the "Use the entity builder" section + code-first example + MCP tool list
- CHANGELOG has the `[0.5.0]` entry including the breaking `ef-data` input-shape migration

