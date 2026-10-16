## 18. Return to Analyst, Rework and Retest

### 18.1 Return a Test to the Analyst
<!-- WF-REVIEW-002 -->
#### Purpose
Return a count test order to the assigned analyst for recount or result re-entry before completing technical review, soft-superseding recorded readings and reopening the incubation step.

#### Who can use it
Requires the *Review samples* permission (by default: Reviewer, Section Head, System Administrator). Users must hold an active membership in the laboratory section governing the test order.

#### Before you start
- The test order must be in status `ResultEntered` and must not be marked as superseded.
- The test must be configured as a count test (containing count readings, or results without biochemical, location-grid, or pathogen data).
- The laboratory section must be in **In testing** or **Under review** status. If the section has already advanced to approval or later, returning a test is blocked with: "Cannot return a test to the analyst after the sample has been reviewed. Use Reject or Retest at approval instead."
- The overall sample status must not be **Under approval**, **Approved**, **Rejected**, **Retest requested**, **Cancelled**, or **Voided**.

> **Note for reviewers:** Return to Analyst takes no password or signature, the reason is optional, and the reviewer who performed the test is not blocked by a segregation-of-duties check. (GAP-009, GAP-301)

> **Note for reviewers:** Return to Analyst is offered in the user interface for count tests only, although the backend service also supports analysis workflows. (GAP-300)

> **Note for reviewers:** A reviewer cannot return a test to the analyst after the section has been reviewed; once the section reaches approval or later, modifications require the Section Head's rejection or retest decision. (BR-REVIEW-007)

#### What you see
- In the **Sample Summary** dialog, under the test results section, eligible count test orders display the **Return to Analyst** button.
- **Return Test to Analyst** dialog:
  - Dialog title: **Return Test to Analyst**.
  - Subtitle: "Return count test for recount or result re-entry".
  - Informational notice: "Returning this test order will soft-supersede active count readings, reopen the incubation step, and return the test to the assigned analyst. If the parent sample is Under Review, it will revert to In Testing."
  - Input field: **Reason for Return (optional)** with placeholder text "e.g. Plate count recount requested due to bubble artifact".
  - Action buttons: **Cancel** and **Confirm Return** (displays "Returning..." while processing).

[SCREENSHOT SS-181]

#### How to use it
1. Open the **Sample Summary** dialog by clicking the **Under Review** badge on the laboratory workspace or by clicking **Sample Summary**.
2. Locate the count test order requiring revision and expand its results card.
3. Click **Return to Analyst**.
4. In the **Return Test to Analyst** dialog, read the informational notice.
5. Optionally, type an explanation in the **Reason for Return (optional)** field.
6. Click **Confirm Return**.
7. The dialog closes, the sample summary reloads, and the test order returns to the analyst's workspace.

#### System behavior
- The system executes server checks in this sequence: confirms user access to the test order's section; verifies that the test order exists; validates that the section and sample are in an eligible state; confirms that the test order is in `ResultEntered` status; confirms the test definition exists; and verifies the workflow type.
- Active count test readings are set to inactive (`IsActive = false`). They remain preserved in the database for audit traceability.
- The latest incubation record is reopened: its completion timestamp, completing user, and incubation outcome are cleared.
- If the laboratory section was in **Under review**, it reverts to **In testing**, and the overall sample status rolls back to **Under Testing**.
- The test order workflow step moves to **Incubating** (for count tests), and its history records: "Returned to analyst: {reason}" (or "Returned to analyst by reviewer" if no reason was entered).
- A `TestReturnEvent` audit entry is recorded with the test order ID, reviewer ID, assigned analyst ID, return reason, and UTC timestamp.
- The assigned analyst remains unchanged.
- No electronic signature or password is required.

#### Validation
- If the test order cannot be found: "Test order {id} not found."
- If the sample has already been reviewed: "Cannot return a test to the analyst after the sample has been reviewed. Use Reject or Retest at approval instead."
- If the test is not in `ResultEntered` status: "Cannot return a test order in {status} status. Only test orders in ResultEntered status can be returned to the analyst."
- If the test definition cannot be found: "Test definition \"{code}\" not found."
- If the workflow type is unsupported: "Return to Analyst is only supported for Count Test and result-entry (analysis) workflows. \"{code}\" is a {workflow} workflow."
- If the user is not assigned to the section: "This test belongs to a laboratory section you are not assigned to."
- Client fallback error: "Failed to return test to analyst."

#### Status
- Laboratory section: if previously **Under review**, reverts to **In testing**.
- Test order: step reverts to **Incubating** (status `InProgress`).
- Sample status: if previously **Under review**, reverts to **Under Testing**.

#### What happens next
The assigned analyst sees the test order reopened in their testing workspace, performs the required recount or result re-entry, and re-submits results. Once all tests in the section reach the **Ready** step again, the section is automatically re-submitted for technical review (see §17 Review).


### 18.2 Retest of Retained Sample
<!-- WF-APPROVAL-002 -->
#### Purpose
Order a retest using retained physical sample material for selected non-conforming or questionable tests, superseding the original test orders and creating a single new retest sample linked to the original.

#### Who can use it
Requires the *Approve samples* permission (by default: Section Head, System Administrator). Users must hold an active membership in the laboratory section being authorized.

#### Before you start
- The laboratory section must be in **Under approval** status.
- The user must not be the section's reviewer ("You cannot approve a sample you reviewed.") and must not have performed any current test in the section ("You cannot approve a sample you tested.").
- A cause of testing named "Retest" must be configured in master data. If missing, the system blocks retest creation with: "The 'Retest' cause of testing is not configured."
- At least one test order must be selected for retesting.

> **Note for reviewers:** When a retest sample reaches a final status, its outcome is mirrored back to the origin sample section automatically without requiring a new electronic signature. (BR-APPROVAL-021)

#### What you see
- In the **Sample Summary** dialog, on the **Approval & Electronic Signatures** card:
  - Form: **Submit Release Decision**.
  - Dropdown: **Laboratory Section** (visible if multiple sections are awaiting decision).
  - Radio option: **Retest Retained Sample: Return to testing**.
  - Retest selection panel:
    - Heading: **Tests to Retest**.
    - Guidance text: "Non-conforming tests are pre-checked. Adjust as needed - only the checked test(s) move to the new sample(s); everything else on this sample is left untouched."
    - Checkbox list of all current, non-superseded test orders in the section, each showing its section chip and a **Non-conforming** indicator chip if out-of-specification.
    - Warning message (shown if all checkboxes are cleared): "Select at least one test to retest."
  - Text field: **Comment (optional)**.
  - Action button: **Submit Decision** (disabled if no tests are selected).
- **Electronic Signature** dialog:
  - Displays user's name, role, and the decision label "Retest Retained Sample: Return to testing".
  - Field: **Password**.
  - Action buttons: **Cancel** and **Sign**.

[SCREENSHOT SS-182]

#### How to use it
1. In the laboratory workspace, locate the sample and click the **Under Approval** badge (or click **Sample Summary**).
2. Scroll to the **Approval & Electronic Signatures** card.
3. If multiple sections are pending, select the appropriate section from the **Laboratory Section** dropdown.
4. Select the radio option **Retest Retained Sample: Return to testing**.
5. In the **Tests to Retest** panel, verify the pre-checked non-conforming tests and adjust test order checkboxes as needed.
6. Optionally, enter internal notes in the **Comment (optional)** field.
7. Click **Submit Decision**. The **Electronic Signature** dialog opens.
8. Enter your account password in the **Password** field.
9. Click **Sign**.

#### System behavior
- The system validates approver segregation of duties, verifies that at least one test is selected, checks that all selected tests belong to the section's current test orders, and confirms that the "Retest" cause of testing exists.
- An electronic signature is recorded with meaning `RetestRequested`.
- An `ApprovalDecisionMade` event is logged in the Workflow History with the decision `RetestRetainedSample`.
- The origin laboratory section sign-off status advances to **Retest requested**.
- If the origin sample does not already have an OOS tracking group code, the system generates a new unique `OosGroupCode`.
- The selected original test orders are marked as superseded (`IsSuperseded = true`) with the history note: "Retest ordered by {name} - new sample pending reference number". Unchecked test orders on the original sample remain untouched.
- Exactly ONE new sample is created:
  - Assigned status **Received** and cause of testing "Retest".
  - Inherits identity attributes from the original sample (item, category, batch, control number, dates, quantity, sampling point, machine, department, and production stage).
  - Linked to the original sample through `OriginSampleId` and shares the same `OosGroupCode`.
  - New test orders are created for the selected tests with status `Pending` and step `Waiting`, assigned to the original analysts.
  - For Water, Environmental Monitoring, and After Cleaning samples, only failed locations are carried over to the new test orders (or all locations if none failed), and sample preparation is set to **Ready**. For other categories, normal preparation status applies.
- The parent sample status is recomputed.

#### Validation
- If no test is selected: "At least one test must be selected for retest."
- If an invalid test is selected: "One or more selected tests do not belong to this sample's current test orders."
- If the "Retest" cause of testing is not configured: "The 'Retest' cause of testing is not configured."
- If the approver reviewed the section: "You cannot approve a sample you reviewed."
- If the approver performed any testing on the section: "You cannot approve a sample you tested."
- If password verification fails: "Password verification failed. The signature was not applied."

#### Status
- Origin section sign-off status: becomes **Retest requested**.
- Selected origin test orders: marked superseded (`IsSuperseded = true`).
- New sample status: created in **Received** status.

#### What happens next
The new retest sample appears in the testing workspace for the assigned analyst to execute. The retest chain is tracked in OOS Tracking (see §20 OOS Tracking). When the retest sample completes review and approval, its final outcome (Approved or Rejected) is automatically mirrored back to the origin sample section.


### 18.3 Requesting a New Sample
<!-- WF-APPROVAL-002 -->
#### Purpose
Reject testing on non-conforming or compromised tests and order two entirely new physical samples to be drawn and tested in parallel by two distinct analysts.

#### Who can use it
Requires the *Approve samples* permission (by default: Section Head, System Administrator). Loading the analyst list in the user interface requires the *Assign responsible analyst* permission (by default: Section Head, System Administrator). Users must hold an active membership in the laboratory section.

#### Before you start
- The laboratory section must be in **Under approval** status.
- Segregation of duties: The user must not have reviewed or tested the section.
- Two distinct active analysts with the Analyst role type must be available.
- The "Retest" cause of testing must be configured in master data.

> **Note for reviewers:** For New Sample Required, the screen states that neither analyst may be anyone who tested the original sample, but the server check verifies only the assigned analyst of the selected test orders. (GAP-302)

> **Note for reviewers:** New Sample Required and the retest decisions are signed with meanings Rejected and RetestRequested; the audit trail and certificate show a rejection for New Sample Required. (GAP-308)

#### What you see
- On the **Approval & Electronic Signatures** card:
  - Radio option: **New Sample Required: Close and request new sample**.
  - Panel: **Tests to Retest** (same checkbox selection as Retest Retained Sample).
  - Panel: **Analysts for the Two New Samples**:
    - Heading: **Analysts for the Two New Samples**.
    - Guidance note: "Two different analysts are required, and neither may be whoever tested the original sample."
    - Dropdowns: **New Sample 1 - Analyst** and **New Sample 2 - Analyst**.
    - Warning message (shown if the same analyst is chosen for both): "The two new samples must be assigned to two different analysts."
  - Text field: **Comment (optional)**.
  - Action button: **Submit Decision** (disabled if the analyst selection is invalid or incomplete).
- **Electronic Signature** dialog:
  - Confirmation statement: "New Sample Required: Close and request new sample".
  - Field: **Password**.

[SCREENSHOT SS-183]

#### How to use it
1. In the **Sample Summary** dialog, scroll to the **Approval & Electronic Signatures** card.
2. Select the radio option **New Sample Required: Close and request new sample**.
3. In the **Tests to Retest** panel, verify and adjust the test order checkboxes.
4. In the **Analysts for the Two New Samples** panel, select the first analyst in **New Sample 1 - Analyst**.
5. Select a different analyst in **New Sample 2 - Analyst**.
6. Optionally, enter internal notes in the **Comment (optional)** field.
7. Click **Submit Decision**.
8. Enter your password in the **Electronic Signature** dialog and click **Sign**.

#### System behavior
- The system validates approver segregation of duties, confirms test selections, validates that both selected analysts are active Analyst-role users, verifies that the two analysts are different, and verifies that neither analyst was the assigned analyst on any selected original test.
- An electronic signature is recorded with meaning `Rejected`.
- An `ApprovalDecisionMade` event is logged in the Workflow History with decision `NewSampleRequest`.
- The origin section sign-off status becomes **Retest requested**.
- The origin sample is assigned an `OosGroupCode` (if not already assigned).
- Selected original test orders are marked superseded (`IsSuperseded = true`) with the history note: "New sample requested by {name}".
- TWO new sample records are created sequentially with distinct reference numbers:
  - Both samples inherit sample metadata from the origin and link to `OriginSampleId` and `OosGroupCode`.
  - Sample 1 test orders are assigned to the first selected analyst.
  - Sample 2 test orders are assigned to the second selected analyst.
- Outcome propagation: The origin sample section waits for both new samples to complete authorization. The origin section receives `Approved` only if both new samples are approved; if either new sample is rejected, the origin section receives `Rejected`.

#### Validation
- If analysts are not assigned: "Two analysts must be assigned - one per new sample - when requesting a new sample."
- If the same analyst is selected twice: "The two new samples must be assigned to two different analysts."
- If an assigned user is inactive, locked, or lacks the Analyst role type: "Both retest analysts must be active, eligible Analyst-role users."
- If a chosen analyst tested the original sample: "Neither new sample's analyst may be the analyst who tested the original sample."
- If the eligible analyst list fails to load: "The list of eligible analysts could not be loaded. Close and reopen this sample to try again."

#### Status
- Origin section sign-off status: becomes **Retest requested**.
- Origin test orders: marked superseded (`IsSuperseded = true`).
- New sample records: two new samples created in **Received** status.

#### What happens next
Both new samples appear on the sample receiving and testing workspaces for their respective analysts. The entire investigation group is managed in OOS Tracking (see §20 OOS Tracking).
