# D4 — Analyst Competency Gate: Design Specification

**Roadmap package:** D4 (`improvement-roadmap.md`)
**Status:** Approved by the user 2026-11-11
**Date:** 2026-11-11

## 1. Purpose

Make sure only qualified people perform a test: a signed, expiring qualification per analyst and test, plus a read and acknowledged current SOP of the item tested, checked by the server when work is assigned and when results are recorded.

## 2. Decisions (confirmed by the user, 2026-11-11)

| # | Decision | Answer |
|---|---|---|
| 1 | Qualified means | A signed qualification for the test (by the Section Head, with an expiry) **and** the item's current SOP read and acknowledged in Document Control |
| 2 | Where enforced | At assignment and at result entry |
| 3 | Expiry | Requalification interval per test, in months, set by the Section Head; empty = no expiry |
| 4 | Override | Section Head override for one test order, with reason and signature |

Roadmap decision D-6 is closed by 4.

## 3. Out of scope

- Training courses, quizzes and training records other than Document Control's.
- Competency for non-test work (media preparation, solution preparation, equipment calibration). A later release can reuse the same record.
- Qualifying reviewers and approvers.
- Proficiency-testing schemes with external providers.

## 4. Current state (from code, not run)

- Assignment checks only that the person is active, not locked, has an assignable role and belongs to the test's laboratory.
- **Result entry checks only laboratory membership.** Anyone in the laboratory can record a result on any test, assigned to them or not. The only "assigned to another analyst" check is in the batch media selection.
- Some workflows assign the acting user automatically at start (water, environmental monitoring, after cleaning, sample preparation).
- Document Control holds reading assignments per user and **document revision**, completed by acknowledgement. A new effective revision creates new reading assignments for earlier readers, with a due date (grace period, default 14 days); earlier ones become "superseded". Training has no expiry.
- An item's SOP is found by matching its SOP number to a controlled document; the current effective revision is known. Water, environmental monitoring and after-cleaning samples have no item and therefore no SOP link.
- No qualification record for people exists.

## 5. Design

### 5.1 Qualification record

- One record per analyst and test: basis (observed practice, proficiency sample, previous qualification, other), evidence (text and attachments, optionally a link to the sample used), qualified-on date, expiry date, signed by the Section Head of the test's laboratory with the meaning "Analyst qualified".
- The expiry date is calculated at signing from the test's requalification interval; empty interval = no expiry.
- The Section Head cannot qualify themselves; another Section Head of the laboratory or the QC Manager does.
- **Withdraw:** with reason and signature, effective immediately. **Requalify:** a new record; the old one stays in history.
- A record is never edited after signing.

### 5.2 SOP read

- For a sample with an item, the item's SOP is resolved as today. The analyst must have acknowledged the **current effective revision**.
- **New revision grace:** if the analyst acknowledged the previous revision and has an open reading assignment for the new one that is not yet past its due date, the analyst is still allowed. After the due date, not.
- If the item has no SOP number, the number is not found in Document Control, or there is no effective revision, the SOP part cannot be checked: the work is allowed, and the screens show "SOP not checked: {reason}". This is the same signal that already warns in Test Master (package C6).
- Samples without an item (water, environmental monitoring, after cleaning) are checked for the qualification only.

### 5.3 Turning it on

- A setting per laboratory, **off by default**: "Analysts must be qualified for the tests they perform." This lets each laboratory enter its qualifications before the gate starts refusing work.
- Before switching it on, the Section Head sees how many open test orders would become blocked and for whom.
- Switching it on or off needs a reason and a signature, and is audited.

### 5.4 Where the server checks

| Moment | Who is checked | Result when not qualified |
|---|---|---|
| Assigning an analyst to a test (single, bulk, retest plans) | The assignee | Refused with the reason |
| Automatic assignment at workflow start | The acting user | Refused with the reason |
| Recording anything on a test order (preparation, each workflow step, readings, results) | The person recording | Refused with the reason |
| Batch actions on many tests | Each person per test | That test is skipped with the reason; the others proceed |

- The check happens on the server in the same place as the existing laboratory check, so no screen or route can skip it.
- The reason names what is missing: "You are not qualified for TAMC (qualification expired on 3 Oct 2026).", "You have not acknowledged SOP-QC-014 revision 3.", and so on.
- Work already recorded is never undone when a qualification expires or is withdrawn. Only further recording is refused.

### 5.5 Override

- A Section Head of the laboratory can allow one named analyst to perform one test order without the qualification or SOP read, with a reason and signature ("Competency override").
- It covers that test order only, until the test is completed.
- It is shown on the test, on the review screen and in the sample's audit trail.
- The overriding Section Head may not review or approve that sample (added to the separation-of-duties rules).

### 5.6 Reminders and screens

- **Expiry reminders:** analysts and their Section Head are reminded a configurable number of days before a qualification expires (setting per laboratory, default empty = no reminder), through My Work and notifications.
- **Qualification matrix:** per laboratory, analysts by tests, each cell showing Qualified (with expiry), Expiring, Expired, Withdrawn or Not qualified, plus the SOP status for the tests' items. Filters and export.
- **Analyst view:** "My qualifications" with expiry dates and SOPs to read.
- **Assignment dialogs** list only qualified analysts first, and show unqualified ones disabled with the reason.
- **Test screens:** a refused action shows the reason as text beside the button (package C8 style).
- **Test Master:** a field for the requalification interval.

### 5.7 Permissions

| Permission | Default holders | Allows |
|---|---|---|
| `Competency.View` | Section Head, Reviewer, QC Manager, QA; analysts for their own | Read |
| `Competency.Manage` | Section Head, QC Manager | Sign and withdraw qualifications; set intervals and laboratory settings |
| `Competency.Override` | Section Head | Override for one test order |

## 6. Error handling

| Situation | Behaviour |
|---|---|
| Unqualified assignee | Assignment refused with the reason |
| Unqualified person records | Refused with the reason; nothing saved |
| Qualification expires during a test in progress | Further recording refused; Section Head can requalify or override |
| SOP cannot be resolved | Allowed; "SOP not checked" shown and recorded in the audit entry of the result |
| Override by the analyst themselves | Refused |
| Gate turned off | No checks; qualifications still recorded |

## 7. Testing

- **Backend `[PostgresFact]`:** qualification signed with expiry from the interval; self-qualification refused; withdraw; requalify keeps history; gate off → no refusals; gate on: assignment refused and allowed per case; every recording entry point refused for an unqualified user (one test per entry point); expired, withdrawn and missing qualification; SOP not acknowledged refused; grace period allowed until due date, refused after; unresolvable SOP allowed with the signal; samples without item checked for qualification only; batch skips; auto-assign refusal; override allows exactly one analyst on one test order and blocks the overrider from review and approval; recorded work unchanged after expiry; settings change audited with signature; state, audit and negative checks throughout.
- **Frontend (`userEvent`):** matrix states and filters; qualify and withdraw dialogs; assignment dialog ordering and disabled reasons; refusal reason beside actions; override dialog; impact preview before turning the gate on.
- **Manual:** turn the gate on in one laboratory with one qualified and one unqualified analyst and walk through assignment, recording, expiry and override.

## 8. Delivery stages

1. Qualification record, intervals, matrix (gate off).
2. Central check and its use at every recording point and at assignment.
3. SOP part with grace period.
4. Laboratory switch with impact preview.
5. Override and the review restriction.
6. Reminders and analyst view.
7. Documents.

## 9. Decisions recorded 2026-11-11

1. The gate is off by default per laboratory and switched on after qualifications are entered, with an impact preview.
2. After a new SOP revision, an analyst who read the previous one may continue until the new reading assignment's due date.
3. When no SOP can be resolved, the work is allowed and "SOP not checked" is shown and audited.
4. Reviewers and Section Heads who record results are checked like analysts.
