# AspireForm — Drag-and-Drop Designer — Design Sketch

- **Date:** 2026-05-26
- **Status:** Deferred (design only — implementation pending)
- **Scope:** Sub-project #5.3 — Visual page composer inside aspireform ui
- **Estimated version:** 0.9.0

---

## 1. What it is

A `/designer` page added to `aspireform ui` where the user assembles a Blazor page by dragging
pre-built component tiles from a palette onto a design surface. The result is serialized to a
designer model (`.aspireform/pages/<PageName>.designer.json`) and can be applied as a scaffold-mode
`.razor` file into the user's Blazor project via `aspireform apply`.

The component palette initially contains:

- **Layout primitives** — row, column, card, tab panel.
- **Entity-bound components** — entity grid (list), entity form (create/edit), entity detail view;
  sourced from the entity catalog built in #4a.
- **Static widgets** — heading, paragraph, divider, badge.

The designer is not a pixel-perfect WYSIWYG canvas. It produces a semantic component tree that maps
to readable Blazor markup. The output is intended as a starting point the user completes by hand.

---

## 2. Architecture

```
/designer Blazor page
├── ComponentPalette.razor        draggable tile list; sourced from IComponentRegistry
├── DesignCanvas.razor            droppable target; renders DesignNode tree via JS interop
├── PropertyPanel.razor           shows editable properties of the selected node
└── DesignerToolbar.razor         Undo / Redo / Apply / Export buttons

IComponentRegistry (new)         combines built-in tiles + entity-bound components from IEntityCatalogService
DesignerModel (new)              DesignNode tree + metadata; serialized to .designer.json
RazorPageEmitter (new)           walks DesignerModel → emits .razor scaffold file
```

Drag-and-drop interaction requires JavaScript interop. The minimal viable approach uses HTML5
drag-and-drop events (`dragstart` / `dragover` / `drop`) bridged to Blazor via `IJSRuntime`.
A CSS grid canvas (fixed columns) provides snapping. More fluid snapping (fractional positioning)
is deferred to a later iteration.

Undo/redo is a command stack of `DesignNode` tree snapshots (simple deep-clone stack, max 50 deep).

---

## 3. Risks

1. **JS interop complexity** — HTML5 drag-and-drop is notoriously inconsistent across browsers.
   `DragEvent.dataTransfer` serialization needs careful handling in Blazor Server (each event is a
   SignalR round-trip). A proof-of-concept spike is strongly recommended before full implementation.
2. **Canvas performance** — Large pages with many nodes may cause slow SignalR round-trips per
   drag event. Consider debouncing drag-over updates and only committing on drop.
3. **DesignerModel ↔ Razor fidelity** — Some compositions (deeply nested conditionals, `@foreach`
   loops) cannot be losslessly round-tripped through the visual model. The emitter should warn when
   the loaded `.designer.json` cannot represent what the current `.razor` file contains.
4. **Entity component generation** — Generating a functional entity grid from the catalog requires
   the entity schema to be stable. If the entity model changes after page generation, the emitted
   Razor must still compile (use generic-enough bindings).

---

## 4. Deferred decisions

- Whether Apply writes the `.razor` directly or goes through the `aspireform apply` plan/approve
  loop (the latter is safer but slower for an interactive design session).
- Whether the designer supports multiple pages simultaneously or is single-page-at-a-time.
- MCP tool surface: at minimum `aspireform_designer_apply <page>` so an agent can trigger emit.
- The column-count and snap resolution of the CSS-grid canvas.
- Whether entity-bound components auto-inject the relevant scoped service or emit static markup stubs.
