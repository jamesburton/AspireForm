# Terraform Architecture: In-Depth Research Note

> **Purpose:** Design reference for AspireForm — a tool that applies Terraform-like
> Infrastructure-as-Code concepts to scaffolding and configuring .NET Aspire applications.
>
> **Date:** 2026-05-22

---

## 1. State Management

### Mechanism

Terraform maintains a **state file** (`terraform.tfstate`) — a JSON document that records
the last-known mapping between each resource block in configuration and its real-world
counterpart. For every managed resource it stores:

- The resource type, name, and provider.
- All input attribute values used during the last successful apply.
- All computed attribute values returned by the provider after creation (IDs, ARNs, generated
  names, connection strings, etc.).
- Resource metadata: schema version, dependencies, taint status.
- A `serial` counter incremented on every write (used to detect concurrent writes).

The desired state is the configuration (`.tf` files). The actual state as Terraform knows
it is the state file. The actual state of the world is whatever is running in the cloud
right now.

**Desired-vs-actual reconciliation** works like this: during `plan`, Terraform calls the
provider to refresh each resource's current attributes from the API, then diffs the refreshed
state against the configuration. The diff drives the plan: add, update, replace, or
no-op.

**State locking** prevents concurrent runs from corrupting the state file. Remote backends
(S3+DynamoDB, Azure Blob + Lease, Terraform Cloud) implement a lock-acquire/release
protocol. If a process holds the lock and crashes, an operator can force-unlock. Local
state uses a `.terraform.tfstate.lock.info` file and OS-level file locking.

**Remote backends** (S3, Azure Blob, GCS, Terraform Cloud, etc.) store the state file
outside the local filesystem, enabling team collaboration, history retention, and
encryption at rest. The backend configuration lives in the `terraform {}` block. A
backend can also provide locking and remote plan execution.

**`terraform refresh`** (deprecated as a standalone command in Terraform ≥ 0.15 in favour
of `terraform apply -refresh-only`) calls every provider's read operation for every
resource in state and updates the state file to reflect the real-world values — without
changing any configuration or making any infrastructure changes.

### Design Intent

The state file is Terraform's memory. Without it, Terraform cannot know what it has
already created, and would attempt to create resources afresh on every run. The serial
counter and locking protect state integrity in team environments. Separating "what I
think the world looks like" (state) from "what the world should look like" (config)
is the fundamental enabler of the plan/apply idempotency guarantee: re-running an
apply on unchanged config is a no-op.

---

## 2. The Resource Lifecycle

### Mechanism

**Plan** is a read-only operation that computes an execution plan: Terraform loads config,
reads state, optionally refreshes from providers, then for each resource determines
whether to create, update (in-place), replace (destroy + re-create), or delete. The plan
is serialized as a binary `.tfplan` file when saved with `-out`.

**Apply** executes the plan. Terraform walks the dependency graph and runs provider
operations in topological order. Parallelism is automatic where no dependency edges
exist; the default parallelism is 10.

**Destroy** is syntactic sugar for a plan/apply where every resource has a deletion
action. It can also be achieved with `terraform apply -destroy`.

**The dependency graph** is a directed acyclic graph (DAG) of resources and data sources.
Edges are added automatically from interpolated references (e.g., `aws_vpc.main.id`
appearing in another resource's config) and explicitly via `depends_on`. Terraform uses
the graph to determine creation/deletion order and to maximize parallel execution.

**Create/Update/Replace/Delete decisions:**

- **Create** — resource exists in config, not in state.
- **Update (in-place)** — resource in both; provider schema indicates the changed
  attribute supports in-place mutation.
- **Replace** — provider schema marks a changed attribute as `ForceNew`; old instance
  destroyed, new one created. With `create_before_destroy`, the new instance is created
  first; without it, the old instance is destroyed first.
- **Delete** — resource in state, not in config.
- **No-op** — config and refreshed state match.

**`lifecycle` meta-arguments** (present on every resource block):

| Argument | Effect |
|---|---|
| `create_before_destroy = true` | Spin up the replacement before tearing down the old one (useful for zero-downtime deploys). |
| `prevent_destroy = true` | Terraform errors if a plan would destroy this resource; protects critical data stores. |
| `ignore_changes = [attr, ...]` | Terraform ignores drift in the listed attributes; useful when an external system mutates attributes Terraform doesn't own. |
| `replace_triggered_by = [ref, ...]` | Forces a replace when another resource or attribute changes. |
| `precondition` / `postcondition` | Inline validation rules; plan/apply errors if conditions are not met. |

### Design Intent

The plan/apply split gives operators a safe, human-reviewable gate before any
infrastructure changes. The dependency graph means the user never has to think about
ordering; Terraform handles it. `lifecycle` blocks are escape hatches for the cases where
Terraform's default behaviour (destroy-before-create, delete on removal, track all
changes) would cause data loss or downtime. The separation between `ForceNew` (provider
decides) and in-place update (also provider decides) keeps the complexity in the provider,
not in user configuration.

---

## 3. Providers and the Plugin Model

### Mechanism

A **provider** is a compiled Go plugin that implements a gRPC interface defined by the
Terraform Plugin SDK (or Plugin Framework). Each provider binary is responsible for:

- Declaring a schema: the set of resource types and data source types it can manage,
  with their attribute names, types, validation rules, and `ForceNew` flags.
- Implementing CRUD operations (Create, Read, Update, Delete) for each resource type,
  mapping HCL attributes to API calls.
- Implementing Read for data sources.
- Optionally implementing import logic.

Providers are distributed via the **Terraform Registry** (`registry.terraform.io`) using
a namespace/type convention (e.g., `hashicorp/aws`, `hashicorp/azurerm`). The `required_providers`
block in `terraform {}` specifies the source address and version constraint. `terraform init`
downloads providers from the registry (or a mirror/vendor directory) and caches them in
`.terraform/providers/`.

The provider schema is the contract. Terraform itself has no knowledge of AWS, Azure, or
any cloud. All resource-specific logic lives in the provider. Terraform's engine is a
generic orchestrator that consults provider schemas to understand what it can and cannot
do in-place vs. requiring replacement.

**Data sources** are read-only resources that query existing infrastructure and expose
values for use elsewhere in configuration (e.g., look up an existing VPC by tag). They
are fetched during plan.

### Design Intent

The plugin model makes Terraform infinitely extensible without changing the core. Any
team can write a provider for any API. The gRPC boundary provides language independence
(providers can be written in any language with a gRPC library, though Go dominates). The
registry provides discovery and versioning. Pinning provider versions via
`required_providers` ensures reproducible infrastructure across runs.

---

## 4. Configuration Language (HCL)

### Mechanism

**HashiCorp Configuration Language (HCL)** is a declarative, human-readable language
with JSON as a subset. Key constructs:

**Resource block** — the primary unit:
```hcl
resource "aws_s3_bucket" "my_bucket" {
  bucket = "my-unique-name"
  tags   = { Environment = "prod" }
}
```

**Variable** — parameterise a module or root config. Declared with `variable "name" {}`,
consumed as `var.name`. Supplied via `.tfvars` files, environment variables (`TF_VAR_*`),
or CLI flags. Support default values, type constraints, and validation blocks.

**Output** — export values from a module or root config. Declared with
`output "name" { value = ... }`. Root outputs are printed after apply. Module outputs
are consumed by calling modules via `module.name.output_name`.

**Locals** — intermediate named values computed from expressions, avoiding repetition:
```hcl
locals {
  env_prefix = "${var.environment}-${var.region}"
}
```

**Expressions** — HCL supports string interpolation (`"${...}"`), conditional expressions
(`condition ? true_val : false_val`), collection manipulation (`for` expressions,
splat `[*]`), and built-in functions (`length()`, `merge()`, `jsonencode()`, etc.).

**`count`** — creates N instances of a resource. Each is addressed as
`resource_type.name[index]`. Simple but inflexible: inserting an element mid-list
renumbers indices, causing Terraform to destroy and re-create.

**`for_each`** — creates one instance per element in a map or set of strings. Each is
addressed by its key: `resource_type.name["key"]`. Stable identity regardless of
insertion order; preferred over `count` for collections.

**Provisioners** — attached to resources; execute scripts during create or destroy. Types:
`local-exec` (runs on the machine executing Terraform), `remote-exec` (runs on the
created resource via SSH/WinRM), `file` (copies files). Covered in depth in section 9.

### Design Intent

HCL prioritises readability over generality. It is deliberately not a full programming
language (no loops as statements, no mutable state). Expressions and `for_each` provide
the minimum dynamism needed for practical infrastructure. The separation of variables,
locals, outputs, and resources gives a clear data-flow model. The JSON-compatibility
means tooling can generate valid HCL programmatically. The design intentionally resists
turning configs into imperative scripts — the plan/apply guarantee depends on the config
being a declaration of desired state, not a procedure.

---

## 5. Modules

### Mechanism

A **module** is a directory of `.tf` files. The **root module** is the working directory
`terraform apply` is run from. A **child module** is called with a `module` block:

```hcl
module "network" {
  source  = "terraform-aws-modules/vpc/aws"
  version = "5.1.2"

  name = "my-vpc"
  cidr = "10.0.0.0/16"
}
```

Module sources can be:
- Local filesystem paths (`./modules/network`).
- The public Terraform Registry (`namespace/module/provider`).
- Git URLs, GitHub/GitLab shortcuts, Mercurial, HTTP archives.

**Inputs** are the variables declared in the child module's `variable` blocks. The calling
module sets them in the `module` block body.

**Outputs** are the values declared in the child module's `output` blocks. The calling
module references them as `module.name.output_name`.

Modules do not expose internal resources to the parent; encapsulation is strict. There is
no way to reference `module.network.aws_subnet.private[0].id` directly — the module
must explicitly output it.

**The Module Registry** at `registry.terraform.io/modules` hosts verified and community
modules. Versioning follows SemVer. `terraform init` resolves version constraints and
downloads module sources alongside providers.

### Design Intent

Modules are the unit of reuse in Terraform. They let teams encode best-practice
infrastructure patterns (networking, IAM, databases) as versioned, parameterised
components that can be composed. The strict input/output boundary forces explicit
interfaces, making modules testable and auditable. Version pinning enables stability
across environments. Modules map directly to software package concepts but for
infrastructure.

---

## 6. Drift Detection

### Mechanism

**Drift** is when the real-world state of infrastructure diverges from what Terraform's
state file records. This can happen because:

- A human made a manual change in the cloud console.
- An external automation system mutated an attribute.
- A cloud service autonomously changed a value (e.g., auto-scaling changed instance count).

Terraform detects drift by calling the provider's **Read** operation for each resource
during a plan (the refresh step). The provider returns the current attribute values from
the API. Terraform diffs those values against the configuration. Attributes that are in
state but not in config (because they're computed) are simply updated in state. Attributes
that ARE in config and now differ from the API response appear as changes in the plan.

`terraform plan -refresh-only` produces a plan that *only* updates the state file to
match the real world, without proposing any remediation changes. This is useful for
auditing what has drifted and deciding whether to accept the drift or correct it.

`ignore_changes` suppresses drift detection for specific attributes — Terraform will not
flag those attributes as changed even if the provider returns a different value.

### Design Intent

Drift detection is what makes Terraform more than a one-shot provisioner. By continuously
reconciling against the real world, it enforces the declared desired state over time.
The operator always knows whether the infrastructure matches what was intended. The
explicit `ignore_changes` escape hatch acknowledges that some attributes (tags managed by
a cost-allocation system, for example) are legitimately owned by other tools.

---

## 7. `terraform import`

### Mechanism

`terraform import` brings an **existing** real-world resource under Terraform management
without recreating it. The command takes a resource address and a provider-specific ID:

```
terraform import aws_s3_bucket.my_bucket my-existing-bucket-name
```

Terraform calls the provider's **Import** function, which fetches the current state of
the resource and writes it into the state file. The resource now appears in state.
However, **configuration is not generated** — the user must write (or generate) the
corresponding resource block manually. Running `plan` after import will show the diff
between the imported state and the configuration, which the user then reconciles.

Terraform 1.5+ introduced **`import` blocks** — a declarative alternative that can be
included in configuration and executed as part of a plan/apply cycle, enabling import
to be part of a repeatable workflow:

```hcl
import {
  to = aws_s3_bucket.my_bucket
  id = "my-existing-bucket-name"
}
```

Terraform 1.5+ also added **`terraform plan -generate-config-out=generated.tf`** which,
when used with `import` blocks, will generate HCL configuration stubs for the imported
resources.

### Design Intent

Import bridges the gap between brownfield infrastructure (created before Terraform was
adopted) and Terraform management. Without it, teams would have to destroy and recreate
existing resources to bring them under management — unacceptable for production systems.
The limitation that config must be written manually (historically) reflects the
fundamental constraint: Terraform's job is to reconcile config with reality, and config
must be authoritative. The new generated-config feature acknowledges the practical
friction and automates the boilerplate, though the generated config still requires
human review.

---

## 8. Workspaces

### Mechanism

A **workspace** is a named instance of state within a single backend configuration.
Every backend starts with a `default` workspace. Additional workspaces are created with
`terraform workspace new <name>` and selected with `terraform workspace select <name>`.

Each workspace has its own isolated state file. The configuration is identical across
workspaces — what differs is the state and any variable values the user supplies. The
current workspace name is available in configuration as `terraform.workspace`, enabling
environment-specific logic:

```hcl
locals {
  instance_size = terraform.workspace == "prod" ? "t3.large" : "t3.micro"
}
```

With the local backend, workspace states are stored in `terraform.tfstate.d/<name>/`.
With remote backends, each workspace maps to a separate state object (a different S3 key,
a different Terraform Cloud workspace, etc.).

**Limitations:** Workspaces share configuration exactly. They are not well-suited to
scenarios where different environments have fundamentally different resource topologies.
For that, separate root modules (directories) per environment — sometimes managed with
Terragrunt — are preferred.

### Design Intent

Workspaces solve the common need to deploy the same infrastructure topology to multiple
environments (dev, staging, prod) from a single configuration. They keep the
environment-switching workflow simple (select + apply) and prevent state cross-contamination.
The design intentionally keeps workspaces lightweight — they are not separate configs,
just separate state stores. This means the same team conventions and code review process
apply to all environments.

---

## 9. Provisioners and Escape Hatches

### Mechanism

**Provisioners** are a last-resort mechanism for running scripts as part of resource
creation or destruction. They run on the Terraform executor's machine (`local-exec`) or
on the created resource via SSH/WinRM (`remote-exec`).

```hcl
resource "aws_instance" "web" {
  ami           = "ami-12345678"
  instance_type = "t3.micro"

  provisioner "local-exec" {
    command = "echo ${self.public_ip} >> inventory.txt"
  }
}
```

Provisioners have significant problems:
- Their success or failure is not tracked in state in a useful way.
- On failure during creation, the resource is marked **tainted** and will be destroyed
  on the next plan/apply.
- They introduce imperative, non-idempotent behaviour into a declarative system.
- They require network connectivity and credentials that may not be available.

**`null_resource`** (provider: `hashicorp/null`) is a resource with no real-world object.
Its only purpose is to host provisioners or to serve as a dependency anchor. It has a
`triggers` argument — a map of values; if any trigger value changes, the null_resource
is replaced, which re-runs its provisioners. Used as an escape hatch for side effects
(run Ansible, invoke an API, run a script) that don't map to a real provider resource.

**`terraform_data`** (built-in to Terraform 1.4+) replaces `null_resource` without
requiring the `hashicorp/null` provider.

**`local-exec` with `external` data source** is another pattern: a data source that
runs an external program and captures its JSON output for use in config.

HashiCorp explicitly discourages provisioners and recommends alternatives:
- Use provider resources where they exist (e.g., `aws_ssm_document` instead of
  `remote-exec` to run a script).
- Use cloud-init / user data for bootstrapping.
- Use configuration management tools (Ansible, Chef) separately.

### Design Intent

Provisioners exist because the real world is messy and providers don't cover every
possible action. They are the "break glass" mechanism. Their discouraged status is
intentional: every provisioner is a hole in Terraform's idempotency and plan guarantees.
The `null_resource` + `triggers` pattern attempts to bring side effects into the
declarative model (by making them reproducible on change), but it's fundamentally an
impedance mismatch. The official guidance is to push side effects out of Terraform and
into the surrounding pipeline.

---

## 10. The Plan Output Format

### Mechanism

`terraform plan` produces a human-readable diff using a consistent visual language:

```
Terraform will perform the following actions:

  # aws_s3_bucket.my_bucket will be created
  + resource "aws_s3_bucket" "my_bucket" {
      + bucket = "my-unique-name"
      + id     = (known after apply)
      + tags   = {
          + Environment = "prod"
        }
    }

  # aws_instance.web will be updated in-place
  ~ resource "aws_instance" "web" {
      ~ instance_type = "t3.micro" -> "t3.large"
        id            = "i-0abcd1234"
    }

  # aws_db_instance.legacy will be destroyed
  - resource "aws_db_instance" "legacy" {
      - id                 = "mydb" -> null
      - allocated_storage  = 20 -> null
    }

  # aws_security_group.old must be replaced
-/+ resource "aws_security_group" "old" {
      ~ name = "old-name" -> "new-name" # forces replacement
    }

Plan: 1 to add, 1 to change, 1 to destroy.
```

**Symbols:**
- `+` — will be created (new attribute value)
- `-` — will be destroyed (removed attribute value)
- `~` — will be updated in-place
- `-/+` — will be destroyed and recreated (replace)
- `+/-` — will be created before destroying (create_before_destroy replace)
- `<=` — will be read (data source)

Values that can't be known until apply (because they're computed by the provider after
creation) are displayed as `(known after apply)`. Sensitive values are displayed as
`(sensitive value)`.

The summary line (`Plan: N to add, N to change, N to destroy`) gives a quick count.
If no changes are needed, Terraform prints `No changes. Infrastructure is up-to-date.`

The saved plan file (`-out=plan.tfplan`) is a binary that can be applied with
`terraform apply plan.tfplan`, guaranteeing that exactly the reviewed plan is executed.

### Design Intent

The plan output is Terraform's primary UX contribution to infrastructure safety. By
showing exactly what will happen — with attribute-level granularity — before any
irreversible changes are made, it converts infrastructure operations from "hope it works"
to "reviewed and approved." The symbol language is dense but learnable; the colour coding
(+ green, - red, ~ yellow) reinforces semantics visually. The saved plan binary closes
the TOCTOU gap between review and execution.

---

## Lessons Transferable to a Code-Scaffolding Tool

### What Maps Well

**1. The plan/apply split is the most transferable concept.**
For AspireForm, this means generating a preview of what files will be created, modified,
or deleted before writing anything to disk. The same `+` / `~` / `-` symbol language is
immediately understandable to .NET developers. An AspireForm plan should show:
`+ src/Services/OrderService/OrderService.csproj will be created` and
`~ AppHost/Program.cs: AddProject("OrderService") will be added`. This gate prevents
irreversible scaffold writes and is the core UX differentiator from a one-shot generator.

**2. State tracking is necessary but must be adapted.**
Terraform's state file maps resource declarations to real-world objects. AspireForm needs
an analogous `.aspireform.state.json` that maps declared resources to the files it
generated. This is the only way to support idempotent re-runs (don't re-create what
already exists), in-place updates (add an attribute to an existing project), and deletion
(remove a resource from config → remove the generated files). Without state, AspireForm
cannot distinguish "I created this file" from "the user created this file."

**3. The dependency graph maps directly to Aspire resource composition.**
Aspire's `WithReference(postgresDb)` is a dependency edge. AspireForm should build a DAG
from resource declarations (web project depends on Redis depends on nothing) and scaffold
in topological order, exactly as Terraform creates infrastructure in dependency order.
This ensures that a database resource is scaffolded before the service that references it.

**4. `for_each` over a collection maps to "scaffold N services of the same type."**
A config like `for_each = var.microservices` generating one Aspire project per entry is
a natural pattern. Using a stable key (service name) rather than a numeric index
prevents re-scaffolding all services when one is inserted — the exact lesson `for_each`
teaches over `count`.

**5. `lifecycle.prevent_destroy` maps to hand-edited file protection.**
If a user has hand-edited a generated file, AspireForm should honour a
`lifecycle { prevent_destroy = true }` equivalent — or detect that the file has diverged
from its generated checksum and refuse to overwrite without explicit confirmation. This
is the code-generation answer to `prevent_destroy`.

**6. `ignore_changes` maps to regions of a file owned by the user.**
Terraform ignores specific attributes; AspireForm can designate regions of generated
files (delimited by comments: `// <aspireform:begin managed />`) as owned by the tool,
leaving the rest as user territory. Changes outside the managed region are not overwritten
on re-apply — the direct analog of `ignore_changes`.

**7. The module/composition model maps to resource blueprints.**
Terraform modules are reusable infrastructure patterns. AspireForm can have a module
registry of Aspire resource templates: a "PostgreSQL with migrations" module that
scaffolds the container resource, the EF Core project, a migration runner, and the
`AppHost` wiring. Versioning, inputs (database name, schema), and outputs (connection
string reference) follow the same pattern.

**8. Provider schema maps to resource type registrations.**
Each AspireForm "provider" is a code generator for a specific resource category (SQL
databases, message buses, gRPC services, Blazor frontends). The provider declares its
schema (what inputs it accepts, what files it generates, what outputs it exposes), and
the core engine remains generic — exactly the Terraform plugin architecture. This enables
third-party providers (community templates for MassTransit, Dapr, etc.).

**9. `terraform import` maps to "adopt existing project."**
If a user already has an `OrderService.csproj`, AspireForm's import command registers it
in state and generates the corresponding resource block — making it a managed resource.
The same limitation applies: the generated resource block needs human review, since
AspireForm can't perfectly reverse-engineer a config from an existing project.

**10. Workspaces map to environment-specific AppHost configurations.**
The same AspireForm config generating an AppHost for dev (using emulators, minimal
replicas) vs. production (real Azure resources, multiple replicas) via workspace
selection is a clean analogy. Workspace name (`aspireform.workspace`) drives environment
variables, replica counts, and whether to use `AddAzureServiceBus` vs.
`AddRabbitMQ`.

---

### What Does NOT Map Well — Critical Asymmetries

**Code generation is not idempotent like cloud APIs.**
When Terraform re-applies an unchanged config, the provider's Create is not called
again — the resource already exists and the provider's Read confirms it matches.
For code generation, re-running a file write always overwrites. This is why the state
file and file-hash tracking are non-negotiable for AspireForm: without them, every
re-apply would stomp user edits. Cloud APIs have their own idempotency keys; files do
not.

**Deleting a resource block cannot safely delete hand-edited code.**
Terraform can destroy a cloud resource without data-loss risk (assuming backups exist).
Deleting a generated `.csproj` or `Program.cs` that a developer has spent hours
customising is irreversible and catastrophic. AspireForm must default to
`prevent_destroy = true` semantics for all generated files, require explicit
`--force-delete` flags, and optionally back up before deletion.

**Drift detection has no API equivalent.**
Terraform detects drift by calling the provider's Read (an HTTP GET to an API).
AspireForm detects drift by comparing file contents against stored checksums. The
mechanism is file-based rather than API-based, but the concept is identical: has the
world diverged from what the tool believes it wrote? A drift report for AspireForm is
"file X has been modified since last apply" — giving the user the same choice as
Terraform's `-refresh-only`: accept the drift (update state checksum) or overwrite it
(re-apply the template).

**There is no `refresh` for source code.**
In Terraform, `refresh` calls the provider and updates state. For code, the file IS the
source of truth — there is no separate API to call. AspireForm's equivalent of refresh
is reading the filesystem and recomputing checksums. This is simpler but means drift
can't be resolved by "calling the API" — if a file diverges, only the human can decide
whether the config or the file is authoritative.

**Provisioners (imperative side-effects) are even more dangerous in code generation.**
In Terraform, a `local-exec` provisioner runs an arbitrary script. For AspireForm, the
equivalent would be running `dotnet add package` or `dotnet ef migrations add`. These
are even harder to make idempotent than cloud API calls. Aspireform should treat these
as explicit, audited commands separate from the declarative apply — shown in the plan as
`! will execute: dotnet add package ...` — and never run them silently.

**The plan output must adapt to file-level granularity.**
Terraform plans at the resource level (whole resource create/update/destroy). AspireForm
must plan at multiple levels: file create/update/delete, AND within a file, show which
lines/sections will change (a diff). The `~` symbol at file level is insufficient;
developers need to see the actual unified diff for modified files before approving.
This is a UX requirement Terraform doesn't face because it doesn't edit text files.

---

*End of research note.*
