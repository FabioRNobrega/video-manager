# Validation: EPUB Book Reader

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | A `.epub` file under `Books` appears in `/api/archive/books/items` with `IsBook = true`, while the same extension outside `books` is not opened as a book. |
| FR2 | EPUB endpoints resolve only opaque IDs from `ArchiveService` and return `404` for stale, non-book, path-shaped, or wrong-category IDs. |
| FR3 | A valid EPUB returns title, author, table of contents, cover data when present, and sanitized chapter content without scripts or unsafe attributes. |
| FR4 | The Books grid renders EPUB files as portrait book cards with embedded covers when available and an accessible icon fallback otherwise. |
| FR5 | Selecting an EPUB opens the reader surface without starting the persistent audio/video player. |
| FR6 | Chapter text is readable in light and dark modes, preserves basic semantic formatting, and does not inherit unsafe publisher styling. |
| FR7 | The reader displays nested table-of-contents entries and marks or announces the current chapter. |
| FR8 | Previous/next chapter controls and table-of-contents jumps load the correct chapter content. |
| FR9 | Reader light/dark mode changes the reading surface while preserving global app theme compatibility. |
| FR10 | Font-size controls adjust text within defined bounds and remain responsive on mobile and desktop. |
| FR11 | Font-family selection supports Montserrat, Arial, and Times New Roman, with Montserrat selected by default. |
| FR12 | Save note is disabled with no selection and enabled only for non-empty text selected inside the current reader. |
| FR13 | Saving a note creates or appends `${ArchiveRoot}/Books/Notes/pereneArchiveBookNotes.txt`. |
| FR14 | Saved entries match the Kindle-style structure expected by the sample `My Clippings.txt`, including separator lines. |
| FR15 | Copy selection copies selected reader text without appending to the notes file. |
| FR16 | Closing and reopening a book restores last saved chapter/position from a lightweight local progress file. |
| FR17 | Malformed or unreadable EPUBs show accessible UI errors and API responses/log-visible messages do not include physical paths. |

## Test Cases

**Unit tests:**

- `WebApp.Tests/Services/ArchiveServiceTests.cs`: verify `.epub` classification only for the Books category, `TryResolveBook` success/failure, and `Books/Notes` folder handling.
- `WebApp.Tests/Services/EpubContentSanitizerTests.cs`: verify allowed formatting survives and scripts, event attributes, iframes, forms, remote resource URLs, and unsafe styles are stripped.
- `WebApp.Tests/Services/EpubNoteServiceTests.cs`: verify note file creation, append behavior, Kindle-style separators, title/author metadata, generated location text, and serialized concurrent appends.
- `WebApp.Tests/Services/EpubProgressServiceTests.cs`: verify progress save/load, missing progress fallback, malformed progress fallback, and temp-file/atomic replacement behavior.
- `WebApp.Tests/Services/EpubBookServiceTests.cs`: verify metadata, navigation, cover, and chapter extraction from a small EPUB fixture.

**Integration tests:**

- `WebApp.Tests/Endpoints/ArchiveEndpointsTests.cs`: verify `/api/archive/books/items` returns browser-safe EPUB fields and no archive root paths.
- `WebApp.Tests/Endpoints/ArchiveEndpointsTests.cs`: verify book metadata/chapter/cover endpoints for a valid EPUB fixture.
- `WebApp.Tests/Endpoints/ArchiveEndpointsTests.cs`: verify note-saving endpoint appends to `Books/Notes/pereneArchiveBookNotes.txt` and rejects invalid IDs.
- `WebApp.Tests/Endpoints/ArchiveEndpointsTests.cs`: verify progress endpoint writes and reads lightweight progress for the same EPUB.
- `WebApp.Tests/Endpoints/ArchiveEndpointsTests.cs`: verify malformed EPUB endpoint response is controlled and path-safe.
- ⚠️ TODO: Add or identify a reusable minimal EPUB fixture that can be committed without licensing concerns.

## Manual Verification

1. Place at least one non-DRM `.epub` with a cover and one `.epub` without a cover under `${VIDEO_ROOT:-/home/PereneArchive}/Books`.
2. Start the app with `make docker-run-bg`.
3. Open the app and navigate to Books.
4. Confirm EPUB cards use a tall book-cover shape, show embedded covers when available, and use the fallback book icon otherwise.
5. Open an EPUB and verify the reader loads metadata, chapter content, and the table of contents.
6. Jump to several table-of-contents entries and use previous/next chapter controls.
7. Change reader light/dark mode, font size, and font family; verify text remains readable and controls do not overlap on desktop and mobile widths.
8. Select a passage, copy it, and paste elsewhere to confirm clipboard behavior.
9. Select a passage and save it as a note.
10. Confirm `${VIDEO_ROOT:-/home/PereneArchive}/Books/Notes/pereneArchiveBookNotes.txt` exists and contains one Kindle-style entry with `==========`.
11. Close the reader, reopen the same EPUB, and verify the saved chapter/position is restored.
12. Stop the app with `make docker-down`.

## Definition of Done

- Requirements, Plan, and Validation docs are updated in this spec folder.
- Server EPUB parsing, note persistence, progress persistence, and endpoint code are covered by xUnit tests.
- Existing tests still pass through `make test`.
- New UI follows the design-system spec, Bootstrap-first composition, Bootstrap Icons, accessible labels, responsive states, empty/loading/error states, and reduced-motion expectations.
- No browser response, UI text, or normal log message exposes physical or root-relative archive paths.
- Vendor-specific decisions are supported by the EpubReader and Microsoft Learn documentation cited in `Plan.md`.
- Package additions are limited to server-side EPUB/HTML parsing and are restored through the existing Docker Compose workflow.

## Rollback Plan

- Remove the EPUB endpoint mappings from `WebApp/WebApp/Endpoints/ArchiveEndpoints.cs` and the service registrations from `WebApp/WebApp/Program.cs`.
- Revert `ArchiveItemDto`, `ArchiveItemEntry`, `IArchiveService`, and `ArchiveService` EPUB classification changes so `.epub` files return to ordinary archive-file cards.
- Remove `EpubReader.razor`, `EpubReader.razor.css`, and `ebookReader.js` references from `Books.razor` and `ArchiveBrowser.razor`.
- Leave `Books/Notes/pereneArchiveBookNotes.txt` in place as user-owned data unless the user explicitly chooses to remove generated notes.
