## 46. Quality Events

*(Draft — pending approval; written from the quality record screens and their server rules)*

Quality Events hold four kinds of record: **deviations**, **CAPAs** (corrective and preventive actions), **change controls** and **complaints**. Each record follows a fixed path of steps. Some steps are signed with your password and an electronic signature. The register lists every record you can read. The record page shows where the record is on its path, who has signed each step, and the linked records, actions, attachments, comments and due-date extensions. A change control that is approved for implementation can also be required before certain master-data changes are saved (section 46.12).

### 46.1 Quality Events Register
#### Purpose
Find quality records, filter them, and open one.

#### Who can use it
Requires the *Quality View* permission (`Quality.View`). By default this is held by Section Head, Reviewer, Analyst, QC Manager and QA. Users only see the records the server allows them to read: site-wide records and records in laboratory sections they may read. An Analyst who holds none of the wider quality permissions sees only the records they raised, own, or hold an action in.

#### What you see
- In the sidebar group **QUALITY & COMPLIANCE**, the entry **Quality Events** (`/quality`).
- Title **Quality Events** and the subtitle "Deviations, CAPAs, change controls and complaints with their approval path."
- Two buttons in the page header, shown only to users who may create the record:
  - **Propose change control** (requires *Quality Raise*, `Quality.Raise`).
  - **Receive complaint** (requires *Quality Approve*, `Quality.Approve`, which is QA).
- Five tabs: **All**, **Deviations**, **CAPAs**, **Change controls**, **Complaints**.
- A filter row with **Status**, **Classification**, **Section**, **Raised from**, **Raised to**, **Owned by me** (shown only when you are signed in), **Overdue only** and **Clear filters** (shown when a filter is set).
- The **Status** list changes with the tab, so it only offers the states that record type can have.
- A status line such as "Showing 25 of 60 records."
- A paged table with the columns **Number**, **Type**, **Title**, **Status**, **Classification**, **Section**, **Owner**, **Due date**, **Raised** and **Actions**. Overdue records show an **Overdue** badge next to their number. Each row has an **Open** button, and clicking the row also opens the record. The table shows 25 rows by default.

#### How to use it
1. Open **Quality Events** from the sidebar.
2. Choose a tab. **All** lists every type.
3. Set any filters you need. The table reloads with each change, and the page returns to the first page.
4. Click **Open** (or the row) to open the record.
5. To limit the list by date, use **Raised from** and **Raised to**. Both dates are read as whole days in UTC.

#### System behavior
- The status badge shows **Cancelled** for a cancelled record, whatever its underlying status.
- If the classification or section list cannot be loaded, a warning appears above the filters with a **Retry** option. The table still loads.
- A failed load shows "Failed to load quality records." with a **Retry** button.
- If nothing matches, the table shows "No quality records found."

#### Important rules
- The register reads its filters from the web address and keeps the address up to date when you change a filter. A link such as `/quality?overdue=true` or `/quality?type=Capa` therefore opens the register already filtered (section 46.8).

#### What happens next
Opening a record shows its page (section 46.2).

---

### 46.2 Quality Record Page
#### Purpose
Read one record, see its path and signed history, and take the next step on it.

#### Who can use it
Requires *Quality View* (`Quality.View`) to open the page. If you have only that permission, the page shows the notice "Read-only view: your role can see this record but not change it (Quality.View permission)." and hides the step forms.

#### What you see
From top to bottom:
1. **Back to Quality Events** and **Export PDF**. **Export PDF** downloads a PDF of the record, including its signatures.
2. The record number as the heading and its title underneath.
3. A notice if the record is cancelled, with the reason.
4. A summary panel with the type, status, classification and an **Overdue** badge where it applies. It shows **Section**, **Raised by** (name, and the date and time in UTC), **Owner**, **Due date** and the **Description**.
5. **Record path**: the steps of the record's type. Each step is marked **completed**, **current step** or **not started**. A completed step shows who completed it and when.
6. The step panel for the record's current step (sections 46.3 to 46.6 describe each type's steps). It shows only the step you can act on, or a message such as "Waiting for QA to assess this deviation." When nothing is left to do, it shows "No further steps are open on this record."
7. **Details**: the fields the record has recorded so far, such as a deviation's root cause and proposed actions, or a CAPA's effectiveness criteria. Fields not yet recorded show "Not recorded yet."
8. **Actions**: CAPA actions and change-control tasks, with **Complete** and **Request extension** buttons. For other types the panel says "This record has no actions."
9. **Due-date extensions**.
10. **Linked records**.
11. **Attachments**.
12. **Comments**.
13. **Signed Timeline**: every signed step, with the signer's full name, the date and time in UTC, and the meaning of the signature. It says "Nobody has signed this record yet." when there are none.

Each panel loads separately. If the links, comments or extension requests cannot be loaded, a banner names the panel that failed and has a **Retry** button. The rest of the page still works.

#### How to use it
1. Open the record from the register, from a source screen (section 46.7), or from a notification.
2. Read the summary and the record path to see the current step.
3. Use the step panel to take the step, if it is yours (sections 46.3 to 46.6).
4. Use the other panels as needed.

#### System behavior
- Each step is saved against the version of the record you loaded. If someone else changed the record first, the save is refused. Reload the record and check it again before you repeat the step.
- After each signed step, the page reloads the record, its path, its panels and its timeline.

#### Important rules
- A record cannot be approved or closed by the person who raised or owns it. The step panel then shows the reason the button is locked: "You raised or own this record, so you cannot approve or close it." The server applies the same rule.
- Signed steps appear in the Signed Timeline with the signer's name, the time in UTC and the meaning of the signature.

#### What happens next
The steps are described below for each type.

---

### 46.3 Deviation Path
#### Purpose
Record an event that departs from the expected way of working, investigate it, and close it.

#### Who can use it
- **Raise**: any user with *Quality Raise* (`Quality.Raise`), or a user who raises it from a source screen (section 46.7).
- **Assess, approve, close and cancel**: *Quality Approve* (`Quality.Approve`, QA).
- **Investigation (impact, root cause, proposed actions)**: the owner, or a user with *Quality Investigate* (`Quality.Investigate`) who is not QA.
- **Laboratory conclusion**: a user with *Quality LabConclude* (`Quality.LabConclude`, by default Section Head and QC Manager). The owner cannot sign it.

#### Path
Shown in the record path as **Raised**, **Assessed / Investigation**, **Laboratory conclusion**, **QA approved**, **Closed**.

#### How to use it
1. **Raised.** The deviation is recorded. QA opens the step **Assess the deviation**:
   - Choose a **Classification** and an **Owner**. Optionally set a **Due date**. If you leave it empty, the due time set for the classification is used.
   - Click **Accept and sign** and sign. The signature meaning is "Deviation assessed". The deviation moves to the investigation.
   - Or click **Reject as not a deviation**, give a reason, and sign ("Deviation rejected"). A rejected deviation is final.
2. **Investigation.** The owner (or a user with Investigate) fills in **Impact assessment**, **Root cause category**, **Root cause** and **Proposed actions**, then clicks **Save investigation**. All four fields are required to save.
3. **Laboratory conclusion.** Once the investigation is saved, a user with LabConclude who is not the owner clicks **Sign laboratory conclusion** and signs ("Laboratory conclusion signed"). The button is locked until the investigation is saved.
4. **QA approval.** QA chooses between:
   - **Approve and sign** ("Quality record approved"). QA may tick **A CAPA is required** and add a comment.
   - **Return to investigation**. A comment is required. The return is not signed, and the comment is kept on the record.
5. **Closure.** QA clicks **Close and sign** ("Quality record closed"). If a CAPA is required, the step offers **Create CAPA** (section 46.4). The server refuses the closure until that CAPA's plan is approved, and the refusal is shown in the signature dialog.
6. **Cancel record.** QA can cancel an open deviation up to the approved state. The cancellation needs a reason and a signature ("Quality record cancelled"). The record stays visible as cancelled.

#### System behavior
- Every action on the record must be finished before the deviation can close.
- A deviation raised from an OOS request or a monitoring deviation can be raised only once for that source. A second attempt is refused and the existing deviation is named.

#### Important rules
- QA cannot save the investigation. The investigation form shows that the record is with its owner.
- The signed steps are Assess, Reject, Laboratory conclusion, Approve, Close and Cancel.

#### What happens next
An approved deviation that needs a CAPA waits until that CAPA's plan is approved. QA then closes the deviation with a signature.

---

### 46.4 CAPA Path
#### Purpose
Plan, carry out, verify and check the effectiveness of a corrective or preventive action.

#### Who can use it
- **Create**: a CAPA is created from a deviation's closure step, or as a successor from a CAPA that was not effective. The person creating it needs *Quality Investigate* or *Quality Approve*. The owner is chosen in the dialog.
- **Plan**: the owner of the CAPA.
- **Plan approval, completion verification and effectiveness check**: QA.
- **Each action**: the responsible person named on that action.

#### Path
Shown in the record path as **Plan**, **Plan approved**, **Completion verified**, **Effectiveness checked / Closed**.

#### How to use it
1. **Create CAPA.** Enter a **Title** (up to 200 characters), a **Description**, and an **Owner**. The CAPA starts at the plan step.
2. **Plan (owner).** Add one or more actions. Each action has a **Kind** (Corrective or Preventive), a **Description**, a **Responsible person** and a **Due date**. Then fill in **What will be checked** (effectiveness criteria), **How it will be checked** (method) and **Planned date of the effectiveness check**. The planned date must be after the last action's due date. Click **Save plan**. The owner may save the plan again until QA approves it.
3. **Plan approval (QA).** QA clicks **Approve plan and sign** ("CAPA plan approved") or **Return plan to owner**. A comment is required to return the plan, and returning is not signed.
4. **Implementation.** Each responsible person opens the action in the **Actions** panel and clicks **Complete**. They describe what was done in **Evidence**, which is required, and may choose an evidence link from the record's linked records. Completing an action is not signed. An action can also show **Request extension** (section 46.9).
5. **Completion verification (QA).** Once every action is complete, QA clicks **Verify completion and sign** ("CAPA completion verified"). If an action is still open, the step names it.
6. **Effectiveness check (QA).** On or after the planned date, QA chooses **Effective** or **Not effective**, writes the **Findings**, and clicks **Sign effectiveness check** ("CAPA effectiveness checked"). The CAPA closes with either result.
   - For **Not effective**, a successor CAPA must exist first. Click **Create successor CAPA**; the new CAPA is created from this one, and the signature then names it.

#### System behavior
- The effectiveness check cannot be signed before the planned date. The step shows "The effectiveness check cannot be signed before its planned date (date)."
- Once a plan is approved, its planned fields are locked on the server, and actions cannot be removed.

#### Important rules
- The owner cannot approve the plan, verify completion or check the effectiveness. QA cannot own a CAPA or be the responsible person for one of its actions.
- QA can cancel a CAPA at any point before it is closed (section 46.2).

#### What happens next
A CAPA that was not effective leads to a successor CAPA, which follows the same path.

---

### 46.5 Change Control Path
#### Purpose
Plan a change to a specification, test, item, equipment or controlled document, assess what it affects, approve it for implementation, carry out the tasks, and close it.

#### Who can use it
- **Propose**: a user with *Quality Raise* (`Quality.Raise`) whose role is Section Head, QC Manager or QA; the server refuses anyone else. The owner named in the proposal must be an active Section Head or QC Manager of the laboratory.
- **Impact assessment, checklist and tasks**: a user with *Quality Investigate* (`Quality.Investigate`), by default the Section Head.
- **Approval and rejection**: QA.
- **Each task**: the responsible person named on that task.
- **Closure**: QA.

#### Path
Shown in the record path as **Proposed**, **Impact assessed**, **Approved for implementation**, **Closed**.

#### How to use it
1. **Propose change control** (from the register). Enter **Title**, **What changes**, **Why**, and the **Laboratory section** (or **Whole site**). Choose the **Owner**; if you leave it empty, you own it yourself. Click **Propose**.
2. **Affected records.** While the change is proposed, the **Affected records** panel lets you add existing items, equipment, controlled documents, test definitions or specifications, or record a new one by its planned code or name. The list is fixed when the impact assessment is signed.
3. **Impact assessment (Section Head).** Write the **Impact summary**, answer every checklist item (**Yes**, **No** or **Not applicable**, with an optional comment per item), and add the implementation tasks (description, responsible person, due date). Click **Save assessment** first. Then click **Sign impact assessment** ("Change impact assessed"). The signature stays locked until the assessment is saved, no edits are unsaved, and every checklist item has an answer.
4. **Approval (QA).** QA clicks **Approve and sign** ("Change control approved"), which moves the change to **Approved for implementation**, or **Reject** ("Change control rejected"), which needs a reason. QA can reject from either the proposed or the assessed state.
5. **Implementation.** Each task's responsible person completes the task in the **Actions** panel with evidence.
6. **Closure (QA).** Once every task is complete, QA writes the **Post-change review**. If an affected specification or test was not changed, QA must also give **Why affected specifications or tests were not changed**. QA then clicks **Close and sign** ("Quality record closed").

#### System behavior
- The change-control gate (section 46.12) accepts only a change control that is approved for implementation.
- Checklist answers can no longer be changed once the assessment is signed.

#### Important rules
- The owner cannot approve or close a change control they raised or own.
- The impact assessment is saved and signed by a user with Investigate, normally the Section Head. The owner named on the change must be a Section Head or QC Manager.

#### What happens next
An approved change control stays open until its tasks are done and QA closes it.

---

### 46.6 Complaint Path
#### Purpose
Record a complaint received from outside the laboratory, have the laboratory investigate it, and conclude it.

#### Who can use it
- **Receive**: QA (*Quality Approve*, `Quality.Approve`), from the register.
- **Request the laboratory investigation, conclusion and closure**: QA.
- **Findings and laboratory report**: the owner, or a user with *Quality Investigate* who is not QA. The laboratory report is signed by a user with *Quality LabConclude* who is not the owner.

#### Path
Shown in the record path as **Received**, **Investigation**, **Laboratory report**, **Concluded**, **Closed**.

#### How to use it
1. **Receive complaint** (from the register). Enter **Title**, **Source** (for example a customer or a regulator), **Date received** (cannot be in the future), **Complaint text** and the laboratory section (or **Whole site**). Optionally enter the **External reference**, the **Product** (searched from the items list), and the **Lot** as written in the complaint. Click **Receive**.
2. **Request investigation (QA).** Choose a **Classification**, an **Owner**, an optional **Due date**, and write **What to test**. Click **Request investigation**. This step is not signed.
3. **Laboratory investigation.** The owner records the **Findings** and can link the complaint samples with the sample search, which offers samples. Click **Save investigation**. A user with LabConclude who is not the owner then clicks **Sign laboratory report** ("Laboratory conclusion signed").
4. **Conclusion (QA).** Choose the **Outcome** (**Justified**, **Not justified** or **Inconclusive**), write the **Summary of the conclusion**, and click **Conclude and sign** ("Complaint concluded").
5. **Closure (QA).** Click **Close and sign** ("Quality record closed").

#### Important rules
- The conclusion does not record a follow-up deviation or CAPA. Raise one separately if it is needed (section 46.7).

#### What happens next
A concluded complaint is closed by QA.

---

### 46.7 Raising a Deviation from a Sample, Test, Equipment, OOS or Monitoring Screen
#### Purpose
Record a deviation at the point where it happens, with the affected item already linked.

#### Who can use it
- **Sample, test and equipment screens**: *Quality Raise* (`Quality.Raise`).
- **OOS deviation requests (on an OOS investigation)**: *Quality Approve* (QA).
- **Monitoring deviations (on the Monitoring Plan)**: *Quality Approve* or *Quality Investigate*.

#### Where you see it
- **Raise deviation** button on:
  - the selected sample panel in the Analyst Testing Workspace (linked to the sample),
  - each assigned test card in the Analyst Testing Workspace (linked to the test),
  - the equipment details dialog (linked to the equipment),
  - an OOS deviation request on an OOS investigation (linked to the request), and
  - an open monitoring deviation on the Monitoring Plan (linked to the monitoring deviation).
- A **Quality records** panel on the sample, test, equipment, OOS investigation and monitoring deviation screens lists the records already linked to that item. Each record has an **Open** button.

#### How to use it
1. Click **Raise deviation**.
2. Enter the **Title** and the **Immediate action**. Both are required.
3. For a deviation raised from a sample, test or equipment screen, also enter a **Description**. The OOS and monitoring versions do not ask for one.
4. Check **Occurred at (UTC)**. It defaults to the current time. An entered time is read as UTC.
5. For a deviation raised from a sample, test or equipment screen, optionally enter a **Location**.
6. Click **Raise deviation**. The message "Deviation DEV-… raised." appears, and the new record is listed under **Quality records**.

#### System behavior
- On the OOS and monitoring versions, the title is filled in: from the first 120 characters of the request's reason on the OOS screen, and from the monitoring deviation's number on the Monitoring Plan.
- A source (an OOS request or a monitoring deviation) can have only one open deviation. A second attempt is refused and the existing deviation is named.

#### Important rules
- A deviation raised from a sample, test or equipment screen is linked to that record. Its owner and classification are set later by QA in the Assess step (section 46.3).

#### What happens next
The new deviation starts at the **Raised** step (section 46.3).

---

### 46.8 Dashboard Tiles
#### Purpose
Show how many quality items need attention.

#### Who can use it
The quality tiles appear only to users with *Quality View* (`Quality.View`). They appear on the QA dashboard and on the Section Head dashboard.

#### What you see
- **QA dashboard** (titled **Quality Assurance Command Center**), in addition to its audit and document tiles:
  - **Waiting for my decision**: records at a step that needs a QA decision, for example a deviation's laboratory conclusion, a CAPA plan ready for approval, a change control assessed and ready for approval, or a complaint ready to conclude. For some steps, records you raised or own are not counted.
  - **My open records & actions**: open records you own or raised, plus open actions assigned to you.
  - **Overdue quality items**: open records and actions past their due date.
  - **Effectiveness checks due**: CAPAs whose effectiveness check is due (their planned date has been reached).
- **Section Head dashboard**:
  - **Lab open records & actions**: open records and actions in the laboratory sections you manage.
  - **My open records & actions**.
  - **Overdue quality items**.

#### How to use it
1. Click a tile to open the register.
2. **Overdue quality items** opens the register filtered to overdue records (**Overdue only** set).
3. **Effectiveness checks due** opens the CAPA tab.
4. **My open records & actions** opens the register filtered to records you own (**Owned by me** set).
5. **Waiting for my decision** and **Lab open records & actions** open the register without a filter, because the register has no matching filter.

---

### 46.9 Due Dates, Extensions and Overdue Notifications
#### Purpose
Keep track of due dates, ask for more time, and see when items are overdue.

#### Who can use it
- **Request an extension for a record**: the owner of the record.
- **Request an extension for an action**: the responsible person of the action. The button is shown on that action in the **Actions** panel.
- **Decide an extension**: QA.

#### How to use it
1. To request an extension, click **Request extension** (in **Due-date extensions** for the record, or on the action). Enter a **New due date**, which must be a later date, and a **Reason**, then click **Send request**.
2. A user with *Quality Approve* sees the request in **Due-date extensions** and clicks **Decide**.
3. Choose **Approve the extension** or **Refuse the extension**, optionally add a comment, and click **Sign and decide** ("Due-date extension decided").
4. An approved extension replaces the due date of the record or action.

#### System behavior
- A due date is set from the due rule for the record's type and classification, in working days. When there is no rule, there is no due date.
- An action or task counts as overdue only once its CAPA plan or change control has been approved.
- The overdue check runs about every 60 minutes by default. Administrators set the interval in the system configuration.
- Each person gets at most one overdue notification per item per laboratory day.

#### Important rules
- The person who asked for an extension cannot decide it.
- A decided extension request cannot be changed.

#### Notifications
Quality notifications appear in the notification list (chapter 09). None of them are sent by e-mail.
- **Record raised**: QA, users who can investigate in the section, and the owner, except the person who raised it.
- **Assigned**: the owner, when the owner is not the person who created the record.
- **Returned for revision**: the owner.
- **Awaiting QA decision**: QA, excluding the person who raised it and the owner.
- **Action assigned**: the responsible person.
- **Closed**: QA, users who can investigate in the section, the person who raised it, and the owner.
- **Rejected** and **Cancelled**: the person who raised it.
- **Overdue record**: QA and the owner.
- **Overdue action**: QA, the responsible person and the owner.

---

### 46.10 Attachments, Links and Comments
#### Purpose
Add supporting evidence and notes to a record.

#### Who can use it
- **Add a link, upload an attachment, add a comment**: *Quality Raise* (`Quality.Raise`).
- **Remove a link or void an attachment**: the record owner, or a user with *Quality Investigate*. The buttons are shown to every user with *Quality Raise*. If you are not one of those users, the server refuses the request.

#### How to use it
1. **Linked records**: click **Add link**, search for the record to link (type at least 2 characters), and choose it. You can link samples, tests, equipment, media lots, documents and items, among others.
2. **Attachments**: click **Upload** and choose a file. Accepted files are PDF, images, Office files, TXT or CSV, up to 10 MB each. Each upload is kept in the audit trail.
3. To void an attachment, click **Void**, give a reason, and confirm. The attachment stays on the record and is marked **Voided**, with the reason.
4. **Comments**: type in **Add a comment** (up to 2000 characters) and click **Post comment**.

#### System behavior
- Links can be removed only before the first signed step on the record.
- A voided attachment is never deleted. Its reason and date stay on the record.
- Comments cannot be edited or deleted after posting.
- Links, attachments and comments can be added to a cancelled or closed record.

---

### 46.11 Quality Configuration
#### Purpose
Set up checklists, classifications, due rules, and each laboratory's change-control gate.

#### Who can use it
The page (**Quality Configuration**, `/quality/config`) opens for users with *Quality Approve* (`Quality.Approve`, QA) or *Quality Investigate* (`Quality.Investigate`). QA can change every tab. A user with Investigate can edit checklist templates for the laboratory sections they may configure. The other tabs are read-only for them.

#### What you see
Four tabs: **Checklist templates**, **Classifications**, **Due rules** and **Section settings**.

#### How to use it
1. **Checklist templates.** Choose a **Record type** (Deviation, CAPA, Change control or Complaint) and a **Scope**: **Site-wide (default)** or a laboratory section. Add, edit, deactivate (**Active**) or remove items, then click **Save template**. A section template is used for that section instead of the site-wide one. A template with no items shows "No checklist items configured for this scope."
2. **Classifications.** Add a classification with a **Name**, an **Order** and an active flag, then click **Create**. Edit an existing one with **Save**. Only QA can change them. The seeded classifications are Minor, Major and Critical.
3. **Due rules.** Choose a **Record type** and a **Classification** (or **Default (no classification)**), enter the **Working days**, and click **Save rule**. Only QA can change them.
4. **Section settings.** Choose a **Laboratory section**, switch **Require an approved change control for master-data changes (change-control gate)** on or off, and enter a **Reason for change** (required). Click **Save gate setting**, then sign. The signature states that you are changing the change-control gate for this laboratory section. Only QA can change the gate.

#### System behavior
- The page shows the reason when a save is refused. If a section's setting cannot be loaded, **Reload setting** tries again.
- Changes to classifications, due rules and the gate are recorded in the audit trail.

#### Important rules
- Saving the site-wide checklist template needs *Quality Approve*. Saving a section template needs *Quality Investigate* and access to that section. On the templates tab, the **Save template** button is enabled for Investigate users even for the site-wide scope. The server refuses that save, and the refusal is shown on the page.
- The gate applies only to saves made after it is switched on; it does not change records already saved.

---

### 46.12 Change-Control Gate on Master-Data Saves
#### Purpose
Require an approved change control before certain master-data changes in a laboratory are saved.

#### Who can use it
- Any user who may configure the laboratory's master data can make a gated change, provided a change control is approved for it.
- Only QA can switch the gate on or off (section 46.11).

#### What is checked
With the gate on for a laboratory, the server checks a change control before these saves go through:
- specifications: create, edit, delete and reinstate, including specifications created in a batch, copied from another item, or imported through the Master Data Workbook,
- test definitions: create, edit, freeze and unfreeze,
- test workflow steps and the workflow type: create, edit, move and delete,
- stage replicates of a test: create, edit and delete.

Water and environmental monitoring limits, chemistry limits, HPLC and ICP methods, and the test assignments of items are not checked by the gate in this release. Deleting an item is refused when any of its specifications belongs to a laboratory whose gate is on, with the message "Delete or retire this item's specifications first; each specification change needs an approved change control."

#### How to use it
1. When the laboratory requires a change control, the **Change control needed** dialog opens before the save. If the setting cannot be read, the dialog opens when the server refuses the save. The dialog says: "This laboratory requires an approved change control for this change. Choose the change control that covers it before saving."
2. In **Approved change control**, search by number or title, or type the number. The number is capitals, digits and hyphens, up to 20 characters. Lower case is changed to capitals. The field shows the title of the chosen change control.
3. Click **Save with this change control**. The save then runs.
4. If the server refuses the number (for example it is not approved, belongs to another laboratory, or does not list the record you are changing), the dialog reopens with the reason under "The server refused the change". Correct the number and click **Retry with this change control**. Nothing is saved until you choose a number.
5. Click **Cancel** to stop. Nothing is saved.

#### System behavior
- The change control must be **Approved for implementation**, must not be cancelled, must list the record being changed, and must belong to the same laboratory.
- The number you chose is kept for the same record until the editing session ends.
- The list of approved change controls is a help. If it cannot be loaded, the field says so and you can type the number. The server still decides.
- When a gated change is saved, the matching entry in the change control's list of affected records is marked with the date and time of that change.
- If the gate is off for the laboratory, no dialog is shown and the saves run as before.

#### Workbook import
On the **Master Data Workbook** page, **Change control number (optional)** accepts the same number. The **Load and sign** button stays disabled while the number is not in a valid format. If the server refuses the import because the laboratory requires a change control, the page says "If the laboratory requires a change control, enter its number in the change control field and import the remaining rows again."

#### Important rules
- The gate never approves, rejects or closes a change control. Only the steps in section 46.5 do that.

---

### 46.13 Who Can Do What: Default Roles
The permissions below are the defaults for the built-in roles. Administrators can change roles, so check your own role's permissions in **Roles** if the screens differ from this section.

| Role | Quality View | Quality Raise | Quality Investigate | Quality LabConclude | Quality Approve |
|---|---|---|---|---|---|
| Section Head | yes | yes | yes | yes | no |
| Reviewer | yes | yes | no | no | no |
| Analyst | yes | yes | no | no | no |
| QC Manager | yes | yes | no | yes | no |
| QA | yes | yes | no | no | yes |

- QA does not investigate or sign the laboratory parts of a record. The server refuses approval or closure of a record that you raised or own (section 46.2).

---

> **Note for reviewers:** Users with *Quality Raise* see **Propose change control** and the **Void** and remove-link buttons. The server refuses void and remove unless the user is the owner or holds Investigate (section 46.10), but it does not check the role of the person proposing a change control, although the rules say only Section Head, QC Manager or QA may propose one (section 46.5). This is recorded as CONF-242 in `documents/Conflicts.md`. (GAP-912)
>
> **Note for reviewers:** The Save template button for site-wide checklist templates is enabled for Investigate users, although only Approve users can save that scope (section 46.11). (GAP-913)
