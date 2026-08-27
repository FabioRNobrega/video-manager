# Requirements: Perene Tech Design System Refactor

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

The implemented Blazor interface uses a bespoke `--color-*` theme, custom control styling, hand-authored SVG/Unicode icons, the Inter/system font stack, and a vendored Bootstrap 5.3.3 stylesheet. It therefore does not consistently follow the Perene.Tech system defined by `Specs/20260827194328-perene-tech-design-system-refactor/design-guide-en.html`. The complete current experience—including the full custom video-player menu—must be visually refactored into “Perene Tech Videos” using the guide's dark and Kindle-paper light palettes, Zilla Slab/Montserrat typography, Bootstrap 5.3.8 components, and Bootstrap Icons without changing video discovery, streaming, reframing, playback, theme, or Fill-tab behavior.

## User Stories

- Given any current Perene Tech Videos screen or state, when the page renders in dark or light mode, then its color, typography, hierarchy, and component states follow the spec-local `design-guide-en.html` consistently.
- Given a user operating the library, editor, or media player, when they use buttons, lists, alerts, progress controls, and form controls, then those controls retain their existing behavior while using recognizable Bootstrap 5 markup and states.
- Given a selected video in normal or Fill-tab mode, when the player menu is visible, then its timeline, time readout, playback, audio, speed, loop, A/B marker, clear, and Fill-tab controls form a cohesive responsive Bootstrap toolbar without losing any existing state or behavior.
- Given a user navigating with keyboard or assistive technology, when they encounter an icon-only action or status, then it has a sufficient target, accessible name, tooltip, focus treatment, and non-color indication.
- Given a future contributor or coding agent, when they add or change UI, then `AGENTS.md`, the spec-local `design-guide-en.html`, and the shared design tokens provide an explicit, durable implementation contract.

## Functional Requirements

1. FR1 — All user-visible product naming in `App.razor`, `NavMenu.razor`, and `Home.razor` must use “Perene Tech Videos” while retaining the existing local-only product description and routes.
2. FR2 — `WebApp/WebApp/wwwroot/app.css` must define the guide's dark and Kindle-paper light design tokens and map the applicable Bootstrap `--bs-*` variables to those tokens for `data-bs-theme="dark"` and `data-bs-theme="light"`.
3. FR3 — The application root document must load Zilla Slab and Montserrat from Google Fonts; headings and product/title typography must use Zilla Slab, while body, labels, metadata, utility text, controls, and form elements use Montserrat with documented fallback stacks.
4. FR4 — The root document must load Bootstrap 5.3.8 CSS and the Bootstrap 5.3.8 bundle from the official jsDelivr URLs documented by Bootstrap, and must no longer load the existing vendored Bootstrap 5.3.3 stylesheet at runtime.
5. FR5 — The root document must load Bootstrap Icons 1.13.1 from its documented jsDelivr stylesheet, and every current hand-authored SVG, emoji, or text glyph used as an interface icon in `ThemeToggle.razor` and `MediaPlayerControls.razor` must be replaced by a semantically appropriate `.bi` icon.
6. FR6 — `MainLayout`, `NavMenu`, `Home`, `VideoLibrary`, `VerticalVideoEditor`, `ThemeToggle`, and `MediaPlayerControls` must use Bootstrap 5 component classes and utilities where the guide defines an equivalent pattern, while component-specific layout and behavior remain in their existing `.razor.css` files.
7. FR7 — The Scan action must remain the single gold-filled `btn-primary` screen action, while complementary actions use the user-selected green-filled `btn-secondary` treatment; semantic success, warning, danger, and info variants must be reserved for matching feedback/actions.
8. FR8 — Library scan loading, initial, empty, error, populated, hover, and selected states must retain their existing copy/behavior and be presented using the guide's Bootstrap spinner, alert/card, and list-group conventions.
9. FR9 — Editor empty, selected, playback-error, normal 9:16, and Fill-tab states must retain their current behavior and adopt the guide's surface, typography, border, shadow, alert, and responsive conventions.
10. FR10 — The complete `MediaPlayerControls` menu—including Play/Pause, Mute/Unmute, whole-video Loop, Fill tab, Set A, Set B, A/B Loop, and Clear actions—must remain real buttons, use Bootstrap Icons at `currentColor` where the action is iconographic, expose accessible labels/state, provide at least a 40×40 CSS-pixel target for icon-only actions, and initialize/dispose Bootstrap tooltips safely across Blazor renders.
11. FR11 — The player menu's timeline and A/B markers, elapsed/total time readout, volume range, playback-rate selector, grouped action rows, validation feedback, and playback errors must retain their existing C# state/callback flow while adopting Bootstrap progress, form, toolbar, button-group, and feedback conventions in both normal and Fill-tab modes.
12. FR12 — The existing theme preference and early `data-bs-theme` bootstrap must continue to prevent a wrong-theme flash; the theme control must use the guide's Bootstrap form-switch pattern and Bootstrap Icons without changing browser-local persistence behavior.
13. FR13 — The full interface must remain usable at the existing narrow and wide breakpoints: the library/editor columns stack, navigation content does not collide, media controls wrap without overlap, and Fill-tab continues to occupy the viewport.
14. FR14 — Meaning must not depend on color alone; normal text must meet WCAG AA contrast, focus must remain visible, icon-only controls must have accessible names, toggle state must remain programmatic, live status/error semantics must be preserved, and reduced-motion preferences must be honored.
15. FR15 — The refactor must not change scan/selection behavior, opaque video IDs, API routes or DTOs, filesystem privacy, crop math, custom playback logic, transient player state, theme storage, or Fill-tab lifecycle.
16. FR16 — `Specs/20260827194328-perene-tech-design-system-refactor/design-guide-en.html` must remain the detailed visual reference colocated with this spec, and `AGENTS.md` must identify it and the shared tokens/Bootstrap conventions as mandatory guidance for future UI work, including the approved CDN and typography decisions.
17. FR17 — `ThemeBootstrapTests.cs` must verify the product name and required stylesheet/script ordering and versions for Google Fonts, Bootstrap 5.3.8, Bootstrap Icons 1.13.1, theme bootstrap, application CSS, and the Bootstrap bundle without adding a browser-test framework.
18. FR18 — If a CDN asset is temporarily unavailable, semantic content and native control behavior must remain present with the declared fallback font stacks; no CDN URL, font request, or third-party asset may contain a physical/root-relative video path or video identifier.

## Non-Functional Requirements

- `Specs/20260827194328-perene-tech-design-system-refactor/design-guide-en.html` is the visual source of truth. Where its button prose conflicts, the discovery decision governs: gold-filled primary and green-filled secondary buttons.
- Bootstrap customization must use public Bootstrap classes and CSS custom properties instead of editing generated Bootstrap files or copying Bootstrap source into component CSS.
- Global palette, type, semantic, Bootstrap mapping, and reusable component tokens belong in `WebApp/WebApp/wwwroot/app.css`; page/component layout remains in CSS-isolated `.razor.css` files.
- No new NuGet, npm, frontend framework, browser automation, or build step may be introduced. The approved CDNs are runtime dependencies and require an Internet connection for complete styling/fonts/icons.
- Existing C# component/state ownership and the thin-JavaScript boundary must remain intact. Tooltip interop may only initialize/dispose Bootstrap UI behavior and must not own application state.
- Existing local-only, loopback, Docker Compose, filesystem-path privacy, and browser-safe logging constraints remain unchanged.
- Validation uses the existing Docker Compose xUnit workflow plus manual responsive, keyboard, screen-reader, contrast, and Vivaldi checks.

## Out of Scope

- New video/library/player features, navigation destinations, persistence, API changes, server services, database work, media processing, thumbnails, or transcoding.
- Recreating the style-guide demonstration page inside the application or adopting its book/folder sample content.
- Introducing Sass, a CSS preprocessor, npm, a component library beyond Bootstrap 5, or automated screenshot/visual-regression tooling.
- Self-hosting Bootstrap, Bootstrap Icons, Zilla Slab, or Montserrat in this setup.
- Deleting or hand-editing the existing vendored Bootstrap 5.3.3 files; they may remain in the repository but must not be referenced at runtime.
- Supporting additional themes beyond the existing dark and light modes.

## Open Questions

- None. Discovery established full-UI scope, CDN delivery, Bootstrap 5.3.8, green-filled secondary buttons, “Perene Tech Videos” naming, and xUnit-plus-manual validation.
