# FS-AUD Functional Specification

| Field | Value |
|---|---|
| Document number | FS-AUD |
| Title | Audit trail functional specification |
| Version | 0.3 |
| Status | Draft |
| Author | Claude (AI agent), draft from code |
| Reviewer | To be assigned by QA |
| Approver | To be assigned by QA |
| Effective date | Set when Approved |

## Change history

| Version | Date | Author | Change |
|---|---|---|---|
| 0.1 | 2026-11-12 | Claude (AI agent), draft from code | First draft, written from the code as built. |
| 0.2 | 2026-11-13 | AI coding agent, draft from code | Added periodic audit trail review workflow and immutability triggers (FS-AUD-011, FS-AUD-012). |
| 0.3 | 2026-11-17 | AI coding agent, draft from code | Added table export log, bench draft source context and interface-settings exclusion specifications (FS-AUD-013 to FS-AUD-015). |

## Scope and reading notes

- There are two audit trails. The GxP audit trail is table `AuditLogs`. The security audit trail is table `SecurityAuditEvents`, with `LoginHistories`. Entry FS-AUD-010 covers reading the second one. Writing to it is described in FS-SEC-010.
- Automatic capture is in the database context (`MicroLimsDbContext`). Many services also add their own `AuditLog` rows by hand. For the same change, both an automatic row and a hand-made row can exist, for example for a user record in `UserService`. This was seen in the code and is stated in the entries that use it.
- The audit controller has no write endpoint except the export, which only reads.
- Nothing was run. Trigger and test names were read, not executed.

## Functions

### FS-AUD-001
**Satisfies:** URS-AUD-001
**Function:** Automatic capture on every save. `SaveChanges` and `SaveChangesAsync` in the database context first list every tracked entity that is Added, Modified or Deleted and is not an instance of a type in `AuditCaptureExcludedTypes`. If the list is empty, the save is a plain save. Otherwise, when no transaction is open on a relational database, the context opens one; when one is open, it uses it and does not open another. It saves the business change, adds one `AuditLog` row per listed entity, and saves again; then it commits the transaction it opened. For a new row, the record number is read after the first save so it is the real key; previous values for updates and deletes are read before the save. `EntityName` is the class name, `EntityId` is the `Id` property as text (or "unknown" if there is none), `Action` is "Create", "Update" or "Delete", the time comes from the injected `TimeProvider`, and `Reason` comes from the `CurrentReason` property set by the calling service. The user is `CurrentUserId` set from the token; with no user and no agent or process name the row records user 0. For an instrument agent or a named background process the row has no user, actor type System and a system process name. The row also fills the search columns (batch number, control number, sample reference, media lot, cryovial code, sample ID, test order ID) from properties with the matching names. Where the caller already holds a transaction (for example `UnitOfWork`), the audit rows join it and commit or roll back with it. If the audit row fails to save, the business change is not committed in the transaction the context opened. The method does not set the correlation number on the automatic rows.
**Code:** `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs` — `SaveChangesAsync`, `CapturePendingAuditEntries`, `AddAuditLogs`, `BuildAuditLog`
**Verified by:** reading (not run)
**Conflicts:** CONF-032, CONF-033

### FS-AUD-002
**Satisfies:** URS-AUD-002
**Function:** Redaction of secrets in automatic capture. `SerializeValues` writes each property of an audited entity into the previous/new value text; for a property marked `[AuditRedacted]` it writes the fixed text of `AuditRedactedAttribute.RedactedValue` instead of the value. The attribute is on the password hash of `User`, on the hash fields of `PasswordHistory`, `PasswordResetToken`, `AdminPasswordRecovery` and `InstrumentAgent`. Redaction is applied here, once, for all entities that reach automatic capture. It does not apply to `AuditLog` rows that a service builds by hand: those carry only the fields the service chooses. The entity `RefreshToken` is in the excluded list, so its token hash does not reach `AuditLogs`. No secret value is quoted in this document.
**Code:** `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs` — `SerializeValues`, `IsAuditRedacted`; `backend/MicroLIMS.Domain/Entities/User.cs` — `User`
**Verified by:** reading (not run)

### FS-AUD-003
**Satisfies:** URS-AUD-001
**Function:** Types that automatic capture skips. `AuditCaptureExcludedTypes` lists: `AuditLog`, `AuditEventChange`, `MaterialDocumentAccessLog`, `EquipmentDocumentAccessLog`, `Incident`, `ErrorLog`, `BackgroundJobRun`, `BackupRun`, `RefreshToken`, `NotificationLog`, `InstrumentIngestPayload`, `SecurityAuditEvent`, `SystemSetting`, `SystemSettingVersion`, `AccessReview` and `AccessReviewFinding`. Each has a comment giving the reason. `SystemSetting` is audited by hand in `SystemSettingsService`; the two access-review types are audited by hand in `AccessReviewService` (the second service was not read for this document). An instance of a subclass of a listed type is also skipped. Every other tracked type is captured, including `ElectronicSignature`, `User`, `Role` and `RolePermission`.
**Code:** `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs` — `AuditCaptureExcludedTypes`, `CapturePendingAuditEntries`
**Verified by:** reading (not run)

### FS-AUD-004
**Satisfies:** URS-AUD-004
**Function:** Append-only protection. The migration `AddAuditImmutabilityTriggers` creates a PostgreSQL trigger on `AuditLogs` that runs before update or delete; later migrations add no-truncate triggers and extend the protection to `AuditEventChanges`, `LoginHistories`, `SecurityAuditEvents`, `SystemSettingVersions`, `AccessReviews` and others. The test `AppendOnlyTriggerCatalogPostgresTests` holds the list of protected tables (`AuditLogs`, `AuditEventChanges`, `ElectronicSignatures`, `DocumentAcknowledgementRecords`, `WorkflowProfileVersions`, `InstrumentIngestPayloads`, `InstrumentIngestVersions`, `InstrumentIngests`, `LoginHistories`, `SecurityAuditEvents`, `SystemSettingVersions`, `AccessReviews`, `AccessReviewFindings`) and checks them after all migrations. `WorkflowHistories` is not in that list and no trigger for it was found in the migrations. The application has no service method that updates or deletes an `AuditLog`; the one reader that looks for correction rows (`AuditEntityIdCorrection`) only reads them. Not run.
**Code:** `backend/MicroLIMS.Persistence/Migrations/20260902181230_AddAuditImmutabilityTriggers.cs` — `Up`; `backend/MicroLIMS.Persistence/Migrations/20261027050929_HardenAppendOnlyAuditTables.cs` — `Up`; `backend/MicroLIMS.Tests/IntegrationTests/AppendOnlyTriggerCatalogPostgresTests.cs` — `ProtectedTables`
**Verified by:** reading (not run)
**Conflicts:** CONF-034

### FS-AUD-005
**Satisfies:** URS-AUD-003
**Function:** Named audit events (`RecordUserEventAsync`, `RecordSystemEventAsync`). Inputs: action code, category, record type, optional document IDs, reason, correlation number, source context, list of field changes (field name, previous, new), record ID, optional sample ID. The service takes the next number from sequence `audit_event_seq` and sets the event UID to "EVT-" plus seven digits. It adds an `AuditLog` with actor type User (user from `CurrentUserId`) or System (named process, no user), the injected-clock time, and one `AuditEventChange` child row per field change, then saves. The system-event method refuses a blank process name. Unlike automatic capture, these methods take the correlation number from the caller, and a caller that does not pass one leaves it empty. Saving here saves everything else pending in the same context.
**Code:** `backend/MicroLIMS.Application/Services/AuditEventService.cs` — `RecordUserEventAsync`, `RecordSystemEventAsync`
**Verified by:** reading (not run)

### FS-AUD-006
**Satisfies:** URS-AUD-005
**Function:** Search the audit trail (`POST api/admin/audit/search`; permission `Audit.View`, required on the whole controller). Input: `AuditSearchRequest`: from and to time, batch, control, media lot, sample reference, reference strain code, cryovial code, sample ID, test order ID, user ID, record type, action, action category, actor type, correlation number, reason text, a flag for negative record IDs, page size `Take` (default 200) and a cursor `BeforeId`. `ApplyFilters` ANDs each filter that is given: text filters are exact matches; the reason filter is a case-insensitive "contains". `Take` is limited to 1 to 1000 (`MaxTake`). Rows come newest first by ID; one extra row is read to set `HasMore`, and the next cursor is the ID of the last row. Each row is returned with the user's name, user name and current role name (the role today, not at the time of the action, as the DTO comment says), a flag when the record ID is negative (a temporary key was recorded) and the ID of a correction row if one exists. The query reads whole `AuditLog` rows into memory before mapping (this method does not call `AsNoTracking` and does not project columns in SQL). No laboratory-section scope check is made: any holder of `Audit.View` can search every row. In the first-run seed, the System Administrator and the Section Head hold `Audit.View`. The search is not itself audited.
**Code:** `backend/MicroLIMS.API/Controllers/AuditController.cs` — `Search`; `backend/MicroLIMS.Application/Services/AuditSearchService.cs` — `SearchPageAsync`, `ApplyFilters`, `ToDtosAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-035

### FS-AUD-007
**Satisfies:** URS-AUD-005
**Function:** History of one record (`GET api/admin/audit?entityName=…&entityId=…`; permission `Audit.View`). `GetForEntityAsync` returns every audit row with that record type and record ID text, newest first by time, with no page limit. No section scope check is made. Not audited.
**Code:** `backend/MicroLIMS.API/Controllers/AuditController.cs` — `Get`; `backend/MicroLIMS.Application/Services/AuditSearchService.cs` — `GetForEntityAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-035

### FS-AUD-008
**Satisfies:** URS-AUD-006
**Function:** Export to CSV (`POST api/admin/audit/export`; permission `Audit.View`). Input: the same `AuditSearchRequest`. `PrepareAsync` clears the cursor, counts the matching rows, limits the export to `MaxExportRows` rows (50,000, newest first) and sets a truncated flag if more match, fixes the newest row ID so rows added while streaming are not included, and then records an audit event `AuditExported` (category ImportExport, record type `AuditLog`) with the filter as JSON, the row count and the truncated flag, and saves it before any byte of the file is sent. `WriteCsvAsync` then streams rows in chunks of 1000 with a header line; times are written in ISO 8601 round-trip format; a text cell that starts with `=`, `+`, `-`, `@`, tab or carriage return gets a leading apostrophe (spreadsheet formula guard) and cells with commas, quotes or line breaks are quoted. If truncated, a last line says so. The response headers carry the row count and the truncated flag. The file name uses `DateTime.UtcNow` in the controller. The export includes previous and new values. No section scope check is made. The reasons for choosing the number 50,000 are not in the code.
**Code:** `backend/MicroLIMS.API/Controllers/AuditController.cs` — `Export`; `backend/MicroLIMS.Application/Services/AuditExportService.cs` — `PrepareAsync`, `WriteCsvAsync`, `Cell`
**Verified by:** reading (not run)
**Conflicts:** CONF-035

### FS-AUD-009
**Satisfies:** URS-AUD-007
**Function:** Traceability (`GET api/admin/audit/{auditLogId}/traceability` and `GET api/admin/audit/traceability?entityName=…&entityId=…`; permission `Audit.View`). From one audit row (or, for the entity form, the newest row of that record, or a direct lookup if none) the service picks a chain by the row's record type or reference columns and builds a list of nodes: sample and testing chain (source item, sample, test orders, results, reviews, signatures), media chain, material lot chain, cryovial and culture chain, equipment chain, and item set-up chain. It returns "not found" (404) when nothing can be built. The node building for each chain was read only at its entry points (which chain is chosen) and not line by line. No section scope check is made.
**Code:** `backend/MicroLIMS.API/Controllers/AuditController.cs` — `GetTraceability`, `GetTraceabilityForEntity`; `backend/MicroLIMS.Application/Services/AuditTraceabilityService.cs` — `GetTraceabilityAsync`, `GetTraceabilityForEntityAsync`, `BuildTraceabilityFromLogAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-035

### FS-AUD-010
**Satisfies:** URS-AUD-008
**Function:** Read the security audit trail. Two endpoints need both `Audit.View` (controller) and `System.ViewSecurityAudit` (action): `GET api/admin/audit/login-history` (filter by user name, `take` limited to 1 to 1000, cursor `beforeId`) and `GET api/admin/audit/security-events` (filter by event code, target user name, from and to time, `take`, cursor). Rows come newest first by ID. The security events include event code, outcome, actor user, whether the actor is the system, target user, correlation text, IP address, request path, source and reason; the user agent is stored but is not returned by this DTO. The writing of these rows is in FS-SEC-010. Both tables are listed as append-only in the trigger catalogue test (see FS-AUD-004). The security events are deliberately not in `AuditLogs`; the excluded-type comment says so.
**Code:** `backend/MicroLIMS.API/Controllers/AuditController.cs` — `GetLoginHistory`, `GetSecurityEvents`; `backend/MicroLIMS.Application/Services/AuditSearchService.cs` — `GetLoginHistoryPageAsync`, `GetSecurityEventsAsync`
**Verified by:** reading (not run)

### FS-AUD-011
**Satisfies:** URS-AUD-009
**Function:** Periodic audit trail review workflow (`AuditTrailReviewsController`, `AuditTrailReviewService`, `AuditTrailReviewsPage`, `AuditTrailReviewDetailPage`). Read endpoints (`GET /api/audit-trail-reviews`, `GET /{id}`, `GET /{id}/entries`, `GET /{id}/pdf`) require `Audit.View`; PDF export logs through `DataExportAuditService.LogExportAsync` and renders UTC timestamps, scope, cut-off, entry count, findings, and signature block (`"Audit trail review"` from `ReportVocabulary`). Mutating endpoints (`POST /`, `POST /{id}/findings`, `POST /{id}/sign` requiring `If-Match`, `POST /findings/{id}/close`) require `Audit.Review`. `StartAsync` validates `PeriodFromUtc < PeriodToUtc <= now`, captures `CutoffAuditLogId = MAX(AuditLogs.Id)` and `EntryCount`, and fixes scope (`All`, `EntityName`, or `Record`; `Section` is unused — `CONF-186`). `GetAsync` reports `NewerEntriesCount` (`Id > CutoffAuditLogId`). `AddFindingAsync` rejects entries outside the fixed scope, duplicates, or signed reviews. `SignAsync` checks `StartedByUserId == actingUserId`, `RecordVersion.EnsureCurrent`, conclusion (1–2000 chars), and `IElectronicSignatureService.SignAsync` (`SignatureMeaning.AuditTrailReviewed`) in one unit of work.
**Code:** `backend/MicroLIMS.API/Controllers/AuditTrailReviewsController.cs`; `backend/MicroLIMS.Application/Services/AuditTrailReviewService.cs` — `StartAsync`, `GetEntriesAsync`, `AddFindingAsync`, `SignCoreAsync`, `CloseFindingAsync`; `backend/MicroLIMS.Application/Services/AuditTrailReviewReportBuilder.cs` — `Build`; `backend/MicroLIMS.Domain/Enums/AuditTrailReviewEnums.cs`; `frontend/src/modules/auditReview/AuditTrailReviewsPage.tsx`; `frontend/src/modules/auditReview/AuditTrailReviewDetailPage.tsx`
**Verified by:** reading (not run)
**Conflicts:** CONF-186, CONF-187

### FS-AUD-012
**Satisfies:** URS-AUD-010
**Function:** Immutability and audit capture for audit trail reviews and findings. Migration `20261112220249_AddAuditTrailReviews` installs PostgreSQL triggers on `AuditTrailReviews` (reject `DELETE` and `TRUNCATE` always; reject `UPDATE` once `Status = 1` (`Signed`)) and `AuditTrailReviewFindings` (reject `DELETE` and `TRUNCATE` always; allow `UPDATE` only once from `Open` to `Closed` on the close fields). Both tables are registered in `AppendOnlyTriggerCatalogPostgresTests.ProtectedTables`, and neither `AuditTrailReview` nor `AuditTrailReviewFinding` is in `MicroLimsDbContext.AuditCaptureExcludedTypes`, so lifecycle saves are captured in `AuditLogs`.
**Code:** `backend/MicroLIMS.Persistence/Migrations/20261112220249_AddAuditTrailReviews.cs`; `backend/MicroLIMS.Domain/Entities/AuditTrailReview.cs`; `backend/MicroLIMS.Tests/IntegrationTests/AppendOnlyTriggerCatalogPostgresTests.cs` — `ProtectedTables`
**Verified by:** reading (not run)

### FS-AUD-013
**Satisfies:** URS-AUD-011
**Function:** Export log. `DataExportAuditService.LogListExportAsync` writes a `DataExportLog` row with the export type `ListCsv:<tableKey>`, the filter JSON (filters, columns and record identifiers), the row count and the user. It refuses a row count above 5,000 and a filter JSON whose identifiers do not match the row count. `ExportsController` (`POST api/exports/log`, policy `Data.Export`) calls it before the browser builds the file and returns the export identifier, the server time with its zone and the exporting user's name. `DataExportLog` is not on `AuditCaptureExcludedTypes`, so the automatic audit trail also records its insert.
**Code:** `backend/MicroLIMS.Application/Services/DataExportAuditService.cs`; `backend/MicroLIMS.API/Controllers/ExportsController.cs`; `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs`
**Verified by:** reading (not run); tests present: backend `ListExportPostgresTests.cs`
**Not verified:** the content of the audit row written for a list export (not run).

### FS-AUD-014
**Satisfies:** URS-AUD-012
**Function:** Draft start time as source context. The browser sends `X-Draft-Started-At` (ISO 8601 with an offset) only when the bench draft was started more than 5 minutes before the save (`useBenchDraft`, `submitHeaders`). `DraftContextMiddleware` runs in the audit pipeline after authentication (`UseMicroLimsAuditPipeline`). A header of at most 64 characters with an offset is converted to the text "Submitted from a device draft started at {yyyy-MM-dd HH:mm} UTC (device clock, not verified)" and set as the request's `CurrentSourceContext`. `MicroLimsDbContext` copies it into `AuditLog.SourceContext` for the audit rows of that request. A missing, malformed or long header changes nothing. The audit timestamp stays the server clock, and a future or very old device time is stored as stated, not corrected. The API's CORS policy allows the header.
**Code:** `backend/MicroLIMS.API/Middleware/DraftContextMiddleware.cs`; `backend/MicroLIMS.API/Extensions/ApplicationBuilderExtensions.cs`; `backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs`; `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs` (`CurrentSourceContext`, `BuildAuditLog`); `frontend/src/hooks/useBenchDraft.ts`
**Verified by:** reading (not run); tests present: backend `DraftContextPostgresTests.cs` (8 PostgresFact tests), `DraftContextMiddlewareTests.cs`
**Not verified:** that an explicit source context set by a service is never overwritten (the test was not read); the browser preflight (not run).

### FS-AUD-015
**Satisfies:** URS-AUD-013
**Function:** Audit exclusions for interface data. `MicroLimsDbContext.AuditCaptureExcludedTypes` lists `UserUiState` ("per-user interface preferences; no laboratory data (approved 2026-11-11)") and `RecentRecord` ("convenience list, not an access log (approved 2026-11-11)"). Saves of these types write no `AuditLogs` row. `TableView` and `UserTableDefault` are not on the list and are audited automatically.
**Code:** `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs` (`AuditCaptureExcludedTypes`); `backend/MicroLIMS.Domain/Entities/UserUiState.cs`; `backend/MicroLIMS.Domain/Entities/RecentRecord.cs`
**Verified by:** reading (not run); tests present: backend `UserUiStatePostgresTests.cs`, `RecentRecordPostgresTests.cs`
**Not verified:** whether a test asserts that these types write no audit rows (the tests were not read); the 2026-11-11 approval record itself (cited in the code comment, not read).
