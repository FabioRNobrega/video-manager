# Validation: Archive Image Viewer Carousel

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | A `.jpg`, `.jpeg`, or `.png` direct child file classifies with `IsImage = true` in `ArchiveService.List`/`CreateEntry` for every category (`photos`, `documents`, `family`, `shared`, `downloads`, `music`, `books`, `history`, `trash`), independent of `IsMusic`. |
| FR2 | `IArchiveService.TryResolveImage` returns `true` with the resolved entry only for a file-kind, `IsImage` item whose path is contained within the requested category root; it returns `false` for folders, non-image files, unknown IDs, and any attempted path-traversal ID. |
| FR3 | `GET /api/archive/{category}/items/{id}/image` returns 200 with `image/jpeg` for a resolved `.jpg`/`.jpeg` item and `image/png` for a resolved `.png` item, and 404 for a non-image item, an unknown ID, or a wrong category. |
| FR4 | `ArchiveItemDto` JSON for an image item includes `isImage: true` and a non-null `imageUrl` pointing at the `/image` route above; non-image items serialize `isImage: false` and `imageUrl: null`. No response body contains the archive root's physical or root-relative path. |
| FR5 | In `ArchiveBrowser.razor`, an image item renders a square cover tile showing the image via `ImageUrl`, with name/size/extension revealed in an overlay on hover/focus, matching the existing music-cover hover interaction. |
| FR6 | Clicking an image item does not change `PersistentPlayerState.Selected`/`MediaKind`/`StreamBasePath` (verified by asserting the persistent footer is not populated after the click) and instead opens the fullscreen carousel. |
| FR7 | Opening the image carousel adds the `fill-tab-active` class to `document.body` (via the shared `enterFillTab` interop) and pressing Escape removes it and closes the carousel, calling the same JS path already exercised by video/music Fill-tab. |
| FR8 | The fullscreen carousel renders one `.carousel-item` per direct image file in the current folder, the item matching the clicked image starts `active`, no `.carousel-indicators` dots are present, and the slide does not auto-advance after waiting past a typical Bootstrap default interval (5s). |
| FR9 | Bottom-center `bi-chevron-compact-left`/`bi-chevron-compact-right` buttons are present, hidden (opacity/visibility) until the pointer hovers/interacts with the carousel, and no default Bootstrap `.carousel-control-prev`/`.carousel-control-next` side arrows are rendered. |
| FR10 | On the first image, the previous chevron is `disabled`; on the last image, the next chevron is `disabled`; clicking next/previous never wraps past the last/first image. |
| FR11 | The fullscreen carousel markup contains no play/pause, seek/timeline, volume, saturation, subtitle, A/B-loop, or Save-Cut controls. |
| FR12 | After exiting the carousel (Escape or exit control), the archive folder grid is shown again and `PersistentPlayerState.HasSelection` remains whatever it was before the image was clicked (unchanged, per FR6). |

## Test Cases

**Unit/Service tests** (`WebApp.Tests/Services/ArchiveServiceTests.cs` or new `ArchiveServiceImageTests.cs`, following the existing `CreateArchive`/`CreateService` test helpers):
- Direct `.jpg`, `.jpeg`, `.png` children classify `IsImage = true` in a non-`photos` category (e.g. `documents`) to prove the classification is category-agnostic.
- A non-image file (`.txt`) classifies `IsImage = false`.
- `TryResolveImage` returns `false` for a folder ID, an unknown ID, and a resolved item in a different category than requested.
- `TryResolveImage` returns `true` and the correct `PhysicalPath` for a valid image ID.

**Endpoint tests** (`WebApp.Tests/Endpoints/ArchiveEndpointsTests.cs` or new `ArchiveEndpointsImageTests.cs`, following the existing `VideoManagerFactory`/`CreateArchive` pattern):
- `GET /api/archive/{category}/items` response JSON includes `isImage`/`imageUrl` fields for an image file and omits any physical/root-relative path substring (`root.Path`) anywhere in the raw JSON body — mirrors the existing path-leak assertions already used for video/music tests.
- `GET /api/archive/{category}/items/{id}/image` returns `200 OK` with `Content-Type: image/jpeg` for a `.jpg` fixture and `image/png` for a `.png` fixture.
- `GET /api/archive/{category}/items/{id}/image` returns `404 NotFound` for a folder ID, a non-image file ID, and a nonexistent ID.
- Image classification test repeated across at least two categories (e.g. `photos` and `documents`) to prove FR1's category-agnostic behavior.

**Client/component tests** (`WebApp.Tests/Client/`, following existing markup-smoke-test conventions such as `ArchiveBrowserTests.cs`):
- ⚠️ TODO: Render `ArchiveBrowser` with a listing containing one image item and assert the cover-tile markup (image `src`, hover overlay text) appears and the generic file-icon fallback does not.
- ⚠️ TODO: Simulate clicking an image item and assert `PersistentPlayerState` was not mutated (no `SelectVideo`/`SelectMusic`/`SelectArchiveVideo` call observed) while the `ImageCarouselViewer` render flag becomes active.
- ⚠️ TODO: Render `ImageCarouselViewer` with a 3-image list starting on the middle image and assert: only the matching `carousel-item` has the `active` class; previous/next buttons are both enabled; after simulating "next" twice, the next button becomes `disabled` and no wraparound occurs.

**Integration tests:**
- Full-stack `WebApplicationFactory` flow: seed an archive folder with 3 images, list it, resolve each `imageUrl` and confirm each returns valid image bytes with the correct content type end-to-end.

## Manual Verification

Starting from a clean state using this repo's documented Docker workflow:

1. `make docker-run` (or `make docker-run-bg`) to start the app with hot reload.
2. Ensure the bind-mounted archive root (`VIDEO_ROOT`) has at least one category folder (e.g. `Pictures`) containing 3+ `.jpg`/`.jpeg`/`.png` files, and confirm a non-`photos` category (e.g. `Documents`) also has a folder with 2+ images to verify category-agnostic behavior.
3. Open the app in a browser, navigate to a category containing images, and confirm each image renders as a square cover tile with the image visible.
4. Hover over an image tile and confirm the name/size/extension overlay fades in, matching the existing music-cover hover behavior.
5. Click an image tile and confirm the view immediately goes fullscreen (dark background, no footer player populated) showing that image.
6. Move the pointer over the fullscreen view and confirm bottom-center chevron-compact-left/right buttons fade in after a short delay of inactivity they fade out.
7. Click next/previous repeatedly and confirm navigation moves through every direct image in that folder in listing order, with the previous/next button disabling (not wrapping) at the first/last image.
8. Confirm no play/pause/seek/volume/subtitle/A-B-loop/Save-Cut controls appear anywhere in the fullscreen image view.
9. Press Escape and confirm the view returns to the archive folder grid, and that any previously playing video/music in the persistent footer (if one was active before opening the image) is unaffected.
10. Repeat steps 3-9 in the non-`photos` category folder from step 2 to confirm the feature is not accidentally scoped to `photos` only.
11. `make test` to confirm all new and existing tests pass.

## Definition of Done

- Requirements, Plan, and Validation docs in this spec folder are complete and internally consistent.
- All existing tests still pass (`make test`), and new tests listed above are added and passing.
- `ArchiveItemEntry`, `ArchiveService`, `IArchiveService`, `ArchiveEndpoints`, and `ArchiveItemDto` consistently expose `IsImage`/`ImageUrl` without leaking physical paths.
- `ArchiveBrowser.razor` renders image cover tiles with hover metadata and opens `ImageCarouselViewer` on click without mutating `PersistentPlayerState`.
- `ImageCarouselViewer.razor` implements the Bootstrap Carousel, bottom-only chevron controls, hover-based visibility timer, Fill-tab interop, and no-wraparound navigation.
- No FFmpeg, image-processing library, or new background worker/cache volume was introduced.
- `AGENTS.md` is updated (via the `init-agent` skill) to document the new image classification/endpoint and the `ImageCarouselViewer` component if this becomes part of the implemented architecture summary.

## Rollback Plan

This feature is additive: it introduces new fields (`IsImage`/`ImageUrl`), a new endpoint (`/image`), a new component (`ImageCarouselViewer.razor`), and new branches in `ArchiveBrowser.razor`'s rendering/`ActivateAsync` method, without modifying `PersistentPlayerState`, `Player.razor`, `MediaPlayerControls.razor`, or any existing endpoint's behavior for videos/music/other files. To roll back:

- Revert the `ArchiveBrowser.razor`/`ArchiveBrowser.razor.css` changes to restore the prior generic file-icon tile for image files (they will fall back to the existing `else` branch once the `IsImage` check is removed).
- Remove the `GET /api/archive/{category}/items/{id}/image` route registration in `ArchiveEndpoints.cs`; this is an isolated route addition with no other endpoint depending on it.
- Remove `ImageCarouselViewer.razor` entirely; nothing else references it once the `ArchiveBrowser.razor` click branch is reverted.
- `IsImage`/`ImageUrl` on `ArchiveItemDto`/`ArchiveItemEntry` can remain harmlessly unused (default `false`/`null`) or be removed in the same revert commit — no other feature depends on them.
