# Requirements: Static Video Thumbnails with FFmpeg

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

`WebApp/WebApp.Client/Components/VideoLibrary.razor` currently renders the same Bootstrap `bi-film` placeholder in every discovered video's 16:9 card area. Users need a recognizable static image for each video without delaying the existing explicit library scan or exposing either the read-only video filesystem or a new writable preview filesystem to the WebAssembly client. This specification introduces a deliberately bounded exception to the earlier project rule excluding FFmpeg and media processing: FFmpeg may generate only static JPEG thumbnails, inside the existing local ASP.NET Core container, into a separate cache volume. All other media processing remains prohibited.

## User Stories

- Given an empty preview cache, when a user scans the library, then discovered videos appear promptly with film placeholders while thumbnails are generated sequentially in the background.
- Given a thumbnail that finishes after the initial scan response, when the library remains open, then the corresponding card eventually replaces its placeholder without a page restart or manual rescan.
- Given an unchanged video and a persisted preview cache, when the app container is recreated or the library is rescanned, then the existing thumbnail is reused without starting FFmpeg again.
- Given a corrupt or unsupported video, when thumbnail generation fails, then that card keeps its placeholder, other videos continue processing and playback remains available.
- Given a video that has been modified or replaced, when the user rescans, then the new source version receives a different internal cache identity and its old thumbnail is not served for the new version.

## Functional Requirements

1. **FR1 — FFmpeg runtime:** `Dockerfile` must install FFmpeg in the existing .NET 10 SDK image, and the resulting development container must start normally and execute `ffmpeg -version` successfully.
2. **FR2 — Filesystem separation:** `docker-compose.yml` must keep `${VIDEO_ROOT}` mounted read-only at `/videos` and mount a distinct named volume at `/previews` with write access only for generated thumbnail cache files.
3. **FR3 — Persistent MVP cache:** The `/previews` named volume must survive ordinary `webapp` container recreation and be removed only by an explicit volume-deleting workflow such as `make docker-reset`.
4. **FR4 — Startup configuration validation:** The server must bind thumbnail-cache configuration, require an absolute existing writable directory, and fail startup with a clear options-validation error when the configured preview directory is missing, relative, unreadable, unwritable, or overlaps the configured video-library directory.
5. **FR5 — Stable source identity:** The backend must derive a deterministic SHA-256 cache key from the video's normalized root-relative path, file size, and UTC last-modification value. The unchanged source version must produce the same key across scans and restarts; changing any identity input must produce a different key.
6. **FR6 — Private identity and paths:** Cache keys, root-relative paths, physical source paths, preview paths, and temporary paths must remain server-internal. Browser-facing video references must continue to use only current-snapshot opaque video IDs and safe application URLs/state.
7. **FR7 — Thumbnail output contract:** Each successful generation must produce one 640×360 JPEG, cropped/scaled to fill 16:9 without distorting the source aspect ratio, representing a frame chosen by a duration-aware seek: approximately 10% into the video's probed duration, floored at 2 seconds and capped at 10 minutes so the offset stays sensible for both very short and very long sources; when the duration cannot be determined, generation falls back to a fixed 3-second offset.
8. **FR8 — Focused generator abstraction:** A backend thumbnail-generator interface must accept an already discovered and validated source entry plus its server-resolved final destination, invoke FFmpeg, await completion, and return a structured success, failure, or cancellation result without discovering files, managing queues, serving HTTP, or referencing Blazor.
9. **FR9 — Safe process invocation:** The FFmpeg implementation must use `System.Diagnostics.ProcessStartInfo` with `UseShellExecute = false` and `ArgumentList`; it must never construct a shell command or accept a browser-controlled filesystem path.
10. **FR10 — Process diagnostics and cancellation:** The generator must capture the exit code and standard error, avoid redirected-stream deadlocks, honor application-shutdown cancellation, terminate the FFmpeg process tree when cancellation stops the wait, and log a bounded/redacted diagnostic that cannot disclose physical or root-relative paths.
11. **FR11 — Atomic publication:** FFmpeg must write to a unique temporary JPEG in the preview cache. The generator must verify successful exit and a non-empty readable output before atomically moving it to the final cache filename; failed, cancelled, or abandoned temporary outputs must be deleted when possible.
12. **FR12 — Cache reuse:** Before queueing or generating work, the backend must treat a valid, non-empty final JPEG for the current cache key as ready and must not invoke FFmpeg for it again.
13. **FR13 — Bounded deduplicated jobs:** The server must provide an in-memory bounded thumbnail job queue that prevents simultaneous duplicate jobs for the same cache key and admits work without making `VideoLibraryService.ScanAsync` wait for queue capacity or FFmpeg.
14. **FR14 — Complete scheduling under backpressure:** Videos that cannot enter the bounded channel immediately must remain pending, and background reconciliation must refill the queue from the current library snapshot as capacity becomes available so large scans are not silently abandoned.
15. **FR15 — Sequential resilient worker:** A registered `BackgroundService` must consume one thumbnail job at a time, continue after an individual failure, and log start, success, failure, and cancellation using the opaque media ID and/or cache-key prefix rather than a filesystem path.
16. **FR16 — Non-blocking scan integration:** After each discovered entry passes the existing containment, symlink, extension, and readability checks, `VideoLibraryService` must capture the identity metadata needed by FR5, atomically publish the snapshot, reconcile cache/job state, and return without awaiting FFmpeg completion.
17. **FR17 — Browser-safe thumbnail state:** `VideoItemDto` must expose a typed state equivalent to `Unavailable`, `Pending`, `Ready`, or `Failed`, plus a nullable application thumbnail URL that is populated only when the state is `Ready`; it must never expose cache keys or filesystem paths.
18. **FR18 — Current-snapshot status API:** A read-only endpoint must return DTOs for the existing current snapshot without rescanning the filesystem, allowing the client to observe thumbnail state changes while preserving the same opaque IDs.
19. **FR19 — Protected thumbnail endpoint:** A thumbnail endpoint must first resolve a current-snapshot opaque video ID through `IVideoLibraryService`, derive that entry's expected cache file internally, return `image/jpeg` only when the final file is valid, and otherwise return `404`; it must not accept arbitrary paths or expose the preview directory through static-file middleware.
20. **FR20 — Conditional card image:** `VideoLibrary.razor` must preserve its existing 16:9 card container and Bootstrap film placeholder whenever the thumbnail is not ready. When ready, it must render the JPEG in that same container with cover sizing, without changing video selection or playback behavior.
21. **FR21 — Pending-only refresh:** `Home.razor` must poll the current-snapshot status API at a modest fixed interval only while at least one DTO is `Pending`, update cards in place, preserve a still-valid selection, stop when no item is pending, and cancel polling when the component is disposed or a new scan supersedes it.
22. **FR22 — Single failure attempt:** A failed cache key must enter `Failed` for the remainder of the current server process and must not be retried by polling, reconciliation, or rescanning during that process. Restarting the application or changing the source identity creates a fresh attempt opportunity.
23. **FR23 — Source-change safety:** Before launching FFmpeg, the worker must confirm that the queued source still matches the validated size and last-modification metadata; a changed, missing, or unreadable source must not publish a thumbnail under the stale cache key.
24. **FR24 — End-to-end persistence behavior:** After normal container recreation, cached thumbnails for unchanged videos must remain ready, the original video mount must remain read-only, and only new or changed videos must require generation.

## Non-Functional Requirements

- **Responsiveness:** Filesystem discovery and DTO rendering must remain independent from FFmpeg runtime; scanning hundreds of files must not wait for media generation or bounded-channel capacity.
- **Resource control:** The MVP must run at most one FFmpeg process at a time. Queue capacity must be finite and configurable or documented with a safe default.
- **Security and privacy:** Only `VideoFileEntry` instances produced by the existing validated scan may become jobs. No request parameter may become an FFmpeg path. Normal application logs and browser responses must not reveal any physical or root-relative path.
- **Source integrity:** The `/videos` mount remains read-only, FFmpeg receives no output path under `/videos`, and generation must never overwrite or mutate source media.
- **Failure isolation:** Process-start failures, corrupt input, unsupported codecs, permissions errors, I/O races, cancellation, and cleanup errors must not crash the host or terminate the background worker.
- **Cache consistency:** Only completed, verified files may be served. Obsolete cache files may remain on disk in this MVP but must be unreachable for a different current source version.
- **Architecture and testability:** Server-only process, cache, queue, and orchestration responsibilities must remain behind focused interfaces and preserve the existing server/client boundary. Tests must be able to exercise identity, state, queue, endpoint, and UI behavior without exposing paths to the client.
- **UI and accessibility:** The existing Bootstrap 5.3.8 design system, responsive card grid, keyboard-selectable card button, selected-state text, focus behavior, and non-color cues must remain intact. Thumbnail images are decorative because the adjacent card title names the video and therefore use an empty alternative text.
- **Local-only deployment:** Existing loopback-only port publication and lack of remote authentication remain unchanged. The endpoint shape must permit future authorization without treating the preview volume as a public directory.
- **Workflow:** Build, run, and test validation must continue exclusively through the repository's Docker Compose-backed `Makefile` commands.

## Out of Scope

- Animated GIF/WebM previews, preview videos, multiple frame selection, scene detection, and user-selected thumbnail frames.
- FFprobe metadata persistence beyond information required by this static generation operation.
- Entity Framework Core, SQLite, persistent media records, persistent job/status records, and cache-index databases.
- SignalR, server-sent events, WebSockets, or per-card push updates.
- Multiple worker containers, distributed queues, parallel FFmpeg execution, remote processing, and NAS write access outside `/previews`.
- ASP.NET Core Identity, ACLs, LAN/Internet hosting, or authorization policy implementation in this slice.
- Automatic orphaned-thumbnail garbage collection and cache size/retention policies.
- Retrying a failed cache key within the same application process.
- Changing the supported source extension allowlist, video streaming behavior, reframing editor, media controls, theme, or Fill-tab behavior.
- Any FFmpeg use other than producing the specified static JPEG thumbnails. The previous general media-processing exclusion remains in force for all other uses.

## Open Questions

None. MVP decisions confirmed on 2026-08-31: JPEG at 640×360, persistent named-volume cache at `/previews`, automatic polling while work is pending, and one failed attempt per cache key for the current server process. The frame offset was refined on 2026-08-31 from a fixed three seconds to a duration-aware seek (see FR7) so a single frame stays representative across widely varying video lengths.
