# Quality Assurance (QA) Role Guide

> **Draft — not approved.** Written from code on `2026-11-13` (no screenshots; `documents/UserManual/roles/` had no prior role guides to copy from).

| Field | Value |
|---|---|
| Title | Quality Assurance (QA) Role Guide |
| Version | 0.5 |
| Status | Draft — pending approval |
| Author | AI coding agent, draft from code |
| Reviewer | |
| Approver | |
| Effective date | |

## 1. What QA can see and do

- **Dashboard (`Quality Assurance Command Center`):** When you sign in as QA, **Dashboard** opens three summary tiles: **Audit trail reviews in progress**, **Open findings**, and **Documents waiting for my approval**.
- **Read-only oversight across all laboratories:** QA holds `Sections.ViewAll` and has no laboratory section membership, so the section filter does not hide other laboratories from you. What you can open still depends on the view permissions granted to the QA role (sample tracking, laboratory and review dashboards, KPIs, monitoring plan, OOS tracking, audit trail, security audit).
- **OOS investigation oversight and deviation requests (`Oos.View`, `Oos.RequestDeviation`):** *(Draft — pending approval)* View OOS investigations and excursion reviews across all sections and submit a **Request Deviation** on an investigation in any status. Open deviation requests do not block investigation conclusion; a Section Head addresses them in the investigation page.
- **Audit trail reviews:** Start, inspect, record findings on, close findings on, export PDFs for, and electronically sign periodic audit trail reviews (**Audit Trail Reviews**).
- **Access reviews:** Generate/view periodic user access reviews, record or update findings, and electronically sign reviews (**Access Review**).
- **Controlled documents:** Read controlled documents, review drafts when assigned, and approve revisions when assigned (**Document Control**).
- **Quality events (`Quality.View`, `Quality.Approve`):** *(Draft — pending approval)* Receive complaints, assess and approve deviations, approve and verify CAPAs, check CAPA effectiveness, approve and close change controls, conclude complaints, decide due-date extensions and cancel records. See section 6.
- **Quality dashboard tiles (`Quality.View`):** *(Draft — pending approval)* The QA dashboard also shows **Waiting for my decision**, **My open records & actions**, **Overdue quality items** and **Effectiveness checks due**. **Waiting for my decision** opens the **Quality Events** register without a filter. **My open records & actions** opens it filtered to records you own (`owner=me`), **Overdue quality items** to overdue records (`overdue=true`), and **Effectiveness checks due** to CAPAs (`type=Capa`).
- **Analyst competency oversight (`Competency.View`):** *(Draft — pending approval)* View the **Qualification Matrix** across laboratories (`Sections.ViewAll`) to inspect analyst test qualifications, expiry dates, and current effective SOP codes/revisions. QA cannot qualify or withdraw analyst qualifications (`Competency.Manage`), toggle the laboratory competency gate, or grant competency overrides (`Competency.Override`).

## 2. What QA cannot do (and why)

- **No laboratory execution or laboratory approval:** QA cannot receive samples, enter or correct test results, prepare media or solutions, review or approve samples, release media/materials, or close OOS investigations.
- **No system or master-data administration:** QA cannot create or edit users, change roles or permissions, manage system settings, or edit laboratory master data.
- **No Section Head Document Control overrides:** QA can approve assigned revisions, but cannot use Section Head override actions in Document Control.
- **Why (independence):** MicroLIMS enforces segregation of duties on the server so quality oversight stays independent of both laboratory testing/approval and system administration. Any write action that requires section membership or a forbidden permission is refused.

## 3. How to review the audit trail and sign

1. Open **Audit Trail Reviews** from the sidebar.
2. Click **Start review**, enter a **Title**, **Period from** / **Period to** (not in the future), and **Scope** (**All**, **Record type**, or **Record**). MicroLIMS fixes the cut-off audit ID and entry count at start; entries written afterwards stay outside the review (`N newer entries exist after this review's cut-off`).
3. Inspect entries in the paged table, click **Mark as finding** on any entry of concern, enter a note (1–2000 characters), and save. Open findings can later be closed with a comment.
4. Only the user who started the review can sign it. Click **Sign review**, enter your **Conclusion** (1–2000 characters) and your account password (`Audit trail review` meaning), and submit. Once signed, the review cannot be edited or deleted; use **Export PDF** to download the signed report.

## 4. How to decide and sign an access review

1. Open **Access Review** from the sidebar and open a draft review (or generate one if needed).
2. Filter the user snapshot (for example by active/inactive status or flagged accounts with dormant/never-logged-in/SoD conflict markers) and record findings for any user whose access needs correction. Note: changing a user's account or role is done by a System Administrator in **Users** or **Roles**, never inside **Access Review**.
3. Click **Sign**, enter your conclusion and password, and submit. Once an active, unlocked QA user (`Access.Review.Sign`) exists, administrators cannot decide or sign access reviews. You cannot sign a review that concerns your own access (a finding about your account, or your account flagged in the report); another reviewer must sign it.

## 5. How to approve a controlled document

1. Open **Document Control** -> **Document Library** (or click **Documents waiting for my approval** on the dashboard).
2. Open the assigned document revision, review its metadata, content, and prior review records.
3. Record your approval decision with your password as an electronic signature. You cannot approve a revision if you are its author or reviewer. For document types configured with **Requires a Quality Assurance approver**, at least one assigned QA approver must approve before the revision can become effective.

## 6. Quality events *(Draft — pending approval)*

QA approves, verifies, closes and cancels quality records. QA does not investigate, and QA does not sign laboratory conclusions or laboratory reports. Each signed step asks for your password and states its meaning. The owner and the raiser of a record cannot approve, close or cancel it, and the server refuses such a request.

### 6.1 Find and open a record
1. Open **Quality Events** from the **QUALITY & COMPLIANCE** group in the sidebar.
2. Choose a tab (**All**, **Deviations**, **CAPAs**, **Change controls**, **Complaints**) and set filters if needed. Click **Open** on a row.
3. The record page shows the path, the current step, the signed timeline and the other panels.

### 6.2 Receive a complaint
1. On **Quality Events**, click **Receive complaint**.
2. Enter **Title**, **Source**, **Date received** (not in the future), **Complaint text** and the laboratory section (or **Whole site**). Optionally enter the external reference, the product and the lot. Click **Receive**.
3. On the complaint page, choose **Classification**, **Owner**, an optional **Due date** and **What to test**, then click **Request investigation** (not signed).
4. After the owner's findings are signed by the laboratory, choose **Outcome** (**Justified**, **Not justified** or **Inconclusive**), choose a **Follow-up deviation or CAPA** (required when **Justified**), write the **Summary of the conclusion** and click **Conclude and sign**. Then click **Close and sign**.

### 6.3 Assess and approve a deviation
1. When a deviation is **Raised**, open the **Assess the deviation** step. Choose **Classification** and **Owner**, optionally a **Due date**, then click **Accept and sign**. To reject it as not a deviation, click **Reject as not a deviation**, give a reason and sign.
2. When the laboratory conclusion is signed, open the **QA approval** step. Click **Approve and sign** (tick **A CAPA is required** if one is needed) or **Return to investigation** with a comment.
3. When the deviation is **Approved**, click **Close and sign**. If a CAPA is required, the closure waits until its plan is approved; use **Create CAPA** from the step to raise it.

### 6.4 Approve, verify and check a CAPA
1. In the **CAPA plan** step, read the plan. Click **Approve plan and sign**, or **Return plan to owner** with a comment.
2. While responsible people complete the actions, watch the **Actions** panel. When every action is complete, click **Verify completion and sign** in the **Completion verification** step.
3. On or after the planned date, open the **Effectiveness check** step. Choose **Effective** or **Not effective**, write the **Findings** and click **Sign effectiveness check**. For **Not effective**, first click **Create successor CAPA**; the signature then names it.

### 6.5 Approve, reject and close a change control
1. In the **Approval** step (after the Section Head has signed the impact assessment), click **Approve and sign** to approve the change for implementation, or **Reject** with a reason.
2. When every task is complete, write the **Post-change review** in the **Closure** step. If a listed specification or test was not changed, give the reason in **Why affected specifications or tests were not changed**. Click **Close and sign**.

### 6.6 Decide a due-date extension
1. On the record page, the **Due-date extensions** panel lists requests. Click **Decide** on a pending request.
2. Choose **Approve the extension** or **Refuse the extension**, add an optional comment, and click **Sign and decide**. Approval replaces the due date.

### 6.7 Cancel a record
1. In the step panel of an open record, click **Cancel record**.
2. Confirm, then enter the reason and your password. The record stays visible as cancelled.

### 6.8 Change control gate and quality configuration
1. Open **Quality Configuration** (sidebar, **QUALITY & COMPLIANCE**).
2. To switch the change-control gate for a laboratory, open **Section settings**, choose the **Laboratory section**, switch **Require an approved change control for master-data changes (change-control gate)**, enter a **Reason for change**, click **Save gate setting** and sign.
3. **Classifications** and **Due rules** are also changed here by QA only.

### 6.9 What QA cannot do
- Investigate a deviation or complaint, or sign the laboratory conclusion or the laboratory report.
- Own a CAPA, or be the responsible person for one of its actions.
- Approve, verify, close or cancel a record that you raised or own.
- Decide an extension that you requested, or an extension on a record you raised or own.
