# Requirements: Saturation Filter Control

## Table of Contents

- [Requirements: Saturation Filter Control](#requirements-saturation-filter-control)
  - [Problem Statement](#problem-statement)
  - [User Stories](#user-stories)
  - [Functional Requirements](#functional-requirements)
  - [Non-Functional Requirements](#non-functional-requirements)
  - [Out of Scope](#out-of-scope)
  - [Open Questions](#open-questions)

## Problem Statement

`VerticalVideoEditor.razor` currently exposes crop repositioning (`VideoFrameState`), playback/A-B looping (`MediaPlayerState` via `MediaPlayerControls.razor`), and Fill-tab presentation (`FillTabState`), but has no way to adjust the visual look of the previewed clip. Users reviewing footage for color/vibrancy issues have no in-app control to preview a saturation adjustment against the live video element. This feature adds a client-side-only saturation preview control — a vertical Bootstrap range slider placed beside the video viewport — that applies a CSS `filter: saturate(x%)` to the `<video>` element in both normal and Fill-tab layouts, sharing the exact show/hide behavior already implemented for `MediaPlayerControls` (`_controlsVisible` in `VerticalVideoEditor.razor`).

## User Stories

- Given a video is selected and controls are visible (hover/tap reveals them), when the user drags the vertical saturation slider, then the video's rendered saturation updates live and a numeric percentage output next to the slider updates in sync.
- Given the controls are hidden (after the auto-hide delay, same as `MediaPlayerControls`), when the user is not hovering/interacting, then the saturation slider and its output are also hidden, matching the media controls' fade behavior.
- Given a saturation adjustment has been made away from 100%, when the user activates the reset action, then saturation returns to 100% (`saturate(100%)`, i.e. no filter effect) and the slider/output reflect the reset value, without needing to drag back manually.
- Given the user selects a different video from the library, when the new video loads, then saturation resets to 100% for that new selection, mirroring how `VideoFrameState` resets crop position on `Select`.
- Given the user is in Fill-tab mode, when they hover the side region of the full-tab viewport, then the same saturation slider appears and behaves identically to normal mode.

## Functional Requirements

1. FR1 — `VerticalVideoEditor.razor` renders a vertical Bootstrap `form-range` slider (`min=0`, `max=300`, per `SaturationState.Max`) positioned along the side of the video viewport, in both normal and Fill-tab (`_fillTab.IsActive`) layouts.
2. FR2 — The slider's visibility is driven by the same `_controlsVisible` boolean (and its existing hover/interaction/auto-hide timer logic) already used for `MediaPlayerControls`'s `IsVisible` parameter — no separate hover region or timer is introduced.
3. FR3 — A live numeric `<output>` element next to the slider displays the current saturation percentage value, updating on every `input` event (drag), consistent with the plain-HTML `<output>` pattern the user requested.
4. FR4 — Dragging the slider applies `filter: saturate(<value>%)` directly to the `<video>` element's inline style (parallel to the existing `VideoStyle` computed property that sets `object-position`), with no FFmpeg or server involvement.
5. FR5 — A new `SaturationState` (or equivalent) client-owned state object, following the `VideoFrameState`/`FillTabState` pattern, owns the current saturation value and exposes a `Select(string? id)` method that resets the value to 100 whenever the selected video id changes.
6. FR6 — A reset affordance (icon button) returns the value to 100 and is shown/hidden using the same `_controlsVisible` visibility as the slider itself (not always visible).
7. FR7 — The control uses Bootstrap Icons, real Bootstrap `form-range`/utility classes, and design tokens from `Specs/20260827194328-perene-tech-design-system-refactor/design-guide-en.html` (gold/primary accent for the active track, dark toolbar surface) for any custom CSS needed to render the slider vertically, since Bootstrap has no built-in vertical range orientation.
8. FR8 — The saturation slider and reset button are keyboard operable (native `<input type="range">` and `<button>` semantics), have accessible names (`aria-label`), and the slider's current value is exposed via `aria-valuenow`/`aria-valuetext` semantics already provided natively by `<input type="range">` plus the visible `<output>`.

## Non-Functional Requirements

- The feature must not introduce any new NuGet/npm dependency; it uses existing Bootstrap 5.3.8, Bootstrap Icons, and Blazor two-way/event binding.
- No physical/root-relative path or server round-trip is involved; this is a pure client-side CSS presentation change, consistent with the project's opaque-ID/local-only boundaries.
- Custom CSS for the vertical slider orientation belongs in `VerticalVideoEditor.razor.css` (an existing narrowly scoped `.razor.css` file), per the project's convention that only behavior Bootstrap can't express gets component-scoped CSS.
- The control must remain usable at the existing responsive breakpoints already handled by `VerticalVideoEditor`/`MediaPlayerControls` (small viewport, Fill-tab full-bleed).

## Out of Scope

- Persisting saturation preference across app sessions or across video selections (resets per FR5).
- Any server-side or FFmpeg-based saturation processing, export, or baking the filter into the actual video file — this is preview-only, mirroring the existing crop/reframe preview which also never touches the source file.
- Additional CSS filters (brightness, contrast, hue-rotate, etc.) — only saturation is in scope for this spec.
- Changing `MediaPlayerControls.razor`'s bottom toolbar layout or its existing controls.

## Open Questions

- None outstanding — placement (side of video, both normal and Fill-tab modes), visibility (tied to `_controlsVisible`), reset behavior (visible only when controls are visible), and persistence (reset per video) were all confirmed with the user during discovery.
