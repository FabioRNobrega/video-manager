# Agent Context

## Table of Contents

- [Agent Context](#agent-context)
  - [Project Overview](#project-overview)
  - [Repository Map](#repository-map)
  - [Architecture Summary](#architecture-summary)
  - [Execution Environment](#execution-environment)
  - [Coding Conventions](#coding-conventions)
  - [Microsoft Learn MCP Server](#microsoft-learn-mcp-server)
  - [Constraints](#constraints)
  - [Available Commands](#available-commands)
  - [Spec-Kit Workflow](#spec-kit-workflow)
  - [Key Documentation](#key-documentation)

## Project Overview

Video Manager is a .NET 10 Blazor Web App (server + Interactive WebAssembly client) that is being built into a local, privacy-preserving vertical-video browser and manual reframing tool. It reads a user-selected host directory of videos through a Docker-only, read-only bind mount, exposes only opaque logical IDs to the browser (never host paths), and lets the user preview and drag-reposition a 9:16 crop over supported video files. As of this writing the repository is still the stock `dotnet new blazor` scaffold plus Docker/Compose/Makefile plumbing — the feature described in `Specs/20260826213348-local-vertical-video-manager/` has not been implemented yet (see [Spec-Kit Workflow](#spec-kit-workflow)).

## Repository Map

- `WebApp/WebApp/` — ASP.NET Core hosted Blazor server project (`WebApp.csproj`), currently the default template: `Program.cs`, `Components/` (App, Layout, Pages), `appsettings*.json`, `wwwroot/` (app.css, favicon, vendored Bootstrap under `lib/bootstrap/`).
- `WebApp/WebApp.Client/` — Blazor WebAssembly client project (`WebApp.Client.csproj`), currently the default template: `Program.cs`, `Pages/Counter.razor`, `_Imports.razor`, `wwwroot/appsettings*.json`.
- `WebApp.Tests/` — xUnit test project (`WebApp.Tests.csproj`), currently one empty placeholder test (`UnitTest1.cs`), referencing `WebApp.csproj`.
- `video-manager.slnx` — solution file listing the three projects above.
- `Dockerfile` — `mcr.microsoft.com/dotnet/sdk:10.0` image running `dotnet watch run` for the web project on port 8080.
- `docker-compose.yml` — dev stack for the `webapp` service (hot reload, bind-mounts source and NuGet cache volumes).
- `docker-compose.test.yml` — isolated `tests` service that restores and runs `dotnet test` in a throwaway container.
- `Makefile` — single source of truth for build/run/test commands; wraps `docker compose` and detects the local Docker/Podman socket.
- `Specs/` — spec-kit folders (`<timestamp>-<slug>/Requirements.md`, `Plan.md`, `Validation.md`) documenting features to implement; currently one spec, not yet implemented.
- `.gitignore` / `.dockerignore` — exclude build output, secrets, and `.env` (but not `.env.example`, which does not exist yet — see the pending spec's FR20).

## Architecture Summary

This is the standard Blazor Web App "hosted WebAssembly" split: `WebApp` is the ASP.NET Core host that maps Razor components and (per the pending spec) will map minimal API endpoints; `WebApp.Client` is the Interactive WebAssembly project whose assemblies are added via `AddInteractiveWebAssemblyRenderMode().AddAdditionalAssemblies(...)` in `WebApp/WebApp/Program.cs`. `WebApp.Tests` references `WebApp` directly and will (per the plan) use `Microsoft.AspNetCore.Mvc.Testing`'s `WebApplicationFactory` once that package is added.

The intended architecture (from `Specs/20260826213348-local-vertical-video-manager/Plan.md`, not yet built) is:

- **Configuration boundary**: `docker-compose.yml` requires `VIDEO_ROOT` from a repo-root `.env`, bind-mounts it read-only to a fixed internal path, and passes it to ASP.NET Core as `VideoLibrary__Path`. A `VideoLibraryOptions` type validates it's absolute, existing, and readable at startup.
- **Server library snapshot**: a singleton `IVideoLibraryService`/`VideoLibraryService` recursively enumerates the mounted root (skipping symlinks/reparse points, canonical-path containment checks, extension allowlist `.mp4/.webm/.mov/.m4v`), and holds an atomically-replaced in-memory snapshot mapping opaque IDs to internal `VideoFileEntry` records. No physical or root-relative path is ever sent to the browser.
- **Minimal API endpoints** (`MapVideoEndpoints` in `WebApp/WebApp/Endpoints/`): `POST /api/videos/scan` triggers a rescan and returns browser-safe `VideoItemDto`s (from `WebApp.Client/Models/`); `GET /api/videos/{id}/stream` resolves only current-snapshot IDs and returns a range-enabled file stream (404 otherwise).
- **Interactive WebAssembly UI**: a client-owned `WebApp.Client/Pages/Home.razor` (`@rendermode InteractiveWebAssembly`) coordinates a `VideoLibrary.razor` (explicit scan/rescan, row selection) and a `VerticalVideoEditor.razor` (9:16 `object-fit: cover` viewport, pointer-driven `PositionX`/`PositionY` framing state clamped 0–100, Reset). A small isolated JS module (`wwwroot/js/videoEditor.js`) only bridges pointer capture and element/media dimensions; all framing math and state stay in C#.

### Request flow (target design)

`User → VideoLibrary.razor (Scan) → Home.razor → POST /api/videos/scan → VideoLibraryService.ScanAsync() → enumerates /videos (read-only mount) → new opaque-ID snapshot → VideoItemDto list → Home.razor renders list → user selects → VerticalVideoEditor sets src="/api/videos/{id}/stream" → GET with Range header → VideoLibraryService resolves snapshot entry → range-enabled response → drag events update C# frame state → object-position style updates`. See the Mermaid sequence diagram in `Specs/20260826213348-local-vertical-video-manager/Plan.md` for the full version.

## Execution Environment

This project is Docker Compose-only; there is no documented native `dotnet run` workflow (the pending spec explicitly keeps native execution out of scope). All commands go through the `Makefile`, which wraps `docker compose` and auto-detects a Docker or Podman socket via `DOCKER_HOST`.

- `make docker-build` — build the .NET 10 SDK image.
- `make docker-run` / `make docker-run-bg` — start the app (hot reload via `dotnet watch`) in foreground/background.
- `make docker-down` / `make docker-reset` — stop the stack, optionally deleting volumes.
- `make docker-logs`, `make docker-ps` — follow logs / list containers for the `video-manager` compose project.
- `make docker-shell` — open a shell in a fresh SDK container; `make docker-exec` — shell into the running `webapp` container.
- `make dotnet ARGS="build"` — run an arbitrary `dotnet` command inside the SDK image.
- `make test` (alias `make docker-test`) — run `WebApp.Tests` in the isolated `video-manager-test` compose project (`docker-compose.test.yml`), always tearing down volumes afterward regardless of test outcome.
- `make docker-test-shell` — shell into the test image without running tests.

Once the pending spec lands, running the app will additionally require a repo-root `.env` (copied from `.env.example`) defining `VIDEO_ROOT` (absolute host path to a video directory) and optionally `WEBAPP_PORT`.

## Coding Conventions

- `Nullable` and `ImplicitUsings` are enabled in every project; keep new code nullable-aware.
- The client (`WebApp.Client`) and server (`WebApp`) projects are kept as separate concerns: the server owns filesystem/path/config logic, the client owns Razor components, rendering, and browser-side state — the pending spec explicitly keeps physical path manipulation out of endpoints/components and confines it to a server-only service.
- Browser-visible DTOs live in `WebApp.Client/Models/` (reusing the existing project reference from server to client) rather than a separate shared project.
- CSS is component-scoped using Blazor CSS isolation (`ComponentName.razor.css`); `wwwroot/app.css` holds shared/global styles; vendored Bootstrap under `wwwroot/lib/bootstrap/` is generated/vendored and should not be hand-edited.
- Custom JavaScript is kept minimal and isolated to what Blazor can't do natively (e.g. pointer capture, element/media geometry) — no framing logic or state belongs in JS.
- Tests are xUnit, organized by concern under `WebApp.Tests/` (e.g. `Services/`, `Endpoints/`, `Client/` once added), and run exclusively through `make test` in an isolated Docker Compose stack, not on the host.

## Microsoft Learn MCP Server

This repository is a Microsoft-stack project: .NET 10 (`net10.0`), ASP.NET Core (`Microsoft.NET.Sdk.Web`), Blazor WebAssembly (`Microsoft.NET.Sdk.BlazorWebAssembly`, `Microsoft.AspNetCore.Components.WebAssembly*`), and (once the pending spec is implemented) `Microsoft.AspNetCore.Mvc.Testing`. Use the Microsoft Learn MCP Server as the first-party documentation source for decisions involving these technologies:

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
- Do not introduce FFmpeg, transcoding, thumbnail generation, or other media-processing dependencies — these are explicitly out of scope per the current spec.
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

Features are designed before implementation under `Specs/<timestamp>-<slug>/`, each containing `Requirements.md` (problem, user stories, functional/non-functional requirements, out-of-scope, open questions), `Plan.md` (technical approach, component breakdown, dependencies, external documentation evidence, flow diagram, risk assessment), and `Validation.md`. The current spec, `Specs/20260826213348-local-vertical-video-manager/`, defines the video-discovery/streaming/reframing feature described in [Project Overview](#project-overview) and has not yet been implemented — use the `implement-spec` workflow to build the most recent unimplemented spec, and `new-spec` to add another one.

## Key Documentation

- [Specs/20260826213348-local-vertical-video-manager/Requirements.md](Specs/20260826213348-local-vertical-video-manager/Requirements.md) — current feature's requirements.
- [Specs/20260826213348-local-vertical-video-manager/Plan.md](Specs/20260826213348-local-vertical-video-manager/Plan.md) — current feature's technical plan and risk assessment.
- [Specs/20260826213348-local-vertical-video-manager/Validation.md](Specs/20260826213348-local-vertical-video-manager/Validation.md) — current feature's validation criteria.
