## 45. My Work

### 45.1 My Work Overview and Landing Behavior
#### Purpose
Give every signed-in user a single page that lists the tests, reviews, approvals, document tasks, equipment tasks, and material tasks waiting for their action, ordered with the most urgent items first.

#### Who can use it
Any signed-in user. Each group on the page is limited to your own assignments, your permissions, and your laboratory sections, so you only see tasks that belong to your work and scope.

#### What you see
- **After sign-in:** **My Work** opens automatically as your home screen. If you have no waiting tasks in **My Work** and your role holds the *Reviewer Dashboard* or *Lab Overview Dashboard* permission, MicroLIMS redirects you to **Dashboard** once per browser session. You can return to **My Work** at any time from the left sidebar or by clicking **Open My Work** on the **Analyst Dashboard**.
- **Sidebar count badge:** **My Work** is the first link under **OVERVIEW** in the left sidebar. Next to **My Work**, a badge shows your total number of actionable tasks across laboratories:
  - The badge appears in red when at least one of your tasks is **Overdue**, and in blue otherwise.
  - When the sidebar is collapsed to icons, hovering over **My Work** shows a tooltip with the total task count and any overdue count (for example, "My Work, 7 tasks, 2 overdue").
  - Locked items (which you cannot decide because of segregation of duties) are never included in the badge count.
  - If you have zero tasks, or if any task source could not be loaded, the badge is hidden rather than showing an incomplete number.
- **Page header and controls:**
  - The heading **My Work**, the subtitle "Tests, reviews, approvals, and tasks waiting for your action.", and the **Refresh** button.
  - Four summary tiles across the top: **All work**, **Overdue**, **Now**, and **Today**.
  - The **Search by identifier** box and the live item counter "{n} items shown".
- **Empty, filtered, and outage states:**
  - When nothing is waiting for you across all groups: **Nothing waiting for you** ("All assigned tests, reviews, and tasks are up to date.") with the button **Go to Dashboard**.
  - When your active urgency tile or search text matches no rows: **No matching work items** ("No items match the current urgency filter or search.") with the button **Clear filters**.
  - When one or more task sources cannot be loaded while others succeed: a warning banner "Some data could not be loaded: {sources}." with a **Retry** button, above the groups that did load.
  - When the page cannot be loaded at all: an error alert with a **Retry** button.

#### System behavior
- **My Work** refreshes automatically when you open the page, when you return focus to the browser window, and every 60 seconds while the tab is visible.
- Background refreshes preserve your scroll position and keep keyboard focus on the row or button you were using.

---

### 45.2 Task Groups and Who Sees What
#### Purpose
Organize waiting work by the kind of action required so you can handle immediate bench work, reviews, approvals, and upcoming deadlines in priority order.

#### What you see
Groups appear in the fixed order below. Empty groups are hidden. Each group card shows its heading, a count chip with the number of actionable items in that group, and up to 50 rows. When a group has more than 50 items, a **Show all {n}** link appears in the group header and opens the corresponding workspace or list filtered to that queue.

| Order | Group heading | What it contains | Who sees it |
|---|---|---|---|
| 1 | **Ready to read now** | Open incubations on active tests assigned to you (or started by you) whose reading time has arrived—because the expected reading time has passed, the incubation window has ended, or the minimum duration was overridden—and that do not have a pending return for revision. | Users with assigned or started incubations in their readable sections |
| 2 | **Returned to me** | Active tests assigned to you that a reviewer returned for revision and that do not yet have an active reading recorded. The row displays the reviewer's return reason. | Users with returned tests in their readable sections |
| 3 | **Incubation ending today** | Open incubations on active tests assigned to you (or started by you) that are not ready to read yet, whose expected reading or end time falls before the end of today in the laboratory time zone. | Users with assigned or started incubations in their readable sections |
| 4 | **Incubation ending in the next 24 hours** | Open incubations on active tests assigned to you (or started by you) that are not ready to read yet, whose expected reading or end time falls after the end of today in the laboratory time zone and within the next 24 hours from now. | Users with assigned or started incubations in their readable sections |
| 5 | **Tests to perform** | Open test orders (**Pending** or **In Progress**, not superseded) assigned to you in your readable sections that have no open incubation and no pending return. | Users with assigned tests in their readable sections |
| 6 | **Awaiting my review** | Sample sections in **Under Review** status within the laboratory sections you are a member of. | Requires the *Review samples* permission (by default: Reviewer, Section Head, System Administrator) |
| 7 | **Awaiting my approval** | Sample sections in **Under Approval** status within your member sections, plus media lots in your member sections whose evaluation completed as conforming and are awaiting a release decision. | Requires the *Approve samples* permission for samples and the *Release media* permission for media lots (by default: Section Head, QC Manager, System Administrator) |
| 8 | **Available in my section** | Unassigned open test orders (not superseded and not outsourced) in the laboratory sections you are a member of, ready for you to take when self-assignment is enabled. | Requires the *Self-assign tests* permission (by default: Analyst) |
| 9 | **Unassigned tests** | Unassigned open test orders (not superseded and not outsourced) in your readable sections waiting to be assigned to an analyst. | Requires the *Sample analyst assignment* permission (by default: Section Head, System Administrator) |
| 10 | **Documents** | Your pending or in-progress document technical review tasks, your pending document approval tasks, and your open training or reading assignments (statuses **Assigned**, **Reading**, **Overdue**, **KAF Pending**, or **Failed**). | All signed-in users |
| 11 | **Equipment** | Open calibration and preventive maintenance tasks that are overdue, due today, or due within the look-ahead window (7 days by default). | Requires the *Record equipment work* permission |
| 12 | **Media, solutions and strains** | Incomplete media evaluations (growth promotion, indication/inhibition, or enrichment characteristics) on media lots you prepared; prepared or active media lots and prepared solutions you prepared that expire within the look-ahead window (7 days by default) or have already expired; and reference cryovials you thawed whose destruction reminder time has passed without a recorded destruction. | Users who prepared the media or solutions, or thawed the cryovials, in their readable sections |

#### Important rules
- "My" tests and materials always mean records assigned to or prepared by your own user account. Holding cross-section read access widens which sections you can view in section queues, but never places another analyst's assigned tests into your personal groups.
- If your role holds both *Self-assign tests* and *Sample analyst assignment*, you see both **Available in my section** (to take a test yourself) and **Unassigned tests** (to assign a test to an analyst).
- A test assigned to you appears in only one analyst group at a time: if it has a pending return it appears in **Returned to me**; otherwise if it has an open incubation that is ready, ending today, or ending in the next 24 hours it appears in **Ready to read now**, **Incubation ending today**, or **Incubation ending in the next 24 hours**; otherwise, when it has no open incubation, it appears in **Tests to perform**.

> **Note for reviewers:** Equipment tasks do not have a per-user owner field and are linked to a document section rather than a laboratory section, so **Equipment** lists all due equipment tasks to every user who holds the *Record equipment work* permission. (CONF-199)

---

### 45.3 Urgency, Due Times, and Locked Rows
#### Purpose
Show which items need action first and clearly indicate when segregation of duties prevents you from reviewing or approving a record you worked on.

#### What you see
- **Urgency chips:** Each row displays an urgency chip when a deadline or priority applies:
  - **Overdue** (red):
    - A document review, document approval, training assignment, equipment task, media lot expiry, solution expiry, or cryovial destruction reminder is past its due time; or
    - A sample test in **Tests to perform**, **Available in my section**, or **Unassigned tests** belongs to a sample received more than 24 hours ago; or
    - A review, approval, or media release entry in **Awaiting my review** or **Awaiting my approval** has waited more than 24 hours in its current stage; or
    - A ready-to-read incubation has waited longer than the configured reading-overdue limit (when enabled by an administrator).
  - **Now** (purple): An incubation in **Ready to read now** (while within the reading-overdue limit or when that limit is switched off), or a test in **Returned to me**.
  - **Today** (amber): A task due on or before the end of the current day in the laboratory time zone, including items in **Incubation ending today**.
  - **Later** (blue): A task with a due date after today within the look-ahead window (7 days by default), or an incubation in **Incubation ending in the next 24 hours**.
  - Tasks with no due date that have not passed the 24-hour stage window show no urgency chip.
- **Due time:** When a task has a due time or expected reading time, the row shows the date, time, and laboratory time zone abbreviation (for example, `14 Nov, 16:30 EET`). All urgency levels and due times come from the server clock in the laboratory time zone.
- **Row ordering within a group:** Unlocked rows always appear first, ordered from most urgent (**Overdue**, **Now**, **Today**, **Later**, then no urgency) and then oldest first. Locked rows appear at the bottom of the group.
- **Locked rows (segregation of duties):** In **Awaiting my review** and **Awaiting my approval**, any item you are not permitted to decide yourself is shown greyed out with a lock icon and the exact reason:
  - "You performed a test on this sample" — shown on review and approval rows when you were the assigned analyst, prepared the sample, or recorded any result, count reading, pathogen observation, workflow step result, confirmatory plate reading, or physicochemical analysis on a test in that section.
  - "You reviewed this sample" — shown on sample approval rows when you completed the technical review for that section.
  - "You prepared or evaluated this media lot" — shown on media release rows in **Awaiting my approval** when you prepared the media lot or read a challenge in its evaluation.
  - Locked rows are not links, have no action buttons, and are not counted in the group chip, the summary tiles, or the sidebar badge.

#### How to use it
1. Click the **Overdue**, **Now**, or **Today** summary tile at the top of **My Work** to filter all groups to that urgency level. Click the active tile again, or click **All work**, to remove the urgency filter.
2. Type part of a sample reference number, document ID, lot number, equipment code, or task title into **Search by identifier** to narrow the visible rows across all groups.
3. Click **Clear filters** (or clear the search box and select **All work**) to restore the full list.

> **Note for reviewers:** The system setting that marks a ready-to-read incubation as **Overdue** after a set number of hours (`myWork.readOverdueHours`) ships as `0` (switched off) until the laboratory confirms the allowed reading wait time, so ready incubations stay at **Now**. (CONF-198)

---

### 45.4 Opening a Task from My Work
#### Purpose
Go straight from a row in **My Work** to the workspace dialog or record where the step is performed.

#### How to use it
1. Locate the task in its group (use the summary tiles or **Search by identifier** if needed).
2. Click the row's identifier and title link.
3. MicroLIMS opens the target screen directly at the required step:
   - **Ready to read now**, **Incubation ending today**, and **Incubation ending in the next 24 hours**: opens the **Microbiology** or **Physicochemical** testing workspace and opens the incubation reading dialog for that sample, test order, and incubation.
   - **Returned to me**, **Tests to perform**, and **Available in my section**: opens the **Microbiology** or **Physicochemical** testing workspace and opens the test step dialog for that sample and test order.
   - **Awaiting my review**: opens the testing workspace and opens the technical review dialog for that sample.
   - **Awaiting my approval**: for a sample section, opens the testing workspace and opens the approval dialog for that sample; for a media lot, opens the **Media** page pre-selected to that lot.
   - **Unassigned tests**: opens the testing workspace and opens the **Assign Responsible Analyst** dialog for that sample and test order.
   - **Documents**: opens the document detail page in **Document Control** for technical reviews and document approvals, or opens **My Reading List** for training and reading assignments.
   - **Equipment**: opens the **Equipment** inventory page.
   - **Media, solutions and strains**: opens **Media Evaluation** for pending evaluations, **Media** for expiring media lots, **Solution Preparation** for expiring solutions, or **Reference Cryovials** for thawed vials awaiting destruction.

---

### 45.5 Taking and Releasing Unassigned Tests
#### Purpose
Allow analysts to claim unassigned tests in their own laboratory section directly from **My Work**, and return a self-assigned test to the section queue if no work has been recorded yet.

#### Who can use it
Requires the *Self-assign tests* permission (by default: Analyst), membership in the test's laboratory section, an assignable role (Analyst, Reviewer, or Section Head), and the system setting for test self-assignment to be enabled (enabled by default).

#### What you see
- In **Available in my section**, each row includes a **Take** button on the right.
- In **Tests to perform**, a row includes a **Release** button on the right only when you took the test yourself and have not yet recorded any preparation, incubation, or testing work on the sample.
- Clicking **Release** opens the confirmation dialog **Release test assignment** with the message "Release {reference} ({title}) back to the section queue?" and the buttons **Cancel** and **Release**.

#### How to use it
**To take an unassigned test:**
1. In **Available in my section**, find the unassigned test you want to perform.
2. Click **Take** on that row (you do not need to leave **My Work**).
3. Verify the confirmation toast "Taken {reference}." The test moves from **Available in my section** to **Tests to perform**.

**To release a test you took:**
1. In **Tests to perform**, find the test you took and click **Release**.
2. In the **Release test assignment** dialog, click **Release** (or click **Cancel** to keep the test).
3. Verify the confirmation toast "Released {reference}." The test returns to **Available in my section**.

#### System behavior
- **When you take a test:**
  - The system assigns the test order to you and writes an audit trail entry for the test order with action `TEST_TAKEN`, recording the change from `Unassigned` to your full name.
  - If two analysts click **Take** on the same test at the same time, only the first save succeeds; the second analyst receives "Already taken by {name}." and no second audit entry is written.
- **When you release a test:**
  - A test can be released by you only when all three rules hold:
    1. The test is still open (**Pending** or **In Progress**), not superseded, not outsourced, and currently assigned to you.
    2. The most recent assignment audit record on the test order is your own `TEST_TAKEN` action. A test that a Section Head assigned or reassigned to you cannot be released by you.
    3. You have not recorded anything on the sample: no sample preparation, no started or completed incubation, no workflow step result, no count reading, no pathogen observation, no physicochemical test analysis, and no result on any test of the sample.
  - When confirmed, the system clears your assignment on the test order and writes an audit trail entry with action `TEST_RELEASED`, recording the change from your full name back to `Unassigned`.

#### Validation
The system enforces the following validation messages when taking or releasing a test:
- "Taking tests is switched off. Ask a Section Head to assign this test."
- "You do not have permission to take or release tests."
- "This test belongs to a laboratory section you are not assigned to."
- "Already taken by {name}."
- "This test is closed, replaced or outsourced and can no longer be taken."
- "Only the analyst who holds this test can release it."
- "This test can no longer be released. Ask a Section Head to reassign it."
- "This test was changed by someone else. Refresh and try again."
