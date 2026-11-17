## 18. Return to Analyst, Rework and Retest

### 18.1 Return a Test to the Analyst
<!-- WF-REVIEW-002 -->
#### Purpose
Send a count test or a physicochemical result-entry test back to the assigned analyst for a recount or result re-entry before the review is signed. The current count readings or active analysis entries are set aside and the test returns to testing.

#### Who can use it
Requires the *Sample Technical Review* permission (by default: Reviewer, Section Head, System Administrator). You must be assigned to the laboratory section of the test.

#### Before you start
- The test is a count test or a physicochemical result-entry test whose result is entered. Pathogen, location (EM/Water/After Cleaning) and biochemical tests do not offer the button.
- The test must not be superseded.
- The test's laboratory section must be **In testing** or **Under review**, and the sample must not be **Under approval**, **Approved**, **Rejected**, **Retest requested**, **Cancelled** or **Voided**. Once the section has been reviewed the system refuses with: "Cannot return a test to the analyst after the sample has been reviewed. Use Reject or Retest at approval instead."

> **Note for reviewers:** Return to Analyst takes no password or signature, the reason is optional, and the reviewer who performed the test is not blocked. (GAP-009, GAP-301)

> **Note for reviewers:** A test cannot be returned after the section has been reviewed; correction then requires the Section Head's Not Conform or retest decision (see §18.2 and §19). This follows the "Cannot return a test…" message above. (GAP-018)

#### What you see
- In the **Sample Summary** dialog, an eligible count test shows the button **Return to Analyst**.
- Dialog **Return Test to Analyst**:
  - Subtitle "Return count test for recount or result re-entry".
  - Information text: "Returning this test order will soft-supersede active count readings, reopen the incubation step, and return the test to the assigned analyst. If the parent sample is Under Review, it will revert to In Testing."
  - Field **Reason for Return (optional)**, with the example text "e.g. Plate count recount requested due to bubble artifact".
  - Buttons **Cancel** and **Confirm Return** (shows "Returning..." while working).

[SCREENSHOT SS-181]

#### How to use it
1. Open the **Sample Summary** dialog (the lifecycle badge or **Sample Summary**).
2. Find the test and expand it.
3. Click **Return to Analyst**.
4. Read the information text in the **Return Test to Analyst** dialog.
5. Optionally, type a reason in **Reason for Return (optional)**.
6. Click **Confirm Return**. No password is asked.
7. The dialog closes and the summary reloads.

#### System behavior
- The system checks, in this order: your access to the test's section; the test exists; the section and sample are in an allowed state; the test has its result entered; the test definition exists; the test type is supported.
- For a count test: the active count readings are set aside (kept in the record but no longer current), and the latest incubation is reopened (its completion time, who completed it and its outcome are cleared).
- For a physicochemical (result-entry) test, the active analysis entries and parameter results are set aside, no incubation is reopened, and the test goes back to **Running**.
- If the section was **Under review**, it goes back to **In testing** and the sample status is recomputed.
- The test moves to the **Incubating** step (count tests) or **Running** (physicochemical tests) with the history note "Returned to analyst: {reason}" (or "Returned to analyst by reviewer" when no reason was typed).
- A return event is stored with the reviewer, the assigned analyst, the reason and the time. The assigned analyst does not change.
- No electronic signature is recorded.

#### Validation
- "Test order {id} not found."
- "Cannot return a test to the analyst after the sample has been reviewed. Use Reject or Retest at approval instead."
- "Cannot return a test order in {status} status. Only test orders in ResultEntered status can be returned to the analyst."
- "Test definition \"{code}\" not found."
- "Return to Analyst is only supported for Count Test and result-entry (analysis) workflows. \"{code}\" is a {workflow} workflow."
- "This test belongs to a laboratory section you are not assigned to."
- "Failed to return test to analyst." (shown when the system gives no other message)

#### Status
- Section: **Under review** goes back to **In testing**.
- Test: **Incubating** for a count test, **Running** for a result-entry test.
- Sample: recomputed from all sections.

#### What happens next
The analyst recounts or re-enters the readings in the testing workspace. When all tests of the section are at the **Ready** step again, the section is submitted for review automatically (see §17.1).


### 18.2 Retest of Retained Sample
<!-- WF-APPROVAL-002 -->
#### Purpose
Order a retest of selected tests as a Section Head decision. The selected tests are superseded on the original sample and one new retest sample, linked to the original, receives new copies of them.

#### Who can use it
Requires the *Sample Release Approval* permission (by default: Section Head, System Administrator). You must be assigned to the laboratory section.

#### Before you start
- The laboratory section must be **Under approval**.
- You must not be the section's reviewer ("You cannot approve a sample you reviewed.") and must not have performed any current test of the section ("You cannot approve a sample you tested."). No role is exempt, including System Administrator.
- A cause of testing named "Retest" must exist. Otherwise: "The 'Retest' cause of testing is not configured."
- At least one test must be selected.

> **Note for reviewers:** When a retest sample reaches a final status, its outcome is copied to the original sample's section without a new electronic signature. The original section takes the outcome (Retest Retained Sample: the retest's outcome; New Sample Required: Approved only when both new samples are approved). Each original test is then marked Approved or Rejected from its own recorded result, so the test that triggered the retest stays **Rejected**. The original section records the Section Head who decided the retest as its approver; no new signature is applied to the original. This happens only when the retest sample covers one laboratory section. (GAP-018)

> **Note for reviewers:** The Investigation and OOS Investigation decisions in the design specifications are not available; only the four decisions in §19 exist. (GAP-017)

#### What you see
- **Sample Summary** dialog, card **Approval & Electronic Signatures**, form **Submit Release Decision**:
  - **Laboratory Section** (when more than one section is waiting).
  - The option **Retest Retained Sample: Return to testing**.
  - The block **Tests to Retest**, with the text "Non-conforming tests are pre-checked. Adjust as needed - only the checked test(s) move to the new sample; everything else on this sample is left untouched." (for **New Sample Required** the text reads 'new samples') and one checkbox per current test, each with a section chip and a **Non-conforming** chip where the screen judges the test non-conforming.
  - The warning "Select at least one test to retest." when no box is checked.
  - **Comment (optional)** and **Submit Decision** (disabled until the decision is valid).
- **Electronic Signature** dialog with your name, role, the text "Retest Retained Sample: Return to testing", the field **Password** and the buttons **Cancel** and **Sign**.

[SCREENSHOT SS-182]

#### How to use it
1. Click the **Under Approval** badge (or **Sample Summary**) of the sample.
2. Go to the **Approval & Electronic Signatures** card.
3. If several sections are waiting, choose yours in **Laboratory Section**.
4. Select **Retest Retained Sample: Return to testing**.
5. In **Tests to Retest**, check the pre-checked tests and adjust the boxes.
6. Optionally, type an internal note in **Comment (optional)**.
7. Click **Submit Decision**. The **Electronic Signature** dialog opens.
8. Type your password in **Password**.
9. Click **Sign**.

#### System behavior
- The system checks, in this order: the reviewer and tester rules above; at least one test is selected; the selected tests belong to the section's current tests; the "Retest" cause exists; your password.
- One electronic signature is recorded with the meaning *Retest requested*, and a Workflow History event records the decision.
- The section becomes **Retest requested**.
- If the original sample has no OOS group yet, a group number is created for it.
- The selected original tests are marked superseded, with the history note "Retest ordered by {name} - new sample pending reference number". The other tests of the original sample are not touched.
- ONE new sample is created in **Received** status with the cause "Retest". It copies the identity details of the original (item, category, batch, control number, dates, quantity, sampling point, machine, department, production stage), is linked to the original and shares its OOS group. It holds new copies of the selected tests, pending and waiting, with the same assigned analyst.
- For Water, Environmental Monitoring and After Cleaning samples only the failed locations are copied (all locations if none failed) and the preparation screen is skipped. Other samples get the normal initial preparation status.
- The sample status is recomputed.

#### Validation
- "At least one test must be selected for retest."
- "One or more selected tests do not belong to this sample's current test orders."
- "The 'Retest' cause of testing is not configured."
- "You cannot approve a sample you reviewed."
- "You cannot approve a sample you tested."
- "Select at least one test to retest." (on the screen)
- "Password verification failed. The signature was not applied."

#### Status
- Original section: **Retest requested**.
- Selected original tests: superseded.
- New sample: **Received**.

#### What happens next
The new retest sample appears in the testing workspace and goes through testing, review and approval itself. The group appears in OOS Tracking (see §20) for users with the *OOS Tracking Management* permission. When the retest sample is finally approved or rejected, its outcome is copied to the original sample's section (Retest Retained Sample: the retest's outcome; New Sample Required: Approved only when both new samples are approved). Each original test is then marked Approved or Rejected from its own recorded result, so the test that triggered the retest stays **Rejected**. The original section records the Section Head who decided the retest as its approver; no new signature is applied to the original. This happens only when the retest sample covers one laboratory section.


### 18.3 Requesting a New Sample
<!-- WF-APPROVAL-002 -->
#### Purpose
Order two new samples, each holding copies of the selected tests, to be tested by two different analysts. This is the decision **New Sample Required: Close and request new sample**.

#### Who can use it
Requires the *Sample Release Approval* permission (by default: Section Head, System Administrator). The list of analysts is loaded only for users holding the *Sample Analyst Assignment* permission (by default: Section Head, System Administrator); otherwise the screen shows "The list of eligible analysts could not be loaded. Close and reopen this sample to try again." You must be assigned to the laboratory section.

#### Before you start
- The laboratory section must be **Under approval**.
- You must not be the section's reviewer and must not have performed any current test of the section.
- Two different active analysts must be available.
- The cause of testing "Retest" must exist.

> **Note for reviewers:** For New Sample Required, the screen says neither analyst may be anyone who tested the original sample, but the system checks only the assigned analyst of the selected tests. (GAP-302)

> **Note for reviewers:** New Sample Required and the retest decisions are signed with the meanings Rejected and Retest requested; the signature trail and the certificate show a rejection for New Sample Required. (GAP-308)

#### What you see
- On the card **Approval & Electronic Signatures**:
  - The option **New Sample Required: Close and request new sample**.
  - The block **Tests to Retest** (same as in §18.2).
  - The block **Analysts for the Two New Samples** with the text "Two different analysts are required, and neither may be whoever tested the original sample." and the selects **New Sample 1 - Analyst** and **New Sample 2 - Analyst**.
  - The warning "The two new samples must be assigned to two different analysts." when the same analyst is chosen twice.
  - **Comment (optional)** and **Submit Decision** (disabled until at least one test and two different analysts are chosen).
- **Electronic Signature** dialog with the text "New Sample Required: Close and request new sample" and the field **Password**.

[SCREENSHOT SS-183]

#### How to use it
1. Open the **Sample Summary** dialog and go to the **Approval & Electronic Signatures** card.
2. Select **New Sample Required: Close and request new sample**.
3. In **Tests to Retest**, check and adjust the boxes.
4. Choose an analyst in **New Sample 1 - Analyst**.
5. Choose a different analyst in **New Sample 2 - Analyst**.
6. Optionally, type an internal note in **Comment (optional)**.
7. Click **Submit Decision**.
8. Type your password in the **Electronic Signature** dialog and click **Sign**.

#### System behavior
- The system checks the reviewer and tester rules, the selected tests, that both analysts are given, different, active, not locked and of the Analyst role type, and that neither is the assigned analyst of a selected test.
- One electronic signature is recorded with the meaning *Rejected*, and a Workflow History event records the decision.
- The section becomes **Retest requested**, and the original sample receives an OOS group number if it has none.
- The selected original tests are marked superseded, with the history note "New sample requested by {name}".
- TWO new samples are created one after the other, each with its own reference number, the details of the original sample, a link to it, the shared OOS group, and copies of the selected tests assigned to its own chosen analyst.
- Outcome: the original section waits until both new samples have a final status. It then takes **Approved** only if both are approved, otherwise **Rejected**. No new signature is recorded for this (see §18.2).

#### Validation
- "Two analysts must be assigned - one per new sample - when requesting a new sample."
- "The two new samples must be assigned to two different analysts."
- "Both retest analysts must be active, eligible Analyst-role users."
- "Neither new sample's analyst may be the analyst who tested the original sample."
- "At least one test must be selected for retest."
- "The 'Retest' cause of testing is not configured."
- "The list of eligible analysts could not be loaded. Close and reopen this sample to try again."

#### Status
- Original section: **Retest requested**.
- Selected original tests: superseded.
- Two new samples: **Received**.

#### What happens next
The two new samples appear in the testing workspace for their analysts and follow the normal test, review and approval workflow. The group is followed in OOS Tracking (see §20).

<!-- Review log (Sonnet): 22 corrections; invented items removed: "active membership", enum/database/ID wording (ResultEntered code kept only inside exact message), InProgress test status, "in parallel", "sample receiving" workspace, "master data", analyst-notification claim; callouts added: GAP-017 (Investigation/OOS Investigation not available), GAP-018 on after-review return and retest mirroring -->
<!-- Phase 13 (agy): applied F-B01, F-B02, F-B08, F-B12 -->

<!-- Phase 13 review (Sonnet): F-B01, F-B02, F-B08, F-B12 verified; 2 wording fixes (status line, duplicated sentence); no invented items. -->
