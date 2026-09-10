# Validation: Image Cutter (crop tool for the archive image viewer)

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | `ImageCarouselViewer` toolbar shows a scissors button (`bi-scissors`); clicking it toggles `_cropMode` for the currently displayed image only, and the button's active state visually matches the existing toggle-button convention (e.g. `ToggleCssClass`). |
| FR2 | With cut mode active, a rectangle with visible drag handles renders over the image; dragging the body moves it and dragging a handle resizes it, always clamped within the image's rendered bounds; existing pan (`StartPanAsync`/`MovePan`/`EndPanAsync`) does not fire while `_cropMode` is true. |
| FR3 | While `_cropMode` is true, a `bi-check-square-fill` button and a `bi-x-circle-fill` button are visible near the rectangle. |
| FR4 | Clicking confirm issues `POST api/archive/{category}/items/{id}/crop` with a body containing integer `X`, `Y`, `Width`, `Height` expressed in source-image pixels (verified by comparing the sent payload against the image's known natural dimensions in a test image). |
| FR5 | After a successful crop on an image located in any subfolder of `Pictures`, the new file appears on disk directly under `Pictures/cuts/` (not under the source image's subfolder); `Pictures/cuts/` is created automatically if absent. |
| FR6 | Two crops from source files with the same first-two-word prefix produce sequential 4-digit-suffixed names (e.g. `Beach Sunset 0001.jpg`, `Beach Sunset 0002.jpg`); a source file with no name segments falls back to prefix `Cut`. |
| FR7 | Cropping a `.png` source produces a `.png` output; cropping a `.jpg`/`.jpeg` source produces a same-extension output. |
| FR8 | The confirm request's HTTP response (success or failure) arrives synchronously in response to the POST — no job id, no subsequent polling call is made by the client. |
| FR9 | After a successful save, `_cropMode` is false, the rectangle and confirm/cancel buttons are gone, and the same image remains displayed at the same carousel index. |
| FR10 | Simulating a backend failure (e.g. 404/400/500 response) while confirming a crop leaves `_cropMode` true, keeps the rectangle's last position/size, and surfaces a visible error message in the viewer. |
| FR11 | Clicking cancel while a rectangle is drawn results in zero HTTP requests, `_cropMode` becomes false, and the source file's bytes/last-write-time are unchanged. |
| FR12 | `Utilities.razor` renders a tile labeled "ImageCutter" with icon `bi-bounding-box-circles`, and its `href` is `/utilities/image-cutter`. |
| FR13 | Navigating to `/utilities/image-cutter` lists every file currently present in `Pictures/cuts/` (name + thumbnail/preview image), reflects newly added crops after a reload/refresh, and clicking a listed crop opens it fullscreen in `ImageCarouselViewer` (same component/behavior as opening an image from `ArchiveBrowser`). |
| FR14 | Posting a crop region with `X`/`Y`/`Width`/`Height` that extend beyond the source image's actual pixel dimensions, or with `Width <= 0`/`Height <= 0`, returns `400 Bad Request` and writes no file. |
| FR15 | Posting a crop request with an `id` that does not resolve to an existing image in the given category returns `404 Not Found`. |

## Test Cases

**Unit tests** (mirroring existing patterns in `WebApp.Tests/Services/` and `WebApp.Tests/Endpoints/`):

- `WebApp.Tests/Services/ImageCropNamingServiceTests.cs` (mirrors `CutNamingServiceTests.cs`): verifies prefix extraction (first two words, fallback to `Cut`), sequential 4-digit counters scanning a temp directory, and case-insensitive prefix matching.
- `WebApp.Tests/Services/ImageSharpCropGeneratorTests.cs` (mirrors `FfmpegCutGeneratorTests.cs`): verifies a real crop against a small fixture `.png`/`.jpg` produces a file with the expected pixel dimensions, verifies out-of-bounds regions throw/return a typed failure without writing a file, and verifies the temp-file-then-move publish never leaves a partial file at the destination path on failure.
- `WebApp.Tests/Services/ImageCropServiceTests.cs` (mirrors `VideoCutServiceTests.cs`/orchestration tests): verifies not-found source id returns the not-found outcome, verifies `Pictures/cuts` is created when absent, verifies the returned DTO's id matches `ArchiveService.ComputeItemId` for the new file's path.
- `WebApp.Tests/Endpoints/ArchiveEndpointsCropTests.cs` (mirrors `ArchiveEndpointsTests.cs`/`CutEndpointsTests.cs`): verifies the new `POST .../crop` route maps 400/404/200 correctly for bad-region, missing-id, and happy-path cases using a fake `IImageCropService`.
- Extend `WebApp.Tests/Services/ArchiveServiceTests.cs`: verifies the new public `GetCategoryRootPath`/`ComputeItemId` wrappers return values consistent with the service's existing internal folder-listing/id behavior.

**Integration tests:**

- ⚠️ TODO: an end-to-end test that boots the WebApp test host against a temp `ArchiveRootOptions.Path` containing a real `Pictures` folder with a sample image, POSTs a crop, and asserts the resulting file exists under `Pictures/cuts/` and is retrievable via `GET api/archive/photos/items?folderId=...` — following the same test-host bootstrap pattern already used by `ArchiveEndpointsTests.cs`/`CutEndpointsTests.cs`.
- Full test suite run via `make test` (isolated `docker-compose.test.yml` stack), which must pass with the new tests included.

## Manual Verification

1. `make docker-run` to start the app with hot reload.
2. Open the archive browser, navigate into the Photos category, and open an image so `ImageCarouselViewer` shows it fullscreen.
3. Click the scissors icon; confirm the crop rectangle and its confirm/cancel buttons appear, and that pan/zoom no longer respond to dragging.
4. Drag the rectangle's body and a corner handle; confirm it stays within the image bounds and cannot invert (width/height going negative).
5. Click the `x-circle-fill` cancel button; confirm the overlay disappears, no network request appears in devtools, and the viewer behaves normally again (pan/zoom work).
6. Re-enter crop mode, size a rectangle over a known region of the image, and click `check-square-fill`; confirm the viewer returns to normal mode with no visible error.
7. Using the archive browser (or a shell into the `webapp` container via `make docker-exec`), confirm a new file now exists at `Pictures/cuts/<Prefix> 0001.<ext>` with the expected extension, and that visually it matches the selected region.
8. Repeat the crop on the same source image; confirm the new file is named `<Prefix> 0002.<ext>`.
9. Navigate to `/utilities` and confirm the new "ImageCutter" tile (icon `bi-bounding-box-circles`) is present; click it and confirm both saved crops appear in the gallery. Click one of the crops and confirm it opens fullscreen in `ImageCarouselViewer` with working pan/zoom/saturation/crop controls, exactly as it would from the archive browser.
10. Attempt a crop with dev-tools network throttling/offline simulation or by stopping the container mid-request (or a temporarily invalid id via manual `fetch`) to confirm an error case leaves the rectangle intact and shows an inline error rather than silently exiting crop mode.

## Definition of Done

- Requirements, Plan, and Validation docs in this folder are complete and consistent with the implementation.
- `make test` passes with new tests covering `ImageCropNamingService`, `ImageSharpCropGenerator`, `ImageCropService`, and the new crop endpoint.
- `ImageCarouselViewer.razor` cut-mode UI covers active/inactive states, drag/resize interaction, success, cancel, and inline-error states (no separate loading spinner needed given the synchronous, fast nature of a still-image crop, but the confirm button should disable itself for the duration of the in-flight request to prevent double-submits).
- `Utilities.razor` and the new `ImageCutter.razor` gallery page are implemented and manually verified per the steps above.
- `WebApp/WebApp.csproj` pins an explicit `SixLabors.ImageSharp` version; no Dockerfile/compose changes were required (confirmed during planning).
- Rollback path below is documented and requires no data migration.

## Rollback Plan

This feature is purely additive — it does not modify any existing endpoint, DTO, or database/config schema (there is no database in this app; state is the filesystem). To roll back:

- Revert the commit(s) touching `ImageCarouselViewer.razor`, `Utilities.razor`, `videoEditor.js`, `ArchiveEndpoints.cs`, `Program.cs`, `ArchiveService.cs`/`IArchiveService.cs`, and the new `ImageCrop*` service/page files.
- Remove the `SixLabors.ImageSharp` package reference from `WebApp/WebApp.csproj`.
- Any files already saved under `Pictures/cuts/` before rollback remain on disk as ordinary image files (harmless) and can be left in place or removed manually — no code path depends on their continued existence.
