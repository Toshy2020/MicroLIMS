# FS-CRY Functional Specification

| Field | Value |
|---|---|
| Document number | FS-CRY |
| Title | Cryovial batches and reference organisms functional specification |
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

## Scope and reading notes

- This document covers cryovial batches (preparation, approval, thaw, withdrawal, storage move) and the reference organism master. The use of a thawed vial in a media evaluation is in FS-MED. The biological waste destruction record that the code comments refer to is not in the files read and is not described. The batch summary PDF and Word endpoints (`CryovialSummaryService`) were not read and are not described.
- Organism names are master data (organisms and reference organisms) and are not listed in this document.
- Every statement was written from the code files named in each entry. Nothing was run.
- `Cryovial` has no version column and no method for cryovial batches calls `RecordVersion.EnsureCurrent` (no stale-form version check). `ReferenceOrganism` has a version column; where a method calls `EnsureCurrent` this is stated. Audit rows for inserts and updates come from the automatic capture in `MicroLimsDbContext`; neither entity is on the exclusion list.

## Functions

### FS-CRY-001
**Satisfies:** URS-CRY-001
**Function:** Permission and scope for batches. `CryovialController` needs `Cryovials.Manage` on all actions. `approve` and `move-storage` additionally need `Cryovials.Approve`. Actions that take a batch ID call `EnsureCryovialAccessAsync` (read access for the summary endpoints, write access for approve, destroy, thaw and move). The list is filtered to the sections from `GetReadableSectionIdsAsync` (null means all). `POST prepare` makes no scope call in the controller; scope comes from `MaterialService.ConsumeAsync`, which calls `EnsureMaterialAccessAsync` (write) on the stock lot, but only after the media lots of the identity panel were loaded and checked (FS-CRY-002). The `destroy` and `thaw` endpoints are open to every holder of `Cryovials.Manage`, not only to approvers.
**Code:** `backend/MicroLIMS.API/Controllers/CryovialController.cs` — `PrepareCryovials`, `Approve`, `Destroy`, `ThawVial`, `MoveStorage`, `GetAll`
**Verified by:** reading (not run)

### FS-CRY-002
**Satisfies:** URS-CRY-002, URS-CRY-003
**Function:** Prepare a cryovial batch. Inputs: stock lot ID, number of vials prepared, expiry date, storage condition text, physical check confirmed flag and text, identity-confirmation rows (media lot, incubator equipment, incubation start and end, observation text), number of discs used. One transaction. Rules in order: (1) physical check confirmed must be true; (2) the stock lot must exist and be of type lyophilized microorganism; (3) it must have an organism set; (4) for each panel row the media lot must exist, be released for use and not be OutOfStock or QuarantineFailed; (5) all panel media lots must be of the same laboratory section as the stock lot (`SectionMediaRule.EnsureLotsAsync`); (6) the panel must not be empty; (7) `MaterialService.ConsumeAsync` takes the discs off stock (FS-INV-005); (8) the code is built (FS-CRY-003). The batch is saved with approval status PendingReview, vials remaining equal to vials prepared, the organism name as a snapshot, the reference organism link of the stock lot, the manufacturer name, the preparing user and time. The method does not check that the number of vials or the discs used is above zero, that the expiry date lies in the future or after the preparation date, that the incubator exists, is an incubator, is in calibration or in service, that the incubation end is after the start, or that the observation text is not empty. The panel media lot expiry date is not checked (only released status). The batch is audited by the automatic capture.
**Code:** `backend/MicroLIMS.Application/Services/CryovialService.cs` — `PrepareCryovialsAsync`, `PrepareCryovialsCoreAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-123

### FS-CRY-003
**Satisfies:** URS-CRY-002
**Function:** Batch code. Same scheme as media lots: `{prefix}/{two-digit sequence}/{two-digit year}`, where the prefix is the stock lot code, or, if it has none, its material name reduced to letters and digits in upper case (`PreparedLotNumber.PrefixFor`). The sequence is one more than the highest already issued for that prefix and year. A unique-index clash on save is retried once with the next code; a second clash is reported and nothing is saved.
**Code:** `backend/MicroLIMS.Application/Helpers/PreparedLotNumber.cs` — `PrefixFor`, `NextAsync`; `backend/MicroLIMS.Application/Services/CryovialService.cs` — `PrepareCryovialsCoreAsync`
**Verified by:** reading (not run)

### FS-CRY-004
**Satisfies:** URS-CRY-004, URS-CRY-003
**Function:** Approve or reject a batch. Needs `Cryovials.Approve` and write section access. Inputs: approved yes or no, password, optional comment. One transaction. Preconditions: approval status is PendingReview. Segregation of duties: the user must not be the preparing user (`SegregationOfDutiesGuard.DidUserPrepareCryovialAsync`). The password is verified and an electronic signature with meaning Approved or Rejected and a review workflow event are written first (`ReviewGateService.SignAndLogAsync`; signature rules are in FS-SIG). Then the approval status becomes Approved or Rejected, with approved-by user and time. A rejection also sets the batch to destroyed. After saving, an archived PDF record of the batch is written. A rejection needs no comment. The identity-confirmation rows are not re-checked at approval; no check is made on what the observation text says. No stale-form version check.
**Code:** `backend/MicroLIMS.Application/Services/CryovialService.cs` — `ApproveAsync`, `ApproveCoreAsync`; `backend/MicroLIMS.Application/Services/SegregationOfDutiesGuard.cs` — `DidUserPrepareCryovialAsync`
**Verified by:** reading (not run)

### FS-CRY-005
**Satisfies:** URS-CRY-005
**Function:** Thaw a vial. Needs `Cryovials.Manage` and write section access. Inputs: batch ID and optional notes. The batch must be approved, not destroyed, and not expired by date, and must have vials remaining above zero (`CryovialThaw.Record`). One vial is taken off the remaining count and a thaw event is added with the injected-clock time, the user and trimmed notes. The same method is used for the thaw inside a media evaluation (FS-MED-005). The thawed vial may be used for 24 hours after thawing; this is a fixed value in the code and a destruction reminder is set at 48 hours, also fixed (CONF-124). This method does not use a transaction wrapper and has no stale-form check. Whether two simultaneous thaws of the last vial are both refused was not checked, because `Cryovial` has no version column.
**Code:** `backend/MicroLIMS.Application/Services/CryovialService.cs` — `ThawVialAsync`; `backend/MicroLIMS.Application/Services/CryovialThaw.cs` — `Record`, `EnsureUsable`
**Verified by:** reading (not run)
**Conflicts:** CONF-124

### FS-CRY-006
**Satisfies:** URS-CRY-006
**Function:** Withdraw a batch. Needs `Cryovials.Manage` and write section access. The method sets the batch to destroyed and saves. It asks for no reason, no password and no signature. It does not check the approval status or whether the batch is already destroyed, and the user ID is not passed to the method. The only audit row is the one from the automatic capture; this method does not set the context's `CurrentUserId`, and whether the audit row names the acting user was not checked. The code comment says physical destruction is recorded and signed elsewhere (`BiologicalWasteDestructionService`, not read for this document). Once destroyed, `CryovialThaw.EnsureUsable` refuses further thaws, and `SelectThawedVialAsync` in the media evaluation refuses a vial of that batch because it calls `EnsureUsable` too. The code comment says all vials of the batch then become eligible for destruction.
**Code:** `backend/MicroLIMS.Application/Services/CryovialService.cs` — `DestroyAsync`
**Verified by:** reading (not run)

### FS-CRY-007
**Satisfies:** URS-CRY-007
**Function:** Move cryovial storage. Needs `Cryovials.Approve` and write section access. Inputs: reason (required), and exactly one of an equipment ID or a free-text location. Preconditions: batch is approved and not destroyed. A chosen equipment item is read from the equipment inventory and must have status InService. The storage condition text of the batch is replaced by the new location text and the equipment link is set. A review workflow event StorageLocationChanged with old location, new location and reason is logged. One transaction. No password is asked. No check is made that the equipment is in calibration. The move changes the storage condition text, so the original storage condition entered at preparation is replaced in the batch row (the old text is kept only in the log comment and the audit row).
**Code:** `backend/MicroLIMS.Application/Services/CryovialService.cs` — `MoveStorageAsync`, `MoveStorageCoreAsync`
**Verified by:** reading (not run)

### FS-CRY-008
**Satisfies:** URS-CRY-008
**Function:** Reference organism list and create. `GET api/masterdata/reference-organisms` needs only an authenticated user and returns every strain of every laboratory section (optionally active only or one organism), each flagged with whether the user may edit it (the user's configurable sections include the owner section, or the user is unrestricted) and whether stock exists for it (a stock lot references it). Create needs `MasterData.Manage`. Inputs: code, organism ID, culture collection, strain number, optional owner section. Rules: the code is required, trimmed and made upper case, must start with the fixed prefix `ORG-` with something after it, and be at most 50 characters; the culture collection must be a defined value; the strain number is required unless the culture collection is Other and is at most 50 characters; the organism must exist; the code must be unique; and the pair of culture collection and strain number must be unique (compared in upper case). The owner section is resolved with configure access (`ResolveSectionForCreateAsync`). The strain is created active with creator and time from the injected clock. No signature.
**Code:** `backend/MicroLIMS.Application/Services/ReferenceOrganismService.cs` — `GetAllAsync`, `CreateAsync`, `ValidateAndNormalize`, `EnsureUniqueAsync`; `backend/MicroLIMS.API/Controllers/ReferenceOrganismController.cs` — `GetAll`, `Create`
**Verified by:** reading (not run)

### FS-CRY-009
**Satisfies:** URS-CRY-008, URS-CRY-009
**Function:** Reference organism update. Needs `MasterData.Manage`; the strain must belong to a section in the user's configurable sections, otherwise the method refuses (`LoadWithAccessAsync`). The version check `RecordVersion.EnsureCurrent` is called. The same validation as create applies. If any stock lot or cryovial batch references the strain, the code cannot be changed here (the user is told to use the code change with signature) and the organism, culture collection and strain number cannot change. Without stock, all of these may be changed freely and no reason is asked. Last-modified user and time are set. Changes are audited by the automatic capture (old and new values).
**Code:** `backend/MicroLIMS.Application/Services/ReferenceOrganismService.cs` — `UpdateAsync`, `LoadWithAccessAsync`, `HasStockAsync`
**Verified by:** reading (not run)

### FS-CRY-010
**Satisfies:** URS-CRY-009
**Function:** Change the code of a reference organism. Needs `MasterData.Manage` and owner-section access. Inputs: new code, reason, password. A reason is required; the new code is required, upper-cased, must differ from the current code, must carry the `ORG-` prefix and must be unique. The user's password is verified and an electronic signature with meaning MasterDataChanged is written with the reason and old and new code (signature rules are in FS-SIG), then the code is changed, then an explicit audit event with the old and new code and the reason is recorded. One transaction. This is allowed whether or not stock exists. Existing stock lots and cryovial batches keep their own code snapshot (code comment). This method does not call `RecordVersion.EnsureCurrent` (no stale-form version check).
**Code:** `backend/MicroLIMS.Application/Services/ReferenceOrganismService.cs` — `ChangeCodeAsync`, `ChangeCodeCoreAsync`
**Verified by:** reading (not run)

### FS-CRY-011
**Satisfies:** URS-CRY-010
**Function:** Deactivate or reactivate a reference organism. Needs `MasterData.Manage` and owner-section access. The flag is set and last-modified user and time are stored. No reason, no signature and no stale-form version check are asked. The method does not check whether stock lots or batches reference the strain. When a stock lot is received or edited against a strain, `MaterialService` refuses an inactive strain unless the lot already points at it (FS-INV-003). `ResolveForLotAsync` gives the same refusal for stock receipt.
**Code:** `backend/MicroLIMS.Application/Services/ReferenceOrganismService.cs` — `SetActiveAsync`, `ResolveForLotAsync`
**Verified by:** reading (not run)
