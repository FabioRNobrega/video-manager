# Requirements: System Dashboard (replaces History page)

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

`Home.razor` (`WebApp/WebApp.Client/Pages/Home.razor`) is routed at `@page "/"` and is the first item in the primary sidebar nav (`WebApp/WebApp.Client/Layout/Sidebar.razor:68`), labeled "History". It currently renders `<ArchiveContentHost Category="history" .../>`, a view of the `history` archive folder — not system telemetry. PereneArchive has no way today to see the health of the box it runs on: no CPU/memory/storage/network telemetry, no visibility into the Docker containers it runs alongside, no application-level counters (files, active jobs, ffmpeg usage), and no alerting when something is close to a limit. The only existing telemetry primitive in the codebase is `IStorageUsageService` (`WebApp/WebApp/Services/StorageUsageService.cs`), which reports disk used/total via `DriveInfo` and backs `StorageMeter.razor`.

This spec repurposes the `/` route and its "History" nav slot into a System Dashboard: a single page presenting System, Memory, Storage, Network, PereneArchive, Docker, Health, History (short-term metric trend), and Alerts sections, built with existing Bootstrap primitives (cards, progress bars, colored divs) and no charting library. The current "recently accessed files" History feature is removed as part of this change (confirmed with the user); it is not moved to a new route.

## User Stories

- Given the app is running on the user's NAS/server, when they open PereneArchive and land on `/`, then they see a Dashboard summarizing system, memory, storage, network, Docker, and application health at a glance.
- Given a resource (storage, memory, CPU) is approaching or past a safe threshold, when the user views the Dashboard, then the relevant metric is visually flagged (yellow/red) and also listed under Alerts.
- Given the user wants up-to-date numbers, when they click a Refresh control on the Dashboard, then all sections reload from the backend without a full page navigation.
- Given a metric cannot be read on the current host/container (e.g. temperature sensors unavailable), when the Dashboard loads, then that metric renders a clear "unavailable" state instead of a blank card or an error.
- Given the user opens the Dashboard on a phone-width screen, when the page renders, then all nine sections remain readable as stacked, responsive Bootstrap cards.

## Functional Requirements

1. FR1 — The route `@page "/"` renders a new `Dashboard.razor` page instead of the History `ArchiveContentHost`; `Home.razor`'s History rendering is removed.
2. FR2 — `Sidebar.razor`'s first nav item is relabeled from "History" (`bi-clock-history`) to "Dashboard" (`bi-speedometer2`), still routed at `/`.
3. FR3 — `Player.razor`'s `CategoryRoutes` mapping (`WebApp/WebApp.Client/Components/Player.razor:691-694`) no longer maps `"history"` to `/`, since `/` no longer hosts a history file browser.
4. FR4 — The Dashboard exposes a **System** card showing CPU utilization %, 1/5/15-minute load average, CPU temperature (or "unavailable"), and system uptime, sourced from a new server-side `ISystemMetricsService`.
5. FR5 — The Dashboard exposes a **Memory** card showing RAM used/total, available memory, page cache, and swap used/total, sourced from `ISystemMetricsService`.
6. FR6 — The Dashboard exposes a **Storage** card showing capacity used/total (reusing `IStorageUsageService`), recent read/write throughput, and a derived health status (OK/Warning/Critical based on free-space thresholds).
7. FR7 — The Dashboard exposes a **Network** card showing total upload/download throughput, error/drop counters, and a per-interface breakdown, sourced from a new `INetworkMetricsService`.
8. FR8 — The Dashboard exposes a **PereneArchive** card showing total indexed files (via `IArchiveService`), an active-users approximation, active media streams, counts of queued/running background jobs (thumbnail, hover-preview, subtitle, cut, composition queues), and active ffmpeg process count.
9. FR9 — The Dashboard exposes a **Docker** card listing each visible container's name, CPU %, RAM usage, uptime, and restart count, sourced from a new `IDockerMetricsService` backed by the Docker Engine API.
10. FR10 — The Dashboard exposes a **Health** card that aggregates Storage health, application responsiveness, key dependency availability (ffmpeg/ffprobe present and runnable), and backup status, each shown as OK/Warning/Critical.
11. FR11 — The Dashboard exposes a **History** card showing the last N samples (e.g. last 15 minutes) of CPU, RAM, network, disk, and temperature as simple non-chart-library visualizations (sequences of colored divs / mini bar rows), sourced from a background sampling service that keeps a rolling in-memory buffer independent of page visits.
12. FR12 — The Dashboard exposes an **Alerts** card listing current warning/critical conditions, derived by evaluating the latest metrics against configurable thresholds (e.g. storage/memory/CPU > 90% = critical, > 70% = warning); shows "No active alerts" when none apply.
13. FR13 — The Dashboard provides a manual **Refresh** control that reloads all section data on demand; there is no automatic polling/auto-refresh in this version.
14. FR14 — Every percentage/utilization value on the Dashboard is rendered with a Bootstrap `progress` bar (or colored div) using a consistent threshold coloring: green (< 70%), yellow (70–90%), red (> 90%).
15. FR15 — When any backend metrics call fails or a metric is not obtainable on the current host, the corresponding card/field renders an explicit "unavailable" state rather than throwing or showing stale/blank data.

## Non-Functional Requirements

- **Responsiveness**: All Dashboard cards use Bootstrap's grid/card utilities and remain usable at phone width (per the project's existing responsive conventions used elsewhere, e.g. `ArchiveBrowser.razor`).
- **Testability**: All new metric-gathering logic lives behind interfaces (`ISystemMetricsService`, `INetworkMetricsService`, `IDockerMetricsService`, `IAlertEvaluationService`) so unit tests can substitute fakes, following the existing `IStorageUsageService` pattern.
- **Resilience**: Reading OS/container/Docker data must never throw uncaught exceptions into the request pipeline; each service catches expected I/O/permission/timeout failures and returns a typed "unavailable" result, matching `StorageUsageService`'s existing `try/catch` fallback style.
- **No new page-load blocking work**: metrics endpoints must respond independently per section (separate `GET` calls) so a slow/unavailable section (e.g. Docker) does not block the rest of the Dashboard from rendering.
- **Least-privilege Docker access**: the Docker Engine API integration (FR9) only reads container state/stats; it must not expose start/stop/exec capabilities through the Dashboard UI or API.

## Out of Scope

- Auto-refresh/live polling of Dashboard data (deferred to a future spec).
- Charting library integration (explicitly deferred; this spec uses Bootstrap-only visuals).
- Any true SMART-based disk health (not obtainable without a `smartctl` dependency); Storage health in this spec is capacity-threshold based only.
- A real backup subsystem; Health's "backups" indicator reports "not configured" until a backup feature exists.
- Removing the underlying `history` `ArchiveCategory` definition (`WebApp/WebApp/Models/ArchiveCategory.cs:20`) or its folder — only its navigation entry point and page are removed.
- Authentication/authorization changes; the Dashboard is exposed the same way (private, local-only) as the rest of the app today.
- Historical data persistence across app restarts (FR11's rolling buffer is in-memory only).

## Open Questions

- ⚠️ TODO: CPU temperature sensors are frequently unavailable inside a Docker container (no `/sys/class/thermal` or `/sys/class/hwmon` access without extra host mounts). Confirm whether `docker-compose.yml` should mount host thermal paths read-only, or whether "unavailable" is acceptable for v1.
- ⚠️ TODO: Docker container stats (FR9) require mounting `/var/run/docker.sock` into the app container and adding a Docker Engine API client dependency (e.g. `Docker.DotNet`) — this is a real infrastructure and security-surface change to `docker-compose.yml`/`Dockerfile`. Confirm this tradeoff is acceptable before implementation.
- ⚠️ TODO: The app has no authentication/session system, so "active users" (FR8) has no ground truth. Plan proposes a best-effort proxy (distinct client identifiers seen hitting `/api/*` in the last few minutes) — confirm this approximation is acceptable, or define what "active users" should mean.
- ⚠️ TODO: "Active media streams" and "active ffmpeg processes" (FR8) need a definition of what counts as active (e.g. open video-serving HTTP ranges vs. currently running `ffmpeg`/`ffprobe` child processes) — confirm scope before implementation.
- ⚠️ TODO: Confirm the alert thresholds in FR12 (proposed: warning at 70%, critical at 90%, applied uniformly to storage/memory/CPU) are the desired defaults, and whether they should be configurable via `appsettings.json`.
