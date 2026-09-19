# Calibration Curve (ICP-OES) - Build Spec

Scoping: `E:\files\files\microlims-finished-product-calibration-curve-scoping.md`. Recon + gate:
`docs/FP_Calibration_Curve_Phase0_Recon.md`. Local branch `feat/fp-hplc-foundation`, never pushed.

## Gate 0 decisions (user, 2026-09-19)

- G1 **Sign once, like the System Suitability Run**: created in one signed action with the Syngistix
  report attached, immutable afterwards, gate computed at creation. No draft/approve. A failed run is kept;
  the analyst creates a new run. (TC11 "edit after submission" therefore = no edit endpoint at all.)
- G2 Code `{MethodAbbreviation} CAL {seq:00}/{MM}{yyyy}` (e.g. `ICP-MIN CAL 01/092026`), sequence per
  method per calendar year - reuse/generalise `Helpers/SystemSuitabilityRunCode.cs` (infix parameter).
- G4 A calibration / ICV standard with no ExpiryDate cannot be used (analyte gate fails).
- G3 Syngistix specifics are configuration on the Test Master (r vs r², which checks, acceptance values);
  spec comparison uses the unrounded value (same as HPLC).

## Enums (append-only; Domain/Enums)

- `EquationType` + `CalibrationCurve`. `WorkflowType` + `ElementalAssay`. `EquipmentType` + `IcpOes`.
  `CdsSoftware` + `PerkinElmerSyngistix`.
- New: `CalibrationEntryMode` {InstrumentReported, LimsFitted (reserved, not built)},
  `CorrelationType` {R, RSquared}, `CalibrationCheckType` {Blank, Icv, Ccv, InternalStandard},
  `AnalyteView` {Axial, Radial}, `ResultBasis` {MgPerKg, MgPerUnit, PercentLabelClaim}.

## Slice S1 - Test Master analytes + criteria, ICP-OES equipment, Calibration Run (backend)

TestDefinition additions (nullable): `CalibrationEntryMode`, `CalMinCorrelation` decimal(10,6),
`CalCorrelationType`, `CalMinStandards` int, `CalCheckRecoveryLowPercent` / `CalCheckRecoveryHighPercent`
(ICV+CCV window), `CalBlankMax` (mg/L; null = analyte LOQ), `CalIsRecoveryLowPercent` / `CalIsRecoveryHighPercent`.
Validation when EquationType = CalibrationCurve: WorkflowType must be ElementalAssay, MethodAbbreviation
required, CalMinCorrelation + CalCorrelationType + CalMinStandards + both check-window bounds required, low <= high.

`TestAnalyte` (child of TestDefinition): Id, TestDefinitionId, Element (string 20, e.g. "Zn"),
WavelengthNm decimal(10,4), View (AnalyteView), LoqMgPerL decimal(18,6), DisplayOrder, IsActive.
Unique (TestDefinitionId, Element, WavelengthNm). CRUD endpoints under masterdata/test-definitions/{id}/analytes
(SectionHead/Admin), deactivate instead of delete once used by a run.

Equipment: IcpOes requires a CdsSoftware exactly like Hplc (MasterDataController equipment create/update
validation, currently HPLC-only).

`CalibrationRun`: Id, Code (unique), TestDefinitionId, SectionId (= test's section), EquipmentId (must be IcpOes,
same section, active), CalibrationStandardMaterialId, IcvStandardMaterialId (nullable), PerformedByUserId,
PerformedAt, SignatureId (sign against "TestDefinition"/test.Id like SST - ElectronicSignatures is append-only,
set the navigation not the id), Comment, `AnalytesPassed` int, `AnalytesTotal` int.
`CalibrationRunDocument` (one per run, mandatory): StorageKey via IFileStorageService
(`calibration-runs/{runId}/{guid}{ext}`), OriginalFileName, ContentType, SizeBytes, ContentSha256 (hex),
UploadedByUserId, UploadedAt - follow EquipmentDocument/MaterialDocument exactly.
`CalibrationRunAnalyte`: Id, CalibrationRunId, TestAnalyteId, Element / WavelengthNm / View (snapshots),
CorrelationValue decimal(10,6), CorrelationType, NumberOfStandards, LowestStandardMgPerL, HighestStandardMgPerL,
Passed, FailureReasons (string).
`CalibrationRunCheck`: Id, CalibrationRunAnalyteId, CheckType, SequencePosition int, NominalMgPerL (null for
Blank), MeasuredMgPerL, RecoveryPercent (computed; null for Blank), Passed.

Gate per analyte (server-side, never typed): correlation >= min, converting when the run's type differs from
the criterion's type (r² = r·r; r = √r²); NumberOfStandards >= min; every ICV/CCV recovery within
[low, high] inclusive; every Blank measured <= (CalBlankMax ?? analyte LOQ); every InternalStandard recovery
within the IS window when configured (IS rows with no configured window = error); calibration standard and ICV
standard (if given) have ExpiryDate and ExpiryDate.Date >= PerformedAt.Date (else every analyte fails with
reason "standard expired / no expiry date"); an analyte must have at least one ICV or CCV row (config
decision: yes). Recovery % = Measured / Nominal × 100, stored unrounded.

Service `CalibrationRunService` (Application) + `ICalibrationRunService`: CreateAsync(request, file stream,
userId, ip) - validate, sign, compute gate, generate code with unique-index retry, save document (hash),
all in one save; GetAll(filter: test, passed), GetById, GetReportDetails (as SST report). Section scope via
IUserSectionScopeService (add `EnsureCalibrationRunAccessAsync` like `EnsureSuitabilityRunAccessAsync`).
Controller `api/calibration-runs`: POST multipart (`payload` JSON + `report` file; refuse missing file - TC10),
GET list, GET {id}, GET {id}/report, GET {id}/document (download, verify hash). Return view records, never
entities with User navigations (PasswordHash leak).

Tests (xUnit InMemory + one Postgres create test): TC2 (92.4 % Pass), TC3 (89.0 % Fail -> analyte Fail),
TC4 (0.9989 < 0.999 Fail), TC5 (90.0 and 110.0 Pass), TC9 (expired standard), TC10 (no report),
r vs r² conversion, per-analyte independence (Zn fail, Ca pass - P2), code format + sequence, missing
expiry, IS without window rejected, equipment not ICP-OES rejected.

## Slice S2 - frontend for S1

FP Test Master: CalibrationCurve equation reveals analyte list editor + acceptance block ("Requires
calibration run" locked on). Equation Types page entry. FP Instruments: ICP-OES type + Syngistix CDS.
Calibration Runs page (LABORATORY menu, like System Suitability): create dialog (header, standards,
report upload, analyte grid with expandable check rows; paste-from-spreadsheet optional), list with
per-analyte pass chips, printable report page like the suitability run report.

## Slice S3 - element results (backend) / S4 - result entry + summary/CoA (frontend)

Specified after S1/S2 review: `Specification` += ResultBasis, LabelClaim, LabelClaimUnit, ConversionFactor
(default 1.0), TestAnalyteId; digest record per test order (W g, V mL, Wu g); element result per
(TestOrder, Specification) with calculation snapshot (§6 of scoping, TC1), OverRange / <LOQ flags ->
RequiresReview (TC6/TC7), only passing analytes of the order's method selectable (TC8); approval gate,
review return, projection, summary and CoA per element.
