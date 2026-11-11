# D1 — Structured OOS / OOT Investigation: Design Specification

**Roadmap package:** D1 (`improvement-roadmap.md`)
**Status:** Approved by the user 2026-11-11
**Date:** 2026-11-11
**Related:** B4 (QA role), C5 (notifications), D2 (deviations and CAPA).

## 1. Purpose

Replace the uploaded investigation file with a structured, signed investigation that opens by itself when a result is out of specification, stops the sample from being approved until it is concluded, and controls retesting through an approved plan.

## 2. Decisions (confirmed by the user, 2026-11-11)

| # | Decision | Answer |
|---|---|---|
| 1 | Phases | Phase I laboratory investigation; if no laboratory error is found, Phase II full investigation; then conclusion |
| 2 | Opening | Automatically on every out-of-specification result, when the reviewer confirms it. The sample cannot be approved, rejected or certified until the investigation is concluded |
| 3 | Closure | The Section Head closes it. QA is informed and can open a deviation |
| 4 | Retest | A retest plan is approved before any retest. Retesting without a concluded Phase I is refused |

## 3. Out of scope

- The deviation and CAPA records themselves (package D2). D1 records QA's request for a deviation; D2 turns it into a deviation record.
- Automatic out-of-trend detection. No approved trend rule exists; see 5.8.
- Changing how a result is calculated or compared with its limits.
- The manufacturing investigation outside the laboratory. Its findings are entered as text and attachments.
- Old OOS groups. They stay as they are, read-only, with their documents.

## 4. Current state (from code, not run)

- **An out-of-specification result opens nothing.** An OOS group exists only after the Section Head signs a retest or new-sample decision. Nothing stops a sample with an OOS result from being approved, and the certificate builder does not check.
- There is no investigation record: no phases, checklist, cause or conclusion. Only an uploaded file per group, with no signature.
- A retest is a new sample linked to the original; the original test orders are marked superseded and kept. There is no limit on retests and no plan.
- When the retest samples close, the original is finalised automatically **without a new signature**.
- The decision "new sample" is signed with the meaning "Rejected".
- The approval decisions "Investigation" and "OOS investigation" exist in a list but cannot be used; `BusinessRules.md` describes them as working.
- Results are classified as within limits, alert limit exceeded, action limit exceeded, out of specification, and (for pathogens) detected / not detected. Alert and action results create nothing, except in the monitoring plan, where an action-limit result opens a monitoring deviation.
- Control charts evaluate eight run rules and only display them.
- Two KPIs exist: OOS rate and retest rate.

## 5. Design

### 5.1 The investigation record

- One investigation per sample and laboratory section, covering every failing result of that section. Number: the existing OOS group code, so old and new records share one numbering.
- Status: **Phase I → Phase II → Concluded**, or **Phase I → Concluded** when a laboratory error is confirmed. A concluded investigation can be reopened only by the Section Head with a reason and signature; the earlier conclusion is kept.
- Every step records who and when. Nothing in a signed phase can be changed afterwards; additions are new dated entries.
- Templates (checklists and cause categories) are configured per laboratory (microbiology, physicochemical) by the Section Head. The investigation keeps a copy of the template it started with, so later template changes do not alter it.

### 5.2 Opening

- The investigation opens when the **reviewer completes the review** of a test whose result fails its specification, in the same transaction as the review. "Fails" is decided by the existing evaluation (out of specification, or a pathogen detected where the specification requires absence); no new rule is added.
- A further failing result on the same sample and section is added to the open investigation.
- A result corrected before review (with its reason, as today) is reviewed as corrected; the correction and the original value are in the audit trail and are shown to the reviewer. Once an investigation is open, a later correction does not close it; it must be concluded.
- The analyst, the Section Head and QA are notified.
- From that moment the section of the sample cannot be approved or rejected, and no certificate can be issued for the sample. Review of the other tests continues.

### 5.3 Phase I — laboratory investigation

- **Analyst statement:** what was done and observed, entered by the analyst who produced the result.
- **Checklist** from the laboratory's template: each item answered Yes / No / Not applicable with a comment; the system pre-fills what it knows and shows it read-only beside the item: equipment used and its calibration status, media and solution lots with release and expiry, reference standards, incubation times against the configured window, and the audit trail of the result.
- **Assessment** by the Section Head: *Laboratory error confirmed* (an assignable cause is selected from the cause categories and described) or *No laboratory error found*.
- Signed by the Section Head with the meaning "Phase I concluded". The signer cannot be an analyst who performed the failing test.
- **Laboratory error confirmed:** the original result is marked invalidated with the cause. It is never deleted or changed. A retest plan is required (5.5), and the investigation is concluded after the retest.
- **No laboratory error found:** Phase II starts.

### 5.4 Phase II — full investigation

- Sections: review outside the laboratory (sampling, manufacturing — text and attachments), hypotheses, each with the test performed to check it and its outcome, further checklist from the template, and root cause (category and description, or "No cause found").
- Hypothesis tests are recorded as investigation data only. They are never reportable results and never appear on a certificate.
- A retest plan may be added (5.5).
- **Conclusion** by the Section Head, signed with the meaning "Investigation concluded": *OOS confirmed* or *OOS invalidated — assignable cause*. "Invalidated" requires a selected cause; "No cause found" can only lead to "OOS confirmed".

### 5.5 Retest plan

- Written inside the investigation **before** any retest: what is retested (which tests), on what (retained sample or new sample), how many retests, by which analyst or analysts, and how the retest results will be judged.
- Signed by the Section Head ("Retest plan approved"). Only then does the system create the retest samples, using the existing linked-sample mechanism. It creates exactly what the plan says; a further retest needs a signed plan amendment with a reason.
- A retest cannot be planned before Phase I is signed.
- For a sample with a failing result, the direct "Retest" and "New sample" decisions on the approval screen are removed; they exist only through the plan. For other samples they stay as today.
- The automatic, unsigned finalisation of the original sample when retests close is removed. The Section Head's signed conclusion decides.

### 5.6 After conclusion

| Conclusion | Effect |
|---|---|
| OOS confirmed | The original result stands. The block is lifted; the section can be **rejected**, not approved |
| OOS invalidated | The original result is shown as invalidated with the cause. The reportable result comes from the retest results according to the plan. The block is lifted; normal approval rules apply |

- The certificate and the sample report show the investigation number against the affected tests, and an invalidated result is never shown as the reported value.
- QA is notified of the conclusion.

### 5.7 QA

- QA (package B4) reads every investigation and is notified when one opens, enters Phase II, becomes overdue and is concluded.
- QA can **request a deviation** on an investigation at any time, with a reason. The request is recorded, shown on the investigation, and notified to the Section Head. Package D2 will create the deviation record from it; until then it is a tracked request with status Open / Addressed.
- A deviation request does not by itself block the conclusion; whether it should is asked in section 9.

### 5.8 Excursions and out-of-trend

- **Excursion review** — the same engine with a one-phase template: checklist, cause, action taken, Section Head signature. It opens automatically for an action-limit result. For an alert-limit result it opens only if the laboratory switches that on (off by default).
- Where the monitoring plan already opens a monitoring deviation for the same result, no second record is created; the monitoring deviation remains the record and is linked.
- An excursion review does not block the sample.
- **Out-of-trend:** a user can open an excursion review from a point or a flagged rule on a control chart. The system does not open one by itself, because no trend rule has been approved as a trigger.

### 5.9 Due dates

Two settings per laboratory, in working days: Phase I due, and whole investigation due. They are **empty until the site sets them**; with no value there is no due date. Overdue investigations appear in My Work and in notifications.

### 5.10 Screens

- OOS Tracking becomes a register of investigations (open, overdue, concluded, legacy groups) with filters.
- The investigation page: header (sample, tests, results, status, due dates), then one section per phase, the retest plan with the linked retest samples and their results, attachments, QA requests, and a timeline of every signed step with name, time with time zone and meaning.
- The sample and test screens show a banner "OOS investigation OOS-… open" with a link, and the reason approval is not available.
- A printable investigation report (PDF) with all signatures.

### 5.11 Trending

A report of investigations by product, test, analyst, instrument, cause category, conclusion and time to conclude, with the same filters as other reports and export. The two existing KPIs keep their definitions.

### 5.12 Permissions and separation

- `Oos.Manage` (existing; Section Head): assess, plan, conclude. `Oos.View`: read (Reviewer, QC Manager, QA, and analysts for investigations of their own results). `Oos.Contribute`: analyst statement and checklist answers. `Oos.RequestDeviation`: QA.
- No signature on an investigation by a person who performed the failing test or any retest in it.
- Section scope applies as everywhere else.

### 5.13 Upgrade

- The rules apply to results recorded after the upgrade. Existing OOS groups are shown as "legacy" and keep their uploaded documents.
- Samples that already have a failing result and are not yet approved at upgrade are listed for the Section Head, who opens an investigation for each or records why not, with a signature.
- The signature meaning of the old "new sample" decision is corrected for new records only; old signatures are untouched.
- The two unusable approval decisions are removed from the screen and the description in `BusinessRules.md` is corrected.

## 6. Error handling

| Situation | Behaviour |
|---|---|
| Opening the investigation fails | The review is not saved either; the reviewer sees the error |
| Approval, rejection or certificate attempted while open | Refused: "OOS investigation OOS-… is open." |
| Approve attempted after "OOS confirmed" | Refused: "A confirmed out-of-specification result cannot be approved." |
| Retest requested without a signed plan | Refused |
| Signer performed the failing test or a retest | Refused with the reason |
| Two people edit the same phase | The second save is refused; reload |
| Template changed during an investigation | The investigation keeps its copy |

## 7. Testing

- **Backend `[PostgresFact]`:** investigation opens with the failing result in one transaction and both roll back together; second failing result joins it; correction does not close it; approval, rejection and certificate blocked while open; Phase I signature rules and separation; error-confirmed path invalidates without changing the original values; Phase II conclusion rules ("no cause" cannot invalidate); retest refused without plan, created exactly per plan, amendment needed for more; no automatic finalisation; approve refused after confirmed; excursion review opening rules and no duplicate with monitoring deviations; QA deviation request; reopen keeps earlier conclusion; legacy groups untouched; state, audit and negative checks on every signed step; signed phases immutable.
- **Frontend (`userEvent`):** register filters; each phase form; checklist with pre-filled facts; retest plan and amendment; signature dialogs; blocked-approval banner; QA request; report view.
- **Manual:** one full microbiology and one physicochemical investigation, both outcomes.

## 8. Delivery stages

1. Investigation record, automatic opening, approval and certificate block.
2. Templates and Phase I.
3. Retest plan; removal of the direct decisions and of automatic finalisation.
4. Phase II and conclusion; effects on approval and certificate.
5. QA notifications and deviation request.
6. Excursion review.
7. Register, investigation report, trending.
8. Upgrade handling; documents.

## 9. Decisions recorded 2026-11-11

1. Checklist templates ship empty; the Section Head fills them per laboratory. An investigation cannot be assessed while its laboratory's template is empty.
2. Cause categories shipped as editable defaults: Analyst error, Equipment, Method, Sample, Media or reagent, Environment, No cause found. "No cause found" is fixed and cannot be removed.
3. The retest plan states the judging rule in words; the Section Head concludes. The system does not calculate the outcome.
4. No extra rules for microbiology.
5. An open QA deviation request does not block the conclusion.
6. The investigation opens when the reviewer confirms the failing result.
7. Due dates are settings in working days, per laboratory, set by the site.
