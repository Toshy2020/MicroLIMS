## 10. Search and Filters

### 10.1 Search Architecture Across MicroLIMS
MicroLIMS does not provide a single, universal global search bar in the header. Instead, search and filtering tools are embedded directly within specific operational modules:
- **Sample Register & Workspaces**: Quick-filtering incoming samples, assigned tests, and incubation statuses.
- **Tracking Board**: Multi-laboratory cross-sectional querying by laboratory section, overall status, and product.
- **Reports (Record Search)**: Historical analytical query engine with multi-parameter filtering, quick-report shortcuts, and CSV/PDF export.
- **Audit Trail (Audit Search)**: Change-event log searching across date ranges, action types, entity categories, and laboratory reference identifiers.
- **Document Library**: Lifecycle and classification filtering across controlled SOPs, training matrices, and document audit records.
- **Error Monitoring**: Technical incident filtering by severity, source tier, date range, and correlation ID.

---

### 10.2 Sample Register and Tracking Board Filters

#### Sample Register Filters (`/receiving`)
On the **Receive Sample** screen, the filter bar allows locating active and incoming records:
- **Search Field**: Text box with placeholder `"Search by item, reference number, control number, batch number..."`. Matches partial or complete strings across reference codes, lot numbers, and item names.
- **Item Type Dropdown**: Filters by product classification (options include "All Item Types", "Product", "Raw Material", "Packaging Material", "Water", "Environmental Monitoring", "After Cleaning").
- **Sample Status Dropdown**: Filters by overall lifecycle stage (options include "All Sample Statuses", "Received", "Under Testing", "Pending Review", "Under Review", "Under Approval", "Approved").
- **Test Status Dropdown**: Filters records based on the progress of their subordinate test orders.
- **Date Range Pickers**: **Received From** and **Received To** date fields to isolate samples received within a defined calendar window.
- **Selection Filtering**: When checkboxes on sample rows are ticked, the **Selection Bar** appears:
  - Displays count: `"{n} sample(s) selected"`.
  - Shows if active filters hide selected items: `"{n} hidden by current filters"`.
  - Button **Show selected only**: Temporarily hides all unselected samples, displaying guidance text: `"Showing only the selected samples; other filters are paused."`
  - Button **Back to filters**: Restores the previous filter criteria.
  - Button **Clear selection**: Unchecks all samples.
- **View Mode Toggles**: Buttons on the right edge switch between **Table view**, **Card view**, and **Kanban view**.

#### Tracking Board Filters (`/receiving/tracking`)
Requires the *Cross-lab sample tracking* permission (`Samples.TrackAll`).
- **Laboratory**: Dropdown with options "All Laboratories", "Microbiology Laboratory", and "Physicochemical Laboratory".
- **Overall Status**: Dropdown with options "All Overall Statuses", "In Progress", "Approved", "Rejected", "Retest Requested", "Voided", and "Cancelled".
- **Product / Item**: Search and selection dropdown populated with registered laboratory items. If the catalog fails to load, helper text reads: `"Product list unavailable - reload the page to retry."`
- **Received From / Received To**: Calendar date pickers (the "To" date includes the full 24-hour calendar day).
- **Reset**: Clears all filter parameters to default settings.
- Default page size is 50 records; maximum server pagination is 200 records per page.

#### Testing Workspace Workload Filters
In the **Microbiology**, **FP**, and **RM & PM** workspaces:
- Clicking any KPI tile (**Needs Preparation**, **Ready to Read**, **Awaiting Review**, **Overdue**, **Assigned to Me**, **Unassigned**) activates a dedicated workload filter.
- Deep-link workload filter chips display at the top of the register: **Active Workload Filter:** with chips such as **Retests in Progress**, **Overdue Review (>24h)**, and **Overdue Approval (>24h)**. Clicking a chip toggles the filter off.

---

### 10.3 Reports — Record Search and Specialized Reporting Filters

#### Record Search Tab (`/reports?tab=search`)
Available to any authenticated user to search historical, approved, and in-progress analytical results.
- **Left Filter Panel (`ReportFilterPanel`)**:
  - Header with **Filters** title and **Reset** link button.
  - **Search**: Free-text input with placeholder `"Search Sample ID, Batch No., Item, Test, Location..."`.
  - **Result Type**: Dropdown options "All", "Quantitative", and "Qualitative".
  - **Category**: Dropdown options "All" plus available sample categories (FinishedProduct, RawMaterial, Water, EnvironmentalMonitoring, AfterCleaning).
  - **Test**: Dropdown displaying `{testCode} - {testDisplayName}`.
  - **Result Level**: Segmented button selector: **All**, **Within Limit**, **Alert Level**, **Action Level**, and **Out of Spec**.
  - **Status**: Dropdown options "All", "Received", "InTesting", "UnderReview", "UnderApproval", "Approved", "Rejected", "RetestRequested".
  - **Approval Status**: Dropdown options "All", "Approved", "Pending", "Rejected".
  - **Date Range**: Two date inputs labeled **From date** and **To date**.
  - **Location / Point**: Dropdown filtering by specific water sampling points, cleanroom areas, or machine surfaces.
  - Action button: **Search Records** (submits the query).
- **Quick Reports Tiles**: Clickable tiles above the filter panel that pre-populate standard filters:
  - **Product**, **Raw Material**, **Water**, **Environmental Monitoring**, **After Cleaning**, **Media/GPT** (switches to Media tab), **Reference Strains** (switches to Strains tab), and **Custom Report** (resets filters and focuses the search box).
- **Results Table Controls**:
  - Displays total count chip: `"{totalCount} Records Found"`.
  - **Columns** button: Dropdown menu with checkboxes allowing users to toggle visible columns (Date/Time, Sample/Reference, Item/Location, Type, Test, Result, Unit, Result Level, Status, Approved By, Approval Date).
  - **Export CSV**: Prompts with confirmation: `"This will export {totalCount} row(s) matching your current filters as a CSV file. This export will be logged."` (enforces a hard 10,000-row maximum export limit).
  - **Export PDF**: Generates a printable A4 landscape report of the currently displayed page.
  - Multi-select actions: Selecting Approved rows displays **Export Selected PDF**, **Export Selected CSV**, and **Analyze Selected** (opens Trending tab).
  - Empty state: `"No results found"` with subtitle `"Try widening the date range or clearing some filters."` and button **Clear Filters**.

[SCREENSHOT SS-101]

#### Specialized Reporting Filters
- **Media / GPT Tab (`/reports?tab=media-gpt`)**: Filters by Search text, Media Type, Evaluation Type (default "GrowthPromotion"), Evaluation Outcome ("Conform", "NonConform"), Approval Status, and Date Range.
- **Reference Strains Tab (`/reports?tab=reference-strains`)**: Filters by Search text, Organism, Approval Status, Active / Destroyed status, Receipt date range, and Usage date range.
- **Trending & Analysis Tab (`/reports?tab=trending`)**: Filters by Parameter / Test, Product / Item (or cleanroom location cascade), Category, Date Range ("Last 30 Days", "Last 3 Months", "Last 6 Months", "Last 12 Months"), and Compare With ("None", "Previous Period", "Product Comparison", "Location Comparison").
- **KPI / Performance Tab (`/reports?tab=performance`)**: Requires `Kpi.View`. Filter bar includes mandatory **From** and **To** dates, **Category** ("All categories"), **Location / subject** ("All locations"), and **Test** ("All tests").

---

### 10.4 Audit Search and Traceability Filters

#### Audit Trail Search (`/audit-search`)
Requires the *Audit trail viewing* permission (`Audit.View`). Allows inspecting change events, user identities, and field diffs across the entire system.
- **Standard Filter Controls**:
  - **From Date** and **To Date**: Date range selection for change events.
  - **Action**: Dropdown options: **All Actions**, **Create**, **Update**, and **Delete**.
  - **Entity Category**: Dropdown displaying all auditable entities formatted as `"{label} ({key})"`.
- **Collapsible Laboratory Identifiers**:
  - Clicking the toggle **More Laboratory Identifiers (Batch, Sample, Media...)** expands specialized search fields:
    - **Sample Reference** (e.g. `FP0107026`)
    - **Batch Number**
    - **Control Number**
    - **Media Lot Number**
    - **Cryovial / RS Code**
    - **User ID**
  - Clicking **Hide Laboratory Identifiers** collapses the panel.
- **Search Execution**:
  - Buttons: **Search Audit Trail** (displays **Searching...**) and **Reset**. Pressing Enter within any filter field executes the query.
- **Results Capping and Presentation**:
  - Results header displays: **Audit Events (N)**.
  - If 300 or more matching events exist, the header indicates: `", capped at 300 latest"`.
  - Empty state: `"No audit events found"`.
  - Row action: **Inspect Event & Traceability Chain** opens a drawer displaying before-and-after JSON diffs, actor details, and upstream/downstream traceability nodes.

[SCREENSHOT SS-102]

> **Note for reviewers:** Audit Trail shows 300 events at most, has no export, cannot filter on user/role actions, and does not show login history or security events. (GAP-030, GAP-101)

---

### 10.5 Document Control Library and Audit Filters

#### Document Library Filters (`/document-control/library`)
Open to all authenticated users for locating Standard Operating Procedures and controlled specifications:
- **Search Field**: Text box with placeholder `"Search code, title, or keywords..."`.
- **Document Type**: Dropdown with options "All Types", followed by registered document types (`{code}: {name}`).
- **Department**: Dropdown with options "All Departments", followed by registered departments (`{code}: {name}`).
- **Status**: Dropdown with options "All Statuses", "Draft", "In Review", "Awaiting Approval", "Future Effective", "Effective", "Superseded", "Obsolete", and "Cancelled".
- **Switches**:
  - **Show Voided**: Toggles visibility of retired and voided document masters.
  - **Review Overdue Only**: Filters the library to active effective documents whose periodic review date is past due.
- **Reset**: Clears all active filter parameters.
- Empty state: `"No controlled documents match the selected filters"`. Pagination options: 5, 10, 25, 50 rows per page.

[SCREENSHOT SS-103]

#### Training and Compliance Filters
- **My Reading List (`/document-control/my-reading-list`)**: Search box (`"Search document title, code, MicroLIMS ID..."`), button **Refresh List**, and status tabs: **To read**, **Overdue**, **Completed**, **Superseded**, and **All**.
- **Training Matrix (`/document-control/training-matrix`)**: Search box (`"Search personnel name, username, or role..."`), **Department** dropdown ("All Departments"), and **Qualification Status** dropdown ("All States", "Qualified", "Pending", "Overdue", "Trained on Superseded Only").

#### Document Control Audit Trail Filters (`/document-control/audit`)
Requires base role type `Reviewer`, `SectionHead`, or `SystemAdministrator`.
- **Search Field**: Text input with placeholder `"Search Event UID, Action, Reason, Entity..."`.
- **Action Category**: Dropdown options: "All Categories", "Document", "Workflow", "Security", "Configuration", and "System".
- **Date Pickers**: **Date From** and **Date To**.
- **Reset**: Restores default search parameters.
- Action button: **Export Audit Trail (CSV)**.

> **Note for reviewers:** In the Document Control Audit Trail, the dropdown options "Workflow" and "System" do not match the backend enum values, and some backend categories cannot be selected. (GAP-813)

---

### 10.6 Error Monitoring Filters
#### Error Monitoring (`/error-monitoring`)
Requires the *Error log monitoring* permission (`System.ViewErrorLog`). Displays technical system incidents and application exceptions.

- **Filter Controls**:
  - **Severity**: Dropdown options: "All", "Info", "Warning", "Error", "Critical".
  - **Source**: Dropdown options: "All", "Backend", "Frontend", "Database".
  - **Status**: Dropdown options: "All", "Open", "Resolved".
  - **Active from / Active to**: Calendar date pickers applied against the incident's "Last seen" timestamp.
  - **Search summary or correlation id**: Text input filtering incident descriptions or unique error correlation identifiers.
- **Actions**:
  - Button **Reset**: Clears all filters.
  - Button **Export CSV** (displays **Exporting...**): Exports up to 5,000 incidents matching current filters to a CSV file.
- **Empty State**: `"No incidents match these filters."`

> **Note for reviewers:** Error Monitoring is a technical log, not a deviation or investigation record; resolving an incident is not audited. (GAP-110)
