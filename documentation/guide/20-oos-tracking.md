## 20. OOS Tracking

MicroLIMS groups out-of-specification investigations and retest chains under an out-of-specification group code, linking retest samples back to their parent origin sample and providing document management for laboratory investigation reports.

### 20.1 Out-of-Specification Tracking
<!-- WF-OOS-001 -->
#### Purpose
Track out-of-specification investigation chains grouped by OOS number, tracing retest spin-off samples back to the original sample and reviewing retest progression.

#### Who can use it
Requires the *Track out-of-specification groups and manage their investigation documents* permission (`Oos.Manage`, by default: Section Head, System Administrator). Users with section-scoped access see OOS groups containing retest samples with tests in their assigned sections.

#### Before you start
- An out-of-specification investigation group must exist in the system. An OOS group is not created automatically when an out-of-specification test result is recorded; it is created only when a Section Head signs a release decision of **Retest Retained Sample: Return to testing** or **New Sample Required: Close and request new sample** during sample approval.

> **Note for reviewers:** An out-of-specification result by itself does not create an OOS record; only a signed retest decision does. (GAP-305)

> **Note for reviewers:** Investigation and OOS Investigation decisions described in earlier specifications are not available; OOS groups are created only through sample retest decisions. (GAP-017, GAP-018)

#### What you see
- On the navigation sidebar, under **AUDIT & COMPLIANCE**, click **OOS Tracking** (`/oos-tracking`).
- Page header:
  - Title: **Out-of-Specification Tracking**
  - Subtitle: "Every out-of-specification investigation chain grouped by OOS number, tracing all retest spin-offs back to the original sample."
- Section: **OOS Investigation Groups**
  - Summary count: "1 OOS Group" or "{n} OOS Groups ({m} retest samples)". If no chains exist, the page displays: "No active OOS investigation chains found."
- Per OOS group card:
  - Header showing the group code with report status chip: **Lab Investigation Report Attached** (when a non-voided document exists) or **No Report Attached**.
  - Action button: **Manage Investigation** (when a document exists) or **Upload Investigation** (when no document exists).
  - Retest table columns:
    - **Retest Sample**: Reference number of the retest sample.
    - **Parent / Origin**: Reference number of the original sample from which the retest was spawned.
    - **Item / Point**: Material item name or sampling point.
    - **Retest Type**: Retest category (**Retest Retained Sample** or **New Sample Request**).
    - **Test(s)**: Tests ordered for retesting.
    - **Analyst(s)**: Assigned analysts (or "User #{id}").
    - **Retest Status**: Current lifecycle status of the retest sample.
    - **Opened**: Date and time when the retest was ordered.
    - **Actions**: Action icon with tooltip "Open the retest sample's printable report".
  - Empty table text: "No retest samples have been spun off yet for this OOS chain."

[SCREENSHOT SS-201]

#### How to use it
1. In the sidebar, select **AUDIT & COMPLIANCE** -> **OOS Tracking**.
2. Locate the OOS investigation group code to inspect.
3. Review the retest table to trace each retest sample back to its parent origin sample, and check assigned analysts, ordered tests, and current retest status.
4. To inspect the full analytical details of a retest sample, click the action button under **Actions** to open its printable sample report.
5. To view or attach laboratory investigation documentation, click **Upload Investigation** or **Manage Investigation**.

#### System behavior
- Retest samples inherit product identity, batch, sampling points, and storage details from the origin sample, while establishing a permanent link via their origin sample identifier.
- Retest samples carry the shared OOS group code of the parent sample.
- When all retest samples reach final approval or rejection, the resulting outcome is automatically mirrored to the parent sample section without requiring an additional signature.

#### Status
- Retest samples display standard sample lifecycle statuses: **Received**, **In testing** (displayed as **Under Testing** in lifecycle badges), **Under review** (displayed as **Under Review**), **Under approval** (displayed as **Under Approval**), **Approved**, or **Rejected**.
- Retest types appear as **Retest Retained Sample** or **New Sample Request**.

#### Important rules
- Out-of-specification analytical results alone do not open an OOS group. Opening a group requires a formal Section Head retest decision.
- Clicking the retest report icon navigates directly to the printable sample summary report for that specific retest.

#### Errors
- If the system cannot load tracking items from the server, it displays: "Failed to load OOS tracking items."

#### What happens next
- Analysts complete the ordered retest procedures in the laboratory testing workspaces.
- Authorized users upload and manage formal laboratory investigation reports (§20.2).

---

### 20.2 Laboratory Investigation Documents
<!-- WF-OOS-001 -->
#### Purpose
Upload, supersede, and download formal laboratory investigation reports and root-cause files attached to an OOS investigation group.

#### Who can use it
Requires the *Track out-of-specification groups and manage their investigation documents* permission (`Oos.Manage`, by default: Section Head, System Administrator).

#### Before you start
- Open the target investigation group in **Out-of-Specification Tracking**.
- Ensure the investigation report file is in PDF, JPG, JPEG, PNG, WEBP, or TIFF format.
- The file size must not exceed 25 MB.
- When superseding an active report, a non-empty administrative justification is required.

> **Note for reviewers:** OOS investigation documents are not electronically signed, have no technical review step, and cannot be voided from the screen. (GAP-313)

#### What you see
- Dialog: **Lab Investigation Documents: {oosGroupCode}**
- Upload panel (visible only when no **Current** document exists):
  - Heading: **Upload Initial Investigation Report**
  - Buttons: **Select File** and **Upload**
- Investigation documents table:
  - Columns: **Status**, **Filename**, **Size**, **Uploaded By**, **Date**, **Actions**
  - Status chips: **Current**, **Superseded**, **Voided** (displays "Voided: {voidReason}")
  - Empty text: "No investigation documents uploaded for this OOS group yet."
  - Row action tooltips: "Download file", "Supersede with replacement report" (supersede icon appears only on the **Current** document row)
- Supersede panel (opens when clicking the supersede icon on the current row):
  - Informational notice: "The current document will be marked Superseded. A new active version will take its place."
  - Text area: **Reason for Supersession \*** (mandatory)
  - Action buttons: **Choose Replacement File** and **Supersede** (disabled until both a file is selected and a reason is typed)

[SCREENSHOT SS-202]

#### How to use it
To upload an initial investigation report:
1. In the OOS group card, click **Upload Investigation** (or **Manage Investigation**).
2. Under **Upload Initial Investigation Report**, click **Select File**.
3. In your file browser, select the document file (.pdf, .jpg, .jpeg, .png, .webp, or .tiff).
4. Click **Upload**. Upon upload, the document appears in the table with status **Current**.

To supersede an existing investigation report:
1. In the **Lab Investigation Documents: {oosGroupCode}** dialog, locate the document with status **Current**.
2. Click the supersede icon ("Supersede with replacement report").
3. In the **Reason for Supersession \*** field, type the justification for replacing the report.
4. Click **Choose Replacement File** and select the revised report file.
5. Click **Supersede**. The previous document changes to **Superseded**, and the new file becomes **Current**.

To download an investigation document:
1. In the document table, locate the document to view.
2. Click the download icon ("Download file"). The file is downloaded to your browser.

#### System behavior
- Uploaded files undergo validation for file size (maximum 25 MB), empty content, extension matching, declared content type, minimum content length, and file format headers.
- Files are saved with an immutable SHA-256 hash. The hash is verified upon download.
- When superseding, the system records the supersession reason, the user identity, and timestamp on the prior record and designates the replacement as **Current**.
- Uploading and superseding investigation documents do not request a password re-entry or electronic signature.

#### Validation
The system validates file characteristics and form inputs, returning exact messages upon failure:
- "No file was provided."
- "A replacement file is required."
- "A reason for supersession is required."
- "A supersession reason is required."
- "The uploaded file is empty."
- "File exceeds the maximum allowed size of {n} MB."
- "File type '{ext}' is not supported. Allowed types: PDF, JPG, JPEG, PNG, WEBP, TIFF."
- "The declared content type '{type}' does not match the file extension '{ext}'."
- "The file content could not be validated because it is too short."
- "The file does not appear to be a valid WEBP image."
- "The file content does not match the expected format for '{ext}'. The file may be corrupted or misnamed."
- "A voided document cannot be superseded. Upload a new document instead."
- "This document is already voided."
- "A void reason is required."

#### Status
- Document status labels:
  - **Current**: The active laboratory investigation report for the OOS chain.
  - **Superseded**: A previous report replaced by a subsequent version.
  - **Voided**: An invalidated document (voided through administrative service endpoints).

#### Important rules
- Only one document may hold **Current** status for an OOS group at any time.
- A new document upload can only be initiated when no **Current** document exists.
- The **Supersede** button remains disabled until both a valid replacement file and a non-blank supersession reason are provided.
- A voided document cannot be superseded; a new report must be uploaded.

#### Errors
- "Failed to load investigation documents."
- "Failed to upload document."
- "Failed to download document content."
- "Failed to supersede document."
- "OOS group \"{code}\" not found."
- "Document {id} not found for OOS group {code}."
- "Document storage failed. The upload was not completed."
- "Replacement file storage failed. The supersession was not completed."
- "The document file could not be retrieved."
- "Document integrity check failed. The file may have been altered. Please contact your System Administrator."

#### What happens next
- The uploaded investigation document remains permanently linked to the OOS group for review by Section Heads and System Administrators.
