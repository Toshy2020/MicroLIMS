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

## Browser check (2026-11-02): passed on local LIMSV2
- Found and fixed:
  - HPLC entry refused every raw and packaging material sample because they have no production stage (319ad9d5).
  - The reviewer's related-substances panel used the assay layout, and internal-standard results showed "moisture: [object Object]".
- Open decision: with no stage, the test's `ReplicateCount` sets the replicates; with none set, the analyst enters at least one. The Test Master screen doesn't offer that field for HPLC tests, so a fixed count can only be set through the API today.
- Noticed, not fixed: an assay stores 98.49 and displays 98.5 (one decimal); the sample was judged within limits either way.
- Not exercised in the browser: method and SST screens for the IS method (created by API), and approval after review.
- Local test data left in LIMSV2: items E2E-RM-HPLC (296) and E2E-RM-GRADE (295, gained tests E2E-RS-T and E2E-IS-T plus their spec rows), methods E2E-RS (15) and E2E-IS (16), column COL-E2E-L1, preparations 12 and 13, samples RM1126001 and RM1126002, runs 17-20 (all completed).
