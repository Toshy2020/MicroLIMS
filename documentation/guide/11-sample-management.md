## 11. Sample Management and Tracking Board

### 11.1 Sample Register and Lifecycle Monitoring
#### Purpose
View, search, filter, and monitor incoming and in-progress laboratory samples across table, card, and kanban views, inspect workload summary indicators, and access sample-level actions.

#### Who can use it
Requires the *Own-lab sample receiving* permission (by default: Analyst, Reviewer, Section Head, System Administrator) or both the *Global sample receiving* and the *Own-lab sample receiving* permissions (by default: Section Head, System Administrator). Holders of the own-lab permission work with the laboratory sections they belong to; holders of both permissions can receive for any laboratory section. Row actions have their own permissions: **Assign Analyst** (*Assign responsible analyst* permission; by default Section Head, System Administrator), **Add Laboratory** (global sample receiving permission) and the edit icon and **Void Sample** (*Sample correction & voiding* permission; by default Section Head, Reviewer, System Administrator).

#### Before you start
You must be signed in with an account that holds one of the receiving permissions above.

#### What you see
- Page header: title **Receive Sample** and subtitle "Register incoming Finished Product, Raw Material, and Packaging Material samples, and choose which laboratories they go to."
- Action buttons: **Refresh** and **Receive** (shown when you are allowed to receive samples; see Section 12).
- Key performance indicator cards:
  - **Needs Preparation** ("Preparation not yet signed off")
  - **Ready to Read** ("Incubation complete, awaiting result entry")
  - **Awaiting Review** ("All tests complete, awaiting reviewer")
  - **Overdue** ("Open more than 24 hours after receipt")
  - **Assigned to Me** ("Samples with a test assigned to you")
  - **Unassigned** ("No analyst assigned yet")
- Filter bar:
  - Search field: "Search by item, reference number, control number, batch number..."
  - **Item Type** dropdown (options include "All Item Types")
  - **Sample Status** dropdown (options include "All Sample Statuses", "Received", "Under Testing", "Pending Review", "Under Review", "Under Approval", "Approved", "Rejected", "Cancelled / Voided". **Cancelled / Voided** finds samples that were voided, cancelled, or closed with a retest request. **Pending Review** finds samples with a section waiting for technical review or for the release decision, so it overlaps **Under Review** and **Under Approval**.)
  - **Test Status** dropdown
  - Date pickers: **Received From** and **Received To**
  - View toggle options: **Table view**, **Card view**, and **Kanban view**
- Table view columns (in order):
  1. **Received At**
  2. **#**
  3. **Item / Reference** (with a select-all checkbox in the column header)
  4. **Item Type**
  5. **Cause**
  6. **Sampled By**
  7. **Batch / Control No.**
  8. **Assigned To**
  9. **Sample Status**
  10. **Test Status Summary**
  11. **Actions**
- Row chips and indicators:
  - Retest indicator: **Retest** chip (tooltip "Expand retest chain" or "Collapse retest chain")
  - Assignment indicator: **Unassigned** chip, or assigned analyst chip (tooltips "Click to reassign analyst (Section Head)" and "Assign responsible analyst (Section Head)")
- Selection bar (visible when one or more samples are checked):
  - Counter: "{n} sample(s) selected"
  - Indicator: "{n} hidden by current filters"
  - Guidance text: "Select at least one more to open grouped preparation and incubation setup.", "Grouped actions are available for this selection.", or "Showing only the selected samples; other filters are paused."
  - Action buttons: **Show selected only** (or **Back to filters**) and **Clear selection**
- Row actions menu:
  - **Assign Analyst**
  - **Add Laboratory**
  - **Prepare Sample**
  - **Void Sample**
  - **View Full Report**
  - **Audit History**
  - Edit icon (tooltip "Edit Sample Details" when editable, or "Locked: the sample has been submitted for review")
- Kanban view columns: **Received**, **Under Testing**, **Under Review**, **Under Approval**, **Approved**, **Rejected**, and **Retest Requested**.

[SCREENSHOT SS-111]

#### How to use it
1. Open **RECEIVING** and select **Receive Sample** from the sidebar navigation.
2. Review the summary indicator cards at the top of the screen to identify items requiring immediate attention, such as samples requiring preparation, ready incubation readings, or pending reviews.
3. Use the filter bar to locate specific records:
   - Enter a search term (item name, reference number, control number, or batch number) in the search box.
   - Filter by item type, sample status, test status, or receipt date range.
4. Toggle between **Table view**, **Card view**, and **Kanban view** using the view buttons on the right side of the filter bar.
5. In table view, select checkboxes on sample rows to trigger the selection bar. Click **Show selected only** to isolate the selected samples from the remaining list.
6. Open the **Actions** menu on any sample row to perform sample-level operations, view audit history, or generate reports.

#### System behavior
- The overall sample status is a roll-up of each laboratory's own sign-off status. The least advanced open laboratory decides (testing, then review, then approval). If no laboratory is still open, a rejected laboratory makes the sample Rejected; otherwise a retest request makes it Retest Requested; otherwise the sample is Approved.
- The sample register displays consolidated status labels:
  - Samples in received status display as **Received**.
  - Samples undergoing testing display as **Under Testing**.
  - Samples awaiting review display as **Pending Review**.
  - Samples awaiting release approval display as **Pending Approval**.
  - Samples approved display as **Approved**.
  - Samples rejected display as **Rejected**.
  - Samples with a retest requested, a cancelled status, or a voided status all display as **Cancelled / Voided**.

| Register label | Badge / review screens | Status |
|---|---|---|
| **Under Testing** | **In Testing** | in testing |
| **Pending Review** | **Under Review** | under review |
| **Pending Approval** | **Under Approval** | under approval |
| **Cancelled / Voided** | **Retest Requested**, **Cancelled** or **Voided** | closed without approval |

> **Note for reviewers:** The sample register displays the single label "Cancelled / Voided" for three different statuses: retest requested, cancelled, and voided. (GAP-204)

> **Note for reviewers:** Receiving, correcting, voiding, adding a laboratory and assigning an analyst have no approved intended-workflow document; this section describes observed screen behavior only. Any active user of any role can be chosen as the responsible analyst, and the reason for the assignment is optional. (GAP-200, GAP-213)

#### Errors
- If the register cannot load data, the system displays the notification: "Failed to load the sample register."


### 11.2 Tracking Board
#### Purpose
Provide a read-only cross-laboratory overview of every received sample across both Microbiology and Physicochemical laboratories, showing overall sample progress and the specific workflow stage in each laboratory.

#### Who can use it
Requires the *Cross-lab sample tracking* permission (by default: Section Head, System Administrator).

#### Before you start
Samples must be registered in the system with at least one test order created.

#### What you see
- Page header: title **Tracking Board** and subtitle "Every received sample across both laboratories, with each lab's own stage."
- Filter bar:
  - **Laboratory** dropdown (options: "All Laboratories", "Microbiology Laboratory", "Physicochemical Laboratory")
  - **Overall Status** dropdown (options: "All Overall Statuses", "In Progress", "Approved", "Rejected", "Retest Requested", "Voided", "Cancelled")
  - **Product / Item** dropdown (displays helper "Product list unavailable - reload the page to retry." if loading fails)
  - Date pickers: **Received From** and **Received To**
- Table columns:
  - **Reference**
  - **Product**
  - **Batch**
  - **Received**
  - **Overall**
  - **Microbiology**
  - **Physicochemical**
- Laboratory stage chips:
  - "Testing"
  - "Under Review"
  - "Under Approval"
  - "Approved"
  - "Rejected"
  - "Retest Requested"
  - "Closed"
  - "Voided"
  - "Not requested" (displayed when the sample was not submitted to that laboratory)
- Empty state: displays "No samples found matching the filter criteria." when no records match the active filters.

[SCREENSHOT SS-112]

#### How to use it
1. Open **RECEIVING** and select **Tracking Board** from the sidebar navigation.
2. Review the cross-laboratory status of all samples.
3. Filter the board by selecting a specific laboratory, overall status, product/item, or received date range.
4. Compare progress between the **Microbiology** and **Physicochemical** columns to identify whether testing, review, or approval is pending in either department.
5. Click a sample row to open the sample summary dialog.

#### System behavior
- The tracking board displays all received samples that have at least one test order generated, including samples where initial test orders were superseded by retest orders.
- The **Product** column dynamically displays identifier information based on the sample category:
  - For After Cleaning samples: displays the machine name.
  - For Water samples: displays the sampling point code, or the water department name.
  - For Environmental Monitoring samples: displays the department name.
  - For Product, Raw Material, and Packaging Material samples: displays the item name.
- The tracking board is strictly read-only. Viewing records or changing filter criteria does not alter record states or generate audit trail entries.

> **Note for reviewers:** The Tracking Board displays only samples with at least one test order; Environmental Monitoring, Water, and After Cleaning samples do not appear until their preparation has been completed. (GAP-229)


### 11.3 Correcting Sample Details
<!-- WF-SAMPLE-004 -->
#### Purpose
Modify administrative and traceability details of a received sample under electronic signature control and audit trail recording before testing results are submitted for review.

#### Who can use it
Requires the *Sample correction & voiding* permission (by default: Section Head, Reviewer, System Administrator).

#### Before you start
- The sample must be in **Received** or **Under Testing** status. Once any test of the sample is submitted for review, the sample record is locked and cannot be edited.
- To modify the item or sampling location, no laboratory testing work (incubation, plate count, observation, media usage, or location result entry) may have started.

#### What you see
- Dialog title: **Edit Sample Details**
- Lock notification banner (when locked): "This sample has been submitted for review, so its details are locked."
- Editable fields:
  - Item or location selector (item for Product, Raw Material, or Packaging Material; "Water Department" for Water; "Department" for Environmental Monitoring; "Machine" for After Cleaning). Displays helper text "Locked: testing has started for this sample" when testing work has begun.
  - **Batch Number**
  - **Control Number**
  - **Cause of Testing**
  - **Sampled By**
  - **Sample Quantity**
  - **Production Stage** (for Finished Product)
  - **Mfg Date**
  - **Exp Date**
  - **Previous Product** (for After Cleaning)
  - **Previous Product Batch Number** (for After Cleaning)
  - **Storage Condition** (options: "Room Temperature", "Refrigerator")
  - **Storage Time (hours)** (required when storage condition is "Refrigerator")
  - **Reason for Correction** (mandatory; helper text: "Recorded with your electronic signature and in the sample's audit trail.")
- Action buttons: **Cancel** and **Sign & Save** (disabled when details are locked or a required value, including the reason, is blank).

[SCREENSHOT SS-113]

#### How to use it
1. Locate the sample in the sample register or testing workspace.
2. Click the edit icon in the register row actions, or click **Edit Details** in the testing workspace sample panel.
3. In the **Edit Sample Details** dialog, modify the necessary fields.
4. Enter a detailed justification in the **Reason for Correction** field.
5. Click **Sign & Save**.
6. In the electronic signature prompt, enter your user password to confirm the signature meaning for sample correction.
7. Confirm that the notification displays: "Sample details corrected and signed."

#### System behavior
- The system verifies that at least one field has been modified; if no data changed, the submission is rejected.
- The system records the electronic signature with the signer, role, meaning and reason.
- An audit trail record is created storing the old and new values for each modified field.
- If the batch number or control number is changed, the system automatically propagates the updated identifiers to associated result records.
- If the item or location is changed prior to the start of testing, the system removes any recorded preparation data, removes test orders that do not belong to the newly selected item, generates test orders required by the new item, and returns the sample preparation status to **Needs Preparation** (or **Ready** if the new item requires only Finished Product tests with no preparation).

#### Validation
The system enforces the following exact validation rules:
- "A reason for the correction is required."
- "No changes were made."
- "Sample details can only be corrected until the sample is submitted for review - this record is locked."
- "Testing has already started for this sample, so its item or location can no longer change. Void the sample and receive it again."
- "A retest sample must test the same item or location as its original sample, so it cannot be changed."
- "The \"Retest\" cause of testing is assigned by the system to retest samples and cannot be set or changed here."
- "The expiry date cannot be before the manufacturing date."
- "Control number is required."
- "Sampled by is required."
- "Batch number is required."
- "Production stage is required."
- "Previous product is required."
- "Previous product batch number is required."
- "Production stage '{stage}' is not a known stage."
- "The selected cause of testing does not exist."
- "The new item must belong to the same category as the sample."
- "The selected item does not exist."
- "Item '{name}' is frozen and cannot be used."
- "Item '{name}' has no assigned tests."
- "The selected water department does not exist."
- "The selected department does not exist."
- "The selected machine does not exist."
- "Storage condition is required for a prepared water sample."
- "Storage condition must be Refrigerator or RoomTemperature."
- "Storage time is required when a water sample was refrigerated."
- "Storage time cannot be negative."

> **Note for reviewers:** Correcting and voiding samples have no approved intended-workflow document, and nothing prevents the user who received or tested a sample from correcting or voiding it. (GAP-201)


### 11.4 Voiding a Sample
<!-- WF-SAMPLE-005 -->
#### Purpose
Strike an erroneously created or invalid sample and all of its current (non-superseded) tests, including tests that are already approved or rejected, from active laboratory workflows under electronic signature and audit trail tracking.

#### Who can use it
Requires the *Sample correction & voiding* permission (by default: Section Head, Reviewer, System Administrator).

#### Before you start
The sample must not already be in Voided status.

#### What you see
- Dialog title: **Void Sample Confirmation**
- Subtitle: "Strikes the sample from the register - signed with your password"
- Warning banner: "The sample record and its open tests will be marked as Voided." The banner says "open tests", but the system voids every current test of the sample, including approved and rejected ones.
- Mandatory field: **Reason for Voiding \*** (helper text displays "A reason for voiding is required." when empty, and "Recorded with your electronic signature and in the sample's audit trail." when populated).
- Action buttons: **Cancel** and **Sign & Void** (disabled until a reason is entered).

[SCREENSHOT SS-114]

#### How to use it
1. Locate the sample in the sample register or laboratory workspace.
2. In the row actions menu (or workspace header panel), select **Void Sample**.
3. In the **Void Sample Confirmation** dialog, enter a complete explanation in the **Reason for Voiding \*** field.
4. Click **Sign & Void**.
5. In the electronic signature prompt, enter your user password to confirm the signature meaning for sample voiding.
6. Verify the confirmation message: "Sample voided." (in the laboratory workspace: "Sample #{id} ({name}) has been marked as Voided.").

#### System behavior
- The system records the electronic signature with the signer, role, meaning and reason.
- The sample status is set to **Voided**.
- All non-superseded test orders associated with the sample are set to **Voided**.
- All laboratory section sign-off records are set to **Voided**.
- The sample's result records are marked as **Voided**.
- The certificate remarks field on the sample is populated with "Voided: {reason}".
- An audit trail entry is created recording the previous status, the new voided status, and the list of voided test orders.
- Voided is a terminal status.
- In the sample register, the sample displays under the status label **Cancelled / Voided**. In the Tracking Board, it displays under **Voided**.

#### Validation
The system enforces the following exact validation rules:
- "A reason for voiding is required."
- "Sample is already voided."

> **Note for reviewers:** A sample can be voided at any status except already Voided, including after approval or rejection, with no warning if an archived certificate of analysis exists. (GAP-203)

<!-- Review log (Sonnet): 14 corrections; invented items removed: Sign & Save disabled-when-unchanged, signature timestamp, 'reopened/edited/reset' claim, section-membership and System Administrator unrestricted-access claim, enum names; callouts added: workflow-spec/analyst assignment note in 11.1 (GAP-200, GAP-213) -->
<!-- Phase 13 (agy): applied F-A02, F-A06, F-B11, R-NEW-02 -->
<!-- Phase 13 review (Sonnet): checked against RECONCILIATION section 2 and frontend strings; no invented items -->
