## 13. Test Orders and Analyst Assignment

### 13.1 Test Order Generation and Assignment Overview
#### Purpose
Understand how laboratory test orders are generated automatically from configured items, how test orders progress through laboratory workflow steps, and how analyst assignments impact workflow governance.

#### Who can use it
This section is explanatory. Test orders are created by the system; the actions that create or change them are described in Sections 12, 13.2 and 13.3 with their own permissions.

#### Before you start
A sample must be registered in the system through sample receiving or preparation.

#### What you see
- In the laboratory workspace (selected sample panel):
  - Section header: **Assigned Tests (N)** with subtitle "Test Master configured laboratory workflows" (displays "No tests assigned to this sample." when none are assigned).
  - The tests assigned to the selected sample.
  - Controlled item document links: **SOP** and **Verification Report** (for item-based samples).
- The **Test Status Summary** column in the sample register.

#### System behavior
- Test orders are created automatically, one per configured test. The system does not support manual creation of test orders.
- Generation timing by category:
  - For Product, Raw Material, and Packaging Material samples, test orders are generated at the time of sample receiving for each selected laboratory section.
  - For Environmental Monitoring, Water, and After Cleaning samples, test orders are generated during the sample preparation step once monitoring rooms, sampling points, or machine parts have been confirmed.
- Initial state:
  - Every newly created test order begins in the status **Pending** with workflow step **Waiting**.
- Workflow step progression:
  - When media is selected and incubation is started (or when selective plating begins), the test order transitions to workflow step **Incubating** and status **In Progress**.
  - When test observations, plate counts, or testing sessions are finalized, the test order transitions to workflow step **Ready** and status **Result Entered**.
- Automatic review submission:
  - When every non-superseded test order of an assigned laboratory section reaches the **Ready** step, the section automatically advances from **Under Testing** to **Pending Review** for technical verification.
- Test orders that have reached **Ready**, **Reviewed**, or **Approved** can no longer have testing results submitted against them. The system displays: "Test order {id} is already at {step} - its workflow can no longer be submitted against."

#### Status
- Test order statuses: Pending, In Progress, Result Entered, Reviewed, Approved, Rejected, Retest Requested, Voided, and Cancelled.
- Workflow steps: Waiting, Running, Incubating, Ready, Reviewed, and Approved.

> **Note for reviewers:** For Environmental Monitoring, Water and After Cleaning samples, the user who confirms preparation becomes the assigned analyst on every test order created, with no ownership check, and the preparation is not signed. These receive and prepare actions require sign-in only. (GAP-212, GAP-227)


### 13.2 Adding a Laboratory to a Sample
<!-- WF-SAMPLE-002 -->
#### Purpose
Add an additional laboratory section (such as the Physicochemical Laboratory to an existing Microbiology sample, or vice versa) to a registered Product, Raw Material, or Packaging Material sample under electronic signature control.

#### Who can use it
Requires the *Global sample receiving* permission (by default: Section Head, System Administrator).

#### Before you start
- The sample must belong to the Product, Raw Material, or Packaging Material category (a laboratory cannot be added to Environmental Monitoring, Water, or After Cleaning samples).
- The overall sample status must not be Rejected, Voided, or Cancelled.
- The target laboratory section must not already have test orders on the sample.
- The item must have configured tests in the Test Master for the target laboratory section.

#### What you see
- Dialog title: **Add Laboratory**
- Subtitle: "Creates that lab's test orders on this sample - signed with your password"
- Form controls:
  - **Laboratory** dropdown (select the additional laboratory section)
  - **Reason \*** text field (mandatory; helper text: "Recorded with your electronic signature and in the sample's audit trail.")
- Action buttons: **Cancel** and **Sign & Add**

[SCREENSHOT SS-131]

#### How to use it
1. In the sample register, locate the sample and open the row **Actions** menu.
2. Click **Add Laboratory** (the option is available unless the sample is in Rejected, Voided, or Cancelled status).
3. In the **Add Laboratory** dialog, select the additional **Laboratory** from the dropdown menu.
4. In the **Reason \*** field, enter a complete explanation for why the additional laboratory testing is required.
5. Click **Sign & Add**.
6. In the electronic signature prompt, enter your user password to confirm the electronic signature meaning for laboratory addition.
7. Verify the confirmation message: "Laboratory added to the sample."

#### System behavior
- The system verifies that the sample is item-based and that its overall status is not rejected, voided, or cancelled.
- Generates test orders for all tests configured for the newly added laboratory section in status **Pending** and workflow step **Waiting**.
- Sets the added laboratory section's sign-off status to **InTesting**.
- Recomputes overall sample status: if the sample had previously finished testing or received an approval decision in another laboratory, the sample returns to **Under Testing**, and any recorded approval decision is cleared.
- If the newly added tests require sample preparation and no preparation record exists, sets the sample preparation status to **Needs Preparation**.
- Records the electronic signature with the signer, role, meaning and reason.

#### Validation
The system enforces the following exact validation rules:
- "A reason is required to add a laboratory."
- "A laboratory cannot be added to a rejected, voided or cancelled sample."
- "This laboratory is already testing the sample."
- "A laboratory can be added only to a product, raw material or packaging material sample."
- "Item '{name}' has no tests for {lab}."
- "A reason is required." (shown in the dialog when the reason is empty)
- "Password verification failed. The signature was not applied."

> **Note for reviewers:** Adding a laboratory has no approved intended-workflow document; this procedure describes observed screen behavior only. (GAP-200)

#### Status
- Added laboratory section status is set to **InTesting**.
- If the sample had finished testing in its other laboratories, the overall sample status returns to **Under Testing**.
- Preparation status is set to **Needs Preparation** if new tests require preparation.

#### What happens next
The sample appears in the added laboratory's register. If preparation is needed it must be confirmed first (preparation status **Needs Preparation**); testing then proceeds as usual. Each laboratory is reviewed separately.


### 13.3 Assigning a Responsible Analyst
<!-- WF-SAMPLE-003 -->
#### Purpose
Assign or reassign a designated laboratory analyst to all open test orders on a sample, or clear an existing assignment so that any authorized analyst may prepare the sample.

#### Who can use it
Requires the *Sample analyst assignment* permission (by default: Section Head, System Administrator).

#### Before you start
The sample must be within the user's laboratory section scope.

#### What you see
- Dialog title: **Assign Responsible Analyst**
- Read-only sample information:
  - **Reference Number:**
  - **Batch / Control No.:**
  - **Current Assignment:**
- Form controls:
  - **Select Analyst** dropdown (includes active user accounts and the option "Unassigned (Open for any authorized analyst to prepare)")
  - **Reason for Assignment / Reassignment (recorded in Audit Trail)** text field (optional)
- Action buttons: **Cancel** and **Save Assignment** (displays "Saving..." while processing)

[SCREENSHOT SS-132]

#### How to use it
1. In the sample register, click **Assign Analyst** from the row **Actions** menu, or click the analyst chip in the register row (tooltips: "Click to reassign analyst (Section Head)" or "Assign responsible analyst (Section Head)").
2. In the **Assign Responsible Analyst** dialog, review the reference number, batch/control number, and current assignment.
3. In the **Select Analyst** dropdown, select the target analyst, or select **Unassigned (Open for any authorized analyst to prepare)** to remove the current assignment.
4. Optionally enter an explanation in the **Reason for Assignment / Reassignment (recorded in Audit Trail)** field.
5. Click **Save Assignment**.

#### System behavior
- The system updates the assigned analyst on every non-superseded test order of the sample that is not currently in Approved or Rejected status.
- Writes an audit trail log entry recording the change:
  - Action is logged as "AssignedAnalyst" when assigned initially.
  - Action is logged as "ReassignedAnalyst" when changed from one analyst to another.
  - Action is logged as "UnassignedAnalyst" when an assignment is cleared.
  - The audit trail records previous and new user names, as well as the reason if entered (formatted as "{new} | Reason: {reason}").
- Operational impact on sample preparation:
  - For Product, Raw Material, and Packaging Material samples, only the assigned analyst may confirm sample preparation. If the sample is unassigned, any authorized analyst may perform preparation.
- Operational impact on workspace filtering:
  - The **Assigned to Me** and **Unassigned** indicators in the register count samples by this assignment. When an Analyst-role user starts tests through grouped actions, tests assigned to another analyst are skipped with the message: "Test order is assigned to another analyst."
- Impact on segregation of duties:
  - The assigned analyst counts as having performed the test, whatever that user's role. As a result, that user cannot perform technical review or release approval on the sample:
    - Attempting review displays: "You cannot review a sample you tested."
    - Attempting approval displays: "You cannot approve a sample you tested."
- Analyst assignment does not require an electronic signature.

#### Validation
The system enforces the following exact validation rules:
- "User {id} not found."
- "User '{name}' is not active."

> **Note for reviewers:** Any active user can be assigned as responsible analyst regardless of their role or laboratory membership, and the reason field is optional. (GAP-213)

> **Note for reviewers:** The screen lets a Section Head or System Administrator confirm preparation even when another analyst is assigned, but the server applies the ownership rule to every role; the intended behavior is unconfirmed. Single-test actions do not check the assigned analyst. (GAP-219)

#### Status
The test orders retain their current workflow step and approval status, with the assigned analyst field updated.

<!-- Review log (Sonnet): 14 corrections; invented items removed: test-order card contents, 'Assigned to Me' default test filter, all-user access statement, signature timestamp, enum-style status names; callouts added: preparer-becomes-analyst/unsigned preparation (GAP-212, GAP-227), assigned-analyst ownership mismatch (GAP-219); note on 13.2 narrowed (GAP-200) -->
