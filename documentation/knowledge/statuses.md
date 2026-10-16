# Statuses

## C1 Platform & administration

<!-- from C1-platform.md -->

### MOD-AUTH-001 Authentication, session, password
- Account active state: "Active" (chip, green) = IsActive true; "Disabled" (chip, red) = IsActive false. Transitions: Active -> Disabled (Users.Manage, reason required, self refused, sessions revoked) -> Active (Users.Manage, no reason). [src: frontend/src/modules/users/UsersPage.tsx:L405-L407; backend/MicroLIMS.Application/Services/UserService.cs:L206-L252]
- Lock state: "Account Locked" (warning chip) while LockedUntil is in the future; transitions: 5 failures -> locked 15 min (automatic, no user actor); lock expires by time; Unlock (Users.Manage, reason optional) clears it. [src: UsersPage.tsx:L412; User.cs:L20; AuthenticationService.cs:L114-L133; UserService.cs:L267-L293]
- Must change password: chip "Must Change Password" (info) while MustChangePassword true; set on create and by force-change; cleared by a successful change or reset. [src: UsersPage.tsx:L413; UserService.cs:L121, L322-L334; AuthenticationService.cs:L318-L321]
- Security column default: "Normal" when neither flag is set. [src: UsersPage.tsx:L414]
- Admin recovery code status (enum AdminPasswordRecoveryStatus): Pending (0), Used (1), Expired (2), FailedLimitExceeded (3); UI label for FailedLimitExceeded "Failed - Limit Exceeded". Transitions: Pending -> Used (successful entry), Pending -> Expired (a new code is generated for the same user; or someone enters the code after the 15 minutes, which is when the stored status changes; with no entry the stored status stays Pending), Pending -> FailedLimitExceeded (5 wrong entries). [src: backend/MicroLIMS.Domain/Enums/AdminPasswordRecoveryStatus.cs:L3; frontend/src/modules/auditSearch/utils/auditEnumLabels.ts:L15; AdminPasswordRecoveryService.cs:L93-L241]
- Security event outcome: Success (0), Failure (1). [src: backend/MicroLIMS.Domain/Enums/SecurityEventOutcome.cs:L7]

### MOD-USERS-001 Users
- Role type (RoleType enum): SystemAdministrator (0), SectionHead (1), Reviewer (2), Analyst (3). Drives dashboard variant, lab scope bypass and some role-type checks. [src: backend/MicroLIMS.Domain/Enums/RoleType.cs:L3]
- Role name shown in UI: custom role names; the role type is the base type, unchanged by rename. [src: knowledge/_raw/verified-roles-permissions.md §3]

### MOD-ROLES-001 Roles and permissions
- Role chips: "System" (IsSystemRole) / "Custom". [src: frontend/src/modules/roles/RolesPage.tsx:L83-L84]
- Role active: "Active" / "Inactive" (IsActive). [src: RolesPage.tsx:L91]
- Permission chip: "Enforced" (success) / "Legacy-only" (warning). Transitions: none in the UI; enforcement is a code-level set (40 enforced, 10 not). [src: frontend/src/modules/roles/components/PermissionMatrix.tsx:L75-L83; backend/MicroLIMS.Shared/Constants/PermissionConstants.cs:L109-L143]
- Role lifecycle: create (Active) -> edit -> delete only when custom and unassigned. [src: RoleService.cs:L63-L124]

### MOD-NOTIF-001 Notifications
- Notification severity (stored string): "info", "warning", "error". Rendering colour per severity. Transition: none, read flag only (unread -> read per notification or read all). [src: DashboardNotificationService.cs:L126-L131, L236-L243; NotificationLog entity]

### MOD-ERR-001 Error Monitoring
- Incident status (enum IncidentStatus): Open (0), Resolved (1). Transitions: Open -> Resolved (Mark resolved; notes optional) ; Resolved -> Open (Reopen; resolution fields kept). Labels "Open" (warning, filled) and "Resolved" (success, outlined). [src: backend/MicroLIMS.Domain/Enums/IncidentStatus.cs:L4; ErrorMonitoringService.cs:L106-L131; frontend/src/modules/errorMonitoring/components/IncidentTable.tsx:L89-L93]
- Severity (enum ErrorSeverity): Info (0), Warning (1), Error (2), Critical (3). Incident severity = max of children; override per log entry; chip "overridden from {severity}"; select "Auto ({severity})". [src: backend/MicroLIMS.Domain/Enums/ErrorSeverity.cs:L6-L12; ErrorMonitoringService.cs:L133-L158; IncidentRowDetail.tsx:L155-L167]
- Source (enum ErrorSource): Backend (0), Frontend (1), Database (2). [src: backend/MicroLIMS.Domain/Enums/ErrorSource.cs:L4-L9]

### MOD-SECAUDIT-001 Security audit events
- Event code list (19, prefix AUTH_): LOGIN_SUCCESS, LOGIN_FAILED_BAD_CREDENTIALS, LOGIN_FAILED_UNKNOWN_USER, LOGIN_FAILED_ACCOUNT_LOCKED, LOGIN_FAILED_ACCOUNT_DISABLED, REFRESH_TOKEN_ISSUED, REFRESH_TOKEN_ROTATED, REFRESH_REJECTED_INVALID_TOKEN, REFRESH_REJECTED_ACCOUNT_BLOCKED, LOGOUT, SESSION_REVOKED, PASSWORD_CHANGED, PASSWORD_CHANGE_FAILED, PASSWORD_RESET, ACCOUNT_DISABLED, ACCOUNT_ENABLED, ACCOUNT_LOCKED, ACCOUNT_UNLOCKED, ROLE_CHANGED. [src: backend/MicroLIMS.Shared/Constants/SecurityEventCodes.cs:L12-L38] (each code is the AUTH_ prefix plus this name)

### MOD-AUDIT-001 Audit Trail
- Audit action (stored): Create, Update, Delete (EF capture); plus custom upper-case actions written by platform services (e.g. USER_DATA_UPDATED, USER_ROLE_CHANGED, USER_UNLOCKED, USER_DISABLED, USER_ENABLED, ADMIN_PASSWORD_RESET_REQUESTED, FORCE_PASSWORD_CHANGE_SET, ROLE_PERMISSIONS_UPDATED, ADMIN_PASSWORD_RECOVERY_REQUESTED / FAILED / EXPIRED / USED). [src: backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs:L327-L337; UserService.cs:L152, L194, L287, L312, L334; AdminPasswordRecoveryService.cs:L93, L135, L152, L169, L183, L241; RoleService.cs:L162]
- Action chip in results: uppercase text (CREATE, UPDATE, DELETE) [src: frontend/src/modules/auditSearch/components/AuditResultsTable.tsx:L130].

### MOD-DISC-001 Discussions
- Post flags: "Important" chip (pinned at top); "v{n} Edited" chip shows edit count. Transitions: Important <-> not (author or EditAny). Post delete is a soft delete. [src: frontend/src/modules/discussions/components/DiscussionCard.tsx:L130-L188; DiscussionService.cs:L410-L421, L424-L435]
- Discussion category (enum DiscussionCategory): 1 Water, 2 Equipment, 3 Environmental Monitoring (EM), 4 Products, 5 Media / Materials, 6 Internal Decisions, 7 Management Requirements, 8 EDA Requirements, 9 ISO 17025, 10 GMP / Regulatory, 11 Other. [src: backend/MicroLIMS.Domain/Enums/DiscussionCategory.cs:L4-L17; frontend/src/modules/discussions/types/discussionTypes.ts:L20-L31]

### MOD-MSG-001 Messages
- Conversation kind: Direct (IsGroup false) or Group (IsGroup true). "Direct Message (1-on-1)" / "Group Conversation". [src: frontend/src/modules/messages/NewConversationDialog.tsx:L140, L150]
- Unread count per conversation and in the account menu "Messages (N unread)". [src: frontend/src/components/Header.tsx:L89, L343-L350]

### MOD-SIG-001 Electronic signatures and segregation of duties
- Signature meaning (enum SignatureMeaning, 17): see BR-SIG-005.
- Signature record fields are immutable; no status.

### MOD-AUDIT-002 Login history / MOD-SIG-002 Signature trail
- No UI status.

### MOD-ARCHIVE-001 Archived records
- Archive reason and integrity state: "verified" or "FAILED" (response header). [src: backend/MicroLIMS.API/Controllers/ArchivedRecordsController.cs:L44-L48]

## C2 Sample flow & micro testing

<!-- from C2-sample-testing.md -->

Enum values from backend (numeric value in parentheses). UI labels from the frontend cited.

### Sample (SampleStatus, backend/MicroLIMS.Domain/Enums/SampleStatus.cs:L3)
| Enum | Value | UI label(s) | Set by / transitions |
|---|---|---|---|
| Received | 0 | "Received" (register, sample report) | Created at receipt (ProductWorkflowEngine:90; WaterWorkflowEngine:64; EMWorkflowEngine:49; AfterCleaningWorkflowEngine:69). Reset from InTesting/UnderReview by session reset (PathogenSessionService.cs:1385). |
| InTesting | 1 | "Under Testing" (register, kanban); "Under testing" (report) | Received -> InTesting on first incubation start (IncubationStepRecorder.cs:117-119) or selective-plating start (PathogenConfirmationRecorder.cs:295). Not signed. |
| UnderReview | 2 | "Pending Review" (register); "Under Review" (lifecycle badge, sample report) | InTesting -> UnderReview on testing-session completion (PathogenSessionService.cs:1255). Also auto-submit when all tests complete (SampleReviewService.cs:89, C3). |
| UnderApproval | 3 | "Pending Approval" (register); "Under Approval" (badge) | C3 approval flow. |
| Approved | 4 | "Approved" | C3 (SampleApprovalService). |
| Rejected | 5 | "Rejected" | C3. Blocks session reset (PathogenSessionService.cs ~1288). |
| RetestRequested | 6 | "Cancelled / Voided" (register); "Retest Requested" (tracking, kanban) | C3 approval decisions. Label conflict (GAP-204). |
| Cancelled | 7 | "Cancelled / Voided" (register); "Cancelled" (tracking) | C3 section closure. |
| Voided | 8 | "Cancelled / Voided" (register); "Voided" (tracking, lifecycle text "Voided: struck from the record") | Void (SampleCorrectionService.cs:269). Signed. |

Lifecycle badge (SampleLifecycleBadge): InTesting "Under Testing"; Received renders nothing; UnderReview "Under Review" (click requires `Samples.Review`); UnderApproval "Under Approval" (requires `Samples.Approve`); Approved (clickable, print icon); Rejected (not clickable). [SampleLifecycleBadge.tsx:6-60, 92]

### Sample preparation (SamplePreparationStatus)
| Enum | Value | UI | Transition |
|---|---|---|---|
| NeedsPreparation | 0 | "Sample Needs Preparation" / "PREP STATUS" | Default; reset by correction when item changes (SampleCorrectionService.cs:423) |
| Ready | 1 | Preparation complete | SamplePreparationService.cs:188 (signed PreparationConfirmed) |

### Test order approval status (ApprovalStatus, ApprovalStatus.cs:L3)
Values: Pending 0, InProgress 1, ResultEntered 2, Reviewed 3, Approved 4, Rejected 5, RetestRequested 6, Voided 7, Cancelled 8.
Set in C2 code: Pending (on receipt and on session reset, PathogenSessionService.cs:1367); ResultEntered (session completion, PathogenSessionService.cs:1242); Voided (SampleCorrectionService.cs:277); Reviewed (SampleReviewService.cs:140, C3); Approved/Rejected (C3).

### Workflow step (WorkflowStep)
Full enum (verified): Waiting, Running, Incubating, Ready, Reviewed, Approved. Mapping to the test-order Status set by the state machine: Waiting -> Pending, Running and Incubating -> InProgress, Ready -> ResultEntered, Reviewed -> Reviewed, Approved -> Approved. Used in C2: Waiting (initial), Incubating (first incubation start), Ready (final result or session completion). Running is used by the product and water engines' advance only. [src: backend/MicroLIMS.Domain/Enums/WorkflowStep.cs; WorkflowStateMachine.cs:21-36]

### Incubation (per Incubation row)
Fields: StartedAt, IncubationStartUtc, IncubationEndUtc, StageNumber (1 or 2), CompletedAt, CompletedByUserId, Outcome, MinimumDurationOverriddenAt/ByUserId. Open = CompletedAt null. [IncubationStepRecorder.cs:117-180, 380-431; PathogenConfirmationRecorder.cs:318-338]

### Result status (ResultStatus, ResultStatus.cs:L7)
WithinLimits 1; AlertLimitExceeded 2; ActionLimitExceeded 3; OutOfSpecification 4; LimitsNotConfigured 5; RequiresReview 6; Absent 7; Detected 8; PendingConfirmation 9; NotDetected 10; NextStageRequired 11; Inconclusive 12. Used in C2: WithinLimits, AlertLimitExceeded, ActionLimitExceeded, OutOfSpecification, LimitsNotConfigured (count status via SpecLimitParser); RequiresReview (TNTC/Uncountable); Absent / Detected (batch pathogen, final observation); NotDetected (selective plating NoGrowth/NonConforming, biochemical no, analyst-decision NotDetected path); Detected (analyst decision, biochemical yes). Frontend labels: "Within Limits", "Out of Specification", "Alert Limit Exceeded", "Action Limit Exceeded", "Limits Not Configured" (StatusBadge.tsx:17-26); "Detected" / "Absent" (pathogen grids).

### Confirmatory result (ConfirmatoryResult)
AllConforming 0; Inconclusive 1. [backend-enums.md ConfirmatoryResult; PathogenConfirmationRecorder.cs:581]

### Growth observation (GrowthObservation, GrowthObservation.cs:L6)
NoGrowth; GrowthNonConforming; GrowthConforming. UI: "No growth" / "No Growth (-)"; "Growth, does not match expected appearance" / "Growth Non-Conforming"; "Growth matching expected appearance" / "Growth Conforming (+)".

### Pathogen session test state (strings, SessionReviewPanel and PathogenSession services)
UI chip labels (frontend): "Locked", "Window Not Configured", "TSB Incubating", "Plating In Progress", "Incubating", "Ready to Read", "Enter Result", "Pending Review", "Reviewed", "Approved", "Rejected", "Voided", "Cancelled", "Superseded", "On Hold", "Pending". Server state strings include "TSB_INCUBATING". [SessionReviewPanel.tsx:408-530; PathogenSessionService.cs:1218]

### Transitions summary (C2 scope)
| From -> To | Trigger | Who | Signed | src |
|---|---|---|---|---|
| (none) -> Received | Receive | Receiver | Receive: no signature in verified list | ProductWorkflowEngine.cs:90 |
| Received -> InTesting | first incubation start | Analyst | no | IncubationStepRecorder.cs:117-119 |
| Received -> InTesting | first selective-plating start | Analyst | no | PathogenConfirmationRecorder.cs:294-295 |
| (current status, any) -> UnderReview, set directly (corrected: not limited to InTesting) | pathogen session completed | Analyst | no (client sends no password) | PathogenSessionService.cs:1255; PathogenSessionService.ts:40 |
| InTesting/UnderReview -> Received | session reset | Section Head | no (reason only) | PathogenSessionService.cs:1385 |
| any status except Voided -> Voided (corrected: no status precondition) | void | Samples.Correct holder | yes (SampleVoided) | SampleCorrectionService.cs:269 |
| Received -> Received (details) | correction | Samples.Correct holder | yes (SampleCorrected) | SampleCorrectionService.cs:218 |
| Test order ResultEntered -> Pending | session reset | Section Head | no | PathogenSessionService.cs:1367 |
| Test order Waiting/Pending -> Incubating/InProgress | first media selection or selective-plating start | Analyst (TestWorkflow.Execute) | no | WorkflowStateMachine.cs:L21-L36; IncubationStepRecorder.cs:121-122 |
| Test order Incubating -> Ready/ResultEntered | final count, batch, qualitative or pathogen result | Analyst | count and pathogen: no; qualitative and other physicochemical: yes (ResultRecorded) | IncubationStepRecorder.cs:338, 541, 646, 718; PathogenConfirmationRecorder.cs:872-898; TestWorkflowSupport.cs:751 |
| Section InTesting -> UnderReview | every current test of the section at Ready | automatic | no | SampleReviewService.cs:66-87 |
| Test order Ready -> Incubating | reviewer returns a Detected-without-biochemical result | Reviewer / Section Head (TestWorkflow.BiochemicalDecision) | no (reason required) | PathogenConfirmationRecorder.cs:813-824 |
| Sample NeedsPreparation -> Ready | preparation confirmed (Product / RM / PM) or prepared (EM / AC / Water) | any signed-in user with section access | Product / RM / PM yes (PreparationConfirmed); others no | SamplePreparationService.cs:188; EMWorkflowEngine.cs:111 |
| Sample Ready -> NeedsPreparation | item / location correction before any work, or laboratory added with micro tests and no preparation | Samples.Correct / Samples.Receive holder | yes | SampleCorrectionService.cs:423; AddLaboratoryService.cs:71-73 |
| Section (any) -> InTesting | laboratory added | Samples.Receive holder | yes (LaboratoryAdded) | AddLaboratoryService.cs:60-61 |

---

## C3 Review, approval, reports out

<!-- from C3-review-approval.md -->

### Sample (SampleStatus)
| Enum | Value | UI label (where read) |
|---|---|---|
| Received | 0 | "Received" (report timeline); no label in lifecycle badge |
| InTesting | 1 | "Under Testing" (badge); "In testing" (sections, report) |
| UnderReview | 2 | "Under Review" (badge); "Under review" (report) |
| UnderApproval | 3 | "Under Approval" (badge); "Under approval" (report) |
| Approved | 4 | "Approved" |
| Rejected | 5 | "Rejected" |
| RetestRequested | 6 | "Retest Requested" (kanban) |
| Cancelled | 7 | No label in the lifecycle badge (the badge shows nothing for it); report page shows the raw value |
| Voided | 8 | "Voided" (report timeline, sections) |
Sources: [src: backend/MicroLIMS.Domain/Enums/SampleStatus.cs:L3; documentation/knowledge/_raw/backend-enums.md:L606-L619; frontend/src/modules/testingWorkspace/SampleLifecycleBadge.tsx:6-12; frontend/src/modules/testingWorkspace/SampleKanbanView.tsx:6-14; frontend/src/modules/testingWorkspace/sampleSummary/summaryFormatting.ts:1-26].

### Section sign-off (SectionSignoffStatus)
| Enum | Value | UI label (summaryFormatting) |
|---|---|---|
| InTesting | 0 | In testing |
| UnderReview | 1 | Under review |
| UnderApproval | 2 | Under approval |
| Approved | 3 | Approved |
| Rejected | 4 | Rejected |
| RetestRequested | 5 | Retest requested |
| Cancelled | 6 | Cancelled |
| Voided | 7 | Voided |
A cancelled section also shows as "Closed" with "{section} closed by {name} on {date}: {reason}" [src: SampleSummaryDialog.tsx:470-477]. Sources: [src: backend/MicroLIMS.Domain/Enums/SectionSignoffStatus.cs:L5; documentation/knowledge/_raw/backend-enums.md:L631-L643; summaryFormatting.ts:1-26].

### Test order (ApprovalStatus)
Pending 0, InProgress 1, ResultEntered 2, Reviewed 3, Approved 4, Rejected 5, RetestRequested 6, Voided 7, Cancelled 8 [src: backend/MicroLIMS.Domain/Enums/ApprovalStatus.cs:L3; backend-enums.md:L34-L47]. Test-level UI labels: UNCLEAR for this cluster (test chips belong to C2).

### Overall sample outcome (OverallSampleStatus)
InProgress 0, Approved 1, Rejected 2, RetestRequested 3, Voided 4, Cancelled 5 [src: backend/MicroLIMS.Domain/Enums/OverallSampleStatus.cs:L6; backend-enums.md:L411-L421]. Computed by SampleSectionRollup.Overall (BR-APPROVAL-019).

### Approval decision (ApprovalDecision)
Values: Approve, Reject, RetestRetainedSample, NewSampleRequest, Investigation, OOSInvestigation [src: backend/MicroLIMS.Domain/Enums/ApprovalDecision.cs:L4]. Reachable at sample approval: the first four [src: SampleApprovalService.cs:L17-L19]. UI labels: "Approve: Results conform to specifications"; "Not Conform (Final Conclusion): Close this sample"; "New Sample Required: Close and request new sample"; "Retest Retained Sample: Return to testing" [src: summaryFormatting.ts:73-78].

### Result status (ResultStatus), count, location and micro results
WithinLimits 1, AlertLimitExceeded 2, ActionLimitExceeded 3, OutOfSpecification 4, LimitsNotConfigured 5, RequiresReview 6, Absent 7, Detected 8, PendingConfirmation 9, NotDetected 10, NextStageRequired 11, Inconclusive 12 [src: backend/MicroLIMS.Domain/Enums/ResultStatus.cs:L7; backend-enums.md:L506-L522]. Report labels: "Within Limits", "Out of Specification", "Alert Limit Exceeded", "Action Limit Exceeded", "Limits Not Configured" [src: frontend/src/components/StatusBadge.tsx:17-26].

### Transitions (sample level; sections change first, then the sample rolls up)
- InTesting -> UnderReview, automatic, per section, when all its current tests are Ready. Event "All tests completed - automatically submitted for review". No password [src: SampleReviewService.cs:L72-L93].
- UnderReview -> UnderApproval, Submit Technical Review; signed (Reviewed); Samples.Review holder in section scope who did not test the section [src: SampleReviewService.cs:L95-L155].
- UnderReview -> InTesting, Return to Analyst; unsigned; count or analysis test in ResultEntered; Samples.Review holder [src: SampleReviewService.cs:L157-L269].
- UnderApproval -> Approved, Approve; signed (Approved); Samples.Approve holder who is not the reviewer and did not test [src: SampleApprovalService.cs:L143-L335].
- UnderApproval -> Rejected, Reject; signed (Rejected) [src: SampleApprovalService.cs:L353-L375].
- UnderApproval -> RetestRequested, Retest Retained Sample; signed (RetestRequested); selected tests superseded; one new sample (section status RetestRequested; the sample status follows the roll-up and becomes RetestRequested only when no section is still open or rejected) [src: SampleApprovalService.cs:L377-L438; SampleSectionRollup.cs:L78-L88].
- UnderApproval -> RetestRequested, New Sample Required; signed (Rejected meaning); selected tests superseded; two new samples (same roll-up rule) [src: SampleApprovalService.cs:L440-L503].
- Open section (InTesting, UnderReview, UnderApproval) -> Cancelled, Close Testing; signed (TestingClosed); only after another section is Rejected [src: SectionClosureService.cs:L31-L100].
- Origin sample RetestRequested -> Approved or Rejected, automatically, when the retest outcome is final [src: SampleApprovalService.cs:L541-L642].
- Test order (corrected): at review the test Status is set to Reviewed and its step from Ready to Reviewed (the review does not read the test Status); at approval Status becomes Approved and step Approved, or on rejection Status Rejected with the step unchanged; Return to Analyst needs Status ResultEntered and moves the step to Running (analysis) or Incubating (count) - a Reviewed test cannot be returned; the superseded flag is set on retest; Status Cancelled on testing closure (the draft wrongly listed "Reviewed -> Running or Incubating (return)") [src: SampleReviewService.cs:L137-L151, L236-L253; SampleApprovalService.cs:L336-L375, L490-L501; SectionClosureService.cs:L74-L88].
- Mixed sections: while any lab is still open the sample sits at the least advanced open status, even after another lab rejected [src: SampleSectionRollup.cs:L73-L77].
- Note: Reject and Close Testing give sections their own final state; the sample status and Overall outcome are computed from all sections (BR-APPROVAL-019).

### Review workflow event types (ReviewWorkflowEventType, shown in Workflow History)
SubmittedForReview, ReviewCompleted, SubmittedForApproval, ApprovalDecisionMade, SampleVoided, SampleCorrected, SectionTestingClosed, LaboratoryAdded, StorageLocationChanged [src: backend/MicroLIMS.Domain/Enums/ReviewWorkflowEventType.cs:L6-L32]. (verified) In this cluster only SubmittedForReview (automatic), ReviewCompleted, ApprovalDecisionMade and SectionTestingClosed are written; SubmittedForApproval is never written (git grep over backend .cs files finds only the enum member and a comment); the others belong to receiving, correction and storage workflows.

### OOS investigation document
Current, Superseded, Voided (UI chips) [src: frontend/src/modules/oosTracking/OosTrackingPage.tsx:640-646]. Stored as MaterialDocumentStatus: Current 0, Superseded 1, Voided 2 [src: backend/MicroLIMS.Domain/Enums/MaterialDocumentStatus.cs:L6-L11]. (verified) Transitions: Current -> Superseded when a replacement is uploaded (reason required); any non-Voided -> Voided through the void endpoint (no screen control); a new upload is offered only when no Current document exists [src: OosInvestigationDocumentService.cs:L185-L275].

## C4 Monitoring & micro configuration

<!-- from C4-monitoring-microconfig.md -->

### MOD-EM-001 Environmental Monitoring configuration
None found. Departments, rooms and room test configurations have no status field. Display chips only: "No tests configured" (room without tests). [src: environmentalMonitoring/components/EmRoomCard.tsx:L32] (verified)

### MOD-WATER-001 Water configuration
None found. Water systems, locations and limit rows have no status field. Display chips only: "Limits not set" (location with no limit row). The "Needs attention" filter is a view filter, not a stored status. [src: water/components/WaterLocationList.tsx:L132; WaterConfigPage.tsx:L190-L200] (verified)

### MOD-AC-001 After Cleaning configuration
None found. Machines, parts and part configurations have no status field. Display chip "No tests configured" on a part card. [src: afterCleaning/components/AcPartCard.tsx:L36] (verified)

### MOD-ORG-001 Organisms master
None found. An organism has no status (no active / inactive flag); it is either present or deleted. [src: backend/MicroLIMS.Application/Services/MasterData/OrganismMasterDataService.cs:L28-L80] (verified)

### MOD-MEDIACFG-001 Media Configurations
None stored. Media products, incubation conditions and configurations have no status field. Display chip "Not used" on an incubation condition that no configuration and no step medium uses; a condition in use shows how many configurations and step media use it ("{n} configuration(s)", "{n} step medium" / "step media") and its edit and delete icons are disabled with the text "In use - add a new condition instead". [src: components/MediaIncubationConditionsSection.tsx:L120-L205] (verified)

### MOD-EQUIP-002 Equipment configuration
- **Autoclave program status** - UI label "Active" or "Inactive" (boolean `IsActive`). Transitions: Active to Inactive with the row button "Deactivate", Inactive to Active with "Activate", or by changing the status select in the Edit Program dialog; performed by any user holding `MasterData.Manage`; no signature; each change writes a history row (action "StatusChanged" for the button, "Updated" for the dialog). A new program is created Active by default. [src: EquipmentPage.tsx:L325-L335, L376-L392, L960-L990, L1465-L1473; EquipmentConfigurationService.cs:L363-L396] (verified)
- **Inventory status** shown on the configured equipment row - the label of the matching inventory asset's status (for example "InService"); "In Service" when no asset matches. Changing it is done in the inventory module (C5), not here. [src: EquipmentPage.tsx:L709; EquipmentConfigurationService.cs:L129] (verified)
- Equipment records, set points and configurations have no status of their own (no retire or delete action). [src: EquipmentMasterDataController.cs:L38-L124] (verified)

### MOD-TESTMASTER-001 Test Master (Microbiology)
| UI label | Stored value | Meaning | Transition |
|---|---|---|---|
| Active | IsActive = true | The test is offered for new selections in the test code pickers | "Unfreeze" row action (`PUT .../unfreeze`) |
| Frozen | IsActive = false | The test is not offered for new selections; a frozen code still displays where it is already assigned | "Freeze" row action (`PUT .../freeze`) |

New tests are created Active. Any user holding `MasterData.Manage` can freeze or unfreeze; no signature, no reason, no usage check. Message `Test "{code}" frozen.` / `Test "{code}" unfrozen.` [src: TestMasterPage.tsx:L548-L556, L636-L640, L686; TestDefinitionMasterDataService.cs:L796-L813] (verified)

Step chip "Needs configuration" (warning): not stored; shown when a step is missing a required organism or medium (typically inherited from the older migration). Chips "2-Stage" and "Transfer" describe a step's second incubation stage. [src: WorkflowStepsSection.tsx:L522-L574] (verified)

## C5 Media, strains, stock

<!-- from C5-media-strains-stock.md -->

Media lot (`MediaStatus`, enum values from backend/MicroLIMS.Domain/Enums/MediaStatus.cs:L4-L10):
- Prepared (0) - set at preparation. Screen label (computed, verified): "Pending Evaluation" until the lot is on the awaiting-approval list, then "Awaiting Approval" [src: frontend/src/modules/laboratoryConfiguration/media/components/MediaLotKpiCards.tsx:L20-L26].
  - to Active: release approved (signed, Section Head); to QuarantineFailed: release rejected or NonConform evaluation; to OutOfStock: mark out of stock (released and Active only).
- Active (1) - set by release approval only. Screen label "Released". Offered in lot lists while ExpiryDate is later than now; individual test selection does not re-check expiry (BR-MEDIA-024).
- Expired (2) - never assigned in code (GAP-502). Expiry is shown from the date.
- QuarantineFailed (3) - set by rejected release or NonConform evaluation (no reverse path found). Screen label "Rejected" (also for approval Rejected).
- Destroyed (4) - never assigned in code (GAP-502).
- OutOfStock (5) - set by mark out of stock; not reversed in code found. Screen label "Out of Stock".
Media approval (`ApprovalGateStatus`): PendingReview, Approved, Rejected [src: backend/MicroLIMS.Application/Services/MediaReleaseService.cs:L41-L51, L93-L95].
Evaluation status (`MediaEvaluationStatus`, backend/MicroLIMS.Domain/Enums/MediaEvaluationStatus.cs:L3-L7): Assigned (0) at preparation -> InProgress (1) when incubation recorded -> Completed (2) when all challenges have an outcome.
Evaluation outcome (`EvaluationOutcome`): Conform (0), NonConform (1) [src: backend/MicroLIMS.Domain/Enums/EvaluationOutcome.cs:L3-L6].
Expiring-list evaluation label: Pending / Passed / Failed (computed) [src: MediaExpiryService.cs:L47-L49].
Storage form: Plates, AgarBottles, BrothBottles, Tubes [src: MediaStorageService.cs:L51].

### MOD-CRYO-001 Cryovials
- Approval (`ApprovalGateStatus`): PendingReview (set at preparation) -> Approved (signed, approver is not the preparer) or Rejected (signed; also sets IsDestroyed). [src: CryovialService.cs:L103, L148-L166]
- IsDestroyed flag: set by Destroy (any state, no check) or by rejection. Not a status value. [src: CryovialService.cs:L166, L202-L208]
- Expired: computed from ExpiryDate (date before today); not stored. [src: MediaEvaluationEngine.cs:L88-L89; CryovialService.cs:L192-L193]
- Vials remaining: starts at NumberOfVialsPrepared; each thaw decrements; no status change at zero (thaw refused). [src: CryovialService.cs:L99, L194-L197]
- Storage: StorageCondition text plus optional StorageEquipmentId; moves allowed only while Approved and not destroyed. [src: CryovialService.cs:L258-L260]
- Transitions: PendingReview -> Approved (approve, signed); PendingReview -> Rejected + destroyed (reject, signed); Approved -> destroyed (destroy, no signature); Approved -> storage moved (no status change); Approved -> vial thawed (no status change).

### MOD-MATERIAL-001 Materials Stock
- Stock status (`StockStatus`, computed, not stored): InStock, Depleted (remaining zero or less), Expired (expiry before today). [src: backend/MicroLIMS.Domain/Enums/StockStatus.cs:L6-L10; backend/MicroLIMS.Domain/Entities/Material.cs:L84-L87]
- Document status (`MaterialDocumentStatus`): Current (0), Superseded (1), Voided (2). [src: documentation/knowledge/_raw/backend-enums.md:L350-L357]
- Transitions: upload -> Current; supersede -> old Superseded, new Current (signed reason, DocumentControl); void -> Voided with reason (DocumentControl; any non-voided document) [src: MaterialDocumentService.cs:L256-L257, L282-L289, L343-L349].
- Material type (`MaterialType`): DehydratedMedia, LyophilizedMicroorganism, Supplement, AntibioticDisc, IdentificationKit, IdentificationReagent, Chemical, Indicator, ReferenceBuffer, DisposableTool, Other, ReferenceStandard, WorkingStandard, PrimaryStandard [src: documentation/knowledge/_raw/backend-enums.md:L370-L388].
- Document type (`MaterialDocumentType`): COA, SupplierCertificate, Specification, SDS, Other [src: backend-enums.md:L359-L368].

### MOD-EQUIP-001 Equipment Inventory
- Operational status (`EquipmentOperationalStatus`): InService (0), OutOfService (1), Retired (2) [src: documentation/knowledge/_raw/backend-enums.md:L156-L162]. Change requires a comment and writes a history row; no transition rules found between values [src: EquipmentInventoryService.cs:L173-L188].
- Calibration overdue: computed (calibration due date before today), shown on register; not a status value [src: backend/MicroLIMS.Domain/Entities/EquipmentInventory.cs:L44].
- Equipment type (`EquipmentType`): Incubator, Autoclave, LafCabinet, BiologicalSafetyCabinet, WaterBath, Other [src: backend-enums.md:L165-L173].
- Document type (`EquipmentDocumentType`): CalibrationCertificate [src: backend-enums.md:L149-L154]. Document statuses (verified): the same Current / Superseded / Voided values as material documents; upload -> Current; supersede -> old Superseded, new Current; void -> Voided (reason required, repeat void refused) [src: backend/MicroLIMS.Application/Services/EquipmentDocumentService.cs:L236-L296].
- Effect of status on use (verified): only InService is required for media storage and cryovial storage-move destinations. The status is not read by micro incubator selection or media-evaluation incubation (BR-EQUIP-012, BR-EQUIP-013); those use the master-data record. HPLC, ICP and physicochemical analysis paths do refuse OutOfService and Retired (C6).

## C6 Physicochemical

<!-- from C6-physchem.md -->

Labels are from the frontend badges; values are from the backend enums.

- HPLC run: Open (label "Open") -> Completed ("Completed") via POST complete when every assigned sample is submitted or removed; Open -> Abandoned ("Abandoned") via POST abandon with reason. Enum HplcRunStatus Open=0, Completed=1, Abandoned=2. [src: backend/MicroLIMS.Domain/Enums/HplcRunStatus.cs:L5; HplcRunService.Samples.cs:L258-L280; HplcRunService.cs:L735-L751; frontend/src/modules/hplcWorkspace/components/HplcStatusBadge.tsx:L12-L31]
- HPLC SST: Pending ("Pending") -> Passed ("Passed") or Failed ("Failed") on signed confirmation. No transition back from Failed. Enum HplcSstStatus Pending=0, Passed=1, Failed=2. [src: backend/MicroLIMS.Domain/Enums/HplcSstStatus.cs:L5; HplcRunService.cs:L543; L396, L472]
- HPLC run sample: Assigned ("Assigned"; UI "Pending" until submitted) -> Removed ("Removed") with reason. Submitted state is shown as "Submitted" (Completed tone). Enum HplcRunSampleStatus Assigned=0, Removed=1. [src: HplcRunSampleStatus.cs:L6; SampleAssignmentPanel.tsx:L110; HplcRunService.Samples.cs:L140-L168]
- HPLC badges (also used for other states): available "Available", running "Running", unavailable "Unavailable", assigned "Assigned", removed "Removed", withinlimits "Within Limits", outofspecification "OOS", actionlimitexceeded "Action Limit Exceeded", alertlimitexceeded "Alert Limit Exceeded", requiresreview "Requires Review". [HplcStatusBadge.tsx:L12-L31]
- ICP run: Open -> Completed or Abandoned, as HPLC. Enum IcpRunStatus Open=0, Completed=1, Abandoned=2. [src: backend/MicroLIMS.Domain/Enums/IcpRunStatus.cs:L3; IcpRunService.Samples.cs:L234-L244; IcpRunService.cs:L261-L267]
- ICP calibration: Pending ("Pending") -> Confirmed ("Confirmed") on signed confirmation. Element chips "Pass", "Fail", "Pending". Enum IcpCalibrationStatus Pending=0, Confirmed=1. [src: IcpCalibrationStatus.cs:L3; IcpRunService.Calibration.cs:L135; frontend/src/modules/icpWorkspace/components/IcpStatusBadge.tsx:L10-L27; IcpCalibrationElementTable.tsx:L208-L212]
- ICP CCV reading: pass or fail stored; failed CCV locks element. [IcpRunService.Ccv.cs:L37; IcpElementAvailability.cs:L41-L44]
- ICP run sample: Assigned / Removed. [src: IcpRunSampleStatus.cs:L3]
- Solution preparation: InProgress ("In Progress") -> Prepared ("Prepared", on signed complete) | Cancelled ("Cancelled", reason, from In Progress); Prepared -> Expired ("Expired", automatic) | Discarded ("Discarded", reason, stock not restored). Expired, Discarded and Cancelled are final. Enum SolutionPreparationStatus InProgress=0, Prepared=1, Expired=2, Discarded=3, Cancelled=4. [src: backend/MicroLIMS.Domain/Enums/SolutionPreparationStatus.cs:L6; SolutionPreparationService.cs:L233-L361; PreparationStatusBadge.tsx:L25-L51]
- Titrant factor state (context and record): "NotStandardized" ("Not Standardized"), "BeforeEachUse" ("Restandardize Before Each Use"), "Due" ("Due for Restandardization", wording to confirm), "Valid" ("Valid"). [src: TitrantStandardizationService.cs:L320-L335; CurrentFactorCard.tsx:L25-L43]
- Titrant standardization record: Passed or failed (Failed never current). [TitrantStandardizationService.cs:L326]
- Working standard qualification: Draft (0) -> Assayed (1) on analyst submit; Assayed -> Reviewed (2) on signed review; Assayed -> Draft on Return (reason; clears results; replicates editable again); Reviewed -> Approved (3) on signed approval (only if Passed; creates or updates the WS lot); Rejected (4) on signed reject (reason). A failed assay can only be rejected. Enum WorkingStandardQualificationStatus Draft=0, Assayed=1, Reviewed=2, Approved=3, Rejected=4. [src: backend/MicroLIMS.Domain/Enums/WorkingStandardQualificationStatus.cs:L4; WorkingStandardService.SignOff.cs:L21-L140; SignOffDialog.tsx:L61-L80]
- Working standard lot status (list): "Depleted" (quantity 0), "Expired" (expiry before today), "DueSoon" (expiry within 30 days); "Valid" otherwise (KPI label "Valid"). [src: backend/MicroLIMS.Application/Services/WorkingStandardService.cs:L75-L77; frontend/src/modules/workingStandards/WorkingStandardsPage.tsx:L103-L106]
- Stage outcomes (dissolution, disintegration, weight variation): Complies (WithinLimits), NextStageRequired, DoesNotComply (OutOfSpecification). Enum DissolutionStageOutcome Complies, DoesNotComply, NextStageRequired (values in _raw/backend-enums.md:L75-L81). [src: DissolutionCalculator.cs:L125-L131]
- Specification result statuses used by physchem: WithinLimits, OutOfSpecification, RequiresReview (RSD or <LOQ), NextStageRequired, LimitsNotConfigured. [src: SpecificationEvaluator.cs:L8-L86; TitrationRecorder.cs:L270; IcpSampleEvaluator.cs:L63-L80]
- Equation type enum values (for MOD-EQTYPE-001): None 0, HplcAssay 1, SystemSuitability 2, CalibrationCurve 3, Measurement 4, GravimetricLoss 5, GravimetricResidue 6, Qualitative 7, Dissolution 8, Disintegration 9, WeightVariation 10, HplcMultiAnalyte 11, StandardComparison 12, HplcMethodAssay 13, Titration 14, IcpMethodAssay 15. [src: _raw/backend-enums.md:L127-L148; backend/MicroLIMS.Domain/Enums/EquationType.cs:L3]. Retirement of HplcAssay, HplcMultiAnalyte and StandardComparison: docs/FP_Standard_Comparison_Assay_Build_Spec.md:L11, L19 (spec says retired; code enum values still present).
- Test type enums (TitrationType, TitrationMode, TitrationCalculation, TitrationEndpoint, HplcTechnique, HplcResultMode, HplcDetectorType, IcpMethodMode, TitrantStrengthUnit, SolutionType, ShelfLifeUnit): see backend/MicroLIMS.Domain/Enums/*.cs. SolutionType MobilePhase, Diluent, Titrant. [src: backend/MicroLIMS.Domain/Enums/SolutionType.cs:L6-L7]

## C7 General masters

<!-- from C7-masters.md -->

### MOD-ITEM-001 Items
- Item active status (`Item.IsActive`) (verified):
  - `Active` (UI chip "Active"; Overview card text "Active (Sample receipt enabled)"): the item can receive samples. Initial state of an item created in the Items screen. Items created by the workbook start inactive (see workbook statuses).
  - `Frozen` (UI chip "Frozen"; Overview card text "Frozen (Sample receipt disabled)"): `ReceiveAsync` refuses new samples; assigned tests, specifications, documents and past samples stay. Transitions: Active <-> Frozen by the "Freeze item" / "Unfreeze item" button; any holder of `Items.Manage`; not signed; no reason; the workbook verification also sets Active [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L244-L249; backend/MicroLIMS.Application/Services/ItemService.cs:L105-L111].
- Item document status (`MaterialDocumentStatus`, stored as the member name; numeric order Current 0, Superseded 1, Voided 2) (verified):
  - `Current` (the document shown in the SOP / Verification Report card): set on upload [src: backend/MicroLIMS.Application/Services/ItemDocumentService.cs:L122].
  - `Superseded` (listed under "Historical SOP Versions" / "Historical Verification Reports"): set automatically on the previous Current document of the same type when a new one is uploaded; records superseding document, user, time and reason text [src: backend/MicroLIMS.Application/Services/ItemDocumentService.cs:L128-L136].
  - `Voided`: exists in the enum; no code path that sets it for item documents was found (GAP-711) [src: backend/MicroLIMS.Domain/Enums/MaterialDocumentStatus.cs:L6-L11].
- Preparation configuration status (`ApprovalGateStatus`; numeric order PendingReview 0, Approved 1, Rejected 2) (verified):
  - `PendingReview` (chip "Pending Approval"): usable immediately. Set when the analyst's first manual entry seeds the configuration, and on every save from the Items screen (including a first configuration and any edit of an approved one) [src: backend/MicroLIMS.Application/Services/ItemPreparationConfigurationService.cs:L69-L79; backend/MicroLIMS.Application/Services/SamplePreparationService.cs:L73-L83].
  - `Approved` (chip "Approved"): set by "Approve" (`Items.Manage`, no password, no second person); stores approver and time; re-approval of an approved protocol is refused [src: backend/MicroLIMS.Application/Services/ItemPreparationConfigurationService.cs:L85-L99].
  - `Rejected`: in the enum; nothing sets it for item configurations (GAP-707).

### MOD-SPEC-001 Specifications
- Result verdict (`ResultStatus`) produced by specification evaluation. The values are stored and sent as the member NAME (the enum comment says members must never be renamed) and numbering starts at 1 so that an unset value is not a pass. The earlier draft listed numeric values 0-4; they were wrong (verified) [src: backend/MicroLIMS.Domain/Enums/ResultStatus.cs:L1-L50].
  - Produced by `SpecificationEvaluator` / `SpecLimitParser`: `WithinLimits`; `OutOfSpecification` (fails the specification; triggers the OOS handling described in the OOS module, not documented here); `AlertLimitExceeded` and `ActionLimitExceeded` (count-tiered limits only); `LimitsNotConfigured` ("There was nothing to compare the result against").
  - Other members of the same enum used by other workflows (not produced by this cluster): `RequiresReview`, `Absent`, `Detected`, `PendingConfirmation`, `NotDetected`, `NextStageRequired`, `Inconclusive`.
  - The specification record itself has no status field; a specification exists or is deleted.

### MOD-RECVCFG-001 Receiving Configuration
- Reference entries (causes of testing, samplers, production stages) have an `IsActive` column that the lists filter on (only active entries are returned). No screen or service sets it to false: the Delete action removes the row (or is refused for a cause in use). The earlier draft's meaning "false = removed/deleted entry" is therefore unsupported (corrected) [src: backend/MicroLIMS.Application/Services/MasterData/ReferenceListMasterDataService.cs:L25-L26, L56-L68, L98-L106, L143-L151].

### MOD-WORKBOOK-001 Master Data Workbook
- Master data import status (`MasterDataImportStatus`; Loaded 0, Verified 1; sent to the screen as the member name) (verified):
  - `Loaded` (UI "Waiting for verification"): the signed load committed. Newly created Items and Material Master entries are inactive. Transition: Check file passes -> "Load and sign" with the loader's password (`MasterDataChanged`).
  - `Verified` (UI `Verified by {name}`): a different user signed `Reviewed` after comparing the load with the source documents. Transition: "Verify" in the register -> Verified; the Items and Material Master entries created by the load become active. No path back from Verified. There is no "rejected load" status: a wrong load can only be left unverified and corrected in the individual screens (SOP section 9 step 5) [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L138-L231; backend/MicroLIMS.Domain/Enums/MasterDataImportStatus.cs:L4-L8; frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L58-L61].

### MOD-TESTMASTER-001 Test Master (FP and RM & PM)
- Test definition active status (`TestDefinition.IsActive`) (verified):
  - `Active` (chip "Active"): offered in test pickers (items, sampling points).
  - `Frozen` (chip "Frozen"): hidden from pickers for new selections; existing orders and records unaffected. Transition: row action "Freeze" / "Unfreeze"; any holder of `MasterData.Manage` (no section check, see roles); not signed; no reason [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/TestMasterPage.tsx:L548-L557, L634-L640; backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L796-L812].

## C8 Reports, KPI, Document Control

<!-- from C8-reports-dc.md -->

### MOD-REPORTS-001 Reports
- Result Level (`ResultLevel`):
  - `WithinLimit` (UI: "Within Limit", green tone) [src: frontend/src/modules/reports/components/ReportFilterPanel.tsx:L10; frontend/src/modules/reports/utils/exportPdf.ts:L42].
  - `AlertLevel` (UI: "Alert Level", amber tone) [src: frontend/src/modules/reports/components/ReportFilterPanel.tsx:L11; frontend/src/modules/reports/utils/exportPdf.ts:L43].
  - `ActionLevel` (UI: "Action Level", orange tone) [src: frontend/src/modules/reports/components/ReportFilterPanel.tsx:L12; frontend/src/modules/reports/utils/exportPdf.ts:L44].
  - `OutOfSpecification` (UI: "Out of Spec", red error tone) [src: frontend/src/modules/reports/components/ReportFilterPanel.tsx:L13; frontend/src/modules/reports/utils/exportPdf.ts:L45].
  - `NotApplicable` (UI: "—") [src: frontend/src/modules/reports/components/ReportResultsTable.tsx:L280].
  - `LimitsNotConfigured` (UI: "—" or unconfigured limits) [src: backend/MicroLIMS.Application/Services/ResultProjectionService.cs:L107].
- Approval Status (derived from SampleStatus):
  - "Approved" (green chip): Sample has been formally approved [src: frontend/src/modules/reports/components/ReportResultsTable.tsx:L282; backend/MicroLIMS.Application/Services/ReportingQueryService.cs:L111].
  - "Rejected" (red chip): Sample has been rejected [src: backend/MicroLIMS.Application/Services/ReportingQueryService.cs:L112].
  - "Pending" (amber chip): Sample is Received, InTesting, UnderReview, UnderApproval, or RetestRequested [src: backend/MicroLIMS.Application/Services/ReportingQueryService.cs:L115].
  - "Voided": Sample was voided before completion [src: backend/MicroLIMS.Application/Services/ReportingQueryService.cs:L113].
  - "Cancelled": Sample was cancelled [src: backend/MicroLIMS.Application/Services/ReportingQueryService.cs:L114].

### MOD-REPORTS-002 Reporting backfill
- None directly (operates on existing `SampleStatus` and `ResultRecord` values).

### MOD-KPI-001 KPI and Workflow Profiles
- Workflow Profile Status (`WorkflowProfileStatus`):
  - `Draft`: Unsigned draft version (e.g. from legacy migration import or staged insert). Shown as the chip "{n} draft" / "{n} drafts" in the profile panel [src: backend/MicroLIMS.Domain/Enums/WorkflowProfileStatus.cs:L3; frontend/src/modules/kpi/components/WorkflowProfilesPanel.tsx:L166].
  - `Effective`: Active, electronically signed version currently or future effective. Read by resolver during KPI calculations [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L83, L125].
  - Transition: `Draft` -> `Effective`, triggered by electronic signature with `SignatureMeaning.MasterDataChanged`. Irreversible. [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L80-L84, L116-L126].
- Profile Coverage (`ProfileCoverage`):
  - `Configured` (green Chip: both work units and target days configured) [src: frontend/src/modules/kpi/components/WorkflowProfilesPanel.tsx:L41, L46].
  - `Partial` (amber Chip: either work units or target days configured, but not both) [src: frontend/src/modules/kpi/components/WorkflowProfilesPanel.tsx:L42, L47].
  - `NeedsConfiguration` (grey/default Chip: neither work units nor target days configured) [src: frontend/src/modules/kpi/components/WorkflowProfilesPanel.tsx:L43, L48].
- KPI Direction (`KpiDirection`):
  - `HigherIsBetter` (green when higher) [src: backend/MicroLIMS.Application/Services/Kpi/KpiTypes.cs:L5].
  - `LowerIsBetter` (green when lower) [src: backend/MicroLIMS.Application/Services/Kpi/KpiTypes.cs:L5].
  - `Neutral` (no directional valence) [src: backend/MicroLIMS.Application/Services/Kpi/KpiTypes.cs:L5].
- KPI Status (`KpiStatus`):
  - `Ok`: successfully computed with data [src: backend/MicroLIMS.Application/Services/Kpi/KpiTypes.cs:L7].
  - `NoData`: zero matching records [src: backend/MicroLIMS.Application/Services/Kpi/KpiTypes.cs:L7].
  - `Partial`: partial data or gaps present [src: backend/MicroLIMS.Application/Services/Kpi/KpiTypes.cs:L7].

### MOD-KPI-002 Analyst availability and KPI section settings
- Section setting active status: `IsDefault` boolean (true if synthesized from defaults, false if row exists) [src: backend/MicroLIMS.Application/Services/Kpi/KpiSettingsService.cs:L37, L97].

### MOD-DC-001 Document Control: library and document lifecycle
- Document Master Record Status (`DocumentRecordStatus`):
  - `Active` (1): Master record is active and valid [src: backend/MicroLIMS.Domain/Enums/DocumentRecordStatus.cs:L5].
  - `Void` (2): Master record was registered in error and voided prior to having an effective revision [src: backend/MicroLIMS.Domain/Enums/DocumentRecordStatus.cs:L6].
- Document Revision Status (`DocumentRevisionStatus`):
  - `Draft` (1): Initial draft or revision returned for corrections; files and metadata editable [src: backend/MicroLIMS.Domain/Enums/DocumentRevisionStatus.cs:L5].
  - `Cancelled` (2): Draft revision cancelled by author/controller or declined by approver; terminal inactive state [src: backend/MicroLIMS.Domain/Enums/DocumentRevisionStatus.cs:L6].
  - `InReview` (3): Submitted for technical review; open review task active, files locked [src: backend/MicroLIMS.Domain/Enums/DocumentRevisionStatus.cs:L7].
  - `AwaitingApproval` (4): Technical review completed; awaiting formal approver evaluation [src: backend/MicroLIMS.Domain/Enums/DocumentRevisionStatus.cs:L8].
  - `FutureEffective` (5): Formally approved with future target effective date; awaits automated activation sweep [src: backend/MicroLIMS.Domain/Enums/DocumentRevisionStatus.cs:L9].
  - `Effective` (6): Active, legally approved, current version; reading training active, controlled PDF accessible to all readers [src: backend/MicroLIMS.Domain/Enums/DocumentRevisionStatus.cs:L10].
  - `Superseded` (7): Historical revision replaced by a newly activated effective revision [src: backend/MicroLIMS.Domain/Enums/DocumentRevisionStatus.cs:L11].
  - `Obsolete` (8): Formally decommissioned document; retired via approved obsolescence task [src: backend/MicroLIMS.Domain/Enums/DocumentRevisionStatus.cs:L12].
- Review Task Status (`ReviewTaskStatus`): `Pending` (1, set when the task is created), `InProgress` (2, declared; no code sets it), `Completed` (3, review completed), `ReturnedForCorrection` (4, review returned), `Cancelled` (5, declared; no code sets it) (verified) [src: backend/MicroLIMS.Domain/Enums/ReviewTaskStatus.cs:L5-L9; backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L140-L150, L576-L649].
- Change Item Status (`RevisionChangeItemStatus`, stored as text): `Draft`, `Addressed`, `Deferred` [src: backend/MicroLIMS.Domain/Enums/RevisionChangeItemStatus.cs:L5-L8].
- Review Finding Status (`ReviewFindingStatus`):
  - `Open` (1): Logged by technical reviewer [src: backend/MicroLIMS.Domain/Enums/ReviewFindingStatus.cs:L5].
  - `AuthorResponded` (2): Author submitted formal response to comment [src: backend/MicroLIMS.Domain/Enums/ReviewFindingStatus.cs:L6].
  - `ReviewerVerified` (3): Reviewer verified author's response [src: backend/MicroLIMS.Domain/Enums/ReviewFindingStatus.cs:L7].
  - `Resolved` (4): Closed finding; required state for mandatory findings before review completion [src: backend/MicroLIMS.Domain/Enums/ReviewFindingStatus.cs:L8].
- Document Approval Task Status (`DocumentApprovalTaskStatus`):
  - `Pending` (1): Awaiting designated approver evaluation and signature [src: backend/MicroLIMS.Domain/Enums/DocumentApprovalTaskStatus.cs:L5].
  - `Approved` (2): Formally approved via electronic signature [src: backend/MicroLIMS.Domain/Enums/DocumentApprovalTaskStatus.cs:L6].
  - `ReturnedForCorrection` (3): Returned by approver; revision reverts to Draft [src: backend/MicroLIMS.Domain/Enums/DocumentApprovalTaskStatus.cs:L7].
  - `Declined` (4): Declined by the approver; revision transitions to Cancelled [src: backend/MicroLIMS.Domain/Enums/DocumentApprovalTaskStatus.cs:L8].
  - `Cancelled` (5): declared; no code sets it [src: backend/MicroLIMS.Domain/Enums/DocumentApprovalTaskStatus.cs:L9].
- Periodic Review Task Status (`PeriodicReviewTaskStatus`):
  - `Pending` (1): Scheduled or triggered, awaiting reviewer evaluation [src: backend/MicroLIMS.Domain/Enums/PeriodicReviewTaskStatus.cs:L5].
  - `InProgress` (2): Reviewer active on task [src: backend/MicroLIMS.Domain/Enums/PeriodicReviewTaskStatus.cs:L6].
  - `Completed` (3): Review finalized with formal outcome [src: backend/MicroLIMS.Domain/Enums/PeriodicReviewTaskStatus.cs:L7].
  - `Cancelled` (4): Cancelled administratively [src: backend/MicroLIMS.Domain/Enums/PeriodicReviewTaskStatus.cs:L8].
- Periodic Review Outcome (`PeriodicReviewOutcome`):
  - `RemainsValid` (1): Document is current; advances `NextReviewDate` [src: backend/MicroLIMS.Domain/Enums/PeriodicReviewOutcome.cs:L5].
  - `RevisionRequired` (2): Document needs updates; findings transferred to next revision draft [src: backend/MicroLIMS.Domain/Enums/PeriodicReviewOutcome.cs:L6].
  - `ObsolescenceRecommended` (3): Decommissioning recommended; creates obsolescence approval task [src: backend/MicroLIMS.Domain/Enums/PeriodicReviewOutcome.cs:L7].

Document Revision Lifecycle State Transitions (re-verified from the services; UI status labels in the library filter read "In Review", "Awaiting Approval", "Future Effective"):
| From | To | Action / trigger | Who | Signed? |
|---|---|---|---|---|
| (none) | Draft | Register document master (Revision 01) | holder of `Documents.Register` (default System Administrator, Section Head, Analyst) | No |
| Effective | new Draft revision | "Create revision" | System Administrator, Section Head, owner, assigned Author | No (reason of at least 10 characters) |
| Draft | InReview | Submit for technical review | same editors | No |
| InReview | Draft | Technical review "Return" | assigned reviewer or Section Head | No (notes optional) |
| InReview | AwaitingApproval | "Complete Review" (all mandatory findings Resolved) | assigned reviewer or Section Head | No |
| AwaitingApproval | Draft | "Return for Correction" | assigned approver, Section Head or System Administrator (never author or technical reviewer) | No (reason of at least 10 characters) |
| AwaitingApproval | Cancelled | "Decline / Cancel" | same | No (reason of at least 10 characters) |
| AwaitingApproval | Effective | "Approve", effective date today or earlier; the previous Effective revision becomes Superseded in the same save | same | Yes: password, meaning Approved |
| AwaitingApproval | FutureEffective | "Approve", effective date after today | same | Yes: password, meaning Approved |
| FutureEffective | Effective | Worker activation on the date (60-minute cycle by default); previous Effective -> Superseded; training cascade | system process | No |
| Draft | Cancelled | "Cancel draft revision" | System Administrator, Section Head, owner, assigned Author | No (reason of at least 10 characters) |
| Effective | Obsolete | NOT IMPLEMENTED: no code sets Obsolete (DISCREPANCY, GAP-818) | n/a | n/a |
| master Active | master Void | "Void document master" (never had an Effective or Superseded revision) | Section Head only | No (reason of at least 10 characters) |
[src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L124-L175, L510-L620; backend/MicroLIMS.Application/Services/DocumentControl/DocumentRevisionService.cs:L59-L193; backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L55-L160, L565-L649; backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L433-L602; backend/MicroLIMS.Application/Services/DocumentControl/DocumentEffectiveDateService.cs:L46-L230]

### MOD-DC-002 Document Control: reading and training
- Training Assignment Status (`TrainingAssignmentStatus`, 11 declared values) [src: backend/MicroLIMS.Domain/Enums/TrainingAssignmentStatus.cs:L5-L15]:
  - Set by code (verified): `Assigned` (2, created by the cascade), `Reading` (3, set by the first reading-progress call), `Acknowledged` (4, set by the acknowledgement), `Overdue` (8, set by the escalation evaluation at the due time), `SupersededIncomplete` (9, set when a newer revision is activated), `Cancelled` (11, set by the obsolescence closure routine that is never called, GAP-818) [src: backend/MicroLIMS.Application/Services/DocumentControl/TrainingAssignmentService.cs:L106-L112, L462; backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs:L405-L430; backend/MicroLIMS.Application/Services/DocumentControl/DocumentEscalationService.cs:L151-L158].
  - Declared but no code path found that sets them: `NotAssigned` (1), `KafPending` (5), `CompletedPassed` (6), `Failed` (7), `TrainedOnSupersededOnly` (10, used only as a matrix cell state). The matrix counts Acknowledged and CompletedPassed as completed. The Reading List tabs "To read", "Overdue", "Completed", "Superseded", "All" group these states (the exact grouping was not extracted).
- Training Matrix Cell Status:
  - `Qualified`: current revision acknowledged [src: backend/MicroLIMS.Application/Services/DocumentControl/TrainingMatrixService.cs:L149].
  - `Pending`: assignment open and within due date [src: backend/MicroLIMS.Application/Services/DocumentControl/TrainingMatrixService.cs:L165].
  - `Overdue`: assignment open and past due date [src: backend/MicroLIMS.Application/Services/DocumentControl/TrainingMatrixService.cs:L159].
  - `TrainedOnSupersededOnly`: user qualified on earlier revision, not current [src: backend/MicroLIMS.Application/Services/DocumentControl/TrainingMatrixService.cs:L154, L174].
  - `NotAssigned`: not required by curriculum [src: backend/MicroLIMS.Application/Services/DocumentControl/TrainingMatrixService.cs:L123].

### MOD-DC-003 Document Control: audit trail
- Audit Action Categories (`AuditActionCategory`): Document, Review, Approval, PeriodicReview, Configuration, Security [src: backend/MicroLIMS.Domain/Entities/AuditLog.cs:L1-L30; frontend/src/modules/documentControl/types/documentControlTypes.ts:L15-L25].

### MOD-DC-004 Document Control: configuration
- Statuses: `IsActive` boolean on DocumentType, DocumentDepartment, DocumentSection; `IsEnabled` boolean on DocumentNumberingConfiguration [src: backend/MicroLIMS.Domain/Entities/DocumentType.cs; backend/MicroLIMS.Domain/Entities/DocumentNumberingConfiguration.cs].

### MOD-DC-005 Document escalations (backend)
- Document Escalation Levels (`DocumentEscalationLevel`): `ApproachingDue` (1), `Due` (2), `Overdue` (3), `CriticalOverdue` (4, declared; the evaluation creates no record at this level) [src: backend/MicroLIMS.Domain/Enums/DocumentEscalationLevel.cs:L5-L8; backend/MicroLIMS.Application/Services/DocumentControl/DocumentEscalationService.cs:L240-L244].
- Document Escalation Status (`DocumentEscalationStatus`): `Raised` (1, created by the worker), `Resolved` (2, set by a manager's resolution), `Cancelled` (3) and `Superseded` (4) are declared; no code sets them [src: backend/MicroLIMS.Domain/Enums/DocumentEscalationStatus.cs:L5-L8; backend/MicroLIMS.Application/Services/DocumentControl/DocumentEscalationService.cs:L191, L387].
