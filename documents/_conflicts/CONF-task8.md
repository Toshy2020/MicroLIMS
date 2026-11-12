# Conflicts raised by Task 8 (areas MDM, OOS, RPT, NTF)

IDs CONF-150 to CONF-165 are used in this file; CONF-166 to CONF-179 are unused. The orchestrator copies the entries into `documents/Conflicts.md`. Each entry states what the code does and what the other source says. No entry takes a side. CONF-002 and CONF-008 already exist; the code locations found for them are given in the Task 8 report and in FS-NTF-003 and FS-RPT-006.

### CONF-150
- **Area:** MDM
- **Status:** Open
- **What the code does:** A specification row is changed in place. `UpdateSpecificationAsync` overwrites the limits, unit, basis, grade and other fields of the existing row and re-creates its stages. `DeleteSpecificationAsync` removes the row. No effective date, no version number and no copy of the old row is kept; the old values survive only in the automatic audit row. No reason for change is asked for, and the delete has no stale-form version check. The code comment in the delete method says no dependent exists because results copy the alert, action and specification values as strings at calculation time; that was not checked against the result tables. Location: `backend/MicroLIMS.Application/Services/MasterData/SpecificationMasterDataService.cs` — `UpdateSpecificationAsync`, `DeleteSpecificationAsync`.
- **What the other source says:** `CLAUDE.md` Rule 3.1 (line 92): modifying an Item Specification or limit must never retroactively alter or re-evaluate recorded or approved results; historical records must stay bound to the specification version, units, precision and criteria active when the test was performed and approved; changes must append new linked records or versioned states and keep the reason for change.
- **Question:** Does every recorded result keep its own copy of the limit text, unit, precision and criteria, so that changing or deleting a specification row cannot change a past verdict? If not, must a specification row be versioned, and must a reason be required?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-151
- **Area:** MDM
- **Status:** Open
- **What the code does:** A Test Master row and a workflow step are changed in place. `UpdateTestDefinitionAsync` can change the test code, equation type, workflow type, replicate counts, system suitability and the other settings without checking whether test orders or results already use the test. When the code is renamed, no other table that stores the old code as text (specifications, assigned item tests, test orders) is changed by this method. A reason is required only for titration settings and the moisture role. `UpdateTestWorkflowStepAsync` edits a step that test orders have already used (the comment says an incubation row keeps its own copy of temperature and duration), while `DeleteTestWorkflowStepAsync` refuses a step that was used. Location: `backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs` — `UpdateTestDefinitionCoreAsync`; `backend/MicroLIMS.Application/Services/MasterData/TestWorkflowStepMasterDataService.cs` — `UpdateTestWorkflowStepAsync`, `DeleteTestWorkflowStepAsync`.
- **What the other source says:** `CLAUDE.md` Rule 3.1 (line 92): modifying a Workflow Profile, STM or master data must never retroactively alter previously recorded results, and corrections must append linked records and keep the reason for change.
- **Question:** Should a Test Master row that already has test orders be protected from a change of code, equation type or workflow type? Should a reason for change be required for every change to a test, and for a change to a step that has been used?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-152
- **Area:** MDM
- **Status:** Open
- **What the code does:** When a dissolution, disintegration or weight-variation test is saved with a setting left empty, the code fills in a number written in the service. Dissolution offsets 5, 15, 25 and 2; disintegration stage-1 units 6, stage-2 units 12, maximum stage-1 failures 2, minimum pass total 16; weight variation unit count 20, tablet band 1 up to 130 mg at 10 percent, band 2 up to 324 mg at 7.5 percent, band 3 at 5 percent, maximum outside 2, capsule inner 10 percent and outer 25 percent, capsule stage-1 maximum outside 2 and for retest 6, stage-2 extra units 40 and maximum outside 6. The same defaults are written again on update. The weight-variation canonical specification text also contains the text "USP <2091>". Location: `backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs` — `CreateTestDefinitionAsync`, `UpdateTestDefinitionCoreAsync`; `backend/MicroLIMS.Application/Services/SpecificationService.cs` — `BuildCanonicalSpecLimit`.
- **What the other source says:** `CLAUDE.md` Rule 3.2 (line 101): never hardcode a method-specific constant, limit or formula in a service; calculations use the configured equation type, replicate rules, units, precision and acceptance criteria. `CLAUDE.md` Principle 1 (line 36): configuration drives behaviour. No approved document in `documents/` was found that states these default numbers.
- **Question:** Are these defaults an approved starting point that the Section Head must confirm, or should the fields be required with no default? Which approved document gives the numbers?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-153
- **Area:** MDM
- **Status:** Open
- **What the code does:** The workbook load sets items, material master entries and reference organisms inactive until a second person verifies the load. Specifications and specification stages created by the same load are active at once and are not set inactive; the verification does not touch them. A specification added for an item that is already active is therefore live before any second-person check. Location: `backend/MicroLIMS.Application/Services/MasterDataWorkbook/Sheets/SpecificationSheets.cs` — `CreateAsync`; `backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs` — `ImportAsync`, `VerifyAsync`.
- **What the other source says:** The workbook instructions say that a second person checks the load against the approved source documents and signs Verify, and that this activates the items, material master entries and reference organisms. Location: `backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataSheets.cs` — `Instructions`, line 48. Specifications are not mentioned.
- **Question:** Should specifications loaded by a workbook also wait for the second-person verification before they apply? Is it intended that media products and equipment loaded by a workbook are not held back either?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-154
- **Area:** MDM
- **Status:** Open
- **What the code does:** The workbook download filters sheets by the user's readable sections, but the specification and specification-stage sheets ignore that scope and list every specification. The list of loads (`GetImportsAsync`) and the stored file of a load (`GetFileAsync`) are available to any holder of `MasterData.Manage` with no section check. The existence test for rows during a load compares against all sections. Location: `backend/MicroLIMS.Application/Services/MasterDataWorkbook/Sheets/SpecificationSheets.cs` — `ExportAsync` (both sheets); `backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs` — `GetImportsAsync`, `GetFileAsync`; `backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookRunner.cs` — `RunAsync`.
- **What the other source says:** `CLAUDE.md` Section 5, item 2 (line 175): backend services must verify resource-level and laboratory-section scope before reading a record by ID. The same workbook download does apply the scope to the material master sheet.
- **Question:** Should the specification sheets, the load list and the stored load file be limited to the user's sections? Or are specifications and loads meant to be visible to all master-data users?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-155
- **Area:** MDM
- **Status:** Open
- **What the code does:** Room test configurations (environmental monitoring test type, test code, alert, action and specification limit text, unit) are created, changed and deleted by a holder of `MasterData.Manage` with no check of the laboratory that owns the test code, and the limit text is not checked by `LocationChemistryLimitRules`. The limits are overwritten in place. The corresponding water sampling configurations and machine part configurations do check test ownership and validate the limit fields. The control chart `apply-limits` action changes the same room limits only with a reason, a signature and a version check. Location: `backend/MicroLIMS.Application/Services/MasterData/EnvironmentalMonitoringMasterDataService.cs` — `CreateRoomTestConfigurationAsync`, `UpdateRoomTestConfigurationAsync`, `DeleteRoomTestConfigurationAsync`; compare `backend/MicroLIMS.Application/Services/MasterData/WaterMasterDataService.cs` — `UpdateWaterSamplingConfigurationAsync`.
- **What the other source says:** `CLAUDE.md` Section 5, item 2 (line 175): services must verify laboratory-section scope before mutating a record by ID. `CLAUDE.md` Rule 3.1 (line 92): changes to alert and action limits must not alter historical results.
- **Question:** Should room test configurations follow the same ownership and limit checks as water and after-cleaning configurations? Should a change of EM alert or action limits through the master-data screen need a reason and a signature, as it does through the control chart?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-156
- **Area:** RPT
- **Status:** Open
- **What the code does:** Two places decide behaviour from the text of a test code. The Certificate of Analysis matrix puts every test whose code starts with "TAMC" (upper-cased) first. The pathogen result matrix treats a test as quantitative when its workflow type is CountTest or its code contains "TAMC" or "TYMC". Location: `backend/MicroLIMS.Application/Services/CertificateOfAnalysisBuilder.cs` — `BuildMatrix` (line 99); `backend/MicroLIMS.Application/Services/PathogenSession/ResultMatrixBuilder.cs` (lines 36 and 37).
- **What the other source says:** `CLAUDE.md` Rule 3.2 (line 100): never branch on a test code or organism name; and Rule 3.2 (line 104): never order certificate rows by test code or name. `CLAUDE.md` Principle 1 (line 36): configuration drives behaviour. `OrganismLiteralTests` named in Rule 3.2 covers organism names; whether it covers these test-code literals was not checked.
- **Question:** Should the matrix column order and the quantitative decision come from the configured workflow type and the Test Master CoA order instead of the text of a test code?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-157
- **Area:** RPT
- **Status:** Open
- **What the code does:** The conformance of a result is decided differently in the two parts of the same report package. The Certificate of Analysis builder and the test card footer treat a missing status as "no result" (not certified) and a status with no configured limits as "limits not configured". The results matrix of the archived sample report counts a location with an empty status as conforming (`loc.Status is null || ConformingStatuses.Contains(...)`) and counts a LimitsNotConfigured location as an exception. The analysis card says "Out of limits" for any parameter whose status is not WithinLimits or Absent, including one with no limits configured. Location: `backend/MicroLIMS.Application/Services/ReportDocumentMapper.cs` — `ToResultsMatrix` (line 247), `ToAnalysisCard` (line 497), `ConformingStatuses` (line 210); `backend/MicroLIMS.Application/Helpers/ResultConformance.cs` — `ResultConformanceRules.FromStatus`.
- **What the other source says:** `CertificateOfAnalysisBuilder` states in its header comment that every verdict comes from `ResultConformanceRules` and that only a conforming result is certified. This is a related but separate point from CONF-008 (the private rule in `SampleApprovalService`).
- **Question:** Should the results matrix and the analysis card of the sample report use `ResultConformanceRules`, so that a missing result is never shown as conforming and a missing limit is not shown as "Out of limits"?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-158
- **Area:** RPT
- **Status:** Open
- **What the code does:** Dates and times in the archived sample report, including the signing time of each electronic signature, are formatted with the pattern "dd-MMM-yyyy HH:mm". No time zone or "UTC" mark is printed. Whether the value is converted from UTC before it reaches the mapper was not traced. Location: `backend/MicroLIMS.Application/Services/ReportDocumentMapper.cs` — `Dt` (line 16), `ToSignatureBlock` (line 44).
- **What the other source says:** `CLAUDE.md` Rule 3.3 item 5 (line 116): every generated report of a signed record must show the signer's printed full name, the UTC date and time of signing displayed with an explicit time zone, and the meaning. `CLAUDE.md` Section 5 item 6 (line 195): always display the time zone alongside regulated timestamps.
- **Question:** Should the report print the time zone with every regulated timestamp and signature time? Is the value passed to the mapper UTC?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-159
- **Area:** RPT
- **Status:** Open
- **What the code does:** The archived sample report rounds the average and calculated result of a count reading to a whole number with `MidpointRounding.AwayFromZero`, for display, and prints the reported result as stored. The Certificate of Analysis prints the stored calculated or CFU value with no rounding (format "0.############################"). A count value with a fraction can therefore read differently in the two documents. Location: `backend/MicroLIMS.Application/Services/ReportDocumentMapper.cs` — `Cfu` (line 205); `backend/MicroLIMS.Application/Services/CertificateOfAnalysisBuilder.cs` — `BuildMatrix`.
- **What the other source says:** `CLAUDE.md` Rule 3.2 (line 98): whole-number colony counts and the configured rounding rules are applied in backend services. CONF-001 records a different rounding difference (screen preview against server).
- **Question:** Which rounding applies to an average or calculated count shown in a report, and should the certificate and the sample report show the same number?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-160
- **Area:** RPT
- **Status:** Open
- **What the code does:** The trend and compare reports replace a result flagged below the detection limit by half of its detection limit (`DetectionLimit / 2`) before computing the mean, standard deviation, minimum, maximum and latest value, and the compare mean. The compliance percent leaves out results whose level is LimitsNotConfigured. The mean is rounded to 2 decimals and percentages to 1 decimal with the default `Math.Round` midpoint rule. None of this is configured by the Section Head. Location: `backend/MicroLIMS.Application/Services/ReportingQueryService.cs` — `Impute` (line 511), `GetTrendAsync`, `GetCompareBySubjectAsync` (lines 467 to 476).
- **What the other source says:** No approved specification in `documents/` was found for substituting half the detection limit, for the compliance percent, or for the rounding of these statistics. `CLAUDE.md` Rule 3.2 (line 98) says rounding and averaging rules come from configuration.
- **Question:** Is substituting half the detection limit the approved way to treat below-detection results in trends, and should the factor and the rounding be configured?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-161
- **Area:** NTF
- **Status:** Open
- **What the code does:** The approval and review waiting notifications count samples whose section sign-off is under approval or under review, for the role SectionHead (approval) or Reviewer and SectionHead (review), or for a holder of the permission `Dashboards.LabOverview` or `Dashboards.Review`. The incubation-ready notification also requires the test order not to be superseded and to be Pending or InProgress. Location: `backend/MicroLIMS.Application/Services/DashboardNotificationService.cs` — `ComputeAsync`.
- **What the other source says:** `documents/DesignSpecification/BusinessRules.md` section 12, lines 228 to 231: Approval Waiting is "any `TestOrder.Status == Reviewed`" shown to SectionHead and Admin only; Review Waiting is "any `TestOrder.Status == ResultEntered`" shown to Reviewer, SectionHead and Admin; Incubation Ready has only the completed-time and step conditions.
- **Question:** Which rule is right: waiting test orders by status, or samples by section sign-off? Which roles or permissions should see each notification?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-162
- **Area:** OOS
- **Status:** Open
- **What the code does:** The OOS tracking list loads sample entities with several related entities, with no `AsNoTracking`, no projection to DTOs in SQL, no paging and no row limit. Location: `backend/MicroLIMS.Application/Services/OosTrackingService.cs` — `GetOosGroupsAsync`.
- **What the other source says:** `CLAUDE.md` Section 5 item 4 (line 184): every read-only query in dashboards, reports, workspace listings and search services must use `AsNoTracking` and project to DTOs in SQL, and never materialize unbounded entity graphs.
- **Question:** Should the OOS tracking query be rewritten to meet the read-query rule, or is the list expected to stay small?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-163
- **Area:** RPT
- **Status:** Open
- **What the code does:** The reporting queries are not `AsNoTracking`, and the trend, compare and qualitative-event methods load the whole matching record list into memory before computing statistics. The search is paged with a cap of 100 and the CSV export is capped at 10,000 rows, but trend, compare, qualitative events and overview have no row limit. Location: `backend/MicroLIMS.Application/Services/ReportingQueryService.cs` — `GetTrendAsync`, `GetCompareBySubjectAsync`, `GetQualitativeEventsAsync`.
- **What the other source says:** `CLAUDE.md` Section 5 item 4 (line 184): read queries in reports must use `AsNoTracking`, project to DTOs in SQL and not materialize unbounded entity graphs.
- **Question:** Should these report queries be rewritten as SQL projections, or are they accepted at the present data volume?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-164
- **Area:** RPT
- **Status:** Open
- **What the code does:** The control chart action that applies proposed alert and action limits to room test configurations and water sampling configurations needs only the permission `MasterData.Manage`. It makes no check that the test or location belongs to a laboratory section the user may configure. The read scope is used only to load the series. Location: `backend/MicroLIMS.Application/Services/Spc/ControlChartService.cs` — `ApplyLimitsCoreAsync`, `LoadConfigurationAsync`.
- **What the other source says:** The master-data methods that change the same limits check test ownership: `TestCodeOwnership.EnsureCanEditAsync` in `WaterMasterDataService.UpdateWaterSamplingConfigurationAsync` and `AfterCleaningMasterDataService.UpdateMachinePartConfigurationAsync`. `CLAUDE.md` Section 5 item 2 (line 175): services must verify laboratory-section scope before mutating a record by ID.
- **Question:** Should applying control chart limits check that the user may configure the laboratory that owns the test code?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-165
- **Area:** NTF
- **Status:** Open
- **What the code does:** An error-severity notification sends an email to an address built from the user name, `{username}@microlims.local`, not from an e-mail address on the user record (whether the user record has an e-mail field was not checked). The email and the push are sent from a request that only reads the notification list, before the notification rows are saved. Location: `backend/MicroLIMS.Application/Services/DashboardNotificationService.cs` — `PersistAndDeliverAsync` (line 266).
- **What the other source says:** `documents/DesignSpecification/BusinessRules.md` section 12, lines 235 to 237: error notifications trigger an email through `IEmailSender` (no-op if SMTP is not configured) and a real-time push.
- **Question:** Is `{username}@microlims.local` the intended recipient, or should the user's stored e-mail address be used? Should the email be sent only after the notification rows are saved?
- **Decision:** Not decided.
- **Date:** Not applicable
