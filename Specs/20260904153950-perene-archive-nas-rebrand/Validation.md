# Validation: PereneArchive NAS Rebrand (Sidebar + Auto-Configured Storage Root)

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | Running `make docker-run` with no `.env` file present (or a `.env` without `VIDEO_ROOT`) starts the app successfully; `docker compose config` shows the `webapp` service's video bind mount source resolved to `/home/PereneArchive/videos`. |
| FR2 | With `VIDEO_ROOT` unset, `docker compose config` shows the read-only video bind mount source as `/home/PereneArchive/videos` mapped to the same internal target `VideoLibraryOptions` already expects. |
| FR3 | With `VIDEO_ROOT` unset, `docker compose config` shows the Cuts bind mount source as `/home/PereneArchive/videos/Cuts` and the VideoComposition bind mount source as `/home/PereneArchive/videos/VideoComposition`, both read-write. |
| FR4 | `.env.example` documents `VIDEO_ROOT` as optional with a `/home/PereneArchive` default and describes the `videos/{Cuts,VideoComposition}` subfolder layout; no instruction to manually `mkdir` folders at the old flat root remains. |
| FR5 | After the operator manually populates `/home/PereneArchive/videos` (including pre-existing `Cuts/`/`VideoComposition/` content), scanning the library lists those videos, streaming/thumbnailing works, saving a new cut succeeds and appears under Cuts, and creating a new composition from 2+ cuts succeeds and appears under Video Compositions. |
| FR6 | The rendered page shows a persistent left sidebar with a brand header, 9 icon+label nav entries (Videos, Photos, Music, Documents, Downloads, Shared, Family, History, Trash), and a storage-usage indicator, styled with Bootstrap Icons and existing `app.css` tokens (verified visually in both dark and light theme). |
| FR7 | Clicking/activating "Videos" in the sidebar shows the unchanged Library/Cuts/Video Compositions experience at `/`, with scan, cut-save, and composition-create still functioning exactly as before this change. |
| FR8 | Clicking/activating each of the 8 other sidebar entries navigates to a distinct route rendering a "coming soon" empty state (icon + heading + message) with no network requests fired for real data (verified via browser dev tools Network tab showing no XHR/fetch beyond the initial page/asset load). |
| FR9 | The active sidebar entry is visually distinguished (e.g. `active` class/gold highlight) matching the current route; all 9 entries are reachable via Tab/Enter keyboard navigation and each exposes an accessible name to a screen reader (verified via browser accessibility tree inspection). |
| FR10 | Resizing the browser below the `md` breakpoint (or loading on a mobile-width viewport) replaces the left sidebar rail with a single horizontally-scrollable top tab bar containing the same 9 destinations, matching the Yandex Disk mobile reference (no wrapping, swipe/scroll left-right, active tab highlighted). |
| FR11 | `GET /api/storage/usage` returns a 200 JSON body with `usedBytes`/`totalBytes` reflecting the real size/free space of the mounted `/home/PereneArchive` volume (cross-checked against `df -h /home/PereneArchive` on the host), and the sidebar's storage meter renders a proportional bar from that response. |
| FR12 | The browser tab title, sidebar brand header, and top nav brand text read "PereneArchive" (with "Perene Tech" only as a visually-secondary byline, if present) instead of "Perene Tech Videos"; the brand mark icon is `bi-archive`; every on-screen rendering of the brand name uses the Zilla Slab (`--font-family-title`) font, never Montserrat; `.NET` project files, namespaces, and `video-manager.slnx` remain unchanged (`git diff --stat` shows no `.csproj`/`.slnx`/namespace renames). |

## Test Cases

**Unit tests (xUnit, `WebApp.Tests/`, run via `make test`):**

- `WebApp.Tests/Configuration/`: existing `VideoLibraryOptions`/`VideoCutOptions`/`VideoCompositionOptions` validation tests continue to pass unmodified (they test path-shape validation, not specific path values, so no test changes are expected — re-run as a regression check).
- `WebApp.Tests/Services/StorageUsageServiceTests.cs` (new): given a configured/valid mount path, `StorageUsageService` returns non-negative `UsedBytes`/`TotalBytes` with `UsedBytes <= TotalBytes`; given an invalid/inaccessible path, it returns a safe fallback (e.g. zeros or a documented failure result) rather than throwing an unhandled exception past the endpoint boundary.
- `WebApp.Tests/Endpoints/StorageEndpointsTests.cs` (new): using `WebApplicationFactory` (matching the existing pattern in `WebApp.Tests/Endpoints/`), `GET /api/storage/usage` returns 200 with a JSON body containing only `usedBytes`/`totalBytes` fields (asserting no path/filename strings appear in the response body, protecting the "no physical path exposure" NFR).
- `WebApp.Tests/Client/` (new or extended): `ComingSoonSection` renders the given `Icon`/`Title`/`Message` parameters without making any HTTP calls (component test using existing Blazor component-testing conventions in this folder, if present; otherwise a plain constructor/parameter-binding test consistent with existing `Client` test style).

**Integration tests:**

- ⚠️ TODO: An end-to-end Docker Compose run (`make docker-run` against a real `/home/PereneArchive` test fixture directory) is the only way to verify the bind-mount path resolution (FR1–FR3) and the manual-migration workflow (FR5) end-to-end; this is covered under Manual Verification below rather than an automated integration test, consistent with this repo's existing reliance on `WebApplicationFactory` for endpoint-level (not full-container) integration coverage.

## Manual Verification

1. On the host, confirm `/home/PereneArchive/videos`, `/home/PereneArchive/videos/Cuts`, and `/home/PereneArchive/videos/VideoComposition` exist and contain the operator's manually-moved video/cut/composition files (precondition, performed outside this spec's automation).
2. From the repo root, ensure no `.env` file exists (or remove `VIDEO_ROOT` from an existing one), then run `make docker-run`.
3. Confirm the container starts without a `VideoLibraryOptions`/`VideoCutOptions`/`VideoCompositionOptions` startup validation failure in `make docker-logs`.
4. Open the app in a browser at the configured `WEBAPP_PORT` (default 8080). Confirm the sidebar renders with all 9 entries, the storage meter shows a non-zero usage bar, and the page title/brand read "PereneArchive" with a `bi-archive` icon rendered in the Zilla Slab heading font (inspect via devtools computed styles if not visually obvious), with "Perene Tech" (if shown at all) appearing only as a smaller, secondary byline.
5. Click "Videos"; click Scan; confirm the previously-moved videos appear with thumbnails, confirm an existing cut plays back under Cuts, and confirm an existing composition plays back under Video Compositions.
6. Select a video, set A/B points, and save a new cut; confirm it appears under Cuts and streams correctly from the new `/home/PereneArchive/videos/Cuts` location.
7. Select 2+ cuts and create a composition; confirm it completes and streams correctly from the new `/home/PereneArchive/videos/VideoComposition` location.
8. Click each of the 8 other sidebar entries (Photos, Music, Documents, Downloads, Shared, Family, History, Trash) in turn; confirm each shows a distinct "coming soon" empty state, the sidebar highlights the correct active entry, and the browser Network tab shows no data-fetch requests for those routes beyond static assets.
9. Toggle dark/light theme via the existing `ThemeToggle`; confirm the sidebar, storage meter, and placeholder cards remain readable and on-token in both themes.
10. Resize the viewport to a narrow/mobile width (below the `md` breakpoint); confirm the left sidebar rail is replaced by a horizontally-scrollable top tab bar with the same 9 destinations (Yandex Disk mobile pattern), that it scrolls smoothly left-right without wrapping, and that the active tab and top nav/main content remain usable.
11. Run `make test` and confirm all existing and new tests pass.

## Definition of Done

- Requirements, Plan, and Validation docs in this spec folder are complete and internally consistent.
- All existing tests still pass; new tests listed above are added and passing.
- `docker-compose.yml` and `.env.example` reflect the `/home/PereneArchive` default and nested `videos/{Cuts,VideoComposition}` layout.
- Sidebar, `ComingSoonSection`, all 8 placeholder pages, `StorageMeter`, and the storage-usage service/endpoint/DTO are implemented with responsive, empty, loading, and error states covered per the Plan, and use only Bootstrap/`app.css` tokens and Bootstrap Icons.
- Branding text updated to PereneArchive without touching `.NET` project/namespace/solution identifiers.
- `AGENTS.md` is updated (via the `init-agent` workflow) to reflect the new storage-root default, the sidebar/placeholder architecture, and the new storage-usage service/endpoint, once implemented.
- The run/build workflow (`make docker-run`, `make test`) is verified working end-to-end against the new default path.

## Rollback Plan

- Revert `docker-compose.yml` and `.env.example` to restore the required `VIDEO_ROOT` (`${VIDEO_ROOT:?...}`) and the old flat `${VIDEO_ROOT}`/`${VIDEO_ROOT}/Cuts`/`${VIDEO_ROOT}/VideoComposition` bind-mount sources — this immediately restores the pre-spec configuration surface with no data loss, since the operator's files remain wherever they were last moved.
- The sidebar, placeholder pages, `StorageMeter`, and storage-usage endpoint are additive, isolated files (`Sidebar.razor`, `ComingSoonSection.razor`, the 8 placeholder pages, `StorageEndpoints.cs`, `StorageUsageService.cs`, `StorageUsageDto.cs`) plus small edits to `MainLayout.razor`/`NavMenu.razor`/`App.razor`/`Program.cs`/`Home.razor` header copy; reverting the corresponding commit(s) fully restores the prior single-page layout and "Perene Tech Videos" branding with no server-side state or migration to unwind.
- No database, persistent job state, or on-disk schema is introduced by this spec, so no data migration rollback is needed beyond the compose/env path revert above.
