# Release 1d Implementation Plan & Work Package Governance
## Controlled Execution Roadmap for Word-First Lifecycle & Post-Approval Controlled PDF Release

- **Document ID:** `ML-DC-IP-1D-001`
- **Version:** 1.0 (Formal Planning Baseline — WP1)
- **Status:** **APPROVED FOR EXECUTION PLANNING**
- **Governing Change Control:** `CC-DC-R1D-001`
- **Architecture Baseline:** `ADR-DC-004`
- **Requirements Baseline:** `ML-DC-URS-1D-001`, `ML-DC-FRS-1D-001`, `ML-DC-RTM-1D-001`
- **Date:** September 7, 2026

---

### 1. Phasing Strategy & Work Package Governance

To preserve the qualified Release 1c baseline and prevent uncontrolled regressions, Release 1d is structured into eight tightly scoped, sequential Work Packages (WPs).

```
                      RELEASE 1d WORK PACKAGE EXECUTION PHASING
                      
  [ WP1: Change Control, Delta URS/FRS, ADR, RTM ]  ◄── [ CURRENT: COMPLETE ]
        │
        ▼
  [ WP2: Domain Model & Database Lineage ]
        │
        ├───────────────────────────────────────────────────────┐
        ▼                                                       ▼
  [ WP3: Authorization & Word File Versioning ]       [ WP5: Gotenberg Conversion Engine & Release ]
        │                                                       │
        ▼                                                       │
  [ WP4: Multi-Cycle Review & Finding Continuity ]              │
        │                                                       │
        ├───────────────────────────────────────────────────────┘
        ▼
  [ WP6: Author & Reviewer Workspace UI ]
        │
        ▼
  [ WP7: Approval & Controlled Distribution UI ]
        │
        ▼
  [ WP8: Multi-User Integration, Regression & Formal OQ/UAT Qualification ]
```

#### Mandatory GxP & Governance Invariants:
1. **Implementation Gate:** Execution of WP2 shall commence only upon formal sign-off of WP1 planning documents.
2. **Authenticated Business Execution:** Qualification test suites in WP8 and throughout all WPs must use genuine, independently authenticated credentials for Author, Reviewer, Approver, and Analyst roles. Administrative accounts (`admin`) shall never execute business tasks under the guise of an author or approver, and operational database password resets shall be strictly prohibited during verification.
3. **Additive-Only Schema Changes:** All database changes in WP2 must be strictly additive (new nullable columns, new foreign keys, new file roles) to guarantee that Release 1c data remains intact and functional.

---

### 2. Detailed Work Package Specifications

---

#### WORK PACKAGE 1 (WP1): Change Control, Delta Requirements & Architecture Baseline
- **Status:** **COMPLETE**
- **Objective:** Establish formal regulatory change control, delta user requirements, delta functional specifications, architecture decisions, and delta traceability matrix.
- **Scope:** Author `Release_1d_Change_Control.md`, `Release_1d_Delta_URS.md`, `Release_1d_Delta_FRS.md`, `Release_1d_ADR_Document_Lifecycle.md`, `Release_1d_RTM_Delta.md`, and `Release_1d_Implementation_Plan.md`.
- **Affected Files:** All documentation artifacts in `E:\MicroLIMS\MicroLIMS\docs`. Zero code or database modifications.
- **Dependencies:** None (Initiation baseline).
- **Acceptance Criteria:** All six planning deliverables approved; 100% traceability coverage from Delta URS to FRS; four architectural decisions formalized.
- **Test Strategy:** N/A (Documentation / Peer review).
- **Qualification Impact:** Governance foundation for Release 1d.
- **Rollback Considerations:** Delete or archive WP1 markdown files if change control is canceled.

---

#### WORK PACKAGE 2 (WP2): Domain Model & Database Lineage
- **Objective:** Enhance entity data models and apply additive database migrations to establish explicit sequential file versioning, source-to-PDF linkage, and multi-cycle review task lineage.
- **Scope:**
  - Update `RevisionFile.cs`: Add `FileVersion` (int), `IsApprovedFinalSource` (bool), `GeneratedFromSourceFileId` (int?).
  - Update `DocumentRevision.cs`: Add `ApprovedSourceFileId` (int?), `ControlledPdfFileId` (int?).
  - Update `DocumentApprovalTask.cs`: Add `ApprovedSourceFileId` (int?), `GeneratedControlledPdfId` (int?).
  - Update `DocumentReviewFinding.cs`: Add `SourceFileVersion` (int?), `RevisionFileId` (int?).
  - Update `DocumentReviewTask.cs`: Add `ReviewCycleNumber` (int, default 1), `ReviewedSourceFileId` (int?).
  - Update EF Core configurations in `MicroLIMS.Persistence/Configurations`.
  - Generate and verify EF Core Migration: `20260907_Release1d_DocumentLifecycleLineage.cs`.
- **Affected Files:**
  - `backend/MicroLIMS.Domain/Entities/RevisionFile.cs`
  - `backend/MicroLIMS.Domain/Entities/DocumentRevision.cs`
  - `backend/MicroLIMS.Domain/Entities/DocumentApprovalTask.cs`
  - `backend/MicroLIMS.Domain/Entities/DocumentReviewFinding.cs`
  - `backend/MicroLIMS.Domain/Entities/DocumentReviewTask.cs`
  - `backend/MicroLIMS.Persistence/Configurations/*.cs`
  - `backend/MicroLIMS.Persistence/Migrations/*`
- **Dependencies:** WP1 Sign-off.
- **Acceptance Criteria:**
  - Migration applies cleanly to PostgreSQL without modifying or truncating existing Release 1c tables.
  - Foreign key navigation properties resolve correctly in EF Core change tracker.
  - Down-migration cleanly removes additive columns without data loss on core tables.
- **Test Strategy:** Automated database migration test, EF Core model validation test suite.
- **Qualification Impact:** Modifies database schema; covered under delta OQ protocol (`OQ-1D-03`).
- **Rollback Considerations:** Revert EF Core migration using `dotnet ef database update 20260906161305_AddDilutionFactorConfigAndAudit`.

---

#### WORK PACKAGE 3 (WP3): Authorization & Word File Versioning Subsystem
- **Objective:** Update authorization rules to allow assigned reviewers and approvers to download the active Word source file, and implement automatic sequential file versioning during author upload.
- **Scope:**
  - Update `DocumentAuthorizationService.CanAccessSourceFileAsync`: Permit assigned reviewers and approvers on active tasks for the revision to download the Word source file.
  - Update `DocumentFileService.UploadRevisionFileAsync`:
    - Accept `.docx` and `.doc` files.
    - Calculate sequential `FileVersion` monotonically per revision.
    - Deactivate previous active file (`IsActive = false`) and link `SupersededByFileId`.
    - Enforce Draft-only author upload while permitting correction uploads on returned revisions.
  - Update `DocumentFilesController`: Verify content headers and MIME types for Word downloads.
- **Affected Files:**
  - `backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuthorizationService.cs`
  - `backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs`
  - `backend/MicroLIMS.Application/DTOs/DocumentControl/DocumentControlDtos.cs`
  - `backend/MicroLIMS.API/Controllers/DocumentControl/DocumentFilesController.cs`
- **Dependencies:** WP2 Migration.
- **Acceptance Criteria:**
  - Assigned reviewer can download author's Word document via API.
  - Non-assigned users (viewers, trainees, unrelated analysts) receive HTTP 403 Forbidden.
  - Sequential file versioning increments correctly: initial upload is `v1`; second is `v2`.
  - Reviewer cannot upload or replace author's Word file.
- **Test Strategy:** Unit tests for file service; security matrix integration tests for authorization.
- **Qualification Impact:** High (Security and data integrity verification via `OQ-1D-02`, `OQ-1D-03`, `OQ-1D-04`).
- **Rollback Considerations:** Revert modified service classes to Release 1c git commit.

---

#### WORK PACKAGE 4 (WP4): Multi-Cycle Review & Finding Continuity Engine
- **Objective:** Refactor review submission gating to require an active Word source, implement multi-turn review task management, and enforce the mandatory findings gate across all cycles.
- **Scope:**
  - Update `DocumentReviewService.SubmitForReviewAsync`:
    - Remove Controlled PDF requirement; require active `SourceFile` (.docx/.doc).
    - Capture `ReviewedSourceFileId = activeSource.Id`.
    - Support multi-cycle resubmission, carrying forward all open and historical findings.
  - Update `DocumentReviewService.AddReviewFindingAsync`:
    - Snapshot `SourceFileVersion` and `RevisionFileId` onto the finding.
  - Update `DocumentReviewService.RespondToFindingAsync`:
    - Allow author responses when review task status is `ReturnedForCorrection` or revision is in correction state.
  - Update `DocumentReviewService.DecideReviewAsync`:
    - Return for correction transitions revision to `Draft` and review task to `ReturnedForCorrection`, preserving findings.
    - Complete review executes global mandatory findings check across all cycles, blocking completion if any open mandatory findings remain.
- **Affected Files:**
  - `backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs`
  - `backend/MicroLIMS.Application/DTOs/DocumentControl/DocumentReviewTaskDto.cs`
  - `backend/MicroLIMS.Application/DTOs/DocumentControl/DocumentReviewFindingDto.cs`
- **Dependencies:** WP2, WP3.
- **Acceptance Criteria:**
  - Revision submits to review with only a Word file attached.
  - Review findings preserve the Word file version they were raised against.
  - Mandatory findings raised in Cycle 1 block completion in Cycle 2 until marked `Resolved`.
  - Author can submit text responses to findings after reviewer returns revision for correction.
- **Test Strategy:** Multi-cycle state machine integration test suite; negative gate enforcement tests.
- **Qualification Impact:** Critical (Quality gate and regulatory review integrity via `OQ-1D-05`, `OQ-1D-07`, `OQ-1D-08`).
- **Rollback Considerations:** Revert `DocumentReviewService` to Release 1c git commit.

---

#### WORK PACKAGE 5 (WP5): DOCX-to-PDF Conversion & Post-Approval Release Pipeline
- **Objective:** Integrate the containerized Gotenberg conversion service, implement atomic approval post-processing, and generate the immutable Controlled PDF.
- **Scope:**
  - Define `IDocumentConversionService` in `MicroLIMS.Application/Interfaces/DocumentControl`.
  - Implement `GotenbergDocumentConversionService` in `MicroLIMS.Infrastructure/Conversion`:
    - Configured with endpoint `http://gotenberg:3000/forms/libreoffice/convert`.
    - Implements Polly retry policy for transient timeouts with a 60-second absolute timeout.
  - Update `DocumentApprovalService.EvaluateReadiness`:
    - Validate presence of active reviewed Word source and resolution of all mandatory findings.
    - Remove requirement for pre-existing Controlled PDF.
  - Update `DocumentApprovalService.ExecuteApprovalDecisionAsync`:
    - Execute within database transaction (`BeginTransactionAsync`).
    - Authenticate password via BCrypt and record 21 CFR Part 11 e-signature.
    - Pin active Word source: `IsApprovedFinalSource = true`.
    - Invoke `IDocumentConversionService.ConvertDocxToPdfAsync`.
    - Compute SHA-256 hash of generated PDF bytes.
    - Store PDF via `IFileStorageService` and create `RevisionFile` (`FileRole.ControlledPdf`, `GeneratedFromSourceFileId`).
    - Link `revision.ControlledPdfFileId` and `task.GeneratedControlledPdfId`.
    - Log semantic audit events `ControlledPdfGenerated` and `ControlledPdfRegistered`.
    - If conversion or registration fails: Roll back transaction completely, abort e-signature, return HTTP 500.
- **Affected Files:**
  - `backend/MicroLIMS.Application/Interfaces/DocumentControl/IDocumentConversionService.cs`
  - `backend/MicroLIMS.Infrastructure/Conversion/GotenbergDocumentConversionService.cs`
  - `backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs`
  - `backend/MicroLIMS.API/Controllers/DocumentControl/DocumentApprovalController.cs`
  - `backend/MicroLIMS.API/Program.cs` (Service registration)
  - `docker-compose.yml` (Gotenberg container definition)
- **Dependencies:** WP2, WP3, WP4.
- **Acceptance Criteria:**
  - Approval executes e-signature and generates Controlled PDF in a single atomic transaction.
  - Controlled PDF SHA-256 is recorded and matches physical file content.
  - If Gotenberg is offline, approval transaction rolls back completely; zero orphaned signatures exist.
- **Test Strategy:** Mocked unit tests for rollback logic; containerized integration tests with real Gotenberg service; fault-injection tests.
- **Qualification Impact:** Critical (21 CFR Part 11 compliance and controlled document release via `OQ-1D-10`, `OQ-1D-11`, `OQ-1D-12`, `OQ-1D-14`).
- **Rollback Considerations:** Revert `DocumentApprovalService` and disable Gotenberg container.

---

#### WORK PACKAGE 6 (WP6): Author & Reviewer Workspace UI
- **Objective:** Upgrade the Document Detail page and Technical Review Drawer to support Word-first authoring, multi-turn review cycles, and correction response interactions.
- **Scope:**
  - Update `DocumentDetailPage.tsx`:
    - Key authoring actions ("Submit for Review") off active Word source rather than PDF.
    - Display prominent "Correction Required" banner when revision is in `Draft` following a return.
    - Render Word file version lineage in Files tab (`v1`, `v2`, `v3`).
  - Update `TechnicalReviewDrawer.tsx`:
    - Display current Word source filename, version, and SHA-256 checksum.
    - Add "Download Word Source" button for assigned reviewer.
    - Fix Author "Respond" button visibility: allow response when task is `ReturnedForCorrection`.
    - Tag findings with Word version number (`v1`, `v2`).
    - Display multi-cycle review history.
  - Update `FileUploadDialog.tsx`:
    - Contextualize modal for Word upload / correction replacement.
- **Affected Files:**
  - `frontend/src/modules/documentControl/pages/DocumentDetailPage.tsx`
  - `frontend/src/modules/documentControl/components/TechnicalReviewDrawer.tsx`
  - `frontend/src/modules/documentControl/components/SubmitForReviewDialog.tsx`
  - `frontend/src/modules/documentControl/components/FileUploadDialog.tsx`
- **Dependencies:** WP3, WP4.
- **Acceptance Criteria:**
  - Author can submit for review with only Word document uploaded.
  - Reviewer can download Word document directly from review drawer.
  - Author can respond to findings after document is returned for correction.
  - Reviewer can see prior cycle findings and verify corrections on newly uploaded version.
- **Test Strategy:** Frontend component test suite, Cypress / Playwright automated UI workflow tests.
- **Qualification Impact:** User interface operational qualification (`OQ-1D-06`, `OQ-1D-09`, `UAT-1D-04`, `UAT-1D-05`).
- **Rollback Considerations:** Revert frontend git branch.

---

#### WORK PACKAGE 7 (WP7): Approval & Controlled Distribution UI
- **Objective:** Upgrade the Approval Workspace to inspect the final reviewed Word source and verify that downstream user views deliver strictly the post-approval Controlled PDF.
- **Scope:**
  - Update `ApprovalWorkspaceDialog.tsx`:
    - Replace "Controlled PDF Evidence" with "Final Reviewed Word Source Evidence" (filename, version, SHA-256 hash, download button).
    - Update readiness checklist: check for reviewed Word source and resolved findings.
    - Update approval ceremony instructions: explain that approval generates the immutable Controlled PDF.
  - Verify downstream presentation in `DocumentDetailPage.tsx`, `DocumentLibraryPage.tsx`, and `MyReadingListPage.tsx`:
    - Ensure regular users can open and download strictly the post-approval generated Controlled PDF.
    - Verify Controlled PDF viewer renders the generated artifact with correct header and pagination.
- **Affected Files:**
  - `frontend/src/modules/documentControl/components/ApprovalWorkspaceDialog.tsx`
  - `frontend/src/modules/documentControl/pages/DocumentDetailPage.tsx`
  - `frontend/src/modules/documentControl/pages/DocumentLibraryPage.tsx`
  - `frontend/src/modules/documentControl/pages/MyReadingListPage.tsx`
- **Dependencies:** WP5, WP6.
- **Acceptance Criteria:**
  - Approver inspects Word source metadata and full multi-cycle review finding history.
  - Approver signs with 21 CFR Part 11 electronic signature; UI displays generation progress.
  - Upon approval, Controlled PDF appears in document library for general users.
- **Test Strategy:** Frontend integration tests, approval dialog component tests.
- **Qualification Impact:** High (Approval workflow verification via `OQ-1D-10`, `OQ-1D-13`, `UAT-1D-07`, `UAT-1D-08`).
- **Rollback Considerations:** Revert frontend git branch.

---

#### WORK PACKAGE 8 (WP8): Multi-User Integration, Regression & Formal OQ/UAT Qualification
- **Objective:** Execute formal delta qualification protocols in an integrated staging environment using genuine multi-user authenticated sessions, proving zero regression of Release 1c.
- **Scope:**
  - Execute automated regression suite covering all Release 1a, 1b, and 1c tests (target: 100% pass rate).
  - Execute formal Delta OQ Protocol (`OQ-1D-01` through `OQ-1D-18`).
  - Execute formal Operational UAT Scenarios (`UAT-1D-01` through `UAT-1D-11`):
    - *Scenario 1:* Happy Path: Authoring in Word ➔ Review ➔ Approval ➔ Gotenberg PDF Generation ➔ Effective Release ➔ Training Acknowledgement.
    - *Scenario 2:* Iterative Correction Path: Cycle 1 Findings ➔ Return for Correction ➔ Author Correction in Word (v2) ➔ Cycle 2 Re-Review ➔ Verification ➔ Approval.
    - *Scenario 3:* Mandatory Findings Gate: Attempted approval/completion with open mandatory finding ➔ System blocks.
    - *Scenario 4:* Fault Injection: Simulated Gotenberg conversion crash during approval ➔ Atomic rollback verification.
    - *Scenario 5:* Strict Segregation of Duties: Author cannot review or approve; Reviewer cannot approve; Admin subject to identical restrictions.
  - Compile `Release_1d_OQ_Execution_Record.md`, `Release_1d_UAT_Execution_Record.md`, and `Release_1d_Validation_Summary_Report.md`.
- **Affected Files:**
  - `backend/MicroLIMS.Tests/*`
  - `docs/Release_1d_OQ_Protocol.md`, `docs/Release_1d_UAT_Protocol.md`, `docs/Release_1d_Validation_Summary_Report.md`
- **Dependencies:** WP2 through WP7 complete and merged in staging branch.
- **Acceptance Criteria:**
  - 100% of Release 1d Delta URS requirements verified (`DC-URS-1D-001` through `DC-URS-1D-025`).
  - 100% pass rate on Release 1a, 1b, and 1c regression tests.
  - Zero open Critical or Major deviations.
  - Genuine authenticated user sessions used exclusively (no admin impersonation).
- **Test Strategy:** Automated xUnit integration tests against PostgreSQL; Playwright end-to-end browser automation; formal witness-verified UAT.
- **Qualification Impact:** Final formal qualification and release authorization for Release 1d.
- **Rollback Considerations:** Fix forward under formal deviation procedure or abort deployment.

---

### 3. Traceability of Work Packages to Release 1d Deliverables

| Work Package | Focus Area | Responsible Role | Key Deliverable Artifacts | Target Gate |
| :--- | :--- | :--- | :--- | :---: |
| **WP1** | Requirements & Planning | System Architect / CSV Lead | Change Control, Delta URS, Delta FRS, ADR, RTM | **GATE 1** (Approved) |
| **WP2** | Database & Domain Model | Senior Backend Engineer | Entity models, EF Core Migration script | **GATE 2** |
| **WP3** | Auth & File Versioning | Senior Backend Engineer | File Service, Authorization Service, Unit Tests | **GATE 3** |
| **WP4** | Review Engine & Cycles | Senior Backend Engineer | Review Service, Decision Engine, Gate Tests | **GATE 4** |
| **WP5** | Gotenberg & Approval | Principal Backend Architect | Gotenberg Service, Approval Atomic Transaction | **GATE 5** |
| **WP6** | Author & Review UI | Senior Frontend Engineer | Detail Page, Review Drawer, File Upload Modal | **GATE 6** |
| **WP7** | Approval & Library UI | Senior Frontend Engineer | Approval Workspace, Controlled PDF Viewer integration | **GATE 7** |
| **WP8** | Delta Qualification | CSV Lead & Independent Witness | Delta OQ/UAT Records, Validation Summary Report | **GATE 8** (Release) |
