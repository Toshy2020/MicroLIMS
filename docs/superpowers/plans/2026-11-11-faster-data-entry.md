# Faster Data Entry (C4) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** All-or-nothing batch receiving, server errors shown next to their field, one keyboard model for entry grids, validated "last choice" defaults, paste with preview (receiving, EM/water, five configuration screens), and a server-calculated count preview.

**Architecture:** A field-error extension to `ApiResponse` plus one frontend helper carries every server validation message to its control. Batch operations reuse the existing single-row services inside one outer transaction, so no rule is re-implemented. Paste is one shared preview dialog fed by a per-grid column map. The count calculation is extracted into a pure calculator used by both save and preview.

**Tech Stack:** .NET 10, EF Core 10, PostgreSQL; React 19, TypeScript 6, MUI 9, Vitest.

**Spec:** `docs/superpowers/specs/2026-11-11-faster-data-entry-design.md`

**Prerequisite:** C2 Tasks 3–4 (`NumericEntryField`, `useBenchDraft`).

## Global Constraints

- `CLAUDE.md` binds every task (separate contexts + `AsNoTracking`; `[PostgresFact]` executed, 0 skipped; `userEvent`; real components; mock only `services/`; no `any`; injected `TimeProvider`).
- No laboratory rule is added, removed or re-implemented. Batch and paste paths call the same validators and services as the single-row paths.
- Limits: 100 rows per batch receive; 500 rows per paste; 200 rows per configuration batch.
- Batch endpoints are all-or-nothing: any failing row → nothing persisted, every failing row and field reported.
- No nested transactions: check `Database.CurrentTransaction` before beginning one (`CLAUDE.md` 5.4).
- The frontend never computes a result. The count preview comes from the server and is labelled "Preview — not recorded".
- A default is offered only when the server confirms it would pass the save-time checks; a pre-filled **lot** must be confirmed or replaced before Save is enabled.
- Paste never writes to a grid without the preview being accepted, and acceptance is blocked while any cell has a format error.
- Existing single-row endpoints and `ApiResponse` fields keep their shape (additive changes only).
- Setting: `entry.lastChoiceDefaults.enabled` (Boolean, default true, GxP-relevant).

## Review Focus

1. **Two people receive samples of the same category at the same moment.** Sample numbers are `COUNT + 1` with no lock today, and the product path has no retry. Both receipts must succeed with distinct, gap-free numbers. Task 2.
2. **Row 100 of a batch fails at the database** (not at validation). Rows 1–99, their audit rows and their numbers must all disappear. Task 3.
3. **A pasted EM block lists the locations in a different order, or omits one.** Every count must land on the row with the same location name; the omitted location stays empty and is reported. Task 7.
4. **The equipment an analyst used yesterday failed calibration overnight.** It must not be offered today. Task 6.
5. **Preview and saved result differ.** Today the browser rounds with `toFixed` and the server with `MidpointRounding.AwayFromZero`. After this plan one calculator produces both; a test feeds the same inputs to preview and save and compares. Task 8.

## File Structure

| File | Responsibility |
|---|---|
| `backend/MicroLIMS.Shared/Responses/ApiResponse.cs`, `FieldError.cs`; `Exceptions/FieldValidationException.cs` | Field-error contract |
| `frontend/src/services/apiTypes.ts`, `frontend/src/utils/fieldErrors.ts` | One shared `ApiResponse<T>` type; error extraction |
| `backend/MicroLIMS.Application/Services/ReferenceNumberGenerator.cs` | Lock-safe numbering |
| `backend/MicroLIMS.Application/Services/SampleBatchReceiveService.cs`; `DTOs/BatchReceiveDtos.cs` | Batch receive |
| `frontend/src/components/entry/useGridNavigation.ts`, `PastePreviewDialog.tsx`, `parseClipboardTable.ts`, `ShortcutsDialog.tsx`, `LastChoiceField.tsx` | Shared entry primitives |
| `backend/MicroLIMS.Application/Services/EntryDefaults/EntryDefaultsService.cs`; `API/Controllers/EntryDefaultsController.cs` | Last-choice defaults |
| `backend/MicroLIMS.Application/Workflows/TestWorkflow/CountResultCalculator.cs` | Pure count calculation |
| `backend/MicroLIMS.Application/Services/MasterData/MasterDataBatchService.cs` | Configuration batch create |

---

### Task 1: Field-error contract

**Files:** Create `FieldError.cs`, `FieldValidationException.cs`, `apiTypes.ts`, `fieldErrors.ts`; Modify `ApiResponse.cs` (L4), `backend/MicroLIMS.API/Filters/ValidationFilter.cs`, `ExceptionMiddleware.cs` (new arm **before** L50), `frontend/src/utils/errorMessage.ts`; Tests `backend/MicroLIMS.Tests/UnitTests/FieldErrorContractTests.cs`, `fieldErrors.test.ts`

**Interfaces — Produces:**
```csharp
public sealed record FieldError(string Field, string Message, int? Row = null);     // Field is camelCase, matching the request JSON
// ApiResponse<T>: List<FieldError>? FieldErrors;  static Fail(string message, List<string>? errors, List<FieldError>? fieldErrors)
public class FieldValidationException : BusinessRuleException { public IReadOnlyList<FieldError> FieldErrors { get; } public FieldValidationException(string message, IReadOnlyList<FieldError> fieldErrors); }
```
```ts
export interface FieldError { field: string; message: string; row?: number | null }
export interface ApiResponse<T> { success: boolean; message?: string | null; data?: T | null; errors?: string[] | null; fieldErrors?: FieldError[] | null }
export function getFieldErrors(err: unknown): FieldError[];
export function errorsByField(err: unknown): Record<string, string>;                    // rows ignored
export function errorsByRow(err: unknown): Record<number, Record<string, string>>;      // row → field → message
```
`ValidationFilter` fills `FieldErrors` from the `ModelState` keys (camel-cased, strip a leading `request.`) and keeps `Errors` as today. The new middleware arm answers 400 with `Message`, `Errors` (the messages) and `FieldErrors`. The six local `ApiResponse` declarations in the frontend are **not** replaced in this task.

- [ ] **Step 1: Failing tests:** `Fail_WithoutFieldErrors_SerialisesExactlyAsBefore` (JSON equality against a stored string; `fieldErrors` omitted when null); `ModelStateError_ProducesCamelCaseFieldAndKeepsErrorsList`; `FieldValidationException_Returns400_WithFieldErrorsAndRows`; `BusinessRuleException_StillReturns400_NoFieldErrors`; frontend `getFieldErrors returns [] for network errors, non-axios errors and old-shape responses`; `errorsByRow groups by row`; `getErrorMessage unchanged for old responses`.
- [ ] **Step 2–4:** FAIL → implement → PASS. **Step 5:** Commit `feat(entry): field-level validation errors in ApiResponse`.

### Task 2: Lock-safe sample numbers

**Files:** Modify `ReferenceNumberGenerator.cs` (L34), the product receive path `ProductWorkflowEngine.cs` (~L200); Test `backend/MicroLIMS.Tests/IntegrationTests/ReferenceNumberConcurrencyPostgresTests.cs`

**Interfaces — Produces:** `GenerateAsync(SampleCategory category)` keeps its signature and format `{code}{MM}{YY}{seq:D3}`. Before counting, it takes `pg_advisory_xact_lock(hashtext('sample-ref:' || code || MMYY))` when a transaction is active on the connection; when none is active it behaves as today (the unique index plus the existing retry remain the backstop). The product path gains the same one-retry `TrySaveChangesAsync(UniqueIndexNames.SampleReferenceNumber)` the other three engines already use (`WaterWorkflowEngine.cs` L123–126).

- [ ] **Step 1: Failing tests:** `TwoConcurrentTransactions_SameCategory_GetDistinctConsecutiveNumbers` (two connections, each in a transaction, started together) *(Review Focus 1)*; `TwoConcurrentProductReceives_BothSucceed` (today one fails with a unique violation); `FiveSamplesInOneTransaction_GetFiveConsecutiveNumbers`; `DifferentCategories_DoNotBlockEachOther` (second completes while the first transaction is still open); `NumberFormat_Unchanged` (fixed clock, known count).
- [ ] **Step 2–4:** FAIL → implement → PASS, 0 skipped. **Step 5:** Commit `fix(receiving): lock-safe sample numbering`.

### Task 3: Batch receive — backend

**Files:** Create `SampleBatchReceiveService.cs`, `BatchReceiveDtos.cs`; Modify `SampleController.cs` (new action), the four validators / services so their validation can run without saving (`ReceiveSampleValidator.Validate` exists, L… in `Validators/`; add the equivalent callable for Water, EM, After Cleaning by extracting the checks their `ReceiveAsync` runs before the engine call); Test `backend/MicroLIMS.Tests/IntegrationTests/SampleBatchReceivePostgresTests.cs`

**Interfaces — Produces:**
```csharp
public sealed record BatchReceiveRequest(string Category,                       // "Product" | "Water" | "EM" | "AfterCleaning" (same names the frontend uses)
    IReadOnlyList<ReceiveItemBasedSampleRequest>? Items, IReadOnlyList<ReceiveWaterRequest>? Water,
    IReadOnlyList<ReceiveEMRequest>? Em, IReadOnlyList<ReceiveAfterCleaningRequest>? AfterCleaning);
public sealed record BatchReceivedSample(int Row, int SampleId, string ReferenceNumber);
public sealed record BatchReceiveResult(IReadOnlyList<BatchReceivedSample> Samples);
Task<BatchReceiveResult> ReceiveAsync(BatchReceiveRequest request, int userId, CancellationToken ct = default);
```
`POST api/samples/receive-batch`, same authorisation policy as the single receive actions. Order: request checks (exactly one list set and matching `Category`; 1–100 rows) → validate **all** rows, collecting `FieldError(field, message, row)`; also duplicates inside the request (same batch + control number where the single path forbids duplicates) → if any, throw `FieldValidationException("Some rows need correction.", …)` → begin one transaction (enlist if one is active) → call the existing per-row service (`ReceivingService.ReceiveSampleAsync`, `WaterService.ReceiveAsync`, `EMService.ReceiveAsync`, `AfterCleaningService.ReceiveAsync`) for each row in order → commit. Any exception during the loop rolls back and is rethrown as `FieldValidationException` carrying that row number when it is a `BusinessRuleException`, or unchanged otherwise. Section scope is enforced per row by the existing services.

- [ ] **Step 1: Failing tests:**
  - `AllValid_CreatesAllSamples_ConsecutiveNumbers_InRowOrder_WithAuditRows` (triad; assert one audit row per sample and tests created as the single path does)
  - `OneInvalidRow_NothingCreated_ReturnsRowAndField`
  - `ThreeInvalidRows_AllThreeReported`
  - `LastRowFailsInsideTheEngine_EverythingRolledBack_NoSamples_NoAuditRows_NumbersNotConsumed` *(Review Focus 2)* (force with a row whose item is deactivated between validation and execution, via a second context)
  - `Rows101_Rejected`; `EmptyList_Rejected`; `TwoListsSet_Rejected`; `CategoryMismatch_Rejected`
  - `RowForSectionICannotWriteTo_WholeBatchRejected_WithThatRow`
  - `DuplicateBatchAndControlInsideRequest_ReportedOnBothRows`
  - `ResultOfBatch_EqualsResultOfSameRowsReceivedSingly` (compare created samples and test orders field by field)
  - `WithoutPermission_403`
- [ ] **Step 2–4:** FAIL → implement → PASS, 0 skipped. Mutation check: remove the transaction → the rollback test fails.
- [ ] **Step 5:** Commit `feat(receiving): all-or-nothing batch receive`.

### Task 4: Grid navigation, shortcuts, focus on open

**Files:** Create `useGridNavigation.ts`, `ShortcutsDialog.tsx` with tests; Modify `frontend/src/components/UnitEntryGrid.tsx` (L42–64), `frontend/src/components/lab/FormDialog.tsx` (L24: prop `initialFocus?: "firstEmptyRequired" | "none"`, default `"firstEmptyRequired"`), `Header.tsx` (help menu entry)

**Interfaces — Produces:**
```ts
export function useGridNavigation(opts: { rows: number; cols: number; isEditable?(r: number, c: number): boolean; onCopyDown?(r: number, c: number): void; onSave?(): void }): {
  cellProps(r: number, c: number): { ref: (el: HTMLElement | null) => void; onKeyDown: (e: React.KeyboardEvent) => void; "data-grid-cell": string };
  focusCell(r: number, c: number): void };
```
Keys exactly as spec 5.1. Left/Right move only when the caret is at the text edge or the control is not a text input. Enter inside a grid calls `preventDefault` so the surrounding form is not submitted. Non-editable cells are skipped. Keys are ignored while an Autocomplete popup is open.

- [ ] **Step 1: Failing tests:** each key from the table, including edges (last row + Enter stays; first row + Up stays); `Left in the middle of text moves the caret, not the cell`; `Enter does not submit the form; Ctrl+Enter calls onSave`; `Ctrl+D calls onCopyDown and is ignored on row 0`; `read-only cells are skipped`; `open Autocomplete keeps arrow keys`; `UnitEntryGrid keeps its existing Enter/Up/Down behaviour and paste, and gains Left/Right and Ctrl+D`; `FormDialog focuses the first empty required field; "none" leaves focus alone`; `ShortcutsDialog lists the keys and opens from the help menu and from "?" when no input is focused`.
- [ ] **Step 2–4:** FAIL → implement → PASS. **Step 5:** Commit `feat(entry): shared grid keyboard navigation`.

### Task 5: Receiving grid — batch save, errors, keys, copy-down, draft

**Files:** Modify `frontend/src/modules/receiving/dialogs/NewSampleDialog.tsx` (validation L186–262, save loop L283–363), `MultiSampleEntryGrid.tsx` (props L54–62: replace `value: any` with a typed union; table L191–294), `ReceiveService.ts`, `receivingTypes.ts`; Tests `NewSampleDialog.test.tsx`, `MultiSampleEntryGrid.test.tsx`

**Interfaces — Consumes:** Tasks 1, 3, 4; `useBenchDraft` (`formKey` `"receiving"`, `recordKey` = category, `enabled` always; `pick` excludes `coaFile` and `errors`).

- [ ] **Step 1:** Characterise today's dialog first: for each category, the payload per row equals the current single-request body (fixtures). Run → PASS.
- [ ] **Step 2: Failing tests:** `Save sends one receiveBatch call with all rows in the same per-row shape`; `server fieldErrors with rows appear under the matching cells, linked by aria-describedby, and focus moves to the first`; `a summary line states "3 problems in 2 rows" in a live region`; `on failure no success toast and all rows stay`; `on success the result lists sample numbers in row order`; `certificate files upload after success; a failed upload shows "Certificate not attached — retry" on that row and does not re-register`; `grid keys work; Ctrl+D copies the cell above, including choice columns`; `"Add row" still copies sampledBy, cause and techniques`; `"Draft saved on this computer at HH:mm — not recorded" appears after typing; reopening offers the draft; files are not restored`; `more than 100 rows: Add row disabled with the reason`.
- [ ] **Step 3–4:** Implement → PASS; lint, `tsc -b`, `debt:check` (`debt:update` after removing `any`).
- [ ] **Step 5:** Commit `feat(receiving): batch save with cell-level errors and keyboard entry`.

### Task 6: Last-choice defaults

**Files:** Create `EntryDefaultsService.cs`, `EntryDefaultsController.cs`, `LastChoiceField.tsx`, `frontend/src/services/entryDefaultsService.ts`; Modify `SystemSettingDefinitions.cs`, `TestWorkflowDialog.tsx` (media and incubator fields, L76–77), `MediaPreparationDialog.tsx`, `LotsStep.tsx`, `NewSampleDialog.tsx`; Tests `backend/MicroLIMS.Tests/IntegrationTests/EntryDefaultsPostgresTests.cs`, `LastChoiceField.test.tsx`

**Interfaces — Produces:**
```csharp
public sealed record EntryDefault(string Field, int? Id, string? Text, string Label, DateTime LastUsedAtUtc);
public sealed record EntryDefaultsResponse(IReadOnlyList<EntryDefault> Defaults);
Task<EntryDefaultsResponse> GetAsync(int userId, string context, int? testOrderId, int? stepMediaId, int? mediaProductId, int? solutionMasterId, string? category, CancellationToken ct = default);
```
`GET api/entry-defaults?context=incubation|mediaPreparation|solutionPreparation|receiving&…`. Sources (the user's own latest record of the same kind): `Incubation` by `StartedByUserId` → `IncubatorEquipmentId`, and `MediaId` for the **same media product** as the step requires; `Media.AutoclaveEquipmentId` by `PreparedByUserId`; `SolutionPreparation.AutoclaveEquipmentId` by `StartedByUserId`; `Sample.SampledBy` / `CauseOfTestingId` by `ReceivedByUserId` and category. Offered only when all hold: the setting is on; `EnsureEquipmentAccessAsync` passes; equipment is in service and `IsCalibrationOverdueAt(now)` is false; for an incubator `RequireEligibleIncubatorAsync(stepMediaId, equipmentId)` passes; for a lot the exact save-time condition (`IsReleasedForUse`, status not `OutOfStock`/`QuarantineFailed`, `SectionMediaRule.EnsureLot`) passes and the lot is not expired; a cause of testing is still active. **There is no balance field in the data, so no balance default is offered**; say so in the PR.

`LastChoiceField` wraps a picker: shows the offered value with the caption "From your last entry"; `kind="lot"` adds a "Confirm" button and reports `confirmed: false` until pressed or the value is changed (a B1 scan counts as a change).

- [ ] **Step 1: Failing tests:**
  - `ReturnsMyLastIncubator_NotAnotherUsers`
  - `EquipmentCalibrationOverdueToday_NotOffered` *(Review Focus 4)*; `EquipmentOutOfService_NotOffered`; `IncubatorOutsideStepTemperatureRange_NotOffered`
  - `LotForDifferentMediaProduct_NotOffered`; `LotExpired_NotOffered`; `LotNotReleased_NotOffered`; `LotOfAnotherSection_NotOffered`
  - `EquipmentInSectionINoLongerBelongTo_NotOffered`
  - `SettingOff_ReturnsEmpty`; `NoHistory_ReturnsEmpty`
  - `Receiving_ReturnsSampledByAndCause_ForSameCategoryOnly`; `InactiveCause_NotOffered`
  - `EndpointSavesNothing` (row counts and audit count unchanged)
  - Frontend: `default shown with caption`; `lot: Save disabled with reason "Confirm the pre-filled lot" until Confirm or change`; `equipment default needs no confirmation`; `defaults request fails → empty field, no error dialog`; `changing the value removes the caption`.
- [ ] **Step 2–4:** FAIL → implement → PASS. **Step 5:** Commit `feat(entry): validated last-choice defaults`.

### Task 7: Paste with preview — receiving, EM, water

**Files:** Create `parseClipboardTable.ts`, `PastePreviewDialog.tsx` with tests; Modify `MultiSampleEntryGrid.tsx`, `LocationResultGridDialog.tsx`, `WaterLocationResultGridDialog.tsx`

**Interfaces — Produces:**
```ts
export function parseClipboardTable(text: string): string[][];        // tabs/newlines, quoted cells, trailing blank line dropped
export interface PasteColumn { key: string; label: string; check(value: string): string | null }   // null = ok; format checks only
export interface PastePlan<Row> { title: string; columns: PasteColumn[]; maxRows: number;
  match?(cells: string[][]): { rows: { target: number | null; values: Record<string, string>; problem?: string }[]; unmatchedTargets: string[] } }
// PastePreviewDialog props: { open; plan; cells; onAccept(rows): void; onCancel(): void }
```
Receiving plan: positional mapping onto the visible text/date columns for the category, starting at the focused cell's column; choice columns are skipped and listed as "not pasted". Location plan: exactly two columns, name then count; `match` pairs by `locationName` after trimming and case-folding. The dialog blocks Accept when: any format error; a name not in the grid; a name twice in the paste; a name that matches two grid rows (duplicate names in the session — paste is refused with "Location names are not unique in this session; enter counts by hand"); one column only; more than 500 rows. Locations not present in the paste are listed as "Left empty" and do not block.

- [ ] **Step 1: Failing tests:** parser (tabs, CRLF, quoted cell with newline, trailing newline, single cell → not a table); `receiving: block fills rows from the focused cell, adds rows as needed up to 100, reports date format problems per cell`; `EM: shuffled block lands each count on its named row` *(Review Focus 3)*; `EM: omitted location stays empty and is listed`; `unknown name blocks Accept and is listed`; `duplicate name in paste blocks`; `duplicate names in grid refuse paste with the message`; `single column refused with explanation`; `count "12.5" or "-1" flagged`; `Cancel leaves the grid untouched`; `after Accept values are in the grid, unsaved, and Save goes through the normal service call`; `501 rows refused`; `preview table has headers, row numbers and is keyboard operable`.
- [ ] **Step 2–4:** FAIL → implement → PASS. **Step 5:** Commit `feat(entry): paste with preview for receiving, EM and water`.

### Task 8: Server count preview

**Files:** Create `CountResultCalculator.cs`; Modify `IncubationStepRecorder.cs` (`RecordCountTestAsync` L765), `TestWorkflowController.cs` (action beside L169), `TestWorkflowService.ts`, `TestWorkflowDialog.tsx` (L237–257); Tests `backend/MicroLIMS.Tests/UnitTests/CountResultCalculatorTests.cs`, `backend/MicroLIMS.Tests/IntegrationTests/CountPreviewPostgresTests.cs`, `TestWorkflowDialog.countTest.test.tsx`

**Interfaces — Produces:**
```csharp
public sealed record CountCalculationInput(IReadOnlyList<string> RawPlateReadings, decimal DilutionFactor, /* plus the specification/category inputs RecordCountTestAsync reads today */ …);
public sealed record CountCalculationResult(string Reported, decimal? Average, decimal? Calculated, ResultStatus Status, IReadOnlyList<string> Messages);
public static class CountResultCalculator { public static CountCalculationResult Calculate(CountCalculationInput input); }
```
**This is a move, not a change.** Step 1 pins today's behaviour: before touching `RecordCountTestAsync`, write tests through the existing save path covering every branch that method has (direct count, dilution, the DF = 1 rule for Water/EM/After Cleaning, TNTC/Uncountable → `RequiresReview`, rounding midpoints, below-limit reporting). Then extract the arithmetic and reporting into `Calculate` so `RecordCountTestAsync` loads data, calls it, and builds the entity. If any branch is unclear while extracting, stop and ask; do not guess a rule. `POST api/test-workflow/{testOrderId}/preview-count-result` takes `RecordTestResultRequest`, runs the same loading and `Calculate`, returns `{ reported, average, calculated, status, messages }`, saves nothing, and uses the same authorisation and section scope as `record-result`.

- [ ] **Step 1:** Characterisation tests on the save path (above) → PASS on current code.
- [ ] **Step 2: Failing tests:** `Calculate_MatchesSavedValues_ForEachCharacterisedCase` (same table); `Preview_ReturnsSameReportedValueAsSubsequentSave` *(Review Focus 5)*; `Preview_PersistsNothing` (no reading, no result, no audit row); `Preview_OtherSection_Forbidden`; `Preview_InvalidReadings_ReturnsFieldErrors`; frontend `the dialog shows the server's reported value labelled "Preview — not recorded"`; `no arithmetic remains in the component` (the preview text equals the mocked service value even when it disagrees with count × dilution); `typing debounces the preview call (400 ms) and aborts the previous one`; `preview failure shows "Preview unavailable" and Save still works`.
- [ ] **Step 3–4:** Extract and implement → all PASS, 0 skipped.
- [ ] **Step 5:** Commit `refactor(workflow): one count calculator for save and preview`.

### Task 9: Configuration paste (five screens)

**Files:** Create `MasterDataBatchService.cs` and one `POST …/batch` action per controller; frontend paste plans beside each page; Tests `backend/MicroLIMS.Tests/IntegrationTests/MasterDataBatchPostgresTests.cs`, one frontend test per screen

One commit per screen, in this order:

| Screen | Page | Batch endpoint (list of the existing create DTO) |
|---|---|---|
| Water sampling points | `WaterConfigPage.tsx` / `WaterLocationPanel.tsx` | `POST masterdata/water-sampling-points/batch` (`CreateWaterSamplingPointRequest`) |
| EM rooms and room test configurations | `EMConfigPage.tsx` / `EmPanels.tsx` | `POST masterdata/rooms/batch`, `POST masterdata/room-test-configurations/batch` |
| Item specifications | `ItemSpecificationsSection.tsx` | `POST masterdata/specifications/batch` (`CreateSpecificationRequest`) |
| Master testing materials | `MasterTestingMaterialsPage.tsx` | `POST masterdata/material-masters/batch` (`SaveMaterialMasterEntryRequest`) |
| Solution master | `SolutionMasterPage.tsx` | `POST masterdata/solution-masters/batch` (`SaveSolutionMasterRequest`) |
| Equipment master | `EquipmentPage.tsx` | `POST masterdata/equipment/batch` (`CreateEquipmentRequest`) |

Each batch action: same policy as the single create; ≤ 200 rows; validate all → `FieldValidationException` with rows; one transaction calling the existing single-create service method per row; all-or-nothing; each row audited exactly as a single create. Paste columns are the text, number and date fields of that DTO in the order the page's form shows them; fields that are choices (item, test code, type, department) are pasted as their **code or name** and resolved by the server-provided lists already loaded on the page — an unresolved or ambiguous name is a preview error. Codes that already exist are flagged in the preview from the loaded list and rejected again by the server.

The existing master-data workbook (`api/masterdata/workbook`: staged Excel import with a separate password-confirmed verify step) already covers most of these lists for large loads. It is not changed. The user guide states when to use which: paste for a handful to a few hundred rows typed into the screen; the workbook for initial loads that need a second person's verification.

- [ ] **Step 1 (per screen):** failing backend tests: `AllValid_CreatesAll_AuditedLikeSingleCreate`; `OneInvalid_NoneCreated_RowAndFieldReported`; `DuplicateCodeInRequest_BothRowsReported`; `ExistingCode_Reported`; `201Rows_Rejected`; `WithoutPermission_403`; `SectionICannotConfigure_Rejected`. Frontend: `paste opens preview with the screen's columns`; `unknown choice name is a preview error`; `existing code flagged`; `Accept then Save sends one batch call`; `server row errors appear in the preview list`.
- [ ] **Step 2–4:** FAIL → implement → PASS, 0 skipped. **Step 5:** Commit `feat(config): paste entry for <screen>`.

### Task 10: Remaining grids, final verification, documents

- [ ] **Step 1:** Apply `useGridNavigation` to `PathogenLocationResultGridDialog.tsx`, `PrimaryObservationMatrixPanel.tsx`, `ReplicateEntryTable.tsx` (keeping its existing Enter handlers' behaviour); a test per grid that Enter moves down and the save payload is unchanged.
- [ ] **Step 2:** Backend Release build; full suite without and with PostgreSQL; exact counts. Frontend lint, `tsc -b`, test, build, `debt:check`.
- [ ] **Step 3:** Run the app: register 12 product samples by keyboard and by paste, with one bad row first; time it against the old dialog on `main`; enter an EM session by paste with a shuffled block; start an incubation with a pre-filled lot; paste 20 water points. Report what was and was not exercised.
- [ ] **Step 4:** `documents/` requirements, user-guide pages (keyboard sheet, paste, defaults), SOP note: pasted data must be checked in the preview; a pre-filled lot must be checked against the plate before confirming. Roadmap links.
- [ ] **Step 5:** Commit `docs(entry): requirements, guide, roadmap links`.

## Found while reading the code (not fixed here)

- **Incubator selection checks only the temperature range at save time**, not the equipment's calibration or qualification status (`TestWorkflowSupport.RequireEligibleIncubatorAsync`, L818). `CLAUDE.md` Rule 3.2 expects equipment used in testing to be in valid status. This plan does not offer such equipment as a default, but it does not change the save rule. Raise with the user as a separate decision.
- The notification email for an expired media lot is sent to `{username}@microlims.local`, not to the user's email address (`DashboardNotificationService.cs` L266). Handled in package C5.

## Cross-plan corrections (whole-programme review, 2026-11-11)

These corrections come from reviewing all plans together against the code. Where they differ from the text above, they win. Shared names are defined once in `docs/superpowers/plans/2026-11-11-build-order.md` (section "Programme conventions").

1. Task 2: `ReferenceNumberGenerator.GenerateAsync` always runs inside a transaction — when called without one it wraps itself in `UnitOfWork.RunAsync` (which joins an existing transaction), so the advisory lock always applies.
2. Task 5: `useBenchDraft` uses an explicit allow-list of the receiving fields (not a deny-list).
3. Configuration paste covers six screens (the plan's list); the spec's 'five' is corrected to six.

## Review corrections (independent review, 2026-11-11)

From the independent review of the build order. These win over the text above and over the earlier corrections section where they differ.

1. **Sample number lock (replaces the earlier correction):** the advisory lock must cover generating the number **and** inserting the sample. `ProductWorkflowEngine.ReceiveAsync` and the number-generating paths in `EMWorkflowEngine`, `WaterWorkflowEngine` and `AfterCleaningWorkflowEngine` wrap generate-plus-insert in `UnitOfWork.RunAsync`. `ReferenceNumberGenerator.GenerateAsync` throws `InvalidOperationException` when the provider is relational and there is no current transaction. The lock key is `AdvisoryLockKeys.SampleNumber` (not `hashtext(...)`).
2. Open items to resolve before Task 3/8: the line placeholder `L…` in Task 3 and the elided `…` fields of `CountCalculationInput` in Task 8 are filled from the code when the task starts; the supplier certificate stays optional for every category (user decision 2026-11-11).
