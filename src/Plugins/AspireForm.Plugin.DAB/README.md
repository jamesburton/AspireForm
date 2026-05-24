# AspireForm.Plugin.DAB

Microsoft Data API Builder (DAB) Resource provider for [AspireForm](https://github.com/jamesburton/AspireForm).
DAB exposes REST and GraphQL endpoints over a database using a declarative JSON config file.

## Block type
`dab` (Resource)

## Inputs
| Input | Type | Default | Description |
|---|---|---|---|
| `aspireName` | string | block name | Name passed to `builder.AddDataAPIBuilder(...)`. |
| `databaseReference` | string | _(none)_ | Block name of the database resource to wire via `.WithReference(...)`. |

## Example
```yaml
resources:
  api:
    type: dab
    aspireName: api
    databaseReference: sql
```
