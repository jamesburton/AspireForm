# Changelog — AspireForm.Plugin.Auth.Common

## [0.1.0] — 2026-05-24

### Added

- `AuthScaffold.RenderRegistrationComment(variant, projectName)` — generates a multi-line comment block describing how to wire `AddAuthentication` / `UseAuthentication` for the given auth variant.
- `AuthMarkerNames.Marker(variant)` — returns the aspireform marker-region block name for the given auth variant (e.g. `"apikey"` → `"auth-apikey"`).
