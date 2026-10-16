## 19. Approval

MicroLIMS does not have a separate Final Release step. Release is completed upon Section Head approval, after which the sample reaches approved status and Certificates of Analysis can be generated.

### 19.1 Sample Authorization (Approve)
<!-- WF-APPROVAL-001 -->
#### Purpose
Authorize and release a laboratory section's test results when they conform to specifications, providing Section Head electronic sign-off, recording optional certificate remarks, and advancing the sample toward final release.

#### Who can use it
Requires the *Approve samples* permission (by default: Section Head, System Administrator). Users must hold an active membership in the laboratory section being authorized.

#### Before you start
- The laboratory section must be in **Under approval** status following completion of technical review. If attempting to approve a section in another state, the system displays: "Sample must be under approval before a decision can be made."
- Segregation of duties: The approver cannot be the section's reviewer ("You cannot approve a sample you reviewed.") and cannot have performed any current test within the laboratory section ("You cannot approve a sample you tested."). Both segregation rules apply to all user roles, including System Administrator.
- Analytical prerequisites:
  - If any test order in the section is configured in Test Master to require system suitability (HPLC assay or dissolution), it must have a linked HPLC run that achieved a passed system suitability result.
  - Every analysis-type test order must have an active result entry and cannot be in a pending stage.

> **Note for reviewers:** Review and approval are decided per laboratory section, not per test order; Quick Table review and Investigation / OOS Investigation decisions in the specification are not available. (GAP-017, GAP-018)

> **Note for reviewers:** There is no separate Final Release step; Approved is the last status, shown as a "Release Decision" on the screen. (GAP-017, GAP-018)

> **Note for reviewers:** Reviewer and approver separation of duties is evaluated per laboratory section only; an approver who tested or reviewed a different laboratory section of the same multi-laboratory sample is not blocked. (GAP-312)

> **Note for reviewers:** Approval of out-of-specification results is allowed after a warning; it certifies them as conforming. (GAP-304)

> **Note for reviewers:** Approval, rejection, and close-testing signature statements are worded differently between the Certificate of Analysis, the sample report page, and the approval card. (GAP-315)

> **Note for reviewers:** Dashboard approval notifications count samples awaiting release decision rather than individual test orders as described in earlier specifications. (GAP-309)

#### What you see
- On the laboratory workspace, samples awaiting release display the **Under Approval** lifecycle badge (interactive for users holding the *Approve samples* permission).
- **Sample Summary** dialog -> **Approval & Electronic Signatures** card:
  - Form: **Submit Release Decision**.
  - Dropdown: **Laboratory Section** (visible and mandatory if more than one section is awaiting approval; selecting a section pre-fills any saved certificate remarks).
  - Text field: **Comment (optional)** (internal comment; never printed on the Certificate of Analysis).
  - Radio options:
    - **Approve: Results conform to specifications**
    - **Not Conform (Final Conclusion): Close this sample**
    - **New Sample Required: Close and request new sample** (see §18)
    - **Retest Retained Sample: Return to testing** (see §18)
  - Text field: **Certificate Remarks (optional)** (visible only when **Approve** is selected). Helper text: "Printed on the Certificate of Analysis exactly as typed. Leave blank to show "No remarks." Separate from the internal comment above, which never appears on the certificate."
  - Warning banner (shown if any test order in the section has out-of-specification results): "Out-of-specification results in {section}: {tests}. Approving will certify them as conforming - consider Not Conform or a retest."
  - Action button: **Submit Decision** (disabled if required section selection is missing).
- **Electronic Signature** dialog:
  - Displays user's name, assigned role, and confirmation text: "Approve: Results conform to specifications".
  - Field: **Password** (mandatory).
  - Action buttons: **Cancel** and **Sign**.

[SCREENSHOT SS-191]

[SCREENSHOT SS-192]

#### How to use it
1. In the laboratory workspace, locate the sample and click the **Under Approval** badge (or click **Sample Summary**).
2. In the **Sample Summary** dialog, review the test results, raw readings, and prior technical review signatures.
3. Scroll to the **Approval & Electronic Signatures** card.
4. If multiple sections are pending, select your section in the **Laboratory Section** dropdown.
5. Select the radio option **Approve: Results conform to specifications**.
6. Optionally, type internal notes in the **Comment (optional)** field.
7. Optionally, enter formal remarks in the **Certificate Remarks (optional)** field. If left blank, the Certificate of Analysis will display "No remarks."
8. If an out-of-specification warning banner appears, verify whether an approval override is intended.
9. Click **Submit Decision**. The **Electronic Signature** dialog opens.
10. Enter your account password in the **Password** field.
11. Click **Sign**.

#### System behavior
- The system executes server checks in this sequence: verifies sample existence; resolves section scope; verifies approver segregation of duties against reviewer and performers; verifies system suitability requirements; validates analysis entry stages; and validates password authentication.
- All updates commit in a single database transaction:
  - An electronic signature is recorded with meaning `Approved`.
  - A Workflow History event of type `ApprovalDecisionMade` is recorded with decision `Approve`.
  - The laboratory section sign-off status advances to **Approved**, recording approver name, timestamp, and trimmed certificate remarks.
  - Every current test order in the section is updated to status `Approved` and step `Approved`, with a history entry: "Sample approved by {name}".
- Multi-section sample roll-up:
  - If this approval completes the last open laboratory section on the sample (and no section was rejected), the parent sample status becomes **Approved**.
  - When the sample reaches final approved status, an immutable summary report PDF is frozen and stored in the record archive with the label "Sample Approve" and a SHA-256 cryptographic hash.

#### Validation
- If the section is not in the approval stage: "Sample must be under approval before a decision can be made."
- If the approver reviewed the section: "You cannot approve a sample you reviewed."
- If the approver tested any current test in the section: "You cannot approve a sample you tested."
- If system suitability is required but unsupported by the test workflow: "Cannot approve section: test order {id} (\"{code}\") requires system suitability, but only HPLC workspace tests (assay, dissolution) can supply one. Clear \"Requires system suitability\" for this test in Test Master."
- If a required HPLC system suitability run is missing or not passed: "Cannot approve section: test order {id} (\"{code}\") lacks an HPLC run with a passed system suitability."
- If an analysis-type test lacks an active entry: "Cannot approve section: test order {id} (\"{code}\") lacks an active {display name} entry."
- If an analysis-type test has an incomplete stage: "Cannot approve section: test order {id} (\"{code}\") has a pending stage."
- If an unrecognized decision code is sent: "'{decision}' is not a valid sample-level decision."
- Password errors: "Password verification failed. The signature was not applied." / "Too many failed signature attempts. Wait 15 minutes, then try again. The signature was not applied."

#### Status
- Laboratory section sign-off status: advances to **Approved**.
- Test orders: advance to status `Approved` (workflow step `Approved`).
- Sample status: advances to **Approved** when all laboratory sections are approved.

#### What happens next
The sample testing workflow is complete. Users can open the printable sample summary report or view and print the official Certificate of Analysis (see §21 Sample Report, Certificate of Analysis and Archived Records).


### 19.2 Rejection (Not Conform)
<!-- WF-APPROVAL-001 -->
#### Purpose
Reject a laboratory section when test results fail to conform to specifications and no further retesting is warranted, closing the section as non-conforming.

#### Who can use it
Requires the *Approve samples* permission (by default: Section Head, System Administrator). Users must hold an active membership in the laboratory section.

#### Before you start
- The laboratory section must be in **Under approval** status.
- Segregation of duties: The user must not have reviewed or tested the section.

> **Note for reviewers:** "Not Conform (Final Conclusion): Close this sample" rejects the deciding section only; other laboratories continue; the code comments contradict each other. (GAP-303)

#### What you see
- On the **Approval & Electronic Signatures** card:
  - Form: **Submit Release Decision**.
  - Radio option: **Not Conform (Final Conclusion): Close this sample**.
  - Text field: **Comment (optional)** (internal rejection notes).
  - Action button: **Submit Decision**.
- **Electronic Signature** dialog:
  - Confirmation statement: "Not Conform (Final Conclusion): Close this sample".
  - Field: **Password**.

#### How to use it
1. In the **Sample Summary** dialog, locate the **Approval & Electronic Signatures** card.
2. If multiple sections are pending, select your section from the **Laboratory Section** dropdown.
3. Select the radio option **Not Conform (Final Conclusion): Close this sample**.
4. Optionally, enter an explanation in the **Comment (optional)** field.
5. Click **Submit Decision**.
6. Enter your account password in the **Electronic Signature** dialog and click **Sign**.

#### System behavior
- An electronic signature is recorded with meaning `Rejected`.
- A Workflow History event of type `ApprovalDecisionMade` is recorded with decision `Reject`.
- The laboratory section sign-off status advances to **Rejected**, recording approver name and timestamp.
- Each current test order in the section is set to status `Rejected` (workflow step remains unchanged), with a history note: "Sample rejected by {name}".
- Other laboratory sections on multi-section samples are NOT automatically closed; they remain open and continue testing.
- When the sample reaches a final state across all sections, an immutable summary report PDF is frozen and archived with the label "Sample Reject" and a SHA-256 hash.

#### Validation
- Same segregation-of-duties and password validation rules as §19.1.

#### Status
- Laboratory section sign-off status: becomes **Rejected**.
- Test orders: become status `Rejected`.
- Sample status: recomputed according to the section roll-up rule. If other laboratory sections are still open, the overall sample status remains at the least advanced open state; once all sections are finalized, the overall sample reflects **Rejected**.

#### What happens next
The rejected section is closed. If another laboratory section has open tests on the same sample, that section can choose to continue testing or execute the Close Testing procedure (see §19.3).


### 19.3 Closing Laboratory Testing After Another Laboratory Rejected
<!-- WF-APPROVAL-003 -->
#### Purpose
Discontinue and cancel open tests in a laboratory section when another laboratory section has already rejected the sample, avoiding unnecessary testing on an already non-conforming batch.

#### Who can use it
Requires the *Approve samples* permission (by default: Section Head, System Administrator). Users must hold an active membership in the laboratory section being closed.

#### Before you start
- Another laboratory section on the sample must already be in **Rejected** status. If no section is rejected, the system displays: "Testing can be closed only after another laboratory rejected the sample."
- The user's laboratory section must have open tests in **In testing**, **Under review**, or **Under approval**. If already closed, the system displays: "This laboratory's testing is already closed." If no open tests remain, it displays: "This laboratory has no open tests on the sample."
- A non-blank justification reason is mandatory.

#### What you see
- In the **Sample Summary** dialog, when the overall sample outcome is Rejected and the user's section has open tests:
  - Panel: **Close Testing**.
  - Warning banner: "Another laboratory has rejected this sample. You may close your own laboratory's remaining tests instead of continuing them."
  - Dropdown: Section selector (visible if multiple sections can be closed).
  - Action button: **Close Testing**.
- **Close Testing** dialog:
  - Dialog title: **Close Testing: {section}**.
  - Subtitle: "Stops your laboratory's remaining tests on this sample - signed with your password".
  - Informational notice: "Every still-open test in this laboratory will be recorded as Cancelled at the stage it had reached. Approved results already on file are kept."
  - Text area: **Reason** (mandatory; pre-filled with "Sample rejected by {lab}").
  - Action buttons: **Cancel** and **Sign & Close** (disabled while the Reason field is empty).
- **Electronic Signature** dialog:
  - Confirmation statement: "I am closing this laboratory's testing; it will not be resumed on this sample."
  - Field: **Password**.

[SCREENSHOT SS-193]

#### How to use it
1. Open the **Sample Summary** dialog for the rejected sample.
2. Locate the **Close Testing** panel.
3. If multiple sections are closable, select your section from the dropdown.
4. Click **Close Testing**.
5. In the **Close Testing: {section}** dialog, review the informational text.
6. Review or edit the justification in the **Reason** field (mandatory).
7. Click **Sign & Close**.
8. In the **Electronic Signature** dialog, enter your account password.
9. Click **Sign**.

#### System behavior
- Checks that another laboratory rejected the sample, that the user is assigned to the section, that the section has open tests, and that a reason is provided.
- An electronic signature is recorded with meaning `TestingClosed`.
- A Workflow History event of type `SectionTestingClosed` is recorded with the closure reason.
- Every current test order in the section that is not already Approved, Voided, or Cancelled is set to status `Cancelled`, recording the workflow step and incubation stage it had reached, with a history note: "Testing closed by {name}: {reason}".
- Previously approved test orders on file remain untouched.
- The laboratory section sign-off status advances to **Cancelled** (displayed in the user interface as **Closed**), recording the user, timestamp, reason, and signature.
- If this closure leaves no remaining open sections on the sample, an immutable summary report PDF is frozen and archived with the label "Laboratory testing closed".

#### Validation
- If the reason is empty: "A reason is required to close testing."
- If no other section has rejected the sample: "Testing can be closed only after another laboratory rejected the sample."
- If the section is already closed: "This laboratory's testing is already closed."
- If the section has no open tests: "This laboratory has no open tests on the sample."
- If the user is not assigned to the section: "This test belongs to a laboratory section you are not assigned to."

#### Status
- Section sign-off status: becomes **Cancelled** (displayed as **Closed**).
- Open test orders: become status `Cancelled`.
- Overall sample status: recomputed to **Rejected**.

#### What happens next
Testing in the closed laboratory is terminated. On the multi-section Certificate of Analysis, the closed laboratory's tests are reported as: "{test}: Not completed, testing closed ({step}, stage {n})."
