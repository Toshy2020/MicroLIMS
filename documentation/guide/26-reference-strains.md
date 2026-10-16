## 26. Reference Strains (Reference Cryovials)

MicroLIMS manages laboratory reference strains and working cryovial collections, governing the preparation of working cryovial batches from lyophilized stock materials, physical reference verification, purity and identity confirmation against released culture media, electronic signature authorization, vial thawing tracking, storage relocation, and decommissioning.

MicroLIMS does not track reference-strain passage numbers or generation counts.

---

### 26.1 Reference Cryovials Overview and Inventory
<!-- WF-CRYO-001 -->
#### Purpose
Track working reference strain cryovials in the microbiology laboratory, monitor remaining vial counts, review identity confirmation results, and inspect batch lifecycles.

#### Who can use it
Requires the *Manage cryovials* permission (`Cryovials.Manage`, by default: Section Head, Reviewer, Analyst, System Administrator).

#### Before you start
- Verify that reference organisms are defined in the canonical Organisms master list (§32).

> **Note for reviewers:** No passage or generation number is recorded for reference strains in the application. (GAP-037)

> **Note for reviewers:** Cryovial approval does not check batch expiry or identity-confirmation results. (BR-CRYO-015)

#### What you see
- On the navigation sidebar, under **LABORATORIES — Microbiology Laboratory**, click **Reference Cryovials** (`/laboratory-configuration/cryovials`).
- Page header:
  - Title: **Cryovials**
  - Subtitle: "Prepare working cryovial batches from an approved lyophilized microorganism material."
  - Action button: **Prepare New Batch**.
- Tabs:
  - **Cryovials**: operational management table of active, pending, and decommissioned batches.
  - **Approved Cryovials**: dedicated inventory register of approved, non-destroyed reference strain batches.
- Table columns:
  - **Batch Code**: unique reference strain batch identifier.
  - **Organism**: scientific name and ATCC number.
  - **Source Material**: source lyophilized stock lot and manufacturer.
  - **Prepared On**: preparation timestamp and preparer.
  - **Vials Remaining**: current vial balance versus initial prepared count.
  - **Storage Location**: cold storage equipment or storage condition text.
  - **Expiry Date**: lot expiration date.
  - **Status**: approval status badge (**Pending Review**, **Approved**, or **Rejected**) and destroyed indicator.
  - **Actions**: row actions (**Approve batch**, **Reject batch**, **Thaw vial**, **Change storage location**, **Destroy (decommission batch)**, and **View laboratory report record**).

[SCREENSHOT SS-261]

#### How to use it
1. In **Reference Cryovials**, review the list of working strain batches across the **Cryovials** and **Approved Cryovials** tabs.
2. Use the search and filter controls to locate specific organisms or batch codes.
3. To view comprehensive analytical details, click the report icon to open the **Cryovial Report** (§26.4).
4. Perform operational lifecycle tasks using row actions: prepare new batches (§26.2), approve/reject (§26.3), thaw vials, move storage, or decommission (§26.4).

#### System behavior
- Displays active and historical cryovial batches for the Microbiology Laboratory section.
- Batches whose expiry date has passed are computed as expired dynamically in the user interface.
- Action availability rules:
  - **Approve batch** and **Reject batch** appear while approval status is **Pending Review** and the batch is not destroyed.
  - **Thaw vial** is available only when the batch is **Approved**, not destroyed, unexpired, and has remaining vials (`> 0`).
  - **Change storage location** is available when the batch is **Approved** and not destroyed.
  - **Destroy (decommission batch)** is available for any non-destroyed batch.

#### Status
- Approval status: **PendingReview**, **Approved**, or **Rejected**.
- Decommissioned flag: **Destroyed** (sets `IsDestroyed = true`).

---

### 26.2 Preparing Working Cryovial Batches
<!-- WF-CRYO-001 -->
#### Purpose
Prepare a working cryovial batch from an approved lyophilized microorganism inventory material, log physical identity checks, select storage freezers, and record purity/identity confirmation against released media.

#### Who can use it
Requires the *Manage cryovials* permission (`Cryovials.Manage`, by default: Section Head, Reviewer, Analyst, System Administrator). Populating inventory stock and equipment pickers requires *Manage materials stock* (`Materials.Manage`) and *Manage equipment inventory* (`Equipment.Manage`).

#### Before you start
- A lyophilized microorganism stock lot must exist in Materials Stock with status **InStock**, linked to a canonical organism, with an active Certificate of Analysis (COA).
- Released, unexpired culture media lots must be available for identity confirmation.
- Microbiology cold storage equipment (freezers or deep freezers) with status **InService** must be operational.

> **Note for reviewers:** Reviewers may be unable to load stock and freezer equipment in the preparation dialog because the pickers require inventory permissions not held by Reviewers by default. (GAP-514)

> **Note for reviewers:** At cryovial preparation, storage equipment is stored as free text and is not validated by the server. (GAP-503)

> **Note for reviewers:** Cryovial expiry dates and preparation counts are typed by the user and are not range-checked. (GAP-509)

#### What you see
- In **Reference Cryovials**, click **Prepare New Batch**.
- Dialog: **Prepare Cryovial Batch**:
  - **Lyophilized Microorganism Stock (Inventory) \*** dropdown (lists in-stock lyophilized organism lots).
  - **Vials Prepared \*** (number greater than zero).
  - **Discs Used \*** (number zero or greater).
  - **Expiry Date \***.
  - **Storage Equipment \*** (dropdown of in-service cold storage freezers).
  - **Physical Check Notes / Discrepancies (optional)**.
  - Checkbox: **Physical check confirmation against the organism reference description is required. \***
  - Section: **Identity Confirmation (Released Media)** (table requiring at least one row):
    - **GPT-released media \*** dropdown.
    - **Incubator \*** dropdown.
    - **Incubation start date \*** and **Incubation end date \***.
    - **Observation notes / description**.
    - Button **Add Media Row**.
  - Buttons: **Cancel** and **Save Batch**.

[SCREENSHOT SS-262]

#### How to use it
1. Click **Prepare New Batch**.
2. Select the **Lyophilized Microorganism Stock (Inventory) \***.
3. Enter the number of **Vials Prepared \*** and **Discs Used \***.
4. Enter the batch **Expiry Date \***.
5. Select the **Storage Equipment \*** freezer.
6. Perform the physical check against the reference morphological description and select the confirmation checkbox.
7. Under **Identity Confirmation (Released Media)**:
   - Select a released media lot (**GPT-released media \***).
   - Select the **Incubator \***.
   - Enter the **Incubation start date \*** and **Incubation end date \***.
   - Enter morphological observation notes.
   - Click **Add Media Row** if confirming across multiple media formulations.
8. Click **Save Batch**.
9. The system validates all inputs, consumes the used discs from inventory stock, creates the batch, and displays: `Cryovial batch prepared successfully ({code}).`

#### System behavior
- Generates a batch code based on the source material code.
- Instantiates the batch with approval status **PendingReview** and sets `VialsRemaining` equal to `VialsPrepared`.
- Deducts the specified `DiscsUsed` from the inventory stock lot.
- Records the preparing user identity. The preparing user is permanently barred by segregation of duties from later approving the batch.
- Stores storage equipment as formatted text (`"{instrument type}, {manufacturer} ({code})"`) in the batch record.
- Cryovial preparation does not prompt for an electronic signature or password.

#### Validation
The system validates required entries and biological prerequisites:
- "Please select a Lyophilized Microorganism material."
- "Please enter a valid number of vials prepared (> 0)."
- "Please enter the number of discs used (0 or more)."
- "Please enter an expiry date."
- "Please select a Storage Equipment (Freezer / Deep Freezer)."
- "Please confirm the physical check against the reference description."
- "At least one Identity Confirmation row is required."
- "Row {n}: Please select a GPT-released media."
- "Row {n}: Please select an incubator."
- "Row {n}: Please specify the incubation start date."
- "Row {n}: Please specify the incubation end date."
- "Physical check confirmation against the organism reference description is required."
- `Material {id} not found.`
- `Material {name} is not a Lyophilized Microorganism item.`
- `Set an Organism on material \"{name}\" (Inventory > Materials Stock) before preparing cryovials from it.`
- `Media {id} not found.`
- `Media lot {lot} is not GPT-released, out of stock, or rejected - cannot be used for identity confirmation.`
- `At least one identity confirmation row is required.`
- "Cryovial code {code} was taken by another preparation at the same moment. Nothing was saved - submit the preparation again."

#### Status
- Approval Status: **PendingReview** (0).
- Destruction flag: `IsDestroyed = false`.

#### Important rules
- At least one identity confirmation row on released media is mandatory.
- The source lyophilized microorganism must have an organism assigned in Materials Stock.
- Prepared batches remain unusable for testing or thawing until formally approved by an independent Section Head.

#### Errors
- "Could not prepare cryovial batch."

#### What happens next
- An authorized Section Head reviews the preparation and identity confirmation data to approve or reject the batch (§26.3).

---

### 26.3 Cryovial Batch Approval and Rejection
<!-- WF-CRYO-002 -->
#### Purpose
Authorize a prepared working cryovial batch for laboratory use through a signed electronic decision, or reject and permanently decommission an unacceptable batch.

#### Who can use it
Requires the *Approve cryovial batches* permission (`Cryovials.Approve`, by default: Section Head, System Administrator).

#### Before you start
- The batch must have approval status **PendingReview** and must not be destroyed.
- The signing user must satisfy segregation of duties: they cannot be the user who prepared the batch.

> **Note for reviewers:** Cryovial approval does not verify batch expiry dates or identity-confirmation results on the server. (BR-CRYO-015)

#### What you see
- In **Reference Cryovials**, locate the batch with status **Pending Review**.
- In the **Actions** column, click **Approve batch** or **Reject batch**.
- Dialog: **Electronic Signature**:
  - Displays signer identity, role, and legal signing statement.
  - Text area: **Comment (optional)**.
  - Password field and buttons: **Cancel** and **Sign**.

#### How to use it
To approve a batch:
1. On the pending batch row, click **Approve batch**.
2. Review the signature statement: `I am approving Cryovial Batch {code} for laboratory testing and media evaluation.`
3. Optionally enter a comment.
4. Enter your password and click **Sign**.
5. The system validates credentials, marks the batch **Approved**, archives an immutable PDF record, and updates the status badge to **Approved**.

To reject a batch:
1. On the pending batch row, click **Reject batch**.
2. Review the signature statement: `I am rejecting and decommissioning Cryovial Batch {code}.`
3. Enter your password and click **Sign**.
4. The system marks the batch **Rejected**, sets `IsDestroyed = true`, and archives an immutable PDF record.

#### System behavior
- Verifies that the batch has not already been decided.
- Enforces segregation of duties: the user who prepared the batch is rejected with: `"You cannot approve a cryovial batch you prepared."`
- On approval: sets approval status to **Approved** (1).
- On rejection: sets approval status to **Rejected** (2) and sets `IsDestroyed = true`. Rejection permanently decommissions the batch.
- Archives an immutable PDF copy of the batch summary in the audit records.
- Records an electronic signature entry with meaning **Approved** or **Rejected**.

#### Validation
- `Cryovial batch {code} has already been decided ({status}).`
- "You cannot approve a cryovial batch you prepared."

#### Status
- On approval: Approval status changes to **Approved** (1).
- On rejection: Approval status changes to **Rejected** (2), and `IsDestroyed` becomes true.

#### Important rules
- The preparing user can never approve their own cryovial batch.
- Approved batches become immediately available for thawing and media evaluation challenges.
- Rejection is final and permanently decommissions the batch.

#### What happens next
- Approved batches are ready for routine laboratory use, thawing, and storage management (§26.4).

---

### 26.4 Vial Thawing, Storage Movements, and Decommissioning
<!-- WF-CRYO-003 -->
#### Purpose
Log the controlled thawing of individual reference strain cryovials with an audit reason, relocate batches to different cold storage equipment, or permanently decommission batches.

#### Who can use it
Requires the *Manage cryovials* permission (`Cryovials.Manage`, by default: Section Head, Reviewer, Analyst, System Administrator).

#### Before you start
- To thaw: the batch must be **Approved**, unexpired, not destroyed, and have remaining vials (`> 0`).
- To move storage: the batch must be **Approved** and not destroyed; destination storage must be in-service cold storage.
- To destroy: the batch must not already be destroyed.

> **Note for reviewers:** The reason for a vial thaw is enforced on the screen only (5 to 500 characters) and the server accepts an empty note. (BR-CRYO-009)

> **Note for reviewers:** Cryovial destruction requires no reason or password and cannot be undone. (GAP-507)

#### What you see
- In **Reference Cryovials**, locate the approved batch.
- Row action icons:
  - **Thaw vial**: opens `ThawVialReasonDialog`. Displays batch code, organism name, remaining vial count, text area **Reason for thawing this cryovial... \***, and button **Confirm Thaw** (disabled until reason is 5 to 500 characters).
  - **Change storage location**: opens `MoveCryovialStorageDialog`. Displays destination storage picker, text area **Reason for move \*** (minimum 5 characters), and button **Save**.
  - **Destroy (decommission batch)**: opens `DestroyCryovialDialog`. Displays warning: "Destroying this cryovial batch will decommission it permanently. It will no longer be available for thawing, Media Evaluation, or GPT testing.", and button **Confirm Destruction**.

[SCREENSHOT SS-263]
[SCREENSHOT SS-264]

#### How to use it
To thaw an individual vial:
1. In the batch row, click **Thaw vial**.
2. In the dialog, review the current vial balance.
3. In the text area, enter the mandatory reason for thawing (e.g., "Inoculation of Media Evaluation SDA Lot SDA/01/26", 5 to 500 characters).
4. Click **Confirm Thaw**.
5. The system decrements the remaining vial count by 1, logs the thaw event, and displays: `Vial from {code} successfully thawed and logged.`

To change storage location:
1. In the batch row, click **Change storage location**.
2. Select the new destination freezer from the in-service cold storage list.
3. Enter the mandatory **Reason for move \*** (minimum 5 characters).
4. Click **Save**.
5. The system updates the location, writes a review-trail event with previous and new locations, and displays: `Storage location of cryovial batch {code} changed.`

To destroy/decommission a batch:
1. In the batch row, click **Destroy (decommission batch)**.
2. Review the decommissioning warning.
3. Click **Confirm Destruction**.
4. The system decommissions the batch and displays: `Batch {code} has been decommissioned and destroyed.`

#### System behavior
- Thawing:
  - Decrements `VialsRemaining` by exactly 1.
  - Records a `ThawEvent` entry capturing user identity, timestamp, and the reason text.
  - Does not change the approval status of the batch.
  - When remaining vials reach zero, further thawing is blocked, but historical traceability remains.
- Storage relocation:
  - Verifies the destination equipment is in service.
  - Records a `StorageLocationChanged` review-trail event documenting acting user, old location, new location, and reason.
- Destruction:
  - Sets `IsDestroyed = true`.
  - Immediately withdraws the batch from thawing, Media Evaluation challenge selection, and active lists.
  - Destruction requires no password or electronic signature, and cannot be reversed.

#### Validation
The system validates operational conditions:
- "Please enter a reason for thawing (at least 5 characters)."
- "Reason must be at least 5 characters."
- `Cryovial batch {code} is not approved - cannot thaw a vial.`
- `Cryovial batch {code} has been destroyed and cannot be used.`
- `Cryovial batch {code} is expired and cannot be used.`
- `No vials remaining in batch {code}.`
- `A reason is required to move cryovial storage.`
- `Exactly one of EquipmentId or LocationText must be provided.`
- `Cryovial batch {code} is not approved - cannot move storage.`
- `Cryovial batch {code} has been destroyed and cannot be moved.`
- `Equipment {code} is not currently in service.`

#### Status
- Thawing and storage moves do not alter approval status.
- Decommissioned batches display as **Destroyed**.

#### Important rules
- Selecting a cryovial as an organism source in Media Evaluation challenges does not decrement the remaining vials count; only the explicit **Thaw vial** procedure logs a vial consumption.
- Decommissioning a batch is irreversible.

#### Errors
- "Failed to thaw cryovial."
- "Failed to change storage location."
- "Failed to destroy cryovial batch."

#### What happens next
- The cryovial summary report records all historical thaw events, storage relocations, and lifecycle events for audit inspection (§26.5).

---

### 26.5 Cryovial Summary Report
#### Purpose
Inspect and print the complete historical dossier of a working reference strain batch, including source lyophilized material details, preparation parameters, purity and identity confirmation results, thaw logs, and electronic signature authorizations.

#### Who can use it
Requires the *Manage cryovials* permission (`Cryovials.Manage`, by default: Section Head, Reviewer, Analyst, System Administrator) to view data.

#### What you see
- Navigated via direct URL (`/cryovials/:id/report`) or clicking **View laboratory report record** in the batch table.
- Printable summary record displaying:
  - Batch header: Cryovial code, organism scientific name, ATCC number, preparation date, and preparer name.
  - Source inventory: Lyophilized material name, stock batch number, manufacturer, and initial disc consumption count.
  - Vials summary: Total vials prepared, current vials remaining, expiry date, and current storage equipment.
  - Identity confirmation table: Culture media lots tested, incubators, incubation intervals, and observed morphological characteristics.
  - Thaw event history: Log of each thawed vial with timestamp, user name, and recorded thaw reason.
  - Authorization section: Electronic signature details (Approved or Rejected), signer name, role, timestamp, and comments.

#### How to use it
1. In the **Reference Cryovials** table, click the action icon **View laboratory report record**.
2. Review the historical record, identity confirmation results, and thaw logs.
3. Use the browser print dialog to print a physical copy or export to PDF/Word.
