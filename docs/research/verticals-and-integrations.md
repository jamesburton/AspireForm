# Vertical Slice Modules — Technology Research

Research date: 2026-05-22. All package versions are confirmed current as of this date.

---

## 1. Microsoft Data API Builder (DAB)

### What it is

Data API Builder (DAB) is an open-source tool from Microsoft that generates REST and GraphQL endpoints directly from database objects (tables, views, stored procedures) via a JSON configuration file. No application code is required — you define entities and permissions in `dab-config.json` and run the DAB container. It supports SQL Server, Azure SQL, PostgreSQL, MySQL, and Azure Cosmos DB.

GitHub: <https://github.com/Azure/data-api-builder>

### Current version / status

- **DAB CLI / NuGet tool:** `Microsoft.DataApiBuilder` v1.7.93 (released 2026-04-14, stable)
- v2.0 preview in progress (March 2026 preview docs available), adding an MCP server mode
- Hosted as a Docker container; the official Docker image is `mcr.microsoft.com/azure-databases/data-api-builder`

### Aspire integration

**Package:** `CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder`
**Latest stable:** 13.1.0 (versioned to match Aspire releases)

```csharp
// AppHost Program.cs
var sqldb = builder.AddSqlServer("sql")
                   .AddDatabase("mydb");

var dab = builder.AddDataAPIBuilder("dab")
                 .WithReference(sqldb);
```

Aspire automatically injects the connection string into the DAB container. Multiple config files can be passed as an array for multi-database scenarios:

```csharp
builder.AddDataAPIBuilder("dab",
    ["dab-config.SqlServer.json", "dab-config.PostgreSQL.json"])
```

Documentation: <https://aspire.dev/integrations/devtools/dab/>

### dab-config.json

The config file has three top-level sections:

| Section | Purpose |
|---------|---------|
| `$schema` | Points to the DAB JSON schema for validation |
| `data-source` | Database type (`mssql`, `postgresql`, `mysql`, `cosmosdb_nosql`) and connection string name |
| `runtime` | REST (`/api`) and GraphQL (`/graphql`) path and enabled flags |
| `entities` | Mapping of entity name → database object, REST/GraphQL configuration, permissions per role |

Example auth and entity snippet:

```json
{
  "data-source": { "database-type": "mssql", "connection-string": "@env('DATABASE_CONNECTION_STRING')" },
  "runtime": {
    "rest": { "enabled": true, "path": "/api" },
    "graphql": { "enabled": true, "path": "/graphql" },
    "authentication": { "provider": "StaticWebApps" }
  },
  "entities": {
    "Product": {
      "source": "dbo.Products",
      "permissions": [{ "role": "anonymous", "actions": ["read"] }]
    }
  }
}
```

### Authentication

DAB supports the following `runtime.authentication.provider` values:

| Provider | Notes |
|----------|-------|
| `Unauthenticated` | No auth; all requests treated as anonymous |
| `StaticWebApps` | Azure Static Web Apps EasyAuth; reads `X-MS-CLIENT-PRINCIPAL` header |
| `AppService` | Azure App Service EasyAuth |
| `EntraID` / `AzureAD` | JWT validation against Entra ID; requires `audience` and `issuer` |
| `Simulator` | Dev-only; simulates roles via `X-MS-API-ROLE` header |
| `Custom` | Any JWT-issuing provider; requires `audience` and `issuer` |

Claims from validated JWTs can be passed into SQL session context for row-level security.

---

## 2. MailDev / Mailpit — Local SMTP Test Servers

### What they are

Both MailDev and Mailpit are local SMTP traps for development: they accept outbound email from an application and present it in a web UI, without actually delivering it. Mailpit is the more actively maintained successor to MailDev.

- **Mailpit**: <https://mailpit.axllent.org/> — SMTP on port 1025, web UI on port 8025, includes search, spam analysis, HTML/text preview.
- **MailDev**: <https://maildev.github.io/maildev/> — older, SMTP on port 1025, web UI on port 1080.

### Aspire integration

**Mailpit — official Community Toolkit package:**

| | |
|---|---|
| Package | `CommunityToolkit.Aspire.Hosting.MailPit` |
| Latest stable | 13.3.0 |

```csharp
// AppHost Program.cs
var mailpit = builder.AddMailPit("mailpit")
                     .WithDataVolume();   // optional: persist emails across restarts

// In a service that sends email:
builder.AddProject<Projects.MyApi>("api")
       .WithReference(mailpit);
```

Connection string format injected into consuming services:
`endpoint=smtp://<host>:<port>`

The `SmtpHost` and `SmtpPort` properties are resolved automatically. The web UI endpoint is available through `HttpEndpoint`.

**MailDev — community (not in official toolkit):**

| | |
|---|---|
| Package | `BCat.Aspire.MailDev` v9.0.0 |
| Source | <https://www.nuget.org/packages/BCat.Aspire.MailDev> |

MailDev is not in the CommunityToolkit/Aspire repository. The `BCat.Aspire.MailDev` package provides an equivalent `AddMailDev()` extension but is a third-party community package with minimal maintenance activity.

**Recommendation:** Prefer **Mailpit** (`CommunityToolkit.Aspire.Hosting.MailPit`) for all new work; it has official toolkit support and is actively maintained.

### Key configuration

- Configure `SmtpClient` (or `MailKit`) in application services using the injected SMTP host/port.
- No authentication is required by default (dev-only tool).
- The Aspire dashboard shows the Mailpit web UI URL as a resource endpoint link.

---

## 3. Hangfire — Background Job Processing

### What it is

Hangfire is a .NET library for reliable background job execution. It supports fire-and-forget jobs, delayed jobs, recurring jobs (cron), continuations, and batches. It persists job state to a backing store (SQL Server or Redis), providing durability across application restarts. The built-in dashboard is a web UI for monitoring and managing jobs.

Homepage: <https://www.hangfire.io/>

### Current version / status

| Package | Version | Notes |
|---------|---------|-------|
| `Hangfire` (meta) | 1.8.23 (2026-02-05) | Pulls in Core + SqlServer + AspNetCore |
| `Hangfire.Core` | 1.8.23 | Core abstractions |
| `Hangfire.AspNetCore` | 1.8.23 | ASP.NET Core DI/middleware integration |
| `Hangfire.SqlServer` | 1.8.23 | SQL Server storage |
| `Hangfire.Redis.StackExchange` | 1.12.0 (2025-03-28) | Redis storage via StackExchange.Redis |

### Aspire integration

There is **no dedicated Aspire hosting package** for Hangfire (GitHub issue [#2408](https://github.com/HangfireIO/Hangfire/issues/2408) tracks this). Integration with Aspire is done via generic Aspire primitives:

1. Add SQL Server or Redis to the AppHost:

```csharp
// AppHost Program.cs
var sql = builder.AddSqlServer("sql").AddDatabase("hangfire-db");
// or
var redis = builder.AddRedis("hangfire-redis");

builder.AddProject<Projects.WorkerService>("worker")
       .WithReference(sql);
```

2. Configure Hangfire in the consuming project:

```csharp
// Worker Program.cs — SQL Server storage
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("hangfire-db")));

builder.Services.AddHangfireServer();

// Dashboard (add to web API project, not worker):
app.UseHangfireDashboard("/hangfire");
```

```csharp
// Redis storage alternative
builder.Services.AddHangfire(config => config
    .UseRedisStorage(builder.Configuration.GetConnectionString("hangfire-redis")));
```

### Key configuration considerations

- **Storage choice:** SQL Server is zero-additional-dependency; Redis processes jobs significantly faster and suits high-throughput scenarios.
- **Dashboard security:** `UseHangfireDashboard` should be protected with `DashboardOptions.Authorization` in production.
- **Multiple servers:** Each `AddHangfireServer()` call registers a worker. Queues can be partitioned with `BackgroundJobServerOptions.Queues`.
- **Recurring jobs:** Registered with `RecurringJob.AddOrUpdate(...)` using CRON expressions.
- **Aspire dashboard:** The Hangfire dashboard runs as a middleware endpoint on the web project, not as a separate Aspire resource; it appears as a sub-URL, not a top-level resource endpoint.

---

## 4. Blazing Story — Storybook for Blazor

### What it is

BlazingStory is an open-source Storybook clone for Blazor — a UI workshop that displays Blazor components in isolation with configurable props (args), multiple variants (stories), docs pages, and interactive controls. It requires no npm, no webpack, and no JavaScript toolchain. It runs as a standalone Blazor application that references your component library.

GitHub: <https://github.com/jsakamoto/BlazingStory>
License: Mozilla Public License 2.0

### Current version / status

- **NuGet package:** `BlazingStory` v1.0.0-preview.80 (released 2026-05-21) — still in preview, but actively maintained
- **Templates package:** `BlazingStory.ProjectTemplates`
- Targets .NET 8, 9, and 10
- Recent additions: accessibility panel powered by axe-core; MCP server for AI agent integration

### Setup

```bash
# Install project templates (once per machine)
dotnet new install BlazingStory.ProjectTemplates

# Create a BlazingStory app alongside your component library
dotnet new blazingstorywasm -n MyApp.Stories
dotnet sln add ./MyApp.Stories

# Reference the component library from the Stories project
dotnet add ./MyApp.Stories reference ./MyApp.Components
```

For Blazor Server mode, use `blazingstoryserver` instead of `blazingstorywasm`.

### Defining stories

Story files follow the naming convention `*.stories.razor`:

```razor
@using MyApp.Components

[Stories("Components/Buttons")]
<Stories TComponent="MyButton">
    <Story Name="Primary">
        <Template>
            <MyButton Variant="primary">Click me</MyButton>
        </Template>
    </Story>
    <Story Name="Disabled">
        <Template>
            <MyButton Disabled="true">Disabled</MyButton>
        </Template>
    </Story>
</Stories>
```

### Aspire integration

There is **no Aspire hosting package** for BlazingStory. It is a standalone Blazor app referenced in the solution. In an Aspire context it would be added as a normal project resource:

```csharp
builder.AddProject<Projects.MyApp_Stories>("stories");
```

### Key configuration considerations

- The Stories project is a development-only project (do not deploy to production).
- The built-in MCP server allows AI agents to query component metadata and generate code using your actual component signatures.
- Interactive controls (knobs) are defined via `[Parameter]`-decorated args classes on each story.

---

## 5. Figma API and Figma MCP Server

### 5a. Figma REST API

The Figma REST API provides programmatic read (and limited write) access to Figma design files.

Base URL: `https://api.figma.com/v1/`
Authentication: Personal Access Token (`X-Figma-Token` header) or OAuth 2.0 bearer token.

**Key endpoints:**

| Endpoint | What it returns |
|----------|----------------|
| `GET /files/:key` | Full document tree as JSON — all frames, components, text, styles |
| `GET /files/:key/nodes?ids=...` | Subtree for specific node IDs |
| `GET /files/:key/images` | Exported image renderings of nodes |
| `GET /files/:key/comments` | Comments on the file |
| `GET /files/:key/components` | Published components in the file |
| `GET /files/:key/styles` | Published styles (colours, typography, effects) |
| `GET /teams/:id/components` | All published components across a team library |
| `GET /teams/:id/styles` | All published styles across a team library |

As of November 2025, Figma introduced stricter rate limits (requests/minute per token). A v2 API is in development with an OpenAPI spec: <https://github.com/figma/rest-api-spec>.

There is no official .NET SDK. Community wrappers exist but the REST API is straightforward to call with `HttpClient` + `System.Text.Json`.

### 5b. Figma MCP Server (Dev Mode MCP)

Figma launched a first-party MCP server in 2024/2025 that brings design context directly into AI coding agents (Claude Code, GitHub Copilot, Cursor, Windsurf, etc.).

Documentation: <https://developers.figma.com/docs/figma-mcp-server/>

**What it exposes to agents:**

| Capability | Description |
|-----------|-------------|
| Design context extraction | Provides variables, component definitions, layout constraints, and style references from selected frames |
| Code generation | Converts selected Figma frames to code respecting design system tokens |
| Canvas writing | Agents can create and modify frames, components, variables, and auto-layout directly in Figma (write-back) |
| Code Connect | Links generated code back to actual codebase components to prevent drift |
| Make file resources | Retrieves code resources from Figma Make prototypes |

**Transport:** Remote MCP server (no local Figma desktop app required) — recommended for broadest feature set. Also available as a local MCP server via the Figma desktop app.

**Pricing (as of 2026-05-22):** Canvas writing (write-to-canvas) is free during beta; it will become a usage-based paid feature post-beta. Read-only design context tools are available on all paid Figma plans.

**Access:** Only MCP clients listed in the Figma MCP Catalog can connect. Developers can request new client integration via a waitlist.

**Aspire context:** No Aspire hosting integration exists or is needed — the Figma MCP server is a cloud service consumed by agents, not a local container.

---

## 6. Authentication Approaches for .NET Web Apps

### 6a. API Keys

**What it is:** A shared secret transmitted in an HTTP header (conventionally `X-API-Key`), query parameter, or authorization header. Suitable for machine-to-machine (M2M) or trusted-client scenarios.

**Libraries:**

| Package | Version | Notes |
|---------|---------|-------|
| `AspNetCore.Authentication.ApiKey` | 9.0.0 | Lightweight, Microsoft-style `AuthenticationHandler`; supports header, query, route, and auth-header placement; AOT-compatible |
| `AspNetCore.SecurityKey` | — | Alternative; includes OpenAPI/Swagger integration |

**Pattern (AspNetCore.Authentication.ApiKey):**

```csharp
builder.Services
    .AddAuthentication(ApiKeyDefaults.AuthenticationScheme)
    .AddApiKeyInHeader<ApiKeyProvider>("ApiKey", options =>
    {
        options.Realm = "My API";
        options.KeyName = "X-API-Key";
    });
```

Implement `IApiKeyProvider` to look up keys from a database or configuration. The handler integrates with standard `[Authorize]` attributes and policies.

**Aspire configuration:** No special Aspire integration needed. Connection strings or secrets for key storage (e.g. SQL Server, Redis) are wired in via standard `WithReference()`.

### 6b. Magic Link / Passwordless Email

**What it is:** Authentication by sending a time-limited, single-use token URL to the user's email. No password required.

**Approach in ASP.NET Core:**

There is no first-party ASP.NET Core package. The canonical implementation uses ASP.NET Core Identity's custom `IUserTokenProvider<TUser>`:

1. Generate a time-limited token via `UserManager.GenerateUserTokenAsync()`.
2. Email the link containing the token.
3. On click, validate via `UserManager.VerifyUserTokenAsync()` and sign in.

**Community libraries:**

| Package | Notes |
|---------|-------|
| `Authentication.MagicLink` (GitHub: aiandcodeconsultants) | ASP.NET Core Identity add-on; pluggable email provider |
| DIY + ASP.NET Core Identity | Recommended for control; Andrew Lock's blog post is the definitive guide |

**Key considerations:**
- Token expiry should be short (15–60 minutes).
- Tokens must be single-use (mark as consumed on validation).
- Requires a working email delivery mechanism (integrate with Mailpit in dev, SendGrid/SMTP in production).
- Rate-limit the send endpoint to prevent abuse.

**Aspire configuration:** No Aspire-side hosting package. Use `builder.AddMailPit("mailpit")` in AppHost for dev-time email capture.

### 6c. Microsoft Entra External ID (formerly Azure AD B2C)

**What it is:** Microsoft's CIAM (Customer Identity and Access Management) platform for consumer-facing apps. Azure AD B2C was retired for new customers from **May 1, 2025**; Entra External ID is the replacement. Supports social logins (Google, Facebook), email OTP, SSPR, and custom branding.

**Library:**

| Package | Version | Notes |
|---------|---------|-------|
| `Microsoft.Identity.Web` | 4.9.0 | Wraps MSAL.NET with ASP.NET Core middleware; supports Entra ID (workforce) and External ID (CIAM) |
| `Microsoft.Identity.Web.UI` | 4.9.0 | Razor Pages UI for sign-in/sign-out |
| `Microsoft.Identity.Web.MicrosoftGraph` | 4.9.0 | Optional: call Microsoft Graph on behalf of users |

**Configuration:**

```csharp
// Program.cs
builder.Services.AddMicrosoftIdentityWebAppAuthentication(
    builder.Configuration, "AzureAdB2C");
```

```json
// appsettings.json
{
  "AzureAdB2C": {
    "Instance": "https://<tenant>.ciamlogin.com/",
    "TenantId": "<tenant-id>",
    "ClientId": "<client-id>",
    "ClientSecret": "<client-secret>",
    "CallbackPath": "/signin-oidc",
    "SignUpSignInPolicyId": "B2C_1_susi"
  }
}
```

For Aspire, the client secret is wired via `builder.Configuration` backed by user secrets or a secrets manager (no dedicated Aspire resource required). The Aspire AppHost does not orchestrate Entra External ID — it is a cloud service.

**Blazor-specific:** Use `AddMicrosoftIdentityWebAppAuthentication` in Blazor Web App (server-side); for WASM, use MSAL.js or the preview `Microsoft.Identity.Client.Extensions.Msal` Web-compatible variant.

---

## 7. ETL / Data Import in .NET

### Libraries

#### CSV Reading: CsvHelper

| | |
|---|---|
| Package | `CsvHelper` |
| Version | 33.1.0 (2025-06-02) |
| License | MS-PL / Apache-2.0 (dual, free commercial use) |

The standard .NET CSV library. Supports class mapping, custom converters, culture settings, and streaming large files.

```csharp
using var reader = new StreamReader("data.csv");
using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
var records = csv.GetRecords<Product>().ToList();
```

#### Excel Reading: ExcelDataReader

| | |
|---|---|
| Package | `ExcelDataReader` + `ExcelDataReader.DataSet` |
| Version | 3.8.0 (2025-09-21) |
| License | MIT |

Lightweight, fast reader for `.xls` and `.xlsx` formats using the `IDataReader` pattern. Good for large files — streams rows without loading the whole workbook into memory.

#### Excel Reading/Writing: ClosedXML

| | |
|---|---|
| Package | `ClosedXML` |
| Version | 0.105.0 (2025-05-14) |
| License | MIT |

Friendly API over OpenXML SDK for reading and writing `.xlsx` files. Suitable when you need to produce formatted output (formulas, styles, charts). Free for commercial use.

#### Excel Writing (commercial): EPPlus

| | |
|---|---|
| Package | `EPPlus` |
| Version | 7.x |
| License | Polyform Non-Commercial for non-commercial use; commercial license required for production |

Feature-rich Excel library. **Licensing gotcha:** v5+ requires a paid commercial license for production use. Prefer ClosedXML for free projects.

#### Bulk SQL Insert: SqlBulkCopy

Built into `Microsoft.Data.SqlClient`. The idiomatic pattern for loading large volumes of rows into SQL Server — significantly faster than individual `INSERT` statements.

```csharp
using var bulkCopy = new SqlBulkCopy(connectionString)
{
    DestinationTableName = "dbo.Products",
    BatchSize = 1000
};
bulkCopy.ColumnMappings.Add("Name", "ProductName");
await bulkCopy.WriteToServerAsync(dataTable);
```

#### Full ETL Framework: Cinchoo ETL

| | |
|---|---|
| Package | `ChoETL` / `ChoETL.NETStandard` |
| Notes | Supports CSV, JSON, XML, Parquet, fixed-width; declarative mapping |

For complex multi-source import pipelines requiring transformation, Cinchoo ETL provides a pipeline abstraction. For simpler scenarios (CSV → SQL or Excel → SQL), CsvHelper or ExcelDataReader + `SqlBulkCopy` is sufficient and preferred.

### Aspire configuration

No dedicated Aspire hosting packages for ETL libraries. Typical pattern: the import service is a `worker` or `console` project in the Aspire AppHost, with `WithReference(sql)` injecting the SQL Server connection string.

---

## 8. EF Core Scaffolding

### 8a. Database-First: Reverse Engineering

The `dotnet-ef` global tool scaffolds a `DbContext` and entity classes from an existing database.

**Prerequisites:**

```bash
dotnet tool install --global dotnet-ef
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Microsoft.EntityFrameworkCore.SqlServer   # or Npgsql.EntityFrameworkCore.PostgreSQL
```

**Command:**

```bash
dotnet ef dbcontext scaffold \
  "Server=.;Database=MyDb;Trusted_Connection=True;" \
  Microsoft.EntityFrameworkCore.SqlServer \
  --output-dir Models \
  --context-dir Data \
  --context MyDbContext \
  --context-namespace MyApp.Data \
  --namespace MyApp.Models \
  --no-onconfiguring \
  --data-annotations \
  --table Products --table Orders \
  --force
```

**Key options:**

| Option | Purpose |
|--------|---------|
| `--output-dir` / `-o` | Directory for entity classes |
| `--context-dir` | Separate directory for DbContext |
| `--context` / `-c` | DbContext class name |
| `--namespace` | Namespace for entity classes |
| `--context-namespace` | Namespace for DbContext |
| `--no-onconfiguring` | Suppress `OnConfiguring` (connection string in config, not in class) |
| `--data-annotations` / `-d` | Use data annotations instead of Fluent API |
| `--table` / `-t` | Scaffold specific tables only (repeatable) |
| `--schema` / `-s` | Scaffold specific schemas |
| `--force` / `-f` | Overwrite existing files |

**EF Core 9 note:** Calling `Migrate()` when there are unmodelled schema changes now throws an exception rather than silently continuing.

### 8b. Code-First: Migrations

Migrations are the inverse: the C# model is the source of truth; EF Core generates SQL to keep the database in sync.

**Workflow:**

```bash
# 1. Create a migration after model changes
dotnet ef migrations add AddProductCategoryRelationship

# 2. Preview the generated SQL
dotnet ef migrations script

# 3. Apply to database
dotnet ef database update

# 4. Roll back to a specific migration
dotnet ef database update PreviousMigrationName

# 5. Remove the last unapplied migration
dotnet ef migrations remove
```

**Aspire integration pattern:** For development, apply migrations on startup via `context.Database.MigrateAsync()` (guarded behind an environment check). Aspire's `WaitFor()` pattern ensures the database resource is healthy before the API project applies migrations:

```csharp
// AppHost
var sql = builder.AddSqlServer("sql").AddDatabase("mydb");
var api = builder.AddProject<Projects.MyApi>("api")
                 .WithReference(sql)
                 .WaitFor(sql);
```

```csharp
// API Program.cs (development only)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();
    await db.Database.MigrateAsync();
}
```

For production, generate a SQL migration bundle (`dotnet ef migrations bundle`) and run it as a deployment step, separate from application startup.

**Migration bundles (EF Core 8+):**

```bash
dotnet ef migrations bundle --output migrate.exe --self-contained
```

Produces a self-contained executable that applies all pending migrations — suitable for CI/CD pipelines without requiring the EF CLI tool installed on the target machine.
