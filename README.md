# AspireForm

Declarative construction and configuration of [.NET Aspire](https://aspire.dev) applications —
Infrastructure-as-Code ideas (Terraform) and declarative orchestration (Docker Compose) applied
to scaffolding and evolving an Aspire solution.

You describe the desired shape of your app in `aspireform.yaml` (or `aspireform.jsonc`); AspireForm
reconciles that against what is on disk and applies the difference.

## Status

v0.2.0 — Core Engine complete. Reconciles a declarative `aspireform.yaml` against on-disk state
for the built-in `sqlserver` and `ef-data` blocks. External plugins, full Module wiring, and
additional verticals arrive in the verticals-catalog sub-project.

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

## Documentation

- Design spec: `docs/superpowers/specs/`
- Research notes: `docs/research/`
- Implementation plans: `docs/superpowers/plans/`
