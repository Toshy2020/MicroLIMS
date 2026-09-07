# Architecture Decision Record: ADR-DC-004
## Word-First Authoring/Review Lifecycle, Multi-Cycle Finding Persistence & Post-Approval Controlled PDF Generation

- **Status:** **APPROVED & ADOPTED FOR RELEASE 1d**
- **Date:** September 7, 2026
- **Deciders:** Lead Solution Architect, CSV Lead, Document Control Process Owner
- **Governing Change Control:** `CC-DC-R1D-001`
- **Technical Context:** MicroLIMS Document Control Architecture (Release 1c Qualified Baseline)

---

### 1. Context & Problem Statement

In the qualified Release 1c baseline, the Document Control module requires the author to upload a Controlled PDF document during the initial `Draft` status before the document can be submitted for technical review (`DocumentReviewService.SubmitForReviewAsync`). The editable Word document (.docx/.doc) is treated as an optional, secondary attachment.

This architectural approach creates four significant regulatory, operational, and data-integrity problems:
1. **Uncontrolled Off-System Conversions:** Authors must convert Word files to PDF on unvalidated local desktop tools before submitting to review. If modifications occur during review, the author must manually re-convert and re-upload both files, with zero algorithmic guarantee that the uploaded PDF matches the uploaded DOCX.
2. **Reviewer Authorization Barrier:** `DocumentAuthorizationService.CanAccessSourceFileAsync` restricts Word source downloads to Document Owners, Authors, and Administrators. Assigned technical reviewers are prevented from opening the author's Word document, forcing them to review the PDF even though their comments pertain to editorial and technical changes in Word.
3. **Loss of Review Findings Across Cycles:** When a reviewer returns a document for correction, the review task is closed. Resubmission generates a new review task with an empty findings collection. Mandatory findings raised in Cycle 1 are orphaned, allowing a reviewer in Cycle 2 to complete review without system-enforced verification of previous findings.
4. **Premature "Controlled" Designation:** A document is labeled as a "Controlled PDF" while still an unapproved draft. Under pharmaceutical cGMP (FDA 21 CFR Part 11, EU Annex 11, ISO 17025 §8.3), a document can only become a "controlled copy" once it has received formal electronic signature approval from authorized personnel.

---

### 2. The Four Approved Architectural Decisions for Release 1d

#### DECISION 1: Isolated Server-Side Document Conversion via Gotenberg / Headless LibreOffice
- **Decision:** Deploy a dedicated, containerized Gotenberg service (based on headless LibreOffice and Chromium) on internal laboratory infrastructure to execute automated DOCX-to-PDF conversion.
- **Rules:**
  - **No Cloud Document Conversion:** The system shall strictly prohibit utilizing public cloud APIs (e.g. Adobe PDF Services, Google Drive API, Microsoft Graph, CloudConvert) to prevent unauthorized transmission of proprietary pharmaceutical data across external network boundaries.
  - **No Unapproved Commercial Libraries:** The system shall not introduce proprietary commercial .NET libraries (e.g. Aspose.Words, Syncfusion) that require external licensing fees and vendor audit qualification.
- **Architectural Implementation:**  
  `MicroLIMS.Infrastructure` implements `IDocumentConversionService` via `GotenbergDocumentConversionService`, communicating with `http://gotenberg:3000/forms/libreoffice/convert` over an isolated Docker network.

#### DECISION 2: Atomic Approval Transaction Boundary on PDF Generation Failure
- **Decision:** The electronic signature ceremony, revision status mutation, Word source pinning, and Controlled PDF generation/registration shall execute within a single atomic transactional boundary.
- **Rules:**
  - If PDF conversion fails, times out, or encounters a hashing mismatch, the database transaction shall be rolled back immediately.
  - The approver's 21 CFR Part 11 electronic signature shall **not** be committed to the audit trail as an approved signature.
  - The revision shall remain in status `AwaitingApproval`, the approval task shall remain `Pending`, and the approver shall receive a clear, actionable failure message.
  - Under no circumstances shall an approved revision exist in the database without its immutable, cryptographically verified Controlled PDF.

#### DECISION 3: Authoritative Word Working Source with Optional "DRAFT — NOT CONTROLLED" Preview
- **Decision:** The Word document (.docx/.doc) is the sole authoritative working source during authoring, review, and approval.
- **Rules:**
  - Reviewers and approvers must have authenticated read-only access to download the exact Word file version under inspection.
  - Reviewers are strictly prohibited from uploading or modifying the author's Word document.
  - An optional in-browser preview may be provided by rendering an ephemeral PDF from the active Word file. Any such preview must display an indelible diagonal watermark: **"DRAFT — NOT CONTROLLED"** and must never be stored as or mistaken for the controlled release document.

#### DECISION 4: Persistent Review History & Finding Lineage Across Multi-Turn Cycles
- **Decision:** The system shall natively support multi-cycle review workflows without orphaning findings.
- **Rules:**
  - When a document is returned for correction, all review findings remain active and bound to the revision.
  - Each finding permanently records the exact Word file version (`SourceFileVersion`) against which it was raised.
  - Resubmission advances the `ReviewCycleNumber` (Cycle 1, Cycle 2...) while carrying forward all unaddressed findings.
  - Technical review completion is strictly blocked across all cycles until every mandatory finding is marked `Resolved` by reviewer verification.

---

### 3. Alternatives Considered & Rejection Rationale

```
┌─────────────────────────────────────────────────────────────────────────────────────────────────┐
│                               ARCHITECTURAL ALTERNATIVES EVALUATION                             │
├───────────────────────┬─────────────────────────────┬───────────────────────────────────────────┤
│ Option Considered     │ Key Characteristics         │ Rejection Rationale                       │
├───────────────────────┼─────────────────────────────┼───────────────────────────────────────────┤
│ 1. Asynchronous PDF   │ E-signature commits first;  │ Rejected: Violates 21 CFR Part 11. If     │
│    Generation via     │ background queue converts   │ conversion fails in background, revision  │
│    Background Worker  │ Word to PDF asynchronously. │ is legally approved but unusable for      │
│                       │                             │ distribution, requiring manual data patch.│
├───────────────────────┼─────────────────────────────┼───────────────────────────────────────────┤
│ 2. Client-Side Word   │ Frontend or desktop tool    │ Rejected: Violates ALCOA+ Data Integrity. │
│    to PDF Conversion  │ converts DOCX to PDF prior  │ Off-system conversion cannot be validated │
│    via Browser WASM   │ to upload.                  │ or audited; font rendering varies by PC.  │
├───────────────────────┼─────────────────────────────┼───────────────────────────────────────────┤
│ 3. Commercial .NET    │ Aspose.Words or Spire.Doc   │ Rejected: High recurring license cost;    │
│    In-Process Library │ embedded into API process.  │ requires commercial vendor GxP audit and  │
│                       │                             │ proprietary closed-source binary testing. │
├───────────────────────┼─────────────────────────────┼───────────────────────────────────────────┤
│ 4. Cloud Conversion   │ Adobe Document Cloud API /  │ Rejected: Violates Pharmaceutical IP &    │
│    REST Endpoints     │ Microsoft Graph API.        │ Data Sovereignty. Sends proprietary SOPs  │
│                       │                             │ across third-party internet endpoints.    │
└───────────────────────┴─────────────────────────────┴───────────────────────────────────────────┘
```

---

### 4. Detailed Component Consequences & Interactions

#### 4.1 Persistence Layer (`MicroLIMS.Persistence`)
- Additive migrations introduce:
  - `RevisionFiles.FileVersion` (integer, default 1)
  - `RevisionFiles.IsApprovedFinalSource` (boolean, default false)
  - `RevisionFiles.GeneratedFromSourceFileId` (nullable FK to `RevisionFiles`)
  - `DocumentRevisions.ApprovedSourceFileId` (nullable FK to `RevisionFiles`)
  - `DocumentRevisions.ControlledPdfFileId` (nullable FK to `RevisionFiles`)
  - `DocumentApprovalTasks.ApprovedSourceFileId` (nullable FK to `RevisionFiles`)
  - `DocumentApprovalTasks.GeneratedControlledPdfId` (nullable FK to `RevisionFiles`)
  - `DocumentReviewFindings.SourceFileVersion` (integer)
  - `DocumentReviewFindings.RevisionFileId` (nullable FK to `RevisionFiles`)
  - `DocumentReviewTasks.ReviewCycleNumber` (integer, default 1)
- The unique filtered index on `RevisionFiles` (`"IsActive" = true`) is retained, guaranteeing that only one active Word file and one active Controlled PDF exist per revision simultaneously.

#### 4.2 Application Layer (`MicroLIMS.Application`)
- **`DocumentReviewService`:**
  - `SubmitForReviewAsync`: Replaces PDF check with Word source existence check (`FileRole.SourceFile && IsActive`).
  - `DecideReviewAsync`: Computes unresolved mandatory findings across the entire revision (`r.ReviewTasks.SelectMany(t => t.Findings)`), blocking completion if any remain unaddressed.
  - `ReturnForCorrection`: Sets revision to `Draft` and review task to `ReturnedForCorrection`, maintaining finding persistence.
- **`DocumentApprovalService`:**
  - `EvaluateReadiness`: Validates presence of active Word source and resolution of all mandatory findings.
  - `ExecuteApprovalDecisionAsync`: Wraps signature verification, Word source pinning, `IDocumentConversionService.ConvertDocxToPdfAsync`, PDF hashing, and `RevisionFile` insertion within a relational execution strategy transaction (`BeginTransactionAsync`).
- **`DocumentAuthorizationService`:**
  - `CanAccessSourceFileAsync`: Extended to return `true` if caller is assigned reviewer on an active `DocumentReviewTask` or assigned approver on an active `DocumentApprovalTask`.

#### 4.3 Infrastructure Layer (`MicroLIMS.Infrastructure`)
- Introduces `GotenbergDocumentConversionService` implementing `IDocumentConversionService`.
- Configured with resilient HTTP retry policies (`Polly`) for transient socket timeouts, with an absolute 60-second execution boundary.

---

### 5. Security & GxP Compliance Matrix

| Regulatory Requirement | Technical Mechanism Enforced by ADR-DC-004 |
| :--- | :--- |
| **21 CFR § 11.10(a) Validation** | Deterministic containerized conversion engine produces identical PDF artifacts from identical Word input bytes. |
| **21 CFR § 11.10(b) Document Protection** | Controlled PDF stored with read-only ACLs; SHA-256 hash verified upon every retrieval attempt. |
| **21 CFR § 11.10(e) Audit Trails** | Semantic events capture author Word upload, replacements, reviewer downloads, approval e-signature, and post-approval PDF generation. |
| **21 CFR § 11.50 Signature Manifestation** | Printed name, date/time, and signature meaning (`Approved`) embedded in database audit record and approval task. |
| **21 CFR § 11.70 Signature Linking** | Electronic signature record is atomically committed in the same database transaction that links the approved Word source and generated Controlled PDF. |
| **Segregation of Duties (SoD)** | Author ≠ Reviewer, Author ≠ Approver, Reviewer ≠ Approver enforced at service layer with zero administrative bypass. |

---

### 6. Qualification & Verification Strategy

1. **Deterministic Unit Testing:**  
   Mock `IDocumentConversionService` to verify state transitions, rollback behavior, and gate checks under simulated failure conditions without container dependencies.
2. **Integration Testing with Real Gotenberg Container:**  
   Verify actual OOXML Word document conversion, font rendering, table borders, and PDF magic bytes validation.
3. **End-to-End Operational Qualification (OQ):**  
   Formal OQ protocols verifying the complete 8-step lifecycle from Word draft authoring through technical review, author correction, approval, post-approval PDF release, and training cascade.
4. **Zero Administrative Masquerading:**  
   Qualification scenarios shall execute strictly using genuine authenticated user sessions for Author (`analyst1`), Reviewer (`reviewer1`), and Approver (`approver1`).
