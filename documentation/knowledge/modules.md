# Modules

## C1 Platform & administration

<!-- from C1-platform.md -->

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

## C2 Sample flow & micro testing

<!-- from C2-sample-testing.md -->

### MOD-SAMPLE-001 Sample receiving and register
- Purpose: register incoming samples in six categories (Product, Raw Material, Packaging Material, Water, Environmental Monitoring, After Cleaning), create one test order per configured test, and maintain the sample record (correct, void, add laboratory, assign analyst). [src: frontend/src/modules/receiving/ReceivingPage.tsx:83-84; backend/MicroLIMS.Application/Services/ReceivingService.cs:21-27]
- Sub-features: receive; register table / card / kanban; correct details (signed); void (signed); add laboratory (signed); assign analyst (audit reason).
- Status: IMPLEMENTED.
- Lab scope: any laboratory for `Samples.Receive`; own laboratory only for `Samples.ReceiveOwnLab`. [src: verified-roles-permissions.md §receiving]

### MOD-TRACK-001 Tracking Board
- Purpose: read-only cross-laboratory view of each sample with each lab's stage. [src: frontend/src/modules/receiving/TrackingBoardPage.tsx:105]
- Status: IMPLEMENTED (UI + `GET /api/samples/tracking`; backend read in Phase 3, see BR-TRACK-001..003). (verified)
- Lab scope: all laboratories, deliberately not scoped to the caller's own section; gated by `Samples.TrackAll` on the controller class. [src: backend/MicroLIMS.API/Controllers/SampleTrackingController.cs:L16; backend/MicroLIMS.Application/Services/SampleTrackingService.cs:L10-L14] (verified)

### MOD-PREP-001 Sample preparation
- Purpose: record preparation before testing starts. Product / RM / PM: diluent and neutraliser, signed. EM: room grid. After Cleaning: machine-part grid. Water: sampling-point grid plus storage condition. Grouped preparation for several samples. [src: frontend/src/modules/testPreparation/PreparationDialog.tsx:70-101; backend/MicroLIMS.Application/Services/SamplePreparationService.cs]
- Status: IMPLEMENTED. Item preparation configuration and approval (WF-PREP-002) backend read in Phase 3 (BR-PREP-008..010); the approval screen is the Items > Preparation Configuration tab (**Approve**); remaining question in GAP-214 (corrected in Phase 13, RECONCILIATION.md).
- FP-section tests have no preparation stage: a sample whose tests are all in section code "FP" is Ready at receipt; a mixed sample needs preparation only for its non-FP tests. [src: backend/MicroLIMS.Application/Workflows/PreparationRules.cs:L7-L30] (verified, BR-PREP-011)
- Lab scope: preparation endpoints check section access (`EnsureSampleAccessAsync`) but have no permission policy beyond being signed in (EM, After Cleaning and Water receive/prepare endpoints have neither; see roles). [src: backend/MicroLIMS.API/Controllers/SamplePreparationController.cs:L45; EMController.cs:L33-L35]

### MOD-TESTING-001 Laboratory workspaces
- Purpose: one page per laboratory area to receive, prepare, start incubation, record results, review-submit and track the samples and assigned tests. [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:142]
- Status: IMPLEMENTED.
- Lab scope: Microbiology Workspace (MICRO members); FP Workspace (area `fp`); RM & PM Workspace (area `rmpm`). Route guard shows "You are not a member of the {lab name}." or "You do not have access to the FP workspace." / "You do not have access to the RM & PM workspace." [src: frontend/src/modules/receivingTesting/LabWorkspaceRoute.tsx:20-28, 65-79]

### MOD-COUNT-001 Count tests
- Purpose: plate-count tests (CFU) with media lot and incubator selection, optional two-stage incubation, plate readings, dilution factor, TNTC / Uncountable handling and calculated result. [src: frontend/src/modules/testingWorkspace/TestWorkflowDialog.tsx; backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs]
- Status: IMPLEMENTED. Lab scope: MICRO in practice; whether FP count tests exist is UNCLEAR (test definitions not checked).

### MOD-OBS-001 Observation and location-grid tests
- Purpose: (a) location grids for EM, After Cleaning and Water (count average per location; pathogen Yes/No per location); (b) qualitative observation. Qualitative panel is a physicochemical panel (also covered in C6).
- Status: IMPLEMENTED.

### MOD-PATH-001 Pathogen testing
- Purpose: step chain for pathogen tests (broth enrichment or shared TSB, selective plating, confirmatory plating on one or more media, biochemical test) and the six-step Pathogen Testing Session Workspace (shared TSB, primary matrix Location x Test, confirmatory batch, review and completion). [src: frontend/src/modules/testingWorkspace/PathogenStepDialog.tsx:143-166; frontend/src/modules/testingWorkspace/pathogenSession/PathogenSessionDialog.tsx:44-51]
- Status: IMPLEMENTED. Spec mismatch: see BR-PATH-016.

---

## C3 Review, approval, reports out

<!-- from C3-review-approval.md -->

### MOD-REVIEW-001 Technical review and return to analyst
- Purpose: a reviewer signs a technical review per laboratory section once every current test in that section is Ready [src: backend/MicroLIMS.Application/Services/SampleReviewService.cs:L13-L17, L98-L155]. IMPLEMENTED.
- Auto-submit: when the last test of a section reaches Ready, the section moves to UnderReview with event text "All tests completed - automatically submitted for review". Only runs while the sample status is Received or InTesting, and only for a section that has at least one current test and is still InTesting [src: backend/MicroLIMS.Application/Services/SampleReviewService.cs:L43-L55, L72-L93]. IMPLEMENTED (verified).
- Sub-features: "Submit Technical Review" (signed); "Return to Analyst" (unsigned) [src: frontend/src/modules/testingWorkspace/sampleSummary/ApprovalSignaturesCard.tsx:235-283; frontend/src/modules/testingWorkspace/sampleSummary/TestResultsSection.tsx:109-129].
- Lab scope: section-scoped (GetAccessibleSectionIdsAsync; null means all sections) [src: backend/MicroLIMS.Application/Services/SampleReviewService.cs:L103-L104]. Physicochemical review screens (HPLC, ICP send-for-review) are not in this cluster; UNCLEAR whether they use this path.
- Spec status: INTENDED design differs (per test order, Quick Table review). DISCREPANCY, see GAP-018.

### MOD-APPROVAL-001 Approval, retest, new sample, close testing
- Purpose: a Section Head signs a release decision per section: Approve, Reject, Retest Retained Sample, New Sample Required (NewSampleRequest) [src: backend/MicroLIMS.Application/Services/SampleApprovalService.cs:L12-L19, L136-L141]. IMPLEMENTED.
- Close testing: after another section rejects the sample, a section may cancel its own open tests with a signed reason [src: backend/MicroLIMS.Application/Services/SectionClosureService.cs:L14-L22, L31-L100]. IMPLEMENTED.
- Lab scope: section-scoped, same scope service as review [src: backend/MicroLIMS.Application/Services/SampleApprovalService.cs:L152-L153].
- Spec status: BusinessRules.md section 9 describes per-test-order approval and two more decisions (Investigation, OOSInvestigation). DISCREPANCY, see GAP-017, GAP-018.

### MOD-COA-001 Sample report and Certificate of Analysis
- Printable sample summary report page, Certificate of Analysis page, server PDF export of the summary, and frozen archived PDF written at the final decision [src: frontend/src/routes/AppRoutes.tsx:102-103; backend/MicroLIMS.API/Controllers/SampleSummaryController.cs:L44-L51; backend/MicroLIMS.Application/Services/RecordArchiveService.cs:L12-L20]. IMPLEMENTED.
- Status: the application map lists this as PARTIAL; the full SampleCoaPage.tsx and SampleReportPage.tsx were read in Phase 3 and are described under SCR-COA-001 and SCR-COA-002 (verified).
- Lab scope: report and CoA routes have no route guard; server summary is scoped by section for non-final samples [src: backend/MicroLIMS.Application/Services/SampleSummaryService.cs:L84-L90].

### MOD-OOS-001 OOS Tracking
- Purpose: groups every retest spin-off of an OOS chain under one OosGroupCode, and holds lab investigation documents for the group [src: backend/MicroLIMS.Application/Services/OosTrackingService.cs:L11-L24]. IMPLEMENTED.
- There is no separate OOS record entity. The group is the set of Sample rows sharing OosGroupCode; the only other OOS entity is OosInvestigationDocument [src: backend/MicroLIMS.Domain/Entities/OosInvestigationDocument.cs:L12-L43; git grep of backend/MicroLIMS.Domain/Entities].
- Lab scope: list filtered by section when the caller is section-scoped [src: backend/MicroLIMS.Application/Services/OosTrackingService.cs:L24-L34].

### MOD-RESULT-001 Results list (backend)
- GET /api/results, class-level [Authorize] only. No UI caller found in this cluster [src: backend/MicroLIMS.API/Controllers/ResultController.cs:L10-L22]. Response shape: not read; no screen in this cluster uses it (UNCLEAR, out of scope).

## C4 Monitoring & micro configuration

<!-- from C4-monitoring-microconfig.md -->

### MOD-EM-001 Environmental Monitoring configuration
- Purpose: configure EM departments (name, class, testing frequency), rooms (with grade A to D) and per-room test configurations (test type, test code, alert / action / specification limits, unit). [src: frontend/src/modules/laboratoryConfiguration/environmentalMonitoring/EMConfigPage.tsx:129; backend/MicroLIMS.Application/Services/MasterData/EnvironmentalMonitoringMasterDataService.cs:L25-L181] (verified)
- Sub-features: Departments; Rooms; Room tests (test type, test, limits, unit). [src: environmentalMonitoring/components/EmPanels.tsx:33-240]
- Status: IMPLEMENTED (configuration and API). The EM receive and preparation flows are in C2 (WF-SAMPLE-006, MOD-PREP-001).
- Lab scope: microbiology. The page is guarded by `MasterData.Manage`; the three write families (department, room, room test configuration) are `[Authorize(Policy = MasterData.Manage)]`; the GET endpoints are `[Authorize]` only. Not laboratory-section scoped. [src: backend/MicroLIMS.API/Controllers/EnvironmentalMonitoringMasterDataController.cs:L21-L88] (verified)
- "Testing frequency" is informational: the field is stored as text and no scheduling or reminder code uses it. [src: backend/MicroLIMS.Domain/Entities/Department.cs:L9; `git grep TestingFrequency` over backend returned only the entity, DTOs and master-data services] (verified)

### MOD-WATER-001 Water configuration
- Purpose: configure water systems (entity WaterDepartment, name only), sample locations (entity WaterSamplingPoint: point code, point name, water system, testing frequency, assigned test codes) and per-location limits for the assigned count tests (entity SamplingConfiguration). [src: frontend/src/modules/laboratoryConfiguration/water/WaterConfigPage.tsx:142; backend/MicroLIMS.Application/Services/MasterData/WaterMasterDataService.cs:L16-L157; backend/MicroLIMS.Domain/Entities/WaterDepartment.cs:L1-L13] (verified)
- Sub-features: Water systems (Add water system, Rename, delete); Sample locations (one side panel holds details, assigned tests and limits); filter buttons "All ({n})" and "Needs attention ({n})"; summary tiles "Water systems", "Sample locations", "Missing limits", "No tests assigned". [src: water/WaterConfigPage.tsx:140-200]
- Status: IMPLEMENTED. Water receive, preparation and results are in C2 (WF-SAMPLE-007, WF-COUNT-002).
- Lab scope: microbiology. Write endpoints (point, department, sampling configuration) are `MasterData.Manage`; GET endpoints `[Authorize]` only. [src: backend/MicroLIMS.API/Controllers/WaterMasterDataController.cs:L21-L87] (verified)

### MOD-AC-001 After Cleaning configuration
- Purpose: configure machines, machine parts, swab and rinse count tests per part (with limits), and pathogen (presence / absence) tests per part. [src: frontend/src/modules/laboratoryConfiguration/afterCleaning/AfterCleaningConfigPage.tsx:151; backend/MicroLIMS.Application/Services/MasterData/AfterCleaningMasterDataService.cs:L16-L172] (verified)
- Sub-features: Machines; Parts; Count tests per part (sample type Swab or Rinse, test, limits, unit); Pathogen tests per part (multi-select). [src: afterCleaning/components/AcPanels.tsx:30-270; AcPartCard.tsx:58-110]
- Status: IMPLEMENTED. After Cleaning receive and preparation are in C2 (WF-SAMPLE-008).
- Lab scope: microbiology. Writes `MasterData.Manage`; GET `machines` and `machine-part-configurations` are `[Authorize]` only (there is no GET endpoint for machine parts alone; parts come with the machine list). [src: backend/MicroLIMS.API/Controllers/AfterCleaningMasterDataController.cs:L21-L82] (verified)

### MOD-ORG-001 Organisms master
- Purpose: the canonical organism list (scientific name, ATCC number, common name, description) referenced by media configuration challenges, media evaluation challenges, cryovials and materials, and by Test Master step "target organism". [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/OrganismsPage.tsx:90-91; backend/MicroLIMS.Application/Services/MasterData/OrganismMasterDataService.cs:L16-L81; backend/MicroLIMS.Persistence/Configurations/TestWorkflowStepConfiguration.cs:L24-L27]
- Status: IMPLEMENTED (the "COVERED" label in the application map is kept as is). Not laboratory-scoped; the list is shared. Writes `MasterData.Manage`; GET `[Authorize]`. [src: backend/MicroLIMS.API/Controllers/OrganismMasterDataController.cs:L21-L48] (verified)

### MOD-MEDIACFG-001 Media Configurations
- Purpose: configure each dehydrated medium as a media product (name, unique code that prefixes lot numbers), its incubation conditions (hours and temperature range) and its evaluation configuration (Growth Promotion or Indication / Inhibition, with challenge organisms and recovery limits). [src: frontend/src/modules/laboratoryConfiguration/media/MediaConfigurationPage.tsx:138-139; backend/MicroLIMS.Application/Services/MediaProductService.cs:L13-L235; MediaIncubationConditionService.cs:L24-L166; MasterData/MediaMasterDataService.cs:L15-L290] (verified)
- Sub-features: media products (add, rename, change code (signed), delete); incubation conditions per product; one evaluation configuration per product; challenge organisms.
- Status: IMPLEMENTED (PARTIAL in the application map: lot preparation and evaluation are in MOD-MEDIA-001, C5).
- Lab scope: writes need `MasterData.Manage`; the product code change instead needs the role type Section Head (see roles). The page and the "Change code" button gate differently: the page needs the permission, the button needs `role === "SectionHead"`. [src: backend/MicroLIMS.API/Controllers/MediaMasterDataController.cs:L38-L107; frontend/src/modules/laboratoryConfiguration/media/MediaConfigurationPage.tsx:L23-L24; components/MediaProductWorkspace.tsx:L191] (verified)

### MOD-EQUIP-002 Equipment configuration
- Purpose: configure the microbiology equipment used by the laboratory (incubators, autoclaves, cabinets, water baths and others): master record, an optional link to an inventory asset, incubator set point (with reason and history) and autoclave programs (loads) with cycle time, temperature and history. [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/EquipmentPage.tsx:555-556; backend/MicroLIMS.Application/Services/MasterData/EquipmentMasterDataService.cs:L16-L149; backend/MicroLIMS.Application/Services/EquipmentConfigurationService.cs:L93-L430] (verified)
- Sub-features: tabs "Configured Equipment", "Autoclave Programs / Loads ({n})", "Configuration History"; buttons "Add Equipment" and "Select from Inventory"; "Edit Equipment"; "Edit Set Point"; "Add Program / Load". [src: EquipmentPage.tsx:564-601, 763-766, 884-887, 942]
- Status: IMPLEMENTED. Physicochemical instruments (HPLC, ICP-OES, pH meter, balance) are configured on the separate FP Instruments page (MOD-EQUIP-002 SCR-EQUIP-003 in the map; read in C6) and are filtered out of this page. [src: EquipmentPage.tsx:63-69, 686]
- Lab scope: the page offers only microbiology equipment types; the server list (`GET equipment`, `GET equipment/{id}`) is filtered to the caller's laboratory sections (`IUserSectionScopeService`; a System Administrator is unrestricted). [src: EquipmentPage.tsx:53-69; EquipmentMasterDataService.cs:L27-L46; backend/MicroLIMS.Application/Services/UserSectionScopeService.cs:L20-L30] (verified)
- Equipment inventory is a separate module (C5 / MOD-EQUIP-001). On this page the inventory dialog only pre-fills the Add Equipment form (see SCR-EQUIP-002); the API `link-inventory` endpoint is not called by the page. [src: EquipmentPage.tsx:409-433, 1549-1597; `git grep linkInventory` in frontend returns only the service definition] (verified)

### MOD-TESTMASTER-001 Test Master (Microbiology)
- Purpose: define microbiology tests (code, display name, laboratory section, workflow type), their ordered workflow steps (each with step type, target organism, step media with a chosen incubation condition, optional second incubation stage and phenotypic test types) and freeze or unfreeze tests. [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/TestMasterPage.tsx:35, 655-659; backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L17-L46; TestWorkflowStepMasterDataService.cs:L16-L333] (verified)
- Scope in this module: the microbiology page only (route `/laboratory-configuration/test-master`, `lab="micro"`). The FP and RM & PM Test Masters (titration, HPLC, ICP, dissolution, disintegration, weight variation, measurement, gravimetric, qualitative, stage replicates) are in C7 and are not described here.
- Microbiology workflow types: "Count Test" (CountTest) and "Observation" (Observation); both are step-driven. [src: frontend/.../testMaster/testMasterConstants.ts:L8-L25; backend/MicroLIMS.Application/Services/MasterData/TestWorkflowStepMasterDataService.cs:L32; backend/MicroLIMS.Domain/Enums/WorkflowType.cs:L5-L6] (verified)
- Status: IMPLEMENTED. Test execution (step chains, incubation, results) is in C2 (WF-COUNT-001, WF-PATH-001..003).
- Lab scope: a microbiology test belongs to the laboratory section chosen in the form; the test update is refused for a user outside the test's section ("This test belongs to a laboratory section you are not assigned to."). Freeze, unfreeze and all step writes carry no such check. [src: TestDefinitionMasterDataService.cs:L393-L394, L796-L813; TestWorkflowStepMasterDataService.cs:L25-L333] (verified)

## C5 Media, strains, stock

<!-- from C5-media-strains-stock.md -->

### MOD-MEDIA-001 Media Preparation, Evaluation, Release, Storage
- Purpose: prepare media lots from dehydrated media in Materials Stock; auto-assign an evaluation per lot; record evaluation results; signed release or rejection; storage entries; mark out of stock; approved media list; lot report.
- Sub-features: Media Lots register; Approved Media tab; Preparation Overview; Evaluations (N) tab; Release & Audit Trail tab; Storage tab (only when released); media evaluation list page; lot summary PDF/Word [src: frontend/src/modules/laboratoryConfiguration/media/MediaPage.tsx:L198-L199, L280-L283; backend/MicroLIMS.API/Controllers/MediaController.cs:L113-L131].
- Status label: IMPLEMENTED (application map lists PARTIAL) [src: documentation/01_APPLICATION_MAP.md:L72].
- Lab scope: MICRO (media products and GPT are micro). Routes do not read the lab parameter; FP scope UNCLEAR [src: documentation/knowledge/_raw/frontend-config-physchem-dc-screens.md:L43].
- Evaluation types: GrowthPromotion, IndicationInhibition, EnrichmentCharacteristics [src: backend/MicroLIMS.Domain/Enums/EvaluationType.cs:L5-L9]. Sterility is shown in UI text but no sterility type exists (see GAP-022).

### MOD-CRYO-001 Cryovials (reference strain batches)
- Purpose: prepare working cryovial batches from a Lyophilized Microorganism Material; identity confirmation against released media; signed approve or reject; thaw one vial; move storage; destroy; approved cryovial list; cryovial report.
- Status label: IMPLEMENTED (application map lists PARTIAL) [src: documentation/01_APPLICATION_MAP.md:L74].
- Lab scope: MICRO (lyophilized microorganisms, GPT media). Route lab scope not read (UNCLEAR).
- Sub-feature not found: passage number or generation count (see GAP-037).

### MOD-MATERIAL-001 Materials Stock
- Purpose: receive and track lots of reagents, media, standards, supplies and microorganisms; stock deduction on use; lot documents (CoA, supplier certificate, specification, SDS, other).
- Status label: IMPLEMENTED (application map lists PARTIAL) [src: documentation/01_APPLICATION_MAP.md:L76].
- Lab scope: section-scoped by `SectionId` [src: backend/MicroLIMS.Application/Services/MaterialService.cs:L117-L121].

### MOD-EQUIP-001 Equipment Inventory
- Purpose: register lab instruments (incubators, autoclaves, LAF cabinets, biological safety cabinets, water baths, other); operational status; calibration due date; calibration certificates; status history; active equipment and where-is-it lookup.
- Status label: IMPLEMENTED (application map lists PARTIAL) [src: documentation/01_APPLICATION_MAP.md:L77].
- Lab scope: section-scoped by equipment section (`UnauthorizedAccessException` when outside assigned section) [src: backend/MicroLIMS.Application/Services/EquipmentInventoryService.cs:L118-L127].

## C6 Physicochemical

<!-- from C6-physchem.md -->

### MOD-PHYSRESULT-001 Physicochemical result panels
- Purpose: enter and sign results for Measurement, Gravimetric, Qualitative, Titration, Dissolution, Disintegration and Weight Variation tests inside the test workflow dialog. [src: documentation/01_APPLICATION_MAP.md:L63, L279]
- Status: IMPLEMENTED (code). App map: PARTIAL. Lab scope: FP section. Test-level area (FP / RM&PM) is a C7 Test Master topic. [src: docs/superpowers/specs/2026-10-05-physchem-areas-design.md D8-D9]
- Panels: frontend/src/modules/testingWorkspace/{MeasurementPanel,GravimetricPanel,TitrationPanel,DissolutionPanel,DisintegrationPanel,WeightVariationPanel}.tsx; QualitativePanel.tsx (read in the verification pass, see BR-PHYSRESULT-002). [src: frontend/src/modules/testingWorkspace/]
- Endpoints (all Policy TestWorkflow.Execute; Password in body): POST /api/test-workflow/{testOrderId}/record-measurement-result [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L195]; record-gravimetric-result [:L214]; record-titration-result [:L238]; record-qualitative-result [:L256]; record-dissolution-result [:L276]; record-dissolution-stage [:L296]; record-disintegration-result [:L311]; record-disintegration-stage [:L329]; record-weight-variation-result [:L344]; record-weight-variation-stage [:L365]. GET titration-context [:L233].
- Calculators: GravimetricCalculator, TitrationEngine, DissolutionCalculator and DissolutionStageEvaluator, DisintegrationStageEvaluator, WeightVariationEvaluator (see business-rules).

### MOD-TITR-001 Titration tests
- Purpose: titration assay tests configured on the Test Master and recorded in the Titration panel, using titrant factors from MOD-SOLN-001.
- Status: IMPLEMENTED (code). INTENDED: docs/superpowers/specs/2026-10-03-titration-test-design.md (sections 1-7, amendments A1-A6 in section 8); docs/superpowers/specs/2026-10-05-physchem-areas-design.md section 2 (S2).
- Enum TitrationType { AcidBase, Redox, Complexometric, Precipitation, KarlFischer }; TitrationMode { Direct, Residual }; TitrationCalculation { UspFactor, Relative }; TitrationEndpoint { Visual, Potentiometric }. [src: backend/MicroLIMS.Domain/Enums/TitrationType.cs:L4; TitrationMode.cs:L3; TitrationCalculation.cs:L4; TitrationEndpoint.cs:L3] Matches spec section 3.1.
- Per-test fields and validation messages: see validations and business-rules. [src: backend/MicroLIMS.Application/Services/MasterData/TitrationDefinitionRules.cs:L20-L146]
- Replicates 1-10 for titration. [src: TitrationDefinitionRules.cs:L28] Titration tests do not require SST. [src: TitrationDefinitionRules.cs:L30]
- Screens: Test Master physchem page Titration section (not enumerated here; C7); Titration panel in TestWorkflowDialog (SCR-PHYSRESULT-001).

### MOD-HPLC-001 HPLC Workspace
- Purpose: instrument overview, run start, SST, sample assignment, replicate entry, send for review (signed), evidence upload, complete or abandon run, run history. GC (MOD-GC-001) reuses these screens.
- Status: IMPLEMENTED (code). INTENDED: docs/superpowers/specs/2026-09-29-hplc-chain-design.md sections 4-6 and D1-D12; docs/superpowers/specs/2026-10-02-retire-legacy-sst-design.md (dissolution in workspace); docs/superpowers/specs/2026-10-03-working-standard-qualification-design.md (qualification entry).
- Routes: /hplc-workspace/* with no PermissionRoute. [src: frontend/src/routes/AppRoutes.tsx:L174-L184] Writes require Hplc.Operate. [src: backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:L74-L165]
- Scope: section-scoped via GetAccessibleSectionIdsAsync. [src: backend/MicroLIMS.Application/Services/HplcRunService.cs:L790-L795]
- Technique: HplcTechniqueEquipment maps Hplc to EquipmentType.Hplc and Gc to EquipmentType.Gc. [src: backend/MicroLIMS.Application/Services/HplcRunService.cs:L18-L21]

### MOD-GC-001 GC Workspace
- Purpose: same workflow as HPLC on /gc-workspace, for GC assay and residual solvents (USP <467>, ppm).
- Status: IMPLEMENTED (code). INTENDED: docs/superpowers/specs/2026-10-03-icp-gc-workspaces-design.md section 3 (G1-G3).
- Technique taken from the URL path. [src: frontend/src/modules/hplcWorkspace/useTechnique.ts:L18-L45]
- Run wizard step lists: HPLC 4 steps including "Assign Mobile Phases"; GC 3 steps without it. [src: frontend/src/modules/hplcWorkspace/run/StartHplcRunWizard.tsx:L36-L37]

### MOD-ICP-001 ICP-OES Workspace
- Purpose: instrument overview, run start, calibration summary (signed), CCV, sample assignment, entry, send for review (signed), evidence, complete or abandon run.
- Status: IMPLEMENTED (code). INTENDED: docs/superpowers/specs/2026-10-03-icp-gc-workspaces-design.md section 4 (I1-I4).
- Routes: /icp-workspace/*, no PermissionRoute. [src: frontend/src/routes/AppRoutes.tsx:L200-L209] Writes require Hplc.Operate. [src: backend/MicroLIMS.API/Controllers/IcpWorkspaceController.cs:L70-L139]

### MOD-HPLCM-001 Chromatography Methods Master (HPLC and GC)
- Purpose: method master: name, abbreviation, technique, column, elution and detection, mobile phases, analytes, SST criteria, GC conditions and oven program, residual-solvent mode.
- Status: IMPLEMENTED. INTENDED: hplc-chain-design section 3.3; icp-gc-workspaces-design section 3.1.
- Route /laboratory-configuration/hplc-methods, guard MasterData.Manage. [src: frontend/src/routes/AppRoutes.tsx:L241] API GET is Authenticated; POST, PUT and PUT active require MasterData.Manage. [src: backend/MicroLIMS.API/Controllers/HplcMethodController.cs:L22-L45]
- Edits require a reason; no signature found in this controller. [src: backend/MicroLIMS.Application/Services/HplcMethodService.cs, message "A reason is required."]

### MOD-ICPM-001 ICP Methods Master
- Purpose: ICP-OES method: mode, elements and wavelengths, calibration levels, optional blank, ICV and CCV checks, sample-prep defaults.
- Status: IMPLEMENTED. INTENDED: icp-gc-workspaces-design section 4.1.
- Route /laboratory-configuration/icp-methods, guard MasterData.Manage. [src: frontend/src/routes/AppRoutes.tsx:L242] API: GET Authenticated; POST, PUT and PUT active require MasterData.Manage. [src: backend/MicroLIMS.API/Controllers/IcpMethodController.cs:L21-L44]

### MOD-SOLN-001 Solution Preparation and titrant standardization
- Purpose: prepare mobile phases (MP), diluents (DL) and volumetric titrants (VS) from master recipes against stock lots; expiry; titrant standardization.
- Status: IMPLEMENTED. INTENDED: hplc-chain-design section 4; working-standard spec D1 (solution preparation never takes a standard lot).
- Routes: /preparation, /preparation/new, /preparation/:id/edit, /preparation/:id; guard Solutions.Prepare. [src: frontend/src/routes/AppRoutes.tsx:L166-L170] API: /api/solution-preparations. [src: backend/MicroLIMS.API/Controllers/SolutionPreparationController.cs:L29-L93]
- Expiry worker runs every 5 minutes. [src: backend/MicroLIMS.API/BackgroundServices/SolutionPreparationExpiryWorker.cs:L14, L45]

### MOD-SOLNM-001 Solutions Master
- Purpose: master recipes for mobile phases, diluents and titrants: components, shelf life, pH target and tolerance, titrant strength and standardization rules.
- Status: IMPLEMENTED. INTENDED: hplc-chain-design section 3.2 (titrant extras) and D7 (edit in place, reason, audit).
- Route /laboratory-configuration/solution-master, guard MasterData.Manage. [src: frontend/src/routes/AppRoutes.tsx:L240] API: GET Authenticated; POST, PUT and PUT active require MasterData.Manage. [src: backend/MicroLIMS.API/Controllers/SolutionMasterController.cs:L22-L41]

### MOD-REAGENT-001 Reagents and Reference Standards (material master)
- Purpose: master definitions of reagents, indicators, reference standards and primary standards.
- Status: IMPLEMENTED. INTENDED: hplc-chain-design section 3.1 (D9, D10); physchem-areas-design section 1 (PrimaryStandard type, D4).
- Route /laboratory-configuration/material-master, guard MasterData.Manage. [src: frontend/src/routes/AppRoutes.tsx:L239] API: GET Authenticated; POST, PUT and PUT active require MasterData.Manage. [src: backend/MicroLIMS.API/Controllers/MaterialMasterController.cs:L22-L37]
- Code uniqueness: "Material master entry code ... already exists." [src: backend/MicroLIMS.Application/Services/MaterialMasterService.cs:L67, L106]

### MOD-WS-001 Working Standards
- Purpose: in-house working standard lots created by qualification against a primary reference standard (HPLC assay), dried-basis potency, review, approve and reject sign-off, 12-month validity.
- Status: IMPLEMENTED. INTENDED: docs/superpowers/specs/2026-10-03-working-standard-qualification-design.md (D1-D12, amendments 1-6).
- Route /working-standards with no PermissionRoute. [src: frontend/src/routes/AppRoutes.tsx:L211] API: /api/working-standards. [src: backend/MicroLIMS.API/Controllers/WorkingStandardsController.cs:L30-L103]

### MOD-COLUMN-001 Chromatography Columns
- Purpose: column master records (designation, parameters, instrument compatibility, deactivate). Every HPLC or GC run stores the column it used.
- Status: IMPLEMENTED. INTENDED: hplc-chain-design D12 (run records the physical column, compatible with the instrument).
- Route /laboratory-configuration/columns, guard MasterData.Manage. [src: frontend/src/routes/AppRoutes.tsx:L238] API: GET Authenticated; POST, PUT and deactivate require Equipment.Manage. [src: backend/MicroLIMS.API/Controllers/EquipmentMasterDataController.cs:L129-L148] DISCREPANCY CONFIRMED (code vs code): the page guard is MasterData.Manage but the write endpoints require Equipment.Manage (verified). With default roles the SectionHead holds both and can use the page; the Analyst holds Equipment.Manage but not MasterData.Manage, so cannot open the page. REQUIRES CONFIRMATION (GAP-607).
- Run column link: HplcRun.ChromatographyColumnId. [src: backend/MicroLIMS.Domain/Entities/HplcRun.cs:L19]

### MOD-EQTYPE-001 Equation Types
- Purpose: read-only list of predefined equation types used by test definitions.
- Status: IMPLEMENTED as a read list. Route /laboratory-configuration/equation-types, guard MasterData.Manage. [src: frontend/src/routes/AppRoutes.tsx:L226] API: GET /api/masterdata/equation-types only. [src: backend/MicroLIMS.API/Controllers/TestDefinitionMasterDataController.cs:L59] No create or update endpoint found. The list is hard-coded in the service and shows six entries only: None, System Suitability, Numeric Measurement, Gravimetric % Loss, Gravimetric % Residue, Qualitative / Identification, each with a formula text and required inputs; the newer equation types (Dissolution, Disintegration, Weight Variation, Titration, HPLC method assay, ICP method assay) are not listed. Read-only by design: "Equation Types" = hardcoded read-only list endpoint [docs/FP_HPLC_Phase3_Build_Spec.md:L71, L127; docs/FP_Calibration_Curve_Phase0_Recon.md:L12] (INTENDED, verified). [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L815-L857]

### MOD-INSTIMPORT-001 Instrument report reading (OCR and parsers)
- Purpose (code): read instrument report text (PDF text or OCR) and parse Empower-style HPLC reports and ICP-OES reports.
- Status: NOT EXPOSED IN UI. [src: documentation/01_APPLICATION_MAP.md:L109]. Facts in business-rules (BR-PHYSCHEM-INSTIMPORT-001).

## C7 General masters

<!-- from C7-masters.md -->

### MOD-ITEM-001 Items
- Purpose: Master configuration for tested catalog items across Product, Raw Material, and Packaging Material categories; defines item identity (name, code, category, SOP number), tests auto-assigned upon sample receipt, controlled document management (SOP and Verification Report), and item-specific microbiology preparation configurations (technique, amount, filtration/washing volumes, diluent, neutralizer) with a Section Head approval click (no electronic signature in the approval itself, see BR-ITEM-007); supports active/frozen lifecycle gating and delete restriction safeguards. Menu group: GENERAL LABORATORY CONFIGURATION (verified) [src: frontend/src/routes/menuConfig.ts:L210]. [src: frontend/src/modules/laboratoryConfiguration/items/ItemsPage.tsx:L11-L202; backend/MicroLIMS.Application/Services/ItemService.cs:L12-L130; backend/MicroLIMS.Application/Services/ItemPreparationConfigurationService.cs:L26-L99; backend/MicroLIMS.Application/Services/ItemDocumentService.cs:L17-L115]
- Sub-features: SCR-ITEM-001. Status: IMPLEMENTED. Lab scope: shared across all laboratories (MICRO and Physicochemical FP / RM&PM); item master rows carry no lab field, and sample receipt creates one test order per assigned test, each in the section that owns that test (verified) [src: backend/MicroLIMS.Application/Workflows/ProductWorkflowEngine.cs:L89-L122]. [src: backend/MicroLIMS.Domain/Entities/Item.cs:L8-L34; backend/MicroLIMS.Application/Services/ItemService.cs:L18-L23]

### MOD-SPEC-001 Specifications (stand-alone page)
- Purpose: Routed URL destination (`/laboratory-configuration/specifications`) intended as a dedicated specification management interface; in code it redirects immediately (`<Navigate to="/laboratory-configuration/items" replace />`) to the Items page where specifications are configured inside the item workspace. Status: IMPLEMENTED (as redirect; see GAP-034 and GAP-702). Lab scope: none directly (redirects to Items). [src: frontend/src/modules/laboratoryConfiguration/specifications/SpecificationsPage.tsx:L4-L6; frontend/src/routes/AppRoutes.tsx:L229]
- Sub-features: SCR-SPEC-001. Status: IMPLEMENTED (Redirect only).

### MOD-RECVCFG-001 Receiving Configuration
- Purpose: Master reference lists offered as selection choices on the sample receiving form across all six receiving categories; manages Samplers (free suggestion picklist), Cause of Testing (mandatory foreign key with delete protection against referenced samples), and Production Stages (name and role mapping: Bulk, InProcess, Finished, Stability, Other). Status: IMPLEMENTED. Lab scope: global across all receiving categories and laboratories. [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ReceivingConfigurationPage.tsx:L49-L338; backend/MicroLIMS.Application/Services/MasterData/ReferenceListMasterDataService.cs:L14-L151]
- Sub-features: SCR-RECVCFG-001. Status: IMPLEMENTED.

### MOD-WORKBOOK-001 Master Data Workbook
- Purpose: Bulk data onboarding and migration tool using an Excel workbook (`.xlsx`); allows downloading current master data with instruction and choices sheets, dry-run checking (validation report table with sheet summaries and row-level errors), signed loading with electronic signature (`SignatureMeaning.MasterDataChanged`), and second-person verification (`SignatureMeaning.Reviewed`) that activates newly loaded items and materials; strictly add-only, transactions with savepoints per row, max 10 MB file size, max 5,000 new rows. Any row error in a signed load rolls the whole load back (nothing is half-loaded; see BR-WORKBOOK-004). Status: IMPLEMENTED. INTENDED source: docs/superpowers/specs/2026-10-05-master-data-workbook-design.md and docs/validation/ML-SOP-MDW-001_Master_Data_Workbook_User_Guide.md (Draft v0.1) [src: docs/superpowers/specs/2026-10-05-master-data-workbook-design.md:L1-L140; docs/validation/ML-SOP-MDW-001_Master_Data_Workbook_User_Guide.md:L1-L308]. Lab scope: cross-laboratory (Microbiology, Physicochemical FP and RM&PM). [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L12-L114; backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L16-L284; backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookRunner.cs:L6-L122]
- Sub-features: SCR-WORKBOOK-001. Status: IMPLEMENTED.

### MOD-TESTMASTER-001 Test Master (FP and RM & PM)
- Purpose: Canonical Test Master defining test codes, display names, owning laboratory section, workflow types, equation types, system suitability requirements, condition fields, replicate counts, stage-specific replicates (per ProductionStageRole), and specialized testing regimes (HPLC method assays, ICP method assays, titration configurations with reason-audited changes, dissolution S1-S3 stage offsets, disintegration multi-stage unit limits, and USP <2091> weight variation criteria). Status: IMPLEMENTED. Lab scope: Physicochemical Laboratory (`lab="fp"`), scoped by area: finished products (`area="fp"`) and raw/packaging materials (`area="rmpm"`). [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/TestMasterPage.tsx:L24-L140; backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L15-L820; backend/MicroLIMS.Application/Services/MasterData/TestStageReplicateMasterDataService.cs:L13-L110]
- Sub-features: SCR-TESTMASTER-002 (FP Test Master), SCR-TESTMASTER-003 (RM & PM Test Master). Status: IMPLEMENTED. The Microbiology Test Master (`/laboratory-configuration/test-master`, `TestMasterPage lab="micro"`) shares the same component but is documented in cluster C4, not here [src: frontend/src/routes/AppRoutes.tsx:L223]. Note: the physchem Test Master has no electronic signature on create/edit; only titration setting changes require a typed reason (see BR-TESTMASTER-006).

## C8 Reports, KPI, Document Control

<!-- from C8-reports-dc.md -->

### MOD-REPORTS-001 Reports
- Purpose: Read-only query, analytical trending, multi-subject comparison, and tabular reporting across laboratory test results (ResultRecord flattened projection), media preparation and growth promotion testing (MediaEvaluation and challenges), and reference microorganism cryovial batches; provides multi-criteria filtering, CSV and PDF exports with audit logging, and deep-link drill-down to underlying laboratory records. [src: frontend/src/pages/Reports.tsx:L16-L157; backend/MicroLIMS.API/Controllers/ReportingController.cs:L17-L51; backend/MicroLIMS.Application/Services/ReportingQueryService.cs:L89-L93]
- Sub-features: SCR-REPORTS-001 (Overview tab), SCR-REPORTS-002 (Record Search tab), SCR-REPORTS-003 (Media / GPT tab), SCR-REPORTS-004 (Reference Strains tab), SCR-REPORTS-005 (Trending & Analysis tab). Status: IMPLEMENTED (with hardcoded fallback options and mock-styled deltas; see GAP-800, GAP-801). Lab scope: Cross-laboratory (Microbiology, Physicochemical FP and RM&PM); queries are scoped server-side to the user's accessible laboratory sections via `IUserSectionScopeService.GetAccessibleSectionIdsAsync`. [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L36-L38]

### MOD-REPORTS-002 Reporting backfill
- Purpose: Standalone administrative synchronization endpoint (`POST /api/admin/reporting/backfill`) that populates and maintains the flattened `ResultRecord` read-model from operational tables (`CountTestReading`, `WorkflowStepResult` for pathogens, `SampleLocation` for EM/water, `ParameterResult` for chemical/physicochemical), derives 1-based test order rounds, and refreshes sample-level approval timestamps and usernames for historically decided samples. [src: backend/MicroLIMS.API/Controllers/ReportingAdminController.cs:L9-L28; backend/MicroLIMS.Application/Services/ResultProjectionService.cs:L12-L20, L409-L514]
- Sub-features: None (backend endpoint only). Status: NOT-EXPOSED-IN-UI (see GAP-803). Lab scope: Global / system-wide across all laboratory sections. Gated to SystemAdministrator via `Reporting.Admin` permission. [src: backend/MicroLIMS.API/Controllers/ReportingAdminController.cs:L15]

### MOD-KPI-001 KPI and Workflow Profiles
- Purpose: Operational performance monitoring dashboard and workflow profile management; renders 8 headline performance tiles (Sample turnaround time, On time all stages, Right first time, Retest rate, OOS rate, WIP backlog, Incubation reads on time, EM excursion rate), 3 category breakdown sections (Flow, Quality, Micro), an interactive analyst workload table with capacity planning metrics (1 unit = 1 standard analyst-hour), definitions drawer, and signed workflow profile versioning (append-only, electronic signature with `SignatureMeaning.MasterDataChanged`). [src: docs/superpowers/plans/2026-10-11-kpi-remodel-index.md:L1-L22; frontend/src/modules/kpi/components/KpiPerformanceTab.tsx:L21-L127; backend/MicroLIMS.API/Controllers/KpiDashboardController.cs:L10-L44; backend/MicroLIMS.API/Controllers/WorkflowProfilesController.cs:L10-L51; backend/MicroLIMS.Application/Services/Kpi/KpiRegistry.cs:L3-L18; backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L12-L91]
- Sub-features: SCR-KPI-001 (KPI / Performance tab), SCR-KPI-002 (Workflow Profiles dialog/panel). Status: IMPLEMENTED. Lab scope: Scoped to caller's accessible laboratory sections (`IUserSectionScopeService.GetAccessibleSectionIdsAsync`). Users without `Kpi.ViewTeam` are restricted to their own individual figures (D8) in the workload table and the analyst filter only; the headline tiles stay laboratory-level for every `Kpi.View` holder (verified). [src: backend/MicroLIMS.API/Controllers/KpiDashboardController.cs:L14, L22; backend/MicroLIMS.Application/Services/Kpi/KpiDashboardService.cs:L105-L107]

### MOD-KPI-002 Analyst availability and KPI section settings
- Purpose: Backend data model and REST endpoints for configuring per-section capacity parameters (working weekdays bitmask, standard hours per analyst-day, and forward load horizon) and recording individual analyst availability adjustments (leave, training, secondments reducing capacity fractions from 0.0 to 1.0); non-overlapping date windows with reason audit. [src: backend/MicroLIMS.API/Controllers/KpiWorkloadController.cs:L10-L74; backend/MicroLIMS.Application/Services/Kpi/KpiSettingsService.cs:L12-L85; backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs:L12-L117; frontend/src/modules/kpi/services/kpiWorkloadService.ts:L44-L67]
- Sub-features: None (service exists in frontend `kpiWorkloadService.ts`, but no UI screen, form, or dialog caller exists in the client application; see GAP-802). Status: NOT-EXPOSED-IN-UI. Lab scope: Scoped per laboratory section (`DocumentSections`); editing requires `Kpi.ConfigureProfiles` and section access. [src: backend/MicroLIMS.API/Controllers/KpiWorkloadController.cs:L56, L65, L70]

### MOD-DC-001 Document Control: library and document lifecycle
- Purpose: Enterprise Document Management System (EDMS) for laboratory controlled documents (SOPs, test methods, specifications, policies, forms); provides document master registration, metadata governance, versioned draft authoring, change rationale itemization, multi-category impact assessment, multi-cycle technical review with finding resolution ladder, formal approval with a password-confirmed electronic signature, dual-PDF side-by-side comparison, automated effective date transitions, periodic review workspace, voiding, and draft cancellation. [src: frontend/src/modules/documentControl/pages/DocumentLibraryPage.tsx:L57-L611; frontend/src/modules/documentControl/pages/DocumentDetailPage.tsx:L82-L1414; backend/MicroLIMS.API/Controllers/DocumentControl/DocumentControlController.cs:L17-L120; backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L12-L35; backend/MicroLIMS.Application/Services/DocumentControl/DocumentRevisionService.cs:L13-L55; backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L15-L65; backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L20-L75; backend/MicroLIMS.Application/Services/DocumentControl/PeriodicReviewService.cs:L20-L55]
- Sub-features: SCR-DC-001 (Document Control Dashboard), SCR-DC-005 (Document Library), SCR-DC-006 (Document detail, revisions, workflow, and lifecycle dialogs). Status: IMPLEMENTED. Lab scope: Cross-laboratory / Enterprise-wide (organized by DocumentDepartment and DocumentSection taxonomies). [src: backend/MicroLIMS.Domain/Entities/DocumentMaster.cs:L1-L40]

### MOD-DC-002 Document Control: reading and training
- Purpose: Controlled document training curricula and personal reading lists; automates training assignment generation upon document revision activation, tracks reading progress, enforces conscious legal reading acknowledgement with frozen evidentiary statement capture, maintains an organizational training matrix cross-referencing personnel against active SOPs, and calculates executive training compliance metrics. [src: frontend/src/modules/documentControl/pages/MyReadingListPage.tsx:L86-L758; frontend/src/modules/documentControl/pages/TrainingMatrixPage.tsx:L59-L816; frontend/src/modules/documentControl/pages/ComplianceDashboardPage.tsx:L37-L442; backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs:L17-L45; backend/MicroLIMS.Application/Services/DocumentControl/TrainingAssignmentService.cs:L14-L48; backend/MicroLIMS.Application/Services/DocumentControl/TrainingMatrixService.cs:L16-L35]
- Sub-features: SCR-DC-002 (My Reading List), SCR-DC-003 (Training Matrix), SCR-DC-004 (Training Compliance Dashboard). Status: IMPLEMENTED. Lab scope: Cross-laboratory / Enterprise-wide. [src: backend/MicroLIMS.Domain/Entities/DocumentTrainingAssignment.cs:L1-L35]

### MOD-DC-003 Document Control: audit trail
- Purpose: Append-only, chronological audit trail and specialized query interface across all document control events, revision lifecycle transitions, file uploads/replacements, controlled PDF and source Word views/downloads, cryptographic integrity check failures, technical review findings, approval ceremonies, periodic reviews, reading acknowledgements, configuration updates, and security violations; provides multi-criteria filtering and CSV export up to 5,000 records with logged export events. [src: frontend/src/modules/documentControl/pages/DocumentAuditPage.tsx:L36-L319; backend/MicroLIMS.API/Controllers/DocumentControl/DocumentAuditController.cs:L15-L52; backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuditService.cs:L12-L120]
- Sub-features: SCR-DC-007 (Document Control Audit Trail). Status: IMPLEMENTED. Lab scope: not laboratory-scoped; the screen shows Document Control rows only and answers only users whose role type is Reviewer, Section Head or System Administrator (verified) [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuditService.cs:L33-L35, L141-L152].

### MOD-DC-004 Document Control: configuration
- Purpose: Administrative configuration of Document Types (with default review cycles), Departments, Sections, document numbering sequence configuration (prefix, digit format, sequence counter), and global module settings (e.g. controlled copy download permissions). [src: frontend/src/modules/documentControl/pages/DocumentConfigurationPage.tsx:L46-L684; backend/MicroLIMS.API/Controllers/DocumentControl/DocumentConfigurationController.cs:L15-L150; backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs:L12-L100]
- Sub-features: SCR-DC-008 (Document Control Configuration). Status: IMPLEMENTED. Lab scope: global system configuration. Writes need the permission `Documents.ConfigManage` AND the System Administrator role type; reading the lists needs only sign-in (verified) [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentConfigurationController.cs:L37; backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuthorizationService.cs:L229-L233].

### MOD-DC-005 Document escalations (backend)
- Purpose: Automated and manual evaluation of document training assignments nearing due dates or overdue; manages three-tiered escalation triggers (Level 1: ApproachingDue, Level 2: Due, Level 3: Overdue) with recipient routing (AssignedUser, Manager, QACompliance), automatic status transitions to Overdue, escalation history retrieval, and manager resolution with regulatory audit. [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentEscalationController.cs:L16-L91; backend/MicroLIMS.Application/Services/DocumentControl/DocumentEscalationService.cs:L18-L180]
- Sub-features: None (backend REST API endpoints and the background worker `DocumentEffectiveDateWorker` only; the same worker, every 60 minutes by default, also activates FutureEffective revisions, creates periodic review tasks and raises these escalations; see GAP-807). Status: NOT-EXPOSED-IN-UI. Lab scope: System-wide. [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentEscalationController.cs:L18, L51-L87; backend/MicroLIMS.API/BackgroundServices/DocumentEffectiveDateWorker.cs:L17, L103-L143].
