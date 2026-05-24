# AspireForm.Plugin.Reporting

DAB-curated read-only reports Module provider for [AspireForm](https://github.com/jamesburton/AspireForm).
Declared database views become REST and GraphQL endpoints by emitting a sibling `dab-reports.json`
config file that Microsoft Data API Builder (DAB) consumes.

## Block type
`reporting` (Module)

## Inputs
| Input | Type | Default | Description |
|---|---|---|---|
| `dependsOn` | string[] | _(none)_ | Block names this module depends on (informational; typically references the DAB block). |
| `views` | object[] | `[]` | View descriptors: `{ "name": "...", "source": "dbo.MyView", "permissions": [...] }`. |

The `permissions` field defaults to anonymous read (`[{"role":"anonymous","actions":["read"]}]`)
when omitted from a view entry.

## Example
```yaml
modules:
  reports:
    type: reporting
    dependsOn: [api]
    views:
      - name: Sales
        source: dbo.vw_Sales
      - name: Orders
        source: dbo.vw_Orders
        permissions:
          - role: authenticated
            actions: [read]
```

The scaffolded `dab-reports.json` should be added to the DAB block's `configFiles` list so DAB
loads both configs at startup.
