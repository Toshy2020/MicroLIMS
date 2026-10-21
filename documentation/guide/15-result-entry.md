## 15. Result Entry (count, observation and location-grid tests)

### 15.1 Single-Test Incubation and Media Selection
<!-- WF-COUNT-001 -->
#### Purpose
Choose a released media lot and a suitable incubator to start the plate incubation of one microbiological count test after preparation is confirmed.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head, System Administrator).

#### Before you start
- The sample is prepared. Tests of the FP section need no preparation.
- The item's specification for the test has a reporting unit and (for Product, Raw Material and Packaging Material) a dilution factor; limits are needed to get a limit status.
- The incubation hours and temperatures of the step medium are configured in the Test Master.
- A released media lot of your laboratory section's material is available.
- An incubator with a calibrated set point inside the medium's temperature range and a current calibration is available.

#### What you see
Dialog **{testCode} Workflow** with the subtitle "{ref} · {sample name} · TO #{testOrderId}", at the media selection step:
- "Requires {names or 'an approved'} media."
- "Required Temperature (Stage 1): {min}-{max} °C, Incubation Period: {h}-{h} hours"
- Media dropdown, placeholder "Media Batch ({names} lots only)"; entries read "{lotNumber}, expires {date}".
- Incubator dropdown, placeholder "Stage 1 Incubator ({min}-{max} °C)"; entries read "{name} ({code}), {setPoint}°C".
- If no incubator fits: "No incubator is set to {min}-{max} °C for this step."
- Button **Start Incubation**, enabled once both are chosen.

[SCREENSHOT SS-151]

#### How to use it
1. Select the sample and find the test under **Assigned Tests**.
2. Click **Open Workflow** on the test card.
3. Choose the media lot.
4. Choose an incubator whose set point is inside the required range.
5. Click **Start Incubation**.
6. Check that the dialog shows "Incubation in progress".

#### System behavior
- The test changes from waiting to **Incubating** (In Progress). If the sample was **Received**, it becomes **Under Testing**.
- The system records the incubation: start time, expected reading time (start plus the minimum incubation hours), media lot, incubator, stage (Stage 1) and the user.
- No electronic signature is needed.

#### Validation
- "Test Preparation must be completed and confirmed for sample {ref} before incubation can be started for step \"{step}\"."
- "Media lot \"{lot}\" is not released for use, out of stock, or rejected."
- "This step requires a different medium. The selected lot is {name}."
- "Step \"{step}\" has no media configured in Test Master."
- "Media lot \"{lot}\" was prepared from another laboratory section's material and cannot be used for this test."
- "The selected incubator's set point is outside this medium's temperature range."
- "Incubator \"{code}\" has no calibrated set point temperature."
- "Incubator \"{code}\" calibration expired on {date}."
- "The incubation window for {medium} on step {step} is not configured - set its incubation hours and temperature in Test Master."
- "Workflow order violation: step \"{a}\" must be completed before \"{b}\"."
- "Media has already been selected for step \"{step}\" - awaiting its result."

> **Note for reviewers:** When one test is started, the system checks that the lot is released but not the lot's expiry date; the grouped action checks both. (GAP-220)

#### Status
The test is **Incubating** (In Progress); the sample is **Under Testing**.

#### Errors
- "Could not start incubation for this step."
- "Could not load this test's workflow."

#### What happens next
The plates stay in the incubator. The dialog shows the temperature range, duration and expected reading time.


### 15.2 Two-Stage Incubation Transfer
<!-- WF-COUNT-001 -->
#### Purpose
Move the plates of a two-stage count test from the first incubator to a second one for the Stage 2 incubation.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head, System Administrator).

#### Before you start
- Stage 1 has reached its minimum incubation time, or a supervisor used **Skip Wait**.
- The test has a Stage 2 configuration in the Test Master (hours and temperature range).
- A suitable Stage 2 incubator is available.

#### What you see
- While incubating: "Incubation in progress" with "(Stage 1 of 2)" and the button **Transfer to Stage 2 Incubation**.
- Transfer step: "Stage 1 incubation is complete. Transfer plates to Stage 2 incubator ({min}-{max} °C) to start the second stage.", "Media Lot (inherited): {lot}", an incubator dropdown with the placeholder "Select Stage 2 Incubator ({min}-{max} °C)", and the buttons **Back** and **Start Stage 2 Incubation**.

#### How to use it
1. When Stage 1 is complete, open the **{testCode} Workflow** dialog.
2. Click **Transfer to Stage 2 Incubation**.
3. Select a Stage 2 incubator in the required temperature range.
4. Click **Start Stage 2 Incubation**.
5. Place the plates in the selected incubator.

#### System behavior
- The system checks that Stage 1 has reached its minimum time (unless Skip Wait was used).
- Stage 1 is closed with the outcome "Transferred to stage 2 incubation." Stage 2 is opened with the same media lot, the new incubator and the Stage 2 start time.
- No electronic signature is needed.

#### Validation
- "Stage 1 incubation for step \"{step}\" requires at least {h} hours of incubation - not ready until {utc} UTC."
- "Stage 2 start time ({t}) cannot precede Stage 1 start time ({t}) UTC."
- "Step \"{step}\" has no stage 2 configuration."

> **Note for reviewers:** Starting Stage 2 does not re-check the incubator set point against the medium's temperature range; the screen only lists incubators in range. (GAP-220)

#### Status
The test stays **Incubating** (In Progress), now at Stage 2 of 2.

#### Errors
"Could not start stage 2 incubation."

#### What happens next
Result entry opens when the Stage 2 incubation period has ended.


### 15.3 Plate Count Result Entry and Automatic Calculations
<!-- WF-COUNT-001 -->
#### Purpose
Record plate counts (CFU) or the observations TNTC and Uncountable, let the system calculate the result using the dilution factor and the average of the plates, and compare it with the limits.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head, System Administrator).

#### Before you start
- Incubation is complete. For a single-stage test the screen enables **Calculate** only when it considers the incubation ready (or after **Skip Wait**). For a two-stage test the end of the Stage 2 window must have passed.
- For Product, Raw Material and Packaging Material samples, the item's specification defines the **Dilution Factor** and the reporting unit. Limits (Alert, Action, Specification) are needed for a limit status.

#### What you see
In the **{testCode} Workflow** dialog at result entry:
- **Dilution Factor** with helper text "Configured value from Specifications (N)", "Direct count: dilution factor fixed at 1" or "Enter multiplier: 10 for 1:10 dilution, 100 for 1:100".
- An override icon ("Override dilution factor") and the button **Cancel Override**.
- **Justification for override**, required whenever the entered factor differs from the configured one; helper text "Required: the entered value differs from what's configured on Specifications. This is captured in the audit trail."
- One field **Plate 1**, **Plate 2** ... with the placeholder "Colony count or TNTC", a remove icon ("Remove reading {i}") and the button **Add Plate**.
- The live text "Calculated result: {display}".
- When TNTC or Uncountable is typed: "Non-numeric result: reviewer will decide accept or retest".
- Button **Calculate**.
- After calculating: "Step recorded: {outcome}" with ": Average X, Calculated Y, Status Z". For a result that is not definitive: "{outcome} - this step must be repeated with a fresh media lot." with **Proceed to Next Step** and **Retry This Step**. For the final step: "Final result: {outcome}" and **Done / Close**.

[SCREENSHOT SS-152]

#### How to use it
1. Open the **{testCode} Workflow** dialog of the test whose incubation is complete.
2. Click **Record Result**.
3. Check the **Dilution Factor**:
   - Water, Environmental Monitoring and After Cleaning use a fixed factor of 1.
   - For Product, Raw Material and Packaging Material, the configured factor is filled in. To use a different factor, click the override icon, enter the new factor and write the **Justification for override**.
4. In **Plate 1**, enter the colony count (for example 25), or enter TNTC or Uncountable if the colonies cannot be counted.
5. If more plates were read, click **Add Plate** and enter each count.
6. Check the **Calculated result** shown.
7. Click **Calculate**.
8. Review the average, the calculated result and the status, then click **Done / Close** (or **Proceed to Next Step** / **Retry This Step**).

#### System behavior
- Dilution factor:
  - Water, Environmental Monitoring and After Cleaning: the factor is forced to 1 whatever is entered.
  - Product, Raw Material, Packaging Material: if no factor is configured on the item's specification, entry is refused.
  - If the entered factor differs from the configured one, a justification is required. The entered factor, the configured factor, the override and the justification are stored with the reading.
  - The system does not check that the factor is greater than zero.
- Reporting unit: must be configured on the specification. The unit shown starts with "CFU/"; the system adds the prefix if the configured unit does not have it.
- TNTC or Uncountable (not case-sensitive): if any plate reading is TNTC or Uncountable, no average or calculated result is produced and no limits are compared. The result is flagged **Requires Review**. The reported result is "TNTC" if the first reading that is not a number is TNTC, otherwise "Uncountable". The step still completes, the test goes to **Ready**, and the reviewer decides to accept or retest. The dilution factor, justification and unit checks run first.
- Numeric result:
  - Average = the arithmetic mean of all plate readings entered (none is left out).
  - Calculated result = Average × Dilution Factor (stored with full precision).
  - Reported result: if the calculated result is smaller than the entered dilution factor, "<{dilution factor} {unit}" (the factor rounded away from zero; factor 1 gives "<1 {unit}"). Otherwise the calculated result rounded to a whole number, half away from zero, followed by the unit.
  - Example (fake data): plates 25 and 27 with factor 10 give average 26, calculated result 260, reported "260 CFU/g". Plates 0 and 1 with factor 10 give average 0.5, calculated result 5, which is below 10, so "<10 CFU/g" is reported.
- Limit status, using the item's primary specification: the calculated result is compared in this order: Specification, then Action, then Alert.
  1. **Out of Specification** if the Specification limit is exceeded.
  2. **Action Limit Exceeded** if the Action limit is exceeded.
  3. **Alert Limit Exceeded** if the Alert limit is exceeded.
  4. **Within Limits** if none is exceeded (equal to a limit is not exceeded).
  5. **Limits Not Configured** if none of the three limits can be read as a limit.
- Limit text: a plain number or "NMT x" is an upper limit; "NLT x" is a lower limit; "a-b" is a range; "t ± d", "t +/- d" or "t ± d%" is a range around a target. Limit text that cannot be read is treated as no limit without any message, so a mistyped limit can show **Limits Not Configured**.
- The system stores the plate readings, dilution factor details, average, calculated result, reported result, limit values and status with the entering user and time, and closes the incubation.
- The test goes to **Ready** (Result Entered). When every current test of the laboratory section is **Ready**, the section moves to **Pending Review** automatically (review log "All tests completed - automatically submitted for review").
- No electronic signature is needed.

#### Validation
- "At least one plate reading is required to calculate an average."
- "Plate readings are required."
- "No Dilution Factor is configured for \"{code}\" on this item's Specifications. Configure it before recording a result."
- "The entered Dilution Factor ({x}) differs from the configured value ({y}). A justification note is required."
- "No reporting Unit is configured for \"{code}\" on this item's Specifications. Configure it before recording a result."
- "Media must be selected for step \"{step}\" before a result can be recorded."
- "EM/After Cleaning results must be submitted via the batch results endpoint."
- "Step \"{step}\" requires stage 2 incubation to be started before a count can be recorded."
- "This step's incubation period has not finished yet."

> **Note for reviewers:** For a single-stage count test the system itself does not check that the incubation time has passed; only the screen holds back **Calculate**. (GAP-202)

> **Note for reviewers:** Count results and plate readings are not electronically signed at entry. (GAP-218)

> **Note for reviewers:** Text that is neither a number, TNTC nor Uncountable has no specific validation message, and negative numbers are accepted. The system also does not check that the dilution factor is above zero. (GAP-209, GAP-228)

#### Status
- The test changes from **Incubating** to **Ready** (Result Entered).
- The result status is one of **Within Limits**, **Alert Limit Exceeded**, **Action Limit Exceeded**, **Out of Specification**, **Limits Not Configured** or **Requires Review**.
- When all tests of the section are **Ready**, the section shows **Pending Review**.

#### Important rules
- A stored count result cannot be edited in place. Before the test reaches **Ready**, a result that is not definitive can be repeated with **Retry This Step**. After **Ready**, a result changes only if the reviewer returns the test to the analyst or the sample is voided.

#### Errors
- "Could not record this result."
- "Enter at least one plate reading."
- "A justification note is required when the entered Dilution Factor differs from the configured value."

#### What happens next
The test shows its result and status in the workspace. When all tests of the section are complete, the sample goes to technical review.


### 15.4 Batch Location Count Results (Environmental Monitoring, Water, After Cleaning)
<!-- WF-COUNT-002 -->
#### Purpose
Enter plate counts for all rooms, sampling points or machine parts of a test in one grid. The system calculates the averages and statuses and a summary of the worst result.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head, System Administrator).

#### Before you start
- Incubation is started for the test. If the template has several incubation windows, all earlier windows must be closed.
- The minimum incubation time of the final window has passed, or a supervisor used **Skip Wait**.

#### What you see
- Environmental Monitoring and After Cleaning grid: title **{testCode} Results: {displayName}**; columns **Location**, **Limits (Alert / Action / Spec)**, **Plate Readings** (with **Add Plate**), **Calculated Result**, **Status**; summary "{conform}/{total} locations within spec; worst status: {badge}"; before the minimum time, "Results cannot be submitted before {datetime}."; button **Submit Results** ("Submitting…").
- Water grid: columns **Sampling Point**, **Limits (Alert / Action / Spec)**, **Plate Readings**, **Average**, **Status**; button **Submit Results**.
- In the workflow dialog: **Advance to Next Incubation Window** (non-final window) and **Record Results**.

[SCREENSHOT SS-153]

#### How to use it
1. Open the test whose final incubation window is complete.
2. If an earlier window is still open, click **Advance to Next Incubation Window** after its minimum time, choose fresh media for the next window and let it incubate.
3. On the final window click **Record Results**.
4. Enter the plate counts for each location; use **Add Plate** for additional plates.
5. Check that every location has at least one reading.
6. Click **Submit Results**.

#### System behavior
- Results are accepted only on the final incubation window, and only after its minimum time (measured from the recorded incubation start).
- All-or-nothing: every location needs at least one reading, otherwise the whole batch is refused.
- Environmental Monitoring and After Cleaning: factor 1; calculated result = average of the readings; reported "<1" if the average is below 1, otherwise the average rounded to a whole number, half away from zero. Limits come from the room or machine part configuration. Units come from the configuration, otherwise from the test type: passive air "CFU/plate/4 hours", surface or air "CFU/25 cm2", swab "CFU/25 cm2", rinse "CFU/mL".
- Water: reported result = average rounded to a whole number, half away from zero; there is no "<1" rule. Limits come from the sampling configuration. No dilution factor is stored.
- Each location is compared in the order Specification, Action, Alert. The summary reads "{n} locations: {conform} conform, {rest} alert/action/spec (worst: {status})"; only **Within Limits** counts as conforming, and **Limits Not Configured** counts in the second figure.
- The incubation is closed, the test goes to **Ready** (Result Entered), and the section moves to **Pending Review** when all its tests are **Ready**.
- No electronic signature is needed.

#### Validation
- "No locations are assigned to this test order."
- "Results are missing for: {names}."
- "At least one plate reading is required for: {names}."
- "\"{step}\" is not the final incubation window yet - close it and start the next window first."
- "This is the final incubation window - record results instead of advancing."
- "This incubation window needs at least {h} hours - not ready until {utc} UTC."
- "Media must be selected for this test before batch results can be recorded."

> **Note for reviewers:** The Environmental Monitoring out-of-trend rule in the earlier specification (step 1 count plus step 2 count above the Action limit) is not applied by the system. (GAP-211)

#### Status
The test goes to **Ready** (Result Entered); the section to **Pending Review** when all its tests are complete.

#### Important rules
- For Water, an average below 0.5 is reported as "0".

#### Errors
- "Could not record batch results."
- "Could not record batch readings."

#### What happens next
The location results and the batch summary are stored and the test waits for technical review.


### 15.5 Batch Location Pathogen Results (Environmental Monitoring and After Cleaning)
<!-- WF-COUNT-002 -->
#### Purpose
Record, for every monitored location, whether pathogen growth was observed.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head, System Administrator).

#### Before you start
The test is on its final incubation window and the minimum incubation time has passed (or **Skip Wait** was used).

#### What you see
Dialog **{testCode} Results: {displayName}** with the columns **Location**, **Growth Observed?** (**Yes** or **No**) and **Status** (**Detected** when Yes, **Absent** when No), the summary "{detected}/{total} locations detected; overall: {Detected|Absent}", and the button **Submit Results**.

#### How to use it
1. Open the pathogen test on its final window.
2. For each location choose **Yes** (growth observed) or **No**.
3. Check that every location has an answer.
4. Check the overall result: **Detected** if any location is Yes, otherwise **Absent**.
5. Click **Submit Results**.

#### System behavior
- Every location needs an answer.
- A location is **Detected** if growth was observed, otherwise **Absent**; the overall result is **Detected** if any location is Detected.
- The summary result reads "{n} locations: {absent} absent, {detected} detected (overall: {Detected|Absent})".
- The incubation closes and the test goes to **Ready** (Result Entered).
- No electronic signature is needed.

#### Validation
- "A growth observation is required for every location."
- "Step \"{step}\" requires a growth observation for every location."
- "Results are missing for: {names}."
- "\"{step}\" is not the final incubation window yet - close it and start the next window first."
- "This incubation window needs at least {h} hours - not ready until {utc} UTC."

> **Note for reviewers:** Pathogen results entered at location level are not electronically signed. (GAP-218)

#### Status
The test goes to **Ready** (Result Entered); the result is **Detected** or **Absent**.

#### Errors
"Could not record batch pathogen results."

#### What happens next
The test waits for technical review once the other tests of the section are complete.


### 15.6 Qualitative Observation Results (Physicochemical)
<!-- WF-OBS-001 -->
#### Purpose
Record "Complies" or "Does not comply" for each parameter of a qualitative physicochemical test, with an electronic signature.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head, System Administrator). Details of the other physicochemical panels are in the physicochemical sections of this guide.

#### Before you start
- The sample's item has specifications of the qualitative or presence/absence kind for the test.
- The test is not yet **Ready**.
- No active qualitative entry already exists for the test.

#### What you see
- **Analysis time** (cannot be in the future).
- **Equipment (Physicochemical)**, optional; includes "None (no instrument linked)".
- **Comment (optional)**.
- For each parameter: its name and limits, the choices **Complies** and **Does not comply**, and the field **Observation (optional)**, which becomes **Observation (required) \*** with the helper "Observation is required when non-compliant" when **Does not comply** is chosen.
- Button **Sign and record result**.

[SCREENSHOT SS-154]

#### How to use it
1. In the FP or RM & PM workspace, open the qualitative test.
2. Set the **Analysis time**.
3. Optionally choose the instrument in **Equipment (Physicochemical)**.
4. For each parameter choose **Complies** or **Does not comply**.
5. For **Does not comply**, enter the explanation in **Observation (required) \*** (up to 500 characters).
6. Click **Sign and record result**.
7. Read the signature meaning: "I entered these qualitative observations and am recording this test result."
8. Enter your password and confirm.

#### System behavior
- Every specification of the test must be answered exactly once.
- An observation is mandatory if any parameter does not comply.
- The analysis time may not be more than 5 minutes ahead.
- The system records an electronic signature with the user, time and meaning, and stores one result per parameter: **Complies** counts as within limits, **Does not comply** as out of specification.
- The test goes to **Ready** (Result Entered); the section is submitted for review when all its tests are **Ready**.

#### Validation
- "Password is required to sign the result."
- "At least one parameter result is required."
- "Analysis time cannot be in the future."
- "An observation is required when result does not conform."
- "Observation cannot exceed 500 characters."
- "An active Qualitative entry already exists for this test order."
- "No specifications configured for this test and item."
- "Every specification for this test must be supplied exactly once."

> **Note for reviewers:** Qualitative physicochemical results are signed with a password at entry, whereas microbiological count, location and pathogen results are not. (GAP-218)

#### Status
The test goes to **Ready** (Result Entered); the result is **Within Limits** or **Out of Specification**.

#### Errors
- "Password verification failed. The signature was not applied."
- "Analysis time cannot be in the future."
- "An observation is required when result does not conform."

#### What happens next
The test waits for technical review.

<!-- Review log (Sonnet): 48 corrections; invented items removed: phase names, class/entity/enum names, stored-field names, code fragments, unsupported "Calculate disabled until a reading is entered", wrong "reset by supervisor" rework path, unsupported unit fallback, "result is locked" claim, BR id in a reviewer note (moved to Important rules); callouts added: none missing (GAP-202, 209, 211, 218, 220, 228 present); dilution/rounding/limit rules checked against verified rules, worked examples added -->
