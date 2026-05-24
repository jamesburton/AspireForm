# Changelog

## [0.1.0] - 2026-05-24

Initial release. Passwordless magic-link authentication Module provider for AspireForm.

### Added
- `auth-magiclink` block type (Module) scaffolding `MagicLinkAuthSetup.cs` + managed AppHost region.
- `fromAddress` input (default `"noreply@example.com"`).
- `tokenLifetimeMinutes` input (default `15`).
