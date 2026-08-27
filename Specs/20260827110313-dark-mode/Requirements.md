# Requirements: Dark Mode

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

Video Manager currently hard-codes a light palette across `WebApp/WebApp/wwwroot/app.css` and the isolated layout, page, library, and editor styles. Users need a dark-first interface that retains the existing green/gold identity, can be switched explicitly between dark and light from the application header, and remembers that choice locally without adding server state or weakening the application's local-only privacy boundary.

## User Stories

- Given a first visit with no saved preference, when Video Manager loads, then the complete application appears in dark mode without first flashing the light palette.
- Given either theme is active, when the user activates the header theme control, then the entire interface switches immediately to the other theme without navigation or loss of video-library/editor state.
- Given the user selected a theme, when the page is refreshed or reopened in the same browser origin, then that theme is restored from local browser storage.
- Given local storage is unavailable or contains an unsupported value, when the app loads or the toggle is used, then the app remains usable and falls back safely to dark mode.

## Functional Requirements

1. FR1 — `WebApp/WebApp/Components/Layout/NavMenu.razor` must expose one visible theme control in the application header at desktop and handheld widths.
2. FR2 — The application must support exactly two modes, `dark` and `light`, with `dark` used when no valid saved preference exists.
3. FR3 — Activating the theme control must switch the root document and all current UI states immediately without a page reload, navigation, rescan, selection reset, playback reset, or crop-position reset.
4. FR4 — A manual theme choice must be stored under one application-specific key in browser `localStorage` and restored for later loads on the same origin.
5. FR5 — The saved theme, or the dark fallback, must be applied in `WebApp/WebApp/Components/App.razor` before the application styles and interactive WebAssembly component initialize, avoiding a visible light-theme flash.
6. FR6 — The root `<html>` element must use Bootstrap 5.3's `data-bs-theme="dark|light"` mechanism so Bootstrap controls and custom application styles share one authoritative theme value.
7. FR7 — Both modes must preserve the existing green primary and gold accent identity while providing mode-appropriate backgrounds, surfaces, borders, shadows, text, muted text, focus rings, selection cues, and error colors.
8. FR8 — Theme tokens must cover the application shell, header, workspace heading, library states and rows, editor states and stage, Reset/Scan buttons, playback errors, and the Blazor error UI without hand-editing vendored Bootstrap files.
9. FR9 — Each mode must declare the matching browser `color-scheme` so native video controls, form controls, scrollbars, and browser-supplied UI use an appropriate palette.
10. FR10 — The theme control must be keyboard operable, retain a visible focus indicator, and expose an accessible name that communicates the action it will perform (for example, “Switch to light mode”).
11. FR11 — Theme switching and persistence must execute entirely in the browser, make no HTTP request, and send no preference or browser-storage data to the server.
12. FR12 — Storage read/write failures and invalid stored values must not surface an application error; they must leave the current UI usable and select the dark fallback when no valid value can be read.
13. FR13 — Dark and light modes must remain usable without clipped or overlapping header controls at the existing responsive breakpoints.

## Non-Functional Requirements

- **Accessibility:** Text, interactive controls, focus indicators, selected rows, and error states must maintain clear contrast in both modes; color cannot be the only indicator of selection or control purpose.
- **Performance:** Initial theme application must be synchronous and minimal, execute before CSS paint, and require no server round trip or WebAssembly startup wait.
- **Privacy:** The preference remains browser-local and contains only the literal value `dark` or `light`; no filesystem or video data is involved.
- **Maintainability:** Shared semantic CSS custom properties live in the existing global authoring stylesheet, while component layout rules remain in their current isolated CSS files.
- **Dependency control:** Use the vendored Bootstrap 5.3.3 color-mode support and browser APIs already available; add no runtime package, frontend framework, remote asset, or service.
- **Compatibility:** Preserve the existing .NET 10 Interactive WebAssembly architecture, Docker-only workflow, scan/stream behavior, native video controls, and framing interactions.

## Out of Scope

- A system/automatic theme mode or reacting to operating-system theme changes.
- More than two themes, user-defined palettes, per-component themes, or theme scheduling.
- Server-side profiles, cookies, database persistence, cross-browser synchronization, or account-level preferences.
- Replacing Bootstrap, editing its vendored output, or introducing Sass/build tooling.
- Redesigning the application layout, typography, video workflow, or green/gold brand identity.
- Adding a browser automation framework solely for this feature.

## Open Questions

None. Dark-default behavior, two explicit modes, `localStorage` persistence, and the green/gold visual direction were selected during discovery.
