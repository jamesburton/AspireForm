# AspireForm

Declarative construction and configuration of [.NET Aspire](https://aspire.dev) applications —
Infrastructure-as-Code ideas (Terraform) and declarative orchestration (Docker Compose) applied
to scaffolding and evolving an Aspire solution.

You describe the desired shape of your app in `aspireform.yaml` (or `aspireform.jsonc`); AspireForm
reconciles that against what is on disk and applies the difference.

## Status

v0.3.0 — Plugin loader. AspireForm now supports external NuGet plugins; the first one
(`AspireForm.Plugin.Redis`) is available. More verticals (Mailpit, Hangfire, DAB, auth × 3,
reporting, ETL) arrive in Plans 2.1–2.9.

## Install / run

AspireForm is a zero-install .NET tool. With the .NET 10 SDK present:

    dnx AspireForm config
    dnx AspireForm doctor

`dnx` resolves the latest published version on each run, so the tool is always current.

## Commands

| Command | Description |
|---|---|
| `aspireform new <name>` | Scaffold a new Aspire solution + a starter `aspireform.yaml`. |
| `aspireform add <type> [name]` | Append a Resource (or Module via `--module`) block to the config (comments and formatting are not preserved). |
| `aspireform config` | Print the fully merged, interpolated desired-state configuration. |
| `aspireform plan` | Show the reconciliation diff between desired and current state. |
| `aspireform apply` | Execute the plan after an approval gate (skip with `--yes`). |
| `aspireform destroy [block]` | Remove one block (or all blocks) from state. |
| `aspireform import <block>` | Adopt an existing block into state without executing. |
| `aspireform state list` | List every tracked block. |
| `aspireform state show <block>` | Dump one block's state as JSON. |
| `aspireform doctor` | Check prerequisites (.NET 10 SDK + `aspire` CLI). |
| `aspireform plugin list` | List installed plugins. |
| `aspireform plugin install <name>[@version]` | Install a plugin (auto-restore handles unknown types on next plan/apply). |
| `aspireform plugin update <name>` | Update a plugin to the latest version. |
| `aspireform plugin remove <name>` | Remove a plugin from the lockfile. |

## Use with an agent (MCP server)

AspireForm includes an MCP server that exposes its verbs as tools, so AI agents can chat-construct an Aspire app:

```bash
aspireform mcp                         # stdio (default — for Claude Desktop / Claude Code / Aspire CLI)
aspireform mcp --http --port 5050      # localhost HTTP transport
aspireform mcp --project-dir ./myapp   # set the default projectDir for tool calls
```

### Claude Code config snippet

Add to `~/.claude/mcp.json` (or the project-scoped equivalent):

```json
{
  "mcpServers": {
    "aspireform": {
      "command": "dnx",
      "args": ["AspireForm", "mcp"]
    }
  }
}
```

### Tool surface

**14 low-level tools** mirror the CLI verbs:
`aspireform_new`, `aspireform_add`, `aspireform_config`, `aspireform_plan`, `aspireform_apply`, `aspireform_destroy`, `aspireform_import`, `aspireform_state_list`, `aspireform_state_show`, `aspireform_doctor`, `aspireform_plugin_list`, `aspireform_plugin_install`, `aspireform_plugin_update`, `aspireform_plugin_remove`.

**3 curated macros** orchestrate common recipes:
`scaffold_aspire_app_with_data`, `add_cache_layer`, `add_authentication`.

> **Security:** the HTTP transport binds localhost only and has no authentication in this version. Do not expose it on a public interface.

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

## Plugins

AspireForm supports external plugins that contribute new block types (Resources or Modules)
via NuGet packages. The first available plugin is **Redis**:

| Plugin | Block type | NuGet |
|---|---|---|
| [AspireForm.Plugin.Redis](https://www.nuget.org/packages/AspireForm.Plugin.Redis) | `redis` | 0.1.0 |

When you reference an unknown block `type` in `aspireform.yaml` (e.g. `type: redis`), AspireForm
auto-restores the matching plugin from NuGet on the next `plan` or `apply`. The resolved
(name, version) pair is recorded in `.aspireform/plugins.lock.yaml` (committed to git).

### Script plugins (`.cs` files)

For quick local extension without packaging, drop a `.cs` file into `.aspireform/scripts/`.
AspireForm compiles it via Roslyn into the same plugin context as NuGet plugins. Use
`#:package <id>@<version>` directives at the top of the file to declare NuGet dependencies.

```csharp
#:package Some.Helper.Lib@1.2.3

using AspireForm.Providers;

public sealed class MyVerticalProvider : IProvider
{
    public string Type => "my-vertical";
    public BlockKind Kind => BlockKind.Module;
    public ProviderPlan Plan(PlanContext context) => new() { /* ... */ };
}
```

The compiler caches by source-hash at `.aspireform/scripts/.cache/<sha256>/` — unchanged scripts
skip recompile.

## Documentation

- Design spec: `docs/superpowers/specs/`
- Research notes: `docs/research/`
- Implementation plans: `docs/superpowers/plans/`
