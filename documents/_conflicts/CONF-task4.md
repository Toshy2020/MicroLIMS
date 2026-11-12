# Conflicts raised by Task 4 (areas SIG, AUD, SEC, ADM)

IDs CONF-030 to CONF-059 belong to this file. The orchestrator copies them into `documents/Conflicts.md`. Each entry states what the code does and what the other source says. No entry takes a side. None of CONF-001 to CONF-008 falls in these four areas.

### CONF-030
- **Area:** SIG
- **Status:** Open
- **What the code does:** A signature is tied to its record only by the entity type text and the entity ID number. The signature row holds no record version, no version of the specification, and no hash or copy of the signed content. Location: `backend/MicroLIMS.Domain/Entities/ElectronicSignature.cs` — `ElectronicSignature` (properties `EntityType`, `EntityId`); `backend/MicroLIMS.Application/Services/ElectronicSignatureService.cs` — `SignAsync`. Callers sign before they change the record (for example `SystemSettingsService.UpdateCoreAsync` signs with the setting row ID), so the signature points at the record, not at the state that was signed.
- **What the other source says:** CLAUDE.md line 115 (Rule 3.3, item 5): the signature must be permanently bound to the exact signed record version so it cannot be copied or transferred to a later version.
- **Question:** Is binding by entity type and ID enough for QA, or must each signature also carry the version or a content hash of the record it signs?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-031
- **Area:** SIG
- **Status:** Open
- **What the code does:** The signing time (`SignedAt`) and the time of the failed-attempt and throttled rows are not set by the signature service from the injected clock. `SignedAt` takes the entity default `DateTime.UtcNow`; the two `AuditLog` rows made in `SignAsync` set no `Timestamp`, so they take the `AuditLog` default `DateTime.UtcNow`. The throttle window start in the same method is taken from the injected clock (`_time`). Location: `backend/MicroLIMS.Domain/Entities/ElectronicSignature.cs` — `SignedAt`; `backend/MicroLIMS.Domain/Entities/AuditLog.cs` — `Timestamp`; `backend/MicroLIMS.Application/Services/ElectronicSignatureService.cs` — `SignAsync`.
- **What the other source says:** CLAUDE.md line 109 (Rule 3.3, item 2) and line 194 (Rule 5.6): audit and regulated timestamps come from the injected clock; the legacy entity defaults are allowed only as a capped legacy item and new ones must not be added.
- **Question:** Should the signature service set `SignedAt` and the failed-attempt row times from the injected clock, so that the throttle window and the stored times use one clock?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-032
- **Area:** AUD
- **Status:** Open
- **What the code does:** The automatic audit rows do not set `CorrelationId`. `BuildAuditLog` fills user, time, reason and the search columns but not the correlation number. Hand-made audit rows in `UserService`, `RoleService`, `AdminPasswordRecoveryService` and `SystemSettingsService` also leave it empty. Only `AuditEventService` takes a correlation number, and only if the caller passes one. The request correlation text made by `CorrelationIdMiddleware` is a different value, and its comment says it is unrelated to `AuditLog.CorrelationId`. Location: `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs` — `BuildAuditLog`; `backend/MicroLIMS.Application/Services/AuditEventService.cs` — `RecordUserEventAsync`; `backend/MicroLIMS.API/Middleware/CorrelationIdMiddleware.cs`.
- **What the other source says:** CLAUDE.md line 109 (Rule 3.3, item 2): every regulated mutation must record a `CorrelationId`.
- **Question:** Should automatic capture fill `CorrelationId` for every audit row, and from which value (the request correlation text, or a new Guid per save)?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-033
- **Area:** AUD
- **Status:** Open
- **What the code does:** When automatic capture runs with no user, no agent code and no system process name, the audit row records `UserId` 0 and no actor type. Location: `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs` — `BuildAuditLog` (`UserId = bySystem ? null : CurrentUserId ?? 0`). The search screen shows such rows as "System" when the user ID is null or 0 (`AuditSearchService.ToDtosAsync`), so an unattributed change and a system change look the same. Several anonymous flows reach this path, for example the password reset confirm (`AuthenticationService.ConfirmPasswordResetAsync`).
- **What the other source says:** CLAUDE.md line 109 (Rule 3.3, item 2): every regulated mutation must record an attributable user or an explicit agent or system identity.
- **Question:** Should a row written with no known actor carry an explicit marker (for example actor type System with a process name, or a distinct "anonymous" actor) instead of user 0?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-034
- **Area:** AUD
- **Status:** Open
- **What the code does:** The append-only database triggers cover `AuditLogs`, `AuditEventChanges`, `ElectronicSignatures`, `LoginHistories`, `SecurityAuditEvents`, `SystemSettingVersions`, `AccessReviews` and others listed in the catalogue test. `WorkflowHistories` is not in that list and no trigger for it was found in the migrations. Location: `backend/MicroLIMS.Tests/IntegrationTests/AppendOnlyTriggerCatalogPostgresTests.cs` — `ProtectedTables`; `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs` — `WorkflowHistories`.
- **What the other source says:** `documents/DesignSpecification/BusinessRules.md` lines 206-218 (section 11, Audit Trail): nothing is ever hard-deleted from the audit log and the workflow history table; treat them as append-only.
- **Question:** Is the workflow history table meant to be protected at database level like the audit log, or is "treat as append-only" a rule for code only?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-035
- **Area:** AUD
- **Status:** Open
- **What the code does:** Reading the audit trail, the traceability graph, the CSV export and the signature trail needs only the permission (`Audit.View`, `Signatures.Manage`). No method checks the laboratory section of the record. In the first-run seed the System Administrator and the Section Head hold `Audit.View` and `Signatures.Manage`. Location: `backend/MicroLIMS.Application/Services/AuditSearchService.cs` — `ApplyFilters`, `GetForEntityAsync`; `backend/MicroLIMS.Application/Services/AuditExportService.cs` — `PrepareAsync`; `backend/MicroLIMS.Application/Services/AuditTraceabilityService.cs` — `GetTraceabilityForEntityAsync`; `backend/MicroLIMS.Application/Services/ElectronicSignatureService.cs` — `GetTrailAsync`.
- **What the other source says:** CLAUDE.md line 175 (Rule 5.2, anti-IDOR): services must also check resource and laboratory-section scope before reading or changing a record by ID.
- **Question:** Should audit and signature reads be limited to the sections of the user (with `Sections.ViewAll` as the wider right), or is a global read right intended for these two permissions?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-036
- **Area:** SEC
- **Status:** Open
- **What the code does:** The failed-login counter is not reset when a lock expires. After the lock time passes, the counter is still at or above the limit, so the next single wrong password raises it again and sets a new lock. It is reset by a successful sign-in, by an administrator unlock and by a completed administrator recovery. The password-reset confirm (the forgotten-password flow) does not reset it and does not clear a lock. Location: `backend/MicroLIMS.Application/Services/AuthenticationService.cs` — `LoginCoreAsync`, `ConfirmPasswordResetAsync`; `backend/MicroLIMS.Application/Services/AdminPasswordRecoveryService.cs` — `ConfirmRecoveryAsync`.
- **What the other source says:** `documents/DesignSpecification/BusinessRules.md` lines 192-194 (section 10): five failed login attempts lock the account for 15 minutes. It does not say what happens to the count when the lock ends.
- **Question:** After a lock ends, should the next failure start a new count from zero, or lock again at once? Should a completed password reset clear the lock?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-037
- **Area:** SEC
- **Status:** Open
- **What the code does:** The security values are settings that an administrator can change, with defaults in the code: lockout attempts (default 5, range 3 to 20), lock time (15, 1 to 1440 minutes), refresh token life (7, 1 to 30 days), reset token life (60, 5 to 1440 minutes), password history (5, 1 to 24) and minimum length (8, 8 to 64). The constants in `AuthenticationService` (`MaxFailedAttempts`, `LockDuration`, `RefreshTokenLifetime`, `PasswordResetTokenLifetime`) are not read by the methods. Also, `RequestPasswordResetAsync` throws a business-rule error for an unknown user name and the controller hides it; it does not "always return a generic success message" itself. Location: `backend/MicroLIMS.Application/Services/SystemSettings/SystemSettingDefinitions.cs` — `All`; `backend/MicroLIMS.Application/Services/AuthenticationService.cs` — `LoginCoreAsync`, `RequestPasswordResetAsync`; `backend/MicroLIMS.API/Controllers/AuthenticationController.cs` — `RequestPasswordReset`.
- **What the other source says:** `documents/DesignSpecification/BusinessRules.md` lines 192-200 (section 10) state the values as fixed (5 attempts, 15 minutes, 7 days, 1 hour) and say `RequestPasswordResetAsync` always returns a generic success message.
- **Question:** Is the business rule the default value or the configured value? Should BusinessRules.md be updated to say the values are settings?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-038
- **Area:** SEC
- **Status:** Open
- **What the code does:** Two code paths set a new password with different rules. The normal path (`ValidateAndApplyNewPasswordAsync`) reads the history size from the setting `security.password.historyLimit`, removes history rows beyond the limit, and writes the security event `AUTH_SESSION_REVOKED`. The administrator recovery path checks the last 5 password hashes with a fixed number, does not remove old history rows, and writes no security event. The recovery path also uses a fixed expiry of 15 minutes and a fixed limit of 5 wrong codes. Location: `backend/MicroLIMS.Application/Services/AuthenticationService.cs` — `ValidateAndApplyNewPasswordAsync`; `backend/MicroLIMS.Application/Services/AdminPasswordRecoveryService.cs` — `ConfirmRecoveryAsync`, `CreateRecoveryRequestAsync`.
- **What the other source says:** The settings description in `backend/MicroLIMS.Application/Services/SystemSettings/SystemSettingDefinitions.cs` — `All` says the history setting is "how many of a user's last passwords cannot be reused" and has no exception for recovery.
- **Question:** Should recovery follow the history setting and write security events like the normal path, or is the fixed behaviour intended?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-039
- **Area:** SEC
- **Status:** Open
- **What the code does:** The recovery confirm endpoint is anonymous. Its code comment says an unknown user name, a disabled account and a wrong code "must all look the same to the caller". It gives "Invalid or expired recovery code." for unknown user, disabled user and no pending request, but it gives "Invalid recovery code." for a wrong code (when a pending request exists) and a different text for an expired request ("Recovery code has expired…") and for the fifth wrong try. A caller can therefore tell that the user name exists and has a pending recovery. Location: `backend/MicroLIMS.Application/Services/AdminPasswordRecoveryService.cs` — `ConfirmRecoveryAsync` (comment at `InvalidRecoveryCodeMessage`).
- **What the other source says:** The same method's comment (above) and `documents/DesignSpecification/BusinessRules.md` lines 197-200 (the API never reveals whether a user name exists; stated there for the reset request).
- **Question:** Should every failure of the recovery confirm return the same message?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-040
- **Area:** ADM
- **Status:** Open
- **What the code does:** The System Administrator built-in grants include `MasterData.Manage`, `Items.Manage`, `KpiConfigureProfiles`, `DocumentsConfigManage`, `Sections.ConfigureAll` and `System.ConfigureSettings`; the Section Head seed grants include `MasterData.Manage`, `Items.Manage` and `KpiConfigureProfiles`. The segregation-of-duties rule enforced by the role screen only refuses a role that holds `Users.Manage` or `Roles.Manage` together with a code in a fixed list of laboratory execution and approval codes; configuration codes are not in either list. The code comment says administrators control "users, roles, configuration, master data". Location: `backend/MicroLIMS.Shared/Constants/PermissionConstants.cs` — `SystemAdministratorGrants`; `backend/MicroLIMS.Persistence/Seed/DbSeeder.cs` — role grants for `RoleType.SectionHead`; `backend/MicroLIMS.Shared/Constants/SegregationOfDutiesRules.cs` — `AdministrationCodes`, `GxpExecutionAndApprovalCodes`, `FindConflicts`; `backend/MicroLIMS.Application/Services/RoleService.cs` — `UpdatePermissionsCoreAsync`.
- **What the other source says:** CLAUDE.md line 36 (Principle 1): laboratory configuration is created by the Section Head. CLAUDE.md lines 44-46 (Principle 3): System Administration and Laboratory Administration (Section Head) remain strictly separated.
- **Question:** Should master data, item and workflow configuration be a Section Head right only, with the System Administrator limited to users, roles and system settings? If not, which of these codes may both roles hold?
- **Decision:** Not decided.
- **Date:** Not applicable
