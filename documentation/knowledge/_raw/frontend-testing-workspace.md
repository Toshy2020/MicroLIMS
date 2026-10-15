# Frontend testing workspace - panel, step and dialog inventory (facts only)

Scope: frontend/src/modules/testingWorkspace (non-test .tsx), frontend/src/modules/testPreparation, frontend/src/modules/receivingTesting, plus the laboratoryConfiguration preparation forms and shared components that the workspace renders (SignatureDialog, FloatingDialog, ConfirmationDialog, ReturnToAnalystDialog). Route-level pages were already covered in documentation/knowledge/_raw/frontend-core-screens.md sections 4-5; this file does not repeat those except where a panel needs them.

Method: source read directly for the workflow dialog, the pathogen step panels, the pathogen session panels, the sample summary and approval card, the preparation forms, and the workspace page. Physicochemical panels (Measurement, Gravimetric, Qualitative, Titration, Dissolution, Disintegration, Weight Variation) were read by pattern search for labels, conditions and buttons, not full read. Every fact is tagged with [src: path:Lnnn] (repo-relative). UNCLEAR marks anything not confirmed in source.

Legend: "Signature" = the shared SignatureDialog (password re-entry) is rendered by that action. "No signature" = the action calls the service with no password and renders no SignatureDialog.

---

## 1. Workspace shell and queues

### 1.1 ReceivingTestingWorkspacePage (lab workspace shell)
- Component `ReceivingTestingWorkspacePage({ lab })` at frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:142 [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:142].
- Page title: "RM & PM Workspace" (area rmpm), "FP Workspace" (FP), otherwise "{lab.name} Workspace" [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:744-749].
- Subtitle "Manage incoming samples, assignments, testing progress, review, and laboratory workflow execution from one workspace." [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:751].
- Header buttons: "Refresh" (outlined, disabled while loading) [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:755-770]; "+ New Sample" (contained; opens NewSampleDialog) [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:772-784].
- Allowed categories per lab: MICRO = product, rm, pm, water, em, ac; FP area = product; RM&PM area = rm, pm [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:114-122].
- Display modes from URL `view` or toolbar: "table" (default), "card", "kanban" [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:61, 163-166, 980-1020].
- Layout branches (in render order):
  - 2 or more checked samples -> split pane: compact register (left, 38% width on md) + GroupedActionsPanel (right) [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:861-917].
  - one selected sample -> split pane: compact register + SelectedSampleTestingPanel [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:918-954].
  - none -> full-width register with SampleRegisterTable / SampleCardView / SampleKanbanView [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:955-1050].
- Compact register title "Laboratory Register (N of M)"; header note "Click a sample to switch" (single selection) or "{n} checked" plus " · {h} hidden" (grouped) [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:657-664, 873-878, 930].
- Compact table headers: "Item / Reference", "Type", "Batch/Ctrl", "Status" [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:682-685]; empty text "No matching samples found." [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:706].
- Pagination options: compact register [25, 50, 100] [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:721]; full-width register [25, 50, 100, 200] [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:971, 1040]. Default page size 50, capped at 200 [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:154-157].
- Full-width heading "Laboratory Register (N samples)" [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:959-961].
- Grouped-action toast texts: "{n} test(s) started in incubation." with " ({k} skipped)" when skipped (severity warning) [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:901-906]; "{n} sample(s) prepared and signed." [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:908-914].
- Receive success text "Successfully received N sample(s). Test orders generated automatically." [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:629-634].
- Void success text "Sample #{id} ({name}) has been marked as Voided." [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:1137-1143].
- Deep-link toast on failure "Sample #{id} could not be opened. It may not belong to this laboratory, or the server could not be reached." [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:136-140].
- Export: "Export" in the filter bar produces CSV named "receiving-testing-workspace-{date}.csv" with columns Sample ID, Reference, Item / Display Name, Category, Batch No, Control No, Cause of Testing, Sampled By, Received At, Status [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:81-103, 840].
- Deep-link workload chip "Active Workload Filter:" with labels "Retests in Progress", "Overdue Review (>24h)", "Overdue Approval (>24h)" (shown only for those three filters) [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:797-820].
- Whitelisted URL workloads: needsPreparation, readyToRead, awaitingReview, overdue, mine, unassigned, retestInProgress, reviewOverdue, approvalOverdue [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:65-75].
- Dialog ecosystem mounted by the page: NewSampleDialog, EditSampleDetailsDialog, AssignAnalystDialog, SampleSummaryDialog, TestWorkflowDialogRouter, PreparationDialog, AuditHistoryDialog, VoidSampleConfirmationDialog [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:1055-1145].
- Permission for "Assign analyst" tile: PERMISSIONS.SAMPLES_ASSIGN_ANALYST [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:793].
- Endpoints used by the page: see section 9.

### 1.2 KPI tiles (SampleStatusKpiCards, rendered by the shell)
- Tile labels with hints [src: frontend/src/modules/receiving/components/SampleStatusKpiCards.tsx:54-90]:
  - "Needs Preparation" - hint "Preparation not yet signed off" (key needsPreparation).
  - "Ready to Read" - hint "Incubation complete, awaiting result entry" (readyToRead).
  - "Awaiting Review" - hint "All tests complete, awaiting reviewer" (awaitingReview).
  - "Overdue" - hint "Open more than 24 hours after receipt" (overdue).
  - "Assigned to Me" - hint "Samples with a test assigned to you" (mine).
  - "Unassigned" - hint "No analyst assigned yet" (unassigned).
- Tile click toggles the workload filter (clicking the active tile clears it) [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:476-479].
- Tile file is in the receiving module (out of scope); only labels were read.

### 1.3 Register views
- Table (SampleRegisterTable) - receiving module, not read [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:981-1000].
- Card view (SampleCardView) - each card shows displayName, referenceNumber, CategoryBadge; line "{causeOfTesting} · {received date}"; line "Batch: {b}" or "Control: {c}" plus " · Tests: {n}"; footer = first assigned analyst name or "Unassigned", plus SampleLifecycleBadge [src: frontend/src/modules/testingWorkspace/SampleCardView.tsx:84-124].
- Kanban (SampleKanbanView) - read-only, grouped by sample.status, no drag-and-drop. Columns and labels: Received; Under Testing (InTesting); Under Review (UnderReview); Under Approval (UnderApproval); Approved; Rejected; Retest Requested (RetestRequested) [src: frontend/src/modules/testingWorkspace/SampleKanbanView.tsx:6-14]. Empty column shows an em dash [src: frontend/src/modules/testingWorkspace/SampleKanbanView.tsx:60]. Card click selects the sample in the page (not the summary dialog) [src: frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx:1013-1019]. Source comment says card click "opens the same Sample Summary dialog" - this contradicts the page wiring at line 1016-1018 which only calls setSelectedSampleId [src: frontend/src/modules/testingWorkspace/SampleKanbanView.tsx:18-19].
- Compact row (SampleTableRow, isCompact) - checkbox aria-label "Select {name} ({ref}) for grouped actions"; row background purple when selected; left border inconclusive colour when NeedsPreparation [src: frontend/src/modules/testingWorkspace/SampleTableRow.tsx:93-109, 74-88].
- Full row columns (non-compact): expand chevron with aria "Show details for {name}" / "Hide details for {name}" (only when details exist); columns category, batch ("B: {batch}" or "C: {control}" in compact), control, cause, receivedAt, assignedTo ("Unassigned", single name, or "Name +N"), status [src: frontend/src/modules/testingWorkspace/SampleTableRow.tsx:33-60, 201-237].
- Expanded detail fields: "Production Stage", "Sample Quantity", "Sampled By", "Mfg Date", "Exp Date", "Sampling Point", "Storage Condition" ("Refrigerator (Nh)") [src: frontend/src/modules/testingWorkspace/SampleTableRow.tsx:240-266].

### 1.4 SampleLifecycleBadge
- Labels: "Under Testing" (InTesting), "Under Review", "Under Approval", "Approved", "Rejected" [src: frontend/src/modules/testingWorkspace/SampleLifecycleBadge.tsx:6-12]. No label for Received, so it renders nothing for that status [src: frontend/src/modules/testingWorkspace/SampleLifecycleBadge.tsx:52].
- Clickable (opens SampleSummaryDialog) when: UnderReview requires PERMISSIONS.SAMPLES_REVIEW; UnderApproval requires PERMISSIONS.SAMPLES_APPROVE; Approved always clickable; InTesting and Rejected are not clickable [src: frontend/src/modules/testingWorkspace/SampleLifecycleBadge.tsx:27-33, 56].
- Approved shows a print icon [src: frontend/src/modules/testingWorkspace/SampleLifecycleBadge.tsx:92].

### 1.5 SelectedSampleTestingPanel (single sample detail)
- Header buttons, in order [src: frontend/src/modules/testingWorkspace/SelectedSampleTestingPanel.tsx:104-242]:
  - "Sample Summary" (always) -> onLifecycleBadgeClick (opens summary).
  - "Audit History" (always).
  - "Edit Details" - shown only when canManageSample (PERMISSIONS.SAMPLES_CORRECT) and onEdit is given [src: :148]; disabled when `!sample.canEditDetails`; tooltip "Correct sample details (signed)" or "Locked: the sample has been submitted for review" [src: :154-155].
  - "Void Sample" - shown when canManageSample and sample.status !== "Voided" [src: :174-196].
  - "Prepare Sample" - shown when preparationStatus === "NeedsPreparation" [src: :199-219].
  - "Deselect" [src: :241].
- Permission note in code: "Analysts cannot correct or void (the backend enforces it)" [src: frontend/src/modules/testingWorkspace/SelectedSampleTestingPanel.tsx:62].
- Metadata grid labels: "ASSIGNED TO", "PREP STATUS", "CAUSE OF TESTING", "BATCH NUMBER", "CONTROL NUMBER", "RECEIVED AT", "SAMPLED BY", "SAMPLE QTY", category-specific ("STAGE" for FinishedProduct, "SAMPLING POINT" for Water, "PREV PRODUCT" for AfterCleaning, else "MFG DATE"), and "STORAGE" / "EXP DATE" / "CATEGORY" [src: frontend/src/modules/testingWorkspace/SelectedSampleTestingPanel.tsx:258-381].
- Product-like samples show ItemDocumentsCard (SOP and Verification Report) [src: frontend/src/modules/testingWorkspace/SelectedSampleTestingPanel.tsx:385-387; frontend/src/modules/testingWorkspace/ItemDocumentsCard.tsx:85, 93].
- Section title "Assigned Tests (N)" with subtitle "Test Master configured laboratory workflows" [src: frontend/src/modules/testingWorkspace/SelectedSampleTestingPanel.tsx:393-397].
- "Open Pathogen Workflow" button - shown only when some assigned test has usesSharedTsb [src: :401-417]; opens PathogenSessionDialog [src: :488-497].
- Needs-preparation banner [src: :420-466]: title "Sample Needs Preparation"; body "Record the sample preparation and sign for it before starting laboratory tests." (product-like) or "Test locations and configuration must be completed before starting laboratory tests." (others); button "Start Preparation".
- Empty state "No tests assigned to this sample." [src: :468-471].

### 1.6 AssignedTestCard (one test row in the selected sample)
- Title "{testCode}{ (N rooms|parts)}" and order id "#{testOrderId}" [src: frontend/src/modules/testingWorkspace/components/AssignedTestCard.tsx:125-126, 462-467].
- Preparation gate: `awaitingPreparation = sample.preparationStatus === "NeedsPreparation" && !test.skipsPreparation` [src: :69]. When true the "Open Workflow" button is disabled and its tooltip reads "Complete and sign the sample preparation before starting laboratory testing." [src: :488-502]. Quick-setup chevron is hidden [src: :523].
- Closed-state labels (win over all other rules) [src: :46-52]: "Rejected" (text "✗ Rejected"), "Voided" ("Voided: struck from the record"), "Cancelled", "Superseded" ("Superseded: retested on a new sample"), "On Hold" ("On hold, awaiting retest outcome").
- Badge logic (dynamicBadge) [src: :161-246]: "Window Not Configured" (Blocked) when workflowState WINDOW_NOT_CONFIGURED; "Approved"; "Reviewed, Pending Approval"; "Pending Review"; "Enter Result" / "Ready: Plating" when incubation has ended; "Incubating"; step-aware "Ready: Broth", "Ready: Plating", "Ready: Setup", "Ready: Confirmatory", "Enter Result".
- Centre text (centerText) [src: :261-379]: "Blocked: incubation window not configured in Test Master"; "✓ Approved & Complete"; "✓ Reviewed, Pending Approval"; "Result Recorded, Pending Review"; countdown text "⏳ {media} · {range} · {h}h {m}m remaining ({incubator})"; "Incubation Complete, Ready for Downstream Setup"; "Incubation In Progress"; "Ready for {stepName}"; "Next: {stepName} · {media} · {temp} · ({h}h)"; "Ready for Colony Count Readings (CFU)"; "Ready for Growth Observation (...)"; "Ready for Confirmatory Readings"; "Ready for Biochemical Test Result"; "Ready for Result Entry".
- Duration format: "{h}h {m}m remaining", "{m}m {s}s remaining", "{s}s remaining", "Ready to read" when zero [src: :30-42].
- Quick setup drawer (chevron only when requiresMediaSetup and not incubating and not approved and not awaiting preparation) [src: :523, 578-669]: selects "Select Media Lot ({permitted})", "No released lots available", "Incubator ({min}-{max}°C)", "No matching incubator"; button "Starting..." / ctaLabel; disabled when !selectedMediaId || !selectedIncubatorId || submitting [src: :656]. Otherwise drawer text "Step requires full workflow interaction." with link "Open Workflow →" [src: :671-684].
- Optimistic update: on click, incubating state is set before the server answers; reverted if refused [src: :390-430].

### 1.7 Grouped actions (2 or more samples checked)
- GroupedActionsPanel [src: frontend/src/modules/testingWorkspace/components/GroupedActionsPanel.tsx:35-363]:
  - Header "Grouped Actions", subtitle "{n} samples selected across workspace", buttons "Deselect All" and an icon with aria "Clear selection" [src: :154-202].
  - Preparation section "Test Preparation Required (N)" with note "Must complete before incubation" [src: :93-100]; excluded line "{n} sample(s) cannot be prepared as a group" [src: :111].
  - If preparation blocks all actions: "Incubation Setup Locked" - "No incubation can start on a sample whose Test Preparation is still outstanding. Confirm the preparation above and the compatible setup actions appear here." [src: :237-243].
  - If no groups and some tests excluded: "Individual Result Entry Required" - "No grouped workflow action is available. The selected tests require individual result entry. Result entry must be completed separately for each test." [src: :262-265]; chips list heading "Tests Awaiting Individual Result Entry (N):" [src: :270]; chip label "{ref} · {name} ({step})" opens the individual workflow [src: :273-287].
  - If no groups at all: "No Shared Setup Actions Available" [src: :309-313].
  - Loading text "Calculating compatible workflow steps..." [src: :221].
  - Warning when some tests excluded: "{n} test(s) excluded from grouped actions" [src: :332-335].
  - Section header "Compatible Actions (N)" with note "Shared operational workflow transition" [src: :341-346].
  - Grouped fetch enabled only when selectedSampleIds.length >= 2 [src: :54, 68].
- GroupedActionRow [src: frontend/src/modules/testingWorkspace/components/GroupedActionRow.tsx:32-449]:
  - Collapsed label: transitionLabel or stepName; chip "× N Sample(s)"; status "Ready: Transfer" (TRANSFER_INCUBATOR), "Ready: Plating" (TRANSFER_SELECTIVE), otherwise "Ready: Setup" [src: :203-236].
  - Media lots filtered to permitted material ids and not expired [src: :74-82]; incubators filtered to setpoint within group tempMin-tempMax and excluding calibrationStatus "lapsed" [src: :85-92].
  - Auto-selects first matching lot and first matching incubator [src: :95-105].
  - Expanded drawer: "Selected Samples (N Tests):" chips; "Media Lot ({permitted or 'Approved Media'})"; "Transfer to Incubator" or "Incubator" with range; no-lot text "No released, active lots available"; "No eligible incubator in range" [src: :284-398].
  - Buttons: "Cancel"; submit label "Start Incubation (N Tests)", "Transfer & Start Incubation (N Tests)" (TRANSFER_SELECTIVE), "Transfer to Incubator (N Tests)" (TRANSFER_INCUBATOR), "Processing..." while submitting [src: :419-441]. Disabled when submitting || !selectedIncubatorId || (not incubator-only && !selectedMediaId) [src: :422].
  - Batch refusal handling: the endpoint returns 200 with per-order skipped rows; if all refused, panel stays open with "No tests were started. {reasons}"; partial: "{n} of {m} started. {k} refused - {reasons}" [src: :141-158]. Source comment says this was added because a full refusal previously read as success [src: :133-140].
  - Error fallback text "Failed to execute batch action." [src: :163].
- GroupedPreparationRow [src: frontend/src/modules/testPreparation/components/GroupedPreparationRow.tsx:32-221]: row text "Test Preparation · {itemName}", chip "× N Sample(s)", chip "Approved" or "Pending Approval" [src: :95-111]; expanded "Samples to prepare (N):", info "This item's preparation configuration is still awaiting Section Head approval..." when PendingReview [src: :125-133]; button "Confirm & Start Testing (N Samples)" [src: :185-190]. **Signature**: SignatureDialog (meaning: "I confirm the preparation steps shown above are the steps performed for all N ... listed in this group (...).") [src: :209-214]; calls SamplePreparationService.confirmBatch [src: :44-48].

### 1.8 Test workflow dialog routing
- TestWorkflowDialogRouter [src: frontend/src/modules/testingWorkspace/FloatingDialogs.tsx:44-79]: title "{testCode} Workflow" with subtitle "{ref} · {sample name} · TO #{testOrderId}" [src: :50-62]; the TestWorkflowDialog is lazy-loaded [src: :10-23].
- Routing inside TestWorkflowDialog [src: frontend/src/modules/testingWorkspace/TestWorkflowDialog.tsx:304-427]: workflowType Measurement -> MeasurementPanel; Gravimetric -> GravimetricPanel; Titration -> TitrationPanel; Qualitative -> QualitativePanel; Dissolution -> DissolutionPanel; Disintegration -> DisintegrationPanel; WeightVariation -> WeightVariationPanel. Then, when workflowType is not CountTest and category is not EM/AfterCleaning, PathogenStepDialog is used [src: :425-427]. CountTest and EM/AC use the phases in TestWorkflowDialog itself.
- Loading state: skeleton, or error text "Could not load this test's workflow." [src: :88, 295-300].
- Returned for revision banner: "Returned for revision: {reason}" or "Returned by reviewer for revision" [src: :452-457].
- Step heading "Step {order}: {name}" with suffixes " (Two-stage transfer: Stage N of 2)" and " (determines the final result)" [src: :461-469].
- Final banner: "Final result: {outcome}" (red if Detected) and status chip "Result Recorded, Pending Review"; button "Done / Close" [src: :429-447].
- StepChainStrip (nav aria "Workflow progress"): headline "Step N of M", "All M steps complete", or "K of M steps complete"; per-step aria "{step}: Not started" / "In progress" / "Complete: {outcome}" [src: frontend/src/modules/testingWorkspace/components/StepChainStrip.tsx:26-86].

### 1.9 Shell-level time gates and signature triggers (summary; detail in each section)
- Preparation gate on Open Workflow (AssignedTestCard:501) and on incubation actions (GroupedActionsPanel:89, 224).
- Incubation window gate in TestWorkflowDialog (see section 2.6) and in pathogen panels (section 4.2-4.4).
- Signature-bearing actions in the workspace shell: none directly. Shell-level signed actions are in the dialogs listed in section 7.6.

---

## 2. Count tests (CountTest workflow type)

### 2.1 TestWorkflowDialog - CountTest phases
Phases (type `Phase`) [src: frontend/src/modules/testingWorkspace/TestWorkflowDialog.tsx:37]: "loading", "select-media", "awaiting-result", "transfer-stage-2", "enter-result", "step-complete", "all-complete".
- Initial phase after load [src: :86]: all steps complete -> all-complete; incubationLock present -> awaiting-result; otherwise select-media.
- select-media [src: :471-521]:
  - Helper "Requires {permitted names or 'an approved'} media." [src: :476].
  - Media lot select aria "Media batch"; empty option "Media Batch ({names} lots only)"; options "{lotNumber}, expires {date}" [src: :488-492, 490-491]. Filter: lot.materialId in step's stepMedia list [src: :112-113].
  - Incubator select aria "Stage 1 Incubator"; empty option "Stage 1 Incubator ({min}-{max} °C)"; options "{name} ({code}), {setPoint}°C" [src: :494-496]. Filter: setPointTemperature within the step's temperature range [src: :131-133].
  - Warning when none match: "No incubator is set to {min}-{max} °C for this step." [src: :498-500].
  - Shown after both chosen: "Required Temperature (Stage 1): {min}-{max} °C, Incubation Period: {h}-{h} hours" [src: :501-508].
  - Button "Start Incubation" disabled when !mediaId || !incubatorId [src: :512-518]. No signature. Endpoint select-media [src: frontend/src/modules/testingWorkspace/services/TestWorkflowService.ts:26].
- awaiting-result [src: :578-622]:
  - Info "Incubation in progress" (with "(Stage N of 2)" when two-stage) [src: :580-582].
  - Lines "Temperature: {range} °C", "Duration: {range} hours", "Expected reading: {datetime}" [src: :583-585].
  - Skip button "Skip Wait" [src: :598-601] (see 2.6).
  - Primary buttons [src: :603-619]:
    - Two-stage and not stage 2: "Transfer to Stage 2 Incubation" (disabled !isTimeReady) [src: :604-606].
    - Two-stage and stage 2: "Record Results" (batch) or "Record Result" [src: :607-612].
    - EM/AC and not final step: "Advance to Next Incubation Window" -> closeIncubationWindow [src: :613-614; endpoint TestWorkflowService.ts:189].
    - Batch (EM, AC, Water count): "Record Results" opens the location grid [src: :615-616].
    - Otherwise: "Record Result" -> enter-result phase [src: :617-618].
- transfer-stage-2 [src: :523-576]: text "Stage 1 incubation is complete. Transfer plates to Stage 2 incubator ({min}-{max} °C) to start the second stage."; "Media Lot (inherited): {lot}"; select aria "Stage 2 incubator", empty option "Select Stage 2 Incubator ({min}-{max} °C)"; buttons "Back", "Start Stage 2 Incubation" (disabled when no incubator). Endpoint start-stage-2-incubation [src: TestWorkflowService.ts:29].
- enter-result (CountTest only) [src: :666-810]:
  - Dilution Factor text field (label "Dilution Factor", type number, min 1) [src: :682-699].
  - Helper text variants: "Direct count: dilution factor fixed at 1" (EM/AC/Water); "Enter multiplier: 10 for 1:10 dilution, 100 for 1:100" (overriding); "Configured value from Specifications (N)" [src: :688-693].
  - Override icon button aria "Override dilution factor"; button "Cancel Override" [src: :701-721].
  - Plate fields: label "Plate {i}", placeholder "Colony count or TNTC", remove aria "Remove reading {i}"; button "Add Plate" [src: :742-768].
  - Live result box: "Calculated result: {display}" [src: :782-783]; non-numeric note "Non-numeric result: reviewer will decide accept or retest" [src: :787].
  - Submit button label "Calculate" (CountTest) [src: :806]; disabled when !isTimeReady || dilutionFactorNotConfigured || (differs and no note) [src: :803].
  - Submit calls TestWorkflowService.recordCountResult -> POST record-result [src: TestWorkflowService.ts:37]. **No signature**: TestWorkflowDialog imports no SignatureDialog [src: frontend/src/modules/testingWorkspace/TestWorkflowDialog.tsx:1-25].
- step-complete [src: :812-830]: "Step recorded: {outcome}" (+ ": Average X, Calculated Y, Status Z" when average present) [src: :815-817]; non-definitive: "{outcome} - this step must be repeated with a fresh media lot." [src: :821]; button "Proceed to Next Step" or "Retry This Step" [src: :827].
- Error texts [src: :88, 176, 189, 201, 211, 265, 269, 273, 291]: "Could not load this test's workflow."; "Could not skip the wait."; "Could not start incubation for this step."; "Could not start stage 2 incubation."; "Could not close this incubation window."; "No Dilution Factor is configured for this test on the item's Specifications. Ask a Section Head/Admin to configure it before recording a result."; "Enter at least one plate reading."; "A justification note is required when the entered Dilution Factor differs from the configured value."; "Could not record this result."

### 2.2 Dilution factor rules (CountTest, non-direct)
- isDirectCount = sampleType in Water, EnvironmentalMonitoring, AfterCleaning [src: :80, 222-223]; direct count factor set to "1" [src: :83].
- dilutionFactorNotConfigured = CountTest && not direct && configuredDilutionFactor == null. Blocks submit and shows error alert "No Dilution Factor is configured for this test on the item's Specifications tab. Ask a Section Head/Admin to configure it before recording a result." [src: :233, 670-673].
- dilutionFactorDiffersFromConfigured -> required TextField "Justification for override" (multiline, helper "Required: the entered value differs from what's configured on Specifications. This is captured in the audit trail.") [src: :234-235, 726-739].
- Default factor when configured: the configured value; otherwise empty string [src: :83].
- Client default when parse fails: `Number(dilutionFactor) || 1` [src: :279].

### 2.3 Live calculated result and TNTC / Uncountable handling
- isNonNumeric: trimmed lower-case value equals "tntc" or "uncountable" [src: :219-220].
- Live result [src: :239-259]:
  - If any reading is TNTC/Uncountable -> display = that text upper-cased (e.g. "TNTC"), flagged isNonNumeric; no average computed [src: :240-244].
  - Else average of numeric readings times dilution factor [src: :245-252]. Display "< {df} {unit}" when the final value is below the dilution factor, else the number (no decimals if whole, else one decimal) + unit [src: :253-258].
  - Unit default "CFU/mL" when sampleContext.cfuUnit is absent [src: :237].
- Visual flag: the plate field border uses the inconclusive colour and helper text "⚠ Non-numeric, will be flagged for reviewer decision" when the reading is TNTC/Uncountable [src: :752-762].
- Server-side handling of non-numeric readings is not read (UNCLEAR from this file); this file sends raw strings in `rawPlateReadings` [src: :276-283].

### 2.4 Location-batch count entry (EM, After Cleaning, Water)
- LocationResultGridDialog (EM/AC count batch) [src: frontend/src/modules/testingWorkspace/LocationResultGridDialog.tsx:62-212]:
  - Title "{testCode} Results: {displayName}" [src: :142].
  - Columns "Location", "Limits (Alert / Action / Spec)", "Plate Readings", "Calculated Result", "Status" [src: :152-156].
  - Each location has its own numeric inputs (type number, aria "{location} reading {i}"); "Add Plate" and remove buttons [src: :168-186].
  - Calculated result = average of the location's numeric readings, dilution factor always 1 [src: :102-105]; reported = "<1" when below 1, else rounded integer [src: :52-54].
  - Live status via compareStatus: OutOfSpecification > ActionLimitExceeded > AlertLimitExceeded > LimitsNotConfigured > WithinLimits (precedence Spec -> Action -> Alert) [src: :41-50].
  - Summary "{conform}/{total} locations within spec; worst status: {badge}" [src: :195-197].
  - Non-numeric text cannot be entered (inputs are type number) [src: :174-178]; a location with no numeric reading blocks submit [src: :109-112].
  - Submit "Submit Results" / "Submitting…" disabled when !allEntered || !isTimeReady || submitting [src: :204-206]; warning "Results cannot be submitted before {datetime}." [src: :198-200]. Endpoint POST batch-results [TestWorkflowService.ts:206]. Error "Could not record batch results." [src: :135]. **No signature**.
- WaterLocationResultGridDialog (Water count batch) [src: frontend/src/modules/testingWorkspace/WaterLocationResultGridDialog.tsx:48-195]:
  - Columns "Sampling Point", "Limits (Alert / Action / Spec)", "Plate Readings", "Average", "Status" [src: :137-141].
  - Shows the average, not calculated CFU; no dilution factor [src: :88-90, 171].
  - Same compareStatus precedence [src: :37-46].
  - Submit "Submit Results" -> POST water-batch-readings [TestWorkflowService.ts:209]; error "Could not record batch readings." [src: :120]. **No signature**.
- Routing: Water count batch -> WaterLocationResultGridDialog; other CountTest batch -> LocationResultGridDialog [src: frontend/src/modules/testingWorkspace/TestWorkflowDialog.tsx:631-652].

### 2.5 Count result display in the sample summary and printable report
- Count reading block "Plate Readings", "Dilution Factor", "Average", "Calculated", "Reported Result", "Alert / Action / Spec", "Status", "Entered By", "Entered At" [src: frontend/src/modules/testingWorkspace/sampleSummary/FinalResultBlock.tsx:44-64].
- Count card (report) stat labels "Plate count", "Dilution", "Average", "Calculated"; section "plate readings" [src: frontend/src/modules/testingWorkspace/TestResultCards.tsx:517-570] (partially read).
- Formatting: CFU rounded to whole number for display only [src: frontend/src/modules/testingWorkspace/TestResultCards.tsx:15-21].
- Badge text: "{n} exception(s)" or "Within limits" for location tests; humanised status otherwise [src: frontend/src/modules/testingWorkspace/TestResultCards.tsx:528-532].

### 2.6 Incubation window and time gates (count and pathogen)
- Window source: server-resolved; panels show values "exactly as the server resolved it" [src: frontend/src/modules/testingWorkspace/utils/incubationWindow.ts:6-9]. Not-configured message: "The incubation window for this step's medium is not configured in Test Master. Its incubation hours and temperature must be set before this step can continue." [src: frontend/src/modules/testingWorkspace/utils/incubationWindow.ts:3-4].
- isTimeReady = !windowNotConfigured && (no minReadyAt || now >= minReadyAt || minimumDurationOverridden) [src: frontend/src/modules/testingWorkspace/TestWorkflowDialog.tsx:159-162].
- Gate effects in TestWorkflowDialog: awaiting-result buttons disabled when !isTimeReady (lines 604, 609, 611, 614, 616, 618); enter-result submit disabled (803); warning "Not ready yet - available from {datetime}." [src: :589-591]; "Results cannot be submitted before {datetime}." [src: :795-797].
- Skip Wait: visible when canOverrideWait (PERMISSIONS.TEST_WORKFLOW_SUPERVISE) and !isTimeReady and !windowNotConfigured [src: :165, 598-601]. Confirmation text: "Skip the remaining minimum incubation wait time for this step? This bypasses the wait only; the recorded incubation window is not changed." [src: :624-629]. Calls overrideMinimumDuration [src: :172]. Shown text after skip: "Minimum wait time was skipped by a Section Head/System Administrator." [src: :592-594]. Skip does not use a signature [src: :168-180].
- Windowed-not-configured error alert shown in awaiting-result [src: :586-588].
- LocationResultGridDialog, WaterLocationResultGridDialog and PathogenLocationResultGridDialog each disable Submit on !isTimeReady (LocationResultGridDialog.tsx:204; WaterLocationResultGridDialog.tsx:187; PathogenLocationResultGridDialog.tsx:112).

### 2.7 Count-test helper
- Count test is selected by `workflowType === "CountTest"` [src: frontend/src/modules/testingWorkspace/TestWorkflowDialog.tsx:225].
- Item-based count tests (TAMC, TYMC) per PrimaryObservationMatrixPanel are treated as quantitative by code match: see section 4.10 (heuristic).

---

## 3. Observation tests (qualitative observation and yes/no growth)

### 3.1 QualitativePanel (workflowType "Qualitative") - physicochemical [read by pattern search]
- Loaded through TestWorkflowDialog [src: frontend/src/modules/testingWorkspace/TestWorkflowDialog.tsx:352-365].
- Labels: "Analysis time" (date field) [src: frontend/src/modules/testingWorkspace/QualitativePanel.tsx:298]; "Equipment (Physicochemical)" with "None (no instrument linked)" [src: :307-316]; "Comment (optional)" [src: :449]; observation field label "Observation (required) *" or "Observation (optional)" with helper "Observation is required when non-compliant" [src: :425-436].
- Per-row toggle values: "complies" (label "Complies") and "non-comply" (label "Does not comply") [src: frontend/src/modules/testingWorkspace/QualitativePanel.tsx:369-417].
- Validation (isFormValid): at least one row; analysedAt not in the future by more than 5 minutes; a non-conforming row needs an observation; observation max 500 chars [src: :186-200].
- Action button "Sign and record result", disabled when !isFormValid [src: :459-466]; opens SignatureDialog with meaning "I entered these qualitative observations and am recording this test result." [src: :470-475]. Endpoint record-qualitative-result [src: frontend/src/modules/testingWorkspace/services/TestWorkflowService.ts:98]. **Signature**.
- Outcome banner: text from outcome summary; severity by status containing "OutOfSpecification" or "RequiresReview" [src: :239-257].

### 3.2 PathogenLocationResultGridDialog (EM/AC batch pathogen - yes/no per location)
- Title "{testCode} Results: {displayName}" [src: frontend/src/modules/testingWorkspace/PathogenLocationResultGridDialog.tsx:72].
- Columns "Location", "Growth Observed?", "Status" [src: :82-85].
- Radio group per row, labels "Yes" (value yes) and "No" (value no) [src: :92-94].
- Live status "Detected" (Yes) or "Absent" (No) [src: :49-52]; summary "{detected}/{total} locations detected; overall: {Detected|Absent}" [src: :103-105].
- Submit "Submit Results" / "Submitting…", disabled when not all rows answered, !isTimeReady or submitting [src: :112-114]; endpoint batch-pathogen-results [TestWorkflowService.ts:200]; error "Could not record batch pathogen results." [src: :65]. **No signature**.
- Note in source: this dialog is the EM/AC final-step per-location Detected/Absent call; there is no dual-plate variant [src: :28-30; frontend/src/modules/testingWorkspace/services/TestWorkflowService.ts:196-199].

### 3.3 Primary plate observation (session matrix, qualitative cells)
- Option labels in the cell select "Select Observation", "No Growth (-)", "Growth Non-Conforming", "Growth Conforming (+)" [src: frontend/src/modules/testingWorkspace/pathogenSession/PrimaryObservationMatrixPanel.tsx:811-828].
- Covered in section 4.10.

---

## 4. Pathogen

### 4.1 PathogenStepDialog (five-stage chain router)
- Renders by step.stepType [src: frontend/src/modules/testingWorkspace/pathogenSteps/*; frontend/src/modules/testingWorkspace/PathogenStepDialog.tsx:143-166]:
  - BrothEnrichment / SelectiveBroth -> BrothWaitingPanel if incubation lock exists, else BrothStepPanel.
  - SelectivePlating -> SelectivePlatingPanel.
  - ConfirmatoryPlating -> ConfirmatoryPlatingPanel.
  - BiochemicalTest -> BiochemicalTestPanel.
  - anything else -> UnsupportedStepPanel (not read).
- Step chip [src: :103-107]: "Incubating" (broth, incubating), "Preparation · no pass/fail" (broth, not incubating), "Set up" (plating, not incubating), "Ready to read" (plating, time ready), "Incubating" (plating, not ready), "Result entry" (biochemical).
- Step help behind link "What happens in this step?" / "Hide step details" [src: :123-134]; texts per type [src: :27-33]:
  - BrothEnrichment / SelectiveBroth: "This step is preparation only - it is not a pass/fail result. The workflow proceeds to the next step once the assigned incubation window completes."
  - SelectivePlating: "Plate onto the selective medium and start incubation. Once the minimum incubation has passed, read the plate and record what grew against the expected appearance."
  - ConfirmatoryPlating: "Plate onto each confirmatory medium the method permits and start incubation. Once the minimum incubation has passed, record an observation for every plate."
  - BiochemicalTest: "Record the biochemical identification result and whether it shows the target organism. Your Detected / Not Detected decision sets the final result."
- Final: "✓ Final result: {finalResult}" and button "Done / Close" [src: :72-88].
- Load error: "No current step is available for this test order." [src: :94].

### 4.2 BrothStepPanel (BrothEnrichment and SelectiveBroth, not incubating)
- Fixed medium row "Medium" with lock "Test Master" [src: frontend/src/modules/testingWorkspace/pathogenSteps/StepLayout.tsx:89-100; BrothStepPanel.tsx:244].
- Lot and incubator fields: "Media lot" (helper "Released and within expiry" / "No released lots of this medium are available" / "{n} released lot(s) available") and "Incubator" (helper "Only incubators set within {min}–{max} °C are listed" or "No incubators are set within ..."); incubators from getEligibleIncubators (server-filtered) [src: StepLayout.tsx:108-155; BrothStepPanel.tsx:79].
- Timeline "Incubation window": "Start" 0 h, "Ready from" {minHours} h, "Read by" {maxHours} h [src: BrothStepPanel.tsx:258-264].
- Note "Start time, media lot and incubator are recorded in the audit trail." or "Select a media lot and an incubator to continue." [src: :266-270].
- Button "Start Incubation" / "Starting..." disabled when saving or !canStart [src: :271-278].
- Missing medium: "This step has no assigned medium configured in Test Master." [src: :236].
- Shared-TSB logic: if the sample already has a shared TSB (or shared broth step complete) the panel shows the "Shared TSB Applied" success alert with text "This test is linked to the shared TSB for this sample. There is one shared tube per sample; no separate broth entry required." and button "Proceed to Next Step →" (or "Advancing...") [src: :114-173]. Proceed calls submitBroth with null observation [src: :96-112, 100]. Error text containing "order violation" or "already" is treated as success [src: :104-106].
- Shared-TSB prompt: before the first start on the BrothEnrichment step (not SelectiveBroth), sibling pathogen orders are fetched; if any, dialog "Apply Shared TSB to All Pathogen Tests" with "Confirm & Apply to All" [src: :202-227, 282-352]. Dialog text: "One TSB tube is shared across all pathogen tests for this sample. There is one physical shared tube per sample; no per-test variation is permitted." [src: :312-315]. Applies-to list heading "Applies to all pathogen tests for this sample:" [src: :332]. Footer "This action is recorded in the audit trail per ALCOA+ contemporaneous recording requirements." [src: :349-351].
- Client errors: "Select a media lot and an incubator." [src: :193].
- **No signature** on this panel. Endpoint submit-broth [TestWorkflowService.ts:216]; selectMedia used for start [src: BrothStepPanel.tsx:179-181].

### 4.3 BrothWaitingPanel (incubating)
- Countdown and details come from IncubationWaitingView (section 4.6).
- Button "Proceed to {nextStep}" or "Proceed to Reading" (or "Submitting..."), disabled when !ready or submitting [src: frontend/src/modules/testingWorkspace/pathogenSteps/BrothWaitingPanel.tsx:46-54].
- **No signature**.

### 4.4 SelectivePlatingPanel
- Expected appearance note: when `expectedAppearance` is null: warning "Expected appearance not configured in Test Master for {media}. Growth vs. no-growth can still be recorded, but a conforming/non-conforming judgment is unreliable until Test Master is fixed." Otherwise info "Expected target-positive colony" + value [src: frontend/src/modules/testingWorkspace/pathogenSteps/StepLayout.tsx:159-171].
- Phases: setup (not incubating) / waiting (countdown; readiness on server or countdown zero) / readout [src: frontend/src/modules/testingWorkspace/pathogenSteps/SelectivePlatingPanel.tsx:71-75].
- Setup: lot and incubator fields; timeline; button "Start Incubation" / "Starting..." disabled when !mediaId || !equipmentId || submitting [src: :153-166]. Endpoint start-selective-plating-incubation [TestWorkflowService.ts:223].
- Waiting: IncubationWaitingView with nextLabel "Plate reading" and a disabled "Record Observation" [src: :171-183].
- Readout: info "Incubation complete. Read the plate and record what you observe." [src: :188]; rows "Started", "Ready from", "Read by" [src: :189-195].
- Observation options (ObservationOptions radio) [src: :16-20]:
  - "No growth" (detail "Target absent") - value NoGrowth.
  - "Growth, does not match expected appearance" (detail "Target absent") - GrowthNonConforming.
  - "Growth matching expected appearance" (detail "Presumptive positive") - GrowthConforming.
- Note field "Observed appearance note (optional)" with helper "Colony characteristics seen on the plate." [src: :204-212].
- Submit "Record Observation" / "Recording..." disabled when !observation or submitting; note "Select an observation to continue." [src: :214-221]. Error "Select an observation." [src: :100]. Endpoint submit-selective-plating-observation [TestWorkflowService.ts:230]. **No signature**.

### 4.5 ConfirmatoryPlatingPanel
- Target organism row "Target organism" with organism name [src: frontend/src/modules/testingWorkspace/pathogenSteps/ConfirmatoryPlatingPanel.tsx:243].
- Setup phase: text "Tick every medium you actually plated, then choose its lot and incubator. Only media the step template permits are listed." [src: :247-250]. Each permitted medium: checkbox labelled with the medium name; when ticked shows "Media lot" and "Incubator" fields and expected-appearance note [src: :251-286].
- Timeline when incubationMaxHours > 0; otherwise warning "Incubation duration is not configured in Test Master." [src: :287-297]. Note "Each medium keeps its own window; the shared incubation runs to the longest one selected." [src: :298-300].
- Setup note "Start time, media lots and incubators are recorded in the audit trail." or "Tick at least one medium and choose its lot and incubator to continue." [src: :302-304]. Button "Start Incubation" / "Starting..." disabled when submitting or !setupComplete [src: :306-313].
- Setup validation: "Check at least one medium that was actually plated." [src: :147]; "Every checked medium needs a media lot and an incubator." [src: :149].
- **Hardcoded fallback**: end time for the setup is `step.incubationMaxHours` or 24 hours when that value is 0 [src: :153-154]. Start is the browser clock at click time [src: :152].
- Waiting phase button "Submit Observations" (disabled) [src: :327].
- Readout phase [src: :332-378]:
  - If setup was already submitted in an earlier session: warning "Confirmatory media for this step were already selected in a previous session. Confirm each medium before recording its observation." [src: :335-338]; otherwise success "Incubation complete. Read each plate against its expected appearance." [src: :340].
  - Per medium: checkbox "This medium was actually plated" (only when uncertain) [src: :353]; observation radios with the same three options as the selective panel (titles "No growth"; "Growth, does not match expected appearance"; "Growth matching expected appearance" with detail "Conforming") [src: :20-24].
  - Note "Record an observation for every plated medium to continue." [src: :367]. Button "Submit Observations" / "Submitting..." disabled when submitting or !readoutComplete [src: :368-375].
  - Validation: "At least one medium must be confirmed as plated." [src: :191]; "Select an observation for: {media}." [src: :194].
- Decision phase (when the server reports analystDecisionRequired) [src: :201-202, 380-406]: success "Every confirmatory medium showed growth matching the target organism's expected appearance."; text "Per SOP, choose whether to conclude detection from this conforming confirmatory result alone, or proceed with biochemical confirmation."; buttons "Conclude as Detected" (SubmitAsDetected, red outlined) and "Proceed to Biochemical Confirmation" (ProceedToBiochemical) [src: :390-403].
- Already-recorded error code CONFIRMATORY_ALREADY_RECORDED shows the server message in an info alert [src: :175-177, 226-232]; CONFIRMATORY_SETUP_ALREADY_SUBMITTED moves on [src: :171-174].
- **No signature**. Endpoints: submit-confirmatory-setup [TestWorkflowService.ts:248], submit-confirmatory-observations [:256], analyst-decision [:260].

### 4.6 IncubationWaitingView and IncubationCountdown (shared by all incubating pathogen steps)
- Details rows "Media lot", "Incubator" (with set temperature), "Temperature", "Duration" [src: frontend/src/modules/testingWorkspace/pathogenSteps/IncubationWaitingView.tsx:88-95].
- Timeline "Started", "Ready from", "Read by" [src: :117-123].
- Alerts: "Not ready yet. Available from {datetime}." [src: :129-131]; "Minimum wait time was skipped by a Section Head/System Administrator." [src: :132-134].
- Countdown ring with centre text "Ready" when reached [src: frontend/src/modules/testingWorkspace/pathogenSteps/IncubationCountdown.tsx:69]; aria "{remaining} until {next} is available" or "{next} is available" [src: :64]; caption "until {next} is available ({h} h minimum incubation)" or "{next} is available now" [src: :81].
- Countdown reaching zero triggers a reload of the step (onSubmitted) [src: IncubationWaitingView.tsx:72-79].
- Skip Wait visible to PERMISSIONS.TEST_WORKFLOW_SUPERVISE when not ready and not already overridden and window configured [src: :49, 137-141]; same ConfirmationDialog text as section 2.6 [src: :145-150]. Calls overrideMinimumDuration [src: :101].
- No signature on Skip Wait.

### 4.7 PathogenSessionDialog (session workspace, six steps)
- Title "Pathogen Testing Session Workspace"; subtitle "Session: {sessionId} · Sample: {ref} ({area})" [src: frontend/src/modules/testingWorkspace/pathogenSession/PathogenSessionDialog.tsx:191-197].
- Step labels, in order [src: :44-51]: "Overview", "Shared TSB", "Test Workflows", "Primary Matrix", "Confirmatory Plating", "Review & Complete".
- Label suffix "(N/A)" on "Shared TSB" when no test requires TSB, and on "Confirmatory Plating" when no eligible confirmations [src: :250-252].
- Step disabled (lock icon) when: Confirmatory Plating and no eligible confirmations (hasEligibleConfirmations false) [src: :244-245]; Review & Complete and not canReview [src: :246-247]. canReview = completedResultCount === requiredResultCount && required > 0 && !sharedTsb.isIncubating [src: :143-146].
- Initial step on open [src: :75-83]: if completed === required (and > 0) -> Review; else if some completed or primary observations exist -> Primary Matrix; else if TSB started -> Shared TSB; else Overview.
- Overview "Start" button goes to Shared TSB if any test requires TSB else Test Workflows [src: :292-295].
- Header chip shows overallSessionStatusDisplay.
- "Reset Session" button (header, outlined) shown only when TEST_WORKFLOW_SUPERVISE [src: :55-56, 217-233]. Reset dialog: title "Reset Testing Session & Steps?"; text resets "all workflow step records, incubations, primary observations, and confirmatory results for Sample {ref} back to the initial 'Not Started' state" [src: :357-360]; field "Reason for Reset *" with placeholder "e.g. Test configuration error / Wrong media lot selected / Operational error" [src: :362-371]; buttons "Cancel", "Confirm Reset" / "Resetting..." (disabled when !reason.trim()) [src: :374-383]. Default reason sent when blank: "Section Head requested session reset" [src: :115].
- **No signature** on session reset; reset uses reason only (PathogenSessionService.ts:43).

### 4.8 SessionOverviewPanel (step 0)
- Cards: "Sampling Locations" (totalLocations), "Assigned Tests", "Required Results ({locations} × {tests})", "Completed ({pending} pending)" [src: frontend/src/modules/testingWorkspace/pathogenSession/SessionOverviewPanel.tsx:70, 99, 128, 157].
- Callout chip "Master Data Driven" and title "Workflow Configured from Test Master"; text says assigned tests are loaded from the Sample Test Profile and TSB enrichment and selective incubation run once per session [src: :182-191].
- Button "Open Shared TSB Setup" (when any test requires TSB) or "Open Test Workflows" [src: :202].
- Tables: "Assigned Tests (N)" with chip "Source: Test Master" and headers "Test Code", "Display Name", "Workflow", "TSB" (chip "Required") [src: :219-262]; "Sampling Locations (N)" with chip "Program: {programName}" and headers "#", "Location Name", "Type", "Grade" (chip "Grade X") [src: :280-316].

### 4.9 SharedTsbEnrichmentPanel (step 1) and SharedTsbStatusCard
- Not started: title "Start Shared TSB Enrichment"; description "Configure media and incubator once for the entire session. Required temperature and duration are governed by Test Master." [src: frontend/src/modules/testingWorkspace/pathogenSession/SharedTsbEnrichmentPanel.tsx:197-202].
- Scope chips "{n} Sampling Locations" and each TSB test "{code} ({name})" under "APPLICABLE WORKFLOW SCOPE:" [src: :210-227].
- Fields [src: :243-302]:
  - "TSB Media Lot (GPT Passed & Released) *" (helper "Only active, unexpired, GPT-released lots are permitted"). Option text "{lot}, {material} (Exp: {date})".
  - "Incubator Equipment (In-Service) *" (helper "Select an operational, calibrated incubator"). Option text "{code}: {name} ({location})".
  - "Required Incubation Temperature *" (read-only; value from server or "Not configured in Test Master"; helper depends on windowNotConfigured).
  - "Required Incubation Time *" (read-only; helper "Each test keeps its own duration; the shared tube only starts them together").
- **Default selection (hardcoded heuristic)**: media default is the first lot whose lotNumber includes "TSB", or materialName includes "Tryptic" or "TSB", or mediaTypeName includes "Broth"; otherwise the first lot. Incubator default is the first in the list [src: :53-66]. Lookups use masterDataOptions.getReleasedMedia and getEquipment("Incubator") with no set-point filter [src: :46-49].
- Button "Start TSB Enrichment" disabled when submitting or lookups loading or no lot or no incubator [src: :316-322]. Validation text "Please select a valid released TSB media lot and an in-service incubator." [src: :73]. Start sends incubationStartUtc = browser time [src: :79-83]. Error fallback "Failed to start shared TSB enrichment." [src: :87].
- Button "Next: Test Workflows" [src: :324-331]; no-TSB branch: "None of the assigned tests for this session require TSB broth enrichment. You may proceed directly to the test workflows and result matrix." with "Proceed to Downstream Workflows" [src: :95-105].
- Started (locked): title "LOCKED: Execution in Progress"; text "Shared TSB Enrichment parameters are locked and cannot be modified while execution is active."; alert "TSB Enrichment incubation is currently in progress. Downstream steps and final result entry remain locked until completion." or "TSB Enrichment incubation has elapsed and is complete. You may proceed to downstream testing." [src: :109-152].
- **No signature** on TSB start. Endpoint start-tsb [PathogenSessionService.ts:18].
- SharedTsbStatusCard [src: frontend/src/modules/testingWorkspace/pathogenSession/SharedTsbStatusCard.tsx]: chips "Incubation Completed", "Window Not Configured", "Incubating in Progress" [src: :90-101]; title "Shared TSB Enrichment / Incubation"; alert "A test on this shared TSB has no incubation window configured for its medium in Test Master, so the TSB can't be reported as ready. Set the medium's incubation hours and temperature in Test Master." [src: :106-111]; fields "TSB Media Lot" (GPT default "GPT Conform"), "Incubator Equipment", "Incubation Start" (By {user} default "Analyst"), "Available From" ("Unlock Point (...)"), "Expected Window" ("Max Duration: N h"); scope line "Scope: applies to N sampling locations and M applicable pathogen tests (codes)" [src: :113-195].

### 4.10 PrimaryObservationMatrixPanel (step 3, Panel A)
- Matrix: rows = sampling locations, columns = assigned tests; cell key = location x test; test-to-location mapping via testLocationMap [src: frontend/src/modules/testingWorkspace/pathogenSession/PrimaryObservationMatrixPanel.tsx:66-100].
- **Quantitative detection heuristic**: a column is quantitative when workflowType contains "count" OR testCode contains "tamc" OR "tymc" (case-insensitive) [src: :77-79, 155-157, 525-527, 640-642]. Not driven by a configured field.
- Quantitative cell: number input, placeholder "CFU", adornment "CFU", min 0, max 10000, step 1; aria "{test} count (CFU) for {location}" [src: :756-781].
- Qualitative cell: select with the four options (above) [src: :782-829].
- Pre-filled from existing matrix: primaryObservation, or resultCode NOT_DETECTED -> NoGrowth, DETECTED -> GrowthConforming (text-based fallback on resultDisplay) [src: :82-88].
- Locked cells show "Locked" with tooltip t.lockReason or "Locked until upstream workflow step completes" [src: :650-686, 662].
- Counts chips: "Recorded: N", "Available: N", "Locked: N" [src: :361-382].
- Progress bar; "Filter by location name..." search; toggle "Show Pending Only" / "Showing Pending Only"; footer "Showing N of M locations" [src: :467-494].
- Button "Fill All Unset as No Growth (-)" disabled when all tests locked or no available cells [src: :418-423]. Confirmation: "This will set N unset result(s) as No Growth (-) across M location(s): {names}." [src: :843-846]. Empty-cells errors [src: :170-172].
- Save "Save Observations" disabled when saving or all locked [src: :427-434]. Qualitative values saved via savePrimaryObservations; quantitative via saveResultMatrix with resultDisplay "{n} CFU" [src: :269-311]. Error "No observations or quantitative readings have been entered to save." [src: :294-296]; generic error "Failed to save primary plate observations." [src: :325]. Success text "Primary plate observations saved successfully with ALCOA+ contemporaneous tracking!" [src: :453].
- Auto-advance after save compares the completedCount captured at render with totalCount [src: :321-323] - factual note; result is stale-by-one-render in closure terms (UNCLEAR whether this is intentional).
- Next button "Proceed to Confirmation" (when eligible confirmations) or "Review & Complete"; disabled when completedCount < totalCount or TSB incubating [src: :437-445].
- Lock banner: "Primary plate observations are locked until required workflow steps complete. Shared TSB Enrichment is currently incubating." [src: :334-337].
- **No signature**. Endpoints: save-primary-observations [PathogenSessionService.ts:21], save-matrix [:37], eligible-confirmations [:25-26].

### 4.11 BatchConfirmatoryPlatingPanel (step 4, Panel B)
- Eligible pathogen tabs; each has requiredMediaCount = requiredConfirmatoryMediaCount || (test code contains "SALMONELLA" ? 2 : 1) [src: frontend/src/modules/testingWorkspace/pathogenSession/BatchConfirmatoryPlatingPanel.tsx:146-168, 161].
- Header "Confirmatory Plating (Panel B)" with chip "N Flagged Locations" [src: :455-471]. Phase toggles "1. Shared Media Setup" and "2. Plate Observations" [src: :480-495].
- Empty state "Confirmatory Plating Not Required" - "All sampling locations in this batch were recorded as No Growth (-) during primary plate observation. No confirmatory plating is required for this session." with buttons "Back to Primary Matrix" and "Proceed to Review & Complete" [src: :418-441].
- Setup: title "Shared Media & Incubation Setup: {test}"; text "{n} confirmatory media required by Test Master specification for {code}."; list "FLAGGED SAMPLING LOCATIONS (N):" [src: :557-588].
  - Media selects "Confirmatory Medium {i} Lot (GPT Passed & Released) *" with helper "Select media lot for medium #i"; option text "{lot}, {material} (Exp: {date})" [src: :603-625].
  - "Incubator Equipment (In-Service) *" (helper "Select active, calibrated incubator") [src: :628-642].
  - "Actual Incubation Start Time *" (datetime-local, helper "Contemporaneously recorded start timestamp") [src: :645-656].
  - Read-only "Incubation Duration (from Test Master)" showing "{min}–{max} hours" or "Not configured in Test Master" in error colour [src: :658-671].
  - Defaults: lot = released lot at the same index; incubator = first incubator; start = now [src: :186-198, 193-194].
  - Validation: "Please select all N required media lot(s) and an incubator." [src: :250-251].
  - Button "Start Confirmatory Incubation" disabled when submitting or lookups loading [src: :685-693]. Endpoint start-confirmatory-setup [PathogenSessionService.ts:31]. **No signature**.
- Readout: chips "Multi-Media Agreement Status:", "Detected (+): n", "Not Detected (-): n", "Inconclusive (Retest): n", "Pending Reads: n" [src: :711-745].
  - Agreement rule (client-side, per location) [src: :315-326]: all media Conforming -> Detected; all media NonConforming or NoGrowth -> Not Detected; otherwise Inconclusive. Badges "Pending Read", "Detected (+)", "Not Detected (-)", "Inconclusive (Retest)" with tooltip "Media observations disagree across plates. Retest is required." [src: :866-895].
  - Per medium radios: "No Growth", "Non-Conf", "Conf (+)" [src: :845-859].
  - Biochemical card (only when stats.detected > 0): title "Biochemical Supporting Observation (Optional)"; text "... This comment will be contemporaneously recorded in the audit trail."; placeholder examples "E.g. 'Indole test positive; confirms E. coli detection' or 'Oxidase negative; gram-negative rod; consistent with Enterobacteriaceae family'" [src: :904-925].
  - Buttons "Modify Setup", "Save Readings & Continue" (disabled when !allComplete) [src: :748-761]; footer "Back to Setup", "Save Confirmatory Readings & Continue" [src: :930-946].
  - Errors: "Please complete plate readings for all N pending location(s)." [src: :369-371].
  - Materials fallback: materialId = lot's materialId, else the lot id if > 0, **else hardcoded 1** [src: :386-390].
  - Success: "Confirmatory records and agreement evaluation updated successfully!" [src: :503].
- **No signature** on setup or readings. Endpoints: start-confirmatory-setup, save-confirmatory-readings [PathogenSessionService.ts:34].

### 4.12 SessionReviewPanel (step 5, Review & Complete)
- Title "Testing Session Review: {ref}"; meta "Program: … · Area: … · Control: …" [src: frontend/src/modules/testingWorkspace/pathogenSession/SessionReviewPanel.tsx:248-259].
- Button "Complete Testing Session" / "Session Completed & Submitted" disabled when submitting, not all complete, or already completed [src: :262-272]. Error "Cannot complete session: N required results are still missing." [src: :191]; fallback "Failed to complete testing session." [src: :200].
- **No signature**: handleCompleteSession calls completeSession(sampleId) with no password [src: :189-198; PathogenSessionService.ts:40].
- Warning "There are N missing results. Please complete all Location × Test entries before final completion." with action "Edit Matrix" [src: :278-289]. Inconclusive warning "N sampling location(s) resulted in Inconclusive (Retest) due to confirmatory plate media disagreement. These have been flagged for laboratory investigation / re-test." [src: :291-295].
- Results table heading "Final Analytical Results Summary"; subtitle "N of M results verified (L Locations × T Tests) · Click any result badge to toggle evidence derivation details." [src: :312-318]; button "Modify Matrix" [src: :324-332].
- Result chips: "Detected (+)", "Not Detected (-)", "Inconclusive (Retest)", "N CFU", otherwise by testSessionState: "Locked", "Window Not Configured", "TSB Incubating", "Plating In Progress", "Incubating", "Ready to Read", "Enter Result", "Pending Review", "Reviewed", "Approved", "Rejected" / "Voided" / "Cancelled", "Superseded" / "On Hold", default "Pending" [src: :408-530].
- Evidence panel "Evidence Chain & Derivation:" with steps "1. Primary Plate Observation:", "2. Confirmatory Plating (N Media):" (or "Not required (resolved at primary plate)"), "3. Agreement Evaluation:"; agreement sentences "All confirmatory media conforming → Detected (+)", "All media non-conforming / no growth → Not Detected (-)", "Media disagreement across plates → Inconclusive (Retest Required)" [src: :85-141]; footer "Entered by: {name} at {datetime}" [src: :158-160].
- Audit card "Audit & Traceability (ALCOA+ Compliance)" with "Total Sampling Locations", "Results Verified", "Outcome Breakdown", "Session Status" ("✓ Completed & Locked" or "Ready for Technical Review") [src: :550-619].

### 4.13 DownstreamWorkflowsPanel (step 2)
- Title "Assigned Test Workflows (N)"; button "Go to Result Matrix" [src: frontend/src/modules/testingWorkspace/pathogenSession/DownstreamWorkflowsPanel.tsx:46-69].
- Tabs per test with chip "TSB" [src: :88-106]; test detail "Workflow Model: {type} · Operational Scope: {n} Sampling Locations" [src: :127-131]; heading "Configured Step Sequence (N Steps)" [src: :146].
- Step rows: chip "TSB Incubating" or "Shared Session Step" (TSB step); "Locked until TSB Complete" [src: :200-220]. Detail: "Incubation Parameters" "{min}–{max} °C ({h}–{h} h)"; "Target Medium" (default "Standard Culture Media"); "Execution State": "✓ Executed & Recorded", "🔒 Locked (TSB Incubating)", or "Pending Matrix Evaluation" [src: :232-265].
- Alert when TSB incubating: "Shared TSB Enrichment incubation is currently in progress. Downstream pathogen workflow steps and final result entry are strictly locked until TSB incubation completes." [src: :37-41].

---

## 5. EM, Water and After Cleaning (sampling, locations, batch flows)

### 5.1 Preparation dialog (opened from the workspace "Prepare Sample")
- PreparationDialog title "Preparation" [src: frontend/src/modules/testPreparation/PreparationDialog.tsx:70].
- Routing by category [src: frontend/src/modules/testPreparation/PreparationDialog.tsx:21-24, 70-101]: EnvironmentalMonitoring -> EMPreparationForm (needs departmentId); AfterCleaning -> AfterCleaningPreparationForm (needs machineId); Water -> WaterPreparationForm (needs waterDepartmentId); Product/RM/PM -> item preparation configuration (ConfirmPreparationForm when a config exists, TestPreparationForm when none).
- Note: EMPreparationForm, AfterCleaningPreparationForm and WaterPreparationForm are imported from frontend/src/modules/laboratoryConfiguration (not testPreparation) [src: frontend/src/modules/testPreparation/PreparationDialog.tsx:7-9].

### 5.2 EMPreparationForm (room grid)
- Items = rooms of the department, subtitle "Grade {x}" (leading "Grade " stripped), assigned tests = distinct test codes of the room configs; rooms with no configs are disabled [src: frontend/src/modules/laboratoryConfiguration/environmentalMonitoring/EMPreparationForm.tsx:31-40].
- Loading text "Loading rooms..." [src: :60].
- Confirm: sends the ids of every test configuration of every checked room to POST /em/prepare [src: frontend/src/modules/laboratoryConfiguration/environmentalMonitoring/EMPreparationForm.tsx:44-52; frontend/src/modules/laboratoryConfiguration/environmentalMonitoring/services/EMPreparationService.ts:9]. Comment: one TestOrder per distinct test code across all selected rooms [src: :14-15].
- **No signature** (confirm goes straight to prepare).
- Error fallback "Could not prepare sample." [src: :58].

### 5.3 AfterCleaningPreparationForm (machine part grid)
- Same grid pattern; subtitle "Machine Part" [src: frontend/src/modules/laboratoryConfiguration/afterCleaning/AfterCleaningPreparationForm.tsx:37-45]; loading "Loading machine parts..." [src: :63].
- POST /aftercleaning/prepare [src: frontend/src/modules/laboratoryConfiguration/afterCleaning/services/AfterCleaningPreparationService.ts:9].
- **No signature**.

### 5.4 WaterPreparationForm (sampling point grid + storage)
- Storage fields before the grid [src: frontend/src/modules/laboratoryConfiguration/water/WaterPreparationForm.tsx:97-128]: "Storage Condition *" (options "Refrigerator", "Room Temperature"; placeholder "Select storage condition"); when Refrigerator, "Storage Time (hours) *" (number).
- Confirm disabled while storage incomplete (no condition, or refrigerator without hours) [src: :31-33, 137]. Payload: waterSamplingPointIds, storageCondition, storageTimeHours (number only when Refrigerator) [src: :62-68].
- Points with no assigned tests are disabled; subtitle = location [src: :44-52].
- POST /water/prepare [src: frontend/src/modules/laboratoryConfiguration/water/services/WaterPreparationService.ts:21].
- Loading "Loading sampling points..." [src: :122]. **No signature**.

### 5.5 SamplingPointGrid (shared grid)
- Select All checkbox, chip "{n} selected" [src: frontend/src/modules/testPreparation/components/SamplingPointGrid.tsx:82-111].
- Card per point with "Assigned Tests" chips or "No tests configured" [src: :184-270].
- Button label default "Start Testing" (confirmLabel); "Starting..." when loading; disabled when none selected, loading or confirmDisabled [src: :274-278].

### 5.6 Batch workflow after preparation (EM, AC, Water)
- Preparation creates one test order per distinct test code (see 5.2-5.4). The grouped action panel then offers "Start Incubation" for the group (section 1.7).
- Incubation for EM/AC uses the TestWorkflowDialog phases (section 2.1): "Advance to Next Incubation Window" between steps; "Record Results" on the last step (location grid).
- EM/AC non-CountTest (pathogen) location results: PathogenLocationResultGridDialog (yes/no) - section 3.2.
- EM/AC CountTest: LocationResultGridDialog (CFU average) - section 2.4.
- Water CountTest: WaterLocationResultGridDialog (per point average) - section 2.4.
- Water pathogen: routed to PathogenStepDialog (section 4.1) because the Water + non-CountTest path is not in the EM/AC branch [src: frontend/src/modules/testingWorkspace/TestWorkflowDialog.tsx:419-427].

---

## 6. Media and equipment selection within tests

- Media lot list, count workflow (TestWorkflowDialog): released media filtered to the step's permitted material ids [src: frontend/src/modules/testingWorkspace/TestWorkflowDialog.tsx:112-113]; released list from lookupCache.getReleasedMedia [src: :97].
- Incubator list, count workflow: setpoint within step temperature range; set-point test uses `i.setPointTemperature >= min && <= max` [src: :131-133].
- Stage 2 incubator: setpoint within stage2 range [src: :135-137].
- Grouped action (GroupedActionRow): lots filtered to group permitted materials and not expired (expiryDate < now excluded); incubators filtered to set point within group range and excluding calibrationStatus "lapsed" [src: frontend/src/modules/testingWorkspace/components/GroupedActionRow.tsx:74-92].
- Quick setup (AssignedTestCard): lots filtered to matching materials [src: frontend/src/modules/testingWorkspace/components/AssignedTestCard.tsx:391, 607-611 (options) ]; incubators labelled "{code} ({setpoint}°C)" [src: :639-641].
- Broth / selective / confirmatory panels: incubators from server getEligibleIncubators (filtered to the medium's range server-side) [src: frontend/src/modules/testingWorkspace/services/TestWorkflowService.ts:18; BrothStepPanel.tsx:79; ConfirmatoryPlatingPanel.tsx:105-108].
- Shared TSB and Primary/Batch confirmatory setup: lots from masterDataOptions.getReleasedMedia and incubators from getEquipment("Incubator") with **no set-point or material filter** in the component [src: frontend/src/modules/testingWorkspace/pathogenSession/SharedTsbEnrichmentPanel.tsx:46-49; BatchConfirmatoryPlatingPanel.tsx:132-142].
- Physicochemical equipment: "Equipment (Physicochemical)" option list plus "None (no instrument linked)" in Measurement, Qualitative, Gravimetric [src: frontend/src/modules/testingWorkspace/MeasurementPanel.tsx:329-342; QualitativePanel.tsx:307-320; GravimetricPanel.tsx:487-500]. Titration uses "Equipment (titrator)" [src: frontend/src/modules/testingWorkspace/TitrationPanel.tsx:484].
- Preparation: no media selection in TestPreparationForm; fields "Diluent" and "Neutralizer" are required [src: frontend/src/modules/testPreparation/TestPreparationForm.tsx:57-63, 92].
- Lot display in selects: "{lotNumber} · expires {date}" (MediaLotIncubatorFields) [src: frontend/src/modules/testingWorkspace/pathogenSteps/StepLayout.tsx:137]; "{lot} (material) · Exp: {date}" (GroupedActionRow) [src: components/GroupedActionRow.tsx:362].

---

## 7. Review, approval, return to analyst, retest, void, correction

### 7.1 SampleSummaryDialog (sample-level review and release)
- Title "Sample Summary" [src: frontend/src/modules/testingWorkspace/SampleSummaryDialog.tsx:357-361].
- Header buttons [src: :388-443]: "Open Pathogen Workflow" (if any test usesSharedTsb) [src: :390-406]; "Printable Report" (opens /samples/{id}/report) [src: :407-418]; "View COA" (only when coaEligible) [src: :419-432]; "Export PDF" / "Exporting…" (exportPdf only) [src: :433-442].
- Sections chips "Sections:" with "{section}: {status}" (formatSectionStatus: In testing, Under review, Under approval, Approved, Rejected, Retest requested, Cancelled, Closed, Voided) [src: :447-466; frontend/src/modules/testingWorkspace/sampleSummary/summaryFormatting.ts:1-26]. Closed-section text "{section} closed by {name} on {date}: {reason}" [src: :470-477].
- Workflow History strip "Workflow History" with "No lifecycle events yet." when empty [src: :39-51].
- Printable Report / View COA buttons repeated at the bottom [src: :535-577]: "Open Printable Report", "View Certificate of Analysis".
- Footer "Sample Reference: {ref} · Record Status: {status} · MicroLIMS Operational View" [src: :591-593].
- coaEligible rules [src: :333-353]: needs matrix or simple certificate data; single-section uses combinedCoaAvailable; multi-section uses allSectionsVisible + combinedCoaAvailable or any canView section with coaAvailable.
- overallStatus rollup [src: :297-322]: Approved / Rejected from summary; OutOfSpecification if any location status not WithinLimits/Absent or any count reading OutOfSpecification; Detected if any biochemical organismDetected true, or conforming pathogen not ruled out by biochemistry.
- Permissions [src: :95, 262-278]: canReview = some own section UnderReview AND SAMPLES_REVIEW; canReturn = SAMPLES_REVIEW; canApprove = some own section UnderApproval AND SAMPLES_APPROVE; canCloseTesting = overallStatus "Rejected" AND closable section AND SAMPLES_APPROVE.
- Load error "Failed to load sample summary." [src: :189].
- Export error "Export failed." [src: :290]. Note: exportWord exists in the service [src: frontend/src/modules/testingWorkspace/services/SampleSummaryService.ts:30] but has no caller in frontend/src (verified by grep).

### 7.2 ApprovalSignaturesCard (review and release form)
- Card title "Approval & Electronic Signatures" [src: frontend/src/modules/testingWorkspace/sampleSummary/ApprovalSignaturesCard.tsx:143-145].
- Signature trail: "{role}: {printedName} ({username})" with chip "Verified"; meaning text from SIGNATURE_STATEMENTS [src: :153-193]. Fallback "Reviewer:" / "Section Head:" blocks [src: :196-222]. Empty states: "Awaiting technical review by authorized reviewer." (UnderReview); "Reviewed. Awaiting release approval decision by Section Head." (UnderApproval); "No formal signatures recorded." [src: :225-231].
- Review form "Submit Technical Review" [src: :235-283]:
  - Section select "Laboratory Section" (only when more than one reviewable section) [src: :240-257].
  - Alert "By submitting, I confirm I have reviewed all test results for {section}." (or "... for this sample.") [src: :258-262].
  - Comment "Comment (optional)" [src: :263-272].
  - Button "Submit Review" disabled when more than one reviewable section and none chosen [src: :273-281]. Opens SignatureDialog (password).
- Release form "Submit Release Decision" [src: :287-475]:
  - Section select for approvable sections (>1) [src: :291-308].
  - Comment (optional) [src: :309-318].
  - Decision radio options (DECISION_OPTIONS) [src: frontend/src/modules/testingWorkspace/sampleSummary/summaryFormatting.ts:73-78; ApprovalSignaturesCard.tsx:319-332]:
    - "Approve: Results conform to specifications"
    - "Not Conform (Final Conclusion): Close this sample"
    - "New Sample Required: Close and request new sample"
    - "Retest Retained Sample: Return to testing"
  - "Certificate Remarks (optional)" (only for Approve) with helper "Printed on the Certificate of Analysis exactly as typed. Leave blank to show "No remarks." Separate from the internal comment above, which never appears on the certificate." [src: :333-345].
  - Retest section (RetestRetainedSample or NewSampleRequest): heading "Tests to Retest"; text "Non-conforming tests are pre-checked. Adjust as needed - only the checked test(s) move to the new sample(s); everything else on this sample is left untouched."; checkbox per non-superseded test, chips section name and "Non-conforming" [src: :347-408]. Warning "Select at least one test to retest." [src: :402-406].
  - NewSampleRequest: heading "Analysts for the Two New Samples"; text "Two different analysts are required, and neither may be whoever tested the original sample."; selects "New Sample 1 - Analyst" and "New Sample 2 - Analyst" (name and username); warning "The two new samples must be assigned to two different analysts." [src: :410-457]. Error if analyst list failed: "The list of eligible analysts could not be loaded. Close and reopen this sample to try again." [src: :418-422].
  - Approve out-of-spec warning: "Out-of-specification results in {section}: {tests}. Approving will certify them as conforming - consider Not Conform or a retest." [src: :459-463; logic :101-136].
  - Button "Submit Decision" disabled when !decisionValid [src: :465-473]. decisionValid requires chosen section (when >1), and for retest decisions at least one selected test; NewSampleRequest also requires two distinct analysts [src: :85-92].
  - Opens SignatureDialog with the decision label as meaning text [src: SampleSummaryDialog.tsx:623-630].
- Close Testing block (visible when canCloseTesting) [src: ApprovalSignaturesCard.tsx:477-518]: title "Close Testing"; warning "Another laboratory has rejected this sample. You may close your own laboratory's remaining tests instead of continuing them."; section select when more than one; button "Close Testing" (disabled when >1 closable and none chosen).

### 7.3 TestResultsSection - Return to Analyst
- Button "Return to Analyst" shown only when canReturn (SAMPLES_REVIEW), the test is not superseded, status === "ResultEntered", and the test is a count test [src: frontend/src/modules/testingWorkspace/sampleSummary/TestResultsSection.tsx:43-45, 109-129].
- Count-test definition in the summary [src: frontend/src/modules/testingWorkspace/sampleSummary/TestResultsSection.tsx:33-41]: has countTestReadings or (results without biochemical and without locations), and no pathogen observations.
- Opens ReturnToAnalystDialog [src: SampleSummaryDialog.tsx:600-608].
- ReturnToAnalystDialog [src: frontend/src/components/ReturnToAnalystDialog.tsx:62-136]: title "Return Test to Analyst"; subtitle "Return count test for recount or result re-entry"; field "Reason for Return (optional)" (placeholder "e.g. Plate count recount requested due to bubble artifact"); buttons "Cancel", "Confirm Return" / "Returning...". **No signature** (SampleSummaryDialog.handleReturnConfirm calls returnTestToAnalyst with no password [src: SampleSummaryDialog.tsx:232-238; SampleSummaryService.ts:42-47]).

### 7.4 CloseTestingDialog (other lab rejected; this lab closes its open work)
- Title "Close Testing: {section}" with subtitle "Stops your laboratory's remaining tests on this sample - signed with your password" [src: frontend/src/modules/testingWorkspace/dialogs/CloseTestingDialog.tsx:67-98].
- Text: "Every still-open test in this laboratory will be recorded as Cancelled at the stage it had reached. Approved results already on file are kept." [src: :117-120].
- Field "Reason" (required, multiline) with default "Sample rejected by {lab}" [src: :37-41, 121-130].
- Buttons "Cancel", "Sign & Close" (disabled when reason empty) [src: :101-113]. Then SignatureDialog with meaning "I am closing this laboratory's testing; it will not be resumed on this sample." [src: :133-137]. **Signature**. Endpoint POST approval/sections/{id}/close [SampleSummaryService.ts:69].

### 7.5 Void sample (receiving module, opened from SelectedSampleTestingPanel and the register)
- VoidSampleConfirmationDialog [src: frontend/src/modules/receiving/dialogs/VoidSampleConfirmationDialog.tsx]: title "Void Sample Confirmation" [src: :87]; warning "The sample record and its open tests will be marked as Voided." [src: :128]; field "Reason for Voiding *" (placeholder "Please provide the operational or compliance reason for voiding this sample...", helper "A reason for voiding is required." or "Recorded with your electronic signature and in the sample's audit trail.") [src: :185-197]; button "Sign & Void" disabled when !reason [src: :102, 112]; then SignatureDialog [src: :201-205]. **Signature**. Endpoint POST /samples/{id}/void with reason and password [src: frontend/src/modules/receiving/services/ReceiveService.ts:132-133].

### 7.6 Edit (correction) of sample details
- Entry: "Edit Details" button (SelectedSampleTestingPanel) and "Edit" action in the register [src: frontend/src/modules/testingWorkspace/SelectedSampleTestingPanel.tsx:148-172; ReceivingTestingWorkspacePage.tsx:994, 613-615]. Dialog EditSampleDetailsDialog (receiving module) - labels recorded in frontend-core-screens.md section 3 (EditSampleDetailsDialog) [src: documentation/knowledge/_raw/frontend-core-screens.md:195]. Lock text "Locked: testing has started for this sample" [src: frontend/src/modules/receiving/dialogs/EditSampleDetailsDialog.tsx:261 as quoted in core screens doc].
- Endpoint PUT /samples/{id}/correct [frontend/src/modules/receiving/services/ReceiveService.ts:123].

### 7.7 Retest and new-sample decisions
- Retest decisions are the approval decisions RetestRetainedSample and NewSampleRequest; the retest list is pre-checked for non-passing tests (isTestOrderNonPassing) [src: frontend/src/modules/testingWorkspace/SampleSummaryDialog.tsx:213-223; sampleSummary/summaryFormatting.ts:44-57].
- Retest scope is per section when more than one section exists [src: ApprovalSignaturesCard.tsx:86, 95].
- Biochemical reviewer decision endpoint exists but its UI caller is not present: comment "Reviewer-only. No frontend UI calls this yet" [src: frontend/src/modules/testingWorkspace/services/TestWorkflowService.ts:268-270].

### 7.8 Analyst decision during the pathogen chain (not a review)
- ConfirmatoryPlatingPanel decision "Conclude as Detected" / "Proceed to Biochemical Confirmation" [src: frontend/src/modules/testingWorkspace/pathogenSteps/ConfirmatoryPlatingPanel.tsx:380-403]; BiochemicalTestPanel "Submit as Detected (skip biochemical)" / "Proceed to Biochemical Test" [src: frontend/src/modules/testingWorkspace/pathogenSteps/BiochemicalTestPanel.tsx:98-108]. **No signature** on either; endpoint analyst-decision [TestWorkflowService.ts:260].

### 7.9 Signature dialog (shared, components)
- Title "Electronic Signature"; field "Password" (type password); optional "Comment (optional)"; buttons "Cancel" and "Sign" / "Signing..." (disabled when password empty or submitting) [src: frontend/src/components/SignatureDialog.tsx:52-75]. Server message is shown on failure and the password is cleared [src: :33-36].

---

## 8. Printable sample report and certificate

### 8.1 SampleReportPage (route /samples/:id/report)
- Route and page fields: see frontend-core-screens.md section 5.1 (route at frontend/src/routes/AppRoutes.tsx:102; component at frontend/src/modules/testingWorkspace/SampleReportPage.tsx:108). Not re-read at source level here.
- Loading "Loading report…" [src: frontend/src/modules/testingWorkspace/SampleReportPage.tsx:130].
- Print button "Print / Save PDF" calls window.print [src: :318].
- Archived copies section (ArchivedCopiesSection from reportPrimitives) with download [src: :303].
- Test cards: SummaryMatrix (per-location table) [src: :235] and TestResultCard dispatcher [src: frontend/src/modules/testingWorkspace/TestResultCards.tsx:37].
- Card toggles: "Show analysis details and raw readings (N parameter(s))" [src: TestResultCards.tsx:134]; "Show incubation stage details (N stages)" [src: :485, 550]; "Show full location table" [src: :491, 585].
- Count card badge text and reading labels: see 2.5.
- Detection card: "Detected" / "Absent" / "Undetermined" wording for biochemical interpretation [src: TestResultCards.tsx:468]; observation text per ReportDocumentMapper mirror: "No growth", "Growth observed - does not match target organism", "Growth observed - matches target organism" [src: frontend/src/modules/testingWorkspace/utils/pathogenObservationLabel.ts:8-19].
- Card footer "Entered by {name} · {datetime}" or "No result recorded yet" [src: TestResultCards.tsx:385-388].
- Analysis cards show parameter table headers "Parameter", "Reported", "Unit", "Specification", "Status" and reading columns "Stage", "Time (min)", "Peak Area" (vessel), "Shell (mg)", "Titrant volume (mL)", "Deviation %", "Corrected volume (mL)", "Pass" [src: frontend/src/modules/testingWorkspace/sampleSummary/AnalysisBlocks.tsx:59-177]; flags ">Range" and "<LOQ" [src: AnalysisBlocks.tsx:68; TestResultCards.tsx:108].
- Incubation stage labels "Media Lot", "Incubator", "Temperature", "Duration", "Started At", "Transferred At", "Transferred By", "Completed At", "Completed By", "Outcome" [src: TestResultCards.tsx:284-328; sampleSummary/SummaryCards.tsx:207-261].
- Cancelled-at text "Cancelled at {step}, stage N" [src: frontend/src/modules/testingWorkspace/sampleSummary/TestResultsSection.tsx:86-90].
- Overall status text "Within Limits", "Out of Specification", "Alert Limit Exceeded", "Action Limit Exceeded", "Limits Not Configured" (StatusBadge mapping; frontend/src/components/StatusBadge.tsx:17-26).

### 8.2 Sample identity and preparation (summary cards used in the dialog and report)
- Identity labels: "Reference Number", "Category", "Received At", "Exp Date", "Item / Point / Room / Machine", "Production Stage", "Sampled By", "Batch Number", "Control Number", "Received By", "Sample Quantity", "Mfg Date", "Cause of Testing", "Assigned Analyst" (or "Unassigned") [src: frontend/src/modules/testingWorkspace/sampleSummary/SummaryCards.tsx:63-83]; AfterCleaning adds "Previous Product" and "Previous Product Batch" [src: :80-83]; Water adds "Sampling Point" and "Storage Condition" [src: :87-92].
- Preparation card labels "Amount", "Technique", "Filtration Volume" (mL), "Washing Volume" (mL), "Diluent", "Neutralizer", "Prepared By", "Prepared At" [src: :119-130].
- Location result table headers "Location", "Limits (Alert / Action / Spec)", "Reported Result", "Status", "Entered By", "Entered At" [src: :290-296]; overall conclusion uses severity order (WithinLimits, Absent, LimitsNotConfigured, AlertLimitExceeded, ActionLimitExceeded, OutOfSpecification, Detected) [src: :274-278].

### 8.3 Certificate of Analysis (route /samples/:id/coa)
- Page SampleCoaPage (806 lines) [src: frontend/src/modules/testingWorkspace/SampleCoaPage.tsx]. Only headings were extracted by pattern search: "Certificate of Analysis" [src: :421]; per-location table headers "Location", "Specification", "Sample Result", "Analyst", "Alert", "Action" [src: :166, 231-233, 103-104]; header fields "Reference", "Category", "Batch / Control", "Received", "Batch No.", "Mfg. Date", "Exp. Date", "Sampling / Arrival", "Test Date", "Result Date", "QC No.", "Certificate Date" [src: :495-522, 562-584]; sections "Test Results by Location", "Test Results", "Remarks", "Overall Conclusion", signatories "Reviewer" and "Approver" [src: :501-553].
- Remarks default "No remarks." when none [src: :531-534, 636-637].
- Availability text in the dialog and page: "A Certificate of Analysis is only available once this sample has been approved or rejected. Current status: {status}." [src: frontend/src/modules/testingWorkspace/SampleCoaPage.tsx] (cited in core screens doc section 5.2).

---

## 9. Endpoints called (HTTP verb and path)

### 9.1 TestWorkflowService (frontend/src/modules/testingWorkspace/services/TestWorkflowService.ts)
- GET /test-workflow/{testOrderId}/current-step [:12]
- GET /test-workflow/{testOrderId}/sibling-pathogen-orders [:15]
- GET /test-workflow/{testOrderId}/eligible-incubators/{stepMediaId} [:18]
- GET /test-workflow/{testOrderId}/permitted-confirmatory-media?stepName= [:21]
- POST /test-workflow/{testOrderId}/select-media [:26]
- POST /test-workflow/{testOrderId}/start-stage-2-incubation [:29]
- POST /test-workflow/{testOrderId}/record-result (count; body rawPlateReadings, dilutionFactor, optional dilutionFactorOverrideNote) [:34, 37]
- POST /test-workflow/{testOrderId}/record-measurement-result [:53]
- POST /test-workflow/{testOrderId}/record-gravimetric-result [:74]
- GET /test-workflow/{testOrderId}/titration-context [:78]; POST record-titration-result [:81]
- POST /test-workflow/{testOrderId}/record-qualitative-result [:98]
- POST /test-workflow/{testOrderId}/record-dissolution-result [:114]; record-dissolution-stage [:125]
- POST /test-workflow/{testOrderId}/record-disintegration-result [:139]; record-disintegration-stage [:150]
- POST /test-workflow/{testOrderId}/record-weight-variation-result [:168]; record-weight-variation-stage [:183]
- GET /test-workflow/{testOrderId}/locations [:186]
- POST /test-workflow/{testOrderId}/close-incubation-window [:189]
- POST /test-workflow/{testOrderId}/override-minimum-duration [:194]
- POST /test-workflow/{testOrderId}/batch-pathogen-results [:200]
- POST /test-workflow/{testOrderId}/batch-results [:206]
- POST /test-workflow/{testOrderId}/water-batch-readings [:209]
- POST /test-workflow/{testOrderId}/submit-broth [:216]
- POST /test-workflow/{testOrderId}/start-selective-plating-incubation [:223]
- POST /test-workflow/{testOrderId}/submit-selective-plating-observation [:230]
- POST /test-workflow/{testOrderId}/submit-selective-plating (deprecated) [:239]
- POST /test-workflow/{testOrderId}/submit-confirmatory-setup [:248]
- POST /test-workflow/{testOrderId}/submit-confirmatory-observations [:256]
- POST /test-workflow/{testOrderId}/analyst-decision [:260]
- POST /test-workflow/{testOrderId}/submit-biochemical [:263]
- POST /test-workflow/results/{workflowStepResultId}/biochemical-decision [:270] (no UI caller)
- GET /test-workflow/actionable-groups [:275]
- POST /test-workflow/batch-select-media [:278]

### 9.2 PathogenSessionService (frontend/src/modules/testingWorkspace/services/PathogenSessionService.ts)
- GET /pathogen-session/{sampleId} [:15]
- POST /pathogen-session/{sampleId}/start-tsb [:18]
- POST /pathogen-session/{sampleId}/save-primary-observations [:21]
- GET /pathogen-session/{sampleId}/eligible-confirmations[?testOrderId=] [:25-26]
- POST /pathogen-session/{sampleId}/start-confirmatory-setup [:31]
- POST /pathogen-session/{sampleId}/save-confirmatory-readings [:34]
- POST /pathogen-session/{sampleId}/save-matrix [:37]
- POST /pathogen-session/{sampleId}/complete [:40]
- POST /pathogen-session/{sampleId}/reset {reason} [:43]

### 9.3 SampleSummaryService and ArchivedRecordsService
- GET /samples/{id}/summary [SampleSummaryService.ts:25]
- GET /samples/{id}/summary/pdf (blob) [:28]; GET /samples/{id}/summary/word (blob) [:31; no caller]
- POST /samples/{id}/review/complete {password, comment, sectionId} [:34]
- POST /samples/{id}/review/return-test {testOrderId, reason} [:42]
- POST /samples/{id}/approval/decide {password, decision, comment, certificateRemarks, selectedTestOrderIds, newSampleAnalystOneId, newSampleAnalystTwoId, sectionId} [:59]
- POST /samples/{id}/approval/sections/{sectionId}/close {password, reason} [:69]
- GET /archived-records?entityType&entityId [ArchivedRecordsService.ts:22]; GET /archived-records/{id}/download (blob) [:26]

### 9.4 testPreparation services
- GET /items/{id}/preparation-configuration; PUT same; POST .../approve; GET /preparation-configurations/pending [frontend/src/modules/testPreparation/services/ItemPreparationConfigurationService.ts:39, 42, 45, 48]
- POST /sample-preparation [frontend/src/modules/testPreparation/services/SamplePreparationService.ts:74]
- POST /sample-preparation/confirm [:78]
- GET /sample-preparation/groups?sampleIds= [:84]
- POST /sample-preparation/batch-confirm [:92]

### 9.5 Preparation forms in laboratoryConfiguration
- EM: GET /masterdata/rooms; GET /masterdata/room-test-configurations?roomId= [frontend/src/modules/laboratoryConfiguration/environmentalMonitoring/services/EMPreparationService.ts:5, 7]; POST /em/prepare [:9].
- After cleaning: GET /masterdata/machines (parts); GET /masterdata/machine-part-configurations?machinePartId= [frontend/src/modules/laboratoryConfiguration/afterCleaning/services/AfterCleaningPreparationService.ts:5, 7]; POST /aftercleaning/prepare [:9].
- Water: GET /masterdata/water-departments [frontend/src/modules/laboratoryConfiguration/water/services/WaterPreparationService.ts:10]; POST /water/prepare [:21]; sampling-point list endpoint not read (UNCLEAR).

### 9.6 Receiving-module calls made by the workspace shell
- GET /testorders/page [frontend/src/modules/receiving/services/ReceiveService.ts:92] (via getRecordsPaged at :75)
- GET /testorders/counts [:100] (getWorkloadCounts at :96)
- GET /testorders/{sampleId} [:115] (getSample at :111)
- PUT /samples/{id}/correct [:123]; PUT /samples/{id}/assign-analyst [:128]; POST /samples/{id}/void [:133].

### 9.7 Lookups (read only as call sites)
- lookupCache.getReleasedMedia and getEquipment("Incubator") [frontend/src/services/lookupCache.ts:31, 62]; masterDataOptions.getReleasedMedia and getEquipment [frontend/src/services/masterDataOptions.ts:217, 219]. Underlying URLs not read beyond the call sites (UNCLEAR).

---

## 10. Mock, placeholder, hardcoded and fallback values

- Search of the scope for mock, placeholder data, dummy, fake, TODO and FIXME found no mock records. Matches were comments or UI placeholders only [git grep result over frontend/src/modules/testingWorkspace, testPreparation, receivingTesting].
- Hardcoded or fallback values that affect displayed or submitted data:
  - ConfirmatoryPlatingPanel.tsx:153 - end-of-incubation fallback of 24 hours when `step.incubationMaxHours` is 0 (sent as incubationEndUtc) [frontend/src/modules/testingWorkspace/pathogenSteps/ConfirmatoryPlatingPanel.tsx:153-160].
  - BatchConfirmatoryPlatingPanel.tsx:389 - materialId falls back to the lot id, else to the literal 1 [frontend/src/modules/testingWorkspace/pathogenSession/BatchConfirmatoryPlatingPanel.tsx:389].
  - PrimaryObservationMatrixPanel.tsx:524 and BatchConfirmatoryPlatingPanel.tsx:161 - confirmatory medium count defaults to 2 when the test code contains "SALMONELLA", else 1 [src: frontend/src/modules/testingWorkspace/pathogenSession/PrimaryObservationMatrixPanel.tsx:524; BatchConfirmatoryPlatingPanel.tsx:161].
  - PrimaryObservationMatrixPanel.tsx:77-79, 155-157, 525-527, 640-642 - quantitative vs qualitative column by code/workflow text match ("count", "tamc", "tymc").
  - SharedTsbEnrichmentPanel.tsx:53-62 - default TSB lot chosen by name text heuristic ("TSB", "Tryptic", "Broth").
  - BrothStepPanel.tsx:118-155 - fallback display values "Standard Lot", "Standard Incubator", startedBy "Analyst" (line 123) when the server summary is absent.
  - SessionReviewPanel.tsx:106 - medium name fallback "Selective Agar"; 115 and 159 - "Analyst" fallback for entered-by name.
  - SharedTsbStatusCard.tsx:136 - GPT status fallback "GPT Conform"; 161 - "Analyst".
  - DownstreamWorkflowsPanel.tsx:245 - target medium fallback "Standard Culture Media".
  - PathogenSessionDialog.tsx:115 - reset reason fallback "Section Head requested session reset".
  - TestWorkflowDialog.tsx:237 - unit fallback "CFU/mL"; TestWorkflowDialog.tsx:83 and 279 - direct-count dilution fixed to "1"; TestWorkflowDialog.tsx:237-258 - live result formatting.
  - Shared TSB and report defaults for "No remarks." (SampleCoaPage.tsx:534, 637).
- Dates: confirmatory start defaults to the browser clock (BatchConfirmatoryPlatingPanel.tsx:194; ConfirmatoryPlatingPanel.tsx:152; SharedTsbEnrichmentPanel.tsx:82). Server time is not used on these paths.

---

## 11. Cross-cutting: time gates and signature triggers

### 11.1 Time gates (client-side display plus server enforcement)
- Count workflow (TestWorkflowDialog): awaiting-result actions and enter-result submit disabled until minReadyAt (TestWorkflowDialog.tsx:159-162, 604-618, 803).
- Batch grids (LocationResultGridDialog.tsx:63, 204; WaterLocationResultGridDialog.tsx:49, 187; PathogenLocationResultGridDialog.tsx:32, 112).
- Pathogen panels: phase switches to readout only when time ready or countdown finished (SelectivePlatingPanel.tsx:71-75; ConfirmatoryPlatingPanel.tsx:85-88); broth proceed disabled until ready (BrothWaitingPanel.tsx:50).
- Shared TSB: Start disabled only by lot/incubator presence; no time gate on start (SharedTsbEnrichmentPanel.tsx:318).
- Primary matrix: Proceed disabled while TSB incubating and until all cells filled (PrimaryObservationMatrixPanel.tsx:441).
- Session review: Complete disabled until all required results exist (SessionReviewPanel.tsx:268).
- Grouped preparation gate: incubation actions hidden for samples whose preparation is outstanding (GroupedActionsPanel.tsx:89, 224-245).
- Unconfigured window: blocks readiness, shows INCUBATION_WINDOW_NOT_CONFIGURED_MESSAGE (incubationWindow.ts:3-4, 47-48).
- Skip Wait: supervisor only (TEST_WORKFLOW_SUPERVISE) on TestWorkflowDialog, IncubationWaitingView, and session reset on PathogenSessionDialog.

### 11.2 Signature (SignatureDialog) triggers in this scope
- Preparation: TestPreparationForm "Start Testing" [frontend/src/modules/testPreparation/TestPreparationForm.tsx:98]; ConfirmPreparationForm "Confirm & Start Testing" [frontend/src/modules/testPreparation/ConfirmPreparationForm.tsx:82]; GroupedPreparationRow "Confirm & Start Testing (N)" [frontend/src/modules/testPreparation/components/GroupedPreparationRow.tsx:209].
- Measurement "Sign and record result" [MeasurementPanel.tsx:387]; Gravimetric [GravimetricPanel.tsx:577]; Qualitative [QualitativePanel.tsx:470]; Titration [TitrationPanel.tsx:684]; Dissolution "Sign & Record Stage N Result" [DissolutionPanel.tsx:749]; Disintegration [DisintegrationPanel.tsx:833]; Weight Variation [WeightVariationPanel.tsx:896].
- Summary: review submit [SampleSummaryDialog.tsx:612]; approval decision [:624]; close testing [dialogs/CloseTestingDialog.tsx:133].
- Void: VoidSampleConfirmationDialog [frontend/src/modules/receiving/dialogs/VoidSampleConfirmationDialog.tsx:201].
- No signature (service call directly): count result submit (TestWorkflowDialog.tsx submitResult); all pathogen step submits; batch count and batch pathogen grids; primary matrix save; batch confirmatory save; session complete; session reset (reason only); shared TSB start; return to analyst; wait skip; EM/AC/Water preparation confirm; storage and start incubation actions.

---

## 12. Not read / UNCLEAR

- Not read at source level (pattern search only): MeasurementPanel, GravimetricPanel, QualitativePanel, TitrationPanel, DissolutionPanel, DisintegrationPanel, WeightVariationPanel (labels and buttons from grep; full validation and display not confirmed beyond what is quoted).
- Not read: pathogenSteps/UnsupportedStepPanel.tsx; pathogenSession BatchConfirmatory/Session panels were read; TestResultCards.tsx lines 40-250 and 585-607 (partial); SampleReportPage.tsx body (only grep); SampleCoaPage.tsx body (only headings); SummaryMatrix.tsx, reportPrimitives.tsx, CollapsibleTestCard.tsx, SummaryCards.tsx IncubationStageBlock internals; AnalysisBlocks.tsx internals; icp/IcpReviewPanel.tsx and IcpReportsSection.tsx (grep only); HplcReviewPanel (not in scope); ItemDocumentsCard internals; TitrantDueAcknowledgement warnings source (server-provided: titrant.warning, TitrationPanel.tsx:224-231).
- Not read: hooks useActionableGroups, useTestStepQuickAction, useGroupedPreparation; utils/workflowErrors.ts (error code mapping); utils/incubationWindow beyond the lines quoted; PreparationStepsSummary read; ConfirmationDialog and FloatingDialog internals; StatusBadge mapping beyond lines 7-44 (UNCLEAR for other statuses).
- Not read: receiving SampleRegisterTable, SampleFilterBar, SelectionSummaryBar, TestStatusSummaryCell, NewSampleDialog (existing doc covers NewSampleDialog and EditSampleDetailsDialog labels), AssignAnalystDialog, AuditHistoryDialog.
- Not read: laboratoryConfiguration EM/water/AC config pages (only preparation forms read).
- UNCLEAR: server behaviour for TNTC/Uncountable strings in rawPlateReadings (sent as strings; handling not read).
- UNCLEAR: whether the "Analysis time" default and client-clock start times are overridden by the server.
- UNCLEAR: the SampleKanbanView source comment says a card click opens the Sample Summary dialog, but the page wires card click to selection only (contradiction recorded in section 1.3).
- UNCLEAR: exportWord has no UI caller in frontend/src (checked by grep).
- UNCLEAR: recordBiochemicalDecision has no UI caller (source comment says so, not re-verified beyond the comment).
