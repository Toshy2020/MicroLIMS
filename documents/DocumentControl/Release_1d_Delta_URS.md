# Release 1d Delta User Requirements Specification (Delta URS)
## Word-First Controlled Document Lifecycle, Review Continuity & Post-Approval PDF Release

- **Document ID:** `ML-DC-URS-1D-001`
- **Version:** 1.0 (Formal Delta Baseline)
- **Status:** **APPROVED FOR PLANNING & ARCHITECTURE**
- **Authoritative Baseline Extended:** MicroLIMS Document Control URS v1.1 (`DC-URS-001`..`DC-URS-200`)
- **Governing Change Control:** `CC-DC-R1D-001`
- **Date:** September 7, 2026

---

### 1. Scope, Baseline Invariant & Supersession Statement

> [!IMPORTANT]
> **Baseline Invariant Statement:**  
> This **Release 1d Delta URS** supplements and supersedes **only** the affected portions of the previous requirements baseline (`Release 1c`). Historical qualification records for Release 1a, 1b, and 1c remain authoritative for their respective baselines and shall not be retroactively renumbered or modified.

Requirements defined herein carry the prefix **`DC-URS-1D-xxx`**. Where a Release 1d requirement modifies or replaces an earlier requirement, the historical requirement ID is explicitly cross-referenced with the exact legal and functional nature of the supersession.

---

### 2. Detailed Delta Requirements Specification (Letters A–Z)

#### Section A: Authoring & Working Document Foundation
- **DC-URS-1D-001: Word Source as Sole Working Document**
  - **Category:** Core Authoring / File Management
  - **Priority:** Mandatory (High)
  - **Requirement:** The system shall enforce that the editable Word document (.docx/.doc) is the mandatory working source file for any document revision throughout the `Draft`, `InReview`, and `AwaitingApproval` workflow states. The system shall strictly prohibit requiring a Controlled PDF file during these authoring and review phases.
  - **Rationale:** Technical authoring and collaborative review occur in native word-processing tools. Off-system conversion to PDF prior to review completion creates unverified, premature document artifacts.
  - **Verification Method:** Automated Unit Test / Integration Test (`SubmitForReview_SucceedsWithOnlyWordSource_ThrowsWithoutWordSource`).

- **DC-URS-1D-002: DOC/DOCX Format Validation & Extension Enforcement**
  - **Category:** Data Ingestion & Security
  - **Priority:** Mandatory (High)
  - **Requirement:** The system shall accept source document uploads strictly in OOXML Word (.docx) or legacy Word (.doc) format, validating file extension, declared MIME type (`application/vnd.openxmlformats-officedocument.wordprocessingml.document`, `application/msword`), and non-empty file payload within the 50 MB threshold.
  - **Rationale:** Prevents ingestion of unsupported or potentially malicious binary payloads into the GxP source repository (ALCOA+ Accuracy / System Integrity).
  - **Verification Method:** Automated Negative Test (`UploadSourceFile_RejectsNonWordExtensionsAndMimeTypes`).

#### Section B: Source File Immutability, Lineage & Sequential Versioning
- **DC-URS-1D-003: Immutable Historical Word File Storage**
  - **Category:** Data Integrity / Audit Trail
  - **Priority:** Mandatory (High)
  - **Requirement:** The system shall store every uploaded Word document file in append-only storage under an immutable, unique physical storage key. No uploaded Word file shall ever be modified, overwritten, or physically deleted from disk or database records.
  - **Rationale:** Satisfies 21 CFR § 11.10(e) and GAMP 5 §D4 data integrity principles by preserving the complete physical history of all authoring drafts.
  - **Verification Method:** Storage Service Unit Test / Database Foreign Key Restrict Verification.

- **DC-URS-1D-004: Explicit Sequential File Version Numbering**
  - **Category:** Traceability / Data Model
  - **Priority:** Mandatory (High)
  - **Requirement:** The system shall automatically compute and assign an explicit sequential integer version number (`FileVersion`: 1, 2, 3...) to each Word source file uploaded to a given document revision. The initial draft upload shall be version 1 (`v1`); each subsequent correction upload within the same revision shall increment the version by exactly 1 (`v2`, `v3`).
  - **Rationale:** Ambiguous timestamps or boolean flags are insufficient for regulatory inspection. An auditor must be able to cite exact file versions (e.g. "Comment raised against Word v1; resolved in Word v2").
  - **Verification Method:** Automated Integration Test (`SequentialFileVersion_IncrementsMonotonicallyPerRevision`).

- **DC-URS-1D-005: Source-File Lineage & Supersession Chaining**
  - **Category:** Traceability / Data Model
  - **Priority:** Mandatory (Medium)
  - **Requirement:** When a new Word version is uploaded to a revision, the system shall atomically mark the previously active Word file as inactive (`IsActive = false`), set its `SupersededByFileId` pointer to the newly created file ID, mark the new file as active (`IsActive = true`), and preserve the full bidirectional chain.
  - **Rationale:** Ensures unambiguous identification of the currently active source file while enabling traversal backward through the entire lineage.
  - **Verification Method:** Database Schema Verification / Automated Service Test.

#### Section C: Reviewer Access & Permissions Boundary
- **DC-URS-1D-006: Reviewer Access to Assigned Word Source Document**
  - **Supersedes / Modifies:** `DC-URS-024`
  - **Category:** Security / Authorization
  - **Priority:** Mandatory (High)
  - **Requirement:** The system shall grant authenticated read-only access to download the active Word source document (.docx/.doc) to the designated technical reviewer assigned to the active `DocumentReviewTask` for that revision.
  - **Release 1c vs 1d Delta Note:** `DC-URS-024` originally restricted source file access strictly to Document Owner, Author, and Document Controller. `DC-URS-1D-006` formally supersedes `DC-URS-024` by expanding read-only access to the assigned technical reviewer during the active review lifecycle.
  - **Rationale:** Technical reviewers cannot evaluate scientific methodology, table formulas, or procedural phrasing without direct access to the working Word source document.
  - **Verification Method:** Authorization Integration Test (`AssignedReviewer_CanDownloadWordSource_NonAssignedUserForbidden`).

- **DC-URS-1D-007: Strict Read-Only Reviewer Source Access (No Reviewer Ingestion)**
  - **Category:** Segregation of Duties / Data Integrity
  - **Priority:** Mandatory (High)
  - **Requirement:** The system shall strictly prevent a technical reviewer from uploading, replacing, or editing the author's Word document as if they were the author. Only the designated Document Owner or assigned Author shall have authorization to upload or replace Word files.
  - **Rationale:** Preserves Author ≠ Reviewer boundaries. Reviewers must request changes via structured findings; they must never unilaterally modify the author's working file.
  - **Verification Method:** Security Negative Test (`AssignedReviewer_AttemptingUploadOrReplaceFile_ThrowsUnauthorized`).

#### Section D: Review Findings & Multi-Cycle Continuity
- **DC-URS-1D-008: Review Findings Linked to Exact Word File Version**
  - **Category:** Review Management / Traceability
  - **Priority:** Mandatory (High)
  - **Requirement:** The system shall record the exact Word source file ID (`RevisionFileId`) and sequential version number (`SourceFileVersion`) against which each technical review finding is created, along with page number, section number, comment text, and mandatory status.
  - **Rationale:** Review comments must be permanently contextualized to the exact document version inspected by the reviewer.
  - **Verification Method:** Automated Integration Test (`ReviewFinding_PreservesSourceFileVersionSnapshot`).

- **DC-URS-1D-009: Author Response Workflow on Returned Revision**
  - **Category:** Review Management / User Interface
  - **Priority:** Mandatory (High)
  - **Requirement:** When a revision is returned for correction, the system shall provide the author with full interactive access to view all open review findings and record structured text responses against each finding.
  - **Rationale:** Remediates Release 1c defect where author response controls were hidden when the review task was in status `ReturnedForCorrection`.
  - **Verification Method:** Frontend UI Test / Integration Test (`Author_CanRespondToFindings_WhenRevisionReturnedForCorrection`).

- **DC-URS-1D-010: Author Correction Word Upload Workflow**
  - **Category:** Authoring / Review Workflow
  - **Priority:** Mandatory (High)
  - **Requirement:** After editing the Word document offline to address review findings, the author shall be permitted to upload the corrected Word document to the revision. The system shall ingest this file as a new sequential version (`v2`, `v3`) without altering or overwriting previous versions.
  - **Rationale:** Establishes the authoritative revised draft that will be presented to the reviewer for the next review cycle.
  - **Verification Method:** Automated Integration Test (`AuthorCorrection_UploadsNewWordVersion_PreservesHistoricalFiles`).

- **DC-URS-1D-011: Multi-Cycle Re-Review Continuity & Finding Persistence**
  - **Category:** Review Management / State Machine
  - **Priority:** Mandatory (High)
  - **Requirement:** The system shall support multiple correction and re-review cycles (`Cycle 1`, `Cycle 2`, etc.) on a single revision. All review findings raised in earlier cycles shall persist across cycles and remain visible, traceable, and verifiable in all subsequent review cycles.
  - **Rationale:** Prevents review findings from being lost or orphaned when a revision undergoes iterative author corrections.
  - **Verification Method:** Multi-Cycle End-to-End Integration Test (`MultiCycleReview_CarriesForwardAllFindings`).

- **DC-URS-1D-012: Reviewer Verification of Author Corrections**
  - **Category:** Review Management / Quality Gate
  - **Priority:** Mandatory (High)
  - **Requirement:** In re-review cycles, the system shall allow the reviewer to inspect the newly uploaded Word version, verify the author's responses and document changes against previous findings, add verification notes, and mark findings as `ReviewerVerified` or `Resolved`.
  - **Rationale:** In cGMP workflows, only the reviewer who raised a mandatory concern (or an authorized Document Controller) may verify and resolve that concern.
  - **Verification Method:** Automated Review Workflow Test (`Reviewer_CanVerifyAndResolveFindingInCycle2`).

- **DC-URS-1D-013: Mandatory Finding Completion Gate Across All Cycles**
  - **Supersedes / Extends:** `DC-URS-071`
  - **Category:** Quality Gate / Review Enforcement
  - **Priority:** Mandatory (Critical)
  - **Requirement:** The system shall strictly block technical review completion and prevent promotion of the revision to `AwaitingApproval` if ANY mandatory finding (`IsMandatory == true`) across ANY review cycle of the revision remains in a status other than `Resolved`.
  - **Release 1c vs 1d Delta Note:** `DC-URS-071` originally enforced mandatory finding checks only on the single active review task. `DC-URS-1D-013` extends this gate across the complete multi-cycle history of the revision.
  - **Rationale:** Zero critical quality findings may be bypassed or ignored before a document advances to formal approval.
  - **Verification Method:** Automated Negative Gate Test (`CompleteReview_ThrowsIfAnyMandatoryFindingUnresolvedAcrossCycles`).

- **DC-URS-1D-014: Side-by-Side Word Inspection & Optional Draft Preview**
  - **Supersedes / Modifies:** `DC-URS-067`
  - **Category:** User Workspace / Inspection
  - **Priority:** Highly Desirable (Medium)
  - **Requirement:** The system shall provide reviewers and approvers with direct download links to the authoritative Word source file for offline comparison. If an in-browser PDF preview of the draft Word source is rendered for inspection, it shall display an indelible watermark: **"DRAFT — NOT CONTROLLED"** and shall strictly be treated as an ephemeral view, never as a controlled document.
  - **Release 1c vs 1d Delta Note:** `DC-URS-067` originally specified side-by-side comparison of pre-existing controlled PDFs. `DC-URS-1D-014` modifies this to reflect that the proposed revision working source is Word, and any rendered preview is strictly non-controlled draft evidence.
  - **Rationale:** Prevents confusion between working draft previews and authorized controlled release documents.
  - **Verification Method:** Visual Inspection Test / PDF Preview Header/Watermark Verification.

#### Section E: Approval & Atomic Controlled Release
- **DC-URS-1D-015: Final Approved Word Source Designation**
  - **Category:** Approval / Traceability
  - **Priority:** Mandatory (High)
  - **Requirement:** Upon successful completion of technical review, the currently active Word source file version shall be designated as the candidate approved source. Upon approval, the system shall immutably flag this exact file (`IsApprovedFinalSource = true`) and record its ID (`ApprovedSourceFileId`) on the `DocumentRevision` and `DocumentApprovalTask`.
  - **Rationale:** Eliminates ambiguity regarding which specific Word draft received regulatory approval.
  - **Verification Method:** Automated Approval State Test (`Approval_PermanentlyPinsApprovedSourceFileId`).

- **DC-URS-1D-016: Approval Dossier Inspection of Word Source & Findings History**
  - **Category:** Approval / User Interface
  - **Priority:** Mandatory (High)
  - **Requirement:** The Approval Workspace shall present the approver with a complete inspection dossier containing: document metadata, change rationale, impact assessment, complete multi-cycle technical review history with all resolved findings, and the candidate approved Word source file metadata (filename, version number, upload timestamp, and SHA-256 hash) with download access for verification.
  - **Rationale:** Approvers must have full visibility into the review trail and the exact source text prior to executing their 21 CFR Part 11 electronic signature.
  - **Verification Method:** Frontend UI Test / Dossier API Response Contract Test.

- **DC-URS-1D-017: Controlled PDF Generation Gated Strictly Post-Approval**
  - **Category:** Controlled Document Lifecycle
  - **Priority:** Mandatory (Critical)
  - **Requirement:** Generation of the Controlled PDF shall execute ONLY AFTER the designated approver has successfully applied a valid 21 CFR Part 11 electronic signature (with BCrypt password re-authentication). The system shall strictly prohibit generating or designating any document as a Controlled PDF prior to formal approval.
  - **Rationale:** A document cannot be a "controlled release copy" until regulatory approval is legally committed.
  - **Verification Method:** Workflow State Machine Test (`ControlledPdf_DoesNotExistPriorToApproval_GeneratedImmediatelyAfter`).

- **DC-URS-1D-018: Server-Side DOCX-to-PDF Conversion Engine**
  - **Category:** Document Conversion / Infrastructure
  - **Priority:** Mandatory (High)
  - **Requirement:** The system shall utilize an isolated, dedicated server-side document conversion engine (Gotenberg / headless LibreOffice) running in a containerized environment to convert the approved Word source document bytes into an ISO 32000 (PDF) compliant document. The conversion shall execute on internal infrastructure without transmitting document content to external third-party cloud services.
  - **Rationale:** Satisfies GxP security, privacy, and system boundaries while ensuring high-fidelity rendering of styles, tables, and pagination.
  - **Verification Method:** Infrastructure Integration Test (`GotenbergConversionService_ConvertsDocxToValidPdf`).

- **DC-URS-1D-019: Cryptographic Hash Calculation & Source-to-PDF Binding**
  - **Category:** Data Integrity / Cryptography
  - **Priority:** Mandatory (Critical)
  - **Requirement:** Immediately upon generation of the Controlled PDF, the system shall calculate its SHA-256 cryptographic checksum. The system shall store the PDF as a new `RevisionFile` (`FileRole.ControlledPdf`), permanently record the `GeneratedFromSourceFileId` referencing the approved Word file, and log a semantic audit event binding the Word SHA-256 hash to the PDF SHA-256 hash.
  - **Rationale:** Proves non-repudiation and cryptographic provenance from approved editable source to final released PDF.
  - **Verification Method:** Automated Cryptographic Verification Test (`PdfSha256_ComputedAndLinkedToSourceWordSha256`).

- **DC-URS-1D-020: Immutable Controlled PDF Protection & Distribution Restriction**
  - **Category:** Security / Document Control
  - **Priority:** Mandatory (High)
  - **Requirement:** The generated Controlled PDF shall be marked active (`IsActive = true`) and protected as strictly immutable. General users (Analysts, Technicians, Viewers) shall have access strictly to the Controlled PDF for viewing, downloading, and training. Direct access to editable Word files shall remain restricted to authorized Document Controllers, Owners, and Administrators.
  - **Rationale:** Complies with ISO 17025 §8.3 and cGMP rules: only authorized controlled copies may be distributed for operational laboratory use.
  - **Verification Method:** Security Permissions Test (`GeneralUser_CanAccessControlledPdf_DeniedAccessToSourceDocx`).

- **DC-URS-1D-021: Atomic Approval Transaction & Conversion Failure Rollback**
  - **Category:** Transaction Safety / GxP Boundary
  - **Priority:** Mandatory (Critical)
  - **Requirement:** The electronic signature ceremony, revision status transition, Word source pinning, and Controlled PDF generation/registration shall execute within a single atomic transactional boundary. If PDF conversion fails, times out, or encounters an integrity fault, the entire approval transaction shall be rolled back, the electronic signature shall not be committed, the revision shall remain in `AwaitingApproval`, and a descriptive error shall be returned to the approver.
  - **Rationale:** Under 21 CFR Part 11, the system must never permit an approved revision to exist in a released status without its immutable, verified controlled document artifact.
  - **Verification Method:** Fault Injection Integration Test (`ApprovalTransaction_RollsBackSignature_WhenPdfConversionFails`).

#### Section F: Downstream Integration & Governance
- **DC-URS-1D-022: Training Matrix & Acknowledgement Integration**
  - **Category:** Integration / Training Cascade
  - **Priority:** Mandatory (High)
  - **Requirement:** When a revision approved under Release 1d reaches its `EffectiveDate`, the existing Release 1c effective-date worker and training cascade engine shall automatically present the post-approval generated Controlled PDF to assigned trainees. Reading acknowledgement records shall capture the Controlled PDF file ID and SHA-256 hash.
  - **Rationale:** Guarantees complete end-to-end alignment with qualified Release 1c training matrix infrastructure without code redesign.
  - **Verification Method:** Integration Verification Test (`EffectiveDateCascade_UsesRelease1dGeneratedControlledPdf`).

- **DC-URS-1D-023: Semantic Audit Trail for Full 1d Lifecycle**
  - **Category:** Audit Trail / Compliance
  - **Priority:** Mandatory (High)
  - **Requirement:** The system shall record detailed semantic audit trail events via `IAuditEventService` for: Word upload, Word version replacement, review submission, review finding creation, author response, reviewer verification, finding resolution, review return for correction, review completion, approval dossier inspection, electronic signature application, approval decision, Controlled PDF generation, and Controlled PDF registration.
  - **Rationale:** 21 CFR § 11.10(e) requires a complete, contemporaneous record of all operator actions affecting document status and file contents.
  - **Verification Method:** Automated Audit Verification Test (`AuditTrail_CapturesAllRelease1dLifecycleEvents`).

- **DC-URS-1D-024: Non-Exempt Segregation of Duties Enforcement**
  - **Category:** Security / Segregation of Duties
  - **Priority:** Mandatory (Critical)
  - **Requirement:** The system shall enforce that:
    1. Author ≠ Reviewer
    2. Author ≠ Approver
    3. Reviewer ≠ Approver
    System Administrator credentials shall be strictly subject to these rules with zero bypass. No administrative account shall execute business authoring, review, or approval actions while masquerading as another user or bypassing role assignment.
  - **Rationale:** Prevents regulatory integrity violations and upholds cGMP multi-person review independence.
  - **Verification Method:** Automated Negative Security Test Suite (`SoD_StrictlyEnforced_ForStandardAndAdminUsers`).

- **DC-URS-1D-025: Authenticated Session Requirement for Test Protocols**
  - **Category:** Validation Governance / Testing Protocol
  - **Priority:** Mandatory (Critical)
  - **Requirement:** All automated and manual qualification protocols (OQ and UAT) for Release 1d shall execute using genuine, independently authenticated credentials for Author, Reviewer, Approver, and Analyst roles. Qualification tests shall strictly prohibit using administrative tokens or database password resets to simulate business workflow execution.
  - **Rationale:** Remediates previous testing findings and proves true operational segregation in the qualified environment.
  - **Verification Method:** Qualification Protocol Review / Evidence Log Audit.

---

### 3. Summary of Delta Requirements Count

| Requirement Identifier Range | Section Focus | Requirement Count |
| :--- | :--- | :---: |
| `DC-URS-1D-001` .. `DC-URS-002` | Authoring & Working Document Foundation | 2 |
| `DC-URS-1D-003` .. `DC-URS-005` | Immutability, Lineage & Sequential Versioning | 3 |
| `DC-URS-1D-006` .. `DC-URS-007` | Reviewer Access & Permissions Boundary | 2 |
| `DC-URS-1D-008` .. `DC-URS-014` | Review Findings & Multi-Cycle Continuity | 7 |
| `DC-URS-1D-015` .. `DC-URS-021` | Approval & Atomic Controlled Release | 7 |
| `DC-URS-1D-022` .. `DC-URS-025` | Downstream Integration & Governance | 4 |
| **TOTAL RELEASE 1d DELTA REQUIREMENTS** | **Complete Regulated Enhancement Scope** | **25** |
