# Validation: Static Video Thumbnails with FFmpeg

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | `make docker-build` succeeds, the app starts normally, and `ffmpeg -version` exits zero inside the running `webapp` container. |
| FR2 | Compose inspection and an in-container write probe show `/videos` is read-only while `/previews` is writable and backed by a different mount. |
| FR3 | A generated marker/thumbnail in `/previews` remains after `make docker-down` followed by `make docker-run-bg`; it is removed only by an explicit volume-reset operation. |
| FR4 | Startup succeeds for a valid absolute writable disjoint cache and fails clearly for missing, relative, nonexistent, non-writable, equal-to-video-root, nested-in-video-root, or video-root-nested-in-preview configurations. |
| FR5 | Repeated identity calculation for unchanged metadata produces the same lowercase SHA-256 key; changing relative path, length, or UTC last-write value changes the key. |
| FR6 | Scan/status JSON, thumbnail headers/bodies, errors, and normal logs contain no `/videos`, `/previews`, host path, root-relative path, or cache key; only opaque IDs and safe URLs/state reach the browser. |
| FR7 | A successful generated file decodes as JPEG, measures exactly 640×360, fills the frame without aspect-ratio distortion, and represents a frame at approximately 10% of the probed duration (clamped between a 2-second floor and a 10-minute cap), or the 3-second fallback offset when duration cannot be probed. |
| FR8 | Tests can invoke one generation operation through `IThumbnailGenerator` using a validated server entry and destination without constructing a scan, HTTP request, queue, or Blazor component. |
| FR9 | Generator tests/code inspection show `UseShellExecute = false`, individual `ArgumentList` values, no `Arguments` string, no shell wrapper, and no request-derived source/destination. |
| FR10 | Success, nonzero exit, process-start failure, and cancellation produce structured outcomes; stderr is drained without hanging; shutdown terminates/reaps FFmpeg; emitted diagnostics are bounded and path-redacted. |
| FR11 | While generation runs, only a unique temporary file can exist; the final `<hash>.jpg` appears only after verified success, and failure/cancellation leaves no publishable partial final file and removes its temporary file when possible. |
| FR12 | A valid pre-existing final JPEG yields `Ready`, starts no process, and remains byte-for-byte unchanged across rescan and restart. |
| FR13 | Queue tests prove finite capacity, non-blocking full-queue admission, cancellation-aware dequeue, and at most one queued/running job per cache key. |
| FR14 | With more missing entries than queue capacity, scan returns promptly and worker completion repeatedly admits remaining pending entries until each becomes `Ready` or `Failed`. |
| FR15 | At most one FFmpeg process/job executes concurrently; one thrown/failed job is logged and the next queued job is still processed; cancellation stops the worker cleanly. |
| FR16 | A scan of missing-thumbnail entries returns before a deliberately blocked generator completes and includes the identity metadata/current snapshot needed for background reconciliation. |
| FR17 | DTO serialization represents all four states correctly, includes a safe URL only for `Ready`, and contains no server-only identity/path property. |
| FR18 | `GET /api/videos` before a scan returns an empty current snapshot; after a scan it returns the same opaque IDs without filesystem rescan, and state changes appear on subsequent GETs. |
| FR19 | The thumbnail route returns `200 image/jpeg` for a ready current ID and `404` for unavailable, pending, failed, malformed, path-like, random, stale-snapshot, missing-file, or unsupported requests; `/previews/...` is not a public route. |
| FR20 | Ready cards render a cover-sized `<img>` in the existing 16:9 area; all other states retain `bi-film`; cards remain keyboard-selectable and playback works regardless of thumbnail state. |
| FR21 | Polling begins only when a scan response contains `Pending`, occurs at the specified modest interval, updates cards/selection, ends when no item is pending, and is cancelled by rescan/disposal; it never runs continuously for an all-ready/all-failed list. |
| FR22 | A failing cache key launches FFmpeg once for the lifetime of that server process despite polling/rescans; restarting the server or changing the source identity permits one new attempt. |
| FR23 | Changing/removing the source after enqueue but before execution prevents publication under the queued key and does not alter the source or crash the worker. |
| FR24 | Clean-cache end-to-end testing proves initial placeholders, sequential generation, live replacement, restart reuse, generation only for an added video, and a distinct pending/new thumbnail for a modified video while `/videos` remains read-only. |

## Test Cases

### Unit tests

- `WebApp.Tests/Configuration/ThumbnailCacheOptionsTests.cs`
  - Accept an absolute existing readable/writable preview directory disjoint from the video root.
  - Reject blank, relative, nonexistent, non-writable, equal, parent, and child overlap cases with configuration-key-focused messages.
  - Delete the writability probe after success and best-effort cleanup after failure.
- `WebApp.Tests/Services/ThumbnailCacheTests.cs`
  - Produce an identical SHA-256 key for identical normalized relative path/length/UTC timestamp inputs.
  - Produce different keys when each identity input changes independently.
  - Keep all final and temporary path resolution within the configured preview root.
  - Treat only non-empty readable regular `<key>.jpg` files as ready.
  - Never expose the relative path through the key or browser-facing state.
- `WebApp.Tests/Services/ThumbnailJobQueueTests.cs`
  - Enforce configured capacity without blocking `TryEnqueue` when full.
  - Reject duplicate queued and running keys.
  - Release a key after completion and after failed admission.
  - Honor dequeue cancellation and preserve FIFO behavior for admitted jobs.
- `WebApp.Tests/Services/FfmpegThumbnailGeneratorTests.cs`
  - Generate/publish a valid JPEG through the concrete executable path available in the Docker test image or a controlled executable seam.
  - Verify source/destination are separate `ArgumentList` values, including paths containing spaces or shell metacharacters.
  - Verify nonzero exit and process-start failure return failure without a final file.
  - Verify redirected stderr is consumed, bounded, and redacted before logging.
  - Verify cancellation terminates a long-running child and removes its temporary file.
  - Verify a changed/missing source is rejected before process start.
  - Verify a pre-existing valid final file wins without overwrite.
  - Verify the computed seek clamps to 10% of a probed duration between a 2-second floor and a 10-minute cap, and falls back to a fixed 3-second offset when duration is null, zero, or negative.
  - Verify the computed seek is placed in `ArgumentList` as its own `-ss` value.
- `WebApp.Tests/Services/FfprobeDurationProbeTests.cs`
  - Return the real duration (within tolerance) for a known-length fixture generated through the Docker test image's ffmpeg.
  - Return null for a missing, corrupt, or unsupported source rather than throwing.
  - Propagate cancellation from a precancelled token.
- `WebApp.Tests/Services/ThumbnailCoordinatorTests.cs`
  - Resolve unavailable/no-snapshot, pending, ready, and failed states.
  - Suppress retries for a failed key within the process.
  - Permit a changed key and a fresh coordinator after simulated restart.
  - Reconcile more entries than channel capacity across worker completions until all are admitted.
- `WebApp.Tests/Services/VideoLibraryServiceTests.cs`
  - Preserve all existing extension, symlink/reparse-point, containment, cancellation, scan-failure, concurrency, and opaque-snapshot behavior.
  - Capture normalized root-relative identity data and UTC last-write metadata only for validated files.
  - Return the current snapshot without rescanning or changing IDs.
  - Return from scan while generation remains blocked.

### Integration and endpoint tests

- `WebApp.Tests/Endpoints/VideoEndpointsTests.cs`, using the existing `WebApplicationFactory<Program>` convention:
  - Preserve scan DTO and range-enabled stream tests.
  - Verify `GET /api/videos` is empty before scan and returns unchanged current IDs afterward.
  - Transition a DTO from pending to ready in a controlled test setup and verify safe URL projection.
  - Serve a known ready JPEG as `image/jpeg` through the opaque-ID endpoint.
  - Return `404` for non-ready, malformed, path-like, random, stale, and missing-cache cases.
  - Assert serialized DTOs and problem responses contain no physical/root-relative/preview path or cache key.
  - Assert `/previews/<name>.jpg` is not served directly.
- `WebApp.Tests/Client/ThemeBootstrapTests.cs` and/or a focused client-source test following the repository's current static markup conventions:
  - Preserve the Bootstrap card grid, ratio container, accessible card button, selected badge, and existing empty/loading/error states.
  - Verify ready-state image markup uses an empty `alt`, safe URL, and Bootstrap cover/size utilities.
  - Verify unavailable/pending/failed branches retain `bi-film`.
  - Verify no new handwritten icon, external UI framework, or unnecessary component stylesheet is introduced.
- Full infrastructure-backed suite:
  - Run `make test` in `docker-compose.test.yml`, including a valid isolated preview volume/path.
  - Confirm existing tests pass and the test project cleanup removes its disposable volumes.

### Timing and concurrency assertions

- Use gates/task-completion sources rather than wall-clock sleeps in queue/worker tests.
- Prove maximum observed generator concurrency equals one.
- Prove scan completes while the fake generator is still gated.
- Use a short injected/test polling interval only in client logic tests if polling is factored behind a controllable delay; production remains two seconds.

## Manual Verification

1. Copy `.env.example` to `.env` if needed, set `VIDEO_ROOT` to an absolute host directory containing known videos, and leave `WEBAPP_PORT` at its desired loopback value. Do not commit `.env`.
2. Run `make docker-build`, then `make docker-run-bg` and `make docker-ps`. Confirm the `webapp` container is healthy/running.
3. Run `make docker-exec`, then execute `ffmpeg -version`. Confirm it exits successfully and reports the installed build.
4. In that container shell, inspect mounts and test boundaries: create/delete a harmless file under `/previews`, then attempt a harmless create under `/videos` and confirm the latter fails because the mount is read-only. Do not alter a source video.
5. Choose a known mounted video, record its checksum, use `ffprobe` to read its duration and compute the seek per the 10%/2s-floor/10-minute-cap rule, then manually run FFmpeg with separate input/output paths at that offset to produce `/previews/manual-proof.jpg` using the planned 640×360 scale/crop filter. Open or copy/view the JPEG through an appropriate local inspection method, confirm its dimensions/content, recompute the source checksum, and confirm it is unchanged. Remove only the derived manual proof afterward.
6. Run `make test`. Confirm all service, endpoint, client, configuration, queue, and generator tests pass in the isolated Compose test project.
7. Clear the development preview cache with the explicit `make docker-reset` workflow (which deletes this Compose project's volumes), rebuild/start the app, open the loopback URL, and select **Scan**. Confirm cards appear promptly with `bi-film` placeholders rather than waiting for FFmpeg.
8. Follow `make docker-logs`. Confirm one thumbnail generation runs at a time and logs start/success using opaque IDs or cache-key prefixes without `/videos`, `/previews`, relative paths, or host paths.
9. Keep the browser open. Confirm polling replaces each placeholder with a 16:9 cover-sized image as its JPEG becomes ready, without changing the selected video or interrupting playback. Confirm network polling stops after no item is pending.
10. Select cards whose thumbnails are pending and ready. Confirm both select and stream normally, keyboard focus remains visible, selection is conveyed by text/state, and responsive one/two/three/four-column layouts remain intact.
11. Add or identify a corrupt/unsupported test video and rescan. Confirm it attempts generation once, becomes failed, keeps the film placeholder, does not retry during later polls/rescans in the same server process, and does not prevent later jobs or playback. Confirm its diagnostic is useful but path-redacted.
12. Run `make docker-down`, then `make docker-run-bg`, rescan, and confirm unchanged cached thumbnails are immediately ready and are not regenerated. Confirm the previously failed video is eligible for one new attempt because process-local failure state was reset.
13. Add one new supported video and rescan. Confirm only it becomes pending and only it launches FFmpeg.
14. Replace or modify an existing video, ensuring its size or last-modification timestamp changes, then rescan. Confirm it receives a new cache identity internally, shows pending/placeholder, and never receives the old source version's JPEG.
15. While a deliberately slow generation is queued/running, modify or remove that source. Confirm no final thumbnail is published under stale metadata, the worker continues, and the source directory remains unchanged.
16. Recreate the app container again without deleting volumes. Confirm thumbnails persist. Finally, inspect `/videos` as read-only and `/previews` as the only writable media-derived location.

## Definition of Done

- `Requirements.md`, `Plan.md`, and `Validation.md` remain synchronized in this spec folder, with every implemented behavior mapped to FR1–FR24.
- The new spec is treated as the explicit authority for the narrow static-thumbnail FFmpeg exception; all unrelated media-processing exclusions remain active.
- `make docker-build`, `make test`, and normal `make docker-run-bg` startup succeed.
- All existing tests still pass and new behavior has xUnit coverage following the repository's `Services/`, `Endpoints/`, and `Client/` organization.
- FFmpeg installation and one manual source-to-preview generation are proven inside the running container.
- The source mount remains read-only and path-private; the persistent preview volume is separate, writable, and not directly exposed as a static directory.
- Scan responsiveness, bounded queueing, sequential work, cancellation, one-failure-per-process behavior, atomic files, stable cache reuse, and source invalidation are verified.
- The complete clean-cache browser flow shows immediate placeholders, eventual automatic image replacement, stopped polling, preserved selection/playback, responsive Bootstrap presentation, and accessible semantics.
- Container recreation reuses unchanged thumbnails; adding/changing videos generates only the required new cache entries.
- Normal logs and all browser-visible payloads have been checked for physical path, root-relative path, preview path, and cache-key leakage.
- Microsoft/.NET-specific decisions remain supported by the official Microsoft Learn evidence recorded in `Plan.md`.

## Rollback Plan

- Revert the feature's service registrations, endpoints, DTO/UI polling/image changes, Docker FFmpeg layer, Compose `/previews` mount/configuration, and new thumbnail-specific files as one coherent change; retain the existing scan/stream routes and `bi-film` card markup.
- Remove `ThumbnailCache__Path` and the `thumbnail_cache` volume declaration only after the application no longer validates or uses them.
- Stop/recreate the stack through `make docker-down` and `make docker-run-bg`, then verify the original scan, selection, stream, crop, playback, theme, and Fill-tab tests.
- Generated thumbnails are derived, not user-authored data. If disk reclamation is desired after rollback, `make docker-reset` may remove Compose volumes, but this is an explicit destructive cleanup and should be run only after confirming no other desired development-volume data is needed.
- No database migration, source-video rollback, or browser storage migration is required. The `/videos` mount and source files are never modified by this feature.
