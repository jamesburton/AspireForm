# AspireForm.Plugin.ETL

CSV/Excel file ETL import Module provider for [AspireForm](https://github.com/jamesburton/AspireForm).
Scaffolds an `EtlSetup.cs` helper with a `WatchDirectory` constant and `AddEtl` extension method,
and records the watch configuration in a managed AppHost region.

## Block type

`etl` (Module)

## Inputs

| Input | Type | Default | Description |
|---|---|---|---|
| `watchDirectory` | string | `./incoming` | Directory the file watcher polls for new files. |
| `parsers` | string[] | `["csv", "excel"]` | Enabled file format parsers (informational). |
| `dependsOn` | string[] | — | Names of the Hangfire block + database block this module depends on (informational). |

## Example

```yaml
modules:
  import:
    type: etl
    watchDirectory: ./drop
    parsers: [csv]
    dependsOn: [jobs, db]
```
