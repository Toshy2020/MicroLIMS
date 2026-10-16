# C7 General masters

## modules

### MOD-ITEM-001 Items
- Purpose: Master configuration for tested catalog items across Product, Raw Material, and Packaging Material categories; defines item identity (name, code, category, SOP number), tests auto-assigned upon sample receipt, controlled document management (SOP and Verification Report), and item-specific microbiology preparation configurations (technique, amount, filtration/washing volumes, diluent, neutralizer) with Section Head sign-off; supports active/frozen lifecycle gating and delete restriction safeguards. [src: frontend/src/modules/laboratoryConfiguration/items/ItemsPage.tsx:L11-L202; backend/MicroLIMS.Application/Services/ItemService.cs:L12-L130; backend/MicroLIMS.Application/Services/ItemPreparationConfigurationService.cs:L26-L99; backend/MicroLIMS.Application/Services/ItemDocumentService.cs:L17-L115]
- Sub-features: SCR-ITEM-001. Status: IMPLEMENTED. Lab scope: shared across all laboratories (MICRO and Physicochemical FP / RM&PM); sample receipt evaluates assigned tests regardless of lab. [src: backend/MicroLIMS.Domain/Entities/Item.cs:L8-L34; backend/MicroLIMS.Application/Services/ItemService.cs:L18-L23]

### MOD-SPEC-001 Specifications (stand-alone page)
- Purpose: Routed URL destination (`/laboratory-configuration/specifications`) intended as a dedicated specification management interface; in code it redirects immediately (`<Navigate to="/laboratory-configuration/items" replace />`) to the Items page where specifications are configured inside the item workspace. Status: IMPLEMENTED (as redirect; see GAP-034 and GAP-702). Lab scope: none directly (redirects to Items). [src: frontend/src/modules/laboratoryConfiguration/specifications/SpecificationsPage.tsx:L4-L6; frontend/src/routes/AppRoutes.tsx:L229]
- Sub-features: SCR-SPEC-001. Status: IMPLEMENTED (Redirect only).

### MOD-RECVCFG-001 Receiving Configuration
- Purpose: Master reference lists offered as selection choices on the sample receiving form across all six receiving categories; manages Samplers (free suggestion picklist), Cause of Testing (mandatory foreign key with delete protection against referenced samples), and Production Stages (name and role mapping: Bulk, InProcess, Finished, Stability, Other). Status: IMPLEMENTED. Lab scope: global across all receiving categories and laboratories. [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ReceivingConfigurationPage.tsx:L49-L338; backend/MicroLIMS.Application/Services/MasterData/ReferenceListMasterDataService.cs:L14-L151]
- Sub-features: SCR-RECVCFG-001. Status: IMPLEMENTED.

### MOD-WORKBOOK-001 Master Data Workbook
- Purpose: Bulk data onboarding and migration tool using an Excel workbook (`.xlsx`); allows downloading current master data with instruction and choices sheets, dry-run checking (validation report table with sheet summaries and row-level errors), signed loading with electronic signature (`SignatureMeaning.MasterDataChanged`), and second-person verification (`SignatureMeaning.Reviewed`) that activates newly loaded items and materials; strictly add-only, transactions with savepoints per row, max 10 MB file size, max 5,000 new rows. Status: IMPLEMENTED. Lab scope: cross-laboratory (Microbiology, Physicochemical FP and RM&PM). [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L12-L114; backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L16-L284; backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookRunner.cs:L6-L122]
- Sub-features: SCR-WORKBOOK-001. Status: IMPLEMENTED.

### MOD-TESTMASTER-001 Test Master (FP and RM & PM)
- Purpose: Canonical Test Master defining test codes, display names, owning laboratory section, workflow types, equation types, system suitability requirements, condition fields, replicate counts, stage-specific replicates (per ProductionStageRole), and specialized testing regimes (HPLC method assays, ICP method assays, titration configurations with reason-audited changes, dissolution S1-S3 stage offsets, disintegration multi-stage unit limits, and USP <2091> weight variation criteria). Status: IMPLEMENTED. Lab scope: Physicochemical Laboratory (`lab="fp"`), scoped by area: finished products (`area="fp"`) and raw/packaging materials (`area="rmpm"`). [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/TestMasterPage.tsx:L24-L140; backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L15-L820; backend/MicroLIMS.Application/Services/MasterData/TestStageReplicateMasterDataService.cs:L13-L110]
- Sub-features: SCR-TESTMASTER-002 (FP Test Master), SCR-TESTMASTER-003 (RM & PM Test Master). Status: IMPLEMENTED.

## screens

### MOD-ITEM-001 Items
- SCR-ITEM-001 Items. Route `/laboratory-configuration/items` [src: frontend/src/routes/AppRoutes.tsx:L220; frontend/src/modules/laboratoryConfiguration/items/ItemsPage.tsx:L11].
  - Who can open: Users holding permission `Items.Manage` (`PERMISSIONS.ITEMS_MANAGE`) [src: frontend/src/routes/AppRoutes.tsx:L219; documentation/knowledge/_raw/verified-roles-permissions.md:L86].
  - Page header: Title "Items", Subtitle "Configure which tests are auto-assigned when a sample is received." [src: frontend/src/modules/laboratoryConfiguration/items/ItemsPage.tsx:L191].
  - Primary button: "Add Item" (startIcon `<AddIcon />`) opens `AddItemDialog` [src: frontend/src/modules/laboratoryConfiguration/items/ItemsPage.tsx:L192-L200].
  - Filter bar (`ItemFilterBar`):
    - Search field: placeholder "Search by name, code, SOP..." (searches `item.name`, `item.code`, `item.sopNumber` case-insensitively) [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemFilterBar.tsx:L40-L48; frontend/src/modules/laboratoryConfiguration/items/ItemsPage.tsx:L51-L57].
    - Category select: options "All Categories" (`ALL`), "Product" (`FinishedProduct`), "Raw Material" (`RawMaterial`), "Packaging Material" (`PackagingMaterial`) [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemFilterBar.tsx:L50-L66].
    - Status select: options "All Statuses" (`ALL`), "Active" (`Active`), "Frozen" (`Frozen`) [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemFilterBar.tsx:L68-L82].
    - Button "Reset Filters": visible only when filters are active [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemFilterBar.tsx:L84-L92].
  - Register layout: Split workspace layout when an item is selected (Left: 35-40% compact register `ItemTable compact`; Right: 60-65% detailed workspace `ItemWorkspace`). Full-width register when no item is selected. [src: frontend/src/modules/laboratoryConfiguration/items/ItemsPage.tsx:L235-L315].
  - Register card items (`ItemTable` / `ItemRowCard`):
    - Item Name, Code in parentheses (`{item.name} ({item.code})`) [src: frontend/src/modules/laboratoryConfiguration/items/ItemTable.tsx:L252-L256].
    - Badges/Chips: `CategoryBadge` (`item.category`), `StatusBadge` ("Active" / "Frozen"), Chip `"{N} Test" / "{N} Tests"`, Chip `"{N} Spec" / "{N} Specs"`, Chip `"{N} Doc" / "{N} Docs"`, Chip "⚠ Legacy category" (if category is not one of `FinishedProduct`, `RawMaterial`, `PackagingMaterial`) [src: frontend/src/modules/laboratoryConfiguration/items/ItemTable.tsx:L266-L321].
    - Action buttons per card:
      - "Edit item" (icon `<EditIcon />`, aria-label `Edit {item.name}`) -> opens `AddItemDialog` in edit mode [src: frontend/src/modules/laboratoryConfiguration/items/ItemTable.tsx:L332-L336].
      - "Freeze item" / "Unfreeze item" (icon `<BlockIcon />` if active, `<LockOpenIcon />` if frozen; aria-label `Freeze {item.name}` / `Unfreeze {item.name}`) -> toggles active state [src: frontend/src/modules/laboratoryConfiguration/items/ItemTable.tsx:L337-L345; frontend/src/modules/laboratoryConfiguration/items/ItemsPage.tsx:L162-L176].
      - "Delete item" (icon `<DeleteIcon />`, color "error", aria-label `Delete {item.name}`) -> opens `ConfirmationDialog` [src: frontend/src/modules/laboratoryConfiguration/items/ItemTable.tsx:L346-L355].
  - Delete Confirmation Dialog:
    - Title/Message: `Delete item "{pendingDelete.name}" ({pendingDelete.code})? This cannot be undone. If it has already been used to receive samples, deletion will be blocked - freeze it instead.` [src: frontend/src/modules/laboratoryConfiguration/items/ItemTable.tsx:L150-L153].
    - Buttons: Cancel, Confirm (destructive) [src: frontend/src/modules/laboratoryConfiguration/items/ItemTable.tsx:L146-L159].
  - Pagination (`TablePagination`): rows per page options `[10, 25, 50]`, label "Items per page:" [src: frontend/src/modules/laboratoryConfiguration/items/ItemTable.tsx:L46, L126-L143].
  - Empty states:
    - When no items match filters: Icon `<Inventory2OutlinedIcon />`, title "No items match your filters", description "Try a different search term or clear the filters.", button "Clear filters" [src: frontend/src/modules/laboratoryConfiguration/items/ItemTable.tsx:L81-L94].
    - When no items exist in database: Icon `<Inventory2OutlinedIcon />`, title "No items configured yet", description "Use “Add Item” to create the first one." [src: frontend/src/modules/laboratoryConfiguration/items/ItemTable.tsx:L81-L89].
  - Add / Edit Item Dialog (`AddItemDialog`):
    - Title: "Add New Item" or `Edit Item: {itemToEdit.name}` [src: frontend/src/modules/laboratoryConfiguration/items/components/AddItemDialog.tsx:L91].
    - Fields:
      - "Item Name" (text, required) [src: frontend/src/modules/laboratoryConfiguration/items/components/AddItemDialog.tsx:L107-L113].
      - "Item Code" (text, required) [src: frontend/src/modules/laboratoryConfiguration/items/components/AddItemDialog.tsx:L116-L123].
      - "SOP Number" (text, optional) [src: frontend/src/modules/laboratoryConfiguration/items/components/AddItemDialog.tsx:L124-L130].
      - "Category" (select, default "FinishedProduct"; options: "Product" (`FinishedProduct`), "Raw Material" (`RawMaterial`), "Packaging Material" (`PackagingMaterial`)) [src: frontend/src/modules/laboratoryConfiguration/items/components/AddItemDialog.tsx:L29-L33, L137-L143].
      - "Assigned Tests (Auto-assigned on sample receipt)" (`TestCodePickerMulti`, required: at least 1 test) [src: frontend/src/modules/laboratoryConfiguration/items/components/AddItemDialog.tsx:L146-L151, L63-L66].
    - Buttons: "Cancel", "Save Item" / "Save Changes" / "Saving..." [src: frontend/src/modules/laboratoryConfiguration/items/components/AddItemDialog.tsx:L94-L99].
  - Item Workspace Panel (`ItemWorkspace`):
    - Workspace header: `{item.name} ({item.code})`, `CategoryBadge`, `StatusBadge`, Chip `SOP: {item.sopNumber}`, close button (aria-label "Close workspace", title "Close Workspace") [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L150-L186].
    - Navigation tabs (6 tabs):
      1. `Overview` [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L191].
      2. `Assigned Tests ({N})` [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L193].
      3. `Specifications ({N})` [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L197].
      4. `Documents & Attachments ({N})` [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L201].
      5. `Preparation Configuration` [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L204].
      6. `Audit History` [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L205].
    - Tab 0 (Overview):
      - 4 cards: "ITEM CODE" (`item.code`), "CATEGORY" (`item.category`), "SOP NUMBER" (`item.sopNumber || "—"`), "STATUS" (`item.isActive ? "Active (Sample receipt enabled)" : "Frozen (Sample receipt disabled)"`) [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L214-L250].
      - "Configuration Summary" block with clickable navigation rows:
        - "Assigned Tests: {N} tests configured →" (switches to Tab 1) [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L257-L276].
        - "Specifications: {N} specifications defined →" (switches to Tab 2) [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L278-L297].
        - "Controlled Documents: {SOP Available / No SOP} • {VR Available / No VR} →" (switches to Tab 3) [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L299-L318].
        - "Preparation Steps: Configured · {Approved / Pending Approval} →" or "Not configured →" (switches to Tab 4) [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L320-L348].
    - Tab 1 (Assigned Tests):
      - Info alert: `These tests are automatically assigned when a new sample is received for {item.name}.` [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L356-L358].
      - Test list: grid of cards showing `{t.displayName || t.testCode}` and Chip `{t.testCode}` [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L360-L382].
      - Empty text: "No tests assigned to this item yet." [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L384-L386].
    - Tab 2 (Specifications):
      - Rendered by `ItemSpecificationsSection` [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L393-L399].
      - Shows specifications grouped by assigned test, stage tags, limit details, and parameter action buttons (Edit, Delete).
      - Button "+ Add Specification Parameter" opens `SpecificationParameterDialog`.
    - Tab 3 (Documents & Attachments):
      - SOP Section: Heading "SOP (Standard Operating Procedure)", Button "+ Upload SOP" [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L415-L426].
        - Current SOP Card: original filename, version badge (`Rev 01`), Effective Date, File Size, Uploaded by username and datetime; action buttons (`ItemDocumentActionButtons`: View, Download) [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L429-L431, L575-L642].
        - Empty SOP: dashed box "No current SOP uploaded for this item." [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L432-L436].
        - Historical SOPs: Accordion "Historical SOP Versions ({N})" with superseded chips [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L439-L461].
      - Verification Report Section: Heading "Verification Report", Button "+ Upload Verification Report" [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L475-L486].
        - Current VR Card: filename, version, Effective Date, File Size, Uploaded by; View/Download actions [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L489-L491].
        - Empty VR: dashed box "No current Verification Report uploaded for this item." [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L492-L496].
        - Historical VRs: Accordion "Historical Verification Reports ({N})" [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L499-L521].
    - Tab 4 (Preparation Configuration):
      - Rendered by `ItemPreparationConfigurationSection` [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L527-L533].
      - When not configured: Info alert: `No preparation configuration set for {itemName}. One will be created automatically from the first analyst confirmation, or you can configure it now.` [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemPreparationConfigurationSection.tsx:L105-L110].
      - When configured:
        - Status Chip: "Approved" (green) or "Pending Approval" (amber/warning) [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemPreparationConfigurationSection.tsx:L115-L119].
        - Caption: `Approved by {approvedByName} on {date}` or `Created by {createdByName} on {date}` [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemPreparationConfigurationSection.tsx:L120-L124].
        - Summary block (`PreparationStepsSummary`) [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemPreparationConfigurationSection.tsx:L127].
        - Warning alert if PendingReview: `This configuration is in effect and is already being used for testing. Approving it records your review; editing it first replaces the protocol for future samples.` [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemPreparationConfigurationSection.tsx:L129-L134].
      - Edit / Configure Mode fields:
        - "Sample Amount" (numeric input) [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemPreparationConfigurationSection.tsx:L141].
        - "Technique" (select: "Pour Plate" [`PourPlate`], "Filtration" [`Filtration`]) [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemPreparationConfigurationSection.tsx:L142-L145].
        - Conditional fields if Technique == "Filtration": "Filtration Volume (ml)" (numeric), "Washing Volume (ml)" (numeric) [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemPreparationConfigurationSection.tsx:L148-L153].
        - "Diluent" (text, required) [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemPreparationConfigurationSection.tsx:L156].
        - "Neutralizer" (text, required) [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemPreparationConfigurationSection.tsx:L157].
      - Buttons:
        - View mode: "Approve" (green button, visible only if `approvalStatus === "PendingReview"`), "Edit" or "Configure Preparation" [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemPreparationConfigurationSection.tsx:L172-L181].
        - Edit mode: "Cancel", "Save Configuration" / "Saving..." (disabled if diluent or neutralizer empty) [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemPreparationConfigurationSection.tsx:L164-L170].
    - Tab 5 (Audit History):
      - Description: `View full GxP audit log history for Item {item.name} ({item.code}).` [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L540-L542].
      - Button: "Open Audit History Log" (startIcon `<HistoryIcon />`) -> opens `AuditHistoryDialog` for entity `Item` and ID `{item.id}` [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L543-L550, L565-L570].
  - Document Upload Dialog (`UploadItemDocumentDialog`):
    - Title: `Upload Controlled SOP` or `Upload Controlled Verification Report` [src: frontend/src/modules/laboratoryConfiguration/items/components/UploadItemDocumentDialog.tsx:L78].
    - Item label: `Item: {itemName}` [src: frontend/src/modules/laboratoryConfiguration/items/components/UploadItemDocumentDialog.tsx:L93-L95].
    - "Document Type" select: "SOP" (`0`), "Verification Report" (`1`) [src: frontend/src/modules/laboratoryConfiguration/items/components/UploadItemDocumentDialog.tsx:L98-L111].
    - "Version" (text, placeholder "e.g. Rev 01", default "Rev 01", required) [src: frontend/src/modules/laboratoryConfiguration/items/components/UploadItemDocumentDialog.tsx:L114-L122].
    - "Effective Date" (date picker, defaults to current date `yyyy-MM-dd`) [src: frontend/src/modules/laboratoryConfiguration/items/components/UploadItemDocumentDialog.tsx:L123-L133].
    - File upload zone: drag-and-drop / click file area accepting `.pdf,.doc,.docx,.xls,.xlsx,.png,.jpg,.jpeg`; label "Choose File" or `{file.name}`, helper "PDF, Word, Excel, or Image (Max 25MB)" [src: frontend/src/modules/laboratoryConfiguration/items/components/UploadItemDocumentDialog.tsx:L136-L165].
    - Buttons: "Cancel", "Upload" / "Uploading..." (disabled when no file selected or uploading) [src: frontend/src/modules/laboratoryConfiguration/items/components/UploadItemDocumentDialog.tsx:L81-L87].
  - Specification Parameter Dialog (`SpecificationParameterDialog`):
    - Title: `Add Specification Parameter` or `Edit Specification: {parameterName}`.
    - Fields:
      - "Assigned Test" (select from item's assigned tests; disabled in edit mode) [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L135, L157-L170].
      - "Production Stage" (select: Bulk, InProcess, Finished, Stability; or leave as "All Stages" / null; only offered for Finished Product items) [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L142-L146, L765].
      - "Parameter Name" (text, required; auto-filled for HPLC/ICP/Titration) [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L136, L667].
      - "Limit Type" (select from 11 limit types; defaults by test workflow type) [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L73-L94].
      - "Reference Standard" (text, optional) [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L138, L670].
      - "Unit" (text; forced to "ppm" for residual solvents, "mg" for WeightVariation, "min" for DisintegrationTime) [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L671-L678].
      - Dynamic fields per limit type:
        - Range: "Lower Limit", "Upper Limit", checkboxes "Lower Inclusive", "Upper Inclusive" [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L183-L187].
        - NotMoreThan: "Upper Limit" (inclusive) [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L690-L695].
        - NotLessThan: "Lower Limit" (inclusive) [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L684-L689].
        - TargetWithTolerance: "Target", "Tolerance", "Tolerance Mode" ("Absolute" or "Percent") [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L189-L193].
        - CountTiered: "Alert Limit", "Action Limit", "Specification Limit", "Dilution Factor" [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L194-L198, L680-L683].
        - Qualitative: "Expected Result Text" (multiline text) [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L199-L201].
        - PresenceAbsence: "Expected State" ("Presence" / "Absence"), "Sample Quantity", "Quantity Unit" [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L202-L206].
        - MultiStage: Dynamic list of stages with "Stage Number", "Stage Label", "Acceptance Criteria Text", Add Stage / Remove Stage buttons [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L208-L210, L728-L735].
        - DissolutionQ: "Lower Limit Q (%)" (1..100), "Label Claim", "Label Claim Unit" (forced "mg") [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L611-L620, L756-L757].
        - DisintegrationTime: "Upper Limit (min)" (positive number), Unit (forced "min") [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L631-L635, L676-L677].
        - WeightVariation: "Dosage Form" ("Tablet" / "Capsule"), Unit (forced "mg") [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L645-L649, L674-L675].
        - Specialized fields:
          - HPLC Method Assay: "Method Analyte" picker, "Result Basis" ("PercentLabelClaim" or "MgPerUnit"), "Label Claim" + "Label Claim Unit" (if MgPerUnit) [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L556-L568].
          - Residual Solvents (GC): "Method Analyte" picker, Result Basis forced to `Ppm`, limit type forced to `NotMoreThan`, unit forced to `ppm` [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L550-L554, L672-L673].
          - ICP Method Assay: "Method Element" picker, Result Basis (`MgPerKg` for impurities; `PercentLabelClaim` or `MgPerUnit` for MineralAssay) [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L575-L598].
          - Titration: "Result Basis" (`PercentAsIs`, `PercentDriedBasis`, `PercentAnhydrousBasis`, `PercentLabelClaim`, `MgPerUnit`), "Label Claim" + "mg" (required if PercentLabelClaim or MgPerUnit) [src: frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L380-L395, L600-L606].
    - Buttons: "Cancel", "Save Specification" / "Saving...".

### MOD-SPEC-001 Specifications (stand-alone)
- SCR-SPEC-001 Specifications (stand-alone). Route `/laboratory-configuration/specifications` [src: frontend/src/routes/AppRoutes.tsx:L229; frontend/src/modules/laboratoryConfiguration/specifications/SpecificationsPage.tsx:L4].
  - Who can open: Guarded by permission `MasterData.Manage` (`PERMISSIONS.MASTER_DATA_MANAGE`) at the route level [src: frontend/src/routes/AppRoutes.tsx:L222, L229].
  - Behavior: Component renders `<Navigate to="/laboratory-configuration/items" replace />` - immediately sends user to SCR-ITEM-001. No distinct UI, tabs, or fields exist on this standalone route. Not linked from the application sidebar navigation menu (`menuConfig.ts`). [src: frontend/src/modules/laboratoryConfiguration/specifications/SpecificationsPage.tsx:L4-L6; frontend/src/routes/menuConfig.ts:L216-L266; see GAP-034 and GAP-702].

### MOD-RECVCFG-001 Receiving Configuration
- SCR-RECVCFG-001 Receiving Configuration. Route `/laboratory-configuration/receiving-configuration` [src: frontend/src/routes/AppRoutes.tsx:L234; frontend/src/modules/laboratoryConfiguration/masterDataSimple/ReceivingConfigurationPage.tsx:L290].
  - Who can open: Guarded by permission `MasterData.Manage` (`PERMISSIONS.MASTER_DATA_MANAGE`) [src: frontend/src/routes/AppRoutes.tsx:L222, L234].
  - Legacy redirect: `/laboratory-configuration/cause-of-testing` automatically redirects to `/laboratory-configuration/receiving-configuration` [src: frontend/src/routes/AppRoutes.tsx:L235].
  - Page header: Title "Receiving Configuration", Subtitle "Reference lists offered on the sample receiving form, shared across all six receiving categories." [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ReceivingConfigurationPage.tsx:L293-L296].
  - Layout: 3-column responsive card layout (`Samplers`, `Cause of Testing`, `Production Stages`) [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ReceivingConfigurationPage.tsx:L298-L335].
  - Card 1: Samplers (`NameListCard`):
    - Title: "Samplers" [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ReceivingConfigurationPage.tsx:L308].
    - Subtitle/Purpose: "Offered as suggestions in the “Sampled By” field. Analysts can still type a name that isn’t listed." [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ReceivingConfigurationPage.tsx:L309].
    - Delete note: "Samples already recorded with this name keep it; only the suggestion list changes." [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ReceivingConfigurationPage.tsx:L310].
    - Input field: "Sampler name", placeholder "e.g. Ahmed Reda" [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ReceivingConfigurationPage.tsx:L311-L312].
    - Add button: "Add" / "+ Add" [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ReceivingConfigurationPage.tsx:L16].
    - Actions per row: "Rename" (icon `<EditOutlinedIcon />`), "Delete" (icon `<DeleteOutlineIcon />`) [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ReceivingConfigurationPage.tsx:L254-L268].
    - Delete confirmation: `ConfirmationDialog` with message `Delete "{name}" from Samplers? Samples already recorded with this name keep it; only the suggestion list changes.` [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ReceivingConfigurationPage.tsx:L276-L281].
  - Card 2: Cause of Testing (`NameListCard`):
    - Title: "Cause of Testing" [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ReceivingConfigurationPage.tsx:L321].
    - Subtitle/Purpose: "Required on every sample. A cause already used by a sample cannot be deleted." [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ReceivingConfigurationPage.tsx:L322].
    - Delete note: "If any sample already uses it, the delete will be rejected and nothing changes." [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ReceivingConfigurationPage.tsx:L323].
    - Input field: "Cause of testing", placeholder "e.g. Investigation" [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ReceivingConfigurationPage.tsx:L324-L325].
    - Add button: "Add", inline Edit / Save, Delete button [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ReceivingConfigurationPage.tsx:L100-L138].
    - Delete confirmation: `ConfirmationDialog` with message `Delete "{name}" from Cause of Testing? If any sample already uses it, the delete will be rejected and nothing changes.` [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ReceivingConfigurationPage.tsx:L276-L281].
  - Card 3: Production Stages (`ProductionStagesCard`):
    - Title: "Production Stages" [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ProductionStagesCard.tsx:L142].
    - Subtitle/Purpose: "Stages a sample can be received at (e.g. Bulk, In-Process). Defines stage-specific replicate rules on tests." [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ProductionStagesCard.tsx:L143-L144].
    - Delete note: "Stage-specific replicates using this stage role will lose their association." [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ProductionStagesCard.tsx:L145].
    - Fields:
      - "Stage name" (text input, placeholder "e.g. Blend / Granulation") [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ProductionStagesCard.tsx:L170].
      - "Role" (select dropdown, options: "Bulk", "InProcess", "Finished", "Stability", "Other") [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ProductionStagesCard.tsx:L31-L37, L184-L198].
    - Add button: "Add" / "Save Changes" [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ProductionStagesCard.tsx:L202-L208].
    - Row items: display name and Chip `{role}`, action buttons: Rename (`<EditOutlinedIcon />`), Delete (`<DeleteOutlineIcon />`) [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ProductionStagesCard.tsx:L260-L288].
    - Delete confirmation: `ConfirmationDialog` with message `Delete "{name}" from Production Stages? Stage-specific replicates using this stage role will lose their association.` [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ProductionStagesCard.tsx:L295-L300].

### MOD-WORKBOOK-001 Master Data Workbook
- SCR-WORKBOOK-001 Master Data Workbook. Route `/laboratory-configuration/master-data-workbook` [src: frontend/src/routes/AppRoutes.tsx:L228; frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L15].
  - Who can open: Guarded by permission `MasterData.Manage` (`PERMISSIONS.MASTER_DATA_MANAGE`) [src: frontend/src/routes/AppRoutes.tsx:L222, L228].
  - Header: Title "Master Data Workbook", Subtitle "Load organisms, departments, rooms, water points, material master, media products, equipment, items and specifications from one Excel workbook." [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L72-L73].
  - Top action button: "Download workbook" (triggers GET download of populated `MicroLIMS-master-data-{yyyy-MM-dd}.xlsx` template) [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L74; backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L67-L97].
  - Section 1: "1. Check the workbook" [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L80]:
    - Button "Choose file" (hidden input accepting `.xlsx`) [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L82-L85].
    - File status label: `{file.name}` or "No file chosen" [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L86].
    - Button "Check file" / "Checking..." (disabled if no file or busy; performs dry-run validation against server) [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L87].
    - Button "Load and sign" (color "success", disabled unless `canLoad(report)` - enabled only when report exists, has 0 errors, and at least 1 created record) [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L88; frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/types.ts:L33-L34].
    - Validation report table (`WorkbookReportTable`) [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L90; frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/WorkbookReportTable.tsx:L6-L48]:
      - Table 1 ("Rows per sheet"): Columns `Sheet`, `New`, `Unchanged`, `Errors` for all 12 sheets.
      - Table 2 ("Problems to fix"): Header `{N} problem(s) to fix in the workbook`, Columns `Sheet`, `Row`, `Column`, `Problem`.
  - Section 2: "2. Loads and verification" [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L94]:
    - Register table (`RegisterTable`) displaying past import records [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L95]:
      - Columns:
        - "Loaded": formatted date/time (`new Date(i.importedAt).toLocaleString()`) [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L54].
        - "Loaded by": full name of loader [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L55].
        - "File": button with file name that re-downloads the stored Excel file from storage [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L56].
        - "New records": total count of created records (`sum of s.created across all sheets`) [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L57].
        - "Status": `Verified by {verifiedBy}` (if status == "Verified") or `Waiting for verification` (if status == "Loaded") [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L58].
        - Actions column: Button "Verify" (variant "outlined", visible only if status == "Loaded") [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L60-L62].
    - Empty state: Title "No workbook loads yet", Description "Check a workbook above, then load and sign it." [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L96].
  - Electronic Signature Dialog (`SignatureDialog`):
    - Meaning statement for Load: "I am loading this master data workbook into MicroLIMS." [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L102-L103].
    - Meaning statement for Verify:
      - If load contains items without specifications: `I verified this load against the approved source documents. Note: these items have no specifications yet and will become usable: {itemCodes}.` [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L65-L68].
      - Otherwise: `I verified this load against the approved source documents.` [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L68].
    - Password field, optional Comment textarea, Confirm / Cancel buttons [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L100-L110].

### MOD-TESTMASTER-001 Test Master (FP and RM & PM)
- SCR-TESTMASTER-002 FP Test Master. Route `/laboratory-configuration/fp-test-master` (`TestMasterPage lab="fp" area="fp"`) [src: frontend/src/routes/AppRoutes.tsx:L224; frontend/src/modules/laboratoryConfiguration/masterDataSimple/TestMasterPage.tsx:L35].
  - Who can open: Guarded by permission `MasterData.Manage` (`PERMISSIONS.MASTER_DATA_MANAGE`) [src: frontend/src/routes/AppRoutes.tsx:L222, L224]. User must belong to the Finished Product section to edit tests in that section [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L392-L394].
  - Page header: `LabPage` title "Physicochemical Test Master" (or area-specific), subtitle "Test definitions and calculation equations for Finished Product testing." [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/TestMasterPage.tsx:L35-L62].
  - Action button: "+ Add Test" opens Add Test Dialog [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/TestMasterPage.tsx:L140].
  - Filter bar: search input "Search tests by code or name..." [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/TestMasterPage.tsx:L132].
  - Register Table:
    - Columns: Test Code (monospace), Display Name, Workflow Type (`WORKFLOW_TYPE_LABELS`), Equation Type (`EQUATION_TYPE_LABELS`), Area Chip ("FP"), Status ("Active" / "Frozen"), Actions (Edit, Freeze/Unfreeze, Expand for Stage Replicates) [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/TestMasterPage.tsx:L20-L24, L55-L63].
  - Expanded row: Renders `TestStageReplicatesSection` (shows replicates per `ProductionStageRole` table with Add/Edit/Delete actions) [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/testMaster/TestStageReplicatesSection.tsx:L1-L100]. Note: FP tests have NO workflow steps or media [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/testMaster/testMasterConstants.ts:L3-L6].
- SCR-TESTMASTER-003 RM & PM Test Master. Route `/laboratory-configuration/rm-pm-test-master` (`TestMasterPage lab="fp" area="rmpm"`) [src: frontend/src/routes/AppRoutes.tsx:L225; frontend/src/modules/laboratoryConfiguration/masterDataSimple/TestMasterPage.tsx:L35].
  - Who can open: Guarded by permission `MasterData.Manage` (`PERMISSIONS.MASTER_DATA_MANAGE`) [src: frontend/src/routes/AppRoutes.tsx:L222, L225].
  - Behavior: Same as SCR-TESTMASTER-002, filtered to `area="rmpm"`. Displays Raw Materials and Packaging Materials physicochemical tests.
  - Add / Edit Test Dialog (FP & RM&PM):
    - Fields:
      - "Test Code" (text, uppercase alphanumeric, required, unique) [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/TestMasterPage.tsx:L66; backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L40-L42].
      - "Display Name" (text, required) [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/TestMasterPage.tsx:L67].
      - "Section" (select from user's assigned sections) [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/TestMasterPage.tsx:L68].
      - "Physicochemical Area" (`TestAreaField`: "Finished Product" [`fp`], "Raw and Packaging Materials" [`rmpm`], or "Both" [`both`]) [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/TestAreaField.tsx:L1-L40; backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L779-L794].
      - "Workflow Type" (select: `HplcMethodAssay`, `IcpMethodAssay`, `Measurement`, `Gravimetric`, `Qualitative`, `Dissolution`, `Disintegration`, `WeightVariation`, `Titration`) [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/testMaster/testMasterConstants.ts:L10-L24].
      - "Equation Type" (select: `None`, `HplcMethodAssay`, `IcpMethodAssay`, `SystemSuitability`, `Measurement`, `GravimetricLoss`, `GravimetricResidue`, `Qualitative`, `Dissolution`, `Disintegration`, `WeightVariation`, `Titration`) [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/testMaster/testMasterConstants.ts:L26-L53].
      - "Requires System Suitability" (switch/checkbox; mandatory for HplcMethodAssay and Dissolution; forbidden for Disintegration, WeightVariation, and IcpMethodAssay) [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L53-L57, L134-L135, L243-L244, L279-L280].
      - "Method Abbreviation" (text, 1-20 uppercase alphanumeric or hyphen characters; required when SST is enabled) [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L61-L71].
      - Dynamic equation-specific fields:
        - Measurement: "Replicate Count" (1..30), "Evaluation Basis" ("Mean", "EachValue", "Min", "Max") [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L78-L82].
        - Gravimetric: "Replicate Count" (1..30), "Uses Tare" (boolean) [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L98-L99, L325].
        - Condition Fields: optional text up to 500 characters [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L90-L91].
        - HPLC Method Assay: "HPLC Method" select (active methods belonging to same section) [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L282-L290].
        - ICP Method Assay: "ICP Method" select (active methods belonging to same section) [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L246-L254].
        - Dissolution: "S1 Offset (% Q)" (default 5), "S2 Min Offset (% Q)" (default 15), "S3 Min Offset (% Q)" (default 25), "S3 Max Below S2 Min" (default 2), optional "HPLC Method" [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L137-L144, L292-L303].
        - Disintegration: "Stage 1 Units" (default 6), "Stage 2 Units" (default 12), "Max Stage 1 Failures" (default 2), "Min Pass Total" (default 16) [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L162-L173].
        - Weight Variation: "Unit Count" (default 20), "Tablet Band 1 Max Mg" (default 130), "Tablet Band 1 %" (default 10), "Tablet Band 2 Max Mg" (default 324), "Tablet Band 2 %" (default 7.5), "Tablet Band 3 %" (default 5), "Tablet Max Outside" (default 2), "Capsule Inner %" (default 10), "Capsule Outer %" (default 25), "Capsule S1 Max Outside" (default 2), "Capsule S1 Max for Retest" (default 6), "Capsule S2 Extra Units" (default 40), "Capsule S2 Max Outside" (default 6) [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L191-L222].
        - Titration: Comprehensive titration settings section (`TitrationConfigSection`) covering Titration Type, Aqueous/Non-aqueous, Mode, Calculation, Titrant Solution, Equivalency Factor, Blank Requirement, Excess Solution/Volume, Endpoint, Indicator, Temperature Correction / Expansion Coefficient, Primary Standard [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/TitrationConfigSection.tsx:L1-L100; backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L349-L364].
        - When editing titration settings: "Reason for Change" textarea (`TitrationChangeReasonField`, required, 5..500 characters) [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/TitrationChangeReasonField.tsx:L1-L50; backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L758-L762].
    - Buttons: "Cancel", "Save Test" / "Save Changes" / "Saving...".

## roles

### MOD-ITEM-001 Items
- Permission `Items.Manage` (`PERMISSIONS.ITEMS_MANAGE`):
  - UI Gate: Wraps route `/laboratory-configuration/items` in `PermissionRoute`; gates "Add Item" button, edit/freeze/delete actions, and preparation configuration save/approve buttons. [src: frontend/src/routes/AppRoutes.tsx:L219; frontend/src/modules/laboratoryConfiguration/items/components/ItemPreparationConfigurationSection.tsx:L23-L24].
  - Backend enforcement: `[Authorize(Policy = PermissionConstants.ItemsManage)]` on `ItemController` (Create, Update, SetActive, Delete) [src: backend/MicroLIMS.API/Controllers/ItemController.cs:L20, L38, L46, L54]; `ItemPreparationConfigurationController` (Upsert, Approve) [src: backend/MicroLIMS.API/Controllers/ItemPreparationConfigurationController.cs:L35, L46]; `PreparationConfigurationQueueController` (Pending) [src: backend/MicroLIMS.API/Controllers/ItemPreparationConfigurationController.cs:L54].
  - Held by: SectionHead, SystemAdministrator [src: documentation/knowledge/_raw/verified-roles-permissions.md:L86, L90].
- Permission `Items.DocumentUpload` (`PERMISSIONS.ITEMS_DOCUMENT_UPLOAD`):
  - Backend enforcement: `[Authorize(Policy = PermissionConstants.ItemsDocumentUpload)]` on `ItemDocumentController.UploadAsync` [src: backend/MicroLIMS.API/Controllers/ItemDocumentController.cs:L33].
  - Read/download of documents is open to any authenticated user (`[Authorize]` on `GetDocumentsForItemAsync`, `DownloadDocumentAsync`, `GetDocumentPdfAsync`) [src: backend/MicroLIMS.API/Controllers/ItemDocumentController.cs:L16, L24, L44, L59].
  - Held by: SectionHead, SystemAdministrator [src: documentation/knowledge/_raw/verified-roles-permissions.md:L86].
- Segregation of Duties (SoD) / Ownership:
  - Preparation Configuration approval: Any Section Head holding `Items.Manage` can approve a configuration; testing is never blocked while in `PendingReview` status. [src: backend/MicroLIMS.Application/Services/ItemPreparationConfigurationService.cs:L26-L29].

### MOD-SPEC-001 Specifications
- Permission `MasterData.Manage` (`PERMISSIONS.MASTER_DATA_MANAGE`):
  - UI Gate: Route `/laboratory-configuration/specifications` is guarded by `PermissionRoute code={PERMISSIONS.MASTER_DATA_MANAGE}` [src: frontend/src/routes/AppRoutes.tsx:L222, L229].
  - Specification CRUD endpoints in `SpecificationMasterDataController` (`api/masterdata/specifications` and `api/masterdata/items/{itemId}/specifications`) enforce `[Authorize(Policy = PermissionConstants.MasterDataManage)]` [src: backend/MicroLIMS.API/Controllers/SpecificationMasterDataController.cs:L15, L29, L45, L59].
  - Section Ownership Gate (`SpecificationOwnership.EnsureCanEditAsync`):
    - Specifications carry no laboratory field of their own; their owning laboratory is determined by the `SectionId` of the `TestDefinition` corresponding to their `TestCode` [src: backend/MicroLIMS.Application/Services/SpecificationOwnership.cs:L7-L12].
    - Only a Section Head of the owning section (or a System Administrator, whose section scope is unrestricted) can add, update, or delete a specification [src: backend/MicroLIMS.Application/Services/SpecificationOwnership.cs:L15-L35].
    - Exact unauthorized rejection: `This specification belongs to {labName} - only its Section Head can change it.` [src: backend/MicroLIMS.Application/Services/SpecificationOwnership.cs:L32-L34].

### MOD-RECVCFG-001 Receiving Configuration
- Permission `MasterData.Manage` (`PERMISSIONS.MASTER_DATA_MANAGE`):
  - UI Gate: Route `/laboratory-configuration/receiving-configuration` guarded by `PermissionRoute code={PERMISSIONS.MASTER_DATA_MANAGE}` [src: frontend/src/routes/AppRoutes.tsx:L222, L234].
  - Backend enforcement: `[Authorize(Policy = PermissionConstants.MasterDataManage)]` on `ReferenceListMasterDataController` endpoints for samplers, causes-of-testing, and production-stages [src: backend/MicroLIMS.API/Controllers/ReferenceListMasterDataController.cs:L16, L25, L33, L41, L50, L58, L66, L75, L83, L91].
  - Held by: SectionHead, SystemAdministrator [src: documentation/knowledge/_raw/verified-roles-permissions.md:L86, L90].

### MOD-WORKBOOK-001 Master Data Workbook
- Permission `MasterData.Manage` (`PERMISSIONS.MASTER_DATA_MANAGE`):
  - UI Gate: Route `/laboratory-configuration/master-data-workbook` guarded by `PermissionRoute code={PERMISSIONS.MASTER_DATA_MANAGE}` [src: frontend/src/routes/AppRoutes.tsx:L222, L228].
  - Backend enforcement: `[Authorize(Policy = PermissionConstants.MasterDataManage)]` on `MasterDataWorkbookController` (Download, Validate, Import, Verify, File) [src: backend/MicroLIMS.API/Controllers/MasterDataWorkbookController.cs:L14, L24, L33, L41, L50, L59].
- Segregation of Duties (SoD) Rule:
  - Second-Person Verification Rule: The user verifying an imported workbook MUST be a different person than the user who loaded it (`import.ImportedByUserId == userId`) [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L199-L200].
  - Exact rejection: `The person who loaded the workbook cannot verify it. Ask a second person.` [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L200].
  - Section Scoping Rule on Verifier: If the import added `MaterialMasterEntry` records to sections outside the verifier's section scope, verification is blocked: `This load added material master entries to lab section(s) you are not a member of: {sections}. Ask a Section Head of that section to verify it.` [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L204-L213].
  - Signature Meanings: Loader signs with `SignatureMeaning.MasterDataChanged`; verifier signs with `SignatureMeaning.Reviewed` [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L150, L216].

### MOD-TESTMASTER-001 Test Master (FP and RM & PM)
- Permission `MasterData.Manage` (`PERMISSIONS.MASTER_DATA_MANAGE`):
  - UI Gate: Routes `/laboratory-configuration/fp-test-master` and `/laboratory-configuration/rm-pm-test-master` guarded by `PermissionRoute code={PERMISSIONS.MASTER_DATA_MANAGE}` [src: frontend/src/routes/AppRoutes.tsx:L222-L225].
  - Backend enforcement: `[Authorize(Policy = PermissionConstants.MasterDataManage)]` on `TestDefinitionMasterDataController` and `TestStageReplicateMasterDataController` [src: backend/MicroLIMS.API/Controllers/TestDefinitionMasterDataController.cs:L18; backend/MicroLIMS.API/Controllers/TestStageReplicateMasterDataController.cs:L16].
- Laboratory Section Membership Gate:
  - When creating or modifying a test definition: User's section scope is checked. If the test belongs to a section outside the user's scope: `This test belongs to a laboratory section you are not assigned to.` [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L392-L394].
  - When configuring stage replicates: User must be assigned to the test definition's owning section: `This test belongs to a laboratory section you are not assigned to.` [src: backend/MicroLIMS.Application/Services/MasterData/TestStageReplicateMasterDataService.cs:L44-L46, L71-L73, L95-L97].

## workflows

Pending Phase 4.

## business-rules

### MOD-ITEM-001 Items
- BR-ITEM-001 Allowed Item Categories. IMPLEMENTED. Items can only be configured for Product (`FinishedProduct`), Raw Material (`RawMaterial`), or Packaging Material (`PackagingMaterial`) categories. Water, Environmental Monitoring, After Cleaning, and GPT are managed via their dedicated configuration pages. [src: backend/MicroLIMS.Application/Services/ItemService.cs:L18-L23, L84-L94].
- BR-ITEM-002 Item Code Uniqueness. IMPLEMENTED. Item Code must be unique across all items in the database. Creating or updating an item with a code matching an existing record is rejected. [src: backend/MicroLIMS.Application/Services/ItemService.cs:L39-L40, L70-L71].
- BR-ITEM-003 Auto-Assignment of Tests upon Sample Receipt. IMPLEMENTED. Tests assigned to an item (`Item.AssignedTests`) are automatically assigned as sample tests when a sample for that item is received in the sample receiving workflow. At least one assigned test is required to create an item. [src: frontend/src/modules/laboratoryConfiguration/items/components/AddItemDialog.tsx:L63-L66, L148-L151; backend/MicroLIMS.Application/Services/ItemService.cs:L48, L78-L79, L96-L99].
- BR-ITEM-004 Delete Protection on Referenced Items. IMPLEMENTED. Hard deletion of an item is blocked if the item has ever received samples (`Sample.ItemId` is a Restrict foreign key). If samples exist, deletion is rejected with instructions to freeze the item instead. [src: backend/MicroLIMS.Application/Services/ItemService.cs:L122-L126].
- BR-ITEM-005 Frozen Item Lifecycle. IMPLEMENTED. Freezing an item (`item.IsActive = false`) disables receipt of new samples for that item in `ProductWorkflowEngine.ReceiveAsync` while retaining full historical traceability, assigned tests, specifications, and document records. Unfreezing restores sample receipt. [src: backend/MicroLIMS.Application/Services/ItemService.cs:L105-L111; frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L244-L249].
- BR-ITEM-006 Item Document Versioning and Access Logging. IMPLEMENTED. Item documents are categorized as either SOP (`DocumentType = 0`) or Verification Report (`DocumentType = 1`). A newly uploaded document starts with status `Current` (`0`). Uploading a new version of the same document type automatically marks the previously Current document as `Superseded` (`1`). Max upload file size is 25 MB (service limit) with allowed extensions `.pdf,.doc,.docx,.xls,.xlsx,.png,.jpg,.jpeg`. All view and download actions are logged in `ItemDocumentAccessLog`. [src: backend/MicroLIMS.Application/Services/ItemDocumentService.cs:L17-L115; frontend/src/modules/laboratoryConfiguration/items/components/UploadItemDocumentDialog.tsx:L150, L163-L164].
- BR-ITEM-007 Preparation Configuration Auto-Creation and Lifecycle. IMPLEMENTED. If an item has no preparation configuration, one is created automatically from the analyst's first test confirmation, landing in `PendingReview` status. It is usable immediately for sample testing (testing is never blocked waiting for Section Head approval). When a Section Head edits an approved configuration, approval is cleared and status resets to `PendingReview`. When approved by a Section Head, status transitions to `Approved`, recording `ApprovedByUserId` and `ApprovedAt`. [src: backend/MicroLIMS.Application/Services/ItemPreparationConfigurationService.cs:L26-L99; frontend/src/modules/laboratoryConfiguration/items/components/ItemPreparationConfigurationSection.tsx:L105-L135].
- BR-ITEM-008 Preparation Configuration Parameters. IMPLEMENTED. A preparation protocol defines `Amount` (decimal), `Technique` (`PourPlate` or `Filtration`), `Diluent` (mandatory non-empty text), and `Neutralizer` (mandatory non-empty text). If the technique is `Filtration`, both `FiltrationVolume` and `WashingVolume` are mandatory. [src: backend/MicroLIMS.Application/Services/PreparationParameterValidator.cs:L6-L37].

### MOD-SPEC-001 Specifications
- BR-SPEC-001 Limit Type Enumeration. IMPLEMENTED. Specifications support exactly 11 limit types: `Range` (0), `NotMoreThan` (1), `NotLessThan` (2), `TargetWithTolerance` (3), `CountTiered` (4), `Qualitative` (5), `PresenceAbsence` (6), `MultiStage` (7), `DissolutionQ` (8), `DisintegrationTime` (9), `WeightVariation` (10). [src: backend/MicroLIMS.Domain/Enums/LimitType.cs:L3-L20; frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx:L73-L85].
- BR-SPEC-002 Canonical Limit Formatting. IMPLEMENTED. On save, non-CountTiered specifications automatically generate a canonical formatted text representation in `SpecLimit`, while clearing `AlertLimit`, `ActionLimit`, and `DilutionFactor`:
  - `Range`: `"{LowerLimit}-{UpperLimit}"` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L42].
  - `NotMoreThan`: `"NMT {UpperLimit}"` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L43].
  - `NotLessThan`: `"NLT {LowerLimit}"` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L44].
  - `TargetWithTolerance`: `"{Target} ± {Tolerance}%"` (if Percent) or `"{Target} ± {Tolerance}"` (if Absolute) [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L45-L47].
  - `Qualitative`: `ExpectedResultText` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L48].
  - `PresenceAbsence`: `"Present"` or `"Absent"` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L49].
  - `DissolutionQ`: `"Q = {LowerLimit} %"` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L52].
  - `DisintegrationTime`: `"NMT {UpperLimit} min"` [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L53].
  - `WeightVariation`: `"USP <2091>: tablets, limit by average weight"` (if Tablet) or `"USP <2091>: net content 90-110 % of average"` (if Capsule) [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L54-L56].
  - `MultiStage`: empty string (criteria kept in child stages table) [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L50].
- BR-SPEC-003 Limit Comparison and Evaluation Logic. IMPLEMENTED. `SpecificationEvaluator` evaluates raw decimal results against limits without intermediate rounding:
  - `Range`: `LowerInclusive ? value < LowerLimit : value <= LowerLimit` -> `OutOfSpecification`; `UpperInclusive ? value > UpperLimit : value >= UpperLimit` -> `OutOfSpecification`; otherwise `WithinLimits` (returns `LimitsNotConfigured` if neither limit is set). [src: backend/MicroLIMS.Application/Services/SpecificationEvaluator.cs:L14-L34].
  - `NotMoreThan`: `UpperInclusive ? value > UpperLimit : value >= UpperLimit` -> `OutOfSpecification`; otherwise `WithinLimits`. [src: backend/MicroLIMS.Application/Services/SpecificationEvaluator.cs:L36-L43].
  - `NotLessThan`: `LowerInclusive ? value < LowerLimit : value <= LowerLimit` -> `OutOfSpecification`; otherwise `WithinLimits`. [src: backend/MicroLIMS.Application/Services/SpecificationEvaluator.cs:L45-L52].
  - `TargetWithTolerance`: `tol = ToleranceMode == Percent ? Target * Tolerance / 100 : Tolerance`; out of specification if `value < Target - tol || value > Target + tol`. [src: backend/MicroLIMS.Application/Services/SpecificationEvaluator.cs:L54-L70].
  - `CountTiered`: Calls `SpecLimitParser.CompareAgainstLimits(value, alert, action, spec)`. Evaluates Spec limit first (if exceeded -> `OutOfSpecification`), Action limit second (if exceeded -> `ActionLimitExceeded`), Alert limit third (if exceeded -> `AlertLimitExceeded`). Parsing supports plain numbers, ranges, NMT, NLT, and Target ± Tolerance. Exceedance checks use strict inequality (`value < Min || value > Max`), meaning configured boundary numbers are inclusive passes. [src: backend/MicroLIMS.Application/Services/SpecificationEvaluator.cs:L72-L73; backend/MicroLIMS.Application/Services/SpecLimitParser.cs:L19-L21, L208-L230].
  - Non-numeric limit types (`Qualitative`, `PresenceAbsence`, `MultiStage`, `DissolutionQ`, `DisintegrationTime`, `WeightVariation`) throw `InvalidOperationException` if evaluated against a numeric value. [src: backend/MicroLIMS.Application/Services/SpecificationEvaluator.cs:L75-L82].
- BR-SPEC-004 Dilution Factor Restriction. IMPLEMENTED. Dilution factor is permitted ONLY on `CountTiered` specifications. Attempting to set dilution factor on any other limit type throws `InvalidOperationException`. [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L84-L85].
- BR-SPEC-005 Cross-Laboratory Specification Ownership. IMPLEMENTED. A specification carries no laboratory section ID of its own; its ownership is determined by `TestDefinition.SectionId` of its `TestCode`. Specifications are visible across all laboratory sections on the item's workspace, but only the Section Head of the owning section (or System Administrator) can create, update, or delete specifications for that test. [src: backend/MicroLIMS.Application/Services/SpecificationOwnership.cs:L7-L35].
- BR-SPEC-006 Stage-Specific Specifications Override General Rows. IMPLEMENTED. Finished Product items can configure specifications specific to a `ProductionStageRole` (`Bulk`, `InProcess`, `Finished`, `Stability`). When resolving specifications for a sample (`SpecificationLookup`), if the item has specifications for the sample's stage role, those stage rows apply; otherwise, general rows (`ProductionStageRole == null`) apply. Stage rows and general rows are never mixed. Stage-specific specifications are forbidden on Raw Material and Packaging Material items. [src: backend/MicroLIMS.Application/Services/SpecificationLookup.cs:L8-L67; backend/MicroLIMS.Application/Services/SpecificationService.cs:L202-L211].
- BR-SPEC-007 Test Assignment Requirement for Specifications. IMPLEMENTED. A specification can only be configured for a test that is already assigned to the item (`Item.AssignedTests`). [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L185-L189].
- BR-SPEC-008 Specialized Limit Type Constraints. IMPLEMENTED:
  - `DissolutionQ`: Allowed only for Dissolution tests; only 1 specification allowed per dissolution test; LowerLimit (Q) must be 1..100; label claim mandatory > 0 with unit "mg". [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L137-L144, L245-L259].
  - `DisintegrationTime`: Allowed only for Disintegration tests; only 1 specification allowed per disintegration test; UpperLimit must be > 0 with unit "min"; label claim forbidden. [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L146-L156, L261-L275].
  - `WeightVariation`: Allowed only for WeightVariation tests; only 1 specification allowed per weight variation test; DosageForm (`Tablet` or `Capsule`) required; unit "mg"; scalar numeric limits and label claim forbidden. [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L157-L168, L277-L291].
  - `HplcMethodAssay`: Keyed by method analyte + result basis (`PercentLabelClaim` or `MgPerUnit`). If `MgPerUnit`, label claim > 0 and unit required. If GC residual solvents, result basis forced to `Ppm`, limit type forced to `NotMoreThan`, unit forced to `ppm`. Limit type must be Range, NotMoreThan, NotLessThan, or TargetWithTolerance. [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L297-L335].
  - `IcpMethodAssay`: Keyed by method element + result basis. For `MineralAssay`, basis is `MgPerUnit` or `PercentLabelClaim` with label claim. For elemental impurities, basis is `MgPerKg` (`µg/g`), limit type `NotMoreThan`, label claim forbidden. [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L336-L372].
  - `Titration`: Result basis required (`PercentAsIs`, `PercentDriedBasis`, `PercentAnhydrousBasis`, `PercentLabelClaim`, `MgPerUnit`). Label claim required if and only if basis is `PercentLabelClaim` or `MgPerUnit` (and only permitted for Finished Products). Limit type must be Range, NotMoreThan, NotLessThan, or TargetWithTolerance. [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L373-L404].

### MOD-RECVCFG-001 Receiving Configuration
- BR-RECVCFG-001 Samplers Suggestion Picklist. IMPLEMENTED. Samplers reference list provides auto-complete suggestions for the "Sampled By" field on receiving forms. It is not an enforced foreign key; deleting a sampler removes it from future suggestions while existing samples retain the recorded name as entered. [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ReceivingConfigurationPage.tsx:L308-L310; backend/MicroLIMS.Application/Services/MasterData/ReferenceListMasterDataService.cs:L98-L106].
- BR-RECVCFG-002 Cause of Testing Foreign Key Integrity. IMPLEMENTED. Cause of Testing is a mandatory selection on every sample and is enforced by a database foreign key (`Sample.CauseOfTestingId` Restrict). Deleting a Cause of Testing that is referenced by one or more samples is strictly rejected. [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ReceivingConfigurationPage.tsx:L321-L323; backend/MicroLIMS.Application/Services/MasterData/ReferenceListMasterDataService.cs:L56-L68].
- BR-RECVCFG-003 Production Stage Roles. IMPLEMENTED. Every production stage is assigned a role: `Bulk`, `InProcess`, `Finished`, `Stability`, or `Other`. Stage roles govern stage-specific test replicate counts in the Test Master and stage-specific specifications on Finished Products. [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ProductionStagesCard.tsx:L31-L37; backend/MicroLIMS.Application/Services/MasterData/ReferenceListMasterDataService.cs:L108-L122].

### MOD-WORKBOOK-001 Master Data Workbook
- BR-WORKBOOK-001 12 Dependency-Ordered Sheets. IMPLEMENTED. The workbook contains exactly 12 data sheets loaded in strict dependency order: 1. `Organisms`, 2. `ProductionStages`, 3. `EmDepartments`, 4. `EmRooms`, 5. `WaterDepartments`, 6. `WaterPoints`, 7. `MaterialMaster`, 8. `MediaProducts`, 9. `Equipment`, 10. `Items`, 11. `Specifications`, 12. `SpecificationStages`. Sheets 1-10 are loaded first; test definitions and methods are created in the UI; then Specifications and SpecificationStages are loaded. [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataSheets.cs:L8-L33, L43-L44].
- BR-WORKBOOK-002 Add-Only Ingestion Rule. IMPLEMENTED. The workbook only ADDS new records. Existing database records are exported into the workbook for comparison. If an uploaded row matches an existing record's natural key but contains different values in one or more columns, it is rejected with an error specifying the differing columns. Existing records must be edited directly in their MicroLIMS screen. [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataSheets.cs:L39-L40; backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookRunner.cs:L86-L93].
- BR-WORKBOOK-003 File Size, Row, and Formatting Limits. IMPLEMENTED. Workbook must be `.xlsx` (OpenXML format), max file size 10 MB (10,485,760 bytes), max 5,000 new rows across all sheets, max 260 character file name, max 2,000 character comment. Decimal numbers must use dot (`.`), dates must use `yyyy-MM-dd`. [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L22-L26, L128-L131, L263-L274; backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookRunner.cs:L13, L52-L57; backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataSheets.cs:L42].
- BR-WORKBOOK-004 Savepoint Transaction Isolation. IMPLEMENTED. Master data dry run and signed import execute inside a database transaction with a named savepoint (`mdw_row`) per row. Any row error (including database constraint violations) rolls back only that row and records an error, allowing the validation run to check all remaining rows across all sheets in a single pass. [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookRunner.cs:L7-L10, L95-L111].
- BR-WORKBOOK-005 Two-Phase Activation via Second-Person Verification. IMPLEMENTED. When loaded, Items and Material Master entries are saved with `IsActive = false` (inactive/frozen). Loader signs with `SignatureMeaning.MasterDataChanged`. A second person who did not load the file must verify the load against approved source documents and sign with `SignatureMeaning.Reviewed`. This verification transitions status from `Loaded` to `Verified` and activates the newly loaded items and materials. [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L145, L150, L187-L231; backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataSheets.cs:L46-L47].
- BR-WORKBOOK-006 Section Scoping on Verifier. IMPLEMENTED. If an import creates `MaterialMasterEntry` records, the verifier must have access to all sections those materials belong to. If any materials belong to sections outside the verifier's scope, verification is rejected. [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L204-L213].

### MOD-TESTMASTER-001 Test Master (FP and RM & PM)
- BR-TESTMASTER-001 Canonical Test Code and Display Name. IMPLEMENTED. Every test definition has a unique alphanumeric code and display name. Freezing a test definition (`isActive = false`) excludes it from test selection dropdowns on items and sampling points while maintaining existing sample records and audit history. [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L40-L42, L796-L812].
- BR-TESTMASTER-002 Physicochemical Area Enforcement. IMPLEMENTED. Physicochemical tests must be categorized as Finished Product (`fp`), Raw and Packaging Materials (`rmpm`), or Both (`both`). When changing an existing test's area away from Both, the change is blocked if items in the lost category still have specifications linked to the test. [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L779-L794].
- BR-TESTMASTER-003 Mandatory Workflow and Equation Type Coupling. IMPLEMENTED. Equation types must match workflow types:
  - `Measurement`: Workflow `Measurement`, Equation `Measurement`, ReplicateCount 1..30, EvaluationBasis mandatory. [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L73-L89].
  - `Gravimetric`: Workflow `Gravimetric`, Equation `GravimetricLoss` or `GravimetricResidue`, ReplicateCount 1..30, optional `UsesTare`. [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L93-L108].
  - `Qualitative`: Workflow `Qualitative`, Equation `Qualitative`. [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L110-L119].
  - `Dissolution`: Workflow `Dissolution`, Equation `Dissolution`, RequiresSystemSuitability mandatory true, stage offsets S1/S2/S3 >= 0. [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L121-L144].
  - `Disintegration`: Workflow `Disintegration`, Equation `Disintegration`, RequiresSystemSuitability forbidden false, Stage 1/2 units >= 1, MaxStage1Failures 0..(S1-1), MinPassTotal 1..(S1+S2). [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L146-L173].
  - `WeightVariation`: Workflow `WeightVariation`, Equation `WeightVariation`, RequiresSystemSuitability forbidden false, UnitCount >= 1, band weights and percentages > 0, Tablet Band 1 Max < Band 2 Max, Capsule Inner % < Outer %, Capsule S1 Max Outside < S1 Max Retest <= UnitCount. [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L175-L222].
  - `HplcMethodAssay`: Workflow `HplcMethodAssay`, Equation `HplcMethodAssay`, RequiresSystemSuitability mandatory true, active HPLC method required in same section. [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L266-L290].
  - `IcpMethodAssay`: Workflow `IcpMethodAssay`, Equation `IcpMethodAssay`, RequiresSystemSuitability forbidden false (uses calibration run), active ICP method required in same section. [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L231-L259].
  - `Titration`: Workflow `Titration`, Equation `Titration`. [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L349-L365].
  - Retired types (`HplcAssay`, `HplcMultiAnalyte`, `StandardComparison`, `CalibrationCurve`, `ElementalAssay`) are rejected. [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L43-L44, L224-L227].
- BR-TESTMASTER-004 System Suitability Gate and Method Abbreviation. IMPLEMENTED. System suitability is allowed ONLY for `HplcMethodAssay` and `Dissolution` tests. When enabled, a Method Abbreviation matching regex `^[A-Z0-9-]{1,20}$` is required. [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L59-L71, L261-L263].
- BR-TESTMASTER-005 Stage Replicates Resolution and Gating. IMPLEMENTED. A test definition can define replicate requirements per `ProductionStageRole` (SampleReplicates >= 1). When testing, `StageReplicateResolver` checks the sample's reconciled stage. If the sample has no reconciled stage or the test has no replicate row for that stage role, testing is blocked with instructions to contact the Section Head or configure the test master. [src: backend/MicroLIMS.Application/Services/MasterData/TestStageReplicateMasterDataService.cs:L48-L53; backend/MicroLIMS.Application/Services/StageReplicateResolver.cs:L65-L101].
- BR-TESTMASTER-006 Titration Setting Change Audit Trail. IMPLEMENTED. Editing titration parameters on an existing test requires a Reason for Change between 5 and 500 characters. Changes commit in a single transaction that writes an audit event `TestDefinition.TitrationChanged` with before and after JSON snapshots. [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L752-L774].

## validations

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
  - Document file empty: `File is empty.` [src: backend/MicroLIMS.Application/Services/ItemDocumentService.cs:L56].
  - Document file too large: `File size exceeds the 25 MB limit.` [src: backend/MicroLIMS.Application/Services/ItemDocumentService.cs:L60].
  - Document extension invalid: `File type not allowed. Permitted: .pdf, .doc, .docx, .xls, .xlsx, .png, .jpg, .jpeg.` [src: backend/MicroLIMS.Application/Services/ItemDocumentService.cs:L66].
  - Document version missing: `Version is required.` [src: backend/MicroLIMS.Application/Services/ItemDocumentService.cs:L70].
  - Document not found: `Document {documentId} not found.` [src: backend/MicroLIMS.Application/Services/ItemDocumentService.cs:L96].
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

## statuses

### MOD-ITEM-001 Items
- Item Active Status (`item.isActive`):
  - `Active` (UI label "Active"): Item is available for testing; sample receipt enabled in `ProductWorkflowEngine.ReceiveAsync`. Initial state when an item is created. [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L247; backend/MicroLIMS.Application/Services/ItemService.cs:L109].
  - `Frozen` (UI label "Frozen"): Item is deactivated; sample receipt is blocked for new samples. Historical records, assigned tests, specifications, and documents remain accessible. Transition: Section Head or System Administrator clicks "Freeze item" / "Unfreeze item" in `ItemTable`. [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L247; frontend/src/modules/laboratoryConfiguration/items/ItemTable.tsx:L337-L345; backend/MicroLIMS.Application/Services/ItemService.cs:L105-L111].
- Item Document Status (`MaterialDocumentStatus`):
  - `Current` (`0`, UI label "Current" / version chip `Rev 01`): Active controlled document for the item. Assigned automatically when a document is uploaded. [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L85-L88, L620-L625; backend/MicroLIMS.Application/Services/ItemDocumentService.cs:L78].
  - `Superseded` (`1`, UI label "Superseded"): Historical document version. Transition: When a newer document of the same type (SOP or Verification Report) is uploaded, the existing Current document automatically transitions to Superseded. [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx:L626-L628; backend/MicroLIMS.Application/Services/ItemDocumentService.cs:L39-L44].
- Preparation Configuration Status (`ApprovalGateStatus`):
  - `PendingReview` (`0`, UI label "Pending Approval"): In effect and immediately usable for sample testing. Set on initial auto-creation from analyst confirmation or whenever a Section Head edits an existing protocol. [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemPreparationConfigurationSection.tsx:L117-L118, L129-L134; backend/MicroLIMS.Application/Services/ItemPreparationConfigurationService.cs:L79].
  - `Approved` (`1`, UI label "Approved"): Officially reviewed and approved by Section Head. Transition: Section Head clicks "Approve" button, recording approval user ID and timestamp. Editing an approved configuration re-opens it to `PendingReview`. [src: frontend/src/modules/laboratoryConfiguration/items/components/ItemPreparationConfigurationSection.tsx:L117-L125; backend/MicroLIMS.Application/Services/ItemPreparationConfigurationService.cs:L67-L71, L93-L96].

### MOD-SPEC-001 Specifications
- Specification Evaluation Status (`ResultStatus`):
  - `WithinLimits` (`0`): Result satisfies configured specification limits. [src: backend/MicroLIMS.Domain/Enums/ResultStatus.cs:L5; backend/MicroLIMS.Application/Services/SpecificationEvaluator.cs:L33, L42, L51, L69].
  - `OutOfSpecification` (`1`): Result breaches lower or upper limits; triggers Out of Specification (OOS) investigation workflow. [src: backend/MicroLIMS.Domain/Enums/ResultStatus.cs:L6; backend/MicroLIMS.Application/Services/SpecificationEvaluator.cs:L23, L30, L42, L51, L67].
  - `AlertLimitExceeded` (`2`): Result exceeds alert limit on CountTiered specification. [src: backend/MicroLIMS.Domain/Enums/ResultStatus.cs:L7; backend/MicroLIMS.Application/Services/SpecLimitParser.cs:L220].
  - `ActionLimitExceeded` (`3`): Result exceeds action limit on CountTiered specification. [src: backend/MicroLIMS.Domain/Enums/ResultStatus.cs:L8; backend/MicroLIMS.Application/Services/SpecLimitParser.cs:L216].
  - `LimitsNotConfigured` (`4`): Result evaluated but specification limits are not configured. [src: backend/MicroLIMS.Domain/Enums/ResultStatus.cs:L9; backend/MicroLIMS.Application/Services/SpecificationEvaluator.cs:L17, L39, L48, L57].

### MOD-RECVCFG-001 Receiving Configuration
- Reference Item Status (`IsActive`):
  - `true`: Active reference entry offered on sample receiving dropdown picklists. [src: backend/MicroLIMS.Application/Services/MasterData/ReferenceListMasterDataService.cs:L26, L71, L109].
  - `false`: Removed/deleted entry.

### MOD-WORKBOOK-001 Master Data Workbook
- Master Data Import Status (`MasterDataImportStatus`):
  - `Loaded` (`0`, UI label "Waiting for verification"): Workbook has been successfully ingested into database tables and signed by loader (`SignatureMeaning.MasterDataChanged`). Newly created Items and Material Master entries remain inactive. Transition: Signed load in SCR-WORKBOOK-001. [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L58, L61; backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L145].
  - `Verified` (`1`, UI label `Verified by {verifiedBy}`): Second person has verified the import against approved source documents and electronically signed (`SignatureMeaning.Reviewed`). Newly loaded Items and Material Master entries transition to `IsActive = true`. [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L58; backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L219-L230].

### MOD-TESTMASTER-001 Test Master (FP and RM & PM)
- Test Definition Active Status (`TestDefinition.IsActive`):
  - `Active` (`true`, UI label "Active"): Test is available for assignment to items, sampling points, and samples. [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/TestMasterPage.tsx:L7; backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L809].
  - `Frozen` (`false`, UI label "Frozen"): Test is deactivated and hidden from test selection dropdowns for new assignments. Existing test orders and past records remain intact. Transition: Section Head or Administrator clicks Freeze/Unfreeze in Test Master register. [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/TestMasterPage.tsx:L33-L34; backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L800].

## terminology

### MOD-ITEM-001 Items
- Item: A manufactured product or raw/packaging material catalog entry tested by the laboratory. [src: backend/MicroLIMS.Application/Services/ItemService.cs:L18-L23].
- Assigned Tests: The battery of laboratory test definitions linked to an item that are automatically instantiated as sample test orders when a sample for that item is received. [src: frontend/src/modules/laboratoryConfiguration/items/components/AddItemDialog.tsx:L148-L151].
- Frozen Item: A deactivated catalog item whose sample receipt is blocked while preserving all past historical testing data and traceability. [src: backend/MicroLIMS.Application/Services/ItemService.cs:L101-L104].
- Controlled Document: An authoritative standard operating procedure (SOP) or verification report file uploaded to an item, governed by revision versioning and access logging. [src: backend/MicroLIMS.Application/Services/ItemDocumentService.cs:L17-L25].
- Preparation Protocol: Standardized sample preparation configuration (sample amount, technique, filtration/washing volume, diluent, neutralizer) used by analysts during microbiology testing. [src: backend/MicroLIMS.Application/Services/ItemPreparationConfigurationService.cs:L26-L29].

### MOD-SPEC-001 Specifications
- Canonical Limit: Standardized text representation of an acceptance limit (e.g. "90.0-110.0", "NMT 10", "Q = 75 %") automatically formatted and saved by the system. [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L36-L59].
- Count-Tiered Specification: Multi-threshold limit configuration allowing Alert, Action, and Specification limits with an optional dilution factor, used for microbial enumeration tests. [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L51, L84-L85].
- Stage-Specific Specification: Acceptance criteria applicable exclusively to samples received at a designated production stage role (Bulk, In-Process, Finished, Stability). [src: backend/MicroLIMS.Application/Services/SpecificationLookup.cs:L8-L14].
- Dilution Factor: A multiplier applied during count evaluation, permitted exclusively on Count-Tiered specifications. [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L84-L85].

### MOD-RECVCFG-001 Receiving Configuration
- Sampler: An individual qualified to draw samples; configured in a suggestion picklist on the sample receiving form. [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ReceivingConfigurationPage.tsx:L308-L310].
- Cause of Testing: Mandatory justification selected upon receiving a sample (e.g. Routine Release, Investigation, Stability Study). [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ReceivingConfigurationPage.tsx:L321-L323].
- Production Stage Role: Standardized functional classification of a manufacturing stage (`Bulk`, `InProcess`, `Finished`, `Stability`, `Other`). [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ProductionStagesCard.tsx:L31-L37].

### MOD-WORKBOOK-001 Master Data Workbook
- Master Data Workbook: An Excel spreadsheet (`.xlsx`) structured into 12 predefined sheets used to bulk-load laboratory master data. [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataSheets.cs:L8-L33].
- Second-Person Verification: A dual-control compliance safeguard requiring an independent qualified user to review and sign off on imported data before it becomes active. [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L187-L190].
- Dry Run (Check File): Pre-load validation that executes all row insertions against a transactional savepoint and rolls back, generating a comprehensive error report without modifying database state. [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L99-L114].

### MOD-TESTMASTER-001 Test Master (FP and RM & PM)
- Test Definition: Canonical master record defining a test code, display name, owning section, workflow type, equation type, and operating parameters. [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L30-L34].
- Physicochemical Area: Operational sub-division of the physicochemical laboratory separating Finished Product testing (`fp`) from Raw and Packaging Material testing (`rmpm`). [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/testMasterArea.ts:L1-L30].
- System Suitability (SST): Instrumental checks (e.g. RSD, resolution, tailing factor, plate count) required before running chromatographic tests. [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L59-L71].
- Stage Replicate: Minimum number of sample test determinations mandated for a sample based on its reconciled manufacturing stage role. [src: backend/MicroLIMS.Application/Services/MasterData/TestStageReplicateMasterDataService.cs:L48-L53].

## reports

### MOD-ITEM-001 Items
- Item Document Access Log: System report tracking user interactions with controlled documents (SOPs and Verification Reports); logs document ID, user ID, access type (View or Download), timestamp, and IP address. [src: backend/MicroLIMS.Application/Services/ItemDocumentService.cs:L100-L114].

### MOD-WORKBOOK-001 Master Data Workbook
- Master Data Workbook Download (.xlsx): Dynamic export generated via `GET /api/masterdata/workbook` containing 12 data sheets populated with current database records (filtered to user's section scope), column instructions, and drop-down choices. [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L67-L97].
- Workbook Validation Report: On-screen diagnostic report generated during check file dry-run or import failure; displays:
  - Rows per sheet table: Columns `Sheet`, `New`, `Unchanged`, `Errors` for each of the 12 workbook sheets. [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/WorkbookReportTable.tsx:L9-L23].
  - Problems to fix table: Columns `Sheet`, `Row`, `Column`, `Problem` detailing exact cell-level issues. [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/WorkbookReportTable.tsx:L25-L46].
- Stored Workbook Copy Download: Archived copy of the uploaded workbook file retrievable by clicking the file name button in the loads register (`GET /api/masterdata/workbook/imports/{id}/file`). [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L56; backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L236-L241].

## audit-trail

### MOD-ITEM-001 Items
- Automatic Audit Capture: Item creation, updates, and active status toggles (Freeze/Unfreeze) are tracked by EF Core interceptors and recorded in the audit trail with before/after state diffs. [src: backend/MicroLIMS.Application/Services/ItemService.cs:L51, L81, L110].
- Document Access Logging: Document downloads and in-browser views are logged in `ItemDocumentAccessLog` (`View` or `Download`), storing document ID, user ID, timestamp, and client IP address. [src: backend/MicroLIMS.Application/Services/ItemDocumentService.cs:L100-L114].
- Preparation Configuration Sign-off: Captures `CreatedByUserId`, `CreatedAt`, `ApprovedByUserId`, and `ApprovedAt` on `ItemPreparationConfiguration`. [src: backend/MicroLIMS.Application/Services/ItemPreparationConfigurationService.cs:L62, L94-L95].

### MOD-SPEC-001 Specifications
- Automatic Audit Capture: Specification creations, updates, and deletions write audit log entries capturing parameter names, limit types, limits, and display orders. [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L32].

### MOD-WORKBOOK-001 Master Data Workbook
- Electronic Signature on Import Load: Ingestion requires an electronic signature with `SignatureMeaning.MasterDataChanged`, recording user ID, timestamp, meaning, entity type `MasterDataImport`, import ID, file SHA-256 hash, and optional comment. [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L150-L152].
- Electronic Signature on Verification: Verification requires an electronic signature with `SignatureMeaning.Reviewed`, recording verifier user ID, timestamp, meaning, entity type `MasterDataImport`, import ID, and verification comment. [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L216-L218].

### MOD-TESTMASTER-001 Test Master (FP and RM & PM)
- Titration Changed Audit Event: Modifying titration configuration on an existing test triggers `_audit.RecordUserEventAsync("TestDefinition.TitrationChanged", AuditActionCategory.Configuration, "TestDefinition", entityId, changes, reason)`, storing before and after JSON snapshots and mandatory Reason for Change. [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L767-L773].
- Automatic Audit Capture: Test definition creations, updates, and freeze/unfreeze actions are tracked in the audit trail. [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L368, L765, L801, L810].

## traceability

### MOD-ITEM-001 Items
- Item to Sample Traceability: `Sample.ItemId` links each received sample to its parent `Item` (Restrict foreign key). [src: backend/MicroLIMS.Application/Services/ItemService.cs:L113-L126].
- Item to Test Assignment: `Item.AssignedTests` links `Item` to `TestDefinition` by `TestCode`. On sample receipt, `ProductWorkflowEngine.ReceiveAsync` instantiates tests for the sample matching the item's configured tests. [src: frontend/src/modules/laboratoryConfiguration/items/components/AddItemDialog.tsx:L148-L151; backend/MicroLIMS.Application/Services/ItemService.cs:L48, L96-L99].
- Item to Specifications: `Item.Specifications` links `Item` to one or more `Specification` records. Each specification points to `TestDefinition` by `TestCode`, and optionally links to `HplcMethodAnalyte` or `IcpMethodElement`. [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L20-L25, L293-L372].
- Item to Preparation Configuration: `Item` links 1:1 to `ItemPreparationConfiguration` (`ItemId`), defining preparation parameters referenced during sample testing. [src: backend/MicroLIMS.Application/Services/ItemPreparationConfigurationService.cs:L45-L50].
- Item to Controlled Documents: `Item` links 1:N to `ItemDocument` (`ItemId`), maintaining version revisions and access logs. [src: backend/MicroLIMS.Application/Services/ItemDocumentService.cs:L28-L34].

### MOD-WORKBOOK-001 Master Data Workbook
- Workbook Ingestion Traceability: Each `MasterDataImport` row links 1:N to `MasterDataImportRecord` entries storing `EntityType`, `EntityId`, `Sheet`, and `RowNumber`, maintaining direct bidirectional traceability between database entities and the exact source workbook row that created them. [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L168-L172].

### MOD-TESTMASTER-001 Test Master (FP and RM & PM)
- Test Definition to Methods: `TestDefinition.HplcMethodId` links to `HplcMethod`; `TestDefinition.IcpMethodId` links to `IcpMethod`. [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L249-L254, L285-L290].
- Test Definition to Stage Replicates: `TestDefinition` links 1:N to `TestDefinitionStageReplicate` (`TestDefinitionId`, `Role`), resolved during sample testing via `StageReplicateResolver`. [src: backend/MicroLIMS.Application/Services/MasterData/TestStageReplicateMasterDataService.cs:L31-L37; backend/MicroLIMS.Application/Services/StageReplicateResolver.cs:L86-L101].

## gaps

### GAP-700 Item deletion button state vs runtime rejection
- Question: Should the "Delete item" button on the item card be disabled in the UI when the item has already been used to receive samples, instead of waiting for backend rejection?
- Affected: SCR-ITEM-001 (`ItemTable.tsx`), `ItemService.cs`.
- Facts: In `ItemTable.tsx`, the delete button is always clickable. Clicking opens a confirmation dialog warning that deletion will be blocked if samples exist. The backend service checks `sampleCount > 0` and throws `InvalidOperationException` with `Cannot delete '{item.Name}' - it has been used to receive {sampleCount} sample(s). Freeze it instead to stop new samples without losing history.` [src: frontend/src/modules/laboratoryConfiguration/items/ItemTable.tsx:L150-L153; backend/MicroLIMS.Application/Services/ItemService.cs:L122-L126].
- Why it matters: Users attempting to clean up items experience an unexpected error alert after confirming a destructive action.
- Needs confirmation: Whether `ItemDto` should include a `sampleCount` or `canDelete` boolean flag to allow the UI to disable the delete button and show a helpful tooltip recommending Freeze.
- Who could confirm: System Architect / Frontend Lead.

### GAP-701 Item document upload file size limit discrepancy
- Question: What is the authoritative maximum file size for item controlled document uploads: 25 MB or 30 MB?
- Affected: SCR-ITEM-001 (`UploadItemDocumentDialog.tsx`), `ItemDocumentController.cs`, `ItemDocumentService.cs`.
- Facts: In `ItemDocumentController.cs`, the endpoint is decorated with `[RequestSizeLimit(30 * 1024 * 1024)]` (30 MB). In `ItemDocumentService.cs`, the code enforces `fileLength > 25 * 1024 * 1024` (25 MB) with message `File size exceeds the 25 MB limit.` The UI helper text in `UploadItemDocumentDialog.tsx` states `(Max 25MB)`. [src: backend/MicroLIMS.API/Controllers/ItemDocumentController.cs:L34; backend/MicroLIMS.Application/Services/ItemDocumentService.cs:L58-L61; frontend/src/modules/laboratoryConfiguration/items/components/UploadItemDocumentDialog.tsx:L163].
- Why it matters: A 28 MB file passes the ASP.NET request size filter but is rejected by the application service layer with an unexpected validation error.
- Needs confirmation: Align controller attribute with service limit (25 MB) or increase service limit to 30 MB.
- Who could confirm: Backend Lead.

### GAP-702 Standalone Specifications route redirect behaviour
- Question: Is the standalone `/laboratory-configuration/specifications` route permanently superseded by the Items workspace specifications tab, and should it be retired or linked from the navigation menu?
- Affected: SCR-SPEC-001 (`SpecificationsPage.tsx`), `AppRoutes.tsx`, `menuConfig.ts`.
- Facts: `SpecificationsPage.tsx` consists entirely of `<Navigate to="/laboratory-configuration/items" replace />`. The route is registered in `AppRoutes.tsx` under `PERMISSIONS.MASTER_DATA_MANAGE` but is not present in sidebar navigation `menuConfig.ts`. (Resolves GAP-034). [src: frontend/src/modules/laboratoryConfiguration/specifications/SpecificationsPage.tsx:L4-L6; frontend/src/routes/AppRoutes.tsx:L229; frontend/src/routes/menuConfig.ts:L216-L266].
- Why it matters: Users expecting a dedicated specification catalog screen are redirected to Items without explanation.
- Needs confirmation: Whether to document `/laboratory-configuration/specifications` as a legacy redirect to Items or plan a future standalone specification search register.
- Who could confirm: Product Owner / UX Lead.

### GAP-703 Master Data Workbook format constraint and error mapping
- Question: Should the Master Data Workbook upload accept older `.xls` binary workbooks or provide an immediate client-side format rejection before upload?
- Affected: SCR-WORKBOOK-001 (`MasterDataWorkbookPage.tsx`), `MasterDataWorkbookService.cs`.
- Facts: The workbook engine uses ClosedXML (`IWorkbookFormat` implementation), which strictly requires OpenXML `.xlsx` format. The file input in `MasterDataWorkbookPage.tsx` specifies `accept=".xlsx"`. Uploading an older `.xls` or non-Excel file throws `WorkbookFormatException`, which `MasterDataWorkbookService.ReadSheets` catches and wraps into `InvalidOperationException`. [src: frontend/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx:L84; backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L276-L282].
- Why it matters: Users attempting to upload legacy `.xls` files receive generic check errors rather than clear format conversion instructions.
- Needs confirmation: Whether explicit client-side validation for `.xlsx` MIME type/extension should be added with a prompt to save as `.xlsx`.
- Who could confirm: Technical Lead.

### GAP-704 Preparation configuration auto-creation bypasses upfront review
- Question: Should auto-creation of preparation configurations from an analyst's initial test entry require an acknowledgment from the Section Head before being used for subsequent samples?
- Affected: SCR-ITEM-001 (`ItemPreparationConfigurationSection.tsx`), `ItemPreparationConfigurationService.cs`.
- Facts: When an item has no preparation configuration, the first analyst's confirmation automatically seeds `ItemPreparationConfiguration` in `PendingReview` status. The code explicitly notes that testing is never blocked waiting for approval (`usable immediately - testing is never blocked waiting for approval`). The Section Head reviews and approves asynchronously. [src: backend/MicroLIMS.Application/Services/ItemPreparationConfigurationService.cs:L26-L29; frontend/src/modules/laboratoryConfiguration/items/components/ItemPreparationConfigurationSection.tsx:L129-L134].
- Why it matters: If an analyst inputs non-standard dilution or washing parameters during first testing, subsequent samples inherit those parameters until a Section Head inspects the pending queue.
- Needs confirmation: Whether QA policy accepts immediate execution of unapproved seeded protocols or mandates upfront Section Head configuration.
- Who could confirm: QA Manager / Microbiologist Lead.


