# Validation: Local Vertical Video Manager

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | With no `.env` or with `VIDEO_ROOT` empty, `make docker-run` exits before starting the web service and identifies `VIDEO_ROOT` as required. |
| FR2 | `docker inspect` reports `/videos` as a read-only bind mount sourced from the configured host directory, the container has `VideoLibrary__Path=/videos`, and no committed file contains the user's actual path. |
| FR3 | Compose reports the published endpoint on `127.0.0.1`, local requests using `localhost` or `127.0.0.1` succeed, and a request with an unapproved `Host` header is rejected. |
| FR4 | Each missing, relative, nonexistent, or unreadable `VideoLibrary:Path` configuration prevents application startup and produces a configuration-focused error without logging library filenames. |
| FR5 | Opening the root page performs no scan request, displays an unscanned state, and issues exactly one scan request when Scan is activated; filesystem changes appear only after a later Rescan. |
| FR6 | A manual scan includes case-insensitive `.mp4`, `.webm`, `.mov`, and `.m4v` files from the root and nested directories and excludes other extensions. |
| FR7 | Scanning neither descends through directory symbolic links/reparse points nor returns linked files, and no canonical candidate outside the configured root reaches the snapshot. |
| FR8 | Scan JSON and rendered rows contain only opaque ID, filename, extension, and size; neither response bodies nor normal logs contain host paths or root-relative directory names. |
| FR9 | A successful rescan replaces all prior IDs; empty scans show the empty state; failed scans show an error, clear the UI list, and make all IDs from the preceding snapshot return `404`. |
| FR10 | Selecting a row marks it semantically and visually, loads its stream URL without document navigation, and initializes both position values to `50`. |
| FR11 | The stream endpoint returns `404` for an unknown, malformed, stale, deleted, or unreadable ID and has no route/query/body input that accepts a filesystem path. |
| FR12 | A normal stream request does not load the full file into managed memory, and a valid byte range returns `206 Partial Content`, correct range headers, and only the requested bytes; seeking works in the browser. |
| FR13 | The selected media viewport remains 9:16 at desktop and handheld sizes, hides overflow, fills with `object-fit: cover`, begins at `50% 50%`, and retains usable native controls. |
| FR14 | Mouse, touch, and pen dragging updates C# position state in the WebAssembly client with no movement-time HTTP requests, and neither coordinate can leave `0..100`. |
| FR15 | Dragging changes only cropped axes, suppresses unintended page scroll/text selection on the drag surface, preserves surrounding layout dimensions, and always ends on release, cancel, leave, or capture loss. |
| FR16 | Activating Reset immediately renders `object-position: 50% 50%` and sets both C# coordinates to `50`. |
| FR17 | After reframing one video, selecting another starts centered; returning to the first also starts centered because no framing values are persisted. |
| FR18 | A supported-extension fixture with an unsupported codec remains listed; selection produces an accessible inline playback error and does not disable scan, selection, or Reset. |
| FR19 | Desktop and handheld checks cover unscanned, scanning, empty, populated, selected, scan-error, and playback-error states with no overlap or clipped controls and with keyboard-visible focus. |
| FR20 | `.env.example` contains only safe placeholders/documentation for `VIDEO_ROOT` and `WEBAPP_PORT`; a created `.env` remains absent from `git status` and the Docker build context. |

## Test Cases

**Unit tests:**

- `WebApp.Tests/Services/VideoLibraryServiceTests.cs` using the repository's xUnit setup: discover all four extensions case-insensitively at root and nested levels; ignore unsupported files; return only filename/extension/size; generate opaque IDs; and replace IDs/snapshot on rescan.
- `WebApp.Tests/Services/VideoLibraryServiceTests.cs`: build temporary sibling-prefix directories (for example `root` and `root-other`) and assert canonical containment never accepts the sibling; on the Linux Docker test runner, create file and directory symlinks and assert both are skipped.
- `WebApp.Tests/Services/VideoLibraryServiceTests.cs`: verify empty directory, inaccessible/deleted entry handling, cancellation, atomic clearing after a failed scan, and concurrent resolve/rescan behavior. Tests must use temporary fixtures and never the real `VIDEO_ROOT`.
- `WebApp.Tests/Client/VideoFrameStateTests.cs`: verify centered initialization, positive/negative drag direction, overflow-aware percentage conversion, axis locking when overflow is zero, clamping at both bounds, Reset, and selection-change reset.

**Integration tests:**

- `WebApp.Tests/Endpoints/VideoEndpointsTests.cs` with xUnit and `WebApplicationFactory<Program>`: override typed options with a temporary root, call `POST /api/videos/scan`, assert the exact safe DTO shape, and verify no physical or relative directory path occurs in JSON.
- `WebApp.Tests/Endpoints/VideoEndpointsTests.cs`: request a generated fixture through its opaque ID with `Range: bytes=2-5`; assert `206`, `Accept-Ranges`, `Content-Range`, media type, four response bytes, and byte equality. Also assert full requests return the fixture without application-side base64/JSON buffering.
- `WebApp.Tests/Endpoints/VideoEndpointsTests.cs`: assert `404` for malformed/unknown IDs, IDs invalidated by rescan, and files deleted after scan; assert a path-like route value cannot select a host file.
- `WebApp.Tests/Endpoints/VideoEndpointsTests.cs`: force a scan exception after a successful scan, assert the safe problem response contains no filesystem path, then assert the previous ID returns `404`.
- Run the existing repository workflow `make test`; the test Compose stack must use its own temporary configuration and must not mount or inspect the user's video library.
- No browser automation framework currently exists. Pointer capture, native playback/seek, responsive rendering, and codec failure are covered by the manual checks below rather than adding another frontend dependency in this slice.

## Manual Verification

1. From the repository root, create the ignored local configuration from `.env.example`, set `VIDEO_ROOT` to an absolute test directory containing nested supported videos, an unsupported file, and no sensitive production-only media, then confirm `git status --short` does not show `.env`.
2. Temporarily move `.env` aside and run `docker compose config`; confirm Compose fails with a clear missing-`VIDEO_ROOT` error. Restore `.env` before continuing.
3. Run `docker compose config` and inspect the resolved model. Confirm the host path appears only as the bind source, `/videos` is the target, the mount is read-only, the application environment contains `VideoLibrary__Path=/videos`, and the published port uses `127.0.0.1`.
4. Run `make docker-run`, then open `http://localhost:8080` (or the configured `WEBAPP_PORT`). Confirm the initial page is unscanned and no video filenames appear until Scan is activated.
5. Activate Scan. Confirm root and nested `.mp4`, `.webm`, `.mov`, and `.m4v` files appear with filename, extension, and formatted size; confirm unsupported files and directory names do not appear.
6. Add or remove a supported file in the host test directory. Confirm the UI does not change automatically, activate Rescan, and confirm the new snapshot replaces the old list and clears any selection.
7. Select a playable video. Confirm there is no full-page reload, the preview is 9:16, the video covers the viewport, native play/pause controls work, and browser seeking works on a large fixture.
8. In browser developer tools, inspect a media request and confirm the URL contains only an opaque ID. Seek and confirm a range request receives `206 Partial Content` and appropriate `Content-Range`/`Accept-Ranges` headers.
9. Drag a landscape and a portrait video using a mouse. Confirm the visibly cropped axis follows the drag, non-cropped axes remain stable, the page does not select text or scroll, position remains bounded, and releasing outside/cancelling does not leave dragging active.
10. Repeat the drag check with touch and, when available, pen input at a handheld viewport. Confirm the interface stacks without overlap, the native controls and Reset remain reachable, and no pointer-movement network requests occur.
11. Reframe a video, activate Reset, and confirm it returns to center. Reframe again, select another video, and confirm the new selection is centered; return to the first and confirm it is also centered.
12. Select a supported-extension file encoded with a browser-unsupported codec. Confirm it stays listed and produces a readable inline error while Scan/Rescan, other selections, and Reset still work.
13. Scan an empty test directory and confirm the empty state. Then make the mounted directory unreadable or unavailable in a controlled test setup, rescan, and confirm a path-free error state and that previously issued stream IDs no longer work.
14. Add file and directory symlinks that target content outside the test root, rescan, and confirm neither linked content nor its metadata appears. Directly request a random and a stale stream ID and confirm both return `404`.
15. Send a local HTTP request with an unapproved `Host` header and confirm host filtering rejects it. From another machine on the LAN, confirm the application port is not reachable.
16. Run `make test` and confirm all xUnit unit and integration tests pass in the isolated test stack.

## Definition of Done

- `Requirements.md`, `Plan.md`, and `Validation.md` remain synchronized in this spec folder, with every FR mapped to an acceptance criterion.
- The app starts only through the documented Docker Compose workflow with a mandatory private `.env`, loopback port publication, and a read-only configured video mount.
- All existing tests still pass, and the new behavior has xUnit unit/integration coverage matching this repository's conventions.
- Scan, stream authorization, canonical containment, symlink avoidance, safe error responses, range processing, and invalidated snapshot IDs are covered by automated tests.
- The Interactive WebAssembly UI covers responsive unscanned, scanning, empty, populated, selected, scan-error, and playback-error states with keyboard focus and status semantics.
- Mouse, touch, pen, native media controls, browser seeking, unsupported codecs, desktop layout, and handheld layout have been manually verified.
- No browser response, normal log entry, committed configuration, or source file exposes the user's actual video root or descendant directory paths.
- Vendor-specific configuration, range response, Compose interpolation, bind-mount, and network decisions remain supported by the official documentation recorded in `Plan.md`.
- The new test-only dependency is pinned to the runtime's ASP.NET Core patch version, and `make test` plus `make docker-run` are verified.

## Rollback Plan

- Revert the video endpoint/service registrations and client manager components, then restore the template root component and layout files listed in `Plan.md`.
- Revert `docker-compose.yml` to remove the `/videos` bind mount and `VideoLibrary__Path`; recreating the Compose service removes the container's access to the host directory immediately.
- Remove `.env.example` from version control. The user's ignored `.env` remains local and can be deleted manually if no longer needed; it contains configuration only, not application data.
- Remove `Microsoft.AspNetCore.Mvc.Testing` and the feature tests if the endpoint feature is rolled back.
- No database, migration, persisted framing state, generated thumbnails, or modified source videos exist, so rollback requires no data conversion or recovery.
