## 16. Pathogen Testing

### 16.1 Single-Test Pathogen Step Chain Overview
<!-- WF-PATH-001 -->
#### Purpose
Understand the steps of a qualitative pathogen test opened from its test card: broth enrichment, selective plating, confirmatory plating and biochemical test.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head, System Administrator).

#### Before you start
- The sample is received and its preparation is confirmed.
- The Test Master template defines the step chain, the target organism, the permitted media with their incubation windows and the expected colony appearance.

#### What you see
In the **{testCode} Workflow** dialog (subtitle "{ref} · {sample name} · TO #{testOrderId}"):
- Step chips: **Incubating**, **Preparation · no pass/fail**, **Set up**, **Ready to read**, **Result entry**.
- A link **What happens in this step?** / **Hide step details**.
- At the end: "✓ Final result: {finalResult}" and the button **Done / Close**.

#### System behavior
- The steps follow the chain defined in the Test Master for the test: broth enrichment (or selective broth), selective plating, confirmatory plating, biochemical test. A step cannot be skipped: "Workflow order violation: step \"{a}\" must be completed before \"{b}\"."
- The first incubation start moves the test to **Incubating** (In Progress) and the sample to **Under Testing**. The final result moves the test to **Ready** (Result Entered).
- When all tests of the laboratory section are **Ready**, the section moves to **Pending Review** automatically.
- Steps and results are not electronically signed.

> **Note for reviewers:** The pathogen steps are driven by the template in the Test Master, not by the fixed pathogen sequence in the earlier specification. (GAP-205)

#### Status
Step: Waiting, then **Incubating**, then **Ready**. Status: Pending, then In Progress, then Result Entered.

#### Errors
- "No current step is available for this test order."
- "Workflow order violation: step \"{a}\" must be completed before \"{b}\"."


### 16.2 Broth Enrichment and Shared TSB Steps
<!-- WF-PATH-001 -->
#### Purpose
Start the pre-enrichment or selective broth incubation, or link the test to the shared TSB tube already running for the sample.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head, System Administrator).

#### Before you start
- The sample is prepared.
- A released media lot of the step's broth medium is available.
- An incubator with a set point inside the required temperature range is available.

#### What you see
- Start panel:
  - A fixed row **Medium** with a lock ("Test Master").
  - **Media lot** (helper "Released and within expiry", or "No released lots of this medium are available").
  - **Incubator** (helper "Only incubators set within {min}–{max} °C are listed").
  - A timeline: **Start** (0 h), **Ready from** ({minHours} h), **Read by** ({maxHours} h).
  - "Start time, media lot and incubator are recorded in the audit trail."
  - Button **Start Incubation** ("Starting...").
- If the shared TSB already runs for the sample: the banner **Shared TSB Applied** ("This test is linked to the shared TSB for this sample. There is one shared tube per sample; no separate broth entry required.") and the button **Proceed to Next Step →**. A prompt "Apply Shared TSB to All Pathogen Tests" has the button **Confirm & Apply to All**.
- While incubating: "Incubating" and the button **Proceed to {nextStep}** (or **Proceed to Reading**), disabled until the minimum incubation time has passed.

#### How to use it
1. Open the **{testCode} Workflow** dialog at the broth step.
2. If **Shared TSB Applied** is shown, click **Proceed to Next Step →**.
3. Otherwise choose the **Media lot**.
4. Choose the **Incubator**.
5. Click **Start Incubation**.
6. When the minimum time has passed (or a supervisor used **Skip Wait**), open the dialog and click **Proceed to {nextStep}**.

#### System behavior
- Starting a test that uses the shared TSB also starts it for all other pathogen tests of the sample. The system first checks that each of them has an incubation window configured and that the incubator suits each medium.
- The broth step is refused before the minimum time: "This step requires at least {h} hours of incubation - not ready until {utc} UTC."
- Submitting the finished broth step moves the test to selective plating.

#### Validation
- "Test Preparation must be completed and confirmed for sample {ref} before incubation can be started for step \"{step}\"."
- "This step requires at least {h} hours of incubation - not ready until {utc} UTC."
- "Step \"{step}\" has no media configured in Test Master."
- "This step requires a different medium. The selected lot is {material name}."
- "Media lot \"{lot}\" was prepared from another laboratory section's material and cannot be used for this test."
- "The selected incubator's set point is outside this medium's temperature range."
- "The shared TSB can't start: the incubation window for \"{medium}\" on test {code} (step \"{step}\") is not configured - set its incubation hours and temperature in Test Master."
- "The shared TSB can't start: the selected incubator's set point is outside the {min}-{max} °C range of \"{medium}\" on test {code}."

#### Status
The test changes from waiting to **Incubating**; the sample from **Received** to **Under Testing**.

#### Errors
- "Could not start incubation for this step."

#### What happens next
The broth step is complete and the test moves to selective plating.


### 16.3 Selective Plating and Primary Observations
<!-- WF-PATH-001 -->
#### Purpose
Plate the enriched broth on selective agar, incubate, and record what is seen to decide whether the target is absent or presumptively present.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head, System Administrator).

#### Before you start
- The broth step is complete.
- A released lot of the permitted selective medium and a suitable incubator are available.

#### What you see
- Start view: an expected-appearance note (warning "Expected appearance not configured in Test Master for {media}..." or info "Expected target-positive colony"), the **Media lot** and **Incubator** dropdowns, and **Start Incubation**.
- Waiting view: **Started**, **Ready from**, **Read by**, and a disabled **Record Observation**.
- Read view: "Incubation complete. Read the plate and record what you observe." with the choices
  - **No growth** ("Target absent")
  - **Growth, does not match expected appearance** ("Target absent")
  - **Growth matching expected appearance** ("Presumptive positive")
  - the optional field **Observed appearance note (optional)**, and **Record Observation** ("Recording...").

[SCREENSHOT SS-161]

#### How to use it
1. Open the selective plating step in the **{testCode} Workflow** dialog.
2. Choose the **Media lot** and **Incubator** and click **Start Incubation**.
3. Let the plates incubate until the minimum time has passed.
4. Compare the plate with the expected appearance shown.
5. Choose **No growth**, **Growth, does not match expected appearance** or **Growth matching expected appearance**.
6. Optionally add a note.
7. Click **Record Observation**.

#### System behavior
- The system refuses the observation before the minimum time: "Minimum incubation time not elapsed. Available from: {utc} UTC." (bypassed by **Skip Wait**).
- **No growth** or **Growth, does not match expected appearance** ends the test at once as **Not Detected**: a result is written, the test goes to **Ready** (Result Entered), the incubation closes, and the section moves to **Pending Review** if all its tests are done.
- **Growth matching expected appearance** records the observation with a copy of the expected appearance and opens confirmatory plating.
- The lot must be released, from your section's material and of the permitted medium; the incubator must be in range.
- No electronic signature is needed.

#### Validation
- "No active incubation found for step \"{step}\". Start incubation before recording observation."
- "Minimum incubation time not elapsed. Available from: {utc} UTC."
- "Media lot {lot} is not a lot of the permitted medium for this step."
- "Step \"{step}\" has no target organism configured - complete this step's template in Test Master before recording results."
- "Select an observation."

> **Note for reviewers:** The incubation start time sent by the analyst is accepted for selective plating, so the minimum time is measured from the time submitted. (GAP-224)

#### Status
- Target absent: **Ready** (Result Entered), result **Not Detected**.
- Presumptive positive: the test stays In Progress and confirmatory plating opens.

#### Errors
- "Could not record this observation."
- "Select an observation."

#### What happens next
Presumptive positive: set up confirmatory plating. Target absent: the test waits for technical review.


### 16.4 Confirmatory Plating, Agreement and Analyst Decision
<!-- WF-PATH-001 -->
#### Purpose
Plate on the permitted confirmatory media, record the plate observations, and decide whether to conclude as Detected or continue with the biochemical test.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head, System Administrator).

#### Before you start
- Selective plating was recorded as **Growth matching expected appearance**.
- Released lots of the permitted confirmatory media and suitable incubators are available.

#### What you see
- Start view: **Target organism**; "Tick every medium you actually plated, then choose its lot and incubator. Only media the step template permits are listed."; a checkbox per permitted medium with its lot and incubator; "Each medium keeps its own window; the shared incubation runs to the longest one selected."; **Start Incubation**.
- Read view: "Incubation complete. Read each plate against its expected appearance."; for each medium the checkbox **This medium was actually plated** and the choices **No growth**, **Growth, does not match expected appearance**, **Growth matching expected appearance**; **Submit Observations**.
- Decision block, shown after submission when all media show growth matching the expected appearance: **Conclude as Detected** and **Proceed to Biochemical Confirmation**.

[SCREENSHOT SS-162]

#### How to use it
1. Tick each confirmatory medium you actually plated.
2. Choose the **Media lot** and **Incubator** for each.
3. Click **Start Incubation**.
4. When the incubation period has ended, open the read view.
5. Check **This medium was actually plated** for each plated medium.
6. Choose the observation for each medium.
7. Click **Submit Observations**.
8. If all media show **Growth matching expected appearance**, choose **Conclude as Detected** or **Proceed to Biochemical Confirmation**.

#### System behavior
- Setup: at least one medium; each medium must be on the step's permitted list and have a lot and an incubator. One shared incubation is created, starting at the start time given and running to the longest maximum time of the media chosen.
- Read-out: exactly one observation per selected medium, and only after the incubation period has ended (unless **Skip Wait** was used).
- If all media show growth matching the expected appearance, the analyst decision is offered. Any other combination is inconclusive: the system writes "Confirmatory plating inconclusive - flagged for investigation." and flags the test; no investigation record is created. The analyst decision is not offered, but the biochemical test remains available.
- **Conclude as Detected** writes "Analyst decision: submitted as Detected without biochemical confirmation.", sets the result **Detected**, flags that no biochemical confirmation was done, moves the test to **Ready** (Result Entered) and submits the section for review when all its tests are done.
- **Proceed to Biochemical Confirmation** writes "Analyst decision: proceed to biochemical confirmation." and opens the biochemical step.
- No electronic signature or password is needed.

#### Validation
- "At least one confirmatory medium must be selected."
- "Every selected medium needs a lot and an incubator."
- "That medium is not on this step's permitted list."
- "Step \"{step}\" requires at least {h} hours of incubation - the declared window is {n} hours."
- "Confirmatory media have already been selected for step \"{step}\" - awaiting their plate readings. The submitted panel and its incubation are shown on this test order's current step."
- "Confirmatory plating for step \"{step}\" has already been read out and cannot be set up again."
- "Exactly one observation is required for each selected medium."
- "This step's incubation period has not finished yet."
- "An analyst decision is only available after an all-conforming confirmatory result."
- "An analyst decision ({decision}) was already recorded for this confirmatory result."

> **Note for reviewers:** Concluding a pathogen test as Detected without biochemical confirmation needs no electronic signature or password. (GAP-216)

#### Status
- Concluded as Detected: **Ready** (Result Entered), result **Detected**.
- Proceed to biochemical: the test stays In Progress at the biochemical step.

#### Errors
- "Select an observation for: {media}."
- "Check at least one medium that was actually plated."
- "Every checked medium needs a media lot and an incubator."
- "At least one medium must be confirmed as plated."

#### What happens next
Concluded as Detected: the test waits for review. Otherwise the analyst does the biochemical test.


### 16.5 Biochemical Test Confirmation and Finalization
<!-- WF-PATH-001 -->
#### Purpose
Record the biochemical identification result and finish the pathogen test.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head, System Administrator).

#### Before you start
- The preceding step was conforming: a confirmatory result where all media conformed or that was inconclusive, or a selective plating with growth matching the expected appearance.
- The biochemical identification has been performed.

#### What you see
- **Decision**, shown only after an all-conforming confirmatory result: the alert "Confirmatory result: All Conforming. An analyst decision is required before this workflow proceeds.", the warning "Submitting as Detected without biochemical confirmation will be flagged for the reviewer.", and the buttons **Submit as Detected (skip biochemical)** and **Proceed to Biochemical Test**. **Submit as Detected (skip biochemical)** ends the test as **Detected** without a biochemical result, and the result is flagged for the reviewer.
- **Result form**: **Biochemical result** (required; helper "For example an IMViC pattern or API strip result."), the question "Based on this result, is the organism Detected or Not Detected? …" with choices **Detected** and **Not Detected**, and the button **Submit Result** (disabled until both are filled).
- For an Inconclusive confirmatory result, the screen also shows "Confirmatory plating was Inconclusive and has been flagged for investigation in the audit trail. …".

#### How to use it
1. Open the biochemical step.
2. If the decision buttons are shown, click **Proceed to Biochemical Test** (use **Submit as Detected** only when the laboratory procedure allows it).
3. Enter the **Biochemical result**.
4. Choose **Detected** or **Not Detected**.
5. Click **Submit Result**.
6. Click **Done / Close**.

#### System behavior
- The step is available only after a conforming result on the preceding step: "A biochemical test is only available after a conforming result on \"{step}\"."
- Organism detected: result **Detected**. Not detected: result **Not Detected**.
- Only the last biochemical step of the chain finishes the test: the final result is written, the test goes to **Ready** (Result Entered) and the section moves to **Pending Review** when all its tests are done.
- No electronic signature is needed.

#### Validation
- "A biochemical result is required."
- "A biochemical test is only available after a conforming result on \"{step}\"."
- "Test order {id} is already at {step} - its workflow can no longer be submitted against."

> **Note for reviewers:** A reviewer decision on a result submitted as Detected without biochemical confirmation exists in the system, but no screen offers it. (GAP-901)

#### Status
**Ready** (Result Entered); result **Detected** or **Not Detected**.

#### Errors
"Could not record biochemical result."

#### What happens next
The test is complete and cannot be changed by the analyst; it waits for technical review.


### 16.6 Pathogen Testing Session Workspace
<!-- WF-PATH-002 -->
#### Purpose
Run the pathogen tests of one sample across all its locations in one workspace: shared TSB enrichment, a Location × Test grid of primary observations, confirmatory plating for flagged locations, and completion.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head, System Administrator).

#### Before you start
- The sample is prepared and its monitoring locations exist.
- The sample has pathogen tests that use the shared TSB broth.
- An unexpired TSB media lot and an in-service incubator are available.

#### What you see
Title **Pathogen Testing Session Workspace**, subtitle "Session: {sessionId} · Sample: {ref} ({area})", and (for supervisors) **Reset Session**. Six steps:
1. **Overview**: cards **Sampling Locations**, **Assigned Tests**, **Required Results ({locations} × {tests})** and **Completed ({pending} pending)**; buttons **Open Shared TSB Setup** and **Open Test Workflows**.
2. **Shared TSB**: **TSB Media Lot (GPT Passed & Released) \***, **Incubator Equipment (In-Service) \***, read-only **Required Incubation Temperature \*** and **Required Incubation Time \***, button **Start TSB Enrichment**; while running: "LOCKED: Execution in Progress".
3. **Test Workflows**: **Assigned Test Workflows (N)** and **Go to Result Matrix**.
4. **Primary Matrix**: a grid with locations as rows and tests as columns. Count tests have CFU number fields (0 to 10000); other tests have a dropdown with "Select Observation", "No Growth (-)", "Growth Non-Conforming", "Growth Conforming (+)". Counts "Recorded: N", "Available: N", "Locked: N" (locked cells: "Locked until upstream workflow step completes"). Buttons **Fill All Unset as No Growth (-)**, **Save Observations**, and **Proceed to Confirmation** or **Review & Complete**. A confirmation message appears after saving.
5. **Confirmatory Plating**: "Confirmatory Plating (Panel B)" and the chip "N Flagged Locations". Phase 1 "Shared Media Setup": a lot and incubator per medium and **Actual Incubation Start Time \***, button **Start Confirmatory Incubation**. Phase 2 "Plate Observations": per medium **No Growth**, **Non-Conf**, **Conf (+)**; chips **Detected (+): n**, **Not Detected (-): n**, **Inconclusive (Retest): n**, **Pending Reads: n**; buttons **Modify Setup** and **Save Readings & Continue**. If nothing needs it: "Confirmatory Plating Not Required".
6. **Review & Complete** (locked until all required results exist): **Final Analytical Results Summary** with the evidence "1. Primary Plate Observation", "2. Confirmatory Plating", "3. Agreement Evaluation"; result chips **Detected (+)**, **Not Detected (-)**, **Inconclusive (Retest)** or a count in CFU; button **Complete Testing Session** (then "Session Completed & Submitted"); if results are missing, "There are N missing results. Please complete all Location × Test entries before final completion." with **Edit Matrix**.

[SCREENSHOT SS-163]
[SCREENSHOT SS-164]

#### How to use it
1. Select the sample and click **Open Pathogen Workflow**.
2. In **Overview**, check the locations and tests, then click **Open Shared TSB Setup**.
3. In **Shared TSB**, choose the lot and incubator and click **Start TSB Enrichment**.
4. Wait for the TSB incubation; the matrix stays locked meanwhile.
5. In **Primary Matrix**, for each location and test choose **No Growth (-)**, **Growth Non-Conforming** or **Growth Conforming (+)**, or enter the CFU count. **Fill All Unset as No Growth (-)** fills the empty cells.
6. Click **Save Observations**.
7. If any location shows growth, go to **Confirmatory Plating**: choose lots and incubator, enter the actual start time, click **Start Confirmatory Incubation**; afterwards record **No Growth**, **Non-Conf** or **Conf (+)** for each medium at each flagged location and click **Save Readings & Continue**.
8. In **Review & Complete**, check the summary and click **Complete Testing Session**.

#### System behavior
- Starting the TSB starts the incubation of all assigned pathogen tests that use it; the matrix is locked until the TSB incubation ends.
- **No Growth (-)** gives **Not Detected (-)**. **Growth Conforming (+)** and **Growth Non-Conforming** are pending confirmation and flag the location for confirmatory plating. Count cells are compared with the limits (Specification, Action, Alert).
- The number of confirmatory media is fixed by the Test Master (if not set: 2 for test codes containing "SALMONELLA", otherwise 1): "Pathogen {code} requires exactly {n} confirmatory media; received {m}."
- When all media of a location are read, the system evaluates agreement: all **Growth Conforming (+)** gives **Detected (+)**; all No Growth, or all Growth Non-Conforming, gives **Not Detected (-)**; any other mix gives **Inconclusive (Retest)**.
- Completion is refused while any Location × Test result is missing or a test is still incubating. On success every test of the sample goes to **Ready** (Result Entered), the sample is set directly to **Pending Review**, and the log reads "Testing session completed - submitted for review".
- No electronic signature or password is needed.

#### Validation
- Shared TSB: "Please select a valid released TSB media lot and an in-service incubator."; "Media lot #{lot} expired on {date}."; "None of the assigned tests on this sample require TSB enrichment."; "Media lot #{lot} ({name}) is not the TSB medium of any test on this sample."; "The shared TSB can't start: incubator {code}'s set point is outside the {range} range of \"{medium}\" on test {code}."
- Matrix and confirmation: "Cannot enter primary observations for {code}: TSB incubation or required workflow steps are still in progress."; "Location #{id} does not have a GrowthConforming or GrowthNonConforming primary observation and cannot proceed to confirmation."; "Pathogen {code} requires exactly {n} confirmatory media; received {m}."; "Material #{id} is not on the permitted media list of step \"{step}\"."; "Incubator {code}'s set point is outside the {range} range of \"{medium}\"."
- Completion: "Cannot complete testing session: {n} required Location × Test result(s) are missing ({first 5 as Location (Test)}{, and k more})."; "Cannot complete testing session: Assigned tests {codes} have incomplete workflow prerequisites."

> **Note for reviewers:** The screen and the system judge confirmatory agreement differently when one medium shows No Growth and another shows Non-Conforming growth: the screen shows Not Detected, the system stores Inconclusive. (GAP-223)

> **Note for reviewers:** Completing a session needs no electronic signature or password and sets the whole sample directly to Pending Review, bypassing the normal section check. (GAP-222)

> **Note for reviewers:** When results are missing, the screen shows "Cannot complete session: {n} required results are still missing.", which differs from the system's detailed message. (GAP-210)

> **Note for reviewers:** In the session, the shared TSB and confirmatory lots are checked for expiry and medium but not for release status. (GAP-220)

#### Status
Every test of the sample goes to **Ready** (Result Entered); the sample goes to **Pending Review**.

#### Errors
- "Cannot complete session: {n} required results are still missing."

#### What happens next
The sample goes to the technical reviewer.


### 16.7 Supervisor Pathogen Session Reset
<!-- WF-PATH-003 -->
#### Purpose
Let a supervisor clear all entries of a sample's testing session so that the tests can be started again. The reset applies to every test order of the sample, including count tests, physicochemical tests and superseded orders, not only the pathogen tests.

#### Who can use it
Requires the *Supervise test workflows* permission (by default: Section Head, System Administrator).

#### Before you start
The sample is not **Voided** or **Rejected**.

> **Warning:** The system does not stop a reset of a sample that is **Under Approval** or **Approved**. Its results are deleted, its tests return to Pending, and an approved sample keeps the status **Approved**. Do not reset a sample that has been reviewed or released.

#### What you see
In the session workspace header, the button **Reset Session**. The dialog **Reset Testing Session & Steps?** has the field **Reason for Reset \*** (placeholder "e.g. Test configuration error / Wrong media lot selected / Operational error") and the buttons **Cancel** and **Confirm Reset** ("Resetting...").

#### How to use it
1. Click **Reset Session**.
2. Enter the **Reason for Reset \***.
3. Click **Confirm Reset**.
4. Check that the workspace returns to its initial state.

#### System behavior
- A voided or rejected sample cannot be reset.
- The system permanently deletes, for every test of the sample, the observations, step results, plate readings, results and incubations, including the location results.
- Every test returns to waiting (Pending). A sample that was **Under Testing** or **Pending Review** returns to **Received**; samples in other statuses keep their status.
- Test orders of every laboratory on the sample are set to Pending.
- Preparation is kept.
- The system writes an audit entry with user, time, reference and reason, and a workflow note on each test: "Testing session and workflow steps reset. Reason: {reason}".
- No electronic signature or password is needed.

#### Validation
- "Sample #{ref} has been voided and its testing session cannot be reset."
- "Sample #{ref} has been rejected and its testing session cannot be reset. A rejection is reversed through the approval workflow by a Section Head."

> **Note for reviewers:** Resetting a session permanently deletes all entered observations, step results, readings, results and incubations for every test order across all laboratories on the sample, and is not refused for samples under approval or approved; the **Reset Session** button is not status-gated and an Approved sample keeps the status Approved while its results are deleted and its test orders return to Pending. (GAP-221)

> **Note for reviewers:** The dialog asks for a reason, but if the reason arrives blank the system uses a default text ("Section Head requested session reset" from the screen, "Analyst requested session workflow reset" from the system). (GAP-217)

#### Status
All tests return to waiting (Pending). **Under Testing** or **Pending Review** samples return to **Received**.

#### Errors
- "Sample #{ref} has been voided and its testing session cannot be reset."
- "Sample #{ref} has been rejected and its testing session cannot be reset. A rejection is reversed through the approval workflow by a Section Head."

#### What happens next
Analysts must start incubation again and re-enter all observations and counts.

<!-- Review log (Sonnet): 44 corrections; invented items removed: class and enum names, flag/audit codes, permission code, unconfirmed biochemical field labels and button "Record Biochemical Result", ALCOA+ confirmation message, unsupported "Failed to save observations", "testing data must exist" precondition, reset "operational warning" step; callouts added: GAP-220 (session lots), biochemical reviewer-return note (no gap id), others kept (GAP-205, 210, 216, 217, 221, 222, 223, 224) -->
<!-- Phase 13 (agy): applied F-A07, F-A10, F-A12 -->
<!-- Phase 13 review (Sonnet): checked against RECONCILIATION section 2 and frontend strings; no invented items -->
