# Validation: Empty Trash (Permanent Delete)

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | On the Trash page, the toolbar (card-header area) shows an "Empty Trash" button rendering `<i class="bi bi-recycle">`; the button does not render on any other category's page. |
| FR2 | With Trash empty, the "Empty Trash" button is present but has the `disabled` attribute; with at least one item, it is enabled. |
| FR3 | Clicking "Empty Trash" (when enabled) opens a modal with a permanent-deletion warning and Cancel/Confirm actions, before any HTTP request is sent. |
| FR4 | Confirming sends `DELETE /api/archive/trash/items`; after a successful response, every file/folder previously in the Trash root is gone from disk and the Trash grid shows the empty state. |
| FR5 | Clicking Cancel (or the modal's close control) closes the modal without any HTTP request and without any filesystem change. |
| FR6 | Calling `IArchiveService.EmptyTrash` with any category key other than `"trash"` throws `ArchiveForbiddenException`; calling `DELETE /api/archive/{otherCategory}/items` returns the same error status `MoveToTrash`'s forbidden case returns today. |
| FR7 | If a file in the Trash root cannot be deleted (simulated via a locked file/read-only permission in a test), the endpoint returns an error status, the `_error` alert renders in the UI, and the item remains present in Trash after reload. |
| FR8 | After a successful empty operation, `ArchiveBrowser.razor`'s `_listing` is refreshed via the existing `SendMutationAsync` flow and the grid re-renders as empty without a manual page refresh. |

## Test Cases

**Unit tests (`WebApp.Tests/Services/ArchiveServiceTests.cs`, xUnit, following existing `[Fact]` conventions like `MoveToTrash_moves_item_without_permanent_delete` at line 171):**

- `EmptyTrash_deletes_all_files_in_trash_root`: seed the trash folder with multiple files, call `EmptyTrash("trash")`, assert none exist on disk and the returned `ArchiveListing` has zero items.
- `EmptyTrash_deletes_folders_recursively`: seed a folder containing nested files inside the trash root, call `EmptyTrash("trash")`, assert the folder and its contents no longer exist.
- `EmptyTrash_on_non_trash_category_throws_ArchiveForbiddenException`: call `EmptyTrash("documents")` (or any other category key), assert `ArchiveForbiddenException` is thrown and no files under `documents` are touched.
- `EmptyTrash_with_empty_trash_returns_empty_listing_without_error`: call `EmptyTrash("trash")` when the trash root has zero entries, assert it returns successfully with an empty listing (no exception).
- ⚠️ TODO: `EmptyTrash_stops_on_first_failure_and_reports_conflict` — requires a way to simulate a locked file/denied delete in a unit test (e.g. an open `FileStream` held during the call, or a read-only file on the test OS); assert `ArchiveConflictException` (or the chosen mapped exception) is thrown and any entries already deleted before the failing one are not resurrected, matching FR7's "stop, don't lie about success" behavior.

**Integration tests (`WebApp.Tests/Endpoints/ArchiveEndpointsTests.cs`, following existing endpoint test conventions):**

- `DELETE /api/archive/trash/items` with items present returns 200 and an empty listing payload.
- `DELETE /api/archive/documents/items` (or another non-trash category) returns the same forbidden status code the existing `MoveToTrash`-on-trash-item forbidden case returns.

## Manual Verification

Using this repo's documented run command (check `AGENTS.md`/`README.md` for the exact `dotnet run`/`dotnet watch` invocation for `WebApp/WebApp`):

1. Start the app and upload/create a couple of files in any category (e.g. Documents).
2. Move one file and one folder (containing at least one nested file) to Trash via the existing "Move to Trash" action.
3. Navigate to the Trash page (`/trash`) and confirm both items are listed.
4. Confirm the "Empty Trash" button (bi-recycle icon) is visible and enabled.
5. Click "Empty Trash" and confirm the confirmation modal appears with a permanent-deletion warning.
6. Click Cancel; confirm the modal closes and both items are still listed in Trash.
7. Click "Empty Trash" again, then confirm; verify the Trash grid becomes empty and the button becomes disabled.
8. On the host filesystem, verify the underlying `Trash/` folder (per `GetCategoryRootPath("trash")`) no longer contains the previously-trashed file or folder.
9. Refresh the browser page and confirm Trash still shows empty (proves deletion, not just client-side state clearing).
10. Repeat steps 2–7 while a trashed file is open in another program (e.g. previewed), to observe the all-or-nothing failure path: confirm an error banner appears and the locked item remains in Trash after the failed attempt.

## Definition of Done

- Requirements, Plan, and Validation docs are updated in this spec folder.
- All existing tests still pass (`dotnet test` for `WebApp.Tests`).
- New unit tests for `EmptyTrash` (success, recursive folder delete, forbidden-category guard, empty-trash-no-op) are added and passing; the locked-file/failure test is either implemented or explicitly left as the ⚠️ TODO above if OS-level file locking proves impractical to simulate in the test environment.
- UI changes (toolbar button, confirmation modal) are implemented consistently with existing Move/Rename modal styling, including the disabled-when-empty and disabled-during-`_operationPending` states.
- No responsive/accessibility regressions: the new button and modal use the same Bootstrap classes and `aria-*` attributes as neighboring existing controls (e.g. `aria-label`, `aria-hidden` on icons, `role="dialog"`/`aria-modal` on the modal).

## Rollback Plan

- The feature is additive: a new service method, a new endpoint route, and new UI elements gated behind `Category == "trash"`. To roll back, revert the commit(s) touching `IArchiveService.cs`, `ArchiveService.cs`, `ArchiveEndpoints.cs`, and `ArchiveBrowser.razor` for this spec.
- No migrations, config flags, or persisted data formats are introduced, so rollback carries no data-migration risk. Existing Trash contents (any items not yet emptied) are unaffected by reverting the code.
