# F6 — Review by Exception: Design Specification

**Roadmap package:** F6 (`improvement-roadmap.md`)
**Status:** Draft for user review
**Date:** 2026-11-11
**Uses (when built):** E1 (value source), D4 (competency overrides), F2 (photos added later).

## 1. Purpose

Show the reviewer first what needs attention, require them to confirm each item, and keep everything else one click away, so review is faster without becoming lighter.

## 2. Decisions (confirmed by the user, 2026-11-11)

| # | Decision | Answer |
|---|---|---|
| 1 | Confirmation | Each exception has a "Seen" tick with optional comment; review cannot be completed until all are ticked; the ticks are part of the review record |
| 2 | Exception types | Values typed by hand or re-entered; incubation timing; materials and equipment at use; results and history |
| 3 | Bulk review | No; one sample at a time with its own signature, as today |

## 3. Out of scope

- Changing what review requires (all tests ready, separation of duties, signature).
- Automatic review or skipping review for exception-free samples.
- New rules about what is acceptable; the exceptions show facts, the reviewer judges.

## 4. Current state (from code, not run)

- Review happens in the sample summary dialog: every test is shown in full with its results, readings, limits and incubations; the reviewer signs once per section.
- The data needed for most exceptions exists but is not shown: earlier replaced results and readings, audit entries with reasons, return-to-analyst events, incubation timing and minimum-duration overrides, the media lot and incubator used.
- Material expiry and equipment calibration **at the time of use** are not shown; they can be worked out from the lot and equipment records and the time of use.
- The reviewer queue ranks by out-of-specification and age only.

## 5. Design

### 5.1 Exceptions computed by the server

For each section under review, the server lists exceptions per test:

| Group | Exception | Shown with |
|---|---|---|
| Values | Value typed by hand on an instrument-connected field (E1) | Reason given, instrument selected |
| Values | Result or reading entered more than once (earlier entry replaced) | Every earlier value, who, when, reason |
| Values | Any audit entry with a reason on the test's records (corrections, overrides) | The audit entry |
| Incubation | Read before or after the configured window | Window, actual time, difference |
| Incubation | Minimum duration overridden | Who, when, reason |
| Materials and equipment | Medium, reagent, solution or standard expired at the time of use | Lot, expiry, time of use |
| Materials and equipment | … expiring within N days of use (setting; empty = off) | Lot, expiry, time of use |
| Materials and equipment | Equipment past its calibration due date at the time of use | Equipment, due date, time of use |
| Results and history | Alert, action or out-of-specification result | Value, limit, level; link to OOS investigation if any |
| Results and history | Test returned to the analyst before | Each return with reason |
| Results and history | Competency override (D4) | Who allowed whom, reason |
| Results and history | Photo added after the reading (F2) | Photo and reason |

- Each exception has a **stable key** from its facts. If the facts change (a new value, a new photo), it is a new exception and needs a new tick.
- Exceptions whose package is not built yet are simply absent.
- Where the system cannot work out a fact (for example no lot recorded), it shows "Could not be checked" as an exception of its own.

### 5.2 Review screen

- At the top: the list of exceptions grouped by test, each with a **Seen** tick and an optional comment, and a link to the evidence.
- Tests without exceptions are collapsed with a one-line summary (test, result, "No exceptions"); one click expands them to today's full view.
- A counter shows "7 of 9 exceptions seen". **Complete review** stays disabled with the reason until all are ticked; the server enforces the same.
- Ticks are saved as the reviewer works, so leaving and returning keeps them. Ticks belong to that reviewer; another reviewer starts fresh.

### 5.3 What is recorded

- When the review is signed, the list of exceptions with their keys, ticks, comments, who and when is stored with the review and bound to the review signature. It is shown in the sample's history and in the review record of the sample report (not on the certificate).
- If an exception appears between the ticks and the signature, the signature is refused and the new item shown.

### 5.4 Reviewer queue

The queue shows each section's number of exceptions and the groups present, next to today's priority and age, and can sort by them. Priority rules are unchanged.

## 6. Error handling

| Situation | Behaviour |
|---|---|
| Complete with unticked exceptions | Refused, listing them |
| New exception after ticks | Refused at signing; new item shown |
| Fact cannot be determined | Shown as "Could not be checked" exception |
| Exception service fails | Review cannot be completed; message "Exceptions could not be loaded. Retry." |

## 7. Testing

- **Backend `[PostgresFact]`:** each exception type produced from real data and absent when its condition is not met (including time-of-use boundaries for expiry and calibration, window boundaries); stable keys; changed facts create new keys; ticks saved per reviewer; completion refused with unticked or new exceptions; recorded list bound to the signature and immutable; queue counts; existing review rules unchanged.
- **Frontend (`userEvent`):** grouped list, ticks and comments, counter, disabled Complete with reason, collapsed tests expand, refusal at signing shows new item.

## 8. Delivery stages

1. Exception service with values, incubation and results-and-history groups.
2. Materials and equipment at use.
3. Ticks, completion check, recorded list.
4. Review screen and queue.
5. Exceptions from E1, D4 and F2 as those packages arrive.
6. Documents.

## 9. Open item

1. The "near expiry" number of days is a setting, off until the Section Head sets it.

**Audit-capture decision (user, 2026-11-11):** approved — `ReviewExceptionTick` (working ticks before signing) are kept out of automatic audit capture, each added to `AuditCaptureExcludedTypes` with a justification comment. The signed `ReviewExceptionRecord` list stays under audit and is bound to the review signature.
