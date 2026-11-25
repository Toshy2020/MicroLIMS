# Quality Assurance (QA) Role Guide

> **Draft — not approved.** Written from code on `2026-11-13` (no screenshots; `documents/UserManual/roles/` had no prior role guides to copy from).

| Field | Value |
|---|---|
| Title | Quality Assurance (QA) Role Guide |
| Version | 0.3 |
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
