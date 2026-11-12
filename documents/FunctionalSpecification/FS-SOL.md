# FS-SOL Functional Specification

| Field | Value |
|---|---|
| Document number | FS-SOL |
| Title | Solution preparation and titrant standardisation functional specification |
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

- This document covers `SolutionPreparationController`, `SolutionPreparationService`, and the two services the controller also calls: `SolutionPreparationReleaseService` and `TitrantStandardizationService`. The solution master (recipes) is configured by the Section Head and was not read. The background worker that calls `ExpireDueAsync` (`SolutionPreparationExpiryWorker`) was not opened; its schedule is taken from the code comment. `MaterialService.ConsumeAsync` (stock deduction) was not read beyond its call site.
- Every statement was written from the code files named in each entry. Nothing was run. Pharmacopoeia chapters are pointers only (to be confirmed by QA).
- Recipes, shelf life, pH target and tolerance, sterilisation need, release need, titrant strength, factor range, maximum RSD, replicate count and validity days are configured by the Section Head in the solution master and are copied into each preparation when it starts (`RecipeSnapshotJson`). Later changes of the master do not change a started preparation.
- Section scope: reads use the user's readable sections; changes use the user's member sections for every role (`GetSectionIdsAsync`); a preparation outside the scope is reported as "not found".
- All preparation, status history, standardisation and replicate rows are audited by the context's automatic capture; none is in `AuditCaptureExcludedTypes`. Every table also has an automatic `xmin` concurrency token. Only `SolutionPreparationReleaseService.DecideCoreAsync` calls `RecordVersion.EnsureCurrent` (stale-form check). The other methods in this document have no stale-form version check.
- Methods that sign (`CompleteAsync`, `DecideAsync`, `StandardizeAsync`) validate and plan first, then call `IElectronicSignatureService.SignAsync`, and only then change data, so a wrong password leaves the data unchanged (see FS-SIG).

## Functions

### FS-SOL-001
**Satisfies:** URS-SOL-001, URS-SOL-009
**Function:** Permissions and read endpoints of `SolutionPreparationController`. Reads (list with optional status and type filter, `available`, `usable`, `by-lot/{materialId}`, detail, lot options, source preparations, standardisations and their pickers) need only a signed-in user; the services limit them to the readable sections. Start, save, complete, cancel, discard and standardise need `Solutions.Prepare`. Release needs `Solutions.Release`. `GetAvailableAsync` lists preparations of one solution master (and, if given, one method) that are Prepared, not past their expiry, and either need no release or are Approved. `GetUsableAsync` does the same for the types Diluent, Reagent and Neutralizer, optionally for one section. `GetByLotAsync` lists the preparations that have a component picked from the given stock lot. The status shown in the lists is the effective status (Prepared is shown as Expired when the expiry time has passed, using the injected clock). The lot trace reads the section scope of the user but does not check the lot's own section. No endpoint here reads an `If-Match` header.
**Code:** `backend/MicroLIMS.API/Controllers/SolutionPreparationController.cs` — class `SolutionPreparationController`; `backend/MicroLIMS.Application/Services/SolutionPreparationService.cs` — `GetAllAsync`, `GetByIdAsync`, `GetAvailableAsync`, `GetUsableAsync`, `GetByLotAsync`, `EnsureAccessAsync`
**Verified by:** reading (not run)

### FS-SOL-002
**Satisfies:** URS-SOL-002
**Function:** Start a preparation (`POST api/solution-preparations`; input: solution master ID, optional HPLC method ID). The master must exist and be in the user's member sections (otherwise not found) and be active. For a MobilePhase master a method is required; it must exist, belong to the same section as the master, be active, and use this mobile phase solution. For other types a method is refused. The recipe is copied as JSON, and the components (order, kind, master entry, source solution, text, recipe quantity and unit) are copied from the master. The status is InProgress, the starting user and time come from the injected clock, and a status history row is added. No signature is asked at start.
**Code:** `backend/MicroLIMS.Application/Services/SolutionPreparationService.cs` — `StartAsync`
**Verified by:** reading (not run)

### FS-SOL-003
**Satisfies:** URS-SOL-003
**Function:** Save the picks (`PUT api/solution-preparations/{id}`). The preparation must be InProgress and in write scope. Per component: a Text line takes nothing. A Material line takes a stock lot and a quantity used (above zero); the lot must be in the preparation's section and pass `LotUsability.Check` (linked to the line's master entry, not awaiting release, not rejected, not expired by laboratory-local date, stock above zero, stock at least the quantity). A PreparedSolution line takes a prepared bottle (and an optional quantity above zero, never deducted); the bottle must be of the same section and the line's source solution, with the effective status Prepared and not expired, and not awaiting release or rejected (`CheckSource`). A lot given for a prepared-solution line, or a bottle for a material line, is refused. The final volume must be above zero and the measured pH from 0 to 14, when given. The autoclave record is stored only when the recipe needs sterilisation (a record is refused otherwise): text fields at most 100 characters, temperature, cycle time and cycle number above zero if given, and the equipment must be of type Autoclave. The option lists (`GetLotOptionsAsync`, `GetSourcePreparationsAsync`) return the lots or bottles of the line with a usable flag and the reason; they do not hide unusable ones.
**Code:** `backend/MicroLIMS.Application/Services/SolutionPreparationService.cs` — `SaveAsync`, `CheckSource`, `ApplySterilizationAsync`, `GetLotOptionsAsync`, `GetSourcePreparationsAsync`; `backend/MicroLIMS.Application/Helpers/LotUsability.cs` — `Check`
**Verified by:** reading (not run)

### FS-SOL-004
**Satisfies:** URS-SOL-004
**Function:** Complete a preparation (`POST api/solution-preparations/{id}/complete`; inputs: password, comment). Preconditions: InProgress; every Material line has a lot and a quantity; every PreparedSolution line has a bottle; the final volume is entered; if the recipe has a pH target, a measured pH is required, and if the recipe also has a pH tolerance, the measured pH must not differ from the target by more than the tolerance (a difference equal to the tolerance passes). If the recipe has a pH target and no tolerance, only the presence of a measured pH is checked. If the recipe needs sterilisation, the whole autoclave record is required and the equipment must be an autoclave. The lots are checked again with `LotUsability.Check` and the bottles with `CheckSource`. The method then signs (meaning PreparationConfirmed, bound to the preparation), deducts each lot's quantity through `MaterialService.ConsumeAsync`, generates the code (`SolutionPreparationCode`, from the solution type, the method abbreviation for a mobile phase, and the laboratory-local date), sets the preparer and time, sets the expiry to the preparation time plus the recipe's shelf life (hours or days), sets the status Prepared, sets the approval status PendingReview when the recipe needs release (otherwise empty), adds a status history row with the comment, and saves once. A code clash is reported as an error asking the user to sign again; it is not retried. The unit of the quantity used is not converted to the lot's unit by this method. The quantity used is typed by the analyst and compared only with the lot stock.
**Code:** `backend/MicroLIMS.Application/Services/SolutionPreparationService.cs` — `CompleteAsync`, `ValidateSterilizationAsync`, `ShelfLifeExpiry`
**Verified by:** reading (not run)

### FS-SOL-005
**Satisfies:** URS-SOL-006
**Function:** Cancel, discard and expiry. `CancelAsync` needs the preparation InProgress and a reason (trimmed, required, at most 500 characters); it sets Cancelled and adds a status history row. No stock was deducted before completion, so none is returned. `DiscardAsync` needs the effective status Prepared (not expired) and a reason; it sets Discarded and adds a history row; stock already deducted is not returned. Neither asks for a signature. `ExpireDueAsync` finds Prepared preparations whose expiry time is not after the injected clock, sets them Expired and adds a history row with the reason "Expired automatically" and no user; the code comment says a background worker calls it every 5 minutes, and every read also treats an overdue Prepared preparation as Expired.
**Code:** `backend/MicroLIMS.Application/Services/SolutionPreparationService.cs` — `CancelAsync`, `DiscardAsync`, `ExpireDueAsync`; `backend/MicroLIMS.Application/DTOs/Responses/SolutionPreparationResponse.cs` — `EffectiveStatusOf`
**Verified by:** reading (not run)

### FS-SOL-006
**Satisfies:** URS-SOL-005
**Function:** Release gate (`POST api/solution-preparations/{id}/release`; input: approved flag, comment, password; permission `Solutions.Release`). `DecideAsync` runs in one transaction. It needs write scope (otherwise not found), a record version that matches the `If-Match` version (`RecordVersion.EnsureCurrent`), the effective status Prepared, an approval status of PendingReview (a preparation that needs no release, or was already decided, is refused), a comment when rejecting (at most 500 characters), and a user who did not prepare the preparation (`SegregationOfDutiesGuard.DidUserPrepareSolutionAsync`). It signs through `ReviewGateService.SignAndLogAsync` (meaning Approved or Rejected, with a review workflow event) and then sets the approval status Approved or Rejected, the releasing user, time, comment and signature, and adds a status history row (status stays Prepared). A preparation that is PendingReview or Rejected is not offered by the availability queries and is refused as a prepared-solution source; it is not refused where a consumer checks only the effective status. `HplcRunService.StartRunAsync` (mobile phase per channel) and `TitrationRecorder.LoadPreparationAsync` (titrant and excess titrant) check that the effective status is Prepared and do not read the approval status, so a mobile phase or titrant that is PendingReview or Rejected is not refused by those methods (CONF-101).
**Code:** `backend/MicroLIMS.Application/Services/SolutionPreparationReleaseService.cs` — `DecideAsync`, `DecideCoreAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-101

### FS-SOL-007
**Satisfies:** URS-SOL-007, URS-SOL-008
**Function:** Standardise a titrant (`POST api/solution-preparations/{id}/standardizations`; input: replicates, password, comment, optional temperature; permission `Solutions.Prepare`). The preparation must be a Titrant in write scope with the effective status Prepared, and its saved recipe must carry the standardisation mode, nominal strength, strength unit, replicate count, factor minimum and maximum, and maximum RSD (otherwise "not configured"). The number of replicates must equal the saved replicate count. Per replicate the titrant volume must be above zero; the blank is required (any value) when the recipe needs one and refused when it does not. PrimaryStandard mode: a standard lot and weight (mg) are required, reference fields refused; the lot must be in the preparation's section, pass `LotUsability.Check` against the recipe's standard entry, be of type PrimaryStandard and have a purity; the factor = (standard weight times purity divided by 100) divided by ((titrant volume minus blank) times the equivalence in mg per mL), with the standard weight above zero, purity above 0 and not above 100, equivalence above zero, blank not negative and titrant volume above the blank. Against-a-volumetric-solution mode: a reference preparation and volume are required, standard-lot fields refused; the reference preparation must be of the same section and of the recipe's reference solution, effectively Prepared, with the same strength unit and a nominal strength, and its own current factor must be Valid, or BeforeEachUse standardised today (laboratory-local); the factor = (reference volume times reference factor times reference nominal strength) divided by ((titrant volume minus blank) times the titrant's nominal strength). A temperature, when given, must be from minus 20 to 60 C. `TitrationEngine.Evaluate` takes the mean factor, flags each replicate factor below the minimum or above the maximum (limits included), and flags an RSD (sample standard deviation divided by the mean times 100, only with 2 or more replicates) above the maximum RSD; the record Passes only with no flag. Standard weights are summed per lot, converted from mg to grams or kilograms (other units refused) and re-checked against stock before signing. The method signs (meaning TitrantStandardized), stores the record with the settings snapshot, mean factor, RSD, pass flag, reasons, user, time, temperature and replicates, sets the valid-until time to the time plus the saved validity days (empty when validity days is 0 or less), and reduces the lot quantities in the same save. A failed record is stored and signed like a passed one. The factor range, maximum RSD, replicate count, equivalence, strength and validity days come from the recipe; none is written in the code.
**Code:** `backend/MicroLIMS.Application/Services/TitrantStandardizationService.cs` — `StandardizeAsync`; `backend/MicroLIMS.Application/Helpers/TitrationEngine.cs` — `PrimaryStandardFactor`, `AgainstVolumetricSolutionFactor`, `Evaluate`
**Calculations:** CALC-016, CALC-017, CALC-018
**Verified by:** reading (not run)

### FS-SOL-008
**Satisfies:** URS-SOL-008
**Function:** Current factor and pickers. `ComputeCurrentFactor` takes the latest record that passed (by standardisation time, then ID); none gives the state NotStandardized; a record with no valid-until time gives BeforeEachUse; otherwise Valid when the valid-until time is after the injected clock and Due when it is not. A failed record never becomes current, so an older passed record stays current. `GetCurrentFactorAsync` has no access check by design (code comment). `GetForPreparationAsync`, `GetStandardLotOptionsAsync` (primary standard lots of the preparation's section for the recipe's standard entry, with usable flag and reason) and `GetReferenceOptionsAsync` (prepared, unexpired preparations of the reference solution in the same section whose state is Valid) check read access to the preparation. The titration assay reads these states (FS-PHY-010).
**Code:** `backend/MicroLIMS.Application/Services/TitrantStandardizationService.cs` — `ComputeCurrentFactor`, `GetCurrentFactorAsync`, `GetForPreparationAsync`, `GetStandardLotOptionsAsync`, `GetReferenceOptionsAsync`
**Verified by:** reading (not run)
