# Simpler Configuration Screens (C7) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild the Specification, Test Master, Equipment and Add Material screens as step-by-step wizards with a review step, add a where-used panel, enforce change reasons on the server, retire used specifications instead of deleting them, add four copy-from-existing flows, and add an analyst preview of a test.

**Architecture:** A shared `ConfigWizard` frame owns step order, per-step validation and the review step; each screen supplies small step components and one form model that maps to the existing request payload unchanged. The backend gains a `MasterDataUsageService` (where-used counts), a `ChangeReasonGuard` applied inside the existing update/delete/freeze services, validate-only endpoints, and combined endpoints that wrap existing services in one transaction.

**Tech Stack:** React 19, TypeScript 6, MUI 9, Vitest; .NET 10, EF Core 10, PostgreSQL.

**Spec:** `docs/superpowers/specs/2026-11-11-simpler-configuration-design.md`

## Global Constraints

- `CLAUDE.md` binds every task (separate contexts + `AsNoTracking`; `[PostgresFact]` executed, 0 skipped; `userEvent`; real components; mock only `services/`; no `any`; WCAG 2.2 AA; design tokens).
- **No configuration option changes.** Every wizard sends the same payload to the same endpoint as the screen it replaces; this is pinned by characterisation tests written against the old screen first.
- Recorded results are never modified by this package. `ParameterResult` and `ResultRecord` keep their copied limits.
- Limits stay "in force when the result is entered" (user decision 2026-11-11): no specification versioning, no effective dates.
- Reason rules, enforced by the server: edit of a specification, test definition, item or equipment **with open work affected** → reason required; delete / retire / freeze → reason always required; reason is trimmed and at least 10 characters; stored through `db.CurrentReason` so it lands on the audit rows of that save.
- **A specification that any recorded result refers to cannot be deleted; it is retired** (user decision). An unused specification may still be deleted, with a reason.
- "Open" = test orders not approved, rejected, cancelled or superseded.
- Copy flows never save without confirmation and never copy codes, SOP numbers, serial numbers, calibration dates or documents.
- Each wizard step component stays under 350 lines.
- One migration, `AddSpecificationRetirement`.

## Review Focus

1. **A limit type's hidden fields.** Switching limit type in the wizard and back must not send stale values from the first type; the payload must equal what the old dialog sends for the same visible input. Task 4.
2. **Two Section Heads edit the same test.** The second Save gets the existing version conflict; the wizard keeps the user's input and shows what changed underneath instead of discarding an hour of work. Task 2.
3. **Reason bypass.** A direct API call without a reason while open samples exist must be refused, and the usage count used for that decision is computed in the same request, not trusted from the client. Task 3.
4. **Retired specification and a sample received afterwards.** The retired row must not be picked up for evaluation or auto-assignment, and a sample in testing when it was retired must fail clearly ("specification retired"), not silently pass with no limit. Task 3.
5. **Copy to an item in a section the user cannot configure.** Refused on the server for the target; the source may be any item the user can read. Task 5.

## File Structure

| File | Responsibility |
|---|---|
| `frontend/src/components/config/ConfigWizard.tsx`, `WizardReview.tsx`, `useWizard.ts`, `diffValues.ts` | Wizard frame, review, change view |
| `frontend/src/components/config/WhereUsedPanel.tsx`, `ChangeReasonDialog.tsx`, `CopyFromDialog.tsx` | Shared configuration parts |
| `frontend/src/modules/laboratoryConfiguration/items/specWizard/*` | `SpecificationWizard.tsx`, one step per file, `specFormModel.ts` (form ↔ payload) |
| `frontend/src/modules/laboratoryConfiguration/masterDataSimple/testWizard/*` | Test wizard steps, `testFormModel.ts`, `AnalystPreview.tsx` |
| `frontend/src/modules/laboratoryConfiguration/masterDataSimple/equipmentWizard/*` | Equipment wizard steps, typed payload |
| `frontend/src/modules/inventory/materials/materialWizard/*` | Material wizard steps |
| `backend/MicroLIMS.Application/Services/MasterData/MasterDataUsageService.cs`, `ChangeReasonGuard.cs` | Usage counts; reason rule |
| `backend/MicroLIMS.Application/Services/MasterData/MasterDataCopyService.cs` | Copy specifications; item copy |
| `backend/MicroLIMS.API/Controllers/MasterDataUsageController.cs` | `api/masterdata/usage` |

---

### Task 1: Where-used service and panel

**Files:** Create `MasterDataUsageService.cs`, `MasterDataUsageController.cs`, `WhereUsedPanel.tsx`, `frontend/src/services/masterDataUsageService.ts`; Tests `backend/MicroLIMS.Tests/IntegrationTests/MasterDataUsagePostgresTests.cs`, `WhereUsedPanel.test.tsx`

**Interfaces — Produces:**
```csharp
public enum MasterRecordType { TestDefinition, Specification, Item, Equipment, MaterialMaster }
public sealed record UsageLine(string Key, string Label, int Count, bool IsOpenWork, string? ListRoute);
public sealed record UsageSummary(MasterRecordType Type, int Id, int OpenWorkCount, int HistoricalCount, IReadOnlyList<UsageLine> Lines);
Task<UsageSummary> GetAsync(MasterRecordType type, int id, int userId, CancellationToken ct = default);
```
`GET api/masterdata/usage?type=&id=` (policy `MasterDataManage`, or the record's own read policy for Equipment and Item). Sources (count queries only, one per line):
- **TestDefinition** (by its `Code`): `Specification`, `SampleTest`, `RoomTestConfiguration`, `SamplingConfiguration`, `MachinePartConfiguration`, `WaterSamplingPoint.AssignedTestCodes`; open `TestOrder`; all `TestOrder`.
- **Specification**: open `TestOrder` for its `ItemId` + `TestCode`; `ParameterResult.SpecificationId`; `SpecificationStage`.
- **Item**: `Specification`, `SampleTest`, open `Sample`, all `Sample`, `ItemDocument`, `ItemPreparationConfiguration`, `HplcMethod`.
- **Equipment**: open `Incubation` (not completed), `TestAnalysis`, `HplcRun`/`IcpRun` in progress; historical totals of the same; `AutoclaveProgram`, `InstrumentAgent`.
- **MaterialMaster**: `Material` lots, `SolutionComponent`, `ItemPreparationConfiguration`, `MediaConfiguration`.
Counts respect the caller's readable sections; when some usage lies in sections the caller cannot read, a final line "In other sections" gives only a count.

- [ ] **Step 1: Failing tests:** one per record type seeding each source and asserting each line's count; `OpenVersusHistorical_Separated`; `SupersededAndCancelledOrders_NotOpen`; `UsageInOtherSection_CountedOnlyInOtherSectionsLine`; `UnknownId_404`; `NoQueryPerRow` (command count is constant for 1 and 50 usages). Frontend `panel lists lines with counts as text; open-work lines are marked "Open"; links go to ListRoute; loading, empty ("Not used anywhere") and error with Retry`.
- [ ] **Step 2–4:** FAIL → implement → PASS, 0 skipped. **Step 5:** Commit `feat(config): where-used service and panel`.

### Task 2: Wizard frame

**Files:** Create `ConfigWizard.tsx`, `WizardReview.tsx`, `useWizard.ts`, `diffValues.ts`, `ChangeReasonDialog.tsx` with tests

**Interfaces — Produces:**
```ts
export interface WizardStep<M> { key: string; title: string; applies?(model: M): boolean; validate(model: M): Record<string, string>;   // field → message; {} = valid
  render(ctx: { model: M; set(patch: Partial<M>): void; errors: Record<string, string>; mode: "create" | "edit" }): ReactNode;
  summary(model: M): { label: string; value: string; field: string }[] }
export interface ConfigWizardProps<M> { title: string; mode: "create" | "edit"; steps: WizardStep<M>[]; initial: M; original?: M;      // original = record as loaded (edit)
  draftKey: string; validateOnServer?(model: M): Promise<FieldError[]>; usage?(): Promise<UsageSummary>; reasonRequired?(usage: UsageSummary | null): boolean;
  onSave(model: M, reason: string | null): Promise<void>; onClose(): void; extraReview?(model: M): ReactNode }
export function diffValues(a: { label: string; value: string; field: string }[], b: typeof a): { label: string; before: string; after: string }[];
```
Behaviour: Next validates the current step only; the step list (MUI `Stepper`, non-linear once every step has been visited) marks a step with errors by icon and the text "needs attention"; Back keeps input; Review groups `summary` by step with an Edit link each, shows `diffValues` in edit mode, runs `validateOnServer` and maps `FieldError.field` to its step, loads `usage` and renders `WhereUsedPanel`; Save opens `ChangeReasonDialog` when `reasonRequired` is true (10-character minimum, counter shown). A 409 on save keeps the model, reloads `original`, and shows the diff between the user's model and the new `original` with "Review and save again". The device draft uses `useBenchDraft` with `enabled: true` and `pick` = the whole model.

- [ ] **Step 1: Failing tests (a two-step sample wizard):** `Next blocked with errors shown and focus on the first`; `Back keeps values`; `step with applies=false is omitted and its values are not in summary`; `Review groups by step; Edit jumps to the step and focuses the field`; `edit mode shows before/after only for changed values`; `server FieldErrors appear on Review and mark their step`; `usage failure blocks Save when a reason may be required, with Retry`; `reason dialog enforces 10 characters and passes the trimmed text to onSave`; `409 keeps input and shows what changed underneath` *(Review Focus 2)*; `draft restored after close and reopen`; `Escape with changes asks before closing`; `stepper has list semantics and aria-current on the active step`.
- [ ] **Step 2–4:** FAIL → implement → PASS; lint, `tsc -b`, `debt:check`. **Step 5:** Commit `feat(config): shared wizard frame`.

### Task 3: Reason rule, specification retirement, validate-only

**Files:** Create `ChangeReasonGuard.cs`, migration `AddSpecificationRetirement`; Modify `Specification.cs` (`bool IsRetired`, `DateTime? RetiredAt`, `int? RetiredByUserId`), `SpecificationMasterDataService.cs` (`UpdateSpecificationAsync` L142, `DeleteSpecificationAsync` L214–223), `SpecificationService.cs` (`ValidateAsync` L231 exposed through an endpoint), `TestDefinitionMasterDataService.cs` (`UpdateTestDefinitionAsync` L424, freeze L956–961), `ItemService.cs` (`SetActiveAsync` L132–136, update), `EquipmentMasterDataService.cs` (update), the four controllers (accept `reason`), every query that selects specifications for evaluation, auto-assignment or receipt (find all `_db.Specifications` readers in Application; each must exclude `IsRetired` unless it is a configuration or history view); Test `backend/MicroLIMS.Tests/IntegrationTests/ChangeReasonPostgresTests.cs`, `SpecificationRetirementPostgresTests.cs`

**Interfaces — Produces:**
```csharp
public sealed class ChangeReasonGuard
{
    // Throws FieldValidationException(field "reason") when a reason is required and missing/short. Sets db.CurrentReason when given.
    Task EnsureAsync(MasterRecordType type, int id, ChangeKind kind, string? reason, int userId, CancellationToken ct = default);
}
public enum ChangeKind { Edit, Delete, Retire, Freeze, Unfreeze }
```
Reason transport: header `X-Change-Reason` (URL-encoded UTF-8, ≤ 500 characters) on the existing PUT/DELETE/freeze routes, so request bodies and DTOs stay unchanged; CORS allows the header. `Edit` requires a reason when `MasterDataUsageService` reports `OpenWorkCount > 0` (computed here, in the same request). `Delete`/`Retire`/`Freeze` always require one; `Unfreeze` requires one too. `DELETE masterdata/specifications/{id}`: when any `ParameterResult.SpecificationId` or `ResultRecord` refers to the row → the row is retired (`IsRetired = true`, timestamp, user) and the response says `{ outcome: "retired" }`; otherwise it is deleted (`{ outcome: "deleted" }`). New `POST masterdata/specifications/{id}/reinstate` (reason required) clears retirement. New `POST masterdata/specifications/validate` and `POST masterdata/test-definitions/validate` run the existing validation for a create or update payload and return `FieldErrors` without saving.

A sample in testing whose specification was retired: the evaluation path must throw `BusinessRuleException("The specification for {test} on {item} has been retired. Ask the Section Head to reinstate it or configure a new one.")` rather than evaluate without limits. Before implementing, read how the result-entry path loads its specification and confirm with the user if any path would treat "no specification" as a pass.

- [ ] **Step 1: Failing tests:**
  - `EditSpec_WithOpenOrders_NoReason_Refused`; `…_WithReason_Saved_ReasonOnAuditRow` (triad)
  - `EditSpec_NoOpenOrders_NoReason_Saved`
  - `EditSpec_ClientClaimsNoOpenWork_ServerStillRequiresReason` *(Review Focus 3)*
  - `Reason9Chars_Refused`; `ReasonWhitespacePadded_TrimmedAndCounted`
  - `DeleteUnusedSpec_WithReason_Deleted`; `DeleteUnusedSpec_NoReason_Refused`
  - `DeleteSpecWithResults_IsRetired_NotDeleted_ResultsUnchanged` (compare `ParameterResult` rows before and after)
  - `RetiredSpec_NotUsedForNewSampleAssignmentOrEvaluation`; `ResultEntryAgainstRetiredSpec_ThrowsClearMessage` *(Review Focus 4)*
  - `Reinstate_WithReason_Works_Audited`; `RetiredSpec_ListedInConfigurationWithRetiredMark`
  - `FreezeTest_NoReason_Refused`; `FreezeItem_NoReason_Refused`; `EquipmentEdit_WithIncubationInProgress_RequiresReason`
  - `Validate_ReturnsFieldErrors_SavesNothing` (row and audit counts unchanged)
  - `ExistingClientsWithoutHeader_EditWithNoOpenWork_StillWork`
- [ ] **Step 2–4:** FAIL → implement → PASS, 0 skipped; `has-pending-model-changes` → none. Update every existing frontend call that deletes a specification or freezes a test/item to ask for a reason (`ChangeReasonDialog`) in this task, so the application is never left unable to perform them.
- [ ] **Step 5:** Commit `feat(config): change reasons and specification retirement`.

### Task 4: Specification wizard

**Files:** Create `specWizard/SpecificationWizard.tsx`, `specFormModel.ts`, steps `TestParameterStep.tsx`, `MethodStep.tsx`, `LimitStep.tsx` plus one file per limit-type group under `specWizard/limits/`, `UnitDilutionStep.tsx`; Modify `ItemSpecificationsSection.tsx` (open the wizard), delete `SpecificationParameterDialog.tsx` when unused; Tests `specFormModel.test.ts`, `SpecificationWizard.test.tsx`, keep `SpecificationParameterDialog.rs.test.tsx` cases by porting them

**Interfaces — Produces:**
```ts
export interface SpecFormModel { /* every field the dialog's 39 state variables hold, typed */ }
export function toPayload(model: SpecFormModel): CreateSpecificationPayload | UpdateSpecificationPayload;   // the body of handleSave L612–868, moved unchanged
export function fromSpecification(spec: SpecificationDto): SpecFormModel;
export function validateStep(step: "test" | "method" | "limit" | "unit", model: SpecFormModel): Record<string, string>;   // the checks of handleSave L612–735, split by step, same messages
```
`toPayload` sends only the fields belonging to the selected limit type and method kind, exactly as the dialog does today.

- [ ] **Step 1:** Characterisation against the **old dialog**: for each of the 11 limit types (Range, NotMoreThan, NotLessThan, TargetWithTolerance, CountTiered, Qualitative, PresenceAbsence, MultiStage, DissolutionQ, DisintegrationTime, WeightVariation), plus an HPLC assay parameter, an ICP parameter and a titration basis case, fill the dialog with `userEvent` and capture the payload passed to `SpecificationService.create` / `update` as fixtures. Run → PASS.
- [ ] **Step 2: Failing tests:** `toPayload(fixtureModel) deep-equals each captured fixture`; `switching limit type then back sends no stale fields` *(Review Focus 1)*; `validateStep returns the dialog's messages for each missing field`; `Method step is omitted for tests without an HPLC/ICP method`; `wizard create and edit send the fixture payloads`; `Review shows the limits in the same wording the item's specification table uses`; `edit with open orders asks for a reason and sends X-Change-Reason`.
- [ ] **Step 3–4:** Implement by moving code, not rewriting rules → all PASS; lint, `tsc -b`, `debt:check`.
- [ ] **Step 5:** Commit `refactor(config): specification wizard`.

### Task 5: Copy specifications between items; item copy

**Files:** Create `MasterDataCopyService.cs`, `CopyFromDialog.tsx`, `frontend/src/modules/laboratoryConfiguration/items/CopySpecificationsDialog.tsx`; Modify `SpecificationMasterDataController.cs`, `ItemController.cs`, item create form; Tests `backend/MicroLIMS.Tests/IntegrationTests/MasterDataCopyPostgresTests.cs`, frontend tests

**Interfaces — Produces:**
```csharp
public sealed record CopySpecificationsRequest(int SourceItemId, int TargetItemId, IReadOnlyList<int> SpecificationIds);
public sealed record CopySpecificationsPreviewRow(int SourceSpecificationId, string TestCode, string ParameterName, string Status /* Ok | AlreadyPresent | Invalid */, string? Message);
Task<IReadOnlyList<CopySpecificationsPreviewRow>> PreviewAsync(CopySpecificationsRequest r, int userId, CancellationToken ct = default);
Task<int> CopyAsync(CopySpecificationsRequest r, int userId, CancellationToken ct = default);          // returns rows created
public sealed record CopyItemOptions(int SourceItemId, bool Specifications, bool AutoAssignedTests);
```
`POST masterdata/specifications/copy/preview`, `POST masterdata/specifications/copy`; item create accepts optional `copyFrom: CopyItemOptions`. "Already present" = the target has a non-retired row with the same test code, parameter, production stage role and grade (the duplicate rule `SpecificationService` L282–324 uses). `CopyAsync` creates each row through the existing create path inside one transaction; any invalid row aborts all; `AlreadyPresent` rows are skipped. The target must be configurable by the caller (`SpecificationOwnership.EnsureCanEditAsync`); the source must be readable. Retired source rows are not offered. Each created row's audit entry carries `SourceContext = "Copied from specification {id} of item {code}"`. Item copy: the new item's own fields come from the form (code, name and SOP number are never pre-filled); documents are never copied.

- [ ] **Step 1: Failing tests:** `Preview_MarksOkAlreadyPresentInvalid_SavesNothing`; `Copy_CreatesOkRows_SkipsAlreadyPresent_AllOrNothingOnInvalid`; `Copy_AuditNotesSource`; `Copy_TargetInSectionICannotConfigure_Refused` *(Review Focus 5)*; `Copy_SourceIJustRead_Allowed`; `RetiredSourceRows_NotOffered`; `CopiedRow_EqualsSourceInEveryConfiguredField_ExceptItemId` (reflection over the entity's columns, with an explicit exclusion list); `ItemCopy_WithBothOptions_CreatesItemSpecsAndAutoAssignedTests_InOneTransaction`; `ItemCopy_DoesNotCopyCodeNameSopNumberOrDocuments`. Frontend `choose source, tick tests, preview with statuses, Save disabled while any ticked row is Invalid`; `"New from existing" item form shows the two tick boxes and empty code/name/SOP`.
- [ ] **Step 2–4:** FAIL → implement → PASS, 0 skipped. **Step 5:** Commit `feat(config): copy specifications and items`.

### Task 6: Test Master wizard, combined save, analyst preview, new from existing

**Files:** Create `testWizard/TestMasterWizard.tsx`, `testFormModel.ts`, steps `BasicsStep.tsx`, `TypeSettingsStep.tsx` (with one file per settings block: measurement, gravimetric, dissolution, disintegration, weight variation, titration, ICP, UV), `StagesStep.tsx`, `StepsStep.tsx`, `AnalystPreview.tsx`; Modify `TestMasterPage.tsx` (list, filters and details stay; the Add/Edit dialog L820+ is replaced), `TestDefinitionMasterDataController.cs` + service (combined endpoints), `WorkflowStepsSection.tsx` (driven by the model in the wizard; unchanged in the details view); Tests `TestDefinitionWithStepsPostgresTests.cs`, `testFormModel.test.ts`, `TestMasterWizard.test.tsx`, `AnalystPreview.test.tsx`; keep `TestMasterPage.test.tsx` passing

**Interfaces — Produces:**
```csharp
public sealed record SaveTestWithStepsRequest(CreateTestDefinitionRequest Test, IReadOnlyList<…existing step create DTO…> Steps, IReadOnlyList<…existing stage/replicate DTO…> Stages);
// POST masterdata/test-definitions/with-steps  → creates the test, then stages, then steps through the existing services in one transaction
```
```ts
export function toTestPayload(model: TestFormModel): CreateTestDefinitionPayload | UpdateTestDefinitionPayload;   // the body of save() L266–640, moved unchanged
export function previewModel(model: TestFormModel): PreviewStep[];
export interface PreviewStep { order: number; name: string; media: string[]; incubation?: { minHours: number; maxHours: number; tempMin?: number; tempMax?: number }; stages: string[]; analystEnters: string[] }
// AnalystPreview props: { steps: PreviewStep[] }  — reuses StepChainStrip for the chain
```
Edit mode keeps the existing separate endpoints (test update; step add/update/remove) and their order; only **create** uses the combined endpoint. `analystEnters` is derived from the workflow type and step configuration the same way the workflow dialog decides which panel to show; if that mapping is not a pure function today, extract it into a shared module and use it in both places rather than writing a second copy.

- [ ] **Step 1:** Characterisation against the old page: capture `addNew` / `update` payloads for one test of each workflow/test type the page supports (count, pathogen, qualitative, measurement, gravimetric, dissolution, disintegration, weight variation, titration, HPLC assay, ICP, UV). Run → PASS.
- [ ] **Step 2: Failing tests:** `toTestPayload equals each fixture`; `step validation messages equal the old save() messages`; backend `WithSteps_CreatesTestStagesSteps_Atomically`; `WithSteps_InvalidStep_NothingCreated_FieldErrorNamesTheStep`; `WithSteps_ResultEqualsCreatingTestThenStepsSeparately`; `previewModel orders steps and lists media and windows from the unsaved model`; `AnalystPreview shows "Preview — configuration only" and has no input controls`; `"New from existing" pre-fills everything except code and display name, and creating it leaves the source unchanged`; `edit with open test orders asks for a reason`.
- [ ] **Step 3–4:** Implement → PASS, 0 skipped; lint, `tsc -b`, `debt:check`.
- [ ] **Step 5:** Commit `refactor(config): test master wizard with analyst preview`.

### Task 7: Equipment wizard and copy

**Files:** Create `equipmentWizard/*` (wizard, `equipmentFormModel.ts`, three steps); Modify `EquipmentPage.tsx` (tabs L600–602 and list stay; dialog L1181+ replaced), `EquipmentConfigurationService.ts` (L232, L237: typed `CreateEquipmentRequest` / `UpdateEquipmentRequest` instead of `any`); Create `EquipmentPage.test.tsx` (none exists)

- [ ] **Step 1:** Characterisation against the old dialog for each `EquipmentType` with type-specific fields (incubator with set point, autoclave, HPLC with CDS software and connection settings, balance): captured `createEquipment` / `updateEquipment` payloads. Run → PASS.
- [ ] **Step 2: Failing tests:** `payloads equal fixtures`; `type step shows only that type's fields`; `validation messages equal handleSaveEquipment L479–495`; `section required when the user has several`; `"New from existing" pre-fills type and settings; code, calibration due date and documents are empty`; `edit while an incubation is in progress asks for a reason`; `set-point and autoclave-program actions on the page are unchanged`.
- [ ] **Step 3–4:** Implement → PASS; `debt:update` (removed `any`). **Step 5:** Commit `refactor(config): equipment wizard`.

### Task 8: Add Material wizard

**Files:** Create `materialWizard/*`; Modify `AddMaterialDialog.tsx` callers; keep `AddMaterialDialog.test.tsx` cases by porting

- [ ] **Step 1:** Characterisation: `MaterialService.create` / `update` payloads (`SaveMaterialPayload`) for a new lot, an edit, and each material kind with kind-specific fields. Run → PASS.
- [ ] **Step 2: Failing tests:** `payloads equal fixtures`; `validation messages equal handleSave L558–630`; `Review shows quantity with unit and dates with time zone where they are timestamps`; `document step keeps the existing upload behaviour`; `B1 "Print labels" offer still appears after create when the feature is present`.
- [ ] **Step 3–4:** Implement → PASS. **Step 5:** Commit `refactor(inventory): add-material wizard`.

### Task 9: Final verification and documents

- [ ] **Step 1:** Backend Release build; full suite without and with PostgreSQL; exact counts; `has-pending-model-changes` → none.
- [ ] **Step 2:** Frontend lint, `tsc -b`, test, build, `debt:check`. Report the line counts of the four original files and of the largest new step file.
- [ ] **Step 3:** Run the app: create an item from an existing one, copy specifications, create a test from an existing one and check its preview, edit a specification with an open sample (reason asked) and without (not asked), delete an unused specification, delete a used one (retired), reinstate it. Keyboard-only pass on one wizard. Report what was and was not exercised.
- [ ] **Step 4:** `documents/` requirements; note for guide chapters 31 and 32 (listed for the guide owner, not edited here if the guide is under separate control); SOP note on change reasons and retirement. Roadmap links.
- [ ] **Step 5:** Commit `docs(config): requirements, roadmap links`.

## Cross-plan corrections (whole-programme review, 2026-11-11)

These corrections come from reviewing all plans together against the code. Where they differ from the text above, they win. Shared names are defined once in `docs/superpowers/plans/2026-11-11-build-order.md` (section "Programme conventions").

1. Prerequisites (were undeclared): C2 Tasks 3–4 (`useBenchDraft`) and C4 Task 3 (`FieldError`, `FieldValidationException`).
2. Task 2: the wizard draft uses an explicit allow-list of the wizard's form fields.
3. Unfreeze of a test also requires a reason (same rule as freeze).
4. The test file `SpecificationParameterDialog.rs.test.tsx` is named `SpecificationParameterDialog.retired.test.tsx`.

## Review corrections (independent review, 2026-11-11)

From the independent review of the build order. These win over the text above and over the earlier corrections section where they differ.

1. Before Task 3, read every result-entry path and record whether a missing specification is treated as a pass anywhere; report each case to the user as a finding (spec risk 4) rather than deciding it.
