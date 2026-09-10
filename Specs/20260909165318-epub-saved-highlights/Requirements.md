# Requirements: EPUB Saved Highlights

## Problem Statement

Saved EPUB notes are appended to `pereneArchiveBookNotes.txt`, but the reader has no durable, structured range data to redraw those passages as Kindle-like yellow highlights after a chapter reload. Progress is a separate single-position concern and must remain so.

## User Stories

- Given I save a selected passage, when the note succeeds, then that passage is visibly yellow-highlighted when I revisit its chapter.
- Given I change reader layout or reopen the same unchanged EPUB, then saved highlights remain attached to their passages.
- Given a new EPUB version replaces a book, then highlights for its prior version are not applied.

## Functional Requirements

1. FR1 — Saving a note must preserve the existing Kindle-style `pereneArchiveBookNotes.txt` entry and persist a separate structured highlight record.
2. FR2 — Highlight records must be stored in a new private `pereneArchiveBookHighlights.json` sidecar keyed by the current opaque book-version identity, with chapter ID, normalized text start/end offsets, selected quote, nearby context, timestamp, and opaque record ID.
3. FR3 — The note-save request and endpoint must provide and validate the selected range against the server-owned sanitized chapter text before writing either durable record.
4. FR4 — The reader must load only highlights for the current opaque book version and render a translucent Kindle-yellow background around each saved range in light and dark reading modes.
5. FR5 — Highlights must reapply after chapter navigation, reader reopening, and pagination/layout reflow without exposing archive paths.
6. FR6 — Malformed/missing highlight data must fail closed to no highlights without breaking chapter reading; writes must be serialized and JSON publication atomic.

## Non-Functional Requirements

- Do not parse, migrate, or alter legacy text-note entries; the user will start with a clean notes file.
- Keep server filesystem and identity logic server-side; browser DTOs contain only opaque IDs and highlight range data.
- Follow existing `EpubNoteService` append and `EpubProgressService` atomic JSON patterns; add no packages.

## Out of Scope

- Editing/deleting highlights, colors other than the approved yellow, note listing, sharing, and legacy-note migration.

## Open Questions

- None.
