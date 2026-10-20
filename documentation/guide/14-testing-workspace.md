## 14. Analyst Testing Workspace and Sample Preparation

### 14.1 Laboratory Testing Workspaces
#### Purpose
Give each laboratory area one workspace to monitor incoming workload, track sample progress, review assigned tests, confirm sample preparation, open test workflows and run grouped actions.

#### Who can use it
Users who are members of the relevant laboratory section. The system blocks the workspace for non-members (see Validation). System Administrators are not restricted by section membership.

#### Before you start
- Your account must be assigned to the laboratory section (Microbiology, or Physicochemical with the Finished Product area or the Raw Material and Packaging Material area).
- Samples must already be received.

#### What you see
- Workspace title and subtitle:
  - **Microbiology Laboratory Workspace**, with the subtitle "Manage incoming samples, assignments, testing progress, review, and laboratory workflow execution from one workspace."
  - **FP Workspace** (Physicochemical members with the Finished Product area).
  - **RM & PM Workspace** (Physicochemical members with the Raw Material and Packaging Material area).
- Buttons:
  - **Refresh**
  - **+ New Sample** (opens the sample registration dialog)
  - **Export** (downloads a CSV file named "receiving-testing-workspace-{date}.csv" with the columns Sample ID, Reference, Item / Display Name, Category, Batch No, Control No, Cause of Testing, Sampled By, Received At, Status)
- Workload tiles (clicking a tile switches that filter on or off):
  - **Needs Preparation** ("Preparation not yet signed off")
  - **Ready to Read** ("Incubation complete, awaiting result entry")
  - **Awaiting Review** ("All tests complete, awaiting reviewer")
  - **Overdue** ("Open more than 24 hours after receipt")
  - **Assigned to Me** ("Samples with a test assigned to you")
  - **Unassigned** ("No analyst assigned yet")
- When the workspace is opened from a dashboard link, a chip **Active Workload Filter:** shows "Retests in Progress", "Overdue Review (>24h)" or "Overdue Approval (>24h)".
- View switchers: **Table view** (default), **Card view** and **Kanban view**. The Kanban view is read-only, with the columns Received, Under Testing, Under Review, Under Approval, Approved, Rejected and Retest Requested.
- The register:
  - With no sample checked: **Laboratory Register (N samples)**, page sizes 25, 50, 100, 200 (default 50).
  - With one sample selected or several checked: **Laboratory Register (N of M)**, page sizes 25, 50, 100.
  - Empty list: "No matching samples found."
- Selected-sample panel (shown when exactly one sample is selected). Header buttons in order: **Sample Summary**, **Audit History**, **Edit Details**, **Void Sample**, **Prepare Sample** (only when preparation is outstanding) and **Deselect**.
  - **Edit Details** and **Void Sample** need the *Correct samples* permission (by default: Section Head, Reviewer, System Administrator). **Edit Details** is disabled once the sample has been submitted for review. **Void Sample** is not shown once the sample is voided.
  - Information cards: **ASSIGNED TO**, **PREP STATUS**, **CAUSE OF TESTING**, **BATCH NUMBER**, **CONTROL NUMBER**, **RECEIVED AT**, **SAMPLED BY**, **SAMPLE QTY**, **STORAGE**, **EXP DATE**, **CATEGORY**, plus one category-specific card: **STAGE** (Product), **SAMPLING POINT** (Water), **PREV PRODUCT** (After Cleaning), otherwise **MFG DATE**.
  - For item-based samples, links to the item documents **SOP** and **Verification Report**.
  - When preparation is outstanding, the banner **Sample Needs Preparation** with the text "Record the sample preparation and sign for it before starting laboratory tests." (Product, Raw Material, Packaging Material) or "Test locations and configuration must be completed before starting laboratory tests." (other categories), and the button **Start Preparation**.
  - **Open Pathogen Workflow**, shown only when an assigned test uses the shared TSB broth.
  - **Assigned Tests (N)** with the subtitle "Test Master configured laboratory workflows"; empty list: "No tests assigned to this sample."
- Grouped Actions panel (shown when two or more samples are checked): header **Grouped Actions**, subtitle "{n} samples selected across workspace", button **Deselect All** and a clear-selection icon.

[SCREENSHOT SS-141]

#### How to use it
1. Open the workspace of your laboratory from the **Laboratories** area of the sidebar.
2. Check the workload tiles for work that needs action, such as **Needs Preparation** or **Ready to Read**.
3. Filter the register by clicking a tile or using the filters above the list.
4. Click one sample row to open the selected-sample panel and see its assigned tests.
5. To act on several samples together, tick two or more sample checkboxes; the Grouped Actions panel opens.
6. To save the current list, click **Export**.

#### System behavior
- If you are not a member of the laboratory, the workspace is blocked with the messages below.
- Categories shown: the Microbiology workspace shows all six categories (Product, Raw Material, Packaging Material, Water, Environmental Monitoring, After Cleaning); the FP Workspace shows Product only; the RM & PM Workspace shows Raw Material and Packaging Material only.
- **Assigned to Me** lists samples that have a test assigned to you.
- Clicking a sample opens its panel without leaving the workspace.

#### Validation
- "You are not a member of the {lab name}."
- "You do not have access to the FP workspace."
- "You do not have access to the RM & PM workspace."
- "Sample #{id} could not be opened. It may not belong to this laboratory, or the server could not be reached."

#### Status
The register shows **Received**, **Under Testing**, **Pending Review**, **Pending Approval**, **Approved**, **Rejected** and **Cancelled / Voided**. The preparation card shows **Sample Needs Preparation** until preparation is confirmed, then the preparation-complete state.

> **Note for reviewers:** The register label **Cancelled / Voided** is used for three different statuses: retest requested, cancelled and voided. (GAP-204)

> **Note for reviewers:** **Void Sample** has no status condition other than "already voided", so a sample that is already approved or rejected can still be voided. (GAP-203)

#### Errors
"Sample #{id} could not be opened. It may not belong to this laboratory, or the server could not be reached."

#### What happens next
Depending on the sample, you confirm preparation, open an individual test workflow, or start grouped incubation.


### 14.2 Confirming Sample Preparation for Product, Raw Material and Packaging Material Samples
<!-- WF-PREP-001 -->
#### Purpose
Record and electronically sign the preparation (diluent, neutralizer, technique and volumes) of a Product, Raw Material or Packaging Material sample before microbiological testing can start.

#### Who can use it
Any signed-in user with access to the sample's laboratory section; no separate permission is needed. If an analyst is assigned to the sample's tests, only that analyst can prepare it. If the sample is unassigned, any authorised analyst of the section can prepare it.

#### Before you start
- The sample's preparation status is **Sample Needs Preparation**.
- If the sample is assigned, you must be the assigned analyst.
- Tests that belong only to the Physicochemical (FP) section need no preparation; the sample is ready at receipt.

#### What you see
Dialog **Preparation**. Which form appears depends on whether the item already has a standing preparation configuration:
- **Item with a configuration** (confirm form):
  - The configured preparation steps.
  - A chip **Approved** or **Pending Approval**. A configuration that is still pending can be used; the text explains that it awaits Section Head approval and that testing is not blocked.
  - Button **Confirm & Start Testing**.
- **Item without a configuration** (manual form):
  - **Sample Amount** (not checked as required by the form).
  - **Technique**: "Pour Plate" (default) or "Filtration".
  - **Filtration Volume (ml)** and **Washing Volume (ml)**, shown only for "Filtration".
  - **Diluent** and **Neutralizer**, both required.
  - A text explaining that the entry becomes the item's standing configuration and is sent to the Section Head for approval, while testing is not held up.
  - Button **Start Testing**, enabled once Diluent and Neutralizer are filled and the sample is not assigned to another analyst.

[SCREENSHOT SS-142]

#### How to use it
1. Select the sample and click **Prepare Sample** (or **Start Preparation** in the **Sample Needs Preparation** banner).
2. In the **Preparation** dialog, review the configured steps, or fill in the manual form.
3. Click **Confirm & Start Testing** (or **Start Testing**).
4. In the signature prompt, read the meaning: "I confirm the preparation steps entered above are the steps performed for this sample, and are correct for this item."
5. Enter your password and confirm.
6. Check the message "{n} sample(s) prepared and signed."

#### System behavior
- A sample can be prepared only once.
- When the sample is assigned to an analyst, only that analyst can confirm the preparation, whatever their role.
- When you enter the manual form for an item without a configuration, the values are saved as the item's standing configuration with the status **Pending Approval**; later samples of the item can use it at once.
- Confirming from an existing configuration copies the configured values into this sample's own preparation record, so later edits of the configuration do not change it.
- The user who signs becomes the assigned analyst of every test of the sample that is still waiting to start.
- The preparation status changes from **Sample Needs Preparation** to complete.
- The system records an electronic signature with the signer, role, meaning and time.

#### Validation
- "This sample has already been prepared."
- "This sample is assigned to {name}. Only the assigned analyst may perform sample preparation, unless reassigned by an authorized Section Head."
- "Diluent is required."
- "Neutralizer is required."
- "Filtration technique requires both Filtration Volume and Washing Volume."
- "This sample has no Item and cannot use the preparation configuration flow."
- "This item has no preparation configuration to confirm."

> **Note for reviewers:** The preparation form lets Section Head and System Administrator users press the button for a sample assigned to someone else, but the system still allows only the assigned analyst to confirm; the sample must first be reassigned. (GAP-219)

> **Note for reviewers:** The Sample Amount is not checked by the system. (GAP-228)

> **Note for reviewers:** Any active user can be assigned as the responsible analyst, whatever their role or laboratory, and the reason for assignment is optional. (GAP-213)

#### Status
- Preparation status: **Sample Needs Preparation** becomes preparation complete.
- The tests stay in **Waiting** (Pending), with the signing user as analyst; the sample stays **Received**.

#### Important rules
- Testing cannot start before preparation is confirmed. Starting incubation earlier is refused: "Test Preparation must be completed and confirmed for sample {ref} before incubation can be started for step \"{step}\"." The refusal is also written to the audit trail and the workflow history.

#### Errors
"Password verification failed. The signature was not applied."

#### What happens next
The banner disappears and the tests can be opened for media selection and incubation.


### 14.3 Preparing Environmental Monitoring, Water and After Cleaning Samples
<!-- WF-PREP-001 -->
#### Purpose
Select the rooms, sampling points or machine parts that were monitored, so that the system creates the laboratory tests for Environmental Monitoring, Water and After Cleaning samples.

#### Who can use it
Any signed-in user with access to the sample's laboratory section. No separate permission is needed.

#### Before you start
- The sample's preparation status is **Sample Needs Preparation**.
- Master data must exist: rooms with room test configurations (Environmental Monitoring), sampling points with sampling configurations (Water), machine parts with test configurations (After Cleaning).

#### What you see
Dialog **Preparation**, with a form by category:
- **Environmental Monitoring**: "Loading rooms..." while rooms load; one card per room with the subtitle "Grade {x}"; rooms without a test configuration are disabled; button **Start Testing**.
- **Water**: **Storage Condition \*** ("Refrigerator" or "Room Temperature"; placeholder "Select storage condition"); **Storage Time (hours) \*** (only for "Refrigerator"); "Loading sampling points..."; a grid of sampling points (points without assigned tests are disabled); button **Start Testing**, enabled once the storage data are complete.
- **After Cleaning**: "Loading machine parts..."; one card per machine part with the subtitle "Machine Part" (parts without a test configuration are disabled); button **Start Testing**.
- In each grid: **Select All**, a chip "{n} selected", and the badges **Assigned Tests** or **No tests configured**.

[SCREENSHOT SS-143]

#### How to use it
1. Select the sample and click **Prepare Sample** (or **Start Preparation**).
2. By category:
   - Environmental Monitoring: tick each room that was monitored.
   - Water: choose **Storage Condition \***; for "Refrigerator" enter **Storage Time (hours) \***; then tick the sampling points.
   - After Cleaning: tick each machine part.
3. Click **Start Testing**.

#### System behavior
- The system creates one test per distinct test code across all selected rooms, points or parts (waiting to start, Pending), and one sample location for each selected room, machine part or sampling point test.
- The user who prepares becomes the assigned analyst of every test created.
- Because of this, that user counts as having performed the tests and cannot later review or approve the sample ("You cannot review a sample you tested." / "You cannot approve a sample you tested.").
- For Water, the storage time is kept only for "Refrigerator".
- The preparation status becomes complete.
- This preparation is not electronically signed and needs no password.

#### Validation
- Environmental Monitoring: "At least one room must be selected."; "Room test configuration(s) not found: {ids}."; "Room(s) {names} do not belong to this sample's department."
- Water: "At least one sampling point must be selected."; "Storage condition is required for water samples."; "Storage time is required when a water sample was refrigerated."; "Sampling point(s) not found: {ids}."; "Sampling point(s) {codes} do not belong to this sample's department."
- After Cleaning: "At least one machine part must be selected."; "Machine part configuration(s) not found: {ids}."; "Part(s) {names} do not belong to this sample's machine."
- All: "This sample has already been prepared."

> **Note for reviewers:** Preparation of Environmental Monitoring, Water and After Cleaning samples is not signed and has no check on who prepares; the preparer automatically becomes the assigned analyst on all created tests. The receive and prepare actions need sign-in only, with no permission or laboratory check. (GAP-212, GAP-227)

> **Note for reviewers:** For After Cleaning, the system creates one test per distinct test code across the selected machine parts, not a collective pathogen test plus one TAMC test per part as in the earlier specification. (GAP-225)

#### Status
Preparation status becomes complete. New tests are waiting to start (Pending).

#### Errors
"Could not prepare sample."

#### What happens next
The new tests appear under **Assigned Tests** and are ready for media selection and incubation.


### 14.4 Grouped Sample Preparation
<!-- WF-PREP-001 -->
#### Purpose
Confirm and sign the preparation of several Product, Raw Material or Packaging Material samples of the same item in one action.

#### Who can use it
Any signed-in user with access to the section who is the assigned analyst of the samples (or any authorised analyst when they are unassigned).

#### Before you start
- Tick two or more samples in the register.
- The samples must share the same item preparation configuration and need preparation.

#### What you see
In the Grouped Actions panel:
- Section **Test Preparation Required (N)** with the note "Must complete before incubation".
- A row "Test Preparation · {item}" with the chips "× N Sample(s)" and **Approved** or **Pending Approval**; expanded, "Samples to prepare (N):" lists the sample references; a note appears if the configuration awaits Section Head approval.
- Button **Confirm & Start Testing (N Samples)**.
- A warning "{n} test(s) excluded from grouped actions" with the reasons.

#### How to use it
1. Tick the samples of the same item in the register.
2. In the Grouped Actions panel, open **Test Preparation Required**.
3. Check the samples listed.
4. Click **Confirm & Start Testing (N Samples)**.
5. Enter your password in the signature prompt.
6. Check the message "{n} sample(s) prepared and signed."

#### System behavior
- Samples assigned to another analyst are left out with the reason "Assigned to {name} - only the assigned analyst may prepare this sample."
- One password signs all samples; the system records one signature per sample.
- A sample that fails its own rule is skipped and reported. A wrong password fails the whole run if nothing has been signed yet.
- If the item's configuration changed after the group was loaded, the action is refused.
- Each confirmed sample becomes prepared, and the signing user becomes analyst of its open tests.

#### Validation
- "No samples were selected for grouped preparation."
- "The preparation configuration for this group no longer exists."
- "This item's preparation configuration changed after the group was loaded. Reload the grouped action and review the steps before confirming."

#### Status
Preparation status of each confirmed sample becomes complete.

#### What happens next
The prepared samples unlock the **Compatible Actions** section for grouped incubation.


### 14.5 Grouped Incubation Setup
<!-- WF-TESTING-001 -->
#### Purpose
Choose one released media lot and one suitable incubator and start incubation for compatible tests of several prepared samples together.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head, System Administrator).

#### Before you start
- Two or more samples are ticked in the register.
- The tests are at the same kind of step (setup, selective plating or stage 2 transfer).
- Preparation is confirmed for all included samples.
- A released, unexpired media lot of your laboratory section's material is available.
- An incubator with a calibrated set point in the medium's temperature range and a current calibration is available.

#### What you see
In the Grouped Actions panel:
- Section **Compatible Actions (N)** with the note "Shared operational workflow transition".
- If preparation is outstanding: the banner **Incubation Setup Locked**: "No incubation can start on a sample whose Test Preparation is still outstanding. Confirm the preparation above and the compatible setup actions appear here."
- If tests need individual result entry: **Tests Awaiting Individual Result Entry (N):** with "No grouped workflow action is available. The selected tests require individual result entry. Result entry must be completed separately for each test."
- Each group row: the chip "× N Sample(s)", the state **Ready: Setup**, **Ready: Plating** or **Ready: Transfer**; expanded, "Selected Samples (N Tests):", **Media Lot (...)** ("No released, active lots available" when none qualifies) and **Incubator** or **Transfer to Incubator** ("No eligible incubator in range" when none qualifies).
- Buttons **Cancel**, **Start Incubation (N Tests)**, **Transfer & Start Incubation (N Tests)** (selective plating) or **Transfer to Incubator (N Tests)** (stage 2); "Processing..." while submitting.

[SCREENSHOT SS-144]

#### How to use it
1. Tick two or more samples whose tests are ready for incubation.
2. Open the group under **Compatible Actions**.
3. Select the **Media Lot**.
4. Select the **Incubator**.
5. Click **Start Incubation (N Tests)** (or the transfer button shown).
6. Check the message "{n} test(s) started in incubation." (with " ({k} skipped)" when some were skipped).

#### System behavior
- Each test goes through the same checks as a single start (preparation, media, incubator range).
- For users with the Analyst role, tests assigned to another analyst are skipped.
- The lot must be released, not expired, and made from your laboratory section's material.
- An incubator is eligible if it is an incubator, has a calibrated set point inside the medium's temperature range, and its calibration is not overdue.
- If a test uses the shared TSB broth, starting it also starts the shared TSB for the sample's other pathogen tests.
- Result entry, biochemical tests and selective or confirmatory plate readings cannot be grouped.
- No electronic signature or password is needed.

#### Validation
Refusing the whole request:
- "Media lot must be selected for this workflow transition."
- "Media lot \"{lot}\" is not released for use, out of stock, or rejected."
- "Media lot \"{lot}\" is expired (expired on {date})."
- "Incubator \"{code}\" has no calibrated set point temperature."
- "Incubator \"{code}\" calibration expired on {date}."
- "The selected incubator's set point is outside this medium's temperature range."
- "Grouped actions cannot be executed against analytical result-entry operations. Result entry must be performed individually."

Skipping single tests:
- "Test order is assigned to another analyst."
- "Media lot \"{lot}\" was prepared from another laboratory section's material."
- "Test Preparation is not complete for this sample."
- "Sample preparation is not complete (locations/swabs not verified)."
- "Test order has completed all workflow steps."
- "Test order requires individual colony count entry. Result entry cannot be grouped."
- "Test order requires individual selective plate observation. Observations cannot be grouped."
- "Test order requires individual confirmatory readout. Observations cannot be grouped."
- "Biochemical test entry must be completed individually."

> **Note for reviewers:** Media release and expiry checks differ between starting one test and starting grouped tests: the grouped action checks release and expiry, the single-test selection checks release but not expiry. (GAP-220)

#### Status
Started tests change from waiting to **Incubating** (In Progress). The sample changes from **Received** to **Under Testing**.

#### Errors
- "No tests were started. {reasons}"
- "{n} of {m} started. {k} refused - {reasons}"
- "Failed to execute batch action."

#### What happens next
Plates stay in incubation until the minimum time has passed; the tests then appear under **Ready to Read**.


### 14.6 Overriding the Minimum Incubation Time (Skip Wait)
<!-- WF-TESTING-002 -->
#### Purpose
Let a supervisor bypass the minimum incubation time of one test so that results can be recorded before that time has passed.

#### Who can use it
Requires the *Supervise test workflows* permission (by default: Section Head, System Administrator).

#### Before you start
An incubation must be open on the test and its minimum time must not yet have passed.

#### What you see
In the test workflow dialog or the pathogen incubation view:
- The incubation countdown with elapsed time and expected reading time.
- Button **Skip Wait**, visible only to users with the permission.
- After the override: "Minimum wait time was skipped by a Section Head/System Administrator." and the result buttons (such as **Record Result** or **Record Observation**) become available.

#### How to use it
1. Open the workflow dialog of the incubating test.
2. Click **Skip Wait**.
3. Check that the dialog shows "Minimum wait time was skipped by a Section Head/System Administrator."
4. Record the plate counts or observations.

#### System behavior
- The system records who skipped the wait and when, on the open incubation.
- The time checks of that incubation are bypassed on every result path.
- Incubation hours, temperatures and the incubator are not changed.
- No reason, password or electronic signature is asked.

#### Validation
"No incubation window is currently open."

> **Note for reviewers:** Skip Wait bypasses the minimum incubation time without a reason, password or electronic signature. (GAP-226)

#### Status
The test stays **Incubating**, with the time check bypassed.

#### Errors
- "Could not skip the wait."
- "No incubation window is currently open."

#### What happens next
The analyst records plate counts or observations.


### 14.7 Item Preparation Configuration and Approval
<!-- WF-PREP-002 -->
#### Purpose
Hold the standing preparation (technique, diluent, neutralizer, volumes) of an item so that samples of that item are prepared consistently.

#### Who can use it
Requires the *Manage items* permission (by default: Section Head, System Administrator).

#### Before you start
The item must exist.

#### What you see
No dedicated screen for editing or approving the configuration has been located. The same values (**Sample Amount**, **Technique**, **Filtration Volume (ml)**, **Washing Volume (ml)**, **Diluent**, **Neutralizer**) and the chip **Pending Approval** or **Approved** appear in the **Preparation** dialog (see 14.2).

#### System behavior
- The first manual preparation of an item, or any saved change, sets the configuration to **Pending Approval**. Editing an existing configuration clears the earlier approval.
- Analysts can use a configuration that is not yet approved; testing is never held up for approval.
- Approval by a user with the permission records the approver and the time, and sets **Approved**. It needs no password or electronic signature.

#### Validation
- "Diluent is required."
- "Neutralizer is required."
- "Filtration technique requires both Filtration Volume and Washing Volume."
- "Item {id} not found."
- "Item {id} has no preparation configuration to approve."
- "This preparation configuration is already approved."

> **Note for reviewers:** The screen and the queue for approving item preparation configurations have not been located, and the system does not check that the person who approves differs from the analyst whose entry created the configuration. (GAP-214)

#### Status
**Pending Approval** becomes **Approved**.

#### What happens next
Later samples of the item show the configuration in the confirm form (see 14.2).

<!-- Review log (Sonnet): 62 corrections; invented items removed: route paths, section code names, class names, enum and audit codes, unsupported audit reason text, unsupported "Failed to load the sample register" and "Could not start testing" errors, "system administrators hold all roles"-type access wording, approval screen description; callouts added: GAP-203, GAP-204, GAP-213 (GAP-212, 214, 219, 220, 225, 226, 227, 228 kept) -->
