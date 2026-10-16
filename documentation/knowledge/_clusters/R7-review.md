# R7 Domain and GMP review — C7 General masters

Basis: C7-masters.md plus spot checks in code (workbook signature and second-person check, Test Master freeze, Test Master reason rule, audit capture exclusions). The application was not run. No limit or value is stated here that is not in C7 or code.

## Phase 5 domain checklist

### MOD-ITEM-001 Items
| Checklist item | Status | Where | Note |
|---|---|---|---|
| Item master (name, code, category, SOP number) | DOCUMENTED | SCR-ITEM-001, BR-ITEM-001, -002 | Categories Product, Raw Material, Packaging Material only; code unique, compared case-sensitively. |
| Assigned tests auto-created at receipt | DOCUMENTED | BR-ITEM-003, WF-ITEM-001 | One test order per assigned test, placed in the section that owns the test. |
| "At least one test" rule | PARTIAL | BR-ITEM-003, GAP-710 | Enforced by the dialog only; server and workbook accept an empty list. Such an item cannot receive samples. |
| Test codes checked against Test Master | PARTIAL | GAP-714 | Item save does not check; receipt does. Picker can create Test Master entries inline. |
| Freeze / unfreeze, delete | DOCUMENTED | BR-ITEM-004, -005 | Delete refused once any sample used the item; freeze has no reason or confirmation. |
| Controlled documents (SOP, Verification Report) | PARTIAL | BR-ITEM-006, GAP-701, GAP-711, GAP-713 | Versioning, SHA-256, view/download log exist. No void path; size limit 25 MB service vs 30 MB controller; upload buttons not permission-gated. |
| Preparation configuration approval | PARTIAL | BR-ITEM-007, GAP-704, GAP-707 | Usable while Pending Approval; Approve is an unsigned click, same person may save and approve; Rejected unused. |
| Intended behaviour (spec) | REQUIRES CONFIRMATION | WF-ITEM-001 | No specification found for Items. |

### MOD-SPEC-001 Specifications
| Checklist item | Status | Where | Note |
|---|---|---|---|
| Where specifications are edited | DOCUMENTED | SCR-SPEC-001, GAP-702 | Only in the Items workspace, tab Specifications; the stand-alone route redirects. |
| Limit types | DOCUMENTED | BR-SPEC-001 | Eleven: Range, NMT, NLT, Target +/- Tolerance, Count-Tiered, Qualitative, Presence/Absence, Multi-Stage, Dissolution Q, Disintegration Time, Weight Variation. |
| Evaluation | DOCUMENTED | BR-SPEC-003 | Exact decimal comparison, no rounding in the evaluator; inclusive limits by default; Count-Tiered boundary equal passes; missing limit gives LimitsNotConfigured. |
| Rounding point before comparison across all calculators | REQUIRES CONFIRMATION | GAP-712 | Titration judges the rounded mean (C6 BR-TITR-005); measurement passes unrounded; HPLC/ICP callers not all read. |
| Canonical limit text | DOCUMENTED | BR-SPEC-002 | Server overwrites the typed limit text for all types except Count-Tiered. |
| Stage-specific rows | DOCUMENTED | BR-SPEC-006 | Stage rows replace every-stage rows for a sample whose stage role has rows; never mixed. Deleting a production stage can drop samples back to every-stage rows (GAP-709). |
| Specialised constraints (dissolution, disintegration, weight variation, HPLC/ICP/titration bases) | DOCUMENTED | BR-SPEC-008 | Units fixed (mg, min, ppm etc.) by limit type. |
| Ownership by section | DOCUMENTED | BR-SPEC-005 | Lab of a specification = section of its Test Master row; section membership needed. Physchem area not enforced (GAP-708). |
| Specification change control | NOT IMPLEMENTED | BR-ITEM-009, GAP-705 | No reason, no signature, no review on specification create, edit or delete; only the generic audit row. Past results keep the limits copied at calculation time (BR-SPEC-010). |

### MOD-TESTMASTER-001 FP and RM & PM Test Master
| Checklist item | Status | Where | Note |
|---|---|---|---|
| Test creation (code, workflow type, equation type, section, area) | DOCUMENTED | WF-TESTMASTER-001, BR-TESTMASTER-001 to -004 | Workflow and equation type coupled; physchem tests need an area FP, RM & PM or Both. |
| Type-specific settings (HPLC/ICP method link, replicates, tare, stage offsets, disintegration units, weight-variation bands, titration) | DOCUMENTED | SCR-TESTMASTER-002/003 | Form defaults shown (for example stage offsets 5/15/25/2, bands 130/324 mg) are defaults only; product values are site configuration (C6 GAP-610, GAP-611). |
| Stage replicates | PARTIAL | BR-TESTMASTER-005, GAP-709 | Only HPLC sample entry was found to use them; other physchem screens not checked. |
| Change control on Test Master | PARTIAL | BR-TESTMASTER-006 | Reason (5 to 500 characters) only for titration setting changes; no signature; all other edits and freeze/unfreeze unsigned. |
| Freeze / unfreeze section scoping | NOT IMPLEMENTED | GAP-706 | Freeze takes only the id; no section check. |
| Area scoping per user | NOT IMPLEMENTED | GAP-708 | Section membership checked; per-user physchem area not. |
| Intended behaviour (spec) | REQUIRES CONFIRMATION | WF-TESTMASTER-001 | No specification for the physchem Test Master as a whole. |

### MOD-RECVCFG-001 Receiving Configuration
| Checklist item | Status | Where | Note |
|---|---|---|---|
| Samplers, Cause of Testing, Production Stages | DOCUMENTED | SCR-RECVCFG-001, BR-RECVCFG-001 to -003 | Global lists, no lab scoping. |
| Delete integrity | PARTIAL | BR-RECVCFG-002/-003, GAP-709 | Cause in use cannot be deleted; sampler and stage deleted without usage check; deleting a stage clears the stage link on samples. |
| Change control | NOT IMPLEMENTED | roles | No reason, signature or review. |
| Intended behaviour (spec) | REQUIRES CONFIRMATION | WF-RECVCFG-001 | No specification found. |

### MOD-WORKBOOK-001 Master Data Workbook
| Checklist item | Status | Where | Note |
|---|---|---|---|
| Download, check (dry run), signed load | DOCUMENTED | WF-WORKBOOK-001, BR-WORKBOOK-001 to -004 | 12 sheets, add-only, 10 MB, 5,000 new rows; any row error rolls back the whole load. |
| Second-person verification | DOCUMENTED | WF-WORKBOOK-002, BR-WORKBOOK-005 | Different user must sign Reviewed; no administrator exemption (checked in code). |
| What verification activates | DOCUMENTED | BR-WORKBOOK-005 | Only Items and Material Master entries start inactive; other sheets create active records at load, before verification. |
| Verifier scope | PARTIAL | BR-WORKBOOK-006 | Only material master sections are checked; items, equipment and specifications are not scope-checked at verification. |
| Reject a load | NOT IMPLEMENTED | statuses | No rejected status; a wrong load is corrected in the individual screens. |
| Error text for non-.xlsx file | REQUIRES CONFIRMATION | GAP-703 | |
| Signature dialog button labels | REQUIRES CONFIRMATION | screens | Shared SignatureDialog labels not re-read. |

### Department-scoped masters (cross-cluster)
| Checklist item | Status | Where | Note |
|---|---|---|---|
| Items and Receiving lists | DOCUMENTED | roles | Not scoped to a laboratory section. |
| Test Master and Specifications | DOCUMENTED | roles | Scoped by section membership of the test's owning section; System Administrator unrestricted. |
| Workbook | PARTIAL | roles | Loader section rules come from the called create services; download filters only the material master sheet. |

## Phase 6 GMP / data-integrity facts

### MOD-ITEM-001 Items
- The system records item, test-assignment, document and preparation-configuration changes in the audit trail with old and new values through the database context (generic audit, no reason). [audit-trail; MicroLimsDbContext.cs:L307-L318]
- No reason and no electronic signature is required to create, edit, freeze, unfreeze or delete an item, or to upload a controlled document. [BR-ITEM-009]
- Approval of the preparation configuration records the approver and time. It is not an electronic signature (no password). The same user who edited the protocol can approve it. A protocol in Pending Approval is already used for testing. [BR-ITEM-007; GAP-704, GAP-707]
- Editing an approved protocol resets it to Pending Approval and clears the approver; the earlier values remain in the generic audit row. [audit-trail]
- Controlled documents: a new upload supersedes the previous Current document with a recorded reason text; the file hash (SHA-256) is stored; each view and download is logged with user and time. A document cannot be voided or deleted from the screen. [BR-ITEM-006; GAP-711]
- Deleting an item is refused once any sample used it; the audit row records a delete that is allowed. [BR-ITEM-004]
- Session security and role definitions: C1.

### MOD-SPEC-001 Specifications
- The system records specification creation, change and deletion in the audit trail (old and new values). No reason, signature or review is required. [audit-trail; GAP-705]
- Recorded results keep the alert, action and specification values as copied at calculation time, so editing or deleting a specification does not change recorded results. [BR-SPEC-010]
- A specification can be changed only by a System Administrator or a member of the section owning the test; the refusal text refers to "only its Section Head". The check is on section membership, not on the Section Head role. [roles]
- Calculation transparency: limits are shown as the canonical text generated by the server; the evaluator compares exact decimals without rounding. Whether other calculators round first is not confirmed. [BR-SPEC-002, -003; GAP-712]

### MOD-TESTMASTER-001 Test Master
- The system records test creation, change and freeze/unfreeze in the audit trail (old and new values). [audit-trail]
- A reason (5 to 500 characters) is required only when titration settings change; it is stored with before/after JSON in the event "TestDefinition.TitrationChanged". No other Test Master change takes a reason; none takes a signature. [BR-TESTMASTER-006; verified in code]
- Freezing a test hides it from new selections and does not change existing records. Any holder of MasterData.Manage can freeze or unfreeze any test; no section check. [GAP-706; verified in code]
- Test master edits that change calculation behaviour (replicate count, evaluation basis, stage offsets, weight-variation bands) are plain saves; results already recorded keep their calculation JSON (C6). The system does not warn about open test orders when a test is edited (not found in code). [BR-TESTMASTER-003; REQUIRES CONFIRMATION]
- Result-affecting data (physchem result modification, signatures, SoD between entry and review) belong to C6/C3 and are not controlled from this module.

### MOD-RECVCFG-001 Receiving Configuration
- The system records changes to the three lists in the audit trail (generic). No reason, signature or review is required. Renaming a cause of testing changes the name shown on existing samples because they reference the row; renaming a sampler does not change recorded samples. [BR-RECVCFG-001, -002]
- Deleting a production stage clears the stage link on existing samples; the stage text stays. [GAP-709]

### MOD-WORKBOOK-001 Master Data Workbook
- User accountability: the load is signed with the loader's own password (meaning MasterDataChanged) before any row is touched; the signature stores user, printed name, username, role, time and IP address. [audit-trail; MasterDataWorkbookService.cs:L150]
- Second-person verification: a different user signs Reviewed (own password, optional comment). The system refuses the loader with "The person who loaded the workbook cannot verify it. Ask a second person." and no role is exempt. [roles; MasterDataWorkbookService.cs:L199-L216, verified]
- Evidence kept: file name, SHA-256, loader, time, comment, status, per-sheet summary, a stored copy of the file and one record per created entity (sheet and row number). [BR-WORKBOOK-007; traceability]
- Verification activates only Items and Material Master entries. Organisms, production stages, EM/water areas, media products and equipment created by the load are active immediately after the loader's signature and before verification. [BR-WORKBOOK-005]
- The verification dialog names items that have no specification yet; verification still activates them. [WF-WORKBOOK-002]
- No rejection of a load: a wrong load stays "Waiting for verification" until corrected in the individual screens, which take no signature. [statuses; GAP-705]
- Comparison against source documents is a manual offline step; the system does not compare the load with any source document. [WF-WORKBOOK-002]
- Add-only: the workbook refuses rows whose key exists with different values, so it cannot overwrite existing records. [BR-WORKBOOK-002]
- Non-repudiation for failed attempts: a refused (wrong-password) signature attempt stays on record. [BR-WORKBOOK-004]
- The draft SOP ML-SOP-MDW-001 states that every screen change asks for a reason and an electronic signature. The Items, Specifications, Receiving Configuration and Test Master screens do not. [GAP-705 — DISCREPANCY]

### Cross-cluster comparison of controlled master changes (facts)
- Reason plus audit event, no signature: HPLC methods, ICP methods, Solutions Master (C6). [C6 GAP-615]
- Reason only for a specific field group, no signature: Test Master titration settings (C7).
- No reason, no signature, generic audit only: Items, Specifications, Receiving Configuration, other Test Master edits, Reagent master and Chromatography Columns (C6/C7). [GAP-705; C6 BR-REAGENT-002]
- Electronic signature plus second person: Master Data Workbook (load and verification) and working-standard qualification (C6).

## Mock / placeholder / unverified data
- None found in C7 modules.
- Not verified by running the application: labels of the shared SignatureDialog, ICP/residual-solvent/titration specification basis fields, the text for a non-.xlsx file, what the inline "+ Add to Test Master" creates.

## Guide callouts
1. Items, specifications, receiving lists and most Test Master edits are saved without a reason or electronic signature; changes are recorded in the audit trail only. The draft workbook SOP says otherwise. (GAP-705)
2. Specifications are edited only inside an item (tab Specifications); the stand-alone page redirects. A change does not alter results already recorded. (GAP-702, BR-SPEC-010)
3. Item approval of the preparation configuration is a click, not a signature, can be done by the person who saved it, and the protocol is used before approval. (GAP-704, GAP-707)
4. Freezing a test is not limited to the test's section. (GAP-706)
5. The physchem area (FP / RM & PM) of a user is not enforced when editing tests or specifications. (GAP-708)
6. Deleting a production stage removes the stage link from existing samples and can change which specification rows and replicate counts apply. Create tests in the Test Master first rather than with the inline picker option. (GAP-709, GAP-714)
7. An item with no assigned tests can be saved by the server and the workbook but cannot receive samples. (GAP-710)
8. Workbook: a second person must verify; the loader cannot, including an administrator. Only items and material master entries wait for verification; other records are active after the load. Verification does not reject a load. (BR-WORKBOOK-005; GAP-705)
9. Workbook verification checks section membership only for material master entries. (BR-WORKBOOK-006)
10. Item documents cannot be voided; a new upload supersedes the old one. The upload limit is 25 MB. (GAP-701, GAP-711)
11. Rounding before comparison with a specification differs between test types (titration judges the rounded mean). (GAP-712)
12. Stage defaults and weight-variation bands shown on the Test Master are defaults; the product values are site configuration. (C6 GAP-610, GAP-611)

## Corrections made to the cluster file
- None. Spot checks (workbook second-person rule and signature meanings, Test Master reason rule, freeze without section check, audit exclusions) agree with C7. No content added.
