# RM HPLC Impurities (Plan 3 of 6) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** HPLC methods can report related substances (named impurities with correction factors, largest unspecified impurity, totals, disregard limit) against a diluted reference solution. Assays can be calculated from analyte / internal-standard response ratios. Raw-material HPLC assays can report on an as-is, dried or anhydrous basis, using the sample's own LOD or KF result.

**Architecture:**
- **Related substances** is a third `HplcResultMode` (HPLC only).
  - The method carries one analyte: the principal peak. It holds the SST criteria and SST standard.
  - The method also carries a new `HplcMethodImpurity` list (name, RRT, correction factor), plus two method-level values: the reference-solution concentration (% of the test solution) and the disregard limit.
  - Per sample replicate, the principal analyte's existing response row holds the **reference-solution principal-peak area**. A new `HplcReplicatePeak` list holds the test-solution impurity peaks (named, or unspecified with an optional RRT).
  - Spec rows link to the principal analyte and carry a `RelatedSubstanceRow` kind (plus the impurity, for named rows).
- **Internal standard** is a nullable method name. When it is set, every SST injection and every sample replicate also records the IS response, and the calculation uses analyte/IS ratios wherever it used responses.
- **Content basis for RM:** HPLC assay spec rows may use % as is / dried / anhydrous. The evaluator divides by `(100 − moisture)/100`, with moisture from Plan 2's `SampleMoistureResolver`.

**Tech Stack:** ASP.NET Core 10, EF Core + PostgreSQL (Npgsql), xUnit, React + TypeScript + MUI.

**Spec:** `docs/superpowers/specs/2026-10-25-raw-material-stm-support-design.md` (goals 5, 6; goal 4 for HPLC; "Related substances" call; "Main method only" decision). STM facts below come from `E:/MicroLIMS/rls-tmp/rm-stms-txt`.

**STM facts used (latest files):**
- **Sildenafil citrate:**
  - impurity D, RRT 0.15, NMT 0.15 %, correction factor 0.7;
  - impurity A, RRT 1.25, NMT 0.15 %;
  - unspecified NMT 0.10 %; total NMT 0.5 %; reporting/disregard 0.05 %;
  - result = (rU/rS) × (CS/CU) × 100 against a diluted reference solution.
- **Pyridoxine HCl:**
  - impurity B NMT 0.15 % (impurity A has no limit); unspecified NMT 0.10 %; total NMT 0.2 %; disregard 0.05 %; reference (a) = 0.1 % of the test solution;
  - USP assay uses the internal standard p-hydroxybenzoic acid: result = (RU/RS) × (CS/CU) × 100 with area ratios; SST RSD of ratios NMT 3.0 %; dried basis.
- **Sodium methyl / propyl paraben:**
  - reference (c) = 0.5 % of the test solution; impurity A correction factor 1.4;
  - methyl: A NMT 3.0 %; propyl: A NMT 4.0 % (impurity C has no own limit);
  - unspecified each NMT 0.5 %; "sum of others than A" NMT 1.0 %; disregard 0.1 %.

## Global Constraints

- Clean Architecture: calculations and rules live in `MicroLIMS.Application`; the frontend only displays and collects.
- Results are written only through `HplcMethodAssayRecorder` → `PersistTestAnalysisAndFinalizeAsync` (signed, audited).
- **Existing methods are unchanged.** Assay and GC residual-solvent methods, and run snapshots taken before this plan (no impurities, no IS, no new fields), keep working exactly as today. New snapshot fields are nullable or empty on deserialize.
- `ResultMode` and the internal-standard setting are fixed at method creation. The update path refuses changing either, like `ResultMode` today (`HplcMethodService.cs:163-164`).
- **Related-substance values:**
  - Rounded to 2 decimals (MidpointRounding.AwayFromZero). The rounded value is stored and judged.
  - Display: `"0.08 %"`, `"Not detected"` (no peak, or every area 0), or `"< {disregard} %"` (above 0 but below the disregard limit; value 0, excluded from totals).
- Main method only: no area-normalisation option.
- **Builds and tests:**
  - `dotnet build backend/MicroLIMS.sln --artifacts-path E:/MicroLIMS/rls-tmp/rm3-build`
  - `dotnet test backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/rm3-test-<task>` with `MICROLIMS_TEST_POSTGRES` set
  - migrations: `dotnet ef migrations add <Name> --configuration Release`
  - frontend: `npm run build`, `npx eslint <changed files>`, `npx vitest run --testTimeout=30000 <files>`.
- Commit each tested task locally on `feat/rm-stm-support`; never push.

## Review Focus

1. **A related-substances sample with no impurity peaks at all** (every named area 0, no unspecified peaks) reports "Not detected" on every row and passes. It must not crash on an empty max or sum. Test in Task 3.
2. **A reference-solution area of 0 or missing, or an IS response of 0:** the entry problem says "{name}: the reference solution area is required." / "Replicate {n}: the internal standard response is required." It never divides by zero, and preview and submit are both refused. Tests in Tasks 3 and 4.
3. **A run started before this plan** (snapshot JSON without `impurities` / `internalStandardName` / `referenceSolutionPercent`) opens, previews and submits exactly as before. Test in Task 3, using a stored pre-plan snapshot string.
4. **A related-substances spec set that is missing a kind** (e.g. no Total row) still evaluates the rows it has. A named impurity without its own spec row counts as an unspecified peak for "each" and "others". Test in Task 3.
5. **An HPLC dried-basis assay before the sample's LOD result exists** is refused with Plan 2's message "Record the sample's loss on drying result first.". Returning the LOD returns the HPLC assay too. Test in Task 5.

---

### Task 1: Related-substances method master data

**Files:**
- Modify: `backend/MicroLIMS.Domain/Enums/HplcResultMode.cs`, `backend/MicroLIMS.Domain/Entities/HplcMethod.cs`, `backend/MicroLIMS.Persistence/Configurations/HplcMethodConfiguration.cs`, `backend/MicroLIMS.Application/Services/HplcMethodService.cs` (request :16-37, ValidateCommonAsync :420-496, ValidateHplcSectionAsync :498+, ApplyFields :308, ApplyAnalytes ~:380-409, update snapshot audit :147-200), `backend/MicroLIMS.Application/DTOs/Responses/HplcMethodResponse.cs`
- Create: migration `AddHplcRelatedSubstancesMethod`. Tasks 2, 3 and 4 each add their own migration (`AddRelatedSubstanceSpecRows`, `AddHplcReplicatePeaks`, `AddHplcInternalStandard`).
- Test: `backend/MicroLIMS.Tests/UnitTests/HplcMethodServiceRelatedSubstancesTests.cs`

**Interfaces:**
- Produces:
  - `HplcResultMode.RelatedSubstances = 2`.
  - `HplcMethod`: `decimal? ReferenceSolutionPercent`, `decimal? DisregardLimitPercent`, `List<HplcMethodImpurity> Impurities`.
  - `HplcMethodImpurity { int Id; int HplcMethodId; int DisplayOrder; string Name; decimal? RelativeRetention; decimal CorrectionFactor = 1m; }`. Rows are kept on update when unchanged (matched by Id), like analytes, so spec rows stay linked.
  - Request: `SaveHplcMethodRequest.ReferenceSolutionPercent`, `.DisregardLimitPercent`, `.Impurities : List<HplcImpurityInput>`, where `HplcImpurityInput(int? Id, string Name, decimal? RelativeRetention, decimal? CorrectionFactor)`.
  - Response: `HplcMethodResponse.ReferenceSolutionPercent`, `.DisregardLimitPercent`, `.Impurities : List<HplcMethodImpurityResponse(int Id, int DisplayOrder, string Name, decimal? RelativeRetention, decimal CorrectionFactor)>`. Empty list, not null, when absent.

- [ ] **Step 1: Write the failing tests** in `HplcMethodServiceRelatedSubstancesTests` (reuse the HPLC method test builder used by `HplcMethodServiceGcTests`):
  - `Create_RelatedSubstances_SavesImpuritiesAndMethodValues`: HPLC technique, one analyte (wavelength 230, no theoretical weights), `ReferenceSolutionPercent` 0.1, `DisregardLimitPercent` 0.05. Impurities: "Impurity D" (RRT 0.15, CF 0.7) and "Impurity A" (RRT 1.25, CF null). Assert 2 impurities with CF 0.7 and 1.0, and the response maps them.
  - `Create_RelatedSubstances_Refusals` (theory), each refused with its exact message:
    - two analytes → "A related-substances method has exactly one analyte: the principal peak."
    - theoretical weights given → "Theoretical weights are not used in a related-substances method."
    - `ReferenceSolutionPercent` null or ≤ 0 or > 100 → "Enter the reference solution concentration as % of the test solution (above 0, up to 100)."
    - `DisregardLimitPercent` < 0 → "The disregard limit cannot be negative."
    - impurity name blank or duplicate (case-insensitive) → "Each impurity needs a unique name."
    - CF ≤ 0 → "The correction factor must be greater than zero."
    - GC technique → "Related substances is available for HPLC methods only."
  - `Create_AssayOrResidual_RefusesImpuritiesAndRsValues`: an Assay method with impurities or `ReferenceSolutionPercent` → "Impurities and reference-solution values belong to related-substances methods only."
  - `Update_KeepsImpurityIdsWhenUnchanged_AndRefusesModeChange`.
- [ ] **Step 2: Run them to verify they fail.**
  Run: `dotnet test backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/rm3-test-1 --filter FullyQualifiedName~HplcMethodServiceRelatedSubstancesTests`. Expected: FAIL (compile errors).
- [ ] **Step 3: Implement** the entity, config, enum, validation, Apply/ApplyImpurities, response mapping and migration.
  - The update audit snapshot includes the impurity list and the two values.
  - Impurity table: `HplcMethodImpurities`, FK cascade from the method, unique `(HplcMethodId, Name)`.
- [ ] **Step 4: Run** the filter, then the full Postgres suite. Expected: PASS, no regressions in `HplcMethodServiceGcTests` / `HplcMethodAssayTestMasterTests`.
- [ ] **Step 5: Commit** `feat(hplc): related-substances method mode with impurities, reference solution and disregard limit`.

### Task 2: Related-substances specification rows

**Files:**
- Create: `backend/MicroLIMS.Domain/Enums/RelatedSubstanceRow.cs`
- Modify: `backend/MicroLIMS.Domain/Entities/Specification.cs` (near :65), `backend/MicroLIMS.Persistence/Configurations/SpecificationConfiguration.cs:37`, `backend/MicroLIMS.Application/Services/SpecificationService.cs` (HplcMethodAssay branch :323-361, duplicate check :236-244), the spec request/response DTOs
- Test: `backend/MicroLIMS.Tests/UnitTests/RelatedSubstanceSpecificationTests.cs`

**Interfaces:**
- Consumes: Task 1 `HplcResultMode.RelatedSubstances`, `HplcMethodImpurity`.
- Produces: `enum RelatedSubstanceRow { Named = 0, UnspecifiedEach = 1, Total = 2, TotalOthers = 3 }`; `Specification.RelatedSubstanceRow? RelatedSubstanceRow`; `Specification.int? HplcMethodImpurityId` (FK to `HplcMethodImpurities`, restrict delete). The same two fields are on the spec create/update DTOs and the response.

- [ ] **Step 1: Write the failing tests:**
  - `RelatedSubstancesSpec_ValidRows_Save`: one row per kind, all `NotMoreThan`, unit "%", `ResultBasis.PercentAsIs`, `HplcMethodAnalyteId` = the principal analyte. The Named row carries `HplcMethodImpurityId` (Impurity D, limit 0.15); UnspecifiedEach 0.10; Total 0.5; TotalOthers 1.0.
  - `RelatedSubstancesSpec_Refusals` (theory), each with its exact message:
    - row kind missing → "Choose the related-substance row: named impurity, any unspecified impurity, total, or total of others."
    - Named without impurity → "Choose the impurity for this row."
    - an impurity from another method → "The impurity does not belong to this test's HPLC method."
    - non-Named with an impurity → "Only a named-impurity row names an impurity."
    - limit type other than NotMoreThan → "Related-substance limits are 'not more than' limits."
    - basis other than PercentAsIs → "Related-substance rows report % (as is)."
    - a second row with the same kind (+ same impurity for Named) for the same stage/grade → the existing duplicate message.
  - `AssayModeSpec_RefusesRelatedSubstanceFields`: an Assay-mode test with `RelatedSubstanceRow` set → "Related-substance rows belong to related-substances methods only."
- [ ] **Step 2: Run them to verify they fail** (filter `RelatedSubstanceSpecificationTests`).
- [ ] **Step 3: Implement** a RelatedSubstances branch beside the Residual/Assay branches at :334-352, and extend the duplicate key with `(RelatedSubstanceRow, HplcMethodImpurityId)`.
- [ ] **Step 4: Run** the filter and the full suite. Expected: PASS.
- [ ] **Step 5: Commit** `feat(specs): related-substance specification rows`.

### Task 3: Related-substances calculation, entry and submission

**Files:**
- Create: `backend/MicroLIMS.Application/Helpers/RelatedSubstancesEvaluator.cs`
- Modify: `backend/MicroLIMS.Domain/Entities/HplcRun.cs` (add `HplcReplicatePeak`, `HplcSampleReplicate.Peaks`), its persistence config and the migration, `backend/MicroLIMS.Application/Services/HplcRunService.cs` (SST row creation at run start: in RelatedSubstances mode the analyte row needs a lot but no weight, purity or moisture, as residual does at :412-431), `backend/MicroLIMS.Application/Services/HplcRunService.Samples.cs` (DTOs :17-38, SaveReplicatesAsync :180-221, BuildEntryDtoAsync :304-341), `backend/MicroLIMS.Application/Services/HplcSampleEntryContext.cs` (EntryProblem :55-96, EvaluateRows :98-100), `backend/MicroLIMS.Application/Workflows/TestWorkflow/HplcMethodAssayRecorder.cs` (:34-134)
- Test: `backend/MicroLIMS.Tests/UnitTests/RelatedSubstancesEvaluatorTests.cs`, `backend/MicroLIMS.Tests/UnitTests/HplcRunServiceRelatedSubstancesTests.cs`

**Interfaces:**
- Consumes: Tasks 1–2.
- Produces:
  - `HplcReplicatePeak { int Id; int HplcSampleReplicateId; int? HplcMethodImpurityId; decimal? RelativeRetention; decimal Area; }`. A null impurity means an unspecified peak.
  - `HplcReplicatePeakInput(int? HplcMethodImpurityId, decimal? RelativeRetention, decimal Area)`.
  - `HplcReplicateInput` gains `List<HplcReplicatePeakInput>? Peaks`, and `HplcReplicateDto` gains `List<HplcReplicatePeakInput> Peaks`.
  - In RelatedSubstances mode, the principal analyte's `HplcReplicateResponseInput.Response` is the reference-solution principal-peak area. `ActualWeightMg` may be 0 and is not checked.
  - Evaluator: `public record RelatedSubstancesInput(decimal ReferenceSolutionPercent, decimal DisregardLimitPercent, IReadOnlyList<HplcMethodImpurityResponse> Impurities, IReadOnlyList<RsReplicate> Replicates, IReadOnlyList<Specification> Specs)`, with `public record RsReplicate(int ReplicateNo, decimal ReferenceArea, IReadOnlyList<HplcReplicatePeakInput> Peaks)`. `RelatedSubstancesEvaluator.Evaluate(int principalAnalyteId, string principalName, RelatedSubstancesInput input) : List<HplcAssayRow>` returns one row per spec row: Named rows in impurity order, then UnspecifiedEach, TotalOthers, Total. Each row's `Basis` is `ResultBasis.PercentAsIs` and its `ReplicateNo` is null.
  - Recorder `CalculationJson.quantity`: `"RelatedSubstance"`; the payload also carries `kind`, `impurityId`, the per-replicate areas, percents and `referenceArea`.

Calculation (per replicate, then the mean over replicates; round only the final value):
```
pct(area, cf) = area × cf / referenceArea × ReferenceSolutionPercent
named i   = pct(area of peaks with impurity i, CF_i)      (sum if the analyst entered two)
counted   = a peak whose pct ≥ DisregardLimitPercent (strictly: pct < disregard is ignored)
unspecified pool = unspecified peaks + peaks of named impurities that have no Named spec row (CF applied)
UnspecifiedEach = max(pct over counted pool peaks), 0 if none
Total       = Σ counted named + Σ counted pool
TotalOthers = Σ counted pool
```

- [ ] **Step 1: Write the failing evaluator tests**, all with `ReferenceSolutionPercent` 0.1, disregard 0.05, reference area 10000:
  - `Sildenafil_Example`:
    - inputs: D area 12000 with CF 0.7 → 0.084 → "0.08 %"; A area 9000 → "0.09 %"; unspecified peaks 6000 (0.06) and 3000 (0.03, ignored);
    - expected: UnspecifiedEach "0.06 %"; Total 0.084 + 0.09 + 0.06 = 0.234 → "0.23 %";
    - all WithinLimits against limits 0.15 / 0.15 / 0.10 / 0.5.
  - `Paraben_Example`:
    - inputs: `ReferenceSolutionPercent` 0.5, disregard 0.1, reference area 20000; A area 50000 with CF 1.4 → 1.75 → "1.75 %" (limit 3.0, Within); impurity C (no own row) area 4000 → 0.10 (counted, at the limit equality); unspecified 2000 → 0.05 (ignored);
    - expected: UnspecifiedEach "0.10 %"; TotalOthers "0.10 %".
  - `NoPeaks_AllNotDetected` (Review Focus 1).
  - `BelowDisregard_ShowsLessThan`: D area 500 → "< 0.05 %", value 0.
  - `TwoReplicates_MeanOfReplicatePercents`.
  - `OOS_WhenNamedAboveLimit`: A area 20000 → 0.20 > 0.15 → OutOfSpecification.
- [ ] **Step 2: Write the failing service tests** in `HplcRunServiceRelatedSubstancesTests`:
  - `SaveReplicates_StoresPeaks_AndPreviewMatchesEvaluator`.
  - `EntryProblem_ReferenceAreaZero_Refused`: message "{principal name}: the reference solution area is required." (Review Focus 2).
  - `SaveReplicates_RefusesNegativeArea_AndForeignImpurity`: messages "Peak areas cannot be negative." / "The impurity does not belong to this method."
  - `Submit_WritesOneParameterResultPerSpecRow_WithDisplays`.
  - `PrePlanSnapshot_AssayRun_StillPreviewsAndSubmits` (Review Focus 3). It uses a snapshot JSON string captured from the current `HplcMethodResponse` serializer before Task 1's fields.
- [ ] **Step 3: Run them to verify they fail.**
- [ ] **Step 4: Implement:**
  - the evaluator;
  - `IsRelatedSubstances` in the context, with EntryProblem rules: SST passed, replicate count, reference area > 0, at least one RS spec row on the item, otherwise "No related-substances specification on this item.";
  - EvaluateRows branch;
  - SaveReplicates peak validation and storage, replacing peaks with the replicates;
  - entry DTO peaks;
  - the recorder branch, writing readings per spec row: one ResultReading per contributing peak, with text `"{name or 'Unspecified RRT x'}: {pct}"`.
- [ ] **Step 5: Run** both filters and the full suite. Expected: PASS.
- [ ] **Step 6: Commit** `feat(hplc): related-substances calculation, entry and submission`.

### Task 4: Internal-standard assay

**Files:**
- Modify: `HplcMethod.cs` (`string? InternalStandardName`), `HplcMethodService.cs` (request/validation/apply/snapshot), `HplcMethodResponse.cs`, `HplcRun.cs` (`HplcSstInjection.InternalStandardResponse decimal?`, `HplcSampleReplicate.InternalStandardResponse decimal?`), `HplcRunService.cs` (`SaveSstAnalyteInput` :34-37, SaveSstAsync :389-458, ConfirmSst :515-534), `HplcRunService.Samples.cs`, `HplcSampleEntryContext.cs` (BuildAnalyteInputs :119-137, EntryProblem), the migration
- Test: `backend/MicroLIMS.Tests/UnitTests/HplcInternalStandardTests.cs`

**Interfaces:**
- Produces:
  - `SaveHplcMethodRequest.InternalStandardName` (trimmed, ≤ 100 chars). Allowed only for an HPLC Assay-mode method, otherwise "An internal standard applies to HPLC assay methods only."; fixed after creation.
  - `HplcMethodResponse.InternalStandardName`.
  - `SaveSstAnalyteInput` gains `List<decimal>? InternalStandardResponses` (same length as `Responses`).
  - `HplcReplicateInput` gains `decimal? InternalStandardResponse`; `HplcReplicateDto` gains the same.
  - When the method has an IS, the SST `MeanResponse`, the RSD and every sample response used by `HplcAssayCalculator` are the **ratio** analyte/IS, computed per injection or replicate. Stored raw responses are unchanged.

- [ ] **Step 1: Write the failing tests:**
  - `Sst_WithInternalStandard_MeanAndRsdUseRatios`: responses [50000, 50500, 49500], IS [100000, 101000, 99000] → MeanResponse 0.5, computed RSD 0.0.
  - `Sample_WithInternalStandard_AssayFromRatio`: std weight = Th.Wt std, purity 100, moisture 0, sample weight = Th.Wt test, sample 49000 / IS 100000 → assay "98.0 %".
  - `Refusals`, with exact messages:
    - IS responses missing, wrong length or ≤ 0 at SST → "{analyte}: enter an internal standard response (above zero) for every injection."
    - replicate IS missing or ≤ 0 → EntryProblem "Replicate {n}: the internal standard response is required." (Review Focus 2)
    - IS values sent to a method without an IS → "This method has no internal standard."
  - `Method_InternalStandard_OnlyHplcAssay_AndFixedOnUpdate`.
- [ ] **Step 2: Run them to verify they fail.**
- [ ] **Step 3: Implement.** Put the ratio in one helper, `HplcResponse.Effective(decimal response, decimal? isResponse)`, used by SST, the context and the recorder readings, so that every caller divides the same way.
- [ ] **Step 4: Run** the filter and the full suite. Expected: PASS.
- [ ] **Step 5: Commit** `feat(hplc): internal-standard assay from response ratios`.

### Task 5: HPLC content assay on as-is / dried / anhydrous basis

**Files:**
- Modify:
  - `SpecificationService.cs`: the HPLC Assay-mode bases at :341-352. Extend the Plan 2 moisture prerequisite (:406-438) and the one-basis rule to HplcMethodAssay rows.
  - `HplcSampleAssayEvaluator.cs` and `HplcAssayCalculator.cs`: basis from the spec, with a moisture divisor.
  - `HplcSampleEntryContext.cs`: content-basis spec lookup, and moisture via `SampleMoistureResolver`. EntryProblem returns the resolver message when moisture is missing.
  - `HplcMethodAssayRecorder.cs`: write `TestAnalysis.ConditionsJson` with `{"moisture":{"parameterResultId":…,"testOrderId":…,"percent":…,"role":…}}`, the shape `SampleReviewService.UsesMoistureResult` (:286-297) reads. Re-resolve under `ResolveLockedAsync` exactly as `TitrationRecorder` does.
  - `SampleReviewService.FindDependentAssaysAsync` (:270-282): include `WorkflowType.HplcMethodAssay`.
  - `TestWorkflowController` moisture GET: it must serve HPLC test orders too. Rename `GetForTitrationOrderAsync` → `GetForAssayOrderAsync` if needed.
- Test: `backend/MicroLIMS.Tests/IntegrationTests/HplcMoisturePostgresTests.cs`, plus spec tests in `MoistureRoleSpecificationTests.cs`

**Interfaces:**
- Consumes: Plan 2's `SampleMoistureResolver.ResolveAsync(int sampleId, MoistureRole role)`, `ResolveLockedAsync`, and the `MoistureRole` mapping from `ResultBasis`.
- Produces:
  - HPLC Assay-mode spec rows accept `PercentAsIs`, `PercentDriedBasis` and `PercentAnhydrousBasis`, besides the existing `PercentLabelClaim` and `MgPerUnit`.
  - Per analyte, an item uses either a label-claim basis or one content basis, never both: "An HPLC analyte reports either % of label claim or a content basis, not both."
  - `HplcAssayAnalyteInput` gains `decimal? MoisturePercent`; the reported value is `mean × 100 / (100 − moisture)` on dried/anhydrous, and the mean itself on as-is.

- [ ] **Step 1: Write the failing tests:**
  - `HplcSpec_DriedBasis_NeedsLodTest`: Plan 2's message "Assign a loss on drying test to this item before using a dried basis."
  - `HplcSpec_LabelClaimAndContentBasis_Refused`: the message above.
  - `HplcDriedAssay_BeforeLod_Refused`: preview and submit both give "Record the sample's loss on drying result first."
  - `HplcDriedAssay_UsesLod`: as-is 98.0 with LOD 0.5 → 98.49, display "98.5 %". ConditionsJson carries the moisture result id.
  - `ReturningLod_ReturnsHplcAssay` (Review Focus 5).
  - `AsIsAssay_NoMoistureNeeded`.
- [ ] **Step 2: Run them to verify they fail.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** the filter and the full suite. Expected: PASS. Finished-product HPLC tests are unchanged.
- [ ] **Step 5: Commit** `feat(hplc): raw-material content assay on as-is, dried or anhydrous basis`.

### Task 6: Screens — method editor and specification dialog

**Files (frontend/src):**
- Method editor, under `modules/laboratoryConfiguration/masterDataSimple/`:
  - `services/HplcMethodService.ts` (types :6, :83, :106, :187);
  - `hplcMethod/hplcMethodForm.ts` (:58, :100, :144-259, :375-381);
  - `hplcMethod/useHplcMethodDialogState.ts` (:163-242);
  - `hplcMethod/HplcGeneralSection.tsx` (:94-111);
  - `hplcMethod/HplcAnalytesSection.tsx` (:32-58);
  - `hplcMethod/hplcMethodValidation.ts` (:70);
  - new `hplcMethod/HplcImpuritiesSection.tsx`.
- Spec dialog: `modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx` (HPLC branches around :257-271, :554-557, :679, :913).
- Test: `hplcMethod/relatedSubstancesMethod.test.ts`, `items/components/SpecificationParameterDialog.rs.test.tsx`

**Interfaces:**
- Consumes the Task 1, 2 and 4 request/response field names exactly (camelCase): `referenceSolutionPercent`, `disregardLimitPercent`, `impurities[{id,name,relativeRetention,correctionFactor}]`, `internalStandardName`, `relatedSubstanceRow` ("Named" | "UnspecifiedEach" | "Total" | "TotalOthers"), `hplcMethodImpurityId`.

**Method editor:**
- The result-mode select offers "Related Substances" for the HPLC technique.
- In that mode:
  - the analyte table shows one principal row, with no theoretical weights;
  - a "Reference solution (% of test)" field and a "Disregard limit (%)" field appear;
  - an impurities table is shown (name, RRT, correction factor, default 1).
- In Assay mode, an optional "Internal standard" text field appears. It is disabled when editing an existing method (it is fixed at creation).

**Spec dialog:**
- For a related-substances test, the dialog shows:
  - a "Row" select with options "Named impurity", "Any unspecified impurity", "Total impurities" and "Total of others";
  - an impurity picker, shown only for "Named impurity";
  - a fixed limit type of NMT, unit %, and basis "% as is".
- For an HPLC Assay test on a raw-material item, the basis select adds "% as is", "% dried basis" and "% anhydrous basis".

- [ ] **Step 1: Write the failing tests:**
  - **Form mapping:** an RS form maps to a request with impurities and both values; an Assay form with `internalStandardName` maps it.
  - **Validation messages** mirror the server's messages in Task 1.
  - **Spec dialog:** an RS test shows the Row select. The impurity picker appears only for Named, and the payload carries `relatedSubstanceRow` and `hplcMethodImpurityId`.
- [ ] **Step 2: Run them to verify they fail.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** `npx vitest run --testTimeout=30000 <the two files and existing gcMethod.test.ts>`, `npm run build`, eslint. Expected: PASS, clean.
- [ ] **Step 5: Commit** `feat(ui): related-substances and internal-standard method setup, RS spec rows`.

### Task 7: Screens — run entry and results

**Files (frontend/src/modules/hplcWorkspace):**
- `types.ts` (:43-60, :287, :325);
- `services/HplcWorkspaceService.ts` (saveSst :125, saveReplicates :167);
- `entry/HplcSampleEntryPage.tsx` (:102-121, :152-159, :254-346);
- `entry/ReplicateEntryTable.tsx` (:31-264);
- `entry/CalculationSummaryCard.tsx` (:109-216);
- `sst/StandardEntryForm.tsx` (:48-233) and `sst/sstValidation.ts`;
- new `entry/RelatedSubstancesPeakTable.tsx`.
- Tests: `entry/RelatedSubstancesPeakTable.test.tsx`, `entry/ReplicateEntryTable.test.tsx` (extend), `sst/sstValidation.test.ts` (extend).

**Interfaces:**
- Consumes the Task 3–5 DTO fields: `peaks[{hplcMethodImpurityId, relativeRetention, area}]`, `internalStandardResponse`, `internalStandardResponses`, the preview rows (quantity `"RelatedSubstance"`), and the moisture GET (`{role, percent, testCode} | null`).

**Related-substances mode:**
- Per replicate:
  - "Reference solution area" (the principal response);
  - one area input per named impurity, labelled with its name and RRT;
  - an "Unspecified peaks" list (add/remove rows: RRT optional, area).
- No sample-weight column.
- The preview card lists the rows with their displays ("Not detected", "< 0.05 %").

**Internal-standard mode:**
- The SST standard-injection grid and the replicate table each get an "IS response" column next to each response.
- Labels read "Analyte area" and "IS area".

**Content basis:** when the assay spec is dried or anhydrous, show the read-only line "Loss on drying 0.5 % (from LOD-xxx)" or "Water 0.4 % (from KF-xxx)", the same component pattern as `TitrationPanel`. If moisture is missing, show the server's message and disable submission.

- [ ] **Step 1: Write the failing tests:**
  - **Peak table:** add/remove unspecified rows; negative area shows "Peak areas cannot be negative."; the payload shape.
  - **Replicate table in IS mode:** shows the IS column and requires it above 0.
  - **SST validation:** in IS mode, IS responses are required, one per injection.
- [ ] **Step 2: Run them to verify they fail.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** vitest on the changed tests and the existing hplcWorkspace tests, `npm run build`, eslint. Expected: PASS.
- [ ] **Step 5: Commit** `feat(ui): related-substances peak entry, internal-standard columns, content-basis moisture line`.

### Task 8: Phase check

- [ ] Opus whole-branch review of the Plan 3 commits against spec goals 5, 6 and 4 (HPLC), and the Review Focus list. One fix wave, then a re-review.
- [ ] Back up LIMSV2 with `pg_dump`, then apply the migrations.
- [ ] Browser check in Brave, using the E2E accounts in `frontend/.env.e2e.local`, on test item 295 (Raw Material):
  1. **RS method "E2E-RS"** (HPLC): reference 0.1 %, disregard 0.05 %, impurities D (CF 0.7) and A. Add its test, and spec rows Named D 0.15 / Named A 0.15 / Unspecified 0.10 / Total 0.5.
  2. **Run:** SST; sample with reference area 10000, D 12000, A 9000, unspecified 6000 and 3000. The preview shows 0.08 / 0.09 / 0.06 / 0.23 %. Submit; the review shows those rows.
  3. **IS method "E2E-IS"** (Assay, IS "p-hydroxybenzoic acid"), with a dried-basis spec on item 295. Before the sample's LOD exists, entry is refused. After the LOD, the result follows Task 5's numbers.
  4. **Return:** returning the LOD returns the HPLC assay.
- [ ] Add read-only Neon pre-merge queries to `E:/MicroLIMS/rls-tmp/neon-precheck-plan3.sql`: count HPLC methods by ResultMode, and existing spec rows that would collide with the new duplicate key.
- [ ] Write `docs/superpowers/plans/2026-10-27-rm-hplc-impurities-followups.md` (deferred minors), update memory, remove the SDD workspace and any worktrees, then stop and report to the user before Plan 4.
