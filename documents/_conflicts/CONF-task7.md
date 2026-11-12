# Conflicts raised by Task 7 (areas MED, CRY, INV, EQP)

IDs CONF-120 to CONF-128 are used in this file; CONF-129 to CONF-149 are unused. The orchestrator copies the entries into `documents/Conflicts.md`. Each entry states what the code does and what the other source says. No entry takes a side. CONF-003 (decided by the user, 2026-11-11) is not repeated here; the code locations found for it are in FS-EQP-012.

### CONF-120
- **Area:** MED
- **Status:** Open
- **What the code does:** The media lot is qualified by a media evaluation (challenges per lot, one result each, outcome Conform or NonConform) and released by a signed decision. No step records a sterility check, and no method sets or reads a GPT stage. The release decision approves a lot whose evaluation is Completed and Conform; it does not test the lot's expiry date or a sterility result. The lot is marked released for use by the signed decision alone (`IsReleasedForUse` set true). Other methods test the expiry date at the point of use; `IncubationStepRecorder` does not (see FS-MIC-002). The enum `GptStage` (Media Preparation, Sterility, Recovery, Release) still exists in the domain but is not used by the services read. Location: `backend/MicroLIMS.Application/Services/MediaReleaseService.cs` — `DecideCoreAsync`; `backend/MicroLIMS.Application/Workflows/MediaEvaluationEngine.cs` — `RecordResultAsync`; `backend/MicroLIMS.Domain/Enums/GptStage.cs`.
- **What the other source says:** `documents/DesignSpecification/BusinessRules.md` lines 136 to 153 (section 7) describe a GPT engine in `Application/Workflows/GptWorkflowEngine.cs` with stages Media Preparation, Sterility, Recovery, Release; advancing is strict; a lot passes only if every challenge organism passed; and `IsReleasedForUse` is true only when the stage is Release, the status is Active and the lot is not expired, checked together. That file does not exist in the repository. `CLAUDE.md` line 102 (Rule 3.2) says media used in testing must have passed GPT and sterility checks, be within expiry and be linked by prepared lot number.
- **Question:** Is the media evaluation plus the signed release the intended replacement for the GPT engine of the business rules? Must a sterility check be recorded before release, and must the expiry date be tested at release and at every use?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-121
- **Area:** MED
- **Status:** Open
- **What the code does:** For a growth promotion challenge, the outcome is Conform when no minimum recovery is configured (the maximum is then not tested at all). When a minimum is configured and the maximum is empty, the test `recovery <= maximum` is false and the outcome is always NonConform. Location: `backend/MicroLIMS.Application/Workflows/MediaEvaluationEngine.cs` — `RecordResultAsync`.
- **What the other source says:** No approved specification line in `documents/` was found that says what a missing minimum or maximum recovery means. The code comment in the same file says recovery is compared with the configured band, both ends included.
- **Question:** Should a growth promotion challenge with no configured recovery band be refused instead of conforming? Should a band with a minimum and no maximum (or the reverse) be refused when it is configured, or when it is used?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-122
- **Area:** MED
- **Status:** Open
- **What the code does:** Recovery percent is rounded to one decimal place with `Math.Round(value, 1)`, which uses the .NET default midpoint rule (to the nearest even digit). The count recorders round reported counts with `MidpointRounding.AwayFromZero`. The recovery is rounded before it is compared with the band, so a value just under the minimum may round up into the band. Location: `backend/MicroLIMS.Application/Workflows/MediaEvaluationEngine.cs` — `RecordResultAsync`; compare `backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs` — `RecordCountTestAsync`.
- **What the other source says:** No approved specification line in `documents/` was found on rounding of recovery percent or on whether the band is compared before or after rounding. `CLAUDE.md` Rule 3.2 says rounding rules must be applied strictly from configuration.
- **Question:** What rounding rule (mode and decimals) applies to recovery percent, and is the comparison with the band made on the rounded or the unrounded value?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-123
- **Area:** MED
- **Status:** Open
- **What the code does:** Equipment used in media work is not checked for calibration, type, range or service status. (1) Media preparation accepts any equipment of type Autoclave. Location: `backend/MicroLIMS.Application/Services/MediaPreparationService.cs` — `PrepareCoreAsync`. (2) The media evaluation incubation accepts any equipment ID without checking it exists, is an incubator, has a set point in the configured range, is in calibration or is in service. Location: `backend/MicroLIMS.Application/Workflows/MediaEvaluationEngine.cs` — `RecordIncubationCoreAsync`. (3) Cryovial identity-confirmation rows store the incubator ID without any check. Location: `backend/MicroLIMS.Application/Services/CryovialService.cs` — `PrepareCryovialsCoreAsync`. In contrast, incubator selection for test media checks the set point range and the calibration due date. Location: `backend/MicroLIMS.Application/Services/IncubatorEligibilityService.cs` — `GetEligibleIncubatorsAsync`.
- **What the other source says:** `CLAUDE.md` line 103 (Rule 3.2): active equipment linked to test execution must be within valid calibration or qualification status and expiry. Decision CONF-003: equipment with no calibration due date counts as in calibration.
- **Question:** Must the autoclave of a media lot and the incubators of a media evaluation and of a cryovial identity confirmation pass the same calibration, status and range checks as the incubators used for test media?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-124
- **Area:** CRY
- **Status:** Open
- **What the code does:** A thawed vial may be used for 24 hours (`CryovialThaw.UsableFor`), and a destruction reminder is set at 48 hours (`CryovialThaw.DestructionReminderAfter`). Both are fixed values in the code, not configuration. The same 24-hour value is used in the query that lists usable thawed vials. Location: `backend/MicroLIMS.Application/Services/CryovialThaw.cs` — `UsableFor`, `DestructionReminderAfter`, `EnsureThawedVialUsable`; `backend/MicroLIMS.Application/Services/MediaEvaluationService.cs` — `GetThawedVialOptionsAsync`.
- **What the other source says:** No approved specification line in `documents/` was found for the 24-hour and 48-hour periods (a search of `documents/` for "thaw" and "cryovial" found only a scope line in `documents/DocumentControl/ML-SYS-VP-001_Validation_Plan.md` line 32, with no period). The code comment says only that after 24 hours a vial may no longer be used. `CLAUDE.md` Principle 1 and Rule 3.2 say times and limits come from configuration set by the Section Head.
- **Question:** Are the 24-hour use period and the 48-hour reminder the approved rules? Should they be configured by the Section Head instead of fixed in the code?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-125
- **Area:** EQP
- **Status:** Open
- **What the code does:** When an inventory item is linked to the configuration list, a new incubator is given a set point of 32.5 (a literal in the code), and the new item is placed in the section with code `MICRO` (also a literal). The set point is then used to decide which media an incubator may serve. No set-point history row is written for it. Location: `backend/MicroLIMS.Application/Services/EquipmentConfigurationService.cs` — `LinkInventoryEquipmentToMasterAsync`.
- **What the other source says:** `CLAUDE.md` Rule 3.2 (line 101) says never hardcode a method-specific constant or limit in a service; Principle 1 says configuration by the Section Head drives behaviour. No approved specification line in `documents/` was found for the value 32.5.
- **Question:** Should a linked incubator start with no set point until the Section Head enters one, and should the section be taken from the register row?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-126
- **Area:** EQP
- **Status:** Open
- **What the code does:** One physical asset has two records, matched only by code text. The incubator check (FS-EQP-012 place 1) reads the configuration record and looks at its calibration due date and set point only; it does not read the register status. The register record carries the status, the calibration plan and the automatic out-of-service scan. The scan changes the register row only. A calibration record updates both due dates, but a manual edit of the register (`EquipmentInventoryService.UpdateAsync`) or of the configuration item (`EquipmentMasterDataService.UpdateEquipmentAsync`) changes only its own record, with no reason for the date. The configured summary shows the configuration date or, if that is empty, the register date. So an incubator can be shown as having a past due date and be out of service in the register while the incubator check still offers it. The HPLC and ICP availability checks read both records. Location: `backend/MicroLIMS.Application/Services/IncubatorEligibilityService.cs` — `GetEligibleIncubatorsAsync`; `backend/MicroLIMS.Application/Services/HplcRunService.cs` — `DetermineUnavailableReasonAsync`; `backend/MicroLIMS.Application/Services/EquipmentPlan/EquipmentWorkService.cs` — `ApplyOverdueAsync`, `RecordCalibrationAsync`; `backend/MicroLIMS.Application/Services/EquipmentConfigurationService.cs` — `GetConfiguredEquipmentSummaryAsync`.
- **What the other source says:** Decision CONF-003 (`documents/Conflicts.md`): equipment with no calibration due date counts as in calibration. The decision does not say which of the two records holds the due date, or whether an out-of-service register status makes an incubator ineligible. `CLAUDE.md` line 103 says equipment must be within valid calibration status.
- **Question:** Which record is the single source for calibration due date and service status? Should the incubator check refuse an incubator that is OutOfService or Retired in the register, as the HPLC and ICP checks do? When the configuration date is empty and the register date is set, which one decides?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-127
- **Area:** INV
- **Status:** Open
- **What the code does:** The activity type shown in equipment traceability is decided by text: a step name containing "Pathogen", or a test code starting with "PAT", gives "Pathogen Test"; a step name containing "GPT" gives "GPT"; anything else gives "Media Incubation". The media evaluation incubation has the step name "MediaEvaluation", so it shows as "Media Incubation". The category of idle equipment is decided from the instrument type text and the code prefixes INC, AUT and LAF. The same text rule is written three times. Location: `backend/MicroLIMS.Application/Services/EquipmentInventoryService.cs` — `GetActiveActivitiesForEquipmentInternalAsync` (and the history and where-is-it methods), `ClassifyEquipmentCategory`.
- **What the other source says:** `CLAUDE.md` line 100 (Rule 3.2): never branch on a test code. `CLAUDE.md` Principle 1: workflow type comes from configuration, not from code text.
- **Question:** Should the activity type come from the configured workflow type of the test or step instead of the test code prefix and step name text? Should media evaluation incubations be shown as their own type?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-128
- **Area:** EQP
- **Status:** Open
- **What the code does:** The calibration due date is compared in two ways. (a) As an instant: the incubator checks treat a due date at 00:00 UTC on the due day as past once the clock is after that instant, so from the first minute of the due day the incubator is refused. Location: `backend/MicroLIMS.Application/Services/IncubatorEligibilityService.cs` — `GetEligibleIncubatorsAsync`; `backend/MicroLIMS.Application/Services/GroupedTestAction/BatchSelectMediaRules.cs` — `EnsureIncubatorUsable`. (b) As a date: the HPLC and ICP checks and the automatic scan treat the item as overdue only when the due date is before today's lab date, so the item is usable all through the due day. Location: `backend/MicroLIMS.Application/Services/HplcRunService.cs` — `DetermineUnavailableReasonAsync`; `backend/MicroLIMS.Application/Services/EquipmentPlan/EquipmentWorkService.cs` — `ApplyOverdueAsync`. The register flag uses UTC date; the lab date used by the scan and by HPLC and ICP comes from the lab clock. Location: `backend/MicroLIMS.Domain/Entities/EquipmentInventory.cs` — `IsCalibrationOverdueAt`; `backend/MicroLIMS.Application/Services/SectionCountersService.cs` — `EquipmentCardAsync`.
- **What the other source says:** Decision CONF-003 covers a missing due date only. No approved specification line in `documents/` was found on whether the due day itself counts as in calibration, or on which time zone the day is read in.
- **Question:** Is equipment in calibration through the whole due date, or only until the start of it? Which clock (UTC or lab date) defines the day?
- **Decision:** Not decided.
- **Date:** Not applicable
