# Conflicts raised by Task 6 (areas PHY, INS, WST, SOL)

IDs CONF-090 to CONF-119 belong to this file. The orchestrator copies them into `documents/Conflicts.md`. Each entry states what the code does and what the other source says. No entry takes a side.

### CONF-090
- **Area:** PHY
- **Status:** Open
- **What the code does:** The shared check for physicochemical recording tests the equipment's inventory status (InService) and its section code ("FP") only. No calibration due date, calibration status or qualification status is read. Location: `backend/MicroLIMS.Application/Workflows/TestWorkflow/TestWorkflowSupport.cs` — `ValidateTestAnalysisOrderAsync`. HPLC and ICP method assays pass no equipment ID to this check, so even that check is skipped for them. Location: `backend/MicroLIMS.Application/Workflows/TestWorkflow/HplcMethodAssayRecorder.cs` — `SubmitCoreAsync`.
- **What the other source says:** `CLAUDE.md` Rule 3.2 ("Equipment, Columns & Standards Traceability") says active equipment linked to test execution must be within valid calibration/qualification status and expiry. `HplcRunService.DetermineUnavailableReasonAsync` does read `Equipment.CalibrationDueDate` when an HPLC or GC run starts.
- **Question:** Should every physicochemical result recording check the equipment's calibration and qualification status at the time of recording, as the run start does for HPLC, GC and ICP?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-091
- **Area:** PHY
- **Status:** Open
- **What the code does:** The value compared with the specification is not rounded in the same way across result types. Measurement and gravimetric results are compared before display rounding. Location: `backend/MicroLIMS.Application/Helpers/MeasurementCalculator.cs` — `Calculate`; `backend/MicroLIMS.Application/Helpers/GravimetricCalculator.cs` — `Calculate`. Titration, UV (non-limit modes) and outsourced numeric results are rounded first (to the larger of 2 decimals and the specification decimals) and the rounded value is compared. Location: `backend/MicroLIMS.Application/Workflows/TestWorkflow/TitrationRecorder.cs` — `RecordCoreAsync`; `UvRecorder.cs` — `RecordCoreAsync`; `OutsourcedRecorder.cs` — `RecordCoreAsync`. For a measurement, the displayed text can therefore show a value that looks equal to a limit while the stored status was decided on the unrounded value.
- **What the other source says:** No approved specification line in `documents/` was found on which value is compared with a limit. `documents/DesignSpecification/BusinessRules.md` has no physicochemical rule (searched for dissolution, titration, HPLC, assay, gravimetric, weight variation, working standard, solution).
- **Question:** Is a physicochemical result compared with its limit before rounding, or after rounding to the reported precision? Should every result type follow the same rule?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-092
- **Area:** PHY
- **Status:** Open
- **What the code does:** A later stage of dissolution, disintegration or weight variation changes the existing analysis rows instead of adding linked records. The stage method replaces the signature, the entered-at time and (when given) the comment on the same `TestAnalysis`, and replaces the reported value, text, calculation text and status of the same parameter result. The entering user of the analysis is not changed, so a later stage signed by another user shows the first user as the enterer. Weight variation also rewrites the deviation and pass flag of every stage 1 reading against the combined mean. Location: `backend/MicroLIMS.Application/Workflows/TestWorkflow/DissolutionRecorder.cs` — `RecordDissolutionStageCoreAsync`; `DisintegrationRecorder.cs` — `RecordDisintegrationStageCoreAsync`; `WeightVariationRecorder.cs` — `RecordWeightVariationStageCoreAsync`. The earlier values remain only in the audit trail.
- **What the other source says:** `CLAUDE.md` Rule 3.1 says corrections, invalidations and retests must append new linked records or versioned states while preserving the original recorded values, attribution and reason. Rule 3.3 point 5 says a signature is permanently bound to the exact signed record version.
- **Question:** Should each stage be stored as its own signed record (or a versioned state) so that the stage 1 signature and values stay on the record, as Rule 3.1 and Rule 3.3 describe?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-093
- **Area:** PHY
- **Status:** Open
- **What the code does:** Numbers that define the method are written in the code and used when the Test Master has no value: dissolution offsets 5, 15, 25 and 2; disintegration units 6 and 12, maximum stage 1 failures 2, minimum passes 16; weight variation unit count 20, tablet bands 130 mg at 10 %, 324 mg at 7.5 %, 5 %, tablet maximum outside 2, capsule inner 10 %, outer 25 %, stage limits 2, 6, 40 and 6; titration expansion coefficient 0.0011; UV wavelength 190 to 1100 nm and maximum absorbance 5. Location: `backend/MicroLIMS.Application/Workflows/TestWorkflow/DissolutionRecorder.cs` — `RecordDissolutionResultCoreAsync`; `DisintegrationRecorder.cs` — `RecordDisintegrationResultCoreAsync`; `WeightVariationRecorder.cs` — `RecordWeightVariationResultCoreAsync`; `backend/MicroLIMS.Application/Services/MasterData/TitrationDefinitionRules.cs` — `DefaultExpansionCoefficient`; `UvRecorder.cs` — `MinWavelengthNm`, `MaxWavelengthNm`, `MaxAbsorbance`. The dissolution code comment cites a laboratory decision of 2026-09-19.
- **What the other source says:** `CLAUDE.md` Frozen Guiding Principle 1 and Rule 3.2 say formulas, limits and step sequences come from the approved method/test configuration and that no method-specific constant, limit or formula may be hardcoded in a service. No approved specification in `documents/` was found for the laboratory decision of 2026-09-19.
- **Question:** Should these defaults be removed so that a test with no configured value is refused, or are they approved site rules that belong in an approved document?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-094
- **Area:** PHY
- **Status:** Open
- **What the code does:** Recording a physicochemical result checks laboratory section membership only. The Physicochemical area of the user (finished product, or raw and packaging material) is checked when the workspace is listed but not when a result is recorded. Location: `backend/MicroLIMS.Application/Services/UserSectionScopeService.cs` — `EnsureTestOrderAccessAsync`; compare `GetPhyschemAreasAsync` and `backend/MicroLIMS.Application/Services/TestingWorkspaceService.cs` — `ResolveAreaAsync`.
- **What the other source says:** `CLAUDE.md` Rule 5.2 says services must verify resource-level scope, e.g. that the user is assigned to the target LaboratorySection / PhyschemArea, before mutating a record by ID.
- **Question:** Should the recorders refuse a user whose area membership does not cover the sample's area?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-095
- **Area:** INS
- **Status:** Open
- **What the code does:** HPLC and ICP results are judged on a value that is not the value shown. HPLC assay text is rounded to 1 decimal (`FormatPercent`) while the status is decided on the unrounded mean; a dried or anhydrous basis is rounded to 2 decimals before the status is decided. ICP assay text is rounded to 1 decimal and ICP ppm text to 4 decimals while the status is decided on the unrounded value. Related substances are rounded to 2 decimals (or replaced by 0 below the disregard limit) before the status is decided. Location: `backend/MicroLIMS.Application/Helpers/HplcSampleAssayEvaluator.cs` — `Evaluate`, `FormatPercent`; `HplcAssayCalculator.cs` — `ApplyBasis`; `IcpSampleEvaluator.cs` — `Evaluate`; `RelatedSubstancesEvaluator.cs` — `Calculate`.
- **What the other source says:** No approved specification line in `documents/` was found on precision or on which value is compared. Related to CONF-091.
- **Question:** At which precision should HPLC, ICP and related-substance values be compared with the limit, and should the shown text always be the compared value?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-096
- **Area:** INS
- **Status:** Open
- **What the code does:** The calibration due date of an HPLC, GC or ICP instrument is read when a run starts. It is not read when SST or calibration is confirmed, when samples are assigned, or when a result is sent for review, which can be days after the run started. Mobile phase expiry is re-checked at SST confirmation but not at submission. Location: `backend/MicroLIMS.Application/Services/HplcRunService.cs` — `StartRunAsync`, `DetermineUnavailableReasonAsync`, `ConfirmSstAsync`; `backend/MicroLIMS.Application/Services/IcpRunService.cs` — `StartRunAsync`; `backend/MicroLIMS.Application/Workflows/TestWorkflow/HplcMethodAssayRecorder.cs` — `SubmitCoreAsync`; `IcpMethodAssayRecorder.cs` — `SubmitCoreAsync`. An instrument with no calibration due date counts as in calibration, which follows the decision recorded under CONF-003.
- **What the other source says:** `CLAUDE.md` Rule 3.2 says equipment and columns linked to test execution must be within valid calibration/qualification status and expiry. The column's own expiry is not read anywhere in `StartRunAsync`.
- **Question:** Should calibration status (and column and mobile phase expiry) be checked again when the result is sent for review?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-097
- **Area:** INS
- **Status:** Open
- **What the code does:** The ICP minimum correlation (0.999) and the calibration validity (24 hours) are constants in the code ("There is no method master any more, so these are system-wide"). The correlation r of each element is typed by the analyst; the server judges the typed number and does not read it from the calibration report file. Location: `backend/MicroLIMS.Application/Helpers/IcpDefaults.cs` — `MinCorrelation`, `CalibrationValidHours`; `backend/MicroLIMS.Application/Services/IcpRunService.Calibration.cs` — `SaveCalibrationAsync`, `ConfirmCalibrationAsync`.
- **What the other source says:** `CLAUDE.md` Rule 3.2 says never hardcode a method-specific constant or limit in a service. Frozen Principle 1 says configuration drives behaviour. The HPLC equivalents (SST criteria) are configured in the method master and copied into the run.
- **Question:** Are 0.999 and 24 hours approved site rules? Should the criteria be configured, and should the typed r be checked against the report or the curve?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-098
- **Area:** PHY
- **Status:** Open
- **What the code does:** The UV standard comparison reuses the HPLC weight-based assay formula: the total volume is passed as the theoretical sample weight and the standard volume as the theoretical standard weight, and the result is multiplied by the standard's purity and (100 minus moisture) / 100. Location: `backend/MicroLIMS.Application/Helpers/UvCalculator.cs` — `StandardComparison`; `backend/MicroLIMS.Application/Helpers/StandardComparisonCalculator.cs` — `CalculatePreparationAssay`.
- **What the other source says:** No approved method or specification in `documents/` was found for the UV standard comparison. The parameter names of `CalculatePreparationAssay` are weights, not volumes.
- **Question:** Is it correct to use volumes in the weight positions of the standard comparison formula for a UV test?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-099
- **Area:** WST
- **Status:** Open
- **What the code does:** The working standard rules are numbers in the code: 6 replicates, a maximum RSD of 2.0 %, a validity of 12 months and a due-soon warning of 30 days. The RSD is compared after rounding to 2 decimals. The qualification passes on replicate count and RSD only; the potency is not compared with any limit. Location: `backend/MicroLIMS.Application/Helpers/WorkingStandardCalculator.cs` — `WorkingStandardRules`, `PassesRsd`, `Evaluate`.
- **What the other source says:** The code comment calls them a "Fixed site rule" (spec 2026-10-03). `CLAUDE.md` Rule 3.2 says limits and replicate rules come from the approved method configuration. No approved specification in `documents/` was found for them.
- **Question:** Are these approved site rules? Should a potency range apply, and should the RSD be compared before rounding?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-100
- **Area:** WST
- **Status:** Open
- **What the code does:** Approving a requalification replaces the purity, moisture and expiry of the existing working standard lot in place. Results already recorded keep their own copies (the SST copies purity and moisture when it is saved), but the lot row itself no longer shows the earlier values; they remain in the audit trail only. Location: `backend/MicroLIMS.Application/Services/WorkingStandardService.SignOff.cs` — `ApproveAsync`.
- **What the other source says:** `CLAUDE.md` Rule 3.1 says master data changes must not retroactively alter historical records and that corrections append new linked records or versioned states.
- **Question:** Should a requalification create a new lot version or a linked record instead of changing the lot in place?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-101
- **Area:** SOL
- **Status:** Open
- **What the code does:** A prepared solution whose recipe needs release is kept out of the availability lists while it is PendingReview or Rejected, and the prepared-solution picker refuses it. The methods that actually use a mobile phase or a titrant check only that the effective status is Prepared; they do not read the approval status. Location: `backend/MicroLIMS.Application/Services/HplcRunService.cs` — `StartRunAsync`; `backend/MicroLIMS.Application/Workflows/TestWorkflow/TitrationRecorder.cs` — `LoadPreparationAsync`; compare `backend/MicroLIMS.Application/Services/SolutionPreparationService.cs` — `GetAvailableAsync`, `CheckSource`.
- **What the other source says:** The comment in `backend/MicroLIMS.Application/Services/SolutionPreparationReleaseService.cs` says a PendingReview or Rejected preparation is not offered for use. `CLAUDE.md` Rule 1 principle 2 says the backend must re-validate consequential inputs from the frontend.
- **Question:** Should the server refuse a mobile phase or titrant preparation that is pending release or rejected when it is used in a run or a titration?
- **Decision:** Not decided.
- **Date:** Not applicable
