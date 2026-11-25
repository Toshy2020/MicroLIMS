# URS-RPT User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-RPT |
| Title | Reporting, Certificate of Analysis and trending user requirements |
| Version | 0.3 |
| Status | Draft — pending approval |
| Author | Claude (AI agent), draft from code |
| Reviewer | To be assigned by QA |
| Approver | To be assigned by QA |
| Effective date | Set when Approved |

## Change history

| Version | Date | Author | Change |
|---|---|---|---|
| 0.1 | 2026-11-12 | Claude (AI agent), draft from code | First draft, written from the code as built. |
| 0.2 | 2026-11-17 | AI coding agent, draft from code | Added the audited CSV export of register rows requirement (URS-RPT-010). |
| 0.3 | 2026-11-25 | AI coding agent, draft from code | Added Certificate of Analysis OOS gate and invalidated-result reporting requirement (URS-RPT-011; Draft — pending approval). |

## Requirements

Each requirement below is a need that the code as built serves. Gaps found in the code are not written as requirements here; they are recorded in the functional specification entries and as conflicts (see `documents/_conflicts/CONF-task8.md`).

### URS-RPT-001
**Requirement:** The system shall limit every report, search, trend, comparison, export and control chart to the laboratory sections the user may read.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-RPT-002
**Requirement:** The system shall let a user search, sort and page recorded results and open one result record, with the page size capped.
**GxP:** No
**Serves:** Not applicable
**Status:** Active

### URS-RPT-003
**Requirement:** The system shall show a trend of a count test over time with mean, standard deviation, minimum and maximum, and shall compare subjects for one test.
**GxP:** No
**Serves:** Not applicable
**Status:** Active

### URS-RPT-004
**Requirement:** The system shall refuse a results export that matches more than 10,000 rows, and shall record every export in the data export log with the user, the time, the filter and the row count.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e); EU GMP Annex 11 clause 9 (to be confirmed by QA)
**Status:** Active

### URS-RPT-005
**Requirement:** The system shall decide on the server whether each result on a Certificate of Analysis conforms, shall certify only results that conform, and shall state "Cannot certify" for a result that is missing or has no configured limits.
**GxP:** Yes
**Serves:** 21 CFR 211.188; EU GMP Annex 11 clause 4 (to be confirmed by QA)
**Status:** Active

### URS-RPT-006
**Requirement:** The system shall order the rows of a product, raw material or packaging Certificate of Analysis by laboratory, then by test group, then by the Test Master CoA order.
**GxP:** Yes
**Serves:** Not applicable
**Status:** Active

### URS-RPT-007
**Requirement:** The system shall produce an archived sample report that shows the identity, results, lifecycle timeline and, for each electronic signature, the signer's printed name, the date and time and the meaning.
**GxP:** Yes
**Serves:** 21 CFR 11.50; EU GMP Annex 11 clause 14 (to be confirmed by QA)
**Status:** Active

### URS-RPT-008
**Requirement:** The system shall compute control chart limits, special-cause rule flags and process capability for a series of results on the server.
**GxP:** No
**Serves:** Not applicable
**Status:** Active

### URS-RPT-009
**Requirement:** The system shall apply proposed alert and action limits to water and environmental monitoring configurations only with a written reason and the user's electronic signature, and shall refuse the change if the limits changed after the analysis was loaded.
**GxP:** Yes
**Serves:** 21 CFR 11.50, 11.200; EU GMP Annex 11 clause 14 (to be confirmed by QA)
**Status:** Active

### URS-RPT-010
**Requirement:** The system shall let a user who holds the data export permission export the rows of a register table, or only the rows the user has selected, as a CSV file of at most 5,000 rows; shall write the file as UTF-8 with a byte-order mark; shall stop any cell that a spreadsheet could read as a formula from running as one; and shall not produce the file unless the export has first been logged.
**GxP:** Yes
**Serves:** 21 CFR 11.10(b) (to be confirmed by QA)
**Status:** Active


### URS-RPT-011
*(Draft — pending approval)*
**Requirement:** The system shall block Certificate of Analysis generation while any OOS investigation covering the sample is open or while the reporting rule for multiple signed-plan retest samples after invalidation is unresolved, shall display the OOS investigation number against affected tests on the report, and shall never display an invalidated result as the reported value.
**GxP:** Yes
**Serves:** 21 CFR 211.188, 211.192 (to be confirmed by QA)
**Status:** Active
