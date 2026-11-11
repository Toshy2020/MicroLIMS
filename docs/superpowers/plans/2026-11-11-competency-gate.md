# Analyst Competency Gate (D4) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Signed, expiring analyst qualifications per test plus current-SOP acknowledgement, enforced on the server at assignment and at every recording point, with a per-laboratory switch and a signed per-test-order override.

**Architecture:** A `CompetencyGuard` service answers "may user U perform test order T now, and if not why" from `AnalystQualification`, the item's SOP (via `ItemStmService` logic) and `DocumentTrainingAssignment`, plus `CompetencyOverride`. It is called next to every write-access `EnsureTestOrderAccessAsync` call in the recorders, in `AnalystAssignmentRules.EnsureAssignableAsync`, at the automatic-assignment sites and in the batch paths. An architecture test keeps new recorders from skipping it.

**Tech Stack:** .NET 10, EF Core 10, PostgreSQL, xUnit `[PostgresFact]`; React 19, TypeScript, MUI, Vitest.

**Spec:** `docs/superpowers/specs/2026-11-11-competency-gate-design.md`

## Global Constraints

- `CLAUDE.md` binds every task (anti-fake tests, triad, `[PostgresFact]` must run, `TimeProvider` / `ILabClock`, `CancellationToken`, one migration per schema change + `has-pending-model-changes`, no new `any`, `userEvent`, WCAG 2.2 AA).
- Permission codes exactly: `Competency.View`, `Competency.Manage`, `Competency.Override`. `Competency.Manage` and `Competency.Override` join `GxpExecutionAndApprovalCodes`.
- `SignatureMeaning` values appended in order: `AnalystQualified`, `AnalystQualificationWithdrawn`, `CompetencyOverride`, `CompetencyGateChanged`.
- Expiry = `QualifiedOn.AddMonths(TestDefinition.RequalificationMonths)` (calendar months, `DateOnly`); expired when `LabToday > ExpiresOn`.
- SOP acknowledged = a `DocumentTrainingAssignment` for the user and the revision with `Status` in {`Acknowledged`, `CompletedPassed`}. Grace = the user acknowledged the immediately superseded revision **and** holds an open assignment (`Assigned` or `Reading`) for the current revision with `DueDateUtc >= now`.
- Refusal messages (exact templates): `"You are not qualified for {test}."`, `"Your qualification for {test} expired on {date}."`, `"Your qualification for {test} was withdrawn on {date}."`, `"You have not acknowledged {documentCode} revision {revision}."`; for assignment the subject is the assignee's name instead of "You". Dates in lab time, `d MMM yyyy`.
- Recording is never undone; no change to recorded results.
- New entities stay under automatic audit capture.

## Review Focus

1. A recording entry point added later skips the guard → `Every_write_access_check_in_workflows_is_paired_with_competency_guard` architecture test (Task 3).
2. Auto-assign at water / EM / after-cleaning start assigns an unqualified user → `Auto_assign_refused_for_unqualified_acting_user` per engine (Task 4).
3. Expiry computed in UTC instead of lab date at midnight boundary → `Expiry_uses_lab_date` (Task 1).
4. Override reused for another test order or another analyst → `Override_covers_one_analyst_one_test_order_only` (Task 5).
5. Grace period continues after the new revision's due date passes → `Grace_ends_at_due_date` (Task 2).

## File Structure

| File | Responsibility |
|---|---|
| `Domain/Entities/AnalystQualification.cs` | `AnalystQualification`, `CompetencyOverride`, `CompetencySectionSetting` |
| `Domain/Entities/TestDefinition.cs` (modify) | `RequalificationMonths?` |
| `Persistence/Configurations/Competency*.cs`, migration | Mapping, triggers |
| `Application/Services/Competency/CompetencyGuard.cs` (+ `ICompetencyGuard`) | The decision |
| `Application/Services/Competency/QualificationService.cs`, `CompetencyOverrideService.cs`, `CompetencySettingsService.cs`, `CompetencyMatrixService.cs`, `CompetencyReminderService.cs` | Records, override, switch, matrix, reminders |
| `Application/Services/ItemSopResolver.cs` | Extracted SOP resolution from `ItemStmService.GetForItemAsync` (`ItemStmService.cs:14`) |
| Recorders and assignment sites listed in Task 3–4 | Guard calls |
| `API/Controllers/CompetencyController.cs` | Endpoints |
| `frontend/src/modules/competency/*`, `services/competencyService.ts` | Screens |

---

### Task 1: Records, intervals, permissions

**Files:** entities, `TestDefinition.cs`, migration `<ts>_AddAnalystCompetency.cs` (+ permission grants via `RoleGrantSql.Grant`: View → Section Head, Reviewer, QC Manager, QA, Analyst; Manage → Section Head, QC Manager; Override → Section Head), `PermissionConstants.cs`, `SegregationOfDutiesRules.cs`, `DbSeeder.cs`, `SignatureMeaning.cs`, `ReportVocabulary.cs`, `QualificationService.cs`; Test `IntegrationTests/AnalystQualificationPostgresTests.cs`

**Interfaces:**
- `AnalystQualification { Id; UserId; TestDefinitionId; Basis (QualificationBasis { ObservedPractice, ProficiencySample, PreviousQualification, Other }); Evidence; EvidenceSampleId?; QualifiedOn (DateOnly); ExpiresOn (DateOnly?); SignedByUserId; SignatureId; WithdrawnOn?; WithdrawnReason?; WithdrawnByUserId?; WithdrawnSignatureId? }` — trigger: reject DELETE; reject UPDATE except setting the four withdrawal columns once from null.
- `QualifyAsync(QualifyRequest { UserId, TestDefinitionId, Basis, Evidence, EvidenceSampleId?, QualifiedOn, Password }, int actingUserId, ct)` — `Competency.Manage`; configure scope on the test's section; user active and member of the section; actor ≠ user; `QualifiedOn` ≤ lab today; signs `AnalystQualified`.
- `WithdrawAsync(int qualificationId, string reason, string password, int actingUserId, ct)`.
- `SetIntervalAsync` is part of the test update (Test Master field `RequalificationMonths`, 1–120 or null) — existing update path, audited automatically.
- Attachments: reuse the D2 attachment pattern (`QualityAttachment`-style table `QualificationAttachment`).

- [ ] **Step 1:** Failing tests: qualify persists with expiry from interval, signature and audit; null interval → no expiry; `Expiry_uses_lab_date`; self-qualification refused; non-member refused; other-section Section Head refused; withdraw once; trigger rejects edits and deletes; grants per role.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(competency): analyst qualification records`.

### Task 2: The guard

**Files:** `ItemSopResolver.cs` (extract; `ItemStmService` calls it — behaviour unchanged, characterised first), `CompetencyGuard.cs`, `CompetencySectionSetting`; Test `IntegrationTests/CompetencyGuardPostgresTests.cs`

**Interfaces:**
- `Task<SopResolution> ResolveAsync(int itemId, CancellationToken ct)` → `{ Status (Effective|NoSopNumber|NotFound|NoEffectiveRevision), DocumentMasterId?, DocumentCode?, RevisionId?, RevisionNumber? }`.
- `Task<CompetencyDecision> EvaluateAsync(int userId, int testOrderId, CancellationToken ct = default)` → `{ Allowed, Reasons: string[], SopNotChecked: string?, ViaOverrideId? }`. Order: gate off for the section → allowed; active override for (user, test order) → allowed; qualification (latest non-withdrawn for user + test) missing / expired / withdrawn → reason; sample has item → SOP: `Effective` → acknowledged or grace, else reason; other statuses → `SopNotChecked` text.
- `Task EnsureAllowedAsync(int userId, int testOrderId, CancellationToken ct = default)` → throws `BusinessRuleException(string.Join(" ", reasons))`.
- `Task<IReadOnlyDictionary<int, CompetencyDecision>> EvaluateManyAsync(int userId, IReadOnlyCollection<int> testOrderIds, ct)` and `EvaluateAssigneeAsync(int assigneeId, IReadOnlyCollection<int> testOrderIds, ct)` (assignee wording).

- [ ] **Step 1:** Failing tests: each decision branch; `Grace_allows_until_due_date`, `Grace_ends_at_due_date`; acknowledged only an older (two back) revision → refused; sample without item → qualification only; unresolvable SOP → allowed with text; gate off → allowed with no queries of qualification (assert result only); characterisation of `ItemStmService` unchanged.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(competency): competency guard`.

### Task 3: Recording points

**Files:** `Workflows/TestWorkflow/TestWorkflowSupport.cs:602`, `DissolutionRecorder.cs:248`, `DisintegrationRecorder.cs:256`, `LocationResultRecorder.cs:20`, `OutsourcedRecorder.cs:46`, `IcpMethodAssayRecorder.cs:25`, `HplcMethodAssayRecorder.cs:26`, `WeightVariationRecorder.cs:291`; batch paths `TestWorkflowEngine.RecordBatchResultsAsync` / `RecordBatchPathogenResultsAsync` (lines 124, 130) and `GroupedTestActionService` (batch actions, near L424–477); preparation recording (`SamplePreparationService`); Test `IntegrationTests/CompetencyRecordingPostgresTests.cs`, `ArchitectureTests/CompetencyGuardCoverageTests.cs`

**Interfaces:** after each write-access `EnsureTestOrderAccessAsync(userId, testOrderId…)`, call `_competency.EnsureAllowedAsync(userId, testOrderId, ct)`. Batch: `EvaluateManyAsync`; refused orders go to the existing skipped list with the reason. `ParameterResult` / audit: when `SopNotChecked` is set, include it in the audit entry's reason field of the recording (no schema change).

- [ ] **Step 1:** Failing tests — one per entry point (measurement, gravimetric, qualitative, count reading, pathogen step, dissolution, disintegration, weight variation, location result, outsourced, HPLC method assay, ICP method assay, UV, titration, preparation): unqualified user refused, nothing persisted; qualified user persists. Batch: mixed list → refused ones skipped with reason, others recorded. Architecture test: in `Application/Workflows` and `Application/Services`, every method that calls `EnsureTestOrderAccessAsync` / `EnsureTestOrdersAccessAsync` with default (write) access also references `ICompetencyGuard` (Roslyn syntax walk or source scan; read-access calls excluded).
- [ ] **Step 2:** FAIL → implement → PASS; all existing workflow suites pass with the gate off (default). Commit `feat(competency): enforce at every recording point`.

### Task 4: Assignment points

**Files:** `AnalystAssignmentRules.EnsureAssignableAsync` (`AnalystAssignmentRules.cs:33`, add `IReadOnlyCollection<int> testOrderIds` and `ICompetencyGuard`), callers `SampleAssignmentService.cs:58`, `:89`, `SampleApprovalService.cs:278`, D1 retest-plan analysts; auto-assign sites `EMWorkflowEngine.cs:134`, `WaterWorkflowEngine.cs:225`, `AfterCleaningWorkflowEngine.cs:302`, `:323`, `SamplePreparationService.cs:243`; `SampleCorrectionService.cs:475`, `:569` (reassignment on correction); `GET` candidates endpoint for the dialog returns per-candidate decision; Test `IntegrationTests/CompetencyAssignmentPostgresTests.cs`

- [ ] **Step 1:** Failing tests: manual single and bulk assignment refused for unqualified assignee with the assignee wording; allowed when qualified; `Auto_assign_refused_for_unqualified_acting_user` per engine (and nothing persisted); correction reassignment checked; candidates list orders qualified first with reasons for the rest; gate off → unchanged behaviour.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(competency): enforce at assignment`.

### Task 5: Override and separation

**Files:** `CompetencyOverrideService.cs`, `SegregationOfDutiesGuard.cs` (new `DidUserOverrideCompetencyAsync(int userId, int sampleId)`), `SampleReviewService` and `SampleApprovalService` SoD checks; Test `IntegrationTests/CompetencyOverridePostgresTests.cs`

**Interfaces:** `CompetencyOverride { Id; TestOrderId; AnalystUserId; Reason; GrantedByUserId; GrantedAtUtc; SignatureId; EndedAtUtc? }` (append-only except `EndedAtUtc` once). `GrantAsync(testOrderId, analystUserId, reason, password, actingUserId)` — `Competency.Override`, section scope, actor ≠ analyst, test order not completed; signs `CompetencyOverride`. Ends automatically when the order reaches `Reviewed` or is cancelled. Review / approval by the granter refused: `"You overrode competency for a test on this sample, so you cannot review it."` / `"…cannot approve it."`

- [ ] **Step 1:** Failing tests: `Override_covers_one_analyst_one_test_order_only`; self-override refused; granter cannot review or approve; override ends at review; review DTO carries the override (who, reason, when); audit and signature.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(competency): signed override`.

### Task 6: Switch, impact preview, reminders, matrix

**Files:** `CompetencySettingsService.cs`, `CompetencyMatrixService.cs`, `CompetencyReminderService.cs` (query used by My Work and C5 notifications; no new worker if C5's generation worker exists, else a daily check added to the existing notification path), `CompetencyController.cs`; Test `IntegrationTests/CompetencySettingsAndMatrixPostgresTests.cs`

**Interfaces:** `CompetencySectionSetting : IVersionedEntity { SectionId; GateEnabled (false); ReminderDaysBeforeExpiry? }`. `GetImpactAsync(sectionId)` → open test orders per assigned analyst that would be refused, with reasons. `SetGateAsync(sectionId, enabled, reason, password, expectedVersion)` — `Competency.Manage`, signs `CompetencyGateChanged`. `GetMatrixAsync(sectionId, filters)` → analysts × tests with cell status and SOP status; `GET my` for the analyst's own. Export logged.

- [ ] **Step 1:** Failing tests: impact numbers for a fixed data set; switch needs reason and signature, audited; matrix cell statuses (qualified, expiring within reminder days, expired, withdrawn, none); analyst sees only own; reminder query by lab date.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(competency): laboratory switch, matrix and reminders`.

### Task 7: Frontend

**Files:** `services/competencyService.ts`; `modules/competency/QualificationMatrixPage.tsx`, `QualifyDialog.tsx`, `WithdrawDialog.tsx`, `MyQualificationsPage.tsx`, `GateSettingsCard.tsx` (impact preview before confirm), `OverrideDialog.tsx`; `modules/receiving/dialogs/AssignAnalystDialog.tsx` (qualified first, others disabled with reason text); Test Master interval field; refusal reasons beside actions in `modules/testingWorkspace/TestWorkflowDialog.tsx` and panels (server message shown as text); review screen override notice; tests beside each

- [ ] **Step 1:** Failing tests (`userEvent`, `services/` mocked): matrix states and filters; qualify sends basis, evidence, date, password; withdraw needs reason; assignment dialog ordering and disabled reasons; recording refusal text shown; override dialog and review notice; impact preview shown before the switch dialog confirms; analyst page shows expiry and SOPs to read.
- [ ] **Step 2:** FAIL → implement → lint, types, tests, debt check, build PASS. Commit `feat(ui): competency matrix, dialogs and refusals`.

### Task 8: Documents and verification

- [ ] **Step 1:** `BusinessRules.md` section on competency. If A1–A3 exist: URS / FS entries (areas ADM, MIC, PHY), risk rows and tags, manual and role-guide tasks, SOP template note (training and qualification). Otherwise list as follow-ups.
- [ ] **Step 2:** Full verification per `CLAUDE.md` section 2 with PostgreSQL; exact counts; `has-pending-model-changes`; frontend checks.
- [ ] **Step 3:** Report to the user: gate ships off in every laboratory; how to enter qualifications and switch it on.

## Cross-plan corrections (whole-programme review, 2026-11-11)

These corrections come from reviewing all plans together against the code. Where they differ from the text above, they win. Shared names are defined once in `docs/superpowers/plans/2026-11-11-build-order.md` (section "Programme conventions").

1. **Recording points: the scope check for incubation and pathogen recording is in the API (`TestWorkflowController`), not in `IncubationStepRecorder`, `PathogenConfirmationRecorder` or `TestWorkflowEngine`.** The guard must therefore be called in every public recording method of those three classes as well. The architecture test is renamed `RecordingPointCoverageTests` (owned by this plan) and checks an explicit list of recorder classes and methods, not only methods that call `EnsureTestOrderAccessAsync`.
2. `SopResolution.Status` values map to D5's `SopCaptureStatus` as: Effective → Captured, NoSopNumber → NoSopNumber, NotFound → NotFound, NoEffectiveRevision → NoEffectiveRevision.
3. `AnalystAssignmentRules.EnsureAssignableAsync` gains the new parameters with defaults so D1's existing calls compile; D1's retest-plan path passes the test order ids.

## Review corrections (independent review, 2026-11-11)

From the independent review of the build order. These win over the text above and over the earlier corrections section where they differ.

1. **Add the pathogen session path.** `PathogenSessionService` writes results directly (`SavePrimaryObservationsAsync` ~L731, `SaveBatchConfirmatoryPlateReadingsAsync` ~L995, `SaveResultMatrixAsync` ~L1129, `CompleteSessionAsync` ~L1220), with scope checked per sample in `PathogenSessionController`. Each of these calls `EvaluateManyAsync` for the affected test orders; refused orders are skipped with the reason (batch behaviour). Split `PathogenSessionService.cs` (1,415 lines, near the 1,500-line limit of `SourceFileSizeTests`) before adding code.
2. `RecordingPointCoverageTests` is a rule, not a list: it fails for any public method in `Application/Workflows` or `Application/Services` that adds or updates a result entity (`ParameterResult`, `CountTestReading`, `WorkflowStepResult`, `PathogenObservation`, `ConfirmatoryPlateObservation`, `LocationPathogenObservation`, `SampleLocation` results, `TestAnalysis`, `Result`) without calling `ICompetencyGuard`, unless the method is on a named exemption list with a reason.
3. Call the guard in the recorders only, not again in `TestWorkflowEngine` (its batch callers in `GroupedTestActionService` ~L589-673 must skip refused orders, not throw).

**Translation (build order 2.7):** if F4 Task 4 is merged when this package is built, every new screen uses translation keys and every new refusal carries a message code.
