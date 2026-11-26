# MicroLIMS — Business Rules (Frozen Logic)

Every rule below is implemented in exactly one place in the codebase.
If a rule needs to change, change it there — never re-implement it in
a controller, a frontend component, or a second service. File
references are given so this document and the code can be cross-checked.

---

## 1. Frozen Architectural Principles

1. **Configuration drives behavior.** The Section Head's Master
   Configuration (`Item.AssignedTests`) determines what tests are
   auto-created — nothing is created manually.
2. **Workflow before implementation.** Every feature is discussed,
   prototyped, approved, and frozen before coding.
3. **Backend owns laboratory logic.** The frontend never implements
   GMP or microbiology rules — it only collects input and displays
   what the backend decides.
4. **Role separation.** System administration and laboratory
   administration remain separate (`RoleType` enum: SystemAdministrator,
   SectionHead, Reviewer, Analyst).
5. **Traceability.** Every action is auditable
   (`MicroLimsDbContext.SaveChanges` + `WorkflowHistory`).
6. **Consistency.** Product, Water, EM, and After Cleaning share a
   common workflow-engine shape (`IStatefulWorkflowEngine`) while
   keeping domain-specific rules.
7. **Automation.** Repetitive laboratory tasks (test order generation,
   audit logging, average calculation) are automated.

---

## 2. Automatic Test Order Generation

**File:** `Application/Workflows/ProductWorkflowEngine.cs` (and the
Water/EM/AfterCleaning equivalents)

```
Receive Sample → Read Item Configuration → Generate Test Orders → Assign Status → Return Workspace Cards
```

- A sample cannot be received against an Item with zero assigned
  tests — `ProductWorkflowEngine.ReceiveAsync` throws.
- One `TestOrder` is created per configured test, starting at
  `WorkflowStep.Waiting` / `ApprovalStatus.Pending`.
- **No user, at any role, can manually create a TestOrder.** The only
  entry points are the five `ReceiveAsync` methods on the workflow
  engines.

---

## 3. Pathogen Engine

**File:** `Application/Workflows/PathogenWorkflowEngine.cs`

**Universal chain** (any pathogen except Salmonella):

```
TSB → Observation → Continue → Detection Media → Growth = Detected / No Growth = Absent
```

- The chain completes as soon as one `PathogenObservation` is recorded.
- The interpretation is the growth/no-growth value of that single observation.

**Salmonella exception:**

```
TSB → RVS → XLD+TSI → Detected / Absent
```

- Steps must be recorded strictly in order (`TSB` → `RVS` → `XLD_TSI`).
  Recording out of order throws `InvalidOperationException`
  ("workflow order violation").
- A **negative result at TSB or RVS closes the chain early** as
  `Absent` — no further steps can be recorded once closed.
- Final result is `Detected` only if TSB, RVS, and XLD+TSI are all
  positive/positive/positive respectively; otherwise `Absent`.
- `InterpretAsync` throws if called before the chain is complete —
  there is no partial/best-guess interpretation.

---

## 4. Water Calculation Engine

**File:** `Application/Workflows/WaterWorkflowEngine.cs`

```
Enter readings → Average → Compare against Alert → Action → Specification limits
```

- Comparison order of severity, most severe first: **Specification →
  Action → Alert**. The first (most severe) limit exceeded wins; a
  result can only be flagged with one status.
- Status values: `WithinLimits`, `AlertLimitExceeded`,
  `ActionLimitExceeded`, `OutOfSpecification`.
- Limits are read from `SamplingConfiguration`, matched by
  `TestCode` + the sample's `WaterSamplingPoint`.
- At least one numeric reading is required — throws otherwise.

---

## 5. EM (Environmental Monitoring) Engine

**File:** `Application/Workflows/EMWorkflowEngine.cs`

```
Room Selection → Incubation Step 1 → Incubation Step 2 → Average → Automatic OOT Detection
```

- Step 2 cannot start before Step 1 is completed
  (`CompletedAt is not null`) — enforced, not just UI-guided.
- A step cannot be completed twice — throws
  "workflow order violation" if attempted.
- **OOT (Out Of Trend) rule:** `(Step1Count + Step2Count) > ActionLimit`.
  This is computed automatically on Step 2 completion and persisted to
  `RoomMonitoring.IsOutOfTrend` — never left to human judgment.

---

## 6. After Cleaning Engine

**File:** `Application/Workflows/AfterCleaningWorkflowEngine.cs`

```
Machine → Selected Parts → ONE collective pathogen sample (if configured) + ONE individual TAMC per part
```

- Collective pathogen sampling is opt-in per `MachinePartConfiguration.IsCollectivePathogenSample`
  — if none of the selected parts require it, no collective TestOrder is created.
- Every selected part always gets its own individual TAMC TestOrder
  (`TestCode = "TAMC:{partName}"`), regardless of the collective sample.
- At least one part must be selected — throws otherwise.

---

## 7. GPT (Growth Promotion Test) Engine

**File:** `Application/Workflows/GptWorkflowEngine.cs`

```
Media Preparation → Sterility → Recovery → Release (or Rejected)
```

- Stages advance strictly forward, one at a time — cannot skip or go
  backward. Advancing from `Release` or `Rejected` throws.
- At the **Recovery** stage, at least one `GptChallengeResult` must be
  recorded before advancing — throws otherwise.
- **A media lot passes GPT only if every recorded challenge organism
  passed.** If any organism failed, the stage becomes `Rejected`
  instead of `Release`, and the lot can never be released.
- `Media.IsReleasedForUse` is `true` only when `GptStage == Release`
  **and** `Status == Active` **and** not expired — all three
  conditions, checked together (`IsReleasedForUseAsync`).

---

## 8. Review Engine

**File:** `Application/Services/ReviewService.cs`

- A test order cannot be reviewed before its status is `ResultEntered`
  — throws otherwise.
- Two modes, both ending in the same `Reviewed` state:
  - **Detailed** — one test order at a time, full workflow history visible.
  - **Quick table review** (`QuickReviewBatchAsync`) — reviews a batch;
    silently skips any test order in the batch that isn't eligible
    (still `Waiting`, etc.) rather than failing the whole batch.

---

## 9. Approval Engine

**Files:** `Application/Services/SampleApprovalService.cs`, `Application/Services/Oos/OosGate.cs`, `Application/Services/Oos/OosRetestPlanService.cs`, `Application/Services/Oos/OosInvestigationService.cs` (and partial files `.PhaseOne.cs`, `.Conclusion.cs`, `.Excursion.cs`)

*Draft — pending approval (updated 2026-11-25 to match the code as built, including D1 OOS investigation and CONF-209..220).*

- **Per-section decision (`DecideCoreAsync`):** A laboratory section cannot be decided before its `SampleSectionSignoff` status is `UnderApproval` and the acting user is a member of that section (`SampleSectionRollup.CheckSection`).
- **Reachable decisions:** Only four of the six `ApprovalDecision` enum values are reachable at sample approval: `Approve`, `Reject`, `RetestRetainedSample`, and `NewSampleRequest`. `Investigation` and `OOSInvestigation` remain in the `ApprovalDecision` enum as persisted integer values for historical records and `PathogenConfirmationRecorder` workflow events, but are not offered on the approval screen and are rejected by `DecideCoreAsync` (`Sample.InvalidApprovalDecision`).
- **Comments and signatures:** Comments are optional on all four approval decisions. Every decision records a `ReviewWorkflowEvent` (`ApprovalDecisionMade`) and an `ElectronicSignature` (`Approved`, `Rejected`, `RetestRequested`, or `NewSampleRequested`) in the same transaction before state changes are saved.
- **Segregation of duties:** The decider cannot have overridden competency on the sample, cannot be the section's reviewer (`ReviewedByUserId`), and cannot have performed any current test order in the section (`SegregationOfDutiesGuard`).
- **OOS gate (`OosGate.CheckSectionMayBeDecidedAsync`):** Before signing, the gate enforces:
  - Direct `RetestRetainedSample` and `NewSampleRequest` decisions are refused (`Oos.RetestNeedsApprovedPlan`) when any `Oos`-kind investigation covers the sample and section; retests must go through a signed `OosRetestPlan` instead.
  - `Approve` and `Reject` are refused (`Oos.InvestigationOpen`) while any covering `Oos`-kind investigation is not `Concluded`.
  - Any current reviewed test order in the section with outcome `Fails` (`OosOpeningRule.ReadFailingOrdersAsync`) that lacks an `Oos`-kind `OosInvestigationResults` link blocks approval/rejection (`Oos.FailingResultWithoutInvestigation`).
  - For `Approve` only:
    - Refused if any covering `Oos`-kind investigation is `Concluded` with `Confirmed` (`Oos.ConfirmedCannotBeApproved`).
    - Refused if any current test order fails and the sample is linked to a signed `OosRetestPlan` for that section (`Oos.FailingRetestCannotBeApproved`, CONF-219).
    - Refused if any `Oos`-kind investigation on the sample and section is `Concluded` with `Invalidated` while any sample linked to its signed retest plan(s) is not `SampleStatus.Approved` (`Oos.RetestSamplesNotApproved`).
- **Excursions are separate:** `Excursion`-kind reviews (`OosInvestigationKind.Excursion`) are opened for `ActionLimitExceeded` (and `AlertLimitExceeded` when enabled on the section's Excursion template, unless the sample belongs to a `MonitoringOccurrence`) or manually via `OpenManualExcursionAsync`. `OosGate` filters strictly on `OosInvestigationKind.Oos`; excursion reviews never block sample approval or certificates (CONF-209).
- **Approval-only technical checks:** When `decision == Approve`, every test requiring system suitability must be an HPLC workspace test (`HplcMethodAssay` or `Dissolution`) with a passed SST run, and every analysis test must have an active `TestAnalysis` with no `NextStageRequired` parameter results.
- **Signed retest plans (`OosRetestPlanService`):** For an `Oos`-kind investigation in `AwaitingRetest` or `PhaseTwo` (after Phase I is signed), `DraftAsync` saves a plan of 1 to 10 retests per revision (no cumulative cap, CONF-218) on current, non-superseded failing orders linked via `OosInvestigationResults`, with a judging rule and one assignable analyst per sample (for `NewSample`, no analyst who performed an investigated order; for `RetainedSample`, the original analyst is eligible). Amendments (`Revision > 1`) require an `AmendmentReason`. `SignAndCreateAsync` signs `OosRetestPlanApproved` (signer cannot be in the analyst list or have performed any investigated or retest order) and creates the linked retest samples with `OosGroupCode = investigation.Number` via `RetestSampleFactory` without superseding original orders or changing the origin section signoff status. If a planned retest sample later fails at review, it joins the investigation and moves `AwaitingRetest` to `PhaseTwo` (CONF-219).
- **Signed conclusion replacing automatic finalisation:** `SampleApprovalService.FinalizeIfClosedAsync` calls `FinalizeClosedSampleAsync` (refreshing result projection approval fields and archiving the report document when `Sample.Status` is `Approved` or `Rejected`) and then `PropagateOosOutcomeAsync`. `PropagateOosOutcomeAsync` is skipped when the origin sample and section have an `Oos`-kind `OosInvestigation` row — closing retest samples does not automatically approve, reject, or finalise the origin sample. Instead, an independent Section Head must sign the investigation conclusion (`OosInvestigationService.ConcludeAsync`, meaning `OosInvestigationConcluded`):
  - `Invalidated` (requires a signed retest plan with created samples, all planned retest samples' section reviews completed, and an assignable cause category other than `IsNoCauseFound`) marks only the origin sample's investigated orders `IsSuperseded = true` and `OosInvestigationResults.IsInvalidated = true` (CONF-219).
  - `Confirmed` reverses any remaining origin invalidation and supersession flags (CONF-219), leaving `Approve` blocked and `Reject` allowed.
  - A signed conclusion can be reopened (`ReopenAsync`, meaning `OosInvestigationReopened`) only while the origin section is not yet `Approved` or `Rejected`, reversing origin invalidation/supersession flags and returning the investigation to `PhaseTwo` while preserving the prior conclusion in `OosInvestigationReopenings` (CONF-219).
  - For legacy retest chains without an `Oos`-kind `OosInvestigation` row, `PropagateOosOutcomeAsync` still mirrors the closed retest outcome to the origin section and calls `FinalizeClosedSampleAsync` when the origin closes.
- **Certificate gate (`OosGate.EnsureCertificateAllowedAsync`):** Certificates are blocked while any covering `Oos`-kind investigation is not `Concluded`, and also when a `Concluded`/`Invalidated` investigation on the sample has more than one distinct sample linked across signed retest plans (CONF-220: reporting rule for multiple retests is open).

---

## 10. Security Rules

**File:** `Application/Services/AuthenticationService.cs`

- **Account locking:** 5 failed login attempts locks the account for
  15 minutes (`MaxFailedAttempts` / `LockDuration`). A locked account
  cannot log in even with the correct password until the lock expires.
- **Refresh tokens** rotate on every use — the old token is revoked
  and a new one issued (`RefreshAsync`), 7-day lifetime.
- **Password reset tokens** are single-use (`UsedAt` set on
  consumption), 1-hour lifetime, and the API never reveals whether a
  given username exists (`RequestPasswordResetAsync` always returns
  a generic success message).
- Every login attempt — success or failure, with reason — is recorded
  in `LoginHistory`.

### 10.1 Quality Assurance independence

**Files:** `Shared/Constants/SegregationOfDutiesRules.cs`,
`Application/Services/RoleService.cs`

- A role of type Quality Assurance (the system role `QA` and any custom
  role based on it), and any role holding `Audit.Review` or
  `Access.Review.Sign`, can never hold an administration permission or
  a laboratory execution or approval permission. The only approval
  permissions it may hold are `Documents.Review` and
  `Documents.Approve`. The forbidden list is
  `SegregationOfDutiesRules.QaForbiddenCodes`.
- The rule is checked on the server when a role is created and whenever
  its permissions are changed. A refused change returns the message
  "A Quality Assurance role cannot hold: <codes>."
- Quality Assurance reads all laboratories through `Sections.ViewAll`
  and has no section membership, so every write that needs membership
  is refused.

### 10.2 Access review signer

**File:** `Application/Services/AccessReviewService.cs`

- Recording findings on, and signing, an access review needs
  `Access.Review.Sign`.
- While no active, unlocked user holds `Access.Review.Sign`, the
  administrator who manages access reviews may still decide and sign,
  so a site is not blocked on the day of upgrade. From the moment such a user exists, anyone
  without `Access.Review.Sign` is refused with "Access reviews are
  decided and signed by Quality Assurance."
- A person named in a finding cannot sign the review (unchanged).

### 10.3 Audit trail review

**File:** `Application/Services/AuditTrailReviewService.cs`

- A review fixes its scope when it is started: period, scope, the
  highest audit entry ID at that moment (the cut-off) and the entry
  count. Entries written later are not part of the review; the review
  reports how many newer entries exist.
- Starting a review, recording findings, signing and closing need
  `Audit.Review`. Reading reviews needs `Audit.View`.
- A review is completed by an electronic signature (user ID and
  password) with the meaning "Audit trail reviewed". A signed review
  and its findings cannot be changed or deleted; PostgreSQL triggers
  enforce this.
- The scope columns cannot be changed after the review is started, even
  before it is signed.

### 10.4 Quality Assurance approver for controlled documents

**Files:** `Application/Services/DocumentControl/DocumentApprovalService.cs`,
`DocumentEffectiveDateService.cs`

- A Quality Assurance user may be assigned as reviewer or approver of a
  document. Section Head override powers are not given to Quality
  Assurance.
- A document type may be set to require a Quality Assurance approver
  (`DocumentType.RequiresQaApprover`, off by default). When it is on, a
  revision of that type can only be approved by a Quality Assurance
  user. The author of a revision cannot approve it (unchanged).

---

## 11. Audit Trail

**File:** `Persistence/DbContext/MicroLimsDbContext.cs`

- Every `Add`/`Update`/`Remove` tracked by EF Core is captured
  automatically in `AuditLog` on `SaveChanges` — **no service can
  forget to log a change**, because it isn't manual.
- Records the entity name, entity ID, action, previous value (JSON),
  new value (JSON), user ID, and UTC timestamp.
- `WorkflowHistory` additionally captures test-order-specific state
  transitions with a human-readable note.
- **Nothing is ever hard-deleted from these two tables** — treat them
  as append-only.

---

## 12. Dashboard Notification Triggers

**File:** `Application/Services/DashboardNotificationService.cs`

| Trigger | Condition | Severity |
|---|---|---|
| Media Expiry | `Media.ExpiryDate` within 7 days (or already past) | `warning` / `error` if already expired |
| Incubation Ready | An `Incubation` with `CompletedAt` set, whose `TestOrder` is still `Incubating`/`Running` | `info` |
| Approval Waiting | Any `TestOrder.Status == Reviewed` (shown to SectionHead/Admin only) | `info` |
| Review Waiting | Any `TestOrder.Status == ResultEntered` (shown to Reviewer/SectionHead/Admin) | `info` |

- Notifications are deduplicated per user within a 12-hour window (by
  exact message text) to avoid spamming the same alert repeatedly.
- `severity == "error"` notifications additionally trigger an email
  via `IEmailSender` (no-op if SMTP isn't configured) and a real-time
  push via `INotificationService`.

---

## 13. Analyst Competency Gate *(Draft — pending approval)*

**Files:** `Application/Services/Competency/*`, `Application/Services/ItemSopResolver.cs`, `Application/Services/AnalystAssignmentRules.cs`, `Application/Services/SegregationOfDutiesGuard.cs`, `Tests/ArchitectureTests/RecordingPointCoverageTests.cs`

### 13.1 Per-Laboratory Gate Switch & Impact Preview
- The competency gate is configured per laboratory section (`CompetencySectionSetting.GateEnabled`) and ships **OFF** by default in every laboratory (an absent row means the gate is off).
- Enabling or disabling the gate (`CompetencySettingsService.SetGateAsync`) requires `Competency.Manage`, section configure scope, a non-blank reason, optimistic concurrency (`expectedVersion`), and an electronic signature (`SignatureMeaning.CompetencyGateChanged`).
- `CompetencySettingsService.GetImpactAsync` previews all open, assigned, non-superseded test orders (`Pending`, `InProgress`, `RetestRequested`) in the section as if the gate were enabled, listing per-analyst refused orders and reasons (active overrides count as allowed).

### 13.2 Qualification Records & Requalification Intervals
- `QualificationService.QualifyAsync` (`Competency.Manage`, section configure scope) records an `AnalystQualification` with `Basis` (`ObservedPractice`, `ProficiencySample`, `PreviousQualification`, `Other`), `Evidence`, optional `EvidenceSampleId`, `QualifiedOn` (no later than `LabToday`), optional attachments (`QualificationAttachment`), and an electronic signature (`SignatureMeaning.AnalystQualified`).
- Self-qualification is refused; the analyst must be active and a member of the test's laboratory section.
- Expiry is `QualifiedOn.AddMonths(TestDefinition.RequalificationMonths)` (`DateOnly`), or no expiry when `RequalificationMonths` is null; a qualification is expired when `LabToday > ExpiresOn`.
- Requalification intervals (`RequalificationMonths`, 1–120 or null) are configured only through the Test Master update (`MasterData.Manage`, CONF-210). Moving a test to another laboratory in Test Master preserves existing qualifications, while assignment and recording still require membership in the test's current section (CONF-211).
- `QualificationService.WithdrawAsync` (`Competency.Manage`, section configure scope) withdraws a qualification once with a non-blank reason and electronic signature (`SignatureMeaning.AnalystQualificationWithdrawn`). `AnalystQualification` rows are protected against `DELETE` and `UPDATE` (except setting the four withdrawal columns once from null).
- The **latest** `AnalystQualification` record by `Id` governs each `(UserId, TestDefinitionId)` pair.

### 13.3 Guard Decision & SOP Resolution
- `CompetencyGuard` evaluates `(userId, testOrderId)` (or `CompetencySubject` for not-yet-created orders):
  1. **Gate off** for the section → Allowed.
  2. **Active override** (`CompetencyOverride` for that `(userId, testOrderId)` where `EndedAtUtc == null`, order is not superseded and not `Reviewed`, `Approved`, `Rejected`, `Voided`, or `Cancelled`) → Allowed (`ViaOverrideId`).
  3. **Qualification check** (latest record by `Id`): missing, withdrawn, or expired (`LabToday > ExpiresOn`) → Refused with coded reason.
  4. **SOP check** (when the sample has an `ItemId`, via `ItemSopResolver.ResolveManyAsync`):
     - `Effective` → Allowed if the user has a `DocumentTrainingAssignment` for the current effective revision with status `Acknowledged` or `CompletedPassed`, OR is in the **grace period** (acknowledged the immediately superseded revision AND holds an open assignment in status `Assigned`, `Reading`, or `KafPending` for the current revision with `DueDateUtc >= nowUtc`); otherwise Refused.
     - `NoSopNumber`, `NotFound`, `NoEffectiveRevision`, or `Ambiguous` (more than one active document master matches case-insensitively, CONF-212) → Allowed, with `SopNotChecked` text included in the audit entry's reason field when recording.

### 13.4 Guard Points (Recording & Assignment)
- **Recording points:** Every result-recording entry point calls `ICompetencyGuard` (`TestWorkflowSupport`, `IncubationStepRecorder`, `PathogenConfirmationRecorder`, `PathogenSessionService`, `GroupedTestActionService`, `SamplePreparationService`, and physicochemical/instrument recorders). `RecordingPointCoverageTests` enforces that every public method in `Application/Workflows` or `Application/Services` that writes a result entity calls `_competency` and `_sopCapture` (with `SopCapture` preceding the first write), unless explicitly exempted. Batch paths (`PathogenSessionService`, `GroupedTestActionService`) skip refused orders with their reasons and record allowed orders.
- **Assignment points** (`AnalystAssignmentRules.EnsureAssignableAsync` and `ICompetencyGuard`):
  - **Whole-sample assignment** (`SampleAssignmentService.PrepareSampleAssignmentAsync`): mixed qualification across a sample's tests is refused all-or-nothing (`Competency.AssignmentRefused`) with per-test reasons.
  - **Per-test and bulk assignment** (`AssignTestsAsync`, `SampleBulkAssignmentService`): refused for unqualified assignees; bulk assignment records failed rows with reasons.
  - **Self-assignment ("Take"), auto-assignment at workflow start, and new-sample requests at approval** (`TestSelfAssignmentService`, `EMWorkflowEngine`, `WaterWorkflowEngine`, `AfterCleaningWorkflowEngine`, `SamplePreparationService`, `SampleApprovalService`): unqualified users are refused before persisting.
  - **Automatic analyst carry-over** (sample correction inheritance in `SampleCorrectionService` and retained-sample retest in `SampleApprovalService`): unqualified carried-over analysts are cleared (left unassigned) and the action succeeds.
  - **Planned OOS retests** (`OosRetestPlanService`): no override is allowed; drafting and signing are refused until the analyst is qualified or replaced.
  - **Assignment options** (`GetAssignmentOptionsAsync`): lists qualified analysts first and includes per-test `CompetencyRefusals`.

### 13.5 Signed Override & Whole-Sample Separation of Duties
- `CompetencyOverrideService.GrantAsync` (`Competency.Override`, section write scope) grants a one-time, electronically signed (`SignatureMeaning.CompetencyOverride`) override for one analyst on one open (`Pending` or `InProgress`, not superseded, not outsourced, not an OOS retest plan sample) test order when the gate is on and the analyst is currently refused (refused as not needed if the gate is off or the analyst is already allowed).
- Self-override is refused, a user who has already reviewed or decided approval for the sample cannot grant an override, and at most one active override may exist per test order (partial unique index). `CompetencyOverride` rows are append-only (except setting `EndedAtUtc` and `EndReason` once).
- An override ends automatically when the test order is reviewed, cancelled, voided, superseded, or reassigned to a different analyst.
- **Whole-sample separation:** `SegregationOfDutiesGuard.DidUserOverrideCompetencyAsync` checks *all* overrides on the sample (including ended overrides) and blocks the granting user from reviewing (`Sample.CannotReviewOverrode`) or approving (`Sample.CannotApproveOverrode`) the whole sample, and excludes them from review/approval notifications. Review and approval DTOs carry the override manifestation details.

### 13.6 Qualification Matrix & Expiry Reminders
- `CompetencyMatrixService` (`Competency.View`) returns analysts × tests with cell statuses (`NotQualified`, `Qualified`, `ExpiringSoon`, `Expired`, `Withdrawn`) and effective item SOP code/revision; analysts see only their own rows even on the laboratory matrix endpoint.
- `CompetencyReminderService.GetExpiringAsync` returns qualifications expiring within `CompetencySectionSetting.ReminderDaysBeforeExpiry` based on `LabToday` (a withdrawn latest record never reminds).

---

## 14. SOP Revision Capture on Test Orders (Rule 3.1 Note) *(Draft — pending approval)*

**Files:** `Application/Services/SopRevisionCapture.cs`, `Application/Services/ItemSopResolver.cs`, `Application/Services/TestOrderSopDisplay.cs`, `Application/Services/CertificateOfAnalysisBuilder.cs`, `Application/Services/ReportDocumentMapper.cs`, `Tests/ArchitectureTests/RecordingPointCoverageTests.cs`

- **Immutable evaluation context (Rule 3.1):** When a sample has an `ItemId`, the item's effective SOP revision is captured once on each `TestOrder` (`SopRevisionCapture.CaptureIfNeededAsync`) at its first recorded step and never changes afterward. The PostgreSQL trigger `test_orders_sop_capture_immutable` rejects any modification to the eight capture columns once `SopCaptureStatus` is set; concurrent first-step writers lock the `TestOrders` row (`FOR UPDATE` on Npgsql) and adopt the already-stored capture (`AdoptStoredCaptureAsync`).
- **Ambiguous / unresolved SOPs:** If `ItemSopResolver` returns `NoSopNumber`, `NotFound`, `NoEffectiveRevision`, or `Ambiguous` (case-variant active document masters, CONF-212), the status and capture timestamp are recorded without blocking testing, and displays show `"SOP not recorded: {reason}"`.
- **Newer-revision notice (CONF-222):** Later effective revisions on the captured `DocumentMasterId` (with higher `RevisionSequence`, effective date after `SopCapturedAtUtc` and on or before `nowUtc`, while the test is open or before its `Ready` transition) never alter the captured test-order record; they only surface a non-blocking notice on the test workflow dialog, review screen, sample report, and Certificate of Analysis. Reassigning the item to a different SOP does not trigger a notice.
---

## 15. Quality Records (D2) *(Draft — pending approval)*

**Files:** `Application/Services/Quality/*` (`QualityRecordCore`, `DeviationService`, `CapaService`, `ChangeControlService`, `ComplaintService`, `QualityConfigService`, `QualityLinkService`, `QualityAttachmentService`, `QualityCommentService`, `QualityDueDateService`), `Domain/Entities/Quality/*`, `Domain/Enums/QualityEnums.cs`, `API/Controllers/Quality/*`, `API/Controllers/QualityRecordsController.cs`, `Shared/Constants/SegregationOfDutiesRules.cs`, `Persistence/Migrations/20261124201920_AddQualityRecords.cs`. Specification: `FS-QLT`; conflicts: CONF-223 to CONF-226 and CONF-228 to CONF-240.

Scope of this section: the merged D2 Tasks 1 to 8. Master-data change-control gate: see part 2 (D2 Task 9). Queries, reports, trending, notifications, the overdue job and the screens are not described here.

### 15.1 Types, numbers and status paths
- Four types, one root row (`QualityRecord`) and one detail row each (`DeviationDetail`, `CapaDetail`, `ChangeControlDetail`, `ComplaintDetail`). Numbers are `DEV-`, `CAPA-`, `CC-` and `CMP-` followed by the laboratory year and a four-digit sequence, counted per type and year under a per-type advisory lock (`ReferenceNumberGenerator.GenerateQualityNumberAsync`).
- Each type is created only by its typed service (`DeviationService` raise routes, `CapaService.CreateAsync`, `ChangeControlService.ProposeAsync`, `ComplaintService.ReceiveAsync`). There is no generic create route; `POST api/quality/{type}` was removed (CONF-230).
- **Deviation:** Raised → Assess → Investigation → lab conclusion → LabConcluded → Approve → Approved → Close → Closed. Raised → Reject → Rejected (final). LabConcluded → Return → Investigation.
- **CAPA:** Plan → approve plan → PlanApproved → verify completion → CompletionVerified → effectiveness check → Closed (for either result). Plan → return plan → Plan.
- **Change control:** Proposed → sign assessment → Assessed → Approve → ApprovedForImplementation → Close → Closed. Proposed or Assessed → Reject → Rejected (final).
- **Complaint:** Received → request investigation → Investigation → sign lab report → LabReported → Conclude → Concluded → Close → Closed.
- **Cancel:** `Quality.Approve` (QA by default), with reason and signature. Allowed from Raised, Investigation, LabConcluded and Approved (deviation; CONF-224); from any status except Closed and Cancelled (CAPA); from any status except Closed, Rejected and Cancelled (change control); from Received, Investigation, LabReported and Concluded (complaint). The record stays visible with `IsCancelled` and the reason (`QualityRecordCore.CancelAsync`).
- **Terminal states:** `QualityConfigService.IsOpenExpression` treats as not open: any cancelled record, deviation Rejected, Closed or Cancelled, CAPA Closed or Cancelled, change control Closed, Rejected or Cancelled, and complaint Closed or Cancelled. Checklist answers and extension requests refuse a record that is not open (`Quality.RecordTerminal`). The type's state steps refuse a cancelled record with the type's wrong-state message. Links, attachments and comments do not check the state (CONF-238).

### 15.2 Who may do each step
- Permissions: `Quality.View` (read), `Quality.Raise`, `Quality.Investigate`, `Quality.LabConclude`, `Quality.Approve`. Defaults are in FS-QLT-017. `Quality.Approve` is held by QA only; `Quality.Investigate` and `Quality.LabConclude` may not be held by QA (`SegregationOfDutiesRules.QaOnlyCodes`, `GxpExecutionAndApprovalCodes`).
- Raise a deviation, comment, link, upload: `Quality.Raise` (Section Head, Reviewer, Analyst, QC Manager, QA by default).
- Propose a change control: `Quality.Raise` and role Section Head, QC Manager or QA.
- Create a CAPA: `Quality.Investigate` or `Quality.Approve`. Receive a complaint: `Quality.Approve`.
- Assess, reject, return, approve, close and cancel a deviation, a CAPA step, a change-control step or a complaint step (except the lab report): `Quality.Approve`.
- Save the deviation investigation or complaint findings: the owner, or `Quality.Investigate` when the caller does not hold `Quality.Approve`.
- Lab conclusion (deviation) and lab report (complaint): `Quality.LabConclude` (Section Head and QC Manager by default).
- Save a CAPA plan and complete a CAPA action: the owner of the CAPA; the responsible person of the action.
- Save and sign a change-control assessment: `Quality.Investigate` (Section Head by default; CONF-236). Complete a change task: the responsible person.
- Request an extension: the record owner (record due date) or the action's responsible user (action due date) (CONF-237). Decide an extension: `Quality.Approve`.
- Remove a link or void an attachment: the record owner or `Quality.Investigate`.
- Save site checklist templates, classifications and due rules: `Quality.Approve`. Save a section checklist template: `Quality.Investigate` with configure scope for the section.
- The role types allowed per endpoint are listed in `backend/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt`. Policies are necessary but not sufficient: the service refuses where it makes the decision.

### 15.3 Separation rules
- **Raiser or owner cannot approve or close.** `QualityRecordCore.EnsureNotRaiserOrOwner` refuses with `Quality.RaiserOrOwner` on: deviation approve, return and close; CAPA plan approve and return, completion verification and effectiveness check; change control approve, reject and close; complaint conclude and close. It is not applied to deviation assessment or rejection, to any cancellation, or to the extension decision (CONF-228, CONF-229).
- A null `RaisedByUserId` never matches a user, so a record with no raising person (treated as "System") excludes no one.
- **Owner cannot sign the laboratory part.** `Deviation.OwnerLabConclusion` and `Complaint.OwnerLabReport`. Another user holding `Quality.LabConclude` signs. QA approval does not replace the laboratory signature (specification section 9, item 4).
- **Analyst cannot own a result-based deviation.** `Deviation.AnalystOwner`, when the analyst performed a linked test order or an investigated OOS order (`SegregationOfDutiesGuard`).
- **QA cannot own or act in the laboratory.** An owner or responsible person with role QA is refused (`Deviation.InvalidOwner`, `Complaint.InvalidOwner`, `Capa.InvalidOwner`, `Capa.ActionResponsibleInvalid`). A change-control owner must be an active Section Head or QC Manager.
- **Extension self-decision.** The requester cannot decide the request (`Quality.ExtensionSelfDecision`).

### 15.4 Signed steps and signature meanings
- A signed step calls `IElectronicSignatureService` with the user's identity and password, the meaning, the optional comment (4000 characters), and a snapshot of the content. It writes one append-only `QualityRecordStep` bound to the signature (FS-QLT-003, FS-QLT-004). Signed step rows, and signed extension requests, cannot be updated or deleted.
- **Deviation:** Assess → `DeviationAssessed`; Reject → `DeviationRejected`; lab conclusion → `QualityLabConclusion`; Approve → `QualityApproved`; Close → `QualityClosed`; cancel → `QualityCancelled`.
- **CAPA:** plan approval → `CapaPlanApproved`; verification → `CapaCompletionVerified`; effectiveness → `CapaEffectivenessChecked`; cancel → `QualityCancelled`.
- **Change control:** assessment → `ChangeImpactAssessed`; approval → `ChangeApproved`; rejection → `ChangeRejected`; closure → `QualityClosed`; cancel → `QualityCancelled`.
- **Complaint:** lab report → `QualityLabConclusion`; conclusion → `ComplaintConcluded`; closure → `QualityClosed`; cancel → `QualityCancelled`.
- **Extension decision:** `QualityExtensionDecided`.
- Not signed: receipt, investigation and findings saves, deviation return (the comment is kept as a `QualityComment`), CAPA plan save and return, action completion, change-control assessment save, task completion, extension request, links, attachments and comments.

### 15.5 Concurrency
- Every change to an existing record needs the current version in `If-Match`. A different version is refused with `Quality.Stale`. The record row is locked (`SELECT … FOR UPDATE`) before the version is read, and again inside every signed step.
- A missing `If-Match` is refused with `Quality.VersionRequired`.

### 15.6 Deviation rules
- Occurrence time must be UTC and not in the future (`Deviation.OccurrenceUtc`, `Deviation.FutureOccurrence`). Immediate action is required. Location is optional, up to 200 characters.
- Assessment requires `Accept = true`, an active classification and a valid owner. The due date is the one entered (not before the laboratory date) or the due rule's date (FS-QLT-007).
- The laboratory conclusion requires a complete investigation: impact assessment, root cause category (an active `OosCauseCategory`, shared with OOS), root cause description and proposed actions.
- **CAPA required (CONF-223).** Closure with `CapaRequired` needs a non-cancelled linked CAPA in PlanApproved, CompletionVerified or Closed. A cancelled CAPA never qualifies, even if its plan was signed before cancellation.
- Closure is refused while any action of the record is unfinished (`Deviation.UnfinishedActions`).
- **Source raises (CONF-224).** One non-cancelled deviation per monitoring deviation, audit finding, or already-addressed OOS request. A second raise is refused and the existing deviation is named.

### 15.7 CAPA rules
- A plan needs at least one action. Each action is Corrective or Preventive, has a responsible laboratory user and a due date not before the laboratory date. Effectiveness criteria, method and a planned date are required. The planned date must be after the latest action due date (`Capa.EffectivenessDateOrder`). Actions cannot be removed (`Capa.ActionCannotBeRemoved`).
- After the plan is approved, the planned fields of each action are protected by a database trigger (`ProtectApprovedCapaActions`), and so are the completion fields once recorded.
- Only the responsible person completes an action; completion needs evidence and may cite an evidence link of the same record. An action cannot be completed twice.
- Verification needs every action complete.
- The effectiveness check cannot be signed before the planned date by the laboratory date (`Capa.EffectivenessEarly`). A NotEffective result needs a new non-cancelled CAPA whose `SourceRecordId` is this CAPA (`Capa.NewCapaRequired`).

### 15.8 Change-control rules
- Affected records are of type Item, Specification, TestDefinition, Equipment or DocumentMaster. Workflow steps and document revisions cannot be selected as affected records (CONF-235). An existing record must be readable; a new one is named by a record code (up to 100 characters). Duplicates are refused.
- The affected list is frozen when the impact assessment is signed (CONF-226). The refusal is `ChangeControl.AffectedFrozen`; its English and Arabic texts currently say "after approval" (CONF-231).
- Assessment requires a saved impact summary and an answer to every active checklist item (`ChangeControl.ChecklistIncomplete`). Checklist answers are refused after signing (`ChangeControl.AssessmentAlreadySigned`).
- Tasks are laboratory users with due dates. Completion is by the responsible person with evidence. Closure needs every task complete and a post-change review.
- **Closure and unchanged records.** Closure needs an unchanged justification when a listed Specification or TestDefinition has no `ChangedAtUtc`. No merged code writes `ChangedAtUtc`, so in the merged build every such change control needs the justification (CONF-232).
- Writes to test workflow steps and to test freeze or unfreeze check laboratory scope (`TestConfigurationScope`). Updates, moves and deletes of steps, and the freeze and unfreeze, also check the record version (`RecordVersion.EnsureCurrent`). This is the Task 8 fix, commit 9ed6ced.
- Master-data change-control gate: see part 2 (D2 Task 9).

### 15.9 Complaint rules
- Receipt is by QA. The received date cannot be in the future (`Complaint.FutureReceivedOn`). An item, when given, must be active. The lot is free text (CONF-233).
- The laboratory report needs findings and a signer who is not the owner. The conclusion needs an outcome (Justified, NotJustified or Inconclusive) and a summary. The conclusion does not record a follow-up deviation or CAPA (CONF-234).
- Closure needs no unfinished action.

### 15.10 Due dates, classifications and extensions
- A due date is computed in working days from the rule for the record's type and classification, or the site rule. Working weekdays come from the monitoring settings and holidays from the laboratory calendar. With no rule, or an empty rule, there is no due date. A classification rule with an empty value does not fall back to the site rule (CONF-239).
- Classifications and due rules are set by `Quality.Approve`; every change is audited (CONF-225). Seeded classifications: Minor, Major, Critical.
- An extension is requested with a reason and a later date. QA decides with a signature. Approval replaces the record's or the action's due date in the same transaction. The request and its decision are kept; a decided request cannot change.
- Overdue listing: open records whose due date is before the laboratory date, and unfinished actions whose due date is before it. Notifications are not part of the merged code.

### 15.11 Links, attachments and comments
- A link is chosen from the system and its target must be readable by the user. A duplicate link is refused. Links can be removed only before the first signed step, and after that the core target columns are fixed by a database trigger.
- A link to a target the reader cannot read shows its number only.
- Attachments: allowed types with a content check, at most 10 MB, SHA-256 stored. A download is audited, and an integrity failure blocks delivery and is audited. Voiding keeps the row and the reason. Nothing is deleted.
- Comments: up to 2000 characters, no edit and no delete.
- Links, attachments and comments are not refused on a closed or cancelled record (CONF-238).

### 15.12 Reading scope
- A user reads a quality record with `Quality.View` and when the record is site-wide or in a laboratory section the user may read. A user without `Quality.Investigate`, `Quality.LabConclude` or `Quality.Approve` whose role is Analyst reads only the records they raised, own, or hold an action in. A record outside that scope is reported as not found (`Quality.NotFound`).

### 15.13 Queries, reports, trending, notifications and overdue job *(Draft — pending approval)*
- **Register and detail:** `GET api/quality/records` filters by type, status, classification, section, owner, overdue and raised date range, with paging normalised by `Paging.Normalize` (page size capped at 100). `GET api/quality/records/{id}` returns the full record detail (type detail, path steps, signed timeline with signer full name, UTC time with explicit `UTC` zone and `ReportVocabulary` meaning text, actions, attachments, links count). `GET api/quality/for-target/{type}/{targetId}` lists readable records linked to a target. All queries use `AsNoTracking` and SQL projections under `QualityRecordCore.ReadableRecords`.
- **PDF report:** `GET api/quality/records/{id}/pdf` generates a PDF via `IPdfGenerator.GenerateReportAsync` showing record identity, type details, actions, resolved links (restricted links show number only), and electronic signatures (printed name, username, role, UTC time with zone, meaning text). Every export is logged in `DataExportLogs` (`QualityRecordPdf`).
- **Trending:** `GET api/quality/trends` groups readable records by `type`, `classification`, `cause`, `section`, `item` or `source` (invalid `groupBy` refused with `Quality.TrendGroupByInvalid`), computing total/open/closed counts, average time to close, on-time rate, CAPA effectiveness rate, and repeat causes (same root cause category and linked item or equipment within `repeatWindowDays`).
- **Dashboard:** `GET api/quality/dashboard` returns role-scoped waiting-for-decision records, my open records and actions, overdue records and actions, and CAPA effectiveness checks due.
- **Notifications and overdue worker:** `QualityNotifier` sends post-commit notifications (`QualityRecordRaised`, `QualityRecordAssigned`, `QualityRecordReturned`, `QualityAwaitingQa`, `QualityActionAssigned`, `QualityRecordClosed`) via `IPermissionRecipientResolver` (generalised from `OosRecipientResolver` without changing OOS behaviour); a notifier failure is logged and never rolls back a committed step. `QualityOverdueWorker` (classified `Gxp` in `BackgroundJobCatalog`, configured via `QualityOverdue:Enabled` and `QualityOverdue:IntervalMinutes` default 60) sends one `QualityRecordOverdue` / `QualityActionOverdue` notification per open overdue item per laboratory day using `NotificationLog.DedupeKey` and the injected `ILabClock`.
