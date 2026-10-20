## 19. Approval

MicroLIMS does not have a separate Final Release step. The Section Head's signed release decision on a laboratory section is the last approval step; **Approved** is the final status.

The release decision is made per laboratory section. The form offers four options:

| Option on the screen | Resulting section status | Signature meaning | Described in |
|---|---|---|---|
| **Approve: Results conform to specifications** | **Approved** | Approved | §19.1 |
| **Not Conform (Final Conclusion): Close this sample** | **Rejected** | Rejected | §19.2 |
| **New Sample Required: Close and request new sample** | **Retest requested** | Rejected | §18.3 |
| **Retest Retained Sample: Return to testing** | **Retest requested** | Retest requested | §18.2 |

The sample status is then recomputed from all of its sections.

> **Note for reviewers:** Review and approval are decided per laboratory section, not per test order. The Investigation and OOS Investigation decisions in the specification and Quick Table review are not available. (GAP-017, GAP-018)

> **Note for reviewers:** There is no Final Release step; Approved is the last status, shown as a "Release Decision" on the screen. (GAP-017, GAP-018)

### 19.1 Sample Authorization (Approve)
<!-- WF-APPROVAL-001 -->
#### Purpose
Release a laboratory section whose results conform to specification, with the Section Head's electronic signature and optional certificate remarks.

#### Who can use it
Requires the *Sample Release Approval* permission (by default: Section Head, System Administrator). You must be assigned to the laboratory section.

#### Before you start
- The laboratory section must be **Under approval**, which follows a signed technical review (§17). Otherwise: "Sample must be under approval before a decision can be made."
- Segregation of duties: you must not be the section's reviewer ("You cannot approve a sample you reviewed.") and must not have performed any current test of the section ("You cannot approve a sample you tested."). Both rules apply to every role, including System Administrator.
- For **Approve** only: every test that requires system suitability must have a linked HPLC run with a passed system suitability, and every analysis-type test must have an active entry and no pending stage.

> **Note for reviewers:** Reviewer and approver separation of duties is checked per laboratory section only; an approver who reviewed or tested a different laboratory section of the same sample is not blocked. (GAP-312)

> **Note for reviewers:** Approval of out-of-specification results is allowed after a warning; it certifies them as conforming. (GAP-304)

> **Note for reviewers:** Approval, rejection and close-testing signature statements are worded differently on the certificate, the sample report and the approval card. (GAP-315)

> **Note for reviewers:** Dashboard approval notifications count samples awaiting a release decision rather than individual test orders as described in the specification. (GAP-309)

#### What you see
- On the testing workspace, a sample waiting for release shows the **Under Approval** lifecycle badge. It is clickable only for users holding the *Sample Release Approval* permission.
- **Sample Summary** dialog, card **Approval & Electronic Signatures**, form **Submit Release Decision**:
  - **Laboratory Section** (when more than one section can be decided). Choosing a section fills **Certificate Remarks (optional)** with any remarks already stored for it.
  - **Comment (optional)**: an internal comment that is never printed on the certificate.
  - The four options listed above.
  - **Certificate Remarks (optional)**, shown only when **Approve** is selected. Helper text: "Printed on the Certificate of Analysis exactly as typed. Leave blank to show "No remarks." Separate from the internal comment above, which never appears on the certificate."
  - When **Approve** is selected and a test in the section is out of specification, the warning "Out-of-specification results in {section}: {tests}. Approving will certify them as conforming - consider Not Conform or a retest."
  - **Submit Decision** (disabled until the decision is valid).
- **Electronic Signature** dialog: your name and role, the text of the selected option (for example "Approve: Results conform to specifications"), the field **Password**, and the buttons **Cancel** and **Sign**.

[SCREENSHOT SS-191]

[SCREENSHOT SS-192]

#### How to use it
1. Click the **Under Approval** badge (or **Sample Summary**) of the sample.
2. Read the test results and the earlier review signature in the **Sample Summary** dialog.
3. Go to the **Approval & Electronic Signatures** card.
4. If several sections are waiting, choose yours in **Laboratory Section**.
5. Select **Approve: Results conform to specifications**.
6. Optionally, type an internal note in **Comment (optional)**.
7. Optionally, type the remarks to print on the certificate in **Certificate Remarks (optional)**. If left blank, the certificate shows "No remarks."
8. If the out-of-specification warning appears, decide whether to continue or to choose **Not Conform** or a retest instead. The warning does not block the approval.
9. Click **Submit Decision**. The **Electronic Signature** dialog opens.
10. Type your account password in **Password**.
11. Click **Sign**.

#### System behavior
- The system checks, in this order: the sample exists; the laboratory section and your access to it; you are not the section's reviewer; you did not perform a current test of the section; the system suitability and analysis-entry requirements for **Approve**; your password.
- When all checks pass, everything is saved together:
  - An electronic signature with the meaning *Approved*, and a Workflow History event recording the decision.
  - The section becomes **Approved**, recording the approver, the time and the trimmed certificate remarks.
  - Every current test of the section is marked **Approved**, with the history entry "Sample approved by {name}".
- When no section of the sample is open any more and none is rejected, the sample becomes **Approved**.
- When the sample reaches **Approved** or **Rejected**, the summary report is saved as a frozen PDF archived copy labelled "Sample Approve" (see §21.3).

#### Validation
- "Sample must be under approval before a decision can be made."
- "You cannot approve a sample you reviewed."
- "You cannot approve a sample you tested."
- "Cannot approve section: test order {id} (\"{code}\") requires system suitability, but only HPLC workspace tests (assay, dissolution) can supply one. Clear \"Requires system suitability\" for this test in Test Master."
- "Cannot approve section: test order {id} (\"{code}\") lacks an HPLC run with a passed system suitability."
- "Cannot approve section: test order {id} (\"{code}\") lacks an active {display name} entry."
- "Cannot approve section: test order {id} (\"{code}\") has a pending stage."
- "'{decision}' is not a valid sample-level decision." (appears only if a decision other than the four options is submitted)
- "Password verification failed. The signature was not applied."
- "Too many failed signature attempts. Wait 15 minutes, then try again. The signature was not applied."

#### Status
- Section: **Under approval** to **Approved**.
- Tests: each current test of the section becomes **Approved**.
- Sample: **Approved** when no section is open and none is rejected; otherwise it follows the roll-up of the other sections.

#### What happens next
View or print the sample report and the Certificate of Analysis (see §21). If the sample is not yet final, other laboratory sections continue with their own review and approval.


### 19.2 Rejection (Not Conform)
<!-- WF-APPROVAL-001 -->
#### Purpose
Reject a laboratory section whose results do not conform to specification, using the option **Not Conform (Final Conclusion): Close this sample**.

#### Who can use it
Requires the *Sample Release Approval* permission (by default: Section Head, System Administrator). You must be assigned to the laboratory section.

#### Before you start
- The laboratory section must be **Under approval**.
- The same segregation-of-duties rules as in §19.1 apply: you must not be the section's reviewer and must not have performed any current test of the section.

> **Note for reviewers:** "Not Conform (Final Conclusion): Close this sample" rejects the deciding section only; other laboratories continue. The comments in the system's own code contradict each other on this point. (GAP-303)

#### What you see
- On the card **Approval & Electronic Signatures**, form **Submit Release Decision**: the option **Not Conform (Final Conclusion): Close this sample**, **Comment (optional)** and **Submit Decision**.
- **Electronic Signature** dialog with the text "Not Conform (Final Conclusion): Close this sample" and the field **Password**.

#### How to use it
1. In the **Sample Summary** dialog, go to the **Approval & Electronic Signatures** card.
2. If several sections are waiting, choose yours in **Laboratory Section**.
3. Select **Not Conform (Final Conclusion): Close this sample**.
4. Optionally, type an explanation in **Comment (optional)**.
5. Click **Submit Decision**.
6. Type your account password in the **Electronic Signature** dialog and click **Sign**.

#### System behavior
- One electronic signature is recorded with the meaning *Rejected*, and a Workflow History event records the decision.
- The section becomes **Rejected**, recording the approver and the time.
- Every current test of the section is marked **Rejected** (its step is unchanged), with the history entry "Sample rejected by {name}".
- Other laboratory sections of the sample are not closed. They stay open and carry on.
- When the sample reaches **Approved** or **Rejected**, the summary report is saved as a frozen PDF archived copy labelled "Sample Reject".

#### Validation
- The same messages as in §19.1 for the section state, segregation of duties and password.

#### Status
- Section: **Rejected**.
- Tests: **Rejected**.
- Sample: recomputed from all sections. While another section is open, the sample shows the least advanced open status; once no section is open, a rejected section makes the sample **Rejected**.

#### What happens next
If another laboratory still has open tests on the sample, it can continue or use Close Testing (§19.3).


### 19.3 Closing Laboratory Testing After Another Laboratory Rejected
<!-- WF-APPROVAL-003 -->
#### Purpose
Stop the remaining open tests of your laboratory section after another laboratory section has rejected the sample.

#### Who can use it
Requires the *Sample Release Approval* permission (by default: Section Head, System Administrator). You must be assigned to the laboratory section being closed.

#### Before you start
- Another section of the sample must be **Rejected**. Otherwise: "Testing can be closed only after another laboratory rejected the sample."
- Your section must be open (**In testing**, **Under review** or **Under approval**). If it is already closed: "This laboratory's testing is already closed." If it has no open tests: "This laboratory has no open tests on the sample."
- A reason is required.

#### What you see
- In the **Sample Summary** dialog, when the overall outcome is Rejected and you have a closable section, the block **Close Testing** with the warning "Another laboratory has rejected this sample. You may close your own laboratory's remaining tests instead of continuing them.", a section select (when several sections can be closed) and the button **Close Testing**.
- Dialog **Close Testing: {section}**:
  - Subtitle "Stops your laboratory's remaining tests on this sample - signed with your password".
  - Text "Every still-open test in this laboratory will be recorded as Cancelled at the stage it had reached. Approved results already on file are kept."
  - Field **Reason** (required), pre-filled with "Sample rejected by {lab}".
  - Buttons **Cancel** and **Sign & Close** (disabled while **Reason** is empty).
- **Electronic Signature** dialog with the text "I am closing this laboratory's testing; it will not be resumed on this sample." and the field **Password**.

[SCREENSHOT SS-193]

#### How to use it
1. Open the **Sample Summary** dialog of the sample.
2. Find the **Close Testing** block.
3. If several sections can be closed, choose yours.
4. Click **Close Testing**.
5. Read the text in the **Close Testing: {section}** dialog.
6. Check or edit the **Reason**.
7. Click **Sign & Close**.
8. In the **Electronic Signature** dialog, type your account password.
9. Click **Sign**.

#### System behavior
- The system checks, in this order: the reason is not blank; the sample exists; your access to the section; the section belongs to the sample; another section is rejected; the section is not already closed; the section has open tests.
- One electronic signature is recorded with the meaning *Testing closed*, and a Workflow History event records the reason.
- Every current test of the section that is not already Approved, Voided or Cancelled becomes **Cancelled**, with the step and incubation stage it had reached recorded and the history entry "Testing closed by {name}: {reason}". Approved results already on file are kept.
- The section becomes **Cancelled** (shown as **Closed**), recording who closed it, when, and the reason.
- The sample status is recomputed. If the sample is now final, the report is saved as a frozen PDF archived copy labelled "Laboratory testing closed".

#### Important rules
- The reviewer and tester checks used for Approve do not apply to Close Testing.

#### Validation
- "A reason is required to close testing."
- "Testing can be closed only after another laboratory rejected the sample."
- "This laboratory's testing is already closed."
- "This laboratory has no open tests on the sample."
- "This sample has no tests in the selected laboratory section."
- "This test belongs to a laboratory section you are not assigned to."
- "Password verification failed. The signature was not applied."

#### Status
- Section: **Cancelled** (shown as **Closed**).
- Open tests: **Cancelled**.
- Sample: recomputed from all sections.

#### What happens next
On the combined Certificate of Analysis the closed laboratory's tests are listed as "{test}: Not completed, testing closed ({step}, stage {n})." No section-only certificate exists for a closed section.

<!-- Review log (Sonnet): 20 corrections; invented items removed: "active membership", enum/database/transaction wording, "approval override", "workflow complete" claim, "recomputed to Rejected" for close testing, "immutable"/"cryptographic" claims; added decision-options table with resulting statuses and signature meanings; callouts added: none missing (GAP-017/018 incl. no Final Release, GAP-312, GAP-304, GAP-303, GAP-315, GAP-309 present) -->
