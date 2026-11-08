# Phase 4a — After-cleaning receipt (previous product, techniques) and UV scan

Spec decisions 5, 9, 11, 13. HPLC cleaning-residue mode and ICP limits are Phase 4b.

## Technique of a part test (no new field)
Derived from the test's `WorkflowType`: `HplcMethodAssay` → HPLC, `IcpMethodAssay` → ICP,
anything else on a physicochemical test → UV. Micro tests have no technique.
One static helper `CleaningTechniques.Of(WorkflowType)` in Application/Helpers.

## Backend (job A)
- `Sample`: `int? PreviousProductItemId` (+ nav `Item? PreviousProductItem`, FK Restrict) and
  `string? CleaningTechniques` (comma list of "UV","HPLC","ICP"; After Cleaning only).
  Migration `AfterCleaningPreviousProductAndTechniques`, no backfill (old rows keep `PreviousProductName`).
- `ReceiveAfterCleaningRequest` (+ engine record): `int? PreviousProductItemId`, `List<string>? Techniques`.
  - When `PreviousProductItemId` is given: the item must exist, be active, Category FinishedProduct;
    set `PreviousProductName` = item name (keeps every existing display working).
  - Physicochemical lab: `PreviousProductItemId` required; `Techniques` required, ≥1, each in UV/HPLC/ICP,
    stored normalised (distinct, fixed order UV,HPLC,ICP). Micro lab: techniques ignored (null);
    item optional, free-text name still accepted when no item is given.
- Prepare (physchem lab only): a selected part configuration whose technique is not in the sample's
  `CleaningTechniques` → refuse "Part test {code} is {technique}, which was not chosen at receipt."
- Expose `previousProductItemId` and `cleaningTechniques` (string[]) on `SampleDto`.
- Tests: receipt validation (physchem requires item + techniques; bad technique; non-FP item), name copied
  from item, prepare filter by technique, micro unchanged.

## Frontend (job B)
- `NewSampleDialog` AC row: previous product becomes an Autocomplete over Finished Product items
  (find the existing item picker used elsewhere with git grep; else `masterDataOptions` items filtered by
  category). Micro keeps the free-text fallback (Autocomplete with freeSolo); physchem requires a pick.
  Physchem (labMode fixed to the FP section) also shows technique checkboxes UV / HPLC / ICP, ≥1 required.
  Send `previousProductItemId`, `techniques`.
- `AfterCleaningPreparationForm`: in the physchem lab, offer only part configs whose test technique
  (from `useTestDefinitions` workflowType, same mapping) is in `sample.cleaningTechniques`.
- After-cleaning config (`afterCleaning/components/AcPanels.tsx` / `AcPartCard.tsx`): for a physchem test on a
  part, the same chemistry limit editor Phase 3 built for water (`ChemistryLimitFields`), saving the new
  machine-part-configuration fields; pills show the limit. For a UV test default to Qualitative, expected text
  "No peaks".
- `LocationResultsPanel`: for Qualitative rows whose expected text is "No peaks", offer two buttons /
  a select "No peaks" / "Peaks detected" instead of free text (other qualitative rows keep free text).
- Tests for each.

## Verification (stop point)
Postgres suite, tsc, vitest, build; backup + migrate LIMSV2.
UV scan range / scan file: the analyst attaches the scan to the sample's Documents (existing feature);
no new fields. Add if the lab needs them on the result line.
