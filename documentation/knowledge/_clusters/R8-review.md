# R8 Domain and GMP review — C8 Reports, KPI, Document Control

Method: read C8 in full (screens, roles, workflows, business rules, gaps, audit trail); re-checked in code: TrendingTab fallbacks, OverviewTab chip, OverviewService deltas and failure fallback, KPI-deltas service, MediaGpt summary source, periodic-review role check, approval signature call, DC audit query permission, audit immutability triggers. Application not run.

## Phase 5 domain checklist

### MOD-REPORTS-001 Reports (Overview, Record Search, Media/GPT, Reference Strains, Trending)
| Checklist item | Status | Where | Note |
|---|---|---|---|
| KPIs on Overview (6 cards) use real counts | PARTIAL | SCR-REPORTS-001, GAP-801 | Values (counts) come from server aggregates; the arrows and percentages next to them are not period comparisons (verified OverviewService.ts L133-L177). |
| Overview "Live Database Data" chip | NOT IMPLEMENTED (as a status) | GAP-801 | Static chip, always shown (OverviewTab.tsx L120-L124). |
| Trend analysis (count tests) | DOCUMENTED | SCR-REPORTS-005, BR-REPORTS-002/003/005 | Below-detection readings use DL/2 in statistics. Statistics are sample SD (n-1). |
| Trend selector fallbacks | PARTIAL | GAP-800 | When server lists are empty, hardcoded test codes, product/item names and a default selection are shown (TrendingTab.tsx L44-L51, L217-L260). |
| Qualitative trending | PARTIAL | SCR-REPORTS-005 | Event table only; display choice per test REQUIRES CONFIRMATION. |
| Failed load shows an error | PARTIAL | GAP-817 | Overview failure shows zeros ("no data"); point drill-down builds a record from the chart point. |
| SLA / overdue logic | NOT APPLICABLE in Reports | see KPI | Overdue appears on dashboards (R1) and KPI tiles. |
| Exports (CSV server, PDF client) | PARTIAL | WF-REPORTS-001, BR-REPORTS-004, GAP-804 | Server CSV: 10,000-row cap, export audit entry (user, filters, row count, type). Client PDF and trend exports: no audit entry found (REQUIRES CONFIRMATION). Cap is not warned before confirming. |
| Scope by laboratory section | DOCUMENTED | roles | All signed-in users; data limited to the user's sections; System Administrator unrestricted. |
| Spec / acceptance criteria for Reports | REQUIRES CONFIRMATION | WF-REPORTS-001 | No URS/FRS found for Reports. |

### MOD-REPORTS-002 Reporting backfill
| Item | Status | Where | Note |
|---|---|---|---|
| Rebuild of the reporting table | NOT IMPLEMENTED in UI | GAP-803 | Endpoint only (`Reporting.Admin`). Reports read this flattened table, not the source tables. |

### MOD-KPI-001 KPI and Workflow Profiles
| Item | Status | Where | Note |
|---|---|---|---|
| 8 headline KPIs and formulas | DOCUMENTED | BR-KPI-005..016 | Definitions are implemented behaviour; design file is a draft (GAP-045), approved definitions REQUIRES CONFIRMATION. |
| On-time all stages excludes FP physicochemical, EM, retest clones | PARTIAL | BR-KPI-006, GAP-805 | Excluded with gap chip. |
| Right first time excludes observation (pathogen) tests | PARTIAL | BR-KPI-007, GAP-806 | |
| Default targets (7 days; 24 h review/approval gate; 4 h reading grace) | REQUIRES CONFIRMATION | BR-KPI-006/011/013 | Values are code constants; approved SLAs not found in project documents. |
| OOS rate depends on sample field, not an investigation decision | PARTIAL | BR-KPI-009 | Spec/code differ. |
| Workflow profile versioning (append-only, signed) | DOCUMENTED | WF-KPI-002, BR-KPI-003 | DB trigger refuses edits of Effective rows. |
| Second-person check for profile changes | NOT IMPLEMENTED | GAP-820 | Same user adds and signs. |
| Capacity settings and analyst availability | NOT IMPLEMENTED in UI | GAP-802 | Workload uses defaults (6 h, Mon-Fri, 5-day horizon); table says so. |
| Workload figures are planning estimates, not performance scores | DOCUMENTED | SCR-KPI-001 | |

### MOD-DC-001 Document Control: documents, versions, approval
| Item | Status | Where | Note |
|---|---|---|---|
| Register, draft, upload, review, approve, supersede | DOCUMENTED | WF-DC-001/002, BR-DC-001..012 | |
| Technical review findings ladder | DOCUMENTED | BR-DC-009/010 | Review decisions are not e-signed. |
| Approval with password re-entry | DOCUMENTED | BR-DC-011 | Uses the shared signature service (meaning Approved), same throttle. Return and decline need no password. |
| Segregation of duties (author, reviewer, approver) | DOCUMENTED | roles MOD-DC-001 | No administrator exemption. Not checked: reviewer vs periodic reviewer; registrant vs later steps. |
| Document file integrity (SHA-256 at upload; re-check on every read) | DOCUMENTED | BR-DC-006 | Delivery blocked on mismatch; security audit event. |
| Word-to-PDF conversion | NOT IMPLEMENTED | GAP-815 | Controlled PDF is uploaded by hand. |
| Retirement (Obsolete) | NOT IMPLEMENTED | GAP-818 | "Obsolescence Recommended" cannot complete; UI text says approval will decommission. |
| Void master | PARTIAL | WF-DC-007, BR-DC-012 | Section Head only; System Administrator refused; not signed; refusal text says "Document Controller". |
| Permissions Documents.DraftEdit/RevisionCreate/Review/Approve/PeriodicReview | NOT IMPLEMENTED (unenforced) | roles MOD-DC-001 | Granting or revoking changes nothing; authority follows role type and per-document assignment. |
| Registration refusal text vs rule | PARTIAL | WF-DC-001 | Analyst holds Documents.Register by default but message names Document Controller / System Administrator. |
| Owner entered as user number | PARTIAL | GAP-821 | |
| Periodic review | PARTIAL | WF-DC-003, GAP-814 | Not signed; no role check when no reviewer assigned (verified PeriodicReviewService L553-L561). |
| Dashboard counts first 100 masters; may fail for Analysts | PARTIAL | GAP-811/812 | |
| Dual PDF comparison | PARTIAL | GAP-808 | Side-by-side only, no diff highlighting. |

### MOD-DC-002 Reading and training evidence
| Item | Status | Where | Note |
|---|---|---|---|
| Acknowledgement record (statement, file hash, time, IP, user agent) | DOCUMENTED | WF-DC-004, BR-DC-014 | Immutable record; DB trigger on acknowledgement table (migration 20260904160000). |
| Acknowledge requires reading | NOT IMPLEMENTED | GAP-810 | Checkbox only; scroll progress informational. No password or signature. |
| Training assignment on approval | PARTIAL | GAP-816 | Only on worker activation of FutureEffective; immediate approvals create none. |
| Role curriculum maintenance | NOT IMPLEMENTED in UI | GAP-819 | Department ignored. |
| Training matrix visibility | DOCUMENTED | roles MOD-DC-002 | Whole matrix only with Documents.TrainingAssign; `TrainingViewMatrix` unenforced; compliance figures visible to every signed-in user. |
| Escalations | NOT IMPLEMENTED in UI | GAP-807 | Records only; no message sent. |
| Training settings screen | NOT IMPLEMENTED | GAP-809 | |

### MOD-DC-003 / 004 / 005
| Item | Status | Where | Note |
|---|---|---|---|
| DC audit trail search + CSV export (5,000 cap, export itself logged) | DOCUMENTED | BR-DC-016 | Role types Reviewer, Section Head, System Administrator only. |
| Category filter matches server values | PARTIAL | GAP-813 | |
| Configuration (types, departments, sections, numbering, settings) | DOCUMENTED | WF-DC-006 | Needs Documents.ConfigManage AND System Administrator role type; audited; no reason field, no signature. |

## Phase 6 GMP / data-integrity facts

### MOD-REPORTS-001 / 002
- Reports are read-only; no screen edits results [WF-REPORTS-001].
- The server-built CSV exports write an audit entry with user, filters, row count and export type [audit-trail]. PDF exports built in the browser and the trend exports are not recorded in the audit trail as far as the code shows (REQUIRES CONFIRMATION).
- Reports read a derived table (ResultRecord); the data shown is only as current as its last projection; the backfill that rebuilds it has no screen [MOD-REPORTS-002, GAP-803].
- Below-detection results are shown as DL/2 in trend statistics; the stored value and reported text are unchanged [BR-REPORTS-002].
- Overview arrows/percentages are not trend comparisons [GAP-801]; a failed overview load shows zeros [GAP-817].
- Approval status in reports is derived from sample status [BR-REPORTS-001].

### MOD-KPI-001 Controlled changes to KPI configuration
- A workflow profile version (work units, target days) is saved only with the author's password, meaning "MasterDataChanged"; the version, signature and audit entry are written in one transaction [WF-KPI-002].
- Versions cannot be edited or deleted once Effective (database trigger) [BR-KPI-003].
- The same person may add and sign a version; no second approval [GAP-820].
- Section capacity and analyst availability changes are audited with a mandatory reason at API level but have no screen [MOD-KPI-002].
- Without `Kpi.ViewTeam` a user sees only their own workload row; headline tiles remain laboratory-level [roles MOD-KPI-001].

### MOD-DC-001 Document approval, versions, status control
- Approval of a document revision requires the approver to re-enter their own password; the system writes an electronic signature row (name, username, role at the time, meaning Approved, time, IP address, comment) and sets the revision status in the same save [BR-DC-011].
- The system refuses approval by the revision's author or by any technical reviewer of that revision; System Administrator is not exempt [roles MOD-DC-001].
- Technical review completion, return, findings, periodic review, void, draft cancellation and document configuration changes are not electronically signed; each writes an audit entry with the acting user (reason where entered) [audit-trail].
- Reasons of at least 10 characters are required for void, cancel draft, approver return/decline, periodic review summary [WF-DC-003/007].
- Only one revision per master can be in Draft, In Review or Awaiting Approval [BR-DC-004].
- A revision's controlled files are hashed on upload; the hash is re-verified on every view/download and delivery is blocked on mismatch with a security audit entry [BR-DC-006].
- A voided master is hidden from users other than System Administrator and Section Head [roles].
- No function sets a revision to Obsolete [GAP-818]; an Effective document cannot be retired in the application.
- Database triggers refuse UPDATE/DELETE on AuditLogs, AuditEventChanges and ElectronicSignatures (migration 20260902181230); presence in the live database was not checked.
- Periodic review completion without an assigned reviewer has no role check [GAP-814].
- Document-control permissions DraftEdit, RevisionCreate, Review, Approve, PeriodicReview do not control access [roles MOD-DC-001]; role edits to them change nothing.
- Dialog text in the approval flow refers to 21 CFR Part 11; the guide describes the function only [GAP-023].

### MOD-DC-002 Reading and training evidence
- An acknowledgement stores the statement text, controlled-file SHA-256, time, IP address and user agent in an immutable record; only the assigned user can acknowledge [BR-DC-014, roles].
- Acknowledgement needs a ticked checkbox only; no password, no minimum reading time or progress [GAP-810].
- Reading assignments are produced when the worker activates a future-dated revision; none are produced when approval is effective immediately [GAP-816]. Old assignments of the superseded revision are then not closed.
- The compliance dashboard figures are visible to every signed-in user; the full matrix needs `Documents.TrainingAssign` [roles].

### Result modification, rework, retest, investigation, session security
- Not in C8 scope (see C3, C1). Session handling: R1.

## Mock / placeholder / unverified data
1. TrendingTab (SCR-REPORTS-005): when the server returns no tests, products or categories, the screen offers built-in test codes (TAMC, TYMC, WATER_TAMC, PATHOGEN_ECOLI) and five built-in product/material/water/point names; initial selection is a built-in test code and product name; these are not from the user's data and may not exist in Test Master (GAP-800, verified).
2. OverviewTab: static chip "Live Database Data" (always displayed); header text says "aggregated directly from live laboratory ResultRecords" (GAP-801, verified).
3. OverviewService pseudo-deltas: "Total Tests" always +100 % when any exist; "Pending Review/Approval" 100 % when any exist; "Approved Results" shows the approved share as a delta; "Out of Spec" and "Alert/Action" show the count as a percent. Comparison captions read "live database records", etc. No prior-period comparison exists (GAP-801, verified).
4. OverviewService failure branch: zeros with caption "no data" and no error (GAP-817, verified).
5. Recent Results "Entered By" shows "Analyst" and location shows "Other" when the name is missing; PDF header shows "Authorized User" when the user name is unavailable (added to GAP-817 in Phase 5, verified).
6. TrendingTab point drill-down builds a record from the chart point when the record cannot be loaded (GAP-817).
7. KPI: default targets and constants used where no profile exists (7-day target, 24 h gates, 4 h reading grace, 6 h/day, Mon-Fri) are flagged on screen by gap chips; their approval status is unknown (REQUIRES CONFIRMATION).
8. Dashboard (Document Control) counts use the first 100 masters (GAP-812).
9. Not mock: Media/GPT summary and Reference Strains tables are server-aggregated (MediaGptReportTab calls the summary endpoint). No random or demo data found in modules/reports or modules/kpi.

## Guide callouts
1. Reports > Overview: the "Live Database Data" label is fixed text; the arrows and percentages on the six cards are not comparisons with a previous period. Do not read them as trends. (GAP-801)
2. Reports > Overview: if loading fails the cards show 0 with "no data" and no error; a zero can mean a failed load. (GAP-817)
3. Reports > Trending: if the lists are empty the screen offers built-in example test codes and product names and starts with a default selection; confirm the Test and Product/Item are really yours before using the chart. (GAP-800)
4. Reports > Trending: clicking a point may show a record rebuilt from the chart point if the real record cannot be loaded; open the sample from Record Search to confirm. (GAP-817)
5. Recent Results shows "Analyst" and "Other" when a name or location is missing. (GAP-817)
6. Reports > Record Search CSV export stops above 10,000 rows and the confirmation does not warn first; PDF and trend exports are produced in the browser and are not recorded in the audit trail as far as the code shows. (GAP-804)
7. Reports depend on a derived results table; the administrative rebuild has no screen. (GAP-803)
8. KPI: tiles exclude samples without a preparation record (finished-product physicochemical, environmental monitoring, retests) from "On time, all stages" and pathogen observation tests from "Right first time"; targets default to 7 days where none is set; the design of these KPIs is not formally approved. (GAP-805, GAP-806, GAP-045)
9. KPI capacity assumes 6 h/day, Monday-Friday; section settings and leave cannot be entered in the application. Workload units are a planning estimate, not a performance score. (GAP-802)
10. KPI workflow profile versions can be created and signed by the same person. (GAP-820)
11. Document Control: effective documents cannot be retired; "Obsolescence Recommended" in periodic review cannot be completed, and the approval screen's "decommission" wording is not backed by a status change. (GAP-818)
12. Document Control: the controlled PDF must be uploaded by hand; there is no automatic Word-to-PDF conversion. (GAP-815)
13. Document Control: a reading acknowledgement is a ticked checkbox only; scrolling the PDF is not enforced. (GAP-810)
14. Document Control: reading assignments are not created when a revision is approved with an effective date of today or earlier; check the training matrix. (GAP-816)
15. Document Control: role curricula, per-document training settings and training escalations have no screen; the curriculum department is not applied; escalations send no message. (GAP-807, GAP-809, GAP-819)
16. Document Control permissions DraftEdit, RevisionCreate, Review, Approve and PeriodicReview have no effect; authority follows role type and per-document assignment. Void is Section Head only. (GAP-015)
17. Document Control: periodic review with no assigned reviewer has no role check. (GAP-814)
18. Document Control dashboard counts cover only the first 100 documents and may show an error for users who cannot query the audit trail. (GAP-811, GAP-812)
19. Document Control audit filter lacks Review, Approval and Training categories and offers two names the server does not have. (GAP-813)
20. Approval and acknowledgement dialogs contain regulatory wording; it describes screen text, not a validation statement. (GAP-023)
21. Document owner is entered as a user number at registration. (GAP-821)

## Corrections made to the cluster file
- GAP-817 (corrected in Phase 5): added verified fallbacks "Analyst" (entered-by), "Other" (location) in OverviewService.ts L104, L127 and "Authorized User" in PDF headers.
- No other factual errors found: GAP-800, GAP-801, GAP-814, GAP-818 and the approval signature call (DocumentApprovalService L550-L564) were re-verified against code.
- Note for the C1 review: C8's audit-trail immutability statement is supported by migration 20260902181230 (AuditLogs, AuditEventChanges, ElectronicSignatures).
