# Audit Trail

## C1 Platform & administration

<!-- from C1-platform.md -->

### MOD-AUDIT-001 Audit Trail (automatic capture)
- Automatic capture on every SaveChanges for tracked entities (Create, Update, Delete): entity name, entity id, action, previous values (JSON, for Update and Delete), new values (JSON), user id, UTC time, plus laboratory identifiers when present [src: backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs:L319-L337, L363].
- Excluded from automatic capture (not written to AuditLog): AuditLog, AuditEventChange, MaterialDocumentAccessLog, EquipmentDocumentAccessLog, Incident, ErrorLog, RefreshToken, NotificationLog, SecurityAuditEvent [src: backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs:L305-L316]. See GAP-111.
- Explicit audit rows written by platform code (action text, what is recorded):
  - `USER_DATA_UPDATED`: previous and new full name, username, e-mail, job title [src: backend/MicroLIMS.Application/Services/UserService.cs:L142-L154].
  - `USER_ROLE_CHANGED`: new role id and name, reason; plus security event AUTH_ROLE_CHANGED [UserService.cs:L185-L196].
  - `USER_DISABLED` / `USER_ENABLED`: previous and new active flag, reason [UserService.cs:L232-L250].
  - `USER_UNLOCKED`: previous failed count and lock time; new values zero and empty, reason [UserService.cs:L273-L290].
  - `ADMIN_PASSWORD_RESET_REQUESTED`: target username, whether e-mail on file, reason [UserService.cs:L308-L315].
  - `USER_HARD_DELETED` (added in verification): snapshot of the deleted user (id, username, full name, e-mail, role) [src: backend/MicroLIMS.Application/Services/UserDeletionService.cs:L77-L103].
  - `SignatureFailed` and `SignatureThrottled` (entity name ElectronicSignature; added in verification): a failed or refused signature attempt with meaning, entity type and id, no password [src: backend/MicroLIMS.Application/Services/ElectronicSignatureService.cs:L44-L55, L65-L81]. A successful signature also appears as an automatic Create row of the ElectronicSignature entity (the entity is not on the exclusion list).
  - Recovery-code audit rows other than REQUESTED carry the TARGET user as the acting user, because the caller is anonymous; REQUESTED carries the administrator [src: AdminPasswordRecoveryService.cs:L96, L138, L155, L172, L244].
  - `FORCE_PASSWORD_CHANGE_SET`: previous and new must-change flag [UserService.cs:L322-L345].
  - `ROLE_PERMISSIONS_UPDATED`: granted and revoked codes [RoleService.cs:L126-L171].
  - `ADMIN_PASSWORD_RECOVERY_REQUESTED`, `ADMIN_PASSWORD_RECOVERY_FAILED`, `ADMIN_PASSWORD_RECOVERY_EXPIRED`, `ADMIN_PASSWORD_RECOVERY_USED`: target user, failure reason where applicable [AdminPasswordRecoveryService.cs:L93-L241].
  - Membership changes: audit captured under the acting user [src: knowledge/_raw/verified-roles-permissions.md §7].
- Security events (table SecurityAuditEvent, not AuditLog; no viewer): 19 AUTH_* codes, with actor, target, reason and a metadata JSON (e.g. revokedSessionCount on lock or disable) [src: backend/MicroLIMS.Shared/Constants/SecurityEventCodes.cs:L12-L38; AuthenticationService.cs:L114-L131; UserService.cs:L238-L244].
- Login attempts: login history rows for every outcome, with reason text: "User not found", "Account locked", "Wrong password", "Account inactive", success (verified); each row also stores the client IP address [src: AuthenticationService.cs:L85, L100, L132, L140, L405-L415].
- Electronic signature: append-only ElectronicSignature row (user, names and role snapshots, meaning, entity, time, comment, IP) [src: backend/MicroLIMS.Domain/Entities/ElectronicSignature.cs:L4-L30].
- Notifications: NotificationLog rows (not audited; read flag only).
- Discussions: post and comment changes captured by automatic capture; versions kept for history [src: backend/MicroLIMS.Application/Services/DiscussionService.cs:L365-L369; DiscussionController.cs:L112].
- Messages: no explicit audit row; message notifications only [src: backend/MicroLIMS.Application/Services/MessageService.cs:L428-L446].
- Error Monitoring: resolve, reopen and override write no explicit audit row; Incident and ErrorLog are excluded from automatic capture (verified); the resolver and time are kept on the incident itself [src: backend/MicroLIMS.Application/Services/ErrorMonitoringService.cs:L106-L158; backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs:L314-L315].
- Archive: an archived record row with reason and generated-by name snapshot [src: backend/MicroLIMS.Application/Services/RecordArchiveService.cs:L54-L68].

## C2 Sample flow & micro testing

<!-- from C2-sample-testing.md -->

Actions that write audit entries or signatures (C2 scope). Signature rows are from `verified-roles-permissions.md` §signature list; audit fields from code.

| Action | Signed | Audit content | src |
|---|---|---|---|
| Receive sample | no | Sample and (Product / RM / PM) test orders created; EM / Water / AC create the sample only; no audit-log row is written by the receive code read here | ProductWorkflowEngine.cs:44-135 |
| Correct sample details | yes (SampleCorrected) | Reason; field changes (old/new) per field; review event log | SampleCorrectionService.cs:212-239 |
| Void sample | yes (SampleVoided) | Reason; field change Status old/new; "Tests Voided" list; review event SampleVoided; test orders Voided, sections Voided, result records marked Voided | SampleCorrectionService.cs:253-303 |
| Add laboratory | yes (LaboratoryAdded) | Laboratory and reason | AddLaboratoryService.cs:53-54 |
| Assign analyst | no (reason in audit) | Action AssignedAnalyst / ReassignedAnalyst / UnassignedAnalyst; previous and new names; reason text | SampleAssignmentService.cs:60-75 |
| Preparation (product) | yes (PreparationConfirmed) | Diluent, neutraliser, preparer, time | SamplePreparationService.cs:184-188 |
| Preparation (EM / AC / Water) | no | Test orders (AssignedAnalystId = preparer) and sample locations created; WorkflowHistory rows only from later transitions; no signature, no explicit audit-log row | EMWorkflowEngine.cs:58-114; WaterWorkflowEngine.cs:73-164; AfterCleaningWorkflowEngine.cs:78-134 |
| Start incubation / transfer / stage 2 | no | Incubation row: start time, media lot, incubator, user | IncubationStepRecorder.cs:117-200 |
| Skip wait | no | MinimumDurationOverriddenAt and user on the incubation; no reason | IncubationStepRecorder.cs:431-443 |
| Count result | no | CountTestReading: plate readings, DF, configured DF, override flag and note, average, calculated, limits, status, entered by/at | IncubationStepRecorder.cs:854-931 |
| Batch location results | no | Location rows: raw readings, DF, limits, status, EnteredAt, EnteredByUserId | IncubationStepRecorder.cs:485-530 |
| Pathogen observation | no | PathogenObservation: observation, user, media; WorkflowStepResult with ExpectedAppearanceSnapshot | PathogenConfirmationRecorder.cs:369-395 |
| Confirmatory setup / observations | no | Selections, observations, expected appearance snapshot, recorded user and time | PathogenConfirmationRecorder.cs:549-575 |
| Analyst decision (Conclude as Detected) | no | Note "Analyst decision: submitted as Detected without biochemical confirmation." | PathogenConfirmationRecorder.cs:658 |
| Biochemical result | no | Biochemical text, organism flag, attachment | PathogenConfirmationRecorder.cs:676-760 |
| Session complete | no | WorkflowHistory per order (Ready); ReviewEventLog "Testing session completed - submitted for review" | PathogenSessionService.cs:1240-1256 |
| Session reset | no | HARD DELETE of observations, step results, readings, results and incubations for the whole sample; AuditLog action "ResetWorkflowSteps" (entity Sample, new value with reference and reason); WorkflowHistory per order "Testing session and workflow steps reset. Reason: {reason}" | PathogenSessionService.cs:1301-1399 |
| Preparation refused | n/a | AuditLog "TestStartRefused" (reason "GMP Refusal: Test preparation stage must be confirmed for sample {ref} before testing can start.") plus a WorkflowHistory refusal note | TestWorkflowSupport.cs:299-319 |
| Qualitative / other physicochemical result | yes (ResultRecorded) | TestAnalysis with signature, equipment, analysis time, per-parameter results; Result row; projection | TestWorkflowSupport.cs:691-756 |
| Item preparation configuration save / approve | no | Created-by, approved-by and time on the configuration row; any edit re-opens approval | ItemPreparationConfigurationService.cs:52-99 |
| Biochemical review decision | no | ReviewEventLog ReviewCompleted (Approve or Investigation) with comment; notification to analyst | PathogenConfirmationRecorder.cs:801-858 |
| Shared TSB start (session) | no | Incubation rows for each joining order (media lot, incubator, window), WorkflowHistory note "Shared TSB Enrichment started: ..." | PathogenSessionService.cs:613-647 |
| Return test to analyst | no | Reason (optional in dialog) | SampleReviewService (C3) |

Note: "Analyst decision" and "session complete" carry no signature in code (verified list). Whether a count-test result or pathogen step is intended to be signed is REQUIRES CONFIRMATION (verified file §signature).

---

## C3 Review, approval, reports out

<!-- from C3-review-approval.md -->

- Review completed: a ReviewWorkflowEvent (entity Sample, performer, name snapshot, comment, section) and an ElectronicSignature (meaning Reviewed), written in one transaction [src: ReviewGateService.cs:L32-L52; SampleReviewService.cs:L121-L123].
- Automatic submit for review: a ReviewWorkflowEvent with no signature [src: SampleReviewService.cs:L86-L89; ReviewGateService.cs:L57-L60].
- Approval decision: a ReviewWorkflowEvent with the decision value, and an ElectronicSignature with the meaning for that decision [src: SampleApprovalService.cs:L305-L307].
- Close testing: a ReviewWorkflowEvent (SectionTestingClosed) with the reason as comment, and an ElectronicSignature TestingClosed [src: SectionClosureService.cs:L62-L65].
- Test-order history (WorkflowHistory): review, approval, rejection, retest, new sample, close and return each write a note (texts in BR-REVIEW-010, BR-APPROVAL-012 to BR-APPROVAL-015, BR-APPROVAL-018) [src: SampleReviewService.cs:L143-L150; SampleApprovalService.cs:L341-L348, L366-L373, L415-L422, L493-L500; SectionClosureService.cs:L80-L87].
- Return to analyst: a TestReturnEvent (test order, reviewer, assigned analyst, reason, UTC time) [src: SampleReviewService.cs:L256-L266].
- Signature row: user id, full name, username and role snapshots, meaning, entity type and id, SignedAt, optional comment, IP address; append-only by design [src: documentation/knowledge/_raw/verified-roles-permissions.md:L144].
- Final archive: an ArchivedRecord with label and SHA-256 [src: SampleApprovalService.cs:L534-L535; RecordArchiveService.cs:L38-L80].
- OOS documents: uploaded by and at; supersession reason, superseded by and at; void reason, voided by and at [src: backend/MicroLIMS.Domain/Entities/OosInvestigationDocument.cs:L26-L43].
- Automatic change capture on SaveChanges is described in BusinessRules.md section 11 (not verified in this pass) [src: documents/DesignSpecification/BusinessRules.md:L206-L220].

## C4 Monitoring & micro configuration

<!-- from C4-monitoring-microconfig.md -->

General rule (verified) for every write in this cluster: the database context writes an audit log row for each entity added (action Create), modified (Update, with the previous values and the new values as JSON of all properties) or deleted (Delete, with the previous values); the audit rows and the change are committed in one transaction. Each row records the entity name, record id, acting user id (set per request by the audit middleware; 0 when none), UTC time, and, where the entity has them, the batch number, control number, sample reference, media lot number, sample id and test order id. Entity types excluded from this audit: the audit tables themselves and the audit event changes, material and equipment document access logs, incidents, error logs, refresh tokens, notification logs and security events. No reason text is captured by the generic audit. [src: backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs:L259-L375; backend/MicroLIMS.API/Middleware/AuditMiddleware.cs:L23]

### MOD-EM-001 Environmental Monitoring configuration
- Department, room and room test configuration create, update and delete: generic audit only (Create / Update / Delete with previous and new values). [src: MicroLimsDbContext.cs:L259-L375] (verified)
- No electronic signature: no signature dialog exists on the page and no service in this cluster's EM code calls the signature service. [src: EMConfigPage.tsx, EmPanels.tsx (no SignatureDialog import); EnvironmentalMonitoringMasterDataService.cs (no sign call)] (verified)
- No reason is captured (the request records carry no reason field). [src: backend/MicroLIMS.Application/DTOs/MasterDataRequests.cs:L15-L26] (verified)
- Limits copied onto a sample location at result entry become part of the location row, which is itself audited (C2); later edits to the configuration do not rewrite it.

### MOD-WATER-001 Water configuration
- Water system, sample location (including assigned test codes) and limit row create, update and delete: generic audit only. One save of the location panel can produce several audit rows (the location, then each limit row). No signature, no reason. [src: WaterMasterDataService.cs:L25-L156; WaterLocationPanel.tsx:L95-L120] (verified)

### MOD-AC-001 After Cleaning configuration
- Machine, part and part configuration create, update and delete: generic audit only. The pathogen dialog saves one configuration per ticked pathogen and deletes one per unticked pathogen, each audited separately (an add and a remove can partly succeed: "Some changes were not saved. ..."). No signature, no reason. [src: AcPanels.tsx:L226-L253; AfterCleaningMasterDataService.cs:L36-L171] (verified)

### MOD-ORG-001 Organisms master
- Create, update and delete: generic audit only. No signature, no reason. [src: OrganismMasterDataService.cs:L28-L80; MicroLimsDbContext.cs:L259-L375] (verified)

### MOD-MEDIACFG-001 Media Configurations
- **Media product code change (signed):** the user enters the password in the "Electronic Signature" dialog; the server writes an electronic signature record (signer full name, user name and role at signing time, meaning "MasterDataChanged", entity type MediaProduct, product id, comment `Code {old} -> {new}: {reason}`, IP address) before changing anything; then the code is changed and an audit event with the action code `MediaProductCodeChanged`, category Configuration, the reason and the field change `Code` (old and new value) is recorded; the generic audit also records the Update of the product row. a wrong password writes an audit log row (entity "ElectronicSignature", action "SignatureFailed") "Failed signature attempt (MasterDataChanged) for MediaProduct #{id}." and changes nothing. [src: backend/MicroLIMS.Application/Services/MediaProductService.cs:L111-L161; backend/MicroLIMS.Application/Services/ElectronicSignatureService.cs:L38-L100] (verified)
- Product create, rename and delete; incubation condition and media configuration create, update and delete; challenge organism changes: generic audit only; no signature, no reason. A rename also updates the display name on the product's configurations (each an audited Update). [src: MediaProductService.cs:L65-L109, L163-L193; MediaMasterDataService.cs:L66-L304; MediaIncubationConditionService.cs:L57-L141] (verified)

### MOD-EQUIP-002 Equipment configuration
- Equipment master create and update: generic audit only (including a change of the "Set Point Temp (°C)" field made through Edit Equipment, which has no reason and no history row, BR-EQUIP-013). [src: EquipmentMasterDataService.cs:L48-L148] (verified)
- **Incubator set point change** (Edit Set Point): a history row (previous value, new value, reason, user, UTC time) in addition to the generic audit of the equipment row. The reason is mandatory in the dialog and on the server. [src: EquipmentConfigurationService.cs:L176-L203] (verified)
- **Autoclave program create, update and status change:** history rows with the comment and user; the history shows previous and new name, load type, temperature, cycle time and active state. Default comments apply only when no comment is sent at all: "Initial program configuration" on create, "Program configuration update" on update; the status change through the endpoint stores an empty comment if none is sent (the page sends "Activated program" / "Deactivated program"). [src: EquipmentConfigurationService.cs:L284-L303, L323-L342, L370-L389; EquipmentMasterDataController.cs:L106-L111] (verified)
- The "Configuration History" tab shows only the set point history of the selected incubator; the program history is shown per program ("Program History: {code}"). [src: EquipmentPage.tsx:L1126-L1178, L1495-L1521]
- Inventory link (API only): the new master equipment row is audited generically; the default incubator set point 32.5 °C is written without a history row. [src: EquipmentConfigurationService.cs:L160-L173] (verified)
- No electronic signature for equipment, set point or program changes. [src: EquipmentPage.tsx (no signature dialog; `grep Signature` returns 0 matches); EquipmentConfigurationService.cs (no sign call)] (verified)

### MOD-TESTMASTER-001 Test Master (Microbiology)
- Test, workflow step and step medium writes (create, update, move, delete, workflow type change, freeze, unfreeze): generic audit only (Create / Update / Delete with previous and new values; a step save also records the replaced step medium rows as deletes and creates). No reason and no signature. The dedicated audit event with a change reason applies to titration tests only (FP, C7). [src: MicroLimsDbContext.cs:L259-L375; TestDefinitionMasterDataService.cs:L770, L796-L813; TestWorkflowStepMasterDataService.cs:L127-L188] (verified)

## C5 Media, strains, stock

<!-- from C5-media-strains-stock.md -->

- General record-change log (verified, applies to every module below): every saved insert, update or delete of a business row (media lot, evaluation, challenge, cryovial, material, equipment, storage entry and so on) is also written to the audit log with entity name, entity id, action (Create / Update / Delete), previous values, new values, user id and timestamp; the searchable columns include media lot number and cryovial code. The user id is taken from the signed-in user of the request. Not logged this way: the append-only audit tables themselves, the material and equipment document access logs (kept in their own tables), incidents, error logs, refresh tokens, notification logs and security audit events. It records no reason text and no electronic signature [src: backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs:L290-L368; backend/MicroLIMS.API/Middleware/AuditMiddleware.cs:L17-L26]. So "Destroy" a cryovial, "Mark Out of Stock" and a stock deduction each leave a before/after row even where no review-trail event or signature exists.
- Release / rejection: signature with meaning Approved or Rejected, decision event (ApprovalDecisionMade), comment, IP, recorded in the same save as the state change [src: MediaReleaseService.cs:L87-L107; ReviewGateService].
- Mark out of stock: review-gate event ApprovalDecisionMade with the comment [src: MediaPreparationService.cs:L172-L177].
- Storage add and move: review-gate log comments (see BR-MEDIA-026/027) [src: MediaStorageService.cs:L121, L255].
- Evaluation: challenge fields ReadAt, ReadByUserId, Incubation StartedBy/CompletedBy, evaluation CompletedAt/CompletedByUserId [src: MediaEvaluationEngine.cs:L152-L160, L259-L268, L281-L282].
- Preparation: PreparedByUserId on lot [src: MediaPreparationService.cs:L96].
- No electronic signature for: mark out of stock, storage moves, expiry changes (per raw inventory, verified by grep of password fields in module folders).
- Signature attempt throttling writes an audit entry: "Signature attempt (...) for {entity} #{id} refused: {n} failed attempts in the last {m} minutes." [src: backend/MicroLIMS.Application/Services/ElectronicSignatureService.cs:L50-L57].

### MOD-CRYO-001 Cryovials
- Approve or reject: signature (password, meaning Approved or Rejected), decision event, comment, IP; written in one save with the status change [src: CryovialService.cs:L157-L168].
- Storage move: review-gate event StorageLocationChanged with old location, new location and reason [src: CryovialService.cs:L262-L268].
- Thaw: ThawEvent row (batch, user, notes); no review-gate event [src: CryovialService.cs:L198].
- Preparation: PreparedByUserId on batch [src: CryovialService.cs:L104].
- Destroy: no review-trail event and no reason in the service; the general record-change log still records the IsDestroyed change with user and time (verified; see GAP-507).
- Thaw: the thaw reason is stored in the thaw event Notes and is not in the review trail (verified) [src: CryovialService.cs:L198].

### MOD-MATERIAL-001 Materials Stock
- Document upload, supersede and void: access log rows (MaterialDocumentAccessLog) with action Supersede or Void [src: MaterialDocumentService.cs:L319, L353]; actor and time on Superseded and Voided fields [src: MaterialDocumentService.cs:L287-L289, L347-L349]. No password signature on these actions (verified).
- Stock deduction: LastModifiedByUserId and LastModifiedAt on the Material row [src: MaterialService.cs:L410-L411]; no separate review-trail event; the change is in the general record-change log above.
- Receipt edits: LastModifiedBy fields via the update path [src: MaterialService.cs:L291 region].

### MOD-EQUIP-001 Equipment Inventory
- Operational status change: EquipmentStatusHistory row with previous status, new status, comment, actor [src: EquipmentInventoryService.cs:L178-L188]; read via status-history endpoint [src: EquipmentInventoryController.cs:L91].
- Calibration certificate: EquipmentDocumentAccessLog for access; supersede and void write log entries [src: backend/MicroLIMS.Application/Services/EquipmentDocumentService.cs:L272, L298, L322].
- Integrity failure on read is logged as an error [src: EquipmentDocumentService.cs:L185].

## C6 Physicochemical

<!-- from C6-physchem.md -->

- Every save through the DbContext writes audit entries (spec "Every insert/update is audited by MicroLimsDbContext.SaveChanges"). [src: docs/superpowers/specs/2026-10-03-working-standard-qualification-design.md sec. 4.2; 2026-09-29-hplc-chain-design.md sec. 6]
- Signed actions (electronic signature with password, comment, IP) - meanings from SignatureMeaning:
  - SST confirmation: SuitabilityRunPerformed [src: HplcRunService.cs:L524-L525; IcpRunService.Calibration.cs:L131-L132]
  - ICP calibration confirmation: SuitabilityRunPerformed [IcpRunService.Calibration.cs:L131]
  - Sample send for review (HPLC, ICP, qualification, dissolution, disintegration, weight variation, titration result, gravimetric result): ResultRecorded [HplcMethodAssayRecorder.cs:L136-L147; DissolutionRecorder.cs:L119; DisintegrationRecorder.cs:L88-L91; WeightVariationRecorder.cs:L101-L104]
  - Solution preparation completion: PreparationConfirmed [SolutionPreparationService.cs:L280-L281]
  - Titrant standardization: TitrantStandardized [TitrantStandardizationService.cs:L244-L245]
  - Due titrant acknowledgement: TitrantDueAcknowledged [TitrationRecorder.cs:L349-L354]
  - Working standard review: Reviewed; approval: Approved; rejection: Rejected [WorkingStandardService.SignOff.cs:L26, L91, L135]
  - Working standard replicate submission: ResultRecorded [HplcRunService.WorkingStandards.cs:L159-L160]
  - Full meaning list: backend/MicroLIMS.Domain/Enums/SignatureMeaning.cs (17 values in _raw/backend-enums.md:L145). [src: backend/MicroLIMS.Domain/Enums/SignatureMeaning.cs:L3]
- Failed password: the signing service writes its own failure audit row immediately; nothing else is persisted. [src: HplcRunService.cs:L491-L496 (comment); IcpRunService.Calibration.cs:L113-L115 (comment)]
- Method, solution, titrant and column edits: edit with reason; history viewable (method history endpoints). [src: HplcMethodController.cs:L30; IcpMethodController.cs:L29]
- Titration settings change (verified): when a Test Master update changes the titration settings, a reason of 5 to 500 characters is required ("A reason for change is required when titration settings change."), and a user event "TestDefinition.TitrationChanged" is written with the before and after settings and the reason. INTENDED: docs/superpowers/specs/2026-10-05-physchem-areas-design.md D5. [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L752-L774]
- Evidence supersede keeps the old file visible with a reason. [src: HplcRunService.cs:L629-L659]
- Removal of a sample keeps the row (soft removal) with a reason. [src: HplcRunService.Samples.cs:L161-L168]
- Working standard lots and qualifications: history and audit rows via SaveChanges; lot created on approval. [src: WorkingStandardService.SignOff.cs:L98-L122]

## C7 General masters

<!-- from C7-masters.md -->

### MOD-ITEM-001 Items
- Automatic Audit Capture (verified, mechanism corrected): `MicroLimsDbContext.SaveChanges` captures every added, modified and deleted entity (except an exclusion list such as audit rows, incidents, error logs, refresh tokens, notification logs and the Material/Equipment document access logs) and writes an `AuditLog` row in the same transaction, with action Create/Update/Delete, the previous values (updates and deletes) and the new values. It is a DbContext override, not an EF interceptor. So item create/update/freeze/unfreeze/delete, item documents (including the `ItemDocumentAccessLog` rows, which are NOT on the exclusion list), preparation configurations, specifications, test definitions, stage replicates, reference lists and workbook import rows are all audited without the service doing anything. No reason or e-signature is attached to these entries [src: backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs:L251-L342].
- Document Access Logging: Document downloads and in-browser views are logged in `ItemDocumentAccessLog` (`View` or `Download`), storing document ID, user ID, action and timestamp (no IP address). [src: backend/MicroLIMS.Application/Services/ItemDocumentService.cs:L150-L157; backend/MicroLIMS.Domain/Entities/ItemDocumentAccessLog.cs:L7-L18].
- Preparation Configuration approval (verified): records `CreatedByUserId`, `CreatedAt`, `ApprovedByUserId` and `ApprovedAt` on `ItemPreparationConfiguration`; it is NOT an electronic signature (no password, no signature record). Editing clears the approver and time (the previous values stay in the generic audit row) [src: backend/MicroLIMS.Application/Services/ItemPreparationConfigurationService.cs:L62, L67-L71, L93-L95].

### MOD-SPEC-001 Specifications
- Automatic Audit Capture (verified): specification creations, updates and deletions (and their stage rows) write generic audit rows through the same DbContext mechanism with old and new column values; no reason and no signature [src: backend/MicroLIMS.Application/Services/MasterData/SpecificationMasterDataService.cs:L127-L129, L196-L208; backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs:L251-L342].

### MOD-WORKBOOK-001 Master Data Workbook
- Electronic signature on load (verified, corrected): meaning `MasterDataChanged`, entity type `MasterDataImport`, entity id = the import id, comment = the loader's comment or the text `Loaded {file name}`, plus the signer's user id, printed name / username / role captured at signing, time and IP address. The signature is taken before any row is loaded (wrong password -> `Password verification failed. The signature was not applied.`; too many failed attempts -> `Too many failed signature attempts. Wait ... minutes, then try again. The signature was not applied.`). The file's SHA-256 is stored on the `MasterDataImport` row, not inside the signature record (the earlier draft said the hash was recorded in the signature) [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L138-L152; backend/MicroLIMS.Application/Services/ElectronicSignatureService.cs:L38-L95; backend/MicroLIMS.Domain/Entities/ElectronicSignature.cs:L12-L33].
- Electronic signature on verification (verified): meaning `Reviewed`, entity type `MasterDataImport`, entity id = the import id, comment = the verifier's comment (may be empty), same captured fields. The import row also stores `VerifiedByUserId`, `VerifiedAt` and `VerificationComment` [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L215-L223].
- Generic audit rows: every record the load creates (organisms, rooms, items, specifications ...) gets its normal Create audit row through the DbContext mechanism, inside the same transaction as the load; a rolled-back check or failed load leaves none (INTENDED: design spec Testing section "dry run leaves no rows and no audit rows") [src: docs/superpowers/specs/2026-10-05-master-data-workbook-design.md:L126-L129].

### MOD-TESTMASTER-001 Test Master (FP and RM & PM)
- Titration Changed Audit Event: Modifying titration configuration on an existing test triggers `_audit.RecordUserEventAsync("TestDefinition.TitrationChanged", AuditActionCategory.Configuration, "TestDefinition", entityId, changes, reason)`, storing before and after JSON snapshots and mandatory Reason for Change. [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L767-L773].
- Automatic Audit Capture: Test definition creations, updates, and freeze/unfreeze actions are tracked in the audit trail. [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L368, L765, L801, L810].

## C8 Reports, KPI, Document Control

<!-- from C8-reports-dc.md -->

### MOD-REPORTS-001 Reports
- Export audit logging: Every CSV export via `ReportingController` logs an audit event via `DataExportAuditService.LogExportAsync(...)` recording the user ID, serialized JSON filter parameters, exported row count, export type name (`"ResultRecordsCsv"`, `"MediaGptCsv"`, or `"ReferenceStrainsCsv"`), and UTC timestamp [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L170-L175, L257-L263, L339-L345].

### MOD-REPORTS-002 Reporting backfill
- Operation logging: `ResultProjectionService.BackfillAsync` writes structured informational and warning logs at each projection stage (`CountTestReading`, `WorkflowStepResult`, `SampleLocation`, `ParameterResult`, and sample approval refresh), and returns a `BackfillResult` record containing counts of Created, Updated, Skipped, and row-level error descriptions [src: backend/MicroLIMS.Application/Services/ResultProjectionService.cs:L413-L514].

### MOD-KPI-001 KPI and Workflow Profiles
- Workflow profile version added:
  - Electronic signature: Signed with `SignatureMeaning.MasterDataChanged`, entity type `ReviewEntityTypes.WorkflowProfile`, recording user ID, IP address, and comment: `"{Scope}: units {WorkUnits ?? inherit}, target days {TargetAnalystDays ?? inherit}, effective {EffectiveFrom}: {ReasonForChange}"` [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L80-L82, L298-L299].
  - Audit event: Action code `WorkflowProfileVersionAdded`, category `Configuration`, entity type `WorkflowProfile`, recording previous vs new values for Scope, WorkUnits, TargetAnalystDays, and EffectiveFrom, plus the mandatory reason for change [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L85-L88, L303-L309].
- Workflow profile draft signed:
  - Electronic signature: Signed with `SignatureMeaning.MasterDataChanged` [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L116-L118].
  - Audit event: Action code `WorkflowProfileDraftSigned`, category `Configuration`, entity type `WorkflowProfile`, recording field diffs and reason for change [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L128-L131].

### MOD-KPI-002 Analyst availability and KPI section settings
- KPI settings changed: Action code `KpiSettingsChanged`, category `Configuration`, record type `KpiSetting`, recording previous and new values for `HoursPerAnalystDay`, `WorkingWeekdays`, and `LoadHorizonDays`, along with user ID and mandatory reason [src: backend/MicroLIMS.Application/Services/Kpi/KpiSettingsService.cs:L82-L84].
- Analyst availability changed: Action code `AnalystAvailabilityChanged`, category `Configuration`, record type `AnalystAvailability`, recording previous and new values for `UserId`, `FromDate`, `ToDate`, and `FractionAvailable`, along with entered by user ID and mandatory reason [src: backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs:L77-L85, L114-L115].

### MOD-DC-001 Document Control: library and document lifecycle
Action codes below were re-checked with `git grep` in the services; the first extraction listed twelve codes that do not exist, corrected here. Categories shown only where read in the code.
- Master and revision [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L193, L500, L554, L611, L683, L730; backend/MicroLIMS.Application/Services/DocumentControl/DocumentRevisionService.cs:L159, L180, L255, L305, L355, L420, L514]:
  - `DocumentMasterRegistered` (category Document): registration with code, title, type, department, owner, initial revision number.
  - `DocumentMasterMetadataUpdated`: draft metadata edit.
  - `DocumentMasterVoided` (Document): previous and new record status plus the void reason.
  - `DraftRevisionCancelled` (Document): previous and new revision status plus the cancel reason.
  - `WorkflowAssignmentAdded`, `WorkflowAssignmentRemoved`: personnel assignments.
  - `DocumentRevisionCreated`, `RevisionNumberOverridden` (Section Head override), `RevisionChangeItemAdded`, `RevisionChangeItemUpdated`, `RevisionChangeItemDeactivated`, `ReviewFindingConvertedToChangeItem`, `RevisionImpactAssessmentSaved`.
- Files [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs:L108, L208, L231, L275, L298, L347, L374, L393-L394]: `RevisionFileUploaded`, `RevisionFileReplaced`, `ControlledFileViewed`, `ControlledFileDownloaded`, `SourceFileViewed`, `SourceFileDownloaded`, `UnauthorizedFileAccessAttempted` (security), `StoredFileMissing` (security), `FileIntegrityVerificationFailed` (security).
- Technical review [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L159, L344, L393, L452, L524, L606, L641] (category Review where read): `RevisionSubmittedForReview`, `ReviewFindingCreated`, `ReviewFindingAuthorResponded`, `ReviewFindingVerified`, `ReviewFindingResolved`, `TechnicalReviewCompleted`, `TechnicalReviewReturned` (each with previous and new status values).
- Approval [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L137, L321, L457, L502, L606, L625] (category Approval): `ApprovalTaskCreated`, `ApprovalDossierViewed`, `RevisionReturnedByApprover`, `RevisionDeclinedByApprover`, `ElectronicSignatureApplied` (signature id, signer user name, full name, role, meaning, time), `DocumentRevisionApproved` (task status, revision status, effective date, next review date). Return and decline store the typed reason.
- Automatic activation [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentEffectiveDateService.cs:L221, L239, L335]: `RevisionAutomaticallyActivated`, `RevisionAutomaticallySuperseded`, `SystemProcessError` (system process, no user).
- Periodic review [src: backend/MicroLIMS.Application/Services/DocumentControl/PeriodicReviewService.cs:L143, L180, L417, L465, L511, L589, L619, L645] : `PeriodicReviewTaskCreated`, `PeriodicReviewNoteCreated`, `PeriodicReviewFindingResolved`, `PeriodicReviewAssigned`, `PeriodicReviewRemainsValid`, `PeriodicReviewRevisionRequired`, `PeriodicReviewObsolescenceRecommended` (category Review).
- Not found in code (listed by the first extraction): `TechnicalReviewSubmitted`, `ReviewFindingAdded`, `ReviewFindingResponseSubmitted`, `PeriodicReviewCreated`, `AcknowledgementRecorded`, `DepartmentCreated`, `DepartmentUpdated`, `SectionCreated`, `SectionUpdated`, `NumberingConfigurationUpdated`, `EscalationCreated`, `EscalationResolved`.

### MOD-DC-002 Document Control: reading and training
- Training and reading audit actions [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs:L158, L175, L215, L302; backend/MicroLIMS.Application/Services/DocumentControl/TrainingAssignmentService.cs:L120, L260, L369, L475, L492]:
  - `ReadingAssignmentAcknowledged`: the conscious acknowledgement (assignment id, user, client IP address, user agent, frozen legal statement, controlled file SHA-256) [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs:L265-L320].
  - `AcknowledgementRejected`: security event for another user's assignment or a mismatch.
  - `AcknowledgementAlreadyRecorded`: repeat submission handled without a second record.
  - `SupersededAssignmentsClosed` (Document, system attributed), `TrainingCascadeAssignmentsCreated`, `ManualTrainingAssignmentsCreated`, `TrainingAssignmentsCancelledDueToObsolescence` (never reached, GAP-818).

### MOD-DC-003 Document Control: audit trail
- Audit export event: action code `AuditTrailExported`, category `Security`, record type AuditLog, reason "Exported {n} audit records to CSV.", with the exported record count [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuditService.cs:L122-L130].
- The audit trail table of the database is append-only for Document Control by design (FS-1a-182); the immutability mechanism was not re-read here.

### MOD-DC-004 Document Control: configuration
- Configuration audit actions [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs:L92, L144, L213, L258, L326, L371, L441, L479, L569]: `DocumentTypeCreated`, `DocumentTypeUpdated`, `DocumentDepartmentCreated`, `DocumentDepartmentUpdated`, `DocumentSectionCreated`, `DocumentSectionUpdated`, `DocumentNumberingConfigUpdated` (previous and new prefix, format, enabled), `ConfigurationSettingUpdated`.

### MOD-DC-005 Document escalations (backend)
- Escalation audit actions: `TrainingEscalationRaised_{ApproachingDue|Due|Overdue}` (system process, category Training; level, recipient, due date, trigger time) and `TrainingEscalationResolved` (user, category Training, reason) [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentEscalationService.cs:L208-L222, L395-L410].
