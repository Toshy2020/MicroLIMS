# Frontend screen inventory: Laboratory Configuration, Inventory, Document Control, HPLC/GC, ICP, Solution Preparation, Working Standards

Raw facts only. Read-only collection, no interpretation.

- Paths are relative to `E:\MicroLIMS\MicroLIMS\` (for example `frontend/src/routes/AppRoutes.tsx:130`).
- `[src: path:Lnn]` marks a fact's source line. `[src: path:Lnn-Lnn]` marks a range.
- `UNCLEAR` = not determined from the code read. `not enumerated` = field list not read for that dialog or page.
- Route guards: `PermissionRoute` (`frontend/src/routes/PermissionRoute.tsx:12`) renders the page only if `useHasPermission(code)` is true, otherwise redirects to `/dashboard`. Permission code strings are in `frontend/src/routes/routes.ts`.
- Coverage note: every in-scope route was mapped. Page-level titles, tabs, endpoints and signature triggers were collected for every page and for the main dialogs. Field-level labels and buttons were collected only where marked. Everything else is marked `not enumerated`.

---

## Routes and permission codes (shared)

| Permission constant | Code string | Source |
|---|---|---|
| MEDIA_PREPARE | "Media.Prepare" | routes.ts:244 |
| MEDIA_RELEASE | "Media.Release" | routes.ts:245 |
| TEST_WORKFLOW_EXECUTE | "TestWorkflow.Execute" | routes.ts:228 |
| CRYOVIALS_MANAGE | "Cryovials.Manage" | routes.ts:230 |
| SOLUTIONS_PREPARE | "Solutions.Prepare" | routes.ts:247 |
| MATERIALS_MANAGE | "Materials.Manage" | routes.ts:231 |
| EQUIPMENT_MANAGE | "Equipment.Manage" | routes.ts:233 |
| ITEMS_MANAGE | "Items.Manage" | routes.ts:235 |
| MASTER_DATA_MANAGE | "MasterData.Manage" | routes.ts:236 |
| DOCUMENTS_CONFIG_MANAGE | "Documents.ConfigManage" | routes.ts:240 |
| WORKING_STANDARDS_QUALIFY | "WorkingStandards.Qualify" | routes.ts:248 (used only in a tooltip, see WS section) |
| OOS_MANAGE | "Oos.Manage" | routes.ts:246 |
| SAMPLES_RECEIVE / _OWN_LAB / TRACK_ALL | "Samples.Receive", "Samples.ReceiveOwnLab", "Samples.TrackAll" | routes.ts:223-225 (receiving, not in scope) |

Routes in scope that have NO `PermissionRoute` (any authenticated user with the MainLayout can open them):
- `/document-control`, `/document-control/my-reading-list`, `/document-control/training-matrix`, `/document-control/compliance-dashboard`, `/document-control/library`, `/document-control/documents/:id`, `/document-control/audit` [src: frontend/src/routes/AppRoutes.tsx:246-252]
- `/hplc-workspace/*` and `/gc-workspace/*` [src: frontend/src/routes/AppRoutes.tsx:174-197]
- `/icp-workspace/*` [src: frontend/src/routes/AppRoutes.tsx:200-209]
- `/working-standards` [src: frontend/src/routes/AppRoutes.tsx:211]
- `/cryovials/:id/report`, `/media/:id/report` (AuthenticatedRoutes only) [src: frontend/src/routes/AppRoutes.tsx:104-105]

Legacy redirects in scope:
- `/laboratory-configuration/cause-of-testing` -> `/laboratory-configuration/receiving-configuration` [src: frontend/src/routes/AppRoutes.tsx:235]
- `/inventory/approved-media` -> `/laboratory-configuration/media?tab=approved` [src: frontend/src/routes/AppRoutes.tsx:261]
- `/inventory/approved-cryovials` -> `/laboratory-configuration/cryovials?tab=approved` [src: frontend/src/routes/AppRoutes.tsx:262]

Menu note (not verified against routes): the sidebar links `/inventory/materials?lab=MICRO|FP` and `/inventory/equipment?lab=MICRO|FP` [src: frontend/src/routes/menuConfig.ts:138-139, 175-176]. Routes themselves do not read `lab`.

---

## MODULE 1: laboratoryConfiguration

All pages below are inside `MainLayout` [src: frontend/src/routes/AppRoutes.tsx:107] and use the guard shown.

### 1.1 Media Preparation & Evaluation: `/laboratory-configuration/media`
- Component: `MediaPage` [src: frontend/src/modules/laboratoryConfiguration/media/MediaPage.tsx:21]
- Guard: PermissionRoute "Media.Prepare" [src: frontend/src/routes/AppRoutes.tsx:157-158]
- Title: "Media Preparation & Evaluation" [src: .../media/MediaPage.tsx:216], subtitle "Prepare media lots and manage their evaluations (GPT, Sterility, Indication/Inhibition, Enrichment Characteristics)." [src: .../media/MediaPage.tsx:217]
- Tabs: "Media Lots" (value lots) [src: .../media/MediaPage.tsx:198], "Approved Media" (value approved) [src: .../media/MediaPage.tsx:199]
- Dialogs/panels: MediaPreparationDialog [src: .../media/dialogs/MediaPreparationDialog.tsx:33]; SelectedMediaLotWorkspace [src: .../media/components/SelectedMediaLotWorkspace.tsx:73] with tabs "Preparation Overview" (:280), "Evaluations (N)" (:281), "Release & Audit Trail" (:282), "Storage" (only if released, :283); MediaLotRegisterTable [src: .../media/components/MediaLotRegisterTable.tsx:33]; MediaLotFilterBar [src: .../media/components/MediaLotFilterBar.tsx:27] (STATUS_OPTIONS array at :19, values not read); MediaLotKpiCards [src: .../media/components/MediaLotKpiCards.tsx:28]; MediaStoragePanel [src: .../media/components/MediaStoragePanel.tsx:44] (buttons "Move" :103, "Cancel" :155; FORMS array :18); MarkOutOfStockDialog [src: .../media/dialogs/MarkOutOfStockDialog.tsx:22].
- Permission-driven UI: canRelease = permissions.includes("Media.Release") [src: .../media/components/SelectedMediaLotWorkspace.tsx:86; .../media/components/MediaLotRegisterTable.tsx:46]. Exact gating of the release button: not enumerated.
- Main fields/buttons/validation: not enumerated.
- Status chips: UNCLEAR (not read).
- Signature: YES. Release decision. SignatureDialog [src: .../media/MediaPage.tsx:346]; confirmDecision(password) [src: .../media/MediaPage.tsx:150-153] calls `MediaPreparationService.decideRelease(lot.id, password, approved)`.
- Endpoints:
  - GET /media [src: .../media/services/MediaPreparationService.ts:4]
  - POST /media (prepare) [src: .../media/services/MediaPreparationService.ts:5]
  - GET /media/awaiting-approval [src: .../media/services/MediaPreparationService.ts:9]
  - POST /media/{mediaId}/release (body: password, approved, comment) [src: .../media/services/MediaPreparationService.ts:11-12]
  - GET /media/{mediaId}/storage [src: .../media/services/MediaPreparationService.ts:14]
  - POST /media/{mediaId}/storage [src: .../media/services/MediaPreparationService.ts:15]
  - POST /media/{mediaId}/storage/{entryId}/move [src: .../media/services/MediaPreparationService.ts:16-17]
  - POST /media/{id}/mark-out-of-stock (body: comment) [src: .../media/dialogs/MarkOutOfStockDialog.tsx:38]
  - GET /media/released (used by masterDataOptions) [src: frontend/src/services/masterDataOptions.ts:220]
- Mock data: none found.

### 1.2 Media lot report: `/media/:id/report`
- Component: `MediaReportPage` [src: .../media/MediaReportPage.tsx:31]. Route [src: frontend/src/routes/AppRoutes.tsx:104], no PermissionRoute.
- Endpoint: GET /media/{id}/summary [src: .../media/MediaReportPage.tsx:40]. Renders SignatureSection [src: .../media/MediaReportPage.tsx:187] (display of signatures, not an action).

### 1.3 Media Configurations: `/laboratory-configuration/media-configurations`
- Component: `MediaConfigurationPage` [src: .../media/MediaConfigurationPage.tsx:21]
- Guard: "MasterData.Manage" [src: frontend/src/routes/AppRoutes.tsx:222-230]. Page has `isManager = permissions.includes("MasterData.Manage")` [src: .../media/MediaConfigurationPage.tsx:23].
- Title: "Media Configurations", subtitle "Configure each dehydrated medium and how it is used." [src: .../media/MediaConfigurationPage.tsx:138-139]
- Components: MediaConfigurationsSection [src: .../media/components/MediaConfigurationsSection.tsx:48] (tooltips "Edit configuration" :207, "Delete configuration" :212); MediaConfigurationDialog [src: .../media/dialogs/MediaConfigurationDialog.tsx:57]; MediaIncubationConditionsSection [src: .../media/components/MediaIncubationConditionsSection.tsx:41] (tooltips :183-206); MediaIncubationConditionDialog [src: .../media/dialogs/MediaIncubationConditionDialog.tsx:24].
- Endpoints (via masterDataOptions): GET/POST/PUT/DELETE /masterdata/media-configurations [src: frontend/src/services/masterDataOptions.ts:238-243]; GET/POST/PUT/DELETE /masterdata/media-incubation-conditions [src: frontend/src/services/masterDataOptions.ts:231-236].
- Signature: none found in this page.

### 1.4 Media Products (rendered inside the Media Configuration area)
- Components: MediaProductList [src: .../media/components/MediaProductList.tsx:19] (tooltips "Rename Product" :171, "Delete Product" :182); MediaProductWorkspace [src: .../media/components/MediaProductWorkspace.tsx:44] with WORKSPACE_TABS overview/conditions/configurations [src: .../media/components/MediaProductWorkspace.tsx:28], tab "Overview" [src: .../media/components/MediaProductWorkspace.tsx:158]; MediaProductFilterBar [src: .../media/components/MediaProductFilterBar.tsx:11].
- Dialogs:
  - AddMediaProductDialog, title "Add Media Product" [src: .../media/dialogs/AddMediaProductDialog.tsx:13, :53]
  - RenameMediaProductDialog [src: .../media/dialogs/RenameMediaProductDialog.tsx:13]; helper text "The code is changed from the Overview tab with an electronic signature." [src: .../media/dialogs/RenameMediaProductDialog.tsx:73]
  - ChangeMediaProductCodeDialog [src: .../media/dialogs/ChangeMediaProductCodeDialog.tsx:14]; buttons "Cancel" :56; SignatureDialog :97.
- Endpoints (masterDataOptions): GET /masterdata/media-products [src: frontend/src/services/masterDataOptions.ts:222]; POST [:224]; PUT /masterdata/media-products/{id} [:226]; PUT /masterdata/media-products/{id}/code (body code, reason, password) [:228]; DELETE [:229].
- Signature: YES, media product code change. handleConfirmSignature(password) [src: .../media/dialogs/ChangeMediaProductCodeDialog.tsx:35-40] -> PUT /masterdata/media-products/{id}/code [src: frontend/src/services/masterDataOptions.ts:228].

### 1.5 Media Evaluation: `/laboratory-configuration/media-evaluation`
- Component: `MediaEvaluationPage` [src: .../mediaEvaluation/MediaEvaluationPage.tsx:26]
- Guard: "TestWorkflow.Execute" [src: frontend/src/routes/AppRoutes.tsx:160-161]
- Title: "Media Evaluation" [src: .../mediaEvaluation/MediaEvaluationPage.tsx:125]; subtitle "Growth Promotion, Indication/Inhibition, and Enrichment Characteristics - auto-assigned on media preparation." [src: same :125]
- Status list constant ["Assigned", "InProgress", "Completed"] [src: .../mediaEvaluation/MediaEvaluationPage.tsx:20]
- Buttons: "Close" :169, "Select" :209, "Record Incubation" :234, "Record Result" :299 [src: same file]
- Dialog: MediaEvaluationWorkflowDialog [src: .../media/dialogs/MediaEvaluationWorkflowDialog.tsx:41] (fields not enumerated)
- Endpoints (MediaEvaluationService): GET /media-evaluations (optional status) [src: .../mediaEvaluation/services/MediaEvaluationService.ts:6]; GET /media-evaluations/{id} [:7]; POST /media-evaluations/challenges/{challengeId}/cryovial [:9]; POST .../lyophilized-disk [:11]; POST .../incubation [:13]; POST .../result [:15].
- Signature: none found.

### 1.6 Cryovials: `/laboratory-configuration/cryovials`
- Component: `CryovialsPage` [src: .../cryovials/CryovialsPage.tsx:25]
- Guard: "Cryovials.Manage" [src: frontend/src/routes/AppRoutes.tsx:163-164]
- Title "Cryovials" [src: .../cryovials/CryovialsPage.tsx:190]; subtitle "Prepare working cryovial batches from an approved lyophilized microorganism material." [src: :191]
- Tabs: "Cryovials" (value register) [src: :172], "Approved Cryovials" (value approved) [src: :173]
- Components: CryovialFilterBar [src: .../cryovials/components/CryovialFilterBar.tsx:17]; CryovialKpiCards [src: .../cryovials/components/CryovialKpiCards.tsx:19]; CryovialReviewTable [src: .../cryovials/components/CryovialReviewTable.tsx:112] (callbacks onApproveClick, onThawClick, onDestroyClick, onMoveStorageClick); PrepareCryovialBatchDialog [src: .../cryovials/components/PrepareCryovialBatchDialog.tsx:53]; DestroyCryovialDialog [src: .../cryovials/components/DestroyCryovialDialog.tsx:23]; MoveCryovialStorageDialog, title "Change Storage Location" [src: .../cryovials/components/MoveCryovialStorageDialog.tsx:14, :51]; ThawVialReasonDialog [src: .../cryovials/components/ThawVialReasonDialog.tsx:23].
- Main fields/buttons/validation: not enumerated.
- Endpoints (CryovialService): GET /cryovials [src: .../cryovials/services/CryovialService.ts:4]; POST /cryovials/prepare [:5]; POST /cryovials/{id}/approve (body approved, password, comment) [:6-7]; POST /cryovials/{id}/destroy [:8]; POST /cryovials/{id}/move-storage [:10]; POST /cryovials/{id}/thaw (body notes) [:11].
- Signature: YES, approve/reject. SignatureDialog [src: .../cryovials/CryovialsPage.tsx:266]; confirmDecision(password) [src: :131-133] -> CryovialService.approve(id, approved, password, comment). Destroy, move-storage and thaw: no password path found in those components.

### 1.7 Cryovial report: `/cryovials/:id/report`
- Component: `CryovialReportPage` [src: .../cryovials/CryovialReportPage.tsx:27]. No PermissionRoute [src: frontend/src/routes/AppRoutes.tsx:105].
- Endpoint: GET /cryovials/{id}/summary [src: .../cryovials/CryovialReportPage.tsx:36]. SignatureSection display [src: :179].

### 1.8 Items: `/laboratory-configuration/items`
- Component: `ItemsPage` [src: .../items/ItemsPage.tsx:11]
- Guard: "Items.Manage" [src: frontend/src/routes/AppRoutes.tsx:219-220]
- Title "Items" [src: .../items/ItemsPage.tsx:191]; subtitle "Configure which tests are auto-assigned when a sample is received." [src: same]
- Components: ItemTable [src: .../items/ItemTable.tsx:48] (tooltips "Edit item" :332, "Delete item" :346); ItemFilterBar [src: .../items/components/ItemFilterBar.tsx:28]; ItemWorkspace [src: .../items/components/ItemWorkspace.tsx:45] with tabs "Overview" :191, "Preparation Configuration" :204, "Audit History" :205; AddItemDialog [src: .../items/components/AddItemDialog.tsx:35]; UploadItemDocumentDialog [src: .../items/components/UploadItemDocumentDialog.tsx:25]; ItemSpecificationsSection [src: .../items/components/ItemSpecificationsSection.tsx:178] (tooltips "Edit Specification" :634, :861; "Delete Specification" :642, :869); SpecificationParameterDialog [src: .../items/components/SpecificationParameterDialog.tsx:106] (DialogTitle :798-812; "Remove Stage" tooltip :1423); ItemPreparationConfigurationSection [src: .../items/components/ItemPreparationConfigurationSection.tsx:21] (canManage = "Items.Manage" :23; "Cancel" button :166); ResidualSolventSpecFields [src: .../items/components/ResidualSolventSpecFields.tsx:12]; TitrationSpecBasisFields [src: .../items/components/TitrationSpecBasisFields.tsx:35] (TITRATION_BASIS_OPTIONS :4); IcpSpecFields [src: .../items/components/IcpSpecFields.tsx:40].
- Endpoints (ItemService): GET /items [src: .../items/services/ItemService.ts:26]; POST /items [:29]; PUT /items/{id} [:32]; DELETE /items/{id} [:35]; PUT /items/{id}/freeze [:38]; PUT /items/{id}/unfreeze [:41].
- Documents (ItemDocumentService): GET /items/{id}/documents [src: .../items/services/ItemDocumentService.ts:50]; POST /items/{id}/documents (multipart) [:69]; GET /item-documents/{id}/content [:121, :145].
- Signature: none found.

### 1.9 Test Master (Micro), `/laboratory-configuration/test-master`
- Component: `TestMasterPage lab="micro"` [src: .../masterDataSimple/TestMasterPage.tsx:35]. Route [src: frontend/src/routes/AppRoutes.tsx:223].
- Guard: "MasterData.Manage" [src: frontend/src/routes/AppRoutes.tsx:222].
- Also routed: `/laboratory-configuration/fp-test-master` (lab fp, area fp) [src: AppRoutes.tsx:224] and `/laboratory-configuration/rm-pm-test-master` (lab fp, area rmpm) [src: AppRoutes.tsx:225]. Same guard.
- Buttons: "Add Test" [src: .../masterDataSimple/TestMasterPage.tsx:659, :691].
- Sub-components: WorkflowStepsSection [src: .../masterDataSimple/testMaster/WorkflowStepsSection.tsx:51] (buttons "Add" :643, "Cancel" :680, "Add Medium" :837; tooltips "Move up" :580, "Move down" :587, "Edit step" :594, "Delete step" :599, "Remove medium" :828; dialog title "Delete Workflow Step" :845); TestStageReplicatesSection [src: .../testMaster/TestStageReplicatesSection.tsx:12] (dialog title "Delete Stage Replicate Configuration" :241; tooltips "Edit Replicates" :151, "Delete Replicate Configuration" :158); TitrationConfigSection [src: .../masterDataSimple/TitrationConfigSection.tsx:23]; TitrationChangeReasonField [src: .../masterDataSimple/TitrationChangeReasonField.tsx:93]; TestAreaField [src: .../masterDataSimple/TestAreaField.tsx:17].
- Warning tooltip text: "This template is missing a required organism or medium (likely inherited from the pre-refactor migration) and will fail validation the first time an analyst runs it. Edit it to complete the configuration." [src: .../testMaster/WorkflowStepsSection.tsx:573]
- Endpoints (masterDataOptions, used via hooks/useTestDefinitions.ts): GET /masterdata/test-definitions [src: frontend/src/services/masterDataOptions.ts:252]; POST [:257]; PUT /masterdata/test-definitions/{id} [:263]; PUT .../{id}/freeze [:266]; PUT .../{id}/unfreeze [:268]; PUT .../{id}/workflow-type [:270]; GET .../{id}/steps [:272]; POST .../{id}/steps [:284]; PUT /masterdata/test-definitions/steps/{stepId} [:286]; PUT .../steps/{stepId}/move [:288]; DELETE .../steps/{stepId} [:289]; GET/POST .../{id}/stage-replicates [:274, :276]; PUT/DELETE .../stage-replicates/{replicateId} [:278, :280]. Hook calls at frontend/src/hooks/useTestDefinitions.ts:76, 91, 102, 108-109.
- Also imports HplcMethodService and IcpMethodService [src: TestMasterPage.tsx:13-14].
- Main fields/validation: not enumerated.
- Signature: none found.

### 1.10 Equation Types: `/laboratory-configuration/equation-types`
- Component: `EquationTypesPage` [src: .../masterDataSimple/EquationTypesPage.tsx:67]. Guard "MasterData.Manage" [src: AppRoutes.tsx:226].
- Title "Equation Types" [src: EquationTypesPage.tsx:103]; subtitle "Predefined mathematical formulas and required calculation parameters for analytical test methods and system suitability." [src: :104]
- Endpoint observed: GET /masterdata/equation-types [src: frontend/src/services/masterDataOptions.ts:251]. No create/update/delete calls found for this page in masterDataOptions (only the GET). UNCLEAR whether the page is read-only.

### 1.11 Organisms: `/laboratory-configuration/organisms`
- Component: `OrganismsPage` [src: .../masterDataSimple/OrganismsPage.tsx:15]. Guard "MasterData.Manage" [src: AppRoutes.tsx:227].
- Title "Organisms" [src: OrganismsPage.tsx:90]; subtitle "The canonical organism list referenced by Media Challenge Specs, Materials, and Cryovials." [src: :91]. Button "Cancel" [src: :109].
- Hook useOrganisms [src: frontend/src/hooks/useOrganisms.ts:22-45]: GET/POST/PUT/DELETE /masterdata/organisms via masterDataOptions [src: frontend/src/services/masterDataOptions.ts:244-249]. Create/update payload fields: scientificName, atccNumber, commonName, description [src: masterDataOptions.ts:246].
- Signature: none found.

### 1.12 Master Data Workbook: `/laboratory-configuration/master-data-workbook`
- Component: `MasterDataWorkbookPage` [src: .../masterDataWorkbook/MasterDataWorkbookPage.tsx:15]. Guard "MasterData.Manage" [src: AppRoutes.tsx:228].
- Title "Master Data Workbook" [src: MasterDataWorkbookPage.tsx:72]; subtitle "Load organisms, departments, rooms, water points, material master, media products, equipment, items and specifications from one Excel workbook." [src: :73]
- Buttons: "Verify" [src: :61], "Download workbook" [src: :74], "Load and sign" [src: :88]. Report table: WorkbookReportTable [src: .../masterDataWorkbook/WorkbookReportTable.tsx:6].
- Endpoints (masterDataWorkbookService.ts, BASE "/masterdata/workbook" [:5]): GET /masterdata/workbook (blob) [:23]; POST /masterdata/workbook/validate [:30]; POST /masterdata/workbook/import (multipart incl. password field) [:34-39]; GET /masterdata/workbook/imports [:44]; POST /masterdata/workbook/imports/{id}/verify (body password, comment) [:48-49]; GET /masterdata/workbook/imports/{id}/file (blob) [:54].
- Signature: YES. SignatureDialog [src: MasterDataWorkbookPage.tsx:6, :100]; load(file, password, comment) [:35-37]; verify(id, password, comment) [:46].

### 1.13 Specifications: `/laboratory-configuration/specifications`
- Component: `SpecificationsPage` [src: .../specifications/SpecificationsPage.tsx:4]. Guard "MasterData.Manage" [src: AppRoutes.tsx:229].
- Title/tabs/buttons: UNCLEAR (not read).
- Endpoints (SpecificationService): GET /masterdata/specifications?itemId= [src: .../specifications/services/SpecificationService.ts:134]; GET /masterdata/specifications/for-test-order/{testOrderId} [:138]; POST /masterdata/specifications [:140]; PUT /masterdata/specifications/{id} [:142]; DELETE /masterdata/specifications/{id} [:143-144].
- Constant PRODUCTION_STAGE_ROLE_OPTIONS [src: SpecificationService.ts:37].

### 1.14 Water: `/laboratory-configuration/water`
- Component: `WaterConfigPage` [src: .../water/WaterConfigPage.tsx:24]. Guard "MasterData.Manage" [src: AppRoutes.tsx:231].
- Title "Water" [src: WaterConfigPage.tsx:142]; subtitle "Water systems, their sample locations, assigned tests and per-location limits." [src: :142]. Button "Retry" [src: :160]. Delete tooltip "Delete water system" [src: :212].
- Components: WaterDepartmentPanel [src: .../water/components/WaterDepartmentPanel.tsx:15]; WaterLocationPanel [src: .../water/components/WaterLocationPanel.tsx:36]; WaterLocationList [src: .../water/components/WaterLocationList.tsx:28] (tooltips "Edit location" :97, "Delete location" :102); WaterPreparationForm [src: .../water/WaterPreparationForm.tsx:18].
- Endpoints (WaterConfigService.ts): /masterdata/water-departments GET/POST/PUT/DELETE [:9-14]; /masterdata/water-sampling-points GET/POST/PUT/DELETE [:16-21]; /masterdata/water-sampling-configurations GET/POST/PUT/DELETE [:24-29]. Prep: WaterPreparationService GET /masterdata/water-departments [src: .../water/services/WaterPreparationService.ts:10].
- Signature: none found.

### 1.15 Environmental Monitoring: `/laboratory-configuration/environmental-monitoring`
- Component: `EMConfigPage` [src: .../environmentalMonitoring/EMConfigPage.tsx:24]. Guard "MasterData.Manage" [src: AppRoutes.tsx:232].
- Title "Environmental Monitoring" [src: EMConfigPage.tsx:129]; subtitle "Departments, their rooms and grades, and per-room test limits." [src: :129]. Button "Retry" [src: :147]. Delete tooltip "Delete department" [src: :198].
- Components: EmDepartmentPanel [src: .../environmentalMonitoring/components/EmPanels.tsx:17]; EmRoomPanel [:80]; EmTestConfigPanel [:152]; EmRoomCard [src: .../components/EmRoomCard.tsx:22] (tooltips "Edit room" :40, "Delete room" :45, "Edit test" :65, "Delete test" :70); GradeChip [src: .../components/GradeChip.tsx:4]; EM_GRADES ["A","B","C","D"] [src: .../emConfigTypes.ts:44]; EM_TEST_TYPES [src: .../emConfigTypes.ts:33].
- Endpoints (EMConfigService.ts): /masterdata/departments GET/POST/PUT/DELETE [:4-9]; /masterdata/rooms GET/POST/PUT/DELETE [:11-16]; /masterdata/room-test-configurations GET/POST/PUT/DELETE [:19-24].
- Preparation (EMPreparationForm [src: .../environmentalMonitoring/EMPreparationForm.tsx:18]): GET /masterdata/rooms [src: .../EMPreparationService.ts:5]; GET /masterdata/room-test-configurations?roomId= [:7]; POST /em/prepare [:9].
- Signature: none found.

### 1.16 After Cleaning: `/laboratory-configuration/after-cleaning`
- Component: `AfterCleaningConfigPage` [src: .../afterCleaning/AfterCleaningConfigPage.tsx:25]. Guard "MasterData.Manage" [src: AppRoutes.tsx:233].
- Title "After Cleaning" [src: AfterCleaningConfigPage.tsx:151]; subtitle "Machines, their parts, and the swab, rinse and pathogen tests run on each part." [src: :151]. Button "Retry" [src: :169]. Tooltip "Delete machine" [src: :216].
- Components: AcMachinePanel [src: .../afterCleaning/components/AcPanels.tsx:18]; AcPartPanel [:73]; AcCountTestPanel [:135]; AcPathogenPanel [:215] (titles "Pathogen tests" :260, "Pathogens tested on this part" :268); AcPartCard [src: .../components/AcPartCard.tsx:24] (tooltips "Rename part" :44, "Delete part" :49, "Edit test" :66, "Remove test" :71); PathogenPicker [src: .../components/PathogenPicker.tsx:7]. Constants: AC_COUNT_TYPES [src: .../afterCleaning/acConfigTypes.ts:30]; PATHOGEN_TEST_TYPE = "Pathogen" [:35].
- Endpoints (AfterCleaningConfigService.ts): /masterdata/machines GET/POST/PUT/DELETE [:4-7]; /masterdata/machine-parts POST/PUT/DELETE [:10-13]; /masterdata/machine-part-configurations GET/POST/PUT/DELETE [:16-21].
- Preparation: AfterCleaningPreparationForm [src: .../afterCleaning/AfterCleaningPreparationForm.tsx:18]; POST /aftercleaning/prepare [src: .../afterCleaning/services/AfterCleaningPreparationService.ts:9]; GET /masterdata/machines [:5].
- Signature: none found.

### 1.17 Receiving Configuration: `/laboratory-configuration/receiving-configuration`
- Component: `ReceivingConfigurationPage` [src: .../masterDataSimple/ReceivingConfigurationPage.tsx:290]. Guard "MasterData.Manage" [src: AppRoutes.tsx:234].
- Title "Receiving Configuration" [src: ReceivingConfigurationPage.tsx:294]; subtitle "Reference lists offered on the sample receiving form, shared across all six receiving categories." [src: :295]
- Sections: "Samplers" [src: :308], "Cause of Testing" [src: :321]. Tooltips "Rename" :254, "Delete" :259.
- Endpoints (masterDataOptions): samplers GET/POST/PUT/DELETE [src: frontend/src/services/masterDataOptions.ts:197-202]; causes-of-testing GET/POST/PUT/DELETE [:191-196].
- Signature: none found.

### 1.18 Equipment: `/laboratory-configuration/equipment`
- Component: `EquipmentPage` [src: .../masterDataSimple/EquipmentPage.tsx:135]. Guard "MasterData.Manage" [src: AppRoutes.tsx:236].
- Title "Laboratory Configuration: Equipment" [src: EquipmentPage.tsx:555]; subtitle "Configure equipment used by this laboratory, set points, and autoclave programs." [src: :556]. IconButton "Refresh data" [src: :559].
- Tabs: "Configured Equipment" [src: :599], "Autoclave Programs / Loads (N)" [src: :600], "Configuration History" [src: :601].
- Dialogs: "Edit Incubator Set Point" [src: :1334]; "Select Equipment from Master Inventory" [src: :1549]. Tooltips "Edit Program" :976, :1096; "View History" :989, :1101.
- Constants: EQUIPMENT_TYPES [src: :53]; FP_ONLY_EQUIPMENT_TYPES = ["Hplc","PhMeter","Balance","IcpOes"] [:68]; CDS_SOFTWARE_OPTIONS [:71].
- Endpoints (EquipmentConfigurationService.ts): GET /masterdata/equipment/configured-summary [:171]; POST /masterdata/equipment/link-inventory/{inventoryId} [:177]; PUT /masterdata/equipment/{id}/set-point [:182]; GET /masterdata/equipment/{id}/set-point-history [:187]; autoclave programs list (URL variable) [:196]; POST/PUT autoclave programs (create/update by req.id) [:205]; PUT /masterdata/equipment/autoclave-programs/{programId}/status [:211]; GET .../autoclave-programs/{programId}/history [:215]; GET /masterdata/equipment [:221]; GET /masterdata/equipment/{id} [:227]; POST /masterdata/equipment [:232]; PUT /masterdata/equipment/{id} [:237].
- Inventory read: EquipmentInventoryService.getAll -> GET /inventory/equipment [src: frontend/src/modules/inventory/equipment/services/EquipmentInventoryService.ts:57] (called from EquipmentPage imports :48).
- Signature: none found.

### 1.19 Physicochemical Instruments: `/laboratory-configuration/fp-instruments`
- Component: `FpInstrumentsPage` [src: .../masterDataSimple/FpInstrumentsPage.tsx:109]. Guard "MasterData.Manage" [src: AppRoutes.tsx:237].
- Title "Physicochemical Instruments" [src: FpInstrumentsPage.tsx:324]; subtitle "Physicochemical laboratory instruments (HPLC, ICP-OES, pH meters, balances). Assets come from the Equipment Inventory." [src: :325]
- FP_INSTRUMENT_TYPES [src: :31].
- Calls: EquipmentConfigurationService (list :135, update :234, create :237) and EquipmentInventoryService.getAll (:136). Endpoints as listed in 1.18.
- Signature: none found.

### 1.20 Chromatography Columns: `/laboratory-configuration/columns`
- Component: `ChromatographyColumnsPage` [src: .../masterDataSimple/ChromatographyColumnsPage.tsx:45]. Guard "MasterData.Manage" [src: AppRoutes.tsx:238].
- Title "Chromatography Columns" [src: ChromatographyColumnsPage.tsx:315]; subtitle "Manage HPLC chromatography column master records, column parameters, and instrument compatibility." [src: :316]. Dialog title "Deactivate Chromatography Column" [src: :543].
- Endpoints (ChromatographyColumnService.ts): GET /masterdata/columns [src: .../masterDataSimple/services/ChromatographyColumnService.ts:64]; GET /masterdata/columns/{id} [:70]; POST [:75]; PUT /masterdata/columns/{id} [:80]; PUT /masterdata/columns/{id}/deactivate [:85].
- Note: the page imports EquipmentConfigurationService [src: ChromatographyColumnsPage.tsx:30]. UNCLEAR which of its calls use that service.
- Signature: none found.

### 1.21 Material Master (Reagents & Reference Standards): `/laboratory-configuration/material-master`
- Component: `MaterialMasterPage` [src: .../masterDataSimple/MaterialMasterPage.tsx:57]. Guard "MasterData.Manage" [src: AppRoutes.tsx:239].
- Title "Reagents & Reference Standards" [src: MaterialMasterPage.tsx:418]; subtitle "Manage master definitions for chemical reagents, indicators, and reference standards across laboratory sections." [src: :419]
- Endpoints (MaterialMasterService.ts): GET /masterdata/material-masters [src: .../masterDataSimple/services/MaterialMasterService.ts:51]; GET {id} [:57]; POST [:62]; PUT {id} [:67]; PUT {id}/active [:72].
- Signature: none found.

### 1.22 Solutions Master: `/laboratory-configuration/solution-master`
- Component: `SolutionMasterPage` [src: .../masterDataSimple/SolutionMasterPage.tsx:36]. Guard "MasterData.Manage" [src: AppRoutes.tsx:240].
- Title "Solutions Master" [src: SolutionMasterPage.tsx:273]; subtitle "Manage master recipes for mobile phases, diluents, and volumetric titrants including component stoichiometry, shelf life, pH specifications, and standardization criteria." [src: :274]
- Dialog SolutionMasterDialog [src: .../solutionMaster/SolutionMasterDialog.tsx:49], dialog title "Reason for Modification" [src: :311]. ReasonDialog [src: .../solutionMaster/ReasonDialog.tsx:29]. Sections: SolutionGeneralSection [src: .../solutionMaster/SolutionGeneralSection.tsx:41], SolutionPhSection [src: .../solutionMaster/SolutionPhSection.tsx:27], SolutionTitrantSection [src: .../solutionMaster/SolutionTitrantSection.tsx:55], SolutionComponentsSection [src: .../masterDataSimple/solutionMaster/SolutionComponentsSection.tsx:35] (tooltips "Move Up" :140, "Move Down" :151, "Remove Component" :162).
- Option lists: SOLUTION_TYPE_OPTIONS [src: .../solutionMaster/solutionForm.ts:52]; SHELF_LIFE_UNIT_OPTIONS [:58]; COMPONENT_UNIT_OPTIONS [:63]; STRENGTH_UNIT_OPTIONS [:72].
- Endpoints (SolutionMasterService.ts): GET /masterdata/solution-masters [src: .../masterDataSimple/services/SolutionMasterService.ts:102]; GET {id} [:108]; POST [:113]; PUT (two call sites, URLs on continuation lines) [:118, :127].
- Signature: "Reason for Modification" dialog present; signature not confirmed (UNCLEAR).

### 1.23 Chromatography Methods (HPLC and GC): `/laboratory-configuration/hplc-methods`
- Component: `HplcMethodsPage` [src: .../masterDataSimple/HplcMethodsPage.tsx:32]. Guard "MasterData.Manage" [src: AppRoutes.tsx:241].
- Title "Chromatography Methods Master" [src: HplcMethodsPage.tsx:177]; subtitle "Manage HPLC and GC methods, column chemistry, carrier gas/oven parameters, mobile phases, and analyte theoretical constants with SST criteria." [src: :178]
- Dialog: HplcMethodDialog [src: .../hplcMethod/HplcMethodDialog.tsx:36]. Tabs for HPLC: "Column" [:189], "Elution & Detection" [:190], "Solutions" [:191], "Analytes (N)" [:192]. Tabs for GC: "Column" [:178], "GC Conditions & Oven" [:179], "Analytes (N)" [:180].
- Sections: HplcGeneralSection [.../hplcMethod/HplcGeneralSection.tsx:35], HplcColumnSection [:27], HplcElutionSection [:45], HplcElutionDetectionTab [:28], HplcDetectionSection [.../hplcMethod/HplcDetectionSection.tsx:26], HplcSolutionsSection [.../hplcMethod/HplcSolutionsSection.tsx:37], HplcAnalytesSection [.../hplcMethod/HplcAnalytesSection.tsx:44], GcConditionsSection [.../hplcMethod/GcConditionsSection.tsx:58], GcOvenProgramTable [.../hplcMethod/GcOvenProgramTable.tsx:28].
- Dialogs: HplcMethodHistoryDialog [.../hplcMethod/HplcMethodHistoryDialog.tsx:35]. Table: HplcMethodTable [.../hplcMethod/HplcMethodTable.tsx:22].
- Option lists: TECHNIQUE_OPTIONS [.../hplcMethod/hplcMethodForm.ts:93]; RESULT_MODE_OPTIONS [:98]; CARRIER_GAS_OPTIONS [:103]; ELUTION_MODE_OPTIONS [:109]; HPLC_DETECTOR_OPTIONS [:114]; GC_DETECTOR_OPTIONS [:123]; DETECTOR_TYPE_OPTIONS [:130]; CHANNELS ["A","B","C","D"] [:135].
- Endpoints (HplcMethodService.ts): GET /masterdata/hplc-methods [src: .../masterDataSimple/services/HplcMethodService.ts:215]; GET {id} [:221]; GET {id}/history [:226]; POST [:232]; PUT (two call sites) [:237, :246].
- Signature: none found.

### 1.24 ICP Methods: `/laboratory-configuration/icp-methods`
- Component: `IcpMethodsPage` [src: .../masterDataSimple/IcpMethodsPage.tsx:28]. Guard "MasterData.Manage" [src: AppRoutes.tsx:242].
- Title "ICP Methods Master" [src: IcpMethodsPage.tsx:157]; subtitle "Manage ICP-OES methods, calibration standard curves, quality control checks, sample preparation defaults, and element emission lines." [src: :158]
- Dialogs: IcpMethodDialog [src: .../icpMethod/IcpMethodDialog.tsx:34]; tabs "General" [:155], "Calibration" [:156], "Optional Checks" [:157], "Sample Prep" [:158], "Elements" [:159]; reason dialog "Reason for Changing Method" [:256]. IcpMethodHistoryDialog [.../icpMethod/IcpMethodHistoryDialog.tsx:35] (button "Close" :84).
- Sections: IcpGeneralSection [.../icpMethod/IcpGeneralSection.tsx:35], IcpCalibrationSection [.../icpMethod/IcpCalibrationSection.tsx:27], IcpOptionalChecksSection [.../icpMethod/IcpOptionalChecksSection.tsx:47], IcpSamplePrepSection [.../icpMethod/IcpSamplePrepSection.tsx:16], IcpElementsTable [.../icpMethod/IcpElementsTable.tsx:46] (tooltips "Move up" :212, "Move down" :224, "Delete element" :236), IcpMethodTable [.../icpMethod/IcpMethodTable.tsx:21].
- Option lists: ICP_MODE_OPTIONS [.../icpMethod/icpMethodForm.ts:43]; ANALYTE_VIEW_OPTIONS [:56].
- Endpoints (IcpMethodService.ts, BASE "/masterdata/icp-methods" [:82]): GET [src: .../masterDataSimple/services/IcpMethodService.ts:87]; GET {id} [:93]; GET {id}/history [:98]; POST [:104]; PUT {id} [:109]; PUT {id}/active (body reason, query value) [:114].
- Signature: "Reason for Changing Method" dialog present; signature not confirmed (UNCLEAR).

### 1.25 Production stages (embedded card, not routed)
- Component: `ProductionStagesCard` [src: .../masterDataSimple/ProductionStagesCard.tsx:39]; tooltips "Rename" :357, "Delete" :366. Used by a page not traced (UNCLEAR which).
- Endpoints (masterDataOptions): GET /masterdata/production-stages [src: frontend/src/services/masterDataOptions.ts:203]; POST [:207]; PUT /masterdata/production-stages/{id} [:213]; DELETE [:214].

### 1.26 Lab config embedded: Test definition helpers
- Titration: TITRATION_TYPE_OPTIONS :10, TITRATION_MODE_OPTIONS :17, TITRATION_CALCULATION_OPTIONS :21, TITRATION_ENDPOINT_OPTIONS :25, DEFAULT_EXPANSION_COEFFICIENT "0.0011" :50 [src: .../masterDataSimple/titrationConfig.ts]
- Test master constants: WORKFLOW_TYPES_BY_LAB :8, STEP_TYPES :54, STEP_TYPES_REQUIRING_ORGANISM :55, STEP_TYPES_WITH_NO_MEDIA :56, STEP_TYPES_SINGLE_MEDIA :61, PHENOTYPIC_TEST_TYPES :63, EQUATION_TYPES :26, ALL_STAGE_ROLES :209 [src: .../masterDataSimple/testMaster/testMasterConstants.ts]

---

## MODULE 2: inventory

### 2.1 Materials Stock: `/inventory/materials`
- Component: `MaterialsPage` [src: frontend/src/modules/inventory/materials/MaterialsPage.tsx:53]
- Guard: "Materials.Manage" [src: frontend/src/routes/AppRoutes.tsx:254-255]
- Error text: "Failed to load materials stock." [src: .../materials/MaterialsPage.tsx:139]
- Table columns (exact labels): "Type" [:238], "Material Name" [:240], "Manufacturer" [:269], "Batch/Lot No." [:271], "Received" [:274], "Expiry" [:276], "Code" [:290], "Location" [:291], "Qty Received" [:292]. Status chip "Not linked to a media product" (StatusBadge status OnHold) [:251].
- Components: AddMaterialDialog [src: .../materials/components/AddMaterialDialog.tsx:35 region]; MaterialLotDetailsDialog; LotPreparationsList; MaterialDocumentList [src: .../materials/components/MaterialDocumentList.tsx] (error "Failed to load documents." [:59]); UploadMaterialDocumentDialog (button "Cancel" :88; "Please select a file." [src: .../UploadMaterialDocumentDialog.tsx:59]); SupersedeMaterialDocumentDialog (button "Cancel" :87); VoidMaterialDocumentDialog (button "Cancel" :50; "A reason is required." helper [src: .../VoidMaterialDocumentDialog.tsx:82]; generic error "Void operation failed. Please try again." [:31]); MaterialFilterBar; MaterialKpiCards.
- AddMaterialDialog validation messages (exact):
  - "Choose the configured media product for this dehydrated media." [src: .../AddMaterialDialog.tsx:581]
  - "Choose the material master entry for this lot." [:586]
  - "Purity must be greater than 0 and less than or equal to 100." [:601]
  - "Moisture content must be at least 0 and below 100." [:607]
  - "Material name, batch/lot number, receiving date, storage location, and quantity received are required." [:621]
  - "Quantity received must be greater than zero." [:626]
  - "Laboratory section is required." [:631]
  - Helpers: "Pick a type or type a new one" [:811]; "Media not listed? A Section Head adds it in Laboratory Configuration > Media Configuration." [:830]; "Purity percentage (0 < p ≤ 100)" [:968]; "Moisture percentage (0 ≤ m < 100, optional)" [:982]
- Endpoints (MaterialService.ts): GET /inventory/materials (optional type) [src: .../materials/services/MaterialService.ts:29]; GET /inventory/materials/usable-reference-standards [:35]; GET /inventory/materials/print [:36]; GET /inventory/materials/default-unit [:38]; POST /inventory/materials [:39]; PUT /inventory/materials/{id} [:40]; GET /inventory/materials/{id}/documents [:44]; POST /inventory/materials/{id}/documents [:50]; GET /inventory/material-documents/{id}/content [:56]; POST /inventory/material-documents/{id}/supersede [:66]; POST (void) [:72]; GET /inventory/materials/{id}/coa-eligibility [:79].
- Signature: none found in inventory module.
- Main fields/buttons: partially enumerated above; full list not enumerated.
- Mock data: none found.

### 2.2 Equipment Inventory: `/inventory/equipment`
- Component: `EquipmentInventoryPage` [src: frontend/src/modules/inventory/equipment/EquipmentInventoryPage.tsx:41]
- Guard: "Equipment.Manage" [src: frontend/src/routes/AppRoutes.tsx:257-258]
- Title "Equipment" [src: EquipmentInventoryPage.tsx:234]; subtitle "QC/Microbiology lab instrument register & active equipment traceability." [src: :235]
- Tabs: "Equipment Register" [src: :249], "Active Equipment" [src: :250] (ActiveEquipmentView [src: .../equipment/components/ActiveEquipmentView.tsx]).
- Table columns: "Type" [:170], "Manufacturer" [:171], "Serial No." [:172], "Firmware" [:173], "Code" [:175], "Location" [:178], "Calibration Due" [:180], "Laboratory" [:207], "Status" [:210] (StatusBadge).
- Row action: "Details and documents" [src: :294].
- Dialogs: RegisterEquipmentDialog [src: .../equipment/components/RegisterEquipmentDialog.tsx] with messages "Instrument type, equipment code, and location are required." [:99], "Choose the laboratory this asset belongs to." [:104], "A comment explaining the operational status change is required." [:109], helper "Required whenever the operational status changes." [:310]. EquipmentDetailsDialog; UploadEquipmentDocumentDialog; SupersedeEquipmentDocumentDialog; VoidEquipmentDocumentDialog; EquipmentDocumentList (error "Failed to load calibration certificates." [src: .../EquipmentDocumentList.tsx:58]).
- Endpoints (EquipmentInventoryService.ts): GET /inventory/equipment [:57]; GET /inventory/equipment/{id} [:58]; GET /inventory/equipment/print [:59]; GET /inventory/equipment/active [:60]; GET .../{id}/activities [:62]; GET .../{id}/history [:64]; GET /inventory/equipment/where-is-it?query= [:66]; POST /inventory/equipment [:67]; PUT /inventory/equipment/{id} [:68]; GET .../{id}/status-history [:71]; GET .../{id}/documents [:74].
- Signature: none found in inventory module.

### 2.3 Approved lists (no own route; redirected from the legacy URLs)
- ApprovedMediaListPage [src: frontend/src/modules/inventory/approvedLists/ApprovedMediaListPage.tsx:114] (`embedded` prop). Title "Approved Media List", subtitle "Prepared media lots that passed GPT and are released for routine use." [src: :361-362, :1058-1059]. KPI labels "Total Media Lots" [:294], "In Stock" [:301], "Expiring Soon" [:308], "Media Formulations" [:315]. Columns: "Lot Number" [:334], "Media Type" [:336], "Prepared By" [:343], "Total Volume" [:344], "Total Weight" [:345], "Manufacturer Lot" [:346], "Manufacturer" [:347], "Prepared On" [:348], "Expiry" [:349], "Status" [:351]. Error "Failed to retrieve approved media register. Please try again." [:164].
- ApprovedCryovialListPage [src: frontend/src/modules/inventory/approvedLists/ApprovedCryovialListPage.tsx:89] (`embedded` prop). Title "Approved Cryovial List" [:303-304, :928-929]; subtitle "Approved, non-destroyed cryovials available for use." [:304]. Columns: "Cryovial Code" [:282], "Organism" [:283], "ATCC No." [:284], "Manufacturer" [:285], "Storage" [:286], "Vials Remaining" [:288], "Expiry" [:291], "Status" [:293]. Tooltips "Thaw a single vial from this approved batch" [:854], "View cryovial report record" [:878]. Error [:125].
- Whether these components are routed directly: UNCLEAR (AppRoutes.tsx has only the redirects).

### 2.4 Lab kit use in inventory
- `components/lab` is imported by EquipmentInventoryPage, MaterialsPage, ActiveEquipmentView, EquipmentFilterBar, EquipmentKpiCards, MaterialFilterBar, MaterialKpiCards (list from grep of `components/lab`). Specific kit components used: not enumerated.

---

## MODULE 3: documentControl

Common: all pages below are inside MainLayout with no PermissionRoute except `/document-control/configuration`.

### 3.1 Dashboard: `/document-control`
- Component: `DocumentControlDashboardPage` [src: frontend/src/modules/documentControl/pages/DocumentControlDashboardPage.tsx:47]. Route [src: AppRoutes.tsx:246]. No PermissionRoute.
- Title "Document Control Dashboard" [src: DocumentControlDashboardPage.tsx:117]; subtitle "Document governance overview, lifecycle metrics, and quality records status" [src: :118].
- Endpoints: UNCLEAR (not traced).

### 3.2 My Reading List: `/document-control/my-reading-list`
- Component: `MyReadingListPage` [src: .../pages/MyReadingListPage.tsx:86]. Route [src: AppRoutes.tsx:247].
- Title "My Reading List" [src: MyReadingListPage.tsx:265]. Tabs from READING_TABS [src: :78] (values ReadingCategory | "ALL"; labels not read). Tooltips "Open Controlled Revision in PDF Viewer" [:671], "Record Legal Read-and-Understand Acknowledgement" [:684].
- Dialog: DocumentAcknowledgementDialog [src: .../components/DocumentAcknowledgementDialog.tsx:35].
  - Error "You must explicitly verify and accept the legal acknowledgement statement." [src: .../DocumentAcknowledgementDialog.tsx:51]
  - Checkbox label "I confirm the evidentiary statement above." [src: :247]
  - The statement text is backend-supplied (context.legalStatementText) [src: :240-242 comment].
- Endpoints (documentAcknowledgementService.ts): GET /document-control/acknowledgements/assignments/{id}/context [:14-15]; POST .../{id}/submit [:24-25]; POST .../{id}/reading-progress [:35-36]; GET .../{id}/status [:45-46].
- Signature/re-auth: none found (no password field in this dialog). Acknowledgement submit is not a password-confirmed action per the code read.

### 3.3 Training Matrix: `/document-control/training-matrix`
- Component: `TrainingMatrixPage` [src: .../pages/TrainingMatrixPage.tsx:59]. Route [src: AppRoutes.tsx:248].
- Title "Training Matrix" [src: TrainingMatrixPage.tsx:246]; subtitle "Multi-axis qualification matrix mapping personnel to controlled document training requirements." [src: :247]
- Tooltips "Not Assigned / Not in Curriculum" [:169], "Qualified on Prior Revision Only: Retraining Gap" [:219].
- Endpoints (trainingMatrixService.ts): GET /document-control/training-matrix/grid [:14]; GET .../kpis [:23]; GET .../users/{userId} [:33]; GET .../documents/{documentMasterId} [:42].
- Signature: none.

### 3.4 Training Compliance Dashboard: `/document-control/compliance-dashboard`
- Component: `ComplianceDashboardPage` [src: .../pages/ComplianceDashboardPage.tsx:37]. Route [src: AppRoutes.tsx:249].
- Title "Training Compliance Dashboard" [src: ComplianceDashboardPage.tsx:65]; subtitle "Executive & operational compliance metrics, departmental ranking, and overdue exceptions." [src: :66]
- Endpoints: UNCLEAR (not traced).

### 3.5 Document Library: `/document-control/library`
- Component: `DocumentLibraryPage` [src: .../pages/DocumentLibraryPage.tsx:57]. Route [src: AppRoutes.tsx:250].
- Title "Document Library" [src: DocumentLibraryPage.tsx:204]; subtitle "Authoritative repository of controlled documents, Standard Operating Procedures, and quality records" [src: :205]
- Tooltips "View Controlled PDF" [:494], "Source File Attached (.docx)" [:506], "View Document Details" [:526], "Void Document Master" [:537].
- Endpoint: GET /document-control/library [src: .../services/documentControlService.ts:36].
- Dialogs: RegisterDocumentDialog [src: .../components/RegisterDocumentDialog.tsx] messages: "Company Document Code and Title are required." [:138]; "Please complete all mandatory selection fields." [:142]; helpers "Must be unique among active documents" [:210], "Default: 01" [:281], "Inherited from Document Type" [:290], "Assigned primary owner" [:307].
- Signature: none.

### 3.6 Document Detail: `/document-control/documents/:id`
- Component: `DocumentDetailPage` [src: .../pages/DocumentDetailPage.tsx:82]. Route [src: AppRoutes.tsx:251].
- Tabs: "Document" [src: DocumentDetailPage.tsx:580], "Revisions (N)" [:581], "Audit trail (N)" [:585].
- Tooltip "Remove this workflow assignment" [:895]; dialog "Remove Role Assignment" [:1404].
- Dialogs and their messages:
  - RegisterDocumentDialog (see 3.5).
  - DraftMetadataDialog [src: .../components/DraftMetadataDialog.tsx]: "Company Document Code and Title are required." [:121]; "Please complete all mandatory selection fields." [:125]; helper "E.g. SOP-QC-001" [:186]; "Current: {owner}" [:265].
  - CreateRevisionDialog [src: .../components/CreateRevisionDialog.tsx]: helper "Must be unique alphanumeric format (1-20 characters). Override is strictly audited." [:179]; "Optional reference to Change Control or Quality Event ticket" [:222].
  - FileUploadDialog [src: .../components/FileUploadDialog.tsx]: "Controlled document file must be in PDF format (.pdf)." [:54]; "Source editable file must be in Word format (.docx or .doc)." [:59]; "Please select a file to upload." [:71].
  - RevisionChangeItemsDialog [src: .../components/RevisionChangeItemsDialog.tsx]: "Description of change is required." [:104]; "Failed to update status." [:159]; CATEGORIES ["Addition","Deletion","Modification","Clarification","Correction","Administrative"] [:49]; button "Close" [:323].
  - RevisionImpactAssessmentDialog [src: .../components/RevisionImpactAssessmentDialog.tsx]: "For every category marked as having an impact, details must contain at least 5 characters." [:133].
  - SubmitForReviewDialog [src: .../components/SubmitForReviewDialog.tsx]: title "Submit for Technical Review" [:110]; "Please select a technical reviewer." [:81]; "Segregation of Duties: Author cannot be assigned as technical reviewer." [:86].
  - TechnicalReviewDrawer [src: .../components/TechnicalReviewDrawer.tsx]: dialogs "Add Review Finding" [:488], "Respond to Review Finding" [:535], "Verify Finding Resolution" [:559]; buttons "Cancel" [:526, :550, :573, :605].
  - AssignmentDialog [src: .../components/AssignmentDialog.tsx]: "Please select a target personnel." [:90]; "Failed to load active personnel directory." [:81]; placeholder "Search by name, username, or title..." [:169].
  - AssignApproverDialog [src: .../components/AssignApproverDialog.tsx]: "Please select a designated approver." [:90]; "Failed to load user list for approver selection." [:82]; helper "If set to a future date, document will be approved in Future Effective state until activation." [:171].
  - CancelDraftDialog [src: .../components/CancelDraftDialog.tsx] (not read).
  - VoidDocumentDialog [src: .../components/VoidDocumentDialog.tsx:26]; placeholder "State the regulatory / administrative justification for voiding this document..." [:100]; no password field found in this file.
  - PeriodicReviewWorkspaceDialog [src: .../components/PeriodicReviewWorkspaceDialog.tsx]: tabs "Review Notes & Findings (N)" [:316], "Independent Review History (N)" [:317], "Outcome & Decision" [:318, shown only if not completed and not author]; "A review summary of at least 10 characters is mandatory." [:142].
  - ApprovalWorkspaceDialog [src: .../components/ApprovalWorkspaceDialog.tsx:62]: tabs "Inspection Dossier" [:301], "Decision Controls" [:316]; "A mandatory justification of at least 10 characters is required for return or decline." [:121]; helper "Leave blank or select today for immediate activation; select a future date for deferred activation." [:833]; placeholders "Optional remarks or quality rationale..." [:848], "Specify the corrections, missing evidence, or changes required..." [:877], "Specify the reason for declining and cancelling this proposed revision..." [:908]; text "requires cryptographic re-authentication with your account password." [:257].
  - Other: ControlledPdfViewer, DualPdfComparisonViewer, RevisionLifecycleStrip (not read).
- Status values: displayed via documentMasterStatusLabel [src: frontend/src/modules/documentControl/documentStatusDisplay.ts:20-27]; statuses named in comment: Draft, InReview, AwaitingApproval, FutureEffective, Effective, Superseded, Obsolete, Cancelled, plus "Void" and "No Revision" [src: documentStatusDisplay.ts:6-9, :21, :26]. The canonical display mapping is in StatusBadge (not read).
- Endpoints:
  - Documents (documentControlService.ts): GET /document-control/documents/{id} [:41]; POST /document-control/documents [:46]; PUT /document-control/documents/{id}/metadata [:55]; POST /document-control/documents/{id}/void [:61]; POST /document-control/revisions/{id}/cancel [:70]; POST /document-control/documents/{masterId}/assignments [:80]; POST .../assignments/{assignmentId}/deactivate [:87]; POST /document-control/revisions/{id}/files [:101]; GET /document-control/files/{id}/download [:108]; GET /document-control/files/{id}/view [:124].
  - Revisions (documentRevisionService.ts): POST /document-control/documents/{masterId}/propose-revision [:20]; POST /document-control/documents/{masterId}/revisions [:31]; GET /document-control/revisions/{id} [:39]; GET /document-control/revisions/{id}/change-items [:46]; POST .../change-items [:56]; PUT /document-control/change-items/{id} [:68]; POST /document-control/change-items/{id}/deactivate [:78]; POST /document-control/revisions/{revisionId}/findings/{findingId}/convert [:86-87]; GET/POST /document-control/revisions/{id}/impact-assessment [:96, :106].
  - Review (documentReviewService.ts): POST /document-control/revisions/{id}/submit-review [:17-18]; GET /document-control/reviews/{id} [:25]; GET /document-control/revisions/{id}/review-tasks [:32]; GET /document-control/reviews/my-tasks [:39]; POST /document-control/reviews/{taskId}/findings [:47]; POST /document-control/findings/{id}/respond [:58]; POST /document-control/findings/{id}/verify [:69]; POST /document-control/findings/{id}/resolve [:77]; POST /document-control/reviews/{taskId}/decide [:87].
  - Approval (documentApprovalService.ts): POST /document-control/revisions/{id}/approvals [:16]; GET /document-control/revisions/{id}/active-approval [:26]; GET /document-control/approvals/{taskId} [:35]; GET /document-control/approvals [:46]; GET .../approvals/{taskId}/dossier [:53]; GET .../readiness [:62]; POST .../decide [:72]; POST .../sign [:83]; GET .../signature [:93].
- Signature (electronic signature): YES, approval sign-off.
  - ApprovalWorkspaceDialog opens ElectronicSignatureDialog (`showSignatureDialog` set true at :126), handleConfirmSignature(password) [src: .../ApprovalWorkspaceDialog.tsx:158-162].
  - ElectronicSignatureDialog [src: .../components/ElectronicSignatureDialog.tsx:36]: error "Password re-authentication is required to apply electronic signature." [:56]; password field placeholder "Account password" [:201]; helper "Per 21 CFR §11.200, individual password confirmation is required at the time of each signing ceremony." [:202]; submit disabled when password empty [:229].
  - Service sign call [src: documentApprovalService.ts:83-84] with a request containing `password` [:81].
  - Other password-bearing types: documentControlTypes.ts:701 (`password?: string | null`), :706 (`password: string`).
- Mock data: none found.

### 3.7 Document Audit Trail: `/document-control/audit`
- Component: `DocumentAuditPage` [src: .../pages/DocumentAuditPage.tsx:36]. Route [src: AppRoutes.tsx:252].
- Title "Document Control Audit Trail" [src: DocumentAuditPage.tsx:109]; subtitle "Chronological, append-only regulatory audit log of document life-cycle actions and configuration changes" [src: :110].
- Endpoints: GET /document-control/audit [documentControlService.ts:228]; GET /document-control/documents/{id}/audit [:236]; POST /document-control/audit/export [:243].

### 3.8 Document Control Configuration: `/document-control/configuration`
- Component: `DocumentConfigurationPage` [src: .../pages/DocumentConfigurationPage.tsx:46].
- Guard: "Documents.ConfigManage" [src: AppRoutes.tsx:268-269].
- Title "Document Control Configuration" [src: DocumentConfigurationPage.tsx:224]; subtitle "System Master Data, Numbering Schemes, Governance Rules, and Module Settings" [src: :225].
- Tabs: "Document Types (N)" [:238], "Departments & Sections (N)" [:239], "Numbering Configuration" [:240], "Module Settings (N)" [:241].
- Dialog "Edit Configuration Setting" [:662]; buttons "Cancel"/"Save" [:595-596, :629-630, :655-656], "Cancel"/"Save Setting" [:677-678]; helpers "E.g. DOC-, SOP-, QA-" [:467], "E.g. 0000000 (7 digits), 00000 (5 digits)" [:474]; tooltip "Edit Type" [:296].
- Endpoints (documentControlService.ts): GET /document-control/config/types [:135]; POST [:142]; PUT /document-control/config/types/{id} [:147]; GET /document-control/config/departments [:152]; POST [:161]; PUT /document-control/config/departments/{id} [:170]; GET /document-control/config/departments/{departmentId}/sections [:178]; POST /document-control/config/sections [:185]; PUT /document-control/config/sections/{id} [:194]; GET /document-control/config/numbering [:199]; PUT /document-control/config/numbering [:206]; GET /document-control/config/settings [:211]; PUT /document-control/config/settings/{key} [:219].
- Signature: none.

### 3.9 Document Control services not routed directly
- Periodic review service (API_BASE "/document-control/periodic-reviews") [src: .../services/periodicReviewService.ts:12]: GET tasks [:21], GET tasks/{id} [:29], GET tasks/{id}/workspace [:35], POST tasks/{id}/findings [:41], PUT tasks/{id}/findings/{findingId}/resolve [:47], POST tasks/{id}/assign [:53], POST tasks/{id}/complete [:59], GET masters/{id}/history [:65], POST trigger-worker [:73].
- Training assignments [src: .../services/trainingAssignmentService.ts]: GET /document-control/training-assignments [:22]; GET .../{assignmentId} [:31]; GET .../my-assignments [:40]; GET .../users/{userId} [:53]; GET .../calculate-due-date [:66]; GET .../superseded-gap-check [:79].

---

## MODULE 4: hplcWorkspace (HPLC and GC share these screens)

GC note: `/gc-workspace/*` routes reuse the HPLC components. The technique is taken from the path (`useTechnique.ts:18-45`; `GC_CONTEXT` basePath "/gc-workspace" [src: frontend/src/modules/hplcWorkspace/useTechnique.ts:41]). GC-specific components: GcMethodDetails [src: .../hplcWorkspace/run/GcMethodDetails.tsx]. No PermissionRoute on either family [src: AppRoutes.tsx:174-197].

### 4.1 Instruments overview: `/hplc-workspace` (and `/gc-workspace`)
- Component: `HplcWorkspacePage` [src: frontend/src/modules/hplcWorkspace/overview/HplcWorkspacePage.tsx:21]. Route [src: AppRoutes.tsx:174, :187].
- Subtitle "Instrument operations, system suitability, and sample testing workflow" [src: HplcWorkspacePage.tsx:104]. Title: UNCLEAR (not found by grep).
- Tabs: "All (N)" [:140], "In use (N)" [:141], "Available (N)" [:142], "Unavailable (N)" [:143].
- Empty state "No instruments found" [:163].
- Component HplcInstrumentCard [src: .../overview/HplcInstrumentCard.tsx].
- Endpoints: GET /hplc-workspace/instruments [HplcWorkspaceService.ts:35]; GET /hplc-workspace/method-options [:41].
- Signature: none on this page.

### 4.2 Instrument workspace: `/hplc-workspace/:instrumentId`, `/hplc-workspace/:instrumentId/run/:runId[/sst|/samples|/evidence|/history]`, `/hplc-workspace/:instrumentId/history`
- Component: `HplcInstrumentWorkspace` [src: .../run/HplcInstrumentWorkspace.tsx:55]. Routes [src: AppRoutes.tsx:175-181, :184].
- Tabs (exact): "Overview" [src: .../run/HplcInstrumentWorkspace.tsx:246], "System Suitability" [:247], "Samples (N)" [:248], "Evidence (N)" [:249], "Run History" [:250].
- Button "Back to Instruments" [src: :198].
- Components: WorkspaceRunHeader [src: .../run/WorkspaceRunHeader.tsx]; WorkflowStepRail [src: .../components/WorkflowStepRail.tsx] with step labels "Run Setup" :49, "System Suitability" :57, "Sample Assignment" :66, "Testing & Evidence" :79, "Review & Complete" :92; SystemSuitabilityPanel [src: .../sst/SystemSuitabilityPanel.tsx]; SstStatusCard [src: .../sst/SstStatusCard.tsx]; SstValuesTable [src: .../sst/SstValuesTable.tsx]; StandardEntryForm [src: .../sst/StandardEntryForm.tsx] (placeholder "Peak area" [:261], helper "Not required for residual solvents (optional)" / "Weighed on analytical balance" [:233]); SampleAssignmentPanel [src: .../samples/SampleAssignmentPanel.tsx]; EligibleSampleTable [src: .../samples/EligibleSampleTable.tsx]; EligibleQualificationTable [src: .../samples/EligibleQualificationTable.tsx]; ChromatogramEvidencePanel [src: .../evidence/ChromatogramEvidencePanel.tsx] (tabs "All Files" :106, "SST" :107, "Sample" :108, "Run" :109); ReportUploadPanel [src: .../evidence/ReportUploadPanel.tsx]; SupersedeEvidenceDialog [src: .../evidence/SupersedeEvidenceDialog.tsx] (label "Supersede Reason" [:126], placeholder "Explain why this report is being replaced..." [:127]); HplcRunHistoryTable [src: .../history/HplcRunHistoryTable.tsx]; HplcReviewPanel [src: .../review/HplcReviewPanel.tsx] (table headers "Replicate", "Actual Weight (mg)", "Peak Response" [:198-200]); HplcReportsSection [src: .../review/HplcReportsSection.tsx] (headers "Report / Context" [:136], "File Name" [:137]).
- Run-abandon reason placeholder: "Document why this run is being abandoned (e.g. system suitability failure, instrument pressure error)..." [src: .../sst/SystemSuitabilityPanel.tsx:382].
- Endpoints: GET /hplc-workspace/instruments/{equipmentId}/runs [HplcWorkspaceService.ts:46]; GET /hplc-workspace/runs/{id} [:55]; GET /hplc-workspace/runs/{id}/eligible-tests [:62]; GET /hplc-workspace/samples/{runSampleId} [:70]; GET /hplc-workspace/test-orders/{testOrderId}/evidence [:81]; GET /hplc-workspace/test-orders/{testOrderId}/dissolution-standard [:89]; GET /hplc-workspace/evidence/{id}/file [:96]; PUT /hplc-workspace/runs/{id}/sst [:126]; POST /hplc-workspace/runs/{id}/sst/confirm [:131]; POST /hplc-workspace/runs/{id}/abandon [:139]; POST /hplc-workspace/runs/{id}/complete [:146]; POST /hplc-workspace/runs/{id}/samples [:153]; POST /hplc-workspace/runs/{id}/samples/{runSampleId}/remove [:160]; POST /hplc-workspace/runs/{runId}/evidence [:227]; POST /hplc-workspace/evidence/{evidenceId}/supersede [:246]; GET /hplc-workspace/runs/{runId}/eligible-qualifications [:186]; POST /hplc-workspace/runs/{runId}/qualifications [:191].
- Signature: YES, SST confirm. SystemSuitabilityPanel imports SignatureDialog [src: .../sst/SystemSuitabilityPanel.tsx:14]; handleConfirmSignature(password) [:175-179]; render [:359]. Call -> POST /hplc-workspace/runs/{id}/sst/confirm [HplcWorkspaceService.ts:131].
- Signature: review submit (see 4.4).

### 4.3 New run wizard: `/hplc-workspace/:instrumentId/new-run`, `/gc-workspace/:instrumentId/new-run`
- Component: `StartHplcRunWizard` [src: .../run/StartHplcRunWizard.tsx:39]. Route [src: AppRoutes.tsx:176, :189].
- Dialog title (template) `Start ${label} Run: {instrument name}` [src: .../run/StartHplcRunWizard.tsx:227]; subtitle "Set up method and column{, and mobile phases} for {code}" [:228].
- Steps: HPLC ["Select Method", "Select Column", "Assign Mobile Phases", "Review & Start"] [src: :36]; GC ["Select Method", "Select Column", "Review & Start"] [:37].
- Step components: WizardMethodStep [src: .../run/WizardMethodStep.tsx], WizardColumnStep [src: .../run/WizardColumnStep.tsx], WizardMobilePhasesStep [src: .../run/WizardMobilePhasesStep.tsx], WizardReviewStep [src: .../run/WizardReviewStep.tsx]; MethodReadOnlyPanel [src: .../run/MethodReadOnlyPanel.tsx].
- Endpoints: GET /hplc-workspace/method-options [HplcWorkspaceService.ts:41]; POST /hplc-workspace/runs [:121].
- Signature: none.

### 4.4 Sample entry: `/hplc-workspace/:instrumentId/run/:runId/sample/:runSampleId`, `/gc-workspace/...` (same)
- Component: `HplcSampleEntryPage` [src: .../entry/HplcSampleEntryPage.tsx:45]. Routes [src: AppRoutes.tsx:182, :195].
- Components: ReplicateEntryTable [src: .../entry/ReplicateEntryTable.tsx]; CalculationSummaryCard [src: .../entry/CalculationSummaryCard.tsx]; WorkingStandardPreviewCard [src: .../entry/WorkingStandardPreviewCard.tsx]; ResidualSolventCalculationView [src: .../components/ResidualSolventCalculationView.tsx]; SendForReviewDialog [src: .../entry/SendForReviewDialog.tsx] (title block at :104-106, text not read).
- Endpoints: GET /hplc-workspace/samples/{runSampleId} [HplcWorkspaceService.ts:70]; PUT /hplc-workspace/samples/{runSampleId}/replicates [:168]; POST /hplc-workspace/samples/{runSampleId}/submit [:176].
- Signature: YES, send for review. SendForReviewDialog imports SignatureDialog [src: .../entry/SendForReviewDialog.tsx:22]; handleSignatureConfirm(password) [:62-74] sends password (lines 68, 74); render [:246]. Caller endpoint: UNCLEAR (service submit call at HplcWorkspaceService.ts:176; service type with password at types.ts:96, :270).

### 4.5 Qualification entry: `/hplc-workspace/:instrumentId/run/:runId/qualification/:runSampleId`, `/gc-workspace/...`
- Component: `QualificationEntryPage` [src: .../entry/QualificationEntryPage.tsx:27]. Routes [src: AppRoutes.tsx:183, :196].
- Endpoints: GET /hplc-workspace/qualification-samples/{runSampleId} [HplcWorkspaceService.ts:196]; PUT .../{runSampleId}/replicates [:201]; POST .../{runSampleId}/submit [:206].
- Signature: none found in this page (UNCLEAR for submit; no password found in QualificationEntryPage grep).

### 4.6 Status badges (HPLC)
- HplcStatusBadge maps raw values to labels [src: .../components/HplcStatusBadge.tsx:12-31]: open -> "Open" (InProgress tone), completed -> "Completed", abandoned -> "Abandoned", pending -> "Pending", passed -> "Passed", failed -> "Failed", available -> "Available", running -> "Running", unavailable -> "Unavailable", assigned -> "Assigned", removed -> "Removed", withinlimits -> "Within Limits", outofspecification -> "OOS", actionlimitexceeded -> "Action Limit Exceeded", alertlimitexceeded -> "Alert Limit Exceeded", requiresreview -> "Requires Review".

### 4.7 Mock data (HPLC)
- none found.

---

## MODULE 5: icpWorkspace

### 5.1 ICP-OES overview: `/icp-workspace`
- Component: `IcpWorkspacePage` [src: frontend/src/modules/icpWorkspace/overview/IcpWorkspacePage.tsx:22]. Route [src: AppRoutes.tsx:200]. No PermissionRoute.
- Title "ICP-OES Workspace" [src: IcpWorkspacePage.tsx:111]; subtitle "Overview of inductively coupled plasma optical emission spectrometers, active analytical runs, and calibrations." [src: :112]
- KPI labels: "Instruments" [:129], "Available" [:130], "In use" [:131], "Cal. pending" [:132], "Unavailable" [:133].
- Tabs: "All (N)" [:149], "In use (N)" [:150], "Available (N)" [:151], "Unavailable (N)" [:152].
- Search placeholder "Search by code, name, run or method..." [:141]; empty "No ICP instruments found" [:173].
- Components: IcpInstrumentCard [src: .../overview/IcpInstrumentCard.tsx]; StartIcpRunDialog [src: .../run/StartIcpRunDialog.tsx:35] with title "Start ICP Run" [:96].
- Endpoints (IcpWorkspaceService.ts): GET /icp-workspace/instruments [:30]; GET /icp-workspace/method-options [:35]; GET /icp-workspace/instruments/{equipmentId}/runs [:40]; POST /icp-workspace/runs [:105].
- Signature: none on this page.

### 5.2 ICP run workspace: `/icp-workspace/:instrumentId`, `/icp-workspace/:instrumentId/run/:runId[/calibration|/ccv|/samples|/evidence|/history]`, `/icp-workspace/:instrumentId/history`
- Component: `IcpRunWorkspace` [src: .../run/IcpRunWorkspace.tsx:38]. Routes [src: AppRoutes.tsx:201-207, :209].
- Tabs (exact): "Overview" [src: .../run/IcpRunWorkspace.tsx:216], "Calibration" [:217], "CCV" [:218, shown only when showCcvTab], "Samples (N)" [:219], "Evidence (N)" [:220], "Run History" [:221].
- Abandon dialog title "Abandon ICP Run" [:279]; field label "Reason for Abandoning *" [:280]; placeholder "Explain why this run is being abandoned (e.g. plasma extinguished, nebulizer clog, out of argon)..." [:281].
- Components: IcpWorkspaceRunHeader [src: .../run/IcpWorkspaceRunHeader.tsx]; IcpMethodReadOnlyPanel [src: .../run/IcpMethodReadOnlyPanel.tsx] (table headers "Symbol" [:188], "Wavelength (nm)" [:189], "Plasma View" [:190], "Conversion Factor" [:191]); IcpWorkflowStepRail [src: .../components/IcpWorkflowStepRail.tsx] labels "Run Setup" :47, "Calibration" :55, "Samples" :64, "Testing & Evidence" :77, "Review & Complete" :90.
- Endpoint: GET /icp-workspace/runs/{id} [IcpWorkspaceService.ts:49]; POST /icp-workspace/runs/{id}/abandon [:128]; POST /icp-workspace/runs/{id}/complete [:135]; POST /icp-workspace/runs/{id}/samples [:142]; POST /icp-workspace/runs/{id}/samples/{runSampleId}/remove [:149]. Reason field for remove: "Reason for Removal *" [src: .../samples/IcpSampleAssignmentPanel.tsx:252], placeholder "Explain why this sample is being removed from the run..." [:253].
- Signature: none on the abandon/remove dialogs found.

### 5.3 Calibration panel (tab Calibration)
- Component: `IcpCalibrationPanel` [src: .../calibration/IcpCalibrationPanel.tsx] (SignatureDialog import :23; render :368; handleConfirmSignature(password) :159-161). Title for report dialog "Calibration Report Document" [:361].
- Components: IcpCalibrationElementTable [src: .../calibration/IcpCalibrationElementTable.tsx] (headers "Symbol" :53, "Wavelength (nm)" :54, "Status" :89, "Failure Reasons" :90; chips "Pass" :208, "Fail" :210, "Pending" :212); IcpCalibrationLotPickers [src: .../calibration/IcpCalibrationLotPickers.tsx] (placeholders "Select Reference Standard Lot" :102, "Select ICV Standard Lot" :135).
- Endpoints: PUT /icp-workspace/runs/{id}/calibration [IcpWorkspaceService.ts:110]; POST /icp-workspace/runs/{id}/calibration/confirm [:115].
- Signature: YES, calibration confirm. Call -> POST .../calibration/confirm with password [IcpWorkspaceService.ts:115-116; types.ts:87, :101].

### 5.4 CCV tab
- Component: `IcpCcvPanel` [src: .../ccv/IcpCcvPanel.tsx]. Element select label "Element" [:179] with placeholder "Select Analyte / Element" [:184]; field "Measured Concentration (mg/L)" [:201]; table headers "Time" [:257], "Element" [:258], "Measured (mg/L)" [:259], "Status" [:261], "Entered By" [:262].
- Endpoint: POST /icp-workspace/runs/{id}/ccv [IcpWorkspaceService.ts:123].

### 5.5 Sample entry: `/icp-workspace/:instrumentId/run/:runId/sample/:runSampleId`
- Component: `IcpSampleEntryPage` [src: .../entry/IcpSampleEntryPage.tsx:40]. Route [src: AppRoutes.tsx:208].
- Section titles: "Replicate Measurements" (step 1) [:293]; "Evidence: Sample Reports" (step 3) [:344]; "Sample Report Upload" [:352]. Label "Amount Unit" [:242]. Badge "Submitted for Review" [:184] (StatusBadge status Completed).
- Components: IcpReplicateEntryTable [src: .../entry/IcpReplicateEntryTable.tsx] (tooltip "Remove Replicate" :246); IcpPreviewCard [src: .../entry/IcpPreviewCard.tsx] (badges "Official Results (Submitted)" :37, "Server Preview (Draft)" :95; empty-state text references button "Save Replicates" :109; table headers "Parameter", "Quantity", "Result", "Unit", "Spec Limit", "Status", "Problem / Warning" :58-62, :122-128); IcpEvidencePanel; IcpReportUploadPanel.
- Endpoints: GET /icp-workspace/samples/{runSampleId} [IcpWorkspaceService.ts:64]; PUT /icp-workspace/samples/{runSampleId}/replicates [:157]; POST /icp-workspace/samples/{runSampleId}/submit [:165].
- Signature: YES, send for review. IcpSendForReviewDialog imports SignatureDialog [src: .../entry/IcpSendForReviewDialog.tsx:22]; handleSignatureConfirm(password) [:59-64]; render [:273]. Caller endpoint: UNCLEAR (submit service method at IcpWorkspaceService.ts:165).
  - Dialog step list (primary text) [src: IcpSendForReviewDialog.tsx:129-240]: "Instrument Calibration", "Element Availability", "Replicate Measurements", "Instrument Report Evidence", "Server Calculation Preview", "Submission Gate". Body text: "Signing will freeze replicate entries, record official ICP assay results, and move the test order to Under Review in the testing workspace." [:251]

### 5.6 Evidence (ICP)
- IcpEvidencePanel [src: .../evidence/IcpEvidencePanel.tsx]: table headers "File Name" :152, "Context" :153, "Kind" :154, "Uploaded By" :155, "Uploaded At" :156, "Status" :157, "Actions" :158; badges "Current" [:195] and "Superseded" [:197]; tooltips "View in New Tab" :202, "Download File" :218, "Supersede File" :235; dialog title "Attach Run Supporting Document" [:261].
- IcpSupersedeEvidenceDialog [src: .../evidence/IcpSupersedeEvidenceDialog.tsx]: label "Reason for Superseding" [:118]; placeholder "Explain why this file is being replaced (e.g. recalculated baseline, re-exported chromatogram)..." [:119].
- IcpReportUploadPanel [src: .../evidence/IcpReportUploadPanel.tsx]: default title "Calibration Report Upload" [:57]; headers "File Name" :221, "Uploaded By" :222, "Uploaded At" :223, "Status" :224, "Actions" :225; badges "Current" :252, "Superseded" :254; tooltips "View File" :259, "Supersede (Replace) File" :276.
- Endpoints: GET /icp-workspace/test-orders/{testOrderId}/evidence [IcpWorkspaceService.ts:71]; GET /icp-workspace/evidence/{id}/file [:80]; POST /icp-workspace/runs/{runId}/evidence [:189]; POST /icp-workspace/evidence/{evidenceId}/supersede [:208].

### 5.7 Sample assignment and eligibility
- IcpSampleAssignmentPanel [src: .../samples/IcpSampleAssignmentPanel.tsx]: headers "Sample Number" :167, "Batch" :168, "Product" :169, "Test Code" :170, "Status" :171, "Actions" :172 (right-aligned).
- IcpEligibleSampleDialog [src: .../samples/IcpEligibleSampleDialog.tsx]: placeholder "Search by sample number, batch, product, or test code..." [:122]; headers "Sample Number" :181, "Batch" :182, "Product" :183, "Test Code" :184, "Stage" :185, "Received At" :186.

### 5.8 Status badges (ICP)
- IcpStatusBadge map [src: .../components/IcpStatusBadge.tsx:10-27]: same as HPLC plus "confirmed" -> "Confirmed" (Completed tone) [:15].

### 5.9 Mock data (ICP)
- none found.

---

## MODULE 6: solutionPreparation

### 6.1 Preparation list: `/preparation`
- Component: `PreparationListPage` [src: frontend/src/modules/solutionPreparation/PreparationListPage.tsx:17]. Route [src: frontend/src/routes/AppRoutes.tsx:167]. Guard "Solutions.Prepare" [src: AppRoutes.tsx:166].
- Title "Solution Preparation" [src: PreparationListPage.tsx:117]; subtitle "Manage and execute preparations for mobile phases, diluents, and volumetric titrants" [:118].
- Search placeholder "Search code, recipe, method, preparer..." [:128].
- Status filter options (exact): "All Statuses", "In Progress", "Prepared", "Expired", "Discarded", "Cancelled" [:141-146]. Type filter options: "All Types", "Mobile Phase", "Diluent", "Titrant" [:157-160].
- Table columns (exact labels): "Code" [:69], "Type" [:78], "Solution Recipe" [:81], "HPLC Method" [:85], "Status" [:88], "Prepared At" [:94], "Expires At" [:101], "Prepared By" [:108].
- Row action label: "Resume Preparation" (goes to /preparation/{id}/edit) [:175] or "View Record" (/preparation/{id}) [:176].
- Empty state title "No solution preparations found" [:179]; button "Start Preparation" -> /preparation/new [:181].
- Endpoints (SolutionPreparationService.ts): GET /solution-preparations [:29-30]; GET /solution-preparations/available [:50-51]; GET /solution-preparations/by-lot/{materialId} [:59-60].
- Signature: none on list.
- Mock data: none found.

### 6.2 New preparation wizard: `/preparation/new`, and resume `/preparation/:id/edit`
- Component: `PreparationWizardPage` [src: .../PreparationWizardPage.tsx:26]. Routes [src: AppRoutes.tsx:168-169]. Guard "Solutions.Prepare".
- Subtitle "Prepare mobile phases, diluents, and titrants from master recipes against stock lots" [src: PreparationWizardPage.tsx:147]. Step labels: UNCLEAR (not extracted).
- Step components:
  - SelectStep [src: .../wizard/SelectStep.tsx:32 props]: label "Solution Type" [:161, :165]; options "Mobile Phase" [:168], "Diluent" [:169], "Titrant" [:170]; labels "Solution Recipe *" [:179], "HPLC Method *" [:199].
  - LotsStep [src: .../wizard/LotsStep.tsx]: labels "Final Volume *" [:226], "Measured pH *" [:247], "Measured pH (Optional)" [:270]; helpers "Recipe target: {N} mL" [:239], "Recipe does not specify a target pH" [:282].
  - ReviewStep [src: .../wizard/ReviewStep.tsx]: headers "Reagent / Material" [:191], "Recipe Target" [:192], "Chosen Stock Lot" [:193], "Quantity Used" [:194].
  - ComponentLotPicker [src: .../components/ComponentLotPicker.tsx]: label "Select Lot *" [:96], "Quantity Used" [:126], helper "Entered in lot's unit (...)" [:147].
- Endpoints: POST /solution-preparations [SolutionPreparationService.ts:80-81]; PUT /solution-preparations/{id} [:91-92]; GET /solution-preparations/{id}/components/{componentId}/lots [:70-71]; GET /solution-preparations/{id} [:38-39].
- Signature: YES (wrapped by SignatureDialog). PreparationWizardPage imports SignatureDialog [src: .../PreparationWizardPage.tsx:12]; handleConfirmSignature(password) [:124-127]; render [:193]. Which endpoint the signed action calls: UNCLEAR (candidate POST /solution-preparations/{id}/complete at SolutionPreparationService.ts:102).

### 6.3 Preparation record: `/preparation/:id`
- Component: `PreparationRecordPage` [src: .../PreparationRecordPage.tsx:24]. Route [src: AppRoutes.tsx:170].
- Field labels: "Code" [:160], "Started By / At" [:166], "Prepared By / At" [:175], "Expires At" [:184]. Reason field "Reason *" [:325].
- Record sub-components:
  - PreparationComponentsTable [src: .../record/PreparationComponentsTable.tsx] headers "Reagent / Material" :28, "Target Quantity" :29, "Consumed Stock Lot" :30, "Quantity Used" :31.
  - PreparationHistoryTable [src: .../record/PreparationHistoryTable.tsx] headers "Transition" :29, "Changed By" :30, "Changed At" :31, "Reason / Note" :32.
  - CurrentFactorCard [src: .../record/CurrentFactorCard.tsx] labels: "Valid" [:25], "Due for Restandardization" [:31], "Restandardize Before Each Use" [:37], "Not Standardized" [:43].
  - StandardizationHistoryTable [src: .../record/StandardizationHistoryTable.tsx] headers "Standard Lot" :50, "Weight (mg)" :51, "Reference Solution" :56, "Ref Volume (mL)" :57, "Ref Factor" :58, "Titrant Vol (mL)" :61, "Blank (mL)" :62, "Calculated Factor" :63, "Date & Time" :199, "Analyst" :200, "Mode" :201, "Mean Factor" :202, "Status" :204, "Valid Until" :205.
  - useTitrantStandardization hook [src: .../record/useTitrantStandardization.ts].
- Endpoints: GET /solution-preparations/{id} [SolutionPreparationService.ts:38]; GET /solution-preparations/{id}/standardizations [:134]; GET /solution-preparations/{id}/standardizations/standard-lots [:142]; GET .../standardizations/reference-options [:152].
- Status: see 6.5.

### 6.4 Titrant standardization dialog (opened from the record)
- Component: `TitrantStandardizationDialog` [src: .../components/TitrantStandardizationDialog.tsx]. SignatureDialog import [:12]; handleConfirmSignature(password) [:186-200]; render [:311]. Button "Cancel" [:237]. Field "Temperature (°C)" [:273]; helper "Optional. Needed later for non-aqueous temperature volume correction." [:276].
- ReplicateRowInputCard [src: .../components/ReplicateRowInputCard.tsx]: labels "Standard Lot" (:53, :55), "Reference Solution" (:69, :71), "Weight (mg)" [:88], "Ref Vol (mL)" [:98], "Titrant Vol (mL)" [:109], "Blank (mL)" [:120].
- StandardizationResultView [src: .../components/StandardizationResultView.tsx] headers "Titrant Vol (mL)" [:97], "Blank (mL)" [:98, conditional], "Calculated Factor" [:99].
- Endpoints: GET /solution-preparations/{id}/standardizations/standard-lots [SolutionPreparationService.ts:142-143]; POST /solution-preparations/{id}/standardizations [:163-164].
- Signature: YES (wraps the standardization submit). Caller-to-endpoint mapping: UNCLEAR (POST at :163-164 is the candidate).

### 6.5 Status values (solution preparations)
- PreparationStatusBadge [src: .../components/PreparationStatusBadge.tsx:25-51]: "In Progress", "Prepared", "Expired", "Discarded", "Cancelled". Unknown values render raw [:53-57].
- Type values in list: "MobilePhase", "Diluent", "Titrant" [src: PreparationListPage.tsx:158-160; SelectStep.tsx:168-170].

### 6.6 Mock data (solution preparation)
- none found.

---

## MODULE 7: workingStandards

### 7.1 Working Standards: `/working-standards`
- Component: `WorkingStandardsPage` [src: frontend/src/modules/workingStandards/WorkingStandardsPage.tsx:27]. Route [src: frontend/src/routes/AppRoutes.tsx:211]. No PermissionRoute.
- Title "Working Standards" [src: WorkingStandardsPage.tsx:113]; subtitle "Working standard lots, qualifications, moisture verification and sign-off" [:114].
- KPI labels: "Active Lots" [:103], "Valid" [:104], "Due Soon" [:105], "Expired / Depleted" [:106], "Pending Quals" [:107].
- Tabs: "Lots (N)" [:148], "Qualifications (N)" [:149].
- Button tooltip "Create initial qualification" when permitted, otherwise "Permission required: WorkingStandards.Qualify" [:127]. Permission constant WORKING_STANDARDS_QUALIFY = "WorkingStandards.Qualify" [src: frontend/src/routes/routes.ts:248]. Route itself has no PermissionRoute.
- Endpoints (WorkingStandardService.ts): GET /working-standards/lots [:23]; GET /working-standards/qualifications [:28]; GET /working-standards/qualifications/{id} [:33]; GET /working-standards/source-samples [:40-41]; POST /working-standards/qualifications [:48-49]; PUT /working-standards/qualifications/{id} [:59-60]; POST /working-standards/qualifications/{id}/documents [:75-76]; GET /working-standards/documents/{documentId}/file [:86]; POST .../{id}/review [:112-113]; POST .../{id}/return [:123-124]; POST .../{id}/reject-review [:134-135]; POST .../{id}/approve [:145-146]; POST .../{id}/reject-approval [:156-157].
- Signature: YES, qualification sign-off. SignOffDialog [src: .../qualifications/SignOffDialog.tsx] — title default "Electronic Signature" [:59]; password field "Password" (type password) [:197-199]; isValid requires password when isPasswordRequired [:87]; the password is sent in each action branch [:99-117]. Action-to-endpoint mapping per branch is in the service calls at WorkingStandardService.ts:112, :123, :134, :145, :156.
  - Note: this dialog is its own password dialog, not the shared SignatureDialog component.

### 7.2 Lots table
- WorkingStandardLotsTable [src: .../lots/WorkingStandardLotsTable.tsx]: headers "Code" :48, "Material" :49, "Batch" :50, "Remaining (g)" :53, "Expiry" :54, "Status" :55, "Actions" :56.

### 7.3 Qualifications table and drawer
- QualificationsTable [src: .../qualifications/QualificationsTable.tsx]: headers "Code" :44, "Kind" :45, "Material" :46, "Batch" :47, "Status" :48, "Created" :50.
- QualificationDetailDrawer [src: .../qualifications/QualificationDetailDrawer.tsx:33]. Parts: QualificationHeader [src: .../drawer/QualificationHeader.tsx] (IconButton aria-label "Close" :39); QualificationSourceSection [src: .../drawer/QualificationSourceSection.tsx] labels "Master Entry" :21, "Source Material & Batch" :31, "Source Sample Reference" :42, "Working Standard Lot" :51, "Quantity" :59, "Location" :66, "Moisture Content" :73; QualificationDocumentsSection [src: .../drawer/QualificationDocumentsSection.tsx] (tooltip "View document" :103; select label "Type" :156, options "Source Report" :163, "Moisture Report" :164); QualificationResultsSection [src: .../drawer/QualificationResultsSection.tsx]; QualificationSignOffSection [src: .../drawer/QualificationSignOffSection.tsx].

### 7.4 New qualification dialog
- NewQualificationDialog [src: .../qualifications/NewQualificationDialog.tsx]: button "Cancel" [:164]; field "Moisture %" [:211].
- InitialQualificationFields [src: .../qualifications/newDialog/InitialQualificationFields.tsx]: radio labels "Manual" [:66], "Received sample" [:67]; "Source Sample" with placeholder "Search received samples..." [:79]; "Name" [:87]; "Batch" [:95]; "Master Entry" [:104, :108]; "Quantity (g)" [:125]; "Location" [:133].
- Endpoint: POST /working-standards/qualifications [WorkingStandardService.ts:48-49].
- Signature: none on create (UNCLEAR).

### 7.5 Mock data (working standards)
- none found.

---

## MODULE 8: documentControl additional components (see also 3.6)
- Acknowledgement: DocumentAcknowledgementDialog (3.2).
- Revision/approval flows: ApprovalWorkspaceDialog and ElectronicSignatureDialog (3.6).
- Periodic review: PeriodicReviewWorkspaceDialog (3.6).

---

## Signature-triggering actions

All of these show an electronic signature or password re-entry before the call.

| # | Screen / component | Trigger | Dialog | Endpoint (method and URL) | Source |
|---|---|---|---|---|---|
| 1 | Master Data Workbook | "Load and sign" button | SignatureDialog | POST /masterdata/workbook/import (password field) | MasterDataWorkbookPage.tsx:88, :100; masterDataWorkbookService.ts:34-39 |
| 2 | Master Data Workbook | "Verify" button | SignatureDialog | POST /masterdata/workbook/imports/{id}/verify | MasterDataWorkbookPage.tsx:61, :46; masterDataWorkbookService.ts:49 |
| 3 | Media Preparation | Lot release decision (approve/reject) | SignatureDialog | POST /media/{id}/release | MediaPage.tsx:346, :150-153; MediaPreparationService.ts:11-12 |
| 4 | Media product | Change product code | SignatureDialog | PUT /masterdata/media-products/{id}/code | ChangeMediaProductCodeDialog.tsx:35-40, :97; masterDataOptions.ts:228 |
| 5 | Cryovials | Approve / reject | SignatureDialog | POST /cryovials/{id}/approve | CryovialsPage.tsx:131-133, :266; CryovialService.ts:6-7 |
| 6 | HPLC run | SST confirm | SignatureDialog | POST /hplc-workspace/runs/{id}/sst/confirm | SystemSuitabilityPanel.tsx:14, :175-179, :359; HplcWorkspaceService.ts:131 |
| 7 | HPLC sample | Send for review (submit) | SignatureDialog | POST /hplc-workspace/samples/{id}/submit (candidate; UNCLEAR) | SendForReviewDialog.tsx:22, :62-74, :246; HplcWorkspaceService.ts:176 |
| 8 | ICP run | Calibration confirm | SignatureDialog | POST /icp-workspace/runs/{id}/calibration/confirm | IcpCalibrationPanel.tsx:23, :159-161, :368; IcpWorkspaceService.ts:115 |
| 9 | ICP sample | Send for review (submit) | SignatureDialog | POST /icp-workspace/samples/{id}/submit (candidate; UNCLEAR) | IcpSendForReviewDialog.tsx:22, :59-64, :273; IcpWorkspaceService.ts:165 |
| 10 | Solution preparation wizard | Sign and complete (candidate) | SignatureDialog | UNCLEAR (candidate POST /solution-preparations/{id}/complete) | PreparationWizardPage.tsx:12, :124-127, :193; SolutionPreparationService.ts:102 |
| 11 | Titrant standardization | Submit standardization | SignatureDialog | UNCLEAR (candidate POST /solution-preparations/{id}/standardizations) | TitrantStandardizationDialog.tsx:12, :186-200, :311; SolutionPreparationService.ts:163 |
| 12 | Working standard qualification | Review / return / reject-review / approve / reject-approval | SignOffDialog (own password field) | POST /working-standards/qualifications/{id}/review, /return, /reject-review, /approve, /reject-approval | SignOffDialog.tsx:36, :87, :99-117, :197-199; WorkingStandardService.ts:112-157 |
| 13 | Document approval | Sign approval (sign-off) | ElectronicSignatureDialog (via ApprovalWorkspaceDialog) | POST /document-control/approvals/{taskId}/sign | ApprovalWorkspaceDialog.tsx:126, :158-162, :257, :944; ElectronicSignatureDialog.tsx:36, :56, :201-202; documentApprovalService.ts:81-84 |

Not signature-triggering despite a permission gate or confirmation: cryovial destroy/move/thaw (CryovialService.ts:8, :10, :11), media mark-out-of-stock (MarkOutOfStockDialog.tsx:38), document acknowledgement submit (documentAcknowledgementService.ts:24), document void (VoidDocumentDialog.tsx; documentControlService.ts:61), equipment and material dialogs (inventory grep found no password field), document configuration saves (documentControlService.ts:135-219). Source for "no password path found": grep of the module folders for password/signature/reauth.

---

## Mock or placeholder data found

Mock, fixture, stub, TODO, FIXME, dummy or "coming soon" data in scope: **none found** (repo-wide grep over frontend/src excluding tests).

The following matches were checked and are NOT mock data:
- `frontend/src/modules/documentControl/components/DocumentAcknowledgementDialog.tsx:244` - code comment about hardcoded wording (the statement text is backend-supplied).
- `frontend/src/modules/laboratoryConfiguration/masterDataSimple/ReceivingConfigurationPage.tsx:289` - code comment: lists were formerly hardcoded in masterDataOptions.ts and are now served by the backend.
- `frontend/src/modules/laboratoryConfiguration/masterDataSimple/testMaster/WorkflowStepsSection.tsx:48` - code comment about a removed per-test-code hardcoded chain.
- `placeholder=` attributes across scope - UI input hints only.

Hardcoded option lists in scope (UI enumerations, not sample data):
- `frontend/src/modules/laboratoryConfiguration/masterDataSimple/EquipmentPage.tsx:53, :68, :71` (EQUIPMENT_TYPES, FP_ONLY_EQUIPMENT_TYPES, CDS_SOFTWARE_OPTIONS)
- `frontend/src/modules/laboratoryConfiguration/masterDataSimple/FpInstrumentsPage.tsx:31` (FP_INSTRUMENT_TYPES)
- `frontend/src/modules/laboratoryConfiguration/environmentalMonitoring/emConfigTypes.ts:33, :44` (EM_TEST_TYPES, EM_GRADES)
- `frontend/src/modules/laboratoryConfiguration/afterCleaning/acConfigTypes.ts:30, :35` (AC_COUNT_TYPES, PATHOGEN_TEST_TYPE)
- `frontend/src/modules/laboratoryConfiguration/masterDataSimple/testMaster/testMasterConstants.ts:8, :26, :54-63, :209` (workflow, step, phenotypic, equation, stage role constants)
- `frontend/src/modules/laboratoryConfiguration/masterDataSimple/titrationConfig.ts:10-50`
- `frontend/src/modules/laboratoryConfiguration/masterDataSimple/hplcMethod/hplcMethodForm.ts:93-135`
- `frontend/src/modules/laboratoryConfiguration/masterDataSimple/icpMethod/icpMethodForm.ts:43, :56`
- `frontend/src/modules/laboratoryConfiguration/masterDataSimple/solutionMaster/solutionForm.ts:52-72`
- `frontend/src/modules/laboratoryConfiguration/media/components/MediaLotFilterBar.tsx:19` (STATUS_OPTIONS), `.../media/components/MediaStoragePanel.tsx:18` (FORMS)
- `frontend/src/modules/laboratoryConfiguration/mediaEvaluation/MediaEvaluationPage.tsx:20` (STATUSES)
- `frontend/src/modules/laboratoryConfiguration/specifications/services/SpecificationService.ts:37` (PRODUCTION_STAGE_ROLE_OPTIONS)
- `frontend/src/modules/documentControl/components/RevisionChangeItemsDialog.tsx:49` (CATEGORIES)
- `frontend/src/modules/documentControl/pages/MyReadingListPage.tsx:78` (READING_TABS)
- `frontend/src/modules/hplcWorkspace/run/StartHplcRunWizard.tsx:36-37` (step lists)
- `frontend/src/modules/laboratoryConfiguration/water/WaterConfigPage.tsx:17` (NO_CONFIGS = [] empty array)

---

## Could not read / not enumerated

- Field-level labels and buttons for most dialogs and drawers (listed by file only).
- Titles for SpecificationsPage, HplcWorkspacePage, StartHplcRunWizard tabs/steps beyond the step list, DocumentControlDashboardPage content, ComplianceDashboardPage content, DocumentDetailPage tabs beyond those listed, and MaterialsPage title (no title string found by grep).
- Endpoints for DocumentControlDashboardPage, ComplianceDashboardPage (not traced).
- Which endpoint the HPLC, ICP and Solution Preparation signature dialogs call (marked UNCLEAR).
- Status chip value sets for MediaLotFilterBar, CryovialFilterBar, EquipmentFilterBar, MaterialFilterBar (only the option constants exist; values not read).
- Components under `frontend/src/components/lab` were identified only by import, not read. The shared `SignatureDialog` (`frontend/src/components/SignatureDialog.tsx`) was read only for its title "Electronic Signature" (:52), comment field (:71) and password field (:74).
- Route-level guard coverage in `frontend/src/routes/menuConfig.ts` was not analysed beyond the links quoted above.
