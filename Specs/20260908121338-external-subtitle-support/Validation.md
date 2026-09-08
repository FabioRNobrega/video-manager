# Validation: External Subtitle Support

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | Given `movie.mp4` and a sibling `movie.srt` in the same folder, `VideoLibraryService.ScanAsync` (or its `SubtitleMatcher` helper) reports a match; given only `movie.mp4` (no `.srt`), or an `.srt` with a different base name, it reports no match. The same holds for a video listed by `ArchiveService`/`ArchiveEndpoints` in any category/folder. |
| FR2 | No new server-only type (`SubtitleMatcher`, `SubtitleCache`, `FfmpegSubtitleGenerator`, `SubtitleBackgroundWorker`) is referenced from `WebApp.Client`; `WebApp.Client` only ever receives `SubtitleState` and a relative `SubtitleUrl` string. |
| FR3 | `FfmpegSubtitleGenerator` invokes `ffmpeg` via `ProcessStartInfo`/`ArgumentList` (verified by inspecting the built `ArgumentList`, not a shell string) and produces a valid non-empty `.vtt` file from a sample `.srt`. |
| FR4 | Given an unchanged `(video, srt)` pair, a second `Reconcile` call does not enqueue a new `SubtitleJob` once the cache already reports `Ready` for that key; given the `.srt` file's content/timestamp changes, the computed cache key changes and a new job is enqueued. A same-named `movie.mp4`/`movie.srt` pair present both in the Video Library root and in a distinct Archive Browser category produces two distinct cache keys/entries, not a collision. |
| FR5 | The generated `.vtt` file is written under `<ThumbnailCacheOptions.Path>/subtitles/`; the original `movie.mp4`/`movie.srt` files' size and last-write time are unchanged after conversion, whether sourced from the Video Library root or any Archive Browser category. |
| FR6 | `VideoItemDto` and `ArchiveItemDto` compile with `SubtitleState` and `SubtitleUrl` members; existing call sites (`VideoEndpoints.BuildDto`, `ArchiveEndpoints.ToDto`/`ToDtoAsync`, `PersistentPlayerState.SelectArchiveVideo`) are updated and the solution builds. |
| FR7 | `POST /api/videos/scan`, `GET /api/videos`, and every `GET /api/archive/{category}/items` response (including after create-folder/rename/move/trash mutations) return items with `SubtitleState`/`SubtitleUrl` populated from live coordinator state, and each call path calls `SubtitleCoordinator.Reconcile`. |
| FR8 | `GET /api/videos/{id}/subtitle` and `GET /api/archive/{category}/items/{id}/subtitle` each return `200` with `Content-Type: text/vtt` and the file body when the id's subtitle state is `Ready`; return `404` when `Unavailable`/`Pending`/`Failed`, and `404` for an unresolvable/unknown id or category. |
| FR9 | Rendered markup for a selected video with `SubtitleState.Ready` includes a `<track kind="subtitles" srclang="en" default src="...">` inside the `<video>`, whether the selection came from `SelectVideo`, `SelectArchiveVideo`, `SelectCut`, or `SelectComposition`; rendered markup for any other state includes no `<track>` element. |
| FR10 | `Player.razor.css` contains a `video::cue` rule setting `color: yellow`, `font-family` starting with `Arial`, and `font-weight: bold`. |
| FR11 | A forced FFmpeg failure (e.g. a corrupt `.srt` fixture) is recorded via `SubtitleCoordinator.MarkFailed` and is not re-enqueued on the next `Reconcile` call within the same process; the corresponding video still returns `200` from its `/stream` endpoint. |
| FR12 | With `SubtitleState.Ready`, `MediaPlayerControls.razor` renders an enabled `bi-card-text` button with `aria-pressed="true"` when `MediaPlayerState.IsSubtitlesEnabled` is true; the button is disabled when the state is not `Ready`. |
| FR13 | Clicking the toggle button flips `MediaPlayerState.IsSubtitlesEnabled` and invokes `setSubtitlesEnabled` via JS interop without changing `_video.src`, `_player.CurrentTime`, or playback state; selecting a new video resets `IsSubtitlesEnabled` to `true`. |
| FR14 | `dotnet test` (via `make test`) passes with new/updated tests covering the cache key (including cross-source collision-freedom), coordinator resolution/reconciliation for both a library-sourced and archive-sourced entry, generator argument-building and failure/cancellation handling, both subtitle endpoints' Ready/not-Ready/not-found paths, and the toggle-button state model. |

## Test Cases

**Unit tests (`WebApp.Tests/Services/`, xUnit, following `ThumbnailCacheTests.cs`/`ThumbnailCoordinatorTests.cs`/`FfmpegThumbnailGeneratorTests.cs` conventions):**

- `SubtitleMatcherTests` — matches `movie.mp4` → `movie.srt` in the same directory; no match when the `.srt` is absent, in a different folder, or has a different base name; case-sensitivity is exercised explicitly (documented as expected/by-design, not a bug). Exercised against both a `VideoLibraryService`-style and an `ArchiveEndpoints.ToMediaEntry`-style `VideoFileEntry`.
- `SubtitleCacheTests` — `ComputeKey` changes when the video's size/last-write time changes, when the subtitle's size/last-write time changes, and is stable for repeated calls with identical inputs; a same-named video with distinct `RelativePath` values (library root vs. an archive category prefix) produces distinct keys; `GetFinalPath`/`GetTemporaryPath` resolve under a `subtitles/` subdirectory of the configured root and reject any path that would escape it (mirroring `ThumbnailCacheTests`' containment tests).
- `SubtitleCoordinatorTests` — `Resolve` returns `Unavailable` with no matched subtitle, `Pending` when matched but not yet cached, `Ready` once the cache file exists and is non-empty, and `Failed` after `MarkFailed`; `Reconcile` enqueues exactly one job per matched-but-not-ready entry and does not re-enqueue an already-`Ready` or already-`Failed` entry; verified with entries from both a library-shaped and an archive-shaped `VideoFileEntry`.
- `SubtitleJobQueueTests` — duplicate `TryEnqueue` calls for the same cache key are rejected while a job is active, mirroring `ThumbnailJobQueueTests`.
- `FfmpegSubtitleGeneratorTests` — builds the expected `ffmpeg` `ArgumentList` for a given source/destination pair; returns `Success` for a valid conversion (or a faked process result, consistent with how `FfmpegThumbnailGeneratorTests` isolates process invocation); returns `Failed` on non-zero exit code without leaving a partial `.vtt` at the final path; returns `Cancelled` when the supplied `CancellationToken` is already cancelled.
- `MediaPlayerStateTests` (existing file, extended) — `IsSubtitlesEnabled` defaults to `true`; `Select(...)` with a new id resets it to `true`; toggling it via a new state method (or direct set, matching this class's existing mutation style) flips the value without affecting unrelated state (`IsMuted`, markers, etc.).

**Endpoint tests (`WebApp.Tests/Endpoints/`, `WebApplicationFactory`, following the existing thumbnail/preview endpoint test conventions):**

- `GET /api/videos/{id}/subtitle` returns `404` for an unknown id; `404` when the resolved state is not `Ready`; `200` with `Content-Type: text/vtt` when `Ready` (seed the cache directory with a fixture `.vtt` in the test host, as the existing thumbnail endpoint tests seed a fixture `.jpg`).
- `GET /api/archive/{category}/items/{id}/subtitle` returns the same `404`/`404`/`200`+`text/vtt` behavior for an archive-sourced video, using the same fixture-seeding approach as the existing `ArchiveEndpoints` thumbnail/preview tests.
- `POST /api/videos/scan` and `GET /api/videos` responses include `SubtitleState`/`SubtitleUrl` fields with correct values for a fixture library containing a matched pair and an unmatched video.
- `GET /api/archive/{category}/items` responses include `SubtitleState`/`SubtitleUrl` fields with correct values for a fixture archive folder containing a matched pair and an unmatched video, and folder/non-video items keep `SubtitleState.Unavailable`/`null`.

**Client tests (`WebApp.Tests/Client/`, if this repo's existing convention covers Razor markup/state assertions for `Player.razor`/DTOs):**

- `PersistentPlayerState.SelectArchiveVideo` construction test confirming it now forwards a resolved `ArchiveItemDto`'s real `SubtitleState`/`SubtitleUrl` into the synthesized `VideoItemDto`, rather than hardcoding `Unavailable`/`null`.
- `VideoItemDto`/`ArchiveItemDto` construction tests confirming the new fields compile and default correctly.
- ⚠️ TODO: a `Player.razor`/`MediaPlayerControls.razor` bUnit-style rendering test covering the `<track>` presence/absence and the toggle button's enabled/disabled and pressed/unpressed rendering (if this repo's test project doesn't already include a bUnit dependency, evaluate whether adding one is proportionate for these two conditional-markup checks, or whether manual verification below is sufficient for this MVP).

**Integration/environment-backed:**

- ⚠️ TODO: a Docker-container-level check that the `ffmpeg` binary in the built image successfully converts a real multi-cue `.srt` fixture to `.vtt` (can reuse the existing `make docker-shell`/`make dotnet ARGS="test"` flow); the unit tests above fake/isolate the process boundary rather than requiring FFmpeg to be present on the test runner.

## Manual Verification

1. `make docker-run-bg` to start the stack.
2. On the host `VIDEO_ROOT` (e.g. `/home/PereneArchive/Videos`), place a test folder containing `movie.mp4` and a valid multi-cue `movie.srt`. Also place a second, differently-named `clip.mp4` + `clip.srt` pair somewhere inside a non-Videos archive category folder (e.g. under Trash or another configured category) to exercise the Archive Browser path.
3. Open the app, go to the Video Library section, and click Scan.
4. Confirm the video card appears and, after a short delay (subtitle conversion running in the background), confirm via `GET /api/videos` (or a repeated poll in the UI) that the item's `subtitleState` becomes `Ready`.
5. Select the video in `Player.razor`; open the browser's native captions/subtitles menu on the `<video>` element and confirm an "English" track is listed and is enabled by default.
6. Confirm the subtitle text renders in **bold yellow Arial/sans-serif**, matching the SRT's cue timing.
7. Click the new `bi-card-text` toggle button in the player controls; confirm captions disappear immediately without the video reloading, pausing, or losing its current playback position. Click it again and confirm captions reappear.
8. Navigate to the Archive Browser, browse to the folder containing `clip.mp4`/`clip.srt`, select it, and repeat steps 5-7 to confirm the same subtitle behavior (detection, `<track>`, styling, and toggle) works identically for an archive-sourced video.
9. Remove `movie.srt` (or select a video with no subtitle in either the Video Library or Archive Browser) and rescan/re-list; confirm the video still plays normally with no `<track>` rendered, the toggle button shown disabled, and no console/UI error.
10. Replace `movie.srt` with a corrupt/invalid file, rescan, and confirm the video still plays (no subtitle track, toggle disabled), no error is shown to the user, and the app logs a warning (via `SubtitleBackgroundWorker`'s logger, mirroring `ThumbnailBackgroundWorker`).
11. `make docker-exec` into the running container and confirm both generated `.vtt` files exist under `/previews/subtitles/` (with distinct cache keys/filenames for the library video vs. the archive video) and that all four original media/subtitle files on the bind mount are byte-for-byte unchanged.

## Definition of Done

- Requirements, Plan, and Validation docs in this folder are complete and internally consistent.
- `make test` passes with the new/updated subtitle test coverage described above.
- `Player.razor`/`Player.razor.css`/`MediaPlayerControls.razor` changes render correctly with the subtitle track present, absent, pending, and failed, and the toggle button correctly enabled/disabled and pressed/unpressed, for both a Video-Library-selected and an Archive-Browser-selected video — verified per the manual steps above (this repo has no browser-automation harness, so this step is explicitly manual, not scripted).
- No physical or root-relative filesystem path is ever exposed to `WebApp.Client` or logged — spot-checked in the new endpoint/service code and in `ILogger` call sites (`SubtitleBackgroundWorker` should log the video's opaque `Id`, not any path, matching `ThumbnailBackgroundWorker`).
- `AGENTS.md`'s FFmpeg-pipeline Constraints bullet and Repository Map/Architecture Summary are updated to name this fourth pipeline as covering both the Video Library and every Archive Browser category (per the `init-agent` workflow), including reconciling the pre-existing gap where the hover-preview pipeline is implemented but not yet listed there.
- No new Docker volume, options section, or native (non-Docker) run/test workflow was introduced.

## Rollback Plan

- The feature is additive and isolated: reverting the commit(s) removes the new `Subtitle*` services, both `.../subtitle` routes (`VideoEndpoints` and `ArchiveEndpoints`), the `VideoItemDto`/`ArchiveItemDto` fields, the `Player.razor`/`Player.razor.css`/`MediaPlayerControls.razor`/`videoEditor.js` changes, and the `Program.cs` DI registrations, with no data migration involved.
- If a regression is found post-deploy without a full revert, the fastest disable path is to stop registering `SubtitleBackgroundWorker`/`ISubtitleJobQueue` in `WebApp/WebApp/Program.cs` (no new jobs get processed for either the Video Library or Archive Browser) while leaving `SubtitleCoordinator.Resolve` in place — because `Resolve` only ever reports `Ready` when a `.vtt` already exists on disk, videos simply stop gaining new subtitle tracks (and the toggle button stays disabled) without breaking existing playback or requiring a config flag.
- Any already-generated `.vtt` files under `/previews/subtitles/` are disposable cache artifacts (like existing thumbnail/hover-preview cache files) and can be deleted without affecting source media in either the Video Library root or any Archive Browser category; `make docker-reset` already covers full cache-volume cleanup if needed.
