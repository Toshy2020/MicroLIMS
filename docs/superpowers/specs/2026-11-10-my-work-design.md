# C1 — "My Work": Design Specification

**Roadmap package:** C1 (`improvement-roadmap.md`)
**Status:** Draft for user review
**Date:** 2026-11-10

## 1. Purpose

Give every user one page that lists what is waiting for them, most urgent first, where each row opens the exact step. Target from the roadmap: an analyst can finish a normal day without using the sidebar.

## 2. Decisions (confirmed by the user, 2026-11-10)

| # | Decision | Answer |
|---|---|---|
| 1 | Placement | My Work is the first page after login for every role; role dashboards stay one click away |
| 2 | Review / approval queue | Show everything the user is allowed to decide in their sections; items they performed themselves are shown locked with the reason and are not counted |
| 3 | Overdue | Keep the existing 24-hour-in-status rule for samples and tests; use the real due times that already exist elsewhere |
| 4 | Unassigned tests | Analysts see unassigned tests in their sections and can take them |

Decision 4 changes a workflow rule (today only an authorised user assigns). Its exact rules are in 5.5 and need your confirmation.

## 3. Out of scope

- Turnaround targets and sample due dates (own package later).
- Assigning reviews to a named reviewer.
- OOS investigation tasks (no assignee exists until package D1).
- Access-review tasks (no list exists today; added when that module has one).
- A team view of other people's work (stays on the Section Head dashboard).
- Live push updates (package C5; C1 refreshes by polling).
- Personal free-text to-dos.

## 4. Current state (from code, not run)

- After login every role goes to `/dashboard`; `DashboardPage.tsx` picks a role dashboard.
- `GET /api/dashboard/my-tasks` (`MyTasksService`) is analyst-only: assigned tests, returned tests, incubations due within 2 days, media evaluations, cryovials.
- Reviewer and Section Head queues come from `GET /api/dashboard/reviewer` and `/section-head`; they are filtered by section only. Segregation of Duties is checked when the decision is made, not in the list.
- **Two different "ready to read" rules exist.** `SampleWorkflowQueues.IsIncubationReadyToRead` uses expected reading time, incubation end, or a recorded minimum-duration override. `MyTasksService` uses expected reading time only. The same incubation can be "ready" on one screen and not on another.
- Overdue: `SampleWorkflowQueues.OverdueAfter` = 24 hours in a status. Samples and tests have no due date.
- Assignment: `TestOrder.AssignedAnalystId`, set by `SampleAssignmentService` after `AnalystAssignmentRules.EnsureAssignableAsync`. No self-assignment.
- Per-user lists already exist for document review tasks and training assignments. Document approvals can be filtered by approver. Equipment tasks and calibration have due dates but no per-user list.
- `PartialOutageBanner` is named in `CLAUDE.md` section 9 but does not exist in `frontend/src`.

## 5. Design

### 5.1 One endpoint

`GET /api/my-work?lab=MICRO|FP` returns everything for the signed-in user. Any authenticated user may call it; each source applies its own permission and section scope, so the response contains only what the user may already see elsewhere.

```
{
  serverTimeUtc,
  groups: [ { key, title, count, lockedCount, items: [ … up to 50 ], hasMore } ],
  unavailableSources: [ key, … ]
}
```

Item:

| Field | Meaning |
|---|---|
| `key` | Stable id (`source:recordId`) |
| `identifier` | Sample number, document code, equipment code |
| `title`, `context` | What to do; test name, section, stage |
| `dueAtUtc` | Null when the task has no due time |
| `urgency` | `Overdue`, `Now`, `Today`, `Later`, `None` — decided by the backend clock |
| `locked`, `lockReason` | For Segregation of Duties and similar gates |
| `target` | `{ type, sampleId?, testOrderId?, stepId?, incubationId?, documentId?, taskId?, equipmentId? }` — the frontend maps `type` to a route |
| `actions` | e.g. `["take"]`, `["release"]` |

`GET /api/my-work/summary` returns only `{ total, overdue, now }` for the sidebar badge and browser tab title.

### 5.2 Groups, in display order

| # | Group | Source and rule | Who |
|---|---|---|---|
| 1 | Ready to read now | Open incubations on my tests (assigned to me or started by me) where `IsIncubationReadyToRead` is true | Analyst |
| 2 | Returned to me | Tests returned for correction (`TestReturnEvent`) not yet resubmitted | Analyst |
| 3 | Incubation ending today | As 1, not ready yet, expected reading before local midnight | Analyst |
| 4 | Tests to perform | `AssignedAnalystId` = me, not closed or superseded, next step is a manual step | Analyst |
| 5 | Awaiting my review | Section sign-offs under review in my sections, when I hold the review permission | Reviewer |
| 6 | Awaiting my approval | Samples under approval in my sections, when I hold the approval permission; plus media awaiting approval | Section Head / QC Manager |
| 7 | Available in my section | Unassigned tests in my sections that I am eligible to perform | Analyst |
| 8 | Unassigned (to assign) | Unassigned tests in my sections | Users who may assign |
| 9 | Documents | My document review tasks, document approvals assigned to me, my training / reading assignments | Everyone |
| 10 | Equipment | Equipment tasks and calibrations due within 7 days or overdue, in my sections | Users with the equipment permission |
| 11 | Media, solutions and strains | My media evaluations; my thawed cryovials awaiting action; media lots and solutions I prepared that expire within 7 days | Analyst |

Empty groups are not shown. Inside a group: `Overdue`, `Now`, `Today`, `Later`, then oldest first.

The 7-day look-ahead is one setting, `myWork.lookAheadDays` (default 7, 1–30).

### 5.3 One "ready to read" rule

`SampleWorkflowQueues.IsIncubationReadyToRead` becomes the only rule. `MyTasksService`'s own version is removed. A test proves that My Work, the incubation overview and the workspace agree for the same incubation.

### 5.4 Urgency

| Urgency | Rule |
|---|---|
| `Overdue` | A real due time has passed (document, training, equipment, expiry), **or** a sample/test has been in its status longer than `SampleWorkflowQueues.OverdueAfter` (24 h), **or** an incubation has been ready to read for more than `myWork.readOverdueHours` |
| `Now` | Ready to read; returned to me |
| `Today` | Due before the end of the laboratory's local day |
| `Later` | Due within the look-ahead |
| `None` | No due time and not overdue |

**Question for you (9.1):** how long may a plate wait after it becomes ready before it counts as overdue to read? This is a laboratory rule; I did not find it in `documents/`. Until you answer, the setting has no default and reading tasks are never marked overdue.

"End of the local day" needs the laboratory's time zone; C1 adds the setting `system.timeZone` (IANA name) if none exists. All times are shown with the zone.

### 5.5 Taking an unassigned test (new rule — please confirm)

- `POST /api/testorders/{id}/take` assigns the test to the caller.
- Allowed when: the setting `workflow.selfAssignment.enabled` is on (default on; a Section Head can switch it off); the caller holds the new permission `Tests.SelfAssign` (seeded to Analyst); the caller is a member of the test's section; the test is unassigned; and `AnalystAssignmentRules.EnsureAssignableAsync` passes — the same eligibility check the Section Head's assignment uses.
- Two analysts taking the same test: the first wins; the second gets "Already taken by {name}" (optimistic concurrency).
- `POST /api/testorders/{id}/release` returns it to unassigned, only by the analyst who took it and only while nothing has been recorded on the test. After that, only a user who may assign can reassign.
- Both actions are audited like any assignment, with the actor as both assigner and assignee.
- When package D4 (competency gate) arrives, it plugs into `EnsureAssignableAsync` and applies here automatically.

### 5.6 Locked rows (Segregation of Duties)

For groups 5 and 6 the backend marks an item `locked` with reason "You performed a test on this sample" when the existing decision-time check (`DidUserPerformTestAsync`) would reject the user. This is computed for the whole list in one query, not per row. Locked rows are greyed, carry the reason as text, are not links, and are excluded from `count` and from the badge. The decision endpoints keep their own check; the lock is a display aid only.

### 5.7 Partial failure

Each source runs independently. If one fails, the others are returned and its key is listed in `unavailableSources`. The page shows a banner: "Some tasks could not be loaded: Documents. Retry." The failure is captured by the existing error capture. This adds the shared `PartialOutageBanner` component that `CLAUDE.md` already requires.

### 5.8 The page

- Route `/my-work`; `/` and the post-login redirect go there. A user whose response has no groups and who holds a dashboard permission is sent to `/dashboard` (typical for a System Administrator).
- Header: "My Work", the date, last-updated time, Refresh, and a link "Dashboard".
- A tile strip: Overdue, Now, Today, Total. Each tile filters the list.
- Lab tabs (Microbiology / Physicochemical) when the user works in both, as on the dashboards.
- Groups as sections with a heading and count. Each row: urgency label (text plus colour token), identifier in monospace, title, context, due time (relative and exact, with time zone), and the row action.
- A row is a link to the exact step. "Take" and "Release" are buttons on the row.
- A search box filters by identifier across groups. Filters live in the URL.
- A group with more than 50 items shows "Show all {n}", linking to the workspace list with the matching filter.
- Refresh: on load, on window focus, and every 60 seconds while the tab is visible. A refresh never moves keyboard focus or scroll position; new overdue items are announced politely.
- States: loading skeleton; empty ("Nothing is waiting for you" with a link to the dashboard); partial outage; error with Retry; locked rows.
- Sidebar: "My Work" is the first item, with the count badge.
- The role dashboards lose `TodaysWorkTable` and the attention panel, replaced by one line "Open My Work ({n})". KPIs and charts stay. `GET /api/dashboard/my-tasks` and `todays-work` are removed once nothing calls them.

### 5.9 Performance

- Every source is a read-only, `AsNoTracking`, projected query with section scope in SQL; no per-row queries.
- Each group returns at most 50 items plus an exact count.
- The summary endpoint uses count queries only.

## 6. Error handling

| Situation | Behaviour |
|---|---|
| One source fails | Partial response and banner (5.7) |
| All sources fail | Error state with Retry; the sidebar badge is hidden, not shown as 0 |
| Row target no longer valid (already done by someone else) | The target page shows its normal state; My Work refreshes on return |
| Take loses the race | Toast "Already taken by {name}"; list refreshes |
| Take refused by eligibility | Toast with the rule's own message |
| User has no section | Only the Documents group can appear |

## 7. Testing

- **`[PostgresFact]` per source:** the item appears for the right user and not for another user, another section, or a closed record; urgency at each boundary with a fake clock (23 h 59 / 24 h 01; ready time; local midnight in a non-UTC zone); 50-item cap with exact count.
- **Consistency:** one incubation, three surfaces, same ready state (5.3).
- **Segregation of Duties:** own-work item is locked, uncounted, and the decision endpoint still rejects it.
- **Take / release:** state + audit + negatives (setting off, no permission, other section, ineligible, already assigned, concurrent take, release after a recording, release by another user).
- **Partial failure:** a throwing source yields the others plus `unavailableSources`.
- **Authorisation:** a user never receives an item from a section they cannot read; `Sections.ViewAll` does not turn other people's assigned tests into "mine".
- **Frontend (`userEvent`, real components, mocked `services/`):** grouping and order, tile filters, search, locked row not a link and reason readable, Take/Release flows, partial-outage banner, empty and error states, refresh keeps focus, landing redirect rules.
- **Manual:** keyboard-only pass, screen reader pass, tablet width.

## 8. Delivery stages

1. Aggregator, item model, analyst sources (groups 1–4, 11), single ready rule.
2. Review and approval sources with locking (5, 6).
3. Take / release and groups 7–8.
4. Documents and equipment sources (9, 10).
5. Page, sidebar badge, landing change, dashboard clean-up.
6. Requirements and user-guide text.

## 9. Open items

1. **Reading overdue time** (5.4): how many hours after "ready" is a reading overdue? Per test or one value?
2. **Confirm the take / release rules** (5.5), especially: on by default, and release only before anything is recorded.
3. **Equipment tasks:** are they owned by a person or by the section? The spec assumes the section; checked against the code when the plan is written.
4. **Laboratory time zone** value for "today".
