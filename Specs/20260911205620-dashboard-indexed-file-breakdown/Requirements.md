# Requirements: Dashboard Indexed File Breakdown

## Problem Statement

The PereneArchive dashboard reports only one aggregate indexed-file count, which does not show how the archive is composed.

## Functional Requirements

1. FR1 — The archive dashboard response reports counts for videos, audio, EPUB books, images, PDFs, text documents, and other files, in addition to the aggregate total.
2. FR2 — Each physical file under every readable archive category is counted once; the seven type counts reconcile to the aggregate total.
3. FR3 — The PereneArchive card renders the aggregate total and the seven type counts using its existing Bootstrap table.
4. FR4 — Files that cannot be read, directories, and reparse points are excluded without failing the dashboard request.

## Non-Functional Requirements

- Classification remains server-side; no physical or root-relative paths are returned to the browser.
- The scan follows the existing archive-service extension definitions and error-handling approach.
- Unit tests cover supported types and the Other fallback.

## Out of Scope

- New supported media/document formats.
- Per-category or per-folder breakdowns.
