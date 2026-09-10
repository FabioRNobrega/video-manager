# Requirements: Folder Playlist View

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

`WebApp/WebApp.Client/Components/ArchiveBrowser.razor` currently lets a user open one video or music file at a time into `PersistentPlayerState`, and `WebApp/WebApp.Client/Services/PersistentPlayerState.cs` only supports ordered previous/next navigation for a music-only playlist built from the direct files of the current Music folder (`Specs/20260908143416-music-folder-playback/`). There is no folder-level "play everything in this folder" action, and no dedicated queue-style layout: the only multi-item playback surface today is the persistent footer mini-player, which shows a single track with no visible upcoming queue. Users need a YouTube-style playlist experience — reachable directly from a folder's action menu — that plays every video and audio file found anywhere inside that folder (including subfolders) in order, shows an upcoming-queue panel beside the active player, and can be expanded to a distraction-free full-tab player that hides the queue panel, reusing the existing Fill-tab overlay from `Specs/20260827113116-fill-tab-video-mode/`.

## User Stories

- Given I am browsing an archive folder that contains at least one supported video or audio file, anywhere inside it or its subfolders, when I open that folder's actions menu, then I see a "Play as a Playlist" action.
- Given I choose "Play as a Playlist" on a folder, when the playlist view loads, then I see the first playable item already loaded in the player, a queue panel listing every video/audio file found in that folder and its subfolders in order with thumbnail/cover art, title, and duration, and playback controls beneath the player.
- Given the playlist view is open and the current item finishes playing, when a next item exists in the queue, then playback automatically advances to it and the queue panel highlights the new current item.
- Given the playlist view is open, when I click a different item in the queue panel, then the player switches to that item without leaving the playlist view.
- Given the playlist view is open, when I activate the fit/expand control on the player, then the player fills the whole tab exactly like today's Fill-tab mode and the queue panel is no longer visible; activating it again (or Escape) returns to the split playlist layout.
- Given I navigate away from the playlist view to another page, when the currently playing item is still eligible for the footer mini-player, then the footer mini-player reappears and keeps playing the same selection, at the same playback position and playing/paused state, without the browser reloading the page.
- Given I am several tracks into a playlist and navigate away, when I reopen the same folder's "Play as a Playlist" action or use the footer's "Show playlist" control, then the playlist view resumes on the same track at the same playback position instead of restarting from the first item.

## Functional Requirements

1. FR1 - `ArchiveService`/`ArchiveItemEntry`/`ArchiveItemDto` must expose whether a folder item contains at least one supported video or music file anywhere within it, including subfolders, without exposing physical or root-relative paths.
2. FR2 - `ArchiveBrowser.razor` must show a "Play as a Playlist" action in a folder tile's existing actions dropdown only when that folder's playable-media flag from FR1 is true, and must not show it for folders without any playable media or for file tiles.
3. FR3 - Activating "Play as a Playlist" must navigate to a new dedicated playlist route scoped to the chosen category and folder, using the same opaque, route-safe category key and item ID tokens already used elsewhere in the archive UI.
4. FR4 - The playlist route must load a dedicated server-computed queue for the folder, built from every video and music file found by walking the folder and its subfolders (non-playable files and empty folders excluded), ordered deterministically (depth-first, case-insensitive name order within each folder level).
5. FR5 - On load, the playlist route must select the first queue item into `PersistentPlayerState` (unless FR16 applies) and must show an empty/explanatory state instead of a player if the folder no longer has any playable media anywhere inside it (for example, items were moved or deleted after the menu action was shown).
6. FR6 - `PersistentPlayerState` must generalize its previous/next-track mechanism so a queue can contain a mix of video and music items, tracking the queue and current index independently of media kind, while preserving today's music-only playlist behavior in `Music.razor`/`ArchiveBrowser.razor` unchanged.
7. FR7 - The reusable player (`WebApp/WebApp.Client/Components/Player.razor`) must auto-advance to the next queue item and continue playback when the current item ends and a next item exists, for both video and music items, not only music as today.
8. FR8 - The reusable player must support an in-page "playlist" presentation mode, distinct from the existing fixed-position footer mode, that lays out the player and its now-playing info in normal document flow instead of a fixed viewport-bottom bar.
9. FR9 - A new playlist layout must render the player (with its now-playing info and controls) beside a queue panel that lists every queue item with a position number, thumbnail (video) or album cover (music), title, and duration, and visually highlights the currently playing item.
10. FR10 - Clicking a queue panel item must select that item into `PersistentPlayerState` and keep the user on the playlist view.
11. FR11 - The existing Fill-tab "fit" control (`bi-arrow-bar-up`, `EnterFillTabAsync`, `FillTabState`) must remain the single mechanism for expanding the player to fill the tab from the playlist view; entering it must visually cover the queue panel the same way it already covers footer/page chrome, and exiting it (button or Escape) must return to the split playlist layout with the same playback position and state.
12. FR12 - While the playlist route is active, `MainLayout.razor` must not render a second, independent footer player instance; the same `Player.razor`/`MediaPlayerState`/media element must be reused so navigating into and out of the playlist view does not reload or restart the active media.
13. FR13 - Leaving the playlist route (internal navigation to another page) must restore the normal footer mini-player for the still-selected item when `PersistentPlayerState.HasSelection` is true, using the existing persistent-footer-player behavior.
14. FR14 - The playlist queue panel and layout must present loading, empty, and error states consistent with the existing archive/player component conventions.
15. FR15 - Save Cut and A/B loop controls in the playlist view must follow the same eligibility rules already enforced by `PersistentPlayerState.CanSaveCut` (only for Video Library items), unchanged by this feature.
16. FR16 - Re-entering "Play as a Playlist" for a folder while the currently selected item is still present in that folder's (re-loaded) queue must resume on that same item instead of restarting at the first queue item; the queue list itself must still refresh to reflect the folder's current contents.
17. FR17 - `PersistentPlayerState` must track the current playback position and playing/paused state of the active selection, updated continuously while a `Player.razor` instance is mounted and playing, so that a new `Player.razor` instance mounted for the same selection (footer <-> playlist transitions, or re-entering the same playlist) resumes at that position and playing/paused state instead of restarting from zero; selecting a genuinely different item (including next/previous/queue-item clicks) must reset the tracked position to zero.
18. FR18 - The reusable player controls must expose a "Show playlist" action, positioned beside the Fill-tab "fit" control, that is available whenever the current selection has a returnable playlist route and the user is not already on that playlist route; activating it must navigate back to that folder's `/playlist/{category}/{folderId}` route.

## Non-Functional Requirements

- Security and privacy: no physical host path or root-relative archive path may be exposed by the playable-media flag, the playlist route/endpoint, or the queue panel; only existing opaque IDs and endpoint URLs are used.
- Data isolation: the recursive media scan and queue construction must stay scoped to the resolved folder and its descendants, matching the existing containment/reparse-point rules in `ArchiveService`.
- Compatibility: the feature must stay within the existing .NET 10 Interactive WebAssembly client, Bootstrap 5.3.8/Bootstrap Icons design system, and Docker Compose-only workflow; no new runtime package, frontend framework, or FFmpeg scope.
- Testability: the generalized playlist/queue logic must live in `PersistentPlayerState`/`ArchiveService` (and any small supporting model) so it can be unit-tested the way `PersistentPlayerStateTests.cs`/`ArchiveServiceTests.cs` already test music playlist and listing behavior, independent of Razor rendering.
- Accessibility: the queue panel's clickable items and the reused Fill-tab control must remain keyboard operable with accessible names, consistent with existing player control accessibility rules.
- Performance: the recursive media flag/scan is only triggered by folder listings and the explicit "Play as a Playlist" action (short-circuiting on the first match for the flag), not by any other archive browsing path, so normal non-playlist folder browsing stays close to its current cost.

## Out of Scope

- Reordering, editing, saving, or persisting a playlist beyond the folder's current on-disk contents at load time.
- Looping the queue back to the first item after the last item ends.
- Playing mixed content types beyond video and music (no images, books, or documents in the queue).
- Changing the existing music-only previous/next behavior surfaced in the footer mini-player outside the new playlist view.
- Any new FFmpeg processing, thumbnail styles, or metadata extraction beyond the existing thumbnail/hover-preview/album-cover pipelines.
- Sample-accurate/gapless resume (the resumed position is last-synced via periodic `timeupdate` events, not the exact frame at the moment of navigation).

## Open Questions

- None. Discovery confirmed a full-page playlist route, a mixed video/audio direct-children queue, reuse of `Player.razor`/`PersistentPlayerState`, and hiding the footer mini-player while the playlist view is open.
