# Changelog

All notable changes to AspireForm are recorded here. Format loosely follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); the project uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-05-23

Initial release. Foundation of the AspireForm Core Engine (Plan 1 of 3).

### Added

- `dnx`-runnable .NET 10 tool packaged as `DotnetTool` (`dnx AspireForm <verb>`).
- `aspireform config [--project-dir DIR] [--env ENV]` — prints the fully
  merged and interpolated desired-state configuration as indented JSON.
- `aspireform doctor` — checks the .NET 10 SDK and the `aspire` CLI
  prerequisites, with remediation guidance on failure.
- Format-agnostic configuration pipeline: YAML and JSONC both normalize to
  a `System.Text.Json.Nodes.JsonObject` DOM; parity is enforced by test.
- Docker-Compose-style override file layering (`aspireform.<env>.yaml` is
  deep-merged over the base; mappings recurse, sequences replace, explicit
  null in the override removes the key).
- `${VAR}` and `${VAR:-default}` interpolation, sourced from `.env` plus
  process environment variables (process environment wins on collision).
- Schema-validating model binder producing the canonical `ProjectModel`,
  with `Resources` and `Modules` blocks and a plugin-ready `Inputs` bag
  per block. Friendly error messages on type mismatches and bad `dependsOn`.
- State store on `.aspireform/state.json` (consumed by the planner in
  Plan 2), including per-block `Inputs` for drift detection.
- Minimal `IAspireCli` seam (extended by Plan 3).
- `examples/sample/` fixture and end-to-end CLI smoke tests.

### Known limitations / not in 0.1

The Plan 1 scope ships only `config` and `doctor`. The reconciliation
verbs (`new`, `add`, `plan`, `apply`, `destroy`, `import`, `state`) arrive
in Plans 2 and 3. See `docs/superpowers/specs/` for the full design and
`docs/superpowers/plans/` for the staged implementation plans.
