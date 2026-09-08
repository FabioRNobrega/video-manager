# Requirements: Persistent Footer Player

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

The current video player is owned by `WebApp/WebApp.Client/Pages/Home.razor` through `VerticalVideoEditor.razor`, so playback is tied to the home page layout instead of being available while the user browses other archive folders or category pages. The app needs a reusable, Plex-style persistent footer player that appears only after a video is selected from any archive surface, keeps the existing playback and Fill-tab behavior, and lets the user expand the mini player into the full-tab experience from a footer control.

## User Stories

- Given no media has been selected, when the user opens any archive page, then no footer player is shown and the page uses the normal available viewport.
- Given a video file is selected from Videos, Music, or another folder-backed archive surface, when the file is playable by the existing video endpoints, then the persistent footer player appears and loads that selected video.
- Given a video is playing in the footer player, when the user browses to another folder or internal app page, then playback state and controls remain available from the footer shell.
- Given the footer player is visible, when the user activates the `bi-arrow-bar-up` control, then the same selected video expands into the existing Fill-tab player experience.
- Given the player is in Fill-tab mode, when the user exits fullscreen/fill mode, then the player returns to the footer mini-player without losing the selected video.

## Functional Requirements

1. FR1 - `MainLayout.razor` MUST host a persistent player surface outside routed page content so the selected video can remain available across internal navigation.
2. FR2 - The footer player MUST remain hidden until a video is selected.
3. FR3 - Archive item activation from `ArchiveBrowser.razor` MUST be able to send playable video selections to the persistent player from any category or folder surface that uses archive file DTOs.
4. FR4 - `Home.razor` MUST stop owning the selected video player directly and instead publish video selections to shared client-side player state.
5. FR5 - The shared player state MUST preserve the selected `VideoItemDto`, stream base path, playback state, volume, mute, playback rate, loop state, A/B markers, crop state, and saturation state while routed page content changes.
6. FR6 - The mini footer player MUST use the same media control layout and semantics as the existing `MediaPlayerControls.razor`, adapted horizontally with video preview and metadata on the side in a Plex-like footer composition.
7. FR7 - The mini footer player MUST include an accessible icon button using `<i class="bi bi-arrow-bar-up"></i>` to enter Fill-tab mode.
8. FR8 - Crop dragging and saturation controls MUST be available only in Fill-tab/full player mode, not in the footer mini-player.
9. FR9 - A/B loop controls and Save Cut MUST remain available from the footer controls when the current selected item supports cut export.
10. FR10 - Existing Fill-tab behavior from `FillTabState`, `videoEditor.js`, and `VerticalVideoEditor.razor` MUST be preserved, including Escape-to-exit and cleanup when the selected video changes.
11. FR11 - Switching to another video MUST reset selection-scoped playback, crop, saturation, Fill-tab, and A/B state using the existing state-model semantics.
12. FR12 - The browser MUST continue to receive only opaque IDs and stream URLs based on existing server endpoints; no physical or root-relative paths may be exposed by the reusable player.
13. FR13 - The server MUST provide a narrow read-only archive video stream endpoint for supported video files outside the Videos category, resolving only category-scoped opaque archive IDs.

## Non-Functional Requirements

- The feature MUST stay in `WebApp.Client` for UI and browser-side state; server endpoint, filesystem, and FFmpeg boundaries remain unchanged.
- The implementation MUST follow the existing Bootstrap 5.3.8, Bootstrap Icons, global `app.css`, and focused component-scoped CSS design contract.
- The shared player state MUST be granular enough to avoid broad app-wide rerenders when unrelated archive UI changes.
- The player MUST be keyboard accessible, screen-reader named, and usable without Bootstrap tooltip JavaScript.
- The footer MUST not obscure content permanently; routed content should have bottom spacing only while the footer player is visible.
- The implementation MUST be testable with xUnit state-model tests and existing Docker Compose test workflows.

## Out of Scope

- Adding generic audio/photo/document playback endpoints.
- Expanding archive browsing behavior beyond the surfaces already represented by `ArchiveBrowser.razor`.
- Adding authentication, remote access, or network hosting.
- Adding new FFmpeg processing or transcoding behavior.
- Replacing Bootstrap, Bootstrap Icons, or the existing player JavaScript bridge.

## Open Questions

- None.
