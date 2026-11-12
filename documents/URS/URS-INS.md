# URS-INS User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-INS |
| Title | Instrument runs (HPLC, GC and ICP) user requirements |
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

Each requirement below is a need that the code as built serves. Gaps found in the code are written in the FS entries and in `documents/_conflicts/CONF-task6.md`, not as requirements here. This document covers the HPLC and GC workspace, the ICP workspace and the calculation and judgement of their results. The qualification of working standards on an HPLC run is in URS-WST. The dissolution calculation is in URS-PHY.

### URS-INS-001
**Requirement:** The system shall let only users who hold the HPLC-operate permission change an HPLC, GC or ICP run, shall limit that change to laboratory sections of which the user is a member, and shall let a user read runs only in the laboratory sections the user may read.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-INS-002
**Requirement:** The system shall refuse to start a run on an instrument that already has an open run, whose calibration due date has passed, or that the equipment inventory shows as out of service or retired.
**GxP:** Yes
**Serves:** 21 CFR 211.68; EU GMP Annex 15 (to be confirmed by QA)
**Status:** Active

### URS-INS-003
**Requirement:** The system shall start an HPLC or GC run only with an active method of the same laboratory as the instrument, a compatible active column whose designation matches the method, and a prepared, unexpired mobile phase for every channel of the method, and shall keep a copy of the method as it was when the run started.
**GxP:** Yes
**Serves:** 21 CFR 211.160(b); EU GMP Annex 11 clause 4 (to be confirmed by QA)
**Status:** Active

### URS-INS-004
**Requirement:** The system shall judge system suitability on the server against the criteria saved with the run's method, shall record the pass or fail with the reasons, shall require the analyst's electronic signature to confirm it, and shall not let samples be assigned to a run whose system suitability has not passed.
**GxP:** Yes
**Serves:** 21 CFR 211.160(b); 21 CFR 11.50; System suitability, chapter pointer USP <621> (to be confirmed by QA)
**Status:** Active

### URS-INS-005
**Requirement:** The system shall accept a reference standard lot for system suitability only when it is of the standard configured on the method, belongs to the laboratory of the run, is released, not rejected, in date and in stock, and shall deduct the weighed standard from the lot when system suitability is confirmed.
**GxP:** Yes
**Serves:** 21 CFR 211.194(a)(2); EU GMP Annex 11 clause 4 (to be confirmed by QA)
**Status:** Active

### URS-INS-006
**Requirement:** The system shall let an analyst assign to a run only the test orders of the method's tests in the run's laboratory that are not finished and not already on another open run, and shall let a sample be removed from a run only with a reason and only before its result is sent for review.
**GxP:** Yes
**Serves:** EU GMP Annex 11 clause 4; ALCOA+ data integrity (to be confirmed by QA)
**Status:** Active

### URS-INS-007
**Requirement:** The system shall check each entered replicate on the server (sample weight, responses, internal standard response, related-substance peaks) against the saved method and the replicate count configured for the stage, and shall refuse a replicate set that does not match.
**GxP:** Yes
**Serves:** 21 CFR 211.194(a); ALCOA+ data integrity (to be confirmed by QA)
**Status:** Active

### URS-INS-008
**Requirement:** The system shall calculate the HPLC assay result, the amount per unit, and the result on a dried or anhydrous basis on the server from the entered replicates, the confirmed system suitability standard and the method's theoretical weights, and shall compare it with the specification configured by the Section Head.
**GxP:** Yes
**Serves:** 21 CFR 211.194(a)(5); EU GMP Annex 11 clause 4 (to be confirmed by QA)
**Status:** Active

### URS-INS-009
**Requirement:** The system shall calculate GC residual solvents in ppm and HPLC related substances in percent on the server, using the method's standard concentration, solution volume, reference solution, correction factors and disregard limit, and shall report a result without any signal as "Not detected".
**GxP:** Yes
**Serves:** Residual solvents, chapter pointer USP <467> (to be confirmed by QA)
**Status:** Active

### URS-INS-010
**Requirement:** The system shall let an analyst send a run sample's result for review only after system suitability has passed, all entries are complete and the sample's instrument report has been uploaded, shall require the analyst's password as an electronic signature, and shall link the stored result to the run and to the system suitability record.
**GxP:** Yes
**Serves:** 21 CFR 11.50; 21 CFR 11.70; EU GMP Annex 11 clause 14 (to be confirmed by QA)
**Status:** Active

### URS-INS-011
**Requirement:** The system shall accept instrument evidence files of the types PDF, PNG and JPEG up to 25 MB for an open run, shall keep a replaced file with the reason for the replacement, and shall show a reviewer the current evidence of a test order.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e); ALCOA+ data integrity (to be confirmed by QA)
**Status:** Active

### URS-INS-012
**Requirement:** The system shall judge an ICP calibration on the server element by element against a minimum correlation, shall require the analyst's electronic signature to confirm it, shall treat a confirmed calibration as usable for a limited time, and shall accept a calibration standard only from a usable reference standard lot of the laboratory.
**GxP:** Yes
**Serves:** 21 CFR 211.160(b); 21 CFR 11.50 (to be confirmed by QA)
**Status:** Active

### URS-INS-013
**Requirement:** The system shall assign samples to an ICP run only while the calibration is confirmed and in date, shall require the analyst's electronic signature for the assignment, and shall refuse to enter or send results for an element that was not calibrated, failed calibration or whose calibration has expired.
**GxP:** Yes
**Serves:** 21 CFR 11.50; EU GMP Annex 11 clause 4 (to be confirmed by QA)
**Status:** Active

### URS-INS-014
**Requirement:** The system shall calculate the ICP assay percent or impurity ppm on the server from the reported concentration, the test's target, theoretical amount and sample form, and the actual weight, and shall compare it with the specification.
**GxP:** Yes
**Serves:** 21 CFR 211.194(a)(5); EU GMP Annex 11 clause 4 (to be confirmed by QA)
**Status:** Active

### URS-INS-015
**Requirement:** The system shall propose ICP results from the uploaded instrument report only as values for the analyst to check and save, and shall flag values that came from optical character recognition.
**GxP:** Yes
**Serves:** ALCOA+ accuracy; EU GMP Annex 11 clause 5 (to be confirmed by QA)
**Status:** Active

### URS-INS-016
**Requirement:** The system shall let an analyst close a run as abandoned only with a reason, shall let a run be completed only when every assigned sample has been sent for review or removed, and shall refuse to abandon or complete a run that is already closed.
**GxP:** Yes
**Serves:** 21 CFR 211.194(a); ALCOA+ data integrity (to be confirmed by QA)
**Status:** Active
