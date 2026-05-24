# AspireForm.Plugin.Auth.Common

Shared substrate library for the AspireForm auth plugin family (ApiKey, MagicLink, Entra).

## Purpose

This is **not** an AspireForm plugin itself — it is a regular `net10.0` class library that the three auth plugin implementations reference as a transitive dependency. It exports common helpers and naming conventions so the auth plugins remain consistent.

## Contents

### `AuthScaffold`

Static helper for generating scaffold content.

- `RenderRegistrationComment(string variant, string projectName)` — returns a multi-line comment block describing where and how to wire `AddAuthentication` / `UseAuthentication` for the given auth variant.

### `AuthMarkerNames`

Static helper for aspireform marker-region naming conventions.

- `Marker(string variant)` — returns the marker block name for the given auth variant (e.g. `"apikey"` → `"auth-apikey"`).

## Versioning

| Version | Notes |
|---------|-------|
| 0.1.0   | Initial release — thin substrate with `AuthScaffold` and `AuthMarkerNames`. |
