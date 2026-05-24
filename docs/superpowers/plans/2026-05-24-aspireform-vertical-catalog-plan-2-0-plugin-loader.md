# AspireForm Vertical Catalog — Plan 2.0: NuGet plugin loader + AspireForm.Plugin.Redis

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `AspireForm 0.3.0` with a working external plugin loader (NuGet packages only — `.cs` scripts are Plan 2.0.5) plus the first dogfooded vertical plugin, `AspireForm.Plugin.Redis 0.1.0`. Both packages publish to NuGet via the existing release workflow extended to recognise `plugin/<Name>/v<version>` tags.

**Architecture:** A new `Plugins/` namespace inside the main AspireForm package: `PluginManifest` (JSON contract), `PluginRestorer` (shells out to `dotnet restore`; locates assemblies in the global NuGet cache), `PluginAssemblyLoader` (custom `AssemblyLoadContext`), `PluginLockfile` (`.aspireform/plugins.lock.yaml`), `PluginManager` (orchestrator). `ConfigLoader` is **unchanged**; CLI commands invoke `PluginManager.DiscoverAndLoad` between load and plan, producing an enriched `ProviderRegistry` for the `Planner`. The Redis plugin is a `net10.0` class library with `<PackageType>AspireFormPlugin</PackageType>`, an embedded `aspireform-plugin.json`, and a `RedisResourceProvider` implementing `IProvider`.

**Tech Stack:** Inherits prior plans — .NET 10 (`net10.0`, SDK 10.0.300), `Spectre.Console.Cli` 0.55.0, `YamlDotNet` 18.0.0, xUnit v3 3.2.2 on Microsoft Testing Platform, `AwesomeAssertions` 9.4.0. **No new package dependencies** — shell-out to `dotnet restore` avoids embedding `NuGet.Protocol`.

**Spec:** `docs/superpowers/specs/2026-05-24-aspireform-vertical-catalog-design.md` — this plan implements §3 (plugin loader) and the first row of §4 (Redis as the first dogfooded vertical).

**Plan position:** Plan 2.0 of 10 in sub-project #2. Plan 2.0.5 adds `.cs`-script plugin support; Plans 2.1–2.9 are the remaining nine vertical plugins.

---

## Conventions for the executor

- **Solo dev workflow** — work in-place on `main`; no feature branch.
- **Assertions:** `AwesomeAssertions` (`value.Should()....`). Never `Assert.*`.
- **XML docs:** every public type and member gets at least a one-line `/// <summary>`.
- **Run tests:** `dotnet run --project tests/AspireForm.Tests --configuration Debug` is authoritative. `dotnet test` works but is flaky on this Windows setup.
- **Spectre commands:** in Spectre.Console.Cli 0.55.0 the abstract sync `Command<T>.Execute` requires a `CancellationToken` overload. Plan 3's pattern was to use `AsyncCommand<T>` with `Task.FromResult` for synchronous bodies — keep that convention.
- All paths relative to `c:/Development/AspireForm`.

---

## Locked technical decisions (verified empirically before writing)

1. **NuGet restore strategy: shell out to `dotnet restore`** (NOT in-process `NuGet.Protocol`). Avoids embedding ~12 NuGet packages into the main AspireForm tool; eliminates SDK-version-conflict risk; gets transitive resolution for free. Verified: `dotnet restore` of a throwaway csproj places packages at `~/.nuget/packages/<id-lower>/<version>/lib/<tfm>/`.
2. **Plugin discovery primary mechanism: `<packageTypes><packageType name="AspireFormPlugin" /></packageTypes>`** in the nuspec, set via `<PackageType>AspireFormPlugin</PackageType>` in the plugin csproj. Verified: MSBuild emits this exactly to the nuspec on `dotnet pack`. Name-pattern (`AspireForm.Plugin.*`) is fallback #2; explicit lockfile entry is fallback #3.
3. **AssemblyLoadContext: not collectible.** Plugins remain loaded for the lifetime of the AspireForm invocation. Documented limitation: `plugin remove` clears the cache but doesn't unload an already-loaded assembly until next AspireForm run.
4. **Plugin manifest location: package root.** `<Content Include="aspireform-plugin.json" Pack="true" PackagePath="\" />` in the plugin csproj puts the file at `~/.nuget/packages/<id>/<version>/aspireform-plugin.json` after restore.
5. **Integration test approach (advisor's option b):** the plugin-loaded e2e test packs `AspireForm.Plugin.Redis` to a temp dir, configures a temporary `NuGet.config` with that dir as a local feed, then runs the real loader against a fixture. Tests the full path; slow but trustworthy.
6. **Auto-restore wiring point:** not inside `ConfigLoader` (which stays pure). A new `PluginManager.DiscoverAndLoad(model, projectDir)` runs **between** `ConfigLoader.Load` and `Planner.Plan` in each command (`plan`, `apply`, etc.), producing an enriched `ProviderRegistry` for that invocation.

---

## File structure (locked)

```
src/AspireForm/
  Plugins/                              NEW
    PluginManifest.cs                   manifest types + JSON deserialization
    PluginContractException.cs          raised on incompat or load failure
    PluginRestorer.cs                   shell-out to dotnet restore + cache locate
    PluginAssemblyLoader.cs             AssemblyLoadContext + manifest -> provider instantiation
    PluginLockfile.cs                   .aspireform/plugins.lock.yaml read/write
    PluginManager.cs                    orchestrator: DiscoverAndLoad(model, projectDir)
  Providers/ProviderRegistry.cs         MODIFY: add Combine(...) + AllProviders()
  Cli/
    PluginListCommand.cs                NEW
    PluginInstallCommand.cs             NEW
    PluginUpdateCommand.cs              NEW
    PluginRemoveCommand.cs              NEW
    ApplyCommand.cs                     MODIFY: insert PluginManager.DiscoverAndLoad before planner
    DestroyCommand.cs                   MODIFY: same
    PlanCommand.cs                      MODIFY: same
    ImportCommand.cs                    MODIFY: same (so import knows about plugin providers)
  Program.cs                            MODIFY: register `plugin` branch
  AspireForm.csproj                     MODIFY: <Version>0.3.0</Version>

src/Plugins/AspireForm.Plugin.Redis/    NEW
  AspireForm.Plugin.Redis.csproj        net10.0 class lib; PackAsAspireFormPlugin
  aspireform-plugin.json                manifest
  RedisResourceProvider.cs              IProvider impl
  README.md                             plugin-local readme
  CHANGELOG.md                          plugin-local changelog

tests/AspireForm.Tests/
  Plugins/PluginManifestTests.cs
  Plugins/PluginRestorerTests.cs
  Plugins/PluginAssemblyLoaderTests.cs
  Plugins/PluginLockfileTests.cs
  Plugins/PluginManagerTests.cs
  Cli/PluginListCommandTests.cs
  Cli/PluginInstallCommandTests.cs
  Cli/PluginUpdateCommandTests.cs
  Cli/PluginRemoveCommandTests.cs
  EndToEnd/PluginLoaderE2ETests.cs      packs Redis -> temp feed -> loads -> plans

tests/Plugins/AspireForm.Plugin.Redis.Tests/   NEW
  AspireForm.Plugin.Redis.Tests.csproj
  RedisResourceProviderTests.cs

.github/workflows/release.yml           MODIFY: handle plugin/<Name>/v<version> tags
.github/workflows/ci.yml                NEW: build+test all on push/PR

README.md                               MODIFY: Plugins section + plugin commands
CHANGELOG.md                            MODIFY: [0.3.0] section
AspireForm.slnx                         MODIFY: add the two new csproj
```

---

## Task 1: Mono-repo restructure + solution wiring

Create the `src/Plugins/` and `tests/Plugins/` directories with placeholder `.gitkeep` files; later tasks add real csproj files. Verify the existing solution still builds.

**Files:**
- Create: `src/Plugins/.gitkeep`, `tests/Plugins/.gitkeep`

- [ ] **Step 1: Create the directories**

```bash
cd c:/Development/AspireForm
mkdir -p src/Plugins tests/Plugins
touch src/Plugins/.gitkeep tests/Plugins/.gitkeep
```

- [ ] **Step 2: Build to confirm nothing broke**

```bash
dotnet build
```

Expected: build succeeds (zero errors).

- [ ] **Step 3: Commit**

```bash
git add src/Plugins/.gitkeep tests/Plugins/.gitkeep
git commit -m "chore: reserve src/Plugins/ and tests/Plugins/ for plugin packages"
```

---

## Task 2: PluginManifest types + JSON deserialization

**Files:**
- Create: `src/AspireForm/Plugins/PluginManifest.cs`
- Test: `tests/AspireForm.Tests/Plugins/PluginManifestTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Text.Json;
using AspireForm.Plugins;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Plugins;

public sealed class PluginManifestTests
{
    [Fact]
    public void Parses_a_well_formed_manifest()
    {
        const string json = """
            {
              "name": "Redis",
              "version": "0.1.0",
              "minAspireFormVersion": "0.3.0",
              "providers": [
                { "type": "redis", "kind": "resource", "className": "AspireForm.Plugin.Redis.RedisResourceProvider" }
              ]
            }
            """;

        var manifest = PluginManifest.Parse(json);

        manifest.Name.Should().Be("Redis");
        manifest.Version.Should().Be("0.1.0");
        manifest.MinAspireFormVersion.Should().Be("0.3.0");
        manifest.Providers.Should().ContainSingle();
        manifest.Providers[0].Type.Should().Be("redis");
        manifest.Providers[0].Kind.Should().Be("resource");
        manifest.Providers[0].ClassName.Should().Be("AspireForm.Plugin.Redis.RedisResourceProvider");
    }

    [Fact]
    public void Throws_PluginContractException_on_malformed_json()
    {
        var act = () => PluginManifest.Parse("{ not json");
        act.Should().Throw<PluginContractException>();
    }

    [Fact]
    public void Throws_PluginContractException_when_a_required_field_is_missing()
    {
        const string missingName = """
            { "version": "0.1.0", "minAspireFormVersion": "0.3.0", "providers": [] }
            """;
        var act = () => PluginManifest.Parse(missingName);
        act.Should().Throw<PluginContractException>().WithMessage("*name*");
    }
}
```

- [ ] **Step 2: Run test to verify it fails (types do not exist)**

Run: `dotnet run --project tests/AspireForm.Tests`
Expected: build error — `PluginManifest`, `PluginContractException` undefined.

- [ ] **Step 3: Create `src/AspireForm/Plugins/PluginContractException.cs`**

```csharp
namespace AspireForm.Plugins;

/// <summary>Raised when a plugin's manifest is malformed, incompatible, or fails to load.</summary>
public sealed class PluginContractException : Exception
{
    /// <summary>Initialises the exception with a human-readable message.</summary>
    public PluginContractException(string message) : base(message) { }

    /// <summary>Initialises the exception with a message and an inner cause.</summary>
    public PluginContractException(string message, Exception inner) : base(message, inner) { }
}
```

- [ ] **Step 4: Create `src/AspireForm/Plugins/PluginManifest.cs`**

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AspireForm.Plugins;

/// <summary>One provider entry inside a plugin manifest.</summary>
/// <param name="Type">The provider's block type (e.g. <c>redis</c>).</param>
/// <param name="Kind">Either <c>resource</c> or <c>module</c>.</param>
/// <param name="ClassName">Fully-qualified type name of the <see cref="Providers.IProvider"/> implementation inside the plugin assembly.</param>
public sealed record PluginManifestProvider(string Type, string Kind, string ClassName);

/// <summary>The contract a plugin package publishes via its embedded <c>aspireform-plugin.json</c>.</summary>
public sealed class PluginManifest
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>The plugin's display name (also used in the lockfile and CLI).</summary>
    public required string Name { get; init; }

    /// <summary>The plugin's package version (informational; the NuGet package version is authoritative).</summary>
    public required string Version { get; init; }

    /// <summary>The minimum AspireForm version this plugin requires (SemVer).</summary>
    public required string MinAspireFormVersion { get; init; }

    /// <summary>The providers this plugin contributes.</summary>
    public required IReadOnlyList<PluginManifestProvider> Providers { get; init; }

    /// <summary>The assembly name to load (without <c>.dll</c>); defaults to <c>AspireForm.Plugin.&lt;Name&gt;</c> when omitted.</summary>
    public string? AssemblyName { get; init; }

    /// <summary>Parses a manifest JSON document. Throws <see cref="PluginContractException"/> on any issue.</summary>
    public static PluginManifest Parse(string json)
    {
        PluginManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<PluginManifest>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new PluginContractException($"Plugin manifest is not valid JSON: {ex.Message}", ex);
        }

        if (manifest is null)
        {
            throw new PluginContractException("Plugin manifest is empty.");
        }

        Validate(manifest);
        return manifest;
    }

    private static void Validate(PluginManifest m)
    {
        if (string.IsNullOrWhiteSpace(m.Name))
        {
            throw new PluginContractException("Plugin manifest is missing the required 'name' field.");
        }

        if (string.IsNullOrWhiteSpace(m.Version))
        {
            throw new PluginContractException("Plugin manifest is missing the required 'version' field.");
        }

        if (string.IsNullOrWhiteSpace(m.MinAspireFormVersion))
        {
            throw new PluginContractException("Plugin manifest is missing the required 'minAspireFormVersion' field.");
        }

        if (m.Providers is null)
        {
            throw new PluginContractException("Plugin manifest is missing the required 'providers' field.");
        }
    }
}
```

Note: STJ's source-generated deserialization for `required` properties checks presence — a missing `name` produces a JsonException, which we wrap and rethrow. The explicit `Validate` call handles the edge case where JSON has `"name": ""`.

- [ ] **Step 5: Run tests (3 new tests; total = 146)**

- [ ] **Step 6: Commit**

```bash
git add src/AspireForm/Plugins/ tests/AspireForm.Tests/Plugins/PluginManifestTests.cs
git commit -m "feat: add PluginManifest types and JSON deserialization"
```

---

## Task 3: ProviderRegistry — add Combine and AllProviders

The existing `ProviderRegistry` is constructed from a fixed list. To support plugin providers, callers need to produce a new registry from `Default() + pluginProviders`. Add helpers without breaking the existing API.

**Files:**
- Modify: `src/AspireForm/Providers/ProviderRegistry.cs`
- Test: extend `tests/AspireForm.Tests/Providers/ProviderRegistryTests.cs` with two new cases

- [ ] **Step 1: Write the failing test (append to existing file)**

Add these two `[Fact]`s at the end of the existing `ProviderRegistryTests` class:

```csharp
    [Fact]
    public void AllProviders_returns_every_registered_provider()
    {
        var registry = new ProviderRegistry([new FakeProvider("a"), new FakeProvider("b")]);
        registry.AllProviders().Select(p => p.Type).Should().BeEquivalentTo(["a", "b"]);
    }

    [Fact]
    public void Combine_builds_a_new_registry_from_two_provider_lists()
    {
        var defaults = ProviderRegistry.Default();
        var plugins = new IProvider[] { new FakeProvider("custom") };

        var combined = ProviderRegistry.Combine(defaults.AllProviders(), plugins);

        combined.Get("sqlserver").Should().NotBeNull();
        combined.Get("custom").Type.Should().Be("custom");
    }
```

- [ ] **Step 2: Run test to verify it fails (AllProviders and Combine missing)**

- [ ] **Step 3: Modify `src/AspireForm/Providers/ProviderRegistry.cs`**

Add the two new members to the existing class. Locate the class body and add (preserving everything that's there):

```csharp
    /// <summary>Returns every provider registered with this registry.</summary>
    public IEnumerable<IProvider> AllProviders() => _byType.Values;

    /// <summary>Builds a new registry that contains every provider from each source. Throws on duplicate types.</summary>
    public static ProviderRegistry Combine(params IEnumerable<IProvider>[] sources) =>
        new(sources.SelectMany(s => s));
```

- [ ] **Step 4: Run tests (2 new tests; total = 148)**

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Providers/ProviderRegistry.cs tests/AspireForm.Tests/Providers/ProviderRegistryTests.cs
git commit -m "feat: add ProviderRegistry.Combine and AllProviders for plugin composition"
```

---

## Task 4: PluginLockfile (.aspireform/plugins.lock.yaml)

**Files:**
- Create: `src/AspireForm/Plugins/PluginLockfile.cs`
- Test: `tests/AspireForm.Tests/Plugins/PluginLockfileTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.Plugins;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Plugins;

public sealed class PluginLockfileTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-pluginlock").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Load_returns_empty_lockfile_when_file_is_absent()
    {
        var lockfile = PluginLockfile.Load(_dir);
        lockfile.Plugins.Should().BeEmpty();
    }

    [Fact]
    public void Save_then_Load_round_trips_plugin_entries()
    {
        var lockfile = new PluginLockfile();
        lockfile.Plugins.Add(new PluginLockEntry
        {
            Name = "Redis",
            Package = "AspireForm.Plugin.Redis",
            Version = "0.1.0",
            Source = "https://api.nuget.org/v3/index.json",
        });

        PluginLockfile.Save(_dir, lockfile);
        var reloaded = PluginLockfile.Load(_dir);

        reloaded.Plugins.Should().ContainSingle();
        reloaded.Plugins[0].Name.Should().Be("Redis");
        reloaded.Plugins[0].Package.Should().Be("AspireForm.Plugin.Redis");
        reloaded.Plugins[0].Version.Should().Be("0.1.0");
    }

    [Fact]
    public void Save_writes_to_the_dot_aspireform_directory()
    {
        PluginLockfile.Save(_dir, new PluginLockfile());
        File.Exists(Path.Combine(_dir, ".aspireform", "plugins.lock.yaml")).Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Create `src/AspireForm/Plugins/PluginLockfile.cs`**

```csharp
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AspireForm.Plugins;

/// <summary>One entry in the plugin lockfile.</summary>
public sealed class PluginLockEntry
{
    /// <summary>The plugin's display name (matches the manifest).</summary>
    public required string Name { get; set; }

    /// <summary>The NuGet package id.</summary>
    public required string Package { get; set; }

    /// <summary>The pinned package version.</summary>
    public required string Version { get; set; }

    /// <summary>The NuGet feed source the plugin was restored from.</summary>
    public required string Source { get; set; }
}

/// <summary>The persisted set of plugins this project has resolved. Committed to git.</summary>
public sealed class PluginLockfile
{
    /// <summary>The lockfile schema version.</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>The locked plugins, ordered by name.</summary>
    public List<PluginLockEntry> Plugins { get; set; } = [];

    private const string DirName = ".aspireform";
    private const string FileName = "plugins.lock.yaml";

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>Loads the lockfile from <paramref name="projectDir"/>, returning an empty instance when no file exists.</summary>
    public static PluginLockfile Load(string projectDir)
    {
        var path = Path.Combine(projectDir, DirName, FileName);
        if (!File.Exists(path))
        {
            return new PluginLockfile();
        }

        return Deserializer.Deserialize<PluginLockfile>(File.ReadAllText(path)) ?? new PluginLockfile();
    }

    /// <summary>Writes <paramref name="lockfile"/> to <c>.aspireform/plugins.lock.yaml</c> under <paramref name="projectDir"/>.</summary>
    public static void Save(string projectDir, PluginLockfile lockfile)
    {
        var lockDir = Path.Combine(projectDir, DirName);
        Directory.CreateDirectory(lockDir);
        File.WriteAllText(Path.Combine(lockDir, FileName), Serializer.Serialize(lockfile));
    }
}
```

- [ ] **Step 4: Run tests (3 new tests; total = 151)**

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Plugins/PluginLockfile.cs tests/AspireForm.Tests/Plugins/PluginLockfileTests.cs
git commit -m "feat: add PluginLockfile (.aspireform/plugins.lock.yaml)"
```

---

## Task 5: PluginRestorer (shell-out to `dotnet restore`)

The most novel task. Builds a temporary csproj referencing the target plugin package, runs `dotnet restore`, then locates the assembly + manifest in the global NuGet cache.

**Files:**
- Create: `src/AspireForm/Plugins/PluginRestorer.cs`
- Test: `tests/AspireForm.Tests/Plugins/PluginRestorerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.Plugins;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Plugins;

public sealed class PluginRestorerTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-plugin-restore").FullName;
    private readonly PluginRestorer _restorer = new();

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task RestoreAsync_returns_path_to_restored_package_when_package_exists()
    {
        // Use a tiny stable package; Newtonsoft.Json 13.0.3 is universally cached on .NET dev machines.
        var result = await _restorer.RestoreAsync(
            packageId: "Newtonsoft.Json", version: "13.0.3", workingDirectory: _dir);

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.PackageDirectory.Should().NotBeNull();
        Directory.Exists(result.PackageDirectory).Should().BeTrue();
        // Cache layout: <package-dir>/lib/<tfm>/<assembly>.dll
        Directory.GetFiles(Path.Combine(result.PackageDirectory!, "lib"), "Newtonsoft.Json.dll",
            SearchOption.AllDirectories).Should().NotBeEmpty();
    }

    [Fact]
    public async Task RestoreAsync_reports_failure_for_a_nonexistent_package()
    {
        var result = await _restorer.RestoreAsync(
            packageId: "This.Package.Does.Not.Exist.AspireForm.Test", version: "0.0.1", workingDirectory: _dir);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetGlobalPackagesPath_returns_a_real_directory()
    {
        var path = PluginRestorer.GetGlobalPackagesPath();
        Directory.Exists(path).Should().BeTrue($"global packages path '{path}' should exist on a dev machine");
    }
}
```

> The first test exercises a real `dotnet restore` against NuGet.org — it requires network and takes a few seconds. Acceptable cost for a plugin loader test.

- [ ] **Step 2: Run test to verify it fails (compile error)**

- [ ] **Step 3: Create `src/AspireForm/Plugins/PluginRestorer.cs`**

```csharp
using System.Diagnostics;

namespace AspireForm.Plugins;

/// <summary>The outcome of a plugin restore attempt.</summary>
/// <param name="Success">True when the package was restored and located.</param>
/// <param name="PackageDirectory">Absolute path to the restored package root (containing lib/, aspireform-plugin.json, etc.); null on failure.</param>
/// <param name="ErrorMessage">A human-readable error description when <paramref name="Success"/> is false.</param>
public sealed record PluginRestoreResult(bool Success, string? PackageDirectory, string? ErrorMessage);

/// <summary>
/// Restores a plugin package by shelling out to <c>dotnet restore</c> against a temporary csproj,
/// then locates the package in the global NuGet cache.
/// Avoids embedding the NuGet client library; lets the SDK handle dependency resolution.
/// </summary>
public sealed class PluginRestorer
{
    /// <summary>Returns the global NuGet packages directory (honours <c>NUGET_PACKAGES</c> env var, falls back to the standard location).</summary>
    public static string GetGlobalPackagesPath()
    {
        var env = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrEmpty(env))
        {
            return env;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".nuget", "packages");
    }

    /// <summary>Restores <paramref name="packageId"/>@<paramref name="version"/> via <c>dotnet restore</c> and returns the path to the cached package directory.</summary>
    public async Task<PluginRestoreResult> RestoreAsync(
        string packageId,
        string version,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        var probeDir = Path.Combine(workingDirectory, ".aspireform", "restore-probes",
            $"{packageId}-{version}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(probeDir);

        var csprojPath = Path.Combine(probeDir, "Probe.csproj");
        await File.WriteAllTextAsync(csprojPath, $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="{{packageId}}" Version="{{version}}" />
              </ItemGroup>
            </Project>
            """, cancellationToken);

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = probeDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("restore");
        startInfo.ArgumentList.Add("--nologo");

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new PluginRestoreResult(false, null, "Failed to start 'dotnet'.");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                var msg = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                return new PluginRestoreResult(false, null, $"dotnet restore exited with {process.ExitCode}: {msg.Trim()}");
            }
        }
        finally
        {
            // Best-effort cleanup of the probe directory.
            try { Directory.Delete(probeDir, recursive: true); } catch { /* ignore */ }
        }

        // Locate the package in the global cache: <globalPackages>/<id-lower>/<version>/
        var packageDir = Path.Combine(GetGlobalPackagesPath(), packageId.ToLowerInvariant(), version);
        if (!Directory.Exists(packageDir))
        {
            return new PluginRestoreResult(false, null,
                $"Restore reported success but package directory '{packageDir}' was not found.");
        }

        return new PluginRestoreResult(true, packageDir, null);
    }
}
```

- [ ] **Step 4: Run tests (3 new tests; total = 154)**

The first test requires network for NuGet.org. If running offline, expect that one test to fail; others should pass.

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Plugins/PluginRestorer.cs tests/AspireForm.Tests/Plugins/PluginRestorerTests.cs
git commit -m "feat: add PluginRestorer (shell-out to dotnet restore + cache locate)"
```

---

## Task 6: PluginAssemblyLoader

Given a restored package directory and a `PluginManifest`, load the assembly into an isolated `AssemblyLoadContext` and instantiate the declared `IProvider` types.

**Files:**
- Create: `src/AspireForm/Plugins/PluginAssemblyLoader.cs`
- Test: `tests/AspireForm.Tests/Plugins/PluginAssemblyLoaderTests.cs`

- [ ] **Step 1: Write the failing test**

The test needs a real assembly to load. We synthesize one inside the test by compiling a tiny snippet via Roslyn (the test is the one place we use Roslyn — the production code path doesn't, since `.cs`-script support is Plan 2.0.5).

```csharp
using System.Reflection;
using AspireForm.Plugins;
using AspireForm.Providers;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace AspireForm.Tests.Plugins;

public sealed class PluginAssemblyLoaderTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-plugin-load").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void LoadProviders_returns_provider_instances_for_each_manifest_entry()
    {
        var assemblyPath = SynthesizeTestPluginAssembly(_dir, "FakePlugin",
            providerClassName: "FakePlugin.FakeProvider", providerType: "fake-type", kind: "resource");
        var packageDir = Path.GetDirectoryName(assemblyPath)!;
        WriteManifest(packageDir, "Fake", providerType: "fake-type",
            className: "FakePlugin.FakeProvider", assemblyName: "FakePlugin");

        var manifest = PluginManifest.Parse(File.ReadAllText(Path.Combine(packageDir, "aspireform-plugin.json")));

        var loader = new PluginAssemblyLoader();
        var providers = loader.LoadProviders(packageDir, manifest);

        providers.Should().ContainSingle();
        providers[0].Type.Should().Be("fake-type");
        providers[0].Kind.Should().Be(BlockKind.Resource);
    }

    [Fact]
    public void LoadProviders_throws_PluginContractException_when_the_named_class_is_absent()
    {
        var assemblyPath = SynthesizeTestPluginAssembly(_dir, "EmptyPlugin",
            providerClassName: "EmptyPlugin.RealClass", providerType: "ignored", kind: "resource");
        var packageDir = Path.GetDirectoryName(assemblyPath)!;
        WriteManifest(packageDir, "Empty", providerType: "x",
            className: "EmptyPlugin.NoSuchClass", assemblyName: "EmptyPlugin");

        var manifest = PluginManifest.Parse(File.ReadAllText(Path.Combine(packageDir, "aspireform-plugin.json")));

        var loader = new PluginAssemblyLoader();
        var act = () => loader.LoadProviders(packageDir, manifest);
        act.Should().Throw<PluginContractException>().WithMessage("*NoSuchClass*");
    }

    private static string SynthesizeTestPluginAssembly(
        string dir, string assemblyName, string providerClassName, string providerType, string kind)
    {
        var libDir = Path.Combine(dir, assemblyName, "lib", "net10.0");
        Directory.CreateDirectory(libDir);

        var (ns, cls) = providerClassName.Contains('.')
            ? (providerClassName[..providerClassName.LastIndexOf('.')], providerClassName[(providerClassName.LastIndexOf('.') + 1)..])
            : ("", providerClassName);

        var source = $$"""
            using AspireForm.Providers;
            using System.Text.Json.Nodes;

            namespace {{ns}};

            public sealed class {{cls}} : IProvider
            {
                public string Type => "{{providerType}}";
                public BlockKind Kind => BlockKind.{{(kind == "module" ? "Module" : "Resource")}};
                public ProviderPlan Plan(PlanContext context) => new();
            }
            """;

        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        var syntax = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees: [syntax],
            references: refs,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var assemblyPath = Path.Combine(libDir, $"{assemblyName}.dll");
        using var stream = File.Create(assemblyPath);
        var emit = compilation.Emit(stream);
        if (!emit.Success)
        {
            var diagnostics = string.Join("\n", emit.Diagnostics.Select(d => d.ToString()));
            throw new InvalidOperationException("Failed to compile test plugin:\n" + diagnostics);
        }
        return assemblyPath;
    }

    private static void WriteManifest(string packageDir, string name, string providerType, string className, string assemblyName)
    {
        File.WriteAllText(Path.Combine(packageDir, "aspireform-plugin.json"), $$"""
            {
              "name": "{{name}}",
              "version": "0.1.0",
              "minAspireFormVersion": "0.3.0",
              "assemblyName": "{{assemblyName}}",
              "providers": [
                { "type": "{{providerType}}", "kind": "resource", "className": "{{className}}" }
              ]
            }
            """);
    }
}
```

This test pulls Roslyn into the **test project** to synthesize a plugin assembly for the load test. The production `PluginAssemblyLoader` does NOT use Roslyn.

- [ ] **Step 2: Add Roslyn to the test csproj (test-only dep)**

```bash
dotnet add tests/AspireForm.Tests/AspireForm.Tests.csproj package Microsoft.CodeAnalysis.CSharp --version 4.13.0
```

- [ ] **Step 3: Run test to verify it fails (PluginAssemblyLoader does not exist)**

- [ ] **Step 4: Create `src/AspireForm/Plugins/PluginAssemblyLoader.cs`**

```csharp
using System.Reflection;
using System.Runtime.Loader;
using AspireForm.Providers;

namespace AspireForm.Plugins;

/// <summary>Loads a plugin assembly into an isolated <see cref="AssemblyLoadContext"/> and instantiates its declared providers.</summary>
public sealed class PluginAssemblyLoader
{
    private static readonly AssemblyLoadContext Context = new("AspireFormPlugins", isCollectible: false);

    /// <summary>
    /// Loads the plugin assembly from <paramref name="packageDirectory"/> (a NuGet cache directory containing
    /// <c>lib/&lt;tfm&gt;/&lt;assembly&gt;.dll</c>) and returns instances of the providers declared in
    /// <paramref name="manifest"/>.
    /// </summary>
    public IReadOnlyList<IProvider> LoadProviders(string packageDirectory, PluginManifest manifest)
    {
        var assemblyName = manifest.AssemblyName ?? $"AspireForm.Plugin.{manifest.Name}";
        var assemblyPath = LocateAssembly(packageDirectory, assemblyName)
            ?? throw new PluginContractException(
                $"Plugin '{manifest.Name}': could not locate '{assemblyName}.dll' under '{packageDirectory}/lib/'.");

        Assembly assembly;
        try
        {
            assembly = Context.LoadFromAssemblyPath(assemblyPath);
        }
        catch (Exception ex)
        {
            throw new PluginContractException(
                $"Plugin '{manifest.Name}': failed to load assembly '{assemblyPath}': {ex.Message}", ex);
        }

        var providers = new List<IProvider>(manifest.Providers.Count);
        foreach (var entry in manifest.Providers)
        {
            var type = assembly.GetType(entry.ClassName, throwOnError: false);
            if (type is null)
            {
                throw new PluginContractException(
                    $"Plugin '{manifest.Name}': declared provider class '{entry.ClassName}' was not found in '{assemblyName}'.");
            }

            if (!typeof(IProvider).IsAssignableFrom(type))
            {
                throw new PluginContractException(
                    $"Plugin '{manifest.Name}': class '{entry.ClassName}' does not implement IProvider.");
            }

            var instance = (IProvider)(Activator.CreateInstance(type)
                ?? throw new PluginContractException(
                    $"Plugin '{manifest.Name}': failed to instantiate '{entry.ClassName}'."));
            providers.Add(instance);
        }

        return providers;
    }

    private static string? LocateAssembly(string packageDirectory, string assemblyName)
    {
        var libDir = Path.Combine(packageDirectory, "lib");
        if (!Directory.Exists(libDir))
        {
            return null;
        }

        // Prefer net10.0; fall back to any other TFM the package ships.
        var fileName = $"{assemblyName}.dll";
        var preferred = Path.Combine(libDir, "net10.0", fileName);
        if (File.Exists(preferred))
        {
            return preferred;
        }

        return Directory.GetFiles(libDir, fileName, SearchOption.AllDirectories).FirstOrDefault();
    }
}
```

- [ ] **Step 5: Run tests (2 new tests; total = 156)**

- [ ] **Step 6: Commit**

```bash
git add src/AspireForm/Plugins/PluginAssemblyLoader.cs tests/AspireForm.Tests/Plugins/PluginAssemblyLoaderTests.cs tests/AspireForm.Tests/AspireForm.Tests.csproj
git commit -m "feat: add PluginAssemblyLoader (AssemblyLoadContext + provider instantiation)"
```

---

## Task 7: PluginManager (orchestrator)

The public entry point. Given a `ProjectModel` and `projectDir`:
1. Reads the lockfile.
2. Identifies unknown block types in the model.
3. For each, restores the plugin (via `PluginRestorer`), reads its `aspireform-plugin.json`, loads providers (via `PluginAssemblyLoader`), records in lockfile.
4. Returns an enriched `ProviderRegistry`.
5. Checks contract version compatibility; throws on mismatch.

**Files:**
- Create: `src/AspireForm/Plugins/PluginManager.cs`
- Test: `tests/AspireForm.Tests/Plugins/PluginManagerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Text.Json.Nodes;
using AspireForm.Configuration;
using AspireForm.Plugins;
using AspireForm.Providers;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Plugins;

public sealed class PluginManagerTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-pluginmgr").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task DiscoverAndLoadAsync_returns_only_builtin_providers_when_model_uses_only_builtin_types()
    {
        var model = new ProjectModel
        {
            AspireForm = new AspireFormHeader { Version = 1, Project = "X", AppHost = "X.AppHost" },
            Resources = new Dictionary<string, ResourceBlock>
            {
                ["sql"] = new() { Name = "sql", Type = "sqlserver", Inputs = new JsonObject() },
            },
        };

        var manager = new PluginManager();
        var registry = await manager.DiscoverAndLoadAsync(model, _dir);

        registry.Get("sqlserver").Should().NotBeNull();
        // No lockfile written because no plugin was needed.
        File.Exists(Path.Combine(_dir, ".aspireform", "plugins.lock.yaml")).Should().BeFalse();
    }

    [Fact]
    public async Task DiscoverAndLoadAsync_uses_lockfile_entries_when_present()
    {
        // Pre-seed lockfile with a fake plugin entry. PluginManager should try to load it via the restorer
        // (which will fail because the package doesn't exist), but the unknown-type isn't in the model so
        // the entry should be honoured without triggering a restore.
        var lockfile = new PluginLockfile();
        lockfile.Plugins.Add(new PluginLockEntry
        {
            Name = "Unused", Package = "AspireForm.Plugin.Unused", Version = "0.0.0",
            Source = "https://api.nuget.org/v3/index.json",
        });
        PluginLockfile.Save(_dir, lockfile);

        var model = new ProjectModel
        {
            AspireForm = new AspireFormHeader { Version = 1, Project = "X", AppHost = "X.AppHost" },
        };

        var manager = new PluginManager();
        var registry = await manager.DiscoverAndLoadAsync(model, _dir);

        // Built-ins still present.
        registry.Get("sqlserver").Should().NotBeNull();
    }
}
```

> Note: the test does NOT cover the auto-restore-of-unknown-type happy path here. That happens in the end-to-end integration test (Task 14) which packs a real plugin to a temp NuGet feed. PluginManager's unit tests cover orchestration without exercising real NuGet.

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Create `src/AspireForm/Plugins/PluginManager.cs`**

```csharp
using System.Reflection;
using AspireForm.Configuration;
using AspireForm.Providers;

namespace AspireForm.Plugins;

/// <summary>
/// Orchestrates plugin discovery, restore, and load. Run between <see cref="ConfigLoader.Load"/> and the
/// planner: produces a <see cref="ProviderRegistry"/> enriched with discovered plugin providers.
/// </summary>
public sealed class PluginManager
{
    private readonly PluginRestorer _restorer;
    private readonly PluginAssemblyLoader _loader;

    /// <summary>Initialises the manager with default restorer and loader implementations.</summary>
    public PluginManager()
    {
        _restorer = new PluginRestorer();
        _loader = new PluginAssemblyLoader();
    }

    /// <summary>
    /// Walks <paramref name="model"/> for block types unknown to the built-in registry; resolves each
    /// against the lockfile (or restores from NuGet if absent); loads and instantiates providers; updates
    /// the lockfile; returns a <see cref="ProviderRegistry"/> combining built-ins with loaded plugins.
    /// </summary>
    public async Task<ProviderRegistry> DiscoverAndLoadAsync(
        ProjectModel model, string projectDir, CancellationToken cancellationToken = default)
    {
        var builtIn = ProviderRegistry.Default();
        var knownTypes = builtIn.AllProviders().Select(p => p.Type).ToHashSet(StringComparer.Ordinal);

        var unknownTypes = model.Resources.Values.Select(r => r.Type)
            .Concat(model.Modules.Values.Select(m => m.Type))
            .Where(t => !knownTypes.Contains(t))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (unknownTypes.Count == 0)
        {
            return builtIn;
        }

        var lockfile = PluginLockfile.Load(projectDir);
        var pluginProviders = new List<IProvider>();

        foreach (var type in unknownTypes)
        {
            var entry = lockfile.Plugins.FirstOrDefault(p =>
                ResolvePackageId(p.Name).Equals(ResolvePackageIdFromType(type), StringComparison.OrdinalIgnoreCase))
                ?? await ResolveAndLockAsync(type, lockfile, projectDir, cancellationToken);

            var packageDir = Path.Combine(PluginRestorer.GetGlobalPackagesPath(),
                entry.Package.ToLowerInvariant(), entry.Version);

            if (!Directory.Exists(packageDir))
            {
                // Lockfile entry exists but package not in cache; restore it.
                var result = await _restorer.RestoreAsync(entry.Package, entry.Version, projectDir, cancellationToken);
                if (!result.Success)
                {
                    throw new PluginContractException(
                        $"Plugin '{entry.Name}' ({entry.Package} {entry.Version}) could not be restored: {result.ErrorMessage}");
                }

                packageDir = result.PackageDirectory!;
            }

            var manifestPath = Path.Combine(packageDir, "aspireform-plugin.json");
            if (!File.Exists(manifestPath))
            {
                throw new PluginContractException(
                    $"Plugin '{entry.Name}' is missing 'aspireform-plugin.json' at the package root.");
            }

            var manifest = PluginManifest.Parse(File.ReadAllText(manifestPath));
            CheckContractCompatibility(manifest);
            pluginProviders.AddRange(_loader.LoadProviders(packageDir, manifest));
        }

        PluginLockfile.Save(projectDir, lockfile);
        return ProviderRegistry.Combine(builtIn.AllProviders(), pluginProviders);
    }

    private async Task<PluginLockEntry> ResolveAndLockAsync(
        string type, PluginLockfile lockfile, string projectDir, CancellationToken cancellationToken)
    {
        var packageId = ResolvePackageIdFromType(type);

        // Restore the latest version (use floating "*" which dotnet restore resolves to the latest stable).
        var result = await _restorer.RestoreAsync(packageId, "*", projectDir, cancellationToken);
        if (!result.Success)
        {
            throw new PluginContractException(
                $"No plugin found for block type '{type}'. Tried package id '{packageId}': {result.ErrorMessage}");
        }

        // The restored directory's parent name is the resolved version.
        var resolvedVersion = new DirectoryInfo(result.PackageDirectory!).Name;
        var displayName = packageId.StartsWith("AspireForm.Plugin.", StringComparison.OrdinalIgnoreCase)
            ? packageId["AspireForm.Plugin.".Length..]
            : packageId;

        var entry = new PluginLockEntry
        {
            Name = displayName,
            Package = packageId,
            Version = resolvedVersion,
            Source = "https://api.nuget.org/v3/index.json",
        };

        lockfile.Plugins.Add(entry);
        lockfile.Plugins.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return entry;
    }

    private static string ResolvePackageIdFromType(string type)
    {
        // Default convention: type 'foo' -> package 'AspireForm.Plugin.Foo' (PascalCase the type name).
        var pascal = string.Concat(type.Split('-').Select(p => p.Length > 0 ? char.ToUpperInvariant(p[0]) + p[1..] : p));
        return $"AspireForm.Plugin.{pascal}";
    }

    private static string ResolvePackageId(string name) =>
        name.StartsWith("AspireForm.Plugin.", StringComparison.OrdinalIgnoreCase)
            ? name
            : $"AspireForm.Plugin.{name}";

    private static void CheckContractCompatibility(PluginManifest manifest)
    {
        var running = typeof(PluginManager).Assembly.GetName().Version
            ?? throw new InvalidOperationException("Cannot determine running AspireForm version.");

        if (!Version.TryParse(manifest.MinAspireFormVersion, out var min))
        {
            throw new PluginContractException(
                $"Plugin '{manifest.Name}' declares an unparseable minAspireFormVersion '{manifest.MinAspireFormVersion}'.");
        }

        // Compare only major.minor (drop build/revision).
        var runningMm = new Version(running.Major, running.Minor);
        var minMm = new Version(min.Major, min.Minor);
        if (runningMm < minMm)
        {
            throw new PluginContractException(
                $"Plugin '{manifest.Name}' requires AspireForm >= {manifest.MinAspireFormVersion}; running {running}.");
        }
    }
}
```

- [ ] **Step 4: Run tests (2 new tests; total = 158)**

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Plugins/PluginManager.cs tests/AspireForm.Tests/Plugins/PluginManagerTests.cs
git commit -m "feat: add PluginManager orchestrating discovery, restore, and load"
```

---

## Task 8: Wire PluginManager into plan / apply / destroy / import commands

The existing commands construct `new Planner(ProviderRegistry.Default())`. Replace each call with `new Planner(await new PluginManager().DiscoverAndLoadAsync(loaded.Model, projectDir, cancellationToken))`.

**Files:**
- Modify: `src/AspireForm/Cli/PlanCommand.cs`, `src/AspireForm/Cli/ApplyCommand.cs`, `src/AspireForm/Cli/DestroyCommand.cs`, `src/AspireForm/Cli/ImportCommand.cs`

- [ ] **Step 1: Modify `src/AspireForm/Cli/PlanCommand.cs`**

Read the current file. Replace the line `var plan = new Planner(ProviderRegistry.Default()).Plan(...);` with:

```csharp
            var registry = await new AspireForm.Plugins.PluginManager().DiscoverAndLoadAsync(loaded.Model, projectDir, cancellationToken);
            var plan = new Planner(registry).Plan(loaded.Model, state, projectDir);
```

Convert `PlanCommand` to `AsyncCommand<Settings>` (currently sync) so `await` works. Change the base class + signature accordingly. Add `catch (PluginContractException ex)` returning `Fail("Plugin error", ex)`.

- [ ] **Step 2: Apply equivalent changes to ApplyCommand.cs, DestroyCommand.cs, ImportCommand.cs**

Each command:
1. Already async (or becomes async).
2. After loading config + state, call `PluginManager.DiscoverAndLoadAsync` to get the enriched registry.
3. Pass that registry to `Planner` instead of `ProviderRegistry.Default()`.
4. Add `PluginContractException` catch returning exit 1.

- [ ] **Step 3: Run the full test suite**

```bash
dotnet run --project tests/AspireForm.Tests
```

Expected: all 158 tests still pass. Existing command tests use only built-in types, so `PluginManager.DiscoverAndLoadAsync` short-circuits to the built-in registry without touching disk.

- [ ] **Step 4: Commit**

```bash
git add src/AspireForm/Cli/
git commit -m "feat: wire PluginManager into plan/apply/destroy/import commands"
```

---

## Task 9: `plugin list` command

**Files:**
- Create: `src/AspireForm/Cli/PluginListCommand.cs`
- Test: `tests/AspireForm.Tests/Cli/PluginListCommandTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.Cli;
using AspireForm.Plugins;
using AwesomeAssertions;
using Spectre.Console.Cli;
using Xunit;

namespace AspireForm.Tests.Cli;

[Collection(nameof(ConsoleCaptureCollection))]
public sealed class PluginListCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-plugin-list").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private (int ExitCode, string StdOut, string StdErr) RunList(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        Console.SetOut(stdout);
        Console.SetError(stderr);
        try
        {
            var app = new CommandApp();
            app.Configure(c => c.AddCommand<PluginListCommand>("list"));
            return (app.Run(["list", .. args]), stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void Plugin_list_reports_no_plugins_when_lockfile_is_empty()
    {
        var (exitCode, stdout, _) = RunList("--project-dir", _dir);
        exitCode.Should().Be(0);
        stdout.Should().Contain("No plugins");
    }

    [Fact]
    public void Plugin_list_prints_each_locked_plugin_with_its_version()
    {
        var lockfile = new PluginLockfile();
        lockfile.Plugins.Add(new PluginLockEntry
        {
            Name = "Redis", Package = "AspireForm.Plugin.Redis", Version = "0.1.0",
            Source = "https://api.nuget.org/v3/index.json",
        });
        PluginLockfile.Save(_dir, lockfile);

        var (exitCode, stdout, _) = RunList("--project-dir", _dir);
        exitCode.Should().Be(0);
        stdout.Should().Contain("Redis").And.Contain("0.1.0");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Create `src/AspireForm/Cli/PluginListCommand.cs`**

```csharp
using System.ComponentModel;
using System.Text;
using AspireForm.Plugins;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>plugin list</c> command: prints every plugin recorded in the lockfile.</summary>
public sealed class PluginListCommand : Command<PluginListCommand.Settings>
{
    /// <summary>Options for <c>plugin list</c>.</summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>The project directory.</summary>
        [CommandOption("-p|--project-dir <DIR>")]
        [Description("The project directory.")]
        public string ProjectDir { get; init; } = ".";
    }

    /// <inheritdoc />
    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var lockfile = PluginLockfile.Load(Path.GetFullPath(settings.ProjectDir));
        if (lockfile.Plugins.Count == 0)
        {
            Console.Out.WriteLine("No plugins installed.");
            return 0;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Name         Package                                     Version");
        sb.AppendLine("----         -------                                     -------");
        foreach (var p in lockfile.Plugins)
        {
            sb.AppendLine($"{p.Name,-12} {p.Package,-43} {p.Version}");
        }

        Console.Out.Write(sb.ToString());
        return 0;
    }
}
```

- [ ] **Step 4: Run tests (2 new tests; total = 160)**

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Cli/PluginListCommand.cs tests/AspireForm.Tests/Cli/PluginListCommandTests.cs
git commit -m "feat: add plugin list command"
```

---

## Task 10: `plugin install <name>[@version]` command

**Files:**
- Create: `src/AspireForm/Cli/PluginInstallCommand.cs`
- Test: `tests/AspireForm.Tests/Cli/PluginInstallCommandTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.Cli;
using AspireForm.Plugins;
using AwesomeAssertions;
using Spectre.Console.Cli;
using Xunit;

namespace AspireForm.Tests.Cli;

[Collection(nameof(ConsoleCaptureCollection))]
public sealed class PluginInstallCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-plugin-install").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private (int ExitCode, string StdOut, string StdErr) RunInstall(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        Console.SetOut(stdout);
        Console.SetError(stderr);
        try
        {
            var app = new CommandApp();
            app.Configure(c => c.AddCommand<PluginInstallCommand>("install"));
            return (app.Run(["install", .. args]), stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public async Task Plugin_install_fails_for_nonexistent_package()
    {
        var (exitCode, _, stderr) = RunInstall("This.Does.Not.Exist.AspireForm.Test", "--project-dir", _dir);
        exitCode.Should().Be(1);
        stderr.Should().NotBeNullOrEmpty();
        await Task.CompletedTask;
    }
}
```

(The happy-path install is covered in the e2e Task 14, which has a real plugin packed to a temp feed.)

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Create `src/AspireForm/Cli/PluginInstallCommand.cs`**

```csharp
using System.ComponentModel;
using AspireForm.Plugins;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>plugin install</c> command: explicit install of a plugin by package id, with optional version pin.</summary>
public sealed class PluginInstallCommand : AsyncCommand<PluginInstallCommand.Settings>
{
    /// <summary>Options for <c>plugin install</c>.</summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>The plugin name or package id (optionally <c>name@version</c>).</summary>
        [CommandArgument(0, "<NAME>")]
        [Description("Plugin name or package id (e.g. 'Redis' or 'AspireForm.Plugin.Redis@0.1.0').")]
        public required string Name { get; init; }

        /// <summary>The project directory.</summary>
        [CommandOption("-p|--project-dir <DIR>")]
        [Description("The project directory.")]
        public string ProjectDir { get; init; } = ".";
    }

    /// <inheritdoc />
    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var projectDir = Path.GetFullPath(settings.ProjectDir);
        var (packageId, version) = ParseNameAndVersion(settings.Name);

        var restorer = new PluginRestorer();
        var result = await restorer.RestoreAsync(packageId, version, projectDir, cancellationToken);
        if (!result.Success)
        {
            Console.Error.WriteLine($"Plugin install error: {result.ErrorMessage}");
            return 1;
        }

        var resolvedVersion = new DirectoryInfo(result.PackageDirectory!).Name;
        var displayName = packageId.StartsWith("AspireForm.Plugin.", StringComparison.OrdinalIgnoreCase)
            ? packageId["AspireForm.Plugin.".Length..]
            : packageId;

        var lockfile = PluginLockfile.Load(projectDir);
        lockfile.Plugins.RemoveAll(p =>
            string.Equals(p.Package, packageId, StringComparison.OrdinalIgnoreCase));
        lockfile.Plugins.Add(new PluginLockEntry
        {
            Name = displayName,
            Package = packageId,
            Version = resolvedVersion,
            Source = "https://api.nuget.org/v3/index.json",
        });
        lockfile.Plugins.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        PluginLockfile.Save(projectDir, lockfile);

        Console.Out.WriteLine($"Installed {displayName} ({packageId} {resolvedVersion}).");
        return 0;
    }

    private static (string PackageId, string Version) ParseNameAndVersion(string input)
    {
        var at = input.IndexOf('@');
        var packageId = at < 0 ? input : input[..at];
        var version = at < 0 ? "*" : input[(at + 1)..];

        if (!packageId.Contains('.'))
        {
            packageId = $"AspireForm.Plugin.{packageId}";
        }

        return (packageId, version);
    }
}
```

- [ ] **Step 4: Run tests (1 new test; total = 161)**

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Cli/PluginInstallCommand.cs tests/AspireForm.Tests/Cli/PluginInstallCommandTests.cs
git commit -m "feat: add plugin install command"
```

---

## Task 11: `plugin update <name>` command

**Files:**
- Create: `src/AspireForm/Cli/PluginUpdateCommand.cs`
- Test: `tests/AspireForm.Tests/Cli/PluginUpdateCommandTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.Cli;
using AspireForm.Plugins;
using AwesomeAssertions;
using Spectre.Console.Cli;
using Xunit;

namespace AspireForm.Tests.Cli;

[Collection(nameof(ConsoleCaptureCollection))]
public sealed class PluginUpdateCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-plugin-update").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private (int ExitCode, string StdOut, string StdErr) RunUpdate(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        Console.SetOut(stdout);
        Console.SetError(stderr);
        try
        {
            var app = new CommandApp();
            app.Configure(c => c.AddCommand<PluginUpdateCommand>("update"));
            return (app.Run(["update", .. args]), stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void Plugin_update_refuses_unknown_plugin()
    {
        var (exitCode, _, stderr) = RunUpdate("Ghost", "--project-dir", _dir);
        exitCode.Should().Be(1);
        stderr.Should().Contain("Ghost").And.Contain("not installed");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Create `src/AspireForm/Cli/PluginUpdateCommand.cs`**

```csharp
using System.ComponentModel;
using AspireForm.Plugins;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>plugin update</c> command: re-resolves the latest version of an installed plugin and updates the lockfile.</summary>
public sealed class PluginUpdateCommand : AsyncCommand<PluginUpdateCommand.Settings>
{
    /// <summary>Options for <c>plugin update</c>.</summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>The plugin name.</summary>
        [CommandArgument(0, "<NAME>")]
        [Description("Plugin name (as recorded in the lockfile).")]
        public required string Name { get; init; }

        /// <summary>The project directory.</summary>
        [CommandOption("-p|--project-dir <DIR>")]
        [Description("The project directory.")]
        public string ProjectDir { get; init; } = ".";
    }

    /// <inheritdoc />
    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var projectDir = Path.GetFullPath(settings.ProjectDir);
        var lockfile = PluginLockfile.Load(projectDir);

        var entry = lockfile.Plugins.FirstOrDefault(p =>
            string.Equals(p.Name, settings.Name, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            Console.Error.WriteLine($"Plugin '{settings.Name}' is not installed.");
            return 1;
        }

        var restorer = new PluginRestorer();
        var result = await restorer.RestoreAsync(entry.Package, "*", projectDir, cancellationToken);
        if (!result.Success)
        {
            Console.Error.WriteLine($"Plugin update error: {result.ErrorMessage}");
            return 1;
        }

        var newVersion = new DirectoryInfo(result.PackageDirectory!).Name;
        var oldVersion = entry.Version;
        entry.Version = newVersion;
        PluginLockfile.Save(projectDir, lockfile);

        Console.Out.WriteLine(
            string.Equals(oldVersion, newVersion, StringComparison.Ordinal)
                ? $"{entry.Name} already at {newVersion}."
                : $"Updated {entry.Name}: {oldVersion} -> {newVersion}.");
        return 0;
    }
}
```

- [ ] **Step 4: Run tests (1 new test; total = 162)**

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Cli/PluginUpdateCommand.cs tests/AspireForm.Tests/Cli/PluginUpdateCommandTests.cs
git commit -m "feat: add plugin update command"
```

---

## Task 12: `plugin remove <name>` command + Program.cs wiring

**Files:**
- Create: `src/AspireForm/Cli/PluginRemoveCommand.cs`
- Modify: `src/AspireForm/Program.cs` — register the `plugin` branch
- Test: `tests/AspireForm.Tests/Cli/PluginRemoveCommandTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AspireForm.Cli;
using AspireForm.Plugins;
using AwesomeAssertions;
using Spectre.Console.Cli;
using Xunit;

namespace AspireForm.Tests.Cli;

[Collection(nameof(ConsoleCaptureCollection))]
public sealed class PluginRemoveCommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-plugin-remove").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private (int ExitCode, string StdOut, string StdErr) RunRemove(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        Console.SetOut(stdout);
        Console.SetError(stderr);
        try
        {
            var app = new CommandApp();
            app.Configure(c => c.AddCommand<PluginRemoveCommand>("remove"));
            return (app.Run(["remove", .. args]), stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void Plugin_remove_drops_the_lockfile_entry()
    {
        var lockfile = new PluginLockfile();
        lockfile.Plugins.Add(new PluginLockEntry
        {
            Name = "Redis", Package = "AspireForm.Plugin.Redis", Version = "0.1.0",
            Source = "https://api.nuget.org/v3/index.json",
        });
        PluginLockfile.Save(_dir, lockfile);

        var (exitCode, stdout, _) = RunRemove("Redis", "--project-dir", _dir);

        exitCode.Should().Be(0);
        stdout.Should().Contain("Removed");
        PluginLockfile.Load(_dir).Plugins.Should().BeEmpty();
    }

    [Fact]
    public void Plugin_remove_refuses_unknown_plugin()
    {
        var (exitCode, _, stderr) = RunRemove("Ghost", "--project-dir", _dir);
        exitCode.Should().Be(1);
        stderr.Should().Contain("Ghost");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Create `src/AspireForm/Cli/PluginRemoveCommand.cs`**

```csharp
using System.ComponentModel;
using AspireForm.Plugins;
using Spectre.Console.Cli;

namespace AspireForm.Cli;

/// <summary>The <c>plugin remove</c> command: drops a plugin from the lockfile.</summary>
public sealed class PluginRemoveCommand : Command<PluginRemoveCommand.Settings>
{
    /// <summary>Options for <c>plugin remove</c>.</summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>The plugin name to remove.</summary>
        [CommandArgument(0, "<NAME>")]
        [Description("Plugin name (as recorded in the lockfile).")]
        public required string Name { get; init; }

        /// <summary>The project directory.</summary>
        [CommandOption("-p|--project-dir <DIR>")]
        [Description("The project directory.")]
        public string ProjectDir { get; init; } = ".";
    }

    /// <inheritdoc />
    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var projectDir = Path.GetFullPath(settings.ProjectDir);
        var lockfile = PluginLockfile.Load(projectDir);

        var removed = lockfile.Plugins.RemoveAll(p =>
            string.Equals(p.Name, settings.Name, StringComparison.OrdinalIgnoreCase));

        if (removed == 0)
        {
            Console.Error.WriteLine($"Plugin '{settings.Name}' is not installed.");
            return 1;
        }

        PluginLockfile.Save(projectDir, lockfile);
        Console.Out.WriteLine($"Removed plugin '{settings.Name}' from the lockfile. Already-loaded plugins remain active until next run.");
        return 0;
    }
}
```

- [ ] **Step 4: Modify `src/AspireForm/Program.cs`**

Add a `plugin` branch alongside the existing `state` branch:

```csharp
    config.AddBranch("plugin", plugin =>
    {
        plugin.SetDescription("Manage AspireForm plugins (NuGet plugin packages).");
        plugin.AddCommand<PluginListCommand>("list")
            .WithDescription("List installed plugins.");
        plugin.AddCommand<PluginInstallCommand>("install")
            .WithDescription("Install a plugin by name or package id.");
        plugin.AddCommand<PluginUpdateCommand>("update")
            .WithDescription("Update an installed plugin to the latest version.");
        plugin.AddCommand<PluginRemoveCommand>("remove")
            .WithDescription("Remove a plugin from the lockfile.");
    });
```

- [ ] **Step 5: Run tests (2 new tests; total = 164)**

- [ ] **Step 6: Commit**

```bash
git add src/AspireForm/Cli/PluginRemoveCommand.cs src/AspireForm/Program.cs tests/AspireForm.Tests/Cli/PluginRemoveCommandTests.cs
git commit -m "feat: add plugin remove command and wire plugin branch into Program.cs"
```

---

## Task 13: AspireForm.Plugin.Redis package

**Files:**
- Create: `src/Plugins/AspireForm.Plugin.Redis/AspireForm.Plugin.Redis.csproj`
- Create: `src/Plugins/AspireForm.Plugin.Redis/aspireform-plugin.json`
- Create: `src/Plugins/AspireForm.Plugin.Redis/RedisResourceProvider.cs`
- Create: `src/Plugins/AspireForm.Plugin.Redis/README.md`
- Create: `src/Plugins/AspireForm.Plugin.Redis/CHANGELOG.md`
- Create: `tests/Plugins/AspireForm.Plugin.Redis.Tests/AspireForm.Plugin.Redis.Tests.csproj`
- Create: `tests/Plugins/AspireForm.Plugin.Redis.Tests/RedisResourceProviderTests.cs`

- [ ] **Step 1: Create the plugin csproj**

`src/Plugins/AspireForm.Plugin.Redis/AspireForm.Plugin.Redis.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>AspireForm.Plugin.Redis</RootNamespace>
    <AssemblyName>AspireForm.Plugin.Redis</AssemblyName>

    <PackageId>AspireForm.Plugin.Redis</PackageId>
    <Version>0.1.0</Version>
    <Authors>James Burton</Authors>
    <Description>AspireForm plugin: Redis resource provider — emits 'aspire add redis' and the AppHost wiring.</Description>
    <PackageProjectUrl>https://github.com/jamesburton/AspireForm</PackageProjectUrl>
    <RepositoryUrl>https://github.com/jamesburton/AspireForm</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageType>AspireFormPlugin</PackageType>
    <PackageTags>aspireform;aspireform-plugin;redis;aspire</PackageTags>
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

Note `PrivateAssets="all"` on the project reference: it makes the AspireForm dependency dev-time only (the plugin loader supplies the contract at runtime; we don't want to drag the full AspireForm assembly into the plugin's transitive deps).

- [ ] **Step 2: Create `src/Plugins/AspireForm.Plugin.Redis/aspireform-plugin.json`**

```json
{
  "name": "Redis",
  "version": "0.1.0",
  "minAspireFormVersion": "0.3.0",
  "assemblyName": "AspireForm.Plugin.Redis",
  "providers": [
    {
      "type": "redis",
      "kind": "resource",
      "className": "AspireForm.Plugin.Redis.RedisResourceProvider"
    }
  ]
}
```

- [ ] **Step 3: Create `src/Plugins/AspireForm.Plugin.Redis/RedisResourceProvider.cs`**

```csharp
using System.Text;
using System.Text.Json.Nodes;
using AspireForm.Providers;

namespace AspireForm.Plugin.Redis;

/// <summary>External Resource provider for Redis. Delegates package add to <c>aspire add redis</c>; owns the AppHost resource declaration in a managed region.</summary>
public sealed class RedisResourceProvider : IProvider
{
    /// <inheritdoc />
    public string Type => "redis";

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
            CliActions = [new PlannedCliAction("aspire", ["add", "redis"])],
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
        sb.Append("var ").Append(blockName).Append(" = builder.AddRedis(\"").Append(aspireName).Append("\")");
        if (withDataVolume)
        {
            sb.Append(".WithDataVolume()");
        }
        sb.Append(';');
        return sb.ToString();
    }
}
```

- [ ] **Step 4: Create `src/Plugins/AspireForm.Plugin.Redis/README.md`**

```markdown
# AspireForm.Plugin.Redis

External Redis resource provider for [AspireForm](https://github.com/jamesburton/AspireForm).

## Block type

`redis` (Resource)

## Inputs

| Input | Type | Default | Description |
|---|---|---|---|
| `aspireName` | string | block name | Name passed to `builder.AddRedis(...)`. |
| `withDataVolume` | bool | `false` | When true, appends `.WithDataVolume()`. |

## Example

```yaml
resources:
  cache:
    type: redis
    aspireName: cache
    withDataVolume: true
```
```

- [ ] **Step 5: Create `src/Plugins/AspireForm.Plugin.Redis/CHANGELOG.md`**

```markdown
# Changelog

## [0.1.0] - 2026-05-24

Initial release. Redis Resource provider for AspireForm.

### Added

- `redis` block type emitting `aspire add redis` + managed AppHost region with `builder.AddRedis(...)`.
- Optional `withDataVolume` input.
```

- [ ] **Step 6: Create the unit test project**

`tests/Plugins/AspireForm.Plugin.Redis.Tests/AspireForm.Plugin.Redis.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Exe</OutputType>
    <RootNamespace>AspireForm.Plugin.Redis.Tests</RootNamespace>
    <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit.v3" Version="3.2.2" />
    <PackageReference Include="xunit.v3.mtp-v2" Version="3.2.2" />
    <PackageReference Include="AwesomeAssertions" Version="9.4.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../../src/Plugins/AspireForm.Plugin.Redis/AspireForm.Plugin.Redis.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 7: Create the unit tests**

`tests/Plugins/AspireForm.Plugin.Redis.Tests/RedisResourceProviderTests.cs`:

```csharp
using System.Text.Json.Nodes;
using AspireForm.Plugin.Redis;
using AspireForm.Providers;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Plugin.Redis.Tests;

public sealed class RedisResourceProviderTests
{
    private readonly RedisResourceProvider _provider = new();

    private static PlanContext Ctx(JsonObject inputs) =>
        new("cache", inputs, AppHostDirectory: "./MyApp.AppHost", ProjectName: "MyApp");

    [Fact]
    public void Type_and_kind_are_correct()
    {
        _provider.Type.Should().Be("redis");
        _provider.Kind.Should().Be(BlockKind.Resource);
    }

    [Fact]
    public void Plan_emits_aspire_add_redis_and_managed_AppHost_region()
    {
        var plan = _provider.Plan(Ctx(new JsonObject { ["aspireName"] = "cache" }));

        plan.CliActions.Should().ContainSingle(c => c.Tool == "aspire");
        plan.CliActions[0].Args.Should().ContainInOrder("add", "redis");

        plan.FileActions.Should().ContainSingle();
        plan.FileActions[0].OwnershipMode.Should().Be(OwnershipMode.Managed);
        plan.FileActions[0].RenderContent().Should().Contain("builder.AddRedis(\"cache\")");
    }

    [Fact]
    public void Plan_appends_WithDataVolume_when_withDataVolume_is_true()
    {
        var inputs = new JsonObject { ["aspireName"] = "cache", ["withDataVolume"] = true };
        _provider.Plan(Ctx(inputs)).FileActions[0].RenderContent()
            .Should().Contain(".WithDataVolume()");
    }

    [Fact]
    public void Plan_defaults_aspireName_to_block_name()
    {
        _provider.Plan(Ctx(new JsonObject())).FileActions[0].RenderContent()
            .Should().Contain("builder.AddRedis(\"cache\")");
    }
}
```

- [ ] **Step 8: Add both new csproj files to the solution**

```bash
dotnet sln add src/Plugins/AspireForm.Plugin.Redis/AspireForm.Plugin.Redis.csproj
dotnet sln add tests/Plugins/AspireForm.Plugin.Redis.Tests/AspireForm.Plugin.Redis.Tests.csproj
```

- [ ] **Step 9: Build and run the new tests**

```bash
dotnet build
dotnet run --project tests/Plugins/AspireForm.Plugin.Redis.Tests
```

Expected: 4 new tests passing in the Redis plugin's own test project. The main `AspireForm.Tests` total is still 164.

- [ ] **Step 10: Commit**

```bash
git add src/Plugins/ tests/Plugins/ AspireForm.slnx
git commit -m "feat: add AspireForm.Plugin.Redis (first dogfooded vertical plugin)"
```

---

## Task 14: End-to-end plugin-loaded integration test

Packs `AspireForm.Plugin.Redis` to a temp directory, configures a local NuGet feed pointing at it, runs the real `aspireform plan` against a fixture that uses `type: redis`, asserts the plugin loaded and the plan rendered.

**Files:**
- Create: `tests/AspireForm.Tests/EndToEnd/PluginLoaderE2ETests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Diagnostics;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.EndToEnd;

/// <summary>
/// End-to-end test of the plugin loader: packs AspireForm.Plugin.Redis to a temp dir,
/// configures a NuGet.config pointing at it as a local feed, then runs the real tool's
/// 'plan' verb against a fixture referencing `type: redis` and asserts the plugin loaded.
/// Slow (packs + restores once per test); intentional — this is the gate that proves the
/// loader works end-to-end.
/// </summary>
public sealed class PluginLoaderE2ETests : IDisposable
{
    private readonly string _projectDir = Directory.CreateTempSubdirectory("aspireform-plugin-e2e").FullName;

    public void Dispose() => Directory.Delete(_projectDir, recursive: true);

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null
               && !File.Exists(Path.Combine(dir, "AspireForm.sln"))
               && !File.Exists(Path.Combine(dir, "AspireForm.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        return dir ?? throw new InvalidOperationException("Repo root not found.");
    }

    private static string BuildConfiguration() =>
        new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Debug";

    [Fact]
    public void Plan_against_fixture_using_redis_loads_the_plugin_and_renders_block()
    {
        var root = RepoRoot();
        var feedDir = Path.Combine(_projectDir, ".local-feed");
        Directory.CreateDirectory(feedDir);

        // 1. Pack AspireForm.Plugin.Redis into the local feed directory.
        var pack = Run("dotnet", workingDirectory: root,
            "pack", Path.Combine("src", "Plugins", "AspireForm.Plugin.Redis"),
            "-c", BuildConfiguration(),
            "-o", feedDir,
            "--no-build", "--nologo");
        pack.ExitCode.Should().Be(0, pack.Output);

        // 2. Write a NuGet.config in the project dir that adds the local feed at top priority.
        File.WriteAllText(Path.Combine(_projectDir, "NuGet.config"), $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="local" value="{{feedDir.Replace('\\', '/')}}" />
                <add key="nuget" value="https://api.nuget.org/v3/index.json" />
              </packageSources>
            </configuration>
            """);

        // 3. Write a fixture aspireform.yaml that references the new 'redis' block type.
        File.WriteAllText(Path.Combine(_projectDir, "aspireform.yaml"), """
            aspireform:
              version: 1
              project: Fixture
              apphost: Fixture.AppHost
            resources:
              cache:
                type: redis
                aspireName: cache
            """);

        // 4. Run the real tool's plan verb.
        var plan = Run("dotnet", workingDirectory: _projectDir,
            "run", "--configuration", BuildConfiguration(), "--no-build",
            "--project", Path.Combine(root, "src", "AspireForm"),
            "--", "plan", "--project-dir", _projectDir);

        plan.ExitCode.Should().Be(0, plan.Output);
        plan.Output.Should().Contain("+ cache").And.Contain("redis");
        plan.Output.Should().Contain("aspire add redis");
        plan.Output.Should().Contain("builder.AddRedis(\"cache\")");

        // 5. Verify the lockfile was written with the resolved Redis plugin entry.
        var lockPath = Path.Combine(_projectDir, ".aspireform", "plugins.lock.yaml");
        File.Exists(lockPath).Should().BeTrue();
        File.ReadAllText(lockPath).Should().Contain("AspireForm.Plugin.Redis");
    }

    private static (int ExitCode, string Output) Run(string fileName, string workingDirectory, params string[] args)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args) startInfo.ArgumentList.Add(a);

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }
}
```

- [ ] **Step 2: Run tests (1 new test; total = 165)**

```bash
dotnet build
dotnet run --project tests/AspireForm.Tests
```

This test packs the Redis plugin and restores it via the real loader — expect ~15–30s for that single test. If it fails:
- Confirm the pack succeeded (check the `Output` from step 4's assertion).
- If "package not found", verify the NuGet.config has the absolute path to the temp feed (forward-slashed on Windows).
- If "plugin assembly not found", confirm `aspireform-plugin.json` is at the package root inside the nupkg (`unzip -l <nupkg>`).

- [ ] **Step 3: Commit**

```bash
git add tests/AspireForm.Tests/EndToEnd/PluginLoaderE2ETests.cs
git commit -m "test: add end-to-end plugin loader test (pack -> local feed -> load)"
```

---

## Task 15: Release workflow extension for `plugin/<Name>/v<version>` tags

The existing `.github/workflows/release.yml` ships the main AspireForm package on `v<version>` tags. Extend it (or add a sibling job) to handle `plugin/<Name>/v<version>` tags: build, pack, and publish only that plugin's nupkg + create a scoped GitHub release.

**Files:**
- Modify: `.github/workflows/release.yml`

- [ ] **Step 1: Read the current workflow**

Read `.github/workflows/release.yml` to see the current shape. It currently triggers on `v*` tags only.

- [ ] **Step 2: Extend the workflow**

Replace the trigger section with:

```yaml
on:
  push:
    tags:
      - 'v*'
      - 'plugin/*/v*'
```

Add a conditional path: when the tag matches `plugin/<Name>/v<version>`, derive `PLUGIN_NAME` and `VERSION` from the tag and pack only that plugin's csproj. The simplest implementation is a separate job that runs when the tag matches the plugin pattern. Concrete addition to the workflow (place after the existing `publish` job):

```yaml
  publish-plugin:
    if: startsWith(github.ref, 'refs/tags/plugin/')
    runs-on: ubuntu-latest
    permissions:
      contents: write
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET SDK
        uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json

      - name: Derive plugin name and version
        id: parse
        run: |
          tag="${GITHUB_REF_NAME}"
          # tag format: plugin/<Name>/v<version>
          name="${tag#plugin/}"
          name="${name%/v*}"
          version="${tag##*/v}"
          echo "name=$name" >> "$GITHUB_OUTPUT"
          echo "version=$version" >> "$GITHUB_OUTPUT"

      - name: Restore + Build the plugin
        run: |
          dotnet restore "src/Plugins/AspireForm.Plugin.${{ steps.parse.outputs.name }}/AspireForm.Plugin.${{ steps.parse.outputs.name }}.csproj"
          dotnet build "src/Plugins/AspireForm.Plugin.${{ steps.parse.outputs.name }}/AspireForm.Plugin.${{ steps.parse.outputs.name }}.csproj" \
            --configuration Release --no-restore

      - name: Pack
        run: |
          dotnet pack "src/Plugins/AspireForm.Plugin.${{ steps.parse.outputs.name }}/AspireForm.Plugin.${{ steps.parse.outputs.name }}.csproj" \
            --configuration Release --no-build --output ./artifacts \
            /p:Version=${{ steps.parse.outputs.version }}

      - name: Publish to NuGet
        env:
          NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}
        run: |
          dotnet nuget push "./artifacts/AspireForm.Plugin.${{ steps.parse.outputs.name }}.${{ steps.parse.outputs.version }}.nupkg" \
            --api-key "$NUGET_API_KEY" \
            --source https://api.nuget.org/v3/index.json \
            --skip-duplicate

      - name: Create GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          name: AspireForm.Plugin.${{ steps.parse.outputs.name }} ${{ steps.parse.outputs.version }}
          generate_release_notes: true
          files: ./artifacts/AspireForm.Plugin.${{ steps.parse.outputs.name }}.${{ steps.parse.outputs.version }}.nupkg
```

Also gate the existing main-package `publish` job so it doesn't run on plugin tags:

```yaml
  publish:
    if: startsWith(github.ref, 'refs/tags/v')
    # ... (rest unchanged)
```

- [ ] **Step 3: Validate workflow syntax locally**

```bash
# Quick YAML sanity check — workflow won't actually run until a tag is pushed.
cat .github/workflows/release.yml | head -50
```

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/release.yml
git commit -m "ci: release workflow handles plugin/<Name>/v<version> tags for per-plugin publishing"
```

---

## Task 16: Cross-plugin CI workflow

A new workflow that runs on every push and PR to `main`, building the entire solution (including all plugins) and running all tests. Catches breakage where a core-engine change breaks a plugin's compile or behaviour.

**Files:**
- Create: `.github/workflows/ci.yml`

- [ ] **Step 1: Create `.github/workflows/ci.yml`**

```yaml
name: ci

on:
  push:
    branches: ['main']
  pull_request:
    branches: ['main']

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET SDK
        uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json

      - name: Install Aspire project templates
        run: dotnet new install Aspire.ProjectTemplates --force

      - name: Restore
        run: dotnet restore

      - name: Build (entire solution incl. all plugins)
        run: dotnet build --configuration Release --no-restore

      - name: Test (main test project)
        run: dotnet run --project tests/AspireForm.Tests --configuration Release --no-build

      - name: Test (per-plugin test projects)
        run: |
          for proj in tests/Plugins/*/*.csproj; do
            echo "Running tests for $proj"
            dotnet run --project "$(dirname "$proj")" --configuration Release --no-build
          done
```

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: add cross-plugin build+test workflow on push and PR to main"
```

---

## Task 17: README + CHANGELOG + csproj version bump for 0.3.0

**Files:**
- Modify: `README.md`, `CHANGELOG.md`, `src/AspireForm/AspireForm.csproj`

- [ ] **Step 1: Bump `<Version>` in `src/AspireForm/AspireForm.csproj`**

Change `<Version>0.2.0</Version>` to `<Version>0.3.0</Version>`. Leave everything else untouched.

- [ ] **Step 2: Update `README.md`**

Add a "Plugins" section above the existing "Documentation" pointer. Add the four new `plugin` commands to the Commands table:

```markdown
| `aspireform plugin list` | List installed plugins. |
| `aspireform plugin install <name>[@version]` | Install a plugin (auto-restore handles unknown types on next plan/apply). |
| `aspireform plugin update <name>` | Update a plugin to the latest version. |
| `aspireform plugin remove <name>` | Remove a plugin from the lockfile. |
```

And add a new section:

```markdown
## Plugins

AspireForm supports external plugins that contribute new block types (Resources or Modules)
via NuGet packages. The first available plugin is **Redis**:

| Plugin | Block type | NuGet |
|---|---|---|
| [AspireForm.Plugin.Redis](https://www.nuget.org/packages/AspireForm.Plugin.Redis) | `redis` | 0.1.0 |

When you reference an unknown block `type` in `aspireform.yaml` (e.g. `type: redis`), AspireForm
auto-restores the matching plugin from NuGet on the next `plan` or `apply`. The resolved
(name, version) pair is recorded in `.aspireform/plugins.lock.yaml` (committed to git).
```

Update the Status section:

```markdown
## Status

v0.3.0 — Plugin loader. AspireForm now supports external NuGet plugins; the first one
(`AspireForm.Plugin.Redis`) is available. More verticals (Mailpit, Hangfire, DAB, auth × 3,
reporting, ETL) arrive in Plans 2.1–2.9.
```

- [ ] **Step 3: Update `CHANGELOG.md`**

Add a `## [0.3.0]` section at the top, ABOVE the existing `## [0.2.0]` section:

```markdown
## [0.3.0] - 2026-05-24

Plugin loader — AspireForm now supports external Resource and Module providers shipped as
separate NuGet packages.

### Added

- **External plugin loader.** Plugins are NuGet packages with `<PackageType>AspireFormPlugin</PackageType>`
  containing an `aspireform-plugin.json` manifest. AspireForm shells out to `dotnet restore` to fetch
  plugin packages into the global NuGet cache, then loads their assemblies into an isolated
  `AssemblyLoadContext`. No `NuGet.Protocol` is embedded into AspireForm itself — the SDK handles
  dependency resolution.
- **Auto-restore on first use.** Declaring a block `type` not provided by a built-in provider
  triggers an automatic restore of `AspireForm.Plugin.<Name>` on the next `plan`/`apply`. Pinned
  versions are recorded in `.aspireform/plugins.lock.yaml` (committed to git).
- **`aspireform plugin list / install / update / remove`** commands for explicit lifecycle
  management (pinning, offline use, CI cache warmup).
- **First dogfooded plugin: AspireForm.Plugin.Redis 0.1.0** — Redis Resource provider with optional
  `withDataVolume` input.
- **Cross-plugin CI workflow** (`.github/workflows/ci.yml`) builds the entire solution and runs every
  test project on every push and PR to main; the release workflow now handles
  `plugin/<Name>/v<version>` tags for per-plugin publishing.

### Notes

- Plugins declare `minAspireFormVersion` in their manifest; the loader refuses incompatible plugins
  with a clear error.
- Plugin assemblies remain loaded for the AspireForm-invocation lifetime — `plugin remove` clears the
  lockfile entry but does not unload an already-loaded plugin until next run.
- `.cs`-script plugin support is a follow-up plan (Plan 2.0.5).
```

- [ ] **Step 4: Build + test + commit**

```bash
dotnet build
dotnet run --project tests/AspireForm.Tests
```

Expected: 165 tests still pass, build clean.

```bash
git add README.md CHANGELOG.md src/AspireForm/AspireForm.csproj
git commit -m "docs: README + CHANGELOG + csproj version bump for 0.3.0"
```

---

## Plan 2.0 — Definition of done

- `dnx AspireForm@0.3.0 plan` against an `aspireform.yaml` declaring `type: redis` auto-restores `AspireForm.Plugin.Redis` from NuGet and renders a correct plan.
- `aspireform plugin list / install / update / remove` all behave per their tests.
- The end-to-end plugin loader test (Task 14) passes — proves the loader works against a real packed plugin.
- Plugin contract version check rejects incompatible plugins with a clear error.
- All 165 tests green locally and in CI (cross-plugin CI workflow runs the main + per-plugin test projects).
- Release artifacts ready: tag `v0.3.0` ships the main AspireForm package; tag `plugin/Redis/v0.1.0` ships `AspireForm.Plugin.Redis` to NuGet.

## Release procedure (after the plan merges to main)

```bash
# Main package first (loader required by the plugin to function).
git tag -a v0.3.0 -m "AspireForm 0.3.0 — external plugin loader"
git push origin v0.3.0
gh run watch                                              # release workflow runs the 'publish' job

# Once 0.3.0 is indexed on NuGet (~5 min), tag and push the plugin.
git tag -a plugin/Redis/v0.1.0 -m "AspireForm.Plugin.Redis 0.1.0 — Redis Resource provider"
git push origin plugin/Redis/v0.1.0
gh run watch                                              # release workflow runs 'publish-plugin'
```

End-to-end verification after both are live:

```bash
mkdir /tmp/aspireform-redis-test && cd /tmp/aspireform-redis-test
dnx AspireForm@0.3.0 new RedisDemo
cd RedisDemo
dnx AspireForm@0.3.0 add redis cache
dnx AspireForm@0.3.0 plan
# Expected: auto-restores AspireForm.Plugin.Redis, renders '+ cache (resource) — CREATE'.
```

---

## Self-review notes

- **Spec coverage:**
  - §3.1 plugin discovery — Tasks 5 (PackageType-implicit via name convention path) + 7 (PluginManager wire-up).
  - §3.2 manifest — Task 2.
  - §3.3 NuGet restore — Tasks 5, 7.
  - §3.4 .cs-script restore — **explicitly deferred to Plan 2.0.5** as agreed with user; called out in the plan header.
  - §3.5 loader architecture — Tasks 6, 7, 8.
  - §3.6 CLI verbs — Tasks 9, 10, 11, 12.
  - §3.7 first plugin Redis — Task 13.
  - §3.8 definition of done — covered in "Plan 2.0 — Definition of done" above.
- **Deliberate v1 narrowings called out:**
  - `.cs`-script plugins deferred to Plan 2.0.5 (advisor pivot, user agreed).
  - In-process NuGet.Protocol rejected in favour of shell-out to `dotnet restore` (advisor pivot — better for tool size + SDK version isolation).
  - AssemblyLoadContext is non-collectible — plugin remove takes effect on next run only. Documented limitation in CHANGELOG + remove command's message.
  - Plugin contract version compares `major.minor` only (drops build/revision).
- **Placeholder scan:** none. Every step has concrete code or commands.
- **Type/name consistency:**
  - `PluginManifest { Name, Version, MinAspireFormVersion, AssemblyName?, Providers[] }`, `PluginManifestProvider(Type, Kind, ClassName)` — Task 2.
  - `PluginLockfile { SchemaVersion, Plugins[] }`, `PluginLockEntry { Name, Package, Version, Source }` — Task 4.
  - `PluginRestorer.RestoreAsync(packageId, version, workingDirectory)` returning `PluginRestoreResult(Success, PackageDirectory?, ErrorMessage?)` — Task 5.
  - `PluginAssemblyLoader.LoadProviders(packageDirectory, manifest)` returning `IReadOnlyList<IProvider>` — Task 6.
  - `PluginManager.DiscoverAndLoadAsync(model, projectDir, ct)` returning `ProviderRegistry` — Task 7.
  - `ProviderRegistry.AllProviders() / .Combine(...)` — Task 3.
  - Command names: `PluginListCommand`, `PluginInstallCommand`, `PluginUpdateCommand`, `PluginRemoveCommand` — Tasks 9–12.
  - Plugin package id pattern: `AspireForm.Plugin.<PascalCase(type)>`. Plugin manifest at package root (via `<Content Include="aspireform-plugin.json" Pack="true" PackagePath="\" />`). PackageType=`AspireFormPlugin`.
- **Tasks 15 and 16 don't follow TDD** — workflow YAML files have no unit-test substrate. Acceptable: validated by running the real workflow (Task 15 by tagging; Task 16 by pushing).
