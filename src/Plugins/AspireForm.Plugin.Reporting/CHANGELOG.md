# Changelog

## [0.1.0] - 2026-05-24

Initial release. DAB-curated read-only reports Module provider for AspireForm.

### Added
- `reporting` block type (Module) with `dependsOn` and `views` inputs.
- Scaffold `dab-reports.json` — DAB-config-format file with schema, data-source placeholder, runtime paths, and an `entities` map built from `views[]`.
- Default anonymous-read permissions applied when `permissions` is omitted from a view entry.
