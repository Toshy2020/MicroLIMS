## 10. Search and Filters

### 10.1 Where You Can Search
MicroLIMS has no single search box that searches the whole application. Search and filters are part of individual screens:
- **Receive Sample (sample register)** and the laboratory workspaces: find samples, tests, and workloads.
- **Tracking Board**: find samples across laboratories.
- **Reports**: search results and see media, reference strain, trending, and KPI figures.
- **Audit Trail**: search change events.
- **Document Control**: library, reading list, training matrix, and Document Control audit trail.
- **Error Monitoring**: filter technical incidents.
- **Discussions** and **Messages**: search posts and conversations (see Section 9).
- **Roles**: search by role name ("Search by role name...").

---

### 10.2 Sample Register, Tracking Board, and Workspace Filters

#### Sample register (Receive Sample)
The filter bar above the register offers:
- **Search**: the box "Search by item, reference number, control number, batch number..."
- **Item Type**: **All Item Types**, **Product**, **Raw Material**, **Packaging Material**, **Water**, **Environmental Monitoring**, **After Cleaning**.
- **Sample Status**: **All Sample Statuses**, **Received**, **Under Testing**, **Pending Review**, **Under Review**, **Under Approval**, **Approved**, **Rejected**, **Cancelled / Voided**.
- **Test Status**: **All Test Statuses**, **Not Started**, **In Progress**, **Result Entered**, **Under Review**, **Reviewed**, **Approved**, **Rejected**.
- **Received From** and **Received To**: dates.
- View buttons for the table, card, and kanban views.

When you tick samples in the register, a selection bar appears. It shows "{n} sample(s) selected" and, when filters hide some of them, "{n} hidden by current filters". The button **Show selected only** shows just the ticked samples ("Showing only the selected samples; other filters are paused."), **Back to filters** returns to your filters, and **Clear selection** unticks everything.

> **Note for reviewers:** The register uses the label **Cancelled / Voided** for the status that other screens call "Retest Requested". (GAP-204)

#### Tracking Board
Needs the *Cross-Lab Sample Tracking* permission (see Section 4).
- **Laboratory**: **All Laboratories**, **Microbiology Laboratory**, **Physicochemical Laboratory**.
- **Overall Status**: **All Overall Statuses**, **In Progress**, **Approved**, **Rejected**, **Retest Requested**, **Voided**, **Cancelled**.
- **Product / Item**: choose an item. If the item list cannot be loaded, the screen shows "Product list unavailable - reload the page to retry."
- **Received From** and **Received To**, and a reset button.
- The board shows 50 samples per page.

#### Laboratory workspaces
In the Microbiology, FP, and RM & PM workspaces, clicking a summary tile (**Needs Preparation**, **Ready to Read**, **Awaiting Review**, **Overdue**, **Assigned to Me**, **Unassigned**) filters the register to that workload. Clicking the tile again removes the filter. Links from dashboards can also set a filter; the line **Active Workload Filter:** then shows a label such as **Retests in Progress**, **Overdue Review (>24h)**, or **Overdue Approval (>24h)**, and clicking it removes the filter.

---

### 10.3 Reports: Record Search and Other Report Filters

#### Record Search tab
Open to every signed-in user. It searches results in your laboratory sections.
- The **Filters** panel, with a **Reset** link, contains:
  - **Search**: "Search Sample ID, Batch No., Item, Test, Location..."
  - **Result Type**: **All**, **Quantitative**, **Qualitative**.
  - **Category**: **All** and the categories that have results.
  - **Test**: tests shown as "{code} - {name}".
  - **Result Level**: **All**, **Within Limit**, **Alert Level**, **Action Level**, **Out of Spec**.
  - **Status**: **All**, **Received**, **InTesting**, **UnderReview**, **UnderApproval**, **Approved**, **Rejected**, **RetestRequested** (these names appear in the list as written).
  - **Approval Status**: **All**, **Approved**, **Pending**, **Rejected**.
  - **Date Range**: **From date** and **To date**.
  - **Location / Point**: **All** and the locations or points that have results.
  - The button **Search Records**.
- **Quick Reports** tiles pre-set the filters: **Product**, **Raw Material**, **Water**, **Environmental Monitoring**, **After Cleaning**, **Media/GPT** and **Reference Strains** (these two open their own tabs), and **Custom Report** (clears the filters and moves to the search box).
- The results table shows "{n} Records Found" and has the buttons **Columns** (choose which columns to show), **Export PDF** (the page you are looking at), and **Export CSV**.
  - **Export CSV** asks "This will export {n} row(s) matching your current filters as a CSV file. This export will be logged." and exports at most 10,000 rows. If more rows match, narrow the filters (for example a shorter date range).
  - When you tick approved rows, the buttons **Export Selected PDF**, **Export Selected CSV**, and **Analyze Selected** (opens the Trending tab) appear.
- When nothing matches: "No results found" and "Try widening the date range or clearing some filters." with the button **Clear Filters**.

[SCREENSHOT SS-101]

#### Other Reports tabs
- **Media / GPT**: Search, Media Type, Evaluation Type (starts as GrowthPromotion), Evaluation Outcome (**Conform**, **NonConform**), Approval Status, and a date range.
- **Reference Strains**: Search, Organism, Approval Status, Active / Destroyed, a receipt date range, and a usage date range.
- **Trending & Analysis**: Parameter / Test, Product / Item (or a location selection for water, environmental monitoring, and after-cleaning), Category, Date Range (**Last 30 Days**, **Last 3 Months**, **Last 6 Months**, **Last 12 Months**, starting at 12 months), and Compare With (**None**, **Previous Period**, **Product Comparison**, **Location Comparison**), then **Apply Criteria**.
- **KPI / Performance**: needs the *KPI Dashboard Viewing* permission. **From** and **To** dates (required), **Category** (starts as "All categories"), **Location / subject** (starts as "All locations"), and **Test** (starts as "All tests").

> **Note for reviewers:** When no options can be loaded, the Trending tab offers a built-in list of tests and products instead. These may not match your laboratory's data. (GAP-002)

---

### 10.4 Audit Trail Search

#### Audit Trail (Audit Search)
Needs the *Audit Trail Viewing* permission (by default: System Administrator, Section Head).
- **From Date** and **To Date**.
- **Action**: **All Actions**, **Create**, **Update**, **Delete**.
- **Entity Category**: **All Entities** and the record types, each shown as a name with its internal key in brackets.
- **More Laboratory Identifiers (Batch, Sample, Media...)**: opens **Sample Reference**, **Batch Number**, **Control Number**, **Media Lot Number**, **Cryovial / RS Code**, and **User ID**. The same control then reads **Hide Laboratory Identifiers**.
- The buttons **Search Audit Trail** (**Searching...** while processing) and **Reset**. Pressing Enter in a field also searches.
- The result heading reads **Audit Events (N)**. When 300 or more events match, it adds ", capped at 300 latest" and only the 300 newest are shown. If nothing matches: "No audit events found". If the search fails: "Failed to fetch audit records from the server."
- **Inspect Event & Traceability Chain** on a row opens a panel with the differences, the raw data, and the traceability chain.

[SCREENSHOT SS-102]

> **Note for reviewers:** The Audit Trail screen shows at most 300 events, has no export or print function, cannot filter on user and role administration actions, and does not show login history or security events. (GAP-030, GAP-101)

> **Note for reviewers:** Some record types (for example error log entries, sign-in tokens, and notification read state) are never written to the audit trail, so they cannot be found here. (GAP-111)

---

### 10.5 Document Control Filters

#### Document Library
Open to every signed-in user.
- **Search**: "Search code, title, or keywords..."
- **Document Type**: **All Types**, then each type as "{code}: {name}".
- **Department**: **All Departments**, then each department as "{code}: {name}".
- **Status**: **All Statuses**, **Draft**, **In Review**, **Awaiting Approval**, **Future Effective**, **Effective**, **Superseded**, **Obsolete**, **Cancelled**.
- The switches **Show Voided** and **Review Overdue Only**, and the button **Reset** (disabled while no filter is set).
- When nothing matches: "No controlled documents match the selected filters". The table can show 5, 10, 25, or 50 rows per page.

[SCREENSHOT SS-103]

#### My Reading List and Training Matrix
- **My Reading List**: the search box "Search document title, code, MicroLIMS ID...", the button **Refresh List**, and the tabs **To read**, **Overdue**, **Completed**, **Superseded**, and **All**.
- **Training Matrix**: the search box "Search personnel name, username, or role...", **Department** (**All Departments**), and **Qualification Status** (**All States**, **Qualified**, **Pending**, **Overdue**, **Trained on Superseded Only**).

#### Document Control Audit Trail
Only users with the Reviewer, Section Head, or System Administrator base role type get results.
- **Search**: "Search Event UID, Action, Reason, Entity..."
- **Action Category**: **All Categories**, **Document**, **Workflow**, **Security**, **Configuration**, **System**.
- **Date From** and **Date To**, and **Reset**.
- The button **Export Audit Trail (CSV)** (**Exporting CSV...** while processing).

> **Note for reviewers:** The categories **Workflow** and **System** in this list are not categories the system uses, and some categories it does use cannot be chosen. How the system answers the two unknown values has not been tested. (GAP-813)

---

### 10.6 Error Monitoring Filters
Needs the *Error Log Monitoring* permission (by default: System Administrator).
- **Severity**: **All**, **Info**, **Warning**, **Error**, **Critical**.
- **Source**: **All**, **Backend**, **Frontend**, **Database**.
- **Status**: **All**, **Open**, **Resolved**.
- **Active from** and **Active to**: dates, applied to the time the incident was last seen.
- **Search summary or correlation id**.
- **Reset**, and **Export CSV** (**Exporting...** while processing), which exports up to 5,000 incidents for the current filters. If the export fails: "The export could not be generated."
- When nothing matches: "No incidents match these filters." If the list cannot be loaded: "Could not load incidents."

> **Note for reviewers:** Error Monitoring is a technical log, not a deviation or investigation record, and resolving an incident is not written to the audit trail. It also lists routine refusals such as business-rule and permission errors. (GAP-110)

<!-- Review log (Sonnet): 25 corrections; invented items removed: page addresses, internal component and permission-code wording (Kpi.View, Samples.TrackAll, Audit.View, System.ViewErrorLog), item-type and sample-status lists corrected (added Rejected and Cancelled / Voided, test status list added), "Reset" button label on Tracking Board, unsupported description of Review Overdue Only, Trending category list, "immutable" audit wording, example reference FP0107026, "before-and-after JSON" wording, "read all" etc.; callouts added: GAP-204, GAP-002, GAP-030/101, GAP-111, GAP-813, GAP-110 -->
