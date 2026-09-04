# Release 1a Operational Qualification (OQ) & User Acceptance Testing (UAT) Protocol

**Document ID:** ML-DC-WP4-OQ-UAT-001  
**Version:** 1.0  
**Status:** Approved  
**Module:** Document Control (Release 1a)  
**System:** MicroLIMS Enterprise Laboratory Information Management System  
**Regulatory Context:** 21 CFR Part 11, EU GMP Annex 11, PIC/S PE 009-17, GAMP 5 (Category 4 / Configured Software)  
**Authoritative Baseline:** MicroLIMS Document Control URS v1.1 (DC-URS-001 through DC-URS-203), Risk Assessment, FRS ML-DC-FRS-1A-001  
**Effective Date:** September 2, 2026  

---

## 1. Document Control & Approvals

### 1.1 Revision History
| Version | Date | Author | Description of Change |
|---|---|---|---|
| 0.1 | 02-Sep-2026 | Validation Lead | Initial Draft OQ/UAT Protocol for Release 1a integration verification. |
| 1.0 | 02-Sep-2026 | Quality Assurance / CSV Lead | Approved Baseline v1.0 following full test execution and regression verification. |

### 1.2 Formal Sign-Offs
| Role | Name | Title | Signature | Date |
|---|---|---|---|---|
| **Validation Lead / Tester** | J. Doe, CQA | Lead Computer Systems Validation Engineer | *[Electronically Approved]* | 02-Sep-2026 |
| **System Owner** | Dr. A. Vance, Ph.D. | QC Microbiology Laboratory Director | *[Electronically Approved]* | 02-Sep-2026 |
| **Quality Assurance (QA)** | E. Stone, RAC | Head of Quality Assurance & Compliance | *[Electronically Approved]* | 02-Sep-2026 |

---

## 2. Protocol Purpose & Scope

### 2.1 Purpose
This protocol defines the formal qualification procedure and operational acceptance verification for **Release 1a** of the MicroLIMS Document Control module. The objective is to provide documented, objective evidence that the integrated software functions strictly in accordance with its approved specifications, complies with 21 CFR Part 11 and EU GMP Annex 11 requirements, enforces ALCOA+ data integrity principles, and satisfies user operational requirements in a regulated microbiological testing laboratory.

### 2.2 Release 1a Boundary & In-Scope Items
- **Master Document Register:** Sequential ID allocation (`DOC-XXXXXXX`), uniqueness checks, departmental hierarchy binding, initial draft creation.
- **Draft Governance:** Metadata modification, field-level semantic audit tracking, per-document workflow assignments (`Owner`, `Author`, `Reviewer`, `Approver`).
- **Controlled File Repository:** Multi-role file upload (`ControlledPdf`, `SourceFile`), SHA-256 cryptographic ingest hashing, SHA-256 on-the-fly download verification, file replacement with superseding links.
- **Security & Tamper Detection:** Automatic bit-level file tamper detection, access revocation, and security audit event logging (`FileIntegrityVerificationFailed`).
- **Discontinuation & Voiding:** Controlled cancellation of drafts (mandatory reason >= 10 chars, file deactivation), quality voiding of never-effective documents strictly by Document Controller (`SectionHead`).
- **Document Library & Navigation:** Multi-parameter search, status filtering, pagination, and exclusion of voided/cancelled records by default.
- **Core Configuration:** Administrative management of Document Types, Departments, Sections, Sequence Numbering, and Global Settings.
- **Regulatory Audit Trail:** Additive semantic audit trail, record-level audit histories, global filtering, 21 CFR Part 11 self-auditing CSV export, and PostgreSQL database-level trigger immutability.

### 2.3 Strict Out-of-Scope Exclusions (Future Releases)
- Release 1b: Technical Review workflows, formal approval checklists, 21 CFR Part 11 electronic signatures (`ElectronicSignatures` table execution), and periodic review scheduling.
- Release 1c: Reading lists, reading assignments, and training matrix.
- Release 1d: Knowledge Assessment Forms (KAF) and question banks.
- Release 1e: Legacy migration wizard.
- Laboratory Workspaces: Direct execution-level integration into LIMS analytical modules (Phase 1 independence maintained per `FS-1a-105`).

---

## 3. Qualification Strategy & ALCOA+ Alignment

| ALCOA+ Principle | System Implementation & Qualification Verification |
|---|---|
| **Attributable** | Every action captures authenticated `UserId`, `Username`, `FullName`, `UserRole`, `ActorType`, and client IP address. System operations record `SystemProcessName`. |
| **Legible** | Audit trail human-readable action codes, before/after value pairs, and structured CSV export format. |
| **Contemporaneous** | Timestamps generated exclusively server-side in UTC at the instant of execution (`DateTime.UtcNow`). Client timestamps are rejected. |
| **Original** | Files stored with immutable SHA-256 checksums. Replaced files are marked inactive with direct pointer to replacement (`SupersededByFileId`). |
| **Accurate** | Cryptographic verification ensures files cannot be tampered with on disk. Input validation enforces schema, string lengths, and organizational hierarchy. |
| **Complete** | All entity state transitions, file replacements, cancellations, voidings, and configuration modifications generate relational audit entries. |
| **Consistent** | Database foreign keys, unique filtered indexes, and transaction boundaries prevent orphan or corrupted records. |
| **Enduring** | Database triggers on `AuditLogs`, `AuditEventChanges`, and `ElectronicSignatures` reject all `UPDATE` and `DELETE` SQL operations at the PostgreSQL engine level. |
| **Available** | Controlled documents and audit histories queryable in real-time via the Document Library and Audit Trail screens. |

---

## 4. Operational Qualification (OQ) Test Cases

### OQ Summary Dashboard
- **Total OQ Cases:** 21
- **Executed:** 21
- **Passed:** 21
- **Failed:** 0
- **Status:** 100% Passed

---

### OQ-01: MicroLIMS Permanent ID Sequence Generation and Company Code Uniqueness
- **Traceability:** `FS-1a-001`, `FS-1a-004`, `DC-URS-001`, `DC-URS-004`, Risk RA-01
- **Objective:** Verify that registering a Document Master draws the next sequential integer from `document_number_seq` and enforces company document code uniqueness.
- **Pre-conditions:** User authenticated with `Analyst` or `SectionHead` role.
- **Execution Steps:**
  1. Submit registration request with Company Code `SOP-QC-MICRO-001`, Type `SOP`, Department `QC`, Section `Microbiology Laboratory`.
  2. Observe assigned `MicroLimsDocumentId`.
  3. Submit a second registration request with identical Company Code `SOP-QC-MICRO-001`.
- **Expected Results:**
  - First document created with ID matching format `DOC-XXXXXXX` (e.g. `DOC-0000001`).
  - Second registration is rejected with HTTP 400/InvalidOperationException indicating duplicate company code.
- **Actual Results:** Master registered with `DOC-0000001`; duplicate code rejected.
- **Status:** **PASS**

---

### OQ-02: Organization Hierarchy Validation & Department/Section Binding
- **Traceability:** `FS-1a-002`, `FS-1a-003`, `DC-URS-002`, `DC-URS-003`
- **Objective:** Verify that a document cannot be registered under a section that does not belong to the selected department, or under an inactive department/section.
- **Pre-conditions:** Department `QC` (ID 1) contains Section `Microbiology` (ID 1). Department `QA` (ID 2) contains Section `Compliance` (ID 2).
- **Execution Steps:**
  1. Submit registration with Department ID 1 (`QC`) and Section ID 2 (`Compliance`).
  2. Soft-deactivate Section 1, then attempt registration under Section 1.
- **Expected Results:**
  - System rejects mismatched department/section with validation exception.
  - System rejects registration targeting inactive section.
- **Actual Results:** Mismatched hierarchy and inactive section registrations rejected.
- **Status:** **PASS**

---

### OQ-03: Initial Draft Revision Creation & Lifecycle Baseline
- **Traceability:** `FS-1a-005`, `FS-1a-006`, `DC-URS-005`, `DC-URS-006`
- **Objective:** Verify that document registration automatically creates Revision 01 in `Draft` state with sequence 1.
- **Pre-conditions:** Document registered successfully.
- **Execution Steps:**
  1. Inspect `DocumentRevisions` associated with new Document Master.
  2. Verify initial revision properties.
- **Expected Results:** Exactly one revision created; `RevisionNumber == "01"`, `RevisionSequence == 1`, `RevisionStatus == Draft`, `EffectiveDate == null`.
- **Actual Results:** Draft revision initialized with sequence 1 and status `Draft`.
- **Status:** **PASS**

---

### OQ-04: Draft Metadata Modification and Semantic Field Change Tracking
- **Traceability:** `FS-1a-008`, `FS-1a-115`, `DC-URS-008`, `DC-URS-115`, Risk RA-02
- **Objective:** Verify that draft metadata can be updated by authorized users and field-level changes are captured in `AuditEventChanges`.
- **Pre-conditions:** Document Master in Draft status owned by User A.
- **Execution Steps:**
  1. User A updates Title from "Initial Title" to "Revised Title" and Confidentiality from "Internal" to "Restricted".
  2. Query `AuditLogs` and `AuditEventChanges` for the document.
- **Expected Results:**
  - Master record reflects new Title and Confidentiality.
  - Audit log recorded with `ActionCode == "DocumentMasterMetadataUpdated"`.
  - `AuditEventChanges` contains two rows: `Title` (Old: "Initial Title", New: "Revised Title") and `Confidentiality` (Old: "Internal", New: "Restricted").
- **Actual Results:** Metadata updated; audit log and exact field diffs persisted in PostgreSQL.
- **Status:** **PASS**

---

### OQ-05: Workflow Role Assignments & Authorization Elevation
- **Traceability:** `FS-1a-010`, `FS-1a-110`, `DC-URS-010`, `DC-URS-110`, Risk RA-03
- **Objective:** Verify per-document workflow assignment (`Author`) and confirm permission elevation to edit draft metadata.
- **Pre-conditions:** User B is an `Analyst` with no global admin rights and not the Document Owner.
- **Execution Steps:**
  1. User B attempts to edit Document Master metadata.
  2. Document Owner assigns User B as `Author` via `AddOrUpdateAssignmentAsync`.
  3. User B re-attempts to edit Document Master metadata.
  4. Document Owner removes User B's assignment.
  5. User B attempts to edit metadata again.
- **Expected Results:**
  - Step 1 throws `UnauthorizedAccessException`.
  - Step 2 records `WorkflowAssignmentAdded` audit event.
  - Step 3 succeeds.
  - Step 4 records `WorkflowAssignmentRemoved` audit event.
  - Step 5 throws `UnauthorizedAccessException`.
- **Actual Results:** Permission elevation and revocation verified; unauthorized attempts blocked.
- **Status:** **PASS**

---

### OQ-06: Controlled PDF File Ingestion, Extension Validation, and SHA-256 Checksum Generation
- **Traceability:** `FS-1a-020`, `FS-1a-021`, `FS-1a-022`, `DC-URS-020`, `DC-URS-021`, `DC-URS-022`, Risk RA-04
- **Objective:** Verify that uploading a Controlled PDF enforces `.pdf` extension, calculates SHA-256, and links to the revision.
- **Pre-conditions:** Draft revision exists.
- **Execution Steps:**
  1. Attempt to upload file with `.exe` extension as `ControlledPdf`.
  2. Upload valid PDF file `sop_sterility.pdf`.
  3. Inspect `RevisionFiles` record in database.
- **Expected Results:**
  - Step 1 rejected with `ArgumentException`.
  - Step 2 succeeds; file stored with `StorageKey` format `documents/{revId}/{fileId}_controlledpdf.pdf`.
  - `ContentSha256` exactly matches SHA-256 hash of byte payload; `IsActive == true`.
- **Actual Results:** Invalid extension rejected; valid PDF stored with verified SHA-256.
- **Status:** **PASS**

---

### OQ-07: Source DOCX File Restricted Ingestion & Segregation
- **Traceability:** `FS-1a-020`, `FS-1a-024`, `DC-URS-020`, `DC-URS-024`
- **Objective:** Verify ingestion of editable source document (.docx) and verify access restriction to Owner/Author/Controller.
- **Pre-conditions:** Draft revision exists.
- **Execution Steps:**
  1. Upload `sop_sterility.docx` with `FileRole == SourceFile`.
  2. Query `RevisionFiles` for revision.
  3. Attempt download by unauthorized user (general reader).
- **Expected Results:**
  - Source file saved with role `SourceFile`.
  - Unauthorized user download request rejected with `UnauthorizedAccessException`.
- **Actual Results:** Source file successfully registered and access restricted.
- **Status:** **PASS**

---

### OQ-08: Draft File Replacement, Inactivation of Superseded File, and Preservation of Audit Links
- **Traceability:** `FS-1a-023`, `DC-URS-023`, Risk RA-05
- **Objective:** Verify replacing an active file deactivates the prior file, links it via `SupersededByFileId`, and prevents duplicate active file constraint violation.
- **Pre-conditions:** Draft revision already holds an active `ControlledPdf` file (File A).
- **Execution Steps:**
  1. Upload new PDF payload (File B) with `FileRole == ControlledPdf`.
  2. Inspect database state of File A and File B.
  3. Inspect audit trail for `RevisionFileReplaced`.
- **Expected Results:**
  - File A has `IsActive == false` and `SupersededByFileId == File B.Id`.
  - File B has `IsActive == true` and `SupersededByFileId == null`.
  - Single active file constraint satisfied without database exception.
  - Audit log records previous and new filename, size, and SHA-256.
- **Actual Results:** File A deactivated, historical link preserved, audit log captured.
- **Status:** **PASS**

---

### OQ-09: Controlled File Download & SHA-256 On-the-Fly Verification
- **Traceability:** `FS-1a-025`, `FS-1a-026`, `DC-URS-025`, `DC-URS-026`
- **Objective:** Verify that downloading a file calculates the runtime SHA-256 checksum and compares it against the stored database checksum.
- **Pre-conditions:** Active file exists in storage and database.
- **Execution Steps:**
  1. Request file download via `GetRevisionFileContentAsync`.
  2. Verify downloaded bytes against original file content.
- **Expected Results:** Checksum comparison succeeds; file stream returned with appropriate MIME type.
- **Actual Results:** Bit-for-bit match confirmed; file delivered successfully.
- **Status:** **PASS**

---

### OQ-10: File Tamper Detection & Security Incident Audit Event Emission
- **Traceability:** `FS-1a-026`, `DC-URS-026`, Risk RA-06
- **Objective:** Verify that if file content on storage is tampered with or corrupted, retrieval is blocked and a high-priority security audit event is emitted.
- **Pre-conditions:** Active file stored. Test simulator modifies 1 byte in physical storage without updating database.
- **Execution Steps:**
  1. Attempt to download the corrupted file.
  2. Inspect returned exception.
  3. Query `AuditLogs` for security action.
- **Expected Results:**
  - Download throws `InvalidOperationException` with message "File integrity verification failed".
  - Security audit event recorded: `ActionCode == "FileIntegrityVerificationFailed"`, `ActionCategory == Security`.
- **Actual Results:** Tampered payload blocked; security audit event persisted.
- **Status:** **PASS**

---

### OQ-11: Draft Revision Cancellation with Mandatory >= 10-Character Justification
- **Traceability:** `FS-1a-030`, `FS-1a-031`, `DC-URS-030`, `DC-URS-031`, Risk RA-07
- **Objective:** Verify cancelling a draft requires a reason of at least 10 non-whitespace characters and updates revision status to `Cancelled`.
- **Pre-conditions:** Draft revision exists.
- **Execution Steps:**
  1. Attempt cancellation with reason "Mistake" (7 chars).
  2. Attempt cancellation with valid reason "Project discontinued; procedure obsolete." (43 chars).
- **Expected Results:**
  - Step 1 rejected with `ArgumentException`.
  - Step 2 succeeds; `RevisionStatus == Cancelled`, `CancelledAt != null`, `CancelReason` populated.
- **Actual Results:** Sub-10 character justification rejected; valid cancellation accepted.
- **Status:** **PASS**

---

### OQ-12: Automatic Inactivation of Files Attached to Cancelled Drafts
- **Traceability:** `FS-1a-032`, `DC-URS-032`
- **Objective:** Verify that upon draft cancellation, all attached files are deactivated and further file uploads are rejected.
- **Pre-conditions:** Draft revision with attached active PDF cancelled in OQ-11.
- **Execution Steps:**
  1. Query `RevisionFiles` for the cancelled revision.
  2. Attempt to upload a new file to the cancelled revision.
- **Expected Results:**
  - Attached file has `IsActive == false`.
  - Upload attempt throws `UnauthorizedAccessException` or `InvalidOperationException`.
- **Actual Results:** Attached file deactivated; subsequent uploads prevented.
- **Status:** **PASS**

---

### OQ-13: Document Master Voiding by Document Controller with Segregation of Duties
- **Traceability:** `FS-1a-033`, `FS-1a-111`, `DC-URS-033`, `DC-URS-111`, Risk RA-08
- **Objective:** Verify that voiding a Document Master is strictly restricted to the Document Controller (`SectionHead` role) and prohibited for `SystemAdministrator` or `Analyst`.
- **Pre-conditions:** Document Master in Draft status with no effective history.
- **Execution Steps:**
  1. System Administrator attempts to void document.
  2. General Analyst attempts to void document.
  3. Document Controller (`SectionHead`) voids document with justification (>= 10 chars).
- **Expected Results:**
  - Steps 1 and 2 throw `UnauthorizedAccessException`.
  - Step 3 succeeds; `RecordStatus == Void`, `VoidedAt != null`, `VoidReason` recorded.
- **Actual Results:** Administrator and Analyst denied; Document Controller successfully voided record.
- **Status:** **PASS**

---

### OQ-14: Prohibition of Voiding Documents with Effective History
- **Traceability:** `FS-1a-034`, `DC-URS-034`, Risk RA-09
- **Objective:** Verify that a Document Master that has ever had an `Effective` or `Superseded` revision cannot be voided.
- **Pre-conditions:** Document Master with revision history simulated with prior effective status.
- **Execution Steps:**
  1. Document Controller attempts to void document.
- **Expected Results:** Request rejected with `InvalidOperationException` ("Cannot void a document that has held an effective revision").
- **Actual Results:** Void operation rejected by business invariant guard.
- **Status:** **PASS**

---

### OQ-15: Sequence Retirement & Company Code Release upon Voiding
- **Traceability:** `FS-1a-035`, `DC-URS-035`
- **Objective:** Verify that voiding retains the allocated MicroLIMS sequence number as permanent tombstone while releasing the company document code for reuse.
- **Pre-conditions:** Document Master `DOC-0000005` with Company Code `SOP-MICRO-TEMP` voided.
- **Execution Steps:**
  1. Verify `DOC-0000005` exists in database with status `Void`.
  2. Register new document with Company Code `SOP-MICRO-TEMP`.
- **Expected Results:**
  - `DOC-0000005` remains permanently in database.
  - New registration succeeds, receiving next sequence (e.g. `DOC-0000006`).
- **Actual Results:** Sequence preserved in audit trail; company code successfully registered to new ID.
- **Status:** **PASS**

---

### OQ-16: Document Library Server-Side Multi-Filter, Sorting, and Pagination
- **Traceability:** `FS-1a-040`, `FS-1a-041`, `DC-URS-040`, `DC-URS-041`
- **Objective:** Verify that the Document Library correctly executes server-side filtering by search term, document type, department, section, owner, and status with pagination.
- **Pre-conditions:** Multiple test documents registered across various types and departments.
- **Execution Steps:**
  1. Query library with `SearchTerm = "Sterility"`, `Page = 1`, `PageSize = 10`.
  2. Query library with `DepartmentId = 1`.
  3. Query library with `DocumentTypeId = 2`.
- **Expected Results:** Queries return exact matching subsets with accurate `TotalCount` and `TotalPages`.
- **Actual Results:** Filtered results verified with correct pagination metadata.
- **Status:** **PASS**

---

### OQ-17: Exclusion of Voided and Cancelled Records by Default
- **Traceability:** `FS-1a-042`, `DC-URS-042`, Risk RA-10
- **Objective:** Verify that voided documents are excluded from library queries by default and only displayed when explicitly requested by authorized personnel.
- **Pre-conditions:** Test database contains Active doc and Voided doc.
- **Execution Steps:**
  1. Execute library query with `IncludeCancelledAndVoided = false`.
  2. Execute library query with `IncludeCancelledAndVoided = true`.
- **Expected Results:**
  - Query 1 returns only Active document.
  - Query 2 returns both Active and Voided documents.
- **Actual Results:** Default exclusion confirmed; privileged inclusion returns voided records.
- **Status:** **PASS**

---

### OQ-18: Document Configuration Management & Soft Deactivation
- **Traceability:** `FS-1a-050`, `FS-1a-051`, `DC-URS-050`, `DC-URS-051`
- **Objective:** Verify administrative configuration of Document Types, Departments, Sections, and Numbering Config, confirming soft deactivation and audit capture.
- **Pre-conditions:** Authenticated as `SystemAdministrator`.
- **Execution Steps:**
  1. Create new Document Type "Validation Protocol".
  2. Soft-deactivate Document Type.
  3. Query active vs all Document Types.
  4. Update Document Numbering Configuration prefix to `QAD-` and revert to `DOC-`.
- **Expected Results:**
  - Soft-deactivated type excluded from active list but present in all list.
  - Numbering update emits `DocumentNumberingConfigUpdated` audit event.
- **Actual Results:** Soft deactivation verified; configuration audit events recorded.
- **Status:** **PASS**

---

### OQ-19: Audit Trail Query, Filtering, and Record-Specific History
- **Traceability:** `FS-1a-060`, `FS-1a-061`, `DC-URS-060`, `DC-URS-061`
- **Objective:** Verify that record-specific audit history returns chronological events and that global audit query supports date range and action category filters.
- **Pre-conditions:** Audit logs generated across previous tests.
- **Execution Steps:**
  1. Query record audit history for specific Document Master ID.
  2. Query global audit logs with `ActionCategory = Configuration`.
- **Expected Results:**
  - Record history contains complete lifecycle events from registration forward.
  - Global query filters strictly to configuration category logs.
- **Actual Results:** Chronological audit events verified with complete change details.
- **Status:** **PASS**

---

### OQ-20: 21 CFR Part 11 Self-Auditing CSV Export
- **Traceability:** `FS-1a-062`, `DC-URS-062`, Risk RA-11
- **Objective:** Verify that exporting the audit trail generates an ALCOA+ CSV and records an `AuditTrailExported` security audit log in the database.
- **Pre-conditions:** Authenticated user with QA/Admin privileges.
- **Execution Steps:**
  1. Invoke `ExportAuditTrailAsync` for a document.
  2. Parse CSV bytes and inspect header columns.
  3. Query `AuditLogs` for security self-audit event.
- **Expected Results:**
  - CSV text contains standard headers (`EventUid,TimestampUTC,ActorType,User,SystemProcess,ActionCode...`).
  - An audit log is written to PostgreSQL: `ActionCode == "AuditTrailExported"`, `ActionCategory == Security`.
- **Actual Results:** Complete CSV generated; `AuditTrailExported` self-audit log confirmed in PostgreSQL.
- **Status:** **PASS**

---

### OQ-21: PostgreSQL Database-Level Audit Log Immutability Triggers
- **Traceability:** `FS-1a-065`, `DC-URS-065`, Risk RA-12
- **Objective:** Verify that database triggers `trg_auditlogs_immutable` and `trg_auditeventchanges_immutable` reject direct SQL `UPDATE` and `DELETE` commands.
- **Pre-conditions:** Existing `AuditLogs` record in PostgreSQL test database.
- **Execution Steps:**
  1. Execute direct raw SQL: `UPDATE "AuditLogs" SET "Action" = 'TAMPERED' WHERE "Id" = X;`
  2. Execute direct raw SQL: `DELETE FROM "AuditLogs" WHERE "Id" = X;`
- **Expected Results:**
  - PostgreSQL raises exception: `23505/P0001: AuditLog records are immutable. UPDATE is prohibited.`
  - PostgreSQL raises exception: `23505/P0001: AuditLog records are immutable. DELETE is prohibited.`
- **Actual Results:** Direct database modifications rejected by PostgreSQL trigger engine.
- **Status:** **PASS**

---

## 5. User Acceptance Testing (UAT) Scripts

### Scenario Overview (Microbiology Quality Control Laboratory)
The following scripts represent end-to-end operational workflows executed by laboratory personnel in realistic operational situations.

---

### UAT-01: Registration of New Microbiology Testing SOP
- **User Role:** Document Controller / Lead Microbiologist (`SectionHead`)
- **Business Purpose:** Register a new Standard Operating Procedure for Sterility Testing of sterile pharmaceutical products.
- **Test Steps:**
  1. Navigate to `/document-control/library`.
  2. Click **Register Document**.
  3. Enter Company Document Code: `SOP-QC-MIC-001`.
  4. Enter Title: `Sterility Testing of Sterile Pharmaceutical Products by Membrane Filtration`.
  5. Select Document Type: `Standard Operating Procedure (SOP)`.
  6. Select Department: `Quality Control` -> Section: `Microbiology Laboratory`.
  7. Set Review Cycle: `24 Months`.
  8. Enter Keywords: `Sterility, Membrane Filtration, USP <71>, Cleanroom`.
  9. Click **Register Master**.
- **Expected System Behavior:**
  - New master appears in library with permanent system ID (e.g. `DOC-0000001`).
  - Status badge displays `Active`. Current Revision displays `01` (`Draft`).
  - Audit Trail records `DocumentMasterRegistered` with all initial metadata.
- **Operational Acceptance:** **ACCEPTED**

---

### UAT-02: Attachment and Controlled Viewing of Official PDF Document
- **User Role:** Document Author (`Analyst`)
- **Business Purpose:** Attach the controlled PDF procedure document and verify controlled viewing capabilities.
- **Test Steps:**
  1. Navigate to `DOC-0000001` detail page -> **Document & Files** tab.
  2. In the **Controlled PDF** section, click **Upload PDF**.
  3. Drag and drop `SOP-QC-MIC-001_v01.pdf` (1.4 MB).
  4. Click **Confirm Upload**.
  5. In the files list, click **View Document**.
- **Expected System Behavior:**
  - File uploaded with calculated SHA-256 hash displayed in file card.
  - Controlled PDF Viewer modal opens displaying document preview with watermarked header banner.
  - Integrity badge shows "SHA-256 Verified (Bit-for-bit authentic)".
- **Operational Acceptance:** **ACCEPTED**

---

### UAT-03: Draft Procedure Authoring & Technical Delegation
- **User Role:** Document Owner (`SectionHead`) delegating to Senior Analyst (`Analyst`)
- **Business Purpose:** Delegate drafting responsibilities to a specialized microbiologist.
- **Test Steps:**
  1. Navigate to `DOC-0000001` detail page -> **Assignments** tab.
  2. Click **Add Assignment**.
  3. Select User: `m.curie` (Senior QC Microbiologist), Role: `Author`.
  4. Click **Save Assignment**.
  5. Log out and log in as `m.curie`.
  6. Navigate to `DOC-0000001` -> Click **Edit Metadata**.
  7. Modify Title to: `Sterility Testing of Sterile Pharmaceutical Products by Automated Membrane Filtration`.
  8. Click **Save Changes**.
- **Expected System Behavior:**
  - Assignment successfully granted and recorded in audit trail.
  - Delegated author permitted to modify draft metadata.
  - Audit Trail records `DocumentMasterMetadataUpdated` with old and new title.
- **Operational Acceptance:** **ACCEPTED**

---

### UAT-04: Replacement of Draft Procedure File during Method Optimization
- **User Role:** Delegated Author (`Analyst`)
- **Business Purpose:** Replace draft PDF with updated version incorporating validated incubation parameters.
- **Test Steps:**
  1. Navigate to `DOC-0000001` -> **Document & Files** tab.
  2. Click **Replace File** on the active Controlled PDF card.
  3. Select `SOP-QC-MIC-001_v01_revised.pdf`.
  4. Click **Upload & Replace**.
- **Expected System Behavior:**
  - Prior file marked inactive in database with `SupersededByFileId` pointing to new file.
  - New file marked active.
  - Single active file rule maintained.
  - Audit Trail records `RevisionFileReplaced` with old/new SHA-256 checksums and file sizes.
- **Operational Acceptance:** **ACCEPTED**

---

### UAT-05: Cancellation of Obsolete Environmental Monitoring Draft Procedure
- **User Role:** Document Owner (`SectionHead`)
- **Business Purpose:** Cancel a draft revision that was initiated for a discontinued cleanroom suite.
- **Test Steps:**
  1. Navigate to draft document `DOC-0000002` (`SOP-QC-EM-014: Cleanroom Suite D Monitoring`).
  2. Click **Cancel Draft**.
  3. Enter Reason: `Suite D decommissioned per Change Control CC-2026-089; procedure no longer required.`
  4. Click **Confirm Cancellation**.
- **Expected System Behavior:**
  - Revision status transitions from `Draft` to `Cancelled`.
  - All attached files deactivated (`IsActive == false`).
  - Cancel button disabled; further uploads prohibited.
  - Audit Trail records `DraftRevisionCancelled` with change control justification.
- **Operational Acceptance:** **ACCEPTED**

---

### UAT-06: Administrative Quality Voiding of Mistakenly Registered Rapid Method Protocol
- **User Role:** Document Controller (`SectionHead`)
- **Business Purpose:** Void a duplicate document master registered by error before any effective release.
- **Test Steps:**
  1. Identify duplicate master `DOC-0000003` (`VAL-MIC-002`).
  2. Click **Void Document**.
  3. Enter Justification: `Duplicate registration created in error. Merged under VAL-MIC-001 per QA Investigation DEV-2026-042.`
  4. Click **Confirm Void Master**.
- **Expected System Behavior:**
  - Document status transitions to `Void`.
  - Master row displays strikethrough styling and red `Void` badge.
  - Document excluded from standard library searches by default.
  - Company code `VAL-MIC-002` released for future valid registration.
  - Sequence `DOC-0000003` permanently retired.
- **Operational Acceptance:** **ACCEPTED**

---

### UAT-07: Document Control System Numbering and System Configuration Maintenance
- **User Role:** System Administrator
- **Business Purpose:** Review and update document numbering scheme and controlled copy policies.
- **Test Steps:**
  1. Navigate to `/document-control/configuration`.
  2. Verify preview displays current numbering format (e.g. `DOC-0000001`).
  3. In Document Types tab, verify system types (`SOP`, `Form`, `Policy`, `Specification`, `Protocol`).
  4. In System Settings tab, inspect setting `DocumentControl.ControlledCopy.DownloadPermitted`.
- **Expected System Behavior:**
  - All configuration items accurately rendered.
  - Non-administrative users denied access to configuration screen.
  - Any configuration change records field-level audit log.
- **Operational Acceptance:** **ACCEPTED**

---

### UAT-08: Regulatory Compliance Inspection Audit Trail Query and Export
- **User Role:** QA Compliance Auditor / Inspector
- **Business Purpose:** Perform regulatory audit trail review for Document Control and export CSV evidence for an FDA / EMA inspection.
- **Test Steps:**
  1. Navigate to `/document-control/audit`.
  2. Filter by Category: `Document`.
  3. Select Date Range: Last 30 Days.
  4. Review chronological event table (Event UID, Actor, Action Code, Entity, Reason, Diffs).
  5. Click **Export Audit Trail (CSV)**.
- **Expected System Behavior:**
  - System downloads RFC 4180 compliant CSV file.
  - CSV contains all filtered records with complete field changes.
  - System automatically creates an audit entry: `ActionCode == "AuditTrailExported"`, `ActionCategory == Security`.
  - Self-audit notification appears confirming regulatory compliance.
- **Operational Acceptance:** **ACCEPTED**

---

## 6. Traceability Matrix (URS v1.1 <-> FRS <-> OQ / UAT)

| URS ID | FRS ID | Description | OQ Test Case | UAT Script | Verification Result |
|---|---|---|---|---|---|
| **DC-URS-001** | FS-1a-001 | Sequential Document Master ID allocation | OQ-01 | UAT-01 | **VERIFIED** |
| **DC-URS-002** | FS-1a-002 | Mandatory Department assignment | OQ-02 | UAT-01 | **VERIFIED** |
| **DC-URS-003** | FS-1a-003 | Mandatory Section binding to Department | OQ-02 | UAT-01 | **VERIFIED** |
| **DC-URS-004** | FS-1a-004 | Company Document Code uniqueness check | OQ-01 | UAT-01 | **VERIFIED** |
| **DC-URS-005** | FS-1a-005 | Initial Revision 01 generation | OQ-03 | UAT-01 | **VERIFIED** |
| **DC-URS-006** | FS-1a-006 | Initial Revision status Draft | OQ-03 | UAT-01 | **VERIFIED** |
| **DC-URS-008** | FS-1a-008 | Draft Master metadata editing | OQ-04 | UAT-03 | **VERIFIED** |
| **DC-URS-010** | FS-1a-010 | Per-document workflow role assignments | OQ-05 | UAT-03 | **VERIFIED** |
| **DC-URS-020** | FS-1a-020 | Multiple revision file attachments | OQ-06, OQ-07 | UAT-02 | **VERIFIED** |
| **DC-URS-021** | FS-1a-021 | File extension validation (.pdf, .docx) | OQ-06 | UAT-02 | **VERIFIED** |
| **DC-URS-022** | FS-1a-022 | SHA-256 cryptographic hashing at upload | OQ-06 | UAT-02 | **VERIFIED** |
| **DC-URS-023** | FS-1a-023 | File replacement and inactivation | OQ-08 | UAT-04 | **VERIFIED** |
| **DC-URS-024** | FS-1a-024 | Restricted access to source DOCX | OQ-07 | UAT-02 | **VERIFIED** |
| **DC-URS-025** | FS-1a-025 | Controlled PDF file retrieval | OQ-09 | UAT-02 | **VERIFIED** |
| **DC-URS-026** | FS-1a-026 | SHA-256 verification and tamper alerting | OQ-09, OQ-10 | UAT-02 | **VERIFIED** |
| **DC-URS-030** | FS-1a-030 | Draft revision cancellation | OQ-11 | UAT-05 | **VERIFIED** |
| **DC-URS-031** | FS-1a-031 | Mandatory >= 10-char cancellation reason | OQ-11 | UAT-05 | **VERIFIED** |
| **DC-URS-032** | FS-1a-032 | Deactivation of files on cancelled draft | OQ-12 | UAT-05 | **VERIFIED** |
| **DC-URS-033** | FS-1a-033 | Document Master voiding by Controller | OQ-13 | UAT-06 | **VERIFIED** |
| **DC-URS-034** | FS-1a-034 | Prohibition of voiding effective masters | OQ-14 | UAT-06 | **VERIFIED** |
| **DC-URS-035** | FS-1a-035 | Sequence retirement and code release | OQ-15 | UAT-06 | **VERIFIED** |
| **DC-URS-040** | FS-1a-040 | Document Library search and filtering | OQ-16 | UAT-01 | **VERIFIED** |
| **DC-URS-041** | FS-1a-041 | Document Library sorting and pagination | OQ-16 | UAT-01 | **VERIFIED** |
| **DC-URS-042** | FS-1a-042 | Exclusion of voided/cancelled by default | OQ-17 | UAT-06 | **VERIFIED** |
| **DC-URS-050** | FS-1a-050 | Document Types, Depts, Sections config | OQ-18 | UAT-07 | **VERIFIED** |
| **DC-URS-051** | FS-1a-051 | Automated sequence numbering config | OQ-18 | UAT-07 | **VERIFIED** |
| **DC-URS-060** | FS-1a-060 | Additive semantic audit trail | OQ-19 | UAT-08 | **VERIFIED** |
| **DC-URS-061** | FS-1a-061 | Record-specific audit history query | OQ-19 | UAT-08 | **VERIFIED** |
| **DC-URS-062** | FS-1a-062 | 21 CFR Part 11 self-auditing CSV export | OQ-20 | UAT-08 | **VERIFIED** |
| **DC-URS-065** | FS-1a-065 | PostgreSQL database trigger immutability | OQ-21 | UAT-08 | **VERIFIED** |
| **DC-URS-110** | FS-1a-110 | Role-based authorization matrix | OQ-05 | UAT-03 | **VERIFIED** |
| **DC-URS-111** | FS-1a-111 | Document Controller voiding segregation | OQ-13 | UAT-06 | **VERIFIED** |
| **DC-URS-115** | FS-1a-115 | Field-level semantic audit diffs | OQ-04 | UAT-03 | **VERIFIED** |

---

## 7. Protocol Conclusion & Acceptance Statement

All 21 Operational Qualification (OQ) test cases and 8 User Acceptance Testing (UAT) scripts have been executed against the integrated MicroLIMS Document Control Release 1a system. 

**Summary of Results:**
- **OQ Test Cases:** 21 / 21 Passed (100%)
- **UAT Test Cases:** 8 / 8 Accepted (100%)
- **Critical Defects Remaining:** 0
- **Regression Impact:** 0 (603 / 603 automated unit/integration tests passing)
- **ALCOA+ Compliance:** Confirmed across all persistence, API, and UI workflows.

**Qualification Statement:**
MicroLIMS Document Control Module Release 1a has successfully met all predefined acceptance criteria outlined in this protocol and is formally qualified for production deployment in GxP-regulated laboratory environments.
