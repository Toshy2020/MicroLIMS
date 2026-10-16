## 31. Item Master, Test Master and Specifications

MicroLIMS maintains laboratory testing catalogs through the Item Master, Test Master definitions, and acceptance specifications. The Item Master defines tested products, raw materials, and packaging materials, along with their auto-assigned test batteries, controlled standard operating procedures (SOPs), and microbiology sample preparation configurations. The Test Master defines individual analytical tests, incubation chains, calculation parameters, and method links across Microbiology, Finished Product (FP), and Raw & Packaging Material (RM & PM) areas. Specifications establish numeric, count-tiered, qualitative, and staged acceptance criteria evaluated during testing.

MicroLIMS does not maintain specifications on a stand-alone page; accessing the stand-alone specifications route (`/laboratory-configuration/specifications`) redirects automatically to the Items page (`/laboratory-configuration/items`), where specifications are configured within the item workspace under the **Specifications** tab.

---

### 31.1 Item Master and Assigned Tests
<!-- WF-ITEM-001 -->
#### Purpose
Create, configure, edit, freeze, and delete catalog items across Product, Raw Material, and Packaging Material categories, and assign the test battery automatically ordered upon sample receipt.

#### Who can use it
Requires the *Manage items* permission (by default: Section Head, System Administrator). Item records are shared across all laboratories and are not scoped to a specific laboratory section.

#### Before you start
- Tests to be assigned must already exist in the Microbiology or Physicochemical Test Master.
- The item code must be unique across the system.

#### What you see
- Page title **Items**, subtitle "Configure which tests are auto-assigned when a sample is received."
- Action button: **Add Item**.
- Filter bar (**ItemFilterBar**):
  - Search field: placeholder "Search by item name, item code, SOP number...".
  - **Category** dropdown: **All Categories**, **Product**, **Raw Material**, **Packaging Material**.
  - **Status** dropdown: **All Statuses**, **Active**, **Frozen**.
  - Button **Reset** (visible when filters are active).
- Item Register: Split workspace layout when an item is selected (compact register on the left, workspace on the right) or full-width register when no item is selected.
  - Card displays: Item Name, Code in parentheses, Category badge, Status badge (**Active** / **Frozen**), and summary chips (**{N} Tests**, **{N} Specs**, **{N} Docs**).
  - Row actions: **Edit item**, **Freeze item** / **Unfreeze item**, **Delete item**.
- **Add New Item** / **Edit Item** dialog:
  - Fields: **Item Name** (required), **Item Code** (required), **SOP Number** (optional), **Category** (select: Product, Raw Material, Packaging Material), **Assigned Tests** (multi-select picker).
  - Action buttons: **Cancel**, **Save Item** / **Save Changes**.

[SCREENSHOT SS-311]

#### How to use it
##### Creating an Item:
1. Navigate to **General Laboratory Configuration** > **Items**.
2. Click **Add Item**.
3. Enter the **Item Name \*** and unique **Item Code \***.
4. Optionally enter the governing **SOP Number**.
5. Select the **Category** (**Product**, **Raw Material**, or **Packaging Material**).
6. In the **Assigned Tests** field, select all tests required for this catalog item.
7. Click **Save Item**. The system creates the item in status **Active** and opens its workspace.

##### Editing an Item:
1. On the item card, click **Edit item**.
2. Modify metadata or update the **Assigned Tests** list.
3. Click **Save Changes**. Replacing assigned tests updates future sample receiving; past received samples and existing test orders are not modified.

##### Freezing and Deleting an Item:
1. To prevent new sample receipts while preserving history, click **Freeze item**. The status changes immediately to **Frozen**. Click **Unfreeze item** to re-enable sample receiving.
2. To permanently delete an item, click **Delete item** and confirm the dialog: "Delete item \"{name}\" ({code})? This cannot be undone. If it has already been used to receive samples, deletion will be blocked - freeze it instead."

#### System behavior
- Category selection is restricted to Product (`FinishedProduct`), Raw Material (`RawMaterial`), and Packaging Material (`PackagingMaterial`).
- Item codes are compared case-sensitively and must be unique.
- When a sample is received, the system instantiates one test order for each assigned test, routed to the section that owns the test.
- Deleting an item is refused by the server if any sample has ever been received for it.
- Item creation, modification, freezing, and deletion are recorded in the general audit log; no electronic signature password or reason for change is required.

> **Note for reviewers:** The dialog requires at least one assigned test, but the server and workbook accept an item with zero assigned tests; such an item cannot receive samples. (GAP-710)

> **Note for reviewers:** Items, specifications, and receiving lists are saved without a reason or electronic signature; changes are recorded in the generic audit trail only. (GAP-705)

> **Note for reviewers:** Creating tests inline via "+ Add to Test Master" inside the item picker creates an Observation test without workflow steps; configure tests in the Test Master first. (GAP-416, GAP-714)

#### Validation
- Client validations:
  - "Item Name, Item Code, and at least one assigned test are required."
- Server validations:
  - "An item with code '{code}' already exists."
  - "Items can only be configured for Product, Raw Material, or Packaging Material categories. Other categories are configured in their dedicated modules."
  - "Item {id} not found."
  - "Cannot delete '{name}' because it has been used to receive {n} sample(s). Freeze the item instead to disable receipt of new samples."
  - "This record was changed by someone else while you were working on it. Nothing was saved - reload it and make your change again."

#### Status
- Item status: **Active** (sample receipt enabled) or **Frozen** (sample receipt disabled).

---

### 31.2 Item Preparation Configuration and Controlled Documents
<!-- WF-ITEM-003 -->
#### Purpose
Manage controlled item documentation (SOPs and Verification Reports) with revision tracking and download logging, and configure standardized microbiology sample preparation protocols.

#### Who can use it
- **Viewing documents and protocols**: Available to any signed-in user.
- **Configuring and approving preparation protocols**: Requires the *Manage items* permission (by default: Section Head, System Administrator).
- **Uploading controlled documents**: Requires the *Upload item documents* permission (by default: Section Head, System Administrator).

#### Before you start
- Document files must be PDF, Word, Excel, or image files up to 25 MB.
- Preparation parameters (sample amount, technique, diluent, neutralizer, wash volumes) must be established.

#### What you see
- Item Workspace tabs:
  - **Documents & Attachments ({N})**:
    - **SOP (Standard Operating Procedure)**: Current card (filename, version e.g. `Rev 01`, effective date, size, uploader), historical accordion (**Historical SOP Versions ({N})**), and button **+ Upload SOP**.
    - **Verification Report**: Current card, historical accordion (**Historical Verification Reports ({N})**), and button **+ Upload Verification Report**.
  - **Preparation Configuration**:
    - Unconfigured state: Info alert "No preparation configuration set for {item}. One will be created automatically from the first analyst confirmation, or you can configure it now."
    - Configured state: Status badge (**Approved** or **Pending Approval**), caption showing approver and timestamp, summary block, and action buttons (**Approve**, **Edit**, **Configure Preparation**).

#### How to use it
##### Uploading Controlled Documents:
1. In the Item Workspace, open the **Documents & Attachments** tab.
2. Click **+ Upload SOP** or **+ Upload Verification Report**.
3. Select the **Document Type** (SOP or Verification Report).
4. Enter the **Version** (defaults to "Rev 01") and select the **Effective Date**.
5. Select the document file and click **Upload**.
6. The uploaded file becomes status **Current**; any prior current document transitions to **Superseded**.

##### Configuring and Approving Preparation Protocols:
1. Open the **Preparation Configuration** tab.
2. Click **Configure Preparation** (or **Edit**).
3. Enter the **Sample Amount** (numeric).
4. Select the **Technique** (**Pour Plate** or **Filtration**).
5. If **Filtration** is selected, enter **Filtration Volume (ml)** and **Washing Volume (ml)**.
6. Enter the mandatory **Diluent** and **Neutralizer** names.
7. Click **Save Configuration**. The protocol is saved in status **Pending Approval**.
8. A Section Head reviews the protocol and clicks **Approve**. The status changes to **Approved**.

#### System behavior
- Document uploads compute and store a SHA-256 hash. Every view and download event is recorded in the document access log.
- Uploading a new document automatically supersedes the existing current version with the audit reason "Superseded by new version '{version}'".
- Item documents cannot be voided from the user interface.
- If an item lacks a preparation protocol, the analyst's first confirmed preparation entry seeds a protocol in status **Pending Approval**.
- A preparation configuration in status **Pending Approval** is immediately active and used for routine testing; approval records administrative review.
- Editing an approved preparation protocol resets its status to **Pending Approval** and clears previous approver metadata.
- Approving a preparation configuration is a button click and does not require an electronic signature password.

> **Note for reviewers:** Approval of an item preparation configuration is an unsigned click and can be completed by the user who authored or edited it; the protocol is used for testing even while Pending Approval. (GAP-704, GAP-707)

> **Note for reviewers:** Controlled documents on items cannot be voided; uploading a replacement supersedes the existing document. The upload size limit is 25 MB. (GAP-701, GAP-711)

#### Validation
- Document upload validations:
  - "Please select a document file to upload."
  - "File size exceeds the 25 MB limit."
  - "Document file is empty."
- Preparation configuration validations:
  - "Diluent is required."
  - "Neutralizer is required."
  - "Filtration technique requires both Filtration Volume and Washing Volume."
  - "This preparation configuration is already approved."

#### Status
- Item document status: **Current**, **Superseded**.
- Preparation configuration status: **Pending Approval** (`PendingReview`), **Approved**.

---

### 31.3 Item Specifications and Limit Evaluation
<!-- WF-ITEM-002 -->
#### Purpose
Define, edit, and delete quantitative, qualitative, and staged acceptance criteria for test parameters assigned to catalog items, establishing the limits evaluated during analytical testing.

#### Who can use it
Requires the *Manage master data* permission (by default: Section Head, System Administrator) AND active membership in the laboratory section that owns the assigned test. System Administrators are unrestricted. Items workspace navigation itself requires *Manage items*.

#### Before you start
- The test must be assigned to the item.
- For physicochemical tests (HPLC, GC, ICP, Dissolution, Disintegration, Weight Variation, Titration), the test and its analytical method must exist in the Test Master.

#### What you see
- Item Workspace tab: **Specifications ({N})**.
  - Button: **Add Specification Parameter**.
  - Specifications table grouped by assigned test, showing parameter name, stage role chip, limit text, unit, and action buttons (**Edit**, **Delete**). Rows owned by another laboratory section are displayed read-only with a section badge.
- **Add Specification Parameter** / **Edit Specification Parameter** dialog:
  - Common fields: **Assigned Test \***, **Parameter Name \***, **Production Stage** (Bulk, InProcess, Finished, Stability; Finished Product items only), **Limit Type \***, **Unit**, **Reference Standard**, **Dilution Factor** (Count-Tiered only).
  - Dynamic limit fields based on selected Limit Type:
    - **Range (NLT - NMT)**: Lower Limit (NLT) \*, Upper Limit (NMT) \*, Lower inclusive (>=), Upper inclusive (<=).
    - **NMT (Not More Than)**: Upper Limit (NMT) \*.
    - **NLT (Not Less Than)**: Lower Limit (NLT) \*.
    - **Target +/- Tolerance**: Target \*, Tolerance \*, Mode \* (Absolute / Percent).
    - **Count-Tiered (Alert / Action / Spec)**: Alert Limit, Action Limit, Specification Limit \*.
    - **Qualitative (descriptive text)**: Expected Result Text \*.
    - **Presence / Absence**: Expected State \*, Sample Quantity, Quantity Unit.
    - **Multi-Stage Criteria**: Stage #, Stage Label \*, Acceptance Criteria Text \*.
    - **Dissolution Q**: Q (% dissolved) \*, Label Claim \*, Unit (fixed %).
    - **Disintegration Time**: Time limit (min) \*, Unit (fixed min).
    - **Weight Variation (USP <2091>)**: Dosage Form \* (Tablet / Capsule), Unit (fixed mg).
    - **HPLC Method Assay**: Analyte \*, Basis \*, Label Claim \*, Label Claim Unit \*.
  - Action buttons: **Cancel**, **Add Parameter** / **Save Changes**.

[SCREENSHOT SS-312]

#### How to use it
1. In the Item Workspace, open the **Specifications ({N})** tab.
2. Click **Add Specification Parameter** (or click **+ Add parameter to {test}** under a specific test group).
3. Select the **Assigned Test \***. The **Limit Type** automatically defaults based on the test's workflow type.
4. Enter the **Parameter Name \***.
5. If configuring for a Finished Product item, optionally select a **Production Stage** (leave blank for all stages).
6. Fill in the required threshold inputs for the chosen limit type.
7. Click **Add Parameter** (or **Save Changes** when editing).
8. To remove a parameter, click **Delete** on the specification row and confirm: "Delete specification parameter \"{parameter}\" for \"{item}\"? This cannot be undone."

#### System behavior
- Canonical formatting: On save, the server automatically formats and overwrites the canonical limit text for all types except Count-Tiered (e.g., `90.0-110.0`, `NMT 10`, `Q = 75 %`, `NMT 15 min`).
- Stage precedence: If a sample's production stage role matches stage-specific specification rows, only those stage-specific rows apply. If no stage-specific rows match, general (every-stage) rows apply. General and stage-specific rows are never combined.
- Result comparison: `SpecificationEvaluator` performs exact decimal comparisons without rounding. Range, NMT, NLT, and Target boundaries are inclusive by default (a reading equal to the boundary passes).
- Count-tiered limits: Evaluated in order: Specification limit (exceeded $\rightarrow$ OutOfSpecification), Action limit ($\rightarrow$ ActionLimitExceeded), Alert limit ($\rightarrow$ AlertLimitExceeded). A count equal to the limit passes.
- Changes to specifications apply to future result calculations; results already recorded retain the limits copied at calculation time.
- No electronic signature password or change reason is required to create, edit, or delete specifications.

> **Note for reviewers:** The stand-alone route `/laboratory-configuration/specifications` redirects to the Items workspace; specifications are edited only within an item. (GAP-702)

> **Note for reviewers:** Specification ownership is checked against the laboratory section of the test definition; a user belonging to another section cannot edit the specification. (BR-SPEC-005)

> **Note for reviewers:** Rounding behavior before specification comparison varies across test types; titration evaluates the rounded mean whereas measurement passes unrounded values. (GAP-712)

#### Validation
- "This specification belongs to {labName} - only its Section Head can change it."
- "Test code '{testCode}' is not in the Test Master."
- "Test '{testCode}' is not assigned to item {id}."
- "Item {id} not found."
- "Specification parameter '{name}' already exists for test '{code}' on item {id}."
- "Dilution factor is only allowed for Count-Tiered specifications."
- "Dilution factor must be greater than zero."
- "Lower limit must be less than upper limit."
- "Target must be greater than zero."
- "Tolerance must be greater than zero."
- "Tolerance percent must be between 0 and 100."
- "Production stage is only allowed for Finished Product items."
- "Choose Bulk, In-Process, Finished or Stability, or leave the stage as all stages."
- "A specification with stage role '{role}' already exists for test '{code}' on item {id}."
- "At least one specification limit is required."
- "Dissolution tests only accept Dissolution Q specifications."
- "Disintegration tests only accept Disintegration Time specifications."
- "Weight variation tests only accept Weight Variation specifications."
- "Only one Dissolution Q specification is allowed per test and stage role."
- "Only one Disintegration Time specification is allowed per test and stage role."
- "Only one Weight Variation specification is allowed per test and stage role."
- "Analyte '{name}' does not belong to HPLC method '{method}'."
- "Elemental impurity '{symbol}' does not belong to ICP method '{method}'."
- "Residual solvent '{name}' does not belong to GC method '{method}'."
- "Q (% dissolved) must be greater than 0 and less than or equal to 100."
- "Time limit (min) must be greater than zero."
- "Weight variation dosage form must be Tablet or Capsule."

#### Status
- Evaluated result verdicts: **WithinLimits**, **OutOfSpecification**, **AlertLimitExceeded**, **ActionLimitExceeded**, **LimitsNotConfigured**.

---

### 31.4 Microbiology Test Master and Workflow Steps
<!-- WF-TESTMASTER-001 (micro) -->
#### Purpose
Define microbiology tests, workflow types (Count Test, Observation), ordered workflow steps, required growth media and incubation conditions, and incubation transfer rules.

#### Who can use it
Requires the *Manage master data* permission (by default: Section Head, System Administrator).

#### Before you start
- Media products and incubation conditions must be configured in Media Configurations.
- Target microorganisms must exist in the Organisms master catalog.

#### What you see
- Page title **Microbiology Test Master**, subtitle "Microbiology tests available to assign to Items, Sampling Points, Rooms, and Machine Parts."
- Action button: **Add Test**.
- Table columns: **Code**, **Display Name**, **Section**, **Status** (**Active** / **Frozen**).
- Row menu: **Details & Workflow Steps**, **Edit**, **Freeze** / **Unfreeze**.
- **Details & Workflow Steps** dialog:
  - Workflow steps table with step ordering buttons (**Move up**, **Move down**), chips (**2-Stage**, **Transfer**, **Needs configuration**), and action buttons (**Edit step**, **Delete step**).
  - Step configuration form: **Step Name**, **Step type** (PlateCount, BrothEnrichment, SelectiveBroth, SelectivePlating, ConfirmatoryPlating, BiochemicalTest), **Target Organism (required)**, phenotypic test selector, **Final Step** checkbox, **Requires incubation transfer** checkbox, and **Stage 2** parameters.
  - **Step Media** section: Assigned stock media, incubation condition picker, **Required** checkbox, and **Add Medium** button.

[SCREENSHOT SS-313]

#### How to use it
##### Creating a Microbiology Test:
1. Navigate to **Laboratories** > **Microbiology Configuration** > **Test Master**.
2. Click **Add Test**.
3. Enter the unique **Code \*** and **Display Name \***.
4. Select the **Section \*** (Microbiology) and **Workflow Type \*** (**Count Test** or **Observation**).
5. Click **Add Test**.

##### Configuring Workflow Steps:
1. Locate the test in the register and select **Details & Workflow Steps**.
2. Click **Add Step**.
3. Enter the **Step Name \*** and select the **Step type**.
4. Configure step-specific requirements:
   - For *SelectivePlating* or *ConfirmatoryPlating*: Select the mandatory **Target Organism**.
   - For *BiochemicalTest*: Add one or more phenotypic test types.
   - For *PlateCount*: If two-stage incubation is required, check **Requires incubation transfer** and enter Stage 2 temperature and duration ranges.
5. In the **Step Media** section, click **Add Medium**, select the dehydrated media stock item and its configured incubation condition, and indicate whether the medium is required.
6. Check **Final Step** if this step completes the testing workflow (only one final step allowed per test).
7. Click **Add Step** / **Save Changes**.
8. To remove an unused step, click **Delete step**. Steps already used by test orders cannot be deleted.

##### Freezing a Test:
1. In the row menu, select **Freeze**. The test transitions to **Frozen** and is hidden from selection pickers for new orders. Existing orders are not affected.

#### System behavior
- Template validation checks all structural rules simultaneously and returns all violations in a consolidated message (`Rule {n} ({step name}): {text}`).
- Step media temperature ranges and incubation hours are copied directly from the linked incubation condition.
- A step used by an active or completed test order cannot be deleted.
- Test Master changes, workflow step edits, and freezing/unfreezing are recorded in the general audit log; no electronic signature password or change reason is required.

> **Note for reviewers:** Freezing a test definition does not check for open test orders, and its effect on open testing is unconfirmed. (GAP-411)

> **Note for reviewers:** Test Master changes carry no reason and are not electronically signed. (GAP-412)

> **Note for reviewers:** The interface cannot configure the confirmatory media count; the server stores a default of 1. (GAP-424)

#### Validation
- "Test code \"{code}\" already exists in the Test Master."
- "Both Code and Display Name are required."
- "Laboratory section is required."
- "This test belongs to a laboratory section you are not assigned to."
- "{WorkflowType} tests have no workflow steps."
- "Cannot delete step \"{name}\" - it has already been used by a test order."
- "This step is already at that end of the sequence."
- "Only one step per test can be marked as the final step."
- "Workflow steps must have contiguous step numbers starting from 1."
- Template validation rules:
  - Rule 1: "A broth step must have exactly one assigned medium, marked as required."; "A broth step must not target an organism."
  - Rule 2: "A selective plating step must have exactly one assigned medium, marked as required."; "A selective plating step must target an organism."
  - Rule 3: "Confirmatory media count must be at least 1."; "A confirmatory plating step requires at least {n} permitted media, all analyst-selectable."; "A confirmatory plating step must have at least one permitted medium, all analyst-selectable."; "A confirmatory plating step must target an organism."
  - Rule 4: "A biochemical test step must have no assigned media."; "A biochemical test step must not target an organism."; "A biochemical test step must specify at least one phenotypic test type."; "Phenotypic test type {type} is assigned to this step more than once."
  - Rule 5: "Medium {materialId}: the minimum temperature must be below the maximum."
  - Rule 6: "Medium {materialId} is assigned to this step more than once."
  - Rule 7: "A step requiring incubation transfer must define stage 2's temperature and incubation-hours range."; "Stage 2's minimum temperature must be below its maximum."; "Stage 2's incubation-hours range must have a positive minimum and a maximum no less than the minimum."; "Only a PlateCount step with incubation transfer enabled may define a second incubation stage."
  - Rule 8: "At least one medium is required for this step type."; "Only a biochemical test step may specify a phenotypic test type."
  - Rule 9: "Medium {materialId}: the incubation-hours range must have a positive minimum and a maximum no less than the minimum - set it on the media configuration."

#### Status
- Test status: **Active**, **Frozen**.

---

### 31.5 Physicochemical Test Master (FP and RM & PM)
<!-- WF-TESTMASTER-001 (physchem) -->
#### Purpose
Define physicochemical test methods across Finished Product (FP) and Raw Material & Packaging Material (RM & PM) areas, establish calculation models and equation types, link chromatography and spectrometry methods, and configure stage-specific replicate requirements.

#### Who can use it
Requires the *Manage master data* permission (by default: Section Head, System Administrator) AND active membership in the Physicochemical laboratory section (`FP`). System Administrators are unrestricted.

#### Before you start
- HPLC, GC, and ICP methods must be created in their respective method masters before linking to method assay tests.
- Master solutions, titrants, and primary standards must exist before configuring titration tests.

#### What you see
- Page title **FP Test Master** (route `/laboratory-configuration/fp-test-master`, area `fp`) or **RM & PM Test Master** (route `/laboratory-configuration/rm-pm-test-master`, area `rmpm`).
- Subtitle: "Physicochemical tests: HPLC methods, equation type and system suitability criteria."
- Columns: **Code**, **Display Name** (with method chips e.g. HPLC Assay, ICP Assay, Dissolution, SST requirements), **Section**, **Status** (**Active** / **Frozen**).
- Action button: **Add Test**.
- **Add Test** / **Edit Test** dialog:
  - Fields: **Code \***, **Display Name \***, **Section \***, **Workflow Type \***, **Equation Type \***.
  - Area checkbox: **Also used for RM & PM** (on FP page) or **Also used for FP** (on RM & PM page).
  - Type-specific configuration sections:
    - *HPLC Method Assay*: Method selector, read-only abbreviation, mandatory system suitability toggle.
    - *ICP Method Assay*: Method selector, read-only abbreviation.
    - *Measurement*: Replicate count (1-30), Evaluation basis (Mean, Min, Max, EachValue).
    - *Gravimetric*: Replicate count (1-30), Tare switch, Condition fields.
    - *Dissolution*: Linked HPLC method, S1/S2/S3 stage offsets.
    - *Disintegration*: Stage 1 and Stage 2 units, failure thresholds.
    - *Weight Variation*: Tablet and capsule weight bands, deviation limits, outer tolerances.
    - *Titration*: Titration type, mode, calculation method, titrant, excess titrant, equivalency factor, indicator, temperature correction.
    - *Titration Change Reason*: Mandatory reason field (5-500 characters) when modifying titration settings.
- **Replicates per stage** box (for non-titration tests): Allows configuring replicate counts per production stage role (Bulk, InProcess, Finished, Stability).

[SCREENSHOT SS-314]

#### How to use it
1. Navigate to **Laboratories** > **Physicochemical Configuration** > **FP Test Master** (or **RM & PM Test Master**).
2. Click **Add Test**.
3. Enter the unique **Code \*** and **Display Name \***.
4. Select the **Workflow Type \*** and matching **Equation Type \***.
5. If the test applies to both finished products and raw materials, check the **Also used for...** area checkbox.
6. Configure the parameters in the type-specific settings box.
7. Click **Add Test**.
8. To set stage-specific replicate numbers, reopen the test with **Edit**, navigate to **Replicates per stage**, click **Add Stage Replicate**, select the **Stage Role**, enter the replicate count, and save.
9. To modify titration settings on an existing test, enter the required justification in **Reason for Change** (5-500 characters) before saving.

#### System behavior
- Workflow types and equation types are strictly coupled (e.g., Workflow `Measurement` requires Equation `Measurement`).
- HPLC method assay and dissolution tests mandate system suitability; ICP method assays use run calibration and forbid system suitability toggles.
- Modifying titration parameters requires a typed reason and writes a `TestDefinition.TitrationChanged` audit event with before and after settings. No electronic signature is required.
- Freezing a test definition can be performed by any user holding *Manage master data*; freezing does not enforce section scoping.

> **Note for reviewers:** Freezing a test is not restricted to the test's owning section. (GAP-706)

> **Note for reviewers:** User-level physchem area assignments (FP vs. RM & PM) are not enforced when editing test definitions or specifications. (GAP-708)

> **Note for reviewers:** Dissolution stage offsets and weight variation bands pre-filled in the dialog are baseline system defaults; specific product monographs must configure appropriate values. (GAP-610, GAP-611)

#### Validation
- "Test code \"{code}\" already exists in the Test Master."
- "Both Code and Display Name are required."
- "Laboratory section is required."
- "This test belongs to a laboratory section you are not assigned to."
- "Workflow type must be Measurement when equation type is Measurement."
- "Replicate count must be between 1 and 30 when equation type is Measurement."
- "Evaluation basis is required when equation type is Measurement."
- "Replicate count must be between 1 and 30 when equation type is {EquationType}."
- "HPLC method is required for HPLC method assay tests."
- "The HPLC method belongs to another laboratory."
- "The HPLC method is inactive."
- "HPLC method assay tests must require system suitability."
- "ICP method is required for ICP method assay tests."
- "ICP method assay tests use the run calibration, not system suitability."
- "Dissolution tests must require system suitability: the standard comes from the HPLC run the sample is assigned to."
- "Dissolution stage offsets are required for Dissolution tests."
- "Disintegration tests must not require system suitability."
- "Weight variation tests must not require system suitability."
- "A reason for change is required when titration settings change."
- "Choose the area of this test (FP, RM & PM or Both)."
- "Items {items} still have specifications on this test for {lostArea} - remove them first."
- "Sample replicates must be at least 1."
- "Stage replicate configuration for role {role} already exists for this test definition."

#### Status
- Test status: **Active**, **Frozen**.

---

### 31.6 Equation Types
#### Purpose
Review system calculation formulas, required mathematical parameters, and input variables available for test definitions.

#### Who can use it
Requires the *Manage master data* permission (by default: Section Head, System Administrator).

#### What you see
- Page title **Equation Types**, subtitle "Predefined mathematical formulas and required calculation parameters for analytical test methods and system suitability."
- Read-only catalog displaying six predefined calculation formulas:
  1. **None**: Descriptive or manual calculation.
  2. **System Suitability**: SST parameters (retention time, peak area, resolution, tailing, plates).
  3. **Numeric Measurement**: Direct reading, replicates, mean, min, max, standard deviation, relative standard deviation.
  4. **Gravimetric % Loss**: Formula `(W1 - W2) * 100 / W1`.
  5. **Gravimetric % Residue**: Formula `W2 * 100 / W1`.
  6. **Qualitative / Identification**: Descriptive comparison and conformance judgment.

#### System behavior
- The Equation Types register is hardcoded and read-only by design. There are no create, update, or delete operations.

> **Note for reviewers:** The Equation Types page displays only six legacy equation types and is read-only; newer test types (Dissolution, Disintegration, Weight Variation, Titration, HPLC method assay, ICP method assay) are configured directly through their specialized Test Master workflow types. (GAP-606)
