## 24. Water Monitoring

MicroLIMS supports water monitoring samples. An administrator sets up water systems, sample locations (sampling points), the tests assigned to each location, and alert, action and specification limits for the count tests. Analysts then receive a water sample, record how it was stored, choose the sampled points, incubate, enter plate counts for all points in one grid, and send the sample to review. The system compares the average of each point with its limits.

MicroLIMS does not have automated water sampling schedules or a sampling plan.

---

### 24.1 Water Configuration
<!-- WF-WATER-001 -->
#### Purpose
Define water systems, their sample locations, the tests run at each location, and the limits and unit for count tests.

#### Who can use it
Requires the *Master Data Management* permission (by default: Section Head, System Administrator).

#### Before you start
- Make sure the water tests exist in the Test Master (§31). A test gets limit rows only if its workflow type is **Count Test**.
- Add the limits before a sample is prepared from the location (see Important rules).

> **Note for reviewers:** Environmental Monitoring, Water and After Cleaning configuration changes are recorded in the audit trail but need no reason and no electronic signature. (GAP-404)

> **Note for reviewers:** Limits are free text. A mistyped limit is accepted and, if the system cannot read it, it is treated as no limit. (GAP-400)

> **Note for reviewers:** If you change a limit after a sample is prepared but before its results are entered, the changed limit applies to that sample. (GAP-402)

> **Note for reviewers:** The testing frequency is entered on the sample location, not on the water system, and it is only a label. The design document puts it on the water system. (GAP-415)

> **Note for reviewers:** Water limit rows have no duplicate check and no required unit. (GAP-401)

> **Note for reviewers:** The hint on the assigned tests says tests are ordered when a sample is "received". The tests are in fact created at preparation. (GAP-418)

> **Note for reviewers:** The two design documents for water configuration and the water batch workflow are both marked "Design — awaiting approval". (GAP-423)

> **Note for reviewers:** Some deletes fail with the message "An unexpected error occurred." when the record is in use. (GAP-420)

#### What you see
- In the menu, open **LABORATORIES**, then **Microbiology Configuration**, then **Water**.
- Title **Water**; subtitle "Water systems, their sample locations, assigned tests and per-location limits."; buttons **Add water system** and **Add sample location** (the second is disabled until a water system exists).
- Tiles: **Water systems**, **Sample locations**, **Missing limits**, **No tests assigned**.
- Left: a search box **Find a system or location** and the list of water systems. Right: "Select a water system" until you pick one; then **Rename**, a delete icon, and the filters **All ({n})** and **Needs attention ({n})**.
- Each location row has the icons **Edit location**, **Delete location** and **Show limits** / **Hide limits**. A location with no limit shows the chip **Limits not set**.

[SCREENSHOT SS-241]

#### How to use it
To add a water system:
1. Click **Add water system**.
2. Enter the **Name**.
3. Click **Add water system**. The system shows `"{name}" added.`

To add or edit a sample location:
1. Click **Add sample location**, or the edit icon of a location.
2. In **1 · Location details**, enter the **Point code \***, choose the **Water system \***, and optionally enter the **Point name** and a **Testing frequency**.
3. In **2 · Assigned tests**, choose the tests to run for this location with the **Assigned tests** picker.
4. In **3 · Limits for count tests**, enter **Alert**, **Action**, **Specification** and **Unit** (for example CFU/mL) for each assigned count test. Presence/absence tests need no limits. If no count test is assigned the panel says "Assign a count test (e.g. TAMC-Water) to set limits."
5. Click **Add sample location** or **Save changes**. The system shows `Sample location {code} added.` or `Sample location {code} updated.`

[SCREENSHOT SS-242]

To rename a water system, click **Rename**. To delete a system or location, use the delete icon and confirm the question.

#### System behavior
- The system saves the location first and then the limit rows. A row is created only if at least one of Alert, Action or Specification is filled. A row is changed only if a value changed. A limit row is never deleted from this panel, even if you remove its test from the location.
- If a limit row fails, the system reports "The location was saved, but some limits were not. {code}: {message}"
- Limit text that the system understands: a plain number or "NMT x" is an upper limit; "NLT x" is a lower limit; "a-b" is a range; "target ± tolerance" is a range around a target. A result equal to a limit is not over it.
- While you type, the dialog warns (without blocking the save) when numbers are out of order, for example "Alert limit is higher than the action limit."
- Changes are recorded in the audit trail with previous and new values.

#### Validation
- "Name is required."
- "Point code and water system are required."
- `Cannot delete '{name}' - it still has {n} sample location(s). Delete those first.`
- `Cannot delete '{code}' - it is referenced by {n} sample(s) and {m} sampling configuration(s).`
- "This record was changed by someone else while you were working on it. Nothing was saved - reload it and make your change again."

#### Status
Water systems and locations have no status. A location without any limit shows **Limits not set**, and the **Needs attention** filter shows locations that have missing limits or no assigned tests.

#### Important rules
- A water system cannot be deleted while it has sample locations.
- A sample location cannot be deleted while samples or limit rows refer to it.
- Add the limits before you prepare a sample. A location prepared before its limit row existed has no limits, and its result shows **Limits Not Configured**.
- Changes need no password or signature.

#### Errors
- "Could not load the water configuration."
- "Could not save this sample location."
- "Could not delete this water system."
- "Could not delete this sample location."
- "An unexpected error occurred." when a delete is refused because the record is in use.

#### What happens next
Water systems and locations can be chosen when a water sample is received and prepared (§24.2, §24.3).

---

### 24.2 Receiving Water Samples
<!-- WF-SAMPLE-007 -->
#### Purpose
Register an incoming water sample.

#### Who can use it
Any signed-in user. The **+ New Sample** button in the Microbiology workspace is shown to every member of the laboratory, and the server checks neither a receiving permission nor the laboratory. On the **Receive Sample** page, the **Receive** button is shown to users who hold a receiving permission. (GAP-227)

#### Before you start
The water system must exist in the Water configuration (§24.1).

> **Note for reviewers:** The server does not check a permission or laboratory section when a water sample is received or prepared. (GAP-227)

#### What you see
- Open **RECEIVING**, then **Receive Sample**, and click **Receive**. Or click **+ New Sample** in the Microbiology workspace.
- The dialog **New Sample** shows category tiles; choose **Water**, then **Next**.
- The entry grid has **Department \*** (the water system), **Cause of Testing \***, **Sampled By \***, **Quantity** and **Control No.**

#### How to use it
1. Click the **Water** tile and **Next**.
2. Choose the water system under **Department**, choose the **Cause of Testing**, and enter **Sampled By**, **Quantity** and **Control No.**
3. Click **Save All Samples**.
4. The system shows "Successfully received 1 sample(s). Test orders generated automatically." For a water sample the tests are in fact created at preparation (§24.3), not at receiving.

#### System behavior
The system creates the sample as **Received** and waiting for preparation. It creates no tests and no location records yet.

#### Validation
- "Department is required"
- "Cause of Testing is required"
- "Sampled By is required"
- "Water department {id} not found."

#### Status
The sample is **Received** and shows the banner **Sample Needs Preparation**.

#### What happens next
Prepare the sample (§24.3).

---

### 24.3 Water Sample Preparation and Storage Tracking
<!-- WF-SAMPLE-007 -->
#### Purpose
Record how the sample was stored, choose the sampled points, and let the system create the tests and locations.

#### Who can use it
Any signed-in user (the system does not check the laboratory). Preparation of a water sample needs no password or signature.

#### Before you start
- The sample must be **Received** and need preparation.
- The sampling points must have at least one assigned test (§24.1).

#### What you see
- In the Microbiology workspace, select the water sample and click **Prepare Sample** (or **Start Preparation** in the banner).
- The dialog **Preparation** has:
  - **Storage Condition \***: **Refrigerator** or **Room Temperature**;
  - **Storage Time (hours) \***, shown when Refrigerator is chosen;
  - the sampling points of the water system, each with code, name and its assigned tests. Points with no assigned test are disabled;
  - the button **Start Testing**, disabled until the storage details are complete and a point is chosen.

[SCREENSHOT SS-243]

#### How to use it
1. Choose the **Storage Condition**.
2. If it is **Refrigerator**, enter the **Storage Time (hours)**.
3. Tick every sampling point that was sampled.
4. Click **Start Testing**.

#### System behavior
- The system stores the storage condition. The storage time is kept only for Refrigerator.
- It creates one test for each distinct test code across the chosen points. Each starts as Pending, waiting for incubation, with the preparer as assigned analyst.
- It creates one location for each point and assigned test. For a count test, the location is linked to that point's limit row if one exists at that moment.
- Preparation is marked complete.

#### Validation
- "Storage condition is required for water samples."
- "Storage time is required when a water sample was refrigerated."
- "At least one sampling point must be selected."
- "Sampling point(s) not found: {ids}."
- "Sampling point(s) {codes} do not belong to this sample's department."
- "This sample has already been prepared."

#### Status
The sample stays **Received**; preparation changes from needed to complete. Each new test is Pending.

#### Important rules
- A refrigerated sample must have its storage time.
- Water storage details can be corrected later with **Edit Details** while the sample is not yet submitted for review (§11.3). That correction is signed.

#### What happens next
Analysts start incubation for each test (§24.4).

---

### 24.4 Water Testing and Incubation
<!-- WF-SAMPLE-007 -->
#### Purpose
Choose the media lot and incubator and start the incubation of each water test.

#### Who can use it
Requires the *Test Workflow Execution* permission (by default: Section Head, Reviewer, Analyst, System Administrator).

#### Before you start
- The sample must be prepared. **Open Workflow** is disabled until preparation is complete.
- A released media lot and an incubator whose set point is inside the medium's temperature range must exist.

> **Note for reviewers:** The incubator check reads the Laboratory Configuration equipment record (set point and calibration due date), not the Equipment Inventory status or date. An incubator refused because its calibration date has passed shows the message "The selected incubator's set point is outside this medium's temperature range." (GAP-501, GAP-517)

> **Note for reviewers:** Whether a media lot can be used when it is unreleased or expired depends on the screen path; see the table in §22.4. (GAP-500, GAP-516)

#### What you see
- In the Microbiology workspace, select the water sample and click **Open Workflow** on the test. The dialog is titled "{testCode} Workflow".
- The lists **Media batch** (options "{lotNumber}, expires {date}") and **Stage 1 Incubator** (options "{name} ({code}), {setPoint}°C"), the required temperature and period, and the button **Start Incubation**.

#### How to use it
1. Choose the **Media batch**.
2. Choose the **Stage 1 Incubator**.
3. Click **Start Incubation**.
4. Wait for the required time. The dialog shows "Incubation in progress" and "Expected reading: {datetime}". A user with the supervisor permission (by default: Section Head, System Administrator) can click **Skip Wait**, which asks for no reason or password.

#### System behavior
- The test changes from Pending to **Incubating** and the sample changes from **Received** to **Under Testing** at the first incubation start.
- Results cannot be entered before the minimum incubation time has passed, unless **Skip Wait** was used.

#### Validation
- "Test Preparation must be completed and confirmed for sample {ref} before incubation can be started for step \"{step}\"."
- "Media lot \"{lot}\" is not released for use, out of stock, or rejected."
- "The selected incubator's set point is outside this medium's temperature range."
- "This incubation window needs at least {h} hours - not ready until {utc} UTC."

#### Status
Test: Pending, then **Incubating**. Sample: **Received**, then **Under Testing**.

#### What happens next
After incubation, enter the plate readings (§24.5).

---

### 24.5 Water Result Entry and Limit Evaluation
<!-- WF-SAMPLE-007, WF-COUNT-002 -->
#### Purpose
Enter the colony counts for all sampling points in one grid, see the average and status of each point, and send the test to review.

#### Who can use it
Requires the *Test Workflow Execution* permission (by default: Section Head, Reviewer, Analyst, System Administrator).

#### Before you start
The final incubation window must be open and its minimum time elapsed (or skipped by a supervisor).

> **Note for reviewers:** Water preparation is not signed, and the preparer counts as having performed the tests. (GAP-212)

#### What you see
- In the test card, click **Record Results**. The dialog "{testCode} Results: {displayName}" has the columns **Sampling Point**, **Limits (Alert / Action / Spec)**, **Plate Readings** (with **Add Plate**), **Average** and **Status**, a summary "{conform}/{total} locations within spec; worst status: {badge}", and **Submit Results**.

[SCREENSHOT SS-244]

#### How to use it
1. Enter the colony count for each point in **Plate Readings**. Click **Add Plate** if more than one plate was used.
2. Check the average and the status of each point.
3. Make sure every point has at least one reading.
4. Click **Submit Results**.

#### System behavior
- The result of a point is the average of its readings, rounded to a whole number (a half rounds up). Unlike EM, water has no "<1" rule: an average below 0.5 is reported as 0.
- The status is based on the average, in this order: if the specification is exceeded, **Out of Specification**; else if the action limit is exceeded, **Action Limit Exceeded**; else if the alert limit is exceeded, **Alert Limit Exceeded**; if the point has no readable limits, **Limits Not Configured**; otherwise **Within Limits**. A value equal to a limit is not exceeded.
- The limits and unit are those of the limit row linked at preparation. Edits made to that row before you submit apply.
- The test result reads "{n} locations: {x} conform, {y} alert/action/spec (worst: {status})". Only **Within Limits** counts as conform.
- The incubation is closed and the test becomes ready for review. When all tests of the laboratory are ready, the sample moves to **Pending Review**.
- No password or signature is asked.

#### Validation
- "No locations are assigned to this test order."
- "Results are missing for: {names}."
- "At least one plate reading is required for: {names}."
- "\"{step}\" is not the final incubation window yet - close it and start the next window first."
- "This incubation window needs at least {h} hours - not ready until {utc} UTC."

#### Status
- Point: **Within Limits**, **Alert Limit Exceeded**, **Action Limit Exceeded**, **Out of Specification**, **Limits Not Configured**.
- Sample: **Pending Review** when all tests are ready.

#### Important rules
All points are submitted together. The grid is accepted or refused as a whole.

#### Errors
- "Could not record batch readings."

#### What happens next
A reviewer checks the results (§17). Return, retest and approval follow the normal sample path (§18, §19).

<!-- Review log (Sonnet): 27 corrections; invented items removed: route paths, permission/enum/status names and numeric codes, "calibrated/in-service incubator" claims, "PW/WFI" system list, internal rounding and class names, dilution-factor wording, section-assignment claim at receipt, OOS retest/investigation claim, legacy-endpoint message, BR id in a reviewer note; callouts added: GAP-423, GAP-418 wording, GAP-227, GAP-212, GAP-501/GAP-517, GAP-500/GAP-516 -->
<!-- Phase 13 (agy): applied F-A09, R-NEW-01 -->

<!-- Phase 13 review (Sonnet): F-A09, R-NEW-01 verified; GAP-227 exists. -->
