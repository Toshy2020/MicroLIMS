# Functional Requirements Specification (FRS) — Release 1a

**Document Number:** ML-DC-FRS-1A-001  
**Title:** MicroLIMS Document Control Module — Functional Requirements Specification (Release 1a)  
**Version:** 1.0  
**Status:** Approved Baseline  
**Release Scope:** Release 1a — Master Document Register, Controlled Files, Document Library & Details, Core Configuration, Audit Trail  
**Applicable Regulations:** GAMP 5, 21 CFR Part 11, EU Annex 11, ALCOA+ Principles  
**Authoritative Baseline:** MicroLIMS Document Control URS v1.1 (URS v1.0 + Amendment v1.1, Requirements DC-URS-001 through DC-URS-203)  
**Date:** September 2, 2026  

---

## 1. Document Control & Governance

### 1.1 Revision History
| Version | Date | Author | Description of Change |
|---|---|---|---|
| 0.1 | 06-Aug-2026 | Document Control Team | Initial Draft for Review (v0.1) |
| 1.0 | 02-Sep-2026 | Lead System Architect | Formal Baseline v1.0 following WP1 completion and preparation for WP2 Application/API layer. Incorporates WP1 persistence schema, sequence helpers, audit triggers, and URS v1.1 203-requirement trace. |

### 1.2 Sign-off & Controlled Approvals
| Role | Title | Verification Mandate |
|---|---|---|
| **Author** | Senior Software Engineer / Architect | Technical design & architecture alignment |
| **System Owner** | Quality Control Microbiology Lead | Business operational suitability |
| **Quality Assurance** | Lead Quality Assurance / CSV Specialist | 21 CFR Part 11 & GAMP 5 regulatory compliance |
| **Technical Reviewer**| IT Technical Architect | Security, persistence, and performance alignment |

---

## 2. Purpose and Release Scope

### 2.1 Purpose
This Functional Requirements Specification (FRS) defines the intended system behaviour, business rules, inputs, validations, permissions, audit requirements, and error handling for **Release 1a** of the MicroLIMS Document Control module. It serves as the authoritative blueprint for the Application/API layer (WP2), Frontend layer (WP3/WP4), and the formal qualification protocol (IQ/OQ/PQ).

### 2.2 Release 1a In-Scope Functional Areas
1. **Document Master Register & Revisions:** Permanent document identity, organizational classification, revision registration, draft authoring, and lifecycle management.
2. **Controlled File Handling:** Upload, storage key generation, SHA-256 cryptographic hashing at upload, SHA-256 integrity verification upon retrieval, prevention of overwrite, and active file constraints.
3. **Document Library & Details:** Server-side search, filtering, sorting, pagination, master record details, revision history, and controlled file download/viewing.
4. **Core Configuration:** Configurable document types, departments, sections, document numbering prefix/format, and key/value system settings (system time zone, controlled-copy policies).
5. **Semantic Audit Trail & Immutability:** Additive semantic audit properties, record-specific audit history, global audit log query, database-level trigger immutability, and IQ privilege revocation.
6. **Controlled Cancellation and Voiding:** Prohibition of physical delete, draft cancellation with mandatory reason, and error voiding of never-effective masters with permanent sequence retirement.
7. **Per-Document Workflow Authority Foundations:** Capturing and maintaining per-document assignments (Owner, Author, Technical Reviewer, Approver) in combination with global role RBAC.

### 2.3 Out-of-Scope (Deferred to Later Releases)
- **Release 1b:** Create New Revision workflow, technical review, formal approval, 21 CFR Part 11 electronic approval signatures, automatic effective-date transitions, and periodic review workflows.
- **Release 1c:** Reading assignments, My Reading List, Training Matrix, and Document Control KPI reports.
- **Release 1d:** Knowledge Assessment Form (KAF) engine, Question Bank, and quiz results.
- **Release 1e:** Migration/Import Wizard and legacy data reconciliation.
- **Future Integration (DC-URS-201 through DC-URS-203):** Direct automated invocation from laboratory testing workspaces (Testing, Water, EM, Media, Receiving).

### 2.4 Scope Boundary Rule (FS-1a-105 / DC-URS-161, DC-URS-162, DC-URS-163)
Release 1a shall not expose any interface, event, or data path through which a controlled document or training status influences laboratory execution modules. Laboratory execution modules shall have zero dependencies on Document Control entities, preserving Phase 1 independence.

---

## 3. Reference Architecture & Standards

- **Clean Architecture Layers:**
  - `MicroLIMS.Domain`: Core entities (`DocumentMaster`, `DocumentRevision`, `RevisionFile`, `DocumentType`, `DocumentDepartment`, `DocumentSection`, `DocumentNumberingConfiguration`, `ConfigurationSetting`, `AuditEventChange`), business enums, and domain invariants.
  - `MicroLIMS.Shared`: DTOs, request/response models, validation rules, constants, and `ApiResponse<T>`.
  - `MicroLIMS.Application`: Application services, workflow guards, file validators, interfaces (`IAuditEventService`, `IDocumentMasterService`, `IDocumentFileService`, `IDocumentConfigurationService`).
  - `MicroLIMS.Infrastructure`: File storage (`LocalFileStorageService`), PDF generation, cryptography, token management.
  - `MicroLIMS.Persistence`: `MicroLimsDbContext`, EF configurations (`DeleteBehavior.Restrict`), DB triggers, DB sequences, seeders.
  - `MicroLIMS.API`: Thin controllers returning `ApiResponse<T>`, global exception filters, audit middleware.
- **Data Integrity Standards:**
  - **No `IsDeleted` Boolean:** Record removal is strictly managed via status (`Void`, `Cancelled`) with mandatory documented reason.
  - **UTC Timestamps:** All timestamps generated server-side using `DateTime.UtcNow`.
  - **File Immutability:** Files stored with server-derived storage keys (`documents/{revisionId}/{guid}.pdf`), hashed via SHA-256 on ingest, and verified bit-for-bit upon retrieval.

---

## 4. Role-Based Access Control & Per-Document Authority (FS-1a-110, FS-1a-111)

### 4.1 Global Role Authorization Matrix (Release 1a)
| Operation / Endpoint Group | System Administrator | Document Controller | Document Author | QA Viewer / Auditor | General Reader |
|---|---|---|---|---|---|
| **View Library & Details** | Yes | Yes | Yes | Yes | Yes |
| **Download / View Controlled PDF** | Yes | Yes | Yes | Yes | Yes (Effective only) |
| **Download / View Source DOCX** | Yes | Yes | Own Docs Only | No | No |
| **Register New Document Master** | Yes | Yes | Yes | No | No |
| **Update Draft Master Metadata** | Yes | Yes | Own Docs Only | No | No |
| **Upload / Replace Draft File** | Yes | Yes | Own Docs Only | No | No |
| **Cancel Draft Revision** | Yes | Yes | Own Docs Only | No | No |
| **Void Document Master** | No (Quality Only) | Yes | No | No | No |
| **Configure System / Master Data** | Yes | No | No | No | No |
| **Query Global Audit Trail** | Yes | Yes | No | Yes | No |
| **View Record Audit History** | Yes | Yes | Yes | Yes | No |

*Design Decision (FS-1a-111):* Document Master voiding is strictly reserved for the **Document Controller** and excluded from System Administrator to enforce quality segregation of duties. "Own Docs" means the user is either the record's `DocumentOwnerUserId` or holds an active `Author` assignment in `DocumentMasterAssignments`.

---

## 5. Detailed Release 1a Functional Requirements Specification

The following table provides the complete requirement-by-requirement specification for Release 1a, traceable to URS v1.1.

| FRS ID | URS ID | Functional Requirement | Functional Behavior & Business Rules | Validation / Input Rules | Authorization Rules | Audit Requirements | Error Handling | Verification Method | Implementation Status |
|---|---|---|---|---|---|---|---|---|---|
| **FS-1a-100** | DC-URS-001<br>DC-URS-002 | Master Document Register & Module Foundation | Provide a centralized, dedicated Document Master register in MicroLIMS managing permanent document identities, types, departments, and metadata. | Clean Architecture domain model; distinct tables for masters, revisions, and files. | All authenticated users can view active register. | Audit trail captures creation and state changes. | Database integrity exceptions abort transactions. | Test suite verification. | **IMPLEMENTED IN WP1** (Entities & DbContext) |
| **FS-1a-120** | DC-URS-003 | Separation of Company Code & System ID | Preserve organization-issued `CompanyDocumentCode` as a separate field from `MicroLimsDocumentId`. | Both fields stored independently on `DocumentMaster`. `CompanyDocumentCode` is editable only in draft/initial registration; `MicroLimsDocumentId` is permanent and immutable. | `CompanyDocumentCode`: mandatory, 1–100 chars, trimmed, case-insensitive unique across active masters (`RecordStatus = Active`). | Changes logged with previous and new values. | Duplicate active code throws 409 Conflict. | Unit & Integration tests (`UniqueCompanyCode_EnforcedAcrossActive_ReleasedAcrossVoid`). | **IMPLEMENTED IN WP1** (Schema/Index) / **TO BE IMPLEMENTED IN WP2** (API/Validation) |
| **FS-1a-121** | DC-URS-004<br>DC-URS-005<br>DC-URS-136<br>DC-URS-137 | MicroLIMS Document ID Generation | System generates permanent, unique `MicroLimsDocumentId` from sequence `document_number_seq` (e.g. `DOC-0000001`). Value never changes across revisions. | Number drawn in registration transaction. Sequence consumed in rolled-back tx is never reused (gaps allowed). Permanent across all revisions. | Prefix and format configured in `DocumentNumberingConfiguration`. Cannot be edited manually. | ID captured on master registration audit event. | Sequence failure rolls back registration. | Integration test (`DocumentNumberSequence_*`). | **IMPLEMENTED IN WP1** (Sequence/Helper) / **TO BE IMPLEMENTED IN WP2** (Application Service) |
| **FS-1a-122** | DC-URS-006 | Document Library Search & Filter | Search and filter active documents by company code, MicroLIMS ID, title, type, department, section, owner, status, origin. | Server-side query execution using LINQ; server-side pagination and sorting. Cancelled/Voided records excluded by default unless explicitly requested. | Max page size 100. Text search terms trimmed. Filter IDs must exist if specified. | Read-only queries not audited. | Invalid filter inputs return 400 Bad Request. | Unit tests on query builder and pagination. | **TO BE IMPLEMENTED IN WP2** |
| **FS-1a-123** | DC-URS-007<br>DC-URS-133 | Configurable Document Types | Maintain document types (SOP, Protocol, Work Instruction, Form, Template, Specification) with default review cycle. | Admin can create, update, and deactivate types. Deactivation removes from selection lists without impacting existing documents. Deletion blocked. | `Code` (unique, 1–50 chars), `Name` (1–200 chars), `DefaultReviewCycleMonths` (1–120 months). | System Administrator role only. | Capture previous and new values on update/deactivate. | Inactive type cannot be used for new documents. | Unit & Integration tests. | **IMPLEMENTED IN WP1** (Entity/Seed) / **TO BE IMPLEMENTED IN WP2** (Service/Controller) |
| **FS-1a-124** | DC-URS-008<br>DC-URS-134 | Configurable Departments and Sections | Maintain Document Departments and child Document Sections for document classification. | Admin can create, update, and deactivate departments and sections. Deletion blocked. Sections must belong to valid department. | Department `Code` (unique, 1–50 chars), `Name` (1–200 chars). Section `Name` (unique within Department). | System Administrator role only. | Capture changes in audit log. | Cannot assign section from different department. | Unit & Integration tests. | **IMPLEMENTED IN WP1** (Entity/Schema) / **TO BE IMPLEMENTED IN WP2** (Service/Controller) |
| **FS-1a-125** | DC-URS-193 | Record Origin Attribution | Every DocumentMaster, Revision, and File must carry `RecordOrigin` (`Native = 1`, `Migrated = 2`). | Records created via Register New Document are set to `Native`. Immutable once written. | Valid `RecordOrigin` enum value. | Captured on registration audit event. | Cannot modify origin after creation. | Unit test verifying immutability. | **IMPLEMENTED IN WP1** (Entity/Enum) / **TO BE IMPLEMENTED IN WP2** (Service Enforcement) |
| **FS-1a-126** | DC-URS-009<br>DC-URS-130 | Document Master Metadata & Details | Maintain complete metadata for Document Master: title, type, department, section, owner, confidentiality, category, keywords, assignments. | Retrieve master record with child collections (current revision, revision history, assignments, keywords). | Mandatory fields: Title, TypeId, DepartmentId, SectionId, OwnerUserId, Confidentiality. | Authenticated users can view details. | Read access logged for restricted/confidential documents if required. | 404 if master ID not found. | Integration test for Details DTO projection. | **TO BE IMPLEMENTED IN WP2** |
| **FS-1a-127** | DC-URS-010 | Identification of Current Effective Revision | Explicitly identify current effective revision via `CurrentEffectiveRevisionId` pointer on `DocumentMaster`. | For Release 1a, newly registered documents remain in `Draft` state; `CurrentEffectiveRevisionId` is `null` until Release 1b approval. | Value must match a valid `DocumentRevision.Id` belonging to the same master. | Read-only in Release 1a. | Inconsistent pointer rejected. | Unit test checking nullability and association. | **IMPLEMENTED IN WP1** (Entity Schema) / **TO BE IMPLEMENTED IN WP2** (Service Query) |
| **FS-1a-128** | DC-URS-011<br>DC-URS-012 | Register New Document Master & Initial Draft | Create new `DocumentMaster` and initial `DocumentRevision` in `Draft` state. | In a single transaction: draw `MicroLimsDocumentId`, insert master, insert initial revision (`RevisionNumber = "01"` or user-specified), add keywords/assignments. | Mandatory: CompanyCode, Title, TypeId, DepartmentId, SectionId, OwnerUserId. Initial revision number required. | System Admin, Document Controller, or Author. | Audit event `DocumentMasterRegistered` with full field capture. | 400 on validation failure; rollback on error. | Service unit test & API integration test. | **TO BE IMPLEMENTED IN WP2** |
| **FS-1a-129** | DC-URS-021<br>DC-URS-022<br>DC-URS-023 | Controlled File Storage & Revision Binding | Support storage of controlled PDF (`FileRole.ControlledPdf = 1`) and optional source file (`FileRole.SourceFile = 2`) bound to exact revision. | Files stored via `IFileStorageService` with server-generated key (`documents/{revisionId}/{guid}.ext`). Max 1 active file per role per revision. | File size <= 50MB (configurable). PDF content verified by signature. Filename sanitized. | Upload: Admin, Doc Controller, or Author (own doc). | Audit event `RevisionFileUploaded` with file name, size, hash. | Mismatch or invalid format returns 400 Bad Request. | Unit test with mock file storage. | **IMPLEMENTED IN WP1** (Schema) / **TO BE IMPLEMENTED IN WP2** (Service/API) |
| **FS-1a-161** | DC-URS-198<br>DC-URS-199<br>DC-URS-200 | Cryptographic Hash & Retrieval Verification | Compute SHA-256 hash at upload; verify SHA-256 hash upon every retrieval. Block delivery and log audit event on mismatch. | Hash calculated via `SHA256.HashData`. On download, recalculate hash of stored bytes; if computed != stored, throw `IntegrityFailureException` and log. | Hash string 64 hex chars. | Audit event `FileIntegrityVerificationFailed` raised on mismatch. | HTTP 500 / Integrity Failure returned; no corrupted file served. | Integration test with corrupted file bytes. | **TO BE IMPLEMENTED IN WP2** |
| **FS-1a-162** | DC-URS-024<br>DC-URS-025 | Prevention of File Overwrite and Silent Replacement | Files cannot be overwritten. In `Draft`, uploading replacement supersedes previous active file (`IsActive = false`, `SupersededByFileId`). In non-draft, replacement blocked. | File store is append-only. Old file row and physical bytes preserved indefinitely. Non-draft revision file replacement rejected. | Revision must be in `Draft` state to replace file. | Audit event `RevisionFileReplaced` capturing old and new file details. | Attempted replacement on non-draft returns 422 Unprocessable Entity. | Integration test for draft replacement and non-draft rejection. | **TO BE IMPLEMENTED IN WP2** |
| **FS-1a-163** | DC-URS-028<br>DC-URS-029 | File Access & Controlled Copy Policy | Separate permissions for source (editable) file vs controlled PDF. Backend enforces controlled-copy download/print policy. | Source file accessible only to Admin, Doc Controller, or Author (own doc). Download/print headers enforced per `ConfigurationSettings`. | User permissions evaluated on file retrieval endpoint. | File download logged in audit trail. | 403 Forbidden if user lacks source file access. | Unit & Integration tests for access control. | **TO BE IMPLEMENTED IN WP2** |
| **FS-1a-170** | DC-URS-184 | Prohibition of Permanent Deletion | No application function shall permanently delete a `DocumentMaster`, `DocumentRevision`, `RevisionFile`, configuration, or audit record. | EF Core configurations specify `DeleteBehavior.Restrict`. API controllers expose NO delete endpoints for controlled records. | Any attempt to call DB delete throws exception. | Immutability triggers on audit tables prevent DB-level deletes. | Deletion request returns 405 Method Not Allowed / 400 Bad Request. | Integration tests verifying absence of delete & restrict behavior. | **IMPLEMENTED IN WP1** (Triggers/FKs) / **TO BE IMPLEMENTED IN WP2** (API Design) |
| **FS-1a-171** | DC-URS-185 | Cancelling a Draft Revision | Cancel a revision in `Draft` state with mandatory reason. | Revision status moves to `Cancelled`. Revision and files become read-only and excluded from active work. Cannot be uncancelled. | Reason mandatory (min 10 chars). Revision must be in `Draft`. | Admin, Doc Controller, or Author (own doc). | Audit event `DraftRevisionCancelled` with reason and previous status. | Rejection if reason < 10 chars or status != Draft. | Unit & Integration tests. | **TO BE IMPLEMENTED IN WP2** |
| **FS-1a-172** | DC-URS-186<br>DC-URS-187<br>DC-URS-188 | Voiding a Document Master | Void a `DocumentMaster` registered in error with mandatory reason. | Record status set to `Void`. Master has never held an effective revision. `MicroLimsDocumentId` permanently retired. `CompanyDocumentCode` released for reuse. | Reason mandatory (min 10 chars). Master must have no effective revision. | **Document Controller role only** (Admin blocked). | Audit event `DocumentMasterVoided` with user, timestamp, reason. | 403 if called by non-Document Controller; 400 if document has effective revision. | Integration test (`UniqueCompanyCode_EnforcedAcrossActive_ReleasedAcrossVoid`). | **IMPLEMENTED IN WP1** (Test 9) / **TO BE IMPLEMENTED IN WP2** (Service/Controller) |
| **FS-1a-180** | DC-URS-123<br>DC-URS-124<br>DC-URS-125<br>DC-URS-126<br>DC-URS-131<br>DC-URS-132 | Semantic Audit Trail Capturing | All Document Control actions capture structured semantic event details. | Calls `IAuditEventService.RecordUserEventAsync` or `RecordSystemEventAsync` with `EventUid`, `ActorType`, `ActionCode`, `Changes`, `Reason`, `CorrelationId`. | Required fields: ActionCode, ActionCategory, EntityName, EntityId. | System-generated actions use `ActorType = System` without `UserId`. | Automatic logging via service; failures roll back business tx. | Exception thrown if mandatory parameters missing. | Unit tests in `AuditEventServiceTests`. | **IMPLEMENTED IN WP1** (Service/Schema) / **TO BE IMPLEMENTED IN WP2** (Integration in Services) |
| **FS-1a-181** | DC-URS-129<br>DC-URS-130 | Audit Trail Queries (Global & Record-Specific) | Search global audit trail and retrieve record-specific audit history for a master/revision. | Paged query by date range, user, action category, record type, master ID, revision ID, event UID. Returns structured event details with changes. | Paging parameters (page, pageSize <= 100). | Global: Admin, Doc Controller, Auditor. Record-specific: includes Author. | Read queries; export action raises audit event `AuditTrailExported`. | 400 on invalid date range; 403 on unauthorized access. | Integration tests for audit queries. | **TO BE IMPLEMENTED IN WP2** |
| **FS-1a-182** | DC-URS-127<br>DC-URS-128<br>DC-URS-152 | Persistence Immutability of Audit Records | Audit logs, event changes, and electronic signatures cannot be updated or deleted. | DB triggers `trg_auditlogs_immutable`, `trg_auditeventchanges_immutable`, `trg_electronicsignatures_immutable` raise exception on `UPDATE`/`DELETE`. | Runtime user lacks UPDATE/DELETE privileges. | Enforced at DB engine level. | Attempt throws `PostgresException`. | Integration tests (Tests 1–6). | **IMPLEMENTED IN WP1** (Triggers & IQ Script) |
| **FS-1a-195** | DC-URS-151<br>DC-URS-182 | Time Handling & System Time Zone | All timestamps generated server-side via `DateTime.UtcNow`. Single system time zone in `ConfigurationSetting` used for display. | Client-supplied timestamps never accepted for controlled records. System time zone key: `System.TimeZone`. | Valid IANA or Windows time zone ID. | Changing time zone setting raises audit event. | Rejection of invalid time zone. | Unit test for server timestamp generation. | **IMPLEMENTED IN WP1** (Conventions/Seed) / **TO BE IMPLEMENTED IN WP2** (Service Enforcement) |
| **FS-1a-135** | DC-URS-135 | Default Review Cycles Configuration | Configurable default review cycle per document type. | Setting on `DocumentType.DefaultReviewCycleMonths`. Defaults to 24 or 36 months; applied to new revisions. | Integer 1 to 120. | System Administrator only. | Audit event on update. | Value outside range rejected. | Unit test. | **IMPLEMENTED IN WP1** (Entity) / **TO BE IMPLEMENTED IN WP2** (Service/Controller) |
| **FS-1a-168** | DC-URS-168 | Document Master Workflow Assignments Foundation | Maintain per-document assignments: Owner, Author, Technical Reviewer, Approver in `DocumentMasterAssignment`. | Captures assigned users per role. Editable by Document Controller and Admin in Release 1a. Foundation for Release 1b enforcement. | Role in (`AssignmentRole.Owner`, `Author`, `TechnicalReviewer`, `Approver`). User must be active. | Document Controller or Admin. | Audit event `WorkflowAssignmentChanged`. | Duplicate active assignment for same user and role rejected. | Unit & Integration tests. | **IMPLEMENTED IN WP1** (Entity/Schema) / **TO BE IMPLEMENTED IN WP2** (Service/API) |

---

## 6. Summary of Non-Release 1a Requirements (Deferred Baseline)

The following requirements are formally documented as out-of-scope for Release 1a and scheduled for subsequent releases:

- **Release 1b (Review, Approval, e-Signatures, Periodic Review):**
  - `DC-URS-013` to `DC-URS-020`: Revision creation, change description, review cycle calculation.
  - `DC-URS-031` to `DC-URS-060`: Technical review submission, approval workflows, Part 11 e-signatures, automatic effective date transition, supersession of previous revision, periodic review scheduling and notifications.
  - `DC-URS-164` to `DC-URS-167`: Segregation of duties enforcement (author cannot review or approve own document; reviewer cannot approve).
- **Release 1c (Training & Reading Lists):**
  - `DC-URS-061` to `DC-URS-100`: Document training assignment rules, My Reading List, training completion tracking, Training Matrix, and KPI reporting.
- **Release 1d (Knowledge Assessment Forms - KAF):**
  - `DC-URS-101` to `DC-URS-122`: KAF authoring, question bank, passing score evaluation, randomized question delivery, and attempt audit tracking.
- **Release 1e (Legacy Migration Wizard):**
  - `DC-URS-189` to `DC-URS-192`, `DC-URS-194` to `DC-URS-197`: Migration batch import, legacy document loading, metadata mapping, reconciliation reporting.
- **Future Integration Project (Laboratory Workspaces):**
  - `DC-URS-201` to `DC-URS-203`: Automated retrieval of effective SOP revisions and analyst qualification status from testing workspaces (deferred to future controlled change).

---

## 7. FRS Verification & Acceptance Criteria
WP2 is complete and verified when:
1. Every requirement marked **TO BE IMPLEMENTED IN WP2** has corresponding domain/application logic, DTOs, validation, and API endpoints.
2. All business invariants (code uniqueness, sequence permanence, SHA-256 retrieval verification, draft file replacement, cancellation reasons, and RBAC authorization) are proven by focused unit and integration tests.
3. Full test regression suite passes with 0 failures and 0 warnings.
