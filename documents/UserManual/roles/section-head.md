# Section Head Role Guide

> **Draft — not approved.** Written from code and the quality record screens (no screenshots yet). Sections on quality events are marked *(Draft — pending approval)*.

| Field | Value |
|---|---|
| Title | Section Head Role Guide |
| Version | 0.1 |
| Status | Draft — pending approval |
| Author | AI coding agent, draft from code |
| Reviewer | |
| Approver | |
| Effective date | |

## 1. What a Section Head can see and do

- **Dashboard (`Section Head Command Center`):** *(Draft — pending approval)* With `Quality.View`, the dashboard also shows **Lab open records & actions** (open quality records and actions in your laboratory sections), **My open records & actions** and **Overdue quality items**. The tiles open the **Quality Events** register; the register does not yet apply the filters the tiles name, so set them yourself.
- **Laboratory work:** Your laboratory's samples, tests, results, reviews and approvals, as described in the other chapters of the user guide.
- **Quality events (`Quality.Raise`, `Quality.Investigate`, `Quality.LabConclude`):** *(Draft — pending approval)* Raise deviations and propose change controls, investigate, sign laboratory conclusions and laboratory reports, plan and carry out CAPAs, and enter the impact assessment of a change control. See section 3.

## 2. What a Section Head cannot do

- **No QA approval or closure:** A Section Head cannot approve, reject, close or cancel a deviation, CAPA, change control or complaint, cannot approve a CAPA plan or check effectiveness, and cannot decide due-date extensions. These are QA steps (`Quality.Approve`).
- **No change-control gate switch and no classifications or due rules:** Only QA changes these (**Quality Configuration**).
- **No signing of your own work:** You cannot sign a laboratory conclusion or laboratory report for a record you own. You cannot approve or close a record you raised or own. The server refuses these requests.

## 3. Quality event tasks *(Draft — pending approval)*

### 3.1 Raise a deviation from a sample, test or equipment screen
1. Open the sample in the **Analyst Testing Workspace**, the assigned test card, or the equipment details dialog.
2. Click **Raise deviation**.
3. Enter **Title**, **Description** (required for these screens), **Immediate action**, **Occurred at (UTC)** and optionally a **Location**. Click **Raise deviation**.
4. The new deviation is listed under **Quality records** on the same screen. QA then assesses it.

### 3.2 Propose a change control
1. On **Quality Events**, click **Propose change control**.
2. Enter **Title**, **What changes**, **Why** and the **Laboratory section** (or **Whole site**). The owner must be an active Section Head or QC Manager of the section; leave **Owner** empty to own it yourself. Click **Propose**.

### 3.3 Record the impact assessment of a change control
1. While the change is **Proposed**, use the **Affected records** panel to add the items, equipment, controlled documents, test definitions or specifications the change affects, or a new record by its planned code or name.
2. In the **Impact assessment** step, write the **Impact summary**, answer every checklist item (**Yes**, **No** or **Not applicable**, with an optional comment), and add the implementation tasks (description, responsible person, due date).
3. Click **Save assessment**. Then click **Sign impact assessment**. The list of affected records is fixed once you sign.

### 3.4 Investigate a deviation
1. When the deviation is at the **Investigation** step, the owner or a Section Head with *Quality Investigate* fills in **Impact assessment**, **Root cause category**, **Root cause** and **Proposed actions**.
2. Click **Save investigation**. All four fields are required to save.
3. A Section Head who is not the owner clicks **Sign laboratory conclusion** once the investigation is saved.

### 3.5 Findings and laboratory report for a complaint
1. When the complaint is at the **Investigation** step, record the **Findings** and, if needed, link the complaint samples with the sample search.
2. Click **Save investigation**, then have a Section Head who is not the owner click **Sign laboratory report**.

### 3.6 CAPAs
1. Create a CAPA from a deviation's closure step, or as a successor of a CAPA that was not effective. Enter **Title**, **Description** and **Owner**. This needs *Quality Investigate*.
2. As the CAPA owner, write the **CAPA plan**: actions (**Corrective** or **Preventive**, responsible person, due date), **What will be checked**, **How it will be checked** and **Planned date of the effectiveness check**. Click **Save plan** until QA approves it.
3. Complete the actions you are responsible for in the **Actions** panel. Describe the evidence (required) and, optionally, choose an evidence link.

### 3.7 Implementing master-data changes under an approved change control
1. When your laboratory requires a change control, the **Change control needed** dialog opens before you save a specification, test definition, test workflow step or stage replicate change.
2. In **Approved change control**, search for the change control or type its number. Only a change control that QA has approved for implementation, for your laboratory, that lists the record you are changing, is accepted.
3. Click **Save with this change control**. If it is refused, correct the number and click **Retry with this change control**. Click **Cancel** to stop; nothing is saved.
4. On the **Master Data Workbook** page, enter the same number in **Change control number (optional)** before you load the file.

### 3.8 Tasks, extensions, attachments and comments
- **Tasks:** Complete change-control tasks you are responsible for in the **Actions** panel, with evidence.
- **Extensions:** You can request a later due date for a record you own, or for an action you are responsible for. QA decides the request.
- **Attachments, links and comments:** Add links (while the record is open), upload attachments and post comments on any record you can read, if you hold *Quality Raise*. On a closed, rejected or cancelled record, links are blocked while comments and attachments remain allowed as a dated addendum (**Added after closure**). You can remove a link (before the first signed step) or void an attachment only on a record you own, or with *Quality Investigate*, which a Section Head holds by default.

## 4. Quality Configuration *(Draft — pending approval)*

- With *Quality Investigate*, you can edit **Checklist templates** for the laboratory sections you configure. Site-wide templates, classifications, due rules and the change-control gate are changed by QA.
- The checklist items of a change control are answered in its impact assessment (section 3.3).

## 5. Who to ask

- For a QA decision on a deviation, CAPA, change control or complaint, the request is with QA; the register shows the record's current step.
- For a change to a QA-only setting (classifications, due rules, change-control gate), ask QA.
