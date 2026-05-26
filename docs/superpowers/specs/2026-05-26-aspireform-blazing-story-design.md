# AspireForm — Blazing Story Demo Pages — Design Sketch

- **Date:** 2026-05-26
- **Status:** Deferred (design only — implementation pending)
- **Scope:** Sub-project #5.2 — Roslyn-driven Blazing Story stub generation
- **Estimated version:** 0.8.0

---

## 1. What it is

A `aspireform story` verb that Roslyn-scans `.razor` files in the user's Blazor project, extracts
`[Parameter]`-decorated properties from `@code` blocks, and generates Blazing Story `.story.razor`
stubs — one per component — into a companion stories project. The result is a live Storybook-like
catalogue of the user's Blazor components, each showing its parameters wired with sample values.

Blazing Story (`Blazing.Story` NuGet package) is a Blazor-native Storybook equivalent. AspireForm
generates the boilerplate; the user refines sample values and adds more stories.

### Command surface

```
aspireform story scan                     # list discoverable components (dry run)
aspireform story generate                 # emit .story.razor stubs for all components
aspireform story generate --component Book  # single component
aspireform story add --project ./MyApp.Stories   # scaffold a new stories project if absent
```

---

## 2. Architecture

```
User's Blazor project (.razor files)
    │  Roslyn RazorComponentScanner (new, reuses MSBuildWorkspace from EntityCatalog)
    ▼
List<ComponentDescriptor>        { Name, Namespace, Parameters: [{ Name, Type, DefaultValue }] }
    │
    ▼
StoryEmitter (new)               → writes .story.razor files (ownership: scaffold)
                                 → optionally scaffolds *.Stories.csproj
```

The `RazorComponentScanner` extends the existing `RoslynEntityScanner` infrastructure. Razor
parameters are found by parsing `[Parameter]` attributes in `@code` blocks using the Roslyn
CSharp analyzer (Razor Roslyn support `Microsoft.AspNetCore.Razor.Language` may be needed for
full Razor syntax; a fallback using CSharp-only analysis of generated Razor C# is acceptable for v1).

---

## 3. Risks

1. **Razor Roslyn support** — `@code` blocks can be analyzed via standard Roslyn once Razor is
   compiled to C# (the `.razor.g.cs` intermediate file). MSBuildWorkspace handles this transparently
   on a built solution. For projects that don't build cleanly, parameter discovery falls back to
   heuristic attribute scanning.
2. **Sample value generation** — Primitive types (`string`, `int`, `bool`, `DateTime`) get hardcoded
   samples. Enum types use the first declared value. Complex objects produce `new T()` with a comment.
3. **Stories project collision** — if a `*.Stories.csproj` already exists, `aspireform story add`
   should detect it and offer to augment rather than recreate.
4. **Blazing.Story package version** — Blazing.Story is a community package; pinning the version
   in the generated project file is essential to avoid silent breakage.

---

## 4. Deferred decisions

- Whether the stories project is a standalone project or a subfolder of the Blazor project.
- Whether to generate interactive stories (Blazor interactive render mode) or static (SSR only).
- MCP tool surface: likely `aspireform_story_scan` + `aspireform_story_generate` (2 tools, read +
  write) — defer to implementation.
- Whether Blazor component grid/form components from #4a EF Model Builder are auto-enrolled.
