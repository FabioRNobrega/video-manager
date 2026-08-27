# Requirements: Custom Media Player Controls

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

`WebApp/WebApp.Client/Components/VerticalVideoEditor.razor` already renders the selected library video in a draggable 9:16 crop and currently delegates playback interaction to the browser through the native `controls` attribute. The application needs its own touch-friendly media controls, including full-video and A/B looping, while retaining the same keyed HTML5 video element, selection flow, crop state, Fill-tab presentation, local-only streaming boundary, and C#-first architecture.

## User Stories

- Given a selected playable video, when the user clicks Play, Pause, the timeline, volume, mute, or playback-rate controls, then the existing HTML5 video responds and the Blazor UI reflects its actual media state.
- Given a selected video, when the user enables standard looping, then the complete video repeats and the control visibly reports that standard loop is active.
- Given a selected video with valid A and B markers, when playback reaches B while A/B loop is active, then playback returns to A and continues.
- Given hidden controls over a selected video, when the user clicks or taps without performing a crop drag, then the controls appear and hide again after one second without interaction.
- Given the normal 9:16 editor or Fill-tab presentation, when the user operates the custom controls or drags the video, then control interaction and crop interaction remain independent.

## Functional Requirements

1. FR1 — `VerticalVideoEditor.razor` must continue to use its existing keyed HTML5 `<video>` element and `/api/videos/{id}/stream` source; it must remove the native `controls` attribute rather than create or embed another player.
2. FR2 — A dedicated Blazor media-control layer must render as an overlay inside the existing `.video-viewport` in both normal 9:16 and Fill-tab modes.
3. FR3 — C# must own the selected player's playing/paused state, current time, duration, volume, muted state, playback rate, standard-loop state, A/B-loop state, and optional A and B marker positions.
4. FR4 — Selecting a different video or clearing the selection must stop/reset selection-specific playback state to position zero, unknown duration, paused, both loop modes off, and no A/B markers; transient volume, mute, and playback-rate preferences may remain only for the lifetime of the mounted editor component and must reset on page reload.
5. FR5 — Initial playback preferences must match the current editor behavior: muted, full volume, 1x playback rate, and looping disabled.
6. FR6 — A Play/Pause button must invoke the existing media element and then synchronize its label, icon, pressed state, and C# state from the element's actual play and pause events.
7. FR7 — If a play or media command is rejected by the browser, the editor must remain usable and show a non-blocking, browser-safe error message without exposing a physical video path.
8. FR8 — The custom timeline must display current playback time and finite duration in a stable human-readable format and use tabular numerals to avoid layout movement.
9. FR9 — The custom timeline must allow mouse and touch seeking within the inclusive range from zero to the known duration and must update the C# position from the resulting media event.
10. FR10 — Playback progress must update from native media timing events while the video runs, without animation-frame polling.
11. FR11 — A volume control must set media volume from 0 through 1 and synchronize the C# volume value from the actual element state.
12. FR12 — A mute/unmute control must set and reflect the element's actual muted state independently of the stored volume level.
13. FR13 — A data-driven playback-rate selector must initially offer 0.25x, 0.5x, 1x, 1.5x, and 2x and allow later rates to be added without changing command logic.
14. FR14 — Changing playback rate must update both the underlying media element and the C# playback-rate state.
15. FR15 — A standard-loop control must enable or disable full-video repetition by synchronizing C# state with the HTML media element's native loop property.
16. FR16 — The standard-loop control must expose its active/inactive state visually and through accessible control state.
17. FR17 — Set A and Set B commands must capture the current C# playback position and allow their corresponding marker to be replaced.
18. FR18 — The state model must maintain the invariant `0 <= A < B <= duration` whenever both A and B exist: setting A at or after an existing B clears B, and setting B at or before A is rejected while retaining the prior valid B and presenting an accessible validation message.
19. FR19 — A/B loop activation must be unavailable until both valid markers exist.
20. FR20 — When active A/B playback reaches or passes B, the C# controller must seek the existing media element to A and allow playback to continue.
21. FR21 — The user must be able to disable A/B looping without deleting valid markers and must be able to clear both markers and disable A/B looping in one action.
22. FR22 — Standard loop and A/B loop must be mutually exclusive: enabling either mode disables the other, and enabling A/B loop must also clear the media element's native loop property.
23. FR23 — The timeline must visually identify valid A and B positions and the controls must report their formatted values when present.
24. FR24 — The custom controls must be shown initially for a selected video, hide after one second without relevant interaction, and reappear after a click or tap on the video that was not classified as a crop drag.
25. FR25 — Interaction with any visible media control must keep or reveal the layer and restart its one-second inactivity interval; the layer must not hide during an active slider/pointer interaction.
26. FR26 — Hidden controls must not receive pointer input, and events originating in buttons, sliders, or selectors must not begin or move the existing video crop drag.
27. FR27 — Existing mouse, touch, and pen crop repositioning, Reset crop, video selection, playback-error recovery, and object-position behavior must continue to work.
28. FR28 — Fill-tab mode must keep the custom media controls operable over the full-pane video while continuing to hide the existing editor header/helper chrome and preserving Escape exit behavior.
29. FR29 — Player-specific styles must use Blazor CSS isolation, retain the application's Bootstrap/theme-token visual approach, provide sufficient contrast over varied video content, and include clear active, focus, disabled, and error states.
30. FR30 — The media browser adapter must remain isolated in the client, expose only focused operations for reading or changing the existing media element, and contain no loop rules, marker validation, visibility policy, selection logic, or other application state.
31. FR31 — Media synchronization must use native media events and bounded updates; it must not poll on every animation frame or perform a browser interop call solely because Blazor rerendered.
32. FR32 — No media-control state may be persisted to the server, browser storage, URL, video library snapshot, or DTOs in this feature.

## Non-Functional Requirements

- The feature must remain compatible with the existing .NET 10 Interactive WebAssembly architecture and add no frontend framework, player library, media-processing dependency, or server endpoint.
- `MediaPlayerState` must keep playback rules and invariants independently understandable and testable; `VerticalVideoEditor` must coordinate the element and existing framing lifecycle; the control component must own presentation and emit focused commands.
- JavaScript interop must remain a thin DOM/media bridge. It may read or set browser media properties and call media methods but must not decide loop precedence, validate markers, schedule control visibility, or retain canonical player state.
- Timeline synchronization must use the browser's media-event cadence. Slider previews should update C# locally, with seeking committed at an appropriate bounded interaction point rather than on every animation frame.
- The overlay must remain usable with mouse and touch in Vivaldi at the existing responsive breakpoints and in both application themes.
- Operable controls must use semantic buttons, labels, native range/select elements, accessible names, and programmatic pressed/disabled state. This slice does not introduce application-level keyboard shortcuts.
- The existing privacy boundary remains unchanged: browser-facing state contains only media values and the current opaque video ID, never a physical or root-relative filesystem path.

## Out of Scope

- Video discovery, scanning, grouping, selection architecture, streaming endpoints, grouping/layout redesign, transcoding, thumbnails, or codec changes.
- Persisting playback position, volume, mute, rate, loop settings, or markers across page reloads.
- Frame stepping, keyboard playback shortcuts, jump forward/backward, multiple loop regions, saved in/out points, fullscreen changes, picture-in-picture, subtitles, audio-track selection, or preference storage.
- Replacing the HTML5 media engine, changing the current 9:16 cover crop, or changing the existing Fill-tab mode into browser fullscreen.
- Supporting or release-validating browsers other than Vivaldi for this feature.
- Adding Playwright, bUnit, Selenium, or other new automated UI/media testing infrastructure at this stage.

## Open Questions

- None. Discovery decisions are incorporated above; automated browser/player test coverage is explicitly deferred for this stage.
