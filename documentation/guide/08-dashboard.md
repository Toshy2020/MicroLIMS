## 8. Dashboard

### 8.1 Dashboard Variants and Role Routing
The primary operational overview at `/dashboard` serves as the user's home screen. The system automatically selects which dashboard variant to render based on the user's underlying **base role type** (stored in the JWT claims token):
- Base role type `SystemAdministrator` opens the **Administrator Command Center**.
- Base role type `Analyst` opens the **Analyst Dashboard**.
- Base role type `Reviewer` opens the **Reviewer Command Center**.
- Base role type `SectionHead` (and any other role type) opens the **Section Head Command Center**.

> **Note for reviewers:** The dashboard variant follows the user's base role type, while underlying data access follows granted permissions and laboratory membership. (GAP-055)

> **Note for reviewers:** "Overdue" has three different meanings on the dashboards; read each tile's rule. (GAP-102)

#### Laboratory Tabs
If you hold active membership in more than one laboratory section (e.g. both `MICRO` and `FP`), the dashboard displays laboratory tabs at the top of the screen (labeled **Microbiology Laboratory** and **Physicochemical Laboratory**). Switching tabs appends the `?lab=` query parameter and filters all dashboard metrics to the selected laboratory. Users assigned to a single laboratory do not see these tabs.

#### Common Error and Empty States
- If dashboard data cannot be retrieved from the server, the interface presents: `"Dashboard could not be loaded"`.
- If no data has been generated yet for the laboratory, the screen displays: `"No dashboard data is available yet."`

---

<!-- SCR-DASH-001 -->
### 8.2 Administrator Command Center
#### Purpose
Provide system administrators with global operational metrics, queue volumes, pending configuration approvals, and system-level turnaround indicators.

#### Who can use it
Users whose base role type is `SystemAdministrator`.

#### What you see
- Heading: **Administrator Command Center**.
- Action button: **Refresh**.
- Operational Metric Tiles (in order):
  1. **Total samples**: All-time total count of samples registered in the database.
  2. **Total tests**: All-time total count of test orders created across all samples.
  3. **Pending tests**: Number of active test orders currently undergoing preparation or testing (clicking links to `?status=Active`).
  4. **Review queue** (caption "samples"): Number of samples currently awaiting technical review (clicking links to `?status=UnderReview`).
  5. **Approval queue** (caption "samples"): Number of samples currently awaiting release approval (clicking links to `?status=UnderApproval`).
  6. **Prep configs pending**: Number of item preparation configurations awaiting Section Head authorization. This tile appears only when the count is greater than zero, linking to `/laboratory-configuration/items`.
  7. **Overdue (>24h)**: Number of active tests open more than 24 hours after sample receipt (clicking links to `?urgency=overdue`).

[SCREENSHOT SS-081]

> **Note for reviewers:** Administrator "Total samples/tests" are all-time counts; Reviewer "Due today" counts submissions made today. (GAP-105, GAP-106)

#### System behavior
- Tiles display live database counts loaded via `/api/dashboard` and `/api/dashboard/kpi-deltas`. Clicking any tile navigates directly to the workspace filtered by that workload or status parameter.

#### Errors
- If the service fails to respond, the screen displays: `"The dashboard service did not respond. Your data has not been changed."`

---

<!-- SCR-DASH-002 -->
### 8.3 Analyst Dashboard
#### Purpose
Guide laboratory analysts through their daily work priorities, assigned tests, upcoming incubation readings, and critical attention items.

#### Who can use it
Users whose base role type is `Analyst`.

#### What you see
- Welcome banner: **Welcome back, {name}**.
- Subtitle: "Your prioritized work in the {lab name} for today." (or "Your prioritized work for today.").
- Action buttons: **Refresh** and a direct link button to open the laboratory workspace.
- **My Operational Performance** panel:
  - **On-Time Reading Rate**: Percentage of incubation readings taken within the validated grace window (Microbiology tab only; defaults to 100% when no data exists).
  - **7-Day Completed Volume**: Number of test orders completed by you in the last 7 days.
  - **Active Assigned Orders**: Number of open test orders currently assigned to you.
- **Completed Today** panel:
  - **Tests Completed**: Total tests finished by you today.
  - **Media Lots Prepared**: Total media lots prepared by you today.
- **Attention Required** panel:
  - Lists up to 5 media lots expiring within 5 days (Microbiology tab only), displayed as "{media type} · Lot {lot}", "Expires in {n} day(s)", with chip **Expiring**, linking to `/laboratory-configuration/media`.
  - Lists up to 5 personal attention notifications (excluding general review/approval queues) with relative timestamps ("just now", "{n} min ago", "{n} hr(s) ago", "{n}d ago").
  - Empty state: `"No active attention alerts. All conditions normal."`
- **Today's Laboratory Work** table:
  - Columns: **Sample / Reference**, **Category**, **Tests**, **Received At**, **Status**, and **Next Action**.
  - Includes a quick filter and a **View all** link. Empty state: `"No work matches this filter."`
- **Incubation Observation Panel** (Microbiology only):
  - Displays currently running incubations and expected completion times. Empty state: `"No active incubations running."`

[SCREENSHOT SS-082]

> **Note for reviewers:** Analyst "On-Time Reading Rate" shows 100 when there is no data. (BR-DASH-010)

#### System behavior
- The dashboard loads your specific assignments via `/api/dashboard/my-tasks` and `/api/dashboard/analyst-metrics`. Attempting to access these endpoints from a non-Analyst role type returns HTTP 403 Forbid.

---

<!-- SCR-DASH-003 -->
### 8.4 Reviewer Command Center
#### Purpose
Provide technical reviewers with queue visibility for submitted results requiring scientific review, delayed reviews, and in-progress retests.

#### Who can use it
Users whose base role type is `Reviewer` (or holding `Dashboards.Review`).

#### What you see
- Header with review queue summary metrics.
- Queue Tiles:
  1. **Pending review** (caption "samples"): Number of sample sections ready for secondary technical verification (links to `?status=UnderReview`).
  2. **Overdue review** (caption "in review >24 hours"): Samples that have remained in review status for longer than 24 hours without sign-off (links to `?status=UnderReview&workload=reviewOverdue`).
  3. **Due today** (caption "submitted today"): Samples submitted for review during the current calendar day (links to `?status=UnderReview`).
  4. **Retests in progress**: Samples where retesting was initiated following an out-of-specification or investigative finding (links to `?workload=retestInProgress`).
  5. **Completed today** (caption "by you"): Number of sample reviews completed and signed by you today.
- Empty states:
  - If no reviews are waiting: `"No samples are currently waiting for scientific review."`
  - If no recent completions exist: `"No recent review history."`

[SCREENSHOT SS-083]

#### System behavior
- Clicking any tile navigates directly to the laboratory workspace with the matching status and workload filters applied.

---

<!-- SCR-DASH-004 -->
### 8.5 Section Head Command Center
#### Purpose
Enable Section Heads to monitor full laboratory throughput, pipeline bottlenecks, overdue testing, final release authorizations, and overall team volume.

#### Who can use it
Users whose base role type is `SectionHead` (or holding `Dashboards.LabOverview`).

#### What you see
- Operational Overview Tiles (Row 1):
  - **Active tests**: Total test orders currently open (links to `?status=Active`).
  - **Incubating**: Tests currently in incubation (links to `?view=kanban`).
  - **Ready to read**: Tests that have completed incubation and await plate reading (links to `?testStatus=ReadyToRead`).
  - **Pending review**: Samples awaiting technical review (links to `?status=UnderReview`).
  - **Pending approval**: Samples awaiting final release authorization (links to `?status=UnderApproval`).
  - **Overdue**: Tests delayed beyond 24 hours from sample receipt (links to `?urgency=overdue`).
  - **Attention items**: Urgent alerts requiring supervisor intervention.
- Pipeline Progress Strip:
  - Visual stage sequence: **Incubation** > **Ready to Read** > **Scientific Review** > **Final Approval**.
- Lists and Queues:
  - Samples awaiting technical review. Empty: `"No results awaiting scientific review."`
  - Samples awaiting final release authorization. Empty: `"No samples awaiting final authorization."`
  - Analyst status list. Empty: `"No active laboratory analysts found."`
- Charts:
  - **Samples Received Trend**: Historical volume chart.
  - **Test Order Status**: Donut chart displaying proportion of tests across lifecycle stages (empty state: `"No data yet."`).

[SCREENSHOT SS-084]

#### Errors
- If the service fails to respond, the screen displays: `"The dashboard service did not respond. No laboratory data has been changed."`
