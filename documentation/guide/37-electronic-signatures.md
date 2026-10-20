## 37. Electronic Signatures and Approval Controls

### 37.1 How Electronic Signatures Work

#### Purpose
For important laboratory decisions (reviews, approvals, release decisions, result recording and some master data changes) MicroLIMS asks the user to confirm the action by typing their own sign-in password again.

#### How a signature is applied
- **Password re-entry**: you re-enter your current sign-in password. If the account is inactive or locked, or the password is wrong, the signature is refused with the same message.
- **Together with the action**: the signature is saved at the same time as the action. If the password check fails, the action is not carried out and nothing changes.
- **Signature record**: each signature is stored with your user, your full name, username and role at the time of signing, the meaning of the signature, the record it applies to, the time, your comment (if any) and your IP address. Signature records cannot be changed.

#### Failed attempts
- After 5 failed signature attempts within 15 minutes, further attempts are refused until the time has passed:
  > "Too many failed signature attempts. Wait 15 minutes, then try again. The signature was not applied."
- Failed signatures do not lock your sign-in account.
- Every failed or refused signature attempt is written to the audit trail, without the password.

> **Note for reviewers:** The meaning and comment (reason) requirements differ from action to action; whether they should be uniform is not confirmed. (GAP-027)

> **Note for reviewers:** There is no screen that lists signatures; signatures of a sample appear in its traceability chain (see §36). (GAP-030)

---

### 37.2 Executing an Electronic Signature

<!-- WF-SIG-001 -->
#### Purpose
Apply an electronic signature to authorize a laboratory decision, complete a preparation, or record analytical results.

#### Who can use it
Any authorized user performing a signature-required action in accordance with their assigned role and permissions.

#### Before you start
- Complete the operational workflow steps (e.g., enter all replicate values, perform review verifications, or review preparation parameters).
- Ensure segregation-of-duties rules are met (the signer must not have performed earlier contradictory steps).

#### What you see
[SCREENSHOT SS-371]
The **Electronic Signature** dialog opens in a forced light theme, displaying:
- Title: **Electronic Signature**
- Signer identification: Read-only display of the user's **Full Name** (or username) and assigned **Role**
- Meaning Statement: A clear, contextual declaration of the signature's intent (e.g., *"I confirm the preparation steps shown above are the steps performed for this sample."*)
- Optional field: **Comment (optional)** (multiline text box, displayed when enabled by the calling action)
- Mandatory field: **Password** (masked password input with autofocus)
- Action buttons: **Cancel** and **Sign** (displays **Signing...** during processing; button remains disabled until a password is typed)

#### How to use it
1. Trigger the desired action (e.g., click **Approve**, **Sign and record result**, or **Complete Preparation**).
2. Review the displayed **Meaning Statement** to verify the scope of authorization.
3. If applicable, enter an explanatory justification in the **Comment (optional)** field.
4. Type your current sign-in password into the **Password** field.
5. Click **Sign** (or press Enter).

#### System behavior
- The system checks the password against your own account.
- When the password is correct, the signature is recorded together with the action itself; if the password is wrong, the action is not carried out.
- If verification fails, the dialog remains open, an error message appears, and the **Password** field is cleared.

#### Validation
- The **Sign** button is disabled until at least one character is entered in the **Password** field.
- If password verification fails: "Password verification failed. The signature was not applied." (if no message comes back from the system, the dialog shows "Signature failed.").
- If throttled: "Too many failed signature attempts. Wait 15 minutes, then try again. The signature was not applied."

#### Status
- A successful signature moves the record to its next status in the workflow of the screen you are using (for example from under review to under approval); the statuses are described in the section for that screen.

#### Important rules
- Signatures cannot be revoked, edited, or detached once applied.
- The dialog always signs as the user who is signed in; the password required is that user's own.

#### Errors
- Submitting an invalid password triggers an error alert inside the dialog without closing it.

#### What happens next
The dialog closes, a success notification appears, and the workspace refreshes to reflect the updated status.

---

### 37.3 Signature-Required versus Non-Signed Actions

The following lists show which laboratory actions require an electronic signature and which do not:

#### The 26 Signature-Required Actions
The system asks for password re-authentication for these 26 actions:
1. **Sample Technical Review**: Secondary verification of sample testing.
2. **Sample Approval Decisions**: Final authorization for Approve, Reject, New sample request, or Retest retained sample.
3. **Section Testing Closure**: Administrative section testing closure.
4. **Add Laboratory to Sample**: Extending testing to an additional section.
5. **Sample Detail Correction**: Modifying logged sample metadata with required reason.
6. **Sample Void**: Voiding a sample and all associated test orders.
7. **Sample Preparation Confirmation**: Confirming diluents and sample preparation.
8. **Cryovial Batch Release / Rejection**: Authorizing reference cryovials.
9. **Media Lot Release / Rejection**: Authorizing prepared media lots.
10. **Media Product Code Modification**: Modifying media master catalog codes.
11. **Solution Preparation Completion**: Completing volumetric solutions or mobile phases.
12. **Titrant Standardization**: Recording volumetric titrant standardization factors.
13. **Titrant-Due Acknowledgement**: Authorizing use of a titrant requiring restandardization.
14. **HPLC System Suitability Confirmation**: Verifying HPLC chromatographic suitability.
15. **ICP Calibration Confirmation**: Verifying spectroscopic calibration curves.
16. **Working Standard Replicate Submission**: Submitting qualification assay replicates.
17. **Working Standard Technical Review**: Verifying working standard qualification data.
18. **Working Standard Approval**: Authorizing an in-house working standard lot.
19. **Working Standard Rejection**: Rejecting a working standard qualification.
20. **KPI Workflow Profile Sign-off**: Signing workflow profile versions or drafts.
21. **Master Data Workbook Import & Verify**: Ingesting master data and second-person verification .
22. **Document Revision Approval**: Authorizing controlled document revisions.
23. **Physicochemical Result Recording**: Recording analytical measurement, qualitative, titration, HPLC, or ICP results.
24. **Dissolution Result Recording**: Recording dissolution stage vessel areas.
25. **Disintegration Result Recording**: Recording disintegration stage times.
26. **Weight Variation Result Recording**: Recording tablet or capsule weights.

#### Actions That Do Not Ask for a Signature
The following do not ask for a signature password (verified in the code for the first group; messages and discussions are not signed either):
- **Microbiology plate count entry**.
- **Pathogen testing steps**.
- **Biochemical decision**.
- **Return to analyst**.
- **Incubator changes**.
- **User administration** (create, edit, change role, disable, unlock, reset password, delete).
- **Role administration** (create, edit, change permissions, delete).
- **Messages and discussions**.

---

### 37.4 Segregation of Duties Enforcement

#### Purpose
MicroLIMS enforces segregation of duties. The person who performs or prepares a test cannot act as its reviewer or final approver.

#### Scope of "Performed a Test"
Under system segregation rules, a user is considered to have performed a test if they:
- Are the assigned analyst on the test order
- Confirmed the sample preparation record
- Entered any analytical result, plate count reading, or test analysis
- Recorded any pathogen workflow observation
- Submitted a workflow step result or confirmatory plate observation

#### Enforced Segregation Rules and Exact Refusal Messages
The system compares user accounts and refuses the action with these messages:

| Operational Area | Segregation Rule | System Refusal Message |
|---|---|---|
| **Sample Technical Review** | A user who performed any testing on a sample section cannot review it | "You cannot review a sample you tested." |
| **Sample Approval** | A user who reviewed a sample section cannot approve it | "You cannot approve a sample you reviewed." |
| **Sample Approval** | A user who performed any testing on a sample section cannot approve it | "You cannot approve a sample you tested." |
| **Pathogen Review Decision** | A reviewer cannot evaluate a pathogen result they observed or performed | "A reviewer cannot decide on a result they performed." |
| **Media Lot Release** | A user who prepared a media lot or read any challenge evaluation cannot approve it | "You cannot approve a media lot you prepared or evaluated." |
| **Cryovial Batch Approval** | A user who prepared a reference cryovial batch cannot approve it | "You cannot approve a cryovial batch you prepared." |
| **Working Standard Qualification** | Submitting analyst cannot review the qualification | "The analyst who submitted the qualification cannot review it." |
| **Working Standard Qualification** | Submitting analyst cannot approve the qualification | "The analyst who submitted the qualification cannot approve it." |
| **Working Standard Qualification** | Reviewer cannot approve the qualification | "The reviewer cannot also approve the qualification." |
| **Working Standard Qualification** | Reviewer cannot reject the qualification at approval | "The reviewer cannot also reject the qualification at approval." |
| **Master Data Workbook** | The person who loaded an import workbook cannot verify it | "The person who loaded the workbook cannot verify it. Ask a second person." |
| **Document Revision Approver Assignment** | Author or technical reviewer cannot be assigned as approver | "Segregation of Duties Violation: The author of a document revision cannot be assigned as its approver." / "Segregation of Duties Violation: A technical reviewer for this revision cannot be assigned as its approver." |
| **Document Revision Approval Decision** | Author or technical reviewer cannot execute the approval decision | "Segregation of Duties Violation: The author of a document revision cannot approve or decide upon their own revision." / "Segregation of Duties Violation: A technical reviewer for this revision cannot approve or decide upon this revision." |
| **Document Periodic Review** | Author of a document cannot conduct its periodic review | "Segregation of Duties Violation: The author cannot perform or complete the periodic review." |

#### Absolute Rule: No Administrative Exemption
The segregation-of-duties guard is role-agnostic and compares user accounts only. **System Administrators possess no administrative exemption**:
- A System Administrator who prepares a sample, enters test counts, standardizes a titrant, or prepares a media lot is strictly blocked from reviewing or approving that record.
- Every analytical review and release decision requires two distinct authorized individuals.


> **Note for reviewers:** Microbiology plate count entry and pathogen steps are not signed, and returns to analyst are not signed. (GAP-009, GAP-010, GAP-028)

> **Note for reviewers:** Role and user changes are audited but not signed and need no second person; nothing prevents removing the *Manage roles* permission from an administrative role. (GAP-009)

<!-- Review log (Sonnet): 18 corrections; invented items removed: "cryptographic" wording, enum/meaning names, class and field names, Autoclave Program Management row, transaction/hash wording, audit claims for unsigned actions; callouts added: GAP-027, GAP-030, GAP-009, GAP-010, GAP-028. Signed list (26) matches verified source; not-signed list limited to verified items. -->
