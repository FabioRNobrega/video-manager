# Validation: EPUB Saved Highlights

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | Saving creates the unchanged Kindle-style note entry and a separate highlight record. |
| FR2 | Highlight JSON records are private-version-scoped and contain validated chapter range/quote/context data. |
| FR3 | Invalid, external, mismatched, or out-of-range selections are rejected without writing a highlight. |
| FR4 | Reopened/current-chapter notes appear with a visible translucent yellow background in both reader themes. |
| FR5 | Highlights survive pagination/layout changes and do not display for a changed book version. |
| FR6 | Missing/malformed data yields no highlights and atomic writes leave no temp file. |

## Test Cases

- `WebApp.Tests/Services/EpubHighlightServiceTests.cs`: save/load, version isolation, malformed file fallback, atomic temp cleanup, and concurrent writes.
- `WebApp.Tests/Endpoints/ArchiveEndpointsTests.cs`: opaque-ID resolution, range/quote validation, note/highlight persistence, and path-safe failures.
- `WebApp.Tests/Services/EpubNoteServiceTests.cs`: Kindle text format regression.

## Manual Verification

1. Reset the user-owned notes/highlights test files as desired, then run `make docker-run-bg`.
2. Open an EPUB, save one or more selections in different chapters, and confirm yellow highlights appear immediately.
3. Change font, line spacing, width, reader mode, page/chapter, and reopen the book; confirm the correct passages remain highlighted.
4. Replace the EPUB with a changed version and confirm prior highlights do not appear.
5. Run `make test` and stop with `make docker-down`.

## Definition of Done

- All FRs have tests and manual validation coverage.
- Kindle text notes and progress JSON remain compatible and unmodified in purpose.
- No browser-facing response contains physical paths.
- `make test` passes.

## Rollback Plan

- Remove the highlight service registration/endpoints and reader highlight application; leave existing text notes and progress intact. The sidecar JSON remains user-owned unless explicitly removed.
