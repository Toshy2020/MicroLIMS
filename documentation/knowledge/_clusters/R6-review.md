# R6 Domain and GMP review — C6 Physicochemical

Basis: C6-physchem.md (verified knowledge and workflows) plus spot checks in code (audit capture, SoD guard usage, workspace permissions). The application was not run. No acceptance limit is stated here that is not in C6 or code.

## Phase 5 domain checklist

### MOD-PHYSRESULT-001 Physicochemical result panels (Measurement, Gravimetric, Qualitative, Dissolution, Disintegration, Weight Variation)
| Checklist item | Status | Where | Note |
|---|---|---|---|
| Workspace (panel chosen by Test Master workflow type) | DOCUMENTED | SCR-PHYSRESULT-001, WF-PHYSRESULT-001/003 | HPLC and ICP assays are NOT submitted here; they go through their workspaces. |
| Equation types | PARTIAL | MOD-EQTYPE-001, SCR-EQTYPE-001 | The Equation Types page is a read-only hard-coded list of six entries; it does not list Dissolution, Disintegration, Weight Variation, Titration, HPLC or ICP assay. Guide must not imply the page is the full list. |
| Test creation (Test Master configuration) | REQUIRES CONFIRMATION | C7 (Test Master) | Replicate counts, evaluation basis, tare, stage offsets, weight-variation bands are per-test fields (BR-PHYSRESULT-002, -006); site values not in repository. GAP-610, GAP-611. |
| Calculation (gravimetric, measurement, dissolution, disintegration, weight variation) | DOCUMENTED | BR-PHYSRESULT-001 to -008 | Server-side; frontend only previews. |
| Specification evaluation / limit types | DOCUMENTED | BR-HPLC-014 | Numeric limit types evaluated; Qualitative, PresenceAbsence, MultiStage, DissolutionQ, DisintegrationTime, WeightVariation evaluated by their own evaluators. |
| Staged testing (Stage 2 / 3) | DOCUMENTED | WF-PHYSRESULT-003 | Each stage has its own signature. Per-product stage values REQUIRES CONFIRMATION (GAP-611). |
| Weight variation band defaults | REQUIRES CONFIRMATION | BR-PHYSRESULT-006, GAP-610 | Code defaults (130/324 mg, 10/7.5/5 %) differ from the spec example; configured values decide. |
| Review of result | DOCUMENTED (owned by C3) | WF-PHYSRESULT-001 | Result goes to Ready, section to Under Review automatically. |

### MOD-HPLC-001 / MOD-GC-001 HPLC and GC workspaces; MOD-HPLCM-001 methods master
| Checklist item | Status | Where | Note |
|---|---|---|---|
| Workspace and run setup (method, column, mobile phases) | DOCUMENTED | WF-HPLC-001 step 1, BR-HPLC-001 to -005 | |
| HPLC methods master | DOCUMENTED | SCR-HPLCM-001, BR-METHOD-001 to -006 | Edit in place with reason; no signature (GAP-615 resolved). |
| Suitability gate | DOCUMENTED | BR-HPLC-007, -008, -009 | Pass/Fail criteria come from the method; no default limits in code. Failed SST: run stays Open, only exit is Abandon (GAP-600, code and spec agree). |
| Standard entry | DOCUMENTED | WF-HPLC-001 step 2, BR-HPLC-006, -010 | Standard weight is deducted from the lot on confirmation, pass or fail. |
| Sample entry | DOCUMENTED | WF-HPLC-001 step 7, BR-HPLC-015, -016 | |
| Calculation | DOCUMENTED | BR-HPLC-011 to -013 (assay), BR-GC-001 (residual solvents) | Th.Wt constants are method constants, not typed by the analyst (BR-HPLC-024 is INTENDED). |
| Review | PARTIAL | WF-HPLC-001 "Review / approval" | Review/approval is in the Testing workspace (C3); a Review panel and reports section exist in the workspace (SCR-HPLC-002) but their content was not enumerated. |
| Run completion / abandon | DOCUMENTED | BR-HPLC-021, -022 | Abandon needs a reason, no password. |
| Dissolution standard from run SST | DOCUMENTED | BR-HPLC-025 | DISCREPANCY spec vs spec on the moisture term; code follows later spec. |
| Column usage tracking | NOT IMPLEMENTED | BR-COLUMN-002, GAP-612 | Run stores the column id only. |
| Report import (Empower/ICP OCR) | NOT IMPLEMENTED (no UI) | MOD-INSTIMPORT-001, GAP-613 | Parsers exist but no caller; results are typed manually. |
| Column master permission | REQUIRES CONFIRMATION | MOD-COLUMN-001, GAP-607 | Page needs MasterData.Manage, API writes need Equipment.Manage. |

### MOD-ICP-001 / MOD-ICPM-001 ICP-OES
| Checklist item | Status | Where | Note |
|---|---|---|---|
| Calibration acceptance rule as implemented | DOCUMENTED | BR-ICP-001 to -005 | Per element: r at least method MinCorrelation; optional blank limit; optional ICV recovery window (inclusive). No code default for MinCorrelation (spec example 0.999 only). Failing elements do not block confirmation; they are unusable for samples. |
| Calibration validity | DOCUMENTED | BR-ICP-005 | Method field, 1 to 168 h. |
| CCV | DOCUMENTED | BR-ICP-006, GAP-603 | One failed reading locks the element for the rest of the run; submitted samples are not pulled back. CCV is not signed. |
| Calculation (content, mg/unit, % LC, LOQ, over range) | DOCUMENTED | BR-ICP-007 to -011 | |
| What to do when no element passes | REQUIRES CONFIRMATION | GAP-601 | No recalibration after confirmation; exit in code is Abandon. |
| Elemental-impurity vs mineral modes | DOCUMENTED (mode field) | BR-ICP-015 | Mode fixed after creation. Which mode the site uses: not in repository. |

### MOD-TITR-001 Titration
| Checklist item | Status | Where | Note |
|---|---|---|---|
| Titration types, modes, calculation, endpoint | DOCUMENTED | MOD-TITR-001, GAP-609 | Five types, direct/residual, USP factor/relative, visual/potentiometric; KF limited. Which the site uses: REQUIRES CONFIRMATION. |
| Calculation | DOCUMENTED | BR-TITR-001 to -005 | Result rounded half away from zero to max(2, spec decimals) and judged on the rounded value. |
| Titrant factor gating / due titrant | DOCUMENTED | BR-TITR-006, -007, WF-PHYSRESULT-002 | UspFactor: due titrant blocks. Relative: due titrant allowed with acknowledgement, 10 to 500 character justification and a second signature. |
| Titrant standardization | DOCUMENTED | WF-SOLN-002, BR-SOLN-010 to -014 | Failed standardization is saved and signed but never becomes the current factor. |
| Test settings change control | DOCUMENTED | audit-trail (TestDefinition.TitrationChanged) | Reason of 5 to 500 characters; no signature (C7 confirms Test Master edits carry no signature in other cases). |

### MOD-SOLN-001 / MOD-SOLNM-001 Solution, mobile phase, diluent, titrant preparation
| Checklist item | Status | Where | Note |
|---|---|---|---|
| Preparation from master recipe against stock lots | DOCUMENTED | WF-SOLN-001, BR-SOLN-001 to -009 | Stock consumed at signed completion; discard does not restore stock. |
| Expiry blocking | DOCUMENTED | BR-SOLN-008 | Background job every 5 minutes plus read-time check; expired solutions cannot be selected; an expired mobile phase blocks SST confirmation. |
| Expiry clock | REQUIRES CONFIRMATION | BR-SOLN-007, GAP-605 | Code uses UTC instants; spec says lab-local clock. |
| Lot checks (purity/moisture) | PARTIAL | BR-SOLN-003 | Spec lists MissingPurity/MissingMoisture reasons; code checks them only in SST and titrant standardization, not in preparation. |
| Master changes | DOCUMENTED | SCR-SOLNM-001 | Reason and audit event, no signature. |

### MOD-WS-001 Working standards; MOD-REAGENT-001 material master
| Checklist item | Status | Where | Note |
|---|---|---|---|
| Qualification, review, approval | DOCUMENTED | WF-WS-001, BR-WS-001 to -009 | Six replicates, RSD at most 2.0 %, 12-month validity are fixed in code, not configurable. Three different people required. |
| Moisture correction after creation | REQUIRES CONFIRMATION | GAP-619 | No edit control found in the drawer. |
| Stock deduction on use | REQUIRES CONFIRMATION | BR-WS-008, GAP-617 | Code deducts at SST; spec D8 says not. |
| Rounding at the RSD limit | REQUIRES CONFIRMATION | GAP-608 | RSD rounded to 2 decimals before comparison. |
| Role naming (QC head) | REQUIRES CONFIRMATION | GAP-614 | |

### Material stock deduction and expiry blocking (cross-cutting)
| Checklist item | Status | Where | Note |
|---|---|---|---|
| Deduction points | DOCUMENTED | BR-HPLC-010, BR-SOLN-006, BR-SOLN-010, BR-TITR-008 | SST (mg to g/kg), solution completion, titrant standardization, relative titration. Applied in the same save as the signature. |
| Expiry blocking of lots | DOCUMENTED | BR-SOLN-003, BR-HPLC-006 | Lot is expired only when expiry date is before the lab date (usable on expiry day). |
| Standard purity/moisture required | DOCUMENTED | BR-REAGENT-004 | Purity required for reference and primary standards; moisture optional, reference standards only. |

### Department-scoped masters
| Checklist item | Status | Where | Note |
|---|---|---|---|
| Section/lab scoping of workspaces and masters | DOCUMENTED | MOD-HPLC-001 scope; method/column/lot "belongs to another laboratory" messages | Scoping by laboratory section; the department model itself is C7/C1. |
| Segregation by role at run level | NOT IMPLEMENTED | GAP-602 | One person may start a run, confirm SST/calibration and submit samples. |

## Phase 6 GMP / data-integrity facts

### MOD-HPLC-001 / MOD-GC-001 / MOD-ICP-001 Instrument workspaces
- User accountability: SST confirmation, ICP calibration confirmation and sample send-for-review each require the user's password and write an electronic signature with a meaning (SuitabilityRunPerformed, ResultRecorded). [BR-HPLC-007, BR-HPLC-017, BR-ICP-003; audit-trail]
- Role separation: all writes need Hplc.Operate (the ICP workspace reuses it). Pages open for any signed-in user. [roles]
- SoD: the user who entered a result cannot review or approve the sample (shared guard). No SoD exists between starting a run, confirming SST/calibration, entering CCV and submitting samples in one run. [roles; GAP-602]
- Calculation transparency: the screen shows a server preview labelled "Preview, not the official result" and, after submission, "Official, Recorded for Review"; the calculation JSON, method snapshot and standard values are stored with the result. The frontend does not calculate. [SCR-HPLC-004; reports]
- Result modification: replicates are editable until sent for review. After submission they are frozen. A returned sample becomes editable again (also on a Completed run) until it is submitted again. [BR-HPLC-015]
- Failed SST: stored as Failed with reasons; the run stays Open; no path back to Pending; Abandon requires a reason and no password. The failed confirmation still deducts the standard from stock. [BR-HPLC-009, -022, -010]
- Rework/retest: abandoned runs release their test orders so they can be assigned to a new run. No investigation record is required by the system. [BR-HPLC-022; GAP-600]
- Audit trail: every save writes audit rows through the database context (verified in MicroLimsDbContext; excluded entities are audit rows, error log, incidents, refresh tokens, notification log, security events, and the two document-access logs). Run, SST, replicate and evidence changes are audited this way; they carry no separate reason except where the dialog asks for one (abandon, remove, supersede). [audit-trail; MicroLimsDbContext.cs:L307-L318]
- Evidence: PDF, PNG, JPEG, up to 25 MB; no delete; replacement needs a reason and the old file stays visible. [BR-HPLC-023]
- CCV readings are append-only and unsigned. [BR-ICP-006]
- Method master edits (HPLC, ICP) require a reason and record before/after values with a history view; no electronic signature is taken. [SCR-HPLCM-001, SCR-ICPM-001; GAP-615]
- Session security: not specific to this module (C1).

### MOD-SOLN-001 Solution preparation and titrant standardization
- Accountability: completion is signed (PreparationConfirmed); standardization is signed (TitrantStandardized). Prepared-by and prepared-at are stored; status history rows record transitions with reason or note. [WF-SOLN-001, WF-SOLN-002]
- Review/approval: none in the system for a preparation or a standardization. [WF-SOLN-001]
- Status control: Prepared solutions expire automatically; Discard and Cancel need a reason and are not signed; discard does not restore stock. [BR-SOLN-008, -009]
- Master changes: Solutions Master edits carry a reason and an audit event; no signature. [SCR-SOLNM-001]
- Record history: a failed standardization is retained and never becomes the current factor. [BR-SOLN-013]

### MOD-PHYSRESULT-001 / MOD-TITR-001 Result panels and titration
- Accountability: each result (and each stage) is signed with ResultRecorded; a due-titrant acknowledgement is signed separately (TitrantDueAcknowledged) with the justification as the comment. [WF-PHYSRESULT-001 to -003; BR-TITR-007]
- Calculation transparency: server calculates; the entry stores calculation JSON, and titration stores a snapshot (titrant code, factor, standardization id, standard lots, warnings). [BR-TITR-012]
- Result modification: no edit-in-place of a recorded result was found; a new entry is possible only after the reviewer returns the test. An active entry blocks a second entry ("An active ... entry already exists for this test order."). Return and supersede rules are C3. [WF-PHYSRESULT-001]
- Review of results: review and approval belong to C3. The recorder performs no review.
- SoD: result enterer cannot review/approve the sample. [roles]
- Test Master changes that alter titration settings require a reason (5 to 500 characters) and write a "TestDefinition.TitrationChanged" event with before/after settings. No signature. [audit-trail]

### MOD-WS-001 Working standards
- Accountability: submit, review, approve and reject are each signed (ResultRecorded, Reviewed, Approved, Rejected). Return to Draft is not signed but needs a reason. [BR-WS-006]
- SoD: three different people are required (submitter, reviewer, approver) with the exact messages listed in roles. [roles]
- Status control: Draft, Assayed, Reviewed, Approved, Rejected. A failed assay can only be rejected. An approved qualification creates or updates the working standard lot in the same save; working standard lots cannot be created or changed by receiving or stock edit. [BR-WS-006, BR-WS-007, BR-REAGENT-003]
- Record history: Return clears the results and the submitter; the earlier values are not kept on the record (audit rows hold the changes). REQUIRES CONFIRMATION whether a Return is acceptable without a stored prior result. [BR-WS-006; audit-trail]

### MOD-REAGENT-001, MOD-COLUMN-001 Reagent master and columns
- Master-data changes: Reagent master, Chromatography Columns: writes need MasterData.Manage (reagent) and Equipment.Manage (column API). C6 records no reason or signature requirement for reagent master or column edits beyond uniqueness and category checks. [BR-REAGENT-002; GAP-607] Audit of these edits is by the generic database audit only.

## Mock / placeholder / unverified data
- None found in C6 modules. The Equation Types list is hard-coded (informational, incomplete) rather than mock data. [MOD-EQTYPE-001]
- Instrument report parsers (Empower, ICP) exist but are not called; no figure on any screen comes from them. [MOD-INSTIMPORT-001]
- Not verified by running the application: all screen behaviour, panel field lists marked "not enumerated", HPLC review panel content.

## Guide callouts
1. Failed system suitability: the run cannot be re-run; abandon it (with a reason) and start a new run. The standard weight has already been deducted from stock. (GAP-600, BR-HPLC-010)
2. ICP: calibration cannot be re-entered after confirmation. If no element is valid, sample assignment stays blocked. Recovery is not defined in the system. (GAP-601)
3. ICP CCV: a failed reading locks that element for the rest of the run; samples already sent for review are not pulled back. (GAP-603)
4. One person can start an HPLC/GC/ICP run, confirm suitability or calibration and submit samples; the system only stops that person from reviewing or approving the same sample. (GAP-602)
5. Working standards: three different people must submit, review and approve. The 6 replicates, RSD at most 2.0 % and 12-month validity are fixed in the system. Moisture cannot be corrected on a Draft from the screen found. (GAP-619, GAP-608)
6. Stock of a working standard decreases when it is used for system suitability; the specification says it should not. (GAP-617)
7. Solution expiry is calculated on the server clock (UTC); the specification says lab-local clock. Confirm for days-based shelf lives. (GAP-605)
8. Stage limits (dissolution, disintegration) and weight-variation bands come from the Test Master per product; the guide shows defaults only as defaults. (GAP-610, GAP-611)
9. Method, solution and ICP master edits require a reason and are audited but are not electronically signed. (GAP-615 resolved; flagged for confirmation of requirement in GAP-705 context)
10. The Equation Types page shows only six equation types and is read-only; it does not list the newer test types. (GAP-606)
11. Instrument reports are not imported; all values are typed and the report is uploaded as evidence. (GAP-613)
12. Column page access (MasterData.Manage) and column writes (Equipment.Manage) differ; the Analyst role can write but cannot open the page. (GAP-607)
13. Which titration types, ICP modes and GC assay mode the site uses is not in the repository. (GAP-609)

## Corrections made to the cluster file
- None required. Spot checks (SoD guard usage, workspace permission policies, audit capture in the database context) agree with C6. Added no content to C6.
