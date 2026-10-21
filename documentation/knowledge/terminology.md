# Terminology

## Standard laboratory terms

General definitions, not taken from the code. Where the application uses a term in a narrower way, the module sections below say so.

| Term | Meaning |
|---|---|
| GPT | Growth Promotion Testing: shows a culture medium supports growth of specified organisms. |
| EM | Environmental Monitoring: microbial sampling of air, surfaces and personnel in controlled areas. |
| AC | After Cleaning: swab or contact sampling of equipment/areas after cleaning. |
| TAMC | Total Aerobic Microbial Count. |
| TYMC | Total Yeast and Mould Count. |
| CFU | Colony-Forming Unit: the counting unit for plate counts. |
| TNTC | Too Numerous To Count: plate with too many colonies to count reliably. |
| TSB | Tryptic Soy Broth: enrichment broth used, among others, in pathogen testing. |
| OOS | Out of Specification: a result outside its specification limit. |
| COA / CoA | Certificate of Analysis. |
| CDS | Chromatography Data System (instrument software, e.g. for HPLC). |
| HPLC | High-Performance Liquid Chromatography. |
| ICP-OES | Inductively Coupled Plasma Optical Emission Spectrometry. |
| ALCOA+ | Data-integrity principles: Attributable, Legible, Contemporaneous, Original, Accurate, plus Complete, Consistent, Enduring, Available. Listed as a term only; no compliance claim is made. |
| GxP | Umbrella term for "good practice" regulations (GMP, GLP, GDP …). Listed as a term only; no compliance claim is made. |

## C1 Platform & administration

<!-- from C1-platform.md -->

### MOD-AUTH-001 Authentication, session, password
- "Sign In" / "Forgot password?" / "Admin-Assisted Password Recovery": sign-in card actions [src: frontend/src/pages/Login.tsx:L171-L186].
- "One-Time Recovery Code": 12-character code an administrator generates for one user; valid 15 minutes [src: frontend/src/pages/AdminPasswordRecovery.tsx:L90-L97; frontend/src/modules/users/UsersPage.tsx:L692].
- "Must Change Password": account flag that forces the change page at next use [src: frontend/src/modules/users/UsersPage.tsx:L413].
- "Stay signed in": button on the idle-timeout warning [src: frontend/src/layouts/MainLayout.tsx:L164].

### MOD-USERS-001 Users and laboratory memberships
- "Role" (account): the named role assigned to a user [src: frontend/src/modules/users/UsersPage.tsx:L356-L359].
- "Role type" / "Base Type": one of four built-in types that a role is built on (SystemAdministrator, SectionHead, Reviewer, Analyst). Dashboard variant, scope bypass and some checks follow the type, not the custom name [src: backend/MicroLIMS.Domain/Enums/RoleType.cs:L3; frontend/src/modules/roles/CreateRolePage.tsx:L122-L129].
- "All sections (entire department)": membership option that covers all active sections of a department [src: frontend/src/modules/users/dialogs/UserSectionsDialog.tsx:L253].
- "Area": physchem membership sub-area, chosen as "FP" (finished product), "RM & PM" (raw and packaging material) or "Both" (stored as none); the backend returns `fp` / `rmpm` for the sidebar [src: frontend/src/modules/users/dialogs/UserSectionsDialog.tsx:L143-L157, L331-L339; backend/MicroLIMS.Application/Services/LaboratoryOrganizationService.cs:L14-L15, L47].
- "Reason for Change" / "Reason for Disabling" / "Reason for Unlock (Optional)" / "Reason for Reset (Optional)" / "Reason for Recovery (Mandatory)": administrative justification stored in the audit row [src: frontend/src/modules/users/UsersPage.tsx:L529, L553, L575, L599, L638].
- "Edit Profile" / "Laboratory Sections" / "Unlock Account" / "Force Password Change at Next Login" / "Permanently Delete User": row actions [src: UsersPage.tsx:L426-L482].
- "Account Locked": lock in effect; "Disabled": account cannot sign in [src: UsersPage.tsx:L405-L412].

### MOD-ROLES-001 Roles and permissions
- "Enforced": the endpoints covered by the code check it directly. "Legacy-only": access still follows the base role type [src: frontend/src/modules/roles/components/PermissionMatrix.tsx:L69-L83].
- "System Role" / "Custom": built-in four roles vs roles created by administrators; custom roles can be deleted when unassigned [src: frontend/src/modules/roles/RoleDetailPage.tsx:L139].
- "Base Type": role type chosen at creation; not changed by later edits [src: backend/MicroLIMS.Application/Services/RoleService.cs:L63-L89].
- "Clone permissions from any existing role": wizard option that copies a role's ticked codes into the new role [src: frontend/src/modules/roles/CreateRolePage.tsx:L144-L154].

### MOD-SIG-001 Electronic signatures
- "Electronic Signature": dialog title; the signer re-types the login password [src: frontend/src/components/SignatureDialog.tsx:L52].
- "Comment (optional)": optional free-text on the signature [src: SignatureDialog.tsx:L71].
- "Signing...": in-progress label on the Sign button [SignatureDialog.tsx:L57].
- SoD = segregation of duties: a person who performed a step may not perform the next decision on the same sample or record [src: backend/MicroLIMS.Application/Services/SegregationOfDutiesGuard.cs:L6-L10].

### MOD-DASH-001 Dashboards
- "Command Center": dashboard heading for the three role-type variants [src: AdminDashboardPage.tsx:L84; ReviewerDashboardPage.tsx:L84; SectionHeadDashboardPage.tsx:L106].
- "Overdue (>24h)", "Overdue review" (caption "in review >24 hours"), "Overdue" (Section Head): three different definitions; see BR-DASH-002, BR-DASH-006, BR-DASH-008 and GAP-102.
- "Due today" (Reviewer): caption "submitted today" [ReviewerDashboardPage.tsx:L129-L135].
- "Ready to read": incubation finished and read window reached [SectionHeadDashboardPage.tsx:L132-L144].
- "On-Time Reading Rate": share of incubations read within 4 h of expected reading time (Microbiology) [backend/MicroLIMS.Application/Services/DashboardService.cs:L386-L396].
- "Attention items" / "Attention Required": summary of overdue and retest items [DashboardService.cs:L683; AttentionRequiredPanel.tsx:L67].

### MOD-NOTIF-001 Notifications
- "Notifications" with "N unread": bell label [src: frontend/src/components/Header.tsx:L216].
- "Expiring" chip: media expiry item on the analyst attention panel [src: frontend/src/modules/dashboard/components/AttentionRequiredPanel.tsx:L107].

### MOD-MSG-001 Messages
- "Direct Message (1-on-1)" / "Group Conversation": conversation kinds [src: frontend/src/modules/messages/NewConversationDialog.tsx:L140, L150].
- "Group Title": required for group conversations in the UI [NewConversationDialog.tsx:L159-L162].
- "Pinned at top" = "Important" discussion flag [src: frontend/src/modules/discussions/components/NewDiscussionDialog.tsx:L142].

### MOD-DISC-001 Discussions
- "v{n} Edited": post has been edited n times; click for history [src: frontend/src/modules/discussions/components/DiscussionCard.tsx:L146-L149].
- Categories (11): Water, Equipment, Environmental Monitoring (EM), Products, Media / Materials, Internal Decisions, Management Requirements, EDA Requirements, ISO 17025, GMP / Regulatory, Other [src: frontend/src/modules/discussions/types/discussionTypes.ts:L20-L31].

### MOD-ERR-001 Error Monitoring
- "Incident": a grouped technical error (summary + correlation id + occurrences); not part of the GxP record [src: frontend/src/modules/errorMonitoring/ErrorMonitoringPage.tsx:L60-L61; backend/MicroLIMS.Application/Services/ErrorMonitoringService.cs:L28-L65].
- "Correlation id": identifier for related error entries, shown in search [src: IncidentFilterBar.tsx:L71].
- "Override" / "Auto (…)": severity set by a person vs computed severity [src: IncidentRowDetail.tsx:L155-L167].

### MOD-AUDIT-001 Audit Trail
- "Audit Events (N)": result heading [src: AuditSearchPage.tsx:L108-L114].
- "Changed Fields (Diff)": column of the changed field names [src: AuditResultsTable.tsx:L69-L75].
- "Inspect Event & Traceability Chain": row action opening the drawer [AuditResultsTable.tsx:L190-L205].
- "Traceability chain": chain of linked records inside the drawer [src: AuditTraceabilityChain.tsx:L24-L128].
- "Record Ref", "Entity Category": result column and filter [AuditResultsTable.tsx:L69-L75; AuditFilterBar.tsx:L108-L120].
- "Laboratory identifiers": Sample Reference, Batch Number, Control Number, Media Lot Number, Cryovial / RS Code, User ID (advanced filters) [AuditFilterBar.tsx:L145-L186].

### MOD-ARCHIVE-001 Archived records
- "Archived copies": heading of frozen PDFs on a sample report [src: frontend/src/modules/testingWorkspace/reportPrimitives.tsx:L178-L184].
- "Immutable PDF": description used by the UI for archived copies [reportPrimitives.tsx:L183-L185].

## C2 Sample flow & micro testing

<!-- from C2-sample-testing.md -->

| Term (UI) | Plain meaning (as coded) | src |
|---|---|---|
| Sample / Reference | Received unit; reference number shown as ref | SampleRegisterTable |
| Test Order (TO #) | One order per test code per sample | ProductWorkflowEngine.cs:124 |
| Control No. / Batch No. | Batch or control identifiers (Product/RM/PM require both at receipt) | ReceiveSampleValidator.cs:15-16 |
| Cause of Testing | Reason the sample was submitted (master list) | ReceiveSampleValidator.cs:14 |
| Sampled By | Person who sampled | ReceiveSampleValidator.cs:17 |
| Assigned analyst / Unassigned | Analyst assigned to all open test orders; "Unassigned" = open for any authorised analyst | SampleAssignmentService.cs:28-70; AssignAnalystDialog.tsx:191-199 |
| Preparation / Needs Preparation | Diluent/neutraliser (product) or room/part/point grid (EM/AC/Water) confirmed before testing | SamplePreparationService.cs |
| Shared TSB | One broth tube per sample applied to all pathogen tests that use TSB | IncubationStepRecorder.cs (shared TSB); PathogenSessionService.cs:557-591 |
| Incubation window | Minimum (Ready from) and maximum (Read by) hours and temperature range from the step medium | IncubationWindowResolver.cs:12-15 |
| Skip Wait | Supervisor (TestWorkflow.Supervise) bypass of the minimum-duration and window-end checks for the open incubation; no reason or password; window unchanged | IncubationStepRecorder.cs:431-443 |
| Stage 1 / Stage 2 | Two-stage count: plates moved to a second incubator after stage 1 window | IncubationStepRecorder.cs:163-200 |
| Dilution Factor (DF) | Multiplier applied to average plate count; direct-count types fixed at 1 | IncubationStepRecorder.cs:780-783, 877-878 |
| Direct count | Water, EM, After Cleaning (DF = 1) | IncubationStepRecorder.cs:780 |
| Plate reading | Colony count entered per plate; or "TNTC" / "Uncountable" | TestWorkflowDialog.tsx:742-768 |
| TNTC | "Too numerous to count" text entry; flags RequiresReview, no average | IncubationStepRecorder.cs:828-852 |
| Uncountable | Same as TNTC | IncubationStepRecorder.cs:844 |
| Calculated result | Average of readings x DF | IncubationStepRecorder.cs:877-878 |
| Reported result | Display value (rounded whole number, or "<DF unit") | IncubationStepRecorder.cs:906-909 |
| Alert / Action / Spec | Limits compared in order Spec, Action, Alert | SpecLimitParser.cs:208-226 |
| Growth Conforming (+) | Growth matching expected appearance for the target organism | GrowthObservation.cs; SelectivePlatingPanel.tsx:16-20 |
| Growth Non-Conforming | Growth not matching expected appearance (ends selective-plating test as NotDetected) | PathogenConfirmationRecorder.cs:370-383 |
| Confirmatory plating | Re-plating on one or more permitted confirmatory media, one observation per medium | PathogenConfirmationRecorder.cs:434-600 |
| Inconclusive (Retest) | Confirmatory media disagree; flagged for investigation/retest | PathogenConfirmationRecorder.cs:581-599 |
| Biochemical test | Identification step; result text + organism detected yes/no | PathogenConfirmationRecorder.cs:676-760 |
| Primary Matrix (Panel A) / Panel B | Location x Test grid of primary plate readings / confirmatory batch | PrimaryObservationMatrixPanel.tsx; BatchConfirmatoryPlatingPanel.tsx |
| Flagged location | Location with a non-"No Growth" primary observation needing confirmation | BatchConfirmatoryPlatingPanel.tsx:455-471 |
| Grouped actions | Actions applied to 2+ checked samples at once | GroupedActionsPanel.tsx:35-363 |
| Void | Strike a sample from the register (signed); not the same as reject | SampleCorrectionService.cs:253-303 |
| Correct / Edit details | Signed correction of sample fields until submitted for review | SampleCorrectionService.cs:62-67 |

---

## C3 Review, approval, reports out

<!-- from C3-review-approval.md -->

- Technical review: a reviewer's signed statement per laboratory section that the test data is complete [src: frontend/src/modules/testingWorkspace/sampleSummary/ApprovalSignaturesCard.tsx:235-283]. Recorded as meaning Reviewed (BR-REVIEW-005).
- Release decision: a Section Head's signed decision per section; four options (see Statuses) [src: ApprovalSignaturesCard.tsx:287-475].
- Submit Technical Review / Submit Release Decision: the two signed forms.
- Comment (optional): internal comment on a review or decision. It never prints on the certificate [src: ApprovalSignaturesCard.tsx:263-272, 309-318].
- Certificate Remarks: text printed on the CoA, shown only for Approve. Blank prints "No remarks." [src: ApprovalSignaturesCard.tsx:333-345; SampleCoaPage.tsx:529-534].
- Return to Analyst: sends a count test back for re-entry (analysis tests in the backend, see GAP-300). Unsigned [src: SampleReviewService.cs:L157-L269].
- Tests to Retest: checklist of tests that move to a new sample; non-conforming tests pre-checked [src: ApprovalSignaturesCard.tsx:347-408].
- Retest Retained Sample: one new sample with the selected tests, same analyst assignments [src: SampleApprovalService.cs:L377-L438].
- New Sample Required: two new samples with the selected tests, one per chosen analyst [src: SampleApprovalService.cs:L440-L503].
- Not Conform (Final Conclusion): the Reject decision label, which closes the sample for this section [src: summaryFormatting.ts:73-78].
- Close Testing: a section stops its own open tests after another section rejected the sample. Open tests become Cancelled [src: SectionClosureService.cs:L74-L88].
- OOS group: all samples sharing one OosGroupCode (origin plus retests) [src: OosTrackingService.cs:L11-L24].
- Origin sample and retest sample: a retest sample points back to its origin through OriginSampleId [src: SampleApprovalService.cs:L79].
- Lab Investigation Report: investigation document attached to an OOS group, status Current, Superseded or Voided [src: OosTrackingPage.tsx:223-233, 640-646].
- Printable Report: the sample summary report page. Not a certificate [src: SCR-COA-001 above].
- Certificate of Analysis (CoA): the certificate page for approved or rejected samples; combined or per-section variant [src: SCR-COA-002 above].
- Archived copy: frozen PDF saved at a final decision, with SHA-256 hash [src: RecordArchiveService.cs:L12-L20].
- Electronic signature statements: the wording differs by screen (corrected; the draft said one map was copied everywhere). Reviewed is the same everywhere: "I have reviewed the test data and confirm it is complete and accurate." Approved: CoA "I approve the release of this sample for its intended use."; report page "I approve the release of this sample for its intended use."; Approval card and reportPrimitives "I approve the release of this record for its intended use." Rejected: CoA "I reject the release of this sample; it does not conform to specification."; report page "I reject this sample; the results do not conform to specification."; Approval card and reportPrimitives "I reject this record; it does not conform to specification." RetestRequested and InvestigationOrdered have statements on the report page and the card ("I am ordering a retest of the retained sample."; "I am ordering an investigation into these results."), not on the CoA. The CoA "Closed" statement "I am closing this laboratory's testing; it will not be resumed on this sample." is used only for closed section blocks; the signature trail on the card and the report page have no statement for TestingClosed (the card prints the raw value, the report page the humanized value) [src: SampleCoaPage.tsx:54-60; SampleReportPage.tsx:42-48; sampleSummary/summaryFormatting.ts:76-83; reportPrimitives.tsx:44-51]. (verified) See GAP-315.
- Signature meaning shown in the signature dialog at decision time is the radio label (for example "Approve: Results conform to specifications"), not the statements above [src: SampleSummaryDialog.tsx:623-630].

## C4 Monitoring & micro configuration

<!-- from C4-monitoring-microconfig.md -->

### MOD-EM-001 Environmental Monitoring configuration
- **Alert** - first limit; a result above it (and not above action or specification) is reported as Alert Limit Exceeded. [src: backend/MicroLIMS.Application/Services/SpecLimitParser.cs:L219-L221]
- **Action** - second limit; a result above it (and not above the specification) is Action Limit Exceeded. [src: SpecLimitParser.cs:L215-L217]
- **Specification** (UI label "Specification"; "Spec" in code) - highest limit; a result above it is Out of Specification. [src: SpecLimitParser.cs:L211-L213; frontend/src/components/configHierarchy/LimitFields.tsx:L42]
- **Grade A to D** - room grade classification stored per room (UI options "Grade A" to "Grade D"). [src: environmentalMonitoring/emConfigTypes.ts:L44]
- **Class** - free-text classification of an EM department (placeholder "e.g. Grade C"). [src: EmPanels.tsx:L45-L68]
- **Testing frequency** - choice of Daily, Weekly, Fortnightly, Monthly, Quarterly, Annually, stored as text; the code marks it "informational only" and no scheduling or reminder uses it. [src: frontend/src/components/configHierarchy/FrequencyField.tsx:L3; backend/MicroLIMS.Domain/Entities/Department.cs:L9] (verified)
- **Test type** (room) - what is sampled: Passive air sample, Active air sample, Surface sample, Compressed air, Drains. [src: emConfigTypes.ts:L33-L39]

### MOD-WATER-001 Water configuration
- **Water system** - UI name for the WaterDepartment entity. [src: water/components/WaterDepartmentPanel.tsx:L52]
- **Sample location / point** - UI name for a water sampling point (entity WaterSamplingPoint); "Point code" = Code, "Point name" = Location. [src: water/components/WaterLocationPanel.tsx:L124-L150]
- **Assigned tests** - the test codes ordered for a sample from this location; the server creates the test orders at preparation. [src: WaterLocationPanel.tsx:L160; WaterWorkflowEngine.cs:L125-L150]
- **Count test** - a test whose Test Master workflow type is Count Test; only these carry limits. **Presence / absence test** - a pathogen (Observation) test, no limits. [src: WaterLocationPanel.tsx:L163-L190]
- **Needs attention** - view filter listing locations with missing limits or no assigned tests. [src: WaterConfigPage.tsx:L190-L200]

### MOD-AC-001 After Cleaning configuration
- **Machine / machine part** - After Cleaning equipment and its parts (entities Machine, MachinePart). [src: afterCleaning/components/AcPanels.tsx:L52, L105]
- **Swab / Rinse** - After Cleaning count sample types, with unit hints "CFU/25 cm2" and "CFU/mL". [src: afterCleaning/acConfigTypes.ts:L30-L33]
- **Pathogen test** - an After Cleaning presence / absence configuration saved with sample type "Pathogen". [src: acConfigTypes.ts:L35]

### MOD-ORG-001 Organisms master
- **Scientific name / ATCC No. / Common name** - organism identifiers in the organism master (only the scientific name is required). [src: OrganismsPage.tsx:L35-L37, L104-L110]

### MOD-MEDIACFG-001 Media Configurations
- **Media product** - a dehydrated medium with a name and a unique short code. The code prefixes the lot numbers prepared from it (`{Code}/{seq:D2}/{yy}`). [src: MediaProductService.cs:L19-L31; ChangeMediaProductCodeDialog.tsx:L33]
- **Incubation condition** - hours (min and max) and temperature range (min and max, °C) for one product. [src: MediaIncubationConditionDialog.tsx:L120-L159]
- **Media configuration** - the evaluation setup for one product: evaluation type, incubation condition, optional recovery range and challenge organisms. One per product. [src: MediaMasterDataService.cs:L78-L82]
- **Growth Promotion** - evaluation type; the recovery percent range applies. [src: MediaConfigurationDialog.tsx:L36]
- **Indication / Inhibition** - evaluation type; each challenge organism has a role (Inhibition or Indication). [src: MediaConfigurationDialog.tsx:L37, L41]
- **Challenge organism** - an organism added to a configuration with an initial inoculum (CFU) and, for the Indication role, an expected colony description. [src: MediaConfigurationDialog.tsx:L351-L361]
- **Recovery %** - minimum and maximum recovery percentage limits for Growth Promotion. [src: MediaConfigurationDialog.tsx:L300-L309]
- **Stock batches** - number of material batches (stock entries) linked to the product. [src: MediaProductWorkspace.tsx:L230-L242; MediaProductService.cs:L55-L61]

### MOD-EQUIP-002 Equipment configuration
- **Set point** - target temperature of an incubator (°C); changes through "Edit Set Point" need a reason and are kept in a history. [src: EquipmentPage.tsx:L1310, L1377]
- **Autoclave program / load** - a sterilisation cycle on an autoclave: code, name, load type, temperature, cycle time. [src: EquipmentPage.tsx:L1434-L1462]
- **Cycle time (min)** - length of the autoclave program in minutes. [src: EquipmentPage.tsx:L1462]
- **Configured equipment** - equipment record in the laboratory configuration (as opposed to the inventory asset). [src: EquipmentPage.tsx:L599]
- **Master Inventory** - the equipment register of the inventory module (C5); "Select from Inventory" uses it to pre-fill the form. [src: EquipmentPage.tsx:L1549-L1600]

### MOD-TESTMASTER-001 Test Master (Microbiology)
- **Test code** - unique identifier of the test (for example TAMC, TYMC are named as count tests in the code comment). [src: TestDefinitionMasterDataService.cs:L40-L41; backend/MicroLIMS.Domain/Enums/WorkflowType.cs:L5]
- **Count Test** - plate-count test (CFU) with media and incubation; see C2. [src: testMasterConstants.ts:L14-L16]
- **Observation** - staged pathogen detection chain; each step's type drives the flow. [src: backend/MicroLIMS.Domain/Enums/WorkflowType.cs:L6]
- **Step type** - PlateCount, BrothEnrichment, SelectiveBroth, SelectivePlating, ConfirmatoryPlating, BiochemicalTest (shown with these names). [src: testMasterConstants.ts:L43]
- **Step medium / medium** - a dehydrated-media stock material, linked to a media product, assigned to a step with one of that product's incubation conditions and a Required flag. [src: WorkflowStepsSection.tsx:L765-L837]
- **Target organism** - the organism a selective plating or confirmatory plating step targets. [src: testMasterConstants.ts:L45-L46]
- **Phenotypic test** - labels "Gram Stain", "Catalase", "Oxidase", "Coagulase", "Antibiogram", "Identification Kit". [src: testMasterConstants.ts:L58-L65]
- **Final Step** - checkbox on a step; only one step per test may carry it. [src: WorkflowStepsSection.tsx:L654; TestWorkflowStepMasterDataService.cs:L315]
- **Requires incubation transfer / 2-Stage** - the plate count is moved to a second incubation stage with its own temperature and hours. [src: WorkflowStepsSection.tsx:L522, L557, L677, L699-L723]
- **Freeze / Frozen** - sets the test inactive; the test is not offered for new selections. [src: TestDefinitionMasterDataService.cs:L796-L803; TestCodePicker.tsx:L21-L30]
- **Needs configuration** - warning chip on a step missing a required organism or medium. [src: WorkflowStepsSection.tsx:L574]

## C5 Media, strains, stock

<!-- from C5-media-strains-stock.md -->

- Media lot: a prepared batch of a dehydrated medium; identified by LotNumber `{Product.Code}/{seq:D2}/{yy}` [src: PreparedLotNumber.cs:L45].
- Manufacturer lot: the batch number of the consumed dehydrated media Material [src: MediaPreparationService.cs:L83].
- Media product: the configured medium type (code and name) a lot belongs to [src: MediaPreparationService.cs:L65-L77].
- GPT: Growth Promotion Testing, the GrowthPromotion evaluation type [src: EvaluationType.cs:L5-L9].
- Indication / Inhibition: IndicationInhibition evaluation with Indication or Inhibition challenge roles [src: MediaEvaluationEngine.cs:L234-L241].
- Enrichment Characteristics: EnrichmentCharacteristics evaluation; Conform if turbid [src: MediaEvaluationEngine.cs:L248-L253].
- Challenge: one organism inoculated in an evaluation, with its source (cryovial or lyophilized disk), incubation and result.
- Recovery %: new count / old count x 100, rounded to 1 decimal [src: MediaEvaluationEngine.cs:L223].
- Conform / NonConform: evaluation result; Conform does not release a lot by itself.
- Release (Approved Media): signed Section Head decision that makes the lot usable [src: MediaReleaseService.cs:L11-L17].
- Out of Stock: manual status, lot no longer used for new storage or tests.
- Storage entry: a quantity of a lot at an equipment location or free-text location, with form.
- Sterility: appears in UI subtitle; no sterility record in code (GAP-022).

### MOD-CRYO-001 Cryovials
- Cryovial batch (Code): a set of frozen vials prepared from one lyophilized microorganism Material [src: CryovialService.cs:L90-L99].
- Identity confirmation: the panel of released media lots and incubators used to confirm the batch organism at preparation [src: CryovialService.cs:L107-L114; PrepareCryovialBatchDialog.tsx:L123-L126].
- Discs used: units consumed from Materials Stock to prepare the batch [src: CryovialService.cs:L85].
- Vials prepared / vials remaining: counts at preparation and after thawing [src: CryovialService.cs:L98-L99].
- Thaw: removing one vial for use; recorded as ThawEvent with notes [src: CryovialService.cs:L183-L199].
- Destroy: marks the batch destroyed [src: CryovialService.cs:L202-L208].
- Change Storage Location: move dialog title [src: MoveCryovialStorageDialog.tsx:L14].
- Reference strain / reference cryovial: UI title "Reference Cryovials" [application map SCR-CRYO-001].

### MOD-MATERIAL-001 Materials Stock
- Material (lot): one received stock entry with batch number, manufacturer, expiry (optional), location, quantity received and remaining [src: MaterialService.cs:L183-L192].
- Batch / Lot No. (BatchNumber) and Manufacturer lot: shown as "Batch/Lot No." [src: MaterialsPage.tsx:L271].
- Master entry: the catalog entry (reagent, reference standard) a lot is linked to [src: MaterialService.cs:L101-L104].
- Working standard: created only by an approved qualification, not by receipt [src: MaterialService.cs:L137].
- COA: Certificate of Analysis, required before use for dehydrated media, lyophilized microorganisms and supplements [src: MaterialService.cs:L366-L372].
- Approved Media List / Approved Cryovial List: the inventory lists of released media and approved, non-destroyed cryovials [src: frontend/src/modules/inventory/approvedLists/ApprovedCryovialListPage.tsx:L303-L304].

### MOD-EQUIP-001 Equipment Inventory
- Equipment register: the instrument register (tab "Equipment Register") [src: EquipmentInventoryPage.tsx:L249].
- Active Equipment: tab listing in-service assets with activities [src: EquipmentInventoryPage.tsx:L250].
- Calibration Due Date: the stored due date [src: RegisterEquipmentDialog.tsx:L271].
- Status Change Comment: comment required when operational status changes (helper "Required whenever the operational status changes.") [src: RegisterEquipmentDialog.tsx:L301, L310].
- Where-is-it: lookup of an asset's current location [src: frontend/src/modules/inventory/equipment/services/EquipmentInventoryService.ts:L66].

## C6 Physicochemical

<!-- from C6-physchem.md -->

- SST (system suitability test): checks on a standard before samples. In the workspace it is the "System Suitability" tab. [src: frontend/src/modules/hplcWorkspace/run/HplcInstrumentWorkspace.tsx:L247]
- Th.Wt (theoretical weight): method constant for the standard and the test, in mg. [src: hplc-chain-design.md D1]
- Standard dilution: method constant used for dissolution standard concentration. [src: 2026-10-02-retire-legacy-sst-design.md Stage 1]
- MC (moisture content) and P (purity): stored on the stock lot; used in assay and standard calculations. [src: hplc-chain-design.md D4]
- RSD: sample standard deviation (n-1) divided by mean, in %. [src: StandardComparisonCalculator.cs:L59-L80]
- r (correlation coefficient): per-element ICP calibration value typed from the Syngistix display. [src: icp-gc spec D3]
- CCV, ICV, blank: ICP checks. ICV is a second-source standard. [src: IcpCalibrationEvaluator.cs:L13-L48]
- LOQ: concentration below the lowest calibration level, shown as "<LOQ". [src: IcpSampleEvaluator.cs:L63]
- Q, S1, S2, S3 (dissolution): acceptance value and stage tests. [src: DissolutionStageEvaluator.cs:L13-L96]
- Stage 1 units (disintegration): first set of units, stage 2 extends the set. [src: DisintegrationStageEvaluator.cs:L21-L89]
- Capsule Step A, Tablet band: weight variation bands by mean weight. [src: WeightVariationEvaluator.cs:L134-L146]
- Excess titrant and back titration: residual titration mode. [src: TitrationEngine.cs:L104-L137]
- Relative (titration): calculation against a reference standard, with factor K. [src: TitrationEngine.cs:L139-L155]
- Uspfactor: titration calculation using the titrant factor and equivalency factor F. [src: TitrationEngine.cs:L115-L120]
- Amount per unit / mg per unit: result basis (assay % x label claim / 100 for Finished stage role). [src: HplcAssayCalculator.cs:L52]
- Individual vs Mean (reporting basis): Bulk role reports each replicate; others report the mean. [src: HplcAssayCalculator.cs:L54]
- VS, MP, DL: titrant, mobile phase, diluent solution prefixes in codes. [src: SolutionPreparationCode.cs:L8-L22]
- WS (working standard) and WSQ (qualification record): WS-nn/MM/yyyy lot code; WSQ-nn/MM/yyyy record code (spec). [src: working-standard spec sec. 4.1-4.2]
- Area FP and RM & PM (physicochemical areas): FP = finished product; RM & PM = raw and packaging material. [src: physchem-areas spec D8; backend/MicroLIMS.Domain/Enums/PhyschemArea.cs:L4-L8]
- Syngistix: the ICP-OES data system whose calibration report is uploaded. [src: IcpRunService.Calibration.cs:L11]

## C7 General masters

<!-- from C7-masters.md -->

### MOD-ITEM-001 Items
- Item: A manufactured product or raw/packaging material catalog entry tested by the laboratory. [src: backend/MicroLIMS.Application/Services/ItemService.cs:L18-L23].
- Assigned Tests: The battery of laboratory test definitions linked to an item that are automatically instantiated as sample test orders when a sample for that item is received. [src: frontend/src/modules/laboratoryConfiguration/items/components/AddItemDialog.tsx:L148-L151].
- Frozen Item: A deactivated catalog item whose sample receipt is blocked while preserving all past historical testing data and traceability. [src: backend/MicroLIMS.Application/Services/ItemService.cs:L101-L104].
- Controlled Document: An authoritative standard operating procedure (SOP) or verification report file uploaded to an item, governed by revision versioning and access logging. [src: backend/MicroLIMS.Application/Services/ItemDocumentService.cs:L17-L25].
- Preparation Protocol: Standardized sample preparation configuration (sample amount, technique, filtration/washing volume, diluent, neutralizer) used by analysts during microbiology testing. [src: backend/MicroLIMS.Application/Services/ItemPreparationConfigurationService.cs:L26-L29].

### MOD-SPEC-001 Specifications
- Canonical Limit: Standardized text representation of an acceptance limit (e.g. "90.0-110.0", "NMT 10", "Q = 75 %") automatically formatted and saved by the system. [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L36-L59].
- Count-Tiered Specification: Multi-threshold limit configuration allowing Alert, Action, and Specification limits with an optional dilution factor, used for microbial enumeration tests. [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L51, L84-L85].
- Stage-Specific Specification: Acceptance criteria applicable exclusively to samples received at a designated production stage role (Bulk, In-Process, Finished, Stability). [src: backend/MicroLIMS.Application/Services/SpecificationLookup.cs:L8-L14].
- Dilution Factor: A multiplier applied during count evaluation, permitted exclusively on Count-Tiered specifications. [src: backend/MicroLIMS.Application/Services/SpecificationService.cs:L84-L85].

### MOD-RECVCFG-001 Receiving Configuration
- Sampler: An individual qualified to draw samples; configured in a suggestion picklist on the sample receiving form. [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ReceivingConfigurationPage.tsx:L308-L310].
- Cause of Testing: Mandatory justification selected upon receiving a sample (the only example in the screen is "Investigation"; the list contents are site data). [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ReceivingConfigurationPage.tsx:L321-L323].
- Production Stage Role: Standardized functional classification of a manufacturing stage (`Bulk`, `InProcess`, `Finished`, `Stability`, `Other`). [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/ProductionStagesCard.tsx:L31-L37].

### MOD-WORKBOOK-001 Master Data Workbook
- Master Data Workbook: An Excel spreadsheet (`.xlsx`) structured into 12 predefined sheets used to bulk-load laboratory master data. [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataSheets.cs:L8-L33].
- Second-Person Verification: A dual-control compliance safeguard requiring an independent qualified user to review and sign off on imported data before it becomes active. [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L187-L190].
- Dry Run (Check File): Pre-load validation that executes all row insertions against a transactional savepoint and rolls back, generating a comprehensive error report without modifying database state. [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L99-L114].

### MOD-TESTMASTER-001 Test Master (FP and RM & PM)
- Test Definition: Canonical master record defining a test code, display name, owning section, workflow type, equation type, and operating parameters. [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L30-L34].
- Physicochemical Area: Operational sub-division of the physicochemical laboratory separating Finished Product testing (`fp`) from Raw and Packaging Material testing (`rmpm`). [src: frontend/src/modules/laboratoryConfiguration/masterDataSimple/testMasterArea.ts:L1-L30].
- System Suitability (SST): Instrumental checks (e.g. RSD, resolution, tailing factor, plate count) required before running chromatographic tests. [src: backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs:L59-L71].
- Stage Replicate: Minimum number of sample test determinations mandated for a sample based on its reconciled manufacturing stage role. [src: backend/MicroLIMS.Application/Services/MasterData/TestStageReplicateMasterDataService.cs:L48-L53].

## C8 Reports, KPI, Document Control

<!-- from C8-reports-dc.md -->

### MOD-REPORTS-001 Reports
- ResultRecord: Flattened read-model projection combining sample identity, test order, reading/observation, specification evaluation, and approval details into a single fast-query table. [src: backend/MicroLIMS.Domain/Entities/ResultRecord.cs:L1-L60].
- Imputation: Substituting half the detection limit (`DetectionLimit / 2`) for results reported as below detection limit (`<n`) when computing numerical averages and statistical trend lines. [src: backend/MicroLIMS.Application/Services/ReportingQueryService.cs:L259-L268, L508-L509].
- Quick Period: Pre-set date range selector above reports tabs ("30d", "90d", "1y", "custom"). [src: frontend/src/modules/reports/components/QuickPeriodSelector.tsx:L16-L25].
- Round: 1-based ordinal position of a test order among all orders for that same sample and test code, distinguishing original testing (Round 1) from retests (Round 2+). [src: backend/MicroLIMS.Application/Services/ResultProjectionService.cs:L64-L79].

### MOD-REPORTS-002 Reporting backfill
- Backfill: Administrative synchronization sweep that populates historical ResultRecord rows from legacy operational tables (`CountTestReading`, `WorkflowStepResult`, `SampleLocation`, `ParameterResult`). [src: backend/MicroLIMS.Application/Services/ResultProjectionService.cs:L409-L514].

### MOD-KPI-001 KPI and Workflow Profiles
- Work Unit: Standardized measure of analyst effort where 1 work unit = 1 standard analyst-hour of hands-on work. Used for capacity planning and right-first-time weighting, not performance scoring. [src: docs/superpowers/plans/2026-10-11-kpi-remodel-index.md:L7-L10; frontend/src/modules/kpi/components/WorkflowProfilesPanel.tsx:L94-L96].
- Target Analyst Days: Maximum allotted calendar time from sample preparation/assignment to submission for review. [src: frontend/src/modules/kpi/components/AddProfileVersionDialog.tsx:L119].
- Workflow Profile Version: Immutably recorded, electronically signed set of work units and target days for a workflow type default or test-specific override. [src: backend/MicroLIMS.Domain/Entities/WorkflowProfileVersion.cs:L1-L35].
- Adverse Trend: Flag raised on Environmental Monitoring excursion rate when at least 5 results exist and alert rate > 2.5% or action rate > 0.15%. [src: backend/MicroLIMS.Application/Services/Kpi/KpiTypes.cs:L40-L46; backend/MicroLIMS.Application/Services/Kpi/EmExcursionRateKpi.cs:L68-L70].
- Forward Load: Ratio of open work units assigned to an analyst divided by their forward capacity across the load horizon window. [src: backend/MicroLIMS.Application/Services/Kpi/AnalystWorkloadService.cs:L121].
- Utilisation: Ratio of delivered work units in period divided by total available capacity units up to today. [src: backend/MicroLIMS.Application/Services/Kpi/AnalystWorkloadService.cs:L120].
- D8 (Own-row restriction): Principle that analysts without `Kpi.ViewTeam` permission can only view their own individual metrics, while Section Heads and Admins see team-wide figures. [src: docs/superpowers/plans/2026-10-11-kpi-remodel-index.md:L81; backend/MicroLIMS.API/Controllers/KpiDashboardController.cs:L22].

### MOD-KPI-002 Analyst availability and KPI section settings
- Load Horizon: Number of working days forward (default 5 days) over which upcoming analyst capacity is calculated. [src: backend/MicroLIMS.Application/Services/Kpi/CapacityCalculator.cs:L11, L47-L56].
- Fraction Available: Proportion of standard working capacity available for an analyst during a specific leave or training window (e.g. 0.0 for full leave, 0.5 for half-time). [src: backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs:L125-L127].

### MOD-DC-001 Document Control: library and document lifecycle
- Document Master: The top-level governance record representing a controlled document title, company document code, and unique permanent MicroLIMS ID. [src: backend/MicroLIMS.Domain/Entities/DocumentMaster.cs:L1-L40].
- Company Document Code: The human-readable corporate identifier (e.g. "SOP-MIC-001") defined by quality policy. [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L44-L45].
- MicroLIMS Document ID: The permanent immutable system sequence ID (e.g. "DOC-0000042") allocated on registration. [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L96-L100].
- Document Revision: A versioned instance of a document master (e.g., Rev 01, Rev 01.1) holding files, change rationales, and lifecycle records. [src: backend/MicroLIMS.Domain/Entities/DocumentRevision.cs:L1-L45].
- Controlled PDF: The official released document file in PDF format (`.pdf`), presented in the controlled viewer with an "OFFICIAL COPY" chip (no watermark code was found). [src: frontend/src/modules/documentControl/components/ControlledPdfViewer.tsx:L150-L153].
- Source File: The editable authoring document in Microsoft Word format (`.docx` or `.doc`), openable by System Administrators, Section Heads and the owner or assigned author; the technical reviewer or approver only while their task is open (see the roles rule above) (corrected in Phase 13, RECONCILIATION.md). [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs:L84-L96].
- Technical Review: Peer review phase where assigned subject matter experts verify content accuracy and log findings across numbered cycles. [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L15-L65].
- Finding Resolution Ladder: Mandatory progressive workflow (`Open` -> `AuthorResponded` -> `ReviewerVerified` -> `Resolved`) governing reviewer comments. [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L497-L508].
- Electronic signature (approval): the signing step of the approval workspace, button "Sign & Approve": the user re-enters the account password, which is checked against the stored hash, before a revision becomes Effective or FutureEffective. The dialog text itself cites 21 CFR Part 11; the guide describes the function only (GAP-023). [src: frontend/src/modules/documentControl/components/ElectronicSignatureDialog.tsx:L119-L122, L193-L232; backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L526-L565].
- Periodic Review: Formal recurring review (e.g. every 24 months) to ensure an effective SOP remains scientifically and operationally valid. [src: backend/MicroLIMS.Application/Services/DocumentControl/PeriodicReviewService.cs:L20-L55].
- Void: Permanent invalidation of a Document Master registered in error that never reached effective release, releasing its code for reuse. [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L528-L534].

### MOD-DC-002 Document Control: reading and training
- Evidentiary Statement: The legally binding statement displayed at reading acknowledgement ("I confirm that I have read, understood, and agree to adhere to the contents of this controlled document revision.") frozen into the compliance record. [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs:L23-L25].
- Reading Progress: Informational client-side viewport scroll percentage, explicitly marked "Informational Only. Formal Acknowledgement Required". [src: frontend/src/modules/documentControl/components/ControlledPdfViewer.tsx:L194; frontend/src/modules/documentControl/components/DocumentAcknowledgementDialog.tsx:L180].
- Author (in segregation rules): the user who created the revision (`CreatedByUserId`); technical reviewer: any user assigned to, or deciding, a review task of that revision. [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L643-L686]
- Training Matrix: A two-dimensional grid cross-referencing organizational personnel against active controlled documents with color-coded compliance states. [src: frontend/src/modules/documentControl/pages/TrainingMatrixPage.tsx:L59-L816].
- Superseded Incomplete: Terminal status for a training assignment that was not completed before a newer revision became effective. [src: backend/MicroLIMS.Application/Services/DocumentControl/TrainingAssignmentService.cs:L106-L108].
- Trained on Superseded Only (matrix chip) / SUPERSEDED GAPS (dashboard card): a person who acknowledged an earlier revision but not the currently effective one. [src: frontend/src/modules/documentControl/pages/TrainingMatrixPage.tsx:L219-L222; backend/MicroLIMS.Application/Services/DocumentControl/TrainingMatrixService.cs:L154, L174].

### MOD-DC-003 Document Control: audit trail
- Audit Field Change: Granular capture of previous value and new value for every modified property in a document control entity. [src: backend/MicroLIMS.Application/Interfaces/IAuditEventService.cs:L6].

### MOD-DC-004 Document Control: configuration
- Review Cycle Months: Configurable periodic review frequency in months defined per Document Type (1 to 120 months). [src: backend/MicroLIMS.Domain/Entities/DocumentType.cs].

### MOD-DC-005 Document escalations (backend)
- Escalation Level: Milestone tier indicating assignment urgency (ApproachingDue, Due, Overdue). [src: backend/MicroLIMS.Domain/Enums/DocumentEscalationLevel.cs].
