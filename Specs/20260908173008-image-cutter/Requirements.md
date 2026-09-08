# Requirements: Image Cutter (crop tool for the archive image viewer)

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

`ImageCarouselViewer.razor` lets a user browse, zoom, pan, and adjust saturation on images from the archive's "Photos" category (folder name `Pictures`), but there is no way to extract a sub-region of an image as a new file. Meanwhile `VideoCut` already gives the video side of the archive an equivalent "cut a piece out and keep it" workflow: `Player.razor` sends an A/B time range to `POST api/videos/{id}/cuts`, a background ffmpeg job produces a new file, and `WebApp/WebApp.Client/Pages/UtilitiesPages/VideoCut.razor` (linked from `Utilities.razor`) shows a gallery of the results. Images have no analogous "crop and keep" action, and no gallery to review saved crops.

## User Stories

- Given a user is viewing an image in `ImageCarouselViewer`, when they click the scissors button, then a movable/resizable crop rectangle appears over the current image and normal pan/zoom interaction is suspended.
- Given a user has sized a crop rectangle over an image, when they click the confirm (`check-square-fill`) button, then the selected region is saved as a new image file in `Pictures/cuts/` and the viewer returns to normal mode.
- Given a user has sized a crop rectangle, when they click the cancel (`x-circle-fill`) button, then the rectangle is discarded, no file is written, and the viewer returns to normal mode.
- Given a user is on the Utilities page, when they click the new "ImageCutter" tile, then they land on a gallery page listing every image previously saved to any `cuts` folder under `Pictures`, mirroring how `VideoCut.razor` lists saved video cuts.

## Functional Requirements

1. FR1 — `ImageCarouselViewer.razor` adds a toolbar button with icon `<i class="bi bi-scissors"></i>` that toggles "cut mode" for the currently displayed image only (button reflects active/inactive state like the existing zoom/saturation buttons).
2. FR2 — Entering cut mode overlays a crop rectangle on the current image with draggable corner/edge handles; the rectangle can be moved and resized within the bounds of the displayed image, and pan/zoom pointer handling is suspended while cut mode is active.
3. FR3 — While cut mode is active, two buttons are shown above/adjacent to the crop rectangle: a confirm button (`bi-check-square-fill`) and a cancel button (`bi-x-circle-fill`).
4. FR4 — Clicking confirm sends the crop rectangle's coordinates (in source-image pixel space, not viewport/zoomed CSS pixels) for the currently displayed archive image to a new backend endpoint that performs the crop and writes a new image file.
5. FR5 — The cropped file is saved into a `cuts` subfolder directly under the root of the Photos category's folder (`Pictures/cuts/`), regardless of which subfolder the source image lives in. The endpoint creates `cuts` on first use if it does not already exist.
6. FR6 — The new file's name follows the `VideoCut`/`CutNamingService` convention: the first two whitespace-separated words of the source file's name (no extension) as a prefix (or `"Cut"` if empty), followed by a space and a 4-digit zero-padded counter derived by scanning existing files in `Pictures/cuts/` (e.g. `"Beach Sunset 0001.jpg"`), with the counter computed fresh per request (no persisted counter store), matching `CutNamingService`'s approach.
7. FR7 — The cropped file is saved in the same image format/extension as the source file (`.jpg`/`.jpeg`/`.png`), matching the archive's existing `ImageExtensions` allowlist in `ArchiveService`.
8. FR8 — The crop operation is synchronous: the confirm action's HTTP request performs the crop and returns success/failure directly (no job queue, no polling), reflecting that a single still-image crop is cheap compared to video re-encoding.
9. FR9 — On successful save, cut mode exits and the viewer returns to normal pan/zoom/saturation mode without navigating away from the current image.
10. FR10 — On save failure (e.g. source file changed/removed, disk error), the user sees an inline error indication in the viewer and remains in cut mode with their rectangle intact so they can retry or cancel.
11. FR11 — Clicking cancel discards the crop rectangle and any in-progress selection state, performs no backend call, and returns the viewer to normal mode with the image completely unmodified.
12. FR12 — `Utilities.razor` gets a new tile "ImageCutter" using icon `<i class="bi bi-bounding-box-circles"></i>`, positioned alongside the existing "Video Composition" and "VideoCut" tiles, linking to a new route `/utilities/image-cutter`.
13. FR13 — The new `/utilities/image-cutter` page (e.g. `WebApp/WebApp.Client/Pages/UtilitiesPages/ImageCutter.razor`) displays a gallery of every image currently present in `Pictures/cuts/`, mirroring `VideoCut.razor`'s grid-based presentation (thumbnail, name) adapted for static images (no thumbnail/hover-preview generation pipeline is needed since the file itself is already a small, ready-to-display image). Clicking a crop opens it in `ImageCarouselViewer`, consistent with every other page in the app that displays images (e.g. `ArchiveBrowser.razor`), with the carousel's image set scoped to the crops currently listed on this page.
14. FR14 — The backend validates that the requested crop rectangle lies fully within the source image's pixel bounds before cropping, rejecting (400) any out-of-bounds or degenerate (zero width/height) rectangle, mirroring the range validation `CreateCutAsync` performs against a re-probed source duration.
15. FR15 — The backend re-resolves the source image by its archive item id at request time (not trusting any client-supplied path) and returns 404 if the id no longer resolves to an image, mirroring `TryResolveImage`/`IVideoLibraryService.TryResolve` usage in the video cut endpoint.

## Non-Functional Requirements

- The crop endpoint must only read from and write within the configured archive root (`ArchiveRootOptions.Path`), never accepting or trusting a client-supplied filesystem path — reuse the existing `ContainedPath`-style containment checks already used elsewhere in `ArchiveService`.
- The new crop/generation logic must be a small, focused service (e.g. `IImageCropGenerator` + a naming service mirroring `CutNamingService`) rather than folded into the existing `ArchiveService` god-object-style controller logic, so it can be unit-tested independently with fake file entries.
- No new runtime process or Docker image changes are required: the crop is performed in-process using the `SixLabors.ImageSharp` NuGet package (fully managed, cross-platform), not `System.Drawing.Common` (unsupported/unreliable on Linux) and not a new ffmpeg invocation (unnecessary for a still-image crop).
- The `cuts` output folder must be excluded from becoming an infinite-recursion hazard if the archive browser ever lists `Pictures/cuts` itself as a normal folder — reuse the existing folder-listing pattern; no special-casing is required functionally, but the gallery page's own listing query must scope specifically to `Pictures/cuts`, not the whole Photos category.

## Out of Scope

- Non-rectangular (freeform/lasso) selection — rectangle only.
- Aspect-ratio locking, rotation, or any other image edit (filters, resize beyond the crop, format conversion) — this feature is crop-only.
- Editing/deleting previously saved crops from the new ImageCutter gallery page (read-only gallery, consistent with `VideoCut.razor`'s current scope).
- Async/queued processing, progress polling, or a background worker for crops (explicitly synchronous per FR8).
- Batch-cropping multiple images at once.

## Open Questions

Resolved:

- The ImageCutter gallery page opens a clicked crop in the full `ImageCarouselViewer`, same as any other page in the app that displays images (`ArchiveBrowser.razor`'s pattern) — see FR13. This also means a crop opened from this gallery can itself be re-cropped, since it is displayed through the same viewer/toolbar as any other image.
- There is no maximum or minimum crop rectangle size beyond "non-zero width and height" — no additional constraint is enforced.
