## 30. Equipment

MicroLIMS keeps equipment in two separate places, and it is important to know the difference:

- **Equipment Inventory** (sections 30.1 and 30.2) is the register of physical assets: operational status, calibration due date and calibration certificates.
- **Laboratory Configuration: Equipment** (section 30.3) is the laboratory's working record of the equipment: incubator set points and autoclave programs, with its own calibration due date.

The two records are linked only by the equipment code. Changing one does not change the other. Chromatography columns are kept in a third list (section 30.4).

MicroLIMS does not have equipment qualification records (IQ/OQ/PQ).

---

### 30.1 Equipment Inventory Overview and Asset Register
#### Purpose
View laboratory equipment assets, monitor operational availability, track calibration due dates and overdue statuses, and inspect equipment activity.

#### Who can use it
Requires the *Manage equipment* permission (by default: Analyst, Section Head, System Administrator). The equipment shown is limited to the laboratory sections the user is assigned to.

#### Before you start
- The user must be assigned to the relevant laboratory section (Microbiology or Physicochemical).

#### What you see
- Page title **Equipment**, subtitle "QC/Microbiology lab instrument register & active equipment traceability."
- Tabs:
  - **Equipment Register**: the register of assets.
  - **Active Equipment**: lists every asset in scope with its activities; the activities and history are matched to test records by equipment code.
- The Equipment Register table shows:
  - Columns: **Type**, **Manufacturer**, **Serial No.**, **Firmware**, **Code**, **Location**, **Calibration Due**, **Laboratory**, **Status**.
  - Calibration badges: **Overdue** (calibration due date before today) and **Due Soon** (calibration due within 30 days).
  - Status values: **InService**, **OutOfService**, **Retired**.
  - Row action menu: **Details and documents**, **Edit equipment**, **Audit history** (shown only to users allowed to see history).
  - Action button: **Register Equipment**.

[SCREENSHOT SS-301]

#### How to use it
1. Open **Laboratories** > **Microbiology Laboratory** > **Equipment Inventory** (or **Physicochemical Laboratory** > **Equipment Inventory**).
2. Review the register for upcoming calibration deadlines, overdue instruments and status values.
3. Open the **Active Equipment** tab to review the activity linked to test records.

#### System behavior
- Using an asset that belongs to a laboratory section the user is not assigned to is refused.
- **Overdue** is worked out each time from the calibration due date and today's date; it is not a stored status.
- The equipment print list contains assets with status **InService** only.
- **OutOfService** or **Retired** inventory equipment cannot be chosen as the storage location for media or cryovials. In the Physicochemical Laboratory, out-of-service or retired inventory equipment is refused when starting HPLC and ICP runs and physicochemical analysis.
- **Which record do test steps check?** When an incubator is chosen for a microbiology test step, the system checks the equipment record in Laboratory Configuration (section 30.3): that it is an incubator, that its set point is inside the medium's temperature range, and that its calibration due date has not passed. It does **not** look at the status or calibration date in Equipment Inventory. An incubator marked **OutOfService** in the inventory is still accepted if its configuration record is in range and in calibration. Recording incubation for a media evaluation accepts any incubator without these checks.

> **Note for reviewers:** Equipment Inventory and Laboratory Configuration Equipment are two records linked by code; update both (the intended procedure is unconfirmed). Test-time incubator checks read the configuration record rather than the inventory status or calibration date. (GAP-501, GAP-517)

> **Note for reviewers:** MicroLIMS does not record equipment qualification (IQ/OQ/PQ). (GAP-038)

---

### 30.2 Registering Equipment, Updating Status, and Calibration Documents
<!-- WF-EQUIP-001 -->
#### Purpose
Register new equipment assets, record changes to operational status with a comment, and keep calibration certificates with a check that the stored file is unchanged.

#### Who can use it
- **Registering and editing equipment, and uploading certificates**: requires the *Manage equipment* permission (by default: Analyst, Section Head, System Administrator).
- **Superseding and voiding certificates**: requires the *Control equipment documents* permission (by default: Section Head, System Administrator).

#### Before you start
- The equipment code must not already exist.
- Certificate files must be PDF, JPG, JPEG, PNG, WEBP or TIFF and must not exceed the maximum file size (25 MB by default).

#### What you see
- **Register Equipment** dialog (titled **Edit Equipment** when editing):
  - Fields: **Instrument Type**, **Manufacturer**, **Equipment Code**, **Location**, **Serial Number**, **Firmware Version**, **Laboratory**, **Calibration Due Date**, **Operational Status**, **Status Change Comment \*** (helper text "Required whenever the operational status changes.").
  - Buttons: **Cancel**, **Register Equipment** / **Save Changes**. The save button stays disabled until a Laboratory is chosen and, if the status was changed, until the comment is filled in.
- **Details and documents** drawer:
  - Document list showing calibration certificates, upload dates and statuses (**Current**, **Superseded**, **Voided**).
  - Buttons: **Upload Document**, **Supersede**, **Void**.

[SCREENSHOT SS-302]

#### How to use it
##### Registering new equipment
1. On the Equipment Inventory page, click **Register Equipment**.
2. Select the **Instrument Type** (Incubator, Autoclave, LAF Cabinet, Biological Safety Cabinet, Water Bath or Other).
3. Enter the **Equipment Code** and **Location**, and the optional **Manufacturer**, **Serial Number** and **Firmware Version**.
4. Select the **Laboratory** section.
5. If known, enter the **Calibration Due Date**.
6. Select the **Operational Status** (a new record starts as **InService** unless you choose another status).
7. Click **Register Equipment**.

##### Changing operational status
1. On the asset row, select **Edit equipment**.
2. Select the new **Operational Status** (**InService**, **OutOfService** or **Retired**).
3. Enter the reason in **Status Change Comment \***. It is required whenever the status changes.
4. Click **Save Changes**.

##### Managing calibration certificates
1. On the equipment row, click **Details and documents**.
2. To add a certificate, click **Upload Document**, select the file and confirm. The document is stored as **Current**.
3. To replace a certificate, click **Supersede**, select the replacement file, enter the reason for superseding and confirm. The earlier document becomes **Superseded**.
4. To void a certificate, click **Void**, enter the reason for voiding and confirm. The document becomes **Voided**.

#### System behavior
- Changing the operational status adds a status history entry with the previous status, new status, comment, user and time. There are no restrictions on moving between statuses.
- Calibration certificates are the only document type. Each time a certificate is opened or downloaded, the system checks that the file is unchanged since upload. If it has changed, access is refused: "Document integrity check failed. The file may have been altered. Please contact your System Administrator."
- Uploading or replacing a certificate does not change the **Calibration Due Date** on the equipment. Update that date separately on the equipment record.
- No electronic signature or password is required for registration, status changes or document actions. Changes are recorded in the audit trail.

#### Validation
- Messages shown by the dialog:
  - "Instrument type, equipment code, and location are required."
  - "Choose the laboratory this asset belongs to."
  - "A comment explaining the operational status change is required."
  - "Failed to load calibration certificates." (when the document list cannot be loaded)
- Messages returned by the system:
  - "Choose the laboratory this asset belongs to."
  - "Selected laboratory section was not found or is inactive."
  - "This equipment belongs to a laboratory section you are not assigned to."
  - "Equipment code \"{code}\" already exists."
  - "A comment explaining the reason for changing the operational status is required."
  - "Equipment {id} not found."
  - "Equipment {code} is not currently in service." (when the equipment is chosen as a storage location for media or cryovials)
  - "A replacement file is required."
  - "A reason for supersession is required."
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
- Equipment operational status: **InService**, **OutOfService**, **Retired**. Changed by editing the equipment with a comment.
- Calibration certificate status: **Current** (after upload), **Superseded** (replaced by a new file), **Voided** (reason required; cannot be voided twice).

---

### 30.3 Laboratory Configuration: Equipment, Set Points, and Autoclave Programs
<!-- WF-EQUIP-002 -->
#### Purpose
Configure the equipment the microbiology laboratory uses, keep incubator set points with a reason and history, define autoclave cycles (programs) and optionally pre-fill a new record from an inventory asset.

#### Who can use it
Requires the *Manage master data* permission (by default: Section Head, System Administrator).

#### Before you start
- Register the physical equipment in Equipment Inventory first if you want to pre-fill from it.
- Know the incubator operating temperature and the autoclave cycle details (load type, cycle time, temperature).

#### What you see
- Page title **Laboratory Configuration: Equipment**, subtitle "Configure equipment used by this laboratory, set points, and autoclave programs." with a **Refresh data** button.
- Tabs:
  - **Configured Equipment**: the configured equipment, with a card for the selected item (an incubator card shows the **Current Set Point Temperature** and a **Set Point Change History** table).
  - **Autoclave Programs / Loads ({n})**: the configured sterilization programs.
  - **Configuration History**: the set point changes of the selected incubator (columns **Effective On**, **Equipment Code**, **Category**, **Details / Change**, **Changed By**, **Reason / Comment**), although its caption also mentions autoclave events. Autoclave program history is opened per program with **View History**.
- Buttons: **Add Equipment**, **Select from Inventory**, **Edit Equipment**, **Edit Set Point** (incubators), **Add Program / Load** (autoclaves).

[SCREENSHOT SS-303]

#### How to use it
##### Adding equipment
1. Open **Laboratories** > **Microbiology Configuration** > **Equipment**.
2. To pre-fill from the inventory, click **Select from Inventory**, find the asset in the dialog and click **Configure for Lab**. The **Add Equipment** form opens with the asset's details filled in (assets that are already configured show **Configured** and cannot be selected again). Otherwise click **Add Equipment**.
3. Enter **Equipment Name \***, **Equipment Code \*** and select **Equipment Type \*** (Incubator, Autoclave, LAF Cabinet, Biological Safety Cabinet, Water Bath or Other).
4. If your account belongs to more than one laboratory section, select the **Laboratory Section \***. With one section it is used automatically.
5. Optionally enter **Vendor / Manufacturer**, **Location**, **Connection Settings (optional)**, **Set Point Temp (°C)** and **Calibration Due Date**.
6. Click **Create Equipment**. The message "Equipment created successfully." appears.

##### Changing an incubator set point
1. On the incubator's card, click **Edit Set Point**.
2. Enter the **New Set Point (°C) \***.
3. Enter the **Reason for Change \***.
4. Click **Save Changes**.

##### Configuring autoclave programs
1. On an autoclave's card, click **Add Program / Load**.
2. Enter **Program / Load Code \*** and **Program / Load Name \***.
3. Enter **Load Type \*** (for example Media, Glassware, Biohazard Waste).
4. Enter **Temperature (°C) \*** (starts at 121) and **Cycle Time (min) \*** (starts at 15).
5. Check the **Audit Comment / Reason \*** (it starts as "Initial program configuration" when adding; empty when editing).
6. Click **Save Program**.
7. To switch a program on or off, use **Activate** or **Deactivate** on the program row. A new program is active.

#### System behavior
- The equipment code must be unique; the name and code are trimmed when saved.
- **Select from Inventory** only pre-fills the form (name, code, location, vendor, calibration due date and an equipment type guessed from the instrument description). The records are not kept in step afterwards: later edits in Equipment Inventory do not update this record.
- **Edit Set Point** applies only to incubators, needs a reason and adds a history row with the previous value, new value, reason, user and time.
- Each autoclave program creation, update, activation or deactivation adds a history row.
- If another user has changed the record since you opened it, your save is refused (see Validation).
- Set point and autoclave program changes are recorded in the audit trail. No electronic signature or password is required.

> **Note for reviewers:** Changing the set point through **Edit Equipment** overwrites it with no reason and no history entry; always use **Edit Set Point**. (GAP-422)

> **Note for reviewers:** Set point and autoclave program changes are not electronically signed, and the autoclave **Audit Comment / Reason** is not enforced by the system. The screen's set point message says the reason is required "for audit" and names a data-integrity standard; this is screen wording only, not a statement the guide can confirm. (GAP-408, GAP-410, GAP-023)

> **Note for reviewers:** Equipment cannot be retired or deleted from this page (autoclave programs can only be deactivated), and qualification is not recorded here. The status shown for an item comes from the inventory. (GAP-038)

#### Validation
- Messages shown by the dialogs:
  - "Name, Code, and Type are required."
  - "Laboratory section is required."
  - "Please enter a valid numeric set point temperature."
  - A message that the Reason for Change is required (the screen text refers to a compliance standard; see the reviewer note above).
  - "Please fill out Program Code, Program Name, and Load Type."
  - Fallbacks: "Could not save equipment.", "Could not update set point.", "Could not save program.", "Status update failed."
- Messages returned by the system:
  - "Equipment {id} not found."
  - "Name is required."
  - "Code is required."
  - "Equipment code \"{code}\" already exists."
  - "Vendor cannot exceed 100 characters."
  - "Your account is not assigned to any laboratory section. Ask an administrator to assign one."
  - "Choose a laboratory section - you belong to more than one."
  - "A reason explaining the change to the set point temperature is required."
  - "Equipment {code} is not an incubator."
  - "Autoclave equipment {id} not found."
  - "Autoclave program {id} not found."
  - "Program code \"{code}\" already exists on autoclave {code}."
  - "This record was changed by someone else while you were working on it. Nothing was saved - reload it and make your change again."

#### Status
- Autoclave program: **Active** or **Inactive**. Changed with **Activate** / **Deactivate** or in the program dialog; each change adds a history row.

#### What happens next
- Incubators are checked against this configuration record when selected for media in individual and grouped test steps, shared TSB starts and pathogen confirmation: the set point must be inside the medium's temperature range and the calibration due date must not have passed.
- If an incubator is rejected, the message is always "The selected incubator's set point is outside this medium's temperature range.", even when the real reason is an overdue calibration.
- Autoclave programs configured here are used when preparing media lots: choosing a program fills in the load type, temperature and cycle time.

> **Note for reviewers:** An incubator rejected because its calibration is overdue shows the "set point is outside this medium's temperature range" message, which does not name the real cause. (BR-EQUIP-009)

---

### 30.4 Chromatography Columns
#### Purpose
Maintain the chromatography column list: code, name, serial number, USP designation and the instruments each column may be used on. Retired columns are deactivated.

#### Who can use it
- **Opening the page**: requires the *Manage master data* permission (by default: Section Head, System Administrator).
- **Adding, editing and deactivating columns**: requires the *Manage equipment* permission (by default: Section Head, Analyst, System Administrator).

#### Before you start
- Have the column code, name, serial number and USP designation available.
- The HPLC or GC instruments the column is compatible with must already exist in the same laboratory section.

#### What you see
- Page title **Chromatography Columns**, subtitle "Manage HPLC chromatography column master records, column parameters, and instrument compatibility."
- A search box ("Search code, name, serial, USP..."), a **Status** filter (**All Statuses**, **Active Only**, **Inactive Only**) and a **Section** filter.
- A table with the code, **Column Name**, USP designation, **Serial Number**, section, compatible instruments ("None linked" when empty) and **Active** / **Inactive** status. Row actions: **Edit** and **Deactivate**.
- Button **Add Column**. The dialog is titled **Add Chromatography Column** (or **Edit Column**) with **Column Code**, **Column Name**, **Serial Number**, **USP Designation**, **Laboratory Section**, **Compatible HPLC Instruments** and, when editing, the checkbox **Column is Active**.
- The deactivate confirmation is titled **Deactivate Chromatography Column**.

[SCREENSHOT SS-304]

#### How to use it
1. Open **Laboratories** > **Physicochemical Configuration** > **Chromatography Columns**.
2. Click **Add Column**.
3. Enter the **Column Code**, **Column Name**, **Serial Number** and **USP Designation** (for example L1, L7, L11).
4. Select the **Laboratory Section** if asked, then select the compatible instruments in **Compatible HPLC Instruments**.
5. Save the column.
6. To retire a column, choose **Deactivate** on its row and confirm. To use it again, edit the column and tick **Column is Active**.

#### System behavior
- Every HPLC and GC run records the physical column that was used.
- The column code must be unique. The USP designation is trimmed and converted to upper case.
- Compatible instruments must be HPLC or GC instruments in the same laboratory section as the column.
- Deactivating a column does not check whether a run is open.
- Column changes are recorded in the audit trail. No electronic signature or password is required.

> **Note for reviewers:** Opening the page needs the *Manage master data* permission, but saving needs the *Manage equipment* permission. By default an Analyst can save columns but cannot open the page. (GAP-607)

> **Note for reviewers:** Column usage (number of runs or injections per column) is not tracked; a run stores only which column was used. (GAP-612)

#### Validation
- "Column code is required."
- "Column name is required."
- "Column code \"{code}\" already exists."
- "Serial number cannot exceed 100 characters."
- "USP designation cannot exceed 10 characters."
- "One or more specified compatible equipment items do not exist."
- "Equipment \"{name}\" is not an HPLC or GC instrument. Only chromatography equipment can be linked to a column."
- "Equipment \"{name}\" belongs to another section. Compatible equipment must belong to the same laboratory section as the column."
- When starting an HPLC run:
  - "Column \"{name}\" is inactive."
  - "Column \"{name}\" is not compatible with \"{instrument}\"."
  - "Column {code} has no USP designation; set it in the column master."
  - "Column {code} is USP {x}; method {abbr} requires {y}."

#### Status
- Column: **Active** or **Inactive**. **Deactivate** makes it inactive; editing with **Column is Active** ticked makes it active again.

#### What happens next
A run can use only an active column that is compatible with the chosen instrument and whose USP designation matches the method.

<!-- Review log (Sonnet): 31 corrections; invented items removed: ?lab= route parameters, enum numbers (0/1/2), "database"/"hash"/SHA-256 wording, "mandatory/strictly" wording, GC and "bench" analysis refusal claim, claim that incubators gate media-evaluation workflows, ALCOA+ message quoted as fact; callouts added: GAP-038, GAP-023 (with GAP-408/410), column fields/filters from code; BR-EQUIP-014 cite replaced by GAP-038 -->
