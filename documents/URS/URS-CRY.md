# URS-CRY User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-CRY |
| Title | Cryovial batches and reference organisms user requirements |
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

### URS-CRY-001
**Requirement:** The system shall let only users who hold the cryovial-manage permission prepare, thaw, destroy and view cryovial batches, only the users who hold the cryovial-approve permission approve, reject or move the storage of a batch, and shall limit these actions and the batch list to the laboratory sections the user may use.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-CRY-002
**Requirement:** The system shall prepare a cryovial batch only from a lyophilized microorganism stock lot that has an organism set, shall require a physical check confirmation and at least one identity-confirmation row, shall accept for identity confirmation only media lots that are released for use and of the same laboratory section, shall take the discs used off stock, and shall give the batch a unique code.
**GxP:** Yes
**Serves:** Reference strain traceability and identity confirmation (reference to be confirmed by QA)
**Status:** Active

### URS-CRY-003
**Requirement:** The system shall hold a new cryovial batch as pending review, and shall not allow vials to be thawed from it, or used in a media evaluation, until it is approved.
**GxP:** Yes
**Serves:** Reference strain control (reference to be confirmed by QA)
**Status:** Active

### URS-CRY-004
**Requirement:** The system shall approve or reject a cryovial batch only with the electronic signature (password) of a user who did not prepare the batch, and shall mark a rejected batch as destroyed and keep an archived record of the batch as decided.
**GxP:** Yes
**Serves:** 21 CFR 11.50, 11.200; EU GMP Annex 11 clause 14 (to be confirmed by QA)
**Status:** Active

### URS-CRY-005
**Requirement:** The system shall record each thaw of a vial from an approved, unexpired, undestroyed batch as a thaw event with user, time and notes, and shall reduce the vials remaining by one.
**GxP:** Yes
**Serves:** ALCOA+ attributable records (reference to be confirmed by QA)
**Status:** Active

### URS-CRY-006
**Requirement:** The system shall let a user withdraw a whole cryovial batch from use so that no more vials can be thawed from it and its thawed vials can no longer be used.
**GxP:** Yes
**Serves:** Reference strain control (reference to be confirmed by QA)
**Status:** Active

### URS-CRY-007
**Requirement:** The system shall move the storage location of an approved, undestroyed cryovial batch only with a reason, to an in-service equipment item or a free-text location, and shall log the move.
**GxP:** Yes
**Serves:** Storage traceability (reference to be confirmed by QA)
**Status:** Active

### URS-CRY-008
**Requirement:** The system shall keep a master list of reference organisms (strains) with a unique code, the culture collection and the strain number, shall let every user read the list, and shall let only users with the master-data-manage permission, in the laboratory section that owns the strain, create or edit a strain.
**GxP:** Yes
**Serves:** Reference strain traceability (reference to be confirmed by QA)
**Status:** Active

### URS-CRY-009
**Requirement:** The system shall not allow the organism, culture collection or strain number of a reference organism that has stock lots or cryovial batches to be changed, and shall allow its code to be changed only with a reason and the electronic signature of the user.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e); 21 CFR 11.50 (to be confirmed by QA)
**Status:** Active

### URS-CRY-010
**Requirement:** The system shall let an owner-laboratory user with the master-data-manage permission deactivate and reactivate a reference organism, and shall not accept an inactive reference organism for a new stock lot.
**GxP:** Yes
**Serves:** Master data control (reference to be confirmed by QA)
**Status:** Active
