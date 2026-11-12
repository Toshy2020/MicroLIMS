# FS-REV Functional Specification

| Field | Value |
|---|---|
| Document number | FS-REV |
| Title | Sample review, approval and segregation of duties functional specification |
| Version | 0.1 |
| Status | Draft |
| Author | Claude (AI agent), draft from code |
| Reviewer | To be assigned by QA |
| Approver | To be assigned by QA |
| Effective date | Set when Approved |

## Change history

| Version | Date | Author | Change |
|---|---|---|---|
| 0.1 | 2026-11-12 | Claude (AI agent), draft from code | First draft, written from the code as built. |

## Scope and reading notes

- Review and approval work per laboratory section: each section of a sample has its own sign-off record (`SampleSectionSignoff`) with status InTesting, UnderReview, UnderApproval, Approved, Rejected or RetestRequested. The sample status is rolled up from the sections (`SampleSectionRollup`, read only where quoted below).
- Signing is done by calling `IElectronicSignatureService.SignAsync` with the user ID and password through `ReviewGateService.SignAndLogAsync`. The signature service itself was not read for this document; see FS-SIG.
- The class that the plan calls `SegregationOfDutiesGuard` exists under that name (`SegregationOfDutiesGuard.cs`). A second, different file `SegregationOfDutiesRules.cs` (in the shared project) holds the role-level rule. Both are described below.
- The controller `SampleApprovalController` also has a `sections/{sectionId}/close` endpoint that calls `SectionClosureService`. That service was not in the list of files to read and is not described here.
- The files `ReviewService.cs` and `ApprovalService.cs` named in `documents/DesignSpecification/BusinessRules.md` were searched for by name and do not exist. See CONF-022.
- Nothing was run. None of the methods here checks a record version (`If-Match`; `RecordVersion.EnsureCurrent` is not called in these files). See CONF-025.

## Functions

### FS-REV-001
**Satisfies:** URS-REV-001, URS-REV-002, URS-REV-011
**Function:** Complete a section review (endpoint `POST api/samples/{id}/review/complete`; permission `SamplesReview`). Inputs: password, optional comment, optional section ID, retention quantity and unit. Runs in one transaction. Section scope: the user's member sections are read; the section to review is the requested one (it must exist on the sample, be inside the user's member sections, and be UnderReview) or, when none is given, the only UnderReview section inside the user's member sections (more than one is refused; none inside scope is refused as outside the user's sections). The scope used is the user's member sections only (`GetMemberSectionIdsAsync`); `Sections.ViewAll` does not widen it. Preconditions: for every current (not superseded) test order of the section, `DidUserPerformTestAsync` must be false for the reviewer, otherwise the call fails with "You cannot review a sample you tested."; every such order must be at step Ready. Retention rule as coded: for a raw-material sample reviewed in the section whose code equals the physicochemical section code, a retention quantity greater than zero and below 1,000,000 and a unit of g, mg, kg, mL or L (any letter case) is required; in any other case supplying a quantity or unit is refused. Signing: the electronic signature (meaning Reviewed) and a review event are created first; a wrong password stops the call before anything else is written. Result: the section sign-off becomes UnderApproval with the reviewer, time and signature; the sample's reviewed-by and reviewed-at are set; each current order of the section moves to Reviewed with a workflow-history row naming the signer. When a retention quantity changed, an audit event `RetentionQuantityRecorded` with old and new text is written through `IAuditEventService`. The review does not look at result statuses, so a section with out-of-specification results can be reviewed. No comment is required.
**Code:** `backend/MicroLIMS.Application/Services/SampleReviewService.cs` — `CompleteReviewCoreAsync`, `ValidateRetentionAsync`; `backend/MicroLIMS.API/Controllers/SampleReviewController.cs` — `Complete`
**Verified by:** reading (not run)
**Conflicts:** CONF-022, CONF-023, CONF-025

### FS-REV-002
**Satisfies:** URS-REV-004
**Function:** Return a test to the analyst (endpoint `POST api/samples/{id}/review/return-test`; permission `SamplesReview`). Inputs: test order ID, optional reason. The controller checks the user's write scope on the test order; the sample ID in the route is not used and is not compared with the order's sample. Runs in one transaction. Preconditions: the sample is not UnderApproval, Approved, Rejected, RetestRequested, Cancelled or Voided and the order's section is InTesting or UnderReview; the order status is ResultEntered; the test's workflow type is CountTest or an analysis workflow (other types, including observation-type pathogen tests, are refused). Count tests: all active count readings of the order are marked inactive (kept), and the latest incubation of the order is re-opened by clearing its completed time, completed-by user and outcome in the same row. Analysis tests: active analyses and active parameter results are marked inactive (kept). If the section was UnderReview it goes back to InTesting and the sample status is rolled up again. The order is moved to Incubating (count) or Running (analysis) through `WorkflowStateMachine` with a history note holding the reason. A `TestReturnEvent` row (order, reviewer, assigned analyst, reason, time) is added. For a moisture test (loss on drying or water content) every dependent assay of the same sample that used its result is returned too, and the call fails if any dependent assay is already past ResultEntered. The reason is optional, no password or signature is asked for, and no check is made that the reviewer did not perform the test.
**Code:** `backend/MicroLIMS.Application/Services/SampleReviewService.cs` — `ReturnToAnalystCoreAsync`, `ReturnOrderAsync`; `backend/MicroLIMS.API/Controllers/SampleReviewController.cs` — `ReturnTestToAnalyst`
**Verified by:** reading (not run)
**Conflicts:** CONF-024, CONF-025

### FS-REV-003
**Satisfies:** URS-REV-003
**Function:** Automatic submission for review. Called by the result recorders after a test order reaches Ready (for example the count, batch, pathogen and finalisation methods). Inputs: sample ID, triggering user ID. If the sample is not Received or InTesting nothing happens. For each section that is InTesting and whose current orders are all at Ready, the section sign-off becomes UnderReview with the submission time and a review event "All tests completed - automatically submitted for review" is logged for the triggering user; the sample status is rolled up. The method stages changes only; the calling transaction saves them. A section is not submitted while any current order is not at Ready.
**Code:** `backend/MicroLIMS.Application/Services/SampleReviewService.cs` — `AutoSubmitForReviewIfReadyAsync`, `CanSubmitForReviewAsync`
**Verified by:** reading (not run)

### FS-REV-004
**Satisfies:** URS-REV-005, URS-REV-006
**Function:** Decide a section's approval (endpoint `POST api/samples/{id}/approval/decide`; permission `SamplesApprove`). Inputs: password, decision (Approve, Reject, RetestRetainedSample, NewSampleRequest), comment, certificate remarks, selected test order IDs, two analyst IDs, optional section ID. Runs in one transaction. Section choice and scope (member sections only) as in FS-REV-001 but the required section status is UnderApproval. Segregation of duties as coded: the deciding user must not be the section's reviewer (`signoff.ReviewedByUserId`) and `DidUserPerformTestAsync` must be false for every current order of the section. The decisions Investigation and OOSInvestigation exist in the enum but are refused here ("not a valid sample-level decision"). A signature is created first with a meaning that follows the decision (Approve gives Approved; Reject and NewSampleRequest give Rejected; RetestRetainedSample gives RetestRequested) and a review event `ApprovalDecisionMade` is logged; a wrong password stops the call before anything is written. Approve: the section becomes Approved with approver, time and the certificate remarks (blank becomes empty); the sample is rolled up and, when it becomes Approved, its approver, time and decision are set; each current order of the section moves to step Approved and status Approved with a history row. Reject: the section becomes Rejected with approver and time; each current order of the section gets status Rejected with a history row; other sections are not closed. The decision does not require a comment and does not check that the section's results are within limits. Afterwards `FinalizeIfClosed` runs (FS-REV-008). Retest decisions: FS-REV-006.
**Code:** `backend/MicroLIMS.Application/Services/SampleApprovalService.cs` — `DecideCoreAsync`; `backend/MicroLIMS.API/Controllers/SampleApprovalController.cs` — `Decide`
**Verified by:** reading (not run)
**Conflicts:** CONF-022, CONF-023, CONF-025

### FS-REV-005
**Satisfies:** URS-REV-007
**Function:** Extra checks when the decision is Approve, made before the signature. System suitability: for every current order whose test definition has `RequiresSystemSuitability`, the test must be an HPLC method assay or dissolution test, otherwise approval is refused with an instruction to clear the flag in Test Master; and the order must be assigned to an HPLC run (not abandoned) whose system suitability status is Passed, otherwise approval is refused. Analysis tests: for every current order whose workflow type uses test analyses, at least one active analysis must exist, and none of its parameter results may have comparison status NextStageRequired, otherwise approval is refused. No other check is made on the results of the section: result statuses such as OutOfSpecification, ActionLimitExceeded, RequiresReview or Inconclusive do not block approval.
**Code:** `backend/MicroLIMS.Application/Services/SampleApprovalService.cs` — `DecideCoreAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-023

### FS-REV-006
**Satisfies:** URS-REV-008
**Function:** Retest decisions. Both need at least one selected test order, and every selected ID must be a current order of the deciding section. The cause of testing named "Retest" must exist in configuration or the call fails. RetestRetainedSample: the section becomes RetestRequested; the sample receives an out-of-specification group code if it has none; one new sample (status Received, copied identity fields, origin sample link, retest cause) is created with new orders (Waiting, Pending, same analyst, section and room) for the selected tests; each selected original order is marked superseded with a history row. For water, environmental and after-cleaning samples only the failed locations (status not WithinLimits and not Absent) are copied; if none failed, all are copied; the new sample is then Ready for testing without the preparation step. NewSampleRequest: two analyst IDs are required and must differ; each must pass `AnalystAssignmentRules.EnsureAssignableAsync` for the sections of the selected tests; neither may be the analyst assigned to any selected original order; two new samples are created, each with its own analyst, and the selected originals are marked superseded. Reference numbers are generated by `ReferenceNumberGenerator`; in the two-sample case the first sample is saved before the second number is generated.
**Code:** `backend/MicroLIMS.Application/Services/SampleApprovalService.cs` — `DecideCoreAsync`, `BuildRetestSample`, `CloneFailedLocationsAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-025

### FS-REV-007
**Satisfies:** URS-REV-009
**Function:** Carry the outcome of a retest back to the origin sample. Runs after a retest sample is closed (Approved or Rejected). Applies only when the retest sample has exactly one section and the origin's sign-off for that section is RetestRequested. For a retained-sample retest the origin section takes the retest's outcome. For a new-sample request the sibling retest sample must also be closed; the origin section is Approved only if both are Approved, otherwise Rejected. The origin section's approver and time are set to the deciding user and now (no new signature is made). Then every current and superseded order of the origin in that section is finalised from its own recorded result: status Approved (and step Approved) when the result is classed as conforming, otherwise Rejected. The classification looks, in order, at the latest active count reading status, the latest sample-location status, the latest biochemical organism flag, any pathogen observation of growth conforming, and the latest `Result` row text, using `ResultConformanceRules`; with no result of any of these kinds the order counts as conforming. Test analyses and parameter results (physicochemical) are not looked at. If the origin is then closed it is projected, a report document is built and archived, and the rule repeats up the chain.
**Code:** `backend/MicroLIMS.Application/Services/SampleApprovalService.cs` — `PropagateOosOutcomeAsync`, `DetermineOwnResultConformanceAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-008, CONF-023

### FS-REV-008
**Satisfies:** URS-REV-010
**Function:** Close-out of a sample. After a decision, when the sample status is Approved or Rejected, `FinalizeIfClosed` refreshes the approval fields of the sample's reporting rows, builds the report document through `SampleSummaryService.BuildReportDocumentAsync`, and archives it through `RecordArchiveService.ArchiveAsync` with a label such as "Sample Approve" and the deciding user; then it propagates the outcome (FS-REV-007). For a retest decision no document is archived because the sample is not closed. The archive and summary services were not read for this document.
**Code:** `backend/MicroLIMS.Application/Services/SampleApprovalService.cs` — `FinalizeIfClosed`
**Verified by:** reading (not run)

### FS-REV-009
**Satisfies:** URS-REV-002, URS-REV-006
**Function:** What counts as "performed the test" for segregation of duties. `DidUserPerformTestAsync(testOrderId, userId)` returns true when any of these is the user: the test order's assigned analyst; the preparer of any sample preparation record on the order's sample; the user who entered any `Result` row, any `CountTestReading`, or any `TestAnalysis` for the order; the observer of any `PathogenObservation`; the submitter of any `WorkflowStepResult`; the recorder of any confirmatory plate observation that is linked through a workflow step result of the order. It compares user IDs only and does not look at roles, so no role is exempt. It returns false for a missing order. It does not read: the user on `LocationPathogenObservation` rows, `SampleLocation.EnteredByUserId`, the starter or closer of an `Incubation`, or confirmatory plate observations that are linked to a primary observation but not to a workflow step result. Besides review and approval it is used by the biochemical review decision (FS-PAT-012). The same class also holds checks for media, stock lot, solution and cryovial release (`DidUserPrepareOrEvaluateMediaAsync`, `DidUserReceiveMaterialAsync`, `DidUserPrepareSolutionAsync`, `DidUserPrepareCryovialAsync`); their callers are in other areas.
**Code:** `backend/MicroLIMS.Application/Services/SegregationOfDutiesGuard.cs` — `DidUserPerformTestAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-021

### FS-REV-010
**Satisfies:** URS-REV-012
**Function:** Role-level separation of administration and GxP work. `SegregationOfDutiesRules` defines two sets of permission codes: administration (`Users.Manage`, `Roles.Manage` through `PermissionConstants`) and a list of GxP execution and approval codes (review, approve, correct and receive samples, workflow execute, biochemical decision, supervise, HPLC operate, solutions, working standards, media, materials, cryovials, monitoring and equipment plan approval, OOS manage, documents review and approve, sample preparation, outsourcing, not-applicable tests). `FindConflicts` returns every pair of (administration code, GxP code) found in a set of permission codes. `SegregationOfDutiesViolationException` carries the pairs; its message names the first pair and the number of others. The comment in the file says `RoleService` enforces the rule when a role's permissions are set and that the API reports it as HTTP 409. `RoleService` and the controller mapping were not read for this entry (see FS-ADM).
**Code:** `backend/MicroLIMS.Shared/Constants/SegregationOfDutiesRules.cs` — `FindConflicts`; `backend/MicroLIMS.Application/Services/SegregationOfDutiesViolationException.cs` — `SegregationOfDutiesViolationException`
**Verified by:** reading (not run)
