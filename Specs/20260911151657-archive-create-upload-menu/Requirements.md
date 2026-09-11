# Requirements: Archive Create Menu and Upload

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

Today `ArchiveBrowser.razor` renders a single circular "+" button per category (gated by `ArchiveCategory.CanCreateFolder`) that opens a modal to create a folder only, backed by `POST /api/archive/{category}/folders`. There is no way to create a new `.txt` or `.md` file directly from the archive browser (the user must already have such a file to open `TextDocumentEditor.razor`), and there is no way to upload existing files (videos, music, images, books, text, PDFs) into the archive at all — files can only be renamed, moved, or trashed once they already exist on disk. Every page built on `ArchiveContentHost`/`ArchiveBrowser` (Videos, Photos, Music, Documents, Books, Downloads, Shared, Family, History) is missing both capabilities.

## User Stories

- Given I am viewing a category that allows folder creation, when I click the "+" control, then I see options to create a Folder, a Text file (.txt), or a Markdown file (.md) instead of only a folder-name prompt.
- Given I choose to create a Text or Markdown file, when I submit a name, then an empty file with the matching extension is created in the current folder and immediately opens in `TextDocumentEditor.razor` so I can start typing.
- Given I am viewing a category that allows folder creation, when I look at the category toolbar, then I see an Upload control next to Create that lets me pick one or more files from my device.
- Given I select one or more files to upload, when the upload is in progress, then I see each file's name and its per-file status (uploading/success/error) in a small panel.
- Given I select a file whose extension is not in the archive's supported-type lists, when I attempt to upload it, then it is rejected with a clear per-file error and no partial/invalid file is written to the archive.
- Given a category does not allow folder creation (Trash), when I view that page, then neither Create nor Upload controls are shown, matching today's folder-creation gating.

## Functional Requirements

1. FR1 — `ArchiveBrowser.razor`'s existing "+" create control becomes a menu (Bootstrap dropdown) offering "Folder", "Text file (.txt)", and "Markdown file (.md)", shown only when `_listing.CanCreateFolder == true` (same gating as today).
2. FR2 — Choosing "Folder" from the create menu preserves today's exact behavior: opens the existing new-folder modal and calls `POST /api/archive/{category}/folders`.
3. FR3 — Choosing "Text file" or "Markdown file" opens a name-prompt modal (parallel to the folder modal) that creates an empty file via a new `POST /api/archive/{category}/files` endpoint, sending the chosen name and a `.txt` or `.md` extension.
4. FR4 — The new file-creation endpoint is implemented by a new `IArchiveService.CreateFile(categoryKey, parentId, name, extension)` method in `ArchiveService.cs`, following the same validation as `CreateFolder` (category must have `CanCreateFolder == true`, name validated via `ValidateName`, path containment via `ContainedPath`, conflict check via `Exists`), writing a new empty file with `File.Create` instead of `Directory.CreateDirectory`.
5. FR5 — After a Text/Markdown file is created, `ArchiveBrowser.razor` locates the created item in the refreshed listing (same pattern as `CreateFolderAsync` locating the created folder) and invokes `OnTextDocumentSelected` with it, so `ArchiveContentHost.razor` opens `TextDocumentEditor.razor` immediately, matching the existing open-on-click behavior for text documents.
6. FR6 — `ArchiveBrowser.razor` renders a new "Upload" button next to the Create control, shown under the same `_listing.CanCreateFolder == true` condition, using a hidden `<input type="file" multiple>` triggered by the visible button (existing Bootstrap-first composition, no new JS framing beyond a click-forwarding helper if required).
7. FR7 — The Upload control accepts multiple files at once and uploads them via a new `POST /api/archive/{category}/upload` multipart endpoint, one file at a time from the client, showing a small status panel/list with each file's name and state (`Uploading`/`Done`/`Error`).
8. FR8 — The new upload endpoint is implemented by a new `IArchiveService.SaveUploadedFile(categoryKey, parentId, fileName, stream)` method (or equivalent) that validates the target category's `CanCreateFolder`, validates the file name the same way as `CreateFolder`/`CreateFile`, rejects any extension not present in `VideoExtensions`, `MusicExtensions`, `ImageExtensions`, `BookExtensions`, `TextDocumentExtensions`, or `PdfDocumentExtensions`, checks for name conflicts via `Exists`, and streams the upload to a temp file inside the category root before an atomic move into place (mirroring the temp-file-then-atomic-move pattern already used by the thumbnail/subtitle/cut/composition pipelines).
9. FR9 — Rejected uploads (unsupported extension, name conflict, or server error) never leave a partial or invalid file behind in the archive tree and surface a per-file error in the status panel without blocking the other files in the same batch.
10. FR10 — After each successful upload, `ArchiveBrowser.razor` refreshes the current folder listing (reusing `LoadAsync`) so the uploaded file appears without a manual page reload.
11. FR11 — All new UI (create menu, file-name modal, Upload button, upload status panel) is added only inside `ArchiveBrowser.razor`/its endpoints, so every page built on `ArchiveContentHost` (Videos, Photos, Music, Documents, Books, Downloads, Shared, Family, History) gets both capabilities automatically; Trash keeps neither, and non-archive utility pages (e.g. `VideoCut.razor`, `ImageCutter.razor`) are unaffected since they don't render `ArchiveBrowser`.

## Non-Functional Requirements

- Server-side extension validation is authoritative; client-side `accept` attributes on the file input are a convenience only and must not be the sole enforcement (matches the project's existing pattern of never trusting client input for filesystem writes).
- Uploaded file paths must go through the same `ContainedPath`/canonical-path containment check already used for folder/file creation and rename/move, so an upload can never escape the category's root (same boundary as the read-only video-library mount and the Cuts/VideoComposition write mounts).
- No physical or root-relative paths may be exposed to the browser in upload responses or errors, consistent with the existing opaque-ID constraint.
- Upload and file-creation endpoints follow the existing `ArchiveForbiddenException`/`ArchiveConflictException`/`ArchiveValidationException` → HTTP status mapping already used by `CreateFolder`/`Rename`/`Move` in `ArchiveEndpoints.cs`, so error handling stays consistent across all archive mutations.
- New Razor UI follows the Bootstrap-first design system contract in `Specs/20260827194328-perene-tech-design-system-refactor/`: dropdown menu and modals use standard Bootstrap components/icons, no bespoke CSS beyond what's already scoped in `ArchiveBrowser.razor` (none exists there today).
- `IArchiveService.CreateFile` and `SaveUploadedFile` are added as narrowly-scoped methods on the existing service/interface (no new controller-like god-object), consistent with the current one-interface-per-concern pattern in `IArchiveService`.

## Out of Scope

- Drag-and-drop upload (only the button-triggered file picker is required).
- Uploading folders/directories or zip extraction.
- Creating file types other than `.txt`/`.md` from the Create menu (e.g. no in-app "Spreadsheet"/"Presentation" equivalents, unlike the Google Drive reference screenshot).
- Resumable/chunked uploads or upload size limits beyond what ASP.NET Core's default request body size enforces (no new configuration is introduced unless a real failure is observed).
- Changes to `VideoCut.razor`, `VideoComposition.razor`, `ImageCutter.razor`, or any other utility page that does not render `ArchiveBrowser`.
- Editing an uploaded file's content inline as part of the upload flow (uploaded text files can be opened afterward via the existing text-document flow, not auto-opened like newly created ones).

## Open Questions

- None. No explicit max upload size is needed beyond ASP.NET Core's default `Kestrel`/form-options limits (confirmed — stays deferred per Out of Scope, not a blocking unknown).
