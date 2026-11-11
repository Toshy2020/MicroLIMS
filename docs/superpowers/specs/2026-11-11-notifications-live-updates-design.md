# C5 — Notification Centre and Live Updates: Design Specification

**Roadmap package:** C5 (`improvement-roadmap.md`)
**Status:** Draft for user review
**Date:** 2026-11-11

## 1. Purpose

Tell the right person about waiting, overdue and expiring items even when they are not looking at the application, let each person choose how they are told, escalate what waits too long, and update screens as things happen instead of once a minute.

## 2. Decisions (confirmed by the user, 2026-11-11)

| # | Decision | Answer |
|---|---|---|
| 1 | What is emailed | Expiry and calibration warnings; overdue and escalated items. Everything else is in-app only |
| 2 | User preferences | Users choose the channel per type; a Section Head can mark types as mandatory |
| 3 | Escalation | Rules set per section by the Section Head: what, how long, who is told |
| 4 | Hosting | One API server now, built so a second can be added later; polling as fallback |

## 3. Out of scope

- SMS, Teams, Slack or mobile push.
- "Who else is viewing this record".
- Rebuilding Messages or Discussions (only their timers change).
- Escalation time limits themselves: the laboratory enters them; none is built in.
- Working-hours calendars (escalation counts clock hours; see 9.2).

## 4. Current state (from code, not run)

- **Notifications are worked out only when a user's browser asks**, at most every 5 minutes per user (`DashboardNotificationService`, throttle held in memory). A user who is not logged in gets nothing.
- Duplicates are detected by comparing message text within 12 hours, so "3 reviews waiting" and "4 reviews waiting" are two notifications.
- **Email is sent only for an expired media lot, and to `{username}@microlims.local`**, not to the user's real email address (`DashboardNotificationService.cs` L266). In practice no notification email reaches anyone.
- Emails are sent before the database save and without retry; there is no outbox.
- The bell shows the 30 newest items in a menu. There is no notifications page.
- Nine polling timers exist (bell, message count, section counters, system status, chat lists).
- No SignalR. A comment in the code already describes the current design as single-instance.
- Saving a stale record returns "changed by someone else" (HTTP 409) only at save time; nothing warns earlier.
- Document Control has its own escalation records for training; samples and tests have none.
- `User` has an optional `Email` and no notification settings.

## 5. Design

### 5.1 Where notifications come from

Two sources replace "compute on request":

- **Events.** When something happens (submitted for review, returned, approved, assigned, document task created), the service that did it adds notifications for the recipients in the same database transaction.
- **A background job every 5 minutes** for things that become true with time: incubation ready, expiry within the look-ahead, calibration due, overdue items, escalations. It runs for every user, logged in or not, and appears in the existing Background Jobs admin page.

The on-request computation and its in-memory throttle are removed.

### 5.2 The notification record

`NotificationLog` is extended (existing rows stay valid):

| Field | Purpose |
|---|---|
| `Type` | From a fixed catalogue (below) |
| `Title`, `Message` | Short text; **no result values** |
| `Target` | What to open (same typed target as My Work) |
| `DedupeKey` | For example `ReviewWaiting:sample:812`. One open notification per key per user |
| `SectionId` | For filtering and for mandatory rules |
| `ReadAt` | When it was read |
| `ResolvedAt` | Set when the cause is gone (the review was done), so stale items leave the unread list by themselves |

Catalogue: `ReviewWaiting`, `ApprovalWaiting`, `TestReturned`, `TestAssigned`, `IncubationReady`, `MediaExpiry`, `SolutionExpiry`, `StrainExpiry`, `CalibrationDue`, `ItemOverdue`, `Escalation`, `DocumentTask`, `TrainingAssigned`, `DirectMessage`, `Discussion`, `ConfigurationApprovalWaiting`, plus the existing document reassignment types.

Recipients keep today's logic (assigned analyst; holders of the review or approval permission in the section; and so on). A reviewer is not notified about a sample they cannot review because they performed a test on it.

Notifications older than `notifications.retentionDays` (default 90) are deleted by the job. They are operational messages, not GMP records; the audit trail is untouched.

### 5.3 Notification centre

- A page `/notifications`: unread and read, filter by type and section, search, paging, "Mark all as read", and each row opens its record.
- The bell keeps its badge and shows the 10 newest with a link "See all".
- Resolved items are shown greyed under "Done".

### 5.4 Email

- **Outbox.** An email is written to a new `EmailOutbox` table in the same transaction as its notification, and sent afterwards by the background job, with up to 5 attempts and increasing delays. A failed email never blocks or rolls back laboratory work.
- **Address.** The user's `Email`. A user without one gets in-app only and appears on an admin list "Users without an email address". The `@microlims.local` construction is removed.
- **Content.** Type, record identifier, section, due time and a link. No result values, no specification limits, no names of other people's work beyond what the in-app text shows. The footer says why the user received it and where to change preferences.
- **Emailed types** (decision 1): `MediaExpiry`, `SolutionExpiry`, `StrainExpiry`, `CalibrationDue`, `ItemOverdue`, `Escalation`. The catalogue marks each type as "email allowed" so this list can be widened later without code changes to the sender.
- The admin Integrations page shows outbox health: waiting, failed, last success.

### 5.5 Preferences

Under Profile → Notifications, for each type the user may receive:

| Choice | Meaning |
|---|---|
| In-app | Always on; cannot be switched off |
| Email: immediately / daily digest / off | Offered only for types that allow email |

- **Mandatory types.** A user with the new permission `Notifications.Configure` (Section Head, QC Manager) marks types as mandatory for their section. For a mandatory type the email choice is fixed to "immediately" and shown locked with the reason.
- **Daily digest** is one email at `notifications.digestTime` (default 07:00, laboratory time zone) listing the user's unread digest-type items, with counts by type. No digest is sent when there is nothing to report.
- Defaults for a new user: email "immediately" for the six emailed types.

### 5.6 Escalation rules

A rule belongs to one section and says:

| Part | Values |
|---|---|
| Trigger | Review waiting; approval waiting; returned test not corrected; incubation ready and not read; sample received and testing not started; document task past its due date |
| After | A number of hours, entered by the Section Head |
| Tell | A role in that section (Section Head, QC Manager, Reviewer) |
| How | In-app and email |

- Managed on a new page, Laboratory Configuration → Escalation rules, with `Notifications.Configure`.
- A rule is never edited in place: a change retires the old rule and creates a new one, so it is always possible to see which rule was in force when an escalation fired. All changes are audited.
- The job checks rules every 5 minutes. A rule fires **once** per record; the firing is stored (`EscalationEvent`: rule, record, time, recipients) and shown on the record's history.
- When the cause is resolved the escalation notification is marked resolved.
- **No rule exists until someone creates one.** No time limit is built in.
- Escalation tells people; it changes no status and blocks nothing.

### 5.7 Live updates

- A SignalR connection is opened after sign-in. If it cannot be established or drops, the app falls back to the current 60-second polling and retries the connection in the background. Nothing depends on it for correctness.
- **Only signals travel on the connection, never data**: "your notifications changed", "counters for section 3 changed", "sample 812 changed". The browser then asks the normal API, which applies permissions as usual. No regulated value is ever pushed.
- Signals are published **after** the database commit, never inside the transaction.
- Replaced timers: bell, message count, section counters, My Work summary, chat lists. The system-status check and the countdown clocks stay.
- The access token is sent when the connection is opened. For WebSockets this means the query string, so request logging is confirmed to exclude query strings for the hub path. The connection is closed when the session is revoked or the token can no longer be refreshed.
- The publisher sits behind one interface, so a shared backplane can be added for a second server without touching callers.

### 5.8 "This record was changed"

- When a user has a sample, test, document or configuration record open and someone else saves a change to it, a banner appears: "Changed by {name} at {time}. Reload to see the latest."
- The page is never reloaded automatically, and a form with unsaved input is never overwritten. Reload asks first if there is unsaved input.
- Save still uses the existing version check, which remains the real protection; the banner only gives earlier warning.
- First release covers: sample detail and workflow dialogs, review and approval screens, document detail, and the configuration dialogs that already send a record version.

## 6. Error handling

| Situation | Behaviour |
|---|---|
| Live connection unavailable | Polling; a small "Live updates paused" indicator; automatic retry |
| Email server down | Emails wait in the outbox and retry; after 5 failures they are marked failed and shown to administrators; in-app delivery unaffected |
| User has no email | In-app only; listed for administrators |
| Notification target no longer exists | The row opens the nearest list with a notice |
| Background job fails | Recorded by the existing job-run tracking; next pass continues; no duplicate notifications thanks to `DedupeKey` |
| Rule's role has no member in the section | The escalation is recorded as "no recipient" and shown to System Administrators |

## 7. Testing

- **`[PostgresFact]`:** event notifications created in the same transaction as their cause and rolled back with it; one notification per `DedupeKey`; resolution when the cause ends; recipients by section and permission; a reviewer who performed the test is not notified; job creates time-based notifications for a user with no session; retention purge. Outbox: written with the notification; sent once; retried with backoff; failed after 5; never sent for a type not allowing email; real address used; no result values in the body (search the body for the seeded result). Preferences: defaults; mandatory overrides user choice; digest groups and is skipped when empty. Escalation: fires once after the threshold with a fake clock; not before; not for another section; retired rule does not fire; new version does; resolved on completion; rule changes audited; `EscalationEvent` stored.
- **Live:** hub rejects an unauthenticated connection; a user receives only their own and their sections' signals; a signal is published after commit and not when the transaction rolls back; payloads contain no entity fields (schema test).
- **Frontend (`userEvent`):** centre list, filters, mark read, open target; bell badge follows a signal; fallback to polling when the connection fails and return when it recovers; preference locking for mandatory types; escalation rule form validation; changed-record banner appears for another user's change, not for one's own, and never discards unsaved input.
- **Manual:** two browsers side by side (review submitted in one, badge moves in the other); email received from the real mail server.

## 8. Delivery stages

1. Extended notification record, catalogue, event and job generation; remove on-request computation.
2. Notification centre page and bell.
3. Email outbox, real addresses, emailed types.
4. Preferences, mandatory types, daily digest.
5. Escalation rules and events.
6. Live connection, signals, timer replacement with fallback.
7. Changed-record banner.
8. Documents: requirements, user guide, SOP note (notifications support work; they are not records and do not replace checking the queue).

## 9. Open items

1. **Escalation time limits** per trigger and section: entered by the laboratory after delivery.
2. **Clock hours or working hours** for escalation. The spec uses clock hours; working-hours calendars would be a later addition.
3. **Mail server** details and sender address for the site.
4. Whether **Reviewer** should also be allowed to configure mandatory types (the spec gives this to Section Head and QC Manager).
