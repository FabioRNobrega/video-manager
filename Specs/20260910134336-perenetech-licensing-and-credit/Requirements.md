# Requirements: PereneTech Licensing and Credit

## Table of Contents

- [Problem Statement](#problem-statement)
- [User Stories](#user-stories)
- [Functional Requirements](#functional-requirements)
- [Non-Functional Requirements](#non-functional-requirements)
- [Out of Scope](#out-of-scope)
- [Open Questions](#open-questions)

## Problem Statement

Perene Archive currently has no repository license or public route for obtaining commercial support. The owner wants people to be able to use, modify, and share the implementation for noncommercial purposes, while preserving PereneTech attribution and directing commercial users to PereneTech. The running application should also visibly identify PereneTech in the desktop sidebar footer.

## User Stories

- Given I am evaluating the repository, when I read its license, then I can determine that noncommercial use, modification, and redistribution are permitted and PereneTech credit must be retained.
- Given I need commercial use or technical support, when I read the README, then I can contact PereneTech at `fabio.r.nobrega@gmail.com`.
- Given I use the desktop application shell, when I view the sidebar footer, then I see `Copyright © 2026 PereneTech.`

## Functional Requirements

1. FR1 — The repository root must contain an unmodified PolyForm Noncommercial License 1.0.0 with the required notice `Copyright © 2026 PereneTech.`.
2. FR2 — `README.md` must identify PolyForm Noncommercial 1.0.0 as the repository license and state that commercial use, managed deployment, customization, and support require a separate written agreement with PereneTech at `fabio.r.nobrega@gmail.com`.
3. FR3 — `WebApp/WebApp.Client/Layout/Sidebar.razor` must render `Copyright © 2026 PereneTech.` in the desktop sidebar footer without removing existing privacy, theme, or storage controls.
4. FR4 — Client coverage must verify that the sidebar component declares the PereneTech credit.

## Non-Functional Requirements

- Preserve the official PolyForm license text without edits other than placing the required notice outside the license text.
- The sidebar credit must use existing Bootstrap typography and color utilities and remain readable in both supported themes.
- Do not add client-side state, JavaScript, external packages, network calls, or configuration.

## Out of Scope

- A signed commercial license, support agreement, service-level agreement, billing system, or payment workflow.
- Enforcing an in-app credit in modified or redistributed versions beyond the repository license terms.
- Changes to archive privacy boundaries, media behavior, or navigation.

## Open Questions

- None.
