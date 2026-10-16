## 17. Review

### 17.1 Automatic Submission for Review
#### Purpose
Automatically transition a laboratory section from active testing to review status as soon as all of its test orders have completed analytical execution and reached the ready step.

#### Who can use it
This is an automated system process. No direct user action, password, or electronic signature is involved.

#### Before you start
All current (non-superseded) test orders assigned to the laboratory section must reach the **Ready** step. The parent sample must be in **Received** or **In Testing** status, and the laboratory section must currently be in **In testing** status with at least one active test order.

#### System behavior
- When the final test order in a laboratory section reaches the **Ready** step, the system automatically advances the section sign-off status from **In testing** to **Under review**.
- An entry is recorded in the sample Workflow History with the event description: "All tests completed - automatically submitted for review". No electronic signature is captured for this automated transition.
- The system updates user dashboard indicators to alert qualified personnel. Users holding the Reviewer, Section Head, or System Administrator role see the sample reflected in their workload indicator: "{n} sample(s) awaiting review".

> **Note for reviewers:** Dashboard review notifications count samples awaiting review rather than individual test orders as described in earlier specifications. (GAP-309)

#### Status
- Laboratory section sign-off status changes from **In testing** to **Under review**.
- Overall sample status updates according to the multi-section roll-up rule: if no other section remains in active testing, the sample displays the **Under Review** lifecycle badge.

#### Important rules
- Automatic submission occurs independently for each laboratory section on multi-section samples (such as samples split between Microbiology and Physicochemical laboratories). One section can enter review while the other continues testing.


### 17.2 Technical Review of a Laboratory Section
<!-- WF-REVIEW-001 -->
#### Purpose
Examine recorded test results, raw readings, observations, and workflow history for a laboratory section, verify completeness and accuracy, and apply an electronic signature certifying that technical review is complete.

#### Who can use it
Requires the *Review samples* permission (by default: Reviewer, Section Head, System Administrator). Users must hold an active membership in the laboratory section being reviewed (System Administrator accounts have unrestricted organizational access by default).

#### Before you start
- The laboratory section must be in **Under review** status. If attempting to review a section in another state, the system displays: "Sample must be under review before it can be reviewed."
- Every current test order in the section must be at the **Ready** step. If any test order is incomplete, the system displays: "Cannot complete review: one or more tests are not in Ready step."
- Segregation of duties: The reviewer must not have performed any current test within the laboratory section. If the reviewer is the assigned analyst, the sample preparer, or recorded any result, count reading, analysis entry, pathogen observation, workflow step result, or confirmatory plate observation for any active test order in that section, the system blocks review with: "You cannot review a sample you tested." This rule applies to all user roles, including System Administrator.

> **Note for reviewers:** Review and approval are decided per laboratory section, not per test order; Quick Table review and per-test-order review described in design specifications are not implemented. (GAP-017, GAP-018)

> **Note for reviewers:** Reviewer separation of duties is evaluated per laboratory section only; a reviewer who performed testing in a different laboratory section of the same multi-laboratory sample is not blocked. (GAP-312)

#### What you see
- Access controls:
  - On the laboratory workspace, the sample displays the **Under Review** lifecycle badge (interactive for users holding the *Review samples* permission).
  - Alternatively, the **Sample Summary** button opens the review interface.
- **Sample Summary** dialog:
  - Dialog title: **Sample Summary**.
  - Section indicators: **Sections:** followed by section status chips (e.g., "Microbiology: Under review", "Physicochemical: In testing").
  - Header actions: **Open Pathogen Workflow** (visible if a test uses a shared TSB broth), **Printable Report**, **View COA** (visible when certificate results exist and the sample or section is eligible), and **Export PDF**.
  - **Workflow History** strip: chronological list of sample lifecycle events (displays "No lifecycle events yet." if empty).
  - Test results section: detailed results, raw readings, incubation parameters, and analyst comments for each test order.
  - Card: **Approval & Electronic Signatures**:
    - Status message: "Awaiting technical review by authorized reviewer."
    - Form: **Submit Technical Review**
      - Dropdown: **Laboratory Section** (visible and mandatory only when more than one section on the sample is awaiting review).
      - Guidance alert: "By submitting, I confirm I have reviewed all test results for {section}." (or "... for this sample.").
      - Text field: **Comment (optional)** (internal comment, not printed on the Certificate of Analysis).
      - Action button: **Submit Review** (disabled if multiple sections are awaiting review and none has been selected).
  - Dialog footer: "Sample Reference: {ref} · Record Status: {status} · MicroLIMS Operational View".
- **Electronic Signature** dialog:
  - Title: **Electronic Signature**.
  - Displays user's full name (or username) and assigned role.
  - Confirmation statement: "By submitting, I confirm I have reviewed all test results for {section}." (or "... for this sample.").
  - Input field: **Password** (mandatory; pressing Enter submits).
  - Action buttons: **Cancel** and **Sign** (displays "Signing..." during processing; disabled while the password field is empty).

[SCREENSHOT SS-171]

[SCREENSHOT SS-172]

#### How to use it
1. In the laboratory workspace, locate the sample and click the **Under Review** badge (or click **Sample Summary**).
2. In the **Sample Summary** dialog, inspect the recorded results, readings, incubation history, and raw data for all test orders in the section.
3. Review the **Workflow History** strip to verify prior lifecycle steps.
4. Scroll to the **Approval & Electronic Signatures** card.
5. If the sample contains tests in multiple laboratories and more than one section is awaiting review, select your section from the **Laboratory Section** dropdown.
6. Optionally, enter internal review notes in the **Comment (optional)** field.
7. Click **Submit Review**. The **Electronic Signature** dialog opens.
8. Verify your name, role, and the displayed confirmation statement.
9. Enter your account password in the **Password** field.
10. Click **Sign**. Upon successful signature verification, the dialogs close and the section status advances.

#### System behavior
- The system executes verification checks in this exact sequence:
  1. Verifies that the sample exists.
  2. Resolves the laboratory section and confirms user section access.
  3. Verifies segregation of duties across all current test orders in the section.
  4. Verifies that every current test order in the section is in the **Ready** step.
  5. Verifies the entered password.
- All database updates are committed in a single atomic transaction:
  - An electronic signature record is created with meaning `Reviewed`, capturing user ID, full name, username, role snapshot, timestamp, IP address, and any optional comment.
  - A Workflow History event of type `ReviewCompleted` is recorded with the section name and comment.
  - The laboratory section sign-off status advances from **Under review** to **Under approval**, recording the reviewer name, timestamp, and signature reference.
  - The parent sample records `ReviewedBy` and `ReviewedAt`.
  - Every current test order in the section is updated to status `Reviewed` and workflow step `Reviewed`, with a history entry: "Sample review completed by {name}".
- If the signature or any validation check fails, no changes are committed to the database, the password field is cleared, and an error message is displayed.

#### Validation
- If the sample record cannot be found: "Sample {id} not found."
- If the section is not in the review stage: "Sample must be under review before it can be reviewed."
- If the user performed any testing on the section: "You cannot review a sample you tested."
- If any test order in the section is incomplete: "Cannot complete review: one or more tests are not in Ready step."
- If the selected section has no tests on the sample: "This sample has no tests in the selected laboratory section."
- If the user is not assigned to the section: "This test belongs to a laboratory section you are not assigned to."
- If multiple sections are waiting and none is selected: "Choose a laboratory section - more than one section of this sample is waiting."
- If password authentication fails: "Password verification failed. The signature was not applied."
- If 5 incorrect signature passwords are submitted within 15 minutes: "Too many failed signature attempts. Wait 15 minutes, then try again. The signature was not applied."
- Generic signature failure fallback: "Signature failed."

#### Status
- Section sign-off status: advances from **Under review** to **Under approval**.
- Test order status: advances to `Reviewed` (workflow step `Reviewed`).
- Sample status: recomputed across all sections. If no section remains in testing or under review, the sample status becomes **Under approval** (displaying the **Under Approval** badge). If another section remains in testing or review, the overall sample reflects the least advanced section status.

#### Errors
- If the sample summary fails to load: "Failed to load sample summary."
- If exporting the summary PDF fails: "Export failed."

#### What happens next
The laboratory section moves to the approval queue for Section Head release authorization (see §19 Approval). If an issue is identified during review that requires recount or re-entry by the analyst, the reviewer can return count tests before completing the review (see §18 Return to Analyst, Rework and Retest).
