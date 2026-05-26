# AspireForm — Sub-project #5 Stretch Goals Decomposition

- **Date:** 2026-05-26
- **Author:** Orchestrator agent (Sub-project #5)
- **Status:** Approved — defines scope and ordering for all four stretch items

---

## Overview

Sub-project #5 ("Stretch") was defined in the Core Engine spec §13 as four ideas:
*"Figma → UI generation, Blazing Story demo pages, theme editor, drag-and-drop designer."*

These are decomposed below. **Theme editor** is the selected item for immediate end-to-end delivery
as `AspireForm 0.7.0`. Version `0.6.0` is reserved for sub-project #4b (API-definition builder).
The three deferred items have lightweight specs that can drive future implementation plans.

---

## 1. Figma → UI Generation

**What it is:** A pipeline that accepts a Figma file URL or file ID, retrieves the design tokens and
component hierarchy via the Figma REST API, and emits Blazor component scaffolding (.razor files +
accompanying CSS) into a Blazor project that is already part of the user's Aspire solution. The
output would be scaffold-mode files (tagged `aspireform: scaffold`) — not fully managed, so the user
can safely edit them after generation.

The token extraction step maps Figma color/typography/spacing styles to CSS custom properties
(likely feeding the Theme Editor). The component extraction step uses the Figma node tree to produce
rough Blazor component stubs with structural divs and placeholder text; it would not reproduce
pixel-perfect layout but would give a meaningful starting structure.

**Effort:** L — Large. The Figma REST API requires OAuth authentication (user must supply a Personal
Access Token or go through an OAuth flow). The node-tree-to-component mapping is non-trivial (Figma
auto-layout maps imperfectly to CSS flexbox). The Aspire solution must already have a Blazor project
— AspireForm needs to locate it reliably. Integration with the theme editor (emitting tokens as CSS
custom properties) adds cross-feature coupling. Error handling for large or deeply-nested designs
is substantial.

**Dependencies:** Theme Editor (v1 is a prerequisite so emitted tokens have a landing place), #4b
API-definition builder (to wire up generated pages to endpoints), #2 Vertical Catalog (the Blazor
project must exist in the solution).

**Recommended order:** 4th (last). Needs Figma credentials, the Figma token-to-CSS pipeline is
cleanest if the theme editor already defines the token vocabulary, and this is the riskiest item
from an integration-with-external-API standpoint.

---

## 2. Blazing Story Demo Pages

**What it is:** A `blazingstory` sub-command (or an extension to `aspireform ui`) that discovers
Blazor components in the user's solution and generates Blazing Story story files — `.story.razor`
stubs in a `*.Stories` companion project (or subfolder). Blazing Story is a Storybook-like
documentation and sandboxing framework for Blazor. Each story file pre-wires a component with
sample parameters. The user iterates from these stubs.

Concretely: AspireForm would Roslyn-scan `@code` blocks in `.razor` files, extract component
`[Parameter]` declarations, produce a `.story.razor` per component that demonstrates each parameter
with sample values, and optionally scaffold a `[ComponentName].Stories.csproj` test project wired
to `Blazing.Story`.

**Effort:** M — Medium. The Roslyn scanning of Razor parameters requires Razor language services
(not just CSharp analysis), which is a meaningful additional dependency over what the Entity Catalog
already uses. Generating plausible sample values for typed parameters (enums, complex objects) needs
heuristics. The Blazing Story package API is relatively stable but sparsely documented.

**Dependencies:** #4a EF Model Builder (reuses Roslyn scanning infrastructure). The Blazing Story
NuGet package (`Blazing.Story`) must be available.

**Recommended order:** 2nd (after theme editor, before designer). Roslyn skills from #4a carry
over directly. Fewer external unknowns than Figma or the drag-and-drop canvas.

---

## 3. Theme Editor

**What it is (the chosen item to deliver):** A visual panel inside `aspireform ui` where the user
edits CSS design tokens (colors, border styles, spacing) that govern the AspireForm UI shell itself.
Tokens are persisted to `.aspireform/theme.json` in the project directory; the Kestrel host serves
a generated `/theme.css` endpoint that converts the JSON into a `:root { --af-*: ... }` block.
Pages link this endpoint, so changes reload live (with a cache-busting timestamp). The existing
`site.css` is refactored to use `var(--af-*)` throughout so the token set is meaningful.

See the full spec at `docs/superpowers/specs/2026-05-26-aspireform-theme-editor-design.md`.

**Effort:** S — Small. All components already exist: Kestrel, Blazor Server, site.css, UiHost. No
new external integrations. No new NuGet packages. No Roslyn analysis.

**Dependencies:** #4a EF Model Builder (the `aspireform ui` verb and Blazor shell must exist).
Nothing else.

**Recommended order:** 1st — that is, the item being delivered in this sub-project.

---

## 4. Drag-and-Drop Designer

**What it is:** A visual canvas inside `aspireform ui` where the user assembles a page layout by
dragging pre-built Blazor component tiles from a palette onto a design surface, reordering them,
configuring properties in a side panel, and then exporting the result as a `.razor` file (scaffold
mode) into their Blazor project. The component palette would initially cover AspireForm-generated
entity grid/form components from #4a, plus generic layout primitives (grid row, card, tab panel).

This is closer to a page-composer than a full low-code designer. State is held in a designer model
(a list of positioned component instances with property bags), which is serialized to
`.aspireform/pages/<PageName>.designer.json`. Applying the page exports it to .razor.

**Effort:** L — Large. Drag-and-drop on a Blazor Server canvas requires either JavaScript interop
(for the drag events and grid snapping) or a significant CSS-grid-based interaction model. Neither
is trivial. Managing the mapping from designer model to Razor output for arbitrary component
compositions is complex. The undo/redo model for a visual canvas is a project in itself.

**Dependencies:** Theme Editor (so the preview respects the active color theme), #4a EF Model
Builder (component palette sources entity-bound components), optionally Blazing Story (for previewing
components before placing them).

**Recommended order:** 3rd (after theme editor and Blazing Story, before or after Figma — both are
large). The JS interop for drag-and-drop is the biggest risk; prototype that first before committing
to full implementation.

---

## Recommended Implementation Order

| Priority | Item | Version | Rationale |
|---|---|---|---|
| 1 | Theme Editor | 0.7.0 | Smallest, fewest unknowns, ships immediately in this sub-project |
| 2 | Blazing Story | 0.8.0 | Reuses #4a Roslyn skills, medium effort, clear value |
| 3 | Drag-and-Drop Designer | 0.9.0 | Large but self-contained after #1 sets the token foundation |
| 4 | Figma → UI Generation | 0.10.0 | Largest, external API dependency, benefits from all prior items |
