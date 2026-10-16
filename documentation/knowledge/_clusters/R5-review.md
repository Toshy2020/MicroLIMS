# R5 Domain and GMP review — C5 Media, strains, stock

Scope: Media preparation/evaluation/release/storage, Cryovials (reference strains), Materials Stock, Equipment Inventory. Application not run; statements are from code and the cluster file. The only specification touching this area is BusinessRules.md section 7 (GPT) and 12 (expiry notice); none exists for cryovials, Materials Stock or Equipment Inventory.

## Phase 5 domain checklist

### MOD-MEDIA-001 Media Preparation, Evaluation, Release, Storage
| Checklist item | Status | Where | Note |
|---|---|---|---|
| Media configuration | DOCUMENTED | C4 WF-MEDIACFG-001 | One configuration per product. |
| Media preparation | DOCUMENTED | WF-MEDIA-001, BR-MEDIA-001..006 | Consumes stock weight; autoclave and program recorded. Values not range-checked (GAP-515). |
| Media batch identification | DOCUMENTED | BR-MEDIA-001, BR-MEDIA-004 | Lot `{code}/{seq}/{yy}`; manufacturer lot copied from stock. |
| GPT / evaluation | PARTIAL | WF-MEDIA-002, BR-MEDIA-007..015, BR-MEDIA-031 | Spec stage engine (sterility, recovery, release) not used by code. |
| Sterility | NOT IMPLEMENTED | GAP-022 | Mentioned in UI subtitle; no sterility record. |
| Organism selection | DOCUMENTED | BR-MEDIA-017 | Cryovial or lyophilized disk, organism- and section-matched. |
| Inoculum | PARTIAL | C4 BR-MEDIA-010 | Inoculum stored as configuration text; no inoculum recorded per evaluation challenge in C5. |
| Recovery | DOCUMENTED | BR-MEDIA-010 | round(new/old x 100, 1); no band configured = always Conform (GAP-505). |
| Acceptance criteria | PARTIAL | BR-MEDIA-010..014 | Band from configuration; Indication judgment is manual; no approved rule available (GAP-021). |
| Activation / release | DOCUMENTED | WF-MEDIA-003, BR-MEDIA-019..022 | Signed; preparer/evaluator barred; final decision. Release does not check expiry (GAP-506). |
| Media status | PARTIAL | statuses, GAP-502 | Expired and Destroyed never assigned; OutOfStock not reversible. |
| Use of unreleased/expired media | PARTIAL | BR-MEDIA-024, GAP-500, GAP-516 | Differs per entry point (see Phase 6). |
| Re-test after NonConform | NOT IMPLEMENTED | WF-MEDIA-002, GAP-504 | No re-prepare/re-test action; results can be re-entered with no lock. |
| Storage / out of stock | DOCUMENTED | WF-MEDIA-004, BR-MEDIA-025..027 | Lot remaining quantity not tracked (GAP-510). |

### MOD-CRYO-001 Cryovials (reference strains)
| Checklist item | Status | Where | Note |
|---|---|---|---|
| Receipt | PARTIAL | WF-MATERIAL-001 | Lyophilized microorganism received as a stock lot with organism and ATCC No. |
| Lot | DOCUMENTED | BR-CRYO-005 | Batch code from material code. |
| Passage | NOT IMPLEMENTED | BR-CRYO-014, GAP-037 | No passage or generation field. |
| Cryopreservation | PARTIAL | WF-CRYO-001 | Vials, discs, storage text, identity confirmation panel; no freezing parameters. |
| Expiry | PARTIAL | BR-CRYO-006, GAP-509 | Entered by hand; not derived. Approval does not check it (BR-CRYO-015). |
| Usage | DOCUMENTED | WF-CRYO-003, BR-CRYO-009, -012 | Thaw decrements vials with a reason (client 5 to 500 chars; server accepts empty). Selection as a challenge source does not decrement. |
| Traceability | PARTIAL | traceability | Batch to material, identity rows, thaw events, evaluation challenge. Thawed vial is not linked to a specific use. |
| Identity confirmation | PARTIAL | BR-CRYO-003, GAP-503 | Media lot status checked; incubator and dates not validated. |
| Destruction | PARTIAL | BR-CRYO-010, GAP-507 | Unsigned, no reason, no review-trail event; not reversible. |

### MOD-MATERIAL-001 Materials Stock
| Checklist item | Status | Where | Note |
|---|---|---|---|
| Receipt | DOCUMENTED | WF-MATERIAL-001 | Required fields listed; no second-person check on receipt. |
| Stock deduction | DOCUMENTED | BR-MATERIAL-006 | Order: type, expiry, COA, stock. Last-modified fields only, no separate event. |
| Expiry blocking | PARTIAL | BR-MATERIAL-006, -013, GAP-508 | Blocks when an expiry is set; a lot without expiry never expires. |
| COA requirement | DOCUMENTED | BR-MATERIAL-007 | Dehydrated media, lyophilized microorganisms, supplements. |
| Documents | DOCUMENTED | BR-MATERIAL-010, -012 | SHA-256 verified on read; supersede/void unsigned. |
| Quarantine / release of received lots | NOT IMPLEMENTED | n/a | No receipt inspection or release status found. |

### MOD-EQUIP-001 Equipment Inventory
| Checklist item | Status | Where | Note |
|---|---|---|---|
| Equipment inventory | DOCUMENTED | WF-EQUIP-001, BR-EQUIP-001..007 | Unique code, section, operational status with comment. |
| Calibration | PARTIAL | BR-EQUIP-005, -012, -014 | Due date is a typed field; overdue is computed; certificates stored as documents; updating a certificate does not change the due date. |
| Qualification (IQ/OQ/PQ) | NOT IMPLEMENTED | BR-EQUIP-011, GAP-038 | No record. |
| Usage restrictions | PARTIAL | BR-EQUIP-007..009, -012, -013, GAP-501, GAP-517 | See Phase 6. Inventory status blocks only storage destinations; micro incubator checks read the separate master-data record. |
| Maintenance / out of service | PARTIAL | statuses | Status values InService, OutOfService, Retired with no transition rules. |

## Phase 6 GMP / data-integrity facts

### MOD-MEDIA-001
- Releasing or rejecting a lot requires Media.Release and the signer's password; the system records an electronic signature (meaning Approved or Rejected), a decision event and the comment, and archives a PDF of the lot record, in the same save as the state change. [src: BR-MEDIA-019..021; MediaReleaseService.cs]
- The user who prepared the lot, or who read any challenge result, cannot release or reject it ("You cannot approve a media lot you prepared or evaluated."). [src: roles; SegregationOfDutiesGuard.cs:L60-L66]
- The release decision is final; there is no un-release. [src: BR-MEDIA-022]
- A Conform evaluation does not release the lot; a NonConform evaluation sets the lot to QuarantineFailed. [src: BR-MEDIA-015, -016]
- Evaluation results can be entered again after the evaluation is Completed; nothing locks them and nothing reverts QuarantineFailed. [src: GAP-504]
- Evaluation actions record reader and time per challenge; no password is required. Incubation can be recorded with any incubator id and without a prior organism source. [src: BR-EQUIP-013, GAP-518]
- Preparation records the preparer, consumes dehydrated-media stock in the same save, and checks expiry, current COA and stock; it checks the autoclave only by type (no status or calibration check) and stores the expiry date, pH, weight and volume as typed. [src: BR-MEDIA-002, BR-MEDIA-033]
- Mark out of stock, storage add and storage move are not signed; out of stock has an optional comment; storage moves require a reason; each writes a review-trail event. [src: WF-MEDIA-004]
- Use of media in tests: individual selection refuses an unreleased, out-of-stock or rejected lot but does not check lot expiry; grouped selection checks status and expiry; shared TSB start checks expiry but not release status; pathogen confirmation checks status but not expiry. The lot lists offered on screen exclude unreleased, inactive and expired lots. [src: BR-MEDIA-023, -024; GAP-500, GAP-516]
- No code assigns the Expired or Destroyed status; expiry is read from the date. [src: BR-MEDIA-029]
- The record-change log records every create, update and delete with previous and new values, user and time; it holds no reason and no signature. [src: audit-trail]

### MOD-CRYO-001
- Approving or rejecting a batch requires Cryovials.Approve and the signer's password; the system records the signature, a decision event and comment, archives a PDF of the batch record, and bars the preparer from deciding. Rejection also marks the batch destroyed. [src: BR-CRYO-007, -008]
- Approval does not check the batch expiry date or any identity-confirmation result. [src: BR-CRYO-015]
- Thaw requires an approved, non-destroyed, unexpired batch with vials remaining; it records a thaw event (user, notes) and decrements the count; no password; the reason is required by the dialog (5 to 500 characters) but not by the server. [src: BR-CRYO-009, validations]
- Storage move requires a reason and an InService destination; it writes a review-trail event with old and new location. At preparation the storage equipment is stored as text only and is not validated by the server. [src: BR-CRYO-011, -017]
- Destruction needs no reason, no password and writes no review-trail event; the record-change log records the change with user and time; nothing clears it. [src: BR-CRYO-010, GAP-507]
- The user who prepared a batch cannot approve it; any other Cryovials.Approve holder (including one who thawed or moved it) can. [src: BR-CRYO-015]
- Selecting a cryovial as an organism source does not decrement vials, so a thawed vial is not linked to a specific evaluation. [src: BR-CRYO-012]
- No passage or generation number is recorded. [src: BR-CRYO-014]

### MOD-MATERIAL-001
- Receipt, edit and stock deduction are recorded in the record-change log; deduction also sets last-modified user and time on the material row. No electronic signature. No second-person check on receipt. [src: audit-trail; WF-MATERIAL-001]
- Document upload, supersede and void keep an access log, actor and time; supersede and void require a reason and Materials.DocumentControl but no password. SHA-256 is checked on read and a mismatch is refused. [src: BR-MATERIAL-010, -012]
- A material without an expiry date is never treated as expired. [src: GAP-508]
- Editing Quantity Received adjusts Quantity Remaining by the difference. [src: WF-MATERIAL-001]

### MOD-EQUIP-001
- Changing the operational status requires a comment and writes a status-history row (previous, new, comment, user, time); there are no transition rules. [src: BR-EQUIP-004]
- Registering or editing equipment (including the calibration due date) is recorded in the record-change log; no signature, no reason for date changes. [src: audit-trail]
- Calibration certificates: access log, supersede/void with reason and no password; hash verified on read and a mismatch is refused. [src: BR-EQUIP-010]
- Incubator checks at test time (individual, pathogen confirmation, shared TSB, grouped) read the master-data Equipment record (set point range, calibration due date), not the inventory status or date; media-evaluation incubation checks nothing; autoclave selection for media preparation checks type only. The two records are linked by code only, with the due date copied once. [src: BR-EQUIP-008, -009, -012..014, GAP-501, GAP-517]
- Inventory OutOfService or Retired blocks only media-storage and cryovial-storage-move destinations; HPLC/ICP/physicochemical paths (C6) do refuse such equipment. [src: BR-EQUIP-007, -012]
- No qualification record exists. [src: BR-EQUIP-011]

## Mock / placeholder / unverified data
None found. The Reviewer's pickers for media and cryovial preparation are read from code only and not run (GAP-514). Raw statements marked "not run" in C5 (GAP-516, GAP-518) remain unverified in the running application.

## Guide callouts
1. A Conform evaluation does not release a lot; a Section Head (or other Media.Release holder) must sign, and cannot be the preparer or evaluator. (BR-MEDIA-019, GAP-513)
2. Whether an expired or unreleased lot can start a test depends on the screen path; lists show only released, unexpired lots. (GAP-500, GAP-516)
3. Release does not check the lot expiry date. (GAP-506)
4. Evaluation results can be re-entered after completion; the correction procedure is unconfirmed. (GAP-504)
5. The sterility step is not in the application although screen text mentions it. (GAP-022)
6. A growth promotion challenge with no configured minimum recovery is always Conform. (GAP-505)
7. Media expiry date, cryovial expiry and media preparation values are typed by the user and not range-checked. (GAP-509, GAP-515)
8. The lot status never shows Expired; check the expiry date. Out of Stock cannot be undone and is unsigned. (GAP-502, GAP-512)
9. Cryovial destruction needs no reason or password and cannot be undone. (GAP-507)
10. The reason for a vial thaw is required on screen only. (BR-CRYO-009)
11. No passage number is recorded for reference strains. (GAP-037)
12. Cryovial approval does not check expiry or the identity-confirmation results. (BR-CRYO-015)
13. At cryovial preparation the freezer is stored as text and is not validated by the server. (GAP-503)
14. Equipment Inventory and Laboratory Configuration Equipment are two records linked by code; update both (procedure unconfirmed). Test-time incubator checks use the configuration record. (GAP-501, GAP-517)
15. Equipment qualification (IQ/OQ/PQ) is not recorded. (GAP-038)
16. An incubator rejected for overdue calibration shows the "set point is outside this medium's temperature range" message. (BR-EQUIP-009)
17. Reviewers may be unable to load the stock and equipment lists needed to prepare media or cryovial batches. (GAP-514)
18. Material lots without an expiry date never expire. (GAP-508)
19. The disc for a media evaluation is taken from stock when selected and is not returned. (GAP-511)

## Corrections made to the cluster file
1. WF-MEDIA-003 step 1: replaced "REQUIRES CONFIRMATION of exact dialog text" with the verified signature statements ("I am releasing media lot {lot} for use in routine testing." / "I am rejecting media lot {lot}.") [MediaPage.tsx:L344-L356] (corrected in Phase 5).
2. Materials Stock statuses/transitions: "supersede ... (signed reason, DocumentControl)" contradicted the verified "no password" statements; corrected to "reason, DocumentControl, no password".
Other spot checks (RecordIncubationAsync no incubator check, GPT Conform expression, thaw expiry test, release check order, no password in material/equipment document services) matched C5.
