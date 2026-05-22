# .NET Aspire — Current State (May 2026)

> Research compiled May 22, 2026. All version facts verified against aspire.dev, Microsoft Learn, and official GitHub releases.

---

## 1. What Aspire Is Now

### The Rebrand and Version Jump

**.NET Aspire** became simply **Aspire** at version 13.0, released **November 11, 2025**. The product decoupled its version numbering from .NET's release cadence, skipping versions 10, 11, and 12 entirely — the jump from **9.5** directly to **13.0** was intentional, signalling a major platform repositioning. Aspire now follows an **evergreen model**: only the latest release is supported by Microsoft (Modern Lifecycle).

**Current version as of May 2026:** `13.3.4` (released May 19, 2026; the 13.3.0 GA dropped May 7, 2026).

#### Release Timeline — 13.x Series

| Version | Released        | Headline |
|---------|----------------|----------|
| 13.0    | Nov 11, 2025   | Rebrand; multi-language first class (Python, JS); `aspire do` pipeline; single-file AppHost |
| 13.1    | Dec 17, 2025   | MCP for AI agents; `aspire agent init`; Azure Functions GA; DevTunnels stable |
| 13.2    | Mar 23, 2026   | `aspire start/stop/ps`; isolated mode; TypeScript AppHost preview; Docker Compose publishing stable |
| 13.3    | May 7, 2026    | Kubernetes/AKS preview; `aspire destroy`; `aspire dashboard`; browser logs; Next.js first-class |

### What Aspire Is

Aspire is a **code-first, extensible orchestration platform** for building and running distributed applications — locally and in production — across .NET, Python, JavaScript/TypeScript, Go, Java, Rust, and more. Its three core pillars are:

1. **Orchestration** — declare your application topology (services, databases, queues, frontends) in a single AppHost file; Aspire manages startup ordering, health monitoring, port assignment, and service discovery automatically.
2. **Integrations** — a curated catalogue of hosting and client packages for popular infrastructure (databases, messaging, caches, AI, cloud services).
3. **Observability** — a built-in developer dashboard with structured logs, distributed traces, resource health, and metrics via OpenTelemetry.

### The App Model

The distributed application builder is the heart of every AppHost:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");
var db    = builder.AddPostgres("postgres").AddDatabase("appdb");
var api   = builder.AddProject<Projects.Api>("api")
                   .WithReference(cache)
                   .WithReference(db)
                   .WaitFor(db);

builder.AddProject<Projects.Web>("web")
       .WithReference(api)
       .WithExternalHttpEndpoints();

builder.Build().Run();
```

The builder:
- Adds **resources** (containers, projects, connection strings, parameters)
- Declares **dependencies** via `.WithReference()` (injects connection strings / environment variables) and `.WaitFor()` (startup ordering)
- Calls `.Build()` then `.Run()` to start the DCP (Distributed Composition Platform) orchestrator

Aspire requires **.NET 10 SDK or later** for the AppHost and integrations packages as of the 13.x series.

---

## 2. The Aspire CLI

The Aspire CLI is a standalone, NativeAOT-compiled tool (instant startup, no JIT warmup) installed separately from the .NET SDK.

### Installation

```powershell
# Windows
irm https://aspire.dev/install.ps1 | iex

# macOS / Linux
curl -sSL https://aspire.dev/install.sh | bash

# Also available as a dotnet global tool
dotnet tool install -g Aspire.Cli
```

Current CLI version matches the NuGet package `Aspire.Cli` — **13.3.3** as of May 2026.

### Core CLI Commands

#### Project Scaffolding

| Command | Description |
|---------|-------------|
| `aspire new` | Interactive template picker — creates one or more Aspire projects from curated starters (Blazor + API, React + FastAPI, empty AppHost, etc.) |
| `aspire init` | Adds Aspire support to an existing repo or workspace; scaffolds AppHost project alongside existing projects |

#### Integration Management

| Command | Description |
|---------|-------------|
| `aspire add [<integration>]` | Searches for and installs a hosting integration NuGet package into the AppHost project. Fuzzy-searches NuGet if a partial name is given. Records AppHost selection in `aspire.config.json`. |
| `aspire restore` | Restores NuGet packages for C# AppHosts; regenerates SDK code for TypeScript AppHosts |
| `aspire update` | Detects and updates outdated Aspire packages while respecting channel settings |

**How `aspire add` works end-to-end:**

1. Run `aspire add redis` from anywhere in the repo.
2. CLI locates the AppHost project (via `aspire.config.json` or automatic discovery).
3. If the name is ambiguous or missing, CLI queries NuGet and presents a filterable list.
4. Adds the `Aspire.Hosting.Redis` package reference to the AppHost `.csproj`.
5. Optionally scaffolds a starter code snippet showing `builder.AddRedis("cache")`.

#### Running Locally

| Command | Description |
|---------|-------------|
| `aspire run` | Runs the AppHost in development mode — starts DCP, launches all resources, opens the dashboard |
| `aspire start` | Starts the AppHost in **detached/background mode** (shorthand for `aspire run --detach`); frees the terminal |
| `aspire stop` | Halts a running AppHost (prompts if multiple are running) |
| `aspire ps` | Lists all running Aspire AppHost processes with paths, PIDs, and dashboard URLs |
| `aspire wait <resource> --status healthy --timeout 120` | Blocks until the named resource reaches the specified status — useful in CI and agent workflows |
| `aspire describe` | Inspects running resources; supports `--follow` for real-time streaming |
| `aspire logs` | Streams resource logs to the terminal |

The `--isolated` flag on `aspire run` / `aspire start` runs the AppHost with randomised ports and isolated user secrets, enabling multiple instances to run simultaneously without port conflicts (useful for parallel CI runs and agentic workflows).

#### Publishing and Deployment

| Command | Description |
|---------|-------------|
| `aspire publish` | Transforms the AppHost model into deployment artifacts (Docker Compose files, Kubernetes Helm charts, Bicep templates). Secrets remain as parameterised placeholders. |
| `aspire deploy` | Resolves parameters, applies configuration, and executes deployment to the target environment (Azure Container Apps, AKS, Docker Compose, etc.) |
| `aspire destroy` | Tears down a previously deployed environment across Azure, Kubernetes, or Docker Compose |
| `aspire do <step>` | Executes a specific pipeline step and its dependencies — replaces the old publishing callback infrastructure |

#### Diagnostics and Utilities

| Command | Description |
|---------|-------------|
| `aspire doctor` | Verifies development environment readiness (HTTPS certs, container runtime, .NET SDK, agent configuration) |
| `aspire certs` | Manages HTTPS development certificates |
| `aspire config` | Views/sets/deletes CLI settings and feature flags (via `list`, `get`, `set`, `delete`) |
| `aspire secret` | Manages user secrets for sensitive values |
| `aspire cache clear` | Clears the CLI disk cache |
| `aspire export` | Packages telemetry and resource data for debug snapshots (exports traces/spans/logs as JSON, env vars as `.env`) |
| `aspire otel` | Accesses OpenTelemetry data (logs, spans, traces) directly from the terminal |
| `aspire dashboard` | Runs the Aspire dashboard **standalone** to consume OTLP telemetry from any source — not just Aspire apps |
| `aspire resource start|stop|restart <name>` | Executes resource-level lifecycle commands |

#### AI Agent Commands

| Command | Description |
|---------|-------------|
| `aspire agent init` | Sets up MCP configuration files for VS Code, GitHub Copilot CLI, Claude Code, and OpenCode; installs skill files |
| `aspire agent mcp` | Starts the MCP server (used as the subprocess target by MCP client config) |
| `aspire mcp tools` | Lists available MCP tools |
| `aspire mcp call <tool>` | Calls a specific MCP tool directly |

#### Documentation

| Command | Description |
|---------|-------------|
| `aspire docs` | Browses and searches official aspire.dev documentation |
| `aspire docs search <query>` | Keyword searches across docs |
| `aspire docs get <slug>` | Retrieves a specific doc page |
| `aspire docs api` | Searches and views API reference from the terminal (added in 13.3) |

#### Configuration File

Aspire 13.2 consolidated project settings into a single **`aspire.config.json`** file (replacing the previous split between `.aspire/settings.json` and `apphost.run.json`). Legacy config is auto-migrated.

### Templates

`aspire new` offers curated starters including:

- **aspire-starter** — Blazor frontend + .NET Web API backend (C#)
- **aspire-py-starter** — Python FastAPI backend with React frontend
- **aspire-ts-cs-starter** — TypeScript (Vite/React) frontend + C# backend
- **aspire** — Empty AppHost (C#)
- Custom templates can be registered and surfaced.

> **Note:** The old `dotnet new aspire-*` template commands still work but `aspire new` is the recommended path in 13.x.

---

## 3. Integrations Catalogue

Aspire integrations come in two complementary forms:

- **Hosting integrations** — live in the AppHost project; model infrastructure resources (containers, cloud services). NuGet namespace: `Aspire.Hosting.*`.
- **Client integrations** — live in consuming service projects; configure DI registrations, health checks, telemetry, and resiliency for that service. NuGet namespace: `Aspire.*`.

### 3.1 Official Hosting Integrations

#### Databases

| Resource | Package | Builder Method |
|----------|---------|----------------|
| SQL Server | `Aspire.Hosting.SqlServer` | `builder.AddSqlServer("sql")` |
| PostgreSQL | `Aspire.Hosting.PostgreSQL` | `builder.AddPostgres("pg")` |
| MySQL | `Aspire.Hosting.MySql` | `builder.AddMySQL("mysql")` |
| MongoDB | `Aspire.Hosting.MongoDB` | `builder.AddMongoDB("mongo")` |
| Redis | `Aspire.Hosting.Redis` | `builder.AddRedis("cache")` |
| Garnet | `Aspire.Hosting.Garnet` | `builder.AddGarnet("garnet")` |
| Valkey | `Aspire.Hosting.Valkey` | `builder.AddValkey("valkey")` |
| Elasticsearch | `Aspire.Hosting.Elasticsearch` | `builder.AddElasticsearch("es")` |
| Meilisearch | `Aspire.Hosting.Meilisearch` | `builder.AddMeilisearch("search")` |
| Qdrant | `Aspire.Hosting.Qdrant` | `builder.AddQdrant("qdrant")` |
| RavenDB | `Aspire.Hosting.RavenDB` | `builder.AddRavenDB("raven")` |
| ClickHouse | `Aspire.Hosting.ClickHouse` | `builder.AddClickHouse("ch")` |
| Milvus | `Aspire.Hosting.Milvus` | `builder.AddMilvus("milvus")` |
| SurrealDB | `Aspire.Hosting.SurrealDB` | `builder.AddSurrealDB("surreal")` |
| KurrentDB | `Aspire.Hosting.KurrentDB` | `builder.AddKurrentDB("kurrent")` |
| SQLite | `Aspire.Hosting.SQLite` | `builder.AddSqlite("db")` |

Adding a database to a SQL Server or PostgreSQL resource:

```csharp
var sql   = builder.AddSqlServer("sql").WithLifetime(ContainerLifetime.Persistent);
var myDb  = sql.AddDatabase("mydb");

var pg    = builder.AddPostgres("pg").WithDataVolume();
var appDb = pg.AddDatabase("appdb");
```

#### Messaging

| Resource | Package | Builder Method |
|----------|---------|----------------|
| RabbitMQ | `Aspire.Hosting.RabbitMQ` | `builder.AddRabbitMQ("rabbit")` |
| Apache Kafka | `Aspire.Hosting.Kafka` | `builder.AddKafka("kafka")` |
| NATS | `Aspire.Hosting.NATS` | `builder.AddNats("nats")` |
| LavinMQ | `Aspire.Hosting.LavinMQ` | `builder.AddLavinMQ("lavinmq")` |

#### AI / Machine Learning

| Resource | Package | Builder Method |
|----------|---------|----------------|
| Ollama | `Aspire.Hosting.Ollama` | `builder.AddOllama("ollama")` |
| OpenAI | `Aspire.Hosting.OpenAI` | `builder.AddOpenAI()` |
| GitHub Models | `Aspire.Hosting.GitHub` | `builder.AddGitHubModels()` |

#### Observability

| Resource | Package | Builder Method |
|----------|---------|----------------|
| Seq | `Aspire.Hosting.Seq` | `builder.AddSeq("seq")` |

#### Security / Auth

| Resource | Package | Builder Method |
|----------|---------|----------------|
| Keycloak | `Aspire.Hosting.Keycloak` | `builder.AddKeycloak("keycloak")` |

Keycloak supports realm import via `.WithRealmImport("realms/")`, data volumes, and OTLP telemetry export. The companion client package `Aspire.Keycloak.Authentication` provides `AddKeycloakJwtBearer()` and `AddKeycloakOpenIdConnect()` DI extensions.

#### Dev Tools

| Resource | Package |
|----------|---------|
| Dev Tunnels | `Aspire.Hosting.DevTunnels` (stable since 13.1) |
| SQL Database Projects | `Aspire.Hosting.SqlDatabaseProjects` |
| flagd / goff | `Aspire.Hosting.flagd` |
| k6 load testing | `Aspire.Hosting.k6` |

#### Language Runtimes (First-Class)

| Language | Package | Builder Methods |
|----------|---------|-----------------|
| Python | `Aspire.Hosting.Python` | `builder.AddPythonApp()`, `AddPythonModule()`, `AddPythonExecutable()` |
| JavaScript/Node.js | `Aspire.Hosting.JavaScript` | `builder.AddJavaScriptApp()` |
| .NET (projects) | Built-in | `builder.AddProject<T>()` |
| Go | `Aspire.Hosting.Go` | `builder.AddGoApp()` |
| Java | `Aspire.Hosting.Java` | `builder.AddJavaApp()` |
| Rust | `Aspire.Hosting.Rust` | `builder.AddRustApp()` |

> **Note:** `Aspire.Hosting.NodeJs` was renamed to `Aspire.Hosting.JavaScript` at 13.0; `AddNpmApp` is deprecated in favour of `AddJavaScriptApp`.

### 3.2 Official Azure Hosting Integrations

All live under `Aspire.Hosting.Azure.*`. When running locally, Aspire provisions resources using **Azurite** (storage emulation) or real Azure resources via the Azure Developer CLI. In production, Aspire generates Bicep templates.

| Azure Service | Notes |
|---------------|-------|
| App Service | Dashboard included; Application Insights integration; deployment slots via `.WithDeploymentSlot()` |
| Application Insights | |
| Azure AI Inference | |
| Azure AI Search | |
| Azure Cache for Redis | Managed Redis via `AddAzureManagedRedis()` (renamed from `AddAzureRedisEnterprise` in 13.1) |
| Azure Container Apps | Core production target |
| Azure Container App Jobs | |
| Azure Container Registry | Explicit registry configuration |
| Azure Cosmos DB | |
| Azure Data Explorer | |
| Azure Data Lake Storage | Added 13.2 |
| Azure Event Hubs | |
| Azure Front Door | `AddAzureFrontDoor()` with origin attachment — added 13.3 |
| Azure Functions | GA since 13.1; supports ACA Native Functions with KEDA auto-scaling |
| Azure Key Vault | |
| Azure Kubernetes Service (AKS) | Preview since 13.3; `AddAzureKubernetesEnvironment()` |
| Azure Log Analytics | |
| Azure PostgreSQL | |
| Azure Service Bus | |
| Azure SignalR Service | |
| Azure SQL Database | |
| Azure Storage Blobs | |
| Azure Storage Queues | |
| Azure Storage Tables | |
| Azure Virtual Network | Network Security Groups, subnets, NAT gateways, private endpoints; added 13.2 |
| Azure Web PubSub | |
| Microsoft Foundry | Replaces Azure AI Foundry (renamed 13.2); hosted agents support |
| Network Security Perimeters | Logical security boundaries for Azure PaaS; added 13.3 |

### 3.3 AWS Integrations

`Aspire.Hosting.AWS` provides support for AWS resources, though it is less comprehensive than the Azure catalogue.

### 3.4 Official Client Integrations (consuming-project side)

These packages are added to your API/service projects (not the AppHost):

| Client Package | Provides |
|----------------|----------|
| `Aspire.StackExchange.Redis` | Redis `IConnectionMultiplexer` with health checks + telemetry |
| `Aspire.StackExchange.Redis.DistributedCaching` | `IDistributedCache` over Redis |
| `Aspire.StackExchange.Redis.OutputCaching` | ASP.NET Core output cache over Redis |
| `Aspire.Npgsql` | Npgsql `NpgsqlDataSource` with health checks + telemetry |
| `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` | EF Core with Npgsql |
| `Aspire.Microsoft.EntityFrameworkCore.SqlServer` | EF Core with SQL Server |
| `Aspire.Microsoft.Data.SqlClient` | `SqlConnection` / `SqlDataSource` |
| `Aspire.MongoDB.Driver` | MongoDB `IMongoClient` |
| `Aspire.RabbitMQ.Client` | RabbitMQ `IConnection` |
| `Aspire.NATS.Net` | NATS `INatsClient` |
| `Aspire.Confluent.Kafka` | Kafka producer / consumer |
| `Aspire.Azure.Messaging.ServiceBus` | `ServiceBusClient` |
| `Aspire.Azure.Security.KeyVault` | `SecretClient` / `KeyClient` |
| `Aspire.Azure.Storage.Blobs` | `BlobServiceClient` |
| `Aspire.Azure.Storage.Queues` | `QueueServiceClient` |
| `Aspire.Azure.Data.Tables` | `TableServiceClient` |
| `Aspire.Azure.Cosmos.Db` | `CosmosClient` |
| `Aspire.Azure.AI.OpenAI` | `AzureOpenAIClient` |
| `Aspire.Microsoft.Azure.Cosmos` | Cosmos DB with EF Core |
| `Aspire.Keycloak.Authentication` | Keycloak JWT Bearer / OIDC handlers |
| `Aspire.Elastic.Clients.Elasticsearch` | Elasticsearch `ElasticsearchClient` |
| `Aspire.Milvus.Client` | Milvus vector DB client |
| `Aspire.Qdrant.Client` | Qdrant `QdrantClient` |
| `Aspire.OllamaSharp` | Ollama `IOllamaApiClient` |

All client integrations automatically wire up:
- **Health checks** (registered in the ASP.NET Core health check system)
- **OpenTelemetry** (traces, metrics, logs)
- **Resiliency** (via `AddStandardResilienceHandler` by default in Service Defaults)
- **DI registration** (strongly-typed client via `IServiceCollection`)

### 3.5 Referencing an Integration from a Project

A project references a resource via `.WithReference()`:

```csharp
var cache = builder.AddRedis("cache");
var api   = builder.AddProject<Projects.Api>("api")
                   .WithReference(cache);   // injects CACHE_URI into api's environment
```

Aspire injects environment variables named after the resource using the convention `{RESOURCENAME}_{PROPERTYNAME}`. For example, a Redis resource named `"cache"` injects `CACHE_URI=redis://...`. Multiple formats are available (URI, JDBC, individual host/port/password) depending on the resource type and consuming language.

---

## 4. The Aspire MCP Server

Aspire ships an **MCP (Model Context Protocol) server** that gives AI coding agents direct runtime access to a running Aspire application. Introduced in 13.0 (dashboard-side), evolved substantially in **13.1**, and restructured in **13.3** (moved from dashboard to AppHost-level access via `aspire agent mcp`).

> **Breaking change in 13.3:** The in-dashboard MCP server endpoint was removed. The correct approach is `aspire agent mcp` (the CLI-hosted MCP server).

### Setup

```bash
aspire agent init
```

This command:
1. Detects your AI development environment (VS Code, Claude Code, Copilot CLI, OpenCode).
2. Creates the appropriate MCP config file:
   - VS Code → `.vscode/mcp.json`
   - Claude Code → `.mcp.json`
   - Copilot CLI → `~/.copilot/mcp-config.json`
   - OpenCode → `opencode.jsonc`
3. Installs **skill files** that teach the agent how to use Aspire CLI commands:
   - `.claude/skills/` (Claude Code)
   - `.agents/skills/` (VS Code / Copilot / OpenCode)
   - `.github/skills/` (GitHub Actions agents)

All configurations launch the MCP server as a **STDIO subprocess**: `aspire agent mcp` — no URL or API key required.

### MCP Tools Exposed (15 tools)

#### Resource Management
| Tool | Description |
|------|-------------|
| `list_resources` | Lists all resources with state, health status, source, endpoints, and commands |
| `execute_resource_command` | Executes start, stop, or restart operations on a resource |

#### Telemetry & Observability
| Tool | Description |
|------|-------------|
| `list_console_logs` | Retrieves stdout/stderr output for a specific resource |
| `list_structured_logs` | Lists structured logs, optionally filtered by resource name |
| `list_traces` | Lists distributed traces, optionally filtered by resource name |
| `list_trace_structured_logs` | Gets structured logs associated with a specific trace |

#### AppHost Management
| Tool | Description |
|------|-------------|
| `list_apphosts` | Lists detected AppHost connections and their scope |
| `select_apphost` | Switches between multiple running AppHosts |

#### Documentation & Integration Discovery
| Tool | Description |
|------|-------------|
| `list_integrations` | Lists available Aspire hosting integration packages |
| `get_integration_docs` | Retrieves documentation for a specific integration package |
| `list_docs` | Lists all available documentation pages from aspire.dev |
| `search_docs` | Keyword-based search across aspire.dev documentation |
| `get_doc` | Retrieves the full content of a documentation page by slug |

#### Maintenance
| Tool | Description |
|------|-------------|
| `doctor` | Diagnoses Aspire environment issues and verifies setup |
| `refresh_tools` | Re-emits the tool list (for MCP clients that cache tool definitions) |

### What an AI Agent Can Do

With the MCP server running, an AI agent can:

- **Query runtime state** — check which services are running, unhealthy, or failed without the developer switching terminals
- **Read logs** — pull console output and structured logs to diagnose startup errors or exceptions
- **Inspect distributed traces** — identify slow or failing service calls across the entire distributed system
- **Control resources** — start, stop, or restart individual services via `execute_resource_command`
- **Discover and add integrations** — use `list_integrations` and `get_integration_docs` to find the right package, then call `aspire add` to install it
- **Search documentation** — resolve API questions and configuration options without leaving the conversation
- **Switch AppHosts** — in monorepos with multiple AppHost projects, select which one to interrogate

The **`aspire start`** + **MCP** combination enables a fully agentic development loop: the agent starts the application in the background, queries its state, reads errors, modifies code, rebuilds, and monitors without any manual terminal interaction.

Sensitive resources can be excluded from MCP exposure by annotating them in the AppHost:

```csharp
builder.AddSqlServer("sql").ExcludeFromMcp();
```

The Aspire agent skill (installed by `aspire agent init`) also teaches the agent to use CLI flags designed for non-interactive execution: `--format Json`, `--non-interactive`, and `aspire wait` for health gating.

---

## 5. Aspire Community Toolkit

The **CommunityToolkit/Aspire** repository ([github.com/CommunityToolkit/Aspire](https://github.com/CommunityToolkit/Aspire)) hosts community-contributed integrations. Packages are published to NuGet under the `CommunityToolkit.Aspire.*` namespace.

### Hosting Integrations

| Package | Integration |
|---------|-------------|
| `CommunityToolkit.Aspire.Hosting.ActiveMQ` | ActiveMQ message broker container |
| `CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder` | Azure Data API Builder |
| `CommunityToolkit.Aspire.Hosting.Azure.Dapr.Redis` | Dapr with Azure Redis backing |
| `CommunityToolkit.Aspire.Hosting.Azure.Extensions` | Azure container extensions |
| `CommunityToolkit.Aspire.Hosting.Bun` | Bun JavaScript runtime |
| `CommunityToolkit.Aspire.Hosting.Dapr` | Dapr distributed application runtime |
| `CommunityToolkit.Aspire.Hosting.Deno` | Deno JavaScript/TypeScript runtime |
| `CommunityToolkit.Aspire.Hosting.Elasticsearch.Extensions` | Elasticsearch extensions |
| `CommunityToolkit.Aspire.Hosting.Flagd` | Flagd feature flag evaluation engine |
| `CommunityToolkit.Aspire.Hosting.GoFeatureFlag` | GoFeatureFlag container |
| `CommunityToolkit.Aspire.Hosting.Golang` | Go application hosting |
| `CommunityToolkit.Aspire.Hosting.Java` | Java apps (local JDK or container) |
| `CommunityToolkit.Aspire.Hosting.JavaScript.Extensions` | Node.js app extensions |
| `CommunityToolkit.Aspire.Hosting.k6` | Grafana k6 load testing |
| `CommunityToolkit.Aspire.Hosting.KurrentDB` | KurrentDB (formerly EventStoreDB) container |
| `CommunityToolkit.Aspire.Hosting.LavinMQ` | LavinMQ message broker |
| `CommunityToolkit.Aspire.Hosting.MailPit` | MailPit SMTP testing container (previously MailDev) |
| `CommunityToolkit.Aspire.Hosting.Meilisearch` | Meilisearch search engine container |
| `CommunityToolkit.Aspire.Hosting.Minio` | MinIO S3-compatible object storage |
| `CommunityToolkit.Aspire.Hosting.MongoDB.Extensions` | MongoDB extensions |
| `CommunityToolkit.Aspire.Hosting.MySql.Extensions` | MySQL extensions |
| `CommunityToolkit.Aspire.Hosting.Ollama` | Ollama LLM container with model download on startup |
| `CommunityToolkit.Aspire.Hosting.Perl` | Perl scripts and APIs |
| `CommunityToolkit.Aspire.Hosting.PostgreSQL.Extensions` | PostgreSQL extensions |
| `CommunityToolkit.Aspire.Hosting.Python.Extensions` | Python app extensions |
| `CommunityToolkit.Aspire.Hosting.RavenDB` | RavenDB document database container |
| `CommunityToolkit.Aspire.Hosting.Redis.Extensions` | Redis extensions |
| `CommunityToolkit.Aspire.Hosting.Rust` | Rust application hosting |
| `CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects` | SQL database projects (DACPAC) |
| `CommunityToolkit.Aspire.Hosting.SqlServer.Extensions` | SQL Server extensions |
| `CommunityToolkit.Aspire.Hosting.Sqlite` | SQLite database with optional SQLite Web UI |
| `CommunityToolkit.Aspire.Hosting.SurrealDb` | SurrealDB multi-model database |
| `CommunityToolkit.Aspire.Hosting.Umami` | Umami analytics platform |

### Client Integrations

| Package | Integration |
|---------|-------------|
| `CommunityToolkit.Aspire.GoFeatureFlag` | GoFeatureFlag client |
| `CommunityToolkit.Aspire.KurrentDB` | KurrentDB client |
| `CommunityToolkit.Aspire.Meilisearch` | Meilisearch client |
| `CommunityToolkit.Aspire.Microsoft.Data.Sqlite` | SQLite `SqliteConnection` |
| `CommunityToolkit.Aspire.Microsoft.EntityFrameworkCore.Sqlite` | EF Core with SQLite |
| `CommunityToolkit.Aspire.Minio.Client` | MinIO `IMinioClient` |
| `CommunityToolkit.Aspire.OllamaSharp` | OllamaSharp `IOllamaApiClient` |
| `CommunityToolkit.Aspire.RavenDB.Client` | RavenDB client |
| `CommunityToolkit.Aspire.SurrealDb` | SurrealDB client |

> Several of these packages (Ollama, Meilisearch, LavinMQ, SQLite, SurrealDB, KurrentDB, RavenDB) have since been promoted to the official Aspire integration catalogue in 13.x releases.

---

## 6. The Aspire Test Framework

### Package

Add `Aspire.Hosting.Testing` to your test project (xUnit, NUnit, or MSTest).

### Core Pattern

```csharp
using Aspire.Hosting.Testing;

[Fact]
public async Task WebFrontend_ReturnsOk()
{
    // 1. Create the testing builder — mirrors DistributedApplication.CreateBuilder
    var appHost = await DistributedApplicationTestingBuilder
        .CreateAsync<Projects.AspireApp_AppHost>(cancellationToken);

    // 2. Optionally configure services for tests
    appHost.Services.AddLogging(logging =>
        logging.SetMinimumLevel(LogLevel.Debug));
    appHost.Services.ConfigureHttpClientDefaults(b =>
        b.AddStandardResilienceHandler());

    // 3. Build and start the full application stack
    await using var app = await appHost.BuildAsync(cancellationToken)
        .WaitAsync(DefaultTimeout, cancellationToken);
    await app.StartAsync(cancellationToken)
        .WaitAsync(DefaultTimeout, cancellationToken);

    // 4. Wait for specific resources to be healthy
    await app.ResourceNotifications
        .WaitForResourceHealthyAsync("webfrontend", cancellationToken)
        .WaitAsync(DefaultTimeout, cancellationToken);

    // 5. Create an HTTP client scoped to the named resource
    using var httpClient = app.CreateHttpClient("webfrontend");
    var response = await httpClient.GetAsync("/");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}
```

### Key Characteristics

- **Closed-box integration testing** — the entire AppHost and all resources launch as real processes, closely simulating production.
- **No Docker/TestContainers wiring** — Aspire handles container orchestration automatically.
- **Port randomisation** — by default, ports are randomised so multiple test instances can run concurrently.
- **Dashboard disabled** — by default in test mode (can be re-enabled).
- **Resource notifications** — `app.ResourceNotifications.WaitForResourceHealthyAsync(name)` blocks until a named resource is healthy.
- **`CreateHttpClient(name)`** — creates an `HttpClient` pre-configured with the base address of the named resource, including service-discovered endpoints.

### Configuration Overrides

```csharp
// Disable port randomisation
appHost.Configuration["DcpPublisher:RandomizePorts"] = "false";

// Re-enable the dashboard during tests
appHost.Configuration["Aspire:Dashboard:Enabled"] = "true";
```

### Relationship to `WebApplicationFactory<T>`

`DistributedApplicationTestingBuilder` is the multi-project equivalent of `WebApplicationFactory<T>`. For single-project unit/integration tests where you want to mock dependencies, `WebApplicationFactory<T>` is still recommended. For full-stack integration tests across all services, use `DistributedApplicationTestingBuilder`.

---

## 7. Publishing and Deployment

### Philosophy: "Your local AppHost model is your deployment model"

The same resource graph declared in `Program.cs` is used both to orchestrate local development and to generate deployment artifacts. There is no separate deployment configuration file.

### The Publish / Deploy Split

| Command | What it does |
|---------|-------------|
| `aspire publish` | Generates intermediate, parameterised deployment artifacts. Secrets remain as placeholder tokens. Produces Docker Compose files, Kubernetes Helm charts, or Bicep templates depending on the configured publisher. |
| `aspire deploy` | Resolves parameters, injects secrets, and applies the artifacts to the target environment. Provisions cloud infrastructure, builds and pushes container images, and applies manifests. |
| `aspire destroy` | Tears down a previously deployed environment (Azure, Kubernetes, Docker Compose). Added in 13.3. |

### Supported Deployment Targets (as of 13.3)

| Target | Status | Notes |
|--------|--------|-------|
| Azure Container Apps | GA | Core production target; azd-compatible |
| Azure App Service | GA | Dashboard + Application Insights built-in |
| Azure Functions on ACA | GA (since 13.1) | KEDA auto-scaling |
| Docker Compose | GA (since 13.2) | Generates `docker-compose.yaml` with parameterised variables |
| Kubernetes (self-managed) | Preview (13.3) | Helm-based engine; generates complete charts |
| Azure Kubernetes Service (AKS) | Preview (13.3) | `AddAzureKubernetesEnvironment()` |

### Deployment State Management

Aspire remembers Azure subscription, resource group, location, and parameter values locally between deployments, avoiding re-prompting on incremental pushes.

### The `aspire do` Pipeline System

Introduced in 13.0, `aspire do` is a pipeline system that replaced the old `PublishingContext` / `WithPublishingCallback` infrastructure. Each deployment target exposes **pipeline steps** with explicit dependencies and parallel execution. `aspire do <step>` executes a specific step and all its prerequisites.

### Legacy Manifest Format (Deprecated)

The JSON `aspire-manifest.json` format (used by the Azure Developer CLI `azd` directly) is **deprecated** as of 13.x and is no longer being evolved. It can still be generated for backward-compatibility troubleshooting:

```bash
aspire do publish-manifest --output-path ./aspire-manifest.json
```

For new workflows, use `aspire deploy` (ACA, AKS) or `aspire publish` (Docker Compose, Kubernetes) directly. The `azd` tool continues to work via an adapter that invokes the Aspire pipeline internally.

### JavaScript Publishing (Preview, 13.3)

First-class static/server JS publishing:

```csharp
builder.AddJavaScriptApp("web", "../web")
       .PublishAsStaticWebsite()   // SPA with optional API proxy
       // or:
       .PublishAsNodeServer()      // Pre-bundled Node server
       // or:
       .PublishAsNpmScript("start"); // npm script entry point

builder.AddNextJsApp("nextapp", "../nextapp"); // First-class Next.js
```

---

## 8. Single-File AppHost and AppHost Project Structure

### Three AppHost Formats

#### 1. File-Based C# AppHost (Single File)

Introduced in 13.0, enabled via:

```bash
aspire config set features.singlefileAppHostEnabled true
```

Requires .NET SDK 10.0.100 or later. A single `apphost.cs` file with package directives replaces the entire project:

```csharp
#!/usr/bin/dotnet-script
#:sdk Aspire.AppHost.Sdk/13.3.0
#:package Aspire.Hosting.Redis/13.3.0
#:package Aspire.Hosting.PostgreSQL/13.3.0

var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");
var db    = builder.AddPostgres("pg").AddDatabase("appdb");

builder.AddProject<Projects.Api>("api")
       .WithReference(cache)
       .WithReference(db);

builder.Build().Run();
```

An accompanying `apphost.run.json` (or the unified `aspire.config.json`) holds run/launch settings.

**Best for:** quick prototypes, demos, learning, small projects.

#### 2. Project-Based C# AppHost (Standard)

The traditional multi-file format with a `.csproj`:

```
MyApp.AppHost/
├── Program.cs            ← orchestration code
├── MyApp.AppHost.csproj  ← references Aspire.AppHost.Sdk
├── appsettings.json
├── appsettings.Development.json
└── Properties/
    └── launchSettings.json
```

The `.csproj` references the `Aspire.AppHost.Sdk`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Sdk Name="Aspire.AppHost.Sdk" Version="13.3.0" />
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
```

**Best for:** production solutions, teams, large solutions with many services.

#### 3. TypeScript AppHost (Preview, since 13.2)

Write the orchestrator in TypeScript using the Aspire TypeScript SDK:

```
my-apphost/
├── apphost.ts           ← orchestration code (TypeScript)
├── package.json
├── tsconfig.json
└── aspire-sdk/          ← generated SDK modules
```

```typescript
import { createBuilder } from '@aspire/hosting';

const builder = createBuilder(process.argv.slice(2));

const cache = builder.addRedis('cache');
const api   = builder.addJavaScriptApp('api', { projectDirectory: '../api' })
                     .withReference(cache);

await builder.build().run();
```

The TypeScript AppHost runs as a guest process communicating with Aspire's .NET orchestration host via JSON-RPC. Full feature parity with the C# experience (dashboard, CLI, service discovery).

### Aspire SDK Packages

| Package | Role |
|---------|------|
| `Aspire.AppHost.Sdk` | MSBuild SDK for AppHost projects; required in `.csproj` |
| `Aspire.Hosting.AppHost` | Core hosting APIs (`DistributedApplication`, builder, resource types) |
| `Aspire.Hosting` | Base hosting primitives |
| `Aspire.ServiceDefaults` | Shared service defaults (OpenTelemetry, resiliency, health checks) — added to each service project |
| `Aspire.Cli` | The standalone CLI (NuGet global tool) |

### The Service Defaults Project

Every Aspire solution conventionally includes a **`MyApp.ServiceDefaults`** project (generated by templates) that is referenced by every service project:

```csharp
// In each service's Program.cs
builder.AddServiceDefaults();
```

This single call wires up:
- OpenTelemetry (traces, metrics, logs) configured to export to the Aspire dashboard OTLP endpoint
- ASP.NET Core health check endpoints (`/health`, `/alive`)
- `HttpClient` resiliency with standard resilience handler
- Service discovery via `AddServiceDiscovery()`

---

## Summary of Key Facts

| Topic | Detail |
|-------|--------|
| Current version | 13.3.4 (May 19, 2026) |
| Previous version | 9.5 (last .NET-aligned release) |
| Version jump | 9.5 → 13.0 (skipped 10/11/12; intentional decoupling from .NET versioning) |
| 13.0 released | November 11, 2025 |
| 13.3 released | May 7, 2026 |
| Requires | .NET 10 SDK+ |
| Support policy | Only latest release supported (Modern Lifecycle) |
| Home | [aspire.dev](https://aspire.dev) |
| Source | [github.com/microsoft/aspire](https://github.com/microsoft/aspire) |

---

## Sources

- [aspire.dev — What's new in Aspire 13](https://aspire.dev/whats-new/aspire-13/)
- [aspire.dev — What's new in Aspire 13.1](https://aspire.dev/whats-new/aspire-13-1/)
- [aspire.dev — What's new in Aspire 13.2](https://aspire.dev/whats-new/aspire-13-2/)
- [aspire.dev — What's new in Aspire 13.3](https://aspire.dev/whats-new/aspire-13-3/)
- [aspire.dev — Aspire MCP Server](https://aspire.dev/get-started/aspire-mcp-server/)
- [aspire.dev — AI Coding Agents](https://aspire.dev/get-started/ai-coding-agents/)
- [aspire.dev — CLI Overview](https://aspire.dev/reference/cli/overview/)
- [aspire.dev — aspire add command](https://aspire.dev/reference/cli/commands/aspire-add/)
- [aspire.dev — Install CLI](https://aspire.dev/get-started/install-cli/)
- [aspire.dev — App Host](https://aspire.dev/get-started/app-host/)
- [aspire.dev — Integrations](https://aspire.dev/integrations/)
- [aspire.dev — Integrations Overview](https://aspire.dev/integrations/overview/)
- [aspire.dev — Keycloak Integration](https://aspire.dev/integrations/security/keycloak/)
- [aspire.dev — SQL Server Hosting](https://aspire.dev/integrations/databases/sql-server/sql-server-host/)
- [aspire.dev — Testing Overview](https://aspire.dev/testing/overview/)
- [aspire.dev — Write Your First Test](https://aspire.dev/testing/write-your-first-test/)
- [aspire.dev — Deployment](https://aspire.dev/deployment/)
- [aspire.dev — Support Policy](https://aspire.dev/support/)
- [devblogs.microsoft.com — Aspire 13.2 Announcement](https://devblogs.microsoft.com/aspire/aspire-13-2-announcement/)
- [devblogs.microsoft.com — Aspire 13.3 What's New](https://devblogs.microsoft.com/aspire/whats-new-aspire-13-3/)
- [infoq.com — Aspire 13.2 Release](https://www.infoq.com/news/2026/04/aspire-13-2-release/)
- [infoq.com — Aspire 13.3 Release](https://www.infoq.com/news/2026/05/aspire-13-3-release/)
- [github.com/CommunityToolkit/Aspire](https://github.com/CommunityToolkit/Aspire)
- [Microsoft Learn — Aspire Hosting.Testing DistributedApplicationTestingBuilder](https://learn.microsoft.com/en-us/dotnet/api/aspire.hosting.testing.distributedapplicationtestingbuilder.createasync?view=dotnet-aspire-13.0)
- [NuGet — Aspire.Cli 13.3.3](https://www.nuget.org/packages/Aspire.CLI)
