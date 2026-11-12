# FS-MED Functional Specification

| Field | Value |
|---|---|
| Document number | FS-MED |
| Title | Media preparation, evaluation and release functional specification |
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

- This document covers media lot preparation, the auto-assigned media evaluation (growth promotion, inhibition, indication, enrichment), the release decision, out-of-stock marking and storage entries. Media master data (products, configurations, incubation conditions) is in `MediaMasterDataController` and belongs to FS-MDM. Reports on media lots are in `ReportingController` and belong to FS-RPT. Use of a released lot in a test is in FS-MIC and FS-PAT.
- Every statement was written from the code files named in each entry. Nothing was run. Pharmacopoeia chapters are not cited here.
- Recovery bands, incubation temperatures and hours, challenge organisms and the evaluation type are not in the code. They are configured by the Section Head (media configuration and incubation condition).
- The `Media`, `MediaEvaluation` and `MediaEvaluationChallenge` entities have no version column. No method here calls `RecordVersion.EnsureCurrent` (no stale-form version check). Audit rows for every insert and update come from the automatic capture in `MicroLimsDbContext`; none of these entities is on the exclusion list.
- Earlier specification text (`documents/DesignSpecification/BusinessRules.md` section 7) describes a different GPT engine. See CONF-120.

## Functions

### FS-MED-001
**Satisfies:** URS-MED-001
**Function:** Permission and section scope. `MediaController` needs the permission `Media.Prepare` on the whole controller. The release endpoint additionally needs `Media.Release`. `MediaEvaluationController` needs `TestWorkflow.Execute`. List endpoints (lots, released lots, awaiting approval, expiring, evaluations) are filtered to the sections returned by `GetReadableSectionIdsAsync`; a null result means all sections. Endpoints that take a lot ID (release, mark out of stock, storage, summary, PDF, Word) call `EnsureMediaAccessAsync` first, with read access for read endpoints and write access for the others. Evaluation endpoints that take a challenge ID call `EnsureMediaEvaluationChallengeAccessAsync` (write for record actions, read for the thawed-vial list); the evaluation read calls `EnsureMediaEvaluationAccessAsync`. `POST api/media` (prepare) makes no section check in the controller. Section scope there comes only from the stock lot, which `MaterialService.ConsumeAsync` checks with `EnsureMaterialAccessAsync` (write). No check is made that the chosen autoclave belongs to the same section as the stock lot (FS-MED-002).
**Code:** `backend/MicroLIMS.API/Controllers/MediaController.cs` — `Prepare`, `DecideRelease`, `MarkOutOfStock`, `GetAll`; `backend/MicroLIMS.API/Controllers/MediaEvaluationController.cs` — `RecordResult`, `GetById`
**Verified by:** reading (not run)

### FS-MED-002
**Satisfies:** URS-MED-002, URS-MED-003, URS-MED-004
**Function:** Prepare a media lot. Inputs: stock lot (material) ID, total weight, total volume text, autoclave equipment ID, autoclave programme text, load type text, temperature, cycle time, cycle number, pH, expiry date, optional additives. The method runs in one database transaction. Rules in order: (1) the equipment must exist and be of type Autoclave, otherwise the method refuses; (2) `MaterialService.ConsumeAsync` (FS-INV-005) checks the stock lot and takes the total weight off its remaining quantity; (3) the stock lot must be linked to a media product; (4) additives are built (FS-MED-003); (5) the media product must exist and a media configuration must exist for it (one per product); (6) a lot number is built (FS-MED-004); (7) the lot is saved with status Prepared, approval status PendingReview, not released for use, the stock lot's manufacturer name and batch number, the preparing user and the preparation time. An evaluation of the configured evaluation type is added with status Assigned and one challenge per configured challenge (organism, role, expected description, initial inoculum). A product with no configured challenges still gets an evaluation with no challenges; the code comment says this is allowed. Autoclave programme text, load type, temperature, cycle time, cycle number and pH are stored as sent. They are not compared with the configured autoclave programmes (`AutoclaveProgram`) and no range check is made on them. No check is made that the autoclave has a calibration due date in the future or is in service. No check is made that the expiry date is after the preparation date, and the total weight is not checked for being above zero in this method. The lot is audited by the automatic capture.
**Code:** `backend/MicroLIMS.Application/Services/MediaPreparationService.cs` — `PrepareAsync`, `PrepareCoreAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-123

### FS-MED-003
**Satisfies:** URS-MED-003
**Function:** Additives on a media lot (optional, none never blocks). Each additive has a kind: StockLot or PreparedSolution. Common rules: quantity above zero; unit text present and at most 20 characters. StockLot: a stock lot ID is needed, a solution ID is not allowed, the same lot may be listed once, the lot must exist, must not be a dehydrated media or lyophilized microorganism lot, and must belong to the same laboratory section as the media stock lot. The quantity is then taken off the lot through `MaterialService.ConsumeAsync` (FS-INV-005), and the lot's own unit is recorded. PreparedSolution: a solution preparation ID is needed, a stock lot ID is not allowed, the same preparation may be listed once, the preparation must belong to the same section, its effective status must be Prepared (not Expired or any other status), and its approval status must not be PendingReview or Rejected. Nothing is deducted for a prepared solution; the pick is only recorded. The additive name and code are stored as a snapshot.
**Code:** `backend/MicroLIMS.Application/Services/MediaPreparationService.cs` — `BuildAdditivesAsync`
**Verified by:** reading (not run)

### FS-MED-004
**Satisfies:** URS-MED-002
**Function:** Lot number. The number is `{product code}/{two-digit sequence}/{two-digit year}`. The prefix is the code of the media product (`MediaPreparationService`) and the year comes from the injected clock. The sequence is one more than the highest sequence already issued with the same prefix and year (not a count), so gaps in older numbers are not reused. If two preparations pick the same number, the unique index refuses the second save; the method then takes the next number and saves again. A second refusal in a row is reported to the user as an error and nothing is saved.
**Code:** `backend/MicroLIMS.Application/Helpers/PreparedLotNumber.cs` — `NextAsync`; `backend/MicroLIMS.Application/Services/MediaPreparationService.cs` — `PrepareCoreAsync`
**Verified by:** reading (not run)

### FS-MED-005
**Satisfies:** URS-MED-005
**Function:** Choose the organism source for a challenge. Three actions: (a) select an already thawed vial; (b) thaw a vial from a cryovial batch and select it in one step; (c) select a lyophilized disk. A challenge that already has a recorded outcome cannot change its source (actions a and b). Common rules (a and b): the cryovial batch must belong to the same laboratory section as the evaluated media lot; the batch must be approved, not destroyed and not expired (`CryovialThaw.EnsureUsable`); the batch organism must be the challenge organism. For (a) the thawed vial must not have been destroyed and must have been thawed less than 24 hours ago (`CryovialThaw.EnsureThawedVialUsable`; 24 hours is a fixed value in the code, see CONF-124). No vial is consumed by (a); one thawed vial may serve any number of challenges. For (b) the fit is checked before the vial is taken, then `CryovialThaw.Record` takes one vial from the batch (refused when none remain) and writes a thaw event, and the challenge is linked to it, in one transaction. For (c): if the same disk is already selected the method returns without change. Otherwise the user needs write access to the material, the material must be in the same section as the evaluated lot, must be a lyophilized microorganism lot, must be of the challenge organism, must not be expired, and must have remaining quantity above zero. `MaterialService.ConsumeAsync` then takes one disc (FS-INV-005). Choosing a different disk later does not give the earlier disc back, and the challenge outcome is not checked in (c), so the source of a challenge that already has a result can be changed through the disk action. The cryovial and disk links exclude each other. The organism name shown in refusal messages comes from master data.
**Code:** `backend/MicroLIMS.Application/Workflows/MediaEvaluationEngine.cs` — `SelectThawedVialAsync`, `ThawAndSelectVialAsync`, `SelectLyophilizedDiskAsync`, `EnsureCryovialFitsChallengeAsync`; `backend/MicroLIMS.Application/Services/CryovialThaw.cs` — `EnsureUsable`, `EnsureThawedVialUsable`, `Record`
**Verified by:** reading (not run)
**Conflicts:** CONF-124

### FS-MED-006
**Satisfies:** URS-MED-006
**Function:** Record the incubation of one challenge. Inputs: challenge ID and incubator equipment ID. The temperature range and hour range are read from the incubation condition of the media configuration, never from the client; the method refuses when no condition is configured. An incubation row is added with the start time from the injected clock, the start user, the temperature text "min-max", the duration text "min-max", and an expected reading time equal to the start plus the configured minimum hours. The challenge is linked to it and an evaluation in Assigned moves to InProgress. The method does not check that the equipment is an incubator, that it exists, that its set point lies in the configured range, that its calibration is current, or that it is in service. The method does not check that an organism source has been chosen (FS-MED-005) before incubation. Calling it again for the same challenge adds a second incubation row and replaces the link.
**Code:** `backend/MicroLIMS.Application/Workflows/MediaEvaluationEngine.cs` — `RecordIncubationAsync`, `RecordIncubationCoreAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-123

### FS-MED-007
**Satisfies:** URS-MED-006, URS-MED-007
**Function:** Record a challenge result. Preconditions: an incubation exists for the challenge, and the injected clock time is not before the expected reading time (start plus the configured minimum hours). Then the rule depends on the evaluation type and challenge role, configured by the Section Head. GrowthPromotion: both counts are required; the old count may not be zero; exactly one of a linked reference lot or a free-text reference description is required; a linked reference lot must exist and belong to the same section as the evaluated lot; recovery percent is new count divided by old count times 100, rounded to one decimal place with `Math.Round(value, 1)` (default midpoint rule of .NET, not an explicit mode); the outcome is Conform when no minimum recovery is configured, or when the recovery is not below the configured minimum and not above the configured maximum (both ends included). IndicationInhibition with role Inhibition: growth observed is required; Conform when no growth was observed. IndicationInhibition with role Indication: an observed description and a Conform or NonConform judgement are required; the outcome is the analyst's judgement, with no comparison with the expected description. EnrichmentCharacteristics: turbid or clear is required; Conform when turbid. Any other combination is refused. No check is made that the counts are whole numbers or not negative. The read time and reading user are stored on the challenge, and the incubation is closed with the same time and user. The method does not check the section scope itself; the controller does (FS-MED-001). The method makes no check that the reading user differs from the user who prepared the lot or chose the organism source (FS-MED-009 checks the release user).
**Code:** `backend/MicroLIMS.Application/Workflows/MediaEvaluationEngine.cs` — `RecordResultAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-121, CONF-122

### FS-MED-008
**Satisfies:** URS-MED-004, URS-MED-008
**Function:** Evaluation roll-up, inside `RecordResultAsync`. When every challenge of the evaluation has an outcome, the evaluation status becomes Completed, with completed time and user from the last reader. The evaluation outcome is Conform only when every challenge is Conform, otherwise NonConform. On NonConform the media lot status becomes QuarantineFailed immediately. On Conform the lot stays PendingReview and not released; it appears in the awaiting-approval queue (lots with a Completed and Conform evaluation and approval status PendingReview, filtered by the user's readable sections). An evaluation with no challenges is never completed by this method. Nothing here sets the lot to released.
**Code:** `backend/MicroLIMS.Application/Workflows/MediaEvaluationEngine.cs` — `RecordResultAsync`; `backend/MicroLIMS.Application/Services/MediaReleaseService.cs` — `GetAwaitingApprovalAsync`
**Verified by:** reading (not run)

### FS-MED-009
**Satisfies:** URS-MED-009, URS-MED-004
**Function:** Release or reject a media lot. Needs the permission `Media.Release` and write section access to the lot. Inputs: lot ID, approved yes or no, password, optional comment. One transaction. Preconditions: the lot approval status is PendingReview; an evaluation exists. To approve, the evaluation must be Completed and its outcome Conform; to reject, any state is allowed. Segregation of duties: the user must not be the user who prepared the lot or who read any challenge of its evaluation (`SegregationOfDutiesGuard.DidUserPrepareOrEvaluateMediaAsync`). The user's password is then verified and an electronic signature with meaning Approved or Rejected, a review workflow event and the comment are written (`ReviewGateService.SignAndLogAsync`; signature rules are in FS-SIG). On approval: approval status Approved, released for use true, lot status Active. On rejection: approval status Rejected, lot status QuarantineFailed. Approved-by user and time are stored. After the save, an archived PDF record of the lot is written. A rejection needs no comment. The method does not call `RecordVersion.EnsureCurrent` (no stale-form version check). The method does not check that the lot is not expired and does not check a sterility result; see CONF-120.
**Code:** `backend/MicroLIMS.Application/Services/MediaReleaseService.cs` — `DecideAsync`, `DecideCoreAsync`; `backend/MicroLIMS.Application/Services/SegregationOfDutiesGuard.cs` — `DidUserPrepareOrEvaluateMediaAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-120

### FS-MED-010
**Satisfies:** URS-MED-010
**Function:** Mark a lot out of stock. Needs `Media.Prepare` and write section access. Precondition: the lot is released for use and its status is Active; otherwise the method refuses. The status becomes OutOfStock and a review workflow event (ApprovalDecisionMade, comment optional with a default text) is logged. No password or signature is asked. There is no endpoint in these files that returns a lot from OutOfStock. No method in the files read sets the lot status to Expired or Destroyed; expiry is tested by date where used.
**Code:** `backend/MicroLIMS.Application/Services/MediaPreparationService.cs` — `MarkOutOfStockAsync`
**Verified by:** reading (not run)

### FS-MED-011
**Satisfies:** URS-MED-011
**Function:** Storage entries of a media lot. Add: needs write access; inputs form (Plates, AgarBottles, BrothBottles or Tubes), quantity above zero, and exactly one of an inventory equipment ID or a free-text location. The lot must be released for use and Active. A chosen equipment item must exist in the equipment inventory and have status InService. An existing entry with the same lot, form and location gets its quantity increased. Otherwise a new entry is created. A review workflow event StorageLocationChanged is logged. Move: needs write access; a reason is required, the quantity must be above zero and not above the source entry quantity, exactly one destination is required, and the destination must differ from the source. Same lot rules and equipment rule as Add. The quantity is merged into an existing entry at the destination or the source entry is moved or reduced. No password is asked. No check is made that the chosen equipment is in calibration. Both are in one transaction.
**Code:** `backend/MicroLIMS.Application/Services/MediaStorageService.cs` — `AddStorageEntryAsync`, `MoveStorageEntryAsync`
**Verified by:** reading (not run)

### FS-MED-012
**Satisfies:** URS-MED-001, URS-MED-012
**Function:** Queries. `GetAllAsync` lists lots (with material and additives) in the user's readable sections, newest first. `GetReleasedAsync` lists lots with released-for-use true in those sections; unless the caller asks to include expired lots, only lots with status Active and an expiry date after now are returned; optional filters for stock lot and an excluded lot. `GetExpiringAsync` lists lots with status Active and an expiry date on or before now plus the number of days asked (default 7), so lots already past expiry are included, in readable sections only, with the evaluation shown as Passed, Failed or Pending. `MediaEvaluationService.GetAllAsync` and `GetThawedVialOptionsAsync` apply the same section filter; the thawed-vial list shows vials thawed in the last 24 hours from approved, unexpired, undestroyed batches of the challenge organism in the same section. The days value is not limited in range. These methods write nothing.
**Code:** `backend/MicroLIMS.Application/Services/MediaPreparationService.cs` — `GetAllAsync`, `GetReleasedAsync`; `backend/MicroLIMS.Application/Services/MediaExpiryService.cs` — `GetExpiringAsync`; `backend/MicroLIMS.Application/Services/MediaEvaluationService.cs` — `GetAllAsync`, `GetThawedVialOptionsAsync`
**Verified by:** reading (not run)
