# Review by Exception (F6) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Server-computed review exceptions per section, per-reviewer "Seen" ticks enforced at review completion, the ticked list bound to the review signature, and a review screen and queue that show exceptions first.

**Architecture:** `IReviewExceptionSource` implementations (one per exception group) produce `ReviewException { Key, Group, Kind, TestOrderId, Summary, EvidenceRef }`; `ReviewExceptionService` merges them; `ReviewExceptionTick` stores ticks; `SampleReviewService.CompleteReviewCoreAsync` re-computes and checks before signing and stores `ReviewExceptionRecord` rows linked to the review signature.

**Tech Stack:** .NET 10, EF Core 10, PostgreSQL, xUnit `[PostgresFact]`; React 19, TypeScript, MUI, Vitest.

**Spec:** `docs/superpowers/specs/2026-11-11-review-by-exception-design.md`

## Global Constraints

- `CLAUDE.md` binds every task (anti-fake tests, triad, `[PostgresFact]`, `AsNoTracking` projections for exception queries, `TimeProvider`, `CancellationToken`, no new `any`, `userEvent`, WCAG: ticks are real checkboxes with labels; counter in `role="status"`).
- No change to existing review preconditions, separation of duties or signature.
- Key = lowercase hex SHA-256 of `"{Kind}|{TestOrderId}|{sorted fact values}"`; facts chosen per kind (listed in Task 1).
- Window: from `IncubationWindowResolver` (existing), not re-implemented. Expiry and calibration "at use": time of use = incubation start for media and incubator; HPLC/ICP run start for run equipment; solution / standard use time from their consumption records — read each module first and use only recorded times; if none, emit `CouldNotBeChecked`.
- Near-expiry setting `Review:NearExpiryDays` per section (empty = off).
- Sources depending on later packages (E1 provenance, D4 override, F2 photos) are registered only when their tables exist (compile-time inclusion per package).

## Review Focus

1. Exception computed with "now" instead of time of use (a lot expired since) → `Expiry_judged_at_time_of_use_not_now` (Task 2).
2. Key unstable between calls (ordering of facts) → `Keys_stable_across_calls` (Task 1).
3. Reviewer B completes using reviewer A's ticks → `Ticks_are_per_reviewer` (Task 3).
4. Exception added between last tick and signing → `New_exception_at_signing_refuses` (Task 3).
5. Superseded tests produce exceptions that block review → superseded orders excluded except for "returned before" history → `Superseded_orders_excluded` (Task 1).

## File Structure

| File | Responsibility |
|---|---|
| `Application/Services/ReviewExceptions/*.cs` | `ReviewException`, `IReviewExceptionSource`, `ValuesSource`, `IncubationSource`, `MaterialsEquipmentSource`, `ResultsHistorySource`, `ReviewExceptionService` |
| `Domain/Entities/ReviewExceptionTick.cs`, `ReviewExceptionRecord.cs` | Ticks and recorded list |
| `Application/Services/SampleReviewService.cs` (modify) | Check and record at completion |
| `API/Controllers/SampleReviewController.cs` (modify) | `GET exceptions`, `PUT exceptions/{key}/tick` |
| `Application/Services/DashboardService.cs` (modify) | Queue counts |
| `frontend/src/modules/testingWorkspace/review/*` | Exception list, collapsed tests |

---

### Task 1: Exception service — values, incubation, results and history

**Interfaces:** `Task<IReadOnlyList<ReviewException>> GetAsync(int sampleId, int sectionId, CancellationToken ct)`. Sources and facts:
- `ValuesSource`: inactive `ParameterResult` / `TestAnalysis` / `CountTestReading` rows on the order (facts: row ids); `AuditLogs` with `TestOrderId` = order and non-empty `Reason` (facts: audit id).
- `IncubationSource`: completed incubations with read time outside the resolver window (facts: incubation id, completed time); `MinimumDurationOverriddenByUserId` set (facts: incubation id).
- `ResultsHistorySource`: active results with `ResultStatus` Alert/Action/OOS (facts: result id, status), linked D1 investigation number if any; `TestReturnEvent` rows (facts: event id).
Superseded orders contribute only `TestReturnEvent` history.

- [ ] **Step 1:** Failing tests: each kind present when its condition holds and absent otherwise (window boundaries inclusive as the resolver defines); `Keys_stable_across_calls`; `Superseded_orders_excluded`; section scope.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(review): exception service for values, incubation and results`.

### Task 2: Materials and equipment at use

**Interfaces:** `MaterialsEquipmentSource`: incubation media lot (`Incubation.MediaId` → `Media` expiry) and incubator (`IncubatorEquipmentId` → `Equipment.CalibrationDueDate`) vs incubation start; HPLC/ICP run equipment vs run start; solutions and working standards via their recorded use on the test order (read `SolutionPreparation` / `WorkingStandard` usage links first). Kinds: `ExpiredAtUse`, `NearExpiryAtUse` (setting), `CalibrationOverdueAtUse`, `CouldNotBeChecked` (a referenced lot or equipment without the needed date or time).

- [ ] **Step 1:** Failing tests: `Expiry_judged_at_time_of_use_not_now`; boundary on the expiry day; near-expiry only when setting set; calibration overdue at use; missing date → `CouldNotBeChecked`.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(review): materials and equipment at time of use`.

### Task 3: Ticks and completion

**Interfaces:** `ReviewExceptionTick { SampleId; SectionId; Key; ReviewerUserId; Comment? (≤ 1000); TickedAtUtc }` unique (`SampleId`, `SectionId`, `Key`, `ReviewerUserId`); `PUT api/samples/{id}/review/exceptions/{key}/tick` (body `{ sectionId, seen, comment }`; untick deletes the reviewer's own row — ticks are working state before signing; the signed record is Task 3's `ReviewExceptionRecord`). `ReviewExceptionRecord { ReviewSignatureId; SampleId; SectionId; Key; Group; Kind; Summary; Comment?; TickedAtUtc; ReviewerUserId }` append-only. In `CompleteReviewCoreAsync` (before `_reviewGate.SignAndLogAsync`, L128): recompute; every key must have a tick by this reviewer, else `BusinessRuleException("Exceptions not yet seen: {n}.")` with the list; after signing, write records with the signature id in the same save (L175). `ReviewExceptionTick` is added to `AuditCaptureExcludedTypes` with the comment `// reviewer working ticks; the signed ReviewExceptionRecord is the record` (approved by the user 2026-11-11).

- [ ] **Step 1:** Failing tests: tick saved and listed; `Ticks_are_per_reviewer`; completion refused with unticked; `New_exception_at_signing_refuses`; completion with all ticked stores records bound to the signature (state, audit, signature id); records immutable by trigger; untick before signing allowed; existing review tests pass.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(review): exception ticks enforced at review completion`.

### Task 4: Screen and queue

**Files:** `modules/testingWorkspace/review/ReviewExceptionsPanel.tsx` (grouped list, checkbox "Seen" with label including test and kind, comment field, evidence link, counter), integration in `SampleSummaryDialog.tsx` (panel above tests; Complete button disabled with reason until counter complete; `handleReviewConfirm` L269 unchanged otherwise), `TestResultCards.tsx` (collapsed one-line summary for tests without exceptions, expandable); queue DTO `ReviewerQueueItemDto` (`DashboardService.cs` L95) gains `ExceptionCount`, `ExceptionGroups`; `ReviewerDashboardPage.tsx` columns and sort.

- [ ] **Step 1:** Failing tests (`userEvent`, `services/` mocked): list grouped; tick saves; counter announces; Complete disabled with reason then enabled; server refusal at signing shows the new item; collapsed tests expand; queue shows counts and sorts.
- [ ] **Step 2:** FAIL → implement → full frontend checks PASS. Commit `feat(ui): review by exception`.

### Task 5: Later-package sources and verification

- [ ] **Step 1:** When E1, D4, F2 exist: `ValuesSource` adds typed values on connected fields (`ValueProvenance.Source == Manual` with reason); `ResultsHistorySource` adds `CompetencyOverride` rows and `EvidencePhoto.AddedLater` rows. Tests per kind.
- [ ] **Step 2:** `BusinessRules.md` review section; A1 entries if present; manual update for the review chapter.
- [ ] **Step 3:** Full verification per `CLAUDE.md`; report counts.

## Cross-plan corrections (whole-programme review, 2026-11-11)

These corrections come from reviewing all plans together against the code. Where they differ from the text above, they win. Shared names are defined once in `docs/superpowers/plans/2026-11-11-build-order.md` (section "Programme conventions").

1. `CompleteReviewCoreAsync` is also changed by D1 (opening investigations), D4 (separation for overrides), F3 (retained-sample registration) and C5/C8. The build order fixes the sequence; this plan's check runs **before** signing and its record write runs **after** the existing writes, in the same save.
2. Migration `AddReviewExceptions`.

## Review corrections (independent review, 2026-11-11)

From the independent review of the build order. These win over the text above and over the earlier corrections section where they differ.

1. **`ReviewExceptionTick` stays under automatic audit capture** (user decision 2026-11-11, following CLAUDE.md Rule 3.3: reviews may not be excluded without an equivalent record in the same save). Remove the exclusion in Task 3. Unticking does not delete: it sets `UntickedAtUtc` on the row, so the comment and both times remain.

**Translation (build order 2.7):** if F4 Task 4 is merged when this package is built, every new screen uses translation keys and every new refusal carries a message code.

**Exception kind added (user decision 2026-11-11):** `NoSpecification` in the Results and history group — a result recorded with status "Limits not configured" (accepted as a pass). The approval screen shows the same flag per test.
