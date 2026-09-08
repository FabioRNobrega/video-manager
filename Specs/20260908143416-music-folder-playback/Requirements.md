# Requirements: Music Folder Playback

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

The Music page at `WebApp/WebApp.Client/Pages/Music.razor` currently delegates to `ArchiveBrowser`, where folders are normal archive folders and `.mp3`/`.wav` files are rendered as generic files rather than playable media. Users need to open a music folder, click a music file, and play the direct `.mp3`/`.wav` files in that current folder through the persistent footer player while using the first `.png`, `.jpg`, or `.jpeg` image in the folder as the album cover. The implementation must follow the existing video/archive privacy boundary: the browser receives opaque IDs and endpoint URLs, never physical or root-relative filesystem paths.

## User Stories

- Given the Music page is open at a folder containing `.mp3` or `.wav` files, when the user clicks one music file, then the persistent footer player starts using that folder as the current playlist.
- Given a music folder contains a `.png`, `.jpg`, or `.jpeg` file, when music from that folder is listed or played, then that image is shown as the album cover instead of a generic folder/file preview.
- Given a music playlist is active, when the user presses previous or next in the player controls, then playback switches to the adjacent music file in the same folder.
- Given the user enters Fill-tab mode while playing music, when the audio player is displayed, then the album cover is centered as the primary visual instead of showing a video viewport.

## Functional Requirements

1. FR1 - `ArchiveService` must identify direct `.mp3` and `.wav` files in the `music` archive category as playable music without marking them as videos.
2. FR2 - The archive listing contract must expose browser-safe music metadata for music files, including whether an item is playable music and the opaque endpoint URL needed to stream it.
3. FR3 - Music folder listings must use the first direct child image with extension `.png`, `.jpg`, or `.jpeg`, ordered by the existing listing/name ordering rules, as the album cover for music files in that folder.
4. FR4 - Album cover files must be served only through an opaque archive endpoint that validates the resolved cover remains inside the selected Music folder and returns the correct image content type.
5. FR5 - Audio streams must be served only through opaque archive endpoints for resolved `.mp3` and `.wav` items, using range-enabled file responses for browser seeking.
6. FR6 - `ArchiveBrowser.razor` must let users open folders normally, then select a playable music file from the current folder without navigating away or exposing paths.
7. FR7 - Selecting a music file must populate `PersistentPlayerState` with the current folder playlist, selected track, stream base path, album cover URL, and audio-specific mode.
8. FR8 - `Player.razor` must render an audio player mode that reuses the current timeline, play/pause, mute, volume, playback-rate, skip, and Fill-tab controls while showing album art instead of a video element.
9. FR9 - Audio player mode must hide video-only controls and states for crop/drag, saturation, subtitles, A/B markers, A/B loop, and Save Cut.
10. FR10 - `MediaPlayerControls.razor` must add Bootstrap Icons `chevron-compact-left` and `chevron-compact-right` controls for previous and next track when a music playlist is active.
11. FR11 - Previous/next controls must stop at the first or last direct music file in the current folder; they must not wrap around or include music from subfolders.
12. FR12 - The persistent player must preserve the existing video behavior for library, archive, cut, and composition videos.

## Non-Functional Requirements

- Preserve the existing archive data-isolation boundary: no physical path or root-relative path may appear in DTOs, normal logs, player state, or browser URLs.
- Keep filesystem resolution and content-type decisions server-owned in `WebApp`; keep rendering and interaction state client-owned in `WebApp.Client`.
- Do not add FFmpeg, ffprobe, audio transcoding, metadata parsing, embedded-cover extraction, or a new background worker for this feature.
- Use Bootstrap 5.3.8 and Bootstrap Icons 1.13.1 patterns already present in `ArchiveBrowser.razor`, `Player.razor`, and `MediaPlayerControls.razor`.
- Keep the player usable without enhanced tooltips or JavaScript failures beyond the existing `videoEditor.js`/`bootstrapInterop.js` fallback behavior.
- Add focused xUnit coverage following the existing `WebApp.Tests/Services`, `WebApp.Tests/Endpoints`, and `WebApp.Tests/Client` conventions.

## Out of Scope

- Recursive playlist playback across subfolders.
- Looping or wrapping from the last track back to the first.
- Editing, renaming, reordering, or persisting playlists.
- Reading ID3 metadata, embedded album art, artist, album, track number, or lyrics.
- Supporting audio extensions beyond `.mp3` and `.wav`.
- Generating thumbnails or converting audio/video formats.
- Changing the Video Library, Cuts, or Video Compositions playback behavior.

## Open Questions

- None for the first implementation slice. Future specs may decide richer metadata, recursive album handling, or embedded album art.
