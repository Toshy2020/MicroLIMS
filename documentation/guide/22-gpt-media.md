## 22. Growth Promotion Testing and Media

MicroLIMS manages the lifecycle of microbiological culture media, from configuring media product master data, incubation parameters, and challenge specifications, through lot preparation, multi-challenge evaluation (Growth Promotion, Indication, Inhibition, Enrichment), dual-person release authorization, and active storage tracking.

MicroLIMS does not have a separate sterility test step or record, although the term appears in some user interface text.

---

### 22.1 Media Configurations
<!-- WF-MEDIACFG-001 -->
#### Purpose
Configure dehydrated media products, set their unique lot number prefix codes, define authorized incubation temperature and duration ranges, and specify evaluation challenge criteria (recovery percentage ranges, challenge organisms, and colony descriptions).

#### Who can use it
Requires the *Manage laboratory master data* permission (`MasterData.Manage`, by default: Section Head, System Administrator). Changing a media product code is restricted exclusively to the `SectionHead` role type.

#### Before you start
- Ensure challenge organisms exist in the canonical Organisms master list (§32).
- Prepare the media product code (2 to 10 alphanumeric characters, dots, or hyphens).

> **Note for reviewers:** Only the Section Head role type can change a media product code (signed); a System Administrator cannot. Other media product changes are unsigned. (GAP-013, GAP-406)

> **Note for reviewers:** The evaluation type Enrichment Characteristics is supported by the server and audit trail, but cannot be configured from the user interface. (GAP-407)

> **Note for reviewers:** The expected colony description for Indication challenges is enforced only in the dialog and is not validated on the server. (GAP-405)

#### What you see
- On the navigation sidebar, under **LABORATORIES — Microbiology Configuration**, click **Media Configurations** (`/laboratory-configuration/media-configurations`).
- Left panel: **Configured Media ({n})** list with search box, reset control, and button **Add Media Product**.
- Product workspace (when a product is selected):
  - **Overview** tab: summary tiles for **Media code** (with button **Change code** for Section Heads), **Lot number format** (`{code}/01/{yy}, {code}/02/{yy} ...`), **Configurations** count, and **Stock batches** count.
  - **Incubation Conditions ({n})** tab: table of defined time and temperature pairs showing **Incubation**, **Temperature**, **Used by**, and **Actions**. Unused conditions show a **Not used** chip; conditions in use show disabled edit/delete icons with tooltip "In use - add a new condition instead".
  - **Configurations ({n})** tab: shows the evaluation configuration and expandable challenge organism cards, with button **Add Configuration** (visible when none exists).

[SCREENSHOT SS-221]

#### How to use it
To add a media product:
1. In the left panel, click **Add Media Product**.
2. In the dialog, enter the **Product Name** and **Product Code** (2 to 10 letters, digits, dot, or hyphen).
3. Click **Save**. Upon creation, the system displays `Media product "{name}" created successfully.` and navigates to the **Incubation Conditions** tab.

To define an incubation condition:
1. In the product workspace, select the **Incubation Conditions** tab and click **Add Condition**.
2. Enter **Incubation Min (h)**, **Incubation Max (h)**, **Temp Min (°C)**, and **Temp Max (°C)**.
3. Click **Save**. The system displays `Incubation condition added.`

To configure evaluation parameters and challenge organisms:
1. In the product workspace, select the **Configurations** tab and click **Add Configuration**.
2. Select the **Evaluation Type** (**Growth Promotion** or **Indication / Inhibition**).
3. Select an **Incubation Condition** from the dropdown.
4. If configuring **Growth Promotion**, enter **Recovery Min (%)** and **Recovery Max (%)**.
5. Under **Challenge Organisms (Optional)**:
   - Select an organism from the canonical list.
   - For Indication/Inhibition, select the **Challenge Role** (**Inhibition** or **Indication**).
   - If the role is **Indication**, enter the **Expected Colony Description \***.
   - Enter the **Initial Inoculum (CFU)** if applicable.
   - Click **Add Organism** to stage the challenge.
6. Click **Save Configuration**.

To change a media product code (Section Head only):
1. In the product workspace, open the **Overview** tab.
2. In the **Media code** tile, click **Change code**.
3. In the dialog, enter the **New Code** and a mandatory **Reason**.
4. Click **Continue to sign**.
5. In the **Electronic Signature** dialog, review the statement: `I am changing the code of media product "{name}" from {old} to {new}. Lots prepared from now on will be numbered {new}/01/{yy} onwards. Reason: {reason}`.
6. Enter your password and click **Sign**.

#### System behavior
- Media product codes prefix all prepared lot numbers (`{code}/{seq:D2}/{yy}`).
- Changing a media product code records an electronic signature with meaning `MasterDataChanged`, logs audit action `MediaProductCodeChanged`, and applies to all newly prepared lots. Existing prepared or received lots retain their historical code.
- Each media product can have at most one evaluation configuration.
- Incubation conditions in use by an evaluation configuration or Test Master step medium cannot be edited or deleted.
- Deleting a media product is prevented if it has configurations, received stock batches, or incubation conditions in use.

#### Validation
The system enforces exact rules on configuration entries:
- "Both product name and code are required."
- "Product name is required."
- "Enter the incubation hours and temperature range."
- "Incubation min hours must be greater than zero."
- "Incubation max hours cannot be less than min hours."
- "Temperature min must be below temperature max."
- `'{product}' already has an incubation condition of {min}–{max} h at {tmin}–{tmax} °C.`
- `This incubation condition is used by {n} evaluation configuration(s) and {m} Test Master step medium/media, so it can't be changed or deleted. Add a new condition instead.`
- `'{product}' already has an evaluation configuration. Edit it instead.`
- "Choose an incubation condition."
- "Recovery % min cannot exceed max."
- "This organism and role combination is already staged for this configuration."
- "Duplicate challenge organism and role combination in request."

#### Status
- Master records have no operational lifecycle status; they remain active until deleted.
- Unused incubation conditions display the chip **Not used**.

#### Important rules
- A media product cannot have more than one evaluation configuration.
- Changing a product code requires password re-entry and is restricted strictly to the Section Head role type.
- Free-text product rename and creation do not require a password or electronic signature.

#### Errors
- `Media product with ID {id} not found.`
- "Failed to create media product."
- "Failed to rename media product."
- "Failed to save the incubation condition."
- "Could not delete this incubation condition."
- "Could not delete this media product."
- "Failed to load media configuration data."

#### What happens next
- The configured media product becomes available in Materials Stock for linking to received dehydrated media batches (§29).
- Analysts prepare media lots using the configured product code and parameters (§22.2).

---

### 22.2 Media Preparation
<!-- WF-MEDIA-001 -->
#### Purpose
Prepare a laboratory media lot from dehydrated media stock in inventory, record autoclave sterilisation parameters, consume stock weight, and automatically assign the lot's evaluation challenge suite.

#### Who can use it
Requires the *Prepare media lots* permission (`Media.Prepare`, by default: Section Head, Reviewer, Analyst, System Administrator). Populating stock and equipment pickers requires *Manage materials stock* (`Materials.Manage`) and *Manage equipment inventory* (`Equipment.Manage`).

#### Before you start
- A dehydrated media stock batch must exist in Materials Stock with status **InStock**, linked to a configured media product, with a verified current Certificate of Analysis (COA), unexpired, and having sufficient quantity.
- An Autoclave must be configured in master-data equipment with at least one active autoclave program.
- Exactly one evaluation configuration must exist for the target media product.

> **Note for reviewers:** Reviewers may be unable to load stock and equipment lists in the preparation dialog because the pickers require inventory permissions not granted to Reviewers by default. (GAP-514)

> **Note for reviewers:** Media preparation values such as pH, weight, volume, and expiry date are typed by the user and are not range-checked by the server. (GAP-509, GAP-515)

#### What you see
- On the navigation sidebar, under **LABORATORIES — Microbiology Laboratory**, click **Media Preparation & Evaluation** (`/laboratory-configuration/media`).
- Header: **Media Preparation & Evaluation**, with button **Prepare New Media Lot**.
- Dialog: **Prepare New Media Lot**:
  - **Dehydrated Media Stock (Inventory) \*** dropdown (lists in-stock dehydrated media linked to products).
  - **Total Weight (g) \*** and **Total Volume \***.
  - **Autoclave \*** and **Autoclave Program / Load \*** dropdowns.
  - Read-only sterilisation fields populated from the chosen program: **Load Type (Read-Only)**, **Temperature (°C) (Read-Only)**, and **Cycle Time (min) (Read-Only)**.
  - **Cycle Number \***, **pH (at 25°C) \***, and **Expiry Date \***.
  - Buttons: **Cancel** and **Save Prepared Lot**.

[SCREENSHOT SS-222]

#### How to use it
1. In **Media Preparation & Evaluation**, click **Prepare New Media Lot**.
2. In the **Dehydrated Media Stock (Inventory) \*** dropdown, select the source dehydrated media batch.
3. Enter the **Total Weight (g) \*** consumed and the **Total Volume \*** prepared.
4. Select the **Autoclave \*** and the specific **Autoclave Program / Load \***. Confirm the read-only sterilisation parameters.
5. Enter the steriliser **Cycle Number \***, measured **pH (at 25°C) \***, and target **Expiry Date \***.
6. Click **Save Prepared Lot**.
7. The system prepares the lot, deducts the consumed weight from stock, generates the lot number, creates the evaluation record, and displays: `Media lot {lot} successfully prepared.`

#### System behavior
- Generates a unique lot identifier formatted as `{Product.Code}/{seq:D2}/{yy}`.
- Deducts the specified total weight from the dehydrated media inventory lot in the same transaction.
- Copies manufacturer lot number and manufacturer name from the inventory material to the media lot record.
- Automatically creates an evaluation record with status **Assigned** and instantiates one challenge per organism configured in the product's evaluation configuration.
- Assigns the lot initial status **Prepared**, approval status **PendingReview**, and client lifecycle label **Pending Evaluation**.
- Records the preparing user identity. The preparing user is permanently barred by segregation of duties from later releasing or rejecting the lot.

#### Validation
The system validates required entries and prerequisites:
- "Please select a Dehydrated Media Stock item from inventory."
- "The selected dehydrated media is not linked to a media product. Edit it in Inventory > Materials Stock before preparing."
- "Please enter the total weight."
- "Please enter the total volume."
- "Please select an Autoclave."
- "Please select an Autoclave Program / Load."
- "Please select an Autoclave Program with valid Load Type."
- "Autoclave temperature is required."
- "Autoclave cycle time is required."
- "Please specify the cycle number."
- "Please specify the pH."
- "Please specify the Expiry Date."
- "Selected equipment is not a valid autoclave."
- "Material {name} (batch {batch}) is expired and cannot be used."
- "A current Certificate of Analysis (COA) is required before {name} (batch {batch}) can be used. Please upload a valid COA in the Inventory module first."
- "Insufficient stock: {name} (batch {batch}) has {remaining} {unit} remaining, {requested} requested."
- "Batch {batch} of {material} isn't linked to a configured media product - edit it in Inventory > Materials Stock and choose the product before preparing."
- "No Media Configuration exists yet for \"{product}\" - configure it in Laboratory Configuration before preparing a lot."
- "Lot number {lot} was taken by another preparation at the same moment. Nothing was saved - submit the preparation again."

#### Status
- Lot Status starts as **Prepared** (0), approval status **PendingReview**.
- Displayed lifecycle badge: **Pending Evaluation**.

#### Important rules
- A prepared lot cannot be used in testing until its evaluation is completed and authorized for release by an independent Section Head.
- Stock deduction occurs immediately upon preparation.
- If two users prepare lots with the same product code simultaneously, the system retries sequence allocation or prompts for re-submission.

#### Errors
- "Could not prepare media lot. Please verify all inputs."

#### What happens next
- Analysts execute evaluation challenges (inoculation, incubation, and reading) in the lot workspace (§22.3).

---

### 22.3 Media Evaluation
<!-- WF-MEDIA-002 -->
#### Purpose
Perform testing challenges on prepared media lots—Growth Promotion Testing (recovery comparison against a reference lot), Indication, Inhibition, or Enrichment—by selecting organism challenge sources, logging incubation, and recording observation results.

#### Who can use it
Requires the *Execute test workflows* permission (`TestWorkflow.Execute`, by default: Section Head, Reviewer, Analyst, System Administrator) within assigned laboratory sections. Selecting challenge sources requires *Manage cryovials* (`Cryovials.Manage`) and *Manage materials stock* (`Materials.Manage`).

#### Before you start
- The media lot must have an auto-assigned evaluation.
- For each challenge, an approved, non-destroyed, unexpired reference cryovial batch or unexpired lyophilized microorganism disk of the matching organism must be available.
- For Growth Promotion challenges, an approved prior released lot of the same medium or manual reference details must be identified.
- An incubator must be available.

> **Note for reviewers:** A growth promotion challenge with no configured minimum recovery is always evaluated as Conform. (GAP-505)

> **Note for reviewers:** The disc for a media evaluation is deducted from Materials Stock immediately upon selection and is not returned if discarded. (GAP-511)

> **Note for reviewers:** Evaluation results can be re-entered after completion; the correction procedure is unconfirmed. (GAP-504)

#### What you see
- In **Media Preparation & Evaluation**, select a media lot from the register table.
- In the lot workspace, click the **Evaluations (N)** tab.
- Click **Open Evaluation** to launch the **Media Evaluation Workflow** dialog.
- Per challenge card:
  - Header: organism name, ATCC number, challenge role/type, and recovery criteria.
  - **REFERENCE LOT (FOR RECOVERY% COMPARISON)** (for Growth Promotion): dropdown of prior released lots or **Reference Lot (manual entry)**.
  - **STEP 1: SELECT WORKING CRYOVIAL OR LYOPHILIZED DISK**: select **Select Cryovial or Disk**, button **Assign**.
  - **STEP 2: RECORD INCUBATION**: select **Select Incubator**, button **Record Incubation**. Displays temperature, hours, and status chip (**Earliest reading: {date}** or **Ready to read**).
  - **STEP 3: RECORD OBSERVATION / RESULT**: result entry fields and button **Record Result**.

[SCREENSHOT SS-223]

#### How to use it
For each challenge organism:
1. Under **STEP 1: SELECT WORKING CRYOVIAL OR LYOPHILIZED DISK**, select an eligible cryovial batch or lyophilized disk and click **Assign**.
   - If a lyophilized disk is selected, 1 unit is immediately consumed from inventory.
2. Under **STEP 2: RECORD INCUBATION**, select an incubator and click **Record Incubation**.
   - Incubation parameters are populated automatically from the media configuration.
3. Wait until the incubation time window elapses and the chip changes to **Ready to read**.
4. Under **STEP 3: RECORD OBSERVATION / RESULT**, enter results according to the challenge type:
   - **Growth Promotion**: Select a prior released reference lot (or enter reference lot text), enter **Old Media Count** and **New Media Count**. The system displays a live recovery percentage preview.
   - **Inhibition**: Under **Growth Observed?**, select **Yes** or **No**.
   - **Indication**: Enter **Observed Description** and select **Judgment** (**Conform** or **NonConform**).
   - **Enrichment**: Under **Turbid or Clear?**, select **Turbid** or **Clear**.
5. Click **Record Result**.
6. Repeat for all staged challenges. When all challenges have recorded results, the evaluation status advances to **Completed**.

#### System behavior
- Growth promotion recovery calculation: `Recovery % = round((New Media Count / Old Media Count) * 100, 1)`. The challenge is marked **Conform** if recovery falls within the configured range.
- Inhibition challenges evaluate to **Conform** when no growth is observed.
- Enrichment challenges evaluate to **Conform** when turbid.
- Indication challenges evaluate according to the analyst's manual judgment.
- An evaluation is **Completed** with outcome **Conform** only when every challenge conforms.
- If any challenge evaluates to **NonConform**, the evaluation outcome is **NonConform**, and the media lot status immediately transitions to **QuarantineFailed** (displayed as **Rejected**).
- A conforming evaluation transitions the lot's screen lifecycle label to **Awaiting Approval**.

#### Validation
The system validates timing, source matching, and result inputs:
- "Organism source selected."
- "Incubation recorded."
- "Result recorded successfully."
- "No approved, unexpired cryovial batches or lyophilized disks available for {organism}."
- "Media Configuration for lot \"{lot}\" is missing an incubation condition."
- "Incubation must be recorded before a result can be entered."
- "Incubation is still in progress - earliest reading time is {yyyy-MM-dd HH:mm} UTC."
- "Old and new media counts are required."
- "Old media count cannot be zero - recovery% cannot be calculated."
- "A reference lot is required - either a linked prior lot or a free-text description, not both or neither."
- "Reference media lot {id} belongs to another laboratory section."
- "Growth observed is required."
- "Observed description and a Conform/NonConform judgment are required."
- "Turbid/clear is required."
- "Cryovial batch {code} belongs to another laboratory section and cannot challenge this media lot."
- "Cryovial batch {code} is {organism}, not {challenge organism}."
- "Cryovial batch {code} is not approved for use."
- "Cryovial batch {code} is expired and cannot be used."

#### Status
- Evaluation lifecycle: **Assigned** (0) -> **InProgress** (1) when incubation is logged -> **Completed** (2) when all challenges are read.
- Evaluation outcome: **Conform** (0) or **NonConform** (1).
- Lot status: remains **Prepared** on Conform (screen badge **Awaiting Approval**); transitions to **QuarantineFailed** (3) on NonConform (screen badge **Rejected**).

#### Important rules
- Results cannot be recorded before the minimum incubation duration has elapsed.
- Cryovial selection does not decrement remaining vial counts; lyophilized disk selection consumes 1 unit immediately.
- Any non-conforming challenge fails the entire media lot immediately.

#### Errors
- "Could not select organism source."
- "Could not record incubation."
- "Could not record result."

#### What happens next
- If the evaluation outcome is **Conform**, an authorized Section Head reviews the evaluation and executes the release decision (§22.4).
- If the evaluation outcome is **NonConform**, the lot is quarantined and cannot be used in laboratory testing.

---

### 22.4 Media Lot Release and Rejection
<!-- WF-MEDIA-003 -->
#### Purpose
Authorize a conforming media lot for routine laboratory testing through a signed electronic release decision, or reject a non-conforming lot.

#### Who can use it
Requires the *Release media lots* permission (`Media.Release`, by default: Section Head, System Administrator).

#### Before you start
- The lot must be in lifecycle state **Awaiting Approval** (evaluation **Completed** and **Conform**, approval **PendingReview**).
- The deciding user must satisfy segregation of duties: they cannot be the user who prepared the lot, nor any analyst who read an evaluation challenge on that lot.

> **Note for reviewers:** A Conform evaluation does not release a lot; a Section Head (or other user holding the *Release media lots* permission) must sign, and cannot be the preparer or evaluator. (BR-MEDIA-019, GAP-513)

> **Note for reviewers:** Release does not check the lot expiry date. (GAP-506)

#### What you see
- In **Media Preparation & Evaluation**, select a media lot whose status is **Awaiting Approval**.
- In the workspace header, action buttons appear: **Release Lot** and **Reject Lot**.
- Dialog: **Electronic Signature** prompting for user password, displaying the legal signing statement.

#### How to use it
To release a media lot:
1. In the selected lot workspace, click **Release Lot**.
2. Review the displayed electronic signature statement: `I am releasing media lot {lot} for use in routine testing.`
3. Enter your password and click **Sign**.
4. The system validates credentials and segregation of duties, updates the lot to **Active**, generates an archived immutable PDF record, and displays: `Media lot {lot} released for use.`

To reject a media lot:
1. In the selected lot workspace, click **Reject Lot**.
2. Review the signature statement: `I am rejecting media lot {lot}.`
3. Enter your password and click **Sign**.
4. The system transitions the lot to **QuarantineFailed** and displays: `Media lot {lot} rejected.`

#### System behavior
- Checks validation rules in strict order:
  1. Verifies the lot has not already been decided.
  2. Verifies the evaluation record exists.
  3. If releasing, verifies evaluation is **Completed** and outcome is **Conform**.
  4. Verifies segregation of duties.
  5. Verifies password.
- Records an electronic signature entry with meaning **Approved** or **Rejected**, logs the decision event in the review audit trail, and archives an immutable PDF copy of the lot summary.
- The release decision is permanent and final; released or rejected media lots cannot be reverted.

#### Validation
The system returns exact refusal messages when release criteria are unmet:
- "Media lot {lot} has already been decided ({status})."
- "Media lot {lot} has no evaluation record."
- "Media lot {lot}'s evaluation is not complete yet."
- "Media lot {lot}'s evaluation did not conform - it cannot be released."
- "You cannot approve a media lot you prepared or evaluated."

#### Status
- On release: Lot status changes to **Active** (1), approval status **Approved**, and screen badge **Released**.
- On rejection: Lot status changes to **QuarantineFailed** (3), approval status **Rejected**, and screen badge **Rejected**.

#### Important rules
- The preparer and all challenge readers are strictly barred from signing the release decision.
- Rejection is permitted regardless of the evaluation state.
- Once released, the lot becomes visible in routine testing workspace pickers.

#### Errors
- "Release decision failed."
- "Signature attempt (...) refused: {n} failed attempts in the last {m} minutes."

#### What happens next
- The released media lot is placed into active inventory and can be assigned to routine test incubations (§14, §15) or logged into storage locations (§22.5).

---

### 22.5 Media Storage and Inventory Management
<!-- WF-MEDIA-004 -->
#### Purpose
Record physical storage forms and locations for released media lots, track inventory movements between storage equipment, and decommission depleted or compromised lots by marking them out of stock.

#### Who can use it
Requires the *Prepare media lots* permission (`Media.Prepare`, by default: Section Head, Reviewer, Analyst, System Administrator).

#### Before you start
- Storage records can only be created for media lots that are **Active** (released) and in stock.
- Target storage equipment must be in-service cold storage inventory equipment in the Microbiology section.

> **Note for reviewers:** The lot status never displays Expired; users must check the expiry date directly. Out of Stock cannot be undone and is unsigned. (GAP-502, GAP-512)

#### What you see
- In the selected lot workspace, click the **Storage** tab (available only for released lots).
- Storage panel:
  - Button **Add Storage** (visible when the lot is active and in stock).
  - List of active storage entries showing storage form, quantity, location, and action buttons.
- Header action: **Mark Out of Stock** button (visible when the lot lifecycle is **Released**).
- Dialog: **Add Storage**:
  - **Stored as \*** (options: Plates, Agar bottles, Broth bottles, Tubes).
  - **Quantity \***.
  - Destination: either **Storage equipment \*** (dropdown of in-service cold storage) or **Storage area \*** (free text).
- Dialog: **Move Storage**:
  - **Quantity to move \***, **New storage location \***, and **Reason for move \*** (minimum 5 characters).
- Dialog: **Mark Media Lot Out of Stock**:
  - Warning: "Warning: This media lot will no longer be available for laboratory testing."
  - Text area: **Reason / Comment (Optional)**.
  - Buttons: **Cancel** and **Confirm Out of Stock**.

[SCREENSHOT SS-224]

#### How to use it
To add a storage entry:
1. In the lot workspace, open the **Storage** tab and click **Add Storage**.
2. Select the storage form (**Plates**, **Agar bottles**, **Broth bottles**, or **Tubes**).
3. Enter the **Quantity \***.
4. Select the target **Storage equipment \*** from the dropdown or enter a **Storage area \***.
5. Click **Add**.

To move media storage:
1. Locate the storage entry in the **Storage** tab and click **Move**.
2. Enter the **Quantity to move \***.
3. Select the new destination location.
4. Enter the mandatory **Reason for move \*** (minimum 5 characters).
5. Click **Move**.

To mark a media lot out of stock:
1. In the workspace header, click **Mark Out of Stock**.
2. In the confirmation dialog, review the warning and optionally enter a reason.
3. Click **Confirm Out of Stock**.
4. The system sets the lot status to **OutOfStock**, displaying the screen badge **Out of Stock**.

#### System behavior
- Adding storage to an existing identical form and location merges the quantity.
- Moving storage validates that available quantities are not exceeded, updates quantities, and records a review-trail event with user and reason.
- Marking out of stock sets status to **OutOfStock** (5), immediately withdrawing the lot from routine testing pickers. This action cannot be reversed.
- Storage operations and marking out of stock do not require an electronic signature or password re-entry.

#### Validation
The system validates storage parameters:
- "Request payload is required."
- "Quantity must be greater than zero."
- "Exactly one of EquipmentId or LocationText must be provided."
- "Media lot {lot} is not currently released for use and active."
- "Equipment {code} is not currently in service."
- "A reason is required to move media storage."
- "Reason must be at least 5 characters."
- "Cannot move {n} units; only {m} available in entry {id}."
- "Destination storage location must be different from the source storage location."
- "Media storage entry {id} not found for media lot {lot}."
- "Media lot {lot} is not currently released for use and cannot be marked Out of Stock."

#### Status
- Stored media lot status: remains **Active** (1, **Released**) while in stock.
- Decommissioned lot status: **OutOfStock** (5), screen badge **Out of Stock**.

#### Important rules
- Storage entries can only be created for released, active media lots.
- Marking a lot out of stock is permanent and cannot be undone.

#### Errors
- "Failed to mark media lot out of stock."

#### What happens next
- The media lot remains tracked in inventory records until depleted or marked out of stock.

---

### 22.6 Media Lot Summary Report
#### Purpose
Review and export the comprehensive historical analytical summary of a prepared media lot, including preparation parameters, sterilisation cycle data, evaluation challenge results, and electronic release signatures.

#### Who can use it
Requires the *Prepare media lots* permission (`Media.Prepare`, by default: Section Head, Reviewer, Analyst, System Administrator) to load report data.

#### What you see
- Navigated via direct URL (`/media/:id/report`) or clicking report links in the media register.
- Printable summary report displaying:
  - Header: Lot number, media product name, product code, preparation date, and preparer name.
  - Material details: Manufacturer name, manufacturer lot number, consumed weight, total volume, pH, and expiry date.
  - Sterilisation records: Autoclave equipment code, program name, cycle number, temperature, and duration.
  - Evaluation results table: Challenge organisms, ATCC numbers, reference lots, observed counts/appearances, recovery percentages, and challenge outcomes.
  - Authorization section: Electronic signature timestamp, signer name, role, and release decision.

#### How to use it
1. In the **Media Preparation & Evaluation** register, locate the media lot.
2. Click the report action icon to open the summary report page.
3. Use your browser's print function to generate a physical copy or export to PDF/Word.
