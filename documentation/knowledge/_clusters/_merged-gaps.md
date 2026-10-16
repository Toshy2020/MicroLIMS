# Gaps

## C1 Platform & administration

<!-- from C1-platform.md -->

### GAP-100 E-mail password reset cannot be completed in the application
- **Question:** How is a user supposed to use the reset e-mail sent by "Forgot password" or by an administrator's "Reset Password via Email"?
- **Affected:** MOD-AUTH-001 (SCR-AUTH-001/002), MOD-USERS-001 (SCR-USERS-001).
- **Facts:** The reset e-mail contains a raw token, not a link [src: backend/MicroLIMS.Application/Services/AuthenticationService.cs:L258-L266]. The confirm endpoint `POST /api/auth/password-reset/confirm` exists [src: backend/MicroLIMS.API/Controllers/AuthenticationController.cs:L71-L78] but no screen calls it (only the service method is defined) [src: frontend/src/modules/authentication/services/authenticationService.ts:L23]. The sign-in page says "a reset link has been generated" [src: frontend/src/pages/Login.tsx:L62]. The admin dialog says "send it via email... secure reset token link" [src: frontend/src/modules/users/UsersPage.tsx:L596-L598] and the API reply says instructions were sent even when no e-mail is on file [src: backend/MicroLIMS.API/Controllers/UserController.cs:L118].
- **Why it matters:** The guide cannot describe a working e-mail reset path. Users without a working e-mail use administrator recovery instead.
- **Needs confirmation:** Whether a reset page is planned, and whether the e-mail should carry a link.
- **Who or what:** Product owner; developer.

### GAP-101 Audit Action filter does not offer platform action names
- **Question:** Should the Audit Trail "Action" filter offer the custom actions (USER_*, ROLE_PERMISSIONS_UPDATED, ADMIN_PASSWORD_RECOVERY_*, FORCE_PASSWORD_CHANGE_SET)?
- **Affected:** MOD-AUDIT-001 (SCR-AUDIT-001).
- **Facts:** The dropdown offers All Actions, Create, Update, Delete only [src: frontend/src/modules/auditSearch/components/AuditFilterBar.tsx:L93-L103]. The backend filters by exact text [src: backend/MicroLIMS.Application/Services/AuditSearchService.cs:L70]. Platform rows use custom action text [src: UserService.cs:L152, L194, L287, L312, L334; RoleService.cs:L162; AdminPasswordRecoveryService.cs:L93-L241].
- **Why it matters:** Administrators cannot filter user-management or role changes from the UI except by free-form search in another filter.
- **Needs confirmation:** Intended filter list.
- **Who or what:** Product owner.

### GAP-102 "Overdue" has three definitions on the dashboards (verified in Phase 3)
- **Question:** Which definition of "overdue" is intended for each dashboard tile?
- **Affected:** MOD-DASH-001 (SCR-DASH-001, SCR-DASH-003, SCR-DASH-004).
- **Facts:** Administrator "Overdue (>24h)" counts open test orders on samples received more than 24 h ago [src: backend/MicroLIMS.Application/Services/DashboardService.cs:L153, L201-L205]. Reviewer "Overdue review" counts review entries aged >= 1440 min [DashboardService.cs:L851]. Section Head "Overdue" counts samples past the 7-day analyst-assignment SLA [DashboardService.cs:L557-L579; KpiService.cs:L19, L124-L129]. Section Head attention items use >= 24 h for review and approval queues [DashboardService.cs:L604, L613].
- **Why it matters:** The same word gives different counts on different pages; the guide must name the rule per tile.
- **Needs confirmation:** Intended SLA per tile.
- **Who or what:** Product owner; Section Head.

### GAP-103 DISCREPANCY: notification triggers in BusinessRules.md versus the code
- **Question:** Which statuses drive the Approval Waiting and Review Waiting notifications?
- **Affected:** MOD-NOTIF-001.
- **Facts:** CONFIRMED DISCREPANCY (Phase 3). Spec §12 says Approval Waiting = any `TestOrder.Status == Reviewed` and Review Waiting = any `TestOrder.Status == ResultEntered` [src: documents/DesignSpecification/BusinessRules.md:L222-L231]. Code counts section sign-off statuses UnderApproval and UnderReview per section [src: backend/MicroLIMS.Application/Services/DashboardNotificationService.cs:L172-L190]. The other triggers (Media Expiry, Incubation Ready) match the spec in outline.
- **Why it matters:** Users may see a notification that does not match the status they see on the sample.
- **Needs confirmation:** Which definition is current.
- **Who or what:** Developer; product owner.

### GAP-104 Error-notification e-mail recipient and EmailSent flag
- **Question:** Should error-severity notification e-mails go to the user's own e-mail address, and should the stored flag say "sent" when SMTP is not configured?
- **Affected:** MOD-NOTIF-001, MOD-AUTH-001.
- **Facts:** The e-mail address used is `{username}@microlims.local` [src: backend/MicroLIMS.Application/Services/DashboardNotificationService.cs:L241-L243]. The stored flag `EmailSent` is set to true before delivery [DashboardNotificationService.cs:L240]. The spec says the e-mail is a no-op without SMTP [src: documents/DesignSpecification/BusinessRules.md:L235-L236]. The user's own e-mail field is not used here.
- **Why it matters:** Alert e-mails may go nowhere and the log says they were sent.
- **Needs confirmation:** Intended recipient; whether the flag should reflect delivery.
- **Who or what:** Developer; system administrator.

### GAP-105 Reviewer "Due today" tile meaning
- **Question:** Does "Due today" mean samples submitted for review today, or samples due for review today?
- **Affected:** MOD-DASH-001 (SCR-DASH-003).
- **Facts:** Label "Due today", caption "submitted today" [src: frontend/src/modules/dashboard/ReviewerDashboardPage.tsx:L129-L135]. Count = queue entries with SubmittedForReviewAt on or after today's start [src: backend/MicroLIMS.Application/Services/DashboardService.cs:L852].
- **Why it matters:** The label suggests a deadline, the count is a submission count.
- **Needs confirmation:** Intended label and rule.
- **Who or what:** Product owner.

### GAP-106 Administrator "Total samples" / "Total tests" are all-time counts
- **Question:** Should these tiles be all-time counts or counts for a period?
- **Affected:** MOD-DASH-001 (SCR-DASH-001).
- **Facts:** The values come from `/dashboard/kpi-deltas`, which counts all samples and test orders in scope with no date filter [src: backend/MicroLIMS.Application/Services/DashboardService.cs:L503-L525; frontend/src/modules/dashboard/AdminDashboardPage.tsx:L70-L71]. The same endpoint also computes this-month and last-month deltas that the page does not show [DashboardService.cs:L509-L518].
- **Why it matters:** Users may read the tiles as today's workload.
- **Needs confirmation:** Intended meaning.
- **Who or what:** Product owner.

### GAP-107 Create role is two separate API calls
- **Question:** What should happen when the role is created but the permission save fails?
- **Affected:** MOD-ROLES-001 (SCR-ROLES-002).
- **Facts:** The wizard calls create, then replace permissions [src: frontend/src/modules/roles/CreateRolePage.tsx:L88-L89]. The create call copies the base system role's grants first [src: backend/MicroLIMS.Application/Services/RoleService.cs:L72-L86]. On failure of the second call the role remains with the base grants.
- **Why it matters:** A role can exist with more permissions than the administrator chose.
- **Needs confirmation:** Whether to make creation atomic or to add a retry.
- **Who or what:** Developer.

### GAP-108 Traceability coverage and the "General" fallback
- **Question:** Which entities should have a traceability chain beyond Sample, Media, Material, Cryovial, Equipment and Item?
- **Affected:** MOD-AUDIT-001; MOD-ARCHIVE-001 (no chain on archived copies).
- **Facts:** Other entities show a single "General" node [src: backend/MicroLIMS.Application/Services/AuditTraceabilityService.cs:L205-L209]. Archived records have no permission or scope check in the controller [src: backend/MicroLIMS.API/Controllers/ArchivedRecordsController.cs:L16-L50].
- **Why it matters:** Users cannot trace, for example, a discussion or a user change; archive access may be broader than the sample's laboratory.
- **Needs confirmation:** Intended trace scope; whether archive download needs the record's laboratory scope.
- **Who or what:** Product owner; QA.

### GAP-109 Partly read platform screens and services (RESOLVED in Phase 3: all five items read; see BR-DASH-007, BR-USERS-010, BR-USERS-014, BR-ERR-005 and the Attention Required rows under SCR-DASH-002)
- **Question:** n/a (work list).
- **Facts:** (Closed.) Items not read in the extraction pass: Analyst "Attention Required" panel rows [src: frontend/src/modules/dashboard/components/AttentionRequiredPanel.tsx]; `UserSectionsDialog` service calls [src: frontend/src/modules/users/UserSectionsDialog.tsx]; hard-delete success text [src: UsersPage.tsx]; `SampleWorkflowQueues.IsIncubationReadyToRead` rule [src: backend/MicroLIMS.Application/Services/SampleWorkflowQueues.cs]; ExceptionMiddleware `CaptureAsync` body [src: backend/MicroLIMS.API/Middleware/ExceptionMiddleware.cs].
- **Why it matters:** Guide text for these items cannot be written from this cluster alone.
- **Needs confirmation:** Complete reading in Phase 4 or a follow-up pass.
- **Who or what:** Documentation team.

### GAP-110 Business-rule rejections are recorded as error incidents
- **Question:** Should every rejected business action (HTTP 400) and every authorisation refusal (HTTP 403) create an error-monitoring entry?
- **Affected:** MOD-ERR-001 (SCR-ERR-001).
- **Facts:** Invalid-operation exceptions return HTTP 400 with the message and call capture with severity Warning [src: backend/MicroLIMS.API/Middleware/ExceptionMiddleware.cs:L105-L111]. Unauthorized exceptions return HTTP 403 and capture Warning [ExceptionMiddleware.cs:L116-L121].
- **Why it matters:** The Error Monitoring list may fill with routine user refusals, and the page says it is not part of the GxP record.
- **Needs confirmation:** Intended scope of Error Monitoring.
- **Who or what:** Developer; product owner.

### GAP-111 Automatic audit capture excludes nine entity types
- **Question:** Are the excluded entities (refresh tokens, notification read state, incidents, error logs, security events and the access logs) intended to be outside the audit trail?
- **Affected:** MOD-AUDIT-001, MOD-ERR-001, MOD-NOTIF-001, MOD-AUTH-001.
- **Facts:** The capture excludes AuditLog, AuditEventChange, MaterialDocumentAccessLog, EquipmentDocumentAccessLog, Incident, ErrorLog, RefreshToken, NotificationLog and SecurityAuditEvent, each with a comment [src: backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs:L305-L316]. Spec §11 says every tracked Add, Update and Remove is captured automatically and that AuditLog and WorkflowHistory are append-only [src: documents/DesignSpecification/BusinessRules.md:L206-L218]; the spec does not list the exclusions.
- **Why it matters:** Changes to these entities (for example an incident being resolved) leave no AuditLog row; sign-in events are kept in login history and the security events table only, not in AuditLog.
- **Needs confirmation:** Whether the exclusions are accepted and documented.
- **Who or what:** QA; product owner.

### GAP-112 Role-change audit row stores the new role id as the previous value
- **Question:** Is it intended that `USER_ROLE_CHANGED` records the new role id in its previous-value column?
- **Affected:** MOD-USERS-001 (SCR-USERS-001), MOD-AUDIT-001.
- **Facts:** The user's role id is overwritten before the previous value is serialised, so previous value = new role id with the old role name [src: backend/MicroLIMS.Application/Services/UserService.cs:L178, L195]. The new value holds the new role id, name and reason [UserService.cs:L196].
- **Why it matters:** The Audit Trail diff for a role change can look as if the id did not change although the name did.
- **Needs confirmation:** Whether this is a defect.
- **Who or what:** Developer; QA.

### GAP-113 Behaviour after a successful password change; notification push
- **Question:** What does the user see after "Change Password" succeeds, and what consumes the notification push?
- **Affected:** MOD-AUTH-001 (SCR-AUTH-004), MOD-NOTIF-001.
- **Facts:** A password change revokes every refresh token and the per-request check rejects an access token issued before the change [src: backend/MicroLIMS.Application/Services/AuthenticationService.cs:L325-L337; backend/MicroLIMS.API/Authorization/AccessTokenRevalidator.cs:L70-L72]. The page then reloads the profile (`/auth/me`) and shows "Password changed successfully. Returning to your profile..." [src: frontend/src/pages/ChangePassword.tsx:L47-L50]. Whether the user is signed out afterwards cannot be told from code alone. Each stored notification is also passed to INotificationService [src: backend/MicroLIMS.Application/Services/DashboardNotificationService.cs:L236]; the bell polls every 60 s instead [src: frontend/src/components/Header.tsx:L57] and no push consumer was traced.
- **Why it matters:** The guide cannot say whether a new sign-in is expected after a change, nor describe any pop-up notification.
- **Needs confirmation:** Run the change-password flow; identify the push consumer.
- **Who or what:** QA; developer.

## C2 Sample flow & micro testing

<!-- from C2-sample-testing.md -->

Existing work-list gaps covering this cluster: GAP-043 (receiving register columns, KPI and selection bar partially read: column headers and selection bar buttons still not extracted). GAP-044 (physicochemical panels: C6).

New questions (GAP-200 onward):

### GAP-200 Batch and control number required on server but not on client
- **Phase 3 update:** The New Sample grid marks Batch No. and Control No. with a red asterisk for Product / RM / PM, so the form signals "required" but does not check it (MultiSampleEntryGrid.tsx:218-228; validateRows has no check). Not a spec discrepancy. EM / Water / After Cleaning receipts have no server check on control number.
- **Question:** Should Batch No. and Control No. be required in New Sample, as the server requires?
- **Affected:** MOD-SAMPLE-001 (SCR-SAMPLE-002).
- **Facts:** Server: "Batch number is required." / "Control number is required." (ReceiveSampleValidator.cs:15-16). Client NewSampleDialog validates item, stage, department, previous product, machine, cause, sampled by, expiry order only (NewSampleDialog.tsx:170-225). MultiSampleEntryGrid.tsx:539, 554 renders the fields; required flag not seen.
- **Why it matters:** A user may complete the form and receive a server rejection with combined text.
- **Needs confirmation:** Whether the grid marks these fields required (not read), and intended rule.
- **Who or what:** Product owner / developer.

### GAP-201 Edit-details lock rule and wording
- **Phase 3 update (downgraded):** The text "Locked: testing has started for this sample" belongs only to the item / location select (server rule: item or location locks once any work exists, SampleCorrectionService.cs:369-389). Other details lock at submission for review, matching the dialog alert "This sample has been submitted for review, so its details are locked." No conflict remains; open item is only whether the register tooltip and dialog wording should be unified.
- **Question:** When are sample details locked: at testing start, or at submission for review?
- **Affected:** SCR-SAMPLE-003, SCR-TESTING-004.
- **Facts:** Server allows correction in Received or InTesting only (SampleCorrectionService.cs:66-67). Dialog lock text "Locked: testing has started for this sample" (EditSampleDetailsDialog.tsx:261) conflicts with server (testing has started = InTesting still correctable). Panel tooltip "Locked: the sample has been submitted for review" (SelectedSampleTestingPanel.tsx:155).
- **Why it matters:** Users may be told a record is locked while it is editable, or the reverse.
- **Needs confirmation:** Intended lock point. Dialog lock text.
- **Who or what:** Product owner.

### GAP-202 Single-stage count result without server-side minimum-duration check
- **Phase 3 update (confirmed):** Single-stage count read-out has no server-side time check. For a transfer step the server waits for the stage-2 window END ("This step's incubation period has not finished yet."), whereas batch paths wait only for the minimum; broth and selective-plating paths wait for the minimum. The rule differs by path.
- **Question:** Should `record-result` for a single-stage count step refuse before the window minimum?
- **Affected:** SCR-COUNT-001; MOD-COUNT-001.
- **Facts:** RecordResultCoreAsync enforces elapsed time only for transfer (stage 2) steps (IncubationStepRecorder.cs:258-270); batch and pathogen paths enforce it (lines 419, 469, 580; PathogenConfirmationRecorder.cs:329-338). UI disables Calculate until ready (TestWorkflowDialog.tsx:803).
- **Why it matters:** The GMP-style minimum incubation rule would then rely on the client alone for this path.
- **Needs confirmation:** Whether the server check is missing or intentionally omitted for PlateCount single-stage steps.
- **Who or what:** Developer and quality owner.

### GAP-203 Void allowed for approved and rejected samples
- **Question:** Should void be allowed once a sample is Approved, Rejected or Cancelled?
- **Affected:** SCR-SAMPLE-004, SCR-TESTING-004; C3 cross-ref.
- **Facts:** Void checks only "already voided" (SampleCorrectionService.cs:260-261); UI shows Void when status is not Voided (SelectedSampleTestingPanel.tsx:174-196). Void sets CertificateRemarks to "Voided: {reason}" (SampleCorrectionService.cs:271), which overwrites certificate remarks.
- **Why it matters:** An approved record could be struck, and certificate remarks replaced.
- **Needs confirmation:** Intended void states; intended use of CertificateRemarks.
- **Who or what:** Product owner / QA.

### GAP-204 Register label for RetestRequested, Cancelled and Voided
- **Question:** Should Retest Requested, Cancelled and Voided show distinct labels in the register?
- **Affected:** SCR-SAMPLE-001, SCR-TRACK-001 (cross-ref C3).
- **Facts:** Register maps all three to "Cancelled / Voided" (SampleRegisterTable.tsx:67-69); kanban shows "Retest Requested" (SampleKanbanView.tsx:6-14); test-card text "Voided: struck from the record" (frontend/src/modules/testingWorkspace/components/AssignedTestCard.tsx:46-52). UnderReview is "Pending Review" in register but "Under Review" in badge.
- **Why it matters:** Users may confuse retest with void.
- **Needs confirmation:** Label set.
- **Who or what:** Product owner.

### GAP-205 Pathogen logic: spec versus code
- **Question:** Which pathogen model is the intended one: the spec (TSB -> Observation -> Detection Media; Salmonella TSB -> RVS -> XLD+TSI) or the code (BrothEnrichment / SelectiveBroth / SelectivePlating / ConfirmatoryPlating / BiochemicalTest)?
- **Affected:** MOD-PATH-001; WF-PATH-001/002.
- **Facts:** BusinessRules.md §3 cites Application/Workflows/PathogenWorkflowEngine.cs; no such class in repo (git grep empty). Code: selective growth other than GrowthConforming ends test NotDetected at once (PathogenConfirmationRecorder.cs:370-383). Spec says the chain completes at one observation.
- **Why it matters:** Phase 4 pathogen workflow must describe actual behaviour; spec may mislead users.
- **Needs confirmation:** Intended model; whether spec should be retired.
- **Who or what:** Product owner / microbiology lead.

### GAP-206 Which agreement result governs a pathogen session
- **Phase 3 update (resolved, see GAP-223):** The server-side agreement rule is ConfirmationAgreementEvaluator (all conforming = Detected; all NoGrowth or all NonConforming = Not Detected; any other mix = Inconclusive), applied per location in the session workspace. The client rule differs for a NoGrowth + NonConforming mix.
- **Question:** Is the per-location agreement (Detected / Not Detected / Inconclusive) shown in the UI the final result, or is the server confirmatory result per run?
- **Affected:** SCR-PATH-002 (Panel B, Review).
- **Facts:** Client agreement rule (BatchConfirmatoryPlatingPanel.tsx:315-326). Server per-run ConfirmatoryResult AllConforming / Inconclusive (PathogenConfirmationRecorder.cs:581). Session completion requires all results present (PathogenSessionService.cs:1218-1222) but server-side agreement not located in this read.
- **Why it matters:** Final reported result must match the rule the lab signs off.
- **Needs confirmation:** Server agreement logic in PathogenSessionService or ResultMatrixBuilder (not fully read).
- **Who or what:** Developer.

### GAP-207 Quantitative versus qualitative matrix cells decided by name text
- **Question:** Should cell type come from configuration rather than test code text?
- **Affected:** SCR-PATH-002 (Primary Matrix).
- **Facts:** Quantitative if workflow type contains "count" or code contains "tamc" / "tymc" (PrimaryObservationMatrixPanel.tsx:77-79, 155-157, 525-527, 640-642). Confirmatory media default 2 for code containing "SALMONELLA" (PrimaryObservationMatrixPanel.tsx:524; PathogenSessionService.cs:877).
- **Why it matters:** A renamed test would change data entry type.
- **Needs confirmation:** Intended source of truth.
- **Who or what:** Developer / product owner.

### GAP-208 Hardcoded fallbacks that affect stored values
- **Phase 3 update (corrected):** The confirmatory end time sent by the client is overwritten by the server (start + largest medium maximum), so the 24 h client fallback does not reach the stored incubation in the single-test path (PathogenConfirmationRecorder.cs:474-478) and the session path (PathogenSessionService.cs:911-912). Client start times are accepted as sent, see GAP-224.
- **Question:** Are these fallbacks intended?
- **Affected:** SCR-PATH-001, SCR-PATH-002.
- **Facts:** End of confirmatory setup defaults to 24 h when step max is 0 (ConfirmatoryPlatingPanel.tsx:153-154, sent as incubationEndUtc); materialId falls back to lot id, else literal 1 (BatchConfirmatoryPlatingPanel.tsx:389); TSB default lot by name text ("TSB", "Tryptic", "Broth") (SharedTsbEnrichmentPanel.tsx:53-66); reset reason default "Section Head requested session reset" (PathogenSessionDialog.tsx:115); start times default to browser clock (BatchConfirmatoryPlatingPanel.tsx:194; SharedTsbEnrichmentPanel.tsx:79-83); "Standard Lot", "Standard Incubator", "Analyst" displayed when server summary absent (BrothStepPanel.tsx:118-155).
- **Why it matters:** Values could be recorded that the lab did not select; clock source differs from server.
- **Needs confirmation:** Which fallbacks are reachable and intended.
- **Who or what:** Developer.

### GAP-209 Non-numeric plate text other than TNTC / Uncountable
- **Question:** What should happen for free text such as "abc" in a plate reading?
- **Affected:** SCR-COUNT-001.
- **Facts:** Numeric branch parses every reading with decimal.Parse (IncubationStepRecorder.cs:877-879); only TNTC/Uncountable are special-cased (828-831). Client inputs are free text ("Colony count or TNTC") (TestWorkflowDialog.tsx:742-768).
- **Why it matters:** Possible unhandled error or silent acceptance.
- **Needs confirmation:** Server validation message (not located).
- **Who or what:** Developer.

### GAP-210 Session completion error text differs
- **Question:** Which message should users see when results are missing?
- **Affected:** SCR-PATH-002 (Review & Complete).
- **Facts:** Server: "Cannot complete testing session: {n} required Location × Test result(s) are missing (...)" (PathogenSessionService.cs:1221). Client literal: "Cannot complete session: {n} required results are still missing." (SessionReviewPanel.tsx:191).
- **Why it matters:** Wording mismatch; the client text may replace the server text.
- **Needs confirmation:** Whether client literal is shown in place of server text.
- **Who or what:** Developer.

### GAP-211 EM count rule: spec versus code
- **Question:** Is EM out-of-trend (Step 1 + Step 2 count against ActionLimit) still intended?
- **Affected:** MOD-COUNT-001, MOD-OBS-001 (EM).
- **Facts:** WorkflowDiagrams.md EM section and BusinessRules.md §5 describe Step1Count + Step2Count > ActionLimit = Out Of Trend. Code batch path: per-location average with Alert/Action/Spec (IncubationStepRecorder.cs:485-530). `IsOutOfTrend` exists on EM DTO / RoomMonitoring entity (EMResultDto.cs:10; RoomMonitoring.cs:15) but trend computation not located in the batch path.
- **Why it matters:** Stated EM rule in the spec would be wrong for users.
- **Needs confirmation:** Intended EM rule and where trend is computed.
- **Who or what:** Product owner / developer.

### GAP-212 Signature and ownership rules for EM, After Cleaning and Water preparation
- **Phase 3 update (bodies read):** EM / After Cleaning / Water preparation: no signature, no ownership check, no scope call; the preparing user becomes AssignedAnalystId on all created test orders (and so counts as having performed them for review / approval segregation). Receive and prepare carry no permission policy (GAP-227).
- **Question:** Should EM / AC / Water preparation be signed and subject to the assigned-analyst rule?
- **Affected:** SCR-PREP-001.
- **Facts:** Product/RM/PM preparation signs (SamplePreparationService.cs:184-188); analyst ownership check is in SamplePreparationService (BR-PREP-002). EM/AC/Water prepare endpoints: no signature in verified list; body and ownership not read (EMController, AfterCleaningController, WaterController not read).
- **Why it matters:** Signature and ownership points differ by category.
- **Needs confirmation:** Intended rule per category.
- **Who or what:** Product owner.

### GAP-213 Analyst assignment: reason and role rules
- **Phase 3 update:** Confirmed: reason optional in UI and server; no role check on the assignee (any active user); the assignee's laboratory is not checked.
- **Question:** Is the reason mandatory, and must the assignee hold the Analyst role?
- **Affected:** SCR-SAMPLE-006.
- **Facts:** Server reason parameter optional (SampleAssignmentService.cs:24-70, default null); only IsActive checked (line 37). UI label "Reason for Assignment / Reassignment (recorded in Audit Trail)" (AssignAnalystDialog.tsx:219); required flag not seen.
- **Why it matters:** Reassignments may occur without reason; non-analysts could be assigned.
- **Needs confirmation:** Intended rule.
- **Who or what:** Product owner.

### GAP-214 Item preparation configuration screen and approval not located
- **Phase 3 update:** Backend read: GET/PUT `/api/items/{id}/preparation-configuration`, POST `.../approve` and GET `/api/preparation-configurations/pending` need `Items.Manage`; behaviour in BR-PREP-008..010. Only the screen (labels, tile for pending approvals) is still unlocated.
- **Question:** Which screen shows preparation configuration and its approval queue?
- **Affected:** MOD-PREP-001 (WF-PREP-002).
- **Facts:** Endpoints: GET/PUT `/api/items/{id}/preparation-configuration`, POST `.../approve`, GET `/api/preparation-configurations/pending` (ItemPreparationConfigurationService.ts:39-48); PreparationConfigurationQueueController exists (not read). No SCR id.
- **Why it matters:** WF-PREP-002 cannot be documented.
- **Needs confirmation:** Screen location and fields.
- **Who or what:** Documentation team (code read).

### GAP-215 Tracking board data source not read
- **Phase 3 update (resolved):** SampleTrackingService read; see BR-TRACK-001..003 and the Tracking Board screen notes. Remaining question is GAP-229.
- **Question:** How is each lab's stage and "Overall" status derived?
- **Affected:** MOD-TRACK-001.
- **Facts:** Endpoint GET `/api/samples/tracking` (TrackingService.ts:49); SampleTrackingController and SampleTrackingService not read.
- **Why it matters:** Tracking statuses cannot be explained.
- **Needs confirmation:** Read backend.
- **Who or what:** Documentation team (code read).

### GAP-216 Biochemical and analyst decisions carry no signature
- **Question:** Should the analyst's "Conclude as Detected" and biochemical result be signed?
- **Affected:** SCR-PATH-001, SCR-PATH-002.
- **Facts:** Verified signature list excludes pathogen steps, biochemical and analyst decisions (verified-roles-permissions.md §signature, count note). Frontend: no SignatureDialog on these panels (raw §11.2).
- **Why it matters:** These decisions set the final Detected result.
- **Needs confirmation:** Intended signature point.
- **Who or what:** Quality owner.

### GAP-217 Session reset reason field versus default
- **Phase 3 update (corrected):** The server default when the reason is blank is "Analyst requested session workflow reset" (PathogenSessionService.cs:1303); the dialog default is "Section Head requested session reset". The reset also deletes data (GAP-221).
- **Question:** Is the reason mandatory, given the default string fallback?
- **Affected:** SCR-PATH-002 (Reset dialog).
- **Facts:** Confirm button disabled when reason is blank (PathogenSessionDialog.tsx:374-383), but code sends "Section Head requested session reset" when blank (PathogenSessionDialog.tsx:115). Server controller passes request.Reason (PathogenSessionController.cs:178).
- **Why it matters:** Default reason may appear in audit trail if the path is reached.
- **Needs confirmation:** Intended rule.
- **Who or what:** Developer.

### GAP-218 Count and pathogen signature points vs spec
- **Question:** Confirm that count results and pathogen steps are intentionally unsigned.
- **Affected:** MOD-COUNT-001, MOD-PATH-001, MOD-OBS-001.
- **Facts:** verified-roles-permissions.md: "Not found signed: microbiology plate-count entry, pathogen steps, biochemical confirmation decision..." with REQUIRES CONFIRMATION note there.
- **Why it matters:** Electronic-signature scope for micro results.
- **Needs confirmation:** Owner decision. (Do not infer compliance.)
- **Who or what:** Quality owner.

### GAP-219 Preparation ownership: UI exemption versus server rule
- **Question:** May a Section Head or System Administrator prepare a sample assigned to another analyst without reassigning it?
- **Affected:** SCR-PREP-001; WF-PREP-001.
- **Facts:** TestPreparationForm exempts roles SectionHead / SystemAdministrator from its own "assigned to someone else" disabling (TestPreparationForm.tsx:29-33), but SamplePreparationService refuses any user other than the assigned analyst, whatever the role (SamplePreparationService.cs:157-169). The refusal text says "unless reassigned by an authorized Section Head". Single-test execution endpoints have no assigned-analyst check at all (only grouped actions do).
- **Why it matters:** The guide must say whether a Section Head has to reassign first.
- **Needs confirmation:** Intended ownership rule for preparation and for result entry.
- **Who or what:** Product owner.

### GAP-220 Media release and expiry checks differ by entry path
- **Question:** Should every path that starts an incubation require a released, unexpired lot and an in-range incubator?
- **Affected:** WF-TESTING-001, WF-COUNT-001, WF-PATH-001, WF-PATH-002.
- **Facts:** Single-test select-media, selective plating and confirmatory setup check release / out of stock / rejected but not the lot's expiry date (IncubationStepRecorder.cs:49-51; PathogenConfirmationRecorder.cs:115-116). Grouped actions check release and expiry plus incubator set point and calibration (BatchSelectMediaRules.cs:19-47). The pathogen session paths (shared TSB, confirmatory setup) check expiry (TSB only) but never release status, although the screens label lots "GPT Passed & Released". Stage-2 transfer runs no incubator range check. No code sets IsReleasedForUse to false on expiry.
- **Why it matters:** An unreleased or expired lot may be accepted on some paths.
- **Needs confirmation:** Whether the UI lists only eligible lots and whether expiry is meant to be enforced server-side everywhere.
- **Who or what:** Developer and quality owner.

### GAP-221 Pathogen session reset deletes data
- **Question:** Is permanent deletion of all results, readings, observations and incubations on reset intended, and should it depend on sample status?
- **Affected:** WF-PATH-003.
- **Facts:** Reset hard-deletes records for every test order of the sample (including non-pathogen tests) and writes an audit-log line with the reason (PathogenSessionService.cs:1301-1399). Only Voided and Rejected samples are refused; for UnderApproval, Approved, RetestRequested or Cancelled the data is deleted and the sample status is left unchanged. The reason is optional with a server default text.
- **Why it matters:** Result history can disappear after review or approval.
- **Needs confirmation:** Intended statuses, and whether a reason and signature should be mandatory.
- **Who or what:** Quality owner.

### GAP-222 Pathogen session completion sets whole-sample status
- **Question:** Should completing a session mark all of the sample's test orders Ready and the sample UnderReview, even for other laboratories' tests?
- **Affected:** WF-PATH-002.
- **Facts:** CompleteSessionAsync loops over all test orders of the sample and assigns UnderReview directly, bypassing the section roll-up and the "all tests Ready" check used elsewhere (PathogenSessionService.cs:1239-1256).
- **Why it matters:** A sample that also has physicochemical or other pending tests could be pushed to review early.
- **Needs confirmation:** Behaviour for mixed samples.
- **Who or what:** Developer.

### GAP-223 Client and server agreement rules differ
- **Question:** Which result applies when one confirmatory medium shows no growth and another shows non-conforming growth?
- **Affected:** SCR-PATH-002 Panel B.
- **Facts:** Client: Not Detected (BatchConfirmatoryPlatingPanel.tsx:315-326). Server: Inconclusive (ConfirmationAgreementEvaluator.cs:29-45).
- **Why it matters:** The screen can show a result that changes after saving.
- **Needs confirmation:** Intended rule.
- **Who or what:** Microbiology lead.

### GAP-224 Analyst-supplied incubation start times
- **Question:** Are backdated or future start times for selective plating, shared TSB and confirmatory incubation intended?
- **Affected:** WF-PATH-001, WF-PATH-002.
- **Facts:** The request start time is stored as sent (start = supplied value, else now) and the minimum-duration check is measured from it (PathogenConfirmationRecorder.cs:273-290, 395-478; PathogenSessionService.cs:603, 906). The confirmatory step validates only end-before-start and an under-minimum declared window.
- **Why it matters:** The minimum incubation time can be satisfied by the entered start time.
- **Needs confirmation:** Whether the start time must be near the server time.
- **Who or what:** Quality owner.

### GAP-225 After Cleaning: spec versus code
- **Question:** Is the collective pathogen order plus one TAMC order per part still intended?
- **Affected:** WF-SAMPLE-008, WF-PREP-001.
- **Facts:** BusinessRules.md §6 and WorkflowDiagrams.md describe a collective pathogen test order (IsCollectivePathogenSample) and "TAMC:{partName}" orders. Code creates one test order per distinct test code across the selected parts with one location per part; neither the flag nor "TAMC:" is used in backend code.
- **Why it matters:** The spec would mislead users.
- **Needs confirmation:** Intended model.
- **Who or what:** Product owner.

### GAP-226 Skip Wait carries no reason or signature
- **Question:** Should Skip Wait require a reason and a signature?
- **Affected:** WF-TESTING-002.
- **Facts:** The endpoint records user and time only; it bypasses the minimum-duration and window-end checks for broth, selective plating, confirmatory, stage 1, stage 2 and batch paths (IncubationStepRecorder.cs:431-443).
- **Why it matters:** A bypass of an incubation control is not explained in the record.
- **Needs confirmation:** Intended control.
- **Who or what:** Quality owner.

### GAP-227 EM / Water / After Cleaning receive and prepare have no permission or lab-scope check
- **Question:** Should these endpoints require a permission and a lab-membership check like Product receipt?
- **Affected:** WF-SAMPLE-006..008.
- **Facts:** The controllers carry `[Authorize]` only; the authorization matrix lists all four roles (authorization-matrix.txt:1-2, 138-139, 522-523). Product receipt needs `Samples.ReceiveOwnLab` and the lab guard. UI gating for these categories was not read.
- **Why it matters:** Role rules differ by category.
- **Needs confirmation:** Intended rule.
- **Who or what:** Product owner.

### GAP-228 Count and preparation input validation not enforced on the server
- **Question:** Should negative or non-numeric readings, a dilution factor of zero or below, and a missing sample amount be refused?
- **Affected:** WF-COUNT-001, WF-PREP-001.
- **Facts:** The recorder parses readings with decimal.Parse and does not range-check readings or the dilution factor (IncubationStepRecorder.cs:873-879); the preparation validator does not check Amount (PreparationParameterValidator.cs:22-37); the manual form converts Amount with Number() without a required check.
- **Why it matters:** Invalid values may be stored or raise an unfriendly error.
- **Needs confirmation:** Intended validation.
- **Who or what:** Developer.

### GAP-229 Tracking Board omits unprepared EM / Water / After Cleaning samples
- **Question:** Should samples with no test orders yet appear on the Tracking Board?
- **Affected:** SCR-TRACK-001.
- **Facts:** The board lists samples with at least one test order (SampleTrackingService.cs:39); EM, Water and After Cleaning samples get orders only at preparation.
- **Why it matters:** Received-but-unprepared samples are invisible on the board.
- **Needs confirmation:** Intended scope.
- **Who or what:** Product owner.

---

## Not read / UNCLEAR (for coverage)
- (Phase 3) SampleTrackingService, EM / Water / After Cleaning controllers and engines, item preparation configuration service, SingleResultRecorder (qualitative) were read. Still not read: UI gating of EM / Water / After Cleaning receive, ResultMatrixBuilder, SessionStatusResolver, SharedTsbStateBuilder, GroupedActionRow internals, C3 services (review / approval).
- ResultMatrixBuilder, SessionStatusResolver, SharedTsbStateBuilder (only names seen).
- Frontend: SampleFilterBar sub-labels, AuditHistoryDialog, ReturnToAnalystDialog (C3); UI gating of EM / Water / After Cleaning receive.
- Backend: validators for physical/route fields beyond ReceiveSampleValidator, EMValidator, WaterValidator, ProductValidator.
- Texts now read: AddLaboratoryService, MediaNotInPermittedList, BiochemicalResultRequired. Still code-only: other WorkflowErrorCodes texts.
- Spec: BusinessRules §1-§6 and WorkflowDiagrams (product, pathogen, water, EM, After Cleaning) read. No spec found for receiving screens, preparation, count dilution rules or grouped actions.

## C3 Review, approval, reports out

<!-- from C3-review-approval.md -->

Existing gaps referenced, not duplicated: GAP-009 (return unsigned), GAP-010 (count and pathogen steps unsigned), GAP-017 (decision set), GAP-018 (review and approval design), GAP-043 (OOS void control and CoA body not read).

### GAP-300 Return to Analyst scope: service versus UI
- **Question:** Should Return to Analyst be offered for analysis (non-count) tests, as the service allows, or count tests only, as the UI offers?
- **Affected:** MOD-REVIEW-001, SCR-REVIEW-002, WF-REVIEW-002 (Phase 4).
- **Facts:** The service accepts CountTest and analysis workflows and refuses others [src: backend/MicroLIMS.Application/Services/SampleReviewService.cs:L188-L189]. The UI button is shown only for count tests [src: frontend/src/modules/testingWorkspace/sampleSummary/TestResultsSection.tsx:33-45, 109-129].
- **Why it matters:** The guide must state one rule. Analysis-test return is reachable only by a direct API call today.
- **Needs confirmation:** Intended scope of return to analyst.
- **Who or what:** QA; Section Head.

### GAP-301 Return to Analyst: separation of duties and sample-route check
- **Question:** Should the reviewer who performed a test be prevented from returning it, as they are prevented from reviewing it?
- **Affected:** MOD-REVIEW-001, WF-REVIEW-002 (Phase 4).
- **Facts:** The return path calls no SegregationOfDutiesGuard check [src: backend/MicroLIMS.Application/Services/SampleReviewService.cs:L157-L269]; the review path does [src: SampleReviewService.cs:L109-L113]. The route id is not compared with the test order's sample [src: backend/MicroLIMS.API/Controllers/SampleReviewController.cs:L44-L54].
- **Why it matters:** The guide must say whether a reviewer can return their own test.
- **Needs confirmation:** Intended rule for returns.
- **Who or what:** QA; Section Head.

### GAP-302 New Sample Required analysts: separation of duties and section scope
- **Question:** Should the two new-sample analysts be checked against everyone who tested the original sample (not only the analyst assigned to each selected test), and against the decider's section scope?
- **Affected:** MOD-APPROVAL-001, SCR-APPROVAL-001.
- **Facts:** The UI text says "neither may be whoever tested the original sample" [src: frontend/src/modules/testingWorkspace/sampleSummary/ApprovalSignaturesCard.tsx:410-457]. The service checks only AssignedAnalystId of selected tests [src: backend/MicroLIMS.Application/Services/SampleApprovalService.cs:L279-L283]. Other performers are checked by the guard elsewhere (DidUserPerformTestAsync) [src: SegregationOfDutiesGuard.cs:L25-L54]. The analysts only need Role.Type Analyst, active and unlocked; no section scope check [src: SampleApprovalService.cs:L272-L277].
- **Why it matters:** The UI promise is wider than the check.
- **Needs confirmation:** Intended rule for the two new analysts.
- **Who or what:** QA; Section Head.

### GAP-303 Rejection scope: whole sample or one section
- **Question:** Does Reject close the whole sample, or only the deciding section?
- **Affected:** MOD-APPROVAL-001, statuses of the sample.
- **Facts:** The service header says rejecting "rejects the whole sample" [src: backend/MicroLIMS.Application/Services/SampleApprovalService.cs:L12-L16]. The Reject branch says a rejection "no longer closes any other lab still open" [src: SampleApprovalService.cs:L360-L362]. The roll-up keeps the sample at the least advanced open status while another section is open [src: backend/MicroLIMS.Application/Services/SampleSectionRollup.cs:L73-L77].
- **Why it matters:** The status words users see for a rejected multi-lab sample depend on this.
- **Needs confirmation:** Intended behaviour; the header comment may be stale.
- **Who or what:** Developer; QA.

### GAP-304 Approval of out-of-specification results
- **Question:** Should the server block or require a reason when a section with out-of-specification results is approved?
- **Affected:** MOD-APPROVAL-001, SCR-APPROVAL-001.
- **Facts:** The UI warns that approval certifies out-of-spec results as conforming [src: frontend/src/modules/testingWorkspace/sampleSummary/ApprovalSignaturesCard.tsx:459-463]. The server approve path checks only system suitability and analysis entries [src: backend/MicroLIMS.Application/Services/SampleApprovalService.cs:L173-L242].
- **Why it matters:** The guide must state whether approval of OOS results is possible and what it certifies.
- **Needs confirmation:** Intended control.
- **Who or what:** QA; Section Head.

### GAP-305 OOS detection and group opening
- **Question:** Should an OOS group be opened automatically when a result is out of specification, or only when a Section Head chooses a retest?
- **Affected:** MOD-OOS-001, WF-OOS-001 (Phase 4).
- **Facts:** An OutOfSpecification result is recorded by the result classifier [src: backend/MicroLIMS.Application/Services/SpecLimitParser.cs:L209-L224]. An OOS group code is written only by the retest decision [src: SampleApprovalService.cs:L392-L393, L447-L448]. There is no OOS record entity [src: backend/MicroLIMS.Domain/Entities/OosInvestigationDocument.cs]. The OOSInvestigation decision is never written [src: backend/MicroLIMS.Application/Services/Kpi/OosRateKpi.cs:L5] (path corrected from the draft; the file comment says nothing writes the OOSInvestigation decision) (verified).
- **Why it matters:** An OOS that is not retested never appears in OOS Tracking.
- **Needs confirmation:** Intended trigger for an OOS group.
- **Who or what:** QA; laboratory owner.

### GAP-306 Certificate of Analysis: availability gate
- **Question:** Is it intended that the server returns certificate data for samples that are not yet approved or rejected?
- **Affected:** MOD-COA-001, SCR-COA-002.
- **Facts:** The page refuses non-final samples [src: frontend/src/modules/testingWorkspace/SampleCoaPage.tsx:363]. The server returns a summary for any status, using section scope for non-final samples [src: backend/MicroLIMS.Application/Services/SampleSummaryService.cs:L84-L90; backend/MicroLIMS.API/Controllers/SampleSummaryController.cs:L35-L40].
- **Why it matters:** The guide describes the page gate; the API gate is weaker.
- **Needs confirmation:** Intended API rule.
- **Who or what:** Developer; QA.

### GAP-307 Certificate signatory fallback without a signature
- **Question:** Should the CoA print a reviewer or approver name and time when no signature row exists?
- **Affected:** MOD-COA-001, SCR-COA-002.
- **Facts:** The signature block falls back to the stored name and time when no matching signature exists [src: frontend/src/modules/testingWorkspace/SampleCoaPage.tsx:126-147, 413-416, 553-554].
- **Why it matters:** A printed name may look signed when it is not.
- **Needs confirmation:** Intended printing rule.
- **Who or what:** QA.

### GAP-308 Certificate and signature meaning for New Sample Required
- **Question:** What should the certificate and the signature trail show for a New Sample Required decision?
- **Affected:** MOD-APPROVAL-001, MOD-COA-001.
- **Facts:** The signature meaning is Rejected [src: backend/MicroLIMS.Application/Services/SampleApprovalService.cs:L297]. The UI label is "New Sample Required: Close and request new sample" [src: frontend/src/modules/testingWorkspace/sampleSummary/summaryFormatting.ts:73-78]. The deciding section is set to RetestRequested; the origin sample reaches Approved or Rejected only through OOS propagation (BR-APPROVAL-021) [src: SampleApprovalService.cs:L377-L393, L541-L627].
- **Why it matters:** A retest-spawning decision is printed as a rejection.
- **Needs confirmation:** Intended meaning and printed wording.
- **Who or what:** QA; Section Head.

### GAP-309 DISCREPANCY: review and approval notifications versus BusinessRules.md
- **Question:** Are the approval and review notifications counted per sample or per test order?
- **Affected:** MOD-DASH-001 (C1), MOD-APPROVAL-001, MOD-REVIEW-001.
- **Facts:** BusinessRules.md section 12 says "Approval Waiting: any TestOrder.Status == Reviewed" and "Review Waiting: any TestOrder.Status == ResultEntered" [src: documents/DesignSpecification/BusinessRules.md:L230-L231]. The code counts samples in section state UnderApproval and UnderReview for Section Head/Admin and Reviewer/Section Head/Admin [src: backend/MicroLIMS.Application/Services/DashboardNotificationService.cs:L171-L189].
- **Why it matters:** Counts shown in the notification differ from the spec's unit.
- **Needs confirmation:** Intended unit.
- **Who or what:** QA.

### GAP-310 Word export of the sample summary
- **Question:** Is the Word export of the sample summary intended to be available to users?
- **Affected:** MOD-COA-001.
- **Facts:** Endpoint GET /api/samples/{id}/summary/word exists [src: backend/MicroLIMS.API/Controllers/SampleSummaryController.cs:L53-L60]. No frontend caller found [src: documentation/knowledge/_raw/frontend-testing-workspace.md:L468, L603, L685].
- **Why it matters:** The guide must not list an export that users cannot reach.
- **Needs confirmation:** Keep or retire.
- **Who or what:** Developer; QA.

### GAP-311 Which PDF is the record
- **Question:** Is the archived PDF (frozen at the final decision) the controlled copy, and what is the on-demand PDF export for?
- **Affected:** MOD-COA-001.
- **Facts:** The archive is written at FinalizeIfClosedAsync and frozen with a SHA-256 [src: backend/MicroLIMS.Application/Services/SampleApprovalService.cs:L528-L539; RecordArchiveService.cs:L12-L20]. The "Export PDF" button renders the summary on demand [src: frontend/src/modules/testingWorkspace/SampleSummaryDialog.tsx:433-442; SampleSummaryController.cs:L44-L51].
- **Why it matters:** Two PDFs for one sample can differ after later changes.
- **Needs confirmation:** Intended controlled copy.
- **Who or what:** QA.

### GAP-312 Reviewer separation of duties across sections
- **Question:** Should a reviewer who tested a different section of the same sample be allowed to review this section?
- **Affected:** MOD-REVIEW-001, MOD-APPROVAL-001.
- **Facts:** The reviewer check covers only the current test orders of the section being reviewed [src: backend/MicroLIMS.Application/Services/SampleReviewService.cs:L108-L113]. The approver check is also per section [src: SampleApprovalService.cs:L165-L171].
- **Why it matters:** The guide must say whether separation is per section or per sample.
- **Needs confirmation:** Intended rule.
- **Who or what:** QA; Section Head.


### GAP-313 OOS investigation documents: no signature, no void screen
- **Question:** Should uploading, superseding or voiding a lab investigation document need an electronic signature, and should users be able to void a document from the screen?
- **Affected:** MOD-OOS-001, SCR-OOS-001, WF-OOS-001 (Phase 4).
- **Facts:** The upload, supersede and void service methods call no signature service; supersede and void record reason, user and time on the document row [src: backend/MicroLIMS.Application/Services/OosInvestigationDocumentService.cs:L97-L275]. The void endpoint and the frontend service method exist; the OOS Tracking page has no control that calls them [src: backend/MicroLIMS.API/Controllers/OosInvestigationDocumentController.cs:L96-L105; frontend/src/modules/oosTracking/OosTrackingPage.tsx].
- **Why it matters:** The guide cannot tell users how to void a wrongly uploaded report, and must not imply the actions are signed.
- **Needs confirmation:** Intended signing rule and whether a void control is wanted.
- **Who or what:** QA; laboratory owner.

### GAP-314 Archived PDF integrity failure is a response header only
- **Question:** What should a user see when an archived copy fails its integrity re-check?
- **Affected:** MOD-COA-001, WF-COA-001 (Phase 4).
- **Facts:** The download endpoint returns the file and sets the header "X-Archive-Integrity: FAILED" on a hash mismatch [src: backend/MicroLIMS.API/Controllers/ArchivedRecordsController.cs:L35-L48]. Whether the report page reads this header was not checked.
- **Why it matters:** The guide must not promise a visible warning that may not exist.
- **Needs confirmation:** Intended user-visible behaviour; the front-end handling of the header.
- **Who or what:** Developer; QA.

### GAP-315 Signature statement wording differs between screens
- **Question:** Which wording is the official statement of meaning for Approved, Rejected and Closed signatures?
- **Affected:** MOD-APPROVAL-001, MOD-COA-001, SCR-APPROVAL-001, SCR-COA-001, SCR-COA-002.
- **Facts:** The certificate, the report page and the approval card print different sentences for Approved and Rejected ("sample" versus "record", "release" present or absent); the trail has no statement for the close-testing signature [src: frontend/src/modules/testingWorkspace/SampleCoaPage.tsx:54-60; SampleReportPage.tsx:42-48; sampleSummary/summaryFormatting.ts:76-83].
- **Why it matters:** The same signature reads differently depending on where it is viewed.
- **Needs confirmation:** The approved wording and where it must appear.
- **Who or what:** QA.

## C4 Monitoring & micro configuration

<!-- from C4-monitoring-microconfig.md -->

### GAP-400 EM, water and after-cleaning limits are free text with no server validation
- **Question:** Should the server reject or warn when alert, action and specification limits are not plain numbers or are out of order?
- **Affected:** SCR-EM-001, SCR-WATER-001, SCR-AC-001; BR-EM-005, BR-WATER-003.
- **Facts:** The services store limit strings unchanged; the order warning exists only in the UI and never blocks a save. An unparseable non-empty limit is silently treated as no limit at result time. [src: EnvironmentalMonitoringMasterDataService.cs:L141-L166; LimitFields.tsx:L12-L23; SpecLimitParser.cs:L52-L205]
- **Why it matters:** A typo is accepted silently and then drives (or silently disables) every result status.
- **Needs confirmation:** Intended validation rule.
- **Who or what:** Product owner / quality.

### GAP-401 Water sampling configuration has no duplicate or unit rule
- **Question:** Is a second limit row for the same test code on the same sampling point intended, and should a unit be required for water count tests?
- **Affected:** SCR-WATER-001; BR-WATER-003, BR-WATER-005.
- **Facts:** The server checks neither duplicates nor unit (After Cleaning checks duplicates). At preparation the first matching row for point and test code is linked (`FirstOrDefault`); which row that is when two exist was not determined. [src: WaterMasterDataService.cs:L122-L147; WaterWorkflowEngine.cs:L143-L149; AfterCleaningMasterDataService.cs:L117-L123]
- **Why it matters:** Duplicate rows could give an unpredictable limit.
- **Needs confirmation:** Intended rule.
- **Who or what:** Product owner.

### GAP-402 EM / water / after-cleaning configuration edits between preparation and result entry
- **Question:** Should a limit or unit change be allowed after samples are prepared from the configuration, and should it be visible?
- **Affected:** BR-EM-006, BR-EM-009, BR-WATER-005, BR-AC-008.
- **Facts:** A prepared location is linked to its configuration row; limits and unit are read at result entry, so an edit before entry applies and an edit after entry does not change the stored location. No marker records which limits were used other than the copied values. [src: IncubationStepRecorder.cs:L497-L520, L603-L620]
- **Why it matters:** Two samples prepared together can be judged against different limits.
- **Needs confirmation:** Intended behaviour (freeze at preparation or at entry).
- **Who or what:** Quality assurance.

### GAP-403 Unit typed on passive / surface / swab / rinse configurations overrides the derived unit
- **Question:** Is it intended that a unit typed for Passive air, Surface, Swab or Rinse replaces the standard unit?
- **Affected:** SCR-EM-001, SCR-AC-001; BR-EM-004, BR-AC-008.
- **Facts:** The page hint says results are reported in the standard unit; the server uses the typed unit when not blank. The code comment calls the AC unit field informational. [src: IncubationStepRecorder.cs:L511-L512, L738-L753; acConfigTypes.ts:L28-L29]
- **Why it matters:** Printed units may differ from the hint.
- **Needs confirmation:** Intended rule.
- **Who or what:** Product owner.

### GAP-404 Configuration changes carry no reason or signature
- **Question:** Should changes to EM, water and after-cleaning limits or assignments require a reason and an electronic signature?
- **Affected:** SCR-EM-001, SCR-WATER-001, SCR-AC-001; audit-trail section.
- **Facts:** Writes are audited generically; no reason or signature. Media product code change is signed. [src: MicroLimsDbContext.cs:L259-L375; MediaProductService.cs:L137-L144]
- **Why it matters:** The guide must say which changes are signed.
- **Needs confirmation:** Intended rule.
- **Who or what:** Quality assurance.

### GAP-405 Expected colony description is not enforced on the server
- **Question:** Should the server refuse an Indication challenge organism without an expected colony description?
- **Affected:** SCR-MEDIACFG-001; BR-MEDIA-009.
- **Facts:** The dialog requires it; the server stores it only for the Indication role without a non-blank check. [src: MediaConfigurationDialog.tsx:L128-L129; MediaMasterDataService.cs:L120-L126]
- **Why it matters:** An Indication challenge can be saved without the text the analyst compares against.
- **Needs confirmation:** Intended rule.
- **Who or what:** Quality assurance.

### GAP-406 Media product rename, create and delete are not signed
- **Question:** Should renaming, creating or deleting a media product require a signature as the code change does?
- **Affected:** SCR-MEDIACFG-001; audit-trail section.
- **Facts:** Only `ChangeCodeAsync` calls the signature service. [src: MediaProductService.cs:L65-L109, L137-L144, L163-L193]
- **Why it matters:** The guide must state which product changes are signed.
- **Needs confirmation:** Intended signature rule.
- **Who or what:** Quality assurance.

### GAP-407 Evaluation type "Enrichment Characteristics" has no configuration screen
- **Question:** Is "Enrichment Characteristics" a supported evaluation type?
- **Affected:** SCR-MEDIACFG-001; BR-MEDIA-015.
- **Facts:** Defined in the enum and accepted by the server; the dialog offers two types. [src: EvaluationType.cs:L6-L11; MediaConfigurationDialog.tsx:L35-L38; MediaMasterDataService.cs:L84-L85]
- **Why it matters:** The guide must not describe it as configurable.
- **Needs confirmation:** Retired or pending.
- **Who or what:** Product owner.

### GAP-408 Set point and autoclave program changes are not signed
- **Question:** Should incubator set point and autoclave program changes require an electronic signature?
- **Affected:** SCR-EQUIP-002; BR-EQUIP-006, BR-EQUIP-007, BR-EQUIP-010.
- **Facts:** The set point dialog says the reason is required "for audit and ALCOA+ compliance" (UI wording); there is no signature step. [src: EquipmentPage.tsx:L301; EquipmentConfigurationService.cs:L176-L203]
- **Why it matters:** The guide must not call the change signed nor repeat the compliance wording as fact.
- **Needs confirmation:** Intended rule and wording.
- **Who or what:** Quality assurance.

### GAP-409 Equipment and autoclave-program changes are not limited to the Section Head role
- **Question:** Should equipment configuration writes be limited to a role type like the media code change?
- **Affected:** SCR-EQUIP-002; roles section.
- **Facts:** Writes need only `MasterData.Manage`; the media code change needs the Section Head role type. [src: EquipmentMasterDataController.cs:L52-L124; MediaMasterDataController.cs:L52-L56]
- **Why it matters:** The role table must state the permission-only rule.
- **Needs confirmation:** Intended rule.
- **Who or what:** Product owner.

### GAP-410 Autoclave program audit comment is marked required but not enforced
- **Question:** Should a blank "Audit Comment / Reason" be refused?
- **Affected:** SCR-EQUIP-002; BR-EQUIP-009, BR-EQUIP-010.
- **Facts:** The label has an asterisk; the client checks only code, name and load type; the server accepts any comment and uses defaults only when the comment is absent (null), so an empty edit comment is stored empty. [src: EquipmentPage.tsx:L355-L360, L1478; EquipmentConfigurationService.cs:L299, L338]
- **Why it matters:** The history may hold empty reasons.
- **Needs confirmation:** Intended rule.
- **Who or what:** Product owner.

### GAP-411 Freeze is not refused when a test has been used or has open orders
- **Question:** Should freezing be refused or warned while open orders use the test, and what happens to open orders?
- **Affected:** SCR-TESTMASTER-001; BR-TM-003.
- **Facts:** Freeze only sets IsActive false; no usage, scope or version check. [src: TestDefinitionMasterDataService.cs:L796-L813]
- **Why it matters:** The guide must state the effect on existing work.
- **Needs confirmation:** Intended rule.
- **Who or what:** Product owner.

### GAP-412 Test Master micro changes have no reason and no signature
- **Question:** Should changes to micro test definitions, steps and media (and freeze) require a reason and signature?
- **Affected:** SCR-TESTMASTER-001; audit-trail section.
- **Facts:** Generic audit only; only titration records a dedicated event (FP). [src: TestDefinitionMasterDataService.cs:L770]
- **Why it matters:** The guide must not call these changes signed.
- **Needs confirmation:** Intended rule.
- **Who or what:** Quality assurance.

### GAP-413 Step media model: material-based in code
- **Question:** Is the step medium meant to be chosen as a media product (not a stock material batch)?
- **Affected:** SCR-TESTMASTER-001; BR-TM-010, BR-TM-018.
- **Facts:** Step medium rows carry a material id (a DehydratedMedia stock entry linked to a product); the condition comes from that product. [src: TestWorkflowStepMasterDataService.cs:L256-L299; WorkflowStepsSection.tsx:L56-L62]
- **Why it matters:** The guide's step-media text must match the released model.
- **Needs confirmation:** Intended model.
- **Who or what:** Product owner.

### GAP-414 Stage replicate section on the microbiology Test Master page (RESOLVED)
- **Question:** Do stage replicates apply to microbiology tests?
- **Affected:** SCR-TESTMASTER-001.
- **Facts:** Withdrawn: the section is rendered only for the FP page or the FP section, so it does not appear for micro tests. [src: TestMasterPage.tsx:L1425-L1430]
- **Why it matters:** None remaining.
- **Needs confirmation:** None.
- **Who or what:** n/a.

### GAP-415 Water department testing frequency: spec says it is stored, code does not
- **Question:** Should the water system store a testing frequency as the EM department does?
- **Affected:** SCR-WATER-001; BR-WATER-007.
- **Facts:** Spec lists Name + TestingFrequency; code stores name only and a code comment says the frequency lives on the location. [src: docs/superpowers/specs/2026-08-17-water-config-em-parity-design.md:L44-L46; WaterDepartment.cs:L3-L13]
- **Why it matters:** Spec and code disagree; the guide shows the code behaviour.
- **Needs confirmation:** Intended model.
- **Who or what:** Product owner.

### GAP-416 Test created through a picker defaults to workflow type Observation
- **Question:** Is it intended that "+ Add {code} to Test Master" in the EM, After Cleaning and Items pickers creates an Observation test with no steps?
- **Affected:** SCR-EM-001, SCR-AC-001; BR-TM-020.
- **Facts:** The call sends code and display name only; the server default is Observation; the test has no steps until added in Test Master; a System Administrator or multi-section user gets a section error. [src: masterDataOptions.ts:L253-L258; MasterDataRequests.cs:L170-L174; UserSectionScopeService.cs:L127-L142]
- **Why it matters:** A count test added this way cannot run correctly and is treated as presence / absence on the water page.
- **Needs confirmation:** Intended default.
- **Who or what:** Product owner.

### GAP-417 Inventory link endpoint is not used by the page
- **Question:** Is the API `link-inventory` (default 32.5 °C set point, type guess, MICRO section) still intended?
- **Affected:** SCR-EQUIP-002; BR-EQUIP-005.
- **Facts:** The page pre-fills the Add Equipment form instead; `linkInventory` is only defined in the service. [src: EquipmentPage.tsx:L405-L433; EquipmentConfigurationService.ts:L176]
- **Why it matters:** The guide must describe the form path.
- **Needs confirmation:** Whether the endpoint is retired.
- **Who or what:** Developer.

### GAP-418 Water assigned-tests hint says "received"
- **Question:** Should the hint "Every test here is ordered when a sample from this location is received" say "prepared"?
- **Affected:** SCR-WATER-001.
- **Facts:** Receiving creates no test orders; preparation does. [src: WaterLocationPanel.tsx:L160; WaterWorkflowEngine.cs:L66-L161]
- **Why it matters:** Users may expect orders at receiving.
- **Needs confirmation:** Wording.
- **Who or what:** Product owner.

### GAP-419 Test Master freeze, workflow type and step writes have no section scope check
- **Question:** Should these writes be limited to users of the test's section like the test update?
- **Affected:** SCR-TESTMASTER-001; roles section.
- **Facts:** Only the update checks the section. [src: TestDefinitionMasterDataService.cs:L393-L394; TestWorkflowStepMasterDataService.cs:L25-L333]
- **Why it matters:** Lab separation could be bypassed for steps and freezing.
- **Needs confirmation:** Intended rule.
- **Who or what:** Product owner.

### GAP-420 Delete guards are incomplete; the database refuses with a generic error
- **Question:** Should deleting a used room test configuration, water limit row, water location, EM or water department, or an organism that a step targets give a clear message?
- **Affected:** BR-EM-001, BR-EM-009, BR-WATER-001, BR-WATER-002, BR-ORG-002.
- **Facts:** Restrict foreign keys refuse these deletes; the user sees "An unexpected error occurred." and an incident is logged. After Cleaning has a clear guard. [src: SampleLocationConfiguration.cs:L25-L28; SampleConfiguration.cs:L20-L23; ExceptionMiddleware.cs:L67-L84; AfterCleaningMasterDataService.cs:L157-L171]
- **Why it matters:** Users cannot tell why a delete failed.
- **Needs confirmation:** Intended guard messages.
- **Who or what:** Developer.

### GAP-421 Autoclave program code duplicate check is case-sensitive
- **Question:** Should a code typed in other case be refused with the clear message?
- **Affected:** SCR-EQUIP-002; BR-EQUIP-008.
- **Facts:** Codes are stored upper-case; the check compares the typed text; the unique index then refuses with the generic error. [src: EquipmentConfigurationService.cs:L265-L271, L320-L321; AutoclaveProgramConfiguration.cs:L22]
- **Why it matters:** Unclear failure.
- **Needs confirmation:** Intended behaviour.
- **Who or what:** Developer.

### GAP-422 Edit Equipment changes the incubator set point without reason or history
- **Question:** Should the set point field be read-only in Edit Equipment so that only "Edit Set Point" (reason and history) changes it?
- **Affected:** SCR-EQUIP-002; BR-EQUIP-013.
- **Facts:** The field is editable and saved by the equipment update; no history row. [src: EquipmentPage.tsx:L505, L1308-L1315; EquipmentMasterDataService.cs:L140]
- **Why it matters:** Set point history can be bypassed.
- **Needs confirmation:** Intended rule.
- **Who or what:** Quality assurance.

### GAP-423 Status of the two water design specs
- **Question:** Were the water configuration and water batch specs ("Design — awaiting approval") approved, changed or superseded?
- **Affected:** BR-WATER-007, BR-WATER-010, BR-WATER-011.
- **Facts:** Both are headed "Status: Design — awaiting approval"; code differs on the department frequency, role gate and endpoint names. [src: docs/superpowers/specs/2026-08-17-water-config-em-parity-design.md:L1-L5; 2026-08-17-water-batch-workflow-design.md:L1-L5]
- **Why it matters:** INTENDED labels depend on their standing.
- **Needs confirmation:** Approval status.
- **Who or what:** Product owner.

### GAP-424 Confirmatory media count cannot be set in the page
- **Question:** Should the Test Master step form offer the confirmatory media count?
- **Affected:** BR-TM-014.
- **Facts:** The request omits it; the server stores 1. [src: WorkflowStepsSection.tsx:L168-L190; TestWorkflowStepMasterDataService.cs:L101]
- **Why it matters:** Multi-medium confirmatory rules cannot be configured.
- **Needs confirmation:** Intended.
- **Who or what:** Product owner.

### GAP-425 C2 wording on After Cleaning preparation (contradiction recorded, C2 not edited)
- **Question:** C2 says preparation ticks parts and creates one location per part; should it say part configurations?
- **Affected:** C2 WF-SAMPLE-008, GAP-225.
- **Facts:** The form submits `machinePartConfigurationIds`; one location is created per chosen configuration, one test order per distinct test code. [src: AfterCleaningPreparationForm.tsx:L59-L64; AfterCleaningWorkflowEngine.cs:L85-L130]
- **Why it matters:** The guide steps must match.
- **Needs confirmation:** Check in the C2 merge.
- **Who or what:** Documentation team.

## C5 Media, strains, stock

<!-- from C5-media-strains-stock.md -->

Existing gaps referenced, not repeated: GAP-013 (media product code role), GAP-021 (GPT engine vs code), GAP-022 (sterility), GAP-034 (Media Evaluation page not linked), GAP-037 (cryovial passage and expiry), GAP-038 (equipment qualification).

### GAP-500 Expiry not checked in individual test media selection and in the cryovial identity panel
- **Question:** Should an expired media lot be refused in individual incubation media selection and in the cryovial identity panel, as grouped batch selection already refuses it?
- **Affected:** MOD-MEDIA-001, MOD-CRYO-001, WF-CRYO-001.
- **Facts:** (verified) Individual incubation selection refuses only lots that are not released, OutOfStock or QuarantineFailed [src: backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs:L49-L51]. Grouped selection refuses expired lots with "is expired (expired on yyyy-MM-dd)" [src: backend/MicroLIMS.Application/Services/GroupedTestAction/BatchSelectMediaRules.cs:L26-L29; GroupedTestActionService.cs:L453]. The cryovial identity check does not read ExpiryDate [src: backend/MicroLIMS.Application/Services/CryovialService.cs:L70-L73]. The lot list offered in the UI filters ExpiryDate > now [src: backend/MicroLIMS.Application/Services/TestWorkflowQueryService.cs:L69]. The shared TSB start refuses an expired lot (date before today) but does not check released, OutOfStock or QuarantineFailed [src: backend/MicroLIMS.Application/Services/PathogenSessionService.cs:L575-L591] (see GAP-516). Pathogen-confirmation media selection has the status refusal and no expiry check [src: backend/MicroLIMS.Application/Workflows/TestWorkflow/PathogenConfirmationRecorder.cs:L115].
- **Why it matters:** The guide must say whether an expired lot can be used in a test or identity check.
- **Needs confirmation:** Intended rule for each path, and whether a direct request can reach the individual path with an expired lot.
- **Who or what:** QC lead and development lead.

### GAP-501 Inventory equipment status is not read by micro incubator selection; media-evaluation incubation checks nothing
- **Question:** Should an incubator marked OutOfService or Retired in Equipment Inventory, or past its calibration date only on the inventory record, be refused in individual incubation, and should media-evaluation incubation check the incubator at all?
- **Affected:** MOD-MEDIA-001, MOD-EQUIP-001, WF-EQUIP-001, WF-MEDIA-002.
- **Facts:** (corrected after re-verification) Individual incubation, pathogen confirmation and shared TSB DO check the incubator: master-data Equipment of type Incubator, set point inside the medium range, calibration due date not passed (message "The selected incubator's set point is outside this medium's temperature range." for any of these) [src: backend/MicroLIMS.Application/Services/IncubatorEligibilityService.cs:L33-L41; IncubationStepRecorder.cs:L80]. Grouped batch selection has its own check on the same master record [src: BatchSelectMediaRules.cs:L32-L46]. None of them reads the inventory register status or its calibration date, and the two records are linked only by code with the calibration date copied once at link time [src: EquipmentConfigurationService.cs:L142-L175]. Media-evaluation incubation takes an incubator id and checks nothing [src: MediaEvaluationEngine.cs:L136-L177]. HPLC, ICP and physicochemical paths do refuse OutOfService or Retired inventory equipment [src: HplcRunService.cs:L252-L256].
- **Why it matters:** The guide's "blocked from use" statement for equipment depends on which path is used.
- **Needs confirmation:** Whether micro incubators should honour inventory status and calibration date, which record is the calibration source of truth, and whether evaluation incubation should be checked.
- **Who or what:** Development lead and QC.

### GAP-502 Media status Expired and Destroyed are never set
- **Question:** Should an expired lot show an Expired status (stored or computed), and is there a Destroyed transition?
- **Affected:** MOD-MEDIA-001.
- **Facts:** MediaStatus defines Expired (2) and Destroyed (4) [src: backend/MicroLIMS.Domain/Enums/MediaStatus.cs:L4-L10]. No code assigns either value; the only reference is a comparison [src: backend/MicroLIMS.Application/Services/EquipmentInventoryService.cs:L593]. Expiry is read from ExpiryDate in queries [src: MediaPreparationService.cs:L154; MediaExpiryService.cs:L32].
- **Why it matters:** A user looking at the Status column may not see that a lot has expired.
- **Needs confirmation:** Whether the two values are intended or unused.
- **Who or what:** Product owner and QC lead.

### GAP-503 Cryovial preparation: identity-panel incubator not validated; Storage Equipment field mapping
- **Question:** What does the "Storage Equipment" field store, and should the identity-panel incubators be validated?
- **Affected:** MOD-CRYO-001, WF-CRYO-001.
- **Facts:** The request carries StorageCondition as text [src: backend/MicroLIMS.Application/Services/CryovialService.cs:L13]. The dialog requires "Storage Equipment *" [src: frontend/src/modules/laboratoryConfiguration/cryovials/components/PrepareCryovialBatchDialog.tsx:L118, L335]. The preparation service checks only media in the panel, not IncubatorEquipmentId or incubation times [src: CryovialService.cs:L68-L77, L107-L114]. Resolved: the dialog sends only text "{instrument type}, {manufacturer} ({code})" as StorageCondition and no equipment id; the freezer/InService filter is in the dialog only [src: PrepareCryovialBatchDialog.tsx:L92-L98, L141-L160].
- **Why it matters:** The guide cannot state where the storage location of a new batch is recorded at preparation.
- **Needs confirmation:** Whether the server should validate the storage equipment at preparation and whether the equipment id should be stored; intended validation of the identity-panel incubator and dates.
- **Who or what:** Development lead.

### GAP-504 Media evaluation results can be re-entered after the evaluation is Completed
- **Question:** Can a challenge result be corrected after the evaluation is Completed or the lot is QuarantineFailed, and what locks it?
- **Affected:** MOD-MEDIA-001, WF-MEDIA-002.
- **Facts:** RecordResultAsync has no check for an existing outcome or a Completed evaluation [src: backend/MicroLIMS.Application/Workflows/MediaEvaluationEngine.cs:L179-L290]. It recalculates the challenge outcome and the evaluation outcome [src: MediaEvaluationEngine.cs:L229-L231, L276-L280]. The lot is set to QuarantineFailed only when the new outcome is NonConform [src: MediaEvaluationEngine.cs:L284-L285], and nothing reverts it. Approval queue membership depends on evaluation Completed and Conform and ApprovalStatus PendingReview [src: backend/MicroLIMS.Application/Services/MediaReleaseService.cs:L41-L51].
- **Why it matters:** A corrected Conform result could leave a QuarantineFailed lot listed for release; the guide cannot describe correction.
- **Needs confirmation:** Whether re-entry is intended, and the correction procedure.
- **Who or what:** QC lead and development lead.

### GAP-505 Growth promotion without a recovery band is always Conform
- **Question:** Is a growth-promotion challenge with no configured minimum recovery intended to be Conform?
- **Affected:** MOD-MEDIA-001, WF-MEDIA-002.
- **Facts:** Outcome is Conform when RecoveryPercentMin has no value; otherwise Conform only when the recovery is inside the band [src: backend/MicroLIMS.Application/Workflows/MediaEvaluationEngine.cs:L229-L231].
- **Why it matters:** The acceptance rule the guide states must match the configured band.
- **Needs confirmation:** Approved GPT acceptance rule (also GAP-021).
- **Who or what:** QC lead.

### GAP-506 Release does not check the lot expiry date
- **Question:** Should approval refuse a lot whose expiry date has passed?
- **Affected:** MOD-MEDIA-001, WF-MEDIA-003.
- **Facts:** Approval checks evaluation status and outcome only [src: backend/MicroLIMS.Application/Services/MediaReleaseService.cs:L73-L79]. Approval sets Status Active [src: MediaReleaseService.cs:L100]. The usable lot list then excludes it because ExpiryDate is in the past [src: MediaPreparationService.cs:L154].
- **Why it matters:** A lot can be Active yet absent from every lot list.
- **Needs confirmation:** Intended rule.
- **Who or what:** QC lead.

### GAP-507 Cryovial batch destruction is unsigned, has no reason and writes no review-trail event
- **Question:** Should destruction require a reason and a password signature and be recorded in the review trail?
- **Affected:** MOD-CRYO-001, WF-CRYO-003.
- **Facts:** DestroyAsync takes only the id, sets IsDestroyed and saves [src: backend/MicroLIMS.Application/Services/CryovialService.cs:L202-L208]. The endpoint has only the class policy Cryovials.Manage [src: backend/MicroLIMS.API/Controllers/CryovialController.cs:L58]. Storage moves do write a review-gate event [CryovialService.cs:L262-L268]. No code path clears IsDestroyed.
- **Why it matters:** Destruction is the one irreversible action in the batch lifecycle with no recorded reason in the service.
- **Needs confirmation:** Intended controls. (A general record-change log row does exist for the IsDestroyed change, with user and time but no reason: see audit-trail.)
- **Who or what:** QC lead and development lead.

### GAP-508 Material receipt expiry is optional
- **Question:** Should expiry be required for dehydrated media, lyophilized microorganisms and supplements?
- **Affected:** MOD-MATERIAL-001.
- **Facts:** Expiry is nullable on receipt [src: backend/MicroLIMS.Application/Services/MaterialService.cs:L14]; the required-field message does not include expiry [src: frontend/src/modules/inventory/materials/components/AddMaterialDialog.tsx:L621]; consumption checks expiry only when set [src: MaterialService.cs:L387]. A lot without expiry never expires.
- **Why it matters:** Users need to know whether a lot can be used indefinitely.
- **Needs confirmation:** Intended rule per material type.
- **Who or what:** QC lead.

### GAP-509 Media lot expiry date is entered by hand; no shelf-life rule found
- **Question:** Is the media lot expiry set from a configured shelf life, or entered at each preparation?
- **Affected:** MOD-MEDIA-001, MOD-CRYO-001 (see also GAP-037).
- **Facts:** The preparation request takes ExpiryDate from the user and stores it [src: backend/MicroLIMS.Application/Services/MediaPreparationService.cs:L11-L14, L94]. Cryovial expiry is also entered [src: CryovialService.cs:L97]. No shelf-life calculation was found.
- **Why it matters:** The guide cannot state how an expiry date is derived.
- **Needs confirmation:** Intended rule.
- **Who or what:** QC lead.

### GAP-510 Media lot remaining quantity is not tracked on the lot
- **Question:** How is the remaining quantity of a media lot known, and does Out of Stock depend on storage quantities?
- **Affected:** MOD-MEDIA-001.
- **Facts:** The lot stores TotalWeight and TotalVolume [src: MediaPreparationService.cs:L85-L86]; no remaining-quantity field exists on Media [src: backend/MicroLIMS.Application/Services/MediaExpiryService.cs:L10-L11]. Storage entries hold quantities per location [src: backend/MicroLIMS.Application/Services/MediaStorageService.cs:L99, L108-L109].
- **Why it matters:** Out-of-stock is a manual status only (GAP-512).
- **Needs confirmation:** Intended stock model.
- **Who or what:** Product owner.

### GAP-511 Lyophilized disc consumed at selection, before a result
- **Question:** If a disc is selected and the challenge is later changed or the evaluation is abandoned, is the unit returned to stock?
- **Affected:** MOD-MEDIA-001, MOD-CRYO-001.
- **Facts:** One unit is consumed at selection [src: backend/MicroLIMS.Application/Workflows/MediaEvaluationEngine.cs:L122-L127]; re-selecting the same disc returns without consuming again [src: MediaEvaluationEngine.cs:L102-L103]; no reversal path found.
- **Why it matters:** Stock counts may diverge from physical use.
- **Needs confirmation:** Intended rule.
- **Who or what:** Development lead.

### GAP-512 Mark out of stock is unsigned, reason optional and has no reversal
- **Question:** Should Out of Stock require a reason and a signature, and can a lot be reinstated?
- **Affected:** MOD-MEDIA-001, WF-MEDIA-004.
- **Facts:** The service takes an optional comment and sets OutOfStock [src: backend/MicroLIMS.Application/Services/MediaPreparationService.cs:L160-L177]; the dialog label is "Reason / Comment (Optional)" [src: frontend/src/modules/laboratoryConfiguration/media/dialogs/MarkOutOfStockDialog.tsx:L120]; no password path in that dialog (raw inventory §3). No transition back to Active found.
- **Why it matters:** The guide must state the reversal path, if any.
- **Needs confirmation:** Intended controls.
- **Who or what:** QC lead.

### GAP-513 Media release gate uses permission and SoD only; System Administrator grant not read
- **Question:** Should the release action also be restricted by role type, and is System Administrator intended to hold Media.Release?
- **Affected:** MOD-MEDIA-001, WF-MEDIA-003.
- **Facts:** The endpoint has Policy:Media.Release [src: backend/MicroLIMS.API/Controllers/MediaController.cs:L73-L74]; SectionHead holds Media.Release in the seed [src: documentation/knowledge/_raw/verified-roles-permissions.md:L86]; SoD check [src: backend/MicroLIMS.Application/Services/MediaReleaseService.cs:L81-L82]; SystemAdministrator holds all 50 default codes (verified, section 2.3).
- **Why it matters:** The guide must name the roles that can release.
- **Needs confirmation:** Role grants on the live Roles table (read-only query) and intent; compare GAP-013.
- **Who or what:** System administrator and QC lead.

### GAP-514 Reviewer holds Media.Prepare and Cryovials.Manage but not Materials.Manage or Equipment.Manage
- **Question:** Is a Reviewer meant to prepare media lots and cryovial batches, given the pickers need stock and equipment lists that a Reviewer's default permissions cannot read?
- **Affected:** MOD-MEDIA-001, MOD-CRYO-001, WF-MEDIA-001, WF-CRYO-001.
- **Facts:** Reviewer default set includes Media.Prepare, Cryovials.Manage, TestWorkflow.Execute and not Materials.Manage or Equipment.Manage [src: documentation/knowledge/_raw/verified-roles-permissions.md:L86-L88, role table]. The media dialog loads `GET /api/inventory/materials` and the cryovial dialog also `GET /api/inventory/equipment`, both under those class policies [src: frontend/src/modules/inventory/materials/services/MaterialService.ts:L28-L29; backend/MicroLIMS.API/Controllers/MaterialController.cs:L22; backend/MicroLIMS.API/Controllers/EquipmentInventoryController.cs:L26]. Not run: the effect on screen is read from code.
- **Why it matters:** The guide's "who can prepare" statement depends on it.
- **Needs confirmation:** Intended Reviewer scope for media and cryovial preparation, and the live Roles grants.
- **Who or what:** QC lead and system administrator.

### GAP-515 Media preparation values are not range-checked
- **Question:** Should weight, volume, pH, cycle values and expiry date be range-checked, and should the autoclave be refused when out of service or out of calibration?
- **Affected:** MOD-MEDIA-001, WF-MEDIA-001.
- **Facts:** The dialog checks only that each field is filled; the service stores the values and checks only that the equipment is an Autoclave [src: frontend/src/modules/laboratoryConfiguration/media/dialogs/MediaPreparationDialog.tsx:L105-L153; backend/MicroLIMS.Application/Services/MediaPreparationService.cs:L51-L52, L85-L94]. Program, load, temperature and cycle time come from the chosen autoclave program in the dialog but the server does not re-validate them against the program.
- **Why it matters:** A lot can be saved with an implausible pH or a past expiry date, or on an autoclave marked out of service.
- **Needs confirmation:** Intended limits and the autoclave usability rule.
- **Who or what:** QC lead.

### GAP-516 Shared TSB start does not check release status of the media lot
- **Question:** Should starting the shared TSB refuse a lot that is not released, out of stock or quarantined, as individual selection does?
- **Affected:** MOD-MEDIA-001, WF-MEDIA-003.
- **Facts:** StartSharedTsb checks expiry, section, incubator range and the step's permitted medium list, with no IsReleasedForUse / OutOfStock / QuarantineFailed refusal [src: backend/MicroLIMS.Application/Services/PathogenSessionService.cs:L575-L600]. Individual selection has that refusal [src: backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs:L49-L51]. Not tested.
- **Why it matters:** Whether an unreleased lot can start a test depends on the entry point.
- **Needs confirmation:** Intended rule and whether the screen only offers released lots.
- **Who or what:** Development lead.

### GAP-517 Two equipment records with a code-only link; micro incubator checks read the master-data one
- **Question:** Which record is authoritative for incubator calibration and status, and should edits to the inventory register update the master-data record?
- **Affected:** MOD-EQUIP-001, WF-EQUIP-001.
- **Facts:** Inventory (`EquipmentInventories`) holds status, calibration date, certificates; master data (`Equipment`) holds set point and calibration date used by test checks; matched by code, inventory edits do not write the master record, and linking an incubator creates a master record with set point 32.5 [src: backend/MicroLIMS.Application/Services/EquipmentInventoryService.cs:L190-L200; EquipmentConfigurationService.cs:L142-L175; IncubatorEligibilityService.cs:L33-L41].
- **Why it matters:** A user who updates the calibration date or marks an incubator out of service in Equipment Inventory may believe tests are blocked when they are not.
- **Needs confirmation:** Intended source of truth; whether a manual second update is the procedure.
- **Who or what:** QC lead and development lead.

### GAP-518 Evaluation steps are not ordered on the server
- **Question:** Should incubation or a result be refused until an organism source (cryovial or disk) has been assigned, and should incubation be recordable only once per challenge?
- **Affected:** MOD-MEDIA-001, WF-MEDIA-002.
- **Facts:** `RecordIncubationAsync` and `RecordResultAsync` do not check that CryovialId or LyophilizedDiskId is set, and `RecordIncubationAsync` does not check for an existing incubation (a second call creates a new Incubation and re-points the challenge) [src: backend/MicroLIMS.Application/Workflows/MediaEvaluationEngine.cs:L136-L290]. The dialog shows Step 2 whether or not Step 1 is done [src: frontend/src/modules/laboratoryConfiguration/media/dialogs/MediaEvaluationWorkflowDialog.tsx:L340-L404]. Read from code; not run.
- **Why it matters:** The record of which organism lot challenged a media lot can be empty for a conforming lot.
- **Needs confirmation:** Intended step order and whether it should be enforced.
- **Who or what:** QC lead and development lead.

## C6 Physicochemical

<!-- from C6-physchem.md -->

GAP-600 Failed HPLC system suitability has no re-run path (narrowed by verification)
- Question: Is abandon-and-restart the only intended recovery from a Failed SST, and may the same instrument start a new run immediately?
- Affected: MOD-HPLC-001, MOD-GC-001 (SCR-HPLC-002).
- Facts: A failed SST is stored as Failed and the run stays Open; no code path returns it to Pending; the only exit is Abandon Run. [src: backend/MicroLIMS.Application/Services/HplcRunService.cs:L364, L396, L472, L543]. The spec states the same: "A failed SST keeps the run locked; the analyst abandons the run (with a reason) and starts a new one." [src: docs/superpowers/specs/2026-09-29-hplc-chain-design.md:L83]. Code and spec agree, so the three-way DISCREPANCY flagged by the extractor is downgraded: the behaviour is intended. A new run needs a fresh SST and a new standard weighing; the failed confirmation already deducted the standard from the lot (BR-HPLC-010).
- Why it matters: the guide can say "abandon the run and start a new one" and must not describe a re-run of the same SST.
- Needs confirmation: only whether the site SOP wants anything extra (for example an investigation record). Nothing in code requires one.
- Who could confirm: FP Section Head / QC lead.

GAP-601 ICP run when no element passes calibration (narrowed by verification)
- Question: What should the analyst do when the calibration is confirmed but no element is valid?
- Affected: MOD-ICP-001 (SCR-ICP-002a).
- Facts: Confirmation is not blocked by failing elements; sample assignment is blocked until at least one element is valid ("No element is valid on this run."). This matches the spec rule "The run continues if >= 1 element passed" [docs/superpowers/specs/2026-10-03-icp-gc-workspaces-design.md:L105-L106; backend/MicroLIMS.Application/Services/IcpRunService.Samples.cs:L85-L96]. No recalibration path exists after confirmation ("The calibration has already been confirmed."); the exit in code is Abandon Run.
- Why it matters: the guide must not describe re-entering the calibration after confirmation.
- Needs confirmation: that abandon-and-restart is the intended recovery.
- Who could confirm: FP Section Head.

GAP-602 Segregation of duties for physchem run decisions
- Question: Must the person who ran or entered an HPLC or ICP SST, calibration, or sample be barred from reviewing it?
- Affected: MOD-HPLC-001, MOD-ICP-001, MOD-WS-001 (partly covered).
- Facts: The shared SoD guard (sample review and approval, owned by C3) counts a user who entered a physchem TestAnalysis as having performed the test, so that user cannot review or approve the sample [backend/MicroLIMS.Application/Services/SegregationOfDutiesGuard.cs:L24-L45]. No SoD check exists between starting a run, confirming the SST or ICP calibration, entering CCV and submitting samples in the same run. Working standard sign-off has its own SoD (roles section). [src: verified-roles-permissions.md:L138; WorkingStandardService.SignOff.cs:L22-L88]
- Why it matters: the guide must describe SoD rules accurately.
- Needs confirmation: intended SoD for physchem run review.
- Who could confirm: QA.

GAP-603 Critical CCV lock scope
- Question: Should a failed ICP CCV stop submission of samples already submitted in the run, or only pending samples?
- Affected: MOD-ICP-001.
- Facts: Lock applies to samples not yet submitted; submitted samples are not pulled back ("ponytail" ceiling); reviewer sees the failed CCV on the run. [src: docs/superpowers/specs/2026-10-03-icp-gc-workspaces-design.md sec. 4.2; IcpElementAvailability.cs:L41-L44]
- Why it matters: affects what the guide says to do after a failed CCV.
- Needs confirmation: accepted behaviour.
- Who could confirm: QA.

GAP-604 Measurement and Qualitative panels (RESOLVED by verification)
- Question: What are the rules for Measurement and Qualitative results?
- Facts: Read and recorded in BR-PHYSRESULT-002 (replicate count and basis from the Test Master; Mean, Min, Max or EachValue; Complies / Does not comply with a mandatory observation when not complying). GAP-044 field-label work list for these two panels is filled there. No open question remains except the Test Master configuration of a given test, which is C7.
- Who could confirm: not needed.

GAP-605 Solution expiry clock (DISCREPANCY confirmed, effect limited)
- Question: Should shelf-life expiry use the lab-local clock (spec) or UTC (code)?
- Affected: MOD-SOLN-001.
- Facts: Spec: "expiresAt = preparedAt + shelf life (lab-local clock via ILabClock)" [docs/superpowers/specs/2026-09-29-hplc-chain-design.md:L69]. Code: ExpiresAt = PreparedAt (UTC) plus hours or days, compared with UTC now [backend/MicroLIMS.Application/Services/SolutionPreparationService.cs:L293-L298, L435-L436; SolutionPreparationResponse.cs:L49-L52]. Both are absolute instants, so an hours-based shelf life is identical; a days-based shelf life can differ only across a daylight-saving change of the lab time zone (derived from the code, not tested). The lab time zone is configurable [backend/MicroLIMS.Application/Services/LabClock.cs:L41-L56].
- Why it matters: expiry shown near midnight or around a clock change.
- Needs confirmation: whether the spec's "lab-local clock" meant wall-clock expiry.
- Who could confirm: developer and QA.

GAP-606 Equation types page is read-only (RESOLVED by verification)
- Facts: Read-only is intended: "Equation Types" = hardcoded read-only list endpoint [docs/FP_HPLC_Phase3_Build_Spec.md:L71, L127]. The hard-coded list shows six entries only (None, System Suitability, Numeric Measurement, Gravimetric % Loss, Gravimetric % Residue, Qualitative / Identification), not the newer equation types. [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L815-L857]
- Why it matters: the guide must not describe editing equations or imply the list covers dissolution, titration or chromatography tests.
- Needs confirmation: only whether the short list is acceptable to the site (informational).
- Who could confirm: developer.

GAP-607 Column master permission mismatch (confirmed)
- Question: Should column writes require MasterData.Manage (page) or Equipment.Manage (API)?
- Affected: MOD-COLUMN-001.
- Facts: Page guard MasterData.Manage [frontend/src/routes/AppRoutes.tsx]; API POST/PUT/deactivate Equipment.Manage [backend/MicroLIMS.API/Controllers/EquipmentMasterDataController.cs:L137-L147]. Default roles: SectionHead holds both; Analyst holds Equipment.Manage only (cannot open the page). [src: documentation/knowledge/_raw/verified-roles-permissions.md:L86, L88]
- Why it matters: who can add or deactivate columns in practice.
- Needs confirmation: intended permission.
- Who could confirm: system administrator / developer.

GAP-608 Working standard rounding
- Question: Is the RSD limit (2.0 %) to be compared after rounding to 2 decimals, and is potency stored at 3 decimals?
- Affected: MOD-WS-001.
- Facts: PassesRsd rounds to 2 decimals before comparing. Potency = mean x 100 / (100 - MC) rounded to 3 decimals. [src: backend/MicroLIMS.Application/Helpers/WorkingStandardCalculator.cs:L22-L40]
- Why it matters: boundary cases (RSD exactly near 2.00) pass or fail depending on rounding.
- Needs confirmation: site SOP wording.
- Who could confirm: QC lead.

GAP-609 Titration types offered (UI exposure resolved)
- Facts: The Test Master offers all five types (Acid-base, Redox, Complexometric, Precipitation, Karl Fischer (volumetric)), both modes, both calculation methods and both endpoints. [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/titrationConfig.ts:L5-L27]. Karl Fischer is limited to the USP factor calculation in direct mode (TitrationDefinitionRules.cs:L49-L54, L72; UI message "Karl Fischer cannot be a residual titration."). The titration spec lists all five (section 1).
- Why it matters: the guide may list the five types; which ones the site actually uses stays a configuration matter.
- Needs confirmation: which types the site uses.
- Who could confirm: Physicochemical Section Head.

GAP-610 Weight variation band defaults versus configured bands (DISCREPANCY confirmed, minor)
- Question: Which band thresholds and percentages apply to each tablet product?
- Affected: MOD-PHYSRESULT-001.
- Facts: Evaluator defaults are 130 mg / 324 mg with 10 / 7.5 / 5 %. Spec example is tablets >= 250 mg +/-5 %. Bands are per-test fields, so configured values may differ. [src: backend/MicroLIMS.Application/Helpers/WeightVariationEvaluator.cs:L5-L19; docs/FP_Other_Equation_Types_Build_Spec.md:L160-L162; TestDefinitionMasterDataService.cs:L191-L204]
- Why it matters: the guide cannot state a band without the product configuration.
- Needs confirmation: product configuration and intended EP 2.9.5 table.
- Who could confirm: Physicochemical Section Head.

GAP-611 Dissolution and disintegration stage parameters
- Question: Are stage offsets (5/15/25/2), the always-continue rule, and the disintegration counts (6/12, 16 of 18) fixed or set per product?
- Affected: MOD-PHYSRESULT-001.
- Facts: Per-test offsets exist on TestDefinition (DissolutionS1Offset etc.). Defaults in the evaluator match the spec. Source of the always-continue rule: lab decision 2026-09-19 (spec). [src: FP_Other_Equation_Types_Build_Spec.md:L188-L197, L210-L213; DissolutionStageEvaluator.cs:L48-L52; DisintegrationStageEvaluator.cs:L38-L40]
- Why it matters: the guide must not present defaults as the product limits.
- Needs confirmation: per-product values and the USP edition used.
- Who could confirm: QC lead.

GAP-612 Column usage recording
- Question: Is per-column usage (number of runs, injections) required?
- Affected: MOD-COLUMN-001.
- Facts: Run stores the column id only. No usage counter, column-use log or column history screen found. [src: backend/MicroLIMS.Domain/Entities/HplcRun.cs:L19]
- Why it matters: the guide must not describe column life tracking.
- Needs confirmation: requirement.
- Who could confirm: QC lead.

GAP-613 Instrument report import
- Question: Is any user-facing import of Empower or ICP reports intended?
- Affected: MOD-INSTIMPORT-001.
- Facts: Parsers and OCR reader exist; no caller in controllers, services or frontend. Evidence upload is manual. [src: documentation/01_APPLICATION_MAP.md:L109; ServiceCollectionExtensions.cs:L297-L301]
- Why it matters: the guide must state that results are typed manually.
- Needs confirmation: product intent.
- Who could confirm: product owner.

GAP-614 Working standard sign-off roles
- Question: Which roles are the reviewer and "QC head" for working standard sign-off?
- Affected: MOD-WS-001.
- Facts: Review uses Samples.Review and approval uses Samples.Approve (policies). Section Head holds Samples.Approve; Reviewer holds Samples.Review. [src: backend/MicroLIMS.API/Controllers/WorkingStandardsController.cs:L82-L103; verified-roles-permissions.md:L86-L87]
- Why it matters: role names in the guide must match the system.
- Needs confirmation: roles used at the site.
- Who could confirm: system administrator.

GAP-615 Signature on master reason dialogs (RESOLVED by verification)
- Facts: The Solution master, ICP method and HPLC method "reason" dialogs record an audit event with the reason and before/after values; no electronic signature is taken. [src: backend/MicroLIMS.Application/Services/SolutionMasterService.cs:L28, L171-L180; IcpMethodService.cs:L118, L185, L215; HplcMethodService.cs:L243-L265]. Spec D7 asks for "edit in place with a required reason and audit trail", with no signature. No question remains.
- Who could confirm: not needed.

GAP-616 Signature point for HPLC qualification submit (RESOLVED by verification)
- Facts: The qualification entry page button "Send for Review" opens SendForReviewDialog (title "Submit Qualification for Review", button "Proceed to Sign"), then the "Electronic Signature" dialog, which posts the password to qualification-samples/{id}/submit. [src: frontend/src/modules/hplcWorkspace/entry/QualificationEntryPage.tsx:L145-L146, L233; SendForReviewDialog.tsx:L62-L94; backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:L141-L145]
- Who could confirm: not needed.

GAP-617 Working standard stock deduction at SST (spec vs code)
- Question: Should a working standard lot be deducted when it is used as an HPLC SST standard?
- Affected: MOD-WS-001, MOD-HPLC-001.
- Facts: Working-standard spec D8 says stock is "not deducted by SST use, same as primary RS lots today" [docs/superpowers/specs/2026-10-03-working-standard-qualification-design.md:L27]. Code deducts the weighed standard (mg to g or kg) from the lot at SST confirmation, working standard lots included, and also from primary standards in titrant standardization and relative titration [backend/MicroLIMS.Application/Services/HplcRunService.cs:L460-L464, L553-L583; commit 890c3382, same day as the spec]. The spec statement appears stale.
- Why it matters: the guide must say that stock decreases on SST confirmation (pass or fail) and that a depleted lot blocks further SST use.
- Needs confirmation: the intended stock rule.
- Who could confirm: FP Section Head / QC lead.

GAP-619 Working standard moisture edit in the interface
- Question: Can the analyst correct the moisture value of a Draft qualification after creating it?
- Affected: MOD-WS-001 (SCR-WS-001, WF-WS-001).
- Facts: The server accepts edits of a draft (PUT /api/working-standards/qualifications/{id}; "Only a draft qualification can be edited."); the frontend service has an update call, but no screen that uses it was found, and the detail drawer shows "Moisture Content" as read-only text. Moisture is required before the qualification can be assigned to a run ("Enter the moisture content first."). [src: backend/MicroLIMS.API/Controllers/WorkingStandardsController.cs:L52-L55; backend/MicroLIMS.Application/Services/WorkingStandardService.cs:L236-L262, L316-L328; frontend/src/modules/workingStandards/services/WorkingStandardService.ts:L55; qualifications/drawer/QualificationSourceSection.tsx:L73-L75]
- Why it matters: if moisture was left empty or mistyped at creation, the guide must say what the user does (a Draft can be returned or rejected only after submit; no delete was found).
- Needs confirmation: whether an edit control exists in the running application, and the intended correction path.
- Who could confirm: developer or QA.

## C7 General masters

<!-- from C7-masters.md -->

Numbering: this cluster uses GAP-700..749. Existing GAP-034 (Specifications route) is resolved by GAP-702. GAP-043/GAP-044 (field-label work lists) are covered for the Items, Receiving Configuration, Workbook and physchem Test Master screens by the corrected `screens` section; labels of the shared `SignatureDialog`, `IcpSpecFields`, `ResidualSolventSpecFields`, `TitrationSpecBasisFields` and the titration selects were not re-read.

### GAP-700 Item deletion button state vs runtime rejection
- Question: Should the "Delete item" button be disabled when the item has already received samples, instead of waiting for the server refusal?
- Affected: SCR-ITEM-001 (`ItemTable.tsx`), `ItemService.cs`.
- Facts: The button is always enabled; the confirmation text warns that deletion is blocked once samples exist. The server counts samples and refuses with `Cannot delete '{item.Name}' - it has been used to receive {sampleCount} sample(s). Freeze it instead to stop new samples without losing history.` [src: frontend/src/modules/laboratoryConfiguration/items/ItemTable.tsx:L150-L153; backend/MicroLIMS.Application/Services/ItemService.cs:L122-L126] (verified).
- Why it matters: A user gets an error after confirming a destructive action. Status: this is a usability observation, not a spec discrepancy (no specification covers it).
- Needs confirmation: Whether the item list should carry a "used by samples" indicator.
- Who could confirm: Product Owner / Frontend Lead.

### GAP-701 Item document size limit: controller 30 MB, service 25 MB
- Question: What is the intended maximum size of an item document: 25 MB or 30 MB?
- Affected: SCR-ITEM-001 (upload dialog), `ItemDocumentController`, `ItemDocumentService`.
- Facts: The controller allows requests up to 30 MB; the service refuses files over 25 MB with `File size must be between 1 byte and 25 MB.`; the dialog says "PDF, Word, Excel, or Image (Max 25MB)". The earlier draft quoted a message `File size exceeds the 25 MB limit.` that does not exist (corrected). A 27 MB file passes the request filter and is refused by the service [src: backend/MicroLIMS.API/Controllers/ItemDocumentController.cs:L50; backend/MicroLIMS.Application/Services/ItemDocumentService.cs:L74-L75; frontend/src/modules/laboratoryConfiguration/items/components/UploadItemDocumentDialog.tsx:L163].
- Why it matters: The user sees the service message; the user guide should state 25 MB. No specification covers the limit, so this is a code-internal inconsistency, not a DISCREPANCY against a spec (downgraded).
- Needs confirmation: Which value is intended.
- Who could confirm: Backend Lead.

### GAP-702 Standalone Specifications route is only a redirect
- Question: Is `/laboratory-configuration/specifications` a retired page that should be removed or documented?
- Affected: SCR-SPEC-001, `AppRoutes.tsx`, `menuConfig.ts`.
- Facts: `SpecificationsPage` only renders `<Navigate to="/laboratory-configuration/items" replace />`; the route is guarded by `MasterData.Manage`, whereas the Items page needs `Items.Manage` (a user holding only `MasterData.Manage` is redirected to a page that needs `Items.Manage`; what they then see was not run). The sidebar has no entry. The Build Spec places the UI in the "Item workspace -> Specifications tab", which matches the code, so there is no spec-vs-code discrepancy. Resolves GAP-034 [src: frontend/src/modules/laboratoryConfiguration/specifications/SpecificationsPage.tsx:L4-L6; frontend/src/routes/AppRoutes.tsx:L219-L229; docs/Universal_Specifications_Build_Spec.md:L84-L85].
- Why it matters: Tells the guide not to describe a separate Specifications screen.
- Needs confirmation: Whether to retire the route.
- Who could confirm: Product Owner.

### GAP-703 Workbook accepts only .xlsx
- Question: Should a clearer message be shown when a non-.xlsx file is chosen?
- Affected: SCR-WORKBOOK-001.
- Facts: The file picker filters to `.xlsx`; the server converts a workbook format error into `InvalidOperationException` with the format exception's message (the exact text depends on the workbook library and was not read) [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L84; backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L275-L282].
- Why it matters: The guide cannot quote the message. The design spec says "XLSX via ClosedXML" only (no statement about the error text).
- Needs confirmation: The text shown for a corrupt or non-xlsx file (REQUIRES CONFIRMATION by running the application).
- Who could confirm: Technical Lead.

### GAP-704 Preparation configuration is usable before approval
- Question: Is it acceptable that a configuration seeded from an analyst's first entry (or saved by a Section Head) is used by later samples while still `PendingReview`?
- Affected: SCR-ITEM-001 (Preparation Configuration tab), `SamplePreparationService`, `ItemPreparationConfigurationService`.
- Facts: By design in the code comments "testing is never blocked waiting for approval"; the Items screen warns "This configuration is in effect and is already being used for testing. Approving it records your review; editing it first replaces the protocol for future samples." [src: backend/MicroLIMS.Application/Services/ItemPreparationConfigurationService.cs:L26-L29; frontend/src/modules/laboratoryConfiguration/items/components/ItemPreparationConfigurationSection.tsx:L129-L134]. No specification covers this (the nearest spec, `docs/superpowers/specs/2026-09-01-config-driven-preparation-fs.md`, was not read for this cluster: REQUIRES CONFIRMATION).
- Why it matters: A non-standard first entry becomes the standing protocol until reviewed.
- Needs confirmation: QA policy.
- Who could confirm: QA Manager / Microbiology Section Head.

### GAP-705 Reason and e-signature on configuration changes: SOP says yes, screens do not
- Question: Should Item, Specification, Receiving Configuration and Test Master edits ask for a reason and an electronic signature as the draft SOP states?
- Affected: SCR-ITEM-001, SCR-RECVCFG-001, SCR-TESTMASTER-002/003; SOP ML-SOP-MDW-001 sections 9 and 10.
- Facts: SOP section 10: "In the screens, every change asks for a reason and an electronic signature, and the audit trail records the old and new values." Code: Item, specification, reference list, freeze/unfreeze and Test Master saves take no reason and no signature; only titration changes need a typed reason (no signature). The generic audit trail does record old and new values. DISCREPANCY (spec vs code), confirmed [src: docs/validation/ML-SOP-MDW-001_Master_Data_Workbook_User_Guide.md:L302-L306; backend/MicroLIMS.Application/Services/ItemService.cs:L62-L82; backend/MicroLIMS.Application/Services/MasterData/SpecificationMasterDataService.cs:L127-L208].
- Why it matters: The workbook's add-only rule is justified by this statement, and the user guide must not promise a signature step that does not exist.
- Needs confirmation: Whether the SOP sentence is a goal or a description; whether the screens will be changed.
- Who could confirm: QA / Validation lead.

### GAP-706 Freeze and unfreeze of a test have no section check
- Question: Should freezing a Test Master row be limited to members of the test's section?
- Affected: SCR-TESTMASTER-002/003, `TestDefinitionMasterDataService.FreezeTestDefinitionAsync`.
- Facts: Create, update and stage-replicate changes refuse users outside the test's section; freeze and unfreeze take only the id and check nothing but `MasterData.Manage` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L796-L812; backend/MicroLIMS.API/Controllers/TestDefinitionMasterDataController.cs:L40-L48]. Only the Finished Product section's tests appear on the physchem pages, so another lab's Section Head cannot reach them from these screens but can call the endpoint.
- Why it matters: The guide cannot promise lab-isolated freezing.
- Needs confirmation: Intended behaviour.
- Who could confirm: Backend Lead / QA.

### GAP-707 Preparation approval: no signature, no second person, `Rejected` unused
- Question: Is an unsigned "Approve" click by the same person who saved the protocol the intended review?
- Affected: SCR-ITEM-001 (Preparation Configuration), `ItemPreparationConfigurationService`.
- Facts: `ApproveAsync` checks only that the configuration exists and is not already approved; it does not take a password and does not compare the approver with the creator or last editor; `ApprovalGateStatus.Rejected` exists but nothing sets it (verified). No spec found: REQUIRES CONFIRMATION [src: backend/MicroLIMS.Application/Services/ItemPreparationConfigurationService.cs:L85-L99; backend/MicroLIMS.Domain/Enums/ApprovalGateStatus.cs:L3-L8].
- Why it matters: The word "approved" in the UI may be read as a signed GMP approval.
- Needs confirmation: Intended control.
- Who could confirm: QA Manager.

### GAP-708 Physicochemical area membership not enforced in Test Master / specification editing
- Question: Should a user whose membership grants only one physchem area (fp or rmpm) be limited to that area's tests and specifications?
- Affected: SCR-TESTMASTER-002/003, Items specifications tab.
- Facts: Memberships can carry a physchem area and `GetPhyschemAreasAsync` is used by the testing workspace and the organization service; the Test Master and specification services only check section membership (search found no other use) [src: backend/MicroLIMS.Application/Services/UserSectionScopeService.cs:L81-L100; backend/MicroLIMS.Application/Services/TestingWorkspaceService.cs:L274].
- Why it matters: The guide should not say area-limited users are restricted here.
- Needs confirmation: Intended rule; REQUIRES CONFIRMATION by testing with an area-limited user.
- Who could confirm: Physicochemical Section Head.

### GAP-709 Deleting a production stage unlinks historical samples and blocks stage-dependent tests
- Question: Is it intended that deleting a production stage (no usage check) lets historical Finished Product samples lose their stage link?
- Affected: SCR-RECVCFG-001 (Production Stages card), `StageReplicateResolver`.
- Facts: The delete confirmation says samples keep the stage; the database sets the sample's stage link to empty on deletion (the stage text on the sample stays), and the replicate resolver then returns `Sample has no reconciled production stage - it cannot be resolved to a stage role. Contact a Section Head to set its stage.` for those samples. Stage-specific specifications also fall back to the every-stage rows for them [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ProductionStagesCard.tsx:L386-L391; backend/MicroLIMS.Persistence/Configurations/SampleConfiguration.cs:L28-L30; backend/MicroLIMS.Application/Services/StageReplicateResolver.cs:L61-L72; backend/MicroLIMS.Application/Services/SpecificationLookup.cs:L19-L23].
- Why it matters: A routine list edit can change which specification and replicate rules apply to in-flight samples.
- Needs confirmation: Whether delete should be refused for a stage in use, as it is for a cause of testing.
- Who could confirm: Product Owner.

### GAP-710 "At least one assigned test" is enforced only in the Add/Edit dialog
- Question: Should the server refuse an item with no assigned tests?
- Affected: SCR-ITEM-001, `ItemService`, workbook Items sheet.
- Facts: The dialog refuses to save with zero tests; `CreateAsync`/`UpdateAsync` and the workbook accept zero tests; the item then cannot receive samples (`Item '{name}' has no assigned tests. ...`). Principle 2 in the project instructions says rules live on the backend [src: frontend/src/modules/laboratoryConfiguration/items/components/AddItemDialog.tsx:L63-L66; backend/MicroLIMS.Application/Services/ItemService.cs:L35-L99; backend/MicroLIMS.Application/Workflows/ProductWorkflowEngine.cs:L58-L61].
- Why it matters: A verified workbook load can activate an item that cannot be received.
- Needs confirmation: Intended server rule.
- Who could confirm: Backend Lead.

### GAP-711 Item document status `Voided` has no path
- Question: Is voiding an item document planned?
- Affected: SCR-ITEM-001 (Documents & Attachments), `MaterialDocumentStatus`.
- Facts: The enum has `Voided`; item document code sets only `Current` and `Superseded`; there is no delete or void action on the Documents tab (View and Download only) [src: backend/MicroLIMS.Domain/Enums/MaterialDocumentStatus.cs:L6-L11; backend/MicroLIMS.Application/Services/ItemDocumentService.cs:L122-L136].
- Why it matters: A wrongly uploaded document can only be superseded by a newer upload.
- Needs confirmation: Intended correction route.
- Who could confirm: QA / Document Control.

### GAP-712 Rounding before specification comparison across all callers
- Question: Do all result calculators pass unrounded values to `SpecificationEvaluator`?
- Affected: all workspaces that call `SpecificationEvaluator.Evaluate`.
- Facts: The evaluator never rounds. The measurement and gravimetric calculators pass the unrounded mean, min or max. Callers in the HPLC, ICP and other helpers (`HplcSampleAssayEvaluator`, `IcpSampleEvaluator`, `HplcResidualSolventEvaluator`) were not read for rounding [src: backend/MicroLIMS.Application/Services/SpecificationEvaluator.cs:L8-L87; backend/MicroLIMS.Application/Helpers/MeasurementCalculator.cs:L92-L117].
- Why it matters: The user guide states that "a result equal to an inclusive limit passes"; whether the reported (rounded) figure or the raw figure is compared must be stated per test type.
- Needs confirmation: Per-calculator rounding point. The physchem-cluster owners can confirm.
- Who could confirm: Physicochemical Section Head / validation lead.

### GAP-714 The Items test picker can create Test Master entries, and the server does not check item test codes
- Question: Is it intended that the "Assigned Tests" picker offers `+ Add "{text}" to Test Master` and that item create/update accepts test codes that are not in the Test Master?
- Affected: SCR-ITEM-001 (Add / Edit Item dialog), `TestCodePickerMulti`, `ItemService`, `TestSectionLookup`.
- Facts: Typing an unknown code in the picker creates a Test Master entry (code and display name both set to the typed text) through the same API as the Test Master screen, so it needs `MasterData.Manage` and a section; the workflow type chosen by that inline call was not read. `ItemService.CreateAsync` stores the codes as given without checking them; at sample receipt `TestSectionLookup` refuses codes missing from the Test Master (`Test code(s) '...' are not in the Test Master. Add them to the Test Master with a laboratory section before creating this test.`). The workbook Items sheet does check codes (`No test definition with code '{code}'. Create it in Test Master first.`) [src: frontend/src/components/TestCodePickerMulti.tsx:L19-L58; backend/MicroLIMS.Application/Services/ItemService.cs:L96-L99; backend/MicroLIMS.Application/Workflows/TestSectionLookup.cs:L28-L34; backend/MicroLIMS.Application/Services/MasterDataWorkbook/Sheets/InventorySheets.cs:L190-L192].
- Why it matters: The guide should tell users to create tests in the Test Master first and not to rely on the inline option.
- Needs confirmation: Intended use of the inline option; the default workflow type it creates.
- Who could confirm: Product Owner.

### GAP-713 Document upload permission is not applied to the upload buttons
- Question: Should the "+ Upload SOP" and "+ Upload Verification Report" buttons be hidden from users without `Items.DocumentUpload`?
- Affected: SCR-ITEM-001 Documents & Attachments.
- Facts: The server requires `Items.DocumentUpload`; no permission check was found for these buttons in `items/` (only `Items.Manage` gates the route). Default roles hold both codes, so the difference appears only with custom roles [src: backend/MicroLIMS.API/Controllers/ItemDocumentController.cs:L48-L49].
- Why it matters: A custom-role user could see a button that always fails.
- Needs confirmation: By running the screen with a custom role.
- Who could confirm: Frontend Lead.

## C8 Reports, KPI, Document Control

<!-- from C8-reports-dc.md -->

### GAP-800 TrendingTab hardcoded fallback options and criteria shown as real data
- Question: Why does `TrendingTab.tsx` contain hardcoded test codes, products, categories, and initial search criteria that appear in the UI when backend options fail to load or are empty?
- Affected: SCR-REPORTS-005 (`TrendingTab.tsx:L44-L51, L217-L223, L249-L256, L276-L282`).
- Facts: In `TrendingTab.tsx`, initial state defaults `testCode` to "TAMC" and `subjectName` to a hardcoded product name. When `filterOptions.testCodes` is empty, fallback items (TAMC, TYMC, WATER_TAMC, PATHOGEN_ECOLI) are rendered. When `filterOptions.subjectNames` is empty, five fallback subjects (including two hardcoded product names, a raw material, purified water, and a sampling point) are rendered. When `filterOptions.categories` is empty, four fallback categories are rendered.
- Why it matters: If backend filter options fail to load or the database is freshly initialized, users see misleading mock product names and tests. Selecting a hardcoded test code that is not in the Test Master makes the server answer `Test code "{code}" is not configured in Test Master.`; whether the four codes exist depends on the data (REQUIRES CONFIRMATION). The same page also keeps two other fallbacks: a point click that cannot load its record builds a record from the chart point, and a failed qualitative row click does nothing (see GAP-817).
- Needs confirmation: Whether to replace all hardcoded fallbacks with empty state pickers disabled until valid filter options are fetched from the server.
- Who could confirm: Frontend Lead / Product Owner.

### GAP-801 OverviewTab static "Live Database Data" chip and pseudo-deltas
- Question: Should the "Live Database Data" chip reflect actual connection status, and should KPI card deltas show real historical comparisons instead of static 100% / ratio figures?
- Affected: SCR-REPORTS-001 (`OverviewTab.tsx:L120-L124`, `OverviewService.ts:L133-L177`).
- Facts: In `OverviewTab.tsx`, `<Chip size="small" label="Live Database Data" ... />` is hardcoded static JSX rendered on every load. In `OverviewService.ts`, the `deltaPercent` and arrow indicators on KPI cards do not compare with prior periods; instead, `totalTests`, `pendingReview`, and `pendingApproval` hardcode `deltaPercent: 100` (or `0`), `approvedResults` displays the within-period approval percentage as a delta, and `outOfSpec` displays the raw count as a delta percentage.
- Why it matters: Users interpret the green/red arrows and percentages as month-over-month or period-over-period trend deltas, which misrepresents laboratory operational metrics. When the overview request fails, the service also returns zeros with the caption "no data" and no error message, so an outage looks like an empty laboratory (GAP-817) [src: frontend/src/modules/reports/services/OverviewService.ts:L188-L205].
- Needs confirmation: Whether to implement true prior-period comparison query in `ReportingQueryService.GetOverviewAggregateAsync` or remove the delta arrows from `OverviewTab`.
- Who could confirm: Product Owner / Backend Lead.

### GAP-802 MOD-KPI-002 section capacity settings and analyst availability not exposed in UI
- Question: When will frontend management screens or dialogs be added for section capacity settings (`/api/kpi/settings/{sectionId}`) and analyst availability windows (`/api/kpi/availability`)?
- Affected: MOD-KPI-002, SCR-KPI-001 (`kpiWorkloadService.ts:L44-L67`, `WorkloadTable.tsx:L111-L113`).
- Facts: Backend controllers (`KpiWorkloadController.cs`) and services (`KpiSettingsService.cs`, `AnalystAvailabilityService.cs`) fully support per-section working weekday masks, standard analyst hours, load horizons, and analyst leave windows. Frontend service `kpiWorkloadService.ts` contains client methods for them. However, no UI screen, dialog, or form in the application calls these methods. The workload table shows alert "Capacity assumes full availability and the default hours."
- Why it matters: Section Heads cannot adjust standard hours or record analyst leave through the application interface, forcing all workload capacity calculations to run on static default assumptions (6.0 hours, Mon-Fri, 5-day horizon).
- Needs confirmation: Roadmap for adding a "Section Capacity Settings" dialog and "Analyst Availability" calendar to the KPI or Laboratory Configuration area.
- Who could confirm: UX Lead / Product Manager.

### GAP-803 MOD-REPORTS-002 reporting backfill endpoint not exposed in UI
- Question: Should an administrative trigger button be added to the Admin/System area to run the `POST /api/admin/reporting/backfill` sweep?
- Affected: MOD-REPORTS-002 (`ReportingAdminController.cs:L25-L27`, `ResultProjectionService.cs:L413-L514`).
- Facts: `ReportingAdminController` exposes `POST /api/admin/reporting/backfill` gated by `Reporting.Admin`. It performs a full projection synchronization from CountTestReading, WorkflowStepResult, SampleLocation, and ParameterResult into ResultRecord. However, there is no button or view in the frontend admin interface to trigger this backfill.
- Why it matters: If data is migrated, imported, or reconciled directly in the database, system administrators cannot re-trigger the reporting projection from the application UI and must use external API tools (e.g. Swagger / curl).
- Needs confirmation: Whether to expose a "Backfill Reporting Data" button under System Administration / Maintenance.
- Who could confirm: System Architect / Backend Lead.

### GAP-804 Lack of user notification before triggering 10,000-row export limit
- Question: Should the UI warn users or disable exports when matching records exceed 10,000 rows, rather than waiting for backend exception rejection?
- Affected: SCR-REPORTS-002 (`ReportResultsTable.tsx:L322-L330, L484-L489`), `ReportingController.cs:L160-L165`.
- Facts: Backend enforces `MaxExportRows = 10_000` on CSV exports. If `totalCount > 10,000`, backend throws `InvalidOperationException` with message `This filter matches {totalCount} rows, which exceeds the 10,000-row export limit. Narrow your filters (e.g. a shorter date range) and try again.` In `ReportResultsTable.tsx`, clicking "Export CSV" opens a confirmation dialog stating `This will export {totalCount} row(s)...` without checking if `totalCount > 10,000`. Confirming triggers a request that fails and displays an error banner.
- Why it matters: Users expecting an export of a large dataset experience an unexpected failure after confirming the action.
- Needs confirmation: Whether `ReportResultsTable` should inspect `totalCount > 10000` client-side and disable the export button with a tooltip or show an explanatory warning in the confirmation dialog.
- Who could confirm: Frontend Lead / UX Lead.

### GAP-805 On-time all stages KPI unconditionally excludes samples without preparation records
- Question: How should on-time turnaround be evaluated for finished-product physicochemical samples and environmental monitoring samples that do not generate `SamplePreparation` records?
- Affected: MOD-KPI-001 (`OnTimeAllStagesKpi.cs:L69-L74`, `2026-10-11-kpi-remodel-index.md:L70`).
- Facts: In `OnTimeAllStagesKpi.cs`, the analyst stage clock starts strictly at `SamplePreparation.PreparedAt`. Finished-product physicochemical samples, Environmental Monitoring samples, and retest clones do not have a `SamplePreparation` row in the database. Consequently, they are skipped from evaluation and reported under gap `NO_ASSIGNMENT_TIME`.
- Why it matters: Physicochemical FP and EM testing constitutes a major portion of laboratory testing, but none of these samples are factored into the headline "On time, all stages" percentage.
- Needs confirmation: Whether samples without `SamplePreparation` should use `Sample.ReceivedAt` or `TestOrder.CreatedAt` as the analyst stage clock start.
- Who could confirm: Quality Assurance Lead / System Architect.

### GAP-806 Observation workflow tests excluded from Right First Time KPI
- Question: Should pathogen observation test returns be tracked via `TestReturnEvent` so qualitative tests can be included in the Right First Time KPI?
- Affected: MOD-KPI-001 (`RightFirstTimeKpi.cs:L75-L80`, `AnalystWorkloadService.cs:L184-L187`, `2026-10-11-kpi-remodel-index.md:L72`).
- Facts: In `RightFirstTimeKpi.cs` and `AnalystWorkloadService.cs`, test orders with `WorkflowType.Observation` are excluded from right-first-time evaluation because reviewer returns in the observation workflow do not create `TestReturnEvent` records. These tests are omitted from both numerator and denominator with gap `OBSERVATION_NOT_TRACKED`.
- Why it matters: Pathogen testing teams do not see their test volume reflected in the headline Right First Time metric.
- Needs confirmation: Whether to instrument observation workflow send-backs to record `TestReturnEvent` rows with `WorkflowType.Observation`.
- Who could confirm: Microbiology Section Head / Backend Lead.

### GAP-807 MOD-DC-005 training escalation management not exposed in UI
- Question: Should a dedicated escalation management panel or action buttons be added to the frontend Document Control interface for reviewing overdue summaries and manually triggering escalation sweeps?
- Affected: MOD-DC-005 (`DocumentEscalationController.cs:L38-L91`, `DocumentEscalationService.cs:L39-L80`).
- Facts: `DocumentEscalationController` exposes 4 endpoints (`assignments/{id}/history`, `summary/overdue`, `process-due`, `resolve`) gated by `Documents.TrainingAssign`. They evaluate assignments against 3 escalation tiers (ApproachingDue, Due, Overdue) and record resolutions with regulatory reasons. However, no UI screen, dashboard tile, or dialog in `frontend/src/modules/documentControl` calls these endpoints. The escalation code also sends no message: it stores a record with a recipient text and an audit entry only (BR-DC-019).
- Why it matters: Section Heads and QA Compliance managers cannot view overdue training escalation summaries or resolve open escalation records through the web interface, relying entirely on background workers or direct API calls.
- Needs confirmation: Whether to add an "Overdue Escalations" tab or drawer to `ComplianceDashboardPage` or `TrainingMatrixPage`.
- Who could confirm: Product Owner / UX Lead.

### GAP-808 Dual PDF comparison viewer lacks visual text diffing
- Question: Is side-by-side iframe rendering intended as the final revision comparison experience, or is automated text/layout diff highlighting planned?
- Affected: SCR-DC-006 (`DualPdfComparisonViewer.tsx:L1-L150`, `Release_1d_Delta_URS.md`).
- Facts: `DualPdfComparisonViewer` embeds two standard PDF viewer iframes side-by-side (left: effective revision, right: proposed revision) with zoom and page controls. It does not perform automated visual diffing, text difference extraction, or change highlighting.
- Why it matters: Approvers and reviewers reviewing long SOPs must manually scan pages side-by-side to detect text deletions or phrasing adjustments that are not documented in the change items table.
- Needs confirmation: Whether side-by-side visual inspection meets standard operating requirements or if text diff highlighting is required.
- Who could confirm: Quality Assurance Lead / System Architect.

### GAP-809 Document training configuration lacks dedicated UI management interface
- Question: Where should administrators manage per-document and per-type training settings (`RequiresReading`, `RequiresRetrainingOnRevision`, `EscalationDaysBeforeDue`, `EscalationDaysAfterDue`)?
- Affected: MOD-DC-004, MOD-DC-002 (`TrainingAssignmentService.cs:L73-L82`, `DocumentConfigurationPage.tsx:L46-L100`).
- Facts: Backend services evaluate `DocumentTrainingConfigurations` hierarchy (Master-specific > Type-specific > System defaults); the same table also holds the configurable acknowledgement statement text, grace period and retraining switch, and nothing in the application writes to it. However, `DocumentConfigurationPage.tsx` manages Document Types, Departments, Sections, and Numbering, but has no tab or form for configuring training parameters or escalation threshold days.
- Why it matters: Laboratory training administrators cannot toggle whether a document requires reading upon revision or adjust the 3-day approaching / 1-day overdue escalation thresholds without direct database changes.
- Needs confirmation: Whether to add a "Training Policies" tab to `DocumentConfigurationPage` or embed training settings into `DocumentType` dialog.
- Who could confirm: UX Lead / Training Manager.

### GAP-810 Reading progress viewport tracking does not enforce reading before acknowledgement
- Question: Should the system enforce a minimum viewport reading progress (e.g. 100% viewed) before enabling the legal acknowledgement checkbox?
- Affected: SCR-DC-002 (`ControlledPdfViewer.tsx:L180-L195`, `DocumentAcknowledgementDialog.tsx:L170-L197`, `DocumentAcknowledgementService.cs:L126-L131`).
- Facts: `ControlledPdfViewer` and `DocumentAcknowledgementDialog` track viewport scroll percentage (`readingProgress`), but display the mandated disclaimer `"Reading Progress {progress}%. Informational Only. Formal Acknowledgement Required"`. The submit button in `DocumentAcknowledgementDialog` only checks `confirmedStatement` (checkbox) and `canAcknowledge`; it does not check if `readingProgress >= 100`.
- Why it matters: Users can open the acknowledgement dialog and attest to having read the document without actually scrolling through the entire controlled PDF.
- Needs confirmation: Whether regulatory policy requires blocking acknowledgement until the user has scrolled through 100% of the document pages.
- Who could confirm: Quality Assurance Lead / Compliance Officer.

### GAP-811 Document Control dashboard may fail for users who cannot query the global audit trail
- Question: Is it intended that the dashboard (open to every signed-in user) depends on the global audit query, which only Reviewer, Section Head and System Administrator role types may call?
- Affected: SCR-DC-001 (`DocumentControlDashboardPage.tsx:L76-L110`), `DocumentAuditService.cs:L33-L35`.
- Facts: the page requests the library and the 5 newest audit rows together in one `Promise.all`; the audit request is refused with "You do not have permission to query the global audit trail." for an Analyst (verified in code). The `catch` shows one error alert. Not seen in the running application.
- Why it matters: an Analyst holding `Documents.Register` is offered the "Register Document" button on a page that may show only an error.
- Needs confirmation: what an Analyst sees on opening `/document-control`.
- Who could confirm: developer; Document Control owner.

### GAP-812 Dashboard counts use only the first 100 masters
- Question: Should the six dashboard counts come from server-side totals?
- Affected: SCR-DC-001 (`DocumentControlDashboardPage.tsx:L76-L102`).
- Facts: the page loads 100 library rows (`pageSize: 100`, voided included) and counts Effective, Draft, File Pending, Review Overdue and Voided from those rows; only "Total Documents" uses the server total.
- Why it matters: with more than 100 masters the five counts under-state the real figures while the total is correct.
- Needs confirmation: the expected number of masters and whether server counts are required.
- Who could confirm: Document Control owner; developer.

### GAP-813 Audit trail category filter offers values the server does not have
- Question: Which category values should the filter offer?
- Affected: SCR-DC-007 (`DocumentAuditPage.tsx:L151-L156`), `AuditActionCategory.cs:L5-L13`.
- Facts: the dropdown lists Document, Workflow, Security, Configuration, System; the server enum has Document, Review, Approval, Training, Kaf, Configuration, Security, ImportExport, Other. Review, Approval and Training entries (most of the workflow rows) cannot be selected. Behaviour of the server for "Workflow" or "System" was not tested.
- Why it matters: an auditor cannot filter to review or approval events and may get an error or an empty list for two entries.
- Needs confirmation: result of selecting "Workflow" and "System" in the running application.
- Who could confirm: developer; QA.

### GAP-814 Periodic review completion has no role check when no reviewer is assigned
- Question: Who may complete a periodic review task that has no assigned reviewer?
- Affected: WF-DC-003 (`PeriodicReviewService.cs:L554-L561`), `PeriodicReviewService.cs:L103-L112`.
- Facts: the worker leaves the assigned reviewer empty when the default person is the revision's author. The completion check only compares roles when a reviewer is assigned; with none assigned any signed-in user except the author passes it (the controller has a plain authorize). Verified in code, not run.
- Why it matters: a periodic review with an outcome (including an obsolescence recommendation) could be completed by anyone.
- Needs confirmation: whether this state occurs and the intended rule.
- Who could confirm: Document Control owner; developer.

### GAP-815 Word-to-PDF conversion at approval is specified but not in the code; PDF-only submission is accepted
- Question: Which release baseline applies to how the controlled PDF is produced and what submission requires?
- Affected: WF-DC-002 (`Release_1d_Delta_FRS.md:L109-L127, L299-L335`, `DocumentReviewService.cs:L64-L74`, `DocumentDetailPage.tsx:L271-L284`, `DocumentApprovalService.cs:L688-L700`).
- Facts: FS-1d-013 and FS-1d-014 describe a server-side converter (Gotenberg) producing the controlled PDF from the approved Word file; no converter exists in the code (git grep). The approval checklist requires an active controlled PDF that a person uploads. FS-1d-003 says submission no longer needs a PDF; the page offers "Submit for technical review" only when a controlled PDF is active, and the server accepts a PDF when no Word file exists. The Release 1d RTM marks the items PLANNED (GAP-024).
- Why it matters: the guide must describe manual PDF upload, not automatic conversion.
- Needs confirmation: whether conversion is planned or dropped.
- Who could confirm: Document Control owner; QA.

### GAP-816 Approval with an immediate effective date does not start the training cascade
- Question: Should every activation of an Effective revision create reading assignments and close old ones?
- Affected: WF-DC-002, WF-DC-005 (`DocumentApprovalService.cs:L576-L600`, `DocumentEffectiveDateService.cs:L296-L310`).
- Facts: `ProcessEffectiveRevisionCascadeAsync` is called only after the worker activates a FutureEffective revision. The approval step sets Effective directly (effective date today or earlier) and does not call it. No other caller exists (git grep). Manual assignment has an API but no screen (git grep in the frontend).
- Why it matters: documents approved with an immediate date may produce no reading assignments and leave old ones open as Assigned or Overdue.
- Needs confirmation: observe a test approval with today's date and check My Reading List and the matrix.
- Who could confirm: developer; Document Control owner.

### GAP-817 Reports show substitute data instead of an error in two places
- Question: Should a failed load show an error?
- Affected: SCR-REPORTS-001, SCR-REPORTS-005 (`OverviewService.ts:L188-L205`, `TrendingTab.tsx:L104-L121, L490-L497`).
- Facts: if the overview request fails, the six cards show 0 with the caption "no data" and the page does not show an error. If a chart point's record cannot be loaded, the dialog is filled from the chart point itself (round 1, no sample id, no test order id). If a qualitative row's record cannot be loaded, nothing happens.
- Why it matters: a failed load can be read as zero activity or as a complete record.
- Needs confirmation: whether the dialog marks such a record as partial.
- Who could confirm: developer; Product Owner.

### GAP-818 No transition to Obsolete exists although the interface and specification promise it
- Question: How is an effective document retired?
- Affected: WF-DC-003, MOD-DC-001 (`PeriodicReviewService.cs:L638-L700`, `DocumentApprovalService.cs:L688-L700`, `TrainingAssignmentService.cs:L425-L500`, `ApprovalWorkspaceDialog.tsx:L275-L278`).
- Facts: the periodic review outcome "Obsolescence Recommended" creates an approval task for an Effective revision. The approval workspace tells the approver that approving "will decommission the document". The approval readiness check requires status AwaitingApproval, so approving that task is refused, and no code assigns the status Obsolete (git grep). The routine that cancels training on obsolescence is never called. FS-1b-052 requires the transition after a signed approval. The void rule tells users to retire documents "through obsolescence".
- Why it matters: effective documents cannot be retired in the application; the guide must not describe obsolescence as available.
- Needs confirmation: the intended retirement route and whether it is planned.
- Who could confirm: Document Control owner; QA.

### GAP-819 Training curriculum cannot be edited in the application and its department filter is not applied
- Question: Where are role curricula managed, and should the department be applied?
- Affected: WF-DC-005 (`TrainingAssignmentService.cs:L160-L190`).
- Facts: the cascade reads each curriculum item's role and department but queries users by role only (department ignored). No screen or controller found for creating curricula or manual assignments in the frontend. The same finding appears in the multi-department recon memory note.
- Why it matters: staff in other departments with the same role may receive assignments; administrators cannot see or change who is targeted.
- Needs confirmation: how curricula are loaded today (database script, API tool).
- Who could confirm: Document Control owner; developer.

### GAP-820 KPI profile versions can be created and signed by the same person
- Question: Is a second-person check wanted for changes that alter weighted KPI figures?
- Affected: WF-KPI-002 (`WorkflowProfileService.cs:L56-L90`).
- Facts: one user adds a version and signs it in the same request; no segregation rule exists in the Kpi service folder (git grep). The signature records the user, the meaning MasterDataChanged and the reason.
- Why it matters: the person whose workload is measured could also set the weights if they hold `Kpi.ConfigureProfiles`.
- Needs confirmation: intended approval model for profile versions.
- Who could confirm: Laboratory owner; QA.

### GAP-821 Register dialog asks for the owner as a user number
- Question: Should the owner be picked from a list of people?
- Affected: WF-DC-001 (`RegisterDocumentDialog.tsx:L302-L307`).
- Facts: the dialog field is "Document Owner User ID *" with the helper "Assigned primary owner". The reviewer picker in the submit dialog, by contrast, lists people by name.
- Why it matters: users must know an internal id; a wrong id gives the server error "Owner user {id} not found." or assigns the wrong owner.
- Needs confirmation: whether the field is a number box in the running application.
- Who could confirm: developer; UX Lead.

### Confirmation of existing DISCREPANCY gaps (no new ids)
- GAP-023 CONFIRMED: the sign-in footer wording is outside this cluster; inside it, the approval signature dialog states "you certify under 21 CFR Part 11" and cites "21 CFR §11.200", the approval workspace says "Approval Governed by 21 CFR Part 11 Electronic Signature", the acknowledgement dialog heading says "(GMP / 21 CFR Part 11)" and the KPI PDF footer says "MicroLIMS verified report" [src: frontend/src/modules/documentControl/components/ElectronicSignatureDialog.tsx:L119-L122, L202; frontend/src/modules/documentControl/components/ApprovalWorkspaceDialog.tsx:L253; frontend/src/modules/documentControl/components/DocumentAcknowledgementDialog.tsx:L212; frontend/src/modules/kpi/utils/exportKpiPdf.ts:L143]. The guide does not repeat these claims.
- GAP-024 CONFIRMED with an update: the code contains the Training Matrix, My Reading List, acknowledgement, periodic review, the effective-date worker and the approval signature that older release records list as not implemented. It does not contain the Release 1d server-side conversion (GAP-815) or the obsolescence transition (GAP-818), which the newer records plan.
- GAP-045 CONFIRMED (not downgraded): the KPI design file still reads "Status: draft for review (no code written)" while the code and the plan index (with 21 listed deviations from it) are in use. The KPI definitions in BR-KPI-005 to BR-KPI-016 are therefore described as implemented behaviour and marked DISCREPANCY only where the plan or design text differs (BR-KPI-005, BR-KPI-009) [src: docs/superpowers/specs/2026-10-11-kpi-performance-remodel-design.md:L3].
