# Validation: Unified Archive Browser

## Table of Contents

- [Automated Validation](#automated-validation)
- [Manual Validation](#manual-validation)
- [Acceptance Criteria](#acceptance-criteria)
- [Regression Checks](#regression-checks)

## Automated Validation

1. Run `make test`.
2. Add or update endpoint tests in `WebApp.Tests/Endpoints/ArchiveEndpointsTests.cs`:
   - Supported video files in any archive category return `IsVideo == true`, media preview states, and safe thumbnail/preview URLs when ready.
   - Non-video files return no thumbnail URL and no hover-preview URL.
   - Archive listing JSON does not contain physical temp paths, container archive paths, or root-relative archive paths.
   - Archive thumbnail/preview endpoints return `404` for stale IDs, non-video IDs, folders, unavailable previews, and IDs outside the current archive snapshot.
3. Add or update client markup tests in `WebApp.Tests/Client/ArchiveBrowserTests.cs` where practical:
   - Video items render a `ratio ratio-16x9` media region with image/video/fallback branches.
   - Folder and non-video items render Bootstrap Icons rather than media preview elements.
   - Each item card has an accessible top-right action button with `bi-three-dots-vertical`, `data-bs-toggle="dropdown"`, and dropdown actions for Rename, Move, and Move to Trash.
   - The old archive card footer action bar is absent.
4. Keep or update tests that confirm `Home.razor` still renders `VideoGrid` for Cuts and Video Compositions.

## Manual Validation

1. Start the app with `make docker-run-bg` after ensuring the archive root has at least:
   - One supported video under Videos.
   - One supported video under another category, such as Downloads or Shared.
   - One non-video file.
   - One folder.
2. Open the Videos page and verify:
   - Folders show folder icons and open on click.
   - Video files show thumbnails when ready.
   - Hovering a ready video tile switches to a muted looping preview.
   - Clicking the tile body selects/plays the video.
3. Open a non-Video category containing a supported video and verify the same thumbnail, hover preview, and click-to-play behavior.
4. Open a folder containing non-video files and verify:
   - Non-video files show an icon, filename, size, and extension/type.
   - No image or video preview is requested or displayed for non-video files.
5. For a folder, video file, and non-video file, click the top-right three-dots button and verify:
   - The Bootstrap dropdown opens aligned to the right.
   - Rename opens the existing rename dialog and refreshes the listing on success.
   - Move opens the existing move dialog and refreshes the listing on success.
   - Move to Trash works outside Trash and is hidden or disabled in Trash as current behavior requires.
   - Clicking the dropdown toggle or menu actions does not open the folder or select/play the video.
6. Resize to a phone-width viewport and a wide desktop viewport and verify item names, metadata, dropdowns, thumbnails, and icons do not overlap.

## Acceptance Criteria

- AC1 - `ArchiveBrowser.razor` renders mixed archive folders/files with one card model that supports icons for folders/non-video files and media previews for video files.
- AC2 - Supported video files in every archive category can display static thumbnails and hover video previews through browser-safe archive URLs.
- AC3 - Non-video files do not render media preview elements and show icon, name, size, and extension/type metadata.
- AC4 - Archive item Rename, Move, and Move to Trash controls live in a top-right Bootstrap dropdown using `bi-three-dots-vertical`; the old footer action bar is gone.
- AC5 - Main card activation still opens folders and selects playable videos, while dropdown interactions remain isolated.
- AC6 - `VideoGrid.razor` remains in use for Cuts and Video Compositions and retains existing composition-selection behavior.
- AC7 - Automated tests and manual checks show no physical host paths or root-relative paths in archive browser API responses.

## Regression Checks

- Existing folder navigation, create folder, rename, move, and soft-delete-to-Trash behavior still works.
- Existing video player/editor selection from the Videos archive page still works.
- Existing Cuts grid selection and Create Composition flow still works.
- Existing Video Compositions grid playback still works.
- Existing thumbnail and hover-preview queues continue processing asynchronously without blocking archive listing responses.
