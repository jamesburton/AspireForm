# AspireForm — MCP Server (Sub-project #3) — Design Spec

- **Date:** 2026-05-25
- **Status:** Approved (design); pending implementation plan
- **Scope:** Sub-project #3 of 5 — the MCP server that exposes AspireForm's verbs to AI agents so they can chat-construct an Aspire application.
- **Predecessors:**
  - Sub-project #1 (Core Engine) shipped as **AspireForm 0.2.0**.
  - Sub-project #2 (Vertical Catalog + plugin loader + 9 plugins) shipped as **AspireForm 0.3.x** + nine plugin packages.

---

## 1. Context

Per the Core-Engine roadmap (§13): *"#3 Agent surface — an MCP server exposing AspireForm's verbs so an agent can chat-construct a project."* The agent surface unlocks the chat-driven UX: a developer (or AI) asks "scaffold me an Aspire app with SQL Server, Redis, and API-key auth," and the agent strings AspireForm verbs to deliver it.

AspireForm already has all the verbs (Plans 1–3 + plugin catalog). What's missing is the **protocol layer** that lets an agent call them with structured arguments and get structured responses, without shelling out to a CLI and parsing text.

The Model Context Protocol (MCP) is the standard wire protocol agents use to call tools. The .NET ecosystem has the official `ModelContextProtocol` NuGet package (Microsoft + Anthropic-aligned) providing JSON-RPC over stdio and HTTP/SSE.

This sub-project adds an `aspireform mcp` verb that starts an MCP server exposing AspireForm's verbs as tools. Same-process; no shell-out.

---

## 2. Locked design decisions

1. **Packaging — new verb in the main AspireForm package.** `aspireform mcp` is a new `AsyncCommand` alongside `plan`, `apply`, etc. One install (`dnx AspireForm mcp`); MCP code colocated with the verb implementations it exposes. Adds `ModelContextProtocol` as a dep to the main package.
2. **Transports — stdio + HTTP/SSE.** stdio is the default (Claude Desktop / Claude Code / Aspire CLI's own MCP pattern). HTTP/SSE on a configurable port for hosted-MCP scenarios. **Localhost-only**; no auth on the HTTP endpoint in v1.
3. **Tool surface — low-level mirror + curated macros.**
   - **14 low-level tools**, one per CLI verb (one-to-one mapping).
   - **3 curated macros** for common end-to-end recipes.
4. **Execution model — in-process, same services as CLI.** Each tool handler invokes the same internal services the CLI commands use (`ConfigLoader`, `PluginManager`, `Planner`, `Executor`, `StateStore`, `AspireCli`). No shell-out — full error fidelity, no subprocess overhead, single source of truth.
5. **Decomposition — one plan.** Plan 3.0 covers MCP foundation + all 14 low-level tools + 3 macros + tests + release. ~20–25 tasks; most tools are templated boilerplate once the foundation is in place.
6. **SDK — `ModelContextProtocol` NuGet.** Microsoft's official .NET MCP SDK. If the SDK turns out to be unavailable or unsuitable at implementation time, fall back to a thin in-house JSON-RPC layer (the MCP-tools surface needed is small enough — JSON-RPC method `tools/call` + a few capability ones).
7. **Ships as AspireForm 0.4.0** via the existing `v*` release-workflow path.

---

## 3. Architecture

```
src/AspireForm/
  Cli/McpCommand.cs                    NEW — Spectre AsyncCommand for `aspireform mcp`
  Mcp/                                 NEW
    McpServer.cs                       transport-agnostic server: dispatch tools/call
    StdioTransport.cs                  reads JSON-RPC from stdin, writes to stdout
    HttpSseTransport.cs                Kestrel-hosted SSE endpoint
    ToolRegistry.cs                    holds tool handlers + schemas
    IToolHandler.cs                    contract: Name, Description, InputSchema, ExecuteAsync(args)
    Tools/                             one file per low-level verb tool
      ConfigTool.cs
      PlanTool.cs
      ApplyTool.cs
      DestroyTool.cs
      NewTool.cs
      AddTool.cs
      ImportTool.cs
      StateListTool.cs
      StateShowTool.cs
      DoctorTool.cs
      PluginListTool.cs
      PluginInstallTool.cs
      PluginUpdateTool.cs
      PluginRemoveTool.cs
    Tools/Macros/                      curated higher-level recipes
      ScaffoldAspireAppWithDataTool.cs
      AddCacheLayerTool.cs
      AddAuthenticationTool.cs

src/AspireForm/AspireForm.csproj       MODIFY — add ModelContextProtocol dep, bump 0.3.2 → 0.4.0
src/AspireForm/Program.cs              MODIFY — register `mcp` verb

tests/AspireForm.Tests/Mcp/            NEW per-tool unit tests + transport integration tests

README.md                              MODIFY — add MCP section + sample agent config snippet
CHANGELOG.md                           MODIFY — add [0.4.0] section
```

### 3.1 Pipeline

```
agent / client
     │  JSON-RPC over stdio | HTTP-SSE
     ▼
┌──────────────────┐
│  McpServer       │  ── reads frame ──>  parses tools/call  ──>  ToolRegistry
└──────────────────┘                                                   │
                                                                       ▼
                                                              IToolHandler.ExecuteAsync(args)
                                                                       │
                                                                       ▼
                                              ConfigLoader / PluginManager / Planner /
                                              Executor / StateStore / AspireCli
                                                                       │
                                                                       ▼
                                                              structured result + diagnostics
                                                                       │
                                                                       ▼
                                                              JSON-RPC response frame
```

### 3.2 Tool contract

```csharp
public interface IToolHandler
{
    string Name { get; }                       // e.g. "aspireform_plan"
    string Description { get; }                 // human-readable, surfaced to the agent
    JsonObject InputSchema { get; }             // JSON Schema for the tool's inputs
    Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken ct);
}

public sealed record ToolResult(
    bool IsError,                               // maps to MCP isError
    IReadOnlyList<ToolContent> Content);        // text or structured chunks

public sealed record ToolContent(string Type, string Text);
```

Most tools return a single `text`-type `ToolContent` with either the underlying verb's stdout or a structured JSON summary.

---

## 4. Tool surface

### 4.1 Low-level (14 — one per CLI verb)

| Tool name | Inputs | Returns |
|---|---|---|
| `aspireform_new` | `name`, `output?` | "Created <dir> with starter aspireform.yaml" + file list |
| `aspireform_add` | `type`, `name?`, `module?` (bool), `dependsOn?` (string[]), `projectDir?` | "Added <kind> '<name>' (<type>) to aspireform.yaml" |
| `aspireform_config` | `env?`, `projectDir?` | The merged + interpolated JSON config |
| `aspireform_plan` | `env?`, `projectDir?` | Rendered plan output (unified diffs) |
| `aspireform_apply` | `env?`, `projectDir?`, `forceDrift?` (bool) | "Applied N block(s)" + plan output |
| `aspireform_destroy` | `block?`, `projectDir?`, `allowModuleDestroy?` (bool) | "Destroyed N block(s)" |
| `aspireform_import` | `block`, `projectDir?` | "Imported '<block>'" |
| `aspireform_state_list` | `projectDir?` | Tabular text |
| `aspireform_state_show` | `block`, `projectDir?` | Indented JSON for that block |
| `aspireform_doctor` | — | Tabular text |
| `aspireform_plugin_list` | `projectDir?` | Tabular text |
| `aspireform_plugin_install` | `name`, `projectDir?` | "Installed X (Y Z)" |
| `aspireform_plugin_update` | `name`, `projectDir?` | "Updated X: A → B" |
| `aspireform_plugin_remove` | `name`, `projectDir?` | "Removed X" |

### 4.2 Macros (3 curated recipes)

| Tool name | Inputs | What it does |
|---|---|---|
| `scaffold_aspire_app_with_data` | `name`, `output?`, `databaseName?` (default "appdb") | `new` → `add sqlserver` → `add ef-data dependsOn=[sql]` → `plan` → `apply --yes`. Returns the rendered plan + apply summary. |
| `add_cache_layer` | `projectDir`, `name?` (default "cache") | `add redis name` → `plan` → `apply --yes`. |
| `add_authentication` | `projectDir`, `name?` (default "auth"), `variant` (apikey \| magiclink \| entra), `inputs?` (variant-specific) | If the corresponding plugin isn't installed, auto-install via plugin manager. `add auth-<variant>` → `plan` → `apply --yes`. |

Macros return a structured summary listing each underlying step + its outcome.

---

## 5. Transport

### 5.1 stdio (default)

```bash
aspireform mcp
```

JSON-RPC 2.0 framed messages on stdin/stdout. This is what agents like Claude Desktop, Claude Code, and the Aspire CLI's own MCP support already invoke.

### 5.2 HTTP/SSE

```bash
aspireform mcp --http --port 5050
```

Kestrel-hosted SSE per the MCP HTTP-SSE binding. Localhost-only binding by default. **No authentication on the endpoint in v1** — dev-tool scope; document that opening to non-localhost is at user's risk.

### 5.3 Shared options

```bash
aspireform mcp --project-dir <DIR>           # default projectDir for tools whose inputs omit it
aspireform mcp --log-level <debug|info|warn|error>  # passed through to all tool handlers
```

---

## 6. Error model

Each tool handler catches the same exception set the corresponding CLI command catches:
`ConfigValidationException`, `StateException`, `PluginContractException`, `DependencyCycleException`, `ProviderNotFoundException`.

Mapping:
- Known/typed errors → MCP JSON-RPC error with code `-32001` (server error range) + descriptive `message` + the underlying exception type as `data.type`.
- Unhandled exceptions → MCP `InternalError` (`-32603`) + exception type + message.
- Tool-level "expected failures" (e.g. "Block 'X' is not tracked") → `ToolResult(IsError: true, Content: [text("…")])` — surfaced to the agent as a tool result with `isError: true`, NOT a transport-level error.

The distinction matters: transport-level errors abort the agent's reasoning loop; tool-result errors let the agent recover and try a different approach.

---

## 7. Execution model

Tools invoke the same internal services the CLI uses:

```csharp
// Example: PlanTool
var loaded = new ConfigLoader().Load(projectDir, env);
var state = new StateStore().Load(projectDir);
var registry = await new PluginManager().DiscoverAndLoadAsync(loaded.Model, projectDir, ct);
var plan = new Planner(registry).Plan(loaded.Model, state, projectDir);
var rendered = PlanRenderer.Render(plan);
return new ToolResult(IsError: false, [new ToolContent("text", rendered)]);
```

No subprocess. No CLI-output text parsing. Single source of truth for the verb's behavior.

---

## 8. Testing strategy

- **Per-tool unit tests:** each `IToolHandler` implementation gets a test class. Tests construct the handler, supply a fixture `projectDir`, call `ExecuteAsync`, and assert the `ToolResult`. Skips the transport.
- **Macro tests:** test the composing tool returns the right structured summary; integration-level for steps it orchestrates.
- **stdio transport test:** spin up `McpServer` with a `MemoryStream`-backed transport, send a `tools/list` and a `tools/call`, assert the responses round-trip.
- **HTTP/SSE transport test:** start the server on an ephemeral port, `HttpClient` against it for a single tool call.

xUnit v3 / MTP / AwesomeAssertions throughout.

---

## 9. Scope boundaries — explicitly NOT in this sub-project

- Auth/authorization on the HTTP/SSE endpoint (dev-only; future hardening pass).
- Non-localhost HTTP binding.
- MCP **resources** and **prompts** (this version exposes only **tools**).
- MCP **sampling** (client-callback for LLM inference inside a tool) — not needed for the verb surface.
- Streaming partial results (each tool returns a single response).
- Persistent server state — each tool call is self-contained.
- Builder UIs (sub-project #4).
- Stretch goals (sub-project #5).

---

## 10. Risks & open questions

1. **`ModelContextProtocol` NuGet maturity.** Microsoft's SDK is the natural choice but its API surface and versioning may not yet be stable. Mitigation: the in-house JSON-RPC fallback option in §2; the protocol surface we need is small.
2. **HTTP/SSE binding to a real port.** Kestrel hosting from inside a dotnet-tool process is fine but adds startup cost. Acceptable for v1 since HTTP is opt-in via `--http`.
3. **Macro tool dependency-resolution.** `add_authentication` needs to know whether the user already has the relevant auth plugin installed — leverages existing `PluginManager.DiscoverAndLoadAsync` short-circuit logic.
4. **Tool naming convention.** `aspireform_<verb>` is verbose but namespaced; collides with no other agent-side tools. Alternative `af_<verb>` is shorter — chosen verbose for clarity.

---

## 11. Definition of done (sub-project #3)

- `aspireform mcp` runs an MCP server (stdio + HTTP/SSE).
- All 14 low-level verb tools registered and behave end-to-end.
- All 3 macros registered and orchestrate their step sequences.
- Tests: per-tool unit + stdio integration + HTTP/SSE smoke; xUnit v3 / MTP suite green.
- AspireForm 0.4.0 ready to ship via `v0.4.0` tag through the existing release workflow.
- README has a "Use with an agent" section + a Claude Code / Claude Desktop config-snippet example.
