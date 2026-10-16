## 30. Equipment

MicroLIMS manages laboratory instrumentation across physical inventory registration, laboratory configuration parameters, calibration certificates, and chromatography column masters. The system tracks equipment operational status, calibration due dates, incubator set point history, autoclave sterilization programs, and instrument compatibility for chromatography runs.

MicroLIMS does not have equipment qualification records (IQ/OQ/PQ).

---

### 30.1 Equipment Inventory Overview and Asset Register
#### Purpose
View laboratory equipment assets, monitor operational availability, track calibration due dates and overdue statuses, and inspect active instrument usage history.

#### Who can use it
Requires the *Manage equipment* permission (by default: Analyst, Section Head, System Administrator). Visibility and access are strictly scoped to the user's assigned laboratory section (`?lab=MICRO` or `?lab=FP`).

#### Before you start
- The user must hold active membership in the relevant laboratory section (Microbiology or Physicochemical).

#### What you see
- Page title **Equipment**, subtitle "QC/Microbiology lab instrument register & active equipment traceability."
- Tabs:
  - **Equipment Register**: Comprehensive register of assets.
  - **Active Equipment**: Lists every asset in scope with its recent test activity and history matched by equipment code.
- Equipment Register table displaying:
  - Columns: **Type**, **Manufacturer**, **Serial No.**, **Firmware**, **Code**, **Location**, **Calibration Due**, **Laboratory**, **Status**.
  - Calibration badges: **Overdue** (calibration due date prior to the current date) and **Due Soon** (calibration due within 30 days).
  - Operational status values: **InService**, **OutOfService**, **Retired**.
  - Row action menu: **Details and documents**, **Edit equipment**, **Audit history** (visible to users authorized to inspect history).
  - Action button: **Register Equipment**.

[SCREENSHOT SS-301]

#### How to use it
1. Navigate to **Laboratories** > **Microbiology Laboratory** > **Equipment Inventory** (or **Physicochemical Laboratory** > **Equipment Inventory**).
2. Review the register for upcoming calibration deadlines, flagged overdue instruments, and operational status values.
3. Switch to the **Active Equipment** tab to review instrument utilization and activity linked to test records.

#### System behavior
- Equipment assets are filtered to the user's laboratory section. Attempting to access an asset belonging to another section is refused.
- Overdue calibration is computed dynamically by comparing the stored calibration due date to the current date.
- The equipment print list contains assets in status **InService** only.
- In Inventory, status **OutOfService** or **Retired** blocks an asset from being selected as a destination for media storage or cryovial storage moves. In Physicochemical workflows, out-of-service or retired inventory equipment is refused for HPLC, GC, ICP, and bench analysis runs.

> **Note for reviewers:** Equipment Inventory and Laboratory Configuration Equipment are two records linked by code; update both. Test-time incubator checks read the configuration record rather than the inventory status or calibration date. (GAP-501, GAP-517)

---

### 30.2 Registering Equipment, Updating Status, and Calibration Documents
<!-- WF-EQUIP-001 -->
#### Purpose
Register new equipment assets, record changes to operational status with mandatory justification comments, and maintain controlled calibration certificates with integrity verification.

#### Who can use it
- **Registering and editing equipment**: Requires the *Manage equipment* permission (by default: Analyst, Section Head, System Administrator).
- **Managing calibration certificates**: Uploading requires the *Manage equipment* permission (by default: Analyst, Section Head, System Administrator). Superseding and voiding certificates requires the *Control equipment documents* permission (by default: Section Head, System Administrator).

#### Before you start
- Ensure the equipment code is unique across the facility.
- Certificate files must be formatted as PDF, JPG, JPEG, PNG, WEBP, or TIFF and must not exceed the maximum allowed file size (default 25 MB).

#### What you see
- **Register Equipment** dialog:
  - Fields: **Instrument Type**, **Manufacturer**, **Equipment Code**, **Location**, **Serial Number**, **Firmware Version**, **Laboratory**, **Calibration Due Date**, **Operational Status**, **Status Change Comment \***.
  - Action buttons: **Cancel**, **Register Equipment** / **Save Changes**.
- **Details and documents** drawer:
  - Document list displaying attached calibration certificates, upload dates, statuses (**Current**, **Superseded**, **Voided**), and action buttons.
  - Buttons: **Upload Document**, **Supersede**, **Void**.

[SCREENSHOT SS-302]

#### How to use it
##### Registering New Equipment:
1. On the Equipment Inventory page, click **Register Equipment**.
2. Select the **Instrument Type** (e.g., Incubator, Autoclave, LafCabinet, BiologicalSafetyCabinet, WaterBath, Other).
3. Enter the unique **Equipment Code**, **Location**, and manufacturer details (**Manufacturer**, **Serial Number**, **Firmware Version**).
4. Select the **Laboratory** section.
5. If known, enter the **Calibration Due Date**.
6. Select the initial **Operational Status** (defaults to **InService**).
7. Click **Register Equipment**.

##### Changing Operational Status:
1. Locate the asset row on the Equipment Register and select **Edit equipment**.
2. Select the new **Operational Status** (**InService**, **OutOfService**, or **Retired**).
3. In the mandatory **Status Change Comment \*** field, enter a detailed explanation for the status change.
4. Click **Save Changes**.

##### Managing Calibration Certificates:
1. On the equipment row, click **Details and documents**.
2. To upload a new certificate, click **Upload Document**, select the certificate file, and confirm upload. The document is stored in status **Current**.
3. To supersede an existing certificate, click **Supersede**, select the replacement file, enter the required supersession reason, and confirm. The prior document transitions to **Superseded**.
4. To void an invalid certificate, click **Void**, enter the required void reason, and confirm. The document transitions to **Voided**.

#### System behavior
- New equipment records default to **InService** unless an alternative status is selected.
- Modifying the operational status requires a comment and creates a status history entry recording the previous status, new status, justification comment, user ID, and timestamp.
- Uploaded calibration certificates are hashed using SHA-256. Every view or download verifies the file hash against the stored hash. If the hash does not match, file access is refused: "Document integrity check failed. The file may have been altered. Please contact your System Administrator."
- Updating or replacing a calibration certificate document does not update the typed **Calibration Due Date** field on the equipment asset; the date must be updated separately on the equipment record.
- No electronic signature password is required for equipment registration, status updates, or document operations.

#### Validation
- Client validations:
  - "Instrument type, equipment code, and location are required."
  - "Choose the laboratory this asset belongs to."
  - "A comment explaining the operational status change is required."
  - "Failed to load calibration certificates."
- Server validations:
  - "Choose the laboratory this asset belongs to."
  - "Selected laboratory section was not found or is inactive."
  - "This equipment belongs to a laboratory section you are not assigned to."
  - "Equipment code \"{code}\" already exists."
  - "A comment explaining the reason for changing the operational status is required."
  - "Equipment {id} not found."
  - "Equipment {code} is not currently in service."
  - "A reason for supersession is required."
  - "A replacement file is required."
  - "A reason for voiding is required."
  - "A void reason is required."
  - "This document is already voided."
  - "Document integrity check failed. The file may have been altered. Please contact your System Administrator."
  - "File exceeds the maximum allowed size of {n} MB."
  - "The uploaded file is empty."
  - "File type '{ext}' is not supported. Allowed types: PDF, JPG, JPEG, PNG, WEBP, TIFF."
  - "The declared content type '{type}' does not match the file extension '{ext}'."
  - "The file content could not be validated because it is too short."
  - "The file does not appear to be a valid WEBP image."
  - "The file content does not match the expected format for '{ext}'. The file may be corrupted or misnamed."

#### Status
- Equipment operational status: **InService** (0), **OutOfService** (1), **Retired** (2).
- Equipment document status: **Current** (0), **Superseded** (1), **Voided** (2).

---

### 30.3 Laboratory Configuration: Equipment, Set Points, and Autoclave Programs
<!-- WF-EQUIP-002 -->
#### Purpose
Configure laboratory equipment records, maintain incubator operating set points with change reasons and audit history, configure autoclave cycles and load recipes, and pre-fill master equipment assets from inventory.

#### Who can use it
Requires the *Manage master data* permission (by default: Section Head, System Administrator).

#### Before you start
- Physical equipment should be registered in Equipment Inventory before pre-filling configuration records.
- Incubator operating temperatures and autoclave cycle parameters (load type, cycle time, temperature) must be determined.

#### What you see
- Page title **Laboratory Configuration: Equipment**, subtitle "Configure equipment used by this laboratory, set points, and autoclave programs."
- Tabs:
  - **Configured Equipment**: Table of configured equipment with set point indicators and inventory status chips.
  - **Autoclave Programs / Loads ({n})**: Configured sterilization cycles across all autoclaves.
  - **Configuration History**: Audit trail of set point changes for the selected incubator.
- Action buttons: **Add Equipment**, **Select from Inventory**, **Refresh data**.
- Card action buttons: **Edit Equipment**, **Edit Set Point** (incubators), **Add Program / Load** (autoclaves).

[SCREENSHOT SS-303]

#### How to use it
##### Adding Equipment Manually or from Inventory:
1. Navigate to **Laboratories** > **Microbiology Configuration** > **Equipment**.
2. To pre-fill from inventory, click **Select from Inventory**, locate the asset in the dialog, and click **Configure for Lab**. The system opens the **Add Equipment** dialog pre-populated with inventory metadata.
3. Alternatively, click **Add Equipment**.
4. Enter **Equipment Name \***, **Equipment Code \***, and select the **Equipment Type \*** (Incubator, Autoclave, LAF Cabinet, Biological Safety Cabinet, Water Bath, Other).
5. If your user account belongs to multiple sections, select the **Laboratory Section \***.
6. Enter optional details (**Vendor / Manufacturer**, **Location**, **Connection Settings (optional)**, **Set Point Temp (°C)**, **Calibration Due Date**).
7. Click **Create Equipment**.

##### Updating Incubator Set Points:
1. On the incubator card under **Configured Equipment**, click **Edit Set Point**.
2. In the dialog, enter the **New Set Point (°C) \***.
3. In the **Reason for Change \*** field, enter the operational reason for adjusting the operating temperature.
4. Click **Save Changes**.

##### Configuring Autoclave Programs:
1. On an autoclave card, click **Add Program / Load**.
2. Enter the **Program / Load Code \*** and **Program / Load Name \***.
3. Enter the **Load Type \*** (e.g., Media, Glassware, Biohazard Waste).
4. Enter the **Temperature (°C) \*** (defaults to 121) and **Cycle Time (min) \*** (defaults to 15).
5. Enter the **Audit Comment / Reason \*** (defaults to "Initial program configuration" when creating).
6. Click **Save Program**.
7. To activate or deactivate a program, click **Activate** or **Deactivate** on the program row.

#### System behavior
- Equipment codes are unique across the master data catalog.
- "Select from Inventory" pre-fills the configuration form with asset details and infers the equipment type from the inventory instrument description; it does not automatically synchronize subsequent edits made in Equipment Inventory.
- Updating an incubator set point through the **Edit Set Point** dialog requires a reason and appends a row to the incubator's set point history capturing the previous value, new value, reason, user, and UTC timestamp.
- Autoclave programs write a history row upon creation, update, activation, or deactivation.
- Set point and autoclave program updates are audited in the database but do not require an electronic signature password.

> **Note for reviewers:** Updating an incubator's set point via "Edit Equipment" overwrites the set point field directly without requiring a reason and without creating a history entry; always use "Edit Set Point". (GAP-422)

> **Note for reviewers:** Set point and autoclave program updates are not electronically signed, and the autoclave program audit comment field is not enforced by server validation. (GAP-408, GAP-410)

> **Note for reviewers:** Equipment cannot be retired or deleted from the Laboratory Configuration page, and qualification records are not maintained here. (BR-EQUIP-014)

#### Validation
- Client validations:
  - "Name, Code, and Type are required."
  - "Laboratory section is required."
  - "Please enter a valid numeric set point temperature."
  - "Reason for Change is required for audit and ALCOA+ compliance."
  - "Please fill out Program Code, Program Name, and Load Type."
  - Fallbacks: "Could not save equipment.", "Could not update set point.", "Could not save program.", "Status update failed."
- Server validations:
  - "Equipment {id} not found."
  - "Name is required."
  - "Code is required."
  - "Equipment code \"{code}\" already exists."
  - "Vendor cannot exceed 100 characters."
  - "The Microbiology laboratory section (code MICRO) is missing."
  - "A reason explaining the change to the set point temperature is required."
  - "Equipment {code} is not an incubator."
  - "Autoclave equipment {id} not found."
  - "Autoclave program {id} not found."
  - "Program code \"{code}\" already exists on autoclave {code}."
  - "This record was changed by someone else while you were working on it. Nothing was saved - reload it and make your change again."

#### What happens next
Configured incubators with active calibration dates and valid set points become eligible for selection during microbiology sample incubation, pathogen confirmation, and media evaluation workflows. Configured autoclave programs become selectable when preparing media lots in Media Preparation.

> **Note for reviewers:** When an incubator is rejected during test media selection because its calibration is overdue, the system displays the generic message "The selected incubator's set point is outside this medium's temperature range." (BR-EQUIP-009)

---

### 30.4 Chromatography Columns
#### Purpose
Maintain chromatography column master records, track physical column parameters (USP designation, dimensions, packing), define instrument compatibility, and deactivate retired columns.

#### Who can use it
- **Viewing the column master**: Requires the *Manage master data* permission (by default: Section Head, System Administrator).
- **Creating, updating, and deactivating columns**: Requires the *Manage equipment* permission (by default: Section Head, Analyst, System Administrator).

#### Before you start
- The column code and manufacturer serial number must be verified.
- Compatible HPLC or GC instrument assets must already exist in the Physicochemical laboratory section.

#### What you see
- Page title **Chromatography Columns**, subtitle "Manage HPLC chromatography column master records, column parameters, and instrument compatibility."
- Table of chromatography columns displaying designation, dimensions, serial number, compatible instruments, and active status.
- Deactivate dialog title **Deactivate Chromatography Column**.

[SCREENSHOT SS-304]

#### How to use it
1. Navigate to **Laboratories** > **Physicochemical Configuration** > **Chromatography Columns**.
2. To register a column, click **Add Column**.
3. Enter the **Column Code**, **Column Name**, **Serial Number**, and **USP Designation** (e.g., L1, L7, L11).
4. Select the compatible chromatography instruments located in the same laboratory section.
5. Save the column record.
6. To retire a column, open the column row and select **Deactivate**. Deactivated columns can be reactivated by editing the record.

#### System behavior
- Every HPLC and GC analytical run records the specific physical chromatography column used.
- Column codes must be unique.
- The USP designation is automatically trimmed and converted to uppercase.
- Linked compatible instruments must be chromatography instruments (HPLC or GC) belonging to the same laboratory section as the column.
- Column changes are recorded in the system audit trail; no electronic signature password is required.

> **Note for reviewers:** Page access requires MasterData.Manage while API write operations require Equipment.Manage; users holding only the Analyst role can perform writes but cannot access the interface under default permissions. (GAP-607)

> **Note for reviewers:** Column usage tracking (cycle or injection counts) is not implemented; analytical runs store only the linked column identifier. (GAP-612)

#### Validation
- "Column code is required."
- "Column name is required."
- "Column code \"{code}\" already exists."
- "Serial number cannot exceed 100 characters."
- "USP designation cannot exceed 10 characters."
- "One or more specified compatible equipment items do not exist."
- "Equipment \"{name}\" is not an HPLC or GC instrument. Only chromatography equipment can be linked to a column."
- "Equipment \"{name}\" belongs to another section. Compatible equipment must belong to the same laboratory section as the column."
- "Column \"{name}\" is inactive."
- "Column \"{name}\" is not compatible with \"{instrument}\"."
- "Column {code} has no USP designation; set it in the column master."
- "Column {code} is USP {x}; method {abbr} requires {y}."

#### What happens next
Active columns become selectable in the Start HPLC Run and Start GC Run wizards for compatible instruments and matching method USP designations.
