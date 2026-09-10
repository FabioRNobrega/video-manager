# Validation: EPUB Reader Toolbar Consolidation

## Table of Contents

- [Validation: EPUB Reader Toolbar Consolidation](#validation-epub-reader-toolbar-consolidation)
  - [Acceptance Criteria](#acceptance-criteria)
  - [Test Cases](#test-cases)
  - [Manual Verification](#manual-verification)
  - [Definition of Done](#definition-of-done)
  - [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | Opening a book renders exactly one `<header class="epub-reader-toolbar">` element in the DOM (not two), containing all controls previously split across both rows. |
| FR2 | The former close (X) button is now rendered with `bi-arrow-left`; clicking it still saves progress, unregisters JS interop, and invokes `OnClose` (same observable effect as before: the reader closes). |
| FR3 | Clicking the "Aa" button opens a panel containing, in order, text-size controls, the font-family select, the line-height select, text-width controls, and the reading-mode toggle; each control's action (e.g. clicking "increase font size") visibly changes the chapter content exactly as it did before the refactor. |
| FR4 | The Aa panel is closed on initial render; toggling "Aa" twice returns it to closed; clicking outside the open panel closes it; none of `_fontSizeIndex`/`_fontFamily`/`_lineHeightIndex`/`_contentPaddingPercent`/`_isDarkReaderMode` reset when the panel opens or closes. |
| FR5 | A disabled Search icon button (`bi-search`) is visible in the toolbar and cannot be activated (no click effect, `disabled` attribute present). |
| FR6 | A disabled Notes/highlights icon button (`bi-bookmark-star`) is visible in the toolbar and cannot be activated (no click effect, `disabled` attribute present). |
| FR7 | Clicking the hide-toolbar button (now `bi-chevron-bar-up`) sets `_isToolbarCollapsed = true`, hiding the toolbar, exactly as `HideToolbar()` did before. |
| FR8 | When collapsed, a centered pill control (not a top-right square button) is visible; clicking it calls `ShowToolbar()` and restores the toolbar. |
| FR9 | TOC toggle, chapter prev/next buttons, and the book title remain present with unchanged `disabled`/`aria-*` behavior and still functionally toggle the TOC / navigate chapters. |
| FR10 | Triggering a status message (e.g. saving a note) still renders visible, `aria-live="polite"` text in or directly below the consolidated toolbar. |
| FR11 | Every icon-only control in the toolbar uses a Bootstrap Icon class (`bi-*`), has an `aria-label`, and renders at a minimum 40×40 CSS-pixel hit target (verified via browser dev tools box model). |
| FR12 | The reading-progress footer (`Book Progress X%` / `Chapter Progress X%`) renders identically to pre-refactor behavior. |

## Test Cases

**Unit tests:**

- ⚠️ TODO: No Razor component test project exists in this repo today (`WebApp.Tests` only covers `Services/Epub*ServiceTests.cs` — backend services, not components). This refactor changes no C# logic in `EpubReader.razor.cs`/`@code`, so no new unit test is required for this spec; if the project later adopts bUnit or similar for Blazor component testing, add rendering tests for the consolidated toolbar and Aa panel then.

**Integration tests:**

- Existing `WebApp.Tests/Services/EpubBookServiceTests.cs`, `EpubProgressServiceTests.cs`, `EpubHighlightServiceTests.cs`, and `EpubNoteServiceTests.cs` must continue to pass unchanged via `make test` — they exercise the backend services this component calls (`/book`, `/book/progress`, `/book/highlights`, `/book/notes`), which are untouched by this markup-only refactor.

## Manual Verification

Starting from a clean state using this repo's documented Docker workflow (`AGENTS.md` → Available Commands):

1. Run `make docker-run` (or `make docker-run-bg`) and open the app in a browser at the LAN/localhost URL from `make get-url`.
2. Navigate to the Archive Browser, open any EPUB item, and confirm the reader shows a single toolbar row (no second row underneath it).
3. Confirm the back button (left-arrow icon) closes the reader and returns to the previous view, the same as the old close button did.
4. Click the TOC button and confirm the table of contents still opens/closes and chapter links still navigate.
5. Use the chapter prev/next buttons and confirm chapter navigation still works and disables at the first/last chapter.
6. Click the "Aa" button and confirm the panel opens showing text size, font family, line height, text width, and reading-mode controls; exercise each one and confirm the chapter content visibly updates (font size, font family, line spacing, margin width, light/dark reader mode) exactly as before the refactor.
7. Click outside the open Aa panel and confirm it closes; reopen it and confirm the previously-set values are still reflected (state was not reset).
8. Confirm the Search and Notes/highlights buttons are visibly present but disabled (cannot be clicked, hover/focus shows a "coming soon" title/tooltip).
9. Click "Hide toolbar" and confirm the toolbar disappears and a small centered pill appears at the top edge (not a top-right square button); click the pill and confirm the toolbar reappears.
10. Select some chapter text, save a note, and confirm the status message ("Note saved.") still appears with the same visibility/timing as before.
11. Resize the browser (or use device emulation) below the existing `47.98rem` breakpoint and confirm the consolidated toolbar wraps usably, the title still truncates instead of overflowing, and the Aa panel does not clip off-screen.
12. Confirm the book progress / chapter progress footer at the bottom of the page still renders and updates while turning pages, unchanged from before this refactor.

## Definition of Done

- Requirements, Plan, and Validation docs are updated in this spec folder.
- All existing tests still pass (`make test`).
- `EpubReader.razor` and `EpubReader.razor.css` are the only files changed; no `@code` logic, JS interop, or HTTP call is modified.
- All 12 manual verification steps above pass in a Docker-run instance of the app, in both reader-light and reader-dark modes.
- Icon-only controls (back, Aa, search, notes, hide/reveal) all use Bootstrap Icons with accessible names, matching the repo's design-system contract in `AGENTS.md`.
- No new color tokens, custom SVGs, or external fonts are introduced.

## Rollback Plan

This is a self-contained markup/CSS change to two files (`WebApp/WebApp.Client/Components/EpubReader.razor` and `WebApp/WebApp.Client/Components/EpubReader.razor.css`) with no data migration, feature flag, or server-side change involved. To roll back, revert the commit(s) touching these two files (`git revert <sha>` or restore the pre-refactor version of both files) — no configuration, environment variable, or route change needs to be undone since none was introduced.
