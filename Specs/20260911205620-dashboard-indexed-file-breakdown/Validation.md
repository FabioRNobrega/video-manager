# Validation: Dashboard Indexed File Breakdown

| Requirement | Acceptance criterion |
| --- | --- |
| FR1 | `GET /api/dashboard/archive` returns non-negative values for all seven classifications. |
| FR2 | The seven classifications sum to `TotalFiles` for a mixed fixture tree. |
| FR3 | The PereneArchive card renders Videos, Audio, EPUB books, Images, PDFs, Text documents, and Other files. |
| FR4 | An unreadable or skipped path does not cause the archive dashboard endpoint to fail. |

## Tests

- Extend `WebApp.Tests/Services/ArchiveMetricsServiceTests.cs` with mixed extensions, including an unknown extension for Other.
- Run `make test`.

## Rollback

Revert the DTO fields, archive-metrics classifier/counting changes, and card rows together.
