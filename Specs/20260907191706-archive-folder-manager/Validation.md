# Validation: Archive Folder Manager

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | Running the app through Compose mounts the configured archive root and the app can see all default category folders. |
| FR2 | Startup fails with a clear options validation error when the archive root is missing, relative, unreadable, or unwritable for required CRUD. |
| FR3 | Archive and video API JSON responses never contain the temporary test root, host archive root, or root-relative folder paths. |
| FR4 | Category pages show only children of the selected default folder and no UI action can modify the default category folder itself. |
| FR5 | Each sidebar route loads the expected physical category folder, including Photos mapping to `Pictures`. |
| FR6 | Users can create folders from every non-Trash category page and see the new folder in the current listing. |
| FR7 | Invalid folder names and duplicate sibling names return validation errors and do not create filesystem entries. |
| FR8 | Opening nested folders and navigating upward stays inside the selected default category. |
| FR9 | Listings show folders before files in stable case-insensitive order, and folder tiles render `bi-folder-fill` with the folder name. |
| FR10 | Rename, move, and delete work for eligible files/folders; delete moves items into Trash rather than removing them permanently. |
| FR11 | Trash lists moved items and does not offer folder creation. |
| FR12 | Rename and move reject stale IDs, collisions, invalid destinations, and cross-root escapes. |
| FR13 | Selecting a supported video file in Videos opens the existing player/editor and preserves thumbnail, preview, cut, and composition behavior. |
| FR14 | Non-video category pages browse and manage files/folders without adding media preview or playback. |
| FR15 | Existing cut and composition outputs remain available in the Videos workflow after the archive-root mount change. |
| FR16 | UI states exist for empty, loading, pending operation, validation error, and operation failure scenarios. |
| FR17 | New unit and endpoint tests cover the archive service and API behavior, and existing video tests are updated without weakening privacy assertions. |

## Test Cases

**Unit tests:**

- `WebApp.Tests/Services/ArchiveServiceTests.cs`: validates category allowlist, Photos -> `Pictures` mapping, default-root protection, stable ordering, folder creation, invalid names, collision handling, containment checks, reparse-point skipping, rename, move, and soft-delete-to-Trash.
- `WebApp.Tests/Services/VideoLibraryServiceTests.cs`: updates or adds coverage for Videos-folder-scoped discovery and selected-folder video resolution.
- `WebApp.Tests/Services/StorageUsageServiceTests.cs`: verifies storage usage reads from the archive root configuration.

**Integration tests:**

- `WebApp.Tests/Endpoints/ArchiveEndpointsTests.cs`: uses `WebApplicationFactory<Program>` with temporary archive folders to verify list/create/rename/move/delete endpoints, status codes, JSON shape, and no path leakage.
- `WebApp.Tests/Endpoints/VideoEndpointsTests.cs`: verifies video stream/thumbnail/preview behavior still rejects stale IDs and path-like IDs after the archive manager is introduced.
- `WebApp.Tests/Client/ArchiveBrowserTests.cs`: verifies category pages render the shared archive browser, folder icon markup, and disabled/unavailable Trash create-folder UI where practical.

## Manual Verification

1. Create a local archive root with `Videos`, `Pictures`, `Music`, `Documents`, `Books`, `Downloads`, `Shared`, `Family`, `History`, and `Trash`.
2. Set `VIDEO_ROOT` in `.env` if the archive root is not `/home/PereneArchive`.
3. Start the app with `make docker-run-bg`.
4. Open the loopback URL configured by `WEBAPP_PORT` or the default `http://localhost:8080`.
5. Open each sidebar category and confirm only the contents inside that default folder are shown.
6. In Videos, create a nested folder, place or move a supported video into it from the host, open the folder, select the video, and confirm the existing player/editor appears.
7. In Photos, Music, Documents, Books, Downloads, Shared, Family, and History, create a folder and verify invalid names show validation errors.
8. Rename and move a test folder inside a category and confirm it remains contained in that category.
9. Delete a test folder or file from a category and confirm it appears in Trash and still exists on disk under the Trash folder.
10. Confirm Trash does not offer folder creation.
11. Run `make test`.
12. Stop the app with `make docker-down`.

## Definition of Done

- Requirements, Plan, and Validation docs are updated in this spec folder.
- All existing tests still pass through `make test`.
- Archive service, archive endpoint, updated video, and focused client behavior have test coverage matching existing xUnit and `WebApplicationFactory` conventions.
- UI changes follow the existing Bootstrap/design-system contract, including responsive layout, accessible controls, and empty/loading/error/pending states.
- Browser-facing contracts preserve opaque IDs and do not expose physical or root-relative paths.
- Docker Compose and `.env.example` document the archive-root mount clearly.
- Vendor-specific Blazor and Minimal API decisions are supported by current Microsoft Learn evidence in `Plan.md`.

## Rollback Plan

- Revert `docker-compose.yml` and app configuration to the previous `VideoLibrary:Path=/videos`, `VideoCut:Path=/videos-cuts`, and `VideoComposition:Path=/videos-composition` setup.
- Remove or disable `MapArchiveEndpoints()` and archive service registrations in `WebApp/WebApp/Program.cs`.
- Restore sidebar category pages to `ComingSoonSection` placeholders and restore `Home.razor` to the previous Videos grid/editor flow.
- Keep user files safe: because delete is implemented as move-to-Trash only, rollback does not require restoring permanently deleted data.
