# Plan: EPUB Touch Selection Actions

## Table of Contents

- [Summary](#summary)
- [Technical Approach](#technical-approach)
- [Component Breakdown](#component-breakdown)
- [Dependencies](#dependencies)
- [External Vendor Documentation Evidence](#external-vendor-documentation-evidence)
- [Flow](#flow)
- [Risk Assessment](#risk-assessment)

## Summary

Extend the existing module-based EPUB reader interop so browser selection changes, including Android Chrome touch selection, update Blazor state and drive a contextual floating action menu. The existing note API and clipboard interop remain unchanged.

## Technical Approach

`EpubReader.razor` already owns `_selectedText`, note saving, clipboard status, reader lifecycle, and a module reference. It currently invokes `ebookReader.js:getSelectionText` only from `@onmouseup` and `@onkeyup`; this excludes the Android Chrome long-press selection completion path. Retain the component as the owner of actionable selection state, but replace the event-specific refresh with a module registration that listens for document selection changes and relevant touch/pointer completion within `_contentRef`.

In `WebApp/WebApp.Client/wwwroot/js/ebookReader.js`, add focused selection registration/unregistration beside the existing page-navigation and tap-navigation registrations. The JS module will validate that the range belongs to the supplied reader container, return the trimmed text plus viewport-relative range bounds, and invoke a `[JSInvokable]` callback on `EpubReader`. It must ignore collapsed/external selections and avoid `preventDefault`, so Android Chrome retains its native long-press handles. Debounce or coalesce callback work enough to avoid excessive Blazor renders while a handle is dragged.

In `EpubReader.razor`, replace the static selection toolbar buttons with a conditionally rendered menu within the reader overlay. A small private selection-presentation state (text plus normalized anchor rectangle/placement) is sufficient; do not introduce a broad shared service. The callback updates that state after validating the reader/chapter is still current. Existing `SaveNoteAsync` and `CopySelectionAsync` will use the stored text rather than querying the browser at click time, preventing a toolbar tap from losing the selection.

Position the menu with runtime inline CSS custom properties calculated from the safe geometry supplied by JS: prefer an above-selection position, flip below when the menu would collide with the toolbar or viewport, and clamp its inline coordinate into the reader frame. Scoped CSS in `EpubReader.razor.css` handles only nonstandard overlay/geometry behavior; Bootstrap classes compose the buttons and layout. The action menu must have an accessible label, preserve the current icons/loading status, and stay within the reader's light/dark custom-property scope.

Selection state is reset through the existing `ResetState`, chapter-load, successful-save, and disposal paths. `DisposeAsync` must unregister the selection listener before disposing its `DotNetObjectReference`, mirroring the existing page/tap-navigation cleanup. Existing `registerTapNavigation` should continue to suppress a page change when a selection exists.

No server behavior changes. The design keeps archive path handling server-only and preserves the existing POST note endpoint plus `copyText` implementation. No new abstraction, package, endpoint, or infrastructure is needed.

## Component Breakdown

**Existing files to modify:**

- `WebApp/WebApp.Client/Components/EpubReader.razor` — register and receive selection callbacks; hold captured selection/menu state; render the contextual actions; clear lifecycle state; use stored text for the existing save/copy methods.
- `WebApp/WebApp.Client/Components/EpubReader.razor.css` — add narrowly scoped positioning, safe-area, stacking, and responsive rules for the nonstandard contextual menu while inheriting reader tokens.
- `WebApp/WebApp.Client/wwwroot/js/ebookReader.js` — register/unregister selection observation, verify the selection is owned by the reader, extract text and range geometry, and preserve native touch selection/tap navigation behavior.

**New files to create:**

- None required.

## Dependencies

- Existing Blazor WebAssembly `IJSRuntime`, `IJSObjectReference`, `ElementReference`, and `DotNetObjectReference` usage in `EpubReader.razor`.
- Existing `POST /api/archive/{category}/items/{id}/book/notes` note-save endpoint and `ebookReader.js:copyText` Clipboard API bridge.
- Existing Bootstrap 5.3.8, Bootstrap Icons 1.13.1, reader theme variables, and Docker/Makefile validation workflow.

## External Vendor Documentation Evidence

The Microsoft Learn MCP server identified in `AGENTS.md` is not available in this session, so official verification is pending before implementation. The original EPUB-reader spec cites the applicable official Blazor JS interop guidance: https://learn.microsoft.com/aspnet/core/blazor/javascript-interoperability/call-javascript-from-dotnet?view=aspnetcore-10.0 and module-based no-inline-handler guidance: https://learn.microsoft.com/aspnet/core/blazor/javascript-interoperability/?view=aspnetcore-10.0#avoid-inline-event-handlers. Implementation should reverify those sources, and Android Chrome selection-event behavior, with official documentation before code is written.

## Flow

```mermaid
sequenceDiagram
    actor User
    participant Chrome as Android Chrome
    participant JS as ebookReader.js
    participant Reader as EpubReader.razor
    participant API as ArchiveEndpoints

    User->>Chrome: Long-press and drag EPUB text handles
    Chrome->>JS: selection/pointer change
    JS->>JS: Verify range belongs to reader; get text and bounds
    JS->>Reader: Selection callback(text, geometry)
    Reader->>Reader: Store actionable selection; render contextual menu
    User->>Reader: Tap Save Note or Copy Selection
    alt Save Note
        Reader->>API: POST existing note request with stored text
        API-->>Reader: Success/failure
        Reader->>JS: Clear native selection on success
    else Copy Selection
        Reader->>JS: copyText(stored text)
    end
```

## Risk Assessment

| Risk | Evidence | Mitigation |
| --- | --- | --- |
| A global selection event can capture text outside the reader. | `getSelectionText` already checks `container.contains(range.commonAncestorContainer)`. | Reuse and centralize that containment check before any .NET callback; clear state for external/collapsed ranges. |
| Tapping a floating action can collapse the native range before Blazor handles the click. | The reported touch issue already shows browser input timing differs from mouse input. | Capture trimmed text in component state when selection changes and make save/copy consume that state. |
| The menu can obscure the selection or be clipped under reader toolbars. | The reader is fixed, has toolbars, a TOC overlay, and an absolute progress footer. | Calculate above/below placement from viewport bounds, clamp inline position, use an overlay z-index above content, and manually verify all reader layouts. |
| New pointer listeners can accidentally turn selection gestures into page turns. | `registerTapNavigation` currently owns pointer events and detects drags/taps. | Do not prevent default; preserve drag thresholds and keep the existing noncollapsed-selection page-turn guard; test taps and long-press drags separately. |
| Lifecycle cleanup can leave duplicate selection callbacks after reopening a book. | The component already imports a module and holds .NET references. | Pair every registration with explicit unregistration in `DisposeAsync` and reset the captured state on chapter/book transitions. |
