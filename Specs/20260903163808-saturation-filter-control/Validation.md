# Validation: Saturation Filter Control

## Table of Contents

- [Validation: Saturation Filter Control](#validation-saturation-filter-control)
  - [Acceptance Criteria](#acceptance-criteria)
  - [Test Cases](#test-cases)
  - [Manual Verification](#manual-verification)
  - [Definition of Done](#definition-of-done)
  - [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | A vertical `input.form-range` (`min=0`, `max=300`) renders beside the video viewport in both normal and Fill-tab layouts whenever a video is selected. |
| FR2 | The slider and its side rail carry the same visible/hidden CSS state as `MediaPlayerControls` at all times — toggling `_controlsVisible` (via hover, interaction, or the auto-hide timer) changes both simultaneously with no independent timer. |
| FR3 | An `<output>` element next to the slider shows the current integer percentage and updates on every `input` event during a drag, without waiting for `change`/commit. |
| FR4 | Setting the slider to a non-100 value visibly changes the rendered video's saturation (verified via computed style / visual inspection) and the value is present in `VideoStyle`'s `filter: saturate(...)` segment together with the existing `object-position`. |
| FR5 | `SaturationState.Value` is 100 by default, changes only via `SetValue`, and resets to 100 when `Select` is called with a different id than the currently stored id (mirroring `VideoFrameStateTests`/`FillTabStateTests` conventions). |
| FR6 | The reset button sets the value back to 100, is disabled when the value is already 100, and is hidden/shown by the same class as the slider (not always visible). |
| FR7 | The slider/reset button use Bootstrap classes/tokens (`form-range`, `btn`, `bi-*` icons, `--bs-primary`) with only vertical-orientation CSS added in `VerticalVideoEditor.razor.css`; no hardcoded colors outside existing design tokens. |
| FR8 | The slider has an `aria-label`, is operable with arrow keys (native range semantics), and the reset button has an `aria-label` and a visible focus state consistent with other icon-only buttons in this component (e.g. "Reset crop"). |

## Test Cases

**Unit tests:**

- `WebApp.Tests/Client/SaturationStateTests.cs` (new, following `VideoFrameStateTests.cs`'s style):
  - Default `Value` is 100.
  - `SetValue` clamps below 0 to 0 and above 300 to 300.
  - `Select("first")` then `SetValue(400)` then `Select("second")` resets `Value` to 100.
  - `Select("second")` then `Select("second")` again (same id) does not reset a value that was changed via `SetValue`.
  - `Reset()` sets `Value` to 100 regardless of prior value.

**Integration tests:**

- ⚠️ TODO: No existing Blazor component-render test harness is present in `WebApp.Tests` for `VerticalVideoEditor.razor` (existing `Client` tests cover only plain C# state models and theme bootstrap, per `WebApp.Tests/Client/`), so the rendered-markup/visibility-class behavior (FR1, FR2, FR6, FR7, FR8) is covered by Manual Verification below rather than an automated component test, consistent with how Fill-tab/crop UI behavior is already validated in this repo.

## Manual Verification

Starting from a clean state via `make docker-run` (per `AGENTS.md`'s Execution Environment):

1. Run `make docker-run` and open the app at the published loopback URL; scan the library and select a video.
2. Hover the video preview — confirm the bottom media toolbar and the new vertical saturation slider (with its `%` output) both appear together on the side of the video.
3. Move the pointer away and wait past the auto-hide delay — confirm both the toolbar and the saturation slider/reset button fade out together.
4. Hover again, drag the saturation slider — confirm the video visibly desaturates/oversaturates live and the numeric output updates continuously during the drag, not just after release.
5. Click the reset button — confirm the video returns to normal saturation, the slider snaps back to 100, and the reset button becomes disabled.
6. Select a different video from the library — confirm saturation resets to 100% for the new selection even if the previous video had a custom value.
7. Enter Fill-tab mode (fullscreen icon) — confirm the same slider/output/reset control appears beside the full-bleed video on hover and behaves identically to normal mode; exit Fill-tab and confirm state is unaffected.
8. Tab to the slider with the keyboard only — confirm arrow keys change the value and the output updates; tab to the reset button and activate it with Enter/Space.
9. Resize the browser to a narrow viewport (per the existing `@media (max-width: 24rem)` breakpoint in `MediaPlayerControls.razor.css`) — confirm the side rail remains usable and does not overlap or clip the video/toolbar.
10. Toggle the OS/browser dark and light theme — confirm the slider's colors remain legible and consistent with `--bs-primary` in both themes.

## Definition of Done

- Requirements, Plan, and Validation docs in this folder reflect the implemented behavior.
- `make test` passes, including the new `SaturationStateTests.cs`.
- `VerticalVideoEditor.razor` / `VerticalVideoEditor.razor.css` updated consistently; responsive, hidden/visible, and Fill-tab states are covered per the Manual Verification steps above.
- No new external dependency was introduced; only existing Bootstrap/Bootstrap Icons assets and Blazor two-way binding are used.
- No vendor-specific API decision was made (see Plan.md's "Not applicable" evidence section).

## Rollback Plan

Revert the changes to `VerticalVideoEditor.razor`, `VerticalVideoEditor.razor.css`, and delete `WebApp.Client/Models/SaturationState.cs` (and its test file) — the feature is additive, self-contained markup/state with no persisted data, configuration flag, or migration, so reverting the commit fully restores prior behavior with no other cleanup required.
