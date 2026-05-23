# CLAUDE.md — AspireForm

Architectural and technical guidance for agents working on AspireForm.

## What this is

AspireForm is a .NET 10 tool that constructs and configures .NET Aspire applications declaratively.
It layers a Terraform-style `plan`/`apply` reconciliation loop and Docker-Compose-style config
ergonomics on top of the official `aspire` CLI.

## Read first

- `docs/superpowers/specs/2026-05-22-aspireform-core-engine-design.md` — the authoritative design.
- `docs/research/` — background on Terraform, Aspire 13.x, `dnx`, the verticals, and Docker Compose.
- `docs/superpowers/plans/` — the implementation plans (3 for the core engine).

## Core concepts

- **Resource** — infrastructure (SQL Server, Redis, …); managed and safely destroyable.
- **Module** — a feature slice that scaffolds cross-layer code; destroy-protected by default.
- **Ownership mode** — every generated file is tagged `managed`, `scaffold`, or `merge`, which
  determines what `apply` does to it on re-run.
- **State** — `.aspireform/state.json` (source of truth) plus in-file `// aspireform:` markers.

## Conventions

- Target framework `net10.0`. C# nullable enabled, implicit usings enabled.
- Tests: xUnit v3 on the Microsoft Testing Platform; assertions via `AwesomeAssertions`.
- Public types and members carry XML doc comments.
- The config pipeline is format-agnostic: YAML and JSONC both normalize to a
  `System.Text.Json.Nodes.JsonObject` DOM before any logic runs.
- All interaction with the `aspire` CLI goes through `IAspireCli` — never shell out directly.

## Build & test

    dotnet build
    dotnet test
    dotnet pack src/AspireForm/AspireForm.csproj -o ./artifacts
