# AspireForm.Plugin.Hangfire

Hangfire background jobs Module provider for [AspireForm](https://github.com/jamesburton/AspireForm).
Scaffolds a `HangfireSetup.cs` helper and records the storage dependency in a managed AppHost region.

## Block type

`hangfire` (Module)

## Inputs

| Input | Type | Default | Description |
|---|---|---|---|
| `storage` | string | `sql` | Storage backend: `sql` (SQL Server) or `redis`. |
| `dependsOn` | string[] | — | Names of the storage block(s) this module depends on (informational). |
| `dashboardPath` | string | `/hangfire` | URL path for the Hangfire dashboard. |

## Example

```yaml
modules:
  jobs:
    type: hangfire
    storage: sql
    dependsOn: [db]
    dashboardPath: /jobs
```
