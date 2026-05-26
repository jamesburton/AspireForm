# AspireForm

Declarative construction and configuration of [.NET Aspire](https://aspire.dev) applications —
Infrastructure-as-Code ideas (Terraform) and declarative orchestration (Docker Compose) applied
to scaffolding and evolving an Aspire solution.

You describe the desired shape of your app in `aspireform.yaml` (or `aspireform.jsonc`); AspireForm
reconciles that against what is on disk and applies the difference.

## Status

v0.8.0 — API endpoint builder. AspireForm now includes a code-first Minimal API endpoint builder:
`[ApiEndpoint]` attributes on handler methods are scanned via Roslyn, exposed through 10 MCP tools,
editable in the `aspireform ui` `/endpoints` page, and emitted to `_Endpoints.g.cs` by the built-in
`api-endpoints` module provider. `AspireForm.Annotations 0.2.0` adds the four endpoint attributes.
Theme Editor (v0.7.0) also available at `aspireform ui` `/theme`.

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

**12 entity tools** drive code-first EF Core entity authoring:
`aspireform_entity_{list,show,create,delete}`, `aspireform_property_{add,remove,rename}`, `aspireform_attribute_{set,clear}`, `aspireform_relationship_{add,remove}`, `aspireform_dbcontext_list`.

**10 endpoint tools** drive code-first Minimal API endpoint authoring:
`aspireform_endpoint_{list,show,emit,create,delete}`, `aspireform_endpoint_parameter_{add,remove}`, `aspireform_endpoint_auth_set`, `aspireform_endpoint_attribute_{set,clear}`.

**3 curated macros** orchestrate common recipes:
`scaffold_aspire_app_with_data`, `add_cache_layer`, `add_authentication`.

> **Security:** the HTTP transport binds localhost only and has no authentication in this version. Do not expose it on a public interface.

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

12 MCP tools cover full CRUD: `aspireform_entity_{list,show,create,delete}`, `aspireform_property_{add,remove,rename}`, `aspireform_attribute_{set,clear}`, `aspireform_relationship_{add,remove}`, `aspireform_dbcontext_list`.

> **Security:** the UI binds localhost only and has no authentication. Dev-tool use only.

## Use the theme editor

`aspireform ui` includes a **Theme** tab where you can adjust the color tokens that govern the
AspireForm UI shell. Changes are saved to `.aspireform/theme.json` in your project directory and
take effect immediately (no page reload required).

```bash
aspireform ui           # then navigate to http://localhost:5050/theme
```

The token map can also be read by an agent via the `aspireform_theme_show` MCP tool.

> **Scope:** The theme editor styles the AspireForm UI shell only. It does not modify your
> project's own CSS or scaffold any code into your Aspire solution.

## Use the API builder

`aspireform ui` also includes an `/endpoints` page where you browse and edit Minimal API endpoints
defined in your Web project. The page discovers all methods decorated with `[ApiEndpoint]` via
Roslyn and lets you inspect route parameters, set auth policies, and manage method-level attributes.

### Code-first API authoring

Reference the `AspireForm.Annotations` package from your Web project, then decorate handler methods:

```csharp
using AspireForm.Annotations;

public class BooksHandler
{
    [ApiEndpoint("/books", "GET")]
    [ApiSummary("Return all books")]
    [ApiAuth("authenticated")]
    [ApiTag("Books")]
    public static IResult GetBooks() => Results.Ok(new[] { "Dune", "Foundation" });

    [ApiEndpoint("/books/{id:int}", "GET")]
    public static IResult GetBook(int id) => Results.Ok(new { id });
}
```

When the `api-endpoints` block in your `aspireform.yaml` points at this project, `aspireform plan`
emits `_Endpoints.g.cs` with a `MapAspireFormEndpoints` extension method that wires every decorated
handler into the Minimal API pipeline:

```yaml
modules:
  api:
    type: api-endpoints
    inputs:
      projectPath: ./MyWebApp
```

### API endpoint MCP tools

10 MCP tools let agents drive the endpoint catalog: `aspireform_endpoint_{list,show,emit,create,delete}`,
`aspireform_endpoint_parameter_{add,remove}`, `aspireform_endpoint_auth_set`,
`aspireform_endpoint_attribute_{set,clear}`.

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
