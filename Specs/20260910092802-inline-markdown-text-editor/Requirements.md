# Requirements: Inline Markdown and Text Editor

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

`ArchiveBrowser.razor` can list arbitrary files in every PereneArchive category, but its activation flow only opens folders, video, music, images, and Books-only EPUBs. Markdown and plain-text files consequently have no in-app reading or editing experience. Users need to open an existing `.md`, `.markdown`, or `.txt` file from any archive folder into the normal page content area, edit it safely, and see a side-by-side (or mobile-stacked) preview without exposing archive paths or weakening the archive boundary.

## User Stories

- Given I am browsing any PereneArchive category and folder, when I select an existing Markdown file, then its archive listing is replaced in the page content area by an editor showing source and a rendered Markdown preview.
- Given I open an existing text file anywhere in the archive, when I edit its source, then I see the same editor workspace with a whitespace-preserving plain-text preview.
- Given I have unsaved document edits, when I choose Save or press Ctrl+S (Cmd+S on Apple platforms), then the server safely writes the file and reports the saved state.
- Given another NAS process changes the opened file after I load it, when I try to save, then the app detects the revision conflict and does not overwrite the other change.
- Given Markdown contains raw HTML, non-HTTPS links, or image syntax, when it is previewed, then active/raw HTML and images do not render, while safe standard Markdown formatting and HTTPS links do.

## Functional Requirements

1. FR1 — `ArchiveService` and `ArchiveItemDto` must classify existing `.md`, `.markdown`, and `.txt` files in every archive category as browser-visible text documents, while retaining the existing opaque item-ID and path-containment boundary.
2. FR2 — `ArchiveBrowser.razor` must render identifiable Markdown/text file cards and invoke a new text-document selection callback for those files in any category or nested folder; folders and existing video, music, image, and EPUB activation behavior must remain unchanged.
3. FR3 — Each archive page that hosts `ArchiveBrowser.razor` must be able to replace its listing inside the normal content container with one reusable inline editor workspace when a text document is selected; it must return to the same archive browsing context through a visible Back action.
4. FR4 — The server must expose opaque-ID-only endpoints to load text-document source and revision metadata, render a preview from bounded submitted source, and save source conditionally against the loaded revision. Non-text files, folders, invalid category/ID pairs, stale IDs, and traversal-shaped IDs must not be readable or writable through these endpoints.
5. FR5 — Opening a Markdown document must display an editable source pane and a rendered preview pane side-by-side at desktop widths and stacked source-above-preview on mobile; opening a `.txt` document must use the same layout but display an encoded, whitespace-preserving plain-text preview.
6. FR6 — Markdown preview must support CommonMark plus the approved common extensions (tables, task lists, strikethrough, and common emphasis extensions), while disabling raw HTML and preventing all image rendering.
7. FR7 — Markdown links must render only for `https://` destinations. They must open safely in a new browsing context with appropriate `rel` protection; relative, protocol-relative, `http`, `mailto`, `file`, `data`, `javascript`, and malformed destinations must not become live links.
8. FR8 — The editor must have an explicit Save button and support Ctrl+S/Cmd+S. It must show accessible loading, saved, dirty, saving, validation-error, and save-failure/conflict feedback. Autosave is not part of this feature.
9. FR9 — A save request must carry the revision received at load time. The server must reject an external change with `409 Conflict`, preserve both the on-disk version and the in-browser draft, and offer the user an observable recovery path (reload the current disk version or copy the draft before leaving).
10. FR10 — The server must apply a documented maximum source-size limit on load, preview, and save; validate UTF-8 text; re-resolve the file immediately before I/O; write through a contained sibling temporary file and atomically replace the original only after successful validation.
11. FR11 — All document source, rendered HTML, endpoint errors, and normal logs must preserve the project privacy boundary: browser responses and UI may contain opaque IDs, safe names, revision data, and document content, but never physical paths or root-relative paths.
12. FR12 — The text editor must follow the existing Bootstrap/design-system contract, work with dark and Kindle-paper themes, provide labeled keyboard-operable controls and focus management, respect reduced motion, and avoid a new frontend framework or a full-screen/player-style surface.

## Non-Functional Requirements

- Continue the server-owned filesystem pattern in `ArchiveService`/`ArchiveEndpoints`; the Interactive WebAssembly client receives no physical path and makes no filesystem decision.
- Keep parsing, HTML safety enforcement, link policy, revision comparison, size validation, and file writes server-side behind focused, directly testable interfaces. The client owns editor state and rendering only.
- Reuse existing `HtmlAgilityPack` for a final generated-preview allowlist where practical; add a Markdown parser package only if existing dependencies cannot produce CommonMark-plus-extension output with raw HTML disabled. Any package must be server-only, pinned consistently with the project, and covered by restore/build tests.
- Treat archive file content as untrusted despite the private LAN scope. Never render raw source with `MarkupString`, concatenate it into JavaScript/HTML, or expose it in ordinary logs.
- Preview updates must be debounced and cancellable so rapid typing does not create unbounded HTTP requests, renders, memory use, or stale preview results.
- No database, cloud service, account model, filesystem static-file mapping, or background worker is introduced.

## Out of Scope

- Creating blank Markdown or text files; the existing plus action remains folder-only.
- Autosave, collaborative editing, version history, undo across reloads, document search, and offline editing.
- PDF, Office, RTF, HTML, source-code, or other document viewers/editors.
- Raw HTML in Markdown, arbitrary embedded HTML, inline/external images, image upload, local-file images, iframes, forms, scripts, custom CSS, and remote-content fetching.
- Any link scheme other than HTTPS, including relative archive links.
- Editing the media-specific state or behavior of video, music, image, and EPUB items.

## Open Questions

- None for the first implementation slice. The exact maximum text-document size and the compatible, pinned Markdown parser version will be selected during implementation and documented in the resulting code/tests.
