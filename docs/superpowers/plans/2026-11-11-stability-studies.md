# Stability Study Management (D3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Approved stability protocols with limits fixed at approval, chambers and unit counts, a pull schedule with missed-pull deviations, pulls that create normal samples judged against the protocol's limits, results grid and charts, and a signed summary report.

**Architecture:** Protocol limits are stored as ordinary `Specification` rows tagged with a `StabilityLimitSetId` and hidden from all normal reads by an EF global query filter; `SpecificationLookup` (the single resolver) switches to the pull's limit set when the sample belongs to a stability pull, so every recorder stays unchanged and `ParameterResult.SpecificationId` points at the protocol row. Pulls create samples through `ProductWorkflowEngine.ReceiveAsync` with an internal explicit test list. A background worker marks missed pulls and raises system deviations (D2).

**Tech Stack:** .NET 10, EF Core 10, PostgreSQL, xUnit `[PostgresFact]`; React 19, TypeScript, MUI, recharts 3, Vitest.

**Spec:** `docs/superpowers/specs/2026-11-11-stability-studies-design.md`

## Global Constraints

- Prerequisites: B4 (role pattern, `QaForbiddenCodes` rule), D2 (quality records), C1 optional (My Work); D1 optional (OOS works on stability samples without change).
- `CLAUDE.md` binds every task (anti-fake tests, state + audit + negative triad, `[PostgresFact]` must run, `ILabClock` / `TimeProvider`, `CancellationToken`, one migration per schema change + `has-pending-model-changes`, no new `any`, `userEvent`, WCAG 2.2 AA, design tokens, no external effects inside transactions).
- No change to how results are calculated or compared. No organism, test-code or laboratory literal in code; the stability laboratory is whatever `SectionId` the protocol names.
- `RoleType.RdManager = 6` (after B4's `Qa = 5`); name `"R&D Manager"`; display `"R&D Manager"`. Its forbidden codes = B4's `QaForbiddenCodes`; `Stability.Approve` is added to the independence-trigger set (a role holding it is subject to the same refusal).
- Permission codes exactly: `Stability.View`, `Stability.Manage`, `Stability.Pull`, `Stability.Approve`. `Stability.Manage` and `Stability.Pull` join `GxpExecutionAndApprovalCodes`.
- `EquipmentType.StabilityChamber` appended after `Caliper` (enum stored as int; never insert).
- `SignatureMeaning` values appended in order: `StabilityProtocolAuthored`, `StabilityProtocolApproved`, `StabilityAmendmentAuthored`, `StabilityAmendmentApproved`, `StabilityDiscontinued`, `StabilityTimeZeroLinked`, `StabilityPullJustified`, `StabilityConclusionSigned`, `StabilityReportApproved`; each gets a `ReportVocabulary` label.
- Numbers `STB-{yyyy}-{seq:D3}`, year from `ILabClock`, unique index, advisory-lock generation (D1 pattern).
- Seed cause of testing `"Stability"` (`RequiresTestSelection = false`) if absent; conditions seeded exactly as spec 5.1.
- Months are calendar months on `DateOnly.AddMonths` (end-of-month clamps to the last day). Working-day adjustment uses `PlanWindowCalculator.PreviousWorkingDay` with the monitoring plan's working-day source.
- Text limits: title ≤ 200; reasons, descriptions, conclusion 1–4000.

## Review Focus

1. A normal sample of the same item picks up protocol limit rows (filter missed somewhere) → `Routine_sample_never_sees_protocol_rows` across every reader listed in Task 3.
2. A pull created after a limit amendment still uses the old set, or an earlier pull switches to the new set → `Pull_uses_limit_set_in_force_at_pull_creation` (Task 6).
3. Window closes on a non-working day or at month end (31 Jan + 1 month) → `Schedule_month_end_and_non_working_target` (Task 5).
4. The missed-pull worker runs twice and opens two deviations → `Missed_pull_opens_exactly_one_deviation_on_repeated_runs` (Task 7).
5. Units drop below what remaining pulls need after a move or an extra pull → `Shortfall_reported_after_move` (Task 5).

## File Structure

| File | Responsibility |
|---|---|
| `Domain/Entities/Stability/StabilityProtocol.cs` | `StabilityProtocol`, `StabilityBatch`, `StabilityStudyCondition`, `StabilityTimePoint`, `StabilityTimePointTest`, `StabilityLimitSet`, `StabilityAmendment` |
| `Domain/Entities/Stability/StabilityOperations.cs` | `StorageCondition`, `ChamberConditionAssignment`, `StabilityPlacement`, `StabilityUnitMovement`, `StabilityPull`, `ChamberExcursion`, `StabilityReport` |
| `Domain/Enums/StabilityEnums.cs` | Enums below |
| `Domain/Entities/Specification.cs` (modify) | `StabilityLimitSetId?` |
| `Persistence/Configurations/Stability*.cs`, `MicroLimsDbContext` | Mapping; global filter on `Specification` |
| `Application/Services/SpecificationLookup.cs` (modify), `Workflows/TestWorkflow/TestWorkflowSupport.cs:640` (modify) | Limit-set aware resolution |
| `Application/Services/Stability/*.cs` | `StorageConditionService`, `ChamberService`, `StabilityProtocolService`, `StabilityScheduleService`, `StabilityPlacementService`, `StabilityPullService`, `StabilityResultsService`, `StabilityReportService`, `StabilityNotifier` |
| `API/Controllers/Stability/*.cs`, `API/BackgroundServices/StabilityPullWorker.cs` | Endpoints, worker |
| `frontend/src/modules/stability/*`, `services/stabilityService.ts` | Screens |

Enums: `StabilityStudyType { LongTerm, Intermediate, Accelerated, Ongoing, Other }`; `StabilityProtocolStatus { Draft, Authored, Approved, Completed, Discontinued }`; `StabilityAmendmentStatus { Draft, Authored, Approved }`; `StabilityPullStatus { Scheduled, Pulled, Missed, Cancelled, LinkedTimeZero }`.

---

### Task 1: Role, permissions, conditions, chamber type

**Files:** `RoleType.cs`, `PermissionConstants.cs` (`RdManagerGrants`: `Stability.View`, `Stability.Approve`, `Sections.ViewAll`, `Samples.TrackAll`, `Dashboards.LabOverview`, `Kpi.View`, `Messages.Use`, `Discussions.View`), `SegregationOfDutiesRules.cs`, `DbSeeder.cs`, migration `<ts>_AddRdManagerRoleAndStabilityPermissions.cs` (pattern: B4's `AddQaRole`), `EquipmentType.cs`, `StorageCondition` entity + migration seed, `StorageConditionService`, `StorageConditionsController`; frontend role union, `userDisplay.ts`, `CreateRolePage.tsx` `BASE_TYPES`, `DashboardPage.tsx` (R&D Manager → stability dashboard from Task 9); Test `IntegrationTests/StabilityRoleAndConditionsPostgresTests.cs`

**Interfaces:** `StorageCondition : IVersionedEntity { Id; Code (unique); Name; TemperatureC; TemperatureToleranceC; RelativeHumidityPct?; RelativeHumidityTolerancePct?; IsActive }`. `CreateAsync`, `UpdateAsync` (refused when used by an approved protocol: `"This condition is used by a study; deactivate it instead."`), `SetActiveAsync` — `Stability.Approve`.

- [ ] **Step 1:** Failing tests: role created with exact grants and audit; R&D Manager role refused each forbidden code (B4 rule extended); conditions seeded once; condition edit by non-approver refused; used condition edit refused, deactivate allowed; `EquipmentType.StabilityChamber` numeric value equals previous count.
- [ ] **Step 2:** FAIL → implement → PASS; no pending model changes. Commit `feat(stability): R&D Manager role, permissions, conditions, chamber type`.

### Task 2: Protocol schema and limit sets

**Files:** Domain files above; `Specification.cs`; migration `<ts>_AddStabilityProtocols.cs`; `MicroLimsDbContext` (`modelBuilder.Entity<Specification>().HasQueryFilter(s => s.StabilityLimitSetId == null)`); `AppendOnlyTriggerCatalogPostgresTests.ProtectedTables`; Test `IntegrationTests/StabilitySchemaPostgresTests.cs`

**Interfaces (fields):**
- `StabilityProtocol : IVersionedEntity { Id; Number; Title; ItemId; SectionId; StudyType; Reason; Status; AuthoredByUserId?; AuthoredAtUtc?; AuthorSignatureId?; ApprovedByUserId?; ApprovedAtUtc?; ApprovalSignatureId?; DiscontinuedReason?; …; Version }`.
- `StabilityBatch { Id; ProtocolId; BatchNumber; MfgDate?; ExpDate?; BatchSize?; PackagingDescription? }`.
- `StabilityStudyCondition { Id; ProtocolId; StorageConditionId; Pack; Orientation?; UnitsPerPullSpare }`.
- `StabilityTimePoint { Id; StudyConditionId; Months (0–120); WindowDaysBefore (0–60); WindowDaysAfter (0–60); UnitsRequired }` unique (`StudyConditionId`, `Months`).
- `StabilityTimePointTest { TimePointId; TestCode }`.
- `StabilityLimitSet { Id; ProtocolId; Sequence; CreatedByAmendmentId?; EffectiveFromUtc }`; the latest approved set is current.
- `StabilityAmendment : IVersionedEntity { Id; ProtocolId; Number (1..n); Reason; ChangeJson; Status; author / approval signature fields }`.
- Triggers: once `Status` ≥ `Authored`, reject UPDATE of `StabilityBatches`, `StabilityStudyConditions`, `StabilityTimePoints`, `StabilityTimePointTests` rows of that protocol except through an approved amendment (implement as: reject UPDATE/DELETE when the parent protocol is Approved and no `app.stability_amendment` session setting equals an approved amendment id — the amendment service sets it with `SET LOCAL`). `Specification` rows with `StabilityLimitSetId` not null: reject UPDATE and DELETE always.

- [ ] **Step 1:** Failing tests: global filter hides tagged rows from `db.Specifications`; tagged row update and delete rejected by trigger; approved protocol child rows reject direct update; `SET LOCAL` path allows the amendment service only; number uniqueness under concurrent creation.
- [ ] **Step 2:** FAIL → implement → PASS incl. trigger catalog test. Commit `feat(stability): protocol schema and protected limit sets`.

### Task 3: Limit-set aware specification resolution

**Files:** `SpecificationLookup.cs` (lines 40–100), `TestWorkflowSupport.cs:640-641` (replace the inline query with `SpecificationLookup.ForSampleAsync`), `Sample.cs` (`StabilityPullId?`), the sample-level readers `SampleSummaryService.cs:230`, `SupplierStatedAttributeService.cs:203`, `NotApplicableTestService.cs:58`, `:296`, `AddLaboratoryService.cs:47`, `SampleCorrectionService.cs:145`, `ProductWorkflowEngine.cs:82`, `SampleTestSelectionService.cs:152`; Test `IntegrationTests/StabilitySpecificationLookupPostgresTests.cs`, `ArchitectureTests/SpecificationReadersTests.cs`

**Interfaces:**
- `static Task<int?> LimitSetForSampleAsync(IMicroLimsDbContext db, int sampleId, CancellationToken ct)` — from `Sample.StabilityPullId` → `StabilityPull.LimitSetId`.
- `ApplicableGradeAsync`, `ApplicableStageAsync`, `ForSampleAsync`, `PrimaryAsync` gain the limit-set branch: when a set applies, query `db.Specifications.IgnoreQueryFilters().Where(s => s.StabilityLimitSetId == setId && s.ItemId == itemId && s.TestCode == testCode)`; grade and stage selection then run over those rows unchanged. When no set applies, behaviour is byte-for-byte today's.
- Each listed reader that resolves rows for a **sample** calls the helper; readers at item or master-data level keep the filtered `db.Specifications` (protocol rows invisible).
- Architecture test: no file under `Application/Workflows` or `Application/Services` except `SpecificationLookup.cs` and the stability services may call `IgnoreQueryFilters()` on `Specifications`.

- [ ] **Step 1:** Characterisation tests first (before any change) for each listed reader on a routine sample; then failing tests: `Stability_sample_resolves_protocol_rows`, `Routine_sample_never_sees_protocol_rows` (one assertion per reader), `Grade_and_stage_rules_apply_within_limit_set`, `Recorded_result_snapshot_points_at_protocol_row` (record a single result on a stability sample; `ParameterResult.SpecificationId` = the protocol row, `SpecLimit` = its limit), architecture test.
- [ ] **Step 2:** FAIL → implement → PASS; full existing result-recording suites pass unchanged. Commit `feat(stability): limit-set aware specification lookup`.

### Task 4: Protocol authoring, approval, amendments, discontinuation

**Files:** `StabilityProtocolService.cs`, `StabilityProtocolsController.cs`; Test `IntegrationTests/StabilityProtocolPostgresTests.cs`

**Interfaces:**
- `CreateAsync(CreateProtocolRequest { Title, ItemId, SectionId, StudyType, Reason })`, `SaveDraftAsync(id, ProtocolDraft { Batches[], Conditions[{ StorageConditionId, Pack, Orientation?, UnitsPerPullSpare, TimePoints[{ Months, WindowDaysBefore, WindowDaysAfter, UnitsRequired, TestCodes[] }] }] }, expectedVersion)` — `Stability.Manage`, section configure scope, status `Draft`; item must have the tests; conditions active.
- `AuthorAsync(id, password)` — validates completeness (≥ 1 batch, condition, time point, test each); signs `StabilityProtocolAuthored`; `Authored`.
- `ApproveAsync(id, password)` — `Stability.Approve`, signer ≠ author; in one transaction: signs `StabilityProtocolApproved`, creates limit set 1 by **copying** for each (`TestCode`) the rows `SpecificationLookup.SelectForStage(itemRows, ProductionStageRole.Stability, grade: null)` (stability-stage rows, else the general rows) into new `Specification` rows with `StabilityLimitSetId`; `Approved`. `ReturnAsync(id, comment)` → `Draft`.
- Amendments: `CreateAmendmentAsync(protocolId, reason)`, `SaveAmendmentAsync(id, AmendmentChange { AddTimePoints[], AddTests[], RemoveFutureTimePoints[], DiscontinueConditions[], Limits[{ TestCode, row fields }] })`, `AuthorAmendmentAsync`, `ApproveAmendmentAsync` — applies the change under `SET LOCAL app.stability_amendment`; a `Limits` change creates a new limit set (copy of current + changes); a removed time point cancels its scheduled pulls only if not pulled.
- `DiscontinueAsync(protocolId, scope { All | BatchId | StudyConditionId }, reason, password)` — `Stability.Approve`; cancels open pulls in scope.

- [ ] **Step 1:** Failing tests: full path with state, signature and audit; author cannot approve; incomplete protocol cannot be authored; `Limits_copied_at_approval_are_unaffected_by_later_specification_change`; stability-stage row preferred over general row; amendment creates new set; earlier pulls keep their set (with Task 6); removing a pulled time point refused; discontinue cancels open pulls only; no R&D Manager user → approve unavailable by permission.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(stability): protocol approval and amendments`.

### Task 5: Chambers, placement, schedule, inventory

**Files:** `ChamberService.cs`, `StabilityPlacementService.cs`, `StabilityScheduleService.cs` (pure `StabilitySchedule.Calculate`), controllers; Test `UnitTests/StabilityScheduleTests.cs`, `IntegrationTests/StabilityPlacementPostgresTests.cs`

**Interfaces:**
- `ChamberConditionAssignment { Id; EquipmentId; StorageConditionId; FromUtc; ToUtc? }` — `AssignAsync(equipmentId, conditionId, userId)` (`Stability.Manage`; equipment `Type == StabilityChamber`; closes the previous assignment; refused while units of another condition are inside).
- Calibration rule: same as `IncubatorEligibilityService.GetEligibleIncubatorsAsync` line 38 (`CalibrationDueDate == null || CalibrationDueDate >= now`). Reuse by extracting `EquipmentCalibration.IsCurrent(Equipment, DateTime nowUtc)`; do not change the incubator behaviour. (The null-date acceptance is A1 conflict CONF-003; it is not decided here.)
- `static IReadOnlyList<ScheduledPull> StabilitySchedule.Calculate(DateOnly placementDate, IReadOnlyList<StabilityTimePoint> points, Func<DateOnly,bool> isWorkingDay)` — target = `placementDate.AddMonths(Months)`; if not a working day → previous working day; window = target − before … target + after (window not shifted).
- `PlaceAsync(protocolId, PlaceRequest { BatchId, StudyConditionId, EquipmentId, Units, PlacementDate })` — `Stability.Pull`; protocol `Approved`; chamber assigned to that condition on that date and calibration current; units ≥ sum of `UnitsRequired` + spares, else refused with the shortfall; creates the pulls (`Scheduled`); one placement per (batch, condition).
- `MoveUnitsAsync(placementId, toEquipmentId, units, reason)`; `GetInventoryAsync(protocolId)` → per placement: placed, pulled, moved, left, needed by remaining pulls, `Shortfall`.

- [ ] **Step 1:** Failing tests: unit `Schedule_month_end_and_non_working_target` (31 Jan + 1 → 28/29 Feb; Friday-off calendar), `Month_zero_is_placement_date`; integration: wrong condition, expired calibration and non-chamber refused; insufficient units refused with numbers; pulls created per time point with audit; `Shortfall_reported_after_move`; assignment change refused with units inside.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(stability): chambers, placement, schedule and inventory`.

### Task 6: Pull to sample, time zero

**Files:** `StabilityPullService.cs`, `ProductWorkflowEngine.cs` (request gains internal `IReadOnlyCollection<string>? ExplicitTestCodes` — not on any API DTO; when set, test orders are created for exactly those codes, which must all be in the item's assigned tests, ignoring `RequiresTestSelection`), controller; Test `IntegrationTests/StabilityPullPostgresTests.cs`

**Interfaces:**
- `StabilityPull : IVersionedEntity { Id; PlacementId; TimePointId; TargetDate; WindowStart; WindowEnd; Status; LimitSetId?; SampleId?; PulledByUserId?; PulledAtUtc?; UnitsTaken?; CalibrationJustification?; JustificationSignatureId?; DeviationRecordId?; Version }`.
- `PullAsync(pullId, PullRequest { UnitsTaken, Justification?, Password? }, expectedVersion, userId)` — `Stability.Pull`, section write scope; lab date inside the window (before → `"This pull opens on {date}."`; after → `"This pull was missed on {date}."`); units ≤ left; chamber calibration current, else `Justification` + password of a holder of `Stability.Manage` (signs `StabilityPullJustified`); in one `UnitOfWork.RunAsync`: sets `LimitSetId` = current set, calls `ProductWorkflowEngine.ReceiveAsync` with item, batch, dates, `ProductionStage` = the seeded Stability stage, cause "Stability", `ExplicitTestCodes` = time point tests, `TargetSectionIds` null; sets `Sample.StabilityPullId`; `Pulled`. Confirm `ReceiveAsync`'s `SaveChangesAsync` enlists in the ambient transaction (CLAUDE.md 5.4); if not, restructure so it does.
- `LinkTimeZeroAsync(pullId, sampleId, password, userId)` — `Stability.Manage`; time point `Months == 0`; sample of the same item and batch number, overall status Approved; signs `StabilityTimeZeroLinked`; status `LinkedTimeZero`; no new sample, no units taken.

- [ ] **Step 1:** Failing tests: `Pull_creates_one_sample_with_protocol_tests_batch_stage_cause` (persisted state, test codes exact, audit, pull link); `Pull_before_window_refused`; `Pull_after_window_refused`; `Pull_uses_limit_set_in_force_at_pull_creation`; `Pull_judged_against_protocol_limits` (record a failing value per protocol that passes the current item spec → `OutOfSpecification`); calibration-expired pull needs justification by `Stability.Manage`; units exceeded refused; time-zero link rules (other batch, not approved, non-zero time point refused); wrong password changes nothing; rollback: forced failure in receipt leaves pull `Scheduled` and units unchanged.
- [ ] **Step 2:** FAIL → implement → PASS; receiving suites pass. Commit `feat(stability): pulls create samples judged against protocol limits`.

### Task 7: Missed pulls, excursions, notifications, reminders

**Files:** `API/BackgroundServices/StabilityPullWorker.cs` (pattern `MonitoringDeviationWorker.cs`; `ProcessName = "StabilityPullWorker"`; `BackgroundJobCatalog` entry, Gxp; config `StabilityPulls:Enabled`, `:IntervalMinutes` default 60), `StabilityPullService.MarkMissedAsync()`, `ChamberExcursion` + service, `StabilityNotifier`, `MyTasksService` (task type `"StabilityPull"`); D2: `QualityRecord.RaisedByUserId` becomes nullable with `RaisedBySystemProcess?` (migration in this task; D2 services treat a null raiser as "System" for the separation rule); Test `IntegrationTests/StabilityMissedAndExcursionPostgresTests.cs`

**Interfaces:**
- `Task<int> MarkMissedAsync(CancellationToken ct)` — pulls `Scheduled` with `WindowEnd < LabToday`: status `Missed`, raise a D2 deviation (system) titled `"Missed stability pull {protocol} {condition} {months} M, batch {batch}"`, linked to the protocol's item and the pull's placement chamber, `SectionId` = protocol section; idempotent (`DeviationRecordId` set once).
- `RecordExcursionAsync(ExcursionRequest { EquipmentId, FromUtc, ToUtc, ExtremeValue, Description })` — `Stability.Pull`; finds placements in that chamber during the period; raises one D2 deviation linked to every affected protocol (as `QualityLinkTargetType` extended with `StabilityProtocol`).
- Reminder setting per section: `StabilityReminderLeadWorkingDays?` (store on D2's `QualitySectionSetting`-style table `StabilitySectionSetting`); My Work lists pulls whose `WindowStart` minus lead ≤ today.
- Notifier (after commit): missed → Section Head of section, R&D Manager holders, QA (`Stability.View` + role QA); excursion → same.

- [ ] **Step 1:** Failing tests: `Missed_pull_opens_exactly_one_deviation_on_repeated_runs`; deviation audit row has `ActorType.System` and process name; pull not yet past window untouched; excursion links every affected protocol and none outside the period; My Work shows pulls by lead time (empty = window start); notifications created after commit.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(stability): missed pulls, excursions and reminders`.

### Task 8: Results, report, completion

**Files:** `StabilityResultsService.cs`, `StabilityReportService.cs`, controllers; Test `IntegrationTests/StabilityResultsAndReportPostgresTests.cs`

**Interfaces:**
- `GetResultsAsync(protocolId)` → per study condition and batch: rows = tests, columns = time points; cell = `{ pullStatus, sampleId?, reportedValue?, limitText?, resultStatus?, oosNumber? }` (reported value and status from the approved result projection the sample report uses; never an invalidated value). `AsNoTracking`, projected in SQL.
- `GetTrendAsync(protocolId, testCode)` → points `{ batch, condition, months, value }` plus limits per limit set.
- CSV export logged via `DataExportAuditService.LogExportAsync`.
- `StabilityReport : IVersionedEntity { Id; ProtocolId; Conclusion; ConclusionByUserId?; ConclusionSignatureId?; ApprovedByUserId?; ApprovalSignatureId?; IssuedAtUtc?; SnapshotJson? }`; attachments via the D2 attachment pattern. `SignConclusionAsync` (`Stability.Manage`), `ApproveAsync` (`Stability.Approve`, ≠ conclusion signer) — freezes `SnapshotJson` (results grid, inventory, linked deviations and OOS numbers); protocol → `Completed` when every pull is `Pulled` (sample decided), `Missed`, `Cancelled` or `LinkedTimeZero`, else refused listing what is open. Issued report immutable (trigger on update after `IssuedAtUtc`).
- PDF via `IPdfGenerator.GenerateReportAsync`: every signature with printed name, UTC time with zone, meaning.

- [ ] **Step 1:** Failing tests: grid cells for each pull status; invalidated value never shown; trend points and limit sets; export logged; completion refused with an open pull; approve by conclusion signer refused; issued report rejects update; PDF text contains signatures.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(stability): results, report and completion`.

### Task 9: Frontend

**Files:** `services/stabilityService.ts`; `modules/stability/StudiesPage.tsx` (register), `ProtocolEditorPage.tsx` (batches, conditions, time points with windows and units, tests per time point), `ProtocolPage.tsx` (status, signatures, amendments, placements, inventory, pulls), `PullDialog.tsx`, `PlacementDialog.tsx`, `ExcursionDialog.tsx`, `ResultsGrid.tsx`, `TrendChart.tsx` (recharts, same style as `modules/reports/components/controlChart/SpcCharts.tsx`), `ReportPage.tsx`, `ConditionsPage.tsx`, `StabilityDashboardPage.tsx` (R&D Manager); menu group, routes; sample screens show a "Stability: STB-… / 25/60 / 6 M / batch" chip; tests beside each

- [ ] **Step 1:** Failing tests (`userEvent`, real components, `services/` mocked; recharts wrapper may be stubbed per CLAUDE.md 4.5 with assertions on the data passed): editor adds batch, condition, time point and tests and saves with version; author and approve dialogs with server meaning; approve hidden for the author; amendment flow; placement refusal shown; pull dialog disabled before window with the date as text; missed pulls shown as such; grid states; trend data per batch and limit set; report signatures; conditions page edit refused message.
- [ ] **Step 2:** FAIL → implement → lint, types, tests, debt check, build PASS. Commit `feat(ui): stability studies`.

### Task 10: Documents and verification

- [ ] **Step 1:** `BusinessRules.md`: stability section (limits fixed in protocol, windows, no late pulls, missed-pull deviation, R&D Manager approval). If A1–A3 exist: URS / FS area `STB`, risk rows and tags, manual chapter, R&D Manager role guide, SOP template note. Otherwise list as follow-ups.
- [ ] **Step 2:** Full verification per `CLAUDE.md` section 2 with PostgreSQL; exact counts; `has-pending-model-changes`; frontend checks.
- [ ] **Step 3:** Report to the user: the manual scenario of spec section 7; that chamber calibration follows today's incubator rule (a missing due date counts as current — A1 conflict CONF-003); that the stability laboratory section seeded today sits under the R&D department.

## Cross-plan corrections (whole-programme review, 2026-11-11)

These corrections come from reviewing all plans together against the code. Where they differ from the text above, they win. Shared names are defined once in `docs/superpowers/plans/2026-11-11-build-order.md` (section "Programme conventions").

1. Task 7: remove the change to D2's `RaisedByUserId` (D2 now creates it nullable).
2. **Global query filter risk:** this is the first `HasQueryFilter` in the codebase. Before Task 2, list every `Specifications` query in Application and API (including master-data screens, workbook export and `ItemService`) and add a test per reader proving protocol rows stay invisible; workbook export must exclude them.
3. `EquipmentCalibration.IsCurrent(Equipment equipment, DateTime nowUtc)` is the shared signature (E1 uses the same).
4. Numbering uses `AdvisoryLockKeys.StabilityProtocol`.
