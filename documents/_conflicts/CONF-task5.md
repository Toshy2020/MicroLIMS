# Conflicts raised by Task 5 (areas RCV, WAT, ENV, ACL, MON)

IDs CONF-060 to CONF-089 belong to this file. The orchestrator copies them into `documents/Conflicts.md`. Each entry states what the code does and what the other source says. No entry takes a side.

### CONF-060
- **Area:** RCV
- **Status:** Open
- **What the code does:** The reference number sequence is the count of samples of the same category received in the laboratory month, plus 1. The count and the save are separate and no lock is taken. A unique index on the reference number makes the second of two simultaneous receipts fail at save; nothing retries in the item-based receipt. The water receipt does retry once with a new number, then refuses with nothing saved. Location: `backend/MicroLIMS.Application/Services/ReferenceNumberGenerator.cs` — `GenerateAsync`; used by `backend/MicroLIMS.Application/Workflows/ProductWorkflowEngine.cs` — `ReceiveAsync`; retry in `backend/MicroLIMS.Application/Workflows/WaterWorkflowEngine.cs` — `ReceiveAsync`. Index: `backend/MicroLIMS.Persistence/Configurations/SampleConfiguration.cs`. This is the code location for CONF-004.
- **What the other source says:** `documents/Conflicts.md` CONF-004 asks how numbers are to stay unique under concurrent receipt. The XML comment on the class says the sequence "resets monthly". No approved line in `documents/` giving the number format was searched for beyond `BusinessRules.md` lines 33 to 51, which do not mention it.
- **Question:** Is a failed second receipt (database error, user must retry) acceptable, or must the number be allocated safely? Should voided samples keep their number in the count (they do now)?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-061
- **Area:** RCV
- **Status:** Open
- **What the code does:** Test orders are created in more places than receipt: `ProductWorkflowEngine.ReceiveAsync` (receipt), `SampleTestSelectionService.SelectCoreAsync` (periodic retest), `AddLaboratoryService.AddCoreAsync` (added laboratory) and `SampleCorrectionService` (rebuild after an item change or a grade change). Location: `backend/MicroLIMS.Application/Workflows/ProductWorkflowEngine.cs`, `backend/MicroLIMS.Application/Services/SampleTestSelectionService.cs`, `backend/MicroLIMS.Application/Services/AddLaboratoryService.cs`, `backend/MicroLIMS.Application/Services/SampleCorrectionService.cs`.
- **What the other source says:** `documents/DesignSpecification/BusinessRules.md` lines 44 to 49 say no user at any role can create a TestOrder by hand and the only entry points are "the five `ReceiveAsync` methods on the workflow engines". The code has four engine `ReceiveAsync` methods (Product, Water, EM, AfterCleaning). Water, EM and After Cleaning create their orders in `PrepareAsync`, not in `ReceiveAsync` (see FS-WAT, FS-ENV, FS-ACL).
- **Question:** Should the business rule be reworded to list all the places that create test orders, or should some of them be removed?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-062
- **Area:** RCV
- **Status:** Open
- **What the code does:** Adding a laboratory to a sample is allowed to any holder of `Samples.Receive`, with no laboratory-membership check and no sample-scope check. Correct, void and assign-analyst check scope. Location: `backend/MicroLIMS.API/Controllers/SampleController.cs` — `AddLaboratory`; `backend/MicroLIMS.Application/Services/AddLaboratoryService.cs` — `AddCoreAsync`.
- **What the other source says:** `CLAUDE.md` section 5, item 2 (Anti-IDOR) says backend services must verify resource-level and laboratory-section scope before mutating a record by ID. `Samples.Receive` is described in `ReceiptLabGuard` as the permission that may target any active laboratory, which may be the intended scope.
- **Question:** Is "any laboratory, any sample" the intended scope for `Samples.Receive`, or must the sample-scope check be added?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-063
- **Area:** RCV
- **Status:** Open
- **What the code does:** Voiding a sample needs `Samples.Correct`, a reason and a password. It refuses only a sample that is already Voided. A sample that is InTesting, in review or Approved can be voided. No second person is involved and there is no stale-form version check. Location: `backend/MicroLIMS.Application/Services/SampleCorrectionService.cs` — `VoidCoreAsync`.
- **What the other source says:** Correction of the same sample is refused once the sample is submitted for review (`SampleCorrectionService.IsCorrectable`), with the comment "review signs the data as it stands". `CLAUDE.md` Principle 3 says an actor cannot review or approve their own work across the record lifecycle. No line in `documents/` was found on voiding an approved sample.
- **Question:** May a sample that is submitted for review or Approved be voided by one person? If yes, should a second signer be required?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-064
- **Area:** WAT
- **Status:** Open
- **What the code does:** The water limit comparison is `SpecLimitParser.Compare` (specification, then action, then alert; first one exceeded wins) and can return the status LimitsNotConfigured as well as WithinLimits, AlertLimitExceeded, ActionLimitExceeded and OutOfSpecification. The single-point calculate path reads the limits for the sample's `WaterSamplingPointId`, and no code in Application or API sets that field; every prepared water test has sample locations, which that path refuses. The interface comment in the same file says "Alert -> Action -> Specification, in that order of severity". Location: `backend/MicroLIMS.Application/Workflows/WaterWorkflowEngine.cs` — `CalculateAndCompareAsync`, `Compare`; `backend/MicroLIMS.Application/Services/SpecLimitParser.cs` — `Compare`.
- **What the other source says:** `documents/DesignSpecification/BusinessRules.md` lines 83 to 98 describe the water engine as entering readings, averaging, and comparing "Specification, Action, Alert" with four status values (no LimitsNotConfigured), and say limits are read from the sampling configuration by test code and the sample's sampling point.
- **Question:** Is the single-point calculate path still wanted? Should the business rules file describe the per-location batch entry, and should LimitsNotConfigured be listed?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-065
- **Area:** WAT
- **Status:** Open
- **What the code does:** The daily water report reads the average and status back from the text field `InterpretedValue` by splitting on brackets. The batch method writes "n locations: x conform, y alert/action/spec (worst: status)" in that field, so the parsed average is 0 and the status text is "worst: status". The date match uses the stored date part without conversion to the laboratory time zone. All matching rows are loaded without paging. Location: `backend/MicroLIMS.Application/Workflows/WaterWorkflowEngine.cs` — `GetDailyAggregateAsync`, `ParseAggregateResult`; text written by `backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs` — `RecordWaterBatchReadingsCoreAsync`.
- **What the other source says:** No line in `documents/` describes the daily report. `CLAUDE.md` section 5.4 says read queries must project in SQL and section 5.6 says to use the injected clock.
- **Question:** What should the daily report show for batch results, and should it read the stored numeric values instead of the display text?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-066
- **Area:** WAT
- **Status:** Open
- **What the code does:** Storage condition is checked differently in two places. Preparation accepts any non-blank text and needs a storage time only for exactly "Refrigerator" (negative time not refused). Correction accepts only "Refrigerator" or "RoomTemperature" and refuses a negative time. Location: `backend/MicroLIMS.Application/Workflows/WaterWorkflowEngine.cs` — `PrepareCoreAsync`; `backend/MicroLIMS.Application/Services/SampleCorrectionService.cs` — `CorrectCoreAsync`.
- **What the other source says:** No line in `documents/` was found on the storage condition values or the hold time.
- **Question:** Which values are valid for the storage condition of a water sample, and should preparation apply the same checks as correction?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-067
- **Area:** ENV
- **Status:** Open
- **What the code does:** No code in Application or API sets `RoomMonitoring.IsOutOfTrend` or computes a step 1 plus step 2 count for environmental monitoring. The entity and `EMResultDto` carry the fields. Environmental monitoring locations are compared against alert, action and specification limits only. Location: `backend/MicroLIMS.Domain/Entities/RoomMonitoring.cs`; `backend/MicroLIMS.Application/Workflows/EMWorkflowEngine.cs`; `backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs` — `RecordBatchResultsCoreAsync`.
- **What the other source says:** `documents/DesignSpecification/BusinessRules.md` lines 102 to 118 describe a two-step incubation per room and an automatic out-of-trend flag set when the sum of the two counts is above the action limit, persisted to `RoomMonitoring.IsOutOfTrend`, and say step 2 cannot start before step 1 is completed.
- **Question:** Is the out-of-trend rule still required? If yes, where should it be computed; if no, should the business rules file and the unused entity fields be removed?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-068
- **Area:** ACL
- **Status:** Open
- **What the code does:** After-cleaning preparation creates one test order per distinct test code (or one per part for HPLC and ICP method tests in the physicochemical section) from the tests configured on the chosen machine parts. It has no collective pathogen order and no order named from "TAMC" and the part name. Location: `backend/MicroLIMS.Application/Workflows/AfterCleaningWorkflowEngine.cs` — `PrepareCoreAsync`.
- **What the other source says:** `documents/DesignSpecification/BusinessRules.md` lines 120 to 133 say one collective pathogen test order is created when a selected part is marked `IsCollectivePathogenSample`, and every selected part gets its own individual count order with test code "TAMC:{partName}".
- **Question:** Which description is correct, and should the business rules file be rewritten to match the configuration-driven engine?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-069
- **Area:** ACL
- **Status:** Open
- **What the code does:** The cleaning residue mean is evaluated against the item's specification row for the analyte. The analyte's MACO limit and unit (stored on the HPLC method) are carried into the calculator but are not used in the comparison; `HplcSampleEntryContext` also shows the MACO limit as the upper limit in the entry screen data. Location: `backend/MicroLIMS.Application/Helpers/CleaningResidueCalculator.cs` — `Evaluate`; `backend/MicroLIMS.Application/Services/HplcSampleEntryContext.cs` — `BuildCleaningResidueInputs`.
- **What the other source says:** No line in `documents/` was searched that fixes whether the MACO limit or the specification row decides conformity. The comment in the calculator cites "spec 2026-11-07", which was not found under `documents/`.
- **Question:** Which limit decides the conformity of a cleaning residue result, and must the two always be equal?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-070
- **Area:** MON
- **Status:** Open
- **What the code does:** "Excursion" means different things in two places. The occurrence status counts an approved result at the alert limit as PerformedExcursion. The deviation scan opens a deviation only for action limit, out of specification or detected, and not for the alert limit. A rejected, retested or superseded test also counts as PerformedExcursion in the status, but opens no deviation. Location: `backend/MicroLIMS.Application/Services/Monitoring/OccurrenceStatus.cs` — `OccurrenceProgressRules.For`; `backend/MicroLIMS.Application/Services/Monitoring/MonitoringDeviationService.cs` — `OpenExcursionsAsync`.
- **What the other source says:** The code comment in `OccurrenceProgressRules.For` says a rejected, retested or out-of-limit result still counts as performed because the sampling was done ("spec 10, N2"); that spec was not found under `documents/`. `documents/DesignSpecification/BusinessRules.md` has no monitoring plan section.
- **Question:** Should an alert-limit result be shown as an excursion in the plan status but open no deviation? Is it correct that a rejected test counts as the planned sampling done?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-071
- **Area:** MON
- **Status:** Open
- **What the code does:** When several deviations are closed in one request, one pair of signatures is created and bound to the first deviation's ID only; the other deviations link to the same two signatures. Location: `backend/MicroLIMS.Application/Services/Monitoring/MonitoringDeviationService.cs` — `CloseAsync`; `backend/MicroLIMS.Application/Services/Monitoring/DualSignature.cs` — `SignAsync`.
- **What the other source says:** `CLAUDE.md` Rule 3.3 item 5 says a signature must be permanently bound to the exact signed record version and not be transferable to another record. The same pair of signatures covers up to 500 deviations.
- **Question:** Is one signature pair for a batch of deviations acceptable, or must each deviation carry its own signature?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-072
- **Area:** ENV
- **Status:** Open
- **What the code does:** `EnsureSectionsAccessAsync` passes when the user is a member of at least one of the listed sections. Environmental monitoring preparation passes the sections of all chosen tests, so a user who belongs to only one of several sections involved can create orders for the others. Location: `backend/MicroLIMS.Application/Services/UserSectionScopeService.cs` — `EnsureSectionsAccessAsync`; used by `backend/MicroLIMS.Application/Workflows/EMWorkflowEngine.cs` — `PrepareCoreAsync`.
- **What the other source says:** `CLAUDE.md` section 5, item 2 says services must verify laboratory-section scope before mutating a record. The error text of the method ("These tests belong to a laboratory section you are not assigned to") reads as if every section must be in scope.
- **Question:** Must the user belong to every section involved, or is one enough?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-073
- **Area:** MON
- **Status:** Open
- **What the code does:** No method of the monitoring plan or deviation services checks the laboratory section of the user. A holder of `MonitoringPlan.View`, `Manage` or `Approve` sees and changes the plan for every laboratory. The approver of a revision is not required to differ from the user who generated the draft. Location: `backend/MicroLIMS.Application/Services/Monitoring/MonitoringPlanService.cs` — `GenerateDraftAsync`, `ApproveAsync`.
- **What the other source says:** `CLAUDE.md` section 5, item 2 requires section scope checks; Principle 3 requires that a person who performs a step cannot approve their own work across the regulated record lifecycle (monitoring plan revisions are not named there).
- **Question:** Is plan-wide access intended for these permissions, and must the approver differ from the generator?
- **Decision:** Not decided.
- **Date:** Not applicable
