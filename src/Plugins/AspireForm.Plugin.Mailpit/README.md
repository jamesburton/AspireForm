# AspireForm.Plugin.Mailpit

Mailpit Resource provider for [AspireForm](https://github.com/jamesburton/AspireForm).
Mailpit is a local SMTP test mail server that catches outgoing email and presents it in a web UI.

## Block type
`mailpit` (Resource)

## Inputs
| Input | Type | Default | Description |
|---|---|---|---|
| `aspireName` | string | block name | Name passed to `builder.AddMailPit(...)`. |
| `withDataVolume` | bool | `false` | When true, appends `.WithDataVolume()`. |

## Example
```yaml
resources:
  mail:
    type: mailpit
    aspireName: mail
    withDataVolume: true
```
