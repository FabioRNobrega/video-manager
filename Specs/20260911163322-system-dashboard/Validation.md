# Validation: System Dashboard (replaces History page)

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | Navigating to `/` renders the Dashboard (System/Memory/.../Alerts cards); no `ArchiveContentHost Category="history"` markup is rendered anywhere on that route. |
| FR2 | `Sidebar.razor`'s first nav item reads "Dashboard" with `bi-speedometer2`, links to `/`, and is highlighted active when on `/`. |
| FR3 | `Player.razor`'s `CategoryRoutes` dictionary no longer contains a `"history"` key; a return-to-category call with `returnCategory == "history"` navigates to `/history` (the `TryGetValue`-fails fallback), not `/`. |
| FR4 | `GET /api/dashboard/system` returns CPU %, load average (1/5/15m), temperature-or-unavailable, and uptime; the System card renders all four fields. |
| FR5 | `GET /api/dashboard/memory` returns RAM used/total, available, cache, swap used/total; the Memory card renders all fields with a progress bar for RAM %. |
| FR6 | `GET /api/dashboard/storage` returns capacity used/total (matching `IStorageUsageService.GetUsage()`), recent read/write throughput, and a Health enum (OK/Warning/Critical) consistent with the free-space threshold. |
| FR7 | `GET /api/dashboard/network` returns aggregate upload/download totals, error/drop counts, and a list of interfaces with per-interface RX/TX. |
| FR8 | `GET /api/dashboard/archive` returns a file count matching `IArchiveService`'s current index, non-negative active-user/stream/job counts, and an ffmpeg-process count. |
| FR9 | `GET /api/dashboard/docker` returns one entry per visible container with name, CPU %, RAM, uptime, and restart count, or an "unavailable" payload if the Docker socket is unreachable (never a 500). |
| FR10 | `GET /api/dashboard/health` returns Storage/application/dependency/backup statuses, each one of OK/Warning/Critical/NotConfigured. |
| FR11 | `GET /api/dashboard/history` returns the last N sampled points for CPU/RAM/network/disk/temperature, populated even on the very first page load after app startup (proving the sampler runs independently of page visits, once at least one sampling interval has elapsed). |
| FR12 | `GET /api/dashboard/alerts` returns an empty list when all metrics are below the warning threshold, and returns a Warning/Critical entry with a human-readable message when a metric crosses 70%/90% respectively (verified by injecting a fake metrics snapshot). |
| FR13 | Clicking "Refresh" re-issues all nine `GET /api/dashboard/*` calls; no timer-based request fires without that click. |
| FR14 | For a metric at 50%/80%/95%, the rendered progress bar uses `bg-success`/`bg-warning`/`bg-danger` respectively, on every card that shows a percentage. |
| FR15 | When a backing service throws or reads fail (e.g. `/proc/net/dev` missing, Docker socket absent), the corresponding endpoint returns a 200 with an "unavailable" DTO shape (not a 500), and the card shows an explicit "unavailable" message instead of blank/stale content. |

## Test Cases

**Unit tests** (xUnit, matching `WebApp.Tests/Services/StorageUsageServiceTests.cs` and `WebApp.Tests/Endpoints/StorageEndpointsTests.cs` conventions — service tests construct the service directly with fakes/temp fixtures, endpoint tests call the static handler method directly):

- `WebApp.Tests/Services/SystemMetricsServiceTests.cs` — parsing a known sample `/proc/stat`/`/proc/meminfo`/`/proc/loadavg` fixture string yields expected CPU %, load, RAM figures; a missing/unreadable thermal path yields "unavailable" rather than throwing.
- `WebApp.Tests/Services/NetworkMetricsServiceTests.cs` — parsing a sample `/proc/net/dev` fixture yields correct per-interface and aggregate totals; malformed input yields "unavailable" instead of throwing.
- `WebApp.Tests/Services/StorageUsageServiceTests.cs` (extended) — new read/write-throughput method returns non-negative deltas; health enum matches documented thresholds (e.g. > 90% used → Critical).
- `WebApp.Tests/Services/ArchiveMetricsServiceTests.cs` — job-queue counts reflect fake `I*JobQueue` implementations; ffmpeg-process count uses an injectable process-lister abstraction (not `Process.GetProcesses()` directly) so it's fake-able in tests.
- `WebApp.Tests/Services/DockerMetricsServiceTests.cs` — using a fake Docker client abstraction, verifies mapping from container inspect/stats results to the DTO, and verifies an unreachable-socket scenario returns an "unavailable" result instead of throwing.
- `WebApp.Tests/Services/HealthAggregationServiceTests.cs` — given fake sub-statuses (storage/app/dependency/backup), verifies the aggregated result matches expected precedence (e.g. any Critical sub-status makes the overall Health Critical).
- `WebApp.Tests/Services/MetricsHistoryServiceTests.cs` — the ring buffer retains at most N samples, evicting oldest first; a fresh instance with zero samples returns an empty-but-valid (not null/unavailable) history payload.
- `WebApp.Tests/Services/AlertEvaluationServiceTests.cs` — given a fake metrics snapshot at 50%/75%/95% for a metric, asserts no-alert / Warning / Critical respectively, per FR12's acceptance criterion.
- `WebApp.Tests/Endpoints/DashboardEndpointsTests.cs` — each of the nine handlers returns `Results.Ok` wrapping the service's DTO, following `StorageEndpointsTests.cs`'s existing handler-invocation pattern.

**Integration tests:**
- ⚠️ TODO: An end-to-end test hitting the real `/api/dashboard/*` routes against a running container (via the project's existing `docker-compose.test.yml`) to confirm `/proc` reads succeed in the actual deployment environment — no such infra-level test exists for `StorageEndpoints` today either, so this would be a new addition; add only if the project decides it wants container-environment coverage beyond unit tests with fixture data.

## Manual Verification

Starting from a clean checkout, using the Makefile targets documented in `AGENTS.md`/`README.md` for running the app locally:

1. Run the project's standard local dev command (per `AGENTS.md`, typically `make` or `docker compose up`) and open the app in a browser.
2. Confirm the sidebar's first item now reads "Dashboard" and navigating to it does not show the old "recently accessed files" History list.
3. Confirm all nine cards (System, Memory, Storage, Network, PereneArchive, Docker, Health, History, Alerts) render without unhandled errors, each showing either real values or an explicit "unavailable" state.
4. Trigger a known condition (e.g. fill disk past 90%, or temporarily stop the Docker socket mount) and confirm the affected card turns yellow/red and/or an Alert appears, per FR12/FR14.
5. Click "Refresh" and confirm all cards reload (e.g. via browser network tab showing nine fresh `GET /api/dashboard/*` calls) with no automatic polling in between clicks.
6. Resize the browser to phone width and confirm all cards remain readable, stacked, and free of horizontal overflow.
7. Play a video from a category that used to return to `/` via the old History mapping, and confirm the "back" navigation no longer lands unexpectedly on the Dashboard mid-playback-return flow (per FR3).

## Definition of Done

- `Requirements.md`, `Plan.md`, and `Validation.md` in this spec folder are complete and consistent with the implementation.
- All existing tests in `WebApp.Tests` still pass, including `StorageUsageServiceTests.cs`/`StorageEndpointsTests.cs` if `IStorageUsageService` was extended.
- New unit tests listed above exist and pass, covering fixture-based parsing, unavailable-state fallbacks, and alert threshold behavior.
- Dashboard cards, `Sidebar.razor`, and `Player.razor`'s `CategoryRoutes` are all updated consistently; responsive, unavailable/error, and empty (e.g. "no alerts", zero-Docker-containers) states are covered per the Plan.
- The Open Questions in `Requirements.md` (Docker socket mount, temperature availability, "active users" proxy definition, alert thresholds) are explicitly resolved with the user before or during implementation — not silently assumed.
- If `docker-compose.yml`/`Dockerfile` changed (Docker socket mount, new NuGet dependency), that change is documented and the app is verified to still build/run via the project's documented workflow.

## Rollback Plan

- All changes are additive-plus-one-route-swap: revert by restoring `Home.razor`'s original `<ArchiveContentHost Category="history" .../>` body, reverting `Sidebar.razor:68`'s label/icon back to "History"/`bi-clock-history`, and restoring the `"history" → "/"` entry in `Player.razor`'s `CategoryRoutes`.
- The new `/api/dashboard/*` endpoints and services can be left registered but unused (no data loss risk) or removed via reverting `Program.cs`'s new registrations and `MapDashboardEndpoints()` call.
- If the Docker socket mount (FR9) causes concerns after deployment, it can be dropped from `docker-compose.yml` independently of the rest of the Dashboard — `IDockerMetricsService` already degrades to an "unavailable" state (FR15) when the socket is unreachable, so removing the mount alone disables only the Docker card without breaking the rest of the page.
