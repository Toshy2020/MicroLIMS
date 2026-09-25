# Finished Product / HPLC — Phase 3 Build Spec (manual entry)

Source: `E:\files\files\microlims-finished-product-instrument-integration-FINAL.md` §5, §10, §12 phase 3.
Branch: `feat/fp-hplc-foundation` (local only — never push). Builds on the section segregation
work (`DocumentSection`, `UserSectionScopeService`, `TestDefinition.SectionId`, `Material.SectionId`).

Out of scope this round: instrument listener/adapters, CDS file import, duplicate-hash table, KPIs,
label-claim/mg conversion (REQ-FP-021), bracketing (REQ-FP-004), Final Release, Test Master picker fix,
EquipmentInventory.

## Rules that apply to every slice

- Clean Architecture: entities in Domain, logic in Application services, EF config + migration in
  Persistence, thin controllers in API.
- Section scoping is **required, fail-closed**: every new list endpoint filters by
  `UserSectionScopeService`; every by-id read/write calls the scope guard and throws the existing
  `UnauthorizedAccessException` (403) for another section's record. Never make the scope service optional.
- Every create/update of a GMP record writes an audit entry (existing `IAuditService` pattern).
  Suitability Run creation and HPLC result entry require an electronic signature
  (`IElectronicSignatureService.SignAsync`, password re-entry). Add enum values only at the END of
  int-persisted enums.
- Business rules live in the backend; the frontend never computes Pass/Fail or % Assay.
- Tests: xUnit. Run the FULL suite **with Postgres** (`MICROLIMS_TEST_POSTGRES` set, see existing
  Postgres integration tests) and with `--artifacts-path <temp dir>` because the API is running and
  locks bin. Report the exact pass/fail/skip counts. Postgres-backed tests must not be skipped.

## 1. Masters (slice A1)

### Equipment (extend in place, REQ-FP-010/013)
- `EquipmentType`: append `Hplc`, `PhMeter`, `Balance`.
- New fields on `Equipment`: `Vendor` (string?, max 100), `CdsSoftware` (new enum `CdsSoftware`
  { ShimadzuLabSolutions, AgilentOpenLab, WatersEmpower3 }, nullable — only allowed when Type = Hplc,
  required when Type = Hplc), `ConnectionSettings` (string?, jsonb, not used yet), `SectionId`
  (int, FK → DocumentSections, required).
- Migration: existing Equipment rows get SectionId of the Microbiology section (Code `MICRO`); look it
  up by code in SQL, do not hardcode 1.
- Equipment create auto-tags to the creator's single section via `ResolveSectionForCreateAsync`
  (picker only when the user holds >1 section). Lists/pickers scoped by section.

### Column Master (new, REQ-FP-011)
- Entity `ChromatographyColumn`: Id, Code (unique, required), Name (column name/type), SerialNumber,
  SectionId (required), IsActive, created/modified by+at, optional many-to-many to compatible
  `Equipment` (only Type = Hplc).
- CRUD endpoints under the existing master-data area, permission `EquipmentManage` for writes,
  scoped by section, audited. Deactivate instead of delete.

### Material Stock (REQ-FP-012)
- `MaterialType`: append `ReferenceStandard`.
- `Material.Purity` (decimal?, percent 0 < p ≤ 100, precision 6,3) — required when MaterialType =
  ReferenceStandard, must be null otherwise. Shown/edited wherever materials are created/edited.
- Endpoint for the Suitability Run picker: usable (in stock, not expired) reference standards in the
  caller's sections.

## 2. Spec ranges (slice B — independent)

Today `TestWorkflowEngine.Compare` and `SpecificationService.CompareAgainstLimits` do
`decimal.TryParse(limit)` and treat it as an upper bound; a range is silently ignored.
- One shared parser in Application (e.g. `SpecLimitParser`) returning (Min?, Max?, Unit text) for:
  plain number (existing meaning: upper bound — unchanged), `NMT x`, `NLT x`, `x-y`, `x - y`,
  `x–y` (en dash), optional trailing unit/`%`, decimals.
- Comparison: out of spec when value < Min or value > Max. Alert/Action limits use the same parser.
- Unparseable non-empty limit: keep today's behaviour (ignored) but make it testable; do not throw.
- Both call sites use the parser. Existing count-test behaviour must not change (existing tests stay green).
- `SampleSummaryService.FormatSpecificationText` prints ranges as `90.0 – 110.0 %`, NLT/NMT as today.

## 3. Test Master + Suitability Run (slice A2)

### Test Master fields (REQ-FP-030/042)
- `WorkflowType`: append `HplcAssay`.
- `EquationType` enum { None, HplcAssay, SystemSuitability } on TestDefinition (default None).
  "Equation Types" = hardcoded read-only list endpoint (code, name, formula text, required inputs).
- `RequiresSystemSuitability` (bool), `MethodAbbreviation` (string?, e.g. `VIT-C`, required when
  RequiresSystemSuitability, uppercase letters/digits/hyphen, max 20).
- Acceptance criteria on TestDefinition, all nullable (null = not checked):
  `SstMaxRsdPercent`, `SstMinResolution`, `SstMaxTailingFactor`, `SstMinTheoreticalPlates`.
  At least one required when RequiresSystemSuitability.
- Seed nothing in migrations; the prototype test "Vitamin C Assay" is created through the UI/API
  (Code `VITC_ASSAY`, Equation HplcAssay, RequiresSuitability yes, Section Finished Product).

### System Suitability Run (REQ-FP-001/001a/002/040/041)
- Entity `SystemSuitabilityRun`: Id, Code (unique), TestDefinitionId (the method), SectionId (from the
  test), EquipmentId (Type = Hplc, same section), ChromatographyColumnId (active, same section),
  ReferenceStandardMaterialId (MaterialType ReferenceStandard, usable, same section),
  StandardPurityPercent (snapshot copied from Material.Purity at creation), StandardWeightMg,
  StandardDilution (total dilution / volume factor as used by the formula), StandardMeanArea
  (mean standard peak area read off the CDS report — needed by the assay formula),
  entered RsdPercent, Resolution, TailingFactor, TheoreticalPlates (REQ-FP-001a: typed from the
  CDS report, not recomputed), Passed (bool, computed server-side from the test's criteria),
  FailureReasons (text), PerformedByUserId, PerformedAt, SignatureId, Comment.
- Pass rule: every configured criterion satisfied (RSD ≤ max, Resolution ≥ min, Tailing ≤ max,
  Plates ≥ min). Unconfigured criteria are skipped.
- Create = one signed action; the record is immutable afterwards (a mistake = a new run; the old one
  stays visible). Failed runs are stored and listed.
- Code: `{MethodAbbreviation} S.S {seq:00}/{MM}{yyyy}` e.g. `VIT-C S.S 05/112026`. seq = continuous
  per (MethodAbbreviation, calendar year of PerformedAt), resets each January; MM/yyyy = run date.
  Unique index on Code; max+1 with retry on unique violation (same approach as media lot numbers).
- Endpoints: create, list (filters: test, status passed/failed, date; scoped), get by id (scoped),
  list passed runs selectable for a given TestOrder.

### Linking samples (REQ-FP-003)
- `TestOrder.SystemSuitabilityRunId` (int?, FK). Link/relink endpoint per TestOrder: run must be
  Passed, same TestDefinition as the order's TestCode, same section; only while no HPLC result exists.
  Audited. Bulk link: many TestOrders to one run in one call (all-or-nothing).

## 4. HPLC assay result (slice C, after A2 + B)

- Payload (new, alongside CountTestPayload/ObservationPayload): SampleWeightMg, SampleDilution,
  SampleAreas (list, ≥ 1 replicate), password (signed).
- Gate (REQ-FP-033): reject entry unless the order is linked to a Passed run; approval of a section is
  rejected if any HplcAssay order in it lacks a Passed run (server-side).
- Per replicate: `% Assay = (SampleArea ÷ StandardMeanArea) × (StandardWeightMg ÷ SampleWeightMg)
  × (StandardPurityPercent ÷ 100) × (SampleDilution ÷ StandardDilution) × 100`; mean of replicates,
  rounded for display to 1 decimal, full precision stored.
- New entity `HplcAssayResult` (TestOrderId, SystemSuitabilityRunId, input snapshot incl. the run's
  standard values, replicate areas + per-replicate %, MeanAssayPercent, compared status, entered
  by/at, SignatureId). Plus the usual `Result` row and a `ResultRecord` projection
  (ResultKind.Quantitative, NumericValue = mean, Unit `%`), compared with the spec-range parser.
- Wire into TestWorkflowEngine (IsStepDone, RecordResultAsync, projection), ReviewService
  return-to-analyst (allow HplcAssay; returning clears the result, keeps history), reporting numeric
  trending (ReportingQueryService ~L243/440). Then the existing auto-submit-for-review flow applies.
- OOS: out-of-range mean flows into the existing OOS handling unchanged.

## 5. Frontend (slices F1, F2)

- F1 (after A1/A2): Equipment form fields (type, vendor, CDS when HPLC, section when >1),
  Column Master page, Material form Reference Standard + Purity, Test Master fields (workflow type,
  equation type, requires suitability, abbreviation, criteria), read-only Equation Types page.
- F2 (after C): System Suitability page (create signed run, list with Passed/Failed, detail),
  link samples to a run from the testing workspace, HPLC result entry dialog (weight, dilution,
  replicate areas, signature) showing the server-calculated per-replicate % and mean, sample summary
  and CoA showing the assay result and the linked run code.
