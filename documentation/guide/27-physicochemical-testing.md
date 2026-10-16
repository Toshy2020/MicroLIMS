## 27. Physicochemical Testing

MicroLIMS provides dedicated workspaces for physicochemical testing across Finished Products (**FP Workspace**, `/physicochemical/workspace` for organizational area `fp`) and Raw & Packaging Materials (**RM & PM Workspace**, `/physicochemical/rm-pm-workspace` for organizational area `rmpm`). Laboratory analysts assigned to the Physicochemical section (`FP`) access the workspace matching their assigned organizational area, or both workspaces if unassigned to a specific area.

In addition to standard bench testing panels within the test workflow dialog, MicroLIMS provides dedicated instrument workstations for high-performance liquid chromatography (**HPLC Workspace**), gas chromatography (**GC Workspace**), and inductively coupled plasma optical emission spectrometry (**ICP-OES Workspace**).

> **Important notice:** MicroLIMS does not have an automated instrument report import feature. Analytical measurements, peak areas, retention times, and instrument suitability parameters are transcribed manually by the analyst, and instrument report files (PDF, PNG, or JPEG) are uploaded as electronic evidence documents. (GAP-613)

---

### 27.1 Physicochemical Testing Workspaces (FP and RM & PM)
#### Purpose
Review assigned sample test orders, launch specialized test recording panels, verify specification limits, and track testing progress for finished products and raw/packaging materials.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head). Viewing workspaces is bounded by user organizational membership in the Physicochemical laboratory (`FP`) and assigned area (`fp` or `rmpm`).

#### Before you start
- The sample must be logged and received in the Physicochemical laboratory.
- Active item specifications must be established for the test on the sample's product and stage.
- For raw materials and packaging materials, sample preparation must be confirmed (preparation status **Ready**). Finished product tests are exempt from sample preparation.

#### What you see
- Navigation header showing active section (**FP**) and area (**Finished Product Workspace** or **RM & PM Workspace**).
- Tabbed sample queue: **Assigned to Me**, **All Active Samples**, **Under Testing**, and **Pending Review**.
- Test Order cards displaying: Sample reference (`{ref}`), item name, batch number, test code, stage role, analyst assignment, and action button **Open Workflow**.

#### How to use it
1. Navigate to **Laboratories** > **Physicochemical Laboratory** and select **FP Workspace** or **RM & PM Workspace**.
2. Locate the sample in the queue or search by sample code.
3. On the relevant test order card, click **Open Workflow** to open the specialized test dialog.
4. The system automatically loads the test recording panel configured for the test in the Test Master: **Measurement**, **Gravimetric**, **Qualitative**, **Titration**, **Dissolution**, **Disintegration**, or **Weight Variation**.
5. Complete testing according to the specific test procedure and submit the signed result.

#### System behavior
- Loading a test verifies section access and retrieves current item specifications, parameter limits, and target values.
- When an analyst enters and signs a result, the test order transitions to workflow step **Ready**.
- When every assigned test order for a sample section reaches **Ready**, the section automatically transitions to **Under Review** ("All tests completed - automatically submitted for review").

#### Status
- Test Order workflow step moves from **Assigned** / **InProgress** to **Ready**.
- Sample section status moves to **Under Review** once all section tests are **Ready**.

---

### 27.2 Measurement, Gravimetric, and Qualitative Test Results
<!-- WF-PHYSRESULT-001 -->
#### Purpose
Record and electronically sign analytical results for direct physical measurements, gravimetric determinations (Loss on Drying or Ash / Residue), and qualitative compliance observations.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head).

#### Before you start
- Test order exists, is not superseded, and is not already finalized at **Ready**, **Reviewed**, or **Approved**.
- No active result entry already exists for the test order ("An active ... entry already exists for this test order.").
- Item specifications must be configured for the test ("No specifications configured for this test and item.").
- Selected equipment must be active and registered in the Finished Product (`FP`) section.

#### What you see
- Dialog title: **{testCode} Workflow** (subtitle: "{ref} · {sample name} · TO #{testOrderId}").
- **Analysis configuration** header displaying chips for configured settings:
  - Measurement: chips **Basis: {Mean|Min|Max|EachValue}** and **Replicates: {N}**.
  - Gravimetric: chips **Loss on Drying** or **Ash / Residue**, **Replicates: {N}**, and **Uses Tare**.
- Entry form controls:
  - **Analysis time**: date and time picker (must not be in the future).
  - **Equipment (Physicochemical)**: dropdown listing active FP instruments or "None (no instrument linked)".
  - Parameter entry grid:
    - Measurement: numeric input fields labeled **Rep 1** through **Rep N** for each specification parameter.
    - Gravimetric: per replicate, fields for **Initial weight (g)**, **Final weight (g)**, and **Container weight (g)** (visible and required only when tare is used).
    - Qualitative: radio toggle between **Complies** and **Does not comply**, with field **Observation (required) \*** (mandatory if non-compliant) or **Observation (optional)**.
  - **Comment (optional)**: free-text field.
  - Returned revision alert (if previously returned): "Returned for revision: {reason}".
  - Action button: **Sign and record result** (disabled until all required inputs are complete and valid).

[SCREENSHOT SS-271]
[SCREENSHOT SS-272]

#### How to use it
1. In the test dialog, select the **Analysis time** (local date and time).
2. Select the analytical balance or apparatus in **Equipment (Physicochemical)**.
3. Enter replicate readings according to the panel type:
   - **Measurement**: Enter exactly N numeric readings for each parameter.
   - **Gravimetric**: Enter initial weight, final weight, and (if tare is enabled) container weight for each replicate.
   - **Qualitative**: Select **Complies** or **Does not comply**. If selecting **Does not comply**, enter a detailed description in **Observation (required) \*** (up to 500 characters).
4. Click **Sign and record result**.
5. In the **Electronic Signature** dialog, verify the signing statement:
   - Measurement: "I entered these measurement readings and am recording this test result."
   - Gravimetric: "I entered these gravimetric measurements and am recording this test result."
   - Qualitative: "I entered these qualitative observations and am recording this test result."
6. Enter your password and optional comment, then click **Sign**.

#### System behavior
- The server validates all readings and executes official calculations:
  - **Gravimetric Loss %**: `Loss % = (W1 - W2) * 100 / W1` (where `W1 = Initial - Container`, `W2 = Final - Container`).
  - **Gravimetric Residue %**: `Residue % = W2 * 100 / W1`.
  - **Measurement**: Computes Mean, Min, Max, sample standard deviation ($n-1$ for $n \ge 2$), and RSD. Evaluates compliance against specification limits based on the configured basis (**Mean**, **Min**, **Max**, or **EachValue** where every individual replicate must comply).
  - **Qualitative**: **Complies** evaluates to **WithinLimits**; **Does not comply** evaluates to **OutOfSpecification**.
- Stores the signed record with calculation JSON and audit trail meaning `ResultRecorded`.
- Displays the completion view with a verdict banner: **WithinLimits**, **OutOfSpecification**, or **RequiresReview**.

#### Validation
- Time gate:
  - "Analysis time cannot be in the future."
- Order state:
  - "Test order {id} is already at {status} - its workflow can no longer be submitted against."
  - "An active {type} entry already exists for this test order."
  - "Test order sample must be linked to an item with specifications."
  - "No specifications configured for test {code}."
  - "No specifications configured for this test and item."
- Measurement inputs:
  - "Each parameter must have exactly {N} readings."
- Gravimetric inputs:
  - "Initial sample weight (W1) must be greater than zero."
  - "Final weight (W2) cannot be negative."
  - "Final weight (W2) cannot be greater than initial weight (W1)."
  - "Container weight is required when tare is used."
  - "Container weight must be null when tare is not used."
- Qualitative inputs:
  - "An observation is required when result does not conform."
  - "Observation cannot exceed 500 characters."
- Equipment and conditions:
  - "Equipment \"{code}\" is not active."
  - "Equipment \"{code}\" is not in the Finished Product (FP) section."
  - "Condition field \"{label}\" is required."

#### Status
- Test Order workflow step transitions to **Ready**.
- Test Order result status transitions to **WithinLimits**, **OutOfSpecification**, or **RequiresReview**.

#### What happens next
The result is locked. When all tests for the sample section are **Ready**, the section automatically submits for technical review. The user who recorded the result cannot review or approve the sample.

---

### 27.3 Titration Testing
<!-- WF-PHYSRESULT-002 -->
#### Purpose
Record volumetric and Karl Fischer titrations using standardized titrants, calculate sample content or assay percentage, acknowledge any standardization warnings, and electronically sign results.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head).

#### Before you start
- The test must be configured as a titration test in the Test Master (Titration type: AcidBase, Redox, Complexometric, Precipitation, or KarlFischer; Mode: Direct or Residual; Calculation: UspFactor or Relative; Endpoint: Visual or Potentiometric).
- A **Prepared**, unexpired volumetric titrant preparation (`VS-...`) must be available in the laboratory with an active standardization factor.
- For residual titrations, a Prepared excess titrant preparation must be available.
- For relative titrations, a reference standard or approved working standard lot with recorded purity and available stock must be available.

#### What you see
- Sections: **Method**, **Analysis set-up**, **Blank (one per series)**, and (for relative mode) **Reference standard titrations (1-3, same lot)**.
- Method details: Titration type, calculation mode, endpoint, and required replicate count (1 to 10).
- Fields:
  - **Titrant preparation**: dropdown of available standardized titrants.
  - **Excess titrant preparation**: visible only for residual titrations.
  - **Blank volume (mL)**: visible only if the method uses a blank.
  - **Titration temperature (°C)**: visible only when temperature correction is enabled.
  - **Loss on drying / water (%)**: required for dried or anhydrous result bases (0 to $< 100\%$).
  - **Average unit weight (mg)**: required for label claim or mg/unit result bases.
  - Replicate rows: **Sample Weight (mg)** and **Titrant Volume (mL)**.
  - Due titrant warning panel (if applicable): checkbox **Acknowledge due titrant** and justification text box (10 to 500 characters).
  - Action button: **Sign and record result**.

[SCREENSHOT SS-273]

#### How to use it
1. Select the **Titrant preparation** from the dropdown. If standardization is due in Relative mode, review the warning.
2. Select the **Analysis time** and optional **Equipment (titrator)**.
3. If the test uses a blank, enter the single **Blank volume (mL)** for the analysis series.
4. If temperature correction is active, enter the **Titration temperature (°C)**.
5. If testing on a dried or anhydrous basis, enter **Loss on drying / water (%)**. If testing for label claim or mg/unit, enter **Average unit weight (mg)**.
6. Enter sample weight and consumed titrant volume for each required replicate.
7. For relative titrations, select the standard lot and enter 1 to 3 standard titrations (standard weight and titre).
8. If a titrant warning is displayed, check the acknowledgement box and enter a justification (10 to 500 characters).
9. Click **Sign and record result**.
10. In the **Electronic Signature** dialog, review the signing statement:
    - Standard: "I entered these titration measurements and am recording this test result."
    - With due titrant: "Your signature records the result and your acknowledgement of the due titrant."
11. Enter your password and click **Sign**.

#### System behavior
- **Gating on titrant factor**:
  - Unstandardized titrant is blocked: "This titrant preparation has not been standardized."
  - **UspFactor** calculation: Due titrant is blocked: "Titrant standardization is due - restandardize before use." Titrants standardized "before each use" must have been standardized on the current laboratory day: "This titrant must be restandardized before each use - it was not standardized today."
  - **Relative** calculation: Due titrant is permitted with a warning: "Titrant standardization is due - factor not used in the relative calculation." Requires acknowledgement and a 10–500 character justification.
- **Calculations**:
  - *Direct UspFactor*: `mg = (V - B) * N * f * F` (for Karl Fischer: `N` is mg water/mL, `F = 1`).
  - *Residual with blank*: `mg = (B - V) * N_back * f_back * F`.
  - *Residual without blank*: `mg = (V_ex * N_ex * f_ex - V * N * f) * F`.
  - *Relative*: `K_s = W_std * (P / 100) * ((100 - MC) / 100) / net_std`; `K = mean(K_s)`; `mg = net * K`.
  - *Temperature correction* (non-aqueous acid-base): `V' = V * (1 + (T_std - T_t) * 0.0011)`.
  - *Result bases*: Percent As Is (`mg/W * 100`), Dried/Anhydrous (`PercentAsIs * 100 / (100 - LOD)`), Percent Label Claim (`mg/W * AvgUnitWt / LC * 100`), or Mg Per Unit (`mg/W * AvgUnitWt`).
- **Reporting & Evaluation**:
  - Reported value is the mean rounded half away from zero to the maximum of 2 or the specification limit decimals.
  - If replicate RSD exceeds the configured maximum, status changes from **WithinLimits** to **RequiresReview** with warning: "RSD exceeds the configured maximum of X % - requires review."
- **Stock deduction**: For relative titrations, consumed standard weights are deducted from the standard stock lot in the same save.
- **Signatures recorded**: `ResultRecorded`, preceded by `TitrantDueAcknowledged` if a due titrant was acknowledged.

#### Validation
- "Test \"{name}\" is not fully configured as a titration test in Test Master."
- "The titrant has no current factor."
- "The titrant is due for standardization - acknowledge it and give a justification to continue."
- "The justification must be 10 to 500 characters."
- "No titrant warning to acknowledge."
- "Exactly {N} replicate(s) are required."
- "Replicate {N}: sample weight must be greater than zero."
- "Replicate {N}: titrant volume must be greater than zero."
- "The titrant volume must be greater than the blank volume."
- "The blank volume must be greater than the sample titrant volume."
- "The back-titrated amount exceeds the excess titrant added."
- "A blank volume is required for this test."
- "This test does not use a blank."
- "The titration temperature is required for temperature correction."
- "The excess titrant preparation is required for residual titration."
- "An excess titrant is only used in residual titration."
- "Loss on drying / water (0 to below 100) is required for a dried or anhydrous basis."
- "The average unit weight (mg) is required for a label claim or mg per unit basis."
- "One to three standard titrations are required for the relative calculation."
- "All standard titrations must use the same standard lot."
- "The standard lot must be a reference standard or an approved working standard."
- "Standard lot {lot} has no purity recorded."

> **Note for reviewers:** The site configuration dictates which specific titration types, calculation modes, and endpoint methods are active in production. (GAP-609)

#### Status
- Test Order workflow step transitions to **Ready**.
- Result status evaluates to **WithinLimits**, **OutOfSpecification**, or **RequiresReview**.

---

### 27.4 Staged Testing: Dissolution, Disintegration, and Weight Variation
<!-- WF-PHYSRESULT-003 -->
#### Purpose
Record multi-stage physical and performance tests where failure to meet Stage 1 criteria triggers immediate progression to Stage 2 (or Stage 3) testing.

#### Who can use it
Requires the *Execute test workflows* permission (by default: Analyst, Reviewer, Section Head).

#### Before you start
- **Dissolution**: The test order must be assigned to an active HPLC run with a passed system suitability test (SST), and the HPLC method must have exactly one analyte with a standard dilution configured.
- **Disintegration**: Disintegration time limit (minutes) must be configured on the specification.
- **Weight Variation**: Dosage form (Tablets or Capsules) must be configured on the item specification.

#### What you see
- **Dissolution Panel**:
  - Sections: **Analysis parameters & conditions**, **HPLC run standard** (standard concentration $C_s$ and mean standard area $A_s$ loaded from the assigned HPLC run), **Stage 1 vessel peak areas (6 units)**, **Previously recorded stages**, and **Analysis summary**.
  - Fields: **Medium Volume (mL)**, **Sample Dilution Factor** (default 1), and 6 vessel peak area inputs.
  - Action button: **Sign & Record Stage 1 Result**.
- **Disintegration Panel**:
  - 6 unit inputs for Stage 1: unit time in minutes, or checkbox **Not disintegrated**.
  - Action button: **Sign & Record Stage 1 Result**.
- **Weight Variation Panel**:
  - Tablets: 20 unit weight inputs (mg).
  - Capsules: per unit, **Gross weight (mg)** and **Shell weight (mg)**.
  - Action button: **Sign & Record Stage 1 Result**.

[SCREENSHOT SS-274]
[SCREENSHOT SS-275]

#### How to use it
##### Dissolution:
1. Open the test order. Confirm that the **HPLC run standard** displays the standard values from the assigned run.
2. Enter the **Medium Volume (mL)** and **Sample Dilution Factor**.
3. Enter the 6 vessel peak areas.
4. Click **Sign & Record Stage 1 Result** and sign with your password (statement: "I entered these stage 1 dissolution vessel results and am recording this test result.").
5. If Stage 1 complies, the test order moves to **Ready**.
6. If Stage 1 requires Stage 2 ("Stage 2 needed"), enter the additional 6 vessel peak areas in the Stage 2 section and click **Sign & Record Stage 2 Result**.
7. If Stage 2 requires Stage 3 ("Stage 3 needed"), enter the additional 12 vessel peak areas and click **Sign & Record Stage 3 Result**.

##### Disintegration:
1. Enter the disintegration time (minutes) for each of the 6 units in Stage 1, or check **Not disintegrated**.
2. Click **Sign & Record Stage 1 Result** and sign with your password.
3. If 1 or 2 units fail, the panel displays: "N unit(s) failed to disintegrate within L min. Proceed to Stage 2."
4. In the **Stage 2 unit entry (12 additional units)** section, enter times for units 7 through 18.
5. Click **Sign & Record Stage 2 Result** and sign with your password.

##### Weight Variation:
1. For tablets, enter the individual weights for all 20 units. Tablets do not have a Stage 2.
2. For capsules, enter gross and shell weights for the 20 Stage 1 units.
3. Click **Sign & Record Stage 1 Result** and sign.
4. If capsule Step A passes (all gross weights within inner limit), Stage 1 complies immediately.
5. If net weight variation requires Stage 2 (3 to 6 units exceed inner limit), enter the additional 40 units in **Stage 2 unit entry (40 additional units)** and click **Sign & Record Stage 2 Result**.

#### System behavior
- **Dissolution calculation**:
  - Standard concentration: $C_s = W_{std} \times \frac{P}{100} \times \frac{100 - MC}{100} / \text{Dilution}_{std}$.
  - Vessel % dissolved: $\text{Vessel } \% = \frac{A_u \times C_s \times V \times DF \times 100}{A_s \times LC}$.
  - Stage 1 ($n=6$): Each unit $\ge Q + 5\%$ evaluates to **Complies**; otherwise **NextStageRequired**.
  - Stage 2 ($n=12$): Mean $\ge Q$ and no unit $< Q - 15\%$ evaluates to **Complies**; otherwise **NextStageRequired**.
  - Stage 3 ($n=24$): Mean $\ge Q$, at most 2 units $< Q - 15\%$, and no unit $< Q - 25\%$ evaluates to **Complies**; otherwise **DoesNotComply**.
  - Reported value is the mean of all tested units rounded to 0 decimals.
- **Disintegration evaluation**:
  - Stage 1 ($n=6$): 0 units fail = **Complies**; 1 or 2 fail = **NextStageRequired**; $> 2$ fail = **DoesNotComply**.
  - Stage 2 ($n=18$ total): $\ge 16$ units pass = **Complies**; otherwise **DoesNotComply**.
- **Weight variation evaluation**:
  - Tablets ($n=20$): Deviation $= |W - \text{mean}| \times 100 / \text{mean}$. Bands based on mean weight: $\le 130\text{ mg}$ (10%), $\le 324\text{ mg}$ (7.5%), $> 324\text{ mg}$ (5%). Complies if $\le 2$ units exceed the band and 0 units exceed twice the band.
  - Capsules: Net weight $= \text{Gross} - \text{Shell}$. Stage 1 ($n=20$): 0 outer breaches ($> 25\%$) and $\le 2$ inner breaches ($> 10\%$) = **Complies**; 3 to 6 inner breaches = **NextStageRequired**; $> 6$ inner breaches or any outer breach = **DoesNotComply**. Stage 2 ($n=60$ total): 0 outer breaches and $\le 6$ inner breaches = **Complies**.
- Each stage entry is individually electronically signed with meaning `ResultRecorded`.

#### Validation
- Dissolution:
  - "Assign this sample to an HPLC run with a passed system suitability."
  - "System suitability {code} has not passed."
  - "Dissolution needs an HPLC method with exactly one analyte."
  - "Method {code} has no standard dilution for {analyte}."
  - "Medium volume must be greater than zero."
  - "Dilution factor must be greater than zero."
  - "Stage 1 dissolution requires exactly 6 vessel areas."
  - "Stage 2 dissolution requires exactly 6 vessel areas."
  - "Stage 3 dissolution requires exactly 12 vessel areas."
  - "Vessel area must be greater than zero."
  - "Active dissolution analysis does not require a next stage."
  - "Q must be greater than 0 and less than or equal to 100."
- Disintegration:
  - "Unit minutes are required."
  - "Disintegration time must be greater than zero."
  - "Stage 1 disintegration requires exactly 6 units."
  - "Stage 2 disintegration requires exactly 12 units."
  - "Disintegration time limit must be greater than zero."
- Weight Variation:
  - "Units are required."
  - "Specification dosage form is not configured."
  - "Stage 1 weight variation requires exactly {N} units."
  - "Tablets do not support Stage 2 evaluation."
  - "Tablet units must only contain WeightMg."
  - "Tablet unit weight must be greater than zero."
  - "Capsule units must contain both GrossMg and ShellMg."
  - "Shell weight must be greater than zero."
  - "Unit {N}: Shell weight must be less than gross weight."
  - "Stage 2 weight variation requires exactly 40 units."

> **Note for reviewers:** Stage acceptance criteria, offsets (e.g. dissolution $Q+5$, $Q-15$, $Q-25$), and weight-variation band limits are configured per product in the Test Master; system defaults serve as baselines. (GAP-610, GAP-611)

#### Status
- Intermediate stages leave the order active with panel notification **Stage 2 needed** or **Stage 3 needed**.
- Final stage completion transitions Test Order workflow step to **Ready**.

---

### 27.5 Operating the HPLC Workspace
<!-- WF-HPLC-001 -->
#### Purpose
Initialize analytical chromatography runs, record and evaluate system suitability tests (SST), assign sample test orders, enter replicate peak responses, upload chromatogram evidence, submit official results for technical review, and complete or abandon runs.

#### Who can use it
Requires the *Operate HPLC* permission (by default: Analyst, Section Head, System Administrator).

#### Before you start
- The HPLC instrument must have no active open run, must not have an overdue calibration, and its inventory status must be **InService**.
- An active HPLC method matching the instrument technique must exist in the Chromatography Methods Master.
- A compatible chromatography column with a matching USP designation must be available.
- For every mobile phase channel defined in the method, an unexpired, **Prepared** mobile phase preparation created for that method must exist.
- A usable stock lot of reference standard for each analyte with recorded purity and moisture content must be available.

#### What you see
- Overview page (`/hplc-workspace`): KPI summary tiles (**Instruments**, **Available**, **In use**, **Unavailable**), filter tabs (**All**, **In use**, **Available**, **Unavailable**), and instrument cards.
- Instrument workspace (`/hplc-workspace/:instrumentId`):
  - Step rail: **Run Setup**, **System Suitability**, **Sample Assignment**, **Testing & Evidence**, **Review & Complete**.
  - Tabs: **Overview**, **System Suitability**, **Samples (N)**, **Evidence (N)**, **Run History**.
  - Action buttons: **Abandon Run**, **Confirm System Suitability...**, **Assign Samples...**, **Complete Run**.

[SCREENSHOT SS-276]
[SCREENSHOT SS-277]

#### How to use it
##### 1. Start a New Run:
1. In the **HPLC Workspace**, locate an available instrument and click **Start New Run**.
2. In the **Start HPLC Run** wizard:
   - **Select Method**: Choose the active HPLC method.
   - **Select Column**: Choose an active, compatible column matching the method's USP designation.
   - **Assign Mobile Phases**: Select an unexpired, prepared mobile phase preparation for each required channel.
   - **Review & Start**: Verify selections and click **Start HPLC Run**.
3. The run is created with code format `{ABBR}-RUN-{seq}` in status **Open**, and an SST record in status **Pending**.

##### 2. Enter and Confirm System Suitability:
1. Open the **System Suitability** tab.
2. For each method analyte, select the standard lot, enter the **Actual Std Weight (mg)**, and transcribe the CDS integration results: peak response for each required standard injection, reported RSD, resolution, tailing factor, theoretical plates, retention factor ($k'$), signal-to-noise ($S/N$), and peak-to-valley ($P/V$) as required by the method.
3. Click **Save System Suitability**.
4. In **System Suitability Report Evidence**, click **Upload Report** to upload the CDS standard integration report (PDF, PNG, or JPEG, up to 25 MB).
5. Click **Confirm System Suitability...**.
6. In the **Electronic Signature** dialog, verify the statement: "I confirm that the system suitability results meet the method acceptance criteria and the standard integration report has been verified."
7. Enter your password and click **Sign**.

> **Note for reviewers:** If system suitability fails, the run cannot be re-tested; the analyst must abandon the run with a documented reason and start a new run. The standard weight has already been deducted from inventory stock upon confirmation. (GAP-600, BR-HPLC-010)

##### 3. Assign Samples:
1. Click the **Samples** tab and click **Assign Samples...**.
2. Select eligible test orders (assay or dissolution tests linked to the method) and click **Assign Selected (N)**.

##### 4. Enter Replicates and Submit for Review:
1. Open an assigned sample to display the sample entry page.
2. Enter the actual sample weight (mg) and peak response for each replicate.
3. Click **Save Replicates** to view the live calculation preview (labeled **Preview, not the official result**).
4. In **Report & Chromatogram Evidence**, upload the sample CDS chromatogram report.
5. Click **Send for Review**, review the submission checklist in the dialog, and click **Proceed to Sign**.
6. In the **Electronic Signature** dialog, confirm the statement: "I confirm that chromatographic replicate data and evidence for sample {no} ({test}) are complete, accurate, and ready for review."
7. Enter your password and click **Sign**.
8. Replicate data is frozen, the badge updates to **Submitted for Review**, the official result displays **Official, Recorded for Review**, and the test order moves to **Ready**.

##### 5. Complete or Abandon Run:
1. When all assigned samples have been submitted for review (or removed with a reason), return to the run header and click **Complete Run**.
2. If an instrument or suitability issue occurs, click **Abandon Run**, enter a detailed justification in **Reason for Abandoning** (up to 500 characters), and confirm. Abandoned runs release their unsubmitted test orders so they can be reassigned.

#### System behavior
- **Run concurrency**: Exactly one open run per instrument is permitted ("\"{instrument}\" already has an open run.").
- **Suitability evaluation**: Evaluates injection count, peak response $> 0$, unrounded RSD $\le$ method maximum, and chromatographic parameters. Run SST passes only if all analytes pass.
- **Stock deduction**: Standard weights are deducted from inventory lots upon SST confirmation ($mg/1000$ for Gram lots, $mg/1,000,000$ for Kilogram lots), whether SST passes or fails.
- **Assay calculation**:
  $$A\% = \frac{R_{\text{test}}}{R_{\text{std\_mean}}} \times \frac{W_{\text{std}}}{\text{ThWt}_{\text{std}}} \times \frac{\text{ThWt}_{\text{test}}}{W_{\text{test}}} \times \frac{100 - MC}{100} \times P$$
- **Reporting**: Bulk stage roles report individual replicate values; Finished stage roles report the mean assay % (1 decimal) and amount per unit (`Assay% * LabelClaim / 100`, 2 decimals).
- **Segregation of duties**: One user may start the run, confirm suitability, and submit samples; the system prevents that user from reviewing or approving the sample.

#### Validation
- Run setup:
  - "\"{instrument}\" already has an open run."
  - "Calibration overdue since {date}."
  - "Equipment is {status}."
  - "HPLC method \"{name}\" is inactive."
  - "The HPLC method belongs to another laboratory."
  - "Column \"{name}\" is inactive."
  - "Column \"{name}\" is not compatible with \"{instrument}\"."
  - "Column {code} has no USP designation; set it in the column master."
  - "Column {code} is USP {x}; method {abbr} requires {y}."
  - "Select a prepared mobile phase for every channel this method uses."
  - "Channel {c}: the selected preparation is not currently prepared."
- System suitability:
  - "{analyte}: standard weight must be greater than zero."
  - "Lot {lot} has no purity recorded."
  - "Lot {lot} has no moisture content recorded."
  - "Upload the standard report before confirming system suitability."
  - "Channel {c}: the mobile phase preparation has expired since it was selected."
  - "{analyte}: {N} injections required, {M} entered."
  - "{analyte}: RSD {X}% exceeds {Y}%."
- Sample assignment & entry:
  - "Sample assignment is locked until system suitability passes."
  - "Test order {N} is already assigned to run {X}."
  - "Replicate {N}: the sample weight must be greater than zero."
  - "Replicate {N}: the response for {analyte} must be greater than zero."
  - "Expected exactly {N} replicates for stage role {R}, but {M} entered."
  - "Upload the sample report before sending for review."
- Run completion:
  - "{N} assigned sample(s) have not been sent for review. Submit or remove them first."
  - "A reason is required."
  - "Reason cannot exceed 500 characters."

#### Status
- Run status: **Open** $\rightarrow$ **Completed** (or **Abandoned**).
- SST status: **Pending** $\rightarrow$ **Passed** or **Failed**.
- Sample status: **Assigned** $\rightarrow$ **Submitted for Review** (test order at **Ready**).

---

### 27.6 Operating the GC Workspace and Residual Solvents Testing
<!-- WF-GC-001 -->
#### Purpose
Operate gas chromatography runs for assay and residual solvents determinations (USP $\langle 467 \rangle$, ppm) using specialized GC conditions, carrier gas, and oven temperature ramps.

#### Who can use it
Requires the *Operate HPLC* permission (by default: Analyst, Section Head, System Administrator).

#### Before you start
- An operational GC instrument without an active run and in calibration.
- An active GC method configured in the Chromatography Methods Master with carrier gas, inlet/detector temperatures, and oven program steps.
- Compatible GC capillary or packed column matching the method's G-designation.
- For residual solvents, standard concentrations ($\mu\text{g/mL}$) configured in the method.

#### What you see
- GC Workspace overview (`/gc-workspace`): Displays GC instruments and runs.
- **Start GC Run** wizard: 3-step wizard (**Select Method**, **Select Column**, **Review & Start**; mobile phase assignment is omitted).
- Sample entry page: Displays replicate rows for sample weight (g) and solvent peak responses, along with the **ResidualSolventCalculationView**.

[SCREENSHOT SS-278]

#### How to use it
1. Navigate to **Laboratories** > **Physicochemical Laboratory** > **GC Workspace**.
2. Click **Start New Run** on an available GC instrument.
3. Select the GC method and compatible column, review settings, and click **Start GC Run**.
4. In the **System Suitability** tab, enter the CDS standard injection responses for each solvent. For residual solvents, standard weights, purity, and moisture are not required.
5. Upload the standard integration report and click **Confirm System Suitability...** to sign.
6. Assign residual solvent sample test orders.
7. In the sample entry page, enter sample weight (g) and peak responses for each solvent replicate. Replicate responses of zero are permitted.
8. Upload the sample chromatogram report and click **Send for Review** to electronically sign and submit results.

#### System behavior
- **Residual Solvents Calculation**:
  $$\text{ppm} = \frac{C_{\text{std}} \ [\mu\text{g/mL}] \times V \ [\text{mL}]}{W \ [\text{g}]} \times \frac{r_u}{r_{\text{std\_mean}}}$$
- **Evaluation**: The mean ppm of replicates is judged against the specification limit for each solvent.
- If all replicate peak responses for a solvent are zero, the reported result displays **Not detected** (value 0). Otherwise, results are displayed with 1 decimal plus " ppm".

#### Validation
- "“{instrument}” is not a GC instrument; method {abbr} needs one."
- "Column {code} is USP {x}; method {abbr} requires {y}."
- "Replicate {N}: the response for {analyte} must be zero or more."
- "Sample solution volume is required for residual solvents."
- "The standard concentration must be greater than zero."

#### Status
- Follows the same run lifecycle (**Open**, **Completed**, **Abandoned**) and test order transition to **Ready**.

---

### 27.7 Operating the ICP-OES Workspace
<!-- WF-ICP-001 -->
#### Purpose
Perform multi-element determinations, mineral assays, and heavy metals / elemental impurities testing using inductively coupled plasma optical emission spectrometry, including calibration curve confirmation, continuing calibration verification (CCV), sample analysis, and signed submission.

#### Who can use it
Requires the *Operate HPLC* permission (by default: Analyst, Section Head, System Administrator).

#### Before you start
- ICP-OES instrument active, in calibration, and free of open runs.
- Active ICP-OES method in the ICP Methods Master with configured elements, emission wavelengths, and calibration standard levels.
- Usable ReferenceStandard stock lots for calibration and (if configured) Initial Calibration Verification (ICV).

#### What you see
- ICP-OES Workspace overview (`/icp-workspace`): Title **ICP-OES Workspace**, KPI tiles (**Instruments**, **Available**, **In use**, **Cal. pending**, **Unavailable**), and instrument cards.
- ICP Run Workspace (`/icp-workspace/:instrumentId`):
  - Step rail: **Run Setup**, **Calibration**, **Samples**, **Testing & Evidence**, **Review & Complete**.
  - Tabs: **Overview**, **Calibration**, **CCV** (displayed if the method uses CCV), **Samples (N)**, **Evidence (N)**, **Run History**.
  - Element status table: Columns for **Symbol**, **Wavelength (nm)**, **Status** (chips **Pass**, **Fail**, **Pending**), and **Failure Reasons**.
  - Action buttons: **Confirm Calibration**, **Record Reading** (CCV), **Assign Samples...**, **Complete Run**, **Abandon ICP Run**.

[SCREENSHOT SS-279]

#### How to use it
##### 1. Start ICP Run:
1. Click **Start Run** on an available instrument card, select the active ICP method, and click **Start Run**.
2. The run is initialized in status **Open** with calibration status **Pending**.

##### 2. Enter and Confirm Calibration:
1. In the **Calibration** tab, select the **Calibration Standard Lot** and (if required) **ICV Standard Lot**.
2. For each element, enter the correlation coefficient $r$ (transcribed from Syngistix, between 0 and 1), blank concentration (mg/L, if required), and measured ICV concentration (mg/L, if required).
3. Click **Save Calibration Readings**.
4. In **Calibration Report Document**, upload the Syngistix calibration report.
5. Click **Confirm Calibration** and sign the electronic signature dialog: "I confirm this ICP calibration is accurate and complies with laboratory standards."

> **Note for reviewers:** Calibration values cannot be re-entered after confirmation. If no element achieves a passing calibration, sample assignment remains blocked, and the run must be abandoned. (GAP-601)

##### 3. Record Continuing Calibration Verification (CCV):
1. If the method requires CCV, open the **CCV** tab during the analytical run.
2. Select the element in **Select Analyte / Element** and enter **Measured Concentration (mg/L)**.
3. Click **Record Reading**. The system calculates recovery percentage and stores the reading.

> **Note for reviewers:** A failed CCV reading permanently locks that element for all pending and future sample entries in the run; samples previously submitted for review are not automatically recalled. (GAP-603)

##### 4. Assign and Test Samples:
1. Open the **Samples** tab and click **Assign Samples...** to assign eligible `IcpMethodAssay` test orders.
2. In the sample entry page, select the **Amount Unit** (grams required for elemental impurities).
3. If the specification evaluates mg/unit or % label claim, enter the unit amount (average tablet weight or dose).
4. Enter replicate sample amounts, preparation volumes, dilution factors ($\ge 1$), and measured solution concentrations (mg/L) for each required element.
5. Click **Save Replicates** to view the calculation preview.
6. In **Sample Report Upload**, attach the Syngistix sample report.
7. Click **Send for Review**, complete the six verification checklist steps, and sign with your password ("I confirm that ICP replicate data, element concentrations, and evidence for sample {no} ({test}) are complete, accurate, and ready for review.").

##### 5. Complete or Abandon Run:
1. Return to the run header and click **Complete Run** once all samples are submitted.
2. If plasma extinguishes, argon runs out, or nebulizer clogs occur, click **Abandon Run**, enter a justification in **Reason for Abandoning \***, and confirm.

#### System behavior
- **Calibration validity**:
  - $r$ must meet or exceed method `MinCorrelation`.
  - Blank must not exceed the configured limit.
  - ICV recovery must fall within the configured recovery window inclusive.
  - Confirmed calibrations remain valid for the method's configured maximum age (1 to 168 hours, default 24 hours). Once expired, sample submission is refused: "Calibration expired at yyyy-MM-dd HH:mm."
- **Calculations**:
  - Content ($\mu\text{g/g}$): $\text{Content} = \frac{C \times V \times DF}{W}$.
  - Amount per unit: $\text{mg/unit} = \frac{\text{Mean Content} \times \text{Unit Amount}}{1000} \times \text{Conversion Factor}$ (displayed with 3 decimals).
  - Percent Label Claim: $\% \ LC = \frac{\text{mg/unit}}{\text{Label Claim}} \times 100$ (displayed with 1 decimal).
- **Limit checks**:
  - Replicates above the highest calibration level block submission: "{symbol}: replicate {N} is above the top standard - dilute and re-measure."
  - Replicates below the lowest standard are excluded from the mean. If all replicates are below the lowest standard, the result is reported as `<LOQ`.

#### Validation
- "Choose the calibration standard lot."
- "Choose the ICV standard lot."
- "Upload the Syngistix calibration report before confirming."
- "The calibration has already been confirmed."
- "r must be between 0 and 1."
- "r {X} is below the minimum {Y}."
- "Blank {X} mg/L is above the limit {Y} mg/L."
- "ICV recovery {X}% is outside {low}-{high}%."
- "Confirm the calibration before assigning samples."
- "No element is valid on this run."
- "Confirm a valid calibration before entering CCV."
- "The measured value cannot be negative."
- "Calibration expired at {date} {time}."
- "{symbol} is not valid on this run: {reason}"
- "Elemental impurities are reported per gram; enter the sample amount in g."
- "Enter the unit amount (average unit weight or dose)."
- "Replicate {N}: the sample amount and volume must be greater than zero."
- "Replicate {N}: the dilution factor must be at least 1."
- "Replicate {N}: exactly one concentration for {symbol} is required."
- "{symbol}: replicate {N} is above the top standard - dilute and re-measure."
- "Upload the sample result report before sending for review."
- "{N} assigned sample(s) have not been sent for review. Submit or remove them first."

#### Status
- ICP Run: **Open** $\rightarrow$ **Completed** or **Abandoned**.
- Calibration: **Pending** $\rightarrow$ **Confirmed**.
- Element availability: **Pass**, **Fail**, **Pending**, **CCV failed (X%)**, **Needs a passing CCV**.

---

### 27.8 Instrument Report Documents and Evidence Upload
#### Purpose
Attach, inspect, and supersede raw instrument data files, chromatograms, integration reports, and calibration printouts associated with analytical runs and sample results.

#### Who can use it
Requires the *Operate HPLC* permission (by default: Analyst, Section Head, System Administrator).

#### Before you start
- Report files must be formatted as PDF, PNG, or JPEG.
- File size must not exceed 25 MB.
- The file must contain valid, non-empty binary content.

#### What you see
- Workspace tabs: **ChromatogramEvidencePanel** with filter tabs **All Files**, **SST**, **Sample**, and **Run**.
- File list displaying: File name, file size, upload category, uploaded by, and upload timestamp.
- Row action: **Supersede Evidence** (icon or button).
- Dialog: **Supersede Reason** text field and replacement file picker.

#### How to use it
1. Navigate to the **Evidence** tab of the run or the evidence section of the sample entry page.
2. Drag and drop the file or click **Upload Report Document**.
3. Select the file and click **Upload**.
4. To replace an existing document, click **Supersede Evidence**.
5. Select the replacement file, type a mandatory justification in **Supersede Reason**, and click **Confirm Replacement**.

#### System behavior
- Documents are stored in secure document storage with a computed SHA-256 integrity hash.
- Files cannot be deleted. When an evidence document is superseded, the original file remains permanently visible in the audit trail alongside the replacement file, timestamp, user, and supersession reason.

#### Validation
- "The uploaded file is empty."
- "File exceeds the maximum allowed size of 25 MB."
- "A file name is required."
- "File type \"{t}\" is not supported. Allowed types: PDF, PNG, JPEG."
- "This evidence has already been superseded."
- "A reason is required."
- "Reason cannot exceed 500 characters."

---

### 27.9 Equation Types Reference
<!-- MOD-EQTYPE-001 -->
#### Purpose
Provide a read-only catalog of predefined mathematical formulas and calculation inputs used by legacy analytical test definitions.

#### Who can use it
Requires the *Manage master data* permission (by default: Section Head, System Administrator) to access the menu route (`/laboratory-configuration/equation-types`).

#### What you see
- Title: **Equation Types**.
- Subtitle: "Predefined mathematical formulas and required calculation parameters for analytical test methods and system suitability."
- Table displaying 6 predefined equation types:
  1. **None**
  2. **System Suitability**
  3. **Numeric Measurement**
  4. **Gravimetric % Loss**
  5. **Gravimetric % Residue**
  6. **Qualitative / Identification**

> **Note for reviewers:** The Equation Types page displays a hard-coded read-only list of six baseline formulas; modern physicochemical workflows (Dissolution, Disintegration, Weight Variation, Titration, HPLC Method Assay, and ICP Method Assay) execute through dedicated workflow engines and are not listed on this page. (GAP-606)
