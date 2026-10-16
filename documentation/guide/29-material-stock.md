## 29. Material Stock

MicroLIMS provides centralized inventory tracking for laboratory reagents, culture media, reference standards, microbiological strains, chemical supplies, and consumables within the **Materials Stock** module (`/inventory/materials`). Inventory records are scoped to laboratory sections (**Microbiology** or **Physicochemical**), support automated stock deductions across testing and preparation workflows, enforce mandatory Certificate of Analysis (COA) verification gates, and maintain controlled document lifecycles.

> **Important notice:** MicroLIMS does not have a separate receipt quarantine or release inspection step; newly received material lots are registered directly into inventory stock in status InStock.

---

### 29.1 Materials Stock Inventory Overview
#### Purpose
View current stock levels, track expiration dates, locate storage assignments, and filter laboratory materials across chemical and biological categories.

#### Who can use it
Requires the *Manage materials* permission (by default: Analyst, Section Head, System Administrator). Visibility is strictly scoped to the user's assigned laboratory section (`?lab=MICRO` or `?lab=FP`).

#### Before you start
- The user must hold active membership in the relevant laboratory section (Microbiology or Physicochemical).

#### What you see
- Materials Stock table displaying:
  - Columns: **Type**, **Material Name**, **Manufacturer**, **Batch/Lot No.**, **Received**, **Expiry**, **Code**, **Location**, **Qty Received**.
  - Status badges: **In Stock**, **Depleted** (remaining quantity zero or less), and **Expired** (expiration date earlier than the current laboratory date).
  - Informational chip: **Not linked to a media product** (for dehydrated media lacking a product link).
  - Row actions: **Details and documents**, **Edit material**.
  - Empty state (if no inventory exists): "No materials in stock — Receive the first material lot."
  - Action button: **Add to Stock**.

[SCREENSHOT SS-291]

#### How to use it
1. Navigate to **Laboratories** > **Microbiology Laboratory** > **Materials Stock** (or **Physicochemical Laboratory** > **Materials Stock**).
2. Filter the inventory register by material type or search by material name, batch/lot number, or catalog code.
3. Review available quantities remaining, storage locations, and upcoming expiration dates.

#### System behavior
- Stock status is evaluated dynamically and is not stored as a static database status:
  - **InStock**: Remaining quantity $> 0$ and expiration date on or after the current date (or no expiration date configured).
  - **Depleted**: Remaining quantity $\le 0$.
  - **Expired**: Expiration date earlier than the current laboratory date.

> **Note for reviewers:** Material lots registered without an expiration date are never treated as expired by the system. (GAP-508)

---

### 29.2 Receiving Material Lots into Stock
<!-- WF-MATERIAL-001 -->
#### Purpose
Register newly received physical lots of chemical reagents, reference standards, culture media, and biological materials into section inventory.

#### Who can use it
Requires the *Manage materials* permission (by default: Analyst, Section Head, System Administrator).

#### Before you start
- For chemical reagents, indicators, primary standards, and reference standards, the master entry must exist in the Reagents & Reference Standards catalog.
- For dehydrated media, the corresponding media product must be configured in Laboratory Configuration.
- In-house working standards cannot be received here; they are created exclusively through approved qualification workflows.

#### What you see
- Dialog title: **Add Material to Stock** (buttons: **Cancel**, **Add to Stock**).
- Entry fields:
  - **Material Type**: Dropdown of built-in types or custom text (saved under category `Other`).
  - Type-specific configuration pickers:
    - Dehydrated Media: Media product selector (helper: "Media not listed? A Section Head adds it in Laboratory Configuration > Media Configuration.").
    - Chemical, Indicator, Primary Standard, Reference Standard: Master entry selector (helper: "Required. Master entry sets the name, code, and base unit.").
    - Primary Standard: **Purity (%)** (helper: "Purity percentage (0 < p ≤ 100)").
    - Reference Standard: **Purity (%)** and optional **Moisture Content (%)** (helper: "Moisture percentage (0 ≤ m < 100, optional)").
    - Lyophilized Microorganism: Organism selector and **ATCC No.**
  - Common receiving fields:
    - **Laboratory Section**: Dropdown (visible if the user has multiple section assignments).
    - **Material Name \***: Catalog or commercial name.
    - **Manufacturer**: Supplier or manufacturer name.
    - **Code / Catalog No.**: Manufacturer catalog or reference code.
    - **Batch / Lot No. \***: Manufacturer batch or lot identifier.
    - **Storage Location \***: Designated shelf, cabinet, refrigerator, or freezer location.
    - **Quantity Received \***: Numerical received quantity (must be $> 0$).
    - **Unit**: Stock measurement unit (e.g., Gram, Kilogram, Milliliter, Liter, Unit).
    - **Min. Stock Level (optional)**: Reorder threshold.
    - **Receiving Date \***: Date of physical receipt.
    - **Expiry Date**: Expiration date indicated on the manufacturer label.

[SCREENSHOT SS-292]

#### How to use it
1. On the Materials Stock page, click **Add to Stock**.
2. Select the **Material Type** from the dropdown.
3. If receiving:
   - *Dehydrated Media*: Select the configured media product.
   - *Chemical, Indicator, or Standards*: Select the master entry. For primary standards, enter **Purity (%)**. For reference standards, enter **Purity (%)** and optional **Moisture Content (%)**.
   - *Lyophilized Microorganism*: Select the organism and enter the ATCC number.
4. Enter the **Material Name \***, **Manufacturer**, and **Code / Catalog No.**.
5. Enter the **Batch / Lot No. \*** and assigned **Storage Location \***.
6. Enter the **Quantity Received \***, select the **Unit**, and set the **Receiving Date \***.
7. Enter the manufacturer **Expiry Date** (if specified on the container).
8. Click **Add to Stock**.

#### System behavior
- Initializes `QuantityRemaining = QuantityReceived`.
- Evaluates stock status (**InStock**, or **Expired** if received past expiration).
- Records an entry in the system record-change log capturing the user, timestamp, and initial inventory quantities.
- No electronic signature password is required for material receiving.

#### Validation
- Client presence checks:
  - "Material name, batch/lot number, receiving date, storage location, and quantity received are required."
  - "Quantity received must be greater than zero."
  - "Laboratory section is required."
  - "Choose the configured media product for this dehydrated media."
  - "Choose the material master entry for this lot."
  - "Purity percentage is required for primary standards."
  - "Purity percentage is required for reference standards."
  - "Moisture content must be at least 0 and below 100."
- Server validations:
  - "Purity is required for primary standards."
  - "Purity is required for reference standards."
  - "Purity must be greater than 0 and less than or equal to 100."
  - "Purity is only allowed for reference and primary standards."
  - "Moisture content is only allowed for reference standards."
  - "Moisture content must be at least 0 and below 100."
  - "Choose the material master entry for this lot."
  - "That master entry belongs to another laboratory."
  - "Master entry \"{code}\" is inactive."
  - "Master entry \"{code}\" is a {category}, not a {category}."
  - "Choose the configured media product for this dehydrated media."
  - "Working standards are created by approving a qualification, not received here."
  - "A custom material type can only be saved with the type Other."
  - "'{name}' is the name of a built-in material type - pick it from the list or use a different name."

#### Status
- Dynamic stock status evaluates to **InStock** (or **Expired**).

#### What happens next
The lot appears in the Materials Stock register. If the material type requires a Certificate of Analysis (Dehydrated Media, Lyophilized Microorganisms, or Supplements), upload the COA before consuming the lot.

---

### 29.3 Managing Lot Documents: COA, Specifications, and Certificates
#### Purpose
Upload, verify, supersede, and void controlled documents associated with received material lots, including Certificates of Analysis, supplier certificates, material specifications, and safety data sheets.

#### Who can use it
- **Viewing and uploading documents**: Requires the *Manage materials* permission (by default: Analyst, Section Head, System Administrator).
- **Superseding and voiding documents**: Requires the *Control material documents* permission (by default: Section Head, System Administrator).

#### Before you start
- Document files must be formatted as PDF, JPG, JPEG, PNG, WEBP, or TIFF.
- File size must not exceed the maximum allowed size (default 25 MB).
- The file must contain valid binary content matching the declared extension.

#### What you see
- Material Lot Details drawer:
  - Tab: **Lot details and documents**.
  - Document list displaying: Document type badge (**COA**, **Supplier Certificate**, **Specification**, **SDS**, **Other**), file name, upload date, status (**Current**, **Superseded**, **Voided**), and action buttons.
  - Action buttons: **Upload Document**, **Supersede**, **Void**.
- Upload Dialog: Document type dropdown and file selector.
- Supersede Dialog: Replacement file picker, mandatory **A supersession reason is required** field, and confirm button.
- Void Dialog: Mandatory **A void reason is required** field and confirm button.

[SCREENSHOT SS-293]
[SCREENSHOT SS-294]

#### How to use it
##### Uploading a Document:
1. In the Materials Stock table, click **Details and documents** on the material row.
2. Under the documents tab, click **Upload Document**.
3. Select the **Document Type \*** (**COA**, **Supplier Certificate**, **Specification**, **SDS**, or **Other**).
4. Select the file and click **Upload**. The document is saved in status **Current**.

##### Superseding a Document:
1. On an existing **Current** document row, click **Supersede**.
2. Select the replacement file.
3. In the **Supersession Reason \*** field, enter a detailed explanation of why the document is being replaced.
4. Click **Confirm Supersession**. The old document transitions to **Superseded** and remains visible in the history; the new document becomes **Current**.

##### Voiding a Document:
1. On an active document row, click **Void**.
2. In the **Void Reason \*** field, enter a mandatory explanation for voiding the document.
3. Click **Confirm Void**. The document transitions to **Voided**. A voided document cannot be superseded.

#### System behavior
- **File integrity verification**: The system computes a SHA-256 cryptographic hash upon upload. Every subsequent download or view verifies the hash against stored bytes. If a mismatch occurs, access is refused: "Document integrity check failed. The file may have been altered. Please contact your System Administrator."
- **Audit tracking**: Uploading, superseding, and voiding record the user, timestamp, documented reason, and access events in the document audit log. No password signature is required.
- **COA gating**: For Dehydrated Media, Lyophilized Microorganisms, and Supplements, having at least one document of type **COA** in status **Current** satisfies the COA requirement for preparation workflows.

#### Validation
- "File exceeds the maximum allowed size of {n} MB."
- "The uploaded file is empty."
- "File type '{ext}' is not supported. Allowed types: PDF, JPG, JPEG, PNG, WEBP, TIFF."
- "The declared content type '{type}' does not match the file extension '{ext}'."
- "The file content could not be validated because it is too short."
- "The file does not appear to be a valid WEBP image."
- "The file content does not match the expected format for '{ext}'. The file may be corrupted or misnamed."
- "A supersession reason is required."
- "A replacement file is required for supersession."
- "A voided document cannot be superseded. Upload a new document instead."
- "A void reason is required."
- "This document is already voided."
- "Document integrity check failed. The file may have been altered. Please contact your System Administrator."

#### Status
- Document status transitions: **Current** (0) $\rightarrow$ **Superseded** (1) or **Voided** (2).

---

### 29.4 Stock Deduction and Inventory Consumption Rules
#### Purpose
Automatically verify eligibility, enforce quality gates, and deduct material quantities during laboratory testing, solution preparation, media preparation, and strain maintenance.

#### Who can use it
Triggered automatically during testing and preparation actions by authorized users (`Solutions.Prepare`, `Media.Prepare`, `Hplc.Operate`, `TestWorkflow.Execute`).

#### System behavior
When any laboratory workflow attempts to consume material stock, the system enforces the following verification sequence:
1. **Material Type Check**: Confirms that the material lot matches the required category (e.g., DehydratedMedia for media preparation, PrimaryStandard for titrant standardization).
2. **Expiration Gate**: Confirms that the material is not expired. If the expiration date is earlier than the current date, consumption is refused: "Material {name} (batch {batch}) is expired and cannot be used."
3. **Mandatory COA Check**: For Dehydrated Media, Lyophilized Microorganisms, and Supplements, the system verifies that a **Current** COA document is attached: "A current Certificate of Analysis (COA) is required before {name} (batch {batch}) can be used. Please upload a valid COA in the Inventory module first."
4. **Stock Quantity Check**: Confirms that the remaining quantity covers the requested amount. If stock is insufficient, the action is refused: "Insufficient stock: {name} (batch {batch}) has {remaining} {unit} remaining, {requested} requested."
5. **Consumption Deduction**: Decrements `QuantityRemaining` by the consumed amount and updates last-modified user and timestamp metadata.

#### Workflows consuming stock:
- **Solution Preparation**: Deducts component quantities upon signed completion.
- **Titrant Standardization**: Deducts primary standard weight upon signed submission (whether standardization passes or fails).
- **Relative Titration**: Deducts standard weight upon signed result recording.
- **HPLC System Suitability**: Deducts standard weight upon SST confirmation (whether suitability passes or fails).
- **Media Preparation**: Deducts dehydrated media weight upon saving a prepared lot.
- **Cryovial Preparation**: Deducts lyophilized discs upon batch preparation.
- **Media Evaluation**: Deducts one lyophilized disc from inventory stock immediately when assigned as a challenge organism source.

> **Note for reviewers:** Selecting a lyophilized microorganism disc as a challenge source during media evaluation deducts one unit from inventory stock immediately upon selection; the disc is not returned if the challenge is cancelled. (GAP-511)

---

### 29.5 Editing Material Lot Records
#### Purpose
Update non-critical receipt metadata, storage location, or corrected received quantities.

#### Who can use it
Requires the *Manage materials* permission (by default: Analyst, Section Head, System Administrator).

#### What you see
- Dialog: **Edit Material** (fields populated with existing lot metadata).

#### How to use it
1. Locate the lot in the Materials Stock register and click **Edit material**.
2. Update the storage location, manufacturer details, or catalog number.
3. If correcting the quantity received, enter the new value in **Quantity Received**.
4. Click **Save Changes**.

#### System behavior
- **Quantity adjustments**: Correcting **Quantity Received** automatically recalculates **Quantity Remaining** by applying the difference:
  $$\text{QuantityRemaining}_{\text{new}} = \text{QuantityRemaining}_{\text{old}} + (\text{QuantityReceived}_{\text{new}} - \text{QuantityReceived}_{\text{old}})$$
- **Media product lock**: Once media lots have been prepared from a dehydrated media batch, its linked media product cannot be changed: "Lots have already been prepared from batch {batch} - its media product can't be changed."
- Working standard lots cannot be edited through this interface: "Working standards change only through their qualifications."
