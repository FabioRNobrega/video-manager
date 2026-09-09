# Requirements: EPUB Touch Selection Actions

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

`WebApp/WebApp.Client/Components/EpubReader.razor` refreshes `_selectedText` only after `mouseup` or keyboard input. On Android Chrome tablets, a long-press-and-drag text selection can finish without either event, leaving Save Note and Copy Selection disabled. The reader also keeps those actions in the top toolbar instead of presenting them next to the selected passage, which makes touch use unnecessarily awkward.

## User Stories

- Given an EPUB is open in Android Chrome on a tablet, when I long-press and extend a selection with native handles, then a contextual action menu offers enabled Save Note and Copy Selection actions for that passage.
- Given the browser visually changes or collapses a selection as I reach for the contextual menu, when I choose Save Note, then the exact captured passage is saved once.
- Given a non-empty text selection is near an edge of the reader, when the contextual menu is shown, then it is placed above or below the selection where it stays visible and tappable.
- Given no usable reader-owned selection exists, then no contextual selection menu is shown and no note or clipboard action can act on stale text.

## Functional Requirements

1. FR1 — `EpubReader.razor` and `ebookReader.js` must detect reader-owned non-empty selections on Android Chrome touch/pointer interaction as well as mouse and keyboard interaction, without interfering with native long-press selection handles.
2. FR2 — When a valid reader-owned selection is detected, the reader must retain its trimmed text and selection geometry in component state until the user completes, dismisses, replaces, or invalidates that selection.
3. FR3 — The permanent Save Note and Copy Selection controls must be removed from the reader toolbar and replaced with a contextual floating action menu that is rendered only while a valid selection is actionable.
4. FR4 — The contextual menu must expose Save Note and Copy Selection actions with the existing icon, loading, disabled, status, and error behavior; Save Note must continue to use the existing note endpoint and Copy Selection must continue to use the existing clipboard interop.
5. FR5 — The contextual menu must position itself above the selected range when space permits, otherwise below it, and must constrain itself to the visible reader viewport so both actions remain accessible on tablet and desktop layouts.
6. FR6 — Tapping an action in the contextual menu must use the previously captured selection even if focus movement or native browser behavior changes the live DOM selection before the click is handled.
7. FR7 — A selection that is cleared, falls outside the current reader content, or is replaced by a different selection must hide or update the contextual menu promptly; chapter changes, reader reset, and successful note saving must clear the captured selection and native range.
8. FR8 — The contextual menu must meet the project design contract: Bootstrap-first controls, Bootstrap Icons, 40 by 40 CSS-pixel minimum icon target where applicable, visible focus, semantic live status, reader light/dark token compatibility, and no regression to tap-based page navigation.

## Non-Functional Requirements

- The browser must receive only rendered EPUB content and existing opaque IDs; no archive paths, selection metadata containing paths, or new server data may be exposed.
- The change must remain client-side within the existing `EpubReader.razor`/`ebookReader.js` interop boundary; it must not add packages, endpoints, persistence formats, or server-side services.
- Selection listeners must be registered once per reader lifecycle and reliably released during disposal to avoid duplicate callbacks or retained .NET references.
- The presentation must work at tablet and desktop widths, respect reduced-motion expectations, and avoid covering the selected text when a valid alternate placement is available.

## Out of Scope

- iPad Safari-specific support beyond avoiding known regressions; Android Chrome is the initial acceptance browser.
- Custom replacement of Android Chrome's native selection handles or browser context menu.
- Editing, highlighting, sharing, deleting, or listing saved notes.
- Changes to EPUB parsing, note persistence, archive endpoints, or clipboard fallback semantics.

## Open Questions

- None. Android Chrome tablet behavior and the Kindle-like contextual placement are the agreed first scope.
