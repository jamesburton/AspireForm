# AspireForm.Plugin.Redis

External Redis resource provider for [AspireForm](https://github.com/jamesburton/AspireForm).

## Block type

`redis` (Resource)

## Inputs

| Input | Type | Default | Description |
|---|---|---|---|
| `aspireName` | string | block name | Name passed to `builder.AddRedis(...)`. |
| `withDataVolume` | bool | `false` | When true, appends `.WithDataVolume()`. |

## Example

```yaml
resources:
  cache:
    type: redis
    aspireName: cache
    withDataVolume: true
```
