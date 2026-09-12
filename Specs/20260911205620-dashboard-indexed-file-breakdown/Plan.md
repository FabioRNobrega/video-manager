# Plan: Dashboard Indexed File Breakdown

## Summary

Extend the existing `IArchiveMetricsService` → `DashboardArchiveDto` → `DashboardArchiveCard` flow with a server-owned media-type breakdown.

## Implementation

- Add a small server-side classifier for the current Video (`.mp4`, `.webm`, `.mov`, `.m4v`), Audio (`.mp3`, `.wav`, `.m4a`), EPUB (`.epub`), Image (`.jpg`, `.jpeg`, `.png`), PDF (`.pdf`), and text (`.md`, `.markdown`, `.txt`) rules; unrecognized extensions are Other.
- Update `ArchiveMetricsService` to count regular readable files across the existing archive category roots into this breakdown while retaining its current queue/process metrics.
- Extend `DashboardArchiveDto` and render the new rows in `DashboardArchiveCard` with Bootstrap table markup and `text-success` values.
- Extend `ArchiveMetricsServiceTests` with fixture files for every classification and reconciliation assertions.

## Validation

- Run `make test` in the documented Docker Compose environment.
- Confirm the dashboard table shows the total and all seven classifications, without browser-visible paths.

## External Documentation

Not applicable; this extends repository-owned file classification and existing .NET file enumeration.
