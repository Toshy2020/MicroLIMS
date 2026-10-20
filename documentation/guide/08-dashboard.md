## 8. Dashboard

### 8.1 Dashboard Variants
The Dashboard is the home screen after sign-in. MicroLIMS shows one of four dashboards, chosen by the **base role type** of your role (see Section 4):
- System Administrator: the **Administrator Command Center**.
- Analyst: the **Analyst Dashboard**.
- Reviewer: the **Reviewer Command Center**.
- Section Head, and any other role type: the **Section Head Command Center**.

The data on a dashboard follows your permissions and your laboratory assignment. The Reviewer dashboard needs the *Reviewer Dashboard* permission (by default: System Administrator, Section Head, Reviewer), and the Section Head dashboard needs the *Lab Overview Dashboard* permission (by default: System Administrator, Section Head). The Analyst dashboard is available to users with the Analyst base role type only.

Dashboards cannot be exported.

> **Note for reviewers:** The dashboard variant follows the base role type, while the data follows the permissions and laboratory assignment, so a custom role can see a dashboard that does not match its permissions. (GAP-055)

#### Laboratory Tabs
If you are assigned to more than one laboratory, tabs named **Microbiology Laboratory** and **Physicochemical Laboratory** appear at the top. Each tab limits the figures to that laboratory. Users assigned to one laboratory do not see the tabs.

#### Clicking a Tile
Clicking a tile opens the laboratory workspace already filtered to the samples or tests that the tile counts (the Prep configs pending tile opens the Items configuration).

#### Empty and Error States
- "Dashboard could not be loaded" when the data cannot be retrieved.
- "No dashboard data is available yet." when there is nothing to show.

---

<!-- SCR-DASH-001 -->
### 8.2 Administrator Command Center
#### Purpose
Show system administrators the totals and queues across the laboratories in their scope.

#### Who can use it
Users with the System Administrator base role type.

#### What you see
The heading **Administrator Command Center** and the button **Refresh**. The tiles, in order:
1. **Total samples**: all samples ever registered (not only today's).
2. **Total tests**: all test orders ever created (not only today's).
3. **Pending tests**: open test orders (not yet completed).
4. **Review queue** (caption "samples"): sections waiting for technical review.
5. **Approval queue** (caption "samples"): sections waiting for the release decision.
6. **Prep configs pending**: item preparation configurations waiting for approval. This tile appears only when the count is above zero, and opens the Items configuration.
7. **Overdue (>24h)**: open test orders on samples that were received more than 24 hours ago.

[SCREENSHOT SS-081]

> **Note for reviewers:** "Total samples" and "Total tests" are all-time counts, not counts for today. (GAP-106)

> **Note for reviewers:** "Overdue" has three different meanings on the dashboards. Here it counts from sample receipt; on the Section Head dashboard it uses the 7-day analyst deadline; on the Reviewer dashboard it counts time in review. Read each tile's rule. (GAP-102)

#### System behavior
The tiles show current counts. Samples with several sections can be counted once per section in the queue tiles.

#### Errors
- "The dashboard service did not respond. Your data has not been changed."

---

<!-- SCR-DASH-002 -->
### 8.3 Analyst Dashboard
#### Purpose
Show analysts their work for the day, their recent performance, and items that need attention.

#### Who can use it
Users with the Analyst base role type.

#### What you see
- The banner **Welcome back, {name}** and the subtitle "Your prioritized work in the {lab name} for today." (or "Your prioritized work for today."), the button **Refresh**, and a link to the laboratory workspace. If part of the data cannot be loaded, an alert with a **Retry** button appears.
- **My Operational Performance**:
  - **On-Time Reading Rate** (Microbiology only): the share of your completed incubations that were read within 4 hours after the expected reading time. It shows 100 when you have no completed incubations.
  - **7-Day Completed Volume**: results and readings you entered in the last 7 days.
  - **Active Assigned Orders**: orders assigned to you that are pending, in progress, or marked for retest.
- **Completed Today**: **Tests Completed** (results and readings you entered today) and **Media Lots Prepared** (lots you prepared today).
- **Attention Required**: up to 5 media lots that expire within 5 days (Microbiology only), each shown as "{media type} · Lot {lot}" with "Expires in {n} day(s)" and the label **Expiring**, then up to 5 of your notifications (not including the review and approval waiting notices), each with its age ("just now", "{n} min ago", "{n} hr(s) ago", "{n}d ago"). When there is nothing: "No active attention alerts. All conditions normal."
- **Today's Laboratory Work**: a table with the columns **Sample / Reference**, **Category**, **Tests**, **Received At**, **Status**, and **Next Action**, a filter, and a **View all** link. When the filter finds nothing: "No work matches this filter."
- An incubation panel (Microbiology only). When none is running: "No active incubations running."

[SCREENSHOT SS-082]

> **Note for reviewers:** The "On-Time Reading Rate" shows 100 when there is no data, so it cannot be told apart from a real 100.

#### System behavior
Only users with the Analyst base role type can load the Analyst dashboard figures.

---

<!-- SCR-DASH-003 -->
### 8.4 Reviewer Command Center
#### Purpose
Show reviewers the samples waiting for technical review, reviews that have waited too long, and retests in progress.

#### Who can use it
Users with the Reviewer base role type. The figures need the *Reviewer Dashboard* permission.

#### What you see
Five tiles:
1. **Pending review** (caption "samples"): sections waiting for technical review.
2. **Overdue review** (caption "in review >24 hours"): reviews waiting for more than 24 hours.
3. **Due today** (caption "submitted today"): reviews that were submitted today.
4. **Retests in progress**: samples with a retest in progress.
5. **Completed today** (caption "by you"): reviews you completed today.

Empty states: "No samples are currently waiting for scientific review." and "No recent review history."

[SCREENSHOT SS-083]

> **Note for reviewers:** "Due today" counts the reviews submitted today. It does not show the reviews that are due today. (GAP-105)

#### System behavior
Clicking a tile opens the laboratory workspace with the matching filter.

---

<!-- SCR-DASH-004 -->
### 8.5 Section Head Command Center
#### Purpose
Show Section Heads the laboratory workload, the stages of the pipeline, overdue work, and the analysts' status.

#### Who can use it
Users with the Section Head base role type, and any other base role type that has no dashboard of its own. The figures need the *Lab Overview Dashboard* permission.

#### What you see
- Tiles:
  - **Active tests**: open tests that are not superseded.
  - **Incubating**: open incubations that are not yet ready to read.
  - **Ready to read**: open incubations whose reading time has come.
  - **Pending review** and **Pending approval**: sections waiting for the technical review or the release decision.
  - **Overdue**: samples still with the analyst after the 7-day analyst deadline.
  - **Attention items**: overdue samples, retests in progress, and review or approval waits of 24 hours or more, added together.
- A pipeline strip with the stages **Incubation**, **Ready to Read**, **Scientific Review**, and **Final Approval**.
- Lists: samples awaiting scientific review ("No results awaiting scientific review."), samples awaiting final authorization ("No samples awaiting final authorization."), and laboratory analysts ("No active laboratory analysts found.").
- Charts: **Samples Received Trend** and **Test Order Status** (a donut chart; empty: "No data yet.").

[SCREENSHOT SS-084]

> **Note for reviewers:** "Overdue" on this dashboard uses the 7-day analyst deadline and differs from the "Overdue" tiles on the other dashboards. (GAP-102)

#### Errors
- "The dashboard service did not respond. No laboratory data has been changed."

<!-- Review log (Sonnet): 20 corrections; invented items removed: JWT/endpoint/query-parameter wording, "validated grace window", Overdue tile meaning for Section Head (was 24 h from receipt; actual is 7-day analyst deadline), retest cause "out-of-specification or investigative finding", "Dashboards.Review/LabOverview" as role selectors, "Section Head authorization" for prep configs, unsupported incubation panel details; callouts added: GAP-055, GAP-102 (x2), GAP-105, GAP-106, on-time rate default -->
