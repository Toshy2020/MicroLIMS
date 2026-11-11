# D2 — Deviation, CAPA, Change Control and Complaint: Design Specification

**Roadmap package:** D2 (`improvement-roadmap.md`)
**Status:** Approved by the user 2026-11-11
**Date:** 2026-11-11
**Depends on:** B4 (QA role). **Related:** D1 (OOS investigation), C5 (notifications), C7 (reasons on configuration changes).

## 1. Purpose

Give the laboratory and QA four linked quality records — deviation, CAPA, change control and complaint — each with a clear path, owners, due dates and signatures, and connect them to the laboratory records they come from.

## 2. Decisions (confirmed by the user, 2026-11-11)

| # | Decision | Answer |
|---|---|---|
| 1 | Existing QMS | None. Built in MicroLIMS |
| 2 | Types in the first release | Deviation, CAPA, change control, complaint |
| 3 | Approval and closure | QA approves and closes everything. The laboratory investigates and acts; the Section Head signs the laboratory part. The person who raised or owns a record cannot close it |
| 4 | Master-data changes | A laboratory can switch on "change control required" for specifications, tests and workflows. Off by default |

## 3. Out of scope

- Supplier quality, internal and external audits, a risk register, training records, recalls and regulatory reporting of complaints.
- A designer for new record types or new paths. The four types and their paths are fixed in this release; checklists, categories, classifications and due times are configurable.
- Emergency changes made first and approved afterwards.
- Replacing monitoring deviations of the monitoring plan. They stay and can be linked or escalated.
- Manufacturing-side investigation tools. Findings from outside the laboratory are entered as text and attachments.

## 4. Current state (from code, not run)

- No deviation, CAPA, change control or complaint record exists.
- The monitoring plan has its own small deviation record (missed occurrence, action limit exceeded) closed with a signature.
- Package D1 adds OOS investigations and lets QA record a "deviation request" on one; package B4 adds the QA role and audit trail review findings. Both wait for this package to become real records.
- The system records incidents of the software itself (error monitoring). Those are not laboratory deviations.
- Master data is changed in place; package C7 adds a required reason when a change affects open work.

## 5. Design

### 5.1 One engine, four types

All four are "quality records" sharing the same base: number, type, title, description, laboratory (optional — a record can concern the whole site), who raised it and when, owner, status, due date, classification, links, attachments, comments, and a timeline of signed steps.

- Numbers: `DEV-2026-0001`, `CAPA-2026-0001`, `CC-2026-0001`, `CMP-2026-0001`, counted per type and year, never reused.
- Every status change records who and when. Steps marked "signed" need an electronic signature (user and password) with a stated meaning. Signed content cannot be changed afterwards; later information is added as new dated entries.
- Any record can be **cancelled** only by QA with a reason and signature; it stays visible.

### 5.2 Deviation

| Step | Who | What |
|---|---|---|
| Raised | Anyone in the laboratory, or QA | What happened, when, where, what was affected, immediate action taken |
| Assessed | QA | Accept and classify (5.7), name the owner and due date — or reject as "not a deviation" with a reason (signed) |
| Investigation | Owner (laboratory) | Impact assessment (which samples, results, lots, equipment are affected — chosen from the system, 5.6), root cause (category and description), proposed actions |
| Laboratory conclusion | Section Head (signed) | Confirms the investigation |
| QA approval | QA (signed) | Approves, or returns with comments. Decides whether a CAPA is required |
| Closed | QA (signed) | Closure. If a CAPA is required, it must exist and its plan must be approved before closure |

### 5.3 CAPA

| Step | Who | What |
|---|---|---|
| Plan | Owner | Actions, each marked corrective or preventive, with a person responsible and a due date; and the **effectiveness check**: what will be checked, how, and when |
| Plan approved | QA (signed) | Or returned |
| Implementation | Each responsible person | Marks an action done with evidence (text, attachment or a link to a system record) |
| Completion verified | QA (signed) | All actions done and evidence acceptable |
| Effectiveness check | QA (signed), on or after the planned date | Effective, or not effective. "Not effective" requires a new linked CAPA |
| Closed | Automatic on "effective"; on "not effective" once the new CAPA exists | |

A CAPA can be raised from a deviation, a complaint, an OOS investigation, an audit trail review finding, or on its own.

### 5.4 Change control

| Step | Who | What |
|---|---|---|
| Proposed | Section Head, QC Manager or QA | What changes and why; what is affected, chosen from the system: items, specifications, tests, workflow steps, equipment, documents |
| Impact assessment | Section Head (signed) | A configurable checklist (for example validation, documents, training, stock, open samples) answered with comments; implementation tasks with owners and dates |
| Approved for implementation | QA (signed) | Or rejected with a reason |
| Implementation | Task owners | Each task done with evidence. Master-data changes made under this change are linked automatically (5.5) |
| Closed | QA (signed) | All tasks done; post-change review comment |

### 5.5 Change control for master data

- A setting per laboratory, off by default: **"Changes to specifications, tests and workflows need an approved change control."**
- When on: saving a change to a specification, a test definition or its workflow steps asks for a change control number. The server accepts it only if that record is "Approved for implementation", belongs to the same laboratory or the whole site, and **lists the item, test or specification being changed**. Otherwise the save is refused with the reason.
- The change is then linked to the change control and appears in its timeline with the audit entry of what changed.
- A change control cannot be closed while it lists a record that has not been changed, unless QA records why at closure.
- Creating new master data (a new item or test) follows the same rule when the setting is on.
- The reason required by package C7 is filled from the change control title and can be extended.

### 5.6 Links to laboratory records

A quality record can be linked to: samples, test orders, OOS investigations and excursion reviews, monitoring deviations, equipment, media lots, stock lots, items, specifications, tests, controlled documents and revisions, audit trail review findings, and other quality records.

- Links are chosen with the system's search, never typed as free text, and the linked record shows a "Quality records" panel in return.
- Reading a quality record does not give access to a linked record the user could not otherwise read; the link then shows only its number.
- **From D1:** when QA requests a deviation on an OOS investigation, the request now opens a deviation directly, pre-filled and linked; the request is marked addressed.
- **From the monitoring plan:** a monitoring deviation can be escalated to a quality deviation by QA or the Section Head; it stays as the source record.
- **From B4:** an audit trail review finding can be turned into a deviation.
- A link does not block the linked record. The one exception already exists in D1 (an open OOS investigation blocks its sample).

### 5.7 Classification, causes, due dates

- **Classification** for deviations and complaints: Minor, Major, Critical — set by QA. The names are configurable.
- **Cause categories** are shared with OOS investigations (package D1).
- **Due dates:** configurable per type and classification, in working days; empty until the site sets them. The owner can ask for an extension with a reason; QA approves or refuses it; every extension stays in the timeline.
- Overdue records and actions appear in My Work and in notifications, with escalation through the rules of package C5.

### 5.8 Complaint

| Step | Who | What |
|---|---|---|
| Received | QA | Source, date received, external reference, product and lot (chosen from the system where known), the complaint text |
| Laboratory investigation requested | QA | Classification, owner, due date, what to test (for example the retained sample) |
| Investigation | Owner (laboratory) | Linked samples and results, findings |
| Laboratory report | Section Head (signed) | |
| Conclusion | QA (signed) | Justified, not justified, or inconclusive; whether a deviation or CAPA is raised |
| Closed | QA (signed) | |

Complaint samples are received through the normal receiving process and linked; no testing rule changes.

### 5.9 Permissions and separation

| Permission | Who by default | Allows |
|---|---|---|
| `Quality.View` | Section Head, Reviewer, QC Manager, QA; analysts for records they raised, own, or have an action in | Read |
| `Quality.Raise` | All laboratory roles, QA | Raise a deviation; propose a change control (Section Head, QC Manager, QA only) |
| `Quality.Investigate` | Section Head; the named owner of a record | Investigation, plans, tasks, laboratory signature |
| `Quality.Approve` | QA only | Assess, classify, approve, verify, effectiveness, close, cancel, extensions, complaints |

- QA never performs the investigation or the actions. The laboratory never approves or closes.
- Nobody signs an approval or closure of a record they raised or own. The Section Head who signs the laboratory conclusion cannot be the owner's only reviewer if they are the owner themselves: a second Section Head or the QC Manager signs instead.
- For a deviation that comes from a result, the analyst who produced the result cannot be the owner.
- Laboratory scope applies. Site-wide records are visible to all holders of `Quality.View`.
- Without an active QA user, records can be raised and investigated but not approved or closed. The register says so.

### 5.10 Screens

- A "Quality" menu group: one register with tabs per type and filters (status, classification, laboratory, owner, overdue), and saved views (package C3).
- A record page: header, the path shown as steps with the current one marked, one section per step, actions list, links, attachments, comments, signed timeline.
- "Raise deviation" is available from a sample, a test, an equipment record, an OOS investigation and a monitoring deviation, pre-linking the source.
- A printable report (PDF) per record with all signatures: printed name, date and time with time zone, meaning.
- Dashboards: for QA — waiting for my decision, overdue, effectiveness checks due; for the Section Head — my laboratory's open records and actions.

### 5.11 Trending

Reports by type, classification, cause category, laboratory, product, source (OOS, excursion, complaint, other), time to close, on-time rate, CAPA effectiveness rate, and repeat causes (same category and product or equipment within a chosen period). Exportable.

## 6. Error handling

| Situation | Behaviour |
|---|---|
| Step attempted by the wrong role or by the raiser / owner | Refused with the reason |
| Closure with an unfinished action or missing required CAPA | Refused, naming what is missing |
| Master-data save without a valid change control (setting on) | Refused: which rule failed (not approved, wrong laboratory, record not listed) |
| Effectiveness check signed before its planned date | Refused |
| Two people edit the same step | The second save is refused; reload |
| Linked record not readable by the user | Shown as a number only |
| No QA user | Approval steps unavailable, with the reason |

## 7. Testing

- **Backend `[PostgresFact]`:** each type's full path with state, signature binding and audit at every signed step; every refusal in section 6; separation rules (raiser, owner, analyst of the result, Section Head as owner); numbering under concurrent creation; signed content immutable (database level); extension flow; deviation cannot close without required CAPA plan approved; CAPA "not effective" needs a new CAPA; change-control gate on specification, test and workflow-step saves — on and off, each refusal rule, automatic link, new master data; D1 deviation request opens a deviation and is marked addressed; escalation of a monitoring deviation; link visibility by scope; site-wide records; cancel.
- **Frontend (`userEvent`):** register tabs and filters; each step form; actions with evidence; signature dialogs with server meaning; refusals shown; raise from a source record with the link pre-filled; change-control prompt on a master-data save; report view; dashboards.
- **Manual:** one deviation from an OOS investigation through CAPA to effectiveness; one change control that changes a specification with the setting on; one complaint with a retained-sample test.

## 8. Delivery stages

1. Base record, numbering, links, attachments, timeline, permissions.
2. Deviation, including the links from D1, the monitoring plan and B4.
3. CAPA with effectiveness check.
4. Change control; then the master-data gate.
5. Complaint.
6. Due dates, extensions, notifications, dashboards.
7. Reports and trending.
8. Documents: requirements, manual chapters, SOP templates for deviation, CAPA, change control and complaint handling.

## 9. Decisions recorded 2026-11-11

1. A deviation can close once its required CAPA plan is approved; the CAPA then runs on its own.
2. Every laboratory role, including analysts, may raise a deviation.
3. Classifications: Minor, Major, Critical.
4. When the Section Head is also the owner, a second Section Head or the QC Manager signs the laboratory conclusion. QA's approval does not replace that signature. (My reading of "no need for QA"; to be corrected if wrong.)
5. Complaint fields: source, date received, external reference, product, lot, text.
6. Due times are in working days, set later in the system; empty until then.
