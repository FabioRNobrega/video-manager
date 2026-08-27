# Validation: Perene Tech Design System Refactor

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | The browser title, header brand, and home content show “Perene Tech Videos”; routes, APIs, namespaces, and Docker project names remain unchanged. |
| FR2 | Inspecting computed styles in both themes shows the guide's app/surface/elevated, border, text, semantic, brand, hover, tint, and mapped Bootstrap variables controlling the rendered UI. |
| FR3 | Network/computed-font inspection shows Zilla Slab on headings/product titles and Montserrat on body, labels, metadata, controls, and forms, with declared fallbacks present. |
| FR4 | The root document references Bootstrap CSS and bundle version 5.3.8 from jsDelivr, includes published SRI/crossorigin values, and contains no runtime link to `lib/bootstrap/dist/css/bootstrap.min.css`. |
| FR5 | The root document references Bootstrap Icons 1.13.1 and inspection finds no hand-authored interface SVG or emoji/text glyph remaining in ThemeToggle or MediaPlayerControls. |
| FR6 | Each listed component uses applicable Bootstrap card, list-group, alert, spinner, button, form, progress, flex/grid, spacing, or responsive classes while bespoke CSS is limited to design mapping and component composition. |
| FR7 | Scan is the only page-level gold-filled primary action; complementary actions render green-filled and semantic variants are used only when their meaning matches. |
| FR8 | Initial, scanning, empty, error, populated, hover, and selected library states retain their text/callback behavior and visibly follow the guide's component states. |
| FR9 | Editor empty, selected, error, 9:16, and Fill-tab states retain their behavior and consistently use the guide's surface/type/border/shadow/alert treatment. |
| FR10 | Play/Pause, Mute/Unmute, whole-video Loop, Fill tab, Set A, Set B, A/B Loop, and Clear remain operable in the redesigned menu; iconographic actions use semantic buttons, Bootstrap Icons at `currentColor`, accessible label/state, at least 40×40 icon-only targets, and non-duplicating Bootstrap tooltips. |
| FR11 | Timeline/A/B markers, elapsed/total time, volume, rate, grouped action rows, validation, and error feedback remain synchronized with existing C# state/callbacks and render as a cohesive responsive Bootstrap menu in normal and Fill-tab modes. |
| FR12 | Reloading either saved theme shows no wrong-theme flash; the form-switch label/icon/state matches the active theme and the existing browser-local persistence remains the only preference store. |
| FR13 | At narrow and wide viewports the navigation, library/editor layout, player controls, and Fill-tab mode remain fully usable without overlap, clipping, or unintended horizontal page scrolling. |
| FR14 | Keyboard focus is visible, control meaning/state is available without color, live status/error semantics remain, normal text meets WCAG AA contrast, and reduced-motion mode removes nonessential transitions. |
| FR15 | Existing automated and manual scan, selection, stream, crop, playback, theme, and Fill-tab behaviors pass with no API/DTO/storage/privacy changes. |
| FR16 | The design guide exists inside this spec folder, and `AGENTS.md` links that exact path while stating the mandatory Bootstrap 5, Bootstrap Icons, Zilla Slab/Montserrat, token ownership, accessible-state, and generated-asset rules for future UI work. |
| FR17 | `ThemeBootstrapTests` passes assertions for product identity, theme-first ordering, Google Fonts, Bootstrap 5.3.8, Bootstrap Icons 1.13.1, app/isolated CSS order, bundle loading, and absence of the old runtime Bootstrap link. |
| FR18 | With CDN requests blocked, content and native controls remain readable/operable using fallbacks; network/DOM/log inspection finds no video path or opaque video ID in any third-party request. |

## Test Cases

**Automated regression tests:**

- Extend `WebApp.Tests/Client/ThemeBootstrapTests.cs` using the existing xUnit and `WebApplicationFactory<Program>` pattern to assert the server-rendered root markup, product name, theme script position, exact CDN versions, CSS ordering, Bootstrap bundle, and removal of the old runtime Bootstrap link.
- Run all existing tests with `make test`, retaining service/endpoint privacy and range-stream checks plus `VideoFrameStateTests`, `FillTabStateTests`, and existing theme tests.
- Do not add bUnit, Playwright, Selenium, axe, screenshot comparison, npm, or another UI test dependency in this slice.

**Integration/manual boundary:**

- `WebApplicationFactory` can verify generated root HTML and unchanged endpoints, but it cannot prove downloaded font rendering, CSS layout, tooltip lifecycle, browser contrast, pointer interaction, or screen-reader output.
- A live Docker Compose application in Vivaldi with Internet access and a browser-decodable configured local video is required for the authoritative visual/interaction pass.

## Manual Verification

1. From the repository root, configure the existing private `.env`, run `make docker-run`, and open the loopback application in Vivaldi with DevTools available.
2. Inspect the initial HTML/network waterfall. Confirm `theme.js` runs before styles, Google Fonts loads Zilla Slab/Montserrat, Bootstrap CSS and bundle are exactly 5.3.8, Bootstrap Icons is 1.13.1, app CSS follows Bootstrap, isolated CSS follows app CSS, and vendored Bootstrap 5.3.3 is not requested.
3. Confirm the page title/header/home identity says “Perene Tech Videos,” retains the local/private language, and introduces no changed route, external navigation, authentication, or remote-hosting affordance.
4. In dark mode, inspect the app background, surfaces, elevated headers, borders, text tiers, links, gold primary, green secondary, feedback colors, hover/active/focus states, and Zilla Slab/Montserrat assignments against the spec-local `design-guide-en.html`.
5. Switch to light mode and repeat the inspection. Confirm the warm Kindle-paper background (`#EFE3C6`), lighter surface hierarchy, dark text, theme icon/label, and persistence after reload with no visible wrong-theme flash.
6. Before scanning, verify the library ready state and editor empty state. Use keyboard-only navigation to reach the theme switch, Scan, library area, and disabled Reset action; confirm logical order and visible focus.
7. Trigger Scan and confirm the spinner/status live region, gold Scan/Rescan primary treatment, and disabled state. Exercise an empty library and a scan failure if practical; confirm Bootstrap-themed empty/alert states and browser-safe copy.
8. Scan a populated library. Confirm real list-group rows, hover, keyboard activation, selected styling plus non-color indication, name wrapping, metadata, scrolling, and no physical/root-relative path.
9. Select a playable video. Confirm the editor card, Reset green secondary action, 9:16 viewport, helper/readout, playback error treatment, crop dragging, Reset crop, and all pre-existing player operations still work.
10. Inspect the complete media-player menu: timeline and A/B markers, elapsed/total time, Play/Pause, Mute/Unmute, volume, speed, whole-video Loop, Fill tab, Set A, Set B, A/B Loop, Clear, validation, and error feedback. Confirm the regions form deliberate Bootstrap toolbars/groups, remain readable in normal and Fill-tab modes, and wrap without overlap at narrow widths.
11. Confirm Bootstrap Icons replace emoji/SVG glyphs, inherit `currentColor`, icon-only buttons are at least 40×40, toggles expose pressed state, disabled controls remain distinguishable, and visible focus is never clipped.
12. Hover/focus every icon-only action and confirm a Bootstrap tooltip appears with the same meaning as its accessible label. Change state and selection repeatedly; confirm tooltip text updates, stale tooltip elements disappear, and no duplicates accumulate.
13. Exercise timeline seek, volume, mute, playback-rate selection, standard loop, A/B marker validation/looping/clearing, control auto-hide, and playback error recovery. Confirm only presentation changed and controls do not start crop dragging.
14. Enter Fill-tab mode while playing with a moved crop. Confirm the same video/state remains, controls wrap without overlap, tooltip/control contrast is readable over bright and dark frames, Escape exits, and focus returns according to existing behavior.
15. Test representative widths around 320, 375, 768, 1024, and 1440 CSS pixels. Confirm header content does not collide, columns stack at the existing breakpoint, lists/cards fit, player controls stay operable, and the normal page has no unintended horizontal scroll.
16. Enable reduced motion and confirm nonessential application/control transitions are removed or reduced. Check normal text and control-state contrast with browser accessibility tooling against WCAG AA, including the fixed dark text on Gold 500 and both theme backgrounds.
17. Use a screen reader or browser accessibility tree to verify landmarks/headings, library live states, error alerts, theme switch, selected/toggle states, ranges/select labels, and icon-only names. Confirm color is never the sole indication.
18. Block `fonts.googleapis.com`, `fonts.gstatic.com`, and `cdn.jsdelivr.net`, then reload. Confirm semantic content, route navigation, scan button, native form controls, and accessible names remain present with fallback fonts; record that visual parity/tooltips/icons are unavailable by the approved CDN tradeoff.
19. Inspect third-party requests, DOM, console, application storage, and normal logs. Confirm static CDN URLs contain no video ID/path, no new storage key/API request exists, and physical/root-relative paths remain absent.
20. Run `make test` and confirm the complete Docker Compose xUnit suite passes.

## Definition of Done

- The three spec files remain synchronized and every functional requirement has an observable acceptance criterion.
- The spec-local `design-guide-en.html`, `app.css`, and `AGENTS.md` establish a consistent design-system contract for future UI features.
- All current UI surfaces and states use “Perene Tech Videos,” the exact dark/Kindle-paper token system, Zilla Slab/Montserrat, Bootstrap 5.3.8, Bootstrap Icons 1.13.1, and the approved gold-primary/green-secondary hierarchy.
- Bootstrap CDN assets are version-pinned; Bootstrap CSS/JS use published integrity metadata; the old vendored Bootstrap stylesheet remains unmodified and unreferenced at runtime.
- Existing scan, selection, streaming, crop, playback, theme, and Fill-tab behavior and all privacy/local-only boundaries remain unchanged.
- Responsive, keyboard, focus, screen-reader, contrast, reduced-motion, tooltip lifecycle, all empty/loading/error states, and CDN degradation complete the manual acceptance pass.
- `ThemeBootstrapTests` covers root identity and asset invariants with existing xUnit patterns; no new test/runtime/build package is added.
- All existing tests pass through `make test`.
- External/vendor decisions remain supported by the official documentation evidence in `Plan.md`.

## Rollback Plan

- Revert CDN/product/theme link changes in `App.razor` and restore the previous vendored Bootstrap stylesheet reference.
- Revert the global token/type/Bootstrap mappings in `app.css` and the Razor/class/style changes in the listed layout, page, and client components.
- Remove `WebApp/WebApp.Client/wwwroot/js/bootstrapInterop.js` and restore the prior emoji/hand-authored SVG controls if tooltip/icon integration causes regressions.
- Revert the design-system additions to `AGENTS.md` and the corresponding root-markup test assertions.
- No migration, server/service rollback, environment cleanup, data recovery, or video-library rescan is required because this refactor changes presentation assets and documentation only.
