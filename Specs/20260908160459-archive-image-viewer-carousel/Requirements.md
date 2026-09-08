# Requirements: Archive Image Viewer Carousel

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)

## Problem Statement

`ArchiveService`/`ArchiveEndpoints`/`ArchiveBrowser.razor` currently classify archive files as video (`IsVideo`) or music (`IsMusic`, scoped only to the `music` category) and give both a rich card treatment: video gets a thumbnail/hover-preview tile, music gets an album-cover tile. Image files (`.jpg`, `.jpeg`, `.png`) fall through to the generic file-icon card in every category, and there is no way to view an image at a larger size or browse sibling images without leaving the archive browser. Users need image files, in any archive category, to show a small cover-style preview with hover metadata (matching the existing music-cover interaction), and clicking one should immediately open a fullscreen Bootstrap Carousel — styled like the existing video/music Fill-tab (`WebApp/WebApp.Client/Components/Player.razor`) — that lets them page through every other direct image in that same folder using bottom, hover-revealed chevron controls.

## User Stories

- Given an archive folder (in any category — Photos, Documents, Family, Shared, etc.) containing `.jpg`/`.jpeg`/`.png` files, when the folder is listed, then each image file renders as a small square cover-style tile showing the image itself, with name/metadata revealed on hover the same way music covers behave today.
- Given an archive folder with multiple direct image files, when the user clicks one image tile, then a fullscreen fit-to-tab carousel view opens immediately, starting on the clicked image, using the same dark full-viewport chrome as the existing video/music Fill-tab.
- Given the fullscreen image carousel is open, when the user moves the pointer over the carousel, then bottom-center chevron-compact-left/chevron-compact-right controls fade in (matching the existing controls-visible/hide-timer behavior in `Player.razor`), and clicking them advances to the previous/next direct image in the same folder.
- Given the image carousel is open on the first or last direct image in the folder, then the previous or next chevron (respectively) is disabled and does not wrap around.
- Given the fullscreen image carousel is open, when the user presses Escape or invokes the existing exit action, then the view exits back to the archive folder grid without leaving any footer player populated for the image.

## Functional Requirements

1. FR1 - `ArchiveItemEntry`/`ArchiveService` must classify direct child files with extension `.jpg`, `.jpeg`, or `.png` as viewable images (`IsImage`) in every archive category, independent of the `music`-only album-cover classification already in place.
2. FR2 - `ArchiveService`/`IArchiveService` must expose a way to resolve a validated image item by category + item ID (mirroring `TryResolveVideo`/`TryResolveMusic`) and to serve its bytes only through an opaque, contained-path-checked endpoint — no physical or root-relative path may reach the browser.
3. FR3 - `ArchiveEndpoints` must add an opaque `GET /api/archive/{category}/items/{id}/image` route that streams the resolved image's bytes with the correct `image/jpeg` or `image/png` content type, reusing the existing `ContainedPath`/`ResolveItem` validation pattern; no new resizing/transcoding pipeline is introduced — the original file bytes are served and the browser/CSS scales them for the grid tile.
4. FR4 - `ArchiveItemDto` must carry browser-safe image fields (`IsImage`, `ImageUrl`) populated the same way `IsMusic`/`AudioUrl` are populated today, for every category.
5. FR5 - `ArchiveBrowser.razor` must render image items as a square cover-style tile (reusing the `archive-music-cover`-style ratio/cover pattern) showing the image via `ImageUrl`, with the existing hover-reveals-name/metadata overlay behavior applied the same way it already applies to other non-folder cards.
6. FR6 - Clicking an image tile must not populate `PersistentPlayerState`/the footer player; instead it must immediately enter a fullscreen fit-to-tab image carousel view scoped to the current archive folder listing.
7. FR7 - The fullscreen image view must reuse the existing Fill-tab chrome and lifecycle: the same full-viewport dark container classes and `videoEditor.js` `enterFillTab`/`exitFillTab` interop already used by `Player.razor`, including Escape-to-exit.
8. FR8 - Inside the fullscreen view, a Bootstrap 5.3 Carousel must render one slide per direct image file in the current archive folder (same listing order already used for other archive items), starting on the clicked image, with the browser's built-in slide-indicator/autoplay/keyboard-swipe chrome disabled (no dots, no auto-advance, no default side arrow controls).
9. FR9 - The only navigation controls in the fullscreen carousel are icon-only Bootstrap buttons using `bi-chevron-compact-left` and `bi-chevron-compact-right`, positioned at the bottom-center of the carousel (not vertically centered), and hidden until pointer hover/interaction using the same visibility/opacity/timed-hide pattern as `_controlsVisible`/`ArmControlsHideTimer` in `Player.razor`.
10. FR10 - The previous/next chevrons must stop at the first/last direct image in the folder (no wraparound), matching the existing music playlist previous/next boundary behavior.
11. FR11 - The fullscreen image view must not render any play/pause/seek/volume/timeline/saturation/subtitle/A-B-loop/Save-Cut control — navigation (prev/next) and exit are the only controls.
12. FR12 - Exiting the fullscreen image view (Escape or exit action) must return to the archive folder grid without leaving any player state selected, consistent with FR6.

## Non-Functional Requirements

- Preserve the existing archive data-isolation boundary: image bytes are served only through the opaque `/api/archive/{category}/items/{id}/image` endpoint after `ContainedPath` validation; no physical/root-relative path may appear in DTOs, logs, or URLs.
- Keep filesystem resolution and content-type decisions server-owned in `WebApp`; keep rendering/interaction state client-owned in `WebApp.Client`.
- Do not add FFmpeg, ImageSharp/System.Drawing, thumbnail generation, EXIF parsing, or any new background worker/cache volume for this feature — this is explicitly a non-goal.
- Use Bootstrap 5.3.8 Carousel and Bootstrap Icons 1.13.1 patterns already present in the app; do not hand-edit vendored Bootstrap assets.
- Follow the existing Fill-tab/`videoEditor.js` interop pattern rather than introducing a second fullscreen mechanism.
- Add focused xUnit coverage following the existing `WebApp.Tests/Services`, `WebApp.Tests/Endpoints`, and `WebApp.Tests/Client` conventions.

## Out of Scope

- Image zoom, pan, or crop.
- Image editing, rotation, or any FFmpeg/ImageSharp-based cut/composition/transcoding equivalent.
- Thumbnail/resized-derivative generation or caching for images (original bytes are served as-is for the first slice).
- Captions/subtitles or an equivalent metadata overlay inside the carousel itself.
- A per-image saturation rail (video-only today).
- Recursive sibling-image discovery across subfolders.
- Populating the persistent footer player for images (fullscreen carousel is the only image playback surface).
- Supporting additional image extensions beyond `.jpg`, `.jpeg`, `.png`.
- Optimizing grid-thumbnail delivery for very large source images (originals are served as-is; not a concern for this spec).
