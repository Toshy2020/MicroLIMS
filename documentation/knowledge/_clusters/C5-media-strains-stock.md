# C5 Media, strains, stock

## modules

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

## screens
### SCR-MEDIA-001 Media Preparation & Evaluation
- Route `/laboratory-configuration/media`; guard `Media.Prepare` [src: frontend/src/routes/AppRoutes.tsx:L157-L158]. Component `MediaPage` [src: frontend/src/modules/laboratoryConfiguration/media/MediaPage.tsx:L21].
- Title "Media Preparation & Evaluation"; subtitle "Prepare media lots and manage their evaluations (GPT, Sterility, Indication/Inhibition, Enrichment Characteristics)." [src: MediaPage.tsx:L216-L217].
- Tabs: "Media Lots", "Approved Media" [src: MediaPage.tsx:L198-L199].
- Workspace tabs for a selected lot: "Preparation Overview", "Evaluations (N)", "Release & Audit Trail", "Storage" (Storage only if released) [src: frontend/src/modules/laboratoryConfiguration/media/components/SelectedMediaLotWorkspace.tsx:L280-L283].
- Preparation dialog fields (labels, * = required in label): "Total Weight (g) *", "Total Volume *", "Cycle Number *", "pH (at 25°C) *", "Expiry Date *"; read-only "Load Type (Read-Only)", "Temperature (°C) (Read-Only)", "Cycle Time (min) (Read-Only)" [src: frontend/src/modules/laboratoryConfiguration/media/dialogs/MediaPreparationDialog.tsx:L251-L385].
- Storage panel: "Stored as *", "Quantity *" (or "Quantity to move *" for an existing entry), "Reason for move *"; buttons "Move", "Cancel" [src: frontend/src/modules/laboratoryConfiguration/media/components/MediaStoragePanel.tsx:L103, L155, L171, L178, L193].
- Mark out of stock dialog: label "Reason / Comment (Optional)" [src: frontend/src/modules/laboratoryConfiguration/media/dialogs/MarkOutOfStockDialog.tsx:L120].
- Release decision uses SignatureDialog; confirm calls `MediaPreparationService.decideRelease(lot.id, password, approved)` [src: frontend/src/modules/laboratoryConfiguration/media/MediaPage.tsx:L150-L153, L346].
- Lot filter status options (verified): "Pending Evaluation", "Awaiting Approval", "Released", "Rejected", "Out of Stock" [src: frontend/src/modules/laboratoryConfiguration/media/components/MediaLotFilterBar.tsx:L19-L25]. These are screen lifecycle labels computed in the browser, in this order: Out of Stock if status is OutOfStock; Released if released for use; Rejected if approval is Rejected or status is QuarantineFailed; Awaiting Approval if the lot is on the awaiting-approval list; otherwise Pending Evaluation [src: frontend/src/modules/laboratoryConfiguration/media/components/MediaLotKpiCards.tsx:L20-L26]. A lot made QuarantineFailed by a NonConform evaluation therefore shows "Rejected" although its approval status is still PendingReview. An expired lot still shows "Released" (no expiry term in the label).
- Preparation dialog (verified): title "Prepare New Media Lot"; the stock picker lists only dehydrated-media rows whose stock status is InStock, and rows with no media product are disabled; the autoclave picker lists master-data equipment of type Autoclave (no status or calibration filter); choosing an autoclave program fills Load Type, Temperature and Cycle Time [src: frontend/src/modules/laboratoryConfiguration/media/dialogs/MediaPreparationDialog.tsx:L35-L99, L187-L192].
- Release buttons (verified): "Release Lot" and "Reject Lot" appear only when the lot lifecycle is Awaiting Approval AND the signed-in user holds Media.Release; "Mark Out of Stock" appears when the lifecycle is Released [src: frontend/src/modules/laboratoryConfiguration/media/components/SelectedMediaLotWorkspace.tsx:L86, L217-L252]. Success text "Media lot {lot} released for use." / "Media lot {lot} rejected."; failure fallback "Release decision failed." [src: frontend/src/modules/laboratoryConfiguration/media/MediaPage.tsx:L150-L163].
- Mark Out of Stock dialog (verified): title "Mark Media Lot Out of Stock"; warning "Warning: This media lot will no longer be available for laboratory testing."; buttons "Cancel" and "Confirm Out of Stock" ("Processing..."); failure fallback "Failed to mark media lot out of stock." [src: frontend/src/modules/laboratoryConfiguration/media/dialogs/MarkOutOfStockDialog.tsx:L42, L57, L65-L66, L114-L120].
- Empty/error: preparation dialog fallback "Could not prepare media lot. Please verify all inputs." [src: MediaPreparationDialog.tsx:L174].
- Kit: Media lot KPI cards, register table (`MediaLotRegisterTable`), `MediaLotFilterBar`. Visible "Approved Media" tab columns: see Material/Approved lists below.
- Evaluation dialog `MediaEvaluationWorkflowDialog` is opened from the lot's "Evaluations (N)" tab (prop `onOpenEvaluation`) (verified) [src: frontend/src/modules/laboratoryConfiguration/media/components/SelectedMediaLotWorkspace.tsx:L67, L79]. Per challenge: for Growth Promotion, "REFERENCE LOT (FOR RECOVERY% COMPARISON)" (a prior released lot of the same material, or "Reference Lot (manual entry)"); "STEP 1: SELECT WORKING CRYOVIAL OR LYOPHILIZED DISK" (select "Select Cryovial or Disk", button "Assign"; warning "No approved, unexpired cryovial batches or lyophilized disks available for {organism}." when none); "STEP 2: RECORD INCUBATION" (select "Select Incubator", button "Record Incubation"; once recorded it shows temperature, hours and "Ready to read" or "Earliest reading: {date}"); then the result inputs and button "Record Result" [src: frontend/src/modules/laboratoryConfiguration/media/dialogs/MediaEvaluationWorkflowDialog.tsx:L274-L440]. Result inputs: Growth Promotion "Old Media Count" and "New Media Count" with a live recovery % preview; Inhibition "Growth Observed?" (Yes/No); Indication "Observed Description" and "Judgment" (Conform/NonConform); Enrichment "Turbid or Clear?" (Turbid/Clear) [src: MediaEvaluationWorkflowDialog.tsx:L477-L562]. Cryovial options offered: approved, not destroyed, expiry after now, same organism; disk options: same organism, quantity above zero, no expiry or expiry after now [src: MediaEvaluationWorkflowDialog.tsx:L113-L128]. The incubator list is all master-data Incubator equipment (no status, set point or calibration filter in the list) [src: MediaEvaluationWorkflowDialog.tsx:L70; frontend/src/services/masterDataOptions.ts:L217-L218]. The dialog has no client-side validation messages of its own: failures show the server message or the fallbacks "Could not select organism source.", "Could not record incubation.", "Could not record result."; success messages "Organism source selected.", "Incubation recorded.", "Result recorded successfully." (verified) [src: MediaEvaluationWorkflowDialog.tsx:L134-L182].

### SCR-MEDIA-002 Media lot report
- Route `/media/:id/report`; no PermissionRoute in the router [src: frontend/src/routes/AppRoutes.tsx:L104]; the data call `GET /api/media/{id}/summary` sits under the class policy Media.Prepare, so a user without it gets no data (verified) [src: backend/MicroLIMS.API/Controllers/MediaController.cs:L24, L113]. Component `MediaReportPage` [src: frontend/src/modules/laboratoryConfiguration/media/MediaReportPage.tsx:L31]. Endpoint GET `/media/{id}/summary` [src: MediaReportPage.tsx:L40]. Shows signature section (display only) [src: MediaReportPage.tsx:L187].

### SCR-MEDIA-003 Media Evaluation (routed, no in-app link)
- Route `/laboratory-configuration/media-evaluation`; guard `TestWorkflow.Execute` [src: frontend/src/routes/AppRoutes.tsx:L160-L161]. Title "Media Evaluation"; subtitle "Growth Promotion, Indication/Inhibition, and Enrichment Characteristics - auto-assigned on media preparation." [src: frontend/src/modules/laboratoryConfiguration/mediaEvaluation/MediaEvaluationPage.tsx:L125].
- Status filter constant `["Assigned", "InProgress", "Completed"]` [src: MediaEvaluationPage.tsx:L20]. No menu item found (GAP-034).
- Dialog `MediaEvaluationWorkflowDialog` [src: frontend/src/modules/laboratoryConfiguration/media/dialogs/MediaEvaluationWorkflowDialog.tsx:L41]. Labels: "Reference Lot (manual entry)" [:L315], "Old Media Count" [:L481], "New Media Count" [:L488], "Observed Description" [:L521].

### SCR-CRYO-001 Cryovials ("Reference Cryovials")
- Route `/laboratory-configuration/cryovials`; guard `Cryovials.Manage` [src: frontend/src/routes/AppRoutes.tsx:L163-L164]. Component `CryovialsPage` [src: frontend/src/modules/laboratoryConfiguration/cryovials/CryovialsPage.tsx:L25].
- Title "Cryovials"; subtitle "Prepare working cryovial batches from an approved lyophilized microorganism material." [src: CryovialsPage.tsx:L190-L191].
- Tabs: "Cryovials", "Approved Cryovials" [src: CryovialsPage.tsx:L172-L173].
- Row actions (callbacks): approve, thaw, destroy, move storage [src: frontend/src/modules/laboratoryConfiguration/cryovials/components/CryovialReviewTable.tsx:L112].
- Prepare dialog labels: "Lyophilized Microorganism Stock (Inventory) *" [:L232], "Vials Prepared *" [:L298], "Discs Used *" [:L310], "Expiry Date *" [:L323], "Storage Equipment *" [:L335], "Physical Check Notes / Discrepancies (optional)" [:L372] in `frontend/src/modules/laboratoryConfiguration/cryovials/components/PrepareCryovialBatchDialog.tsx`.
- Destroy dialog button "Confirm Destruction" (progress "Destroying...") [src: frontend/src/modules/laboratoryConfiguration/cryovials/components/DestroyCryovialDialog.tsx:L89].
- Move storage dialog title "Change Storage Location"; label "Reason for move *" [src: frontend/src/modules/laboratoryConfiguration/cryovials/components/MoveCryovialStorageDialog.tsx:L14, L51, L72].
- Approve/reject uses SignatureDialog [src: frontend/src/modules/laboratoryConfiguration/cryovials/CryovialsPage.tsx:L131-L133, L266].
- Row actions (verified): while the batch is PendingReview and not destroyed: "Approve batch", "Reject batch"; otherwise "Thaw vial" (Approved, not destroyed, expiry date after now in the browser, vials remaining above zero), "Change storage location" (Approved and not destroyed), "Destroy (decommission batch)" (any non-destroyed batch); always "View laboratory report record". These actions are NOT hidden by the Cryovials.Approve permission: a user with Cryovials.Manage but not Cryovials.Approve sees "Approve batch" and receives the server refusal [src: frontend/src/modules/laboratoryConfiguration/cryovials/components/CryovialReviewTable.tsx:L112-L137; backend/MicroLIMS.API/Controllers/CryovialController.cs:L49-L50].
- Thaw dialog (verified): title "Thaw Vial: Provide Reason"; shows the batch code, organism and vials left; text box placeholder "Enter reason for thawing this cryovial..." (reason required, 5 to 500 characters after trimming; the button is disabled until valid); buttons "Cancel" and "Confirm Thaw" ("Thawing..."); failure fallback "Failed to thaw cryovial."; success message "Vial from {code} successfully thawed and logged." The reason travels as the thaw event Notes [src: frontend/src/modules/laboratoryConfiguration/cryovials/components/ThawVialReasonDialog.tsx:L45, L54, L67-L100, L156-L160; frontend/src/modules/laboratoryConfiguration/cryovials/CryovialsPage.tsx:L145-L149].
- Destroy dialog (verified): title "Confirm Batch Destruction"; warning "Destroying this cryovial batch will decommission it permanently. It will no longer be available for thawing, Media Evaluation, or GPT testing."; no reason field and no password; success message "Batch {code} has been decommissioned and destroyed." [src: frontend/src/modules/laboratoryConfiguration/cryovials/components/DestroyCryovialDialog.tsx:L89-L99; CryovialsPage.tsx:L152-L160].
- Approve/reject signature text (verified): "I am approving Cryovial Batch {code} for laboratory testing and media evaluation." / "I am rejecting and decommissioning Cryovial Batch {code}."; dialog has an optional comment box [src: CryovialsPage.tsx:L258-L270].
- Prepare dialog pickers (verified): materials = Lyophilized Microorganism stock with status InStock; media = released media list (server lists released, Active, not expired lots); incubators = master-data Incubator equipment; "Storage Equipment" = inventory equipment whose instrument type contains "freezer" and whose status is InService; the "Save Batch" button is disabled while no such freezer exists [src: frontend/src/modules/laboratoryConfiguration/cryovials/components/PrepareCryovialBatchDialog.tsx:L78-L102, L209]. The dialog sends Storage Equipment only as text "{instrument type}, {manufacturer} ({code})" in `storageCondition`; no storage equipment id is sent at preparation [src: PrepareCryovialBatchDialog.tsx:L141-L160]. Success message "Cryovial batch prepared successfully ({code})."; fallback "Could not prepare cryovial batch."
- Move storage dialog (verified): reason needs at least 5 characters (helper "Reason must be at least 5 characters." / "Recorded in the audit trail."), limited to 500; fallback "Failed to change storage location."; success "Storage location of cryovial batch {code} changed." [src: frontend/src/modules/laboratoryConfiguration/cryovials/components/MoveCryovialStorageDialog.tsx:L60-L100]. The location picker lists inventory equipment in the Microbiology section that is cold storage and InService [src: frontend/src/components/StorageLocationPicker.tsx:L29, L53-L54].

### SCR-CRYO-002 Cryovial report
- Route `/cryovials/:id/report`; no PermissionRoute [src: frontend/src/routes/AppRoutes.tsx:L105]. Component `CryovialReportPage` [src: frontend/src/modules/laboratoryConfiguration/cryovials/CryovialReportPage.tsx:L27]; endpoint GET `/cryovials/{id}/summary` [:L36]; signature display [:L179].

### SCR-MATERIAL-001 Materials Stock
- Route `/inventory/materials`; guard `Materials.Manage` [src: frontend/src/routes/AppRoutes.tsx:L254-L255]. Component `MaterialsPage` [src: frontend/src/modules/inventory/materials/MaterialsPage.tsx:L53]. Page title not found by grep (UNCLEAR).
- Table columns: "Type", "Material Name", "Manufacturer", "Batch/Lot No.", "Received", "Expiry", "Code", "Location", "Qty Received" [src: MaterialsPage.tsx:L238-L292]. Status chip "Not linked to a media product" [:L251].
- Dialogs: AddMaterialDialog, MaterialLotDetailsDialog, UploadMaterialDocumentDialog (button "Cancel"; "Please select a file." [src: frontend/src/modules/inventory/materials/components/UploadMaterialDocumentDialog.tsx:L59]), SupersedeMaterialDocumentDialog, VoidMaterialDocumentDialog ("A reason is required." helper, :L82; "Void operation failed. Please try again." :L31) [src: frontend/src/modules/inventory/materials/components/ as cited in documentation/knowledge/_raw/frontend-config-physchem-dc-screens.md:L267].
- Pick-list helper "Media not listed? A Section Head adds it in Laboratory Configuration > Media Configuration." [src: frontend/src/modules/inventory/materials/components/AddMaterialDialog.tsx:L830].
- AddMaterialDialog (verified): title "Add Material to Stock" / "Edit Material"; buttons "Cancel", "Add to Stock" / "Save Changes" ("Saving..."). Fields: "Material Type" (helper "Pick a type or type a new one"); for dehydrated media a media-product picker; for Chemical / Indicator / Reference or Primary standard a "{Reagent|Indicator|Primary Standard|Reference Standard} Master Entry" picker (required on new lots; helper "Required. Master entry sets the name, code, and base unit."); "Laboratory Section" (required, shown when the user has more than one section); "Material Name" (required); "Manufacturer"; "Media code" (dehydrated media) or "Code / Catalog No."; for Lyophilized Microorganism an organism picker and "ATCC No."; for reference/primary standards "Purity (%)" (helper "Purity percentage (0 < p ≤ 100)") and, for reference standards, "Moisture Content (%)" (helper "Moisture percentage (0 ≤ m < 100, optional)"); "Batch / Lot No." (required); "Storage Location" (required); "Quantity Received" (required); "Unit"; "Min. Stock Level (optional)"; "Receiving Date" (required); "Expiry Date" (not marked required). When editing, a note says changing Quantity Received adjusts Quantity Remaining by the difference [src: frontend/src/modules/inventory/materials/components/AddMaterialDialog.tsx:L690-L711, L809-L1275]. The Lyophilized Microorganism organism is optional in this dialog (saved as null if empty) but cryovial preparation refuses a material without one [src: AddMaterialDialog.tsx:L652; backend/MicroLIMS.Application/Services/CryovialService.cs:L65-L66].
- Document upload (verified server text): allowed file types PDF, JPG, JPEG, PNG, WEBP, TIFF; refusal messages "File exceeds the maximum allowed size of {n} MB.", "The uploaded file is empty.", "File type '{ext}' is not supported. Allowed types: PDF, JPG, JPEG, PNG, WEBP, TIFF.", "The declared content type '{type}' does not match the file extension '{ext}'.", "The file content could not be validated because it is too short.", "The file does not appear to be a valid WEBP image.", "The file content does not match the expected format for '{ext}'. The file may be corrupted or misnamed." [src: backend/MicroLIMS.Application/Services/MaterialDocumentFileValidator.cs:L16-L33, L44-L80]. Supersede and void on materials and equipment take a reason and a file but no password (see roles).
- Approved lists (not separately assigned SCR id in extraction): the old routes `/inventory/approved-media` and `/inventory/approved-cryovials` now redirect to the "Approved Media" / "Approved Cryovials" tabs of the Media and Cryovials pages (verified) [src: frontend/src/routes/AppRoutes.tsx:L261-L262]. The media list (tab "Approved Media List", columns "Lot Number", "Media Type", "Prepared By", "Total Volume", "Total Weight", "Manufacturer Lot", "Manufacturer", "Prepared On", "Expiry", "Status"; KPIs "Total Media Lots", "In Stock", "Expiring Soon", "Media Formulations") [src: frontend/src/modules/inventory/approvedLists/ApprovedMediaListPage.tsx:L294-L351; documentation/knowledge/_raw/frontend-config-physchem-dc-screens.md:L294].

### SCR-EQUIP-001 Equipment Inventory ("Equipment")
- Route `/inventory/equipment`; guard `Equipment.Manage` [src: frontend/src/routes/AppRoutes.tsx:L257-L258]. Component `EquipmentInventoryPage` [src: frontend/src/modules/inventory/equipment/EquipmentInventoryPage.tsx:L41]. Title "Equipment"; subtitle "QC/Microbiology lab instrument register & active equipment traceability." [:L234-L235].
- Tabs: "Equipment Register", "Active Equipment" [:L249-L250].
- Register dialog `RegisterEquipmentDialog` labels: "Instrument Type", "Manufacturer", "Equipment Code", "Location", "Serial Number", "Firmware Version", "Laboratory", "Calibration Due Date", "Operational Status", "Status Change Comment *" (helper "Required whenever the operational status changes.") [src: frontend/src/modules/inventory/equipment/components/RegisterEquipmentDialog.tsx:L189-L310].
- Register dialog (verified): title "Register Equipment" / "Edit Equipment"; buttons "Cancel", "Register Equipment" / "Save Changes" ("Saving..."); the save button is disabled until a Laboratory is chosen and, when the Operational Status was changed, until the Status Change Comment is non-blank [src: frontend/src/modules/inventory/equipment/components/RegisterEquipmentDialog.tsx:L146-L171]. Register columns: "Type", "Manufacturer", "Serial No.", "Firmware", "Code", "Location", "Calibration Due" (badges "Overdue" and "Due Soon" within 30 days), "Laboratory", "Status"; row actions "Details and documents", "Edit equipment", "Audit history" (shown only to users allowed to see history) [src: frontend/src/modules/inventory/equipment/EquipmentInventoryPage.tsx:L170-L210, L294-L296]. The calibration date on this register is the inventory copy; test-time incubator checks read the separate master-data Equipment record (see roles/business-rules).
- Other dialogs: EquipmentDetailsDialog, UploadEquipmentDocumentDialog, SupersedeEquipmentDocumentDialog, VoidEquipmentDocumentDialog; document list error "Failed to load calibration certificates." [src: documentation/knowledge/_raw/frontend-config-physchem-dc-screens.md:L289].

## roles
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

## workflows
All steps below are reconstructed from code reading; the application has not been run. Where the code does not show what the user sees or does next, the step says REQUIRES CONFIRMATION. Evidence labels: IMPLEMENTED unless stated. The only specification for the media area is `documents/DesignSpecification/BusinessRules.md` section 7 (GPT) and section 12 (expiry notice), which pre-date the code (GAP-021); there is no specification for cryovials, Materials Stock or Equipment Inventory, so their INTENDED workflow is REQUIRES CONFIRMATION. The lab-separation design says media configurations, organisms, cryovials and environmental monitoring are Microbiology-area features (INTENDED, micro only) [src: docs/superpowers/specs/2026-09-24-lab-separation-design.md:L105]; the Media and Cryovials routes do not themselves read a lab parameter.

### WF-MEDIA-001 Prepare media lot
- Start / trigger: a user with Media.Prepare opens "Media Preparation & Evaluation" (route `/laboratory-configuration/media`) and presses "Prepare New Media Lot". (verified) [src: frontend/src/routes/AppRoutes.tsx:L157-L158; frontend/src/modules/laboratoryConfiguration/media/MediaPage.tsx:L220]
- Who: default holders of Media.Prepare are Section Head, Reviewer, Analyst (System Administrator holds all). The stock list needs Materials.Manage (Section Head, Analyst), so a Reviewer's picker would fail to load (GAP-514).
- Prerequisites: (1) a dehydrated-media stock row in Inventory > Materials Stock that is InStock, linked to a media product, with a Current COA, not expired and enough quantity; (2) exactly one Media Configuration for that product (Laboratory Configuration); (3) an Autoclave in master-data equipment with at least one autoclave program.
- Steps:
  1. User opens the dialog "Prepare New Media Lot". System loads autoclaves and dehydrated media stock; a failed list shows a load-failure alert. Stock rows with no media product are disabled. [src: MediaPreparationDialog.tsx:L35-L48]
  2. User picks the stock item (required), enters "Total Weight (g) *" and "Total Volume *" (required). Validation: "Please select a Dehydrated Media Stock item from inventory."; "The selected dehydrated media is not linked to a media product. Edit it in Inventory > Materials Stock before preparing."; "Please enter the total weight."; "Please enter the total volume."
  3. User picks the autoclave and an autoclave program / load. System fills Load Type, Temperature and Cycle Time (read-only). Validation: "Please select an Autoclave.", "Please select an Autoclave Program / Load.", "Please select an Autoclave Program with valid Load Type.", "Autoclave temperature is required.", "Autoclave cycle time is required."
  4. User enters "Cycle Number *", "pH (at 25°C) *", "Expiry Date *". Validation: "Please specify the cycle number.", "Please specify the pH.", "Please specify the Expiry Date." The expiry date is typed by the user; no shelf-life rule was found (GAP-509).
  5. User presses "Save Prepared Lot". System (in one save): checks the equipment is an Autoclave ("Selected equipment is not a valid autoclave."); consumes Total Weight from the stock row (refusals: not a dehydrated-media item, "Material {name} (batch {batch}) is expired and cannot be used.", "A current Certificate of Analysis (COA) is required before {name} (batch {batch}) can be used. Please upload a valid COA in the Inventory module first.", "Insufficient stock: ..."); checks the product link and the Media Configuration (messages in validations); creates the lot with number `{Product.Code}/{seq}/{yy}`, copying the manufacturer lot and manufacturer name from the stock row; creates one evaluation with the configuration's evaluation type and one challenge per configured organism. If two users take the same number at once the second save is retried once, then "Lot number {lot} was taken by another preparation at the same moment. Nothing was saved - submit the preparation again." [src: MediaPreparationService.cs:L49-L136]
  6. System response: message "Media lot {lot} successfully prepared." and the new lot is selected. [src: MediaPage.tsx:L165-L170]
- Status change: lot Status = Prepared, approval PendingReview, not released; evaluation Assigned. Screen label "Pending Evaluation".
- Next action: WF-MEDIA-002 (evaluate). A lot with zero configured challenges has an evaluation that cannot complete (BR-MEDIA-007, BR-MEDIA-015).
- Review / approval / final state: none at this step. The preparer is later barred from releasing the lot (WF-MEDIA-003).
- Required data: stock item, total weight, total volume, autoclave, program, cycle number, pH, expiry date. Optional: none. Server-side: no range checks (GAP-515).
- Permissions: Media.Prepare for the endpoint; Materials.Manage for the stock list; no signature.
- Blocking conditions: expired stock row, missing current COA, insufficient stock, no media product link, no Media Configuration, autoclave not of type Autoclave. NOT blocking: autoclave operational status or calibration (no check), lot expiry in the past (no check).
- Signature points: none. Audit: preparer recorded on the lot; general record-change log rows (audit-trail section).
- INTENDED source: REQUIRES CONFIRMATION (no specification covers media preparation). BusinessRules section 7 starts the GPT flow at "Media Preparation" only [src: documents/DesignSpecification/BusinessRules.md:L141].

### WF-MEDIA-002 Evaluate media lot (GPT, Indication/Inhibition, Enrichment)
- Start / trigger: a user with TestWorkflow.Execute opens a lot's "Evaluations (N)" tab and opens the evaluation dialog. The standalone `/laboratory-configuration/media-evaluation` page is routed but has no menu link (GAP-034). (verified)
- Who: TestWorkflow.Execute (Section Head, Reviewer, Analyst); lab section scope applies to every call. The user who reads any challenge result later cannot release the lot. The cryovial and disk pickers need Cryovials.Manage and Materials.Manage (Analyst and Section Head hold both).
- Prerequisites: a prepared lot with an auto-assigned evaluation; per challenge an Approved, unexpired cryovial batch of the same organism and section, or a lyophilized-disk stock row (same organism and section, quantity above zero, not expired, current COA); an incubator; for Growth Promotion a reference lot (a prior released lot of the same material, or free text).
- Steps (per challenge; repeated for each organism):
  1. Step 1 "SELECT WORKING CRYOVIAL OR LYOPHILIZED DISK": user picks from "Select Cryovial or Disk" and presses "Assign". System checks section, approval / destroyed / expiry (cryovial) or type, organism, expiry, quantity (disk) and, for a disk, consumes ONE unit from stock immediately (GAP-511). A cryovial choice does not reduce vials remaining. Refusals: BR-MEDIA-017 and validations. Message "Organism source selected." If nothing is offered: "No approved, unexpired cryovial batches or lyophilized disks available for {organism}."
  2. Step 2 "RECORD INCUBATION": user picks an incubator and presses "Record Incubation". System creates the incubation with temperature and hours taken from the Media Configuration's incubation condition (not typed by the user) and sets the earliest reading time = now + minimum hours. Evaluation Assigned becomes InProgress. Refusal: "Media Configuration for lot \"{lot}\" is missing an incubation condition." No check of the incubator (BR-EQUIP-013), none that Step 1 was done (GAP-518). Message "Incubation recorded."; the dialog shows "Earliest reading: {date}" and later "Ready to read".
  3. Step 3 "RECORD OBSERVATION / RESULT": before the earliest reading time the dialog shows "Incubation still in progress; result entry opens at {time}." (or "Record incubation before a result can be entered."). When ready the user enters, by type: Growth Promotion: Old Media Count, New Media Count, reference lot (a prior lot, or manual text; exactly one) ; Inhibition challenge: "Growth Observed?"; Indication challenge: "Observed Description" and "Judgment"; Enrichment: "Turbid or Clear?". User presses "Record Result".
  4. System validates and computes (messages in validations): result before reading time is refused ("Incubation is still in progress - earliest reading time is {yyyy-MM-dd HH:mm} UTC."); Growth Promotion recovery % = round(new / old x 100, 1) and Conform when inside the configured band (BR-MEDIA-010); Inhibition Conform when no growth; Indication Conform / NonConform as judged by the user (the observed text is not compared with the expected description); Enrichment Conform when turbid. The reader and time are recorded; the challenge's incubation is closed.
  5. When every challenge has an outcome, the evaluation becomes Completed with outcome Conform only if all challenges are Conform; if NonConform the lot becomes QuarantineFailed immediately (screen label "Rejected"). A Conform evaluation does not release the lot.
- Status change: evaluation Assigned -> InProgress -> Completed; lot unchanged on Conform (PendingReview, screen label "Awaiting Approval" once the lot is on the awaiting list); lot -> QuarantineFailed on NonConform.
- Next action: Conform -> WF-MEDIA-003. NonConform -> no further step in code (no re-test or re-prepare action found); REQUIRES CONFIRMATION what the lab does next (GAP-504).
- Required data: see step 3 by type. Optional: none.
- Permissions: TestWorkflow.Execute for all four evaluation endpoints; section scope. No Media.Prepare or Media.Release needed.
- Blocking conditions: incubation window not elapsed (time gate, set from configuration), expired or unapproved cryovial, wrong organism, other section, depleted or expired disk, missing COA on disk stock, missing incubation condition in configuration. NOT blocking: incubator status / calibration / set point; lot expiry.
- Signature points: none (no password in evaluation). The reader and time are recorded on each challenge.
- Correction after completion: the server has no lock on re-entry (GAP-504); the dialog shows "EVALUATION RESULT RECORDED" and no further inputs.
- INTENDED source: BusinessRules section 7 (GPT stages, "passes only if every recorded challenge organism passed") — DISCREPANCY with code, see BR-MEDIA-031 and GAP-021 / GAP-022 (no sterility step in code although the screen subtitle and menu text mention it).

### WF-MEDIA-003 Release or reject media lot
- Start / trigger: a user holding Media.Release selects a lot whose screen label is "Awaiting Approval" (evaluation Completed and Conform, approval PendingReview) and presses "Release Lot" or "Reject Lot". (verified) [src: SelectedMediaLotWorkspace.tsx:L217-L240]
- Who: Media.Release (default: Section Head; System Administrator). Segregation: the person who prepared the lot or read any challenge result cannot decide it.
- Prerequisites: lot approval PendingReview; for release, evaluation exists, is Completed and Conform.
- Steps:
  1. User presses "Release Lot" (approve) or "Reject Lot". System opens the signature dialog (password) with the statement "I am releasing media lot {lot} for use in routine testing." or "I am rejecting media lot {lot}." (corrected in Phase 5) [src: frontend/src/modules/laboratoryConfiguration/media/MediaPage.tsx:L344-L356].
  2. User enters the password and confirms. System checks in this order: lot not already decided ("Media lot {lot} has already been decided ({status})."); evaluation exists ("Media lot {lot} has no evaluation record."); if releasing, evaluation Completed ("Media lot {lot}'s evaluation is not complete yet.") and Conform ("Media lot {lot}'s evaluation did not conform - it cannot be released."); segregation of duties ("You cannot approve a media lot you prepared or evaluated."); then verifies the password and records the signature (Approved or Rejected), the decision event and the comment, and the state change in one save. A failed password stops everything; repeated failures are throttled and logged. [src: MediaReleaseService.cs:L59-L113; ElectronicSignatureService.cs:L50-L57]
  3. Release: approval Approved, `IsReleasedForUse` true, Status Active, approver and time set. Reject: approval Rejected, Status QuarantineFailed. Rejection is allowed whatever the evaluation state.
  4. System archives an immutable PDF of the lot record ("Media lot released" / "Media lot rejected"). Screen message "Media lot {lot} released for use." / "rejected."; failure fallback "Release decision failed."
- Status change: PendingReview -> Approved (Active, label "Released") or -> Rejected (QuarantineFailed, label "Rejected"). Decision is final; there is no un-release.
- Next action: released lot -> storage and use in tests (WF-MEDIA-004); a QuarantineFailed lot is not usable.
- Review / approval: single step, single signer (no separate review).
- Required data: password; comment optional (server accepts none).
- Expiry: release does not check the lot expiry date (GAP-506); no check that the lot is not already QuarantineFailed from a NonConform evaluation (a re-entered Conform result could place such a lot back on the awaiting list; GAP-504).
- Permissions: Media.Release at the endpoint; the release buttons are hidden without it. Section access check on the lot.
- Signature points: the release / reject decision (password, meaning Approved or Rejected).
- Use after release: lot lists in test screens contain only released, Active, unexpired lots; individual test selection refuses non-released, OutOfStock and QuarantineFailed lots but does not re-check expiry; grouped selection and shared-TSB start also check expiry (BR-MEDIA-024). The cryovial identity panel checks release and status on the server, not expiry.
- INTENDED source: BusinessRules section 7 says a lot is released when GPT passes (`GptStage == Release`); the signed Section Head decision and SoD rule are not in any specification. DISCREPANCY (BR-MEDIA-031). Whether QA, not only a Section Head, must approve: REQUIRES CONFIRMATION (GAP-513).

### WF-MEDIA-004 Store a released lot, move storage, mark out of stock
- Start / trigger: a user with Media.Prepare opens a released lot. The "Storage" tab exists only when the lot is released; "Mark Out of Stock" shows when the label is "Released". (verified) [src: SelectedMediaLotWorkspace.tsx:L242-L251, L283]
- Steps (store):
  1. User presses "Add storage" (visible only while the lot is released and Active; otherwise the panel says "Storage can be recorded once the lot is released and in stock."). Dialog "Add Storage": "Stored as *" (Plates, Agar bottles, Broth bottles, Tubes), "Quantity *", "Storage equipment *" (cold-storage inventory equipment of the Microbiology section that is InService) or "Storage area *" (free text); button "Add".
  2. System checks form, quantity above zero, exactly one of equipment or text, lot released and Active, equipment InService (messages in validations). An entry with the same lot, form and place is merged by adding the quantity. A review-trail event "Added {qty} {form} to storage: {place}." is written. Expiry is not checked.
- Steps (move):
  1. User presses "Move" on a row: dialog "Move Storage": "Quantity to move *", new place, "Reason for move *" (at least 5 characters in the dialog), button "Move".
  2. System checks reason, quantity within the entry, destination different from source, destination equipment InService; partial or full move; writes a review-trail event with the reason.
- Steps (out of stock):
  1. User presses "Mark Out of Stock", sees "Warning: This media lot will no longer be available for laboratory testing.", optionally types "Reason / Comment (Optional)", presses "Confirm Out of Stock". No password.
  2. System refuses unless the lot is released and Active ("Media lot {lot} is not currently released for use and cannot be marked Out of Stock."); sets Status OutOfStock and writes the review-trail event with the comment (default "Media lot manually marked Out of Stock."). Individual selection, grouped selection and the cryovial identity panel then refuse the lot.
- Status change: Active -> OutOfStock (label "Out of Stock"). No reverse step in code (GAP-512).
- Permissions: Media.Prepare (storage and out-of-stock endpoints rely on the class policy). Section access check. No signature on any of these.
- Required vs optional: storage quantity, form and one place required; move reason required; out-of-stock comment optional.
- Remaining quantity is not tracked on the lot (GAP-510); expiry does not change status (GAP-502); the expiring list, the dashboard "Media Expiry" notice and the lot's expiry date are how expiry is surfaced (Active lots, 7 days).
- INTENDED source: BusinessRules section 12 for the expiry notice only (BR-MEDIA-032); storage and out-of-stock are REQUIRES CONFIRMATION.

### WF-CRYO-001 Prepare cryovial batch
- Start / trigger: a user with Cryovials.Manage opens "Cryovials" (title "Reference Cryovials" in the menu) and presses "Prepare Cryovial Batch". (verified) [src: CryovialsPage.tsx:L193-L194]
- Who: Cryovials.Manage (Section Head, Reviewer, Analyst, System Administrator). The dialog needs Materials.Manage and Equipment.Manage for its lists, so a Reviewer's lists would fail to load (GAP-514).
- Prerequisites: an InService Lyophilized Microorganism stock row with an organism set, a current COA, not expired and enough discs; at least one released, Active, unexpired media lot of the same section for identity confirmation; an incubator; an InService freezer in the inventory (otherwise "Save Batch" is disabled).
- Steps:
  1. User opens "Prepare Cryovial Batch"; picks "Lyophilized Microorganism Stock (Inventory) *", enters "Vials Prepared *", "Discs Used *", "Expiry Date *", picks "Storage Equipment *", ticks the physical-check confirmation and may add "Physical Check Notes / Discrepancies (optional)".
  2. User fills at least one Identity Confirmation row: GPT-released media, incubator, incubation start and end, optional observation text.
  3. Client validation, in order: "Please select a Lyophilized Microorganism material."; "Please enter a valid number of vials prepared (> 0)."; "Please enter the number of discs used (0 or more)."; "Please enter an expiry date."; "Please select a Storage Equipment (Freezer / Deep Freezer)."; "Please confirm the physical check against the reference description."; "At least one Identity Confirmation row is required."; "Row {n}: Please select a GPT-released media." / "...an incubator." / "...the incubation start date." / "...the incubation end date." [src: PrepareCryovialBatchDialog.tsx:L111-L129]
  4. User presses "Save Batch" ("Saving Batch..."). System checks: physical check confirmed; the material is a Lyophilized Microorganism with an organism; each panel media lot released and not OutOfStock / QuarantineFailed, and of the material's section; at least one row; then consumes Discs Used from stock (expiry, COA, stock checks as for media); creates the batch with code `{material code}/{seq}/{yy}`, vials remaining = vials prepared, storage text from the chosen freezer, PendingReview. Duplicate code at the same moment is retried once. Message "Cryovial batch prepared successfully ({code})." Fallback "Could not prepare cryovial batch."
- Status change: new batch ApprovalStatus PendingReview. Not usable as a challenge source until approved.
- Next action: WF-CRYO-002 by a different user.
- Required data: material, vials, discs (0 allowed), expiry, freezer, physical-check confirmation, one complete identity row. Optional: physical-check notes, observation text.
- Blocking conditions: expired, no COA or too little stock on the source stock; unreleased / out-of-stock / quarantined identity media. Not blocking: expired identity media on the server; incubator status or dates; the freezer choice on the server (GAP-503); organism passage / generation number (not recorded; GAP-037).
- Signature points: none at preparation; preparer recorded.
- INTENDED source: REQUIRES CONFIRMATION (no cryovial specification). Which tests count as "identity confirmation" and what the observation must contain: REQUIRES CONFIRMATION.

### WF-CRYO-002 Approve or reject cryovial batch
- Start / trigger: on the Cryovials register a batch in PendingReview (not destroyed) offers "Approve batch" and "Reject batch". (verified) [src: CryovialReviewTable.tsx:L117-L119]
- Who: Cryovials.Approve (Section Head; System Administrator). The row actions are shown to every Cryovials.Manage holder; the server refuses others. Preparer cannot decide.
- Steps:
  1. User presses the action; system opens the signature dialog with the statement "I am approving Cryovial Batch {code} for laboratory testing and media evaluation." or "I am rejecting and decommissioning Cryovial Batch {code}." and an optional comment.
  2. User enters the password and confirms. System checks the batch is still PendingReview ("Cryovial batch {code} has already been decided ({status})."), the user did not prepare it ("You cannot approve a cryovial batch you prepared."), verifies the password and records the signature (Approved / Rejected), the decision event and comment, then sets the status in the same save. A rejection also marks the batch destroyed.
  3. System archives an immutable PDF of the batch record. Message "Cryovial batch {code} has been Approved." / "...Rejected."
- Status change: PendingReview -> Approved, or -> Rejected + destroyed. Final.
- Next action: Approved batch becomes available for thaw, storage moves, media-evaluation challenges and the Approved Cryovials list.
- Review: none separate (single signer). Approval does not check expiry or any identity-confirmation result on the server (BR-CRYO-015); whether the approver must verify the panel results: REQUIRES CONFIRMATION.
- Permissions: Cryovials.Approve at the endpoint; section access check.
- Signature points: the decision (password, meaning Approved / Rejected).
- INTENDED source: REQUIRES CONFIRMATION (no specification).

### WF-CRYO-003 Thaw, move or destroy cryovials
- Start / trigger: the row actions on an Approved batch: "Thaw vial", "Change storage location", "Destroy (decommission batch)". Any Cryovials.Manage holder. (verified)
- Thaw steps: (1) user presses "Thaw vial" (shown only while Approved, not destroyed, not expired in the browser, vials remaining above zero); (2) dialog "Thaw Vial: Provide Reason": reason 5-500 characters, button "Confirm Thaw"; (3) system refuses when not approved ("Cryovial batch {code} is not approved - cannot thaw a vial."), destroyed ("...has been destroyed and cannot be used."), expired ("...is expired and cannot be used.") or empty ("No vials remaining in batch {code}."); otherwise decreases vials remaining by one and writes a thaw event (user, notes = reason). Message "Vial from {code} successfully thawed and logged." No password. One thawed vial may serve several challenges; selecting a cryovial in an evaluation does not reduce the count.
- Move steps: (1) "Change storage location" opens "Change Storage Location" with the current location; (2) user picks a cold-storage inventory location, types "Reason for move *" (5-500 characters); (3) system refuses a blank reason ("A reason is required to move cryovial storage."), both or neither of equipment / text ("Exactly one of EquipmentId or LocationText must be provided."), a batch not approved or destroyed, and equipment not InService ("Equipment {code} is not currently in service."); otherwise stores the new location and the equipment id, and writes a review-trail event with old location, new location and reason. Message "Storage location of cryovial batch {code} changed."
- Destroy steps: (1) "Destroy (decommission batch)" opens "Confirm Batch Destruction" with the warning "Destroying this cryovial batch will decommission it permanently. It will no longer be available for thawing, Media Evaluation, or GPT testing."; (2) user presses "Confirm Destruction"; (3) system sets the batch destroyed; no reason, no password, no status check, no review-trail event (general record-change log only); nothing clears it (GAP-507). Message "Batch {code} has been decommissioned and destroyed."
- Status change: thaw and move change no status; destroy sets IsDestroyed (also set by rejection). Vials remaining reaching zero changes no status.
- Blocking conditions: expiry (thaw and challenge use, date-based), destroyed, not approved, no vials, equipment not InService.
- Signature points: none in this workflow.
- INTENDED source: REQUIRES CONFIRMATION; passage-limit and expiry-derivation rules are open (GAP-037, GAP-509).

### WF-MATERIAL-001 Receive material lot and manage documents
- Start / trigger: a user with Materials.Manage opens "Materials Stock" (route `/inventory/materials`, menu entry with `?lab=MICRO` or `?lab=FP`) and presses "Add to Stock". Empty state: "No materials in stock — Receive the first material lot." (verified) [src: MaterialsPage.tsx:L329, L383]
- Who: Materials.Manage (Section Head, Analyst; System Administrator); supersede and void need Materials.DocumentControl (Section Head; System Administrator). Section scope applies to lists, consumption and document upload.
- Steps (receive):
  1. User opens "Add Material to Stock" and chooses "Material Type" (built-in or a new custom name saved as type Other). Depending on type the dialog asks for a media product (dehydrated media), a master entry (chemical, indicator, primary or reference standard), organism and "ATCC No." (lyophilized microorganism), "Purity (%)" and "Moisture Content (%)" (standards). Working standards cannot be received here.
  2. User fills "Material Name", "Manufacturer", code, "Batch / Lot No.", "Storage Location", "Quantity Received", "Unit", optional "Min. Stock Level (optional)", "Receiving Date", optional "Expiry Date", and "Laboratory Section" if the user has several.
  3. Client validation in order: product for dehydrated media; master entry; purity and moisture; "Material name, batch/lot number, receiving date, storage location, and quantity received are required."; "Quantity received must be greater than zero."; "Laboratory section is required." Server messages are in validations (purity, moisture, master entry, custom type).
  4. User presses "Add to Stock". System creates the row with Quantity Remaining = Quantity Received; stock status is computed (InStock / Depleted / Expired).
- Steps (documents): in "Lot details and documents": "Upload Document" (Document Type *: COA, Supplier Certificate, Specification, SDS, Other; file PDF, JPG, JPEG, PNG, WEBP or TIFF; server checks size, extension, declared type and file signature) -> status Current. Users with DocumentControl can "Supersede" (reason and replacement file; old -> Superseded, new -> Current) or "Void" (reason; -> Voided). A voided document cannot be superseded. File integrity is checked by SHA-256 on read. No password on any document action.
- Status change: stock status Expired when expiry date is before today; Depleted at zero; documents Current / Superseded / Voided.
- Use (consumption): media and cryovial preparation and disk selection deduct stock and are refused for the wrong type, an expired lot ("Material {name} (batch {batch}) is expired and cannot be used."), a missing Current COA for dehydrated media, lyophilized microorganisms and supplements, or insufficient stock. Stock without an expiry date never expires (GAP-508).
- Edit: "Edit material"; changing Quantity Received changes Quantity Remaining by the difference; the media product of a batch cannot be changed once lots were prepared from it ("Lots have already been prepared from batch {batch} - its media product can't be changed.").
- Required: type, name, batch, storage location, quantity, receiving date; type-specific as above. Optional: expiry, minimum stock level (and moisture for reference standards).
- Signature points: none. Audit: general record-change log; document access log rows.
- INTENDED source: REQUIRES CONFIRMATION (no specification). Whether receipt needs a second-person check, and what inspection is expected at receipt: REQUIRES CONFIRMATION (no SoD rule found).

### WF-EQUIP-001 Register equipment, status and calibration certificates
- Start / trigger: a user with Equipment.Manage opens "Equipment" (route `/inventory/equipment`, menu `?lab=MICRO` or `?lab=FP`) and presses "Register Equipment". (verified)
- Who: Equipment.Manage (Section Head, Analyst; System Administrator); document supersede and void need Equipment.DocumentControl (Section Head; System Administrator). Section scope: "This equipment belongs to a laboratory section you are not assigned to."
- Steps (register / edit):
  1. User opens "Register Equipment": "Instrument Type", "Manufacturer", "Equipment Code", "Location", "Serial Number", "Firmware Version", "Laboratory", "Calibration Due Date", "Operational Status" (InService, OutOfService, Retired). Client check: "Instrument type, equipment code, and location are required."; "Choose the laboratory this asset belongs to."
  2. User presses "Register Equipment". System refuses a duplicate code ("Equipment code \"{code}\" already exists.") or an inactive / unknown section; new equipment starts InService unless a status is supplied.
  3. To change Operational Status the user opens "Edit equipment", changes the status and must type "Status Change Comment *" ("A comment explaining the operational status change is required." in the dialog; server: "A comment explaining the reason for changing the operational status is required."). System writes a status-history row (previous, new, comment, user, time). No transition rules between statuses.
- Steps (certificates): in "Details and documents" upload a calibration certificate (the only document type; same file rules and 25 MB default size limit as materials); Equipment.DocumentControl holders supersede or void with a reason; access and integrity checks as for materials. Updating the document does not change the Calibration Due Date: that field is typed on the equipment record.
- Active Equipment tab: lists every asset in scope with its activities; the activities and history are matched to the test records by equipment code.
- Effect on use: inventory status OutOfService / Retired blocks the asset as a media-storage or cryovial-storage destination only. Micro incubator selection, media-evaluation incubation and autoclave selection for media preparation do not read the inventory status or its calibration date; incubators are checked on the separate master-data Equipment record (set point range and calibration due date) in individual, shared-TSB, confirmation and grouped selection, but not in media-evaluation incubation (BR-EQUIP-008 to BR-EQUIP-014; GAP-501, GAP-517). A user must therefore update both records (REQUIRES CONFIRMATION that this is the intended procedure).
- Signature points: none. Audit: status history, general record-change log, document access log.
- INTENDED source: REQUIRES CONFIRMATION (no equipment specification; qualification IQ/OQ/PQ not recorded: GAP-038). Equipment configuration (set points, autoclave programs) is WF-EQUIP-002 in C4.

## business-rules
Re-verification pass (Phase 3): BR-MEDIA-001 to BR-MEDIA-030, BR-CRYO-001 to BR-CRYO-014, BR-MATERIAL-001 to BR-MATERIAL-013 and BR-EQUIP-001 to BR-EQUIP-008 were re-read against the cited code and stand as written unless a line below says "CORRECTED" or adds "(verified)". Corrected or replaced: BR-MATERIAL-009, BR-EQUIP-009 (earlier claim reversed), BR-EQUIP-010, BR-MEDIA-017, BR-MEDIA-024. BR-MEDIA-029 re-checked by `git grep` (only one reference to MediaStatus.Destroyed, in the Equipment Inventory service).
- BR-MEDIA-001 IMPLEMENTED: Lot number = `{Product.Code}/{seq:D2}/{yy}`; sequence continues from the highest number issued for that prefix and year. [src: backend/MicroLIMS.Application/Helpers/PreparedLotNumber.cs:L45; backend/MicroLIMS.Application/Services/MediaPreparationService.cs:L17-L20, L77-L82]. Prefix is the product code (a code change starts a new series, per code comment).
- BR-MEDIA-002 IMPLEMENTED: Preparing a lot consumes `TotalWeight` grams from the selected Dehydrated Media Material in the same save as the new lot; blocked by expiry, COA, and stock checks. [src: MediaPreparationService.cs:L59, L26-L30; backend/MicroLIMS.Application/Services/MaterialService.cs:L377-L409].
- BR-MEDIA-003 IMPLEMENTED: The Material must be linked to a media product, and the product must have exactly one Media Configuration. [src: MediaPreparationService.cs:L61-L75].
- BR-MEDIA-004 IMPLEMENTED: Lot manufacturer lot = Material batch number; manufacturer name copied from Material (not caller-supplied). [src: MediaPreparationService.cs:L83-L84, L17-L20].
- BR-MEDIA-005 IMPLEMENTED: Expiry date is entered by the user at preparation (`ExpiryDate` in request); no shelf-life calculation found in the preparation service. [src: MediaPreparationService.cs:L11-L14, L94]. Compare with GAP-509.
- BR-MEDIA-006 IMPLEMENTED: A new lot starts `Status = Prepared`, approval `PendingReview`, `IsReleasedForUse = false`. [src: MediaPreparationService.cs:L95; backend/MicroLIMS.Domain/Entities/Media.cs:L43, L49, L53].
- BR-MEDIA-007 IMPLEMENTED: Preparing a lot auto-creates one evaluation with the configuration's evaluation type and copies its challenges (organism, role, expected description, inoculum); zero challenges allowed. Evaluation starts `Assigned`. [src: MediaPreparationService.cs:L101-L121].
- BR-MEDIA-008 IMPLEMENTED: Incubation parameters for an evaluation challenge come from the Media Configuration's incubation condition (temperature range and hours); the earliest reading time is incubation start plus minimum hours. [src: backend/MicroLIMS.Application/Workflows/MediaEvaluationEngine.cs:L149-L166].
- BR-MEDIA-009 IMPLEMENTED: A result cannot be recorded before `ExpectedReadingAt`. [src: MediaEvaluationEngine.cs:L194-L198].
- BR-MEDIA-010 IMPLEMENTED (GrowthPromotion): Recovery % = round(NewMediaCount / OldMediaCount x 100, 1). Outcome Conform when the recovery is within the configured band, inclusive (recovery >= RecoveryPercentMin and <= RecoveryPercentMax); if `RecoveryPercentMin` has no value the outcome is always Conform. If a minimum is set but the maximum has no value, the comparison with the maximum is false and the outcome is NonConform (read from the expression; no test run). (verified) [src: MediaEvaluationEngine.cs:L223, L229-L231]. This is the only GPT pass/fail rule in code; the band comes from the product's Media Configuration (MediaType band in the old wording). See GAP-505.
- BR-MEDIA-011 IMPLEMENTED (GrowthPromotion reference): exactly one reference lot is required, either a linked prior lot or free text; the linked lot must be in the same section. [src: MediaEvaluationEngine.cs:L208-L221].
- BR-MEDIA-012 IMPLEMENTED (IndicationInhibition, Inhibition role): Conform when no growth observed; NonConform when growth observed. [src: MediaEvaluationEngine.cs:L234-L239].
- BR-MEDIA-013 IMPLEMENTED (IndicationInhibition, Indication role): Conform or NonConform is a manual judgment; the observed text is not compared with the expected description. [src: MediaEvaluationEngine.cs:L27-L30, L241-L246].
- BR-MEDIA-014 IMPLEMENTED (EnrichmentCharacteristics): Conform when the broth is turbid, NonConform otherwise. [src: MediaEvaluationEngine.cs:L248-L253].
- BR-MEDIA-015 IMPLEMENTED: Evaluation becomes Completed when every challenge has an outcome; evaluation Outcome is Conform only if all challenges are Conform. [src: MediaEvaluationEngine.cs:L276-L280].
- BR-MEDIA-016 IMPLEMENTED: A NonConform evaluation sets the lot to `QuarantineFailed`; such a lot is never listed for approval. [src: MediaEvaluationEngine.cs:L284-L285; MediaReleaseService.cs:L41-L51]. A Conform evaluation does not release the lot.
- BR-MEDIA-017 IMPLEMENTED (verified): The evaluation's challenge source must be organism-matched and in the same section as the lot under evaluation. A cryovial batch must also be Approved, not destroyed, and not expired (expiry date before today refuses). A lyophilized disk must be a LyophilizedMicroorganism material, not expired (only if it has an expiry), and have quantity remaining above zero, and selecting it also runs the Materials consumption checks (current COA, stock). Lot-level expiry of the media lot under evaluation is not checked at this step. [src: MediaEvaluationEngine.cs:L60-L132]. Messages: "Material {name} is not a lyophilized microorganism disk.", "Material {name} is {organism}, not {challenge organism}.", "Material {name} (batch {batch}) is expired and cannot be used.", "Material {name} (batch {batch}) has no quantity remaining.", "Material {name} belongs to another laboratory section and cannot challenge this media lot." [MediaEvaluationEngine.cs:L108-L120].
- BR-MEDIA-018 IMPLEMENTED: Selecting a lyophilized disk consumes one unit from Materials Stock at selection time. [src: MediaEvaluationEngine.cs:L122-L127]. GAP-511.
- BR-MEDIA-019 IMPLEMENTED (release approve): Allowed only when evaluation status is Completed and outcome Conform. On approval: `ApprovalStatus = Approved`, `IsReleasedForUse = true`, `Status = Active`, approver and time recorded. [src: MediaReleaseService.cs:L64-L79, L93-L100].
- BR-MEDIA-020 IMPLEMENTED (release reject): Always allowed when the lot is pending. Sets `ApprovalStatus = Rejected`, `Status = QuarantineFailed`. [src: MediaReleaseService.cs:L73, L93-L105].
- BR-MEDIA-021 IMPLEMENTED: A release decision requires a password signature; signature meaning Approved or Rejected; an immutable PDF of the lot record is archived. [src: MediaReleaseService.cs:L87-L91, L109-L113].
- BR-MEDIA-022 IMPLEMENTED: Decision is final: a lot whose approval is no longer PendingReview cannot be decided again. [src: MediaReleaseService.cs:L64-L65].
- BR-MEDIA-023 IMPLEMENTED (verified): The released-lot list (`GET /api/media/released`, used by the cryovial identity panel and the evaluation reference-lot picker) returns lots with `IsReleasedForUse`; unless `includeExpired` is passed it also requires `Status = Active` and `ExpiryDate` later than now. The per-step lot list shown in the testing workspace applies the same three conditions for the step's medium. [src: MediaPreparationService.cs:L149-L158; backend/MicroLIMS.Application/Services/TestWorkflowQueryService.cs:L69].
- BR-MEDIA-024 IMPLEMENTED (verified, per entry point; this is the answer to "can an unreleased or expired lot be used in a test"):
  - Individual incubation media selection (`IncubationStepRecorder.SelectMediaAsync`): refuses a lot that is not released, or is OutOfStock or QuarantineFailed ("Media lot \"{lot}\" is not released for use, out of stock, or rejected."); refuses a lot of another laboratory section; refuses a medium not on the step's permitted list ("This step requires a different medium. The selected lot is {name}."). It does NOT check the lot's expiry date. [src: backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs:L49-L81].
  - Grouped batch media selection: same status refusal plus an expiry refusal ("Media lot \"{lot}\" is expired (expired on yyyy-MM-dd)."; refused when expiry <= now). [src: backend/MicroLIMS.Application/Services/GroupedTestAction/BatchSelectMediaRules.cs:L20-L30; GroupedTestActionService.cs:L453].
  - Shared TSB start (`PathogenSessionService.StartSharedTsbAsync`): refuses an expired lot (expiry date before today; error code MediaExpired, "Media lot #{lot} expired on yyyy-MM-dd.") but has no check of released, OutOfStock or QuarantineFailed status. [src: backend/MicroLIMS.Application/Services/PathogenSessionService.cs:L575-L591].
  - Pathogen confirmation media: the same released/OutOfStock/QuarantineFailed refusal as individual selection; no expiry check found. [src: backend/MicroLIMS.Application/Workflows/TestWorkflow/PathogenConfirmationRecorder.cs:L115].
  - The lot lists offered by the screens exclude unreleased, non-Active and expired lots (BR-MEDIA-023), so the gaps above are reachable only by a direct request or a lot that expired after it was listed (not tested). See GAP-500 and GAP-516.
- BR-MEDIA-025 IMPLEMENTED (mark out of stock): Allowed only when released and `Status = Active`; sets `Status = OutOfStock`; comment optional; default log text "Media lot manually marked Out of Stock."; no password. [src: MediaPreparationService.cs:L160-L180].
- BR-MEDIA-026 IMPLEMENTED (storage add): Lot must be released and Active; form must be one of Plates, AgarBottles, BrothBottles, Tubes; quantity greater than zero; exactly one of equipment or location text; equipment must be InService; same location and form entries are merged. [src: backend/MicroLIMS.Application/Services/MediaStorageService.cs:L48-L54, L59, L64-L65, L76-L77, L92-L99].
- BR-MEDIA-027 IMPLEMENTED (storage move): Reason required; quantity greater than zero and not more than the entry quantity; destination must differ from source; destination equipment must be InService; partial or full move; log comment "Moved {qty} {form} from ... to ... Reason: ...". [src: MediaStorageService.cs:L144-L147, L152, L157-L158, L165-L166, L177-L178, L193, L211-L235, L255].
- BR-MEDIA-028 IMPLEMENTED (expiring list): Active lots with expiry within N days (default 7) and any date already past; days remaining = ceiling of days to expiry; evaluation status shown as Passed (Conform), Failed (NonConform) or Pending. [src: backend/MicroLIMS.Application/Services/MediaExpiryService.cs:L25-L32, L45-L53].
- BR-MEDIA-029 IMPLEMENTED: No code path assigns `MediaStatus.Expired` or `MediaStatus.Destroyed`; expiry is read from `ExpiryDate` at query time. [git grep for MediaStatus.Expired / MediaStatus.Destroyed: only comparison at backend/MicroLIMS.Application/Services/EquipmentInventoryService.cs:L593]. See GAP-502.
- BR-MEDIA-030 IMPLEMENTED: Media lot quantity remaining is not stored on Media; storage entries track quantities per location. [src: backend/MicroLIMS.Application/Services/MediaExpiryService.cs:L10-L11]. See GAP-510.
- BR-MEDIA-031 DISCREPANCY (confirmed, spec vs code; the only spec for GPT is `documents/DesignSpecification/BusinessRules.md` section 7, which is older than the code, see GAP-021):
  - Spec: stages "Media Preparation → Sterility → Recovery → Release (or Rejected)", advancing "strictly forward" [src: documents/DesignSpecification/BusinessRules.md:L141-L145]. Code: no stage engine is used; `GptStage` is declared and not used by the evaluation or release code; there is no sterility step (GAP-022) [src: backend/MicroLIMS.Domain/Enums/GptStage.cs; MediaEvaluationEngine.cs:L1-L312].
  - Spec: "A media lot passes GPT only if every recorded challenge organism passed" [BusinessRules.md:L148-L149]. Code: matches in effect: the evaluation outcome is Conform only if every challenge is Conform (BR-MEDIA-015).
  - Spec: failure means the lot "can never be released" [BusinessRules.md:L149-L150]. Code: a NonConform evaluation sets QuarantineFailed and approval is refused, but a challenge result can be entered again after completion and nothing reverts QuarantineFailed (GAP-504), so "never" is not enforced as a rule.
  - Spec: "`Media.IsReleasedForUse` is `true` only when `GptStage == Release` and `Status == Active` and not expired" [BusinessRules.md:L151-L153]. Code: `IsReleasedForUse` is a stored flag set only by the signed release decision; expiry is not part of the flag and is applied by individual queries (BR-MEDIA-023/024).
  - Spec: at the Recovery stage "at least one `GptChallengeResult` must be recorded" [BusinessRules.md:L146-L147]. Code: a lot can be prepared with zero challenges; such an evaluation never reaches Completed because completion is computed only when a result is recorded (BR-MEDIA-007, BR-MEDIA-015).
  - The signed Section Head release decision, the segregation-of-duties rule and the lot report are not in the spec at all. (verified)
- BR-MEDIA-032 INTENDED (BusinessRules.md:L228): a Media Expiry dashboard notification fires for `Media.ExpiryDate` within 7 days or already past, severity warning, or error if already expired. Code (verified): active lots only, window 7 days (`ExpiryWarningWindow`), text "{material} (Lot {lot}) has expired" / "expires {dd-MMM-yyyy}", severity error when expired and warning otherwise [src: backend/MicroLIMS.Application/Services/DashboardNotificationService.cs:L22, L135-L143]. Matches the spec except that only Active lots are included.
- BR-MEDIA-033 IMPLEMENTED (preparation checks, verified): the autoclave is validated only as "equipment of type Autoclave" (no operational status, no calibration check); autoclave program, load type, temperature, cycle time, cycle number and pH are stored as sent, with no range or consistency check on the server; the dialog checks only presence [src: MediaPreparationService.cs:L51-L52, L87-L93; MediaPreparationDialog.tsx:L105-L153]. Server-side there is no check that Total Weight, Total Volume, pH or Expiry Date are positive or in the future beyond the Materials stock guards (not found). See GAP-515.

### MOD-CRYO-001 Cryovials
- BR-CRYO-001 IMPLEMENTED: A batch cannot be prepared unless the physical check against the organism reference description is confirmed. [src: backend/MicroLIMS.Application/Services/CryovialService.cs:L56-L57]
- BR-CRYO-002 IMPLEMENTED: Source must be a LyophilizedMicroorganism Material with an organism set. [src: CryovialService.cs:L59-L66]
- BR-CRYO-003 IMPLEMENTED (verified): Each identity-confirmation media lot must be released, not OutOfStock, not QuarantineFailed, and in the same section. Media expiry is not checked on the server (the dialog's media list is the released/Active/unexpired list, BR-MEDIA-023). The identity rows' incubator id, start and end are stored as sent: no check that the incubator exists, is in calibration or in service, or that end is after start. [src: CryovialService.cs:L68-L77, L107-L114]. See GAP-500, GAP-503.
- BR-CRYO-015 IMPLEMENTED (verified): "Approved" for a cryovial means the signed approval only; the approver check is "not the preparer" and nothing else (the approver may be anyone with Cryovials.Approve, including a user who thawed or moved the batch). Approval does not check the batch expiry date. [src: CryovialService.cs:L143-L168].
- BR-CRYO-016 IMPLEMENTED (verified): Thaw is allowed until the end of the expiry date (refused when expiry date is before today); the Cryovials screen hides "Thaw vial" when the expiry is at or before the current moment, so on the expiry day itself the button is hidden although the server would accept a direct request. [src: CryovialService.cs:L192-L193; frontend/src/modules/laboratoryConfiguration/cryovials/components/CryovialReviewTable.tsx:L20-L23, L122].
- BR-CRYO-017 IMPLEMENTED (verified): The storage equipment at preparation is recorded only as text in `StorageCondition`; the server does not check that it is a freezer, in service, or in the section. The "freezer, InService" restriction exists only in the dialog. After preparation, "Change storage location" does check InService on the server and stores the equipment id. [src: CryovialService.cs:L100, L245-L260; PrepareCryovialBatchDialog.tsx:L92-L98, L141-L143].
- BR-CRYO-004 IMPLEMENTED: Preparing a batch consumes `DiscsUsed` from the Material (same guards as Materials: expiry, current COA, stock). [src: CryovialService.cs:L85; backend/MicroLIMS.Application/Services/MaterialService.cs:L377-L409].
- BR-CRYO-005 IMPLEMENTED: Batch code uses the same numbering as media lots, prefixed by the Material code (or an alphanumeric form of the name if no code). [src: CryovialService.cs:L88, L92; backend/MicroLIMS.Application/Helpers/PreparedLotNumber.cs:L17-L23].
- BR-CRYO-006 IMPLEMENTED: New batch: `ApprovalStatus = PendingReview`, `VialsRemaining = NumberOfVialsPrepared`, expiry date from the request (entered by the user). [src: CryovialService.cs:L97-L103; L99].
- BR-CRYO-007 IMPLEMENTED: Approve or reject is signed with the password; a decided batch cannot be decided again; the preparer cannot decide it. [src: CryovialService.cs:L148-L161].
- BR-CRYO-008 IMPLEMENTED: Rejecting a batch also sets `IsDestroyed = true`. [src: CryovialService.cs:L163-L166].
- BR-CRYO-009 IMPLEMENTED (thaw): Requires Approved, not destroyed, not expired (date before today), and vials remaining; decrements `VialsRemaining` and writes a ThawEvent with optional notes. No password, no reason required. [src: CryovialService.cs:L183-L199].
- BR-CRYO-010 IMPLEMENTED (destroy): Sets `IsDestroyed = true`; no status check, no reason, no review-gate event in this method. [src: CryovialService.cs:L202-L208]. See GAP-507.
- BR-CRYO-011 IMPLEMENTED (move storage): Reason required; exactly one of equipment or location text; batch must be Approved and not destroyed; equipment must be InService; logs StorageLocationChanged with old and new location and reason. [src: CryovialService.cs:L214-L268].
- BR-CRYO-012 IMPLEMENTED (use as challenge source): Batch must be Approved, not destroyed, not expired (date before today), same organism as the challenge, same section. Selecting does not decrement vials; thawing does. [src: backend/MicroLIMS.Application/Workflows/MediaEvaluationEngine.cs:L60-L90].
- BR-CRYO-013 IMPLEMENTED: A batch's `IsCryovialApprovedAsync` check is Approved and not destroyed. [src: CryovialService.cs:L211-L212].
- BR-CRYO-014 IMPLEMENTED: No passage number or generation field found in the batch entity. [src: backend/MicroLIMS.Domain/Entities/Cryovial.cs] (grep: none). See GAP-037.

### MOD-MATERIAL-001 Materials Stock
- BR-MATERIAL-001 IMPLEMENTED: Purity required for Primary and Reference standards, must be >0 and <=100; not allowed for other types. [src: backend/MicroLIMS.Application/Services/MaterialService.cs:L67-L82].
- BR-MATERIAL-002 IMPLEMENTED: Moisture content allowed only for Reference standards, must be >=0 and <100. [src: MaterialService.cs:L111-L112].
- BR-MATERIAL-003 IMPLEMENTED: At receipt `QuantityRemaining = QuantityReceived`. Remaining quantity changes by consumption and by receipt-quantity edits (delta). [src: MaterialService.cs:L191, L291; L208-L209 comment].
- BR-MATERIAL-004 IMPLEMENTED: A master-entry link must belong to the same laboratory, be active, and match the material's category. [src: MaterialService.cs:L101-L104].
- BR-MATERIAL-005 IMPLEMENTED: Dehydrated media lots must choose a configured media product. Once lots are prepared from a batch, its media product cannot be changed. [src: MaterialService.cs:L146, L229-L238].
- BR-MATERIAL-006 IMPLEMENTED: Consumption (`ConsumeAsync`) checks in order: material exists and is of the expected type; not expired (date before today, only if an expiry is set); COA rule; stock is enough. Then decrements and sets last-modified fields (no separate audit event). [src: MaterialService.cs:L377-L409].
- BR-MATERIAL-007 IMPLEMENTED: COA is required before use for DehydratedMedia, LyophilizedMicroorganism and Supplement; a Current COA must exist. [src: MaterialService.cs:L366-L372, L392-L402; backend/MicroLIMS.Application/Services/MaterialDocumentService.cs:L72-L76].
- BR-MATERIAL-008 IMPLEMENTED: Stock status is computed, never stored: Expired if expiry date before today; Depleted if remaining is zero or less; otherwise InStock. [src: backend/MicroLIMS.Domain/Entities/Material.cs:L84-L87].
- BR-MATERIAL-009 CORRECTED (verified): the rule "remaining > 0 and (no expiry or expiry today or later)" at MaterialService.cs:L355-L362 belongs to the usable REFERENCE/WORKING STANDARDS list (`GET /usable-reference-standards`), not to media preparation. The media and cryovial preparation dialogs filter the stock list in the browser to rows whose computed stock status is InStock (BR-MATERIAL-008), and the server then repeats the expiry, COA and stock checks in `ConsumeAsync` (BR-MATERIAL-006). Print list = InStock only. [src: MaterialService.cs:L127-L131, L355-L362; MediaPreparationDialog.tsx:L35; PrepareCryovialBatchDialog.tsx:L89].
- BR-MATERIAL-010 IMPLEMENTED (documents): Upload sets status Current; supersede sets old to Superseded and new to Current; voided documents cannot be superseded; void sets Voided with reason. Supersede and void are not password-signed (no password in the controller or service); they record the reason, actor and time and write an access-log row (verified). [src: backend/MicroLIMS.Application/Services/MaterialDocumentService.cs:L165, L256-L257, L282-L289, L337-L349].
- BR-MATERIAL-011 IMPLEMENTED: CoA eligibility returns IsEligible = a Current COA exists, when CoA is required for the material type. [src: MaterialDocumentService.cs:L367-L387].
- BR-MATERIAL-012 IMPLEMENTED: Documents store a SHA-256 hash at upload and verify it on read; mismatch is refused. [src: MaterialDocumentService.cs:L134-L150, L225].
- BR-MATERIAL-013 IMPLEMENTED: Expiry date on receipt is optional in the server model (nullable) and not in the required-field message. See GAP-508. [src: MaterialService.cs:L14; frontend/src/modules/inventory/materials/components/AddMaterialDialog.tsx:L621].

### MOD-EQUIP-001 Equipment Inventory
- BR-EQUIP-001 IMPLEMENTED: Equipment code must be unique. [src: backend/MicroLIMS.Application/Services/EquipmentInventoryService.cs:L135, L169].
- BR-EQUIP-002 IMPLEMENTED: Laboratory section required on create; caller must be assigned to it. [src: EquipmentInventoryService.cs:L118-L127].
- BR-EQUIP-003 IMPLEMENTED: New equipment starts InService unless a status is supplied on create. [src: backend/MicroLIMS.Domain/Entities/EquipmentInventory.cs:L31; EquipmentInventoryService.cs:L147-L148].
- BR-EQUIP-004 IMPLEMENTED: Changing operational status requires a comment; a history row (previous, new, comment) is written. [src: EquipmentInventoryService.cs:L173-L188].
- BR-EQUIP-005 IMPLEMENTED: Calibration due date is stored; "overdue" is computed (due date before today) and not persisted. [src: backend/MicroLIMS.Domain/Entities/EquipmentInventory.cs:L30, L44; backend/MicroLIMS.Persistence/Configurations/EquipmentInventoryConfiguration.cs:L27].
- BR-EQUIP-006 IMPLEMENTED: Equipment print list = InService only. [src: EquipmentInventoryService.cs:L104].
- BR-EQUIP-007 IMPLEMENTED (verified): Inventory equipment must be InService to be a media storage location (add and move) and a cryovial storage location after preparation (move). At cryovial preparation the storage equipment is not checked by the server (BR-CRYO-017). Only the InService status is tested; retired or out-of-service equipment is refused with the same message. [src: backend/MicroLIMS.Application/Services/MediaStorageService.cs:L76-L77, L177-L178; CryovialService.cs:L248-L249].
- BR-EQUIP-008 IMPLEMENTED (verified; grouped batch selection): the incubator must exist as a master-data Equipment record of type Incubator, have a set point, and not be past its calibration due date (refused when due date < now). The check reads the master-data Equipment record, not the inventory register. [src: backend/MicroLIMS.Application/Services/GroupedTestAction/BatchSelectMediaRules.cs:L32-L46; GroupedTestActionService.cs:L458].
- BR-EQUIP-009 CORRECTED (verified): the earlier "NOT ENFORCED" claim was wrong for individual incubation. Individual media selection (`IncubationStepRecorder.SelectMediaAsync`), the pathogen-confirmation selections and the shared TSB start (including the tests that join it) call `IncubatorEligibilityService.IsWithinRangeAsync`, directly or through `RequireEligibleIncubatorAsync`; grouped batch selection uses the separate check in BR-EQUIP-008. An incubator is eligible only if it is a master-data Equipment of type Incubator, has a set point inside the step medium's temperature range (inclusive), and has no calibration due date or a due date on or after now. A rejected incubator gives the single message "The selected incubator's set point is outside this medium's temperature range." even when the real reason is an expired calibration (error code IncubatorTempOutOfRange). [src: backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs:L80; PathogenConfirmationRecorder.cs:L271, L461; backend/MicroLIMS.Application/Services/PathogenSessionService.cs:L556, L973; backend/MicroLIMS.Application/Workflows/TestWorkflow/TestWorkflowSupport.cs:L803-L808; backend/MicroLIMS.Application/Services/IncubatorEligibilityService.cs:L33-L41]. Shared TSB messages: "The shared TSB can't start: the selected incubator's set point is outside the {min}-{max} °C range of \"{medium}\" on test {code}." [IncubationStepRecorder.cs:L148-L150].
- BR-EQUIP-012 NOT ENFORCED (verified): none of those incubator checks read the inventory register's `Operational Status` (InService / OutOfService / Retired) or its Calibration Due Date. An incubator marked OutOfService or Retired in Equipment Inventory, or with an overdue calibration date only on the inventory record, is still accepted if its master-data record is in range and in calibration. [git grep EquipmentInventories in the incubation, media-evaluation and incubator-eligibility code: no use; src: IncubatorEligibilityService.cs:L33-L41; EquipmentInventoryService.cs:L190-L200 (the inventory update writes only the inventory row)]. By contrast the HPLC run, ICP run and physicochemical analysis paths do refuse OutOfService or Retired inventory equipment (C6 scope) [src: backend/MicroLIMS.Application/Services/HplcRunService.cs:L252-L256; IcpRunService.cs:L167-L171; TestWorkflowSupport.cs:L607-L610]. See GAP-501 and GAP-517.
- BR-EQUIP-013 NOT ENFORCED (verified): media-evaluation incubation (`MediaEvaluationEngine.RecordIncubationAsync`) accepts any incubator id: no type, set point, calibration or status check. [src: MediaEvaluationEngine.cs:L136-L177]. Media preparation likewise checks the autoclave only by type (BR-MEDIA-033).
- BR-EQUIP-014 IMPLEMENTED (verified): inventory and master-data equipment are linked only by matching code (case-insensitive). The Active Equipment tab and history match the master record by code; "Link inventory equipment to master" creates a master record with set point 32.5 for incubators and the inventory calibration date copied once; later inventory edits do not update the master record. [src: backend/MicroLIMS.Application/Services/EquipmentConfigurationService.cs:L101-L175; EquipmentInventoryService.cs:L266, L297]. See GAP-517.
- BR-EQUIP-010 IMPLEMENTED (documents, verified): Calibration certificate is the only document type. File checks use the shared validator (PDF, JPG, JPEG, PNG, WEBP, TIFF; size limit from configuration `MaterialDocuments:MaxFileSizeBytes`, default 25 MB; request ceiling 30 MB). Statuses reuse Current / Superseded / Voided. On read the SHA-256 hash is checked; on mismatch the download is REFUSED with "Document integrity check failed. The file may have been altered. Please contact your System Administrator." and an access row is still written. Supersede needs a reason ("A reason for supersession is required." at the controller) and a file; void needs a reason ("A reason for voiding is required."), repeat void refused ("This document is already voided."). [src: backend/MicroLIMS.API/Controllers/EquipmentDocumentController.cs:L40-L42, L72-L108; backend/MicroLIMS.Application/Services/EquipmentDocumentService.cs:L164-L190, L236-L296; backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs:L184-L185].
- BR-EQUIP-011 IMPLEMENTED: Equipment qualification (IQ/OQ/PQ) record not found (see GAP-038).

## validations
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

## statuses
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
- Transitions: upload -> Current; supersede -> old Superseded, new Current (reason, DocumentControl, no password; "signed reason" corrected in Phase 5); void -> Voided with reason (DocumentControl; any non-voided document) [src: MaterialDocumentService.cs:L256-L257, L282-L289, L343-L349].
- Material type (`MaterialType`): DehydratedMedia, LyophilizedMicroorganism, Supplement, AntibioticDisc, IdentificationKit, IdentificationReagent, Chemical, Indicator, ReferenceBuffer, DisposableTool, Other, ReferenceStandard, WorkingStandard, PrimaryStandard [src: documentation/knowledge/_raw/backend-enums.md:L370-L388].
- Document type (`MaterialDocumentType`): COA, SupplierCertificate, Specification, SDS, Other [src: backend-enums.md:L359-L368].

### MOD-EQUIP-001 Equipment Inventory
- Operational status (`EquipmentOperationalStatus`): InService (0), OutOfService (1), Retired (2) [src: documentation/knowledge/_raw/backend-enums.md:L156-L162]. Change requires a comment and writes a history row; no transition rules found between values [src: EquipmentInventoryService.cs:L173-L188].
- Calibration overdue: computed (calibration due date before today), shown on register; not a status value [src: backend/MicroLIMS.Domain/Entities/EquipmentInventory.cs:L44].
- Equipment type (`EquipmentType`): Incubator, Autoclave, LafCabinet, BiologicalSafetyCabinet, WaterBath, Other [src: backend-enums.md:L165-L173].
- Document type (`EquipmentDocumentType`): CalibrationCertificate [src: backend-enums.md:L149-L154]. Document statuses (verified): the same Current / Superseded / Voided values as material documents; upload -> Current; supersede -> old Superseded, new Current; void -> Voided (reason required, repeat void refused) [src: backend/MicroLIMS.Application/Services/EquipmentDocumentService.cs:L236-L296].
- Effect of status on use (verified): only InService is required for media storage and cryovial storage-move destinations. The status is not read by micro incubator selection or media-evaluation incubation (BR-EQUIP-012, BR-EQUIP-013); those use the master-data record. HPLC, ICP and physicochemical analysis paths do refuse OutOfService and Retired (C6).

## terminology
- Media lot: a prepared batch of a dehydrated medium; identified by LotNumber `{Product.Code}/{seq:D2}/{yy}` [src: PreparedLotNumber.cs:L45].
- Manufacturer lot: the batch number of the consumed dehydrated media Material [src: MediaPreparationService.cs:L83].
- Media product: the configured medium type (code and name) a lot belongs to [src: MediaPreparationService.cs:L65-L77].
- GPT: Growth Promotion Testing, the GrowthPromotion evaluation type [src: EvaluationType.cs:L5-L9].
- Indication / Inhibition: IndicationInhibition evaluation with Indication or Inhibition challenge roles [src: MediaEvaluationEngine.cs:L234-L241].
- Enrichment Characteristics: EnrichmentCharacteristics evaluation; Conform if turbid [src: MediaEvaluationEngine.cs:L248-L253].
- Challenge: one organism inoculated in an evaluation, with its source (cryovial or lyophilized disk), incubation and result.
- Recovery %: new count / old count x 100, rounded to 1 decimal [src: MediaEvaluationEngine.cs:L223].
- Conform / NonConform: evaluation result; Conform does not release a lot by itself.
- Release (Approved Media): signed Section Head decision that makes the lot usable [src: MediaReleaseService.cs:L11-L17].
- Out of Stock: manual status, lot no longer used for new storage or tests.
- Storage entry: a quantity of a lot at an equipment location or free-text location, with form.
- Sterility: appears in UI subtitle; no sterility record in code (GAP-022).

### MOD-CRYO-001 Cryovials
- Cryovial batch (Code): a set of frozen vials prepared from one lyophilized microorganism Material [src: CryovialService.cs:L90-L99].
- Identity confirmation: the panel of released media lots and incubators used to confirm the batch organism at preparation [src: CryovialService.cs:L107-L114; PrepareCryovialBatchDialog.tsx:L123-L126].
- Discs used: units consumed from Materials Stock to prepare the batch [src: CryovialService.cs:L85].
- Vials prepared / vials remaining: counts at preparation and after thawing [src: CryovialService.cs:L98-L99].
- Thaw: removing one vial for use; recorded as ThawEvent with notes [src: CryovialService.cs:L183-L199].
- Destroy: marks the batch destroyed [src: CryovialService.cs:L202-L208].
- Change Storage Location: move dialog title [src: MoveCryovialStorageDialog.tsx:L14].
- Reference strain / reference cryovial: UI title "Reference Cryovials" [application map SCR-CRYO-001].

### MOD-MATERIAL-001 Materials Stock
- Material (lot): one received stock entry with batch number, manufacturer, expiry (optional), location, quantity received and remaining [src: MaterialService.cs:L183-L192].
- Batch / Lot No. (BatchNumber) and Manufacturer lot: shown as "Batch/Lot No." [src: MaterialsPage.tsx:L271].
- Master entry: the catalog entry (reagent, reference standard) a lot is linked to [src: MaterialService.cs:L101-L104].
- Working standard: created only by an approved qualification, not by receipt [src: MaterialService.cs:L137].
- COA: Certificate of Analysis, required before use for dehydrated media, lyophilized microorganisms and supplements [src: MaterialService.cs:L366-L372].
- Approved Media List / Approved Cryovial List: the inventory lists of released media and approved, non-destroyed cryovials [src: frontend/src/modules/inventory/approvedLists/ApprovedCryovialListPage.tsx:L303-L304].

### MOD-EQUIP-001 Equipment Inventory
- Equipment register: the instrument register (tab "Equipment Register") [src: EquipmentInventoryPage.tsx:L249].
- Active Equipment: tab listing in-service assets with activities [src: EquipmentInventoryPage.tsx:L250].
- Calibration Due Date: the stored due date [src: RegisterEquipmentDialog.tsx:L271].
- Status Change Comment: comment required when operational status changes (helper "Required whenever the operational status changes.") [src: RegisterEquipmentDialog.tsx:L301, L310].
- Where-is-it: lookup of an asset's current location [src: frontend/src/modules/inventory/equipment/services/EquipmentInventoryService.ts:L66].

## reports
- Media lot summary: GET `/api/media/{id}/summary`, `/summary/pdf`, `/summary/word` [src: backend/MicroLIMS.API/Controllers/MediaController.cs:L113-L131]. Screen SCR-MEDIA-002 (no permission gate) [src: frontend/src/routes/AppRoutes.tsx:L104].
- Lot summary PDF is archived as an immutable record at release decision [src: MediaReleaseService.cs:L109-L113].
- Approved Media List (inventory): columns and KPIs as in SCR-MATERIAL-001 section above.
- Expiring media list: GET `/api/media/expiring` (default 7 days) [src: MediaController.cs:L51; MediaExpiryService.cs:L25].
- Dashboard notification: "{material} (Lot {lot}) has expired" or "expires {date}" [src: backend/MicroLIMS.Application/Services/DashboardNotificationService.cs:L143].

### MOD-CRYO-001 Cryovials
- Cryovial batch summary: GET `/api/cryovials/{id}/summary`, `/summary/pdf`, `/summary/word` [src: backend/MicroLIMS.API/Controllers/CryovialController.cs:L81-L99]; screen SCR-CRYO-002 (no permission gate) [src: frontend/src/routes/AppRoutes.tsx:L105].
- Batch record PDF archived at approve or reject [src: backend/MicroLIMS.Application/Services/CryovialService.cs:L170-L174].
- Approved Cryovial List (inventory): columns "Cryovial Code", "Organism", "ATCC No.", "Manufacturer", "Storage", "Vials Remaining", "Expiry", "Status" [src: frontend/src/modules/inventory/approvedLists/ApprovedCryovialListPage.tsx:L282-L293].

### MOD-MATERIAL-001 Materials Stock
- Materials print: GET `/api/inventory/materials/print` [src: backend/MicroLIMS.API/Controllers/MaterialController.cs:L46]; list = InStock only [src: backend/MicroLIMS.Application/Services/MaterialService.cs:L127-L131].
- Document access log entries per view/download and per supersede or void [src: MaterialDocumentService.cs:L319 (RecordAccessAsync), L353].

### MOD-EQUIP-001 Equipment Inventory
- Equipment print: GET `/api/inventory/equipment/print` [src: backend/MicroLIMS.API/Controllers/EquipmentInventoryController.cs:L52]; InService only [src: backend/MicroLIMS.Application/Services/EquipmentInventoryService.cs:L104-L106].
- Status history: GET `/api/inventory/equipment/{id}/status-history` [src: EquipmentInventoryController.cs:L91]; activities and history GET `/api/inventory/equipment/{id}/activities`, `/history` [EquipmentInventoryController.cs:L60, L66].

## audit-trail
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

## traceability
- Lot (Media) -> Material (dehydrated media stock: batch, manufacturer, media product) [src: MediaPreparationService.cs:L81].
- Lot -> autoclave Equipment (AutoclaveEquipmentId, program, load, temperature, cycle, pH) [src: MediaPreparationService.cs:L87-L93].
- Lot -> MediaEvaluation -> MediaEvaluationChallenge -> organism, source (Cryovial or Material disk), Incubation (incubator equipment, start/complete), ReferenceMedia lot [src: MediaEvaluationService.cs:L33-L42; MediaEvaluationEngine.cs:L152-L168].
- Lot -> MediaStorageEntry -> Equipment or location text [src: MediaStorageService.cs:L108-L109].
- Test step -> media lot chosen per incubation (test traceability in C2) [src: IncubationStepRecorder.cs:L56-L70].
- Lot -> MediaConfiguration (per product) for incubation condition and challenges [src: MediaEvaluationEngine.cs:L294-L305].

### MOD-CRYO-001 Cryovials
- Cryovial batch -> Material (lyophilized microorganism stock, batch, manufacturer) and Organism [src: CryovialService.cs:L90-L96].
- Cryovial batch -> identity confirmation rows -> Media lot and incubator equipment id, incubation start and end, observation text [src: CryovialService.cs:L107-L114].
- Cryovial batch -> storage equipment (StorageEquipmentId) or location text [src: CryovialService.cs:L258-L260].
- Cryovial batch -> thaw events [src: CryovialService.cs:L198].
- Cryovial batch -> media evaluation challenge (CryovialId on challenge) [src: MediaEvaluationEngine.cs:L78].

### MOD-MATERIAL-001 Materials Stock
- Material -> media product (MediaProductId) -> media lots prepared from it [src: MediaPreparationService.cs:L61-L66, L81].
- Material -> organism (OrganismId) for lyophilized microorganisms [src: CryovialService.cs:L65].
- Material -> master entry (MaterialMasterEntryId) and section [src: MaterialService.cs:L101-L104].
- Material -> documents (COA, supplier certificate, specification, SDS, other) [src: MaterialDocumentService.cs:L72-L76].

### MOD-EQUIP-001 Equipment Inventory
- Equipment inventory asset -> status history, calibration certificate documents, activities (where-is-it) [src: EquipmentInventoryController.cs:L60-L72, L91; EquipmentDocumentController.cs:L31].
- Equipment inventory asset -> media storage entries and cryovial storage location [src: MediaStorageService.cs:L76-L77; CryovialService.cs:L248-L251].
- Equipment inventory asset -> master configuration equipment (matched by code, for active activities) [src: EquipmentInventoryService.cs:L290-L291].

## gaps
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
