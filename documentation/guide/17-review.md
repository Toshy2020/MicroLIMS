## 17. Review

### 17.1 Automatic Submission for Review
#### Purpose
Move a laboratory section from testing to review automatically as soon as all of its current tests have reached the **Ready** step.

#### Who can use it
This is an automatic system step. No user action, password or electronic signature is involved.

#### Before you start
All current (non-superseded) tests of the laboratory section must reach the **Ready** step. The sample must be in **Received** or **In Testing** status, and the section must be in **In testing** status with at least one current test.

#### System behavior
- When the last test of a laboratory section reaches the **Ready** step, the section moves from **In testing** to **Under review**.
- The system records an event in the sample Workflow History with the text "All tests completed - automatically submitted for review". No electronic signature is recorded for this automatic step.
- The dashboard counts the sample for users whose role is, or is based on, Reviewer, Section Head or System Administrator (the count follows the role type, not the permission): "{n} sample(s) awaiting review".

> **Note for reviewers:** Dashboard review notifications count samples awaiting review rather than individual test orders as described in earlier specifications. (GAP-309)

#### Status
- The laboratory section changes from **In testing** to **Under review**.
- The sample status is recomputed from all sections: if no other section is still in testing, the sample shows the **Under Review** lifecycle badge (the sample register shows these as Pending Review / Pending Approval; see §11.1).

#### Important rules
- Automatic submission happens separately for each laboratory section of a sample tested by more than one laboratory. One section can enter review while another continues testing.


### 17.2 Technical Review of a Laboratory Section
<!-- WF-REVIEW-001 -->
#### Purpose
Check the recorded test results of a laboratory section and apply an electronic signature confirming that the technical review is complete.

#### Who can use it
Requires the *Sample Technical Review* permission (by default: Reviewer, Section Head, System Administrator). You must be assigned to the laboratory section being reviewed (users with unrestricted access are not limited to assigned sections).

#### Before you start
- The laboratory section must be in **Under review** status. Otherwise the system shows: "Sample must be under review before it can be reviewed."
- Every current test in the section must be at the **Ready** step. Otherwise the system shows: "Cannot complete review: one or more tests are not in Ready step."
- Segregation of duties: you must not have performed any current test of the section. This applies if you are the assigned analyst, the person who prepared the sample, or you entered any result, count reading, analysis, pathogen observation, workflow step result or confirmatory plate observation on a current test of that section. The system then shows: "You cannot review a sample you tested." No role is exempt, including System Administrator.

> **Note for reviewers:** Review and approval are decided per laboratory section, not per test order; Quick Table review and per-test-order review described in the design specifications are not available. (GAP-017, GAP-018)

> **Note for reviewers:** Reviewer separation of duties is checked per laboratory section only; a reviewer who tested a different laboratory section of the same sample is not blocked. (GAP-312)

#### What you see
- On the testing workspace, the sample shows the **Under Review** lifecycle badge. It is clickable only for users holding the *Sample Technical Review* permission. The **Sample Summary** button opens the same dialog.
- **Sample Summary** dialog:
  - Title **Sample Summary**.
  - **Sections:** followed by one chip per laboratory section (for example "Section A: Under review", "Section B: In testing").
  - Header buttons: **Open Pathogen Workflow** (only if a test uses a shared TSB), **Printable Report**, **View COA** (only when certificate results exist and a certificate is available), **Export PDF**.
  - The **Workflow History** strip (empty text: "No lifecycle events yet.").
  - The test results of the sample.
  - Card **Approval & Electronic Signatures**, showing "Awaiting technical review by authorized reviewer." and the form **Submit Technical Review**:
    - **Laboratory Section** (shown only when more than one section is reviewable).
    - The alert "By submitting, I confirm I have reviewed all test results for {section}." (or "... for this sample.").
    - **Comment (optional)**. This internal comment is never printed on the Certificate of Analysis.
    - Button **Submit Review** (disabled while more than one section is reviewable and none is chosen).
  - Footer: "Sample Reference: {ref} · Record Status: {status} · MicroLIMS Operational View".
- **Electronic Signature** dialog: your full name (or username) and role, the confirmation sentence, the field **Password** (Enter submits), and the buttons **Cancel** and **Sign** (shows "Signing..." while working; disabled while the password is empty).

[SCREENSHOT SS-171]

[SCREENSHOT SS-172]

#### How to use it
1. On the testing workspace, click the **Under Review** badge (or **Sample Summary**) of the sample.
2. In the **Sample Summary** dialog, read the results of all tests in the section.
3. Check the **Workflow History** strip.
4. Go to the **Approval & Electronic Signatures** card.
5. If more than one section is waiting, select your section in **Laboratory Section**.
6. Optionally, type an internal note in **Comment (optional)**.
7. Click **Submit Review**. The **Electronic Signature** dialog opens.
8. Check your name, role and the confirmation sentence.
9. Type your account password in **Password**.
10. Click **Sign**. When the signature is accepted, the dialogs close and the section moves on.

#### System behavior
- The system checks, in this order: the sample exists; the laboratory section and your access to it; segregation of duties for every current test in the section; every current test is at the **Ready** step; your password.
- When all checks pass, everything is saved together:
  - An electronic signature with the meaning *Reviewed*, recording your name, username, role, time and any comment.
  - A Workflow History event for the completed review, with the section and your comment.
  - The laboratory section moves from **Under review** to **Under approval**, recording the reviewer and the time.
  - The sample records who reviewed it and when.
  - Every current test in the section is marked **Reviewed**, with the history entry "Sample review completed by {name}".
- If the signature or any check fails, nothing is saved, the password field is cleared, the message is shown in the dialog and the dialog stays open.

#### Validation
- "Sample {id} not found."
- "Sample must be under review before it can be reviewed."
- "You cannot review a sample you tested."
- "Cannot complete review: one or more tests are not in Ready step."
- "This sample has no tests in the selected laboratory section."
- "This test belongs to a laboratory section you are not assigned to."
- "Choose a laboratory section - more than one section of this sample is waiting."
- "Password verification failed. The signature was not applied."
- "Too many failed signature attempts. Wait 15 minutes, then try again. The signature was not applied." (after 5 failed attempts within 15 minutes)
- "Signature failed." (general fallback)

#### Status
- Laboratory section: **Under review** to **Under approval**.
- Tests: each current test in the section becomes **Reviewed**.
- Sample: recomputed from all sections. If no section is still in testing or under review, the sample becomes **Under approval** (badge **Under Approval**). If another section is still open, the sample shows the least advanced open status.

#### Errors
- "Failed to load sample summary."
- "Export failed."

#### What happens next
The section waits for the Section Head's release decision (see §19 Approval). If a count test needs a recount or re-entry, return it to the analyst before you sign the review (see §18 Return to Analyst, Rework and Retest).

<!-- Review log (Sonnet): 14 corrections; invented items removed: "active membership" wording, SysAdmin-only unrestricted access, raw readings/incubation/analyst-comment detail in the summary, enum/record/database/transaction wording, test order status code; callouts added: none missing (GAP-017/018, GAP-312, GAP-309 present) -->
<!-- Phase 13 (agy): applied F-B05, F-B11 -->

<!-- Phase 13 review (Sonnet): F-B05, F-B11 verified; no invented items. -->
