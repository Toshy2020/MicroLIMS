# Microbial Identification (E4) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Isolates with identification attempts (VITEK 2, manual kit, Gram and simple tests), organism mapping and confidence thresholds, required isolates per source with an open-identification list (no approval gate), pathogen consistency checks, and a controlled organism list.

**Architecture:** `Isolate` (polymorphic source by typed nullable FKs), `IdentificationAttempt` with per-method detail, `OrganismNameMapping`, `IdentificationThreshold`; an `IdentificationRequirementService` used by the pathogen biochemical recorder and the open-identification list; VITEK 2 files arrive through E1's agent and export-format path as pending instrument data.

**Tech Stack:** .NET 10, EF Core 10, PostgreSQL, xUnit `[PostgresFact]`; React 19, TypeScript, Vitest.

**Spec:** `docs/superpowers/specs/2026-11-11-microbial-identification-design.md`

## Global Constraints

- `CLAUDE.md` binds every task. **No organism name literal in Application, API or Domain code** (`OrganismLiteralTests`); tests use generated names. Comparisons use organism ids only.
- No change to count, pathogen or water evaluation. "Alert or action level" = the existing `ResultStatus` values `AlertLimitExceeded`, `ActionLimitExceeded` (and `OutOfSpecification`) of the reading.
- Numbers `ISO-{yyyy}-{seq:D5}` (lab clock year, advisory-lock generation as in D1).
- Thresholds have no defaults; empty = always analyst choice + Section Head confirmation.
- Identification attempts and isolates append-only (void with reason; attempts never edited).
- VITEK 2 import requires E1 Tasks 1, 7, 8. Without them, VITEK 2 results are typed with a reason.
- Permission code: `Identification.Configure` (Section Head).

## Review Focus

1. A reading corrected from OOS to within limits after an isolate was required → requirement disappears but the isolate and identification stay on record → `Corrected_reading_keeps_isolate_history` (Task 4).
2. A voided isolate still satisfies the requirement → `Voided_isolate_does_not_count` (Task 4).
3. Final identity changes after the section is approved → refused without the post-decision correction gate → `Identity_change_after_approval_refused` (Task 3).
4. Same VITEK 2 file adopted onto two isolates → `Vitek_result_adopted_once` (Task 5).
5. Analyst creates an organism through the old endpoint directly → `Organism_create_requires_master_data_permission` (Task 1).

## File Structure

| File | Responsibility |
|---|---|
| `Domain/Entities/Identification/*.cs` | `Isolate`, `IdentificationAttempt`, `KitIdentification`, `VitekIdentification`, `SimpleTestResult`, `OrganismNameMapping`, `IdentificationThreshold` |
| `Domain/Entities/Organism.cs` (modify) | `Genus?` |
| `Application/Services/Identification/*.cs` | `IsolateService`, `IdentificationService`, `OrganismMappingService`, `IdentificationRequirementService`, `VitekImportService` |
| `API/Controllers/IdentificationController.cs` | Endpoints |
| `frontend/src/modules/identification/*`, `components/OrganismPicker.tsx` (modify) | Screens |

---

### Task 1: Organism list control

**Files:** `OrganismMasterDataController.cs` (POST/PUT/DELETE already require `MasterData.Manage` — verify with a test, and remove the frontend path that lets others call create), `frontend/src/components/OrganismPicker.tsx:48-56` (show "Add" only when the user holds `MasterData.Manage`; otherwise the "Ask your Section Head" text), `Organism.Genus` + migration; Tests `IntegrationTests/OrganismListControlPostgresTests.cs`, `components/OrganismPicker.test.tsx`

- [ ] **Step 1:** Failing tests: `Organism_create_requires_master_data_permission` (analyst 403, nothing created); genus saved and audited; picker shows no "Add" for an analyst and shows it for a Section Head.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(identification): controlled organism list and genus`.

### Task 2: Isolates and manual identifications

**Interfaces:**
- `Isolate { Id; Number; TestOrderId?; WorkflowStepResultId?; SampleLocationId?; LocationReadingRef?; ParentIsolateId?; Medium?; PlateRef?; ColonyDescription; PickedByUserId; PickedAtUtc; VoidedAtUtc?; VoidReason?; FinalAttemptId?; Version }` — exactly one source set (check constraint). Read the EM and water result entities (`SampleLocation`, location result recorders) to pick the precise FK for "reading"; do not create a parallel reading record.
- `IdentificationAttempt { Id; IsolateId; Method (Vitek2|ManualKit|SimpleTests); ReportedName?; OrganismId?; GenusOnly?; Confidence? (decimal); ConfidenceText?; Status (Pending|Proposed|AwaitingConfirmation|Accepted|Rejected|HeldUnmapped); AnalystChoice? (Accept|Repeat|OtherMethod|GenusOnly|NotIdentified); ChoiceReason?; ConfirmedByUserId?; ConfirmedAtUtc?; RecordedByUserId; RecordedAtUtc; RawSha256?; IngestVersionId? }`; detail rows `KitIdentification { KitName; KitLot; ProfileNumber; PercentId?; KitComment? }`, `VitekIdentification { CardType; Bionumber?; Probability?; }`, `SimpleTestResult { TestType (PhenotypicTestType); Result (text); Morphology? }`.
- `IsolateService.CreateAsync(CreateIsolateRequest, userId)`, `SubCultureAsync`, `VoidAsync`; `IdentificationService.RecordKitAsync`, `RecordSimpleTestsAsync`, `RecordVitekManualAsync(… , reason)` — each calls the D4 competency guard and section write scope on the source test order.

- [ ] **Step 1:** Failing tests: isolate per source with one-source constraint; number unique under concurrency; sub-culture; void; each method's fields persisted with audit; competency and scope refusals; attempt update and delete rejected by trigger.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(identification): isolates and manual identifications`.

### Task 3: Mapping, thresholds, acceptance, final identity

**Interfaces:** `OrganismNameMapping { Method; ReportedName (case-insensitive unique per method); OrganismId?; GenusOnly? }`; `IdentificationThreshold { Method; CardType?; MinConfidence }` (`Identification.Configure`). On record: map name → unmapped → `HeldUnmapped` + Section Head task; mapped and confidence ≥ threshold → `Proposed`; else `AwaitingConfirmation`. `AcceptAsync(attemptId, userId)` (analyst, `Proposed` only) → `Accepted`, sets `Isolate.FinalAttemptId`. `ChooseAsync(attemptId, choice, reason, userId)` → `AwaitingConfirmation`; `ConfirmAsync(attemptId, password, sectionHeadId)` (signature `IdentificationConfirmed`, appended meaning) → `Accepted` or `Rejected`. `MapAsync(method, reportedName, organismId | genus, userId)` releases held attempts. Final identity change after the source section is approved → refused unless the post-decision correction gate (E2/E3) approves.

- [ ] **Step 1:** Failing tests: unmapped held and released after mapping; above threshold proposed and accepted; below threshold and no-threshold paths need Section Head signature; analyst cannot confirm own choice; new accepted attempt replaces final identity, history kept; `Identity_change_after_approval_refused`.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(identification): mapping, confidence and final identity`.

### Task 4: Requirements, open-identification list, pathogen consistency

**Interfaces:** `IdentificationRequirementService.GetOpenAsync(int sampleId, int sectionId, ct)` → list of `{ source, requiredCount, identifiedCount }`: pathogen tests: identification is recorded **inside** the existing biochemical step — `SubmitBiochemicalAsync` (`PathogenConfirmationRecorder.cs:653`) gains `IReadOnlyList<int> isolateIds` (isolates created on that `WorkflowStepResult`), refuses when none has a final identity, and fills `BiochemicalResultText` with a readable summary when the analyst left it empty; no approval gate for pathogen tests (review already requires completed steps); EM / water: each plate whose reading has `ResultStatus` `ActionLimitExceeded` or `OutOfSpecification`, or that has a `SampleLocationTrendFlag` row from F1 (the old `RoomMonitoring.IsOutOfTrend` is never written by code and is not used; without F1, OOS only), needs exactly one isolate with a final identity; `AlertLimitExceeded` alone needs none. **No approval gate:** `SampleApprovalService` is not changed. `ListOpenAsync(sectionIds, ct)` feeds My Work (task type `"Identification"`) and an open-identification register. Consistency in `SubmitBiochemicalAsync`: any isolate of the step with accepted `OrganismId == step.TargetOrganismId` and `organismDetected == false` → refuse; `organismDetected == true` with all accepted isolates ≠ target → reason required.

- [ ] **Step 1:** Failing tests: each requirement source; `Alert_only_reading_needs_no_identification`; `Oos_and_oot_plate_need_one_identification`; `Voided_isolate_does_not_count`; `Approval_not_blocked_by_open_identification`; open list and My Work entries; biochemical submit refused without isolate; consistency rules with generated organisms; architecture `OrganismLiteralTests` still green.
- [ ] **Step 2:** FAIL → implement → PASS; pathogen and EM suites pass. Commit `feat(identification): requirements, open list and pathogen consistency`.

### Task 5: VITEK 2 import

**Interfaces:** an E1 export format of kind ExportCsv (or the LIS text layout, from the user's sample files) mapping: isolate number, organism name, probability, confidence text, card type, bionumber. `VitekImportService` turns parsed rows into `VitekIdentification` attempts on the isolate whose `Number` matches (case-insensitive, trimmed); unmatched rows stay pending for manual matching; one ingest row adopted once (unique on `IngestVersionId` + row index).

- [ ] **Step 1:** Failing tests (with synthetic files built from the user's sample layout — **blocked until samples arrive**; write the fixtures from them, never from memory): matched row creates attempt with raw hash; unmatched listed; `Vitek_result_adopted_once`; mapping and threshold applied as in Task 3.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(identification): VITEK 2 import`.

### Task 6: Screens, reports, documents

**Files:** `modules/identification/IsolatesPanel.tsx` (embedded in `frontend/src/modules/testingWorkspace/pathogenSteps/BiochemicalTestPanel.tsx` with the method choice VITEK 2 / manual kit / simple tests, and in EM/water result screens), `IdentificationDialog.tsx` (method forms), `PendingVitekPanel.tsx`, `SectionHeadQueuesPage.tsx` (unmapped names, confirmations, thresholds), room and sampling-point configuration fields; report mappers (pathogen tests' identified organisms; EM/water isolates per location); tests; documents (BusinessRules, A1 entries, manual chapter, SOP note).

- [ ] **Step 1:** Failing tests (`userEvent`, `services/` mocked): biochemical step shows the three methods and cannot be submitted without an identification; pending VITEK 2 result shown inside the step; add isolate, record kit and simple tests, accept proposed, choose below threshold, Section Head confirm, unmapped queue, open-identification list, biochemical submit refusal text.
- [ ] **Step 2:** FAIL → implement → full checks PASS. Commit `feat(ui): isolates and identification`.
- [ ] **Step 3:** Full verification per `CLAUDE.md`; report counts and that Task 5 waits for VITEK 2 sample files if not provided.
