# Agent Context

## Table of Contents

- [Agent Context](#agent-context)
  - [Project Overview](#project-overview)
  - [Repository Map](#repository-map)
  - [Architecture Summary](#architecture-summary)
  - [Execution Environment](#execution-environment)
  - [Coding Conventions](#coding-conventions)
  - [Design System](#design-system)
  - [Microsoft Learn MCP Server](#microsoft-learn-mcp-server)
  - [Constraints](#constraints)
  - [Available Commands](#available-commands)
  - [Spec-Kit Workflow](#spec-kit-workflow)
  - [Key Documentation](#key-documentation)

## Project Overview

Perene Tech Videos is an implemented .NET 10 Blazor Web App (server + Interactive WebAssembly client) for locally browsing and manually reframing vertical video. It reads a user-selected host directory through a Docker-only, read-only bind mount, exposes only snapshot-scoped opaque IDs to the browser, streams supported files with range processing, and provides a draggable 9:16 preview with custom playback, A/B looping, dark/light themes, and Fill-tab mode. A narrowly scoped FFmpeg pipeline (`Specs/20260831104814-static-video-thumbnails-ffmpeg/`) generates static 640×360 JPEG card thumbnails into a separate writable cache volume, entirely server-side and without weakening the read-only source mount or opaque-ID boundary. A second narrowly scoped FFmpeg pipeline (`Specs/20260903190117-video-cut-export/`) stream-copies A/B selections into a separate read-write Cuts bind mount while keeping source videos read-only and browser IDs opaque. Its presentation follows the design system in `Specs/20260827194328-perene-tech-design-system-refactor/` without changing these data or behavior boundaries.

## Repository Map

- `WebApp/WebApp/` — ASP.NET Core host with startup composition in `Program.cs`, validated video-library/thumbnail-cache/video-cut configuration, the source and cut snapshot services, the thumbnail and cut queue/background-worker/FFmpeg-generator services, minimal video/cut endpoints, server-owned layout/error components, global `wwwroot/app.css`, and unreferenced legacy vendored Bootstrap assets.
- `WebApp/WebApp.Client/` — Interactive WebAssembly UI with `Home.razor`, shared video-grid/library/editor/theme/player components, browser-safe models/state objects (including `ThumbnailState`), Bootstrap-first static composition, scoped CSS for behavior-intensive editor/player presentation, and focused `theme.js`, `videoEditor.js`, and `bootstrapInterop.js` browser interop.
- `WebApp.Tests/` — xUnit tests organized under `Configuration/`, `Services/`, `Endpoints/`, and `Client/`, using `WebApplicationFactory` for host/endpoint/static-root checks and direct tests for C# state models.
- `video-manager.slnx` — solution file listing the three projects above.
- `Dockerfile` — `mcr.microsoft.com/dotnet/sdk:10.0` image with an installed `ffmpeg` OS package, running `dotnet watch run` for the web project on port 8080.
- `docker-compose.yml` — dev stack for the `webapp` service (hot reload, read-only source bind mount, read-write `${VIDEO_ROOT}/Cuts` bind mount, NuGet cache volume, and persistent `thumbnail_cache` named volume at `/previews`).
- `docker-compose.test.yml` — isolated `tests` service that restores and runs `dotnet test` in a throwaway container with its own disposable thumbnail cache volume.
- `Makefile` — single source of truth for build/run/test commands; wraps `docker compose` and detects the local Docker/Podman socket.
- `Specs/` — spec-kit folders (`<timestamp>-<slug>/Requirements.md`, `Plan.md`, `Validation.md`) documenting implemented and pending features; the latest design-system spec also contains its authoritative `design-guide-en.html` reference.
- `.env.example` — safe template for the required host `VIDEO_ROOT` and optional `WEBAPP_PORT`; the real `.env` remains ignored.
- `.gitignore` / `.dockerignore` — exclude build output, secrets, and the real `.env`.

## Architecture Summary

This is the standard Blazor Web App hosted-WebAssembly split: `WebApp` is the ASP.NET Core host that maps Razor components and minimal API endpoints; `WebApp.Client` is the Interactive WebAssembly project whose assemblies are added via `AddInteractiveWebAssemblyRenderMode().AddAdditionalAssemblies(...)` in `WebApp/WebApp/Program.cs`. `WebApp.Tests` references `WebApp` directly and already uses `Microsoft.AspNetCore.Mvc.Testing`'s `WebApplicationFactory`.

The implemented architecture is:

- **Configuration boundary**: `docker-compose.yml` requires `VIDEO_ROOT` from a repo-root `.env`, bind-mounts it read-only to a fixed internal path, and passes it to ASP.NET Core as `VideoLibrary__Path`. A `VideoLibraryOptions` type validates it's absolute, existing, and readable at startup. A parallel `ThumbnailCacheOptions` (`ThumbnailCache__Path`, default `/previews`) validates an absolute, existing, writable directory disjoint from the video root, backed by its own persistent `thumbnail_cache` named volume. `VideoCutOptions` (`VideoCut__Path`, default `/videos-cuts` in Compose) validates the separate read-write Cuts bind mount used only for exported cuts.
- **Server library snapshot**: a singleton `IVideoLibraryService`/`VideoLibraryService` recursively enumerates the mounted root (skipping symlinks/reparse points, canonical-path containment checks, extension allowlist `.mp4/.webm/.mov/.m4v`), captures each file's root-relative path/size/UTC last-write time, and holds an atomically-replaced in-memory snapshot mapping opaque IDs to internal `VideoFileEntry` records; `GetCurrentSnapshot()` exposes that snapshot without rescanning. No physical or root-relative path is ever sent to the browser.
- **Thumbnail pipeline** (`WebApp/WebApp/Services/`): `ThumbnailCache` derives a private SHA-256 cache key from an entry's relative path/size/last-write time and resolves contained `<key>.jpg` paths under the preview root; `ThumbnailCoordinator` resolves each entry's `Unavailable/Pending/Ready/Failed` state, tracks one-failure-per-process, and reconciles the current snapshot into `IThumbnailJobQueue` (a bounded, deduplicated `Channel`-backed queue); `ThumbnailBackgroundWorker` (a `BackgroundService`) drains one job at a time through `IThumbnailGenerator`/`FfmpegThumbnailGenerator`, which revalidates the source, probes its duration via `IVideoDurationProbe`/`FfprobeDurationProbe` to compute a duration-aware seek (~10% in, floored at 2s, capped at 10 min, falling back to a fixed 3s when duration is unknown), shells out to `ffmpeg` via `ProcessStartInfo`/`ArgumentList` (never a shell string), and atomically publishes a verified temp file to its final cache path. `VideoLibraryService.ScanAsync` reconciles thumbnail work after publishing a snapshot but never awaits FFmpeg.
- **Cut pipeline** (`WebApp/WebApp/Services/`): `POST /api/videos/{id}/cuts` resolves the source through the current library snapshot, validates A/B seconds against a fresh `ffprobe` duration, and enqueues a `CutJob` into `ICutJobQueue`. `CutBackgroundWorker` drains one job at a time through `ICutGenerator`/`FfmpegCutGenerator`, which revalidates source size/timestamp, computes the next `<first two words> NNNN.mp4` path with `CutNamingService`, invokes `ffmpeg` via `ProcessStartInfo`/`ArgumentList` with stream copy only (`-c copy`), publishes via temp-file-then-atomic-move, and refreshes `IVideoCutService` so `GET /api/cuts` and the UI can list the new opaque cut ID.
- **Minimal API endpoints** (`MapVideoEndpoints`/`MapCutEndpoints` in `WebApp/WebApp/Endpoints/`): `POST /api/videos/scan` triggers a rescan and returns browser-safe `VideoItemDto`s (from `WebApp.Client/Models/`) with a `ThumbnailState`/nullable thumbnail URL; `GET /api/videos` returns the current snapshot's DTOs without rescanning, for polling; `GET /api/videos/{id}/stream` resolves only current-snapshot IDs and returns a range-enabled file stream (404 otherwise); `GET /api/videos/{id}/thumbnail` resolves the same way and serves the `image/jpeg` cache file only when `Ready` (404 otherwise); `POST /api/videos/{id}/cuts` enqueues a server-side cut job; `GET /api/cuts` scans the cut root into its own opaque snapshot; `GET /api/cuts/{id}/stream` streams only resolved cut IDs with range processing — `/previews` and `/videos-cuts` are never mapped as static-file roots.
- **Interactive WebAssembly UI**: client-owned `Home.razor` coordinates `VideoLibrary.razor`, the shared `VideoGrid.razor`, the Cuts section, and `VerticalVideoEditor.razor`; it runs a cancellation-aware two-second poll of `GET /api/videos` while thumbnail work is pending and a separate cuts poll after Save Cut is queued. `VideoFrameState`, `FillTabState`, and `MediaPlayerState` own framing, presentation, and playback rules in C#; `MediaPlayerControls.razor` renders custom playback/A/B/Save Cut controls; `VideoLibrary.razor` renders a cover-sized thumbnail `<img>` in place of the `bi-film` placeholder once a card's state is `Ready`. Focused JavaScript in `theme.js` and `videoEditor.js` bridges browser storage, pointer/media DOM operations, and Fill-tab lifecycle without owning application rules.

### Request flow

`User → VideoLibrary.razor (Scan) → Home.razor → POST /api/videos/scan → VideoLibraryService.ScanAsync() → enumerates /videos (read-only mount) → new opaque-ID snapshot → ThumbnailCoordinator.Reconcile() enqueues missing thumbnails (non-blocking) → VideoItemDto list → user selects → VerticalVideoEditor sets src="/api/videos/{id}/stream" → range-enabled stream → video/crop events update C# state → object-position and MediaPlayerControls update`. In the background, `ThumbnailBackgroundWorker` sequentially runs `FfmpegThumbnailGenerator` per job and publishes cache files that `GET /api/videos/{id}/thumbnail` later serves; `Home.razor` polls `GET /api/videos` to replace placeholders with `Ready` thumbnails in place. For cuts, `MediaPlayerControls.razor (Save Cut) → VerticalVideoEditor.razor → POST /api/videos/{id}/cuts → ICutJobQueue → CutBackgroundWorker → FfmpegCutGenerator → /videos-cuts/<prefix> NNNN.mp4 → VideoCutService.ScanAsync() → GET /api/cuts → Home.razor Cuts section → VerticalVideoEditor src="/api/cuts/{id}/stream"`. Fill-tab changes only presentation around the same keyed video element; theme preference is applied early from browser-local storage.

## Execution Environment

This project is Docker Compose-only; there is no documented native `dotnet run` workflow. All commands go through the `Makefile`, which wraps `docker compose` and auto-detects a Docker or Podman socket via `DOCKER_HOST`.

- `make docker-build` — build the .NET 10 SDK image.
- `make docker-run` / `make docker-run-bg` — start the app (hot reload via `dotnet watch`) in foreground/background.
- `make docker-down` / `make docker-reset` — stop the stack, optionally deleting volumes.
- `make docker-logs`, `make docker-ps` — follow logs / list containers for the `video-manager` compose project.
- `make docker-shell` — open a shell in a fresh SDK container; `make docker-exec` — shell into the running `webapp` container.
- `make dotnet ARGS="build"` — run an arbitrary `dotnet` command inside the SDK image.
- `make test` (alias `make docker-test`) — run `WebApp.Tests` in the isolated `video-manager-test` compose project (`docker-compose.test.yml`), always tearing down volumes afterward regardless of test outcome.
- `make docker-test-shell` — shell into the test image without running tests.

Running the app requires a repo-root `.env` copied from `.env.example`, with `VIDEO_ROOT` set to an absolute host video directory and optional `WEBAPP_PORT`. Compose mounts the directory read-only at `/videos`, mounts `${VIDEO_ROOT}/Cuts` read-write at `/videos-cuts` (the host folder must exist before first run), supplies `VideoLibrary__Path`/`VideoCut__Path`, and publishes only on loopback.

## Coding Conventions

- `Nullable` and `ImplicitUsings` are enabled in every project; keep new code nullable-aware.
- The client (`WebApp.Client`) and server (`WebApp`) projects are kept as separate concerns: the server owns filesystem/path/config logic, the client owns Razor components, rendering, and browser-side state. Physical path manipulation stays confined to a server-only service.
- Browser-visible DTOs live in `WebApp.Client/Models/` (reusing the existing project reference from server to client) rather than a separate shared project.
- Static UI composition belongs in Razor markup using Bootstrap components and utilities. `wwwroot/app.css` owns design tokens, public Bootstrap variable/component mappings, document-level accessibility/behavior rules, and theme integration. Use isolated `.razor.css` only when Bootstrap cannot express required behavior, such as the video overlay gradient, range pseudo-elements, A/B marker geometry, Fill-tab layout, or player-specific responsive states; keep data-driven inline styles limited to runtime values such as crop, progress, volume, and marker positions.
- Custom JavaScript is kept minimal and isolated to what Blazor can't do natively (for example pointer capture, element/media geometry, and Bootstrap tooltip lifecycle) — no framing, playback, or application state belongs in JS.
- Tests are xUnit, organized by concern under `WebApp.Tests/Services`, `Endpoints`, and `Client`, and run exclusively through `make test` in an isolated Docker Compose stack, not on the host.

## Design System

- `Specs/20260827194328-perene-tech-design-system-refactor/design-guide-en.html` is the detailed visual source of truth for every current and future UI feature. `WebApp/WebApp/wwwroot/app.css` owns the shared palette, typography, semantic feedback, Bootstrap variable/component mappings, and document-level rules; Bootstrap classes and utilities own standard component/page composition, while narrowly scoped `.razor.css` files may supply video/editor presentation that Bootstrap cannot represent.
- The approved design contract is Bootstrap 5.3.8 plus Bootstrap Icons 1.13.1, Zilla Slab for headings/product titles, and Montserrat for body, labels, metadata, forms, and controls. For the current setup these assets are version-pinned CDN dependencies; complete presentation therefore requires Internet access, while declared font fallbacks and semantic controls must remain usable when a CDN is unavailable.
- Use real Bootstrap components/classes and their public CSS custom properties wherever the guide defines a matching pattern. The hierarchy decision is gold-filled `btn-primary` for the one principal screen action and green-filled `btn-secondary` for complementary actions; semantic variants are reserved for matching feedback or destructive/confirming meaning.
- Use Bootstrap Icons at `currentColor`, not hand-authored SVGs, emoji, or text glyphs for interface icons. Icon-only controls require an accessible name, visible focus, programmatic toggle state where applicable, a minimum 40×40 CSS-pixel target, and a Bootstrap tooltip or equivalent documented fallback.
- Apply the same system to the complete video-player menu: timeline/time, playback, audio, speed, whole-video loop, Fill-tab, A/B marker/loop, Clear, validation, and error regions must remain a cohesive responsive Bootstrap toolbar in normal and Fill-tab modes.
- Preserve the guide's dark and Kindle-paper light tokens, WCAG AA normal-text contrast, non-color state cues, live status/error semantics, responsive states, and reduced-motion behavior. Do not hand-edit vendored/generated Bootstrap assets.
- Prefer Bootstrap before adding custom selectors. Any component-scoped CSS exception must stay local to behavior Bootstrap cannot represent—especially pseudo-elements, gradients, runtime marker/progress styling, overlay interaction states, and nonstandard responsive geometry—and must reuse the global design tokens where applicable.
- The governing implemented design-system spec is `Specs/20260827194328-perene-tech-design-system-refactor/`. New UI must follow this contract instead of extending the superseded styling.

## Microsoft Learn MCP Server

This repository is a Microsoft-stack project: .NET 10 (`net10.0`), ASP.NET Core (`Microsoft.NET.Sdk.Web`), Blazor WebAssembly (`Microsoft.NET.Sdk.BlazorWebAssembly`, `Microsoft.AspNetCore.Components.WebAssembly*`), and `Microsoft.AspNetCore.Mvc.Testing`. Use the Microsoft Learn MCP Server as the first-party documentation source for decisions involving these technologies:

- Start with `microsoft_docs_search` to locate current official guidance (e.g. ASP.NET Core configuration/options binding, minimal API file/range responses, Blazor WebAssembly render modes, CSS isolation).
- Use `microsoft_code_sample_search` when a decision depends on API usage or examples (e.g. `enableRangeProcessing`, `WebApplicationFactory` setup, pointer event handling in Blazor).
- Use `microsoft_docs_fetch` when complete prerequisites, version notes, or procedures are needed (e.g. verifying `Microsoft.AspNetCore.Mvc.Testing` version compatibility with `net10.0`).
- Cite the relevant Microsoft Learn URLs and summarize evidence in plans/reviews when it affects a technical decision — the existing `Plan.md` already does this for several ASP.NET Core/Docker behaviors and is a model to follow.
- Reconcile Learn guidance with this repo's own documented constraints (privacy/loopback-only boundary, Docker-only execution, no new frontend framework); if they conflict, preserve the repo's constraint unless the user approves a change, and document why.
- Do not rely only on model memory for Microsoft-specific architecture/API/security/compatibility/version decisions. If the MCP server is unavailable, say verification is pending rather than presenting the decision as verified.

## Constraints

- Follow the spec-driven workflow in `Specs/`: new features get a `<timestamp>-<slug>` folder with `Requirements.md`, `Plan.md`, and `Validation.md` before implementation; do not implement ahead of an approved spec.
- Never commit a real `VIDEO_ROOT` value or any personal host filesystem path — `.env` is gitignored/dockerignored for exactly this reason; only `.env.example` (with a placeholder path) may be committed.
- No physical or root-relative filesystem paths may ever be exposed to the browser or written to normal application logs — all browser-facing video references must be opaque, snapshot-scoped IDs.
- The application is local-only by design: loopback-only binding, no authentication/authorization layer, no LAN/Internet hosting. Do not add remote-access features without an explicit spec update.
- FFmpeg (and `ffprobe`) are authorized only for the narrow static-JPEG card-thumbnail pipeline defined in `Specs/20260831104814-static-video-thumbnails-ffmpeg/` and the narrow A/B cut-export pipeline defined in `Specs/20260903190117-video-cut-export/`. Thumbnails produce one 640×360 JPEG per video at a duration-aware seek into `/previews`; cuts stream-copy (`-c copy`) a validated source selection into `/videos-cuts` as `<first two words> NNNN.mp4`. Both pipelines must invoke FFmpeg only via `ProcessStartInfo`/`ArgumentList` on validated `VideoFileEntry` sources. All other transcoding, media processing, or FFmpeg use beyond those specs remains out of scope.
- Do not hand-edit the vendored Bootstrap assets under `WebApp/WebApp/wwwroot/lib/bootstrap/`.
- All build/run/test workflows go through Docker Compose via the `Makefile`; do not introduce a native `dotnet run`/`dotnet test` workflow outside Docker unless the user asks for one.

## Available Commands

| Command | Purpose |
| --- | --- |
| `make docker-build` | Build the .NET 10 SDK image |
| `make dotnet-new` | (One-time scaffold step; already run) Generate the Blazor solution and xUnit project |
| `make docker-run` | Start the app with hot reload (foreground) |
| `make docker-run-bg` | Start the app in the background |
| `make docker-down` | Stop the app |
| `make docker-reset` | Stop the app and delete Docker volumes |
| `make docker-logs` | Follow application logs |
| `make docker-ps` | List running containers for this compose project |
| `make docker-shell` | Open a new .NET SDK container shell |
| `make docker-exec` | Open the running web container shell |
| `make dotnet ARGS="..."` | Run any `dotnet` command in Docker (e.g. `ARGS="build"`) |
| `make test` / `make docker-test` | Run tests in an isolated Docker Compose stack |
| `make docker-test-shell` | Open a shell in the test image without running tests |

## Spec-Kit Workflow

Features are designed before implementation under `Specs/<timestamp>-<slug>/`, each containing `Requirements.md`, `Plan.md`, and `Validation.md`. The discovery, theme, Fill-tab, custom-media-control, Perene Tech design-system, and static-video-thumbnails-ffmpeg specs are implemented. Use the `implement-spec` workflow to build the most recent unimplemented spec and `new-spec` to add another one.

## Key Documentation

- [design-guide-en.html](Specs/20260827194328-perene-tech-design-system-refactor/design-guide-en.html) — mandatory visual and component design reference colocated with the governing spec.
- [Specs/20260827194328-perene-tech-design-system-refactor/Requirements.md](Specs/20260827194328-perene-tech-design-system-refactor/Requirements.md) — approved product, asset, accessibility, and compatibility requirements.
- [Specs/20260827194328-perene-tech-design-system-refactor/Plan.md](Specs/20260827194328-perene-tech-design-system-refactor/Plan.md) — implemented technical design and official documentation evidence.
- [Specs/20260827194328-perene-tech-design-system-refactor/Validation.md](Specs/20260827194328-perene-tech-design-system-refactor/Validation.md) — acceptance criteria and Docker/browser validation procedure.
- [Specs/20260831104814-static-video-thumbnails-ffmpeg/Requirements.md](Specs/20260831104814-static-video-thumbnails-ffmpeg/Requirements.md) — the governing, narrow authority for the FFmpeg static-thumbnail exception; supersedes the prior blanket no-FFmpeg constraint only as scoped here.
- [Specs/20260831104814-static-video-thumbnails-ffmpeg/Plan.md](Specs/20260831104814-static-video-thumbnails-ffmpeg/Plan.md) — implemented thumbnail cache/queue/worker/FFmpeg design and official documentation evidence.
- [Specs/20260831104814-static-video-thumbnails-ffmpeg/Validation.md](Specs/20260831104814-static-video-thumbnails-ffmpeg/Validation.md) — acceptance criteria and Docker/browser validation procedure for the thumbnail pipeline.
