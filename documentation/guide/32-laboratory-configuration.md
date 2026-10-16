## 32. Laboratory Configuration (Organisms, Receiving Configuration, Master Data Workbook)

MicroLIMS provides shared master data configuration interfaces that support laboratory-wide operations across Microbiology and Physicochemical sections. These include the canonical **Organisms** catalog referenced across challenge testing, stock materials, and test steps; **Receiving Configuration** reference lists governing sample intake metadata; and the **Master Data Workbook** onboarding engine for bulk data import, dry-run checking, electronically signed loading, and independent second-person verification.

---

### 32.1 Organisms Master
<!-- WF-ORG-001 -->
#### Purpose
Maintain the canonical microorganism repository (scientific name, ATCC catalog number, common name, and description) referenced by Media Challenge Specifications, Lyophilized Microorganism Materials, Reference Cryovials, and Test Master workflow steps.

#### Who can use it
- **Viewing organisms**: Available to any signed-in user.
- **Adding, editing, and deleting organisms**: Requires the *Manage master data* permission (by default: Section Head, System Administrator). The list is shared across all laboratory sections.

#### Before you start
- Verify the correct taxonomic scientific name and standard culture collection identifier (e.g., ATCC number).
- Ensure the scientific name is not already registered.

#### What you see
- Page title **Organisms**, subtitle "The canonical organism list referenced by Media Challenge Specs, Materials, and Cryovials."
- Inline entry form at the top:
  - Fields: **Scientific Name \*** (placeholder "e.g. Escherichia coli"), **ATCC No.** (placeholder "e.g. 25922"), **Common Name (optional)**, **Description (optional)**.
  - Buttons: **Add Organism** (or **Save Changes** and **Cancel** when editing an existing row).
- Organisms table displaying:
  - Columns: **Scientific Name** (sortable), **ATCC No.** (sortable), **Common Name** (sortable; "—" when blank), **Description** ("—" when blank).
  - Row action menu: **Edit**, **Delete** (danger).
  - Delete confirmation dialog: "Delete organism \"{name}\"? This cannot be undone."

[SCREENSHOT SS-321]

#### How to use it
##### Adding an Organism:
1. Navigate to **Laboratories** > **Microbiology Configuration** > **Organisms**.
2. In the inline form, enter the **Scientific Name \***.
3. Enter the **ATCC No.** (e.g., "25922"), optional **Common Name**, and optional **Description**.
4. Click **Add Organism**. The system confirms: "Organism \"{name}\" added."

##### Editing an Organism:
1. Locate the organism row in the table, click the row menu, and select **Edit**.
2. The form fields populate with existing metadata. Update the fields as needed.
3. Click **Save Changes**. The system confirms: "Organism \"{name}\" updated."

##### Deleting an Organism:
1. In the row menu, select **Delete**.
2. Confirm the deletion dialog: "Delete organism \"{name}\"? This cannot be undone."
3. If the organism is referenced by media configurations, cryovials, or material inventory, deletion is blocked by system safeguards.

#### System behavior
- Scientific names are unique, compared case-insensitively across the catalog.
- Updating an organism checks the record version to prevent concurrent overwrite collisions.
- Deleting an organism is refused if it is referenced by Media Configuration Challenges, Media Evaluation Challenges, Reference Cryovials, or Materials Stock lots.
- Organism creation, update, and deletion are audited in the general record-change log; no electronic signature password or change reason is required.

> **Note for reviewers:** Deleting an organism that is targeted by a Test Master workflow step is refused by a database constraint with the generic error message "An unexpected error occurred." (GAP-420)

#### Validation
- Client validations:
  - "Scientific Name is required."
- Server validations:
  - "Organism \"{name}\" already exists in the Organism list."
  - "Organism {id} not found."
  - "Cannot delete '{name}' - it is referenced by {n} record(s) (Media Configuration Challenges: {a}, Media Evaluation Challenges: {b}, Cryovials: {c}, Materials: {d})."
  - "This record was changed by someone else while you were working on it. Nothing was saved - reload it and make your change again."

#### What happens next
The registered organism is immediately available in selection dropdowns for Media Configurations (challenge organisms), Cryovial preparation, Materials Stock (Lyophilized Microorganisms), and selective/confirmatory plating steps in the Test Master.

---

### 32.2 Receiving Configuration (Samplers, Cause of Testing, Production Stages)
<!-- WF-RECVCFG-001 -->
#### Purpose
Maintain shared picklists and reference data presented on sample receiving forms across all receiving categories, including sampler suggestions, mandatory causes of testing, and manufacturing production stages.

#### Who can use it
Requires the *Manage master data* permission (by default: Section Head, System Administrator). Reading reference lists is open to any authenticated user during sample receipt.

#### Before you start
- Determine required administrative reasons for sample submission (causes of testing).
- Identify production stage names and assign them to standard production stage roles.

#### What you see
- Page title **Receiving Configuration**, subtitle "Reference lists offered on the sample receiving form, shared across all six receiving categories."
- Three configuration cards:
  1. **Samplers**:
     - Caption: "Offered as suggestions in the “Sampled By” field. Analysts can still type a name that isn’t listed."
     - Input field: **Sampler name** (placeholder "e.g. Ahmed Reda"), button **Add** (or **Save** / **Cancel** during renaming).
     - Row actions: **Rename**, **Delete**.
     - Delete confirmation: "Delete \"{name}\" from Samplers? Samples already recorded with this name keep it; only the suggestion list changes."
  2. **Cause of Testing**:
     - Caption: "Required on every sample. A cause already used by a sample cannot be deleted."
     - Input field: **Cause of testing** (placeholder "e.g. Investigation"), button **Add**.
     - Row actions: **Rename**, **Delete**.
     - Delete confirmation: "Delete \"{name}\" from Cause of Testing? If any sample already uses it, the delete will be rejected and nothing changes."
  3. **Production Stages**:
     - Caption: "The stage dropdown on Finished Product receiving. Not shown for other categories."
     - Input field: **Production stage** (placeholder "e.g. Coating"), dropdown **Role** (**Bulk**, **InProcess**, **Finished**, **Stability**, **Other**), button **Add**.
     - Row actions: **Rename**, **Delete**.
     - Delete confirmation: "Delete \"{name}\" from Production Stages? Samples already recorded with this stage keep it; only the dropdown changes."

[SCREENSHOT SS-322]

#### How to use it
##### Adding and Renaming Samplers:
1. Navigate to **General Laboratory Configuration** > **Receiving Configuration**.
2. On the **Samplers** card, type the sampler's full name and click **Add** (or press Enter). The system confirms: "\"{name}\" added."
3. To rename a sampler, click the pencil icon, modify the name, and click **Save**. Samples previously received with this text retain the original text.
4. To delete a sampler, click the bin icon and confirm the deletion.

##### Managing Causes of Testing:
1. On the **Cause of Testing** card, enter the submission justification (e.g., "Routine Release", "Investigation", "Annual Stability") and click **Add**.
2. To rename an entry, click the pencil icon, edit the text, and click **Save**. Because received samples maintain a foreign-key link to the cause record, renaming updates the cause name displayed across existing sample records.
3. To delete a cause, click the bin icon and confirm. If any sample has ever been received citing this cause, the system blocks deletion.

##### Configuring Production Stages:
1. On the **Production Stages** card, enter the stage name (e.g., "Granulation", "Blending", "Coating", "Packaging").
2. Select the functional **Role** (**Bulk**, **InProcess**, **Finished**, **Stability**, or **Other**).
3. Click **Add**.
4. To rename or adjust the assigned role, click the pencil icon, make changes, and click **Save**.
5. To delete a stage, click the bin icon and confirm.

#### System behavior
- Names within each list are unique, compared case-insensitively.
- The sampler list operates as a suggestion picklist on receiving forms; analysts can still enter unlisted sampler names manually.
- Cause of testing is mandatory on every received sample. Deleting a cause referenced by any sample is strictly blocked.
- Production stages appear exclusively when receiving Finished Product samples.
- Changes to receiving lists are recorded in the general audit log; no electronic signature password or change reason is required.

> **Note for reviewers:** Deleting a production stage removes the stage link from existing samples and can change which stage-specific specification rows and replicate counts apply to them. (GAP-709)

> **Note for reviewers:** Receiving configuration changes are not electronically signed and require no documented reason. (GAP-705)

#### Validation
- "Name is required."
- "Sampler \"{name}\" already exists."
- "Cause of Testing \"{name}\" already exists."
- "Production Stage \"{name}\" already exists."
- "Cannot delete '{name}' - it is referenced by {n} sample(s)."
- "Sampler {id} not found."
- "Cause of testing {id} not found."
- "Production stage {id} not found."

#### Status
- All active reference list entries are offered on sample intake forms.

---

### 32.3 Master Data Workbook: Template Download and Dry-Run Check
<!-- WF-WORKBOOK-001 -->
#### Purpose
Download master data onboarding templates populated with existing records and execute pre-import dry-run validation to detect formatting errors, missing columns, duplicate records, or data conflicts before committing changes.

#### Who can use it
Requires the *Manage master data* permission (by default: Section Head, System Administrator).

#### Before you start
- Prepare the source spreadsheet (`.xlsx`) using the downloaded template structure.
- Do not modify or delete column headers on existing sheets.
- Ensure new rows adhere to system data types (semicolon-separated lists, period decimal delimiters, dates formatted as `yyyy-MM-dd`).

#### What you see
- Page title **Master Data Workbook**, subtitle "Load organisms, departments, rooms, water points, material master, media products, equipment, items and specifications from one Excel workbook."
- Header button: **Download workbook**.
- Section 1: **1. Check the workbook**:
  - Button **Choose file** (accepts `.xlsx`), file status label (`{file.name}` or "No file chosen").
  - Button **Check file** / **Checking...** (executes non-committing dry run).
  - Button **Load and sign** (disabled until a clean check report exists).
- Validation Report tables (**WorkbookReportTable**):
  - Table 1 (**Rows per sheet**): Columns **Sheet**, **New**, **Unchanged**, **Errors**.
  - Table 2 (**Problems to fix**): Header "{N} problem(s) to fix in the workbook", Columns **Sheet**, **Row**, **Column**, **Problem**.

[SCREENSHOT SS-323]

#### How to use it
1. Navigate to **General Laboratory Configuration** > **Master Data Workbook**.
2. Click **Download workbook**. The system generates and downloads `MicroLIMS-master-data-{yyyy-MM-dd}.xlsx` containing 12 data sheets populated with existing active master data, an Instructions sheet, and data validation dropdowns.
3. Open the workbook in Excel and append new records beneath existing rows on sheets 1 through 10. Do not edit existing rows.
4. Return to MicroLIMS, click **Choose file**, and select the updated `.xlsx` file.
5. Click **Check file**.
6. Review the validation report table:
   - Verify the count of **New** and **Unchanged** rows per sheet.
   - Inspect Table 2 (**Problems to fix**) for sheet names, row numbers, and specific error descriptions.
7. Correct identified errors in the Excel file offline and re-click **Check file** until zero errors remain.

#### System behavior
- The workbook engine is strictly add-only: attempting to alter values on existing records triggers conflict errors ("Already in MicroLIMS with different values in: {columns}. The workbook only adds records - change it in its screen.").
- Maximum allowed file size is 10 MB.
- Maximum number of newly added rows across the entire workbook is 5,000.
- The dry-run validation executes inside a database transaction with per-row savepoints and rolls back completely; no records are altered or saved during a check.
- The downloaded workbook filters Material Master rows to the laboratory sections accessible to the user; other sheets export global records.

#### Validation
- "The file is empty."
- "The file is larger than 10 MB. Split it into several workbooks."
- "Unknown sheet '{name}'. Use the sheets of the downloaded workbook."
- "Missing column(s): {columns}."
- "The same record appears more than once on this sheet."
- "Already in MicroLIMS with different values in: {columns}. The workbook only adds records - change it in its screen."
- "The workbook has {n} new rows; the limit is 5000. Split it into several uploads."

---

### 32.4 Master Data Workbook: Signed Load and Second-Person Verification
<!-- WF-WORKBOOK-001, WF-WORKBOOK-002 -->
#### Purpose
Commit validated master data workbooks into the database using an electronic signature, enforce mandatory second-person verification, and activate newly imported catalog items and materials.

#### Who can use it
- **Loading and signing a workbook**: Requires the *Manage master data* permission (by default: Section Head, System Administrator).
- **Verifying a loaded workbook**: Requires the *Manage master data* permission (by default: Section Head, System Administrator) held by a **different user** than the person who loaded the file. System Administrators are subject to this segregation rule and cannot verify their own loads.

#### Before you start
- A dry-run check must be completed with zero errors for the selected file.
- The second-person verifier must have access to approved source documentation to inspect uploaded records offline.

#### What you see
- Button: **Load and sign** (color "success"; enabled when check report has zero errors).
- Section 2: **2. Loads and verification**:
  - Register table displaying previous loads:
    - Columns: **Loaded** (timestamp), **Loaded by** (full name), **File** (button to re-download stored file), **New records** (total created count), **Status** (**Waiting for verification** or **Verified by {name}**), **Actions** (**Verify** button).
  - Empty state: "No workbook loads yet — Check a workbook above, then load and sign it."
- **Electronic Signature** dialog for Loading:
  - Statement: "I am loading this master data workbook into MicroLIMS."
  - Inputs: **Comment (optional)**, **Password \***, buttons **Cancel**, **Sign**.
- **Electronic Signature** dialog for Verification:
  - Statement: "I verified this load against the approved source documents." (If loaded items lack specifications, the dialog appends: "Note: these items have no specifications yet and will become usable: {itemCodes}.").
  - Inputs: **Comment (optional)**, **Password \***, buttons **Cancel**, **Sign**.

[SCREENSHOT SS-324]

#### How to use it
##### Loading and Signing Master Data:
1. After achieving a clean check report with zero errors, click **Load and sign**.
2. In the Electronic Signature dialog, review the statement "I am loading this master data workbook into MicroLIMS."
3. Optionally enter an audit comment (e.g., change control authorization or protocol number).
4. Enter your system password and click **Sign**.
5. The system records the signature `MasterDataChanged`, executes the import, and sets the load status to **Waiting for verification**.
6. The screen confirms: "Workbook loaded. A second person must now verify it."

##### Verifying Master Data (Second Person):
1. A second qualified user signs in to MicroLIMS and navigates to **General Laboratory Configuration** > **Master Data Workbook**.
2. In section **2. Loads and verification**, locate the load showing status **Waiting for verification**.
3. Click the file name button in the **File** column to download the exact stored `.xlsx` file.
4. Perform an offline audit comparing the imported rows against approved source documents. If errors are identified, do not verify; correct records individually in their respective screens.
5. If the load is verified, click **Verify** on the register row.
6. In the Electronic Signature dialog, review the verification statement.
7. Enter an optional comment and your system password, then click **Sign**.
8. The system records the signature `Reviewed`, changes the status to **Verified**, and activates newly imported items and materials. The screen confirms: "Load of {fileName} verified. Its items are now active."

#### System behavior
- **Pre-execution signature**: The electronic signature (`MasterDataChanged`) is verified and captured before row processing begins; an incorrect password stops execution immediately.
- **Atomic rollback**: If any error occurs while committing rows during a signed load, the entire transaction rolls back completely; no partial master data is saved.
- **Activation states**:
  - Organisms, departments, rooms, water points, equipment, media products, and production stages become active immediately upon load completion.
  - Items and Material Master entries are created in status **Inactive** (frozen / unavailable for sample receipt or testing) until second-person verification is completed.
- **Enforced Segregation of Duties**: The system strictly prevents the loading user from verifying the same load: "The person who loaded the workbook cannot verify it. Ask a second person." System Administrators possess no exemption from this rule.
- **Verifier laboratory scope**: If the load created Material Master entries, the verifier must be assigned to those laboratory sections (or be a System Administrator).
- **No rejection action**: The application does not provide a "Reject Load" button; an erroneous load remains in status **Waiting for verification** while records are corrected or retired in individual configuration screens.

> **Note for reviewers:** Only items and material master entries wait for second-person verification; organisms, equipment, production stages, and media products are active immediately after the initial load. (BR-WORKBOOK-005)

> **Note for reviewers:** Second-person verification checks section membership only for material master entries; items, equipment, and specifications are not scope-checked at verification. (BR-WORKBOOK-006)

#### Validation
- Loading validations:
  - "The file name is longer than 260 characters. Rename the file and try again."
  - "The comment is longer than 2000 characters. Shorten it and sign again."
  - "Password verification failed. The signature was not applied."
  - "Too many failed signature attempts. Wait {n} minutes, then try again. The signature was not applied."
  - "Nothing was loaded: the workbook has problems to fix."
  - "The workbook load must run inside a transaction."
- Verification validations:
  - "Master data import {id} not found."
  - "This load has already been verified."
  - "The person who loaded the workbook cannot verify it. Ask a second person."
  - "This load added material master entries to lab section(s) you are not a member of: {sections}. Ask a Section Head of that section to verify it."
  - "Password verification failed. The signature was not applied."

#### Status
- Master data import status: **Waiting for verification** (`Loaded`), **Verified** (`Verified by {name}`).
