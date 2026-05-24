# Changelog

## [0.1.0] - 2026-05-24

Initial release. Microsoft Data API Builder (DAB) Resource provider for AspireForm.

### Added
- `dab` block type emitting `aspire add dab` + managed AppHost region with `builder.AddDataAPIBuilder(...)`.
- Optional `databaseReference` input wiring `.WithReference(...)` to a sibling database resource.
- Scaffold `dab-config.json` with minimal schema, data-source, runtime, and empty entities map.
