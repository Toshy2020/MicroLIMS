# Work Package 1 (WP1) Completion Report: Document Control Foundation

**Module:** MicroLIMS Document Control (Release 1a)  
**Work Package:** WP1 — Persistence Foundation  
**Compliance Context:** GMP-Regulated (21 CFR Part 11, EU Annex 11, ALCOA+ Data Integrity)  
**Date of Completion:** September 2, 2026  
**Status:** Completed & Fully Verified  

---

## 1. Executive Summary

Work Package 1 (WP1) delivers the persistence foundation for Release 1a of the MicroLIMS Document Control module. It implements:
- An additive semantic event model on the shared audit trail.
- Database-level immutability triggers and privilege revocation scripts for `"AuditLogs"`, `"AuditEventChanges"`, and `"ElectronicSignatures"`.
- PostgreSQL database sequences (`document_number_seq` and `audit_event_seq`) and transaction-aware sequence retrieval helpers.
- Ten Document Control domain entities and enums following Clean Architecture and DDD principles.
- EF Core configurations enforcing `DeleteBehavior.Restrict`, unique constraints, and filtered indexes.
- Complete forward and rollback EF Core migrations.
- A PostgreSQL integration test fixture and comprehensive integration test suite.
- Comprehensive unit tests covering semantic event recording, user/system actor attribution, and ChangeTracker safety net verification.

**In-Scope Boundaries Adhered To:**
- **Zero API endpoints** delivered.
- **Zero frontend code or components** delivered.
- **Zero application services** delivered beyond `IAuditEventService` and `AuditEventService`.
- **No `IsDeleted` boolean** introduced (lifecycle state is managed strictly via status flags and mandatory audit reasons).
- **All timestamps** generated server-side using `DateTime.UtcNow`.
- **All foreign keys** configured with `DeleteBehavior.Restrict`.

---

## 2. Test Execution & Backward Compatibility Verification

### 2.1 Before and After Test Suite Counts

| Metric | Before WP1 | After WP1 | Net Change |
|---|---|---|---|
| **Total Tests** | 565 | 579 | +14 |
| **Passed** | 565 | 579 | +14 |
| **Failed** | 0 | 0 | 0 |
| **Skipped** | 0 | 0 | 0 |
| **Regressions** | N/A | None (0) | Clean |

### 2.2 Results of Nine PostgreSQL Integration Tests (`DocumentControlPostgresIntegrationTests`)

Executed against live PostgreSQL:
1. `AuditLogs_Update_ThrowsPostgresException`: **PASSED** — Proves PostgreSQL trigger blocks direct SQL `UPDATE` on `"AuditLogs"`.
2. `AuditLogs_Delete_ThrowsPostgresException`: **PASSED** — Proves PostgreSQL trigger blocks direct SQL `DELETE` on `"AuditLogs"`.
3. `AuditEventChanges_Update_ThrowsPostgresException`: **PASSED** — Proves PostgreSQL trigger blocks direct SQL `UPDATE` on `"AuditEventChanges"`.
4. `AuditEventChanges_Delete_ThrowsPostgresException`: **PASSED** — Proves PostgreSQL trigger blocks direct SQL `DELETE` on `"AuditEventChanges"`.
5. `ElectronicSignatures_Update_ThrowsPostgresException`: **PASSED** — Proves PostgreSQL trigger blocks direct SQL `UPDATE` on `"ElectronicSignatures"`.
6. `ElectronicSignatures_Delete_ThrowsPostgresException`: **PASSED** — Proves PostgreSQL trigger blocks direct SQL `DELETE` on `"ElectronicSignatures"`.
7. `DocumentNumberSequence_ProducesGaplessValuesUnderNormalLoad`: **PASSED** — Proves 10 sequential calls yield strictly incrementing numbers with step 1.
8. `DocumentNumberSequence_PreservesGapsOnRolledBackTransaction`: **PASSED** — Proves sequence drawn in an aborted transaction is permanently consumed and never reused/reseeded.
9. `UniqueCompanyCode_EnforcedAcrossActive_ReleasedAcrossVoid`: **PASSED** — Proves filtered unique index blocks duplicate active codes while permitting code reuse once the conflicting document record is marked `Void`.

### 2.3 Results of Unit Tests (`AuditEventServiceTests`)

1. `RecordUserEventAsync_PopulatesAllExpectedFields`: **PASSED** — Validates `EventUid` format (`EVT-0000001`), `ActorType = User`, `UserId`, `ActionCode`, `ActionCategory`, cross-references, context, and timestamp.
2. `RecordSystemEventAsync_PopulatesAllExpectedFields_WithoutUserId`: **PASSED** — Validates `ActorType = System`, `SystemProcessName`, explicit absence of `UserId` (null), and all semantic properties.
3. `RecordSystemEventAsync_ThrowsIfProcessNameMissing`: **PASSED** — Validates guard preventing un-attributed system events.
4. `RecordUserEventAsync_CapturesChildFieldChanges`: **PASSED** — Validates child `AuditEventChange` records capturing `FieldName`, `PreviousValue`, `NewValue`.
5. `ChangeTracker_SafetyNet_CapturesAuditLogForDocumentControlEntities`: **PASSED** — Proves shared `MicroLimsDbContext.CaptureAuditEntries` continues to capture automatic entity insert and update diffs on Document Control entities as an audit safety net.

---

## 3. Migration Forward Application & Rollback Verification

All migrations were tested forward and backward on PostgreSQL using `dotnet ef database update`:

1. **Rollback to Pre-WP1 Baseline (`20260902160520_AddDiscussionsAndMessagesV1`):**
   - Successfully dropped PostgreSQL sequences `document_number_seq` and `audit_event_seq`.
   - Successfully dropped triggers `trg_auditlogs_immutable`, `trg_auditeventchanges_immutable`, and `trg_electronicsignatures_immutable`, and trigger function `prevent_audit_modification()`.
   - Successfully dropped all 10 Document Control tables and restored `"AuditLogs"` schema.
   - Result: **Clean rollback, 0 errors**.
2. **Forward Application to Latest:**
   - Applied `20260902181229_AddDocumentControlEntities`.
   - Applied `20260902181230_AddAuditImmutabilityTriggers`.
   - Applied `20260902181231_AddDatabaseSequences`.
   - Result: **Clean migration, 0 errors**.

---

## 4. Exact Schema of New Tables, Sequences, and Triggers

### 4.1 Database Sequences

- `document_number_seq`:
  - Type: `bigint`
  - Start: `1`
  - Increment: `1`
  - Cycle: `NO CYCLE`
  - Purpose: Provides numeric portion for MicroLIMS Document IDs (`DOC-0000001`).
- `audit_event_seq`:
  - Type: `bigint`
  - Start: `1`
  - Increment: `1`
  - Cycle: `NO CYCLE`
  - Purpose: Provides numeric portion for audit event IDs (`EVT-0000001`).

### 4.2 Database Triggers & Function

- **Function:** `prevent_audit_modification()`
  - Language: `plpgsql`
  - Action: Raises exception `'Table % is append-only: UPDATE and DELETE operations are prohibited by GMP regulations (21 CFR Part 11, EU Annex 11).'`.
- **Triggers:**
  - `trg_auditlogs_immutable` ON `"AuditLogs"`: `BEFORE UPDATE OR DELETE FOR EACH ROW EXECUTE FUNCTION prevent_audit_modification()`.
  - `trg_auditeventchanges_immutable` ON `"AuditEventChanges"`: `BEFORE UPDATE OR DELETE FOR EACH ROW EXECUTE FUNCTION prevent_audit_modification()`.
  - `trg_electronicsignatures_immutable` ON `"ElectronicSignatures"`: `BEFORE UPDATE OR DELETE FOR EACH ROW EXECUTE FUNCTION prevent_audit_modification()`.

### 4.3 Table Schemas

#### 1. `"AuditEventChanges"`
- `Id` (int, PK, identity)
- `AuditLogId` (int, FK to `"AuditLogs"`, Restrict)
- `FieldName` (varchar(150), NOT NULL)
- `PreviousValue` (text, NULL)
- `NewValue` (text, NULL)

#### 2. `"AuditLogs"` (Extended Properties)
- `EventUid` (varchar(50), NULL, Indexed)
- `ActorType` (int, NULL)
- `SystemProcessName` (varchar(150), NULL)
- `ActionCode` (varchar(100), NULL)
- `ActionCategory` (int, NULL)
- `Reason` (varchar(2000), NULL)
- `SourceContext` (varchar(100), NULL)
- `CorrelationId` (uuid, NULL, Indexed)
- `DocumentMasterId` (int, NULL, Indexed)
- `DocumentRevisionId` (int, NULL, Indexed)

#### 3. `"DocumentMasters"`
- `Id` (int, PK, identity)
- `MicroLimsDocumentId` (varchar(50), NOT NULL, Unique Index)
- `CompanyDocumentCode` (varchar(100), NOT NULL, Filtered Unique Index where `"RecordStatus" = 1`)
- `Title` (varchar(500), NOT NULL)
- `DocumentTypeId` (int, FK to `"DocumentTypes"`, Restrict)
- `DepartmentId` (int, FK to `"DocumentDepartments"`, Restrict)
- `SectionId` (int, FK to `"DocumentSections"`, Restrict)
- `DocumentOwnerUserId` (int, FK to `"Users"`, Restrict)
- `Confidentiality` (int, NOT NULL)
- `Category` (varchar(100), NULL)
- `RecordOrigin` (int, NOT NULL, default Native)
- `RecordStatus` (int, NOT NULL, default Active)
- `CurrentEffectiveRevisionId` (int, NULL, FK to `"DocumentRevisions"`, Restrict)
- `CreatedByUserId` (int, FK to `"Users"`, Restrict)
- `CreatedAt` (timestamp with time zone, NOT NULL)
- `ModifiedByUserId` (int, NULL, FK to `"Users"`, Restrict)
- `ModifiedAt` (timestamp with time zone, NULL)
- `VoidedByUserId` (int, NULL, FK to `"Users"`, Restrict)
- `VoidedAt` (timestamp with time zone, NULL)
- `VoidReason` (varchar(2000), NULL)

#### 4. `"DocumentRevisions"`
- `Id` (int, PK, identity)
- `DocumentMasterId` (int, FK to `"DocumentMasters"`, Restrict)
- `RevisionNumber` (varchar(50), NOT NULL)
- `RevisionSequence` (int, NOT NULL, Indexed)
- `RevisionStatus` (int, NOT NULL)
- `EffectiveDate` (timestamp with time zone, NULL)
- `NextReviewDate` (timestamp with time zone, NULL)
- `ReviewCycleMonths` (int, NULL)
- `RevisionType` (int, NULL)
- `ReasonForRevision` (varchar(2000), NULL)
- `ChangeReference` (varchar(200), NULL)
- `RecordOrigin` (int, NOT NULL)
- `CreatedByUserId` (int, FK to `"Users"`, Restrict)
- `CreatedAt` (timestamp with time zone, NOT NULL)
- `CancelledByUserId` (int, NULL, FK to `"Users"`, Restrict)
- `CancelledAt` (timestamp with time zone, NULL)
- `CancelReason` (varchar(2000), NULL)
- Unique Index on `("DocumentMasterId", "RevisionNumber")`

#### 5. `"RevisionFiles"`
- `Id` (int, PK, identity)
- `DocumentRevisionId` (int, FK to `"DocumentRevisions"`, Restrict)
- `FileRole` (int, NOT NULL)
- `FileName` (varchar(255), NOT NULL)
- `ContentType` (varchar(100), NOT NULL)
- `SizeBytes` (bigint, NOT NULL)
- `ContentSha256` (varchar(64), NOT NULL)
- `StorageKey` (varchar(500), NOT NULL)
- `IsActive` (boolean, NOT NULL)
- `SupersededByFileId` (int, NULL, FK to `"RevisionFiles"`, Restrict)
- `UploadedByUserId` (int, FK to `"Users"`, Restrict)
- `UploadedAt` (timestamp with time zone, NOT NULL)
- Filtered Unique Index on `("DocumentRevisionId", "FileRole")` where `"IsActive" = true`

#### 6. `"DocumentTypes"`
- `Id` (int, PK, identity)
- `Code` (varchar(50), NOT NULL, Unique Index)
- `Name` (varchar(200), NOT NULL)
- `DefaultReviewCycleMonths` (int, NOT NULL)
- `IsActive` (boolean, NOT NULL)

#### 7. `"DocumentDepartments"`
- `Id` (int, PK, identity)
- `Code` (varchar(50), NOT NULL, Unique Index)
- `Name` (varchar(200), NOT NULL)
- `IsActive` (boolean, NOT NULL)

#### 8. `"DocumentSections"`
- `Id` (int, PK, identity)
- `Name` (varchar(200), NOT NULL)
- `DepartmentId` (int, FK to `"DocumentDepartments"`, Restrict)
- `IsActive` (boolean, NOT NULL)
- Unique Index on `("DepartmentId", "Name")`

#### 9. `"DocumentMasterAssignments"`
- `Id` (int, PK, identity)
- `DocumentMasterId` (int, FK to `"DocumentMasters"`, Restrict)
- `UserId` (int, FK to `"Users"`, Restrict)
- `AssignmentRole` (int, NOT NULL)
- `IsActive` (boolean, NOT NULL)
- `AssignedByUserId` (int, FK to `"Users"`, Restrict)
- `AssignedAt` (timestamp with time zone, NOT NULL)
- Index on `("DocumentMasterId", "UserId", "AssignmentRole")`

#### 10. `"DocumentKeywords"`
- `Id` (int, PK, identity)
- `DocumentMasterId` (int, FK to `"DocumentMasters"`, Restrict)
- `Keyword` (varchar(100), NOT NULL)
- Index on `("DocumentMasterId", "Keyword")`

#### 11. `"DocumentNumberingConfigurations"`
- `Id` (int, PK, identity)
- `Prefix` (varchar(50), NOT NULL)
- `NumberFormat` (varchar(50), NOT NULL)
- `IsEnabled` (boolean, NOT NULL)
- `ModifiedByUserId` (int, FK to `"Users"`, Restrict)
- `ModifiedAt` (timestamp with time zone, NOT NULL)

#### 12. `"ConfigurationSettings"`
- `Id` (int, PK, identity)
- `SettingKey` (varchar(100), NOT NULL, Unique Index)
- `SettingValue` (varchar(2000), NOT NULL)
- `DataType` (varchar(50), NOT NULL)
- `SettingGroup` (varchar(100), NOT NULL)
- `ModifiedByUserId` (int, NULL, FK to `"Users"`, Restrict)
- `ModifiedAt` (timestamp with time zone, NOT NULL)
- Seeded keys: `System.TimeZone`, `DocumentControl.ControlledCopy.DownloadPermitted`, `DocumentControl.ControlledCopy.PrintPermitted`, `DocumentControl.Watermark.OnDownload`.

---

## 5. Deployment Privilege Revocation Script

As required by Task 2, privilege management is treated as an Installation Qualification (IQ) deployment activity and is NOT executed during application boot or migrations. The script has been delivered to:
- [`./docs/sql/revoke_audit_privileges.sql`](file:///E:/MicroLIMS/MicroLIMS/docs/sql/revoke_audit_privileges.sql)

It contains explicit `REVOKE UPDATE, DELETE` directives on `"AuditLogs"`, `"AuditEventChanges"`, and `"ElectronicSignatures"` for the application database user, alongside IQ header comments.

---

## 6. Architecture & Out-of-Scope Compliance Sign-Off

1. **No Out-of-Scope Code:** Confirmed by file inventory that no controllers, endpoints, or UI components were created.
2. **User Reference Security:** All 11 new `UserId`-referencing properties were registered under `UserReferenceDisposition.Blocks` with `DB FK Restrict` in `UserReferenceRegistry.cs`, verified by `UserReferenceModelScanTests`.
3. **Clean Architecture Isolation:** Domain layer has zero dependencies on EF Core or infrastructure; persistence configurations and migrations cleanly isolate database specifics.
4. **All Changes Additive & Backward Compatible:** Zero broken contracts across existing workflows or unit tests.
