# Validation: Video Composition (Multi-Clip FFmpeg Concat)

## Table of Contents

- [Validation: Video Composition (Multi-Clip FFmpeg Concat)](#validation-video-composition-multi-clip-ffmpeg-concat)
  - [Acceptance Criteria](#acceptance-criteria)
  - [Test Cases](#test-cases)
  - [Manual Verification](#manual-verification)
  - [Definition of Done](#definition-of-done)
  - [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | The Cuts `VideoGrid.razor` instance renders a checkbox pinned to each card's top corner; clicking it toggles that card's selection without invoking `OnSelect` (the editor selection does not change). |
| FR2 | Selection state lives only in `Home.razor`'s in-memory `_selectedCutIds`; the main Library grid instance never receives `SelectionEnabled`, so it renders no checkboxes. |
| FR3 | A "N clips selected" indicator updates live as checkboxes toggle; "Create Composition" is disabled whenever `_selectedCutIds.Count < 2` and enabled otherwise. |
| FR4 | Clicking an enabled "Create Composition" posts `{ videoIds: [...] }` (only opaque cut IDs) to `POST /api/compositions`, clears `_selectedCutIds`, and shows the returned job id in a pending state. |
| FR5 | `POST /api/compositions` returns `400` for fewer than 2 resolvable ids or any unresolvable id, `202 Accepted` with a job id for a valid request, and does not perform any FFmpeg work before returning (verified by timing/mocking the generator in a test double). |
| FR6 | Given selected cuts named out of filename order (e.g. submitted as `["...0002...","...0001..."]`), the background worker's resolved `OrderedSources` list is sorted ordinally by `Name` before any probing/generation happens. |
| FR7 | `IVideoCompositionProbe` returns width, height, duration, frame rate, video codec, audio codec, sample rate, and channel count for a real test fixture file (verified against a known-good `ffprobe` reading of the same file); a probe failure (missing/corrupt input) fails the job with stage `Probe` and no generation step runs. |
| FR8 | Given three probed inputs of different resolutions, `CompositionFormatPlanner` selects the smallest-pixel-count input's width/height as the canvas, and produced normalized clips are never wider/taller than that canvas (a smaller input is padded, not upscaled — verified by asserting the planner's chosen filter never scales a dimension above its source value). |
| FR9 | `CompositionFormatPlanner` returns the same fixed target audio sample rate/channel count regardless of the mix of probed source sample rates/channels. |
| FR10 | `CompositionFadePlanner`, given a 3-clip ordered list, returns: fade-out-only for clip 1, fade-in-and-fade-out for clip 2, fade-in-only for clip 3; given a clip shorter than `2 * TransitionDuration`, returns a fade duration clamped to at most half that clip's duration (verified with a sub-1-second synthetic duration). |
| FR11 | The generator writes one normalized temporary file per input clip (verifiable by inspecting the working directory mid-run in a controlled test), then a single concat invocation over those temp files produces the final temp output; the final published file's total duration is approximately the sum of the input durations (within a small tolerance for fade/frame-rounding). |
| FR12 | All `ffmpeg`/`ffprobe` invocations in the composition pipeline pass arguments only via `ArgumentList` (verified the same way `FfmpegCutGeneratorTests`/`FfmpegThumbnailGeneratorTests` assert argument-list separation for metacharacter-laden fake paths). |
| FR13 | The final output only appears at its published name in `<VideoComposition__Path>` after a successful `ffmpeg` exit and a non-empty-file check; killing/failing the process mid-run leaves no file at the final destination name. |
| FR14 | After a successful run, no `.tmp`/per-clip temporary file remains in `<VideoComposition__Path>`; after a simulated failure/cancellation, the same is true (best-effort cleanup verified). |
| FR15 | A forced failure at each stage (bad input for probe, non-zero ffmpeg exit for normalize, non-zero ffmpeg exit for concat) is logged with the failing stage identified and a redacted diagnostic (no physical path present in the log message), and results in a `Failed` job with no published file. |
| FR16 | Given ordered sources `"Jennifer White 0001.mp4"`, `"Jennifer White 0002.mp4"`, the published file is named `"Jennifer White Composition 0001.mp4"`; a second composition from the same prefix's sources is named `"...Composition 0002.mp4"`; a composition whose first ordered clip is `"Mike Jones 0003.mp4"` (even if other selected clips have a different prefix) is named `"Mike Jones Composition 0001.mp4"`. |
| FR17 | `GET /api/compositions/jobs` reflects a job's `Pending` state immediately after enqueue, `Processing` once dequeued, and `Completed`/`Failed` with the terminal outcome once the worker finishes, without requiring `GET /api/compositions` to be called. |
| FR18 | `GET /api/compositions`, `GET /api/compositions/{id}/stream`, `.../thumbnail`, and `.../preview` behave identically in shape/contract to the equivalent `/api/cuts/*` endpoints (opaque IDs only, range-enabled streaming, 404 on unresolvable id, no physical path ever in the response); a completed composition's card in `VideoGrid.razor` shows a thumbnail and hover preview the same way a library video's or cut's does, and opening it in `VerticalVideoEditor` supports the same play/pause, seek, volume, speed, and fill-tab controls as any other video — no composition-specific playback code path exists. |
| FR19 | `Home.razor` renders a "Video Compositions" section below "Cuts" showing job-status entries for any non-terminal job and a `VideoGrid` of completed compositions; both are kept current by polling while any job is `Pending`/`Processing`, and polling stops once none remain. |
| FR20 | After a job reaches `Completed`, the next `GET /api/compositions` poll includes the new video without requiring a manual rescan or app restart. |

## Test Cases

**Unit tests** (`WebApp.Tests/Services/`, xUnit, mirroring existing `Ffmpeg*GeneratorTests`/`CutNamingServiceTests`/`ThumbnailJobQueueTests` conventions):

- `FfmpegCompositionGeneratorTests`: per-clip normalize `ArgumentList` keeps source/temp paths as separate entries (metacharacter-laden fake paths, same style as `FfmpegCutGeneratorTests`); asserts scale/pad/fps/fade filter strings are built correctly for first/middle/last clip positions; asserts the concat step uses `-c copy` only after normalization; asserts cleanup removes all per-clip temp files on both success and simulated failure.
- `CompositionFormatPlannerTests`: smallest-pixel-count input wins the canvas across several width/height combinations (including a tie, resolved by first-in-order); target frame rate is the `Min` of inputs; audio format is always the fixed target regardless of input mix.
- `CompositionFadePlannerTests`: first/middle/last fade-plan shapes for a 2-clip and a 3+-clip list; clamping behavior for a clip shorter than `2 * TransitionDuration`; a clip of exactly `2 * TransitionDuration` gets unclamped full-length fades.
- `CompositionNamingServiceTests`: mirrors `CutNamingServiceTests` but asserts the `" Composition "` infix and that the counter is scoped per distinct prefix under `<VideoComposition__Path>`.
- `CompositionJobQueueTests` / `CompositionJobStatusStoreTests`: FIFO enqueue/dequeue (mirroring `CutJobQueueTests`); status store transitions `Pending → Processing → Completed/Failed` are observable and thread-safe under concurrent reads.
- `FfprobeCompositionProbeTests`: parses a known real `ffprobe -of json` sample fixture into the expected `CompositionInputProbe` fields; a malformed/empty ffprobe output surfaces as a probe failure rather than throwing an unhandled exception.
- `VideoCompositionServiceTests` / `VideoCompositionOptionsTests`: mirror `VideoCutServiceTests`/`VideoCutOptionsTests` exactly for the new root.

**Integration tests** (`WebApp.Tests/Endpoints/`, `WebApplicationFactory`-based, mirroring `CutEndpointsTests`):

- `CompositionEndpointsTests`: `POST /api/compositions` end-to-end with 2+ real short test-video cut fixtures and a real `ffmpeg`/`ffprobe` binary (already available per the `Dockerfile`) — enqueue, poll `GET /api/compositions/jobs` until `Completed` (bounded retry loop, same spirit as existing background-work tests), assert `GET /api/compositions` then lists the new video, `GET /api/compositions/{id}/stream` serves range-enabled content, and the response never leaks a physical path.
- Mixed-resolution/frame-rate fixture case: compose two fixtures with deliberately different resolutions/frame rates and assert the published output's `ffprobe`-read resolution matches the smaller input and its frame rate matches the lower of the two.
- Invalid input cases: fewer than 2 ids, an unresolvable id mixed with valid ones, and an empty request — assert `400` without any job enqueued or file appearing under `<VideoComposition__Path>`.

⚠️ TODO: confirm the exact short-sample-`.mp4` fixture convention already used by `FfmpegCutGeneratorTests`/`FfprobeDurationProbeTests` (per the same open item already flagged in `Specs/20260903190117-video-cut-export/Validation.md`) and reuse it rather than adding new binary test assets; this composition feature additionally needs at least two fixtures with differing resolutions/frame rates to exercise FR8/FR9's format-selection behavior end-to-end.

## Manual Verification

1. Ensure `${VIDEO_ROOT}/VideoComposition` exists on the host: `mkdir -p "$VIDEO_ROOT/VideoComposition"` (one-time, before first run with this feature).
2. `make docker-run-bg`, then `make docker-logs` in a second terminal to watch for composition-related log lines.
3. Open the app, Scan, produce at least two cuts (from the same or different source videos) via the existing Save Cut flow so the Cuts section has multiple entries.
4. In the Cuts section, confirm each card shows a checkbox in its top corner and clicking it does not open the card in the editor.
5. Check exactly one clip; confirm "Create Composition" stays disabled and the count shows "1 clip selected".
6. Check a second clip; confirm "Create Composition" becomes enabled and the count updates to "2 clips selected".
7. Click "Create Composition"; confirm the selection clears immediately and a new "Video Compositions" section below Cuts shows the job as Pending, then Processing, without the button/page blocking.
8. Within roughly the time it takes ffmpeg to normalize+concat the selected clips, confirm the job shows Completed and a new card appears in the Video Compositions grid with a thumbnail.
9. On the host, confirm `${VIDEO_ROOT}/VideoComposition/<prefix> Composition 0001.mp4` exists, and that `ffprobe` reports H.264 video / AAC audio and a duration approximately equal to the sum of the selected cuts' durations.
10. Click the new composition card; confirm it loads into the same vertical editor in the center of the page and plays back with visible fade transitions at each clip boundary; exercise play/pause, seek, volume, speed, and fill-tab to confirm they all work exactly as they do for a library video or cut.
11. Repeat with a third, differently-prefixed source's cuts selected first in the list; confirm the new composition's name uses that source's prefix and its own `0001` counter.
12. Force a failure (e.g. temporarily rename/remove one selected cut's file on the host mid-flight, or select a fixture crafted to fail `ffprobe`); confirm the job shows Failed, no file appears under `VideoComposition/`, and `make docker-logs` shows a redacted diagnostic naming the failing stage.
13. Stop the app (`make docker-down`) and restart (`make docker-run-bg`); confirm the Video Compositions section still lists previously produced compositions (folder-scan-based listing survives restart even though in-memory job history does not).
14. `make test` — full suite passes in the isolated Docker Compose test stack.

## Definition of Done

- `Requirements.md`, `Plan.md`, and this `Validation.md` are complete and internally consistent in this spec folder.
- All existing tests still pass under `make test`, and the new unit/integration tests above are added and passing.
- UI changes (`VideoGrid.razor`, `Home.razor`) cover loading/empty/error/pending states for the Video Compositions section and the Cuts-section selection controls, follow the Bootstrap Icons/tooltip accessibility contract in `AGENTS.md`'s Design System section (visible focus, accessible name, 40×40 minimum target for the checkbox control, non-color state cue for selected cards), and leave the existing Cuts/Library grid behavior (hover preview, click-to-select) unchanged when selection mode is off.
- `AGENTS.md`'s FFmpeg constraint bullet and Repository Map/Architecture Summary are updated to reflect the new, narrowly scoped composition pipeline (via the `init-agent` skill, per this project's documented workflow) once implementation lands.
- `docker-compose.yml`/`.env.example` changes are documented, and the manual verification steps above (including the one-time host `VideoComposition` directory creation) are confirmed to work from a clean `make docker-reset` state.
- No physical or root-relative filesystem path is ever observable in any HTTP response or log line, per the existing opaque-ID boundary tests, extended to cover the new `/api/compositions/*` surface.
- Every FR in `Requirements.md` has a corresponding row in Acceptance Criteria above and at least one test case exercising it.

## Rollback Plan

- The feature is additive: removing the checkbox/selection UI and "Video Compositions" section from `Home.razor`/`VideoGrid.razor` fully hides the feature from the UI without touching the existing Library/Cuts flows.
- To fully disable server-side: remove the `AddHostedService<CompositionBackgroundWorker>()` registration and the `MapCompositionEndpoints()` call in `Program.cs`/`Endpoints` composition — `POST /api/compositions` and `GET /api/compositions*` then 404, and no new jobs are processed.
- To revert the mount change: remove the third `VideoComposition` bind mount and `VideoComposition__Path` environment variable from `docker-compose.yml`; `VideoCompositionOptions` validation failing fast on startup (`ValidateOnStart`) makes a misconfigured/missing mount an immediate, visible startup failure rather than a silent runtime one.
- Any already-produced files under `<VIDEO_ROOT>/VideoComposition` are ordinary `.mp4` files on the host filesystem and can be deleted manually if the feature is rolled back; no database or migration state exists to unwind, and no other feature reads from this root.
