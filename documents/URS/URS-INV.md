# URS-INV User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-INV |
| Title | Inventory materials and equipment register user requirements |
| Version | 0.1 |
| Status | Draft |
| Author | Claude (AI agent), draft from code |
| Reviewer | To be assigned by QA |
| Approver | To be assigned by QA |
| Effective date | Set when Approved |

## Change history

| Version | Date | Author | Change |
|---|---|---|---|
| 0.1 | 2026-11-12 | Claude (AI agent), draft from code | First draft, written from the code as built. |

## Requirements

Each requirement below is a need that the code as built serves. Gaps found in the code are not written as requirements here; they are recorded in the functional specification entries and as conflicts (see `documents/_conflicts/CONF-task7.md`).

### URS-INV-001
**Requirement:** The system shall let only users who hold the materials-manage permission receive and edit stock lots, and shall limit the stock lot list and every change to a lot to the laboratory sections the user may read or write.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-INV-002
**Requirement:** The system shall record each received stock lot with its type, name, manufacturer, batch number, receiving date, expiry date, location, quantity received, unit and owning laboratory section, shall give it a generated stock ID, and shall start its remaining quantity at the quantity received.
**GxP:** Yes
**Serves:** ALCOA+ attributable and original records; material traceability (reference to be confirmed by QA)
**Status:** Active

### URS-INV-003
**Requirement:** The system shall require the master-data link, the media product link, the reference organism link, the purity value or the expiry date that the lot type or material category calls for, and shall refuse a lot that lacks them or links to an inactive master entry.
**GxP:** Yes
**Serves:** Master data control; reference standard traceability (reference to be confirmed by QA)
**Status:** Active

### URS-INV-004
**Requirement:** The system shall let an authorised user correct a stock lot, shall refuse the correction when the form is out of date, shall keep the consumption already recorded when the quantity received is corrected, and shall not allow the media product or reference organism of a lot to change once media lots or cryovial batches have been prepared from it.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e) audit trail; ALCOA+ (to be confirmed by QA)
**Status:** Active

### URS-INV-005
**Requirement:** The system shall refuse to take stock from a lot that is of the wrong type, expired, awaiting release, rejected, without a current Certificate of Analysis where one is required, or without enough quantity remaining, and shall record the deduction in the same save as the record that used it.
**GxP:** Yes
**Serves:** Inventory control and traceability (reference to be confirmed by QA)
**Status:** Active

### URS-INV-006
**Requirement:** The system shall hold a stock lot whose material category requires approval before use as pending review, and shall release or reject it only on the decision of a user who holds the materials-release permission, who did not receive the lot, and who re-enters their password as an electronic signature; a rejection shall need a comment.
**GxP:** Yes
**Serves:** 21 CFR 11.50, 11.200; EU GMP Annex 11 clause 14 (to be confirmed by QA)
**Status:** Active

### URS-INV-007
**Requirement:** The system shall list the stock lots in the user's sections, a print list without expired and depleted lots, and the reference and working standards that are in stock, not expired and not held or rejected.
**GxP:** No
**Status:** Active

### URS-INV-008
**Requirement:** The system shall keep a register of equipment with code, instrument type, manufacturer, serial number, firmware version, location, calibration due date, operational status and owning laboratory section, shall refuse a duplicate code, and shall refuse a serial number already used by the same manufacturer on another item that is not retired.
**GxP:** Yes
**Serves:** EU GMP Annex 15 equipment records (to be confirmed by QA)
**Status:** Active

### URS-INV-009
**Requirement:** The system shall require a comment when the operational status of an equipment item changes, shall keep each status change in a history with user and time, and shall refuse an edit from an out-of-date form.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e); ALCOA+ (to be confirmed by QA)
**Status:** Active

### URS-INV-010
**Requirement:** The system shall show, for an equipment item in the user's sections, the incubations currently or previously held in it, and shall let the user find where a sample or media lot is or was incubated.
**GxP:** Yes
**Serves:** Traceability of equipment use (reference to be confirmed by QA)
**Status:** Active
