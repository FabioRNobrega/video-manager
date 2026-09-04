# Requirements: PereneArchive NAS Rebrand (Sidebar + Auto-Configured Storage Root)

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Branding](#branding)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

Today the app presents itself as a single-purpose "Perene Tech Videos" vertical-video reframing tool: `NavMenu.razor` shows one brand mark and no navigation, `Home.razor` is a single flat page with a Library section, a Cuts section, and a Video Compositions section stacked vertically, and the host video directory must be supplied by the operator through a required `VIDEO_ROOT` value in a repo-root `.env` file (`docker-compose.yml`, `.env.example`). There is no in-app folder configuration UI to remove — the only configuration surface is this Docker Compose bind mount.

The product direction is broader: a personal NAS-style archive ("PereneArchive") with a fixed host layout of `/home/PereneArchive/{videos,musics,pictures,documents}`, a Yandex Disk–style left sidebar for navigating between media categories, and zero manual path configuration for the operator. Only the existing video/cuts/compositions functionality needs to work for real; every other sidebar destination (Photos, Music, Documents, Shared, Family, Downloads, History, Trash) is a placeholder for future specs. The operator will move existing video/cut/composition files into the new `/home/PereneArchive/videos` layout by hand before this spec is implemented, so no migration tooling is required — the implementation only needs the storage paths to be correct once the files are already there.

## User Stories

- Given a fresh checkout with `/home/PereneArchive/{videos,musics,pictures,documents}` already present on the host, when the operator runs `make docker-run` without creating a `.env` file, then the app starts successfully using `/home/PereneArchive/videos` as the video library root, with no `VIDEO_ROOT` value required.
- Given an operator who has already moved their videos, `Cuts/`, and `VideoComposition/` folders into `/home/PereneArchive/videos`, when the app scans the library, saves a new cut, or creates a new composition, then all three continue to work exactly as before, reading from and writing to paths under `/home/PereneArchive/videos`.
- Given a user viewing the app, when the page loads, then they see a persistent left sidebar (Yandex Disk–style) with icon-led entries for Videos, Photos, Music, Documents, Downloads, Shared, Family, History, and Trash, plus a storage-usage indicator, using Bootstrap Icons and the existing design-system tokens.
- Given a user on a narrow/mobile viewport, when the page loads, then the same 9 destinations appear as a horizontally-scrollable top tab bar instead of a left rail, matching the Yandex Disk mobile pattern (one row, swipe/scroll left-right, no wrapping).
- Given a user on the sidebar, when they select "Videos," then they see the existing fully-functional Library/Cuts/Video Compositions experience unchanged in behavior.
- Given a user on the sidebar, when they select any other destination (Photos, Music, Documents, Downloads, Shared, Family, History, Trash), then they see a clearly-labeled "coming soon" placeholder view instead of a broken page or a silent no-op.
- Given the storage-usage indicator, when the sidebar renders, then it reflects real used/total bytes for `/home/PereneArchive` (or its mounted equivalent) rather than a hardcoded number.

## Functional Requirements

1. FR1 — `docker-compose.yml` supplies a default host path of `/home/PereneArchive` for `VIDEO_ROOT` when the operator has not set one (via `.env` or shell environment), so `make docker-run` succeeds with zero required configuration.
2. FR2 — The video library bind mount source becomes `${VIDEO_ROOT}/videos` (i.e. `/home/PereneArchive/videos` by default), mounted read-only at the existing internal path consumed by `VideoLibraryOptions`/`VideoLibraryService`.
3. FR3 — The Cuts bind mount source becomes `${VIDEO_ROOT}/videos/Cuts` and the VideoComposition bind mount source becomes `${VIDEO_ROOT}/videos/VideoComposition` (nested under the video root, mirroring their current relationship to the video root), read-write, matching `VideoCutOptions`/`VideoCompositionOptions` expectations.
4. FR4 — `.env.example` is updated to document `VIDEO_ROOT` as optional, defaulting to `/home/PereneArchive`, and to describe the expected `videos/`, `Cuts/`, and `VideoComposition/` subfolder layout instead of instructing the operator to create folders at the old root.
5. FR5 — No application code changes are required to relocate stored files; the operator moves existing videos, `Cuts/`, and `VideoComposition/` content into `/home/PereneArchive/videos` by hand before switching to this configuration, and the app must work correctly once those files are present at the new paths (verified by scanning, streaming, saving a cut, and creating a composition against the relocated files).
6. FR6 — `WebApp/WebApp/Components/Layout/MainLayout.razor` gains a persistent left sidebar region (new component, e.g. `Sidebar.razor`) styled per the Yandex Disk reference layout: brand/logo header, a primary navigation list of icon+label entries (Videos, Photos, Music, Documents, Downloads, Shared, Family, History, Trash), and a bottom storage-usage indicator, all using Bootstrap Icons (`bi-*`) and existing `app.css` design tokens — no new colors, fonts, or icon sets.
7. FR7 — The sidebar's "Videos" entry routes to (or activates) the existing Library/Cuts/Video Compositions experience currently at `Home.razor`'s `/` route, unchanged in behavior, data flow, and existing tests.
8. FR8 — Each non-Videos sidebar entry (Photos, Music, Documents, Downloads, Shared, Family, History, Trash) routes to its own distinct placeholder page/component that renders a "coming soon" empty state (icon + heading + short message), following the same empty-state visual pattern already used by `VideoGrid.razor`'s not-loaded/empty states, and performs no data fetching against real filesystem content.
9. FR9 — The sidebar visually indicates the active section (e.g. active/selected nav item state) and remains keyboard-navigable and screen-reader labeled, consistent with existing `NavMenu.razor`/accessibility conventions in this codebase.
10. FR10 — Below a defined narrow-viewport breakpoint, the left sidebar rail is replaced by a horizontally-scrollable top tab bar (matching the Yandex Disk mobile pattern: one row of the same 9 icon/label destinations, scrollable left-to-right, no wrapping), reusing the same `NavLink`s/active-state logic as the desktop rail rather than a second parallel navigation implementation.
11. FR11 — A new minimal server endpoint (e.g. `GET /api/storage/usage`) computes real used/total byte counts for the mounted `/home/PereneArchive` storage (or its configured internal mount path) via `.NET` filesystem APIs (e.g. `DriveInfo`) and returns a browser-safe DTO; the sidebar storage-usage indicator consumes this endpoint instead of a hardcoded value.
12. FR12 — The application's browser-facing branding text (page title, sidebar header, nav brand) is updated from "Perene Tech Videos" to the "PereneArchive" product identity, per the [Branding](#branding) rules below, without changing the underlying `.NET` project/namespace/solution names (`WebApp`, `WebApp.Client`, `video-manager.slnx` stay as-is).

## Branding

- **Company vs. product**: "Perene Tech" is the company; "PereneArchive" is this product. Every browser-facing brand mark (page `<title>`, sidebar header, top nav brand, any future "about"/footer text) names the product as **PereneArchive**, not "Perene Tech Videos." Where a company byline is still useful (e.g. a small "by Perene Tech" sub-line), it must stay visually secondary to the PereneArchive product mark, mirroring the existing brand-mark/sub-line pairing already used in `NavMenu.razor` (`<strong>`/`<small>`).
- **Product icon**: the PereneArchive brand mark uses Bootstrap Icons' `bi-archive` glyph (replacing the current `bi-aspect-ratio` mark in `NavMenu.razor`), rendered at `currentColor` per the existing icon-button contract — no custom SVG or emoji.
- **Brand typography**: any rendering of the brand name "PereneArchive" (or "Perene Tech"), anywhere in the UI — nav brand, sidebar header, page title text rendered on-screen, future marketing/about copy — uses the **Zilla Slab** heading font already declared as `--font-family-title` in `app.css`/the design guide. Never render the brand name in the Montserrat body font.

## Non-Functional Requirements

- Preserve the existing security/privacy boundary: no physical or root-relative filesystem path is ever sent to the browser, including from the new storage-usage endpoint (it returns only aggregate byte counts, never paths or filenames).
- The new sidebar and placeholder pages must reuse Bootstrap 5.3.8 components/utilities and `app.css` tokens exclusively; no new frontend framework, CSS framework, or icon library is introduced (per the existing Design System contract).
- Placeholder sections must not perform any FFmpeg invocation, filesystem enumeration outside `/home/PereneArchive`, or new bind mounts — they are presentation-only stubs.
- The storage-usage endpoint must not read or enumerate file contents/names — only aggregate free/used/total space for the mounted volume — to avoid a new data-exposure surface.
- All existing xUnit test coverage for `VideoLibraryOptions`, `VideoCutOptions`, `VideoCompositionOptions`, and the video/cut/composition endpoints must continue to pass unmodified in behavior (only their configured paths change, not their validation logic).
- Sidebar and placeholder components should follow small, focused component boundaries consistent with existing `WebApp.Client/Components/` conventions (one component per concern) rather than one large monolithic layout file.

## Out of Scope

- Any real functionality for Photos, Music, Documents, Downloads, Shared, Family, History, or Trash beyond a static placeholder view (file browsing, upload, playback, sharing, permissions, trash/restore logic).
- Automated migration tooling or scripts to move files into the new layout — the operator performs this manually before implementation, per explicit instruction.
- Authentication, multi-user accounts, or sharing/permissions systems implied by "Family"/"Shared" sidebar entries.
- Renaming the `.NET` solution, project files, namespaces, or Docker image/service names away from `WebApp`/`video-manager`.
- Any change to the FFmpeg pipelines' processing logic (thumbnailing, cut export, composition) beyond the path relocation already covered by FR2/FR3.
- A general-purpose file browser, drag-and-drop upload, or folder-tree UI for the mockup sections.

## Open Questions

None — the prior two open items are resolved: the mobile sidebar behavior is specified in FR10 (horizontal scrollable top tab bar, Yandex Disk mobile pattern), and exact "coming soon" placeholder copy is intentionally left to implementation judgment (not a blocking decision, since this project has no production audience to get microcopy wrong for).
