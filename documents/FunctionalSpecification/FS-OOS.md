# FS-OOS Functional Specification

| Field | Value |
|---|---|
| Document number | FS-OOS |
| Title | Out-of-specification tracking functional specification |
| Version | 0.2 |
| Status | Draft — pending approval |
| Author | Claude (AI agent), draft from code |
| Reviewer | To be assigned by QA |
| Approver | To be assigned by QA |
| Effective date | Set when Approved |

## Change history

| Version | Date | Author | Change |
|---|---|---|---|
| 0.1 | 2026-11-12 | Claude (AI agent), draft from code | First draft, written from the code as built. |
| 0.2 | 2026-11-25 | AI coding agent, draft from code | Added structured OOS investigation, retest plan, Phase II, conclusion, reopening, QA deviation, excursion, register, report, trending, and template functions (FS-OOS-003..011; Draft — pending approval). |

## Scope and reading notes

- This document covers the read-only OOS tracking list (`OosTrackingController`, `OosTrackingService`). Nothing in it creates or changes an OOS record. The OOS group code is set on samples elsewhere (the approval decision, see FS-REV); that code was not read for this document. `OosInvestigationDocumentController` was not in the files listed for this area and was not read.
- Every statement was written from the code files named in each entry. Nothing was run.
- Audit. The two methods read data only and write nothing, so there is no audit entry and no concurrency check to describe.

## Functions

### FS-OOS-001
**Satisfies:** URS-OOS-001, URS-OOS-002
**Function:** `GET api/oos-tracking`. The controller carries the policy `Oos.Manage` (`PermissionConstants.OosManage`); no other role or permission is tested. The controller passes the caller's readable sections (`GetReadableSectionIdsAsync`; null for a role that holds `Sections.ViewAll`) to the service. The method has no inputs from the client. It returns a list of OOS groups. A user without the permission is refused by the policy; a user with the permission but no section membership gets an empty list when the scope list is empty (the filter then matches no sample). The list is not paged and has no row limit.
**Code:** `backend/MicroLIMS.API/Controllers/OosTrackingController.cs` — `Get`
**Verified by:** reading (not run)
**Conflicts:** CONF-162

### FS-OOS-002
**Satisfies:** URS-OOS-001, URS-OOS-002
**Function:** Grouping rule, as coded. Every sample with an OOS group code is a candidate. With a scope list, a group is shown only when at least one of its retest samples (a sample with an origin sample) has a test order in the viewer's sections; the group's samples are then limited to the samples that have at least one test order in those sections. Per group: the root sample is the sample without an origin sample, else the one with the lowest ID among the samples loaded. Every other sample in the group is a retest entry, ordered by received date: new sample ID, reference, status, origin sample ID, reference and status (taken from the origin sample, else from the root), display name (item name; for after-cleaning the machine; for water the sampling point code; for environmental monitoring the department), category, batch number, retest type, distinct test codes, distinct assigned-analyst names ("User #id" when the name is not found) and the received date as the opening date. The retest type is the approval decision of the origin sample as text, or empty. The group header carries the root's ID, reference, status, name, category, batch, received date as opening date, and a flag that is true when an investigation document with a status other than Voided exists for the group code (compared without regard to case). Groups are ordered by opening date, newest first. Because the scope filter removes samples with no test in the viewer's sections, a viewer who has no test on the real root sample sees the lowest-ID remaining sample as the root. The method loads sample entities with their related entities without `AsNoTracking`.
**Code:** `backend/MicroLIMS.Application/Services/OosTrackingService.cs` — `GetOosGroupsAsync`, `ResolveDisplayName`
**Verified by:** reading (not run)
**Conflicts:** CONF-162


### FS-OOS-003
*(Draft — pending approval)*
**Satisfies:** URS-OOS-003
**Function:** Automatic and manual OOS opening. `SampleReviewService.CompleteReviewCoreAsync` calls `OosInvestigationService.ApplyReviewedSectionAsync` in the same transaction as review signing. `OosOpeningRule.ReadReviewOutcomesAsync` identifies failing orders (`TestOrderOutcome.Fails`). Under `pg_advisory_xact_lock` (`AdvisoryLockKeys.OosNumber`), `CreateAsync` generates `OOS{MM}{yy}{seq:D3}` via `ReferenceNumberGenerator.GenerateOosCodeAsync`, snapshots the section's `OosInvestigationTemplate` as JSON, and computes `PhaseOneDueDate` and `DueDate` via `PlanWindowCalculator.AddWorkingDays`. Failing orders on a linked retest sample join the origin's open investigation and advance `AwaitingRetest` to `PhaseTwo` (CONF-219). `POST api/oos-investigations/for-sample/{sampleId}/sections/{sectionId}/open` (`Oos.Manage`) opens an investigation manually for a reviewed failing section without one.
**Code:** `backend/MicroLIMS.Application/Services/Oos/OosInvestigationService.cs` — `ApplyReviewedSectionAsync`, `CreateAsync`; `backend/MicroLIMS.Application/Services/Oos/OosInvestigationService.Conclusion.cs` — `OpenManuallyAsync`; `backend/MicroLIMS.Application/Services/SampleReviewService.cs` — `CompleteReviewCoreAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-209, CONF-219

### FS-OOS-004
*(Draft — pending approval)*
**Satisfies:** URS-OOS-004
**Function:** Phase I workflow. `POST api/oos-investigations/{id}/phase-one/statements` (`Oos.Contribute`) appends an `AnalystStatement` entry (1–4000 chars) only for an `Analyst`-role user who performed a test in the investigation. `POST api/oos-investigations/{id}/checklist/{phase}/{itemKey}` (`Oos.Contribute`) appends a `ChecklistAnswer` entry (comment <= 1000 chars). `GET api/oos-investigations/{id}/facts` (`Oos.View`) returns read-only equipment, media, solution, reference standard, incubation, and result audit-count facts. `POST api/oos-investigations/{id}/phase-one/sign` (`Oos.Manage`, `If-Match`) requires a non-empty Phase I template, at least one analyst statement, all active Phase I items answered, an independent signer, and password verification (`SignatureMeaning.OosPhaseOneConcluded`). `LaboratoryErrorConfirmed` requires an active cause category (`!IsNoCauseFound`) and description, marks `OosInvestigationResults.IsInvalidated = true`, and moves status to `AwaitingRetest`; `NoLaboratoryErrorFound` moves status to `PhaseTwo`.
**Code:** `backend/MicroLIMS.Application/Services/Oos/OosInvestigationService.PhaseOne.cs` — `AddAnalystStatementAsync`, `AnswerChecklistAsync`, `SignPhaseOneAsync`; `backend/MicroLIMS.Application/Services/Oos/OosInvestigationService.Facts.cs` — `GetFactsAsync`
**Verified by:** reading (not run)

### FS-OOS-005
*(Draft — pending approval)*
**Satisfies:** URS-OOS-005
**Function:** Signed retest plans. `PUT api/oos-investigations/{id}/retest-plan` (`Oos.Manage`, `If-Match`) drafts or updates an unsigned `OosRetestPlan` while status is `AwaitingRetest` or `PhaseTwo`. Validates source (`RetainedSample` or `NewSample`), count 1–10, `ReportableRetestNumber` (required and in `1..RetestCount` when `RetestCount > 1`; defaults to `1` when `RetestCount == 1`), judging rule (1–4000 chars), amendment reason when `Revision > 1`, distinct current non-superseded failing orders linked to the investigation, and assignable analysts (excluding original performers when `Source == NewSample`). `POST api/oos-investigations/retest-plans/{planId}/sign` (`Oos.Manage`, `If-Match`) re-validates the draft (an existing multi-retest draft without `ReportableRetestNumber` cannot be signed until saved with one), verifies the signer is not in `AnalystUserIds` and performed no test in the investigation, signs `OosRetestPlanApproved`, creates `RetestCount` linked samples via `RetestSampleFactory` with `OosGroupCode = investigation.Number`, links them in `OosRetestPlanSamples`, and binds `ReportableSampleId` to the `k`-th created sample (`k == ReportableRetestNumber`), constrained by composite FK `FK_OosRetestPlans_ReportableSample` so an amendment can only name one of its own samples (CONF-220). `OosInvestigationReportBuilder` lists every retest sample across all revisions with result summaries (`OosOpeningRule.ReadResultSummariesAsync`) and a `"Reportable"` marker.
**Code:** `backend/MicroLIMS.Application/Services/Oos/OosRetestPlanService.cs` — `DraftAsync`, `SignAndCreateAsync`, `ValidateAsync`; `backend/MicroLIMS.Application/Services/Oos/OosInvestigationReportBuilder.cs`
**Verified by:** `OosRetestPlanValidationPostgresTests`, `OosRetestPlanPostgresTests`, `OosReadAndReportPostgresTests`
**Conflicts:** CONF-218, CONF-220

### FS-OOS-006
*(Draft — pending approval)*
**Satisfies:** URS-OOS-006
**Function:** Phase II entries and signed conclusion. `POST api/oos-investigations/{id}/entries` (`Oos.Contribute` or `Oos.Manage`) appends `OutsideReview`, `Hypothesis`, `HypothesisOutcome` (linked to a `Hypothesis` in the same investigation), or `Note` entries. `POST api/oos-investigations/{id}/conclude` (`Oos.Manage`, `If-Match`) requires an independent signer who performed no investigated or retest test, all active Phase II items answered (when in `PhaseTwo`), all signed-plan retest samples reviewed, and signs `OosInvestigationConcluded`. `Invalidated` requires a signed plan with created samples and an active non-`IsNoCauseFound` category, and sets `IsInvalidated = true` and `TestOrder.IsSuperseded = true` on the origin sample's investigated orders. `Confirmed` reverses any remaining origin invalidation and supersession flags.
**Code:** `backend/MicroLIMS.Application/Services/Oos/OosInvestigationService.Conclusion.cs` — `AddEntryAsync`, `ConcludeAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-219

### FS-OOS-007
*(Draft — pending approval)*
**Satisfies:** URS-OOS-007
**Function:** Reopening a concluded investigation. `POST api/oos-investigations/{id}/reopen` (`Oos.Manage`, `If-Match`) checks under `LockSampleDecisionAsync` that the investigation is `Concluded` and the origin laboratory section is not `Approved` or `Rejected`, requires an independent signer and a 1–4000 char reason, signs `OosInvestigationReopened`, inserts an append-only `OosInvestigationReopening` record with the previous conclusion and signature ID, reverses any origin invalidation/supersession flags if previously `Invalidated`, and resets status to `PhaseTwo` with conclusion fields cleared on the root.
**Code:** `backend/MicroLIMS.Application/Services/Oos/OosInvestigationService.Conclusion.cs` — `ReopenAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-219

### FS-OOS-008
*(Draft — pending approval)*
**Satisfies:** URS-OOS-008
**Function:** QA deviation requests. `POST api/oos-investigations/{id}/deviation-requests` (`Oos.RequestDeviation`) creates an `OosDeviationRequest` (`Status = Open`) on an investigation in any status. `POST api/oos-investigations/deviation-requests/{requestId}/address` (`Oos.Manage`, `If-Match`) marks the request `Addressed` with a comment, actor, and timestamp. Open deviation requests do not block investigation conclusion.
**Code:** `backend/MicroLIMS.Application/Services/Oos/OosInvestigationService.Deviation.cs` — `RequestDeviationAsync`, `AddressDeviationRequestAsync`
**Verified by:** reading (not run)

### FS-OOS-009
*(Draft — pending approval)*
**Satisfies:** URS-OOS-009
**Function:** Excursion reviews. `ApplyReviewedExcursionsAsync` opens or joins an `Excursion`-kind investigation at review for `ActionLimitExceeded` (and `AlertLimitExceeded` when `AlertOpensExcursionReview` is enabled on the section's Excursion template), skipping samples linked to a `MonitoringOccurrence`. `POST api/oos-investigations/excursions/open` (`Oos.Contribute` or `Oos.Manage`) opens a manual excursion review for a non-superseded, non-closed test order that does not fail. `POST api/oos-investigations/{id}/excursion/close` (`Oos.Manage`, `If-Match`) requires all active Excursion checklist items answered, an independent signer, and signs `ExcursionReviewClosed` (`Conclusion = ExcursionClosed`). `OosGate` ignores `Kind = Excursion`.
**Code:** `backend/MicroLIMS.Application/Services/Oos/OosInvestigationService.Excursion.cs` — `OpenManualExcursionAsync`, `CloseExcursionAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-209

### FS-OOS-010
*(Draft — pending approval)*
**Satisfies:** URS-OOS-010
**Function:** OOS register, detail, PDF report, and trending. `GET api/oos-investigations` (`Oos.View`) returns a paged list (pageSize clamped 1–100) filtered by status, kind, section, overdue flag, and date range, restricted to the caller's readable sections (and for analysts without `Oos.Manage`, only investigations containing a test they performed). `GET api/oos-investigations/{id}` returns full detail including signed timeline entries; `GET api/oos-investigations/{id}/pdf` renders the PDF via `OosInvestigationReportBuilder` and logs the export via `DataExportAuditService.LogExportAsync`; `GET api/oos-investigations/trends` aggregates counts and median working days to conclude by `product`, `test`, `analyst`, `instrument`, `cause`, or `conclusion`.
**Code:** `backend/MicroLIMS.API/Controllers/OosInvestigationsController.cs`; `backend/MicroLIMS.Application/Services/Oos/OosInvestigationService.Read.cs`; `backend/MicroLIMS.Application/Services/Oos/OosInvestigationReportBuilder.cs`; `backend/MicroLIMS.Application/Services/Oos/OosTrendService.cs`
**Verified by:** reading (not run)

### FS-OOS-011
*(Draft — pending approval)*
**Satisfies:** URS-OOS-011
**Function:** Templates and cause categories. `GET/PUT api/oos-templates/{sectionId}/{kind}` (`Oos.Manage`, section Configure scope, `If-Match`) manages per-laboratory `OosInvestigationTemplate` working-day due targets, `AlertOpensExcursionReview`, and `OosChecklistItem` rows (templates ship empty). `GET api/oos-templates/categories` (`Oos.Manage`) lists `OosCauseCategories`; `POST/PUT api/oos-templates/categories` requires both `Oos.Manage` and `MasterData.Manage`, an audited reason, and refuses renaming or deactivating `IsNoCauseFound`.
**Code:** `backend/MicroLIMS.API/Controllers/OosTemplatesController.cs`; `backend/MicroLIMS.Application/Services/Oos/OosTemplateService.cs`
**Verified by:** reading (not run)
**Conflicts:** CONF-213
