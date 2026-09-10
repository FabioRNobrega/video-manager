# Validation: Music Folder Playback

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | A Music archive listing marks direct `.mp3` and `.wav` files as playable music and does not mark them as videos. |
| FR2 | The listing JSON for music files includes browser-safe music playback fields and contains no archive root, physical path, or root-relative path. |
| FR3 | When a Music folder contains image files, every playable track in that listing receives the first direct `.png`/`.jpg`/`.jpeg` cover URL according to deterministic listing/name order. |
| FR4 | The cover endpoint returns the selected cover with `image/png` or `image/jpeg` and returns 404 for stale IDs, non-cover files, path traversal attempts, and non-Music misuse. |
| FR5 | The audio endpoint streams `.mp3` as `audio/mpeg` and `.wav` as `audio/wav` with successful HTTP range requests. |
| FR6 | Clicking a folder in `Music.razor` opens that folder; clicking a music file selects it for playback. |
| FR7 | Music selection stores the selected track, current-folder playlist, stream endpoint, cover URL, and audio media kind in `PersistentPlayerState`. |
| FR8 | Music mode renders an `<audio>` element and visible album art instead of a `<video>` viewport in footer and Fill-tab modes. |
| FR9 | Music mode hides crop/drag, saturation, subtitle, A/B loop, marker, Clear, and Save Cut controls. |
| FR10 | Music mode renders previous and next controls using `bi-chevron-compact-left` and `bi-chevron-compact-right`. |
| FR11 | Previous is disabled on the first playlist item, next is disabled on the last playlist item, and neither control wraps. |
| FR12 | Existing video archive/library/cut/composition selection and playback tests continue to pass unchanged. |

## Test Cases

**Unit tests:**

- `WebApp.Tests/Services/ArchiveServiceTests.cs`: verify `.mp3` and `.wav` direct children under `Music` are classified as music, `.mp4` remains video, and non-music categories do not classify `.mp3` as playable music unless explicitly supported.
- `WebApp.Tests/Services/ArchiveServiceTests.cs`: verify first direct `.png`/`.jpg`/`.jpeg` cover selection is deterministic and ignores subfolder images.
- `WebApp.Tests/Client/PersistentPlayerStateTests.cs`: verify `SelectMusic` records audio media kind, selected track ID, playlist order, current cover URL, and correct previous/next boundary behavior.
- `WebApp.Tests/Client/PersistentPlayerStateTests.cs`: verify existing `SelectVideo`, `SelectCut`, `SelectComposition`, and `SelectArchiveVideo` behavior remains compatible.

**Integration tests:**

- `WebApp.Tests/Endpoints/ArchiveEndpointsTests.cs`: create a temporary Music folder with `.mp3`, `.wav`, and cover image fixtures; verify listing JSON has opaque music URLs and no physical archive root.
- `WebApp.Tests/Endpoints/ArchiveEndpointsTests.cs`: verify `GET /api/archive/music/items/{id}/audio` returns correct content type and `206 PartialContent` for a valid range request.
- `WebApp.Tests/Endpoints/ArchiveEndpointsTests.cs`: verify `GET /api/archive/music/items/{folderId}/cover` or the chosen cover route serves `image/png`/`image/jpeg` and rejects stale/non-cover IDs.
- `WebApp.Tests/Endpoints/ArchiveEndpointsTests.cs`: verify existing archive video stream, thumbnail, preview, and subtitle endpoint tests still pass.
- `WebApp.Tests/Endpoints/ArchiveEndpointsTests.cs`: add markup or component-source assertions for `ArchiveBrowser.razor`, `Player.razor`, and `MediaPlayerControls.razor` only where the existing test style already uses source assertions.

## Manual Verification

1. Ensure the host archive root contains `Music/Test Album/track 01.mp3`, `Music/Test Album/track 02.wav`, and one cover file such as `Music/Test Album/cover.jpg`.
2. Start the app with `make docker-run`.
3. Open the app, navigate to Music, and open `Test Album`.
4. Confirm the music files show the folder cover instead of generic file previews.
5. Click `track 01.mp3` and confirm the persistent footer player appears with album art, audio timeline, play/pause, seek, mute, volume, speed, and Fill-tab controls.
6. Confirm crop/drag, saturation, subtitles, A/B markers, A/B loop, Clear, and Save Cut controls are hidden for music.
7. Play the track, seek within it, and confirm audio continues through browser controls.
8. Press next and confirm playback switches to `track 02.wav`; confirm next is disabled on the last track.
9. Press previous and confirm playback switches back to `track 01.mp3`; confirm previous is disabled on the first track.
10. Enter Fill-tab mode and confirm the cover is centered as the primary visual while the controls remain usable.
11. Run `make test` and confirm all existing and new tests pass.

## Definition of Done

- Requirements, Plan, and Validation docs are updated in this spec folder.
- All existing tests still pass.
- New behavior has focused xUnit coverage matching this repo's existing `Services`, `Endpoints`, and `Client` test conventions.
- Music UI follows the existing Bootstrap/archive/player design system and covers responsive footer, Fill-tab, empty, error, and accessibility states.
- Audio and cover endpoints preserve opaque IDs and do not expose physical or root-relative filesystem paths.
- Vendor-specific file/range response decisions are supported by current Microsoft Learn documentation evidence in `Plan.md`.
- Docker Compose-only build/test workflow remains documented and verified.

## Rollback Plan

- Revert the changes to `ArchiveService`, `ArchiveEndpoints`, music DTO fields, `PersistentPlayerState`, `ArchiveBrowser`, `Player`, `MediaPlayerControls`, and related tests.
- Removing the new audio/cover endpoint mappings from `MapArchiveEndpoints` disables music playback while leaving existing archive folder browsing and video playback intact.
- Because this feature adds no migrations, no new persistent cache, and no background workers, rollback does not require data cleanup.
