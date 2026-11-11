# In-App Help and Onboarding (C6) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A help panel showing the guide chapter for the current page, shared field hints, a list of the items that use each test with a route to each item's SOP documents, an "Open SOP" action in the test dialog, a per-role first-login tour, and release notes with a visible version.

**Architecture:** The guide chapters in `documentation/guide/` are loaded lazily as raw markdown through Vite's glob import and rendered with raw HTML disabled; screenshots are copied to the build output by a small script. A route-to-chapter map is derived from `menuConfig.ts`, and a test fails when a menu path has no chapter. Per-user interface state (tour seen, last release note) lives in a small server key-value store that C8 will reuse. The SOP features reuse what already exists: `Item.SopNumber` resolved against Document Control by `ItemStmService`, and the item's Documents tab.

**Tech Stack:** React 19, TypeScript 6, MUI 9, Vite 8, `react-markdown`, Vitest; .NET 10, EF Core 10, PostgreSQL.

**Spec:** `docs/superpowers/specs/2026-11-11-in-app-help-design.md`

## Global Constraints

- `CLAUDE.md` binds every task (separate contexts + `AsNoTracking`; `[PostgresFact]` executed, 0 skipped; `userEvent`; real components; mock only `services/`; no `any`; WCAG 2.2 AA; design tokens).
- `UserUiState` is added to `AuditCaptureExcludedTypes` with the comment `// per-user interface preferences; no laboratory data (approved 2026-11-11)`.
- Help is guidance. Every help surface shows, exactly: "Guidance only. Your approved SOPs take precedence."
- Markdown is rendered with raw HTML **disabled**. No `dangerouslySetInnerHTML` anywhere in this package.
- Hint, tour and release-note text never states a limit, formula or workflow rule that is not already shown by the configured data on that screen.
- Help content is loaded only when help is opened: the main bundle must not grow by more than 15 KB gzip (checked in Task 8 from the build output).
- No new link between tests and documents is created. SOPs belong to items: `Item.SopNumber` → `DocumentMaster.CompanyDocumentCode` via `ItemStmService` (existing), and uploaded `ItemDocument` files (existing).
- Interface-state keys match `^[a-z][a-zA-Z0-9.]{1,63}$`; value ≤ 2 KB; at most 100 keys per user.
- New package: `react-markdown` (MIT), version pinned at implementation, with a qualification note.

## Review Focus

1. **A guide chapter with HTML or a script tag in it.** It must appear as text, never execute. Task 2.
2. **A menu page added later without a help chapter.** The build must fail with the page's path in the message. Task 2.
3. **A test used by 300 items.** The side list pages and searches; it does not load 300 rows or make 300 document requests. Task 5.
4. **A sample with no item** (water, EM, after cleaning). "Open SOP" is absent, not broken. Task 5.
5. **The tour on a screen where a highlighted element is missing** (role without that menu item, narrow width, bench mode). The step is skipped and the tour never traps focus on nothing. Task 6.

## File Structure

| File | Responsibility |
|---|---|
| `backend/MicroLIMS.Domain/Entities/UserUiState.cs`; `Application/Services/UserUiStateService.cs`; `API/Controllers/UserUiStateController.cs` | Per-user interface state |
| `backend/MicroLIMS.API/Controllers/SystemVersionController.cs` | Server version |
| `frontend/src/modules/help/content/guideLoader.ts`, `helpMap.ts`, `searchIndex.ts` | Chapters, route map, search |
| `frontend/src/modules/help/HelpPanel.tsx`, `HelpPage.tsx`, `HelpMenu.tsx`, `AboutDialog.tsx`, `MarkdownView.tsx` | Help UI |
| `frontend/src/components/FieldHint.tsx`; `frontend/src/modules/*/hints.ts` | Hints |
| `frontend/src/modules/help/tour/Tour.tsx`, `tourSteps.ts` | Tour |
| `frontend/src/modules/help/whatsNew/releaseNotes.ts`, `WhatsNewDialog.tsx` | Release notes |
| `frontend/src/services/uiStateService.ts`, `frontend/src/hooks/useUiState.ts` | Client for the state store |
| `frontend/scripts/copy-help-assets.mjs` | Copies screenshots into the build |
| `frontend/src/modules/laboratoryConfiguration/masterDataSimple/TestItemsPanel.tsx` | Items using a test |

---

### Task 1: Interface-state store, version, Help menu

**Files:** Create the entity, configuration, service, controllers, migration `AddUserUiState`, `uiStateService.ts`, `useUiState.ts`, `HelpMenu.tsx`, `AboutDialog.tsx`; Modify `backend/Directory.Build.props` (`<Version>` and `<InformationalVersion>` set from an MSBuild property `MicroLimsVersion`, default `0.1.0`), `frontend/vite.config.ts` (`define: { __APP_VERSION__: JSON.stringify(pkg.version) }`), `Header.tsx` (Help button between the theme toggle L238–253 and the bell L254); Tests `backend/MicroLIMS.Tests/IntegrationTests/UserUiStatePostgresTests.cs`, frontend tests

**Interfaces — Produces:**
```csharp
// UserUiState: UserId, Key (≤64), Value (≤2048), UpdatedAt    PK (UserId, Key)
Task<IReadOnlyDictionary<string, string>> GetAllAsync(int userId, CancellationToken ct = default);
Task SetAsync(int userId, string key, string value, CancellationToken ct = default);
Task RemoveAsync(int userId, string key, CancellationToken ct = default);
```
HTTP: `GET api/me/ui-state` → `{ [key]: value }`; `PUT api/me/ui-state/{key}` `{ value }`; `DELETE api/me/ui-state/{key}`; `GET api/system/version` (authenticated) → `{ version, environment }`. The store always acts on the caller; there is no user id in any route. `UserUiState` is interface preference data; it is proposed for `AuditCaptureExcludedTypes` with the comment `// per-user interface preferences (tour seen, favourites); not a GxP record`. **This needs the user's approval before the task starts**; without it, leave it audited.
```ts
export function useUiState(key: string): { value: string | null; loaded: boolean; set(v: string): Promise<void>; remove(): Promise<void> };   // falls back to localStorage when the store is unreachable
```

- [ ] **Step 1: Failing tests:** `Set_Get_Remove_RoundTrip`; `UserCannotSeeAnotherUsersKeys`; `InvalidKeyPattern_Rejected`; `ValueOver2Kb_Rejected`; `Key101_Rejected`; `Unauthenticated_401`; `Version_ReturnsAssemblyInformationalVersion`. Frontend `Help button has an accessible name and opens a menu with: Help for this page, Full guide, Keyboard shortcuts, What's new, Replay tour, About`; `About shows application version from __APP_VERSION__ and server version from the endpoint`; `useUiState falls back to localStorage when the service rejects`.
- [ ] **Step 2–4:** FAIL → implement → PASS, 0 skipped; `has-pending-model-changes` → none. **Step 5:** Commit `feat(help): interface-state store, version, help menu`.

### Task 2: Guide content, route map, safe rendering

**Files:** Create `guideLoader.ts`, `helpMap.ts`, `searchIndex.ts`, `MarkdownView.tsx`, `copy-help-assets.mjs` with tests; Modify `frontend/package.json` (`build`: `tsc -b && vite build && node scripts/copy-help-assets.mjs`), `frontend/vite.config.ts` (`server.fs.allow` for `../documentation`)

**Interfaces — Produces:**
```ts
export interface GuideChapter { id: string; number: number; title: string; load(): Promise<string> }   // id = file name without .md, e.g. "12-sample-receiving"
export const CHAPTERS: GuideChapter[];              // from import.meta.glob("../../../../../documentation/guide/[0-9][0-9]-*.md", { query: "?raw", import: "default" })
export function chapterForPath(pathname: string, search: string): { chapterId: string; heading?: string };
export const HELP_MAP: Record<string, { chapterId: string; heading?: string }>;   // key = menu path exactly as in menuConfig.ts
export function searchGuide(query: string): Promise<{ chapterId: string; title: string; heading: string; excerpt: string }[]>;
// MarkdownView props: { markdown: string; onNavigate(chapterId: string, heading?: string): void }
```
Chapter titles come from the first `## N. Title` line (chapters have no H1). `REVIEW_GUIDE_BRIEF.md`, `WRITING_BRIEF.md` and `_ss/` are excluded by the glob. Image paths `../screenshots/x.png` are rewritten to `/help/screenshots/x.png`; the script copies `documentation/screenshots/*` (74 files, about 11 MB) to `dist/help/screenshots/`. In `MarkdownView`: headings are shifted so the chapter title renders as `h2` inside the panel; links to other chapters call `onNavigate`; external links get `rel="noopener noreferrer"` and open in a new tab; images are lazy and keep their alt text. `chapterForPath` uses the same longest-match rule as `findNavTrail` (`routes/navigation.ts` L34) and falls back to the parent section, then to `01-introduction`. Starting map (extend to every menu path): `/dashboard`→08, `/my-work`→08, `/receiving`→12, `/receiving/tracking`→11, `/microbiology/workspace` and `/physicochemical/workspace`→14, `/oos-tracking`→20, `/reports`→34, `/monitoring-plan`→23, `/laboratory-configuration/media`→22, `/laboratory-configuration/cryovials`→26, `/preparation`→28, `/inventory/materials`→29, `/inventory/equipment` and `/laboratory-configuration/equipment`→30, `/laboratory-configuration/items` and `…/test-master`→31, other `/laboratory-configuration/*`→32, `/laboratory-configuration/water`→24, `…/after-cleaning`→25, `…/environmental-monitoring`→23, `/hplc-workspace`, `/gc-workspace`, `/icp-workspace`, `/working-standards`→27, `/document-control/*`→38, `/admin/users`, `/admin/roles`, `/admin/organization`, `/admin/access-review`→39, `/admin/audit`→35, `/admin/error-log`→40, `/admin/settings`, `/admin/jobs`→41, `/profile`, `/change-password`→05, `/notifications`, `/messages`, `/discussions`→09.

- [ ] **Step 1: Failing tests:** `every menuConfig path has a HELP_MAP entry` (iterate `getGroupedMenu` with all permissions; failure message lists the missing paths) *(Review Focus 2)*; `every HELP_MAP chapterId exists in CHAPTERS`; `CHAPTERS has 44 entries numbered 1–44 with titles`; `chapterForPath: exact, query-string variant (?lab=FP), deep child, unknown → introduction`; `MarkdownView renders "<script>alert(1)</script>" and "<img onerror=…>" as text, not elements` *(Review Focus 1)*; `image src is rewritten; alt kept; loading="lazy"`; `chapter link calls onNavigate; external link has rel and target`; `searchGuide finds a word in a chapter body and returns its nearest heading`; `copy-help-assets copies every referenced screenshot and fails when a chapter references a missing file`.
- [ ] **Step 2–4:** FAIL → implement → PASS; `npm run build` succeeds and `dist/help/screenshots` exists. **Step 5:** Commit `feat(help): guide content pipeline and route map`.

### Task 3: Help panel and guide page

**Files:** Create `HelpPanel.tsx`, `HelpPage.tsx` with tests; Modify `MainLayout.tsx` (panel beside the main content L142–148; F1 handler), `AppRoutes.tsx` (`/help`, `/help/:chapterId`), `HelpMenu.tsx`

Panel: a non-modal right-side drawer, 420 px (full width below `md`), labelled region "Help", focus moves to its heading on open and returns to the Help button on close; Escape closes; the page behind stays interactive. Contents: the guidance line and version; search field with a result list announced via a polite live region; the chapter; "Open the full guide". F1 opens it (default browser help prevented) unless focus is in a text field with a modifier.

- [ ] **Step 1: Failing tests:** `opens the chapter mapped to the current route and scrolls to the mapped heading`; `route change while open switches chapter`; `F1 opens; Escape closes and returns focus to the Help button`; `page controls remain clickable while open`; `guidance line and version shown`; `search lists results and opens one`; `load failure shows Retry and the full-guide link`; `HelpPage lists 44 chapters and renders one by URL`; `loading skeleton; reduced-motion: no slide animation`.
- [ ] **Step 2–4:** FAIL → implement → PASS; lint, `tsc -b`, `debt:check`. **Step 5:** Commit `feat(help): help panel and guide page`.

### Task 4: Field hints

**Files:** Create `FieldHint.tsx` with tests and one `hints.ts` in each of: `laboratoryConfiguration/items`, `laboratoryConfiguration/masterDataSimple`, `receiving`, `testingWorkspace`, `laboratoryConfiguration/media`, `laboratoryConfiguration/escalation` (if C5 is merged); Modify the six forms to place hints

**Interfaces — Produces:**
```ts
export interface Hint { text: string; guide?: { chapterId: string; heading?: string } }
// FieldHint props: { id: string; label: string; hint: Hint }   → IconButton aria-label `About ${label}`, popover with role="dialog", aria-describedby wiring for the field
```
Hint texts are drafted from the matching guide chapter's wording and listed in the PR for the user's review. A hint describes meaning and effect only; it must not contain a number that is a limit, a factor or a time window.

- [ ] **Step 1: Failing tests:** `opens on click, Enter and Space; not on hover`; `Escape closes and returns focus`; `"Read more" opens the help panel at the linked heading`; `target is ≥ 24 px (≥ 48 px in bench mode)`; `every key in each hints.ts is used by exactly one FieldHint` (a script-style test scanning the module); `no hint text matches /\d+\s*(cfu|%|°C|hours?|days?)/i` (guards the no-limits rule).
- [ ] **Step 2–4:** FAIL → implement → PASS. **Step 5:** Commit `feat(help): shared field hints on the first forms`.

### Task 5: Items using a test, and Open SOP

**Files:** Create `TestItemsPanel.tsx` with test; Modify `TestDefinitionMasterDataController.cs` (new action), `TestDefinitionMasterDataService.cs` (new query; reuse the private items-by-test-code query near L948), `TestMasterPage.tsx` (details dialog L809–819: two-column layout, workflow steps left, panel right; stacked below `md`), `TestWorkflowDialog.tsx` (header action), `ItemWorkspace.tsx` (accept `?tab=documents`); Test `backend/MicroLIMS.Tests/IntegrationTests/TestItemsPostgresTests.cs`

**Interfaces — Produces:**
```csharp
public sealed record TestItemRow(int ItemId, string ItemCode, string ItemName, string? SopNumber, string SopStatus /* Linked | NotFound | NoNumber */,
    int? DocumentMasterId, string? EffectiveRevisionNumber, int UploadedDocumentCount, bool AutoAssignedOnReceipt, bool HasSpecification);
Task<PagedResult<TestItemRow>> GetItemsForTestAsync(int testDefinitionId, string? search, int page, int pageSize, int userId, CancellationToken ct = default);
```
`GET api/masterdata/test-definitions/{id}/items?search=&page=&pageSize=` (same policy as reading the Test Master; page size clamped 1–100). An item is listed when it has a `Specification` or a `SampleTest` row for the test's code and is not listed in `NotApplicableTest`; section scope applies. `SopStatus` is computed in the same query by joining `Item.SopNumber` to active `DocumentMaster.CompanyDocumentCode` (the rule `ItemStmService` L22–30 uses) — one query for the page, not one per item. Panel: title "Items using this test ({total})", search, paged list; each row shows item code and name, SOP number with its status as text, and two links: "Item documents" (`/laboratory-configuration/items/{id}?tab=documents`) and, when linked, "Open SOP" (`/document-control/documents/{DocumentMasterId}`). Workflow dialog: when the `itemId` prop is set, an "Open SOP" button calls the existing `GET api/items/{itemId}/stm` and opens the document route in a new tab; states: no SOP number ("No SOP number on this item"), number not found in Document Control ("SOP {number} is not in Document Control"), no effective revision, no read access; when `itemId` is absent the button is not rendered.

- [ ] **Step 1: Failing tests:** backend `ListsItemsWithSpecificationOrAutoAssign_ExcludesNotApplicable`; `SopStatus_Linked_NotFound_NoNumber`; `Paged_ClampedTo100_SearchByCodeOrName`; `ThreeHundredItems_OneQueryPerPage` (command-count interceptor) *(Review Focus 3)*; `ItemsOfSectionICannotRead_Excluded`. Frontend `panel shows total, pages and searches`; `SOP status is text, not colour alone`; `"Item documents" link opens the item on its Documents tab`; `"Open SOP" in the workflow dialog: each of the five states`; `no itemId → no button` *(Review Focus 4)*; `the workflow dialog's result payload is unchanged` (existing test).
- [ ] **Step 2–4:** FAIL → implement → PASS, 0 skipped. **Step 5:** Commit `feat(help): items using a test and Open SOP`.

### Task 6: First-login tour

**Files:** Create `Tour.tsx`, `tourSteps.ts` with tests; Modify `MainLayout.tsx` (start condition), `Sidebar.tsx`, `Header.tsx` and the My Work page (add `data-tour="…"` attributes), `HelpMenu.tsx` (Replay)

**Interfaces — Produces:**
```ts
export interface TourStep { target: string /* data-tour value */; title: string; body: string }
export const TOUR_STEPS: Record<RoleType, TourStep[]>;      // 5–6 steps each; key in the state store: `tour.${role}.v1` = "done" | "skipped"
// Tour props: { steps: TourStep[]; onFinish(result: "done" | "skipped"): void }
```
Built with MUI `Popper` and a focus-trapped dialog per step (`role="dialog"`, `aria-modal="false"`, labelled by the step title, "Step 2 of 6"). The highlighted element gets an outline using a theme token, not a dimming overlay that hides content. Starts once after login when `mustChangePassword` is false, no forced enrolment is pending, and the state key is absent. A step whose target is not in the DOM or not visible is skipped; if no step is available the tour ends silently as "skipped". Respects `prefers-reduced-motion`.

- [ ] **Step 1: Failing tests:** `starts for a user without the key; not for one with it; not while password change is pending`; `Next/Back/Skip work by keyboard; focus moves into each step and returns to the page at the end`; `finishing stores "done", skipping stores "skipped"`; `Replay from Help starts it again without clearing the stored value until it ends`; `missing target is skipped; all missing → ends, nothing rendered` *(Review Focus 5)*; `each role has 5 or 6 steps and every target exists in the app for that role` (render the layout per role and query the attributes); `Escape skips`.
- [ ] **Step 2–4:** FAIL → implement → PASS. **Step 5:** Commit `feat(help): first-login tour per role`.

### Task 7: What's new

**Files:** Create `releaseNotes.ts`, `WhatsNewDialog.tsx` with tests; Modify `MainLayout.tsx`, `HelpMenu.tsx`

**Interfaces — Produces:**
```ts
export interface ReleaseNote { version: string; date: string; items: { roles: RoleType[] | "all"; text: string; guide?: { chapterId: string; heading?: string } }[] }
export const RELEASE_NOTES: ReleaseNote[];     // newest first; state key `whatsNew.lastSeen` = version string
```
Shown once after login when `__APP_VERSION__` is greater than the stored value and the note has at least one item for the user's role; never shown together with the tour (the tour wins and the note waits for the next login); closing stores the version. A test enforces that `RELEASE_NOTES[0].version` equals `package.json` version, so a release cannot ship without its note.

- [ ] **Step 1: Failing tests:** `shown once for a newer version; not again after closing`; `only items for my role`; `no items for my role → not shown, version still stored`; `not shown while the tour runs`; `Help → What's new lists all versions`; `newest note version equals the package version`; `dialog uses span typography inside the title and returns focus on close`.
- [ ] **Step 2–4:** FAIL → implement → PASS. **Step 5:** Commit `feat(help): release notes shown once per version`.

### Task 8: Final verification and documents

- [ ] **Step 1:** Backend Release build; full suite without and with PostgreSQL; exact counts.
- [ ] **Step 2:** Frontend lint, `tsc -b`, test, build, `debt:check`. Compare the main chunk's gzip size before and after from the build output; report both numbers (limit +15 KB).
- [ ] **Step 3:** Run the app as each role: tour, help on five pages, search, a hint, the items panel on a test, Open SOP from a product test and absence on a water test, What's new. Screen-reader and keyboard pass on the panel and tour. Report what was and was not exercised.
- [ ] **Step 4:** List in the PR every guide chapter the map found thin or out of date (not fixed here). `documents/` requirements and an SOP note: in-app help is guidance; approved SOPs govern. Roadmap links.
- [ ] **Step 5:** Commit `docs(help): requirements, roadmap links`.

## Cross-plan corrections (whole-programme review, 2026-11-11)

These corrections come from reviewing all plans together against the code. Where they differ from the text above, they win. Shared names are defined once in `docs/superpowers/plans/2026-11-11-build-order.md` (section "Programme conventions").

1. The `/my-work` chapter mapping and tour steps apply when C1 is built; otherwise the tour starts at the dashboard.
2. Audit capture for `UserUiState` is approved (user, 2026-11-11); Task 1 no longer waits for approval.
3. Task 5's 'no read access' state is a separate `SopLinkState.NoAccess` value alongside `SopStatus`.

## Review corrections (independent review, 2026-11-11)

From the independent review of the build order. These win over the text above and over the earlier corrections section where they differ.

1. Task 6 (tours) is built after C1, since its steps target `/my-work`.
