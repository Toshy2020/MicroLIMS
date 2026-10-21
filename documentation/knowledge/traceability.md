# Traceability

## C1 Platform & administration

<!-- from C1-platform.md -->

### MOD-AUDIT-001 Audit Trail drawer (traceability chain)
- Opened from an audit row ("Inspect Event & Traceability Chain"); chain by entity of the audit row [src: frontend/src/modules/auditSearch/components/AuditEventDrawer.tsx:L36; backend/MicroLIMS.Application/Services/AuditTraceabilityService.cs:L196-L220].
- Resolution by entity name (first match): Sample -> sample chain; Media -> media chain; Material -> material lot chain; Cryovial -> cryovial chain; EquipmentInventory -> equipment chain; Item -> item chain; ItemPreparationConfiguration -> item chain of its item; any other -> single node "General" showing entity name and id [src: backend/MicroLIMS.Application/Services/AuditTraceabilityService.cs:L186-L220].
- Node types the chain can show (as the UI displays `nodeType`, title, status): Item, PreparationConfiguration, Sample, SamplingPoint, Department, SamplePreparation, TestOrder, Result, Review, ElectronicSignature ("Signed"), Material, Media, MediaEvaluation, MaterialDocument, Organism ("Master Strain"), Cryovial, Equipment, EquipmentDocument, EquipmentStatusHistory [src: AuditTraceabilityService.cs:L232-L638].
- Sample chain nodes in order: sample -> preparation -> tests -> results -> reviews -> electronic signatures [src: AuditTraceabilityService.cs:L291-L435].
- "View Complete History for {entity} #{id}" opens all audit rows of that record, no cap [src: frontend/src/modules/auditSearch/components/AuditEventDrawer.tsx:L179-L187; AuditSearchService.cs:L76-L81].
- No stand-alone traceability page or menu item exists (GAP-108 covers coverage questions) [src: 01_APPLICATION_MAP §3.3, §13.5].

### MOD-NOTIF-001 / MOD-ERR-001 / MOD-DISC-001
- Notification links: an item tied to a sample or test order carries SampleId / TestOrderId to open that record [src: backend/MicroLIMS.Application/Services/DashboardNotificationService.cs:L14, L158-L168].
- Error incident links: incident to its error log entries (sources, occurrences) [src: ErrorMonitoringService.cs:L70-L100].
- Discussion post links: attachments and version history [src: DiscussionService.cs:L455-L470].

## C2 Sample flow & micro testing

<!-- from C2-sample-testing.md -->

As the UI exposes it (C2 scope).
- Sample (SMP ref) -> Test orders (one per test code; test order id "#{testOrderId}") -> Incubation(s) per step (media lot, incubator, stage 1/2, start/ready/read times) -> Count reading or location results or pathogen observation -> Result (interpreted "{reported} ({status})").
- Sample -> Assigned analyst (per test order, shown as "ASSIGNED TO").
- Sample -> Item documents: SOP and Verification Report (product-like). [SelectedSampleTestingPanel.tsx:385-387]
- Sample -> Pathogen session (sessionId) -> Shared TSB incubation (one per sample; media lot and incubator) -> Primary matrix (location x test) -> Confirmatory setup (media lots by MaterialId, incubator) -> Confirmatory readings -> Final Analytical Results Summary -> Evidence chain. [PathogenSessionDialog.tsx; SessionReviewPanel.tsx:85-141]
- Media lot -> step medium (Test Master) -> incubator equipment (setpoint range). Equipment calibration status "lapsed" excluded from grouped actions. [GroupedActionRow.tsx:74-92]
- Sample -> audit history dialog ("Audit History") and "Sample Summary" (C3).
- Sample preparation -> Test Master preparation configuration (item) and sampling configuration (water point, EM room/part configs). [PreparationDialog.tsx; EMPreparationForm.tsx:31-40]

---

## C3 Review, approval, reports out

<!-- from C3-review-approval.md -->

- Sample -> SampleSectionSignoff rows (one per section): status; reviewer and time; approver, decision and time; closer, reason and time; review, approval and close signatures; certificate remarks [src: SampleSectionRollup.cs:L44-L65; SampleApprovalService.cs:L309-L335; SectionClosureService.cs:L90-L95].
- Sample -> TestOrder (current and superseded). A superseded order is linked to its retest sample only through the new sample's OriginSampleId, not through a test-order field [src: SampleApprovalService.cs:L395-L409, L459-L473].
- Retest sample -> origin sample (OriginSampleId) -> OOS group (OosGroupCode) -> all samples in the group [src: OosTrackingService.cs:L11-L24, L72-L100].
- OOS group -> investigation documents (any non-voided shows as attached); supersession chain via SupersededByDocumentId [src: OosTrackingService.cs:L47-L53; OosInvestigationDocument.cs:L33-L34].
- Sample -> ReviewWorkflowEvent timeline, shown in Workflow History and in the PDF TIMELINE block [src: SampleSummaryDialog.tsx:39-51; SampleSummaryService.cs:L516-L524].
- Sample -> ArchivedRecord (frozen PDF at final decision; the origin is archived again after OOS resolution) [src: SampleApprovalService.cs:L534-L535, L636-L639].
- TestOrder -> TestReturnEvent (one per return) [src: SampleReviewService.cs:L256-L265].
- Two new samples from one New Sample Required decision share the OosGroupCode and both carry the origin's id as OriginSampleId [src: SampleApprovalService.cs:L455-L488].
- The Sample Summary dialog links to the Printable Report and View COA pages [src: SampleSummaryDialog.tsx:388-432].

## C4 Monitoring & micro configuration

<!-- from C4-monitoring-microconfig.md -->

### MOD-EM-001 Environmental Monitoring configuration
- Department -> Room (department id) -> RoomTestConfiguration (room id, test type, test code, limits, unit). At preparation each chosen configuration becomes a sample location linked to it by id (`SampleLocation.RoomTestConfigurationId`); the test order stores the test code as a copied string (no foreign key). Limits and unit are read from the linked configuration at result entry and copied onto the location. [src: EnvironmentalMonitoringMasterDataService.cs:L114-L116, L171-L172; backend/MicroLIMS.Application/Workflows/EMWorkflowEngine.cs:L100-L108; IncubationStepRecorder.cs:L497-L520] (verified)
- Sample -> Department (`Sample.DepartmentId`, Restrict). [src: SampleConfiguration.cs:L21]
- Test codes come from the Test Master (TestCodePicker). [src: TestCodePicker.tsx:L21]

### MOD-WATER-001 Water configuration
- WaterDepartment -> WaterSamplingPoint (department id, assigned test codes list) -> SamplingConfiguration (point id, test code, limits, unit). At preparation each location of a count test is linked to the point's limit row (`SamplingConfigurationId`) if one exists; samples reference the department (`WaterDepartmentId`) and, in the older model, the point (`WaterSamplingPointId`). [src: WaterMasterDataService.cs:L53-L62, L122-L127; WaterWorkflowEngine.cs:L125-L152; SampleConfiguration.cs:L20, L23] (verified)
- Test codes are chosen with the multi-select test picker (`TestCodePickerMulti`) from the Test Master. [src: water/components/WaterLocationPanel.tsx:L11, L160]

### MOD-AC-001 After Cleaning configuration
- Machine -> MachinePart (machine id) -> MachinePartConfiguration (part id, test type, test code, pathogen flag, limits, unit). At preparation each chosen configuration becomes a sample location linked by id (`MachinePartConfigurationId`); samples reference the machine (`MachineId`). [src: AfterCleaningMasterDataService.cs:L57-L67, L97-L103; AfterCleaningWorkflowEngine.cs:L85-L130] (verified)
- Count test codes come from the Test Master picker; pathogen codes come from the Test Master tests with workflow type Observation. [src: AcPanels.tsx:L155; PathogenPicker.tsx:L7-L12]

### MOD-ORG-001 Organisms master
- Organism -> media configuration challenges, media evaluation challenges, cryovials and materials (each blocks deletion) and -> Test Master step target organism (does not block deletion in the service; the database refuses, BR-ORG-002). [src: OrganismMasterDataService.cs:L61-L80; TestWorkflowStepConfiguration.cs:L24-L27] (verified)

### MOD-MEDIACFG-001 Media Configurations
- Media product (code) -> media lots (lot number prefix) -> material batch snapshot (name and code copied at receiving). [src: MediaProductService.cs:L19-L31, L151-L152]
- Media product -> incubation conditions (product id) -> media configuration (incubation condition id) and Test Master step media (incubation condition id; hours and temperatures copied at save). [src: MediaMasterDataService.cs:L96-L101; MediaIncubationConditionService.cs:L155-L165; TestWorkflowStepMasterDataService.cs:L285-L295] (verified)
- Media configuration -> challenge organisms (organism id) -> organism master. [src: MediaMasterDataService.cs:L120-L125]
- Material batch (stock) -> media product (`Material.MediaProductId`); required before a batch can be used as a Test Master step medium. [src: TestWorkflowStepMasterDataService.cs:L276-L277]

### MOD-EQUIP-002 Equipment configuration
- Equipment master (id, code, type, section) -> inventory asset (matched by code, case-insensitive; only for the summary and the API link). [src: EquipmentConfigurationService.cs:L117, L140-L168]
- Equipment (incubator) -> set point history rows. [src: EquipmentConfigurationService.cs:L190-L226]
- Equipment (autoclave) -> autoclave programs (equipment id) -> program history rows. [src: EquipmentConfigurationService.cs:L257-L303, L398-L430]
- Incubators and autoclaves are referenced by the test workflow when a step is started (the incubator choice is made in C2 / C5). [src: C2 WF-COUNT-001 (cluster file)]

### MOD-TESTMASTER-001 Test Master (Microbiology)
- Test definition -> section (laboratory) -> test orders (copied test code string, not a foreign key). [src: TestDefinitionMasterDataService.cs:L313; EnvironmentalMonitoringMasterDataService.cs:L171-L172 (same pattern)]
- Test definition -> workflow steps -> step media (material -> media product -> incubation condition). [src: TestWorkflowStepMasterDataService.cs:L256-L299]
- Step -> test orders that used it, matched by step name and test code (blocks delete). [src: TestWorkflowStepMasterDataService.cs:L233-L236]
- Organism master -> step target organism. [src: TestWorkflowStepConfiguration.cs:L24-L27]
- The test code is chosen in the EM room test, water location (assigned tests), After Cleaning part test and Items pickers (C7). [src: TestCodePicker.tsx; hooks/useTestDefinitions.ts:L69-L97]

## C5 Media, strains, stock

<!-- from C5-media-strains-stock.md -->

- Lot (Media) -> Material (dehydrated media stock: batch, manufacturer, media product) [src: MediaPreparationService.cs:L81].
- Lot -> autoclave Equipment (AutoclaveEquipmentId, program, load, temperature, cycle, pH) [src: MediaPreparationService.cs:L87-L93].
- Lot -> MediaEvaluation -> MediaEvaluationChallenge -> organism, source (Cryovial or Material disk), Incubation (incubator equipment, start/complete), ReferenceMedia lot [src: MediaEvaluationService.cs:L33-L42; MediaEvaluationEngine.cs:L152-L168].
- Lot -> MediaStorageEntry -> Equipment or location text [src: MediaStorageService.cs:L108-L109].
- Test step -> media lot chosen per incubation (test traceability in C2) [src: IncubationStepRecorder.cs:L56-L70].
- Lot -> MediaConfiguration (per product) for incubation condition and challenges [src: MediaEvaluationEngine.cs:L294-L305].

### MOD-CRYO-001 Cryovials
- Cryovial batch -> Material (lyophilized microorganism stock, batch, manufacturer) and Organism [src: CryovialService.cs:L90-L96].
- Cryovial batch -> identity confirmation rows -> Media lot and incubator equipment id, incubation start and end, observation text [src: CryovialService.cs:L107-L114].
- Cryovial batch -> storage equipment (StorageEquipmentId) or location text [src: CryovialService.cs:L258-L260].
- Cryovial batch -> thaw events [src: CryovialService.cs:L198].
- Cryovial batch -> media evaluation challenge (CryovialId on challenge) [src: MediaEvaluationEngine.cs:L78].

### MOD-MATERIAL-001 Materials Stock
- Material -> media product (MediaProductId) -> media lots prepared from it [src: MediaPreparationService.cs:L61-L66, L81].
- Material -> organism (OrganismId) for lyophilized microorganisms [src: CryovialService.cs:L65].
- Material -> master entry (MaterialMasterEntryId) and section [src: MaterialService.cs:L101-L104].
- Material -> documents (COA, supplier certificate, specification, SDS, other) [src: MaterialDocumentService.cs:L72-L76].

### MOD-EQUIP-001 Equipment Inventory
- Equipment inventory asset -> status history, calibration certificate documents, activities (where-is-it) [src: EquipmentInventoryController.cs:L60-L72, L91; EquipmentDocumentController.cs:L31].
- Equipment inventory asset -> media storage entries and cryovial storage location [src: MediaStorageService.cs:L76-L77; CryovialService.cs:L248-L251].
- Equipment inventory asset -> master configuration equipment (matched by code, for active activities) [src: EquipmentInventoryService.cs:L290-L291].

## C6 Physicochemical

<!-- from C6-physchem.md -->

Links the UI and data expose (verified in code unless marked):
- Run -> instrument (Equipment), method (HplcMethod plus JSON snapshot), column (ChromatographyColumn), mobile phases (SolutionPreparation per channel), SST record (1:1), samples (test orders), evidence files, run history. [src: backend/MicroLIMS.Domain/Entities/HplcRun.cs:L19; HplcRunService.cs:L264-L380]
- SST analyte row -> standard lot (Material) with purity and moisture snapshot; SST deduction recorded on the lot. [HplcRunService.cs:L416-L430, L553-L583]
- Run sample -> test order -> test analysis (sample result) and the sample report. [HplcRunService.Samples.cs:L340-L349]
- Solution preparation -> recipe snapshot (SolutionMaster), components -> consumed lots (Material), code, expiry, status history; a "Consumed by Preparations" list exists in the material lot details dialog (verified in UI code, shown in C5; INTENDED in hplc-chain-design.md:L76) [src: frontend/src/modules/inventory/materials/components/LotPreparationsList.tsx:L63; MaterialLotDetailsDialog.tsx:L193]. [SolutionPreparation entity files; hplc-chain-design.md sec. 4]
- Titrant preparation -> standardization records -> replicates -> standard lots and reference preparations. [TitrantStandardizationService.cs:L200-L268; TitrantStandardization.cs]
- Titration result -> titrant preparation code, factor used, standardization id, excess preparation, standard lots and their deductions (snapshot in ConditionsJson). [TitrationRecorder.cs:L165-L194; amendments A6, titration-test-design.md sec. 8]
- Working standard qualification -> source sample or manual source, master entry, HPLC run sample (assay), documents, and the WS lot created on approval. [WorkingStandardService.SignOff.cs:L98-L122; WorkingStandardQualification.cs]
- ICP run -> calibration (lots, elements, CCV readings), samples, evidence. [IcpRunService.cs:L232; IcpRunService.Calibration.cs]
- Method -> analytes -> specifications and working standard qualifications (block analyte removal). [HplcMethodService.cs; IcpMethodService.cs]
- Material master entry -> stock lots (blocked from category change). [MaterialMasterService.cs:L109]

## C7 General masters

<!-- from C7-masters.md -->

### MOD-ITEM-001 Items
- Item to Sample Traceability: `Sample.ItemId` links each received sample to its parent `Item` (Restrict foreign key). [src: backend/MicroLIMS.Application/Services/ItemService.cs:L113-L126].
- Item to Test Assignment: `Item.AssignedTests` links `Item` to `TestDefinition` by `TestCode`. On sample receipt, `ProductWorkflowEngine.ReceiveAsync` instantiates tests for the sample matching the item's configured tests. [src: frontend/src/modules/laboratoryConfiguration/items/components/AddItemDialog.tsx:L148-L151; backend/MicroLIMS.Application/Services/ItemService.cs:L48, L96-L99].
- Item to Specifications: `Item.Specifications` links `Item` to one or more `Specification` records. Each specification points to `TestDefinition` by `TestCode`, and optionally links to `HplcMethodAnalyte` or `IcpMethodElement`. [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L20-L25, L293-L372].
- Item to Preparation Configuration: `Item` links 1:1 to `ItemPreparationConfiguration` (`ItemId`), defining preparation parameters referenced during sample testing. [src: backend/MicroLIMS.Application/Services/ItemPreparationConfigurationService.cs:L45-L50].
- Item to test order at receipt (verified): the test order created for each assigned test stores only the test code and the owning section; the applicable specification is looked up at result time by item, test code and the sample's stage role (`SpecificationLookup`), it is not copied onto the test order [src: backend/MicroLIMS.Application/Workflows/ProductWorkflowEngine.cs:L103-L112; backend/MicroLIMS.Application/Services/SpecificationLookup.cs:L48-L67].
- Item to Controlled Documents: `Item` links 1:N to `ItemDocument` (`ItemId`), maintaining version revisions and access logs. [src: backend/MicroLIMS.Application/Services/ItemDocumentService.cs:L28-L34].

### MOD-WORKBOOK-001 Master Data Workbook
- Workbook Ingestion Traceability: Each `MasterDataImport` row links 1:N to `MasterDataImportRecord` entries storing `EntityType`, `EntityId`, `Sheet`, and `RowNumber`, maintaining direct bidirectional traceability between database entities and the exact source workbook row that created them. [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L168-L172].

### MOD-TESTMASTER-001 Test Master (FP and RM & PM)
- Test Definition to Methods: `TestDefinition.HplcMethodId` links to `HplcMethod`; `TestDefinition.IcpMethodId` links to `IcpMethod`. [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L249-L254, L285-L290].
- Test Definition to Stage Replicates: `TestDefinition` links 1:N to `TestDefinitionStageReplicate` (`TestDefinitionId`, `Role`), resolved during sample testing via `StageReplicateResolver`. [src: backend/MicroLIMS.Application/Services/MasterData/TestStageReplicateMasterDataService.cs:L31-L37; backend/MicroLIMS.Application/Services/StageReplicateResolver.cs:L86-L101].

## C8 Reports, KPI, Document Control

<!-- from C8-reports-dc.md -->

### MOD-REPORTS-001 Reports
- ResultRecord links: Each `ResultRecord` links back to `SampleId` (Sample), `TestOrderId` (TestOrder), `SourceTable` and `SourceId` (e.g. `CountTestReading`, `WorkflowStepResult`, `SampleLocation`, `ParameterResult`), `ResultEnteredByUserId`, and `ApprovedByUserId`. [src: backend/MicroLIMS.Domain/Entities/ResultRecord.cs:L10-L40].
- Media / GPT traceability: Each `MediaEvaluation` links to `Media` (autoclave equipment, cycle time, temperature, load type, prepared by, approved by) and contains child `MediaEvaluationChallenge` rows linking to `Organism` (scientific name, ATCC number), `Cryovial` (working culture lot), `LyophilizedDisk`, and `ReferenceMedia` lot [src: backend/MicroLIMS.Application/Services/MediaGptReportService.cs:L85-L156].
- Reference Strain traceability: Each `Cryovial` links to parent `Material` (receiving date, manufacturer, source batch), `Organism`, child `IdentityConfirmation` records (media, incubator equipment, observation), child `ThawHistory` events, direct `MediaEvaluationChallenge` usages, and indirect sample test orders qualified by the strain [src: backend/MicroLIMS.Application/Services/ReferenceStrainReportService.cs:L88-L125].
- Click-to-source drill-down: Clicking any point on the trend chart or search table opens `RecordDetailDialog`, displaying full read-only traceability for the sample, test order, reading, and approval sign-off [src: frontend/src/modules/reports/components/TrendingTab.tsx:L104-L121, L824-L828; frontend/src/modules/reports/components/ReportResultsTable.tsx:L258-L268, L491-L495].

### MOD-REPORTS-002 Reporting backfill
- Maintains traceability between source test readings / locations / observations and the reporting projection, preserving historical timestamps (`ResultEnteredAt`, `ApprovedAt`) and user associations [src: backend/MicroLIMS.Application/Services/ResultProjectionService.cs:L376-L380, L399-L403].

### MOD-KPI-001 KPI and Workflow Profiles
- KPI records drawer drill-down: Every headline tile and supporting section allows clicking to open `KpiRecordsDrawer`, which queries `GET /api/kpi/{key}/records` and displays the exact sample IDs, test order IDs, control numbers, test codes, categories, assigned analysts, timestamps, and inclusion rationale (Numerator, Denominator, Excluded) [src: backend/MicroLIMS.Application/Services/Kpi/KpiTypes.cs:L31-L33; frontend/src/modules/kpi/components/KpiPerformanceTab.tsx:L64-L70, L125].
- Workload analyst test drill-down: Clicking an analyst's name in `WorkloadTable` opens the tests drawer, querying `GET /api/kpi/workload/tests` to display all assigned test orders contributing to their capacity and delivered figures [src: frontend/src/modules/kpi/components/KpiPerformanceTab.tsx:L72-L78; frontend/src/modules/kpi/components/WorkloadTable.tsx:L147-L155].
- Profile version linking: Each `WorkflowProfileVersion` links to `TestDefinition` (via `TestCode`), `ElectronicSignature` (`ElectronicSignatureId`), and `CreatedByUserId` [src: backend/MicroLIMS.Domain/Entities/WorkflowProfileVersion.cs:L10-L33].

### MOD-KPI-002 Analyst availability and KPI section settings
- `KpiSetting` links to `DocumentSection` (`SectionId`) and `UpdatedByUserId` [src: backend/MicroLIMS.Domain/Entities/KpiSetting.cs:L1-L22].
- `AnalystAvailability` links to `User` (`UserId`), `EnteredByUserId`, and records audit field changes [src: backend/MicroLIMS.Domain/Entities/AnalystAvailability.cs:L1-L17].

### MOD-DC-001 Document Control: library and document lifecycle
- Document Master links to `DocumentType`, `DocumentDepartment`, `DocumentSection`, `DocumentOwnerUser` (`DocumentOwnerUserId`), `CurrentEffectiveRevision` (`CurrentEffectiveRevisionId`), child `DocumentRevision` collections, `DocumentKeyword` tags, and `DocumentMasterAssignment` personnel roles [src: backend/MicroLIMS.Domain/Entities/DocumentMaster.cs:L1-L40].
- Document Revision links to `DocumentMaster` (`DocumentMasterId`), `OriginatingPeriodicReviewTask` (`OriginatingPeriodicReviewTaskId`), `CreatedByUser`, `CancelledByUser`, collections of `RevisionFile` (source Word and controlled PDF), `RevisionChangeItem`, `RevisionImpactAssessment`, `DocumentReviewTask`, and `DocumentApprovalTask` [src: backend/MicroLIMS.Domain/Entities/DocumentRevision.cs:L1-L45].
- Review Finding links to `DocumentReviewTask` (`DocumentReviewTaskId`), `CreatedByUser`, `ReviewerVerifiedByUser`, `ResolvedByUser`, and transfers into `RevisionChangeItem` via `OriginatingReviewFindingId` across review cycles [src: backend/MicroLIMS.Domain/Entities/DocumentReviewFinding.cs; backend/MicroLIMS.Domain/Entities/RevisionChangeItem.cs].
- Document Approval Task links to `DocumentRevision` (`DocumentRevisionId`), `AssignedApproverUser` (`AssignedApproverUserId`), `AssignedByUser`, and `ElectronicSignature` (`MeaningOfSignature = Approved`) [src: backend/MicroLIMS.Domain/Entities/DocumentApprovalTask.cs; backend/MicroLIMS.Domain/Entities/ElectronicSignature.cs].
- Periodic Review Task links to `DocumentMaster` (`DocumentMasterId`), `DocumentRevision` (`DocumentRevisionId`), `AssignedReviewerUser`, `CompletedByUser`, and collections of `PeriodicReviewFinding` [src: backend/MicroLIMS.Domain/Entities/PeriodicReviewTask.cs].

### MOD-DC-002 Document Control: reading and training
- Training Assignment links to `DocumentMaster` (`DocumentMasterId`), `DocumentRevision` (`DocumentRevisionId`), `AssignedUser` (`AssignedUserId`), `SourceAssignmentId` (linking back to completed assignment on earlier revision for retraining continuity), and child `DocumentAcknowledgementRecord` [src: backend/MicroLIMS.Domain/Entities/DocumentTrainingAssignment.cs:L1-L35].
- Document Acknowledgement Record links to `DocumentTrainingAssignment` (`DocumentTrainingAssignmentId`), `DocumentMaster` (`DocumentMasterId`), `DocumentRevision` (`DocumentRevisionId`), `AcknowledgedByUser` (`AcknowledgedByUserId`), and `ControlledFile` (`ControlledFileId`, `ControlledFileHash`), freezing the exact `StatementText`, UTC timestamp, client IP address, and user agent [src: backend/MicroLIMS.Domain/Entities/DocumentAcknowledgementRecord.cs:L1-L30].
- Training Curricula links `Role` and optional `Department` via `DocumentRoleCurriculum` to `DocumentMaster` through child `DocumentRoleCurriculumItem` rows [src: backend/MicroLIMS.Domain/Entities/DocumentRoleCurriculum.cs].

### MOD-DC-003 Document Control: audit trail
- AuditLog links to `DocumentMaster` (`DocumentMasterId`), `DocumentRevision` (`DocumentRevisionId`), `User` (`UserId`), and child `AuditFieldChange` collections [src: backend/MicroLIMS.Domain/Entities/AuditLog.cs:L1-L30].

### MOD-DC-004 Document Control: configuration
- DocumentSection links to parent `DocumentDepartment` (`DepartmentId`) and `DocumentMaster` rows [src: backend/MicroLIMS.Domain/Entities/DocumentSection.cs].

### MOD-DC-005 Document escalations (backend)
- Document Escalation Record links to `DocumentTrainingAssignment` (`DocumentTrainingAssignmentId`), `ResolvedByUser` (`ResolvedByUserId`), and stores `EscalationLevel`, scheduled trigger timestamp, and recipient routing [src: backend/MicroLIMS.Domain/Entities/DocumentEscalationRecord.cs:L1-L30].
