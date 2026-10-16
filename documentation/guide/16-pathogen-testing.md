## 16. Pathogen Testing

### 16.1 Single-Test Pathogen Step Chain Overview
<!-- WF-PATH-001 -->
#### Purpose
Understand the multi-step analytical progression for qualitative pathogen detection tests, spanning broth enrichment, selective plating, confirmatory plating, and biochemical confirmation.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head).

#### Before you start
- The sample must be received and sample preparation confirmed (preparation status **Ready**).
- The test template in the Test Master must define the step chain sequence, target organism, permitted media lots, incubation windows, and expected colony appearances.

#### What you see
- In the test workflow dialog (`PathogenStepDialog`):
  - Dialog title: **{testCode} Workflow** (subtitle: "{ref} · {sample name} · TO #{testOrderId}").
  - Step progress chips: **Incubating**, **Preparation · no pass/fail**, **Set up**, **Ready to read**, and **Result entry**.
  - Informational toggle: **What happens in this step?** / **Hide step details**.
  - Final step view: "✓ Final result: {finalResult}" with action button **Done / Close**.

#### System behavior
- Template-driven execution:
  - Pathogen testing follows a sequential step chain defined in the Test Master: `BrothEnrichment` or `SelectiveBroth`, `SelectivePlating`, `ConfirmatoryPlating`, and `BiochemicalTest`.
- Sequential order enforcement:
  - Steps must be executed in order. Attempting to skip or perform steps out of order is refused: "Workflow order violation: step \"{a}\" must be completed before \"{b}\"."
- Lifecycle and review trigger:
  - The first incubation start moves the test order to step **Incubating** and status **InProgress**, and the overall sample status moves to **Under Testing**.
  - Final analytical completion moves the test order to step **Ready** and status **ResultEntered**.
  - When all test orders of the laboratory section reach **Ready**, the section automatically advances to **UnderReview** (displayed in the register as **Pending Review**).
- Electronic signature:
  - Pathogen step entries and results do not require an electronic signature.

> **Note for reviewers:** The pathogen test step chain is driven by templates in the Test Master rather than a dedicated hardcoded pathogen engine as described in earlier specifications. (GAP-205)

#### Status
- Test order progresses through workflow steps: **Waiting** → **Incubating** → **Ready**.
- Test order status progresses: **Pending** → **InProgress** → **ResultEntered**.

#### Errors
- "No current step is available for this test order."
- "Workflow order violation: step \"{a}\" must be completed before \"{b}\"."


### 16.2 Broth Enrichment and Shared TSB Steps
<!-- WF-PATH-001 -->
#### Purpose
Initiate pre-enrichment or selective broth incubation, or link the test order to a shared Tryptic Soy Broth (TSB) tube already running for the sample.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head).

#### Before you start
- The sample must be prepared.
- An approved, released media lot matching the step's configured broth medium must be available.
- An operational incubator set within the required temperature range must be available.

#### What you see
- Setup panel (`BrothStepPanel`):
  - Medium display: fixed row **Medium** with lock icon ("Test Master").
  - Media lot dropdown: **Media lot** (helper text: "Released and within expiry", or "No released lots of this medium are available").
  - Incubator dropdown: **Incubator** (helper text: "Only incubators set within {min}–{max} °C are listed").
  - Incubation timeline: **Start** (0 h), **Ready from** ({minHours} h), **Read by** ({maxHours} h).
  - Audit note: "Start time, media lot and incubator are recorded in the audit trail."
  - Action button: **Start Incubation** (displays "Starting..." while processing).
- Shared TSB branch (when shared TSB is already active on the sample):
  - Banner: **Shared TSB Applied** with text "This test is linked to the shared TSB for this sample. There is one shared tube per sample; no separate broth entry required."
  - Action button: **Proceed to Next Step →**.
  - Multi-test prompt: dialog "Apply Shared TSB to All Pathogen Tests" with button **Confirm & Apply to All**.
- Waiting panel (`BrothWaitingPanel`):
  - Status display: "Incubating"
  - Action button: **Proceed to {nextStep}** (or **Proceed to Reading**), disabled until minimum incubation duration has elapsed.

#### How to use it
1. Open the **{testCode} Workflow** dialog for the test order at the broth step.
2. If the sample already has an active shared TSB incubation, review the **Shared TSB Applied** banner and click **Proceed to Next Step →**.
3. Otherwise, select the **Media lot** from the dropdown.
4. Select the **Incubator** whose calibrated set point falls within the required range.
5. Click **Start Incubation**.
6. When incubation completes (minimum incubation duration has elapsed, or supervisor has used **Skip Wait**), open the dialog and click **Proceed to {nextStep}**.

#### System behavior
- Shared TSB linkage:
  - If started from a single test order that uses shared TSB, the system checks that all sibling pathogen tests on the sample have configured incubation windows and compatible incubator set points. Starting incubation automatically propagates the shared TSB incubation across all sibling pathogen test orders.
- Incubation timing:
  - The server refuses broth submission before the medium's minimum incubation duration has elapsed: "This step requires at least {h} hours of incubation - not ready until {utc} UTC."
- Step transition:
  - Submitting the completed broth step advances the test order to the subsequent selective plating step.

#### Validation
The system enforces the following exact validation rules:
- "Test Preparation must be completed and confirmed for sample {ref} before incubation can be started for step \"{step}\"."
- "This step requires at least {h} hours of incubation - not ready until {utc} UTC."
- "Step \"{step}\" has no media configured in Test Master."
- "This step requires a different medium. The selected lot is {material name}."
- "Media lot \"{lot}\" was prepared from another laboratory section's material and cannot be used for this test."
- "The selected incubator's set point is outside this medium's temperature range."
- "The shared TSB can't start: the incubation window for \"{medium}\" on test {code} (step \"{step}\") is not configured - set its incubation hours and temperature in Test Master."
- "The shared TSB can't start: the selected incubator's set point is outside the {min}-{max} °C range of \"{medium}\" on test {code}."

#### Status
- Test order workflow step advances from **Waiting** to **Incubating**.
- Sample status moves from **Received** to **Under Testing**.

#### Errors
- "Could not start incubation for this step."
- "This step requires at least {h} hours of incubation - not ready until {utc} UTC."

#### What happens next
The broth step completes, and the test order advances to selective plating.


### 16.3 Selective Plating and Primary Observations
<!-- WF-PATH-001 -->
#### Purpose
Inoculate selective agar plates from enriched broth, incubate under controlled conditions, and record primary colony appearance observations to determine presumptive positive growth or conclude the test as absent.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head).

#### Before you start
- The preceding broth enrichment step must be completed.
- A released lot of the permitted selective agar medium and an eligible incubator must be available.

#### What you see
- Setup view (`SelectivePlatingPanel`):
  - Expected appearance guidance: warning "Expected appearance not configured in Test Master for {media}..." or info "Expected target-positive colony".
  - Dropdown controls for **Media lot** and **Incubator**.
  - Action button: **Start Incubation**.
- Waiting view:
  - Incubation countdown showing **Started**, **Ready from**, and **Read by** timestamps.
  - Disabled action button: **Record Observation**.
- Readout view (enabled once minimum incubation duration elapses):
  - Instruction: "Incubation complete. Read the plate and record what you observe."
  - Observation radio choices:
    - **No growth** (detail: "Target absent")
    - **Growth, does not match expected appearance** (detail: "Target absent")
    - **Growth matching expected appearance** (detail: "Presumptive positive")
  - Optional field: **Observed appearance note (optional)**
  - Action button: **Record Observation** (displays "Recording..." while submitting).

[SCREENSHOT SS-161]

#### How to use it
1. In the **{testCode} Workflow** dialog, open the selective plating step.
2. Select the selective **Media lot** and **Incubator**, and click **Start Incubation**.
3. Allow the plates to incubate until the minimum duration has elapsed.
4. When the plates are ready, examine colony morphology against the displayed expected appearance.
5. In the readout view, select one observation:
   - Select **No growth** if no colonies developed.
   - Select **Growth, does not match expected appearance** if colonies developed but do not match the target pathogen morphology.
   - Select **Growth matching expected appearance** if presumptive positive colonies developed.
6. Optionally enter descriptive notes in the **Observed appearance note (optional)** field.
7. Click **Record Observation**.

#### System behavior
- Incubation timing gate:
  - The server verifies that the minimum incubation duration has elapsed: "Minimum incubation time not elapsed. Available from: {utc} UTC." (Bypassed if supervisor executed Skip Wait).
- Early negative termination:
  - If the analyst selects **No growth** or **Growth, does not match expected appearance**:
    - The test is finalized immediately as **NotDetected** without requiring confirmatory plating.
    - A `Result` entity is recorded, the test order moves to step **Ready** and status **ResultEntered**, and the incubation is closed.
    - If all tests for the section are now complete, the section automatically advances to **Pending Review**.
- Presumptive positive progression:
  - If the analyst selects **Growth matching expected appearance**:
    - The observation is recorded with a snapshot of the expected appearance.
    - The step completes and unlocks the subsequent confirmatory plating step.
- Electronic signature:
  - Recording selective plate observations does not require an electronic signature.

#### Validation
The system enforces the following exact validation rules:
- "No active incubation found for step \"{step}\". Start incubation before recording observation."
- "Minimum incubation time not elapsed. Available from: {utc} UTC."
- "Media lot {lot} is not a lot of the permitted medium for this step."
- "Step \"{step}\" has no target organism configured - complete this step's template in Test Master before recording results."
- Client check: "Select an observation."

> **Note for reviewers:** Analyst-supplied incubation start times are accepted for selective plating, allowing the minimum duration calculation to be measured from the submitted start time. (GAP-224)

#### Status
- If target is absent: test order workflow step moves to **Ready**, status moves to **ResultEntered**, and result status is set to **NotDetected**.
- If presumptive positive: test order advances to the confirmatory plating step in status **InProgress**.

#### Errors
- "Could not record this observation."
- "Select an observation."

#### What happens next
If presumptive positive, the analyst proceeds to set up confirmatory plating. If target is absent, the test order is finalized and queued for technical review.


### 16.4 Confirmatory Plating, Agreement, and Analyst Decision
<!-- WF-PATH-001 -->
#### Purpose
Perform confirmatory plating on permitted differential media, record plate observations, evaluate confirmatory agreement, and record the analyst decision to conclude testing as Detected or proceed to biochemical confirmation.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head).

#### Before you start
- The preceding selective plating step must have been recorded with **Growth matching expected appearance**.
- Released lots of permitted confirmatory media and eligible incubators must be available.

#### What you see
- Setup phase (`ConfirmatoryPlatingPanel`):
  - Target organism label: **Target organism**.
  - Instruction: "Tick every medium you actually plated, then choose its lot and incubator. Only media the step template permits are listed."
  - Checkboxes for each permitted differential medium.
  - Per-medium dropdowns for media lot and incubator.
  - Informational note: "Each medium keeps its own window; the shared incubation runs to the longest one selected."
  - Action button: **Start Incubation**.
- Readout phase (enabled once the declared window end has elapsed):
  - Instruction: "Incubation complete. Read each plate against its expected appearance."
  - Checkbox: **This medium was actually plated** (confirmed for each plated medium).
  - Observation radio choices per medium: **No growth**, **Growth, does not match expected appearance**, or **Growth matching expected appearance**.
  - Action button: **Submit Observations**.
- Analyst decision block (visible after all observations are submitted and evaluate to AllConforming):
  - Action button: **Conclude as Detected** (flagged as submitted without biochemical confirmation).
  - Action button: **Proceed to Biochemical Confirmation**.

[SCREENSHOT SS-162]

#### How to use it
1. In the confirmatory plating step, tick the checkbox for each permitted confirmatory medium actually inoculated.
2. For each selected medium, choose the **Media lot** and **Incubator**.
3. Click **Start Incubation**.
4. When the incubation period elapses (the shared incubation window runs to the longest maximum duration among selected media), open the readout view.
5. Confirm that **This medium was actually plated** is checked for each medium.
6. Select the observed colony appearance for each medium.
7. Click **Submit Observations**.
8. In the decision block:
   - If all media show conforming growth (AllConforming), choose either **Conclude as Detected** to finalize the test immediately, or click **Proceed to Biochemical Confirmation** to perform biochemical identification.
   - If observations are mixed or non-conforming (Inconclusive), the system flags the result for investigation, and only the biochemical confirmation path remains available.

#### System behavior
- Confirmatory setup validation:
  - At least one confirmatory medium must be selected.
  - Every selected medium must belong to the step's permitted media list and have a designated media lot and incubator.
  - The system creates a single shared incubation record whose start time is set to the analyst-supplied start time and whose end time runs to the largest maximum duration among the selected media.
- Confirmatory readout validation:
  - Exactly one observation must be submitted for each selected medium.
  - Readout is refused until the declared incubation window end has elapsed: "This step's incubation period has not finished yet." (Bypassed if supervisor executed Skip Wait).
- Agreement outcome:
  - If all media observations are **Growth matching expected appearance**, the outcome is evaluated as **AllConforming**.
  - Any other combination is evaluated as **Inconclusive**. The system records a workflow history note: "Confirmatory plating inconclusive - flagged for investigation." and flags the test order as `InconclusiveResult`. Inconclusive results do not offer the option to conclude as Detected; testing must proceed to biochemical confirmation.
- Analyst decision:
  - An analyst decision is available only after an **AllConforming** outcome.
  - Clicking **Conclude as Detected** records the history note: "Analyst decision: submitted as Detected without biochemical confirmation.", sets final test result to **Detected**, attaches the flag `BiochemicalNotPerformed`, advances the test order to **Ready**, and auto-submits the section for review when all tests are Ready.
  - Clicking **Proceed to Biochemical Confirmation** records the history note: "Analyst decision: proceed to biochemical confirmation." and unlocks the biochemical test step.
- Electronic signature:
  - Confirmatory plate submissions and analyst decisions do not require an electronic signature or password.

#### Validation
The system enforces the following exact validation rules:
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

> **Note for reviewers:** Concluding a pathogen test as Detected without biochemical confirmation does not require an electronic signature or password. (GAP-216)

#### Status
- If concluded as Detected: test order workflow step moves to **Ready**, status moves to **ResultEntered**, result status is **Detected**, with flag `BiochemicalNotPerformed`.
- If proceeding to biochemical confirmation: test order moves to the biochemical step in status **InProgress**.

#### Errors
- "Select an observation for: {media}."
- "Check at least one medium that was actually plated."
- "Every checked medium needs a media lot and an incubator."

#### What happens next
If concluded as Detected, the test is submitted for review. If proceeding to biochemical confirmation, the analyst performs biochemical testing.


### 16.5 Biochemical Test Confirmation and Finalization
<!-- WF-PATH-001 -->
#### Purpose
Record biochemical test results (identification gallery or kit), attach supporting analytical documentation, and formally finalize the qualitative pathogen test order.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head).

#### Before you start
- The preceding confirmatory plating step must be completed with either an **AllConforming** or **Inconclusive** outcome (or following a conforming selective plating step).
- Biochemical identification kit or reagents must be completed.

#### What you see
- Biochemical test panel (`BiochemicalTestPanel`):
  - Result description text input field: **Biochemical Result \*** (mandatory).
  - Organism detected toggle / radio: **Organism Detected?** (**Yes** or **No**).
  - Optional file upload: **Supporting Attachment (optional)**.
  - Action buttons: **Proceed to Biochemical Test** and **Submit as Detected (skip biochemical)** (visible when conforming).

#### How to use it
1. In the **{testCode} Workflow** dialog, open the biochemical test step.
2. In the **Biochemical Result \*** field, enter the identification findings (such as analytical gallery profile number or identification percentage).
3. Under **Organism Detected?**, select **Yes** if the biochemical profile confirms the target pathogen, or **No** if the profile excludes the target pathogen.
4. Optionally attach supporting analytical files or instrument printouts.
5. Click **Record Biochemical Result**.
6. Review the finalized analytical result and click **Done / Close**.

#### System behavior
- Step prerequisites:
  - The server verifies that preceding steps were completed and conforming: "A biochemical test is only available after a conforming result on \"{step}\"."
- Final result assignment:
  - If **Organism Detected?** is **Yes**, the final result is set to **Detected**.
  - If **Organism Detected?** is **No**, the final result is set to **NotDetected**.
- Test finalization:
  - If this is the final biochemical step in the template, the system writes the final `Result` entity, updates test order workflow step to **Ready**, and updates status to **ResultEntered**.
  - Auto-submits the laboratory section to **UnderReview** when all section tests are Ready.
- Reviewer return mechanism:
  - A technical reviewer holding supervisory permissions (`TestWorkflow.BiochemicalDecision`) may return a test that was concluded as Detected without biochemical confirmation back to **Incubating** for mandatory biochemical testing, logging a review event with a mandatory explanation reason.
- Electronic signature:
  - Recording biochemical results does not require an electronic signature.

#### Validation
The system enforces the following exact validation rules:
- "A biochemical result is required."
- "A biochemical test is only available after a conforming result on \"{step}\"."
- "Test order {id} is already at {step} - its workflow can no longer be submitted against."
- Reviewer return:
  - "A reviewer cannot decide on a result they performed."
  - "A reason is required when returning a result for biochemical confirmation."

#### Status
- Test order workflow step advances to **Ready**.
- Test order status updates to **ResultEntered**.
- Result status is set to **Detected** or **NotDetected**.

#### Errors
- "Could not record biochemical result."

#### What happens next
The pathogen test order is complete, locked against further changes, and queued for technical review.


### 16.6 Pathogen Testing Session Workspace
<!-- WF-PATH-002 -->
#### Purpose
Execute multi-sample, multi-location pathogen testing campaigns through an integrated six-step session workspace, covering shared TSB pre-enrichment, a primary Location × Test observation matrix, batch confirmatory plating, and final analytical completion.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head).

#### Before you start
- The sample must be prepared with configured monitoring locations (rooms, machine parts, or sampling points).
- The sample must have assigned pathogen test orders utilizing shared TSB broth.
- A released, unexpired TSB media lot and operational incubator must be available.

#### What you see
- Workspace title: **Pathogen Testing Session Workspace**
  - Subtitle: "Session: {sessionId} · Sample: {ref} ({area})"
  - Header actions: **Reset Session** (supervisor only).
- Six-step workflow stepper:
  1. **Overview**
  2. **Shared TSB**
  3. **Test Workflows**
  4. **Primary Matrix**
  5. **Confirmatory Plating**
  6. **Review & Complete** (locked until all required matrix and confirmatory results are recorded).
- Step details:
  - **Overview panel** (`SessionOverviewPanel`): summary cards for **Sampling Locations**, **Assigned Tests**, **Required Results ({locations} × {tests})**, and **Completed ({pending} pending)**; buttons **Open Shared TSB Setup** and **Open Test Workflows**.
  - **Shared TSB panel** (`SharedTsbEnrichmentPanel`): **TSB Media Lot (GPT Passed & Released) \***, **Incubator Equipment (In-Service) \***, read-only **Required Incubation Temperature \*** and **Required Incubation Time \***; button **Start TSB Enrichment**; locked state "LOCKED: Execution in Progress".
  - **Primary Matrix panel** (`PrimaryObservationMatrixPanel`):
    - Two-dimensional grid: rows represent sampling locations; columns represent assigned pathogen and count tests.
    - Cell inputs: quantitative tests display CFU number inputs (min 0, max 10000, step 1); qualitative tests display observation dropdowns with options: "Select Observation", "No Growth (-)", "Growth Non-Conforming", and "Growth Conforming (+)".
    - Summary counts: "Recorded: N", "Available: N", "Locked: N" (locked cells show tooltip: "Locked until upstream workflow step completes").
    - Action buttons: **Fill All Unset as No Growth (-)**, **Save Observations**, and **Proceed to Confirmation** / **Review & Complete**.
    - System confirmation: "Primary plate observations saved successfully with ALCOA+ contemporaneous tracking!".
  - **Confirmatory Plating panel** (`BatchConfirmatoryPlatingPanel`):
    - Subtitle: "Confirmatory Plating (Panel B)" with chip "N Flagged Locations" (locations exhibiting conforming or non-conforming growth).
    - Phase 1 (Shared Media Setup): media lot and incubator pickers per required confirmatory medium, and **Actual Incubation Start Time \***. Button: **Start Confirmatory Incubation**.
    - Phase 2 (Plate Observations): radio options per medium (**No Growth**, **Non-Conf**, **Conf (+)**). Chips: **Detected (+): n**, **Not Detected (-): n**, **Inconclusive (Retest): n**, **Pending Reads: n**. Button: **Save Readings & Continue**.
  - **Review & Complete panel** (`SessionReviewPanel`):
    - Table: **Final Analytical Results Summary** displaying analytical evidence chain (1. Primary Plate Observation, 2. Confirmatory Plating, 3. Agreement Evaluation).
    - Result chips: **Detected (+)**, **Not Detected (-)**, **Inconclusive (Retest)**, or quantitative **N CFU**.
    - Action button: **Complete Testing Session** (displays "Session Completed & Submitted" on success).
    - Warning banner when incomplete: "There are N missing results. Please complete all Location × Test entries before final completion." with button **Edit Matrix**.

[SCREENSHOT SS-163]
[SCREENSHOT SS-164]

#### How to use it
1. In the workspace, select the sample and click **Open Pathogen Workflow**.
2. In the **Overview** step, review assigned sampling locations and required test orders, then click **Open Shared TSB Setup**.
3. In the **Shared TSB** step, select the **TSB Media Lot** and **Incubator Equipment**, and click **Start TSB Enrichment**.
4. Allow TSB broth to incubate. During incubation, downstream matrix cells remain locked ("LOCKED: Execution in Progress").
5. When TSB incubation concludes, navigate to the **Primary Matrix** step.
6. For each location row and test column:
   - For qualitative pathogen tests, select **No Growth (-)**, **Growth Non-Conforming**, or **Growth Conforming (+)**. You may use **Fill All Unset as No Growth (-)** to quickly populate negative rows.
   - For quantitative count tests, enter the observed colony count (CFU).
7. Click **Save Observations**.
8. If any location exhibited conforming or non-conforming growth, proceed to **Confirmatory Plating (Panel B)**:
   - Set up the required differential media lots and incubator, enter the actual incubation start time, and click **Start Confirmatory Incubation**.
   - After incubation, record the plate observation (**No Growth**, **Non-Conf**, or **Conf (+)**) for each medium at each flagged location.
   - Click **Save Readings & Continue**.
9. In the **Review & Complete** step, review the analytical summary and agreement evaluations for each location and test.
10. Click **Complete Testing Session**.

#### System behavior
- Shared TSB execution:
  - Starting TSB enrichment synchronizes and starts incubation across all assigned pathogen tests on the sample. Downstream matrix entries are locked until TSB incubation is complete.
- Primary matrix evaluation:
  - Observations of **No Growth (-)** evaluate directly to **Not Detected (-)** (status Absent).
  - Observations of **Growth Conforming (+)** or **Growth Non-Conforming** are marked **PendingConfirmation** and flag the location for confirmatory plating.
- Confirmatory media count:
  - Enforces the exact number of confirmatory media configured in the Test Master (defaults to 2 for test codes containing "SALMONELLA", otherwise 1): "Pathogen {code} requires exactly {n} confirmatory media; received {m}."
- Confirmatory agreement evaluation:
  - Once all confirmatory media are read for a location, the server evaluates agreement:
    - If all confirmatory media show **Growth Conforming (+)**: evaluates to **Detected (+)**.
    - If all media show **No Growth** or all show **Growth Non-Conforming**: evaluates to **Not Detected (-)**.
    - Any other combination: evaluates to **Inconclusive (Retest)**.
- Session completion gate:
  - Completion is refused if any Location × Test result is missing, or if any test order is still incubating: "Cannot complete testing session: {n} required Location × Test result(s) are missing (...)"
  - On successful completion:
    - Every test order of the sample is advanced to workflow step **Ready** and status **ResultEntered**.
    - The overall sample status is set directly to **UnderReview** (bypassing section roll-up checks).
    - Writes a review event log: "Testing session completed - submitted for review".
- Electronic signature:
  - Completing a pathogen testing session does not require an electronic signature or password.

#### Validation
The system enforces the following exact validation rules:
- Shared TSB:
  - "Please select a valid released TSB media lot and an in-service incubator."
  - "Media lot #{lot} expired on {date}."
  - "None of the assigned tests on this sample require TSB enrichment."
  - "Media lot #{lot} ({name}) is not the TSB medium of any test on this sample."
  - "The shared TSB can't start: incubator {code}'s set point is outside the {range} range of \"{medium}\" on test {code}."
- Matrix and confirmatory setup:
  - "Cannot enter primary observations for {code}: TSB incubation or required workflow steps are still in progress."
  - "Location #{id} does not have a GrowthConforming or GrowthNonConforming primary observation and cannot proceed to confirmation."
  - "Pathogen {code} requires exactly {n} confirmatory media; received {m}."
  - "Material #{id} is not on the permitted media list of step \"{step}\"."
  - "Incubator {code}'s set point is outside the {range} range of \"{medium}\"."
- Completion:
  - "Cannot complete testing session: {n} required Location × Test result(s) are missing ({first 5 as Location (Test)}{, and k more})."
  - "Cannot complete testing session: Assigned tests {codes} have incomplete workflow prerequisites."

> **Note for reviewers:** The client interface and server evaluate confirmatory agreement differently when one medium shows No Growth and another shows Non-Conforming growth; the client screen displays Not Detected while the server stores Inconclusive. (GAP-223)

> **Note for reviewers:** Pathogen session completion requires no electronic signature or password and sets the overall sample status directly to Under Review, bypassing section roll-up rules. (GAP-222)

> **Note for reviewers:** When results are missing at session completion, the client displays "Cannot complete session: {n} required results are still missing.", which differs from the server's detailed validation message. (GAP-210)

#### Status
- Every test order on the sample transitions to workflow step **Ready** and status **ResultEntered**.
- Overall sample status advances directly to **UnderReview** (displayed in the register as **Pending Review**).

#### Errors
- "Cannot complete session: {n} required results are still missing."
- "Failed to save observations."

#### What happens next
The entire testing campaign is submitted to the technical reviewer for verification.


### 16.7 Supervisor Pathogen Session Reset
<!-- WF-PATH-003 -->
#### Purpose
Enable a laboratory Section Head or System Administrator to completely reset an erroneous or compromised pathogen testing session, clearing all entered data and returning the sample to its initial received state for complete re-testing.

#### Who can use it
Requires the *Supervise test workflows* permission (by default: Section Head, System Administrator).

#### Before you start
- The sample must not be in **Voided** or **Rejected** status.
- Testing data must exist on the pathogen session that requires operational reset.

#### What you see
- In the Pathogen Testing Session Workspace header:
  - Action button: **Reset Session** (visible to supervisors).
- Confirmation dialog:
  - Title: **Reset Testing Session & Steps?**
  - Text input field: **Reason for Reset \*** (placeholder: "e.g. Test configuration error / Wrong media lot selected / Operational error").
  - Action buttons: **Cancel** and **Confirm Reset** (displays "Resetting..." while submitting).

#### How to use it
1. In the Pathogen Testing Session Workspace header, click **Reset Session**.
2. In the confirmation dialog, review the operational warning.
3. In the **Reason for Reset \*** field, enter a detailed explanation for why the testing session must be reset.
4. Click **Confirm Reset**.
5. Verify that the session workspace resets to its initial state.

#### System behavior
- Rejection and void blocks:
  - If the sample is Voided, reset is blocked: "Sample #{ref} has been voided and its testing session cannot be reset."
  - If the sample is Rejected, reset is blocked: "Sample #{ref} has been rejected and its testing session cannot be reset. A rejection is reversed through the approval workflow by a Section Head."
- Permanent data deletion:
  - The system permanently deletes all recorded observations, step results, plate count readings, analytical results, and incubation records across all test orders on the sample.
  - Clears all location CFU results and matrix entries.
- Workflow and status reversal:
  - Resets every test order on the sample to workflow step **Waiting** and status **Pending**.
  - If the sample was in **Under Testing** or **Pending Review** status, resets the sample status back to **Received**. Other statuses (such as Under Approval or Retest Requested) retain their status.
  - Sample preparation records are retained and not reset.
- Audit trail:
  - Writes an audit log entry with action `ResetWorkflowSteps` on entity `Sample`, capturing user identity, timestamp, reference number, and reason.
  - Writes a workflow history entry on each test order: "Testing session and workflow steps reset. Reason: {reason}".
- Electronic signature:
  - Session reset does not require an electronic signature or password.

#### Validation
The system enforces the following exact validation rules:
- "Sample #{ref} has been voided and its testing session cannot be reset."
- "Sample #{ref} has been rejected and its testing session cannot be reset. A rejection is reversed through the approval workflow by a Section Head."

> **Note for reviewers:** Resetting a pathogen testing session permanently deletes all entered observations, step results, readings, results, and incubations for all test orders on the sample. (GAP-221)

> **Note for reviewers:** The reset confirmation dialog requires an explanation reason, but if a blank reason is received the server applies default text ("Section Head requested session reset" from client, or "Analyst requested session workflow reset" on server). (GAP-217)

#### Status
- All test orders revert to workflow step **Waiting** and status **Pending**.
- If the sample was **InTesting** or **UnderReview**, sample status reverts to **Received**.

#### Errors
- "Sample #{ref} has been voided and its testing session cannot be reset."
- "Sample #{ref} has been rejected and its testing session cannot be reset. A rejection is reversed through the approval workflow by a Section Head."

#### What happens next
Analysts must re-initiate incubation setup and re-enter all plate observations and test counts from the beginning.
