# RM receiving (Plan 1): deferred minor findings

Collected from task and final reviews on feat/rm-stm-support (12b204c6..2d7dcaba). None block Plan 1.

## Grade
- SpecificationSheets.cs: stage-sheet grade key not whitespace-collapsed; a 21-char grade may throw instead of giving a row error.
- SpecificationParameterDialog.tsx sends grade:null for non-RM items, wiping an imported grade on edit.
- ReceiptLabService.GetForItemAsync lab picker is not grade-aware (refused cleanly later).
- EditSampleDetailsDialog hides the grade select silently when the grades lookup fails; options are not cleared before the new item's grades load.
- No data migration for legacy mixed-case grade rows (none exist); SpecificationService grade FirstOrDefault has no OrderBy.
- Ungraded-guard test passes without the guard; add an ungraded item with zero applicable tests.
- No frontend test for single-grade preselect.
- Migration Down fails if grade-only-distinct spec rows exist.

## Periodic retest selection
- Grade correction on an awaiting sample, or a pending lab with zero grade-eligible tests, leaves the sample awaiting forever (void + re-receive).
- Sample.TestSelectionSignature is one field; the second lab's signature overwrites the link (signature and audit rows both exist).
- No tests for two-heads race (409), Analyst GET, voided awaiting sample, dashboard/KPI counting (shows Unassigned).
- Receive toast says "Test orders generated automatically" for a periodic retest that creates none yet (seen in the 2026-10-26 browser check).

## Minimum quantity
- [NotMapped] Warnings on Domain Sample; ItemSaveRequest null defaults wipe minimums when a client omits them.
- ItemWorkspace shows minimums read-only (edit in AddItemDialog).

## Supplier CoA
- CoA reads are not audited.

## Process
- Several tasks had no honest RED run. vitest has 14 pre-existing failures in modules this branch does not touch (equipment, kpi, biologicalWaste, monitoringPlan, controlChart); check against origin/main before merge.
