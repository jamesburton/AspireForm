# AspireForm Core Engine — Plan 1: Foundations & `config`

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the AspireForm tool skeleton, the format-agnostic configuration pipeline, and the state store — shipping a `dnx`-runnable tool with working `config` and `doctor` commands.

**Architecture:** A .NET 10 console app packaged as a .NET tool (`PackAsTool`). Config files (YAML or JSONC) are each parsed into a normalized `System.Text.Json.Nodes.JsonObject` DOM; per-environment override files are deep-merged; `${VAR}` placeholders are interpolated from `.env` + process environment; the merged DOM is bound to a canonical `ProjectModel`. A separate state store reads/writes `.aspireform/state.json`. The CLI is built on `Spectre.Console.Cli`.

**Tech Stack:** .NET 10 (`net10.0`, SDK 10.0.300) · `Spectre.Console.Cli` 0.55.0 · `YamlDotNet` 18.0.0 · `System.Text.Json` (in-box) · xUnit v3 3.2.2 on Microsoft Testing Platform · `AwesomeAssertions` 9.4.0.

**Spec:** `docs/superpowers/specs/2026-05-22-aspireform-core-engine-design.md` — this plan implements §3 (skeleton), §4 (config layer), §7 (state store), the `config`/`doctor` rows of §9, and the §10 `IAspireCli` seam (minimal).

**Plan position:** Plan 1 of 3. Plan 2 adds the planner and `plan`; Plan 3 adds the executor and `apply`.

---

## Conventions for the executor

- **Assertions:** use `AwesomeAssertions` (`value.Should()....`) in every test. Do not use raw `Assert`.
- **XML docs:** every public type and public member gets at least a one-line `/// <summary>`.
- **Run a single test:** `dotnet test tests/AspireForm.Tests --filter "FullyQualifiedName~<TestName>"`.
- **Run all tests:** `dotnet test`.
- All paths below are relative to the repo root `c:/Development/AspireForm`.

---

## File structure

```
AspireForm.sln
.gitattributes
README.md
CLAUDE.md
src/AspireForm/
  AspireForm.csproj
  Program.cs                              entry point + Spectre CommandApp
  Cli/ConfigCommand.cs                    `config` / `show` verb
  Cli/DoctorCommand.cs                    `doctor` verb
  Configuration/ProjectModel.cs           canonical model (ProjectModel + block types)
  Configuration/ConfigFormat.cs           format enum + extension detection
  Configuration/IConfigParser.cs          parser interface
  Configuration/JsoncConfigParser.cs      JSONC -> JsonObject
  Configuration/YamlConfigParser.cs       YAML  -> JsonObject
  Configuration/JsonObjectMerge.cs        deep-merge for override layering
  Configuration/EnvFile.cs                .env file reader
  Configuration/Interpolator.cs           ${VAR} substitution over the DOM
  Configuration/ConfigModelBinder.cs      JsonObject -> ProjectModel + validation
  Configuration/ConfigValidationException.cs
  Configuration/ConfigLoader.cs           discovery + parse + merge + interpolate + bind
  State/StateModel.cs                     AspireFormState + BlockState + FileState
  State/StateStore.cs                     read/write .aspireform/state.json
  Aspire/IAspireCli.cs                    minimal aspire-CLI seam
  Aspire/AspireCli.cs                     shell-out implementation
  Diagnostics/PrerequisiteReport.cs       doctor result model
  Diagnostics/PrerequisiteChecker.cs      doctor checks
tests/AspireForm.Tests/
  AspireForm.Tests.csproj
  (mirrors the src folders)
examples/sample/aspireform.yaml           fixture used by integration tests
examples/sample/aspireform.dev.yaml
```

---

## Task 1: Solution, projects, packaging, and test harness

**Files:**
- Create: `AspireForm.sln`, `.gitattributes`, `src/AspireForm/AspireForm.csproj`, `src/AspireForm/Program.cs`
- Create: `tests/AspireForm.Tests/AspireForm.Tests.csproj`, `tests/AspireForm.Tests/HarnessTests.cs`

- [ ] **Step 1: Create the solution and the tool project**

```bash
cd c:/Development/AspireForm
dotnet new sln -n AspireForm
dotnet new console -o src/AspireForm -n AspireForm
dotnet sln add src/AspireForm/AspireForm.csproj
```

- [ ] **Step 2: Replace `src/AspireForm/AspireForm.csproj` with the packaged-tool csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>AspireForm</RootNamespace>
    <AssemblyName>AspireForm</AssemblyName>

    <PackAsTool>true</PackAsTool>
    <ToolCommandName>aspireform</ToolCommandName>
    <PackageId>AspireForm</PackageId>
    <Version>0.1.0</Version>
    <Authors>AspireForm</Authors>
    <Description>Declarative construction and configuration of .NET Aspire applications.</Description>
    <PackageReadmeFile>README.md</PackageReadmeFile>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Spectre.Console.Cli" Version="0.55.0" />
    <PackageReference Include="YamlDotNet" Version="18.0.0" />
  </ItemGroup>

  <ItemGroup>
    <None Include="../../README.md" Pack="true" PackagePath="\" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Create `.gitattributes` at the repo root**

This fixes the CRLF warnings already seen on this Windows repo and keeps snapshot tests stable across platforms.

```gitattributes
* text=auto eol=lf
*.sln text eol=crlf
*.csproj text eol=lf
*.png binary
*.jpg binary
```

- [ ] **Step 4: Create the test project from the xUnit v3 template**

```bash
dotnet new install xunit.v3.templates
dotnet new xunit3 -o tests/AspireForm.Tests -n AspireForm.Tests
dotnet sln add tests/AspireForm.Tests/AspireForm.Tests.csproj
dotnet add tests/AspireForm.Tests/AspireForm.Tests.csproj reference src/AspireForm/AspireForm.csproj
dotnet add tests/AspireForm.Tests/AspireForm.Tests.csproj package AwesomeAssertions --version 9.4.0
dotnet add tests/AspireForm.Tests/AspireForm.Tests.csproj package Spectre.Console.Testing --version 0.55.0
```

If `dotnet new xunit3` reports the template is unavailable, the install step failed — re-run `dotnet new install xunit.v3.templates` and confirm network access to NuGet.

- [ ] **Step 5: Write the harness smoke test**

Replace the template-generated test file with `tests/AspireForm.Tests/HarnessTests.cs`:

```csharp
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests;

/// <summary>Confirms the xUnit v3 / MTP harness and AwesomeAssertions are wired up.</summary>
public sealed class HarnessTests
{
    [Fact]
    public void Harness_runs_and_assertions_work()
    {
        const int answer = 42;
        answer.Should().Be(42);
    }
}
```

Delete the template's default `UnitTest1.cs` (or equivalent) if one was generated.

- [ ] **Step 6: Build, test, and verify the tool packs**

```bash
dotnet build
dotnet test
dotnet pack src/AspireForm/AspireForm.csproj -o ./artifacts
```

Expected: build succeeds; 1 test passes; `./artifacts/AspireForm.0.1.0.nupkg` is produced. Verify it is a tool package:

```bash
unzip -p ./artifacts/AspireForm.0.1.0.nupkg AspireForm.nuspec | grep packageType
```

Expected: a line containing `packageType name="DotnetTool"`.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "chore: scaffold AspireForm tool, test harness, and packaging"
```

---

## Task 2: Project documentation — README.md and CLAUDE.md

**Files:**
- Create: `README.md`, `CLAUDE.md`

These are living documents seeded now so contributors and agents have orientation; later plans expand them. No test — verified by review.

- [ ] **Step 1: Create `README.md`**

```markdown
# AspireForm

Declarative construction and configuration of [.NET Aspire](https://aspire.dev) applications —
Infrastructure-as-Code ideas (Terraform) and declarative orchestration (Docker Compose) applied
to scaffolding and evolving an Aspire solution.

You describe the desired shape of your app in `aspireform.yaml` (or `aspireform.jsonc`); AspireForm
reconciles that against what is on disk and applies the difference.

## Status

Early development. Plan 1 of 3 (Foundations) is in progress: the `config` and `doctor` commands.

## Install / run

AspireForm is a zero-install .NET tool. With the .NET 10 SDK present:

    dnx AspireForm config
    dnx AspireForm doctor

`dnx` resolves the latest published version on each run, so the tool is always current.

## Commands (Plan 1)

| Command | Description |
|---|---|
| `aspireform config` | Print the fully merged and interpolated desired-state configuration. |
| `aspireform doctor`  | Check prerequisites: the .NET 10 SDK and the `aspire` CLI. |

`new`, `add`, `plan`, `apply`, `destroy`, `import`, and `state` arrive in Plans 2–3.

## Configuration

A minimal `aspireform.yaml`:

    aspireform:
      version: 1
      project: MyApp
      apphost: ./MyApp.AppHost
    resources:
      sql:
        type: sqlserver
        aspireName: sql
        databases: [appdb]

Per-environment overrides go in `aspireform.<env>.yaml` and are layered with `--env <name>`.

## Documentation

- Design spec: `docs/superpowers/specs/`
- Research notes: `docs/research/`
- Implementation plans: `docs/superpowers/plans/`
```

- [ ] **Step 2: Create `CLAUDE.md`**

```markdown
# CLAUDE.md — AspireForm

Architectural and technical guidance for agents working on AspireForm.

## What this is

AspireForm is a .NET 10 tool that constructs and configures .NET Aspire applications declaratively.
It layers a Terraform-style `plan`/`apply` reconciliation loop and Docker-Compose-style config
ergonomics on top of the official `aspire` CLI.

## Read first

- `docs/superpowers/specs/2026-05-22-aspireform-core-engine-design.md` — the authoritative design.
- `docs/research/` — background on Terraform, Aspire 13.x, `dnx`, the verticals, and Docker Compose.
- `docs/superpowers/plans/` — the implementation plans (3 for the core engine).

## Core concepts

- **Resource** — infrastructure (SQL Server, Redis, …); managed and safely destroyable.
- **Module** — a feature slice that scaffolds cross-layer code; destroy-protected by default.
- **Ownership mode** — every generated file is tagged `managed`, `scaffold`, or `merge`, which
  determines what `apply` does to it on re-run.
- **State** — `.aspireform/state.json` (source of truth) plus in-file `// aspireform:` markers.

## Conventions

- Target framework `net10.0`. C# nullable enabled, implicit usings enabled.
- Tests: xUnit v3 on the Microsoft Testing Platform; assertions via `AwesomeAssertions`.
- Public types and members carry XML doc comments.
- The config pipeline is format-agnostic: YAML and JSONC both normalize to a
  `System.Text.Json.Nodes.JsonObject` DOM before any logic runs.
- All interaction with the `aspire` CLI goes through `IAspireCli` — never shell out directly.

## Build & test

    dotnet build
    dotnet test
    dotnet pack src/AspireForm/AspireForm.csproj -o ./artifacts
```

- [ ] **Step 3: Commit**

```bash
git add README.md CLAUDE.md
git commit -m "docs: add README and CLAUDE project guidance"
```

---

## Task 3: Canonical configuration model

**Files:**
- Create: `src/AspireForm/Configuration/ProjectModel.cs`
- Test: `tests/AspireForm.Tests/Configuration/ProjectModelTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Text.Json.Nodes;
using AspireForm.Configuration;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Configuration;

public sealed class ProjectModelTests
{
    [Fact]
    public void ResourceBlock_defaults_inputs_to_empty_object()
    {
        var block = new ResourceBlock { Name = "sql", Type = "sqlserver" };
        block.Inputs.Should().NotBeNull();
        block.Inputs.Count.Should().Be(0);
    }

    [Fact]
    public void ModuleBlock_is_destroy_protected_by_default()
    {
        var block = new ModuleBlock { Name = "data", Type = "ef-data" };
        block.PreventDestroy.Should().BeTrue();
        block.DependsOn.Should().BeEmpty();
    }

    [Fact]
    public void ProjectModel_holds_header_resources_and_modules()
    {
        var model = new ProjectModel
        {
            AspireForm = new AspireFormHeader { Version = 1, Project = "MyApp", AppHost = "./MyApp.AppHost" },
            Resources = new Dictionary<string, ResourceBlock>
            {
                ["sql"] = new() { Name = "sql", Type = "sqlserver", Inputs = new JsonObject() },
            },
        };

        model.AspireForm.Project.Should().Be("MyApp");
        model.Resources.Should().ContainKey("sql");
        model.Modules.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AspireForm.Tests --filter "FullyQualifiedName~ProjectModelTests"`
Expected: FAIL — `ProjectModel` / `ResourceBlock` / `ModuleBlock` do not exist (compile error).

- [ ] **Step 3: Create `src/AspireForm/Configuration/ProjectModel.cs`**

```csharp
using System.Text.Json.Nodes;

namespace AspireForm.Configuration;

/// <summary>The canonical, format-agnostic representation of an AspireForm project configuration.</summary>
public sealed class ProjectModel
{
    /// <summary>The top-level <c>aspireform</c> header section.</summary>
    public required AspireFormHeader AspireForm { get; init; }

    /// <summary>Declared infrastructure resources, keyed by block name.</summary>
    public IReadOnlyDictionary<string, ResourceBlock> Resources { get; init; }
        = new Dictionary<string, ResourceBlock>();

    /// <summary>Declared feature-slice modules, keyed by block name.</summary>
    public IReadOnlyDictionary<string, ModuleBlock> Modules { get; init; }
        = new Dictionary<string, ModuleBlock>();

    /// <summary>Reserved profile definitions. Parsed and validated but with no behaviour in v1.</summary>
    public IReadOnlyDictionary<string, JsonObject> Profiles { get; init; }
        = new Dictionary<string, JsonObject>();
}

/// <summary>The <c>aspireform</c> header: schema version and project identity.</summary>
public sealed class AspireFormHeader
{
    /// <summary>The configuration schema version. Only version 1 is supported.</summary>
    public required int Version { get; init; }

    /// <summary>The project name.</summary>
    public required string Project { get; init; }

    /// <summary>Relative path to the Aspire AppHost project.</summary>
    public required string AppHost { get; init; }
}

/// <summary>An infrastructure resource block (managed, destroyable).</summary>
public sealed class ResourceBlock
{
    /// <summary>The block name (its key under <c>resources</c>).</summary>
    public required string Name { get; init; }

    /// <summary>The provider type, e.g. <c>sqlserver</c>.</summary>
    public required string Type { get; init; }

    /// <summary>Provider-specific inputs. Bound and validated by the provider, not the loader.</summary>
    public JsonObject Inputs { get; init; } = new();
}

/// <summary>A feature-slice module block (scaffolds cross-layer code, destroy-protected by default).</summary>
public sealed class ModuleBlock
{
    /// <summary>The block name (its key under <c>modules</c>).</summary>
    public required string Name { get; init; }

    /// <summary>The provider type, e.g. <c>ef-data</c>.</summary>
    public required string Type { get; init; }

    /// <summary>Names of blocks this module depends on.</summary>
    public IReadOnlyList<string> DependsOn { get; init; } = [];

    /// <summary>When true (the default), <c>destroy</c> refuses to remove this module without an explicit override.</summary>
    public bool PreventDestroy { get; init; } = true;

    /// <summary>Provider-specific inputs. Bound and validated by the provider, not the loader.</summary>
    public JsonObject Inputs { get; init; } = new();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AspireForm.Tests --filter "FullyQualifiedName~ProjectModelTests"`
Expected: PASS — 3 tests.

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Configuration/ProjectModel.cs tests/AspireForm.Tests/Configuration/ProjectModelTests.cs
git commit -m "feat: add canonical ProjectModel configuration model"
```

---

## Task 4: JSONC parser

**Files:**
- Create: `src/AspireForm/Configuration/ConfigFormat.cs`, `src/AspireForm/Configuration/IConfigParser.cs`,
  `src/AspireForm/Configuration/ConfigValidationException.cs`, `src/AspireForm/Configuration/JsoncConfigParser.cs`
- Test: `tests/AspireForm.Tests/Configuration/JsoncConfigParserTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.Configuration;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Configuration;

public sealed class JsoncConfigParserTests
{
    private readonly JsoncConfigParser _parser = new();

    [Fact]
    public void Parses_object_with_line_and_block_comments_and_trailing_commas()
    {
        const string text = """
            {
              // a line comment
              "aspireform": { "version": 1, "project": "MyApp", },
              /* block comment */
              "resources": {}
            }
            """;

        var root = _parser.Parse(text);

        root["aspireform"]!["version"]!.GetValue<int>().Should().Be(1);
        root["aspireform"]!["project"]!.GetValue<string>().Should().Be("MyApp");
    }

    [Fact]
    public void Throws_ConfigValidationException_when_root_is_not_an_object()
    {
        var act = () => _parser.Parse("[1, 2, 3]");
        act.Should().Throw<ConfigValidationException>();
    }

    [Fact]
    public void Throws_ConfigValidationException_on_malformed_json()
    {
        var act = () => _parser.Parse("{ not json");
        act.Should().Throw<ConfigValidationException>();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AspireForm.Tests --filter "FullyQualifiedName~JsoncConfigParserTests"`
Expected: FAIL — types do not exist (compile error).

- [ ] **Step 3: Create the supporting types and the parser**

`src/AspireForm/Configuration/ConfigValidationException.cs`:

```csharp
namespace AspireForm.Configuration;

/// <summary>Raised when a configuration file is malformed, invalid, or fails schema validation.</summary>
public sealed class ConfigValidationException : Exception
{
    /// <summary>Initializes the exception with a human-readable message.</summary>
    public ConfigValidationException(string message) : base(message) { }

    /// <summary>Initializes the exception with a message and an inner cause.</summary>
    public ConfigValidationException(string message, Exception inner) : base(message, inner) { }
}
```

`src/AspireForm/Configuration/ConfigFormat.cs`:

```csharp
namespace AspireForm.Configuration;

/// <summary>The on-disk format of a configuration file.</summary>
public enum ConfigFormat
{
    /// <summary>YAML (<c>.yaml</c> / <c>.yml</c>).</summary>
    Yaml,

    /// <summary>JSON with comments (<c>.jsonc</c> / <c>.json</c>).</summary>
    Jsonc,
}

/// <summary>Maps file extensions to <see cref="ConfigFormat"/>.</summary>
public static class ConfigFormatDetector
{
    /// <summary>Determines the format from a file path's extension, or null when unrecognized.</summary>
    public static ConfigFormat? FromPath(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".yaml" or ".yml" => ConfigFormat.Yaml,
            ".jsonc" or ".json" => ConfigFormat.Jsonc,
            _ => null,
        };
}
```

`src/AspireForm/Configuration/IConfigParser.cs`:

```csharp
using System.Text.Json.Nodes;

namespace AspireForm.Configuration;

/// <summary>Parses a configuration file's text into a normalized <see cref="JsonObject"/> DOM.</summary>
public interface IConfigParser
{
    /// <summary>Parses configuration text. Throws <see cref="ConfigValidationException"/> on malformed input or a non-object root.</summary>
    JsonObject Parse(string text);
}
```

`src/AspireForm/Configuration/JsoncConfigParser.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AspireForm.Configuration;

/// <summary>Parses JSON-with-comments configuration text into a <see cref="JsonObject"/>.</summary>
public sealed class JsoncConfigParser : IConfigParser
{
    private static readonly JsonNodeOptions NodeOptions = new() { PropertyNameCaseInsensitive = false };

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <inheritdoc />
    public JsonObject Parse(string text)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text, NodeOptions, DocumentOptions);
        }
        catch (JsonException ex)
        {
            throw new ConfigValidationException($"Invalid JSONC configuration: {ex.Message}", ex);
        }

        if (node is not JsonObject obj)
        {
            throw new ConfigValidationException("The configuration root must be an object.");
        }

        return obj;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AspireForm.Tests --filter "FullyQualifiedName~JsoncConfigParserTests"`
Expected: PASS — 3 tests.

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Configuration/ tests/AspireForm.Tests/Configuration/JsoncConfigParserTests.cs
git commit -m "feat: add JSONC config parser and supporting types"
```

---

## Task 5: YAML parser (with format parity)

This task carries the most novel logic in the plan: converting YamlDotNet's untyped object graph
into the same `JsonObject` DOM the JSONC parser produces. Scalar type inference is the subtle part.

**Files:**
- Create: `src/AspireForm/Configuration/YamlConfigParser.cs`
- Test: `tests/AspireForm.Tests/Configuration/YamlConfigParserTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.Configuration;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Configuration;

public sealed class YamlConfigParserTests
{
    private readonly YamlConfigParser _yaml = new();
    private readonly JsoncConfigParser _jsonc = new();

    [Fact]
    public void Infers_scalar_types_from_unquoted_yaml()
    {
        const string text = """
            aspireform:
              version: 1
              project: MyApp
            enabled: true
            ratio: 1.5
            """;

        var root = _yaml.Parse(text);

        root["aspireform"]!["version"]!.GetValue<int>().Should().Be(1);
        root["aspireform"]!["project"]!.GetValue<string>().Should().Be("MyApp");
        root["enabled"]!.GetValue<bool>().Should().BeTrue();
        root["ratio"]!.GetValue<double>().Should().Be(1.5);
    }

    [Fact]
    public void Converts_sequences_to_json_arrays()
    {
        const string text = """
            databases:
              - appdb
              - reportdb
            """;

        var root = _yaml.Parse(text);
        var dbs = root["databases"]!.AsArray();

        dbs.Count.Should().Be(2);
        dbs[0]!.GetValue<string>().Should().Be("appdb");
    }

    [Fact]
    public void Yaml_and_jsonc_produce_identical_dom_for_equivalent_input()
    {
        const string yaml = """
            aspireform:
              version: 1
              project: MyApp
              apphost: ./MyApp.AppHost
            resources:
              sql:
                type: sqlserver
                databases: [appdb]
            """;
        const string jsonc = """
            {
              "aspireform": { "version": 1, "project": "MyApp", "apphost": "./MyApp.AppHost" },
              "resources": { "sql": { "type": "sqlserver", "databases": ["appdb"] } }
            }
            """;

        var fromYaml = _yaml.Parse(yaml).ToJsonString();
        var fromJsonc = _jsonc.Parse(jsonc).ToJsonString();

        fromYaml.Should().Be(fromJsonc);
    }

    [Fact]
    public void Throws_ConfigValidationException_when_root_is_a_sequence()
    {
        var act = () => _yaml.Parse("- one\n- two");
        act.Should().Throw<ConfigValidationException>();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AspireForm.Tests --filter "FullyQualifiedName~YamlConfigParserTests"`
Expected: FAIL — `YamlConfigParser` does not exist (compile error).

- [ ] **Step 3: Create `src/AspireForm/Configuration/YamlConfigParser.cs`**

The converter walks the graph YamlDotNet returns. `WithAttemptingUnquotedStringTypeDeserialization()`
makes YamlDotNet type unquoted scalars (so `version: 1` is a `long`, not `"1"`), giving parity with
JSON. Mappings arrive as `IDictionary<object, object>`; sequences as `IList<object>`; everything else
is a scalar. Note that `string` does not implement `IEnumerable<object>`, so the sequence branch does
not accidentally catch strings.

```csharp
using System.Collections;
using System.Text.Json.Nodes;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace AspireForm.Configuration;

/// <summary>Parses YAML configuration text into the same <see cref="JsonObject"/> DOM that <see cref="JsoncConfigParser"/> produces.</summary>
public sealed class YamlConfigParser : IConfigParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithAttemptingUnquotedStringTypeDeserialization()
        .Build();

    /// <inheritdoc />
    public JsonObject Parse(string text)
    {
        object? graph;
        try
        {
            graph = Deserializer.Deserialize<object?>(text);
        }
        catch (YamlException ex)
        {
            throw new ConfigValidationException($"Invalid YAML configuration: {ex.Message}", ex);
        }

        if (graph is null)
        {
            // An empty document is treated as an empty configuration object.
            return new JsonObject();
        }

        var node = ConvertToJsonNode(graph);
        if (node is not JsonObject obj)
        {
            throw new ConfigValidationException("The configuration root must be a mapping.");
        }

        return obj;
    }

    private static JsonNode? ConvertToJsonNode(object? value)
    {
        switch (value)
        {
            case null:
                return null;

            case IDictionary<object, object> map:
            {
                var obj = new JsonObject();
                foreach (var (key, item) in map)
                {
                    obj[key?.ToString() ?? string.Empty] = ConvertToJsonNode(item);
                }

                return obj;
            }

            case string s:
                return JsonValue.Create(s);

            case IEnumerable sequence:
            {
                var array = new JsonArray();
                foreach (var item in sequence)
                {
                    array.Add(ConvertToJsonNode(item));
                }

                return array;
            }

            case bool b:
                return JsonValue.Create(b);

            case byte or sbyte or short or ushort or int or uint or long:
                return JsonValue.Create(Convert.ToInt64(value));

            case ulong ul:
                return JsonValue.Create(ul);

            case float or double:
                return JsonValue.Create(Convert.ToDouble(value));

            case decimal d:
                return JsonValue.Create(d);

            default:
                return JsonValue.Create(value.ToString());
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AspireForm.Tests --filter "FullyQualifiedName~YamlConfigParserTests"`
Expected: PASS — 4 tests. If the parity test fails, compare the two `ToJsonString()` outputs: the
usual cause is integer width (ensure integral scalars convert to `long` via `JsonValue.Create((long)…)`).

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Configuration/YamlConfigParser.cs tests/AspireForm.Tests/Configuration/YamlConfigParserTests.cs
git commit -m "feat: add YAML config parser with JSONC DOM parity"
```

---

## Task 6: Deep-merge for override layering

**Semantics (locked):** mappings deep-merge; sequences and scalars in the override **replace** the
base wholesale; a key absent from the override leaves the base untouched; an **explicit `null`** in
the override **removes** the key from the result. These are the rules tested below.

**Files:**
- Create: `src/AspireForm/Configuration/JsonObjectMerge.cs`
- Test: `tests/AspireForm.Tests/Configuration/JsonObjectMergeTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Text.Json.Nodes;
using AspireForm.Configuration;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Configuration;

public sealed class JsonObjectMergeTests
{
    private static JsonObject Obj(string json) => (JsonObject)JsonNode.Parse(json)!;

    [Fact]
    public void Mappings_are_deep_merged()
    {
        var result = JsonObjectMerge.Merge(
            Obj("""{ "a": { "x": 1, "y": 2 } }"""),
            Obj("""{ "a": { "y": 20, "z": 30 } }"""));

        result["a"]!["x"]!.GetValue<int>().Should().Be(1);
        result["a"]!["y"]!.GetValue<int>().Should().Be(20);
        result["a"]!["z"]!.GetValue<int>().Should().Be(30);
    }

    [Fact]
    public void Sequences_are_replaced_wholesale()
    {
        var result = JsonObjectMerge.Merge(
            Obj("""{ "items": [1, 2, 3] }"""),
            Obj("""{ "items": [9] }"""));

        result["items"]!.AsArray().Count.Should().Be(1);
        result["items"]![0]!.GetValue<int>().Should().Be(9);
    }

    [Fact]
    public void Empty_sequence_in_override_replaces_to_empty()
    {
        var result = JsonObjectMerge.Merge(
            Obj("""{ "items": [1, 2] }"""),
            Obj("""{ "items": [] }"""));

        result["items"]!.AsArray().Should().BeEmpty();
    }

    [Fact]
    public void Key_absent_from_override_is_preserved_from_base()
    {
        var result = JsonObjectMerge.Merge(
            Obj("""{ "keep": "me", "change": "old" }"""),
            Obj("""{ "change": "new" }"""));

        result["keep"]!.GetValue<string>().Should().Be("me");
        result["change"]!.GetValue<string>().Should().Be("new");
    }

    [Fact]
    public void Explicit_null_in_override_removes_the_key()
    {
        var result = JsonObjectMerge.Merge(
            Obj("""{ "drop": { "nested": true }, "keep": 1 }"""),
            Obj("""{ "drop": null }"""));

        result.ContainsKey("drop").Should().BeFalse();
        result.ContainsKey("keep").Should().BeTrue();
    }

    [Fact]
    public void Merge_does_not_mutate_its_inputs()
    {
        var baseObj = Obj("""{ "a": 1 }""");
        JsonObjectMerge.Merge(baseObj, Obj("""{ "a": 2 }"""));
        baseObj["a"]!.GetValue<int>().Should().Be(1);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AspireForm.Tests --filter "FullyQualifiedName~JsonObjectMergeTests"`
Expected: FAIL — `JsonObjectMerge` does not exist (compile error).

- [ ] **Step 3: Create `src/AspireForm/Configuration/JsonObjectMerge.cs`**

```csharp
using System.Text.Json.Nodes;

namespace AspireForm.Configuration;

/// <summary>Deep-merges configuration override DOMs onto a base DOM.</summary>
public static class JsonObjectMerge
{
    /// <summary>
    /// Returns a new object: <paramref name="overrideObj"/> deep-merged onto <paramref name="baseObj"/>.
    /// Mappings merge recursively; arrays and scalars replace; an explicit null override removes the key.
    /// Neither input is mutated.
    /// </summary>
    public static JsonObject Merge(JsonObject baseObj, JsonObject overrideObj)
    {
        var result = (JsonObject)baseObj.DeepClone();

        foreach (var (key, overrideValue) in overrideObj)
        {
            if (overrideValue is null)
            {
                result.Remove(key);
                continue;
            }

            if (result.TryGetPropertyValue(key, out var baseValue)
                && baseValue is JsonObject baseChild
                && overrideValue is JsonObject overrideChild)
            {
                result[key] = Merge(baseChild, overrideChild);
            }
            else
            {
                result[key] = overrideValue.DeepClone();
            }
        }

        return result;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AspireForm.Tests --filter "FullyQualifiedName~JsonObjectMergeTests"`
Expected: PASS — 6 tests.

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Configuration/JsonObjectMerge.cs tests/AspireForm.Tests/Configuration/JsonObjectMergeTests.cs
git commit -m "feat: add deep-merge for config override layering"
```

---

## Task 7: `.env` file reader

**Files:**
- Create: `src/AspireForm/Configuration/EnvFile.cs`
- Test: `tests/AspireForm.Tests/Configuration/EnvFileTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.Configuration;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Configuration;

public sealed class EnvFileTests
{
    [Fact]
    public void Parses_key_value_pairs_ignoring_comments_and_blank_lines()
    {
        const string text = """
            # a comment
            DB_NAME=appdb

            DB_HOST = localhost
            QUOTED="with spaces"
            """;

        var values = EnvFile.Parse(text);

        values["DB_NAME"].Should().Be("appdb");
        values["DB_HOST"].Should().Be("localhost");
        values["QUOTED"].Should().Be("with spaces");
        values.Should().HaveCount(3);
    }

    [Fact]
    public void Ignores_lines_without_an_equals_sign()
    {
        var values = EnvFile.Parse("NOT_A_PAIR\nVALID=1");
        values.Should().ContainKey("VALID").And.NotContainKey("NOT_A_PAIR");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AspireForm.Tests --filter "FullyQualifiedName~EnvFileTests"`
Expected: FAIL — `EnvFile` does not exist (compile error).

- [ ] **Step 3: Create `src/AspireForm/Configuration/EnvFile.cs`**

```csharp
namespace AspireForm.Configuration;

/// <summary>Reads <c>.env</c>-style files into a dictionary of environment values.</summary>
public static class EnvFile
{
    /// <summary>Parses <c>.env</c> text. Lines without <c>=</c>, blank lines, and <c>#</c> comments are ignored. Surrounding quotes on values are stripped.</summary>
    public static IReadOnlyDictionary<string, string> Parse(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();

            if ((value.StartsWith('"') && value.EndsWith('"') && value.Length >= 2)
                || (value.StartsWith('\'') && value.EndsWith('\'') && value.Length >= 2))
            {
                value = value[1..^1];
            }

            result[key] = value;
        }

        return result;
    }

    /// <summary>Reads and parses a <c>.env</c> file if it exists; returns an empty map when it does not.</summary>
    public static IReadOnlyDictionary<string, string> Load(string path) =>
        File.Exists(path) ? Parse(File.ReadAllText(path)) : new Dictionary<string, string>(StringComparer.Ordinal);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AspireForm.Tests --filter "FullyQualifiedName~EnvFileTests"`
Expected: PASS — 2 tests.

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Configuration/EnvFile.cs tests/AspireForm.Tests/Configuration/EnvFileTests.cs
git commit -m "feat: add .env file reader"
```

---

## Task 8: `${VAR}` interpolation

**Precedence (locked):** process environment variables override `.env` values. An undefined variable
with no `:-default` is a hard error.

**Files:**
- Create: `src/AspireForm/Configuration/Interpolator.cs`
- Test: `tests/AspireForm.Tests/Configuration/InterpolatorTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Text.Json.Nodes;
using AspireForm.Configuration;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Configuration;

public sealed class InterpolatorTests
{
    private static JsonObject Obj(string json) => (JsonObject)JsonNode.Parse(json)!;

    [Fact]
    public void Substitutes_a_known_variable_in_string_values()
    {
        var dom = Obj("""{ "project": "${NAME}" }""");
        var result = Interpolator.Apply(dom, new Dictionary<string, string> { ["NAME"] = "MyApp" });
        result["project"]!.GetValue<string>().Should().Be("MyApp");
    }

    [Fact]
    public void Uses_default_when_variable_is_undefined()
    {
        var dom = Obj("""{ "host": "${DB_HOST:-localhost}" }""");
        var result = Interpolator.Apply(dom, new Dictionary<string, string>());
        result["host"]!.GetValue<string>().Should().Be("localhost");
    }

    [Fact]
    public void Throws_when_variable_is_undefined_and_has_no_default()
    {
        var dom = Obj("""{ "host": "${MISSING}" }""");
        var act = () => Interpolator.Apply(dom, new Dictionary<string, string>());
        act.Should().Throw<ConfigValidationException>().WithMessage("*MISSING*");
    }

    [Fact]
    public void Does_not_touch_numbers_or_booleans()
    {
        var dom = Obj("""{ "version": 1, "enabled": true }""");
        var result = Interpolator.Apply(dom, new Dictionary<string, string>());
        result["version"]!.GetValue<int>().Should().Be(1);
        result["enabled"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public void Interpolates_inside_nested_objects_and_arrays()
    {
        var dom = Obj("""{ "a": { "b": "${V}" }, "c": ["${V}"] }""");
        var result = Interpolator.Apply(dom, new Dictionary<string, string> { ["V"] = "x" });
        result["a"]!["b"]!.GetValue<string>().Should().Be("x");
        result["c"]![0]!.GetValue<string>().Should().Be("x");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AspireForm.Tests --filter "FullyQualifiedName~InterpolatorTests"`
Expected: FAIL — `Interpolator` does not exist (compile error).

- [ ] **Step 3: Create `src/AspireForm/Configuration/Interpolator.cs`**

```csharp
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AspireForm.Configuration;

/// <summary>Substitutes <c>${VAR}</c> and <c>${VAR:-default}</c> placeholders in string values of a config DOM.</summary>
public static partial class Interpolator
{
    [GeneratedRegex(@"\$\{(?<name>[A-Za-z_][A-Za-z0-9_]*)(?::-(?<default>[^}]*))?\}")]
    private static partial Regex PlaceholderRegex();

    /// <summary>
    /// Returns a new DOM with every string value interpolated against <paramref name="variables"/>.
    /// An undefined variable without a <c>:-default</c> throws <see cref="ConfigValidationException"/>.
    /// </summary>
    public static JsonObject Apply(JsonObject dom, IReadOnlyDictionary<string, string> variables)
    {
        return (JsonObject)Walk(dom.DeepClone(), variables)!;
    }

    /// <summary>Builds the variable map: <c>.env</c> values overlaid by process environment variables (process wins).</summary>
    public static IReadOnlyDictionary<string, string> BuildVariables(IReadOnlyDictionary<string, string> envFile)
    {
        var merged = new Dictionary<string, string>(envFile, StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            merged[entry.Key.ToString()!] = entry.Value?.ToString() ?? string.Empty;
        }

        return merged;
    }

    private static JsonNode? Walk(JsonNode? node, IReadOnlyDictionary<string, string> variables)
    {
        switch (node)
        {
            case JsonObject obj:
            {
                foreach (var key in obj.Select(p => p.Key).ToList())
                {
                    obj[key] = Walk(obj[key], variables);
                }

                return obj;
            }

            case JsonArray array:
            {
                for (var i = 0; i < array.Count; i++)
                {
                    array[i] = Walk(array[i], variables);
                }

                return array;
            }

            case JsonValue value when value.TryGetValue(out string? text):
                return JsonValue.Create(Substitute(text, variables));

            default:
                return node;
        }
    }

    private static string Substitute(string text, IReadOnlyDictionary<string, string> variables)
    {
        return PlaceholderRegex().Replace(text, match =>
        {
            var name = match.Groups["name"].Value;
            if (variables.TryGetValue(name, out var value))
            {
                return value;
            }

            if (match.Groups["default"].Success)
            {
                return match.Groups["default"].Value;
            }

            throw new ConfigValidationException(
                $"Configuration variable '{name}' is not defined and has no default (use ${{{name}:-default}}).");
        });
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AspireForm.Tests --filter "FullyQualifiedName~InterpolatorTests"`
Expected: PASS — 5 tests.

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Configuration/Interpolator.cs tests/AspireForm.Tests/Configuration/InterpolatorTests.cs
git commit -m "feat: add \${VAR} interpolation over the config DOM"
```

---

## Task 9: Config model binder and validation

**Validation boundary (locked for Plan 1):** the binder validates the schema version (must be `1`),
required header fields (`project`, `apphost` non-empty), that every block has a non-empty `type`,
and that every `dependsOn` entry **names a declared block**. It does **not** detect dependency
**cycles** — that is the planner's job in Plan 2.

**Files:**
- Create: `src/AspireForm/Configuration/ConfigModelBinder.cs`
- Test: `tests/AspireForm.Tests/Configuration/ConfigModelBinderTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Text.Json.Nodes;
using AspireForm.Configuration;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Configuration;

public sealed class ConfigModelBinderTests
{
    private static JsonObject Obj(string json) => (JsonObject)JsonNode.Parse(json)!;

    private const string ValidHeader =
        """ "aspireform": { "version": 1, "project": "MyApp", "apphost": "./MyApp.AppHost" } """;

    [Fact]
    public void Binds_header_resources_and_modules()
    {
        var dom = Obj($$"""
            {
              {{ValidHeader}},
              "resources": { "sql": { "type": "sqlserver", "aspireName": "sql" } },
              "modules": { "data": { "type": "ef-data", "dependsOn": ["sql"] } }
            }
            """);

        var model = ConfigModelBinder.Bind(dom);

        model.AspireForm.Project.Should().Be("MyApp");
        model.Resources["sql"].Type.Should().Be("sqlserver");
        model.Resources["sql"].Inputs["aspireName"]!.GetValue<string>().Should().Be("sql");
        model.Modules["data"].DependsOn.Should().ContainSingle().Which.Should().Be("sql");
        model.Modules["data"].PreventDestroy.Should().BeTrue();
    }

    [Fact]
    public void Inputs_exclude_reserved_keys()
    {
        var dom = Obj($$"""
            {
              {{ValidHeader}},
              "modules": { "data": { "type": "ef-data", "dependsOn": ["x"], "preventDestroy": false, "database": "appdb" } },
              "resources": { "x": { "type": "sqlserver" } }
            }
            """);

        var model = ConfigModelBinder.Bind(dom);

        model.Modules["data"].PreventDestroy.Should().BeFalse();
        model.Modules["data"].Inputs.ContainsKey("type").Should().BeFalse();
        model.Modules["data"].Inputs.ContainsKey("dependsOn").Should().BeFalse();
        model.Modules["data"].Inputs.ContainsKey("preventDestroy").Should().BeFalse();
        model.Modules["data"].Inputs["database"]!.GetValue<string>().Should().Be("appdb");
    }

    [Theory]
    [InlineData(""" { "resources": {} } """)]                                                  // no header
    [InlineData(""" { "aspireform": { "version": 2, "project": "X", "apphost": "./X" } } """)]  // bad version
    [InlineData(""" { "aspireform": { "version": 1, "project": "", "apphost": "./X" } } """)]   // empty project
    public void Rejects_invalid_headers(string json)
    {
        var act = () => ConfigModelBinder.Bind(Obj(json));
        act.Should().Throw<ConfigValidationException>();
    }

    [Fact]
    public void Rejects_block_without_a_type()
    {
        var dom = Obj($$"""{ {{ValidHeader}}, "resources": { "sql": { "aspireName": "sql" } } }""");
        var act = () => ConfigModelBinder.Bind(dom);
        act.Should().Throw<ConfigValidationException>().WithMessage("*type*");
    }

    [Fact]
    public void Rejects_dependsOn_referencing_an_unknown_block()
    {
        var dom = Obj($$"""
            { {{ValidHeader}}, "modules": { "data": { "type": "ef-data", "dependsOn": ["ghost"] } } }
            """);
        var act = () => ConfigModelBinder.Bind(dom);
        act.Should().Throw<ConfigValidationException>().WithMessage("*ghost*");
    }

    [Fact]
    public void Profiles_are_captured_raw_without_validation()
    {
        var dom = Obj($$"""{ {{ValidHeader}}, "profiles": { "observability": { "anything": true } } }""");
        var model = ConfigModelBinder.Bind(dom);
        model.Profiles.Should().ContainKey("observability");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AspireForm.Tests --filter "FullyQualifiedName~ConfigModelBinderTests"`
Expected: FAIL — `ConfigModelBinder` does not exist (compile error).

- [ ] **Step 3: Create `src/AspireForm/Configuration/ConfigModelBinder.cs`**

```csharp
using System.Text.Json.Nodes;

namespace AspireForm.Configuration;

/// <summary>Binds a merged, interpolated configuration DOM into a validated <see cref="ProjectModel"/>.</summary>
public static class ConfigModelBinder
{
    private const int SupportedSchemaVersion = 1;

    private static readonly string[] ResourceReservedKeys = ["type"];
    private static readonly string[] ModuleReservedKeys = ["type", "dependsOn", "preventDestroy"];

    /// <summary>Binds and validates the DOM. Throws <see cref="ConfigValidationException"/> on any violation.</summary>
    public static ProjectModel Bind(JsonObject dom)
    {
        var header = BindHeader(dom);
        var resources = BindResources(dom);
        var modules = BindModules(dom);
        var profiles = BindProfiles(dom);

        ValidateDependencies(resources, modules);

        return new ProjectModel
        {
            AspireForm = header,
            Resources = resources,
            Modules = modules,
            Profiles = profiles,
        };
    }

    private static AspireFormHeader BindHeader(JsonObject dom)
    {
        if (dom["aspireform"] is not JsonObject section)
        {
            throw new ConfigValidationException("The configuration is missing the required 'aspireform' section.");
        }

        var version = section["version"]?.GetValue<int>()
            ?? throw new ConfigValidationException("'aspireform.version' is required.");
        if (version != SupportedSchemaVersion)
        {
            throw new ConfigValidationException(
                $"Unsupported schema version {version}; this tool supports version {SupportedSchemaVersion}.");
        }

        var project = RequireNonEmptyString(section, "project", "aspireform.project");
        var appHost = RequireNonEmptyString(section, "apphost", "aspireform.apphost");

        return new AspireFormHeader { Version = version, Project = project, AppHost = appHost };
    }

    private static Dictionary<string, ResourceBlock> BindResources(JsonObject dom)
    {
        var result = new Dictionary<string, ResourceBlock>();
        if (dom["resources"] is not JsonObject resources)
        {
            return result;
        }

        foreach (var (name, value) in resources)
        {
            var block = RequireObject(value, $"resources.{name}");
            result[name] = new ResourceBlock
            {
                Name = name,
                Type = RequireNonEmptyString(block, "type", $"resources.{name}.type"),
                Inputs = ExtractInputs(block, ResourceReservedKeys),
            };
        }

        return result;
    }

    private static Dictionary<string, ModuleBlock> BindModules(JsonObject dom)
    {
        var result = new Dictionary<string, ModuleBlock>();
        if (dom["modules"] is not JsonObject modules)
        {
            return result;
        }

        foreach (var (name, value) in modules)
        {
            var block = RequireObject(value, $"modules.{name}");
            var dependsOn = (block["dependsOn"] as JsonArray)?
                .Select(n => n?.GetValue<string>() ?? string.Empty)
                .ToList() ?? [];

            result[name] = new ModuleBlock
            {
                Name = name,
                Type = RequireNonEmptyString(block, "type", $"modules.{name}.type"),
                DependsOn = dependsOn,
                PreventDestroy = block["preventDestroy"]?.GetValue<bool>() ?? true,
                Inputs = ExtractInputs(block, ModuleReservedKeys),
            };
        }

        return result;
    }

    private static Dictionary<string, JsonObject> BindProfiles(JsonObject dom)
    {
        var result = new Dictionary<string, JsonObject>();
        if (dom["profiles"] is not JsonObject profiles)
        {
            return result;
        }

        foreach (var (name, value) in profiles)
        {
            if (value is JsonObject obj)
            {
                result[name] = (JsonObject)obj.DeepClone();
            }
        }

        return result;
    }

    private static void ValidateDependencies(
        IReadOnlyDictionary<string, ResourceBlock> resources,
        IReadOnlyDictionary<string, ModuleBlock> modules)
    {
        var declared = new HashSet<string>(resources.Keys);
        declared.UnionWith(modules.Keys);

        foreach (var module in modules.Values)
        {
            foreach (var dependency in module.DependsOn)
            {
                if (!declared.Contains(dependency))
                {
                    throw new ConfigValidationException(
                        $"Module '{module.Name}' declares dependsOn '{dependency}', which is not a declared block.");
                }
            }
        }
    }

    private static JsonObject ExtractInputs(JsonObject block, IEnumerable<string> reservedKeys)
    {
        var inputs = (JsonObject)block.DeepClone();
        foreach (var key in reservedKeys)
        {
            inputs.Remove(key);
        }

        return inputs;
    }

    private static JsonObject RequireObject(JsonNode? node, string path) =>
        node as JsonObject
        ?? throw new ConfigValidationException($"'{path}' must be an object.");

    private static string RequireNonEmptyString(JsonObject obj, string key, string path)
    {
        var value = obj[key]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ConfigValidationException($"'{path}' is required and must be a non-empty string.");
        }

        return value;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AspireForm.Tests --filter "FullyQualifiedName~ConfigModelBinderTests"`
Expected: PASS — 9 tests (the `[Theory]` contributes 3 cases).

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Configuration/ConfigModelBinder.cs tests/AspireForm.Tests/Configuration/ConfigModelBinderTests.cs
git commit -m "feat: add config model binder with schema validation"
```

---

## Task 10: ConfigLoader — discovery and orchestration

**Files:**
- Create: `src/AspireForm/Configuration/ConfigLoader.cs`
- Test: `tests/AspireForm.Tests/Configuration/ConfigLoaderTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.Configuration;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Configuration;

public sealed class ConfigLoaderTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-test").FullName;
    private readonly ConfigLoader _loader = new();

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private void WriteFile(string name, string content) => File.WriteAllText(Path.Combine(_dir, name), content);

    [Fact]
    public void Loads_a_yaml_config()
    {
        WriteFile("aspireform.yaml", """
            aspireform:
              version: 1
              project: MyApp
              apphost: ./MyApp.AppHost
            """);

        var loaded = _loader.Load(_dir, env: null);

        loaded.Model.AspireForm.Project.Should().Be("MyApp");
    }

    [Fact]
    public void Layers_an_environment_override_file()
    {
        WriteFile("aspireform.yaml", """
            aspireform:
              version: 1
              project: MyApp
              apphost: ./MyApp.AppHost
            resources:
              sql:
                type: sqlserver
                aspireName: sql
            """);
        WriteFile("aspireform.dev.yaml", """
            resources:
              sql:
                aspireName: sql-dev
            """);

        var loaded = _loader.Load(_dir, env: "dev");

        loaded.Model.Resources["sql"].Inputs["aspireName"]!.GetValue<string>().Should().Be("sql-dev");
    }

    [Fact]
    public void Interpolates_variables_from_an_env_file()
    {
        WriteFile(".env", "PROJECT_NAME=FromEnvFile");
        WriteFile("aspireform.jsonc", """
            {
              "aspireform": { "version": 1, "project": "${PROJECT_NAME}", "apphost": "./X" }
            }
            """);

        var loaded = _loader.Load(_dir, env: null);

        loaded.Model.AspireForm.Project.Should().Be("FromEnvFile");
    }

    [Fact]
    public void Throws_when_no_config_file_is_found()
    {
        var act = () => _loader.Load(_dir, env: null);
        act.Should().Throw<ConfigValidationException>().WithMessage("*No AspireForm configuration*");
    }

    [Fact]
    public void Throws_when_multiple_base_config_files_are_present()
    {
        WriteFile("aspireform.yaml", "aspireform: { version: 1, project: A, apphost: ./A }");
        WriteFile("aspireform.jsonc", """{ "aspireform": { "version": 1, "project": "A", "apphost": "./A" } }""");

        var act = () => _loader.Load(_dir, env: null);
        act.Should().Throw<ConfigValidationException>().WithMessage("*Multiple*");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AspireForm.Tests --filter "FullyQualifiedName~ConfigLoaderTests"`
Expected: FAIL — `ConfigLoader` does not exist (compile error).

- [ ] **Step 3: Create `src/AspireForm/Configuration/ConfigLoader.cs`**

```csharp
using System.Text.Json.Nodes;

namespace AspireForm.Configuration;

/// <summary>The result of loading configuration: the resolved DOM and the bound model.</summary>
/// <param name="Resolved">The merged and interpolated configuration DOM.</param>
/// <param name="Model">The validated, bound project model.</param>
public sealed record LoadedConfig(JsonObject Resolved, ProjectModel Model);

/// <summary>Discovers, parses, layers, interpolates, and binds AspireForm configuration files.</summary>
public sealed class ConfigLoader
{
    private static readonly string[] BaseNames = ["aspireform.yaml", "aspireform.yml", "aspireform.jsonc", "aspireform.json"];

    /// <summary>
    /// Loads the configuration from <paramref name="projectDir"/>. When <paramref name="env"/> is supplied,
    /// an <c>aspireform.&lt;env&gt;.*</c> override file (if present) is deep-merged over the base.
    /// </summary>
    public LoadedConfig Load(string projectDir, string? env)
    {
        var basePath = FindBaseConfig(projectDir);
        var dom = ParseFile(basePath);

        if (env is not null)
        {
            var overridePath = FindOverrideConfig(projectDir, env);
            if (overridePath is not null)
            {
                dom = JsonObjectMerge.Merge(dom, ParseFile(overridePath));
            }
        }

        var envFile = EnvFile.Load(Path.Combine(projectDir, ".env"));
        var variables = Interpolator.BuildVariables(envFile);
        var resolved = Interpolator.Apply(dom, variables);

        var model = ConfigModelBinder.Bind(resolved);
        return new LoadedConfig(resolved, model);
    }

    private static string FindBaseConfig(string projectDir)
    {
        var present = BaseNames
            .Select(name => Path.Combine(projectDir, name))
            .Where(File.Exists)
            .ToList();

        return present switch
        {
            { Count: 0 } => throw new ConfigValidationException(
                $"No AspireForm configuration file found in '{projectDir}' (expected one of: {string.Join(", ", BaseNames)})."),
            { Count: > 1 } => throw new ConfigValidationException(
                $"Multiple AspireForm configuration files found in '{projectDir}': {string.Join(", ", present.Select(Path.GetFileName))}. Keep exactly one."),
            _ => present[0],
        };
    }

    private static string? FindOverrideConfig(string projectDir, string env)
    {
        string[] candidates =
        [
            $"aspireform.{env}.yaml", $"aspireform.{env}.yml",
            $"aspireform.{env}.jsonc", $"aspireform.{env}.json",
        ];

        return candidates
            .Select(name => Path.Combine(projectDir, name))
            .FirstOrDefault(File.Exists);
    }

    private static JsonObject ParseFile(string path)
    {
        var format = ConfigFormatDetector.FromPath(path)
            ?? throw new ConfigValidationException($"Unrecognized configuration file extension: '{path}'.");

        IConfigParser parser = format switch
        {
            ConfigFormat.Yaml => new YamlConfigParser(),
            ConfigFormat.Jsonc => new JsoncConfigParser(),
            _ => throw new ConfigValidationException($"Unsupported configuration format for '{path}'."),
        };

        return parser.Parse(File.ReadAllText(path));
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AspireForm.Tests --filter "FullyQualifiedName~ConfigLoaderTests"`
Expected: PASS — 5 tests.

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Configuration/ConfigLoader.cs tests/AspireForm.Tests/Configuration/ConfigLoaderTests.cs
git commit -m "feat: add ConfigLoader orchestrating discovery, layering, and binding"
```

---

## Task 11: State model and state store

**Files:**
- Create: `src/AspireForm/State/StateModel.cs`, `src/AspireForm/State/StateStore.cs`
- Test: `tests/AspireForm.Tests/State/StateStoreTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.State;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.State;

public sealed class StateStoreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-state-test").FullName;
    private readonly StateStore _store = new();

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Load_returns_empty_state_when_no_state_file_exists()
    {
        var state = _store.Load(_dir);
        state.Version.Should().Be(1);
        state.Blocks.Should().BeEmpty();
    }

    [Fact]
    public void Save_then_Load_round_trips_state()
    {
        var state = new AspireFormState();
        state.Blocks["sql"] = new BlockState
        {
            Type = "sqlserver",
            Kind = "resource",
            Files =
            {
                ["MyApp.AppHost/AppHost.cs"] = new FileState
                {
                    OwnershipMode = "managed",
                    Checksum = "abc123",
                },
            },
        };

        _store.Save(_dir, state);
        var reloaded = _store.Load(_dir);

        reloaded.Blocks.Should().ContainKey("sql");
        reloaded.Blocks["sql"].Type.Should().Be("sqlserver");
        reloaded.Blocks["sql"].Files["MyApp.AppHost/AppHost.cs"].OwnershipMode.Should().Be("managed");
        reloaded.Blocks["sql"].Files["MyApp.AppHost/AppHost.cs"].Checksum.Should().Be("abc123");
    }

    [Fact]
    public void Save_writes_to_the_dot_aspireform_directory()
    {
        _store.Save(_dir, new AspireFormState());
        File.Exists(Path.Combine(_dir, ".aspireform", "state.json")).Should().BeTrue();
    }

    [Fact]
    public void Load_throws_when_the_state_file_is_corrupt()
    {
        var stateDir = Directory.CreateDirectory(Path.Combine(_dir, ".aspireform"));
        File.WriteAllText(Path.Combine(stateDir.FullName, "state.json"), "{ not json");

        var act = () => _store.Load(_dir);
        act.Should().Throw<StateException>();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AspireForm.Tests --filter "FullyQualifiedName~StateStoreTests"`
Expected: FAIL — `AspireFormState` / `StateStore` do not exist (compile error).

- [ ] **Step 3: Create the state model and store**

`src/AspireForm/State/StateModel.cs`:

```csharp
namespace AspireForm.State;

/// <summary>The persisted last-known state of an AspireForm-managed project.</summary>
public sealed class AspireFormState
{
    /// <summary>The state-file schema version.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Tracked blocks, keyed by block name.</summary>
    public Dictionary<string, BlockState> Blocks { get; set; } = new();
}

/// <summary>The tracked state of a single resource or module block.</summary>
public sealed class BlockState
{
    /// <summary>The provider type, e.g. <c>sqlserver</c>.</summary>
    public required string Type { get; set; }

    /// <summary>The block kind: <c>resource</c> or <c>module</c>.</summary>
    public required string Kind { get; set; }

    /// <summary>Files emitted for this block, keyed by repo-relative path.</summary>
    public Dictionary<string, FileState> Files { get; set; } = new();
}

/// <summary>The tracked state of a single generated file.</summary>
public sealed class FileState
{
    /// <summary>The file's ownership mode: <c>managed</c>, <c>scaffold</c>, or <c>merge</c>.</summary>
    public required string OwnershipMode { get; set; }

    /// <summary>SHA-256 (hex) of the content AspireForm last generated for this file.</summary>
    public required string Checksum { get; set; }

    /// <summary>For <c>merge</c>-mode files: the last-generated content, used as the 3-way-merge baseline.</summary>
    public string? Baseline { get; set; }
}

/// <summary>Raised when the state file cannot be read or is corrupt.</summary>
public sealed class StateException : Exception
{
    /// <summary>Initializes the exception with a message and an inner cause.</summary>
    public StateException(string message, Exception inner) : base(message, inner) { }
}
```

`src/AspireForm/State/StateStore.cs`:

```csharp
using System.Text.Json;

namespace AspireForm.State;

/// <summary>Reads and writes the <c>.aspireform/state.json</c> file.</summary>
public sealed class StateStore
{
    private const string StateDirName = ".aspireform";
    private const string StateFileName = "state.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Loads state from <paramref name="projectDir"/>, returning a fresh empty state when no file exists.</summary>
    public AspireFormState Load(string projectDir)
    {
        var path = Path.Combine(projectDir, StateDirName, StateFileName);
        if (!File.Exists(path))
        {
            return new AspireFormState();
        }

        try
        {
            return JsonSerializer.Deserialize<AspireFormState>(File.ReadAllText(path), Options)
                ?? new AspireFormState();
        }
        catch (JsonException ex)
        {
            throw new StateException($"The AspireForm state file at '{path}' is corrupt.", ex);
        }
    }

    /// <summary>Writes <paramref name="state"/> to <c>.aspireform/state.json</c> under <paramref name="projectDir"/>.</summary>
    public void Save(string projectDir, AspireFormState state)
    {
        var stateDir = Path.Combine(projectDir, StateDirName);
        Directory.CreateDirectory(stateDir);
        File.WriteAllText(Path.Combine(stateDir, StateFileName), JsonSerializer.Serialize(state, Options));
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AspireForm.Tests --filter "FullyQualifiedName~StateStoreTests"`
Expected: PASS — 4 tests.

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/State/ tests/AspireForm.Tests/State/StateStoreTests.cs
git commit -m "feat: add state model and .aspireform/state.json store"
```

---

## Task 12: Minimal `IAspireCli` seam

This is the **only** place AspireForm shells out to the `aspire` CLI. Plan 3 expands the interface;
Plan 1 needs just availability and version detection for `doctor`.

**Files:**
- Create: `src/AspireForm/Aspire/IAspireCli.cs`, `src/AspireForm/Aspire/AspireCli.cs`
- Test: `tests/AspireForm.Tests/Aspire/AspireCliTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.Aspire;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Aspire;

public sealed class AspireCliTests
{
    [Fact]
    public async Task IsAvailableAsync_returns_false_when_the_executable_does_not_exist()
    {
        var cli = new AspireCli(executablePath: "definitely-not-a-real-command-xyz");
        (await cli.IsAvailableAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task GetVersionAsync_returns_null_when_the_executable_does_not_exist()
    {
        var cli = new AspireCli(executablePath: "definitely-not-a-real-command-xyz");
        (await cli.GetVersionAsync()).Should().BeNull();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AspireForm.Tests --filter "FullyQualifiedName~AspireCliTests"`
Expected: FAIL — `IAspireCli` / `AspireCli` do not exist (compile error).

- [ ] **Step 3: Create the interface and implementation**

`src/AspireForm/Aspire/IAspireCli.cs`:

```csharp
namespace AspireForm.Aspire;

/// <summary>The single seam through which AspireForm interacts with the official <c>aspire</c> CLI.</summary>
public interface IAspireCli
{
    /// <summary>Returns true when the <c>aspire</c> CLI can be invoked.</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the installed <c>aspire</c> CLI version string, or null when it is unavailable.</summary>
    Task<string?> GetVersionAsync(CancellationToken cancellationToken = default);
}
```

`src/AspireForm/Aspire/AspireCli.cs`:

```csharp
using System.Diagnostics;

namespace AspireForm.Aspire;

/// <summary>An <see cref="IAspireCli"/> that shells out to the <c>aspire</c> executable on PATH.</summary>
public sealed class AspireCli : IAspireCli
{
    private readonly string _executablePath;

    /// <summary>Initializes the CLI wrapper, optionally overriding the executable name (used by tests).</summary>
    public AspireCli(string executablePath = "aspire") => _executablePath = executablePath;

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
        await GetVersionAsync(cancellationToken) is not null;

    /// <inheritdoc />
    public async Task<string?> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var startInfo = new ProcessStartInfo(_executablePath, "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            return process.ExitCode == 0 ? output.Trim() : null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            // The executable is not installed or not on PATH.
            return null;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AspireForm.Tests --filter "FullyQualifiedName~AspireCliTests"`
Expected: PASS — 2 tests.

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Aspire/ tests/AspireForm.Tests/Aspire/AspireCliTests.cs
git commit -m "feat: add minimal IAspireCli seam for aspire CLI interaction"
```

---

## Task 13: Prerequisite checker

**Files:**
- Create: `src/AspireForm/Diagnostics/PrerequisiteReport.cs`, `src/AspireForm/Diagnostics/PrerequisiteChecker.cs`
- Test: `tests/AspireForm.Tests/Diagnostics/PrerequisiteCheckerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.Aspire;
using AspireForm.Diagnostics;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Diagnostics;

public sealed class PrerequisiteCheckerTests
{
    private sealed class FakeAspireCli(string? version) : IAspireCli
    {
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(version is not null);

        public Task<string?> GetVersionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(version);
    }

    [Fact]
    public async Task Reports_aspire_cli_as_ok_when_available()
    {
        var checker = new PrerequisiteChecker(new FakeAspireCli("13.3.4"));
        var report = await checker.RunAsync();

        var aspireCheck = report.Checks.Single(c => c.Name == "aspire CLI");
        aspireCheck.Ok.Should().BeTrue();
        aspireCheck.Detail.Should().Contain("13.3.4");
    }

    [Fact]
    public async Task Reports_aspire_cli_as_failed_with_a_remedy_when_missing()
    {
        var checker = new PrerequisiteChecker(new FakeAspireCli(version: null));
        var report = await checker.RunAsync();

        var aspireCheck = report.Checks.Single(c => c.Name == "aspire CLI");
        aspireCheck.Ok.Should().BeFalse();
        aspireCheck.Remedy.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Includes_a_dotnet_sdk_check()
    {
        var checker = new PrerequisiteChecker(new FakeAspireCli("13.3.4"));
        var report = await checker.RunAsync();
        report.Checks.Should().Contain(c => c.Name == ".NET SDK");
    }

    [Fact]
    public async Task AllPassed_is_false_when_any_required_check_fails()
    {
        var checker = new PrerequisiteChecker(new FakeAspireCli(version: null));
        var report = await checker.RunAsync();
        report.AllPassed.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AspireForm.Tests --filter "FullyQualifiedName~PrerequisiteCheckerTests"`
Expected: FAIL — `PrerequisiteChecker` / `PrerequisiteReport` do not exist (compile error).

- [ ] **Step 3: Create the report model and checker**

`src/AspireForm/Diagnostics/PrerequisiteReport.cs`:

```csharp
namespace AspireForm.Diagnostics;

/// <summary>The outcome of a single prerequisite check.</summary>
/// <param name="Name">The check's display name.</param>
/// <param name="Ok">True when the prerequisite is satisfied.</param>
/// <param name="Detail">A human-readable detail line (e.g. the detected version).</param>
/// <param name="Remedy">Guidance for fixing a failed check; null when <paramref name="Ok"/> is true.</param>
public sealed record PrerequisiteCheck(string Name, bool Ok, string Detail, string? Remedy);

/// <summary>The aggregate result of all prerequisite checks.</summary>
public sealed class PrerequisiteReport
{
    /// <summary>The individual check results.</summary>
    public required IReadOnlyList<PrerequisiteCheck> Checks { get; init; }

    /// <summary>True when every check passed.</summary>
    public bool AllPassed => Checks.All(c => c.Ok);
}
```

`src/AspireForm/Diagnostics/PrerequisiteChecker.cs`:

```csharp
using System.Diagnostics;
using AspireForm.Aspire;

namespace AspireForm.Diagnostics;

/// <summary>Checks that the prerequisites for running AspireForm are present.</summary>
public sealed class PrerequisiteChecker
{
    private const int MinimumDotnetMajorVersion = 10;

    private readonly IAspireCli _aspireCli;

    /// <summary>Initializes the checker with the <c>aspire</c> CLI seam.</summary>
    public PrerequisiteChecker(IAspireCli aspireCli) => _aspireCli = aspireCli;

    /// <summary>Runs every prerequisite check and returns the aggregate report.</summary>
    public async Task<PrerequisiteReport> RunAsync(CancellationToken cancellationToken = default)
    {
        var checks = new List<PrerequisiteCheck>
        {
            CheckDotnetSdk(),
            await CheckAspireCliAsync(cancellationToken),
        };

        return new PrerequisiteReport { Checks = checks };
    }

    private static PrerequisiteCheck CheckDotnetSdk()
    {
        var version = TryRun("dotnet", "--version");
        if (version is null)
        {
            return new PrerequisiteCheck(
                ".NET SDK", Ok: false,
                Detail: "The 'dotnet' command was not found.",
                Remedy: "Install the .NET 10 SDK from https://dotnet.microsoft.com/download.");
        }

        var major = ParseMajorVersion(version);
        var ok = major >= MinimumDotnetMajorVersion;
        return new PrerequisiteCheck(
            ".NET SDK", ok,
            Detail: $"Detected {version}.",
            Remedy: ok ? null : $"AspireForm requires the .NET {MinimumDotnetMajorVersion} SDK or later.");
    }

    private async Task<PrerequisiteCheck> CheckAspireCliAsync(CancellationToken cancellationToken)
    {
        var version = await _aspireCli.GetVersionAsync(cancellationToken);
        return version is not null
            ? new PrerequisiteCheck("aspire CLI", Ok: true, Detail: $"Detected {version}.", Remedy: null)
            : new PrerequisiteCheck(
                "aspire CLI", Ok: false,
                Detail: "The 'aspire' CLI was not found on PATH.",
                Remedy: "Install it with: dotnet tool install -g Aspire.Cli  (or run AspireForm anyway — "
                        + "it will fall back to 'dnx Aspire.Cli').");
    }

    private static int ParseMajorVersion(string version)
    {
        var firstSegment = version.Split('.', '-')[0];
        return int.TryParse(firstSegment, out var major) ? major : 0;
    }

    private static string? TryRun(string fileName, string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo(fileName, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output.Trim() : null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return null;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AspireForm.Tests --filter "FullyQualifiedName~PrerequisiteCheckerTests"`
Expected: PASS — 4 tests.

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Diagnostics/ tests/AspireForm.Tests/Diagnostics/PrerequisiteCheckerTests.cs
git commit -m "feat: add prerequisite checker for doctor"
```

---

## Task 14: `config` command

**Files:**
- Create: `src/AspireForm/Cli/ConfigCommand.cs`
- Modify: `src/AspireForm/Program.cs` (replace template content)
- Test: `tests/AspireForm.Tests/Cli/ConfigCommandTests.cs`

- [ ] **Step 1: Write `Program.cs` (the CLI host)**

Replace `src/AspireForm/Program.cs` entirely:

```csharp
using AspireForm.Cli;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("aspireform");

    config.AddCommand<ConfigCommand>("config")
        .WithAlias("show")
        .WithDescription("Print the fully merged and interpolated desired-state configuration.");

    config.AddCommand<DoctorCommand>("doctor")
        .WithDescription("Check that AspireForm's prerequisites are installed.");
});

return await app.RunAsync(args);
```

> `DoctorCommand` is referenced here but created in Task 15. Between Tasks 14 and 15 the project
> will not compile — that is expected; Task 15 completes the wiring. (If executing tasks strictly
> one-at-a-time with a green build between each, create a one-line `DoctorCommand` stub now
> returning `0`, and flesh it out in Task 15.)

- [ ] **Step 2: Write the failing test**

```csharp
using AspireForm.Cli;
using AwesomeAssertions;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace AspireForm.Tests.Cli;

public sealed class ConfigCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-config-cmd").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private CommandAppTester CreateApp()
    {
        var app = new CommandAppTester();
        app.SetDefaultCommand<ConfigCommand>();
        return app;
    }

    [Fact]
    public void Prints_resolved_config_and_exits_zero()
    {
        File.WriteAllText(Path.Combine(_dir, "aspireform.yaml"), """
            aspireform:
              version: 1
              project: MyApp
              apphost: ./MyApp.AppHost
            """);

        var result = CreateApp().Run("--project-dir", _dir);

        result.ExitCode.Should().Be(0);
        result.Output.Should().Contain("MyApp");
    }

    [Fact]
    public void Exits_nonzero_with_an_error_when_no_config_exists()
    {
        var result = CreateApp().Run("--project-dir", _dir);

        result.ExitCode.Should().Be(1);
        result.Output.Should().Contain("No AspireForm configuration");
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/AspireForm.Tests --filter "FullyQualifiedName~ConfigCommandTests"`
Expected: FAIL — `ConfigCommand` does not exist (compile error).

- [ ] **Step 4: Create `src/AspireForm/Cli/ConfigCommand.cs`**

```csharp
using System.ComponentModel;
using System.Text.Json;
using AspireForm.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>config</c> (alias <c>show</c>) command: prints the resolved desired-state configuration.</summary>
public sealed class ConfigCommand : Command<ConfigCommand.Settings>
{
    /// <summary>Options for the <c>config</c> command.</summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>The project directory containing the AspireForm configuration. Defaults to the current directory.</summary>
        [CommandOption("-p|--project-dir <DIR>")]
        [Description("The project directory containing the AspireForm configuration.")]
        public string ProjectDir { get; init; } = ".";

        /// <summary>An optional environment whose override file is layered over the base configuration.</summary>
        [CommandOption("-e|--env <ENV>")]
        [Description("Environment whose override file (aspireform.<env>.*) is layered over the base.")]
        public string? Env { get; init; }
    }

    private static readonly JsonSerializerOptions OutputOptions = new() { WriteIndented = true };

    /// <inheritdoc />
    public override int Execute(CommandContext context, Settings settings)
    {
        try
        {
            var loaded = new ConfigLoader().Load(Path.GetFullPath(settings.ProjectDir), settings.Env);
            AnsiConsole.WriteLine(loaded.Resolved.ToJsonString(OutputOptions));
            return 0;
        }
        catch (ConfigValidationException ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Configuration error:[/] {ex.Message}");
            return 1;
        }
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/AspireForm.Tests --filter "FullyQualifiedName~ConfigCommandTests"`
Expected: PASS — 2 tests. (Requires the `DoctorCommand` stub from Step 1's note, or proceed to Task 15 first.)

- [ ] **Step 6: Commit**

```bash
git add src/AspireForm/Program.cs src/AspireForm/Cli/ConfigCommand.cs tests/AspireForm.Tests/Cli/ConfigCommandTests.cs
git commit -m "feat: add config command and CLI host"
```

---

## Task 15: `doctor` command

**Files:**
- Create (or replace the stub): `src/AspireForm/Cli/DoctorCommand.cs`
- Test: `tests/AspireForm.Tests/Cli/DoctorCommandTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.Cli;
using AwesomeAssertions;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace AspireForm.Tests.Cli;

public sealed class DoctorCommandTests
{
    [Fact]
    public void Runs_and_prints_a_check_table()
    {
        var app = new CommandAppTester();
        app.SetDefaultCommand<DoctorCommand>();

        var result = app.Run();

        // The .NET SDK check always runs and is named in the output.
        result.Output.Should().Contain(".NET SDK");
        result.Output.Should().Contain("aspire CLI");
        // Exit code is 0 when all checks pass, 1 otherwise — both are valid; assert it is one of them.
        result.ExitCode.Should().BeOneOf(0, 1);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AspireForm.Tests --filter "FullyQualifiedName~DoctorCommandTests"`
Expected: FAIL — `DoctorCommand` does not exist, or the stub prints nothing.

- [ ] **Step 3: Create `src/AspireForm/Cli/DoctorCommand.cs`**

```csharp
using AspireForm.Aspire;
using AspireForm.Diagnostics;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>doctor</c> command: checks AspireForm's prerequisites and prints a report.</summary>
public sealed class DoctorCommand : AsyncCommand
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var report = await new PrerequisiteChecker(new AspireCli()).RunAsync();

        var table = new Table().AddColumns("Check", "Status", "Detail");
        foreach (var check in report.Checks)
        {
            var status = check.Ok ? "[green]OK[/]" : "[red]FAILED[/]";
            table.AddRow(Markup.Escape(check.Name), status, Markup.Escape(check.Detail));
        }

        AnsiConsole.Write(table);

        foreach (var failed in report.Checks.Where(c => !c.Ok && c.Remedy is not null))
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]{failed.Name}:[/] {failed.Remedy}");
        }

        return report.AllPassed ? 0 : 1;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AspireForm.Tests --filter "FullyQualifiedName~DoctorCommandTests"`
Expected: PASS — 1 test. Then run the full suite: `dotnet test` — all tests green.

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Cli/DoctorCommand.cs tests/AspireForm.Tests/Cli/DoctorCommandTests.cs
git commit -m "feat: add doctor command"
```

---

## Task 16: End-to-end smoke verification

Confirms the built tool runs the real commands against a real fixture, and that it packs as a tool.

**Files:**
- Create: `examples/sample/aspireform.yaml`, `examples/sample/aspireform.dev.yaml`
- Test: `tests/AspireForm.Tests/EndToEnd/CliSmokeTests.cs`

- [ ] **Step 1: Create the fixture files**

`examples/sample/aspireform.yaml`:

```yaml
aspireform:
  version: 1
  project: SampleApp
  apphost: ./SampleApp.AppHost
resources:
  sql:
    type: sqlserver
    aspireName: sql
    databases: [appdb]
modules:
  data:
    type: ef-data
    dependsOn: [sql]
    database: appdb
    contextName: AppDbContext
```

`examples/sample/aspireform.dev.yaml`:

```yaml
resources:
  sql:
    aspireName: sql-dev
```

- [ ] **Step 2: Write the failing test**

```csharp
using System.Diagnostics;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.EndToEnd;

/// <summary>Builds and runs the real AspireForm tool against the sample fixture.</summary>
public sealed class CliSmokeTests
{
    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "AspireForm.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException("Could not locate the repository root.");
    }

    private static (int ExitCode, string Output) RunTool(params string[] args)
    {
        var root = RepoRoot();
        var allArgs = new List<string> { "run", "--project", Path.Combine(root, "src", "AspireForm"), "--" };
        allArgs.AddRange(args);

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = root,
        };
        foreach (var arg in allArgs)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }

    [Fact]
    public void Config_command_prints_the_sample_project()
    {
        var sampleDir = Path.Combine(RepoRoot(), "examples", "sample");
        var (exitCode, output) = RunTool("config", "--project-dir", sampleDir);

        exitCode.Should().Be(0);
        output.Should().Contain("SampleApp");
    }

    [Fact]
    public void Config_command_applies_the_dev_override()
    {
        var sampleDir = Path.Combine(RepoRoot(), "examples", "sample");
        var (exitCode, output) = RunTool("config", "--project-dir", sampleDir, "--env", "dev");

        exitCode.Should().Be(0);
        output.Should().Contain("sql-dev");
    }

    [Fact]
    public void Doctor_command_runs()
    {
        var (exitCode, output) = RunTool("doctor");

        exitCode.Should().BeOneOf(0, 1);
        output.Should().Contain(".NET SDK");
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/AspireForm.Tests --filter "FullyQualifiedName~CliSmokeTests"`
Expected: FAIL — the `examples/sample` directory is not found, or output assertions fail before the fixture exists. (If Steps 1 ran first, the failure is whichever assertion is not yet satisfied.)

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AspireForm.Tests --filter "FullyQualifiedName~CliSmokeTests"`
Expected: PASS — 3 tests.

- [ ] **Step 5: Verify the tool packs and run the full suite**

```bash
dotnet pack src/AspireForm/AspireForm.csproj -o ./artifacts
dotnet test
```

Expected: `./artifacts/AspireForm.0.1.0.nupkg` produced; the entire test suite is green.

- [ ] **Step 6: Commit**

```bash
git add examples/ tests/AspireForm.Tests/EndToEnd/CliSmokeTests.cs
git commit -m "test: add end-to-end CLI smoke tests with sample fixture"
```

---

## Plan 1 — Definition of done

- `dnx`-packable tool (`AspireForm.0.1.0.nupkg`, `PackageType=DotnetTool`).
- `aspireform config [--project-dir DIR] [--env ENV]` prints the merged, interpolated desired state; exits non-zero with a clear message on a config error.
- `aspireform doctor` reports the .NET SDK and `aspire` CLI checks with remedies.
- Config pipeline: YAML and JSONC normalize to one DOM (parity tested); override layering, `${VAR}` interpolation (`.env` + process env, process wins), and schema validation all covered by unit tests.
- State store reads/writes `.aspireform/state.json` with a round-trip test (consumed by Plan 2).
- `IAspireCli` seam established (expanded in Plan 3).
- Entire test suite green on xUnit v3 / MTP.

---

## Self-review notes

- **Spec coverage:** §3 components — CLI host (T14), config layer (T3–T10), state store (T11), `IAspireCli` adapter (T12, minimal per advisor); provider registry, planner, executor are out of Plan 1 scope (Plans 2–3). §4 config layer fully covered (T3–T10). §7 state store covered (T11). §9 `config` + `doctor` rows covered (T14–T15); other verbs are Plans 2–3. §10 `IAspireCli` minimal seam covered (T12).
- **Advisor gaps folded in:** minimal `IAspireCli` (T12); explicit validation boundary — references checked, cycles deferred (T9); `.env` as its own task (T7) with precedence test (T8); override-merge edge cases — null-removes, absent-preserves, empty-sequence-replaces (T6); `.gitattributes` (T1); pinned versions throughout; YAML converter given disproportionate detail (T5).
- **Placeholder scan:** none — every step has concrete code or commands. The `DoctorCommand` forward-reference in T14 is explicitly called out with a stub instruction.
- **Type consistency:** `LoadedConfig(Resolved, Model)`, `ConfigLoader.Load(projectDir, env)`, `AspireFormState.Blocks`, `BlockState.Files`, `FileState.OwnershipMode/Checksum/Baseline`, `IAspireCli.IsAvailableAsync/GetVersionAsync`, `PrerequisiteCheck(Name, Ok, Detail, Remedy)`, `PrerequisiteReport.Checks/AllPassed` — names are consistent across all tasks that reference them.
