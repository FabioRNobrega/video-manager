# Validation: Multi-Scene Hover Video Previews with FFmpeg

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | Code inspection and `make test` show `ThumbnailCache`, `ThumbnailCoordinator`, `ThumbnailBackgroundWorker`, `ThumbnailJobQueue`, `IThumbnailGenerator`, and `FfmpegThumbnailGenerator` are unchanged in name/shape; the entire existing thumbnail test suite still passes. |
| FR2 | A successful generated preview decodes as MP4/H.264, has no audio stream (`ffprobe` reports zero audio streams), is scaled to the configured width preserving source aspect ratio, and reports the configured frame rate — for at least one `.mp4` and one `.mov`/`.webm`/`.m4v` source. |
| FR3 | Code inspection shows `FfmpegHoverPreviewGenerator` depends on the existing `IVideoDurationProbe` interface and no second FFprobe-invoking type exists. |
| FR4 | For a probed duration ≥ 15s, `ComputeSamplePositions` returns exactly three starts at ~20/50/80% of duration, each the configured segment length, none exceeding `duration − safety margin`. |
| FR5 | For a probed duration in `[3s, 15s)`, `ComputeSamplePositions` returns one segment starting at 0 with length `min(3s, duration − margin)`; for `(0, 3s)`, one segment starting at 0 with length `duration − margin`; no case computes a start/length pair that starts at or extends past the source's end. |
| FR6 | Passing a `null`, zero, or negative duration into generation returns `Failed` without any FFmpeg process being started, verified via a fake/garbage source in a controlled test. |
| FR7 | Tests can invoke one generation operation through `IHoverPreviewGenerator` using a validated entry and destination without constructing a scan, HTTP request, queue, or Blazor component. |
| FR8 | Generator tests/code inspection show `UseShellExecute = false`, individual `ArgumentList` values, no `Arguments` string, source re-validation before start, bounded/redacted stderr capture, and cancellation that kills the process tree and removes the temp file. |
| FR9 | For a three-segment plan, the built arguments contain three `-i` inputs each preceded by their own `-ss`/`-t`, a `-filter_complex` with a `concat` node, `-an`, and no audio-mapped output; for a one-segment plan, the arguments contain exactly one `-i` and a `-vf` scale/fps chain with no `concat`. |
| FR10 | While generation runs, only a unique temporary file can exist; the final `<key>.mp4` appears only after verified non-empty success; failure/cancellation leaves no publishable partial final file and removes its temporary file when possible; a concurrent valid final file wins without overwrite. |
| FR11 | A generated preview lives under `<ThumbnailCache:Path>/hover/<key>.mp4`; the key differs from the corresponding thumbnail's key for the same video; inspecting the hashing logic confirms it is a separate implementation from `ThumbnailCache.ComputeKey`, not a shared call into it. |
| FR12 | A valid pre-existing final MP4 yields `Ready`, starts no process, and remains byte-for-byte unchanged across rescan and restart. |
| FR13 | Queue tests prove finite capacity, non-blocking full-queue admission (`TryEnqueue` returns `false`, not silently drops), and at most one queued/running job per cache key, independent of `ThumbnailJobQueue`'s own instance/state. |
| FR14 | At most one preview FFmpeg process runs concurrently; one failed/thrown job is logged and the next queued job still processes; host cancellation stops the worker cleanly; logs contain only opaque IDs/cache-key prefixes. |
| FR15 | `HoverPreviewCoordinator.Reconcile` does not enqueue a job for an entry whose `ThumbnailCoordinator.Resolve(entry)` is not `Ready`, verified with a fake/stub thumbnail-side result. |
| FR16 | A scan of videos needing new previews returns promptly (before a deliberately blocked preview generator completes) and still returns full thumbnail-DTO behavior unchanged. |
| FR17 | A failing preview cache key launches FFmpeg once for the lifetime of that server process despite polling/rescans; restarting the server or changing the source identity permits one new attempt. |
| FR18 | Changing/removing the source after enqueue but before execution prevents publication under the queued key and does not alter the source or crash the worker. |
| FR19 | `HoverPreviewState` is a distinct client-side type from `ThumbnailState`; a test can construct a DTO with `ThumbnailState.Ready` and `HoverPreviewState.Pending` simultaneously. |
| FR20 | DTO serialization includes `hoverPreviewState`/`hoverPreviewUrl` alongside the existing thumbnail fields, with a non-null URL only when `Ready`, and no server-only identity/path property. |
| FR21 | The preview route returns `200 video/mp4` (range-enabled) for a ready current ID and `404` for unavailable/pending/failed/malformed/path-like/random/stale-snapshot/missing-file requests; `/previews/hover/...` is not a public route. |
| FR22 | With `HoverPreview:Enabled=false`, no preview job is ever enqueued across repeated scans, `GET /api/videos/{id}/preview` always returns `404` even for an otherwise-eligible video, and every DTO reports `HoverPreviewState.Unavailable`/`null` URL, while thumbnail behavior is provably unaffected in the same test run. |
| FR23 | Opening the library (a scan response) requests/serves thumbnails as today; no automatic client request is made to any `/preview` URL until a real hover-and-delay interaction occurs, verified via component/state test asserting no eager preview fetch. |
| FR24 | A test with two distinct hovered/considered video IDs proves hovering one cannot start or cancel the other's preview state. |
| FR25 | A simulated rapid `mouseenter`→`mouseleave` before ~300ms elapses never activates a preview (no state change, no request); a `mouseenter` held past the delay activates it; a subsequent `mouseleave` clears it immediately regardless of any in-flight delay. |
| FR26 | Hovering (past the delay) a video whose `HoverPreviewState` is `Pending`, `Failed`, or `Unavailable` leaves the card showing its existing static-thumbnail state with no additional markup. |
| FR27 | The rendered `<video>` element has `autoplay`, `muted`, `loop`, `playsinline`/inline-equivalent attributes, no `controls` attribute, a `poster` equal to the card's thumbnail URL, and sits inside the same `.ratio-16x9` cover-sized container as the `<img>` it replaces. |
| FR28 | Clicking a card while its preview is active still invokes `OnSelect` with the same video, and the selected-state badge/keyboard-selectable button behavior is unchanged from before this feature. |

## Test Cases

### Unit tests

- `WebApp.Tests/Configuration/HoverPreviewOptionsTests.cs`
  - Accept the documented defaults; reject non-positive `Width`, `FrameRate`, `SegmentSeconds`, or `QueueCapacity`.
- `WebApp.Tests/Services/HoverPreviewCacheTests.cs`
  - Produce an identical key for identical identity inputs; different keys when any identity input changes.
  - Produce a different key than `ThumbnailCache` would for the exact same identity inputs (distinct version marker).
  - Keep all final/temporary path resolution within `<ThumbnailCache:Path>/hover`.
  - Treat only a non-empty readable regular `<key>.mp4` as ready.
- `WebApp.Tests/Services/HoverPreviewJobQueueTests.cs`
  - Enforce configured capacity without blocking `TryEnqueue` when full.
  - Reject duplicate queued/running keys; release a key after completion and after failed admission.
  - Honor dequeue cancellation.
- `WebApp.Tests/Services/FfmpegHoverPreviewGeneratorTests.cs`
  - `[Theory]` over `ComputeSamplePositions`: below 3s, at/near 3s, between 3s and 15s, at/near 15s, well above 15s, and a duration exactly at each clamp boundary; assert start/length values and that none exceed `duration − margin`.
  - Null/zero/negative duration returns `Failed` with no process started (verified via a fake source that would otherwise hang/crash if FFmpeg were invoked).
  - Verify three-input `-ss`/`-t`/`concat` argument shape for a long fixture and single-input `-vf` shape for a short fixture, using the same `ArgumentList`-inspection approach as the existing thumbnail generator tests (including a source path containing spaces/shell metacharacters).
  - Verify a changed/missing source is rejected before process start.
  - Verify a pre-existing valid final file wins without overwrite and without invoking FFmpeg.
  - Real-FFmpeg-gated (skipped, not failed, when `ffmpeg` is unavailable): generate one preview from a real multi-segment-length fixture, decode it to confirm no audio stream, expected width, and expected frame rate; generate one preview from a real short (<3s) fixture and confirm success; verify cancellation terminates a long-running child and removes its temp file.
- `WebApp.Tests/Services/HoverPreviewCoordinatorTests.cs`
  - Resolve unavailable/no-snapshot, pending, ready, and failed states, independent of `ThumbnailState`.
  - Do not enqueue when the corresponding thumbnail is not `Ready` (fake thumbnail coordinator/result).
  - Do not enqueue, and resolve endpoint/DTO state to `Unavailable`, when `HoverPreview:Enabled=false`.
  - Suppress retries for a failed key within the process; permit a changed key and a fresh coordinator after simulated restart.
  - Reconcile more entries than channel capacity across worker completions until all are admitted.
- `WebApp.Tests/Services/VideoLibraryServiceTests.cs`
  - Existing behavior (extension/symlink/containment/cancellation/scan-failure/concurrency/opaque-snapshot) is preserved with the `CreateService` factory updated to supply a `HoverPreviewCoordinator`.

### Integration and endpoint tests

- `WebApp.Tests/Endpoints/VideoEndpointsTests.cs`, using the existing `WebApplicationFactory<Program>` convention:
  - Serve a known-ready preview as `video/mp4` through the opaque-ID endpoint, with range-request support.
  - Return `404` for non-ready/malformed/path-like/random/stale/missing-cache cases, mirroring the existing thumbnail-endpoint test.
  - Assert serialized DTOs contain `hoverPreviewState`/`hoverPreviewUrl` with no physical/root-relative/preview path or cache key anywhere in scan/status responses.
  - Assert `/previews/hover/<name>.mp4` is not served directly.
  - With `HoverPreview:Enabled=false` configured on the test factory, assert the preview endpoint 404s for an otherwise-eligible video and DTOs report `Unavailable`.
- Full infrastructure-backed suite:
  - Run `make test` in `docker-compose.test.yml`; confirm existing and new tests pass, and the test project cleanup removes its disposable volumes (no new volume is introduced by this feature).

### Client-side state tests

- Extend the existing rendered-source/static-root client tests or add focused component tests consistent with current repository capabilities to verify: the default `<img>` markup is unchanged when no card is hovered; no request is made to any `/preview` URL on initial render; and (to the extent practical without full browser video decoding) the hover-delay state machine — pending timer cancelled by early `mouseleave`, activation only after the delay elapses while still hovered, immediate clear on `mouseleave` — is exercised independently of actual `<video>` playback.

## Manual Verification

1. `make docker-build`, then `make docker-run-bg`; confirm the container starts and `ffmpeg`/`ffprobe` still both work (`make docker-exec` → `ffmpeg -version`, `ffprobe -version`).
2. Using a known real video, run the manual multi-scene `ffmpeg` proof from `Plan.md`'s Implementation Sequence step 1 inside the container; open the resulting MP4, confirm it contains three (or the appropriate fallback) distinct-looking sections, has no audio track, and inspect its file size/encode time before trusting the automated pipeline's defaults.
3. Run `make test`. Confirm all new and existing service/endpoint/client/configuration/queue/generator tests pass in the isolated Compose test project.
4. Clear only the `hover` subdirectory of the development preview cache (leave the existing thumbnail files in place), restart the app, and scan. Confirm cards show their existing static thumbnails immediately, unaffected by preview generation status.
5. Follow `make docker-logs`. Confirm preview generation begins only for videos whose thumbnail is already `Ready`, one preview job runs at a time, and logs show opaque IDs/cache-key prefixes only — no `/videos`, `/previews`, relative, or host paths.
6. In the browser, hover a card whose preview has finished. Confirm nothing happens for a brief flicker/rapid pass, but holding the pointer for about 300ms swaps the static image for a small looping, silent, control-free video using the same image as its poster, with no layout shift. Move the pointer away and confirm the static thumbnail returns immediately.
7. Hover a card whose preview has not finished (or that has none because its thumbnail isn't `Ready` yet). Confirm the static thumbnail simply remains — no error, spinner, or blank area.
8. Rapidly move the pointer across several cards in quick succession. Confirm this does not trigger a burst of preview video downloads (check the browser's network panel) and does not leave any wrong card showing a preview.
9. Click a card while its preview is actively showing. Confirm the video still gets selected exactly as before, playback/editor behavior is unaffected, and the selected badge appears correctly.
10. Set `HoverPreview__Enabled=false` in the environment, restart, and rescan. Confirm no preview jobs are logged, `GET /api/videos/{id}/preview` returns `404` for a video whose thumbnail is `Ready`, and the thumbnail experience is otherwise identical to before this feature.
11. Re-enable previews, run `make docker-down` then `make docker-run-bg`. Confirm existing preview files persist and are not regenerated (no new "preview generation started" log lines for unchanged videos).
12. Add or identify a corrupt/unsupported test video and rescan. Confirm its preview attempts generation once (after its thumbnail settles into `Ready` or `Failed`), becomes `Failed` if attempted, does not retry during later polls/rescans in the same server process, and does not prevent other videos' previews or thumbnails from proceeding.
13. Replace or modify an existing video (changing size or last-modification time) and rescan. Confirm both its thumbnail and its hover preview receive new cache identities, and neither the old thumbnail nor the old preview is served for the new version.
14. On the full real library, clear only the `hover` cache subdirectory and let it backfill in the background. Confirm the page stays responsive throughout, thumbnails are unaffected, and completed videos progressively gain working hover previews without the operator needing to do anything further.

## Definition of Done

- `Requirements.md`, `Plan.md`, and `Validation.md` remain synchronized in this spec folder, with every implemented behavior mapped to FR1–FR28.
- `ThumbnailCache`, `ThumbnailCoordinator`, `ThumbnailBackgroundWorker`, `ThumbnailJobQueue`, `IThumbnailGenerator`, and `FfmpegThumbnailGenerator` are unchanged in name, shape, and public behavior; every existing thumbnail test still passes unmodified in intent.
- `make docker-build`, `make test`, and normal `make docker-run-bg` startup succeed.
- New behavior has xUnit coverage following the repository's `Configuration/`, `Services/`, `Endpoints/`, and `Client/` organization, at a depth comparable to the static-thumbnail pipeline's own test suite.
- The manual multi-scene FFmpeg command was proven inside the running container before being encoded into `FfmpegHoverPreviewGenerator`.
- The preview cache lives under the existing preview root's `hover` subdirectory; no new Docker volume was introduced.
- Scan responsiveness, thumbnail-first scheduling, bounded queueing, sequential work, cancellation, one-failure-per-process behavior, atomic files, stable cache reuse, and source invalidation are verified for the preview pipeline exactly as they were for thumbnails.
- The complete hover-interaction flow (default image, ~300ms entry delay, immediate exit, graceful non-ready fallback, no eager downloads, unaffected click/selection) is verified in a real browser against the real video library.
- `HoverPreview:Enabled=false` is verified to fully disable the feature (no jobs, `404` endpoint, `Unavailable` DTOs) without affecting thumbnails.
- Normal logs and all browser-visible payloads have been checked for physical path, root-relative path, preview path, or cache-key leakage from the new endpoint/DTO fields.
- Microsoft-specific decisions (Blazor event handling, `BackgroundService` in .NET 10) remain supported by the official Microsoft Learn evidence recorded in `Plan.md`; the FFmpeg `concat` filter decision is documented as non-Microsoft evidence per the same section.
- The reduced-motion limitation is explicitly recorded in `Requirements.md`'s Out of Scope rather than silently omitted.

## Rollback Plan

- Revert this feature's service registrations, endpoint/DTO additions, and `VideoLibrary.razor` hover markup as one coherent change; retain every existing thumbnail route, DTO field, and card markup exactly as `Specs/20260831104814-static-video-thumbnails-ffmpeg/` left them.
- No Docker volume or Compose service needs removal — the `hover` subdirectory lives inside the existing `thumbnail_cache` volume; deleting it (or the whole volume via `make docker-reset`) removes only regenerable derived data, never source video or the static-thumbnail cache's own files (which sit in the same volume's top level, untouched by this feature).
- Setting `HoverPreview__Enabled=false` is a non-destructive, immediate way to disable the feature in place without a code rollback, per FR22.
- Stop/recreate the stack through `make docker-down` and `make docker-run-bg`, then verify the original scan, selection, stream, thumbnail, crop, playback, theme, and Fill-tab tests, exactly as the prior spec's own rollback plan describes.
- No database migration, source-video rollback, or browser storage migration is required. The `/videos` mount and source files are never modified by this feature.
