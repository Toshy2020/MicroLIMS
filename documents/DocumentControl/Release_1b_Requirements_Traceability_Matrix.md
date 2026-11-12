# Release 1b Requirements Traceability Matrix (RTM)

**Document ID:** ML-DC-RTM-1B-001  
**Version:** 2.0  
**Status:** **FORMALLY QUALIFIED**  
**Module:** Document Control (Release 1b)  
**System:** MicroLIMS Enterprise Laboratory Information Management System  
**Authoritative Baseline:** MicroLIMS Document Control URS v1.1 (`DC-URS-044` through `DC-URS-079`, `DC-URS-177` through `DC-URS-183`)  
**Functional Specification Baseline:** ML-DC-FRS-1B-001  
**Risk Assessment Baseline:** ML-DC-R1B-RA-001  
**Qualification Baseline:** ML-DC-WP9-OQ-001 / ML-DC-WP9-UAT-001  
**Date:** September 4, 2026  

---

## 1. Traceability Methodology

This Requirements Traceability Matrix (RTM) establishes bidirectional traceability for all **43 requirements** of Release 1b across the complete software development lifecycle:
$$\text{URS} \longrightarrow \text{FRS} \longrightarrow \text{Design / Component} \longrightarrow \text{API / Service} \longrightarrow \text{DB Behavior} \longrightarrow \text{Frontend UI} \longrightarrow \text{Automated Tests} \longrightarrow \text{Executed OQ Evidence} \longrightarrow \text{Executed UAT Evidence} \longrightarrow \text{Status}$$

*Validation Rule:* In strict accordance with GAMP 5 principles, requirements transition from `IMPLEMENTED` to **`QUALIFIED`** exclusively upon successful execution and documentation of the formal Release 1b OQ/UAT Protocol without open critical deviations.

---

## 2. Release 1b Formally Qualified Traceability Matrix

| URS ID | FRS ID | Risk ID | Requirement Summary | Design Component | API Route & Service Method | Database Behavior | Frontend Component | Automated Test Evidence | Executed OQ / UAT Evidence | Qualification Status |
|---|---|---|---|---|---|---|---|---|---|:---:|
| **DC-URS-044** | `FS-1b-044` | RA-044 | Automatic Periodic Review task creation | `DocumentEffectiveDateWorker`, `PeriodicReviewService` | Worker / `GET /api/document-control/reviews/tasks` | Inserts `PeriodicReviewTask` (`Pending`) | `PeriodicReviewWorkspace`, Dashboard Badge | `Worker_CreatesPeriodicReviewTask_WhenDue` | OQ-1B-01, UAT-1B-01 | **QUALIFIED** |
| **DC-URS-045** | `FS-1b-045` | RA-045 | Review workspace displaying controlled PDF | `DocumentFileService`, `ControlledPdfViewer` | `GET /api/document-control/files/{id}/download` | SHA-256 retrieval verification | `PeriodicReviewWorkspace` PDF viewer | `PeriodicReview_LoadsVerifiedControlledPdf` | OQ-1B-02, UAT-1B-01 | **QUALIFIED** |
| **DC-URS-046** | `FS-1b-046` | RA-046 | Page/section review notes | `PeriodicReviewService` | `POST /api/document-control/reviews/{id}/findings` | Inserts `PeriodicReviewFinding` | Review Findings Panel | `PeriodicReview_AddNote_PersistsPageSection` | OQ-1B-03, UAT-1B-01 | **QUALIFIED** |
| **DC-URS-047** | `FS-1b-047` | RA-047 | Review outcomes (`RemainsValid`, `RevisionReq`, `Obsolescence`) | `PeriodicReviewService` | `POST /api/document-control/reviews/{id}/complete` | Updates `PeriodicReviewTask.Outcome` | Outcome Decision Dialog | `PeriodicReview_Complete_EnforcesOutcome` | OQ-1B-04, UAT-1B-01 | **QUALIFIED** |
| **DC-URS-048** | `FS-1b-048` | RA-048 | `RemainsValid` creates no new revision | `PeriodicReviewService` | `POST /api/document-control/reviews/{id}/complete` | `DocumentRevision` count unchanged | Review Closure Confirmation | `PeriodicReview_RemainsValid_NoNewRevision` | OQ-1B-04, UAT-1B-01 | **QUALIFIED** |
| **DC-URS-049** | `FS-1b-049` | RA-049 | `RemainsValid` advances next review date | `PeriodicReviewService` | `POST /api/document-control/reviews/{id}/complete` | Updates `DocumentRevision.NextReviewDate` | Next Review Date Badge | `PeriodicReview_RemainsValid_AdvancesDueDate` | OQ-1B-04, UAT-1B-01 | **QUALIFIED** |
| **DC-URS-050** | `FS-1b-050` | RA-050 | `RevisionRequired` links new revision | `DocumentRevisionService` | `POST /api/document-control/documents/{id}/revisions` | Foreign key `OriginatingPeriodicReviewTaskId` | Create Revision Modal | `Revision_CreatedFromReview_MaintainsLink` | OQ-1B-05, UAT-1B-02 | **QUALIFIED** |
| **DC-URS-051** | `FS-1b-051` | RA-051 | Review findings transfer to change items | `DocumentRevisionService` | `POST /api/document-control/documents/{id}/revisions` | Copies to `RevisionChangeItem` table | Draft Change Items Table | `Revision_CopiesFindings_IntoChangeItems` | OQ-1B-05, UAT-1B-02 | **QUALIFIED** |
| **DC-URS-052** | `FS-1b-052` | RA-052 | Obsolescence recommendation approval | `DocumentApprovalService` | `POST /api/document-control/documents/{id}/obsolete` | Inserts `DocumentApprovalTask` (Obsolescence) | Obsolescence Approval Modal | `Obsolescence_RequiresFormalApproval` | OQ-1B-06, UAT-1B-07 | **QUALIFIED** |
| **DC-URS-053** | `FS-1b-053` | RA-053 | Independent periodic review history | `PeriodicReviewService` | `GET /api/document-control/documents/{id}/reviews` | Relational table `PeriodicReviewTasks` | Document Details "Periodic Reviews" Tab | `PeriodicReview_HistoryPreservedAcrossRevisions` | OQ-1B-07, UAT-1B-01 | **QUALIFIED** |
| **DC-URS-054** | `FS-1b-054` | RA-054 | Overdue periodic review identification | `DocumentMasterService`, Library Filter | `GET /api/document-control/library?isOverdue=true` | Filter on `NextReviewDate < UtcNow` | Overdue Alert Badge & Library Filter | `DocumentLibrary_FiltersOverdueReviews` | OQ-1B-08, UAT-1B-06 | **QUALIFIED** |
| **DC-URS-055** | `FS-1b-055` | RA-055 | Create new revision from effective | `DocumentRevisionService` | `POST /api/document-control/documents/{id}/revisions` | Inserts new row in `DocumentRevisions` | Document Details "Create Revision" Action | `CreateRevision_FromEffective_Succeeds` | OQ-1B-09, UAT-1B-02 | **QUALIFIED** |
| **DC-URS-056** | `FS-1b-056` | RA-056 | Retain Master and MicroLIMS ID | `DocumentRevisionService` | `POST /api/document-control/documents/{id}/revisions` | `DocumentMasterId` unchanged | Header Master ID Component | `CreateRevision_RetainsPermanentMasterId` | OQ-1B-09, UAT-1B-02 | **QUALIFIED** |
| **DC-URS-057** | `FS-1b-057` | RA-057 | Propose next revision number & override | `DocumentRevisionService` | `POST /api/document-control/documents/{id}/revisions` | Generates sequence or validates override | Revision Number Input Field | `CreateRevision_ProposesNumberAndAuditsOverride` | OQ-1B-10, UAT-1B-02 | **QUALIFIED** |
| **DC-URS-058** | `FS-1b-058` | RA-058 | Mandatory change reason & summary | `DocumentRevisionValidator` | `POST /api/document-control/documents/{id}/revisions` | Rejects null or <10 char reason | Create Revision Modal Inputs | `CreateRevision_RejectsShortReason` | OQ-1B-09, UAT-1B-02 | **QUALIFIED** |
| **DC-URS-059** | `FS-1b-059` | RA-059 | Change Reference field | `DocumentRevision` | `POST /api/document-control/documents/{id}/revisions` | Stores `ChangeReference` string | Change Reference Input Field | `CreateRevision_StoresChangeReference` | OQ-1B-09, UAT-1B-02 | **QUALIFIED** |
| **DC-URS-060** | `FS-1b-060` | RA-060 | Major / Minor revision classification | `DocumentRevision` | `POST /api/document-control/documents/{id}/revisions` | Stores `RevisionType` Enum | Major/Minor Radio Group | `CreateRevision_SupportsMajorMinor` | OQ-1B-10, UAT-1B-02 | **QUALIFIED** |
| **DC-URS-061** | `FS-1b-061` | RA-061 | Structured affected section items | `RevisionChangeItemService` | `POST/PUT /api/document-control/revisions/{id}/changes` | Inserts into `RevisionChangeItems` | Structured Change Items Table | `RevisionChanges_AddAndEdit_PersistsItems` | OQ-1B-11, UAT-1B-02 | **QUALIFIED** |
| **DC-URS-062** | `FS-1b-062` | RA-062 | Multi-category impact assessment | `RevisionImpactService` | `POST /api/document-control/revisions/{id}/impact` | Inserts `RevisionImpactAssessment` | Impact Assessment Checklist Dialog | `RevisionImpact_EnforcesAllCategories` | OQ-1B-12, UAT-1B-02 | **QUALIFIED** |
| **DC-URS-063** | `FS-1b-063` | RA-063 | Originating review findings traceability | `DocumentRevisionService` | `GET /api/document-control/revisions/{id}` | Relational navigation property | Document Details "Originating Review" Box | `Revision_DisplaysOriginatingReviewFindings` | OQ-1B-05, UAT-1B-02 | **QUALIFIED** |
| **DC-URS-064** | `FS-1b-064` | RA-064 | Originating findings must be addressed | `DocumentReviewService.SubmitForReview` | `POST /api/document-control/revisions/{id}/submit-review` | Guard blocks submission if unaddressed | Submit for Review Validation Alert | `SubmitForReview_BlocksIfFindingsUnaddressed` | OQ-1B-13, UAT-1B-02 | **QUALIFIED** |
| **DC-URS-065** | `FS-1b-065` | RA-065 | Continuous availability of effective SOP | `DocumentMasterService.GetLibraryAsync` | `GET /api/document-control/library` | Returns `Effective` revision to readers | Library Table & PDF Viewer | `Library_ServesEffectiveRevision_WhileDraftInFlight` | OQ-1B-14, UAT-1B-02 | **QUALIFIED** |
| **DC-URS-066** | `FS-1b-066` | RA-066 | Submission to Technical Review | `DocumentReviewService` | `POST /api/document-control/revisions/{id}/submit-review` | Status: `Draft` -> `InReview` | `SubmitForReviewDialog`, Action Button | `SubmitForReview_TransitionsToInReview` | OQ-1B-15, UAT-1B-03 | **QUALIFIED** |
| **DC-URS-067** | `FS-1b-067` | RA-067 | Side-by-side revision inspection | `ControlledPdfViewer` | Frontend Comparison Workspace | Retrieves both active and proposed files | Side-by-Side Dual PDF Viewer | `TechnicalReview_RendersSideBySidePdfs` | OQ-1B-16, UAT-1B-03 | **QUALIFIED** |
| **DC-URS-068** | `FS-1b-068` | RA-068 | Page/section review comments | `DocumentReviewService` | `POST /api/document-control/reviews/{id}/comments` | Inserts `DocumentReviewFinding` | `TechnicalReviewDrawer` Findings Panel | `TechnicalReview_AddComment_StoresPageSection` | OQ-1B-17, UAT-1B-03 | **QUALIFIED** |
| **DC-URS-069** | `FS-1b-069` | RA-069 | Comment lifecycle tracking | `DocumentReviewService` | `PUT /api/document-control/comments/{id}/status` | Updates `FindingStatus` enum | Comment Thread Status Badge | `CommentLifecycle_OpenToResolved` | OQ-1B-17, UAT-1B-03 | **QUALIFIED** |
| **DC-URS-070** | `FS-1b-070` | RA-070 | Author prohibited from closing comments | `DocumentReviewService` | `PUT /api/document-control/comments/{id}/status` | Enforces `CurrentUserId == ReviewerId` | Comment Close Button Disabled for Author | `Author_CannotCloseReviewerComment_ThrowsForbidden` | OQ-1B-18, UAT-1B-03 | **QUALIFIED** |
| **DC-URS-071** | `FS-1b-071` | RA-071 | Open mandatory comments block review | `DocumentReviewService` | `POST /api/document-control/reviews/{id}/complete` | Checks `IsMandatory && Status != Resolved` | Complete Review Disabled State | `CompleteReview_BlockedByMandatoryComments` | OQ-1B-18, UAT-1B-03 | **QUALIFIED** |
| **DC-URS-072** | `FS-1b-072` | RA-072 | Technical review outcomes | `DocumentReviewService` | `POST /api/document-control/reviews/{id}/decide` | `Draft` (returned) or `AwaitingApproval` | Return for Correction / Complete Buttons | `TechnicalReview_Decide_TransitionsCorrectly` | OQ-1B-19, UAT-1B-03 | **QUALIFIED** |
| **DC-URS-073** | `FS-1b-073` | RA-073 | Awaiting Approval transition | `DocumentReviewService` | `POST /api/document-control/reviews/{id}/complete` | Status: `InReview` -> `AwaitingApproval` | Workflow Status Badge | `TechnicalReview_Completion_SetsAwaitingApproval` | OQ-1B-19, UAT-1B-03 | **QUALIFIED** |
| **DC-URS-074** | `FS-1b-074` | RA-074 | Approver inspection dossier | `DocumentApprovalService` | `GET /api/document-control/approvals/{id}/dossier` | Aggregates PDF, Impact, Review Log | Approval Workspace Dossier View | `ApprovalDossier_CompilesFullEvidence` | OQ-1B-20, UAT-1B-04 | **QUALIFIED** |
| **DC-URS-075** | `FS-1b-075` | RA-075 | Approver decision options | `DocumentApprovalService` | `POST /api/document-control/approvals/{id}/decide` | `Effective`/`FutureEffective` / `Draft` / `Cancelled` | Approve / Return / Decline Actions | `ApproverDecision_ExecutesSelectedAction` | OQ-1B-20, UAT-1B-04 | **QUALIFIED** |
| **DC-URS-076** | `FS-1b-076` | RA-076 | Authenticated e-signature for approval | `ElectronicSignatureService` | `POST /api/document-control/approvals/{id}/sign` | Inserts `ElectronicSignatures` row | Electronic Signature Dialog | `ApproveRevision_RequiresValidPassword_WritesESig` | OQ-1B-21, UAT-1B-04 | **QUALIFIED** |
| **DC-URS-077** | `FS-1b-077` | RA-077 | E-signature record completeness | `ElectronicSignature` | DB Insert | Captures User, Role, UTC Time, Meaning | Signature Manifest Badge | `ESignature_CapturesCompletePart11Metadata` | OQ-1B-21, UAT-1B-04 | **QUALIFIED** |
| **DC-URS-078** | `FS-1b-078` | RA-078 | Segregation of Duties enforcement | `DocumentAuthorizationService` | Evaluated across all review & approval endpoints | Hard service exception on conflict | UI Hiding + Server Enforcement | `SoD_AuthorCannotReviewOrApprove_ReviewerCannotApprove` | OQ-1B-22, UAT-1B-05 | **QUALIFIED** |
| **DC-URS-079** | `FS-1b-079` | RA-079 | Complete audit trail for workflow actions | `AuditEventService` | Cross-cutting across review & approval | Additive rows in `AuditLogs` | Document Details Audit Tab | `WorkflowActions_EmitFullSemanticAuditTrail` | OQ-1B-23, UAT-1B-08 | **QUALIFIED** |
| **DC-URS-177** | `FS-1b-177` | RA-177 | Automatic activation of future effective | `DocumentEffectiveDateWorker` | Background Hosted Service | `FutureEffective` -> `Effective` | System Status Indicator | `Worker_ActivatesFutureEffectiveRevisions` | OQ-1B-24, UAT-1B-06 | **QUALIFIED** |
| **DC-URS-178** | `FS-1b-178` | RA-178 | Automatic supersession of prior effective | `DocumentEffectiveDateWorker` | Background Hosted Service | Prior `Effective` -> `Superseded` | Revision History Table | `Worker_SupersedesPriorEffective_InSameTx` | OQ-1B-24, UAT-1B-06 | **QUALIFIED** |
| **DC-URS-179** | `FS-1b-179` | RA-179 | Automatic periodic review task creation | `DocumentEffectiveDateWorker` | Background Hosted Service | Inserts `PeriodicReviewTask` | Review Tasks Dashboard | `Worker_SchedulesPeriodicReviewTasks` | OQ-1B-01, UAT-1B-01 | **QUALIFIED** |
| **DC-URS-180** | `FS-1b-180` | RA-180 | System attribution in audit trail | `AuditEventService` | Background Hosted Service | `ActorType.System`, `Username = "SYSTEM"` | Audit Trail User Column | `Worker_EmitsSystemAttributedAuditLogs` | OQ-1B-25, UAT-1B-06 | **QUALIFIED** |
| **DC-URS-181** | `FS-1b-181` | RA-181 | Downtime recovery for scheduled actions | `DocumentEffectiveDateWorker` | Background Hosted Service on Startup | Evaluates missed events; sets `DelayedExecution` | Delayed Execution Audit Flag | `Worker_RecoversFromDowntime_ProcessesMissedEvents` | OQ-1B-26, UAT-1B-06 | **QUALIFIED** |
| **DC-URS-182** | `FS-1b-182` | RA-182 | UTC time zone standardization | Database & Worker Services | Evaluated against `DateTime.UtcNow` | All timestamps in UTC | System Time Display | `Worker_EvaluatesStrictlyAgainstUtcClock` | OQ-1B-27, UAT-1B-06 | **QUALIFIED** |
| **DC-URS-183** | `FS-1b-183` | RA-183 | Scheduled process failure alerting | `DocumentEffectiveDateWorker` | Background Hosted Service | Logs `SystemProcessError` in `AuditLogs` | Admin System Alerts Banner | `Worker_CapturesExceptions_EmitsIncidentAlert` | OQ-1B-28, UAT-1B-06 | **QUALIFIED** |

---

## 3. RTM Qualification Summary & Sign-Off

- **Authoritative Release 1b Requirement Total:** **43 Requirements**
- **Requirements Formally Qualified:** **43 (100%)**
- **Requirements Disqualified / Pending:** **0 (0%)**
- **Qualification Protocol References:** `Release_1b_OQ_Execution_Report.md`, `Release_1b_UAT_Execution_Report.md`
- **Validation Closure Reference:** `Release_1b_Validation_Summary_Report.md`

All 43 requirements have demonstrated full bidirectional traceability and satisfactory execution of planned OQ/UAT acceptance criteria with zero open deviations.
