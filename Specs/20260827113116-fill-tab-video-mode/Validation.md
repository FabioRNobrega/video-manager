# Validation: Fill-Tab Video Mode

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | With no selection the external Fill tab command is unavailable; with a selected video the crop position and drag instruction form one centered bottom copy block, with the operable icon-only control vertically centered beside it. |
| FR2 | Activating Fill tab covers exactly the current browser content pane while Vivaldi chrome and neighboring split panes remain visible; `document.fullscreenElement` remains `null`. |
| FR3 | Portrait, landscape, and square source videos fill both viewport axes without distortion or empty bars, with mismatched content cropped. |
| FR4 | The crop position is unchanged on entry, and dragging in Fill tab updates the overflowing axis using the active pane dimensions. |
| FR5 | Entering and exiting does not replace/reload the media: selection, current time, play/pause, volume/mute, controls, and crop coordinates are unchanged. |
| FR6 | Normal editor chrome and the external entry button are absent from the active view, and one Escape press returns to the normal workspace when browser-native fullscreen is not active. |
| FR7 | Resizing the browser or Vivaldi split pane refits the video immediately without reload, navigation, or leaving the mode. |
| FR8 | The covered workspace cannot scroll or receive pointer input while active, and normal page interaction and scrolling return after exit. |
| FR9 | Escape, selection change, and component disposal each clear mode styling and listeners; repeated entry/exit cycles never produce duplicate callbacks or a trapped overlay. |
| FR10 | Keyboard activation works, accessible inspection distinguishes Fill tab from fullscreen, its help tooltip appears on hover and focus with Escape guidance, and focus returns to Fill tab after Escape. |
| FR11 | The native video controls remain usable and their browser-owned fullscreen behavior is unchanged and independent of Fill tab. |
| FR12 | DevTools Network shows no request caused by entering, resizing, dragging solely because of resize, or exiting the mode; no state survives reload. |
| FR13 | The active video covers normal workspace content in both themes, and existing playback errors remain recoverable after exiting the mode. |

## Test Cases

**Unit tests:**

- `WebApp.Tests/Client/FillTabStateTests.cs` using xUnit: a selected video can enter Fill tab, exit returns to normal, repeated exit is harmless, and changing/clearing selection forces normal mode.
- `WebApp.Tests/Client/VideoFrameStateTests.cs`: retain the existing cover-geometry, axis-locking, clamping, reset, and selection-change tests because the full-pane layout reuses this framing model.
- Run all tests with `make test`; no browser automation or additional test package is introduced.

**Integration/manual boundary:**

- The existing xUnit/WebApplicationFactory tests continue to validate app startup, streaming, and static assets, but they cannot establish rendered viewport containment or native media state.
- Vivaldi manual QA is authoritative for split-pane boundaries, responsive fitting, browser-control interaction, media-state continuity, Escape handling, focus, and visual behavior.

## Manual Verification

1. From the repository root, configure the existing private `.env`, run `make docker-run`, and open the loopback application URL in Vivaldi.
2. Before selecting a video, confirm Fill tab is unavailable. Scan, select a playable video, and confirm an icon-only, keyboard-focusable Fill tab button appears beside the bottom crop-helper text outside the native video controls. Hover and focus it; confirm the tooltip says “Fill this browser tab. Press Escape to exit.” and accessibility inspection reports “Fill browser tab.”
3. Tile the Video Manager tab beside at least one other Vivaldi tab, matching the split-pane scenario supplied during discovery. Start playback, seek away from the beginning, set a recognizable mute/volume state, and drag the crop away from center.
4. Activate Fill tab. Confirm the video covers the entire Video Manager content pane, but not Vivaldi's tab/address chrome, the neighboring pane, or the physical display. In DevTools, confirm `document.fullscreenElement` is `null`.
5. Confirm the video has no empty bars, is not stretched, and crops overflow. Repeat with portrait, landscape, and square videos when fixtures are available.
6. While active, drag the video and confirm crop repositioning remains bounded and follows the dimensions of the full pane. Confirm the normal header, library, editor header, readout, hint, and entry button do not overlay the video.
7. Resize the Vivaldi tile repeatedly, including narrow and wide pane shapes. Confirm the video continuously covers the pane without reload, mode restart, neighbor overlap, visible gaps, or stale crop geometry on the next drag.
8. Attempt to scroll and click the covered workspace while active. Confirm the background does not move or receive input.
9. Press Escape once. Confirm the normal workspace returns, Fill tab regains keyboard focus, scrolling works, and the same video retains its time, play/pause state, mute/volume state, native controls, and crop position.
10. Repeat entry and Escape exit at least five times. Press Escape once more in normal mode and confirm it causes no application change, proving no duplicate/stale mode handler remains.
11. Enter Fill tab, then exercise a selection-change or component-disposal path available during development. Confirm the next normal render has no overlay and page scrolling remains enabled.
12. Verify the behavior in both application themes and with a playback-error selection. Confirm Fill tab does not permanently hide the existing error/recovery path.
13. Confirm native play, pause, seek, mute, volume, overflow-menu, and fullscreen controls retain their normal browser behavior. If native fullscreen is entered from Fill tab, exit native fullscreen and then press Escape again to leave Fill tab.
14. Keep DevTools Network open through entry, Vivaldi pane resizing, and exit. Confirm the mode itself issues no HTTP request and does not reload the stream.
15. Repeat the core entry, resizing, and Escape checks in one additional Chromium-based browser when available, treating Vivaldi as the release-authoritative result.
16. Run `make test` and confirm the new simple state tests and all existing xUnit tests pass in the isolated Docker Compose test stack.

## Definition of Done

- `Requirements.md`, `Plan.md`, and `Validation.md` remain synchronized in this spec folder, and every FR maps to an observable acceptance criterion.
- A selected video exposes an accessible external Fill tab command without changing native browser controls or invoking the Fullscreen API.
- The same media element fills the current tab/pane with cover cropping, responds to live pane resizing, supports crop dragging, and preserves media/framing state.
- Escape is the active-mode exit, focus returns to the trigger, and listener/body-lock cleanup succeeds for Escape, selection change, and disposal.
- Both themes and existing playback-error recovery remain usable, and covered workspace content neither leaks above the overlay nor remains locked after exit.
- Manual Vivaldi split-pane QA passes for portrait, landscape, and square sources; an additional Chromium smoke check is completed when available.
- `FillTabState` has small xUnit coverage, all existing tests pass through `make test`, and no browser-test framework or runtime dependency is added.
- The implementation and validation continue to use the repository's Docker Compose/Makefile workflow and current official documentation evidence remains recorded in `Plan.md`.

## Rollback Plan

- Remove `FillTabState.cs` and `FillTabStateTests.cs`.
- Revert the Fill tab markup/state/lifecycle additions in `VerticalVideoEditor.razor`, its mode rules in `VerticalVideoEditor.razor.css`, and the Escape/body-class functions in `videoEditor.js`.
- Remove the `body.fill-tab-active` rule from `WebApp/WebApp/wwwroot/app.css`.
- The existing selected-video editor, framing state, native controls, streaming endpoints, and stored data require no migration or recovery because the feature adds no persistence or server state.
