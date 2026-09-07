# Release 1d Work Package 3 (WP3) Completion Report
## Authorization & Word File Versioning

- **Document ID:** `ML-DC-R1D-WP3-001`
- **Release:** Release 1d (Word-First Authoring, Review Lineage & Post-Approval PDF Release)
- **Work Package:** WP3 — Authorization & Word File Versioning
- **Date:** September 7, 2026
- **Status:** **WP3 COMPLETE — READY FOR WP4**
- **Controlled Change Record:** `CC-DC-R1D-001`
- **Governing Architecture Baseline:** `ADR-DC-004`
- **Authoritative Traceability Baseline:** `ML-DC-RTM-1D-001` v1.0
- **Previous Qualified Production Baseline:** Release 1c

---

### 1. Git Commit Identity & Repository State

- **Exact 40-Character Commit SHA:** `220de72b3e9426fc587ff81769cef67b8fb8db37`
- **Short SHA:** `220de72`
- **Branch:** `main`
- **Commit Timestamp:** `2026-09-07 23:57:40 +0300`
- **Parent Commit:** `45976105f7a39082d02e19fc164ad3d6b05c55cf` (WP2 Closeout Commit)
- **Files Included in WP3 Scope (10 Files):**
  1. `backend/MicroLIMS.Application/DTOs/DocumentControl/DocumentControlDtos.cs`
  2. `backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs`
  3. `backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuthorizationService.cs`
  4. `backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs`
  5. `backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs`
  6. `backend/MicroLIMS.Application/Services/DocumentControl/DocumentRevisionService.cs`
  7. `backend/MicroLIMS.Tests/IntegrationTests/DocumentControlRelease1dWP3PostgresIntegrationTests.cs`
  8. `backend/MicroLIMS.Tests/UnitTests/DocumentControlRelease1dWP3UnitTests.cs`
  9. `docs/Release_1d_WP2_Completion_Report.md`
  10. `docs/Release_1d_WP3_Completion_Report.md`

---

### 2. Executive Summary & Objective Fulfillment

Release 1d Work Package 3 (WP3) has been fully implemented, verified, and reconciled against the qualified baseline. WP3 delivers the complete business and security authorization rules for editable Word source files (`FileRole.SourceFile`), server-side sequential versioning, file header/magic-byte validation, cryptographic integrity verification, and security audit logging.

All objectives set forth in `CC-DC-R1D-001` for WP3 have been accomplished:
1. **Active Technical Reviewer Access:** Actively assigned Technical Reviewers are granted read-only download/view access to the Word source file for their specific assigned review task.
2. **Active Approver Access:** Actively assigned Approvers are granted read-only download/view access to the Word source file for their pending approval task.
3. **Read-Only Source Enforcement:** Neither Reviewers nor Approvers receive upload, replacement, or modification authority on source files. Upload and replacement remain strictly restricted to document authors/owners and administrators in Draft state.
4. **Sequential File Versioning:** Monotonically increasing `FileVersion` (v1, v2, v3...) is managed strictly server-side. Client-provided version parameters are ignored. Concurrent uploads are safeguarded via an EF Core retry loop backed by the unique index `IX_RevisionFiles_DocumentRevisionId_FileRole_FileVersion`.
5. **Magic Bytes & MIME Canonicalization:** File uploads for `FileRole.SourceFile` enforce Word format validation via file extension, MIME canonicalization, and binary magic bytes (`PK\x03\x04` for `.docx` and `D0 CF 11 E0 A1 B1 1A E1` for `.doc`).
6. **Physical Retention & SHA-256 Verification:** Superseded source versions remain immutable on physical storage. Cryptographic SHA-256 hash checks verify file integrity before delivery.
7. **Comprehensive Audit Trail:** All access, version replacement, tampering, and unauthorized access attempts are recorded in `AuditLogs` (`SourceFileDownloaded`, `SourceFileViewed`, `RevisionFileUploaded`, `RevisionFileReplaced`, `UnauthorizedFileAccessAttempted`, `FileIntegrityVerificationFailed`).
8. **Zero Database Migrations:** WP3 utilized the complete schema foundation already established in WP2. Zero database migrations were created.

---

### 2. Authorization Matrix Verification

The table below reflects the enforced access permissions across system roles and task assignments:

| Actor / Condition | File Role | Action | Permission | Enforcement Point |
| :--- | :--- | :--- | :--- | :--- |
| **SystemAdministrator** / **SectionHead** | `SourceFile` / `ControlledPdf` | Download / View | **ALLOW** | `DocumentAuthorizationService.CanAccessSourceFileAsync` |
| **Document Author / Owner** | `SourceFile` | Download / View (Draft/Review) | **ALLOW** | `DocumentAuthorizationService.IsOwnerOrAssignedAuthorAsync` |
| **Document Author / Owner** | `SourceFile` | Upload / Replace (Draft only) | **ALLOW** | `DocumentAuthorizationService.CanUploadOrReplaceDraftFileAsync` |
| **Active Technical Reviewer** (`Pending` / `InProgress`) | `SourceFile` | Download / View (Assigned revision only) | **ALLOW** | `DocumentAuthorizationService.CanAccessSourceFileAsync` |
| **Technical Reviewer** | `SourceFile` | Upload / Replace | **DENY (403)** | `DocumentAuthorizationService.CanUploadOrReplaceDraftFileAsync` |
| **Completed / Cancelled Reviewer** | `SourceFile` | Download / View | **DENY (403)** | `DocumentAuthorizationService.CanAccessSourceFileAsync` |
| **Reviewer Assigned to Doc A** | `SourceFile` (Doc B) | Download / View | **DENY (403)** | `DocumentAuthorizationService.CanAccessSourceFileAsync` |
| **Active Approver** (`Pending`) | `SourceFile` | Download / View (Assigned revision only) | **ALLOW** | `DocumentAuthorizationService.CanAccessSourceFileAsync` |
| **Approver** | `SourceFile` | Upload / Replace | **DENY (403)** | `DocumentAuthorizationService.CanUploadOrReplaceDraftFileAsync` |
| **Completed / Rejected Approver** | `SourceFile` | Download / View | **DENY (403)** | `DocumentAuthorizationService.CanAccessSourceFileAsync` |
| **Approver Assigned to Doc A** | `SourceFile` (Doc B) | Download / View | **DENY (403)** | `DocumentAuthorizationService.CanAccessSourceFileAsync` |
| **Unassigned Authenticated User** | `SourceFile` | Download / View | **DENY (403)** | `DocumentAuthorizationService.CanAccessSourceFileAsync` |
| **Anonymous / Unauthenticated** | `SourceFile` | Download / View | **DENY (401)** | ASP.NET Core `[Authorize]` on `DocumentFilesController` |

---

### 3. File Format Validation & Versioning Architecture

#### 3.1 Binary Magic Byte Validation
The file service validates source files using binary signature inspection prior to persistence:
- **DOCX (`.docx`):** Must start with bytes `0x50, 0x4B, 0x03, 0x04` (`PK\x03\x04`). Content type is canonicalized to `application/vnd.openxmlformats-officedocument.wordprocessingml.document`.
- **DOC (`.doc`):** Must start with bytes `0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1` (OLE Compound Document Header). Content type is canonicalized to `application/msword`.
- **Invalid / Renamed Files:** Files with non-Word extensions (e.g. `.pdf`, `.exe`, `.txt`) or mismatched header bytes are immediately rejected with `ArgumentException` (HTTP 400 Bad Request).

#### 3.2 Concurrency & Sequential Versioning
`UploadRevisionFileAsync` employs an optimistic concurrency retry loop:
1. Calculates `currentMaxVersion` directly from committed database records for the `(DocumentRevisionId, FileRole)` pair.
2. Allocates `FileVersion = currentMaxVersion + 1`.
3. Marks existing active file `IsActive = false`.
4. Saves changes to database. If a concurrent upload collides on `IX_RevisionFiles_DocumentRevisionId_FileRole_FileVersion`, EF Core catches the unique constraint violation (`23505`), detaches pending entities, applies a backoff delay, and retries up to 3 times.
5. Sets `SupersededByFileId` linking the superseded record directly to the new file record.
6. Returns `RevisionFileDto` containing `FileVersion`, `IsApprovedFinalSource`, and `GeneratedFromSourceFileId`.

---

### 4. Verification Evidence & Test Execution Results

Two test suites were executed: a focused WP3 test suite and the full repository regression suite.

#### 4.1 Focused WP3 Test Suite (28 Tests Total)
File: `backend/MicroLIMS.Tests/UnitTests/DocumentControlRelease1dWP3UnitTests.cs` (9 Tests)
File: `backend/MicroLIMS.Tests/IntegrationTests/DocumentControlRelease1dWP3PostgresIntegrationTests.cs` (19 Tests)

| Test ID | Test Name | Target Requirement | Result |
| :--- | :--- | :--- | :--- |
| **A-01** | `Test01_ActiveTechnicalReviewer_CanDownloadWordSourceFile_ForAssignedTask` | DC-URS-020 / ADR-DC-004 | **PASS** |
| **A-02** | `Test02_UnassignedTechnicalReviewer_IsDeniedDownloadOfWordSourceFile` | DC-URS-020 / Security | **PASS** |
| **A-03** | `Test03_ReviewerAssignedToDocA_CannotDownloadWordSourceForDocB` | Segregation of Duties | **PASS** |
| **A-04** | `Test04_ReviewerWithCompletedOrCancelledTask_CannotDownloadWordSource` | Lifecycle Access Window | **PASS** |
| **A-05** | `Test05_ActiveApprover_CanDownloadFinalWordSourceFile_ForAssignedApprovalTask` | DC-URS-022 / ADR-DC-004 | **PASS** |
| **A-06** | `Test06_UnassignedApprover_IsDeniedDownloadOfWordSourceFile` | DC-URS-022 / Security | **PASS** |
| **A-07** | `Test07_ApproverAssignedToDocA_CannotDownloadWordSourceForDocB` | Segregation of Duties | **PASS** |
| **A-08** | `Test08_TechnicalReviewer_CannotUploadReplacementWordSourceFile` | Authoring Segregation | **PASS** |
| **A-09** | `Test09_Approver_CannotUploadReplacementWordSourceFile` | Authoring Segregation | **PASS** |
| **A-10** | `Test10_UnauthenticatedOrAnonymousRequest_ToDownloadWordSourceFile_IsRejected` | Authentication Policy | **PASS** |
| **A-11** | `Test11_RandomAuthenticatedUser_GuessingFileId_CannotDownloadWordSourceFile` | Access Control List | **PASS** |
| **B-12** | `Test12_InitialWordUpload_CreatesFileVersion1_Active_SupersededNull` | DC-URS-024 / Lineage | **PASS** |
| **B-13** | `Test13_SecondWordUpload_CreatesFileVersion2_MarksV1Inactive_AndSetsSupersededBy` | DC-URS-025 / Lineage | **PASS** |
| **B-14** | `Test14_ThirdWordUpload_CreatesFileVersion3_MarksV2Inactive_AndSetsSupersededBy` | DC-URS-025 / Lineage | **PASS** |
| **B-15** | `Test15_SupersededPhysicalFiles_ArePreservedOnDisk_AndSha256Verified` | DC-URS-198 / Integrity | **PASS** |
| **B-16** | `Test16_UploadingNonWordFile_AsSourceFile_IsRejectedWithArgumentException` | File Format Gate | **PASS** |
| **B-17** | `Test17_UploadingInvalidOrCorruptWordFile_IsRejectedWithArgumentException` | Binary Magic Bytes Gate | **PASS** |
| **B-18** | `Test18_ClientProvidedFileVersion_IsIgnoredAndCalculatedServerSide` | Data Integrity / Server-side | **PASS** |
| **B-19** | `Test19_ConcurrentSourceFileUploads_HandleVersionCollisionGracefully` | Database Concurrency | **PASS** |
| **U-01** | `DocumentFilesController_EnforcesAuthorizeAttribute` | Controller Security | **PASS** |
| **U-02** | `CanAccessSourceFile_ActiveTechnicalReviewer_Allowed` | Authorization Engine | **PASS** |
| **U-03** | `CanAccessSourceFile_CompletedOrCancelledReviewer_Denied` | Authorization Engine | **PASS** |
| **U-04** | `CanAccessSourceFile_ActiveApprover_Allowed` | Authorization Engine | **PASS** |
| **U-05** | `CanAccessSourceFile_CompletedApprover_Denied` | Authorization Engine | **PASS** |
| **U-06** | `CanAccessSourceFile_ReviewerOrApproverForDifferentDoc_Denied` | Authorization Engine | **PASS** |
| **U-07** | `CanUploadOrReplaceDraftFile_ReviewerAndApprover_Denied` | Authorization Engine | **PASS** |
| **U-08** | `UploadRevisionFile_ValidDocxAndDoc_AcceptsAndCanonicalizesMimeType` | MIME & Versioning | **PASS** |
| **U-09** | `UploadRevisionFile_InvalidMagicBytesOrNonWord_ThrowsArgumentException` | File Format Validation | **PASS** |

**Focused WP3 Result:** `Passed: 28, Failed: 0, Skipped: 0 (100% Pass)`

#### 4.2 Full Regression Suite (896 Tests Total)
- **Execution Command:** `dotnet test backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj`
- **Total Tests:** 896
- **Passed:** 896
- **Failed:** 0
- **Skipped:** 0
- **Document Control Module Tests:** 271 of 271 passing
- **Other Modules (Pathogen, Selective Plating, Media, Preparation, Receiving, Inventory):** 625 of 625 passing
- **Duration:** 47.08 seconds

---

### 5. Production & Operational Safety Compliance

1. **User Accounts & Credentials:** Intact. No accounts or password hashes were modified.
2. **Audit History:** Intact. No historical audit records were modified or deleted. New audit action codes (`SourceFileDownloaded`, `SourceFileViewed`, `RevisionFileUploaded`, `RevisionFileReplaced`, `UnauthorizedFileAccessAttempted`, `FileIntegrityVerificationFailed`) were verified against the real PostgreSQL database.
3. **Database Schema:** 0 migrations created. WP2 database schema is completely sufficient.
4. **Physical File Integrity:** All stored documents on disk remain bit-identical. SHA-256 cryptographic verification prevents corrupted delivery.

---

### 6. Scope Compliance & Signoff

WP3 implementation is complete and verified with zero regression impact.

- **WP3 Scope Implemented:** Authorization & Word File Versioning.
- **Out of Scope (Preserved for Subsequent Work Packages):**
  - Multi-cycle review logic and finding resolution gates (Deferred to WP4).
  - Gotenberg DOCX-to-PDF controlled rendering worker (Deferred to WP5).
  - Frontend review and author correction workspaces (Deferred to WP6).
  - Frontend approval and release dossiers (Deferred to WP7).
  - Formal Qualification & GxP validation protocol (Deferred to WP8).

**WP3 STATUS: COMPLETE AND APPROVED — READY TO COMMENCE WP4.**
