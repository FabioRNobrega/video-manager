# Requirements: Empty Trash (Permanent Delete)

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

Today, deleting an item from any archive category (`ArchiveBrowser.razor` → `DeleteAsync`) only moves it into the Trash category via `IArchiveService.MoveToTrash`. `ArchiveService.MoveToTrash` (`WebApp/WebApp/Services/ArchiveService.cs:211`) explicitly throws `ArchiveForbiddenException` when called against an item that is already in Trash ("Trash items cannot be deleted permanently."), and `ArchiveBrowser.razor` hides the "Move to Trash" action entirely when `Category == "trash"` (line 232). There is currently no way to permanently remove a file or folder from disk anywhere in the app — Trash only ever grows. Users need a way to actually reclaim disk space and remove sensitive/unwanted data for good.

## User Stories

- Given the Trash page has one or more items, when the user clicks "Empty Trash", then a confirmation dialog appears before anything is deleted.
- Given the confirmation dialog is open, when the user confirms, then every item currently in the Trash category root is permanently removed from the local filesystem and the Trash listing becomes empty.
- Given the confirmation dialog is open, when the user cancels (or dismisses it), then no files are deleted and the Trash listing is unchanged.
- Given the Trash is already empty, when the user views the Trash page, then the "Empty Trash" button is visible but disabled.
- Given one or more items fail to delete (e.g. a file is locked/in use), when the deletion runs, then no items are deleted and the user sees an error explaining the operation failed, per the all-or-nothing behavior.

## Functional Requirements

1. FR1 — The Trash page toolbar (`ArchiveBrowser.razor` card-header, alongside the breadcrumb/search area) displays an "Empty Trash" button using the `bi-recycle` icon, visible only when `Category == "trash"`.
2. FR2 — The "Empty Trash" button is disabled when the Trash listing has zero items (`_listing.Items.Count == 0`), matching the existing `disabled="@_operationPending"` pattern used by other toolbar controls.
3. FR3 — Clicking "Empty Trash" opens a confirmation modal (following the existing in-page modal pattern used by Move/Rename in `ArchiveBrowser.razor`, e.g. `_moving`/`_renaming` state fields + `modal fade show d-block` markup) warning that the action permanently deletes all items in Trash and cannot be undone.
4. FR4 — Confirming the modal calls a new endpoint that permanently deletes every item currently at the Trash category root from the local filesystem (files via `File.Delete`, folders recursively via `Directory.Delete(path, recursive: true)`), then refreshes the listing so Trash shows as empty.
5. FR5 — Canceling or dismissing the modal performs no filesystem changes and closes the dialog, consistent with `CancelDialogs()`.
6. FR6 — The new backend operation (`IArchiveService.EmptyTrash` or equivalent) is only valid for the `trash` category; invoking it for any other category throws `ArchiveForbiddenException`, mirroring the existing guard in `MoveToTrash`.
7. FR7 — The delete operation is all-or-nothing: if any item cannot be deleted (e.g. `IOException`, `UnauthorizedAccessException`), no items already processed are left half-deleted in a way that misleads the user — the operation reports failure and the user sees a generic error banner (existing `_error` alert pattern in `ArchiveBrowser.razor`), and any items that remain undeleted stay visible in Trash on the next reload.
8. FR8 — After a successful empty operation, the UI reloads the Trash listing (via the existing `SendMutationAsync` → listing refresh pattern) so the grid immediately reflects the now-empty Trash.

## Non-Functional Requirements

- The permanent-delete code path must never be reachable for non-`trash` categories, preserving the existing safety boundary that only Trash contents can be permanently destroyed.
- The endpoint must be a `DELETE` on a Trash-scoped route (not overloading the existing single-item `DELETE /api/archive/{category}/items/{id}`, which is reserved for move-to-trash semantics) to avoid ambiguity between "move to trash" and "permanently delete."
- Follows the existing layered design: `ArchiveEndpoints.cs` stays a thin HTTP adapter, filesystem logic lives in `ArchiveService`/`IArchiveService`, matching how `MoveToTrash` is structured today.
- No new frontend JS interop or npm/JS packages are required; reuse the existing Bootstrap modal markup pattern already inlined in `ArchiveBrowser.razor` (no `data-bs-toggle` needed since Move/Rename already show modals via component state, not Bootstrap JS triggers).

## Out of Scope

- Per-item permanent delete from within Trash (deleting a single trashed item without emptying everything) — not requested and not currently exposed anywhere in the UI.
- A "restore from Trash" feature.
- Auto-expiring Trash items after N days.
- Any retry/partial-cleanup UI for the failure case beyond a single error message.

## Open Questions

None — all material questions were resolved during discovery (delete scope = everything recursively; button placement = toolbar; failure mode = all-or-nothing; empty-state = always visible but disabled).
