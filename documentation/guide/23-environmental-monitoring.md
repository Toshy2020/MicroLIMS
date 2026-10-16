## 23. Environmental Monitoring

MicroLIMS manages environmental monitoring (EM) across cleanroom facilities, supporting the hierarchical configuration of departments, rooms, cleanroom grades, test types, and alert, action, and specification limits. It governs end-to-end operational sample flows from sample receiving and multi-room preparation to incubation, plate reading entry in location grids, automated limit evaluation, and technical review submission.

MicroLIMS does not have an EM annual plan or automated sampling schedules.

---

### 23.1 Environmental Monitoring Configuration
<!-- WF-EM-001 -->
#### Purpose
Define manufacturing departments, cleanroom rooms with air grades (Grade A through D), and specific room test configurations, specifying sample types, test codes, alert limits, action limits, specification limits, and reporting units.

#### Who can use it
Requires the *Manage laboratory master data* permission (`MasterData.Manage`, by default: Section Head, System Administrator).

#### Before you start
- Verify that required microbiological test codes exist in the Test Master (§31).
- Prepare room classifications and limit specifications according to facility environmental monitoring procedures.

> **Note for reviewers:** Water, EM and After Cleaning configuration changes are recorded in the audit trail but need no reason and no signature. (GAP-404)

> **Note for reviewers:** Limits are free text; a mistyped limit is accepted and, if unreadable, is treated as no limit. (GAP-400)

> **Note for reviewers:** Limit edits made after a sample is prepared but before results are entered apply to that sample. (GAP-402)

> **Note for reviewers:** A typed unit replaces the standard unit for passive air and surface sample results. (GAP-403)

> **Note for reviewers:** Testing frequency is a label only; it schedules nothing. (SCR-EM-001)

> **Note for reviewers:** Some deletes fail with "An unexpected error occurred." when the record is in use. (GAP-420)

> **Note for reviewers:** A test added through "+ Add to Test Master" in a test picker becomes an Observation test with no workflow steps. (GAP-416)

#### What you see
- On the navigation sidebar, under **LABORATORIES — Microbiology Configuration**, click **Environmental Monitoring** (`/laboratory-configuration/environmental-monitoring`).
- Page header:
  - Title: **Environmental Monitoring**
  - Subtitle: "Departments, their rooms and grades, and per-room test limits."
  - Action buttons: **Add department** and **Add room**.
- Summary KPI tiles: **Departments**, **Rooms**, **Tests configured**, and **Rooms without tests**.
- Left panel: Search field **Find a department or room** and the department list.
- Right workspace (when a department is selected):
  - Department header: Name, Class, and actions **Edit department** and delete icon ("Delete department").
  - Room cards listing configured room tests, count of configured tests, button **Add room to {department}**, and per-room actions (**Edit room**, **Delete room**, **Add test**).
  - Room test rows: test type, test code, alert/action/spec limits, unit, and actions (**Edit test**, **Delete test**).

[SCREENSHOT SS-231]

#### How to use it
To add an environmental monitoring department:
1. Click **Add department**.
2. In the dialog, enter the department **Name \***.
3. Optionally enter the cleanroom **Class** (e.g., "Grade C") and select a **Testing frequency** (Daily, Weekly, Fortnightly, Monthly, Quarterly, Annually).
4. Click **Add department**. The system displays: `"{name}" added.`

To add a cleanroom room:
1. Click **Add room** (or **Add room to {department}**).
2. Enter the **Room name \***.
3. Select the parent **Department \***.
4. Select the cleanroom **Grade \*** (**Grade A**, **Grade B**, **Grade C**, or **Grade D**; defaults to Grade A).
5. Click **Add room**. The system displays: `Room "{name}" added.`

To configure a room test:
1. On the target room card, click **Add test** (or **Add first test**).
2. Under section **1 · What is sampled**:
   - Select the **Test type \***: **Passive air sample**, **Active air sample**, **Surface sample**, **Compressed air**, or **Drains**.
   - Select the **Test \*** code from the Test Master dropdown.
3. Under section **2 · Limits**:
   - Enter numeric or threshold strings for **Alert**, **Action**, and **Specification** limits. Leave any non-applicable limit blank.
   - Enter the reporting **Unit** (mandatory for Active air sample, Compressed air, and Drains; optional for Passive air and Surface samples).
4. Click **Add test**. The system displays: `Test added to {room}.`

[SCREENSHOT SS-232]

#### System behavior
- Configuration writes (departments, rooms, and test configurations) are recorded in the generic system audit trail with before and after property values.
- Testing frequency is stored purely as informational metadata and does not trigger automated scheduling or reminders.
- Limit values are stored as free text. The system parses them at result entry time (supporting plain numbers, upper bounds like `NMT 10`, lower bounds like `NLT 5`, and ranges).
- For Passive air and Surface samples, if the unit field is left blank, the system automatically assigns `"CFU/plate/4 hours"` (passive air) or `"CFU/25 cm2"` (surface) at result entry. If a unit is explicitly typed, that typed string overrides the standard default.
- If a room test configuration is edited after a sample is prepared but before results are submitted, the updated limits apply to that sample.

#### Validation
The system validates required entries and structural constraints:
- "Name is required."
- "Room name and department are required."
- "Choose the test to run."
- "Unit is required for {type label}."
- `Unknown test type '{t}'. Allowed: PassiveAirSample, SurfaceAirSample, ActiveAirSample, CompressedAir, Drains.`
- `Unit is required for {testType} test configurations.`
- `Cannot delete '{name}' - it still has {n} room(s). Delete those rooms first.`
- `Cannot delete '{name}' - it still has {n} test configuration(s). Delete those first.`
- `Cannot delete '{name}' - it has {n} monitoring record(s) in its history.`
- Concurrency conflict: "This record was changed by someone else while you were working on it. Nothing was saved - reload it and make your change again."
- Advisory limit order warnings (displayed when plain numbers are entered):
  - "Alert limit is higher than the action limit."
  - "Action limit is higher than the specification."
  - "Alert limit is higher than the specification."
  - "Check the values before saving."

#### Status
- Departments, rooms, and test configurations have no active/inactive status field.
- Rooms without tests display the chip **No tests configured**.

#### Important rules
- A department cannot be deleted while rooms exist within it.
- A room cannot be deleted while room test configurations or historical monitoring records exist.
- Configuration changes do not require an electronic signature or password.

#### Errors
- "Could not load the environmental monitoring configuration."
- "Could not save this test."
- If attempting to delete a department or test configuration referenced by active samples, the database rejects the operation with: "An unexpected error occurred."

#### What happens next
- Configured rooms and tests become available for sample receiving and preparation in the laboratory workspaces (§23.2, §23.3).

---

### 23.2 Receiving Environmental Monitoring Samples
<!-- WF-SAMPLE-006 -->
#### Purpose
Register an incoming environmental monitoring sampling event in the sample register, identifying the sampled department, sampling personnel, and collection metadata.

#### Who can use it
Any signed-in user with access to sample receiving (by default: Section Head, Reviewer, Analyst, System Administrator).

#### Before you start
- The target manufacturing department must be configured in Environmental Monitoring configuration (§23.1).

#### What you see
- On the navigation sidebar, under **RECEIVING**, click **Receive Sample** (`/receiving`) and click **Receive** (or click **+ New Sample** from the Microbiology Workspace).
- Dialog: **New Sample**:
  - Category selection tiles: click **Environmental Monitoring**.
  - Multi-sample entry grid columns: **Department \***, **Cause of Testing \***, **Sampled By \***, and **Control No.** (Quantity is hidden for EM samples).

#### How to use it
1. In the **New Sample** dialog, click the **Environmental Monitoring** tile and click **Next**.
2. In the entry grid row:
   - Select the **Department \***.
   - Select the **Cause of Testing \*** (e.g., "Routine Monitoring").
   - Enter **Sampled By \***.
   - Enter the **Control No.**
3. Click **Save All Samples**.
4. The system registers the sample and displays: `Successfully received 1 sample(s). Test orders generated automatically.`

#### System behavior
- Generates a sample record with initial status **Received** and preparation status **NeedsPreparation**.
- Does not create test orders or location records at receiving time; all test orders and sampling locations are generated during sample preparation.
- Assigns the sample to the Microbiology Laboratory section.

#### Validation
The system validates mandatory receiving metadata:
- "Department is required"
- "Cause of Testing is required"
- "Sampled By is required"
- `Department {id} not found.`

#### Status
- Sample Status: **Received** (0).
- Preparation Status: **NeedsPreparation** (0).

#### What happens next
- The sample appears in the Microbiology Workspace register with banner **Sample Needs Preparation** for room selection and preparation (§23.3).

---

### 23.3 Environmental Monitoring Sample Preparation
<!-- WF-SAMPLE-006 -->
#### Purpose
Select the specific cleanroom rooms sampled during the monitoring session, generating distinct laboratory test orders and per-room sampling locations.

#### Who can use it
Any authorized user assigned to the Microbiology Laboratory section (by default: Section Head, Reviewer, Analyst, System Administrator).

#### Before you start
- The sample must be in **Received** status with preparation status **NeedsPreparation**.
- Cleanroom rooms must have active test configurations in the sampled department.

#### What you see
- In the **Microbiology Workspace** (`/microbiology/workspace`), select the EM sample.
- Click **Prepare Sample** (or **Start Preparation** in the workspace banner).
- Dialog: **Preparation** (EM room selection grid):
  - Header: Department name and sampling point grid.
  - Room cards displaying room name, cleanroom grade (**Grade {x}**), and configured test count.
  - Checkboxes for selecting sampled rooms (rooms with no configured tests are disabled).
  - Button **Start Testing** (or **Select All**).

#### How to use it
1. In the **Preparation** dialog, review the available rooms in the department.
2. Select the checkbox for each room sampled during the monitoring session.
3. Click **Start Testing**.
4. The system creates test orders and sampling locations, updates preparation status to **Ready**, and closes the dialog.

#### System behavior
- Creates exactly one laboratory test order per distinct test code across all selected rooms.
- Sets test order status to **Pending** and workflow step to **Waiting**.
- Assigns the preparing user as the assigned analyst on all created test orders.
- Generates one sample location record per room test configuration in the selected rooms, permanently linking it to the room test configuration.
- Sets sample preparation status to **Ready** (1).
- EM sample preparation is unsigned and does not require an electronic signature or password.

#### Validation
- "At least one room must be selected."
- `Room test configuration(s) not found: {ids}.`
- `Room(s) {names} do not belong to this sample's department.`
- "This sample has already been prepared."

#### Status
- Sample Status: remains **Received** (0).
- Preparation Status: transitions to **Ready** (1).
- Test Order Status: **Pending** (0), step **Waiting**.

#### What happens next
- Analysts start incubation workflows for each generated test order (§23.4).

---

### 23.4 Environmental Monitoring Testing and Incubation
<!-- WF-SAMPLE-006 -->
#### Purpose
Assign released culture media lots, select calibrated incubators, and initiate incubation for EM plate count or pathogen test orders.

#### Who can use it
Requires the *Execute test workflows* permission (`TestWorkflow.Execute`, by default: Section Head, Reviewer, Analyst, System Administrator) within the Microbiology section.

#### Before you start
- Sample preparation must be completed (**Ready**).
- Released, unexpired culture media lots matching the test definition must be available.
- Calibrated incubators with set points matching the media temperature range must be operational.

#### What you see
- In the **Microbiology Workspace**, select the EM sample and locate the assigned test in the **Assigned Tests** list.
- Click **Open Workflow** (launches `{testCode} Workflow` dialog):
  - **Media batch** dropdown (lists released, unexpired media lots).
  - **Stage 1 Incubator** dropdown (lists in-service incubators within temperature range).
  - Incubation parameter summary (required temperature range and duration).
  - Button **Start Incubation**.

#### How to use it
1. In the `{testCode} Workflow` dialog, select the **Media batch** lot.
2. Select the **Stage 1 Incubator**.
3. Click **Start Incubation**.
4. The system logs the incubation start time, transitions the test order to **Incubating**, and updates the sample status to **Under Testing**.
5. Wait for the required incubation duration. The dialog displays the remaining time countdown and expected reading time.
   - For multi-window tests: once the minimum incubation duration elapses, click **Advance to Next Incubation Window**, close the window, and select media for the subsequent window.
   - Authorized supervisors holding `TestWorkflow.Supervise` can click **Skip Wait** if early reading is justified.

#### System behavior
- Test order transitions from step **Waiting** to **Incubating** (status **InProgress**).
- Sample status moves from **Received** to **InTesting** (displayed as **Under Testing**) upon the first incubation start.
- Enforces incubation duration time gates: results cannot be entered before the minimum incubation duration elapses.

#### Validation
- "Test Preparation must be completed and confirmed for sample {ref} before incubation can be started for step \"{step}\"."
- "Preparation not complete - no locations assigned to this test."
- "Media lot \"{lot}\" is not released for use, out of stock, or rejected."
- "This step requires a different medium. The selected lot is {name}."
- "The selected incubator's set point is outside this medium's temperature range."
- "This incubation window needs at least {h} hours - not ready until {utc} UTC."

#### Status
- Test Order Status: **InProgress** (1), step **Incubating**.
- Sample Status: **InTesting** (1, **Under Testing**).

#### What happens next
- Once the final incubation window elapses, the analyst enters plate readings across all cleanroom locations (§23.5).

---

### 23.5 Environmental Monitoring Result Entry and Limit Evaluation
<!-- WF-SAMPLE-006, WF-COUNT-002 -->
#### Purpose
Record colony counts or pathogen observations across all cleanroom locations in a location result grid, execute automated multi-tier limit evaluation (Alert, Action, Specification), and submit completed tests for review.

#### Who can use it
Requires the *Execute test workflows* permission (`TestWorkflow.Execute`, by default: Section Head, Reviewer, Analyst, System Administrator) within the Microbiology section.

#### Before you start
- The test order must be in its final incubation window with the minimum incubation duration elapsed.

> **Note for reviewers:** The out-of-trend rule combining multiple incubation steps is not implemented in application code. (GAP-211)

#### What you see
- In the workspace test card, click **Record Results** (or **Record Result**).
- For count tests: dialog **{testCode} Results: {displayName}** (`LocationResultGridDialog`):
  - Table columns:
    - **Location**: cleanroom room and test type.
    - **Limits (Alert / Action / Spec)**: configured threshold values.
    - **Plate Readings**: numeric input fields per location, with button **Add Plate**.
    - **Calculated Result**: live average and unit display.
    - **Status**: live compliance status badge (**Within Limits**, **Alert Limit Exceeded**, **Action Limit Exceeded**, **Out of Specification**, or **Limits Not Configured**).
  - Summary footer: "{conform}/{total} locations within spec; worst status: {badge}".
  - Button **Submit Results**.
- For pathogen tests: dialog **{testCode} Results: {displayName}** (`PathogenLocationResultGridDialog`):
  - Table columns: **Location**, **Growth Observed?** (radio buttons **Yes** / **No**), and **Status** (**Detected** / **Absent**).
  - Summary footer: "{detected}/{total} locations detected; overall: {Detected|Absent}".
  - Button **Submit Results**.

[SCREENSHOT SS-234]

#### How to use it
To record count results:
1. In the **{testCode} Results** dialog, enter the colony count for each room location in the **Plate Readings** field.
2. If multiple plates were inoculated for a location, click **Add Plate** and enter the additional count readings.
3. Verify that the calculated average, reporting unit, and status badge reflect the entered counts.
4. Confirm that all locations have at least one plate reading.
5. Click **Submit Results**.

To record pathogen results:
1. In the pathogen results dialog, select **Yes** or **No** under **Growth Observed?** for each cleanroom location.
2. Confirm the overall detection summary.
3. Click **Submit Results**.

#### System behavior
- For count tests:
  - Dilution factor is fixed at 1 for environmental monitoring.
  - Calculates the arithmetic mean of all entered plate readings per location.
  - Formats reported result: displays `"<1"` if average is less than 1; otherwise rounds to the nearest whole number (`AwayFromZero`).
  - Reads alert, action, and specification limits from the linked room test configuration.
  - Compares average in strict hierarchy:
    1. Checks **Specification**: if exceeded, assigns **OutOfSpecification**.
    2. Checks **Action**: if exceeded, assigns **ActionLimitExceeded**.
    3. Checks **Alert**: if exceeded, assigns **AlertLimitExceeded**.
    4. If no limits parse, assigns **LimitsNotConfigured**.
    5. If all limits are satisfied, assigns **WithinLimits**.
  - Derives overall test result summary: `"{n} locations: {conform} conform, {rest} alert/action/spec (worst: {status})"`.
- For pathogen tests: assigns **Detected** if growth is observed on any location; otherwise assigns **Absent**.
- Closes the open incubation, sets test order workflow step to **Ready**, and sets status to **ResultEntered**.
- When all test orders for the laboratory section reach step **Ready**, the section automatically transitions to **UnderReview** (displayed as **Pending Review**).
- Microbiology result entry does not prompt for an electronic signature or password.

#### Validation
The system validates complete entry across all locations:
- "No locations are assigned to this test order."
- "Results are missing for: {names}."
- "At least one plate reading is required for: {names}."
- "\"{step}\" is not the final incubation window yet - close it and start the next window first."
- "This incubation window needs at least {h} hours - not ready until {utc} UTC."
- "A growth observation is required for every location."
- Warning banner before window end: "Results cannot be submitted before {datetime}."

#### Status
- Location Status: **WithinLimits**, **AlertLimitExceeded**, **ActionLimitExceeded**, **OutOfSpecification**, or **LimitsNotConfigured** (count); **Detected** or **Absent** (pathogen).
- Test Order Status: transitions to **ResultEntered** (2), step **Ready**.
- Sample Status: automatically advances to **UnderReview** (2, **Pending Review**) once all tests in the section reach **Ready**.

#### Important rules
- All locations must be submitted together; partial location saves are not permitted.
- If a reading exceeds specification, the overall test status reflects Out of Specification, alerting reviewers and Section Heads during review.
- Result entry does not require an electronic signature.

#### Errors
- "Could not record batch results."
- "Could not record batch pathogen results."

#### What happens next
- An independent Reviewer conducts secondary technical verification in the review queue (§17).
- If results exceed specifications, the Section Head initiates an out-of-specification retest and investigation chain (§19, §20).
