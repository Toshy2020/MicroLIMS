# Deviation, CAPA, Change Control and Complaint (D2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build four linked quality record types with fixed paths, QA approval and closure, links to laboratory records, and an optional change-control gate on master-data changes.

**Architecture:** One `QualityRecord` root with a one-to-one detail entity per type, shared child tables (actions, links, attachments, comments, extension requests) and an append-only `QualityRecordStep` table that holds every signed step with a snapshot of what was signed. One state-machine service per type on a shared base. The master-data gate is a scoped `IChangeControlContext` filled from an `X-Change-Control` header and checked inside the master-data services.

**Tech Stack:** .NET 10, EF Core 10, PostgreSQL, xUnit `[PostgresFact]`; React 19, TypeScript, MUI, Vitest.

**Spec:** `docs/superpowers/specs/2026-11-11-quality-events-design.md`

## Global Constraints

- Prerequisites: B4 (QA role, `RoleType.Qa`) and D1 (`OosCauseCategory`, `OosDeviationRequest`, the advisory-lock numbering pattern) are implemented.
- `CLAUDE.md` binds every task (anti-fake tests, state + audit + negative triad, `[PostgresFact]` must run, `TimeProvider` / `ILabClock`, `CancellationToken`, one migration per schema change + `has-pending-model-changes`, no new `any`, `userEvent`, WCAG 2.2 AA, design tokens, no external effects inside transactions).
- Numbers exactly `DEV-{yyyy}-{seq:D4}`, `CAPA-…`, `CC-…`, `CMP-…`; year from `ILabClock`; per type and year; unique index; generated under `pg_advisory_xact_lock`.
- Permission codes exactly: `Quality.View`, `Quality.Raise`, `Quality.Investigate`, `Quality.LabConclude`, `Quality.Approve`. `Quality.Approve` is added to B4's `QaOnlyCodes`; `Quality.Investigate` and `Quality.LabConclude` are added to `GxpExecutionAndApprovalCodes` (so QA can never hold them).
- New `SignatureMeaning` values appended in this order after the last existing value: `DeviationAssessed`, `DeviationRejected`, `QualityLabConclusion`, `QualityApproved`, `QualityClosed`, `QualityCancelled`, `CapaPlanApproved`, `CapaCompletionVerified`, `CapaEffectivenessChecked`, `ChangeImpactAssessed`, `ChangeApproved`, `ChangeRejected`, `ComplaintConcluded`, `QualityExtensionDecided`. Each gets a `ReportVocabulary` label.
- New entities stay under automatic audit capture.
- Text limits: title 1–200; descriptions, findings, reasons 1–4000; comments 1–2000. Classification names default Minor, Major, Critical.
- Attachments follow `DiscussionAttachmentPolicy` rules (allowlist with magic-byte check, 10 MB per file) through `IFileStorageService`, key `quality/{recordId}/{guid}_{cleanName}`.
- Separation rules (server-side, exact messages): `"You raised or own this record, so you cannot approve or close it."`; `"The owner cannot sign the laboratory conclusion."`; `"The analyst who produced the result cannot own this deviation."`
- Items have no laboratory in the data model, so the change-control gate covers specifications, test definitions and workflow steps only. Items are not gated in this release.
- Notifications are sent after commit.

## Review Focus

1. A master-data save path that skips the gate (freeze / unfreeze, step move, specification delete) → `Every_gated_write_is_refused_without_change_control`, one row per method (Task 9).
2. A change control approved for laboratory A used to change a test of laboratory B → `Change_control_of_other_laboratory_refused` (Task 9).
3. Signed step content edited afterwards through a normal update endpoint → `Signed_step_fields_cannot_be_edited` (Task 3) and the database trigger test (Task 1).
4. QA closing a record QA itself raised → `Raiser_cannot_approve_or_close` (Task 3), for each type's closing step.
5. An effectiveness check signed early, or a "not effective" CAPA closed without a successor → `Effectiveness_refused_before_planned_date`, `Not_effective_requires_linked_new_capa` (Task 5).

## File Structure

| File | Responsibility |
|---|---|
| `Domain/Entities/Quality/QualityRecord.cs` | Root + `DeviationDetail`, `CapaDetail`, `ChangeControlDetail`, `ComplaintDetail` |
| `Domain/Entities/Quality/QualityChildren.cs` | `QualityRecordStep`, `QualityAction`, `QualityRecordLink`, `QualityAttachment`, `QualityComment`, `QualityExtensionRequest`, `QualityChecklistAnswer` |
| `Domain/Entities/Quality/QualityConfiguration.cs` | `QualityChecklistTemplate`, `QualityChecklistItem`, `QualityClassification`, `QualityDueRule`, `QualitySectionSetting` |
| `Domain/Enums/QualityEnums.cs` | Enums below |
| `Persistence/Configurations/Quality*.cs` | Mapping |
| `Application/Services/Quality/QualityRecordCore.cs` | Shared: create, number, access, sign step, snapshot, separation |
| `Application/Services/Quality/DeviationService.cs`, `CapaService.cs`, `ChangeControlService.cs`, `ComplaintService.cs` | Paths |
| `Application/Services/Quality/QualityLinkService.cs`, `QualityAttachmentService.cs`, `QualityConfigService.cs`, `QualityDueDateService.cs`, `QualityQueryService.cs`, `QualityReportBuilder.cs`, `QualityNotifier.cs` | Supporting |
| `Application/Services/Quality/ChangeControlGate.cs`, `IChangeControlContext` | Master-data gate |
| `API/Middleware/ChangeControlHeaderMiddleware.cs` | Reads `X-Change-Control` |
| `API/Controllers/Quality/*.cs` | Endpoints |
| `frontend/src/modules/quality/*`, `services/qualityService.ts` | Screens |

Enums: `QualityRecordType { Deviation, Capa, ChangeControl, Complaint }`; `QualityDeviationStatus { Raised, Investigation, LabConcluded, Approved, Closed, Rejected, Cancelled }`; `CapaStatus { Plan, PlanApproved, CompletionVerified, Closed, Cancelled }` plus `CapaEffectiveness { Effective, NotEffective }`; `ChangeControlStatus { Proposed, Assessed, ApprovedForImplementation, Closed, Rejected, Cancelled }`; `ComplaintStatus { Received, Investigation, LabReported, Concluded, Closed, Cancelled }`; `ComplaintOutcome { Justified, NotJustified, Inconclusive }`; `QualityActionKind { Corrective, Preventive, ChangeTask }`; `QualityLinkTargetType { Sample, TestOrder, OosInvestigation, MonitoringDeviation, Equipment, MediaLot, StockLot, Item, Specification, TestDefinition, DocumentMaster, DocumentRevision, AuditTrailReviewFinding, QualityRecord }`; `QualityStepKind` = one value per signed step.

---

### Task 1: Schema, numbering, protection, permissions

**Files:** Domain and Persistence files above; migrations `<ts>_AddQualityRecords.cs`, `<ts>_AddQualityPermissions.cs`; `PermissionConstants.cs`, `SegregationOfDutiesRules.cs`, `DbSeeder.cs`, `SignatureMeaning.cs`, `ReportVocabulary.cs`, `AppendOnlyTriggerCatalogPostgresTests.ProtectedTables`; Test `IntegrationTests/QualitySchemaPostgresTests.cs`

**Interfaces:**
- `QualityRecord : IVersionedEntity { Id; Number; Type; Title; Description; SectionId? (null = site-wide); RaisedByUserId; RaisedAtUtc; OwnerUserId?; ClassificationId?; DueDate?; IsCancelled; CancelReason?; Version }` + one detail row holding the type's status and step fields (investigation text, root cause category/description, `CapaRequired`, effectiveness criteria / planned date / result, impact summary, complaint source / received date / external reference / item / lot text / outcome).
- `QualityRecordStep { Id; RecordId; Kind; SignedByUserId; SignedAtUtc; SignatureId; Comment?; SnapshotJson }` — append-only.
- `QualityAction : IVersionedEntity { Id; RecordId; Kind; Description; ResponsibleUserId; DueDate; DoneAtUtc?; DoneByUserId?; Evidence?; EvidenceLinkId? }`.
- `QualityRecordLink { Id; RecordId; TargetType; TargetId?; NewRecordCode?; IsAffectedByChange; AddedByUserId; AddedAtUtc; ChangedAtUtc?; ChangeAuditLogId? }`; unique (`RecordId`, `TargetType`, `TargetId`, `NewRecordCode`).
- `QualityExtensionRequest : IVersionedEntity { Id; RecordId; ActionId?; RequestedDueDate; Reason; RequestedByUserId; RequestedAtUtc; Decision? (Approved|Refused); DecidedByUserId?; DecidedAtUtc?; DecisionComment?; SignatureId? }`.
- `QualityChecklistTemplate { Id; Type; SectionId?; Items }`, `QualityChecklistItem { Id; TemplateId; Key; Order; Text; IsActive }`, `QualityChecklistAnswer { Id; RecordId; ItemKey; ItemText; Answer (Yes|No|NotApplicable); Comment?; AnsweredByUserId; AnsweredAtUtc }` (append-only; latest per key wins).
- `QualityClassification { Id; Name; Order; IsActive }` seeded Minor, Major, Critical; `QualityDueRule { Type; ClassificationId?; WorkingDays? }`; `QualitySectionSetting : IVersionedEntity { SectionId (unique); ChangeControlRequired (default false) }`.
- Triggers: reject UPDATE and DELETE on `QualityRecordSteps`, `QualityChecklistAnswers`; reject DELETE on `QualityRecords`, `QualityActions`, `QualityRecordLinks` (links are removed only before the first signed step — enforced in the service; after that they are permanent), `QualityAttachments`.
- Grants: `Quality.View` → Section Head, Reviewer, QC Manager, QA, Analyst; `Quality.Raise` → all five; `Quality.Investigate` → Section Head; `Quality.LabConclude` → Section Head, QC Manager; `Quality.Approve` → QA.

- [ ] **Step 1:** Failing tests: `Concurrent_creation_gets_distinct_numbers_per_type`, `Number_restarts_per_year_and_type`, `Step_and_answer_rows_reject_update_and_delete`, `Record_delete_rejected`, `Classifications_seeded`, exact grants per role with audit rows, `Qa_role_cannot_be_granted_investigate_or_lab_conclude` (B4 rule), `Non_qa_role_cannot_be_granted_approve_with_laboratory_codes`.
- [ ] **Step 2:** FAIL → implement → PASS incl. the trigger catalog test; no pending model changes. Commit `feat(quality): schema, numbering, protection and permissions`.

### Task 2: Core service — access, links, attachments, comments

**Files:** `QualityRecordCore.cs`, `QualityLinkService.cs`, `QualityAttachmentService.cs`; Test `IntegrationTests/QualityCorePostgresTests.cs`

**Interfaces:**
- `Task<QualityRecord> CreateAsync(QualityRecordType type, CreateQualityRecordRequest { Title, Description, SectionId?, Links[] }, int userId, CancellationToken ct = default)`.
- `Task<QualityRecord> LoadForReadAsync(int id, int userId, ct)` / `LoadForWriteAsync(int id, uint expectedVersion, int userId, ct)` — read: `Quality.View` and (site-wide, or section in `GetReadableSectionIdsAsync`); a user without `Quality.Investigate`, `Quality.LabConclude` or `Quality.Approve` and with role Analyst reads only records they raised, own, or hold an action in. Not readable → `NotFoundException`.
- `Task<QualityRecordStep> SignStepAsync(QualityRecord record, QualityStepKind kind, SignatureMeaning meaning, object snapshot, string password, string? comment, int userId, string? ip, ct)` — `IElectronicSignatureService.SignAsync(userId, password, meaning, "QualityRecord", record.Id, comment, ip)`; writes the step row with `SnapshotJson`; caller wraps in `UnitOfWork.RunAsync`.
- `void EnsureNotRaiserOrOwner(QualityRecord record, int userId)`; `bool IsStepSigned(QualityRecord record, QualityStepKind kind)`.
- Links: `AddAsync(recordId, targetType, targetId, userId, ct)` validates the target exists and the acting user may read it (per-type check through the existing `Ensure*AccessAsync` family; unknown target → refuse); `RemoveAsync` only before the first signed step; `Task<IReadOnlyList<QualityLinkDto>> ResolveAsync(recordId, userId, ct)` returns label and route for readable targets, number only (`IsRestricted = true`) for others; `Task<IReadOnlyList<QualityRecordRefDto>> ForTargetAsync(targetType, targetId, userId, ct)` for the "Quality records" panel on other screens.
- Attachments: `UploadAsync`, `GetContentAsync`, `VoidAsync(reason)` — never deleted; download logged like other document downloads. Comments: `AddAsync`, no edit or delete.

- [ ] **Step 1:** Failing tests: create with audit; read scope (other laboratory → not found; site-wide visible; analyst limited to raised / owned / action); link to unreadable or missing target refused; restricted link shown as number only; link removal refused after a signed step; `ForTargetAsync` scope; attachment type and size refusals with magic-byte mismatch; void keeps the row; stale version refused.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(quality): core record service, links and attachments`.

### Task 3: Deviation

**Files:** `DeviationService.cs`, `API/Controllers/Quality/DeviationsController.cs`; Test `IntegrationTests/QualityDeviationPostgresTests.cs`

**Interfaces (each mutating method takes `expectedVersion`, `userId`, `ip?`, `ct`):**
- `RaiseAsync(RaiseDeviationRequest { Title, Description, OccurredAtUtc, Location?, ImmediateAction, SectionId?, Links[] })` — `Quality.Raise`.
- `AssessAsync(id, { Accept: true, ClassificationId, OwnerUserId, DueDate? , Password })` — `Quality.Approve`; owner must be active, hold `Quality.Raise`, be a member of the section (when set), and not have performed a test of any linked `TestOrder` / `OosInvestigation` result (`SegregationOfDutiesGuard.DidUserPerformTestAsync`); default due date from `QualityDueRule`; signs `DeviationAssessed` → `Investigation`. `RejectAsync(id, reason, password)` → `Rejected`, signs `DeviationRejected`.
- `SaveInvestigationAsync(id, { ImpactAssessment, RootCauseCategoryId, RootCauseDescription, ProposedActions })` — owner or `Quality.Investigate`; only in `Investigation`.
- `SignLabConclusionAsync(id, password)` — `Quality.LabConclude`; signer ≠ owner; investigation complete → `LabConcluded`.
- `ApproveAsync(id, { CapaRequired, Comment?, Password })` / `ReturnAsync(id, comment)` (back to `Investigation`, lab conclusion step stays in the timeline, a new one is needed) — `Quality.Approve`, not raiser or owner.
- `CloseAsync(id, password)` — `Quality.Approve`, not raiser or owner; if `CapaRequired`, a linked CAPA with status ≥ `PlanApproved` must exist, else `"A CAPA with an approved plan is required before closure."`
- `CancelAsync(id, reason, password)` — `Quality.Approve` (shared by all types through the core).
- Sources: `RaiseFromOosRequestAsync(int oosDeviationRequestId, userId, ct)` — `Quality.Approve`; pre-fills from the investigation, links it, sets the request `Addressed` in the same transaction; `EscalateMonitoringDeviationAsync(int monitoringDeviationId, …)` — `Quality.Approve` or `Quality.Investigate`; `RaiseFromAuditFindingAsync(int findingId, …)` — `Quality.Approve`.

- [ ] **Step 1:** Failing tests: full path with state, step row, signature binding and audit at each signed step; `Signed_step_fields_cannot_be_edited` (investigation save after lab conclusion refused); `Raiser_cannot_approve_or_close`; owner cannot sign lab conclusion (exact message); result's analyst cannot be owner (exact message); owner outside the section refused; return creates a need for a new lab conclusion; closure refused without required CAPA plan approved; closure allowed when CAPA not required; reject and cancel; OOS request → deviation with request addressed (rolls back together); monitoring escalation keeps the source record unchanged; wrong password leaves everything unchanged; no QA user → approval methods refuse by permission.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(quality): deviations`.

### Task 4: Checklists, classifications, due rules, extensions

**Files:** `QualityConfigService.cs`, `QualityDueDateService.cs`, controller `QualityConfigController.cs`; Test `IntegrationTests/QualityConfigPostgresTests.cs`

**Interfaces:** Templates per (`Type`, `SectionId?`) — section template wins over site template; edited with `Quality.Approve` (site) or `Quality.Investigate` + configure scope (section). `AnswerAsync(recordId, itemKey, answer, comment, userId)` stores the item text with the answer. Due: `DateOnly? ComputeDue(QualityRecordType, int? classificationId, DateOnly start)` using `PlanWindowCalculator.AddWorkingDays` and the same working-day source the monitoring plan uses; null rule → null. Extensions: `RequestAsync(recordId, actionId?, requestedDueDate, reason, userId)` (owner or action responsible), `DecideAsync(requestId, approve, comment, password, userId)` — `Quality.Approve`, signs `QualityExtensionDecided`; approval updates the due date; history kept. `ListOverdueAsync(labDate, ct)` for records and actions.

- [ ] **Step 1:** Failing tests: template precedence; answer keeps text after template edit; due computed over a non-working date; empty rule gives no due date; extension request, approve and refuse with audit and signature; requester cannot decide; overdue listing.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(quality): checklists, due rules and extensions`.

### Task 5: CAPA

**Files:** `CapaService.cs`, `CapasController.cs`; Test `IntegrationTests/QualityCapaPostgresTests.cs`

**Interfaces:** `CreateAsync({ Title, Description, SectionId?, SourceRecordId?, Links[], OwnerUserId })` — `Quality.Approve` or `Quality.Investigate`; `SavePlanAsync(id, { Actions[{ Kind, Description, ResponsibleUserId, DueDate }], EffectivenessCriteria, EffectivenessMethod, EffectivenessPlannedDate })` — owner, status `Plan`, at least one action, planned date after the last action due date; `ApprovePlanAsync(id, password)` / `ReturnPlanAsync(id, comment)` — `Quality.Approve`, not raiser or owner → `PlanApproved`; `CompleteActionAsync(actionId, { Evidence, EvidenceLinkId? })` — the responsible user only, status `PlanApproved`; `VerifyCompletionAsync(id, password)` — all actions done → `CompletionVerified`; `SignEffectivenessAsync(id, { Result, Findings, NewCapaId?, Password })` — lab date ≥ planned date; `NotEffective` needs `NewCapaId` naming a CAPA created with `SourceRecordId = id`; → `Closed`.

- [ ] **Step 1:** Failing tests: full path with triad at each signed step; plan without action or without effectiveness check refused; action completed by someone else refused; action edit after plan approval refused; verify with an open action refused naming it; `Effectiveness_refused_before_planned_date`; `Not_effective_requires_linked_new_capa`; raiser / owner cannot approve, verify or sign effectiveness; deviation closure now allowed once this CAPA's plan is approved.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(quality): CAPA with effectiveness check`.

### Task 6: Change control

**Files:** `ChangeControlService.cs`, `ChangeControlsController.cs`; Test `IntegrationTests/QualityChangeControlPostgresTests.cs`

**Interfaces:** `ProposeAsync({ Title, Description, Reason, SectionId?, Affected[{ TargetType, TargetId?, NewRecordCode? }] })` — role type Section Head, QC Manager or QA with `Quality.Raise`; affected targets limited to `Item`, `Specification`, `TestDefinition`, `Equipment`, `DocumentMaster` (stored as links with `IsAffectedByChange = true`); `SaveAssessmentAsync(id, { ImpactSummary, Tasks[…] })` + checklist answers; `SignAssessmentAsync(id, password)` — `Quality.Investigate`, every active checklist item answered → `Assessed`, signs `ChangeImpactAssessed`; `ApproveAsync(id, password)` / `RejectAsync(id, reason, password)` — `Quality.Approve`, not raiser or owner → `ApprovedForImplementation` / `Rejected`; `CompleteTaskAsync(actionId, evidence)`; `CloseAsync(id, { PostChangeReview, UnchangedJustification?, Password })` — all tasks done; every affected `Specification` / `TestDefinition` link has `ChangedAtUtc`, or `UnchangedJustification` is given. Adding an affected record after approval is refused (a new change control is needed).

- [ ] **Step 1:** Failing tests: full path with triad; analyst cannot propose; unanswered checklist refuses assessment signature; reject; affected list frozen after approval; close refused with open task; close refused with unchanged affected record and no justification, allowed with it; raiser cannot approve or close.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(quality): change control`.

### Task 7: Complaint

**Files:** `ComplaintService.cs`, `ComplaintsController.cs`; migration adding a "Complaint" row to `CausesOfTesting` if absent (and to `DbSeeder`); Test `IntegrationTests/QualityComplaintPostgresTests.cs`

**Interfaces:** `ReceiveAsync({ Title, Source, ReceivedOn, ExternalReference?, ItemId?, LotText?, ComplaintText, SectionId? })` — `Quality.Approve`; `RequestInvestigationAsync(id, { ClassificationId, OwnerUserId, DueDate?, WhatToTest })` → `Investigation`; `SaveInvestigationAsync(id, { Findings })` + sample links (samples are received through normal receiving with cause "Complaint"; this service only links them); `SignLabReportAsync(id, password)` — `Quality.LabConclude`, signer ≠ owner → `LabReported`; `ConcludeAsync(id, { Outcome, Summary, Password })` — `Quality.Approve` → `Concluded`, signs `ComplaintConcluded`; `CloseAsync(id, password)`. No change to receiving or testing code.

- [ ] **Step 1:** Failing tests: full path with triad; laboratory user cannot receive; lab report by the owner refused; conclusion without lab report refused; linked sample not readable by a viewer shown as number only; "Complaint" cause present after migration and seed, once.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(quality): complaints`.

### Task 8: Section and version checks on master-data writes (pre-gate hardening)

> **Moved to package G5** (`docs/superpowers/plans/2026-11-11-masterdata-write-scope-fix.md`), to be done early. If G5 is already merged, skip this task; otherwise implement G5 here. The text below is kept for reference.

**Files:** `MasterData/TestWorkflowStepMasterDataService.cs` (constructor takes only the context; add `IUserSectionScopeService` and a user id parameter to each method), `TestDefinitionMasterDataService.cs` (`FreezeTestDefinitionAsync(int id)` L956, `UnfreezeTestDefinitionAsync(int id)` L965), their controllers; Test `IntegrationTests/MasterDataWriteScopePostgresTests.cs`

**Why:** these writes have no laboratory check today: a holder of `MasterData.Manage` can change steps or freeze a test of another laboratory. The gate in Task 9 needs the laboratory of every write, so this is fixed first and reported to the user as a security fix.

**Interfaces:** every method gains `int currentUserId` and resolves `TestDefinitions.SectionId` (via `step.TestDefinitionId` for steps), then requires it in `GetConfigurableSectionIdsAsync(currentUserId)`; freeze, unfreeze and step update / delete / move call `RecordVersion.EnsureCurrent`. Controllers pass the user id and forward `If-Match`.

- [ ] **Step 1:** Failing tests, one per method (`UpdateWorkflowTypeAsync`, `CreateTestWorkflowStepAsync`, `UpdateTestWorkflowStepAsync`, `MoveTestWorkflowStepAsync`, `DeleteTestWorkflowStepAsync`, `FreezeTestDefinitionAsync`, `UnfreezeTestDefinitionAsync`): Section Head of another laboratory refused with nothing persisted and no audit row; own laboratory succeeds with audit; stale version refused where a version applies.
- [ ] **Step 2:** FAIL → implement → PASS; update the frontend service calls to send `If-Match` where newly required and their tests. Commit `fix(masterdata): enforce laboratory scope and record version on workflow step and freeze writes`.

### Task 9: Master-data change-control gate

**Files:** `IChangeControlContext` (scoped; `string? Number`), `API/Middleware/ChangeControlHeaderMiddleware.cs` (reads `X-Change-Control`, trims, max 20 chars; registered next to `RecordVersionMiddleware`; scoped service resolved in `InvokeAsync`), `ChangeControlGate.cs`; Modify `SpecificationMasterDataService` (`CreateSpecificationAsync` L73, `UpdateSpecificationAsync` L142, `DeleteSpecificationAsync` L214), `TestDefinitionMasterDataService` (create L41, update L424, freeze, unfreeze), `TestWorkflowStepMasterDataService` (all five writes); section settings endpoint; Test `IntegrationTests/ChangeControlGatePostgresTests.cs`

**Interfaces:**
- `Task<ChangeControlGrant?> EnsureAllowedAsync(int sectionId, QualityLinkTargetType targetType, int? targetId, string? newRecordCode, CancellationToken ct = default)` — returns null when the section's `ChangeControlRequired` is false. Otherwise requires `IChangeControlContext.Number`: missing → `"This laboratory requires an approved change control for this change."`; not found → `"Change control {n} was not found."`; status ≠ `ApprovedForImplementation` → `"Change control {n} is not approved for implementation."`; its `SectionId` is neither null nor this section → `"Change control {n} belongs to another laboratory."`; no affected link matching (`targetType`, `targetId`) — or `newRecordCode` for a create — → `"Change control {n} does not list this record."`
- A workflow step write is checked against its `TestDefinition`; a specification against the `Specification` id (update, delete) or, for create, against the affected `Item` **or** `TestDefinition`.
- `Task RecordAppliedAsync(ChangeControlGrant grant, CancellationToken ct = default)` — sets the link's `ChangedAtUtc`; called before the service's own `SaveChangesAsync` so both commit together. The audit rows of the change carry the change control number in `Reason` (append to an existing reason).
- Setting: `GET/PUT api/quality/section-settings/{sectionId}` — `Quality.Approve`; change audited.

- [ ] **Step 1:** Failing tests: `Gate_off_changes_nothing` (all writes work with no header); `Every_gated_write_is_refused_without_change_control` — theory over the twelve methods; one test per refusal message; `Change_control_of_other_laboratory_refused`; site-wide change control accepted; `Approved_change_control_allows_listed_record_and_marks_link_changed` (state, link `ChangedAtUtc`, audit reason contains the number); `Create_matches_by_new_record_code`; `Closed_change_control_no_longer_allows_changes`; `Refused_write_persists_nothing`; setting change audited and limited to QA.
- [ ] **Step 2:** FAIL → implement → PASS; run the existing master-data suites. Commit `feat(quality): change-control gate on specifications, tests and workflow steps`.

### Task 10: Queries, report, trending, notifications, overdue job

**Files:** `QualityQueryService.cs`, `QualityReportBuilder.cs`, `QualityNotifier.cs`, `API/BackgroundServices/QualityOverdueWorker.cs` (pattern: `MonitoringDeviationWorker.cs`; catalog entry in `BackgroundJobCatalog.cs`, classification Gxp; config `QualityOverdue:Enabled`, `:IntervalMinutes` default 60); Test `IntegrationTests/QualityQueriesPostgresTests.cs`

**Interfaces:** `GET api/quality/records` (type, status, classificationId, sectionId, ownerUserId, overdue, from, to, page, pageSize ≤ 100); `GET api/quality/records/{id}` (full DTO with path steps, signed timeline: signer full name, UTC time, meaning text); `GET …/{id}/pdf` (via `IPdfGenerator.GenerateReportAsync`, export logged); `GET api/quality/for/{targetType}/{targetId}`; `GET api/quality/trends?groupBy=type|classification|cause|section|item|source&from&to` with time-to-close, on-time rate, CAPA effectiveness rate, repeat causes (`repeatWindowDays`); `GET api/quality/dashboard` (waiting for my decision, my open records and actions, overdue, effectiveness checks due). Notifier events after commit: raised, assigned, returned, awaiting QA, action assigned, overdue (worker; one per record or action per day, deduplicated by a key stored on the notification or by C5's dedupe key), closed. Recipient resolution reuses D1's `OosRecipientResolver` generalised to `IPermissionRecipientResolver`.

- [ ] **Step 1:** Failing tests: filters, paging clamp, scope; analyst visibility; PDF text shows each signer name, time with zone and meaning; each `groupBy`; repeat-cause detection; dashboard per role; worker creates one overdue notification per day per item and none when not overdue; notifier failure does not undo a committed step.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(quality): queries, report, trending and notifications`.

### Task 11: Frontend — register and record page

**Files:** `services/qualityService.ts`; `modules/quality/QualityRegisterPage.tsx`, `QualityRecordPage.tsx`, `components/PathStepper.tsx` (MUI `Stepper`, text labels, current step announced), `SignedTimeline.tsx` (reuse D1's if present), `LinksPanel.tsx`, `ActionsPanel.tsx`, `AttachmentsPanel.tsx`, `CommentsPanel.tsx`, `ExtensionDialog.tsx`; `routes` (`/quality`, `/quality/:id`), `menuConfig.ts` (group `QUALITY_COMPLIANCE_GROUP`, permission `Quality.View`); tests beside each

- [ ] **Step 1:** Failing tests (`userEvent`, real components, mocked `services/` only): tabs per type and filters; stepper shows the current step; link picker uses global search and shows restricted links as number only; action completed with evidence; extension request and decision; attachment upload errors; signed timeline content; read-only by permission; all nine UI states incl. partial outage when one panel fails.
- [ ] **Step 2:** FAIL → implement → lint, types, tests, debt check PASS. Commit `feat(ui): quality register and record page`.

### Task 12: Frontend — type forms and entry points

**Files:** `modules/quality/deviation/*`, `capa/*`, `changeControl/*`, `complaint/*` (one form component per step), `RaiseDeviationButton.tsx` + `QualityRecordsPanel.tsx` added to the sample, test, equipment, OOS investigation and monitoring deviation screens; `QualityConfigPage.tsx` (templates, classifications, due rules, section settings); QA and Section Head dashboard tiles; tests

- [ ] **Step 1:** Failing tests: each step form sends the exact request and version; signature dialogs show the server meaning and keep open on a wrong password; server refusals shown as text beside the action; "CAPA required" closure message; effectiveness form disabled before the planned date with the reason; raise from a sample pre-links it; OOS deviation request opens a pre-filled deviation; configuration save with version.
- [ ] **Step 2:** FAIL → implement → full frontend checks PASS. Commit `feat(ui): quality record forms and entry points`.

### Task 13: Frontend — change-control prompt on master-data saves

**Files:** `services/apiClient.ts` (optional per-request header `X-Change-Control`), specification, Test Master and workflow-step save flows; `components/ChangeControlPrompt.tsx`; tests

**Interfaces:** When a save is refused with one of the gate messages, the prompt opens: a search field listing change controls "Approved for implementation" for that laboratory (`GET api/quality/records?type=ChangeControl&status=ApprovedForImplementation&sectionId=`), the refusal text, and Retry, which repeats the same request with the header. The chosen number is remembered for the editing session of that record only. If the section setting is known to be on, the prompt is shown before the first save.

- [ ] **Step 1:** Failing tests: refusal opens the prompt with the server text; retry sends the header and the unchanged body and version; "does not list this record" message shown and the save not repeated automatically; nothing shown when the gate is off.
- [ ] **Step 2:** FAIL → implement → full frontend checks PASS. Commit `feat(ui): change-control prompt on master-data saves`.

### Task 14: Documents and verification

- [ ] **Step 1:** `BusinessRules.md`: new section for quality records (paths, separation rules, gate). If A1–A3 exist: URS / FS area `QLT`, risk rows and tags, manual chapter, QA and Section Head role-guide tasks, four SOP templates (deviation, CAPA, change control, complaint handling). Otherwise list as follow-ups.
- [ ] **Step 2:** Full verification per `CLAUDE.md` section 2 with PostgreSQL; exact counts; `has-pending-model-changes`; frontend lint, types, tests, debt check, build.
- [ ] **Step 3:** Report to the user: the three manual scenarios of spec section 7; the security fix of Task 8; that items are not covered by the gate; that due rules and checklists ship empty.

## Cross-plan corrections (whole-programme review, 2026-11-11)

These corrections come from reviewing all plans together against the code. Where they differ from the text above, they win. Shared names are defined once in `docs/superpowers/plans/2026-11-11-build-order.md` (section "Programme conventions").

1. Task 1: `QualityRecord.RaisedByUserId` is **nullable** from the start, with `RaisedBySystemProcess?`; the separation rule treats a null raiser as 'System' (no person is excluded by it). D3 no longer changes this.
2. Root-cause categories reuse D1's `OosCauseCategory` (shared list); no separate table.
3. Numbering uses `AdvisoryLockKeys.QualityDeviation`, `QualityCapa`, `QualityChangeControl`, `QualityComplaint`.

## Review corrections (independent review, 2026-11-11)

From the independent review of the build order. These win over the text above and over the earlier corrections section where they differ.

1. Task 7's migration is named `AddComplaintCauseOfTesting`.

**Translation (build order 2.7):** if F4 Task 4 is merged when this package is built, every new screen uses translation keys and every new refusal carries a message code.
