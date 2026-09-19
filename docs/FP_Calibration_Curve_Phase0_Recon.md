# Calibration Curve (ICP-OES) - Phase 0 Recon Findings

Source: `E:\files\files\microlims-finished-product-calibration-curve-scoping.md` (§12 Phase 0).
Read-only recon of local branch `feat/fp-hplc-foundation`, 2026-09-19. No code changed.

## Findings

| # | Area | What exists today | Impact on the design |
|---|---|---|---|
| F1 | Suitability Run lifecycle | `SystemSuitabilityRun` is **signed once at creation and immutable**. Pass/Fail is computed server-side at creation. There is **no Draft / Submitted / Approved / Voided state, no review or approval step, and no attachment**. A failed run is kept; the analyst creates a new one. | §7 says "run approval follows the same review flow as the Suitability Run", but that flow doesn't exist. §8 needs a reviewer to verify the transcription against the attached report. So the Calibration Run needs a lifecycle the Suitability Run doesn't have (decision G1). |
| F2 | Run code | `SystemSuitabilityRunCode`: `{ABBR} S.S {seq:00}/{MM}{yyyy}` (e.g. `VIT-C S.S 05/112026`), sequence per method per calendar year, unique-index retry on collision. Month/year come from UTC. | P1's example `ICP-MIN CAL 01/09/2026` adds a slash between month and year. That doesn't match the SST format (decision G2). The generator can be reused with a different infix. The UTC month-boundary issue applies to both. |
| F3 | Equation types | `EquationType` enum {None, HplcAssay, SystemSuitability}. `GET masterdata/equation-types` returns a hard-coded list. The Equation Types page is read-only. Tests route to their result screen by `WorkflowType` {CountTest, Observation, HplcAssay}. | Add `EquationType.CalibrationCurve`, an `EntryMode` enum (InstrumentReported; LimsFitted reserved), and a new `WorkflowType` (e.g. `ElementalAssay`) for routing. It fits the pattern; no registry table is needed. |
| F4 | Equipment | `Equipment` has SectionId, Vendor and `CdsSoftware` {Shimadzu LabSolutions, Agilent OpenLab, Waters Empower 3}. `EquipmentType` has Hplc, PhMeter and Balance. The FP Instruments page offers those three. | Append `EquipmentType.IcpOes` and `CdsSoftware.PerkinElmerSyngistix`, and add ICP-OES to FP Instruments. The "CDS required" rule currently applies only to HPLC and should cover ICP-OES too. |
| F5 | Material Stock | `Material` has SectionId, `ExpiryDate` (nullable - some rows have none), `Purity`, and a computed `Status` / `IsUsable`. | The expiry check on run date is feasible. Standards with no expiry date need a rule (decision G4). |
| F6 | Specifications | Universal Specifications is **done**: `Specification` rows are parameters, with LimitType and typed limits. Several parameters per test are supported. Readers use the primary parameter. | Add `ResultBasis` (mg/kg, mg/unit, %LC), `LabelClaim` + `LabelClaimUnit`, and `ConversionFactor` (default 1.0) to `Specification`. Each element parameter links to a Test Master analyte (P4). |
| F7 | Test Master analytes | Test Master holds no analyte list. SST criteria are four columns on `TestDefinition`. | Add a new `TestAnalyte` child (element, wavelength, view, LOQ) and `CalibrationAcceptanceCriteria` (Test Master level, optional per-analyte overrides). The FP Test Master dialog gets the analyte block. |
| F8 | Attachments + SHA-256 | Established pattern: `EquipmentDocument` / `MaterialDocument` / `ItemDocument` store a server-generated `StorageKey` in `IFileStorageService` (local disk / B2) plus hex `ContentSha256`. `ArchivedRecord` is append-only with a hash. | Add a `CalibrationRunDocument` following the same pattern. Submit is refused without it (TC10). |
| F9 | Sample preparation | You decided that **FP tests have no preparation stage** (commit 7d3bef2). `ItemPreparationConfiguration` is Microbiology-only. HPLC captures sample weight and dilution in the result entry itself. | The digest record (W, V, Wu) is entered once per digest **in the result entry**, like HPLC, not in the preparation stage. |
| F10 | Result storage | `HplcAssayResult` holds **one result per test order**, evaluated against the primary parameter. The approval gate, return-to-analyst, result projection, summary and CoA all assume one result per test. | Elemental assay needs **one result per specification parameter (element)**. This is the largest part of the build. The new result entity is keyed (TestOrder, Specification). The approval gate, review return, projection, summary and CoA must list each element (the summary text already lists several parameters). |
| F11 | RequiresReview | The status string `"RequiresReview"` exists (TNTC/uncountable counts, `CountTestReading.RequiresReview`). `SpecificationEvaluator` returns WithinLimits / OutOfSpecification / LimitsNotConfigured. | OverRange and `<LOQ` map to `RequiresReview` via an evaluator extension (TC6/TC7). |
| F12 | Rounding | HPLC compares the **unrounded** mean with the limit and reports it rounded to 1 dp. | Q4 is still open; it must be settled for both assays. |
| F13 | Review of results | `ReviewService` returns a test to the analyst by deactivating the active HPLC result. The section review/approval flow is sample-level. | Reuse it for element results. No second review flow is needed for results. |

## Decisions needed at Gate 0

- **G1 Run lifecycle.** Option A: sign-once immutable, like the SST run (the transcription check is then only the analyst's own). Option B **(recommended)**: Draft → Submitted (report attached, signed) → Reviewed / Approved by a second person who records the transcription verification (signed) → Voided. Only Approved runs with a passing analyte are selectable. This satisfies §7 and §8. The SST run could adopt it later.
- **G2 Code format.** Either `ICP-MIN CAL 01/092026` (same shape as SST) or `ICP-MIN CAL 01/09/2026` (as P1 is written).
- **G3 Syngistix answers Q1–Q6.** A redacted report settles Q1, Q2, Q3 and Q5. Q4 (rounding) and Q6 (acceptance values) come from the validated method.
- **G4 Standards without an expiry date.** Recommended: they cannot be used on a calibration run.
- **G5 Sequencing.** Recommended: build in the phase order of §12. Phase 1 = run (analytes, checks, gate, attachment, code) + Test Master analytes + spec additions. Phase 1b = element results and their downstream (F10).

## Nothing blocks the build apart from G1–G5

The scoping doc's assumptions hold for: Equipment tagging, Material expiry, universal specifications, hashing/storage, RequiresReview, and the multi-department section model.
