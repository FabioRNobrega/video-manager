# Requirements: Archive Folder Manager

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

PereneArchive currently behaves mostly as a video browser: `WebApp/WebApp.Client/Pages/Home.razor` lists videos from the configured `VideoLibrary:Path`, while sidebar pages such as `Photos.razor`, `Music.razor`, `Documents.razor`, and `Books.razor` show `ComingSoonSection`. The user needs the app to behave more like a local archive manager, with sidebar categories backed by default machine folders under `VIDEO_ROOT`, folder navigation inside each category, folder/file CRUD operations, and the existing video player/editor appearing when a playable video file is selected from a Videos folder.

## User Stories

- Given I open the Videos page, when the app loads the Videos category, then I see the folders inside the machine-owned `Videos` default folder instead of seeing the default folder itself.
- Given I am inside any non-Trash default category, when I create a folder with a valid unused name, then the folder appears in that category and is created on disk under that category root.
- Given I open a nested folder, when it contains supported video files, then I can select a video and use the existing player/editor workflow on that file.
- Given I rename or move an item inside a category, when the operation succeeds, then the browser view refreshes without exposing physical host paths.
- Given I delete an item from a category, when the operation succeeds, then the item is moved into Trash instead of permanently removed.

## Functional Requirements

1. FR1 - `docker-compose.yml` must mount `${VIDEO_ROOT}` into the container as the archive root so the app can access the default category folders `Videos`, `Pictures`, `Music`, `Documents`, `Books`, `Downloads`, `Shared`, `Family`, `History`, and `Trash`.
2. FR2 - The server must validate an archive root configuration at startup, including absolute path, existence, readability, writability for CRUD, and presence or creatability of the expected default category folders.
3. FR3 - The browser must never display, submit, or receive physical host paths or root-relative filesystem paths; all category, folder, and file references must use route-safe category keys plus opaque snapshot-scoped IDs or sanitized path tokens.
4. FR4 - The UI must show only the children inside each default category folder; it must not show the archive root itself or allow the web UI to create, rename, move, or delete the default category folders.
5. FR5 - The sidebar pages must map to default category roots: Videos -> `Videos`, Photos -> `Pictures`, Music -> `Music`, Documents -> `Documents`, Books -> `Books`, Downloads -> `Downloads`, Shared -> `Shared`, Family -> `Family`, History -> `History`, and Trash -> `Trash`.
6. FR6 - Every non-Trash category page must support creating a folder inside the currently open folder using a Bootstrap form/control flow and the folder icon `<i class="bi bi-folder-fill"></i>` for folder entries.
7. FR7 - Folder creation must reject empty names, names containing `/` or `\`, `.` or `..`, hidden dot-prefixed names, reserved device-style names where applicable, duplicate sibling names, and names that would escape the selected category root after canonicalization.
8. FR8 - Category pages must support opening nested folders and navigating back up within the selected default category without crossing into a sibling default category or the archive root.
9. FR9 - Category pages must list folders and files in a stable, case-insensitive order with folders before files, showing each folder as a Bootstrap card/tile with `bi-folder-fill` above or near its name.
10. FR10 - The archive manager must support CRUD operations for non-default folders and files: create folder, rename, move, and delete; delete must move the item into the top-level Trash folder rather than permanently deleting it.
11. FR11 - Trash must list moved items and allow browsing them, but this spec does not require folder creation inside Trash.
12. FR12 - Move and rename operations must validate containment, reject collisions unless a future explicit overwrite flow is specified, and preserve files/directories atomically where the platform permits.
13. FR13 - Selecting a supported video file inside the Videos category must use the existing stream/thumbnail/preview/player/editor pipeline where practical, while preserving opaque IDs and the current A/B cut and composition behavior.
14. FR14 - Non-video category pages must provide archive browsing and CRUD without introducing media playback, document preview, image preview, audio playback, or book reading in this spec.
15. FR15 - Existing cut and composition outputs must continue to live in their configured writable locations and remain visible in the Videos workflow without exposing filesystem paths.
16. FR16 - The UI must provide empty, loading, operation-pending, validation-error, and operation-failure states for folder listings and CRUD actions.
17. FR17 - The implementation must add focused unit and endpoint tests for category validation, containment, listing, create, rename, move, soft-delete-to-Trash, and default-folder protection.

## Non-Functional Requirements

- Security and privacy: no physical host paths or root-relative paths may be exposed to the browser or normal logs.
- Data isolation: all filesystem operations must be canonicalized and contained within the selected default category root or the Trash root for delete moves.
- Safety: permanent delete is out of scope; delete means move to Trash.
- Compatibility: the app remains local-only and Docker Compose-only through the existing `Makefile` workflow.
- UI consistency: all archive UI must follow the Bootstrap 5.3.8, Bootstrap Icons, Zilla Slab/Montserrat, dark/light token, and responsive interaction rules in `Specs/20260827194328-perene-tech-design-system-refactor/`.
- Testability: filesystem behavior must live behind focused services/interfaces so tests can use temporary directories without relying on the host archive.

## Out of Scope

- Uploading new files through the browser.
- Permanent deletion or empty-Trash behavior.
- File previews for photos, music, documents, books, or downloads.
- Multi-select batch operations beyond what already exists for video compositions.
- Authentication, LAN access, remote sharing, or multi-user permissions.
- Changing the authorized FFmpeg scopes beyond the existing thumbnail, hover-preview, cut, and composition pipelines.

## Open Questions

- None from discovery. The spec assumes the Photos sidebar label maps to the machine folder named `Pictures`, matching the user's examples.
