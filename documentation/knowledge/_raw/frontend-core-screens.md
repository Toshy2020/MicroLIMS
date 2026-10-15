# Frontend core screens - raw fact inventory

Scope: frontend/src/routes/AppRoutes.tsx, layouts/MainLayout.tsx, pages/*, and modules authentication, dashboard, receiving, receivingTesting, testingWorkspace, testPreparation, reports, kpi, auditSearch, oosTracking, errorMonitoring, messages, discussions, users, roles. Test files skipped. All paths relative to repo root.

Coverage legend:
- FULL = every route and the screen's own strings read.
- PARTIAL = top-level screen read; some nested dialogs/panels only listed (see per-module notes).
- UNCLEAR = not found or not verified in code.

Permission codes are quoted from frontend/src/routes/routes.ts:217-252 (PERMISSIONS constant) [src: frontend/src/routes/routes.ts:217].

---

## 0. Global guards and shell

### Route guards
- `AuthenticatedRoutes`: no token -> `<Navigate to="/login">` [src: frontend/src/routes/AuthenticatedRoutes.tsx:6-10].
- `PublicRoutes` (/login, /admin-recovery): token present -> `<Navigate to="/dashboard">` [src: frontend/src/routes/PublicRoutes.tsx:6-10].
- `PermissionRoute`: if permission missing -> silent `<Navigate to="/dashboard" replace>`, no message shown [src: frontend/src/routes/PermissionRoute.tsx:12]. Array code = any-of [src: frontend/src/routes/PermissionRoute.tsx:9].
- `useHasPermission` lives in frontend/src/contexts/AuthContext (not read).

### MainLayout (frontend/src/layouts/MainLayout.tsx)
- Forced password change: if `mustChangePassword` and path != `/change-password` -> redirect to `/change-password` [src: frontend/src/layouts/MainLayout.tsx:16, 65-67].
- Skip link text: "Skip to main content" [src: frontend/src/layouts/MainLayout.tsx:104].
- Sidebar collapse state persisted in localStorage key `microlims_sidebar_collapsed` [src: frontend/src/layouts/MainLayout.tsx:35, 37-43].
- Hamburger: mobile (<md) opens temporary drawer; desktop toggles collapse rail [src: frontend/src/layouts/MainLayout.tsx:45-54].
- Breadcrumbs component rendered above page [src: frontend/src/layouts/MainLayout.tsx:145].
- Per-page error boundary keyed on pathname [src: frontend/src/layouts/MainLayout.tsx:146-148].
- Idle logout: `handleIdleTimeout` calls `logout()` then `navigate("/login")` [src: frontend/src/layouts/MainLayout.tsx:56-59].
- Session-timeout dialog: title "Session Timeout Warning" [src: frontend/src/layouts/MainLayout.tsx:157]; body "You will be signed out due to inactivity in {n} second(s)." (singular when 1) [src: frontend/src/layouts/MainLayout.tsx:159-161]; single button "Stay signed in" (autoFocus) [src: frontend/src/layouts/MainLayout.tsx:164]. Not dismissable by Escape/backdrop (`onClose={() => {}}`) and no sign-out-only bypass [src: frontend/src/layouts/MainLayout.tsx:153-156, 156].

### Idle timeout (frontend/src/hooks/useIdleTimeout.ts)
- Idle timeout = 20 minutes (`20 * 60 * 1000`) [src: frontend/src/hooks/useIdleTimeout.ts:6].
- Warning countdown = 60 seconds [src: frontend/src/hooks/useIdleTimeout.ts:7].
- Activity events resetting the clock: mousemove, keydown, click, scroll [src: frontend/src/hooks/useIdleTimeout.ts:8].
- Countdown reaching 0 invokes the timeout callback (logout) [src: frontend/src/hooks/useIdleTimeout.ts:29-37].

### Header (frontend/src/components/Header.tsx)
- Hamburger aria-label "Toggle navigation menu" [src: frontend/src/components/Header.tsx:184].
- Dark mode switch: tooltip/aria "Switch to light mode" when dark, "Switch to dark mode" otherwise [src: frontend/src/components/Header.tsx:196-208]. Toggled via `useThemeMode` [src: frontend/src/components/Header.tsx:73].
- Notifications bell: aria-label "Notifications" plus ", N unread" [src: frontend/src/components/Header.tsx:216]. Empty: "No notifications" / "You are up to date - nothing needs your attention." [src: frontend/src/components/Header.tsx:257-259]. Load failure: "Notifications could not be loaded" [src: frontend/src/components/Header.tsx:250]. Endpoints: GET `/dashboard/notifications` [src: frontend/src/components/Header.tsx:117], POST `/dashboard/notifications/{id}/read` [src: frontend/src/components/Header.tsx:144], POST `/dashboard/notifications/read-all` [src: frontend/src/components/Header.tsx:154].
- Unread message count: GET `/messages/unread-count` [src: frontend/src/components/Header.tsx:89]; polled every 60 s (`POLL_INTERVAL_MS = 60_000`), skipped while document hidden [src: frontend/src/components/Header.tsx:57, 95-102 region].
- Account menu (aria "Account menu for {name}") [src: frontend/src/components/Header.tsx:285]:
  - "My Profile" -> /profile [src: frontend/src/components/Header.tsx:339-342]
  - "Messages" / "Messages (N unread)" -> /messages [src: frontend/src/components/Header.tsx:343-350]
  - "Discussions" -> /discussions [src: frontend/src/components/Header.tsx:351-354]
  - "Change Password" -> /change-password [src: frontend/src/components/Header.tsx:355-358]
  - "Sign Out" -> `logout()` then `/login`, no confirmation [src: frontend/src/components/Header.tsx:103-108, 360-363].

### Sidebar (frontend/src/components/Sidebar.tsx)
- Desktop collapse/expand button: tooltip and aria "Collapse sidebar" / "Expand sidebar" [src: frontend/src/components/Sidebar.tsx:357-360].
- Collapsed rail width 68 px; collapsed rail hidden on mobile [src: frontend/src/components/Sidebar.tsx:16, 43].
- Nav landmark aria-label "Main navigation" [src: frontend/src/components/Sidebar.tsx:394].
- Menu definitions (labels, paths, permission per item) live in frontend/src/routes/menuConfig.ts (not inventoried in full; examples: "Audit Search" -> /audit-search with Audit.View [src: frontend/src/routes/menuConfig.ts:78]).

### Global "Page not found" (frontend/src/pages/NotFound.tsx)
- Title "Page not found" [src: frontend/src/pages/NotFound.tsx:13].
- Empty state title "There is no page at this address"; description `"{pathname}" does not match any MicroLIMS page. The link may be outdated, or the page may have moved. Use the navigation menu or return to your dashboard.` [src: frontend/src/pages/NotFound.tsx:17-18].
- Button "Go to Dashboard" -> /dashboard [src: frontend/src/pages/NotFound.tsx:19].
- Catch-all route `*` inside MainLayout [src: frontend/src/routes/AppRoutes.tsx:280].

---

## 1. Authentication

### 1.1 Login - route `/login`
- Route: `<Route path="/login" element={<LoginPage/>}>` under PublicRoutes [src: frontend/src/routes/AppRoutes.tsx:97].
- Component: `LoginPage` at frontend/src/pages/Login.tsx:12. Page title (document.title) "Sign in · MicroLIMS" [src: frontend/src/pages/Login.tsx:68].
- Guard: none beyond PublicRoutes [src: frontend/src/routes/PublicRoutes.tsx:6-10].
- Heading: "MicroLIMS" (h1, "Micro" + "LIMS" accent), hidden text " sign in" [src: frontend/src/pages/Login.tsx:86-88].
- Fields: "Username" (autoComplete username, required, autoFocus) [src: frontend/src/pages/Login.tsx:133-139]; "Password" (required, show/hide toggle aria "Show password"/"Hide password") [src: frontend/src/pages/Login.tsx:141-163].
- Buttons: "Sign In" / "Signing in…" [src: frontend/src/pages/Login.tsx:171]; "Forgot password?" [src: frontend/src/pages/Login.tsx:176]; "Admin-Assisted Password Recovery" -> /admin-recovery [src: frontend/src/pages/Login.tsx:179-186].
- Forgot-password mode (same card): text "Enter your username and we'll generate a password reset request." [src: frontend/src/pages/Login.tsx:99]; field "Username" [src: frontend/src/pages/Login.tsx:101]; button "Send Reset Link" / "Submitting..." [src: frontend/src/pages/Login.tsx:103]; links "Back to login" [src: frontend/src/pages/Login.tsx:117] and "Have a recovery code from your admin?" -> /admin-recovery [src: frontend/src/pages/Login.tsx:125].
- Reset confirmation text (always shown after submit): "If that account exists, a reset link has been generated." [src: frontend/src/pages/Login.tsx:62]. Errors from the reset call are swallowed [src: frontend/src/pages/Login.tsx:58-59].
- Login error messages [src: frontend/src/pages/Login.tsx:39-47]:
  - 401 -> "Invalid username or password."
  - >=500 -> "Laboratory authentication service error. Please try again later."
  - no response -> "Unable to reach the laboratory server. Check network connection."
  - other -> server `message` or "Login failed. Please check credentials."
- Footer: "MicroLIMS v0.1 · FDA 21 CFR Part 11 & EU Annex 11 Compliant" [src: frontend/src/pages/Login.tsx:204].
- On success: token written to localStorage key `microlims_token` [src: frontend/src/pages/Login.tsx:34]; navigates to /dashboard [src: frontend/src/pages/Login.tsx:37].
- Endpoints: POST `/auth/login` [src: frontend/src/modules/authentication/services/authenticationService.ts:7]; GET `/auth/me` [src: frontend/src/modules/authentication/services/authenticationService.ts:35]; POST `/auth/password-reset/request` [src: frontend/src/modules/authentication/services/authenticationService.ts:19].
- Mock data: none.

### 1.2 Admin-assisted password recovery - route `/admin-recovery`
- Route [src: frontend/src/routes/AppRoutes.tsx:98] (PublicRoutes). Component `AdminPasswordRecovery` at frontend/src/pages/AdminPasswordRecovery.tsx:7.
- Heading "Password Recovery"; subtitle "Administrator-Assisted Account Access Recovery" [src: frontend/src/pages/AdminPasswordRecovery.tsx:57, 62].
- Fields: "Username" [src: :82]; "One-Time Recovery Code" (placeholder XXXX-XXXX-XXXX, helper "Format: 12 alphanumeric characters provided by your admin") [src: :90-97]; "New Password" (helper "Min 8 chars (upper, lower, digit, symbol)") [src: :100-107]; "Confirm New Password" [src: :110].
- Client validation [src: frontend/src/pages/AdminPasswordRecovery.tsx:22-30]: "All fields are required."; "New password and confirm password do not match." (Client does not check password policy; server does.)
- Failure text: server `message` or "Could not perform password recovery. Please verify your recovery code." [src: :37].
- Button "Submit Password Recovery" / "Resetting Password..." [src: :126].
- Success: "Your password has been successfully reset! You can now log in using your new password." + button "Go to Login Page" [src: :72, 75]. Link "Return to Login" [src: :138].
- Endpoint: POST `/auth/admin-password-recovery/confirm` [src: frontend/src/modules/authentication/services/authenticationService.ts:27].

### 1.3 Change password - route `/change-password`
- Route [src: frontend/src/routes/AppRoutes.tsx:111] inside MainLayout; no permission guard. Forced redirect target [src: frontend/src/layouts/MainLayout.tsx:16, 65-67]. Component `ChangePasswordPage` at frontend/src/pages/ChangePassword.tsx:20.
- Heading "Change Password"; subtitle "Update your laboratory credentials in compliance with periodic security policy." [src: frontend/src/pages/ChangePassword.tsx:61-62].
- Fields: "Current Password" (autoFocus) [src: :71]; "New Password" [src: :75]; "Confirm New Password" with helper "Passwords do not match." when mismatched [src: :79-82].
- Policy rules shown as live checklist, header "Password must contain:" [src: :86; rules :13-17]:
  1. At least 8 characters
  2. At least one uppercase letter
  3. At least one lowercase letter
  4. At least one digit
  5. At least one special (non-alphanumeric) character
  Comment: mirrors server PasswordPolicy in MicroLIMS.Shared.Validation; server authoritative [src: :10-11].
- Submit blocked messages: "New password does not meet the policy requirements below." [src: :37]; "New password and confirmation do not match." [src: :41].
- Fallback error: "Could not change password." [src: :52].
- Success: "Password changed successfully. Returning to your profile..." then navigate to /profile after 1500 ms [src: :50, 66].
- Button "Change Password" / "Updating Credentials..." [src: :111].
- Endpoint: POST `/auth/change-password` [src: frontend/src/modules/authentication/services/authenticationService.ts:31]. Calls `refresh()` from AuthContext after success [src: :48].

### 1.4 Profile - route `/profile`
- Route [src: frontend/src/routes/AppRoutes.tsx:110] (no permission). Component `ProfilePage` at frontend/src/pages/Profile.tsx:20. Read-only by design: "Name and role changes are made by an administrator in the Users module." [src: frontend/src/pages/Profile.tsx:18-19, 33].
- Heading "My Profile"; section "Account" [src: :33-34].
- Fields (dt/dd): "Full Name", "User ID" (shows username), "Role", "Last Login" (fallback "Never"), "Password Last Changed" (fallback "Never") [src: :36-40].
- Load error: "Could not load profile." [src: :25].
- Endpoint: GET `/auth/me` [src: frontend/src/modules/authentication/services/authenticationService.ts:35].

### Authentication module files
- frontend/src/modules/authentication/services/authenticationService.ts (endpoints above), tokenClaims.ts, types/authTypes.ts. No screens in module besides the pages above.
- Logout/token expiry handling in AuthContext/apiClient not read (out of scope).

---

## 2. Dashboard - route `/dashboard`

- Route [src: frontend/src/routes/AppRoutes.tsx:109] inside MainLayout; no permission guard.
- Entry component `DashboardPage` at frontend/src/modules/dashboard/DashboardPage.tsx:16.
- Role routing [src: frontend/src/modules/dashboard/DashboardPage.tsx:40-44]: Analyst -> AnalystDashboardPage; Reviewer -> ReviewerDashboardPage; SystemAdministrator -> AdminDashboardPage; any other role -> SectionHeadDashboardPage.
- Lab tabs (only when user is in >1 lab): aria-label "Laboratory dashboard" [src: :48-52]; labels "Microbiology Laboratory" / "Physicochemical Laboratory" [src: frontend/src/modules/dashboard/DashboardLabContext.tsx:6-9]. Lab selected via `?lab=` query param [src: :25-26, 31-36]. Lab order MICRO then FP [src: :14].
- Loading: WorkspaceSkeleton [src: :29].
- Error/empty components: DashboardStateGate - title "Dashboard could not be loaded" [src: frontend/src/modules/dashboard/components/DashboardStateGate.tsx:45]; empty "No dashboard data is available yet." [src: :65].

### 2.1 Admin dashboard - AdminDashboardPage.tsx:21
- Heading "Administrator Command Center" [src: frontend/src/modules/dashboard/AdminDashboardPage.tsx:84].
- Tiles [src: :70-78]: "Total samples", "Total tests", "Pending tests" (-> ?status=Active), "Review queue" (caption "samples", -> ?status=UnderReview), "Approval queue" (caption "samples", -> ?status=UnderApproval), "Prep configs pending" (conditional, -> /laboratory-configuration/items), "Overdue (>24h)" (-> ?urgency=overdue) [src: :78].
- Button "Refresh" [src: frontend/src/modules/dashboard/AdminDashboardPage.tsx:89-96 region].
- Load error: "The dashboard service did not respond. Your data has not been changed." [src: :52].
- Endpoints: GET `/dashboard?lab=` (getSummary) and GET `/dashboard/kpi-deltas` [src: frontend/src/modules/dashboard/services/DashboardService.ts:16, 25].

### 2.2 Analyst dashboard - AnalystDashboardPage.tsx:24
- Heading "Welcome back, {fullName or username or 'Analyst'}" [src: frontend/src/modules/dashboard/AnalystDashboardPage.tsx:26, 83]. Subtitle "Your prioritized work in the {lab name} for today." or "Your prioritized work for today." [src: :84].
- Buttons "Refresh" (outlined) [src: :86-93] and a link button to the lab workspace [src: :94-101].
- Warning alert on partial failure: "Some panels could not be loaded, so the throughput figures below may be incomplete." / "...incubation and throughput figures below may be incomplete."; retry button "Retry" [src: :106-116].
- Incubations are Microbiology-only; Physicochemical view skips them [src: :57].
- Panels: "My Operational Performance" [src: frontend/src/modules/dashboard/components/AnalystPerformancePanel.tsx:23]; "Completed Today" [src: components/CompletedTodayPanel.tsx:21]; "Attention Required" [src: components/AttentionRequiredPanel.tsx:67] with empty "No active attention alerts. All conditions normal." [src: :147]; "Today's Laboratory Work" table (columns "Sample / Reference", "Category", "Tests", "Received At", "Status", "Next Action") with filter toggle and "View all" tab [src: components/TodaysWorkTable.tsx:60, 87-92]; empty "No work matches this filter." [src: :142]; incubation panel "No active incubations running." [src: components/IncubationObservationPanel.tsx:92] titled via SectionTitle [src: :20].
- Tiles: UNCLEAR exact labels (rendered from data in ReviewerDashboard-style tile arrays not read in Analyst file).
- Endpoints (DashboardService.ts): GET `/dashboard/analyst-metrics` [:58], GET `/dashboard/my-tasks` [:43], GET `/dashboard/todays-work` [:46], GET `/dashboard/incubation-overview?myIncubationsOnly=` [:49].

### 2.3 Reviewer dashboard - ReviewerDashboardPage.tsx:43 (462 lines)
- Heading "Reviewer Command Center" [src: frontend/src/modules/dashboard/ReviewerDashboardPage.tsx:84].
- Tiles [src: :111-151]: "Pending review" (caption "samples", -> ?status=UnderReview) [:111-117]; "Overdue review" (caption "in review >24 hours", -> ?status=UnderReview&workload=reviewOverdue) [:120-126]; "Due today" (caption "submitted today", -> ?status=UnderReview) [:129-135]; "Retests in progress" (caption "samples", -> ?workload=retestInProgress) [:138-144]; "Completed today" (caption "by you") [:147-151].
- Empty states: "No samples are currently waiting for scientific review." [src: :250]; "No recent review history." [src: :411].
- Endpoints: GET `/dashboard/reviewer` [DashboardService.ts:22]; GET `/dashboard/notifications` as used by panels [:37].

### 2.4 Section head dashboard - SectionHeadDashboardPage.tsx:38 (778 lines)
- Heading "Section Head Command Center" [src: frontend/src/modules/dashboard/SectionHeadDashboardPage.tsx:106].
- Tiles row 1 [src: :132-144]: "Active tests" (-> ?status=Active), "Incubating" (-> ?view=kanban), "Ready to read" (-> ?testStatus=ReadyToRead), "Pending review" (-> ?status=UnderReview), "Pending approval" (-> ?status=UnderApproval), "Overdue" (-> ?urgency=overdue), "Attention items".
- Pipeline strip [src: :175-179]: "Incubation", "Ready to Read", "Scientific Review", "Final Approval".
- Section/list titles and empties: "No results awaiting scientific review." [src: :359]; "No samples awaiting final authorization." [src: :506]; "No active laboratory analysts found." [src: :601]. Analyst rows link to ?analystId=... [:624-663].
- Load error: "The dashboard service did not respond. No laboratory data has been changed." [src: :69].
- Endpoints (DashboardService.ts): GET `/dashboard/section-head` [:19], GET `/dashboard/status-distribution` [:31], GET `/dashboard/category-distribution` [:34], GET `/dashboard/monthly-trend?months=` [:28], GET `/dashboard/analyst-metrics` [:58].
- Chart components: "Samples Received Trend" [src: frontend/src/modules/dashboard/components/SamplesTrendChart.tsx:21]; "Test Order Status" donut, empty "No data yet." [src: components/TestOrderStatusDonut.tsx:23, 51].

### Dashboard module other files
- services/DashboardService.ts (endpoints above); hooks useDashboardSummary/useMyTasks/useTodaysWork; types/dashboard.ts. Mock data: none found.

---

## 3. Receiving - routes `/receiving`, `/receiving/tracking`

### 3.1 Receiving desk - route `/receiving`
- Route [src: frontend/src/routes/AppRoutes.tsx:140-142]: PermissionRoute code = any of `Samples.Receive` (PERMISSIONS.SAMPLES_RECEIVE) or `Samples.ReceiveOwnLab` (SAMPLES_RECEIVE_OWN_LAB) [src: frontend/src/routes/routes.ts:223-224].
- Component `ReceivingPage` at frontend/src/modules/receiving/ReceivingPage.tsx:29.
- Title "Receive Sample"; subtitle "Register incoming Finished Product, Raw Material, and Packaging Material samples, and choose which laboratories they go to." [src: frontend/src/modules/receiving/ReceivingPage.tsx:83-84].
- Buttons: "Refresh" (outlined) [src: :87-95]; "Receive" (contained, shown when `canReceiveAny`) [src: :97-104].
- Filter bar, KPI cards, register table, action menu, selection bar: see 3.3.
- Notifications: "Failed to load the sample register." [src: :58]; "Sample details corrected and signed." [src: :138]; "Sample voided." [src: :151]; "Laboratory added to the sample." [src: :164].
- Endpoint: GET `/testorders/page` and `/testorders/counts` [frontend/src/modules/receiving/services/ReceiveService.ts:92, 100]; POST `/samples` [:52]; POST `/water/receive` [:67], `/em/receive` [:70], `/aftercleaning/receive` [:73].

### 3.2 Tracking board - route `/receiving/tracking`
- Route [src: frontend/src/routes/AppRoutes.tsx:143-144]: PermissionRoute `Samples.TrackAll` [src: routes.ts:225].
- Component `TrackingBoardPage` at frontend/src/modules/receiving/TrackingBoardPage.tsx:105.
- Title "Tracking Board"; subtitle "Every received sample across both laboratories, with each lab's own stage." [src: :176-177].
- Filters: "Laboratory" [src: :190-193] options "All Laboratories", "Microbiology Laboratory", "Physicochemical Laboratory" [src: :47-50]; "Overall Status" [src: :204-207] options "All Overall Statuses", "In Progress", "Approved", "Rejected", "Retest Requested", "Voided", "Cancelled" [src: :37-44]; "Product / Item" autocomplete (helper "Product list unavailable - reload the page to retry." on failure) [src: :224-230]; "Received From" [src: :238]; "Received To" [src: :248]; reset button (text UNCLEAR beyond line 256-265).
- Table columns: "Reference", "Product", "Batch", "Received", "Overall", "Microbiology", "Physicochemical" [src: :278-284].
- Empty: "No samples found matching the filter criteria." [src: :292].
- Lab-stage chip when none: "Not requested" [src: :72]. Overall chip colors by status [src: :57-60].
- Endpoint: GET `/samples/tracking` [src: frontend/src/modules/receiving/services/TrackingService.ts:49].

### 3.3 Receiving components (partial)
- `NewSampleDialog` title "New Sample" [src: frontend/src/modules/receiving/dialogs/NewSampleDialog.tsx:320]; stepper Next/Back; final "Save All Samples" / "Receiving..." [src: :339-384]. Validation messages: "Item is required" [:176], "Production Stage is required" [:184], "Department is required" [:189, 194], "Previous Product is required" [:199], "Previous Product Batch No. is required" [:203], "Machine is required" [:207], "Cause of Testing is required" [:213], "Sampled By is required" [:218]. Error alert uses pre-line whitespace [:391].
- `MultiSampleEntryGrid` (grid of rows): headers "Quantity", "Mfg Date", "Exp Date" [src: frontend/src/modules/receiving/dialogs/MultiSampleEntryGrid.tsx:209, 229-230]; placeholders "Select Item", "Stage", "Select Department", "Select Machine", "Cause of Testing", "Sampled By", "Batch No.", "Control No.", "e.g. 50g / 100ml" [src: :276-553]; "Add Another Sample" button [src: :621-640]; row delete tooltip "At least one row is required" / "Delete sample row" [src: :595].
- `SampleTypeSelector` (category tiles): labels "Product", "Raw Material", "Packaging Material", "Water", "Environmental Monitoring", "After Cleaning" [src: frontend/src/modules/receiving/constants/receivingConstants.ts:6-41]; "Product" description "Finished pharmaceutical dosage forms (tablets, syrups, suspensions)" [:9]. Button "Next" disabled until category chosen [src: NewSampleDialog.tsx:~343].
- `EditSampleDetailsDialog` title "Edit Sample Details" [src: frontend/src/modules/receiving/dialogs/EditSampleDetailsDialog.tsx:216]; fields "Batch Number", "Control Number", "Cause of Testing", "Sampled By", "Sample Quantity", "Production Stage", "Mfg Date", "Exp Date", "Previous Product", "Previous Product Batch Number", "Storage Condition" (options "Room Temperature", "Refrigerator" [:418-419]), "Storage Time (hours)", "Reason for Correction" (required; helper "Recorded with your electronic signature and in the sample's audit trail.") [src: :281-446]; lock helper "Locked: testing has started for this sample" [src: :261]; buttons "Cancel" [:225], save button [:228-236].
- `VoidSampleConfirmationDialog`: heading "Void Sample Confirmation", subtitle "Strikes the sample from the register - signed with your password" [src: frontend/src/modules/receiving/dialogs/VoidSampleConfirmationDialog.tsx:87-90]; warning "The sample record and its open tests will be marked as Voided." [:128]; field "Reason for Voiding *" (helper error "A reason for voiding is required.") [:185-197]; "Cancel" [:98].
- `AddLaboratoryDialog`: title "Add Laboratory" [:121]; field "Laboratory" (Select) and "Reason *" (helper error "A reason is required.") [:196-225]; info helper "Recorded with your electronic signature and in the sample's audit trail." [:225]; "Cancel" [:131].
- `AssignAnalystDialog`: title "Assign Responsible Analyst" [src: frontend/src/modules/receiving/dialogs/AssignAnalystDialog.tsx:110]; field "Select Analyst" with option "Unassigned (Open for any authorized analyst to prepare)" [:191-199]; field "Reason for Assignment / Reassignment (recorded in Audit Trail)" [:219]; read-only labels "Reference Number:", "Batch / Control No.:", "Current Assignment:" [:163-173]; "Cancel" [:116].
- `SampleActionMenu` [src: frontend/src/modules/receiving/components/SampleActionMenu.tsx]: items "Assign Analyst" (needs `Samples.AssignAnalyst`) [:52], "Add Laboratory", "Prepare Sample", "Void Sample", "View Full Report", "Audit History" [src: :~90-250 primary= lines]; "Editable" indicator; edit requires `Samples.Correct` and sample.canEditDetails [:70-73]; void requires `Samples.Correct` and status != "Voided" [:73]; receive gate `Samples.Receive` [:69].
- `SampleFilterBar` item-type filter "All Item Types" [src: frontend/src/modules/receiving/components/SampleFilterBar.tsx].
- Sample status filter options: "All Sample Statuses", "Received", "Under Testing", "Pending Review", "Under Review", "Under Approval", "Approved" [src: frontend/src/modules/receiving/constants/receivingConstants.ts:49-55] (rest of list UNCLEAR beyond line 55).
- `SampleRegisterTable`: OOS chain title "OOS Investigation Chain: {code}" [src: frontend/src/modules/receiving/components/SampleRegisterTable.tsx]; expand/collapse title "Expand retest chain" / "Collapse retest chain" [src: :~chip row].
- Not inventoried: SampleStatusKpiCards labels, SelectionSummaryBar button labels, TestStatusSummaryCell (PARTIAL).

### Receiving endpoints (ReceiveService.ts / TrackingService.ts)
- POST `/samples` [:52]; GET `/samples/receipt-labs` [:57]; POST `/samples/{id}/laboratories` [:63]; POST `/water/receive` [:67]; POST `/em/receive` [:70]; POST `/aftercleaning/receive` [:73]; GET `/testorders/page` [:92]; GET `/testorders/counts` [:100]; GET `/testorders/{sampleId}` [:115]; PUT `/samples/{id}/correct` [:123]; PUT `/samples/{id}/assign-analyst` [:128]; POST `/samples/{id}/void` [:133]; GET `/samples/tracking` (TrackingService.ts:49).

---

## 4. Receiving & Testing workspace - routes

### 4.1 Lab workspace routes
- `/microbiology/workspace` -> `LabWorkspaceRoute code="MICRO"` [src: frontend/src/routes/AppRoutes.tsx:130].
- `/physicochemical/workspace` -> `code="FP" area="fp"` [src: :131].
- `/physicochemical/rm-pm-workspace` -> `code="FP" area="rmpm"` [src: :132].
- `/receiving-testing` -> `FirstLabWorkspaceRedirect` (redirect; first lab the user belongs to, MICRO first; preserves query) [src: :133; frontend/src/modules/receivingTesting/LabWorkspaceRoute.tsx:128-141].
- Guard: membership via useMyLabs; not a member -> "Not a member:" error alert with "You are not a member of the {lab name}." [src: frontend/src/modules/receivingTesting/LabWorkspaceRoute.tsx:20-28, 65-67]; RM&PM / FP area denial messages "You do not have access to the RM & PM workspace." / "You do not have access to the FP workspace." [:69-79]; no lab "You are not a member of any laboratory workspace." [:136]; section load failure "Unable to load laboratory details. Please refresh the page." [:85].
- `OtherAreaSampleGuard`: for `?sampleId=` deep links, 404 on this area -> redirect to the other FP area [src: :101-122].
- Lab names [src: :11-14]: "Microbiology Laboratory", "Physicochemical Laboratory".
- Sections fetched via laboratorySectionService.getSections (out of scope file).

### 4.2 ReceivingTestingWorkspacePage - frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:142 (1166 lines)
- Page title: "{lab.name} Workspace" (Microbiology) / "FP Workspace" / "RM & PM Workspace" [src: :744-748 region].
- Subtitle "Manage incoming samples, assignments, testing progress, review, and laboratory workflow execution from one workspace." [src: :751].
- Buttons: "Refresh" (outlined) [src: :755-770]; "+ New Sample" (contained) [src: :772-784] (opens receiving NewSampleDialog).
- KPI cards: SampleStatusKpiCards component [src: :789-799] (labels in frontend/src/modules/receiving/components/SampleStatusKpiCards.tsx, not read).
- Workload filter chip (deep-link only): "Active Workload Filter:" + "Retests in Progress" | "Overdue Review (>24h)" | "Overdue Approval (>24h)" [src: :802-815 region].
- View modes (URL `view` param; `WorkspaceDisplayView` = "table" | "card" | "kanban") [src: :61, 385, 980, 1003, 1013]: table = SampleRegisterTable; card = SampleCardView; kanban = SampleKanbanView [src: :980-1014].
- Table (compact) header: "Item / Reference", "Type", "Batch/Ctrl", "Status" [src: :682-685]; empty "No matching samples found." [src: :706].
- Toast on failure: "Sample #{id} could not be opened. It may not belong to this laboratory, or the server could not be reached." [src: :137].
- Panels/dialogs rendered from this page: SelectedSampleTestingPanel [:53], TestWorkflowDialogRouter [:55], SampleSummaryDialog [:56], receiving SampleActionMenu actions (Prepare Sample -> testPreparation module).
- Alert notifications: `notification.text` [src: :1155-1161].
- Endpoints: uses ReceiveService (GET `/testorders/page`, `/testorders/counts`, `/testorders/{id}`) and the testingWorkspace services (section 5).

### 4.3 testPreparation module (no route of its own)
- No `AppRoutes.tsx` entry imports modules/testPreparation. It is embedded in: laboratoryConfiguration item/water/EM/after-cleaning preparation forms, receivingTesting ReceivingTestingWorkspacePage, and testingWorkspace GroupedActionsPanel [src: git grep of imports; e.g. frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx imports testPreparation].
- Files: PreparationDialog.tsx (104 lines), TestPreparationForm.tsx (106), ConfirmPreparationForm.tsx (90), PreparationStepsSummary.tsx (45), components/GroupedPreparationRow.tsx, components/SamplingPointGrid.tsx; services: SamplePreparationService.ts, ItemPreparationConfigurationService.ts.
- Endpoints: POST `/sample-preparation` [SamplePreparationService.ts:74]; POST `/sample-preparation/confirm` [:78]; GET `/sample-preparation/groups?sampleIds=` [:84]; POST `/sample-preparation/batch-confirm` [:92]; GET `/items/{id}/preparation-configuration` [ItemPreparationConfigurationService.ts:39]; PUT same [:42]; POST `.../approve` [:45]; GET `/preparation-configurations/pending` [:48].
- Screen strings not extracted (PARTIAL/UNCLEAR).

---

## 5. Testing workspace - routes `/samples/:id/report`, `/samples/:id/coa`

### 5.1 Sample report - route `/samples/:id/report`
- Route [src: frontend/src/routes/AppRoutes.tsx:102] under AuthenticatedRoutes (outside MainLayout), no permission guard. Component `SampleReportPage` at frontend/src/modules/testingWorkspace/SampleReportPage.tsx:108.
- Loading text "Loading report…" [src: :130]. Error from `Failed to load the sample report.` [src: :119].
- Document heading label "sample summary report" [src: :144].
- Status labels map [src: :79-80]: Received -> "Received"; InTesting -> "Under testing"; UnderReview -> "Under review"; UnderApproval -> "Under approval"; Approved; Rejected. Other statuses shown raw.
- Timeline steps: "Received", "In testing", "Under review", "Under approval", then "Voided" (danger) or "Rejected"/"Approved" [src: :98-104].
- Sections (section labels): "sample identity", "dates & personnel", "sample preparation", "test results", "workflow timeline", "electronic signatures" [src: :163, 190, 205, 239, 250, 274].
- Key/value labels: Reference, Category, Item, Prev. Product, Prev. Batch, Stage, Batch, Control, Cause, Quantity, Sampling point, Storage ("Refrigerator (Nh)"), Manufactured, Expires, Received by, Received at, Sampled by, Reviewed by, Approved by [src: :165-198].
- Empty test results: "No tests recorded." [src: :244].
- Print button "Print / Save PDF" -> window.print() [src: :312-318]. Footer "This is a controlled document generated by MicroLIMS. Any printed copy is uncontrolled." [src: :307].
- Endpoints: GET `/samples/{id}/summary` [frontend/src/modules/testingWorkspace/services/SampleSummaryService.ts:25]; archived records via GET `/archived-records` and `/archived-records/{id}/download` [services/ArchivedRecordsService.ts:22, 26].
- Mock data: none found.

### 5.2 Certificate of Analysis - route `/samples/:id/coa`
- Route [src: frontend/src/routes/AppRoutes.tsx:103] under AuthenticatedRoutes, no permission guard. Component `SampleCoaPage` at frontend/src/modules/testingWorkspace/SampleCoaPage.tsx:273.
- Loading text "Loading certificate…" [src: :345]; error "Failed to load the certificate of analysis." [src: :283].
- Not-yet-available text: "A Certificate of Analysis is only available once this sample has been approved or rejected. Current status: {status}." [src: :363].
- Multi-section alert: "No approved laboratory sections are currently accessible." (fallback) with per-section status lines [src: :389-407].
- Variant toggle (aria "Certificate variant") [src: :445-469]: "Combined" plus one toggle per eligible section (section name).
- Printed layout headings: "Test Results by Location" [:501], "Test Results" [:526], "Remarks" [:529], fallback "No remarks." [:534], "Overall Conclusion" [:546]; table headers "Test", "Specification", "Sample Result", "Analyst" [:230-233]; matrix header "Location", "Alert", "Action", "Spec" [:103-105, 166].
- Signature blocks: reviewer ("Reviewed") and approver ("Approved" or "Rejected") [src: :413-416, 553-554].
- Endpoint: GET `/samples/{id}/summary?forCertificate=true` [SampleSummaryService.ts:24-25].

---

## 6. Reports - route `/reports`

- Route [src: frontend/src/routes/AppRoutes.tsx:115]; no route-level permission. Component `ReportsPage` at frontend/src/pages/Reports.tsx:30.
- Title "Reports"; subtitle "Search, trend, and export laboratory results." [src: frontend/src/pages/Reports.tsx:92].
- Tabs (labels, key) [src: frontend/src/pages/Reports.tsx:16-25]:
  1. "Overview" (overview)
  2. "Record Search" (search)
  3. "Media / GPT" (media-gpt)
  4. "Reference Strains" (reference-strains)
  5. "Trending & Analysis" (trending)
  6. "KPI / Performance" (performance) - shown only if `Kpi.View` (PERMISSIONS.KPI_VIEW) [src: :34-41, 152-154].
- Period selector (Quick periods) hidden on KPI tab [src: :94-102]; custom date range applies `T00:00:00.000Z` / `T23:59:59.999Z` [src: :78-83]. Default period "30d" [src: :70].
- Unknown `?tab=` falls back to Overview and rewrites the URL [src: :51-62].
- Overview tab: "Live Database Data" chip (static label) [src: frontend/src/modules/reports/components/OverviewTab.tsx:120-124]; "View in Search" button -> Record Search tab [src: OverviewTab.tsx:375]; status legend "Out of Spec", "Alert / Action Level", "Pending Approval", "Pending Review" [src: OverviewTab.tsx:436-466]; table headers "Date Entered", "Entered By", "Status", "Subject / Ref", "Test" [src: :381-385]; OverviewTab component at :95. Empty text "No matching records found" exists in frontend/src/modules/reports (file not pinned; UNCLEAR which component) [src: grep hit at line 193 of a reports component].
- Record Search tab (RecordSearchTab.tsx:29): filters - "Search" (button "Search Records") [src: frontend/src/modules/reports/components/ReportFilterPanel.tsx:49, 178]; "Result Type" (Quantitative | Qualitative) [:57-65]; "Result Level" (All, Within Limit, Out of Spec, Alert Level, Action Level) [:12-13]; "Category" [:70]; "Test" [:84]; "Location / Point" [:167]; "Status" [:123]; "Approval Status" [:135]; "Filters" heading [:41]; date range heading "Date Range" [:147].
- Trending tab (TrendingTab.tsx:36) - filters: "Parameter / Test", "Product / Item", "Category", "Date Range", "Compare With" (values "None", "Previous Period", "Product Comparison", "Location Comparison") [src: frontend/src/modules/reports/components/TrendingTab.tsx:203, 235, 262, 287, 302, 322-325]. Result table headers "Date Entered", "Entered By", "Status", "Item / Location", "Pathogen", "Reference", "Result" [:476-482].
- Media/GPT tab (MediaGptReportTab.tsx:34) and Reference Strains tab (ReferenceStrainReportTab.tsx:24): PARTIAL (components listed: MediaGptFilterPanel, MediaGptResultsTable, MediaGptSummaryCard, MediaGptDetailDialog, ReferenceStrainFilterPanel, ReferenceStrainResultsTable, ReferenceStrainDetailDialog). Labels not extracted.
- Compare dialog component (CompareDialog.tsx) and Record detail dialog (RecordDetailDialog.tsx): PARTIAL.
- KPI tab (KpiPerformanceTab.tsx:35): buttons "Export PDF" [KpiFilters.tsx ~93], "Definitions" [~89], "Workflow Profiles" [~91]; filters "Category" "From" "To" "Location / subject" "Test" with "All categories", "All locations", "All tests" [KpiFilters.tsx:57-84]; section titles "Flow" [KpiPerformanceTab.tsx:114], "Micro" [:116], "Quality" [:115]; KPI tile empty "No data" [KpiTile.tsx:54]; "No data in this period" / "None in this period" / "Not enough points for a trend yet" [KpiSection.tsx:90, 138, 152]; "Retry" [KpiPerformanceTab.tsx:95]; workload table title "Workload", columns "Analyst", "Gaps" (+ capacity/utilisation tile labels "Capacity (units)", "Delivered units", "Forward load", "Open units", "Utilisation", "Weighted on time", "Weighted right first time") [WorkloadTable.tsx:14-20, 98-140]; empty "No analysts to show for this scope" [WorkloadTable.tsx:122]; record drawer close "Close records" [KpiRecordsDrawer.tsx:48].
- Workflow profiles panel [WorkflowProfilesPanel.tsx:56]: columns "Workflow type", "Work units", "Target (days)", "Status", "Last change", "Name", "Test" [:127-131, 187-188]; "No tests to show for this workflow type." [:230]; flag labels "Needs configuration only" [:114], "Override" [:205]; buttons per row "Show tests for {label}" [:143], history [:171], add version [:175].
- KPI endpoints: GET `/kpi/dashboard` [frontend/src/modules/kpi/services/kpiDashboardService.ts:27]; GET `/kpi/{key}/records` [:30]; GET `/kpi/definitions` [:33]; GET `/kpi/workload` [kpiWorkloadService.ts:34]; GET `/kpi/workload/tests` [:40]; GET/PUT `/kpi/settings/{sectionId}` [:45, 50]; GET/POST `/kpi/availability`, PUT `/kpi/availability/{id}` [:55, 60, 65]; workflow profiles base `/kpi/workflow-profiles` [workflowProfileService.ts:17] (per-call URLs UNCLEAR beyond base).
- Reports endpoints: GET `/reporting/results` [frontend/src/modules/reports/services/ReportingService.ts:21]; `/reporting/filter-options` [:24]; `/reporting/completed-by-month` [:28]; `/reporting/results/{id}` [:32]; `/reporting/overview` [:61]; `/reporting/qualitative-events` [:65]; `/reporting/compare` [:70]; `/reporting/results/export` [:78]; `/reporting/trend` [TrendingService.ts:91]; `/reporting/media-gpt` [MediaGptReportService.ts:18], `/reporting/media-gpt/{id}` [:22], `/summary` [:26], `/filter-options` [:31], `/export` [:36]; `/reporting/reference-strains` [ReferenceStrainReportService.ts:17], `/{id}` [:21], `/filter-options` [:25], `/export` [:30].
- Mock data: TrendingTab fallback option lists and default criteria (see section 16).

---

## 7. Audit search - route `/audit-search`

- Route [src: frontend/src/routes/AppRoutes.tsx:213-214] guard `Audit.View` (PERMISSIONS.AUDIT_VIEW) [src: routes.ts:220]. Component `AuditSearchPage` at frontend/src/modules/auditSearch/AuditSearchPage.tsx:27.
- Title "Audit Trail"; subtitle "Search and trace immutable change events, actor identities, and laboratory records across MicroLIMS." [src: AuditSearchPage.tsx:90-91].
- Error: "Failed to fetch audit records from the server." [src: :72].
- Results heading "Audit Events" or "Audit Events (N)" with ", capped at 300 latest" when N >= 300 [src: :108-114].
- Filters (AuditFilterBar.tsx): "From Date", "To Date" [:73, 84]; "Action" (All Actions, Create, Update, Delete) [:93-103]; "Entity Category" (All Entities + ENTITY_DISPLAY_NAMES entries as "{label} ({key})") [:108-120]; advanced lab identifiers (collapsible): "Sample Reference" (placeholder e.g. FP0107026), "Batch Number" (e.g. B-2026-088), "Control Number" (e.g. CTRL-904), "Media Lot Number" (e.g. MED-044), "Cryovial / RS Code" (e.g. CRYO-019), "User ID" (e.g. 5) [:145-186]; toggle "More Laboratory Identifiers (Batch, Sample, Media...)" / "Hide Laboratory Identifiers" [:202-203]; buttons "Reset" [~:215] and "Search Audit Trail" / "Searching..." [:231]; Enter key submits [:41].
- Table columns: "Date / Time", "User", "Entity", "Action", "Changed Fields (Diff)", "Record Ref", "Details" [src: frontend/src/modules/auditSearch/components/AuditResultsTable.tsx:69-75]; empty "No audit events found" [:54]; action chip uppercase (CREATE/UPDATE/DELETE) [:130]; row button "Inspect Event & Traceability Chain" [:190-205].
- Drawer (AuditEventDrawer.tsx:36): close aria "Close audit event details" [:94]; chip uppercase action [:80]; button "View Complete History for {entityName} #{entityId}" [:179-187].
- Other components: AuditDiffViewer, AuditRawJsonViewer, AuditTraceabilityChain (labels not extracted; PARTIAL).
- Endpoints: POST `/admin/audit/search` [frontend/src/modules/auditSearch/services/AuditSearchService.ts:9]; GET `/admin/audit?entityName=&entityId=` [:12]; GET `/admin/audit/{id}/traceability` [:15]; GET `/admin/audit/traceability?entityName=&entityId=` [:18].

---

## 8. OOS tracking - route `/oos-tracking`

- Route [src: frontend/src/routes/AppRoutes.tsx:216-217] guard `Oos.Manage` (PERMISSIONS.OOS_MANAGE) [src: routes.ts:247]. Component `OosTrackingPage` at frontend/src/modules/oosTracking/OosTrackingPage.tsx:64.
- Title "Out-of-Specification Tracking"; subtitle "Every out-of-specification investigation chain grouped by OOS number, tracing all retest spin-offs back to the original sample." [src: OosTrackingPage.tsx:104-105].
- Section title "OOS Investigation Groups"; summary "1 OOS Group" or "N OOS Groups (M retest samples)" [src: :111-116].
- Empty: "No active OOS investigation chains found." [src: :134].
- Per group: chips "Lab Investigation Report Attached" / "No Report Attached" [:223-233]; button "Manage Investigation" (if document) or "Upload Investigation" [:240-247]; expand/collapse aria "Expand OOS group" / "Collapse OOS group" [:171].
- Retest table headers: "Retest Sample", "Parent / Origin", "Item / Point", "Retest Type", "Test(s)", "Analyst(s)", "Retest Status", "Opened", "Actions" [:258]; empty "No retest samples have been spun off yet for this OOS chain." [:270]; retest type labels "Retest Retained Sample", "New Sample Request" [:45-46]; button text per retest row "Open printable report" tooltip "Open the retest sample's printable report" [:339-351].
- Investigation documents dialog (FloatingDialog title "Lab Investigation Documents: {oosGroupCode}") [:486]: "Select File", "Upload", "Choose Replacement File", "Supersede", reason label "Reason for Supersession *" [:555]; info "The current document will be marked Superseded. A new active version will take its place." [:551]; "Upload Initial Investigation Report" section [:~400s]; doc status chips "Current", "Superseded", "Voided" [:640-646]; doc table headers "Status", "Filename", "Size", "Uploaded By", "Date", "Actions" [:609]; empty "No investigation documents uploaded for this OOS group yet." [:627]; tooltips "Download file", "Supersede with replacement report" [:673-679]; accepted file types .pdf,.jpg,.jpeg,.png,.webp,.tiff [:~ input accept].
- Error messages [src: :79, 414, 436, 454, 475]: "Failed to load OOS tracking items.", "Failed to load investigation documents.", "Failed to upload document.", "Failed to download document content.", "Failed to supersede document.".
- Voided doc shows "Voided: {voidReason}" [:656-658]. Void action: UNCLEAR (service method exists, UI control not located).
- Endpoints [frontend/src/modules/oosTracking/services/OosTrackingService.ts]: GET `/oos-tracking` [:57]; GET `/oos-tracking/{group}/investigation-documents` [:61]; POST same [:67]; GET `/oos-investigation-documents/{id}/content` [:71]; POST `/oos-investigation-documents/{id}/supersede` [:87]; POST `/oos-investigation-documents/{id}/void` [:93].

---

## 9. Error monitoring - route `/error-monitoring`

- Route [src: frontend/src/routes/AppRoutes.tsx:264-265] guard `System.ViewErrorLog` (PERMISSIONS.SYSTEM_VIEW_ERROR_LOG) [src: routes.ts:39]. Component `ErrorMonitoringPage` at frontend/src/modules/errorMonitoring/ErrorMonitoringPage.tsx:11.
- Title "Error Monitoring"; subtitle "Technical incidents captured from the application, the API and the database. Operational data only - this is not part of the GxP record." [src: ErrorMonitoringPage.tsx:60-61].
- Errors: "Could not load incidents." [:27]; "The export could not be generated." [:51].
- Filters (IncidentFilterBar.tsx): "Severity" (All; Info, Warning, Error, Critical) [:31-35]; "Source" (All; Backend, Frontend, Database) [:40-44]; "Status" (All; Open, Resolved) [:49-53]; "Active from", "Active to" (date) [:58, 64]; "Search summary or correlation id" [:71]; buttons "Reset" [:76-78] and "Export CSV" / "Exporting..." [:79-84].
- Table headers: "Severity", "Summary", "Sources", "Occurrences", "First seen", "Last seen", "Status" [IncidentTable.tsx:37-43]; empty "No incidents match these filters." [:56]; status chips "Open" (warning filled) / "Resolved" (success outlined) [:89-93].
- Row detail (IncidentRowDetail.tsx:19): severity override select "Override" with option "Auto ({severity})" [:155-167]; chip "overridden from {severity}" [:126-127]; resolution field "Resolution notes" [:83]; buttons "Mark resolved" [:86-91] and "Reopen" [:102-107]; resolved text "Resolved {time} by {user} - {notes}" [:98-100].
- Endpoints [frontend/src/modules/errorMonitoring/services/ErrorMonitoringService.ts]: GET `/error-monitoring/incidents` [:36]; GET `/error-monitoring/incidents/{id}` [:44]; POST `/error-monitoring/incidents/{id}/resolve` [:49]; POST `/error-monitoring/incidents/{id}/reopen` [:53]; POST `/error-monitoring/error-logs/{id}/severity-override` [:57]; GET `/error-monitoring/incidents/export` [:61].
- Enum values: frontend/src/modules/errorMonitoring/types/errorMonitoringTypes.ts:2-4, 6-8.

---

## 10. Messages - route `/messages`

- Route [src: frontend/src/routes/AppRoutes.tsx:114]; no permission guard. Component `MessagesPage` at frontend/src/modules/messages/MessagesPage.tsx:22.
- Title "Direct & Group Messages"; subtitle "Real-time workplace discussions with laboratory analysts, reviewers, and section heads." [src: MessagesPage.tsx:75-76]. Header button "New Message" [:78-86].
- Load error fallback "Failed to load conversations." [:42]; error alert [:90-92].
- Right pane empty: "Select a conversation" / "Choose a conversation from the list or start a new message." [:163, 166]; button "New Message" [:168-175].
- ConversationList: filter placeholder "Filter conversations..." [ConversationList.tsx:60]; empty "No conversations yet. Start a new message!" / "No matching conversations found." [:83-84]; preview "No messages yet" [:162].
- ChatWindow.tsx:27: placeholder "Write a message... (Press Enter to send)" [:223]; aria "Message" [:237]; send aria "Send message" [:239]; empty "No messages in this conversation yet." / "Say hello to start the conversation!" [:164-165]; toast "Failed to send message." [:76]; back aria "Back to conversations" [:113].
- NewConversationDialog.tsx:36: title "New Message" [:110-111]; type radio "Direct Message (1-on-1)" / "Group Conversation" [:140, 150]; "Group Title" with placeholder "e.g. Microbiology Team, Water Testing Group..." [:159-162]; recipient field "Select Participants" (group) / "Select Recipient" (direct), placeholder "Search colleagues by name..." [:186-187]; "Initial Message" placeholder "Type your message here..." [:242-245]; buttons "Cancel" [:255], "Send Message" / "Sending..." [:258-266] (disabled until recipient and message).
- Validation [NewConversationDialog.tsx:68-76]: "Please select at least one recipient."; "Group conversation title is required."; "Initial message cannot be empty."; fallback "Failed to create conversation." [:93]; directory failure: "The user directory could not be loaded, so no recipients can be listed. Close and reopen this dialog to try again." [:115].
- Endpoints [frontend/src/modules/messages/services/messageService.ts]: GET `/messages/conversations` [:11]; GET `/messages/conversations/{id}` [:16]; POST `/messages/conversations` [:21]; GET `/messages/conversations/{id}/messages` [:26]; POST `/messages/conversations/{id}/messages` [:33]; POST `/messages/conversations/{id}/read` [:40]; GET `/messages/unread-count` [:44]; GET `/users/directory` [:49].

---

## 11. Discussions - routes `/discussions`, `/discussions/:id`

### 11.1 Feed - route `/discussions`
- Route [src: frontend/src/routes/AppRoutes.tsx:112], no permission guard. Component `DiscussionsFeedPage` at frontend/src/modules/discussions/DiscussionsFeedPage.tsx:30.
- Title "Discussions & Knowledge Sharing"; subtitle "Collaborative laboratory discussions, technical queries, compliance alerts, and knowledge exchange." [src: DiscussionsFeedPage.tsx:97-98]. Button "New Discussion" [:100-108].
- Errors: "Failed to load discussions." fallback [:60]; error alert [:112-114].
- Toasts: "Marked as important" / "Unmarked as important" [:76]; "Failed to toggle important status." [:78]; "Discussion post deleted." [:87]; "Failed to delete discussion post." [:89].
- Search placeholder "Search discussions by title or content..." [:121]; "Important Only" toggle [:153]; category chips "All Categories" + 11 categories [:157-172]; categories [frontend/src/modules/discussions/types/discussionTypes.ts:20-31]: Water, Equipment, Environmental Monitoring (EM), Products, Media / Materials, Internal Decisions, Management Requirements, EDA Requirements, ISO 17025, GMP / Regulatory, Other.
- Empty: "No discussions found" [:202]; subtext "Try adjusting your search query or category filters." when filtered, else "Be the first to start a conversation in this category." [:205-207]; button "Start a Discussion" [:216].
- Endpoint: GET `/discussions` [frontend/src/modules/discussions/services/discussionService.ts:24]; PATCH `/discussions/{id}/important` [:50]; DELETE `/discussions/{id}` [:55].

### 11.2 Post card (components/DiscussionCard.tsx)
- Chips: "Important" [:130]; "v{n} Edited" with tooltip "Version {n} (Edited). Click to view history." [:146-149].
- Menu aria "Post actions" [:163]; items: "View / Edit" (link to /discussions/{id}) [:167-176], "Mark Important" / "Unmark Important" [:177-182], "Delete Discussion" [:183-188].

### 11.3 New discussion dialog (components/NewDiscussionDialog.tsx:32)
- Title "New Discussion" [:97-99].
- Fields: "Title" (required) [:105]; "Category" select [:117-128, options DISCUSSION_CATEGORIES]; checkbox "Mark as Important (Pinned at top)" [:142]; "Content / Details" (required) [:147].
- Attachments: heading "Attachments (Documents, Images, Worksheets)" [:~162]; button "Choose Files" [:~177-180] (accept .pdf,.png,.jpg,.jpeg,.gif,.doc,.docx,.xls,.xlsx,.pptx,.txt,.csv); each file as chip "{name} ({KB} KB)" [:190-192].
- Buttons "Cancel" [:203-205]; "Publish Discussion" / "Publishing..." [:206-214]; disabled unless title and content non-empty.
- Validation [:53-58]: "Discussion title is required."; "Discussion content is required."; fallback "Failed to create discussion." [:80].
- Endpoint: POST `/discussions` multipart [discussionService.ts:34].

### 11.4 Edit discussion dialog (components/EditDiscussionDialog.tsx:30)
- Title "Edit Discussion (Version {currentVersion})" [:79-82].
- Info alert text [:86-88] (wording not extracted).
- Fields "Title" (required) [:91-95]; "Category" [:102-106]; "Mark as Important" [:127]; "Content / Details" (required) [:132-139].
- Buttons "Cancel" [:144-146]; "Save Changes" / "Saving Changes..." [:147-155].
- Validation [:50-55]: "Discussion title is required."; "Discussion content is required."; fallback "Failed to update discussion." [:71].
- Endpoint: PUT `/discussions/{id}` with If-Match version [discussionService.ts:45].

### 11.5 Discussion detail - route `/discussions/:id`
- Route [src: frontend/src/routes/AppRoutes.tsx:113]. Component `DiscussionDetailPage` at frontend/src/modules/discussions/DiscussionDetailPage.tsx:43.
- Load error fallback "Failed to load discussion."; not found "Discussion not found." [:78, 195].
- Back link "Back to Discussions" -> /discussions [:208-215].
- Buttons: "Edit Post" (opens EditDiscussionDialog) [:219-226]; post menu aria "Post actions" with "Mark Important"/"Unmark Important" [:240] and "Delete Discussion" [:252].
- Chips "Important" [:288], "v{n} Edited" with tooltip "Click to view full revision history" [:300-303] (opens DiscussionHistoryDialog; dialog title not extracted, see UNCLEAR).
- Attachments: download icon aria "Download {fileName}", title "Download file" [:363-367].
- Comments: textarea placeholder "Write a comment or response to this discussion..." (aria "Comment") [:387, 396]; button "Post Comment" / "Posting..." [:399-409]; empty "No comments yet. Be the first to share your feedback or observations!" [:416]; comment actions aria "Edit comment", "Delete comment" [:459-462]; edit mode aria "Edit comment", "Cancel editing", "Save comment" [:480-493].
- Toasts: "Comment added" [:124], "Failed to post comment." (server message fallback) [:126], "Comment updated" [:150], "Failed to save comment." [:152], "Comment deleted" [:173], "Failed to delete comment." [:175], "Discussion post deleted" [:108], "Discussion post" delete failure "Failed to delete discussion post." [:111].
- Confirm dialogs [:538-555]: title "Delete Discussion Post", message "Are you sure you want to delete this discussion post? This action cannot be undone.", confirm "Delete Post" (destructive); title "Delete Comment", message "Are you sure you want to delete this comment? This action cannot be undone.", confirm "Delete Comment" (destructive).
- Endpoints (discussionService.ts): GET `/discussions/{id}` [:29]; PUT `/discussions/{id}` [:45]; PATCH `/discussions/{id}/important` [:50]; DELETE `/discussions/{id}` [:55]; GET `/discussions/{id}/history` [:59]; GET `/discussions/{postId}/attachments/{attachmentId}/download` [:64]; POST `/discussions/{postId}/comments` [:78]; PUT `/discussions/{postId}/comments/{commentId}` [:83]; DELETE same [:88].
- Permission `Discussions.EditAny` exists (routes.ts:38) - UI use not verified (UNCLEAR).

---

## 12. Users - route `/users`

- Route [src: frontend/src/routes/AppRoutes.tsx:271-272] guard `Users.Manage` (PERMISSIONS.USERS_MANAGE) [src: routes.ts:218]. Component `UsersPage` at frontend/src/modules/users/UsersPage.tsx:33.
- Title "User Management"; subtitle "Manage system users, role assignments, security status, and account access." [src: UsersPage.tsx:293].
- Section "Create New User" [:295]: fields "Full Name" [:317], "Username" [:325], "Email" (helper "Password reset destination") [:333-337], "Password" (helper "Min 8 chars") [:342-346], role select (aria "Role", placeholder "Select Role *") [:356-359]; button "Create User" [:361-368]. Toasts: `User "{username}" created successfully.`; fallback "Could not create user." [~:96-100 region].
- Section "All System Users" [:372]; columns "User", "Email", "Role", "Status", "Security / Password", "Last Login", "Actions" [:377-384].
- Row chips: "You" (self) [:393]; role chip or "No role" [:401]; email fallback "No email" [:397]; status "Active" / "Disabled" [:405-407]; security "Account Locked" [:412], "Must Change Password" [:413], "Normal" [:414].
- Row icon buttons with tooltips [:426-482]: "Edit Profile"; "Laboratory Sections"; "Change Role" (disabled for self, tooltip "System Administrators cannot change their own role"); "Disable Account" / "Enable Account" (self tooltip "System Administrators cannot disable their own account"); "Unlock Account" (shown when locked); "Reset Password via Email"; "Admin-Assisted Password Recovery" (self/disabled-user tooltips "Cannot use admin recovery on own account", "Enable user first to perform recovery"); "Force Password Change at Next Login"; "Permanently Delete User" (self tooltip "You cannot permanently delete your own account").
- Dialogs (FloatingDialog):
  - "Edit Profile: {username}" - fields "Full Name", "Username", "Email" (type email, helper "Required for email password resets"); buttons "Cancel", "Save Profile" [:495-506]. Error: "Could not update profile." [fallback].
  - "Change Role: {username}" - "Current Role: {name or None}", select "New Role" ("Select New Role"), "Reason for Change" (required; placeholder "Mandatory administrative justification") [:515-529]; button "Change Role" disabled until role+reason [:519]; validation toast "Selected Role and a Reason are required for role change." [~:136-141].
  - "Disable User Account" / "Enable User Account" - text "Are you sure you want to {disable|enable} account ...?" [:549]; "Reason for Disabling" (required when disabling; placeholder "Mandatory justification") [:553]; button "Disable Account" / "Enable Account" [:542-544]; toasts "A reason is required to disable a user account." [src: :159], "Account enabled|disabled for {username}." and "Could not update account status." [:~164-168].
  - "Unlock User Account" - "Reason for Unlock (Optional)" [:575]; button "Unlock Account" [:567]; toasts "Account unlocked for {username}.", "Could not unlock account." [:~190-196].
  - "Initiate Email Password Reset" - text "Send password reset instructions to {name} ({email or 'No email on file'})?" [:594]; info "The system will generate a secure reset token link and send it via email. Plaintext passwords are never generated or shown to administrators." [:596-598]; "Reason for Reset (Optional)" [:599]; button "Send Reset Link" [:588]; toasts "Password reset instructions initiated for {username}.", "Could not initiate password reset." [:~205-212].
  - "Administrator-Assisted Password Recovery" - mandatory "Reason for Recovery (Mandatory)" (placeholder "e.g. User forgot password and cannot access registered email.") [:638-646]; warning: one-time 15-minute recovery code shown once, communicated via approved internal channels [:634-636]; button "Generate Recovery Code" (disabled until reason) [:613-620]; after generation: "Copy Code" / "Copied!" [:691], chip "Expires in 15 minutes" [:692], error alert "IMPORTANT: This code will NOT be displayed again after closing this dialog..." [:694-695], button "Close & Done" [:623]. Toast on empty reason: "A reason is required for admin-assisted password recovery." [src: :218].
  - "Force Password Change" - "Require {name (@username)} to change their password on next login?" [:714-716]; button "Force Password Change" [:710].
  - "Permanently Delete User" - text "Permanently delete {name (@username)}?" [:739-741]; warning: "This permanently removes the account and cannot be undone. It is only possible when the user has no activity history anywhere in the system. If this fails, deactivate the account instead." [:736-738]; buttons "Cancel" [:727], "Permanently Delete" / "Deleting..." [:728-730]; error alert shows deleteError [:735].
- Section dialog "Laboratory Sections: {fullName}" (UserSectionsDialog.tsx:188) - buttons "Cancel" [:191-193] and save [:194-205]; options "All sections (entire department)" [:253], "Area" select [:318-323]; service calls not inventoried (UNCLEAR beyond laboratorySectionService import).
- Endpoints (UserService.ts): GET `/users` [:23]; GET `/users/analysts` [:26]; GET `/users/{id}` [:29]; POST `/users` [:32]; PUT `/users/{id}` (If-Match) [:35]; PUT `/users/{id}/role` [:38]; PUT `/users/{id}/status` [:41]; PUT `/users/{id}/unlock` [:44]; POST `/users/{id}/password-reset` [:47]; POST `/users/{id}/admin-password-recovery` [:50]; PUT `/users/{id}/force-password-change` [:53]; DELETE `/users/{id}` [:56] (hard delete).
- Also loads roles via RoleService.getAll [UsersPage.tsx:85].

---

## 13. Roles - routes `/roles`, `/roles/new`, `/roles/:id`

- Routes [src: frontend/src/routes/AppRoutes.tsx:274-278] all under `Roles.Manage` (PERMISSIONS.ROLES_MANAGE) [src: routes.ts:219].

### 13.1 Roles list - `/roles` (RolesPage.tsx:15)
- Title "Roles"; subtitle "System Administrator, Section Head, Reviewer, Analyst, and any custom roles." [src: frontend/src/modules/roles/RolesPage.tsx:45].
- Search placeholder "Search by role name..." (aria "Search by role name") [:~50].
- Button "Create Role" -> /roles/new [:60-62].
- Columns "Role", "Type", "Status", "Users" [:69-72]. Row click -> /roles/{id} [:77].
- Chips "System" / "Custom" [:83-84]; "Active" / "Inactive" [:91].
- Empty: `No roles match "{search}".` [:100-101].
- Load error via LoadFailuresAlert "Reload the page to try again." [:47].
- Endpoint: GET `/roles` [RoleService.ts:32]; GET `/permissions` [:50] (on detail).

### 13.2 Create role - `/roles/new` (CreateRolePage.tsx:18)
- Title "Create Role"; subtitle "Define a new role's identity, base type, and granted permissions." [:102].
- Stepper steps [:16]: "Name", "Base Type", "Permissions", "Review".
- Step 1 fields: "Role Name" (required, autoFocus) [:114]; "Description" [:115]. Next gated: name non-empty [:79-82].
- Step 2 info: roles based on one of 4 built-in types for compatibility [:122-126]. Radio options (raw enum labels): SystemAdministrator, SectionHead, Reviewer, Analyst (default Analyst) [:15, 27, 129].
- Step 3: "Or clone permissions from any existing role:" with select "Select a role to clone" [:144-154]; permission checklist via PermissionMatrix.
- Step 4 review: labels "Name", "Description", "Base Type"; checked codes as chips; "None selected." [:166-188].
- Buttons: "Back" [:196], "Next" [:198-199], "Create Role" / "Creating..." [:202-203].
- Errors: "Could not create this role." fallback [:92].
- Endpoints: POST `/roles` [RoleService.ts:38]; GET `/roles` [:32]; GET `/roles/{id}` [:35]; GET `/permissions` [:50].

### 13.3 Role detail - `/roles/:id` (RoleDetailPage.tsx:18)
- Title = role name; subtitle "Edit this role's name, description, and granted permissions." [:115].
- Buttons: "Delete Role" / "Deleting..." (error color) [:117-125] - confirm dialog text `Delete role "{name}"? This cannot be undone.` plus assigned-user count sentence ("It is currently assigned to N user(s) and cannot be deleted until they are reassigned.") [:188-196]; "Save Changes" / "Saving..." [:159-161].
- Chips "System Role" [:139], "Base Type: {type}" [:140].
- Fields "Name" [:143], "Description" [:144].
- Info [:149-152]: role behaves like its base type on screens not yet migrated to the new permission system (marked "Legacy-only").
- Section "Permissions" [:155] (PermissionMatrix).
- Validation: "Role name is required." [:63-65].
- Holders section: "The users holding this role could not be loaded." [:167]; "No users currently hold this role." [:169]; status chips "Active" / "Disabled" [:180].
- Endpoints: GET `/roles/{id}` [:35]; PUT `/roles/{id}` [RoleService.ts:41]; DELETE `/roles/{id}` [:44]; PUT `/roles/{id}/permissions` [:47]; GET `/permissions` [:50]; holders via GET `/users` (UserService.getAll) [RoleDetailPage.tsx:45].

### 13.4 PermissionMatrix (components/PermissionMatrix.tsx:29)
- Each permission rendered as checkbox with code and description [:61-64].
- Chip "Enforced" (success) or "Legacy-only" (warning) [:75-83].
- Tooltip (enforced): "This permission alone controls access - the endpoints it covers check it directly." (legacy): "Access on screens covered by this permission still follows the role's underlying base type, not this checkbox, until that screen is migrated to the new permission system." [:69-74].

---

## 14. Section and group landing pages - routes `/sections/:slug`, `/groups/:slug`

- Routes [src: frontend/src/routes/AppRoutes.tsx:116-117]; component `SectionPage` at frontend/src/pages/Section.tsx:11. No route guard; cards filtered by the same permission-filtered menu [src: frontend/src/pages/Section.tsx:8-10].
- Fallback title "Section not available"; subtitle "You have no pages in this section." [:17].
- Subtitle "{n} page(s) you can open here" [:35].

---

## 15. Redirects / legacy routes (whole AppRoutes.tsx)

| From | To | Mechanism | Source |
|---|---|---|---|
| `/` | `/dashboard` | `<Navigate replace>` | frontend/src/routes/AppRoutes.tsx:108 |
| `/receiving-testing` | first lab workspace (MICRO, then FP areas) keeping query | `FirstLabWorkspaceRedirect` | frontend/src/routes/AppRoutes.tsx:133; frontend/src/modules/receivingTesting/LabWorkspaceRoute.tsx:128-141 |
| `/testing-workspace` | `/receiving-testing` (query preserved) | `LegacyRedirect` helper | frontend/src/routes/AppRoutes.tsx:151, 87-90 |
| `/laboratory-configuration/cause-of-testing` | `/laboratory-configuration/receiving-configuration` (query preserved) | `LegacyRedirect` | frontend/src/routes/AppRoutes.tsx:235 |
| `/inventory/approved-media` | `/laboratory-configuration/media?tab=approved` | `<Navigate replace>` | frontend/src/routes/AppRoutes.tsx:261 |
| `/inventory/approved-cryovials` | `/laboratory-configuration/cryovials?tab=approved` | `<Navigate replace>` | frontend/src/routes/AppRoutes.tsx:262 |
| Redirect inside Login | `/dashboard` after login; PublicRoutes sends logged-in users to `/dashboard` | navigate | frontend/src/pages/Login.tsx:37; frontend/src/routes/PublicRoutes.tsx:6-10 |
| Permission failure | `/dashboard` silently | `PermissionRoute` | frontend/src/routes/PermissionRoute.tsx:12 |
| Unauthenticated | `/login` | `AuthenticatedRoutes` | frontend/src/routes/AuthenticatedRoutes.tsx:6-10 |
| Forced password change | `/change-password` | MainLayout | frontend/src/layouts/MainLayout.tsx:65-67 |
| Idle timeout | `/login` after logout | MainLayout | frontend/src/layouts/MainLayout.tsx:56-59 |

Notes: the "legacy" comment blocks say "Backward-Compatible Query-Preserving Legacy Redirects" [src: frontend/src/routes/AppRoutes.tsx:147]. Dashboard lab selection uses `?lab=` [src: frontend/src/modules/dashboard/DashboardPage.tsx:25].

---

## 16. Mock or placeholder data found

Searched in-scope files for mock|fixture|sample data|placeholder data|coming soon|TODO|FIXME|dummy|hardcoded|lorem|demo. No "coming soon" or TODO in scope. Findings:

1. **Hard-coded fallback filter options in Trending tab** (shown instead of server options when the server list is empty):
   - Product/item options "Osteocare Liquid", "Feroglobin Syrup", "Honey (Raw Material)", "Purified Water (Loop 1)", "Point W-01 (Utilities)" [src: frontend/src/modules/reports/components/TrendingTab.tsx:250-254].
   - Parameter/test options "TAMC", "TYMC", "WATER_TAMC" ("Water TAMC (Purified Water)"), "PATHOGEN_ECOLI" [src: frontend/src/modules/reports/components/TrendingTab.tsx:218-223].
   - Category fallback options Finished Product, Raw Material, Water, Environmental Monitoring [src: frontend/src/modules/reports/components/TrendingTab.tsx:277-281].
2. **Hard-coded default criteria** on first open of Trending tab: testCode "TAMC", subjectName "Osteocare Liquid", category "FinishedProduct", dateRange "12m", compareWith "None" [src: frontend/src/modules/reports/components/TrendingTab.tsx:44-51] (the default subject is a product name, not a live lookup).
3. **Static label "Live Database Data"** on Reports Overview (static chip text, not a data flag) [src: frontend/src/modules/reports/components/OverviewTab.tsx:120-124].
4. **kpiFixtures.ts** (frontend/src/modules/kpi/kpiFixtures.ts:4) - "Test-only builders"; imported only from *.test.* files [src: frontend/src/modules/kpi/kpiFixtures.ts:4; git grep imports]. Not runtime data.
5. Placeholder text examples in inputs (e.g. "FP0107026", "B-2026-088", "CTRL-904", "MED-044", "CRYO-019", "XXXX-XXXX-XXXX") are input hints, not data [src: frontend/src/modules/auditSearch/components/AuditFilterBar.tsx:146-186; frontend/src/pages/AdminPasswordRecovery.tsx:92].
6. Login footer version string "MicroLIMS v0.1" is hard-coded [src: frontend/src/pages/Login.tsx:204].
7. Roles page: "Base type" radio labels show raw enum values [src: frontend/src/modules/roles/CreateRolePage.tsx:15, 129].

Not mock: frontend/src/modules/roles/CreateRolePage.tsx:42 comment says role data is "fetched live rather than hardcoded". frontend/src/modules/reports/components/CompareDialog.tsx:21 comment says no per-row mock data.

---

## 17. Out-of-scope routes (listed for completeness, NOT inventoried)

These routes exist in AppRoutes.tsx but belong to modules outside the requested scope: `/media/:id/report` [AppRoutes.tsx:104], `/cryovials/:id/report` [:105], `/laboratory-configuration/*` (media, media-evaluation, cryovials, items, test-master, fp-test-master, rm-pm-test-master, equation-types, organisms, master-data-workbook, specifications, media-configurations, water, environmental-monitoring, after-cleaning, receiving-configuration, equipment, fp-instruments, columns, material-master, solution-master, hplc-methods, icp-methods) [AppRoutes.tsx:158-242], `/preparation*` [:167-170], `/hplc-workspace*`, `/gc-workspace*`, `/icp-workspace*` [:174-209], `/working-standards` [:211], `/document-control*` [:246-252, 269], `/inventory/materials` [:255], `/inventory/equipment` [:258]. Note `/inventory/*` and `/document-control*` are routed in modules outside this scope.

---

## 18. Gaps and UNCLEAR items

- Dialog titles UNCLEAR (not located): DiscussionHistoryDialog (frontend/src/modules/discussions/components/DiscussionHistoryDialog.tsx:27-55 - title text not extracted), EditDiscussionDialog heading text beyond "Edit Discussion (Version N)" (read), NewDiscussionDialog heading read as "New Discussion".
- Not inventoried in detail (PARTIAL): testingWorkspace top-level panels and dialogs beyond SampleSummaryDialog, CloseTestingDialog and TestWorkflowDialog (e.g. SelectedSampleTestingPanel, SampleCardView, SampleKanbanView, SampleTableRow, LocationResultGridDialog, MeasurementPanel, QualitativePanel, DissolutionPanel, DisintegrationPanel, GravimetricPanel, TitrationPanel, WeightVariationPanel, PathogenSessionDialog and pathogenSteps/*, ICP review, sampleSummary/*); receiving SampleStatusKpiCards, SelectionSummaryBar, TestStatusSummaryCell; reports Media/GPT and Reference Strains tab labels; reports CompareDialog, RecordDetailDialog, TrendingDataDialog; AuditDiffViewer/AuditTraceabilityChain; DashboardPanels' individual tile labels for Analyst dashboard.
- Dashboard Analyst tile labels UNCLEAR.
- Users: UserSectionsDialog service calls UNCLEAR (uses laboratorySectionService, out of scope file).
- Users: exact hard-delete success toast text UNCLEAR.
- Roles list: none.
- OOS: void UI control UNCLEAR (service method at OosTrackingService.ts:93 exists; UI not located).
- Discussions: effect of `Discussions.EditAny` (routes.ts:38) on UI UNCLEAR.
- Role detail holders list: uses UserService.getAll then filters (filter logic not read beyond line 45).
- KPI workflow profile service per-call URLs beyond base UNCLEAR (frontend/src/modules/kpi/services/workflowProfileService.ts:17 only shows BASE).
- No file in scope could not be read. Full read was not performed on the largest files (ReceivingTestingWorkspacePage.tsx 1166 lines, SectionHeadDashboardPage.tsx 778, DashboardService usages, TestWorkflowDialog 833, SampleSummaryDialog 657): strings extracted via pattern search, so some labels may be missing.
