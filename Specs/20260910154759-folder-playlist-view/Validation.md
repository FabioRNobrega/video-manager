# Validation: Folder Playlist View

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | `ArchiveItemDto` for a folder item exposes `HasPlayableMedia = true` when that folder contains at least one file whose extension is in the existing video or music extension sets anywhere within it, including subfolders at any depth; a folder with only non-media files anywhere inside it reports `false`. No response field contains a physical or root-relative path. |
| FR2 | In `ArchiveBrowser.razor`, the folder actions dropdown shows "Play as a Playlist" only for folder tiles where `item.HasPlayableMedia` is true; the action is absent for other folders and for all file tiles. |
| FR3 | Clicking "Play as a Playlist" navigates the browser to `/playlist/{category}/{folderId}` using the same category key and opaque folder ID already used by the archive UI. |
| FR4 | `GET /api/archive/{category}/items/{id}/playlist` returns every video/music file found by walking the folder and its subfolders (folders and non-playable files excluded), in deterministic depth-first, case-insensitive name order; `PlaylistView.razor` builds its queue directly from that response. |
| FR5 | On load, the first queue item is selected into `PersistentPlayerState` and begins loading in the player; if the folder has zero playable items anywhere inside it at load time, an empty/explanatory state is shown instead of a player. |
| FR6 | `PersistentPlayerStateTests` show that a mixed video+music queue supports previous/next navigation by index regardless of item kind, and that `Music.razor`'s existing music-only playlist path (`SelectMusic`) still produces the same previous/next/boundary behavior as before this change. |
| FR7 | With a multi-item queue active (video-only, music-only, and mixed), letting the current item play to its end automatically selects and plays the next queue item; at the last item, playback stops without wrapping. |
| FR8 | `Player.razor` renders without any `position-fixed` footer/full-viewport class when `Mode="PlayerPresentationMode.Playlist"`, and with the existing fixed-footer classes when `Mode="PlayerPresentationMode.Footer"`. |
| FR9 | The playlist route shows a queue panel listing every queue item with a visible position number, a thumbnail (video items) or album cover (music items), title, and formatted duration, with the currently playing item visually distinguished from the rest. At the `lg` breakpoint and above, the whole layout fits within its content container's height (no whole-page scrollbar) and only the queue panel scrolls internally when it has more items than fit. |
| FR10 | Clicking a non-active queue panel item switches playback to that item and updates the highlighted item, while the route stays `/playlist/{category}/{folderId}`. |
| FR11 | Activating the fit control while on the playlist route enters the same Fill-tab overlay used elsewhere (full-viewport, `position-fixed`, `z-3`), visually covering the queue panel; exiting via the same control or Escape returns to the split playlist layout with the same selected item, playback position, and playing/paused state. |
| FR12 | While the playlist route is mounted, exactly one `Player.razor` instance is ever rendered app-wide: `MainLayout.razor` only renders its footer `Player` when `PlayerState.Selected is not null && !PlayerState.PlaylistViewActive`, so it never coexists with the playlist route's own embedded `Player`. |
| FR13 | Navigating from the playlist route to another page (e.g. the sidebar Home/Videos page) while a selection exists re-shows the fixed footer mini-player for that selection, resumed at the same playback position and playing/paused state, without a full page reload. |
| FR14 | The playlist route/layout shows a loading indicator while the listing request is pending, an empty state when the queue is empty, and a clear error state if the listing request fails. |
| FR15 | Save Cut/A/B loop controls in the playlist view are enabled only when `PersistentPlayerState.CanSaveCut` is true for the current item, matching existing footer-player behavior for Video Library vs. archive/cut/composition/music items. |
| FR16 | Re-opening "Play as a Playlist" for a folder while the footer is still playing an item from that folder's queue resumes on that same item (not track 1); `PersistentPlayerStateTests` cover both the resume case (selection still in queue) and the reset case (different folder or no prior selection). |
| FR17 | `PersistentPlayerState.UpdatePlaybackProgress` is a no-op when nothing is selected and never raises `StateChanged`; any genuinely new selection (next/previous/queue-item click/different playlist/single-item select) resets `LastKnownTime`/`WasPlaying` to `0`/`false`; a `Player.razor` instance mounted while `LastKnownTime > 0` seeks to it and resumes playback only if `WasPlaying` was true. |
| FR18 | A "Show playlist" icon button appears in the media controls, beside the Fill-tab control, whenever `PersistentPlayerState.CanReturnToPlaylist` is true and the user is not already on that playlist route; activating it navigates to `/playlist/{category}/{folderId}` for the active selection's playlist. |

## Test Cases

**Unit tests:**

- `WebApp.Tests/Services/ArchiveServiceTests.cs`: folder with a direct video only -> `HasPlayableMedia = true`; folder with a direct music file only -> `true`; folder with both -> `true`; folder with only images/documents -> `false`; folder whose only playable media is in a subfolder -> `true`; empty folder -> `false`; `ListPlaylist` collects direct and nested video/music files in deterministic depth-first order and returns no items for a folder without media.
- `WebApp.Tests/Endpoints/ArchiveEndpointsTests.cs`: listing response JSON includes `hasPlayableMedia` per the above cases (including a nested-only-media folder) and never includes a configured archive root string or path separator patterns tied to the physical test fixture path; the `/playlist` endpoint response includes nested files and excludes non-media files, again without path leakage.
- `WebApp.Tests/Client/PersistentPlayerStateTests.cs`: `EnterPlaylistView` sets `PlaylistViewActive`, `Playlist`, and selects the first item; `SelectPlaylistItem` moves selection and index correctly for a mixed video/music queue; `CanSelectPreviousTrack`/`CanSelectNextTrack` respect queue boundaries for mixed queues; `ExitPlaylistView` clears `PlaylistViewActive` without clearing the current selection; existing `SelectMusic`-based previous/next/boundary tests continue to pass unchanged; `UpdatePlaybackProgress` stores time/playing state without raising `StateChanged` and is a no-op with nothing selected; selecting a new track (next/previous/queue-item click) resets `LastKnownTime`/`WasPlaying`; re-entering `EnterPlaylistView` for the same folder while the current selection is still queued preserves selection and progress, while entering a different folder's playlist still selects the first item and resets progress.

**Integration tests:**

- `WebApp.Tests/Endpoints/ArchiveEndpointsTests.cs` (via `WebApplicationFactory`): `GET /api/archive/{category}/items/{id}/playlist` against a temporary folder fixture with nested subfolders containing a mix of video, music, image, and document files returns only the video/music files across all depths, confirming the recursive rule end-to-end.
- ⚠️ TODO: an endpoint-level smoke test asserting the existing archive video/audio stream endpoints used by playlist queue items remain range-enabled and opaque-ID-only, since the playlist view depends on them but does not add new ones.

## Manual Verification

1. `make docker-run` (or `make docker-run-bg`) to start the app against a populated `VIDEO_ROOT` with at least one folder containing a mix of `.mp4`/`.mp3`/`.wav` files, including some inside a subfolder.
2. Open the Videos or Music page, browse to that folder, open its actions dropdown, and confirm "Play as a Playlist" appears; confirm it does not appear on a folder with no playable media anywhere inside it.
3. Click "Play as a Playlist" and confirm the URL becomes `/playlist/<category>/<folderId>`, the first item plays automatically, and the queue panel lists every video/music file from the folder and its subfolders with correct thumbnails/covers, titles, and durations, with the active item highlighted.
4. On a normal desktop-width window, confirm the whole player+queue layout fits within the content area with no page-level scrollbar, and confirm playback controls at the bottom of the player are fully visible without scrolling.
5. Let a short item play to completion and confirm playback automatically advances to the next queue item and the highlight moves; confirm it stops (no wraparound) after the last item finishes.
6. Click a non-adjacent queue item and confirm playback switches to it immediately while staying on the playlist route.
7. Click the fit/expand control and confirm the player covers the full tab and the queue panel is no longer visible; press Escape and confirm the split layout returns with the same item, time position, and playing/paused state.
8. While the playlist is playing, navigate to another sidebar page (e.g. Photos) and confirm the footer mini-player appears at the bottom, resumed at roughly the same playback position and playing/paused state, without a visible reload/restart of the media element's network request in browser dev tools.
9. From that footer mini-player, click the new "Show playlist" button (beside the Fill-tab control) and confirm it returns to the same `/playlist/<category>/<folderId>` route, resumed on the same item and position (not restarted at track 1).
10. Skip or advance a few tracks into the queue, navigate away, then re-open "Play as a Playlist" on the same folder directly from the archive dropdown (not via the footer button) and confirm it also resumes on the same item and position rather than restarting at track 1.
11. Pause a track, navigate away, and confirm the footer mini-player (and, on returning, the playlist view) stays paused at the saved position rather than auto-resuming playback.
12. Repeat steps 2-7 on a mobile-width browser window and confirm the queue panel stacks below the player without overlapping controls (page-level scrolling is acceptable at this width).
13. Confirm Save Cut appears only for items that came from the Video Library category, matching current footer-player behavior, when testing a folder under Videos.
14. `make test` and confirm all existing and new xUnit tests pass in the isolated Docker Compose test stack.

## Definition of Done

- Requirements, Plan, and Validation docs in this folder are complete and consistent with the implemented behavior.
- All existing tests still pass, and new tests listed above are added and passing via `make test`.
- New Razor/CSS follows the Bootstrap 5.3.8/Bootstrap Icons/design-token contract in `Specs/20260827194328-perene-tech-design-system-refactor/`, with responsive, loading, empty, and error states covered as specified above.
- No physical or root-relative archive path is exposed by the new `HasPlayableMedia` field, the `/playlist` endpoint, the playlist route, or the queue panel, verified by endpoint tests.
- Fill-tab entry/exit from the playlist view is verified manually to behave identically to the existing Fill-tab implementation, with no new Escape/cleanup regressions.
- `MainLayout.razor` never renders two simultaneous `Player.razor` instances, verified by code review of the conditional rendering plus manual network-tab inspection during the playlist-to-footer transition.

## Rollback Plan

- The feature is additive: removing the "Play as a Playlist" dropdown entry in `ArchiveBrowser.razor` and the `/playlist/{Category}/{FolderId}` route registration (deleting or reverting `PlaylistView.razor`) fully disables user access to the feature without touching existing footer-player or music-playlist behavior.
- `PersistentPlayerState`'s generalized queue members are additive alongside the preserved `SelectMusic` wrapper, so reverting just the playlist-route/UI files leaves existing music-only playback unaffected.
- `HasPlayableMedia` is an additive, read-only DTO field; removing its computation in `ArchiveService.cs`/`ArchiveEndpoints.cs` has no effect on any other listing consumer.
- No migration, configuration flag, or infrastructure change is introduced, so rollback is a plain code revert of the files listed in `Plan.md`'s Component Breakdown.
