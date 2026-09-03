# Validation: Save Cut (A/B Video Export)

## Table of Contents

- [Validation: Save Cut (A/B Video Export)](#validation-save-cut-ab-video-export)
  - [Acceptance Criteria](#acceptance-criteria)
  - [Test Cases](#test-cases)
  - [Manual Verification](#manual-verification)
  - [Definition of Done](#definition-of-done)
  - [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | `MediaPlayerControls.razor`'s "Loop points" button group renders a `bi-download` icon button with `title`/`aria-label`/`data-bs-title` "Save Cut"; it is disabled whenever `State.HasValidAbRange` is false and enabled once both markers form a valid range. |
| FR2 | Clicking the enabled button issues `POST /api/videos/{id}/cuts` with a JSON body carrying `MarkerA`/`MarkerB` as seconds. |
| FR3 | The endpoint returns `404` for an unresolvable/expired id, `400` for `start >= end`, `start < 0`, or `end` beyond the re-probed duration, and `202 Accepted` with a job id for a valid request; a valid request does not perform the cut synchronously (the HTTP response returns before ffmpeg finishes). |
| FR4 | A cut job enqueued via the endpoint is later dequeued and processed by a dedicated `BackgroundService`, observable via structured log entries mirroring `ThumbnailBackgroundWorker`'s start/success/failure logging shape. |
| FR5 | The produced file's video and audio codecs/resolution/bitrate match the source (verifiable with `ffprobe` comparing source vs. output streams); ffmpeg is invoked only through `ProcessStartInfo`/`ArgumentList`, never a shell string (verified the same way `FfmpegThumbnailGeneratorTests` verifies argument-list separation for the thumbnail generator). |
| FR6 | Given a source `"Jennifer White - Clip One.mp4"` with no prior cuts, the first cut is named `"Jennifer White 0001.mp4"`; a second cut from a different `"Jennifer White - Clip Two.mp4"` source is named `"Jennifer White 0002.mp4"`; a first cut from `"Maria Rodriguez - X.mp4"` is named `"Maria Rodriguez 0001.mp4"`, all written under `<VIDEO_ROOT>/Cuts`. |
| FR7 | Killing/failing a cut mid-generation (simulated via a non-zero ffmpeg exit code or cancellation) leaves no file at the final destination name and no `.tmp` leftovers after the worker's cleanup path runs. |
| FR8 | `GET /api/cuts` returns `VideoItemDto`-shaped JSON for every file currently in `Cuts/`, addressed by opaque IDs; the response body never contains the configured video root or Cuts root's physical path (checked the same way `VideoEndpointsTests.Scan_returns_only_the_browser_safe_contract` checks the main library). |
| FR9 | `GET /api/cuts/{id}/stream` serves the resolved cut with range support (`Accept-Ranges`, partial-content `206` on a `Range` request) and returns `404` for an id that isn't in the current cuts snapshot. |
| FR10 | `Home.razor` shows a "Cuts" section below `<VerticalVideoEditor>` that lists `GET /api/cuts` results on load, and a newly enqueued cut appears there within the existing 2-second poll cadence without a manual page refresh. |
| FR11 | Clicking a card in the Cuts section sets it as the editor's `Selected` video, the `<video>` element's `src` becomes `/api/cuts/{id}/stream`, and A/B markers/crop/saturation controls all function against it exactly as they do for a library video. |

## Test Cases

**Unit tests** (`WebApp.Tests/Services/`, xUnit, mirroring existing `Ffmpeg*GeneratorTests`/`ThumbnailJobQueueTests`/`ThumbnailCoordinatorTests` conventions):

- `FfmpegCutGeneratorTests`: `BuildArguments` keeps source/destination/start/end as separate `ArgumentList` entries (metacharacter-laden fake paths, same style as `FfmpegThumbnailGeneratorTests.Arguments_keep_source_and_destination_as_separate_list_entries`); asserts `-c copy` is present for both streams; asserts source-freshness re-check rejects a job whose captured size/last-write time no longer matches disk.
- `CutNamingServiceTests`: empty `Cuts/` directory yields `0001` for a fresh prefix; an existing `"Jennifer White 0003.mp4"` yields `0004` for the next same-prefix cut; a differently prefixed existing file doesn't affect a new prefix's counter; a single-word source name uses that one word as the prefix; prefix matching is case-insensitive (`"jennifer white 0001.mp4"` still increments `"Jennifer White"` cuts).
- `CutJobQueueTests`: `TryEnqueue`/`DequeueAsync` FIFO ordering, mirroring `ThumbnailJobQueueTests`' channel-behavior coverage.
- `VideoCutServiceTests`: scans a temporary directory the same way `VideoLibraryServiceTests` verifies `VideoLibraryService` (extension allowlist, symlink skip, canonical containment via `IsWithinRoot`), and confirms `TryResolve` rejects IDs from a stale/replaced snapshot.
- `VideoCutOptionsTests` (`WebApp.Tests/Configuration/`): mirrors `ThumbnailCacheOptionsTests` for `HasConfiguredPath`/`HasAbsolutePath`/`DirectoryExists`/`DirectoryIsWritable`.

**Integration tests** (`WebApp.Tests/Endpoints/`, `WebApplicationFactory`-based, mirroring `VideoEndpointsTests`):

- `CutEndpointsTests`: `POST /api/videos/{id}/cuts` end-to-end against a `VideoManagerFactory`-style fixture with a real short test video fixture and a real `ffmpeg` binary in the test container (already available per the `Dockerfile`) — enqueue a valid cut, poll `GET /api/cuts` until it appears (bounded retry loop, same spirit as the existing thumbnail-pipeline tests that wait for background work), then assert the response never leaks the physical Cuts path and `GET /api/cuts/{id}/stream` serves range-enabled content.
- Same-prefix collision case: enqueue two cuts from two different source fixtures sharing the same first two words, assert they land on sequential numbers with no filename collision or dropped job.
- Invalid input cases: `start >= end`, `end` past duration, unresolvable id — assert `400`/`404` without any file appearing under `Cuts/`.

⚠️ TODO: no existing test fixture directory of short sample `.mp4` files exists yet for exercising real `ffmpeg` cut/stream-copy behavior end-to-end (the thumbnail tests appear to use small synthetic/generated inputs — confirm the exact fixture approach used by `FfmpegThumbnailGeneratorTests`/`FfprobeDurationProbeTests` and reuse it rather than adding a new binary test asset).

## Manual Verification

1. Ensure `${VIDEO_ROOT}/Cuts` exists on the host: `mkdir -p "$VIDEO_ROOT/Cuts"` (one-time, before first run with this feature).
2. `make docker-run-bg`, then `make docker-logs` in a second terminal to watch for cut-related log lines.
3. Open the app, click Scan, select a video with enough duration to mark two distinct points.
4. Set point A and point B via the existing loop-point buttons; confirm the new "Save Cut" (`bi-download`) button becomes enabled only once both are set validly.
5. Click "Save Cut"; confirm the button shows a pending/disabled state and no duplicate job can be triggered by clicking again immediately.
6. Within a few seconds, confirm a new "Cuts" section appears below the editor with the new cut card, without manually refreshing or rescanning.
7. On the host, confirm `${VIDEO_ROOT}/Cuts/<First Two Words> 0001.mp4` exists, and `ffprobe` shows matching resolution/codec/audio properties to the source.
8. Click the new cut card; confirm it loads into the same vertical editor in the center of the page, streams, and that A/B/crop/saturation controls work against it.
9. Repeat steps 3–7 with a second, differently-named source video and confirm it starts its own `0001`; repeat with a third source sharing the first video's first two words and confirm it becomes `0002`.
10. Stop the app (`make docker-down`) and restart (`make docker-run-bg`); confirm the Cuts section still lists previously produced cuts and a new cut from the same prefix continues the sequence correctly (no counter reset, no collision).
11. `make test` — full suite passes in the isolated Docker Compose test stack.

## Definition of Done

- `Requirements.md`, `Plan.md`, and this `Validation.md` are complete and internally consistent in this spec folder.
- All existing tests still pass under `make test`, and the new unit/integration tests above are added and passing.
- UI changes (`MediaPlayerControls.razor`, `VerticalVideoEditor.razor`, `VideoGrid.razor`, `Home.razor`) cover loading/empty/error/pending states for the Cuts section and Save Cut button, follow the Bootstrap Icons/tooltip accessibility contract in `AGENTS.md`'s Design System section (visible focus, accessible name, 40×40 target, tooltip fallback), and the extraction of `VideoGrid.razor` leaves `VideoLibrary.razor`'s existing hover-preview/selection behavior unchanged.
- `AGENTS.md`'s FFmpeg constraint bullet and Repository Map/Architecture Summary are updated to reflect the new, narrowly scoped cut pipeline (via the `init-agent` skill, per this project's documented workflow) once implementation lands.
- `docker-compose.yml`/`.env.example` changes are documented, and the manual verification steps above (including the one-time host `Cuts` directory creation) are confirmed to work from a clean `make docker-reset` state.
- No physical or root-relative filesystem path is ever observable in any HTTP response, per the existing opaque-ID boundary tests.

## Rollback Plan

- The feature is additive: removing the "Save Cut" button (`MediaPlayerControls.razor`) and the "Cuts" section (`Home.razor`) fully hides the feature from the UI without touching the existing library scan/edit flow.
- To fully disable server-side: remove the `AddHostedService<CutBackgroundWorker>()` registration and the `MapCutEndpoints()` call in `Program.cs`/`Endpoints` composition — `POST /api/videos/{id}/cuts` and `GET /api/cuts*` then 404, and no new jobs are processed.
- To revert the mount change: remove the second `Cuts` bind mount and `VideoCut__Path` environment variable from `docker-compose.yml`; `VideoCutOptions` validation failing fast on startup (`ValidateOnStart`) makes a misconfigured/missing mount an immediate, visible startup failure rather than a silent runtime one.
- Any already-produced files under `<VIDEO_ROOT>/Cuts` are ordinary `.mp4` files on the host filesystem and can be deleted manually if the feature is rolled back; no database or migration state exists to unwind.
