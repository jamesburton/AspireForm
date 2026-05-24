# AspireForm Vertical Catalog — Plan 2.0.5: `.cs`-script plugins

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Add the second plugin shape from the spec: `.aspireform/scripts/*.cs` files are compiled via Roslyn into the existing `AspireFormPlugins` `AssemblyLoadContext`. Plugin authors get a zero-NuGet-ceremony local extension path. Ships **AspireForm 0.3.2**.

**Architecture:** A new `ScriptPluginCompiler` parses `#:package <id>@<version>` directives, restores each declared package via the existing `PluginRestorer`, compiles the script with Roslyn (`CSharpCompilation` + `Emit`), caches the compiled assembly by source hash, and hands the assembly path to `PluginAssemblyLoader` for instantiation. `PluginManager.DiscoverAndLoadAsync` discovers script files alongside lockfile entries.

**Tech Stack:** Inherits prior plans + adds `Microsoft.CodeAnalysis.CSharp` 5.3.0 to the **main** AspireForm project (it was already in the test project; now becomes a production dep). This bumps AspireForm's transitive dep footprint — acceptable trade-off for the feature.

**Spec:** `docs/superpowers/specs/2026-05-24-aspireform-vertical-catalog-design.md` §3.4 (.cs-script restore) and §7.4 (.cs-script author UX).

**Plan position:** Plan 2.0.5 (the deferred `.cs`-script piece from Plan 2.0). Plans 2.1–2.9 are the nine vertical plugins.

---

## Locked decisions

1. **Roslyn in main package.** Production code uses `Microsoft.CodeAnalysis.CSharp` — already a stable dep, ~7MB. The cost is acceptable for the feature; AspireForm is a dev tool, not a runtime-shipped library.
2. **Compile cache:** `<projectDir>/.aspireform/scripts/.cache/<source-sha256>/<script-name>.dll`. Source-hash key means edits invalidate cleanly; old cache dirs are garbage but harmless.
3. **`#:package` directive handling:** parse before compile, restore each via `PluginRestorer.RestoreAsync`, locate primary assembly in `lib/net10.0/`, pass as `MetadataReference`. Transitive deps load at runtime via Plan 2.0's `AssemblyDependencyResolver`.
4. **Script manifest convention:** scripts don't ship a separate `aspireform-plugin.json`. The compiler scans the compiled assembly for types implementing `IProvider` via reflection and registers all of them. Simpler author UX.
5. **One-shot compile per AspireForm invocation.** No watch mode. Edits take effect on next run.
6. **Script discovery:** any `*.cs` directly under `.aspireform/scripts/` (no recursion in v1).

---

## File structure

```
src/AspireForm/Plugins/
  ScriptDirective.cs                NEW — parsed #:package directive
  ScriptDirectiveParser.cs          NEW — text → directives
  ScriptPluginCompiler.cs           NEW — Roslyn compile + cache
  PluginAssemblyLoader.cs           MODIFY — add LoadProvidersByDiscovery(packageDir, assemblyPath)
                                      for scripts (no manifest, scan for IProvider)
  PluginManager.cs                  MODIFY — discover + compile scripts before NuGet plugin pass

src/AspireForm/AspireForm.csproj    MODIFY — add Microsoft.CodeAnalysis.CSharp 5.3.0, bump 0.3.1 → 0.3.2

tests/AspireForm.Tests/Plugins/
  ScriptDirectiveParserTests.cs     NEW
  ScriptPluginCompilerTests.cs      NEW

README.md                           MODIFY — add ".cs script plugins" subsection
CHANGELOG.md                        MODIFY — [0.3.2] section
```

---

## Task 1: ScriptDirective + ScriptDirectiveParser

**Files:**
- Create: `src/AspireForm/Plugins/ScriptDirective.cs`
- Create: `src/AspireForm/Plugins/ScriptDirectiveParser.cs`
- Test: `tests/AspireForm.Tests/Plugins/ScriptDirectiveParserTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
using AspireForm.Plugins;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Plugins;

public sealed class ScriptDirectiveParserTests
{
    [Fact]
    public void Parses_package_directive_with_version()
    {
        const string source = """
            #:package Newtonsoft.Json@13.0.3
            using System;
            """;

        var directives = ScriptDirectiveParser.Parse(source).ToList();

        directives.Should().ContainSingle();
        directives[0].Kind.Should().Be(ScriptDirectiveKind.Package);
        directives[0].PackageId.Should().Be("Newtonsoft.Json");
        directives[0].Version.Should().Be("13.0.3");
    }

    [Fact]
    public void Parses_package_directive_without_version_as_floating()
    {
        var directives = ScriptDirectiveParser.Parse("#:package SomeLib").ToList();
        directives.Should().ContainSingle();
        directives[0].PackageId.Should().Be("SomeLib");
        directives[0].Version.Should().Be("*");
    }

    [Fact]
    public void Stops_parsing_at_first_non_directive_line()
    {
        const string source = """
            #:package A@1.0.0
            // a comment
            #:package B@2.0.0
            using System;
            """;

        // After "// a comment", #: directives are ignored (file-based-app convention).
        var directives = ScriptDirectiveParser.Parse(source).ToList();
        directives.Should().ContainSingle();
        directives[0].PackageId.Should().Be("A");
    }

    [Fact]
    public void Ignores_blank_lines_at_the_top()
    {
        const string source = """


            #:package A@1.0.0
            """;
        ScriptDirectiveParser.Parse(source).Should().ContainSingle();
    }

    [Fact]
    public void Returns_empty_for_a_source_with_no_directives()
    {
        ScriptDirectiveParser.Parse("using System;\nclass X { }").Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run test to verify fail (compile error)**

- [ ] **Step 3: Create `src/AspireForm/Plugins/ScriptDirective.cs`**

```csharp
namespace AspireForm.Plugins;

/// <summary>The kind of <c>#:</c> directive declared at the top of a script plugin.</summary>
public enum ScriptDirectiveKind
{
    /// <summary>A NuGet package reference: <c>#:package &lt;id&gt;[@&lt;version&gt;]</c>.</summary>
    Package,
}

/// <summary>A parsed <c>#:</c> directive from a script plugin's source.</summary>
/// <param name="Kind">The directive kind.</param>
/// <param name="PackageId">For <see cref="ScriptDirectiveKind.Package"/>: the NuGet package id.</param>
/// <param name="Version">For <see cref="ScriptDirectiveKind.Package"/>: the package version (or <c>*</c> for floating).</param>
public sealed record ScriptDirective(ScriptDirectiveKind Kind, string PackageId, string Version);
```

- [ ] **Step 4: Create `src/AspireForm/Plugins/ScriptDirectiveParser.cs`**

```csharp
namespace AspireForm.Plugins;

/// <summary>Parses <c>#:</c> directives at the top of a script plugin (.NET 10 file-based-app convention).</summary>
public static class ScriptDirectiveParser
{
    /// <summary>
    /// Returns directives parsed from the leading <c>#:</c>-prefixed lines of <paramref name="source"/>.
    /// Blank lines at the top are skipped; parsing stops at the first non-blank, non-directive line.
    /// </summary>
    public static IEnumerable<ScriptDirective> Parse(string source)
    {
        foreach (var rawLine in source.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r').Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (!line.StartsWith("#:", StringComparison.Ordinal))
            {
                yield break;
            }

            var rest = line[2..].Trim();
            if (rest.StartsWith("package ", StringComparison.OrdinalIgnoreCase))
            {
                var arg = rest["package ".Length..].Trim();
                var (id, version) = SplitIdVersion(arg);
                yield return new ScriptDirective(ScriptDirectiveKind.Package, id, version);
            }
            // Other directive kinds (#:sdk, #:property) are ignored in v1.
        }
    }

    private static (string Id, string Version) SplitIdVersion(string arg)
    {
        var at = arg.IndexOf('@');
        if (at < 0)
        {
            return (arg, "*");
        }

        var id = arg[..at].Trim();
        var version = arg[(at + 1)..].Trim();
        return (id, string.IsNullOrEmpty(version) ? "*" : version);
    }
}
```

- [ ] **Step 5: Run tests pass (5 new tests; total = 186)**

- [ ] **Step 6: Commit**
```bash
git add src/AspireForm/Plugins/ScriptDirective.cs src/AspireForm/Plugins/ScriptDirectiveParser.cs tests/AspireForm.Tests/Plugins/ScriptDirectiveParserTests.cs
git commit -m "feat: add ScriptDirectiveParser for #:package directives"
```

---

## Task 2: Add Roslyn to main AspireForm csproj

**Files:**
- Modify: `src/AspireForm/AspireForm.csproj`

- [ ] **Step 1: Add the package reference**

```bash
dotnet add src/AspireForm/AspireForm.csproj package Microsoft.CodeAnalysis.CSharp --version 5.3.0
```

- [ ] **Step 2: Build to confirm clean**

```bash
dotnet build
```

Expected: clean build.

- [ ] **Step 3: Commit**

```bash
git add src/AspireForm/AspireForm.csproj
git commit -m "chore: add Microsoft.CodeAnalysis.CSharp 5.3.0 to AspireForm (for .cs-script compile)"
```

---

## Task 3: ScriptPluginCompiler

**Files:**
- Create: `src/AspireForm/Plugins/ScriptPluginCompiler.cs`
- Test: `tests/AspireForm.Tests/Plugins/ScriptPluginCompilerTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
using System.Security.Cryptography;
using System.Text;
using AspireForm.Plugins;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Plugins;

public sealed class ScriptPluginCompilerTests : IDisposable
{
    private readonly string _projectDir = Directory.CreateTempSubdirectory("aspireform-script-compile").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_projectDir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task CompileAsync_emits_an_assembly_implementing_IProvider()
    {
        const string source = """
            using AspireForm.Providers;
            namespace InlineScript;
            public sealed class ScriptProvider : IProvider
            {
                public string Type => "script-test";
                public BlockKind Kind => BlockKind.Resource;
                public ProviderPlan Plan(PlanContext context) => new();
            }
            """;

        var scriptPath = Path.Combine(_projectDir, ".aspireform", "scripts", "test.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
        await File.WriteAllTextAsync(scriptPath, source);

        var compiler = new ScriptPluginCompiler();
        var result = await compiler.CompileAsync(scriptPath, _projectDir);

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.AssemblyPath.Should().NotBeNull();
        File.Exists(result.AssemblyPath).Should().BeTrue();
    }

    [Fact]
    public async Task CompileAsync_returns_failure_on_invalid_source()
    {
        var scriptPath = Path.Combine(_projectDir, ".aspireform", "scripts", "bad.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
        await File.WriteAllTextAsync(scriptPath, "this is not valid C# at all");

        var compiler = new ScriptPluginCompiler();
        var result = await compiler.CompileAsync(scriptPath, _projectDir);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CompileAsync_caches_on_source_hash_and_skips_recompile()
    {
        const string source = """
            using AspireForm.Providers;
            namespace CacheTest;
            public sealed class CachedProvider : IProvider
            {
                public string Type => "cached";
                public BlockKind Kind => BlockKind.Resource;
                public ProviderPlan Plan(PlanContext context) => new();
            }
            """;

        var scriptPath = Path.Combine(_projectDir, ".aspireform", "scripts", "cached.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
        await File.WriteAllTextAsync(scriptPath, source);

        var compiler = new ScriptPluginCompiler();
        var first = await compiler.CompileAsync(scriptPath, _projectDir);
        var firstWritten = File.GetLastWriteTimeUtc(first.AssemblyPath!);

        await Task.Delay(50);
        var second = await compiler.CompileAsync(scriptPath, _projectDir);
        var secondWritten = File.GetLastWriteTimeUtc(second.AssemblyPath!);

        // Same source -> same cached assembly path AND no re-write.
        second.AssemblyPath.Should().Be(first.AssemblyPath);
        secondWritten.Should().Be(firstWritten);
    }
}
```

- [ ] **Step 2: Run test to verify fail**

- [ ] **Step 3: Create `src/AspireForm/Plugins/ScriptPluginCompiler.cs`**

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AspireForm.Plugins;

/// <summary>The outcome of compiling a script plugin.</summary>
/// <param name="Success">True when the compile succeeded and the assembly was written or cached.</param>
/// <param name="AssemblyPath">Absolute path to the compiled assembly; null on failure.</param>
/// <param name="ErrorMessage">Human-readable error description; null on success.</param>
public sealed record ScriptCompileResult(bool Success, string? AssemblyPath, string? ErrorMessage);

/// <summary>Compiles a <c>.cs</c>-script plugin via Roslyn, with NuGet dep restore and source-hash caching.</summary>
public sealed class ScriptPluginCompiler
{
    private readonly PluginRestorer _restorer;

    /// <summary>Initialises the compiler with a default <see cref="PluginRestorer"/>.</summary>
    public ScriptPluginCompiler()
    {
        _restorer = new PluginRestorer();
    }

    /// <summary>
    /// Compiles <paramref name="scriptPath"/> into the project's cache directory.
    /// Returns the cached assembly path if a prior compile of the same source already exists.
    /// </summary>
    public async Task<ScriptCompileResult> CompileAsync(
        string scriptPath, string projectDir, CancellationToken cancellationToken = default)
    {
        var source = await File.ReadAllTextAsync(scriptPath, cancellationToken);
        var sourceHash = HashSource(source);
        var assemblyName = Path.GetFileNameWithoutExtension(scriptPath);
        var cacheDir = Path.Combine(projectDir, ".aspireform", "scripts", ".cache", sourceHash);
        var cachedAssemblyPath = Path.Combine(cacheDir, $"{assemblyName}.dll");

        if (File.Exists(cachedAssemblyPath))
        {
            return new ScriptCompileResult(true, cachedAssemblyPath, null);
        }

        Directory.CreateDirectory(cacheDir);

        // Restore #:package directives + collect references.
        var directives = ScriptDirectiveParser.Parse(source).ToList();
        var references = new List<MetadataReference>();
        references.AddRange(BuiltInReferences());

        foreach (var directive in directives.Where(d => d.Kind == ScriptDirectiveKind.Package))
        {
            var restore = await _restorer.RestoreAsync(directive.PackageId, directive.Version, projectDir, cancellationToken);
            if (!restore.Success)
            {
                return new ScriptCompileResult(false, null,
                    $"Failed to restore '#:package {directive.PackageId}@{directive.Version}': {restore.ErrorMessage}");
            }

            // Add every assembly in lib/net10.0/ from the restored package as a metadata reference.
            var libDir = Path.Combine(restore.PackageDirectory!, "lib", "net10.0");
            if (Directory.Exists(libDir))
            {
                foreach (var dll in Directory.EnumerateFiles(libDir, "*.dll"))
                {
                    references.Add(MetadataReference.CreateFromFile(dll));
                }
            }
        }

        var syntax = CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken);
        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees: [syntax],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = File.Create(cachedAssemblyPath);
        var emit = compilation.Emit(stream, cancellationToken: cancellationToken);
        if (!emit.Success)
        {
            stream.Close();
            try { File.Delete(cachedAssemblyPath); } catch { /* ignore */ }
            var diags = string.Join("\n", emit.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString()));
            return new ScriptCompileResult(false, null, $"Script compile failed:\n{diags}");
        }

        return new ScriptCompileResult(true, cachedAssemblyPath, null);
    }

    private static IEnumerable<MetadataReference> BuiltInReferences()
    {
        // The BCL + AspireForm itself + anything currently loaded with a real Location.
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location) && File.Exists(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>();
    }

    private static string HashSource(string source)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return Convert.ToHexStringLower(bytes);
    }
}
```

- [ ] **Step 4: Run tests pass (3 new tests; total = 189)**

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Plugins/ScriptPluginCompiler.cs tests/AspireForm.Tests/Plugins/ScriptPluginCompilerTests.cs
git commit -m "feat: add ScriptPluginCompiler (Roslyn + #:package restore + source-hash cache)"
```

---

## Task 4: PluginAssemblyLoader.LoadProvidersByDiscovery + PluginManager integration

Scripts have no manifest; the loader needs to reflectively discover `IProvider` implementations in the compiled assembly. Add a sibling method on `PluginAssemblyLoader`.

**Files:**
- Modify: `src/AspireForm/Plugins/PluginAssemblyLoader.cs`
- Modify: `src/AspireForm/Plugins/PluginManager.cs`
- Test: extend `tests/AspireForm.Tests/Plugins/PluginAssemblyLoaderTests.cs`

- [ ] **Step 1: Add `LoadProvidersByDiscovery` method to PluginAssemblyLoader**

In `src/AspireForm/Plugins/PluginAssemblyLoader.cs`, add:

```csharp
    /// <summary>
    /// Loads <paramref name="assemblyPath"/> into the plugin ALC and returns every public, instantiable
    /// <see cref="IProvider"/> implementation it contains. Used for script plugins (no manifest).
    /// </summary>
    public IReadOnlyList<IProvider> LoadProvidersByDiscovery(string assemblyPath)
    {
        var assembly = LoadAndRegisterAssembly(assemblyPath); // existing internal helper extracted from LoadProviders

        var providerTypes = assembly.GetTypes()
            .Where(t => t.IsPublic && !t.IsAbstract && !t.IsInterface
                     && typeof(IProvider).IsAssignableFrom(t)
                     && t.GetConstructor(Type.EmptyTypes) is not null)
            .ToList();

        var providers = new List<IProvider>(providerTypes.Count);
        foreach (var type in providerTypes)
        {
            providers.Add((IProvider)Activator.CreateInstance(type)!);
        }
        return providers;
    }
```

Extract the assembly-load + ALC-registration code from existing `LoadProviders` into a private `LoadAndRegisterAssembly(string assemblyPath)` helper so both `LoadProviders` and `LoadProvidersByDiscovery` use it. The helper registers the assembly's directory for the `Resolving` handler exactly like Plan 2.0's path does.

- [ ] **Step 2: Add script discovery in PluginManager.DiscoverAndLoadAsync**

In `src/AspireForm/Plugins/PluginManager.cs`, BEFORE the existing NuGet-plugin foreach, add:

```csharp
        // Script plugins: compile + load every .cs in .aspireform/scripts/.
        var scriptsDir = Path.Combine(projectDir, ".aspireform", "scripts");
        if (Directory.Exists(scriptsDir))
        {
            var compiler = new ScriptPluginCompiler();
            foreach (var scriptPath in Directory.EnumerateFiles(scriptsDir, "*.cs", SearchOption.TopDirectoryOnly))
            {
                var result = await compiler.CompileAsync(scriptPath, projectDir, cancellationToken);
                if (!result.Success)
                {
                    throw new PluginContractException(
                        $"Script plugin '{Path.GetFileName(scriptPath)}' failed to compile: {result.ErrorMessage}");
                }

                pluginProviders.AddRange(_loader.LoadProvidersByDiscovery(result.AssemblyPath!));
            }
        }
```

Note: `pluginProviders` is the list this method already builds. Script-derived providers join NuGet-derived providers in the same returned registry.

- [ ] **Step 3: Add a script-discovery test to PluginManagerTests**

```csharp
[Fact]
public async Task DiscoverAndLoadAsync_compiles_and_loads_a_script_plugin()
{
    var scriptsDir = Path.Combine(_dir, ".aspireform", "scripts");
    Directory.CreateDirectory(scriptsDir);
    await File.WriteAllTextAsync(Path.Combine(scriptsDir, "my-vertical.cs"), """
        using AspireForm.Providers;
        namespace MyScript;
        public sealed class MyVerticalProvider : IProvider
        {
            public string Type => "my-vertical";
            public BlockKind Kind => BlockKind.Module;
            public ProviderPlan Plan(PlanContext context) => new();
        }
        """);

    var model = new ProjectModel
    {
        AspireForm = new AspireFormHeader { Version = 1, Project = "X", AppHost = "X.AppHost" },
        Modules = new Dictionary<string, ModuleBlock>
        {
            ["mine"] = new() { Name = "mine", Type = "my-vertical", Inputs = new() },
        },
    };

    var registry = await new PluginManager().DiscoverAndLoadAsync(model, _dir);

    registry.Get("my-vertical").Type.Should().Be("my-vertical");
}
```

This test exercises the full path: compile + load + register. NOTE: it needs the unknown-types check to NOT short-circuit. Currently `DiscoverAndLoadAsync` returns the built-in registry early when there are no unknown types — but the model HAS an unknown type (`my-vertical`), so the foreach runs. Good.

But there's a subtle issue: the foreach for unknown types will ALSO try to NuGet-restore `AspireForm.Plugin.MyVertical` (the convention-derived package id) for the unknown `my-vertical` type. That's a behavior change we don't want — script-resolved types should not trigger NuGet restore.

Fix in PluginManager: after the script-compile pass adds providers, prune `unknownTypes` to those NOT supplied by scripts before the NuGet-restore foreach. Concretely:

```csharp
        // After script compile + load:
        var scriptProvidedTypes = pluginProviders.Select(p => p.Type).ToHashSet(StringComparer.Ordinal);
        unknownTypes = unknownTypes.Where(t => !scriptProvidedTypes.Contains(t)).ToList();
```

Place this between the script foreach and the NuGet-plugin foreach.

- [ ] **Step 4: Run all tests (1 new test + existing pass; total ≈ 190)**

- [ ] **Step 5: Commit**

```bash
git add src/AspireForm/Plugins/PluginAssemblyLoader.cs src/AspireForm/Plugins/PluginManager.cs tests/AspireForm.Tests/Plugins/PluginAssemblyLoaderTests.cs tests/AspireForm.Tests/Plugins/PluginManagerTests.cs
git commit -m "feat: wire script plugin compile + discovery into PluginManager"
```

---

## Task 5: README + CHANGELOG + csproj 0.3.2

**Files:**
- Modify: `README.md`, `CHANGELOG.md`, `src/AspireForm/AspireForm.csproj`

- [ ] **Step 1: Bump csproj version**

`src/AspireForm/AspireForm.csproj`: `<Version>0.3.1</Version>` → `<Version>0.3.2</Version>`.

- [ ] **Step 2: README update**

Add a subsection to the Plugins section:

```markdown
### Script plugins (`.cs` files)

For quick local extension without packaging, drop a `.cs` file into `.aspireform/scripts/`.
AspireForm compiles it via Roslyn into the same plugin context as NuGet plugins. Use
`#:package <id>@<version>` directives at the top of the file to declare NuGet dependencies.

\`\`\`csharp
#:package Some.Helper.Lib@1.2.3

using AspireForm.Providers;

public sealed class MyVerticalProvider : IProvider
{
    public string Type => "my-vertical";
    public BlockKind Kind => BlockKind.Module;
    public ProviderPlan Plan(PlanContext context) => new() { /* ... */ };
}
\`\`\`

The compiler caches by source-hash at `.aspireform/scripts/.cache/<sha256>/` — unchanged scripts
skip recompile.
```

- [ ] **Step 3: CHANGELOG update**

Add `## [0.3.2]` above `[0.3.1]`:

```markdown
## [0.3.2] - 2026-05-24

Plugin shape #2: `.cs`-script plugins.

### Added

- Drop a `.cs` file into `.aspireform/scripts/` and AspireForm compiles + loads it via Roslyn
  into the same `AspireFormPlugins` context as NuGet plugins. No package authoring needed.
- `#:package <id>[@<version>]` directives at the top of a script declare NuGet dependencies;
  AspireForm restores them via the same `PluginRestorer` path used for NuGet plugins.
- Source-hash compile cache at `.aspireform/scripts/.cache/<sha256>/` — unchanged scripts skip
  recompile across runs.
- Script-provided block types take priority over NuGet auto-restore for the same `type`.
```

- [ ] **Step 4: Build + tests pass + commit**

```bash
dotnet build
dotnet run --project tests/AspireForm.Tests
```

```bash
git add README.md CHANGELOG.md src/AspireForm/AspireForm.csproj
git commit -m "docs: README + CHANGELOG + csproj 0.3.2 for .cs-script plugin support"
```

---

## Definition of done (Plan 2.0.5)

- `.cs` files in `.aspireform/scripts/` compile and load, types reflected for `IProvider` discovery.
- `#:package` directives restore via existing PluginRestorer.
- Source-hash cache prevents recompile across runs.
- Tests cover directive parser, compiler (success + failure + cache hit), and full PluginManager script-discovery path.
- AspireForm 0.3.2 csproj + CHANGELOG ready to ship via `v0.3.2` tag.
