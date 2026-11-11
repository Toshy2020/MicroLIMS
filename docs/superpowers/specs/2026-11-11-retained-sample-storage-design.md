# F3 — Retained Sample Storage and Chain of Custody: Design Specification

**Roadmap package:** F3 (`improvement-roadmap.md`)
**Status:** Draft for user review
**Date:** 2026-11-11
**Uses:** B1 (labels and scanning), B4 (QA role), D1 (retests from the retained sample).

## 1. Purpose

Know where every retained sample is, who moved it and when, how much is left, when its retention ends, and dispose of it only after QA approval.

## 2. Decisions (confirmed by the user, 2026-11-11)

| # | Decision | Answer |
|---|---|---|
| 1 | Scope | Retained samples only |
| 2 | Locations | The system offers four levels — room, unit (fridge, freezer, cabinet), shelf, box — and the site uses the levels it needs for each place |
| 3 | Custody | Every move is recorded, by scanning the label (or picking from a list) |
| 4 | Retention and disposal | The Section Head sets retention periods; QA approves disposal with a signature |

## 3. Out of scope

- Test samples in the laboratory (not tracked in storage).
- Positions inside a box (grid). See question 1.
- Temperature monitoring of storage units.
- Cryovials and media, which keep their own storage records.

## 4. Current state (from code, not run)

- A retained quantity and unit are recorded at review, required only for raw-material samples reviewed by the physicochemical laboratory. There is no retained-sample record, location, expiry or disposal.
- Storage for cryovials and media uses in-service cold-storage equipment or a free-text place; there is no room, shelf or box structure and no position model.
- Nobody is recorded as holding a sample after receipt.
- No labels or scanning exist yet (package B1 is designed, not built).
- D1's retest plan can choose "retained sample" as the source, but nothing records taking material from the retained sample.

## 5. Design

### 5.1 Storage locations

- A tree with up to four levels: **room → unit → shelf → box**. Each place uses only the levels it needs (for example a room with boxes on a bench, or a fridge with shelves and no boxes). A retained sample can be stored at any level.
- A unit that is a fridge or freezer is linked to its equipment record; storing in, or moving into, a unit whose equipment is out of service is refused.
- Each location has a code, a name, an optional storage condition (for example "2–8 °C") and a label with a barcode (B1).
- Locations are maintained by the Section Head; a location holding samples cannot be removed, only deactivated after it is empty.

### 5.2 Retained-sample register

- A **retained sample** record: the sample, item, batch, quantity and unit, quantity remaining, container count, storage condition required, location, retention end date, status (In storage, Out, Disposal requested, Disposed).
- It is created:
  - automatically when the retained quantity is recorded at review (today's raw-material rule), and
  - by the laboratory for any other sample ("Register retained portion"), with quantity and location.
- Each retained sample gets its own label (B1).

### 5.3 Moves and custody

- Every move is a **custody event**: what, from where (location or person), to where, by whom, when, and why (put away, taken out for retest, returned, transferred to another laboratory, sent for disposal). The holder while it is out is the person who took it.
- The usual way is a scan of the retained-sample label and then the destination label; picking from lists is the fallback.
- Taking material out records the quantity taken and the purpose; quantity remaining decreases. Taking out more than remains is refused.
- A sample out of storage longer than a configurable time (setting, empty = no limit) appears on the Section Head's list.
- Events are never edited; a wrong event is corrected by a new event with a reason.

### 5.4 Link to retests

When a D1 retest plan uses the retained sample, the analyst records the withdrawal from the retained sample (quantity and the plan it serves); the retest samples link to it.

### 5.5 Retention periods

- The Section Head sets retention rules: per sample category, optionally overridden per item. A rule is either **a number of months after receipt** or **a number of months after the batch expiry date** (when the sample has one). No rule ships pre-filled.
- The retention end date is calculated when the retained sample is registered and stored; changing a rule later applies to new registrations only, unless the Section Head chooses to recalculate the open ones (recorded with a reason).
- Retained samples without a rule show "Retention period not set".

### 5.6 Disposal

- Samples whose retention has ended appear on a **due for disposal** list for their laboratory.
- The laboratory prepares a **disposal request** (one or more retained samples), signed by the Section Head.
- **QA approves** the request with a signature; QA can remove items from it with a reason.
- After approval, the person disposing scans each retained sample and records the disposal method and date; each sample becomes Disposed. The request is closed when all items are disposed.
- A retained sample linked to an open OOS investigation, deviation or complaint cannot be added to a disposal request.
- Disposed records stay in the register permanently.

### 5.7 Permissions

| Permission | Holders | Allows |
|---|---|---|
| `Retention.View` | Analyst, Reviewer, Section Head, QC Manager, QA | Register and history |
| `Retention.Move` | Analyst, Section Head | Register, move, withdraw, dispose after approval |
| `Retention.Manage` | Section Head | Locations, rules, disposal requests |
| `Retention.ApproveDisposal` | QA | Approve disposal requests |

## 6. Error handling

| Situation | Behaviour |
|---|---|
| Unknown or wrong label scanned | "This label is not a retained sample / location" |
| Destination unit out of service | Refused |
| Withdrawal above remaining quantity | Refused |
| Disposal of a sample linked to an open investigation, deviation or complaint | Refused, naming the record |
| Disposal without QA approval | Refused |
| Location with samples removed | Refused; deactivate after it is empty |

## 7. Testing

- **Backend `[PostgresFact]`:** location tree with one to four levels; out-of-service unit refused; automatic registration at review; manual registration; every custody event type; remaining quantity; correction events; retention end date for both rule kinds and month-end cases; rule change applies to new registrations and recalculation with reason; disposal request signatures by Section Head and QA, separation (requester ≠ approver), item removal, blocked links, disposal completion; state, audit and negative checks.
- **Frontend (`userEvent`):** scan flow (scanner input mocked at the service boundary), list fallback, register views, disposal workflow.
- **Manual:** label print and scan of a retained sample through two moves, a withdrawal for a retest and a disposal.

## 8. Delivery stages

1. Locations and labels.
2. Register and automatic registration at review.
3. Custody events and scanning; withdrawals and retest link.
4. Retention rules and end dates.
5. Disposal requests and QA approval.
6. Documents; SOP template note.

## 9. Questions for you

1. **Levels.** I read "offer the four" as room, unit, shelf and box, each optional per place. Do you also want positions inside a box (a grid such as A1–H12)?
2. **Retained samples of other categories.** Today only raw materials reviewed by the physicochemical laboratory record a retained quantity. Should finished products (or others) require one too, or is manual registration enough?
