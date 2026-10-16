## 14. Analyst Testing Workspace and Sample Preparation

### 14.1 Laboratory Testing Workspaces
#### Purpose
Provide a centralized operational dashboard for each laboratory area to monitor incoming workload, track sample progress, review assigned tests, confirm sample preparations, execute test workflows, and perform batch operations.

#### Who can use it
Requires active membership in the relevant laboratory section (by default: Analyst, Reviewer, Section Head, System Administrator). System Administrators possess unrestricted access across all workspaces.

#### Before you start
- The user account must be assigned to the corresponding laboratory section (`MICRO` for Microbiology, or `FP` with the designated area for Physicochemical workspaces).
- Samples must be received into the laboratory register.

#### What you see
- Workspace title and navigation:
  - Route `/microbiology/workspace`: title **Microbiology Laboratory Workspace**, subtitle "Manage incoming samples, assignments, testing progress, review, and laboratory workflow execution from one workspace."
  - Route `/physicochemical/workspace`: title **FP Workspace** (for members assigned to Finished Product testing).
  - Route `/physicochemical/rm-pm-workspace`: title **RM & PM Workspace** (for members assigned to Raw Material and Packaging Material testing).
- Action buttons:
  - **Refresh** (reloads workspace sample queues)
  - **+ New Sample** (opens the sample registration dialog)
  - **Export** (downloads a CSV file named `receiving-testing-workspace-{date}.csv` with columns: Sample ID, Reference, Item / Display Name, Category, Batch No, Control No, Cause of Testing, Sampled By, Received At, Status)
- Key Performance Indicator (KPI) workload tiles (clicking a tile toggles that filter):
  - **Needs Preparation** (hint: "Preparation not yet signed off")
  - **Ready to Read** (hint: "Incubation complete, awaiting result entry")
  - **Awaiting Review** (hint: "All tests complete, awaiting reviewer")
  - **Overdue** (hint: "Open more than 24 hours after receipt")
  - **Assigned to Me** (hint: "Samples with a test assigned to you")
  - **Unassigned** (hint: "No analyst assigned yet")
- Active workload chip filter when deep-linked: **Active Workload Filter:** displaying values such as "Retests in Progress", "Overdue Review (>24h)", or "Overdue Approval (>24h)".
- View switchers: **Table view** (default), **Card view**, and **Kanban view** (read-only Kanban columns: Received, Under Testing, Under Review, Under Approval, Approved, Rejected, Retest Requested).
- Register displays:
  - Full register when no samples are checked: title **Laboratory Register (N samples)**, page sizes [25, 50, 100, 200], default 50.
  - Compact register when a sample is selected or multiple samples are checked: title **Laboratory Register (N of M)**, page sizes [25, 50, 100].
  - Empty state displays: "No matching samples found."
- Embedded Selected Sample Testing Panel (visible when exactly one sample is selected):
  - Header buttons in order: **Sample Summary**, **Audit History**, **Edit Details** (enabled when the sample has not been submitted for review), **Void Sample**, **Prepare Sample** (visible when preparation is required), and **Deselect**.
  - Metadata cards: **ASSIGNED TO**, **PREP STATUS**, **CAUSE OF TESTING**, **BATCH NUMBER**, **CONTROL NUMBER**, **RECEIVED AT**, **SAMPLED BY**, **SAMPLE QTY**, along with category-specific fields: **STAGE** (Product), **SAMPLING POINT** (Water), **PREV PRODUCT** (After Cleaning), or **MFG DATE**; and **STORAGE**, **EXP DATE**, **CATEGORY**.
  - Controlled item documents: **SOP** and **Verification Report** links for item-based samples.
  - Outstanding preparation banner: **Sample Needs Preparation** with instruction "Record the sample preparation and sign for it before starting laboratory tests." (Product/RM/PM) or "Test locations and configuration must be completed before starting laboratory tests." (monitoring samples), accompanied by the button **Start Preparation**.
  - Pathogen session link: **Open Pathogen Workflow** (displayed only when an assigned test uses shared TSB broth).
  - Test orders section: **Assigned Tests (N)** with subtitle "Test Master configured laboratory workflows" (displays "No tests assigned to this sample." when empty).
- Embedded Grouped Actions Panel (visible when two or more samples are checked in the register):
  - Header **Grouped Actions** with subtitle "{n} samples selected across workspace", **Deselect All** button, and clear selection control.

[SCREENSHOT SS-141]

#### How to use it
1. Navigate to the laboratory workspace corresponding to your assigned department from the sidebar (**Microbiology Laboratory** or **Physicochemical Laboratory**).
2. Review the KPI workload tiles to identify tasks requiring immediate action (such as **Needs Preparation** or **Ready to Read**).
3. Filter the register by clicking a KPI tile, using the search bar, or selecting specific workload filters.
4. To view sample details and assigned tests, click a single sample row to display the embedded Selected Sample Testing Panel.
5. To perform batch operations across multiple samples, select two or more sample checkboxes to display the embedded Grouped Actions Panel.
6. To export the current workspace list, click **Export**.

#### System behavior
- Route authorization:
  - If a user attempts to access a workspace without active membership in that laboratory section, the route guard blocks access and displays: "You are not a member of the {lab name}."
  - Attempting to access unauthorized physicochemical workspaces displays: "You do not have access to the FP workspace." or "You do not have access to the RM & PM workspace."
- Category restrictions per laboratory:
  - Microbiology Laboratory displays samples from all six categories: Product, Raw Material, Packaging Material, Water, Environmental Monitoring, and After Cleaning.
  - FP Workspace displays Product samples only.
  - RM & PM Workspace displays Raw Material and Packaging Material samples only.
- Workload filtering:
  - Selecting **Assigned to Me** restricts the display to samples where at least one open test order is assigned to the current user's account identifier.
- Testing panel display:
  - Clicking a sample updates the compact register and mounts the selected sample panel without leaving the workspace.

#### Validation
The system enforces the following access validation messages:
- "You are not a member of the {lab name}."
- "You do not have access to the FP workspace."
- "You do not have access to the RM & PM workspace."
- "Sample #{id} could not be opened. It may not belong to this laboratory, or the server could not be reached."

#### Status
- Samples display register statuses: **Received**, **Under Testing**, **Pending Review**, **Pending Approval**, **Approved**, **Rejected**, and **Cancelled / Voided**.
- Preparation statuses displayed: **Sample Needs Preparation** (NeedsPreparation) and preparation complete (Ready).

#### Errors
- "Failed to load the sample register."
- "Sample #{id} could not be opened. It may not belong to this laboratory, or the server could not be reached."

#### What happens next
Depending on the sample state, the analyst proceeds to confirm sample preparation, open individual test workflows, or initiate grouped incubation setup.


### 14.2 Confirming Sample Preparation for Product, Raw Material, and Packaging Material Samples
<!-- WF-PREP-001 -->
#### Purpose
Record and electronically sign the sample preparation parameters (diluent, neutralizer, technique, and volumes) for Product, Raw Material, or Packaging Material samples before microbiological testing can begin.

#### Who can use it
Any signed-in user with section access to the sample who is assigned as the responsible analyst on the sample's test orders. If the sample is unassigned, any authorized analyst in the section may prepare it.

#### Before you start
- The sample must be in **Received** status with preparation status **Needs Preparation**.
- For assigned samples, the logged-in user must be the assigned analyst.
- Tests belonging exclusively to the Physicochemical (FP) section do not require preparation; their preparation status is set to **Ready** at receipt.

#### What you see
- Dialog title: **Preparation**
- Form layout depends on whether an approved or pending standing preparation configuration already exists for the item:
  - **Confirm Preparation Form** (item has an existing configuration):
    - Displays the configured standing preparation parameters.
    - Status chip: **Approved** or **Pending Approval** (an unapproved configuration can still be used for testing).
    - Informational text when pending: explains that the configuration is awaiting Section Head approval but testing is not blocked.
    - Action button: **Confirm & Start Testing**.
  - **Manual Test Preparation Form** (item has no prior preparation configuration):
    - **Sample Amount** numeric input field.
    - **Technique** dropdown (options: "Pour Plate", "Filtration"; default is "Pour Plate").
    - **Filtration Volume (ml)** and **Washing Volume (ml)** numeric input fields (displayed only when "Filtration" technique is selected).
    - **Diluent** text field (mandatory).
    - **Neutralizer** text field (mandatory; labeled "Neutralizer").
    - Informational text: informs the user that these parameters will seed the item's standing configuration and be submitted to the Section Head for review, while testing continues immediately.
    - Action button: **Start Testing** (disabled until Diluent and Neutralizer are entered, and disabled if the sample is assigned to another analyst).

[SCREENSHOT SS-142]

#### How to use it
1. In the workspace, select the sample and click **Prepare Sample** or **Start Preparation** from the **Sample Needs Preparation** banner.
2. In the **Preparation** dialog:
   - If an existing configuration is displayed, review the configured technique, diluent, neutralizer, and volumes.
   - If the manual form is displayed, enter the **Sample Amount**, choose the **Technique** ("Pour Plate" or "Filtration"), enter filtration and washing volumes if filtering, and enter the **Diluent** and **Neutralizer**.
3. Click **Confirm & Start Testing** (or **Start Testing**).
4. In the electronic signature prompt, review the signature meaning: "I confirm the preparation steps entered above are the steps performed for this sample, and are correct for this item."
5. Enter your user password and click the confirmation button to apply the electronic signature.
6. Verify the confirmation toast: "{n} sample(s) prepared and signed."

#### System behavior
- Single preparation check:
  - The system verifies that preparation has not already been confirmed for this sample.
- Ownership check:
  - If an analyst is already assigned to the sample's non-superseded test orders, the server verifies that the preparing user matches the assigned analyst.
- Seeding standing configuration:
  - When manual preparation parameters are entered for the first time, the system saves them as the standing preparation configuration for that item with approval status **PendingReview**.
- Test assignment:
  - The user who electronically signs preparation is automatically assigned as the responsible analyst on every test order of the sample that remains in **Waiting** step.
- Preparation status update:
  - Preparation status advances from **Needs Preparation** to **Ready**.
- Audit trail:
  - The system records an electronic signature row with meaning `PreparationConfirmed`, capturing user identity, role, timestamp, diluent, and neutralizer.

#### Validation
The system enforces the following exact validation rules:
- "This sample has already been prepared."
- "This sample is assigned to {name}. Only the assigned analyst may perform sample preparation, unless reassigned by an authorized Section Head."
- "Diluent is required."
- "Neutralizer is required."
- "Filtration technique requires both Filtration Volume and Washing Volume."
- "This sample has no Item and cannot use the preparation configuration flow."
- "This item has no preparation configuration to confirm."

> **Note for reviewers:** In the user interface preparation form, Section Head and System Administrator roles appear exempt from the assigned analyst ownership check, but the server strictly enforces that only the assigned analyst may confirm preparation, requiring reassignment first. (GAP-219)

> **Note for reviewers:** The server-side preparation parameter validator does not enforce validation of the sample amount. (GAP-228)

#### Status
- Sample preparation status transitions from **NeedsPreparation** to **Ready**.
- The test orders remain in workflow step **Waiting** and status **Pending**, with the preparing user assigned as analyst.
- Overall sample status remains **Received**.

#### Important rules
- Testing cannot begin until sample preparation is confirmed. Attempting to start incubation before preparation is refused: "Test Preparation must be completed and confirmed for sample {ref} before incubation can be started for step \"{step}\"."
- The refusal is recorded in the audit trail as `TestStartRefused` with the reason "GMP Refusal: Test preparation stage must be confirmed for sample {ref} before testing can start."

#### Errors
- "Could not start testing."
- "Password verification failed. The signature was not applied."

#### What happens next
The preparation banner disappears, and assigned test orders are unlocked for media selection and incubation setup.


### 14.3 Preparing Environmental Monitoring, Water, and After Cleaning Samples
<!-- WF-PREP-001 -->
#### Purpose
Define monitoring test locations, rooms, sampling points, machine parts, or water storage conditions to generate laboratory test orders for Environmental Monitoring, Water, and After Cleaning samples.

#### Who can use it
Any signed-in user with access to the laboratory section (by default: Analyst, Reviewer, Section Head, System Administrator).

#### Before you start
- The sample must be in **Received** status with preparation status **Needs Preparation**.
- Master data configurations must exist: rooms and room test configurations for EM departments; sampling points and sampling configurations for Water departments; machine parts and test configurations for After Cleaning machines.

#### What you see
- Dialog title: **Preparation**
- Form layout by category:
  - **Environmental Monitoring Form** (`EMPreparationForm`):
    - Status text: "Loading rooms..." while retrieving department rooms.
    - Room cards displaying room name and subtitle "Grade {x}". Rooms without configured tests are disabled.
    - Checkbox selection per room.
    - Action button: **Start Testing**.
  - **Water Preparation Form** (`WaterPreparationForm`):
    - Mandatory dropdown: **Storage Condition \*** (options: "Refrigerator", "Room Temperature"; placeholder "Select storage condition").
    - Mandatory numeric input: **Storage Time (hours) \*** (displayed only when "Refrigerator" is selected).
    - Status text: "Loading sampling points..." while retrieving sampling points.
    - Sampling point selection grid. Points with no assigned tests are disabled.
    - Action button: **Start Testing** (disabled until storage condition and required storage time are completed).
  - **After Cleaning Form** (`AfterCleaningPreparationForm`):
    - Status text: "Loading machine parts..."
    - Machine part cards with subtitle "Machine Part". Parts without configured tests are disabled.
    - Checkbox selection per machine part.
    - Action button: **Start Testing**.
- Shared selection grid controls:
  - **Select All** checkbox.
  - Status chip: "{n} selected".
  - Status badges: **Assigned Tests** or **No tests configured**.

[SCREENSHOT SS-143]

#### How to use it
1. In the workspace, select the monitoring sample and click **Prepare Sample** or **Start Preparation**.
2. Depending on the sample category:
   - For **Environmental Monitoring**: tick the checkboxes for each room monitored during sampling.
   - For **Water**: select the **Storage Condition \*** ("Refrigerator" or "Room Temperature"). If refrigerated, enter the **Storage Time (hours) \***. Then select the monitored sampling points.
   - For **After Cleaning**: tick the checkboxes for each monitored machine part.
3. Click **Start Testing**.
4. Confirm completion when the dialog closes.

#### System behavior
- Test order and location generation:
  - The system creates one test order per distinct test code across all selected rooms, sampling points, or machine parts in status **Pending** and workflow step **Waiting**.
  - Creates one sample location record per selected room configuration, machine part configuration, or sampling point test configuration.
- Analyst assignment:
  - The preparing user is automatically assigned as `AssignedAnalystId` on every test order generated during preparation.
- Impact on segregation of duties:
  - Because the preparing user becomes the assigned analyst, that user is recorded as having performed the tests and cannot subsequently perform technical review or release approval on the sample.
- Preparation status:
  - Sample preparation status transitions to **Ready**.
- Electronic signature:
  - Preparation for Environmental Monitoring, Water, and After Cleaning samples does not require an electronic signature or password.

#### Validation
The system enforces the following exact validation rules:
- Environmental Monitoring:
  - "At least one room must be selected."
  - "Room test configuration(s) not found: {ids}."
  - "Room(s) {names} do not belong to this sample's department."
- Water:
  - "At least one sampling point must be selected."
  - "Storage condition is required for water samples."
  - "Storage time is required when a water sample was refrigerated."
  - "Sampling point(s) not found: {ids}."
  - "Sampling point(s) {codes} do not belong to this sample's department."
- After Cleaning:
  - "At least one machine part must be selected."
  - "Machine part configuration(s) not found: {ids}."
  - "Part(s) {names} do not belong to this sample's machine."
- Duplicate preparation:
  - "This sample has already been prepared."

> **Note for reviewers:** Sample preparation for Environmental Monitoring, Water, and After Cleaning is not signed, has no ownership check, and the preparer automatically becomes the assigned analyst on all created test orders. The receive and prepare endpoints require sign-in only with no permission code check. (GAP-212, GAP-227)

> **Note for reviewers:** After Cleaning preparation in the code creates one test order per distinct test code across the selected machine parts, rather than a collective pathogen order and individual TAMC orders per part as described in earlier specifications. (GAP-225)

#### Status
- Sample preparation status advances to **Ready**.
- Newly generated test orders are placed in workflow step **Waiting** and status **Pending**.

#### Errors
- "Could not prepare sample."

#### What happens next
The generated test orders appear in the **Assigned Tests** section and are ready for media selection and incubation initiation.


### 14.4 Grouped Sample Preparation
<!-- WF-PREP-001 -->
#### Purpose
Confirm and electronically sign sample preparation for multiple Product, Raw Material, or Packaging Material samples sharing identical item preparation configurations in a single batch operation.

#### Who can use it
Any signed-in user with section access who is the assigned analyst on the selected samples (or any authorized analyst if samples are unassigned).

#### Before you start
- Select two or more samples in the workspace register.
- The selected samples must share identical item preparation configurations and have preparation status **Needs Preparation**.

#### What you see
- Embedded Grouped Actions Panel:
  - Section header: **Test Preparation Required (N)** with explanatory note "Must complete before incubation".
  - Grouped preparation row (`GroupedPreparationRow`):
    - Title: "Test Preparation · {item}"
    - Chips: "× N Sample(s)", **Approved** or **Pending Approval**.
    - Expanded details: "Samples to prepare (N):" listing included sample references.
    - Informational text if standing configuration is pending Section Head review.
    - Action button: **Confirm & Start Testing (N Samples)**.
  - Excluded sample warnings: "{n} test(s) excluded from grouped actions" with specific exclusion reasons.

#### How to use it
1. In the workspace register, check the checkboxes next to two or more samples sharing the same item.
2. In the Grouped Actions Panel, expand the **Test Preparation Required** section.
3. Review the grouped item preparation row and the list of included samples.
4. Click **Confirm & Start Testing (N Samples)**.
5. In the electronic signature prompt, enter your user password to apply the signature across all eligible samples.
6. Verify the confirmation message: "{n} sample(s) prepared and signed."

#### System behavior
- Ownership verification:
  - Samples assigned to another analyst are automatically excluded from the grouped action with the reason: "Assigned to {name} - only the assigned analyst may prepare this sample."
- Electronic signatures:
  - The system applies one distinct electronic signature record (`PreparationConfirmed`) per sample in the database using the single entered password.
- Configuration pinning:
  - If the standing configuration was modified after the panel was loaded, the action is refused: "This item's preparation configuration changed after the group was loaded. Reload the grouped action and review the steps before confirming."
- Status update:
  - Preparation status advances to **Ready** for each confirmed sample.
  - The signing user is assigned as analyst on all open test orders.

#### Validation
The system enforces the following exact validation rules:
- "No samples were selected for grouped preparation."
- "The preparation configuration for this group no longer exists."
- "This item's preparation configuration changed after the group was loaded. Reload the grouped action and review the steps before confirming."

#### Status
- Preparation status for all included samples advances to **Ready**.

#### Errors
- "Failed to execute batch action."

#### What happens next
The prepared samples unlock compatible incubation actions in the **Compatible Actions** section of the Grouped Actions Panel.


### 14.5 Grouped Incubation Setup
<!-- WF-TESTING-001 -->
#### Purpose
Assign a released media lot and an eligible incubator to start incubation for compatible test orders across multiple prepared samples simultaneously.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head).

#### Before you start
- Two or more samples must be selected in the workspace register.
- Selected test orders must be at the same workflow step (e.g., initial setup, selective plating, or stage 2 transfer).
- Test preparation must be completed and confirmed for all included samples.
- An active, released, unexpired media lot matching the test step requirements must be available.
- An operational incubator with an active calibration and calibrated set point matching the medium's required temperature range must be available.

#### What you see
- In the Grouped Actions Panel:
  - Section header: **Compatible Actions (N)** with note "Shared operational workflow transition".
  - If preparation is still outstanding: alert banner **Incubation Setup Locked** with message "No incubation can start on a sample whose Test Preparation is still outstanding. Confirm the preparation above and the compatible setup actions appear here."
  - If tests require manual result entry: section **Tests Awaiting Individual Result Entry (N):** with message "No grouped workflow action is available. The selected tests require individual result entry. Result entry must be completed separately for each test."
  - Compatible group rows (`GroupedActionRow`):
    - Chip: "× N Sample(s)"
    - Transition state: **Ready: Setup**, **Ready: Plating**, or **Ready: Transfer**.
    - Expanded sample list: "Selected Samples (N Tests):"
    - Media selection dropdown: **Media Lot (...)** (displays "No released, active lots available" when none qualify).
    - Incubator selection dropdown: **Incubator** or **Transfer to Incubator** (displays "No eligible incubator in range" when none qualify).
    - Action buttons: **Cancel**, **Start Incubation (N Tests)**, **Transfer & Start Incubation (N Tests)** (selective plating), or **Transfer to Incubator (N Tests)** (stage 2 transfer). Displays "Processing..." while submitting.

[SCREENSHOT SS-144]

#### How to use it
1. In the workspace register, check the checkboxes next to two or more samples whose tests are ready for incubation.
2. In the Grouped Actions Panel, expand the relevant group under **Compatible Actions**.
3. In the **Media Lot** dropdown, select an active, released, unexpired media lot.
4. In the **Incubator** dropdown, select an eligible incubator whose set point temperature falls within the required range.
5. Click **Start Incubation (N Tests)** (or **Transfer & Start Incubation (N Tests)** / **Transfer to Incubator (N Tests)**).
6. Verify the confirmation message: "{n} test(s) started in incubation." (or "{n} test(s) started in incubation. ({k} skipped)").

#### System behavior
- Grouped incubation execution:
  - The system processes each test order individually through the standard incubation engine.
- Analyst role scoping:
  - For users holding the Analyst role, the action skips any test order assigned to a different analyst with the refusal: "Test order is assigned to another analyst."
- Media validation:
  - The system verifies that the selected media lot is released, not expired, and belongs to the active laboratory section's material stock.
- Incubator eligibility:
  - An incubator is eligible only if its equipment type is Incubator, its calibrated set point temperature falls within the step medium's required temperature range (`TempMin` to `TempMax`), and its calibration due date is not overdue.
- Shared TSB propagation:
  - If a test order uses shared TSB broth, starting incubation automatically propagates the shared TSB incubation to sibling pathogen tests on the same sample.
- Electronic signature:
  - Grouped incubation setup does not require an electronic signature or password.

#### Validation
The system enforces the following exact validation rules:
- Pre-execution validation:
  - "Media lot must be selected for this workflow transition."
  - "Media lot \"{lot}\" is not released for use, out of stock, or rejected."
  - "Media lot \"{lot}\" is expired (expired on {date})."
  - "Media lot \"{lot}\" was prepared from another laboratory section's material and cannot be used for this test."
  - "Incubator \"{code}\" has no calibrated set point temperature."
  - "Incubator \"{code}\" calibration expired on {date}."
  - "The selected incubator's set point is outside this medium's temperature range."
  - "Grouped actions cannot be executed against analytical result-entry operations. Result entry must be performed individually."
- Individual skip reasons:
  - "Test order is assigned to another analyst."
  - "Test Preparation is not complete for this sample."
  - "Sample preparation is not complete (locations/swabs not verified)."
  - "Test order has completed all workflow steps."
  - "Test order requires individual colony count entry. Result entry cannot be grouped."
  - "Test order requires individual selective plate observation. Observations cannot be grouped."
  - "Test order requires individual confirmatory readout. Observations cannot be grouped."
  - "Biochemical test entry must be completed individually."

> **Note for reviewers:** Media release and expiry checks differ between single-test and grouped workflows; grouped actions verify both release status and expiry dates on the server, whereas single-test media selection checks release status but does not check lot expiry dates. (GAP-220)

#### Status
- Started test orders transition from workflow step **Waiting** to **Incubating**, and status advances from **Pending** to **InProgress**.
- Sample status moves from **Received** to **Under Testing**.

#### Errors
- "No tests were started. {reasons}"
- "{n} of {m} started. {k} refused - {reasons}"
- "Failed to execute batch action."

#### What happens next
Test plates remain in incubation until the required minimum incubation duration elapses, after which they appear under the **Ready to Read** workspace filter.


### 14.6 Overriding Minimum Incubation Duration (Skip Wait)
<!-- WF-TESTING-002 -->
#### Purpose
Enable an authorized laboratory supervisor to override the mandatory incubation time gate when urgent operational intervention is required, allowing plate counts or observations to be recorded before the configured minimum duration has elapsed.

#### Who can use it
Requires the *Supervise test workflows* permission (by default: Section Head, System Administrator).

#### Before you start
An incubation window must be actively open on the test order, and the minimum duration must not yet have elapsed.

#### What you see
- In the test workflow dialog (`TestWorkflowDialog`) or pathogen incubation waiting view (`IncubationWaitingView`):
  - Active incubation countdown showing elapsed time and expected completion timestamp.
  - Action button: **Skip Wait** (visible only to users holding supervisory permissions).
- Once overridden:
  - Notification banner: "Minimum wait time was skipped by a Section Head/System Administrator."
  - Result entry action buttons (such as **Record Result** or **Record Observation**) become immediately active.

#### How to use it
1. Open the test workflow dialog for the incubating test order.
2. In the incubation status section, click **Skip Wait**.
3. Verify that the incubation status updates to show: "Minimum wait time was skipped by a Section Head/System Administrator."
4. Proceed to record test observations or plate counts.

#### System behavior
- Time gate bypass:
  - The system records the overriding user identifier and timestamp on the open incubation record (`MinimumDurationOverriddenAt`).
  - Bypasses subsequent elapsed-time checks for that specific incubation window across broth, selective plating, confirmatory plating, stage 1, stage 2, and batch result paths.
- Parameters unchanged:
  - Configured incubation hours, target temperatures, and incubator set points remain unaltered in the audit history.
- Authorization and accountability:
  - The action requires supervisory permissions (`TestWorkflow.Supervise`). It does not prompt for an electronic signature, password, or explanatory reason.

#### Validation
The system enforces the following exact validation rule:
- "No incubation window is currently open."

> **Note for reviewers:** The Skip Wait supervisory override bypasses the minimum incubation duration without requiring an explanatory reason, password, or electronic signature. (GAP-226)

#### Status
The test order remains in workflow step **Incubating** with the minimum duration gate bypassed, enabling immediate transition to result entry.

#### Errors
- "Could not skip the wait."
- "No incubation window is currently open."

#### What happens next
The analyst opens the test workflow dialog to record plate counts or qualitative observations.


### 14.7 Item Preparation Configuration and Approval
<!-- WF-PREP-002 -->
#### Purpose
Establish and formally approve standard standing preparation parameters (technique, diluent, neutralizer, and volumes) for an item, ensuring consistent sample preparation across routine testing.

#### Who can use it
Requires the *Manage items* permission (by default: Section Head, System Administrator).

#### Before you start
The item must exist and be active in the Item Master.

#### What you see
- Configuration parameters for the item:
  - **Sample Amount**
  - **Technique** ("Pour Plate" or "Filtration")
  - **Filtration Volume (ml)** and **Washing Volume (ml)** (for Filtration)
  - **Diluent**
  - **Neutralizer**
- Approval status chip: **Pending Approval** or **Approved**.

#### System behavior
- Seeding and editing:
  - When an analyst confirms manual preparation on a new item, or when a user saves preparation configuration parameters, the system creates or updates the configuration with approval status **PendingReview**.
  - Any edit to an existing configuration automatically clears previous approval details and resets the status to **PendingReview**.
- Standing availability:
  - Analysts can immediately use an unapproved configuration to prepare samples; testing is not held up awaiting Section Head approval.
- Approval action:
  - A Section Head or System Administrator approves the configuration. The system records the approver's user identifier and timestamp, setting the status to **Approved**.
  - Approval does not require a password or electronic signature.

#### Validation
The system enforces the following exact validation rules:
- "Diluent is required."
- "Neutralizer is required."
- "Filtration technique requires both Filtration Volume and Washing Volume."
- "Item {id} not found."
- "Item {id} has no preparation configuration to approve."
- "This preparation configuration is already approved."

> **Note for reviewers:** The dedicated screen and pending queue for item preparation configuration approval have not been located in the user interface, and the system does not enforce segregation of duties between the analyst who seeds a configuration and the Section Head who approves it. (GAP-214, BR-PREP-010)

#### Status
- Configuration approval status transitions from **PendingReview** to **Approved**.

#### What happens next
Subsequent samples received for this item automatically load the approved configuration in the **Confirm Preparation Form**.
