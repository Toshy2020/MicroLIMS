# C2 Sample flow & micro testing

Evidence: code read directly for backend services and recorders; frontend read from `documentation/knowledge/_raw/frontend-core-screens.md` §3-5 and `frontend-testing-workspace.md` (paths cited there), plus targeted reads. Physicochemical panels are out of scope (C6). Application not run. Labels: IMPLEMENTED = verified in code; INTENDED = spec only; REQUIRES CONFIRMATION; DISCREPANCY.

Spec sources read: `documents/DesignSpecification/BusinessRules.md` §1-§6 and `WorkflowDiagrams.md` (product, pathogen, water, EM, After Cleaning sections).

Phase 3 verification (Sonnet pass): every role, permission, signature, time-gate, calculation and status claim below was re-checked against the cited code and the services it calls. Items marked "(verified)" were opened and confirmed; items marked "(corrected)" replace an earlier wrong statement; unsupported claims were removed. Read in addition to the Haiku pass: SampleTrackingService, EMController/EMService/EMWorkflowEngine, WaterController/WaterWorkflowEngine, AfterCleaningController/AfterCleaningWorkflowEngine, SamplePreparationController, ItemPreparationConfigurationController/Service, PreparationParameterValidator, SampleSectionRollup, SpecLimitParser, ConfirmationAgreementEvaluator, SingleResultRecorder (qualitative), GroupedTestActionService (batch select media), BatchSelectMediaRules, IncubatorEligibilityService, SectionMediaRule.

## modules

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
- Status: IMPLEMENTED. Item preparation configuration and approval (WF-PREP-002) backend read in Phase 3 (BR-PREP-008..010); UI screen still not located (GAP-214).
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

## screens

Count: 17 SCR ids in map (SCR-SAMPLE-001..006, SCR-TRACK-001, SCR-PREP-001, SCR-TESTING-001..005, SCR-COUNT-001, SCR-OBS-001, SCR-PATH-001..002) plus unnumbered components listed under each module. No new SCR ids assigned.

### MOD-SAMPLE-001 Sample receiving and register

#### SCR-SAMPLE-001 Receive Sample
- Route `/receiving`; guard: any of `Samples.Receive` or `Samples.ReceiveOwnLab`. Title "Receive Sample". Subtitle "Register incoming Finished Product, Raw Material, and Packaging Material samples, and choose which laboratories they go to." [src: frontend/src/routes/AppRoutes.tsx:140-142; frontend/src/modules/receiving/ReceivingPage.tsx:83-84]
- Buttons: "Refresh" (outlined); "Receive" (contained, shown when `canReceiveAny`). [src: ReceivingPage.tsx:87-104]
- KPI cards (labels, hints): "Needs Preparation" ("Preparation not yet signed off"); "Ready to Read" ("Incubation complete, awaiting result entry"); "Awaiting Review" ("All tests complete, awaiting reviewer"); "Overdue" ("Open more than 24 hours after receipt"); "Assigned to Me" ("Samples with a test assigned to you"); "Unassigned" ("No analyst assigned yet"). [src: frontend/src/modules/receiving/components/SampleStatusKpiCards.tsx:54-89]
- Filter bar: search placeholder "Search by item, reference number, control number, batch number..."; "Item Type" (option "All Item Types"); "Sample Status" (options listed in fields below); "Test Status"; "Received From"; "Received To"; view toggle aria labels "Table view", "Card view", "Kanban view". [src: frontend/src/modules/receiving/components/SampleFilterBar.tsx:74, 100-109, 151-220]
- Sample Status options: "All Sample Statuses", "Received", "Under Testing", "Pending Review", "Under Review", "Under Approval", "Approved"; remainder of list UNCLEAR. [src: frontend/src/modules/receiving/constants/receivingConstants.ts:49-55]
- Register status labels: Received -> "Received"; InTesting -> "Under Testing"; UnderReview and PendingReview -> "Pending Review"; UnderApproval -> "Pending Approval"; Approved; Rejected; RetestRequested, Cancelled, Voided -> all "Cancelled / Voided". [src: frontend/src/modules/receiving/components/SampleRegisterTable.tsx:60-69]
- Register column headers (table view), in order: "Received At", "#", "Item / Reference" (with select-all-on-page checkbox), "Item Type", "Cause", "Sampled By", "Batch / Control No.", "Assigned To", "Sample Status", "Test Status Summary", "Actions". [src: SampleRegisterTable.tsx:532-580] (GAP-043 columns closed)
- Register row extras: retest chain chip ("Retest", tooltip "Expand retest chain" / "Collapse retest chain") and "Unassigned" chip. Analyst chip tooltips: "Click to reassign analyst (Section Head)" and "Assign responsible analyst (Section Head)". [src: SampleRegisterTable.tsx:324, 340, 415, 448, 473]
- Selection bar: "{n} sample(s) selected"; chip "{n} hidden by current filters"; hint "Select at least one more to open grouped preparation and incubation setup." / "Grouped actions are available for this selection." / "Showing only the selected samples; other filters are paused."; buttons "Show selected only" / "Back to filters" (shown when samples are hidden or already showing selection only) and "Clear selection". [src: frontend/src/modules/receiving/components/SelectionSummaryBar.tsx:60-101] (GAP-043 selection bar closed)
- Row action menu (SampleActionMenu). Items: "Assign Analyst" (`Samples.AssignAnalyst`), "Add Laboratory" (shown when the user holds `Samples.Receive` and the sample status is not Rejected, Voided or Cancelled), "Prepare Sample", "Void Sample", "View Full Report", "Audit History", edit. Edit requires `Samples.Correct`; it is enabled only when `sample.canEditDetails` (tooltip "Edit Sample Details", else "Locked: the sample has been submitted for review"). Void requires `Samples.Correct` and status not "Voided". [src: frontend/src/modules/receiving/components/SampleActionMenu.tsx:52-73, 83-112] (verified)
- Notifications: "Failed to load the sample register."; "Sample details corrected and signed."; "Sample voided."; "Laboratory added to the sample." [src: frontend/src/modules/receiving/ReceivingPage.tsx:58, 138, 151, 164]
- Endpoints: GET `/api/testorders/page`, GET `/api/testorders/counts`, POST `/api/samples`, POST `/api/water/receive`, `/api/em/receive`, `/api/aftercleaning/receive`, GET `/api/samples/receipt-labs`, POST `/api/samples/{id}/laboratories`, PUT `/api/samples/{id}/correct`, PUT `/api/samples/{id}/assign-analyst`, POST `/api/samples/{id}/void`. [src: frontend/src/modules/receiving/services/ReceiveService.ts:52-133; backend/MicroLIMS.API/Controllers/SampleController.cs]

#### SCR-SAMPLE-002 New Sample (dialog)
- Opened from Receive Sample and from lab workspace "+ New Sample". Title "New Sample". [src: frontend/src/modules/receiving/dialogs/NewSampleDialog.tsx:320; frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:772-784]
- Step 1 category tiles: "Product", "Raw Material", "Packaging Material", "Water", "Environmental Monitoring", "After Cleaning"; "Next" disabled until chosen; then "Back", "Add Another Sample", "Save All Samples" / "Receiving...". [src: frontend/src/modules/receiving/constants/receivingConstants.ts:6-41; NewSampleDialog.tsx:339-384]
- Row fields (grid): "Quantity", "Mfg Date", "Exp Date"; placeholders "Select Item", "Stage", "Select Department", "Select Machine", "Cause of Testing", "Sampled By", "Batch No.", "Control No.", "e.g. 50g / 100ml". [src: frontend/src/modules/receiving/dialogs/MultiSampleEntryGrid.tsx:209, 229-230, 276-553]
- Client validation messages (per row): "Item is required"; "Choose at least one laboratory"; "Production Stage is required" (Product only); "Department is required" (Water, EM); "Previous Product is required"; "Previous Product Batch No. is required"; "Machine is required" (AC); "Cause of Testing is required"; "Sampled By is required"; "Expiry Date must be after Manufacturing Date" (only when both dates set and expiry < manufacturing). [src: NewSampleDialog.tsx:170-225]
- Required markers in the grid header (red asterisk): "Cause of Testing", "Sampled By", "Machine" (After Cleaning), "Previous Batch No." (After Cleaning), and, for Product / RM / PM only, "Batch No." and "Control No.". "Quantity" (hidden for EM and After Cleaning), "Mfg Date", "Exp Date" carry no asterisk. The server-side requirement for Batch and Control numbers is stated in a code comment in the grid; `validateRows` does not check them, so an empty value is refused only by the server (BR-SAMPLE-003, GAP-200). [src: frontend/src/modules/receiving/dialogs/MultiSampleEntryGrid.tsx:175-228; NewSampleDialog.tsx:167-225] (verified)
- Water, EM and After Cleaning receive create the sample only; their tests and locations are created later in preparation (BR-SAMPLE-002). [src: EMWorkflowEngine.cs:35-56; WaterWorkflowEngine.cs:49-71; AfterCleaningWorkflowEngine.cs:41-76] (verified)
- Success: "Successfully received N sample(s). Test orders generated automatically." [src: ReceivingTestingWorkspacePage.tsx:629-634]

#### SCR-SAMPLE-003 Edit Sample Details (dialog)
- Title "Edit Sample Details". [src: frontend/src/modules/receiving/dialogs/EditSampleDetailsDialog.tsx:216]
- Fields: "Batch Number", "Control Number", "Cause of Testing", "Sampled By", "Sample Quantity", "Production Stage", "Mfg Date", "Exp Date", "Previous Product", "Previous Product Batch Number", "Storage Condition" (options "Room Temperature", "Refrigerator"), "Storage Time (hours)", "Reason for Correction" (required; helper "Recorded with your electronic signature and in the sample's audit trail."). [src: EditSampleDetailsDialog.tsx:281-446, 418-419]
- The dialog also offers the sample's item or location as a select (label depends on category: item for Product / RM / PM, "Water Department", "Department", "Machine"). That select shows helper text "Locked: testing has started for this sample" and is disabled when `canChangeItemOrLocation` is false; it concerns the item/location only (server rule: BR-SAMPLE-005). Retest cause is not selectable ("Retest" is system-assigned). [src: EditSampleDetailsDialog.tsx:134-171, 252-262] (corrected: the Haiku pass read this text as the lock for all fields)
- When `sample.canEditDetails` is false: alert "This sample has been submitted for review, so its details are locked." and Save is disabled. [src: EditSampleDetailsDialog.tsx:238-242]
- Save enabled only when: control number, sampled by and cause are non-blank; Product/RM/PM batch number; Finished Product production stage; After Cleaning previous product and previous batch; Refrigerator storage time (water); and a non-blank reason. [src: EditSampleDetailsDialog.tsx:167-174]
- Buttons: "Cancel"; "Sign & Save" (opens SignatureDialog, meaning statement for SampleCorrected). [src: EditSampleDetailsDialog.tsx:222-235] (verified)
- Entry points: "Edit Details" in SCR-TESTING-004 (tooltips "Correct sample details (signed)" / "Locked: the sample has been submitted for review") and "Edit" row action. [src: frontend/src/modules/testingWorkspace/SelectedSampleTestingPanel.tsx:148-172]
- Endpoint: PUT `/api/samples/{id}/correct`, policy `Samples.Correct`, password in body. [src: backend/MicroLIMS.API/Controllers/SampleController.cs:L106-107]

#### SCR-SAMPLE-004 Void Sample Confirmation (dialog)
- Title "Void Sample Confirmation"; subtitle "Strikes the sample from the register - signed with your password"; warning "The sample record and its open tests will be marked as Voided." [src: frontend/src/modules/receiving/dialogs/VoidSampleConfirmationDialog.tsx:87-90, 128]
- Field "Reason for Voiding *" (helper "A reason for voiding is required." when empty, else "Recorded with your electronic signature and in the sample's audit trail."). Button "Sign & Void" (disabled when no reason), then SignatureDialog. [src: VoidSampleConfirmationDialog.tsx:102, 112, 185-205]
- Endpoint: POST `/api/samples/{id}/void`, policy `Samples.Correct`, password in body. [src: SampleController.cs:L135-136]

#### SCR-SAMPLE-005 Add Laboratory (dialog)
- Title "Add Laboratory", subtitle "Creates that lab's test orders on this sample - signed with your password". Fields "Laboratory" (select) and "Reason *" (error "A reason is required."). Helper "Recorded with your electronic signature and in the sample's audit trail." Buttons "Cancel" and "Sign & Add". [src: frontend/src/modules/receiving/dialogs/AddLaboratoryDialog.tsx:121-142, 196-225] (verified)
- Endpoint: POST `/api/samples/{id}/laboratories`, policy `Samples.Receive`, password in body. [src: SampleController.cs:L147-148]

#### SCR-SAMPLE-006 Assign Responsible Analyst (dialog)
- Title "Assign Responsible Analyst". Select "Select Analyst" with option "Unassigned (Open for any authorized analyst to prepare)". Field "Reason for Assignment / Reassignment (recorded in Audit Trail)". Read-only labels "Reference Number:", "Batch / Control No.:", "Current Assignment:". The reason box has no required marker and no client check; an empty reason is sent as undefined, and the server also treats it as optional. Buttons "Cancel" and "Save Assignment" ("Saving..." while submitting). [src: frontend/src/modules/receiving/dialogs/AssignAnalystDialog.tsx:83, 116-128, 163-173, 191-199, 219] (verified; resolves the "required flag" part of GAP-213)
- Endpoint: PUT `/api/samples/{id}/assign-analyst`, policy `Samples.AssignAnalyst`. [src: SampleController.cs:L126-127]

### MOD-TRACK-001 Tracking Board

#### SCR-TRACK-001 Tracking Board
- Route `/receiving/tracking`; guard `Samples.TrackAll`. Title "Tracking Board"; subtitle "Every received sample across both laboratories, with each lab's own stage." [src: frontend/src/routes/AppRoutes.tsx:143-144; frontend/src/modules/receiving/TrackingBoardPage.tsx:176-177]
- Filters: "Laboratory" (options "All Laboratories", "Microbiology Laboratory", "Physicochemical Laboratory"); "Overall Status" (options "All Overall Statuses", "In Progress", "Approved", "Rejected", "Retest Requested", "Voided", "Cancelled"); "Product / Item" (helper on failure "Product list unavailable - reload the page to retry."); "Received From"; "Received To"; reset button (label UNCLEAR). [src: TrackingBoardPage.tsx:37-50, 190-265]
- Columns: "Reference", "Product", "Batch", "Received", "Overall", "Microbiology", "Physicochemical". Empty: "No samples found matching the filter criteria."; lab-stage chip when absent: "Not requested". [src: TrackingBoardPage.tsx:72, 278-292]
- Stage chip text per laboratory (server value, camel-case split into words): "Testing" (section InTesting), "Under Review", "Under Approval", "Approved", "Rejected", "Retest Requested", "Closed" (section Cancelled), "Voided". [src: SampleTrackingService.cs:L141-L146; TrackingBoardPage.tsx:79-98] (verified)
- "Product" column text by category: After Cleaning = machine name; Water = sampling point code, else water department name; Environmental Monitoring = department name; otherwise item name. Default page size 50, server maximum 200. [src: SampleTrackingService.cs:L28, L121-L127] (verified)
- Filters sent: laboratory by section code or id, item, received-from / received-to dates (date part only; "to" is inclusive of the whole day), overall status. [src: SampleTrackingService.cs:L41-L68] (verified)
- Endpoint: GET `/api/samples/tracking`. [src: frontend/src/modules/receiving/services/TrackingService.ts:49]

### MOD-PREP-001 Sample preparation

#### SCR-PREP-001 Preparation dialog ("Preparation")
- Opened from "Prepare Sample" in the workspace, register row menu, and grouped panel. Title "Preparation". Routing by category: EM -> EM room grid (needs department); After Cleaning -> machine-part grid (needs machine); Water -> sampling point grid (needs water department); Product / RM / PM -> ConfirmPreparationForm when a configuration exists, otherwise TestPreparationForm. [src: frontend/src/modules/testPreparation/PreparationDialog.tsx:21-24, 70-101]
- Product / RM / PM manual form (TestPreparationForm; shown only when the item has no preparation configuration): fields "Sample Amount", select "Technique" (options "Pour Plate", "Filtration"; default Pour Plate), "Filtration Volume (ml)" and "Washing Volume (ml)" (only when Filtration), "Diluent" and "Neutralizer" (both required; label as coded "Neutralizer"). Info text: the entry becomes the item's standing configuration and is sent to the Section Head for approval; testing is not held up. Button "Start Testing", enabled when Diluent and Neutralizer are non-blank and the sample is not assigned to someone else (SectionHead and SystemAdministrator roles are exempt from that check in the UI only; see GAP-219); opens SignatureDialog with meaning "I confirm the preparation steps entered above are the steps performed for this sample, and are correct for this item." "Sample Amount" is not checked as required by the form. [src: frontend/src/modules/testPreparation/TestPreparationForm.tsx:25-96] (corrected: Haiku pass listed only Diluent and Neutraliser)
- Product / RM / PM confirm form (ConfirmPreparationForm; item has a configuration): shows the configured steps, chip "Approved" / "Pending Approval", info while pending section-head approval, button "Confirm & Start Testing" (signed). [src: frontend/src/modules/testPreparation/ConfirmPreparationForm.tsx:42-82] (verified)
- EM form (EMPreparationForm): "Loading rooms..."; room cards with subtitle "Grade {x}"; rooms with no configuration disabled; confirm sends one call to `/api/em/prepare`; no signature. Error "Could not prepare sample." [src: frontend/src/modules/laboratoryConfiguration/environmentalMonitoring/EMPreparationForm.tsx:31-60]
- After Cleaning form: subtitle "Machine Part"; "Loading machine parts..."; `/api/aftercleaning/prepare`; no signature. [src: frontend/src/modules/laboratoryConfiguration/afterCleaning/AfterCleaningPreparationForm.tsx:37-63]
- Water form (WaterPreparationForm): "Storage Condition *" (options "Refrigerator", "Room Temperature"; placeholder "Select storage condition"); "Storage Time (hours) *" (number, shown when Refrigerator); confirm disabled until storage complete; points with no assigned test disabled; "Loading sampling points..."; `/api/water/prepare`; no signature. [src: frontend/src/modules/laboratoryConfiguration/water/WaterPreparationForm.tsx:31-33, 97-137]
- Shared grid (SamplingPointGrid): "Select All", chip "{n} selected", chip "Assigned Tests" / "No tests configured"; default confirm label "Start Testing"; "Starting..." while loading. [src: frontend/src/modules/testPreparation/components/SamplingPointGrid.tsx:82-111, 184-278]
- Grouped preparation row (GroupedPreparationRow): "Test Preparation · {item}", chips "× N Sample(s)", "Approved" / "Pending Approval"; expanded "Samples to prepare (N):"; info when configuration pending section-head approval; button "Confirm & Start Testing (N Samples)" (signed). [src: frontend/src/modules/testPreparation/components/GroupedPreparationRow.tsx:95-209]
- Item preparation configuration screen: not located (GAP-214).

### MOD-TESTING-001 Laboratory workspaces

#### SCR-TESTING-001 Microbiology Workspace ("{lab name} Workspace")
- Route `/microbiology/workspace` (`LabWorkspaceRoute code="MICRO"`). Title "{lab.name} Workspace" (Microbiology Laboratory). Subtitle "Manage incoming samples, assignments, testing progress, review, and laboratory workflow execution from one workspace." [src: frontend/src/routes/AppRoutes.tsx:130; frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:744-751]
- Buttons: "Refresh" (outlined); "+ New Sample" (contained); "Export" (CSV "receiving-testing-workspace-{date}.csv" with columns Sample ID, Reference, Item / Display Name, Category, Batch No, Control No, Cause of Testing, Sampled By, Received At, Status). [src: ReceivingTestingWorkspacePage.tsx:755-784, 81-103, 840]
- Category allowance per lab: MICRO = product, rm, pm, water, em, ac; FP = product; RM & PM = rm, pm. [src: ReceivingTestingWorkspacePage.tsx:114-122]
- KPI tiles: "Needs Preparation", "Ready to Read", "Awaiting Review", "Overdue", "Assigned to Me", "Unassigned" (see SCR-SAMPLE-001 hints). Tile click toggles the workload filter. [src: ReceivingTestingWorkspacePage.tsx:476-479]
- Deep-link workload chip "Active Workload Filter:" with "Retests in Progress", "Overdue Review (>24h)", "Overdue Approval (>24h)". Whitelisted URL workloads: needsPreparation, readyToRead, awaitingReview, overdue, mine, unassigned, retestInProgress, reviewOverdue, approvalOverdue. [src: ReceivingTestingWorkspacePage.tsx:65-75, 797-820]
- Views: table (default), card, kanban (read-only; columns Received, Under Testing, Under Review, Under Approval, Approved, Rejected, Retest Requested; card click selects sample, does not open summary). [src: frontend/src/modules/testingWorkspace/SampleKanbanView.tsx:6-14; ReceivingTestingWorkspacePage.tsx:1013-1019]
- Layout: 2+ checked samples -> compact register + GroupedActionsPanel; one selected -> compact register + SelectedSampleTestingPanel; none -> full register. Compact register title "Laboratory Register (N of M)"; full "Laboratory Register (N samples)"; empty "No matching samples found."; page sizes [25, 50, 100] compact and [25, 50, 100, 200] full, default 50. [src: ReceivingTestingWorkspacePage.tsx:154-157, 657-721, 861-1050]
- Toasts: "{n} test(s) started in incubation." (+ " ({k} skipped)"); "{n} sample(s) prepared and signed."; "Sample #{id} ({name}) has been marked as Voided."; "Sample #{id} could not be opened. It may not belong to this laboratory, or the server could not be reached." [src: ReceivingTestingWorkspacePage.tsx:901-914, 1137-1143, 136-140]
- Dialogs mounted: NewSampleDialog, EditSampleDetailsDialog, AssignAnalystDialog, SampleSummaryDialog (C3), TestWorkflowDialogRouter, PreparationDialog, AuditHistoryDialog, VoidSampleConfirmationDialog. [src: ReceivingTestingWorkspacePage.tsx:1055-1145]

#### SCR-TESTING-002 FP Workspace
- Route `/physicochemical/workspace` (`code="FP" area="fp"`). Same shell as SCR-TESTING-001; title "FP Workspace"; members with area `fp`. [src: frontend/src/routes/AppRoutes.tsx:131; ReceivingTestingWorkspacePage.tsx:744-749]

#### SCR-TESTING-003 RM & PM Workspace
- Route `/physicochemical/rm-pm-workspace` (`code="FP" area="rmpm"`). Title "RM & PM Workspace". [src: frontend/src/routes/AppRoutes.tsx:132; ReceivingTestingWorkspacePage.tsx:744-749]

#### SCR-TESTING-004 Selected sample panel (embedded)
- Header buttons, in order: "Sample Summary" (always); "Audit History" (always); "Edit Details" (`Samples.Correct`, disabled when `!sample.canEditDetails`); "Void Sample" (`Samples.Correct` and status not Voided); "Prepare Sample" (when preparation status NeedsPreparation); "Deselect". [src: frontend/src/modules/testingWorkspace/SelectedSampleTestingPanel.tsx:104-242]
- Metadata labels: "ASSIGNED TO", "PREP STATUS", "CAUSE OF TESTING", "BATCH NUMBER", "CONTROL NUMBER", "RECEIVED AT", "SAMPLED BY", "SAMPLE QTY", plus "STAGE" (Product), "SAMPLING POINT" (Water), "PREV PRODUCT" (AC), else "MFG DATE"; "STORAGE", "EXP DATE", "CATEGORY". [src: SelectedSampleTestingPanel.tsx:258-381]
- "Open Pathogen Workflow" shown only when a test uses shared TSB. [src: SelectedSampleTestingPanel.tsx:401-417]
- Banner "Sample Needs Preparation" with body "Record the sample preparation and sign for it before starting laboratory tests." (product-like) or "Test locations and configuration must be completed before starting laboratory tests." (others); button "Start Preparation". [src: SelectedSampleTestingPanel.tsx:420-466]
- Section "Assigned Tests (N)", subtitle "Test Master configured laboratory workflows"; empty "No tests assigned to this sample." [src: SelectedSampleTestingPanel.tsx:393-397, 468-471]
- Product-like samples show "SOP" and "Verification Report" item documents. [src: SelectedSampleTestingPanel.tsx:385-387; ItemDocumentsCard.tsx:85, 93]

#### SCR-TESTING-005 Grouped actions (embedded, 2+ samples checked)
- Header "Grouped Actions"; subtitle "{n} samples selected across workspace"; buttons "Deselect All", clear icon aria "Clear selection". [src: frontend/src/modules/testingWorkspace/components/GroupedActionsPanel.tsx:154-202]
- Sections: "Test Preparation Required (N)" (note "Must complete before incubation"); "Compatible Actions (N)" (note "Shared operational workflow transition"); "Tests Awaiting Individual Result Entry (N):". [GroupedActionsPanel.tsx:93-100, 270, 341-346]
- Empty / locked states: "Incubation Setup Locked" ("No incubation can start on a sample whose Test Preparation is still outstanding. Confirm the preparation above and the compatible setup actions appear here."); "Individual Result Entry Required" ("No grouped workflow action is available. The selected tests require individual result entry. Result entry must be completed separately for each test."); "No Shared Setup Actions Available"; "Calculating compatible workflow steps..."; warning "{n} test(s) excluded from grouped actions". [GroupedActionsPanel.tsx:221, 237-243, 262-265, 309-313, 332-335]
- Group row (GroupedActionRow): chip "× N Sample(s)"; status "Ready: Transfer" / "Ready: Plating" / "Ready: Setup"; expanded "Selected Samples (N Tests):", "Media Lot (...)", "Transfer to Incubator" or "Incubator"; text "No released, active lots available", "No eligible incubator in range". Buttons "Cancel", "Start Incubation (N Tests)", "Transfer & Start Incubation (N Tests)" (selective), "Transfer to Incubator (N Tests)" (incubator), "Processing...". [src: frontend/src/modules/testingWorkspace/components/GroupedActionRow.tsx:203-441]
- Partial refusal text: "No tests were started. {reasons}"; "{n} of {m} started. {k} refused - {reasons}"; fallback "Failed to execute batch action." [GroupedActionRow.tsx:141-163]
- Grouped actions do not sign (no SignatureDialog in grouped incubation start). [src: frontend/src/modules/testingWorkspace/FloatingDialogs.tsx / GroupedActionRow.tsx; raw §11.2]

### MOD-COUNT-001 Count tests

#### SCR-COUNT-001 "{testCode} Workflow" dialog (count test)
- Title "{testCode} Workflow", subtitle "{ref} · {sample name} · TO #{testOrderId}". [src: frontend/src/modules/testingWorkspace/FloatingDialogs.tsx:44-79]
- Phases: select-media -> awaiting-result -> (transfer-stage-2) -> enter-result -> step-complete / all-complete. [src: frontend/src/modules/testingWorkspace/TestWorkflowDialog.tsx:37, 86]
- select-media: helper "Requires {names or 'an approved'} media."; select aria "Media batch" (empty option "Media Batch ({names} lots only)"; options "{lotNumber}, expires {date}"); select aria "Stage 1 Incubator" (empty "Stage 1 Incubator ({min}-{max} °C)"; options "{name} ({code}), {setPoint}°C"); warning "No incubator is set to {min}-{max} °C for this step."; info "Required Temperature (Stage 1): {min}-{max} °C, Incubation Period: {h}-{h} hours"; button "Start Incubation" (disabled until both chosen; no signature). [src: TestWorkflowDialog.tsx:471-521]
- awaiting-result: "Incubation in progress" (with "(Stage N of 2)"); "Temperature: {range} °C", "Duration: {range} hours", "Expected reading: {datetime}"; buttons "Skip Wait" (supervisor), "Transfer to Stage 2 Incubation" (two-stage, stage 1), "Record Results" / "Record Result" (stage 2 or batch), "Advance to Next Incubation Window" (EM/AC non-final), "Record Result" (other). [src: TestWorkflowDialog.tsx:578-622]
- transfer-stage-2: "Stage 1 incubation is complete. Transfer plates to Stage 2 incubator ({min}-{max} °C) to start the second stage."; "Media Lot (inherited): {lot}"; aria "Stage 2 incubator" (empty "Select Stage 2 Incubator ({min}-{max} °C)"); buttons "Back", "Start Stage 2 Incubation". [src: TestWorkflowDialog.tsx:523-576]
- enter-result (CountTest): field "Dilution Factor" (number, min 1; helper "Direct count: dilution factor fixed at 1" / "Enter multiplier: 10 for 1:10 dilution, 100 for 1:100" / "Configured value from Specifications (N)"); icon aria "Override dilution factor"; button "Cancel Override"; field "Justification for override" (required when value differs from configured; helper "Required: the entered value differs from what's configured on Specifications. This is captured in the audit trail."); plate fields "Plate {i}" (placeholder "Colony count or TNTC"; remove aria "Remove reading {i}"); button "Add Plate"; live text "Calculated result: {display}"; non-numeric note "Non-numeric result: reviewer will decide accept or retest"; submit "Calculate" (no signature). [src: TestWorkflowDialog.tsx:666-810; raw frontend-testing-workspace.md §2.1-2.2]
- step-complete: "Step recorded: {outcome}" (+ ": Average X, Calculated Y, Status Z"); non-definitive "{outcome} - this step must be repeated with a fresh media lot."; buttons "Proceed to Next Step" / "Retry This Step". [src: TestWorkflowDialog.tsx:812-830]
- Other visible texts: "Returned for revision: {reason}" / "Returned by reviewer for revision"; "Step {order}: {name}"; "Final result: {outcome}"; buttons "Done / Close". [src: TestWorkflowDialog.tsx:429-469]
- Error texts (client): "Could not load this test's workflow."; "Could not skip the wait."; "Could not start incubation for this step."; "Could not start stage 2 incubation."; "Could not close this incubation window."; "No Dilution Factor is configured for this test on the item's Specifications tab. Ask a Section Head/Admin to configure it before recording a result."; "Enter at least one plate reading."; "A justification note is required when the entered Dilution Factor differs from the configured value."; "Could not record this result." [src: TestWorkflowDialog.tsx:88-291]

#### SCR-COUNT-001 location variants (EM, AC, Water count grids)
- See SCR-OBS-001 (no separate SCR id).

### MOD-OBS-001 Observation and location-grid tests

#### SCR-OBS-001 Location result grids (dialogs)
- LocationResultGridDialog (EM / AC count batch): title "{testCode} Results: {displayName}"; columns "Location", "Limits (Alert / Action / Spec)", "Plate Readings", "Calculated Result", "Status"; per-location numeric inputs aria "{location} reading {i}"; "Add Plate"; summary "{conform}/{total} locations within spec; worst status: {badge}"; warning "Results cannot be submitted before {datetime}."; button "Submit Results" / "Submitting…"; error "Could not record batch results.". No signature. [src: frontend/src/modules/testingWorkspace/LocationResultGridDialog.tsx:102-212]
- WaterLocationResultGridDialog (Water count batch): columns "Sampling Point", "Limits (Alert / Action / Spec)", "Plate Readings", "Average", "Status"; button "Submit Results"; error "Could not record batch readings.". No signature. [src: frontend/src/modules/testingWorkspace/WaterLocationResultGridDialog.tsx:48-195]
- PathogenLocationResultGridDialog (EM / AC pathogen batch): title "{testCode} Results: {displayName}"; columns "Location", "Growth Observed?", "Status"; radios "Yes" / "No"; live "Detected" / "Absent"; summary "{detected}/{total} locations detected; overall: {Detected|Absent}"; button "Submit Results"; error "Could not record batch pathogen results.". No signature. [src: frontend/src/modules/testingWorkspace/PathogenLocationResultGridDialog.tsx:49-114]
- QualitativePanel (physchem Qualitative, listed under MOD-OBS-001; detail in C6): "Analysis time"; "Equipment (Physicochemical)" (option "None (no instrument linked)"); "Comment (optional)"; observation label "Observation (required) *" / "Observation (optional)" (helper "Observation is required when non-compliant"); per-row "Complies" / "Does not comply"; button "Sign and record result" (signed, meaning "I entered these qualitative observations and am recording this test result."). [src: frontend/src/modules/testingWorkspace/QualitativePanel.tsx:186-475]

### MOD-PATH-001 Pathogen testing

#### SCR-PATH-001 Pathogen step dialog
- Routed by step type: BrothEnrichment / SelectiveBroth -> BrothWaitingPanel (if incubating) else BrothStepPanel; SelectivePlating -> SelectivePlatingPanel; ConfirmatoryPlating -> ConfirmatoryPlatingPanel; BiochemicalTest -> BiochemicalTestPanel; other -> UnsupportedStepPanel (not read). [src: frontend/src/modules/testingWorkspace/PathogenStepDialog.tsx:143-166]
- Step chips: "Incubating", "Preparation · no pass/fail", "Set up", "Ready to read", "Result entry". Help link "What happens in this step?" / "Hide step details". Final: "✓ Final result: {finalResult}", button "Done / Close". Load error "No current step is available for this test order." [src: PathogenStepDialog.tsx:72-134]
- BrothStepPanel: fixed row "Medium" (lock "Test Master"); "Media lot" (helper "Released and within expiry" / "No released lots of this medium are available"); "Incubator" (helper "Only incubators set within {min}–{max} °C are listed"); timeline "Start" 0 h, "Ready from" {minHours} h, "Read by" {maxHours} h; button "Start Incubation" / "Starting..."; note "Start time, media lot and incubator are recorded in the audit trail." (no signature). Shared-TSB branch: "Shared TSB Applied" with "This test is linked to the shared TSB for this sample. There is one shared tube per sample; no separate broth entry required." and "Proceed to Next Step →". Prompt dialog "Apply Shared TSB to All Pathogen Tests" ("Confirm & Apply to All"). [src: frontend/src/modules/testingWorkspace/pathogenSteps/BrothStepPanel.tsx:79-352; StepLayout.tsx:89-155]
- BrothWaitingPanel: button "Proceed to {nextStep}" / "Proceed to Reading", disabled until ready. No signature. [src: BrothWaitingPanel.tsx:46-54]
- SelectivePlatingPanel: expected appearance note (warning "Expected appearance not configured in Test Master for {media}..." or info "Expected target-positive colony"); setup: lot, incubator, "Start Incubation"; waiting: disabled "Record Observation"; readout: "Incubation complete. Read the plate and record what you observe."; rows "Started", "Ready from", "Read by"; options "No growth" (detail "Target absent"), "Growth, does not match expected appearance" (detail "Target absent"), "Growth matching expected appearance" (detail "Presumptive positive"); note "Observed appearance note (optional)"; button "Record Observation" / "Recording..."; error "Select an observation.". No signature. [src: pathogenSteps/SelectivePlatingPanel.tsx:71-221; StepLayout.tsx:159-171]
- ConfirmatoryPlatingPanel: "Target organism"; setup text "Tick every medium you actually plated, then choose its lot and incubator. Only media the step template permits are listed."; checkbox per permitted medium; "Start Incubation"; notes "Each medium keeps its own window; the shared incubation runs to the longest one selected."; warning "Incubation duration is not configured in Test Master."; readout: "Incubation complete. Read each plate against its expected appearance."; checkbox "This medium was actually plated"; button "Submit Observations" / "Submitting..."; decision block: "Conclude as Detected" and "Proceed to Biochemical Confirmation". Validation "Check at least one medium that was actually plated.", "Every checked medium needs a media lot and an incubator.", "At least one medium must be confirmed as plated.", "Select an observation for: {media}.". No signature. [src: pathogenSteps/ConfirmatoryPlatingPanel.tsx:147-406]
- BiochemicalTestPanel: "Submit as Detected (skip biochemical)" / "Proceed to Biochemical Test". No signature. [src: pathogenSteps/BiochemicalTestPanel.tsx:98-108]
- IncubationWaitingView (shared): "Media lot", "Incubator", "Temperature", "Duration"; timeline "Started", "Ready from", "Read by"; "Not ready yet. Available from {datetime}."; "Minimum wait time was skipped by a Section Head/System Administrator."; countdown centre "Ready". [src: pathogenSteps/IncubationWaitingView.tsx:88-134; IncubationCountdown.tsx:64-81]

#### SCR-PATH-002 Pathogen Testing Session Workspace
- Title "Pathogen Testing Session Workspace"; subtitle "Session: {sessionId} · Sample: {ref} ({area})". Steps: "Overview", "Shared TSB", "Test Workflows", "Primary Matrix", "Confirmatory Plating", "Review & Complete" (suffix "(N/A)" when not applicable). Step "Review & Complete" locked until canReview. [src: frontend/src/modules/testingWorkspace/pathogenSession/PathogenSessionDialog.tsx:44-51, 191-197, 244-252]
- Initial step: Review if all required results done; else Primary Matrix if any result/observation exists; else Shared TSB if started; else Overview. [src: PathogenSessionDialog.tsx:75-83]
- Header "Reset Session" (supervisor only). Reset dialog: title "Reset Testing Session & Steps?"; field "Reason for Reset *" (placeholder "e.g. Test configuration error / Wrong media lot selected / Operational error"); buttons "Cancel", "Confirm Reset" / "Resetting...". Default reason sent when blank "Section Head requested session reset". [src: PathogenSessionDialog.tsx:115, 217-233, 357-383]
- Overview step: cards "Sampling Locations", "Assigned Tests", "Required Results ({locations} × {tests})", "Completed ({pending} pending)"; chip "Source: Test Master"; buttons "Open Shared TSB Setup" / "Open Test Workflows". [src: pathogenSession/SessionOverviewPanel.tsx:70-316]
- Shared TSB step (SharedTsbEnrichmentPanel): "TSB Media Lot (GPT Passed & Released) *" (helper "Only active, unexpired, GPT-released lots are permitted"); "Incubator Equipment (In-Service) *" (helper "Select an operational, calibrated incubator"); read-only "Required Incubation Temperature *" and "Required Incubation Time *"; button "Start TSB Enrichment" (no signature); validation "Please select a valid released TSB media lot and an in-service incubator."; locked state "LOCKED: Execution in Progress". Default lot chosen by name heuristic (see GAP-208). [src: pathogenSession/SharedTsbEnrichmentPanel.tsx:46-316]
- Primary Matrix (PrimaryObservationMatrixPanel): rows locations, columns tests; quantitative cell (input placeholder "CFU", adornment "CFU", min 0, max 10000, step 1) vs qualitative select options "Select Observation", "No Growth (-)", "Growth Non-Conforming", "Growth Conforming (+)"; counts "Recorded: N", "Available: N", "Locked: N"; locked cell tooltip "Locked until upstream workflow step completes"; buttons "Fill All Unset as No Growth (-)", "Save Observations", "Proceed to Confirmation" / "Review & Complete"; text "Primary plate observations saved successfully with ALCOA+ contemporaneous tracking!". [src: pathogenSession/PrimaryObservationMatrixPanel.tsx:361-453, 811-829]
- Confirmatory Plating (BatchConfirmatoryPlatingPanel): header "Confirmatory Plating (Panel B)", chip "N Flagged Locations"; phases "1. Shared Media Setup", "2. Plate Observations"; setup labels "Confirmatory Medium {i} Lot (GPT Passed & Released) *", "Incubator Equipment (In-Service) *", "Actual Incubation Start Time *", read-only "Incubation Duration (from Test Master)"; button "Start Confirmatory Incubation"; readout chips "Detected (+): n", "Not Detected (-): n", "Inconclusive (Retest): n", "Pending Reads: n"; per-medium radios "No Growth", "Non-Conf", "Conf (+)"; buttons "Modify Setup", "Save Readings & Continue"; empty state "Confirmatory Plating Not Required". Agreement shown per location (see BR-PATH-010). No signature. [src: pathogenSession/BatchConfirmatoryPlatingPanel.tsx:146-946]
- Downstream workflows (DownstreamWorkflowsPanel): "Assigned Test Workflows (N)"; button "Go to Result Matrix"; "Execution State" values "✓ Executed & Recorded", "🔒 Locked (TSB Incubating)", "Pending Matrix Evaluation". [src: pathogenSession/DownstreamWorkflowsPanel.tsx:46-265]
- Review & Complete (SessionReviewPanel): title "Testing Session Review: {ref}"; button "Complete Testing Session" / "Session Completed & Submitted"; warning "There are N missing results. Please complete all Location × Test entries before final completion." with "Edit Matrix"; table heading "Final Analytical Results Summary"; button "Modify Matrix"; chips "Detected (+)", "Not Detected (-)", "Inconclusive (Retest)", "N CFU", state labels; evidence labels "1. Primary Plate Observation:", "2. Confirmatory Plating (N Media):", "3. Agreement Evaluation:"; "Audit & Traceability (ALCOA+ Compliance)" card. Complete calls the server without a password. [src: pathogenSession/SessionReviewPanel.tsx:189-619]

---

## roles

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

## workflows

Phase 4 reconstruction from code (application not run). Every step cites the rule IDs in business-rules / validations; "INTENDED" is stated only where a specification covers the step (BusinessRules.md §1-§6, WorkflowDiagrams.md); everywhere else the intended workflow is REQUIRES CONFIRMATION. Review and approval of the resulting results belong to C3 and are only referenced.

### WF-SAMPLE-001 Receive sample(s) (Product, Raw Material, Packaging Material)
- **Trigger / who:** A user opens "Receive Sample" (`/receiving`) or "+ New Sample" in a laboratory workspace and presses "Receive" / "+ New Sample". Needs `Samples.ReceiveOwnLab` (own laboratories) or `Samples.Receive` (any laboratory). [src: SampleController.cs:L81-L95]
- **Prerequisites:** The item exists, is active and has assigned tests (Test Master / item configuration, C4); cause-of-testing and sampler master lists exist; for Finished Product a production stage exists; the user belongs to the chosen laboratory unless holding `Samples.Receive`.
- **Steps:**
  1. User picks the category tile ("Product", "Raw Material" or "Packaging Material") → grid of rows appears; "Next" is enabled only after a tile is chosen. [SCR-SAMPLE-002]
  2. For each row the user selects the item, laboratory (a picker appears on the Receive page; inside a workspace the lab is fixed), and fills "Cause of Testing", "Sampled By", "Batch No.", "Control No.", optional "Quantity", "Mfg Date", "Exp Date"; Product also "Stage". → client validation per row ("Item is required", "Choose at least one laboratory", "Production Stage is required", "Cause of Testing is required", "Sampled By is required", "Expiry Date must be after Manufacturing Date"). Batch and Control numbers are marked required but only the server checks them (BR-SAMPLE-003).
  3. User presses "Save All Samples" ("Receiving..." while busy) → one call per row to `POST /api/samples`. Server resolves the target laboratories (BR-SAMPLE-001), validates the request, creates the sample (reference number generated by category, Status Received, preparation NeedsPreparation, or Ready when every test is in the FP section, BR-PREP-011) and one test order per assigned test of the chosen laboratories (Pending / Waiting). No signature.
  4. Success message: "Successfully received N sample(s). Test orders generated automatically." (workspace) → samples appear in the register; KPI "Needs Preparation" counts them.
- **Next action:** Preparation (WF-PREP-001) for micro tests; testing (WF-TESTING-001 / WF-COUNT-001 / WF-PATH-*) afterwards. **Review / approval:** C3, after all tests of a laboratory reach Ready. **Final state at this workflow:** sample Received, tests Pending.
- **Required data:** item, cause of testing, sampled by, batch number, control number, at least one laboratory, production stage (Finished Product). **Optional:** quantity, manufacturing and expiry dates.
- **Blocking / errors:** frozen item "Item '{name}' is frozen and cannot be used to receive new samples."; no tests "Item '{name}' has no assigned tests. Configuration must be completed by the Section Head before samples can be received."; "You can receive samples only for your own laboratory."; "Batch number is required." / "Control number is required." (errors joined by a space). Full list: validations table.
- **Signature points:** none. Audit: sample and test orders created (no explicit audit-log row found).
- **Source:** INTENDED (BusinessRules.md §2: one test order per configured test at Waiting / Pending, zero-test item refused, no manual test-order creation). The multi-laboratory choice and quick-receive rules have no spec: REQUIRES CONFIRMATION.

### WF-SAMPLE-002 Add laboratory to a sample
- **Trigger / who:** A user with `Samples.Receive` chooses "Add Laboratory" in the row actions (shown unless the sample is Rejected, Voided or Cancelled).
- **Prerequisites:** The sample is item-based (Product / RM / PM); the item has tests for the laboratory to add; the laboratory is not already testing the sample.
- **Steps:**
  1. Dialog "Add Laboratory": user selects "Laboratory" and enters "Reason *" → "Sign & Add" is enabled.
  2. Password prompt (SignatureDialog) → `POST /api/samples/{id}/laboratories`. Server checks (BR-SAMPLE-009), signs (LaboratoryAdded), creates that laboratory's test orders (Pending / Waiting), sets that laboratory's sign-off to InTesting, recomputes the sample status (a finished sample returns to InTesting and any recorded approval decision is cleared) and returns the sample to NeedsPreparation when the new tests need preparation and none exists.
  3. Toast "Laboratory added to the sample."
- **Next action:** WF-PREP-001 if preparation is needed; then testing. **Review / approval:** each laboratory is reviewed separately (C3).
- **Required:** laboratory, reason, password. **Permissions:** `Samples.Receive`. 
- **Errors:** "A reason is required to add a laboratory."; "A laboratory cannot be added to a rejected, voided or cancelled sample."; "This laboratory is already testing the sample."; "A laboratory can be added only to a product, raw material or packaging material sample."; "Item '{name}' has no tests for {lab}."; signature failure "Password verification failed. The signature was not applied."
- **Signature point:** yes, LaboratoryAdded, with the reason; review event log LaboratoryAdded.
- **Source:** no spec: REQUIRES CONFIRMATION (intended workflow).

### WF-SAMPLE-003 Assign responsible analyst
- **Trigger / who:** A user with `Samples.AssignAnalyst` (SectionHead, SystemAdministrator by default) chooses "Assign Analyst" or clicks the analyst chip in the register.
- **Prerequisites:** The sample is in the user's section scope.
- **Steps:**
  1. Dialog "Assign Responsible Analyst" shows reference, batch / control number and current assignment. User picks an analyst or "Unassigned (Open for any authorized analyst to prepare)"; optional reason.
  2. "Save Assignment" → `PUT /api/samples/{id}/assign-analyst`. Server sets the analyst on every test order of the sample that is not superseded and not Approved or Rejected; writes an audit log "AssignedAnalyst", "ReassignedAnalyst" or "UnassignedAnalyst" with previous and new names and the reason.
- **Effects downstream:** only the assigned analyst can prepare a Product / RM / PM sample (BR-PREP-002); the user counts as having performed the test for segregation checks (C3); grouped actions skip tests assigned to another Analyst-role user.
- **Required:** none beyond the analyst choice (clearing is allowed). **Optional:** reason (recommended by the label; not enforced).
- **Errors:** "User {id} not found."; "User '{name}' is not active." No role check on the assignee (GAP-213).
- **Signature point:** none (reason in audit trail only).
- **Source:** no spec: REQUIRES CONFIRMATION.

### WF-SAMPLE-004 Correct sample details
- **Trigger / who:** A user with `Samples.Correct` (SectionHead, Reviewer, SystemAdministrator) chooses "Edit Details" (workspace panel) or the edit icon (register).
- **Prerequisites:** Sample status Received or InTesting (otherwise the action is disabled: tooltip "Locked: the sample has been submitted for review").
- **Steps:**
  1. Dialog "Edit Sample Details": user changes fields (batch, control number, cause, sampled by, quantity, stage, dates, previous product, storage, item or location) and enters "Reason for Correction" (required).
  2. "Sign & Save" → password prompt → `PUT /api/samples/{id}/correct`. Server refuses a locked sample, a blank reason or no change; validates; signs (SampleCorrected); applies the changes; copies new batch / control numbers to result records; writes a review event and an audit event with old and new values per field.
  3. Toast "Sample details corrected and signed."
  4. Item / location changes (only before any test work) delete the preparation, remove test orders the new item does not require and return the sample to NeedsPreparation.
- **Errors:** BR-SAMPLE-005 and BR-SAMPLE-006 messages ("A reason for the correction is required.", "No changes were made.", locked record, "Testing has already started for this sample, so its item or location can no longer change. Void the sample and receive it again.", "The expiry date cannot be before the manufacturing date.").
- **Signature point:** yes, SampleCorrected with the reason.
- **Source:** no spec: REQUIRES CONFIRMATION.

### WF-SAMPLE-005 Void sample
- **Trigger / who:** A user with `Samples.Correct` chooses "Void Sample" (register or workspace panel); hidden once the sample is Voided.
- **Steps:**
  1. Dialog "Void Sample Confirmation": field "Reason for Voiding *"; "Sign & Void" enabled when a reason exists.
  2. Password prompt → `POST /api/samples/{id}/void`. Server signs (SampleVoided), sets the sample Voided, writes "Voided: {reason}" into the certificate remarks, sets every non-superseded test order Voided, every laboratory sign-off Voided, marks result records Voided, writes a review event and an audit event (status old / new, tests voided).
  3. Toast "Sample voided." (workspace: "Sample #{id} ({name}) has been marked as Voided.")
- **Blocking:** only "Sample is already voided." — no status precondition (an Approved or Rejected sample can be voided; GAP-203). "A reason for voiding is required."
- **Final state:** Voided (terminal; shown in the register as "Cancelled / Voided"). A voided sample cannot have its pathogen session reset.
- **Signature point:** yes, SampleVoided. **Source:** no spec: REQUIRES CONFIRMATION.

### WF-SAMPLE-006 Environmental Monitoring sample: receive, prepare, test (end to end)
- **Trigger / who:** Any signed-in role starts "New Sample" and chooses "Environmental Monitoring" (no permission policy or lab-scope check on the EM endpoints; GAP-227).
- **Prerequisites:** Departments, rooms and room test configurations (test code, test type, alert / action / spec limits, unit) exist (C4); an EM test definition with incubation steps and step media exists (C4); equipment, media lots and incubators as for WF-COUNT-001.
- **Steps:**
  1. Receive: user selects "Department", "Cause of Testing", "Sampled By", "Control No." → `POST /api/em/receive`. Sample created (Received, NeedsPreparation); no test orders yet; department not found: "Department {id} not found."
  2. Prepare ("Prepare Sample" → room grid): user ticks rooms (rooms without configuration are disabled) → `POST /api/em/prepare`. Server creates one test order per distinct test code across the chosen rooms (Pending / Waiting, analyst = the preparer) and one location per room configuration; preparation Ready. No signature. Errors in BR-PREP-006.
  3. Start incubation: the analyst opens each test ("{code} Workflow"), selects a media lot and incubator and presses "Start Incubation" (or uses grouped actions, WF-TESTING-001). Order Waiting → Incubating; sample Received → InTesting. Checks: BR-PREP-004, BR-TESTING-009, BR-TESTING-013.
  4. If the template has more windows: after the minimum duration the analyst presses "Advance to Next Incubation Window" (close window), then selects media again for the next window.
  5. Final window: after the minimum duration the analyst opens "{code} Results" and enters plate readings per room → WF-COUNT-002 (count) or the Yes / No growth grid for pathogen tests (WF-COUNT-002 step 5).
  6. Order becomes Ready (ResultEntered); when all tests of the laboratory are Ready the section moves to UnderReview automatically.
- **Next / review / approval:** C3 (review by someone other than the preparer / entrant; segregation applies to the preparer because the preparer is the assigned analyst).
- **Signature points:** none in this workflow. **Source:** INTENDED partly (BusinessRules.md §5 room selection → incubation steps → average); the out-of-trend rule in the spec is not implemented (BR-COUNT-017, DISCREPANCY). Everything else: REQUIRES CONFIRMATION.

### WF-SAMPLE-007 Water sample: receive, prepare, test (end to end)
- **Trigger / who:** Any signed-in role; "New Sample" → "Water".
- **Steps:**
  1. Receive: "Department" (water department), "Cause of Testing", "Sampled By", "Quantity", "Control No." → `POST /api/water/receive`; sample Received / NeedsPreparation, no test orders.
  2. Prepare (water form): user chooses "Storage Condition *" ("Refrigerator" or "Room Temperature"), "Storage Time (hours) *" when refrigerated, and ticks sampling points that have assigned tests → `POST /api/water/prepare`. Server stores the storage data (time kept only for Refrigerator), creates one test order per distinct test code (analyst = preparer) and one location per point x test (count tests linked to the point's sampling configuration), preparation Ready. Errors: "Storage condition is required for water samples."; "Storage time is required when a water sample was refrigerated."; point selection errors (BR-PREP-005, BR-PREP-006).
  3. Incubate: as WF-SAMPLE-006 steps 3-4.
  4. Results: per sampling point plate readings via "Submit Results" (WF-COUNT-002); average, whole-number report, limit status per point.
  5. Order Ready; section auto-submitted for review when all tests are Ready.
- **Notes:** the legacy `/api/water/calculate` is refused for batch-prepared orders (BR-COUNT-015). Water storage can be corrected later through WF-SAMPLE-004.
- **Signature points:** none. **Source:** INTENDED for the comparison order only (BusinessRules.md §4, matches code, BR-COUNT-018); the rest REQUIRES CONFIRMATION.

### WF-SAMPLE-008 After Cleaning sample: receive, prepare, test (end to end)
- **Trigger / who:** Any signed-in role; "New Sample" → "After Cleaning".
- **Steps:**
  1. Receive: "Machine", "Previous Product", "Previous Batch No.", "Cause of Testing", "Sampled By", "Control No." → `POST /api/aftercleaning/receive`. Previous product and batch are required by the server ("Previous Product is required for After Cleaning receiving.", "Previous Product Batch Number is required for After Cleaning receiving."); the sample's batch number is set to the previous product batch.
  2. Prepare (machine-part grid, subtitle "Machine Part"): tick parts → `POST /api/aftercleaning/prepare`; one test order per distinct test code, one location per machine-part configuration; preparation Ready.
  3. Incubate, read results, advance: as WF-SAMPLE-006 steps 3-6, using swab or rinse units (BR-COUNT-012).
- **Signature points:** none. **Source:** INTENDED in BusinessRules.md §6 (collective pathogen order + one TAMC order per part), DISCREPANCY with code (GAP-225). Everything else REQUIRES CONFIRMATION.

### WF-PREP-001 Confirm sample preparation (single and grouped)
- **Trigger / who:** The assigned analyst (or any user when the sample is unassigned) presses "Prepare Sample" / "Start Preparation" in the workspace or register, or selects 2+ samples of the same item configuration for "Confirm & Start Testing (N Samples)".
- **Prerequisites:** Sample NeedsPreparation, in the user's section scope, not already prepared; for Product / RM / PM: the item preparation configuration exists (confirm form) or not (manual form). FP-section tests need no preparation (BR-PREP-011).
- **Steps (Product / RM / PM, configuration exists):**
  1. Dialog "Preparation" shows the configured steps and the chip "Approved" or "Pending Approval" (an unapproved configuration is still usable).
  2. "Confirm & Start Testing" → password prompt (signature PreparationConfirmed) → `POST /api/sample-preparation/confirm`. Server checks ownership (BR-PREP-002) and re-validates the configuration, copies its values into a sample preparation record, signs, sets preparation Ready and assigns the signer as analyst on every test still Waiting.
- **Steps (no configuration yet):** the manual form (Sample Amount, Technique, Filtration / Washing volumes for Filtration, Diluent, Neutralizer) → "Start Testing" → password → `POST /api/sample-preparation`; the same values become the item's configuration with approval pending (WF-PREP-002).
- **Steps (grouped):** the grouped panel lists groups by configuration; samples assigned to someone else or already prepared are excluded with reasons; one password signs all samples (one signature per sample); a sample that fails its own rule is skipped and reported ("N prepared and signed"); a wrong password fails the whole run when nothing has been signed yet.
- **Steps (EM / After Cleaning / Water):** room, machine-part or sampling-point grid (water also storage) → WF-SAMPLE-006..008 step 2; not signed, not groupable.
- **Permissions:** no permission code; section scope only. **Blocking / errors:** "This sample has already been prepared."; "This sample is assigned to {name}. Only the assigned analyst may perform sample preparation, unless reassigned by an authorized Section Head."; "Diluent is required."; "Neutralizer is required."; "Filtration technique requires both Filtration Volume and Washing Volume."; "This item has no preparation configuration to confirm."; grouped configuration changed message.
- **Signature points:** yes for Product / RM / PM (PreparationConfirmed); none for EM / AC / Water (GAP-212).
- **Final state:** preparation Ready; tests still Waiting. **Source:** no spec: REQUIRES CONFIRMATION.

### WF-PREP-002 Item preparation configuration and approval
- **Trigger / who:** A user with `Items.Manage` (SectionHead, SystemAdministrator) saves a preparation configuration for an item, or an analyst's first manual preparation seeds one.
- **Steps:**
  1. Save configuration (Amount, Technique, Filtration / Washing volume, Diluent, Neutralizer) → `PUT /api/items/{id}/preparation-configuration`; validated (BR-PREP-003); stored; approval status PendingReview; any previous approval is cleared.
  2. The configuration appears in the pending queue (`GET /api/preparation-configurations/pending`, oldest first) for approval.
  3. `POST .../approve` records approver and time; status Approved. Error when none or already approved (BR-PREP-009).
- **Effects:** analysts confirm from the configuration immediately, approved or not; confirmed values are copied into each sample.
- **Signature / password:** none. **Screen:** not located (GAP-214); button labels and the pending-approval tile are REQUIRES CONFIRMATION. **Source:** no spec: REQUIRES CONFIRMATION (including whether the seeder may approve own configuration, BR-PREP-010).

### WF-TESTING-001 Start incubation for several samples (grouped actions)
- **Trigger / who:** A user with `TestWorkflow.Execute` ticks 2+ samples in the workspace register ("Grouped Actions" panel).
- **Prerequisites:** samples prepared (the panel shows "Incubation Setup Locked" until preparation is confirmed), tests at a setup / transfer step, released unexpired media lot of the user's section, incubator with set point in range and calibration current.
- **Steps:**
  1. Panel lists "Test Preparation Required", "Compatible Actions" (groups "Ready: Setup" / "Ready: Transfer" / "Ready: Plating") and "Tests Awaiting Individual Result Entry".
  2. User expands a group, selects "Media Lot" and "Incubator" (or "Transfer to Incubator") and presses "Start Incubation (N Tests)" / "Transfer & Start Incubation (N Tests)" / "Transfer to Incubator (N Tests)".
  3. `POST /api/test-workflow/batch-select-media`: each test goes through the same single-test engine call; results returned as started / skipped with reasons (BR-TESTING-012). Shared TSB propagates to the sample's other tests once.
  4. Message "{n} test(s) started in incubation." (+ skipped count); partial failure text "{n} of {m} started. {k} refused - {reasons}".
- **Blocking:** no lot chosen, lot unreleased or expired, incubator without set point or overdue calibration; per-test skip reasons listed in BR-TESTING-012. Result entry, biochemical tests and selective / confirmatory read-outs cannot be grouped.
- **Signature:** none. **Source:** no spec: REQUIRES CONFIRMATION.

### WF-TESTING-002 Skip minimum incubation wait (supervisor)
- **Trigger / who:** A user with `TestWorkflow.Supervise` (SectionHead, SystemAdministrator) presses "Skip Wait" in the test dialog or the incubation waiting view while the minimum duration has not elapsed.
- **Steps:** `POST /api/test-workflow/{testOrderId}/override-minimum-duration` → the open incubation of the order gets the overriding user and time → the countdown shows "Minimum wait time was skipped by a Section Head/System Administrator." → read-out buttons become available.
- **Effect:** bypasses minimum-duration and window-end checks of that incubation on every path (BR-TESTING-004..008); window and temperature are unchanged.
- **Errors:** "No incubation window is currently open." **Reason / password / signature:** none (GAP-226). **Source:** no spec: REQUIRES CONFIRMATION.

### WF-COUNT-001 Count test: media, incubation, plate counts
- **Trigger / who:** A user with `TestWorkflow.Execute` opens an assigned count test ("Open Workflow") after preparation.
- **Prerequisites:** sample prepared (FP tests excepted); item specification for the test has a Dilution Factor, a reporting Unit and (for status) limits; step medium incubation hours and temperatures configured in Test Master; a released lot of the user's section; an eligible incubator.
- **Steps:**
  1. select-media: user chooses "Media batch" and "Stage 1 Incubator" → "Start Incubation" → `POST /select-media`. Order Waiting → Incubating (Status InProgress), sample Received → InTesting, incubation row created with start, expected reading time and window text. Errors: BR-TESTING-009, BR-TESTING-010, BR-PREP-004. No signature.
  2. Waiting: "Incubation in progress", "Expected reading: {datetime}". A supervisor may "Skip Wait" (WF-TESTING-002).
  3. Two-stage steps: after the stage-1 minimum the analyst presses "Transfer to Stage 2 Incubation", picks "Stage 2 incubator" and "Start Stage 2 Incubation" → stage 1 closed, stage 2 opened with the same lot; error "Stage 1 incubation for step ... requires at least {h} hours ...".
  4. Result entry ("Record Result"): "Dilution Factor" (override needs "Justification for override"), plate fields "Plate {i}" ("Colony count or TNTC"), "Add Plate"; live "Calculated result: {display}"; "Calculate" → `POST /record-result`. Server: DF rules, unit rule, mean, calculated = mean x DF, report string, limit status (BR-COUNT-001..011). For a transfer step the server waits for the stage-2 window end; for a single-stage step only the client waits (GAP-202).
  5. TNTC / Uncountable: result "TNTC" or "Uncountable", status RequiresReview, no calculation; the dialog notes "Non-numeric result: reviewer will decide accept or retest".
  6. Step complete: "Step recorded: {outcome}". Non-final step: "Proceed to Next Step" ("Retry This Step" when the result is non-definitive); final step: order Ready (ResultEntered), history note "Workflow complete: ...", and the laboratory moves to UnderReview when all its tests are Ready.
- **Required:** media lot, incubator, at least one plate reading, DF (and justification when changed). **Optional:** none.
- **Permissions:** `TestWorkflow.Execute`, section scope; no assigned-analyst check on the single-test endpoints. 
- **Errors (exact):** BR-COUNT-002..004, BR-COUNT-016; "Plate readings are required."; "Media must be selected for step \"{step}\" before a result can be recorded."; "Workflow order violation: step \"{a}\" must be completed before \"{b}\".".
- **Signature points:** none (verified: count entry is not signed; GAP-218). **Review:** C3; reviewer must differ from the analyst, preparer and entrant. **Source:** no spec for the count calculation (dilution factor logic): REQUIRES CONFIRMATION; BusinessRules.md §4 covers water averaging only.

### WF-COUNT-002 Batch location results (EM, After Cleaning, Water)
- **Trigger / who:** A user with `TestWorkflow.Execute` opens a batch test (locations exist from preparation) whose incubation has been started.
- **Steps:**
  1. Incubation: select media and incubator (WF-COUNT-001 step 1). For templates with several windows: "Advance to Next Incubation Window" after the minimum ("close-incubation-window"), then select media again; error on the last window "This is the final incubation window - record results instead of advancing."
  2. Final window: after the minimum duration (or Skip Wait) the analyst opens the results grid ("{testCode} Results: {displayName}") and enters plate readings per location ("Add Plate"). A warning "Results cannot be submitted before {datetime}." shows until ready.
  3. "Submit Results" → `POST /batch-results` (EM, After Cleaning) or `/water-batch-readings` (Water). Server checks the final window, the minimum duration, that every location has a result and at least one reading; computes average, reported value, unit and status per location (BR-COUNT-012, BR-COUNT-013); stores the rows; writes one Result "{n} locations: {x} conform, {y} alert/action/spec (worst: {status})"; closes the incubation; order Ready; section auto-submit.
  4. Summary on screen: "{conform}/{total} locations within spec; worst status: {badge}".
  5. Pathogen test types on EM / After Cleaning: columns "Growth Observed?" (Yes / No) → "Submit Results" → `POST /batch-pathogen-results`; per location Detected or Absent; overall Detected if any location Detected (BR-OBS-001).
- **Required:** a result for every location, at least one reading each (count); a Yes / No answer each (pathogen). All-or-nothing.
- **Errors:** "No locations are assigned to this test order."; "Results are missing for: {names}."; "At least one plate reading is required for: {names}."; "\"{step}\" is not the final incubation window yet - close it and start the next window first."; "This incubation window needs at least {h} hours - not ready until {utc} UTC."; "Media must be selected for this test before batch results can be recorded."; "A growth observation is required for every location."
- **Signature points:** none. **Review:** C3. **Source:** INTENDED comparison order only (BusinessRules.md §4); EM out-of-trend not implemented (DISCREPANCY BR-COUNT-017); rest REQUIRES CONFIRMATION.

### WF-OBS-001 Qualitative observation result (physicochemical)
- **Trigger / who:** A user with `TestWorkflow.Execute` in the FP or RM & PM workspace opens a qualitative test (workflow type Qualitative, specification limit type Qualitative or PresenceAbsence).
- **Prerequisites:** the sample has an item with specifications for the test (and stage role); the test order is not yet at Ready; no active result entry exists.
- **Steps:**
  1. Panel fields: "Analysis time", "Equipment (Physicochemical)" (optional, "None (no instrument linked)"), "Comment (optional)", per parameter "Complies" / "Does not comply" and "Observation (required) *" when not compliant.
  2. "Sign and record result" → password prompt (meaning "I entered these qualitative observations and am recording this test result.") → `POST /record-qualitative-result`.
  3. Server validates (BR-OBS-004), signs (ResultRecorded), stores a TestAnalysis with one result per parameter ("Complies" → WithinLimits; "Does not comply" → OutOfSpecification), writes a Result, updates the reporting projection, moves the order to Ready and auto-submits the laboratory for review when all its tests are Ready.
- **Errors:** "Password is required to sign the result."; "Analysis time cannot be in the future."; "An observation is required when result does not conform."; "Observation cannot exceed 500 characters."; "An active Qualitative entry already exists for this test order."; "No specifications configured for this test and item."; equipment "is not active" / "is not in the Finished Product (FP) section."
- **Signature point:** yes, ResultRecorded. **Detail and other physicochemical panels:** C6. **Source:** no spec: REQUIRES CONFIRMATION.

### WF-PATH-001 Pathogen test step chain (single test)
- **Trigger / who:** A user with `TestWorkflow.Execute` opens a pathogen test ("Open Workflow"); the dialog routes by step type (broth, selective plating, confirmatory plating, biochemical).
- **Prerequisites:** sample prepared; the Test Master template defines the chain, target organism, permitted media (with incubation windows) and, for confirmatory plating, the media panel (C4).
- **Steps:**
  1. Broth step (BrothEnrichment / SelectiveBroth): choose "Media lot" and "Incubator" (medium fixed by Test Master) → "Start Incubation"; when a shared TSB step is already running for the sample the panel shows "Shared TSB Applied" and "Proceed to Next Step →". After the minimum duration "Proceed to {next step}" submits the broth step (`submit-broth`; error "This step requires at least {h} hours of incubation - not ready until {utc} UTC.").
  2. Selective plating: start incubation (lot, incubator, optional start time) → after the minimum duration read the plate: "No growth", "Growth, does not match expected appearance" (both end the test as NotDetected) or "Growth matching expected appearance" (next step) → "Record Observation". Result row Not Detected, order Ready.
  3. Confirmatory plating: tick every medium actually plated, choose lot and incubator per medium → "Start Incubation" (one shared window to the longest medium) → after the window ends "Submit Observations" (one per medium). All conforming → AllConforming; otherwise Inconclusive (flag, history note).
  4. After AllConforming the analyst chooses "Conclude as Detected" (result Detected, flagged BiochemicalNotPerformed) or "Proceed to Biochemical Confirmation". After Inconclusive only the biochemical route exists.
  5. Biochemical test: result text (required), optional attachment, organism detected yes / no → Detected or NotDetected; only the last biochemical step finalizes.
  6. Finalizing writes the Result, moves the order to Ready, re-projects the result and auto-submits the laboratory for review when all tests are Ready.
  7. A Detected result submitted without biochemical confirmation can be returned by a reviewer (`TestWorkflow.BiochemicalDecision`) for confirmation: order Ready → Incubating, analyst notified (no UI caller found: REQUIRES CONFIRMATION of how reviewers reach it).
- **Required:** lot, incubator per start; observation per read-out; biochemical text. **Optional:** observation note, attachment.
- **Blocking / errors:** BR-PATH-002..009, BR-TESTING-006, BR-TESTING-009..011, "Test order {id} is already at {step} - its workflow can no longer be submitted against.", "Workflow order violation ...".
- **Signature points:** none (GAP-216, GAP-218). **Review:** C3 with segregation. **Source:** INTENDED in BusinessRules.md §3 / WorkflowDiagrams.md but DISCREPANCY (BR-PATH-016, GAP-205): spec chain not present; chain is template-driven.

### WF-PATH-002 Pathogen testing session (shared TSB, matrix, confirmatory plating, complete)
- **Trigger / who:** A user with `TestWorkflow.Execute` presses "Open Pathogen Workflow" (shown when a test of the sample uses shared TSB).
- **Prerequisites:** sample prepared; locations created at preparation; Test Master TSB steps with windows; released TSB lot and in-service incubator.
- **Steps:**
  1. "Overview" shows locations, assigned tests, required results (locations x tests) and completed count; buttons "Open Shared TSB Setup" / "Open Test Workflows".
  2. "Shared TSB": choose "TSB Media Lot (GPT Passed & Released) *" and "Incubator Equipment (In-Service) *" (temperature and time read-only) → "Start TSB Enrichment" → `POST /pathogen-session/{sampleId}/start-tsb`. All tests whose TSB medium accepts the lot start together; locked state "LOCKED: Execution in Progress" while incubating. Errors BR-PATH-015.
  3. "Primary Matrix": after the TSB window, per location x test: quantitative cell (CFU) or qualitative select ("No Growth (-)", "Growth Non-Conforming", "Growth Conforming (+)") → "Save Observations" (`save-primary-observations`) or "Save Observations"/matrix (`save-matrix`); "Fill All Unset as No Growth (-)" available; cells stay locked until prerequisites finish (BR-PATH-013).
  4. "Confirmatory Plating (Panel B)": flagged locations (growth observed) → "1. Shared Media Setup" (lots, incubator, start time) → "Start Confirmatory Incubation" → "2. Plate Observations" per medium ("No Growth", "Non-Conf", "Conf (+)") → "Save Readings & Continue". Server stores the plates and, once all required media are read, sets each location to Detected (+), Not Detected (-) or Inconclusive (Retest) (BR-PATH-010, GAP-223).
  5. "Review & Complete": table "Final Analytical Results Summary"; "Complete Testing Session" → `POST /complete`. Refused while results are missing or tests are still TSB incubating (BR-PATH-011). On success every test order is Ready and the sample is UnderReview.
- **Required:** lot and incubator for TSB and confirmatory media (count fixed per test, BR-PATH-014); an entry for every location x test.
- **Permissions:** `TestWorkflow.Execute`; section access; no assigned-analyst check; no password.
- **Signature points:** none (GAP-216). **Review:** C3. **Source:** DISCREPANCY with BusinessRules.md §3 (BR-PATH-016); relationship to WF-PATH-001 REQUIRES CONFIRMATION (BR-PATH-017).

### WF-PATH-003 Reset pathogen session (supervisor)
- **Trigger / who:** A user with `TestWorkflow.Supervise` presses "Reset Session" in the session workspace.
- **Steps:**
  1. Dialog "Reset Testing Session & Steps?": field "Reason for Reset *"; "Confirm Reset" → `POST /pathogen-session/{sampleId}/reset`.
  2. Server refuses Voided and Rejected samples (BR-PATH-012). Otherwise it permanently deletes all observations, step results, readings, results and incubations of every test order of the sample, sets orders to Waiting / Pending, sets InTesting or UnderReview samples back to Received, and writes an audit-log entry "ResetWorkflowSteps" with the reason and workflow history notes.
- **Required:** none on the server (default reason text); the dialog requires a reason. **Signature / password:** none.
- **Blocking / errors:** "Sample #{ref} has been voided and its testing session cannot be reset."; "Sample #{ref} has been rejected and its testing session cannot be reset. A rejection is reversed through the approval workflow by a Section Head."
- **Final state:** sample Received, tests Waiting; preparation is not reset. Statuses UnderApproval, Approved, RetestRequested, Cancelled are not refused (GAP-221).
- **Source:** no spec: REQUIRES CONFIRMATION.

---

## business-rules

Labels: IMPLEMENTED unless marked. Calculations as coded.

### MOD-SAMPLE-001 Sample receiving and register
- BR-SAMPLE-001 (IMPLEMENTED, verified): Receipt of Product/RM/PM requires a valid item, cause of testing, batch number, control number and "Sampled By". Errors are joined with a single space into one message. A Finished Product also needs a known production stage ("Production stage is required for a Finished Product sample." / "Production stage '{stage}' is not a known stage."). A frozen (inactive) item is refused ("Item '{name}' is frozen and cannot be used to receive new samples."). The laboratories are chosen per sample (`TargetSectionIds`): "Choose at least one laboratory to receive the sample for."; "One or more selected laboratories do not exist."; "You can receive samples only for your own laboratory."; a chosen laboratory with none of the item's tests: "Item '{name}' has no tests for {lab name}." [src: backend/MicroLIMS.Application/Validators/ReceiveSampleValidator.cs:13-17; Services/ReceivingService.cs:24; Workflows/ProductWorkflowEngine.cs:51-73, 104-116; Services/ReceiptLabGuard.cs:17-30]
- BR-SAMPLE-002 (IMPLEMENTED, corrected): For Product / RM / PM only, receipt creates one test order per configured test (restricted to the chosen laboratories), each with status Pending and step Waiting; the item must have assigned tests ("Item '{name}' has no assigned tests. Configuration must be completed by the Section Head before samples can be received."). Water, Environmental Monitoring and After Cleaning receipts create the sample (Status Received, preparation NeedsPreparation) and NO test orders; test orders and sample locations are created at preparation (BR-PREP-006). [src: backend/MicroLIMS.Application/Workflows/ProductWorkflowEngine.cs:54-57, 118-127; WaterWorkflowEngine.cs:49-71; EMWorkflowEngine.cs:35-56; AfterCleaningWorkflowEngine.cs:41-76]. INTENDED (BusinessRules.md §2: one TestOrder per configured test at Waiting / Pending; an item with zero tests is refused; no manual TestOrder creation): matches for Product / RM / PM. The spec lists "the five ReceiveAsync methods" as the creation point; for Water / EM / After Cleaning the orders are created at prepare (minor DISCREPANCY, documentation only).
- BR-SAMPLE-003 (downgraded from DISCREPANCY to a UI-versus-server mismatch; no spec involved): the New Sample grid marks Batch No. and Control No. with an asterisk for Product / RM / PM, but `validateRows` does not check them; the backend refuses an empty value with "Batch number is required." / "Control number is required." (joined into one message). EM, Water and After Cleaning receipts have no server check on control number (the EM / Water validators are not wired, see validations). Affects SCR-SAMPLE-002. See GAP-200. [src: MultiSampleEntryGrid.tsx:218-228; NewSampleDialog.tsx:170-225; ReceiveSampleValidator.cs:15-16]
- BR-SAMPLE-004 (IMPLEMENTED, verified): Sample status moves Received -> InTesting on the first incubation start when still Received (and the test order moves Waiting -> Incubating, Status InProgress). [src: backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs:117-122]. Also on first selective-plating start. [PathogenConfirmationRecorder.cs:293-298]. A sample whose tests never incubate (Finished Product) stays Received until its results complete; the auto-submit for review treats Received and InTesting alike. [src: SampleReviewService.cs:40-44]
- BR-SAMPLE-005 (IMPLEMENTED, verified, extended): Sample details can be corrected only while status is Received or InTesting. Otherwise: "Sample details can only be corrected until the sample is submitted for review - this record is locked." [src: backend/MicroLIMS.Application/Services/SampleCorrectionService.cs:66-67, 83-85]. The sample's item (Product / RM / PM) or location (Water department, EM department, After Cleaning machine) can additionally be changed only before any test work exists (status still Received, every test order still Waiting, and no incubation, result, reading, observation, media usage or entered location result). Otherwise: "Testing has already started for this sample, so its item or location can no longer change. Void the sample and receive it again." A retest sample cannot change item or location ("A retest sample must test the same item or location as its original sample, so it cannot be changed."). An item/location change deletes the sample's preparation, removes test orders the new item does not require, and returns the sample to NeedsPreparation (FP-only items return to Ready). The "Retest" cause of testing can be neither set nor removed here ("The \"Retest\" cause of testing is assigned by the system to retest samples and cannot be set or changed here."). Frontend lock texts: GAP-201 (downgraded). [src: SampleCorrectionService.cs:106-113, 189-209, 369-464]
- BR-SAMPLE-006 (IMPLEMENTED, verified): Correction requires a non-blank reason ("A reason for the correction is required."), a valid password (signature SampleCorrected), and at least one changed field ("No changes were made."). Blank required fields are refused with "{label} is required." (labels "Control number", "Sampled by", "Batch number", "Production stage", "Previous product", "Previous product batch number"). Expiry earlier than manufacture: "The expiry date cannot be before the manufacturing date." (frontend text differs: "Expiry Date must be after Manufacturing Date"; equal dates accepted by server). On success result projection rows copy the new batch and control numbers. [src: SampleCorrectionService.cs:77-78, 101-102, 124-128, 212, 225-232, 480-481; verified-roles-permissions.md signature #5]
- BR-SAMPLE-007 (IMPLEMENTED, verified; REQUIRES CONFIRMATION for scope): Void requires a non-blank reason ("A reason for voiding is required."), is signed (SampleVoided), and is refused only when the sample is already Voided ("Sample is already voided."). On success: sample Status = Voided; CertificateRemarks = "Voided: {reason}"; each test order Status = Voided; section sign-off rows Voided; record status Voided. No status precondition exists in code for approved or rejected samples (GAP-203). [src: SampleCorrectionService.cs:253-303]
- BR-SAMPLE-008 (IMPLEMENTED, verified): Assign analyst sets `AssignedAnalystId` on every test order of the sample that is not superseded and not Approved or Rejected. Audit action: "AssignedAnalyst" (none before), "ReassignedAnalyst" (changed), "UnassignedAnalyst" (cleared); value text "{new} | Reason: {reason}" when a reason is given (reason optional in code). [src: backend/MicroLIMS.Application/Services/SampleAssignmentService.cs:28-70]
- BR-SAMPLE-009 (IMPLEMENTED, verified; messages now read): Add laboratory requires a non-blank reason ("A reason is required to add a laboratory."); is refused when the OVERALL sample status (computed from all laboratory sign-offs) is Rejected, Voided or Cancelled ("A laboratory cannot be added to a rejected, voided or cancelled sample."); when that laboratory already has a test on the sample ("This laboratory is already testing the sample."); for samples without an item ("A laboratory can be added only to a product, raw material or packaging material sample."); and when the item has no tests for that laboratory ("Item '{name}' has no tests for {lab name}."). It is signed (LaboratoryAdded), creates that laboratory's test orders at Waiting/Pending, sets that laboratory's sign-off to InTesting (re-opening a sample whose other laboratories had finished; a recorded Approve decision on the sample is cleared when the roll-up returns to InTesting), and returns the sample to NeedsPreparation when the new tests need preparation and no preparation exists yet. The UI shows the action unless the sample status is Rejected, Voided or Cancelled; the server rule uses the overall status. [src: backend/MicroLIMS.Application/Services/AddLaboratoryService.cs:27-75; SampleActionMenu.tsx:73; verified-roles-permissions.md signature #4]
- BR-SAMPLE-010 (IMPLEMENTED, verified): Sample Register status display maps RetestRequested, Cancelled and Voided to one label "Cancelled / Voided" (DISCREPANCY-candidate, GAP-204). The same map also contains a "PendingReview" key that is not a SampleStatus value. [src: SampleRegisterTable.tsx:59-69]
- BR-SAMPLE-011 (IMPLEMENTED, verified): Sample status roll-up. Each laboratory section of a sample has a sign-off status (InTesting, UnderReview, UnderApproval, Approved, Rejected, RetestRequested, Cancelled, Voided). `Sample.Status` shows the least advanced OPEN section (InTesting before UnderReview before UnderApproval), then Rejected, then RetestRequested, else Approved; all sections Voided gives Voided. The user-facing "Overall" status (Tracking Board) is separate: Voided, Cancelled, then Rejected if any section Rejected, InProgress if any section is open, RetestRequested, else Approved. [src: backend/MicroLIMS.Application/Services/SampleSectionRollup.cs:L73-L106]
- BR-SAMPLE-012 (IMPLEMENTED, verified): When every current (non-superseded) test order of a section reaches step Ready, the section moves InTesting -> UnderReview automatically (log "All tests completed - automatically submitted for review"). Called at the end of count, batch, physicochemical and pathogen-step final results. The pathogen session "Complete" path sets the sample status itself (BR-PATH-011). [src: SampleReviewService.cs:L43-L55, L66-L87; IncubationStepRecorder.cs:L339, L542, L647, L719; PathogenConfirmationRecorder.cs:L892]

### MOD-PREP-001 Sample preparation
- BR-PREP-001 (IMPLEMENTED, verified): A Product / RM / PM sample can be prepared once: "This sample has already been prepared." EM, After Cleaning and Water refuse a second preparation when PreparationStatus is not NeedsPreparation with the same text. [src: backend/MicroLIMS.Application/Services/SamplePreparationService.cs:153-154; EMWorkflowEngine.cs:63-64; WaterWorkflowEngine.cs:78-79; AfterCleaningWorkflowEngine.cs:83-84]
- BR-PREP-002 (IMPLEMENTED, verified, scope corrected): If an analyst is already assigned on a non-superseded test order, only that analyst may prepare a Product / RM / PM sample, whatever the role of the acting user: "This sample is assigned to {name}. Only the assigned analyst may perform sample preparation, unless reassigned by an authorized Section Head." The text implies a Section Head can prepare; in code a Section Head must first reassign the sample. The preparation form UI exempts the SectionHead and SystemAdministrator roles from its own disabled state (GAP-219). Grouped preparation excludes such samples with "Assigned to {name} - only the assigned analyst may prepare this sample." [src: SamplePreparationService.cs:157-170, 300-304; TestPreparationForm.tsx:29-33]
- BR-PREP-003 (IMPLEMENTED, verified, corrected): Manual (first-time) preparation of an item stores Sample Amount, Technique (Pour Plate or Filtration), Filtration Volume and Washing Volume (Filtration only), Diluent and Neutralizer (trimmed). Server rules: "Diluent is required."; "Neutralizer is required."; for Filtration "Filtration technique requires both Filtration Volume and Washing Volume." Amount is not validated (GAP-228). The same values become the item's standing preparation configuration, created as PendingReview, usable immediately by later samples. Confirming from an existing configuration copies the configured values into the sample's own preparation record (later edits to the configuration do not change it) and re-validates them. Both paths sign as PreparationConfirmed against the sample, set preparation status Ready and assign the signing user as analyst on every test order still at Waiting. Samples without an Item: "This sample has no Item and cannot use the preparation configuration flow."; none to confirm: "This item has no preparation configuration to confirm." Grouped confirmation pins the configuration: "This item's preparation configuration changed after the group was loaded. Reload the grouped action and review the steps before confirming." [src: SamplePreparationService.cs:50-146, 174-206; PreparationParameterValidator.cs:22-37]
- BR-PREP-004 (IMPLEMENTED, verified, corrected messages): Preparation status is NeedsPreparation until confirmed; the test-start gate (AssignedTestCard) disables "Open Workflow" until it is cleared, except tests with `skipsPreparation`. Server refusals when media/incubation is started before preparation (`select-media`, selective-plating start, shared TSB start, grouped start): "Test Preparation must be completed and confirmed for sample {ref} before incubation can be started for step \"{step}\"." (error code PreparationNotConfirmed; the refusal is written to the audit log as TestStartRefused and to workflow history). For EM and After Cleaning also "Preparation not complete - no locations assigned to this test." Other wordings: "Test Preparation must be completed and confirmed for sample {ref} before testing can start." (product engine advance) and "Test preparation must be completed and confirmed for sample {ref} before the test can be started." (step navigator validate). Grouped media start skips with "Test Preparation is not complete for this sample." or, for EM / After Cleaning, "Sample preparation is not complete (locations/swabs not verified)." For Product / RM / PM "prepared" also requires a SamplePreparation record. [src: TestWorkflowSupport.cs:281-363; ProductWorkflowEngine.cs:141-184; TestStepNavigator.cs:253-295; BatchSelectMediaRules.cs:60-63; AssignedTestCard.tsx:69, 488-502]
- BR-PREP-005 (IMPLEMENTED, verified, corrected): Water storage at PREPARATION: condition required ("Storage condition is required for water samples."); refrigerated requires time ("Storage time is required when a water sample was refrigerated."); stored time is kept only for Refrigerator. The prepare endpoint does NOT check the condition value against an allowed list. The CORRECTION path (edit details after preparation) does: "Storage condition is required for a prepared water sample."; "Storage condition must be Refrigerator or RoomTemperature."; "Storage time is required when a water sample was refrigerated."; "Storage time cannot be negative." Front end requires hours only when Refrigerator. Water prepare also requires at least one sampling point: "At least one sampling point must be selected." [src: WaterWorkflowEngine.cs:81-89, 158-161; SampleCorrectionService.cs:158-170; WaterPreparationForm.tsx:31-33]
- BR-PREP-006 (IMPLEMENTED, verified; resolves the "server body not read" note): EM, After Cleaning and Water preparation create one test order per distinct test code across the selected rooms / machine parts / sampling points (status Pending, step Waiting, section from the test definition, `AssignedAnalystId` = the preparing user) and one sample location per selected room configuration / machine-part configuration / (sampling point x assigned test code); Water links the sampling configuration to count-test locations. Then preparation status becomes Ready. No signature, no ownership check, no scope call. Validations: "At least one room must be selected." / "At least one machine part must be selected." / "At least one sampling point must be selected."; "Room test configuration(s) not found: {ids}." / "Machine part configuration(s) not found: {ids}." / "Sampling point(s) not found: {ids}."; "Room(s) {names} do not belong to this sample's department." / "Part(s) {names} do not belong to this sample's machine." / "Sampling point(s) {codes} do not belong to this sample's department." [src: EMWorkflowEngine.cs:58-114; WaterWorkflowEngine.cs:73-164; AfterCleaningWorkflowEngine.cs:78-134]. INTENDED (BusinessRules.md §6): After Cleaning creates ONE collective pathogen test order (when any selected part is flagged collective) plus ONE individual TAMC order per part (test code "TAMC:{partName}"). DISCREPANCY: code creates one order per distinct test code and has no collective flag handling (`git grep IsCollectivePathogenSample` and `TAMC:` find no use in backend code). GAP-225.
- BR-PREP-007 (IMPLEMENTED, verified): Only Product / RM / PM preparation is signed (PreparationConfirmed); EM / AC / Water preparation writes no signature (code read; unsigned). Whether that is intended: REQUIRES CONFIRMATION (GAP-212). [src: SamplePreparationService.cs:184-186; EMWorkflowEngine.cs:58-114]
- BR-PREP-008 (IMPLEMENTED, verified): Item preparation configuration (Section Head territory, permission `Items.Manage`): saving validates Diluent, Neutralizer and the Filtration rule (BR-PREP-003), stores trimmed values and ALWAYS sets approval to PendingReview, clearing any previous approver; an unknown item gives "Item {id} not found." [src: ItemPreparationConfigurationService.cs:52-83]
- BR-PREP-009 (IMPLEMENTED, verified): Approving a configuration (`Items.Manage`): refused when none exists ("Item {id} has no preparation configuration to approve.") or already approved ("This preparation configuration is already approved."); records approver and time. Approval is not an electronic signature and takes no password. An unapproved configuration is still used by analysts ("testing is never blocked waiting for approval"). The pending queue lists PendingReview configurations oldest first. [src: ItemPreparationConfigurationService.cs:85-116; ItemPreparationConfigurationController.cs:45-67]
- BR-PREP-010 (REQUIRES CONFIRMATION): Segregation between the analyst who seeded a configuration and the Section Head who approves it: not checked in code. [src: ItemPreparationConfigurationService.cs:85-99]
- BR-PREP-011 (IMPLEMENTED, verified): FP-section tests have no preparation stage. A sample whose tests are all in section code "FP" starts at Ready; a mixed sample starts at NeedsPreparation but a FP test order is not blocked by the preparation gate. [src: PreparationRules.cs:L7-L30]

### MOD-TESTING-001 Laboratory workspaces
- BR-TESTING-001 (IMPLEMENTED, verified): Incubation window is configured per step medium only when IncubationMinHours > 0, IncubationMaxHours >= IncubationMinHours, TempMax > 0 and TempMax >= TempMin. [src: backend/MicroLIMS.Application/Helpers/IncubationWindowResolver.cs:28-32]
- BR-TESTING-002 (IMPLEMENTED): Minimum ready time = incubation start + MinHours; window end = start + MaxHours. [src: IncubationWindowResolver.cs:12-13]
- BR-TESTING-003 (IMPLEMENTED): Unconfigured window blocks the step: "The incubation window for {medium} on step {step} is not configured - set its incubation hours and temperature in Test Master." [src: IncubationWindowResolver.cs:42-49]
- BR-TESTING-004 (IMPLEMENTED, verified): Batch result submission (EM/AC/Water count, and pathogen batch) and "close window" require the minimum duration (start + MinHours of the selected step medium, measured from the incubation's recorded `StartedAt`) to have elapsed unless Skip Wait was used: "This incubation window needs at least {h} hours - not ready until {utc} UTC." [src: backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs:378-386, 419, 469, 580, 673]. For a step with an incubation transfer the batch check uses stage 2: minimum = stage-2 incubation min hours (else the medium's) from the stage-2 start: "Stage 2 incubation for step \"{step}\" requires at least {h} hours of incubation - not ready until {utc} UTC." [IncubationStepRecorder.cs:392-406] Batch results are accepted only while the order's current step is Incubating: "Media must be selected for this test before batch results can be recorded."
- BR-TESTING-005 (IMPLEMENTED, verified, refined): Single-stage count result (`record-result`, PlateCount, no transfer): the server performs NO elapsed-time check. For a step with an incubation transfer the server requires stage 2 to be started ("Step \"{step}\" requires stage 2 incubation to be started before a count can be recorded.") and then requires the stage-2 window END (start + max hours, `IncubationEndUtc`) to have passed, not the minimum: "This step's incubation period has not finished yet." (with remaining seconds). Skip Wait bypasses both. The client disables "Calculate" until its own ready check passes. [src: IncubationStepRecorder.cs:253-270; TestWorkflowSupport.cs:810-817; TestWorkflowDialog.tsx:803] — REQUIRES CONFIRMATION (GAP-202).
- BR-TESTING-006 (IMPLEMENTED, verified): Pathogen selective plating observation refused before minimum duration: "Minimum incubation time not elapsed. Available from: {utc} UTC." [src: backend/MicroLIMS.Application/Workflows/TestWorkflow/PathogenConfirmationRecorder.cs:326-337]. Submitting a broth step (`submit-broth`) is refused before the medium's minimum with "This step requires at least {h} hours of incubation - not ready until {utc} UTC." [PathogenConfirmationRecorder.cs:183-195]. Confirmatory read-out waits until the declared window END: "This step's incubation period has not finished yet." [PathogenConfirmationRecorder.cs:549; TestWorkflowSupport.cs:810-817]. Skip Wait bypasses each check.
- BR-TESTING-007 (IMPLEMENTED, verified): Skip Wait (`override-minimum-duration`, policy TestWorkflow.Supervise, held by SectionHead and SystemAdministrator by default) records MinimumDurationOverriddenAt and the overriding user on the currently open incubation of the test order; subsequent elapsed-time checks are bypassed for that incubation. It takes no reason, no password and writes no signature; window values are not changed. Error when nothing is open: "No incubation window is currently open." [src: IncubationStepRecorder.cs:431-443; TestWorkflowController.cs:543-549; frontend text TestWorkflowDialog.tsx:624-629] (GAP-226)
- BR-TESTING-008 (IMPLEMENTED, verified): Stage 2 transfer requires stage 1 to have reached its minimum: "Stage 1 incubation for step \"{step}\" requires at least {h} hours of incubation - not ready until {utc} UTC." (error code IncubationStage1NotComplete, with remaining seconds; Skip Wait bypasses). Stage 2 start cannot precede stage 1 start ("Stage 2 start time ({t}) cannot precede Stage 1 start time ({t}) UTC."). Stage 2 reuses the stage 1 media lot and takes the new incubator and the stage-2 hours and temperatures from the step's stage-2 configuration ("Step \"{step}\" has no stage 2 configuration." when missing). It closes stage 1 with outcome "Transferred to stage 2 incubation." and opens stage 2. The stage-2 start runs no incubator temperature-range check against the medium (no `RequireEligibleIncubatorAsync` call in `StartStage2IncubationAsync`); the grouped "Transfer to Incubator" action only checks that the incubator exists, has a set point and has an unexpired calibration due date, and the UI lists incubators in range. [src: IncubationStepRecorder.cs:159-222; BatchSelectMediaRules.cs:L33-L47] (REQUIRES CONFIRMATION whether intended, GAP-220)
- BR-TESTING-009 (IMPLEMENTED, verified): Media must be selected before a result: "Media must be selected for step \"{step}\" before a result can be recorded."; media must be released, not out of stock, not rejected: "Media lot \"{lot}\" is not released for use, out of stock, or rejected." Step must have media configured (broth steps): "Step \"{step}\" has no media configured in Test Master."; wrong medium: "This step requires a different medium. The selected lot is {material name}."; lot from another section's material: "Media lot \"{lot}\" was prepared from another laboratory section's material and cannot be used for this test."; incubator outside the medium range OR with an expired calibration: "The selected incubator's set point is outside this medium's temperature range." The single-test path does not check the lot's expiry date (GAP-220). [IncubationStepRecorder.cs:44-80; SectionMediaRule.cs:L12-L18; IncubatorEligibilityService.cs:L29-L45; TestWorkflowSupport.cs:803-808]
- BR-TESTING-010 (IMPLEMENTED, verified): Workflow order: earlier step must be completed: "Workflow order violation: step \"{a}\" must be completed before \"{b}\"." Media for a step cannot be selected twice: "Media has already been selected for step \"{step}\" - awaiting its result." [IncubationStepRecorder.cs:33-44]
- BR-TESTING-011 (IMPLEMENTED, verified): Shared TSB start from a test (`select-media` on a shared-TSB step) is refused if any sibling pathogen test that would join has no window configured or its incubator is outside the test's medium range. Messages: "The shared TSB can't start: the incubation window for \"{medium}\" on test {code} (step \"{step}\") is not configured - set its incubation hours and temperature in Test Master." / "The shared TSB can't start: the selected incubator's set point is outside the {min}-{max} °C range of \"{medium}\" on test {code}." Start then propagates the incubation to siblings (PropagateSharedTsbToSiblingOrdersAsync). [IncubationStepRecorder.cs:85-87, 126-152]
- BR-TESTING-012 (IMPLEMENTED, verified): Grouped actions (`batch-select-media`) require the permission `TestWorkflow.Execute` and section access to every listed order; then, per order, skip with a reason when: the test is assigned to another analyst (Analyst role only), the lot belongs to another section ("Media lot \"{lot}\" was prepared from another laboratory section's material."), the sample is not prepared, the order has finished all steps ("Test order has completed all workflow steps."), the open incubation is ready for individual result entry ("Test order requires individual colony count entry. Result entry cannot be grouped." / "Test order requires individual selective plate observation. Observations cannot be grouped." / "Test order requires individual confirmatory readout. Observations cannot be grouped."), or the step is a biochemical test ("Biochemical test entry must be completed individually."). The whole request is refused before any order is touched when no media lot is chosen ("Media lot must be selected for this workflow transition."), the lot is not released / out of stock / rejected, the lot is expired ("Media lot \"{lot}\" is expired (expired on {date})."), the incubator is not found, has no calibrated set point ("Incubator \"{code}\" has no calibrated set point temperature.") or its calibration is overdue ("Incubator \"{code}\" calibration expired on {date}."), or the step name looks like result entry ("Grouped actions cannot be executed against analytical result-entry operations. Result entry must be performed individually."). Each started order goes through the same engine methods as a single start, so the per-test window, incubator range and preparation checks still apply. Results are partial: succeeded and skipped lists with counts. No signature, no password. [src: GroupedTestActionService.cs:424-716; BatchSelectMediaRules.cs:8-60; frontend GroupedActionRow.tsx:141-158]
- BR-TESTING-013 (IMPLEMENTED, verified): Incubator eligibility = type Incubator, a set point recorded, set point within the step medium's temperature range, and calibration due date empty or not past. An overdue-calibration incubator is therefore not listed and, if forced, refused with the temperature-range text. [src: IncubatorEligibilityService.cs:L24-L45]
- BR-TESTING-014 (IMPLEMENTED, verified): A test order at Ready, Reviewed or Approved can no longer be submitted against: "Test order {id} is already at {step} - its workflow can no longer be submitted against." (pathogen steps, analyst decision and physicochemical results). Count results are blocked indirectly (no open incubation remains). [src: TestWorkflowSupport.cs:763-768]
- BR-TESTING-015 (IMPLEMENTED, verified): The first step of an order opened from the Waiting state moves it to Incubating (Status InProgress); the final result moves it to Ready (Status ResultEntered), each written to WorkflowHistory with the performing user and a note. Shared TSB start through the pathogen session sets CurrentStep Incubating but leaves Status Pending. [src: WorkflowStateMachine.cs:L13-L36; PathogenSessionService.cs:L635-L636]

### MOD-COUNT-001 Count tests
- BR-COUNT-001 (IMPLEMENTED, verified): Direct-count sample categories (Water, Environmental Monitoring, After Cleaning) force Dilution Factor = 1 regardless of input. [src: IncubationStepRecorder.cs:774-783]
- BR-COUNT-002 (IMPLEMENTED, verified): For other categories with an Item (Product / RM / PM), a Dilution Factor must be configured on the item's primary Specification for this test, else: "No Dilution Factor is configured for \"{code}\" on this item's Specifications. Configure it before recording a result." [IncubationStepRecorder.cs:793-802]
- BR-COUNT-003 (IMPLEMENTED, verified): Entered DF different from the configured DF needs a non-blank note: "The entered Dilution Factor ({x}) differs from the configured value ({y}). A justification note is required." The note, an overridden flag and the configured value are stored with the reading (DilutionFactorOverrideNote, DilutionFactorOverridden, ConfiguredDilutionFactor). The server does not check that the DF is greater than zero (GAP-228). [IncubationStepRecorder.cs:804-809, 855-857, 918-920]
- BR-COUNT-004 (IMPLEMENTED, verified): Reporting unit must be configured on the item Specification: "No reporting Unit is configured for \"{code}\" on this item's Specifications. Configure it before recording a result." [IncubationStepRecorder.cs:815-819]
- BR-COUNT-005 (IMPLEMENTED, verified, refined): Non-numeric: if any reading equals "TNTC" or "Uncountable" (case-insensitive), no average or calculated result is produced and no limits are compared; Status = RequiresReview, HasNonNumericReading = true, RequiresReview = true. ReportedResult is "TNTC" when the first reading that does not parse as a number is the text TNTC, otherwise "Uncountable" (a mix of other free text and TNTC is therefore reported as "Uncountable"). The step still completes (test goes to Ready) and the reviewer decides accept or retest. The DF, justification and unit checks (BR-COUNT-002..004) run first. [IncubationStepRecorder.cs:767-869]
- BR-COUNT-006 (IMPLEMENTED, verified): Numeric branch: Average = arithmetic mean of all raw plate readings (none excluded); CalculatedResult = Average x DilutionFactor (full precision stored). [IncubationStepRecorder.cs:873-878]
- BR-COUNT-007 (IMPLEMENTED, verified): ReportedResult: if Calculated < the entered DilutionFactor -> "<{DF rounded away from zero} {unit}" (DF 1 gives "<1 {unit}"); otherwise Math.Round(Calculated, MidpointRounding.AwayFromZero) + " " + unit (whole number). [IncubationStepRecorder.cs:879, 906-909]
- BR-COUNT-008 (IMPLEMENTED, corrected): Unit shown = the configured Specification (or water sampling-configuration) unit, prefixed "CFU/" when it does not already start with it; if blank, `GetCfuUnit(category, null)`. `prepUnit` is always null in this call, so the fallback is "CFU/mL" (Water), "CFU/plate/4h" (EM), "CFU/25cm2" (After Cleaning), "CFU/unit" (other). Item-based samples always have a configured unit (BR-COUNT-004) and EM / After Cleaning never reach this method (BR-COUNT-015), so the Haiku variants keyed on prepUnit "25cm2" / "ml" are unreachable. [IncubationStepRecorder.cs:826, 895-897, 949-967]
- BR-COUNT-009 (IMPLEMENTED, verified): Status = SpecLimitParser.Compare(Calculated, Alert, Action, Spec): OutOfSpecification if the Spec limit is exceeded, else ActionLimitExceeded, else AlertLimitExceeded; LimitsNotConfigured when none of the three parses to a limit; else WithinLimits. Limits: the item's primary Specification, or for a legacy single-point water sample the SamplingConfiguration for the test code and sampling point. [SpecLimitParser.cs:208-226; IncubationStepRecorder.cs:881-893, 910]
- BR-COUNT-010 (IMPLEMENTED, verified, extended): Limit text: a plain number (thousands separators allowed) or "NMT x" is an upper bound; "NLT x" is a lower bound; "a-b" (hyphen or dash variants, a not above b) is a range; "t ± d", "t +/- d", "t ± d%" is a range around the target. A value is exceeded when below Min or above Max (equal to a limit is not exceeded). Unparseable text is treated as no limit and never raises an error, so a mistyped limit can silently give "Limits Not Configured". [SpecLimitParser.cs:19-21, 37-206]
- BR-COUNT-011 (IMPLEMENTED, verified): A Result row is written (RawValue = readings comma-joined, InterpretedValue "{reported} ({status})", type Numeric, entered by the user). The CountTestReading stores readings, DF, average, calculated, reported, limit snapshot and status, and feeds the reporting projection. The incubation is closed with the outcome summary. [IncubationStepRecorder.cs:295-310, 935-946]
- BR-COUNT-012 (IMPLEMENTED, verified): Batch count per location (EM / AC): average of readings; DF = 1; calculated = average; ReportedResult "<1" if average < 1, else whole number (AwayFromZero); limits from the RoomTestConfiguration or MachinePartConfiguration, same Spec > Action > Alert order; unit from the configuration, else derived from the test type (passive air "CFU/plate/4 hours", surface/air "CFU/25 cm2", swab "CFU/25 cm2", rinse "CFU/mL", water point "CFU/mL"; unmapped type raises "No unit mapping for SampleLocation {id} ..."). Summary "{n} locations: {conform} conform, {rest} alert/action/spec" where only WithinLimits counts as conform (LimitsNotConfigured counts in the second figure). Result reads "{summary} (worst: {status})", severity Spec > Action > Alert > LimitsNotConfigured. [IncubationStepRecorder.cs:485-530, 728-763]
- BR-COUNT-013 (IMPLEMENTED, verified): Batch count per water point: average; ReportedResult = Math.Round(average, AwayFromZero) whole number (no "<1" rule, so an average below 0.5 reports "0"); limits from SamplingConfiguration; same summary and worst-status logic; no dilution factor stored. [IncubationStepRecorder.cs:596-650]
- BR-COUNT-014 (IMPLEMENTED, verified): Batch completeness: "No locations are assigned to this test order."; "Results are missing for: {names}."; "At least one plate reading is required for: {names}." All locations are submitted together; the batch is accepted or refused as a whole. [IncubationStepRecorder.cs:472-483, 583-594]
- BR-COUNT-015 (IMPLEMENTED, verified): Any test order with sample locations (EM, After Cleaning, batch-prepared Water) sent to the single-result endpoint is refused: "EM/After Cleaning results must be submitted via the batch results endpoint." The legacy `POST /api/water/calculate` refuses a batch-prepared order: "This water test was prepared across multiple sampling points; per-location result entry is not available yet." [IncubationStepRecorder.cs:237-238; WaterWorkflowEngine.cs:178-181]
- BR-COUNT-016 (REQUIRES CONFIRMATION): Plate text other than TNTC / Uncountable that is not a number: the numeric branch calls `decimal.Parse` with no validation message; negative numbers are accepted. The controller refuses an empty list with "Plate readings are required." and the recorder with "At least one plate reading is required to calculate an average." (GAP-209, GAP-228).
- BR-COUNT-017 (DISCREPANCY, confirmed): INTENDED (spec quote, BusinessRules.md §5): "OOT (Out Of Trend) rule: (Step1Count + Step2Count) > ActionLimit. This is computed automatically on Step 2 completion and persisted to RoomMonitoring.IsOutOfTrend"; WorkflowDiagrams.md EM flow: "Step1Count + Step2Count > ActionLimit? Yes: Out Of Trend". Code: the batch path compares each location's average with Alert / Action / Spec; `IsOutOfTrend` is declared on RoomMonitoring and the EM result DTO but no application code assigns it. The number of incubation windows comes from the Test Master template, not a fixed two steps. Spec outdated or rule dropped (GAP-211). [src: documents/DesignSpecification/BusinessRules.md:L102-L118; Domain/Entities/RoomMonitoring.cs:L15; Application/DTOs/EMResultDto.cs:L10]
- BR-COUNT-018 (INTENDED, matches code): BusinessRules.md §4: water readings are averaged and compared Specification, then Action, then Alert; the first limit exceeded wins; statuses WithinLimits, AlertLimitExceeded, ActionLimitExceeded, OutOfSpecification; at least one reading required. Code matches (BR-COUNT-009, BR-COUNT-013). [src: BusinessRules.md:L83-L100]

### MOD-OBS-001 Observation and location-grid tests
- BR-OBS-001 (IMPLEMENTED, verified): Batch EM/AC pathogen (`batch-pathogen-results`): the controller refuses a location without an answer ("A growth observation is required for every location."); the recorder refuses an empty list ("Step \"{step}\" requires a growth observation for every location.") and missing locations ("Results are missing for: {names}."). Location status = Detected if growth was observed, else Absent; overall Detected if any location is Detected. Summary "{n} locations: {absent} absent, {detected} detected"; Result reads "{summary} (overall: {Detected|Absent})". Allowed only at the final step window (`IsFinalStep`) after its minimum duration. [TestWorkflowController.cs:431-442; IncubationStepRecorder.cs:658-723]
- BR-OBS-002 (IMPLEMENTED, verified): Batch submission (count, water, pathogen) is accepted only at the final incubation window: "\"{step}\" is not the final incubation window yet - close it and start the next window first." A non-final window is closed with `close-incubation-window` (minimum duration applies; "This is the final incubation window - record results instead of advancing." on the last one; "This step requires incubation transfer - start stage 2 incubation instead of advancing window." for a transfer step) and the next window is started with a new media selection. [IncubationStepRecorder.cs:408-425, 456-457, 567-568, 670-671]
- BR-OBS-003 (IMPLEMENTED in code, unreachable from the API, corrected): `RecordObservationAsync` (Detected / Absent on a final step, "Growth" / "No Growth" otherwise) can only run for a PlateCount step of a non-count workflow with an observation payload. The `record-result` controller builds a count payload only (an empty list gives "Plate readings are required."), so no HTTP call reaches it. Do not describe it to users as a screen behaviour. [IncubationStepRecorder.cs:285-289, 973-985; TestWorkflowController.cs:175-190]
- BR-OBS-004 (IMPLEMENTED, client; server verified for the qualitative recorder): Qualitative (physicochemical) result needs at least one parameter row (server: "At least one parameter result is required."), analysis time not later than 5 minutes ahead (server: "Analysis time cannot be in the future."), an observation when a row does not comply (server: "An observation is required when result does not conform."), observation at most 500 characters (server: "Observation cannot exceed 500 characters."), the password ("Password is required to sign the result."), every specification of the test supplied exactly once, specification limit type Qualitative or PresenceAbsence, and only one active entry per test order ("An active {name} entry already exists for this test order."). Optional equipment must be in service and in the FP section. The result is signed (meaning ResultRecorded), stored as a TestAnalysis with per-parameter "Complies" (WithinLimits) or "Does not comply" (OutOfSpecification), the order moves to Ready and the section is auto-submitted for review when all its tests are Ready. [src: SingleResultRecorder.cs:242-327; TestWorkflowSupport.cs:552-756; QualitativePanel.tsx:186-200]

### MOD-PATH-001 Pathogen testing
- BR-PATH-001 (IMPLEMENTED, verified): Step types: PlateCount, BrothEnrichment, SelectiveBroth, SelectivePlating, ConfirmatoryPlating, BiochemicalTest. [src: backend/MicroLIMS.Domain/Enums/StepType.cs]
- BR-PATH-002 (IMPLEMENTED, verified): Selective plating observation: only GrowthConforming unlocks the next step; NoGrowth or GrowthNonConforming finalizes the test immediately as NotDetected (Result written, order to Ready, sample section auto-submitted when all its tests are Ready). The observation records an expected-appearance snapshot taken at that moment. [PathogenConfirmationRecorder.cs:344-380, 872-898]. See GAP-205.
- BR-PATH-003 (IMPLEMENTED, verified): Selective plating start requires the step to be next in order, the sample prepared, the lot released and from the same section's material, on the step's permitted medium ("Media lot {lot} is not a lot of the permitted medium for this step."), a configured window and an incubator in range; the optional start time is taken from the request (GAP-224). An observation requires an open incubation ("No active incubation found for step \"{step}\". Start incubation before recording observation.") and the minimum duration (BR-TESTING-006). A step with no target organism: "Step \"{step}\" has no target organism configured - complete this step's template in Test Master before recording results." [PathogenConfirmationRecorder.cs:238-337, 72-75]
- BR-PATH-004 (IMPLEMENTED, verified): Confirmatory setup (`submit-confirmatory-setup`): at least one medium ("At least one confirmatory medium must be selected."); each medium on the step's permitted list ("That medium is not on this step's permitted list."), with a lot and incubator ("Every selected medium needs a lot and an incubator."), a released lot and an in-range incubator. One shared incubation row is created: start = analyst-supplied start, end = start + the largest medium maximum (the client-supplied end is overwritten); the declared window must reach the largest medium minimum ("The incubation window ends before it starts (...)" / "Step \"{step}\" requires at least {h} hours of incubation - the declared window is {n} hours."). A second setup is refused: "Confirmatory plating for step \"{step}\" has already been read out and cannot be set up again." (after read-out) or "Confirmatory media have already been selected for step \"{step}\" - awaiting their plate readings. The submitted panel and its incubation are shown on this test order's current step." [PathogenConfirmationRecorder.cs:386-518]
- BR-PATH-005 (IMPLEMENTED, verified): Confirmatory observations: exactly one observation per selected medium ("Exactly one observation is required for each selected medium." / "...one was submitted more than once."); setup must exist ("This step's media selection has not been submitted yet."); the declared window END must have passed ("This step's incubation period has not finished yet.") unless Skip Wait was used. [PathogenConfirmationRecorder.cs:524-566]
- BR-PATH-006 (IMPLEMENTED, corrected): Confirmatory outcome of a run: all observations GrowthConforming -> ConfirmatoryResult.AllConforming (analyst decision required, no flag); any other mix -> Inconclusive (flag "InconclusiveResult"; WorkflowHistory note "Confirmatory plating inconclusive - flagged for investigation."). Correction: Inconclusive is NOT a dead end. It does not offer the analyst decision, but a biochemical test is allowed after an Inconclusive confirmatory result (BR-PATH-008), and a positive biochemical result gives a final Detected. [PathogenConfirmationRecorder.cs:580-599, 712-723, 760]
- BR-PATH-007 (IMPLEMENTED, verified): Analyst decision after AllConforming ("An analyst decision is only available after an all-conforming confirmatory result."; "Confirmatory plating has not been completed for this test order."), once only ("An analyst decision ({decision}) was already recorded for this confirmatory result."). Two choices: "Proceed to biochemical" (history note "Analyst decision: proceed to biochemical confirmation.", no state change) or "Conclude as Detected" (note "Analyst decision: submitted as Detected without biochemical confirmation.", final result Detected, flag BiochemicalNotPerformed, result row written). No signature, no password. [PathogenConfirmationRecorder.cs:608-667]
- BR-PATH-008 (IMPLEMENTED, verified): Biochemical submission requires result text ("A biochemical result is required."), a preceding SelectivePlating or ConfirmatoryPlating step in the template, that step completed, and a conforming preceding result: after a confirmatory step AllConforming or Inconclusive; after a selective step GrowthConforming ("A biochemical test is only available after a conforming result on \"{step}\"."). Final result Detected if "organism detected" is set, else NotDetected; only the last biochemical step of a chain finalizes. Attachment optional. [PathogenConfirmationRecorder.cs:675-765]
- BR-PATH-009 (IMPLEMENTED, verified; no UI caller): Biochemical decision by a reviewer (policy TestWorkflow.BiochemicalDecision, section access): refused if the reviewer performed the test ("A reviewer cannot decide on a result they performed."), if the row is not a confirmatory result submitted as Detected without biochemical confirmation, or if returning without a reason ("A reason is required when returning a result for biochemical confirmation."). Approve clears the flag and logs ReviewCompleted/Approve; return moves the order Ready -> Incubating, notifies the assigned analyst and re-projects the result. No signature. [PathogenConfirmationRecorder.cs:774-866; TestWorkflowController.cs:527-539]
- BR-PATH-010 (DISCREPANCY, now located): Agreement across confirmatory media per location in the session workspace. Server rule (ConfirmationAgreementEvaluator, applied when all required media are read): all conforming -> Detected ("Detected (+)"); all NoGrowth or all GrowthNonConforming (a single distinct value) -> NotDetected ("Not Detected (-)", status Absent); any other mix -> Inconclusive ("Inconclusive (Retest)"). Client rule (BatchConfirmatoryPlatingPanel.tsx:315-326): all conforming -> Detected; NoGrowth plus NonConforming combined also -> Not Detected; otherwise Inconclusive. They differ when one medium shows NoGrowth and another shows NonConforming growth: the screen says Not Detected, the server stores Inconclusive. The per-run ConfirmatoryResult (BR-PATH-006) belongs to the single-test step chain. GAP-206 (resolved), GAP-223. [src: Services/ConfirmationAgreementEvaluator.cs:13-46; PathogenSessionService.cs:1099-1150]
- BR-PATH-011 (IMPLEMENTED, verified, extended): Session completion refused while required Location x Test results are missing: "Cannot complete testing session: {n} required Location × Test result(s) are missing ({first 5 as Location (Test)}{, and k more})." and while any assigned test is TSB_INCUBATING or not result-entry-allowed: "Cannot complete testing session: Assigned tests {codes} have incomplete workflow prerequisites." On success: EVERY test order of the sample (not only pathogen tests) is set to step Ready / Status ResultEntered, the sample Status is set directly to UnderReview (bypassing the per-section roll-up and the "all tests Ready" check) and ReviewedAt cleared; ReviewEventLog "Testing session completed - submitted for review". Needs only TestWorkflow.Execute and section access; no password. GAP-222. [PathogenSessionService.cs:1209-1265; PathogenSessionController.cs:150-168]
- BR-PATH-012 (IMPLEMENTED, verified, corrected): Session reset (policy TestWorkflow.Supervise; reason optional, server default "Analyst requested session workflow reset" when blank, the dialog sends "Section Head requested session reset"). Refused for Voided ("Sample #{ref} has been voided and its testing session cannot be reset.") and Rejected ("Sample #{ref} has been rejected and its testing session cannot be reset. A rejection is reversed through the approval workflow by a Section Head."). Otherwise it PERMANENTLY DELETES, for every test order of the sample: location pathogen observations, confirmatory plate observations and media selections, workflow step results, pathogen observations, count readings, Results and Incubations; clears location CFU results; sets every order to Waiting / Pending; sets the sample to Received only when it was InTesting or UnderReview (other statuses are left unchanged); writes an AuditLog entry "ResetWorkflowSteps" and a workflow-history note per order with the reason. GAP-221. [PathogenSessionService.cs:1267-1403; PathogenSessionController.cs:169-180]
- BR-PATH-013 (IMPLEMENTED, verified): Primary observations and matrix entries refused while TSB or prerequisite steps incomplete: "Cannot enter primary observations for {code}: TSB incubation or required workflow steps are still in progress." / "Cannot enter results for {code}: TSB incubation or required workflow steps are still in progress ({state})." Primary observation results: NoGrowth -> "Not Detected (-)" (Absent); GrowthConforming -> "Growth Conforming (Presumptive +)" and GrowthNonConforming -> "Growth Non-Conforming", both PendingConfirmation. Quantitative matrix cells are compared with the SamplingConfiguration limits (Spec > Action > Alert). [PathogenSessionService.cs:733-800, 1139-1203]
- BR-PATH-014 (IMPLEMENTED, verified): Confirmatory media count: `ConfirmatoryMediaCount` from the step if set, else 2 when the test code contains "SALMONELLA" (case-insensitive), else 1. Exact count enforced: "Pathogen {code} requires exactly {n} confirmatory media; received {m}." Locations must have a conforming or non-conforming primary observation: "Location #{id} does not have a GrowthConforming or GrowthNonConforming primary observation and cannot proceed to confirmation." The test must have a confirmatory step: "Test \"{code}\" has no confirmatory plating step in Test Master." Media must be on the step's list ("Material #{id} is not on the permitted media list of step \"{step}\".") and from the test's section; the incubator must suit every medium ("Incubator {code}'s set point is outside the {range} range of \"{medium}\"."); the window runs to the longest medium maximum. The session path does not check that the lots are released (GAP-220). [PathogenSessionService.cs:853-998, 877-883]
- BR-PATH-015 (IMPLEMENTED, verified): Shared TSB start (session) requires the sample prepared (refusal audited as TestStartRefused), an unexpired lot ("Media lot #{lot} expired on {date}."), an Incubator-type equipment, at least one assigned test with a TSB step ("None of the assigned tests on this sample require TSB enrichment."), a test whose TSB medium accepts the lot ("Media lot #{lot} ({name}) is not the TSB medium of any test on this sample."), and for each joining test a configured window and an in-range incubator ("The shared TSB can't start: incubator {code}'s set point is outside the {range} range of \"{medium}\" on test {code}."). Existing TSB incubations of the joining orders are deleted and replaced; orders go to Incubating. The session path does not check that the lot is released (GAP-220). [PathogenSessionService.cs:451-677]
- BR-PATH-016 (DISCREPANCY, spec vs code, confirmed): BusinessRules.md §3 / WorkflowDiagrams.md describe a PathogenWorkflowEngine (TSB -> Observation -> Continue -> Detection Media; Salmonella TSB -> RVS -> XLD+TSI; negative closes early) citing `Application/Workflows/PathogenWorkflowEngine.cs`. No such class exists; code comments state the generic `TestWorkflowEngine` replaced it ("Generic step-runner replacing PathogenWorkflowEngine and ..."), and the chain is whatever the Test Master template defines using the step types in BR-PATH-001. Spec outdated. (GAP-205) [src: TestWorkflowEngine.cs:17; documents/DesignSpecification/BusinessRules.md:L52-L81]
- BR-PATH-017 (REQUIRES CONFIRMATION): The single-test step chain (WF-PATH-001) and the session workspace (WF-PATH-002) both exist for the same tests; the code read does not state when users are meant to use which, or how the session workspace's matrix results relate to the per-order step chain. [src: SelectedSampleTestingPanel.tsx:401-417 ("Open Pathogen Workflow" only when a test uses shared TSB)]

### MOD-TRACK-001
- BR-TRACK-001 (IMPLEMENTED, verified): The Tracking Board lists every sample that has at least one test order (superseded orders included, so a sample whose tests all moved to a retest stays visible), newest received first, with each laboratory's own stage (BR-SAMPLE-011 values). Not scoped to the caller's section. Samples without any test order (EM, Water and After Cleaning samples before preparation) do not appear (GAP-229). [SampleTrackingService.cs:24-139]
- BR-TRACK-002 (IMPLEMENTED, verified): The "Overall" filter is applied in memory over all candidates, then paged. Laboratory columns show a lab only when it has a test order or sign-off on the sample; others show "Not requested". [SampleTrackingService.cs:83-104, 129-134; TrackingBoardPage.tsx:64-75]
- BR-TRACK-003 (IMPLEMENTED, verified): Read-only list; no state change is made from this screen. The page imports the sample summary dialog (C3). [TrackingBoardPage.tsx:25]

---

## validations

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

## statuses

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

## terminology

| Term (UI) | Plain meaning (as coded) | src |
|---|---|---|
| Sample / Reference | Received unit; reference number shown as ref | SampleRegisterTable |
| Test Order (TO #) | One order per test code per sample | ProductWorkflowEngine.cs:124 |
| Control No. / Batch No. | Batch or control identifiers (Product/RM/PM require both at receipt) | ReceiveSampleValidator.cs:15-16 |
| Cause of Testing | Reason the sample was submitted (master list) | ReceiveSampleValidator.cs:14 |
| Sampled By | Person who sampled | ReceiveSampleValidator.cs:17 |
| Assigned analyst / Unassigned | Analyst assigned to all open test orders; "Unassigned" = open for any authorised analyst | SampleAssignmentService.cs:28-70; AssignAnalystDialog.tsx:191-199 |
| Preparation / Needs Preparation | Diluent/neutraliser (product) or room/part/point grid (EM/AC/Water) confirmed before testing | SamplePreparationService.cs |
| Shared TSB | One broth tube per sample applied to all pathogen tests that use TSB | IncubationStepRecorder.cs (shared TSB); PathogenSessionService.cs:557-591 |
| Incubation window | Minimum (Ready from) and maximum (Read by) hours and temperature range from the step medium | IncubationWindowResolver.cs:12-15 |
| Skip Wait | Supervisor (TestWorkflow.Supervise) bypass of the minimum-duration and window-end checks for the open incubation; no reason or password; window unchanged | IncubationStepRecorder.cs:431-443 |
| Stage 1 / Stage 2 | Two-stage count: plates moved to a second incubator after stage 1 window | IncubationStepRecorder.cs:163-200 |
| Dilution Factor (DF) | Multiplier applied to average plate count; direct-count types fixed at 1 | IncubationStepRecorder.cs:780-783, 877-878 |
| Direct count | Water, EM, After Cleaning (DF = 1) | IncubationStepRecorder.cs:780 |
| Plate reading | Colony count entered per plate; or "TNTC" / "Uncountable" | TestWorkflowDialog.tsx:742-768 |
| TNTC | "Too numerous to count" text entry; flags RequiresReview, no average | IncubationStepRecorder.cs:828-852 |
| Uncountable | Same as TNTC | IncubationStepRecorder.cs:844 |
| Calculated result | Average of readings x DF | IncubationStepRecorder.cs:877-878 |
| Reported result | Display value (rounded whole number, or "<DF unit") | IncubationStepRecorder.cs:906-909 |
| Alert / Action / Spec | Limits compared in order Spec, Action, Alert | SpecLimitParser.cs:208-226 |
| Growth Conforming (+) | Growth matching expected appearance for the target organism | GrowthObservation.cs; SelectivePlatingPanel.tsx:16-20 |
| Growth Non-Conforming | Growth not matching expected appearance (ends selective-plating test as NotDetected) | PathogenConfirmationRecorder.cs:370-383 |
| Confirmatory plating | Re-plating on one or more permitted confirmatory media, one observation per medium | PathogenConfirmationRecorder.cs:434-600 |
| Inconclusive (Retest) | Confirmatory media disagree; flagged for investigation/retest | PathogenConfirmationRecorder.cs:581-599 |
| Biochemical test | Identification step; result text + organism detected yes/no | PathogenConfirmationRecorder.cs:676-760 |
| Primary Matrix (Panel A) / Panel B | Location x Test grid of primary plate readings / confirmatory batch | PrimaryObservationMatrixPanel.tsx; BatchConfirmatoryPlatingPanel.tsx |
| Flagged location | Location with a non-"No Growth" primary observation needing confirmation | BatchConfirmatoryPlatingPanel.tsx:455-471 |
| Grouped actions | Actions applied to 2+ checked samples at once | GroupedActionsPanel.tsx:35-363 |
| Void | Strike a sample from the register (signed); not the same as reject | SampleCorrectionService.cs:253-303 |
| Correct / Edit details | Signed correction of sample fields until submitted for review | SampleCorrectionService.cs:62-67 |

---

## reports

C2-related printouts and exports only (report and CoA belong to C3).
- CSV export "receiving-testing-workspace-{date}.csv" from the workspace (columns in SCR-TESTING-001). [src: ReceivingTestingWorkspacePage.tsx:81-103, 840]
- Sample summary report and CoA are C3 (`/samples/:id/report`, `/samples/:id/coa`).
- Count result display in the sample summary: labels "Plate Readings", "Dilution Factor", "Average", "Calculated", "Reported Result", "Alert / Action / Spec", "Status", "Entered By", "Entered At". [src: frontend/src/modules/testingWorkspace/sampleSummary/FinalResultBlock.tsx:44-64]
- Pathogen wording on report: "No growth", "Growth observed - does not match target organism", "Growth observed - matches target organism". [src: frontend/src/modules/testingWorkspace/utils/pathogenObservationLabel.ts:8-19]
- Biochemical card wording: "Detected" / "Absent" / "Undetermined". [src: TestResultCards.tsx:468]

---

## audit-trail

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

## traceability

As the UI exposes it (C2 scope).
- Sample (SMP ref) -> Test orders (one per test code; test order id "#{testOrderId}") -> Incubation(s) per step (media lot, incubator, stage 1/2, start/ready/read times) -> Count reading or location results or pathogen observation -> Result (interpreted "{reported} ({status})").
- Sample -> Assigned analyst (per test order, shown as "ASSIGNED TO").
- Sample -> Item documents: SOP and Verification Report (product-like). [SelectedSampleTestingPanel.tsx:385-387]
- Sample -> Pathogen session (sessionId) -> Shared TSB incubation (one per sample; media lot and incubator) -> Primary matrix (location x test) -> Confirmatory setup (media lots by MaterialId, incubator) -> Confirmatory readings -> Final Analytical Results Summary -> Evidence chain. [PathogenSessionDialog.tsx; SessionReviewPanel.tsx:85-141]
- Media lot -> step medium (Test Master) -> incubator equipment (setpoint range). Equipment calibration status "lapsed" excluded from grouped actions. [GroupedActionRow.tsx:74-92]
- Sample -> audit history dialog ("Audit History") and "Sample Summary" (C3).
- Sample preparation -> Test Master preparation configuration (item) and sampling configuration (water point, EM room/part configs). [PreparationDialog.tsx; EMPreparationForm.tsx:31-40]

---

## gaps

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
