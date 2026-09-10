# Requirements: EPUB Reader Toolbar Consolidation

## Table of Contents

- [Requirements: EPUB Reader Toolbar Consolidation](#requirements-epub-reader-toolbar-consolidation)
  - [Problem Statement](#problem-statement)
  - [User Stories](#user-stories)
  - [Functional Requirements](#functional-requirements)
  - [Non-Functional Requirements](#non-functional-requirements)
  - [Out of Scope](#out-of-scope)
  - [Open Questions](#open-questions)

## Problem Statement

`Components/EpubReader.razor` currently renders two stacked toolbar rows (`WebApp/WebApp.Client/Components/EpubReader.razor:20` and `:125`): a primary row with TOC toggle, chapter prev/next, title, font-size buttons, a font-family `<select>`, a line-height `<select>`, text-width buttons, dark/light toggle, and close button; and a secondary row that only holds an optional status message and the "Hide reader toolbar" button. This consumes a large, fixed amount of vertical space above the page content, spreads related text-styling controls across five separate always-visible controls, and has no room for planned Search or Notes/highlights entry points. A user shared a static HTML mockup showing a single consolidated toolbar row with a collapsible "Aa" text-settings panel, which this spec adapts to the project's actual Bootstrap-based design system and existing `EpubReader` Blazor logic.

## User Stories

- Given a book is open, when the reader loads, then the reader shows one single toolbar row (not two) containing back, TOC, chapter navigation, the book title, search, notes, text-settings ("Aa"), and hide-toolbar controls.
- Given the toolbar is visible, when the user selects the "Aa" button, then a dropdown panel opens exposing font size, font family, line height, text width, and reading mode controls, and closes when the user picks a value, clicks elsewhere, or toggles "Aa" again.
- Given the user is reading, when they press "Hide toolbar", then the toolbar and any open "Aa" panel are hidden and a small centered pill control appears at the top edge to bring the toolbar back.
- Given the user wants to leave the reader, when they press the back control, then the same `CloseAsync` flow runs (progress saved, JS interop unregistered, `OnClose` invoked) as today's close button.
- Given Search and Notes/highlights are not yet implemented, when the user looks at the toolbar, then both controls are visibly present but disabled, communicating that the features are coming without implying they work.

## Functional Requirements

1. FR1 — The two existing `<header class="epub-reader-toolbar">` rows in `EpubReader.razor` (lines 20-137) are consolidated into a single toolbar row containing all controls that are currently split across both rows, using a wrapping flex layout consistent with the existing `d-flex flex-wrap align-items-center gap-2` pattern.
2. FR2 — The existing "Close book" button (`EpubReader.razor:116-122`, bound to `CloseAsync`) is replaced by a "Back" icon button using the Bootstrap Icon `bi-arrow-left`, keeping the same `@onclick="CloseAsync"` binding and updating its `aria-label`/`title` to "Back" / "Close book" language appropriate for a back action.
3. FR3 — A new single icon button (Bootstrap Icon `bi-fonts`, `aria-label="Text settings"`) toggles a Bootstrap dropdown-style panel (e.g. `dropdown`/`Collapse` pattern already idiomatic to Bootstrap) that contains, grouped in the same order as the reference mockup: a text-size row (existing decrease/increase buttons bound to `DecreaseFontSize`/`IncreaseFontSize`, showing the current `FontSizesPx[_fontSizeIndex]` value), the existing font-family `<select>` bound to `OnFontFamilyChanged`, the existing line-height `<select>` bound to `OnLineHeightChanged`, the existing text-width decrease/increase buttons bound to `DecreaseTextWidth`/`IncreaseTextWidth`, and the existing dark/light reading-mode toggle bound to `ToggleReaderMode`.
4. FR4 — The text-settings panel from FR3 is closed by default, opens only when the "Aa" button is activated, and closes when the "Aa" button is toggled again or the user clicks/taps outside the panel; opening/closing does not reset any of the underlying `_fontSizeIndex`, `_fontFamily`, `_lineHeightIndex`, `_contentPaddingPercent`, or `_isDarkReaderMode` state.
5. FR5 — A new disabled icon button (Bootstrap Icon `bi-search`, `aria-label="Search in book"`, `title="Search (coming soon)"`) is added to the consolidated toolbar with no click handler, positioned near the other utility controls as in the reference mockup.
6. FR6 — A new disabled icon button (Bootstrap Icon `bi-bookmark-star`, `aria-label="Notes and highlights"`, `title="Notes and highlights (coming soon)"`) is added to the consolidated toolbar with no click handler, adjacent to the Search button.
7. FR7 — The existing "Hide reader toolbar" button (`EpubReader.razor:130-136`, bound to `HideToolbar`) is retained in the single consolidated row, restyled with the Bootstrap Icon that best matches a "collapse upward" affordance (`bi-chevron-bar-up`), and continues to call the existing `HideToolbar()` method unchanged.
8. FR8 — When `_isToolbarCollapsed` is `true`, the existing top-right `epub-reader-toolbar-show-button` (`EpubReader.razor:10-16`) is replaced with a small pill-shaped control horizontally centered at the top edge of the reader (matching the mockup's `rt-reveal` affordance), using the Bootstrap Icon `bi-chevron-bar-down`, and continues to call the existing `ShowToolbar()` method unchanged.
9. FR9 — The existing TOC toggle button (`EpubReader.razor:21-28`, bound to `ToggleToc`), the chapter prev/next button group (`EpubReader.razor:30-47`, bound to `GoToPreviousChapterAsync`/`GoToNextChapterAsync`), and the book title (`EpubReader.razor:49-51`) remain present in the consolidated toolbar with their existing bindings, disabled states, and `aria` attributes unchanged.
10. FR10 — The optional status message (`EpubReader.razor:126-129`, `_statusMessage`) continues to be rendered with `role="status" aria-live="polite"` somewhere in or immediately below the consolidated toolbar so note-save/copy feedback remains visible.
11. FR11 — All icon buttons in the consolidated toolbar (including the new Search, Notes, back, Aa, and hide/reveal controls) use Bootstrap Icons at `currentColor` inside `btn`/`btn-outline-secondary`-style controls with a minimum 40×40 CSS-pixel hit target and visible focus state, consistent with the repo's design-system contract; no hand-authored SVGs are introduced.
12. FR12 — The reading-progress footer (`EpubReader.razor:184-193`, `BookProgress`/`CurrentChapterProgress`) is left unchanged by this refactor.

## Non-Functional Requirements

- Visual styling must extend the existing `.epub-reader`, `.reader-light`, `.reader-dark`, and `.epub-reader-toolbar*` CSS custom properties already defined in `EpubReader.razor.css` rather than introducing a new color palette, per the repo's Bootstrap-first design-system contract (`AGENTS.md` Design System section).
- The consolidated toolbar and its Aa panel must remain usable at the existing responsive breakpoint already defined in `EpubReader.razor.css` (`@media (max-width: 47.98rem)`), including on touch/coarse-pointer devices that already receive special handling elsewhere in this component (e.g. `epub-reader-selection-menu` coarse-pointer rules).
- All existing `@onclick`/`@onchange` bindings, `disabled` conditions, and `aria-*` attributes referenced by FR2-FR10 must be preserved verbatim in behavior; this is a markup/CSS restructuring, not a change to `EpubReader.razor`'s C# code-behind logic, JS interop calls, or HTTP calls.
- Icon-only controls must keep an accessible name (`aria-label`) and a Bootstrap tooltip or `title` fallback, and toggle controls (Aa button, TOC button) must expose `aria-expanded`/`aria-pressed` state as applicable, consistent with existing controls in this component.

## Out of Scope

- Implementing Search or Notes/highlights functionality (both remain disabled placeholders).
- Adding a thin progress-bar strip under the toolbar (the existing text-based progress footer is unchanged).
- Any change to chapter loading, pagination, highlight persistence, or selection-menu behavior.
- Introducing a new color palette, custom fonts, or hand-authored SVG icon set from the reference mockup.
- Changing the reader's dark/light theme tokens or the app-wide theme system.

## Open Questions

None — visual fidelity, progress-bar scope, reveal-control style, and placeholder-button behavior were resolved during discovery (see Plan.md Summary for the chosen options).
