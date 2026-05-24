# Changelog

## [0.1.0] - 2026-05-24

Initial release. Microsoft Entra External ID OIDC authentication Module provider for AspireForm.

### Added
- `auth-entra` block type (Module) scaffolding `EntraAuthSetup.cs` + managed AppHost region.
- `tenantId` input (default `"<tenant-id>"`).
- `clientId` input (default `"<client-id>"`).
- `audience` input (optional, defaults to clientId).
