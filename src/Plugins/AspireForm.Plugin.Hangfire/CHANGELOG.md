# Changelog

## [0.1.0] - 2026-05-24

Initial release. Hangfire background jobs Module provider for AspireForm.

### Added

- `hangfire` block type (Module) emitting a scaffold `HangfireSetup.cs` with storage-specific `AddHangfireWithStorage` extension method.
- Managed AppHost region recording storage choice, dependency, and dashboard path.
- Supports `sql` (SQL Server) and `redis` storage backends.
- No CLI actions in v1 (NuGet packages are added when wiring the service project).
