# Bench and Tablet Mode (C2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A per-device bench mode with large touch controls and a shared number field, tablet layouts for the four bench screen groups, locally kept unsaved drafts that are never treated as records, and an installable app shell.

**Architecture:** Bench mode is a theme variant chosen by a device-level preference, so screens get sizes from the theme instead of local logic. A small draft store (IndexedDB) plus one hook, `useBenchDraft`, gives every bench form the same save-locally / restore / submit behaviour. The backend changes in one place only: a request header carrying the device's draft start time is written into the audit rows of that request as source context.

**Tech Stack:** React 19, TypeScript 6, MUI 9, Vite 8, Vitest (jsdom), `idb` + `fake-indexeddb` (dev), `vite-plugin-pwa`; .NET 10 for the audit header.

**Spec:** `docs/superpowers/specs/2026-11-10-bench-tablet-mode-design.md`

## Global Constraints

- `CLAUDE.md` binds every task (`userEvent`; real components; mock only `services/`; no `any`; no weakened lint rules; WCAG 2.2 AA; design tokens, no raw hex).
- **No bench screen's request payload, endpoint or validation changes.** Each migrated screen keeps or gains a test asserting the exact payload sent to its service function.
- **A draft never contains a password, signature or token.** Fields named `password`, `comment` on a signature step, or any `SignatureDialog` input are excluded by an explicit allow-list per form, not by a deny-list.
- A draft is never submitted without a user pressing Save on the open form.
- Client checks are format-only (whole number, not negative, length). Laboratory rules stay on the backend.
- Bench mode sizes: touch targets ≥ 48 × 48 px, ≥ 8 px gaps, input text 18 px.
- Preference keys: `microlims_bench_mode` (`"on"`/`"off"`), `microlims_bench_mode_offered` in `localStorage`; per device, not cleared on logout.
- Draft store: IndexedDB database `microlims-drafts`, store `drafts`; retention `bench.draftRetentionHours` default 24 (1–72); late-save threshold 5 minutes.
- Connection check: `GET {apiOrigin}/health` (anonymous, outside `/api`) every 15 s while a bench form is open or drafts exist.
- The service worker caches only build assets; nothing whose URL is on the API origin or contains `/api/`.
- The frontend is served by Vercel and the API by a different origin; the service worker and manifest are frontend-only.
- New packages: `idb` (ISC), `fake-indexeddb` (Apache-2.0, dev), `vite-plugin-pwa` (MIT, dev); exact versions pinned at implementation, each with a qualification note in the same document B1 uses.

## Review Focus

1. **E-signature fields in a draft.** `QualitativePanel` sends `password` with its result. A test must prove the stored draft for that form has no password and that restoring a draft leaves the password field empty. Task 5.
2. **Two analysts, one tablet.** User B must not be able to read, open or submit user A's draft, including after A's session expired without sign-out. Task 4.
3. **The record changed while the draft waited.** Saving sends the version the form was opened with; the server's 409 keeps the draft and never retries with a fresh version automatically. Task 4.
4. **Tablet clock wrong by hours.** The device time is stored as stated by the device and labelled as such; the server never uses it to compute anything, and a time in the future or older than the retention is still stored verbatim, not "corrected". Task 6.
5. **App updated while a form is open.** The new service worker must wait; no reload, no lost input. Task 10.

## File Structure

| File | Responsibility |
|---|---|
| `frontend/src/theme/BenchModeContext.tsx` | Device preference, `useBenchMode()` |
| `frontend/src/theme/benchTheme.ts` | Component default sizes and spacing for bench mode |
| `frontend/src/components/bench/NumericEntryField.tsx`, `BenchKeypad.tsx`, `BenchActionBar.tsx`, `BenchDialog.tsx` | Shared bench primitives |
| `frontend/src/services/drafts/draftStore.ts` | IndexedDB access |
| `frontend/src/hooks/useBenchDraft.ts`, `useConnectionStatus.ts` | Form-level draft behaviour; connection state |
| `frontend/src/components/bench/ConnectionBanner.tsx`, `DraftsMenu.tsx`, `DraftRestoreNotice.tsx` | Draft and connection UI |
| `backend/MicroLIMS.API/Middleware/DraftContextMiddleware.cs` | Reads the draft header |
| `frontend/public/manifest.webmanifest`, icons; `frontend/src/pwa/registerServiceWorker.ts`, `UpdatePrompt.tsx` | Installable shell |

---

### Task 1: Bench mode preference and theme variant

**Files:** Create `BenchModeContext.tsx`, `benchTheme.ts`; Modify `frontend/src/theme/index.ts` (`getTheme`), `ThemeModeContext.tsx` (L43–63), `frontend/src/main.tsx` (L22), `Header.tsx` (next to the mode switch, L238–243), `MainLayout.tsx` (sidebar closed in bench mode); Tests `BenchModeContext.test.tsx`, `benchTheme.test.ts`, `Header.test.tsx`

**Interfaces — Produces:**
```ts
export function BenchModeProvider(props: { children: ReactNode }): JSX.Element;
export function useBenchMode(): { benchMode: boolean; setBenchMode(on: boolean): void; shouldOffer: boolean; dismissOffer(): void };
export function getTheme(mode: "light" | "dark", bench?: boolean): Theme;       // existing function, new optional parameter
export const benchThemeOptions: ThemeOptions;   // defaultProps size "medium"/"large", minHeight/minWidth 48, input fontSize 18, table cell padding
// theme.custom.bench: boolean  (added in theme.d.ts) — the only flag components may read
```

- [ ] **Step 1: Failing tests:** `defaults to off; persists "on" per device; survives logout (auth keys cleared, bench key kept)`; `shouldOffer is true once on a coarse pointer and false after dismiss`; `storage unavailable → off, no crash`; `bench theme: Button, IconButton, TextField, Checkbox, Radio, Switch, Tab, MenuItem, TableCell each resolve to ≥ 48 px min target` (table test over component keys); `non-bench theme is byte-for-byte the previous theme` (snapshot of `getTheme("light")` against the pre-change object); `Header shows a labelled "Bench mode" switch; toggling changes theme.custom.bench`; `sidebar starts closed in bench mode`.
- [ ] **Step 2–4:** `npx vitest run src/theme src/components/Header.test.tsx` FAIL → implement → PASS.
- [ ] **Step 5:** lint, `tsc -b`, `debt:check`. Commit `feat(bench): device-level bench mode and theme variant`.

### Task 2: Idle activity, Switch user, wake lock

**Files:** Modify `frontend/src/hooks/useIdleTimeout.ts` (L8), `Header.tsx` (L108–112, bench-only button); Create `frontend/src/hooks/useWakeLock.ts`, `useIdleTimeout.test.ts`

**Interfaces — Produces:** `ACTIVITY_EVENTS = ["mousemove","keydown","click","scroll","pointerdown","touchstart"]`; `useWakeLock(active: boolean): void` (requests `navigator.wakeLock` when present, re-requests on `visibilitychange`, releases on inactive/unmount, silent when unsupported).

- [ ] **Step 1: Failing tests (fake timers):** `touchstart and pointerdown reset the idle timer`; `no activity for 20 min shows the warning, then logs out after 60 s`; `Switch user is shown only in bench mode, has an accessible name, signs out and navigates to /login`; `Switch user with drafts asks "You have 2 unsaved entries…" with Cancel focused` (drafts count injected; real wiring in Task 4); `useWakeLock requests and releases; unsupported browser does nothing`.
- [ ] **Step 2–4:** FAIL → implement → PASS.
- [ ] **Step 5:** Commit `feat(bench): touch activity, switch user, wake lock`.

### Task 3: Number field, keypad, action bar, bench dialog

**Files:** Create the four files under `frontend/src/components/bench/` with tests; Modify `FloatingDialog.tsx` (L35: `fullScreen` also when `theme.custom.bench` and the new prop `benchFullScreen` is true)

**Interfaces — Produces:**
```ts
export interface NumericEntryFieldProps {
  label: string; value: string; onChange(value: string): void;
  kind: "count" | "decimal";            // count = whole, non-negative
  maxLength?: number;                   // default 7
  error?: string; disabled?: boolean; required?: boolean; unit?: string;
  onNext?(): void; inputRef?: Ref<HTMLInputElement>; "aria-label"?: string;
}
export function parseCount(text: string): number | null;      // null unless /^\d+$/
export function parseDecimal(text: string): number | null;
export interface BenchActionBarProps { onCancel(): void; onSave(): void; onSaveAndNext?(): void; saving: boolean; saveLabel?: string; saveDisabledReason?: string; keypad?: ReactNode }
export interface BenchKeypadProps { onDigit(d: string): void; onBackspace(): void; onNext(): void; allowDecimal: boolean }
// BenchDialog = FloatingDialog + BenchActionBar + useWakeLock(open)
```
The keypad writes into whichever `NumericEntryField` has focus through a small focus registry; it is rendered only in bench mode.

- [ ] **Step 1: Failing tests:** `count rejects "1.5", "-1", "1e3", " 12", letters; accepts "0" and "250"` (shown as helper text with `role="alert"`); `decimal accepts "0.5" and rejects "1.2.3"`; `value is a string and is passed through unchanged (no rounding, no leading-zero trimming)`; `inputMode is numeric / decimal`; `focus selects all`; `keypad digits go to the focused field; Backspace removes one; Next calls onNext and moves focus in DOM order`; `keypad absent outside bench mode`; `hardware typing works with the keypad present`; `action bar: Save disabled with visible reason; saving shows a spinner and blocks double submit`; `BenchDialog is full-screen in bench mode and unchanged otherwise`; `Escape asks before closing when the form is dirty`.
- [ ] **Step 2–4:** FAIL → implement → PASS.
- [ ] **Step 5:** Commit `feat(bench): numeric entry, keypad, action bar`.

### Task 4: Draft store, connection status, draft hook

**Files:** Create `draftStore.ts`, `useBenchDraft.ts`, `useConnectionStatus.ts`, `ConnectionBanner.tsx`, `DraftsMenu.tsx`, `DraftRestoreNotice.tsx`; Modify `frontend/package.json`, `frontend/src/setupTests.ts` (`import "fake-indexeddb/auto"`), `Header.tsx` (drafts counter), `MainLayout.tsx` (banner), `apiClient.ts` (export `isNetworkError(e: unknown): boolean` built on the existing `!error.response` branch, L136); backend `SystemSettingDefinitions.cs` (`bench.draftRetentionHours`) exposed through a new authenticated endpoint `GET api/client-settings` (returns `{ benchDraftRetentionHours, features: string[] }`; no such endpoint exists today, this task creates it); Tests beside each file

**Interfaces — Produces:**
```ts
export interface Draft<T> { id: string; userId: number; formKey: string; recordKey: string; recordVersion: number | null;
  title: string; route: string; values: T; startedAtDevice: string; updatedAtDevice: string; state: "open" | "rejected"; rejectReason?: string }
export const draftStore = {
  put<T>(d: Draft<T>): Promise<void>, get<T>(userId: number, formKey: string, recordKey: string): Promise<Draft<T> | undefined>,
  listForUser(userId: number): Promise<Draft<unknown>[]>, countOtherUsers(userId: number): Promise<number>,
  remove(id: string): Promise<void>, purgeOlderThan(hours: number, nowDevice: Date): Promise<number>, available(): Promise<boolean>,
};
export function useBenchDraft<T>(opts: { formKey: string; recordKey: string; recordVersion: number | null; title: string;
  values: T; pick(values: T): Partial<T>;      // allow-list of fields that may be stored
  enabled: boolean }): {
  restored: Partial<T> | null; restoredStartedAt: string | null; acceptRestore(): void; discard(): Promise<void>;
  onSubmitted(): Promise<void>; onRejected(reason: string): Promise<void>; submitHeaders(): Record<string, string>;
  storageAvailable: boolean };
export function useConnectionStatus(active: boolean): { online: boolean; lastCheckedAt: Date | null };
```
`id` = `${userId}:${formKey}:${recordKey}`. `submitHeaders()` returns `{ "X-Draft-Started-At": startedAtDevice }` only when the draft is older than 5 minutes, else `{}`. `listForUser` and `get` filter by the signed-in user id from `AuthContext`; `countOtherUsers` returns a number only. Writes are debounced 400 ms and flushed on `visibilitychange`/`pagehide`. Offline when a health check or any request fails with `isNetworkError`; online again after one successful health check.

- [ ] **Step 1: Failing tests:**
  - Store: `put/get round trip`; `listForUser returns only that user's drafts` *(Review Focus 2)*; `countOtherUsers returns a count and no values`; `purge removes drafts older than the retention and reports how many`; `available() false when IndexedDB throws`.
  - Hook: `saves picked fields only after a change`; `fields outside pick() are never stored`; `restored draft is offered, not applied, until acceptRestore`; `draft for another record or another user is not offered`; `onSubmitted removes the draft`; `onRejected keeps it with state "rejected" and the reason`; `submitHeaders empty under 5 min, present after` (fake clock); `recordVersion stored is the one the form opened with, even if the prop changes later` *(Review Focus 3)*; `storage unavailable → hook is inert and reports it`.
  - Connection: `network error flips to offline; successful health check flips back; HTTP 500 does not count as offline`; `banner text is exactly "No connection. Your entries are not saved to MicroLIMS. They are kept on this tablet until you can save." with role="alert"`; `on reconnect announces "Connection restored. You have 2 unsaved entries." and sends nothing`.
  - Menu: `lists my drafts with title and age; opens the draft's route; Discard asks for confirmation; shows "Another user has unsaved entries on this tablet" when countOtherUsers > 0`; `expired drafts are purged at start and the user is told how many`.
- [ ] **Step 2–4:** FAIL → implement → PASS.
- [ ] **Step 5:** lint, `tsc -b`, `debt:check`. Commit `feat(bench): local drafts and connection status`.

### Task 5: Count reading, incubation and qualitative screens

**Files:** Modify `frontend/src/modules/testingWorkspace/TestWorkflowDialog.tsx` (count branch L672–700, `submitResult` L259–281), `QualitativePanel.tsx` (L440 and submit L107–121), `IncubationWaitingView.tsx`, `IncubationCountdown.tsx`; Tests `TestWorkflowDialog.countTest.test.tsx`, `QualitativePanel.test.tsx` (extend; bring any touched `fireEvent`/`any` into compliance)

**Interfaces — Consumes:** Tasks 1, 3, 4. Draft keys: `formKey` `"count-reading"` / `"qualitative"`, `recordKey` `${testOrderId}:${stepName}`.

- [ ] **Step 1: Failing tests:**
  - `count: payload to TestWorkflowService.recordCountResult is exactly { stepName, rawPlateReadings, dilutionFactor, dilutionFactorOverrideNote? } as before` (pin with the pre-change fixture)
  - `count: plates render as NumericEntryField rows in bench mode; Next walks plates in order; "Save and next" is offered when another reading is waiting`
  - `count: typed values survive closing and reopening the dialog (draft offered and accepted)`
  - `count: save with a network error keeps the dialog values, shows "Keep draft", no success toast`
  - `count: 409 keeps the draft as rejected with the server message`
  - `qualitative: stored draft has no password and no signature comment; after restore the password field is empty` *(Review Focus 1)*
  - `qualitative: payload to recordQualitativeResult unchanged`
  - `countdown text is ≥ 24 px in bench mode and has tabular numerals`
  - `late save sends X-Draft-Started-At; a prompt save does not`
- [ ] **Step 2–4:** FAIL → implement → PASS.
- [ ] **Step 5:** Commit `feat(bench): count reading and incubation on tablets`.

### Task 6: Backend — draft start time in the audit trail

**Files:** Create `DraftContextMiddleware.cs`; Modify `IMicroLimsDbContext.cs` + `MicroLimsDbContext.cs` (`string? CurrentSourceContext`; `BuildAuditLog` L643–670 copies it to `AuditLog.SourceContext` when the row has none), `ApplicationBuilderExtensions.cs` (L31, after authentication, with the audit pipeline), CORS policy (allow the request header `X-Draft-Started-At`); Tests `backend/MicroLIMS.Tests/IntegrationTests/DraftContextPostgresTests.cs`, `backend/MicroLIMS.Tests/UnitTests/DraftContextMiddlewareTests.cs`

**Interfaces — Produces:** header `X-Draft-Started-At` (ISO 8601 with offset). Stored text, exact: `Submitted from a device draft started at {value as received, normalised to UTC, yyyy-MM-dd HH:mm 'UTC'} (device clock, not verified)`. Invalid or missing header → no source context, request proceeds. No `SaveChanges`, no entity change, no migration (`SourceContext` already exists).

- [ ] **Step 1: Failing tests:**
  - `HeaderPresent_AuditRowsOfThatRequest_CarrySourceContext_TimestampIsServerClock` (`[PostgresFact]`, FakeTimeProvider at a time different from the header) *(Review Focus 4)*
  - `HeaderInFuture_StoredVerbatim_TimestampUnaffected`
  - `HeaderMalformed_Ignored_RequestSucceeds`; `HeaderLongerThan64Chars_Ignored`
  - `ExplicitSourceContextFromAuditEventService_IsNotOverwritten`
  - `NoHeader_SourceContextNull` (existing behaviour unchanged)
  - `PreflightAllowsTheHeader` (endpoint test)
- [ ] **Step 2–4:** FAIL → implement → PASS, 0 skipped; `AppendOnlyAuditHardeningPostgresTests` still pass.
- [ ] **Step 5:** Commit `feat(bench): record device draft time as audit source context`.

### Task 7: EM and water location grids

**Files:** Modify `LocationResultGridDialog.tsx` (L170–180), `WaterLocationResultGridDialog.tsx` (L150–165), `PathogenLocationResultGridDialog.tsx`; Create a test file for each (none exist)

Draft keys: `"em-grid"`, `"water-grid"`, `"pathogen-grid"`; `recordKey` = the batch/test identifier the dialog already receives.

- [ ] **Step 1: Failing tests (per dialog):** `payload to recordBatchResults / recordWaterBatchReadings / recordBatchPathogenResults is unchanged` (fixture from the current code); `one location per row with a labelled field; no fixed 90 px width (field fills its cell)`; `Next advances to the next location and scrolls it into view`; `progress reads "12 of 40 entered" and is announced politely`; `location name stays visible while scrolling (sticky)`; `add/remove reading buttons have record-specific aria-labels and ≥ 48 px targets in bench mode`; `draft restores all entered locations`; `pathogen grid choices are large toggle buttons with text, not colour alone`.
- [ ] **Step 2–4:** FAIL → implement → PASS.
- [ ] **Step 5:** Commit `feat(bench): EM, water and pathogen location grids on tablets`.

### Task 8: Pathogen observation screens

**Files:** Modify `pathogenSession/PrimaryObservationMatrixPanel.tsx` (L129 `parseFloat`, L759–770), `pathogenSteps/BrothStepPanel.tsx`, `SelectivePlatingPanel.tsx`, `ConfirmatoryPlatingPanel.tsx`, `BiochemicalTestPanel.tsx`; Tests: extend `BrothStepPanel.test.tsx`, `ConfirmatoryPlatingPanel.test.tsx`; create tests for the other three

Before changing L129: check the backend validator for `MatrixCellInput.numericValue`. If it accepts only whole numbers for counts, use `parseCount`; if a result type legitimately takes decimals, keep decimals for that type via `kind="decimal"` and say so in the commit. Do not decide the rule in the frontend.

- [ ] **Step 1: Failing tests:** payload pins for `savePrimaryObservations`, `saveResultMatrix`, `submitBroth`, `startSelectivePlatingIncubation`, `submitSelectivePlatingObservation`, `submitConfirmatorySetup`, `submitConfirmatoryObservations`, `recordAnalystDecision`, `submitBiochemical`; `observation choices render as one column of large labelled options in bench mode`; `matrix shows one plate per row below 1100 px and the existing table above`; `count cell uses NumericEntryField with the kind the result type requires`; `drafts restore observations but never an analyst decision` (decisions are excluded from `pick`); `gated action shows its reason as text`.
- [ ] **Step 2–4:** FAIL → implement → PASS.
- [ ] **Step 5:** Commit `feat(bench): pathogen observation screens on tablets`.

### Task 9: Media and solution preparation

**Files:** Modify `frontend/src/modules/laboratoryConfiguration/media/dialogs/MediaPreparationDialog.tsx` (payload L190–204), `media/services/MediaPreparationService.ts` (L5: replace `payload: any` with a named `PrepareMediaRequest` matching the object literal), `frontend/src/modules/solutionPreparation/PreparationWizardPage.tsx` (L101), `wizard/LotsStep.tsx`; Create tests for each

- [ ] **Step 1: Failing tests:** `media payload equals the current twelve fields (materialId … additives)`; `solution payload equals SavePreparationRequest`; `numeric fields (weight, volume, pH, temperature, cycle time) use NumericEntryField kind="decimal"; cycle number kind="count"`; `sections stack one per screen height in bench mode with a step indicator`; `lot and equipment pickers accept a scan when B1's ScanField is present, and work unchanged when it is not`; `media draft restores; solution wizard draft restores to the same step`; `server validation errors appear on the matching field`.
- [ ] **Step 2–4:** FAIL → implement → PASS; `debt:update` (the removed `any` lowers the baseline).
- [ ] **Step 5:** Commit `feat(bench): media and solution preparation on tablets`.

### Task 10: Installable shell

**Files:** Modify `frontend/vite.config.ts` (`VitePWA` with `registerType: "prompt"`, `injectRegister: null`, `workbox.navigateFallback: "/index.html"`, `runtimeCaching: []`, `globPatterns` for build assets only), `frontend/index.html` (manifest link, theme-color), `frontend/vercel.json` (`Cache-Control: no-cache` for `/sw.js` and `/manifest.webmanifest`); Create `manifest.webmanifest`, icons in `frontend/public/icons/` (192, 512, maskable), `registerServiceWorker.ts`, `UpdatePrompt.tsx`; Tests `UpdatePrompt.test.tsx`, `frontend/scripts/check-sw.mjs` (run after build)

- [ ] **Step 1: Failing tests:** `UpdatePrompt shows "A new version is available." with Reload; does not appear while a bench dialog is open; appears after it closes` *(Review Focus 5)*; `Reload is never called without a click`; `check-sw.mjs fails if the generated sw.js precache list or routes contain "/api/" or the API origin, and passes otherwise`; `manifest has name, short_name, start_url "/", display "standalone", orientation "any", 192/512/maskable icons`.
- [ ] **Step 2–4:** FAIL → implement → `npm run build && node scripts/check-sw.mjs` PASS. Add `check-sw` to the CI frontend job after build.
- [ ] **Step 5:** Commit `feat(bench): installable app shell`.

### Task 11: Final verification and documents

- [ ] **Step 1:** Frontend: lint, `tsc -b`, test, build, `debt:check`, `check-sw`. Backend: Release build and full suite without and with PostgreSQL; exact counts.
- [ ] **Step 2:** Browser check with the pre-installed Chromium at 1280×800, 1024×768 and 768×1024 with touch emulation: each of the four screen groups; measure the smallest interactive target on each (must be ≥ 48 px); screenshots attached to the PR.
- [ ] **Step 3:** Draft walk-through in the browser with the network blocked: type counts, lose connection, reload, restore, reconnect, save; confirm the audit row's source context after a delayed save. Report what was and was not exercised; state plainly that no real tablet or glove test was done.
- [ ] **Step 4:** `documents/`: requirements; user guide page; SOP notes (a draft is not a record; save before leaving the bench; tablets are lab-owned and locked down; what to do with a rejected draft). Roadmap links; D-7 marked decided.
- [ ] **Step 5:** Commit `docs(bench): requirements, guide, roadmap links`.

## Manual gates (site)

- Each screen group on the real tablet model with gloves.
- A real Wi-Fi drop during a count entry; review of the resulting audit entry by QA.
- Install to the home screen on the chosen tablet and one update cycle.

## Cross-plan corrections (whole-programme review, 2026-11-11)

These corrections come from reviewing all plans together against the code. Where they differ from the text above, they win. Shared names are defined once in `docs/superpowers/plans/2026-11-11-build-order.md` (section "Programme conventions").

1. This plan owns `GET api/client-settings`; C3 and others only add fields to it.
2. `/health` already exists (`HealthCheckExtensions.cs`); the connection status hook calls it.
3. Draft rule (programme-wide): every `useBenchDraft` call passes an explicit **allow-list** of fields.
