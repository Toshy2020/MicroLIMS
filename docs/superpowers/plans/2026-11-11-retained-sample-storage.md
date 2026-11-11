# Retained Sample Storage and Chain of Custody (F3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Storage location tree, retained-sample register with custody events by scan, retention rules, and QA-approved disposal.

**Architecture:** `StorageLocation` (self-referencing tree, level enum), `RetainedSample`, append-only `RetainedSampleEvent`, `RetentionRule`, `DisposalRequest` + items. Registration is triggered from `SampleReviewService` where the retention quantity is written. Scanning reuses B1's label payload resolver.

**Tech Stack:** .NET 10, EF Core 10, PostgreSQL, xUnit `[PostgresFact]`; React 19, TypeScript, Vitest.

**Spec:** `docs/superpowers/specs/2026-11-11-retained-sample-storage-design.md`

## Global Constraints

- `CLAUDE.md` binds every task (anti-fake tests, triad, `[PostgresFact]`, `ILabClock` for dates, `CancellationToken`, no new `any`, `userEvent`).
- Levels: `StorageLevel { Room = 1, Unit = 2, Shelf = 3, Box = 4 }`; a child's level must be greater than its parent's (levels may be skipped).
- Numbers: `RS-{yyyy}-{seq:D5}` (retained sample), `DR-{yyyy}-{seq:D4}` (disposal request), lab-clock year, advisory-lock generation.
- Retention end: `MonthsAfterReceipt` → `ReceivedAt (lab date).AddMonths(n)`; `MonthsAfterExpiry` → `Sample.ExpDate (lab date).AddMonths(n)`, rule not applicable when no expiry date (then "Retention period not set"). No rule seeded.
- `SignatureMeaning` appended: `DisposalRequested`, `DisposalApproved`.
- B1 prerequisite for scanning (label payload resolver). Without B1: list pick only, and the plan reports it.
- Permission codes: `Retention.View`, `Retention.Move`, `Retention.Manage`, `Retention.ApproveDisposal` (QA; joins B4's QA-only set).

## Review Focus

1. Two analysts withdraw the last quantity at the same time → `Concurrent_withdrawals_cannot_go_below_zero` (Task 3, version check on `RetainedSample`).
2. Review re-signed (retention quantity changed) creates a second register entry → `Review_update_adjusts_existing_registration` (Task 2).
3. Disposal approved, then the sample becomes linked to a new complaint before disposal → disposal of that item refused at the disposal scan → `Link_added_after_approval_blocks_disposal_scan` (Task 5).
4. 31 Jan + 1 month retention end → `Retention_end_month_end_clamps` (Task 4).
5. Requester approves own disposal request → `Requester_cannot_approve` (Task 5).

## File Structure

| File | Responsibility |
|---|---|
| `Domain/Entities/Retention/*.cs` | `StorageLocation`, `RetainedSample`, `RetainedSampleEvent`, `RetentionRule`, `DisposalRequest`, `DisposalRequestItem` |
| `Application/Services/Retention/*.cs` | `StorageLocationService`, `RetainedSampleService`, `CustodyService`, `RetentionRuleService`, `DisposalService` |
| `API/Controllers/RetentionController.cs` | Endpoints |
| `frontend/src/modules/retention/*`, `services/retentionService.ts` | Screens |

---

### Task 1: Locations

**Interfaces:** `StorageLocation : IVersionedEntity { Id; Code (unique); Name; Level; ParentId?; SectionId; EquipmentInventoryId? (Unit only); StorageCondition?; GridRows? (1–26, Box only); GridColumns? (1–30, Box only); IsActive }`. Positions are written `A1`…; `RetainedSample.Position?` required when the box has a grid, unique per box among non-disposed samples (filtered unique index). Create / update / deactivate (refused while any `RetainedSample` with status `InStorage` references it or a descendant); equipment must be `InService` for storing (check at move time, `EquipmentInventory.Status`). Label via B1's label service (`EntityType = "StorageLocation"`).

- [ ] **Step 1:** Failing tests: level ordering with skipped levels; grid only on boxes; position required and unique in a gridded box, free after disposal; unique code; deactivate refused when not empty; equipment link only on Unit; audit.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(retention): storage locations`.

### Task 2: Register

**Interfaces:** `RetainedSample : IVersionedEntity { Id; Number; SampleId (unique); ItemId?; BatchNumber?; Quantity; Unit; QuantityRemaining; Containers?; RequiredCondition?; LocationId?; HolderUserId?; Status (InStorage|Out|DisposalRequested|Disposed); RetentionEndDate?; RetentionRuleId?; SectionId }`. Receiving request (`ItemBasedReceiveRequest` and the receiving form) gains optional `RetentionQuantity` / `RetentionQuantityUnit` for FP, RM and PM; when given, the sample is registered at receipt. Change `SampleReviewService.ValidateRetentionAsync` (~L178–197): when the sample already has a retained quantity from receipt, the review shows it and does not ask again; otherwise retained quantity required when `Category` ∈ {`FinishedProduct`, `RawMaterial`, `PackagingMaterial`} at the review of the physicochemical section (`PhyschemAreas.SectionCode`), or at the first section review when the sample has no test order in that section; refused elsewhere (as today). `RegisterFromReviewAsync(Sample sample, int userId, ct)` called in `SampleReviewService.CompleteReviewCoreAsync` where `RetentionQuantity` is written (L160–172): creates or adjusts (when the quantity changed and nothing withdrawn yet; otherwise refuse the review change with "Retained sample already in use"). `RegisterManualAsync(sampleId, quantity, unit, containers?, locationId, userId)` for any sample. Units: same list as today's review rule (g, mg, kg, mL, L).

- [ ] **Step 1:** Failing tests: optional retained quantity at receipt registers the sample; review does not ask again when entered at receipt; retained quantity required for FP, RM and PM (when not entered at receipt) at the physicochemical review and at the first review when no physicochemical tests; refused for other categories; existing review tests updated only where the rule changed; automatic registration at review with event `Registered`; `Review_update_adjusts_existing_registration`; refused adjustment after a withdrawal; manual registration; one per sample; retention end from rule (Task 4 stub until then: null).
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(retention): retained-sample register`.

### Task 3: Custody events

**Interfaces:** `RetainedSampleEvent { Id; RetainedSampleId; Kind (Registered|PutAway|TakenOut|Returned|Transferred|Withdrawn|Corrected|DisposalRequested|Disposed); FromLocationId?; ToLocationId?; FromUserId?; ToUserId?; ToSectionId?; Quantity?; Purpose?; OosInvestigationId?; Reason?; CorrectsEventId?; ByUserId; AtUtc }` (append-only trigger). `MoveAsync(retainedSampleId, MoveRequest { Kind, ToLocationId?, ToSectionId?, Reason? }, expectedVersion, userId)`; `WithdrawAsync(id, quantity, purpose, oosInvestigationId?, expectedVersion, userId)`; `CorrectAsync(eventId, reason, …)`; `ResolveScanAsync(string payload)` → retained sample or location (B1). Out-too-long list: setting `Retention:MaxOutHours` (empty = none).

- [ ] **Step 1:** Failing tests: each kind updates location / holder / status; out-of-service unit refused; `Concurrent_withdrawals_cannot_go_below_zero`; withdrawal for a D1 plan links the investigation; correction event; trigger rejects edits; scan resolves both label types and rejects others; out-too-long list.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(retention): custody events and withdrawals`.

### Task 4: Retention rules

**Interfaces:** `RetentionRule : IVersionedEntity { Id; SectionId; Category (SampleCategory); ItemId?; Basis (MonthsAfterReceipt|MonthsAfterExpiry); Months (1–600); IsActive }`; item rule wins over category rule. `RecalculateOpenAsync(ruleId, reason, userId)` recalculates `RetentionEndDate` for `InStorage` / `Out` samples under that rule, one event per sample (`Corrected` with reason).

- [ ] **Step 1:** Failing tests: both bases; `Retention_end_month_end_clamps`; expiry basis without expiry → not set; item rule precedence; rule change affects only new registrations; recalculation with reason and events.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(retention): retention rules`.

### Task 5: Disposal

**Interfaces:** `DisposalRequest : IVersionedEntity { Id; Number; SectionId; Status (Draft|Requested|Approved|Closed|Cancelled); RequestedByUserId?; RequestSignatureId?; ApprovedByUserId?; ApprovalSignatureId? }`, `DisposalRequestItem { RequestId; RetainedSampleId; RemovedReason?; DisposedAtUtc?; DisposedByUserId?; Method? }`. `DueAsync(sectionIds)` (retention end ≤ lab today, status InStorage/Out). `CreateAsync`, `AddItemsAsync` (refuses items linked to open D1 investigation, D2 deviation or complaint — check via D1/D2 link tables; skip a check whose package is not built and report it), `RequestAsync(password)` (`Retention.Manage`, signs `DisposalRequested`), `ApproveAsync(password, removals[])` (`Retention.ApproveDisposal`, ≠ requester, signs `DisposalApproved`), `DisposeItemAsync(requestId, retainedSampleId via scan, method, userId)` (re-checks links), auto-close when all done.

- [ ] **Step 1:** Failing tests: due list; blocked links; request and approval signatures with audit; `Requester_cannot_approve`; QA removes an item with reason; dispose before approval refused; `Link_added_after_approval_blocks_disposal_scan`; close when complete; disposed record permanent.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(retention): QA-approved disposal`.

### Task 6: Screens and verification

**Files:** `modules/retention/LocationsPage.tsx` (tree editor), `RetainedSamplesPage.tsx` (register with filters and history), `ScanMoveDialog.tsx` (scan sample then destination; list fallback), `WithdrawDialog.tsx`, `RulesPage.tsx`, `DisposalPage.tsx` (due list, requests, approval, disposal scan); registration action on the sample page; tests (`userEvent`, `services/` mocked, scanner input mocked at its service).

- [ ] **Step 1:** Failing tests for each screen's main flow and refusal messages. **Step 2:** FAIL → implement → full checks PASS. Commit `feat(ui): retained sample storage and disposal`.
- [ ] **Step 3:** `BusinessRules.md` retention section; A1 entries if present; SOP template note (retention and disposal). Full verification per `CLAUDE.md`; report counts.
