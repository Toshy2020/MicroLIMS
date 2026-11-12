# FS-ACL Functional Specification

| Field | Value |
|---|---|
| Document number | FS-ACL |
| Title | After-cleaning verification functional specification |
| Version | 0.1 |
| Status | Draft |
| Author | Claude (AI agent), draft from code |
| Reviewer | To be assigned by QA |
| Approver | To be assigned by QA |
| Effective date | Set when Approved |

## Change history

| Version | Date | Author | Change |
|---|---|---|---|
| 0.1 | 2026-11-12 | Claude (AI agent), draft from code | First draft, written from the code as built. |

## Scope and reading notes

- This document covers `AfterCleaningController` (route `api/aftercleaning`), `AfterCleaningService`, `AfterCleaningWorkflowEngine`, `CleaningTechniques` and `CleaningResidueCalculator`. Microbiological count recording for after-cleaning locations is done by `IncubationStepRecorder` and is in FS-MIC-008, FS-MIC-006 and FS-MIC-010; HPLC runs and the HPLC assay recorder are in FS-INS.
- Every statement was written from the code files named in each entry. Nothing was run. Pharmacopoeia and guideline chapters are pointers only (to be confirmed by QA).
- Machines, machine parts, their test configurations, limits, units, MACO limits, extraction volumes, standard concentrations and cleaning HPLC methods are configured by the Section Head. No limit or constant is in the code.
- No method in this document calls `RecordVersion.EnsureCurrent` (no stale-form version check). `AfterCleaningService` is a thin wrapper.
- None of the methods here writes an explicit audit row. Whether the context's automatic audit capture covers the rows they change was not checked in this task.
- The business rules file (section 6) describes a collective pathogen test order and a "TAMC:{part}" order per part. The engine does not do this (FS-ACL-004, CONF-068).

## Functions

### FS-ACL-001
**Satisfies:** URS-ACL-001, URS-ACL-002, URS-ACL-003
**Function:** Receive an after-cleaning sample (`POST api/aftercleaning/receive`). Permission: `Samples.ReceiveOwnLab`. Inputs: machine ID, cause of testing ID, sampled by, control number, previous product name, previous product batch number, sampled-at time, laboratory section ID, previous product item ID, cleaning techniques. The received-by user is the caller. `AfterCleaningWorkflowEngine.ReceiveAsync`: the machine must exist and be active; the previous product batch number must not be blank. The laboratory section is resolved by `ResolveSectionForCreateAsync` (write access): if one is requested it must be an active section of which the caller is a member; if none is requested the caller must belong to exactly one section. When the section is the physicochemical section, the caller must hold the physicochemical area for the AfterCleaning category (else `UnauthorizedAccessException`). If a previous product item is given it must exist, be active and be a Finished Product, and its name becomes the previous product name. In the physicochemical section the previous product item is required and at least one cleaning technique is required; `CleaningTechniques.Normalize` accepts only UV, HPLC and ICP (ignoring letter case), removes duplicates and stores them in the fixed order UV, HPLC, ICP, joined by commas. In other sections no technique is stored and, when no item is given, the previous product name text is required. The sample stores the batch number as the previous product's batch number. A sampling time after the receipt time is refused before anything is saved. The cause of testing, sampled-by and control number are not checked by this method. The reference number and the one-time retry on a unique-index clash are as in FS-WAT-002 and FS-RCV-003. No test orders are created at receipt.
**Code:** `backend/MicroLIMS.Application/Workflows/AfterCleaningWorkflowEngine.cs` — `ReceiveAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-060

### FS-ACL-002
**Satisfies:** URS-ACL-004
**Function:** Prepare an after-cleaning sample (`POST api/aftercleaning/prepare`). Permission: `Samples.ReceiveOwnLab`. Inputs: sample ID, machine part configuration IDs, optional monitoring occurrence IDs. Wrapped in a transaction only when occurrence IDs are sent. Rules in order: the sample must exist and have preparation status NeedsPreparation; at least one part configuration must be selected; each must exist, be active and have an active machine part; each part must belong to the sample's machine. The laboratory is the sample's lab section, or the Microbiology section when empty. Only tests whose definition belongs to that laboratory are allowed (for the physicochemical section also the area for the AfterCleaning category); a configuration whose test is not allowed makes the method refuse ("belongs to another laboratory"). Then `EnsureSectionsAccessAsync` is called for the laboratory. For each configuration one `SampleLocation` of type MachinePart is created. Test orders: in the physicochemical section a test of HPLC or ICP method type gets its own `TestOrder` per configuration (so one order per part); every other test gets one order per distinct test code shared by all its parts. Orders are Pending, Waiting, in the laboratory section, with the preparing user as assigned analyst without applying `AnalystAssignmentRules`. If occurrence IDs are sent, `MonitoringLinkService.LinkAsync` is called (FS-MON-004). The preparation status becomes Ready. The controller makes no sample-scope call.
**Code:** `backend/MicroLIMS.Application/Workflows/AfterCleaningWorkflowEngine.cs` — `PrepareAsync`, `PrepareCoreAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-061, CONF-068

### FS-ACL-003
**Satisfies:** URS-ACL-005
**Function:** Technique and HPLC-method gate at preparation, physicochemical section only. The technique of each chosen test is taken from its workflow type by `CleaningTechniques.Of`: HPLC method assay gives HPLC, ICP method assay gives ICP, any other workflow type gives UV. If the technique is not among those stored on the sample at receipt, the method refuses ("which was not chosen at receipt"). For an HPLC method assay test, the test's HPLC method must exist, have result mode CleaningResidue and have a product item equal to the sample's previous product item; otherwise the method refuses with the message "No cleaning HPLC method for" the product name and the test code. No equivalent check is made for an ICP test, and none in other sections.
**Code:** `backend/MicroLIMS.Application/Workflows/AfterCleaningWorkflowEngine.cs` — `PrepareCoreAsync`; also `backend/MicroLIMS.Application/Helpers/CleaningTechniques.cs` — `Of`, `Normalize`
**Verified by:** reading (not run)

### FS-ACL-004
**Satisfies:** URS-ACL-004
**Function:** Test orders generated at preparation, compared with the business rules file. The engine creates orders from the tests of the selected machine part configurations only. It has no reference to a collective pathogen sample and no order named from "TAMC" and the part name. `IsCollectivePathogenSample` appears in the code read only as a word in a DTO comment, not as a property used by the engine. Which tests a part has comes from the Section Head's configuration of the machine part. No rule in this method requires a count test for every part.
**Code:** `backend/MicroLIMS.Application/Workflows/AfterCleaningWorkflowEngine.cs` — `PrepareCoreAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-068

### FS-ACL-005
**Satisfies:** URS-ACL-007
**Function:** What is specific to after-cleaning microbiology in batch count entry (the full rule is FS-MIC-008, endpoint `batch-results`, `IncubationStepRecorder.RecordBatchResultsCoreAsync`). Per machine part location: readings are averaged, dilution factor 1, reported text "<1" when the average is below 1 otherwise the average rounded to a whole number (`MidpointRounding.AwayFromZero`). The limit texts come from the location's machine part configuration (the room configuration is read first and is empty for these locations) and are compared as in FS-MIC-006, most severe first. The unit is the configured unit or, if empty, "CFU/25 cm2" for a machine part test type "Swab" and "CFU/mL" for "Rinse"; any other type makes the derivation throw. Growth observations for presence/absence tests are FS-MIC-010.
**Code:** `backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs` — `RecordBatchResultsCoreAsync`, `DeriveBatchLocationUnit`
**Verified by:** reading (not run)
**Conflicts:** CONF-006, CONF-010, CONF-011

### FS-ACL-006
**Satisfies:** URS-ACL-006
**Function:** HPLC cleaning residue calculation, as coded in `CleaningResidueCalculator.Calculate`. Inputs per analyte: standard concentration (µg/mL), extraction volume (mL), swabbed area (cm2, optional), unit (µg/swab or µg/cm2), mean standard response, and the replicate responses. Refusals: standard concentration, extraction volume or mean standard response not above zero; unit µg/cm2 with no swabbed area above zero; no replicates; a negative response. Per replicate: µg/mL = response divided by mean standard response times standard concentration; µg/swab = µg/mL times extraction volume; for unit µg/cm2 the value is µg/swab divided by the swabbed area, otherwise the value is µg/swab. The result is the arithmetic mean of the replicate values, not rounded. `Evaluate` then passes the mean to `SpecificationEvaluator.Evaluate` with the item's specification row for the analyte (FS-MIC-007). The reported text is the mean rounded to two decimals (`AwayFromZero`) followed by the unit, or the text "Not detected" when every replicate response is exactly zero (the evaluation still runs on the mean, which is then 0). The MACO limit of the analyte is carried in the calculator input but this class does not use it in any calculation; the status comes from the specification row. The inputs are built from the run snapshot and the SST mean by `HplcSampleEntryContext.BuildCleaningResidueInputs` and the stored calculation JSON is written by `HplcMethodAssayRecorder` (both in FS-INS).
**Code:** `backend/MicroLIMS.Application/Helpers/CleaningResidueCalculator.cs` — `Calculate`, `Evaluate`, `FormatValue`, `IsNotDetected`
**Verified by:** reading (not run)
**Conflicts:** CONF-069

### FS-ACL-007
**Satisfies:** URS-ACL-008
**Function:** Advance and validate for an after-cleaning test order (`AfterCleaningWorkflowEngine.AdvanceAsync`, `ValidateAsync`). Same logic as FS-ENV-004: an order at Waiting, Running or Incubating returns the error "Batch results must be entered for every location before this test order can proceed."; Ready moves to Reviewed and Reviewed to Approved through `WorkflowStateMachine.TransitionAsync`; any other step is left unchanged. No segregation-of-duties check and no stale-form version check in this method.
**Code:** `backend/MicroLIMS.Application/Workflows/AfterCleaningWorkflowEngine.cs` — `AdvanceAsync`, `ValidateAsync`
**Verified by:** reading (not run)
