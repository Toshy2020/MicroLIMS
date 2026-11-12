# URS-MED User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-MED |
| Title | Media preparation, evaluation and release user requirements |
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

### URS-MED-001
**Requirement:** The system shall let only users who hold the media-prepare permission prepare media lots, mark them out of stock and add or move their storage entries, and shall limit the lists of lots, evaluations and release queues to the laboratory sections the user may read.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-MED-002
**Requirement:** The system shall record, for each prepared media lot, the dehydrated media stock lot used, the quantity used, the autoclave, the autoclave programme values entered, the pH, the expiry date, the preparing user and the preparation time, and shall give the lot a unique lot number.
**GxP:** Yes
**Serves:** ALCOA+ attributable and original records; media traceability (reference to be confirmed by QA)
**Status:** Active

### URS-MED-003
**Requirement:** The system shall deduct from stock the quantity of dehydrated media and of each stock-lot additive used for a media lot, in the same save as the new lot, and shall refuse an additive that is not valid (wrong lot type, other laboratory section, expired, awaiting release, rejected, or without a current Certificate of Analysis where one is required).
**GxP:** Yes
**Serves:** Inventory traceability (reference to be confirmed by QA)
**Status:** Active

### URS-MED-004
**Requirement:** The system shall create an evaluation for every prepared media lot, with the challenges configured by the Section Head for the media product, and shall not allow a lot to be released for use until its evaluation has completed as conforming.
**GxP:** Yes
**Serves:** Media growth promotion and quality control (reference to be confirmed by QA)
**Status:** Active

### URS-MED-005
**Requirement:** The system shall accept an organism source for a challenge only when it is an approved, unexpired, undestroyed cryovial batch (or an unexpired lyophilized disk with stock remaining) of the challenge organism, from the same laboratory section as the media lot, and, for a thawed vial, within the usable period after thawing.
**GxP:** Yes
**Serves:** Reference strain traceability (reference to be confirmed by QA)
**Status:** Active

### URS-MED-006
**Requirement:** The system shall take the incubation temperature range and duration for an evaluation challenge from the media configuration, and shall not accept a challenge result before the configured minimum incubation time has passed.
**GxP:** Yes
**Serves:** EU GMP Annex 11 clause 5 (to be confirmed by QA)
**Status:** Active

### URS-MED-007
**Requirement:** The system shall decide each challenge outcome on the server from the entered values and the media configuration, and shall decide the evaluation outcome as conforming only when every challenge conforms.
**GxP:** Yes
**Serves:** Data integrity of calculated results (reference to be confirmed by QA)
**Status:** Active

### URS-MED-008
**Requirement:** The system shall put a media lot whose evaluation does not conform into quarantine so that it cannot be used.
**GxP:** Yes
**Serves:** Media control (reference to be confirmed by QA)
**Status:** Active

### URS-MED-009
**Requirement:** The system shall release or reject an evaluated media lot only on the decision of a user who holds the media-release permission, who re-enters their password as an electronic signature, and who did not prepare the lot or read any of its challenges, and shall keep an archived record of the lot as decided.
**GxP:** Yes
**Serves:** 21 CFR 11.50, 11.200; EU GMP Annex 11 clause 14 (to be confirmed by QA)
**Status:** Active

### URS-MED-010
**Requirement:** The system shall let a user mark a released, active media lot as out of stock, with an optional comment, so that it can no longer be released for use.
**GxP:** Yes
**Serves:** Media control (reference to be confirmed by QA)
**Status:** Active

### URS-MED-011
**Requirement:** The system shall record where a released media lot is stored, by form and quantity, in an in-service equipment item or as free text, and shall require a reason when a quantity is moved.
**GxP:** Yes
**Serves:** Storage traceability (reference to be confirmed by QA)
**Status:** Active

### URS-MED-012
**Requirement:** The system shall list media lots that expire within a number of days chosen by the user, with the evaluation result of each, so that expiring lots can be handled.
**GxP:** No
**Status:** Active
