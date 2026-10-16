## 24. Water Monitoring

MicroLIMS manages quality control monitoring for pharmaceutical purified water (PW), water for injection (WFI), and utility water systems. It supports configuring water systems, sample points, test assignments, and per-point count test limits, while tracking routine operational workflows including sample receipt, cold storage tracking during preparation, incubation, plate count result entry in location grids, and multi-tier limit evaluation.

MicroLIMS does not have automated water sampling schedules or a sampling plan engine.

---

### 24.1 Water Configuration
<!-- WF-WATER-001 -->
#### Purpose
Configure pharmaceutical water systems, sample locations (sampling points), assign routine test codes, and establish alert, action, and specification limits with reporting units for count tests.

#### Who can use it
Requires the *Manage laboratory master data* permission (`MasterData.Manage`, by default: Section Head, System Administrator).

#### Before you start
- Ensure water count tests (such as TAMC-Water) and presence/absence tests are defined in the Test Master (§31). Count tests must have workflow type **Count Test** to support limit configurations.

> **Note for reviewers:** Water, EM and After Cleaning configuration changes are recorded in the audit trail but need no reason and no signature. (GAP-404)

> **Note for reviewers:** Limits are free text; a mistyped limit is accepted and, if unreadable, is treated as no limit. (GAP-400)

> **Note for reviewers:** Limit edits made after a sample is prepared but before results are entered apply to that sample. (GAP-402)

> **Note for reviewers:** Water department testing frequency is not stored on the water system in code, but is entered on the sample location. (GAP-415, BR-WATER-007)

> **Note for reviewers:** The water configuration panel does not enforce duplicate checks or mandatory reporting units on count test limits on the server. (GAP-401)

> **Note for reviewers:** Some deletes fail with "An unexpected error occurred." when the record is in use. (GAP-420)

#### What you see
- On the navigation sidebar, under **LABORATORIES — Microbiology Configuration**, click **Water** (`/laboratory-configuration/water`).
- Page header:
  - Title: **Water**
  - Subtitle: "Water systems, their sample locations, assigned tests and per-location limits."
  - Action buttons: **Add water system** and **Add sample location** (disabled until at least one system exists).
- Summary KPI tiles: **Water systems**, **Sample locations**, **Missing limits**, and **No tests assigned**.
- Filter controls: **All ({n})** and **Needs attention ({n})** (highlights points with missing limits or no assigned tests).
- Left panel: Search field **Find a system or location** and list of water systems.
- Right workspace (when a system is selected):
  - System header: System name, button **Rename**, and delete icon ("Delete water system").
  - Table of sample locations: Point Code, Point Name, Testing Frequency, Assigned Tests, limits summary, status chips (**Limits not set**), and row actions (**Edit location**, **Delete location**, **Show limits** / **Hide limits**).

[SCREENSHOT SS-241]

#### How to use it
To add a water system:
1. Click **Add water system**.
2. In the dialog, enter the system **Name \*** (e.g., "Purified Water Loop 1").
3. Click **Add water system**. The system displays: `"{name}" added.`

To configure a sample location:
1. Click **Add sample location** (or click the edit icon on an existing location row).
2. Under section **1 · Location details**:
   - Enter the **Point code \*** (e.g., "PW-01").
   - Select the parent **Water system \***.
   - Enter the **Point name** (e.g., "Production Loop Return Tap").
   - Select the informational **Testing frequency** (Daily, Weekly, Fortnightly, Monthly, Quarterly, Annually).
   - In the **Assigned tests** multi-select picker, choose all tests to be performed when this location is sampled.
3. Under section **3 · Limits for count tests**:
   - The dialog automatically generates one limit row per assigned test having workflow type **Count Test**.
   - For each count test, enter values for **Alert**, **Action**, and **Specification** limits.
   - Enter the reporting **Unit** (defaults to placeholder `e.g. CFU/mL`).
4. Click **Save changes** (or **Add sample location**).
5. The system saves the location, saves the individual limit rows, and displays: `Sample location {code} updated.`

[SCREENSHOT SS-242]

#### System behavior
- Changes to water systems, locations, and limit rows are recorded in the generic audit trail with previous and new values. No electronic signature or password is required.
- The UI helper notes that assigned tests are ordered when a sample is received; however, in application code, test orders and locations are instantiated upon sample preparation.
- Limit rows are created only for assigned tests configured as Count Test in the Test Master. Presence/absence tests carry no numeric limit rows.
- When saving a location, if a limit row fails validation, the system reports: `"The location was saved, but some limits were not. {code}: {message}"`.
- Limits are read and linked to sample locations during preparation; edits made to limits before result entry apply to that sample.

#### Validation
The system validates required entries and deletion rules:
- "Name is required."
- "Point code and water system are required."
- `Cannot delete '{name}' - it still has {n} sample location(s). Delete those first.`
- `Cannot delete '{code}' - it is referenced by {n} sample(s) and {m} sampling configuration(s).`
- Concurrency conflict: "This record was changed by someone else while you were working on it. Nothing was saved - reload it and make your change again."

#### Status
- Water systems and sampling locations do not carry operational active/inactive status fields.
- Locations lacking configured limit rows display the chip **Limits not set**.

#### Important rules
- A water system cannot be deleted while sample locations exist within it.
- A sample location cannot be deleted while referenced by samples or sampling configuration limit rows.
- Always configure limits before preparing samples from that location; a sample prepared from a location without limit rows will record status `LimitsNotConfigured` at result entry.

#### Errors
- "Could not load the water configuration."
- "Could not save this sample location."
- "Could not delete this water system."
- "Could not delete this sample location."
- Deleting a location actively referenced by batch sample records is refused by database constraints with: "An unexpected error occurred."

#### What happens next
- Configured water systems and sampling points become available for sample receiving and preparation (§24.2, §24.3).

---

### 24.2 Receiving Water Samples
<!-- WF-SAMPLE-007 -->
#### Purpose
Register a water sampling event in the laboratory sample register, recording the sampled water system, sampling personnel, collection volume, and collection timestamp.

#### Who can use it
Any signed-in user with sample receiving privileges (by default: Section Head, Reviewer, Analyst, System Administrator).

#### Before you start
- The target water system must exist in the Water configuration (§24.1).

#### What you see
- On the navigation sidebar, under **RECEIVING**, click **Receive Sample** (`/receiving`) and click **Receive** (or click **+ New Sample** from the Microbiology Workspace).
- Dialog: **New Sample**:
  - Category selection: click **Water**.
  - Multi-sample entry grid columns: **Department \*** (water system), **Cause of Testing \***, **Sampled By \***, **Quantity**, and **Control No.**

#### How to use it
1. In the **New Sample** dialog, click the **Water** tile and click **Next**.
2. In the entry grid row:
   - Select the water system under **Department \***.
   - Select the **Cause of Testing \***.
   - Enter **Sampled By \***.
   - Enter the sample **Quantity** (e.g., "500 mL").
   - Enter the **Control No.**
3. Click **Save All Samples**.
4. The system registers the sample and displays: `Successfully received 1 sample(s). Test orders generated automatically.`

#### System behavior
- Creates the water sample with status **Received** and preparation status **NeedsPreparation**.
- Does not create test orders or location records at receiving time.
- Assigns the sample to the Microbiology Laboratory section.

#### Validation
- "Department is required"
- "Cause of Testing is required"
- "Sampled By is required"
- `Water department {id} not found.`

#### Status
- Sample Status: **Received** (0).
- Preparation Status: **NeedsPreparation** (0).

#### What happens next
- The sample appears in the Microbiology Workspace register with banner **Sample Needs Preparation** for storage tracking and point selection (§24.3).

---

### 24.3 Water Sample Preparation and Storage Tracking
<!-- WF-SAMPLE-007 -->
#### Purpose
Record sample storage conditions (refrigeration duration or room temperature), select the sampled water sampling points, instantiate laboratory test orders, and link count test limits.

#### Who can use it
Any authorized user assigned to the Microbiology Laboratory section (by default: Section Head, Reviewer, Analyst, System Administrator).

#### Before you start
- The sample must be in **Received** status with preparation status **NeedsPreparation**.
- Sampling points with assigned tests must exist in the sample's water department.

#### What you see
- In the **Microbiology Workspace** (`/microbiology/workspace`), select the water sample and click **Prepare Sample**.
- Dialog: **Preparation** (Water preparation form):
  - **Storage Condition \*** dropdown: **Refrigerator** or **Room Temperature**.
  - **Storage Time (hours) \*** numeric field (displayed and mandatory when Refrigerator is selected).
  - Sampling point grid: list of sampling points in the water system showing point code, point name, and assigned test chips. Checkboxes to select points (points with no assigned tests are disabled).
  - Button **Start Testing** (disabled until storage details and at least one point are selected).

[SCREENSHOT SS-243]

#### How to use it
1. In the **Preparation** dialog, select the **Storage Condition \*** (**Refrigerator** or **Room Temperature**).
2. If **Refrigerator** was selected, enter the **Storage Time (hours) \***.
3. In the sampling point grid, select the checkbox for each sampling point sampled.
4. Click **Start Testing**.
5. The system records storage metadata, generates test orders and sampling locations, links configured limits, updates preparation status to **Ready**, and closes the dialog.

#### System behavior
- Validates that refrigerated samples record storage hours.
- Creates exactly one laboratory test order per distinct test code across all selected sampling points.
- Sets test order status to **Pending** and workflow step to **Waiting**, assigned to the preparing user.
- Creates one sample location record per sampling point and assigned test code combination.
- For each location belonging to a count test, links the location to the point's `SamplingConfiguration` limit row. If no limit row exists, the location is created with no limit link.
- Sets sample preparation status to **Ready** (1).
- Water sample preparation is unsigned and does not require an electronic signature or password.

#### Validation
The system validates storage parameters and point selections:
- "Storage condition is required for water samples."
- "Storage time is required when a water sample was refrigerated."
- "At least one sampling point must be selected."
- `Sampling point(s) not found: {ids}.`
- `Sampling point(s) {codes} do not belong to this sample's department.`
- "This sample has already been prepared."

#### Status
- Sample Status: remains **Received** (0).
- Preparation Status: transitions to **Ready** (1).
- Test Order Status: **Pending** (0), step **Waiting**.

#### Important rules
- If a sample is refrigerated, storage hours must be recorded.
- Water storage condition and time can be corrected later prior to review submission via **Edit Details** (§12, signed).

#### What happens next
- Analysts start incubation workflows for each generated water test order (§24.4).

---

### 24.4 Water Testing and Incubation
<!-- WF-SAMPLE-007 -->
#### Purpose
Assign released media lots, select calibrated incubators, and initiate incubation for water plate count tests.

#### Who can use it
Requires the *Execute test workflows* permission (`TestWorkflow.Execute`, by default: Section Head, Reviewer, Analyst, System Administrator) within the Microbiology section.

#### Before you start
- Water sample preparation must be **Ready**.
- Released, unexpired culture media lots and calibrated incubators must be available.

#### What you see
- In the **Microbiology Workspace**, select the water sample and click **Open Workflow** on the assigned test.
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
- "Media lot \"{lot}\" is not released for use, out of stock, or rejected."
- "The selected incubator's set point is outside this medium's temperature range."
- "This incubation window needs at least {h} hours - not ready until {utc} UTC."

#### Status
- Test Order Status: **InProgress** (1), step **Incubating**.
- Sample Status: **InTesting** (1, **Under Testing**).

#### What happens next
- Once incubation finishes, the analyst enters plate readings across all sampled water points (§24.5).

---

### 24.5 Water Result Entry and Limit Evaluation
<!-- WF-SAMPLE-007, WF-COUNT-002 -->
#### Purpose
Record colony counts across all water sampling points in a location result grid, calculate rounded averages, evaluate results against alert, action, and specification limits, and complete the test.

#### Who can use it
Requires the *Execute test workflows* permission (`TestWorkflow.Execute`, by default: Section Head, Reviewer, Analyst, System Administrator) within the Microbiology section.

#### Before you start
- The water test order must be in its final incubation window with the minimum incubation duration elapsed.

#### What you see
- In the workspace test card, click **Record Results**.
- Dialog: **{testCode} Results: {displayName}** (`WaterLocationResultGridDialog`):
  - Table columns:
    - **Sampling Point**: point code and name.
    - **Limits (Alert / Action / Spec)**: configured threshold values.
    - **Plate Readings**: numeric colony count inputs per sampling point, with button **Add Plate**.
    - **Average**: live calculated average and unit.
    - **Status**: live compliance status badge (**Within Limits**, **Alert Limit Exceeded**, **Action Limit Exceeded**, **Out of Specification**, or **Limits Not Configured**).
  - Summary footer: "{conform}/{total} locations within spec; worst status: {badge}".
  - Button **Submit Results**.

[SCREENSHOT SS-244]

#### How to use it
1. In the **{testCode} Results** dialog, enter the colony count for each sampling point in the **Plate Readings** field.
2. If multiple plates were poured for a point, click **Add Plate** and enter the additional count.
3. Verify that the calculated average and compliance badge accurately reflect the entered counts.
4. Confirm that all sampling points have at least one plate reading.
5. Click **Submit Results**.

#### System behavior
- Calculates the arithmetic mean of all entered plate readings for each sampling point.
- Dilution factor is not applied to water samples (direct count, dilution factor = 1).
- Reported result formatting: rounds the average to the nearest whole number using midpoint rounding away from zero (`Math.Round(average, AwayFromZero)`). Unlike environmental monitoring, water has no `"<1"` threshold rule; an average below 0.5 reports `"0"`.
- Compares the point average against linked alert, action, and specification limits in strict hierarchy:
  1. Checks **Specification**: if exceeded, assigns **OutOfSpecification**.
  2. Checks **Action**: if exceeded, assigns **ActionLimitExceeded**.
  3. Checks **Alert**: if exceeded, assigns **AlertLimitExceeded**.
  4. If no limits were linked or parsed, assigns **LimitsNotConfigured**.
  5. If all limits are met, assigns **WithinLimits**.
- Derives overall test summary: `"{n} locations: {conform} conform, {rest} alert/action/spec (worst: {status})"`.
- Closes the incubation, sets test order workflow step to **Ready**, and sets status to **ResultEntered**.
- When all test orders for the laboratory section reach **Ready**, the section automatically advances to **UnderReview** (displayed as **Pending Review**).
- Result entry does not prompt for an electronic signature or password.

#### Validation
The system validates complete entry across all sampling points:
- "No locations are assigned to this test order."
- "Results are missing for: {names}."
- "At least one plate reading is required for: {names}."
- "\"{step}\" is not the final incubation window yet - close it and start the next window first."
- "This incubation window needs at least {h} hours - not ready until {utc} UTC."
- Attempting to submit batch-prepared water samples via legacy single-result endpoints returns: "This water test was prepared across multiple sampling points; per-location result entry is not available yet."

#### Status
- Location Status: **WithinLimits**, **AlertLimitExceeded**, **ActionLimitExceeded**, **OutOfSpecification**, or **LimitsNotConfigured**.
- Test Order Status: **ResultEntered** (2), step **Ready**.
- Sample Status: automatically moves to **UnderReview** (2, **Pending Review**) once all tests in the section reach **Ready**.

#### Important rules
- All sampling points must be submitted together; partial saves are rejected.
- If a sampling point exceeds specification, the overall test status displays Out of Specification, signaling Section Heads during sample review.
- Result entry does not require an electronic signature.

#### Errors
- "Could not record batch readings."

#### What happens next
- An independent Reviewer conducts secondary technical verification in the review queue (§17).
- If results exceed specifications, the Section Head initiates an out-of-specification retest and investigation chain (§19, §20).
