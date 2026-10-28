# Task Progress Tracker

| Task ID | Description | Status | Evidence / Notes |
|---|---|---|---|
| TASK-01 | Fix 1: Disregard on final value, display "< {disregard} %" / "< 0.01 %", Total / TotalOthers logic | Done | Unit tests in `RelatedSubstancesEvaluatorTests.cs` pass (`FinalMeanBelowDisregard_DisplaysLessThanDisregard_WithValueZero`, `DisregardLimitZero_RoundsToZeroWhileRawAboveZero_DisplaysLessThan001_WithValueZero`, etc.). |
| TASK-02 | Fix 2: Full peak evidence in signed record CalculationJson with `disregarded: true/false` | Done | Test `Submit_WritesOneParameterResultPerSpecRow_WithDisplays` verifies all entered peaks in `CalculationJson` with `disregarded: true/false` while `ResultReadings` retain contributing peaks only. |
| TASK-03 | Fix 3: Atomic replicate replace with `UnitOfWork.RunAsync` in HplcRunService.Samples.cs | Done | Integration test `ReplaceReplicates_FailingAtInsert_RollsBackTheDeletion` in `UnitOfWorkPostgresIntegrationTests.cs` verifies rollback on failure under PostgreSQL. |
| TASK-04 | Fix 4: Add code comment explaining UnspecifiedEach per-replicate max ruling | Done | Comment added to `RelatedSubstancesEvaluator.cs` at `case RelatedSubstanceRow.UnspecifiedEach:`. |
| TASK-05 | Fix 5: Verification - Local test filter & full test suite with MICROLIMS_TEST_POSTGRES | Done | Local test filter: 61 passed, 0 failed, 1 skipped. Full suite with MICROLIMS_TEST_POSTGRES: 3,800 passed, 0 failed, 0 skipped. |
