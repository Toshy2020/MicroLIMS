## 15. Result Entry (count, observation and location-grid tests)

### 15.1 Single-Test Incubation and Media Selection
<!-- WF-COUNT-001 -->
#### Purpose
Select an approved media lot and an eligible incubator to initiate plate incubation for a single microbiological count test after sample preparation has been confirmed.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head).

#### Before you start
- The sample must be prepared (preparation status **Ready**). FP-section tests are exempt from preparation.
- An item specification for the test must exist, defining reporting units and limits.
- Step medium incubation hours and temperatures must be configured in the Test Master.
- A released media lot belonging to the active laboratory section's material stock must be available.
- An operational incubator with an active calibration and set point temperature matching the medium's temperature range must be available.

#### What you see
- Dialog title: **{testCode} Workflow** (subtitle: "{ref} · {sample name} · TO #{testOrderId}").
- Step phase: **select-media**
  - Informational requirement: "Requires {names or 'an approved'} media."
  - Incubation specification: "Required Temperature (Stage 1): {min}-{max} °C, Incubation Period: {h}-{h} hours"
  - Media selection dropdown: aria "Media batch" (placeholder: "Media Batch ({names} lots only)"; options formatted as "{lotNumber}, expires {date}").
  - Incubator selection dropdown: aria "Stage 1 Incubator" (placeholder: "Stage 1 Incubator ({min}-{max} °C)"; options formatted as "{name} ({code}), {setPoint}°C").
  - Warning banner (if no incubator matches): "No incubator is set to {min}-{max} °C for this step."
  - Action button: **Start Incubation** (disabled until both media batch and incubator are selected).

[SCREENSHOT SS-151]

#### How to use it
1. In the workspace, select the sample and locate the test order in the **Assigned Tests** section.
2. Click **Open Workflow** on the test order card to open the **{testCode} Workflow** dialog.
3. In the **Media batch** dropdown, select an approved, released media lot.
4. In the **Stage 1 Incubator** dropdown, select an incubator whose calibrated set point falls within the required temperature range.
5. Click **Start Incubation**.
6. Verify that the dialog transitions to the **awaiting-result** phase displaying: "Incubation in progress".

#### System behavior
- Workflow state progression:
  - Test order workflow step advances from **Waiting** to **Incubating**, and test order status changes from **Pending** to **InProgress**.
  - If the sample was in **Received** status, its overall status advances to **Under Testing**.
- Incubation record creation:
  - The system creates an incubation audit record logging the start time, expected reading time (`StartedAt + MinHours`), media lot identifier, incubator equipment code, stage number (Stage 1), and operating user.
- Electronic signature:
  - Selecting media and starting incubation does not require an electronic signature.

#### Validation
The system enforces the following exact validation rules:
- Preparation gate:
  - "Test Preparation must be completed and confirmed for sample {ref} before incubation can be started for step \"{step}\"."
- Media checks:
  - "Media lot \"{lot}\" is not released for use, out of stock, or rejected."
  - "This step requires a different medium. The selected lot is {name}."
  - "Step \"{step}\" has no media configured in Test Master."
  - "Media lot \"{lot}\" was prepared from another laboratory section's material and cannot be used for this test."
- Incubator checks:
  - "The selected incubator's set point is outside this medium's temperature range."
  - "Incubator \"{code}\" has no calibrated set point temperature."
  - "Incubator \"{code}\" calibration expired on {date}."
- Window configuration:
  - "The incubation window for {medium} on step {step} is not configured - set its incubation hours and temperature in Test Master."
- Workflow sequence:
  - "Workflow order violation: step \"{a}\" must be completed before \"{b}\"."
  - "Media has already been selected for step \"{step}\" - awaiting its result."

> **Note for reviewers:** The single-test media selection path verifies that the lot is released for use, not out of stock, and not rejected, but does not enforce a server-side check of the media lot expiry date. (GAP-220)

#### Status
- Test order workflow step transitions to **Incubating**.
- Test order status updates to **InProgress**.
- Sample status moves from **Received** to **Under Testing**.

#### Errors
- "Could not start incubation for this step."
- "Could not load this test's workflow."

#### What happens next
Plates remain in the incubator. The test dialog enters the awaiting-result phase, displaying the temperature range, duration, and expected reading date and time.


### 15.2 Two-Stage Incubation Transfer
<!-- WF-COUNT-001 -->
#### Purpose
Transfer incubating plates from Stage 1 incubation to a second incubator for Stage 2 incubation when performing two-stage count test workflows.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head).

#### Before you start
- Stage 1 incubation must have elapsed its configured minimum incubation duration (or a supervisor must have executed **Skip Wait**).
- The test template in the Test Master must have an active Stage 2 configuration specifying Stage 2 incubation hours and temperature range.
- An eligible Stage 2 incubator must be available.

#### What you see
- In the **{testCode} Workflow** dialog (awaiting-result phase):
  - Status display: "Incubation in progress (Stage 1 of 2)"
  - Action button: **Transfer to Stage 2 Incubation** (enabled once Stage 1 minimum duration has elapsed, or after supervisor override).
- Step phase: **transfer-stage-2**
  - Instructional text: "Stage 1 incubation is complete. Transfer plates to Stage 2 incubator ({min}-{max} °C) to start the second stage."
  - Inherited media label: "Media Lot (inherited): {lot}"
  - Incubator dropdown: aria "Stage 2 incubator" (placeholder: "Select Stage 2 Incubator ({min}-{max} °C)").
  - Action buttons: **Back** and **Start Stage 2 Incubation**.

#### How to use it
1. When Stage 1 incubation is complete, open the **{testCode} Workflow** dialog.
2. Click **Transfer to Stage 2 Incubation**.
3. In the **Select Stage 2 Incubator** dropdown, select an available incubator matching the Stage 2 temperature range.
4. Click **Start Stage 2 Incubation**.
5. Physically transfer the test plates to the selected Stage 2 incubator.

#### System behavior
- Minimum duration validation:
  - The system verifies that Stage 1 incubation has reached its minimum duration before permitting the transfer.
- Incubation record lifecycle:
  - Stage 1 incubation record is closed with outcome text: "Transferred to stage 2 incubation."
  - Stage 2 incubation record is opened, inheriting the media lot from Stage 1 and recording the Stage 2 start time, incubator, and stage number (Stage 2).
- Electronic signature:
  - Two-stage transfer does not require an electronic signature.

#### Validation
The system enforces the following exact validation rules:
- "Stage 1 incubation for step \"{step}\" requires at least {h} hours of incubation - not ready until {utc} UTC."
- "Stage 2 start time ({t}) cannot precede Stage 1 start time ({t}) UTC."
- "Step \"{step}\" has no stage 2 configuration."

> **Note for reviewers:** The server-side stage-2 transfer implementation does not re-validate the incubator set point against the medium temperature range during transfer; it relies on client filtering of in-range incubators. (GAP-220)

#### Status
- The test order remains in workflow step **Incubating** and status **InProgress** with the incubation stage updated to Stage 2 of 2.

#### Errors
- "Could not start stage 2 incubation."

#### What happens next
Plates remain in Stage 2 incubation until the Stage 2 incubation period completes, after which result entry becomes available.


### 15.3 Plate Count Result Entry and Automatic Calculations
<!-- WF-COUNT-001 -->
#### Purpose
Record raw plate counts (CFU) or non-numeric uncountable observations for an incubated test, calculate colony concentration based on dilution factors and plate averages, and evaluate compliance against configured specification limits.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head).

#### Before you start
- Incubation must be complete:
  - For single-stage count tests, the incubation window must be elapsed on the client (or skipped by supervisor).
  - For two-stage count tests, the Stage 2 incubation window end must have elapsed on the server.
- The item specification must define a Dilution Factor, a reporting Unit, and Alert, Action, or Specification limits.

#### What you see
- In the **{testCode} Workflow** dialog, phase **enter-result**:
  - **Dilution Factor** numeric input field (helper text: "Configured value from Specifications (N)", or "Direct count: dilution factor fixed at 1", or "Enter multiplier: 10 for 1:10 dilution, 100 for 1:100").
  - Dilution factor override controls: icon button aria "Override dilution factor" and button **Cancel Override**.
  - **Justification for override** text input field (displayed and mandatory whenever the entered dilution factor differs from the configured specification value; helper text: "Required: the entered value differs from what's configured on Specifications. This is captured in the audit trail.").
  - Plate reading rows:
    - Input fields labeled **Plate {i}** (placeholder: "Colony count or TNTC").
    - Individual plate removal button: aria "Remove reading {i}".
  - Button: **Add Plate** (adds an additional plate reading input).
  - Dynamic result calculation display: "Calculated result: {display}".
  - Non-numeric informational note (when TNTC or Uncountable is typed): "Non-numeric result: reviewer will decide accept or retest".
  - Action button: **Calculate** (disabled until at least one plate reading is entered; no electronic signature required).
- Phase **step-complete**:
  - Outcome banner: "Step recorded: {outcome}" with details: ": Average X, Calculated Y, Status Z".
  - For non-definitive steps: "{outcome} - this step must be repeated with a fresh media lot." with buttons **Proceed to Next Step** and **Retry This Step**.
  - For final steps: "Final result: {outcome}" and button **Done / Close**.

[SCREENSHOT SS-152]

#### How to use it
1. In the workspace, open the **{testCode} Workflow** dialog for the test order whose incubation is complete.
2. Click **Record Result**.
3. In the **Dilution Factor** field:
   - For direct-count categories (Water, Environmental Monitoring, After Cleaning), the dilution factor is fixed at 1.
   - For Product, Raw Material, and Packaging Material samples, verify the pre-populated configured dilution factor.
   - If an override is required, click the override icon, enter the new dilution factor, and enter a mandatory explanation in the **Justification for override** field.
4. In the **Plate 1** field, enter the colony count (e.g. `25`), or enter `TNTC` or `Uncountable` if colonies are too numerous to count.
5. If replicate plates were incubated, click **Add Plate** and enter the colony count for each additional plate (e.g. **Plate 2**).
6. Verify the live calculated result displayed on screen.
7. Click **Calculate**.
8. In the step completion view, review the recorded average, calculated result, and compliance status. Click **Done / Close**.

#### System behavior
- Dilution factor governance:
  - For direct-count sample categories (Water, Environmental Monitoring, After Cleaning), the dilution factor is forced to `1` regardless of user input.
  - For item-based samples, if no dilution factor is configured on the item specification, the server refuses result entry: "No Dilution Factor is configured for \"{code}\" on this item's Specifications. Configure it before recording a result."
  - If the entered dilution factor differs from the configured value without an explanation, the server refuses entry: "The entered Dilution Factor ({x}) differs from the configured value ({y}). A justification note is required."
  - The system stores the entered dilution factor, the configured dilution factor, an override flag, and the justification note in the database.
- Reporting unit:
  - The reporting unit must be configured on the item specification, else the server refuses entry: "No reporting Unit is configured for \"{code}\" on this item's Specifications. Configure it before recording a result."
  - The unit displayed is prefixed with "CFU/" if it does not already start with it. Fallback unit is "CFU/unit".
- Non-numeric calculations (TNTC / Uncountable):
  - If any plate reading equals `TNTC` or `Uncountable` (case-insensitive):
    - No average or calculated result is computed.
    - No limit comparison is performed.
    - Status is set to **RequiresReview**, with `HasNonNumericReading = true` and `RequiresReview = true`.
    - If the first non-numeric reading is `TNTC`, the reported result is set to `"TNTC"`. Otherwise, it is set to `"Uncountable"`.
    - The step completes, the test order moves to **Ready**, and the decision to accept or retest is escalated to the technical reviewer.
- Numeric calculations:
  - **Average**: arithmetic mean of all entered plate readings (no readings are excluded).
  - **CalculatedResult**: `Average × DilutionFactor` (stored with full precision).
  - **ReportedResult**:
    - If `CalculatedResult < DilutionFactor`, reports `"<{DF} {unit}"` (with DF rounded away from zero; a dilution factor of 1 reports `"<1 {unit}"`).
    - If `CalculatedResult >= DilutionFactor`, reports `Math.Round(CalculatedResult, MidpointRounding.AwayFromZero) + " " + unit` (rounded to a whole number).
- Specification limit comparison:
  - Evaluates `CalculatedResult` against configured limits in hierarchical priority order:
    1. **OutOfSpecification**: if the value exceeds the Spec limit.
    2. **ActionLimitExceeded**: if the value exceeds the Action limit.
    3. **AlertLimitExceeded**: if the value exceeds the Alert limit.
    4. **WithinLimits**: if none of the configured limits are exceeded (equal to a limit is not considered exceeded).
    5. **LimitsNotConfigured**: if none of the three limits parse to a valid numeric threshold.
  - Limit text conventions: plain numbers or `"NMT x"` represent upper bounds; `"NLT x"` represents lower bounds; `"a-b"` represents a range; `"t ± d"`, `"t +/- d"`, or `"t ± d%"` represents a tolerance range around a target. Unparseable text is treated as no limit without raising an error.
- Record creation and closure:
  - Writes a `CountTestReading` entity and a `Result` record with `RawValue` (comma-joined readings) and `InterpretedValue` ("{reported} ({status})").
  - Closes the active incubation record with outcome details.
  - Advances the test order step to **Ready** and status to **ResultEntered**.
  - Automatic review submission: when all non-superseded test orders of the laboratory section reach **Ready**, the section automatically advances to **Pending Review** with review event log: "All tests completed - automatically submitted for review".
- Electronic signature:
  - Microbiological count test result entry does not require an electronic signature.

#### Validation
The system enforces the following exact validation rules:
- "At least one plate reading is required to calculate an average."
- "Plate readings are required."
- "No Dilution Factor is configured for \"{code}\" on this item's Specifications. Configure it before recording a result."
- "The entered Dilution Factor ({x}) differs from the configured value ({y}). A justification note is required."
- "No reporting Unit is configured for \"{code}\" on this item's Specifications. Configure it before recording a result."
- "Media must be selected for step \"{step}\" before a result can be recorded."
- "EM/After Cleaning results must be submitted via the batch results endpoint."
- "Step \"{step}\" requires stage 2 incubation to be started before a count can be recorded."
- "This step's incubation period has not finished yet."

> **Note for reviewers:** The single-stage count result endpoint has no server-side elapsed-time check; the screen gates the calculation button only on the client. (GAP-202)

> **Note for reviewers:** Count test results and plate readings are not electronically signed at entry. (GAP-218)

> **Note for reviewers:** Non-numeric text other than TNTC or Uncountable is parsed on the server using standard decimal conversion with no custom validation message, and the server does not enforce that the entered dilution factor is greater than zero. (GAP-209, GAP-228)

#### Status
- Test order workflow step transitions from **Incubating** to **Ready**.
- Test order status updates from **InProgress** to **ResultEntered**.
- Result status is assigned one of: **WithinLimits**, **AlertLimitExceeded**, **ActionLimitExceeded**, **OutOfSpecification**, **LimitsNotConfigured**, or **RequiresReview**.
- When all tests of the section reach **Ready**, the laboratory section advances to **UnderReview** (displayed in the register as **Pending Review**).

#### Important rules
- Once a test order reaches **Ready**, its workflow cannot be resubmitted. There is no endpoint to modify or overwrite stored count results in place. Adjustments require the test to be returned by a reviewer or reset by a supervisor.

#### Errors
- "Could not record this result."
- "Enter at least one plate reading."
- "A justification note is required when the entered Dilution Factor differs from the configured value."

#### What happens next
The completed test order displays its calculated result and status badge in the workspace, and the sample advances to technical review once all companion tests are completed.


### 15.4 Batch Location Count Results (Environmental Monitoring, Water, and After Cleaning)
<!-- WF-COUNT-002 -->
#### Purpose
Enter raw colony plate counts across all monitored rooms, sampling points, or machine parts in a single batch grid, automatically calculating averages, compliance statuses, and worst-case batch summaries.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head).

#### Before you start
- Incubation must be complete for the test order. For templates with multiple incubation windows, all intermediate windows must have been completed.
- Incubation elapsed time must reach the configured minimum duration (or have been overridden via **Skip Wait**).

#### What you see
- For Environmental Monitoring and After Cleaning count batches (`LocationResultGridDialog`):
  - Dialog title: **{testCode} Results: {displayName}**
  - Table columns:
    - **Location** (room name or machine part)
    - **Limits (Alert / Action / Spec)**
    - **Plate Readings** (numeric inputs labeled with location name and plate index, with **Add Plate** button)
    - **Calculated Result**
    - **Status** (compliance badge per location)
  - Batch summary text: "{conform}/{total} locations within spec; worst status: {badge}"
  - Timing warning (before minimum duration elapses): "Results cannot be submitted before {datetime}."
  - Action button: **Submit Results** (displays "Submitting…" while processing).
- For Water count batches (`WaterLocationResultGridDialog`):
  - Table columns: **Sampling Point**, **Limits (Alert / Action / Spec)**, **Plate Readings**, **Average**, and **Status**.
  - Action button: **Submit Results**.

[SCREENSHOT SS-153]

#### How to use it
1. In the workspace, open the test order whose final incubation window has completed.
2. If intermediate incubation windows remain open, click **Advance to Next Incubation Window** after the minimum duration, select fresh media for the subsequent window, and allow it to incubate.
3. On the final incubation window, click **Record Results** to open the batch result grid.
4. For each location row, enter the plate counts in the plate reading fields.
5. If replicate plates were incubated for a location, click **Add Plate** and enter the additional count.
6. Verify that every location has at least one plate reading entered.
7. Click **Submit Results**.

#### System behavior
- Window progression and final window gate:
  - Batch submission is accepted only during the final incubation window (`IsFinalStep`).
  - Attempting to submit results on an intermediate window is refused: "\"{step}\" is not the final incubation window yet - close it and start the next window first."
  - Attempting to advance when on the final window displays: "This is the final incubation window - record results instead of advancing."
- Minimum duration check:
  - The server verifies that the elapsed time from incubation start exceeds the medium's minimum incubation hours: "This incubation window needs at least {h} hours - not ready until {utc} UTC."
- Completeness check:
  - The batch is all-or-nothing: every configured sample location must have at least one plate count reading, else the entire batch is rejected.
- Calculations for Environmental Monitoring and After Cleaning:
  - Dilution factor is fixed at `1`.
  - Calculated result equals the arithmetic average of plate readings for that location.
  - Reported result: if average `< 1`, reports `"<1"`. Otherwise reports the whole number rounded away from zero (`Math.Round(average, AwayFromZero)`).
  - Units derived from configuration or test type: passive air ("CFU/plate/4 hours"), surface/air ("CFU/25 cm2"), swab ("CFU/25 cm2"), rinse ("CFU/mL").
- Calculations for Water:
  - Reported result: `Math.Round(average, AwayFromZero)` rounded to a whole number. Water does not apply a `"<1"` rule; an average below 0.5 reports `"0"`.
  - Dilution factor is not stored.
- Batch summary roll-up:
  - Evaluates each location against Alert, Action, and Spec limits (Spec > Action > Alert).
  - Produces a collective batch summary: "{n} locations: {conform} conform, {rest} alert/action/spec (worst: {status})", where only WithinLimits is considered conforming.
- Workflow progression:
  - Closes the active incubation record.
  - Sets the test order workflow step to **Ready** and status to **ResultEntered**.
  - Automatically advances the laboratory section to **UnderReview** when all section tests are Ready.
- Electronic signature:
  - Batch location count submission does not require an electronic signature.

#### Validation
The system enforces the following exact validation rules:
- "No locations are assigned to this test order."
- "Results are missing for: {names}."
- "At least one plate reading is required for: {names}."
- "\"{step}\" is not the final incubation window yet - close it and start the next window first."
- "This incubation window needs at least {h} hours - not ready until {utc} UTC."
- "Media must be selected for this test before batch results can be recorded."

> **Note for reviewers:** The Environmental Monitoring out-of-trend rule from earlier design specifications ((Step 1 Count + Step 2 Count) > Action Limit) is not implemented in the application code. (GAP-211)

> **Note for reviewers:** Water batch results do not apply a less-than-one rule; an average count below 0.5 reports "0". (BR-COUNT-013)

#### Status
- Test order workflow step advances to **Ready**.
- Test order status advances to **ResultEntered**.
- Laboratory section advances to **UnderReview** (register label **Pending Review**) upon completion of all tests.

#### Errors
- "Could not record batch results."
- "Could not record batch readings."

#### What happens next
Individual location results and the overall batch summary are stored, and the test order is queued for technical review.


### 15.5 Batch Location Pathogen Results (Environmental Monitoring and After Cleaning)
<!-- WF-COUNT-002 -->
#### Purpose
Record qualitative presence/absence pathogen observations ("Growth Observed?" Yes or No) across all monitored locations for Environmental Monitoring and After Cleaning pathogen test orders.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head).

#### Before you start
The test order must be on its final incubation window with the minimum incubation duration satisfied.

#### What you see
- Dialog title: **{testCode} Results: {displayName}** (`PathogenLocationResultGridDialog`)
- Table columns:
  - **Location**
  - **Growth Observed?** (radio buttons: **Yes** and **No**)
  - **Status** (live badge: **Detected** when Yes; **Absent** when No)
- Summary display: "{detected}/{total} locations detected; overall: {Detected|Absent}"
- Action button: **Submit Results** (no electronic signature required).

#### How to use it
1. In the workspace, open the batch pathogen test order on its final incubation window.
2. For each location row, select **Yes** if characteristic pathogen growth was observed, or **No** if no growth was observed.
3. Verify that every location has a selection made.
4. Review the overall summary: if any location is marked Yes, the overall result is **Detected**; if all are No, the overall result is **Absent**.
5. Click **Submit Results**.

#### System behavior
- Mandatory completeness:
  - A selection (Yes or No) is required for every location. If any location is unselected, the server rejects submission: "A growth observation is required for every location."
- Result determination:
  - Location status is set to **Detected** if growth was observed, or **Absent** if no growth was observed.
  - Overall test result is set to **Detected** if at least one location has growth observed, else **Absent**.
  - Creates a summary record: "{n} locations: {absent} absent, {detected} detected (overall: {Detected|Absent})".
- Workflow progression:
  - Closes the incubation, sets test order workflow step to **Ready**, and updates status to **ResultEntered**.
- Electronic signature:
  - Batch pathogen observation submission does not require an electronic signature.

#### Validation
The system enforces the following exact validation rules:
- "A growth observation is required for every location."
- "Step \"{step}\" requires a growth observation for every location."
- "Results are missing for: {names}."
- "\"{step}\" is not the final incubation window yet - close it and start the next window first."
- "This incubation window needs at least {h} hours - not ready until {utc} UTC."

#### Status
- Test order workflow step advances to **Ready**.
- Test order status advances to **ResultEntered**.
- Overall result status is set to **Detected** or **Absent**.

#### Errors
- "Could not record batch pathogen results."

#### What happens next
The test order is finalized and moves to **Ready**, enabling sample technical review once companion tests conclude.


### 15.6 Qualitative Observation Results (Physicochemical)
<!-- WF-OBS-001 -->
#### Purpose
Record qualitative compliance observations ("Complies" or "Does not comply") for physicochemical test orders under mandatory electronic signature control.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head).

#### Before you start
- The sample must have configured qualitative or presence/absence specifications for the test in the Physicochemical Test Master.
- The test order must not be in **Ready** step.
- No active qualitative entry may already exist for the test order.

#### What you see
- Panel fields (`QualitativePanel`):
  - **Analysis time** datetime picker (cannot be in the future).
  - **Equipment (Physicochemical)** dropdown (optional; includes option "None (no instrument linked)").
  - **Comment (optional)** text field.
  - Parameter evaluation rows:
    - Parameter name and specification limits.
    - Radio choices: **Complies** and **Does not comply**.
    - Observation field: **Observation (optional)** when compliant; dynamically changes to **Observation (required) \*** with helper text "Observation is required when non-compliant" when non-compliant is selected.
  - Action button: **Sign and record result**.

[SCREENSHOT SS-154]

#### How to use it
1. In the FP or RM & PM workspace, open the assigned qualitative test order.
2. Select the **Analysis time**.
3. Optionally select the instrument from the **Equipment (Physicochemical)** dropdown.
4. For each parameter row, select **Complies** or **Does not comply**.
5. If **Does not comply** is selected, enter an explanation in the **Observation (required) \*** field (up to 500 characters).
6. Click **Sign and record result**.
7. In the electronic signature prompt, review the signature meaning: "I entered these qualitative observations and am recording this test result."
8. Enter your user password and confirm the signature.

#### System behavior
- Validation and compliance:
  - Every parameter specification configured for the test must be evaluated exactly once.
  - If any parameter does not comply, an observation description is strictly mandatory.
  - Analysis time cannot be set in the future (server checks not later than 5 minutes ahead).
- Test analysis and electronic signature:
  - The system applies an electronic signature record (`ResultRecorded`) capturing the user identity, timestamp, meaning, and password verification.
  - Stores a `TestAnalysis` record with parameter results: **Complies** is stored as `WithinLimits`; **Does not comply** is stored as `OutOfSpecification`.
- Workflow progression:
  - Test order workflow step advances to **Ready**, status advances to **ResultEntered**, and the laboratory section is automatically submitted for technical review when all tests reach **Ready**.

#### Validation
The system enforces the following exact validation rules:
- "Password is required to sign the result."
- "At least one parameter result is required."
- "Analysis time cannot be in the future."
- "An observation is required when result does not conform."
- "Observation cannot exceed 500 characters."
- "An active Qualitative entry already exists for this test order."
- "No specifications configured for this test and item."
- "Every specification for this test must be supplied exactly once."

> **Note for reviewers:** Qualitative physicochemical test results require an electronic signature with password verification at entry, whereas microbiological plate count and pathogen results do not require an electronic signature. (GAP-218)

#### Status
- Test order workflow step advances to **Ready**.
- Test order status updates to **ResultEntered**.
- Result compliance status is set to **WithinLimits** or **OutOfSpecification**.

#### Errors
- "Password verification failed. The signature was not applied."
- "Analysis time cannot be in the future."
- "An observation is required when result does not conform."

#### What happens next
The qualitative result is locked and committed to the audit trail, and the test order is ready for technical review.
