# Validation: PereneTech Licensing and Credit

## Table of Contents

- [Acceptance Criteria](#acceptance-criteria)
- [Test Cases](#test-cases)
- [Manual Verification](#manual-verification)
- [Definition of Done](#definition-of-done)
- [Rollback Plan](#rollback-plan)

## Acceptance Criteria

| Requirement | Acceptance Criterion |
| --- | --- |
| FR1 | Root `LICENSE` contains the PolyForm Noncommercial 1.0.0 text and `Required Notice: Copyright © 2026 PereneTech.`. |
| FR2 | README states the license, the separate-written-agreement requirement for commercial work, and `fabio.r.nobrega@gmail.com`. |
| FR3 | Desktop sidebar footer shows the PereneTech credit while the private/local status, theme toggle, and storage meter remain available. |
| FR4 | A focused client test asserts that the sidebar component contains the exact PereneTech credit. |

## Test Cases

**Integration tests:**

- `WebApp.Tests/Client/ThemeBootstrapTests.cs`: read `Sidebar.razor` and assert it contains `Copyright © 2026 PereneTech.`. The sidebar runs only after Interactive WebAssembly startup, so it is not present in the server-rendered document.

## Manual Verification

1. Run `make docker-run`.
2. Open `http://localhost:8080/`.
3. At a desktop viewport, verify the footer of the left sidebar shows `Copyright © 2026 PereneTech.` and that theme and storage controls continue to work.
4. Read root `LICENSE` and README to verify the required notice and PereneTech contact.
5. Run `make test`.

## Definition of Done

- Requirements, plan, and validation documents are present in this spec folder.
- Root license and README present consistent PereneTech licensing and support information.
- Desktop footer credit is accessible and visually consistent with the existing Bootstrap shell.
- `make test` passes.

## Rollback Plan

Revert the root `LICENSE`, README licensing section, `Sidebar.razor` footer line, and the associated assertion in `ThemeBootstrapTests.cs`. No migrations, persistent data, configuration, or deployed service state are changed.
