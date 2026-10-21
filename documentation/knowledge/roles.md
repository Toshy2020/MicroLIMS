# Roles

## C1 Platform & administration

<!-- from C1-platform.md -->

### MOD-AUTH-001 Authentication, session, password
- No permission code gates sign-in, sign-out, forgot password, or change password (`/api/auth/change-password` is `[Authorize]` only) [src: backend/MicroLIMS.API/Controllers/AuthenticationController.cs:L96-L97].
- Password-reset request and confirm, admin-recovery confirm, login and refresh are anonymous (rate-limited) [src: backend/MicroLIMS.API/Controllers/AuthenticationController.cs:L33-L80].
- Administrator-assisted recovery is INITIATED under `Users.Manage` (UserController `POST /api/users/{id}/admin-password-recovery`), the code is then entered anonymously [src: backend/MicroLIMS.API/Controllers/UserController.cs:L120-L121].
- Own-account restriction: a System Administrator cannot start admin recovery on own account [src: backend/MicroLIMS.Application/Services/AdminPasswordRecoveryService.cs:L51].
- While Must Change Password is set, the server refuses every other call with 403 (MustChangePasswordFilter) [src: backend/MicroLIMS.API/Filters/MustChangePasswordFilter.cs:L21-L24].
- Default holders: none needed (all signed-in users).

### MOD-NAV-001 Application shell and navigation
- Sidebar item visibility: `permission` per item (any-of); parent hidden when no child is visible [src: frontend/src/routes/menuConfig.ts:L216-L266]. Page guards: `PermissionRoute`. Server re-checks every API call.
- Lab groups: MICRO items need MICRO membership; FP items need FP membership; area `fp` / `rmpm` [src: frontend/src/routes/menuConfig.ts:L161-L168, L255-L260].
- Document Control "Audit Trail" link: role type Reviewer, Section Head or System Administrator only [src: frontend/src/routes/menuConfig.ts:L101-L103, L118].

### MOD-NOTIF-001 Notifications
- Endpoints have no permission attribute (`DashboardController` class `[Authorize]`); each user sees own notifications only (`UserId` filter) [src: backend/MicroLIMS.API/Controllers/DashboardController.cs:L13, L58-L71; backend/MicroLIMS.Application/Services/DashboardNotificationService.cs:L101-L107].
- Who receives which type (role type plus sections): ApprovalWaiting and PendingPreparationConfigApproval -> role type SectionHead or SystemAdministrator; ReviewWaiting -> Reviewer, SectionHead or SystemAdministrator; TestReturnedForRevision -> the assigned analyst; IncubationReady / MediaExpiry -> any user with accessible sections [src: backend/MicroLIMS.Application/Services/DashboardNotificationService.cs:L128-L210]. Section scope: `GetAccessibleSectionIdsAsync` [src: L131-L133].

### MOD-DASH-001 Dashboards
- Dashboard variant: by role TYPE (JWT claim) [src: frontend/src/modules/dashboard/DashboardPage.tsx:L40-L44].
- `GET /api/dashboard` (summary) and `/kpi-deltas`, `/todays-work`, `/incubation-overview`, `/status-distribution`, `/category-distribution`, `/monthly-trend`: no policy attribute; data scope = lab membership sections [src: backend/MicroLIMS.API/Controllers/DashboardController.cs:L43-L93].
- `GET /api/dashboard/section-head`: `Dashboards.LabOverview` (default: System Administrator, Section Head) [src: backend/MicroLIMS.API/Controllers/DashboardController.cs:L97-L98; knowledge/_raw/verified-roles-permissions.md §2.3].
- `GET /api/dashboard/reviewer`: `Dashboards.Review` (default: System Administrator, Section Head, Reviewer) [src: backend/MicroLIMS.API/Controllers/DashboardController.cs:L102-L103].
- `GET /api/dashboard/my-tasks` and `/analyst-metrics`: role TYPE must be Analyst, otherwise Forbid [src: backend/MicroLIMS.API/Controllers/DashboardController.cs:L86, L110].
- Lab scope: a user may request a laboratory only if a member: "You are not a member of this laboratory." [src: knowledge/_raw/verified-roles-permissions.md §7; backend/MicroLIMS.Application/Services/DashboardLabScopeService.cs:L12-L60].
- See GAP-055 (dashboard variant follows role type while data follows permissions).

### MOD-USERS-001 User management
- Users.Manage (`[Authorize(Policy)]` on UserController class): list, get, create, edit, role, status, deactivate, email, unlock, password-reset, admin recovery, force password change, delete [src: backend/MicroLIMS.API/Controllers/UserController.cs:L22-L140]. Default holders: System Administrator only (SectionHead "-") [src: knowledge/_raw/verified-roles-permissions.md §2.3; 01_APPLICATION_MAP §5.2 PERM-USERS-MANAGE].
- Not gated by Users.Manage: `GET /api/users/directory` (any signed-in user; active users only) [src: backend/MicroLIMS.API/Controllers/UserDirectoryController.cs:L10-L20]; `GET /api/users/analysts` gated by `Samples.AssignAnalyst` [src: backend/MicroLIMS.API/Controllers/UserAnalystController.cs:L11].
- Laboratory membership write: `LaboratoryOrganizationController` (Users.Manage per verified §7) [src: knowledge/_raw/verified-roles-permissions.md §7].
- Role-type rules: a user cannot change own role; cannot disable self; cannot delete self; last active System Administrator cannot be deleted [src: backend/MicroLIMS.Application/Services/UserService.cs:L163-L172, L206-L210; backend/MicroLIMS.Application/Services/UserDeletionService.cs:L60-L75].
- Segregation text (exact): "System Administrators cannot modify their own role." / "System Administrators cannot disable their own account." / "You cannot permanently delete your own account." / "Cannot delete the last active System Administrator account. Promote another user to System Administrator first."

### MOD-ROLES-001 Roles and permissions
- Roles.Manage on every `/api/roles` and `/api/permissions` endpoint (RoleController class policy) [src: backend/MicroLIMS.API/Controllers/RoleController.cs:L20-L70]. Default holders: System Administrator only.
- Role TYPE still drives behaviour that permissions do not change: media product code change (Section Head role type only; System Administrator refused) [src: backend/MicroLIMS.API/Controllers/MediaMasterDataController.cs:L52-L56]; Analyst-only dashboard endpoints; analyst-only own-test filtering [src: knowledge/_raw/verified-roles-permissions.md §3].
- A custom role is treated as its base TYPE for every RoleType check [src: knowledge/_raw/verified-roles-permissions.md §3].
- No guard stops an administrator from removing `Roles.Manage` from the System Administrator or Section Head role [src: backend/MicroLIMS.Application/Services/RoleService.cs:L126-L171; knowledge/_raw/verified-roles-permissions.md §3].

### MOD-SIG-001 Electronic signatures and segregation of duties
- Permission: none for signing itself; each signed action is gated by its own module's permission (e.g. Samples.Review, Samples.Approve, Hplc.Operate) [src: knowledge/_raw/verified-roles-permissions.md §6].
- Signed actions (26, all in verified §6): sample technical review; sample approval decision (Approve / Reject / New sample request / Retest retained sample); section testing closed; add laboratory; sample correction; sample void; sample preparation confirmation; cryovial batch approve / reject; media lot release approve / reject; media product code change; solution preparation completion; titrant standardization; titrant-due acknowledgement; HPLC system suitability confirmation; ICP calibration; working standard replicate submission, review, approval, rejection; KPI workflow profile version; master data workbook load and verify; document revision approval; physchem result recording (single shared path); dissolution, disintegration and weight variation results [src: knowledge/_raw/verified-roles-permissions.md §6 items 1-26].
- Not signed (verified): microbiology plate-count entry, pathogen steps, biochemical decision, return to analyst, incubator changes, user and role administration (GAP-009, GAP-010, GAP-028).
- Segregation rules (exact text) [src: knowledge/_raw/verified-roles-permissions.md §5]:
  - "You cannot review a sample you tested." [backend/MicroLIMS.Application/Services/SampleReviewService.cs:L109-L112]
  - "You cannot approve a sample you reviewed." [backend/MicroLIMS.Application/Services/SampleApprovalService.cs:L162-L163]
  - "You cannot approve a sample you tested." [backend/MicroLIMS.Application/Services/SampleApprovalService.cs:L167-L170]
  - "You cannot approve a media lot you prepared or evaluated." [backend/MicroLIMS.Application/Services/MediaReleaseService.cs:L81-L82]
  - "You cannot approve a cryovial batch you prepared." [backend/MicroLIMS.Application/Services/CryovialService.cs:L151-L152]
  - "A reviewer cannot decide on a result they performed." (error code SEGREGATION_OF_DUTIES_VIOLATION) [backend/MicroLIMS.Application/Workflows/TestWorkflow/PathogenConfirmationRecorder.cs:L780-L782]
  - Working standard: "The analyst who submitted the qualification cannot review it." / "...cannot approve it." / "The reviewer cannot also approve the qualification." / "The reviewer cannot also reject the qualification at approval." [backend/MicroLIMS.Application/Services/WorkingStandardService.SignOff.cs:L22-L23, L76, L85-L88]
  - Workbook: "The person who loaded the workbook cannot verify it. Ask a second person." [backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L199-L200]
  - Document Control: author / reviewer / periodic review messages (C8 owns detail) [knowledge/_raw/verified-roles-permissions.md §5].
- Guard is role-agnostic and compares user ids only; System Administrator is not exempt [src: backend/MicroLIMS.Application/Services/SegregationOfDutiesGuard.cs:L6-L10].

### MOD-MSG-001 Messages
- `[Authorize]` only; no permission gate. `Messages.Use` is NOT-ENFORCED (granting or revoking has no effect) [src: backend/MicroLIMS.API/Controllers/MessageController.cs:L11-L12; knowledge/_raw/verified-roles-permissions.md §1].
- Participant-only access to a conversation; others get "You are not a participant in this conversation." [src: backend/MicroLIMS.Application/Services/MessageService.cs:L159, L302, L342].

### MOD-DISC-001 Discussions
- `[Authorize]` class-level; `Discussions.View` and `Discussions.Create` are NOT-ENFORCED [src: backend/MicroLIMS.API/Controllers/DiscussionController.cs:L13; knowledge/_raw/verified-roles-permissions.md §1].
- `Discussions.EditAny` (claim, default System Administrator and Section Head) lets a user edit, mark important, delete posts and edit comments of others [src: backend/MicroLIMS.API/Controllers/DiscussionController.cs:L24; backend/MicroLIMS.Application/Services/DiscussionService.cs:L372-L373, L416-L417, L430-L431, L538-L539].
- Authors may always edit or delete their own posts and comments [src: backend/MicroLIMS.Application/Services/DiscussionService.cs:L372].

### MOD-ERR-001 Error Monitoring
- `System.ViewErrorLog` on the whole ErrorMonitoringController (class policy) [src: backend/MicroLIMS.API/Controllers/ErrorMonitoringController.cs:L20-L21]. Enforced. Default holders: System Administrator only (Section Head "-") [src: knowledge/_raw/verified-roles-permissions.md §2.3].

### MOD-AUDIT-001 Audit Trail
- `Audit.View` on AuditController (class policy) for search, entity history, traceability and login history [src: backend/MicroLIMS.API/Controllers/AuditController.cs:L11-L51]. Default holders: System Administrator, Section Head [01_APPLICATION_MAP §5.2 PERM-AUDIT-VIEW].
- Frontend route guard `Audit.View` [src: frontend/src/routes/routes.ts:L220].
- Reviewer and Analyst do not hold it by default.

### MOD-AUDIT-002 Login history (backend only)
- Same class policy `Audit.View` [src: backend/MicroLIMS.API/Controllers/AuditController.cs:L51].

### MOD-SECAUDIT-001 Security audit events (backend only)
- `System.ViewSecurityAudit` NOT-ENFORCED; SysAdmin-only default grant; no endpoint checks it [src: knowledge/_raw/verified-roles-permissions.md §1].

### MOD-SIG-002 Signature trail (backend only)
- `Signatures.Manage` on SignaturesController class [src: backend/MicroLIMS.API/Controllers/SignaturesController.cs:L15, L25]. Default holders: System Administrator, Section Head.

### MOD-ARCHIVE-001 Archived records
- `[Authorize]` class-level only; no permission and no per-record scope check in the controller [src: backend/MicroLIMS.API/Controllers/ArchivedRecordsController.cs:L16-L50]. REQUIRES CONFIRMATION (GAP-108 area) whether the sample's laboratory scope should apply.

### MOD-LEGACY-001 Legacy and redirect routes
- No permission of its own; redirects run before permission checks on the target page.

## C2 Sample flow & micro testing

<!-- from C2-sample-testing.md -->

Permission codes come from `documentation/knowledge/_raw/verified-roles-permissions.md` (verified; wins over backend-permissions-roles-auth.md). Backend enforcement point = controller policy; UI gate = frontend guard.

| Permission | Enforced by (backend) | UI gate | Default holders (seed) |
|---|---|---|---|
| `Samples.Receive` | POST `/api/samples/{id}/laboratories` [SampleController.cs:L148]; also a claim check inside POST `/api/samples` and GET `/receipt-labs`: holders may target any laboratory, others only their own [SampleController.cs:L88-L89, L101-L102; ReceiptLabGuard.cs:L25-L30] (verified) | Route `/receiving` (any-of with ReceiveOwnLab) [AppRoutes.tsx:140-142]; "Add Laboratory" menu item [SampleActionMenu.tsx:73] | SectionHead, SystemAdministrator [DbSeeder.cs:L524-546 per verified file] |
| `Samples.ReceiveOwnLab` | POST `/api/samples` [SampleController.cs:L82] (Product / RM / PM only) | same route | SectionHead, Reviewer, Analyst, SystemAdministrator |
| (none: signed in only) | POST `/api/em/receive`, `/api/em/prepare`, `/api/water/receive`, `/api/water/prepare`, `/api/aftercleaning/receive`, `/api/aftercleaning/prepare` carry `[Authorize]` only: no permission policy and no lab-scope call [EMController.cs:L14-L35; WaterController.cs:L16-L40; AfterCleaningController.cs:L19-L48]. The authorization matrix lists all four roles for them [MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt:L1-L2, L138-L139, L522-L523] (verified; GAP-227) | New Sample dialog category tiles (UI gate not read) | any signed-in role |
| (none: signed in only) | POST `/api/sample-preparation` (manual), `/confirm`, `/batch-confirm`, GET `/groups`: `[Authorize]` plus `EnsureSampleAccessAsync` section check; no permission code [SamplePreparationController.cs:L26-L99] (verified) | "Prepare Sample" | any signed-in role with access to the sample's section |
| `Items.Manage` | PUT `/api/items/{id}/preparation-configuration`, POST `.../approve`, GET `/api/preparation-configurations/pending` [ItemPreparationConfigurationController.cs:L34-L67] (verified) | screen not located (GAP-214) | SectionHead, SystemAdministrator |
| `Samples.Correct` | PUT `/correct`, POST `/void` [SampleController.cs:L107, L136] | "Edit Details", "Void Sample" [SelectedSampleTestingPanel.tsx:148-196; SampleActionMenu.tsx:73] | SectionHead, Reviewer |
| `Samples.AssignAnalyst` | PUT `/assign-analyst` [SampleController.cs:L127] (verified) | "Assign Analyst" [SampleActionMenu.tsx:52] | SectionHead, SystemAdministrator |
| `Samples.TrackAll` | controller class policy on `/api/samples/tracking` [SampleTrackingController.cs:L16] (verified) | route `/receiving/tracking` | SectionHead, SystemAdministrator (verified) |
| `Samples.Review` | `/api/samples/{id}/review/*` class policy [SampleReviewController.cs:L19] | ReviewGate in summary (C3) | Reviewer, SectionHead |
| `TestWorkflow.Execute` | all `/api/test-workflow/*` and `/api/pathogen-session/*` (class policy) [TestWorkflowController.cs:L23; PathogenSessionController.cs:L15] | workspace | SectionHead, Reviewer, Analyst |
| `TestWorkflow.Supervise` | POST `/override-minimum-duration` [TestWorkflowController.cs:L543-544]; POST pathogen-session `/reset` [PathogenSessionController.cs:L171-172] | "Skip Wait" (IncubationWaitingView, TestWorkflowDialog), "Reset Session" | SectionHead |
| `TestWorkflow.BiochemicalDecision` | POST `/api/test-workflow/results/{id}/biochemical-decision` [TestWorkflowController.cs:L527-528] | no UI caller (TestWorkflowService.ts:268-270) | SectionHead, Reviewer |
| `Media.Prepare` | media lot selection (not read here) | n/a | SectionHead, Reviewer, Analyst |

- Grouped actions: for a user whose role type is Analyst, the actionable-groups list is limited to tests assigned to them unless scope "all" is requested, and a batch start skips a test assigned to another analyst with "Test order is assigned to another analyst." [src: backend/MicroLIMS.Application/Services/GroupedTestActionService.cs:L77-L85, L477-L481] (verified). The single-test endpoints (`select-media`, `record-result`, batch results, pathogen steps) check section access only and have no assigned-analyst check [src: TestWorkflowController.cs:L136-L192, L391-L442; PathogenSessionController.cs] (verified; GAP-219).
- Preparation ownership: only the analyst already assigned to the sample's non-superseded test orders may prepare a Product / RM / PM sample; this applies to every role including SectionHead (no exemption on the server). EM, After Cleaning and Water preparation have no ownership check; the user who prepares becomes `AssignedAnalystId` on every test order created. [src: SamplePreparationService.cs:L157-L169; EMWorkflowEngine.cs:L97; WaterWorkflowEngine.cs:L132; AfterCleaningWorkflowEngine.cs:L117] (corrected)
- Lab scoping: controllers call `IUserSectionScopeService` (e.g. SampleController.cs:L59, TestWorkflowController.cs:L28, TestingWorkspaceController via ITestWorkspaceService). Frontend LabWorkspaceRoute checks membership via useMyLabs. [src: frontend/src/modules/receivingTesting/LabWorkspaceRoute.tsx:20-28]
- Receipt lab guard: "You can receive samples only for your own laboratory." [src: backend/MicroLIMS.Application/Services/ReceiptLabGuard.cs:L25-30 per verified file]
- Analyst assignment: target user must be active: "User '{name}' is not active." [src: backend/MicroLIMS.Application/Services/SampleAssignmentService.cs:37]; user not found: "User {id} not found." [line 34]. (verified)
- Analyst role check on assignee: none in SampleAssignmentService (only IsActive); any active user, of any role, can be assigned (verified; GAP-213). The assignment is also not limited to the assignee's laboratory. The service itself does not apply section scope; the controller does (`EnsureSampleAccessAsync` on the acting user). [src: SampleController.cs:L130]
- Segregation of duties (verified file §5): review "You cannot review a sample you tested." [SampleReviewService.cs:112]; approve "You cannot approve a sample you reviewed." [SampleApprovalService.cs:163]; approve "You cannot approve a sample you tested." [SampleApprovalService.cs:170]; biochemical decision "A reviewer cannot decide on a result they performed." (code SEGREGATION_OF_DUTIES_VIOLATION) [PathogenConfirmationRecorder.cs:780-782]. "Performed a test" = assigned analyst; preparer of the sample; author of any Result, CountTestReading, PathogenObservation, WorkflowStepResult (submitter), TestAnalysis (physicochemical), or ConfirmatoryPlateObservation (recorder). The check compares user ids only and is role-agnostic, so SystemAdministrator is not exempt. [src: backend/MicroLIMS.Application/Services/SegregationOfDutiesGuard.cs:L11-L54] (verified; corrected: TestAnalysis was missing from the Haiku list)
- Consequence for batch (EM, After Cleaning, Water) tests: the user who prepared the sample becomes the assigned analyst on every test order created at preparation and so counts as having "performed" those tests for the review and approval checks. [src: EMWorkflowEngine.cs:L97; SegregationOfDutiesGuard.cs:L33] (verified)
- Two-analyst retest rule (C3 cross ref): "Two analysts must be assigned - one per new sample - when requesting a new sample." / "The two new samples must be assigned to two different analysts." [SampleApprovalService.cs:267-269]

---

## C3 Review, approval, reports out

<!-- from C3-review-approval.md -->

### MOD-REVIEW-001 Technical review and return to analyst
- Server gate: SampleReviewController is class-level Policy Samples.Review [src: backend/MicroLIMS.API/Controllers/SampleReviewController.cs:L19]. Return endpoint also checks EnsureTestOrderAccessAsync for the test order's section [src: SampleReviewController.cs:L48-L54].
- Permission: Samples.Review, Enforced. Default grants: SystemAdministrator Y (all 50 codes), SectionHead Y, Reviewer Y, Analyst - [src: documentation/knowledge/_raw/verified-roles-permissions.md:L85-L88]. (verified; the extractor's L428 reference pointed at an empty line)
- UI gate: canReview / canReturn flags as in SCR-REVIEW-001.
- Analyst-only filter: an Analyst-type user sees only own tests unless scope is "all" [src: documentation/knowledge/_raw/verified-roles-permissions.md:L110].
- Section scope errors: "This test belongs to a laboratory section you are not assigned to." [src: backend/MicroLIMS.Application/Services/SampleSectionRollup.cs:L130, L142].
- SoD (sample review): "You cannot review a sample you tested." Applies to every current test in the section being reviewed [src: backend/MicroLIMS.Application/Services/SampleReviewService.cs:L109-L112; SegregationOfDutiesGuard.cs:L25-L54]. Performer = assigned analyst, sample preparer, any result/count reading/analysis/pathogen observation/workflow step result/confirmatory plate observation on that order [src: verified-roles-permissions.md:L120].
- Guard is role-agnostic: no role bypasses it, including System Administrator [src: verified-roles-permissions.md:L118].

### MOD-APPROVAL-001 Approval, retest, new sample, close testing
- Server gate: SampleApprovalController is class-level Policy Samples.Approve [src: backend/MicroLIMS.API/Controllers/SampleApprovalController.cs:L34]. Routes: POST /api/samples/{id}/approval/decide; POST /api/samples/{id}/approval/sections/{sectionId}/close [src: SampleApprovalController.cs:L48, L58].
- Permission: Samples.Approve, Enforced. Default grants: SystemAdministrator Y, SectionHead Y, Reviewer -, Analyst - [src: verified-roles-permissions.md:L85-L88, L90]. (verified; L429 in the extractor's draft was an empty line)
- Close testing scope: "This test belongs to a laboratory section you are not assigned to." [src: backend/MicroLIMS.Application/Services/SectionClosureService.cs:L43-L45]. Close testing is on the same controller, so it also needs Samples.Approve [src: SampleApprovalController.cs:L34, L58]. (verified)
- Section scope: GetAccessibleSectionIdsAsync returns null for unrestricted users; when not null, the section must be in the list or the action is refused [src: SampleSectionRollup.cs:L129-L130, L140-L142]. (verified)
- SoD (sample approval):
  - "You cannot approve a sample you reviewed." (approver equals the section's reviewer) [src: backend/MicroLIMS.Application/Services/SampleApprovalService.cs:L162-L163].
  - "You cannot approve a sample you tested." (approver performed any test in the section) [src: SampleApprovalService.cs:L167-L170].
  - Reviewer/approver rule applies to System Administrator too [src: verified-roles-permissions.md:L90, L118].
- Retest analyst rules: both new-sample analysts must be active, not locked, Analyst role type [src: SampleApprovalService.cs:L272-L277]. Analyst may not be the AssignedAnalystId of a selected test [src: SampleApprovalService.cs:L279-L283]. Section scope is not applied to the two analysts (see GAP-302).

### MOD-COA-001 Sample report and Certificate of Analysis
- Report and CoA pages: no route guard [src: frontend/src/routes/AppRoutes.tsx:102-103].
- Summary API: class-level Authenticated; per-sample access check EnsureSampleAccessAsync [src: backend/MicroLIMS.API/Controllers/SampleSummaryController.cs:L16-L19, L31-L33].
- Signature trail endpoint GET /api/signatures requires Signatures.Manage [src: backend/MicroLIMS.API/Controllers/SignaturesController.cs:L15, L25]. Permission Signatures.Manage: SectionHead Y, SystemAdministrator Y, Reviewer -, Analyst - [src: verified-roles-permissions.md:L85-L88]. (verified) The report and CoA pages do not call this endpoint; they read signatures from the summary [src: SampleReportPage.tsx:117; SampleCoaPage.tsx:281].
- Archived records API: class-level [Authorize] only [src: backend/MicroLIMS.API/Controllers/ArchivedRecordsController.cs:L16, L26, L41]. Archived copy download is not permission-gated beyond sign-in.
- Summary PDF/Word endpoints: Authenticated + sample access check [src: SampleSummaryController.cs:L44-L53].

### MOD-OOS-001 OOS Tracking
- Controller: OosTrackingController and OosInvestigationDocumentController, class-level Policy Oos.Manage [src: backend/MicroLIMS.API/Controllers/OosTrackingController.cs:L15-L16; OosInvestigationDocumentController.cs:L14-L15].
- Permission Oos.Manage, Enforced. Default grants: SystemAdministrator Y, SectionHead Y, Reviewer -, Analyst - [src: verified-roles-permissions.md:L85-L88]. (verified; L466 in the draft was an empty line)
- Every document endpoint (list, upload, content, supersede, void) first calls EnsureOosGroupAccessAsync for the caller and group code [src: OosInvestigationDocumentController.cs:L32, L52, L64, L86, L102]. The OOS document actions are not electronically signed (no SignAsync call) and have no separation-of-duties check. (verified; see GAP-313)
- Section filter for groups: samples visible when section-scoped user has tests in the group [src: backend/MicroLIMS.Application/Services/OosTrackingService.cs:L24-L34].

### MOD-RESULT-001 Results list (backend)
- Class-level [Authorize] only; no permission code [src: backend/MicroLIMS.API/Controllers/ResultController.cs:L10].

### Signatures for this cluster
- Review: "Reviewed" meaning, signed inside the same transaction as the state change; a wrong password throws before any change is written [src: backend/MicroLIMS.Application/Services/SampleReviewService.cs:L118-L123; ReviewGateService.cs:L29-L37; verified-roles-permissions.md:L147]. (verified)
- Signature type names: the enum SignatureMeaning holds Reviewed, Approved, Rejected, RetestRequested, InvestigationOrdered, PreparationConfirmed and later values including TestingClosed [src: backend/MicroLIMS.Domain/Enums/SignatureMeaning.cs:L3-L17]. (verified for the first values; the rest belong to other clusters)
- Approval decision meanings: Approve = Approved; Reject = Rejected; NewSampleRequest = Rejected; RetestRetainedSample = RetestRequested [src: backend/MicroLIMS.Application/Services/SampleApprovalService.cs:L293-L300].
- Close testing meaning: TestingClosed [src: backend/MicroLIMS.Application/Services/SectionClosureService.cs:L62-L65].
- Password failure text: "Password verification failed. The signature was not applied." Throttle after 5 failed attempts in 15 minutes: "Too many failed signature attempts. Wait 15 minutes, then try again. The signature was not applied." Signature failures do not lock the login account [src: verified-roles-permissions.md:L142-L143].
- Return to Analyst: no signature; no SoD check on the return path [src: backend/MicroLIMS.Application/Services/SampleReviewService.cs:L157-L269]. See GAP-009, GAP-300, GAP-301.
- Out of this cluster but same mechanism: pathogen biochemical decision SoD ("A reviewer cannot decide on a result they performed.") [src: verified-roles-permissions.md:L127].

## C4 Monitoring & micro configuration

<!-- from C4-monitoring-microconfig.md -->

Common to all modules in this cluster (verified against `verified-roles-permissions.md` and the controllers):
- `MasterData.Manage` is held by default by the System Administrator role (all 50 permission codes) and the Section Head role; it is not held by Reviewer or Analyst. Roles and their permission sets are administrator-editable, so a custom role can hold it; the token carries the permission claims until it expires. [src: documentation/knowledge/_raw/verified-roles-permissions.md:§3 (role table lines 82-90); backend/MicroLIMS.Persistence/Seed/DbSeeder.cs:L523-L546; backend/MicroLIMS.Shared/Constants/PermissionConstants.cs:L29] (verified)
- Every page in this cluster is a route under `PermissionRoute code=MASTER_DATA_MANAGE`; every write endpoint is `[Authorize(Policy = MasterData.Manage)]`; every read endpoint is `[Authorize]` only (any signed-in user can call the GET endpoints directly even though the pages are guarded). [src: frontend/src/routes/AppRoutes.tsx:L222-L236; the eight controllers under backend/MicroLIMS.API/Controllers (class-level `[Authorize]`, per-action policy)] (verified)
- Segregation of duties: none in this cluster. The same user can create, change and delete any configuration; there is no second-person review and no approval of configuration changes (verified: none of the services reads the acting user to refuse a change). 
- Preparation screens for EM / Water / After Cleaning need only sign-in; see C2 (MOD-PREP-001, GAP-227).
- Lab scoping: only equipment and Test Master are section-scoped (details per module). EM, Water, After Cleaning, Organisms and Media configuration are not laboratory-scoped.

### MOD-EM-001 Environmental Monitoring configuration
- Write (departments, rooms, room test configurations: create, update, delete): `MasterData.Manage`. [src: backend/MicroLIMS.API/Controllers/EnvironmentalMonitoringMasterDataController.cs:L36-L88] (verified)
- Read (`GET departments`, `rooms`, `room-test-configurations`): `[Authorize]` only. [src: EnvironmentalMonitoringMasterDataController.cs:L32-L34, L51-L53, L71-L73] (verified)
- Page guard: `MasterData.Manage`. [src: frontend/src/routes/AppRoutes.tsx:232] (verified)
- Default holders: System Administrator, Section Head. No role-type restriction, no lab scoping.

### MOD-WATER-001 Water configuration
- Write (sampling points, water departments, sampling configurations): `MasterData.Manage`; read `[Authorize]`. [src: backend/MicroLIMS.API/Controllers/WaterMasterDataController.cs:L32-L87] (verified)
- Page guard: `MasterData.Manage`. [src: AppRoutes.tsx:231] (verified)
- INTENDED role gate differs in mechanism: the water parity spec says the mutating endpoints carry `[Authorize(Roles = SectionHead + SystemAdministrator)]`; the code uses the permission policy instead (same default holders, but a custom role can differ). DISCREPANCY (minor), see BR-WATER-009. [src: docs/superpowers/specs/2026-08-17-water-config-em-parity-design.md:L136-L138 (the sentence "All mutating endpoints carry the same ..."); WaterMasterDataController.cs:L36-L87]

### MOD-AC-001 After Cleaning configuration
- Write (machines, machine parts, machine part configurations): `MasterData.Manage`; read (`GET machines`, `GET machine-part-configurations`) `[Authorize]`. [src: backend/MicroLIMS.API/Controllers/AfterCleaningMasterDataController.cs:L32-L82] (verified)
- Page guard: `MasterData.Manage`. [src: AppRoutes.tsx:233] (verified)

### MOD-ORG-001 Organisms master
- Write (create, update, delete): `MasterData.Manage`; read `[Authorize]`. Not laboratory-scoped. [src: backend/MicroLIMS.API/Controllers/OrganismMasterDataController.cs:L33-L48] (verified)
- Page guard: `MasterData.Manage`. [src: AppRoutes.tsx:227] (verified)

### MOD-MEDIACFG-001 Media Configurations
- Media products create, rename, delete; incubation conditions and media configurations create, update, delete: `MasterData.Manage`. GET endpoints `[Authorize]`. [src: backend/MicroLIMS.API/Controllers/MediaMasterDataController.cs:L38-L48, L58-L59, L67-L107] (verified)
- Media product code change (`PUT media-products/{id}/code`): `[Authorize(Roles = RoleConstants.SectionHead)]`, a role-type check on the token's role claim, not a permission. Consequences: a System Administrator is refused (even though that role holds every permission); a custom role whose base type is Section Head is accepted; `MasterData.Manage` alone is not enough. The call also needs the signer's password and a reason; the service signs before changing anything. [src: MediaMasterDataController.cs:L52-L56; backend/MicroLIMS.Shared/Constants/RoleConstants.cs:L6; documentation/knowledge/_raw/verified-roles-permissions.md:L105, L108 (JWT role claim is the base type)] (verified; GAP-013 remains for the guide wording)
- UI gate for the code change: the button "Change code" is shown only when the signed-in role type is `SectionHead` (page-level `role === "SectionHead"`); the rename dialog states "The code is changed from the Overview tab with an electronic signature." [src: frontend/src/modules/laboratoryConfiguration/media/MediaConfigurationPage.tsx:L23-L24; components/MediaProductWorkspace.tsx:L191; dialogs/RenameMediaProductDialog.tsx] (verified)
- UI gate for other actions: Add Media Product, rename, delete, Add Condition, edit and delete icons, Add Configuration are shown only when the user holds `MasterData.Manage` (`isManager`); the page itself is also behind that permission, so for these actions the in-page check is redundant. [src: MediaConfigurationPage.tsx:L22-L23; MediaIncubationConditionsSection.tsx:L100, L152; MediaConfigurationsSection.tsx:L106, L153] (verified)
- Segregation of duties: none. The signer of a code change may be the same person who requested it; there is no second signer. (verified)

### MOD-EQUIP-002 Equipment configuration
- Write (create and update equipment, link inventory, set point change, autoclave program create, update and status change): `MasterData.Manage`. [src: backend/MicroLIMS.API/Controllers/EquipmentMasterDataController.cs:L52-L124] (verified)
- Read: `[Authorize]` for `GET equipment`, `GET equipment/{id}`, `configured-summary`, set point history, autoclave programs, program history. [src: EquipmentMasterDataController.cs:L40-L83, L114] (verified)
- Page guard: `MasterData.Manage`. [src: AppRoutes.tsx:236] (verified)
- Laboratory scoping (verified, corrected): the accessible section ids come from the user's organisation memberships; a System Administrator is unrestricted (`null`), a user without any membership gets an empty list. Section checks are applied by `GET equipment` (list filtered), `GET equipment/{id}`, and, through `EnsureEquipmentAccessAsync`, by `PUT equipment/{id}`, `PUT .../set-point`, `GET .../set-point-history`, `GET .../autoclave-programs` and `POST .../autoclave-programs`. They are not applied by `GET configured-summary`, `GET autoclave-programs/all`, `GET autoclave-programs/{id}/history`, `PUT autoclave-programs/{id}` (edit), `PUT autoclave-programs/{id}/status` or `POST link-inventory`. Creating equipment needs a section: with no membership the error is "Your account is not assigned to any laboratory section. Ask an administrator to assign one."; with several memberships and none chosen "Choose a laboratory section - you belong to more than one."; a System Administrator must choose one ("Choose a laboratory section."). [src: backend/MicroLIMS.Application/Services/UserSectionScopeService.cs:L18-L143; EquipmentMasterDataService.cs:L27-L46, L76, L98, L131-L134; EquipmentMasterDataController.cs:L56-L115] (verified; the earlier statement "filtered ... when a scope service is present" was wrong: the service is a required dependency)
- No role-type rule and no signature for equipment, set point or autoclave program changes. (verified; GAP-408, GAP-409)

### MOD-TESTMASTER-001 Test Master (Microbiology)
- Read test definitions and steps: `[Authorize]`. [src: backend/MicroLIMS.API/Controllers/TestDefinitionMasterDataController.cs:L34; TestWorkflowStepMasterDataController.cs:L39] (verified)
- Create test, update test, freeze, unfreeze, change workflow type, create / update / move / delete step: `MasterData.Manage`. [src: TestDefinitionMasterDataController.cs:L38-L54; TestWorkflowStepMasterDataController.cs:L34-L59] (verified)
- Section scope: only `PUT test-definitions/{id}` (update) refuses a caller whose accessible sections do not include the test's section ("This test belongs to a laboratory section you are not assigned to."; HTTP 403). Freeze, unfreeze, change workflow type and every step write have no section check. Creation resolves the section with the same rules as equipment (membership required; System Administrator must choose). [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L393-L397, L796-L813; TestWorkflowStepMasterDataService.cs:L25-L333] (verified; GAP-419)
- Page guard: `MasterData.Manage`. [src: AppRoutes.tsx:223] (verified)
- The stage replicate endpoints (physchem) are described in C7.

## C5 Media, strains, stock

<!-- from C5-media-strains-stock.md -->

### MOD-MEDIA-001 Media
- Class-level gate on `/api/media`: `Policy:Media.Prepare` [src: backend/MicroLIMS.API/Controllers/MediaController.cs:L24].
- Release `POST /api/media/{id}/release`: `Policy:Media.Release`, body password [src: MediaController.cs:L73-L74]. No role-type check in controller; segregation of duties in service (see below).
- Mark out of stock `POST /api/media/{id}/mark-out-of-stock`: `Policy:Media.Prepare` [src: MediaController.cs:L83-L84].
- Storage GET/POST/move endpoints: no extra policy beyond class gate [src: MediaController.cs:L92-L106].
- Evaluation controller `/api/media-evaluations`: class `Policy:TestWorkflow.Execute` [src: backend/MicroLIMS.API/Controllers/MediaEvaluationController.cs:L28].
- Default grants (verified against the role tables): Media.Prepare = SectionHead, Reviewer, Analyst; Media.Release = SectionHead only (plus SystemAdministrator, which holds all 50 codes by default); TestWorkflow.Execute = SectionHead, Reviewer, Analyst [src: documentation/knowledge/_raw/verified-roles-permissions.md:L86-L88, role table in section 2.3]. Roles are editable by an administrator, so these are defaults, not fixed rules (GAP-513).
- Reviewer limitation (verified in code, not run): the Reviewer default set has Media.Prepare, Cryovials.Manage and TestWorkflow.Execute but not Materials.Manage or Equipment.Manage. The preparation dialog loads dehydrated stock from `GET /api/inventory/materials` (class policy Materials.Manage) and the cryovial dialog also loads inventory equipment from `GET /api/inventory/equipment` (Equipment.Manage), so a Reviewer's pickers would fail to load and show the load-failure alert [src: frontend/src/modules/laboratoryConfiguration/media/dialogs/MediaPreparationDialog.tsx:L36; frontend/src/modules/inventory/materials/services/MaterialService.ts:L28-L29; backend/MicroLIMS.API/Controllers/MaterialController.cs:L22; documentation/knowledge/_raw/verified-roles-permissions.md:L86-L88]. See GAP-514.
- Role-type check on release: none. The controller comment says "Section Head only" but the enforcement is the permission policy plus the SoD rule (verified) [src: MediaController.cs:L70-L74; backend/MicroLIMS.Application/Services/MediaReleaseService.cs:L81-L82].
- Order of release checks (verified): not already decided; evaluation exists; if approving, evaluation Completed and Conform; then the SoD check; then the password signature [src: MediaReleaseService.cs:L63-L90]. Rejecting is allowed whatever the evaluation state, but the SoD check still applies.
- Evaluation actions need TestWorkflow.Execute, not Media.Prepare or Media.Release, and are limited by lab section scope (`EnsureMediaEvaluationChallengeAccessAsync`) (verified) [src: MediaEvaluationController.cs:L28, L53-L85].
- Lab scoping: lots scoped by `Material.SectionId` in list queries [src: backend/MicroLIMS.Application/Services/MediaPreparationService.cs:L139-L142].
- SoD (exact text): "You cannot approve a media lot you prepared or evaluated." [src: backend/MicroLIMS.Application/Services/MediaReleaseService.cs:L81-L82]. Logic: preparer, or any user who read a challenge on its evaluation [src: backend/MicroLIMS.Application/Services/SegregationOfDutiesGuard.cs:L60-L66].
- Section rule: evaluated lot's section must match cryovial / disk / reference-lot section (see validations).

### MOD-CRYO-001 Cryovials
- Class `/api/cryovials`: `Policy:Cryovials.Manage` [src: backend/MicroLIMS.API/Controllers/CryovialController.cs:L23]. Prepare, list, thaw, destroy, move-storage and summaries use the class policy only [CryovialController.cs:L39-L99].
- Approve/reject `POST /api/cryovials/{id}/approve`: `Policy:Cryovials.Approve`, body password [src: CryovialController.cs:L49-L50].
- SoD (exact text): "You cannot approve a cryovial batch you prepared." [src: backend/MicroLIMS.Application/Services/CryovialService.cs:L151-L152].
- Section scope: list filtered by material section [src: CryovialService.cs:L46-L49]; identity-confirmation lots must be the same section [src: CryovialService.cs:L77].
- Default grants (verified): Cryovials.Manage = SectionHead, Reviewer, Analyst; Cryovials.Approve = SectionHead only [src: documentation/knowledge/_raw/verified-roles-permissions.md:L86-L88].
- UI gate (corrected): the "Approve batch" / "Reject batch" row actions are not filtered by Cryovials.Approve; any Cryovials.Manage holder sees them, and the server refuses without Cryovials.Approve. Password is required via SignatureDialog [src: frontend/src/modules/laboratoryConfiguration/cryovials/components/CryovialReviewTable.tsx:L117-L119; CryovialsPage.tsx:L131-L133].
- SystemAdministrator holds all 50 default codes, so also Cryovials.Approve (verified) [src: documentation/knowledge/_raw/verified-roles-permissions.md role table, section 2.3]. Reviewer limitation for the prepare dialog pickers: see MOD-MEDIA-001 roles and GAP-514.
- Thaw, destroy and move-storage need only Cryovials.Manage (any holder, including Analyst and Reviewer) (verified) [src: CryovialController.cs:L58-L79]. Prepare also needs only Cryovials.Manage; no section check on the material at the controller (the service filters the identity-panel media by section) [CryovialController.cs:L42-L47; CryovialService.cs:L77].

### MOD-MATERIAL-001 Materials Stock
- Class `/api/inventory/materials`: `Policy:Materials.Manage` [src: backend/MicroLIMS.API/Controllers/MaterialController.cs:L22]. Create and update have no extra policy [MaterialController.cs:L59, L67].
- Document class `/api/inventory/materials/{id}/documents`: `Policy:Materials.Manage` [src: backend/MicroLIMS.API/Controllers/MaterialDocumentController.cs:L22]; supersede and void: `Policy:Materials.DocumentControl` [MaterialDocumentController.cs:L118, L143].
- Section scope: `EnsureMaterialAccessAsync` on consumption and document upload [src: backend/MicroLIMS.Application/Services/MaterialService.cs:L27; backend/MicroLIMS.Application/Services/MaterialDocumentService.cs:L134-L137].
- Default grants (verified): Materials.Manage = SectionHead, Analyst; Materials.DocumentControl = SectionHead [src: documentation/knowledge/_raw/verified-roles-permissions.md:L86, L88].
- Supersede and void of material documents take a reason (and a replacement file for supersede) but NO password: the controller and service contain no password or signature handling (corrected; the earlier "signed reason" wording is removed) [src: MaterialDocumentController.cs:L117-L148; git grep Password/Sign in MaterialDocumentService.cs: no match]. Missing-reason refusals at the controller: "A replacement file is required for supersession.", "A supersession reason is required." [MaterialDocumentController.cs:L121-L125].
- No SoD rule found for material receipt or documents (searched the material and document services; none) (verified absence).

### MOD-EQUIP-001 Equipment Inventory
- Class `/api/inventory/equipment`: `Policy:Equipment.Manage` [src: backend/MicroLIMS.API/Controllers/EquipmentInventoryController.cs:L26]; create and update have no extra policy [EquipmentInventoryController.cs:L76, L82].
- Documents: GET and POST `Policy:Equipment.Manage`; supersede and void `Policy:Equipment.DocumentControl` [src: backend/MicroLIMS.API/Controllers/EquipmentDocumentController.cs:L32, L41, L75, L102].
- Default grants (verified): Equipment.Manage = SectionHead, Analyst; Equipment.DocumentControl = SectionHead [src: documentation/knowledge/_raw/verified-roles-permissions.md:L86, L88].
- Section scope: out-of-section access refused with "This equipment belongs to a laboratory section you are not assigned to." [src: backend/MicroLIMS.Application/Services/EquipmentInventoryService.cs:L127].
- Set points and autoclave programs are MasterData.Manage (C4, not restated). The master-data Equipment record (set point, calibration due date) is a separate table from this inventory register; creating and editing it needs MasterData.Manage, with a few endpoints on Equipment.Manage (verified) [src: backend/MicroLIMS.API/Controllers/EquipmentMasterDataController.cs:L52-L147; backend/MicroLIMS.Application/Services/MasterData/EquipmentMasterDataService.cs:L130-L141].
- Supersede and void of equipment documents: reason (and file for supersede) but no password (verified) [src: EquipmentDocumentController.cs:L72-L102; backend/MicroLIMS.Application/Services/EquipmentDocumentService.cs:L240-L295].
- SystemAdministrator holds all 50 default codes (verified, section 2.3 of the roles source).

## C6 Physicochemical

<!-- from C6-physchem.md -->

### MOD-HPLC-001 HPLC Workspace (roles)

Permission codes gating physchem actions (IMPLEMENTED, verified):
- Hplc.Operate: start run, confirm SST, assign samples, remove samples, enter replicates, submit sample, evidence upload and supersede, complete, abandon. [src: backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:L74-L165]; ICP equivalent [src: IcpWorkspaceController.cs:L70-L139].
- Solutions.Prepare: start, edit, complete, cancel, discard solution preparations; titrant standardization. [src: backend/MicroLIMS.API/Controllers/SolutionPreparationController.cs:L52-L93]
- WorkingStandards.Qualify: create, update and document upload for working standard qualifications; HPLC qualification run endpoints (with Hplc.Operate). [src: backend/MicroLIMS.API/Controllers/WorkingStandardsController.cs:L48-L61; HplcWorkspaceController.cs:L127-L145]
- Samples.Review: working standard review, return, reject-review. [src: WorkingStandardsController.cs:L82-L93]
- Samples.Approve: working standard approve, reject-approval. [src: WorkingStandardsController.cs:L97-L103]
- TestWorkflow.Execute: all physchem result recording endpoints (titration, gravimetric, dissolution, disintegration, weight variation, measurement). [src: TestWorkflowController.cs:L195-L365]
- MasterData.Manage: method masters (HPLC, ICP), solution master writes, material master writes, equation-type page. [src: HplcMethodController.cs:L34-L44; IcpMethodController.cs:L33-L43; SolutionMasterController.cs:L30-L40; MaterialMasterController.cs:L26-L36]
- Equipment.Manage: chromatography column writes (API). [src: EquipmentMasterDataController.cs:L137-L147]

Role grants (default, verified; the seeder is the source):
- Analyst holds Solutions.Prepare, Hplc.Operate, WorkingStandards.Qualify, TestWorkflow.Execute, Materials.Manage, Equipment.Manage. Analyst does NOT hold Samples.Review, Samples.Approve or MasterData.Manage. [src: documentation/knowledge/_raw/verified-roles-permissions.md:L88]
- Reviewer holds TestWorkflow.Execute and Samples.Review but not Samples.Approve. [src: verified-roles-permissions.md:L87]
- SectionHead holds Samples.Approve, Solutions.Prepare, Hplc.Operate, WorkingStandards.Qualify, MasterData.Manage, Equipment.Manage, Materials.Manage. [src: verified-roles-permissions.md:L86]
- SystemAdministrator holds all 50 codes (default) (verified). [src: verified-roles-permissions.md:L85]
- Role edits on the Roles screen are not visible to code analysis. REQUIRES CONFIRMATION on the target system.

Segregation of duties (verified, physchem):
- Working standard qualification: "The analyst who submitted the qualification cannot review it." / "The analyst who submitted the qualification cannot approve it." / "The reviewer cannot also approve the qualification." / "The reviewer cannot also reject the qualification at approval." [src: backend/MicroLIMS.Application/Services/WorkingStandardService.SignOff.cs:L23, L86, L88, L76]
- Review and approval of a physicochemical sample result (owned by C3) are covered by the shared SoD guard: a user counts as having "performed" the test when assigned as analyst, or when they entered a TestAnalysis (HPLC, ICP, dissolution, titration and the other workspaces record there), so that user cannot review or approve the sample. (verified) [src: backend/MicroLIMS.Application/Services/SegregationOfDutiesGuard.cs:L1-L45; verified-roles-permissions.md:L138]
- No SoD check was found between the people who start a run, confirm the HPLC SST or the ICP calibration, enter CCV, and submit samples in the same run: one person may do all of these, and the submitter of a sample is the one whose later review is blocked by the guard above. (verified by search: the guard is used only by sample review, sample approval, media release, cryovial release and the test workflow engine/support classes.) REQUIRES CONFIRMATION whether run-level SoD is intended (GAP-602).

## C7 General masters

<!-- from C7-masters.md -->

Role/permission source: `documentation/knowledge/_raw/verified-roles-permissions.md` (SectionHead holds `Items.Manage`, `Items.DocumentUpload`, `MasterData.Manage`; SystemAdministrator holds all codes; Reviewer and Analyst hold none of the three) [src: documentation/knowledge/_raw/verified-roles-permissions.md:L85-L90]. `Items.DocumentControl` does not exist (the codes are `Items.Manage` and `Items.DocumentUpload`; `Materials.DocumentControl` and `Equipment.DocumentControl` belong to other modules). Default grants can be edited on the Roles screen; code analysis cannot see such edits (verified-roles-permissions.md:L79).

### MOD-ITEM-001 Items
- Permission `Items.Manage` (`PERMISSIONS.ITEMS_MANAGE`) (verified):
  - UI gate: the route `/laboratory-configuration/items` is wrapped in `PermissionRoute`, and the sidebar entry "Items" needs the same code. Inside the page the only control that re-checks the code is the Preparation Configuration section (`canManage`, which shows or hides "Edit", "Configure Preparation", "Approve"); "Add Item", edit, freeze and delete are not individually gated (the route gate is the only gate) [src: frontend/src/routes/AppRoutes.tsx:L219-L221; frontend/src/routes/menuConfig.ts:L210; frontend/src/modules/laboratoryConfiguration/items/components/ItemPreparationConfigurationSection.tsx:L22-L24].
  - Backend enforcement: `[Authorize(Policy = PermissionConstants.ItemsManage)]` on `ItemController` create (POST), update (PUT {id}), freeze (PUT {id}/freeze), unfreeze (PUT {id}/unfreeze) and delete (DELETE {id}). The item list (GET) is open to any authenticated user [src: backend/MicroLIMS.API/Controllers/ItemController.cs:L24-L61].
  - Preparation configuration: reading (GET) is open to any authenticated user; saving (PUT) and `POST .../approve` need `Items.Manage`; the pending queue `GET api/preparation-configurations/pending` needs `Items.Manage` [src: backend/MicroLIMS.API/Controllers/ItemPreparationConfigurationController.cs:L30-L48, L51-L66].
- Permission `Items.DocumentUpload` (`PERMISSIONS.ITEMS_DOCUMENT_UPLOAD`) (verified):
  - Backend: `[Authorize(Policy = PermissionConstants.ItemsDocumentUpload)]` on `POST api/items/{itemId}/documents`, with a 30 MB request-size attribute [src: backend/MicroLIMS.API/Controllers/ItemDocumentController.cs:L48-L50]. Listing documents and viewing/downloading content are open to any authenticated user (class-level `[Authorize]`) [src: backend/MicroLIMS.API/Controllers/ItemDocumentController.cs:L13, L34, L85].
  - UI gate: no permission check was found for the "+ Upload SOP" / "+ Upload Verification Report" buttons in the items folder; a user with `Items.Manage` but without `Items.DocumentUpload` would see the buttons and be refused by the server (inferred from code; the refusal text is produced by the shared authorization handler and was not read: REQUIRES CONFIRMATION).
- Segregation of duties for Items: none found. The person who edits or creates a preparation configuration can approve it; `ApproveAsync` checks only that a configuration exists and is not already approved. No electronic signature is taken on item create/update/freeze/delete, document upload or preparation approval (verified) [src: backend/MicroLIMS.Application/Services/ItemPreparationConfigurationService.cs:L85-L99; backend/MicroLIMS.Application/Services/ItemService.cs:L35-L129]. See GAP-705 and GAP-707.
- Lab scoping: items and their assigned tests are not scoped to a laboratory section (verified).

### MOD-SPEC-001 Specifications
- Permission `MasterData.Manage` (`PERMISSIONS.MASTER_DATA_MANAGE`) (verified):
  - UI gate: the stand-alone route `/laboratory-configuration/specifications` is guarded by `MasterData.Manage` and only redirects to Items [src: frontend/src/routes/AppRoutes.tsx:L222, L229]. The real specification editor lives in the Items workspace (route gate is `Items.Manage`).
  - Backend: `GET api/masterdata/specifications?itemId=` and `GET .../for-test-order/{id}` are open to any authenticated user; `POST api/masterdata/specifications`, `PUT api/masterdata/specifications/{id}` and `DELETE api/masterdata/specifications/{id}` need `MasterData.Manage` [src: backend/MicroLIMS.API/Controllers/SpecificationMasterDataController.cs:L34-L58]. (The earlier draft named a route `api/masterdata/items/{itemId}/specifications`; it does not exist and was removed.)
  - Section ownership gate (`SpecificationOwnership.EnsureCanEditAsync`) runs on create, update (both the current and the new test code) and delete [src: backend/MicroLIMS.Application/Services/MasterData/SpecificationMasterDataService.cs:L75, L141-L142, L205]:
    - A specification has no lab field; its lab is the section that owns the Test Master row for its `TestCode` [src: backend/MicroLIMS.Application/Services/SpecificationOwnership.cs:L7-L12].
    - A user whose role type is SystemAdministrator is unrestricted. Any other user must be a member of the owning section (department-wide membership counts as all its sections); the check is on membership, not on holding the "Section Head" role, so any user who holds `MasterData.Manage` and is a member of the section passes [src: backend/MicroLIMS.Application/Services/UserSectionScopeService.cs:L16-L60; backend/MicroLIMS.Application/Services/SpecificationOwnership.cs:L15-L35].
    - Refusal text: `This specification belongs to {labName} - only its Section Head can change it.` (`UnauthorizedAccessException`); and `Test code '{testCode}' is not in the Test Master.` when the code is unknown to a non-administrator [src: backend/MicroLIMS.Application/Services/SpecificationOwnership.cs:L26-L34].
    - The Items workspace shows rows of another lab read-only (chip with the section name or "Other lab") [src: backend/MicroLIMS.Application/Services/MasterData/SpecificationMasterDataService.cs:L60-L67].
  - The physchem per-user area (fp / rmpm) stored on a membership is not checked when editing specifications (no call to `GetPhyschemAreasAsync` in the specification service) (verified by search). See GAP-708.
- Specification create/update/delete take no electronic signature and no reason for change (verified).

### MOD-RECVCFG-001 Receiving Configuration
- Permission `MasterData.Manage` (verified): route guard [src: frontend/src/routes/AppRoutes.tsx:L222, L234]; backend `[Authorize(Policy = PermissionConstants.MasterDataManage)]` on the create, update and delete actions for causes of testing, samplers and production stages; the three GET lists are open to any authenticated user because the receiving form reads them [src: backend/MicroLIMS.API/Controllers/ReferenceListMasterDataController.cs:L32-L91].
- Held by: SectionHead, SystemAdministrator [src: documentation/knowledge/_raw/verified-roles-permissions.md:L86, L90].
- No signature, no reason, no section scoping on these lists (verified): they are global.

### MOD-WORKBOOK-001 Master Data Workbook
- Permission `MasterData.Manage` (verified): route guard and sidebar entry [src: frontend/src/routes/AppRoutes.tsx:L222, L228; frontend/src/routes/menuConfig.ts:L212]; backend `[Authorize(Policy = PermissionConstants.MasterDataManage)]` on download, validate, import, imports list, verify and file download [src: backend/MicroLIMS.API/Controllers/MasterDataWorkbookController.cs:L23-L61]. Verification needs the same permission as loading; there is no separate "verify" permission.
- Segregation of duties (verified):
  - Second-person rule: `if (import.ImportedByUserId == userId) throw ...`. No role is exempt; System Administrators are bound by it too (the code contains no administrator exception). Exact text: `The person who loaded the workbook cannot verify it. Ask a second person.` [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L199-L200].
  - Order of checks in `VerifyAsync`: comment length, import exists, status is Loaded, different user, section scope, then the password is verified inside the signature call [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L190-L217].
  - Verifier scope: if the user's section scope is not unrestricted (not a System Administrator) and the load created `MaterialMasterEntry` rows in sections outside that scope, verification is refused with `This load added material master entries to lab section(s) you are not a member of: {sections}. Ask a Section Head of that section to verify it.` Only material master entries are checked; items, equipment and specifications in the load are not scope-checked at verification [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L202-L213].
  - Loader scope: each sheet handler calls the existing create service, so section rules of those services apply to loaded rows: the shared section helper refuses a section the user does not belong to with `You are not a member of the selected laboratory section.` (the SOP names MaterialMaster and Equipment `SectionCode` for this; whether the equipment and material-master services call that helper was not re-read: REQUIRES CONFIRMATION), and specifications are subject to the ownership gate above. The Items sheet calls `ItemService.CreateAsync`, which has no section scope [src: backend/MicroLIMS.Application/Services/UserSectionScopeService.cs:L121; backend/MicroLIMS.Application/Services/MasterDataWorkbook/Sheets/InventorySheets.cs:L160-L202]. The download is filtered to the user's section scope for the sheet that supports it, MaterialMaster (the other sheets list every record) [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L67-L80; backend/MicroLIMS.Application/Services/MasterDataWorkbook/Sheets/InventorySheets.cs:L57-L59].
  - Signature meanings: load = `SignatureMeaning.MasterDataChanged`, verify = `SignatureMeaning.Reviewed`, both with the user's own password and (for the load) taken BEFORE any row is touched, so a wrong password fails first [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L122-L125, L150, L216].
- INTENDED (SOP draft v0.1, section 1): "Two people are always needed", "MicroLIMS refuses a verification by the person who loaded the workbook", "The verifier must also be a member of every lab section the load added material master entries to. System Administrators can verify any load." Matches the code, with the nuance that a System Administrator cannot verify their own load [src: docs/validation/ML-SOP-MDW-001_Master_Data_Workbook_User_Guide.md:L12-L18].

### MOD-TESTMASTER-001 Test Master (FP and RM & PM)
- Permission `MasterData.Manage` (verified): route guard on `/laboratory-configuration/fp-test-master` and `/laboratory-configuration/rm-pm-test-master` [src: frontend/src/routes/AppRoutes.tsx:L222-L225]; backend policy on each action of `TestDefinitionMasterDataController` (create POST, update PUT, freeze, unfreeze) and `TestStageReplicateMasterDataController` (create, update, delete); the test list and the stage-replicate list (GET) are open to any authenticated user [src: backend/MicroLIMS.API/Controllers/TestDefinitionMasterDataController.cs:L30-L52; backend/MicroLIMS.API/Controllers/TestStageReplicateMasterDataController.cs:L35-L50].
- Section membership gate (verified):
  - Create: the chosen section must exist, be active and (for non-administrators) be one the user belongs to: `Selected laboratory section was not found or is inactive.` / `You are not a member of the selected laboratory section.` / `Choose a laboratory section.` [src: backend/MicroLIMS.Application/Services/UserSectionScopeService.cs:L104-L130].
  - Update: refused with `This test belongs to a laboratory section you are not assigned to.` when the test's section is outside the user's scope; a moved test must go to a section the user belongs to [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L389-L398].
  - Stage replicates create/update/delete: same refusal text [src: backend/MicroLIMS.Application/Services/MasterData/TestStageReplicateMasterDataService.cs:L44-L46, L71-L73, L95-L97].
  - Freeze and unfreeze have NO section check: `FreezeTestDefinitionAsync(int id)` and `UnfreezeTestDefinitionAsync(int id)` take no user and run for anyone holding `MasterData.Manage` (verified). See GAP-706.
  - The per-user physchem area (fp / rmpm) is not enforced by the Test Master services (verified by search). See GAP-708.
- No electronic signature on create or update; the only extra control is the typed reason for titration changes (see BR-TESTMASTER-006) (verified).

## C8 Reports, KPI, Document Control

<!-- from C8-reports-dc.md -->

### MOD-REPORTS-001 Reports
- Route `/reports` is available to all signed-in users (verified) [src: frontend/src/routes/AppRoutes.tsx:L115].
- Backend endpoints under `/api/reporting/*` carry a plain `[Authorize]`, so any valid signed-in user may call them (verified) [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L23].
- Scoping: the laboratory section scope is applied on every request through `IUserSectionScopeService.GetAccessibleSectionIdsAsync(userId)`. A user sees results, media lots and cryovials of the assigned sections only; the System Administrator role type has no restriction (verified) [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L36-L38; backend/MicroLIMS.Application/Services/UserSectionScopeService.cs:L26-L40]. A user with no lab membership sees no data (baseline section 7).

### MOD-REPORTS-002 Reporting backfill
- `POST /api/admin/reporting/backfill` is decorated with `[Authorize(Policy = PermissionConstants.ReportingAdmin)]` ("Reporting.Admin") (verified) [src: backend/MicroLIMS.API/Controllers/ReportingAdminController.cs:L15, L25].
- Default holder of `Reporting.Admin`: System Administrator only; Section Head does not hold it (verified) [src: documentation/knowledge/_raw/verified-roles-permissions.md:L81]. Default grants can be edited on the Roles screen.

### MOD-KPI-001 KPI and Workflow Profiles
- `Kpi.View`:
  - Backend: `[Authorize(Policy = Kpi.View)]` on `KpiDashboardController`, `KpiWorkloadController` and `WorkflowProfilesController` (verified) [src: backend/MicroLIMS.API/Controllers/KpiDashboardController.cs:L14; backend/MicroLIMS.API/Controllers/KpiWorkloadController.cs:L15; backend/MicroLIMS.API/Controllers/WorkflowProfilesController.cs:L16].
  - UI: the "KPI / Performance" tab and the quick action on the Overview tab are hidden without it; a link to `?tab=performance` falls back to the first tab (verified) [src: frontend/src/pages/Reports.tsx:L36-L41; frontend/src/modules/reports/components/OverviewTab.tsx:L505-L515].
  - Default holders: System Administrator, Section Head (verified) [src: documentation/knowledge/_raw/verified-roles-permissions.md:L86].
- `Kpi.ViewTeam`:
  - Checked as a token claim, not as a policy: `User.HasClaim("permission", Kpi.ViewTeam)` (verified) [src: backend/MicroLIMS.API/Controllers/KpiDashboardController.cs:L22; backend/MicroLIMS.API/Controllers/KpiWorkloadController.cs:L30].
  - Dashboard and records: without it, asking for any analyst other than yourself throws `UnauthorizedAccessException("You can only view your own analyst figures.")`. When no analyst is named, the headline tiles are NOT restricted: they stay laboratory-level for every holder of `Kpi.View` (verified; plan deviation 14) [src: backend/MicroLIMS.Application/Services/Kpi/KpiDashboardService.cs:L104-L107; docs/superpowers/plans/2026-10-11-kpi-remodel-index.md:L81].
  - Workload table and test drawer: without it the server forces the analyst to the caller (only the caller's own row is returned). Asking for another analyst throws `"You may only view your own workload figures."` (verified) [src: backend/MicroLIMS.Application/Services/Kpi/AnalystWorkloadService.cs:L305-L311].
  - Default holders: System Administrator, Section Head; because the same two roles are the only default holders of `Kpi.View`, the own-row rule only matters when an administrator grants `Kpi.View` to another role (verified) [src: documentation/knowledge/_raw/verified-roles-permissions.md:L86; docs/superpowers/plans/2026-10-11-kpi-remodel-index.md:L81].
- `Kpi.ConfigureProfiles`:
  - Backend: policy on `POST /api/kpi/workflow-profiles` and `POST /api/kpi/workflow-profiles/{id}/sign`; also on the three write routes of `KpiWorkloadController` (settings PUT, availability POST and PUT) (verified) [src: backend/MicroLIMS.API/Controllers/WorkflowProfilesController.cs:L42, L47; backend/MicroLIMS.API/Controllers/KpiWorkloadController.cs:L56, L65, L70].
  - UI: button "Workflow Profiles" appears only with the permission; "Add version" and "Add override" in the panel appear only when `canConfigure` is true (verified) [src: frontend/src/modules/kpi/components/KpiPerformanceTab.tsx:L37, L88; frontend/src/modules/kpi/components/WorkflowProfilesPanel.tsx:L58].
  - Default holders: System Administrator, Section Head (verified) [src: documentation/knowledge/_raw/verified-roles-permissions.md:L86].
- Segregation of duties in KPI profiles: NONE FOUND. The same user who adds a version signs it in the same request, and no second-person check exists (git grep for "segregation" in the Kpi service folder returns nothing). Signing requires the user's own password through the shared signature service (5 failed attempts in 15 minutes block further attempts) [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L80-L82; documentation/knowledge/_raw/verified-roles-permissions.md:L143]. A draft that is already Effective is refused: `"This version is already Effective and cannot be signed again."` [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L102-L103].

### MOD-KPI-002 Analyst availability and KPI section settings
- Reads (`GET /api/kpi/settings/{sectionId}`, `GET /api/kpi/availability`) need `Kpi.View`; without `Kpi.ViewTeam` only the caller's own availability rows are returned (verified) [src: backend/MicroLIMS.API/Controllers/KpiWorkloadController.cs:L15, L52, L61; backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs:L37-L38].
- Writes (`PUT settings/{sectionId}`, `POST availability`, `PUT availability/{id}`) need `Kpi.ConfigureProfiles` plus access to the section (verified) [src: backend/MicroLIMS.API/Controllers/KpiWorkloadController.cs:L56, L65, L70; backend/MicroLIMS.Application/Services/Kpi/KpiSettingsService.cs:L87-L94; backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs:L130-L137].

### MOD-DC-001 Document Control: library and document lifecycle
Document Control authority is mostly a fixed set of role types plus per-document ownership. The permission codes `Documents.DraftEdit`, `Documents.RevisionCreate`, `Documents.Review`, `Documents.Approve` and `Documents.PeriodicReview` are NOT read by any code, so granting or revoking them changes nothing (verified baseline section 1) [src: documentation/knowledge/_raw/verified-roles-permissions.md:L33-L37]. "Author" for every segregation rule below means the user who created the revision (`CreatedByUserId`), not only the Author assignment.
- Register a document master (verified): permission `Documents.Register`, read from the role's database grants inside `CanRegisterDocumentMasterAsync` (no controller policy). Default holders: System Administrator, Section Head, Analyst; Reviewer does not hold it. The refusal text says "Only the Document Controller or a System Administrator can register a Document Master." although Analyst holds the permission by default [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuthorizationService.cs:L84-L87; backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L39-L42; documentation/knowledge/_raw/verified-roles-permissions.md:L86-L88].
- Open a master (verified): any active user; a voided master only for System Administrator or Section Head role type [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuthorizationService.cs:L89-L104].
- Edit metadata, create revision, change items, impact assessment, add/remove assignments, assign approver, submit for review (verified): System Administrator, Section Head, the document owner, or an active assigned Author [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuthorizationService.cs:L106-L115; backend/MicroLIMS.Application/Services/DocumentControl/DocumentRevisionService.cs:L59; backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L628, L709].
- Upload or replace a file, cancel a draft revision (verified): the same people, and only while the revision is Draft [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuthorizationService.cs:L117-L145].
- Void a document master (verified): role type Section Head ONLY. System Administrator is refused. INTENDED: "Document Master voiding is strictly reserved for the Document Controller and excluded from System Administrator" [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuthorizationService.cs:L147-L154; docs/ML-DC-FRS-1A-001.md:L91].
- Revision number override (verified): Section Head only; others get "Only Document Controllers are authorized to override the proposed revision number." [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentRevisionService.cs:L109-L112].
- Open the editable Word source file (verified): System Administrator, Section Head, owner or assigned Author, the assigned technical reviewer while the review task is Pending or InProgress, or the assigned approver while the approval task is Pending [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuthorizationService.cs:L156-L204].
- Open the controlled PDF (verified): any active user when the revision is Effective; for every other status (Draft, InReview, AwaitingApproval, FutureEffective, Superseded, Obsolete, Cancelled) only System Administrator, Section Head, Reviewer, owner or assigned Author [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuthorizationService.cs:L206-L227].
- Controlled-copy download (verified): when the setting `DocumentControl.ControlledCopy.DownloadPermitted` is "false", downloads are refused for everyone except System Administrator and Section Head [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs:L316-L333].
- Technical review actions (verified): add findings is open to the assigned reviewer, Section Head or System Administrator ("Only the assigned technical reviewer or document controller may add review findings."); complete or return the review is open to the assigned reviewer or Section Head only, System Administrator is not accepted ("Only the assigned technical reviewer or document controller may complete this technical review."). Review decisions are not electronically signed [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L292-L293, L561-L563].
- Approval (verified): the approval task may only be assigned to a Reviewer, Section Head or System Administrator role type; the decision may be taken by the assigned approver, a Section Head or a System Administrator ("Only the assigned approver or Document Controller may execute an approval decision."), and the segregation checks below always apply. Approving needs the user's password (electronic signature, meaning Approved) [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L104-L111, L413-L415, L528-L531].
- Segregation of duties, no administrator exemption (verified): the author is the revision's creator; a technical reviewer is any user assigned to or deciding a review task of that revision.
  - Author != technical reviewer: `"Segregation of Duties Violation: The author of a document revision cannot be assigned as its technical reviewer."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L107]; `"Segregation of Duties Violation: The author cannot assign themselves as technical reviewer."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L110]; `"Segregation of Duties Violation: The author cannot perform technical review of their own revision."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L555].
  - Author != finding actor: `"Segregation of Duties Violation: The author cannot create reviewer findings."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L285]; `"Segregation of Duties Violation: The author cannot verify reviewer findings."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L420]; `"Segregation of Duties Violation: The author cannot close or resolve a reviewer-mandated finding without reviewer verification."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L480].
  - Author != approver: `"Segregation of Duties Violation: The author of a document revision cannot be assigned as its approver."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L651]; `"Segregation of Duties Violation: The author of a document revision cannot approve or decide upon their own revision."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L672].
  - Technical reviewer != approver: `"Segregation of Duties Violation: A technical reviewer for this revision cannot be assigned as its approver."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L663]; `"Segregation of Duties Violation: A technical reviewer for this revision cannot approve or decide upon this revision."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L684]. The approval readiness checklist repeats both rules with the texts "Segregation of Duties Violation: You are the author of this revision and cannot approve it." and "Segregation of Duties Violation: You participated in technical review and cannot approve this revision." [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L743, L755].
  - Author != periodic reviewer: `"Segregation of Duties Violation: The author cannot perform or complete the periodic review."` [src: backend/MicroLIMS.Application/Services/DocumentControl/PeriodicReviewService.cs:L554]; also `"Segregation of Duties Violation: The author of the revision under review cannot add review notes or perform the review."` [src: backend/MicroLIMS.Application/Services/DocumentControl/PeriodicReviewService.cs:L388] and `"Segregation of Duties Violation: The author of the document revision cannot be assigned as its periodic reviewer."` [src: backend/MicroLIMS.Application/Services/DocumentControl/PeriodicReviewService.cs:L501].
  - Not checked: the reviewer-versus-periodic-reviewer pairing and the pairing of the person who registered the master with later steps. Periodic review completion also lets any Section Head or System Administrator complete a review assigned to someone else: `"Only the assigned reviewer or Document Controller can complete this review."` [src: backend/MicroLIMS.Application/Services/DocumentControl/PeriodicReviewService.cs:L556-L561].

### MOD-DC-002 Document Control: reading and training
- My Reading List (verified): every signed-in user sees only their own assignments unless they hold `Documents.TrainingAssign` (the controller narrows the filter to the caller) [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentTrainingAssignmentController.cs:L31-L45].
- Acknowledgement (verified): only the assigned user. Another user is refused and a security audit event is written: `"You are not authorized to acknowledge an assignment assigned to another user."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs:L154-L168].
- Training Matrix and training assignments (CORRECTED): the controllers decide between "everyone" and "own row only" with the token claim `Documents.TrainingAssign` (default holders: System Administrator, Section Head). `Documents.TrainingViewMatrix` is declared and granted (System Administrator, Section Head, Reviewer) but no code reads it, so Reviewers do NOT see the whole matrix by default [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentTrainingMatrixController.cs:L29-L45; documentation/knowledge/_raw/verified-roles-permissions.md:L38, L86-L87].
- Compliance figures (`GET .../kpis` of the matrix controller): no permission check beyond sign-in [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentTrainingMatrixController.cs:L47-L55].

### MOD-DC-003 Document Control: audit trail
- Global audit query and CSV export (verified): role types System Administrator, Section Head or Reviewer [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuthorizationService.cs:L235-L242].
- Audit history of one document (verified): the same three role types, plus the owner and assigned Authors of that document [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuthorizationService.cs:L244-L253].

### MOD-DC-004 Document Control: configuration
- Reads (types, departments, sections, numbering, settings) need only sign-in; the lists feed the library filters (verified) [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentConfigurationController.cs:L29, L72, L113, L156, L180].
- Writes (verified): each write route carries `[Authorize(Policy = Documents.ConfigManage)]` AND the service then requires the role type System Administrator (`"Only System Administrators can manage module configuration."`, DocumentConfigurationService.cs:L33-L36). A custom role granted the permission but built on another role type is refused by the service [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentConfigurationController.cs:L37, L56, L80, L99, L121, L140, L164, L188; backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuthorizationService.cs:L229-L233; backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs:L33-L36].
- UI: the route and the page both require the permission (verified) [src: frontend/src/routes/AppRoutes.tsx:L268; frontend/src/modules/documentControl/pages/DocumentConfigurationPage.tsx:L48-L50].

### MOD-DC-005 Document escalations (backend)
- The escalation controller checks the token claim `Documents.TrainingAssign` (default holders: System Administrator, Section Head). Others receive HTTP 403 with the texts listed under validations (verified) [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentEscalationController.cs:L32-L34, L51-L86].
