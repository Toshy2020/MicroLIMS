# Navigation (C8) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Favourites, a recently-opened list, remembered laboratory and tabs, crumbs on deep pages, and a shared gated-action component whose reasons come from the server.

**Architecture:** Favourites and last-used values are JSON values in the C6 `UserUiState` store. Recently-opened is its own small table because the server must write the label and re-check access. Action availability is produced by splitting each existing guard into a `Check…` method that returns a reason and the existing method that throws it, so the reason shown and the refusal are the same string by construction.

**Tech Stack:** React 19, TypeScript 6, MUI 9, Vitest; .NET 10, EF Core 10, PostgreSQL.

**Spec:** `docs/superpowers/specs/2026-11-11-navigation-design.md`

**Prerequisites:** C6 Task 1 (`UserUiState`, `useUiState`). C3 saved views and C1 `MyWorkTarget`/`routeForTarget` are used when present; each task says what to do when they are not.

## Global Constraints

- `CLAUDE.md` binds every task (separate contexts + `AsNoTracking`; `[PostgresFact]` executed, 0 skipped; `userEvent`; real components; mock only `services/`; no `any`; WCAG 2.2 AA; design tokens).
- Limits: 15 favourites; 20 recents per user; sidebar shows 5 recents.
- State keys: `nav.favourites` (JSON array), `nav.lastLab`, `nav.tab.<pageKey>`. Total favourites JSON must fit the 2 KB value limit (labels are not stored; they are resolved from the menu and the views list at render).
- No behaviour of any guarded action changes. For every covered action, the availability reason must be string-equal to the message of the exception thrown when the action is attempted in the same state.
- The availability endpoint is read-only: no `SaveChanges`, no audit row, no notification.
- A reason never reveals data the user may not read (for example another section's sample details); when the user cannot read the record the endpoint answers 404.
- A disabled gated action shows its reason as visible text, never tooltip-only.

## Review Focus

1. **Reason and refusal drift apart** after someone edits a guard later. A test per action asserts equality between the availability reason and the thrown message in each refusal state. Task 5.
2. **A record the user could read yesterday** (section membership removed, document voided) must vanish from recents and never show its label. Task 2.
3. **A stored tab that no longer exists** (feature removed, permission lost) must fall back to the default instead of rendering an empty page. Task 3.
4. **Fifteen favourites with long view ids** must still fit the store; the sixteenth is refused in the UI before any request. Task 1.
5. **Availability fails to load.** Buttons must stay usable so work is not blocked by a display aid. Task 6.

## File Structure

| File | Responsibility |
|---|---|
| `frontend/src/modules/navigation/favourites.ts`, `useFavourites.ts`, `FavouriteStar.tsx`, `ManageFavouritesDialog.tsx`, `SidebarFavourites.tsx` | Favourites |
| `backend/MicroLIMS.Domain/Entities/RecentRecord.cs`; `Application/Services/RecentRecordService.cs`, `RecordLabelResolver.cs`; `API/Controllers/RecentRecordsController.cs` | Recents (server) |
| `frontend/src/modules/navigation/useRecentRecords.ts`, `SidebarRecents.tsx`, `useTrackRecent.ts` | Recents (client) |
| `frontend/src/hooks/usePersistedTab.ts`, `usePersistedLab.ts` | Last used |
| `frontend/src/components/breadcrumbContext.tsx` | Page-supplied last crumb |
| `backend/MicroLIMS.Application/Services/ActionAvailability/ActionAvailabilityService.cs`, `ActionCheck.cs`; `API/Controllers/ActionAvailabilityController.cs` | Availability |
| `frontend/src/components/GatedAction.tsx`, `frontend/src/hooks/useActionAvailability.ts` | Gated actions |

---

### Task 1: Favourites

**Files:** Create the five frontend files with tests; Modify `frontend/src/components/PageHeader.tsx` (L13: star when the current route is a menu path), `Sidebar.tsx`, `GlobalSearchDialog.tsx` (empty-query state), C3's `ViewsMenu.tsx` when present

**Interfaces — Produces:**
```ts
export type Favourite = { t: "p"; path: string } | { t: "v"; table: string; view: number };
export function useFavourites(): { items: ResolvedFavourite[]; isFavourite(f: Favourite): boolean; toggle(f: Favourite): Promise<void>; move(index: number, dir: -1 | 1): Promise<void>; remove(index: number): Promise<void>; full: boolean };
export interface ResolvedFavourite { fav: Favourite; label: string; to: string | null; available: boolean }
```
Labels and routes are resolved at render: pages from `getGroupedMenu` (the user's visible menu), views from `tableViewService.list` (lazy, only when a view favourite exists). When C3 is absent, the `"v"` variant and the star in views are not offered.

- [ ] **Step 1: Failing tests:** `star on a menu page toggles and announces "Added to favourites"`; `no star on a page that is not a menu path`; `sidebar shows favourites under My Work in stored order`; `Ctrl+K with an empty query lists favourites first`; `sixteenth is refused with "You can have up to 15 favourites" and no request is sent` *(Review Focus 4)*; `15 view favourites with 7-digit ids serialise under 2,000 bytes`; `Move up/down by keyboard persists the new order`; `favourite page not in my menu → greyed, "No longer available", Remove works`; `store failure → falls back to localStorage and still works`; `collapsed sidebar exposes favourites by accessible name`.
- [ ] **Step 2–4:** FAIL → implement → PASS; lint, `tsc -b`, `debt:check`. **Step 5:** Commit `feat(nav): favourites`.

### Task 2: Recently opened

**Files:** Create `RecentRecord.cs`, configuration, migration `AddRecentRecords`, `RecentRecordService.cs`, `RecordLabelResolver.cs`, `RecentRecordsController.cs`, the three frontend files; Modify the detail screens of the eight record types (call `useTrackRecent(type, id)`), `Sidebar.tsx`, `GlobalSearchDialog.tsx`; Tests `backend/MicroLIMS.Tests/IntegrationTests/RecentRecordPostgresTests.cs`, frontend tests

**Interfaces — Produces:**
```csharp
// RecentRecord: UserId, RecordType (≤30), RecordId, OpenedAt     PK (UserId, RecordType, RecordId); index (UserId, OpenedAt DESC)
public sealed record RecentRecordDto(string RecordType, int RecordId, string Identifier, string Title, DateTime OpenedAtUtc);
public interface IRecordLabelResolver { Task<IReadOnlyDictionary<(string Type, int Id), (string Identifier, string Title)>> ResolveAsync(int userId, IReadOnlyCollection<(string Type, int Id)> records, CancellationToken ct = default); }   // returns only records the user may read
Task TouchAsync(int userId, string recordType, int recordId, CancellationToken ct = default);
Task<IReadOnlyList<RecentRecordDto>> ListAsync(int userId, CancellationToken ct = default);
Task ClearAsync(int userId, CancellationToken ct = default);
```
Types: `sample`, `testOrder`, `item`, `document`, `equipment`, `material`, `media`, `solution`. `POST api/me/recent` `{ recordType, recordId }` → 204 (also 204, storing nothing, when the record is unknown or unreadable, so the endpoint cannot be used to probe); `GET api/me/recent`; `DELETE api/me/recent`. `TouchAsync` upserts and trims to the newest 20 in one statement. `ListAsync` resolves labels in one query per type present (at most eight), using the same section-scope / document-authorisation checks as the records' own GET services, and drops unresolved rows. No label is stored. `RecentRecord` is proposed for `AuditCaptureExcludedTypes` (`// per-user convenience list; not an access log`); **needs the user's approval before this task starts**; without it, leave it audited. `useTrackRecent` posts once per mount, after the record has loaded successfully.

- [ ] **Step 1: Failing tests:** `Touch_AddsOnce_UpdatesTimeOnRepeat`; `TwentyFirst_DropsOldest`; `List_OnlyMine`; `Touch_UnreadableRecord_204_StoresNothing`; `List_DropsRecordINoLongerMayRead_AndReturnsNoLabelForIt` *(Review Focus 2)* (remove section membership via a second context); `List_AtMostEightLabelQueries` (command count); `Clear_RemovesAll`; `UnknownType_400`. Frontend `opening a sample posts once`; `Ctrl+K empty query shows "Recent" after favourites`; `sidebar section shows 5, collapsible, state remembered`; `row opens the record's route` (uses C1 `routeForTarget` when present, else a local map of the eight types); `"Clear" asks for confirmation`; `load failure hides the section`.
- [ ] **Step 2–4:** FAIL → implement → PASS, 0 skipped; `has-pending-model-changes` → none. **Step 5:** Commit `feat(nav): recently opened`.

### Task 3: Last laboratory and tab

**Files:** Create `usePersistedTab.ts`, `usePersistedLab.ts` with tests; Modify `frontend/src/modules/dashboard/DashboardPage.tsx` (L15 default order), `DashboardLabContext.tsx`, My Work page (C1) when present, and the tabbed pages: `EquipmentPage.tsx` (L600–602), `ItemWorkspace.tsx`, `Reports.tsx`, `MaterialsPage.tsx`, `MediaPage.tsx`, `CryovialsPage.tsx`, admin tabs (`adminTabs.ts`)

**Interfaces — Produces:**
```ts
export function usePersistedLab(available: string[]): [string, (lab: string) => void];                       // URL ?lab= wins; else stored nav.lastLab if available; else available[0]
export function usePersistedTab<T extends string>(pageKey: string, tabs: readonly T[], urlParam?: string): [T, (tab: T) => void];   // URL wins; else stored if in tabs; else tabs[0]
```
Writes are debounced 500 ms and never block rendering; the first render uses the URL or the default and switches once when the stored value arrives (no flash when the URL names a tab).

- [ ] **Step 1: Failing tests:** `URL value wins over stored`; `stored value applied when URL is silent`; `stored value not in available tabs → default` *(Review Focus 3)*; `choosing a tab stores it and updates the URL parameter when one is configured`; `store unavailable → browser fallback`; `two pages with different pageKeys do not share a tab`; one test per adopted page that its tabs still switch and deep links still open the named tab.
- [ ] **Step 2–4:** FAIL → implement → PASS. **Step 5:** Commit `feat(nav): remember last laboratory and tab`.

### Task 4: Crumbs on deep pages

**Files:** Create `breadcrumbContext.tsx` with test; Modify `AppBreadcrumbs.tsx` (L16–23), `MainLayout.tsx` (provider around L149), each deep page (`ItemWorkspace.tsx`, `DocumentDetailPage.tsx`, sample detail / tracking detail, `PreparationWizardPage.tsx`, HPLC and ICP run pages, role create/edit pages); Test `frontend/src/routes/breadcrumbCoverage.test.tsx`

**Interfaces — Produces:**
```ts
export function useBreadcrumb(label: string | null): void;      // sets the last crumb for the lifetime of the calling page
```
The trail becomes `group › parent › menu item › label`; the document title uses the same parts. While the label is loading the crumb shows a skeleton of fixed width, not the raw id. Long labels are truncated with the full text in the `title` and in the accessible name.

- [ ] **Step 1: Failing tests:** `page-supplied label is the last crumb and has aria-current="page"`; `label removed on unmount`; `document.title includes the label`; `coverage: every route in AppRoutes.tsx whose path contains ":" renders a component that calls useBreadcrumb` (render each parameterised route with a stub service response and assert a last crumb other than the menu item; the failure message lists the routes without one); `truncated crumb exposes its full text`.
- [ ] **Step 2–4:** FAIL → implement → PASS. **Step 5:** Commit `feat(nav): breadcrumbs on deep pages`.

### Task 5: Action availability — backend

**Files:** Create `ActionCheck.cs`, `ActionAvailabilityService.cs`, `ActionAvailabilityController.cs`; Modify the guard code inside `SampleReviewService.cs` (`CanSubmitForReviewAsync` L59, `CompleteReviewAsync` L97–134, return path L200–380), `SampleApprovalService.cs` (`DecideAsync` L138–190), the sample void service, `SampleAssignmentService.cs` (L41–66 with `AnalystAssignmentRules`), `TestSelfAssignmentService.cs` (C1, when present), `MediaReleaseService.cs` (`DecideAsync` L56–93), `DocumentApprovalService.cs` (`ValidateApprovalReadinessAsync` L222, decision guards ~L121 and L419); Test `backend/MicroLIMS.Tests/IntegrationTests/ActionAvailabilityPostgresTests.cs`

**Interfaces — Produces:**
```csharp
public sealed record ActionCheck(bool Allowed, string? Reason) { public static ActionCheck Ok { get; } public static ActionCheck No(string reason); public void ThrowIfRefused(); }   // throws BusinessRuleException(Reason), or UnauthorizedAccessException for permission/scope refusals, exactly as the service does today
public sealed record ActionAvailability(string Action, bool Allowed, string? Reason);
Task<IReadOnlyList<ActionAvailability>> ForSampleAsync(int sampleId, int? sectionId, int userId, CancellationToken ct = default);     // review.complete, review.return, approval.approve, approval.reject, sample.void, sample.assign
Task<IReadOnlyList<ActionAvailability>> ForTestOrderAsync(int testOrderId, int userId, CancellationToken ct = default);               // test.take, test.release (when C1 is present)
Task<IReadOnlyList<ActionAvailability>> ForMediaAsync(int mediaId, int userId, CancellationToken ct = default);                        // media.release
Task<IReadOnlyList<ActionAvailability>> ForDocumentRevisionAsync(int revisionId, int userId, CancellationToken ct = default);         // document.approve
```
HTTP: `GET api/actions/sample/{id}?sectionId=`, `…/test-order/{id}`, `…/media/{id}`, `…/document-revision/{id}`; `[Authorize]`; 404 when the user may not read the record. **Method:** in each service, move the pre-condition checks of the action (permission, section scope, Segregation of Duties, record state, prerequisites) into a `Check…Async` method returning the first failing `ActionCheck`, in the same order as today; the action method calls `(await Check…Async(…)).ThrowIfRefused()` where the inline checks were. Checks that need the user's input (password, comment, selected tests) are not part of availability and stay in the action. The permission refusal text is, exactly, the text the authorisation layer returns today for that policy; if it returns none, use "Your role does not include this action." and return it from both paths.

- [ ] **Step 1:** Characterisation: for each covered action, tests on today's code for every refusal state (no permission; other section; performed the test; wrong status; prerequisites missing; already decided) recording the exception type and message. Run → PASS.
- [ ] **Step 2: Failing tests:** for each action and state above, `Availability_Reason_Equals_ThrownMessage` (call availability, then attempt the action in a fresh context, compare strings) *(Review Focus 1)*; `AllowedState_AvailabilityAllowed_AndActionSucceeds`; `Availability_ChangesNothing` (row, audit and notification counts unchanged); `RecordINotReadable_404`; `UnknownRecord_404`; `Availability_UsesAConstantNumberOfQueries` (not one per test order of the sample).
- [ ] **Step 3–4:** Refactor the guards → the Step 1 characterisation tests still pass unchanged, and the new tests pass; 0 skipped. Run the existing review, approval, media-release and document-approval Postgres tests.
- [ ] **Step 5:** Commit `feat(nav): action availability with the server's own reasons`.

### Task 6: Gated-action component and adoption

**Files:** Create `GatedAction.tsx`, `useActionAvailability.ts`, `frontend/src/services/actionAvailabilityService.ts` with tests; Modify the review screen, approval screen, sample register row actions (void, assign), My Work rows (take / release), media awaiting-approval list, `ApprovalWorkspaceDialog.tsx`

**Interfaces — Produces:**
```ts
export function useActionAvailability(kind: "sample" | "test-order" | "media" | "document-revision", id: number | null, sectionId?: number | null): { get(action: string): { allowed: boolean; reason?: string } | null; loading: boolean; failed: boolean; refresh(): void };
export interface GatedActionProps { action: string; availability: ReturnType<typeof useActionAvailability>; children: ReactElement<ButtonProps>;   // the real button
  reasonPlacement?: "below" | "inline" }
```
Behaviour: `get` returns null while loading or when `failed`; then the child button is rendered **enabled and unchanged** and the server decides on click, as today. When not allowed: the button is rendered with `aria-disabled="true"` (kept focusable so keyboard and screen-reader users can reach the reason), click is ignored, and the reason is shown as text with an icon, connected by `aria-describedby`. Availability is refreshed after any action on the record and on a C5 `record` signal when live updates are present. Existing local hide/disable logic for these actions is removed on the adopted screens so there is one source of truth; a `RegisterTable` row action uses the reason as its disabled item's secondary text.

- [ ] **Step 1: Failing tests:** `allowed → button works`; `not allowed → aria-disabled, click does nothing, reason visible and described-by`; `loading → button enabled`; `failed → button enabled and a single notice "Reasons are unavailable right now"` *(Review Focus 5)*; `reason text is rendered as text (no HTML)`; `after the action succeeds availability is refetched`; per adopted screen: `a reviewer who performed a test sees Complete review disabled with the server's sentence`, `an analyst sees Approve disabled with the role sentence`, and the action's request payload is unchanged.
- [ ] **Step 2–4:** FAIL → implement → PASS; lint, `tsc -b`, `debt:check`. **Step 5:** Commit `feat(nav): gated actions with visible reasons`.

### Task 7: Final verification and documents

- [ ] **Step 1:** Backend Release build; full suite without and with PostgreSQL; exact counts; `has-pending-model-changes` → none.
- [ ] **Step 2:** Frontend lint, `tsc -b`, test, build, `debt:check`.
- [ ] **Step 3:** Run the app as an analyst and as a Section Head: add and reorder favourites, open records and see them in recents, switch laboratory and tabs and sign in again on another browser, follow crumbs on three deep pages, read the reason on each gated action. Keyboard-only and screen-reader pass of the sidebar and one gated action. Report what was and was not exercised.
- [ ] **Step 4:** List every reason sentence in the PR for the user's wording review. `documents/` requirements; roadmap links.
- [ ] **Step 5:** Commit `docs(nav): requirements, roadmap links`.
