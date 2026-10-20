## 38. Document Control

### 38.1 Document Control Dashboard

#### Purpose
Provide a consolidated executive and operational overview of controlled document governance, active catalog volumes, lifecycle stages, overdue review obligations, and recent audit events.

#### Who can use it
Open to all signed-in users. However, because the page queries both document masters and recent audit trail events simultaneously, and global audit queries are restricted to Reviewer, Section Head, and System Administrator role types, users with other roles may receive an error alert instead of metric figures.

#### What you see
[SCREENSHOT SS-381]
The **Document Control Dashboard** presents:
- Header title **Document Control Dashboard** with subtitle "Document governance overview, lifecycle metrics, and quality records status".
- Navigation buttons in the header: **My Reading List**, **Document Library**, and **Register Document** (visible only to users holding the *Document Master Registration* permission; by default: System Administrator, Section Head, Analyst).
- Six summary metric cards:
  1. **Total Documents**: count of all documents registered in the system catalog, including voided records, with caption "In system catalog".
  2. **Effective Records**: count of active document masters with an active, effective revision, with caption "Active & released".
  3. **Review Overdue**: count of active effective documents whose periodic review date is in the past, with caption "Action Required" when greater than zero, otherwise "All current".
  4. **Draft Revisions**: count of active documents currently in draft status, with caption "Pending publication".
  5. **File Pending**: count of active documents lacking a controlled PDF, with caption "No PDF attached".
  6. **Voided Masters**: count of retired records with record status Void, with caption "Retired records".
- **Recently Registered Documents** panel:
  - Table displaying up to 5 recently registered active documents with columns: **Company Code**, **Title**, **Type**, **Status**, **Files** (chip **PDF** or text "None"), and **Date** (creation date).
  - Empty state displays: "No documents registered yet."
  - Header button: **View Full Library** (navigates to the Document Library).
- **Recent Audit Trail Events** panel:
  - Lists up to 5 recent audit entries displaying action code, event time, actor identification ("By: {user or System}"), category chip, and reason description.
  - Empty state displays: "No recent audit events."
  - Header button: **All Events** (navigates to the Document Control Audit Trail).

> **Note for reviewers:** The six counts cover only the first 100 documents in the catalog. The page also asks for recent audit trail events in the same request, so users whose role type cannot query the audit trail (for example Analysts) may see an error message instead of the figures; this was read from the code and has not been observed in the running application. (GAP-811, GAP-812)

---

### 38.2 Document Library

#### Purpose
Serve as the authoritative catalog and search index for all controlled documents, Standard Operating Procedures (SOPs), test methods, policies, and specifications across the organization.

#### Who can use it
Open to all signed-in users. Viewing voided document masters is restricted by the server to users whose role type is System Administrator or Section Head.

#### What you see
[SCREENSHOT SS-382]
The **Document Library** page presents:
- Header title **Document Library** with subtitle "Authoritative repository of controlled documents, Standard Operating Procedures, and quality records".
- Header button: **Register New Document** (visible only to users holding the *Document Master Registration* permission).
- Filter bar:
  - **Search** box (placeholder "Search code, title, or keywords...")
  - **Document Type** dropdown ("All Types", followed by active types formatted as `{code}: {name}`)
  - **Department** dropdown ("All Departments", followed by active departments formatted as `{code}: {name}`)
  - **Status** dropdown ("All Statuses", "Draft", "In Review", "Awaiting Approval", "Future Effective", "Effective", "Superseded", "Obsolete", "Cancelled")
  - Toggle switches: **Show Voided** and **Review Overdue Only**
  - Button: **Reset** (disabled when no filters are applied)
- Document catalog table (11 columns):
  1. **Company Code**: company-assigned document identifier (displays **VOID** badge if voided)
  2. **MicroLIMS ID**: permanent system sequence identifier (such as `DOC-0000001`)
  3. **Title**: official document title
  4. **Type**: document type code
  5. **Dept / Section**: department and section assignment
  6. **Owner**: assigned document owner
  7. **Rev**: current revision number
  8. **Status**: status chip (displays **REVIEW OVERDUE** chip when periodic review is past due)
  9. **Files**: icons showing source Word attachment (tooltip "Source File Attached (.docx)") and controlled PDF attachment
  10. **Effective Date**: date revision became effective (reads "Pending" if not yet effective)
  11. **Actions**: row action icons
- Row action icons:
  - **View Controlled PDF** (opens the controlled PDF in the viewer)
  - **View Document Details** (opens the document detail workspace)
  - **Void Document Master** (red icon, visible only if user's role type is Section Head, the document is Active, and it has no current effective revision)
- Pagination controls: rows per page choices (5, 10, 25, 50).
- Empty state displays: "No controlled documents match the selected filters".

#### How to use it
1. Navigate to **Document Control** and select **Document Library**.
2. Enter search keywords, select a **Document Type** or **Department**, or filter by **Status**.
3. To view only documents requiring periodic re-evaluation, enable the **Review Overdue Only** switch.
4. Click **View Document Details** to open the governance and workflow workspace for any record.
5. Click **View Controlled PDF** to read the released version of a document.

#### Validation
- If master data lists fail to load, the page displays: "The document types and departments could not be loaded, so those filters are empty. Reload the page to try again."

> **Note for reviewers:** Whether voided document masters appear in the library list for every user when **Show Voided** is switched on has not been confirmed; opening a voided master is allowed only for System Administrators and Section Heads.

---

### 38.3 Document Detail and Governance Workspace

#### Purpose
Manage metadata, file attachments, revision lifecycles, technical reviews, approval ceremonies, periodic evaluations, and audit histories for an individual controlled document master.

#### Who can use it
Open to all signed-in users. Opening a voided document master requires the System Administrator or Section Head role type. Editing draft metadata, uploading files, creating revisions, or routing for review requires the System Administrator or Section Head role type, or being the assigned document owner or Author.

#### What you see
[SCREENSHOT SS-383]
The **Document Detail** page presents:
- Header card:
  - Navigation button: **Document Library** (returns to catalog)
  - Document identifiers: Company Document Code, document type chip, current status badge (displays "Void" for voided masters, otherwise current revision status), and chip **MIGRATED RECORD** for imported legacy records.
  - Document Title, with captions: "MicroLIMS ID", "Department: {name} ({section})", "Current Revision", and "Owner".
- Lifecycle banner:
  - For revisions in future status: "Revision {n} Scheduled for Automatic Activation (Future Effective)".
  - For voided records: "RECORD VOIDED:" accompanied by "Mandatory Justification:".
- Primary action area:
  - The page displays one prominent button representing the next lifecycle stage:
    - **Upload controlled file**: displayed when the revision is Draft and lacks an active controlled PDF.
    - **Submit for technical review**: displayed when the revision is Draft and has an active controlled PDF.
    - **Open technical review ({status})**: displayed when a technical review task exists; prominent while the revision is In Review.
    - **Assign approver**: displayed when the revision is Awaiting Approval and no approval task is assigned.
    - **Open approval workspace ({status})**: displayed when the revision is Awaiting Approval and an approval task exists.
    - **Create revision**: displayed when the master is Active, has an effective revision, no revision is in Draft, In Review or Awaiting Approval, and you are a System Administrator, Section Head, the document owner or an assigned Author.
  - Menu button **More actions** containing contextual actions:
    - **Edit metadata** (editable while in draft)
    - **Replace controlled file** (available in draft when an active PDF exists)
    - **Change items** (itemized change log)
    - **Impact assessment** (multi-category evaluation)
    - **Cancel draft revision** (red text; cancels the active draft)
    - **Void document master** (red text; Section Head only, master Active, no effective revision)
  - Contextual status messages when no action is available to the user:
    - "Waiting on the assigned approver. Only they can record the approval decision."
    - "Waiting for an approver to be assigned. The document owner, an assigned author or the Document Controller can route it for approval."
    - "Waiting on the assigned technical reviewer."
    - "No actions are available to you on this document in its current state."
- Four main workspace tabs:
  1. **Document**:
     - **Document Governance Properties**: displays Document Classification, Confidentiality Level, Department & Section, Category, Review Cycle (in months), and Record Origin.
     - **Keywords & Search Tags**: list of search tags (displays "No keywords assigned." if empty).
     - **Life-Cycle Timestamps & Traceability**: displays Created By and Last Modified By with UTC timestamps.
     - **Controlled Files on Revision {n}**: button **Upload or Replace File** (when draft editable) and files table with columns **File Role**, **File Name**, **Size**, **SHA-256 Checksum**, **Status** (chips **ACTIVE** or **SUPERSEDED**), **Uploaded By**, **Date**, and **Action** (view and download icons). If empty, displays "No files currently uploaded for this revision."
  2. **Revisions ({n})**:
     - Table **Revision History Register**: columns **Revision**, **Seq**, **Type**, **Status**, **Effective Date**, **Next Review**, **Reason for Revision**, **Files**, and **Created By**.
  3. **Workflow ({n})**:
     - **Document Workflow Role Assignments**: button **Add Assignment**, columns **Role**, **Assigned Personnel**, **Username**, **Assigned At**, **Assigned By**, and **Actions** (remove icon). Displays primary owner as "Document Owner" / "Primary System Owner".
     - **Technical Review History & Tasks**: button **Submit for Review**, columns **Task ID**, **Revision**, **Assigned Reviewer**, **Status**, **Assigned Date**, **Due Date**, **Findings** (chips `{n} Total`, `{n} Open Mandatory`), and **Actions** (buttons **Dual PDF** and **Open Review**). Displays "No technical review tasks initiated for this revision." if none exist.
     - **Independent Periodic Review History**: columns **Task ID**, **Revision**, **Due Date**, **Status**, **Outcome**, **Assigned / Completed By**, **Findings**, and **Actions** (button **Workspace**, chip **OVERDUE**). Displays "No periodic review tasks recorded for this document master." if none exist.
  4. **Audit trail ({n})**:
     - Chronological event log: columns **Timestamp (UTC)**, **Action Code**, **Category**, **Actor**, **Reason / Justification**, and **Field Changes**. Displays "No audit events recorded for this record." if empty.

---

### 38.4 Registering a Controlled Document Master

<!-- WF-DC-001 -->
#### Purpose
Register a new controlled document master record in the catalog, allocate a permanent system sequence number, establish metadata ownership, and create initial Draft Revision 01.

#### Who can use it
Requires the *Document Master Registration* permission (by default: System Administrator, Section Head, Analyst).

#### Before you start
- Verify that active Document Types, Departments, and Sections are configured in system master data.
- Ensure the proposed Company Document Code is unique among all active documents.
- Know the Document Owner's user number (the dialog asks for a number, not a name).

#### What you see
[SCREENSHOT SS-384]
Clicking **Register Document** on the dashboard or **Register New Document** in the library opens the **Register New Document Master** dialog:
- Information banner explaining that the MicroLIMS Document ID "is automatically generated by the database sequence upon registration and cannot be modified. Initial status will be Draft".
- Mandatory entry fields:
  - **Company Document Code \*** (helper text: "Must be unique among active documents")
  - **Document Title \***
  - **Document Type \*** (dropdown of active types)
  - **Confidentiality \*** (dropdown: **Public**, **Internal**, **Restricted**, **Confidential**)
  - **Department \*** (dropdown of active departments)
  - **Section \*** (dropdown of active sections belonging to the selected department)
  - **Document Owner User ID \*** (numeric user identifier field, helper text: "Assigned primary owner")
- Optional fields:
  - **Initial Revision Number** (defaults to "01")
  - **Review Cycle (Months)** (inherits default cycle from Document Type, helper text: "Inherited from Document Type")
  - **Category**
  - **Keywords** (with button **Add Keyword**)
- Action buttons: **Cancel** and **Register Document Master** (displays "Registering..." during submission).

#### How to use it
1. Open the **Register New Document Master** dialog.
2. Enter the **Company Document Code** (for example, "SOP-QC-001") and **Document Title**.
3. Select the **Document Type**, **Department**, and **Section**.
4. Enter the **Document Owner User ID** number for the responsible personnel.
5. Optionally adjust the initial revision number, review cycle, confidentiality, or add search keywords.
6. Click **Register Document Master**.
7. Upon successful registration, the dialog closes and the system opens the Document Detail workspace for the new record.

#### System behavior
- The system allocates the next permanent sequence number and builds the MicroLIMS ID from the active numbering configuration (by default the prefix "DOC-" followed by a 7-digit zero-padded number, for example "DOC-0000001"). Once assigned, this ID is permanent and never reissued, even if the master is subsequently voided.
- The system creates the initial revision in status Draft, as a Major revision, with reason "Initial Document Registration", and initial revision number "01".
- The master, its first revision, any initial personnel assignments and keywords are saved together or not at all.
- The action is recorded in the audit trail (action code DocumentMasterRegistered).

#### Validation
- If mandatory fields are omitted in the client: "Company Document Code and Title are required." or "Please complete all mandatory selection fields."
- Server validation enforces the following exact error messages:
  - "Company Document Code is required."
  - "Document Title is required."
  - "A valid Document Type is required."
  - "A valid Department is required."
  - "A valid Section is required."
  - "A valid Document Owner is required."
  - "Section '{section}' does not belong to department '{department}'."
  - "A document with Company Document Code '{code}' already exists and is active."
  - "Only the Document Controller or a System Administrator can register a Document Master."

> **Note for reviewers:** In the registration dialog, the Document Owner is entered as a raw numeric user ID rather than selected from a user lookup dropdown. (GAP-821)

> **Note for reviewers:** The server refusal message states that only Document Controllers or System Administrators can register documents, even though the Analyst role holds the registration permission code by default. (GAP-015)

---

### 38.5 Revision Drafting and File Management

<!-- WF-DC-002 -->
#### Purpose
Author a new revision from an existing effective document, upload or replace controlled PDF and editable Word source files, and calculate cryptographic integrity hashes.

#### Who can use it
Requires the System Administrator or Section Head role type, or being the assigned document owner or Author. Permitted only when the document master is Active.

#### Before you start
- To create a new revision, the document master must currently have an active Effective revision.
- Ensure no other revision of this document is currently in Draft, In Review or Awaiting Approval status.
- Prepare the source Word document (`.docx` or `.doc`) and controlled PDF file (maximum 50 MB each).

#### How to use it
1. Creating a new revision:
   a. On an effective document detail page, click the prominent **Create revision** button.
   b. In the **Create Draft Revision** dialog, select the **Revision Classification**:
      - **Major** or **Minor**. The dialog shows the revision number that the system proposes for the chosen classification.
   c. If authorized as Section Head (Document Controller), you may enable **Override Revision Number (Document Controller Only)** and enter a **Custom Revision Number** (1 to 20 alphanumeric characters, dots, hyphens, or underscores).
   d. Enter the mandatory **Reason for Revision \*** (minimum 10 characters).
   e. Optionally enter a **Summary of Changes** (minimum 10 characters if provided) and **Change Reference / Ticket #**.
   f. Click **Create Draft Revision**. The new revision is created in Draft status and recorded in the audit trail (action codes DocumentRevisionCreated, and RevisionNumberOverridden if the number was overridden).
2. Uploading or replacing draft files:
   a. In the **Document** tab under **Controlled Files on Revision {n}**, click **Upload or Replace File** (or the prominent **Upload controlled file** button).
   b. In the dialog (**Upload Draft File** or **Replace Draft File**), select the **File Role**:
      - **Controlled Document (PDF)**
      - **Editable Source (Word .docx)**
   c. Select the file from your computer and click **Upload & Compute SHA-256**.
   d. The system checks that the file really is a PDF or Word document, computes its SHA-256 checksum, and marks the file as **ACTIVE**. If a previous file of that role existed on the revision, it is kept but marked **SUPERSEDED**.
3. Completing draft prerequisites:
   a. Under **More actions**, open **Change items** to record the changes made in this revision.
   b. Open **Impact assessment** and complete the revision impact assessment. It must be complete before a revision after the first can be submitted for review.

#### System behavior
- **One revision at a time**: A document master cannot have more than one revision in Draft, In Review or Awaiting Approval at the same time.
- **Files only while Draft**: Files can only be uploaded or replaced while the revision is in Draft status. Once the revision is submitted for review, the files are locked.
- **Checksum verification**: Every file is given a SHA-256 checksum when it is stored. When a file is opened or downloaded, the system checks it against the stored checksum. If it does not match, delivery is blocked, a security event is recorded in the audit trail (action code FileIntegrityVerificationFailed) and the system reports: "Document integrity check failed. The file may have been altered or corrupted. Delivery has been blocked."
- **Controlled Copy Download Policy**: If the administrative setting `DocumentControl.ControlledCopy.DownloadPermitted` is set to "false", file download is blocked for general users ("Controlled copy download is currently disabled by administrative policy."); only System Administrators and Section Heads may download copies.

#### Validation
- "Cannot create a revision for a voided document."
- "Cannot create a new revision from an effective document because this document has no effective revision."
- "Cannot create a new revision while another revision is currently in draft or review/approval workflow."
- "Reason for Revision is mandatory and must contain at least 10 characters."
- "Change Summary, if provided, must contain at least 10 characters."
- "Revision number '{number}' already exists for this document."
- "Custom revision number must be 1-20 alphanumeric characters, dots, hyphens, or underscores."
- "Only Document Controllers are authorized to override the proposed revision number."
- "File content cannot be empty."
- "File size exceeds the maximum permitted limit of 50 MB."
- "Controlled document file must be a PDF (.pdf)."
- "Invalid file format. The uploaded file is not a valid PDF document."
- "Source document file must be a Word document (.docx or .doc)."
- "Invalid file format. The uploaded file is not a valid Word document."
- "Files can only be added or replaced on revisions in Draft state. Current status is {status}."
- "You do not have permission to upload or replace files for this revision."

> **Note for reviewers:** The controlled PDF must be uploaded by hand; MicroLIMS does not convert a Word file to PDF at approval, and a revision cannot be approved without an active controlled PDF. (GAP-815)

> **Note for reviewers:** The fields of the **Change items** and **Impact assessment** dialogs have not been documented yet. (GAP-043)

---

### 38.6 Technical Review and Finding Resolution

<!-- WF-DC-002 -->
#### Purpose
Conduct formal secondary technical review of draft document revisions, document itemized review findings across pages and sections, enforce a finding resolution ladder, and ensure technical accuracy before routing for approval.

#### Who can use it
- **Submitting for review**: System Administrator, Section Head, document owner, or assigned Author.
- **Conducting review**: The assigned technical reviewer, or a Section Head. System Administrators cannot complete or return reviews.
- **Recording review findings**: The assigned technical reviewer, Section Head, or System Administrator.
- **Responding to findings**: The author of the revision.

#### Before you start
- The revision must be in Draft status.
- An active editable Word source file (`.docx` or `.doc`) must be attached (a controlled PDF is accepted as fallback).
- The Revision Impact Assessment must be complete (required for every revision after the first).
- Any findings that originated this revision (for example from a periodic review) must be addressed.
- The designated technical reviewer cannot be the revision's author.

#### What you see
[SCREENSHOT SS-385]
Submitting opens the **Submit for Technical Review** dialog:
- **Technical Reviewer** selection dropdown (lists active users, filtering out the revision's author and the logged-in user).
- **Review Due Date** date selector.
- **Submission Notes** text input.
- Button: **Submit for Review**.

Once submitted, opening the review launches the **Technical Review** drawer:
- Revision details, assigned reviewer, due date, and cycle number.
- Button: **Add Finding** (fields: **Page #**, **Section #**, **Comment / Finding Details**, and switch **Mandatory (blocks review completion until resolved)**).
- Findings list displaying finding cards with current status chip (**Open**, **AuthorResponded**, **ReviewerVerified**, **Resolved**).
- Action buttons:
  - Author actions: button **Respond** (field: **Author Response Notes**, button **Submit Response**).
  - Reviewer actions: button **Verify** (field: **Verification Notes**), button **Resolve**.
  - Decision buttons: **Complete Review** (disabled while open mandatory findings exist) and **Return**.
  - Review comparison: button **Dual PDF** (opens side-by-side comparison of the draft PDF against the currently effective PDF).

#### How to use it
1. Submitting for technical review:
   a. On the draft document detail page, click **Submit for technical review**.
   b. Select the **Technical Reviewer**, set a **Review Due Date**, and add submission notes.
   c. Click **Submit for Review**. The revision moves from Draft to In Review and a review task is created with an incremented cycle number (audit action code RevisionSubmittedForReview).
2. Logging findings (Reviewer):
   a. In the **Workflow** tab, click **Open Review** (or prominent **Open technical review**).
   b. Click **Add Finding**.
   c. Specify the page, section, finding comments, and whether the finding is **Mandatory**. Click save.
3. Responding to findings (Author):
   a. On any finding with status Open, click **Respond**.
   b. Enter your response in **Author Response Notes** and click **Submit Response**. Finding status becomes AuthorResponded.
4. Verifying and resolving findings (Reviewer):
   a. On a finding in AuthorResponded status, click **Verify** and enter **Verification Notes**. Finding status becomes ReviewerVerified.
   b. Click **Resolve**. Finding status becomes Resolved.
5. Completing or returning the review (Reviewer):
   a. If corrections are required, click **Return**. Enter the optional **Summary Review Notes** and confirm. The review task is marked as returned for correction, the revision goes back to Draft, and the author can upload updated files (audit action code TechnicalReviewReturned). After a return, the author submits again and a new review cycle starts.
   b. If all technical aspects are verified and all mandatory findings are resolved, click **Complete Review**. Enter the optional **Summary Review Notes** and confirm. The review task is marked as completed and the revision moves to Awaiting Approval (audit action code TechnicalReviewCompleted).

#### System behavior
- **Segregation of Duties Enforcement**:
  - The author of a revision cannot be assigned as its technical reviewer.
  - The author cannot assign themselves as technical reviewer.
  - The author cannot perform technical review of their own revision.
  - The author cannot create reviewer findings, verify findings, or resolve reviewer-mandated findings without reviewer verification.
- **Finding Resolution Ladder**: Findings must follow the sequence Open, then AuthorResponded, then ReviewerVerified, then Resolved. Any other jump is refused.
- **Mandatory Finding Gate**: The review cannot be completed while mandatory findings remain unresolved.
- Technical review decisions are not electronically signed.

#### Validation
- "Cannot submit revision for technical review in status {status}. Status must be Draft."
- "Cannot submit revision for technical review without an active editable Word source file (.docx or .doc)."
- "A multi-category Revision Impact Assessment must be completed before submitting for technical review."
- "Cannot submit revision for review: {n} originating review finding(s) remain unaddressed."
- "A technical review task is already active for this revision."
- "User is not authorized to submit this document revision for technical review."
- "Segregation of Duties Violation: The author of a document revision cannot be assigned as its technical reviewer."
- "Segregation of Duties Violation: The author cannot assign themselves as technical reviewer."
- "Segregation of Duties Violation: The author cannot perform technical review of their own revision."
- "Segregation of Duties Violation: The author cannot create reviewer findings."
- "Segregation of Duties Violation: The author cannot verify reviewer findings."
- "Segregation of Duties Violation: The author cannot close or resolve a reviewer-mandated finding without reviewer verification."
- "Cannot complete technical review while {n} mandatory review finding(s) remain unresolved."
- "A review finding can only be resolved once it has been verified by the reviewer. Current status is {status}; it must be ReviewerVerified (the author responds, the reviewer verifies, then the finding is resolved)."
- "Only the assigned technical reviewer or document controller may complete this technical review."
- "Only the assigned technical reviewer or document controller may add review findings."

> **Note for reviewers:** In the dual PDF viewer, the draft and effective documents are displayed side-by-side for manual visual comparison; the system does not calculate or render automated textual difference highlighting. (GAP-808)

---

### 38.7 Approver Assignment and Electronic Signature Approval

<!-- WF-DC-002 -->
#### Purpose
Route technically verified revisions to authorized approvers, conduct final dossier inspection, enforce approval readiness criteria, and execute final approval with a password-authenticated electronic signature.

#### Who can use it
- **Assigning approver**: System Administrator, Section Head, document owner, or assigned Author.
- **Executing approval**: Designated approver, Section Head, or System Administrator. The approver must have a base role type of Reviewer, Section Head, or System Administrator, and cannot be the author or any technical reviewer of the revision.

#### Before you start
- The revision must be in Awaiting Approval status.
- Technical review must be completed.
- An active controlled PDF file must be attached.
- All mandatory findings must be resolved, the impact assessment must be complete and the change items addressed.
- Have your account password ready to sign the approval record.

#### What you see
[SCREENSHOT SS-386]
Clicking **Assign approver** opens the **Assign Approver & Route for Approval** dialog:
- **Designated Approver** dropdown (filtered to Reviewer, Section Head, and System Administrator role types, excluding the author and technical reviewers).
- **Target Effective Date (Optional)** (helper text explains that selecting a future date causes the revision to become Future Effective upon approval).
- **Approval Due Date (Optional)** date selector.
- **Submission Notes for Approver (Optional)** text input.
- Button: **Assign & Route for Approval**.

Clicking **Open approval workspace** launches the **Approval Workspace** dialog:
- Tabs: **Inspection Dossier** (the document and review material to inspect) and **Decision Controls**.
- Pre-approval readiness checklist verifying:
  - Revision is in Awaiting Approval status.
  - Active controlled PDF is attached.
  - Technical review is completed.
  - Mandatory findings are resolved.
  - Impact assessment is completed.
  - Change items are addressed.
  - Segregation of duties criteria are satisfied.
- Decision options:
  - **Approve**: enabled only when all checklist items pass. Includes fields for **Effective Date** and **Approval Notes (Optional)**.
  - **Return for Correction**: requires mandatory return notes (minimum 10 characters).
  - **Decline / Cancel**: requires mandatory decline justification (minimum 10 characters).
- Clicking **Approve** opens the **Electronic Signature** dialog:
  - The signature meaning (Approved) and the signature statement text shown on screen (see the note below)
  - Password input field: "Re-enter Your Password to Authenticate"
  - Button: **Sign & Approve**

#### How to use it
1. Assigning the approver:
   a. Click the prominent **Assign approver** button.
   b. Select the designated approver from the dropdown.
   c. Optionally set a **Target Effective Date**, approval due date, and notes.
   d. Click **Assign & Route for Approval**. An approval task is created in pending status (audit action code ApprovalTaskCreated).
2. Executing the approval decision (Approver):
   a. Click the prominent **Open approval workspace** button.
   b. In the **Inspection Dossier** tab, inspect the controlled PDF, review findings, and impact assessment.
   c. Switch to **Decision Controls** and verify that all items on the readiness checklist are satisfied.
   d. If approving:
      - Confirm or adjust the **Effective Date** (leave blank or select today for immediate activation; select a future date for scheduled activation).
      - Add optional approval notes.
      - Click **Approve**.
      - In the signature dialog, re-enter your account password and click **Sign & Approve**.
   e. If returning for corrections:
      - Select **Return for Correction**.
      - Enter a mandatory explanation (minimum 10 characters).
      - Click confirm. The revision goes back to Draft and the task is marked as returned for correction (audit action code RevisionReturnedByApprover). No password is required.
   f. If declining:
      - Select **Decline / Cancel**.
      - Enter a mandatory cancellation justification (minimum 10 characters).
      - Click confirm. The revision becomes Cancelled, which is final (audit action code RevisionDeclinedByApprover). No password is required.

#### System behavior
- **Segregation of Duties Enforcement**:
  - The author of a document revision cannot be assigned as its approver, cannot approve, and cannot decide upon their own revision.
  - A user who participated in technical review for the revision cannot be assigned as its approver, cannot approve, and cannot decide upon the revision.
  - System Administrators possess no exemption from these rules.
- **Electronic Signature Immutability**:
  - Approving applies an electronic signature with the meaning Approved. The audit trail records the signer's user name, full name, role, the signature meaning and the time.
  - Password verification is strictly enforced. Five failed signature attempts within 15 minutes throttle further attempts for 15 minutes.
- **Immediate versus Future Activation**:
  - If the effective date is today or earlier: in one save, the approval task becomes Approved, the revision becomes Effective, the document points to this revision, its next periodic review date is set to the effective date plus the review cycle (default 24 months), and any earlier Effective revision becomes Superseded.
  - If the effective date is in the future: the revision becomes Future Effective. It stays in this status until an automatic process activates it on the target date (the check runs every 60 minutes by default).
- **Auditing**: The audit trail records the action codes ElectronicSignatureApplied and DocumentRevisionApproved.

#### Validation
- "Cannot create an approval task: Revision has not completed technical review."
- "Cannot approve document revision: Prerequisites not met. Errors: ..."
- "An active Controlled PDF file is required."
- "Technical review has not been completed."
- "Segregation of Duties Violation: The author of a document revision cannot be assigned as its approver."
- "Segregation of Duties Violation: The author of a document revision cannot approve or decide upon their own revision."
- "Segregation of Duties Violation: A technical reviewer for this revision cannot be assigned as its approver."
- "Segregation of Duties Violation: A technical reviewer for this revision cannot approve or decide upon this revision."
- "Segregation of Duties Violation: You are the author of this revision and cannot approve it."
- "Segregation of Duties Violation: You participated in technical review and cannot approve this revision."
- "Only the assigned approver or Document Controller may execute an approval decision."
- "Password re-authentication is required to apply electronic signature."
- "A mandatory justification of at least 10 characters is required for return or decline."
- "An electronic signature for approval has already been applied to revision {number}."
- "User '{name}' with role '{role}' is not eligible to be assigned as an approver."
- "Password verification failed. The signature was not applied."
- "Too many failed signature attempts. Wait 15 minutes, then try again. The signature was not applied."

> **Note for reviewers:** The automated training assignment cascade is triggered only when the background worker activates a Future Effective revision; approving a revision with an immediate effective date does not automatically create training assignments. (GAP-816)

> **Note for reviewers:** The approval and signature screens contain regulatory wording (for example references to 21 CFR Part 11). This is screen text only, not a validation statement; it is the same kind of claim as on the sign-in page. (GAP-023)

---

### 38.8 Independent Periodic Review

<!-- WF-DC-003 -->
#### Purpose
Conduct scheduled periodic evaluations of released, effective controlled documents to determine whether they remain technically valid, require formal revision, or should be recommended for obsolescence.

#### Who can use it
The assigned periodic reviewer, a Section Head, or a System Administrator. The author of the revision under review cannot be assigned and cannot perform or complete the review.

#### Before you start
- The document master must be Active with an Effective revision that has reached its scheduled Next Review Date.
- No other periodic review task may be pending or in progress for that revision.

#### What you see
Opening a periodic review task from the document detail page launches the **Periodic Review Workspace** dialog:
- Header details: MicroLIMS ID, Effective Revision, Review Due Date (with chip **OVERDUE** if past due), Review Cycle (in months), Assigned Reviewer, and Status.
- Button: **Inspect Controlled PDF** (opens the released document for evaluation).
- Three workspace tabs:
  1. **Review Notes & Findings ({n})**:
     - Fields: **Page #**, **Section #**, **Review Note / Observation \***, and button **Add Note**.
     - Table of recorded observations with action **Mark Resolved**.
  2. **Independent Review History ({n})**: displays historical periodic reviews completed for the document master.
  3. **Outcome & Decision**:
     - Mandatory selection **Decision Outcome \***:
       - **Remains Valid**
       - **Revision Required**
       - **Obsolescence Recommended**
     - Mandatory text area **Review Summary & Conclusion \*** (minimum 10 characters).
     - Button: **Submit & Complete Review**.

#### How to use it
1. Periodic review tasks are generated automatically by the system when a document reaches its next review date.
2. In the **Workflow** tab under **Independent Periodic Review History**, click **Workspace** on the pending task.
3. Click **Inspect Controlled PDF** to evaluate current operational procedures against the document content.
4. If discrepancies or observations are identified, add notes under **Review Notes & Findings**.
5. Switch to **Outcome & Decision**:
   a. Select the **Decision Outcome**:
      - Choose **Remains Valid** if the procedure continues to reflect current practice without modification.
      - Choose **Revision Required** if procedural updates or technical corrections are needed.
      - Choose **Obsolescence Recommended** if the procedure is redundant or being replaced.
   b. Enter a comprehensive conclusion in **Review Summary & Conclusion \*** (minimum 10 characters).
   c. Click **Submit & Complete Review**.

#### System behavior
- **Remains Valid**: The review task is marked Completed, the document's Next Review date moves forward by the review cycle (counted from today), and the audit trail records PeriodicReviewRemainsValid.
- **Revision Required**: The review task is marked Completed, the recorded findings are kept, a new draft revision can then be created that refers to this review, and the audit trail records PeriodicReviewRevisionRequired.
- **Obsolescence Recommended**: The review task is marked Completed and an approval task (due in 14 days) is created for the assigned approver, the document owner, or another Section Head. The audit trail records PeriodicReviewObsolescenceRecommended. The document is not retired by this step (see the note below).
- A new review task is created automatically for each Active document whose Effective revision has reached its next review date, and is assigned to the Technical Reviewer assignment or else the document owner.
- **Segregation of Duties**: The author of the revision cannot perform the periodic review, cannot add review notes, and cannot be assigned as its periodic reviewer.
- The periodic review workspace does not ask for a password or signature.

#### Validation
- "This periodic review task is already completed and cannot be altered."
- "Cannot complete a cancelled periodic review task."
- "A comprehensive review summary of at least 10 characters is mandatory."
- "Only the assigned reviewer or Document Controller can complete this review."
- "Segregation of Duties Violation: The author cannot perform or complete the periodic review."
- "Segregation of Duties Violation: The author of the revision under review cannot add review notes or perform the review."
- "Segregation of Duties Violation: The author of the document revision cannot be assigned as its periodic reviewer."

> **Note for reviewers:** **Obsolescence Recommended** creates an approval task, but MicroLIMS has no step that sets a document to Obsolete, and the approval check refuses to approve that task. Effective documents cannot be retired in the application, although the screens (such as the **Obsolete** status filter) and some wording suggest it. (GAP-818)

> **Note for reviewers:** If a periodic review task has no assigned reviewer, the system applies no role check when the review is completed. (GAP-814)

---

### 38.9 My Reading List and Conscious Acknowledgement

<!-- WF-DC-004 -->
#### Purpose
Track personal controlled document reading and training assignments, access released PDF documents in the controlled viewer, and record a read-and-understood acknowledgement (checkbox confirmation, no password).

#### Who can use it
Open to all signed-in users. Each user sees their own personal training assignments. Users holding the *Document Training Assignment* permission (by default: Section Head, System Administrator) can view assignments across personnel.

#### What you see
[SCREENSHOT SS-387]
The **My Reading List** page presents:
- Header title **My Reading List** with subtitle "Active document training and standard operating procedures assigned to {your name}."
- Four summary counter cards:
  - **TOTAL ASSIGNMENTS**
  - **PENDING READING / ACKNOWLEDGEMENT**
  - **OVERDUE ASSIGNMENTS**
  - **COMPLETED & ACKNOWLEDGED**
- Search box (placeholder "Search document title, code, MicroLIMS ID...") and button **Refresh List**.
- View tabs with counts: **To read**, **Overdue**, **Completed**, **Superseded**, and **All**.
- Assignments table columns:
  1. **Company Document Code & Title**: code and title with chip `MicroLIMS: {id}`
  2. **Revision**: revision chip (e.g. `Rev 01`, with chip `Superseded Revision` if superseded)
  3. **Assignment Type**: curriculum or direct assignment
  4. **Status**: status chip (displays `Overdue by {n}d` if overdue)
  5. **Due Date (UTC)**: completion deadline
  6. **Reading Progress**: progress bar accompanied by caption "Informational Only. Formal Acknowledgement Required"
  7. **Actions**: row action buttons
- Row action buttons:
  - **Read** (tooltip "Open Controlled Revision in PDF Viewer"; opens the controlled PDF viewer)
  - **Acknowledge** (tooltip "Record Legal Read-and-Understand Acknowledgement"; displayed only when the assignment is not acknowledged, not superseded, and not cancelled)
- Empty states: displays "No Training Assignments Found" with "No assignments matched your selected filter criteria." or "You have no active or completed document reading assignments."

Clicking **Acknowledge** opens the **Controlled Document Training Acknowledgement** dialog:
- Header displaying document code, revision, assignment status, assigned user, due date, and controlled file information.
- Evidentiary acknowledgement statement:
  "I confirm that I have read, understood, and agree to adhere to the contents of this controlled document revision."
- Mandatory confirmation checkbox: **I confirm the evidentiary statement above.**
- Optional text area: **Acknowledgement Remarks (Optional)**.
- Action buttons: **Cancel** and **Submit acknowledgement**.

#### How to use it
1. Navigate to **Document Control** and select **My Reading List**.
2. Review pending assignments in the **To read** or **Overdue** tabs.
3. Click **Read** to open the controlled document in the PDF viewer. Review the full procedure and instructions.
4. When finished reading, close the viewer and click **Acknowledge**.
5. In the acknowledgement dialog, review the evidentiary statement.
6. Select the checkbox: **I confirm the evidentiary statement above.**
7. Optionally enter any remarks.
8. Click **Submit acknowledgement**.
9. The assignment status immediately changes to **Acknowledged** (audit action code ReadingAssignmentAcknowledged), and your cell in the Training Matrix counts as **Qualified**.

#### System behavior
- **Acknowledgement requires the checkbox**: reading progress and scroll tracking are informational only; the acknowledgement is recorded only when the confirmation checkbox is ticked.
- **Acknowledgement record**: Submitting an acknowledgement stores the statement text as shown, the SHA-256 checksum of the controlled PDF, the time, the client IP address and the browser user agent.
- **Assignment Ownership**: Users can only acknowledge assignments assigned to their own account. Attempting to submit an acknowledgement for another user is blocked and records a security audit event.
- No password re-entry or electronic signature is required for reading acknowledgement.

#### Validation
- If the checkbox is not checked: "You must explicitly verify and accept the legal acknowledgement statement."
- "Explicit confirmation of the legal acknowledgement statement is required. Reading or viewing alone does not constitute legal acknowledgement."
- "You are not authorized to acknowledge an assignment assigned to another user."
- "Cannot acknowledge assignment because Document {code} is in {status} status."
- "Cannot acknowledge assignment because Revision {number} is in {status} state (must be Effective)."
- "Cannot acknowledge an assignment that has been terminally closed as SupersededIncomplete."
- "Cannot acknowledge an assignment that has been terminally closed as Cancelled due to document obsolescence."

> **Note for reviewers:** The system permits users to submit the acknowledgement without enforcing complete scrolling or a minimum reading duration in the PDF viewer; scroll progress is informational. (GAP-810)

> **Note for reviewers:** The acknowledgement dialog heading contains regulatory wording ("GMP / 21 CFR Part 11"). This is screen text only, not a validation statement. (GAP-023)

> **Note for reviewers:** Reading assignments are not created when a revision is approved with an effective date of today or earlier; check the Training Matrix for missing assignments. (GAP-816)

---

### 38.10 Training Matrix and Qualification Tracking

<!-- WF-DC-005 -->
#### Purpose
Track, verify, and cross-reference personnel qualifications against active controlled documents across departments, and identify training compliance gaps or superseded qualifications.

#### Who can use it
Open to all signed-in users. Users holding the *Document Training Assignment* permission (by default: Section Head, System Administrator) can view the entire matrix; the *Training Matrix Viewing* permission has no effect, so Reviewers do not see the whole matrix by default; users without this permission see only their own row. The **Document Compliance Profile** drawer and the compliance KPIs are not limited in this way: any user who can open the matrix can see every assigned person's status for a document.

#### What you see
[SCREENSHOT SS-388]
The **Training Matrix** page presents:
- Header title **Training Matrix** with subtitle "Multi-axis qualification matrix mapping personnel to controlled document training requirements."
- Action buttons: **Compliance Dashboard** and **Refresh Matrix**.
- Five summary metric cards:
  - **PERSONNEL TRACKED**
  - **QUALIFIED CELLS**: assignments acknowledged on current effective revision
  - **PENDING CELLS**: open assignments within due date
  - **OVERDUE CELLS**: open assignments past due date
  - **SUPERSEDED GAPS**: qualifications held only on older, superseded revisions
- Filter bar:
  - **Search** box (placeholder "Search personnel name, username, or role...")
  - **Department** dropdown ("All Departments")
  - **Qualification Status** dropdown ("All States", "Qualified", "Pending", "Overdue", "Trained on Superseded Only")
- Qualification grid:
  - First column: **Personnel / Role**
  - Document columns: one column for each active effective document, with header chip displaying `Rev {n}` and tooltip showing `{code}: {title} (Current Rev: {n})`.
  - Cell qualification chips:
    - **Qualified** (acknowledged current effective revision)
    - **Pending** (assigned and within due date)
    - **Overdue** (past due date without acknowledgement)
    - **Trained on Superseded Only** (acknowledged an earlier revision, but current revision is unacknowledged)
    - Dash symbol with tooltip "Not Assigned / Not in Curriculum" for unassigned documents.
- Loading and empty states:
  - Loading text: "Computing real-time training qualification matrix..."
  - Empty state: "No personnel records matched your filter criteria."

#### How to use it
1. Navigate to **Document Control** and select **Training Matrix**.
2. Filter by **Department** or **Qualification Status**, or search for a specific analyst by name.
3. Review qualification states across matrix cells.
4. Drill down into profiles:
   - Click any personnel name to open the **User Compliance Profile** drawer, displaying their overall **COMPLIANCE SCORE** and detailed document training breakdown.
   - Click any document column header to open the **Document Compliance Profile** drawer, displaying qualification statuses for all assigned personnel on that document.
   - Click any individual cell to see the assignment history.
5. Click **Compliance Dashboard** to view the compliance rate, departmental ranking and top overdue documents.

#### System behavior
- Reading assignments come from the automatic training process that runs when a Future Effective revision is activated, for users with a curriculum role (the curriculum's department is read but not applied), for earlier completers when retraining is on, or for the document's assigned personnel when neither exists. The due date is the effective date plus a grace period (14 days if none is configured).

> **Note for reviewers:** Role curricula, per-document training settings and training escalations have no screen, and the curriculum department is not applied. (GAP-807, GAP-809, GAP-819)

> **Note for reviewers:** Reading assignments are not created when a revision is approved with an effective date of today or earlier. (GAP-816)

> **Note for reviewers:** The Document Compliance Profile drawer and compliance KPIs are open to every user who can access the matrix, rather than restricted to their own data. (GAP-907)

---

### 38.11 Training Compliance Dashboard

#### Purpose
Provide operational and executive oversight of organizational training compliance rates, departmental performance rankings, and top overdue training exceptions.

#### Who can use it
Open to all signed-in users. The organisation-wide figures are shown to every signed-in user.

#### What you see
The **Training Compliance Dashboard** presents:
- Header title **Training Compliance Dashboard** with subtitle "Executive & operational compliance metrics, departmental ranking, and overdue exceptions."
- Header button: **View Full Matrix** (returns to the Training Matrix).
- Four summary KPI cards:
  1. **OVERALL COMPLIANCE RATE**: percentage of completed versus required training assignments (`{completed} / {total} required training assignments`), with a progress bar that is green at 90% or above and warning below 90%.
  2. **QUALIFIED & COMPLETED**: caption "Across {n} personnel & {n} effective documents".
  3. **OVERDUE EXCEPTIONS**: count of past-due assignments with caption "Action required. Click to filter matrix".
  4. **SUPERSEDED GAPS**: count of personnel qualified on a prior revision only with caption "Qualified on prior revision only".
- **Departmental Compliance Ranking** panel:
  - Subtitle: "Organizational compliance breakdown categorized by department."
  - Table columns: **Department**, **Required**, **Qualified**, **Overdue**, and **Compliance Rate**.
  - Empty state displays: "No department training data recorded."
- **Top Overdue Documents** panel:
  - Subtitle: "Controlled documents with the highest volume of overdue reading assignments."
  - Header link: **View All**
  - Table columns: **Document Code & Title** and **Overdue** (chip `{n} Overdue`).
  - Empty state displays: "Zero overdue documents! All assignments are on schedule."

#### How to use it
1. Navigate to **Document Control** and select **Compliance Dashboard**.
2. Review the organization-wide **OVERALL COMPLIANCE RATE**.
3. Inspect the **Departmental Compliance Ranking** table to identify departments requiring training follow-up.
4. Click **OVERDUE EXCEPTIONS** (caption "Action required. Click to filter matrix") to open the Training Matrix, or **View Full Matrix** to see all personnel.

---

### 38.12 Document Control Audit Trail

<!-- MOD-DC-003 -->
#### Purpose
Search, trace, and export the audit log of all document control lifecycle actions, file modifications, technical reviews, approval decisions, periodic reviews, and configuration updates.

#### Who can use it
Restricted by the server to users whose base role type is Reviewer, Section Head, or System Administrator. Users with other role types receive the refusal: "You do not have permission to query the global audit trail."

#### What you see
[SCREENSHOT SS-389]
The **Document Control Audit Trail** page presents:
- Header title **Document Control Audit Trail** with subtitle "Chronological, append-only regulatory audit log of document life-cycle actions and configuration changes".
- Header button: **Export Audit Trail (CSV)** (displays "Exporting CSV..." during file generation).
- Filter bar:
  - **Search** box (placeholder "Search Event UID, Action, Reason, Entity...")
  - **Action Category** dropdown ("All Categories", "Document", "Workflow", "Security", "Configuration", "System")
  - **Date From** and **Date To** date inputs
  - Button: **Reset**
- Audit log table:
  - Columns:
    1. **Timestamp (UTC)**: precise event timestamp
    2. **Event UID**: unique audit event identifier
    3. **Actor**: user who executed the action
    4. **Action Code**: system audit action identifier
    5. **Category**: action category
    6. **Record Type**: affected entity type
    7. **Reason / Description**: recorded justification or system note
    8. **Field Changes**: property-level change details
  - Pagination: rows per page choices (10, 20, 50, 100).
  - Loading text: "Loading audit trail records..."
  - Empty state displays: "No audit records match the selected criteria".

#### How to use it
1. Navigate to **Document Control** and select **Audit Trail**.
2. Filter by search keyword, action category, or date range.
3. Read the event rows; the **Field Changes** column shows what was modified.
4. To export audit records:
   a. Click **Export Audit Trail (CSV)**.
   b. The browser downloads a CSV file containing up to the 5,000 most recent matching audit records.
   c. The system records the export itself in the audit trail (action code AuditTrailExported, category Security).

#### System behavior
- **Domain Scoping**: The Document Control audit query returns only events associated with Document Control entities, document master IDs, and export operations.
- **Export Retention Cap**: CSV exports are capped at 5,000 records, newest first, with no warning if more records exist.

> **Note for reviewers:** The audit filter dropdown provides category choices "Workflow" and "System" which do not correspond to backend database category values, while omitting backend categories Review, Approval, and Training. (GAP-813)

---

### 38.13 Document Control Configuration

<!-- WF-DC-006 -->
#### Purpose
Configure document types, default review cycles, departments, laboratory sections, automated sequence numbering schemes, and global document control governance parameters.

#### Who can use it
Requires the *Document Control Configuration* permission (by default: System Administrator only) AND the System Administrator role type. The route is hidden and blocked for other users ("Access Denied: Only System Administrators can access and modify Document Control module configuration."). Reading configuration lists requires only sign-in.

#### What you see
The **Document Control Configuration** page presents:
- Header title **Document Control Configuration** with subtitle "System Master Data, Numbering Schemes, Governance Rules, and Module Settings".
- Four configuration tabs:
  1. **Document Types ({n})**:
     - Header: "Document Types Master" with button **Add Document Type**.
     - Table columns: **Code**, **Name**, **Default Review Cycle**, **Status** (chips **ACTIVE** or **DEACTIVATED**), and **Actions** (edit icon).
     - Dialog fields: **Type Code \*** (placeholder "e.g. SOP, POL, VAL"), **Type Name \*** (placeholder "e.g. Standard Operating Procedure"), **Default Review Cycle (Months) \***, and switch **Active Status**.
  2. **Departments & Sections ({n})**:
     - Header: "Departments and Laboratory Sections" with buttons **Add Department** and **Add Section**.
     - Department cards listing associated sections with columns: **Section Name**, **Status**, and **Action** (edit icon). If empty, displays "No sections registered under this department."
     - Dialog fields: **Department Code \***, **Department Name \***, **Section Name \***, and switch **Active Status**.
  3. **Numbering Configuration**:
     - Header: "MicroLIMS Document ID Numbering Scheme".
     - Live preview: **Sample Generated Document ID** (for example "DOC-0000001").
     - Entry fields: **Prefix \*** (helper text: "E.g. DOC-, SOP-, QA-"), **Number Format (Zero-Padded Pattern) \*** (helper text: "E.g. 0000000 (7 digits), 00000 (5 digits)"), switch **Enable Automated Sequence Numbering** (caption: "Active numbering profile").
     - Button: **Save Numbering Configuration** (displays "Updating Scheme..." while saving).
  4. **Module Settings ({n})**:
     - Header: "Document Control Module Governance Settings".
     - Table columns: **Group**, **Setting Key**, **Value**, **Data Type**, **Modified By**, and **Action** (edit icon).
     - Edit dialog: **Edit Configuration Setting** with field **Setting Value \***.

#### How to use it
1. Adding or editing a Document Type:
   a. In the **Document Types** tab, click **Add Document Type** or click the edit icon on an existing row.
   b. Enter the Type Code, Type Name, and Default Review Cycle in months (must be between 1 and 120).
   c. Set the **Active Status** switch and click **Save**. The system confirms: "Document Type saved successfully."
2. Adding a Department or Section:
   a. In the **Departments & Sections** tab, click **Add Department** to create an organizational unit.
   b. Click **Add Section** to create a laboratory section linked to that department.
   c. The system confirms: "Department saved successfully." or "Section saved successfully."
3. Updating Numbering Configuration:
   a. In the **Numbering Configuration** tab, adjust the **Prefix** or zero-padding format mask.
   b. Review the **Sample Generated Document ID** preview.
   c. Click **Save Numbering Configuration**. The system confirms: "Numbering configuration updated successfully." Future registrations use the updated format.
4. Editing Module Governance Settings:
   a. In the **Module Settings** tab, click the edit icon next to a setting (such as `DocumentControl.ControlledCopy.DownloadPermitted`).
   b. Enter the new value ("true" or "false") and click **Save Setting**. The system confirms: "Setting updated successfully."

#### Validation
- "Only System Administrators can manage module configuration."
- "Document Type Code is required." and "Document Type Name is required."
- "Default review cycle must be between 1 and 120 months."
- "Document Type Code '{code}' already exists."
- "Department Code is required.", "Department Name is required." and "Department Code '{code}' already exists."
- "Section Name is required."
- "Prefix is required." and "Number Format is required (e.g. 0000000)."

> **Note for reviewers:** Training settings, escalation days and role curricula have no configuration screen; only the four tabs above exist. (GAP-809)

---

### 38.14 Voiding Document Masters and Cancelling Draft Revisions

<!-- WF-DC-007 -->
#### Purpose
Remove errant, unreleased document masters from the active register, or cancel abandoned draft revisions while preserving complete audit traceability.

#### Who can use it
- **Voiding a Document Master**: Strictly restricted to the **Section Head** role type (Document Controller). System Administrators are refused.
- **Cancelling a Draft Revision**: System Administrator, Section Head, document owner, or assigned Author.

#### Before you start
- **To void a master**: The document master must be Active and must **never have held an Effective or Superseded revision**. Documents that have ever been released cannot be voided.
- **To cancel a draft**: The revision must currently be in Draft status.

#### How to use it
1. Voiding a document master:
   a. In the Document Library or on the Document Detail page under **More actions**, click the red action **Void Document Master** (or **Void document master**).
   b. In the dialog (**VOID DOCUMENT MASTER**), read the explanation that voiding removes the master from the active register and releases its Company Code.
   c. Enter a mandatory justification in the reason field (minimum 10 characters, placeholder "State the regulatory / administrative justification for voiding this document...").
   d. Click **Confirm Void Record** (displays "Voiding Record...").
   e. The master's record status becomes Void, the void date, user and reason are recorded, and the audit trail records the action (action code DocumentMasterVoided).
2. Cancelling a draft revision:
   a. On the Document Detail page under **More actions**, click **Cancel draft revision**.
   b. In the dialog (**CANCEL DRAFT REVISION**), enter a mandatory cancellation justification (minimum 10 characters).
   c. Click **Confirm Cancel Draft** (displays "Cancelling...").
   d. The revision status becomes Cancelled, its active files are set inactive, and the audit trail records the action (action code DraftRevisionCancelled).

#### System behavior
- **Company Code Release on Void**: Company Document Codes are enforced as unique only among *active* documents. When a document master is voided, its Company Document Code is released and may be reused for a future document. The permanent MicroLIMS ID sequence is never reused.
- Neither voiding nor cancelling deletes anything; MicroLIMS has no delete function for documents. Neither asks for a password or signature.

#### Validation
- "Only the Document Controller can void a Document Master."
- "A mandatory void reason of at least 10 characters is required."
- "Document Master is already voided."
- "A Document Master that has held an effective revision cannot be voided. It must be retired through obsolescence."
- "You do not have permission to cancel this draft revision."
- "A mandatory cancellation reason of at least 10 characters is required."
- "Only revisions in Draft state can be cancelled. Current status is {status}."

> **Note for reviewers:** The refusal message about a master that has held an effective revision says it "must be retired through obsolescence", but MicroLIMS has no step that sets a document to Obsolete. (GAP-818)

---

### 38.15 Document Escalations, Background Automation, and Not-Implemented Features

Some Document Control functions run automatically or have no screen:
- **Automatic activation and review scheduling**: An automatic process runs every 60 minutes by default:
  1. Revisions in Future Effective status whose target effective date has arrived become Effective, the previous Effective revision becomes Superseded, the audit trail records RevisionAutomaticallyActivated, and the reading assignments for the new revision are created.
  2. Active documents whose Effective revision has reached its next review date receive a periodic review task in pending status, assigned to the Technical Reviewer assignment or else the document owner (audit action code PeriodicReviewTaskCreated).
- **Training escalations**: The same process checks open reading assignments at three points: 3 days before the due date, at the due date (the assignment then shows as overdue), and 1 day after the due date. Each is recorded once as an escalation entry with an audit trail entry. MicroLIMS has no screen for viewing or resolving escalations and sends no message for them. (GAP-807)
- **Document retirement**: MicroLIMS cannot retire an Effective document; there is no Obsolete step. Periodic review allows selecting "Obsolescence Recommended", but the resulting approval task cannot be completed. (GAP-818)
- **Role Curriculum Management**: MicroLIMS does not provide a user interface screen for defining or maintaining role-to-document training curricula or per-document grace periods. (GAP-809, GAP-819)
- **Permissions that have no effect**: The permissions *Document Draft Editing*, *Document Revision Creation*, *Document Technical Review*, *Document Revision Approval*, *Document Periodic Review* and *Training Matrix Viewing* do not change what anyone can do. Authority follows the user's role type and the per-document assignments; voiding is for Section Heads only. (GAP-015)

<!-- Review log (Sonnet): ~85 corrections; invented items removed: route paths, permission codes (outside admin), enum/class/worker/database names, unverified acknowledgement messages, unsupported example revision numbers, unverified signature-record fields, Inspection Dossier contents, Return for Correction label in technical review; callouts added: GAP-043, GAP-807, GAP-809, GAP-816, GAP-818 (Obsolete filter, void message), GAP-023 (approval and acknowledgement wording), GAP-814 reworded; no Obsolete/retire procedure described -->
<!-- Phase 13 (agy): applied F-C06, F-C15 -->

<!-- Phase 13 review (Sonnet): agy corrections verified against RECONCILIATION and code; GAP ids exist; nothing invented; no change needed. -->
