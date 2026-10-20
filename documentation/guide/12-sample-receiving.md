## 12. Sample Receiving

### 12.1 Receiving Product, Raw Material, and Packaging Material Samples
<!-- WF-SAMPLE-001 -->
#### Purpose
Register incoming samples of Finished Product, Raw Material, or Packaging Material, assign the destination laboratory sections, and automatically generate test orders for all configured laboratory tests.

#### Who can use it
- Users who hold both the *Global sample receiving* and the *Own-lab sample receiving* permissions (by default: Section Head, System Administrator) can receive for any laboratory section.
- Users with the *Own-lab sample receiving* permission (by default: Analyst, Reviewer, Section Head, System Administrator) can receive samples only for the laboratory sections where they hold an active membership.

> **Note for reviewers:** Users who hold *Global sample receiving* alone cannot receive Product, Raw Material, or Packaging Material samples because the endpoint also requires *Own-lab sample receiving*. Whether this requirement is intended has not been confirmed. (GAP-909)

#### Before you start
- The item must exist in the item master, be active, and have configured laboratory tests in the Test Master. If an item is frozen, the system displays: "Item '{name}' is frozen and cannot be used to receive new samples." If an item lacks configured tests, the system displays: "Item '{name}' has no assigned tests. Configuration must be completed by the Section Head before samples can be received."
- For Finished Product samples, a valid production stage must be configured.
- Master data lists for causes of testing and sampling personnel must be configured.
- The user must hold active membership in the selected laboratory section unless authorized for global receiving.

#### What you see
- Category selection screen (Step 1):
  - Dialog title: **New Sample**
  - Category tiles: **Product**, **Raw Material**, **Packaging Material**, **Water**, **Environmental Monitoring**, and **After Cleaning**
  - Navigation button: **Next** (disabled until a category tile is selected)
- Multi-sample entry grid (Step 2):
  - Row fields and selectors:
    - **Select Item** dropdown
    - Laboratory picker (visible when receiving globally; fixed to the current section when initiated from a laboratory workspace)
    - **Stage** selector (mandatory for Finished Product)
    - **Cause of Testing** dropdown
    - **Sampled By** dropdown
    - **Batch No.** (marked with a red asterisk in the header)
    - **Control No.** (marked with a red asterisk in the header)
    - **Quantity** (placeholder: "e.g. 50g / 100ml")
    - **Mfg Date** date picker
    - **Exp Date** date picker
  - Action buttons: **Back**, **Add Another Sample**, and **Save All Samples** (displays "Receiving..." while the request is being processed)

[SCREENSHOT SS-121]

[SCREENSHOT SS-122]

#### How to use it
1. Navigate to **RECEIVING** and click **Receive Sample** from the sidebar navigation (or click **+ New Sample** from within a laboratory workspace).
2. Click **Receive** (or **+ New Sample**).
3. In the **New Sample** dialog, click the tile for **Product**, **Raw Material**, or **Packaging Material**, then click **Next**.
4. In the multi-sample entry grid, select the **Item** from the dropdown menu.
5. In the laboratory selector, choose one or more target laboratories (for example Microbiology Laboratory or Physicochemical Laboratory).
6. If receiving a Finished Product sample, select the applicable **Stage** (Production Stage).
7. Select the **Cause of Testing** and the sampler's name in **Sampled By**.
8. Enter the **Batch No.** and **Control No.**.
9. Optionally enter the sample **Quantity**, **Mfg Date**, and **Exp Date**.
10. To register multiple samples in a single submission, click **Add Another Sample** and complete the fields for each additional row.
11. Click **Save All Samples**.
12. Confirm the success notification: "Successfully received N sample(s). Test orders generated automatically."

#### System behavior
- The system generates a category-specific reference number for each sample.
- The initial status of the sample is set to **Received**.
- Preparation status is established:
  - Samples are assigned the preparation status **Needs Preparation**.
  - A sample whose tests all belong to the FP section is assigned **Ready** instead, because FP tests have no preparation stage. A sample with a mix of tests needs preparation only for its non-FP tests.
- The system automatically generates exactly one test order for each configured test associated with the selected laboratory sections. Each generated test order is created with the status **Pending** and workflow step **Waiting**.
- Sample receiving is recorded without requiring an electronic signature.

#### Validation
The system evaluates the following client-side and server-side validation rules:
- Client validation:
  - "Item is required"
  - "Choose at least one laboratory"
  - "Production Stage is required" (Finished Product only)
  - "Cause of Testing is required"
  - "Sampled By is required"
  - "Expiry Date must be after Manufacturing Date"
- Server validation:
  - "A valid Item must be selected."
  - "Cause of testing is required."
  - "Batch number is required."
  - "Control number is required."
  - "Sampled By is required."
  - "Production stage is required for a Finished Product sample."
  - "Production stage '{stage}' is not a known stage."
  - "Item '{name}' is frozen and cannot be used to receive new samples."
  - "Item '{name}' has no assigned tests. Configuration must be completed by the Section Head before samples can be received."
  - "Choose at least one laboratory to receive the sample for."
  - "One or more selected laboratories do not exist."
  - "You can receive samples only for your own laboratory."
  - "Item '{name}' has no tests for {lab name}."

> **Note for reviewers:** Receiving, correcting, voiding, and assigning samples have no approved intended-workflow specification document; this procedure describes observed screen behavior only. (GAP-200, GAP-201, GAP-203, GAP-213)

> **Note for reviewers:** Batch Number and Control Number are marked with an asterisk as required in the grid header, but client-side row validation does not verify them; empty values are rejected only by the server upon submission. (GAP-200)

#### Status
- Sample status is initialized to **Received**.
- Preparation status is initialized to **Needs Preparation** (or **Ready** if all tests belong to the FP section).
- Test orders are initialized to **Pending** with step **Waiting**.

#### What happens next
For samples requiring preparation, preparation must be confirmed before incubation can start (see Section 14). For samples whose tests do not require preparation, testing can begin immediately.


### 12.2 Receiving Environmental Monitoring Samples
<!-- WF-SAMPLE-006 Step 1 -->
#### Purpose
Register an Environmental Monitoring sample record in the system before rooms, monitoring locations, and test configurations are selected during sample preparation.

#### Who can use it
Any authenticated laboratory user (Environmental Monitoring receive endpoints require user sign-in only).

#### Before you start
Environmental monitoring departments, rooms, and room test configurations must be configured in master data.

#### What you see
- Category selection: **Environmental Monitoring** tile in the **New Sample** dialog.
- Entry fields:
  - **Department** (placeholder: "Select Department", mandatory)
  - **Cause of Testing** (placeholder: "Cause of Testing", mandatory)
  - **Sampled By** (placeholder: "Sampled By", mandatory)
  - **Control No.** (placeholder: "Control No.")
- Action buttons: **Back**, **Add Another Sample**, and **Save All Samples**.

#### How to use it
1. Navigate to **RECEIVING** and select **Receive Sample** (or open the Microbiology Workspace and click **+ New Sample**).
2. Click **Receive** (or **+ New Sample**).
3. Select the **Environmental Monitoring** category tile and click **Next**.
4. In the entry row, select the **Department**.
5. Select the **Cause of Testing** and **Sampled By**.
6. Enter the **Control No.**.
7. Click **Save All Samples**.

#### System behavior
- Generates a sample record with status **Received** and preparation status **Needs Preparation**.
- The system generates **no test orders** at the receiving step. Test orders and room locations are created subsequently when the sample preparation step is executed.
- The receiving action is completed without an electronic signature.

#### Validation
- Client validation:
  - "Department is required"
  - "Cause of Testing is required"
  - "Sampled By is required"
- Server validation:
  - "Department {id} not found."

> **Note for reviewers:** Environmental Monitoring, Water, and After Cleaning receiving endpoints require authentication only, without enforcing specific permission codes or laboratory scoping. (GAP-227)

#### Status
- Sample status is initialized to **Received**.
- Preparation status is initialized to **Needs Preparation**.

#### What happens next
The sample appears in the register. When it is selected in the laboratory workspace, the banner **Sample Needs Preparation** is shown; an analyst opens **Prepare Sample** to select rooms, which generates the required test orders.


### 12.3 Receiving Water Samples
<!-- WF-SAMPLE-007 Step 1 -->
#### Purpose
Register a Water monitoring sample record in the system prior to sampling point selection and storage condition documentation during sample preparation.

#### Who can use it
Any authenticated laboratory user (Water receive endpoints require user sign-in only).

#### Before you start
Water departments and sampling point test configurations must be configured in master data.

#### What you see
- Category selection: **Water** tile in the **New Sample** dialog.
- Entry fields:
  - **Department** (water department, placeholder: "Select Department", mandatory)
  - **Cause of Testing** (placeholder: "Cause of Testing", mandatory)
  - **Sampled By** (placeholder: "Sampled By", mandatory)
  - **Quantity** (placeholder: "e.g. 50g / 100ml", optional)
  - **Control No.** (placeholder: "Control No.")
- Action buttons: **Back**, **Add Another Sample**, and **Save All Samples**.

#### How to use it
1. Navigate to **RECEIVING** and select **Receive Sample** (or open the Microbiology Workspace and click **+ New Sample**).
2. Click **Receive** (or **+ New Sample**).
3. Select the **Water** category tile and click **Next**.
4. In the entry row, select the water **Department**.
5. Select the **Cause of Testing** and **Sampled By**.
6. Enter the **Quantity** (optional) and **Control No.**.
7. Click **Save All Samples**.

#### System behavior
- Creates the sample record with status **Received** and preparation status **Needs Preparation**.
- The system generates **no test orders** at the receiving step. Test orders, sampling points, and storage condition parameters are created during sample preparation.
- The action requires no electronic signature.

#### Validation
- Client validation:
  - "Department is required"
  - "Cause of Testing is required"
  - "Sampled By is required"
- Server validation:
  - "Water department {id} not found."

> **Note for reviewers:** Environmental Monitoring, Water, and After Cleaning receiving endpoints require authentication only, without enforcing specific permission codes or laboratory scoping. (GAP-227)

#### Status
- Sample status is initialized to **Received**.
- Preparation status is initialized to **Needs Preparation**.

#### What happens next
The sample appears in the sample register. An analyst opens sample preparation to document storage conditions (room temperature or refrigeration hours) and select the specific sampling points to be tested.


### 12.4 Receiving After Cleaning Samples
<!-- WF-SAMPLE-008 Step 1 -->
#### Purpose
Register an After Cleaning verification sample, linking the cleaning record to the cleaned equipment, previous product, and previous batch number before selecting specific machine parts during sample preparation.

#### Who can use it
Any authenticated laboratory user (After Cleaning receive endpoints require user sign-in only).

#### Before you start
Machines and machine part test configurations must be configured in master data.

#### What you see
- Category selection: **After Cleaning** tile in the **New Sample** dialog.
- Entry fields:
  - **Machine** (placeholder: "Select Machine", mandatory)
  - **Previous Product** (placeholder: "Select Item", mandatory)
  - **Previous Batch No.** (placeholder: "Batch No.", mandatory)
  - **Cause of Testing** (placeholder: "Cause of Testing", mandatory)
  - **Sampled By** (placeholder: "Sampled By", mandatory)
  - **Control No.** (placeholder: "Control No.")
- Action buttons: **Back**, **Add Another Sample**, and **Save All Samples**.

#### How to use it
1. Navigate to **RECEIVING** and select **Receive Sample** (or open the Microbiology Workspace and click **+ New Sample**).
2. Click **Receive** (or **+ New Sample**).
3. Select the **After Cleaning** category tile and click **Next**.
4. In the entry row, select the **Machine** from the dropdown menu.
5. Select the **Previous Product** and enter the **Previous Batch No.**.
6. Select the **Cause of Testing** and **Sampled By**.
7. Enter the **Control No.**.
8. Click **Save All Samples**.

#### System behavior
- Creates the sample record with status **Received** and preparation status **Needs Preparation**.
- Automatically sets the sample batch number to the value entered in **Previous Batch No.**.
- Generates **no test orders** at the receiving step. Test orders and machine-part test locations are created when sample preparation is completed.
- The action is completed without an electronic signature.

#### Validation
- Client validation:
  - "Machine is required"
  - "Previous Product is required"
  - "Previous Product Batch No. is required"
  - "Cause of Testing is required"
  - "Sampled By is required"
- Server validation:
  - "Previous Product is required for After Cleaning receiving."
  - "Previous Product Batch Number is required for After Cleaning receiving."
  - "Machine {id} not found."

> **Note for reviewers:** Environmental Monitoring, Water, and After Cleaning receiving endpoints require authentication only, without enforcing specific permission codes or laboratory scoping. (GAP-227)

#### Status
- Sample status is initialized to **Received**.
- Preparation status is initialized to **Needs Preparation**.

> **Note for reviewers:** For After Cleaning samples the specification calls for one collective pathogen test order plus one TAMC test order per machine part; the system instead creates one test order per distinct test code. (GAP-225)

#### What happens next
The sample appears in the register. An analyst opens **Prepare Sample** to select the machine parts, which creates the associated test orders.

<!-- Review log (Sonnet): 7 corrections; invented items removed: expiry-date server message on receive, Physicochemical-section Ready rule (now FP section), register banner claim, swab/rinse wording; callouts added: After Cleaning order mismatch (GAP-225) -->
<!-- Phase 13 (agy): applied F-A05, R-NEW-02 -->
<!-- Phase 13 review (Sonnet): checked against RECONCILIATION section 2 and frontend strings; no invented items -->
