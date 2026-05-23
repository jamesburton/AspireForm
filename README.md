# AspireForm

Declarative construction and configuration of [.NET Aspire](https://aspire.dev) applications —
Infrastructure-as-Code ideas (Terraform) and declarative orchestration (Docker Compose) applied
to scaffolding and evolving an Aspire solution.

You describe the desired shape of your app in `aspireform.yaml` (or `aspireform.jsonc`); AspireForm
reconciles that against what is on disk and applies the difference.

## Status

Early development. Plan 1 of 3 (Foundations) is in progress: the `config` and `doctor` commands.

## Install / run

AspireForm is a zero-install .NET tool. With the .NET 10 SDK present:

    dnx AspireForm config
    dnx AspireForm doctor

`dnx` resolves the latest published version on each run, so the tool is always current.

## Commands (Plan 1)

| Command | Description |
|---|---|
| `aspireform config` | Print the fully merged and interpolated desired-state configuration. |
| `aspireform doctor`  | Check prerequisites: the .NET 10 SDK and the `aspire` CLI. |

`new`, `add`, `plan`, `apply`, `destroy`, `import`, and `state` arrive in Plans 2–3.

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
