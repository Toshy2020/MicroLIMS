# Validations

## C1 Platform & administration

<!-- from C1-platform.md -->

### MOD-AUTH-001 Authentication, session, password
- Sign-in (client) [src: frontend/src/pages/Login.tsx:L39-L47]: 401 -> "Invalid username or password."; 500 or more -> "Laboratory authentication service error. Please try again later."; no response -> "Unable to reach the laboratory server. Check network connection."; other -> server message, else "Login failed. Please check credentials.".
- Sign-in (server) [src: backend/MicroLIMS.Application/Services/AuthenticationService.cs:L31]: every failure returns "Invalid username or password." (unknown user, locked, wrong password, inactive).
- Forgot password (client) [src: frontend/src/pages/Login.tsx:L58-L62]: fixed text "If that account exists, a reset link has been generated." after any response; errors swallowed.
- Forgot password (server) [src: backend/MicroLIMS.API/Controllers/AuthenticationController.cs:L54-L68]: same text returned for known and unknown usernames.
- Reset confirm (server) [src: backend/MicroLIMS.API/Controllers/AuthenticationController.cs:L71-L78]: 400 "Reset token is invalid or expired." (no UI caller; GAP-100).
- Administrator recovery (client) [src: frontend/src/pages/AdminPasswordRecovery.tsx:L22-L37]: "All fields are required."; "New password and confirm password do not match."; fallback "Could not perform password recovery. Please verify your recovery code.".
- Administrator recovery (server) [src: backend/MicroLIMS.Application/Services/AdminPasswordRecoveryService.cs:L104-L213]: "Username is required."; "Recovery code is required."; "Invalid or expired recovery code."; "Recovery code has expired. Please request a new recovery code."; "Invalid recovery code." (attempts 1-4); "Maximum failed recovery attempts exceeded. Recovery code invalidated." (attempt 5); "New password must not match any of your last 5 passwords."; policy messages below.
- Administrator recovery initiation (server) [src: AdminPasswordRecoveryService.cs:L51-L60]: "System Administrators cannot initiate admin-assisted password recovery for their own account."; "A reason is required for administrator-assisted password recovery."; "Cannot initiate password recovery for a disabled user account. Please enable the account first.". Client: "A reason is required for admin-assisted password recovery." [src: frontend/src/modules/users/UsersPage.tsx:L218].
- Change password (client) [src: frontend/src/pages/ChangePassword.tsx:L37-L52]: "New password does not meet the policy requirements below."; "New password and confirmation do not match."; fallback "Could not change password.".
- Change password (server) [src: AuthenticationController.cs:L97-L104; AuthenticationService.cs:L210-L224, L296-L314]: "Current password is incorrect." (400); policy failures joined with a space: "Password must be at least 8 characters long.", "Password must contain at least one uppercase letter.", "Password must contain at least one lowercase letter.", "Password must contain at least one digit.", "Password must contain at least one special (non-alphanumeric) character."; history: "New password must not match any of your last 5 passwords.". Business-rule messages reach the client as HTTP 400 [src: backend/MicroLIMS.API/Middleware/ExceptionMiddleware.cs:L105-L111].
- Password policy screen rules (client) [src: frontend/src/pages/ChangePassword.tsx:L13-L17]: "At least 8 characters", "At least one uppercase letter", "At least one lowercase letter", "At least one digit", "At least one special (non-alphanumeric) character".

### MOD-USERS-001 User management
- Create user (client) [src: frontend/src/modules/users/UsersPage.tsx:L94]: "Full Name, Username, Password, and Role are required."
- Create user (server) [src: backend/MicroLIMS.Application/Services/UserService.cs:L106-L117]: "Username is required."; "Full Name is required."; policy messages; "Username '{username}' is already taken."; "Selected role does not exist.".
- Edit profile (server) [src: UserService.cs:L134-L140]: "Full Name is required."; "Username is required."; "Username '{username}' is already taken.".
- Change role (client) [src: UsersPage.tsx:L136]: "Selected Role and a Reason are required for role change."; (server) [src: UserService.cs:L166-L170]: "System Administrators cannot modify their own role."; "A reason is required to change a user's role.".
- Disable (client) [src: UsersPage.tsx:L159]: "A reason is required to disable a user account."; (server) [src: UserService.cs:L209-L212]: "System Administrators cannot disable their own account."; "A reason is required to disable a user account.".
- Permanent delete (server) [src: backend/MicroLIMS.Application/Services/UserDeletionService.cs:L63, L71, L75]: "You cannot permanently delete your own account."; "Cannot delete the last active System Administrator account. Promote another user to System Administrator first."; "This user has activity history and cannot be permanently deleted. Deactivate instead.".
- Not found: "User {id} not found." (404) [src: UserService.cs:L99, L131, L172].
- Toasts (client): "User \"{username}\" created successfully."; "Could not create user."; "Could not update profile."; "Account enabled|disabled for {username}."; "Could not update account status."; "Account unlocked for {username}."; "Could not unlock account."; "Password reset instructions initiated for {username}."; "Could not initiate password reset." [src: UsersPage.tsx:L96-L212 region].
- Laboratory memberships (server) [src: backend/MicroLIMS.Application/Services/LaboratoryOrganizationService.cs:L72, L81, L83, L86]: "One or more departments were not found or are inactive."; "Laboratory section {id} was not found or is inactive."; "Laboratory section {id} does not belong to department {id}."; "An area can only be set on a Physicochemical Laboratory membership.".

### MOD-ROLES-001 Roles and permissions
- Client [src: frontend/src/modules/roles/RoleDetailPage.tsx:L63-L65]: "Role name is required."; [CreateRolePage.tsx:L92] fallback "Could not create this role."; [CreateRolePage.tsx:L79-L82] Next blocked until a name is entered.
- Server [src: RoleService.cs:L66-L67, L94-L95, L110-L120, L137]: "Role name is required."; "System roles cannot be deleted."; "Role '{name}' is currently assigned to {n} user(s) and cannot be deleted."; "Unknown permission code(s): {codes}."; "Role {id} not found.".

### MOD-SIG-001 Electronic signatures and segregation of duties
- Wrong / unknown / inactive / locked password [src: ElectronicSignatureService.cs:L65-L81]: "Password verification failed. The signature was not applied."
- Throttle [src: ElectronicSignatureService.cs:L12-L18, L40-L57, L67]: "Too many failed signature attempts. Wait 15 minutes, then try again. The signature was not applied."
- Client fallback [src: frontend/src/components/SignatureDialog.tsx:L36-L44]: "Signature failed."; Sign button disabled without a password.
- Document approval re-authentication [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L557-L560]: "Password re-authentication is required to apply an electronic signature for approval (21 CFR Part 11)." (C8 owns detail).
- SoD messages: see roles section (MOD-SIG-001).

### MOD-MSG-001 Messages
- Server [src: MessageService.cs:L216, L227, L333, L159]: "Initial message is required."; "A conversation must have at least one other participant."; "Message content cannot be empty."; "You are not a participant in this conversation.".
- Client [src: frontend/src/modules/messages/NewConversationDialog.tsx:L68-L76, L93, L115]: "Please select at least one recipient."; "Group conversation title is required."; "Initial message cannot be empty."; "Failed to create conversation."; "The user directory could not be loaded, so no recipients can be listed. Close and reopen this dialog to try again.".
- Client chat [src: frontend/src/modules/messages/ChatWindow.tsx:L76]: "Failed to send message.".

### MOD-DISC-001 Discussions
- Server [src: DiscussionService.cs:L297-L299, L376-L378]: "Title is required."; "Content is required.".
- Server [src: DiscussionService.cs:L487]: "Comment content cannot be empty.".
- Attachments [src: backend/MicroLIMS.Application/Services/DiscussionAttachmentPolicy.cs:L51-L66]: "A post can carry at most 5 attachments; {n} were attached."; "'{name}' is empty."; "'{name}' is larger than the 10 MB limit for attachments."; "'{name}' is not an allowed attachment type. Allowed types: {list}."; "'{name}' does not look like a valid {EXT} file. It may be corrupted or misnamed."; "File integrity verification failed." [DiscussionService.cs:L473].
- Authorisation [src: DiscussionService.cs:L373, L417, L431, L539]: "You are not authorized to edit this discussion post."; "You are not authorized to modify this discussion post."; "You are not authorized to delete this discussion post."; "You are not authorized to edit this comment.".
- Client [src: frontend/src/modules/discussions/components/NewDiscussionDialog.tsx:L53-L58, L80]: "Discussion title is required."; "Discussion content is required."; "Failed to create discussion.".
- Client [src: frontend/src/modules/discussions/components/EditDiscussionDialog.tsx:L50-L55, L71]: "Discussion title is required."; "Discussion content is required."; "Failed to update discussion.".

### MOD-ERR-001 Error Monitoring
- No input validation in the page. Client messages: "Could not load incidents."; "The export could not be generated." [src: frontend/src/modules/errorMonitoring/ErrorMonitoringPage.tsx:L27, L51].

### MOD-AUDIT-001 Audit Trail
- Client [src: frontend/src/modules/auditSearch/AuditSearchPage.tsx:L72]: "Failed to fetch audit records from the server."; empty "No audit events found" [AuditResultsTable.tsx:L54].

### MOD-DASH-001 Dashboards
- Server [src: backend/MicroLIMS.Application/Services/DashboardLabScopeService.cs:L12-L60]: "You are not a member of this laboratory."
- Client [src: frontend/src/modules/dashboard/components/DashboardStateGate.tsx:L45, L65]: "Dashboard could not be loaded"; "No dashboard data is available yet."; [AdminDashboardPage.tsx:L52] "The dashboard service did not respond. Your data has not been changed."; [SectionHeadDashboardPage.tsx:L69] "The dashboard service did not respond. No laboratory data has been changed."

### MOD-NAV-001 Application shell and navigation
- Not found, client [src: frontend/src/pages/NotFound.tsx:L13-L19]: described under SCR-NAV-002.
- Permission failure: silent (no message) [src: frontend/src/routes/PermissionRoute.tsx:L12].

### MOD-NOTIF-001 Notifications
- Client [src: frontend/src/components/Header.tsx:L250]: "Notifications could not be loaded".
- Error severity email: no client validation.

## C2 Sample flow & micro testing

<!-- from C2-sample-testing.md -->

Server (exact text as coded; errors thrown as InvalidOperationException unless stated).

Note (Phase 3, corrected): `EMValidator`, `WaterValidator` and `ProductValidator` ("A valid room must be selected.", "Incubation step must be 1 or 2.", "A valid sampling point must be selected.", "Test code is required.", "Plate count must be a non-negative number.") are registered in dependency injection but called by no receive path, so those messages are never shown; rows removed. EM / Water / After Cleaning receive have no server check of control number. Added rows follow the table.

| Where | Trigger | Exact message | src |
|---|---|---|---|
| Receive Product/RM/PM | Item missing | "A valid Item must be selected." | ReceiveSampleValidator.cs:13 |
| Receive | Cause missing | "Cause of testing is required." | ReceiveSampleValidator.cs:14 |
| Receive | Batch blank | "Batch number is required." | ReceiveSampleValidator.cs:15 |
| Receive | Control blank | "Control number is required." | ReceiveSampleValidator.cs:16 |
| Receive | Sampled By blank | "Sampled By is required." | ReceiveSampleValidator.cs:17 |
| Receive | Item with no tests | "Item '{name}' has no assigned tests. Configuration must be completed ..." | ProductWorkflowEngine.cs:56 |
| Receiving ReceiptLab | Own-lab only | "You can receive samples only for your own laboratory." | ReceiptLabGuard.cs:25-30 (verified file) |
| Correction | Reason blank | "A reason for the correction is required." | SampleCorrectionService.cs:77 |
| Correction | Locked | "Sample details can only be corrected until the sample is submitted for review - this record is locked." | SampleCorrectionService.cs:84-85 |
| Correction | Cause not found | "The selected cause of testing does not exist." | SampleCorrectionService.cs:109 |
| Correction | Retest cause | "The \"Retest\" cause of testing is assigned by the system to retest samples and cannot be set or changed here." | SampleCorrectionService.cs:112-113 |
| Correction | Expiry before manufacture | "The expiry date cannot be before the manufacturing date." | SampleCorrectionService.cs:128 |
| Correction | Stage unknown | "Production stage '{stage}' is not a known stage." | SampleCorrectionService.cs:143 |
| Correction | Water storage | "Storage condition is required for a prepared water sample." / "Storage condition must be Refrigerator or RoomTemperature." / "Storage time is required when a water sample was refrigerated." / "Storage time cannot be negative." | SampleCorrectionService.cs:160-169 |
| Correction | Item/department/machine | "The new item must belong to the same category as the sample." / "The selected item does not exist." / "Item '{name}' is frozen and cannot be used." / "Item '{name}' has no assigned tests." / "The selected water department does not exist." / "The selected department does not exist." / "The selected machine does not exist." | SampleCorrectionService.cs:318-354 |
| Correction | No change | "No changes were made." | SampleCorrectionService.cs:212 |
| Void | Reason blank | "A reason for voiding is required." | SampleCorrectionService.cs:255 |
| Void | Already voided | "Sample is already voided." | SampleCorrectionService.cs:261 |
| Assign analyst | User missing / inactive | "User {id} not found." / "User '{name}' is not active." | SampleAssignmentService.cs:34, 37 |
| Preparation | Already prepared | "This sample has already been prepared." | SamplePreparationService.cs:154 |
| Preparation | Other analyst | "This sample is assigned to {name}. Only the assigned analyst may perform sample preparation, unless reassigned by an authorized Section Head." | SamplePreparationService.cs:165-168 |
| Preparation | No item | "This sample has no Item and cannot use the preparation configuration flow." | SamplePreparationService.cs:55, 114 |
| Preparation | No configuration | "This item has no preparation configuration to confirm." | SamplePreparationService.cs:117 |
| Review | SoD | "You cannot review a sample you tested." | SampleReviewService.cs:112 |
| Review | Not ready | "Cannot complete review: one or more tests are not in Ready step." | SampleReviewService.cs:116 |
| Return to analyst | Wrong state / type | "Cannot return a test to the analyst after the sample has been reviewed. Use Reject or Retest at approval instead." / "Cannot return a test order in {status} status. Only test orders in ResultEntered status can be returned to the analyst." / "Return to Analyst is only supported for Count Test and result-entry (analysis) workflows. ..." | SampleReviewService.cs:179-189 |
| Approval | SoD | "You cannot approve a sample you reviewed." / "You cannot approve a sample you tested." | SampleApprovalService.cs:163, 170 |
| Approval (retest) | Two analysts | "Two analysts must be assigned - one per new sample - when requesting a new sample." / "The two new samples must be assigned to two different analysts." / "Both retest analysts must be active, eligible Analyst-role users." | SampleApprovalService.cs:267-277 |
| Count | No readings | "At least one plate reading is required to calculate an average." | IncubationStepRecorder.cs:RecordCountTestAsync start |
| Count | DF not configured | "No Dilution Factor is configured for \"{code}\" on this item's Specifications. Configure it before recording a result." | IncubationStepRecorder.cs:800-801 |
| Count | DF differs, no note | "The entered Dilution Factor ({x}) differs from the configured value ({y}). A justification note is required." | IncubationStepRecorder.cs:807-808 |
| Count | Unit missing | "No reporting Unit is configured for \"{code}\" on this item's Specifications. Configure it before recording a result." | IncubationStepRecorder.cs:817-818 |
| Count | Wrong category for route | "EM/After Cleaning results must be submitted via the batch results endpoint." | IncubationStepRecorder.cs:238 |
| Count | Stage 2 not started | "Step \"{step}\" requires stage 2 incubation to be started before a count can be recorded." | IncubationStepRecorder.cs:266 |
| Batch | Missing location results | "Results are missing for: {names}." | IncubationStepRecorder.cs:479, 590 |
| Batch | Empty readings | "At least one plate reading is required for: {names}." | IncubationStepRecorder.cs:483, 594 |
| Batch | No locations | "No locations are assigned to this test order." | IncubationStepRecorder.cs:474, 585 |
| Batch pathogen | Growth missing | "Step \"{step}\" requires a growth observation for every location." | IncubationStepRecorder.cs:682 |
| Window | Not configured | "The incubation window for {medium} on step {step} is not configured - set its incubation hours and temperature in Test Master." | IncubationWindowResolver.cs:42-49 |
| Window | Not ready (batch) | "This incubation window needs at least {h} hours - not ready until {utc} UTC." | IncubationStepRecorder.cs:384-387 |
| Window | Not ready (stage 2) | "Stage 2 incubation for step \"{step}\" requires at least {h} hours of incubation - not ready until {utc} UTC." | IncubationStepRecorder.cs:398-401 |
| Window | Not ready (pathogen) | "Minimum incubation time not elapsed. Available from: {utc} UTC." | PathogenConfirmationRecorder.cs:333-334 |
| Window | Final window | "This is the final incubation window - record results instead of advancing." / "This step requires incubation transfer - start stage 2 incubation instead of advancing window." | IncubationStepRecorder.cs:414, 417 |
| Media | Not released | "Media lot \"{lot}\" is not released for use, out of stock, or rejected." | IncubationStepRecorder.cs:50 |
| Media | Expired (session) | "Media lot #{lot} expired on {date}." | PathogenSessionService.cs:591 |
| Media | Not permitted | `MediaNotInPermittedList` code (text not read) | PathogenConfirmationRecorder.cs:119, 454 |
| Workflow | Order | "Workflow order violation: step \"{a}\" must be completed before \"{b}\"." / "All workflow steps for \"{code}\" are already complete." | IncubationStepRecorder.cs:33-38 |
| Workflow | Duplicate media in step | "Media has already been selected for step \"{step}\" - awaiting its result." | IncubationStepRecorder.cs:44 |
| Shared TSB | Sibling window / incubator | see BR-TESTING-011 | IncubationStepRecorder.cs:RequireSharedTsbSiblingsCanJoinAsync |
| Session | Missing results | "Cannot complete testing session: {n} required Location × Test result(s) are missing ({list}{extra})." | PathogenSessionService.cs:1218-1222 |
| Session | Prerequisites | "Cannot complete testing session: Assigned tests {codes} have incomplete workflow prerequisites." | PathogenSessionService.cs:1226-1231 |
| Session | Reset voided / rejected | "Sample #{ref} has been voided and its testing session cannot be reset." / "...has been rejected and its testing session cannot be reset. A rejection is reversed through the approval workflow by a Section Head." | PathogenSessionService.cs:1284-1300 |
| Confirmatory | Count | "Pathogen {code} requires exactly {n} confirmatory media; received {m}." | PathogenSessionService.cs:883 |
| Confirmatory | Duplicate / set mismatch | "Exactly one observation is required for each selected medium - one was submitted more than once." / "Exactly one observation is required for each selected medium." | PathogenConfirmationRecorder.cs:549-566 |
| Biochemical | Result required | BiochemicalResultRequired code (text not read) | PathogenConfirmationRecorder.cs:683 |
| Biochemical decision | SoD | "A reviewer cannot decide on a result they performed." | PathogenConfirmationRecorder.cs:780-782 |
| Biochemical decision | Reason | "A reason is required when returning a result for biochemical confirmation." | PathogenConfirmationRecorder.cs:797 |
| Receive | Lab choice | "Choose at least one laboratory to receive the sample for." / "One or more selected laboratories do not exist." / "You can receive samples only for your own laboratory." / "Item '{name}' has no tests for {lab}." | ReceiptLabGuard.cs:17-30; ProductWorkflowEngine.cs:104-116 |
| Receive | Item state | "Item '{name}' is frozen and cannot be used to receive new samples." / "Production stage is required for a Finished Product sample." / "Production stage '{stage}' is not a known stage." | ProductWorkflowEngine.cs:51-73 |
| Receive After Cleaning | Previous product | "Previous Product is required for After Cleaning receiving." / "Previous Product Batch Number is required for After Cleaning receiving." / "Machine {id} not found." | AfterCleaningWorkflowEngine.cs:43-54 |
| Receive EM / Water | Lookup | "Department {id} not found." / "Water department {id} not found." | EMWorkflowEngine.cs:38; WaterWorkflowEngine.cs:52 |
| Prepare EM / AC / Water | Selection | "At least one room must be selected." / "At least one machine part must be selected." / "At least one sampling point must be selected." / "... not found: {ids}." / "... do not belong to this sample's department / machine." | EMWorkflowEngine.cs:66-81; AfterCleaningWorkflowEngine.cs:86-101; WaterWorkflowEngine.cs:81-102 |
| Prepare Water | Storage | "Storage condition is required for water samples." / "Storage time is required when a water sample was refrigerated." | WaterWorkflowEngine.cs:86-89 |
| Preparation (Product) | Parameters | "Diluent is required." / "Neutralizer is required." / "Filtration technique requires both Filtration Volume and Washing Volume." | PreparationParameterValidator.cs:24-33 |
| Preparation (grouped) | Selection | "No samples were selected for grouped preparation." / "The preparation configuration for this group no longer exists." / "This item's preparation configuration changed after the group was loaded. Reload the grouped action and review the steps before confirming." | SamplePreparationService.cs:119-122, 358-361 |
| Preparation gate | Not prepared | "Test Preparation must be completed and confirmed for sample {ref} before incubation can be started for step \"{step}\"." / "Preparation not complete - no locations assigned to this test." | TestWorkflowSupport.cs:323-325, 360-362 |
| Item preparation config | Approve | "Item {id} has no preparation configuration to approve." / "This preparation configuration is already approved." / "Item {id} not found." | ItemPreparationConfigurationService.cs:55, 88-91 |
| Add laboratory | Rules | "A reason is required to add a laboratory." / "A laboratory cannot be added to a rejected, voided or cancelled sample." / "This laboratory is already testing the sample." / "A laboratory can be added only to a product, raw material or packaging material sample." | AddLaboratoryService.cs:29-41 |
| Correction | Item / location | "Testing has already started for this sample, so its item or location can no longer change. Void the sample and receive it again." / "A retest sample must test the same item or location as its original sample, so it cannot be changed." | SampleCorrectionService.cs:193, 387-388 |
| Correction | Required fields | "Control number is required." / "Sampled by is required." / "Batch number is required." / "Production stage is required." / "Previous product is required." / "Previous product batch number is required." | SampleCorrectionService.cs:101-138, 178-179, 480-481 |
| Media select | Medium / section / incubator | "This step requires a different medium. The selected lot is {name}." / "Media lot \"{lot}\" was prepared from another laboratory section's material and cannot be used for this test." / "The selected incubator's set point is outside this medium's temperature range." | IncubationStepRecorder.cs:67-80; SectionMediaRule.cs:12-18; TestWorkflowSupport.cs:803-808 |
| Count result | Empty list | "Plate readings are required." (controller) | TestWorkflowController.cs:187 |
| Stage 1 | Not complete | "Stage 1 incubation for step \"{step}\" requires at least {h} hours of incubation - not ready until {utc} UTC." | IncubationStepRecorder.cs:181-182 |
| Stage 2 | Single-test count read-out | "This step's incubation period has not finished yet." | TestWorkflowSupport.cs:816 |
| Skip Wait | Nothing open | "No incubation window is currently open." | IncubationStepRecorder.cs:437 |
| Broth submit | Minimum | "This step requires at least {h} hours of incubation - not ready until {utc} UTC." | PathogenConfirmationRecorder.cs:192-193 |
| Pathogen step | Finished order | "Test order {id} is already at {step} - its workflow can no longer be submitted against." | TestWorkflowSupport.cs:766-767 |
| Pathogen step | Chain | "Step '{step}' is not a broth step." / "... is not a selective plating step." / "... is not a confirmatory plating step." / "... is not a biochemical test step." | PathogenConfirmationRecorder.cs:138, 251, 434, 680 |
| Pathogen step | Confirmatory setup | "At least one confirmatory medium must be selected." / "Every selected medium needs a lot and an incubator." / "That medium is not on this step's permitted list." / "Confirmatory plating for step \"{step}\" has already been read out and cannot be set up again." / "Confirmatory media have already been selected for step \"{step}\" - awaiting their plate readings. ..." / "The incubation window ends before it starts (...)." / "Step \"{step}\" requires at least {h} hours of incubation - the declared window is {n} hours." | PathogenConfirmationRecorder.cs:424-478, 454-458 |
| Pathogen step | Analyst decision | "An analyst decision ({decision}) was already recorded for this confirmatory result." | PathogenConfirmationRecorder.cs:626-628 |
| Pathogen step | Biochemical | "A biochemical result is required." / "A biochemical test is only available after a conforming result on \"{step}\"." | PathogenConfirmationRecorder.cs:682-723 |
| Pathogen session | Confirmatory location | "Location #{id} does not have a GrowthConforming or GrowthNonConforming primary observation and cannot proceed to confirmation." | PathogenSessionService.cs:896 |
| Pathogen session | TSB | "None of the assigned tests on this sample require TSB enrichment." / "Media lot #{lot} ({name}) is not the TSB medium of any test on this sample." / "Incubator #{id} not found." | PathogenSessionService.cs:564, 568, 596 |
| Qualitative result | Server | "Password is required to sign the result." / "At least one parameter result is required." / "Analysis time cannot be in the future." / "An observation is required when result does not conform." / "Observation cannot exceed 500 characters." / "Every specification for this test must be supplied exactly once." | TestWorkflowSupport.cs:562-564, 575, 664; SingleResultRecorder.cs:267-270 |

Client (exact text):
- New Sample rows: "Item is required"; "Choose at least one laboratory"; "Production Stage is required"; "Department is required"; "Previous Product is required"; "Previous Product Batch No. is required"; "Machine is required"; "Cause of Testing is required"; "Sampled By is required"; "Expiry Date must be after Manufacturing Date". [NewSampleDialog.tsx:170-225]
- Edit / Void / Add lab: "A reason for voiding is required." (helper), "A reason is required." (add lab). [VoidSampleConfirmationDialog.tsx:185-197; AddLaboratoryDialog.tsx:196-225]
- Count: "Enter at least one plate reading."; "A justification note is required when the entered Dilution Factor differs from the configured value."; "Non-numeric result: reviewer will decide accept or retest" (note). [TestWorkflowDialog.tsx:265-291, 787]
- Qualitative (C6): "Observation is required when non-compliant" (helper).
- Water: storage hours required when Refrigerator. [WaterPreparationForm.tsx:31-33]
- Pathogen: "Select an observation." [SelectivePlatingPanel.tsx:100]; "Check at least one medium that was actually plated."; "Every checked medium needs a media lot and an incubator."; "At least one medium must be confirmed as plated."; "Select an observation for: {media}." [ConfirmatoryPlatingPanel.tsx:147-194]
- Shared TSB: "Please select a valid released TSB media lot and an in-service incubator." [SharedTsbEnrichmentPanel.tsx:73]
- Session complete (client text, shown when pending results > 0): "Cannot complete session: {n} required results are still missing." [SessionReviewPanel.tsx:191] — differs from server text (GAP-210).

Hardcoded fallbacks that change displayed/submitted values (client): see GAP-208 for list.

---

## C3 Review, approval, reports out

<!-- from C3-review-approval.md -->

### Server messages (exact text)
Review:
- "Sample {id} not found." [src: backend/MicroLIMS.Application/Services/SampleReviewService.cs:L100-L101]
- "Sample must be under review before it can be reviewed." [src: SampleReviewService.cs:L105-L106]
- "You cannot review a sample you tested." [src: SampleReviewService.cs:L112]
- "Cannot complete review: one or more tests are not in Ready step." [src: SampleReviewService.cs:L116]
- "Cannot return a test to the analyst after the sample has been reviewed. Use Reject or Retest at approval instead." [src: SampleReviewService.cs:L179]
- "Cannot return a test order in {status} status. Only test orders in ResultEntered status can be returned to the analyst." [src: SampleReviewService.cs:L183]
- "Test definition \"{code}\" not found." [src: SampleReviewService.cs:L186]
- "Return to Analyst is only supported for Count Test and result-entry (analysis) workflows. \"{code}\" is a {workflow} workflow." [src: SampleReviewService.cs:L189]
- "Test order {id} not found." [src: SampleReviewService.cs:L162-L163]

Approval:
- "Sample must be under approval before a decision can be made." [src: SampleApprovalService.cs:L154-L155]
- "You cannot approve a sample you reviewed." [src: SampleApprovalService.cs:L163]
- "You cannot approve a sample you tested." [src: SampleApprovalService.cs:L170]
- "Cannot approve section: test order {id} (\"{code}\") requires system suitability, but only HPLC workspace tests (assay, dissolution) can supply one. Clear \"Requires system suitability\" for this test in Test Master." [src: SampleApprovalService.cs:L190-L191]
- "Cannot approve section: test order {id} (\"{code}\") lacks an HPLC run with a passed system suitability." [src: SampleApprovalService.cs:L202-L203]
- "Cannot approve section: test order {id} (\"{code}\") lacks an active {display name} entry." [src: SampleApprovalService.cs:L231-L232]
- "Cannot approve section: test order {id} (\"{code}\") has a pending stage." [src: SampleApprovalService.cs:L237-L238]
- "At least one test must be selected for retest." [src: SampleApprovalService.cs:L251]
- "One or more selected tests do not belong to this sample's current test orders." [src: SampleApprovalService.cs:L256]
- "Two analysts must be assigned - one per new sample - when requesting a new sample." [src: SampleApprovalService.cs:L267]
- "The two new samples must be assigned to two different analysts." [src: SampleApprovalService.cs:L269]
- "Both retest analysts must be active, eligible Analyst-role users." [src: SampleApprovalService.cs:L277]
- "Neither new sample's analyst may be the analyst who tested the original sample." [src: SampleApprovalService.cs:L282]
- "The 'Retest' cause of testing is not configured." [src: SampleApprovalService.cs:L290]
- "'{decision}' is not a valid sample-level decision." [src: SampleApprovalService.cs:L299]

Sections and scope (shared):
- "This sample has no tests in the selected laboratory section." [src: backend/MicroLIMS.Application/Services/SampleSectionRollup.cs:L128; SectionClosureService.cs:L47]
- "This test belongs to a laboratory section you are not assigned to." [src: SampleSectionRollup.cs:L130, L142; SectionClosureService.cs:L45]
- "Choose a laboratory section - more than one section of this sample is waiting." [src: SampleSectionRollup.cs:L144]
- Wrong-state text is passed in by the caller: "Sample must be under review before it can be reviewed." or "Sample must be under approval before a decision can be made." [src: SampleSectionRollup.cs:L131-L138]

Close testing:
- "A reason is required to close testing." [src: backend/MicroLIMS.Application/Services/SectionClosureService.cs:L36-L37]
- "Testing can be closed only after another laboratory rejected the sample." [src: SectionClosureService.cs:L52]
- "This laboratory's testing is already closed." [src: SectionClosureService.cs:L56]
- "This laboratory has no open tests on the sample." [src: SectionClosureService.cs:L58]

Signature (shared):
- "Password verification failed. The signature was not applied." [src: documentation/knowledge/_raw/verified-roles-permissions.md:L142]
- "Too many failed signature attempts. Wait 15 minutes, then try again. The signature was not applied." [src: verified-roles-permissions.md:L143]

OOS documents (file checks, same text for upload and supersede) [src: backend/MicroLIMS.Application/Services/MaterialDocumentFileValidator.cs:L38-L83]:
- "File exceeds the maximum allowed size of {n} MB." (n = 25 with the shipped setting)
- "The uploaded file is empty."
- "File type '{ext}' is not supported. Allowed types: PDF, JPG, JPEG, PNG, WEBP, TIFF."
- "The declared content type '{type}' does not match the file extension '{ext}'."
- "The file content could not be validated because it is too short."
- "The file does not appear to be a valid WEBP image."
- "The file content does not match the expected format for '{ext}'. The file may be corrupted or misnamed."
- Controller-level (HTTP 400): "No file was provided."; "A replacement file is required."; "A reason for supersession is required."; "A reason for voiding is required." [src: backend/MicroLIMS.API/Controllers/OosInvestigationDocumentController.cs:L43, L75, L77, L100]
OOS documents (service):
- "OOS group \"{code}\" not found." [src: backend/MicroLIMS.Application/Services/OosInvestigationDocumentService.cs:L81, L104]
- "Document {id} not found for OOS group {code}." [src: OosInvestigationDocumentService.cs:L155, L195, L265]
- "The document file could not be retrieved." [src: OosInvestigationDocumentService.cs:L165]
- "Document storage failed. The upload was not completed." [src: OosInvestigationDocumentService.cs:L143]
- "Document integrity check failed. The file may have been altered. Please contact your System Administrator." [src: OosInvestigationDocumentService.cs:L174]
- "A supersession reason is required." [src: OosInvestigationDocumentService.cs:L191]
- "A voided document cannot be superseded. Upload a new document instead." [src: OosInvestigationDocumentService.cs:L198]
- "Replacement file storage failed. The supersession was not completed." [src: OosInvestigationDocumentService.cs:L250]
- "A void reason is required." [src: OosInvestigationDocumentService.cs:L261]
- "This document is already voided." [src: OosInvestigationDocumentService.cs:L268]

### Client messages (exact text)
- Decision controls (disabled-button rules, warnings "Select at least one test to retest.", "The two new samples must be assigned to two different analysts.", "The list of eligible analysts could not be loaded. Close and reopen this sample to try again.", and the out-of-spec approve warning): see SCR-APPROVAL-001 [src: frontend/src/modules/testingWorkspace/sampleSummary/ApprovalSignaturesCard.tsx:85-92, 402-422, 459-463].
- Close Testing card warning: "Another laboratory has rejected this sample. You may close your own laboratory's remaining tests instead of continuing them." [src: ApprovalSignaturesCard.tsx:477-518].
- Return dialog: no validation; reason is optional [src: frontend/src/components/ReturnToAnalystDialog.tsx:62-136].
- Signature dialog: "Sign" disabled until a password is entered [src: frontend/src/components/SignatureDialog.tsx:52-75].
- Load and export errors: "Failed to load sample summary."; "Export failed."; "Failed to load the sample report."; "Failed to load the certificate of analysis."; "No approved laboratory sections are currently accessible." [src: frontend/src/modules/testingWorkspace/SampleSummaryDialog.tsx:189, 290; SampleReportPage.tsx:119; SampleCoaPage.tsx:283, 389-407].
- OOS page: "Failed to load OOS tracking items."; "Failed to load investigation documents."; "Failed to upload document."; "Failed to download document content."; "Failed to supersede document." [src: frontend/src/modules/oosTracking/OosTrackingPage.tsx:79, 414, 436, 454, 475].

## C4 Monitoring & micro configuration

<!-- from C4-monitoring-microconfig.md -->

How the messages reach the user (verified): a business-rule refusal is an `InvalidOperationException` (HTTP 400) or `BusinessRuleException`; a missing record is a `NotFoundException` (HTTP 404); both return the message text, which the pages show as written (or as the "fallback" text when the response has no message). A stale form version returns HTTP 409 with "This record was changed by someone else while you were working on it. Nothing was saved - reload it and make your change again." A database refusal (foreign key, unique index) is a generic HTTP 500 with "An unexpected error occurred." [src: backend/MicroLIMS.API/Middleware/ExceptionMiddleware.cs:L17-L18, L44-L110]

### MOD-EM-001 Environmental Monitoring configuration
Server messages (exact text):
- `Department {id} not found.` / `Room {id} not found.` / `Room test configuration {id} not found.` [src: backend/MicroLIMS.Application/Services/MasterData/EnvironmentalMonitoringMasterDataService.cs:L47, L95, L158, L176]
- `Cannot delete '{name}' - it still has {n} room(s). Delete those rooms first.` [L65]
- `Cannot delete '{name}' - it still has {n} test configuration(s). Delete those first.` [L116]
- `Cannot delete '{name}' - it has {n} monitoring record(s) in its history.` [L120]
- `Unknown test type '{t}'. Allowed: PassiveAirSample, SurfaceAirSample, ActiveAirSample, CompressedAir, Drains.` [L135-L136]
- `Unit is required for {testType} test configurations.` (ActiveAirSample, CompressedAir, Drains) [L137-L138]
- Concurrency: see the introduction above (the exception text `{Entity} changed after the form was loaded ...` in `RecordVersion.cs:L16-L17` is not shown to the user).

Client messages (exact text):
- "Name is required." (department) [src: environmentalMonitoring/components/EmPanels.tsx:L33]
- "Room name and department are required." [EmPanels.tsx:L96]
- "Choose the test to run." (EM test; same text in the After Cleaning count test) [EmPanels.tsx:L175; afterCleaning/components/AcPanels.tsx:L156]
- "Unit is required for {type label}." (EM test of a unit-required type) [EmPanels.tsx:L176]
- Advisory (does not block): "Alert limit is higher than the action limit." / "Action limit is higher than the specification." / "Alert limit is higher than the specification." followed by "Check the values before saving." [src: frontend/src/components/configHierarchy/LimitFields.tsx:L19-L21, L55]
- Save fallback for a test: "Could not save this test."; load fallback "Could not load the environmental monitoring configuration."; delete fallback "Could not delete this record." [EmPanels.tsx:L195; EMConfigPage.tsx:L53, L123]

Required markers (MUI `required`): department "Name"; room "Room name", "Department"; test "Unit" for unit-required types. [src: EmPanels.tsx:L63, L122-L123; LimitFields.tsx:L47]

Not validated (verified by reading the create methods): empty or duplicate names for departments and rooms; limit text format and order; duplicate room test configurations; the test code (the client requires one, the server stores `TestCode` as given, even if it is not in the Test Master). [src: EnvironmentalMonitoringMasterDataService.cs:L36-L42, L84-L90, L141-L153]

### MOD-WATER-001 Water configuration
Server messages (exact text):
- `Sampling point {id} not found.` / `Water department {id} not found.` / `Water sampling configuration {id} not found.` [src: backend/MicroLIMS.Application/Services/MasterData/WaterMasterDataService.cs:L39, L98, L108, L138, L152]
- `Cannot delete '{code}' - it is referenced by {n} sample(s) and {m} sampling configuration(s).` [L61-L62]
- `Cannot delete '{name}' - it still has {n} sample location(s). Delete those first.` [L111-L112]

Client messages (exact text):
- "Name is required." (water system) [src: water/components/WaterDepartmentPanel.tsx:L28]
- "Point code and water system are required." [water/components/WaterLocationPanel.tsx:L71]
- "The location was saved, but some limits were not. {code}: {message}" [WaterLocationPanel.tsx:L106-L108]
- Fallbacks: "Could not save this sample location."; "Could not delete this water system."; "Could not delete this sample location."; "Could not load the water configuration." [WaterLocationPanel.tsx:L88; WaterConfigPage.tsx:L55, L123, L135]
- Advisory limit-order warnings: as for EM.

Required markers: "Point code", "Water system". [src: WaterLocationPanel.tsx:L134, L138]

Not validated: empty or duplicate names / codes of systems and locations (a location code is not checked for uniqueness); that the department exists; limit text format and order; sampling configuration unit and duplicates; that a limit row's test code is an assigned count test. [src: WaterMasterDataService.cs:L28-L48, L87-L93, L122-L147]

### MOD-AC-001 After Cleaning configuration
Server messages (exact text):
- `Machine {id} not found.` / `Machine part {id} not found.` / `Machine part configuration {id} not found.` [src: backend/MicroLIMS.Application/Services/MasterData/AfterCleaningMasterDataService.cs:L47, L60, L86, L100, L143, L163]
- `Cannot delete '{name}' - it still has {n} part(s). Delete those first.` [L64]
- `Cannot delete '{name}' - it has been used to receive {n} sample(s).` [L68]
- `Cannot delete '{name}' - it still has {n} test configuration(s). Delete those first.` [L104]
- `{testCode} ({testType}) is already configured for this part.` [L122]
- `{testCode} has already been tested on this part ({n} sample location(s)), so it cannot be removed.` [L166-L167]

Client messages (exact text):
- "Name is required." (machine) [AcPanels.tsx:L30]
- "Part name and machine are required." [AcPanels.tsx:L87]
- "Choose the test to run." (count test) [AcPanels.tsx:L156]
- "Some changes were not saved. {code}: {message}" (pathogen tests; per-code reasons "could not be added" / "could not be removed" when the server gives no message) [AcPanels.tsx:L238-L250]
- Fallbacks: "Could not save this test."; "Could not delete this record."; "Could not load the after-cleaning configuration."

Required markers: "Machine name", "Part name", "Machine". [src: AcPanels.tsx:L59, L113-L114]

Not validated: empty or duplicate machine and part names; the test type text (any text accepted); limit text format and order; that a pathogen configuration is an Observation test (the picker offers only Observation tests, the server does not check). [src: AfterCleaningMasterDataService.cs:L36-L42, L75-L81, L125-L138]

### MOD-ORG-001 Organisms master
Server messages (exact text):
- `Organism "{name}" already exists in the Organism list.` [src: backend/MicroLIMS.Application/Services/MasterData/OrganismMasterDataService.cs:L31, L46]
- `Organism {id} not found.` [L42, L64]
- `Cannot delete '{name}' - it is referenced by {n} record(s) (Media Configuration Challenges: {a}, Media Evaluation Challenges: {b}, Cryovials: {c}, Materials: {d}).` [L73-L75]

Client messages (exact text): "Scientific Name is required." (empty check only, not trimmed) [src: OrganismsPage.tsx:L58-L60]; success `Organism "{name}" added.` / `Organism "{name}" updated.`; delete confirmation `Delete organism "{name}"? This cannot be undone.` Required marker: "Scientific Name". Not validated: ATCC number, common name and description (free text, any length). [src: OrganismsPage.tsx:L58-L68, L105; OrganismMasterDataService.cs:L33]

### MOD-MEDIACFG-001 Media Configurations
Server messages (exact text):
- `Media product with ID {id} not found.` [src: backend/MicroLIMS.Application/Services/MediaProductService.cs:L89, L125, L166; MasterData/MediaMasterDataService.cs:L69, L178]
- `Incubation condition {id} not found.` / `Incubation condition with ID {id} not found.` / `Media configuration {id} not found.` [MediaMasterDataService.cs:L72, L169, L181, L300; MediaIncubationConditionService.cs:L98, L135]
- "The chosen incubation condition belongs to a different media product." [MediaMasterDataService.cs:L75, L184]
- `'{product}' already has an evaluation configuration. Edit it instead.` [MediaMasterDataService.cs:L81]
- "Valid evaluation type is required." [MediaMasterDataService.cs:L85, L188]
- "Recovery percent min cannot exceed recovery percent max." [MediaMasterDataService.cs:L92, L195]
- `Organism with ID {id} not found.` [MediaMasterDataService.cs:L112, L211]
- "Challenge role is required for Indication/Inhibition evaluation." [MediaMasterDataService.cs:L117, L216]
- "Duplicate challenge organism and role combination in request." [MediaMasterDataService.cs:L224]
- "A configuration can't be moved to another media product." [MediaMasterDataService.cs:L174]
- "Media product name is required." / "Media product name cannot exceed 200 characters." [MediaProductService.cs:L198, L201]
- "Media product code is required." / "Media product code must be 2-10 characters, start with an alphanumeric character, and contain only letters, digits, '.', or '-'." [MediaProductService.cs:L208, L211-L212]
- `A media product named '{name}' already exists.` / `A media product with code '{code}' already exists ('{name}').` [MediaProductService.cs:L223, L233]
- "A reason for the code change is required." / "The new code must be different from the current code." [MediaProductService.cs:L119, L128]
- Signature failure (code change): "Password verification failed. The signature was not applied." (also after too many attempts: "Too many failed signature attempts. Wait {n} minutes, then try again. The signature was not applied."). [src: backend/MicroLIMS.Application/Services/ElectronicSignatureService.cs:L55-L58, L65-L81] (verified)
- `Cannot delete media product '{name}' because it is referenced by {n} media configuration(s) and {m} material batch(es).` [MediaProductService.cs:L173-L174]
- `Cannot delete media product '{name}' because its incubation conditions are used by {n} Test Master step medium/media.` [MediaProductService.cs:L182-L183]
- "Incubation min hours must be greater than zero." / "Incubation max hours cannot be less than min hours." / "Temperature min must be below temperature max." [MediaIncubationConditionService.cs:L146, L148, L152]
- `'{product}' already has an incubation condition of {min}–{max} h at {tmin}–{tmax} °C.` [MediaIncubationConditionService.cs:L74-L75, L119-L120]
- `This incubation condition is used by {n} evaluation configuration(s) and {m} Test Master step medium/media, so it can't be changed or deleted. Add a new condition instead.` [MediaIncubationConditionService.cs:L162-L163]

Client messages (exact text): "Both product name and code are required." [AddMediaProductDialog.tsx:L33]; "Product name is required." [RenameMediaProductDialog.tsx:L28]; "This organism and role combination is already staged for this configuration." [MediaConfigurationDialog.tsx:L151]; "Choose an incubation condition." [MediaConfigurationDialog.tsx:L171]; "Recovery % min cannot exceed max." [MediaConfigurationDialog.tsx:L177]; "Enter the incubation hours and temperature range." [MediaIncubationConditionDialog.tsx:L62]. Fallbacks: "Failed to create media product."; "Failed to rename media product."; "Failed to save the incubation condition."; "Could not delete this incubation condition."; "Could not delete this media product."; "Failed to load media configuration data."

Required markers: media product - "Product Name", "Product Code"; change code - "New Code", "Reason"; configuration - "Expected Colony Description *" (when the role is Indication; the Add Organism button stays disabled until it is filled) and the "Challenge Role *" placeholder; incubation condition - all four fields. [src: AddMediaProductDialog.tsx:L74-L84; ChangeMediaProductCodeDialog.tsx:L74-L89; MediaConfigurationDialog.tsx:L128-L129, L351-L355; MediaIncubationConditionDialog.tsx:L123-L159] (verified)

### MOD-EQUIP-002 Equipment configuration
Server messages (exact text):
- `Equipment {id} not found.` [src: backend/MicroLIMS.Application/Services/MasterData/EquipmentMasterDataService.cs:L44, L101; EquipmentConfigurationService.cs:L182]
- "Name is required." / "Code is required." [EquipmentMasterDataService.cs:L51-L53, L106-L108]
- `Equipment code "{code}" already exists.` [EquipmentMasterDataService.cs:L55, L110]
- "Vendor cannot exceed 100 characters." [EquipmentMasterDataService.cs:L58, L113]
- "CDS Software is required for HPLC equipment." / "CDS Software is required for ICP-OES equipment." / "CDS Software is only allowed for HPLC and ICP-OES equipment." [EquipmentMasterDataService.cs:L63, L68, L73, L118, L123, L128]
- `Inventory equipment {id} not found.` [EquipmentConfigurationService.cs:L143]
- "The Microbiology laboratory section (code MICRO) is missing." [EquipmentConfigurationService.cs:L158]
- "A reason explaining the change to the set point temperature is required." [EquipmentConfigurationService.cs:L179]
- `Equipment {code} is not an incubator.` [EquipmentConfigurationService.cs:L185]
- `Autoclave equipment {id} not found.` / `Autoclave program {id} not found.` [EquipmentConfigurationService.cs:L260, L317, L366]
- `Program code "{code}" already exists on autoclave {code}.` [EquipmentConfigurationService.cs:L266, L321]
- Section messages: "Your account is not assigned to any laboratory section. Ask an administrator to assign one." / "Choose a laboratory section - you belong to more than one." / "Choose a laboratory section." / "You are not a member of the selected laboratory section." / "Selected laboratory section was not found or is inactive." [src: backend/MicroLIMS.Application/Services/UserSectionScopeService.cs:L116-L142]

Client messages (exact text): "Name, Code, and Type are required."; "Laboratory section is required."; "Please enter a valid numeric set point temperature."; "Reason for Change is required for audit and ALCOA+ compliance."; "Please fill out Program Code, Program Name, and Load Type."; fallbacks "Could not save equipment.", "Could not update set point.", "Could not save program.", "Status update failed." [src: EquipmentPage.tsx:L297-L317, L357-L383, L480-L520]

Required markers (MUI): "Equipment Name *", "Equipment Code *", "Equipment Type *"; set point "New Set Point (°C) *", "Reason for Change *"; program "Program / Load Code *", "Program / Load Name *", "Load Type *", "Temperature (°C) *", "Cycle Time (min) *", "Audit Comment / Reason *" (a marker only: the client does not check the comment and the server does not either, GAP-410). [src: EquipmentPage.tsx:L1200-L1478] (verified)

Not validated by the server: temperature and cycle time ranges (any number), program name and load type length, location length, the set point value range. [src: EquipmentConfigurationService.cs:L257-L361]

### MOD-TESTMASTER-001 Test Master (Microbiology)
Server messages (exact text):
- `Test code "{code}" already exists in the Test Master.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L41, L388]
- `Test {id} not found.` / `Workflow step {id} not found.` [TestDefinitionMasterDataService.cs:L379, L798, L807; TestWorkflowStepMasterDataService.cs:L28, L80, L131, L204, L231]
- "This test belongs to a laboratory section you are not assigned to." [TestDefinitionMasterDataService.cs:L394]
- Section messages as under Equipment (create). 
- "The workflow type of this test is set by its test type; change it in the test settings." [TestWorkflowStepMasterDataService.cs:L38]
- `{WorkflowType} tests have no workflow steps.` [TestWorkflowStepMasterDataService.cs:L82]
- `Material {id} not found.` / `Choose an incubation condition for medium '{material}'.` / `'{material}' isn't linked to a media product. Link it in Inventory > Materials Stock first.` / `Incubation condition {id} not found.` / `The incubation condition chosen for '{material}' belongs to a different media product.` [TestWorkflowStepMasterDataService.cs:L271-L283]
- `Cannot delete step "{name}" - it has already been used by a test order.` [L236]
- "This step is already at that end of the sequence." [L209]
- "Only one step per test can be marked as the final step." [L315]
- "Workflow steps must have contiguous step numbers starting from 1." [L330]
- Workflow template rules. The server returns all violations in one message, each as `Rule {n} ({step name}): {text}`, joined by a space. Rule texts (exact): [src: backend/MicroLIMS.Application/Services/WorkflowTemplateValidator.cs:L13-L109; TestWorkflowStepMasterDataService.cs:L303-L308] (verified)
  - Rule 1 (broth steps): "A broth step must have exactly one assigned medium, marked as required."; "A broth step must not target an organism."
  - Rule 2 (selective plating): "A selective plating step must have exactly one assigned medium, marked as required."; "A selective plating step must target an organism."
  - Rule 3 (confirmatory plating): "Confirmatory media count must be at least 1."; "A confirmatory plating step requires at least {n} permitted media, all analyst-selectable."; "A confirmatory plating step must have at least one permitted medium, all analyst-selectable."; "A confirmatory plating step must target an organism."
  - Rule 4 (biochemical): "A biochemical test step must have no assigned media."; "A biochemical test step must not target an organism."; "A biochemical test step must specify at least one phenotypic test type."; "Phenotypic test type {type} is assigned to this step more than once."
  - Rule 5: "Medium {materialId}: the minimum temperature must be below the maximum."
  - Rule 6: "Medium {materialId} is assigned to this step more than once."
  - Rule 7 (stage 2): "A step requiring incubation transfer must define stage 2's temperature and incubation-hours range."; "Stage 2's minimum temperature must be below its maximum."; "Stage 2's incubation-hours range must have a positive minimum and a maximum no less than the minimum."; "Only a PlateCount step with incubation transfer enabled may define a second incubation stage."
  - Rule 8: "At least one medium is required for this step type."; "Only a biochemical test step may specify a phenotypic test type."
  - Rule 9: "Medium {materialId}: the incubation-hours range must have a positive minimum and a maximum no less than the minimum - set it on the media configuration."
  (The earlier list gave rule 6 as "Medium {type} is assigned ..." and omitted the prefix; corrected.)

Client messages (exact text): "Both Code and Display Name are required."; "Laboratory section is required."; "Could not update the workflow type."; "Step Name is required."; "Step Name and at least one Phenotypic Test Type are required."; the step-form messages listed under SCR-TESTMASTER-001; fallbacks "Could not add this step." / "Could not update this step." / "Could not reorder this step." / "Could not delete this step." / "Could not add this test." / "Could not update this test." / "Could not update this test's status." [src: TestMasterPage.tsx:L246-L256, L545, L562; WorkflowStepsSection.tsx:L77, L162, L206, L217, L228] (verified)

Required markers: "Code", "Display Name", "Section" (MUI required); step "Step Name" (client check). [src: TestMasterPage.tsx:L730-L753]

## C5 Media, strains, stock

<!-- from C5-media-strains-stock.md -->

Server (exact text, file:line):
- "Selected equipment is not a valid autoclave." [src: backend/MicroLIMS.Application/Services/MediaPreparationService.cs:L51-L52]
- "Batch {batch} of {material} isn't linked to a configured media product - edit it in Inventory > Materials Stock and choose the product before preparing." [MediaPreparationService.cs:L61-L63]
- "Media product with ID {id} not found." [MediaPreparationService.cs:L65-L66]
- "No Media Configuration exists yet for \"{product}\" - configure it in Laboratory Configuration before preparing a lot." [MediaPreparationService.cs:L71-L75]
- "Lot number {lot} was taken by another preparation at the same moment. Nothing was saved - submit the preparation again." [MediaPreparationService.cs:L131-L132]
- "Media lot {lot} is not currently released for use and cannot be marked Out of Stock." [MediaPreparationService.cs:L167]
- "Media lot {lot} has already been decided ({status})." [backend/MicroLIMS.Application/Services/MediaReleaseService.cs:L65]
- "Media lot {lot} has no evaluation record." [MediaReleaseService.cs:L68]
- "Media lot {lot}'s evaluation is not complete yet." [MediaReleaseService.cs:L76]
- "Media lot {lot}'s evaluation did not conform - it cannot be released." [MediaReleaseService.cs:L78]
- "You cannot approve a media lot you prepared or evaluated." [MediaReleaseService.cs:L82]
- "Media Configuration for lot \"{lot}\" is missing an incubation condition." [backend/MicroLIMS.Application/Workflows/MediaEvaluationEngine.cs:L149-L150]
- "Incubation must be recorded before a result can be entered." [MediaEvaluationEngine.cs:L195]
- "Incubation is still in progress - earliest reading time is {yyyy-MM-dd HH:mm} UTC." [MediaEvaluationEngine.cs:L197-L198]
- "Old and new media counts are required." [MediaEvaluationEngine.cs:L204]
- "Old media count cannot be zero - recovery% cannot be calculated." [MediaEvaluationEngine.cs:L206]
- "A reference lot is required - either a linked prior lot or a free-text description, not both or neither." [MediaEvaluationEngine.cs:L211]
- "Reference media lot {id} belongs to another laboratory section." [MediaEvaluationEngine.cs:L220]
- "Growth observed is required." [MediaEvaluationEngine.cs:L236]
- "Observed description and a Conform/NonConform judgment are required." [MediaEvaluationEngine.cs:L243]
- "Turbid/clear is required." [MediaEvaluationEngine.cs:L250]
- "Cryovial batch {code} belongs to another laboratory section and cannot challenge this media lot." [MediaEvaluationEngine.cs:L71]
- "Cryovial batch {code} is {organism}, not {challenge organism}." [MediaEvaluationEngine.cs:L76]
- Storage: "Request payload is required." [backend/MicroLIMS.Application/Services/MediaStorageService.cs:L48]; "Invalid media storage form '{form}'. Valid values are Plates, AgarBottles, BrothBottles, Tubes." [L51]; "Quantity must be greater than zero." [L54, L147]; "Exactly one of EquipmentId or LocationText must be provided." [L59, L152]; "Media lot {lot} is not currently released for use and active." [L65, L158]; "Equipment {code} is not currently in service." [L77, L178]; "A reason is required to move media storage." [L144]; "Cannot move {n} units; only {m} available in entry {id}." [L166]; "Destination storage location must be different from the source storage location." [L193]; "Media storage entry {id} not found for media lot {lot}." [L163]
- Expiring list: none.

Client (exact text):
- "Please select an Autoclave." [frontend/src/modules/laboratoryConfiguration/media/dialogs/MediaPreparationDialog.tsx:L123-L126]
- "Please select an Autoclave Program / Load." [MediaPreparationDialog.tsx:L127-L130]
- "Please select an Autoclave Program with valid Load Type." [MediaPreparationDialog.tsx:L131-L134]
- "Autoclave temperature is required." [MediaPreparationDialog.tsx:L135-L136]
- "Autoclave cycle time is required." [MediaPreparationDialog.tsx:L139]
- Remaining preparation checks (verified, in the order the dialog runs them): "Please select a Dehydrated Media Stock item from inventory."; "The selected dehydrated media is not linked to a media product. Edit it in Inventory > Materials Stock before preparing."; "Please enter the total weight."; "Please enter the total volume."; (autoclave messages above); "Please specify the cycle number."; "Please specify the pH."; "Please specify the Expiry Date." Presence checks only: no numeric range, no future-date check in the browser [src: MediaPreparationDialog.tsx:L105-L153]. Server-side the same quantities are not range-checked either (BR-MEDIA-033).
- Evaluation dialog: no client-side validation messages; it relies on the server messages listed above, and the server messages for disk selection and cryovial selection (BR-MEDIA-017) (verified) [src: MediaEvaluationWorkflowDialog.tsx:L134-L182].
- Storage dialog (client, verified): Save is disabled until the quantity is valid, a location is valid and (for a move) the reason has at least 5 characters; helper "Reason must be at least 5 characters." [src: frontend/src/modules/laboratoryConfiguration/media/components/MediaStoragePanel.tsx:L130, L196].

### MOD-CRYO-001 Cryovials
Server:
- "Physical check confirmation against the organism reference description is required." [src: backend/MicroLIMS.Application/Services/CryovialService.cs:L56-L57]
- "Material {id} not found." [CryovialService.cs:L59-L60]; "Material {name} is not a Lyophilized Microorganism item." [L62-L63]; "Set an Organism on material \"{name}\" (Inventory > Materials Stock) before preparing cryovials from it." [L65-L66]
- "Media {id} not found." [L70-L71]; "Media lot {lot} is not GPT-released, out of stock, or rejected - cannot be used for identity confirmation." [L72-L73]
- "At least one identity confirmation row is required." [L79-L80]
- "Cryovial code {code} was taken by another preparation at the same moment. Nothing was saved - submit the preparation again." [L126-L127]
- "Cryovial batch {code} has already been decided ({status})." [L148-L149]; "You cannot approve a cryovial batch you prepared." [L151-L152]
- Thaw: "Cryovial batch {code} is not approved - cannot thaw a vial." [L188-L189]; "Cryovial batch {code} has been destroyed and cannot be used." [L190-L191]; "Cryovial batch {code} is expired and cannot be used." [L192-L193]; "No vials remaining in batch {code}." [L194-L195]
- Move storage: "A reason is required to move cryovial storage." [L219-L220]; "Exactly one of EquipmentId or LocationText must be provided." [L224-L225]; "Cryovial batch {code} is not approved - cannot move storage." [L235-L236]; "Cryovial batch {code} has been destroyed and cannot be moved." [L237-L238]; "Equipment {code} is not currently in service." [L248-L249]
- Challenge use: "Cryovial batch {code} is not approved for use." and "...is expired and cannot be used." [backend/MicroLIMS.Application/Workflows/MediaEvaluationEngine.cs:L86-L89]; "Cryovial batch {code} belongs to another laboratory section and cannot challenge this media lot." [MediaEvaluationEngine.cs:L71]
Client:
- "Please select a Lyophilized Microorganism material." [frontend/src/modules/laboratoryConfiguration/cryovials/components/PrepareCryovialBatchDialog.tsx:L112]
- "Please enter a valid number of vials prepared (> 0)." [PrepareCryovialBatchDialog.tsx:L113-L114]
- "Please enter the number of discs used (0 or more)." [PrepareCryovialBatchDialog.tsx:L115-L116]
- "Please enter an expiry date." [PrepareCryovialBatchDialog.tsx:L117]
- "Please select a Storage Equipment (Freezer / Deep Freezer)." [PrepareCryovialBatchDialog.tsx:L118]
- "Please confirm the physical check against the reference description." [PrepareCryovialBatchDialog.tsx:L119]
- "At least one Identity Confirmation row is required." [PrepareCryovialBatchDialog.tsx:L120]
- "Row {n}: Please select a GPT-released media." / "Row {n}: Please select an incubator." / "Row {n}: Please specify the incubation start date." / "Row {n}: Please specify the incubation end date." [PrepareCryovialBatchDialog.tsx:L123-L126]
- Destroy failure fallback: "Failed to destroy cryovial batch." [frontend/src/modules/laboratoryConfiguration/cryovials/components/DestroyCryovialDialog.tsx:L43]
- Thaw reason (client only, verified): the "Confirm Thaw" button stays disabled until the reason has at least 5 characters (trimmed) and at most 500; the server accepts an empty note (BR-CRYO-009) [src: ThawVialReasonDialog.tsx:L45-L48; CryovialService.cs:L183-L199]. Failure fallback "Failed to thaw cryovial."
- Move storage reason (client only): at least 5 characters, at most 500; the server only requires a non-blank reason [src: MoveCryovialStorageDialog.tsx:L60, L76-L90; CryovialService.cs:L219-L220].
- Prepare fallback "Could not prepare cryovial batch." [PrepareCryovialBatchDialog.tsx:L167]

### MOD-MATERIAL-001 Materials Stock
Server:
- Purity: "Purity is required for primary standards." / "Purity is required for reference standards." [src: backend/MicroLIMS.Application/Services/MaterialService.cs:L72-L73]; "Purity must be greater than 0 and less than or equal to 100." [L75]; "Purity is only allowed for reference and primary standards." [L80]
- Moisture: "Moisture content is only allowed for reference standards." [L111]; "Moisture content must be at least 0 and below 100." [L112]
- Master entry: "That master entry belongs to another laboratory." [L102]; "Master entry \"{code}\" is inactive." [L103]; "Master entry \"{code}\" is a {category}, not a {category}." [L104]; "Material master entry {id} not found." [L101]; "Choose the material master entry for this lot." [L166]
- Media product: "Choose the configured media product for this dehydrated media." [L146]; "Media product with ID {id} not found." [L149]; "Lots have already been prepared from batch {batch} - its media product can't be changed." [L238]
- Working standards: "Working standards are created by approving a qualification, not received here." [L137]; "Working standards change only through their qualifications." [L220]
- Custom type: "A custom material type can only be saved with the type Other." [L323]; "A material type name can be at most {n} characters." [L334]; "'{name}' is the name of a built-in material type - pick it from the list or use a different name." [L338]
- Consumption: "Material {name} is not a {type} item." [L385]; "Material {name} (batch {batch}) is expired and cannot be used." [L388]; "A current Certificate of Analysis (COA) is required before {name} (batch {batch}) can be used. Please upload a valid COA in the Inventory module first." [L400-L402]; "Insufficient stock: {name} (batch {batch}) has {remaining} {unit} remaining, {requested} requested." [L405-L407]
- Documents: "A supersession reason is required." [backend/MicroLIMS.Application/Services/MaterialDocumentService.cs:L250]; "A voided document cannot be superseded. Upload a new document instead." [L256-L257]; "A void reason is required." [L337]; "This document is already voided." [L343-L344]; "Document storage failed. The upload was not completed." [L186]; "The document file could not be retrieved." [L214]; "Document integrity check failed. The file may have been altered. Please contact your System Administrator." [L225]; "Replacement file storage failed. The supersession was not completed." [L316]; upload file-type and size errors (verified): "File exceeds the maximum allowed size of {n} MB.", "The uploaded file is empty.", "File type '{ext}' is not supported. Allowed types: PDF, JPG, JPEG, PNG, WEBP, TIFF.", "The declared content type '{type}' does not match the file extension '{ext}'.", "The file content could not be validated because it is too short.", "The file does not appear to be a valid WEBP image.", "The file content does not match the expected format for '{ext}'. The file may be corrupted or misnamed." [src: backend/MicroLIMS.Application/Services/MaterialDocumentFileValidator.cs:L44-L80]. Controller refusals: "A replacement file is required for supersession.", "A supersession reason is required." [backend/MicroLIMS.API/Controllers/MaterialDocumentController.cs:L121-L125].
Client:
- "Choose the configured media product for this dehydrated media." [frontend/src/modules/inventory/materials/components/AddMaterialDialog.tsx:L581]; "Choose the material master entry for this lot." [AddMaterialDialog.tsx:L586]
- "Purity percentage is required for primary standards." / "...for reference standards." [AddMaterialDialog.tsx:L594-L595]; "Moisture content must be at least 0 and below 100." [AddMaterialDialog.tsx:L607]
- "Material name, batch/lot number, receiving date, storage location, and quantity received are required." [AddMaterialDialog.tsx:L621]; "Quantity received must be greater than zero." [L626]; "Laboratory section is required." [L631]
- "Please select a file." [frontend/src/modules/inventory/materials/components/UploadMaterialDocumentDialog.tsx:L59]; "A reason is required." [VoidMaterialDocumentDialog.tsx:L82]; "Void operation failed. Please try again." [VoidMaterialDocumentDialog.tsx:L31]; "Failed to load materials stock." [frontend/src/modules/inventory/materials/MaterialsPage.tsx:L139]; "Failed to load documents." [frontend/src/modules/inventory/materials/components/MaterialDocumentList.tsx:L59]

### MOD-EQUIP-001 Equipment Inventory
Server:
- "Choose the laboratory this asset belongs to." [backend/MicroLIMS.Application/Services/EquipmentInventoryService.cs:L118]; "Selected laboratory section was not found or is inactive." [L123]; "This equipment belongs to a laboratory section you are not assigned to." [L127]
- "Equipment code \"{code}\" already exists." [L135, L169]; "A comment explaining the reason for changing the operational status is required." [L176]
- "Equipment {id} not found." [L165, L211, L285, L295]
- Storage refusals: "Equipment {code} is not currently in service." [backend/MicroLIMS.Application/Services/MediaStorageService.cs:L77, L178; CryovialService.cs:L249]
- Incubator (grouped test action): "Incubator equipment {id} was not found or is not an incubator." [backend/MicroLIMS.Application/Services/GroupedTestAction/BatchSelectMediaRules.cs:L36]; "Incubator \"{code}\" has no calibrated set point temperature." [L40]; "Incubator \"{code}\" calibration expired on {date}." [L44]
- Documents: "Equipment {id} not found." [backend/MicroLIMS.Application/Services/EquipmentDocumentService.cs:L86, L112]; "Document {id} not found for equipment {eq}." [EquipmentDocumentService.cs:L167]; "Equipment document {id} not found." [EquipmentDocumentService.cs:L330]
Client:
- "Instrument type, equipment code, and location are required." [frontend/src/modules/inventory/equipment/components/RegisterEquipmentDialog.tsx:L99]; "Choose the laboratory this asset belongs to." [RegisterEquipmentDialog.tsx:L104]; "A comment explaining the operational status change is required." [RegisterEquipmentDialog.tsx:L109] (wording differs from the server text above)
- "Failed to load calibration certificates." [frontend/src/modules/inventory/equipment/components/EquipmentDocumentList.tsx:L58]
- Equipment document refusals (verified): same file-type and size messages as materials (shared validator); controller: "A replacement file is required.", "A reason for supersession is required.", "A reason for voiding is required."; service: "A void reason is required.", "This document is already voided.", "Document integrity check failed. The file may have been altered. Please contact your System Administrator." [src: EquipmentDocumentController.cs:L72-L108; EquipmentDocumentService.cs:L164-L190, L244-L295].

## C6 Physicochemical

<!-- from C6-physchem.md -->

Exact messages as coded (server side unless marked client). Module tag in brackets.

- [HPLC] "\"{instrument}\" already has an open run." [HplcRunService.cs:L275]
- [HPLC] "\"{instrument}\" is not a HPLC|GC instrument; method {abbr} needs one." [HplcRunService.cs:L292]
- [HPLC] "HPLC method \"{name}\" is inactive." [HplcRunService.cs:L295]
- [HPLC] "The HPLC method belongs to another laboratory." [HplcRunService.cs:L298]
- [HPLC] "Column \"{name}\" is inactive." / "Column \"{name}\" is not compatible with \"{instrument}\"." / "Column {code} has no USP designation; set it in the column master." / "Column {code} is USP {x}; method {abbr} requires {y}." [HplcRunService.cs:L305-L313]
- [HPLC] "Select a prepared mobile phase for every channel this method uses." / "Select a prepared mobile phase for channel {c}." [HplcRunService.cs:L317-L325]
- [HPLC] "Channel {c}: the selected preparation is not a mobile phase." / "... is not of this channel's solution." / "... was not prepared for this method." / "... is not currently prepared." [HplcRunService.cs:L331-L337]
- [HPLC] "The run is closed." / "System suitability has already been confirmed." [HplcRunService.cs:L395-L397, L471-L473]
- [HPLC] "{analyte}: standard weight must be greater than zero." / "{analyte}: standard material not found." / "{analyte}: lot {lot}: {reason}" [HplcRunService.cs:L413-L423]
- [HPLC] "Upload the standard report before confirming system suitability." [HplcRunService.cs:L107, L480]
- [HPLC] "Complete these entries and save before confirming. {analyte}: {fields}." [HplcRunService.cs:L141-L142]
- [HPLC] "Channel {c}: the mobile phase preparation has expired since it was selected." [HplcRunService.cs:L488]
- [HPLC] "Sample assignment is locked until system suitability passes." / "Select at least one test." / "Test order N is already assigned to run X." / "Test order N is not eligible for this run." [HplcRunService.Samples.cs:L95-L116]
- [HPLC] "Dissolution needs an HPLC method with exactly one analyte." / "Method X needs a standard dilution before dissolution samples can be assigned." [HplcRunService.Samples.cs:L127-L129]
- [HPLC] "The qualification has already been submitted." / "A result has already been sent for review for this sample." / "This sample was already removed from the run." [HplcRunService.Samples.cs:L151-L159]
- [HPLC] "Replicate N: the sample weight must be greater than zero." / "... exactly one response for {analyte} is required." / "... the response for {analyte} must be greater than zero." [HplcRunService.Samples.cs:L197-L214]
- [HPLC] "Dissolution results are entered on the Testing page, not as replicates." [HplcRunService.Samples.cs:L189]
- [HPLC] "{N} assigned sample(s) have not been sent for review. Submit or remove them first." [HplcRunService.Samples.cs:L274]
- [HPLC] "This evidence has already been superseded." / "A reason is required." / "Reason cannot exceed 500 characters." [HplcRunService.cs:L638-L642, L778-L784]
- [HPLC] "The uploaded file is empty." / "File exceeds the maximum allowed size of 25 MB." / "A file name is required." / "File type \"{t}\" is not supported. Allowed types: PDF, PNG, JPEG." [HplcRunService.cs:L716-L724]
- [HPLC sample] "This sample was removed from the run." / "The result has already been sent for review." / "The run is closed." [HplcSampleEntryContext.cs:L40-L46]
- [HPLC sample] "System suitability has not passed." / "Expected exactly N replicates for stage role R, but M entered." [HplcSampleEntryContext.cs:L53-L66]
- [HPLC sample] "Upload the sample report before sending for review." [HplcSampleEntryContext.cs:L50-L52]
- [HPLC] "Document why this run is being abandoned (e.g. system suitability failure, instrument pressure error)..." (placeholder text, not a validation) [SystemSuitabilityPanel.tsx:L382]
- [HPLC SST evaluation] "{analyte}: N injections required, M entered." / "... injection K response must be greater than zero." / "... RSD X% exceeds Y%." / "... {criterion} is required." / "... {criterion} X is below Y." / "... {criterion} X is above Y." [HplcSstEvaluator.cs:L38-L104]
- [Method master HPLC] "Abbreviation is required." / "Abbreviation must be 2-20 uppercase letters, digits or hyphens." / "An HPLC method abbreviated \"{abbr}\" already exists." / "At least one analyte is required." / "At least one mobile phase is required." / "Analyte wavelength must be between 190 and 900 nm." / "Standard injections must be at least 1." / "Mobile phase ratios must sum to 100%." / "Gradient elution requires at least 2 steps." / "The first gradient step must start at time 0." / "Gradient step times must strictly increase." / "Isocratic elution must not have gradient steps." / "A reason is required." [src: backend/MicroLIMS.Application/Services/HplcMethodService.cs]
- [Method master GC] "A GC method needs at least one oven program step." / "Carrier gas is required for a GC method." / "Film thickness is required for a GC method." / "Inlet and detector temperatures are required for a GC method." / "Split ratio must be 1 or more (leave empty for splitless)." / "Residual solvents mode is only available for GC methods." / "Sample solution volume is required for residual solvents." [src: backend/MicroLIMS.Application/Services/HplcMethodService.Gc.cs; HplcMethodService.cs]
- [Method master ICP] "Method name is required." / "Abbreviation is required." / "At least one element is required." / "Element symbol must be 1-3 letters." / "Minimum correlation must be greater than 0 and at most 1." / "Calibration age must be between 1 and 168 hours." / "Blank limit (mg/L) is required when the blank check is on." / "ICV standard, nominal (mg/L) and recovery limits are required when the ICV check is on." / "CCV nominal (mg/L) and recovery limits are required when the CCV check is on." / "The ICV standard must be a second source, not the calibration standard." / "Dilution factor must be 1 or more." / "Sample volume must be greater than zero." / "A reason is required." [src: backend/MicroLIMS.Application/Services/IcpMethodService.cs]
- [ICP calibration] "Choose the calibration standard lot." / "Choose the ICV standard lot." / "Upload the Syngistix calibration report before confirming." / "The calibration has already been confirmed." / "The run is closed." / "r must be between 0 and 1." / "The {which} standard lot must be a usable lot of {code}." [src: IcpRunService.Calibration.cs:L11, L18-L27, L50-L61, L71-L94]
- [ICP] "Confirm a valid calibration before entering CCV." / "The element does not belong to this method." / "The measured value cannot be negative." / "This method does not use CCV." [src: IcpRunService.Ccv.cs:L15-L29]
- [ICP] "No specification is configured for this test on this item." / "Elemental impurities are reported per gram; enter the sample amount in g." / "Enter the unit amount (average unit weight or dose)." / "Replicate N: a concentration belongs to an element that is not needed by this test." / "Replicate N: exactly one concentration for {symbol} is required." [src: IcpRunService.Samples.cs:L170-L194]
- [ICP evaluator] "{symbol}: replicate N is above the top standard - dilute and re-measure." / "{symbol}: {element state reason}" / "Enter the unit amount (average unit weight or dose)." [src: IcpSampleEvaluator.cs:L39-L60]
- [ICP] "Calibration is not confirmed." / "Calibration expired at yyyy-MM-dd HH:mm." / "Failed calibration: {reasons}" / "CCV failed (X%)." / "Needs a passing CCV." [src: IcpElementAvailability.cs:L35-L45]
- [SOLN] "Solution \"{name}\" is inactive." / "An HPLC method is required to start a mobile phase preparation." / "That method does not use this mobile phase." / "An HPLC method only applies to mobile phase preparations." [SolutionPreparationService.cs:L82-L105]
- [SOLN] "This preparation is closed." / "Quantity used for \"{x}\" must be greater than zero." / "Final volume must be greater than zero." / "Measured pH must be between 0 and 14." / "Component {id} does not belong to this preparation." [SolutionPreparationService.cs:L158-L195]
- [SOLN complete] "Every component needs a lot and a quantity before completion." / "Final volume is required before completion." / "Measured pH is required before completion." / "Measured pH X is outside T ± tol." / "{component}: the selected lot no longer exists." / "Another preparation took that code at the same moment - sign again." [SolutionPreparationService.cs:L239-L312]
- [SOLN lot] "Lot is not linked to this reagent." / "Expired on yyyy-MM-dd." / "No stock left." / "Only N UNIT left." [LotUsability.cs:L15-L25]
- [SOLN cancel/discard] "A reason is required." / "Reason cannot exceed 500 characters." [SolutionPreparationService.cs:L441-L444]
- [Titrant standardization] "Only titrant preparations can be standardized." / "This preparation is expired or discarded and cannot be standardized." / "This titrant is not configured for standardization." / "Exactly N replicate(s) are required." / "Replicate N: titrant volume must be greater than zero." / "Replicate N: a blank is required." / "Replicate N: this titrant does not use a blank." / "Replicate N: a standard lot and weight are required." / "This titrant has no primary standard configured." / "Replicate N: standard lot (batch X): {reason}" / "Replicate N: standard lot (batch X) is not a primary standard." / "Standard lot X: {reason}" / "... standard lot (batch X) has no purity - enter it in Materials Stock before standardizing." / "Replicate N: a reference preparation and volume are required." / "Replicate N: reference preparation is expired or discarded." / "Temperature must be between -20 and 60 C." [TitrantStandardizationService.cs:L62-L238]
- [Titration context] "This titrant preparation has not been standardized." / "Titrant standardization is due - restandardize before use." / "This titrant must be restandardized before each use - it was not standardized today." / "Restandardize this titrant and record the temperature - temperature correction needs it" [TitrationContextService.cs:L160-L196]
- [Titration recorder] "Exactly N replicate(s) are required." / "A blank volume is required for this test." / "This test does not use a blank." / "The titration temperature is required for temperature correction." / "This test does not use temperature correction." / "Specification '{name}' has no valid titration result basis." / "Loss on drying / water (0 to below 100) is required for a dried or anhydrous basis." / "Loss on drying / water % is only used with a dried or anhydrous basis." / "The average unit weight (mg) is required for a label claim or mg per unit basis." / "The average unit weight is only used with a label claim or mg per unit basis." / "The selected equipment is not a titrator." / "The titrant has no current factor." / "The excess titrant preparation is required for residual titration." / "An excess titrant is only used in residual titration." / "The titrant is due for standardization - acknowledge it and give a justification to continue." / "The justification must be 10 to 500 characters." / "No titrant warning to acknowledge." / "One to three standard titrations are required for the relative calculation." / "All standard titrations must use the same standard lot." / "Standard weight must be greater than zero." / "Standard titre must be greater than zero." / "The standard lot belongs to another laboratory." / "The standard lot must be a reference standard or an approved working standard." / "Standard lot X has no purity recorded." / "Standard titrations are only used with the relative calculation." [TitrationRecorder.cs:L66-L194]
- [Titration calc] "The titrant volume must be greater than the blank volume." / "The blank volume must be greater than the sample titrant volume." / "The back-titrated amount exceeds the excess titrant added." / "Reference volume must be greater than zero." / "Titrant strength must be greater than zero." [TitrationEngine.cs:L32-L137]
- [Titration definition] "Equation type must be Titration when workflow type is Titration." / "Replicate count must be between 1 and 10 for titration tests." / "Titration tests must not require system suitability." / "Titration type is required." / "Titration mode (direct or residual) is required." / "Titration calculation method is required." / "Titration endpoint is required." / "Titrant is required." / "Karl Fischer tests need a titrant whose strength unit is mg water per mL." / "A titrant with strength unit mg water per mL can only be used for Karl Fischer tests." / "Karl Fischer tests support only the USP factor calculation in direct mode." / "Non-aqueous titration only applies to acid-base titrations." / "Karl Fischer tests cannot use residual mode." / "Residual titration needs the excess titrant." / "The excess titrant must have a nominal strength and the same strength unit as the titrant." / "The relative calculation supports direct mode only." / "The reference standard is required for the relative calculation." / "Maximum RSD must be greater than zero when set." / "Indicator is required for a visual endpoint." / "Temperature correction only applies to non-aqueous acid-base titrations." [src: backend/MicroLIMS.Application/Services/MasterData/TitrationDefinitionRules.cs:L20-L146]
- [Gravimetric] "Initial sample weight (W1) must be greater than zero." / "Final weight (W2) cannot be negative." / "Final weight (W2) cannot be greater than initial weight (W1)." / "Password is required to sign the result." [GravimetricCalculator.cs:L69-L74; TestWorkflow recorders]
- [Dissolution standard from the HPLC run] "Assign this sample to an HPLC run with a passed system suitability." / "System suitability {code} has not passed." / "Dissolution needs an HPLC method with exactly one analyte." / "Method X has no standard dilution for {analyte}." / "{analyte}: the system suitability standard values are incomplete." [src: backend/MicroLIMS.Application/Services/HplcDissolutionStandard.cs:L23-L55]. These are checked after the vessel input checks and before the signature.
- [Dissolution] "Medium volume must be greater than zero." / "Dilution factor must be greater than zero." / "Stage 1 dissolution requires exactly 6 vessel areas." / "Vessel area must be greater than zero." / "Conditions are required." / "Expected N condition fields, but received M." / "Condition field \"{label}\" is required." / "Unexpected condition field \"{key}\"." / "Dissolution evaluation requires 6 (Stage 1), 12 (Stage 2), or 24 (Stage 3) units, but received N." / "Q must be greater than 0 and less than or equal to 100." [DissolutionRecorder.cs:L29-L73; DissolutionStageEvaluator.cs:L24-L28]
- [Disintegration] "Unit minutes are required." / "Disintegration time must be greater than zero." / "Stage 1 disintegration requires exactly N units." / "Stage 2 disintegration requires exactly M units." / "Invalid existing unit count (N) for next stage." / "Disintegration time limit must be greater than zero." [DisintegrationRecorder.cs:L29-L292; DisintegrationStageEvaluator.cs:L29-L44]
- [Weight variation] "Units are required." / "Specification dosage form is not configured." / "Stage 1 weight variation requires exactly N units." / "Tablets do not support Stage 2 evaluation." / "Stage 2 weight variation requires exactly M units." / "Invalid existing unit count (N) for next stage." [WeightVariationRecorder.cs:L30-L337]
- [Working standard sign-off] "A failed qualification can only be rejected." / "The analyst who submitted the qualification cannot review it." / "The analyst who submitted the qualification cannot reject it at review." / "The analyst who submitted the qualification cannot reject it at approval." / "The reviewer cannot also reject the qualification at approval." / "The analyst who submitted the qualification cannot approve it." / "The reviewer cannot also approve the qualification." / "Enter a reason." / "The reason must be 500 characters or fewer." / "The run that carried this assay was abandoned - reject the qualification." / "Another approval took that working standard code at the same moment - sign again." [WorkingStandardService.SignOff.cs:L21-L162]
- [Working standard calc] "Exactly 6 replicates are required (N entered)." / "RSD X % is above 2.0 %." / "Moisture content must be at least 0 and below 100." [WorkingStandardCalculator.cs:L35-L56]
- [Materials] "Choose the material master entry for this lot." / "That master entry belongs to another laboratory." / "Working standards change only through their qualifications." [MaterialService.cs:L101-L220]
- [Master code] "Material master entry code \"X\" already exists." / "This entry has linked stock lots; its category can't change." / "Indicator fields are only allowed for indicators." / "Transition range 'from' must not be above 'to'." / "Code is required." / "Name is required." [MaterialMasterService.cs:L67-L163]
- [Columns] Run start: "Column \"X\" is inactive." Column master (verified): "Column code is required." / "Column name is required." / "Column code \"X\" already exists." / "Serial number cannot exceed 100 characters." / "USP designation cannot exceed 10 characters." (designation is trimmed and upper-cased) / "One or more specified compatible equipment items do not exist." / "Equipment \"X\" is not an HPLC or GC instrument. Only chromatography equipment can be linked to a column." / "Equipment \"X\" belongs to another section. Compatible equipment must belong to the same laboratory section as the column." Deactivate has no further check (no test for open runs); the update request can set the column active again. [src: backend/MicroLIMS.Application/Services/ChromatographyColumnService.cs:L44-L230]
- [Client] ICP calibration confirm is disabled with the server reason shown as tooltip. [IcpCalibrationPanel.tsx:L331]
- [Client] "Please select a file." and "A reason is required." appear in materials upload/void dialogs (not physchem). Not included.
- [Client] Working standard SignOffDialog: password field "Password"; isValid requires password. [src: frontend/src/modules/workingStandards/qualifications/SignOffDialog.tsx:L87, L197-L199]

## C7 General masters

<!-- from C7-masters.md -->

Verification note: the quoted messages were checked mechanically with `git grep -F` against backend and frontend source (203 messages with enough fixed text were tested; 10 did not match as written; 6 of those are interpolated or line-split forms of existing strings and 4 were invented and are removed below). The earlier claim of "205 validation messages" is not a verified count. Line numbers in the `[src]` tags were not all re-checked; the text of each remaining message is real.

### MOD-ITEM-001 Items
- Client validations:
  - Add/Edit Item: `Item Name, Item Code, and at least one assigned test are required.` [src: frontend/src/modules/laboratoryConfiguration/items/components/AddItemDialog.tsx:L64].
  - Add/Edit Item: `Failed to save item.` (fallback) [src: frontend/src/modules/laboratoryConfiguration/items/components/AddItemDialog.tsx:L79].
  - Item list load failure: `Could not load items.` [src: frontend/src/modules/laboratoryConfiguration/items/ItemsPage.tsx:L38].
  - Item delete failure: `Could not delete item.` [src: frontend/src/modules/laboratoryConfiguration/items/ItemsPage.tsx:L158].
  - Item freeze/unfreeze failure: `Could not update item status.` [src: frontend/src/modules/laboratoryConfiguration/items/ItemsPage.tsx:L174].
  - Upload Document: `Please select a document file to upload.` [src: frontend/src/modules/laboratoryConfiguration/items/components/UploadItemDocumentDialog.tsx:L52].
  - Upload Document failure: `Failed to upload document.` [src: frontend/src/modules/laboratoryConfiguration/items/components/UploadItemDocumentDialog.tsx:L64].
  - Load documents failure: `Failed to load documents.` [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L75].
  - Preparation config load failure: `Could not load the preparation configuration.` [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemPreparationConfigurationSection.tsx:L37].
  - Preparation config save failure: `Could not save the configuration.` [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemPreparationConfigurationSection.tsx:L77].
  - Preparation config approve failure: `Could not approve the configuration.` [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemPreparationConfigurationSection.tsx:L91].
- Server validations:
  - Item code duplicate: `An item with code '{request.Code}' already exists.` [src: backend/MicroLIMS.Application/Services/ItemService.cs:L40, L71].
  - Item category invalid: `Items can only be configured for Product, Raw Material, or Packaging Material categories. Water, Environmental Monitoring, After Cleaning, and GPT are managed via their dedicated configuration pages.` [src: backend/MicroLIMS.Application/Services/ItemService.cs:L88-L92].
  - Item not found: `Item {id} not found.` [src: backend/MicroLIMS.Application/Services/ItemService.cs:L67, L108, L120].
  - Item delete blocked by samples: `Cannot delete '{item.Name}' - it has been used to receive {sampleCount} sample(s). Freeze it instead to stop new samples without losing history.` [src: backend/MicroLIMS.Application/Services/ItemService.cs:L124-L125].
  - Document size: `File size must be between 1 byte and 25 MB.` [src: backend/MicroLIMS.Application/Services/ItemDocumentService.cs:L74-L75]. Controller, no file: `No file provided.` [src: backend/MicroLIMS.API/Controllers/ItemDocumentController.cs:L58-L59].
  - Document not found: `Document {documentId} not found.`; item not found: `Item {itemId} not found.` [src: backend/MicroLIMS.Application/Services/ItemDocumentService.cs:L37, L72, L148].
  - Receipt-time refusals caused by item configuration: `Item '{name}' is frozen and cannot be used to receive new samples.`; `Item '{name}' has no assigned tests. Configuration must be completed by the Section Head before samples can be received.`; `Item '{name}' has no tests for {section}.`; `Production stage is required for a Finished Product sample.`; `Production stage '{name}' is not a known stage.` [src: backend/MicroLIMS.Application/Workflows/ProductWorkflowEngine.cs:L50-L70, L96-L100].
  - Preparation diluent missing: `Diluent is required.` [src: backend/MicroLIMS.Application/Services/PreparationParameterValidator.cs:L25].
  - Preparation neutralizer missing: `Neutralizer is required.` [src: backend/MicroLIMS.Application/Services/PreparationParameterValidator.cs:L28].
  - Preparation filtration volumes missing: `Filtration technique requires both Filtration Volume and Washing Volume.` [src: backend/MicroLIMS.Application/Services/PreparationParameterValidator.cs:L33].
  - Preparation config item not found: `Item {itemId} not found.` [src: backend/MicroLIMS.Application/Services/ItemPreparationConfigurationService.cs:L55].
  - Preparation config nothing to approve: `Item {itemId} has no preparation configuration to approve.` [src: backend/MicroLIMS.Application/Services/ItemPreparationConfigurationService.cs:L88].
  - Preparation config already approved: `This preparation configuration is already approved.` [src: backend/MicroLIMS.Application/Services/ItemPreparationConfigurationService.cs:L91].

### MOD-SPEC-001 Specifications
- Client validations (`SpecificationParameterDialog`):
  - Assigned Test missing: `Assigned Test is required.` [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L541].
  - HPLC analyte missing: `Please select an analyte from the HPLC method.` [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L547].
  - GC solvent missing: `Please select a solvent from the GC method.` [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L547].
  - Residual solvent basis: `Basis must be ppm for residual solvents.` [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L552].
  - HPLC basis missing: `Please select a basis (Assay % or Amount per unit).` [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L557].
  - HPLC label claim missing: `Label claim must be greater than 0 for Amount per unit.` [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L563].
  - HPLC label claim unit missing: `Label claim unit is required for Amount per unit.` [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L567].
  - ICP element missing: `Please select an element from the ICP method.` [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L576].
  - ICP impurity basis: `Basis must be µg/g for elemental impurities.` [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L581].
  - ICP mineral assay basis missing: `Please select a basis (% of label claim or mg per unit).` [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L586].
  - ICP label claim missing: `Label claim must be greater than 0.` [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L591].
  - ICP label claim unit missing: `Label claim unit is required.` [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L595].
  - Dissolution Q range: `Lower limit Q (%) must be between 0 and 100 (exclusive of 0, inclusive of 100).` [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L612].
  - Dissolution label claim: `Label claim must be greater than zero for Dissolution Q specifications.` [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L617].
  - Dissolution duplicate: `Only one DissolutionQ specification is allowed per test.` [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L624].
  - Disintegration time limit: `Time limit (min) must be greater than zero for Disintegration Time specifications.` [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L632].
  - Disintegration duplicate: `Only one DisintegrationTime specification is allowed per test.` [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L639].
  - Weight variation dosage form: `Dosage form is required for Weight Variation specifications.` [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L646].
  - Weight variation duplicate: `Only one WeightVariation specification is allowed per test.` [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L653].
  - Save failure: `Failed to save specification.` (fallback) [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L789].
- Server validations (`SpecificationService`):
  - Ownership gate: `This specification belongs to {labName} - only its Section Head can change it.` [src: backend/MicroLIMS.Application/Services/SpecificationOwnership.cs:L33].
  - Test code not in master: `Test code '{testCode}' is not in the Test Master.` [src: backend/MicroLIMS.Application/Services/SpecificationOwnership.cs:L27].
  - Parameter name missing: `Parameter name is required.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L79].
  - Ppm basis restriction: `ppm is only used for residual-solvent specifications.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L82].
  - Dilution factor restriction: `Dilution factor is only allowed for Count-Tiered specifications.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L85].
  - Dosage form restriction: `Dosage form is only allowed for WeightVariation specifications.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L88].
  - Range limits missing: `Lower limit and upper limit are required for Range specifications.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L94].
  - Range bounds inverted: `Lower limit cannot be greater than upper limit.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L96].
  - NMT upper missing: `Upper limit is required for Not More Than specifications.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L101].
  - NLT lower missing: `Lower limit is required for Not Less Than specifications.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L106].
  - Target with tolerance missing: `Target and tolerance are required for Target With Tolerance specifications.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L111].
  - Tolerance mode missing: `Tolerance mode is required for Target With Tolerance specifications.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L113].
  - Tolerance <= 0: `Tolerance must be greater than zero.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L115].
  - Qualitative text missing: `Expected result text is required for Qualitative specifications.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L120].
  - Presence/absence state missing: `Expected state is required for Presence/Absence specifications.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L125].
  - Multi-stage no stages: `At least one stage is required for Multi-Stage specifications.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L130].
  - Multi-stage stage label missing: `Stage label is required for every stage.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L132].
  - Multi-stage criteria missing: `Acceptance criteria text is required for every stage.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L134].
  - Dissolution Q invalid: `Lower limit (Q) must be between 0 and 100 (exclusive of 0, inclusive of 100) for DissolutionQ specifications.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L139].
  - Dissolution label claim invalid: `Label claim must be greater than zero for DissolutionQ specifications.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L141].
  - Dissolution label claim unit: `Label claim unit must be "mg" for DissolutionQ specifications.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L143].
  - Disintegration time invalid: `Upper limit (time in minutes) must be greater than zero for DisintegrationTime specifications.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L148].
  - Disintegration unit: `Unit must be "min" for DisintegrationTime specifications.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L150].
  - Disintegration label claim forbidden: `Label claim is not allowed for DisintegrationTime specifications.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L152].
  - Disintegration label claim unit forbidden: `Label claim unit is not allowed for DisintegrationTime specifications.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L154].
  - Weight variation dosage form missing: `Dosage form is required for WeightVariation specifications.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L159].
  - Weight variation unit: `Unit must be "mg" for WeightVariation specifications.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L161].
  - Weight variation limits forbidden: `Limits are not allowed for WeightVariation specifications (configured on Test Master).` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L163].
  - Weight variation label claim forbidden: `Label claim is not allowed for WeightVariation specifications.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L165].
  - Test code missing: `Test code is required.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L183].
  - Test not assigned to item: `Test '{spec.TestCode}' is not assigned to item {spec.ItemId}.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L189].
  - Physicochemical area mismatch: `Test {spec.TestCode} is not used for finished products - it belongs to RM & PM.` / `Test {spec.TestCode} is not used for raw and packaging materials - it belongs to FP.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L198-L200].
  - Production stage role "Other": `Choose Bulk, In-Process, Finished or Stability, or leave the stage as all stages.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L207].
  - Stage spec on non-FP item: `Stage-specific specifications are only available for Finished Product items.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L210].
  - Duplicate analyte/basis: `A specification for this analyte and basis already exists.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L222].
  - Duplicate element/basis: `A specification for this element and basis already exists.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L232].
  - Duplicate parameter: `A specification for parameter '{spec.ParameterName}' already exists for test '{spec.TestCode}' at {stageText} on this item.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L240].
  - Dissolution test type mismatch: `DissolutionQ specifications are only allowed for Dissolution tests.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L248].
  - Dissolution single spec: `Only one specification is allowed for Dissolution test '{spec.TestCode}' on this item.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L254].
  - Non-dissolutionQ on dissolution test: `Only DissolutionQ specifications are allowed for Dissolution tests.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L258].
  - Disintegration test type mismatch: `DisintegrationTime specifications are only allowed for Disintegration tests.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L264].
  - Disintegration single spec: `Only one specification is allowed for Disintegration test '{spec.TestCode}' on this item.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L270].
  - Non-disintegration on disintegration test: `Only DisintegrationTime specifications are allowed for Disintegration tests.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L274].
  - Weight variation test type mismatch: `WeightVariation specifications are only allowed for WeightVariation tests.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L280].
  - Weight variation single spec: `Only one specification is allowed for WeightVariation test '{spec.TestCode}' on this item.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L286].
  - Non-weightVariation on weight variation test: `Only WeightVariation specifications are allowed for WeightVariation tests.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L290].
  - HPLC analyte missing: `Method analyte is required for HPLC method assay specifications.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L302].
  - HPLC analyte foreign method: `That analyte does not belong to the method of test '{spec.TestCode}'.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L306].
  - Residual solvent parameters: `Residual-solvent specifications use ppm with a not-more-than limit.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L313].
  - HPLC result basis invalid: `Result basis must be assay % (PercentLabelClaim) or amount per unit (MgPerUnit).` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L318].
  - HPLC amount per unit needs claim: `Amount per unit needs a label claim and its unit.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L321].
  - HPLC assay % label claim forbidden: `Label claim belongs on the amount-per-unit row.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L324].
  - HPLC limit type unsupported: `Limit type '{spec.LimitType}' is not supported for HPLC method assay specifications. Must be Range, NotMoreThan, NotLessThan, or TargetWithTolerance.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L333].
  - ICP element missing: `Method element is required for ICP method assay specifications.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L339].
  - ICP element foreign method: `That element does not belong to the method of test '{spec.TestCode}'.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L343].
  - ICP mineral assay basis invalid: `Result basis must be mg per unit or % of label claim for mineral assay.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L351].
  - ICP mineral assay needs claim: `Mineral assay specifications need a label claim and its unit.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L353].
  - ICP impurities basis: `Result basis must be µg/g (MgPerKg) for elemental impurities.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L358].
  - ICP impurities claim forbidden: `Label claim is not used for elemental impurities.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L360].
  - ICP impurities limit type: `Elemental impurity specifications use a not-more-than limit.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L362].
  - Titration basis required: `Result basis is required for titration specifications: % as is, % dried basis, % anhydrous basis, % of label claim or mg per unit.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L382].
  - Titration claim missing: `Label claim (mg per unit) must be greater than zero when the result basis is % of label claim or mg per unit.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L386].
  - Titration claim forbidden on as-is/dried/anhydrous: `Label claim is only used with the % of label claim and mg per unit result bases.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L388].
  - Titration claim category: `% of label claim and mg per unit are only available for Finished Product items.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L394].
  - Titration limit type unsupported: `Limit type '{spec.LimitType}' is not supported for titration specifications. Must be Range, NotMoreThan, NotLessThan, or TargetWithTolerance.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L402].
  - Retired elemental assay: `Elemental assay tests are retired; use an ICP method assay test.` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L407].

### MOD-RECVCFG-001 Receiving Configuration
- Client validations:
  - Load error: `Could not load {samplers / cause of testing / production stages}. Refresh to try again.` [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ReceivingConfigurationPage.tsx:L77; frontend/src/modules/laboratoryConfiguration/masterDataSimple/ProductionStagesCard.tsx:L57].
  - Add/rename error fallback: `Could not {rename / add} this entry. Check the name and try again.` [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ReceivingConfigurationPage.tsx:L117; frontend/src/modules/laboratoryConfiguration/masterDataSimple/ProductionStagesCard.tsx:L113].
  - Delete error fallback: `Could not delete this entry.` [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ReceivingConfigurationPage.tsx:L136; frontend/src/modules/laboratoryConfiguration/masterDataSimple/ProductionStagesCard.tsx:L133].
- Server validations:
  - Cause of testing duplicate: `Cause of Testing "{name}" already exists.` [src: backend/MicroLIMS.Application/Services/MasterData/ReferenceListMasterDataService.cs:L31, L46].
  - Cause of testing not found: `Cause of Testing {id} not found.` [src: backend/MicroLIMS.Application/Services/MasterData/ReferenceListMasterDataService.cs:L42, L59].
  - Cause of testing in use: `Cannot delete '{entity.Name}' - it is referenced by {sampleCount} sample(s).` [src: backend/MicroLIMS.Application/Services/MasterData/ReferenceListMasterDataService.cs:L63].
  - Sampler duplicate: `Sampler "{name}" already exists.` [src: backend/MicroLIMS.Application/Services/MasterData/ReferenceListMasterDataService.cs:L76, L91].
  - Sampler not found: `Sampler {id} not found.` [src: backend/MicroLIMS.Application/Services/MasterData/ReferenceListMasterDataService.cs:L87, L101].
  - Production stage name missing: `Name is required.` [src: backend/MicroLIMS.Application/Services/MasterData/ReferenceListMasterDataService.cs:L114, L132].
  - Production stage duplicate: `Production Stage "{request.Name}" already exists.` [src: backend/MicroLIMS.Application/Services/MasterData/ReferenceListMasterDataService.cs:L117, L135].
  - Production stage not found: `Production Stage {id} not found.` [src: backend/MicroLIMS.Application/Services/MasterData/ReferenceListMasterDataService.cs:L128, L146].

### MOD-WORKBOOK-001 Master Data Workbook
- Client validations:
  - Check failure fallback: `The file could not be checked.` [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L31].
  - Rejected load message: `Nothing was loaded: the workbook has problems to fix.` [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L43].
- Server validations:
  - Empty file: `The file is empty.` [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L272].
  - File larger than 10 MB: `The file is larger than 10 MB. Split it into several workbooks.` [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L274].
  - File name too long: `The file name is longer than 260 characters. Rename the file and try again.` [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L130].
  - Comment too long: `The comment is longer than 2000 characters. Shorten it and sign again.` [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L266].
  - Import not found: `Master data import {id} not found.` [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L196, L239].
  - Already verified: `This load has already been verified.` [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L198].
  - Self-verification blocked (SoD): `The person who loaded the workbook cannot verify it. Ask a second person.` [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L200].
  - Verifier section mismatch: `This load added material master entries to lab section(s) you are not a member of: {outside}. Ask a Section Head of that section to verify it.` [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L212].
  - Transaction requirement: `The workbook load must run inside a transaction.` [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookRunner.cs:L28].
  - Unknown sheet: `Unknown sheet '{unknown.Name}'. Use the sheets of the downloaded workbook.` [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookRunner.cs:L34].
  - Too many new rows: `The workbook has {newRows} new rows; the limit is 5000. Split it into several uploads.` [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookRunner.cs:L55].
  - Missing column: `Missing column(s): {missing}.` [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookRunner.cs:L71].
  - Duplicate row on sheet: `The same record appears more than once on this sheet.` [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookRunner.cs:L82].
  - Add-only conflict: `Already in MicroLIMS with different values in: {diff}. The workbook only adds records - change it in its screen.` [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookRunner.cs:L91].

### MOD-TESTMASTER-001 Test Master (FP and RM & PM)
- Client validations (Add / Edit Test dialog; shown as a red alert at the top of the dialog) [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/TestMasterPage.tsx:L242-L440]:
  - `Both Code and Display Name are required.`; `Laboratory section is required.`
  - `HPLC method is required for HPLC method assay tests.`; `ICP method is required for ICP method assay tests.`
  - `Replicate count must be between 1 and 30 for Measurement tests.`; `Evaluation basis is required for Measurement tests.`; `Replicate count must be between 1 and 30 for Gravimetric tests.`; `Equation type must be GravimetricLoss or GravimetricResidue for Gravimetric tests.`
  - Dissolution, disintegration and weight-variation messages are the same texts as the server messages below (the dialog repeats the checks).
  - Save failure fallback: `Could not add this test.` / `Could not update this test.`; success: `Test "{code}" added to the Test Master.` [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/TestMasterPage.tsx:L537, L541-L545].
  - Stage replicate dialog: `Production stage role is required.`; `Sample replicates is required.`; `Sample replicates must be an integer greater than or equal to 1.`; failures `Could not load stage replicates.`, `Could not save stage replicate configuration.`, `Could not delete stage replicate configuration.` [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/testMaster/TestStageReplicatesSection.tsx:L32, L63-L74, L95, L111].
  - Titration reason (client): `A reason for change is required when titration settings change (5 to 500 characters).` [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/TitrationChangeReasonField.tsx:L66-L72].
- Server validations (`TestDefinitionMasterDataService` & `TestStageReplicateMasterDataService`):
  - Duplicate test code: `Test code "{request.Code}" already exists in the Test Master.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L41, L388].
  - Retired elemental assay: `Elemental assay tests are retired; use an ICP method assay test.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L44, L385].
  - Disintegration SST forbidden: `Disintegration tests must not require system suitability.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L54, L160, L414, L520].
  - Weight variation SST forbidden: `Weight variation tests must not require system suitability.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L57, L189, L417, L549].
  - Method abbreviation missing: `Method abbreviation is required when system suitability is enabled.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L62, L422].
  - Method abbreviation regex: `Method abbreviation must be 1-20 uppercase alphanumeric characters or hyphens.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L65, L70, L425, L430].
  - Measurement workflow mismatch: `Workflow type must be Measurement when equation type is Measurement.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L76, L436].
  - Measurement equation mismatch: `Equation type must be Measurement when workflow type is Measurement.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L87, L447].
  - Measurement replicate count: `Replicate count must be between 1 and 30 when equation type is Measurement.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L79, L439].
  - Measurement evaluation basis: `Evaluation basis is required when equation type is Measurement.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L82, L442].
  - Condition fields max length: `Condition fields cannot exceed 500 characters.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L91, L411].
  - Gravimetric workflow mismatch: `Workflow type must be Gravimetric when equation type is {request.EquationType}.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L96, L453].
  - Gravimetric equation mismatch: `Equation type must be GravimetricLoss or GravimetricResidue when workflow type is Gravimetric.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L104, L461].
  - Gravimetric replicate count: `Replicate count must be between 1 and 30 when equation type is {request.EquationType}.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L99, L107, L456, L464].
  - Qualitative workflow mismatch: `Workflow type must be Qualitative when equation type is Qualitative.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L113, L470].
  - Qualitative equation mismatch: `Equation type must be Qualitative when workflow type is Qualitative.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L118, L475].
  - Dissolution workflow mismatch: `Workflow type must be Dissolution when equation type is Dissolution.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L124, L481].
  - Dissolution equation mismatch: `Equation type must be Dissolution when workflow type is Dissolution.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L129, L486].
  - Dissolution SST mandatory: `Dissolution tests must require system suitability: the standard comes from the HPLC run the sample is assigned to.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L135, L492].
  - Dissolution stage offsets negative: `Dissolution stage offsets must be greater than or equal to zero.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L143, L503].
  - Dissolution stage offsets missing: `Dissolution stage offsets are required for Dissolution tests.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L500].
  - Disintegration workflow mismatch: `Workflow type must be Disintegration when equation type is Disintegration.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L149, L509].
  - Disintegration equation mismatch: `Equation type must be Disintegration when workflow type is Disintegration.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L154, L514].
  - Disintegration stage units < 1: `Disintegration stage units must be greater than or equal to 1.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L168, L528].
  - Disintegration max stage 1 failures: `Disintegration maximum Stage 1 failures must be between 0 and {s1 - 1}.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L170, L530].
  - Disintegration min pass total: `Disintegration minimum pass total must be between 1 and {s1 + s2}.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L172, L532].
  - Weight variation workflow mismatch: `Workflow type must be WeightVariation when equation type is WeightVariation.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L178, L538].
  - Weight variation equation mismatch: `Equation type must be WeightVariation when workflow type is WeightVariation.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L183, L543].
  - Weight variation units < 1: `Weight variation unit count and extra units must be greater than or equal to 1.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L207, L567].
  - Weight variation max outside negative: `Weight variation maximum outside counts must be greater than or equal to 0.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L209, L569].
  - Weight variation tablet band limits <= 0: `Weight variation tablet band weight limits must be greater than zero.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L211, L571].
  - Weight variation percentages <= 0: `Weight variation percentages must be greater than zero.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L213, L573].
  - Weight variation tablet bands inverted: `Weight variation Tablet Band 1 Max Mg must be less than Band 2 Max Mg.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L215, L575].
  - Weight variation capsule percentages inverted: `Weight variation capsule inner percentage must be less than outer percentage.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L217, L577].
  - Weight variation capsule S1 retest criteria: `Weight variation capsule Stage 1 max outside must be less than Stage 1 max for retest, which must be less than or equal to unit count.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L219, L579].
  - Weight variation capsule S2 max outside: `Weight variation capsule Stage 2 max outside must be less than total units ({unitCount + capS2Extra}).` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L221, L581].
  - Retired HPLC types: `HPLC Assay, HPLC Multi-Analyte and Standard-Comparison are retired; use HPLC method assay.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L227, L587].
  - ICP workflow mismatch: `Workflow type must be IcpMethodAssay when equation type is IcpMethodAssay.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L234, L595].
  - ICP equation mismatch: `Equation type must be IcpMethodAssay when workflow type is IcpMethodAssay.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L238, L599].
  - ICP SST forbidden: `ICP method assay tests use the run calibration, not system suitability.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L244, L605].
  - ICP method missing: `ICP method is required for ICP method assay tests.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L247, L608].
  - ICP method not found: `ICP method not found.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L250, L611].
  - ICP method foreign lab: `The ICP method belongs to another laboratory.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L252, L613].
  - ICP method inactive: `The ICP method is inactive.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L254, L615].
  - ICP method has specs attached: `This test has specifications linked to elements of its current ICP method; remove them before changing the method.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L618].
  - ICP method allowed only on ICP: `ICP method is only allowed for ICP method assay tests.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L258, L622].
  - SST test type restriction: `Only HPLC method assay and dissolution tests can require system suitability.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L262, L626].
  - HPLC workflow mismatch: `Workflow type must be HplcMethodAssay when equation type is HplcMethodAssay.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L269, L633].
  - HPLC equation mismatch: `Equation type must be HplcMethodAssay when workflow type is HplcMethodAssay.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L274, L638].
  - HPLC SST mandatory: `HPLC method assay tests must require system suitability.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L280, L645].
  - HPLC method missing: `HPLC method is required for HPLC method assay tests.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L283, L648].
  - HPLC method not found: `HPLC method not found.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L286, L651, L664].
  - HPLC method foreign lab: `The HPLC method belongs to another laboratory.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L288, L653, L666].
  - HPLC method inactive: `The HPLC method is inactive.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L290, L659, L668].
  - Dissolution technique HPLC: `Dissolution needs an HPLC method.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L302].
  - HPLC method allowed only on HPLC/Dissolution: `HPLC method is only allowed for HPLC method assay and dissolution tests.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L306, L672].
  - Test not found: `Test {id} not found.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L379, L799, L808; backend/MicroLIMS.Application/Services/MasterData/TestStageReplicateMasterDataService.cs:L29, L42, L69, L93].
  - Foreign section test: `This test belongs to a laboratory section you are not assigned to.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L394; backend/MicroLIMS.Application/Services/MasterData/TestStageReplicateMasterDataService.cs:L46, L73, L97].
  - Titration reason missing: `A reason for change is required when titration settings change.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L760].
  - Physchem area missing: `Choose the area of this test (FP, RM & PM or Both).` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L783].
  - Abandoned area has specs: `Items {items} still have specifications on this test for {lostArea} - remove them first.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L791].
  - Stage replicates < 1: `Sample replicates must be at least 1.` [src: backend/MicroLIMS.Application/Services/MasterData/TestStageReplicateMasterDataService.cs:L49, L82].
  - Duplicate stage replicate role: `Stage replicate configuration for role {request.Role} already exists for this test definition.` [src: backend/MicroLIMS.Application/Services/MasterData/TestStageReplicateMasterDataService.cs:L52].
  - Stage replicate not found: `Stage replicate {replicateId} not found for test {id}.` [src: backend/MicroLIMS.Application/Services/MasterData/TestStageReplicateMasterDataService.cs:L76, L100].
  - Resolver stage not reconciled: `Sample has no reconciled production stage - it cannot be resolved to a stage role. Contact a Section Head to set its stage.` [src: backend/MicroLIMS.Application/Services/StageReplicateResolver.cs:L70, L83].
  - Resolver not configured: `No replicate configuration is set for stage role {stageRole} on this test - configure it in Test Master.` [src: backend/MicroLIMS.Application/Services/StageReplicateResolver.cs:L96].

## C8 Reports, KPI, Document Control

<!-- from C8-reports-dc.md -->

### MOD-REPORTS-001 Reports
- Server validations (ReportingController / ReportingQueryService):
  - Export row cap: `"This filter matches {result.TotalCount} rows, which exceeds the 10,000-row export limit. Narrow your filters (e.g. a shorter date range) and try again."` (for ResultRecords) [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L164-L165].
  - Media GPT export row cap: `"This filter matches {result.TotalCount} challenge rows, which exceeds the 10,000-row export limit. Narrow your filters (e.g. a shorter date range) and try again."` [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L252-L253].
  - Reference strain export row cap: `"This filter matches {result.TotalCount} rows, which exceeds the 10,000-row export limit. Narrow your filters and try again."` [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L334-L335].
  - Missing trend testCode: `"testCode is required."` [src: backend/MicroLIMS.Application/Services/ReportingQueryService.cs:L238].
  - Missing trend subjectName: `"subjectName is required."` [src: backend/MicroLIMS.Application/Services/ReportingQueryService.cs:L239].
  - Test code not in master: `"Test code \"{testCode}\" is not configured in Test Master."` [src: backend/MicroLIMS.Application/Services/ReportingQueryService.cs:L242, L442].
  - Qualitative trending rejection: `"Trending is only available for numeric results. {testCode} produces qualitative results."` [src: backend/MicroLIMS.Application/Services/ReportingQueryService.cs:L247].
  - Record not found: `"ResultRecord {id} not found."`, `"Media lot {id} not found."`, `"Cryovial batch {id} not found."` [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L93, L212, L302].
- Client validations:
  - Export failure: `"Export failed."` or parsed backend error message [src: frontend/src/modules/reports/services/ReportingService.ts:L97-L99; frontend/src/modules/reports/components/ReportResultsTable.tsx:L158].
  - Filter options load failure: `"The filter options could not be loaded, so the filter lists are empty. Reload the page to try again."` [src: frontend/src/modules/reports/RecordSearchTab.tsx:L87].
  - Trend image capture failure: `"No chart is currently rendered to capture."`, `"Could not capture the chart image for export."` [src: frontend/src/modules/reports/components/TrendingTab.tsx:L180-L185].

### MOD-REPORTS-002 Reporting backfill
- Server validations (ReportingAdminController / ResultProjectionService):
  - Sample not found: `"Sample {sampleId} not found."` [src: backend/MicroLIMS.Application/Services/ResultProjectionService.cs:L390].
  - Row-level projection errors logged and appended to error list: `"CountTestReading {id}: {message}"`, `"TestOrder {id} (pathogen): {message}"`, `"SampleLocation {id}: {message}"`, `"ParameterResult {id}: {message}"` [src: backend/MicroLIMS.Application/Services/ResultProjectionService.cs:L434, L459, L477, L495].

### MOD-KPI-001 KPI and Workflow Profiles
- Server validations (KpiDashboardService / WorkflowProfileService / AnalystWorkloadService):
  - Date range validation: `"A start date and an end date are required."` [src: backend/MicroLIMS.Application/Services/Kpi/KpiDashboardService.cs:L96].
  - Start date after end date: `"The start date must not be after the end date."` [src: backend/MicroLIMS.Application/Services/Kpi/KpiDashboardService.cs:L98]. The workload endpoints word it `"The end date must not be before the start date."` [src: backend/MicroLIMS.Application/Services/Kpi/AnalystWorkloadService.cs:L48].
  - Unknown KPI key: `"Unknown KPI '{key}'."` [src: backend/MicroLIMS.Application/Services/Kpi/KpiDashboardService.cs:L62].
  - D8 own-figures restriction: `"You can only view your own analyst figures."` (dashboard and records) [src: backend/MicroLIMS.Application/Services/Kpi/KpiDashboardService.cs:L107]; `"You may only view your own workload figures."` (workload) [src: backend/MicroLIMS.Application/Services/Kpi/AnalystWorkloadService.cs:L310].
  - Signature failures (shared signature service): `"Password verification failed. The signature was not applied."` and `"Too many failed signature attempts. Wait 15 minutes, then try again. The signature was not applied."` [src: documentation/knowledge/_raw/verified-roles-permissions.md:L142-L143].
  - Workflow profile version not found: `"Workflow profile version {id} was not found."` [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L101].
  - Double signing rejection: `"This version is already Effective and cannot be signed again."` [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L103].
  - Missing reason for change: `"A reason for the change is required."` [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L142].
  - Reason too long: `"The reason must be 500 characters or fewer."` [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L145].
  - Neither units nor target entered: `"Enter work units, a target, or both."` [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L148].
  - Non-positive value: `"{label} must be greater than 0."` [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L271].
  - Exceeds maximum: `"{label} must not exceed {max}."` (Work units max 999999.99, Target analyst days max 9999.99) [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L29-L30, L272].
  - Decimal places limit: `"{label} allows at most 2 decimal places."` [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L274].
  - Test code not in master: `"Test '{code}' does not exist in the test master."` [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L157].
  - Retired workflow type: `"Workflow type {type} is retired and cannot carry a profile."` [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L162].
  - Past effective date rejection: `"The effective date cannot be in the past ({effectiveFrom:yyyy-MM-dd}). Only the first version of a profile may be back-dated; add this version with an effective date of {today:yyyy-MM-dd} or later."` [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L289-L290].
- Client validations:
  - Filter bar date check (browser): `"Choose both dates."` and `"The start date must not be after the end date."` [src: frontend/src/modules/kpi/utils/kpiFilters.ts:L19-L22].
  - Calculation failure alert: `"This figure could not be calculated. The error has been logged."` [src: backend/MicroLIMS.Application/Services/Kpi/KpiDashboardService.cs:L13, L51].
  - Date format check: regex `/^\d{4}-\d{2}-\d{2}$/` [src: frontend/src/modules/kpi/components/AddProfileVersionDialog.tsx:L48].
  - Filter options load failure: `"The category, location and test lists could not be loaded. Reload the page to try again."` [src: frontend/src/modules/kpi/components/KpiFilters.tsx:L98].

### MOD-KPI-002 Analyst availability and KPI section settings
- Server validations (KpiSettingsService / AnalystAvailabilityService):
  - Settings reason required: `"A reason for the change is required."` [src: backend/MicroLIMS.Application/Services/Kpi/KpiSettingsService.cs:L45].
  - Settings reason length: `"The reason must be 500 characters or fewer."` [src: backend/MicroLIMS.Application/Services/Kpi/KpiSettingsService.cs:L46].
  - Hours per day range: `"Hours per analyst day must be greater than 0 and at most 24, with at most two decimals."` [src: backend/MicroLIMS.Application/Services/Kpi/KpiSettingsService.cs:L48].
  - Working weekdays mask: `"Select at least one working weekday."` [src: backend/MicroLIMS.Application/Services/Kpi/KpiSettingsService.cs:L50].
  - Load horizon range: `"The load horizon must be between 1 and 30 working days."` [src: backend/MicroLIMS.Application/Services/Kpi/KpiSettingsService.cs:L52].
  - Section not found: `"Laboratory section not found."` [src: backend/MicroLIMS.Application/Services/Kpi/KpiSettingsService.cs:L90].
  - Section unauthorized: `"You do not have access to this laboratory section."` [src: backend/MicroLIMS.Application/Services/Kpi/KpiSettingsService.cs:L93].
  - Availability reason required: `"A reason is required."` [src: backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs:L122].
  - Availability dates reversed: `"The end date must not be before the start date."` [src: backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs:L124].
  - Fraction available range: `"Fraction available must be between 0 and 1 with at most two decimals."` [src: backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs:L126].
  - Analyst not found: `"The analyst does not exist."` [src: backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs:L133].
  - Analyst outside section: `"You may only record availability for analysts in your laboratory sections."` [src: backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs:L136].
  - Overlapping period rejection: `"This period overlaps an existing availability entry for the analyst. Correct that entry instead."` [src: backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs:L142].
  - Reassigning entry to another analyst: `"An entry cannot be moved to another analyst; correct the dates or enter a new entry."` [src: backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs:L95].
  - Viewing other analyst's availability without team permission: `"You may only view your own availability."` [src: backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs:L38].
  - Target analyst outside section: `"That analyst is outside your laboratory sections."` [src: backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs:L42].

### MOD-DC-001 Document Control: library and document lifecycle
- Server validations (DocumentMasterService / DocumentRevisionService / DocumentFileService / DocumentReviewService / DocumentApprovalService / PeriodicReviewService):
  - Registration missing code: `"Company Document Code is required."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L45].
  - Registration missing title: `"Document Title is required."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L48].
  - Registration missing type: `"A valid Document Type is required."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L51].
  - Registration missing department: `"A valid Department is required."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L54].
  - Registration missing section: `"A valid Section is required."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L57].
  - Registration missing owner: `"A valid Document Owner is required."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L60].
  - Section department mismatch: `"Section '{section.Name}' does not belong to department '{dept.Name}'."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L81].
  - Duplicate code: `"A document with Company Document Code '{trimmedCode}' already exists and is active."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L93].
  - Registration unauthorized: `"Only the Document Controller or a System Administrator can register a Document Master."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L41].
  - Void reason length: `"A mandatory void reason of at least 10 characters is required."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L521].
  - Voiding effective master: `"A Document Master that has held an effective revision cannot be voided. It must be retired through obsolescence."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L533].
  - Voiding already voided: `"Document Master is already voided."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L529].
  - Void unauthorized: `"Only the Document Controller can void a Document Master."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L518].
  - Cancel reason length: `"A mandatory cancellation reason of at least 10 characters is required."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L575].
  - Cancel non-draft: `"Only revisions in Draft state can be cancelled. Current status is {revision.RevisionStatus}."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L585].
  - Cancel unauthorized: `"You do not have permission to cancel this draft revision."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L572].
  - Revision reason length: `"Reason for Revision is mandatory and must contain at least 10 characters."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentRevisionService.cs:L89].
  - Revision summary length: `"Change Summary, if provided, must contain at least 10 characters."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentRevisionService.cs:L92].
  - Revision voided master: `"Cannot create a revision for a voided document."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentRevisionService.cs:L69].
  - Revision without effective: `"Cannot create a new revision from an effective document because this document has no effective revision."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentRevisionService.cs:L76].
  - Revision concurrent workflow: `"Cannot create a new revision while another revision is currently in draft or review/approval workflow."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentRevisionService.cs:L85].
  - Revision number duplicate: `"Revision number '{finalRevisionNumber}' already exists for this document."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentRevisionService.cs:L129].
  - Revision number override format: `"Custom revision number must be 1-20 alphanumeric characters, dots, hyphens, or underscores."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentRevisionService.cs:L117].
  - Revision number override unauthorized: `"Only Document Controllers are authorized to override the proposed revision number."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentRevisionService.cs:L111].
  - Empty file: `"File content cannot be empty."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs:L57].
  - File size limit: `"File size exceeds the maximum permitted limit of 50 MB."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs:L60].
  - File format PDF: `"Controlled document file must be a PDF (.pdf)."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs:L73].
  - File magic bytes PDF: `"Invalid file format. The uploaded file is not a valid PDF document."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs:L79].
  - File format Word: `"Source document file must be a Word document (.docx or .doc)."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs:L87].
  - File magic bytes Word: `"Invalid file format. The uploaded file is not a valid Word document."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs:L92].
  - File upload non-draft: `"Files can only be added or replaced on revisions in Draft state. Current status is {revision.RevisionStatus}."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs:L132].
  - File upload unauthorized: `"You do not have permission to upload or replace files for this revision."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs:L121].
  - Controlled copy policy: `"Controlled copy download is currently disabled by administrative policy."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs:L331].
  - Integrity hash mismatch: `"Document integrity check failed. The file may have been altered or corrupted. Delivery has been blocked."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs:L388].
  - Review without a source file: `"Cannot submit revision for technical review without an active editable Word source file (.docx or .doc)."` (only when no Word file and no controlled PDF exist) [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L70-L74].
  - Review not Draft: `"Cannot submit revision for technical review in status {status}. Status must be Draft."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L62-L63].
  - Review submit unauthorised: `"User is not authorized to submit this document revision for technical review."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L102-L104].
  - Review decision unauthorised: `"Only the assigned technical reviewer or document controller may complete this technical review."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L561-L563]; findings: `"Only the assigned technical reviewer or document controller may add review findings."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L292-L293].
  - Approval decision unauthorised: `"Only the assigned approver or Document Controller may execute an approval decision."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L413-L415]. Approval prerequisites: `"Cannot approve document revision: Prerequisites not met. Errors: {list}"` with items such as "An active Controlled PDF file is required." and "Technical review has not been completed." [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L534-L540, L688-L735].
  - Review missing impact assessment: `"A multi-category Revision Impact Assessment must be completed before submitting for technical review."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L83].
  - Review unaddressed originating findings: `"Cannot submit revision for review: {unaddressedOriginatingItems.Count} originating review finding(s) remain unaddressed."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L96].
  - Review active task exists: `"A technical review task is already active for this revision."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L120].
  - Review completion unresolved mandatory findings: `"Cannot complete technical review while {unresolvedMandatoryFindings.Count} mandatory review finding(s) remain unresolved."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L579].
  - Finding resolution ladder violation: `"A review finding can only be resolved once it has been verified by the reviewer. Current status is {finding.Status}; it must be ReviewerVerified (the author responds, the reviewer verifies, then the finding is resolved)."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L506-L508].
  - Approval task technical review incomplete: `"Cannot create an approval task: Revision has not completed technical review."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L81].
  - Approval task already pending: `"An active approval task (ID: {existingPending.Id}) is already pending for this revision."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L89].
  - Approver role eligibility: `"User '{approver.FullName}' with role '{approver.Role?.Name ?? "None"}' is not eligible to be assigned as an approver."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L111].
  - Segregation of Duties texts: see roles (MOD-DC-001).
  - Return justification length: `"A mandatory justification of at least 10 characters is required when returning a revision for correction."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L441].
  - Decline justification length: `"A mandatory justification of at least 10 characters is required when declining a revision."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L483].
  - Approval missing password: `"Password re-authentication is required to apply an electronic signature for approval (21 CFR Part 11)."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L530].
  - Approval duplicate signature: `"An electronic signature for approval has already been applied to revision {revision.RevisionNumber}."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L546].
  - Periodic review already completed: `"This periodic review task is already completed and cannot be altered."` [src: backend/MicroLIMS.Application/Services/DocumentControl/PeriodicReviewService.cs:L542].
  - Periodic review cancelled: `"Cannot complete a cancelled periodic review task."` [src: backend/MicroLIMS.Application/Services/DocumentControl/PeriodicReviewService.cs:L545].
  - Periodic review summary length: `"A comprehensive review summary of at least 10 characters is mandatory."` [src: backend/MicroLIMS.Application/Services/DocumentControl/PeriodicReviewService.cs:L566].
- Client validations:
  - Viewer integrity error: `"CRITICAL INTEGRITY FAILURE: The document cryptographic SHA-256 signature does not match the stored file content. Delivery has been blocked."` [src: frontend/src/modules/documentControl/components/ControlledPdfViewer.tsx:L104].
  - Signature missing password: `"Password re-authentication is required to apply electronic signature."` [src: frontend/src/modules/documentControl/components/ElectronicSignatureDialog.tsx:L56].
  - Approval return/decline length: `"A mandatory justification of at least 10 characters is required for return or decline."` [src: frontend/src/modules/documentControl/components/ApprovalWorkspaceDialog.tsx:L121].

### MOD-DC-002 Document Control: reading and training
- Server validations (DocumentAcknowledgementService):
  - Missing conscious checkbox: `"Explicit confirmation of the legal acknowledgement statement is required. Reading or viewing alone does not constitute legal acknowledgement."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs:L130].
  - User mismatch: `"You are not authorized to acknowledge an assignment assigned to another user."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs:L167].
  - Revision mismatch: `"Revision mismatch: Assignment is for Revision ID {assignment.DocumentRevisionId}, but Revision ID {request.DocumentRevisionId} was submitted."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs:L185].
  - Non-active document: `"Cannot acknowledge assignment because Document {master.CompanyDocumentCode} is in {master.RecordStatus} status."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs:L192].
  - Non-effective revision: `"Cannot acknowledge assignment because Revision {revision.RevisionNumber} is in {revision.RevisionStatus} state (must be Effective)."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs:L198].
  - Superseded incomplete terminal: `"Cannot acknowledge an assignment that has been terminally closed as SupersededIncomplete."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs:L243].
  - Cancelled terminal: `"Cannot acknowledge an assignment that has been terminally closed as Cancelled due to document obsolescence."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs:L249].
  - Invalid assignment state: `"Assignment is in an invalid state for acknowledgement: {assignment.Status}."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs:L258].
- Client validations:
  - Missing checkbox confirmation: `"You must explicitly verify and accept the legal acknowledgement statement."` [src: frontend/src/modules/documentControl/components/DocumentAcknowledgementDialog.tsx:L51].

### MOD-DC-003 Document Control: audit trail
- Server validations (DocumentAuditService):
  - Global query unauthorized: `"You do not have permission to query the global audit trail."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuditService.cs:L35].
  - Record query unauthorized: `"You do not have permission to view audit history for this document."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuditService.cs:L64].
  - Export unauthorized: `"You do not have permission to export the audit trail."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuditService.cs:L92].

### MOD-DC-004 Document Control: configuration
- Server validations (DocumentConfigurationService):
  - Non-admin access: `"Only System Administrators can manage module configuration."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs:L35].
  - Missing type code: `"Document Type Code is required."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs:L60].
  - Missing type name: `"Document Type Name is required."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs:L63].
  - Review cycle range: `"Default review cycle must be between 1 and 120 months."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs:L66].
  - Duplicate type code: `"Document Type Code '{trimmedCode}' already exists."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs:L70].
  - Missing dept code: `"Department Code is required."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs:L127].
  - Missing dept name: `"Department Name is required."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs:L130].
  - Duplicate dept code: `"Department Code '{trimmedCode}' already exists."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs:L137].
  - Missing section name: `"Section Name is required."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs:L226].
  - Missing prefix: `"Prefix is required."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs:L415].
  - Missing number format: `"Number Format is required (e.g. 0000000)."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs:L419-L420].

### MOD-DC-005 Document escalations (backend)
- Server validations (DocumentEscalationController / DocumentEscalationService): `"A resolution reason is mandatory to resolve an escalation record."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentEscalationService.cs:L365]; `"Escalation record {id} not found."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentEscalationService.cs:L376].
- Controller refusals (HTTP 403):
  - View summary unauthorized: `"Only Section Heads and System Administrators can view the overdue training summary."` [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentEscalationController.cs:L53].
  - Trigger evaluation unauthorized: `"Only Section Heads and System Administrators can manually trigger escalation processing."` [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentEscalationController.cs:L68].
  - Resolve escalation unauthorized: `"Only Section Heads and System Administrators can resolve escalation records."` [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentEscalationController.cs:L85].
