# Docker Compose as Design Inspiration for AspireForm

> **Purpose**: Capture Docker Compose patterns — specifically those not well-covered by Terraform — as design input for AspireForm's declarative .NET Aspire scaffolding/configuration tool.

---

## 1. The Compose File Model

A `docker-compose.yml` file is a single YAML document describing an entire application graph.
The five top-level keys each represent a first-class resource type:

| Key        | Represents                                                                                     |
|------------|-----------------------------------------------------------------------------------------------|
| `services` | Containers to run — the core unit of Compose. Each service maps to one container definition.  |
| `volumes`  | Named persistent volumes. Services declare which volumes they mount; Compose creates them.     |
| `networks` | Named virtual networks. Services are placed on one or more networks.                           |
| `configs`  | Immutable config blobs (files) injected into containers at runtime (Swarm extended this).      |
| `secrets`  | Sensitive blobs with restricted access semantics. Like `configs` but with tighter permissions. |

### Design intent

The file is **desired-state**, not imperative. You describe *what* you want, not *how* to achieve it.
Resources that are not referenced by any service are created but inert.
Cross-cutting concerns (networking, storage, config) are lifted to their own namespace rather than being properties of a single service.

### Relevance to Terraform gap

Terraform has resource blocks for infrastructure, but it is not opinionated about *application-level* resource graphs (services depending on databases, etc.). Compose's named cross-references — a service naming a volume or network by a key defined in the same file — are a higher-level, application-scoped model.

---

## 2. Layered / Override Files

### `docker-compose.override.yml`

Docker Compose automatically loads `docker-compose.override.yml` alongside `docker-compose.yml` when both are present in the working directory. The override file is merged into the base, not concatenated — it produces a single resolved document.

### Multiple `-f` files

```
docker compose -f docker-compose.yml -f docker-compose.prod.yml up
```

Files are merged left-to-right: each successive file is applied as a patch over the accumulated result.

### Merge / override rules

The merge rules are asymmetric by YAML type:

- **Mappings** (key→value objects): deep-merged. A key in the later file overrides the same key in the earlier file, but other keys in the earlier file are preserved. Example: adding one `environment` variable in the override does not wipe the base environment.
- **Sequences** (lists): *replaced wholesale* in most cases. A `command` list in the override replaces the base `command` entirely. Notable exceptions: `ports`, `expose`, `external_links`, `dns`, `dns_search`, `tmpfs` are concatenated rather than replaced.
- **Scalar values**: later file wins outright.

This asymmetry is intentional: it gives predictable "add an env var" semantics for mappings, while letting an override completely change a command or entrypoint without having to know the base value.

### `extends`

A service can inherit from another service definition — even from a different file:

```yaml
services:
  web:
    extends:
      file: common.yml
      service: base-web
    ports:
      - "8080:80"
```

`extends` does a single level of composition. It does not recursively chain. It is a *definition-time* tool, not a runtime merging mechanism.

### Design intent

Override files externalise the delta between environments (dev vs staging vs prod) into separate, diffable files rather than encoding environment logic inside a single monolithic config. The base file is the canonical application description; override files express *what changes* for a context.

---

## 3. Profiles

### Syntax

A service can declare one or more profiles:

```yaml
services:
  db:
    image: postgres
    profiles: [db]

  debug-tools:
    image: some-debug-image
    profiles: [debug]

  api:
    image: my-api
    # no profiles — always active
```

Services with no `profiles` key are always started. Services with `profiles` are only started when one of their declared profiles is active.

### Activation

```
docker compose --profile db up          # starts api + db
docker compose --profile db --profile debug up  # starts all three
```

The `COMPOSE_PROFILES` environment variable is an alternative to the CLI flag.

### Design intent

Profiles implement **optional feature groups** — tools, sidecars, auxilliary databases, or monitoring stacks that should be available on demand without polluting the default `up`. They avoid the maintenance overhead of maintaining separate compose files for "with monitoring" vs "without monitoring".

### Relevance to Terraform gap

Terraform's equivalent is conditional resource creation via `count = var.enable_feature ? 1 : 0`, which works but is verbose, requires explicit variable definitions, and doesn't compose as cleanly at the CLI invocation level. Compose profiles are a first-class verb.

---

## 4. `depends_on` — Startup Ordering and Health Conditions

### Short form

```yaml
services:
  api:
    depends_on:
      - db
      - cache
```

Compose starts `db` and `cache` before `api`. No health guarantees — it waits only for the containers to *start*, not to become ready.

### Long form with conditions

```yaml
services:
  api:
    depends_on:
      db:
        condition: service_healthy
        restart: true
      migrations:
        condition: service_completed_successfully
```

Available conditions:

| Condition                        | Meaning                                                          |
|----------------------------------|------------------------------------------------------------------|
| `service_started`                | Container has started (default — same as short form)            |
| `service_healthy`                | Container's `healthcheck` is passing                            |
| `service_completed_successfully` | Container exited with code 0 (useful for migration jobs)        |

The `restart: true` field causes the dependent service to restart if the dependency restarts.

### Design intent

Application startup has domain-specific ordering requirements (migrations before API, database before migrations) that are invisible to infrastructure-level orchestrators. Expressing these in the desired-state file means Compose can enforce them without wrapper scripts.

### Relevance to Terraform gap

Terraform has `depends_on` but it is purely for resource-graph ordering within the plan/apply, with no concept of runtime readiness. Terraform cannot express "wait for the database to accept connections before starting the app server."

---

## 5. Variable Interpolation and `.env` Files

### `.env` file

Compose automatically loads `.env` from the working directory and makes those values available for interpolation. No explicit reference needed.

```
# .env
POSTGRES_VERSION=15
APP_PORT=8080
```

### Interpolation syntax

Inside `docker-compose.yml`, variables are referenced with `${}`:

```yaml
services:
  db:
    image: postgres:${POSTGRES_VERSION:-14}
  api:
    ports:
      - "${APP_PORT:-3000}:3000"
```

Supported forms:

| Syntax              | Meaning                                              |
|---------------------|------------------------------------------------------|
| `${VAR}`            | Value of VAR; error if not set                       |
| `${VAR:-default}`   | Value of VAR if set and non-empty, else `default`    |
| `${VAR-default}`    | Value of VAR if set (even if empty), else `default`  |
| `${VAR:?error msg}` | Value of VAR if set, else fail with error message    |
| `${VAR?error msg}`  | As above but allows empty string                     |

### Multiple env files

`--env-file` overrides the default `.env` file path. Multiple `--env-file` flags stack, later files winning on conflicts.

### `env_file` on a service

A service can declare `env_file` to load variables *into the container's environment*, distinct from variables used for interpolation in the compose file itself. These are different concerns.

### Design intent

Variable interpolation solves the environment-specificity problem without requiring separate compose files per environment. The `.env` file is the environment's "inputs" — it changes per deployment context; the compose file stays stable.

---

## 6. `docker compose config` — Resolved Desired State

### What it does

```
docker compose config
```

Reads all active compose files (base + overrides), applies all merges, resolves all variable interpolation, expands `extends`, and prints the final, normalised YAML to stdout. No containers are started or changed.

### Flags

| Flag             | Effect                                                             |
|------------------|--------------------------------------------------------------------|
| `--quiet`        | Validate only; suppress output                                     |
| `--services`     | Print only the list of service names                               |
| `--volumes`      | Print only the list of volume names                                |
| `--profiles`     | Print only profile names                                           |
| `--format json`  | Output as JSON rather than YAML                                    |

### Design intent

`docker compose config` embodies a separation between **configuration authoring** and **configuration execution**. It answers "what *would* happen if I ran `up`?" before anything runs. This:

1. Enables human review of the merged, interpolated configuration before applying it.
2. Enables CI pipelines to validate configuration correctness without a Docker daemon.
3. Makes the "resolved desired state" a first-class CLI artifact, not an internal detail.

### Relevance to Terraform gap

`terraform plan` shows resource *changes* but assumes a running state backend. `docker compose config` shows the *full* desired state independent of any existing infrastructure — it is closer to a "render" or "print resolved config" verb with no side effects whatsoever.

---

## 7. Lifecycle Verbs and Reconciliation

### Core verbs

| Command                        | Effect                                                                                           |
|-------------------------------|--------------------------------------------------------------------------------------------------|
| `docker compose up`           | Create and start all active services. Reuse unchanged containers.                                |
| `docker compose up --build`   | Force image rebuild before starting.                                                             |
| `docker compose down`         | Stop and remove containers, default networks. Volumes and images retained by default.            |
| `docker compose down -v`      | Also remove volumes.                                                                             |
| `docker compose stop`         | Stop containers without removing them.                                                           |
| `docker compose start`        | Start previously stopped containers.                                                             |
| `docker compose restart`      | Stop then start.                                                                                 |

### Reconciliation

On `up`, Compose compares the desired state (compose file) against actual running containers using Docker labels. If a container matches the desired configuration, it is left alone. If the configuration has changed, the container is recreated. `--force-recreate` bypasses the comparison and recreates all containers regardless.

### Orphan detection

Compose labels every container it creates with:

```
com.docker.compose.project=<project-name>
com.docker.compose.service=<service-name>
```

On `up`, Compose queries all containers bearing the project label and identifies any whose service name is not present in the current compose file. These are **orphan containers** — resources that were once desired but are no longer declared. Compose warns about them and removes them when `--remove-orphans` is passed.

This label-based reconciliation is the mechanism:

1. Compose creates the label at creation time.
2. On subsequent `up`, Compose re-queries by project label.
3. Any labeled container not matching a current service definition is an orphan.

### Design intent

Orphan detection implements a form of **drift detection**: the running environment includes resources not present in the current desired state. This is directly analogous to Terraform detecting resources in state that are no longer in `.tf` files.

---

## 8. Compose Watch / Develop Mode

### `watch` block on a service

```yaml
services:
  api:
    develop:
      watch:
        - path: ./src
          action: sync
          target: /app/src
        - path: ./package.json
          action: rebuild
```

### Actions

| Action    | Behaviour                                                                              |
|-----------|----------------------------------------------------------------------------------------|
| `sync`    | Copy changed files into the running container without restarting.                      |
| `rebuild` | Rebuild the image and recreate the container on change.                                |
| `sync+restart` | Sync files, then restart the container process.                                   |

### Invocation

```
docker compose watch          # watch all services that declare watch rules
docker compose up --watch     # combine up and watch
```

### Design intent

Develop mode decouples the inner loop (file edits) from the outer loop (full image rebuild). It expresses *how to react to changes* as a declarative rule alongside the service definition, rather than requiring external file-watcher tooling. This keeps the development workflow configuration co-located with the deployment configuration.

### Relevance to Terraform gap

Terraform has no analogous concept. Infrastructure resources don't benefit from file-sync, but the design pattern — **declarative reaction rules co-located with the resource definition** — has potential in tool development workflows.

---

## 9. Project Naming and Resource Identification

### Project name

Every Compose application has a **project name**. Default: the directory name containing the compose file. Overridden by:

- `--project-name` / `-p` CLI flag
- `COMPOSE_PROJECT_NAME` environment variable
- `name:` top-level key in the compose file (Compose v2)

### Container naming

Compose derives container names from:

```
<project-name>-<service-name>-<replica-index>
```

Example: project `myapp`, service `api`, first replica → `myapp-api-1`.

Network names follow the same pattern: `<project-name>_<network-name>`.
Volume names: `<project-name>_<volume-name>`.

### Why this matters

The project-name prefix:

1. **Namespaces** all resources, preventing collisions when multiple projects run on the same Docker host.
2. **Groups** resources for lifecycle operations — `docker compose down` removes all containers bearing the project label, regardless of their individual names.
3. **Enables orphan detection** — Compose can enumerate "everything that belongs to project X" by querying the Docker label.

### Design intent

Project-scoped naming is a **logical grouping mechanism** that elevates the application as a first-class entity, not just a collection of individual containers. Operations act on the project, not on individual containers.

### Relevance to Terraform gap

Terraform workspaces and module namespaces solve a similar problem but at the infrastructure level. Compose's approach — a single `name:` in the compose file that propagates to all child resources automatically — is simpler and produces auditable, predictable resource names.

---

## Patterns to Borrow for AspireForm

### Override-file layering for per-environment config

Adopt the base-plus-override model: a canonical `aspireform.yml` describes the application; environment-specific files (`aspireform.override.dev.yml`, `aspireform.override.prod.yml`) express only the delta. Merge rules should follow Compose semantics — mappings deep-merge, sequences replace — so an override adding one connection string does not wipe the base. Multiple `-f` flags or a `ASPIREFORM_FILES` variable enable arbitrary stacking.

### Profiles for optional feature groups

Introduce a `profiles:` key on resources (Aspire components, sidecars, observability stacks). Resources without a profile are always active; others activate only when the named profile is selected at invocation time. This replaces conditional booleans scattered across multiple variables with a composable, named activation model.

### The `config` command as a "show resolved desired state" verb

Implement `aspireform config` (or equivalent) that reads all active files, applies merges and variable interpolation, and prints the fully resolved configuration — no side effects. This is the standard gate before `apply`: reviewable in CI, diffable, and independent of any running infrastructure. It makes the tool auditable and safe to run in read-only pipelines.

### Dependency conditions

Extend Aspire's `WaitFor` / dependency model with explicit condition semantics: `started`, `healthy` (container health check passing), and `completed_successfully` (one-shot job exited 0). These map naturally to Aspire's existing health-check and wait APIs. Expressing them declaratively in the config file — rather than in C# startup code — makes the dependency graph inspectable and composable without code changes.

### Orphan detection as drift-detection analogue

Label every resource AspireForm creates with the project name and resource key. On each `apply`, enumerate all labeled resources and compare against the current desired state. Any labeled resource whose key is absent from the current config is an **orphan** — it was once desired but is no longer declared. Surface orphans as warnings (or errors in strict mode) and provide a `--remove-orphans` flag. This gives AspireForm the same class of drift detection as Terraform's state comparison, but using resource-level labels rather than a separate state file.
