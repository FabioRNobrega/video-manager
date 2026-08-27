# Validation: Custom Media Player Controls

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | DevTools shows one selected `<video>` using the existing opaque-ID stream URL, with no `controls` attribute and no secondary player element or stream request introduced by the custom UI. |
| FR2 | The custom layer is inside the existing video viewport and overlays the same video in normal 9:16 and Fill-tab modes. |
| FR3 | Code inspection finds all listed canonical player fields in C# and no canonical state object in JavaScript. |
| FR4 | Changing or clearing selection returns the new selection to time zero, paused, no loop, and no markers; volume/mute/rate are at most session-local and a reload restores defaults. |
| FR5 | A fresh page/video starts muted at volume 1, rate 1x, with standard and A/B loop off. |
| FR6 | Play/Pause reliably changes the existing video's actual state, and the button's text/icon/pressed state follows native play/pause events, including playback ending. |
| FR7 | A rejected play/media operation shows recoverable feedback, leaves selection/crop controls usable, and displays/logs no physical path. |
| FR8 | Current time and finite duration are both visible, correctly formatted, and do not visibly shift surrounding controls as digits change. |
| FR9 | Mouse and touch timeline seeking reach the beginning, middle, and end bounds without producing a negative or over-duration position. |
| FR10 | Progress advances during playback from media events; profiling/code inspection finds no requestAnimationFrame or equivalent animation-frame polling loop. |
| FR11 | Moving volume between 0, a midpoint, and 1 changes audible media volume and the C#-rendered slider matches the element. |
| FR12 | Mute preserves the selected volume level, silences playback, and restores that level on unmute while the UI remains synchronized. |
| FR13 | The selector offers exactly the initial 0.25x, 0.5x, 1x, 1.5x, and 2x options from a data collection rather than duplicated command branches. |
| FR14 | Each offered rate changes actual playback speed and the rendered selected value follows the media element's rate. |
| FR15 | Enabling standard loop repeats the complete video from its end; disabling it lets playback end normally. |
| FR16 | Standard-loop active, inactive, and disabled states are visually distinct and programmatically exposed. |
| FR17 | Set A and Set B capture the displayed current position, and setting either again replaces that marker according to the invariant. |
| FR18 | With both markers present, A is always before B and both are within duration; moving A to/past B clears B, while setting B at/before A retains the prior valid B and reports the rejection accessibly. |
| FR19 | A/B activation is disabled until both valid markers exist and becomes available once they do. |
| FR20 | With A/B active, reaching or passing B seeks to A and playback continues without replacing/reloading the video element. |
| FR21 | Disabling A/B preserves its valid markers; Clear removes both markers and disables A/B. |
| FR22 | Enabling standard loop turns A/B off, enabling A/B turns standard/native loop off, and both active states are never rendered simultaneously. |
| FR23 | Valid A and B positions are visible on the timeline and their formatted values are reported in the control UI. |
| FR24 | Controls begin visible, hide approximately one second after inactivity, and reappear after a non-drag mouse click or touch tap on the video. |
| FR25 | Continuous interaction longer than one second does not hide the active control; completing interaction restarts the full one-second interval. |
| FR26 | Hidden controls cannot be clicked, and operating every visible button, range, or selector neither starts crop dragging nor changes object-position. |
| FR27 | Selection, Reset crop, mouse/touch/pen crop drag, framing clamps, playback error recovery, and object-position behave as before. |
| FR28 | Fill-tab retains operable custom controls over the video, hides the pre-existing editor/helper chrome, and exits with Escape without losing media or crop state. |
| FR29 | Player CSS is isolated, both themes remain intact, bright/dark video content remains readable, and active/focus/disabled/error states are distinguishable. |
| FR30 | JavaScript exports only focused media/DOM operations and contains no marker, loop-precedence, visibility-timer, selection, or application business rules. |
| FR31 | Media synchronization is event-driven and bounded; no animation-frame polling or render-triggered media query is observed. |
| FR32 | Reloading loses player settings/markers and inspection finds no new request payload, DTO field, local/session storage key, or server persistence. |

## Test Cases

**Automated regression tests:**

- Run the existing xUnit suite with `make test`, retaining `WebApp.Tests/Client/VideoFrameStateTests.cs`, `FillTabStateTests.cs`, `ThemeBootstrapTests.cs`, service tests, and endpoint tests as regression coverage for framing, Fill-tab lifecycle, themes/static assets, discovery, and streaming.
- Per discovery, add no Playwright, bUnit, Selenium, or other automated UI/media dependency in this stage.
- ⚠️ TODO (deferred by product decision): add direct `MediaPlayerState` transition/invariant tests and rendered-browser media tests in a later testing slice. Until then, the manual Vivaldi checks below are authoritative for the new behavior.

**Integration/manual boundary:**

- Existing `WebApplicationFactory` endpoint tests continue to prove range-enabled stream availability but cannot prove Vivaldi media APIs, audio behavior, timing, CSS overlays, or touch/pointer separation.
- A real browser-decodable file served through the configured read-only library is required for the end-to-end acceptance pass.

## Manual Verification

1. From the repository root, configure the existing private `.env`, run `make docker-run`, and open the loopback application in Vivaldi with DevTools available.
2. Scan and select a playable video. Confirm the existing 9:16 crop and one `<video>` remain, the native controls are absent, and the custom controls initially show muted, volume 1, 1x, loop off, no markers, current time zero, and a finite duration after metadata loads.
3. Let the page idle. Confirm the controls hide after approximately one second. Click/tap the video without moving; confirm they reappear. Hold/drag a visible slider longer than one second and confirm the controls remain until the interaction finishes, then hide one second later.
4. Drag the video crop with mouse and touch. Confirm the crop moves/clamps normally and a drag does not accidentally toggle or activate controls. Operate every control and confirm object-position does not change. Confirm Reset crop still centers framing.
5. Play, pause, and let a short video reach its end. Confirm actual playback and the Play/Pause label/icon/pressed state stay synchronized. Trigger a rejected operation if practical and confirm non-blocking, path-safe recovery feedback.
6. Seek to the beginning, middle, and near end with mouse and touch. Confirm current time follows the completed seek, duration remains stable, values are formatted, and the page performs no extra scan or duplicate stream load.
7. Change volume to 0, 0.5, and 1. Mute at a nonzero volume, move/reveal the controls, and unmute. Confirm actual sound, slider state, and restored volume agree.
8. Select 0.25x, 0.5x, 1x, 1.5x, and 2x. Confirm playback visibly changes speed and the selected value remains synchronized.
9. Enable standard loop and play through the end. Confirm playback restarts from zero and the active state is clear. Disable it and confirm playback can end.
10. At a known time set A, move later and set B, then enable A/B loop. Confirm the timeline shows both markers and playback seeks from B back to A repeatedly. Replace A with an earlier valid value and B with a later valid value and repeat.
11. Attempt to set B at or before A. Confirm the prior valid B remains (or B remains unset), A/B cannot enter an invalid range, and an accessible validation message appears. Set A at or beyond B and confirm B is cleared.
12. Disable A/B and confirm playback crosses B while markers remain. Re-enable it, then enable standard loop and confirm A/B turns off. Enable A/B again and confirm standard/native loop turns off. Confirm both never appear active together.
13. Use Clear A/B and confirm both markers disappear and A/B is disabled. Select a different video and confirm time/play/loop/markers reset. Reload and confirm all non-persistent player preferences return to defaults.
14. Enter Fill-tab mode while playing with a non-center crop and non-default volume/rate. Confirm the same video fills the pane, custom controls remain operable and auto-hide/reveal, normal editor/helper chrome remains hidden, and playback/crop state is retained. Press Escape and confirm normal mode/focus restoration still works.
15. Repeat the main control, auto-hide, crop separation, standard loop, and A/B loop checks with Vivaldi touch emulation or a touchscreen device at narrow and wide pane sizes.
16. Test both application themes and bright/dark video frames. Confirm the scrim and controls remain readable, touch targets do not overlap, active/disabled/error states are distinguishable, and unrelated component styles are unchanged.
17. Inspect Network, storage, console, and rendered DOM. Confirm no player-state API/storage was added, no physical path appears, there is no animation-frame polling, and interop traffic follows media/user events rather than rendering.
18. Run `make test` and confirm the complete existing Docker Compose xUnit suite passes.

## Definition of Done

- `Requirements.md`, `Plan.md`, and `Validation.md` remain synchronized in this spec folder and every FR has an observable acceptance criterion.
- The existing selected HTML5 video and 9:16/Fill-tab layouts remain the playback and presentation engine, with native controls removed and no duplicate player.
- Custom Play/Pause, timeline/time display, seeking, volume/mute, data-driven rates, standard loop, valid A/B loop, marker replacement/clearing, and mutually exclusive loop states pass the Vivaldi manual acceptance flow.
- Controls hide after one second of inactivity, reappear on non-drag click/tap, remain present during active interaction, and do not interfere with selection or crop dragging.
- C# owns canonical player state and behavior; isolated JavaScript contains only focused browser-media operations and bounded snapshot reads.
- Component-isolated player CSS works in both themes, normal 9:16 mode, Fill-tab mode, mouse, and touch layouts without leaking into unrelated components.
- Existing privacy, local-only, stream, library, grouping, framing, theme, and Fill-tab boundaries remain unchanged.
- No new runtime/test package or persistence mechanism is introduced; deferred automated player coverage remains called out as a later task.
- All existing tests pass through `make test`, and the Vivaldi-only manual validation is recorded as completed when implementation lands.
- Vendor-specific decisions remain supported by current Microsoft Learn evidence in `Plan.md`.

## Rollback Plan

- Remove `MediaPlayerControls.razor`, `MediaPlayerControls.razor.css`, and `MediaPlayerState.cs`.
- Revert the player-state/event/control integration and visibility timer in `VerticalVideoEditor.razor`, the overlay-container adjustments in `VerticalVideoEditor.razor.css`, and the media-operation exports in `videoEditor.js`.
- Restore the `controls` attribute on the existing `<video>` to return to the previous native-control behavior.
- No migration, server rollback, stored-state cleanup, DTO change, or video-library recovery is required because this feature changes only client code and keeps no persistent state.
