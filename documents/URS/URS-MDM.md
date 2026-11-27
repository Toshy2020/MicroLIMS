# URS-MDM User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-MDM |
| Title | Master data user requirements |
| Version | 0.6 |
| Status | Draft — pending approval |
| Author | Claude (AI agent), draft from code |
| Reviewer | To be assigned by QA |
| Approver | To be assigned by QA |
| Effective date | Set when Approved |

## Change history

| Version | Date | Author | Change |
|---|---|---|---|
| 0.1 | 2026-11-12 | Claude (AI agent), draft from code | First draft, written from the code as built. |
| 0.2 | 2026-11-13 | AI coding agent, draft from code | Added barcode labels and scanning requirements (URS-MDM-017 to URS-MDM-024). |
| 0.3 | 2026-11-14 | AI coding agent, draft from code | Added configuration spreadsheet paste with preview and all-or-nothing batch save (URS-MDM-025). |
| 0.4 | 2026-11-16 | AI coding agent, draft from code | Added step-by-step configuration screens, where-used summary, change reasons, specification retirement, copy and new-from-existing, and analyst preview requirements (URS-MDM-026 to URS-MDM-031). |
| 0.5 | 2026-11-17 | AI coding agent, draft from code | Added items-using-a-test and open-SOP requirements (URS-MDM-032, URS-MDM-033). |
| 0.6 | 2026-11-25 | AI coding agent, draft from code | Added test-order SOP revision capture and workflow/review display requirement (URS-MDM-034; Draft — pending approval). |

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

## Barcode labels and scanning

### URS-MDM-017
**Requirement:** The system shall assign each printable record type (`Sample`, `MediaLot`, `SolutionPreparation`, `Cryovial`, `MaterialStockLot`, and `EquipmentAsset`) a fixed prefix and barcode symbology (`Code 128` for samples and equipment assets; `Data Matrix` for media lots, solution preparations, cryovials, and material stock lots), and shall not print a label for any record that has no assigned code yet.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e), 211.105; EU GMP Annex 11 clauses 4, 9 (to be confirmed by QA)
**Status:** Active

### URS-MDM-018
**Requirement:** The system shall let only a user holding the record type's creation permission print a first label, shall support 1 to 50 copies per record and up to 100 records per request, shall offer label printing after record creation and from each record's detail view, and shall generate a PDF label carrying the barcode, bold record code, print date, and layout fields that fit the configured page size, refusing the request if the barcode or mandatory text does not fit.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 211.105; EU GMP Annex 11 clauses 4, 12 (to be confirmed by QA)
**Status:** Active

### URS-MDM-019
**Requirement:** The system shall treat any print after the first recorded print of a record as a reprint, shall let only a user holding the `Labels.Reprint` permission reprint a label, shall require a reason of 10 to 500 characters for any reprint request, and shall mark each reprinted label visibly with its sequential reprint number.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.10(e), 211.125; EU GMP Annex 11 clauses 9, 12 (to be confirmed by QA)
**Status:** Active

### URS-MDM-020
**Requirement:** The system shall write an append-only print log row for each printed record (protected in the database against update, delete, and truncate) together with explicit print or reprint audit events capturing user, UTC timestamp, record identifier, code, sequential print number, copy count, label format version, batch ID, and reprint reason, and shall commit those records only after PDF rendering succeeds.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e); EU GMP Annex 11 clause 9 (to be confirmed by QA)
**Status:** Active

### URS-MDM-021
**Requirement:** The system shall let only users holding `Labels.Configure` update per-type label width (10 to 150 mm), height (10 to 150 mm), layout (`Standard` or `Small`), and default copies (1 to 50) with stale-form version checking, shall support a one-to-one link between a workflow instrument and an equipment inventory asset, and shall prevent changing an equipment asset's code once a label has been printed for it.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.10(e); EU GMP Annex 11 clauses 4, 12 (to be confirmed by QA)
**Status:** Active

### URS-MDM-022
**Requirement:** The system shall resolve scanned barcode inputs on the server to `Found`, `NotFound`, or `Ambiguous` outcomes limited to records the signed-in user is permitted to read, shall narrow prefixed scans (`S:`, `MED:`, `SP:`, `CRY:`, `MAT:`, `EQ:`) to that record type and search all six types when unprefixed, and shall convert unshifted Arabic keyboard layout characters and Arabic-Indic digits while informing the user when conversion occurred.
**GxP:** Yes
**Serves:** 21 CFR 11.10(a), 11.10(d); EU GMP Annex 11 clauses 4, 12 (to be confirmed by QA)
**Status:** Active

### URS-MDM-023
**Requirement:** The system shall let a signed-in user scan a barcode in global search to open a single matching record (or choose among ambiguous candidates), and shall provide inline scan-to-select inputs on reagent, media, organism, mobile-phase, and equipment pickers that accept a scanned record only when server resolution confirms the expected type, usability status, workflow instrument link (for equipment), and membership in the picker's currently offered options.
**GxP:** Yes
**Serves:** 21 CFR 11.10(a), 11.10(f); EU GMP Annex 11 clause 4 (to be confirmed by QA)
**Status:** Active

### URS-MDM-024
**Requirement:** Where the browser exposes camera access over a secure context (HTTPS), the system shall provide optional camera scanning for `Data Matrix` and `Code 128` barcodes in global search and inline scan fields using native browser detection when supported or a locally bundled WebAssembly decoder with no external CDN requests at runtime, and shall stop the camera stream immediately on detection or dialog close.
**GxP:** Yes
**Serves:** 21 CFR 11.10(a), 11.10(b); EU GMP Annex 11 clauses 4, 11 (to be confirmed by QA)
**Status:** Active

### URS-MDM-025
**Requirement:** The system shall allow a user holding the master-data permission to paste up to 200 configuration rows per paste from a spreadsheet into Water sampling points, Environmental Monitoring rooms, Environmental Monitoring room test configurations, plain Item specifications, Master Testing Materials, and microbiology Equipment Master with a blocking preview and an explicit pending review step before saving, and shall persist accepted configuration batches in an all-or-nothing server transaction after validating every row (while excluding Solution Master recipes from configuration paste so components continue to be defined via form or workbook).
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.10(e); ALCOA+ accurate and complete (to be confirmed by QA)
**Status:** Active

### URS-MDM-026
**Requirement:** The system shall present the creation and change of specification parameters, Test Master tests, equipment and stock material lots as step-by-step screens with a review step that shows what will be saved (and, when an existing record is changed, what changes), shall keep a draft of the user's entries on the device until it is saved or discarded, and shall offer exactly the same configuration options and apply exactly the same server-side checks as the single-form screens they replace.
**GxP:** Yes
**Serves:** 21 CFR 11.10(a), 11.10(e); ALCOA+ accurate and complete (to be confirmed by QA)
**Status:** Active

### URS-MDM-027
**Requirement:** The system shall show a user with access, before the user changes a specification, a Test Master test, an item or equipment, which other records use it, with a separate count for open work and for history, and shall limit what is counted to the laboratory sections the user may read.
**GxP:** No
**Serves:** Not applicable
**Status:** Active

### URS-MDM-028
**Requirement:** The system shall require a reason for change of at least 10 characters when a specification, a Test Master test, an item or equipment is edited while open work would be affected, and always when a specification is deleted, retired or reinstated or when a test or item is frozen or unfrozen or an item is deleted, shall decide on the server whether open work exists and not rely on the client, and shall record the reason on the audit trail entries of that change.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e); EU GMP Annex 11 clause 9 (to be confirmed by QA)
**Status:** Active

### URS-MDM-029
**Requirement:** The system shall retire, and not delete, a specification that a recorded result refers to, leaving the recorded results unchanged; shall not use a retired specification to evaluate or assign tests to new samples; shall stop result entry on a sample in testing whose specification has been retired, with a message that says so; and shall let a user with the master-data permission reinstate a retired specification with a reason.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e); EU GMP Annex 11 clauses 9 and 10; ALCOA+ original and enduring (to be confirmed by QA)
**Status:** Active

### URS-MDM-030
**Requirement:** The system shall let a user with the master-data or item permission create an item from an existing item, copy specifications from one item to another after a preview that shows which rows will be created, skipped or refused, and create a test or equipment record from an existing one, without ever copying the code, name, SOP number, serial number, calibration date or documents of the source, and shall save a copy only after the user confirms it.
**GxP:** Yes
**Serves:** 21 CFR 11.10(a), 11.10(e) (to be confirmed by QA)
**Status:** Active

### URS-MDM-031
**Requirement:** The system shall show a user who configures a test, before saving, what the analyst will be asked to do for it (steps, media, incubation windows, stages and entries), marked as a preview that cannot record anything, and shall create a new test together with its stages and steps in one all-or-nothing operation.
**GxP:** No
**Serves:** Not applicable
**Status:** Active

### URS-MDM-032
**Requirement:** The system shall show, for each test in the Test Master, the items that use the test, meaning items with a specification or a test order row for it that are not marked not applicable; shall page and search that list by item code or name; and shall show each item's SOP number with its status in Document Control.
**GxP:** No
**Serves:** Not applicable
**Status:** Active

### URS-MDM-033
**Requirement:** The system shall let a user open the SOP of an item from a test's workflow dialog and from the item list of the Test Master when the SOP is in Document Control, and shall state in words why it cannot be opened when no SOP number is set, the SOP is not in Document Control, the SOP has no effective revision, or the user may not read it.
**GxP:** No
**Serves:** Not applicable
**Status:** Active

### URS-MDM-034
*(Draft — pending approval)*
**Requirement:** The system shall capture the item's effective SOP revision once on each test order at the first recorded step, prevent modification once captured, and display the captured SOP revision (or the reason it was not recorded) and any newer effective revision notice on the test workflow dialog and review screen.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e), 211.188; EU GMP Annex 11 clause 4, ALCOA+ original and enduring (to be confirmed by QA)
**Status:** Active
