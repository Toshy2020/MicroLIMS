# RM HPLC impurities (Plan 3): follow-ups

## Deploy
- Before merge, run `E:/MicroLIMS/rls-tmp/neon-precheck-plan3.sql` read-only on Neon. Prod migrates at startup.
- HPLC assay specs on a dried or anhydrous basis need the sample's own LOD/KF result after deploy. Their moisture test needs a MoistureRole (same step as Plan 2).

## Moved to Plan 6
- Workbook `SpecificationSheets` import of the related-substances fields.

## Deferred minors
- Method screen and service
  - In-memory tests don't exercise the impurity unique index. A rename swap A<->B would fail on Postgres.
  - A name over 100 characters reuses the "unique name" message.
  - RRT <= 0, negative CF, the upper bound of 100, 2+ analytes and the update-path analyte count are untested. Unmatched Id behaviour isn't pinned.
  - The client refuses an empty CF while the payload maps empty to 1 (pre-existing). `Date.now()` in the client key is unnecessary.
- Spec rows
  - No DB-level check that a row's impurity belongs to the test's method. The duplicate key ignores the analyte (single-analyte RS by design).
- Calculation
  - No RRT-to-impurity matching for unspecified peaks.
  - UnspecifiedEach is per-replicate max then mean, because peaks aren't tracked across replicates. It can differ from a per-peak mean on multi-injection RS.
  - Not-detected rows have zero readings.
- Entry
  - The peak table falls back to index matching when clientKey is absent (loaded rows).
  - Content basis conflict DTO has no MoistureRole marker.
  - The SST panel still takes injection counts and SST limits from the live method. Mode and internal standard come from the run snapshot.
- Final review minors
  - Dissolution vs RS interaction, a dead preview check, an unused type, working-standard peaks unchecked, and the conflict check ignoring grade and stage.
