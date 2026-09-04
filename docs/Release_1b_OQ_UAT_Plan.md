# Release 1b Operational Qualification (OQ) & User Acceptance Testing (UAT) Plan

**Document ID:** ML-DC-R1B-OQ-UAT-PLAN-001  
**Version:** 1.0  
**Status:** Approved Qualification Plan  
**Module:** Document Control (Release 1b)  
**System:** MicroLIMS Enterprise Laboratory Information Management System  
**Authoritative Baseline:** MicroLIMS Document Control URS v1.1, FRS ML-DC-FRS-1B-001, Risk Assessment ML-DC-R1B-RA-001  
**Regulatory Context:** 21 CFR Part 11, EU GMP Annex 11, PIC/S PE 009-17, GAMP 5  
**Date:** September 3, 2026  

---

## 1. Document Purpose & Qualification Framework

This document defines the formal **Operational Qualification (OQ) and User Acceptance Testing (UAT) Plan** for **Release 1b** of the MicroLIMS Document Control module.

> [!NOTE]
> **PLANNING DOCUMENT NOTICE:**
> This document defines the *intended qualification scenarios, preconditions, steps, and acceptance criteria*. It does **NOT** record execution results or sign-offs. Execution results will be formally recorded in the post-implementation execution protocol.

The qualification plan encompasses two distinct testing perspectives:
1. **Operational Qualification (OQ):** 28 formal, requirement-by-requirement test cases (OQ-1B-01 through OQ-1B-28) systematically verifying system behavior, security gates, automated background execution, and database integrity against the approved FRS (`ML-DC-FRS-1B-001`).
2. **User Acceptance Testing (UAT):** 8 end-to-end, realistic microbiology quality control laboratory scenarios (UAT-1B-01 through UAT-1B-08) evaluating user workflows from the perspective of laboratory analysts, reviewers, approvers, and document controllers.

---

## 2. Planned Operational Qualification (OQ) Test Cases

### OQ-1B-01: Automated Periodic Review Task Generation (`DC-URS-044`, `DC-URS-179`)
- **Objective:** Verify that the background worker identifies effective documents with upcoming review dates and automatically creates a `PeriodicReviewTask` in `Pending` status.
- **Preconditions:** Document Master holds an `Effective` revision with `NextReviewDate = UtcNow.AddDays(15)`. No prior task exists.
- **Execution Steps:**
  1. Trigger execution iteration of `DocumentEffectiveDateWorker`.
  2. Query database for `PeriodicReviewTask` records linked to the revision.
- **Expected Results:** Exactly one `PeriodicReviewTask` created with status `Pending`; due date matches `NextReviewDate`; audit log created with `ActorType.System`.

### OQ-1B-02: Periodic Review Workspace File Retrieval & Integrity Badge (`DC-URS-045`)
- **Objective:** Verify that the periodic review workspace retrieves and displays the active controlled PDF, calculating SHA-256 on retrieval and rendering the verified integrity badge.
- **Preconditions:** `PeriodicReviewTask` exists; user assigned as reviewer.
- **Execution Steps:** Open periodic review workspace; inspect PDF preview.
- **Expected Results:** Authentic PDF displayed; SHA-256 matches database checksum; integrity verification badge visible.

### OQ-1B-03: Periodic Review Page and Section Structured Findings (`DC-URS-046`)
- **Objective:** Verify that a reviewer can record structured review findings with page and section numbers.
- **Execution Steps:** Submit finding with `PageNumber = 4`, `SectionNumber = "6.2"`, `FindingText = "Incubation temperature range requires update to align with USP <71>."`.
- **Expected Results:** `PeriodicReviewFinding` persisted; review task status advances to `InProgress`.

### OQ-1B-04: Periodic Review Outcome "Remains Valid" & Date Advance (`DC-URS-047`..`049`)
- **Objective:** Verify that completing review with outcome `RemainsValid` advances `NextReviewDate` by review cycle months without creating a new revision.
- **Preconditions:** Revision has `NextReviewDate = 2026-09-01`, `ReviewCycleMonths = 24`.
- **Execution Steps:** Complete review with `Outcome = RemainsValid` and mandatory comment (>= 10 chars).
- **Expected Results:** Review task closed as `Completed`; `NextReviewDate` advanced to `2028-09-01`; total revisions count for master remains unchanged.

### OQ-1B-05: Periodic Review Outcome "Revision Required" & Finding Transfer (`DC-URS-050`, `DC-URS-051`, `DC-URS-063`)
- **Objective:** Verify that selecting `RevisionRequired` allows creating a new revision and transfers findings into `RevisionChangeItem` records.
- **Execution Steps:** Complete review with `Outcome = RevisionRequired`; initiate new revision referencing the review task.
- **Expected Results:** New revision created in `Draft` status; review findings automatically copied into `RevisionChangeItems` marked as `Source = PeriodicReview`.

### OQ-1B-06: Periodic Review Outcome "Obsolescence Recommended" Gate (`DC-URS-052`)
- **Objective:** Verify that recommending obsolescence does not immediately obsolete the document, but routes it to an obsolescence approval workflow.
- **Execution Steps:** Complete review with `Outcome = ObsolescenceRecommended`.
- **Expected Results:** Revision status remains `Effective`; `DocumentApprovalTask` created with `TargetStatus = Obsolete`.

### OQ-1B-07: Independent Maintenance of Periodic Review History (`DC-URS-053`)
- **Objective:** Verify that historical periodic review records persist independently when a new revision is published.
- **Execution Steps:** Query review history for a document master with multiple historical revisions.
- **Expected Results:** Complete review rounds across all past revisions remain queryable.

### OQ-1B-08: Identification and Filtering of Overdue Periodic Reviews (`DC-URS-054`)
- **Objective:** Verify that documents past their `NextReviewDate` display overdue alert badges and are filterable in the Document Library.
- **Execution Steps:** Seed document with `NextReviewDate = UtcNow.AddDays(-10)`; query library with `isOverdue = true`.
- **Expected Results:** Document returned in query; UI displays warning badge "Review Overdue (10 days)".

### OQ-1B-09: Create New Revision from Effective Document (`DC-URS-055`, `DC-URS-056`, `DC-URS-058`, `DC-URS-059`)
- **Objective:** Verify creating a new revision from an effective document retains master ID, increments revision sequence, and enforces mandatory reason.
- **Execution Steps:**
  1. Attempt creation with reason "Updated" (7 chars).
  2. Create revision with valid reason (>= 10 chars), `RevisionType = Major`, `ChangeReference = "CC-2026-042"`.
- **Expected Results:** Step 1 rejected; Step 2 creates Revision 02 in `Draft` status; `DocumentMasterId` unchanged.

### OQ-1B-10: Proposed Revision Numbering Algorithm & Override Audit (`DC-URS-057`, `DC-URS-060`)
- **Objective:** Verify automatic revision number proposal (Major vs Minor) and verify that custom overrides by Document Controller are audited.
- **Execution Steps:**
  1. Create Minor revision from `01`; verify proposed number is `01.1`.
  2. Create Major revision from `01`; verify proposed number is `02`.
  3. Document Controller overrides number to `02-SPECIAL` with documented reason.
- **Expected Results:** Proposer generates standard formats; override accepted only for Controller and emits `RevisionNumberOverridden` audit event.

### OQ-1B-11: Structured Affected Section Change Items Management (`DC-URS-061`)
- **Objective:** Verify adding, updating, and removing structured change items during draft stage.
- **Execution Steps:** Add change items for Section 4.1 and Section 5.3; modify Section 4.1 description.
- **Expected Results:** Structured items persisted in `RevisionChangeItems` and rendered in draft details.

### OQ-1B-12: Multi-Category Revision Impact Assessment Completeness Gate (`DC-URS-062`)
- **Objective:** Verify that draft submission requires a completed impact assessment across all required categories.
- **Execution Steps:** Attempt submission with missing "Equipment" impact assessment; complete checklist and re-submit.
- **Expected Results:** Initial submission blocked with validation error; submission succeeds once all categories are evaluated.

### OQ-1B-13: Originating Review Findings Resolution Gate (`DC-URS-064`)
- **Objective:** Verify that any required-change finding originating from a periodic review must be marked `Addressed` before submission to review.
- **Execution Steps:** Attempt submission with one originating finding marked `Unaddressed`.
- **Expected Results:** Submission blocked with error: "All originating periodic review findings must be addressed prior to review submission."

### OQ-1B-14: Continuous Availability of Effective Revision (`DC-URS-065`)
- **Objective:** Verify that general readers continue to access the active effective revision while a draft revision is in review or awaiting approval.
- **Execution Steps:** General reader queries document library and downloads controlled PDF while new draft is in `InReview`.
- **Expected Results:** Effective revision returned without disruption; unapproved draft invisible to general reader.

### OQ-1B-15: Submission to Technical Review & File Locking (`DC-URS-066`)
- **Objective:** Verify submitting draft to technical review transitions status to `InReview` and locks attached files against replacement.
- **Execution Steps:** Submit draft to reviewer; attempt to upload replacement file.
- **Expected Results:** Status transitions to `InReview`; file replacement attempt rejected with `InvalidOperationException`.

### OQ-1B-16: Technical Review Side-by-Side PDF Inspection (`DC-URS-067`)
- **Objective:** Verify that technical review workspace loads current effective PDF and proposed draft PDF concurrently.
- **Execution Steps:** Load review workspace for Revision 02.
- **Expected Results:** Dual PDF viewer renders Revision 01 and Revision 02 side-by-side with synchronized page controls.

### OQ-1B-17: Technical Review Comment Lifecycle Management (`DC-URS-068`, `DC-URS-069`)
- **Objective:** Verify complete lifecycle progression of a review comment: `Open` -> `AuthorResponded` -> `ReviewerVerified` -> `Resolved`.
- **Execution Steps:** Reviewer creates finding; Author posts response; Reviewer verifies change and resolves comment.
- **Expected Results:** Comment state transitions successfully; full author and reviewer attribution preserved.

### OQ-1B-18: Reviewer Concurrence Gate on Mandatory Findings (`DC-URS-070`, `DC-URS-071`)
- **Objective:** Verify author cannot close reviewer findings and technical review completion is blocked while mandatory findings remain open.
- **Execution Steps:**
  1. Author attempts to call `ResolveFindingAsync` on reviewer comment.
  2. Reviewer attempts to call `CompleteTechnicalReviewAsync` while mandatory comment is in state `Open`.
- **Expected Results:** Step 1 throws `UnauthorizedAccessException`; Step 2 throws `InvalidOperationException`.

### OQ-1B-19: Technical Review Outcomes & Return for Correction (`DC-URS-072`, `DC-URS-073`)
- **Objective:** Verify reviewer decisions: `ReturnForCorrection` reverts status to `Draft`; `CompleteReview` advances status to `AwaitingApproval`.
- **Execution Steps:**
  1. Reviewer executes `ReturnForCorrection`; observe status revert to `Draft`.
  2. Author re-submits; Reviewer executes `CompleteReview`; observe status advance to `AwaitingApproval`.
- **Expected Results:** Status transitions match workflow rules; audit events recorded.

### OQ-1B-20: Approver Inspection Dossier Aggregation (`DC-URS-074`, `DC-URS-075`)
- **Objective:** Verify that the approval workspace compiles a complete evidence dossier (controlled PDF, change log, impact checklist, review discussion history).
- **Execution Steps:** Fetch approver dossier for revision in `AwaitingApproval`.
- **Expected Results:** Complete dossier returned; approver presented with `Approve`, `ReturnForCorrection`, and `Decline` options.

### OQ-1B-21: 21 CFR Part 11 Electronic Signature on Approval (`DC-URS-076`, `DC-URS-077`)
- **Objective:** Verify that approving a revision requires password re-entry, writes an immutable `ElectronicSignature` record, and correctly routes effective date.
- **Execution Steps:**
  1. Attempt approval with incorrect password.
  2. Submit approval with valid password and `EffectiveDate = UtcNow.Date`.
- **Expected Results:** Step 1 fails with authentication error; Step 2 succeeds, creates signature row in `"ElectronicSignatures"`, and transitions revision to `Effective`.

### OQ-1B-22: Hard Segregation of Duties (SoD) Enforcement (`DC-URS-078`, `DC-URS-164`..`169`)
- **Objective:** Verify server-side rejection when a user attempts to violate SoD invariants (Author as Reviewer, Author as Approver, Reviewer as Approver, or Admin bypass).
- **Execution Steps:**
  1. Author attempts to review own revision.
  2. Author attempts to approve own revision.
  3. Reviewer attempts to approve same revision.
  4. System Administrator attempts to approve own authored revision.
- **Expected Results:** All four attempts throw `UnauthorizedAccessException`.

### OQ-1B-23: Comprehensive Audit Trail of Review and Approval Actions (`DC-URS-079`)
- **Objective:** Verify that all review, return, comment, approval, and signature actions emit semantic audit logs.
- **Execution Steps:** Query audit trail for document after full review and approval cycle.
- **Expected Results:** Chronological logs present with actor names, timestamps, action codes, and field diffs.

### OQ-1B-24: Automated Activation of Future Effective Revisions (`DC-URS-177`, `DC-URS-178`)
- **Objective:** Verify background worker automatically transitions `FutureEffective` revision to `Effective` on scheduled date and sets prior revision to `Superseded` in the same transaction.
- **Execution Steps:** Seed revision in `FutureEffective` with `EffectiveDate = UtcNow.Date`; trigger worker iteration.
- **Expected Results:** New revision becomes `Effective`; prior revision becomes `Superseded`; initial `NextReviewDate` calculated.

### OQ-1B-25: System Attribution for Automated Actions (`DC-URS-180`)
- **Objective:** Verify automated worker audit entries are attributed to `SYSTEM` with `ActorType.System` and business rule identified.
- **Execution Steps:** Inspect audit logs emitted by worker during OQ-1B-24.
- **Expected Results:** `ActorType == ActorType.System`, `Username == "SYSTEM"`, rule documented in comment.

### OQ-1B-26: Downtime Recovery & Delayed Execution Catch-Up (`DC-URS-181`)
- **Objective:** Verify that revisions whose effective date passed during server downtime are activated upon restart with `DelayedExecution` audit flag.
- **Execution Steps:** Seed revision with `EffectiveDate = UtcNow.AddDays(-2)`; restart worker service.
- **Expected Results:** Revision activated immediately; audit log documents delayed execution.

### OQ-1B-27: UTC Clock Standardization (`DC-URS-182`)
- **Objective:** Verify date transitions are evaluated strictly against UTC clock boundaries.
- **Execution Steps:** Execute worker with client machine in negative/positive time zone offsets.
- **Expected Results:** Execution conforms strictly to UTC calendar day.

### OQ-1B-28: Scheduled Process Failure Alerting (`DC-URS-183`)
- **Objective:** Verify that an unhandled error during worker execution logs a critical incident and alerts administrators without halting processing of other documents.
- **Execution Steps:** Inject simulated database failure on one document; process batch.
- **Expected Results:** Error captured in audit logs; remaining documents processed successfully.

---

## 3. Planned User Acceptance Testing (UAT) Scripts

The following scripts evaluate realistic end-to-end microbiological testing laboratory workflows.

---

### UAT-1B-01: Periodic Review of Sterility Testing SOP (`SOP-QC-MIC-001`)
- **Roles:** Lead Microbiologist (`Reviewer`), QC Director (`Owner`)
- **Scenario:** The 24-month periodic review for Sterility Testing SOP is due. The reviewer inspects the active PDF, verifies incubation temperatures and membrane filtration controls, records page-specific notes, and concludes the review with outcome `RemainsValid`.
- **Success Criteria:** Review task completed; next review due date advanced by 24 months in UTC; no new revision created; full review history queryable.

---

### UAT-1B-02: Method Optimization & Revision Creation (`SOP-QC-MIC-001 Rev 02`)
- **Roles:** QC Microbiologist (`Author`), Document Controller
- **Scenario:** Changes in compendial incubation guidelines (USP <71>) require revising the Sterility Testing procedure. The author creates Revision 02 (Major) referencing Change Control `CC-2026-104`, completes the structured affected-section table, completes the multi-category impact assessment, attaches updated PDF `SOP-QC-MIC-001_v02.pdf`, and submits for technical review.
- **Success Criteria:** Revision 02 created in `Draft`; Revision 01 remains `Effective` and accessible; impact assessment validated; submission advances status to `InReview`.

---

### UAT-1B-03: Technical Review & Collaborative Comment Resolution
- **Roles:** Senior QC Microbiologist (`TechnicalReviewer`), QC Microbiologist (`Author`)
- **Scenario:** The technical reviewer opens the side-by-side comparison workspace, reviews Section 6.2 changes, and logs a mandatory comment regarding positive control media lot verification. The author provides response notes and uploads an adjusted PDF. The reviewer verifies the correction, resolves the comment, and recommends the revision for approval.
- **Success Criteria:** Side-by-side viewer functions smoothly; author cannot close reviewer comment; review completion unblocked only after reviewer verification; revision advances to `AwaitingApproval`.

---

### UAT-1B-04: Formal Regulatory Approval & 21 CFR Part 11 Electronic Signature
- **Roles:** QA Compliance Manager (`Approver`)
- **Scenario:** The approver inspects the complete approval dossier (controlled PDF, change rationale, impact checklist, review comment resolution history). The approver approves the revision, sets `EffectiveDate` to 14 days in the future (to allow analyst training), and signs off using electronic signature with password re-entry.
- **Success Criteria:** Signature prompts for password; signature record immutably created with user snapshot and `MeaningOfSignature = Approved`; revision status transitions to `FutureEffective`.

---

### UAT-1B-05: Segregation of Duties Enforcement in Regulated Laboratory
- **Roles:** Laboratory Analyst (`Author`), QC Manager (`Admin`)
- **Scenario:** The author of a revised protocol attempts to approve their own revision. A laboratory supervisor holding administrative rights attempts to override the restriction and sign as approver.
- **Success Criteria:** Both attempts are rejected with explicit access denial alerts; no signature is generated; security audit events recorded.

---

### UAT-1B-06: Automated Effective Date Activation & Supersession
- **Roles:** System Background Scheduler
- **Scenario:** On the scheduled effective date (00:01 UTC), the automated background worker executes. Revision 02 transitions from `FutureEffective` to `Effective`, and Revision 01 transitions from `Effective` to `Superseded`.
- **Success Criteria:** Activation occurs automatically without human intervention; both transitions committed in single database transaction; audit log attributed to `SYSTEM`.

---

### UAT-1B-07: Obsolescence Workflow for Discontinued Analytical Method
- **Roles:** Microbiologist (`Reviewer`), QA Director (`Approver`)
- **Scenario:** A legacy manual microbial limit test method is obsolete due to adoption of automated rapid bioburden instrumentation. The periodic reviewer selects `ObsolescenceRecommended`. An obsolescence approval task is routed to the QA Director, who signs the obsolescence order.
- **Success Criteria:** Document transitions to `Obsolete`; document watermarked with obsolete banner; excluded from standard library views.

---

### UAT-1B-08: Regulatory Inspection Audit Review for Document Lifecycle
- **Roles:** Regulatory Compliance Inspector / Auditor
- **Scenario:** An inspector queries the complete lifecycle of `SOP-QC-MIC-001` from Revision 01 registration through Revision 02 approval, side-by-side review discussion, electronic signature, and automated activation, exporting a self-auditing CSV.
- **Success Criteria:** Chronological audit trail displays complete lifecycle with zero gaps; CSV export records self-audited security log.

---

## 4. OQ/UAT Execution Readiness Criteria

Execution of this protocol will begin only after:
1. Work Packages 1 through 8 are implemented and passing 100% automated regression tests.
2. The live PostgreSQL test database is initialized with clean baseline seed data.
3. Test user accounts representing all laboratory roles (`Analyst`, `SectionHead`, `Admin`) are provisioned.
