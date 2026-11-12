# FS-SIG Functional Specification

| Field | Value |
|---|---|
| Document number | FS-SIG |
| Title | Electronic signatures functional specification |
| Version | 0.1 |
| Status | Draft |
| Author | Claude (AI agent), draft from code |
| Reviewer | To be assigned by QA |
| Approver | To be assigned by QA |
| Effective date | Set when Approved |

## Change history

| Version | Date | Author | Change |
|---|---|---|---|
| 0.1 | 2026-11-12 | Claude (AI agent), draft from code | First draft, written from the code as built. |

## Scope and reading notes

- One service creates every signature: `ElectronicSignatureService.SignAsync`. Other areas call it (for example review, approval, role changes, setting changes). Their own FS files say when a signature is required. This file describes only what the signature service does.
- The service adds the signature to the database context but does not save it. The calling method saves it together with its own change, so the signature and the change are committed together.
- Only one controller is in this area: `SignaturesController`. It has one read endpoint and no write endpoint.
- Nothing was run. The database trigger that protects the signature table was read in the migration files and in the trigger test list; the trigger itself was not run.

## Functions

### FS-SIG-001
**Satisfies:** URS-SIG-001, URS-SIG-002
**Function:** Create a signature (`SignAsync`). Inputs: user ID, password, meaning (`SignatureMeaning`), entity type text, entity ID number, optional comment, optional IP address. The caller supplies the user ID; the service does not check that it matches the logged-in session (callers pass the ID of the authenticated user). The service loads the user with the role. The signature is refused with the same generic message ("Password verification failed. The signature was not applied.") when the user does not exist, is inactive, is locked (`IsLockedAt`) or the password does not verify (BCrypt check against the stored hash). A refusal does not change the login failure counter or the lock on the account. When the check passes, an `ElectronicSignature` row is created with the user ID and a snapshot of full name, user name, role type, role name, the meaning, the entity type and entity ID, the comment and the IP address. The meaning, the record and the comment come from the calling service; no frontend value builds the signature text. No section or laboratory scope is checked in this method. No record version or record content is stored with the signature: it is tied to the record only by entity type and entity ID. The signing time is not set in this method; the row takes the default of the entity (`DateTime.UtcNow` in the entity class), not the injected clock. The signature row is audited by the automatic audit capture because `ElectronicSignature` is not in the excluded list.
**Code:** `backend/MicroLIMS.Application/Services/ElectronicSignatureService.cs` — `SignAsync`; `backend/MicroLIMS.Domain/Entities/ElectronicSignature.cs` — `ElectronicSignature`
**Verified by:** reading (not run)
**Conflicts:** CONF-030, CONF-031

### FS-SIG-002
**Satisfies:** URS-SIG-003
**Function:** Throttle and record failed attempts (`SignAsync`). Before the password is checked, the service reads two settings: the maximum failed attempts and the window in minutes (`security.signature.maxFailedAttempts` and `security.signature.windowMinutes`; the defaults in the code are 5 and 15; the allowed range of the first is 3 to 20 and of the second 1 to 1440). It counts audit rows for entity `ElectronicSignature` with action `SignatureFailed` for the same user whose time is inside the window (the window start uses the injected clock). When the count is at or above the maximum, the service adds an audit row with action `SignatureThrottled`, saves it, and throws a signature error saying to wait; the password is not checked and the signature is not applied. When the password check fails, the service adds an audit row with action `SignatureFailed` (text names the meaning and the record), saves it, and throws. Both audit rows carry the user ID and, for test orders, the test order ID. The window is counted per user, across all records. The constants `MaxFailedAttempts` and `FailedAttemptWindow` in the class are not read by this method; the settings are read instead. The two audit rows are written directly and take their time from the audit entity default, not from the injected clock.
**Code:** `backend/MicroLIMS.Application/Services/ElectronicSignatureService.cs` — `SignAsync`; `backend/MicroLIMS.Application/Services/SystemSettings/SystemSettingDefinitions.cs` — `All`
**Verified by:** reading (not run)
**Conflicts:** CONF-031

### FS-SIG-003
**Satisfies:** URS-SIG-003
**Function:** Keep the failed-attempt record when the calling command is rolled back. `UnitOfWork.RunAsync` wraps many signed commands in one transaction. When the command fails, the unit of work finds the audit rows that `IsRefusedAttemptRecord` marks (entity `ElectronicSignature`, action `SignatureFailed` or `SignatureThrottled`), rolls the transaction back, restores the change tracker, then adds those rows again and saves them. The result is that a refused signature stays on record and still counts towards the throttle. If the command is not run through `UnitOfWork` (or the database is not relational), this re-save does not happen in that code path; whether every signing caller uses `UnitOfWork` was not checked for this document.
**Code:** `backend/MicroLIMS.Application/Helpers/UnitOfWork.cs` — `RunAsync`; `backend/MicroLIMS.Application/Services/ElectronicSignatureService.cs` — `IsRefusedAttemptRecord`
**Verified by:** reading (not run)

### FS-SIG-004
**Satisfies:** URS-SIG-004, URS-SIG-002
**Function:** Show the signature trail of a record (`GET api/signatures?entityType=…&entityId=…`; permission `Signatures.Manage`). Inputs: entity type text and entity ID number. `GetTrailAsync` returns every signature with that type and ID, oldest first by signing time, as `SignatureDto`: printed name, user name, role (the role name at signing, or the role type for older rows), meaning as text, signing time (a UTC value) and comment. The user ID and password hash are not returned. `SignatureDto.MeaningText` gives the sentence for the meaning from `ReportVocabulary`. In the first-run seed, the System Administrator (`SystemAdministratorGrants`, commented there as a read-only signature trail) and the Section Head hold `Signatures.Manage`; the Reviewer and Analyst seed lists do not name it. An administrator can change these grants later through the role screen. The entity type text is not validated against a list, and no check is made that the user may see the record (no laboratory-section scope check in this method or in the controller). No audit row is written for viewing the trail. The time zone used when the time is shown to a person was not read here (display is outside the files read).
**Code:** `backend/MicroLIMS.API/Controllers/SignaturesController.cs` — `GetTrail`; `backend/MicroLIMS.Application/Services/ElectronicSignatureService.cs` — `GetTrailAsync`; `backend/MicroLIMS.Application/DTOs/SignatureDto.cs` — `SignatureDto`
**Verified by:** reading (not run)
**Conflicts:** CONF-035

### FS-SIG-005
**Satisfies:** URS-SIG-004
**Function:** Protect recorded signatures from change. The controller has no POST, PUT or DELETE action and no service method updates or deletes a signature. The entity class carries a comment that rows are append-only. A database migration creates a trigger on table `ElectronicSignatures` that runs before update or delete, and a second migration adds a no-truncate trigger. The trigger catalogue test lists `ElectronicSignatures` as protected against update and delete. Foreign keys from other tables to a signature use restrict on delete. Whether the triggers work was not tested by this document.
**Code:** `backend/MicroLIMS.Persistence/Migrations/20260902181230_AddAuditImmutabilityTriggers.cs` — `Up`; `backend/MicroLIMS.Persistence/Migrations/20261027050929_HardenAppendOnlyAuditTables.cs` — `Up`; `backend/MicroLIMS.API/Controllers/SignaturesController.cs` — `SignaturesController`
**Verified by:** reading (not run)
