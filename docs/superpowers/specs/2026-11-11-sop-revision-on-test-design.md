# D5 — SOP Revision on the Test Record: Design Specification

**Roadmap package:** D5 (`improvement-roadmap.md`)
**Status:** Approved by the user 2026-11-11
**Date:** 2026-11-11
**Related:** C6 (Open SOP), D4 (competency gate uses the same SOP resolution).

## 1. Purpose

Record on each test which SOP and revision were in force when the test was performed, so the record shows the method version actually used and never changes afterwards (`CLAUDE.md` Rule 3.1).

## 2. Decisions (confirmed by the user, 2026-11-11)

| # | Decision | Answer |
|---|---|---|
| 1 | Moment of capture | At the first recorded step of the test. The test keeps that revision even if a newer one becomes effective while it runs |
| 2 | Certificate | Once in the header ("Method: SOP-QC-014 rev. 3"); if tests of the sample used different revisions, each is listed |

## 3. Out of scope

- Samples without an item (water, environmental monitoring, after cleaning): they have no SOP link today and none is added.
- Tests recorded before this package: they stay without a captured revision and show "Not recorded (before {release date})".
- Blocking a test because the SOP cannot be resolved (that is the competency gate, package D4).

## 4. Current state (from code, not run)

- An item has an SOP number, matched when someone looks to a controlled document and its current effective revision. Nothing is stored on the test order.
- Every recording point already passes through a laboratory-access check on the server (the same points package D4 uses).
- The certificate is built on the server from the sample summary; its header has no method reference.

## 5. Design

- **Capture:** at the first recorded step of a test order (preparation, first workflow step, or first result), the server resolves the item's SOP and stores on the test order: document code, document title, revision number, revision id, effective date, and the time of capture. If it cannot be resolved, it stores why (no SOP number, not found in Document Control, no effective revision).
- Capture happens once, in the same transaction as that first step. It is never changed afterwards, not by later steps, not by a new revision, not by an item edit. The database refuses a change once set.
- **Newer revision:** if a later revision became effective while the test was still open, the test screen and review screen show "A newer revision (rev. 4) became effective on {date} while this test was running." This is information only.
- **Shown on:** the test screen, the review screen (per test), the sample report, and the certificate header (once, or one line per distinct revision), with the effective date.
- **Unresolved:** shown as "SOP not recorded: {reason}". On the certificate the method line is left out for those tests.
- **Retests** get their own capture at their own first step.
- **Audit:** the capture is part of the audited change of the first step.

## 6. Testing

- **Backend `[PostgresFact]`:** capture at the first step for each kind of first step; not re-captured at later steps; unchanged after a new revision becomes effective and after the item's SOP number changes; database rejects an update once set; unresolved reasons stored; no capture for samples without an item; certificate header for one and for two revisions; newer-revision notice; old tests show "not recorded"; audit entry contains the captured values.
- **Frontend (`userEvent`):** test and review screens show the revision, the unresolved reason and the newer-revision notice.

## 7. Done when

Every test order with an item records its SOP revision at its first step, it is shown on the four places in section 5, and the tests above pass against PostgreSQL.
