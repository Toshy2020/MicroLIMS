# URS-MDM User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-MDM |
| Title | Master data user requirements |
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

Each requirement below is a need that the code as built serves. Gaps found in the code are not written as requirements here; they are recorded in the functional specification entries and as conflicts (see `documents/_conflicts/CONF-task8.md`).

### URS-MDM-001
**Requirement:** The system shall let only users who hold the master-data permission (or, for items, the items permission) create, change, freeze or delete master data, and shall let any signed-in user read it.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-MDM-002
**Requirement:** The system shall let a user change a test definition, its workflow steps, its stage replicates or its frozen state only when the test belongs to a laboratory section to which the user is assigned, unless the user's role holds the permission to configure all sections.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.10(g); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-MDM-003
**Requirement:** The system shall let a user add, change or delete an item specification row only when the test of that row belongs to a laboratory section to which the user is assigned, unless the user's role holds the permission to configure all sections.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-MDM-004
**Requirement:** The system shall refuse to save a specification row that does not meet the rules of its limit type, that names a test not assigned to the item, or that duplicates another row of the same item, test, parameter, stage and grade.
**GxP:** Yes
**Serves:** 21 CFR 11.10(a); EU GMP Annex 11 clause 4 (to be confirmed by QA)
**Status:** Active

### URS-MDM-005
**Requirement:** The system shall let a Section Head define a test in the Test Master with its laboratory section, workflow type, equation type and the settings that belong to that type, and shall refuse a combination of settings that does not fit together.
**GxP:** Yes
**Serves:** 21 CFR 11.10(a); EU GMP Annex 11 clause 4 (to be confirmed by QA)
**Status:** Active

### URS-MDM-006
**Requirement:** The system shall require a written reason of 5 to 500 characters when titration settings or the moisture role of a test are changed, and shall record that reason in the audit trail.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e); EU GMP Annex 11 clause 9 (to be confirmed by QA)
**Status:** Active

### URS-MDM-007
**Requirement:** The system shall let a Section Head configure the ordered steps of a step-driven test, with the media, incubation conditions and final step, and shall refuse a step list with gaps in its numbering or with more than one final step.
**GxP:** Yes
**Serves:** 21 CFR 11.10(a); EU GMP Annex 11 clause 4 (to be confirmed by QA)
**Status:** Active

### URS-MDM-008
**Requirement:** The system shall refuse to delete an item, an organism, a cause of testing, a workflow step or a location configuration that records already use, and shall offer freezing for items as the way to retire them.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e); ALCOA+ (to be confirmed by QA)
**Status:** Active

### URS-MDM-009
**Requirement:** The system shall refuse a master-data edit that was made from a form that is out of date, where the request carries the record version.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e); EU GMP Annex 11 clause 9 (to be confirmed by QA)
**Status:** Active

### URS-MDM-010
**Requirement:** The system shall record every create, update and delete of master data in the audit trail with the user, the time and the old and new values.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e); EU GMP Annex 11 clause 9 (to be confirmed by QA)
**Status:** Active

### URS-MDM-011
**Requirement:** The system shall let a user download the current master data as a workbook and check an uploaded workbook without saving anything, listing every row error in one pass.
**GxP:** No
**Serves:** Not applicable
**Status:** Active

### URS-MDM-012
**Requirement:** The system shall load a master-data workbook only with the loader's electronic signature, shall load all rows or none, and shall store the file and its checksum with the load record.
**GxP:** Yes
**Serves:** 21 CFR 11.50, 11.200; EU GMP Annex 11 clause 14 (to be confirmed by QA)
**Status:** Active

### URS-MDM-013
**Requirement:** The system shall keep items, material master entries and reference organisms created by a workbook inactive until a second person, who is not the loader, signs a verification of the load.
**GxP:** Yes
**Serves:** 21 CFR 11.10(g); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-MDM-014
**Requirement:** The system shall let a Section Head set the Certificate of Analysis order of a test as a whole number from 1 to 9999, or clear it.
**GxP:** Yes
**Serves:** Not applicable
**Status:** Active

### URS-MDM-015
**Requirement:** The system shall let the laboratory configure water sampling points, environmental monitoring departments and rooms, after-cleaning machines and parts, and the test configurations with their alert, action and specification limits that apply to them.
**GxP:** Yes
**Serves:** EU GMP Annex 11 clause 4 (to be confirmed by QA)
**Status:** Active

### URS-MDM-016
**Requirement:** The system shall let a Section Head maintain the media evaluation configuration of a media product, with its incubation condition and challenge organisms.
**GxP:** Yes
**Serves:** EU GMP Annex 11 clause 4 (to be confirmed by QA)
**Status:** Active
