# Validation: Dark Mode

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | A visible theme control appears in the application header at desktop and handheld widths. |
| FR2 | With no valid saved value, the first rendered document and completed application use dark mode; the only selectable values are dark and light. |
| FR3 | Toggling changes the complete visible interface immediately without navigation and preserves scan results, selected video, playback, mute choice, and crop coordinates. |
| FR4 | After selecting either mode and reloading or reopening the same origin, the selected mode is restored from the single application-specific `localStorage` entry. |
| FR5 | The root fallback and bootstrap script occur before stylesheet loading, and a saved light theme does not visibly paint dark first under normal browser loading. |
| FR6 | Inspecting `<html>` shows exactly `data-bs-theme="dark"` or `data-bs-theme="light"`, and Bootstrap buttons/controls respond to the same value. |
| FR7 | Both modes retain recognizable green primary and gold accent colors with readable mode-specific canvas, surfaces, borders, text, selections, focus, and error states. |
| FR8 | The shell, header, workspace, every library state, every editor state, buttons, errors, and Blazor error UI have intentional styling in both modes; vendored Bootstrap files are unchanged. |
| FR9 | The computed root `color-scheme` matches the selected mode and native browser/video controls remain legible. |
| FR10 | The control toggles with Enter and Space, has visible keyboard focus, and its accessible name always describes the next action. |
| FR11 | DevTools shows no network request when toggling, and no theme preference appears in server requests or responses. |
| FR12 | Invalid storage values and blocked storage access produce no visible/unhandled error; invalid/no-readable preference falls back to dark while an in-page toggle remains effective. |
| FR13 | Header identity, privacy status when space permits, and theme control do not overlap or clip at existing desktop and handheld breakpoints. |

## Test Cases

**Unit/integration tests:**

- `WebApp.Tests/Client/ThemeBootstrapTests.cs` using xUnit and the existing `WebApplicationFactory<Program>` pattern: request `/` with a temporary video-library root and assert the HTML root declares `data-bs-theme="dark"`.
- `WebApp.Tests/Client/ThemeBootstrapTests.cs`: assert `<meta name="color-scheme" content="dark light">` and the theme script occur before Bootstrap/application stylesheet links, protecting the pre-paint requirement.
- `WebApp.Tests/Client/ThemeBootstrapTests.cs`: assert the response contains no server theme endpoint, cookie configuration, or injected user preference.
- Run all existing service, endpoint, and framing tests with `make test` to prove theme work does not change discovery, streaming, authorization, or editor calculations.
- The repository has no browser automation framework. Actual DOM storage, computed CSS, focus, responsive paint, native-control theming, and no-flash behavior remain covered by the manual checks below rather than introducing a new frontend dependency.

## Manual Verification

1. From the repository root, use the existing private `.env` and run `make docker-run`; open the configured loopback URL.
2. In browser DevTools, clear the `video-manager-theme` local-storage entry and hard reload. Confirm the first visible paint and completed UI are dark, with no light flash.
3. Confirm the header control is visible and its accessible name is “Switch to light mode.” Activate it and confirm `<html data-bs-theme="light">`, the complete UI, Bootstrap controls, scrollbars, and native video controls change to light immediately.
4. Reload and close/reopen the tab. Confirm light mode persists. Toggle to dark, reload again, and confirm dark persists under the same origin.
5. Scan and select a video, start playback, choose an audio mute state, and drag the crop away from center. Toggle twice and confirm the library snapshot, selected row, playback position/state, mute state, and crop coordinates do not reset.
6. Use DevTools Network while toggling and confirm no request is issued. Inspect local storage and confirm the only preference payload is the literal `dark` or `light` under the application-specific key.
7. Replace the saved value with an unsupported string and hard reload. Confirm dark mode is selected and no visible error occurs.
8. Test with browser storage blocked or unavailable. Confirm startup stays dark, the current page can still switch visually, and no unhandled Blazor error appears even though the choice cannot persist.
9. Exercise unscanned, scanning, empty, populated, selected, scan-error, playback-error, and Blazor error states in both modes. Confirm readable text/borders, non-color selection cues, and green/gold identity.
10. Navigate the theme control using only the keyboard. Confirm Tab focus is visible, Enter and Space toggle it, and accessibility inspection reports the correct next-action label after each change.
11. Repeat the state and toggle checks at desktop width and narrow handheld widths. Confirm header controls, Scan/Rescan, Reset, native media controls, and status/error text remain visible without overlap or clipping.
12. Inspect the computed root style in each mode and confirm `color-scheme` matches `dark` or `light`. Check native controls and scrollbars in each supported browser available locally.
13. Run `make test` and confirm all xUnit tests pass in the isolated Docker Compose test stack.

## Definition of Done

- `Requirements.md`, `Plan.md`, and `Validation.md` remain synchronized in this spec folder, with every FR mapped to an acceptance criterion.
- Dark is the no-preference/error default, while valid light and dark choices persist locally across reloads.
- Initial theme application occurs before styles without waiting for WebAssembly and without a visible incorrect-theme flash.
- The accessible header control remains visible and operable at desktop and handheld widths.
- Global semantic tokens and all relevant isolated CSS files render every existing application state intentionally in both modes while retaining green/gold identity.
- Theme switching preserves all library, selection, playback, audio, and crop state and causes no network request.
- Invalid/unavailable storage, keyboard interaction, focus, contrast, native controls, no-flash behavior, and responsive rendering are manually verified.
- Vendored Bootstrap files remain untouched and no new runtime package, remote asset, server endpoint, cookie, or persistence service is introduced.
- Official Blazor, Bootstrap, and browser documentation supporting the design remains recorded in `Plan.md`.
- New bootstrap-markup coverage and all existing xUnit tests pass through `make test`.

## Rollback Plan

- Remove `ThemeToggle.razor`, its isolated CSS, and `wwwroot/js/theme.js`, then remove the toggle reference and client-components import from the server layout.
- Restore the pre-theme `App.razor` root/head markup and the literal colors in `app.css` plus the five isolated CSS files listed in `Plan.md`.
- Remove `ThemeBootstrapTests.cs` if the feature is rolled back.
- A user's inert `video-manager-theme` local-storage string may remain in the browser and can be deleted manually; no server, database, filesystem, or video data requires rollback.
