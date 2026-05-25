# AspireForm.Annotations

Attribute-only library for [AspireForm](https://github.com/jamesburton/AspireForm) code-first entity authoring.

Reference this package from your entity project, then decorate entity classes with:

- `[DabExpose]` — mark entity as exposed via Data API Builder
- `[DabPath("/books")]` — override REST path
- `[DabPermission("anonymous", "read")]` — repeatable; default is `[{anonymous, read}]`
- `[DabRestOnly]` / `[DabGraphqlOnly]` / `[DabHidden]`
- `[OnDelete("Cascade")]` — optional EF helper for cascade behavior

The AspireForm `ef-data` provider reads these attributes via Roslyn and emits a corresponding `dab-config.json`.
