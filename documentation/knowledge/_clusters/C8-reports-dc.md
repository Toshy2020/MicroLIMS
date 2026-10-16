# C8 Reports, KPI, Document Control

## modules

### MOD-REPORTS-001 Reports
- Purpose: Read-only query, analytical trending, multi-subject comparison, and tabular reporting across laboratory test results (ResultRecord flattened projection), media preparation and growth promotion testing (MediaEvaluation and challenges), and reference microorganism cryovial batches; provides multi-criteria filtering, CSV and PDF exports with audit logging, and deep-link drill-down to underlying laboratory records. [src: frontend/src/pages/Reports.tsx:L16-L157; backend/MicroLIMS.API/Controllers/ReportingController.cs:L17-L51; backend/MicroLIMS.Application/Services/ReportingQueryService.cs:L89-L93]
- Sub-features: SCR-REPORTS-001 (Overview tab), SCR-REPORTS-002 (Record Search tab), SCR-REPORTS-003 (Media / GPT tab), SCR-REPORTS-004 (Reference Strains tab), SCR-REPORTS-005 (Trending & Analysis tab). Status: IMPLEMENTED (with hardcoded fallback options and mock-styled deltas; see GAP-800, GAP-801). Lab scope: Cross-laboratory (Microbiology, Physicochemical FP and RM&PM); queries are scoped server-side to the user's accessible laboratory sections via `IUserSectionScopeService.GetAccessibleSectionIdsAsync`. [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L36-L38]

### MOD-REPORTS-002 Reporting backfill
- Purpose: Standalone administrative synchronization endpoint (`POST /api/admin/reporting/backfill`) that populates and maintains the flattened `ResultRecord` read-model from operational tables (`CountTestReading`, `WorkflowStepResult` for pathogens, `SampleLocation` for EM/water, `ParameterResult` for chemical/physicochemical), derives 1-based test order rounds, and refreshes sample-level approval timestamps and usernames for historically decided samples. [src: backend/MicroLIMS.API/Controllers/ReportingAdminController.cs:L9-L28; backend/MicroLIMS.Application/Services/ResultProjectionService.cs:L12-L20, L409-L514]
- Sub-features: None (backend endpoint only). Status: NOT-EXPOSED-IN-UI (see GAP-803). Lab scope: Global / system-wide across all laboratory sections. Gated to SystemAdministrator via `Reporting.Admin` permission. [src: backend/MicroLIMS.API/Controllers/ReportingAdminController.cs:L15]

### MOD-KPI-001 KPI and Workflow Profiles
- Purpose: Operational performance monitoring dashboard and workflow profile management; renders 8 headline performance tiles (Sample turnaround time, On time all stages, Right first time, Retest rate, OOS rate, WIP backlog, Incubation reads on time, EM excursion rate), 3 category breakdown sections (Flow, Quality, Micro), an interactive analyst workload table with capacity planning metrics (1 unit = 1 standard analyst-hour), definitions drawer, and signed workflow profile versioning (append-only, electronic signature with `SignatureMeaning.MasterDataChanged`). [src: docs/superpowers/plans/2026-10-11-kpi-remodel-index.md:L1-L22; frontend/src/modules/kpi/components/KpiPerformanceTab.tsx:L21-L127; backend/MicroLIMS.API/Controllers/KpiDashboardController.cs:L10-L44; backend/MicroLIMS.API/Controllers/WorkflowProfilesController.cs:L10-L51; backend/MicroLIMS.Application/Services/Kpi/KpiRegistry.cs:L3-L18; backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L12-L91]
- Sub-features: SCR-KPI-001 (KPI / Performance tab), SCR-KPI-002 (Workflow Profiles dialog/panel). Status: IMPLEMENTED. Lab scope: Scoped to caller's accessible laboratory sections (`IUserSectionScopeService.GetAccessibleSectionIdsAsync`). Analysts without `Kpi.ViewTeam` are restricted to their own individual figures (D8). [src: backend/MicroLIMS.API/Controllers/KpiDashboardController.cs:L14, L22; backend/MicroLIMS.Application/Services/Kpi/KpiDashboardService.cs:L105-L107]

### MOD-KPI-002 Analyst availability and KPI section settings
- Purpose: Backend data model and REST endpoints for configuring per-section capacity parameters (working weekdays bitmask, standard hours per analyst-day, and forward load horizon) and recording individual analyst availability adjustments (leave, training, secondments reducing capacity fractions from 0.0 to 1.0); non-overlapping date windows with reason audit. [src: backend/MicroLIMS.API/Controllers/KpiWorkloadController.cs:L10-L74; backend/MicroLIMS.Application/Services/Kpi/KpiSettingsService.cs:L12-L85; backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs:L12-L117; frontend/src/modules/kpi/services/kpiWorkloadService.ts:L44-L67]
- Sub-features: None (service exists in frontend `kpiWorkloadService.ts`, but no UI screen, form, or dialog caller exists in the client application; see GAP-802). Status: NOT-EXPOSED-IN-UI. Lab scope: Scoped per laboratory section (`DocumentSections`); editing requires `Kpi.ConfigureProfiles` and section access. [src: backend/MicroLIMS.API/Controllers/KpiWorkloadController.cs:L56, L65, L70]

### MOD-DC-001 Document Control: library and document lifecycle
- Purpose: Enterprise Document Management System (EDMS) for laboratory controlled documents (SOPs, test methods, specifications, policies, forms); provides document master registration, metadata governance, versioned draft authoring, change rationale itemization, multi-category impact assessment, multi-cycle technical review with finding resolution ladder, formal approval governed by 21 CFR Part 11 electronic signature, dual-PDF side-by-side comparison, automated effective date transitions, periodic review workspace, voiding, and draft cancellation. [src: frontend/src/modules/documentControl/pages/DocumentLibraryPage.tsx:L57-L611; frontend/src/modules/documentControl/pages/DocumentDetailPage.tsx:L82-L1414; backend/MicroLIMS.API/Controllers/DocumentControl/DocumentControlController.cs:L17-L120; backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L12-L35; backend/MicroLIMS.Application/Services/DocumentControl/DocumentRevisionService.cs:L13-L55; backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L15-L65; backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L20-L75; backend/MicroLIMS.Application/Services/DocumentControl/PeriodicReviewService.cs:L20-L55]
- Sub-features: SCR-DC-001 (Document Control Dashboard), SCR-DC-005 (Document Library), SCR-DC-006 (Document detail, revisions, workflow, and lifecycle dialogs). Status: IMPLEMENTED. Lab scope: Cross-laboratory / Enterprise-wide (organized by DocumentDepartment and DocumentSection taxonomies). [src: backend/MicroLIMS.Domain/Entities/DocumentMaster.cs:L1-L40]

### MOD-DC-002 Document Control: reading and training
- Purpose: Controlled document training curricula and personal reading lists; automates training assignment generation upon document revision activation, tracks reading progress, enforces conscious legal reading acknowledgement with frozen evidentiary statement capture, maintains an organizational training matrix cross-referencing personnel against active SOPs, and calculates executive training compliance metrics. [src: frontend/src/modules/documentControl/pages/MyReadingListPage.tsx:L86-L758; frontend/src/modules/documentControl/pages/TrainingMatrixPage.tsx:L59-L816; frontend/src/modules/documentControl/pages/ComplianceDashboardPage.tsx:L37-L442; backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs:L17-L45; backend/MicroLIMS.Application/Services/DocumentControl/TrainingAssignmentService.cs:L14-L48; backend/MicroLIMS.Application/Services/DocumentControl/TrainingMatrixService.cs:L16-L35]
- Sub-features: SCR-DC-002 (My Reading List), SCR-DC-003 (Training Matrix), SCR-DC-004 (Training Compliance Dashboard). Status: IMPLEMENTED. Lab scope: Cross-laboratory / Enterprise-wide. [src: backend/MicroLIMS.Domain/Entities/DocumentTrainingAssignment.cs:L1-L35]

### MOD-DC-003 Document Control: audit trail
- Purpose: Append-only, chronological audit trail and specialized query interface across all document control events, revision lifecycle transitions, file uploads/replacements, controlled PDF and source Word views/downloads, cryptographic integrity check failures, technical review findings, approval ceremonies, periodic reviews, reading acknowledgements, configuration updates, and security violations; provides multi-criteria filtering and CSV export up to 5,000 records with logged export events. [src: frontend/src/modules/documentControl/pages/DocumentAuditPage.tsx:L36-L319; backend/MicroLIMS.API/Controllers/DocumentControl/DocumentAuditController.cs:L15-L85; backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuditService.cs:L12-L120]
- Sub-features: SCR-DC-007 (Document Control Audit Trail). Status: IMPLEMENTED. Lab scope: Global / system-wide. [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuditService.cs:L33-L35]

### MOD-DC-004 Document Control: configuration
- Purpose: Administrative configuration of Document Types (with default review cycles), Departments, Sections, document numbering sequence configuration (prefix, digit format, sequence counter), and global module settings (e.g. controlled copy download permissions). [src: frontend/src/modules/documentControl/pages/DocumentConfigurationPage.tsx:L46-L684; backend/MicroLIMS.API/Controllers/DocumentControl/DocumentConfigurationController.cs:L15-L150; backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs:L12-L100]
- Sub-features: SCR-DC-008 (Document Control Configuration). Status: IMPLEMENTED. Lab scope: Global system configuration; gated strictly to SystemAdministrator via `Documents.ConfigManage`. [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuthorizationService.cs:L231-L233]

### MOD-DC-005 Document escalations (backend)
- Purpose: Automated and manual evaluation of document training assignments nearing due dates or overdue; manages three-tiered escalation triggers (Level 1: ApproachingDue, Level 2: Due, Level 3: Overdue) with recipient routing (AssignedUser, Manager, QACompliance), automatic status transitions to Overdue, escalation history retrieval, and manager resolution with regulatory audit. [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentEscalationController.cs:L16-L91; backend/MicroLIMS.Application/Services/DocumentControl/DocumentEscalationService.cs:L18-L180]
- Sub-features: None (backend REST API endpoints and background worker `DocumentEffectiveDateWorker` only; see GAP-807). Status: NOT-EXPOSED-IN-UI. Lab scope: System-wide. [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentEscalationController.cs:L18, L51-L87]

## screens

### MOD-REPORTS-001 Reports
- SCR-REPORTS-001 Reports — Overview tab. Route `/reports` (or `/reports?tab=overview`) [src: frontend/src/routes/AppRoutes.tsx:L115; frontend/src/pages/Reports.tsx:L25, L116-L123].
  - Who can open: Any authenticated user (no specific permission guard beyond route authentication) [src: frontend/src/routes/AppRoutes.tsx:L114-L116; backend/MicroLIMS.API/Controllers/ReportingController.cs:L23].
  - Page header: Title "Reports", Subtitle "Search, trend, and export laboratory results." [src: frontend/src/pages/Reports.tsx:L92].
  - Quick period selector: Rendered above tabs for all tabs except tab 5 (KPI tab); options: "30d" (default, 30 days back), "90d" (90 days back), "1y" (365 days back), "custom" (with HTML5 date inputs "From date" and "To date") [src: frontend/src/pages/Reports.tsx:L70-L83, L94-L102; frontend/src/modules/reports/components/QuickPeriodSelector.tsx:L16-L70].
  - Live data indicator: Header text "Operational Metrics: Aggregated directly from live laboratory ResultRecords" with VerifiedUserIcon and static Chip label "Live Database Data" [src: frontend/src/modules/reports/components/OverviewTab.tsx:L113-L125].
  - 6 Top KPI Cards (`KpiCard`):
    - "Total Tests": value (total tests formatted), delta percent (100% if >0, else 0%), delta direction "up", comparison label "live database records" [src: frontend/src/modules/reports/services/OverviewService.ts:L133-L139; frontend/src/modules/reports/components/OverviewTab.tsx:L129-L136].
    - "Approved Results": value, delta percent (`Math.round((approvedCount / totalCount) * 100)`), delta direction "up", comparison label `{approvedCount} of {totalCount} approved` [src: frontend/src/modules/reports/services/OverviewService.ts:L140-L146; frontend/src/modules/reports/components/OverviewTab.tsx:L137-L144].
    - "Pending Review": value, delta percent (100% if >0, else 0%), delta direction ("up" if >0, else "down"), comparison label "active in testing/review queue" [src: frontend/src/modules/reports/services/OverviewService.ts:L147-L153; frontend/src/modules/reports/components/OverviewTab.tsx:L145-L152].
    - "Pending Approval": value, delta percent (100% if >0, else 0%), delta direction ("up" if >0, else "down"), comparison label "awaiting section head release" [src: frontend/src/modules/reports/services/OverviewService.ts:L154-L160; frontend/src/modules/reports/components/OverviewTab.tsx:L153-L160].
    - "Out of Spec": value, delta percent (outOfSpecCount), delta direction ("up" if >0, else "down"), comparison label "quality signal (independent of analyst)", variant ("error" if >0, else "default"), tooltip "Laboratory Quality Metric: Evaluated independently of analyst performance" [src: frontend/src/modules/reports/services/OverviewService.ts:L161-L169; frontend/src/modules/reports/components/OverviewTab.tsx:L161-L168].
    - "Alert / Action Level": value, delta percent (alertActionCount), delta direction ("up" if >0, else "down"), comparison label "exceeded alert or action thresholds", variant ("warning" if >0, else "default") [src: frontend/src/modules/reports/services/OverviewService.ts:L170-L177; frontend/src/modules/reports/components/OverviewTab.tsx:L169-L176].
  - Middle row visualizations:
    - "Results by Category" (Donut/Pie chart): outerRadius 80, innerRadius 55, center label showing total tests count and "TOTAL"; right legend list with percentage and color dot [src: frontend/src/modules/reports/components/OverviewTab.tsx:L182-L252]. Empty state: "No matching records found" [src: L193].
    - "Results by Test" (Horizontal Bar chart): Top 10 tests by volume; y-axis width 156px, truncated with ellipsis, hover tooltip; bar label list on right with test count [src: frontend/src/modules/reports/components/OverviewTab.tsx:L254-L300; backend/MicroLIMS.Application/Services/ReportingQueryService.cs:L314-L320].
    - "Results by Location / Point" (Donut/Pie chart): Top 7 locations by volume; center text showing total location count and "Total"; right legend list with percentage and count [src: frontend/src/modules/reports/components/OverviewTab.tsx:L303-L359; backend/MicroLIMS.Application/Services/ReportingQueryService.cs:L325-L331].
  - Bottom row panels:
    - "Recent Results" table: Header button "View in Search" (navigates to tab 1); columns: "Subject / Ref" (subjectName, referenceNumber underneath), "Test" (testDisplayName), "Date Entered" (dd MMM yyyy HH:mm), "Entered By" (resultEnteredByName), "Status" (Chip: Approved=success, Rejected=error, other=warning); rows clickable to navigate to tab 1 [src: frontend/src/modules/reports/components/OverviewTab.tsx:L365-L420].
    - "Quality Signals" card: Out of Spec (count, red error styling), Alert / Action Level (count, amber warning styling), Pending Review (count, purple styling), Pending Approval (count, green styling) [src: frontend/src/modules/reports/components/OverviewTab.tsx:L423-L474].
    - "Quick Actions" card: Action buttons: "Search Records" (startIcon `<SearchIcon />`, navigates to tab 1), "Trending & Analysis" (startIcon `<ShowChartIcon />`, navigates to tab 4), "KPI / Performance" (startIcon `<AssessmentIcon />`, navigates to tab 5; rendered only when `canViewKpi` is true) [src: frontend/src/modules/reports/components/OverviewTab.tsx:L477-L518].

- SCR-REPORTS-002 Reports — Record Search tab. Route `/reports?tab=search` [src: frontend/src/pages/Reports.tsx:L25, L125-L133].
  - Who can open: Any authenticated user [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L23].
  - Layout: Two-column grid (Left: 300px filter panel + quick reports tiles; Right: results table) [src: frontend/src/modules/reports/RecordSearchTab.tsx:L84-L115].
  - Filter panel (`ReportFilterPanel`):
    - Header: "Filters", Link button "Reset" [src: frontend/src/modules/reports/components/ReportFilterPanel.tsx:L40-L45].
    - "Search" (text input): placeholder "Search Sample ID, Batch No., Item, Test, Location..." [src: L48-L54].
    - "Result Type" (select): options "All", "Quantitative", "Qualitative" [src: L56-L67].
    - "Category" (select): options "All" plus categories from backend `filter-options` [src: L69-L81].
    - "Test" (select): options "All" plus tests from backend `filter-options` (label: `{testCode} - {testDisplayName}`) [src: L83-L95].
    - "Result Level" (segmented button row): "All", "Within Limit", "Alert Level", "Action Level", "Out of Spec" [src: L97-L120].
    - "Status" (select): options "All", "Received", "InTesting", "UnderReview", "UnderApproval", "Approved", "Rejected", "RetestRequested" [src: L122-L132].
    - "Approval Status" (select): options "All", "Approved", "Pending", "Rejected" [src: L134-L144].
    - "Date Range" (two date inputs): "From date" and "To date" [src: L146-L164].
    - "Location / Point" (select): options "All" plus distinct subject names from backend `filter-options` [src: L166-L176].
    - Primary button: "Search Records" (triggers API query) [src: L178].
  - Quick Reports Tiles (`QuickReportsTiles`):
    - Header: "Quick Reports" [src: frontend/src/modules/reports/components/QuickReportsTiles.tsx:L52].
    - 8 clickable tiles: "Product", "Raw Material", "Water", "Environmental Monitoring", "After Cleaning", "Media/GPT" (switches to tab 2), "Reference Strains" (switches to tab 3), "Custom Report" (resets filters and focuses Search field) [src: L12-L21, L38-L48].
  - Results Table (`ReportResultsTable`):
    - Header: "Search Results", Chip `"{totalCount} Records Found"` [src: frontend/src/modules/reports/components/ReportResultsTable.tsx:L305-L308].
    - Action buttons (top right):
      - "Columns" (startIcon `<ViewColumnIcon />`) -> dropdown menu with checkboxes to toggle visible columns [src: L310-L312, L334-L341].
      - "Export PDF" (startIcon `<PictureAsPdfIcon />`, disabled when totalCount === 0) -> generates client-side printable PDF of current page [src: L313-L321, L164-L171].
      - "Export CSV" (startIcon `<FileDownloadIcon />`, disabled when exporting or totalCount === 0) -> opens ConfirmationDialog [src: L322-L330].
    - Confirmation Dialog: Message `This will export {totalCount} row(s) matching your current filters as a CSV file. This export will be logged.` Buttons: Cancel, Confirm [src: L484-L489].
    - Multi-selection bar (active when rows selected or select all clicked):
      - Link "Select All {totalCount} Matching Records" (selects every Approved result matching filters) [src: L348-L352].
      - Counter text: `All {totalCount} selected` or `{selectionCount} selected`, Link "Clear" [src: L354-L362].
      - Button "Export Selected PDF" [src: L367-L374].
      - Button "Export Selected CSV" [src: L375-L382].
      - Button "Analyze Selected" (navigates to Trending tab with first selected record's testCode and subjectName) [src: L383-L395].
    - Columns available (11 total):
      1. "Date/Time" (`dateTime`): date on top, time underneath (`dd MMM yyyy`, `HH:mm`) [src: L31, L250-L256].
      2. "Sample/Reference" (`reference`): clickable link opening `RecordDetailDialog` [src: L32, L258-L268].
      3. "Item/Location" (`subject`): subject name, with subjectDetail underneath if present [src: L33, L269-L274].
      4. "Type" (`type`): `CategoryBadge` [src: L34, L275].
      5. "Test" (`test`): testCode [src: L35, L276].
      6. "Result" (`result`): reportedValue (bold) [src: L36, L277].
      7. "Unit" (`unit`): unit string or "—" [src: L37, L278].
      8. "Result Level" (`resultLevel`): "—" if NotApplicable, else `StatusBadge` [src: L38, L279-L281].
      9. "Status" (`status`): `StatusBadge` (approval status) [src: L39, L282].
      10. "Approved By" (`approvedBy`): approvedByName or "—" [src: L40, L283].
      11. "Approval Date" (`approvalDate`): formatted date (`dd MMM yyyy`) or "—" [src: L41, L284].
      12. Row action menu button (icon `<MoreVertIcon />`): "View Record Details", "Analyze Trend", "Copy Reference Number" [src: L289-L298, L430-L460].
    - Pagination: Rows per page select (`[10, 25, 50, 100]`), page counter `{pageStart} - {pageEnd} of {totalCount}`, Previous page button, Next page button [src: L24, L462-L480].
    - Empty state: Title "No results found", subtitle "Try widening the date range or clearing some filters.", Button "Clear Filters" [src: L403-L410].

- SCR-REPORTS-003 Reports — Media / GPT tab. Route `/reports?tab=media-gpt` [src: frontend/src/pages/Reports.tsx:L25, L135-L140].
  - Who can open: Any authenticated user [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L23].
  - Filter panel (`MediaGptFilterPanel`): Search input, Media Type (select), Evaluation Type (select: default "GrowthPromotion"), Evaluation Outcome (select: "Conform", "NonConform"), Approval Status (select), Date Range; buttons Reset, Search [src: frontend/src/modules/reports/components/MediaGptFilterPanel.tsx:L1-L150; frontend/src/modules/reports/MediaGptReportTab.tsx:L26].
  - Top summary card (`MediaGptSummaryCard`): Total Media Lots, Growth Promotion Passed, GPT Failed, Pending Evaluation, Overall Pass Rate (%) [src: frontend/src/modules/reports/components/MediaGptSummaryCard.tsx:L1-L100].
  - Results table (`MediaGptResultsTable`):
    - Header: "Media Preparation & GPT Records", subtitle `Showing {from}–{to} of {totalCount} lots` [src: frontend/src/modules/reports/components/MediaGptResultsTable.tsx:L193-L199].
    - Action buttons: "Export CSV (Flattened)" / "Exporting..." (downloads flattened challenge CSV), "Print / PDF View" (opens printable report window) [src: L204-L225].
    - Columns:
      1. "Lot Number": lotNumber (bold primary color) [src: L119-L121].
      2. "Media Type": mediaType [src: L122].
      3. "Prep Date": preparedAt formatted (`dd MMM yyyy`) [src: L123].
      4. "Expiry Date": expiryDate formatted (`dd MMM yyyy`) [src: L124].
      5. "Evaluation": Chip ("GPT" for GrowthPromotion) [src: L125-L131].
      6. "Outcome": Chip (Conform=success, NonConform=error, other=warning), challenge count caption `{conformed}/{total} Challenges` [src: L132-L150].
      7. "Approval Status": Chip (Approved=success, Rejected=error), "Released" green text if released for use [src: L151-L165].
      8. "Prepared By": preparedByName [src: L166].
      9. "Approved By": approvedByName, approvedAt formatted underneath [src: L167-L176].
      10. "Actions": Tooltip "View Traceability Details", IconButton `<VisibilityIcon />` opens `MediaGptDetailDialog` (shows autoclave cycle, load type, temperature, pH, challenge organism ATCC, recovery %, reference media lot, incubation results) [src: L177-L186; frontend/src/modules/reports/components/MediaGptDetailDialog.tsx:L1-L200].

- SCR-REPORTS-004 Reports — Reference Strains tab. Route `/reports?tab=reference-strains` [src: frontend/src/pages/Reports.tsx:L25, L141-L143].
  - Who can open: Any authenticated user [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L23].
  - Filter panel (`ReferenceStrainFilterPanel`): Search input, Organism (select), Approval Status (select), Active / Destroyed status, Receipt date range, Usage date range; buttons Reset, Search [src: frontend/src/modules/reports/components/ReferenceStrainFilterPanel.tsx:L1-L120].
  - Results table (`ReferenceStrainResultsTable`):
    - Header: "Reference Strains & Working Culture Batches", subtitle `Showing {from}–{to} of {totalCount} batches` [src: frontend/src/modules/reports/components/ReferenceStrainResultsTable.tsx:L203-L209].
    - Action buttons: "Export CSV" / "Exporting..." (downloads cryovial batch CSV), "Print / PDF View" (generates printable PDF report) [src: L214-L235].
    - Columns:
      1. "Strain / ATCC": strainName, ATCC number caption [src: L119-L130].
      2. "Cryovial Code": cryovialCode (bold) [src: L131].
      3. "Manufacturer": manufacturerName, batch number caption [src: L132-L140].
      4. "Receipt Date": receiptDate formatted [src: L142].
      5. "Prep Date": preparedAt formatted [src: L143].
      6. "Expiry Date": expiryDate formatted [src: L144].
      7. "Vials Remaining": Chip `{vialsRemaining} / {numberOfVialsPrepared}` [src: L145-L152].
      8. "Storage": storageCondition [src: L153].
      9. "Approval Status": Chip (Approved=success, Rejected=error), "Destroyed" red caption if isDestroyed [src: L154-L168].
      10. "Prepared By": preparedByName [src: L169].
      11. "Approved By": approvedByName, approvedAt formatted underneath [src: L170-L179].
      12. "GPT Usage": Chip `{directUsageCount} GPT` [src: L180-L186].
      13. "Actions": Tooltip "View Batch Traceability", IconButton `<VisibilityIcon />` opens `ReferenceStrainDetailDialog` (shows thaw history, purity confirmations, direct GPT challenges, and indirect sample test order qualifications) [src: L187-L195; frontend/src/modules/reports/components/ReferenceStrainDetailDialog.tsx:L1-L220].

- SCR-REPORTS-005 Reports — Trending & Analysis tab. Route `/reports?tab=trending` [src: frontend/src/pages/Reports.tsx:L25, L145-L150].
  - Who can open: Any authenticated user [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L23].
  - Layout: Three-column layout (Left: Analysis Criteria; Center: Trend Chart & Statistics; Right: Analysis Actions & Compare) [src: frontend/src/modules/reports/components/TrendingTab.tsx:L189].
  - Left column ("Analysis Criteria"):
    - Parameter / Test (select): populated from backend filter options; fallback to hardcoded tests ("TAMC", "TYMC", "WATER_TAMC", "PATHOGEN_ECOLI") when empty [src: L202-L225].
    - Product / Item (select) or Location cascade: for Water/EM/AfterCleaning, renders `LocationCascadeFilter`; for flat categories, renders select populated from backend filter options; fallback to hardcoded product/item names (including a hardcoded product name) when empty [src: L227-L260].
    - Category (select): FinishedProduct, RawMaterial, Water, EnvironmentalMonitoring [src: L261-L285].
    - Date Range (select): "Last 30 Days" (`30d`), "Last 3 Months" (`3m`), "Last 6 Months" (`6m`), "Last 12 Months" (`12m`, default) [src: L286-L300].
    - Compare With (select): "None" (default), "Previous Period", "Product Comparison" (opens CompareDialog in products mode), "Location Comparison" (opens CompareDialog in locations mode) [src: L301-L327].
    - Button: "Apply Criteria" [src: L329-L331].
  - Center column:
    - Trend Chart Card: Header with test name, subject name, period, Chip ("Live Numeric Trend ({unit})" or "Qualitative Detection Event Log") [src: L338-L362].
    - For numeric results: ComposedChart with solid horizontal gridline, Results line (colored dots: green/blue normal, amber Alert, orange Action, red OOS; clickable to open `RecordDetailDialog`), Upper Limit (USL, red dashed "5 5"), Action Level (orange dashed "3 3"), Alert Level (amber dashed "2 2"), Mean line (purple dashed "4 4") [src: L376-L450].
    - For qualitative results: Pathogen Detection Event table (Reference, Item/Location, Pathogen, Result, Date Entered, Entered By, Status); empty state: "No Pathogen Detection Events — Zero detections recorded... All samples conformed (Absent / Negative)" [src: L452-L519].
    - Statistics Summary:
      - For numeric (10 metric cards): "No. of Results", "Minimum", "Maximum", "Mean", "Median", "Std. Deviation", "% Within Spec", "% Alert Level", "% Action Level", "Out of Spec" [src: L528-L669].
      - For qualitative (4 metric cards): "Detection Events", "Pathogen Parameter", "Surveillance Result" ("Conformant / Negative" or "Action Required"), "Period Range" [src: L671-L721].
  - Right column:
    - "Analysis Actions": Button "View Data Table" (opens `TrendingDataDialog`), Button "Export" (opens Menu: "CSV (Data Table)", "PDF (Data Table)", "PDF (Data Table + Chart)") [src: L727-L755].
    - "Quick Compare": Button "Compare Products" (startIcon `<CompareArrowsIcon />`, opens `CompareDialog` in products mode), Button "Compare Locations" (startIcon `<LocationOnIcon />`, opens `CompareDialog` in locations mode) [src: L757-L785].
    - "Drill Down" info card: "Click on any individual data point in the chart to inspect its underlying laboratory record in read-only mode." [src: L787-L794].
  - Compare Dialog (`CompareDialog`):
    - Title: "Multi-Series Trend Comparison", subtitle `{testDisplayName}: every subject with results in the selected date range` [src: frontend/src/modules/reports/components/CompareDialog.tsx:L55-L61].
    - Tabs: "Compare Products / Items" (`products`), "Compare Sampling Locations / Points" (`locations`) [src: L62-L69].
    - Table columns: Product / Item (or Location / Point), Tests Evaluated, Mean Result (numeric) or % Detected (qualitative), Alert / Action, OOS Count, % Within Spec (numeric) or % Not Detected (qualitative) [src: L85-L100].

### MOD-REPORTS-002 Reporting backfill
- None. Backend endpoint only (`POST /api/admin/reporting/backfill`). Status: NOT-EXPOSED-IN-UI (see GAP-803).

### MOD-KPI-001 KPI and Workflow Profiles
- SCR-KPI-001 Reports — KPI / Performance tab. Route `/reports?tab=performance` [src: frontend/src/pages/Reports.tsx:L25, L152-L154].
  - Who can open: Users holding `Kpi.View` (`PERMISSIONS.KPI_VIEW`); tab is completely hidden from the tab bar for users without this permission [src: frontend/src/pages/Reports.tsx:L36-L41; backend/MicroLIMS.API/Controllers/KpiDashboardController.cs:L14].
  - Filters bar (`KpiFilters`):
    - "From" (date input), "To" (date input); validated: required, From <= To [src: frontend/src/modules/kpi/components/KpiFilters.tsx:L56-L65; backend/MicroLIMS.Application/Services/Kpi/KpiDashboardService.cs:L93-L99].
    - "Category" (select, default "All categories") [src: L67-L72].
    - "Location / subject" (select, default "All locations") [src: L73-L79].
    - "Test" (select, default "All tests") [src: L80-L86].
    - Action buttons (right-aligned):
      - "Definitions" (startIcon `<InfoOutlinedIcon />`) -> opens `DefinitionsDrawer` [src: L89].
      - "Workflow Profiles" (startIcon `<TuneIcon />`, visible only if user holds `Kpi.ConfigureProfiles`) -> opens `WorkflowProfilesDialog` [src: L90-L92].
      - "Export PDF" (startIcon `<PictureAsPdfIcon />`, disabled while loading) -> generates printable A4 landscape report containing Headline measures, supporting series tables, and Workload table [src: L93, L80; frontend/src/modules/kpi/utils/exportKpiPdf.ts:L86-L167].
  - Metadata caption: `Calculated {timestamp} for your laboratory sections. Click any figure to see the records behind it.` [src: frontend/src/modules/kpi/components/KpiPerformanceTab.tsx:L102-L104].
  - 8 Headline KPI Tiles (`KpiTile`):
    1. Sample turnaround time (`sample-tat`): unit "days", lower is better; median value, P90 extra, prior comparison. [src: backend/MicroLIMS.Application/Services/Kpi/SampleTatKpi.cs:L13-L20].
    2. On time, all stages (`on-time-all-stages`): unit "%", higher is better; rate value, gaps chips (`NO_ASSIGNMENT_TIME`, `INCOMPLETE_TIMELINE`, `DEFAULT_TARGET`), monthly sparkline series. [src: backend/MicroLIMS.Application/Services/Kpi/OnTimeAllStagesKpi.cs:L21-L27, L98-L105].
    3. Right first time (`right-first-time`): unit "%", higher is better; rate of work units never returned, gaps chips (`UNWEIGHTED_TESTS`, `OBSERVATION_NOT_TRACKED`), monthly sparkline series. [src: backend/MicroLIMS.Application/Services/Kpi/RightFirstTimeKpi.cs:L20-L26, L97-L101].
    4. Retest rate (`retest-rate`): unit "%", lower is better; rate of retest decisions, monthly sparkline series. [src: backend/MicroLIMS.Application/Services/Kpi/RetestRateKpi.cs:L11-L17].
    5. OOS rate (`oos-rate`): unit "%", lower is better; rate of samples with OosGroupCode, monthly sparkline series. [src: backend/MicroLIMS.Application/Services/Kpi/OosRateKpi.cs:L12-L18].
    6. WIP backlog (`wip-backlog`): unit "tests", neutral direction; snapshot count of Pending and InProgress tests, series breakdown by current step. [src: backend/MicroLIMS.Application/Services/Kpi/WipBacklogKpi.cs:L14-L20, L39-L41].
    7. Incubation reads on time (`incubation-reads-on-time`): unit "%", higher is better; rate of reads completed <= expected + 4h grace, gap chip (`NO_EXPECTED_READING`), monthly sparkline series. [src: backend/MicroLIMS.Application/Services/Kpi/IncubationReadsOnTimeKpi.cs:L12-L18, L66-L68].
    8. EM excursion rate (`em-excursion-rate`): unit "%", lower is better; rate of alert + action results, extras `alertRate` and `actionRate`, flag `AdverseTrend` (if total >= 5 and alertRate > 2.5% or actionRate > 0.15%), monthly series. [src: backend/MicroLIMS.Application/Services/Kpi/EmExcursionRateKpi.cs:L17-L22, L67-L71].
    - Clicking any tile calls `openRecords(key)`, opening `KpiRecordsDrawer` with paginated underlying record rows [src: frontend/src/modules/kpi/components/KpiPerformanceTab.tsx:L64-L70, L125].
  - Section breakdowns (`KpiSection`):
    - "Flow": `stage-tat` (Time in each stage: Testing, Incubation, Review, Approval bar charts), `backlog-aging` (Backlog aging: testing/incubation vs review/approval in buckets <=50%, 50-100%, >100% of target). [src: frontend/src/modules/kpi/components/KpiPerformanceTab.tsx:L23, L114].
    - "Quality": `right-first-time`, `retest-rate`, `oos-rate`, `returns-by-test` (Returns by test: bar chart by test code). [src: frontend/src/modules/kpi/components/KpiPerformanceTab.tsx:L24, L115].
    - "Micro": `em-excursion-rate`, `incubation-reads-on-time`. [src: frontend/src/modules/kpi/components/KpiPerformanceTab.tsx:L25, L116].
  - Workload Table (`WorkloadTable`):
    - Section header: "Workload", subtitle: `Listed alphabetically. Click a column heading to sort; the choice is not saved. Units are a planning estimate, not a performance score (1 unit = 1 standard analyst-hour).` [src: frontend/src/modules/kpi/components/WorkloadTable.tsx:L99-L103].
    - Informational alerts:
      - If `data.onlyOwnRow`: "Showing your own numbers only." (D8 restriction for users without `Kpi.ViewTeam`) [src: L110].
      - If `data.capacityUsesDefaults`: "Capacity assumes full availability and the default hours." [src: L111-L113].
      - Warning chips: `{unassignedOpenTests} open tests have no analyst`, gap messages [src: L114-L119].
    - Table Columns:
      1. "Analyst": analyst fullName as button, opens tests drawer [src: L128, L147-L155].
      2. "Capacity (units)": capacityUnits formatted [src: L14, L67].
      3. "Delivered units": deliveredUnits formatted + `{deliveredTests} tests` caption [src: L15, L69-L76].
      4. "Utilisation": formatted ratio (e.g. 85.0%), chip "check profile" if `utilisationCheckProfile` (>100%) [src: L16, L78-L84].
      5. "Open units": openUnits formatted [src: L17, L68].
      6. "Forward load": formatted ratio, chip "Over capacity" if `forwardLoadOverCapacity` (>100% or open units with zero capacity) [src: L18, L85-L91].
      7. "Weighted on time": formatted ratio (null if <5 tests) [src: L19, L92].
      8. "Weighted right first time": formatted ratio (null if <5 tests) [src: L20, L93].
      9. "Gaps": warning chips for `{unweightedTests} delivered tests unweighted`, `{unweightedOpenTests} open tests unweighted`, `{testsOnDefaultTarget} tests on the default target` [src: L140, L158-L163].
    - Clicking analyst name calls `openTests(userId)`, opening `KpiRecordsDrawer` listing the analyst's test orders [src: frontend/src/modules/kpi/components/KpiPerformanceTab.tsx:L72-L78].

- SCR-KPI-002 Workflow Profiles panel / dialog. Mounted from button "Workflow Profiles" on KPI tab [src: frontend/src/modules/kpi/components/KpiFilters.tsx:L91; frontend/src/modules/kpi/components/WorkflowProfilesDialog.tsx:L1-L25].
  - Who can open: Users holding `Kpi.ConfigureProfiles` (`PERMISSIONS.KPI_CONFIGURE_PROFILES`) and `Kpi.View` [src: frontend/src/modules/kpi/components/KpiPerformanceTab.tsx:L37; backend/MicroLIMS.API/Controllers/WorkflowProfilesController.cs:L16, L42].
  - Content:
    - Explanatory text: `Work units: 1 unit = 1 standard analyst-hour of hands-on work. Target: analyst-stage days. A version takes effect on its effective date and is never edited; correct a mistake by adding a new version. Tests without a profile stay unweighted - they are never counted as 1.0.` [src: frontend/src/modules/kpi/components/WorkflowProfilesPanel.tsx:L94-L98].
    - Gaps warning alert: `{N} workflow types without work units, {N} without a target, {N} tests need configuration` with switch "Needs configuration only" [src: L108-L120].
    - Master table columns:
      1. Expand chevron (`KeyboardArrowDownIcon` / `KeyboardArrowRightIcon`): expands test-level overrides [src: L142-L146].
      2. "Workflow type": human label for live workflow types (e.g. "Count test (microbiology)", "Pathogen observation", "Measurement", "Gravimetric", "Qualitative", "Dissolution", "Disintegration", "Weight variation", "HPLC method assay", "Titration", "ICP-OES method assay") [src: L147; frontend/src/modules/kpi/types/workflowProfileTypes.ts:L3-L20].
      3. "Work units": effective type default work units or "Not set" [src: L148].
      4. "Target (days)": effective type default target days or "Not set" [src: L149].
      5. "Last change": effectiveFrom date and signer/creator name [src: L150-L161].
      6. "Status": Chip "Configured" (success), "Partial" (warning), or "Needs configuration" (default), plus Chip `"{N} draft(s)"` if drafts exist [src: L162-L169].
      7. Actions: Button "History" (opens `ProfileHistoryDrawer`), Button "Add version" (opens `AddProfileVersionDialog`) [src: L170-L179].
    - Expanded test override table columns:
      1. "Test": testCode [src: L198].
      2. "Name": testName [src: L199].
      3. "Work units": effective units [src: L200].
      4. "Target (days)": effective target days [src: L201].
      5. "Status": coverage chip + Chip "Override" if test-specific version exists [src: L202-L207].
      6. Actions: Button "History", Button "Add version" / "Add override" [src: L208-L223].
  - Add / Review Version Dialog (`AddProfileVersionDialog`):
    - Title: `Add version: {scope}` or `Review and sign: {scope}` [src: frontend/src/modules/kpi/components/AddProfileVersionDialog.tsx:L85].
    - Helper note: `Leave a field empty to inherit it from the workflow type default.` (for tests) or `Applies to every test of this workflow type that has no override.` (for workflow types) [src: L99-L102].
    - Fields:
      - "Work units" (number input, min 0, step 0.01; helper text "1 unit = 1 standard analyst-hour of hands-on work") [src: L103-L112].
      - "Target analyst days" (number input, min 0, step 0.01; helper text "Analyst-stage deadline, from assignment to submission for review") [src: L113-L121].
      - "Effective from" (date input, shrink label; helper text "Cannot be in the past, except for the first version of this profile") [src: L122-L130].
      - "Reason for change" (text input, required, multiline 3 rows; placeholder "e.g. SOP time study 2026") [src: L131-L140].
    - Primary button: "Continue to sign" (disabled until valid units/target, reason non-empty, valid date) -> opens `SignatureDialog` [src: L91-L93, L144-L149].
    - Electronic signature meaning statement: `I am adding a workflow profile version for {scope}: {u} work units (1 unit = 1 standard analyst-hour), {t} target analyst days, effective {effectiveFrom}. Reason: {reason}` with meaning `MasterDataChanged` [src: L51-L55; backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L81].

### MOD-KPI-002 Analyst availability and KPI section settings
- None. Backend endpoints and services exist (`/api/kpi/settings/{sectionId}`, `/api/kpi/availability`), but no client screen or dialog is implemented. Status: NOT-EXPOSED-IN-UI (see GAP-802).

### MOD-DC-001 Document Control: library and document lifecycle
- SCR-DC-001 Document Control Dashboard. Route `/document-control` [src: frontend/src/routes/AppRoutes.tsx:L246; frontend/src/modules/documentControl/pages/DocumentControlDashboardPage.tsx:L47-L476].
  - Who can open: Any authenticated user (no specific route guard beyond authentication) [src: frontend/src/routes/AppRoutes.tsx:L245-L247].
  - Page header: Title "Document Control Dashboard", Subtitle "Governance overview, quick status metrics, and recent activity." [src: frontend/src/modules/documentControl/pages/DocumentControlDashboardPage.tsx:L150-L152].
  - Header actions: Button "Register Document" (startIcon `<AddIcon />`, enabled if user holds `Documents.Register` permission; opens `RegisterDocumentDialog`) [src: L53, L158-L168].
  - 6 KPI Summary Cards:
    1. "Total Documents": count of active document masters, comparison caption "active registered masters" [src: L175-L184].
    2. "Effective": count of active masters with an effective revision, comparison caption "released & current" [src: L190-L199].
    3. "In Draft": count of active masters with current revision in Draft, comparison caption "authoring / in-progress" [src: L205-L214].
    4. "Void / Retired": count of voided masters, comparison caption "registered in error" [src: L220-L229].
    5. "Pending Approval": count of revisions in AwaitingApproval, comparison caption "awaiting sign-off" [src: L235-L244].
    6. "Periodic Review Due": count of active effective revisions overdue or due within 30 days for periodic review [src: L250-L259].
  - Recent Documents Table: Top 5 active masters; columns: Company Code (bold), MicroLIMS ID, Title, Rev, Status (StatusBadge), Actions (IconButton `<VisibilityOutlinedIcon />` navigating to detail) [src: L275-L330].
  - Recent Audit Events Table: Top 5 audit logs; columns: Timestamp (UTC), Action, Target, Actor, Reason [src: L345-L400].

- SCR-DC-005 Document Library. Route `/document-control/library` [src: frontend/src/routes/AppRoutes.tsx:L250; frontend/src/modules/documentControl/pages/DocumentLibraryPage.tsx:L57-L611].
  - Who can open: Any authenticated user [src: frontend/src/routes/AppRoutes.tsx:L245-L250].
  - Page header: Title "Document Library", Subtitle "Search, view, and manage controlled laboratory documents and SOPs." [src: frontend/src/modules/documentControl/pages/DocumentLibraryPage.tsx:L230-L232].
  - Header action: Button "Register Document" (startIcon `<AddIcon />`, visible/operable if holding `Documents.Register`) [src: L66, L235-L245].
  - Filter panel:
    - Search text input: placeholder "Search code, title, MicroLIMS ID, keywords..." [src: L260-L270].
    - "Type" (select): "All Types" plus active document types [src: L275-L285].
    - "Department" (select): "All Departments" plus active departments [src: L290-L300].
    - "Status" (select): "All Statuses", Draft, InReview, AwaitingApproval, FutureEffective, Effective, Superseded, Obsolete, Cancelled [src: L305-L320].
    - Switch "Include Cancelled / Voided" (default false; visible and operable for SectionHead / SysAdmin) [src: L325-L335].
    - Switch "Overdue Review Only" (default false) [src: L340-L350].
    - Link "Clear Filters" [src: L355-L360].
  - Results Table columns (11):
    1. "Company Code": companyDocumentCode (bold monospace, primary color) [src: L367, L415-L417].
    2. "MicroLIMS ID": microLimsDocumentId (monospace) [src: L368, L420].
    3. "Title": title [src: L369, L423].
    4. "Type": documentTypeName [src: L370, L426].
    5. "Dept / Section": departmentName / sectionName [src: L371, L429].
    6. "Owner": documentOwnerName [src: L372, L432].
    7. "Rev": currentRevisionNumber or "—" [src: L373, L435].
    8. "Status": `StatusBadge` mapped via `documentMasterStatusLabel` [src: L374, L438].
    9. "Files": Chip icons for Controlled PDF and Source Word document [src: L375, L441-L450].
    10. "Effective Date": formatted UTC date or "—" [src: L376, L453].
    11. "Actions": Tooltip "View Details" (IconButton `<VisibilityOutlinedIcon />`), Tooltip "View Controlled PDF" (IconButton `<PictureAsPdfOutlinedIcon />`), Tooltip "Void Document Master" (IconButton `<BlockOutlinedIcon />`, SectionHead only) [src: L377, L456-L475].
  - Pagination: 10, 25, 50, 100 rows per page [src: L580-L595].

- SCR-DC-006 Document Detail. Route `/document-control/documents/:id` [src: frontend/src/routes/AppRoutes.tsx:L251; frontend/src/modules/documentControl/pages/DocumentDetailPage.tsx:L82-L1414].
  - Who can open: Any authenticated user; Void documents restricted to SectionHead and SystemAdministrator [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuthorizationService.cs:L98-L101].
  - Page header: Button "Library" (`<ArrowBackIcon />`), Company Document Code, Document Title, MicroLIMS ID, StatusBadge [src: frontend/src/modules/documentControl/pages/DocumentDetailPage.tsx:L330-L360].
  - Lifecycle strip: `RevisionLifecycleStrip` displaying stage bubbles (Draft -> Technical Review -> Formal Approval -> Effective / Superseded / Obsolete) [src: L380-L420].
  - Action buttons (top right):
    - Button "View Controlled PDF" (startIcon `<PictureAsPdfOutlinedIcon />`, enabled if active controlled PDF exists; opens `ControlledPdfViewer`) [src: L440-L450].
    - Button "Compare Revisions" (startIcon `<CompareArrowsIcon />`, enabled if draft revision and effective revision both exist; opens `DualPdfComparisonViewer`) [src: L455-L465].
    - Menu button "Document Actions" (startIcon `<MoreVertIcon />`):
      - "Edit Draft Metadata" (startIcon `<EditOutlinedIcon />`, opens `DraftMetadataDialog`) [src: L480-L490].
      - "Create New Revision" (startIcon `<AutorenewOutlinedIcon />`, opens `CreateRevisionDialog`) [src: L495-L505].
      - "Submit for Technical Review" (startIcon `<RateReviewOutlinedIcon />`, opens `SubmitForReviewDialog`) [src: L510-L520].
      - "Assign Approver" (startIcon `<PersonAddOutlinedIcon />`, opens `AssignApproverDialog`) [src: L525-L535].
      - "Approval Workspace" (startIcon `<FactCheckOutlinedIcon />`, opens `ApprovalWorkspaceDialog`) [src: L540-L550].
      - "Cancel Draft Revision" (startIcon `<CancelOutlinedIcon />`, opens `CancelDraftDialog`) [src: L555-L565].
      - "Void Document Master" (startIcon `<BlockOutlinedIcon />`, SectionHead only, opens `VoidDocumentDialog`) [src: L570-L575].
  - Tabs (4):
    1. "Document":
       - Document Metadata card: Company Code, MicroLIMS ID, Title, Type, Department, Section, Document Owner, Confidentiality, Category, Created Date, Keywords (chips) [src: L590-L670].
       - Active Files card: Controlled PDF row (file version, filename, size, SHA-256 hash, uploaded date/user, Actions: View, Download), Source Word row (file version, filename, size, SHA-256 hash, uploaded date/user, Actions: Download), Button "Upload / Replace File" (opens `FileUploadDialog`) [src: L680-L750].
    2. `Revisions (${document.revisions.length})`: Table of all revisions; columns: Rev, Type, Status, Reason for Revision, Effective Date, Review Cycle, Next Review Date, Actions (View PDF, Edit Change Items opening `RevisionChangeItemsDialog`, Edit Impact Assessment opening `RevisionImpactAssessmentDialog`) [src: L760-L880].
    3. `Workflow (${document.assignments.length + reviewTasks.length + periodicReviewTasks.length})`:
       - Personnel Assignments panel: Assigned Author, Technical Reviewer, Approver roles; Button "Add Assignment" (opens `AssignmentDialog`), Button "Remove" [src: L900-L980].
       - Technical Review Tasks panel: Cycle #, Assigned Reviewer, Due Date, Status, Open Mandatory Findings count, Button "Open Review Drawer" (opens `TechnicalReviewDrawer`) [src: L990-L1070].
       - Formal Approval Tasks panel: Assigned Approver, Target Effective Date, Due Date, Status, Decision, Decision Notes, Button "Open Approval Workspace" (opens `ApprovalWorkspaceDialog`) [src: L1080-L1150].
       - Periodic Review Tasks panel: Scheduled Due Date, Assigned Reviewer, Status, Outcome, Summary, Button "Open Review Workspace" (opens `PeriodicReviewWorkspaceDialog`) [src: L1160-L1240].
    4. `Audit trail (${auditLogs.length})`: Record-specific chronological audit log; columns: Timestamp (UTC), Action, Actor, Category, Reason, Changes Summary [src: L1250-L1350].

### MOD-DC-002 Document Control: reading and training
- SCR-DC-002 My Reading List. Route `/document-control/my-reading-list` [src: frontend/src/routes/AppRoutes.tsx:L247; frontend/src/modules/documentControl/pages/MyReadingListPage.tsx:L86-L758].
  - Who can open: Any authenticated user [src: frontend/src/routes/AppRoutes.tsx:L245-L247].
  - Page header: Title "My Reading List", Subtitle "Review and acknowledge your assigned controlled documents." [src: frontend/src/modules/documentControl/pages/MyReadingListPage.tsx:L240-L242].
  - Tabs (5): "To read" (`PENDING`), "Overdue" (`OVERDUE`), "Completed" (`COMPLETED`), "Superseded" (`SUPERSEDED`), "All" (`ALL`) [src: L78-L84, L270-L280].
  - Filters: Search input ("Search by document code, title, or revision..."), Button "Refresh" [src: L290-L310].
  - Table columns:
    1. "Document": Company Document Code (bold), Title underneath [src: L330-L340].
    2. "Rev": RevisionNumber [src: L342].
    3. "Effective Date": formatted UTC date [src: L345].
    4. "Due Date": formatted UTC date, with amber/red Chip "Overdue" if `DueDateUtc < UtcNow` [src: L350-L360].
    5. "Status": `StatusBadge` (Assigned, Reading, Overdue, Acknowledged, SupersededIncomplete) [src: L365].
    6. "Reading Progress": LinearProgress bar with percentage, and caption "Informational Only. Formal Acknowledgement Required" [src: L370-L390].
    7. "Actions": Button "Read & Acknowledge" (startIcon `<MenuBookIcon />`, opens `ControlledPdfViewer`), Button "Acknowledge" (startIcon `<VerifiedUserIcon />`, opens `DocumentAcknowledgementDialog`) [src: L395-L430].

- SCR-DC-003 Training Matrix. Route `/document-control/training-matrix` [src: frontend/src/routes/AppRoutes.tsx:L248; frontend/src/modules/documentControl/pages/TrainingMatrixPage.tsx:L59-L816].
  - Who can open: Any authenticated user holding `Documents.TrainingViewMatrix` (or viewing own department/role) [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuthorizationService.cs:L39-L52; backend/MicroLIMS.API/Controllers/DocumentControl/DocumentTrainingMatrixController.cs:L20-L40].
  - Page header: Title "Training Matrix", Subtitle "Organizational reading & qualification matrix mapped by role, department, and document." [src: frontend/src/modules/documentControl/pages/TrainingMatrixPage.tsx:L175-L177].
  - Header actions: Button "Refresh", Button "Compliance Dashboard" (navigates to `/document-control/compliance-dashboard`) [src: L180-L195].
  - Filters:
    - Department (Select: "ALL" or specific department) [src: L205-L220].
    - Compliance Status (Select: "ALL", "Qualified", "Pending", "Overdue", "TrainedOnSupersededOnly") [src: L225-L240].
    - Search input (User or Document) [src: L245-L255].
  - Matrix Grid:
    - Sticky left columns: User Full Name, Username, Role, Department [src: L280-L300].
    - Column headers: Active Document Masters with effective revisions (Company Document Code, Title, Rev) [src: L305-L325].
    - Cells: Status badge:
      - "Qualified" (green Chip): current revision acknowledged [src: L335].
      - "Pending" (blue Chip): assignment active and within due date [src: L345].
      - "Overdue" (red Chip): assignment past due date [src: L355].
      - "Superseded Gap" (amber Chip): qualified on previous revision, not current [src: L365].
      - "—" (grey dash): not required/assigned [src: L375].
    - Clicking any cell opens the Cell Detail Drawer (showing assignment history, exact due date, acknowledged timestamp, and controlled PDF hash) [src: L420-L490].
    - Clicking a user name opens the User Compliance Drawer [src: L500-L580].
    - Clicking a document header opens the Document Compliance Drawer [src: L590-L670].

- SCR-DC-004 Training Compliance Dashboard. Route `/document-control/compliance-dashboard` [src: frontend/src/routes/AppRoutes.tsx:L249; frontend/src/modules/documentControl/pages/ComplianceDashboardPage.tsx:L37-L442].
  - Who can open: Any authenticated user [src: frontend/src/routes/AppRoutes.tsx:L245-L249].
  - Page header: Title "Training Compliance Dashboard", Subtitle "Executive & operational compliance metrics, departmental ranking, and overdue exceptions." [src: frontend/src/modules/documentControl/pages/ComplianceDashboardPage.tsx:L64-L67].
  - Header actions: Button "Refresh", Button "View Full Matrix" (navigates to `/document-control/training-matrix`) [src: L69-L85].
  - 6 Metric KPI Cards:
    1. "Overall Compliance Rate": percentage (>=95% green, >=85% amber, <85% red), comparison caption "qualified / required assignments" [src: L120-L140].
    2. "Required Assignments": total count of tracked assignments across effective documents [src: L145-L155].
    3. "Qualified": total acknowledged / completed assignments [src: L160-L170].
    4. "Pending Action": open assignments within grace period [src: L175-L185].
    5. "Overdue Exceptions": open assignments past due date [src: L190-L200].
    6. "Superseded Knowledge Gap": count of users qualified only on older revisions [src: L205-L215].
  - Departmental Compliance Table: columns Department Name, Required, Completed, Overdue, Compliance Rate (visual progress bar + percentage); sorted ascending by compliance rate (lowest first) [src: L240-L310].
  - Top Overdue Documents Table: Top 10 documents by overdue count; columns Document Code, Title, Overdue Count [src: L320-L380].

### MOD-DC-003 Document Control: audit trail
- SCR-DC-007 Document Control Audit Trail. Route `/document-control/audit` [src: frontend/src/routes/AppRoutes.tsx:L252; frontend/src/modules/documentControl/pages/DocumentAuditPage.tsx:L36-L319].
  - Who can open: Users holding role Reviewer, SectionHead, or SystemAdministrator [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuthorizationService.cs:L240-L242].
  - Page header: Title "Document Control Audit Trail", Subtitle "Append-only GxP audit log of all document actions, revisions, approvals, and security events." [src: frontend/src/modules/documentControl/pages/DocumentAuditPage.tsx:L110-L115].
  - Action button: "Export CSV" (startIcon `<FileDownloadOutlinedIcon />`, disabled while exporting or totalCount === 0) -> exports up to 5,000 audit rows as CSV and records `AuditTrailExported` audit event [src: L86-L105, L180-L195; backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuditService.cs:L95-L98].
  - Filter panel:
    - Search input (searches action code, reason, user, entity ID) [src: L120-L130].
    - "Action Category" (select): "All Categories", Document, Review, Approval, PeriodicReview, Configuration, Security [src: L135-L145].
    - "Date From" and "Date To" (date inputs) [src: L150-L160].
    - Buttons: "Filter", "Clear" [src: L165-L175].
  - Table columns:
    1. "Timestamp (UTC)": ISO UTC timestamp formatted (`dd MMM yyyy HH:mm:ss`) [src: L210].
    2. "Action Code": actionCode chip [src: L212].
    3. "Category": actionCategory chip [src: L215].
    4. "Target Entity": recordType / entityId [src: L218].
    5. "Actor": user FullName or SystemProcessName [src: L220].
    6. "Reason / Notes": regulatory reason string [src: L222].
    7. "Changes Summary": formatted diff of previous vs new values [src: L225].
  - Pagination: 10, 20, 50, 100 rows per page [src: L280-L295].

### MOD-DC-004 Document Control: configuration
- SCR-DC-008 Document Control Configuration. Route `/document-control/configuration` [src: frontend/src/routes/AppRoutes.tsx:L268-L269; frontend/src/modules/documentControl/pages/DocumentConfigurationPage.tsx:L46-L684].
  - Who can open: SystemAdministrator only (`Documents.ConfigManage` permission, verified server-side) [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuthorizationService.cs:L231-L233; frontend/src/modules/documentControl/pages/DocumentConfigurationPage.tsx:L49, L112-L120].
  - Page header: Title "Document Control Configuration", Subtitle "Manage document types, department taxonomies, numbering schemas, and module settings." [src: frontend/src/modules/documentControl/pages/DocumentConfigurationPage.tsx:L130-L135].
  - Tabs (4):
    1. "Document Types": Table of types (Code, Name, Default Review Cycle Months, Status: Active/Inactive switch, Actions: Edit button; Button "Add Document Type" opening dialog) [src: L145-L230].
    2. "Departments & Sections": Table of departments with nested section list (Department Code, Name, Status switch, Actions: Edit Dept, Add Section dialog, Section Status switches) [src: L240-L360].
    3. "Numbering Configuration": Form for sequence formatting (Prefix input e.g. "DOC-", Number Format input e.g. "0000000", Enabled switch, Button "Save Configuration") [src: L370-L450].
    4. "Module Settings": Table of configurable module keys (e.g. `DocumentControl.ControlledCopy.DownloadPermitted`), current value, description, Button "Edit" opening value dialog [src: L460-L550].

### MOD-DC-005 Document escalations (backend)
- None. Backend REST endpoints and background worker only. Status: NOT-EXPOSED-IN-UI (see GAP-807).

## roles

### MOD-REPORTS-001 Reports
- Route `/reports` is available to all authenticated users [src: frontend/src/routes/AppRoutes.tsx:L114-L116].
- Backend endpoints under `/api/reporting/*` are decorated with `[Authorize]` requiring any valid authenticated user [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L23].
- Scoping: Laboratory section scope is enforced server-side on every request via `IUserSectionScopeService.GetAccessibleSectionIdsAsync(userId)`. Users only see results, media lots, and cryovials belonging to their assigned sections; SystemAdministrator has null scope and sees all [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L36-L38, L210, L300].

### MOD-REPORTS-002 Reporting backfill
- Backend endpoint `POST /api/admin/reporting/backfill` is decorated with `[Authorize(Policy = PermissionConstants.ReportingAdmin)]` ("Reporting.Admin") [src: backend/MicroLIMS.API/Controllers/ReportingAdminController.cs:L15, L25].
- Default roles holding `Reporting.Admin`: `SystemAdministrator` only [src: documentation/knowledge/_raw/verified-roles-permissions.md:L81; backend/MicroLIMS.Shared/Constants/PermissionConstants.cs:L13].

### MOD-KPI-001 KPI and Workflow Profiles
- `Kpi.View` (`PERMISSIONS.KPI_VIEW` / `PermissionConstants.KpiView`):
  - Backend gate: `[Authorize(Policy = PermissionConstants.KpiView)]` on `KpiDashboardController`, `KpiWorkloadController`, and `WorkflowProfilesController` [src: backend/MicroLIMS.API/Controllers/KpiDashboardController.cs:L14; backend/MicroLIMS.API/Controllers/KpiWorkloadController.cs:L15; backend/MicroLIMS.API/Controllers/WorkflowProfilesController.cs:L16].
  - UI gate: `useHasPermission(PERMISSIONS.KPI_VIEW)` controls visibility of the "KPI / Performance" tab in `ReportsPage` and the "KPI / Performance" quick action button in `OverviewTab`. If absent, the tab is omitted and attempting to navigate to `?tab=performance` falls back to tab 0 (Overview) [src: frontend/src/pages/Reports.tsx:L36-L41, L152-L154; frontend/src/modules/reports/components/OverviewTab.tsx:L505-L515].
  - Default roles holding `Kpi.View`: `SystemAdministrator`, `SectionHead` [src: backend/MicroLIMS.Shared/Constants/PermissionConstants.cs:L124, L137; backend/MicroLIMS.Persistence/Seed/DbSeeder.cs:L544].
- `Kpi.ViewTeam` (`PermissionConstants.KpiViewTeam`):
  - Backend gate: Checked as a user claim in `KpiDashboardController.cs:L22` and `KpiWorkloadController.cs:L30` (`User.HasClaim("permission", PermissionConstants.KpiViewTeam)`). Enforced in `KpiDashboardService.BuildQueryAsync` and `AnalystWorkloadService.GetAsync`. If a user lacks `Kpi.ViewTeam` and queries an analystId other than their own, the server throws `UnauthorizedAccessException("You can only view your own analyst figures.")` or returns their own row only (D8) [src: backend/MicroLIMS.Application/Services/Kpi/KpiDashboardService.cs:L106-L107; backend/MicroLIMS.Application/Services/Kpi/AnalystWorkloadService.cs:L9-L11, L60-L72].
  - Default roles holding `Kpi.ViewTeam`: `SystemAdministrator`, `SectionHead` [src: backend/MicroLIMS.Shared/Constants/PermissionConstants.cs:L124, L137].
- `Kpi.ConfigureProfiles` (`PERMISSIONS.KPI_CONFIGURE_PROFILES` / `PermissionConstants.KpiConfigureProfiles`):
  - Backend gate: `[Authorize(Policy = PermissionConstants.KpiConfigureProfiles)]` on `WorkflowProfilesController` (`POST /api/kpi/workflow-profiles`, `POST /api/kpi/workflow-profiles/{id}/sign`) [src: backend/MicroLIMS.API/Controllers/WorkflowProfilesController.cs:L42, L47].
  - UI gate: Gated in `KpiFilters.tsx` (button "Workflow Profiles" rendered only when `onOpenProfiles` is provided) and in `WorkflowProfilesPanel.tsx` (buttons "Add version" and "Add override" shown only when `canConfigure` is true) [src: frontend/src/modules/kpi/components/KpiFilters.tsx:L90-L92; frontend/src/modules/kpi/components/WorkflowProfilesPanel.tsx:L58, L174-L178, L215-L222].
  - Default roles holding `Kpi.ConfigureProfiles`: `SystemAdministrator`, `SectionHead` [src: backend/MicroLIMS.Shared/Constants/PermissionConstants.cs:L124, L137].
- Segregation of Duties:
  - Profile version electronic signing enforces re-authentication via password. A user cannot sign an already Effective version (`InvalidOperationException: "This version is already Effective and cannot be signed again."`) [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L102-L103].

### MOD-KPI-002 Analyst availability and KPI section settings
- Reads (`GET /api/kpi/settings/{sectionId}`, `GET /api/kpi/availability`) require `Kpi.View`. Users without `Kpi.ViewTeam` receive only their own availability rows [src: backend/MicroLIMS.API/Controllers/KpiWorkloadController.cs:L15, L52, L61; backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs:L37-L38].
- Writes (`PUT /api/kpi/settings/{sectionId}`, `POST /api/kpi/availability`, `PUT /api/kpi/availability/{id}`) require `Kpi.ConfigureProfiles` and section membership [src: backend/MicroLIMS.API/Controllers/KpiWorkloadController.cs:L56, L65, L70; backend/MicroLIMS.Application/Services/Kpi/KpiSettingsService.cs:L87-L94; backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs:L130-L137].

### MOD-DC-001 Document Control: library and document lifecycle
- Document Control Authority Rules (`DocumentAuthorizationService.cs`):
  - Document Master Registration: Gated by permission `Documents.Register`. Seeded to `SystemAdministrator`, `SectionHead` (Document Controller), and `Analyst` (covers Document Authors) [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuthorizationService.cs:L84-L87; backend/MicroLIMS.Persistence/Seed/DbSeeder.cs:L524-L576].
  - Draft Metadata Editing & File Replacement: Permitted for `SystemAdministrator`, `SectionHead`, or the designated `DocumentOwnerUserId` / assigned `Author` for that specific document [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuthorizationService.cs:L106-L130].
  - Cancel Draft Revision: Permitted for `SystemAdministrator`, `SectionHead`, or the designated Document Owner / assigned Author [src: L132-L145].
  - Void Document Master: Strictly restricted to role type `SectionHead` (Document Controller). Even `SystemAdministrator` is refused (`role == RoleType.SectionHead`) [src: L147-L154].
  - Source Word Document Access (`CanAccessSourceFileAsync`): Read access restricted to `SystemAdministrator`, `SectionHead`, Document Owner / assigned Author, or an active assigned Technical Reviewer or Approver on an open task for that specific revision [src: L156-L204].
  - Controlled PDF Access (`CanAccessControlledPdfAsync`): For Effective revisions, open to all active users. For Draft/non-effective states, restricted to Admin, SectionHead, Reviewer, or Owner/Author [src: L206-L227].
  - Revision Number Override: Gated strictly to role type `SectionHead` (Document Controller) [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentRevisionService.cs:L109-L112].
- Strict Segregation of Duties (SoD) Rules (No Administrative Exemption) [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs; backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs; backend/MicroLIMS.Application/Services/DocumentControl/PeriodicReviewService.cs]:
  - Author != Technical Reviewer:
    - `"Segregation of Duties Violation: The author of a document revision cannot be assigned as its technical reviewer."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L107].
    - `"Segregation of Duties Violation: The author cannot assign themselves as technical reviewer."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L110].
    - `"Segregation of Duties Violation: The author cannot perform technical review of their own revision."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L555].
  - Author != Finding Resolver:
    - `"Segregation of Duties Violation: The author cannot verify reviewer findings."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L420].
    - `"Segregation of Duties Violation: The author cannot close or resolve a reviewer-mandated finding without reviewer verification."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L480].
  - Author != Approver:
    - `"Segregation of Duties Violation: The author of a document revision cannot be assigned as its approver."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L651].
    - `"Segregation of Duties Violation: The author of a document revision cannot approve or decide upon their own revision."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L672].
  - Technical Reviewer != Approver:
    - `"Segregation of Duties Violation: A technical reviewer for this revision cannot be assigned as its approver."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L663].
    - `"Segregation of Duties Violation: A technical reviewer for this revision cannot approve or decide upon this revision."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L683].
  - Author != Periodic Reviewer:
    - `"Segregation of Duties Violation: The author cannot perform or complete the periodic review."` [src: backend/MicroLIMS.Application/Services/DocumentControl/PeriodicReviewService.cs:L554].

### MOD-DC-002 Document Control: reading and training
- Reading Lists (`MyReadingListPage`): Open to all authenticated users for their own reading assignments [src: frontend/src/routes/AppRoutes.tsx:L247; backend/MicroLIMS.Application/Services/DocumentControl/TrainingAssignmentService.cs:L20-L45].
- Reading Acknowledgement: Restricted strictly to the assigned user (`actingUserId == assignment.AssignedUserId`). If another user attempts acknowledgement, rejected with audit security violation: `"You are not authorized to acknowledge an assignment assigned to another user."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs:L154-L168].
- Training Matrix Viewing: Gated by `Documents.TrainingViewMatrix`. Seeded to `SystemAdministrator`, `SectionHead`, and `Reviewer` [src: backend/MicroLIMS.Persistence/Seed/DbSeeder.cs:L524-L559].
- Training Assignment & Curricula Management: Gated by `Documents.TrainingAssign`. Seeded to `SystemAdministrator` and `SectionHead` [src: backend/MicroLIMS.Persistence/Seed/DbSeeder.cs:L524-L546].

### MOD-DC-003 Document Control: audit trail
- Audit Trail Viewing (`CanQueryGlobalAuditAsync` / `CanViewRecordAuditAsync`): Restricted to role types `SystemAdministrator`, `SectionHead` (Document Controller), or `Reviewer` (QA Auditor), plus Document Owner/Author for record-level audit [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuthorizationService.cs:L235-L253].

### MOD-DC-004 Document Control: configuration
- Module Configuration Management (`CanManageConfigurationAsync`): Restricted strictly to role type `SystemAdministrator` (`isActive && role == RoleType.SystemAdministrator`). SectionHead, Reviewer, and Analyst are refused (`"Only System Administrators can manage module configuration."`) [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuthorizationService.cs:L229-L233; backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs:L33-L36].

### MOD-DC-005 Document escalations (backend)
- Escalation Execution and Resolution: Gated to `Documents.TrainingAssign` (`User.HasClaim("permission", PermissionConstants.DocumentsTrainingAssign)`), held by `SectionHead` and `SystemAdministrator`. Non-managers receive 403 Forbidden [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentEscalationController.cs:L32-L34, L51-L86].

## workflows

Pending Phase 4.

## business-rules

### MOD-REPORTS-001 Reports
- BR-REPORTS-001 Approval Status Derivation. IMPLEMENTED (verified). Approval status in the reporting projection is derived on-the-fly from `SampleStatus`: `SampleStatus.Approved` -> "Approved"; `SampleStatus.Rejected` -> "Rejected"; `SampleStatus.Voided` -> "Voided"; `SampleStatus.Cancelled` -> "Cancelled"; any other status (`Received`, `InTesting`, `UnderReview`, `UnderApproval`, `RetestRequested`) -> "Pending". Closed samples (`Voided`, `Cancelled`) never read as Pending or Rejected [src: backend/MicroLIMS.Application/Services/ReportingQueryService.cs:L109-L116].
- BR-REPORTS-002 Imputation for Below Detection Limit Points. IMPLEMENTED (verified). Below detection limit readings (e.g. `<10`) are parsed and flagged with `IsBelowDetectionLimit = true` and `DetectionLimit = 10`. For numeric statistical trending (mean, standard deviation, and trend charts), imputed values are substituted at query time as `DetectionLimit / 2`. The underlying `ResultRecord` always preserves the calculated `NumericValue` and original `ReportedValue` [src: backend/MicroLIMS.Application/Services/ReportingQueryService.cs:L259-L268, L508-L509; backend/MicroLIMS.Application/Services/ResultProjectionService.cs:L81-L95].
- BR-REPORTS-003 Sample Standard Deviation Calculation. IMPLEMENTED (verified). Calculated as sample standard deviation: `sqrt(sum((x - mean)^2) / (n - 1))`. For fewer than two points (`n < 2`), standard deviation is defined as `0` [src: backend/MicroLIMS.Application/Services/ReportingQueryService.cs:L513-L519].
- BR-REPORTS-004 Export Row Cap. IMPLEMENTED (verified). CSV exports for ResultRecords, Media GPT, and Reference Strains enforce a hard cap of 10,000 rows (`MaxExportRows = 10_000`). If a query matches more than 10,000 rows, the query is not truncated; the server throws `InvalidOperationException` forcing the user to narrow their filters [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L28, L160-L165, L250-L254, L332-L336].
- BR-REPORTS-005 Trending Numeric-Only Restriction. IMPLEMENTED (verified). Analytical trending (`GetTrendAsync`) is restricted to tests configured with `WorkflowType.CountTest`. If called on a qualitative or observation test, the server throws `InvalidOperationException: "Trending is only available for numeric results. {testCode} produces qualitative results."` Qualitative tests must be inspected via Qualitative Events [src: backend/MicroLIMS.Application/Services/ReportingQueryService.cs:L246-L248].
- BR-REPORTS-006 Qualitative Compliance Evaluation in Comparison. IMPLEMENTED (verified). In `GetCompareBySubjectAsync`, compliance for qualitative tests counts both single-sample pathogen session results ("Detected", "Not Detected", "Pending Confirmation") and EM/After-Cleaning batch results ("Detected", "Absent"). Compliance percentage is calculated as `(notDetectedCount / testsEvaluated) * 100`, rounded to 1 decimal place. For qualitative subjects, Alert/Action and OOS counts are honest zeros [src: backend/MicroLIMS.Application/Services/ReportingQueryService.cs:L478-L501].

### MOD-REPORTS-002 Reporting backfill
- BR-REPORTS-007 Idempotent Projection Upsert. IMPLEMENTED (verified). Each source row is mapped into `ResultRecord` using a unique constraint `(SourceTable, SourceId, Round)` or `(SourceTable="WorkflowStepResult", TestOrderId, Round)`. Re-running backfill updates existing rows in place without duplication [src: backend/MicroLIMS.Application/Services/ResultProjectionService.cs:L15-L18, L32-L62].
- BR-REPORTS-008 Round Derivation. IMPLEMENTED (verified). A test order's `Round` is its 1-based ordinal position among all test orders ever created for the same Sample ID and Test Code, ordered by ID ascending. Round 1 is the original order; subsequent rounds represent retests [src: backend/MicroLIMS.Application/Services/ResultProjectionService.cs:L64-L79].

### MOD-KPI-001 KPI and Workflow Profiles
- BR-KPI-001 Standard Work Unit Definition. IMPLEMENTED (verified). Exactly 1 work unit represents 1 standard analyst-hour of hands-on work. Work units are a planning and capacity estimation metric, not an analyst performance score [src: docs/superpowers/plans/2026-10-11-kpi-remodel-index.md:L7-L10; frontend/src/modules/kpi/components/WorkflowProfilesPanel.tsx:L94-L96; frontend/src/modules/kpi/components/WorkloadTable.tsx:L101-L102].
- BR-KPI-002 Profile Resolution Precedence and Effective Dating. IMPLEMENTED (verified). To resolve a test's profile on date `t`:
  1. Check for test-specific override: `WorkflowProfileVersion` with `TestCode == testCode`, `Status == Effective`, and `EffectiveFrom <= t`, ordered by `EffectiveFrom` descending, then `Id` descending.
  2. Fall back to workflow type default: `WorkflowProfileVersion` with `WorkflowType == type`, `TestCode == null`, `Status == Effective`, and `EffectiveFrom <= t`, ordered by `EffectiveFrom` descending, then `Id` descending.
  3. If neither defines work units, `WorkUnits` is null (unweighted). If neither defines target analyst days, `TargetAnalystDays` is null (falls back to 7 days in SLA calculations). Unweighted tests are never given a default weight of 1.0 [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileResolver.cs:L1-L60; backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L171-L177].
- BR-KPI-003 Profile Version Immutability and Draft Mutation. IMPLEMENTED (verified). `WorkflowProfileVersions` table is append-only, enforced by database trigger `TR_WorkflowProfileVersions_Immutability`. An Effective version cannot be modified or deleted. The only permitted state transition is `Draft` to `Effective`, which occurs upon electronic signature (`SignatureMeaning.MasterDataChanged`). Creating a new effective version inserts as Draft and flips to Effective in the same transaction [src: docs/superpowers/plans/2026-10-11-kpi-remodel-index.md:L78; backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L70-L84, L102-L127].
- BR-KPI-004 Baseline Back-Dating Exception. IMPLEMENTED (verified). Effective dates cannot be in the past (`effectiveFrom < today`), except for the baseline version: the very first Effective version of a scope (`WorkflowType` + `TestCode`) is permitted to have an effective date in the past to establish initial historical weighting. All subsequent versions must be effective today or in the future [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L277-L291].
- BR-KPI-005 Turnaround Time (TAT) Calculation (Tile 1). IMPLEMENTED (verified). Measures calendar days from `Sample.ReceivedAt` to `Sample.ApprovedAt` (where decision is Approve). Population: distinct non-retest samples approved in period within accessible sections. Discrepancy from spec: spec called for `ReviewWorkflowEvent`; implemented using `Sample.ApprovedAt` to avoid polymorphic event collision and time-provider detachment. Value is median calendar days; 90th percentile (P90) is calculated alongside via linear interpolation [src: docs/superpowers/plans/2026-10-11-kpi-remodel-index.md:L68; backend/MicroLIMS.Application/Services/Kpi/SampleTatKpi.cs:L13-L44].
- BR-KPI-006 On-Time All Stages SLA Calculation (Tile 2). IMPLEMENTED (verified). Evaluates samples approved in period. A sample is on time only if all stages for all visible sections meet their SLAs:
  - Analyst stage: `SamplePreparation.PreparedAt` to `SampleSectionSignoff.SubmittedForReviewAt` <= target days resolved via `AnalystStageTarget.DaysFor(codes, ApprovedAt, resolver)` (uses profile in force on approval date; defaults to 7 days if unconfigured).
  - Review stage: `SubmittedForReviewAt` to `ReviewedAt` <= 24 calendar hours (`StageLimit = TimeSpan.FromHours(24)`).
  - Approval stage: `ReviewedAt` to `ApprovedAt` <= 24 calendar hours (`StageLimit = TimeSpan.FromHours(24)`).
  - Samples without `SamplePreparation` (FP physicochemical, EM, retest clones) cannot be evaluated and are excluded with gap `NO_ASSIGNMENT_TIME` [src: docs/superpowers/plans/2026-10-11-kpi-remodel-index.md:L70; backend/MicroLIMS.Application/Services/Kpi/OnTimeAllStagesKpi.cs:L9-L30, L84-L101].
- BR-KPI-007 Right First Time (RFT) Calculation (Tile 3). IMPLEMENTED (verified). Measures `(work units of approved tests never returned to analyst) / (work units of all approved tests)` in period. Counted in period when the section sign-off was approved. Tests without a work-unit profile are excluded and listed under gap `UNWEIGHTED_TESTS`. Pathogen observation tests (`WorkflowType.Observation`) cannot record return events and are excluded under gap `OBSERVATION_NOT_TRACKED` [src: docs/superpowers/plans/2026-10-11-kpi-remodel-index.md:L71-L72; backend/MicroLIMS.Application/Services/Kpi/RightFirstTimeKpi.cs:L20-L30, L75-L101].
- BR-KPI-008 Retest Rate Calculation (Tile 4). IMPLEMENTED (verified). Measures `(distinct samples with a RetestRetainedSample decision in period) / (distinct samples with at least one decided section sign-off in period)`. Retest clones created from earlier samples are excluded. Stated in UI and definitions as a laboratory quality signal, not an analyst measure [src: docs/superpowers/plans/2026-10-11-kpi-remodel-index.md:L69; backend/MicroLIMS.Application/Services/Kpi/RetestRateKpi.cs:L11-L30].
- BR-KPI-009 OOS Rate Calculation (Tile 5). IMPLEMENTED (verified). Measures `(distinct samples with an OosGroupCode in period) / (distinct samples with at least one decided section sign-off in period)`. Discrepancy from spec: spec called for `OOSInvestigation` decision, but code never writes `OOSInvestigation`; implemented using `Sample.OosGroupCode != null`. Retest rate is a mathematical subset of OOS rate [src: docs/superpowers/plans/2026-10-11-kpi-remodel-index.md:L69; backend/MicroLIMS.Application/Services/Kpi/OosRateKpi.cs:L5-L31].
- BR-KPI-010 WIP Backlog Snapshot (Tile 6). IMPLEMENTED (verified). Measures test orders with status `Pending` or `InProgress` that are not superseded at query time (`q.Now`). Filter period does not apply and no historical prior is calculated. Series breakdown shows count by `CurrentStep` (e.g. Preparation, Inoculation, Incubating, Reading). Tests waiting for review or approval appear in Backlog Aging, not WIP Backlog [src: docs/superpowers/plans/2026-10-11-kpi-remodel-index.md:L74; backend/MicroLIMS.Application/Services/Kpi/WipBacklogKpi.cs:L7-L46].
- BR-KPI-011 Incubation Reads On-Time SLA (Tile 7). IMPLEMENTED (verified). Measures `(completed incubation windows where CompletedAt <= ExpectedReadingAt + 4 hours) / (total completed incubation windows in period)`. Windows with no expected reading time are excluded under gap `NO_EXPECTED_READING`. Media-challenge incubations are excluded (`TestOrderId != null`). Note: stage-1 to stage-2 transfers complete a window and count as reads (known documented limitation) [src: docs/superpowers/plans/2026-10-11-kpi-remodel-index.md:L75; backend/MicroLIMS.Application/Services/Kpi/IncubationReadsOnTimeKpi.cs:L12-L21, L59-L68].
- BR-KPI-012 Environmental Monitoring Excursion Rate (Tile 8). IMPLEMENTED (verified). Measures `(EM ResultRecord rows with AlertLevel or ActionLevel) / (EM ResultRecord rows with WithinLimit, AlertLevel, or ActionLevel)`. Results without levels (e.g. qualitative detected/absent) are excluded. Adverse trend flag (`AdverseTrend`) triggers when total results >= 5 (`MinSampleForRate = 5`) and `alertRate > 2.5%` (`EmAlertRateAdverse = 0.025`) or `actionRate > 0.15%` (`EmActionRateAdverse = 0.0015`) [src: docs/superpowers/plans/2026-10-11-kpi-remodel-index.md:L76; backend/MicroLIMS.Application/Services/Kpi/EmExcursionRateKpi.cs:L17-L25, L61-L71; backend/MicroLIMS.Application/Services/Kpi/KpiTypes.cs:L42-L46].
- BR-KPI-013 Backlog Aging Target Usage Buckets. IMPLEMENTED (verified). Open work is grouped by ratio of age to workflow target: `within50` (<= 0.5x target), `50to100` (0.5x to 1.0x target), and `over100` (> 1.0x target). For testing and incubation, target is `resolver.Resolve(testCode).TargetAnalystDays` (default 7 days). For review and approval gates, target is 24 hours (`GateTargetDays = 1m`) [src: backend/MicroLIMS.Application/Services/Kpi/BacklogAgingKpi.cs:L10-L15, L42-L45, L64-L71, L108-L115].
- BR-KPI-014 Returns by Test Aggregation. IMPLEMENTED (verified). Counts occurrences of `TestReturnEvent` in period, grouped alphabetically by `TestCode`. Process signal grouped strictly by test code, never by individual person [src: docs/superpowers/plans/2026-10-11-kpi-remodel-index.md:L73; backend/MicroLIMS.Application/Services/Kpi/ReturnsByTestKpi.cs:L6-L22, L45-L47].
- BR-KPI-015 Workload Capacity and Filtering Rule. IMPLEMENTED (verified). Capacity is capped at today (`capacityEnd = min(periodEnd, today)`): days in the future add zero capacity. When category, location, or test filters are set, analyst utilisation and forward load are set to null with gap `CAPACITY_NOT_FILTERED`, because capacity applies to an analyst's entire availability and dividing narrowed work by full capacity would misleadingly read as under-load [src: docs/superpowers/plans/2026-10-11-kpi-remodel-index.md:L87; backend/MicroLIMS.Application/Services/Kpi/AnalystWorkloadService.cs:L76-L80, L110-L121].
- BR-KPI-016 Minimum Sample Threshold for Individual Rates. IMPLEMENTED (verified). On an analyst workload row, weighted on-time and weighted right-first-time rates are suppressed (returned as null) if the analyst has fewer than 5 evaluated tests (`MinSampleForRate = 5`), to prevent statistical noise and unfair per-person pressure. Suppressed rows are reported under gap `RATES_TOO_FEW_RECORDS` [src: docs/superpowers/plans/2026-10-11-kpi-remodel-index.md:L87; backend/MicroLIMS.Application/Services/Kpi/AnalystWorkloadService.cs:L124-L126, L139-L145].

### MOD-KPI-002 Analyst availability and KPI section settings
- BR-KPI-017 Non-Overlapping Analyst Availability Windows. IMPLEMENTED (verified). Availability adjustments (`AnalystAvailability`) for a given analyst cannot overlap in date ranges. If an overlapping window exists, the creation or update is rejected (`EnsureNoOverlapAsync`) [src: backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs:L139-L143].
- BR-KPI-018 Working Weekday Bitmask and Default Capacity Assumptions. IMPLEMENTED (verified). Working weekdays are stored as an integer bitmask where Bit 0 is Monday and Bit 6 is Sunday (Mon-Fri = 31 = `0b0011111`). When no section setting exists, defaults are: `HoursPerAnalystDay = 6.0`, `WorkingWeekdays = 31`, `LoadHorizonDays = 5` [src: backend/MicroLIMS.Application/Services/Kpi/CapacityCalculator.cs:L7-L20].

### MOD-DC-001 Document Control: library and document lifecycle
- BR-DC-001 Document Master Uniqueness and MicroLIMS ID Generation. IMPLEMENTED (verified). Company Document Code must be unique among all Active documents (case-insensitive). MicroLIMS ID is generated from database sequence `document_number_seq` using format `{Prefix}{seqVal:NumberFormat}` (default `DOC-0000000`). Once allocated, a MicroLIMS ID is permanent and never reissued even if the document master is voided [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L89-L100].
- BR-DC-002 Initial Document Registration Revision. IMPLEMENTED (verified). Registering a new Document Master automatically creates initial Revision 1 in status `Draft` with `RevisionSequence = 1`, `RevisionType = Major`, `ReasonForRevision = "Initial Document Registration"`, and initial revision number (default "01"). It links keywords and initial personnel assignments atomically [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L124-L175].
- BR-DC-003 Revision Number Incrementation Algorithm. IMPLEMENTED (verified). The proposed next revision number is derived strictly from the currently effective revision:
  - Major revision: increments the integer prefix before any dot, returning two digits if <100 (e.g. `01` -> `02`, `02.1` -> `03`).
  - Minor revision: appends or increments the integer suffix after the dot (e.g. `01` -> `01.1`, `01.1` -> `01.2`).
  - Manual override: SectionHead (Document Controller) may override the proposed number with custom alphanumeric text matching `^[a-zA-Z0-9\.\-_]{1,20}$`. System Administrator is refused [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentRevisionService.cs:L95-L121, L552-L585].
- BR-DC-004 Single Active Workflow Invariant. IMPLEMENTED (verified). A Document Master cannot have more than one revision in an active workflow (`Draft`, `InReview`, `AwaitingApproval`) simultaneously. Creating a new revision is blocked while any active workflow revision exists [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentRevisionService.cs:L78-L86].
- BR-DC-005 File Attachment and Mutability Rules. IMPLEMENTED (verified). Files (Controlled PDF or Source Word document) can only be uploaded or replaced while the revision is in `Draft` status. Uploading a replacement file deactivates the previous file (`IsActive = false`), increments `FileVersion`, and sets `SupersededByFileId`. File sizes are capped at 50 MB (`MaxFileSizeBytes = 50 * 1024 * 1024`). Formats are validated against magic bytes: PDF must start with `%PDF-` (`0x25, 0x50, 0x44, 0x46, 0x2D`); Word must match `.docx` (PK ZIP header `0x50, 0x4B, 0x03, 0x04`) or `.doc` (OLE Compound header `0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1`) [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs:L22, L56-L96, L129-L133, L148-L177, L425-L450].
- BR-DC-006 Cryptographic File Integrity Verification. IMPLEMENTED (verified). Every uploaded file is hashed using SHA-256 upon storage. On every retrieval, view, or download, the SHA-256 hash of the stored bytes is recalculated and compared with `ContentSha256`. If hashes do not match, delivery is blocked, security audit event `FileIntegrityVerificationFailed` is recorded, and `InvalidOperationException` is thrown [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs:L369-L389].
- BR-DC-007 Controlled Copy Policy. IMPLEMENTED (verified). If administrative setting `DocumentControl.ControlledCopy.DownloadPermitted` is set to "false", file download is blocked for non-privileged users; SystemAdministrator and SectionHead bypass this restriction [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs:L316-L333].
- BR-DC-008 Technical Review Submission Preconditions. IMPLEMENTED (verified). Submitting a revision for technical review requires: an active Source Word file, completed multi-category Revision Impact Assessment (`IsComplete == true`), all originating review findings from prior cycles addressed (`Status == Addressed`), and no active review tasks in flight. Submission assigns a sequential review cycle number and transitions revision status to `InReview` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L70-L140].
- BR-DC-009 Finding Resolution Ladder. IMPLEMENTED (verified). Review findings must follow a 4-step ladder: `Open` -> `AuthorResponded` -> `ReviewerVerified` -> `Resolved`. The author cannot close or resolve a finding without reviewer verification. Mandatory findings must reach `Resolved` across all review cycles before technical review can be completed [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L477-L509, L567-L580].
- BR-DC-010 Technical Review Decisions. IMPLEMENTED (verified).
  - `CompleteReview`: advances revision status from `InReview` to `AwaitingApproval`. Blocked if any unresolved mandatory findings remain across any cycle.
  - `ReturnForCorrection`: reverts revision status from `InReview` to `Draft` so author can make corrections and upload replacement files [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L565-L649].
- BR-DC-011 21 CFR Part 11 Electronic Signature Approval Ceremony. IMPLEMENTED (verified). Approving a document revision strictly requires authenticated password re-entry verified via BCrypt with 5-attempt/15-minute throttle. Signature records immutable snapshot of signer full name, username, role, meaning (`SignatureMeaning.Approved`), UTC timestamp, IP address, and comments. Atomically:
  - If effective date is today or past: revision status transitions to `Effective`, prior effective revision transitions to `Superseded`, and master `CurrentEffectiveRevisionId` updates.
  - If effective date is in the future: revision status transitions to `FutureEffective` (activated automatically on target date by background sweep) [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L526-L602; backend/MicroLIMS.Application/Services/ElectronicSignatureService.cs:L40-L97].
- BR-DC-012 Void vs Obsolescence Rules. IMPLEMENTED (verified).
  - Void Document Master: Permitted ONLY if the master has never held an effective revision. Voiding marks master as `Void`, preserves audit trail, releases Company Document Code for reuse, and is restricted strictly to SectionHead.
  - Obsolescence: Once a master has held an effective revision, it cannot be voided; it must be retired through obsolescence via formal approval.
  - Cancel Draft Revision: Permitted only while revision is in `Draft` status. Cancelling deactivates active draft files and marks revision `Cancelled` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L528-L534, L584-L599].

### MOD-DC-002 Document Control: reading and training
- BR-DC-013 Automated Training Cascade. IMPLEMENTED (verified). Activation of an effective revision triggers `ProcessEffectiveRevisionCascadeAsync`. Open assignments on superseded revisions are closed as `SupersededIncomplete`. New assignments (`AssignmentType.Reading`) are generated for all active users mapped to the document via active `DocumentRoleCurriculum` items (or prior completers if retraining is configured). Due date is calculated from `EffectiveDate + GracePeriodDays` (default 14 days) [src: backend/MicroLIMS.Application/Services/DocumentControl/TrainingAssignmentService.cs:L51-L134, L198-L245].
- BR-DC-014 Conscious Legal Reading Acknowledgement. IMPLEMENTED (verified). Legal acknowledgement requires conscious, explicit verification of the configured legal statement ("I confirm that I have read, understood, and agree to adhere to the contents of this controlled document revision.") via checkbox confirmation. Reading or scrolling alone does NOT constitute legal acknowledgement; scroll progress is informational only. Acknowledgement freezes statement text and controlled PDF SHA-256 hash onto an immutable `DocumentAcknowledgementRecord` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs:L126-L131, L265-L298].
- BR-DC-015 Training Matrix Qualification States. IMPLEMENTED (verified). Training matrix cross-references active users with active effective documents:
  - `Qualified`: user acknowledged current effective revision.
  - `Pending`: assignment open and within due date.
  - `Overdue`: assignment open and past due date.
  - `TrainedOnSupersededOnly`: user qualified on an earlier revision, but has not yet acknowledged the current effective revision (knowledge gap).
  - `NotAssigned`: not required by curriculum [src: backend/MicroLIMS.Application/Services/DocumentControl/TrainingMatrixService.cs:L122-L188].

### MOD-DC-003 Document Control: audit trail
- BR-DC-016 Audit Trail Retention and Export Cap. IMPLEMENTED (verified). All document actions, file events, reviews, approvals, and security violations are recorded in append-only table `AuditLogs` with child `AuditFieldChanges`. CSV exports via `DocumentAuditService` are capped at 5,000 records (`Take(5000)`). The export operation itself is logged as an attributable audit event `AuditTrailExported` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuditService.cs:L88-L120].

### MOD-DC-004 Document Control: configuration
- BR-DC-017 Document Numbering Scheme. IMPLEMENTED (verified). Document numbering uses sequence `document_number_seq` formatted with prefix (default "DOC-") and format mask (default "0000000"). Editing numbering configuration requires SystemAdministrator and records an audit event [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs:L400-L460].
- BR-DC-018 Document Type Review Cycle Range. IMPLEMENTED (verified). Default review cycle for a document type must be between 1 and 120 months [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs:L65-L66].

### MOD-DC-005 Document escalations (backend)
- BR-DC-019 Three-Tiered Escalation Policy. IMPLEMENTED (verified). Background job evaluates open assignments against thresholds (default: 3 days before due, due date, 1 day after due):
  - Level 1 (`ApproachingDue`): notification sent to assigned user.
  - Level 2 (`Due`): notification sent to assigned user; assignment status automatically advances to `Overdue`.
  - Level 3 (`Overdue`): escalation alert routed to user's manager and QA compliance [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentEscalationService.cs:L118-L168].
- BR-DC-020 Escalation Resolution Attributability. IMPLEMENTED (verified). Resolving an open escalation requires `Documents.TrainingAssign` permission and a mandatory regulatory reason (minimum 10 characters), recorded in `DocumentEscalationRecords` and the audit log [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentEscalationController.cs:L80-L90; backend/MicroLIMS.Application/Services/DocumentControl/DocumentEscalationService.cs:L260-L310].

## validations

### MOD-REPORTS-001 Reports
- Server validations (ReportingController / ReportingQueryService):
  - Export row cap: `"This filter matches {result.TotalCount} rows, which exceeds the 10,000-row export limit. Narrow your filters (e.g. a shorter date range) and try again."` (for ResultRecords) [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L164-L165].
  - Media GPT export row cap: `"This filter matches {result.TotalCount} challenge rows, which exceeds the 10,000-row export limit. Narrow your filters (e.g. a shorter date range) and try again."` [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L252-L253].
  - Reference strain export row cap: `"This filter matches {result.TotalCount} rows, which exceeds the 10,000-row export limit. Narrow your filters and try again."` [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L334-L335].
  - Missing trend testCode: `"testCode is required."` [src: backend/MicroLIMS.Application/Services/ReportingQueryService.cs:L238].
  - Missing trend subjectName: `"subjectName is required."` [src: backend/MicroLIMS.Application/Services/ReportingQueryService.cs:L239].
  - Test code not in master: `"Test code \"{testCode}\" is not configured in Test Master."` [src: backend/MicroLIMS.Application/Services/ReportingQueryService.cs:L242, L442].
  - Qualitative trending rejection: `"Trending is only available for numeric results. {testCode} produces qualitative results."` [src: backend/MicroLIMS.Application/Services/ReportingQueryService.cs:L247].
  - Record not found: `"ResultRecord {id} not found."`, `"Media lot {id} not found."`, `"Cryovial batch {id} not found."` [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L93, L212, L302].
- Client validations:
  - Export failure: `"Export failed."` or parsed backend error message [src: frontend/src/modules/reports/services/ReportingService.ts:L97-L99; frontend/src/modules/reports/components/ReportResultsTable.tsx:L158].
  - Filter options load failure: `"The filter options could not be loaded, so the filter lists are empty. Reload the page to try again."` [src: frontend/src/modules/reports/RecordSearchTab.tsx:L87].
  - Trend image capture failure: `"No chart is currently rendered to capture."`, `"Could not capture the chart image for export."` [src: frontend/src/modules/reports/components/TrendingTab.tsx:L180-L185].

### MOD-REPORTS-002 Reporting backfill
- Server validations (ReportingAdminController / ResultProjectionService):
  - Sample not found: `"Sample {sampleId} not found."` [src: backend/MicroLIMS.Application/Services/ResultProjectionService.cs:L390].
  - Row-level projection errors logged and appended to error list: `"CountTestReading {id}: {message}"`, `"TestOrder {id} (pathogen): {message}"`, `"SampleLocation {id}: {message}"`, `"ParameterResult {id}: {message}"` [src: backend/MicroLIMS.Application/Services/ResultProjectionService.cs:L434, L459, L477, L495].

### MOD-KPI-001 KPI and Workflow Profiles
- Server validations (KpiDashboardService / WorkflowProfileService / AnalystWorkloadService):
  - Date range validation: `"A start date and an end date are required."` [src: backend/MicroLIMS.Application/Services/Kpi/KpiDashboardService.cs:L96].
  - Start date after end date: `"The start date must not be after the end date."` [src: backend/MicroLIMS.Application/Services/Kpi/KpiDashboardService.cs:L98; backend/MicroLIMS.Application/Services/Kpi/AnalystWorkloadService.cs:L48].
  - Unknown KPI key: `"Unknown KPI '{key}'."` [src: backend/MicroLIMS.Application/Services/Kpi/KpiDashboardService.cs:L62].
  - D8 own-figures restriction: `"You can only view your own analyst figures."` [src: backend/MicroLIMS.Application/Services/Kpi/KpiDashboardService.cs:L107].
  - Workflow profile version not found: `"Workflow profile version {id} was not found."` [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L101].
  - Double signing rejection: `"This version is already Effective and cannot be signed again."` [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L103].
  - Missing reason for change: `"A reason for the change is required."` [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L142].
  - Reason too long: `"The reason must be 500 characters or fewer."` [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L145].
  - Neither units nor target entered: `"Enter work units, a target, or both."` [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L148].
  - Non-positive value: `"{label} must be greater than 0."` [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L271].
  - Exceeds maximum: `"{label} must not exceed {max}."` (Work units max 999999.99, Target analyst days max 9999.99) [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L29-L30, L272].
  - Decimal places limit: `"{label} allows at most 2 decimal places."` [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L274].
  - Test code not in master: `"Test '{code}' does not exist in the test master."` [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L157].
  - Retired workflow type: `"Workflow type {type} is retired and cannot carry a profile."` [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L162].
  - Past effective date rejection: `"The effective date cannot be in the past ({effectiveFrom:yyyy-MM-dd}). Only the first version of a profile may be back-dated; add this version with an effective date of {today:yyyy-MM-dd} or later."` [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L289-L290].
- Client validations:
  - Calculation failure alert: `"This figure could not be calculated. The error has been logged."` [src: backend/MicroLIMS.Application/Services/Kpi/KpiDashboardService.cs:L13, L51].
  - Date format check: regex `/^\d{4}-\d{2}-\d{2}$/` [src: frontend/src/modules/kpi/components/AddProfileVersionDialog.tsx:L48].
  - Filter options load failure: `"The category, location and test lists could not be loaded. Reload the page to try again."` [src: frontend/src/modules/kpi/components/KpiFilters.tsx:L98].

### MOD-KPI-002 Analyst availability and KPI section settings
- Server validations (KpiSettingsService / AnalystAvailabilityService):
  - Settings reason required: `"A reason for the change is required."` [src: backend/MicroLIMS.Application/Services/Kpi/KpiSettingsService.cs:L45].
  - Settings reason length: `"The reason must be 500 characters or fewer."` [src: backend/MicroLIMS.Application/Services/Kpi/KpiSettingsService.cs:L46].
  - Hours per day range: `"Hours per analyst day must be greater than 0 and at most 24, with at most two decimals."` [src: backend/MicroLIMS.Application/Services/Kpi/KpiSettingsService.cs:L48].
  - Working weekdays mask: `"Select at least one working weekday."` [src: backend/MicroLIMS.Application/Services/Kpi/KpiSettingsService.cs:L50].
  - Load horizon range: `"The load horizon must be between 1 and 30 working days."` [src: backend/MicroLIMS.Application/Services/Kpi/KpiSettingsService.cs:L52].
  - Section not found: `"Laboratory section not found."` [src: backend/MicroLIMS.Application/Services/Kpi/KpiSettingsService.cs:L90].
  - Section unauthorized: `"You do not have access to this laboratory section."` [src: backend/MicroLIMS.Application/Services/Kpi/KpiSettingsService.cs:L93].
  - Availability reason required: `"A reason is required."` [src: backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs:L122].
  - Availability dates reversed: `"The end date must not be before the start date."` [src: backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs:L124].
  - Fraction available range: `"Fraction available must be between 0 and 1 with at most two decimals."` [src: backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs:L126].
  - Analyst not found: `"The analyst does not exist."` [src: backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs:L133].
  - Analyst outside section: `"You may only record availability for analysts in your laboratory sections."` [src: backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs:L136].
  - Overlapping period rejection: `"This period overlaps an existing availability entry for the analyst. Correct that entry instead."` [src: backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs:L142].
  - Reassigning entry to another analyst: `"An entry cannot be moved to another analyst; correct the dates or enter a new entry."` [src: backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs:L95].
  - Viewing other analyst's availability without team permission: `"You may only view your own availability."` [src: backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs:L38].
  - Target analyst outside section: `"That analyst is outside your laboratory sections."` [src: backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs:L42].

### MOD-DC-001 Document Control: library and document lifecycle
- Server validations (DocumentMasterService / DocumentRevisionService / DocumentFileService / DocumentReviewService / DocumentApprovalService / PeriodicReviewService):
  - Registration missing code: `"Company Document Code is required."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L45].
  - Registration missing title: `"Document Title is required."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L48].
  - Registration missing type: `"A valid Document Type is required."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L51].
  - Registration missing department: `"A valid Department is required."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L54].
  - Registration missing section: `"A valid Section is required."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L57].
  - Registration missing owner: `"A valid Document Owner is required."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L60].
  - Section department mismatch: `"Section '{section.Name}' does not belong to department '{dept.Name}'."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L81].
  - Duplicate code: `"A document with Company Document Code '{trimmedCode}' already exists and is active."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L93].
  - Registration unauthorized: `"Only the Document Controller or a System Administrator can register a Document Master."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L41].
  - Void reason length: `"A mandatory void reason of at least 10 characters is required."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L521].
  - Voiding effective master: `"A Document Master that has held an effective revision cannot be voided. It must be retired through obsolescence."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L533].
  - Voiding already voided: `"Document Master is already voided."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L529].
  - Void unauthorized: `"Only the Document Controller can void a Document Master."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L518].
  - Cancel reason length: `"A mandatory cancellation reason of at least 10 characters is required."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L575].
  - Cancel non-draft: `"Only revisions in Draft state can be cancelled. Current status is {revision.RevisionStatus}."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L585].
  - Cancel unauthorized: `"You do not have permission to cancel this draft revision."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L572].
  - Revision reason length: `"Reason for Revision is mandatory and must contain at least 10 characters."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentRevisionService.cs:L89].
  - Revision summary length: `"Change Summary, if provided, must contain at least 10 characters."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentRevisionService.cs:L92].
  - Revision voided master: `"Cannot create a revision for a voided document."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentRevisionService.cs:L69].
  - Revision without effective: `"Cannot create a new revision from an effective document because this document has no effective revision."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentRevisionService.cs:L76].
  - Revision concurrent workflow: `"Cannot create a new revision while another revision is currently in draft or review/approval workflow."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentRevisionService.cs:L85].
  - Revision number duplicate: `"Revision number '{finalRevisionNumber}' already exists for this document."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentRevisionService.cs:L129].
  - Revision number override format: `"Custom revision number must be 1-20 alphanumeric characters, dots, hyphens, or underscores."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentRevisionService.cs:L117].
  - Revision number override unauthorized: `"Only Document Controllers are authorized to override the proposed revision number."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentRevisionService.cs:L111].
  - Empty file: `"File content cannot be empty."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs:L57].
  - File size limit: `"File size exceeds the maximum permitted limit of 50 MB."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs:L60].
  - File format PDF: `"Controlled document file must be a PDF (.pdf)."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs:L73].
  - File magic bytes PDF: `"Invalid file format. The uploaded file is not a valid PDF document."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs:L79].
  - File format Word: `"Source document file must be a Word document (.docx or .doc)."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs:L87].
  - File magic bytes Word: `"Invalid file format. The uploaded file is not a valid Word document."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs:L92].
  - File upload non-draft: `"Files can only be added or replaced on revisions in Draft state. Current status is {revision.RevisionStatus}."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs:L132].
  - File upload unauthorized: `"You do not have permission to upload or replace files for this revision."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs:L121].
  - Controlled copy policy: `"Controlled copy download is currently disabled by administrative policy."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs:L331].
  - Integrity hash mismatch: `"Document integrity check failed. The file may have been altered or corrupted. Delivery has been blocked."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs:L388].
  - Review missing impact assessment: `"A multi-category Revision Impact Assessment must be completed before submitting for technical review."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L83].
  - Review unaddressed originating findings: `"Cannot submit revision for review: {unaddressedOriginatingItems.Count} originating review finding(s) remain unaddressed."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L96].
  - Review active task exists: `"A technical review task is already active for this revision."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L120].
  - Review completion unresolved mandatory findings: `"Cannot complete technical review while {unresolvedMandatoryFindings.Count} mandatory review finding(s) remain unresolved."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L579].
  - Finding resolution ladder violation: `"A review finding can only be resolved once it has been verified by the reviewer. Current status is {finding.Status}; it must be ReviewerVerified (the author responds, the reviewer verifies, then the finding is resolved)."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L506-L508].
  - Approval task technical review incomplete: `"Cannot create an approval task: Revision has not completed technical review."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L81].
  - Approval task already pending: `"An active approval task (ID: {existingPending.Id}) is already pending for this revision."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L89].
  - Approver role eligibility: `"User '{approver.FullName}' with role '{approver.Role?.Name ?? "None"}' is not eligible to be assigned as an approver."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L111].
  - Return justification length: `"A mandatory justification of at least 10 characters is required when returning a revision for correction."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L441].
  - Decline justification length: `"A mandatory justification of at least 10 characters is required when declining a revision."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L483].
  - Approval missing password: `"Password re-authentication is required to apply an electronic signature for approval (21 CFR Part 11)."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L530].
  - Approval duplicate signature: `"An electronic signature for approval has already been applied to revision {revision.RevisionNumber}."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L546].
  - Periodic review already completed: `"This periodic review task is already completed and cannot be altered."` [src: backend/MicroLIMS.Application/Services/DocumentControl/PeriodicReviewService.cs:L542].
  - Periodic review cancelled: `"Cannot complete a cancelled periodic review task."` [src: backend/MicroLIMS.Application/Services/DocumentControl/PeriodicReviewService.cs:L545].
  - Periodic review summary length: `"A comprehensive review summary of at least 10 characters is mandatory."` [src: backend/MicroLIMS.Application/Services/DocumentControl/PeriodicReviewService.cs:L566].
- Client validations:
  - Viewer integrity error: `"CRITICAL INTEGRITY FAILURE: The document cryptographic SHA-256 signature does not match the stored file content. Delivery has been blocked."` [src: frontend/src/modules/documentControl/components/ControlledPdfViewer.tsx:L104].
  - Signature missing password: `"Password re-authentication is required to apply electronic signature."` [src: frontend/src/modules/documentControl/components/ElectronicSignatureDialog.tsx:L56].
  - Approval return/decline length: `"A mandatory justification of at least 10 characters is required for return or decline."` [src: frontend/src/modules/documentControl/components/ApprovalWorkspaceDialog.tsx:L121].

### MOD-DC-002 Document Control: reading and training
- Server validations (DocumentAcknowledgementService):
  - Missing conscious checkbox: `"Explicit confirmation of the legal acknowledgement statement is required. Reading or viewing alone does not constitute legal acknowledgement."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs:L130].
  - User mismatch: `"You are not authorized to acknowledge an assignment assigned to another user."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs:L167].
  - Revision mismatch: `"Revision mismatch: Assignment is for Revision ID {assignment.DocumentRevisionId}, but Revision ID {request.DocumentRevisionId} was submitted."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs:L185].
  - Non-active document: `"Cannot acknowledge assignment because Document {master.CompanyDocumentCode} is in {master.RecordStatus} status."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs:L192].
  - Non-effective revision: `"Cannot acknowledge assignment because Revision {revision.RevisionNumber} is in {revision.RevisionStatus} state (must be Effective)."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs:L198].
  - Superseded incomplete terminal: `"Cannot acknowledge an assignment that has been terminally closed as SupersededIncomplete."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs:L243].
  - Cancelled terminal: `"Cannot acknowledge an assignment that has been terminally closed as Cancelled due to document obsolescence."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs:L249].
  - Invalid assignment state: `"Assignment is in an invalid state for acknowledgement: {assignment.Status}."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs:L258].
- Client validations:
  - Missing checkbox confirmation: `"You must explicitly verify and accept the legal acknowledgement statement."` [src: frontend/src/modules/documentControl/components/DocumentAcknowledgementDialog.tsx:L51].

### MOD-DC-003 Document Control: audit trail
- Server validations (DocumentAuditService):
  - Global query unauthorized: `"You do not have permission to query the global audit trail."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuditService.cs:L35].
  - Record query unauthorized: `"You do not have permission to view audit history for this document."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuditService.cs:L64].
  - Export unauthorized: `"You do not have permission to export the audit trail."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuditService.cs:L92].

### MOD-DC-004 Document Control: configuration
- Server validations (DocumentConfigurationService):
  - Non-admin access: `"Only System Administrators can manage module configuration."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs:L35].
  - Missing type code: `"Document Type Code is required."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs:L60].
  - Missing type name: `"Document Type Name is required."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs:L63].
  - Review cycle range: `"Default review cycle must be between 1 and 120 months."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs:L66].
  - Duplicate type code: `"Document Type Code '{trimmedCode}' already exists."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs:L70].
  - Missing dept code: `"Department Code is required."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs:L127].
  - Missing dept name: `"Department Name is required."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs:L130].
  - Duplicate dept code: `"Department Code '{trimmedCode}' already exists."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs:L137].
  - Missing section name: `"Section Name is required."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs:L226].
  - Missing prefix: `"Prefix is required."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs:L415].
  - Missing number format: `"Number format is required."` [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs:L418].

### MOD-DC-005 Document escalations (backend)
- Server validations (DocumentEscalationController):
  - View summary unauthorized: `"Only Section Heads and System Administrators can view the overdue training summary."` [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentEscalationController.cs:L53].
  - Trigger evaluation unauthorized: `"Only Section Heads and System Administrators can manually trigger escalation processing."` [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentEscalationController.cs:L68].
  - Resolve escalation unauthorized: `"Only Section Heads and System Administrators can resolve escalation records."` [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentEscalationController.cs:L85].

## statuses

### MOD-REPORTS-001 Reports
- Result Level (`ResultLevel`):
  - `WithinLimit` (UI: "Within Limit", green tone) [src: frontend/src/modules/reports/components/ReportFilterPanel.tsx:L10; frontend/src/modules/reports/utils/exportPdf.ts:L42].
  - `AlertLevel` (UI: "Alert Level", amber tone) [src: frontend/src/modules/reports/components/ReportFilterPanel.tsx:L11; frontend/src/modules/reports/utils/exportPdf.ts:L43].
  - `ActionLevel` (UI: "Action Level", orange tone) [src: frontend/src/modules/reports/components/ReportFilterPanel.tsx:L12; frontend/src/modules/reports/utils/exportPdf.ts:L44].
  - `OutOfSpecification` (UI: "Out of Spec", red error tone) [src: frontend/src/modules/reports/components/ReportFilterPanel.tsx:L13; frontend/src/modules/reports/utils/exportPdf.ts:L45].
  - `NotApplicable` (UI: "—") [src: frontend/src/modules/reports/components/ReportResultsTable.tsx:L280].
  - `LimitsNotConfigured` (UI: "—" or unconfigured limits) [src: backend/MicroLIMS.Application/Services/ResultProjectionService.cs:L107].
- Approval Status (derived from SampleStatus):
  - "Approved" (green chip): Sample has been formally approved [src: frontend/src/modules/reports/components/ReportResultsTable.tsx:L282; backend/MicroLIMS.Application/Services/ReportingQueryService.cs:L111].
  - "Rejected" (red chip): Sample has been rejected [src: backend/MicroLIMS.Application/Services/ReportingQueryService.cs:L112].
  - "Pending" (amber chip): Sample is Received, InTesting, UnderReview, UnderApproval, or RetestRequested [src: backend/MicroLIMS.Application/Services/ReportingQueryService.cs:L115].
  - "Voided": Sample was voided before completion [src: backend/MicroLIMS.Application/Services/ReportingQueryService.cs:L113].
  - "Cancelled": Sample was cancelled [src: backend/MicroLIMS.Application/Services/ReportingQueryService.cs:L114].

### MOD-REPORTS-002 Reporting backfill
- None directly (operates on existing `SampleStatus` and `ResultRecord` values).

### MOD-KPI-001 KPI and Workflow Profiles
- Workflow Profile Status (`WorkflowProfileStatus`):
  - `Draft`: Unsigned draft version (e.g. from legacy migration import or staged insert). Shown as Chip `"{N} draft(s)"` in profile panel [src: backend/MicroLIMS.Domain/Enums/WorkflowProfileStatus.cs:L3; frontend/src/modules/kpi/components/WorkflowProfilesPanel.tsx:L166].
  - `Effective`: Active, electronically signed version currently or future effective. Read by resolver during KPI calculations [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L83, L125].
  - Transition: `Draft` -> `Effective`, triggered by electronic signature with `SignatureMeaning.MasterDataChanged`. Irreversible. [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L80-L84, L116-L126].
- Profile Coverage (`ProfileCoverage`):
  - `Configured` (green Chip: both work units and target days configured) [src: frontend/src/modules/kpi/components/WorkflowProfilesPanel.tsx:L41, L46].
  - `Partial` (amber Chip: either work units or target days configured, but not both) [src: L42, L47].
  - `NeedsConfiguration` (grey/default Chip: neither work units nor target days configured) [src: L43, L48].
- KPI Direction (`KpiDirection`):
  - `HigherIsBetter` (green when higher) [src: backend/MicroLIMS.Application/Services/Kpi/KpiTypes.cs:L5].
  - `LowerIsBetter` (green when lower) [src: backend/MicroLIMS.Application/Services/Kpi/KpiTypes.cs:L5].
  - `Neutral` (no directional valence) [src: backend/MicroLIMS.Application/Services/Kpi/KpiTypes.cs:L5].
- KPI Status (`KpiStatus`):
  - `Ok`: successfully computed with data [src: backend/MicroLIMS.Application/Services/Kpi/KpiTypes.cs:L7].
  - `NoData`: zero matching records [src: backend/MicroLIMS.Application/Services/Kpi/KpiTypes.cs:L7].
  - `Partial`: partial data or gaps present [src: backend/MicroLIMS.Application/Services/Kpi/KpiTypes.cs:L7].

### MOD-KPI-002 Analyst availability and KPI section settings
- Section setting active status: `IsDefault` boolean (true if synthesized from defaults, false if row exists) [src: backend/MicroLIMS.Application/Services/Kpi/KpiSettingsService.cs:L37, L97].

### MOD-DC-001 Document Control: library and document lifecycle
- Document Master Record Status (`DocumentRecordStatus`):
  - `Active` (1): Master record is active and valid [src: backend/MicroLIMS.Domain/Enums/DocumentRecordStatus.cs:L5].
  - `Void` (2): Master record was registered in error and voided prior to having an effective revision [src: L6].
- Document Revision Status (`DocumentRevisionStatus`):
  - `Draft` (1): Initial draft or revision returned for corrections; files and metadata editable [src: backend/MicroLIMS.Domain/Enums/DocumentRevisionStatus.cs:L5].
  - `Cancelled` (2): Draft revision cancelled by author/controller or declined by approver; terminal inactive state [src: L6].
  - `InReview` (3): Submitted for technical review; open review task active, files locked [src: L7].
  - `AwaitingApproval` (4): Technical review completed; awaiting formal approver evaluation [src: L8].
  - `FutureEffective` (5): Formally approved with future target effective date; awaits automated activation sweep [src: L9].
  - `Effective` (6): Active, legally approved, current version; reading training active, controlled PDF accessible to all readers [src: L10].
  - `Superseded` (7): Historical revision replaced by a newly activated effective revision [src: L11].
  - `Obsolete` (8): Formally decommissioned document; retired via approved obsolescence task [src: L12].
- Review Finding Status (`ReviewFindingStatus`):
  - `Open` (1): Logged by technical reviewer [src: backend/MicroLIMS.Domain/Enums/ReviewFindingStatus.cs:L5].
  - `AuthorResponded` (2): Author submitted formal response to comment [src: L6].
  - `ReviewerVerified` (3): Reviewer verified author's response [src: L7].
  - `Resolved` (4): Closed finding; required state for mandatory findings before review completion [src: L8].
- Document Approval Task Status (`DocumentApprovalTaskStatus`):
  - `Pending` (1): Awaiting designated approver evaluation and signature [src: backend/MicroLIMS.Domain/Enums/DocumentApprovalTaskStatus.cs:L5].
  - `Approved` (2): Formally approved via electronic signature [src: L6].
  - `ReturnedForCorrection` (3): Returned by approver; revision reverts to Draft [src: L7].
  - `Declined` (4): Rejected by approver; revision transitions to Cancelled [src: L8].
  - `Cancelled` (5): Cancelled administratively [src: L9].
- Periodic Review Task Status (`PeriodicReviewTaskStatus`):
  - `Pending` (1): Scheduled or triggered, awaiting reviewer evaluation [src: backend/MicroLIMS.Domain/Enums/PeriodicReviewTaskStatus.cs:L5].
  - `InProgress` (2): Reviewer active on task [src: L6].
  - `Completed` (3): Review finalized with formal outcome [src: L7].
  - `Cancelled` (4): Cancelled administratively [src: L8].
- Periodic Review Outcome (`PeriodicReviewOutcome`):
  - `RemainsValid` (1): Document is current; advances `NextReviewDate` [src: backend/MicroLIMS.Domain/Enums/PeriodicReviewOutcome.cs:L5].
  - `RevisionRequired` (2): Document needs updates; findings transferred to next revision draft [src: L6].
  - `ObsolescenceRecommended` (3): Decommissioning recommended; creates obsolescence approval task [src: L7].

Document Revision Lifecycle State Transitions:
| From | To | Action / Trigger | Actor | Signed? |
|---|---|---|---|---|
| None | Draft | Register Document Master | Documents.Register (SysAdmin/SectionHead/Analyst) | No |
| Draft | InReview | Submit for Technical Review | Owner / Author / SectionHead / SysAdmin | No |
| InReview | Draft | Return for Correction | Assigned Reviewer or SectionHead | No |
| InReview | AwaitingApproval | Complete Technical Review | Assigned Reviewer or SectionHead | No |
| AwaitingApproval | Draft | Return for Correction | Assigned Approver or SectionHead | No |
| AwaitingApproval | Cancelled | Decline Revision | Assigned Approver or SectionHead | No |
| AwaitingApproval | FutureEffective | Approve Revision (future date) | Assigned Approver or SectionHead | Yes (`SignatureMeaning.Approved`, password) |
| AwaitingApproval | Effective | Approve Revision (today/past date) | Assigned Approver or SectionHead | Yes (`SignatureMeaning.Approved`, password) |
| FutureEffective | Effective | DocumentEffectiveDateWorker sweep | System Process | No |
| Effective | Superseded | Newer revision approved & effective | System / Transaction | No |
| Effective | Obsolete | Obsolescence approval task approved | Approver or SectionHead | Yes (`SignatureMeaning.Approved`, password) |
| Active (Master) | Void | Void Document Master | SectionHead only (Document Controller) | No (reason >= 10 chars) |
| Draft | Cancelled | Cancel Draft Revision | Owner / Author / SectionHead / SysAdmin | No (reason >= 10 chars) |
| Effective | Draft (next rev) | Create Revision from Effective | Owner / Author / SectionHead / SysAdmin | No |

### MOD-DC-002 Document Control: reading and training
- Training Assignment Status (`TrainingAssignmentStatus`):
  - `Assigned` (1): Created upon revision activation or role curriculum assignment [src: backend/MicroLIMS.Domain/Enums/TrainingAssignmentStatus.cs:L5].
  - `Reading` (2): User opened controlled PDF viewer [src: L6].
  - `Acknowledged` (3): User completed conscious legal reading acknowledgement [src: L7].
  - `Overdue` (4): Past UTC due date without acknowledgement [src: L8].
  - `SupersededIncomplete` (5): Closed terminally when a newer revision became effective before completion [src: L9].
  - `Cancelled` (6): Cancelled due to document obsolescence [src: L10].
- Training Matrix Cell Status:
  - `Qualified`: current revision acknowledged [src: backend/MicroLIMS.Application/Services/DocumentControl/TrainingMatrixService.cs:L149].
  - `Pending`: assignment open and within due date [src: L165].
  - `Overdue`: assignment open and past due date [src: L159].
  - `TrainedOnSupersededOnly`: user qualified on earlier revision, not current [src: L154, L174].
  - `NotAssigned`: not required by curriculum [src: L123].

### MOD-DC-003 Document Control: audit trail
- Audit Action Categories (`AuditActionCategory`): Document, Review, Approval, PeriodicReview, Configuration, Security [src: backend/MicroLIMS.Domain/Entities/AuditLog.cs:L1-L30; frontend/src/modules/documentControl/types/documentControlTypes.ts:L15-L25].

### MOD-DC-004 Document Control: configuration
- Statuses: `IsActive` boolean on DocumentType, DocumentDepartment, DocumentSection; `IsEnabled` boolean on DocumentNumberingConfiguration [src: backend/MicroLIMS.Domain/Entities/DocumentType.cs; backend/MicroLIMS.Domain/Entities/DocumentNumberingConfiguration.cs].

### MOD-DC-005 Document escalations (backend)
- Document Escalation Levels (`DocumentEscalationLevel`):
  - `ApproachingDue` (1): T - daysBefore due date [src: backend/MicroLIMS.Domain/Enums/DocumentEscalationLevel.cs:L5].
  - `Due` (2): On due date [src: L6].
  - `Overdue` (3): T + daysAfter due date [src: L7].
- Document Escalation Status (`DocumentEscalationStatus`):
  - `Open` (1): Active escalation requiring supervisory attention [src: backend/MicroLIMS.Domain/Enums/DocumentEscalationStatus.cs:L5].
  - `Resolved` (2): Resolved with attributable regulatory justification [src: L6].

## terminology

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
- Adverse Trend: Flag raised on Environmental Monitoring excursion rate when at least 5 results exist and alert rate > 2.5% or action rate > 0.15%. [src: backend/MicroLIMS.Application/Services/Kpi/KpiConstants.cs:L44-L45; backend/MicroLIMS.Application/Services/Kpi/EmExcursionRateKpi.cs:L68-L70].
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
- Controlled PDF: The official released document file in PDF format (`.pdf`), presented in the controlled viewer with security watermarks. [src: frontend/src/modules/documentControl/components/ControlledPdfViewer.tsx:L150-L153].
- Source File: The editable authoring document in Microsoft Word format (`.docx` or `.doc`), restricted to authors, reviewers, and approvers during workflow. [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs:L84-L96].
- Technical Review: Peer review phase where assigned subject matter experts verify content accuracy and log findings across numbered cycles. [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L15-L65].
- Finding Resolution Ladder: Mandatory progressive workflow (`Open` -> `AuthorResponded` -> `ReviewerVerified` -> `Resolved`) governing reviewer comments. [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L497-L508].
- 21 CFR Part 11 Electronic Signature: Digital signing ceremony requiring user password re-authentication with BCrypt verification to execute formal release. [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L526-L565].
- Periodic Review: Formal recurring review (e.g. every 24 months) to ensure an effective SOP remains scientifically and operationally valid. [src: backend/MicroLIMS.Application/Services/DocumentControl/PeriodicReviewService.cs:L20-L55].
- Void: Permanent invalidation of a Document Master registered in error that never reached effective release, releasing its code for reuse. [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L528-L534].

### MOD-DC-002 Document Control: reading and training
- Evidentiary Statement: The legally binding statement displayed at reading acknowledgement ("I confirm that I have read, understood, and agree to adhere to the contents of this controlled document revision.") frozen into the compliance record. [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs:L23-L25].
- Reading Progress: Informational client-side viewport scroll percentage, explicitly marked "Informational Only. Formal Acknowledgement Required". [src: frontend/src/modules/documentControl/components/ControlledPdfViewer.tsx:L194; frontend/src/modules/documentControl/components/DocumentAcknowledgementDialog.tsx:L180].
- Training Matrix: A two-dimensional grid cross-referencing organizational personnel against active controlled documents with color-coded compliance states. [src: frontend/src/modules/documentControl/pages/TrainingMatrixPage.tsx:L59-L816].
- Superseded Incomplete: Terminal status for a training assignment that was not completed before a newer revision became effective. [src: backend/MicroLIMS.Application/Services/DocumentControl/TrainingAssignmentService.cs:L106-L108].
- Superseded Knowledge Gap: A training status where a user was qualified on an earlier revision of a document but has not yet acknowledged the currently effective revision. [src: backend/MicroLIMS.Application/Services/DocumentControl/TrainingMatrixService.cs:L154, L174].

### MOD-DC-003 Document Control: audit trail
- Audit Field Change: Granular capture of previous value and new value for every modified property in a document control entity. [src: backend/MicroLIMS.Domain/Entities/AuditFieldChange.cs].

### MOD-DC-004 Document Control: configuration
- Review Cycle Months: Configurable periodic review frequency in months defined per Document Type (1 to 120 months). [src: backend/MicroLIMS.Domain/Entities/DocumentType.cs].

### MOD-DC-005 Document escalations (backend)
- Escalation Level: Milestone tier indicating assignment urgency (ApproachingDue, Due, Overdue). [src: backend/MicroLIMS.Domain/Enums/DocumentEscalationLevel.cs].

## reports

### MOD-REPORTS-001 Reports
- Result Records CSV Export: Downloaded from Record Search tab (`GET /api/reporting/results/export`); filename `microlims-results-{yyyyMMdd-HHmmss}.csv`. Contains 19 columns: Date/Time, Reference, Subject, Subject Detail, Category, Test Code, Test Name, Reported Value, Unit, Result Level, Alert Limit, Action Limit, Spec Limit, Sample Status, Entered By, Entered At, Approved By, Approved At, Round. Capped at 10,000 rows [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L177, L351-L388].
- Result Records PDF Export: Client-side generated printable PDF (`exportResultsPdf`) from Record Search tab; renders A4 landscape report with header metadata (Scope, Generated By, Calculated At), table of results with styled status badges, and footer notice ("Laboratory Results Export. Generated for analytical and reporting review. Not a formal Certificate of Analysis.") [src: frontend/src/modules/reports/utils/exportPdf.ts:L57-L276].
- Media / GPT CSV Export: Downloaded from Media / GPT tab (`GET /api/reporting/media-gpt/export`); filename `microlims-media-gpt-{yyyyMMdd-HHmmss}.csv`. Contains 30 columns: Lot Number, Media Type, Prepared Date, Expiry Date, Approval Status, Released For Use, Prepared By, Approved By, Approved At, Evaluation Type, Evaluation Status, Evaluation Outcome, Evaluation Completed At, Organism, ATCC Number, Role, Strain Source, Initial Inoculum, Reference Lot, Old Count, New Count, Recovery %, Expected Recovery Range, Growth Observed, Observed Description, Expected Description, Turbid, Challenge Outcome, Read By, Read At. Capped at 10,000 challenge rows [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L265, L390-L440].
- Media / GPT PDF Export: Client-side generated printable PDF (`exportMediaGptPdf`) of media preparation lots and growth promotion outcomes [src: frontend/src/modules/reports/components/MediaGptResultsTable.tsx:L105-L116].
- Reference Strains CSV Export: Downloaded from Reference Strains tab (`GET /api/reporting/reference-strains/export`); filename `microlims-reference-strains-{yyyyMMdd-HHmmss}.csv`. Contains 21 columns: Strain Name, ATCC Number, Cryovial Code, Manufacturer, Source Material, Source Batch Number, Receipt Date, Prepared Date, Expiry Date, Vials Prepared, Vials Remaining, Storage Condition, Approval Status, Destroyed, Prepared By, Approved By, Approved At, Identity Confirmations Count, Thaw Events Count, Direct GPT Usage Count, Indirect Test Orders Qualified. Capped at 10,000 rows [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L347, L442-L483].
- Reference Strains PDF Export: Client-side generated printable PDF (`exportReferenceStrainPdf`) of working culture batches [src: frontend/src/modules/reports/components/ReferenceStrainResultsTable.tsx:L105-L116].
- Trending CSV Export: Downloaded via `buildTrendCsv` / `downloadCsv`; outputs Period, Reference, Reported Value, Numeric Value, Result Level, Mean, Upper Limit, Alert Level, Action Level (for numeric) or Period, Detected Count, Absent Count, Total Samples (for qualitative) [src: frontend/src/modules/reports/utils/exportTrend.ts:L21-L42].
- Trending PDF Export: Client-side generated printable PDF with options for "PDF (Data Table)" and "PDF (Data Table + Chart)" (captures SVG chart as embedded PNG) [src: frontend/src/modules/reports/components/TrendingTab.tsx:L178-L186; frontend/src/modules/reports/utils/exportTrend.ts:L44-L140].

### MOD-REPORTS-002 Reporting backfill
- None (data engine maintenance only).

### MOD-KPI-001 KPI and Workflow Profiles
- KPI & Performance PDF Report: Generated via `exportKpiPdf` from button "Export PDF" on KPI tab. Produces A4 landscape document with:
  - Header: title "MicroLIMS KPI & Performance Report", metadata (Scope, Generated By, Calculated timestamp).
  - Headline measures table: Measure, Value, n, Prior period, Notes.
  - Supporting breakdown tables: series breakdown for Flow (`stage-tat`, `backlog-aging`), Quality (`right-first-time`, `retest-rate`, `oos-rate`, `returns-by-test`), and Micro (`em-excursion-rate`, `incubation-reads-on-time`).
  - Workload table: Analyst, Capacity (units), Delivered units, Utilisation, Open units, Forward load, Weighted on time, Weighted right first time, with planning notes and gap warnings.
  - Footer: "Figures are as shown on screen for the same period and filters. GMP audit trail and data-integrity standard. MicroLIMS verified report." [src: frontend/src/modules/kpi/utils/exportKpiPdf.ts:L86-L167].

### MOD-KPI-002 Analyst availability and KPI section settings
- None directly.

### MOD-DC-001 Document Control: library and document lifecycle
- Controlled PDF Document Presentation: Controlled PDF copy rendered in `ControlledPdfViewer` with "OFFICIAL COPY" chip, document code, MicroLIMS ID, revision number, status badge, and historical revision warning chip ("HISTORICAL REVISION, NOT CURRENT EFFECTIVE") if superseded or obsolete. Supports client-side downloading if permitted by policy [src: frontend/src/modules/documentControl/components/ControlledPdfViewer.tsx:L140-L200].
- Dual PDF Comparison Inspection: Side-by-side inspection view in `DualPdfComparisonViewer` rendering the currently effective controlled PDF alongside the proposed draft revision PDF [src: frontend/src/modules/documentControl/components/DualPdfComparisonViewer.tsx:L1-L100].

### MOD-DC-002 Document Control: reading and training
- Training Matrix Grid View: Visual cross-reference grid of all tracked active users against effective controlled documents, filterable by department and compliance status [src: frontend/src/modules/documentControl/pages/TrainingMatrixPage.tsx:L280-L380].
- Training Compliance Summary Report: Dashboard report aggregating organizational compliance rate, completed vs overdue assignments, departmental compliance rankings, and top 10 overdue documents [src: frontend/src/modules/documentControl/pages/ComplianceDashboardPage.tsx:L120-L380].

### MOD-DC-003 Document Control: audit trail
- Document Control Audit Trail CSV Export: Downloaded from Document Audit page (`GET /api/document-control/audit/export`); exports up to 5,000 records (`Take(5000)`). Headers: `EventUid,TimestampUTC,ActorType,User,SystemProcess,ActionCode,ActionCategory,RecordType,EntityId,DocumentMasterId,DocumentRevisionId,Reason,Changes`. The export operation itself is logged in the audit trail [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuditService.cs:L88-L120; frontend/src/modules/documentControl/pages/DocumentAuditPage.tsx:L86-L105].

### MOD-DC-004 Document Control: configuration
- None directly (configuration forms only).

### MOD-DC-005 Document escalations (backend)
- Overdue Training Summary DTO: Backend analytical summary endpoint `GET /api/document-control/escalations/summary/overdue` aggregating overdue counts, department statistics, and open escalations [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentEscalationController.cs:L48-L58].

## audit-trail

### MOD-REPORTS-001 Reports
- Export audit logging: Every CSV export via `ReportingController` logs an audit event via `DataExportAuditService.LogExportAsync(...)` recording the user ID, serialized JSON filter parameters, exported row count, export type name (`"ResultRecordsCsv"`, `"MediaGptCsv"`, or `"ReferenceStrainsCsv"`), and UTC timestamp [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L170-L175, L257-L263, L339-L345].

### MOD-REPORTS-002 Reporting backfill
- Operation logging: `ResultProjectionService.BackfillAsync` writes structured informational and warning logs at each projection stage (`CountTestReading`, `WorkflowStepResult`, `SampleLocation`, `ParameterResult`, and sample approval refresh), and returns a `BackfillResult` record containing counts of Created, Updated, Skipped, and row-level error descriptions [src: backend/MicroLIMS.Application/Services/ResultProjectionService.cs:L413-L514].

### MOD-KPI-001 KPI and Workflow Profiles
- Workflow profile version added:
  - Electronic signature: Signed with `SignatureMeaning.MasterDataChanged`, entity type `ReviewEntityTypes.WorkflowProfile`, recording user ID, IP address, and comment: `"{Scope}: units {WorkUnits ?? inherit}, target days {TargetAnalystDays ?? inherit}, effective {EffectiveFrom}: {ReasonForChange}"` [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L80-L82, L298-L299].
  - Audit event: Action code `WorkflowProfileVersionAdded`, category `Configuration`, entity type `WorkflowProfile`, recording previous vs new values for Scope, WorkUnits, TargetAnalystDays, and EffectiveFrom, plus the mandatory reason for change [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L85-L88, L303-L309].
- Workflow profile draft signed:
  - Electronic signature: Signed with `SignatureMeaning.MasterDataChanged` [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L116-L118].
  - Audit event: Action code `WorkflowProfileDraftSigned`, category `Configuration`, entity type `WorkflowProfile`, recording field diffs and reason for change [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L128-L131].

### MOD-KPI-002 Analyst availability and KPI section settings
- KPI settings changed: Action code `KpiSettingsChanged`, category `Configuration`, record type `KpiSetting`, recording previous and new values for `HoursPerAnalystDay`, `WorkingWeekdays`, and `LoadHorizonDays`, along with user ID and mandatory reason [src: backend/MicroLIMS.Application/Services/Kpi/KpiSettingsService.cs:L82-L84].
- Analyst availability changed: Action code `AnalystAvailabilityChanged`, category `Configuration`, record type `AnalystAvailability`, recording previous and new values for `UserId`, `FromDate`, `ToDate`, and `FractionAvailable`, along with entered by user ID and mandatory reason [src: backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs:L77-L85, L114-L115].

### MOD-DC-001 Document Control: library and document lifecycle
- Document actions writing audit entries [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs; backend/MicroLIMS.Application/Services/DocumentControl/DocumentRevisionService.cs; backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs; backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs; backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs; backend/MicroLIMS.Application/Services/DocumentControl/PeriodicReviewService.cs]:
  - `DocumentMasterRegistered`: Master registration with code, title, type, department, owner, initial revision number [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentMasterService.cs:L192-L200].
  - `DocumentMasterVoided`: Master voiding with mandatory justification (>= 10 chars) [src: L553-L560].
  - `DraftRevisionCancelled`: Draft cancellation with mandatory justification [src: L610-L618].
  - `DocumentRevisionCreated`: New revision creation with revision number, type, reason [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentRevisionService.cs:L158-L175].
  - `RevisionNumberOverridden`: SectionHead manual override of proposed revision number [src: L179-L193].
  - `RevisionFileUploaded`: Upload of initial controlled PDF or source Word file [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentFileService.cs:L230-L239].
  - `RevisionFileReplaced`: Replacement of active draft file, incrementing FileVersion [src: L207-L216].
  - `ControlledFileViewed` / `ControlledFileDownloaded`: Logged upon viewing/downloading controlled PDF [src: L392-L402].
  - `SourceFileViewed` / `SourceFileDownloaded`: Logged upon viewing/downloading source Word document [src: L392-L402].
  - `StoredFileMissing`: Security audit log for missing file bytes in physical storage [src: L346-L360].
  - `FileIntegrityVerificationFailed`: Security audit log for SHA-256 cryptographic mismatch [src: L373-L387].
  - `UnauthorizedFileAccessAttempted`: Security audit log for unauthorized file access or upload [src: L107-L119, L297-L310].
  - `TechnicalReviewSubmitted`: Submitting draft to assigned reviewer with due date and cycle number [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L142-L155].
  - `ReviewFindingAdded`: Review finding logged with page, section, text, mandatory flag [src: L220-L235].
  - `ReviewFindingResponseSubmitted`: Author responding to reviewer finding [src: L370-L385].
  - `ReviewFindingVerified`: Reviewer verifying author response [src: L451-L460].
  - `ReviewFindingResolved`: Review finding marked as resolved [src: L523-L532].
  - `TechnicalReviewCompleted`: Review completed with all mandatory findings resolved, advancing to `AwaitingApproval` [src: L605-L614].
  - `TechnicalReviewReturned`: Review returned to author for correction, reverting to `Draft` [src: L640-L649].
  - `ApprovalTaskCreated`: Assigning designated approver with target effective date [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L136-L150].
  - `ApprovalDossierViewed`: Approver opening inspection dossier [src: L320-L328].
  - `ElectronicSignatureApplied`: 21 CFR Part 11 signature applied with BCrypt verification, recording full name, username, role, meaning (`SignatureMeaning.Approved`), and UTC timestamp [src: L605-L622].
  - `DocumentRevisionApproved`: Revision approved, setting effective and review dates [src: L624-L639].
  - `RevisionReturnedByApprover`: Approver returning revision to Draft with notes [src: L456-L470].
  - `RevisionDeclinedByApprover`: Approver declining revision to Cancelled [src: L501-L516].
  - `PeriodicReviewCreated`: Automated or manual periodic review task generation [src: backend/MicroLIMS.Application/Services/DocumentControl/PeriodicReviewService.cs:L140-L150].
  - `PeriodicReviewRemainsValid`: Periodic review completed with document remaining valid, advancing next review date [src: L598-L612].
  - `PeriodicReviewRevisionRequired`: Periodic review completed requiring revision, transferring findings to next draft [src: L624-L637].
  - `PeriodicReviewObsolescenceRecommended`: Periodic review recommending document obsolescence, creating approval task [src: L680-L695].

### MOD-DC-002 Document Control: reading and training
- Training and reading audit actions [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs; backend/MicroLIMS.Application/Services/DocumentControl/TrainingAssignmentService.cs]:
  - `AcknowledgementRecorded`: Attributable conscious reading acknowledgement recording assignment ID, user ID, client IP address, user agent, frozen legal statement, and controlled file SHA-256 hash [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs:L302-L320].
  - `AcknowledgementRejected`: Security audit event logged for unauthorized or mismatched acknowledgement attempts [src: L158-L166, L175-L183].
  - `AcknowledgementAlreadyRecorded`: Idempotent repeat acknowledgement submission handled [src: L214-L223].
  - `SupersededAssignmentsClosed`: Automated closing of open training assignments upon newer revision activation [src: backend/MicroLIMS.Application/Services/DocumentControl/TrainingAssignmentService.cs:L118-L133].

### MOD-DC-003 Document Control: audit trail
- Audit export event: Action code `AuditTrailExported`, category `Document`, recording user ID, exported row count, and applied filter criteria [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuditService.cs:L122-L130].

### MOD-DC-004 Document Control: configuration
- Configuration audit actions [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentConfigurationService.cs]:
  - `DocumentTypeCreated` / `DocumentTypeUpdated`: Creation/update of document types [src: L91-L97, L180-L190].
  - `DepartmentCreated` / `DepartmentUpdated`: Creation/update of departments [src: L155-L165, L270-L280].
  - `SectionCreated` / `SectionUpdated`: Creation/update of sections [src: L250-L260, L350-L360].
  - `NumberingConfigurationUpdated`: Changes to numbering prefix or format [src: L440-L450].
  - `ConfigurationSettingUpdated`: Administrative changes to module settings [src: L520-L530].

### MOD-DC-005 Document escalations (backend)
- Escalation audit actions [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentEscalationService.cs]:
  - `EscalationCreated`: System-attributed generation of training escalation record [src: L200-L215].
  - `EscalationResolved`: Privileged manager resolution of escalation with mandatory regulatory justification [src: L290-L305].

## traceability

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
- `KpiSetting` links to `DocumentSection` (`SectionId`) and `UpdatedByUserId` [src: backend/MicroLIMS.Domain/Entities/KpiSetting.cs:L1-L25].
- `AnalystAvailability` links to `User` (`UserId`), `EnteredByUserId`, and records audit field changes [src: backend/MicroLIMS.Domain/Entities/AnalystAvailability.cs:L1-L25].

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

## gaps

### GAP-800 TrendingTab hardcoded fallback options and criteria shown as real data
- Question: Why does `TrendingTab.tsx` contain hardcoded test codes, products, categories, and initial search criteria that appear in the UI when backend options fail to load or are empty?
- Affected: SCR-REPORTS-005 (`TrendingTab.tsx:L44-L51, L217-L223, L249-L256, L276-L282`).
- Facts: In `TrendingTab.tsx`, initial state defaults `testCode` to "TAMC" and `subjectName` to a hardcoded product name. When `filterOptions.testCodes` is empty, fallback items (TAMC, TYMC, WATER_TAMC, PATHOGEN_ECOLI) are rendered. When `filterOptions.subjectNames` is empty, five fallback subjects (including two hardcoded product names, a raw material, purified water, and a sampling point) are rendered. When `filterOptions.categories` is empty, four fallback categories are rendered.
- Why it matters: If backend filter options fail to load or the database is freshly initialized, users see misleading mock product names and tests. Selecting these hardcoded options triggers API calls that fail with 400 errors ("test code is not configured in Test Master").
- Needs confirmation: Whether to replace all hardcoded fallbacks with empty state pickers disabled until valid filter options are fetched from the server.
- Who could confirm: Frontend Lead / Product Owner.

### GAP-801 OverviewTab static "Live Database Data" chip and pseudo-deltas
- Question: Should the "Live Database Data" chip reflect actual connection status, and should KPI card deltas show real historical comparisons instead of static 100% / ratio figures?
- Affected: SCR-REPORTS-001 (`OverviewTab.tsx:L120-L124`, `OverviewService.ts:L133-L177`).
- Facts: In `OverviewTab.tsx`, `<Chip size="small" label="Live Database Data" ... />` is hardcoded static JSX rendered on every load. In `OverviewService.ts`, the `deltaPercent` and arrow indicators on KPI cards do not compare with prior periods; instead, `totalTests`, `pendingReview`, and `pendingApproval` hardcode `deltaPercent: 100` (or `0`), `approvedResults` displays the within-period approval percentage as a delta, and `outOfSpec` displays the raw count as a delta percentage.
- Why it matters: Users interpret the green/red arrows and percentages as month-over-month or period-over-period trend deltas, which misrepresents laboratory operational metrics.
- Needs confirmation: Whether to implement true prior-period comparison query in `ReportingQueryService.GetOverviewAggregateAsync` or remove the delta arrows from `OverviewTab`.
- Who could confirm: Product Owner / Backend Lead.

### GAP-802 MOD-KPI-002 section capacity settings and analyst availability not exposed in UI
- Question: When will frontend management screens or dialogs be added for section capacity settings (`/api/kpi/settings/{sectionId}`) and analyst availability windows (`/api/kpi/availability`)?
- Affected: MOD-KPI-002, SCR-KPI-001 (`kpiWorkloadService.ts:L44-L67`, `WorkloadTable.tsx:L111-L113`).
- Facts: Backend controllers (`KpiWorkloadController.cs`) and services (`KpiSettingsService.cs`, `AnalystAvailabilityService.cs`) fully support per-section working weekday masks, standard analyst hours, load horizons, and analyst leave windows. Frontend service `kpiWorkloadService.ts` contains client methods for them. However, no UI screen, dialog, or form in the application calls these methods. The workload table shows alert "Capacity assumes full availability and the default hours."
- Why it matters: Section Heads cannot adjust standard hours or record analyst leave through the application interface, forcing all workload capacity calculations to run on static default assumptions (6.0 hours, Mon-Fri, 5-day horizon).
- Needs confirmation: Roadmap for adding a "Section Capacity Settings" dialog and "Analyst Availability" calendar to the KPI or Laboratory Configuration area.
- Who could confirm: UX Lead / Product Manager.

### GAP-803 MOD-REPORTS-002 reporting backfill endpoint not exposed in UI
- Question: Should an administrative trigger button be added to the Admin/System area to run the `POST /api/admin/reporting/backfill` sweep?
- Affected: MOD-REPORTS-002 (`ReportingAdminController.cs:L25-L27`, `ResultProjectionService.cs:L413-L514`).
- Facts: `ReportingAdminController` exposes `POST /api/admin/reporting/backfill` gated by `Reporting.Admin`. It performs a full projection synchronization from CountTestReading, WorkflowStepResult, SampleLocation, and ParameterResult into ResultRecord. However, there is no button or view in the frontend admin interface to trigger this backfill.
- Why it matters: If data is migrated, imported, or reconciled directly in the database, system administrators cannot re-trigger the reporting projection from the application UI and must use external API tools (e.g. Swagger / curl).
- Needs confirmation: Whether to expose a "Backfill Reporting Data" button under System Administration / Maintenance.
- Who could confirm: System Architect / Backend Lead.

### GAP-804 Lack of user notification before triggering 10,000-row export limit
- Question: Should the UI warn users or disable exports when matching records exceed 10,000 rows, rather than waiting for backend exception rejection?
- Affected: SCR-REPORTS-002 (`ReportResultsTable.tsx:L322-L330, L484-L489`), `ReportingController.cs:L160-L165`.
- Facts: Backend enforces `MaxExportRows = 10_000` on CSV exports. If `totalCount > 10,000`, backend throws `InvalidOperationException` with message `This filter matches {totalCount} rows, which exceeds the 10,000-row export limit. Narrow your filters (e.g. a shorter date range) and try again.` In `ReportResultsTable.tsx`, clicking "Export CSV" opens a confirmation dialog stating `This will export {totalCount} row(s)...` without checking if `totalCount > 10,000`. Confirming triggers a request that fails and displays an error banner.
- Why it matters: Users expecting an export of a large dataset experience an unexpected failure after confirming the action.
- Needs confirmation: Whether `ReportResultsTable` should inspect `totalCount > 10000` client-side and disable the export button with a tooltip or show an explanatory warning in the confirmation dialog.
- Who could confirm: Frontend Lead / UX Lead.

### GAP-805 On-time all stages KPI unconditionally excludes samples without preparation records
- Question: How should on-time turnaround be evaluated for finished-product physicochemical samples and environmental monitoring samples that do not generate `SamplePreparation` records?
- Affected: MOD-KPI-001 (`OnTimeAllStagesKpi.cs:L69-L74`, `2026-10-11-kpi-remodel-index.md:L70`).
- Facts: In `OnTimeAllStagesKpi.cs`, the analyst stage clock starts strictly at `SamplePreparation.PreparedAt`. Finished-product physicochemical samples, Environmental Monitoring samples, and retest clones do not have a `SamplePreparation` row in the database. Consequently, they are skipped from evaluation and reported under gap `NO_ASSIGNMENT_TIME`.
- Why it matters: Physicochemical FP and EM testing constitutes a major portion of laboratory testing, but none of these samples are factored into the headline "On time, all stages" percentage.
- Needs confirmation: Whether samples without `SamplePreparation` should use `Sample.ReceivedAt` or `TestOrder.CreatedAt` as the analyst stage clock start.
- Who could confirm: Quality Assurance Lead / System Architect.

### GAP-806 Observation workflow tests excluded from Right First Time KPI
- Question: Should pathogen observation test returns be tracked via `TestReturnEvent` so qualitative tests can be included in the Right First Time KPI?
- Affected: MOD-KPI-001 (`RightFirstTimeKpi.cs:L75-L80`, `AnalystWorkloadService.cs:L184-L187`, `2026-10-11-kpi-remodel-index.md:L72`).
- Facts: In `RightFirstTimeKpi.cs` and `AnalystWorkloadService.cs`, test orders with `WorkflowType.Observation` are excluded from right-first-time evaluation because reviewer returns in the observation workflow do not create `TestReturnEvent` records. These tests are omitted from both numerator and denominator with gap `OBSERVATION_NOT_TRACKED`.
- Why it matters: Pathogen testing teams do not see their test volume reflected in the headline Right First Time metric.
- Needs confirmation: Whether to instrument observation workflow send-backs to record `TestReturnEvent` rows with `WorkflowType.Observation`.
- Who could confirm: Microbiology Section Head / Backend Lead.

### GAP-807 MOD-DC-005 training escalation management not exposed in UI
- Question: Should a dedicated escalation management panel or action buttons be added to the frontend Document Control interface for reviewing overdue summaries and manually triggering escalation sweeps?
- Affected: MOD-DC-005 (`DocumentEscalationController.cs:L38-L91`, `DocumentEscalationService.cs:L39-L80`).
- Facts: `DocumentEscalationController` exposes 4 endpoints (`assignments/{id}/history`, `summary/overdue`, `process-due`, `resolve`) gated by `Documents.TrainingAssign`. They evaluate assignments against 3 escalation tiers (ApproachingDue, Due, Overdue) and record resolutions with regulatory reasons. However, no UI screen, dashboard tile, or dialog in `frontend/src/modules/documentControl` calls these endpoints.
- Why it matters: Section Heads and QA Compliance managers cannot view overdue training escalation summaries or resolve open escalation records through the web interface, relying entirely on background workers or direct API calls.
- Needs confirmation: Whether to add an "Overdue Escalations" tab or drawer to `ComplianceDashboardPage` or `TrainingMatrixPage`.
- Who could confirm: Product Owner / UX Lead.

### GAP-808 Dual PDF comparison viewer lacks visual text diffing
- Question: Is side-by-side iframe rendering intended as the final revision comparison experience, or is automated text/layout diff highlighting planned?
- Affected: SCR-DC-006 (`DualPdfComparisonViewer.tsx:L1-L150`, `Release_1d_Delta_URS.md`).
- Facts: `DualPdfComparisonViewer` embeds two standard PDF viewer iframes side-by-side (left: effective revision, right: proposed revision) with zoom and page controls. It does not perform automated visual diffing, text difference extraction, or change highlighting.
- Why it matters: Approvers and reviewers reviewing long SOPs must manually scan pages side-by-side to detect text deletions or phrasing adjustments that are not documented in the change items table.
- Needs confirmation: Whether side-by-side visual inspection meets standard operating requirements or if text diff highlighting is required.
- Who could confirm: Quality Assurance Lead / System Architect.

### GAP-809 Document training configuration lacks dedicated UI management interface
- Question: Where should administrators manage per-document and per-type training settings (`RequiresReading`, `RequiresRetrainingOnRevision`, `EscalationDaysBeforeDue`, `EscalationDaysAfterDue`)?
- Affected: MOD-DC-004, MOD-DC-002 (`TrainingAssignmentService.cs:L73-L82`, `DocumentConfigurationPage.tsx:L46-L100`).
- Facts: Backend services evaluate `DocumentTrainingConfigurations` hierarchy (Master-specific > Type-specific > System defaults). However, `DocumentConfigurationPage.tsx` manages Document Types, Departments, Sections, and Numbering, but has no tab or form for configuring training parameters or escalation threshold days.
- Why it matters: Laboratory training administrators cannot toggle whether a document requires reading upon revision or adjust the 3-day approaching / 1-day overdue escalation thresholds without direct database changes.
- Needs confirmation: Whether to add a "Training Policies" tab to `DocumentConfigurationPage` or embed training settings into `DocumentType` dialog.
- Who could confirm: UX Lead / Training Manager.

### GAP-810 Reading progress viewport tracking does not enforce reading before acknowledgement
- Question: Should the system enforce a minimum viewport reading progress (e.g. 100% viewed) before enabling the legal acknowledgement checkbox?
- Affected: SCR-DC-002 (`ControlledPdfViewer.tsx:L180-L195`, `DocumentAcknowledgementDialog.tsx:L170-L197`, `DocumentAcknowledgementService.cs:L126-L131`).
- Facts: `ControlledPdfViewer` and `DocumentAcknowledgementDialog` track viewport scroll percentage (`readingProgress`), but display the mandated disclaimer `"Reading Progress {progress}%. Informational Only. Formal Acknowledgement Required"`. The submit button in `DocumentAcknowledgementDialog` only checks `confirmedStatement` (checkbox) and `canAcknowledge`; it does not check if `readingProgress >= 100`.
- Why it matters: Users can open the acknowledgement dialog and attest to having read the document without actually scrolling through the entire controlled PDF.
- Needs confirmation: Whether regulatory policy requires blocking acknowledgement until the user has scrolled through 100% of the document pages.
- Who could confirm: Quality Assurance Lead / Compliance Officer.
