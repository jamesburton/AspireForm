# AspireForm — Figma → UI Generation — Design Sketch

- **Date:** 2026-05-26
- **Status:** Deferred (design only — implementation pending)
- **Scope:** Sub-project #5.4 — Figma REST API → Blazor component scaffold generation
- **Estimated version:** 0.10.0 (after Theme Editor 0.7.0, Blazing Story 0.8.0, Designer 0.9.0)

---

## 1. What it is

A new `aspireform figma` verb family that authenticates against the Figma REST API and translates a
Figma file's design tokens and component hierarchy into Blazor scaffold output inside the user's
Aspire solution:

- **Design tokens** — Figma color styles, text styles, and spacing primitives are extracted and
  merged into `.aspireform/theme.json` (the Theme Editor's token store), so they immediately affect
  the AspireForm UI shell and can be referenced by generated components.
- **Component scaffolds** — Figma frames or components designated for export are mapped to `.razor`
  files (ownership mode: `scaffold`) and a companion `.css` file. The Razor output is structural
  (divs + CSS classes); it is not pixel-perfect. Component properties map to Blazor `[Parameter]`
  declarations where the Figma component has named variants.

### Command surface

```
aspireform figma auth                     # store PAT in .aspireform/figma-creds.json (gitignored)
aspireform figma tokens <file-url>        # extract design tokens → .aspireform/theme.json
aspireform figma scaffold <file-url>      # emit component stubs for all exportable frames
aspireform figma scaffold <file-url> --frame "Home Screen"   # single frame
```

---

## 2. Architecture

```
Figma REST API
    │  GET /v1/files/:key, GET /v1/files/:key/styles
    ▼
FigmaApiClient (new)
    │  raw Figma file + styles JSON
    ▼
FigmaTokenExtractor (new)         → .aspireform/theme.json (merge into theme store)
FigmaComponentMapper (new)        → List<ComponentBlueprint>
    ▼
RazorScaffoldEmitter (new)        → writes .razor + .css files via Executor's ownership model
```

All new types live under `src/AspireForm/Figma/`.

The `FigmaApiClient` makes authenticated HTTPS calls; the credential is a Figma Personal Access
Token stored in `.aspireform/figma-creds.json` (which should be `.gitignore`d by `aspireform new`).

---

## 3. Risks

1. **Figma API authentication** — PAT storage is simple but brittle (no rotation). OAuth PKCE flow
   would be safer but requires a browser round-trip and a local HTTP listener.
2. **Node-tree fidelity** — Figma auto-layout maps imperfectly to CSS flexbox. Complex nested groups
   may produce deeply-nested divs with no semantic value.
3. **Credential security** — `.aspireform/figma-creds.json` must not be committed. `aspireform new`
   must add it to `.gitignore`. Doctor must warn if it is absent.
4. **Dependency surface** — one new HTTP client (can use `System.Net.Http.HttpClient` — no new
   NuGet package). JSON deserialization of Figma's large response payload needs care (System.Text.Json
   source-gen recommended).
5. **Scope creep** — Figma files can be enormous. A practical v1 must hard-limit to a named page or
   frame list; full-file recursion should be opt-in.

---

## 4. Deferred decisions

- PAT vs OAuth PKCE — decide before implementation starts.
- Which Figma node types are in-scope (FRAME, COMPONENT, INSTANCE, GROUP — at minimum FRAME + COMPONENT).
- Whether `figma scaffold` emits into an existing Blazor project (user specifies `--blazor-project`)
  or creates a new one.
- Versioning: the Figma file version to pin to (or always pull `latest`).
