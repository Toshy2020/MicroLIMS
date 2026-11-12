# FS-NTF Functional Specification

| Field | Value |
|---|---|
| Document number | FS-NTF |
| Title | Dashboards, notifications and messaging functional specification |
| Version | 0.1 |
| Status | Draft |
| Author | Claude (AI agent), draft from code |
| Reviewer | To be assigned by QA |
| Approver | To be assigned by QA |
| Effective date | Set when Approved |

## Change history

| Version | Date | Author | Change |
|---|---|---|---|
| 0.1 | 2026-11-12 | Claude (AI agent), draft from code | First draft, written from the code as built. |

## Scope and reading notes

- This document covers `DashboardController`, `DashboardLabScopeService`, `DashboardNotificationService`, `MyTasksService` (the task list only), `Dashboard/SectionHeadQueueBuilder`, `MessageController` and `MessageService`. `DashboardService` (the figures, the Section Head and reviewer dashboards, today's work, the incubation overview) and `RecentActivityService` are called by the controller but were not read; their rules are not described here.
- Every statement was written from the code files named in each entry. Nothing was run.
- Audit. `NotificationLog` is on the audit exclusion list (`AuditCaptureExcludedTypes`: derived reminders and read state, not a GxP record), so notification rows are not audited. The conversation, participant and message entities are not on that list, so their saves are audited by the automatic capture. Dashboards and notifications make no regulated decision.
- Concurrency. None of the methods in this document calls `RecordVersion.EnsureCurrent` (no stale-form version check).

## Functions

### FS-NTF-001
**Satisfies:** URS-NTF-001
**Function:** Dashboard access and laboratory scope. The controller is `[Authorize]`; the user ID is read from the name-identifier claim and the role from the role claim (parsed as the `RoleType` enum). Endpoints take an optional `lab` code. `DashboardLabScopeService.ResolveAsync` first gets the user's readable sections (null for `Sections.ViewAll`). With no `lab` the scope is those sections. With a `lab`: an unknown code is refused with a business-rule error (`LaboratoryCodes.IsKnown`); the code is upper-cased and mapped to the active sections with that code; for a user with a section list, the result is the overlap and an empty overlap is refused with `UnauthorizedAccessException` ("not a member of this laboratory"); for a user with `Sections.ViewAll` the scope is all sections of that laboratory. The known laboratory codes are "MICRO" and "FP". The endpoints `section-head` and `reviewer` need the policies `Dashboards.LabOverview` and `Dashboards.Review`. The endpoints `my-tasks` and `analyst-metrics` return 403 (`Forbid`) unless the role claim is Analyst; this test is on the role, not on a permission. The other endpoints (summary, kpi-deltas, monthly-trend, category-distribution, status-distribution, recent-activity, todays-work, incubation-overview, notifications) need only a signed-in user. The `notifications` endpoint without `lab` returns every notification of the user. The services that compute the figures were not read.
**Code:** `backend/MicroLIMS.API/Controllers/DashboardController.cs` — class and actions; `backend/MicroLIMS.Application/Services/DashboardLabScopeService.cs` — `ResolveAsync`
**Verified by:** reading (not run)

### FS-NTF-002
**Satisfies:** URS-NTF-002
**Function:** Notifications. `GetNotificationsAsync(role, userId)` computes the current notifications, stores new ones, pushes them, and returns the user's 30 newest stored notifications (so a read request writes rows). A recompute-throttle may be present; when it says the user is not due, the call returns the stored list only, and a new notification can appear up to one window late. The computed notifications, limited to the user's readable sections where the record has a section: (1) MediaExpiry: media with status Active and expiry date up to now plus 7 days; severity "error" when the expiry is not after now, else "warning"; (2) IncubationReady: incubations with a completion time set, whose test order is in the section scope, is on the Incubating or Running step, is not superseded and has status Pending or InProgress; the 20 newest by completion time; severity "info"; (3) ApprovalWaiting: for the role SectionHead, or a user who holds `Dashboards.LabOverview`, a count of samples with a section sign-off under approval; and PendingPreparationConfigApproval, a count of item preparation configurations pending review (no section filter); (4) ReviewWaiting: for the roles Reviewer or SectionHead, or a holder of `Dashboards.Review`, a count of samples with a section sign-off under review; (5) TestReturnedForRevision: tests assigned to the user that were returned in the last 12 hours, severity "warning", with the reason text; (6) monitoring reminders from `MonitoringReminderService`, when that service is present. Storing: a notification whose message text equals a message stored for the same user in the last 12 hours is skipped. For each new one a `NotificationLog` row is added and a push is sent; for severity "error" the row is marked as emailed and an email is sent to the address `{username}@microlims.local`. The pushes and emails are sent before the single `SaveChangesAsync` of the batch. With a `lab`, the list is then filtered: a notification with a test order is kept if the order's section is in the laboratory's sections; the types MediaExpiry, IncubationReady, PendingPreparationConfigApproval and TestReturnedForBiochemical are kept only for Microbiology; a notification with only a sample is kept if the sample has a test in those sections; the rest are kept. `MarkAsReadAsync` marks one of the user's own notifications read and returns silently when it is not found or not the user's. `MarkAllAsReadAsync` marks all of the user's unread notifications read by a direct update. The 7-day window and the 12-hour window are written in the code. The approval and review waiting rules differ from the text of `BusinessRules.md` section 12.
**Code:** `backend/MicroLIMS.Application/Services/DashboardNotificationService.cs` — `GetNotificationsAsync`, `ComputeAsync`, `PersistAndDeliverAsync`, `MarkAsReadAsync`, `MarkAllAsReadAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-002, CONF-161, CONF-165

### FS-NTF-003
**Satisfies:** URS-NTF-003
**Function:** Analyst task list (`GET api/dashboard/my-tasks`, analysts only, see FS-NTF-001). Test orders assigned to the user (`AssignedAnalystId`), not superseded and not Approved, Rejected, Voided or Cancelled, limited to the user's sections (or the chosen laboratory's). A test order with a pending return gives a "Revise Test" task with urgency Overdue and the return reason. Otherwise the earliest open incubation (completion time empty, expected reading time set) gives a "Read Test" task when its expected reading time is no later than now plus 2 days; the 2-day and the 4-hour windows are written in the code. Urgency: Overdue if the due time is before now; DueSoon if within 4 hours; DueToday if on the same date; else DueTomorrow. Media evaluations not completed, for lots prepared by the user (restricted to the laboratory's sections when a laboratory is chosen), give a task for the earliest open challenge incubation due within the same window. The list is sorted by due time. This "ready to read" rule (an open incubation due within two days) is not the rule of the IncubationReady notification (a completed incubation), see FS-NTF-002. The method writes nothing.
**Code:** `backend/MicroLIMS.Application/Services/MyTasksService.cs` — `GetMyTasksAsync`, `ComputeUrgency`
**Verified by:** reading (not run)
**Conflicts:** CONF-002

### FS-NTF-004
**Satisfies:** URS-NTF-004
**Function:** Messages (`api/messages`, any signed-in user). Create conversation: an initial message is required; the creator is added to the participants; there must be at least two distinct participants; any inactive user among them refuses the request; for a one-to-one conversation that already exists between the two users, the message is added to it instead; otherwise the conversation, participants, first message and the creator's read mark are saved, and the other active participants are notified (a `NotificationLog` row of type "DirectMessage" with the first 80 characters of the text, and a push, through `NotifyMessageSentAsync`; a failure there is logged and swallowed, so the message is still saved). Send message: content must not be blank; the sender must be a participant, else `UnauthorizedAccessException`; the message and the new last-message time are saved with the UTC time from the injected clock, and the participants are notified. Read messages: the user must be a participant; `take` is clamped to 1 to 100; deleted messages are left out; newest are taken, then returned oldest first. Get conversation by ID: refused unless the user is a participant. Mark read: sets the user's last-read message to the newest undeleted message; does nothing if the user is not a participant. Unread count is computed per conversation as messages with a higher ID than the last read, sent by others, not deleted. The create and send methods run in one transaction. No role, permission or laboratory-scope check applies to who may be added to a conversation. There is no endpoint to edit or delete a message. Conversation and message rows are saved with the automatic audit capture.
**Code:** `backend/MicroLIMS.Application/Services/MessageService.cs` — `CreateConversationAsync`, `SendMessageAsync`, `GetMessagesAsync`, `GetConversationByIdAsync`, `MarkAsReadAsync`, `GetTotalUnreadCountAsync`; `backend/MicroLIMS.API/Controllers/MessageController.cs` — actions
**Verified by:** reading (not run)

### FS-NTF-005
**Satisfies:** URS-NTF-005
**Function:** Section Head queue builder (pure functions; the data is loaded elsewhere, by `DashboardService`, which was not read). Review queue: one row per (sample, section) under review; submitted time from the section sign-off, else a stored clock start, else the sample's received time; age in hours rounded to 1 decimal (`Math.Round`, default midpoint rule); analyst names from the assigned analyst of each test, else the latest result entry; the worst result level of the entry's tests; ordered by age, oldest first. Approval queue: one row per (sample, section) under approval; reviewed time and reviewer name; same age rule. Attention items: overdue tests (the reason text says the testing stage was pending for the delay in hours, "(>168h / 7-day Analyst SLA)"; the selection of overdue orders is made by the caller; at most 5 samples); retests in progress (reason "Retest sample in progress" with the origin reference; urgency High); delayed reviews and delayed approvals (entries with an age of 24 hours or more; at most 5 each; urgency Medium). The 168-hour, 24-hour and 5-item numbers are written in the code. The display name of a sample is the item name, else the water point code, department, machine, else the reference.
**Code:** `backend/MicroLIMS.Application/Services/Dashboard/SectionHeadQueueBuilder.cs` — `BuildReviewQueue`, `BuildApprovalQueue`, `BuildOverdueAttentionItems`, `BuildRetestAttentionItems`, `BuildDelayedReviewAttentionItems`, `BuildDelayedApprovalAttentionItems`
**Verified by:** reading (not run)
