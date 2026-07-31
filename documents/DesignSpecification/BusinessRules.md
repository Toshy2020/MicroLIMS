# MicroLIMS — Business Rules (Frozen Logic)

Every rule below is implemented in exactly one place in the codebase.
If a rule needs to change, change it there — never re-implement it in
a controller, a frontend component, or a second service. File
references are given so this document and the code can be cross-checked.

---

## 1. Frozen Architectural Principles

1. **Configuration drives behavior.** The Section Head's Master
   Configuration (`Item.AssignedTests`) determines what tests are
   auto-created — nothing is created manually.
2. **Workflow before implementation.** Every feature is discussed,
   prototyped, approved, and frozen before coding.
3. **Backend owns laboratory logic.** The frontend never implements
   GMP or microbiology rules — it only collects input and displays
   what the backend decides.
4. **Role separation.** System administration and laboratory
   administration remain separate (`RoleType` enum: SystemAdministrator,
   SectionHead, Reviewer, Analyst).
5. **Traceability.** Every action is auditable
   (`MicroLimsDbContext.SaveChanges` + `WorkflowHistory`).
6. **Consistency.** Product, Water, EM, and After Cleaning share a
   common workflow-engine shape (`IStatefulWorkflowEngine`) while
   keeping domain-specific rules.
7. **Automation.** Repetitive laboratory tasks (test order generation,
   audit logging, average calculation) are automated.

---

## 2. Automatic Test Order Generation

**File:** `Application/Workflows/ProductWorkflowEngine.cs` (and the
Water/EM/AfterCleaning equivalents)

```
Receive Sample → Read Item Configuration → Generate Test Orders → Assign Status → Return Workspace Cards
```

- A sample cannot be received against an Item with zero assigned
  tests — `ProductWorkflowEngine.ReceiveAsync` throws.
- One `TestOrder` is created per configured test, starting at
  `WorkflowStep.Waiting` / `ApprovalStatus.Pending`.
- **No user, at any role, can manually create a TestOrder.** The only
  entry points are the five `ReceiveAsync` methods on the workflow
  engines.

---

## 3. Pathogen Engine

**File:** `Application/Workflows/PathogenWorkflowEngine.cs`

**Universal chain** (any pathogen except Salmonella):

```
TSB → Observation → Continue → Detection Media → Growth = Detected / No Growth = Absent
```

- The chain completes as soon as one `PathogenObservation` is recorded.
- The interpretation is the growth/no-growth value of that single observation.

**Salmonella exception:**

```
TSB → RVS → XLD+TSI → Detected / Absent
```

- Steps must be recorded strictly in order (`TSB` → `RVS` → `XLD_TSI`).
  Recording out of order throws `InvalidOperationException`
  ("workflow order violation").
- A **negative result at TSB or RVS closes the chain early** as
  `Absent` — no further steps can be recorded once closed.
- Final result is `Detected` only if TSB, RVS, and XLD+TSI are all
  positive/positive/positive respectively; otherwise `Absent`.
- `InterpretAsync` throws if called before the chain is complete —
  there is no partial/best-guess interpretation.

---

## 4. Water Calculation Engine

**File:** `Application/Workflows/WaterWorkflowEngine.cs`

```
Enter readings → Average → Compare against Alert → Action → Specification limits
```

- Comparison order of severity, most severe first: **Specification →
  Action → Alert**. The first (most severe) limit exceeded wins; a
  result can only be flagged with one status.
- Status values: `WithinLimits`, `AlertLimitExceeded`,
  `ActionLimitExceeded`, `OutOfSpecification`.
- Limits are read from `SamplingConfiguration`, matched by
  `TestCode` + the sample's `WaterSamplingPoint`.
- At least one numeric reading is required — throws otherwise.

---

## 5. EM (Environmental Monitoring) Engine

**File:** `Application/Workflows/EMWorkflowEngine.cs`

```
Room Selection → Incubation Step 1 → Incubation Step 2 → Average → Automatic OOT Detection
```

- Step 2 cannot start before Step 1 is completed
  (`CompletedAt is not null`) — enforced, not just UI-guided.
- A step cannot be completed twice — throws
  "workflow order violation" if attempted.
- **OOT (Out Of Trend) rule:** `(Step1Count + Step2Count) > ActionLimit`.
  This is computed automatically on Step 2 completion and persisted to
  `RoomMonitoring.IsOutOfTrend` — never left to human judgment.

---

## 6. After Cleaning Engine

**File:** `Application/Workflows/AfterCleaningWorkflowEngine.cs`

```
Machine → Selected Parts → ONE collective pathogen sample (if configured) + ONE individual TAMC per part
```

- Collective pathogen sampling is opt-in per `MachinePartConfiguration.IsCollectivePathogenSample`
  — if none of the selected parts require it, no collective TestOrder is created.
- Every selected part always gets its own individual TAMC TestOrder
  (`TestCode = "TAMC:{partName}"`), regardless of the collective sample.
- At least one part must be selected — throws otherwise.

---

## 7. GPT (Growth Promotion Test) Engine

**File:** `Application/Workflows/GptWorkflowEngine.cs`

```
Media Preparation → Sterility → Recovery → Release (or Rejected)
```

- Stages advance strictly forward, one at a time — cannot skip or go
  backward. Advancing from `Release` or `Rejected` throws.
- At the **Recovery** stage, at least one `GptChallengeResult` must be
  recorded before advancing — throws otherwise.
- **A media lot passes GPT only if every recorded challenge organism
  passed.** If any organism failed, the stage becomes `Rejected`
  instead of `Release`, and the lot can never be released.
- `Media.IsReleasedForUse` is `true` only when `GptStage == Release`
  **and** `Status == Active` **and** not expired — all three
  conditions, checked together (`IsReleasedForUseAsync`).

---

## 8. Review Engine

**File:** `Application/Services/ReviewService.cs`

- A test order cannot be reviewed before its status is `ResultEntered`
  — throws otherwise.
- Two modes, both ending in the same `Reviewed` state:
  - **Detailed** — one test order at a time, full workflow history visible.
  - **Quick table review** (`QuickReviewBatchAsync`) — reviews a batch;
    silently skips any test order in the batch that isn't eligible
    (still `Waiting`, etc.) rather than failing the whole batch.

---

## 9. Approval Engine

**File:** `Application/Services/ApprovalService.cs`

- A test order cannot be decided before its status is `Reviewed` —
  throws otherwise.
- Full decision set (`ApprovalDecision` enum): `Approve`, `Reject`,
  `RetestRetainedSample`, `NewSampleRequest`, `Investigation`,
  `OOSInvestigation`.
- **`Investigation` and `OOSInvestigation` require a non-blank comment**
  — this is enforced server-side, not just as a UI hint, because it's
  a GMP documentation requirement.
- Every decision is recorded in `WorkflowHistory` regardless of type,
  so the decision trail and the workflow-step trail are one unified feed.

---

## 10. Security Rules

**File:** `Application/Services/AuthenticationService.cs`

- **Account locking:** 5 failed login attempts locks the account for
  15 minutes (`MaxFailedAttempts` / `LockDuration`). A locked account
  cannot log in even with the correct password until the lock expires.
- **Refresh tokens** rotate on every use — the old token is revoked
  and a new one issued (`RefreshAsync`), 7-day lifetime.
- **Password reset tokens** are single-use (`UsedAt` set on
  consumption), 1-hour lifetime, and the API never reveals whether a
  given username exists (`RequestPasswordResetAsync` always returns
  a generic success message).
- Every login attempt — success or failure, with reason — is recorded
  in `LoginHistory`.

---

## 11. Audit Trail

**File:** `Persistence/DbContext/MicroLimsDbContext.cs`

- Every `Add`/`Update`/`Remove` tracked by EF Core is captured
  automatically in `AuditLog` on `SaveChanges` — **no service can
  forget to log a change**, because it isn't manual.
- Records the entity name, entity ID, action, previous value (JSON),
  new value (JSON), user ID, and UTC timestamp.
- `WorkflowHistory` additionally captures test-order-specific state
  transitions with a human-readable note.
- **Nothing is ever hard-deleted from these two tables** — treat them
  as append-only.

---

## 12. Dashboard Notification Triggers

**File:** `Application/Services/DashboardNotificationService.cs`

| Trigger | Condition | Severity |
|---|---|---|
| Media Expiry | `Media.ExpiryDate` within 7 days (or already past) | `warning` / `error` if already expired |
| Incubation Ready | An `Incubation` with `CompletedAt` set, whose `TestOrder` is still `Incubating`/`Running` | `info` |
| Approval Waiting | Any `TestOrder.Status == Reviewed` (shown to SectionHead/Admin only) | `info` |
| Review Waiting | Any `TestOrder.Status == ResultEntered` (shown to Reviewer/SectionHead/Admin) | `info` |

- Notifications are deduplicated per user within a 12-hour window (by
  exact message text) to avoid spamming the same alert repeatedly.
- `severity == "error"` notifications additionally trigger an email
  via `IEmailSender` (no-op if SMTP isn't configured) and a real-time
  push via `INotificationService`.
