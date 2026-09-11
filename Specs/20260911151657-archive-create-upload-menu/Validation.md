# Validation: Archive Create Menu and Upload

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | On any category with `CanCreateFolder == true`, the "+" control opens a Bootstrap dropdown listing "New folder", "Text file", "Markdown file". |
| FR2 | Choosing "New folder" behaves exactly as before: the existing modal opens, submitting creates a folder via `POST /api/archive/{category}/folders`, and it's unaffected by the new menu. |
| FR3 | Choosing "Text file" or "Markdown file" opens a name-prompt modal; submitting calls `POST /api/archive/{category}/files` with the correct `Extension` (`.txt` or `.md`). |
| FR4 | `ArchiveService.CreateFile` rejects creation when `CanCreateFolder == false`, when the name is invalid/reserved, or when a same-named item already exists, mirroring `CreateFolder`'s exceptions. |
| FR5 | After creating a Text/Markdown file, `TextDocumentEditor.razor` opens automatically with the new empty file loaded, without a manual click on the created card. |
| FR6 | An "Upload" button is visible next to Create on every category where `CanCreateFolder == true`, and absent on Trash. |
| FR7 | Selecting multiple files in the Upload picker uploads each one via `POST /api/archive/{category}/upload` and shows a per-file status (Uploading/Done/Error) in a status panel. |
| FR8 | `ArchiveService.SaveUploadedFile` rejects an unsupported extension, an invalid/reserved name, a name conflict, or an attempted path escape, mirroring `CreateFolder`/`CreateFile`'s validation. |
| FR9 | A rejected or interrupted upload never leaves a partial/invalid file in the category root, and one file's failure does not stop the remaining files in the same batch from uploading. |
| FR10 | After each successful upload, the folder grid refreshes and shows the new file without a manual page reload. |
| FR11 | Every page rendering `ArchiveContentHost`/`ArchiveBrowser` (Videos, Photos, Music, Documents, Books, Downloads, Shared, Family, History) shows both new controls consistently; Trash and non-archive utility pages (`VideoCut.razor`, `ImageCutter.razor`, `VideoComposition.razor`) are unchanged. |

## Test Cases

**Unit tests** (extend `WebApp.Tests/Services/ArchiveServiceTests.cs`, following its existing `CreateFolder` test patterns):
- `CreateFile` creates an empty file with the requested extension in the target category/folder.
- `CreateFile` throws `ArchiveForbiddenException` when `CanCreateFolder == false` (e.g. `trash`).
- `CreateFile` throws `ArchiveConflictException` when a same-named file/folder already exists.
- `CreateFile` throws on invalid/reserved names (reuses `ValidateName` coverage already implied by existing `CreateFolder`/`Rename` tests).
- `SaveUploadedFile` writes the uploaded content to the category root and the file is readable afterward with matching bytes.
- `SaveUploadedFile` throws `ArchiveValidationException` for an unsupported extension (e.g. `.exe`) and leaves no file behind.
- `SaveUploadedFile` throws `ArchiveConflictException` on a name collision.
- `SaveUploadedFile` rejects a path-traversal-style name (e.g. `../evil.txt`) via the existing `ContainedPath` boundary.

**Integration tests** (extend `WebApp.Tests/Endpoints/ArchiveEndpointsTests.cs`, using its existing `WebApplicationFactory` setup):
- `POST /api/archive/{category}/files` with a valid `.md` name returns 200 and the file appears in a subsequent `GET /api/archive/{category}/items`.
- `POST /api/archive/{category}/files` against `trash` returns 403.
- `POST /api/archive/{category}/upload` with a valid multipart file returns 200 and the file is retrievable/listed afterward.
- `POST /api/archive/{category}/upload` with an unsupported extension returns 400 and the item does not appear in a subsequent listing.
- ⚠️ TODO: no existing multipart-upload integration test exists in this codebase yet; the first such test will establish the pattern (likely `MultipartFormDataContent` against the in-memory `WebApplicationFactory` client).

## Manual Verification

Starting from a clean state using the project's documented Docker workflow (`make docker-run` per `AGENTS.md`):

1. Run `make docker-run` and open the app at the reported LAN/local URL (`make get-url`).
2. Navigate to Videos (or any non-Trash category). Click "+"; confirm the dropdown shows Folder / Text file / Markdown file.
3. Create a folder via the menu; confirm identical behavior to before this change (modal, name entry, folder appears).
4. Create a Markdown file via the menu; confirm `TextDocumentEditor.razor` opens automatically with an empty document, type some text, save, close, and confirm the file now appears in the grid with the typed content preserved on reopen.
5. Repeat step 4 for a Text file (.txt).
6. Attempt to create a file with a name that already exists in the folder; confirm a conflict error is shown and no duplicate/overwrite occurs.
7. Click "Upload"; select multiple files of different supported types (e.g. one `.mp4`, one `.jpg`, one `.mp3`) at once; confirm a status panel appears showing each file transitioning to "Done" and all three appear in the grid afterward.
8. Attempt to upload a file with an unsupported extension (e.g. rename a local file to `.exe` for the test); confirm it's rejected with a visible per-file error and does not appear in the grid or on disk in the mounted category folder.
9. Navigate to Trash; confirm neither Create nor Upload controls are shown, matching pre-change behavior.
10. Navigate to `/utilities/video-cut` and `/utilities/image-cutter`; confirm neither page is affected (no Create/Upload controls appear there, since they don't render `ArchiveBrowser`).

## Definition of Done

- Requirements, Plan, and Validation docs in this spec folder are complete and internally consistent.
- All existing tests still pass under `make test`.
- New unit tests (`ArchiveServiceTests`) and integration tests (`ArchiveEndpointsTests`) cover `CreateFile` and `SaveUploadedFile` per the Test Cases above.
- UI changes (dropdown, second modal, Upload button, status panel) follow the Bootstrap-first design system contract; loading/error/empty states for uploads are covered (per-file Uploading/Done/Error, and the existing `_error` alert for request-level failures).
- Vendor-specific decisions (`IFormFile` binding, `InputFile` streaming) are backed by Microsoft Learn evidence recorded in `Plan.md`'s External / Vendor Documentation Evidence section before implementation is considered complete, or explicitly marked pending if the MCP server was unavailable.
- `README.md`'s `## Current Supported Features` table is updated to reflect in-app file creation and upload.
- Manual verification steps above are performed against the Docker Compose stack and pass.

## Rollback Plan

- The two new endpoints (`POST /api/archive/{category}/files`, `POST /api/archive/{category}/upload`) and their `IArchiveService` methods are additive; removing the two `MapPost` registrations in `ArchiveEndpoints.cs` fully disables both capabilities server-side without affecting any existing endpoint.
- The client-side change is confined to `ArchiveBrowser.razor`; reverting that single file's dropdown/Upload-button changes back to the original single "+" button restores the exact prior UI with no other component touched (`ArchiveContentHost.razor` and all page files remain unchanged by this spec).
- No migration, schema, or persisted-state change is introduced, so rollback is a plain code revert with no data cleanup required.
