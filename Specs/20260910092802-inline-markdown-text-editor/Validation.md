# Validation: Inline Markdown and Text Editor

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | Listings in Documents, Downloads, Photos, Books, and another nested archive folder mark `.md`, `.markdown`, and `.txt` items as text documents without including the archive root/path in JSON. |
| FR2 | Selecting a recognized text-document card invokes the text-document flow; folders and video/music/image/EPUB selection retain their current behavior. |
| FR3 | Every archive category can show the editor inside its normal page content area and Back returns to the previous archive listing/folder context. |
| FR4 | Load, preview, and save routes resolve only recognized text document IDs; invalid IDs, folders, other file types, and traversal-like values are rejected without path leakage. |
| FR5 | Markdown and TXT show source/preview panes horizontally at desktop size and vertically with source first at mobile size; TXT preview preserves line breaks and whitespace. |
| FR6 | Markdown CommonMark and approved table/task-list/strikethrough/emphasis extension fixtures render correctly; raw HTML and image syntax produce no active/raw HTML or images. |
| FR7 | HTTPS Markdown links render as protected links opening a new context; every other scheme and relative URL is non-live text. |
| FR8 | Save works by button and Ctrl+S/Cmd+S, has no autosave, avoids duplicate save dispatch, and announces all editor states accessibly. |
| FR9 | A disk mutation after loading produces 409, does not change the disk file, preserves the draft, and offers reload/copy-draft recovery. |
| FR10 | Oversized, invalid UTF-8, invalid request, and failed write cases are controlled; a successful save replaces only the resolved contained file and returns a new revision. |
| FR11 | Endpoint bodies, UI errors, and test-visible error responses contain no physical or root-relative archive path. |
| FR12 | The workspace follows Bootstrap/theme tokens, works with keyboard and reduced motion, has labeled controls/live regions, and is not an overlay/full-screen player. |

## Test Cases

**Unit tests:**

- `WebApp.Tests/Services/ArchiveServiceTests.cs` — add theory coverage proving `.md`, `.markdown`, and `.txt` classify and safely resolve in multiple categories/nested folders; prove folders, media, EPUB, unknown extensions, stale IDs, and invalid IDs do not resolve as text documents.
- `WebApp.Tests/Services/TextDocumentServiceTests.cs` — use temporary fixture files to cover bounded UTF-8 loading, document kind, revision generation, CommonMark-extension rendering, HTML/image removal, exact HTTPS-only link filtering, plain-text whitespace preview, canceled work, external-change conflicts, temp/atomic write behavior, and source limits.
- `WebApp.Tests/Client/TextDocumentEditorStateTests.cs` — if state is extracted, cover dirty/saved state transitions, explicit-save guard, keyboard-save recognition, latest-preview response application, and conflict recovery choices.

**Integration tests:**

- `WebApp.Tests/Endpoints/ArchiveEndpointsTests.cs` — use the existing `VideoManagerFactory` archive fixture to exercise load/preview/save through HTTP. Assert status codes/content contracts, JSON has no fixture-root path, a valid save changes only the expected file, old revision returns 409 after an external write, and unsupported/oversize/malformed paths are rejected.
- Run the complete suite only through `make test`; it uses the repository’s disposable Docker Compose test stack.

## Manual Verification

1. Create representative existing `.md`, `.markdown`, and `.txt` files in different `${VIDEO_ROOT}` archive category folders, including a nested folder. Include CommonMark headings/lists/code, a table, task list, strikethrough, an HTTPS link, raw `<script>`/`<img>` HTML, Markdown image syntax, an HTTP link, and a relative link.
2. Run `make docker-build`, then `make docker-run` from the repository root.
3. Open each relevant archive page on desktop. Select each text file and verify the normal content container changes to the workspace rather than a fullscreen viewer; Back restores the same folder listing.
4. Verify desktop has source left and preview right. Narrow the browser to a mobile width and verify source is above preview, both panes remain usable, and controls do not overlap.
5. Edit a Markdown source document. Verify the preview updates after the debounce, preserves approved syntax, renders only the HTTPS link, does not render images/raw HTML, and opens the HTTPS link in a new tab/window safely.
6. Edit a TXT file with blank lines and repeated spaces. Verify source and preview preserve the expected text layout and no Markdown formatting occurs.
7. Verify the Save button and Ctrl+S/Cmd+S save the document, announce Saved, update its timestamp/listing after Back, and do not save merely from typing.
8. Load a file in the app, edit it through another local/NAS process, then try Save. Verify a visible conflict, unchanged disk version, retained draft, and functional Reload/Copy draft recovery controls.
9. Toggle dark/light theme and reduced-motion preference. Verify contrast, pane readability, focus visibility, keyboard operation, and no problematic transitions.
10. Inspect browser network responses and normal application logs for the test session. Verify no archive physical/root-relative path appears.

## Definition of Done

- Requirements, Plan, and Validation documents are complete in this spec folder.
- All existing tests and the new unit/integration tests pass through `make test`.
- The server retains opaque-ID, contained-path, no-path-log, and Docker-only constraints.
- The new parser dependency is server-only, version-pinned, restore/build verified, and its raw-HTML/link behavior is covered by tests.
- UI changes follow the design guide, Bootstrap-first styling, responsive behavior, accessibility, dark/Kindle-paper themes, and reduced-motion requirements.
- Vendor-specific security/resource decisions remain supported by the Microsoft Learn evidence in `Plan.md`.

## Rollback Plan

- Revert this spec’s implementation commit(s), which removes the text-document routes from `ArchiveEndpoints`, `ITextDocumentService` registration in `Program.cs`, text-document classification/activation, and `ArchiveContentHost`/`TextDocumentEditor` rendering.
- No migration, persistent schema, external infrastructure, cache, or configuration change is introduced. Existing archive files remain unchanged except documents deliberately saved by users before rollback; those retain their normal file-system recoverability through the archive/NAS backup process.
