# SOP Revision on the Test Record (D5) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Capture the item's effective SOP revision on each test order at its first recorded step, protect it, and show it on test, review, sample report and certificate header.

**Architecture:** Nullable snapshot columns on `TestOrder`, filled once by `SopRevisionCapture.CaptureIfNeededAsync` called at the same recording points as D4's competency guard; a PostgreSQL trigger refuses changes once set. `ItemSopResolver` (extracted in D4 Task 2) resolves the SOP. Summary and certificate DTOs carry the values.

**Tech Stack:** .NET 10, EF Core 10, PostgreSQL, xUnit `[PostgresFact]`; React 19, TypeScript, Vitest.

**Spec:** `docs/superpowers/specs/2026-11-11-sop-revision-on-test-design.md`

## Global Constraints

- `CLAUDE.md` binds every task (anti-fake tests, triad, `[PostgresFact]` must run, `TimeProvider`, `CancellationToken`, one migration + `has-pending-model-changes`, no new `any`, `userEvent`).
- If D4 is not yet built, Task 1 extracts `ItemSopResolver` from `ItemStmService.GetForItemAsync` (`backend/MicroLIMS.Application/Services/ItemStmService.cs:14`) with a characterisation test first; if D4 is built, reuse it.
- Capture only when the sample has an `ItemId`. Never block a recording because of capture.
- `SopCaptureStatus { Captured, NoSopNumber, NotFound, NoEffectiveRevision }`.
- Text: "SOP not recorded: {reason}", "Not recorded (before {date})", "A newer revision (rev. {n}) became effective on {date} while this test was running." Dates in lab time.

## Review Focus

1. Two first steps recorded at the same time on one test order both try to capture → `Concurrent_first_steps_capture_once` (Task 2): second writer sees the value already set and does not overwrite (trigger allows null→value only).
2. A recording point captures nothing because it was missed → `Every_write_access_check_in_workflows_is_paired_with_sop_capture` architecture test (Task 2), sharing the pairing rule with D4.
3. Retest sample copies the origin's capture → `Retest_captures_its_own_revision` (Task 2).
4. Item SOP number edited after capture changes what is shown → `Display_uses_stored_values_not_live_lookup` (Task 3).
5. Certificate with tests from two revisions shows only one → `Certificate_lists_each_distinct_revision` (Task 3).

---

### Task 1: Schema and capture service

**Files:** `Domain/Entities/TestOrder.cs` (add `SopCaptureStatus? SopCaptureStatus; int? SopDocumentMasterId; string? SopDocumentCode; string? SopDocumentTitle; int? SopRevisionId; string? SopRevisionNumber; DateTime? SopEffectiveDateUtc; DateTime? SopCapturedAtUtc`), migration `<ts>_AddSopRevisionOnTestOrder.cs` (trigger `test_orders_sop_capture_immutable`: on UPDATE, if `OLD."SopCaptureStatus" IS NOT NULL` and any of the eight columns differs → raise), `Application/Services/SopRevisionCapture.cs` (+ interface); `AppendOnlyTriggerCatalogPostgresTests` (add the trigger to its expected list, as a column-level guard); Test `IntegrationTests/SopRevisionCapturePostgresTests.cs`

**Interfaces:** `Task CaptureIfNeededAsync(TestOrder order, CancellationToken ct = default)` — loads the sample's `ItemId`; if null → return; if `order.SopCaptureStatus` not null → return; resolve via `ItemSopResolver.ResolveAsync(itemId, ct)`; set fields and `SopCapturedAtUtc` from `TimeProvider`; does not save (caller's transaction).

- [ ] **Step 1:** Failing tests: each resolver status stored; no capture without item; trigger rejects update after capture; null → value allowed once; migration leaves existing rows null.
- [ ] **Step 2:** FAIL → implement → PASS; no pending model changes. Commit `feat(sop): capture SOP revision on test order`.

### Task 2: Call at every first-step point

**Files:** the recording points listed in D4 plan Task 3 (`TestWorkflowSupport.cs:602`, `DissolutionRecorder.cs:248`, `DisintegrationRecorder.cs:256`, `LocationResultRecorder.cs:20`, `OutsourcedRecorder.cs:46`, `IcpMethodAssayRecorder.cs:25`, `HplcMethodAssayRecorder.cs:26`, `WeightVariationRecorder.cs:291`), the batch paths, `SamplePreparationService` (preparation start); Test `IntegrationTests/SopRevisionCapturePointsPostgresTests.cs`, extend `ArchitectureTests/CompetencyGuardCoverageTests.cs` (or create `RecordingPointCoverageTests.cs` if D4 is not built)

**Interfaces:** after the write-access check, `await _sopCapture.CaptureIfNeededAsync(order, ct)` with the tracked order the recorder already loaded.

- [ ] **Step 1:** Failing tests: one per kind of first step (preparation, count reading, pathogen step, measurement, gravimetric, qualitative, dissolution, disintegration, weight variation, HPLC, ICP, UV, titration, outsourced) captures; `Second_step_does_not_recapture` (revision made effective between steps); `Concurrent_first_steps_capture_once`; `Retest_captures_its_own_revision`; audit row of the first step contains the captured values; architecture pairing test.
- [ ] **Step 2:** FAIL → implement → PASS; existing workflow suites pass. Commit `feat(sop): capture at every first recorded step`.

### Task 3: Display

**Files:** test order DTOs used by the testing workspace and review (`SampleSummaryService`, review DTOs), `CertificateOfAnalysisDto.cs` (`CoaScopeDto.MethodDocuments: List<CoaMethodDocumentDto { DocumentCode, Title, RevisionNumber, EffectiveDateUtc }>`), `CertificateOfAnalysisBuilder.Build` (distinct captured revisions of the scope's non-superseded test orders, ordered by code then revision), `ReportDocumentMapper` (sample report line per test; certificate header lines), newer-revision check (`ItemSopResolver` current revision ≠ captured and its effective date < test completion or test still open); frontend `TestWorkflowDialog.tsx`, review screen, certificate page; tests

- [ ] **Step 1:** Failing tests: backend `Display_uses_stored_values_not_live_lookup`, `Certificate_lists_each_distinct_revision`, `Certificate_omits_unresolved`, `Old_tests_show_not_recorded`, newer-revision notice true / false cases; frontend shows revision, unresolved reason and notice.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(sop): show captured SOP revision on test, review, report and certificate`.

### Task 4: Verification

- [ ] **Step 1:** Full backend suite with PostgreSQL and frontend checks per `CLAUDE.md` section 2; exact counts.
- [ ] **Step 2:** If A1 exists: FS entries (areas RPT, MDM) and tags. Update `BusinessRules.md` (Rule 3.1 note).
