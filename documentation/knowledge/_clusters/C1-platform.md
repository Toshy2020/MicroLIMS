# C1 Platform & administration

## modules

### MOD-AUTH-001 Authentication, session, password
- Purpose: sign in / sign out; forgot password (e-mail token); administrator-assisted recovery code; change password (incl. forced); read-only profile; browser idle timeout. [src: frontend/src/pages/Login.tsx:L12; frontend/src/pages/ChangePassword.tsx:L20; frontend/src/pages/Profile.tsx:L20]
- Sub-features: SCR-AUTH-001..006. Status: IMPLEMENTED (verified-roles §8-§9). Lab scope: none (lab membership is not checked at sign-in) [src: backend/MicroLIMS.Application/Services/AuthenticationService.cs:L60-L137].
- Password-reset confirm endpoint exists but no screen calls it: IMPLEMENTED endpoint, no UI caller [src: frontend/src/modules/authentication/services/authenticationService.ts:L23; grep of frontend/src: only definition found]. See GAP-100.

### MOD-NAV-001 Application shell and navigation
- Purpose: header (dark mode, notifications, account menu), collapsible sidebar, section landing pages, breadcrumbs, per-page error boundary, page-not-found. Status: IMPLEMENTED. Lab scope: sidebar groups follow lab memberships (MICRO, FP; physchem areas fp / rmpm) [src: frontend/src/routes/menuConfig.ts:L216-L266; frontend/src/hooks/useMyLabs.ts:L11-L66].
- Sub-features: SCR-NAV-001..002; shell items listed under screens. Status: IMPLEMENTED.

### MOD-NOTIF-001 Notifications
- Purpose: in-app bell (list, mark read, read all); notifications computed from current state and persisted; e-mail for error-severity notifications when SMTP is configured. Status: IMPLEMENTED (computed + persisted). Lab scope: per laboratory on lab dashboards (filter by section); all users see sections they can access [src: backend/MicroLIMS.Application/Services/DashboardNotificationService.cs:L57-L86, L128-L131].
- Sub-features: SCR-NOTIF-001; trigger list in business-rules. Status: IMPLEMENTED.

### MOD-DASH-001 Dashboards
- Purpose: four role-type dashboards with tiles that link to filtered workspace views; per-laboratory tabs (`?lab=`). Status: IMPLEMENTED. Lab scope: MICRO / FP tabs only if user is in more than one lab [src: frontend/src/modules/dashboard/DashboardPage.tsx:L40-L52].
- Sub-features: SCR-DASH-001..004 (Administrator, Analyst, Reviewer, Section Head). Status: IMPLEMENTED.

### MOD-USERS-001 User management
- Purpose: create users, edit profile, change role (reason), disable / enable (reason), unlock, e-mail password reset, administrator-assisted recovery code, force password change at next login, permanent delete, laboratory memberships. Status: IMPLEMENTED. Lab scope: memberships define each user's laboratory data [src: backend/MicroLIMS.Application/Services/LaboratoryOrganizationService.cs:L61-L98].
- Sub-features: SCR-USERS-001..002. Status: IMPLEMENTED.

### MOD-ROLES-001 Roles and permissions
- Purpose: list roles, create role (4-step wizard), edit name / description / permissions, delete custom roles. Permissions are editable on system roles too. Status: IMPLEMENTED. Lab scope: none (roles are global) [src: backend/MicroLIMS.Application/Services/RoleService.cs:L63-L171].
- Sub-features: SCR-ROLES-001..003. Status: IMPLEMENTED.

### MOD-SIG-001 Electronic signatures and segregation of duties
- Purpose: shared "Electronic Signature" password dialog; signature record per signed action (26 signed actions, 17 meanings); segregation-of-duties guard. Status: IMPLEMENTED. Lab scope: none at dialog level; SoD uses test-performer ids within section [src: backend/MicroLIMS.Application/Services/SegregationOfDutiesGuard.cs:L6-L54].
- Sub-features: SCR-SIG-001; SoD rules in roles. Status: IMPLEMENTED.

### MOD-MSG-001 Messages
- Purpose: direct (1-to-1) and group conversations, unread count, new-message notification. Status: IMPLEMENTED. Lab scope: none (any signed-in user can message any active user in the directory) [src: backend/MicroLIMS.API/Controllers/UserDirectoryController.cs:L10-L20; backend/MicroLIMS.Application/Services/UserService.cs:L65-L72].
- Sub-features: SCR-MSG-001. Status: IMPLEMENTED.

### MOD-DISC-001 Discussions
- Purpose: posts (11 categories), comments, attachments (max 5 files, 10 MB each), important flag, versioned edits, post history. Status: IMPLEMENTED. Lab scope: none [src: backend/MicroLIMS.Application/Services/DiscussionAttachmentPolicy.cs:L14-L19].
- Sub-features: SCR-DISC-001..002. Status: IMPLEMENTED.

### MOD-ERR-001 Error Monitoring
- Purpose: technical incident list (not part of the GxP record per page text), severity override, resolve / reopen, CSV export. Status: IMPLEMENTED. Lab scope: none [src: frontend/src/modules/errorMonitoring/ErrorMonitoringPage.tsx:L60-L61].
- Sub-features: SCR-ERR-001. Status: IMPLEMENTED.

### MOD-AUDIT-001 Audit Trail (Audit Search) and traceability chain
- Purpose: search audit events (up to 300 latest), inspect a change (diff, raw JSON), open the traceability chain, view entity history. Status: IMPLEMENTED. Lab scope: none in the query; laboratory identifiers are filter fields only [src: backend/MicroLIMS.Application/Services/AuditSearchService.cs:L54-L72].
- Sub-features: SCR-AUDIT-001. Traceability: see traceability section. Status: IMPLEMENTED.

### MOD-AUDIT-002 Login history (backend only)
- Purpose: list login attempts (user, outcome, reason). Status: NOT-EXPOSED-IN-UI (endpoint only). Lab scope: none [src: backend/MicroLIMS.API/Controllers/AuditController.cs:L51; backend/MicroLIMS.Application/Services/AuditSearchService.cs:L86-L91].
- Login attempts are recorded in every branch of login (success, unknown user, locked, wrong password, inactive) [src: backend/MicroLIMS.Application/Services/AuthenticationService.cs:L80-L137].

### MOD-SECAUDIT-001 Security audit events (backend only)
- Purpose: security event rows (authentication, session, password, account and role events). Status: NOT-EXPOSED-IN-UI; `System.ViewSecurityAudit` is NOT-ENFORCED (no endpoint checks it) [src: knowledge/_raw/verified-roles-permissions.md §1; backend/MicroLIMS.Shared/Constants/SecurityEventCodes.cs:L12-L38].

### MOD-SIG-002 Signature trail (backend only)
- Purpose: list electronic signatures for an entity. Status: NOT-EXPOSED-IN-UI (endpoint `GET /api/signatures`, no frontend caller). Lab scope: none [src: backend/MicroLIMS.API/Controllers/SignaturesController.cs:L15, L25]. Per-record signature list is also loaded by the entity-level endpoint in ElectronicSignatureService [src: backend/MicroLIMS.Application/Services/ElectronicSignatureService.cs:L101-L106].

### MOD-ARCHIVE-001 Archived records
- Purpose: frozen PDF copies generated at a final decision (sample approval or rejection, media release, cryovial approval); listed in the sample report "archived copies" block; download. Status: IMPLEMENTED. Lab scope: none in the controller [src: backend/MicroLIMS.API/Controllers/ArchivedRecordsController.cs:L16-L50].
- Creation points: sample approval [src: backend/MicroLIMS.Application/Services/SampleApprovalService.cs:L535, L639]; media release [src: backend/MicroLIMS.Application/Services/MediaReleaseService.cs:L112]; cryovial approval [src: backend/MicroLIMS.Application/Services/CryovialService.cs:L173].

### MOD-LEGACY-001 Legacy and redirect routes
- Purpose: old URLs that redirect to current pages. Status: LEGACY/RETIRED (redirects IMPLEMENTED). Lab scope: none [src: frontend/src/routes/AppRoutes.tsx:L108-L151, L235, L261-L262].
- Full redirect list in screens and in business-rules (BR-NAV-004). Status: IMPLEMENTED.

## screens

### MOD-AUTH-001 Authentication, session, password
- SCR-AUTH-001 Sign in. Route `/login`, public (signed-in users are sent to `/dashboard`) [src: frontend/src/routes/AppRoutes.tsx:L97; frontend/src/routes/PublicRoutes.tsx:L6-L10; frontend/src/pages/Login.tsx:L12].
  - Title (document.title) "Sign in · MicroLIMS"; heading "MicroLIMS" [src: frontend/src/pages/Login.tsx:L68, L86-L88].
  - Fields: "Username" (required, autofocus); "Password" (required; show/hide toggle "Show password" / "Hide password") [src: frontend/src/pages/Login.tsx:L133-L163].
  - Buttons: "Sign In" / "Signing in…"; "Forgot password?"; "Admin-Assisted Password Recovery" -> `/admin-recovery` [src: frontend/src/pages/Login.tsx:L171-L186].
  - Forgot-password mode (same card): text "Enter your username and we'll generate a password reset request."; field "Username"; button "Send Reset Link" / "Submitting..."; links "Back to login", "Have a recovery code from your admin?" -> `/admin-recovery` [src: frontend/src/pages/Login.tsx:L99-L125].
  - Footer text "MicroLIMS v0.1 · FDA 21 CFR Part 11 & EU Annex 11 Compliant" (see GAP-023) [src: frontend/src/pages/Login.tsx:L204].
  - On success: token stored and navigate to `/dashboard` [src: frontend/src/pages/Login.tsx:L34, L37].
  - Errors shown: see validations.
- SCR-AUTH-002 Forgot password (mode of SCR-AUTH-001). Reset confirmation text always shown after submit: "If that account exists, a reset link has been generated." Errors from the request are swallowed [src: frontend/src/pages/Login.tsx:L58-L62].
- SCR-AUTH-003 Password Recovery (administrator-assisted). Route `/admin-recovery`, public [src: frontend/src/routes/AppRoutes.tsx:L98; frontend/src/pages/AdminPasswordRecovery.tsx:L7].
  - Heading "Password Recovery"; subtitle "Administrator-Assisted Account Access Recovery" [src: frontend/src/pages/AdminPasswordRecovery.tsx:L57, L62].
  - Fields: "Username"; "One-Time Recovery Code" (placeholder XXXX-XXXX-XXXX; helper "Format: 12 alphanumeric characters provided by your admin"); "New Password" (helper "Min 8 chars (upper, lower, digit, symbol)"); "Confirm New Password" [src: frontend/src/pages/AdminPasswordRecovery.tsx:L82-L110].
  - Button "Submit Password Recovery" / "Resetting Password..."; success text "Your password has been successfully reset! You can now log in using your new password." with "Go to Login Page"; link "Return to Login" [src: frontend/src/pages/AdminPasswordRecovery.tsx:L72-L138].
- SCR-AUTH-004 Change Password. Route `/change-password` inside MainLayout; no permission guard; also the forced-change target [src: frontend/src/routes/AppRoutes.tsx:L111; frontend/src/layouts/MainLayout.tsx:L63-L67; frontend/src/pages/ChangePassword.tsx:L20].
  - Heading "Change Password"; subtitle "Update your laboratory credentials in compliance with periodic security policy." [src: frontend/src/pages/ChangePassword.tsx:L61-L62].
  - Fields: "Current Password" (autofocus); "New Password"; "Confirm New Password" (helper "Passwords do not match.") [src: frontend/src/pages/ChangePassword.tsx:L71-L82].
  - Live checklist header "Password must contain:" with 5 rules (8+ chars, upper, lower, digit, special) [src: frontend/src/pages/ChangePassword.tsx:L13-L17, L86].
  - Button "Change Password" / "Updating Credentials..." [src: frontend/src/pages/ChangePassword.tsx:L111].
  - Success: "Password changed successfully. Returning to your profile..." then `/profile` after 1500 ms [src: frontend/src/pages/ChangePassword.tsx:L50, L66].
- SCR-AUTH-005 My Profile. Route `/profile`, all signed-in users; read-only ("Name and role changes are made by an administrator in the Users module.") [src: frontend/src/routes/AppRoutes.tsx:L110; frontend/src/pages/Profile.tsx:L18-L40].
  - Fields: "Full Name", "User ID" (shows username), "Role", "Last Login" (fallback "Never"), "Password Last Changed" (fallback "Never") [src: frontend/src/pages/Profile.tsx:L36-L40].
  - Load error "Could not load profile." [src: frontend/src/pages/Profile.tsx:L25].
- SCR-AUTH-006 Session Timeout Warning (dialog). Any MainLayout page [src: frontend/src/layouts/MainLayout.tsx:L153-L170].
  - Title "Session Timeout Warning"; text "You will be signed out due to inactivity in {n} second(s)."; single button "Stay signed in" (autofocus). Not closable by Escape or backdrop [src: frontend/src/layouts/MainLayout.tsx:L153-L170].

### MOD-NAV-001 Application shell and navigation
- Shell (every signed-in page) [src: frontend/src/layouts/MainLayout.tsx:L35-L66, L104, L145-L148]:
  - Skip link "Skip to main content". Hamburger aria "Toggle navigation menu" (mobile drawer; desktop collapse). Sidebar collapse state kept in localStorage key `microlims_sidebar_collapsed`. Breadcrumbs above each page. Per-page error boundary keyed on path.
  - Forced redirect to `/change-password` while `mustChangePassword` is set [src: frontend/src/layouts/MainLayout.tsx:L63-L67].
- Header [src: frontend/src/components/Header.tsx:L184-L363]:
  - Dark/light switch: aria/tooltip "Switch to dark mode" / "Switch to light mode" [src: frontend/src/components/Header.tsx:L196-L208].
  - Notifications bell aria "Notifications" (+ ", N unread") [src: frontend/src/components/Header.tsx:L216].
  - Account menu aria "Account menu for {name}": "My Profile" (`/profile`), "Messages" / "Messages (N unread)" (`/messages`), "Discussions" (`/discussions`), "Change Password" (`/change-password`), "Sign Out" (logout then `/login`, no confirmation) [src: frontend/src/components/Header.tsx:L285, L339-L363, L103-L108].
- Sidebar [src: frontend/src/components/Sidebar.tsx:L16, L43, L357-L394]: collapse/expand button "Collapse sidebar" / "Expand sidebar"; collapsed rail width 68 px (hidden on mobile); nav landmark "Main navigation". Menu contents per lab membership and permission: see business-rules BR-NAV-001..003 and the sidebar table in `01_APPLICATION_MAP.md` §3.3 (C2-C8 own the target screens).
- SCR-NAV-001 Section landing page. Routes `/sections/:slug`, `/groups/:slug` (alias); no route guard; cards are the permission-filtered children [src: frontend/src/routes/AppRoutes.tsx:L116-L117; frontend/src/pages/Section.tsx:L8-L35]. Title fallback "Section not available"; empty "You have no pages in this section."; subtitle "{n} page(s) you can open here" [src: frontend/src/pages/Section.tsx:L17, L35].
- SCR-NAV-002 Page not found. Catch-all `*` inside MainLayout [src: frontend/src/routes/AppRoutes.tsx:L280; frontend/src/pages/NotFound.tsx:L13-L19].
  - Title "Page not found"; empty-state title "There is no page at this address"; description `"{pathname}" does not match any MicroLIMS page. The link may be outdated, or the page may have moved. Use the navigation menu or return to your dashboard.`; button "Go to Dashboard" -> `/dashboard` [src: frontend/src/pages/NotFound.tsx:L13-L19].
- Permission failure: silent redirect to `/dashboard`, no message [src: frontend/src/routes/PermissionRoute.tsx:L9, L12]. Unauthenticated -> `/login` [src: frontend/src/routes/AuthenticatedRoutes.tsx:L6-L10].

### MOD-NOTIF-001 Notifications
- SCR-NOTIF-001 Notifications panel (bell) in Header [src: frontend/src/components/Header.tsx:L216-L259].
  - Empty: "No notifications" / "You are up to date - nothing needs your attention."; load failure "Notifications could not be loaded" [src: frontend/src/components/Header.tsx:L250, L257-L259].
  - Endpoints: GET `/dashboard/notifications`; POST `/dashboard/notifications/{id}/read`; POST `/dashboard/notifications/read-all` [src: frontend/src/components/Header.tsx:L117, L144, L154].
  - Unread message count GET `/messages/unread-count`, polled every 60 s, skipped while the document is hidden [src: frontend/src/components/Header.tsx:L57, L89].

### MOD-DASH-001 Dashboards
- Routing by role TYPE (not permission): Analyst -> Analyst dashboard; Reviewer -> Reviewer dashboard; SystemAdministrator -> Administrator dashboard; any other role type -> Section Head dashboard [src: frontend/src/modules/dashboard/DashboardPage.tsx:L40-L44]. See GAP-055.
- Lab tabs (only if the user is in more than one lab): aria "Laboratory dashboard"; tabs "Microbiology Laboratory" / "Physicochemical Laboratory"; order MICRO then FP; selected via `?lab=` [src: frontend/src/modules/dashboard/DashboardPage.tsx:L25-L52; frontend/src/modules/dashboard/DashboardLabContext.tsx:L6-L9].
- Error / empty states: title "Dashboard could not be loaded"; empty "No dashboard data is available yet." [src: frontend/src/modules/dashboard/components/DashboardStateGate.tsx:L45, L65].
- SCR-DASH-001 Administrator Command Center. Route `/dashboard` for role type SystemAdministrator [src: frontend/src/modules/dashboard/AdminDashboardPage.tsx:L84].
  - Heading "Administrator Command Center". Button "Refresh".
  - Tiles, in order (label -> value source -> link) [src: frontend/src/modules/dashboard/AdminDashboardPage.tsx:L70-L78]:
    - "Total samples" -> kpi-deltas `totalSamples`; "Total tests" -> kpi-deltas `totalTests`.
    - "Pending tests" -> summary `pendingTests` -> `?status=Active`.
    - "Review queue" (caption "samples") -> summary `reviewerQueue` -> `?status=UnderReview`.
    - "Approval queue" (caption "samples") -> summary `approvalQueue` -> `?status=UnderApproval`.
    - "Prep configs pending" (shown only when > 0) -> summary `pendingPreparationConfigApproval` -> `/laboratory-configuration/items`.
    - "Overdue (>24h)" -> summary `delayedTests` -> `?urgency=overdue`.
  - Load error: "The dashboard service did not respond. Your data has not been changed." [src: frontend/src/modules/dashboard/AdminDashboardPage.tsx:L52].
  - Endpoints: GET `/dashboard?lab=`; GET `/dashboard/kpi-deltas` [src: frontend/src/modules/dashboard/services/DashboardService.ts:L16, L25].
- SCR-DASH-002 Analyst dashboard ("Welcome back, {name}"). Route `/dashboard` for role type Analyst [src: frontend/src/modules/dashboard/AnalystDashboardPage.tsx:L24-L116].
  - Subtitle "Your prioritized work in the {lab name} for today." or "Your prioritized work for today." Buttons "Refresh" and a link to the lab workspace. Partial-failure alert with "Retry" [src: frontend/src/modules/dashboard/AnalystDashboardPage.tsx:L83-L116].
  - Panels and tile labels (verified this pass):
    - "My Operational Performance" [src: frontend/src/modules/dashboard/components/AnalystPerformancePanel.tsx:L23]: tile "On-Time Reading Rate" (Microbiology only; default 100 when no data); tile "7-Day Completed Volume"; tile "Active Assigned Orders" [src: AnalystPerformancePanel.tsx:L44, L65, L85].
    - "Completed Today" [src: frontend/src/modules/dashboard/components/CompletedTodayPanel.tsx:L21]: tiles "Tests Completed", "Media Lots Prepared" [CompletedTodayPanel.tsx:L43, L68].
    - "Attention Required" [src: components/AttentionRequiredPanel.tsx:L67]; empty "No active attention alerts. All conditions normal." [src: AttentionRequiredPanel.tsx:L147]; chip "Expiring" [AttentionRequiredPanel.tsx:L107]. Rows (verified): up to 5 media lots expiring within 5 days (GET `/media/expiring?withinDays=5`; Microbiology tab only) as "{media type} · Lot {lot}", "Expires in {n} day(s)", chip "Expiring", linking to `/laboratory-configuration/media`; then up to 5 of the user's notifications (types ReviewWaiting and ApprovalWaiting hidden) with severity icon, message and "just now" / "{n} min ago" / "{n} hr(s) ago" / "{n}d ago"; a load failure shows a failure alert, never the empty text [src: frontend/src/modules/dashboard/components/AttentionRequiredPanel.tsx:L41-L63, L76-L150; frontend/src/modules/dashboard/services/DashboardService.ts:L54-L55].
    - "Today's Laboratory Work" table: columns "Sample / Reference", "Category", "Tests", "Received At", "Status", "Next Action"; filter and "View all"; empty "No work matches this filter." [src: components/TodaysWorkTable.tsx:L60, L87-L92, L142].
    - Incubation panel (Microbiology only): "No active incubations running." [src: components/IncubationObservationPanel.tsx:L92].
  - Endpoints: GET `/dashboard/analyst-metrics`, `/dashboard/my-tasks`, `/dashboard/todays-work`, `/dashboard/incubation-overview?myIncubationsOnly=` [src: frontend/src/modules/dashboard/services/DashboardService.ts:L43-L58].
- SCR-DASH-003 Reviewer Command Center. Route `/dashboard` for role type Reviewer [src: frontend/src/modules/dashboard/ReviewerDashboardPage.tsx:L84].
  - Tiles [src: frontend/src/modules/dashboard/ReviewerDashboardPage.tsx:L111-L151]: "Pending review" (caption "samples", -> `?status=UnderReview`); "Overdue review" (caption "in review >24 hours", -> `?status=UnderReview&workload=reviewOverdue`); "Due today" (caption "submitted today", -> `?status=UnderReview`); "Retests in progress" (-> `?workload=retestInProgress`); "Completed today" (caption "by you").
  - Empty states: "No samples are currently waiting for scientific review."; "No recent review history." [src: ReviewerDashboardPage.tsx:L250, L411].
  - Endpoint: GET `/dashboard/reviewer` [src: frontend/src/modules/dashboard/services/DashboardService.ts:L22].
- SCR-DASH-004 Section Head Command Center. Route `/dashboard` for any other role type [src: frontend/src/modules/dashboard/SectionHeadDashboardPage.tsx:L106].
  - Tiles row 1 [src: SectionHeadDashboardPage.tsx:L132-L144]: "Active tests" (-> `?status=Active`); "Incubating" (-> `?view=kanban`); "Ready to read" (-> `?testStatus=ReadyToRead`); "Pending review" (-> `?status=UnderReview`); "Pending approval" (-> `?status=UnderApproval`); "Overdue" (-> `?urgency=overdue`); "Attention items".
  - Pipeline strip: "Incubation", "Ready to Read", "Scientific Review", "Final Approval" [src: SectionHeadDashboardPage.tsx:L175-L179].
  - Lists: "No results awaiting scientific review."; "No samples awaiting final authorization."; "No active laboratory analysts found." [src: SectionHeadDashboardPage.tsx:L359, L506, L601].
  - Charts: "Samples Received Trend" [src: frontend/src/modules/dashboard/components/SamplesTrendChart.tsx:L21]; "Test Order Status" donut, empty "No data yet." [src: components/TestOrderStatusDonut.tsx:L23, L51].
  - Load error: "The dashboard service did not respond. No laboratory data has been changed." [src: SectionHeadDashboardPage.tsx:L69].
  - Endpoints: GET `/dashboard/section-head`, `/dashboard/status-distribution`, `/dashboard/category-distribution`, `/dashboard/monthly-trend?months=`, `/dashboard/analyst-metrics` [src: DashboardService.ts:L19-L58].

### MOD-USERS-001 User management
- SCR-USERS-001 User Management. Route `/users`; guard `Users.Manage` [src: frontend/src/routes/AppRoutes.tsx:L271-L272; frontend/src/routes/routes.ts:L218; frontend/src/modules/users/UsersPage.tsx:L33].
  - Title "User Management"; subtitle "Manage system users, role assignments, security status, and account access." [src: UsersPage.tsx:L293].
  - Section "Create New User" [UsersPage.tsx:L295-L368]: "Full Name" (required); "Username" (required); "Email" (no required marker; helper "Password reset destination"); "Password" (required; helper "Min 8 chars"); role select (aria "Role", placeholder "Select Role *"); button "Create User".
  - Section "All System Users" [UsersPage.tsx:L372-L384]: columns "User", "Email", "Role", "Status", "Security / Password", "Last Login", "Actions". Row chips: "You" (own row); role or "No role"; email or "No email"; status "Active" / "Disabled"; security "Account Locked", "Must Change Password" or "Normal" [UsersPage.tsx:L393-L414].
  - Row icon actions (tooltips) [UsersPage.tsx:L426-L482]: "Edit Profile"; "Laboratory Sections"; "Change Role" (disabled for self: "System Administrators cannot change their own role"); "Disable Account" / "Enable Account" (self: "System Administrators cannot disable their own account"); "Unlock Account" (only when locked); "Reset Password via Email"; "Admin-Assisted Password Recovery" (self: "Cannot use admin recovery on own account"; disabled user: "Enable user first to perform recovery"); "Force Password Change at Next Login"; "Permanently Delete User" (self: "You cannot permanently delete your own account").
  - Dialogs (FloatingDialog) [src: UsersPage.tsx:L495-L739]:
    - "Edit Profile: {username}": "Full Name", "Username", "Email" (type email; helper "Required for email password resets"); buttons "Cancel", "Save Profile".
    - "Change Role: {username}": "Current Role: {name or None}"; select "New Role" ("Select New Role"); "Reason for Change" (required; placeholder "Mandatory administrative justification"); button "Change Role" (disabled until role and reason).
    - "Disable User Account" / "Enable User Account": "Reason for Disabling" (required when disabling; placeholder "Mandatory justification"); buttons "Disable Account" / "Enable Account".
    - "Unlock User Account": "Reason for Unlock (Optional)"; button "Unlock Account".
    - "Initiate Email Password Reset": text "Send password reset instructions to {name} ({email or 'No email on file'})?"; info "The system will generate a secure reset token link and send it via email. Plaintext passwords are never generated or shown to administrators."; "Reason for Reset (Optional)"; button "Send Reset Link".
    - "Administrator-Assisted Password Recovery": "Reason for Recovery (Mandatory)" (placeholder "e.g. User forgot password and cannot access registered email."); warning that the one-time 15-minute code is shown once; button "Generate Recovery Code" (disabled until reason). After generation: "Copy Code" / "Copied!", chip "Expires in 15 minutes", button "Close & Done".
    - "Force Password Change": text "Require {name (@username)} to change their password on next login?"; button "Force Password Change".
    - "Permanently Delete User": warning "This permanently removes the account and cannot be undone. It is only possible when the user has no activity history anywhere in the system. If this fails, deactivate the account instead."; buttons "Cancel", "Permanently Delete" / "Deleting...".
  - Client toasts [UsersPage.tsx:L94, L136, L159, L218]; further success toasts (verified): "Profile updated for {username}.", "Role changed for {username}.", "Recovery code generated for {username}.", "Recovery code copied to clipboard.", "Forced password change set for {username}.", "Laboratory sections updated for \"{username}\".", `User "{username}" was permanently deleted.`; validation toasts: "Full Name, Username, Password, and Role are required."; "Selected Role and a Reason are required for role change."; "A reason is required to disable a user account."; "A reason is required for admin-assisted password recovery."
  - Endpoints: GET `/users`; GET `/users/analysts`; GET `/users/{id}`; POST `/users`; PUT `/users/{id}`; PUT `/users/{id}/role`; PUT `/users/{id}/status`; PUT `/users/{id}/unlock`; POST `/users/{id}/password-reset`; POST `/users/{id}/admin-password-recovery`; PUT `/users/{id}/force-password-change`; DELETE `/users/{id}` [src: frontend/src/modules/users/services/UserService.ts:L23-L56; backend/MicroLIMS.API/Controllers/UserController.cs:L38-L140].
- SCR-USERS-002 Laboratory Sections (dialog). Title "Laboratory Sections: {fullName}"; options "All sections (entire department)" and an "Area" select ("FP", "RM & PM", "Both"); buttons "Cancel" and "Save Memberships" [src: frontend/src/modules/users/dialogs/UserSectionsDialog.tsx:L188-L205, L253, L316-L340]. Service calls verified (`getSections`, `getUserMemberships`, `replaceUserMemberships`; see BR-USERS-014); the extractor's file path was wrong, the file is `frontend/src/modules/users/dialogs/UserSectionsDialog.tsx`. Backend: replaces all memberships at once [src: backend/MicroLIMS.Application/Services/LaboratoryOrganizationService.cs:L61-L98].

### MOD-ROLES-001 Roles and permissions
- SCR-ROLES-001 Roles list. Route `/roles`; guard `Roles.Manage` [src: frontend/src/routes/AppRoutes.tsx:L275; frontend/src/modules/roles/RolesPage.tsx:L15].
  - Title "Roles"; subtitle "System Administrator, Section Head, Reviewer, Analyst, and any custom roles." Search placeholder "Search by role name...". Button "Create Role" -> `/roles/new` [src: RolesPage.tsx:L45-L62].
  - Columns "Role", "Type", "Status", "Users". Chips "System" / "Custom"; "Active" / "Inactive". Empty `No roles match "{search}".` [src: RolesPage.tsx:L69-L100].
- SCR-ROLES-002 Create Role. Route `/roles/new`; guard `Roles.Manage` [src: frontend/src/routes/AppRoutes.tsx:L276; frontend/src/modules/roles/CreateRolePage.tsx:L18].
  - Title "Create Role"; stepper "Name", "Base Type", "Permissions", "Review" [CreateRolePage.tsx:L16, L102].
  - Step 1: "Role Name" (required, autofocus); "Description". Next requires a non-empty name [CreateRolePage.tsx:L79-L82, L114-L115].
  - Step 2: radio options "SystemAdministrator", "SectionHead", "Reviewer", "Analyst" (raw enum labels; default Analyst) [CreateRolePage.tsx:L15, L27, L122-L129].
  - Step 3: "Or clone permissions from any existing role:" select "Select a role to clone"; permission checklist (PermissionMatrix) [CreateRolePage.tsx:L144-L154].
  - Step 4: labels "Name", "Description", "Base Type"; checked codes as chips; "None selected." [CreateRolePage.tsx:L166-L188].
  - Buttons "Back", "Next", "Create Role" / "Creating..." [CreateRolePage.tsx:L196-L203].
- SCR-ROLES-003 Role detail. Route `/roles/:id`; guard `Roles.Manage` [src: frontend/src/routes/AppRoutes.tsx:L277; frontend/src/modules/roles/RoleDetailPage.tsx:L18, L115-L125].
  - Title = role name; subtitle "Edit this role's name, description, and granted permissions."; chips "System Role", "Base Type: {type}" [RoleDetailPage.tsx:L115-L140].
  - Fields "Name", "Description" [RoleDetailPage.tsx:L143-L144]. Info: the role behaves like its base type on screens not yet migrated; permissions marked "Legacy-only" [RoleDetailPage.tsx:L149-L152].
  - Section "Permissions" (PermissionMatrix). Holders section: "No users currently hold this role."; "The users holding this role could not be loaded."; holder chips "Active" / "Disabled" [RoleDetailPage.tsx:L155-L180].
  - Buttons "Save Changes" / "Saving..."; "Delete Role" / "Deleting..." (not shown for system roles) with confirm text `Delete role "{name}"? This cannot be undone.` plus assigned-user count sentence [RoleDetailPage.tsx:L116-L196].
  - Validation "Role name is required." [RoleDetailPage.tsx:L63-L65].
- PermissionMatrix (component, used on SCR-ROLES-002 step 3 and SCR-ROLES-003) [src: frontend/src/modules/roles/components/PermissionMatrix.tsx:L61-L83]: checkbox per code with description; chip "Enforced" (success) or "Legacy-only" (warning). Tooltip (enforced): "This permission alone controls access - the endpoints it covers check it directly."; (legacy): "Access on screens covered by this permission still follows the role's underlying base type, not this checkbox, until that screen is migrated to the new permission system."
- Endpoints: GET/POST `/roles`; GET/PUT/DELETE `/roles/{id}`; PUT `/roles/{id}/permissions`; GET `/permissions` [src: frontend/src/modules/roles/services/RoleService.ts:L32-L50; backend/MicroLIMS.API/Controllers/RoleController.cs:L20-L70].

### MOD-SIG-001 Electronic signatures and segregation of duties
- SCR-SIG-001 Electronic Signature (shared dialog, used by many screens) [src: frontend/src/components/SignatureDialog.tsx:L52-L76].
  - Title "Electronic Signature". Shows signer full name (or username) and role (read-only), a plain-language meaning statement (passed by the calling screen, shown in an info alert), optional "Comment (optional)" (multiline, 2 rows, only when the caller enables it), and "Password" (required, autofocus; Enter submits when filled).
  - Buttons "Cancel" and "Sign" / "Signing..." (Sign disabled until a password is entered). On failure the server message is shown (fallback "Signature failed.") and the password is cleared; dialog stays open [SignatureDialog.tsx:L36-L60].
  - Renders in a forced light theme (PinnedLightTheme) [SignatureDialog.tsx:L80].
  - Meaning statements seen in callers (examples, verbatim): "I confirm the preparation steps shown above are the steps performed for this sample."; "I confirm this ICP calibration is accurate and complies with laboratory standards."; "I confirm that this titrant was standardized according to SOP and that the titration values entered are accurate."; "I confirm that this solution was prepared according to the approved recipe and that all listed lot quantities were consumed." [src: grep of `meaningStatement=` across frontend/src (14 call sites in modules; calling files not individually cited)].
- Signed-action inventory and meanings are listed in roles and in the audit-trail section; per-screen detail belongs to C2-C6.

### MOD-MSG-001 Messages
- SCR-MSG-001 Messages. Route `/messages`; no permission guard [src: frontend/src/routes/AppRoutes.tsx:L114; frontend/src/modules/messages/MessagesPage.tsx:L22].
  - Title "Direct & Group Messages"; subtitle "Real-time workplace discussions with laboratory analysts, reviewers, and section heads."; button "New Message" [MessagesPage.tsx:L75-L86].
  - Empty right pane: "Select a conversation" / "Choose a conversation from the list or start a new message." [MessagesPage.tsx:L163-L175].
  - Conversation list: filter placeholder "Filter conversations..."; empty "No conversations yet. Start a new message!" / "No matching conversations found."; preview "No messages yet" [ConversationList.tsx:L60, L83-L84, L162].
  - Chat: placeholder "Write a message... (Press Enter to send)"; aria "Message", send aria "Send message"; empty "No messages in this conversation yet." / "Say hello to start the conversation!"; toast "Failed to send message."; back aria "Back to conversations" [ChatWindow.tsx:L113, L164-L165, L223-L239, L76].
  - New Message dialog [NewConversationDialog.tsx:L110-L266]: title "New Message"; type radio "Direct Message (1-on-1)" / "Group Conversation"; "Group Title" (placeholder "e.g. Microbiology Team, Water Testing Group..."; group only); recipient field "Select Recipient" (direct) / "Select Participants" (group), placeholder "Search colleagues by name..."; "Initial Message" (placeholder "Type your message here..."); buttons "Cancel", "Send Message" / "Sending...".
  - Endpoints: GET/POST `/messages/conversations`; GET `/messages/conversations/{id}`; GET/POST `/messages/conversations/{id}/messages`; POST `/messages/conversations/{id}/read`; GET `/messages/unread-count`; GET `/users/directory` [src: frontend/src/modules/messages/services/messageService.ts:L11-L49].

### MOD-DISC-001 Discussions
- SCR-DISC-001 Discussions feed. Route `/discussions`; no permission guard [src: frontend/src/routes/AppRoutes.tsx:L112; frontend/src/modules/discussions/DiscussionsFeedPage.tsx:L30].
  - Title "Discussions & Knowledge Sharing"; button "New Discussion" [DiscussionsFeedPage.tsx:L97-L108].
  - Search placeholder "Search discussions by title or content..."; toggle "Important Only"; chips "All Categories" + 11 categories [DiscussionsFeedPage.tsx:L121-L172].
  - Empty "No discussions found" with "Try adjusting your search query or category filters." or "Be the first to start a conversation in this category."; button "Start a Discussion" [DiscussionsFeedPage.tsx:L202-L216].
  - Toasts: "Marked as important" / "Unmarked as important"; "Discussion post deleted."; failures "Failed to toggle important status.", "Failed to delete discussion post." [DiscussionsFeedPage.tsx:L76-L89].
  - Post card menu aria "Post actions": "View / Edit", "Mark Important" / "Unmark Important", "Delete Discussion"; chips "Important", "v{n} Edited" [components/DiscussionCard.tsx:L130-L188].
  - New Discussion dialog [components/NewDiscussionDialog.tsx:L97-L214]: title "New Discussion"; "Title" (required); "Category" select; checkbox "Mark as Important (Pinned at top)"; "Content / Details" (required); "Attachments (Documents, Images, Worksheets)" with "Choose Files" (accept .pdf .png .jpg .jpeg .gif .doc .docx .xls .xlsx .pptx .txt .csv); buttons "Cancel", "Publish Discussion" / "Publishing...".
  - Endpoints: GET `/discussions`; PATCH `/discussions/{id}/important`; DELETE `/discussions/{id}`; POST `/discussions` (multipart) [src: frontend/src/modules/discussions/services/discussionService.ts:L24-L55].
- SCR-DISC-002 Discussion detail. Route `/discussions/:id`; no route guard [src: frontend/src/routes/AppRoutes.tsx:L113; frontend/src/modules/discussions/DiscussionDetailPage.tsx:L43].
  - Back link "Back to Discussions"; buttons "Edit Post" (edit dialog; "Edit Discussion (Version {n})", fields "Title", "Category", "Mark as Important", "Content / Details", buttons "Cancel" / "Save Changes") [DiscussionDetailPage.tsx:L208-L226; components/EditDiscussionDialog.tsx:L79-L155].
  - Post menu aria "Post actions": "Mark Important" / "Unmark Important", "Delete Discussion". Chip "v{n} Edited" (tooltip "Click to view full revision history").
  - Attachments: download aria "Download {fileName}", title "Download file".
  - Comments: textarea placeholder "Write a comment or response to this discussion..."; button "Post Comment" / "Posting..."; empty "No comments yet. Be the first to share your feedback or observations!"; comment actions "Edit comment", "Delete comment"; edit mode "Cancel editing", "Save comment" [DiscussionDetailPage.tsx:L387-L493].
  - Confirm dialogs: "Delete Discussion Post" (text "Are you sure you want to delete this discussion post? This action cannot be undone.", confirm "Delete Post"); "Delete Comment" (text "Are you sure you want to delete this comment? This action cannot be undone.", confirm "Delete Comment") [DiscussionDetailPage.tsx:L538-L555].
  - Toasts: "Comment added", "Comment updated", "Comment deleted", "Discussion post deleted"; failures "Failed to post comment.", "Failed to save comment.", "Failed to delete comment." [DiscussionDetailPage.tsx:L108-L175].
  - Endpoints: GET `/discussions/{id}`; PUT `/discussions/{id}` (If-Match version); PATCH `/discussions/{id}/important`; DELETE `/discussions/{id}`; GET `/discussions/{id}/history`; GET `/discussions/{postId}/attachments/{attachmentId}/download`; POST `/discussions/{postId}/comments`; PUT and DELETE `/discussions/{postId}/comments/{commentId}` [src: frontend/src/modules/discussions/services/discussionService.ts:L29-L88].

### MOD-ERR-001 Error Monitoring
- SCR-ERR-001 Error Monitoring. Route `/error-monitoring`; guard `System.ViewErrorLog` [src: frontend/src/routes/AppRoutes.tsx:L264-L265; frontend/src/modules/errorMonitoring/ErrorMonitoringPage.tsx:L11, L60-L61].
  - Title "Error Monitoring"; subtitle "Technical incidents captured from the application, the API and the database. Operational data only - this is not part of the GxP record."
  - Filters [components/IncidentFilterBar.tsx:L31-L84]: "Severity" (All; Info, Warning, Error, Critical); "Source" (All; Backend, Frontend, Database); "Status" (All; Open, Resolved); "Active from", "Active to" (dates); "Search summary or correlation id"; buttons "Reset", "Export CSV" / "Exporting...".
  - Table headers "Severity", "Summary", "Sources", "Occurrences", "First seen", "Last seen", "Status"; empty "No incidents match these filters."; status chips "Open" (warning, filled) / "Resolved" (success, outlined) [components/IncidentTable.tsx:L37-L93].
  - Row detail [components/IncidentRowDetail.tsx:L83-L167]: severity select "Override" with "Auto ({severity})"; chip "overridden from {severity}"; field "Resolution notes"; buttons "Mark resolved", "Reopen"; resolved text "Resolved {time} by {user} - {notes}".
  - Errors: "Could not load incidents."; "The export could not be generated." [ErrorMonitoringPage.tsx:L27, L51].
  - Endpoints: GET `/error-monitoring/incidents`, `/incidents/{id}`, `/incidents/export`; POST `/incidents/{id}/resolve`, `/incidents/{id}/reopen`, `/error-logs/{id}/severity-override` [src: frontend/src/modules/errorMonitoring/services/ErrorMonitoringService.ts:L36-L61].

### MOD-AUDIT-001 Audit Trail (Audit Search)
- SCR-AUDIT-001 Audit Trail ("Audit Search"). Route `/audit-search`; guard `Audit.View` [src: frontend/src/routes/AppRoutes.tsx:L213-L214; frontend/src/modules/auditSearch/AuditSearchPage.tsx:L27].
  - Title "Audit Trail"; subtitle "Search and trace immutable change events, actor identities, and laboratory records across MicroLIMS." [AuditSearchPage.tsx:L90-L91].
  - Filters [components/AuditFilterBar.tsx:L73-L231]: "From Date"; "To Date"; "Action" (All Actions, Create, Update, Delete); "Entity Category" (All Entities + entity names as "{label} ({key})"); collapsible laboratory identifiers "Sample Reference" (e.g. FP0107026), "Batch Number", "Control Number", "Media Lot Number", "Cryovial / RS Code", "User ID"; toggle "More Laboratory Identifiers (Batch, Sample, Media...)" / "Hide Laboratory Identifiers"; buttons "Reset", "Search Audit Trail" / "Searching..."; Enter submits.
  - Results heading "Audit Events" or "Audit Events (N)", with ", capped at 300 latest" when N >= 300 [AuditSearchPage.tsx:L108-L114].
  - Table columns "Date / Time", "User", "Entity", "Action", "Changed Fields (Diff)", "Record Ref", "Details"; empty "No audit events found"; action chip uppercase; row button "Inspect Event & Traceability Chain" [components/AuditResultsTable.tsx:L54-L205].
  - Drawer [components/AuditEventDrawer.tsx:L36-L187]: close aria "Close audit event details"; chip (uppercase action); diff viewer; raw JSON viewer; traceability chain; button "View Complete History for {entityName} #{entityId}".
  - Error: "Failed to fetch audit records from the server." [AuditSearchPage.tsx:L72].
  - Endpoints: POST `/admin/audit/search`; GET `/admin/audit?entityName=&entityId=`; GET `/admin/audit/{id}/traceability`; GET `/admin/audit/traceability?entityName=&entityId=` [src: frontend/src/modules/auditSearch/services/AuditSearchService.ts:L7-L19].
  - Export: none found on this screen (no export control in `frontend/src/modules/auditSearch`).

### MOD-AUDIT-002 Login history
- No screen. Endpoint `GET /api/admin/audit/login-history` (class policy `Audit.View`) [src: backend/MicroLIMS.API/Controllers/AuditController.cs:L11, L51].

### MOD-SECAUDIT-001 Security audit events
- No screen, no endpoint for viewing [src: knowledge/_raw/verified-roles-permissions.md §1].

### MOD-SIG-002 Signature trail
- No screen. Endpoint `GET /api/signatures` (`Signatures.Manage`) [src: backend/MicroLIMS.API/Controllers/SignaturesController.cs:L15, L25].

### MOD-ARCHIVE-001 Archived records
- No own screen. The sample report (`/samples/:id/report`, owned by C3) shows an "archived copies" block when copies exist: text "Immutable PDFs frozen at each final decision - what this record looked like when it was issued, not this live view." Each row shows the reason and a download [src: frontend/src/modules/testingWorkspace/reportPrimitives.tsx:L159-L190; frontend/src/modules/testingWorkspace/SampleReportPage.tsx:L112-L120].
- Load error shown as an alert instead of hiding the section [reportPrimitives.tsx:L168-L173].
- Download: integrity header `X-Archive-Integrity`; when "FAILED", toast "Warning: this archived copy failed its integrity check - the stored file no longer matches the hash recorded when it was signed. Do not treat it as authoritative; report this to a System Administrator." (10 s) [src: frontend/src/modules/testingWorkspace/services/ArchivedRecordsService.ts:L19-L35].
- Endpoints: GET `/archived-records?entityType=&entityId=`; GET `/archived-records/{id}/download` [src: ArchivedRecordsService.ts:L22-L26; backend/MicroLIMS.API/Controllers/ArchivedRecordsController.cs:L26-L49].

### MOD-LEGACY-001 Legacy and redirect routes
- No screen. Redirect table in business-rules (BR-NAV-004).

## roles

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

## workflows

Phase 4 reconstruction from code (application not run). Intended-process source: only `documents/DesignSpecification/BusinessRules.md` §10-§12 exist for this cluster; no spec describes the screens, user management, roles, messages or discussions, so their intended workflow is REQUIRES CONFIRMATION. Step lines read: user action -> system response -> validation -> status change. Next action, Review and Approval do not apply to this cluster unless stated (no signature is taken anywhere in it; none of these actions is e-signed: [src: knowledge/_raw/verified-roles-permissions.md §6]).

### WF-AUTH-001 Sign in
- **Start / trigger:** any person with an account opens `/login` (a signed-in person is sent to `/dashboard`) [src: frontend/src/routes/PublicRoutes.tsx:L6-L10].
- **Prerequisites:** an active account created by an administrator. Lab membership is not needed to sign in [src: backend/MicroLIMS.Application/Services/AuthenticationService.cs:L60-L137].
- **Steps:**
  1. Type "Username" and "Password" (both required; "Show password" toggle) -> fields filled.
  2. Click "Sign In" (shows "Signing in…") -> POST `/api/auth/login`, limited to 20 attempts per 60 s per IP [src: frontend/src/pages/Login.tsx:L133-L186; backend/MicroLIMS.API/Extensions/RateLimitingExtensions.cs:L33-L34].
  3. Server checks: unknown username -> login history "User not found"; locked account -> "Account locked" (counter unchanged); disabled account or wrong password -> counter +1, at 5 the account is locked for 15 min and every refresh token is revoked; otherwise success [src: AuthenticationService.cs:L80-L137].
  4. Success -> counter and lock cleared, Last Login stored, access token (15 min default) and refresh token (7 days) returned with the Must Change Password flag; the browser stores them and opens `/dashboard` [src: AuthenticationService.cs:L136-L155; frontend/src/pages/Login.tsx:L33-L37].
  5. If Must Change Password is set, every page is replaced by `/change-password` (WF-AUTH-004) [src: frontend/src/layouts/MainLayout.tsx:L63-L67].
- **Status change:** account lock state: Active -> "Account Locked" after the fifth failure (automatic, no user actor); failure counter is NOT reset when the lock time passes, so the first wrong password after expiry locks again (BR-AUTH-004).
- **Required data:** Username, Password. **Optional:** none.
- **Permissions:** none; anonymous endpoint.
- **Blocking conditions:** locked account (15 min, or until an administrator unlocks it, WF-USERS-001); disabled account; rate limit (refusal text REQUIRES CONFIRMATION).
- **Error messages (exact):** server for every failure "Invalid username or password."; client 401 "Invalid username or password."; 500 or more "Laboratory authentication service error. Please try again later."; no reply "Unable to reach the laboratory server. Check network connection."; other "Login failed. Please check credentials." [src: frontend/src/pages/Login.tsx:L39-L47].
- **Signature points:** none.
- **Audit:** login history row for every outcome with IP; security events AUTH_LOGIN_*, AUTH_REFRESH_TOKEN_ISSUED, AUTH_ACCOUNT_LOCKED; none visible in the application (GAP-030).
- **INTENDED source:** lockout, token rotation and login history match `BusinessRules.md:L192-L202` (INTENDED, consistent). The screen flow is REQUIRES CONFIRMATION.
- **Final state:** signed in, dashboard open.

### WF-AUTH-002 Request password reset
- **Start / trigger:** a person who cannot sign in clicks "Forgot password?" on `/login` [src: frontend/src/pages/Login.tsx:L171-L186].
- **Prerequisites:** none on screen; delivery needs an e-mail address on the account and a configured mail server.
- **Steps:**
  1. Type "Username" -> field filled.
  2. Click "Send Reset Link" ("Submitting...") -> POST `/api/auth/password-reset/request` [src: frontend/src/pages/Login.tsx:L58-L62; backend/MicroLIMS.API/Controllers/AuthenticationController.cs:L54-L69].
  3. Server creates a one-hour single-use token (stores only its hash) and, if the account has an e-mail, sends a message whose body contains the raw token text (not a link); with no e-mail it only logs a warning [src: AuthenticationService.cs:L241-L267].
  4. The screen always shows "If that account exists, a reset link has been generated." whatever happened; errors are swallowed.
  5. Completing the reset: no screen calls the confirm endpoint `POST /api/auth/password-reset/confirm`. REQUIRES CONFIRMATION how a person finishes (GAP-100). The working path for a user without access is WF-AUTH-003.
- **Status change:** none visible.
- **Required data:** Username. **Optional:** none. **Permissions:** none (anonymous, 20 per 60 s per IP).
- **Blocking conditions:** no e-mail on file or no mail server: nothing is delivered and nothing tells the user.
- **Error messages:** server confirm endpoint "Reset token is invalid or expired." (no UI caller); policy and history messages as in WF-AUTH-004.
- **Signature points:** none. **Audit:** none in the audit trail for the request; a successful confirm would write security event AUTH_PASSWORD_RESET and end all sessions.
- **INTENDED source:** `BusinessRules.md:L197-L200` (single use, 1 hour, no existence leak) for the token only; the user-facing steps are REQUIRES CONFIRMATION. DISCREPANCY: screen text says "reset link" while the e-mail carries a token (GAP-100).
- **Final state:** generic confirmation text; password unchanged.

### WF-AUTH-003 Admin-assisted password recovery
- **Start / trigger:** a user cannot sign in and cannot use e-mail reset; an administrator starts recovery, then the user finishes it.
- **Prerequisites:** administrator holds `Users.Manage`; the target account is active and is not the administrator's own [src: AdminPasswordRecoveryService.cs:L48-L60].
- **Steps (administrator, Users page):**
  1. Click the row action "Admin-Assisted Password Recovery" -> dialog opens [src: frontend/src/modules/users/UsersPage.tsx:L426-L482, L630-L700].
  2. Type "Reason for Recovery (Mandatory)" -> "Generate Recovery Code" becomes enabled.
  3. Click "Generate Recovery Code" -> POST `/api/users/{id}/admin-password-recovery`; any earlier pending code of that user becomes Expired; a 12-character code (shown as XXXX-XXXX-XXXX) valid 15 min is shown once; audit ADMIN_PASSWORD_RECOVERY_REQUESTED with the reason; toast "Recovery code generated for {username}."
  4. "Copy Code" (toast "Recovery code copied to clipboard.") and "Close & Done"; the chip reads "Expires in 15 minutes".
  5. Passing the code to the user: no screen or rule defines it. REQUIRES CONFIRMATION.
- **Steps (user):**
  6. On `/login` click "Admin-Assisted Password Recovery" (or "Have a recovery code from your admin?") -> `/admin-recovery` [src: frontend/src/pages/AdminPasswordRecovery.tsx:L57-L138].
  7. Fill "Username", "One-Time Recovery Code", "New Password", "Confirm New Password" (all required) and click "Submit Password Recovery" ("Resetting Password...").
  8. Server: code matches, not expired, fewer than 5 failures, new password meets policy and is not one of the last 5 -> password replaced, Must Change Password off, failure counter and lock cleared, all sessions revoked, code status Used, audit ADMIN_PASSWORD_RECOVERY_USED [src: AdminPasswordRecoveryService.cs:L106-L249].
  9. Screen shows "Your password has been successfully reset! You can now log in using your new password." and "Go to Login Page".
- **Status change:** recovery code Pending -> Used / Expired / FailedLimitExceeded (see statuses).
- **Required data:** reason (administrator); the four fields (user). **Optional:** none.
- **Permissions:** `Users.Manage` to generate; none to use the code (anonymous).
- **Blocking conditions / errors (exact):** own account "System Administrators cannot initiate admin-assisted password recovery for their own account."; empty reason "A reason is required for administrator-assisted password recovery."; disabled account "Cannot initiate password recovery for a disabled user account. Please enable the account first."; client "All fields are required." and "New password and confirm password do not match."; server "Invalid or expired recovery code." (unknown user, disabled user, no pending code), "Recovery code has expired. Please request a new recovery code.", "Invalid recovery code." (attempts 1-4), "Maximum failed recovery attempts exceeded. Recovery code invalidated." (5th), policy messages, "New password must not match any of your last 5 passwords."; fallback "Could not perform password recovery. Please verify your recovery code.".
- **Signature points:** none (reason typed only).
- **INTENDED source:** none. REQUIRES CONFIRMATION for the whole procedure.
- **Final state:** password changed; user signs in with it (WF-AUTH-001).

### WF-AUTH-004 Change password (including forced change)
- **Start / trigger:** a signed-in user opens account menu -> "Change Password", or is redirected there because Must Change Password is set (new account, or administrator forced it) [src: frontend/src/components/Header.tsx:L339-L363; frontend/src/layouts/MainLayout.tsx:L63-L67].
- **Prerequisites:** signed in; no permission needed. While the flag is set the server refuses all other calls with 403 "Your password must be changed before you can use MicroLIMS." [src: backend/MicroLIMS.API/Filters/MustChangePasswordFilter.cs:L65-L79].
- **Steps:**
  1. Fill "Current Password", "New Password", "Confirm New Password"; the checklist "Password must contain:" shows the five rules live [src: frontend/src/pages/ChangePassword.tsx:L13-L17, L71-L86].
  2. Click "Change Password" ("Updating Credentials...") -> client blocks with "New password does not meet the policy requirements below." or "New password and confirmation do not match." -> POST `/api/auth/change-password`, limited to 5 per 900 s per user.
  3. Server verifies the current password (wrong: 400 "Current password is incorrect." and security event AUTH_PASSWORD_CHANGE_FAILED), then policy and last-5 history, stores the hash, sets Password Last Changed, clears Must Change Password, revokes all refresh tokens [src: AuthenticationService.cs:L212-L234, L302-L338].
  4. Screen shows "Password changed successfully. Returning to your profile..." and opens `/profile` after 1.5 s. Whether the person must sign in again afterwards was not verified (GAP-113).
- **Status change:** Must Change Password chip removed from the Users list.
- **Required data:** all three fields. **Optional:** none. **Permissions:** none.
- **Blocking conditions:** rate limit; Must Change Password set (only this page, profile read and sign-out work).
- **Error messages (exact):** server policy texts "Password must be at least 8 characters long.", "Password must contain at least one uppercase letter.", "...lowercase letter.", "...one digit.", "Password must contain at least one special (non-alphanumeric) character." (joined by spaces); "New password must not match any of your last 5 passwords."; fallback "Could not change password.".
- **Signature points:** none. **Audit:** security events AUTH_PASSWORD_CHANGED, AUTH_SESSION_REVOKED.
- **INTENDED source:** none for the screen. Periodic expiry: none exists in the code (verified-roles §9); intent REQUIRES CONFIRMATION although the page subtitle mentions "periodic security policy".
- **Final state:** new password active; flag cleared.

### WF-AUTH-005 Session timeout and sign-out
- **Start / trigger:** idle browser, or the person chooses "Sign Out" in the account menu.
- **Steps (idle):**
  1. No mouse move, key press, click or scroll for 20 minutes -> dialog "Session Timeout Warning": "You will be signed out due to inactivity in {n} second(s)." with a 60 s countdown; Escape and outside click do not close it [src: frontend/src/hooks/useIdleTimeout.ts:L6-L8; frontend/src/layouts/MainLayout.tsx:L153-L170].
  2. Click "Stay signed in" (or any activity event) -> timer restarts; at zero -> automatic sign-out and `/login`.
  3. Behind the scenes the access token (15 min) is renewed with the refresh token while the person is active; the server has no idle timeout [src: verified-roles §8].
- **Steps (sign out):** click "Sign Out" -> no confirmation; POST `/api/auth/logout` with the refresh token (best effort), local session cleared, `/login` opens. The server revokes that one session (all sessions if the token is not identified) and writes AUTH_LOGOUT [src: AuthenticationService.cs:L342-L375; frontend/src/contexts/AuthContext.tsx:L137-L152].
- **Permissions:** none. **Error messages:** none shown; a failed logout call is ignored.
- **Signature points:** none. **INTENDED source:** none in specs; the idle timeout wording in code comment only. REQUIRES CONFIRMATION. Unsaved form data on the page is lost at timeout (not verified per screen).
- **Final state:** signed out, `/login`.

### WF-USERS-001 Manage user accounts
- **Start / trigger:** an administrator opens Users (`/users`).
- **Prerequisites:** `Users.Manage` (default: System Administrator only); route guard sends others silently to `/dashboard` [src: frontend/src/routes/AppRoutes.tsx:L271-L272].
- **Steps (each is a separate action; every one is audited, none is signed):**
  1. Create: fill "Full Name", "Username", "Password", role "Select Role *" (required), "Email" (optional) -> "Create User" -> account created with Must Change Password on; toast `User "{username}" created successfully.` [src: UserService.cs:L103-L126].
  2. Edit Profile: "Edit Profile" icon -> change Full Name / Username / Email -> "Save Profile" -> audit USER_DATA_UPDATED (old and new values); toast "Profile updated for {username}.".
  3. Change Role: icon "Change Role" -> pick "New Role" and type "Reason for Change" (mandatory) -> "Change Role" -> audit USER_ROLE_CHANGED and security event AUTH_ROLE_CHANGED; existing sessions are not ended; if the role TYPE differs, the person's old token is refused on the next request ("Role has changed since the token was issued."), what the screen then shows REQUIRES CONFIRMATION; permission changes reach the person with the next access token [src: backend/MicroLIMS.API/Authorization/AccessTokenRevalidator.cs:L63-L65].
  4. Disable / Enable: "Disable Account" / "Enable Account" -> "Reason for Disabling" mandatory when disabling -> account cannot sign in; all sessions revoked; audit USER_DISABLED / USER_ENABLED.
  5. Unlock: shown only when "Account Locked" -> optional "Reason for Unlock (Optional)" -> "Unlock Account" -> counter and lock cleared; audit USER_UNLOCKED.
  6. Reset Password via Email: "Initiate Email Password Reset" -> optional reason -> "Send Reset Link" -> token flow of WF-AUTH-002; toast "Password reset instructions initiated for {username}."; success reply even if no e-mail exists (GAP-100); audit ADMIN_PASSWORD_RESET_REQUESTED.
  7. Admin-assisted recovery: see WF-AUTH-003.
  8. Force Password Change: "Force Password Change at Next Login" -> confirm -> flag set; sessions are not ended; audit FORCE_PASSWORD_CHANGE_SET; toast "Forced password change set for {username}.".
  9. Permanently Delete: "Permanently Delete User" -> warning text -> "Permanently Delete" -> only if the user never signed in and has no activity anywhere; toast `User "{username}" was permanently deleted.`; audit USER_HARD_DELETED. Otherwise the dialog shows the refusal and the account should be disabled instead.
- **Status changes:** Active <-> Disabled; locked -> unlocked; Must Change Password on/off (see statuses).
- **Required vs optional:** create: Full Name, Username, Password, Role required, Email optional (helper "Password reset destination"); role change: role and reason; disable: reason; recovery: reason; unlock and reset: reason optional.
- **Blocking conditions / errors (exact):** "Full Name, Username, Password, and Role are required."; "Username '{username}' is already taken."; "Selected role does not exist."; "System Administrators cannot modify their own role."; "A reason is required to change a user's role."; "System Administrators cannot disable their own account."; "A reason is required to disable a user account."; "You cannot permanently delete your own account."; "Cannot delete the last active System Administrator account. Promote another user to System Administrator first."; "This user has activity history and cannot be permanently deleted. Deactivate instead."; "User {id} not found."; fallbacks "Could not create user.", "Could not update profile.", "Could not change role.", "Could not update account status.", "Could not unlock account.", "Could not initiate password reset.", "Could not generate recovery code.", "Could not force password change.", "Could not delete user.".
- **Signature points:** none; the typed reason is the only justification record.
- **INTENDED source:** none. REQUIRES CONFIRMATION, including who may hold `Users.Manage` beyond the System Administrator default and the account-creation approval process.
- **Final state:** per action; the Users table refreshes.

### WF-USERS-002 Assign laboratory membership
- **Start / trigger:** administrator clicks the row action "Laboratory Sections" on `/users`.
- **Prerequisites:** `Users.Manage`; active departments and sections exist.
- **Steps:**
  1. Dialog "Laboratory Sections: {Full Name}" loads sections and current memberships.
  2. Tick "{department}: All sections (entire department)" or individual sections; for the Physicochemical section choose "Area": "FP", "RM & PM" or "Both".
  3. Click "Save Memberships" ("Saving...") -> PUT `/api/org/users/{id}/memberships` replaces the whole list; every change is audited under the administrator by automatic capture [src: LaboratoryOrganizationService.cs:L58-L102].
  4. Success toast `Laboratory sections updated for "{username}".`, dialog closes.
- **Status change:** none shown. Effect: the user's dashboards, sidebar groups, queues and notifications follow the new sections; no membership means no laboratory data; System Administrator role type is unrestricted regardless [src: UserSectionScopeService.cs:L26-L77]. When the change reaches an already signed-in user was not verified.
- **Required vs optional:** none required (an empty list is a valid save: the user then sees no laboratory data - REQUIRES CONFIRMATION that this is intended); Area optional.
- **Errors (exact):** "One or more departments were not found or are inactive."; "Laboratory section {id} was not found or is inactive."; "Laboratory section {id} does not belong to department {id}."; "An area can only be set on a Physicochemical Laboratory membership."; load failure "Could not load laboratory sections or user memberships."; save fallback "Could not update user memberships.".
- **Signature points:** none. **INTENDED source:** none; lab separation design is outside the specs listed for this cluster. REQUIRES CONFIRMATION.
- **Final state:** memberships replaced.

### WF-ROLES-001 Create and edit roles and permissions
- **Start / trigger:** administrator opens Roles (`/roles`).
- **Prerequisites:** `Roles.Manage` (default: System Administrator only).
- **Steps (create):**
  1. "Create Role" -> wizard `/roles/new`: step "Name" ("Role Name" required, "Description"); "Next" is blocked while the name is empty.
  2. Step "Base Type": choose SystemAdministrator, SectionHead, Reviewer or Analyst (default Analyst); this decides dashboard type, lab-scope bypass and other type checks for ever.
  3. Step "Permissions": optional "Or clone permissions from any existing role:", then tick codes; chips "Enforced" / "Legacy-only" show which codes work.
  4. Step "Review" -> "Create Role" ("Creating...") -> two calls: create (role gets the grants of the base system role) then replace permissions with the ticked codes; if the second fails the role keeps the base grants (GAP-107) [src: CreateRolePage.tsx:L88-L89; RoleService.cs:L63-L89].
- **Steps (edit):** open a role -> change "Name" / "Description" and the permission matrix -> "Save Changes" ("Saving...") -> audit: automatic Update for the name, and ROLE_PERMISSIONS_UPDATED listing granted and revoked codes [src: RoleService.cs:L91-L106, L126-L171].
- **Steps (delete):** custom roles only: "Delete Role" -> confirm `Delete role "{name}"? This cannot be undone.` -> removed with its grants.
- **Status change:** role Active/Inactive chip and System/Custom chip (no UI to change them).
- **Required vs optional:** Name required; Description optional.
- **Blocking conditions / errors (exact):** "Role name is required."; "Unknown permission code(s): {codes}."; "System roles cannot be deleted."; "Role '{name}' is currently assigned to {n} user(s) and cannot be deleted."; "Role {id} not found."; fallback "Could not create this role.". Nothing stops removing `Roles.Manage` from the System Administrator or Section Head role.
- **Effect timing:** granted or revoked codes reach signed-in users with their next access token (up to 15 min) [src: verified-roles §3].
- **Signature points:** none. **INTENDED source:** none. REQUIRES CONFIRMATION (change-control expectations for role edits).
- **Final state:** role saved; Roles list refreshed.

### WF-AUDIT-001 Audit search and traceability chain
- **Start / trigger:** a user with `Audit.View` (default System Administrator, Section Head) opens Audit Trail (`/audit-search`) [src: frontend/src/routes/AppRoutes.tsx:L213-L214].
- **Steps:**
  1. Optionally set "From Date", "To Date", "Action" (All Actions, Create, Update, Delete), "Entity Category", and the laboratory identifiers (Sample Reference, Batch Number, Control Number, Media Lot Number, Cryovial / RS Code, User ID) behind "More Laboratory Identifiers (Batch, Sample, Media...)".
  2. Click "Search Audit Trail" (or Enter) -> POST `/api/admin/audit/search`; newest first, 300 rows at most, heading "Audit Events (N)" with ", capped at 300 latest" at 300 [src: AuditSearchService.cs:L58-L72; AuditSearchPage.tsx:L47, L108-L114].
  3. Read the table: "Date / Time", "User", "Entity", "Action", "Changed Fields (Diff)", "Record Ref", "Details".
  4. Click "Inspect Event & Traceability Chain" -> drawer with diff, raw JSON and the traceability chain for that record (sample, media, material, cryovial, equipment and item chains; any other entity shows one "General" node) [src: AuditTraceabilityService.cs:L186-L220].
  5. Click "View Complete History for {entity} #{id}" -> every audit row of that record, no cap.
  6. "Reset" clears filters.
- **Status change:** none (read-only). No export or print control exists.
- **Required vs optional:** all filters optional. **Permissions:** `Audit.View` on the whole controller; no lab scoping in the query.
- **Errors:** "Failed to fetch audit records from the server."; empty "No audit events found".
- **Limits worth knowing:** the Action filter cannot select platform actions such as USER_ROLE_CHANGED (GAP-101); login history and security events have no screen; some entities are never audited (GAP-111).
- **Signature points:** none. **INTENDED source:** capture rule `BusinessRules.md:L206-L218`; the search workflow itself is REQUIRES CONFIRMATION.
- **Final state:** results or history shown.

### WF-ERR-001 Review and resolve technical incidents
- **Start / trigger:** an administrator with `System.ViewErrorLog` (default System Administrator only) opens Error Monitoring (`/error-monitoring`).
- **Steps:**
  1. Optional filters: "Severity", "Source", "Status", "Active from", "Active to" (applied to Last seen), "Search summary or correlation id"; "Reset".
  2. The table lists incidents newest activity first (25 per page): Severity, Summary, Sources, Occurrences, First seen, Last seen, Status.
  3. Open a row: see each captured entry (type, message, stack, path, method, status code, user, time). Optional "Override" severity per entry ("Auto ({severity})" restores automatic) -> the incident severity is recomputed.
  4. Type "Resolution notes" (optional) and click "Mark resolved" -> status Resolved with resolver and time; or "Reopen" -> status Open (resolver and notes kept).
  5. "Export CSV" downloads up to 5,000 incidents for the current filters.
- **Status change:** Open <-> Resolved. A new error with the same correlation id reopens a Resolved incident automatically.
- **What fills the list:** unhandled errors, business-rule refusals (400), authorisation refusals (403), conflicts (409), database failures and browser crashes (BR-ERR-005, GAP-110). Not part of the GxP record; resolving writes no audit row.
- **Errors:** "Could not load incidents."; "The export could not be generated."; "Incident not found." (404).
- **Signature points:** none. **INTENDED source:** none. REQUIRES CONFIRMATION (triage process, who reviews, how often).
- **Final state:** incident Resolved or Open.

### WF-MSG-001 Send and read messages
- **Start / trigger:** any signed-in user opens Messages (`/messages`, account menu "Messages" / "Messages (N unread)"). `Messages.Use` has no effect.
- **Steps:**
  1. Click "New Message" -> dialog; choose "Direct Message (1-on-1)" or "Group Conversation".
  2. Group: type "Group Title" (required in the UI). Select recipient(s) with "Search colleagues by name..." (active users only) and type "Initial Message".
  3. Click "Send Message" ("Sending...") -> a direct conversation between the same two people is reused; each other participant gets a notification `New message from {sender}: "{first 80 characters}..."` [src: MessageService.cs:L211-L246, L428-L446].
  4. Open a conversation from the list ("Filter conversations..."), type in "Write a message... (Press Enter to send)" -> sent; the conversation is marked read (POST `/messages/conversations/{id}/read`) each time its messages are loaded in the chat window (corrected in Phase 5) [src: frontend/src/modules/messages/components/ChatWindow.tsx:L39-L45]; the unread count in the account menu is polled every 60 s while the page is visible.
- **Status change:** unread -> read per conversation.
- **Required data:** recipient(s), initial message, group title (UI). **Permissions:** signed in; only participants can read or post.
- **Errors (exact):** "Please select at least one recipient."; "Group conversation title is required."; "Initial message cannot be empty."; "Failed to create conversation."; "Failed to send message."; "Initial message is required."; "A conversation must have at least one other participant."; "Message content cannot be empty."; "You are not a participant in this conversation."; "The user directory could not be loaded, so no recipients can be listed. Close and reopen this dialog to try again.".
- **Signature points:** none; no audit row for messages. **INTENDED source:** none. REQUIRES CONFIRMATION (permitted use, retention; messages are not part of the lab record as far as the code shows).
- **Final state:** message stored and delivered to participants.

### WF-DISC-001 Post and discuss
- **Start / trigger:** any signed-in user opens Discussions (`/discussions`). `Discussions.View` and `Discussions.Create` have no effect.
- **Steps:**
  1. Browse: search "Search discussions by title or content...", toggle "Important Only", category chips (11 categories).
  2. "New Discussion" -> "Title" (required), "Category", optional "Mark as Important (Pinned at top)", "Content / Details" (required), optional attachments (max 5 files, 10 MB each, listed types, content checked against the extension) -> "Publish Discussion" ("Publishing...") [src: DiscussionService.cs:L297-L310; DiscussionAttachmentPolicy.cs:L14-L66].
  3. Open a post (`/discussions/:id`): write "Write a comment or response to this discussion..." -> "Post Comment"; edit or delete own comments; download attachments.
  4. Edit a post: "Edit Post" -> "Save Changes"; each save raises the version shown as "v{n} Edited" and keeps history; a stale edit is refused with the concurrency text (409).
  5. Mark / unmark important and delete (soft delete, confirm "Delete Post"): author, or a holder of `Discussions.EditAny` (default System Administrator, Section Head) [src: DiscussionService.cs:L372-L373, L416-L417, L430-L431, L538-L539].
- **Status change:** Important flag on/off; deleted flag (post hidden, not erased).
- **Errors (exact):** "Discussion title is required."; "Discussion content is required."; "Failed to create discussion."; "Failed to update discussion."; "Title is required."; "Content is required."; "Comment content cannot be empty."; "You are not authorized to edit this discussion post."; "You are not authorized to modify this discussion post."; "You are not authorized to delete this discussion post."; "You are not authorized to edit this comment."; attachment messages (count, empty, size, type, corrupted); toasts "Comment added", "Comment updated", "Comment deleted", "Discussion post deleted".
- **Signature points:** none. **Audit:** post and comment changes captured automatically; versions kept.
- **INTENDED source:** none. REQUIRES CONFIRMATION (whether discussions may be cited as GMP decisions: the categories "Internal Decisions", "Management Requirements", "GMP / Regulatory" suggest it, the code says nothing).
- **Final state:** post and comments stored.

### WF-NOTIF-001 Receive and read notifications
- **Start / trigger:** a signed-in person looks at the bell in the header (polled; stored list recomputed at most once per 5 min per user).
- **Steps:**
  1. System computes the person's notifications from current data (media expiring within 7 days or expired, completed incubations ready, samples awaiting review or approval, preparation configurations awaiting approval, tests returned to the analyst in the last 12 h), skips any whose exact text was stored in the last 12 h, stores new ones [src: DashboardNotificationService.cs:L88-L248].
  2. Click the bell (aria "Notifications", ", N unread") -> the 30 newest stored notifications; an item tied to a sample or test opens that record.
  3. Click an item -> POST `/api/dashboard/notifications/{id}/read`; or "read all" -> POST `/api/dashboard/notifications/read-all`.
  4. Dashboards show the same items filtered by laboratory; the analyst "Attention Required" panel hides ReviewWaiting and ApprovalWaiting.
- **Status change:** unread -> read (stored per notification; there is no way back).
- **Who sees what:** ApprovalWaiting and PendingPreparationConfigApproval: role type Section Head or System Administrator; ReviewWaiting: Reviewer, Section Head, System Administrator; TestReturnedForRevision: the assigned analyst; MediaExpiry and IncubationReady: anyone with the section in scope.
- **Errors:** "Notifications could not be loaded"; empty "No notifications" / "You are up to date - nothing needs your attention.".
- **Blocking conditions:** a new condition can appear up to 5 minutes late; no membership means no media or incubation notifications.
- **Signature points:** none. **Side effect:** an "error" notification (expired media) also triggers an e-mail to `{username}@microlims.local` (no-op without a mail server; GAP-104).
- **INTENDED source:** `BusinessRules.md:L222-L237` for triggers, 12 h de-duplication and e-mail. DISCREPANCY: review and approval triggers differ from the spec (GAP-103, confirmed).
- **Final state:** notification marked read.

## business-rules

### MOD-AUTH-001 Authentication, session, password
- BR-AUTH-001 Lockout: 5 failed attempts (wrong password, or inactive account) set `LockedUntil` to now + 15 min and revoke all refresh tokens. A sign-in attempt on an already locked account does not add to the counter or extend the lock. IMPLEMENTED (verified). [src: backend/MicroLIMS.Application/Services/AuthenticationService.cs:L20, L22, L95-L102, L104-L133; spec: documents/DesignSpecification/BusinessRules.md:L192-L194 INTENDED, consistent]
- BR-AUTH-002 A locked account is refused with the same generic message as a wrong password; the password is checked before the lock state (timing). IMPLEMENTED (verified). [src: AuthenticationService.cs:L90-L102]
- BR-AUTH-003 Successful login clears the failure counter and the lock and stores Last Login. IMPLEMENTED (verified). [src: AuthenticationService.cs:L136-L139]
- BR-AUTH-004 The failure counter is NOT cleared when a lock expires; only success, admin unlock or admin recovery clears it. IMPLEMENTED (verified); consequence: after the 15 minutes pass, one wrong password makes the counter 6 and locks again for 15 min. Intent REQUIRES CONFIRMATION (GAP-012). [src: AuthenticationService.cs:L106, L114, L136; UserService.cs:L275-L276; AdminPasswordRecoveryService.cs:L220-L221]
- BR-AUTH-005 Access token default 15 min (allowed 1-480); refresh token 7 days, rotated on every refresh (the refresh is refused for a disabled or locked account). IMPLEMENTED (verified). [src: backend/MicroLIMS.API/Extensions/JwtConfiguration.cs:L36-L37, L68-L72; AuthenticationService.cs:L23, L185-L205]. Spec §10 INTENDED covers only the refresh-token rotation and 7-day life (BusinessRules.md:L195-L196); the 15-minute access-token life is not in the spec (REQUIRES CONFIRMATION).
- BR-AUTH-006 Password policy: at least 8 characters, one uppercase, one lowercase, one digit, one non-alphanumeric; no maximum length. Applied to create user, change password, reset and admin recovery. IMPLEMENTED (verified). [src: backend/MicroLIMS.Shared/Validation/PasswordPolicy.cs:L8-L24]
- BR-AUTH-007 Password history: the new password may not match the last 5 stored hashes (change and e-mail reset); administrator-assisted recovery applies the same check against the 5 newest history rows. IMPLEMENTED (verified). [src: AuthenticationService.cs:L21, L308-L323; AdminPasswordRecoveryService.cs:L202-L214]
- BR-AUTH-008 Change or e-mail reset of the password revokes every refresh token of that user and writes security event AUTH_SESSION_REVOKED. Administrator-assisted recovery also revokes all refresh tokens (no security event). IMPLEMENTED (verified). [src: AuthenticationService.cs:L325-L337; AdminPasswordRecoveryService.cs:L228-L235]
- BR-AUTH-009 Self-service change requires the current password; wrong current password returns 400 "Current password is incorrect." and writes PASSWORD_CHANGE_FAILED security event. IMPLEMENTED (verified). [src: AuthenticationService.cs:L212-L223; AuthenticationController.cs:L96-L104]
- BR-AUTH-010 E-mail reset token: 32 random bytes, only the hash stored, 1 hour lifetime, single use. Request for unknown username returns the same generic message (the service throws, the controller swallows it; no existence leak). IMPLEMENTED (verified). [src: AuthenticationService.cs:L24, L241-L267, L269-L287; AuthenticationController.cs:L54-L69]. Spec §10 INTENDED consistent (BusinessRules.md:L197-L200).
- BR-AUTH-011 If the user has no e-mail on file the reset token is still created but cannot be delivered (server warning only). IMPLEMENTED (verified). [src: AuthenticationService.cs:L256-L260]
- BR-AUTH-012 The reset e-mail body contains the raw token ("Reset token: ..."), not a link. IMPLEMENTED (verified). [src: AuthenticationService.cs:L262-L266]. See GAP-100.
- BR-AUTH-013 Administrator-assisted recovery: code of 12 characters from a 32-character alphabet without look-alike characters; valid 15 minutes; one pending code per user (earlier pending codes expire); reason mandatory; cannot be started for own account or a disabled account; 5 failed entries invalidate the code. The entered code is compared as a hash, ignoring dashes, spaces and case. Unknown username, disabled account and 'no pending code' all return "Invalid or expired recovery code.". Success clears the failure counter and any lock, turns Must Change Password off and revokes all sessions. IMPLEMENTED (verified). [src: backend/MicroLIMS.Application/Services/AdminPasswordRecoveryService.cs:L17, L40-L46, L48-L102, L106-L249]
- BR-AUTH-014 Login attempts (all outcomes, with reason) are written to login history. IMPLEMENTED (verified). [src: AuthenticationService.cs:L85, L100, L132, L140]. Spec §10 INTENDED consistent (BusinessRules.md:L201-L202).
- BR-AUTH-015 19 security event codes (AUTH_*) are written for login, refresh, logout, session revoke, password, account and role events. IMPLEMENTED (code count not re-verified in this pass; written at the sites cited under audit-trail); no viewer (GAP-030). [src: backend/MicroLIMS.Shared/Constants/SecurityEventCodes.cs:L12-L38]
- BR-AUTH-016 Forced password change: new accounts and admin-forced users must change password at next sign-in; the UI redirects every page to `/change-password` until done. CORRECTION (extractor implied UI-only): the server also enforces it. While the flag is set every authenticated API call except change-password, `/auth/me` and logout is refused with HTTP 403 "Your password must be changed before you can use MicroLIMS.". The flag is read from the database on each request, not from the token. Setting the flag does not end existing sessions. IMPLEMENTED (verified). [src: UserService.cs:L120, L327-L328; AuthenticationService.cs:L319; backend/MicroLIMS.API/Filters/MustChangePasswordFilter.cs:L21-L24, L36-L37, L65-L79; frontend/src/layouts/MainLayout.tsx:L63-L67]
- BR-AUTH-017 Idle timeout is browser-side only: 20 min idle, 60 s countdown, then logout; activity = mousemove, keydown, click, scroll. Server has no idle timeout. Any activity event during the 60 s countdown also cancels the warning (not only the button). IMPLEMENTED (verified). [src: frontend/src/hooks/useIdleTimeout.ts:L6-L8, L41-L54; knowledge/_raw/verified-roles-permissions.md §8] See GAP-053.
- BR-AUTH-018 Token claims: role claim = role TYPE; one `permission` claim per granted code. Permission changes reach a user at the next access token; role type change, disable, lock and password change (token issued before the change) are checked on each request. IMPLEMENTED (verified). [src: backend/MicroLIMS.Infrastructure/Authentication/JwtTokenService.cs:L36-L43; backend/MicroLIMS.API/Authorization/AccessTokenRevalidator.cs:L55-L72]
- BR-AUTH-019 Rate limits: sign-in and reset endpoints 20 per 60 s per IP; refresh 60 per 60 s per IP; password change 5 per 900 s per user (defaults; configurable). IMPLEMENTED (sign-in default verified here; others per verified-roles §9). The user-facing text of a rate-limit refusal was not read: REQUIRES CONFIRMATION. [src: backend/MicroLIMS.API/Extensions/RateLimitingExtensions.cs:L33-L34, L54-L80]

### MOD-NAV-001 Application shell and navigation
- BR-NAV-001 Sidebar hides any item the user lacks the permission for; the page route is guarded too; the server checks every API call. IMPLEMENTED. [src: frontend/src/routes/menuConfig.ts:L216-L266; frontend/src/routes/PermissionRoute.tsx:L9-L12]
- BR-NAV-002 Lab groups in the sidebar follow membership; System Administrator sees both labs and both physchem areas. IMPLEMENTED. [src: frontend/src/hooks/useMyLabs.ts:L11-L66; frontend/src/routes/menuConfig.ts:L255-L260]
- BR-NAV-003 Pages with no route guard (Messages, Discussions, Document Control dashboard, report pages, sample report, CoA) are reachable by URL; their data is still server-checked. IMPLEMENTED. [src: frontend/src/routes/AppRoutes.tsx:L102-L114, L246-L251]
- BR-NAV-004 Redirects: `/` -> `/dashboard`; `/receiving-testing` -> first lab workspace (MICRO first, then FP areas), query kept; `/testing-workspace` -> `/receiving-testing`; `/laboratory-configuration/cause-of-testing` -> `/laboratory-configuration/receiving-configuration`; `/inventory/approved-media` -> `/laboratory-configuration/media?tab=approved`; `/inventory/approved-cryovials` -> `/laboratory-configuration/cryovials?tab=approved`; `/groups/:slug` = `/sections/:slug`. IMPLEMENTED. [src: frontend/src/routes/AppRoutes.tsx:L108, L117, L133, L151, L235, L261-L262; frontend/src/modules/receivingTesting/LabWorkspaceRoute.tsx:L128-L141]
- BR-NAV-005 Permission failure on a route: silent redirect to `/dashboard`, no message. IMPLEMENTED. [src: frontend/src/routes/PermissionRoute.tsx:L12]
- BR-NAV-006 Sign out: no confirmation; clears session and goes to `/login`. IMPLEMENTED. [src: frontend/src/components/Header.tsx:L103-L108]
- BR-NAV-007 Theme: stored choice (localStorage `microlims-theme-mode`); if none, the operating-system dark preference is used and not saved. IMPLEMENTED. [src: frontend/src/theme/ThemeModeContext.tsx:L8, L19, L34, L48]
- BR-NAV-008 Sidebar collapsed state stored (localStorage `microlims_sidebar_collapsed`). IMPLEMENTED. [src: frontend/src/layouts/MainLayout.tsx:L35-L43]

### MOD-NOTIF-001 Notifications
- BR-NOTIF-001 MediaExpiry: active media whose expiry is within 7 days or already past; "error" severity when already expired, otherwise "warning". Message: "{material} (Lot {lot}) has expired." or "... expires {dd-MMM-yyyy}." IMPLEMENTED. [src: backend/MicroLIMS.Application/Services/DashboardNotificationService.cs:L22, L128-L144] Spec §12 INTENDED consistent.
- BR-NOTIF-002 IncubationReady (info): completed incubations on active tests in accessible sections (step Incubating or Running; status Pending or InProgress; not superseded), newest 20. Message "{testCode} ({step}) for {sample or test order} is ready." IMPLEMENTED. [src: DashboardNotificationService.cs:L146-L170]
- BR-NOTIF-003 ApprovalWaiting (info; SectionHead and SystemAdministrator): count of distinct samples with a section in sign-off UnderApproval (or without a sign-off row and sample status UnderApproval) in the user's sections. "{n} sample(s) awaiting approval." The dashboard tiles count (sample, section) pairs, so a sample waiting in two sections counts once here and twice on a tile. The analyst Attention Required panel hides this type. IMPLEMENTED (verified). [src: DashboardNotificationService.cs:L172-L176]. DISCREPANCY vs spec, see BR-NOTIF-010.
- BR-NOTIF-004 PendingPreparationConfigApproval (info; SectionHead, SystemAdministrator): "{n} preparation configuration(s) awaiting approval." IMPLEMENTED. [src: DashboardNotificationService.cs:L178-L183]
- BR-NOTIF-005 ReviewWaiting (info; Reviewer, SectionHead, SystemAdministrator): count of samples in section sign-off UnderReview. "{n} sample(s) awaiting review." IMPLEMENTED. [src: DashboardNotificationService.cs:L185-L190]. DISCREPANCY vs spec, see BR-NOTIF-010.
- BR-NOTIF-006 TestReturnedForRevision (warning; the assigned analyst): test return events in the last 12 h. Message "Test {code} for sample {ref} was returned for revision." plus ": {reason}" when a reason is given. IMPLEMENTED. [src: DashboardNotificationService.cs:L192-L209]. A separate type TestReturnedForBiochemical is written by the pathogen recorder [src: backend/MicroLIMS.Application/Workflows/TestWorkflow/PathogenConfirmationRecorder.cs:L850].
- BR-NOTIF-007 Duplicate protection: a notification whose exact text was stored for the same user in the last 12 h is not stored again; because the count is in the text, a changed count gives a new notification. Each newly stored notification is also passed to an in-process push service; what the browser does with it was not traced (GAP-113). IMPLEMENTED (verified). [src: DashboardNotificationService.cs:L22-L23, L213-L236]. Spec §12 INTENDED consistent (BusinessRules.md:L233-L234).
- BR-NOTIF-008 Recompute: each user's notifications are recomputed at most once per 5 minutes; polls inside the window return the stored list, so a new notification can appear up to 5 minutes late. IMPLEMENTED. [src: backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs:L88; DashboardNotificationService.cs:L88-L100]
- BR-NOTIF-009 The bell shows the 30 newest stored notifications for the user; read state is stored per notification; "read all" marks all. IMPLEMENTED. [src: DashboardNotificationService.cs:L101-L107, L111-L125]
- BR-NOTIF-010 DISCREPANCY (spec vs code): spec §12 says Approval Waiting = TestOrder.Status Reviewed and Review Waiting = TestOrder.Status ResultEntered; code counts section sign-off statuses UnderApproval and UnderReview per section. CONFIRMED in Phase 3: the enum values Reviewed and ResultEntered still exist, so the spec text is not just a renamed status. INTENDED (spec) vs IMPLEMENTED (code). See GAP-103. [src: documents/DesignSpecification/BusinessRules.md:L222-L231; backend/MicroLIMS.Domain/Enums/ApprovalStatus.cs:L3-L9; DashboardNotificationService.cs:L171-L190]
- BR-NOTIF-011 Error-severity notifications: stored with `EmailSent` = true, and an e-mail is sent to `{username}@microlims.local` with subject "MicroLIMS Alert: {Type}" (no-op when SMTP is not configured). IMPLEMENTED (verified; `EmailSent` is set before the send). Recipient and flag meaning REQUIRES CONFIRMATION (GAP-104). In the current code only MediaExpiry of an already expired lot has severity "error". [src: DashboardNotificationService.cs:L143, L238-L244; spec: documents/DesignSpecification/BusinessRules.md:L235-L237 INTENDED]
- BR-NOTIF-012 Lab dashboards filter notifications by laboratory: a notification tied to a test order belongs to that order's section; MicroBiology-only types (MediaExpiry, IncubationReady, PendingPreparationConfigApproval, TestReturnedForBiochemical) show on the MICRO tab only; person-level notifications show on both. IMPLEMENTED. [src: DashboardNotificationService.cs:L49-L51, L57-L86]

### MOD-DASH-001 Dashboards
- BR-DASH-001 Administrator "Pending tests" = open test orders (Pending or InProgress) in scope; for Analyst role type only own assigned tests. IMPLEMENTED. [src: backend/MicroLIMS.Application/Services/DashboardService.cs:L194-L199]
- BR-DASH-002 Administrator "Overdue (>24h)" = open test orders on samples whose ReceivedAt is more than 24 h ago (receipt based; not assignment or review based). IMPLEMENTED. [src: DashboardService.cs:L153, L201-L205]
- BR-DASH-003 Administrator "Review queue" / "Approval queue" = (sample, section) pairs in section sign-off UnderReview / UnderApproval within scope. IMPLEMENTED. [src: DashboardService.cs:L209-L210; backend/MicroLIMS.Application/Services/SectionReviewQueues.cs]
- BR-DASH-004 Administrator "Prep configs pending" = item preparation configurations with ApprovalStatus PendingReview (auto-created from an analyst's first manual entry; still usable). Shown only when > 0. IMPLEMENTED. [src: DashboardService.cs:L215-L216; frontend/src/modules/dashboard/AdminDashboardPage.tsx:L75-L77]
- BR-DASH-005 Administrator "Total samples" and "Total tests" = all-time counts of samples and test orders in scope (not "today"). IMPLEMENTED; label meaning REQUIRES CONFIRMATION (GAP-106). [src: DashboardService.cs:L503-L525]
- BR-DASH-006 Reviewer "Pending review" = review-queue entries; "Overdue review" = entries whose age in minutes is >= 1440 (24 h); "Due today" = entries with SubmittedForReviewAt on or after today's start (caption "submitted today"); "Retests in progress" = samples with a retest in progress; "Completed today" = section sign-offs this reviewer reviewed since today's start. IMPLEMENTED. [src: DashboardService.cs:L850-L858]. See GAP-105.
- BR-DASH-007 Section Head "Active tests" = non-superseded open test orders; "Incubating" = open incubations on active tests not yet ready; "Ready to read" = open incubations meeting the ready rule (rule, verified: an open incubation on an active test is ready when its expected reading time has passed, or its incubation end time has passed, or a user overrode the minimum duration); "Pending review" / "Pending approval" = queue entries. IMPLEMENTED. [src: DashboardService.cs:L536-L554; backend/MicroLIMS.Application/Services/SampleWorkflowQueues.cs:L115-L121]
- BR-DASH-008 (verified; the test is KpiService.IsAnalystStageOverdue, L124-L128) Section Head "Overdue" = distinct samples still in analyst stage (test order Pending or InProgress) past the 7-day analyst-assignment SLA (deadline = AssignedAt + 7 days; or, when submitted for review, submission later than the deadline). Different definition from BR-DASH-002. IMPLEMENTED. [src: DashboardService.cs:L557-L579; backend/MicroLIMS.Application/Services/KpiService.cs:L19, L124-L129]. See GAP-102.
- BR-DASH-009 Section Head "Attention items" = Overdue + retests in progress + review-queue entries aged >= 24 h + approval-queue entries aged >= 24 h (the list shows only the 5 oldest retests, the count uses all). IMPLEMENTED (verified). [src: DashboardService.cs:L604, L613, L683]
- BR-DASH-010 Analyst "On-Time Reading Rate" = of the user's completed incubations that have an expected reading time (on the user's tests or started by the user), the share completed within 4 h after ExpectedReadingAt, one decimal; shown for Microbiology only; 100 when none. IMPLEMENTED (verified). [src: DashboardService.cs:L343, L385-L396; frontend/src/modules/dashboard/components/AnalystPerformancePanel.tsx:L17, L44]
- BR-DASH-011 Analyst "7-Day Completed Volume" = distinct completed results and readings entered in the last 7 days by the user; "Tests Completed" = today's completed results and readings; "Media Lots Prepared" = media lots prepared by the user since today's start; "Active Assigned Orders" = orders assigned to the user with status Pending, InProgress or RetestRequested. IMPLEMENTED (verified). [src: DashboardService.cs:L332-L383, L356-L368]
- BR-DASH-012 Dashboard data endpoints: Section Head needs `Dashboards.LabOverview`; Reviewer needs `Dashboards.Review`; summary and other endpoints have no policy. IMPLEMENTED. [src: DashboardController.cs:L43-L110]
- BR-DASH-013 Sample-level tile links (`?status=`, `?urgency=`, `?workload=`, `?view=`, `?testStatus=`) open the laboratory workspace with that filter. IMPLEMENTED (link targets only). [src: AdminDashboardPage.tsx:L72-L78; ReviewerDashboardPage.tsx:L111-L144; SectionHeadDashboardPage.tsx:L132-L144]

### MOD-USERS-001 User management
- BR-USERS-001 Create user: username, full name and role required; password must meet policy; username unique; role must exist; new account MustChangePassword = true. IMPLEMENTED. [src: backend/MicroLIMS.Application/Services/UserService.cs:L103-L126]
- BR-USERS-002 Edit profile: full name and username required; username unique; e-mail optional on server (UI helper says "Required for email password resets"). IMPLEMENTED; e-mail requirement REQUIRES CONFIRMATION. [src: UserService.cs:L128-L161; frontend/src/modules/users/UsersPage.tsx:L506]
- BR-USERS-003 Change role: reason required; own role cannot be changed; written to audit as USER_ROLE_CHANGED and to security events as AUTH_ROLE_CHANGED (from and to role names). Existing sessions are not ended. IMPLEMENTED (verified). The audit row's previous value holds the NEW role id with the old role name (the id is overwritten before it is serialised): GAP-112. [src: UserService.cs:L163-L204 (L178 overwrite, L195 serialise)]
- BR-USERS-004 Disable: reason required; self-disable refused; all refresh tokens of the user revoked; audit USER_DISABLED with reason. Enable: no reason; audit USER_ENABLED; enabling revokes nothing. IMPLEMENTED (verified). [src: UserService.cs:L206-L252]
- BR-USERS-005 Unlock: clears lock time and failure counter; reason optional; audit USER_UNLOCKED. IMPLEMENTED. [src: UserService.cs:L267-L293]
- BR-USERS-006 "Account Locked" = lock time in the future (computed). IMPLEMENTED. [src: backend/MicroLIMS.Domain/Entities/User.cs:L20]
- BR-USERS-007 Force password change: sets MustChangePassword; audit FORCE_PASSWORD_CHANGE_SET. IMPLEMENTED. [src: UserService.cs:L322-L345]
- BR-USERS-008 Reset password via e-mail: same token flow as the sign-in form; audit ADMIN_PASSWORD_RESET_REQUESTED with reason and HasEmail; the success message "Password reset instructions sent to user's email." is returned even when no e-mail is on file. IMPLEMENTED. [src: UserService.cs:L298-L320; backend/MicroLIMS.API/Controllers/UserController.cs:L110-L118]. See GAP-100.
- BR-USERS-009 Administrator-assisted recovery: audit ADMIN_PASSWORD_RECOVERY_REQUESTED / USED / FAILED / EXPIRED; see BR-AUTH-013. IMPLEMENTED. [src: AdminPasswordRecoveryService.cs:L93, L135-L241]
- BR-USERS-010 Permanent delete: refused for self; refused for last active System Administrator; refused when any activity reference exists (UserHasHistoryException: "Deactivate instead"); otherwise hard delete. A user who has ever signed in (Last Login set) counts as having history. Refusal is HTTP 409 with "This user has activity history and cannot be permanently deleted. Deactivate instead.". Success writes audit USER_HARD_DELETED (previous value: id, username, full name, e-mail, role); success toast `User "{username}" was permanently deleted.`; a refusal appears as a red alert inside the dialog (fallback "Could not delete user."). IMPLEMENTED (verified). [src: backend/MicroLIMS.Application/Services/UserDeletionService.cs:L37-L106; backend/MicroLIMS.API/Controllers/UserController.cs:L139-L152; frontend/src/modules/users/UsersPage.tsx:L275-L286, L735]
- BR-USERS-011 Directory (used for recipients and pickers): active users only, sorted by full name. IMPLEMENTED. [src: UserService.cs:L65-L72]
- BR-USERS-012 Analyst picker (assignment): active, not locked, role type Analyst only. IMPLEMENTED. [src: UserService.cs:L84-L94]
- BR-USERS-013 Laboratory memberships are replaced as a whole on save; department and section must be active; an area can be set only on a Physicochemical membership; a department-only membership grants every active section of that department; no membership means no laboratory data; System Administrator role type is unrestricted. IMPLEMENTED. [src: LaboratoryOrganizationService.cs:L61-L98; knowledge/_raw/verified-roles-permissions.md §7; backend/MicroLIMS.Application/Services/UserSectionScopeService.cs:L26-L77]
- BR-USERS-014 Laboratory Sections dialog (read in full): on open it loads all active sections and the user's memberships (failure: "Could not load laboratory sections or user memberships."); one checkbox per department "{dept} ({code}): All sections (entire department)" and one per section "{section} ({code})" (section boxes are ticked and disabled while the whole department is ticked); for the Physicochemical section (code FP) a select "Area" with "FP", "RM & PM", "Both" (Both is stored as no area); button "Save Memberships" / "Saving..." calls PUT `/org/users/{id}/memberships` (Users.Manage); success toast `Laboratory sections updated for "{username}".`; failure shows the server message (fallback "Could not update user memberships."); empty text "No laboratory sections are configured in the system.". When the user's sidebar changes after a save was not traced (REQUIRES CONFIRMATION). IMPLEMENTED (verified). [src: frontend/src/modules/users/dialogs/UserSectionsDialog.tsx:L73-L84, L159-L181, L204, L224-L226, L253, L274-L340; backend/MicroLIMS.API/Controllers/LaboratoryOrganizationController.cs:L35-L41; backend/MicroLIMS.Application/Services/LaboratoryOrganizationService.cs:L61-L102]

### MOD-ROLES-001 Roles and permissions
- BR-ROLES-001 Four system roles exist after seeding (System Administrator, Section Head, Reviewer, Analyst); default grants 50 / 43 / 15 / 15 permission codes. IMPLEMENTED (defaults; production rows REQUIRES CONFIRMATION, GAP-015). [src: backend/MicroLIMS.Persistence/Seed/DbSeeder.cs:L23-L32, L516-L576; knowledge/_raw/verified-roles-permissions.md §2]
- BR-ROLES-002 Any role can be renamed (system roles included) and its description changed. IMPLEMENTED. [src: backend/MicroLIMS.Application/Services/RoleService.cs:L91-L106]
- BR-ROLES-003 Any role's permission set can be replaced, system roles included; no guard prevents removing `Roles.Manage` from the System Administrator or Section Head set. IMPLEMENTED; intent REQUIRES CONFIRMATION. [src: RoleService.cs:L126-L171; knowledge/_raw/verified-roles-permissions.md §3]
- BR-ROLES-004 Create role: name required; base type is one of the four role types; the new role starts with the grants of the system role of that type; the wizard then replaces the grant set with the ticked codes in a second call. IMPLEMENTED. [src: RoleService.cs:L63-L89; frontend/src/modules/roles/CreateRolePage.tsx:L88-L89]. See GAP-107.
- BR-ROLES-005 Role name is required on save; base type is not changed by an edit. IMPLEMENTED. [src: RoleService.cs:L91-L106]
- BR-ROLES-006 Delete: system roles refused ("System roles cannot be deleted."); roles assigned to users refused; grant rows removed with the role. IMPLEMENTED. [src: RoleService.cs:L108-L124]
- BR-ROLES-007 Permission save: unknown codes refused; audit ROLE_PERMISSIONS_UPDATED records granted and revoked codes. IMPLEMENTED. [src: RoleService.cs:L126-L171]
- BR-ROLES-008 A permission marked Legacy-only (NOT-ENFORCED: 10 codes) has no effect; the other 40 are checked by endpoints. IMPLEMENTED. [src: backend/MicroLIMS.Shared/Constants/PermissionConstants.cs:L109-L143; frontend/src/modules/roles/components/PermissionMatrix.tsx:L70-L83]
- BR-ROLES-009 Role TYPE rules persist regardless of custom grants (see roles section). IMPLEMENTED. [src: knowledge/_raw/verified-roles-permissions.md §3]

### MOD-SIG-001 Electronic signatures and segregation of duties
- BR-SIG-001 A signature is the signer's own login password re-typed; the password is verified against the stored hash; an inactive or locked account is refused with the same message as a wrong password. IMPLEMENTED (verified). [src: backend/MicroLIMS.Application/Services/ElectronicSignatureService.cs:L65-L81]
- BR-SIG-002 Throttle: after 5 failed signature attempts in 15 minutes, further attempts are refused without checking the password; signature failures never lock the sign-in account. Each failed or refused attempt is written to the audit trail. IMPLEMENTED (verified). [src: ElectronicSignatureService.cs:L12-L18, L40-L81]
- BR-SIG-003 Each signature is an append-only record: user id, full name, username and role-type snapshots, meaning, entity type and id, time, optional comment, IP address. IMPLEMENTED. [src: backend/MicroLIMS.Domain/Entities/ElectronicSignature.cs:L4-L30; ElectronicSignatureService.cs:L83-L97]
- BR-SIG-004 The signature is written in the same transaction as the state change; a failed password stops the change. IMPLEMENTED. [src: backend/MicroLIMS.Application/Services/ReviewGateService.cs:L30-L37]
- BR-SIG-005 Meanings (enum SignatureMeaning, 17 values): Reviewed, Approved, Rejected, RetestRequested, InvestigationOrdered, PreparationConfirmed, SampleCorrected, SampleVoided, MasterDataChanged, SuitabilityRunPerformed, ResultRecorded, CalibrationRunPerformed (retired), CalibrationRunWithdrawn (retired), TestingClosed, LaboratoryAdded, TitrantStandardized, TitrantDueAcknowledged. IMPLEMENTED. [src: backend/MicroLIMS.Domain/Enums/SignatureMeaning.cs:L3]
- BR-SIG-006 Comment or reason: free text per action; required for some actions (corrections, voids, code change, add laboratory); not uniform. REQUIRES CONFIRMATION (GAP-027).
- BR-SIG-007 Segregation of duties: the guard compares user ids only and applies to review, approval, close and related decisions; no role bypasses it. IMPLEMENTED. [src: backend/MicroLIMS.Application/Services/SegregationOfDutiesGuard.cs:L6-L10]
- BR-SIG-008 "Performed a test" = assigned analyst of the test order, or preparer of the sample, or entered a result / count reading / analysis, or observed a pathogen step, or submitted a workflow step result, or recorded a confirmatory plate observation. IMPLEMENTED. [src: SegregationOfDutiesGuard.cs:L25-L54]

### MOD-MSG-001 Messages
- BR-MSG-001 A direct conversation between the same two users is reused: a new "direct" message sends into the existing conversation. IMPLEMENTED. [src: backend/MicroLIMS.Application/Services/MessageService.cs:L232-L246]
- BR-MSG-002 A conversation needs at least two participants; the creator is added automatically; the initial message is required. IMPLEMENTED. [src: MessageService.cs:L211-L227]
- BR-MSG-003 Only participants can read, post or mark read. IMPLEMENTED. [src: MessageService.cs:L155-L159, L302, L338-L342]
- BR-MSG-004 Empty message text is refused. IMPLEMENTED. [src: MessageService.cs:L333]
- BR-MSG-005 Each message stores a notification for every other participant: "New message from {sender}: "{first 80 characters}..."" (type DirectMessage, severity info). IMPLEMENTED. [src: MessageService.cs:L428-L446]
- BR-MSG-006 Group title: UI requires it; the server stores whatever title is sent (null allowed). REQUIRES CONFIRMATION (server-side check not found). [src: frontend/src/modules/messages/NewConversationDialog.tsx:L68-L76; MessageService.cs:L251]

### MOD-DISC-001 Discussions
- BR-DISC-001 A post needs a title and content; both are trimmed before save. IMPLEMENTED. [src: backend/MicroLIMS.Application/Services/DiscussionService.cs:L297-L299, L309-L310]
- BR-DISC-002 Attachments: at most 5 files; each at most 10 MB; extensions pdf, png, jpg, jpeg, gif, doc, xls, docx, xlsx, pptx, txt, csv; file signature must match the extension; checked before the post is created. IMPLEMENTED. [src: backend/MicroLIMS.Application/Services/DiscussionAttachmentPolicy.cs:L14-L66; DiscussionService.cs:L303-L305]
- BR-DISC-003 Only the author, or a holder of `Discussions.EditAny`, may edit, mark important, delete posts, or edit and delete comments. IMPLEMENTED. [src: DiscussionService.cs:L372-L373, L416-L417, L430-L431, L538-L539]
- BR-DISC-004 Edits are versioned (version counter shown as "v{n} Edited"); the edit sends the expected version; history is viewable. IMPLEMENTED. [src: DiscussionService.cs:L365-L369 (version check); DiscussionController.cs:L91-L112; frontend/src/modules/discussions/DiscussionDetailPage.tsx:L300-L303]
- BR-DISC-005 Feed: default page size 20, maximum 100; search is by title or content; excerpts are the first 200 characters. IMPLEMENTED. [src: DiscussionService.cs:L158, L180-L195]
- BR-DISC-006 Comments need content. IMPLEMENTED. [src: DiscussionService.cs:L487]
- BR-DISC-007 Posts are deleted by an IsDeleted flag (lookups filter deleted posts). IMPLEMENTED. [src: DiscussionService.cs:L366-L369]

### MOD-ERR-001 Error Monitoring
- BR-ERR-001 Incidents group error log entries; incident severity is the highest child severity (overrides included); changing an override recomputes the incident from its children. IMPLEMENTED. [src: backend/MicroLIMS.Application/Services/ErrorMonitoringService.cs:L133-L158]
- BR-ERR-002 Mark resolved: status Resolved, resolver, time, optional notes. A new error with the same correlation id reopens it automatically. IMPLEMENTED (verified). [src: ErrorMonitoringService.cs:L106-L119; ErrorCaptureService.cs:L121-L122]
- BR-ERR-003 Reopen: status Open; earlier resolver and notes are kept. IMPLEMENTED. [src: ErrorMonitoringService.cs:L121-L131]
- BR-ERR-004 List: newest last-seen first, 25 per page (maximum 200); the date filters apply to Last seen; search covers summary and correlation id. CSV export: at most 5,000 incidents; columns CorrelationId, Severity, Status, Sources, Occurrences, FirstSeenUtc, LastSeenUtc, Summary, ResolvedBy, ResolutionNotes. IMPLEMENTED. [src: ErrorMonitoringService.cs:L17, L160-L197]
- BR-ERR-005 Every exception that reaches the global handler is captured as an error log entry under an incident keyed by the request's correlation id (one incident per correlation id; later entries with the same id attach to it, update Last seen, REOPEN a Resolved incident, and the incident severity becomes the highest child severity). Stored per entry: source, severity, correlation id, exception type, message, full exception text including inner exceptions, request path, HTTP method, response status code, user id (if resolvable), time, and for database failures a raw-context text. Summary = short exception type plus "{METHOD} {path}" (max 500 characters). Capture never fails the request. Mapping (verified): business-rule and invalid-operation exceptions -> HTTP 400 with the message, Backend, Warning; not found -> 404 with the message, Warning; concurrency conflict -> 409 "This record was changed by someone else while you were working on it. Nothing was saved - reload it and make your change again.", Warning; database failure -> 500 "An unexpected error occurred.", Source Database, severity from a classifier; request rejected by the web server (for example oversized body) -> its own status, Warning; unauthorised-access exception -> 403 with the message, Warning; anything else -> 500 "An unexpected error occurred.", Critical. Browser crashes are reported anonymously to POST `/api/system/client-errors` (rate limited, 32 KB cap): Source Frontend, severity Critical for a render crash and Error otherwise. IMPLEMENTED (verified). [src: backend/MicroLIMS.API/Middleware/ExceptionMiddleware.cs:L50-L128, L133-L154; backend/MicroLIMS.Application/Services/ErrorCaptureService.cs:L38-L56, L84-L170; backend/MicroLIMS.API/Controllers/SystemController.cs:L44-L110]. See GAP-110.
- BR-ERR-006 Authorisation refusals (UnauthorizedAccessException) return HTTP 403 and are captured as Warning (for example the message and discussion participant checks). IMPLEMENTED (verified). [src: ExceptionMiddleware.cs:L116-L121]
- BR-ERR-007 Critical alert e-mails are off unless configured (default false). Not re-verified: no e-mail send was found in ExceptionMiddleware or ErrorCaptureService. REQUIRES CONFIRMATION. [src: 01_APPLICATION_MAP §8; GAP-047]

### MOD-AUDIT-001 Audit Trail
- BR-AUDIT-001 Every tracked Add, Modify and Delete is written on SaveChanges with action Create, Update or Delete, entity name, entity id, previous and new JSON values, user and UTC time. IMPLEMENTED. [src: backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs:L319-L337, L363]
- BR-AUDIT-002 Search returns the newest matches first; the server default cap is 200; the UI requests 300 and shows "capped at 300 latest" at 300 results. IMPLEMENTED. [src: backend/MicroLIMS.Application/Services/AuditSearchService.cs:L22, L72; frontend/src/modules/auditSearch/AuditSearchPage.tsx:L47, L113-L114]
- BR-AUDIT-003 Search filters (exact match except dates): from date, to date (to end of day, set in browser), batch number, control number, sample reference, media lot number, reference strain code, cryovial code, sample id, test order id, user id, entity name, action. IMPLEMENTED. [src: AuditSearchService.cs:L58-L70; AuditSearchPage.tsx:L47-L67]
- BR-AUDIT-004 The Action filter offers only Create, Update and Delete; the custom action names written by platform code (e.g. USER_ROLE_CHANGED, USER_DISABLED, ROLE_PERMISSIONS_UPDATED, ADMIN_PASSWORD_RECOVERY_USED) cannot be selected in the UI. IMPLEMENTED. [src: frontend/src/modules/auditSearch/components/AuditFilterBar.tsx:L93-L103]. See GAP-101.
- BR-AUDIT-005 Entity history: every entry for one entity, newest first, no cap. IMPLEMENTED. [src: AuditSearchService.cs:L76-L81]
- BR-AUDIT-006 Login history filters: username, latest N. IMPLEMENTED (endpoint only). [src: AuditSearchService.cs:L86-L91]
- BR-AUDIT-007 Role permission changes and user changes write custom audit actions (see audit-trail). IMPLEMENTED. [src: RoleService.cs:L162; UserService.cs:L152, L194, L287, L312, L334]
- BR-AUDIT-008 Archived PDF integrity: SHA-256 stored at creation; recomputed on every read; mismatch sends header X-Archive-Integrity: FAILED and logs an error. IMPLEMENTED. [src: backend/MicroLIMS.Application/Services/RecordArchiveService.cs:L62, L90-L101; backend/MicroLIMS.API/Controllers/ArchivedRecordsController.cs:L44-L48]
- BR-AUDIT-009 Spec §11 INTENDED: AuditLog and WorkflowHistory are append-only ("Nothing is ever hard-deleted"). Deletion paths for records that reference a user were not checked against this rule. REQUIRES CONFIRMATION. (corrected in Phase 5) Migration `20260902181230_AddAuditImmutabilityTriggers` creates database triggers that refuse UPDATE and DELETE on the tables AuditLogs, AuditEventChanges and ElectronicSignatures; that the triggers exist in the live database was not checked [src: backend/MicroLIMS.Persistence/Migrations/20260902181230_AddAuditImmutabilityTriggers.cs:L15-L32]. [src: documents/DesignSpecification/BusinessRules.md:L206-L218]

### MOD-ARCHIVE-001 Archived records
- BR-ARCHIVE-001 An archived copy is created at: sample approval and rejection (and OOS-resolved approval); media lot release decision; cryovial batch approval. The PDF is generated, stored and recorded with file name `{entityType}_{documentId}_{timestamp}.pdf`, reason text, and generated-by name snapshot. IMPLEMENTED. [src: RecordArchiveService.cs:L38-L80; SampleApprovalService.cs:L535, L639; MediaReleaseService.cs:L112; CryovialService.cs:L173]
- BR-ARCHIVE-002 Behaviour when PDF generation fails is inside a try block; the failure path was not read. REQUIRES CONFIRMATION. [src: RecordArchiveService.cs:L40-L46]

### MOD-LEGACY-001 Legacy and redirect routes
- BR-LEGACY-001 Retired items are listed under the redirect table (BR-NAV-004); retired enum values and retired calibration signature meanings remain in code. IMPLEMENTED. See GAP-054.

## validations

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

## statuses

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

## terminology

### MOD-AUTH-001 Authentication, session, password
- "Sign In" / "Forgot password?" / "Admin-Assisted Password Recovery": sign-in card actions [src: frontend/src/pages/Login.tsx:L171-L186].
- "One-Time Recovery Code": 12-character code an administrator generates for one user; valid 15 minutes [src: frontend/src/pages/AdminPasswordRecovery.tsx:L90-L97; frontend/src/modules/users/UsersPage.tsx:L692].
- "Must Change Password": account flag that forces the change page at next use [src: frontend/src/modules/users/UsersPage.tsx:L413].
- "Stay signed in": button on the idle-timeout warning [src: frontend/src/layouts/MainLayout.tsx:L164].

### MOD-USERS-001 Users and laboratory memberships
- "Role" (account): the named role assigned to a user [src: frontend/src/modules/users/UsersPage.tsx:L356-L359].
- "Role type" / "Base Type": one of four built-in types that a role is built on (SystemAdministrator, SectionHead, Reviewer, Analyst). Dashboard variant, scope bypass and some checks follow the type, not the custom name [src: backend/MicroLIMS.Domain/Enums/RoleType.cs:L3; frontend/src/modules/roles/CreateRolePage.tsx:L122-L129].
- "All sections (entire department)": membership option that covers all active sections of a department [src: frontend/src/modules/users/dialogs/UserSectionsDialog.tsx:L253].
- "Area": physchem membership sub-area, chosen as "FP" (finished product), "RM & PM" (raw and packaging material) or "Both" (stored as none); the backend returns `fp` / `rmpm` for the sidebar [src: frontend/src/modules/users/dialogs/UserSectionsDialog.tsx:L143-L157, L331-L339; backend/MicroLIMS.Application/Services/LaboratoryOrganizationService.cs:L14-L15, L47].
- "Reason for Change" / "Reason for Disabling" / "Reason for Unlock (Optional)" / "Reason for Reset (Optional)" / "Reason for Recovery (Mandatory)": administrative justification stored in the audit row [src: frontend/src/modules/users/UsersPage.tsx:L529, L553, L575, L599, L638].
- "Edit Profile" / "Laboratory Sections" / "Unlock Account" / "Force Password Change at Next Login" / "Permanently Delete User": row actions [src: UsersPage.tsx:L426-L482].
- "Account Locked": lock in effect; "Disabled": account cannot sign in [src: UsersPage.tsx:L405-L412].

### MOD-ROLES-001 Roles and permissions
- "Enforced": the endpoints covered by the code check it directly. "Legacy-only": access still follows the base role type [src: frontend/src/modules/roles/components/PermissionMatrix.tsx:L69-L83].
- "System Role" / "Custom": built-in four roles vs roles created by administrators; custom roles can be deleted when unassigned [src: frontend/src/modules/roles/RoleDetailPage.tsx:L139].
- "Base Type": role type chosen at creation; not changed by later edits [src: backend/MicroLIMS.Application/Services/RoleService.cs:L63-L89].
- "Clone permissions from any existing role": wizard option that copies a role's ticked codes into the new role [src: frontend/src/modules/roles/CreateRolePage.tsx:L144-L154].

### MOD-SIG-001 Electronic signatures
- "Electronic Signature": dialog title; the signer re-types the login password [src: frontend/src/components/SignatureDialog.tsx:L52].
- "Comment (optional)": optional free-text on the signature [src: SignatureDialog.tsx:L71].
- "Signing...": in-progress label on the Sign button [SignatureDialog.tsx:L57].
- SoD = segregation of duties: a person who performed a step may not perform the next decision on the same sample or record [src: backend/MicroLIMS.Application/Services/SegregationOfDutiesGuard.cs:L6-L10].

### MOD-DASH-001 Dashboards
- "Command Center": dashboard heading for the three role-type variants [src: AdminDashboardPage.tsx:L84; ReviewerDashboardPage.tsx:L84; SectionHeadDashboardPage.tsx:L106].
- "Overdue (>24h)", "Overdue review" (caption "in review >24 hours"), "Overdue" (Section Head): three different definitions; see BR-DASH-002, BR-DASH-006, BR-DASH-008 and GAP-102.
- "Due today" (Reviewer): caption "submitted today" [ReviewerDashboardPage.tsx:L129-L135].
- "Ready to read": incubation finished and read window reached [SectionHeadDashboardPage.tsx:L132-L144].
- "On-Time Reading Rate": share of incubations read within 4 h of expected reading time (Microbiology) [backend/MicroLIMS.Application/Services/DashboardService.cs:L386-L396].
- "Attention items" / "Attention Required": summary of overdue and retest items [DashboardService.cs:L683; AttentionRequiredPanel.tsx:L67].

### MOD-NOTIF-001 Notifications
- "Notifications" with "N unread": bell label [src: frontend/src/components/Header.tsx:L216].
- "Expiring" chip: media expiry item on the analyst attention panel [src: frontend/src/modules/dashboard/components/AttentionRequiredPanel.tsx:L107].

### MOD-MSG-001 Messages
- "Direct Message (1-on-1)" / "Group Conversation": conversation kinds [src: frontend/src/modules/messages/NewConversationDialog.tsx:L140, L150].
- "Group Title": required for group conversations in the UI [NewConversationDialog.tsx:L159-L162].
- "Pinned at top" = "Important" discussion flag [src: frontend/src/modules/discussions/components/NewDiscussionDialog.tsx:L142].

### MOD-DISC-001 Discussions
- "v{n} Edited": post has been edited n times; click for history [src: frontend/src/modules/discussions/components/DiscussionCard.tsx:L146-L149].
- Categories (11): Water, Equipment, Environmental Monitoring (EM), Products, Media / Materials, Internal Decisions, Management Requirements, EDA Requirements, ISO 17025, GMP / Regulatory, Other [src: frontend/src/modules/discussions/types/discussionTypes.ts:L20-L31].

### MOD-ERR-001 Error Monitoring
- "Incident": a grouped technical error (summary + correlation id + occurrences); not part of the GxP record [src: frontend/src/modules/errorMonitoring/ErrorMonitoringPage.tsx:L60-L61; backend/MicroLIMS.Application/Services/ErrorMonitoringService.cs:L28-L65].
- "Correlation id": identifier for related error entries, shown in search [src: IncidentFilterBar.tsx:L71].
- "Override" / "Auto (…)": severity set by a person vs computed severity [src: IncidentRowDetail.tsx:L155-L167].

### MOD-AUDIT-001 Audit Trail
- "Audit Events (N)": result heading [src: AuditSearchPage.tsx:L108-L114].
- "Changed Fields (Diff)": column of the changed field names [src: AuditResultsTable.tsx:L69-L75].
- "Inspect Event & Traceability Chain": row action opening the drawer [AuditResultsTable.tsx:L190-L205].
- "Traceability chain": chain of linked records inside the drawer [src: AuditTraceabilityChain.tsx:L24-L128].
- "Record Ref", "Entity Category": result column and filter [AuditResultsTable.tsx:L69-L75; AuditFilterBar.tsx:L108-L120].
- "Laboratory identifiers": Sample Reference, Batch Number, Control Number, Media Lot Number, Cryovial / RS Code, User ID (advanced filters) [AuditFilterBar.tsx:L145-L186].

### MOD-ARCHIVE-001 Archived records
- "Archived copies": heading of frozen PDFs on a sample report [src: frontend/src/modules/testingWorkspace/reportPrimitives.tsx:L178-L184].
- "Immutable PDF": description used by the UI for archived copies [reportPrimitives.tsx:L183-L185].

## reports

### MOD-ERR-001 Error Monitoring
- Export CSV of incidents for the current filters (max 5,000 rows); columns CorrelationId, Severity, Status, Sources, Occurrences, FirstSeenUtc, LastSeenUtc, Summary, ResolvedBy, ResolutionNotes. Error text on failure "The export could not be generated." [src: frontend/src/modules/errorMonitoring/ErrorMonitoringPage.tsx:L51; backend/MicroLIMS.Application/Services/ErrorMonitoringService.cs:L17, L160-L197].

### MOD-AUDIT-001 Audit Trail
- No export or print control found in `frontend/src/modules/auditSearch` (search and drawer only) [grep of that folder].

### MOD-ARCHIVE-001 Archived records
- Downloadable PDF per archived copy (file name `{entityType}_{documentId}_{timestamp}.pdf`); served as application/pdf with the integrity header [src: backend/MicroLIMS.API/Controllers/ArchivedRecordsController.cs:L44-L49; RecordArchiveService.cs:L46, L54-L70].

### MOD-DASH-001 Dashboards
- No export; tiles and lists only, plus "Refresh" [src: frontend/src/modules/dashboard/AdminDashboardPage.tsx:L89-L96].

### MOD-USERS-001 / MOD-ROLES-001 / MOD-NOTIF-001 / MOD-MSG-001 / MOD-DISC-001 / MOD-AUTH-001
- No report or export control was found in the files read for these modules (`UsersPage.tsx`, `RolesPage.tsx`, `Header.tsx`, `MessagesPage.tsx`, `DiscussionsFeedPage.tsx`, `Profile.tsx`). Discussion attachments are downloadable files (see screens) [src: frontend/src/modules/discussions/DiscussionDetailPage.tsx:L363-L367].

## audit-trail

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

## traceability

### MOD-AUDIT-001 Audit Trail drawer (traceability chain)
- Opened from an audit row ("Inspect Event & Traceability Chain"); chain by entity of the audit row [src: frontend/src/modules/auditSearch/components/AuditEventDrawer.tsx:L36; backend/MicroLIMS.Application/Services/AuditTraceabilityService.cs:L196-L220].
- Resolution by entity name (first match): Sample -> sample chain; Media -> media chain; Material -> material lot chain; Cryovial -> cryovial chain; EquipmentInventory -> equipment chain; Item -> item chain; ItemPreparationConfiguration -> item chain of its item; any other -> single node "General" showing entity name and id [src: backend/MicroLIMS.Application/Services/AuditTraceabilityService.cs:L186-L220].
- Node types the chain can show (as the UI displays `nodeType`, title, status): Item, PreparationConfiguration, Sample, SamplingPoint, Department, SamplePreparation, TestOrder, Result, Review, ElectronicSignature ("Signed"), Material, Media, MediaEvaluation, MaterialDocument, Organism ("Master Strain"), Cryovial, Equipment, EquipmentDocument, EquipmentStatusHistory [src: AuditTraceabilityService.cs:L232-L638].
- Sample chain nodes in order: sample -> preparation -> tests -> results -> reviews -> electronic signatures [src: AuditTraceabilityService.cs:L291-L435].
- "View Complete History for {entity} #{id}" opens all audit rows of that record, no cap [src: frontend/src/modules/auditSearch/components/AuditEventDrawer.tsx:L179-L187; AuditSearchService.cs:L76-L81].
- No stand-alone traceability page or menu item exists (GAP-108 covers coverage questions) [src: 01_APPLICATION_MAP §3.3, §13.5].

### MOD-NOTIF-001 / MOD-ERR-001 / MOD-DISC-001
- Notification links: an item tied to a sample or test order carries SampleId / TestOrderId to open that record [src: backend/MicroLIMS.Application/Services/DashboardNotificationService.cs:L14, L158-L168].
- Error incident links: incident to its error log entries (sources, occurrences) [src: ErrorMonitoringService.cs:L70-L100].
- Discussion post links: attachments and version history [src: DiscussionService.cs:L455-L470].

## gaps

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
