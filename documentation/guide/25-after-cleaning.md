## 25. After Cleaning Monitoring

MicroLIMS manages cleaning verification and hygiene monitoring across manufacturing process equipment. It supports configuring processing machines, machine parts, swab and rinse count test limits, and specific pathogen detection tests. It governs the entire operational lifecycle from sample receiving with previous product batch traceability and machine part preparation to incubation, location grid result entry, and automated limit evaluation.

MicroLIMS does not implement the collective pathogen sampling or per-part TAMC workflow described in earlier specifications; instead, each distinct test code generates one standard test order across all selected machine parts.

---

### 25.1 After Cleaning Configuration
<!-- WF-AC-001 -->
#### Purpose
Configure manufacturing processing machines, identify critical machine parts, establish swab and rinse count test limits with reporting units, and assign required pathogen presence/absence tests per part.

#### Who can use it
Requires the *Manage laboratory master data* permission (`MasterData.Manage`, by default: Section Head, System Administrator).

#### Before you start
- Ensure test codes for swab, rinse, and pathogen testing are configured in the Test Master (§31).
- Identify cleanroom processing machines and their specific product-contact or non-contact parts.

> **Note for reviewers:** Water, EM and After Cleaning configuration changes are recorded in the audit trail but need no reason and no signature. (GAP-404)

> **Note for reviewers:** Limits are free text; a mistyped limit is accepted and, if unreadable, is treated as no limit. (GAP-400)

> **Note for reviewers:** Limit edits made after a sample is prepared but before results are entered apply to that sample. (GAP-402)

> **Note for reviewers:** A typed unit replaces the standard unit for swab and rinse results. (GAP-403)

> **Note for reviewers:** The collective pathogen / per-part TAMC design for After Cleaning is not implemented in the application code. (GAP-225, BR-AC-007)

> **Note for reviewers:** Some deletes fail with "An unexpected error occurred." when the record is in use. (GAP-420)

> **Note for reviewers:** A test added through "+ Add to Test Master" in a test picker becomes an Observation test with no workflow steps. (GAP-416)

#### What you see
- On the navigation sidebar, under **LABORATORIES — Microbiology Configuration**, click **After Cleaning** (`/laboratory-configuration/after-cleaning`).
- Page header:
  - Title: **After Cleaning**
  - Subtitle: "Machines, their parts, and the swab, rinse and pathogen tests run on each part."
  - Action buttons: **Add machine** and **Add machine part** (disabled until at least one machine exists).
- Left panel: Search field for machines and list of configured machines.
- Right workspace (when a machine is selected):
  - Machine header: Machine name, button **Rename**, and delete icon ("Delete machine").
  - Cards per machine part:
    - Part header: Part name, actions (**Rename part**, **Delete part**).
    - **Count tests** section: displays configured swab and rinse tests with alert, action, and specification limits; action buttons **Add swab / rinse test**, and row icons **Edit test** and **Remove test**.
    - **Pathogen tests** section: displays assigned pathogen chips; action button **Add pathogen tests** or **Edit pathogen tests**.

[SCREENSHOT SS-251]

#### How to use it
To add a machine:
1. Click **Add machine**.
2. In the dialog, enter the **Machine name \*** (e.g., "Filling Line 2").
3. Click **Add machine**. The system displays: `"{name}" added.`

To add a machine part:
1. Click **Add machine part** (or **Add part to {machine}**).
2. Enter the **Part name \*** (e.g., "Filling Needles").
3. Select the parent **Machine \***.
4. Click **Add part**. The system displays: `Part "{name}" added.`

To configure a swab or rinse count test on a part:
1. On the target part card, under **Count tests**, click **Add swab / rinse test**.
2. Under section **1 · What is sampled**:
   - Select the **Sample type \***: **Swab** (standard unit hint: `CFU/25 cm2`) or **Rinse** (standard unit hint: `CFU/mL`).
   - Select the **Test \*** code from the Test Master dropdown.
3. Under section **2 · Limits**:
   - Enter numeric or threshold strings for **Alert**, **Action**, and **Specification** limits. Leave non-applicable limits blank.
   - Enter the reporting **Unit** (defaults to placeholder `CFU/25 cm2` or `CFU/mL`).
4. Click **Add test**. The system displays: `Test added to {part}.`

To assign pathogen tests to a part:
1. On the target part card, under **Pathogen tests**, click **Add pathogen tests** (or **Edit pathogen tests**).
2. In the multi-select dropdown, check all required pathogen tests (e.g., *E. coli*, *Salmonella*).
3. Click **Save pathogen tests**. The system updates the assigned pathogen tests and displays: `Pathogen tests updated for {part}.`

[SCREENSHOT SS-252]

#### System behavior
- Configuration writes (machines, parts, and part test configurations) are recorded in the generic audit trail with previous and new values. No electronic signature or password is required.
- The same test type and test code cannot be configured twice on the same machine part.
- Pathogen tests are saved with sample type `"Pathogen"`, flag `IsPathogenTest = true`, and blank limits and units.
- At result entry time, if the unit was left blank, the system automatically assigns `"CFU/25 cm2"` for Swab and `"CFU/mL"` for Rinse. If a unit is explicitly typed in configuration, that typed string overrides the default.
- If a machine part test configuration is edited after a sample is prepared but before results are submitted, the updated limits apply to that sample.

#### Validation
The system validates required entries and structural constraints:
- "Name is required."
- "Part name and machine are required."
- "Choose the test to run."
- `{testCode} ({testType}) is already configured for this part.`
- `Cannot delete '{name}' - it still has {n} part(s). Delete those first.`
- `Cannot delete '{name}' - it has been used to receive {n} sample(s).`
- `Cannot delete '{name}' - it still has {n} test configuration(s). Delete those first.`
- `{testCode} has already been tested on this part ({n} sample location(s)), so it cannot be removed.`
- Concurrency conflict: "This record was changed by someone else while you were working on it. Nothing was saved - reload it and make your change again."

#### Status
- Machines, parts, and test configurations carry no operational active/inactive status fields.
- Part cards without tests display the chip **No tests configured**.

#### Important rules
- A machine cannot be deleted while parts exist or if it has been used to receive samples.
- A machine part cannot be deleted while it has test configurations.
- A test configuration that has already been tested on a part cannot be removed.

#### Errors
- "Could not load the after-cleaning configuration."
- "Could not save this test."
- "Could not delete this record."
- If removing a test that has active sample locations, the system refuses: `"{testCode} has already been tested on this part ({n} sample location(s)), so it cannot be removed."`

#### What happens next
- Configured machines, parts, and test suites become available for sample receiving and preparation in laboratory workspaces (§25.2, §25.3).

---

### 25.2 Receiving After Cleaning Samples
<!-- WF-SAMPLE-008 -->
#### Purpose
Register an after-cleaning sampling event in the sample register, capturing the cleaned machine, previous manufactured product, previous product batch number, sampling personnel, and collection metadata.

#### Who can use it
Any signed-in user with sample receiving privileges (by default: Section Head, Reviewer, Analyst, System Administrator).

#### Before you start
- The cleaned processing machine must be configured in After Cleaning configuration (§25.1).
- The previous manufactured product name and batch number must be available.

#### What you see
- On the navigation sidebar, under **RECEIVING**, click **Receive Sample** (`/receiving`) and click **Receive** (or click **+ New Sample** from the Microbiology Workspace).
- Dialog: **New Sample**:
  - Category selection: click **After Cleaning**.
  - Multi-sample entry grid columns: **Machine \***, **Previous Product \***, **Previous Batch No. \***, **Cause of Testing \***, **Sampled By \***, and **Control No.** (Quantity is hidden for After Cleaning samples).

#### How to use it
1. In the **New Sample** dialog, click the **After Cleaning** tile and click **Next**.
2. In the entry grid row:
   - Select the cleaned **Machine \*** from the dropdown.
   - Enter the **Previous Product \*** name.
   - Enter the **Previous Batch No. \*** (mandatory; recorded as the sample's primary batch number).
   - Select the **Cause of Testing \***.
   - Enter **Sampled By \***.
   - Enter the **Control No.**
3. Click **Save All Samples**.
4. The system registers the sample and displays: `Successfully received 1 sample(s). Test orders generated automatically.`

#### System behavior
- Creates an after-cleaning sample record with status **Received** and preparation status **NeedsPreparation**.
- Automatically copies the entered **Previous Batch No.** to the sample's primary `BatchNumber` field for traceability.
- Does not create test orders or location records at receiving time; all test orders and sampling locations are generated during sample preparation.
- Assigns the sample to the Microbiology Laboratory section.

#### Validation
The system validates required receiving metadata:
- "Machine is required"
- "Previous Product is required"
- "Previous Product Batch No. is required"
- "Cause of Testing is required"
- "Sampled By is required"
- `Machine {id} not found.`

#### Status
- Sample Status: **Received** (0).
- Preparation Status: **NeedsPreparation** (0).

#### What happens next
- The sample appears in the Microbiology Workspace register with banner **Sample Needs Preparation** for machine part selection (§25.3).

---

### 25.3 After Cleaning Sample Preparation
<!-- WF-SAMPLE-008 -->
#### Purpose
Select the specific machine parts swabbed or rinsed during the cleaning verification session, generating distinct laboratory test orders and per-part sampling locations.

#### Who can use it
Any authorized user assigned to the Microbiology Laboratory section (by default: Section Head, Reviewer, Analyst, System Administrator).

#### Before you start
- The sample must be in **Received** status with preparation status **NeedsPreparation**.
- Machine parts must have active test configurations on the sampled machine.

#### What you see
- In the **Microbiology Workspace** (`/microbiology/workspace`), select the After Cleaning sample and click **Prepare Sample**.
- Dialog: **Preparation** (Machine part selection grid):
  - Header: Machine name and subtitle "Machine Part".
  - Cards per machine part showing part name and configured test chips.
  - Checkboxes to select sampled machine parts.
  - Button **Start Testing**.

[SCREENSHOT SS-253]

#### How to use it
1. In the **Preparation** dialog, review the available parts for the machine.
2. Select the checkbox for each machine part swabbed or rinsed.
3. Click **Start Testing**.
4. The system creates test orders and sampling locations, updates preparation status to **Ready**, and closes the dialog.

#### System behavior
- Creates exactly one laboratory test order per distinct test code across all selected machine parts.
- Sets test order status to **Pending** and workflow step to **Waiting**, assigned to the preparing user.
- Creates one sample location record per machine-part test configuration in the selected parts, permanently linking it to the `MachinePartConfiguration`.
- Sets sample preparation status to **Ready** (1).
- After Cleaning sample preparation is unsigned and does not require an electronic signature or password.

#### Validation
- "At least one machine part must be selected."
- `Machine part configuration(s) not found: {ids}.`
- `Part(s) {names} do not belong to this sample's machine.`
- "This sample has already been prepared."

#### Status
- Sample Status: remains **Received** (0).
- Preparation Status: transitions to **Ready** (1).
- Test Order Status: **Pending** (0), step **Waiting**.

#### What happens next
- Analysts start incubation workflows for each generated After Cleaning test order (§25.4).

---

### 25.4 After Cleaning Testing and Incubation
<!-- WF-SAMPLE-008 -->
#### Purpose
Assign released media lots, select calibrated incubators, and initiate incubation for swab, rinse, or pathogen test orders.

#### Who can use it
Requires the *Execute test workflows* permission (`TestWorkflow.Execute`, by default: Section Head, Reviewer, Analyst, System Administrator) within the Microbiology section.

#### Before you start
- After Cleaning sample preparation must be **Ready**.
- Released, unexpired culture media lots and calibrated incubators must be available.

#### What you see
- In the **Microbiology Workspace**, select the sample and click **Open Workflow** on the assigned test.
- Dialog: `{testCode} Workflow`:
  - **Media batch** dropdown (lists released, unexpired media lots).
  - **Stage 1 Incubator** dropdown (lists in-service incubators within temperature range).
  - Button **Start Incubation**.

#### How to use it
1. In the `{testCode} Workflow` dialog, select the **Media batch** lot.
2. Select the **Stage 1 Incubator**.
3. Click **Start Incubation**.
4. The system logs the incubation start time, transitions the test order to **Incubating**, and updates the sample status to **Under Testing**.
5. Wait for the required incubation duration.

#### System behavior
- Test order transitions to step **Incubating** (status **InProgress**).
- Sample status moves from **Received** to **InTesting** (displayed as **Under Testing**).
- Enforces incubation duration time gates: results cannot be entered before the minimum incubation duration elapses.

#### Validation
- "Test Preparation must be completed and confirmed for sample {ref} before incubation can be started for step \"{step}\"."
- "Preparation not complete - no locations assigned to this test."
- "Media lot \"{lot}\" is not released for use, out of stock, or rejected."
- "The selected incubator's set point is outside this medium's temperature range."
- "This incubation window needs at least {h} hours - not ready until {utc} UTC."

#### Status
- Test Order Status: **InProgress** (1), step **Incubating**.
- Sample Status: **InTesting** (1, **Under Testing**).

#### What happens next
- Once incubation finishes, the analyst enters swab/rinse plate counts or pathogen growth observations (§25.5).

---

### 25.5 After Cleaning Result Entry and Limit Evaluation
<!-- WF-SAMPLE-008, WF-COUNT-002 -->
#### Purpose
Record colony counts for swab and rinse tests or growth observations for pathogen tests across all machine parts in a location result grid, evaluate results against alert, action, and specification limits, and complete the test.

#### Who can use it
Requires the *Execute test workflows* permission (`TestWorkflow.Execute`, by default: Section Head, Reviewer, Analyst, System Administrator) within the Microbiology section.

#### Before you start
- The test order must be in its final incubation window with the minimum incubation duration elapsed.

#### What you see
- In the workspace test card, click **Record Results**.
- For count tests: dialog **{testCode} Results: {displayName}** (`LocationResultGridDialog`):
  - Table columns:
    - **Location**: machine part name and sample type (Swab or Rinse).
    - **Limits (Alert / Action / Spec)**: configured threshold values.
    - **Plate Readings**: numeric colony count inputs per machine part, with button **Add Plate**.
    - **Calculated Result**: live average and reporting unit (`CFU/25 cm2` for Swab, `CFU/mL` for Rinse).
    - **Status**: live compliance status badge (**Within Limits**, **Alert Limit Exceeded**, **Action Limit Exceeded**, **Out of Specification**, or **Limits Not Configured**).
  - Summary footer: "{conform}/{total} locations within spec; worst status: {badge}".
  - Button **Submit Results**.
- For pathogen tests: dialog **{testCode} Results: {displayName}** (`PathogenLocationResultGridDialog`):
  - Table columns: **Location**, **Growth Observed?** (radio buttons **Yes** / **No**), and **Status** (**Detected** / **Absent**).
  - Summary footer: "{detected}/{total} locations detected; overall: {Detected|Absent}".
  - Button **Submit Results**.

[SCREENSHOT SS-254]

#### How to use it
To record swab or rinse count results:
1. In the **{testCode} Results** dialog, enter the colony count for each machine part in the **Plate Readings** field.
2. If multiple plates were inoculated for a part, click **Add Plate** and enter the additional count.
3. Verify that the calculated average, reporting unit, and status badge accurately reflect the entered counts.
4. Confirm that all machine parts have at least one plate reading.
5. Click **Submit Results**.

To record pathogen results:
1. In the pathogen results dialog, select **Yes** or **No** under **Growth Observed?** for each machine part.
2. Confirm the overall detection summary.
3. Click **Submit Results**.

#### System behavior
- For count tests:
  - Dilution factor is fixed at 1 for After Cleaning samples.
  - Calculates the arithmetic mean of all entered plate readings for each machine part.
  - Formats reported result: displays `"<1"` if average is less than 1; otherwise rounds to the nearest whole number (`AwayFromZero`).
  - Reads alert, action, and specification limits from the linked machine part configuration.
  - Compares average in strict hierarchy:
    1. Checks **Specification**: if exceeded, assigns **OutOfSpecification**.
    2. Checks **Action**: if exceeded, assigns **ActionLimitExceeded**.
    3. Checks **Alert**: if exceeded, assigns **AlertLimitExceeded**.
    4. If no limits were configured, assigns **LimitsNotConfigured**.
    5. If all limits are satisfied, assigns **WithinLimits**.
  - Derives overall test summary: `"{n} locations: {conform} conform, {rest} alert/action/spec (worst: {status})"`.
- For pathogen tests: assigns **Detected** if growth is observed on any machine part; otherwise assigns **Absent**.
- Closes the incubation, sets test order workflow step to **Ready**, and sets status to **ResultEntered**.
- When all test orders for the laboratory section reach **Ready**, the section automatically advances to **UnderReview** (displayed as **Pending Review**).
- Result entry does not prompt for an electronic signature or password.

#### Validation
The system validates complete entry across all machine parts:
- "No locations are assigned to this test order."
- "Results are missing for: {names}."
- "At least one plate reading is required for: {names}."
- "\"{step}\" is not the final incubation window yet - close it and start the next window first."
- "This incubation window needs at least {h} hours - not ready until {utc} UTC."
- "A growth observation is required for every location."

#### Status
- Location Status: **WithinLimits**, **AlertLimitExceeded**, **ActionLimitExceeded**, **OutOfSpecification**, or **LimitsNotConfigured** (count); **Detected** or **Absent** (pathogen).
- Test Order Status: **ResultEntered** (2), step **Ready**.
- Sample Status: automatically advances to **UnderReview** (2, **Pending Review**) once all tests in the section reach **Ready**.

#### Important rules
- All machine parts must be submitted together; partial saves are rejected.
- If a machine part exceeds specification, the overall test status reflects Out of Specification, alerting reviewers and Section Heads during review.
- Result entry does not require an electronic signature.

#### Errors
- "Could not record batch results."
- "Could not record batch pathogen results."

#### What happens next
- An independent Reviewer conducts secondary technical verification in the review queue (§17).
- If results exceed specifications, the Section Head initiates an out-of-specification retest and investigation chain (§19, §20).
