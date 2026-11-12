# Notification Centre and Live Updates (C5) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Event- and schedule-driven notifications for every user, a notification centre, a reliable email outbox, per-user preferences with mandatory types, escalation rules counted in working hours, live signals over SignalR with polling fallback, and a "changed by someone else" banner.

**Architecture:** One `NotificationPublisher` is the only writer of notifications; services call it inside their own transaction, and a `NotificationWorker` calls it for time-based items. Emails are rows in `EmailOutbox`, sent after commit by the worker. Live updates are data-free signals published after commit through `ILiveSignalPublisher`; the browser refetches through the normal API. Escalation uses a pure `WorkingTimeCalculator` over a weekly schedule plus the existing `LabCalendarDate` non-working dates.

**Tech Stack:** .NET 10 (SignalR from the shared framework), EF Core 10, PostgreSQL; React 19, TypeScript 6, MUI 9, `@microsoft/signalr`, Vitest.

**Spec:** `docs/superpowers/specs/2026-11-11-notifications-live-updates-design.md`

## Global Constraints

- `CLAUDE.md` binds every task (separate contexts + `AsNoTracking`; `[PostgresFact]` executed, 0 skipped; `userEvent`; real components; mock only `services/`; no `any`; injected `TimeProvider` / `ILabClock`).
- **No external side effect inside a database transaction** (`CLAUDE.md` 5.4): emails are sent only by the outbox sender; live signals are published only after a successful commit.
- Notification and email text never contains a result value, a specification limit, a password or a token.
- A live signal carries only `{ kind, key }` strings. No entity field is ever sent over the connection.
- In-app delivery cannot be disabled. Email is possible only for catalogue types with `EmailAllowed`.
- Emailed types: `MediaExpiry`, `SolutionExpiry`, `StrainExpiry`, `CalibrationDue`, `ItemOverdue`, `Escalation`.
- Escalation thresholds are **working hours** (user decision 2026-11-11). No threshold and no rule is seeded. A rule cannot be created until working hours are configured.
- `Notifications.Configure` → Section Head and QC Manager (user decision). Granted by `RoleGrantSql.Grant` in migration `AddNotificationCentre`.
- Settings: `notifications.retentionDays` 90 (30–365); `notifications.digestHourLocal` 7 (0–23, an Integer because no time-of-day setting type exists); `notifications.lookAheadDays` 7 (1–30).
- Fixed values: worker interval 5 minutes; outbox attempts 5 with delays 1, 5, 15, 60, 240 minutes; bell shows 10; centre page size 25.
- Existing `NotificationLog` rows must remain readable; new columns are nullable or defaulted.
- One migration, `AddNotificationCentre`. `NotificationLog` stays in `AuditCaptureExcludedTypes` (it already is, L581). `EmailOutbox` and `EscalationEvent` are added to the same list as operational logs, each with a justification comment (approved by the user 2026-11-11). `EscalationRule`, `LabWorkingHours`, `UserNotificationPreference` and `MandatoryNotificationType` are audited by default.

## Review Focus

1. **The transaction that caused a notification rolls back.** No notification, no outbox row, no email, no live signal may survive. Tasks 2, 3, 7.
2. **The worker runs twice at once, or a pass is repeated after a crash.** No duplicate notification, no email sent twice, no escalation fired twice. Tasks 2, 3, 6.
3. **A threshold that spans a weekend and a public holiday.** 8 working hours submitted on the last working afternoon fire on the next working day at the right minute, in laboratory time, across a daylight-saving change. Task 5.
4. **A user removed from a section while connected.** They stop receiving that section's signals without reconnecting, and the API already refuses the data. Task 7.
5. **A user's own save.** The "changed by someone else" banner must not appear for it, including when they have the record open in two tabs (the other tab does show it). Task 9.

## File Structure

| File | Responsibility |
|---|---|
| `backend/MicroLIMS.Application/Services/Notifications/NotificationCatalog.cs` | Types, default severity, `EmailAllowed`, title templates |
| `…/Notifications/NotificationPublisher.cs` (+ `INotificationPublisher`) | The only writer; dedupe; preference lookup; outbox row |
| `…/Notifications/NotificationRecipients.cs` | Who is told for each event |
| `…/Notifications/NotificationScheduler.cs` | Time-based items, resolution, retention, digest |
| `…/Notifications/NotificationQueryService.cs` | Centre list, counts, mark read |
| `…/Notifications/EmailOutboxSender.cs`, `NotificationEmailComposer.cs` | Sending and text |
| `…/Notifications/EscalationService.cs`, `WorkingTimeCalculator.cs` | Rules and working time |
| `…/Abstractions/Live/ILiveSignalPublisher.cs`; `Persistence` post-commit queue | Signals |
| `backend/MicroLIMS.API/Hubs/LiveHub.cs`, `SignalRLiveSignalPublisher.cs`, `BackgroundServices/NotificationWorker.cs` | Hub, publisher, worker |
| `backend/MicroLIMS.API/Controllers/NotificationsController.cs`, `NotificationPreferencesController.cs`, `EscalationRulesController.cs`, `WorkingHoursController.cs` | HTTP |
| `frontend/src/contexts/LiveContext.tsx`, `frontend/src/hooks/useLiveRefresh.ts`, `useRecordChanged.ts` | Connection, refetch-on-signal with polling fallback |
| `frontend/src/modules/notifications/` | Centre page, preferences, service |
| `frontend/src/modules/laboratoryConfiguration/escalation/` | Rules and working-hours pages |
| `frontend/src/components/RecordChangedBanner.tsx` | Banner |

---

### Task 1: Data model, catalogue, migration

**Files:** Create entities `EmailOutbox`, `UserNotificationPreference`, `MandatoryNotificationType`, `EscalationRule`, `EscalationEvent`, `LabWorkingHours`, enums, configurations (including a new `NotificationLogConfiguration.cs`; none exists), `NotificationCatalog.cs`, migration `AddNotificationCentre`; Modify `NotificationLog.cs`, DbContext, `PermissionConstants.cs`, `SystemSettingDefinitions.cs`; Tests `backend/MicroLIMS.Tests/IntegrationTests/NotificationSchemaPostgresTests.cs`, `UnitTests/NotificationCatalogTests.cs`

**Interfaces — Produces:**
```csharp
// NotificationLog adds: string? Title; string? TargetType; int? TargetId; int? SectionId; string? DedupeKey; DateTime? ReadAt; DateTime? ResolvedAt
// EmailOutbox: Id, NotificationLogId?, UserId?, ToAddress, Subject, Body, Kind (Immediate|Digest|System), CreatedAt, NextAttemptAt, Attempts, SentAt?, FailedAt?, LastError (≤500)
// UserNotificationPreference: UserId, Type, EmailMode (Off|Immediate|Digest)   PK (UserId, Type)
// MandatoryNotificationType: SectionId, Type, SetByUserId, SetAt               PK (SectionId, Type)
// EscalationRule: Id, SectionId, Trigger, ThresholdWorkingHours (decimal 1–999), NotifyRoleType, IsActive, CreatedByUserId, CreatedAt, RetiredAt?, RetiredByUserId?, ReplacesRuleId?
// EscalationEvent: Id, EscalationRuleId, RecordType, RecordId, FiredAt, RecipientUserIds (jsonb string), NoRecipient
// LabWorkingHours: DayOfWeek (0–6, PK), IsWorkingDay, StartMinute, EndMinute    (minutes from local midnight)
public enum EscalationTrigger { ReviewWaiting, ApprovalWaiting, ReturnedNotCorrected, IncubationNotRead, TestingNotStarted, DocumentTaskOverdue }
public sealed record NotificationTypeInfo(string Type, string DefaultSeverity, bool EmailAllowed, string TitleTemplate);
public static class NotificationCatalog { public static IReadOnlyDictionary<string, NotificationTypeInfo> Types { get; } public static NotificationTypeInfo Get(string type); }
```
Indexes: `NotificationLogs (UserId, IsRead, CreatedAt DESC)`; unique `NotificationLogs (UserId, DedupeKey)` filtered `"DedupeKey" IS NOT NULL AND "ResolvedAt" IS NULL`; `EmailOutbox (NextAttemptAt)` filtered `"SentAt" IS NULL AND "FailedAt" IS NULL`; unique `EscalationEvents (EscalationRuleId, RecordType, RecordId)`; `EscalationRules (SectionId, Trigger)` filtered `"RetiredAt" IS NULL`. Check constraint `StartMinute < EndMinute` when `IsWorkingDay`. Catalogue = the types in spec 5.2 plus every `Type` string written today (`PendingPreparationConfigApproval`, `TestReturnedForRevision`, `TestReturnedForBiochemical`, `DiscussionPostUpdated`, `DiscussionComment`, `DocumentReassigned`, `ReassignmentOverdue`).

- [ ] **Step 1: Failing tests:** `SecondUnresolvedRowWithSameUserAndDedupeKey_Rejected`; `SameKeyAfterResolve_Allowed`; `NullDedupeKey_NotLimited`; `SecondEscalationEventForSameRuleAndRecord_Rejected`; `WorkingDayWithStartAfterEnd_Rejected`; `ExistingNotificationRows_StillLoad`; `Migration_GrantsNotificationsConfigureToSectionHeadAndQcManager`; unit `EveryTypeWrittenInApplicationCode_IsInCatalog` (scan Application for string literals assigned to `NotificationLog.Type`); `OnlyTheSixTypes_AllowEmail`.
- [ ] **Step 2–4:** FAIL → implement → PASS, 0 skipped; `AppendOnlyTriggerCatalogPostgresTests` pass; `has-pending-model-changes` → none.
- [ ] **Step 5:** Commit `feat(notifications): data model and catalogue`.

### Task 2: Publisher, recipients, event wiring

**Files:** Create `INotificationPublisher.cs`, `NotificationPublisher.cs`, `NotificationRecipients.cs`; Modify `SampleReviewService.cs` (`AutoSubmitForReviewIfReadyAsync` L74–88; `CompleteReviewAsync` L97–147; `ReturnOrderAsync` L301–380), `SampleApprovalService.cs` (`DecideAsync` L138–388), `SampleAssignmentService.cs` (`ApplyAsync` L152–187), `DocumentReviewService.cs` (L145), `DocumentApprovalService.cs` (L135), `TrainingAssignmentService.cs` (L249, L359), `DocumentReassignmentService.cs` (L538–549), `DiscussionService.cs` (L608, L661), `MessageService.cs` (L448), `PathogenConfirmationRecorder.cs` (L832); Test `backend/MicroLIMS.Tests/IntegrationTests/NotificationPublisherPostgresTests.cs`

**Interfaces — Produces:**
```csharp
public sealed record NotificationRequest(string Type, IReadOnlyCollection<int> UserIds, string Message, string? TargetType, int? TargetId,
    int? SectionId, string? DedupeKey, int? SampleId = null, int? TestOrderId = null, string? Severity = null, DateTime? DueAtUtc = null);
public interface INotificationPublisher
{
    Task PublishAsync(NotificationRequest request, CancellationToken ct = default);          // adds rows to the caller's DbContext; does NOT call SaveChanges
    Task ResolveAsync(string dedupeKey, CancellationToken ct = default);                    // sets ResolvedAt for every user's open row with this key
}
public sealed class NotificationRecipients
{
    Task<IReadOnlyCollection<int>> ReviewersAsync(int sectionId, IReadOnlyCollection<int> testOrderIds, CancellationToken ct = default);   // Samples.Review holders in section, minus users who performed any of the tests
    Task<IReadOnlyCollection<int>> ApproversAsync(int sectionId, IReadOnlyCollection<int> testOrderIds, CancellationToken ct = default);
    Task<IReadOnlyCollection<int>> RoleInSectionAsync(RoleType role, int sectionId, CancellationToken ct = default);
}
```
`PublishAsync` per user: skip when an unresolved row with the same `DedupeKey` exists (query + the unique index as backstop); add the `NotificationLog`; when the catalogue type allows email, resolve the email mode (mandatory for the section → `Immediate`; else the user's preference; else default `Immediate`) and, for `Immediate` with a non-blank `User.Email`, add an `EmailOutbox` row in the same unit of work. Dedupe keys: `ReviewWaiting:sample:{id}:section:{id}`, `ApprovalWaiting:sample:{id}:section:{id}`, `TestReturned:test:{id}`, `TestAssigned:test:{id}:user:{id}`, `DocumentTask:review:{id}`, `DocumentTask:approval:{id}`, `TrainingAssigned:{id}`. Events also resolve: completing a review resolves `ReviewWaiting…`; a decision resolves `ApprovalWaiting…`; a new active reading resolves `TestReturned…`. Existing direct `_db.NotificationLogs.Add` calls in the listed services are replaced by `PublishAsync`; `INotificationService.NotifyAsync` (in-memory push) call sites are removed in Task 7.

- [ ] **Step 1: Failing tests:**
  - `SubmitForReview_NotifiesReviewersOfThatSection_NotOtherSections_NotThePerformer`
  - `SubmitForReview_Twice_OneNotificationPerReviewer`
  - `CompleteReview_ResolvesReviewWaiting_AndNotifiesApprovers`
  - `ReturnTest_NotifiesAssignedAnalyst_WithReasonNotInEmail` (type has no email; assert no outbox row)
  - `Decide_ResolvesApprovalWaiting`
  - `AssignAnalyst_NotifiesAssignee_NotWhenAssigningToSelf`
  - `DocumentReviewTask_ApprovalTask_TrainingAssignment_EachNotifyTheirUser`
  - `CausingTransactionRollsBack_NoNotificationRows_NoOutboxRows` *(Review Focus 1)* (throw after publish inside `UnitOfWork.RunAsync`)
  - `MessageDoesNotContainResultValue` (seed a distinctive result; search all notification text)
  - `UnknownType_Throws`
- [ ] **Step 2–4:** FAIL → implement → PASS, 0 skipped. **Step 5:** Commit `feat(notifications): event-driven publishing`.

### Task 3: Scheduler, worker, outbox sender; remove on-request computation

**Files:** Create `NotificationScheduler.cs`, `EmailOutboxSender.cs`, `NotificationEmailComposer.cs`, `NotificationWorker.cs`; Modify `BackgroundJobCatalog.cs`, `DashboardNotificationService.cs` (remove `ComputeAsync` L132–226 and `PersistAndDeliverAsync` L236–271; `GetNotificationsAsync` becomes a read of persisted rows), `ServiceCollectionExtensions.cs` (remove `NotificationRecomputeThrottle`, L91), delete `NotificationRecomputeThrottle.cs`; Tests `NotificationSchedulerPostgresTests.cs`, `EmailOutboxPostgresTests.cs`

**Interfaces — Produces:**
```csharp
public sealed class NotificationScheduler { Task<int> RunAsync(CancellationToken ct = default); }      // returns notifications created
public sealed class EmailOutboxSender   { Task<int> SendDueAsync(int max = 50, CancellationToken ct = default); }   // returns emails sent
public static class NotificationEmailComposer { public static (string Subject, string Body) Compose(NotificationLog n, string appBaseUrl, string timeZoneId); public static (string Subject, string Body) ComposeDigest(IReadOnlyList<NotificationLog> items, …); }
```
`NotificationWorker` follows `SolutionPreparationExpiryWorker` (`ProcessName`, `Interval = 5 min`, `ScheduledJobPass.RunAsync`, `SemaphoreSlim(1,1)`): scheduler pass, then `SendDueAsync`. Scheduler items, each published with a dedupe key and resolved when no longer true: `IncubationReady` (per incubation, via `SampleWorkflowQueues.IsIncubationReadyToRead`, to the assigned analyst and the starter); `MediaExpiry` / `SolutionExpiry` / `StrainExpiry` within `lookAheadDays` (to the preparer and to `RoleInSection(SectionHead)`); `CalibrationDue` (`EquipmentInventory.CalibrationDueDate`, to holders of `Equipment.WorkRecord`); `ItemOverdue` (the existing 24-hour rule via `SampleWorkflowQueues`, to the assigned analyst or the section's reviewers/approvers according to the status); pending configuration approvals as today. Also: retention purge of rows older than `retentionDays`; the digest once per lab-local day at or after `digestHourLocal`, one `EmailOutbox` row of kind `Digest` per user with unread digest-mode items (guard: a `Digest` row for that user created the same lab date). `SendDueAsync` claims rows with `SELECT … FOR UPDATE SKIP LOCKED`, sends through `IEmailSender`, and in a **separate** save marks `SentAt` or increments `Attempts` and sets `NextAttemptAt`; after the fifth failure sets `FailedAt`. Email goes to `User.Email`; the `{username}@microlims.local` construction (L266) is deleted.

- [ ] **Step 1: Failing tests:**
  - `Run_CreatesExpiryNotification_ForUserWithNoSessionOrLogin`
  - `Run_Twice_NoDuplicates`; `TwoSchedulersConcurrently_NoDuplicates` *(Review Focus 2)*
  - `LotNoLongerExpiring_Resolved`; `IncubationRead_ResolvesIncubationReady`
  - `ItemOverdue_At24h01_Created_At23h59_Not` (fake clock)
  - `RetentionPurge_RemovesOldRows_KeepsRecent`
  - `Outbox_SentOnce_WhenTwoSendersRun`; `Outbox_FailedSend_SchedulesNextAttempt_1_5_15_60_240`; `Outbox_FifthFailure_MarksFailed_StopsRetrying`
  - `Outbox_UsesUserEmail_NeverMicrolimsLocal`; `UserWithoutEmail_NoOutboxRow_InAppStillCreated`
  - `EmailBody_HasTypeIdentifierDueLinkAndPreferenceFooter_NoResultValue`
  - `Digest_OneEmailPerUserPerLabDay_SkippedWhenNothingUnread_SentAtConfiguredLocalHour`
  - `SendFailure_DoesNotRollBackNotifications`
  - `GetNotifications_NoLongerComputes` (seed a condition, call the endpoint, assert no new rows)
  - `WorkerIsListedInBackgroundJobs_AndRecordsRuns`
- [ ] **Step 2–4:** FAIL → implement → PASS, 0 skipped; `ClockUsageTests` pass.
- [ ] **Step 5:** Commit `feat(notifications): scheduled generation and email outbox`.

### Task 4: Notification centre and preferences

**Files:** Create `NotificationQueryService.cs`, `NotificationsController.cs` (`api/notifications`), `NotificationPreferencesController.cs`; frontend `modules/notifications/NotificationsPage.tsx`, `NotificationPreferences.tsx`, `services/notificationService.ts`; Modify `Header.tsx` (L95–161 bell; keep its endpoints working by delegating to the new service), `Profile.tsx`, `AppRoutes.tsx`, admin Integrations page (outbox health, users without email); Tests `NotificationCentrePostgresTests.cs`, frontend tests beside each file

**Interfaces — Produces:** `GET api/notifications?state=unread|read|done|all&type=&sectionId=&q=&page=&pageSize=` → `PagedResult<NotificationDto>` (`Id, Type, Title, Message, Severity, CreatedAt, ReadAt, ResolvedAt, Target { type, id, sampleId, testOrderId }, SectionName`); `GET api/notifications/summary` → `{ unread }`; `POST api/notifications/{id}/read`; `POST api/notifications/read-all`; `GET/PUT api/notification-preferences` → rows `{ type, title, emailAllowed, emailMode, mandatory, mandatoryReason }`; `GET/PUT api/notification-preferences/mandatory?sectionId=` (policy `NotificationsConfigure`, caller must be a member of the section); `GET api/admin/email-outbox/summary` → `{ waiting, failed, lastSentAt, usersWithoutEmail[] }` (policy `UsersManage`). A user can read and mark only their own notifications; another user's id answers 404. Setting `emailMode` on a mandatory type is refused.

- [ ] **Step 1: Failing tests:** backend `List_OnlyMine_FilterByStateTypeSection_Paged_ClampedTo100`; `MarkRead_SetsReadAt`; `MarkRead_OtherUsersNotification_404`; `Summary_CountsUnreadUnresolvedOnly`; `Preference_DefaultImmediateForEmailTypes`; `Preference_SetDigest_StopsImmediateOutboxRows`; `Mandatory_OverridesUserOff`; `Mandatory_SetWithoutPermissionOrForOtherSection_Refused`; `SetEmailModeOnNonEmailType_Refused`. Frontend `page lists, filters, pages, marks read, opens the target route`; `resolved items appear under "Done"`; `bell shows 10 newest and "See all"`; `preferences: in-app shown as always on; email choices only for email types; mandatory row locked with the reason`; `empty, loading, error states`; `admin card shows waiting/failed counts and users without email`.
- [ ] **Step 2–4:** FAIL → implement → PASS; lint, `tsc -b`, `debt:check`. **Step 5:** Commit `feat(notifications): centre page and preferences`.

### Task 5: Working hours

**Files:** Create `WorkingTimeCalculator.cs`, `WorkingHoursController.cs` (`api/working-hours`), frontend `escalation/WorkingHoursPage.tsx`; Tests `backend/MicroLIMS.Tests/UnitTests/WorkingTimeCalculatorTests.cs`, `WorkingHoursPostgresTests.cs`, frontend test

**Interfaces — Produces:**
```csharp
public sealed record WorkingDay(DayOfWeek Day, bool IsWorkingDay, int StartMinute, int EndMinute);
public static class WorkingTimeCalculator
{
    // Working time elapsed between two instants, in the laboratory's time zone, skipping non-working days and dates.
    public static TimeSpan Elapsed(DateTime fromUtc, DateTime toUtc, IReadOnlyList<WorkingDay> week, IReadOnlySet<DateOnly> nonWorkingDates, TimeZoneInfo zone);
    public static bool IsConfigured(IReadOnlyList<WorkingDay> week);     // at least one working day
}
```
`GET/PUT api/working-hours` (PUT: `NotificationsConfigure`; all seven days in one request; audited by default). Non-working dates come from the existing `LabCalendarDate` (its own screen is not changed; the page links to it). The page states the time zone from `LabClock`.

- [ ] **Step 1: Failing tests (zone `Africa/Cairo`, week Sun–Thu 08:00–16:00):** `SameDay_WithinHours`; `StartsBeforeOpening_CountsFromOpening`; `EndsAfterClosing_CountsToClosing`; `AcrossNight_SkipsClosedHours`; `AcrossWeekend_SkipsFridayAndSaturday`; `AcrossNonWorkingDate_SkipsIt`; `EightHoursFromThursday1500_IsReachedSunday1500` *(Review Focus 3)*; `AcrossDaylightSavingChange_UsesLocalClockTimes` (use a zone and dates with a transition, e.g. `Europe/London` 2026-03-29); `ToBeforeFrom_Zero`; `NoWorkingDays_IsConfiguredFalse_ElapsedZero`; Postgres `Put_ReplacesAllSeven_Audited`; `Put_StartAfterEnd_FieldError`; `Put_WithoutPermission_403`. Frontend `seven rows with a working-day switch and start/end times; time zone shown; validation messages inline`.
- [ ] **Step 2–4:** FAIL → implement → PASS. **Step 5:** Commit `feat(escalation): working-hours calendar and calculator`.

### Task 6: Escalation rules

**Files:** Create `EscalationService.cs`, `EscalationRulesController.cs` (`api/escalation-rules`), frontend `escalation/EscalationRulesPage.tsx`, `EscalationRuleDialog.tsx`; Modify `NotificationScheduler.cs` (call `EvaluateAsync`), `menuConfig.ts`; Tests `EscalationPostgresTests.cs`, frontend tests

**Interfaces — Produces:**
```csharp
Task<IReadOnlyList<EscalationRuleDto>> ListAsync(int userId, int? sectionId, bool includeRetired, CancellationToken ct = default);
Task<EscalationRuleDto> CreateAsync(int userId, int sectionId, EscalationTrigger trigger, decimal thresholdWorkingHours, RoleType notifyRole, CancellationToken ct = default);
Task<EscalationRuleDto> ReplaceAsync(int userId, int ruleId, decimal thresholdWorkingHours, RoleType notifyRole, CancellationToken ct = default);   // retires old, creates new with ReplacesRuleId
Task RetireAsync(int userId, int ruleId, CancellationToken ct = default);
Task<int> EvaluateAsync(CancellationToken ct = default);          // returns escalations fired
```
Waiting-since per trigger: `ReviewWaiting` → submitted-for-review time; `ApprovalWaiting` → reviewed time; `ReturnedNotCorrected` → `TestReturnEvent.ReturnedAt`; `IncubationNotRead` → the moment it became ready; `TestingNotStarted` → `Sample.ReceivedAt` while no test has a recorded step; `DocumentTaskOverdue` → the task's due date. A rule fires when `WorkingTimeCalculator.Elapsed(since, now) ≥ threshold`; it inserts the `EscalationEvent` first (the unique index makes a concurrent second insert fail and skip) and publishes an `Escalation` notification to `RoleInSectionAsync(rule.NotifyRoleType, rule.SectionId)` with dedupe key `Escalation:rule:{id}:{recordType}:{recordId}`; no recipients → `NoRecipient = true` plus a notification to System Administrators. The record's cause ending resolves the notification. `CreateAsync` is refused while working hours are not configured ("Set the laboratory working hours first."), for a section the caller is not a member of, for a duplicate active rule with the same section, trigger and role, and for a threshold outside 1–999. Policy `NotificationsConfigure`. Escalation changes no status.

- [ ] **Step 1: Failing tests:**
  - `Create_Audited`; `Create_WithoutWorkingHours_Refused`; `Create_ForSectionIAmNotIn_Refused`; `Create_DuplicateActiveRule_Refused`; `Threshold0or1000_Refused`
  - `Replace_RetiresOld_CreatesNewLinked_OldStillListedWithIncludeRetired`
  - `Evaluate_FiresAtThresholdInWorkingHours_NotBefore` (review submitted Thursday 15:00, 8 h rule → not fired Friday, fired Sunday 15:00)
  - `Evaluate_FiresOncePerRecord_EvenAcrossPasses_AndConcurrentPasses` *(Review Focus 2)*
  - `Evaluate_OtherSectionRecord_NotFired`; `RetiredRule_DoesNotFire`; `ReplacedRule_NewThresholdApplies_RecordsAlreadyEscalatedByOldRuleFireAgainOnlyUnderNewRule` (document this behaviour in the test name; it follows from the per-rule unique index)
  - `ReviewCompleted_ResolvesEscalationNotification`
  - `NoMemberWithRole_EventMarkedNoRecipient_AdminsNotified`
  - `Escalation_EmailsRecipients_AndChangesNoSampleField` (compare the sample row before and after)
  - one test per trigger proving its "waiting since" source
  - Frontend `list by section with trigger, hours, role, status`; `dialog validates hours and shows "working hours" unit with a link to the calendar`; `edit says "This replaces the rule; the old one is kept in history"`; `retire asks for confirmation`; `blocked state when working hours are missing, with the reason and a link`.
- [ ] **Step 2–4:** FAIL → implement → PASS, 0 skipped. **Step 5:** Commit `feat(escalation): section escalation rules`.

### Task 7: Live signals — backend

**Files:** Create `ILiveSignalPublisher.cs`, `LiveHub.cs`, `SignalRLiveSignalPublisher.cs`; Modify `MicroLimsDbContext.cs` (`SaveChangesAsync` L546–559 and `SaveChanges` L531–543: after the commit at L558, flush a per-context list of pending signals; when the context is enlisted in an outer transaction, flush on that transaction's commit via `UnitOfWork`, L48), `IMicroLimsDbContext.cs` (`void QueueSignal(string kind, string key)`), `UnitOfWork.cs`, `JwtAuthenticationExtensions.cs` (L21–50: `OnMessageReceived` reads `access_token` **only** for paths starting `/hubs/live`), `Program.cs` (`AddSignalR`; `MapHub<LiveHub>("/hubs/live")` after L222), `NotificationPublisher.cs` (queue `notifications:user:{id}`), `SectionCountersService` writers / the services in Task 2 (queue `counters:section:{id}`, `record:{type}:{id}`), delete `Infrastructure/Notifications/NotificationService.cs` and `INotificationService` with its remaining call sites; Test `backend/MicroLIMS.Tests/IntegrationTests/LiveSignalsPostgresTests.cs`

**Interfaces — Produces:**
```csharp
public sealed record LiveSignal(string Kind, string Key, int? ActorUserId);     // Kind: "notifications" | "counters" | "record" | "mywork" | "messages"
public interface ILiveSignalPublisher { Task PublishAsync(IReadOnlyCollection<LiveSignal> signals, CancellationToken ct = default); }
// Hub methods (client → server): Task WatchRecord(string type, int id); Task UnwatchRecord(string type, int id)
// Client event (server → client): "signal" with { kind, key, actorUserId }
```
Groups: `user:{id}` joined on connect; `section:{id}` for each readable section, **re-evaluated on every `WatchRecord` call and every 5 minutes per connection**, and dropped when membership ends; `record:{type}:{id}` joined by `WatchRecord` only after the same read-access check the record's GET endpoint uses (types allowed: `sample`, `testOrder`, `document`, and the configuration types that carry a version). A publisher failure is logged and swallowed; it never fails the request. Signals queued in a context whose transaction rolls back are discarded. `RequestLoggingMiddleware` logs `Path` only (L21–22), so the token in the query string is not logged; a test pins that.

- [ ] **Step 1: Failing tests (in-process test server + SignalR client):**
  - `Connect_WithoutToken_Rejected`; `Connect_WithValidToken_ReceivesOwnUserSignals_NotAnotherUsers`
  - `SectionSignal_ReachesMembersOnly`
  - `RemovedFromSection_StopsReceivingAfterNextReevaluation` *(Review Focus 4)*
  - `WatchRecord_WithoutReadAccess_Refused_NoSignals`
  - `Signal_PublishedAfterCommit`; `Signal_NotPublishedWhenTransactionRollsBack` *(Review Focus 1)*; `Signal_InsideUnitOfWork_PublishedOnceOnOuterCommit`
  - `SignalPayload_HasOnlyKindKeyActor` (serialise and compare property names)
  - `PublisherThrows_RequestStillSucceeds`
  - `RevokedSession_ConnectionClosed` (hook into the existing `AccessTokenRevalidator` result)
  - `AccessTokenQueryParameter_IgnoredOutsideHubPath`
  - `RequestLog_ForHubPath_ContainsNoToken`
- [ ] **Step 2–4:** FAIL → implement → PASS, 0 skipped; `AuditClockPostgresTests` and the audit atomicity tests still pass (the `SaveChanges` override changed).
- [ ] **Step 5:** Commit `feat(live): data-free signals over SignalR after commit`.

### Task 8: Live signals — frontend, timers replaced

**Files:** Create `LiveContext.tsx`, `useLiveRefresh.ts` with tests; Modify `frontend/package.json` (`@microsoft/signalr`, pinned), `main.tsx` (mount `LiveProvider` inside `AuthProvider`), `Header.tsx` (L101–104, L134–138), `hooks/useSectionCounters.ts` (L25), `modules/messages/MessagesPage.tsx` (L50), `components/ChatWindow.tsx` (L57), My Work `useMyWork` summary (C1), `MessageService.cs`/`DiscussionService.cs` (queue `messages:user:{id}`)

**Interfaces — Produces:**
```ts
export function LiveProvider(props: { children: ReactNode }): JSX.Element;
export function useLive(): { status: "connected" | "connecting" | "offline"; subscribe(kind: string, key: string | null, fn: (s: LiveSignal) => void): () => void; watchRecord(type: string, id: number): () => void };
export function useLiveRefresh(opts: { kind: string; key?: string | null; refetch(): void; fallbackMs: number; enabled?: boolean }): void;
```
`useLiveRefresh`: when connected, calls `refetch` on a matching signal (coalesced to at most one call per second) and once on every reconnect; when not connected, polls every `fallbackMs` while the tab is visible (the timers' current values: 60 s; chat 6 s and 10 s). The connection uses `accessTokenFactory` returning the current token and `withAutomaticReconnect`; it is stopped on logout. `SystemStatusContext` polling and the countdown clocks are not changed.

- [ ] **Step 1: Failing tests (a fake connection object injected through `LiveProvider`'s test prop; no project hook mocked):** `connected: a matching signal triggers refetch; a non-matching one does not`; `burst of 10 signals → one refetch`; `offline: polls at fallbackMs; hidden tab: no polling`; `reconnect: refetches once and stops polling`; `logout stops the connection`; `Header badge updates on a notifications signal without waiting 60 s`; `"Live updates paused" indicator appears when offline and has an accessible name`; `chat list still refreshes every 10 s when the connection is down`.
- [ ] **Step 2–4:** FAIL → implement → PASS; lint, `tsc -b`, `debt:check`. **Step 5:** Commit `feat(live): signal-driven refresh with polling fallback`.

### Task 9: "Changed by someone else" banner

**Files:** Create `RecordChangedBanner.tsx`, `useRecordChanged.ts` with tests; Modify the sample workflow dialogs, review and approval screens, `DocumentDetailPage.tsx`, and the configuration dialogs whose services call `ifMatch(` (list in the plan recon: `SpecificationService`, `EquipmentConfigurationService`, `SolutionMasterService`, `MaterialMasterService`, `EMConfigService`, `WaterConfigService`, `ItemService`, `AfterCleaningConfigService`); backend: queue `record:{type}:{id}` with `ActorUserId` in the save paths of those entities (in `MicroLimsDbContext` from the modified entries of the mapped types, so no service is edited one by one)

**Interfaces — Produces:**
```ts
export function useRecordChanged(type: string, id: number | null): { changed: boolean; byMe: boolean; at: string | null; dismiss(): void };
// RecordChangedBanner props: { type: string; id: number | null; dirty: boolean; onReload(): void }
```
The signal carries the actor id only; the banner text is "This record was changed by another user at {time}. Reload to see the latest." (the name is fetched with the reload, never pushed). A signal whose `actorUserId` is the current user **and** whose arrival falls within 5 seconds of this tab's own successful save is ignored; a signal from the same user in another tab is shown.

- [ ] **Step 1: Failing tests:** `banner appears on a signal from another user`; `not for this tab's own save; shown for the same user's other tab` *(Review Focus 5)*; `Reload with dirty form asks "Discard your unsaved changes?" with Cancel focused`; `the page never reloads by itself and form values are untouched while the banner shows`; `banner has role="status" and a Reload button reachable by keyboard`; `unwatches on unmount`; backend `SavingASample_QueuesRecordSignalWithActor`; `TypesNotInTheMap_QueueNothing`.
- [ ] **Step 2–4:** FAIL → implement → PASS. **Step 5:** Commit `feat(live): record-changed banner`.

### Task 10: Final verification and documents

- [ ] **Step 1:** Backend Release build; full suite without and with PostgreSQL; exact counts; `has-pending-model-changes` → none.
- [ ] **Step 2:** Frontend lint, `tsc -b`, test, build, `debt:check`.
- [ ] **Step 3:** Run the app in two browsers: submit for review in one, watch the badge in the other; stop the hub and confirm polling takes over; create a working calendar and a rule with a short threshold and watch it fire; point SMTP at a local catcher and read the email; open one sample in both and save in one. Report what was and was not exercised; state that no real mail server was used.
- [ ] **Step 4:** `documents/` requirements, user guide, SOP notes (notifications are aids, not records; escalation rules and working hours are controlled configuration). Roadmap links.
- [ ] **Step 5:** Commit `docs(notifications): requirements, guide, roadmap links`.

## Manual gates (site)

- Mail server address, sender and a delivery test to real mailboxes.
- Hosting confirms WebSocket support end to end (the fallback covers its absence).
- The laboratory enters its working hours, non-working dates and escalation rules.

## Cross-plan corrections (whole-programme review, 2026-11-11)

These corrections come from reviewing all plans together against the code. Where they differ from the text above, they win. Shared names are defined once in `docs/superpowers/plans/2026-11-11-build-order.md` (section "Programme conventions").

1. Task 8 publishes the `mywork` live signal whenever a My Work source changes for a user (assignment, take/release, review submission, incubation ready) through `IMicroLimsDbContext.QueueSignal`.
2. Escalation with no recipient notifies holders of `Users.Manage` (the System Administrator role).
3. Task 9's mapped notification types: ReviewWaiting, ApprovalWaiting, ReturnedToAnalyst, IncubationReady, DocumentTaskDue, Escalation.
4. Audit capture for `EmailOutbox` and `EscalationEvent` is approved (user, 2026-11-11).

## Review corrections (independent review, 2026-11-11)

From the independent review of the build order. These win over the text above and over the earlier corrections section where they differ.

1. Build after C1 (Task 8 edits C1's `useMyWork`). Task 2 precedes C8 Task 5.
