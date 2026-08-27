# Plan: Local Vertical Video Manager

## Table of Contents

- [Summary](#summary)
- [Technical Approach](#technical-approach)
- [Component Breakdown](#component-breakdown)
- [Dependencies](#dependencies)
- [External / Vendor Documentation Evidence](#external--vendor-documentation-evidence)
- [Flow](#flow)
- [Risk Assessment](#risk-assessment)

## Summary

Extend the existing .NET 10 hosted Blazor Web App with focused server-side library/streaming services and an Interactive WebAssembly root page. Docker Compose owns the private host-path boundary, ASP.NET Core exposes only snapshot-scoped opaque IDs, and the client owns all drag state and framing calculations.

## Technical Approach

### Configuration and privacy boundary (FR1-FR4, FR20)

Keep `.env` as user-owned Docker Compose input; do not add a .NET `.env` parser or runtime package. Add `.env.example` with `VIDEO_ROOT=/absolute/path/to/videos` and `WEBAPP_PORT=8080`. In `docker-compose.yml`, use Compose's required interpolation form for `VIDEO_ROOT`, a long-syntax read-only bind mount from that host path to `/videos`, and `VideoLibrary__Path: /videos` so ASP.NET Core's built-in environment-variable provider binds the hierarchical option.

Publish `127.0.0.1:${WEBAPP_PORT:-8080}:8080`, replacing the current all-interface mapping. Replace the wildcard `AllowedHosts` in `WebApp/WebApp/appsettings.json` with local host values. In `WebApp/WebApp/Program.cs`, bind and validate `VideoLibraryOptions` at startup, including required, absolute, existing-directory, and read-access checks. Docker's long mount syntax and Compose's required interpolation make a missing or nonexistent host source fail before the application starts; options validation independently protects the application boundary.

The repository already ignores `.env` and `.env.*` except `.env.example`, and `.dockerignore` already excludes `.env`, so those existing files require verification but no edit for this requirement.

### Server library snapshot (FR5-FR9, FR11)

Create a small singleton `IVideoLibraryService` implemented by `VideoLibraryService`. It exclusively owns recursive enumeration, canonical containment validation, media extension filtering, opaque ID allocation, and an immutable in-memory snapshot. No endpoint or Razor component manipulates physical paths directly.

`ScanAsync` performs work only after `POST /api/videos/scan`. It recursively enumerates with reparse points skipped, validates each canonical candidate against the canonical root with path-separator-aware comparison, accepts regular readable files from the extension allowlist, and creates a new random opaque ID for every entry. Filenames, extensions, and sizes are returned; physical and root-relative paths remain only in the internal server entry. A completed scan atomically replaces the snapshot. A scan failure atomically replaces it with an empty snapshot and returns a safe problem response, avoiding stale authorization through an old ID.

Because the hosted server project already references `WebApp.Client`, place the small browser-visible `VideoItemDto` contract in `WebApp.Client/Models` and map internal server entries to it at the endpoint boundary. This uses the existing project dependency instead of adding a shared project. Keep physical-path data in an internal server-only `VideoFileEntry`.

Map endpoints through a focused `MapVideoEndpoints` extension called from the existing minimal-host `Program.cs`:

- `POST /api/videos/scan` invokes the manual scan and returns the new DTO list.
- `GET /api/videos/{id}/stream` resolves the ID from the current atomic snapshot and returns a range-enabled file result, or `404` when the ID is invalid, stale, missing, or no longer readable.

Use a fixed extension-to-content-type allowlist matching the scan allowlist. The endpoint never accepts or reconstructs a path from route input. Concurrent streams may finish through their already-open file handle after a rescan, while new requests must use the new snapshot IDs.

### Interactive WebAssembly experience (FR10, FR13-FR19)

Replace the template server `Home.razor` route with a client-owned `WebApp.Client/Pages/Home.razor` at `/` and `@rendermode InteractiveWebAssembly`. Register a same-origin `HttpClient` in `WebApp.Client/Program.cs`. The page coordinates explicit scan state, the current DTO list, selection, and errors; it delegates rendering and interaction to focused client components.

`VideoLibrary.razor` renders the Scan/Rescan command and unscanned, scanning, empty, error, and populated states. Rows display only filename, extension, and a human-readable size, and expose selection with both visual and semantic state. A rescan clears selection before requesting a replacement snapshot so a stale stream URL is never retained.

`VerticalVideoEditor.razor` owns `PositionX`, `PositionY`, source geometry, drag lifecycle, playback errors, Reset, and the generated `object-position` style. It uses Blazor pointer handlers for pointer down/move/up/cancel/leave and performs all normalized overflow-aware framing math in C#. When the media does not overflow an axis after `object-fit: cover`, movement on that axis is ignored. Values are clamped to `0..100`, and changing the selected ID resets both to `50`.

Blazor does not directly expose reliable `setPointerCapture` or DOM/media dimensions through `PointerEventArgs`. Add one isolated JavaScript module that captures/releases a pointer and returns viewport/video dimensions on drag start. It performs no framing calculations or state management and is not called on every pointer move. Use `touch-action: none` only on the draggable media surface, while keeping native video controls accessible.

The editor listens for HTML media failure and presents a live-region error while leaving the selected item and commands usable. Browser codec support is intentionally not guessed during server scanning.

Update the current Bootstrap-based layout rather than introducing a new UI framework: remove template Counter/Weather navigation, make the root screen a work-focused two-column library/editor layout at desktop sizes, stack it at handheld widths, and use CSS isolation for component-specific viewport and interaction rules. Stable aspect ratio, bounded dimensions, focus-visible styles, non-color selection cues, status semantics, and text wrapping cover responsive and accessibility behavior.

### Testability and design boundaries (FR1-FR20)

The service depends on typed options and framework filesystem APIs, with all policy concentrated in a narrow interface. Unit tests create temporary directory trees and assert discovery and containment without reading the user's actual library. Endpoint integration tests use the existing xUnit project, a test host, and small generated byte fixtures to verify snapshot authorization, safe responses, content types, and range behavior. Client framing math should be extracted into a small C# class so its clamping and overflow behavior can be unit-tested without a browser; the Razor component remains responsible only for event/state coordination.

Add `Microsoft.AspNetCore.Mvc.Testing` version `10.0.11` to the test project for `WebApplicationFactory`; this is the only new package and matches the existing ASP.NET Core package patch version. Expose `public partial class Program` solely for the standard test-host entry point. Browser interaction, responsive layout, native codec failure, and actual Docker mount/network constraints remain manual/end-to-end checks because the repository has no browser automation setup.

## Component Breakdown

**Existing files to modify:**

- `docker-compose.yml` - require `VIDEO_ROOT`, mount it read-only at `/videos`, pass `VideoLibrary__Path`, and bind the published port to `127.0.0.1`.
- `Makefile` - document the `.env` prerequisite in help output while retaining the existing Compose-based run and test workflow.
- `WebApp/WebApp/Program.cs` - register validated options and the library service, map video endpoints, register static same-origin HTTP behavior, and expose the test entry point.
- `WebApp/WebApp/appsettings.json` - replace wildcard host filtering with local host names; do not add a video path.
- `WebApp/WebApp/Components/Layout/MainLayout.razor` - remove template chrome that does not serve the manager and provide the application work area.
- `WebApp/WebApp/Components/Layout/MainLayout.razor.css` - adapt the existing responsive shell for the manager.
- `WebApp/WebApp/Components/Layout/NavMenu.razor` - replace template branding/navigation with the video-manager identity and remove demo links.
- `WebApp/WebApp/Components/Layout/NavMenu.razor.css` - style the simplified existing navigation using the current isolated-CSS pattern.
- `WebApp/WebApp/wwwroot/app.css` - update shared typography, colors, focus, status, and application-level responsive defaults without editing generated Bootstrap assets.
- `WebApp/WebApp.Client/Program.cs` - register the same-origin `HttpClient` used by the scan API.
- `WebApp.Tests/WebApp.Tests.csproj` - add the ASP.NET Core test-host package at the matching framework version.

**Existing template files to remove:**

- `WebApp/WebApp/Components/Pages/Home.razor` - replace the server-rendered placeholder root route with the client-owned interactive root page.
- `WebApp/WebApp/Components/Pages/Weather.razor` - remove the unrelated template demonstration.
- `WebApp/WebApp.Client/Pages/Counter.razor` - remove the unrelated template demonstration.
- `WebApp.Tests/UnitTest1.cs` - replace the empty placeholder test with feature tests.

**New files to create:**

- `.env.example` - safe template for the mandatory `VIDEO_ROOT` and optional `WEBAPP_PORT`.
- `WebApp/WebApp/Configuration/VideoLibraryOptions.cs` - typed server-only configuration and startup validation target.
- `WebApp/WebApp/Endpoints/VideoEndpoints.cs` - scan and range-stream endpoint mapping.
- `WebApp/WebApp/Models/VideoFileEntry.cs` - internal snapshot record containing the canonical physical path.
- `WebApp/WebApp/Services/IVideoLibraryService.cs` - narrow scan and ID-resolution contract.
- `WebApp/WebApp/Services/VideoLibraryService.cs` - recursive safe discovery and atomic snapshot implementation.
- `WebApp/WebApp.Client/Models/VideoItemDto.cs` - browser-safe scan response contract.
- `WebApp/WebApp.Client/Models/VideoFrameState.cs` - normalized C# frame state and overflow-aware drag calculations.
- `WebApp/WebApp.Client/Pages/Home.razor` - Interactive WebAssembly root coordinator.
- `WebApp/WebApp.Client/Pages/Home.razor.css` - responsive root workspace styling.
- `WebApp/WebApp.Client/Components/VideoLibrary.razor` - explicit scanning and selectable library states.
- `WebApp/WebApp.Client/Components/VideoLibrary.razor.css` - stable, accessible list styling.
- `WebApp/WebApp.Client/Components/VerticalVideoEditor.razor` - HTML5 player, pointer handling, normalized framing, Reset, and playback errors.
- `WebApp/WebApp.Client/Components/VerticalVideoEditor.razor.css` - 9:16 crop viewport and interaction styling.
- `WebApp/WebApp.Client/wwwroot/js/videoEditor.js` - minimal pointer-capture and element/media measurement bridge.
- `WebApp.Tests/Services/VideoLibraryServiceTests.cs` - xUnit discovery, recursion, filtering, snapshot, and containment tests.
- `WebApp.Tests/Endpoints/VideoEndpointsTests.cs` - xUnit test-host coverage for manual scan and stream behavior.
- `WebApp.Tests/Client/VideoFrameStateTests.cs` - pure C# normalized framing and clamping tests.

## Dependencies

- Docker Engine or a compatible Docker Compose implementation, following the repository's existing `make docker-run` workflow.
- A repository-root `.env` containing an absolute, existing, readable `VIDEO_ROOT` on the Docker daemon host.
- Read permission for the configured video tree; the container receives no write permission to that tree.
- Browser HTML5 media support for the encoding inside a listed container format. File extension support does not imply codec support.
- Existing .NET 10 SDK container, Blazor WebAssembly hosting packages, Bootstrap assets, and xUnit setup.
- New test-only package: `Microsoft.AspNetCore.Mvc.Testing` `10.0.11`.
- No database, FFmpeg, background service, filesystem watcher, authentication service, or new frontend library.

## External / Vendor Documentation Evidence

- [ASP.NET Core configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-10.0) documents that environment variables override JSON configuration, `__` maps to hierarchical `:`, the options pattern is appropriate for grouped settings, and `WebApplication.CreateBuilder` registers the environment provider. This supports passing `VideoLibrary__Path` from Compose and avoiding a custom `.env` package.
- [ASP.NET Core minimal API responses](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0) documents framework file results and `enableRangeProcessing: true`. This supports a streamed file result rather than buffering video bytes in application memory.
- [Docker Compose variable interpolation](https://docs.docker.com/compose/how-tos/environment-variables/variable-interpolation/) documents automatic repository-root `.env` loading and interpolation into Compose configuration. The `.env` file configures Compose; the application receives only the explicitly mapped internal path.
- [Docker bind mounts](https://docs.docker.com/engine/storage/bind-mounts/) documents that bind mounts are writable by default, that `read_only`/`ro` prevents host writes, and that long syntax fails when a source path does not exist. This supports an explicit read-only long-syntax mount.
- [Docker Compose networking](https://docs.docker.com/compose/how-tos/networking/) explains host/container port mapping and shows that unspecified published ports may bind to `0.0.0.0`. This supports declaring `127.0.0.1` explicitly for local-only access.

## Flow

```mermaid
sequenceDiagram
    actor User
    participant Home as Client Home.razor
    participant Library as VideoLibrary.razor
    participant API as VideoEndpoints
    participant Service as VideoLibraryService
    participant Root as /videos (read-only)
    participant Editor as VerticalVideoEditor.razor
    participant Browser as HTML5 video

    User->>Library: Activate Scan
    Library->>Home: Request scan
    Home->>API: POST /api/videos/scan
    API->>Service: ScanAsync()
    Service->>Root: Recursively enumerate safe files
    Root-->>Service: File metadata
    Service-->>API: New opaque-ID snapshot
    API-->>Home: Browser-safe VideoItemDto list
    Home-->>Library: Render results
    User->>Library: Select video
    Library->>Home: Selected opaque ID
    Home->>Editor: Set selected item; center frame
    Editor->>Browser: src=/api/videos/{id}/stream
    Browser->>API: GET with optional Range header
    API->>Service: Resolve current opaque ID
    Service-->>API: Internal canonical entry
    API-->>Browser: Range-enabled video response
    User->>Editor: Drag pointer
    Editor->>Editor: Update and clamp C# frame state
    Editor-->>Browser: Update object-position
```

## Risk Assessment

| Risk | Evidence | Mitigation |
| --- | --- | --- |
| Host video data becomes writable or remotely reachable | Current Compose publishes `${WEBAPP_PORT:-8080}:8080` on all interfaces and has no video mount policy yet. | Bind the library read-only, publish on `127.0.0.1`, restrict allowed hosts, and document that remote hosting is unsupported. |
| Traversal or symlink escape exposes files outside the root | Recursive host filesystem enumeration crosses a security boundary, and route values are attacker-controlled even on localhost. | Skip reparse points, canonicalize and validate every discovered file, accept only snapshot IDs at the endpoint, and test sibling-prefix and symlink cases. |
| Files change between scan and stream | The library is mutable outside the application and scanning is explicitly manual. | Use atomic snapshots, invalidate all IDs on each rescan, recheck existence/readability at stream time, return `404`, and never infer a path from an ID. |
| Range playback buffers large files or seeking fails | The current app has no media endpoint. | Use the framework file result with range processing and validate `206`, `Content-Range`, partial body size, and seeking manually. |
| Drag behavior is inconsistent across input types or gets stuck | Blazor pointer args do not provide pointer capture or DOM geometry APIs directly. | Use pointer events in WebAssembly, a minimal capture/measurement module, cancel/leave handling, pure C# frame math, and mouse/touch/pen manual checks. |
| `object-position` appears ineffective on one axis | `object-fit: cover` only crops axes where scaled media overflows the viewport. | Calculate overflow from intrinsic and viewport geometry, ignore non-overflow axes, and explain the result through stable position state rather than artificial transforms. |
| A listed file does not play | `.mov`, `.m4v`, and even `.mp4` containers may contain codecs unsupported by the browser. | Keep extension-based discovery, surface the native media error accessibly, and leave transcoding outside MVP. |
| Recursive scanning blocks or consumes excessive resources | A large nested tree may contain many files and filesystem errors. | Enumerate metadata server-side without opening file contents, support cancellation, handle inaccessible entries safely, make scans explicit, and expose scanning/error state. |
| New test-host dependency drifts from the runtime | The repository currently has only xUnit packages and an empty test. | Pin `Microsoft.AspNetCore.Mvc.Testing` to `10.0.11`, matching the existing ASP.NET Core WebAssembly Server package, and verify restore/build through `make test`. |
