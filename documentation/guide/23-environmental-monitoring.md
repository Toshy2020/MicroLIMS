## 23. Environmental Monitoring

MicroLIMS supports environmental monitoring (EM) samples. An administrator sets up departments, rooms with a cleanroom grade, and the tests (with alert, action and specification limits) run in each room. Analysts then receive an EM sample, choose the rooms that were sampled, incubate, enter plate counts for every room in one grid, and send the sample to review. The system compares each room's result with its limits.

MicroLIMS does not have an EM annual plan or automated sampling schedules.

---

### 23.1 Environmental Monitoring Configuration
<!-- WF-EM-001 -->
#### Purpose
Define EM departments, rooms with a grade (A to D) and the tests run in each room, with alert, action and specification limits and a reporting unit.

#### Who can use it
Requires the *Master Data Management* permission (by default: Section Head, System Administrator).

#### Before you start
- Make sure the test codes you need exist in the Test Master (§31).
- Have the room grades and limits from your facility procedures ready.

> **Note for reviewers:** Environmental Monitoring, Water and After Cleaning configuration changes are recorded in the audit trail but need no reason and no electronic signature. (GAP-404)

> **Note for reviewers:** Limits are free text. A mistyped limit is accepted and, if the system cannot read it, it is treated as no limit. (GAP-400)

> **Note for reviewers:** If you change a limit after a sample is prepared but before its results are entered, the changed limit applies to that sample. (GAP-402)

> **Note for reviewers:** A unit typed on a room test replaces the standard unit for the results of that test. (GAP-403)

> **Note for reviewers:** Some deletes fail with the message "An unexpected error occurred." when the record is already in use. (GAP-420)

> **Note for reviewers:** A test added through **+ Add "{code}" to Test Master** in the test picker is created at once as an Observation test with no workflow steps. (GAP-416)

Testing frequency is only a label. It does not schedule or remind anything.

#### What you see
- In the menu, open **LABORATORIES**, then **Microbiology Configuration**, then **Environmental Monitoring**.
- Title **Environmental Monitoring**; subtitle "Departments, their rooms and grades, and per-room test limits."; buttons **Add department** and **Add room**.
- Tiles: **Departments**, **Rooms**, **Tests configured**, **Rooms without tests**.
- Left: a search box **Find a department or room** and the department list. Right: "Select a department" until you pick one; then **Edit department**, a delete icon, and the room cards.
- A room card shows its grade, the count of tests (for example "2 tests"), the button **Add first test** or **Add test**, and icons to edit or delete the room and each test. A room with no tests shows **No tests configured**.

[SCREENSHOT SS-231]

#### How to use it
To add a department:
1. Click **Add department**.
2. Enter the **Name \***. Optionally enter the **Class** (for example Grade C) and a **Testing frequency** (Daily, Weekly, Fortnightly, Monthly, Quarterly, Annually).
3. Click **Add department**. The system shows `"{name}" added.`

To add a room:
1. Click **Add room**, or **Add room to {department}**.
2. Enter the **Room name \***, choose the **Department \*** and the **Grade** (**Grade A** to **Grade D**; a new room starts at Grade A).
3. Save. The system shows `Room "{name}" added.`

To add a room test:
1. On the room card, click **Add first test** or **Add test**.
2. In **1 · What is sampled**, choose the **Test type** (**Passive air sample**, **Active air sample**, **Surface sample**, **Compressed air**, **Drains**) and the **Test**. Test options are shown as "{code}: {display name}".
3. In **2 · Limits**, enter **Alert**, **Action** and **Specification** limits (leave a limit blank if it does not apply) and the **Unit**. The unit is required for Active air sample, Compressed air and Drains.
4. Click **Add test**. The system shows `Test added to {room}.`

[SCREENSHOT SS-232]

To change or remove something, use **Edit department**, the room and test icons, and confirm the deletion question (for example `Remove the {type} / {code} test from {room}?`).

#### System behavior
- Limit text that the system understands: a plain number or "NMT x" is an upper limit; "NLT x" is a lower limit; "a-b" is a range; "target ± tolerance" is a range around a target. A result equal to a limit is not over it.
- While you type, the dialog warns (without blocking the save) when numbers are out of order: "Alert limit is higher than the action limit.", "Action limit is higher than the specification.", "Alert limit is higher than the specification." followed by "Check the values before saving."
- Passive air and Surface tests may have no unit. If the unit is blank, the system uses "CFU/plate/4 hours" (passive air) or "CFU/25 cm2" (surface) at result entry. A typed unit is used instead.
- The system does not stop you from adding the same room, test type and test twice.
- Changes are recorded in the audit trail with previous and new values.

#### Validation
- "Name is required."
- "Room name and department are required."
- "Choose the test to run."
- "Unit is required for {type label}."
- `Cannot delete '{name}' - it still has {n} room(s). Delete those rooms first.`
- `Cannot delete '{name}' - it still has {n} test configuration(s). Delete those first.`
- `Cannot delete '{name}' - it has {n} monitoring record(s) in its history.`
- "This record was changed by someone else while you were working on it. Nothing was saved - reload it and make your change again."

#### Status
Departments, rooms and room tests have no status.

#### Important rules
- A department cannot be deleted while it has rooms. A room cannot be deleted while it has tests or monitoring history.
- Changes need no password or signature.

#### Errors
- "Could not load the environmental monitoring configuration."
- "Could not save this test."
- "An unexpected error occurred." when a delete is refused because the record is in use.

#### What happens next
Rooms that have at least one test can be chosen when an EM sample is prepared (§23.3).

---

### 23.2 Receiving Environmental Monitoring Samples
<!-- WF-SAMPLE-006 -->
#### Purpose
Register an incoming EM sampling event.

#### Who can use it
Any signed-in user. The **+ New Sample** button in the Microbiology workspace is shown to every member of the laboratory, and the server checks neither a receiving permission nor the laboratory. On the **Receive Sample** page, the **Receive** button is shown to users who hold a receiving permission. (GAP-227)

#### Before you start
The department must exist in the Environmental Monitoring configuration (§23.1).

> **Note for reviewers:** The server does not check a permission or laboratory section when an EM sample is received or prepared. (GAP-227)

#### What you see
- Open **RECEIVING**, then **Receive Sample**, and click **Receive**. Or click **+ New Sample** in the Microbiology workspace.
- The dialog **New Sample** shows category tiles; choose **Environmental Monitoring**, then **Next**.
- The entry grid has **Department \***, **Cause of Testing \***, **Sampled By \*** and **Control No.** There is no quantity for EM.

#### How to use it
1. Click the **Environmental Monitoring** tile and **Next**.
2. Choose the **Department**, the **Cause of Testing**, and enter **Sampled By** (and **Control No.** if you have one).
3. Click **Save All Samples**.
4. The system shows "Successfully received 1 sample(s). Test orders generated automatically." For an EM sample the tests are in fact created at preparation (§23.3), not at receiving.

#### System behavior
- The system creates the sample as **Received** and waiting for preparation. It creates no tests and no room records yet.

#### Validation
- "Department is required"
- "Cause of Testing is required"
- "Sampled By is required"
- "Department {id} not found."

#### Status
The sample is **Received** and shows the banner **Sample Needs Preparation**.

#### What happens next
Prepare the sample (§23.3).

---

### 23.3 Environmental Monitoring Sample Preparation
<!-- WF-SAMPLE-006 -->
#### Purpose
Choose the rooms that were sampled. The system then creates the tests and one location per room test.

#### Who can use it
Any signed-in user (the system does not check the laboratory). Preparation of an EM sample needs no password or signature.

#### Before you start
- The sample must be **Received** and need preparation.
- The sampled rooms must have at least one test (§23.1).

#### What you see
- In the Microbiology workspace, select the EM sample. Click **Prepare Sample** or **Start Preparation** in the banner (which reads "Test locations and configuration must be completed before starting laboratory tests.").
- The dialog **Preparation** shows the department's rooms as cards with the grade, a count of assigned tests, and a checkbox. Rooms with no tests are disabled. Buttons: **Select All** and **Start Testing**.

[SCREENSHOT SS-233]

#### How to use it
1. Tick every room that was sampled (or click **Select All**).
2. Click **Start Testing**.

#### System behavior
- The system creates one test for each distinct test code across the chosen rooms. Each starts as Pending, waiting for incubation.
- The person who prepared the sample is recorded as the assigned analyst on all these tests. For review and approval, that person counts as having performed the tests.
- The system creates one location for each room test and links it to that room test, so the limits are read from it at result entry.
- Preparation is marked complete.

#### Validation
- "At least one room must be selected."
- "Room test configuration(s) not found: {ids}."
- "Room(s) {names} do not belong to this sample's department."
- "This sample has already been prepared."

#### Errors
- "Could not prepare sample."

#### What happens next
Analysts start incubation for each test (§23.4).

---

### 23.4 Environmental Monitoring Testing and Incubation
<!-- WF-SAMPLE-006 -->
#### Purpose
Choose the media lot and incubator and start the incubation of each EM test.

#### Who can use it
Requires the *Test Workflow Execution* permission (by default: Section Head, Reviewer, Analyst, System Administrator). **Skip Wait** needs the supervisor permission (by default: Section Head, System Administrator).

#### Before you start
- The sample must be prepared. **Open Workflow** is disabled until preparation is complete.
- A released media lot and an incubator whose set point is inside the medium's temperature range must exist.

> **Note for reviewers:** The incubator check reads the Laboratory Configuration equipment record (set point and calibration due date), not the Equipment Inventory status or date. The two records are linked by code only. (GAP-501, GAP-517)

> **Note for reviewers:** An incubator refused because its calibration date has passed is reported with the message "The selected incubator's set point is outside this medium's temperature range." (GAP-501)

> **Note for reviewers:** Whether a media lot can be used when it is unreleased or expired depends on the screen path; see the table in §22.4. (GAP-500, GAP-516)

#### What you see
- In the Microbiology workspace, select the EM sample and find the test under **Assigned Tests**. Click **Open Workflow**. The dialog is titled "{testCode} Workflow".
- The lists **Media batch** (options "{lotNumber}, expires {date}") and **Stage 1 Incubator** (options "{name} ({code}), {setPoint}°C"), the required temperature and incubation period, and the button **Start Incubation**.
- If no incubator is in range the dialog warns "No incubator is set to {min}-{max} °C for this step."

#### How to use it
1. Choose the **Media batch**.
2. Choose the **Stage 1 Incubator**.
3. Click **Start Incubation**.
4. Wait for the required time. The dialog shows "Incubation in progress", the temperature, duration and "Expected reading: {datetime}".
5. If the test has more than one incubation window, click **Advance to Next Incubation Window** after the minimum time, then choose media again for the next window.
6. A user with the supervisor permission can click **Skip Wait** to read earlier. The system asks for no reason or password.

#### System behavior
- The test changes from Pending to **Incubating** and the sample changes from **Received** to **Under Testing** at the first incubation start.
- Results cannot be entered before the minimum incubation time has passed, unless **Skip Wait** was used.

#### Validation
- "Test Preparation must be completed and confirmed for sample {ref} before incubation can be started for step \"{step}\"."
- "Preparation not complete - no locations assigned to this test."
- "Media lot \"{lot}\" is not released for use, out of stock, or rejected."
- "This step requires a different medium. The selected lot is {name}."
- "The selected incubator's set point is outside this medium's temperature range."
- "This incubation window needs at least {h} hours - not ready until {utc} UTC."

#### Status
Test: Pending, then **Incubating**. Sample: **Received**, then **Under Testing**.

#### What happens next
After the final window, enter the plate readings (§23.5).

---

### 23.5 Environmental Monitoring Result Entry and Limit Evaluation
<!-- WF-SAMPLE-006, WF-COUNT-002 -->
#### Purpose
Enter the colony counts (or growth observations for a pathogen test) for all rooms in one grid, see each room's status against its limits, and send the test to review.

#### Who can use it
Requires the *Test Workflow Execution* permission (by default: Section Head, Reviewer, Analyst, System Administrator).

#### Before you start
The final incubation window must be open and its minimum time elapsed (or skipped by a supervisor).

> **Note for reviewers:** The out-of-trend rule in the design document (sum of the two incubation steps compared with the action limit) is not implemented. (GAP-211)

> **Note for reviewers:** EM, Water and After Cleaning preparation is not signed, and the preparer counts as having performed the tests. (GAP-212)

#### What you see
- In the test card, click **Record Results**. For count tests the dialog "{testCode} Results: {displayName}" has the columns **Location**, **Limits (Alert / Action / Spec)**, **Plate Readings** (with **Add Plate**), **Calculated Result** and **Status**, a summary "{conform}/{total} locations within spec; worst status: {badge}", and **Submit Results**.
- For pathogen tests the dialog has **Location**, **Growth Observed?** (**Yes** / **No**) and **Status** (**Detected** / **Absent**), the summary "{detected}/{total} locations detected; overall: {Detected|Absent}", and **Submit Results**.
- Until the window is ready the dialog warns "Results cannot be submitted before {datetime}."

[SCREENSHOT SS-234]

#### How to use it
Count tests:
1. Enter the colony count for each location in **Plate Readings**. Click **Add Plate** if more than one plate was used.
2. Check the calculated result and the status of each location.
3. Make sure every location has at least one reading.
4. Click **Submit Results**.

Pathogen tests:
1. For each location choose **Yes** or **No** under **Growth Observed?**.
2. Click **Submit Results**.

#### System behavior
- For count tests the result of a location is the average of its readings. If the average is below 1 it is reported as "<1"; otherwise it is rounded to a whole number (a half rounds up).
- The status uses the average (not the rounded value), in this order: if the specification is exceeded the status is **Out of Specification**; else if the action limit is exceeded, **Action Limit Exceeded**; else if the alert limit is exceeded, **Alert Limit Exceeded**; if none of the three limits can be read, **Limits Not Configured**; otherwise **Within Limits**. A value equal to a limit is not exceeded.
- The limits and unit come from the room test linked to the location at the time you submit.
- The test result reads "{n} locations: {x} conform, {y} alert/action/spec (worst: {status})". Only **Within Limits** counts as conform.
- For pathogen tests the overall result is **Detected** if any location shows growth, otherwise **Absent**.
- The incubation is closed and the test becomes ready for review. When all tests of the laboratory are ready, the sample moves to **Pending Review**.
- No password or signature is asked.

#### Validation
- "No locations are assigned to this test order."
- "Results are missing for: {names}."
- "At least one plate reading is required for: {names}."
- "\"{step}\" is not the final incubation window yet - close it and start the next window first."
- "This incubation window needs at least {h} hours - not ready until {utc} UTC."
- "Media must be selected for this test before batch results can be recorded."
- "A growth observation is required for every location."

#### Status
- Location: **Within Limits**, **Alert Limit Exceeded**, **Action Limit Exceeded**, **Out of Specification**, **Limits Not Configured** (count); **Detected** or **Absent** (pathogen).
- Sample: **Pending Review** when all tests are ready.

#### Important rules
- All locations are submitted together. The grid is accepted or refused as a whole.
- A result over a limit is shown in the status and the summary; the system does not start anything else by itself.

#### Errors
- "Could not record batch results."
- "Could not record batch pathogen results."

#### What happens next
A reviewer checks the results (§17). Return, retest and approval follow the normal sample path (§18, §19).

<!-- Review log (Sonnet): 29 corrections; invented items removed: route paths, permission/enum/status names and numeric codes, "calibrated/in-service incubator" list claims, unreferenced Annex/compliance wording, "Dilution factor" and internal rounding names, section-assignment claim at receipt, OOS retest/investigation claim, internal test-type error message; callouts added: GAP-227, GAP-212, GAP-501/GAP-517, calibration message, GAP-500/GAP-516 -->
<!-- Phase 13 (agy): applied F-A09, R-NEW-01 -->

<!-- Phase 13 review (Sonnet): F-A09, R-NEW-01 verified; GAP-227 exists. -->
