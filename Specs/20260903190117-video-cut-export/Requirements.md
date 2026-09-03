# Requirements: Save Cut (A/B Video Export)

## Table of Contents

- [Requirements: Save Cut (A/B Video Export)](#requirements-save-cut-ab-video-export)
  - [Problem Statement](#problem-statement)
  - [User Stories](#user-stories)
  - [Functional Requirements](#functional-requirements)
  - [Non-Functional Requirements](#non-functional-requirements)
  - [Out of Scope](#out-of-scope)
  - [Open Questions](#open-questions)

## Problem Statement

`VerticalVideoEditor.razor` already lets a user set point A and point B (`MediaPlayerState.MarkerA`/`MarkerB`, surfaced by `MediaPlayerControls.razor`'s "Loop points" button group) and preview an A→B loop in the browser, but there is no way to turn that selection into a standalone video file. Today the only server-side FFmpeg use is the narrow, already-approved static-thumbnail pipeline (`Specs/20260831104814-static-video-thumbnails-ffmpeg/`); there is no cut/export pipeline, no writable location under the video library for derived clips, and no library-style browsing UI for anything other than the original scanned videos. This feature adds a "Save Cut" action that exports the A–B selection as its own file, alongside the pipeline and UI needed to produce, track, and browse those exported cuts.

## User Stories

- Given a selected video with a valid A/B range (`MediaPlayerState.HasValidAbRange`), when the user clicks "Save Cut", then a background job starts that produces a new video file containing only the A→B content at the source's original resolution and original audio quality.
- Given a Save Cut job has completed, when the user looks at the Home page, then a new "Cuts" section below the vertical editor lists the produced cut without a manual rescan.
- Given the Cuts section lists a produced cut, when the user clicks it, then it loads into the same `VerticalVideoEditor` used for library videos, in the center of the page.
- Given two different source videos share the same first two words after cutting (e.g. two different "Jennifer White" videos), when the user cuts each of them, then each cut gets the next unused number in that shared name's sequence (`0001`, `0002`, ...); a source whose first two words haven't been used yet starts its own sequence at `0001`.
- Given the Save Cut job is still running, when the user looks at the button, then it shows a busy/disabled state so a second click can't enqueue a duplicate job for the same selection.

## Functional Requirements

1. FR1 — `MediaPlayerControls.razor`'s "Loop points" button group gets a new button using `<i class="bi bi-download"></i>`, `title`/`aria-label`/`data-bs-title` "Save Cut", following the same Bootstrap tooltip/icon-button pattern as the adjacent A/B buttons. It is `disabled` unless `State.HasValidAbRange` is true.
2. FR2 — Clicking the button raises a new `EventCallback` (e.g. `SaveCut`) up through `VerticalVideoEditor.razor`, which calls a new `POST /api/videos/{id}/cuts` endpoint with the current `MarkerA`/`MarkerB` (seconds) in the request body.
3. FR3 — The endpoint resolves `id` through the existing `IVideoLibraryService.TryResolve` (only current-snapshot opaque IDs are accepted, matching `StreamAsync`/`GetThumbnail`), validates `0 <= start < end <= duration` server-side (re-probing duration via the existing `IVideoDurationProbe` rather than trusting client-sent duration), and enqueues a cut job; it does not perform the cut inline. It returns `202 Accepted` with a job id, or `400`/`404` for invalid input/unresolvable id.
4. FR4 — A new bounded, deduplicated job queue and `BackgroundService` (mirroring `IThumbnailJobQueue`/`ThumbnailJobQueue`/`ThumbnailBackgroundWorker`) drains cut jobs one at a time and invokes a new `ICutGenerator`/`FfmpegCutGenerator` service.
5. FR5 — `FfmpegCutGenerator` shells out to `ffmpeg` only via `ProcessStartInfo`/`ArgumentList` (never a shell string), re-validates the source file against the job's captured size/last-write time before running (mirroring `FfmpegThumbnailGenerator.SourceMatches`), and stream-copies (`-c copy`) both the video and audio streams between the requested start/end so the output keeps the source's original resolution, codec, and audio quality bit-for-bit. Because `-c copy` cuts land on the nearest keyframe at or before the requested start, the produced clip's actual start may be at or slightly before point A; the job records the actual start it used.
6. FR6 — The output file is written to `<VIDEO_ROOT>/Cuts/<name>.mp4` where `<name>` is `"<first two whitespace-separated words of the source file name (without extension)> <counter>"`, `<counter>` is a 4-digit zero-padded integer (`0001`, `0002`, ...). The counter is scoped per distinct first-two-words prefix (case-insensitive compare) and is derived by listing the existing files already in `Cuts/` matching that prefix and taking `max + 1` (no separate persisted counter store), so it survives restarts and stays correct even if files are added/removed externally. A source name with fewer than two words uses all of its words as the prefix.
7. FR7 — The generator writes to a temporary file inside `Cuts/` and atomically publishes (`File.Move`, no overwrite) to the final name only after verifying the output is a non-empty, readable file, matching the existing thumbnail generator's publish pattern; on failure or cancellation the temporary file is removed and no partial file is left under its final name.
8. FR8 — A new `GET /api/cuts` endpoint lists the current contents of `<VIDEO_ROOT>/Cuts` (scanned the same way `IVideoLibraryService` scans the main root: extension allowlist, symlink/reparse-point skip, canonical-path containment check) and returns `VideoItemDto`-shaped entries (reusing the existing DTO; thumbnail/hover-preview state may be `Unavailable` for cuts, see FR11) addressed by their own opaque, snapshot-scoped IDs — never a physical or root-relative path.
9. FR9 — A new `GET /api/cuts/{id}/stream` endpoint streams a resolved cut file the same way `StreamAsync` streams library videos (range-enabled, opaque-ID-only resolution, 404 on failure).
10. FR10 — `Home.razor` renders a new "Cuts" section below `<VerticalVideoEditor>`, reusing `VideoLibrary.razor` (or a shared sub-component extracted from it per the Component Breakdown in `Plan.md`) for the grid/empty/loading states. It loads the current cuts on page load via `GET /api/cuts`, and after a Save Cut job is enqueued, polls (same two-second interval and pending-aware stop condition already used for thumbnails in `Home.razor`) until the new cut appears, without requiring a manual rescan.
11. FR11 — Clicking a cut in the Cuts section sets it as `VerticalVideoEditor`'s `Selected`, exactly as `SelectVideo` does today for library videos; the editor streams it from `GET /api/cuts/{id}/stream` instead of `/api/videos/{id}/stream`, and A/B markers, crop, saturation, etc. all work identically since the cut is just another playable video. Cuts do not need their own generated thumbnails/hover previews for this feature (a placeholder film icon is an acceptable card state).

## Non-Functional Requirements

- FFmpeg invocation for cuts follows the same `ProcessStartInfo`/`ArgumentList`-only, no-shell-string constraint as the existing thumbnail/hover-preview generators (`WebApp/WebApp/Services/FfmpegThumbnailGenerator.cs`, `FfmpegHoverPreviewGenerator.cs`).
- The cut job queue/worker must not block the request thread; `POST /api/videos/{id}/cuts` returns immediately after enqueueing, matching the non-blocking reconcile pattern in `VideoLibraryService.ScanAsync`.
- All cut-related endpoints must resolve videos/cuts only through opaque, snapshot-scoped IDs; no physical or root-relative path may reach the browser or be logged (same boundary as `Constraints` in `AGENTS.md`).
- The `Cuts` service/generator/queue/worker are separated by responsibility the same way the thumbnail pipeline is (`ICutGenerator` interface, dedicated coordinator/queue/worker classes) so each piece stays independently testable with fakes, per the existing `WebApp.Tests/Services` conventions.
- The design should keep working when the Cuts folder is briefly unavailable/not yet created (e.g. surface a scan/job failure) rather than crashing the host.

## Out of Scope

- Re-encoding, frame-accurate trimming, or any quality/format conversion of the cut beyond the source's own codec/resolution/audio (stream-copy only, per the confirmed cut-precision decision).
- Thumbnails or hover previews specifically for cut videos.
- Deleting/renaming/managing existing cuts through the UI (only creation and browsing).
- Editing/re-cutting an already-produced cut, or cutting a cut.
- Cross-machine or multi-user job coordination; this remains the existing single-process, local-only app.
- Changing the FFmpeg authorization for anything beyond this narrowly scoped cut pipeline and the already-approved thumbnail pipeline.

## Open Questions

- ⚠️ TODO: None outstanding — cut precision (stream-copy, keyframe-snapped), counter scoping (per first-two-words prefix), and the Cuts mount strategy were confirmed with the user during discovery (see `Plan.md` for the resulting design).
