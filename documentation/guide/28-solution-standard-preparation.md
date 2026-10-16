## 28. Solution and Standard Preparation

MicroLIMS provides dedicated laboratory workflows for preparing and standardizing chemical solutions, mobile phases, diluents, volumetric titrants, and in-house secondary working standards. These workflows manage recipes, track stock consumption against received inventory lots, enforce expiry clocks, and require electronic signatures.

---

### 28.1 Solution Preparation: Mobile Phases, Diluents, and Titrants
<!-- WF-SOLN-001 -->
#### Purpose
Prepare mobile phases (MP), diluents (DL), and volumetric titrants (VS) from approved master recipes against received raw material lots, deduct stock quantities upon signed completion, track pH tolerances, and monitor automated shelf-life expiration.

#### Who can use it
Requires the *Prepare solutions* permission (by default: Analyst, Section Head, System Administrator).

#### Before you start
- An active solution recipe must exist in the Solutions Master (`/laboratory-configuration/solution-master`).
- For mobile phase preparations, an active HPLC method belonging to the same laboratory section that defines the mobile phase must exist.
- For each recipe component, an active stock lot in the same laboratory linked to the component's reagent master entry must be available in Materials Stock (`InStock` status, not expired, and sufficient quantity).

#### What you see
- Solution Preparation list (`/preparation`):
  - Filters: Status (**All Statuses**, **In Progress**, **Prepared**, **Expired**, **Discarded**, **Cancelled**) and Type (**All Types**, **Mobile Phase**, **Diluent**, **Titrant**).
  - Columns: **Code**, **Type**, **Solution Recipe**, **HPLC Method**, **Status**, **Prepared At**, **Expires At**, **Prepared By**.
  - Row actions: **Resume Preparation** (for In Progress items) or **View Record**.
  - Action button: **Start Preparation**.
- Preparation Wizard (`/preparation/new`):
  - Step rail: **Select Recipe & Method**, **Lots & Quantities**, **Review & Sign**.
  - Step 1: **Solution Type** toggle, **Solution Recipe \*** dropdown, and **HPLC Method \*** (for mobile phases).
  - Step 2: Component lot pickers displaying **Select Lot \*** (with remaining stock and expiry) and **Quantity Used** (in the lot's stocking unit); **Final Volume \*** (mL); and **Measured pH \*** (with helper showing recipe target and tolerance, e.g., "Recipe target: 4.50 ± 0.05").
  - Step 3: Review summary table (**Reagent / Material**, **Recipe Target**, **Chosen Stock Lot**, **Quantity Used**) and action button **Sign & Complete Preparation**.

[SCREENSHOT SS-281]
[SCREENSHOT SS-282]

#### How to use it
1. Navigate to **Laboratories** > **Physicochemical Laboratory** > **Solution Preparation**.
2. Click **Start Preparation**.
3. In **Select Recipe & Method**, select the **Solution Type**, **Solution Recipe**, and (if preparing a mobile phase) the target **HPLC Method**. Click **Start Preparation**. The preparation is created in status **In Progress**.
4. In **Lots & Quantities**:
   - For each recipe component, select an active, in-date stock lot from the dropdown. Blocked lots show the reason (e.g., "No stock left.", "Expired on {date}.").
   - Enter the **Quantity Used** in the component's stocking unit.
   - Enter the **Final Volume \*** in mL.
   - If the recipe specifies a target pH, measure the solution pH and enter it in **Measured pH \***. If no target pH is configured, the field displays **Measured pH (Optional)**.
   - Click **Save Draft** to store work in progress, or click **Continue to Review**.
5. In **Review & Sign**, verify the recipe targets against selected lots and quantities.
6. Click **Sign & Complete Preparation**.
7. In the **Electronic Signature** dialog, verify the statement: "I confirm that this solution was prepared according to the approved recipe and that all listed lot quantities were consumed."
8. Enter your password and optional comment, then click **Sign**.
9. The system validates all inputs, consumes component stock quantities, allocates an official preparation code, calculates the expiration timestamp, transitions status to **Prepared**, and opens the record page.

#### System behavior
- **Official code assignment**: Codes are allocated at signed completion using annual numbering sequences:
  - Mobile Phase: `MP-{ABBR} nn/MM/yyyy` (where `{ABBR}` is the HPLC method abbreviation).
  - Diluent: `DL-nn/MM/yyyy`.
  - Volumetric Titrant: `VS-nn/MM/yyyy`.
  - If two preparations complete at the exact same moment: "Another preparation took that code at the same moment - sign again."
- **Stock consumption**: Component quantities are immediately deducted from inventory stock in the same database transaction.
- **Expiry calculation**: Expiry is calculated as `ExpiresAt = PreparedAt + ShelfLife` (configured in hours or days).
- **Automated expiry worker**: A background service executes every 5 minutes; any **Prepared** solution whose expiry timestamp has passed is automatically updated to **Expired** ("Expired automatically"). Expired solutions cannot be selected in run wizards or titration workflows.
- **Cancellation and discarding**:
  - While **In Progress**, clicking **Cancel Preparation** moves status to **Cancelled** with a mandatory reason (up to 500 characters). No stock is consumed.
  - While **Prepared**, clicking **Discard Solution** moves status to **Discarded** with a mandatory reason. Consumed stock is not restored. Closed preparations cannot be edited.

> **Note for reviewers:** Solution shelf-life expiration is evaluated against UTC server timestamps; confirm whether site procedures require local wall-clock midnight expiration for days-based shelf lives. (GAP-605)

#### Validation
- "Solution \"{name}\" is inactive."
- "An HPLC method is required to start a mobile phase preparation."
- "That method does not use this mobile phase."
- "An HPLC method only applies to mobile phase preparations."
- "The HPLC method belongs to another laboratory."
- "This preparation is closed."
- "Quantity used for \"{x}\" must be greater than zero."
- "Final volume must be greater than zero."
- "Measured pH must be between 0 and 14."
- "Every component needs a lot and a quantity before completion."
- "Final volume is required before completion."
- "Measured pH is required before completion."
- "Measured pH {X} is outside {T} ± {tol}."
- "Lot is not linked to this reagent."
- "Expired on {date}."
- "No stock left."
- "Only {N} {UNIT} left."
- "Another preparation took that code at the same moment - sign again."
- "A reason is required."
- "Reason cannot exceed 500 characters."

#### Status
- Preparation status lifecycle: **In Progress** $\rightarrow$ **Prepared** (on signed completion) $\rightarrow$ **Expired** (automatic) or **Discarded** (manual with reason).
- An In Progress preparation can transition to **Cancelled** (manual with reason).

---

### 28.2 Standardization of Volumetric Titrants
<!-- WF-SOLN-002 -->
#### Purpose
Standardize prepared volumetric titrants against primary reference standards or secondary volumetric reference solutions, determine the molarity/normality factor ($f$), evaluate replicate RSD, and update factor validity.

#### Who can use it
Requires the *Prepare solutions* permission (by default: Analyst, Section Head, System Administrator).

#### Before you start
- The titrant preparation must have status **Prepared** (code `VS-...`) and must not be expired or discarded.
- The titrant recipe in the Solutions Master must have standardization rules configured: nominal strength and unit, standardization mode (**PrimaryStandard** or **AgainstVolumetricSolution**), required replicate count, factor acceptance range, and maximum RSD.
- For **PrimaryStandard** mode: A usable stock lot of material type `PrimaryStandard` linked to the titrant's primary standard entry must exist in the laboratory with recorded purity and available stock.
- For **AgainstVolumetricSolution** mode: A **Prepared**, unexpired preparation of the configured reference solution must exist and be currently **Valid** (or standardized today for "before each use" titrants).

#### What you see
- Titrant Record Page (`/preparation/:id`):
  - **Current Factor** card showing factor status (**Valid**, **Due for Restandardization**, **Restandardize Before Each Use**, or **Not Standardized**), mean factor value, and validity date.
  - Action button: **Standardize** (visible only for Prepared titrants).
  - **StandardizationHistoryTable**: Columns for **Standard Lot**, **Weight (mg)**, **Reference Solution**, **Ref Volume (mL)**, **Ref Factor**, **Titrant Vol (mL)**, **Blank (mL)**, **Calculated Factor**, **Date & Time**, **Analyst**, **Mean Factor**, **Status**, **Valid Until**.
- Standardization Dialog (**Standardize Titrant: {code or name}**):
  - Optional field: **Temperature (°C)** (-20 to 60 °C, helper: "Optional. Needed later for non-aqueous temperature volume correction.").
  - Replicate entry rows:
    - Primary Standard mode: **Standard Lot**, **Weight (mg)**, **Titrant Vol (mL)**, and **Blank (mL)** (if configured).
    - Reference Solution mode: **Reference Solution**, **Ref Vol (mL)**, **Titrant Vol (mL)**, and **Blank (mL)**.
  - Action buttons: **Cancel**, **Sign & Submit**.

[SCREENSHOT SS-283]

#### How to use it
1. Navigate to **Solution Preparation** and open the prepared volumetric titrant (`VS-...`).
2. On the **Current Factor** card, click **Standardize**.
3. If non-aqueous temperature volume correction will be used, enter the **Temperature (°C)**.
4. For each required replicate:
   - Primary standard: Select the primary standard lot and enter the weighed amount in **Weight (mg)**.
   - Reference solution: Select the reference solution preparation and enter the volume in **Ref Vol (mL)**.
   - Enter the consumed titrant volume in **Titrant Vol (mL)**.
   - If the titrant recipe requires a blank, enter the **Blank (mL)**.
5. Click **Sign & Submit**.
6. In the **Electronic Signature** dialog, review the statement: "I confirm that this titrant was standardized according to SOP and that the titration values entered are accurate."
7. Enter your password and click **Sign**.
8. The system calculates replicate factors, evaluates the mean factor against acceptance limits and RSD, signs the record (`TitrantStandardized`), deducts standard weights from inventory stock (primary standard mode), and updates the **Current Factor** card. Click **Close** to exit the dialog.

#### System behavior
- **Titrant factor calculation**:
  - *Primary Standard Mode*:
    $$f = \frac{W \times (P / 100)}{(V - B) \times E}$$
    where $W$ is standard weight in mg, $P$ is purity %, $V$ is titrant volume in mL, $B$ is blank volume in mL, and $E$ is milliequivalent weight.
  - *Against Volumetric Solution Mode*:
    $$f = \frac{V_{\text{ref}} \times f_{\text{ref}} \times N_{\text{ref}}}{(V - B) \times N}$$
- **Pass/Fail criteria**: Every replicate factor must fall within the configured `[min, max]` range ("Replicate {N}: factor {x.xxxx} outside {min}-{max}."), and replicate RSD ($n \ge 2$) must not exceed maximum RSD ("RSD {x.xx}% exceeds {y.yy}%.").
- **Stock deduction**: Consumed primary standard weights are deducted from the standard lot in the same save, whether the standardization passes or fails.
- **Factor validity**:
  - A **Passed** standardization updates the current factor. Validity is set as `ValidUntil = StandardizationTime + ValidityDays`. If validity days is 0, the titrant status is **Restandardize Before Each Use**.
  - A **Failed** standardization is saved and permanently retained in the history table, but it never becomes the current factor. Any prior valid factor remains in effect until expired.

#### Validation
- "Only titrant preparations can be standardized."
- "This preparation is expired or discarded and cannot be standardized."
- "This titrant is not configured for standardization."
- "Exactly {N} replicate(s) are required."
- "Replicate {N}: titrant volume must be greater than zero."
- "Titrant volume must be greater than the blank."
- "Replicate {N}: a blank is required."
- "Replicate {N}: this titrant does not use a blank."
- "Replicate {N}: a standard lot and weight are required."
- "This titrant has no primary standard configured."
- "Replicate {N}: standard lot (batch {X}) is not a primary standard."
- "Replicate {N}: standard lot (batch {X}) has no purity - enter it in Materials Stock before standardizing."
- "Replicate {N}: a reference preparation and volume are required."
- "Replicate {N}: reference preparation is expired or discarded."
- "Replicate {N}: reference preparation's standardization is expired, failed or missing."
- "Temperature must be between -20 and 60 C."

#### Status
- Standardization record outcome: **Passed** or **Failed**.
- Titrant factor state: **Valid**, **Due for Restandardization**, **Restandardize Before Each Use**, or **Not Standardized**.

---

### 28.3 Working Standard Qualification
<!-- WF-WS-001 -->
#### Purpose
Qualify, review, approve, and maintain in-house secondary working standard lots against primary reference standards using chromatographic assay (HPLC), enforce dried-basis potency determinations, and record three-person segregation of duties.

#### Who can use it
- **Draft creation, editing, and document upload**: Requires the *Qualify working standards* permission (`WorkingStandards.Qualify`) (by default: Analyst, Section Head, System Administrator).
- **HPLC run assignment and replicate submission**: Requires `Hplc.Operate` and `WorkingStandards.Qualify` (by default: Analyst, Section Head, System Administrator).
- **Secondary technical review**: Requires the *Review samples* permission (`Samples.Review`) (by default: Reviewer, Section Head, System Administrator).
- **Final approval and lot creation**: Requires the *Approve samples* permission (`Samples.Approve`) (by default: Section Head, System Administrator).

#### Before you start
- An active reference standard master entry must exist in Reagents & Reference Standards (`/laboratory-configuration/material-master`).
- For initial qualification: Either a received raw material sample with an approved assay result or a manual source (material name and batch number), quantity in grams, and storage location.
- An open HPLC run whose system suitability test (SST) has **Passed** on a **Primary Reference Standard** (a run that used a working standard for SST cannot qualify working standards).
- The moisture content (%) of the working standard material must be known and entered before run assignment.

#### What you see
- Working Standards page (`/working-standards`):
  - KPI summary tiles: **Active Lots**, **Valid**, **Due Soon**, **Expired / Depleted**, **Pending Quals**.
  - Tabs: **Lots (N)** and **Qualifications (N)**.
  - Action buttons: **Refresh**, **New qualification**, and (per lot) **Requalify**.
- Detail Drawer:
  - Sections: **Source Information**, **Documents** (**Source Report** and **Moisture Report**), **Analytical Replicates & Potency**, and **WORKFLOW & SIGN-OFF**.
  - Sign-off section: Displays **Prepared by:**, **Reviewed by:**, **Approved by:**, and **Rejected by:**.
  - Review actions (visible to `Samples.Review` holders when status is **Assayed**): **Review**, **Return to Draft**, **Reject**.
  - Approval actions (visible to `Samples.Approve` holders when status is **Reviewed**): **Approve**, **Reject**.

[SCREENSHOT SS-284]

#### How to use it
##### 1. Initiate Qualification:
1. Navigate to **Laboratories** > **Physicochemical Laboratory** > **Working Standards**.
2. Click **New qualification** (or click **Requalify** on an existing working standard lot).
3. In the dialog, select the source (**Manual** with Name and Batch, or **Received sample** with approved assay result), **Master Entry**, **Quantity (g)**, **Location**, and **Moisture %** (0 to $<100\%$). Click **Save**.
4. The system creates the record with code format `WSQ-nn/MM/yyyy` in status **Draft**.
5. In the drawer, upload the mandatory **Source Report** (PDF, PNG, or JPEG) and optional **Moisture Report**.

> **Note for reviewers:** The server supports draft edits before run assignment, but no moisture edit control is exposed in the drawer once created; verify whether moisture corrections require recreating the draft. (GAP-619)

##### 2. Assign to HPLC Run and Enter Replicates:
1. Open the open HPLC run where system suitability passed on a primary reference standard.
2. In the **Samples** tab, click **Assign Working Standard...** and select the qualification record.
3. Click the assigned qualification sample to open the qualification entry page (`/hplc-workspace/:instrumentId/run/:runId/qualification/:runSampleId`).
4. Enter exactly 6 replicates (sample weight in mg and peak area response for the qualified analyte).
5. Click **Save Replicates** to view the live calculation preview (mean assay %, RSD %, dried-basis potency %, and pass/fail indicator).
6. Upload the chromatographic sample integration report.
7. Click **Send for Review**, review the dialog, and sign with your password ("I confirm that chromatographic replicate data and evidence for working standard {code} ({material}) are complete, accurate, and ready for review.").
8. The qualification status transitions to **Assayed**, and the submitter is recorded as `Prepared by`.

##### 3. Technical Review:
1. A second user holding `Samples.Review` (who is not the submitter) opens the qualification drawer on the Working Standards page.
2. Under **Review Actions**, click **Review**.
3. In the dialog, confirm the statement: "I confirm that I have reviewed this working standard qualification record and verified the analytical results." Enter your password and click **Sign & Review**.
4. Status moves to **Reviewed**.
5. *Alternative Review Actions*:
   - Click **Return to Draft**: Enter a mandatory reason and click **Return to Draft** (no password). Clears the analytical results and submitter; replicates become editable again in the run.
   - Click **Reject**: Enter a mandatory reason and sign with your password ("I am rejecting this working standard qualification. A detailed justification is required."). Status moves to **Rejected**.

##### 4. Final Approval:
1. A third user holding `Samples.Approve` (who is neither the submitter nor the reviewer) opens the drawer.
2. Under **Approval Actions**, click **Approve**.
3. In the dialog, confirm the statement: "I approve this working standard qualification and release the standard for analytical testing use." Enter your password and click **Sign & Approve**.
4. Status moves to **Approved**.
5. *Initial qualification*: The system creates a new working standard lot in Materials Stock with code `WS-nn/MM/yyyy`, manufacturer "In-house", unit Gram, purity equal to dried-basis potency, moisture content, and expiration date set to exactly 12 months from the approval date.
6. *Requalification*: The system updates the existing working standard lot with the new potency, moisture, and extended 12-month expiration date.

#### System behavior
- **Fixed validation rules**: Exactly 6 replicates are required, RSD must not exceed 2.0%, and validity is fixed at 12 months.
- **Potency calculation**:
  $$\text{Potency (dried basis)} = \frac{\text{Mean Assay } \% \times 100}{100 - MC}$$
  Potency is stored rounded half away from zero to 3 decimals. Replicate RSD is evaluated after rounding to 2 decimals ($\le 2.0\%$).
- **Segregation of duties**: Submitter, reviewer, and approver must be three distinct individuals:
  - "The analyst who submitted the qualification cannot review it."
  - "The analyst who submitted the qualification cannot approve it."
  - "The reviewer cannot also approve the qualification."
  - "The reviewer cannot also reject the qualification at approval."
- **Stock deduction**: When an approved working standard lot is used as an SST standard in routine HPLC testing, the consumed standard weight is deducted from inventory stock upon SST confirmation.

> **Note for reviewers:** In-house working standard qualification strictly requires three distinct individuals across submission, review, and approval. Fixed rules mandate 6 replicates, RSD $\le 2.0\%$, and 12-month validity. (GAP-619, GAP-608)

> **Note for reviewers:** Inventory stock of a working standard decreases when it is used for system suitability testing. (GAP-617)

#### Validation
- "Choose the reference standard master entry."
- "Master entry \"{name}\" is not a reference standard."
- "That sample has no approved assay result."
- "Enter the quantity in grams."
- "Enter the storage location."
- "Lot {lot} already has an open qualification ({code})."
- "Moisture must be at least 0 and below 100 %."
- "Enter the moisture content first."
- "Attach the raw material's first test report first."
- "A working standard can only be qualified against a primary reference standard - this run's system suitability used a working standard."
- "Exactly 6 replicates are required ({N} entered)."
- "RSD {X} % is above 2.0 %."
- "Upload the sample report before submitting."
- "Only an assayed qualification can be reviewed."
- "A failed qualification can only be rejected."
- "The run that carried this assay was abandoned - reject the qualification."
- "Only a reviewed qualification can be approved."
- "Another approval took that working standard code at the same moment - sign again."

#### Status
- Qualification lifecycle: **Draft** (0) $\rightarrow$ **Assayed** (1) $\rightarrow$ **Reviewed** (2) $\rightarrow$ **Approved** (3) or **Rejected** (4).
- Return to Draft resets status from **Assayed** to **Draft**.
- Approved Working Standard lot status: **Valid**, **DueSoon** (expires within 30 days), **Expired**, or **Depleted** (quantity zero).

---

### 28.4 Solutions Master Recipes
<!-- MOD-SOLNM-001 -->
#### Purpose
Define and manage master formulation recipes for mobile phases, diluents, and volumetric titrants, including stoichiometric component quantities, shelf-life limits, pH targets and tolerances, and titrant standardization criteria.

#### Who can use it
Requires the *Manage master data* permission (by default: Section Head, System Administrator) to view and edit recipes (`/laboratory-configuration/solution-master`).

#### What you see
- Solutions Master register displaying recipe code, name, solution type, shelf life, and active status.
- Recipe editing dialog with sections: **General**, **pH**, **Titrant**, and **Components**.
- **Reason for Modification** dialog: Mandatory text field when saving changes.

#### How to use it
1. Navigate to **Laboratories** > **Physicochemical Configuration** > **Solutions**.
2. Click **Add Recipe** or select an existing recipe and click **Edit**.
3. Configure the recipe parameters:
   - Solution type: **Mobile Phase**, **Diluent**, or **Titrant**.
   - Shelf life value and unit (**Hours** or **Days**).
   - Component stoichiometry: select material master entries and enter target quantities.
   - For titrants: nominal strength and unit (**Normal**, **Molar**, or **MgWaterPerMl** for Karl Fischer), standardization mode, factor range `[min, max]`, maximum RSD, and validity days.
4. Click **Save Changes**.
5. In the **Reason for Modification** dialog, enter a detailed explanation and click **Confirm**.

#### System behavior
- Edits are saved in place and write a detailed event to the audit trail recording the before and after configuration values alongside the user's documented reason. No electronic signature password is required.

---

### 28.5 Reagents and Reference Standards Master Catalog
<!-- MOD-REAGENT-001 -->
#### Purpose
Maintain the master catalog of chemical reagents, volumetric indicators, primary standards, and reference standards across laboratory sections.

#### Who can use it
Requires the *Manage master data* permission (by default: Section Head, System Administrator) to view and manage entries (`/laboratory-configuration/material-master`).

#### What you see
- Reagents & Reference Standards register displaying code, name, category, and active status.
- Categories: **Chemical**, **Indicator**, **Reference Standard**, and **Primary Standard**.
- Indicator-specific fields: transition range (**From pH** and **To pH**) and color change descriptions.

#### How to use it
1. Navigate to **Laboratories** > **Physicochemical Configuration** > **Reagents & Standards**.
2. Click **Add Entry** or select an entry to edit.
3. Enter a unique **Code** and **Name**.
4. Select the **Category**. For indicators, configure the pH transition range.
5. Click **Save**.

#### System behavior
- Codes must be unique across the laboratory section ("Material master entry code \"{code}\" already exists.").
- An entry that has linked inventory stock lots cannot have its category altered ("This entry has linked stock lots; its category can't change.").
- Master definitions serve as the catalog baseline for receiving physical stock in Materials Stock.
