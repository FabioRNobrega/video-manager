# Validation: EPUB Touch Selection Actions

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | On an Android Chrome tablet, long-pressing and extending a passage inside an open EPUB enables the contextual actions without requiring a mouse-up or keyboard event. |
| FR2 | The captured action text is trimmed, belongs only to the active reader chapter, and remains available after the user reaches the contextual menu. |
| FR3 | Save Note and Copy Selection are absent from the permanent toolbar and appear only in a contextual menu for a valid selection. |
| FR4 | The contextual menu saves through the current note endpoint and copies with the current clipboard bridge, retaining loading/status/error feedback. |
| FR5 | The menu appears above the selection when it fits, otherwise below it, and remains fully visible/tappable near every reader edge. |
| FR6 | Saving/copying acts on the captured passage even if the native selection highlight changes before the action tap. |
| FR7 | Clearing/replacing selection, changing chapter, closing/resetting reader, and successful save remove actionable menu state; successful save also clears the native range. |
| FR8 | The menu meets reader-theme, Bootstrap, icon, focus, live-status, target-size, responsive, and page-navigation requirements without a regression. |

## Test Cases

**Unit tests:**

- ⚠️ TODO: Extract pure menu-placement calculation into a browser-safe client model only if it cannot be kept trivial in the component; add `WebApp.Tests/Client/EpubSelectionMenuStateTests.cs` to cover above/below choice, horizontal clamping, state reset, and stored-text action eligibility.
- ⚠️ TODO: Add a focused testable JS helper or browser test harness for selection containment/geometry normalization if the selected implementation makes it separable; test collapsed, external, and reader-owned ranges.
- Existing `WebApp.Tests/Services/EpubNoteServiceTests.cs` and `WebApp.Tests/Endpoints/ArchiveEndpointsTests.cs` continue to cover persistence and endpoint validation because their server contracts do not change.

**Integration tests:**

- Run `make test` to verify all existing Docker Compose xUnit tests pass; no endpoint or package change is expected.
- ⚠️ TODO: If the repository gains browser-component testing support, add an end-to-end Android-Chrome-capable scenario that long-presses/extends EPUB text and verifies the contextual actions. Do not introduce a browser-test framework solely for this small client interaction without approval.

## Manual Verification

1. Place a non-DRM EPUB in the configured `Books` archive and start the app with `make docker-run-bg`.
2. In Android Chrome on a tablet, open Books and open the EPUB reader.
3. Long-press and drag native selection handles across a passage. Confirm a floating Save Note / Copy Selection menu appears, both actions are enabled, and native handles still work.
4. Repeat near the top, bottom, left, and right of visible reader content; confirm the menu flips above/below as needed and is never clipped by toolbars, TOC, progress footer, or viewport edges.
5. Tap Save Note after selecting text; confirm one note is saved, the success status is announced, the floating menu disappears, and the native selection clears.
6. Select another passage, intentionally let the visible highlight change while moving to the menu, then use Copy Selection and paste elsewhere; confirm the originally captured passage is copied.
7. Clear the selection, select text outside the reader, and change chapters; confirm no stale menu or stale action text remains.
8. Verify a short tap in the left/right reading zones still changes page, while a long-press drag used for selection never changes page.
9. Repeat basic selection/save/copy checks in reader light and dark modes and at tablet/desktop widths. Confirm visible keyboard focus and usable touch targets.
10. Run `make test`, then stop the stack with `make docker-down`.

## Definition of Done

- Requirements, Plan, and Validation documents exist in this spec folder.
- Android Chrome tablet long-press selection exposes usable contextual Save Note and Copy Selection actions.
- Existing note endpoint, persistence format, opaque-ID boundary, and clipboard bridge remain unchanged.
- Selection listener lifecycle is cleaned up and page tap navigation remains correct.
- The contextual UI follows the governing design-system spec with responsive and accessible behavior.
- Existing tests pass through `make test`; any extracted deterministic placement/state logic has xUnit coverage.
- Official vendor documentation evidence is reverified or remains explicitly marked pending if the Microsoft Learn MCP server is unavailable.

## Rollback Plan

- Revert the selection-registration and contextual-menu changes in `WebApp/WebApp.Client/Components/EpubReader.razor`.
- Revert only the associated selection listener/geometry functions in `WebApp/WebApp.Client/wwwroot/js/ebookReader.js` and the contextual-menu styles in `EpubReader.razor.css`.
- This restores the existing permanent toolbar actions and mouse/keyboard-only selection refresh without changing notes already written to the archive.
