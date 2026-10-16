## 37. Electronic Signatures and Approval Controls

### 37.1 Electronic Signature Authentication and Mechanics

#### Purpose
MicroLIMS requires cryptographic electronic re-authentication for critical laboratory authorizations, analytical result recordings, master data modifications, and release decisions to ensure individual accountability.

#### Authentication Mechanism
- **Re-Authentication by Password**: Applying an electronic signature requires the user to re-enter their current account sign-in password. The system verifies the submitted password against the stored secure cryptographic hash.
- **Atomic Execution**: Electronic signatures are executed within the exact same database transaction as the underlying business action. If password verification fails, the business record is not altered, no status transitions occur, and nothing is persisted.
- **Append-Only Signature Record**: Each successful signature writes an immutable record capturing:
  - User identifier
  - Snapshot of the signer's full name, username, and role type at the moment of signing
  - Signed meaning statement (`MeaningOfSignature`)
  - Target entity type and entity record identifier
  - UTC timestamp of signature application
  - User-entered comment (when provided)
  - Client IP address

#### Throttling and Security Safeguards
- **Failure Throttle**: If a user enters five incorrect signature passwords within a 15-minute window, the system automatically throttles subsequent signature attempts for 15 minutes:
  > "Too many failed signature attempts. Wait 15 minutes, then try again. The signature was not applied."
- **Account Lock Independence**: Signature throttling is completely independent of login authentication. Exceeding failed signature attempts **does not lock** the user's primary login account.
- **Audit Logging of Failures**: Every failed or throttled signature attempt is recorded in the audit trail (under entity `ElectronicSignature` with action `SignatureFailed` or `SignatureThrottled`), logging the attempted meaning, entity type, and entity identifier without storing the entered password.

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
The **Electronic Signature** dialog (`SCR-SIG-001`) opens in a forced light theme, displaying:
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
- The server verifies the password against the user's account hash.
- Upon successful verification, the electronic signature entity is created, the record state transitions, and the transaction commits atomically.
- If verification fails, the dialog remains open, an error message appears, and the **Password** field is cleared.

#### Validation
- The **Sign** button is disabled until at least one character is entered in the **Password** field.
- If password verification fails: `"Password verification failed. The signature was not applied."` (client fallback: `"Signature failed."`).
- If throttled: `"Too many failed signature attempts. Wait 15 minutes, then try again. The signature was not applied."`

#### Status
- Transitions the target record to its next lifecycle status (e.g., `UnderReview` -> `UnderApproval`, `UnderApproval` -> `Approved`).

#### Important rules
- Signatures cannot be revoked, edited, or detached once applied.
- The signer must be actively signed into the session; signing with another user's credentials while signed in is impossible because the system compares credentials against the authenticated session user.

#### Errors
- Submitting an invalid password triggers an error alert inside the dialog without closing it.

#### What happens next
The dialog closes, a success notification appears, and the workspace refreshes to reflect the updated status.

---

### 37.3 Signature-Required versus Non-Signed Actions

MicroLIMS strictly delineates which laboratory transactions require an electronic signature and which do not:

#### The 26 Signature-Required Actions
The system enforces electronic signature re-authentication for exactly 26 distinct operational actions:
1. **Sample Technical Review**: Secondary verification of sample testing (meaning: `Reviewed`).
2. **Sample Approval Decisions**: Final authorization for `Approve`, `Reject`, `New sample request`, or `Retest retained sample` (meanings: `Approved`, `Rejected`, `RetestRequested`).
3. **Section Testing Closure**: Administrative section testing closure (meaning: `TestingClosed`).
4. **Add Laboratory to Sample**: Extending testing to an additional section (meaning: `LaboratoryAdded`).
5. **Sample Detail Correction**: Modifying logged sample metadata with required reason (meaning: `SampleCorrected`).
6. **Sample Void**: Voiding a sample and all associated test orders (meaning: `SampleVoided`).
7. **Sample Preparation Confirmation**: Confirming diluents and sample preparation (meaning: `PreparationConfirmed`).
8. **Cryovial Batch Release / Rejection**: Authorizing reference cryovials (meanings: `Approved`, `Rejected`).
9. **Media Lot Release / Rejection**: Authorizing prepared media lots (meanings: `Approved`, `Rejected`).
10. **Media Product Code Modification**: Modifying media master catalog codes (meaning: `MasterDataChanged`).
11. **Solution Preparation Completion**: Completing volumetric solutions or mobile phases (meaning: `PreparationConfirmed`).
12. **Titrant Standardization**: Recording volumetric titrant standardization factors (meaning: `TitrantStandardized`).
13. **Titrant-Due Acknowledgement**: Authorizing use of a titrant requiring restandardization (meaning: `TitrantDueAcknowledged`).
14. **HPLC System Suitability Confirmation**: Verifying HPLC chromatographic suitability (meaning: `SuitabilityRunPerformed`).
15. **ICP Calibration Confirmation**: Verifying spectroscopic calibration curves (meaning: `SuitabilityRunPerformed`).
16. **Working Standard Replicate Submission**: Submitting qualification assay replicates (meaning: `ResultRecorded`).
17. **Working Standard Technical Review**: Verifying working standard qualification data (meaning: `Reviewed`).
18. **Working Standard Approval**: Authorizing an in-house working standard lot (meaning: `Approved`).
19. **Working Standard Rejection**: Rejecting a working standard qualification (meaning: `Rejected`).
20. **KPI Workflow Profile Sign-off**: Signing workflow profile versions or drafts (meaning: `MasterDataChanged`).
21. **Master Data Workbook Import & Verify**: Ingesting master data (`MasterDataChanged`) and second-person verification (`Reviewed`).
22. **Document Revision Approval**: Authorizing controlled document revisions (meaning: `Approved`).
23. **Physicochemical Result Recording**: Recording analytical measurement, qualitative, titration, HPLC, or ICP results (meaning: `ResultRecorded`).
24. **Dissolution Result Recording**: Recording dissolution stage vessel areas (meaning: `ResultRecorded`).
25. **Disintegration Result Recording**: Recording disintegration stage times (meaning: `ResultRecorded`).
26. **Weight Variation Result Recording**: Recording tablet or capsule weights (meaning: `ResultRecorded`).

#### Actions That Do Not Ask for a Signature
The following transactions are captured in the system audit trail with user identity and timestamp, but **do not require an electronic signature password**:
- **Microbiology Plate Count Entry**: Entering raw plate counts, dilution factors, or reading notes.
- **Pathogen Testing Workflow**: Recording presumptive observations, streak plate selections, and confirmatory setups.
- **Biochemical Confirmation Decisions**: Submitting biochemical identification results.
- **Return Test to Analyst**: Returning a test order for analytical revision.
- **Incubator & Equipment Adjustments**: Changing incubator temperature setpoints or modifying operational status.
- **Autoclave Program Management**: Creating or updating autoclave cycles.
- **User Administration**: Creating accounts, editing profiles, modifying roles, disabling accounts, unlocking accounts, resetting passwords, or executing permanent deletions.
- **Role Administration**: Creating custom roles, editing role descriptions, modifying permission checklists, or deleting custom roles.
- **Messages & Discussions**: Posting discussion threads, adding comments, uploading attachments, or sending direct/group messages.

---

### 37.4 Segregation of Duties Enforcement

#### Purpose
To safeguard data integrity, MicroLIMS enforces strict segregation of duties (SoD). The person who performs or prepares a test cannot act as its reviewer or final approver.

#### Scope of "Performed a Test"
Under system segregation rules, a user is considered to have performed a test if they:
- Are the assigned analyst (`AssignedAnalystId`) on the test order
- Confirmed the sample preparation record (`SamplePreparation`)
- Entered any analytical result, plate count reading, or test analysis
- Recorded any pathogen workflow observation
- Submitted a workflow step result or confirmatory plate observation

#### Enforced Segregation Rules and Exact Refusal Messages
The segregation guard evaluates acting user identifiers and blocks non-compliant attempts with exact refusal messages:

| Operational Area | Segregation Rule | System Refusal Message |
|---|---|---|
| **Sample Technical Review** | A user who performed any testing on a sample section cannot review it | `"You cannot review a sample you tested."` |
| **Sample Approval** | A user who reviewed a sample section cannot approve it | `"You cannot approve a sample you reviewed."` |
| **Sample Approval** | A user who performed any testing on a sample section cannot approve it | `"You cannot approve a sample you tested."` |
| **Pathogen Review Decision** | A reviewer cannot evaluate a pathogen result they observed or performed | `"A reviewer cannot decide on a result they performed."` |
| **Media Lot Release** | A user who prepared a media lot or read any challenge evaluation cannot approve it | `"You cannot approve a media lot you prepared or evaluated."` |
| **Cryovial Batch Approval** | A user who prepared a reference cryovial batch cannot approve it | `"You cannot approve a cryovial batch you prepared."` |
| **Working Standard Qualification** | Submitting analyst cannot review the qualification | `"The analyst who submitted the qualification cannot review it."` |
| **Working Standard Qualification** | Submitting analyst cannot approve the qualification | `"The analyst who submitted the qualification cannot approve it."` |
| **Working Standard Qualification** | Reviewer cannot approve the qualification | `"The reviewer cannot also approve the qualification."` |
| **Working Standard Qualification** | Reviewer cannot reject the qualification at approval | `"The reviewer cannot also reject the qualification at approval."` |
| **Master Data Workbook** | The person who loaded an import workbook cannot verify it | `"The person who loaded the workbook cannot verify it. Ask a second person."` |
| **Document Revision Approver Assignment** | Author or technical reviewer cannot be assigned as approver | `"Segregation of Duties Violation: The author of a document revision cannot be assigned as its approver."` / `"Segregation of Duties Violation: A technical reviewer for this revision cannot be assigned as its approver."` |
| **Document Revision Approval Decision** | Author or technical reviewer cannot execute the approval decision | `"Segregation of Duties Violation: The author of a document revision cannot approve or decide upon their own revision."` / `"Segregation of Duties Violation: A technical reviewer for this revision cannot approve or decide upon this revision."` |
| **Document Periodic Review** | Author of a document cannot conduct its periodic review | `"Segregation of Duties Violation: The author cannot perform or complete the periodic review."` |

#### Absolute Rule: No Administrative Exemption
The segregation-of-duties guard is role-agnostic and evaluates raw user account identifiers. **System Administrators possess no administrative exemption**:
- A System Administrator who prepares a sample, enters test counts, standardizes a titrant, or prepares a media lot is strictly blocked from reviewing or approving that record.
- Every analytical review and release decision requires two distinct authorized individuals.

> **Note for reviewers:** Role and user changes are audited but not signed and need no second person; nothing prevents removing Roles.Manage from an administrative role. (GAP-009)
