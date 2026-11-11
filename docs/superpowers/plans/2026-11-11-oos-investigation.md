# Structured OOS / OOT Investigation (D1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Open a structured, signed investigation when a reviewer confirms a failing result, block approval and certificates until it is concluded, and control retests through a signed plan.

**Architecture:** A new `OosInvestigation` aggregate (results, append-only entries, retest plans, deviation requests) with per-laboratory templates. It is opened inside `SampleReviewService.CompleteReviewCoreAsync`, gated in `SampleApprovalService.DecideCoreAsync` and the certificate paths of `SampleSummaryService`, and it reuses the existing linked-sample retest mechanism and `OosInvestigationDocument` attachments (keyed by the investigation number, which is the `OosGroupCode`).

**Tech Stack:** .NET 10, EF Core 10, PostgreSQL, xUnit `[PostgresFact]`; React 19, TypeScript, MUI, Vitest.

**Spec:** `docs/superpowers/specs/2026-11-11-oos-investigation-design.md`

## Global Constraints

- `CLAUDE.md` binds every task (anti-fake tests, state + audit + negative triad, `[PostgresFact]` must run, `TimeProvider` / `ILabClock`, `CancellationToken`, one migration per schema change + `has-pending-model-changes`, no new `any`, `userEvent`, WCAG 2.2 AA, design tokens).
- No change to how a result is calculated or compared with limits. "Fails" comes only from `ResultConformanceRules` (`backend/MicroLIMS.Application/Helpers/ResultConformance.cs`).
- Original results, readings and test orders are never updated or deleted by this feature. Invalidating is a flag on `OosInvestigationResult`, not a change to the result.
- No test code, organism name or laboratory name literal in code. Laboratory = `DocumentSection` (`SectionId`).
- New entities stay under automatic audit capture (no `AuditCaptureExcludedTypes` change).
- New `SignatureMeaning` values are appended after the last value that exists at implementation time, in this order: `OosPhaseOneConcluded`, `OosRetestPlanApproved`, `OosInvestigationConcluded`, `OosInvestigationReopened`, `ExcursionReviewClosed`, `NewSampleRequested`. Never renumber. Each gets a `ReportVocabulary` label.
- New permission codes exactly: `Oos.View`, `Oos.Contribute`, `Oos.RequestDeviation`. `Oos.Contribute` is added to `SegregationOfDutiesRules.GxpExecutionAndApprovalCodes`.
- Text limits: statements, descriptions, reasons, judging rule 1–4000 characters; checklist comment ≤ 1000.
- Notifications are sent after the transaction commits, never inside it.
- Exact refusal messages (spec section 6): `"OOS investigation {number} is open."`, `"A confirmed out-of-specification result cannot be approved."`, `"A retest needs an approved retest plan."`, `"You performed a test in this investigation, so you cannot sign it."`
- Default cause categories seeded: Analyst error, Equipment, Method, Sample, Media or reagent, Environment, No cause found (`IsNoCauseFound = true`, cannot be deactivated or renamed).
- Checklist templates ship empty. Phase I cannot be assessed while the section's Phase I template has no active item: `"No Phase I checklist is configured for this laboratory."`

## Review Focus

1. Two reviewers complete reviews at the same moment and get the same investigation number → `Concurrent_openings_get_distinct_numbers` (Task 2).
2. A retest sample that also fails opens a second investigation instead of joining the first → `Failing_retest_result_joins_the_origin_investigation` (Task 6).
3. A sample already under approval with a failing result at upgrade slips through → `Section_with_failing_order_and_no_investigation_cannot_be_approved` (Task 3).
4. A certificate reached through the Word or archive path while open → `Every_certificate_path_is_blocked_while_open` (Task 3), one assertion per path.
5. "OOS invalidated" concluded while a planned retest is unfinished or failed-and-unreviewed → `Conclusion_refused_until_every_planned_retest_sample_is_reviewed` (Task 7).

## File Structure

| File | Responsibility |
|---|---|
| `Domain/Entities/OosInvestigation.cs` | Aggregate root + `OosInvestigationResult`, `OosInvestigationEntry`, `OosRetestPlan`, `OosRetestPlanSample`, `OosDeviationRequest`, `OosInvestigationReopening` |
| `Domain/Entities/OosInvestigationTemplate.cs` | `OosInvestigationTemplate`, `OosChecklistItem`, `OosCauseCategory` |
| `Domain/Enums/OosEnums.cs` | Enums below |
| `Persistence/Configurations/OosInvestigationConfiguration.cs` | Mapping, indexes |
| `Application/Services/Oos/TestOrderOutcomeService.cs` | Shared "does this order fail" reading |
| `Application/Services/Oos/OosInvestigationService.cs` | Open, Phase I, Phase II, conclude, reopen |
| `Application/Services/Oos/OosRetestPlanService.cs` | Plan, sign, amend, create retest samples |
| `Application/Services/Oos/OosGate.cs` | Approval / certificate checks |
| `Application/Services/Oos/OosTemplateService.cs` | Templates, categories |
| `Application/Services/Oos/OosRecipientResolver.cs`, `OosNotifier.cs` | Who is told |
| `Application/Services/Oos/OosTrendService.cs`, `OosInvestigationReportBuilder.cs` | Trending, PDF |
| `Application/Services/RetestSampleFactory.cs` | Extracted from `SampleApprovalService.BuildRetestSample` and its callers |
| `API/Controllers/OosInvestigationsController.cs`, `OosTemplatesController.cs` | Endpoints |
| `frontend/src/modules/oosTracking/*`, `services/oosInvestigationService.ts` | Register, investigation page, templates |

Enums: `OosInvestigationKind { Oos, Excursion }`; `OosInvestigationStatus { PhaseOne, AwaitingRetest, PhaseTwo, Concluded }`; `OosPhaseOneAssessment { LaboratoryErrorConfirmed, NoLaboratoryErrorFound }`; `OosConclusion { Confirmed, Invalidated, ExcursionClosed }`; `OosEntryKind { AnalystStatement, ChecklistAnswer, OutsideReview, Hypothesis, HypothesisOutcome, Note }`; `OosChecklistAnswer { Yes, No, NotApplicable }`; `OosRetestSource { RetainedSample, NewSample }`; `OosDeviationRequestStatus { Open, Addressed }`; `TestOrderOutcome { Conforms, AlertLimitExceeded, ActionLimitExceeded, Fails }`.

---

### Task 1: Shared outcome reading

**Files:** Create `Application/Services/Oos/TestOrderOutcomeService.cs` (+ `ITestOrderOutcomeService`); Modify `SampleApprovalService.cs` (replace private `DetermineOwnResultConformanceAsync` L649 with a call); Test `IntegrationTests/TestOrderOutcomePostgresTests.cs`

**Interfaces:**
- `Task<TestOrderOutcome> GetOutcomeAsync(int testOrderId, CancellationToken ct = default)`; `Task<IReadOnlyDictionary<int, TestOrderOutcome>> GetOutcomesAsync(IReadOnlyCollection<int> testOrderIds, CancellationToken ct = default)`.
- Same precedence as today's private method (latest active `CountTestReading.Status` → latest `SampleLocation.Status` → latest `WorkflowStepResult.BiochemicalOrganismDetected` → `PathogenObservation.GrowthConforming` → last `Result` value → conforms). `Fails` exactly when today's method returns false. `ActionLimitExceeded` / `AlertLimitExceeded` only when the deciding reading's `ResultStatus` is that value.

- [ ] **Step 1:** Characterisation tests first, against the **current** private method through `DecideAsync` behaviour where needed, then the new service: one test per precedence branch, `Fails_matches_previous_conformance_for_each_branch`, `Action_and_alert_statuses_are_reported_but_conform`, `Order_without_results_conforms`.
- [ ] **Step 2:** FAIL → implement → PASS; existing approval tests still pass. Commit `refactor(oos): shared test order outcome service`.

### Task 2: Entities, schema, numbering, protection

**Files:** Create the Domain and Persistence files above; migration `<ts>_AddOosInvestigations.cs`; Modify `MicroLimsDbContext` (DbSets), `ReferenceNumberGenerator.GenerateOosCodeAsync`, `SignatureMeaning.cs`, `ReportVocabulary.cs`, `AppendOnlyTriggerCatalogPostgresTests.ProtectedTables`; Test `IntegrationTests/OosInvestigationSchemaPostgresTests.cs`

**Interfaces (fields):**
- `OosInvestigation : IVersionedEntity { Id; Number (unique); Kind; Status; SampleId; SectionId; OpenedAtUtc; OpenedByUserId; TemplateSnapshotJson; PhaseOneDueDate?; DueDate?; PhaseOneAssessment?; PhaseOneCauseCategoryId?; PhaseOneCauseDescription?; PhaseOneSignedAtUtc?; PhaseOneSignedByUserId?; PhaseOneSignatureId?; Conclusion?; RootCauseCategoryId?; RootCauseDescription?; ConclusionSummary?; ConcludedAtUtc?; ConcludedByUserId?; ConclusionSignatureId?; Version }`; unique filtered index: one non-concluded investigation per (`SampleId`, `SectionId`, `Kind`).
- `OosInvestigationResult { Id; InvestigationId; TestOrderId; OutcomeAtOpening; ResultSummaryAtOpening (text ≤ 500); AddedAtUtc; IsInvalidated; InvalidatedAtUtc? }`; unique (`InvestigationId`, `TestOrderId`).
- `OosInvestigationEntry { Id; InvestigationId; Phase (1|2); Kind; ChecklistItemKey?; Answer?; Text; ParentEntryId?; RecordedByUserId; RecordedAtUtc }`.
- `OosRetestPlan { Id; InvestigationId; Revision; Source; RetestCount; TestOrderIdsJson; AnalystUserIdsJson; JudgingRule; AmendmentReason?; SignedAtUtc?; SignedByUserId?; SignatureId?; SamplesCreatedAtUtc? }`; `OosRetestPlanSample { PlanId; SampleId }`.
- `OosDeviationRequest { Id; InvestigationId; Reason; RequestedByUserId; RequestedAtUtc; Status; AddressedComment?; AddressedByUserId?; AddressedAtUtc?; Version }`.
- `OosInvestigationReopening { Id; InvestigationId; Reason; PreviousConclusion; PreviousConclusionSignatureId; ReopenedByUserId; ReopenedAtUtc; SignatureId }`.
- `OosInvestigationTemplate : IVersionedEntity { Id; SectionId; Kind; PhaseOneDueWorkingDays?; TotalDueWorkingDays?; AlertOpensExcursionReview (default false) }` unique (`SectionId`, `Kind`); `OosChecklistItem { Id; TemplateId; Phase; Key (stable guid string); Order; Text; IsActive }`; `OosCauseCategory { Id; Name (unique); IsActive; IsNoCauseFound }`.
- Triggers in the migration: `OosInvestigationEntries`, `OosInvestigationResults` (except the one-way `IsInvalidated` false→true update), `OosInvestigationReopenings`: reject UPDATE and DELETE. `OosRetestPlans`: reject DELETE; reject UPDATE of any column except `SamplesCreatedAtUtc` when `OLD."SignedAtUtc" IS NOT NULL`. `OosInvestigations`: reject DELETE.
- `GenerateOosCodeAsync`: keep the format `OOS{MM}{yy}{seq:D3}`; take `pg_advisory_xact_lock(<constant>)` before counting; count over `OosInvestigations.Number` ∪ distinct `Samples.OosGroupCode`; month and year from `ILabClock` (as `GenerateAsync` does). Requires an ambient transaction; throw `InvalidOperationException` when none.
- Migration seeds the seven cause categories.

- [ ] **Step 1:** Failing tests: `Concurrent_openings_get_distinct_numbers` (two connections, both inside transactions), `Number_unique_index_rejects_duplicate`, `Second_open_investigation_for_same_sample_section_kind_rejected`, `Entry_update_and_delete_rejected_by_trigger`, `Signed_plan_update_rejected_except_samples_created`, `Result_invalidated_flag_is_one_way`, `Investigation_delete_rejected`, `No_cause_found_category_seeded_once`, `Generate_code_uses_lab_clock_month`.
- [ ] **Step 2:** FAIL → implement → PASS incl. `AppendOnlyTriggerCatalogPostgresTests`; no pending model changes. Commit `feat(oos): investigation schema, numbering lock and protection`.

### Task 3: Opening at review, and the gate

**Files:** Create `OosInvestigationService.cs` (open part), `OosGate.cs`; Modify `SampleReviewService.CompleteReviewCoreAsync` (after orders are set `Reviewed`, before the single `SaveChangesAsync` at L175), `SampleApprovalService.DecideCoreAsync` (with the other pre-conditions, L177–244), `SampleSummaryService` (`GetCertificateSummaryAsync` L84, `GenerateSummaryPdfAsync` L394 and the Word path when producing a certificate, `BuildReportDocumentAsync` L413 callers used for certificate/archive); Test `IntegrationTests/OosOpeningAndGatePostgresTests.cs`

**Interfaces:**
- `Task<OosInvestigation?> OpenForReviewedSectionAsync(Sample sample, int sectionId, IReadOnlyCollection<TestOrder> reviewedOrders, int reviewerUserId, CancellationToken ct = default)` — uses `GetOutcomesAsync`; for `Fails` orders: if `sample.OosGroupCode` names a non-concluded investigation (retest sample), add the results to it; else if a non-concluded one exists for (sample, section), add; else create (`Kind = Oos`, `Status = PhaseOne`, number generated, `sample.OosGroupCode = Number`, template snapshot, due dates). Adds to the caller's context; does not save. Returns null when nothing fails. Excursions are Task 9.
- Due dates: `PlanWindowCalculator.AddWorkingDays(DateOnly date, int days, int workingWeekdays, IReadOnlySet<DateOnly> nonWorkingDates)`; read how the monitoring plan obtains `workingWeekdays` and non-working dates and use the same source; null days → null date.
- `OosGate`: `Task EnsureSectionMayBeDecidedAsync(int sampleId, int sectionId, ApprovalDecision decision, CancellationToken ct = default)` — throws `BusinessRuleException`: open investigation → `"OOS investigation {number} is open."`; any current reviewed order of the section with outcome `Fails` and no investigation → `"This sample has an out-of-specification result without an investigation."`; concluded `Confirmed` and decision `Approve` → `"A confirmed out-of-specification result cannot be approved."`; concluded `Invalidated` and decision `Approve` while a plan sample is not `Approved` → `"Retest samples of {number} are not all approved."`; decisions `RetestRetainedSample` / `NewSampleRequest` on a section that has any investigation → `"A retest needs an approved retest plan."`.
- `Task EnsureCertificateAllowedAsync(int sampleId, CancellationToken ct = default)` — refuses when any section of the sample has a non-concluded `Oos` investigation.

- [ ] **Step 1:** Failing tests: `Review_of_failing_result_opens_investigation_in_same_transaction` (state of investigation, results snapshot, `OosGroupCode`, audit rows, reviewer id), `Review_and_opening_roll_back_together` (force the numbering to fail), `Passing_section_opens_nothing`, `Second_failing_section_review_joins_open_investigation`, `Approve_and_reject_refused_while_open`, `Section_with_failing_order_and_no_investigation_cannot_be_approved`, `Every_certificate_path_is_blocked_while_open` (summary `forCertificate`, PDF, Word, archive on close), `Plain_sample_summary_still_readable_while_open`, `Other_section_of_same_sample_can_still_be_approved_but_certificate_is_blocked`, `Direct_retest_decision_refused_for_section_with_investigation`, `Direct_retest_decision_unchanged_for_section_without_failing_result`.
- [ ] **Step 2:** FAIL → implement → PASS; run the whole existing review and approval suites. Commit `feat(oos): open investigation at review and gate approval and certificates`.

### Task 4: Permissions

**Files:** `PermissionConstants.cs`, `SegregationOfDutiesRules.cs`, `DbSeeder.cs`, migration `<ts>_AddOosInvestigationPermissions.cs` (template: `20261103212726_AddSamplesPreparePermission.cs`); Test `IntegrationTests/OosPermissionsMigrationPostgresTests.cs`

**Interfaces:** Grants to system roles — `Oos.View`: Section Head (1), Reviewer (2), Analyst (3), QC Manager (4), QA (5, if the role exists); `Oos.Contribute`: Analyst, Section Head; `Oos.RequestDeviation`: QA only (granted by role filter `Type = 5`; no rows if B4 is not installed — then `DbSeeder` and a follow-up note in the pull request cover it). All `IsEnforced = true`.

- [ ] **Step 1:** Failing tests: exact grants per role, audit rows, `PermissionVersion` bumped, no other grant changed, fresh seed equals migrated. **Step 2:** FAIL → implement → PASS. Commit `feat(oos): investigation permissions`.

### Task 5: Templates, categories, Phase I

**Files:** `OosTemplateService.cs`, `OosInvestigationService.cs` (Phase I), `OosTemplatesController.cs`, `OosInvestigationsController.cs`; Test `IntegrationTests/OosPhaseOnePostgresTests.cs`

**Interfaces:**
- Templates (policy `Oos.Manage`, section scope `Configure`): `GetAsync(sectionId, kind)`, `SaveAsync(sectionId, kind, SaveOosTemplateRequest { PhaseOneDueWorkingDays?, TotalDueWorkingDays?, AlertOpensExcursionReview, Items[{ Key?, Phase, Order, Text, IsActive }] }, expectedVersion, userId)`; categories `ListAsync`, `SaveAsync` (cannot deactivate or rename `IsNoCauseFound`).
- `Task AddAnalystStatementAsync(int investigationId, string text, int userId, CancellationToken ct = default)` — `Oos.Contribute`; user must be an analyst who performed one of the investigation's tests (`SegregationOfDutiesGuard.DidUserPerformTestAsync`).
- `Task AnswerChecklistAsync(int investigationId, int phase, string itemKey, OosChecklistAnswer answer, string? comment, int userId, CancellationToken ct = default)` — appends an entry; the latest entry per key is the current answer.
- `Task<OosChecklistFactsDto> GetFactsAsync(int investigationId, CancellationToken ct = default)` — read-only facts per investigated test order: equipment used with calibration status at time of use, media and solution lots with release and expiry, standards, incubation start/end against configured window, count of audit entries for the results. Read the workflow entities first; include only facts that exist as data; never derive a verdict.
- `Task SignPhaseOneAsync(int investigationId, SignPhaseOneRequest { Assessment, CauseCategoryId?, CauseDescription?, Password }, uint expectedVersion, int userId, string? ip, CancellationToken ct = default)` — `Oos.Manage`; requires an analyst statement, every active Phase I item answered, non-empty template; `LaboratoryErrorConfirmed` requires a category that is not `IsNoCauseFound` and a description; signer performed none of the investigated tests; signs `OosPhaseOneConcluded` on (`"OosInvestigation"`, id) in the same `UnitOfWork.RunAsync`; error confirmed → results `IsInvalidated = true`, status `AwaitingRetest`; no error → `PhaseTwo`.

- [ ] **Step 1:** Failing tests: template save with audit and stale version refused; investigation keeps its snapshot after a template change; `Phase_one_refused_with_empty_template`; statement only by a performing analyst; unanswered item refuses signature; wrong password refused with nothing changed; signer who performed the test refused with the exact message; error-confirmed path (state, invalidated flags, signature bound, audit old/new values, original `Result` rows byte-identical before and after); no-error path; error-confirmed with "No cause found" refused; section-scope refusal for a Section Head of another section; second signature attempt refused.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(oos): templates and Phase I`.

### Task 6: Retest plan

**Files:** `RetestSampleFactory.cs` (extract `BuildRetestSample` L54 and the creation code of the two branches at ~L378–504 without behaviour change), `OosRetestPlanService.cs`; Modify `SampleApprovalService` (use the factory; `NewSampleRequest` signs `NewSampleRequested` for new records; `PropagateOosOutcomeAsync` L542 skipped when the origin sample has an `OosInvestigation` row); Test `IntegrationTests/OosRetestPlanPostgresTests.cs`

**Interfaces:**
- `Task<OosRetestPlanDto> DraftAsync(int investigationId, SaveRetestPlanRequest { Source, RetestCount (1–10), TestOrderIds, AnalystUserIds, JudgingRule, AmendmentReason? }, int userId, CancellationToken ct = default)` — allowed in `AwaitingRetest` or `PhaseTwo`; a revision > 1 needs `AmendmentReason`; analysts must be assignable (`AnalystAssignmentRules`); for `NewSample`, none may have performed the original tests (existing rule).
- `Task SignAndCreateAsync(int planId, string password, int userId, string? ip, CancellationToken ct = default)` — signs `OosRetestPlanApproved`; creates exactly `RetestCount` linked samples through `RetestSampleFactory` with `OosGroupCode = Number`, records `OosRetestPlanSample`, sets `SamplesCreatedAtUtc`; one transaction. Does **not** mark originals superseded and does not change the origin signoff status.
- Planning before Phase I is signed → `"A retest needs an approved retest plan."` is for the gate; here: `"Phase I must be concluded before a retest is planned."`

- [ ] **Step 1:** Failing tests: characterisation of the two existing branches before extraction (samples, orders, analysts identical); `Plan_refused_before_phase_one_signed`; `Sign_creates_exactly_planned_samples` (count, links, group code, analysts, audit, signature); `Unsigned_plan_creates_nothing`; `Amendment_needs_reason_and_new_signature`; `Failing_retest_result_joins_the_origin_investigation`; `Retest_sample_is_gated_until_conclusion`; `Origin_is_not_finalised_automatically_when_retests_close`; `Legacy_group_without_investigation_still_propagates_as_before`; `New_sample_decision_on_non_oos_sample_signs_new_meaning`.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(oos): signed retest plan`.

### Task 7: Phase II, conclusion, reopen

**Files:** `OosInvestigationService.cs`; Test `IntegrationTests/OosConclusionPostgresTests.cs`

**Interfaces:**
- `AddEntryAsync(int investigationId, OosEntryKind kind, string text, int? parentEntryId, int userId, ct)` for `OutsideReview`, `Hypothesis`, `HypothesisOutcome` (parent must be a `Hypothesis`), `Note` — `Oos.Contribute` or `Oos.Manage`; Phase II kinds only in `PhaseTwo`.
- `Task ConcludeAsync(int investigationId, ConcludeRequest { Conclusion, RootCauseCategoryId, RootCauseDescription, Summary, Password }, uint expectedVersion, int userId, string? ip, ct)` — `Oos.Manage`. From `AwaitingRetest`: conclusion must be `Invalidated`, cause = Phase I cause. From `PhaseTwo`: every active Phase II item answered; `Invalidated` requires a category that is not `IsNoCauseFound`; `Confirmed` allowed with any category. Any signed plan: every plan sample's section review completed. `Invalidated` requires at least one signed plan with created samples; sets investigated orders `IsSuperseded = true` and results `IsInvalidated = true`. Signer performed none of the investigated or retest tests. Signs `OosInvestigationConcluded`.
- `Task ReopenAsync(int investigationId, string reason, string password, int userId, string? ip, ct)` — only while the origin section is not yet approved or rejected; writes `OosInvestigationReopening`, signs `OosInvestigationReopened`, status back to `PhaseTwo`, conclusion fields cleared on the root (kept in the reopening row and audit).
- `Task<OosInvestigation> OpenManuallyAsync(int sampleId, int sectionId, int userId, CancellationToken ct = default)` — `Oos.Manage`; for a reviewed section with a failing order and no investigation (data from before this feature). No waiver exists (user decision 2026-11-11: system still under development).

- [ ] **Step 1:** Failing tests: `No_cause_found_cannot_invalidate`; `Invalidated_without_signed_plan_refused`; `Conclusion_refused_until_every_planned_retest_sample_is_reviewed`; `Confirmed_then_approve_refused_reject_allowed`; `Invalidated_then_approve_allowed_only_when_plan_samples_approved`; `Invalidated_marks_orders_superseded_without_touching_results`; `Signer_who_performed_a_retest_refused`; `Reopen_keeps_previous_conclusion_and_signature`; `Reopen_refused_after_section_decided`; `Manual_open_for_reviewed_failing_section_without_investigation`; `Manual_open_refused_when_nothing_fails`; state + signature + audit on each.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(oos): Phase II, conclusion and reopen`.

### Task 8: QA request and notifications

**Files:** `OosRecipientResolver.cs`, `OosNotifier.cs`, `OosInvestigationService.cs` (requests); Test `IntegrationTests/OosQaAndNotificationPostgresTests.cs`

**Interfaces:**
- `Task<IReadOnlyList<int>> ResolveAsync(int sectionId, string permissionCode, CancellationToken ct = default)` — active, unlocked users whose role holds the code and who are members of the section (explicit or department-wide) or hold `Sections.ViewAll`.
- `OosNotifier.OpenedAsync / PhaseTwoStartedAsync / ConcludedAsync / DeviationRequestedAsync(investigationId, ct)` — called by controllers after the service call returns (after commit); uses `INotificationService.NotifyAsync(int userId, string message)`; recipients: performing analysts, `Oos.Manage` holders of the section, `Oos.RequestDeviation` holders. If package C5's event API exists, use it with a dedupe key `oos:{id}:{event}` instead.
- `RequestDeviationAsync(int investigationId, string reason, int userId, ct)` — `Oos.RequestDeviation`, any status; `AddressDeviationRequestAsync(int requestId, string comment, uint expectedVersion, int userId, ct)` — `Oos.Manage`. An open request does not block conclusion.
- Overdue: a query method `ListOverdueAsync(nowLabDate, ct)` for My Work / notifications; no background job is added here.

- [ ] **Step 1:** Failing tests: resolver membership and view-all cases, inactive users excluded; request by non-QA refused; request persisted with audit and notification rows for the section's `Oos.Manage` holders; conclusion allowed with an open request; notification failure does not undo the committed change (simulate throwing notifier); overdue listing by lab date.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(oos): QA deviation requests and notifications`.

### Task 9: Excursion review

**Files:** `OosInvestigationService.cs`, `SampleReviewService` call site; Test `IntegrationTests/OosExcursionPostgresTests.cs`

**Interfaces:** In `OpenForReviewedSectionAsync`: orders with outcome `ActionLimitExceeded` (and `AlertLimitExceeded` when the section's Excursion template has `AlertOpensExcursionReview`) open or join an investigation of `Kind = Excursion`, unless the sample belongs to a monitoring plan occurrence (read how `MonitoringDeviationService.DetectAsync` links a sample to a `MonitoringOccurrence` and use the same link); then nothing is opened. `OosGate` ignores `Excursion`. `OpenManualExcursionAsync(int testOrderId, string reason, int userId, ct)` — `Oos.Manage` or `Oos.Contribute`, for the control-chart entry point. `CloseExcursionAsync(int investigationId, CloseExcursionRequest { CauseCategoryId, CauseDescription, ActionTaken, Password }, uint expectedVersion, int userId, string? ip, ct)` — every active Excursion item answered; signs `ExcursionReviewClosed`; `Conclusion = ExcursionClosed`.

- [ ] **Step 1:** Failing tests: action-limit result opens excursion and does not block approval or certificate; alert opens only when switched on; monitoring-plan sample opens none; failing + action-limit in one section opens one of each kind; manual opening; closure rules, signature, audit; signer who performed the test refused.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(oos): excursion reviews`.

### Task 10: Read API, report, trending

**Files:** `OosInvestigationsController.cs` (route `api/oos-investigations`), `OosInvestigationReportBuilder.cs`, `OosTrendService.cs`; Modify `OosTrackingService` (legacy groups = group codes with no investigation row), `OosInvestigationDocumentController` (GET endpoints accept `Oos.View`), `ReportDocumentMapper` / certificate rows (investigation number against affected tests; invalidated value never the reported value); Test `IntegrationTests/OosReadAndReportPostgresTests.cs`

**Interfaces:**
- `GET /` (filters: status, kind, sectionId, overdue, from, to, page, pageSize ≤ 100), `GET /{id}` (full DTO incl. timeline of signed steps with signer full name, UTC time, meaning text), `GET /{id}/facts`, `GET /{id}/pdf`, `GET /trends?groupBy=product|test|analyst|instrument|cause|conclusion&from&to`, `GET /for-sample/{sampleId}` — all `Oos.View` with section read scope; an Analyst without `Oos.Manage` sees only investigations containing a test they performed.
- Every endpoint from Tasks 5–9 with its policy; mutating ones take `If-Match`.
- PDF via `IPdfGenerator.GenerateReportAsync(ReportDocument)`; every signature shown with printed name, UTC time with zone, meaning; export logged with `DataExportAuditService.LogExportAsync`.
- Trends: counts and median working days to conclude; projection in SQL, `AsNoTracking`.

- [ ] **Step 1:** Failing tests: list filters and paging clamp; section scope (other section → not found); analyst sees only own; DTO timeline content; PDF text contains each signer name, time and meaning; certificate row shows the number and never the invalidated value as reported; legacy groups listed separately; trends for each `groupBy`; KPI `oos-rate` and `retest-rate` values unchanged for a fixed data set (characterisation before the change).
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(oos): investigation API, report and trending`.

### Task 11: Frontend — register and investigation page

**Files:** `services/oosInvestigationService.ts`; `modules/oosTracking/OosTrackingPage.tsx` (rewritten as register with tabs Open / Overdue / Concluded / Excursions / Legacy; legacy tab keeps today's group view and document dialog); `modules/oosTracking/InvestigationPage.tsx`, `components/PhaseOneSection.tsx`, `ChecklistPanel.tsx`, `FactsPanel.tsx`, `RetestPlanSection.tsx`, `PhaseTwoSection.tsx`, `ConclusionSection.tsx`, `DeviationRequestsPanel.tsx`, `SignedTimeline.tsx`; routes `/oos-tracking`, `/oos-tracking/:id`; menu item permission → `Oos.View`; tests beside each

**Interfaces:** Types mirror the DTOs exactly. `RegisterTable` for lists; `SignatureDialog` (existing props) for every signature with the meaning statement from the server; identifiers in `monospaceFontFamily`; status chips from `theme.custom.status` (register tones in `statusTokens.ts`); all nine UI states; disabled actions show the server's reason as visible text.

- [ ] **Step 1:** Failing tests (`userEvent`, real components, mocked `services/` only): register tabs and filters; analyst adds statement; checklist answer with comment and facts shown beside it; Phase I sign sends assessment, cause, password and version; error-confirmed requires a cause; retest plan draft, sign, amendment reason; Phase II hypothesis with outcome; conclusion form disables "Invalidated" with the reason when the cause is "No cause found"; server refusal shown and dialog stays open; QA sees request button, Section Head sees address action; signed timeline shows name, time zone and meaning; read-only for `Oos.View` only; partial outage banner when facts fail to load.
- [ ] **Step 2:** FAIL → implement → `npm run lint`, `npx tsc -b`, `npm run test`, `npm run debt:check` PASS. Commit `feat(ui): OOS investigation register and page`.

### Task 12: Frontend — templates, banners, approval dialog

**Files:** `modules/oosTracking/OosTemplatesPage.tsx` (+ route under laboratory configuration, permission `Oos.Manage`); `components/OosBanner.tsx` used in `modules/testingWorkspace/SampleSummaryDialog.tsx` (retest options at L247–296), `SampleCoaPage.tsx` (L448); "Open investigation" action on the blocked-approval banner when none exists; control-chart point action "Open excursion review"; tests

- [ ] **Step 1:** Failing tests: template items add, reorder by keyboard, deactivate, save with version; cause categories with the fixed one locked; banner with link when an investigation is open and the approval buttons disabled with the reason; retest and new-sample options absent for a section with an investigation and present otherwise; certificate page shows the block reason; open investigation from the banner; excursion opened from a chart point.
- [ ] **Step 2:** FAIL → implement → full frontend checks PASS. Commit `feat(ui): OOS templates, banners and approval changes`.

### Task 13: Documents and verification

- [ ] **Step 1:** `documents/DesignSpecification/BusinessRules.md`: rewrite section 9 (Approval Engine) to match the code — the two unusable decisions removed from the description, the OOS gate, the retest plan, signed conclusion replacing automatic finalisation; fix the file reference to `SampleApprovalService.cs`. Remove `Investigation` / `OOSInvestigation` from the approval screens only; leave the enum values (persisted integers) untouched.
- [ ] **Step 2:** If A1–A3 documents exist: URS/FS entries (area OOS, REV, RPT), risk rows and tags, manual chapter 18 and 20 updates, role guides. Otherwise list as follow-ups.
- [ ] **Step 3:** Full verification per `CLAUDE.md` section 2 with PostgreSQL; exact pass / fail / skip counts; `has-pending-model-changes`; frontend lint, types, tests, debt check, build.
- [ ] **Step 4:** Report to the user: the manual scenarios of spec section 7 to perform; that templates ship empty and must be filled before go-live.
