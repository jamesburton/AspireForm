# Changelog

## [0.1.0] - 2026-05-24

Initial release. API-key authentication Module provider for AspireForm.

### Added
- `auth-apikey` block type (Module) scaffolding `ApiKeyAuthSetup.cs` + managed AppHost region.
- `headerName` input (default `"X-API-Key"`).
- `keysSource` input (default `"config"`, accepts `"config"` | `"db"`).
