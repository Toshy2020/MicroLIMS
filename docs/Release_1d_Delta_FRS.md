# Release 1d Delta Functional Requirements Specification (Delta FRS)
## Functional Behavior for Word-First Authoring, Review Lineage & Post-Approval Controlled PDF Release

- **Document ID:** `ML-DC-FRS-1D-001`
- **Version:** 1.0 (Formal Delta Baseline)
- **Status:** **APPROVED FOR PLANNING & ARCHITECTURE**
- **Governing URS Baseline:** `ML-DC-URS-1D-001` (`DC-URS-1D-001` .. `DC-URS-1D-025`)
- **Governing Change Control:** `CC-DC-R1D-001`
- **Previous Qualified Functional Baseline:** `ML-DC-FRS-1C-001`
- **Date:** September 7, 2026

---

### 1. Architectural Principles & Operational Overview

This Functional Requirements Specification defines the precise system behavior, preconditions, success states, failure handling, authorization, and audit trail outputs for all capabilities introduced in Release 1d.

```
       RELEASE 1d FUNCTIONAL STATE FLOW & BEHAVIOR
       
 [Draft Revision]
       │
       ▼
 [FS-1d-001 / FS-1d-002: Word Ingestion & Versioning] ──► Upload .docx (v1, SHA-256)
       │
       ▼
 [FS-1d-003: Review Submission Gate] ──────────────────► Validates active Word source
       │
       ▼
 [FS-1d-004 / FS-1d-005: Reviewer Workspace] ──────────► Reviewer downloads Word v1 (Read-Only)
       │
       ▼
 [FS-1d-006: Versioned Finding Creation] ──────────────► Finding logged against Word v1
       │
       ├─────────────────────────────────────────┐
       │                                         │
       ▼                                         ▼
 [FS-1d-008: Decision = Complete]          [FS-1d-008: Decision = Return for Correction]
       │                                         │
       │                                         ▼
       │                                   [FS-1d-007: Author Response & Corrected Word Upload]
       │                                         │
       │                                         ▼ Upload Word v2 (SupersededByFileId=v2)
       │                                   [FS-1d-009: Multi-Cycle Re-Review]
       │                                         │
       │                                         ▼ Reviewer verifies v1 findings in v2
       │                                   [FS-1d-010: Mandatory Findings Gate]
       │                                         │ (Blocks if any mandatory finding open)
       ▼                                         │
 [FS-1d-011: Approval Dossier] ◄─────────────────┘
       │
       ▼ Approver inspects Word source & review history
 [FS-1d-012: Atomic Approval & 21 CFR Part 11 e-Signature]
       │
       ├──► [Pass] ──► [FS-1d-013 / FS-1d-014: Post-Approval PDF Conversion & Registration]
       │                      │
       │                      ├──► Word pinned: IsApprovedFinalSource = true
       │                      ├──► Gotenberg renders PDF directly from approved Word
       │                      ├──► PDF SHA-256 computed & stored
       │                      └──► Audit log: ControlledPdfGenerated (Word SHA + PDF SHA)
       │
       └──► [Conversion Fail] ──► [FS-1d-015: Atomic Transaction Rollback]
                                     └──► e-Signature rolled back, revision remains AwaitingApproval
```

---

### 2. Functional Requirements Specifications

#### FS-1d-001: Word Source File Ingestion & Format Enforcement
- **Derived URS:** `DC-URS-1D-001`, `DC-URS-1D-002`
- **Business Behavior:**  
  When an author uploads a working file to a revision in `Draft` status, the system accepts only `.docx` and `.doc` files. The system checks extension, declared MIME type, and ensures file size $\le 50\text{ MB}$. Ingestion of a PDF file as the working source is rejected during Draft authoring.
- **Preconditions:**  
  1. Target revision must be in `Draft` status.  
  2. Calling user must be the designated Document Owner or assigned Author.
- **Success Conditions:**  
  1. File bytes written to physical storage at `documents/{revisionId}/{fileId}_sourcefile.docx`.  
  2. SHA-256 hash computed and stored on new `RevisionFile` entity.  
  3. `FileRole` set to `SourceFile`.  
  4. Response returns HTTP 200 OK with `RevisionFileDto`.
- **Failure Conditions:**  
  1. Non-Word extension: HTTP 400 Bad Request (*"Source document file must be a Word document (.docx or .doc)."*).  
  2. Empty content or size $>50\text{ MB}$: HTTP 400 Bad Request.  
  3. Non-draft status: HTTP 422 Unprocessable Entity (*"Files can only be added or replaced on revisions in Draft state."*).  
  4. Unauthorized caller: HTTP 403 Forbidden.
- **Audit Expectation:**  
  `RevisionFileUploaded` logged with `FileName`, `SizeBytes`, `ContentSha256`, and `FileRole = SourceFile`.
- **Authorization:**  
  Document Owner, assigned Author, SectionHead, or SystemAdministrator.

#### FS-1d-002: Sequential Word File Versioning & Lineage Preservation
- **Derived URS:** `DC-URS-1D-003`, `DC-URS-1D-004`, `DC-URS-1D-005`
- **Business Behavior:**  
  When an author uploads a Word document, the system determines the next sequential `FileVersion`. If no prior Word file exists for the revision, `FileVersion = 1`. If an active Word file exists, the existing file is updated to `IsActive = false`, its `SupersededByFileId` is set to the new file's ID, and the new file receives `FileVersion = priorVersion + 1` and `IsActive = true`.
- **Preconditions:**  
  Revision in `Draft` status; valid Word document payload provided.
- **Success Conditions:**  
  1. New `RevisionFile` row created with monotonically incremented `FileVersion`.  
  2. Previous `RevisionFile` remains in database with `IsActive = false` and valid `SupersededByFileId`.  
  3. Physical files for both versions remain retrievable and unchanged on disk.
- **Failure Conditions:**  
  Database concurrency conflict: HTTP 409 Conflict with transaction retry.
- **Audit Expectation:**  
  `RevisionFileReplaced` logged with `PreviousFileVersion`, `NewFileVersion`, `PreviousSha256`, and `NewSha256`.
- **Authorization:**  
  Document Owner, assigned Author, SectionHead, or SystemAdministrator.

#### FS-1d-003: Review Submission Gate
- **Derived URS:** `DC-URS-1D-001`, `DC-URS-1D-024`
- **Business Behavior:**  
  Author submits a draft revision for technical review via `POST /api/document-control/revisions/{id}/submit-review`. The system validates that an active Word source file exists on the revision. The system no longer requires a Controlled PDF file.
- **Preconditions:**  
  1. Revision status is `Draft`.  
  2. Revision possesses an active `RevisionFile` with `FileRole == SourceFile`.  
  3. Segregation of Duties: Selected reviewer ID $\ne$ revision author ID (`revision.CreatedByUserId`).  
  4. Impact assessment completed (if revision sequence $>1$).
- **Success Conditions:**  
  1. New `DocumentReviewTask` instantiated in status `Pending` with `ReviewCycleNumber = 1`.  
  2. Revision status transitions to `InReview`.  
  3. Task response returns HTTP 200 OK.
- **Failure Conditions:**  
  1. No active Word source file: HTTP 422 Unprocessable Entity (*"Cannot submit revision for technical review without an active Word source document (.docx/.doc)."*).  
  2. Author assigned as reviewer: HTTP 403 Forbidden (SoD violation).  
  3. Status not Draft: HTTP 422 Unprocessable Entity.
- **Audit Expectation:**  
  `RevisionSubmittedForReview` logged with `ReviewerUserId`, `SourceFileVersion`, and `DueDate`.
- **Authorization:**  
  Document Owner, assigned Author, SectionHead, or SystemAdministrator.

#### FS-1d-004: Reviewer Read-Only Source File Download
- **Derived URS:** `DC-URS-1D-006`, `DC-URS-1D-007`
- **Business Behavior:**  
  Assigned technical reviewer downloads the active Word source file via `GET /api/document-control/files/{fileId}/download`. The authorization service verifies that the user is the assigned reviewer on the revision's active review task.
- **Preconditions:**  
  1. Target file has `FileRole == SourceFile`.  
  2. Calling user is the assigned reviewer on an active `DocumentReviewTask` for the revision, OR Document Owner/Author/SectionHead/Admin.
- **Success Conditions:**  
  1. Cryptographic SHA-256 hash verified against storage content.  
  2. File stream returned with headers `Content-Disposition: attachment; filename="{safeFileName}"` and MIME type `application/vnd.openxmlformats-officedocument.wordprocessingml.document`.
- **Failure Conditions:**  
  1. Unassigned user: HTTP 403 Forbidden (*"Access to source/editable document files is restricted."*).  
  2. SHA-256 mismatch: HTTP 500 Internal Server Error (*"File integrity check failed. Delivery has been blocked."*) with security audit event `FileIntegrityVerificationFailed`.
- **Audit Expectation:**  
  `SourceFileDownloaded` logged with `FileId`, `FileVersion`, and `UserId`.
- **Authorization:**  
  Assigned Reviewer (read-only), Document Owner, Author, SectionHead, SystemAdministrator.

#### FS-1d-005: Review Workspace Presentation & Correction Callout
- **Derived URS:** `DC-URS-1D-009`, `DC-URS-1D-014`
- **Business Behavior:**  
  The Technical Review Drawer (`TechnicalReviewDrawer.tsx`) renders the current review task details, including active Word filename, sequential version badge (`v1`, `v2`), SHA-256 checksum snippet, and a prominent "Download Word Source" button. When the task is in status `ReturnedForCorrection`, author response controls are fully accessible.
- **Preconditions:**  
  User has view access to the document.
- **Success Conditions:**  
  UI displays Word source metadata, download button, and finding interaction controls appropriate to the user's role.
- **Failure Conditions:**  
  Network/API error displays user-friendly error banner.
- **Audit Expectation:**  
  N/A (Client-side view render).
- **Authorization:**  
  Authenticated users with document view permissions.

#### FS-1d-006: Versioned Finding Creation
- **Derived URS:** `DC-URS-1D-008`, `DC-URS-1D-024`
- **Business Behavior:**  
  Reviewer adds a review finding via `POST /api/document-control/reviews/{taskId}/findings`. The system snapshots the currently active Word file's ID (`RevisionFileId`) and version number (`SourceFileVersion`) into the finding record, along with page number, section number, comment text, and mandatory status flag.
- **Preconditions:**  
  1. Calling user is assigned reviewer or SectionHead.  
  2. Calling user is NOT the revision author (SoD check).  
  3. Review task status is `Pending` or `InProgress`.
- **Success Conditions:**  
  1. `DocumentReviewFinding` row inserted with `Status = Open`.  
  2. `RevisionFileId` and `SourceFileVersion` captured.  
  3. Review task status advanced to `InProgress` (if `Pending`).
- **Failure Conditions:**  
  1. Author attempts to add reviewer finding: HTTP 403 Forbidden.  
  2. Empty comment text: HTTP 400 Bad Request.  
  3. Review task completed or returned: HTTP 422 Unprocessable Entity.
- **Audit Expectation:**  
  `ReviewFindingCreated` logged with `FindingId`, `PageNumber`, `SectionNumber`, `IsMandatory`, `SourceFileVersion`, and `RevisionFileId`.
- **Authorization:**  
  Assigned Reviewer or SectionHead (strictly non-author).

#### FS-1d-007: Author Response & Corrected Word Upload
- **Derived URS:** `DC-URS-1D-009`, `DC-URS-1D-010`
- **Business Behavior:**  
  When a revision is returned for correction, the author submits structured text responses to open findings via `POST /api/document-control/reviews/findings/{id}/respond`. The author then uploads the revised Word document via `POST /api/document-control/revisions/{id}/files`.
- **Preconditions:**  
  1. Finding status is `Open`.  
  2. Calling user is Document Owner or assigned Author.  
  3. Target revision is in `Draft` status (returned for correction).
- **Success Conditions:**  
  1. Finding status updated to `AuthorResponded`, capturing `AuthorResponse` text and timestamp.  
  2. Corrected Word document ingested as `v2` (or subsequent version), deactivating `v1`.  
  3. Historical `v1` remains preserved on disk and database.
- **Failure Conditions:**  
  1. Non-author responding: HTTP 403 Forbidden.  
  2. Finding already resolved: HTTP 422 Unprocessable Entity.
- **Audit Expectation:**  
  `ReviewFindingAuthorResponded` logged; `RevisionFileReplaced` logged for Word upload.
- **Authorization:**  
  Document Owner or assigned Author.

#### FS-1d-008: Multi-Cycle Review Lifecycle & Decision Engine
- **Derived URS:** `DC-URS-1D-011`, `DC-URS-1D-012`
- **Business Behavior:**  
  Reviewer evaluates document and submits a decision via `POST /api/document-control/reviews/{taskId}/decision`:
  - `ReturnForCorrection`: Review task status becomes `ReturnedForCorrection`. Revision status reverts to `Draft`. Existing findings remain active and persistent.
  - `CompleteReview`: Enforces the Mandatory Findings Gate (FS-1d-010). If all mandatory findings are resolved, review task status becomes `Completed`, and revision status advances to `AwaitingApproval`.
- **Preconditions:**  
  1. Calling user is assigned reviewer or SectionHead (non-author).  
  2. Review task status is `Pending` or `InProgress`.  
  3. Mandatory justification notes provided ($\ge 10$ characters).
- **Success Conditions:**  
  1. Status transition applied.  
  2. If `CompleteReview`: revision candidate approved source pinned to the active Word file.
- **Failure Conditions:**  
  1. `CompleteReview` attempted with open mandatory findings: HTTP 422 Unprocessable Entity.  
  2. Author attempting decision: HTTP 403 Forbidden.
- **Audit Expectation:**  
  `TechnicalReviewReturned` or `TechnicalReviewCompleted` logged with cycle details.
- **Authorization:**  
  Assigned Reviewer or SectionHead.

#### FS-1d-009: Multi-Cycle Re-Review Task Continuation & Finding Inheritance
- **Derived URS:** `DC-URS-1D-011`, `DC-URS-1D-012`
- **Business Behavior:**  
  When an author resubmits a revision after correction, the system initiates the next review cycle (`Cycle 2`). The system preserves all existing findings, linking them forward into the active review workspace so the reviewer can verify that each prior finding was resolved in the new Word document version.
- **Preconditions:**  
  1. Revision in `Draft` status with prior completed/returned review task.  
  2. Active Word source file exists with `FileVersion \ge 2`.
- **Success Conditions:**  
  1. Review task instantiated or reopened with `ReviewCycleNumber = previousCycle + 1`.  
  2. All findings from prior cycles are queryable and linked to the active review context.  
  3. Revision status transitions to `InReview`.
- **Failure Conditions:**  
  No active Word file: HTTP 422 Unprocessable Entity.
- **Audit Expectation:**  
  `RevisionSubmittedForReview` logged with `ReviewCycleNumber`.
- **Authorization:**  
  Document Owner, assigned Author, SectionHead, or SystemAdministrator.

#### FS-1d-010: Mandatory Findings Gate Enforcement Across All Cycles
- **Derived URS:** `DC-URS-1D-013`
- **Business Behavior:**  
  Prior to committing `CompleteReview`, the system executes a comprehensive database query across all findings belonging to the document revision:
  $$\text{OpenMandatoryCount} = \sum_{\text{all tasks on revision}} \left[ f.\text{IsMandatory} == \text{true} \;\wedge\; f.\text{Status} \ne \text{Resolved} \right]$$
  If $\text{OpenMandatoryCount} > 0$, review completion is rejected.
- **Preconditions:**  
  Review completion request received.
- **Success Conditions:**  
  $\text{OpenMandatoryCount} == 0$; review completes successfully.
- **Failure Conditions:**  
  $\text{OpenMandatoryCount} > 0$: HTTP 422 Unprocessable Entity (*"Cannot complete technical review while X mandatory review finding(s) remain unresolved."*).
- **Audit Expectation:**  
  Blocked attempt logged in audit trail if execution was forced.
- **Authorization:**  
  Enforced at server service boundary.

#### FS-1d-011: Approval Dossier Presentation of Final Word Source
- **Derived URS:** `DC-URS-1D-015`, `DC-URS-1D-016`
- **Business Behavior:**  
  Approver inspects the approval dossier via `GET /api/document-control/approvals/{taskId}/dossier`. The dossier presents the candidate approved Word source metadata: filename, `FileVersion`, upload timestamp, uploader name, SHA-256 checksum, along with structured change items, impact assessment, and the complete multi-cycle review finding history. The approver can download the Word source file for review.
- **Preconditions:**  
  Revision in `AwaitingApproval` status. Calling user is assigned approver, SectionHead, or Admin.
- **Success Conditions:**  
  Returns `ApprovalDossierDto` with populated `ApprovedWordSource` object and readiness evaluation.
- **Failure Conditions:**  
  Task not found: HTTP 404. Caller unauthorized: HTTP 403.
- **Audit Expectation:**  
  `ApprovalDossierViewed` logged with `ApprovalTaskId` and `RevisionId`.
- **Authorization:**  
  Assigned Approver, SectionHead, SystemAdministrator.

#### FS-1d-012: 21 CFR Part 11 Electronic Signature & Atomic Approval Ceremony
- **Derived URS:** `DC-URS-1D-017`, `DC-URS-1D-021`, `DC-URS-1D-024`
- **Business Behavior:**  
  Approver submits approval decision via `POST /api/document-control/approvals/{taskId}/decision` with `Decision = Approve` and account password. The system verifies password via BCrypt against the authenticated user account, validates readiness, signs the revision, and immediately transitions to post-approval PDF conversion (FS-1d-013) within a single atomic database execution boundary.
- **Preconditions:**  
  1. Calling user is assigned approver or SectionHead.  
  2. Calling user is NOT author and NOT reviewer (strict SoD).  
  3. Password re-authentication succeeds.  
  4. Approval readiness passes (all mandatory findings resolved, impact assessment complete, active Word source present).
- **Success Conditions:**  
  1. `ElectronicSignature` record created with `MeaningOfSignature = Approved`.  
  2. Revision advances to `FutureEffective` or `Effective`.  
  3. Candidate Word file pinned: `IsApprovedFinalSource = true`.  
  4. PDF generation pipeline executed and committed atomically.
- **Failure Conditions:**  
  1. Invalid password: HTTP 401 Unauthorized with security audit event `ElectronicSignatureFailed`.  
  2. SoD violation: HTTP 403 Forbidden.  
  3. PDF conversion failure: Transaction rolls back completely (FS-1d-015).
- **Audit Expectation:**  
  `ElectronicSignatureApplied` and `DocumentRevisionApproved` logged.
- **Authorization:**  
  Assigned Approver or SectionHead (strictly non-author and non-reviewer).

#### FS-1d-013: Server-Side Gotenberg Document Conversion
- **Derived URS:** `DC-URS-1D-018`
- **Business Behavior:**  
  `IDocumentConversionService` takes the raw bytes of the approved Word source file (.docx) and sends an HTTP POST request to the isolated Gotenberg conversion service (`http://gotenberg:3000/forms/libreoffice/convert`). The service executes headless LibreOffice conversion and returns the generated PDF bytes.
- **Preconditions:**  
  Valid Word bytes provided; Gotenberg container healthy and responding.
- **Success Conditions:**  
  Returns valid PDF byte array (magic bytes `%PDF-`).
- **Failure Conditions:**  
  1. Gotenberg connection timeout ($>60\text{ seconds}$): Throws `InvalidOperationException`.  
  2. Gotenberg non-200 response: Throws `InvalidOperationException` with conversion error details.  
  3. Triggers atomic approval rollback (FS-1d-015).
- **Audit Expectation:**  
  Conversion duration and diagnostic status recorded.
- **Authorization:**  
  Internal infrastructure service invocation only.

#### FS-1d-014: Post-Approval Controlled PDF Registration & Cryptographic Binding
- **Derived URS:** `DC-URS-1D-019`, `DC-URS-1D-020`
- **Business Behavior:**  
  Immediately following successful PDF conversion in the approval transaction, the system computes the SHA-256 checksum of the generated PDF bytes, writes the file to physical storage at `documents/{revisionId}/{fileId}_controlledpdf.pdf`, and creates a new `RevisionFile` with `FileRole = ControlledPdf`, `IsActive = true`, and `GeneratedFromSourceFileId` set to the approved Word source file ID.
- **Preconditions:**  
  Valid PDF bytes generated by conversion service.
- **Success Conditions:**  
  1. `RevisionFile` (`ControlledPdf`) inserted and linked.  
  2. Revision updated with `ControlledPdfFileId`.  
  3. Approval task updated with `GeneratedControlledPdfId`.  
  4. Both files permanently bound.
- **Failure Conditions:**  
  Disk write failure or hashing error: Triggers rollback of entire approval transaction.
- **Audit Expectation:**  
  `ControlledPdfGenerated` logged with `SourceFileId`, `SourceSha256`, `PdfFileId`, `PdfSha256`.  
  `ControlledPdfRegistered` logged with `StorageKey`, `ContentSha256`, and `SizeBytes`.
- **Authorization:**  
  Internal execution within approval transaction.

#### FS-1d-015: Atomic Approval Rollback & Error Dispatch
- **Derived URS:** `DC-URS-1D-021`
- **Business Behavior:**  
  If any exception occurs during DOCX-to-PDF conversion, PDF hashing, or storage registration:
  1. The EF Core execution strategy aborts the database transaction.  
  2. The `ElectronicSignature` record is NOT committed.  
  3. The revision status remains `AwaitingApproval`.  
  4. The approval task remains `Pending`.  
  5. The approver receives HTTP 500 Internal Server Error (*"Approval ceremony failed: Controlled PDF generation could not be completed. The document revision has not been approved."*).  
  6. A critical system audit event `ApprovalGenerationRolledBack` is recorded.
- **Preconditions:**  
  Failure during conversion or registration step of approval.
- **Success Conditions:**  
  Database and file storage remain in the pre-approval state; zero orphaned approvals exist.
- **Failure Conditions:**  
  N/A (Rollback handler guaranteed by relational transaction boundary).
- **Audit Expectation:**  
  `ApprovalGenerationRolledBack` logged with error reason and user ID.
- **Authorization:**  
  System execution boundary.

#### FS-1d-016: Downstream Controlled Distribution & Training Hand-off
- **Derived URS:** `DC-URS-1D-020`, `DC-URS-1D-022`
- **Business Behavior:**  
  When an approved revision reaches its `EffectiveDate`:
  1. The existing `DocumentEffectiveDateWorker` transitions status to `Effective` and supersedes prior revisions.  
  2. `TrainingAssignmentService` generates reading assignments referencing the `ControlledPdfFileId`.  
  3. When users open the document in `DocumentLibraryPage` or `MyReadingListPage`, the system delivers strictly the `ControlledPdf`.  
  4. Trainee electronic acknowledgement binds to the Controlled PDF SHA-256 hash.
- **Preconditions:**  
  Revision approved under Release 1d with active `ControlledPdf`.
- **Success Conditions:**  
  Controlled distribution and training executed strictly using the generated Controlled PDF.
- **Failure Conditions:**  
  Missing PDF triggers system process alert.
- **Audit Expectation:**  
  Existing Release 1c audit events: `RevisionAutomaticallyActivated`, `DocumentReadAndUnderstoodAcknowledged`.
- **Authorization:**  
  Active users authorized for reading/viewing.

---

### 3. Summary of Delta Functional Requirements Count

| FRS Identifier Range | Functional Scope Area | Functional Requirement Count |
| :--- | :--- | :---: |
| `FS-1d-001` .. `FS-1d-003` | Authoring, Word Ingestion & Review Submission Gating | 3 |
| `FS-1d-004` .. `FS-1d-007` | Reviewer Access, Finding Lineage & Author Correction | 4 |
| `FS-1d-008` .. `FS-1d-010` | Multi-Cycle Review Continuity & Mandatory Gate Enforcement | 3 |
| `FS-1d-011` .. `FS-1d-012` | Approval Dossier & Atomic Part 11 Electronic Signature | 2 |
| `FS-1d-013` .. `FS-1d-015` | Gotenberg Conversion, PDF Registration & Rollback Handler | 3 |
| `FS-1d-016` | Downstream Controlled Distribution & Training Hand-off | 1 |
| **TOTAL RELEASE 1d DELTA FRS SPECIFICATIONS** | **Comprehensive Functional Scope** | **16** |
