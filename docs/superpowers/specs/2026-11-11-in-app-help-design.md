# C6 — In-App Help and Onboarding: Design Specification

**Roadmap package:** C6 (`improvement-roadmap.md`)
**Status:** Draft for user review
**Date:** 2026-11-11

## 1. Purpose

Put the right help next to the work: the guide chapter for the page in front of the user, short hints on difficult fields, the test's SOP one click away, a short tour for new users, and a note about what changed after an update.

## 2. Decisions (confirmed by the user, 2026-11-11)

| # | Decision | Answer |
|---|---|---|
| 1 | Help text source | The existing user guide, shipped with each release |
| 2 | Test → SOP link | Made in the Test Master (see my reading below) |
| 3 | Onboarding | A short, skippable tour per role |
| 4 | What's new | Release notes shipped with the version, shown once |

**My reading of answer 2 (please confirm, 9.1):** in the Test Master, the Section Head links a test to a controlled document from Document Control, and analysts get an "Open SOP" button on that test.

## 3. Out of scope

- Editing help text inside the application.
- Arabic help (package F4), videos, a chat assistant.
- Blocking a test when the analyst has not been trained on its SOP (package D4).
- Rewriting the guide. C6 uses it as it is; gaps found are listed, not filled.
- The keyboard-shortcut sheet itself (delivered by C4; C6 links to it).

## 4. Current state (from code, not run)

- No help, tour, onboarding or release-note code in the frontend. No version number is shown anywhere.
- A user guide exists: `documentation/guide/` with 45 numbered chapters and about 80 screenshots, plus a combined `USER_GUIDE.md`. `documents/UserManual/` holds only a placeholder.
- Under the project's rules `documentation/` is descriptive, not an approved specification or SOP.
- 193 tooltips and 295 helper texts are written inline; there is no shared hint component.
- `TestDefinition` has a method abbreviation but no link to a controlled document.
- Nothing remembers per-user interface state on the server (first login leads only to the change-password step).
- No markdown renderer is installed.

## 5. Design

### 5.1 Help panel

- A "?" button in the header (and the F1 key) opens a side panel with the guide chapter for the current page. The page stays usable beside it.
- A map links each route to a chapter and, where useful, a heading inside it. Pages without their own chapter open the nearest parent chapter.
- The panel has search across all chapters, "Open the full guide" (a `/help` page with the chapter list), and links to Keyboard shortcuts, What's new and Replay tour.
- A fixed line at the top: "Guidance only. Your approved SOPs take precedence." with the application version.
- The chapters and screenshots are packaged at build time and loaded only when help is opened, so normal pages do not get slower.
- Rendering uses `react-markdown` (MIT) with raw HTML switched off; links inside chapters open in the panel; images have their alt text from the guide.
- **Build checks:** every mapped chapter exists; every route in the main menu has a mapping. A missing one fails the build, so help cannot silently fall behind the menu.

### 5.2 Field hints

- One shared component, `FieldHint`: a small information button beside a label that opens a short explanation. It opens on click or keyboard, not on hover alone, and closes with Escape.
- Hint texts live in one file per module, keyed by field, shipped with the release.
- First release covers the forms users find hardest: specification parameters, Test Master (workflow steps, dilution, CoA order), sample receiving, count entry, media preparation, escalation rules. Each hint is one to three sentences and may link to the guide section.
- Hints explain what a field means and where its value is used. They never state a limit, formula or rule that is not already in the approved configuration or documents.

### 5.3 Test → SOP link

- `TestDefinition` gains an optional link to a controlled document (the document, not a fixed revision).
- In the Test Master the Section Head picks it with a document search limited to effective documents. The change follows the Test Master's normal versioning and audit.
- In the test workflow dialog an "Open SOP" button opens the document's **current effective revision** in the Document Control viewer.
- Access follows Document Control: a user who may not read the document sees the button disabled with the reason.
- If the linked document has no effective revision (withdrawn, or under revision with none effective) the button says so, and the Test Master row shows a warning to the Section Head.
- The link is informative. It takes no part in result evaluation and does not change historical records.

### 5.4 First-login tour

- A tour per role (Analyst, Reviewer, Section Head, QC Manager, System Administrator) of five or six steps: My Work, the workspace or review queue, search (Ctrl+K), notifications, help. Each step highlights one element and has Next, Back and Skip.
- It starts after the first sign-in (after any forced password or second-factor step) and can be replayed from the Help menu.
- Built with the existing component library (no new dependency). Fully keyboard operable; focus moves to each step; it respects the reduced-motion setting; it never blocks the application if a highlighted element is missing (that step is skipped).
- Whether a user has finished or skipped the tour is stored on the server, so it does not reappear on another computer.

### 5.5 What's new and version

- Each release ships a short note per role (a few lines, with links to guide chapters).
- After an update a user sees it once in a dialog; it is always available under Help → What's new, together with earlier notes.
- Help → About shows the application version and the server version.
- The last version a user has seen is stored on the server.

### 5.6 Per-user interface state

A small server store for interface state, `UserUiState` (user, key, value up to 2 KB): tour seen, last release note seen. It is deliberately generic because package C8 needs the same thing for favourites and last-used laboratory. Only the owner can read or write their state. It holds no laboratory data.

## 6. Error handling

| Situation | Behaviour |
|---|---|
| Help content fails to load | The panel shows a retry and a link to the full guide page; the application is unaffected |
| No chapter mapped (should not pass the build) | The panel opens the guide's contents page |
| Linked SOP not readable by the user | Button disabled with the reason |
| Linked SOP has no effective revision | Button disabled: "No effective revision"; warning in Test Master |
| Interface-state store unavailable | Tour and note fall back to this browser's memory; nothing blocks |
| Tour target missing on this screen size | That step is skipped |

## 7. Testing

- **Build / unit:** route-to-chapter map completeness; every chapter file referenced exists; markdown renders with raw HTML disabled (a chapter containing a script tag shows it as text); search finds a term in a chapter body.
- **Backend `[PostgresFact]`:** SOP link saved through the Test Master with audit and version; only an effective document can be linked; resolving the effective revision; a user without read access is refused by the document endpoint; interface state is per user (another user's key answers 404), size limit, unknown key pattern rejected.
- **Frontend (`userEvent`):** help opens with the chapter for the current route and by F1; the page behind stays interactive; search; hint opens by keyboard and closes with Escape; "Open SOP" states (enabled, no access, no effective revision); tour steps, skip, replay, missing target; note shown once per version; About shows both versions.
- **Manual:** screen-reader pass of the panel and the tour; one walk-through per role.

## 8. Delivery stages

1. Interface-state store; version endpoint; Help menu with About.
2. Help panel, chapter map, search, full guide page, build checks.
3. Field hints on the first forms.
4. Test → SOP link.
5. Tours per role.
6. What's new.
7. Documents: requirements; a short SOP note that in-app help is guidance and the approved SOPs govern.

## 9. Open items

1. **Confirm my reading** of the Test → SOP link (section 2).
2. The guide is descriptive text in `documentation/`, not an approved document. If your quality system requires the in-app help to be an approved, controlled text, it must first be reviewed and issued through Document Control (roadmap package A3); C6 can ship before or after that.
3. Should the system **record which SOP revision was effective** when a test was performed? This is not in C6; it would be a traceability addition for you to decide.
4. Wording of the tours and first release notes: drafted during implementation for your review.
