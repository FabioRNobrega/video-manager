# Requirements: External Subtitle Support

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

Neither the Video Library section nor the Archive Browser has subtitle support: `WebApp/WebApp/Services/VideoLibraryService.cs` and `WebApp/WebApp/Services/ArchiveService.cs` only enumerate video files (`.mp4/.webm/.mov/.m4v`), and `WebApp/WebApp/Endpoints/VideoEndpoints.cs`/`ArchiveEndpoints.cs` build DTOs with no subtitle information, so `Player.razor` — the single persistent player used by both sections plus Cuts and Video Compositions (`PersistentPlayerState`) — renders a bare `<video>` element with no `<track>` and no way to toggle captions. A user who keeps a same-named `.srt` file next to a video anywhere in the archive (e.g. `movie.mp4` + `movie.srt` in the same folder, a convention already used by NAS/media-server tooling) currently has no way to see those subtitles in the app. Browsers cannot render `.srt` natively — they require WebVTT — and the browser must never read the NAS filesystem directly to discover or convert that file, so this has to be a server-side pipeline consistent with the app's existing opaque-ID/no-physical-path boundary (`AGENTS.md` Constraints).

## User Stories

- Given a video's folder also contains a same-named `.srt` file — whether that video lives in the main Video Library or anywhere in the Archive Browser (any category/folder) — when that folder is scanned or listed, then the backend detects the match and queues a one-time SRT→WebVTT conversion using the FFmpeg already installed in the app container.
- Given a matching subtitle has been converted, when the browser loads that video's metadata, then it receives a subtitle language and a controlled HTTP URL — never a filesystem path — for the generated `.vtt` file.
- Given a video with a ready subtitle is selected in `Player.razor`, when the video plays, then a native `<track>` subtitle is available, is on by default, and renders as bold yellow Arial/sans-serif text.
- Given a video with a ready subtitle is playing, when the user clicks the subtitle toggle button in the player controls, then captions turn off; clicking it again turns them back on — without reloading or restarting playback.
- Given a video has no matching `.srt`, or its conversion has not finished or failed, when that video plays, then the player behaves exactly as it does today (no track, no toggle button shown as enabled, no error, no blocked playback).
- Given the same `movie.srt` is scanned again unchanged, when the library or archive folder is scanned or polled again, then FFmpeg is not re-invoked; the previously generated `.vtt` is reused (mirrors the existing thumbnail/hover-preview reconciliation behavior).

## Functional Requirements

1. FR1 - `VideoLibraryService.ScanAsync` and `ArchiveService`'s listing path (via the shared `VideoFileEntry` shape, e.g. `ArchiveEndpoints.ToMediaEntry`) must each, for every scanned/listed video, check for a sibling file with the same base name and a `.srt` extension in the same folder, using `Path.ChangeExtension`-style exact (case-sensitive on Linux) matching — one subtitle file per video, no language variants. This must apply uniformly to the main Video Library and to every Archive Browser category/folder (Videos, Cuts, Video Compositions, Trash, and any other configured category).
2. FR2 - Subtitle detection and the SRT→VTT conversion must run entirely in `WebApp` (the ASP.NET Core host); `WebApp.Client` (Blazor WebAssembly) must never receive or evaluate a filesystem path.
3. FR3 - When a matching `.srt` is found, the backend must convert it to WebVTT using the FFmpeg binary already installed in the app container (`Dockerfile`), invoked only via `ProcessStartInfo`/`ArgumentList` (never a shell string), following the existing `IThumbnailGenerator`/`FfmpegThumbnailGenerator` pattern in `WebApp/WebApp/Services/`.
4. FR4 - The conversion must run at most once per unchanged `(source video, source subtitle)` pair: it must be keyed off a content-identity cache key (path + size + last-write time, like `ThumbnailCache.ComputeKey`) covering both the video and subtitle file, so unchanged pairs are never reconverted on subsequent scans or polls, and playback requests never trigger FFmpeg. This dedup must hold across both the Video Library and every Archive Browser category, using one shared coordinator/cache instance exactly as `ThumbnailCoordinator`/`HoverPreviewCoordinator` are already shared today between `VideoEndpoints` and `ArchiveEndpoints`.
5. FR5 - Generated `.vtt` files must be written to a new subdirectory of the existing writable preview cache (`ThumbnailCacheOptions.Path`, mounted at `/previews`), following the `HoverPreviewCache` precedent of namespacing a subdirectory (e.g. `subtitles/`) inside the same cache root rather than provisioning a new Docker volume; the original `movie.mp4`/`movie.srt` files must never be modified, moved, or deleted, in either the Video Library root or any Archive Browser category.
6. FR6 - `VideoItemDto` and `ArchiveItemDto` (`WebApp.Client/Models/`) must both be extended with subtitle availability state and a browser-safe subtitle URL, following the existing `ThumbnailState`/`ThumbnailUrl` and `HoverPreviewState`/`HoverPreviewUrl` pattern (reuse or mirror the same `Unavailable/Pending/Ready/Failed` state enum) — matching how both DTOs already carry parallel thumbnail/hover-preview fields.
7. FR7 - `VideoEndpoints.BuildDto` and `ArchiveEndpoints`'s `ToDto`/`ToDtoAsync` helpers must each populate the new subtitle fields from the same shared subtitle coordinator/cache resolution, exactly as they already do for thumbnails and hover previews, and must reconcile (enqueue missing conversions) on `POST /api/videos/scan`, `GET /api/videos` polling, and every `GET /api/archive/{category}/items` listing/mutation response.
8. FR8 - A new `GET /api/videos/{id}/subtitle` endpoint and a new `GET /api/archive/{category}/items/{id}/subtitle` endpoint must each serve the generated `.vtt` file as `text/vtt` only when the resolved subtitle state is `Ready` for that item's current snapshot/listing entry (404 otherwise), following the exact pattern of the existing `.../thumbnail` and `.../preview` endpoints in `VideoEndpoints.cs` and `ArchiveEndpoints.cs` — no physical or root-relative path is ever placed in the URL or response.
9. FR9 - `Player.razor`'s `<video>` element must render a child `<track kind="subtitles" srclang="en" label="English" default>` whose `src` is the selected item's subtitle URL only when its subtitle state is `Ready`, regardless of whether the selection came from the Video Library, Archive Browser, Cuts, or Video Compositions (i.e. driven off the single shared `VideoItemDto`/`PersistentPlayerState.Selected` already used by `Player.razor` today); it must render no `<track>` at all otherwise.
10. FR10 - Subtitle cues must render as bold yellow Arial/sans-serif text via a `video::cue` CSS rule scoped to the player (in `Player.razor.css`, following this project's existing convention of isolated CSS only for behavior Bootstrap cannot express).
11. FR11 - A failed conversion must be recorded (mirroring `ThumbnailCoordinator.MarkFailed`) so it is not retried every scan/poll cycle within the process lifetime, and must never surface a user-facing error — the video must remain fully playable without subtitles.
12. FR12 - `Player.razor`/`MediaPlayerControls.razor` must add a subtitle toggle button using `<i class="bi bi-card-text"></i>`, placed and styled consistent with the existing toggle buttons (e.g. Mute, Repeat, A/B Loop — `btn-primary` when active vs. `btn-outline-primary` when inactive, `aria-pressed`, a Bootstrap tooltip, and a minimum 40×40 CSS-pixel target per `AGENTS.md`'s Design System conventions). The button must be disabled (or hidden, consistent with how other conditional controls in this component behave) whenever the selected item's subtitle state is not `Ready`, and must reflect whether captions are currently showing.
13. FR13 - Clicking the subtitle toggle button must flip the native `<track>`'s active/showing state (via JS interop in `videoEditor.js`, following the existing `setVolume`/`setMuted`/`setPlaybackRate`/`setLoop` interop pattern already used by `Player.razor.ApplyPlayerPreferencesAsync`/`ExecuteMediaCommandAsync`) without reloading the `<video>` element or interrupting playback; the toggle state must reset to "on" (matching the `<track>`'s `default` attribute) whenever a new video is selected, mirroring how `MediaPlayerState.Select` resets other per-video preferences.
14. FR14 - The implementation must add or update focused tests for: the subtitle cache key/path computation; the coordinator's state resolution and reconciliation across both a `VideoLibraryService`-sourced and an `ArchiveService`-sourced `VideoFileEntry`; the FFmpeg SRT→VTT generator's argument building and success/failure/cancellation handling; both new `/subtitle` endpoints' Ready/not-Ready/not-found behavior; and the `MediaPlayerState`/DTO changes backing the toggle button's enabled/disabled and pressed/unpressed states — following this repo's existing `WebApp.Tests/Services` and `WebApp.Tests/Endpoints` conventions (see `ThumbnailCacheTests.cs`, `ThumbnailCoordinatorTests.cs`, `FfmpegThumbnailGeneratorTests.cs`).

## Non-Functional Requirements

- Security/privacy: preserve the existing opaque-ID and no-physical-path boundary (`AGENTS.md` Constraints) — the subtitle URL must be an opaque, snapshot-scoped API path exactly like the thumbnail/hover-preview/stream URLs, never a NAS path.
- FFmpeg scope: this is a new, narrowly scoped FFmpeg exception (SRT→WebVTT subtitle conversion only) alongside the three documented in `AGENTS.md` Constraints (static thumbnails, cut export, composition) and the already-implemented but undocumented hover-preview pipeline; it must not be used for any other transcoding.
- Testability: keep the conversion behind a small `ISubtitleGenerator`-style interface (mirroring `IThumbnailGenerator`) so it can be faked/mocked in tests, and keep path/cache-key logic in a plain class separate from the background worker.
- Design system: any new UI element (or the absence of one, since native browser subtitle UI is used) must not require changes outside `Player.razor`/`Player.razor.css`; no new Bootstrap component is needed for this MVP.
- Compatibility: the app remains Docker Compose-only through the existing `Makefile`/`docker-compose.yml` workflow; no new named Docker volume is required (reuse `/previews`).
- Font dependency: `video::cue` styling depends on Arial (or a sans-serif fallback) being available on the viewer's device; this is a client-rendering concern, not an FFmpeg/container concern, and is acceptable for this MVP.

## Out of Scope

- Multiple subtitle languages or multiple `.srt` files per video (e.g. `movie.en.srt`, `movie.pt-BR.srt`) — explicitly the next planned feature after this MVP, without changing this architecture.
- A subtitle-language selection UI; only one always-on `en` track is exposed. The new toggle button only shows/hides that single track — it is not a language picker.
- User-uploaded subtitles, subtitle synchronization/offset adjustment, font-size controls, cue positioning controls, and automatic subtitle downloading.
- Case-insensitive or fuzzy filename matching between the video and `.srt` file.
- Persisting the subtitle on/off toggle preference across sessions or across video selections (each new selection resets to "on", matching the `<track>`'s `default` attribute; no browser-storage or server-side preference is introduced).
- Bundling a web font so subtitle typography is identical across client devices.
- Any change to the read-only/read-write posture of the source video/archive bind mount.

## Open Questions

None — both prior open questions are resolved: subtitle detection/conversion applies uniformly to the Video Library and every Archive Browser category/folder (FR1, FR4-FR8), and `Player.razor` gets a `bi-card-text` toggle button to show/hide the active track (FR12-FR13).
