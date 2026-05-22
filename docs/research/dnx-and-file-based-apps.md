# .NET 10 Research: `dnx` and File-Based Apps

> Sources: Microsoft Learn, .NET Blog, Andrew Lock's .NET 10 preview series (Nov 2025)
> Last updated: 2026-05-22

---

## Part 1 — `dnx`: Zero-Install Tool Execution

### 1.1 What `dnx` Is

`dnx` is a command-line script shipped with the .NET 10 SDK (and later) that lets you **run any .NET NuGet tool without a prior global or local install**. It is conceptually analogous to Node's `npx`. The implementation is a thin wrapper: the `dnx` binary (a shell script / batch file added to `PATH` by the installer) forwards all arguments to `dotnet dnx`, which is itself a hidden alias for `dotnet tool exec`. The actual logic lives in the `dotnet` CLI, so its behaviour can evolve with SDK updates without changing the script.

### 1.2 Exact Command Syntax

```sh
# Run the latest version of a tool
dnx <PackageId> [tool-arguments...]

# Run a specific version
dnx <PackageId>@<version> [tool-arguments...]

# Run a version range (e.g., latest 2.x)
dnx <PackageId>@<versionRange> [tool-arguments...]

# Equivalent long form (same behaviour)
dotnet tool exec <PackageId>[@<version>] [options] [-- tool-arguments...]
```

Examples:

```sh
dnx dotnetsay "Hello, World!"
dnx dotnetsay@2.1.0 "Hello!"
dnx dotnetsay@2.* "Hello!"
dotnet tool exec dotnetsay@2.1.0 -- Hello World
```

### 1.3 Full `dotnet tool exec` Option Reference

```
dotnet tool exec <PACKAGE_NAME>[@<VERSION>]
    [--allow-roll-forward]
    [-a|--arch <ARCHITECTURE>]
    [--add-source <SOURCE>]
    [--configfile <FILE>]
    [--disable-parallel]
    [--framework <FRAMEWORK>]
    [--ignore-failed-sources]
    [--interactive]
    [--no-http-cache]
    [--prerelease]
    [--source <SOURCE>]
    [-v|--verbosity <LEVEL>]
    [--] [<tool-arguments>...]
```

Key flags:

| Flag | Purpose |
|------|---------|
| `--allow-roll-forward` | Allow tool to use a newer .NET runtime than it targets |
| `--add-source <SOURCE>` | Add an extra NuGet feed (feeds run in parallel; fastest wins) |
| `--source <SOURCE>` | Override the NuGet source entirely |
| `--prerelease` | Include pre-release packages in version resolution |
| `--no-http-cache` | Bypass HTTP-level NuGet feed cache |
| `--configfile <FILE>` | Use a specific `nuget.config` |
| `--interactive` | Pause to wait for user input (e.g. authentication) |

> **Note on `--yes`/`-y`:** The `dnx` wrapper script accepts `-y`/`--yes` to suppress the download confirmation prompt. The underlying `dotnet tool exec` command relies on `--interactive` / non-interactive terminal detection for the same purpose. In non-interactive terminals (CI) the confirmation is skipped automatically.

### 1.4 Confirmation Prompts

When a tool is not already cached locally, the user sees:

```
Tool package dotnetsay@1.0.0 will be downloaded from source https://api.nuget.org/v3/index.json. Proceed? [y/n] (y):
```

The default is `y`, so pressing Enter accepts. The prompt appears **only on the first download** of a given package/version; subsequent runs use the cached copy without prompting. In non-interactive (CI) terminals the prompt is bypassed automatically.

### 1.5 Package Resolution and Caching

**Resolution order:**

1. If a `.config/dotnet-tools.json` local tool manifest exists in the current directory or any ancestor, and it contains the requested package ID, that manifest's pinned version is used.
2. Otherwise the latest stable version from the configured NuGet feeds is resolved (or latest pre-release if `--prerelease` is passed, or latest matching a range if `@<range>` is specified).

**Cache location:**

Packages are downloaded to the **global NuGet package cache** — the same location used by `dotnet restore` for project builds. View it with:

```sh
dotnet nuget locals all --list
# e.g. global-packages: C:\Users\<user>\.nuget\packages
```

`dnx` does **not** write to the tool store (`~/.dotnet/tools/.store`) and does **not** create executable shims in `~/.dotnet/tools`. Nothing is added to `PATH`.

**Self-updating / picking up new versions:**

- `dnx <PackageId>` (no version) always resolves the latest stable version from NuGet at invocation time. If a newer version has been published, the next invocation downloads it.
- `dnx <PackageId>@<exact>` is pinned — it always uses exactly that version from cache.
- There is no background update mechanism; freshness is determined per-invocation by the NuGet feed query.

### 1.6 How a NuGet Package Becomes a `dnx`-Runnable Tool

Any package with `PackageType = DotnetTool` can be executed via `dnx`. Creating one requires:

**`.csproj` settings:**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>

    <!-- Mark the package as a dotnet tool -->
    <PackAsTool>true</PackAsTool>

    <!-- The CLI command users will type (defaults to assembly name if omitted) -->
    <ToolCommandName>my-tool</ToolCommandName>

    <!-- Optional: where to write the .nupkg -->
    <PackageOutputPath>./nupkg</PackageOutputPath>
  </PropertyGroup>
</Project>
```

`<PackAsTool>true</PackAsTool>` causes `dotnet pack` to:

1. Set `<packageType name="DotnetTool" />` in the generated `.nuspec`.
2. Emit a `DotnetToolSettings.xml` file inside the package under `tools/<tfm>/any/`.

**`DotnetToolSettings.xml` structure** (auto-generated by the SDK; do not write by hand):

```xml
<?xml version="1.0" encoding="utf-8"?>
<DotNetCliTool Version="1">
  <Commands>
    <Command Name="my-tool" EntryPoint="MyTool.dll" Runner="dotnet" />
  </Commands>
</DotNetCliTool>
```

- `Name` — matches `<ToolCommandName>` from the project.
- `EntryPoint` — the tool's `.dll`, co-located with `DotnetToolSettings.xml` in the package.
- `Runner` — always `"dotnet"` for framework-dependent tools.
- Location in package: `tools/net10.0/any/DotnetToolSettings.xml` (one file, one `<Command>` per package).

**Publishing to NuGet:** `dotnet pack` produces the `.nupkg`; upload it to `nuget.org` (or a private feed). Once published, it is immediately runnable via `dnx <PackageId>`.

**Platform-specific tools (.NET 10 new capability):**

Tools can now include multiple `RuntimeIdentifiers` in a single package (linux-x64, win-x64, macos-arm64, `any`, etc.). The SDK selects the correct binary at install/runtime. Tools can be published as framework-dependent, self-contained, trimmed, or AOT-compiled. This is set in the `.csproj`:

```xml
<RuntimeIdentifiers>linux-x64;linux-arm64;win-x64;win-arm64;macos-arm64;any</RuntimeIdentifiers>
```

### 1.7 Comparison with Other Tool Commands

| Feature | `dnx` / `dotnet tool exec` | `dotnet tool install -g` | `dotnet tool run` |
|---------|---------------------------|--------------------------|-------------------|
| Persistent install | No (cached only) | Yes — global tool store | Requires prior local install |
| PATH shim created | No | Yes (`~/.dotnet/tools`) | No |
| Tool store entry | No | Yes (`~/.dotnet/tools/.store`) | Yes (local manifest store) |
| Version pinning | Per-invocation (`@version`) | At install time | Via `dotnet-tools.json` manifest |
| Local manifest respected | Yes (auto) | No | Yes (required) |
| Scope | Ephemeral / CI-friendly | Machine-wide | Directory tree |
| .NET version | 10+ | All | 3.0+ |

`dotnet tool run` only works for tools already installed via a **local tool manifest** (`dotnet new tool-manifest` → `dotnet tool install`). It cannot download tools on demand. `dotnet tool install -g` performs a permanent installation with a PATH shim. `dnx`/`dotnet tool exec` is the new unified ephemeral path.

### 1.8 Limitations and Gotchas

- Requires .NET 10 SDK or later — not available for older SDKs.
- The `dnx` script is placed on `PATH` by the installer; if you installed .NET via Snap or a non-standard method the script may not be present, and you must fall back to `dotnet tool exec`.
- No offline support — if the NuGet feed is unreachable and the package is not already in the global package cache, the command fails.
- Running the same version twice re-uses the cached package instantly; running a different version downloads the new version to cache (old version is not evicted automatically).
- `--interactive` (or `-y`) is needed in CI pipelines if for some reason the terminal is detected as interactive — prefer `dotnet tool exec` with explicit version and `--source` in pipelines for full determinism.
- Tools built for an incompatible framework may fail; use `--allow-roll-forward` if the tool targets an older .NET version.
- The underlying `dotnet tool exec` command does not yet have a `--yes` long-form flag (as of .NET 10 GA); use `dnx -y` or rely on non-interactive terminal detection.

---

## Part 2 — File-Based Apps / `.cs` Scripts

### 2.1 What File-Based Apps Are

.NET 10 introduced the ability to run a **single C# file as an application** with no project file, no solution, and no build scaffolding. The SDK auto-generates a virtual project from the file's `#:` directives and runs it directly.

### 2.2 Running a File-Based App

Three equivalent syntaxes:

```sh
dotnet run file.cs            # standard form
dotnet run --file file.cs     # explicit --file flag
dotnet file.cs                # shorthand (new in .NET 10)
```

> **Backwards-compatibility note:** `dotnet run file.cs` only works as a file-based app invocation when **no `.csproj`/`.sln` is present** in the current directory. If a project file exists, `file.cs` is passed as an argument to that project — use `dotnet run --file file.cs` or `dotnet file.cs` to be unambiguous.

Pass arguments to the app with `--`:

```sh
dotnet run file.cs -- arg1 arg2
```

Pipe code from stdin:

```sh
# PowerShell
'Console.WriteLine("hello from stdin!");' | dotnet run -

# Bash
echo 'Console.WriteLine("hello from stdin!");' | dotnet run -
```

### 2.3 Supported `#:` Directives

All directives must be placed at the **top of the file**. They use C# 14's "ignored directive" specification so they are invisible to the compiler but parsed by the SDK.

#### `#:package` — NuGet package reference

```csharp
#:package Newtonsoft.Json@13.0.3
#:package Serilog@3.1.1
#:package Spectre.Console@*          // latest version
#:package Humanizer@2.14.1
```

- Syntax: `#:package <PackageId>@<version>`.
- The `@*` wildcard resolves to the latest stable version.
- Version ranges (`@2.*`) are supported.
- Omitting the version currently only works when **central package management** (`Directory.Packages.props`) is in use; otherwise always specify `@<version>` or `@*`.

#### `#:sdk` — MSBuild SDK

```csharp
#:sdk Microsoft.NET.Sdk.Web
#:sdk Aspire.AppHost.Sdk@13.0.2
```

- Defaults to `Microsoft.NET.Sdk` if omitted.
- The first `#:sdk` sets the base SDK. Additional `#:sdk` lines add NuGet-sourced SDKs (additive, versioned with `@`).
- Use `Microsoft.NET.Sdk.Web` for ASP.NET Core file-based apps.

#### `#:property` — MSBuild property

```csharp
#:property TargetFramework=net10.0
#:property LangVersion=preview
#:property PublishAot=false
#:property Nullable=enable
```

- Syntax: `#:property <Name>=<Value>` (equals-separated in .NET 10 GA).
- Supports MSBuild property functions and environment variable expansion:

```csharp
// Use an env var with a default fallback
#:property LogLevel=$([MSBuild]::ValueOrDefault('$(LOG_LEVEL)', 'Information'))

// Conditional boolean from env var
#:property EnableLogging=$([System.Convert]::ToBoolean($([MSBuild]::ValueOrDefault('$(ENABLE_LOGGING)', 'true'))))
```

#### `#:project` — project reference

```csharp
#:project ../SharedLibrary/SharedLibrary.csproj
#:project ../ClassLib
```

Allows referencing a class library project from a file-based app.

#### `#:include` — include additional source files

> Available from .NET 11 Preview 3 / .NET SDK 10.0.300.

```csharp
#:include helpers.cs
#:include models/customer.cs
#:include shared/**/*.cs
#:include $(MSBuildProjectName).*.cs
```

Maps by extension: `.cs` → `Compile`, `.resx` → `EmbeddedResource`, `.json` → `None`, `.razor` → `Content`. Included `.cs` files may add types/methods but **cannot contain top-level statements** (only the entry-point file can). Glob patterns disable build caching for that file-based app.

### 2.4 Shebang Support (Unix/Linux/macOS)

Add a shebang line at the very top of the file, before any `#:` directives:

```csharp
#!/usr/bin/env -S dotnet --
#:package Spectre.Console@*

using Spectre.Console;
AnsiConsole.MarkupLine("[green]Hello, World![/]");
```

Then make the file executable and run it directly:

```bash
chmod +x file.cs
./file.cs
```

Key points:

- Use `#!/usr/bin/env -S dotnet --` — the `-S` flag lets `env` split arguments so `--` can be passed as a separate token. The `--` separator tells `dotnet` to pass all subsequent arguments to the app rather than consuming them itself.
- If `-S` is not supported on the target system, use `#!/usr/bin/env dotnet` (without `--`), but note that arguments matching dotnet CLI flags may be consumed by the CLI rather than the app.
- Use **LF line endings** (not CRLF) and **no BOM** — required for Unix shebang parsing.
- Works with extensionless files too: copy `file.cs` to `~/bin/mytool`, `chmod +x`, and invoke as `mytool` from anywhere on `PATH`.

### 2.5 Build, Publish, and Pack

#### Build

```sh
dotnet build file.cs
```

Output goes to `<temp>/dotnet/runfile/<appname>-<appfilesha>/bin/<configuration>/` by default. Override with `--output` or `#:property OutputPath=./output`.

#### Publish (Native AOT by default)

```sh
dotnet publish file.cs
```

File-based apps **target native AOT by default** (`PublishAot=true` is implicit). Output goes to an `artifacts/` subdirectory next to the `.cs` file. Disable AOT:

```csharp
#:property PublishAot=false
```

#### Pack as NuGet tool

```sh
dotnet pack file.cs
```

File-based apps set `PackAsTool=true` by default, producing a `.nupkg` that can be installed or run via `dnx`. Disable with `#:property PackAsTool=false`.

#### Run without restore

```sh
dotnet run file.cs --no-restore
dotnet build file.cs --no-restore
```

### 2.6 `dotnet project convert` — Eject to Full Project

```sh
dotnet project convert file.cs
```

Options:

```
dotnet project convert <FILE>
    [--dry-run]
    [--force]
    [--interactive]
    [-o|--output <OUTPUT_DIRECTORY>]
```

What it does:

1. Creates a new directory named after the file (without `.cs` extension) next to the source file.
2. Generates a `.csproj` file translating all `#:` directives into MSBuild properties, `<PackageReference>` items, `<ProjectReference>` items, and SDK attributes.
3. Copies the `.cs` source into the new directory, stripping the `#:` directives.
4. Leaves the original `.cs` file untouched.

### 2.7 Build Caching

The SDK caches build outputs based on file content, directives, SDK version, and implicit build file content. Cache lives at `<temp>/dotnet/runfile/`. Clear it with:

```sh
dotnet clean file.cs               # clean a specific file-based app
dotnet clean file-based-apps       # clean all cached file-based apps
dotnet clean file-based-apps --days 14   # clean those unused for 14+ days
```

> **Gotcha:** Concurrent invocations of the same file-based app can cause build-output contention. For parallel execution: build first (`dotnet build file.cs`), then run with `--no-build`.

### 2.8 Implicit Build Files

File-based apps respect MSBuild/NuGet configuration files found in the file's directory or any ancestor:

- `Directory.Build.props` / `Directory.Build.targets`
- `Directory.Packages.props` (central package management)
- `nuget.config`
- `global.json`

> **Gotcha:** Do not place file-based apps inside the directory tree of a `.csproj` project — the project's implicit build files will interfere. Keep file-based apps in their own isolated directory.

### 2.9 Other CLI Commands

| Command | Purpose |
|---------|---------|
| `dotnet restore file.cs` | Restore NuGet packages for the file |
| `dotnet clean file.cs` | Clean build outputs |
| `dotnet build file.cs` | Compile without running |
| `dotnet publish file.cs` | Publish (AOT by default) |
| `dotnet pack file.cs` | Pack as NuGet tool |
| `dotnet project convert file.cs` | Eject to full project |
| `dotnet user-secrets set "Key" "Val" --file file.cs` | Store user secrets |

### 2.10 Limitations

| Limitation | Status |
|-----------|--------|
| Multi-file support via `#:include` | Available in .NET SDK 10.0.300+ / .NET 11 Preview 3; not in .NET 10 GA |
| Only C# files supported | VB.NET and F# not planned |
| Visual Studio IDE support | Not supported; use CLI or VS Code |
| JetBrains Rider | Community issue filed; no timeline |
| Top-level statements in included files | Not allowed — only the entry file may have top-level statements |
| Concurrent invocations | Build-output contention; pre-build with `dotnet build` to workaround |
| `Directory.Build.*` interference | Placing file-based app inside a project cone causes conflicts |
| Stdin piping `-` flag | Disables current-directory file search (e.g., launch profiles not read) |

---

## Part 3 — Quick Reference: Shipping a Tool Runnable via `dnx`

Minimal `.csproj` to ship a `dnx`-runnable tool:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>

    <PackAsTool>true</PackAsTool>
    <ToolCommandName>my-tool</ToolCommandName>
    <PackageId>MyOrg.MyTool</PackageId>
    <Version>1.0.0</Version>
  </PropertyGroup>
</Project>
```

Build and publish:

```sh
dotnet pack                        # produces MyOrg.MyTool.1.0.0.nupkg
# Upload to nuget.org or private feed, then:
dnx MyOrg.MyTool [args]            # runs latest
dnx MyOrg.MyTool@1.0.0 [args]     # runs pinned version
```

Minimal file-based app that is also a `dnx`-runnable tool (uses defaults):

```csharp
#!/usr/bin/env -S dotnet --
#:package Spectre.Console@*

using Spectre.Console;
AnsiConsole.MarkupLine($"[green]Hello from {args.FirstOrDefault("World")}![/]");
```

```sh
dotnet pack app.cs    # PackAsTool=true by default
```

---

## Sources

- [What's new in the .NET 10 SDK and tooling — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/sdk)
- [dotnet tool exec command — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-exec)
- [File-based apps — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps)
- [Tutorial: Create a .NET tool — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools-how-to-create)
- [.NET tools overview — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools)
- [dotnet project convert — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-project-convert)
- [Announcing dotnet run app.cs — .NET Blog](https://devblogs.microsoft.com/dotnet/announcing-dotnet-run-app/)
- [Running one-off .NET tools with dnx — Andrew Lock](https://andrewlock.net/exploring-dotnet-10-preview-features-5-running-one-off-dotnet-tools-with-dnx/)
- [Exploring dotnet run app.cs — Andrew Lock](https://andrewlock.net/exploring-dotnet-10-preview-features-1-exploring-the-dotnet-run-app.cs/)
- [.NET Global Tools internals — Nate McMaster (2018, background)](https://natemcmaster.com/blog/2018/05/12/dotnet-global-tools/)
