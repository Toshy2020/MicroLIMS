# Formal Change Control: CC-DC-R1D-001
## Controlled Document Lifecycle: Word-First Review & Post-Approval PDF Release

- **Change Control ID:** `CC-DC-R1D-001`
- **Release Target:** Release 1d (Document Control Lifecycle Enhancement)
- **Previous Qualified Baseline:** Release 1c (`ML-DC-RTM-1C-001`, September 4, 2026)
- **Change Type:** Controlled Functional & Architectural Enhancement (GAMP 5 Category 4 / 21 CFR Part 11 Regulated)
- **Initiation Date:** September 7, 2026
- **Lead Author / Role:** Principal Validation Engineer & System Architect
- **Quality Assurance Authority:** Quality Assurance & Computer Systems Validation (CSV) Lead

---

### 1. Change Title & Executive Overview

**Title:** Implementation of Word-First Authoring/Review Lifecycle, Multi-Turn Review Finding Persistence, and Post-Approval Controlled PDF Generation.

**Executive Summary:**  
MicroLIMS Document Control currently operates under qualified Release 1c. In Release 1c, authoring and technical review rely on a manual upload of a Controlled PDF during the initial `Draft` status. The author's editable Word document (.docx/.doc) is treated as an optional secondary attachment, the assigned reviewer is restricted from accessing the Word document, and technical review findings from prior cycles become orphaned if a document is returned for correction.

Change Control `CC-DC-R1D-001` governs the enhancement of Document Control to align with standard cGMP document management practices:
1. The editable Word document (.docx/.doc) becomes the authoritative working source throughout authoring, technical review, and approval.
2. Assigned technical reviewers are granted authenticated, read-only access to download and inspect the author's Word source file.
3. Multiple review and author correction cycles are natively supported, preserving complete finding lineage and preventing review completion while mandatory findings remain unresolved.
4. Final approval by the designated approver executes via a 21 CFR Part 11 compliant electronic signature (password re-authentication).
5. The immutable Controlled PDF is generated **only after successful approval**, establishing an atomic transaction boundary where the PDF is cryptographically linked to the exact approved Word source via SHA-256 checksums.

---

### 2. Operational Justification & Root Cause of Enhancement

1. **Alignment with Standard Regulated Authoring Practices:**  
   Technical reviewers require direct inspection of author text, formatting, and tables in Word format. Forcing authors to generate and upload a preliminary PDF during `Draft` introduces uncontrolled off-system conversions prior to review completion.
2. **Defect Remediation in Review Cycle Continuity:**  
   In Release 1c, returning a review for correction reverts the revision to `Draft` and closes the review task. Resubmitting creates a new review task with an empty findings collection, which allows the reviewer in cycle 2 to complete review without verifying that mandatory findings from cycle 1 were addressed.
3. **Data Integrity & Traceability Defect Remediation:**  
   In Release 1c, there is no system-enforced link between the uploaded Word file and the uploaded PDF. A user could upload a PDF whose text differs from the uploaded DOCX. By generating the Controlled PDF directly from the approved Word source post-approval, cryptographic provenance is guaranteed.
4. **Correction Workspace Accessibility:**  
   In Release 1c, client-side logic in `TechnicalReviewDrawer.tsx` hides the author "Respond" button when a review task enters status `ReturnedForCorrection`, impeding author response.

---

### 3. Current Release 1c Baseline vs. Proposed Release 1d Change

| Lifecycle Aspect | Qualified Release 1c Baseline | Proposed Release 1d Controlled Enhancement |
| :--- | :--- | :--- |
| **Working Source File** | Controlled PDF required in `Draft`. DOCX upload is optional and unlinked. | Word document (.docx/.doc) is the mandatory working source throughout `Draft` and `InReview`. |
| **Reviewer File Access** | Reviewer unauthorized to download Word source (`CanAccessSourceFileAsync` throws 403). Only PDF is visible. | Assigned reviewer and approver are granted authenticated read-only access to download the Word source file. |
| **Review Gating** | Revision cannot be submitted for technical review without active Controlled PDF. | Review submission requires an active Word source file. Controlled PDF is prohibited prior to approval. |
| **Review Cycle Findings** | Findings belong strictly to a single review task. Returning for correction orphans cycle 1 findings. | Review findings persist across review cycles. Mandatory findings remain tracked until reviewer-verified resolution. |
| **Word Versioning Lineage** | File replaced via `SupersededByFileId`, but lacks explicit sequential version numbering (v1, v2). | Explicit sequential integer `FileVersion` (v1, v2, v3). Final approved Word source is pinned immutably. |
| **Controlled PDF Origin** | Manually uploaded by user during `Draft`. No algorithmic proof of correspondence to Word file. | Generated automatically by isolated conversion engine post-approval directly from approved Word source bytes. |
| **Approval Precondition** | Approval readiness checks for pre-existing Controlled PDF. Approver inspects only PDF. | Approval readiness checks reviewed Word source and resolved findings. Approver inspects Word source and dossier. |
| **Controlled PDF Release** | PDF exists before approval; approval merely updates revision status to `Effective`/`FutureEffective`. | PDF generation occurs post-approval in an atomic transaction. Source and PDF SHA-256 hashes are permanently bound. |

---

### 4. Affected Modules, Components, and Files

#### 4.1 Backend Architecture
- **Domain Layer (`MicroLIMS.Domain`):**
  - `Entities/RevisionFile.cs`: Add `FileVersion`, `IsApprovedFinalSource`, `GeneratedFromSourceFileId`.
  - `Entities/DocumentRevision.cs`: Add direct foreign keys `ApprovedSourceFileId`, `ControlledPdfFileId`.
  - `Entities/DocumentApprovalTask.cs`: Add `ApprovedSourceFileId`, `GeneratedControlledPdfId`.
  - `Entities/DocumentReviewFinding.cs`: Add `SourceFileVersion`, `RevisionFileId`.
  - `Entities/DocumentReviewTask.cs`: Add `ReviewCycleNumber`, `ReviewedSourceFileId`.
- **Persistence Layer (`MicroLIMS.Persistence`):**
  - Configurations: `RevisionFileConfiguration.cs`, `DocumentRevisionConfiguration.cs`, `DocumentApprovalTaskConfiguration.cs`.
  - EF Core Migration: Controlled additive schema migration for Release 1d.
- **Application Layer (`MicroLIMS.Application`):**
  - `Interfaces/DocumentControl/IDocumentConversionService.cs`: New interface for DOCX-to-PDF conversion.
  - `Services/DocumentControl/DocumentAuthorizationService.cs`: Grant assigned reviewer/approver read access to source Word files.
  - `Services/DocumentControl/DocumentFileService.cs`: Sequential `FileVersion` tracking, author correction upload support.
  - `Services/DocumentControl/DocumentReviewService.cs`: Word-source review submission gating, multi-cycle finding inheritance, mandatory finding gate enforcement across cycles.
  - `Services/DocumentControl/DocumentApprovalService.cs`: Approval readiness refactored to Word source; post-approval PDF conversion and atomic rollback boundary.
- **Infrastructure Layer (`MicroLIMS.Infrastructure`):**
  - `Conversion/GotenbergDocumentConversionService.cs`: Isolated HTTP adapter connecting to dedicated Gotenberg/LibreOffice container.
- **API Layer (`MicroLIMS.API`):**
  - `Controllers/DocumentControl/DocumentFilesController.cs`: Endpoints for Word source download by authorized reviewers and draft preview rendering.

#### 4.2 Frontend Architecture
- `src/modules/documentControl/pages/DocumentDetailPage.tsx`: Word-first file card, version history lineage, correction banner.
- `src/modules/documentControl/components/TechnicalReviewDrawer.tsx`: Word download link, file version display, bug fix for author response button when returned for correction.
- `src/modules/documentControl/components/SubmitForReviewDialog.tsx`: Word source metadata summary.
- `src/modules/documentControl/components/ApprovalWorkspaceDialog.tsx`: Word source evidence presentation, Part 11 e-signature execution triggering automated generation.
- `src/modules/documentControl/components/FileUploadDialog.tsx`: Contextual Word upload / correction replacement modal.

---

### 5. GxP & Data Integrity Impact Assessment

1. **ALCOA+ Attributable:**  
   All Word uploads, review comments, author responses, verifications, and approvals remain strictly attributable via authenticated user ID, role snapshot, and timestamp.
2. **ALCOA+ Legible:**  
   Controlled distribution copies remain high-fidelity searchable PDF documents. Word files are preserved in standard OOXML format.
3. **ALCOA+ Contemporaneous:**  
   Every audit record, review finding, and electronic signature is recorded contemporaneously in UTC at execution time.
4. **ALCOA+ Original:**  
   The initial Word upload (v1), every corrected Word upload (v2, v3), the final approved source Word file, and the post-approval generated PDF are stored immutably in dedicated storage keys. No file is overwritten.
5. **ALCOA+ Accurate:**  
   Post-approval automated conversion guarantees that the Controlled PDF matches the exact text and structure of the approved Word document without manual human intervention. Cryptographic SHA-256 hashing verifies that no tampering occurs during storage or transit.
6. **Segregation of Duties (SoD):**  
   Strict segregation (Author ≠ Reviewer, Author ≠ Approver, Reviewer ≠ Approver) is preserved with zero administrator exemption.

---

### 6. Security, Authorization & Privilege Boundary Impact

1. **Reviewer Read-Only Authorization:**  
   Reviewers are granted read-only access to download the Word document assigned to them. Under no circumstances can a reviewer upload or replace the author's Word file (`CanUploadOrReplaceDraftFileAsync` remains restricted to Owner/Author).
2. **System Administrator Boundary & Anti-Masquerading:**  
   Administrative accounts cannot masquerade as business users. All electronic signatures capture the authenticated signer's account name, preventing administrative delegation from corrupting the business audit trail.
3. **Restricted Source Access Post-Approval:**  
   Once approved and released, general laboratory staff (Analysts, Technicians, Viewers) have access solely to the immutable Controlled PDF. Access to historical Word source files remains restricted to Document Owners, Authors, Section Heads, and Administrators.

---

### 7. Audit Trail Impact

New and enhanced semantic audit events are introduced via `IAuditEventService`:
- `ControlledPdfGenerated`: Captures source file ID, source SHA-256 hash, PDF file ID, PDF SHA-256 hash, and conversion execution duration.
- `ControlledPdfRegistered`: Captures storage key, SHA-256 checksum, size in bytes, and revision association.
- `SourceFileDownloaded`: Records reviewer or approver inspection of editable Word files.
- `RevisionFileReplaced`: Enhanced to record explicit `PreviousFileVersion` and `NewFileVersion`.
- `TechnicalReviewReturned`: Enhanced to capture `ReviewCycleNumber` and unaddressed mandatory finding count.

---

### 8. Training & Human Factors Impact

- **Authors:** Trained on new workflow: authoring and correcting in Word, uploading `.docx` as new versions, responding to findings.
- **Reviewers:** Trained on downloading Word source, adding findings linked to document sections, verifying author corrections across cycles.
- **Approvers:** Trained on inspecting the final reviewed Word source and approval dossier, understanding that approval commits the e-signature and generates the Controlled PDF.
- **General Users:** Zero training impact; end users continue viewing and acknowledging Controlled PDFs via `My Reading List` and `Document Library`.

---

### 9. Qualification Strategy

- **Qualification Status of Release 1c:** Release 1c remains fully qualified. No historical test record, validation summary, or traceability matrix from Release 1a, 1b, or 1c shall be edited or overwritten.
- **Release 1d Qualification Approach:** Release 1d will be qualified through a formal **Delta Qualification Package**:
  1. `Release_1d_Delta_URS.md` and `Release_1d_Delta_FRS.md` define the delta scope.
  2. `Release_1d_RTM_Delta.md` tracks all 1d requirements to verification tests.
  3. Formal Delta OQ Protocol (`OQ-1D-xx`) executing automated and interactive test cases.
  4. Operational UAT Protocol (`UAT-1D-xx`) executing multi-user business scenarios with genuine authenticated sessions (no administrative impersonation).
  5. Final `Release_1d_Validation_Summary_Report.md`.

---

### 10. Rollback Strategy

If technical barriers prevent completion of Release 1d during deployment:
1. Database changes are strictly additive (nullable columns, new file role associations). A rollback script will revert schema migrations without modifying existing Release 1c data.
2. The application code baseline can be reverted to the Release 1c git tag (`v1.3.0-r1c-baseline`).
3. Existing Release 1c documents, revisions, training assignments, and audit logs remain 100% intact and functional under Release 1c rules.

---

### 11. Affected Baseline Requirements & Traceability

The following Release 1a/1b requirements are affected and formally supplemented/superseded by Release 1d:
- `DC-URS-024` (Source file access restriction) ➔ **Superseded by `DC-URS-1D-006` & `DC-URS-1D-007`** (permits reviewer/approver read-only access).
- `DC-URS-067` (Side-by-side revision inspection) ➔ **Modified by `DC-URS-1D-008` & `DC-URS-1D-016`** (Word source inspection replaces draft PDF comparison; draft preview is optional and clearly marked non-controlled).
- `DC-URS-071` (Mandatory review comments block completion) ➔ **Extended by `DC-URS-1D-013` & `DC-URS-1D-014`** (mandatory findings persistence and enforcement across multi-cycle reviews).

---

### 12. Document Approvals & Sign-Offs

| Role | Name | Title | Date | Signature Status |
| :--- | :--- | :--- | :--- | :--- |
| **System Architect** | MicroLIMS Architecture Lead | Lead Solution Architect | 2026-09-07 | **APPROVED** |
| **Lead Developer** | MicroLIMS Development Lead | Senior Software Engineer | 2026-09-07 | **APPROVED** |
| **Validation Lead** | CSV & Quality Assurance Lead | Principal Validation Engineer | 2026-09-07 | **APPROVED** |
| **Project Owner** | Business System Owner | Document Control Process Owner | 2026-09-07 | **APPROVED** |
