# Requirements: Unified Archive Browser

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

The archive UI currently has two different file-card presentations: `WebApp/WebApp.Client/Components/ArchiveBrowser.razor` shows mixed folders/files with type icons and a footer action bar, while `WebApp/WebApp.Client/Components/VideoGrid.razor` shows video-focused cards with static thumbnails, hover video previews, and compact video metadata. This split makes videos inside folders feel less capable than videos in dedicated video grids, and it forces archive actions into a persistent bottom footer that competes with the card content. The user needs one archive-browsing card model that keeps folder/non-video icon affordances, gives every video file thumbnail and hover preview treatment, and moves file actions into a top-right Bootstrap dropdown.

## User Stories

- Given I browse any archive folder, when the folder contains supported video files, then those video file tiles show a static thumbnail when ready and switch to a muted hover video preview when ready.
- Given I browse any archive folder, when the folder contains folders or non-video files, then those tiles keep clear Bootstrap Icons and show the item name with compact metadata rather than media previews.
- Given I need to rename, move, or delete an item, when I click the top-right three-dots button on its tile, then I can choose the same actions from a Bootstrap dropdown without a persistent footer action bar.
- Given I click a video tile's thumbnail or name area, when the file is playable, then the existing video selection/player behavior runs without the action dropdown stealing the click.
- Given Cuts and Video Compositions still use their specialized grids, when I open those sections, then `VideoGrid.razor` continues to support selection, thumbnails, hover previews, and composition workflows.

## Functional Requirements

1. FR1 - `ArchiveBrowser.razor` must become the only mixed archive folder/file browser component for sidebar archive categories.
2. FR2 - `VideoGrid.razor` must remain available for the Cuts and Video Compositions sections in `WebApp/WebApp.Client/Pages/Home.razor`; this spec must not require migrating those specialized grids.
3. FR3 - Archive video file DTOs must include enough browser-safe thumbnail and hover-preview state to render the same static image and hover video behavior currently available through `VideoItemDto`.
4. FR4 - The server must generate, reconcile, and serve thumbnail and hover-preview assets for all supported archive video files, regardless of archive category, without exposing physical paths or root-relative paths.
5. FR5 - Video archive tiles must render a 16:9 media area that shows a hover `<video>` preview when ready, a thumbnail `<img>` when ready, and the existing film placeholder icon while unavailable, pending, or failed.
6. FR6 - Non-video file archive tiles must not attempt image, document, audio, or video previews; they must show a file-type icon, the file name, the formatted file size when known, and the uppercased extension/type when known.
7. FR7 - Folder archive tiles must keep folder icon treatment with the folder name and must continue to open nested folders on activation.
8. FR8 - The persistent card footer containing Rename, Move, and Move to Trash icon buttons must be removed from archive item cards.
9. FR9 - Each archive item card must expose a top-right icon-only action button using `<i class="bi bi-three-dots-vertical"></i>` and Bootstrap dropdown markup.
10. FR10 - The action dropdown must contain Rename, Move, and Move to Trash actions with the same availability, disabled states, dialogs, endpoint calls, and successful-refresh behavior that `ArchiveBrowser.razor` uses today.
11. FR11 - Clicking the dropdown toggle or dropdown actions must not activate/open/select the underlying archive item card.
12. FR12 - Clicking the main body of a video tile must continue to select/open the video through the existing player path; clicking a non-video file tile remains a no-op unless future specs add file opening behavior.
13. FR13 - `ArchiveListingDto`/`ArchiveItemDto` changes must remain browser-safe and must not include physical host paths, container physical paths, or root-relative archive paths.
14. FR14 - The unified archive card layout must preserve loading, empty, selected, operation-pending, validation-error, and operation-failure states already present in `ArchiveBrowser.razor`.
15. FR15 - The archive grid must remain responsive and must keep text, icons, dropdowns, thumbnails, and card metadata from overlapping across mobile and desktop breakpoints.
16. FR16 - The implementation must add or update focused tests for archive DTO media fields, all-video-category preview reconciliation, dropdown action markup/behavior where practical, non-video metadata rendering, and preservation of Cuts/Compositions `VideoGrid` usage.

## Non-Functional Requirements

- Security and privacy: the opaque-ID and server-only filesystem boundary from the archive and video services must remain intact.
- Media scope: this spec reuses the existing static thumbnail and hover-preview FFmpeg scopes; it must not authorize a new transcoding/export pipeline.
- UI consistency: the unified cards must follow Bootstrap 5.3.8, Bootstrap Icons 1.13.1, the project design tokens in `WebApp/WebApp/wwwroot/app.css`, and the governing design guide in `Specs/20260827194328-perene-tech-design-system-refactor/`.
- Accessibility: icon-only dropdown toggles require accessible names, keyboard access, visible focus, and menu items that are operable without pointer-only interactions.
- Testability: media-state mapping should stay in endpoint/service code where it can be tested separately from Razor rendering.
- Compatibility: the app remains local-only and Docker Compose-only through the existing `Makefile` workflow.

## Out of Scope

- Removing `VideoGrid.razor` entirely.
- Migrating Cuts or Video Compositions away from `VideoGrid.razor`.
- Adding image previews for photos, album art, document previews, audio playback, PDF/book reading, or arbitrary file opening.
- Changing create folder, rename, move, or soft-delete server behavior beyond relocating the UI controls into a dropdown.
- Changing FFmpeg thumbnail or hover-preview generation settings beyond allowing archive video entries to use the existing pipeline.
- Adding multi-select batch actions to archive folder browsing.

## Open Questions

- None from discovery. The spec incorporates the user's decisions: keep `VideoGrid.razor` for Cuts/Compositions, apply media previews to all video files, show extension/type for non-video files, keep the same item actions, and keep main video tile activation separate from the dropdown.
