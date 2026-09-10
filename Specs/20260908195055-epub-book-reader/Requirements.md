# Requirements: EPUB Book Reader

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

The application already browses the PereneArchive `Books` category through `WebApp/WebApp.Client/Pages/Books.razor`, but EPUB files behave like ordinary archive files and cannot be opened, read, navigated, or annotated from inside the app. The Books experience needs a private, server-backed EPUB reader that keeps the archive path boundary intact, renders readable book content in the existing Blazor UI, supports reader preferences, and appends Kindle-style highlights/notes to `Books/Notes/pereneArchiveBookNotes.txt`.

## User Stories

- Given I am browsing `Books`, when I select an `.epub` file, then the app opens a book reader instead of doing nothing or treating it as an unsupported file.
- Given an EPUB is open, when I use the table of contents, then I can jump directly to a selected chapter and continue reading there.
- Given I am reading, when I change theme, font size, or font family, then the visible reading surface updates without leaving the book.
- Given I select text in the reader, when I choose the save-note action, then the selected text is appended to the shared Kindle-style notes file.
- Given I reopen a book, when a lightweight progress file exists, then the reader restores the last chapter and position without a database.

## Functional Requirements

1. FR1 — `ArchiveService` must classify `.epub` files in the `books` category as browser-visible book items without exposing physical or root-relative paths.
2. FR2 — `ArchiveEndpoints` must expose server-side EPUB metadata, cover, table-of-contents, chapter content, note-saving, and progress endpoints using only opaque archive item IDs.
3. FR3 — The server must read EPUB files with `VersOne.Epub`, extract title/author/cover/navigation/reading-order content, and sanitize extracted chapter HTML before sending it to the browser.
4. FR4 — `Books.razor` and the archive grid must show EPUB items with a tall rectangular book-cover card, using the embedded cover when available and a Bootstrap Icons fallback when unavailable.
5. FR5 — Selecting an EPUB from the Books grid must open an in-app reader overlay or detail surface that follows the visual language of `Player.razor`, `MediaPlayerControls.razor`, and `ImageCarouselViewer.razor`.
6. FR6 — The reader must display readable chapter content, preserving safe EPUB formatting where practical while favoring app-owned typography, spacing, and color tokens over raw publisher styling.
7. FR7 — The reader must include a visible table-of-contents/index panel that supports nested entries and lets the user jump to a chosen chapter.
8. FR8 — The reader must support chapter navigation controls, including jumping to the previous/next chapter and jumping to a selected table-of-contents entry.
9. FR9 — The reader must support light/dark presentation using the app's existing theme system and a local reader mode that can be changed while the reader is open.
10. FR10 — The reader must support font-size controls with bounded values that remain readable on mobile and desktop.
11. FR11 — The reader must support font-family selection among Montserrat as the default, Arial, and Times New Roman.
12. FR12 — The reader must detect selected text inside the readable content and enable a save-note button only when a non-empty text selection belongs to the current book reader.
13. FR13 — Saving a note must append one shared Kindle-style entry to `${ArchiveRoot}/Books/Notes/pereneArchiveBookNotes.txt`, creating `Books/Notes` and the file when they do not exist.
14. FR14 — Saved note entries must keep the Kindle clipping structure: book title/author line, metadata line with highlight/note language, generated chapter/text offset location, added timestamp, selected text, and `==========` separator.
15. FR15 — The reader must provide a copy-selection action that copies selected reader text to the clipboard without saving it to the notes file.
16. FR16 — The reader must persist progress per book in a lightweight file under `Books/Notes` or another spec-defined local archive subfolder, storing only opaque-safe book identity plus chapter/position data and no database state.
17. FR17 — Unsupported, malformed, missing, or unreadable EPUB files must show accessible error feedback and must not leak physical paths in API responses, UI, or normal logs.

## Non-Functional Requirements

- Preserve the archive privacy boundary: browser-facing responses may include opaque IDs, metadata, sanitized chapter HTML/text, and endpoint URLs, but never physical or root-relative paths.
- Keep EPUB parsing and filesystem writes server-side. The WebAssembly client must not receive direct filesystem paths or parse source files locally.
- Keep note and progress persistence simple and local-file based. Do not introduce a database for this feature.
- Add focused abstractions such as `IEpubBookService`, `IEpubNoteService`, and `IEpubProgressService` so endpoint and UI behavior remains testable with fakes.
- Sanitize EPUB HTML before rendering because EPUB content is user-owned archive data and may contain active markup, scripts, remote images, or unexpected publisher CSS.
- Follow the design system in `Specs/20260827194328-perene-tech-design-system-refactor/design-guide-en.html`: Bootstrap components/utilities first, currentColor Bootstrap Icons, Kindle-paper light mode, dark mode contrast, Montserrat/Zilla Slab defaults, and scoped CSS only for reader-specific layout/selection behavior.
- Keep reader layout responsive, with non-overlapping toolbar controls, accessible names for icon buttons, keyboard-operable navigation, focus restoration when the reader closes, and live status/error regions.

## Out of Scope

- PDF, MOBI, AZW, AZW3, CBZ, CBR, and other ebook formats.
- Cloud sync, multi-user accounts, remote hosting, authentication, or external note services.
- Full Kindle location compatibility. Generated chapter/text offsets are acceptable as long as the clipping file keeps the same parse-friendly shape.
- Editing, deleting, searching, or deduplicating saved notes in this first implementation.
- Rich annotation colors, bookmarks, or database-backed reading history.
- DRM-protected EPUB support.

## Open Questions

- The exact generated location format should be finalized during implementation after inspecting parsed chapter structure, with a bias toward stable chapter index plus text offset ranges.
- The sanitization allowlist needs implementation validation against realistic EPUB content so formatting is preserved without allowing active or remote content.
