# Validation: Persistent Footer Player

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | `MainLayout.razor` renders the player surface outside routed body content, and navigating between internal pages does not remove the selected player. |
| FR2 | With no selected video, no footer player markup is visible and the main content has no player-specific bottom spacing. |
| FR3 | Activating a supported video file in any archive page using `ArchiveBrowser.razor` publishes that video to the persistent player. |
| FR4 | `Home.razor` no longer renders `VerticalVideoEditor.razor` inline and instead selects media through shared player state. |
| FR5 | The selected video, stream base path, media state, crop state, saturation state, and A/B state persist while routed page content changes. |
| FR6 | The footer player shows a side video preview, selected video metadata, and the existing playback-control groups in a compact Plex-like footer layout. |
| FR7 | The footer includes an accessible button with `bi-arrow-bar-up` that enters Fill-tab mode. |
| FR8 | Crop dragging and saturation controls are absent in footer mode and present in Fill-tab/full mode. |
| FR9 | A/B loop controls and Save Cut remain available from the footer for library videos; Save Cut is disabled or hidden for unsupported sources. |
| FR10 | Escape exits Fill-tab mode, JS cleanup runs when the selected video changes, and fullscreen-exit returns to the footer player. |
| FR11 | Selecting a different video resets selection-scoped player state consistently with existing `MediaPlayerState`, `FillTabState`, `VideoFrameState`, and `SaturationState` behavior. |
| FR12 | Browser-visible markup and network requests contain only opaque IDs and existing stream endpoint URLs, never physical or root-relative paths. |
| FR13 | `GET /api/archive/{category}/items/{id}/stream` returns range-enabled video content for supported archive video files and 404 for unsupported files or IDs. |

## Test Cases

**Unit tests:**

- `WebApp.Tests/Client/PersistentPlayerStateTests.cs`: verify initial hidden state, selection visibility, selected source metadata, state-change notifications, and clearing/reset behavior.
- `WebApp.Tests/Client/MediaPlayerStateTests.cs` or existing state tests if added: verify any new source-kind gating for Save Cut does not alter existing playback, loop, or marker semantics.
- `WebApp.Tests/Client/FillTabStateTests.cs`: keep existing Fill-tab selection reset tests passing after the refactor.
- `WebApp.Tests/Client/VideoFrameStateTests.cs` and `WebApp.Tests/Client/SaturationStateTests.cs`: keep existing selection reset behavior passing after state is hosted by the persistent player.

**Integration tests:**

- Existing endpoint tests under `WebApp.Tests/Endpoints/VideoEndpointsTests.cs`, `CutEndpointsTests.cs`, and `CompositionEndpointsTests.cs` should continue to pass.
- `WebApp.Tests/Endpoints/ArchiveEndpointsTests.cs`: verify supported archive video streaming and non-video rejection.
- ⚠️ TODO: If the test project has or gains Razor component rendering support, add a component test that renders `MainLayout.razor` with a fake `PersistentPlayerState`, selects a video, and verifies footer visibility plus hidden initial state.

## Manual Verification

1. Run `make test` from the repository root and confirm all tests pass.
2. Run `make docker-run-bg` from the repository root.
3. Open the loopback URL configured by Docker Compose, normally `http://127.0.0.1:8080`.
4. Confirm no player footer appears before selecting media.
5. Open Videos, select a supported video file, and confirm the footer player appears with mini preview, metadata, controls, A/B controls, and Save Cut.
6. Start playback, navigate to another folder or page such as Music or Documents, and confirm the player remains visible and keeps playback state.
7. Select a supported video from another folder-backed archive surface and confirm the footer switches to that video and resets selection-scoped state.
8. Activate the `bi-arrow-bar-up` control and confirm the same video enters Fill-tab mode.
9. Confirm crop dragging and saturation controls are available in Fill-tab mode and absent in footer mode.
10. Press Escape or the fullscreen-exit control and confirm the player returns to the footer.
11. Select a cut or composition and confirm playback works through the matching stream endpoint; Save Cut is not offered for unsupported source kinds.
12. Use browser dev tools to verify stream requests use only opaque IDs and existing `/api/.../{id}/stream` URLs.
13. Run `make docker-down` when finished.

## Definition of Done

- Requirements, Plan, and Validation docs are updated in this spec folder.
- All existing tests still pass through `make test`.
- New persistent player state behavior has xUnit coverage matching existing client model tests.
- UI changes follow the implemented design system with Bootstrap-first markup, Bootstrap Icons, responsive footer/full modes, and accessible control labels.
- Mini footer, hidden state, loading/error states, unsupported Save Cut state, Fill-tab mode, and navigation persistence are manually verified.
- Vendor-specific Blazor state-management decisions are supported by current Microsoft Learn documentation evidence in `Plan.md`.
- No new server filesystem exposure, FFmpeg pipeline, package, or remote-hosting behavior is introduced.

## Rollback Plan

- Revert the `PersistentPlayerState` service registration in `WebApp/WebApp.Client/Program.cs`.
- Restore `Home.razor` to render `VerticalVideoEditor.razor` inline with its local `_selected` and `_selectedStreamBasePath` fields.
- Remove the layout-hosted player markup from `WebApp/WebApp.Client/Layout/MainLayout.razor`.
- Revert player mode changes in `VerticalVideoEditor.razor`, `MediaPlayerControls.razor`, their scoped CSS files, and the footer spacing in `WebApp/WebApp/wwwroot/app.css`.
