## 26. Reference Strains (Reference Cryovials)

MicroLIMS manages working reference strain cryovial batches. A batch is prepared from a lyophilized microorganism lot in Materials Stock, checked physically against the organism's reference description, confirmed against released media, approved or rejected with a signature, and then used for thawing, storage moves and media evaluation challenges.

MicroLIMS does not record a passage number or generation count for reference strains.

> **Note for reviewers:** No passage or generation number is recorded for reference strains, and the way a cryovial expiry date is set is not defined. (GAP-037)

---

### 26.1 Reference Cryovials Overview
<!-- WF-CRYO-001 -->
#### Purpose
See the working cryovial batches, their approval status, remaining vials and expiry, and start the actions on a batch.

#### Who can use it
Requires the *Cryovial Management* permission (by default: Section Head, Reviewer, Analyst, System Administrator).

#### Before you start
Make sure the reference organisms exist in the Organisms list (§32).

#### What you see
- In the menu, open **LABORATORIES**, then **Microbiology Laboratory**, then **Reference Cryovials**. The page title is **Cryovials** and the subtitle is "Prepare working cryovial batches from an approved lyophilized microorganism material."
- The button **Prepare Cryovial Batch**, summary tiles, and a filter bar (Organism, Status, Expiry).
- Tabs **Cryovials** and **Approved Cryovials**.
- The **Cryovials** table has the columns **Code** (with the storage place), **Organism & Source** (organism, ATCC number, source material and batch), **Status**, **Vials Stock** (for example "9 of 10 vials", with **Depleted** at zero), **Prepared** (date and preparer) and **Expiry** (with **Expired** or **Expiring soon**), and a row action menu.
- The status shows **Pending Review**, **Approved** or **Rejected**; a destroyed batch shows **Destroyed**.
- The **Approved Cryovials** tab lists approved, not destroyed batches with the columns **Cryovial Code**, **Organism**, **ATCC No.**, **Manufacturer**, **Storage**, **Vials Remaining**, **Expiry** and **Status**.

[SCREENSHOT SS-261]

#### How to use it
1. Open **Reference Cryovials** and use the filters to find a batch.
2. Use the row action menu for the task you need. The actions that appear depend on the batch:

| Action | Shown when |
|---|---|
| **Approve batch**, **Reject batch** | The batch is **Pending Review** and not destroyed |
| **Thaw vial** | The batch is **Approved**, not destroyed, not expired, and has vials left |
| **Change storage location** | The batch is **Approved** and not destroyed |
| **Destroy (decommission batch)** | The batch is not destroyed |
| **View laboratory report record** | Always |

#### System behavior
- Expiry is worked out from the expiry date; it is not stored as a status. On the expiry date itself the screen already hides **Thaw vial**, although the system would still accept a thaw on that day.
- **Approve batch** and **Reject batch** are shown to every user who can manage cryovials. The system refuses the signature if the user does not hold the approval permission (§26.3).

#### Status
The approval status is **Pending Review**, **Approved** or **Rejected**. A batch can also be **Destroyed**.

---

### 26.2 Preparing a Working Cryovial Batch
<!-- WF-CRYO-001 -->
#### Purpose
Prepare a working cryovial batch from a lyophilized microorganism stock lot, record the physical check, the storage freezer and the identity confirmation on released media.

#### Who can use it
Requires the *Cryovial Management* permission (by default: Section Head, Reviewer, Analyst, System Administrator). The stock and freezer lists in the dialog also need *Material Stock Management* and *Equipment Inventory Management* (by default: Section Head, Analyst, System Administrator).

#### Before you start
- A lyophilized microorganism stock lot must be in stock in Materials Stock, with an organism set, a current Certificate of Analysis (COA), no expiry in the past and enough discs.
- At least one released, active, unexpired media lot of your laboratory section must exist for the identity confirmation.
- An incubator must exist, and an in-service freezer must be registered in Equipment Inventory. Without a freezer the **Save Batch** button stays disabled.

> **Note for reviewers:** A Reviewer may be unable to load the stock and freezer lists in this dialog, because by default the Reviewer role does not hold the stock and equipment inventory permissions. (GAP-514)

> **Note for reviewers:** The freezer is saved only as text, and the system does not check at preparation that it is a freezer, in service, or in the right section. The incubator, start and end dates of the identity rows are also not checked. (GAP-503)

> **Note for reviewers:** The expiry date and the vial and disc counts are typed by the user; the system does not derive or range-check them. (GAP-509, GAP-037)

#### What you see
- Click **Prepare Cryovial Batch**. The dialog **Prepare Cryovial Batch** has two sections.
- **Section 1: Source & Batch Information**:
  - **Lyophilized Microorganism Stock (Inventory) \***, with the organism, ATCC number, manufacturer and the **Reference Description (Organism Master Data)**. If the organism has no description, the dialog warns "No reference description configured for this organism. Add one under Laboratory Configuration → Organisms before confirming the physical check."
  - **Vials Prepared \***, **Discs Used \***, **Expiry Date \***, **Storage Equipment \***;
  - the checkbox "I confirm the physical characteristics match the reference description above. \*" and **Physical Check Notes / Discrepancies (optional)**.
- **Section 2: Identity Confirmation Panel** (a table; at least one row): **Media (GPT-released) \***, **Incubator \***, **Start \***, **End \***, **Observation**, and the button **Add Media Row**.
- Buttons **Cancel** and **Save Batch**.

[SCREENSHOT SS-262]

#### How to use it
1. Click **Prepare Cryovial Batch**.
2. Select the lyophilized stock lot and read the reference description.
3. Enter **Vials Prepared**, **Discs Used** (zero is allowed) and **Expiry Date**, and choose the **Storage Equipment** freezer.
4. Do the physical check and tick the confirmation checkbox. Add notes about any discrepancy.
5. In the identity confirmation panel, choose a released media lot, an incubator, the start and end of incubation, and an observation. Click **Add Media Row** for more rows.
6. Click **Save Batch**.
7. The system shows "Cryovial batch prepared successfully. Awaiting Section Head approval."

#### System behavior
- The batch code is the material code, a sequence and the two-digit year, in the same way as media lot numbers.
- The discs used are deducted from the stock lot in the same save, with the same checks as for media (expiry, current COA, enough stock).
- The batch starts as **Pending Review** with all prepared vials remaining. It cannot be used until it is approved.
- The system records who prepared the batch. That person can never approve it.
- No password or signature is asked at preparation.

#### Validation
In the dialog:
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

From the system on saving:
- "Physical check confirmation against the organism reference description is required."
- "Material {name} is not a Lyophilized Microorganism item."
- "Set an Organism on material \"{name}\" (Inventory > Materials Stock) before preparing cryovials from it."
- "Media lot {lot} is not GPT-released, out of stock, or rejected - cannot be used for identity confirmation."
- "At least one identity confirmation row is required."
- "Cryovial code {code} was taken by another preparation at the same moment. Nothing was saved - submit the preparation again."
- The stock checks: "Material {name} (batch {batch}) is expired and cannot be used.", the COA message and the "Insufficient stock" message (as in §22.2).

#### Status
A new batch is **Pending Review**.

#### Important rules
- The identity confirmation media must be released and in your laboratory section. The system does not check whether a media lot has expired; the dialog offers only unexpired lots.
- Destroyed or rejected batches cannot be used.

#### Errors
- "Could not prepare cryovial batch."

#### What happens next
A user with the approval permission, other than the preparer, approves or rejects the batch (§26.3).

---

### 26.3 Approving or Rejecting a Cryovial Batch
<!-- WF-CRYO-002 -->
#### Purpose
Release a prepared batch for use with a signature, or reject it.

#### Who can use it
Requires the *Cryovial Batch Approval* permission (by default: Section Head, System Administrator).

#### Before you start
- The screen offers **Approve** and **Reject** only for a pending batch that is not destroyed (the server itself checks only that the batch is still pending).
- You must not be the person who prepared it.

> **Note for reviewers:** Approval does not check the batch expiry date or the identity confirmation results. Whether the approver must check them is unconfirmed. (GAP-037)

#### What you see
- On the **Cryovials** table, the row menu of a **Pending Review** batch has **Approve batch** and **Reject batch**.
- A signature dialog shows the statement, an optional comment box, a password field, and **Cancel** and **Sign**.

#### How to use it
To approve:
1. Choose **Approve batch**.
2. Read the statement: `I am approving Cryovial Batch {code} for laboratory testing and media evaluation.`
3. Optionally enter a comment. Enter your password and click **Sign**.
4. The system shows "Cryovial batch {code} has been Approved."

To reject:
1. Choose **Reject batch**.
2. Read the statement: `I am rejecting and decommissioning Cryovial Batch {code}.`
3. Enter your password and click **Sign**.
4. The system shows "Cryovial batch {code} has been Rejected."

#### System behavior
- The system checks that the batch is still pending and that you are not the preparer, checks your password, and records the electronic signature (approved or rejected) with the comment.
- It saves a PDF copy of the batch record as an archived record.
- A rejection also marks the batch as destroyed.
- The decision is final.

#### Validation
- "Cryovial batch {code} has already been decided ({status})."
- "You cannot approve a cryovial batch you prepared."

#### Status
**Pending Review** becomes **Approved**, or **Rejected** with **Destroyed**.

#### Important rules
- Any other holder of the approval permission can approve, including someone who thawed or moved the batch.
- An approved batch can be thawed, moved, destroyed, and chosen as the working source in a media evaluation (§22.3).

#### What happens next
Thaw, move or destroy as needed (§26.4).

---

### 26.4 Thawing, Moving and Destroying
<!-- WF-CRYO-003 -->
#### Purpose
Record the use of a vial, change the storage place of a batch, or decommission a batch.

#### Who can use it
Requires the *Cryovial Management* permission (by default: Section Head, Reviewer, Analyst, System Administrator).

#### Before you start
- Thaw: the batch must be **Approved**, not destroyed, not expired and have vials left.
- Move: the batch must be **Approved** and not destroyed, and the new place must be in service.

> **Note for reviewers:** Destroying a batch needs no reason and no password, writes no entry in the review trail, and cannot be undone. (GAP-507)

#### What you see
- **Thaw vial** opens **Thaw Vial: Provide Reason**, which shows the batch code, organism and vials left, a text box (hint "Enter reason for thawing this cryovial..."), and **Cancel** and **Confirm Thaw**.
- **Change storage location** opens **Change Storage Location**, which shows the batch and its current place, a choice of **Storage equipment \*** (cold storage in service, Microbiology section) or **Storage area \***, the box **Reason for move \*** (helper "Recorded in the audit trail."), and **Cancel** and **Move**.
- **Destroy (decommission batch)** opens **Confirm Batch Destruction** with the warning "Destroying this cryovial batch will decommission it permanently. It will no longer be available for thawing, Media Evaluation, or GPT testing." and the button **Confirm Destruction**.

[SCREENSHOT SS-263]

[SCREENSHOT SS-264]

#### How to use it
To thaw a vial:
1. Choose **Thaw vial**.
2. Enter the reason (5 to 500 characters). **Confirm Thaw** stays disabled until the reason is long enough.
3. Click **Confirm Thaw**. The system shows "Vial from {code} successfully thawed and logged."

To move a batch:
1. Choose **Change storage location**.
2. Choose the new place and enter the reason (5 to 500 characters).
3. Click **Move**. The system shows "Storage location of cryovial batch {code} changed."

To destroy a batch:
1. Choose **Destroy (decommission batch)**.
2. Read the warning and click **Confirm Destruction**.
3. The system shows "Batch {code} has been decommissioned and destroyed."

#### System behavior
- A thaw reduces the vials remaining by one and records who thawed it and when, with your reason as the note. No password is asked. The 5-character minimum reason is enforced by the dialog; the system itself accepts an empty note.
- One thawed vial may serve several challenges. Choosing a cryovial batch as a source in a media evaluation does not reduce the vial count.
- A move saves the old place, the new place and your reason in the review trail.
- Destruction withdraws the batch from thawing, media evaluation and the approved list.

#### Validation
- "Cryovial batch {code} is not approved - cannot thaw a vial."
- "Cryovial batch {code} has been destroyed and cannot be used."
- "Cryovial batch {code} is expired and cannot be used."
- "No vials remaining in batch {code}."
- "A reason is required to move cryovial storage."
- "Exactly one of EquipmentId or LocationText must be provided."
- "Cryovial batch {code} is not approved - cannot move storage."
- "Cryovial batch {code} has been destroyed and cannot be moved."
- "Equipment {code} is not currently in service."
- In the dialogs: "Reason must be at least 5 characters."

#### Status
Thawing and moving do not change the approval status. Destroying sets **Destroyed**. When no vials remain, thawing is refused, but the batch status does not change.

#### Errors
- "Failed to thaw cryovial."
- "Failed to change storage location."
- "Failed to destroy cryovial batch."

#### What happens next
All thaw events and moves appear in the batch record (§26.5).

---

### 26.5 Cryovial Batch Record
#### Purpose
Review or print the history of one batch.

#### Who can use it
Requires the *Cryovial Management* permission (by default: Section Head, Reviewer, Analyst, System Administrator) to load the data.

#### What you see
- Choose **View laboratory report record** in the row menu. The record opens in a new tab.
- It shows: batch identity (code, organism, source material, material batch, manufacturer, expiry), stock and approval (vials prepared and remaining, storage, approval, prepared by, decided by and when), the physical check, the identity confirmation panel, the thaw history (who and when, with the reason), an event timeline, the signatures, the archived copies, and a **Print** button.

#### How to use it
1. Choose **View laboratory report record** for the batch.
2. Use **Print** (or the browser's print function) to print or save the page as a PDF.

<!-- Review log (Sonnet): 31 corrections; invented items removed: route paths, permission/enum codes and numeric codes, "Prepare New Batch" button name, invented table columns and dialog names, invented "legal signing statement", "Save" button on the move dialog, "Please enter a reason for thawing" message, unsupported report content; callouts added: GAP-037 (approval without expiry check), GAP-503, GAP-509, GAP-514, GAP-507 -->
<!-- Phase 13 (agy): applied F-B13 -->

<!-- Phase 13 review (Sonnet): F-B13 verified. -->
