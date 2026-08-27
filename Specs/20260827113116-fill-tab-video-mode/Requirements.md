# Requirements: Fill-Tab Video Mode

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

`WebApp/WebApp.Client/Components/VerticalVideoEditor.razor` currently relies on the native HTML `<video controls>` fullscreen command. In Vivaldi, that command expands the video across the user's physical screen, but the desired workflow is an application-owned viewing mode that fills only the current browser tab or split-pane viewport. The editor needs a separate Fill tab command that covers the visible page area, crops any source aspect ratio to that area, responds to pane resizing, and exits with Escape without using the browser Fullscreen API.

## User Stories

- Given a selected video in the normal editor, when the user activates Fill tab, then the existing video expands over the complete content viewport of the current browser tab without entering operating-system fullscreen.
- Given a source whose aspect ratio differs from the browser pane, when Fill tab is active, then the video preserves its intrinsic proportions, fills the pane, and crops the overflowing content according to the current crop position.
- Given Fill tab is active, when the user presses Escape, then the normal workspace returns with the same selected video, playback state, media-control state, and crop position.
- Given Fill tab is active in a Vivaldi split-screen pane, when the pane is resized, then the video continuously fits the new pane dimensions without exiting and re-entering the mode.

## Functional Requirements

1. FR1 - `VerticalVideoEditor.razor` must group the crop position and drag instruction into one bottom helper block and expose an icon-only external Fill tab button vertically centered beside that block when a video is selected and the editor is in its normal layout; the command must be unavailable when no video is selected.
2. FR2 - Activating Fill tab must make the editor's video viewport cover the complete current browser content viewport without calling `requestFullscreen`, entering browser fullscreen, or covering browser chrome or neighboring Vivaldi split-screen panes.
3. FR3 - In Fill tab mode, the viewport and `<video>` element must occupy the full available width and height, and `object-fit: cover` must preserve the source aspect ratio while clipping whichever axis overflows.
4. FR4 - Fill tab mode must continue applying the current `VideoFrameState` `object-position`, and pointer dragging must remain available so the user can reposition cropped content against the current full-pane geometry.
5. FR5 - Entering and exiting Fill tab must keep the same mounted `<video>` element and preserve the selected video, current playback time, playing/paused state, volume/mute state, native controls, and crop coordinates.
6. FR6 - While Fill tab is active, the external entry button and normal editor chrome must not be presented as exit controls; pressing the `Escape` key must exit the mode and restore the normal workspace.
7. FR7 - Fill tab sizing must react automatically to browser-window and Vivaldi split-pane resizing without a page reload, navigation, explicit refresh command, or mode restart.
8. FR8 - Fill tab mode must prevent the covered page from scrolling or receiving pointer interaction, then restore normal page scrolling and interaction on every exit path.
9. FR9 - The editor must remove its Escape listener and any page-level mode styling when Fill tab exits, the selected video changes, or `VerticalVideoEditor` is disposed, so stale handlers and a trapped overlay cannot remain.
10. FR10 - The Fill tab button must be keyboard operable, have an accessible name that distinguishes it from native fullscreen, expose a tooltip on hover and keyboard focus that communicates that Escape exits the mode, and regain focus after Escape returns to the normal workspace.
11. FR11 - The browser's existing native video controls and fullscreen behavior must remain unchanged; Fill tab is a separate application control and must not attempt to intercept or redefine browser-owned controls.
12. FR12 - Entering, resizing, and exiting Fill tab must be client-only operations with no new HTTP request, server state, persistence, or external dependency.
13. FR13 - Fill tab must work with the application's existing dark and light themes, preserve playback-error handling, and avoid exposing clipped normal-workspace content above the overlay.

## Non-Functional Requirements

- **Responsiveness:** The overlay must follow the current tab's viewport dimensions, including desktop window changes and Vivaldi split-pane changes, without hard-coded screen dimensions.
- **Accessibility:** The entry action needs visible keyboard focus and explicit Escape instructions before activation. Escape must provide a reliable way out, and focus must return to the entry command afterward.
- **Maintainability:** Blazor owns mode state and rendering; the existing isolated JavaScript module is limited to browser-global keyboard/listener and document-scroll behavior that Razor cannot own reliably. Listener setup and teardown must be symmetric.
- **Performance:** CSS performs viewport fitting, and resizing must not trigger media reloads, server calls, or high-frequency .NET/JavaScript resize callbacks.
- **Compatibility:** The implementation must retain the existing .NET 10 Interactive WebAssembly, CSS isolation, native HTML video controls, theme tokens, and Docker Compose/xUnit workflows.
- **Dependency control:** No frontend framework, media library, browser extension, or new runtime package may be added.

## Out of Scope

- Changing, hiding, emulating, or intercepting the native HTML video fullscreen control.
- Using the browser Fullscreen API or expanding across Vivaldi's window chrome, other split panes, or the physical display.
- Adding an on-screen exit button while Fill tab is active; Escape is the specified exit interaction.
- Persisting Fill tab state across selection changes, scans, reloads, navigation, or application restarts.
- Adding browser-automation infrastructure solely for this feature.
- Changing streaming, scanning, video codec support, transcoding, or crop persistence.

## Open Questions

- None. Discovery established the external control, cover-style cropping, Escape-only exit, responsive split-pane behavior, and manual QA emphasis.
