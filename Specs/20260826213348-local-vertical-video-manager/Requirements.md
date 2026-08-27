# Requirements: Local Vertical Video Manager

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

The repository currently contains the default .NET 10 Blazor Web App pages and has no way to discover or preview the user's local videos. The application needs a privacy-preserving, Docker-only workflow that reads a user-selected video root without committing that personal path, exposes only logical video identifiers to the browser, and provides a responsive Interactive WebAssembly editor for previewing and manually reframing supported files in a 9:16 viewport.

## User Stories

- Given a fresh checkout, when the user creates the required `.env` from `.env.example` and starts Docker Compose, then only that configured host directory is mounted read-only and the application is reachable only from the local machine.
- Given the application is open, when the user explicitly scans the library, then supported videos in the root and nested folders appear without revealing host filesystem paths.
- Given a listed video, when the user selects it, then it loads without a full-page navigation and can be played and sought through a range-enabled server endpoint.
- Given a selected video with content outside the 9:16 crop, when the user drags with a mouse, touch, or pen, then the crop position changes smoothly and remains within normalized bounds.
- Given a selected video, when the browser cannot decode its media encoding, then the file remains listed and the editor shows a useful playback error without failing the library.

## Functional Requirements

1. FR1 - `docker-compose.yml` must require a non-empty `VIDEO_ROOT` value supplied through the repository-root `.env`; Compose startup must stop with a clear configuration error when it is absent.
2. FR2 - `docker-compose.yml` must bind-mount `VIDEO_ROOT` into the web container at a fixed internal directory in read-only mode and pass that internal directory to ASP.NET Core as `VideoLibrary__Path`; no personal host path may be committed to application settings or source code.
3. FR3 - Docker Compose must publish the web port on host address `127.0.0.1` only, and the ASP.NET Core host must reject non-local host names; authentication and remote/LAN access are not part of this local-only application.
4. FR4 - The application must fail startup with a clear log message when `VideoLibrary:Path` is missing, not absolute, nonexistent, or not a readable directory.
5. FR5 - The video library UI must not enumerate the filesystem automatically; it must show an initial unscanned state and start or repeat discovery only when the user activates a visible Scan/Rescan command.
6. FR6 - A server-side library service must recursively discover regular files under the configured root whose extensions are `.mp4`, `.webm`, `.mov`, or `.m4v`, using case-insensitive extension matching and ignoring unsupported files.
7. FR7 - Recursive discovery must skip symbolic-link/reparse-point traversal and must reject every candidate whose canonical path falls outside the canonical configured root.
8. FR8 - The scan API and library UI must expose each video through an opaque logical identifier plus filename, extension, and byte size; absolute paths and root-relative directory paths must never be sent to the browser or written to normal application logs.
9. FR9 - Repeated scans must replace the current in-memory library snapshot, report empty-library and scan-error states in the UI, and keep the previous snapshot unavailable when a scan fails so stale files cannot be streamed unintentionally.
10. FR10 - Selecting a discovered video must load it into the editor without a full-page reload, initialize `PositionX` and `PositionY` to `50`, and visually identify the selected library row.
11. FR11 - `GET /api/videos/{id}/stream` must resolve only identifiers in the current server-side snapshot and return `404` for unknown, stale, malformed, or no-longer-readable entries without accepting filesystem paths from the request.
12. FR12 - The stream endpoint must stream the file without buffering the complete video, provide an appropriate video content type, support HTTP byte range requests, and permit browser seeking.
13. FR13 - The editor must render the selected video inside a stable `9 / 16` viewport with hidden overflow, `object-fit: cover`, standard HTML5 video controls, and an initial `object-position` of `50% 50%`.
14. FR14 - The Interactive WebAssembly editor must handle mouse, touch, and pen pointer input without server round trips and update C# `PositionX` and `PositionY` state while dragging; each value must remain within `0` through `100`.
15. FR15 - Dragging must change framing only on an axis where the source video overflows the 9:16 viewport and must not cause scrolling, selection, layout shifts, or a stuck drag state after pointer release, cancellation, or loss of capture.
16. FR16 - A Reset command must restore `PositionX` and `PositionY` to `50` immediately for the selected video.
17. FR17 - Selecting a different video must discard the current in-memory framing values and initialize the new selection at the centered position; framing persistence is not part of this release.
18. FR18 - A supported-extension file that the browser cannot decode must remain in the library and produce an accessible inline playback error for that selection while leaving scanning, selection, and Reset usable.
19. FR19 - The root experience must use the repository's Bootstrap-based Blazor layout and provide usable unscanned, scanning, empty, populated, selected, playback-error, and scan-error states on desktop and handheld widths.
20. FR20 - `.env.example` must document the required `VIDEO_ROOT` and optional `WEBAPP_PORT` values without containing the user's real path, while `.env` remains excluded by `.gitignore` and `.dockerignore`.

## Non-Functional Requirements

- **Privacy and isolation:** The container receives read-only access to one configured directory, the HTTP listener is loopback-only, and browser-visible responses contain no host or container filesystem paths. This is defense in depth for a single trusted local user, not a substitute for authentication on a remotely accessible deployment.
- **Performance:** Scanning must stream filesystem enumeration rather than preloading file contents. Playback must use range processing, and pointer updates must execute in WebAssembly with no network request per movement.
- **Security:** Canonical containment checks, symlink avoidance, an allowlist of extensions, and snapshot-backed opaque IDs must be enforced in the server service rather than trusted to UI validation.
- **Maintainability:** Configuration validation, discovery/path containment, endpoint mapping, API contracts, and editor interaction must have focused ownership and remain independently testable. High-level endpoints and UI components must not perform direct host path manipulation.
- **Accessibility:** Commands need accessible names and keyboard focus states; status/error changes need appropriate live-region semantics; selection must not be communicated through color alone; native video controls must remain reachable.
- **Compatibility:** The implementation targets the existing `net10.0` projects, Bootstrap assets, Blazor Web App hosting model, Interactive WebAssembly client, Docker Compose workflow, and xUnit test project.
- **Dependency control:** Custom JavaScript is limited to browser APIs Blazor does not expose directly, such as reliable pointer capture and element/media dimensions. Framing rules and state remain in C#. No media-processing dependency is introduced.

## Out of Scope

- Saving or persisting framing metadata across selections, scans, or application restarts.
- Multiple configured video roots or changing the root from the browser UI.
- Authentication, authorization, LAN/Internet hosting, multi-user isolation, or TLS termination.
- Thumbnail generation, duration/resolution extraction during scanning, search, sorting controls, or filesystem watching.
- Zoom, timeline editing, in/out points, keyboard editing shortcuts, subject/face tracking, or automatic reframing.
- FFmpeg integration, transcoding unsupported codecs, exporting cropped media, or batch processing.
- Native `dotnet run` configuration and execution outside Docker Compose.
- Following directory or file symbolic links, including links whose targets happen to remain inside the configured root.

## Open Questions

- None. The discovery decisions for local-only access, mandatory `.env` configuration, recursive scanning, explicit manual scans, Docker-only execution, and browser decode errors are incorporated above.
