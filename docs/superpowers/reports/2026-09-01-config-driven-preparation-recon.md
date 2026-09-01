# Recon: Config-Driven Preparation (Product / RM / PM)

**Type:** Read-only investigation. No code changes made.
**Status:** For Mohamed's review before any FS/build work begins.
**Date:** 2026-09-01

---

## Background / Intent (not implemented — context only)

Today, when a Product/RM/PM sample moves from **Needs Preparation → Start Testing**, the analyst opens a
"prepare sample" dialogue and manually enters preparation step data each time.

Target state (future, not yet designed in detail):
- Preparation steps become per-item configuration, set up once under Laboratory Configuration for each
  specific Product/RM/PM item.
- At Start Testing, the dialogue shows pre-configured steps and the analyst confirms them (immutable at
  confirm time — no edit/deviation path in this flow).
- Confirmation requires an e-signature (same pattern as existing Review/Approval).
- The sample record snapshots the confirmed steps at confirmation time, so later config edits never
  retroactively change historical sample records.
- Fallback: if an item has no configuration yet, the old manual-entry dialogue opens; whatever the analyst
  enters and signs becomes the item's standing configuration going forward, but flagged pending Section Head
  approval. Testing is never blocked by this.
- Water / EM / After Cleaning are unaffected.
- Full audit trail required, matching the Media Configuration pattern.

---

## A. Current "Needs Preparation" → "Start Testing" Transition

### A1. Where the transition is triggered and owned

**Initial `NeedsPreparation` assignment** (on receipt):
- Product/RM/PM: `ProductWorkflowEngine.ReceiveAsync` — `backend/MicroLIMS.Application/Workflows/ProductWorkflowEngine.cs:65-66`
  ```csharp
  Status = SampleStatus.Received,
  PreparationStatus = SamplePreparationStatus.NeedsPreparation
  ```
  (Test orders from `item.AssignedTests` are auto-generated at receipt.)
- Water: `WaterWorkflowEngine.ReceiveAsync` — `backend/MicroLIMS.Application/Workflows/WaterWorkflowEngine.cs:50-65` (0 test orders generated at receipt)
- EM: `EMWorkflowEngine.ReceiveAsync` — `backend/MicroLIMS.Application/Workflows/EMWorkflowEngine.cs:39-54`
- After Cleaning: `AfterCleaningWorkflowEngine.ReceiveAsync` — `backend/MicroLIMS.Application/Workflows/AfterCleaningWorkflowEngine.cs:59-74`
- Retest spinoff: `SampleApprovalService.CreateRetestSpinoffAsync` — `backend/MicroLIMS.Application/Services/SampleApprovalService.cs:56-65`

**Transition execution (`NeedsPreparation` → `Ready`):**
- Product/RM/PM: `SamplePreparationController.Prepare` (`backend/MicroLIMS.API/Controllers/SamplePreparationController.cs:29-43`) → `SamplePreparationService.PrepareAsync` (`backend/MicroLIMS.Application/Services/SamplePreparationService.cs:24-111`).
  Line 93: `sample.PreparationStatus = SamplePreparationStatus.Ready;`
  Also auto-assigns the submitting user as `AssignedAnalystId` on all `Waiting`-step TestOrders (lines 102-108).
- Water: `WaterController.Prepare` → `WaterWorkflowEngine.PrepareAsync` (line 142 sets Ready)
- EM: `EMController.Prepare` → `EMWorkflowEngine.PrepareAsync` (line 108 sets Ready)
- After Cleaning: `AfterCleaningController.Prepare` → `AfterCleaningWorkflowEngine.PrepareAsync` (line 128 sets Ready)

**Frontend trigger points** — all route through `ReceivingTestingWorkspacePage.tsx:431-434` (`handlePrepareSample` opens `<PreparationDialog>`, lines 728-743):
1. `TestStatusSummaryCell.tsx:92-96` — badge click shortcut
2. `SelectedSampleTestingPanel.tsx:251-267` — "Prepare Sample" header button
3. `SelectedSampleTestingPanel.tsx:480-504` — "Sample Needs Preparation" warning banner
4. `SampleTableRow.tsx:78-82, 124-128` — table row quick actions
5. `SampleCardView.tsx:43-47` — Kanban card quick action

### A2. Exact branching condition

`frontend/src/modules/testPreparation/PreparationDialog.tsx:27-42`:

```tsx
{sample.category === "EnvironmentalMonitoring" && sample.departmentId != null && (
  <EMPreparationForm ... />
)}
{sample.category === "AfterCleaning" && sample.machineId != null && (
  <AfterCleaningPreparationForm ... />
)}
{sample.category === "Water" && sample.waterDepartmentId != null && (
  <WaterPreparationForm ... />
)}
{sample.category !== "EnvironmentalMonitoring" && sample.category !== "AfterCleaning" && sample.category !== "Water" && (
  <TestPreparationForm sample={sample} onSaved={onClose} />
)}
```

Branch is by `sample.category` string + a matching id being non-null. Default/fallback (`FinishedProduct`,
`RawMaterial`, `PackagingMaterial`, or any category missing its id) → `TestPreparationForm`. **This is the
exact insertion point for the new config-driven flow** — it would need a fifth branch (or a wrapper around
the existing default branch) that checks whether the sample's `Item` has an approved/pending config before
deciding between "confirm" and "manual entry" variants.

### A3. Duplication / inconsistencies flagged

1. **Category string variance.** `SampleStatusKpiCards.tsx:38-48` matches multiple case variants
   (`FinishedProduct`/`Product`, `RawMaterial`/`RM`, `AfterCleaning`/`Aftercleaning`/`AC`,
   `EnvironmentalMonitoring`/`EM`), while `PreparationDialog.tsx:29-38` does exact-string comparison with no
   normalizer. A new branch condition should follow the `PreparationDialog` exact-match style, but be aware
   this inconsistency exists elsewhere.
2. **Orphaned Water fields in the Product/RM/PM path.** `TestPreparationForm.tsx:120-131` and
   `SamplePreparationService.cs:68-77` both still carry a dedicated `category == SampleCategory.Water` branch
   (`storageCondition`/`storageTimeHours`) even though Water sample prep is meant to go entirely through
   `WaterPreparationForm`/`WaterWorkflowEngine.PrepareAsync`. Dead/vestigial branch — flagged, not fixed.
3. **Analyst-ownership check duplicated** between backend (`SamplePreparationService.cs:34-45`) and frontend
   (`TestPreparationForm.tsx:32-36`). Not a blocker but any new confirm-flow component would need to
   replicate this check too, in whichever layer(s) it currently lives.

---

## B. Current "Prepare Sample" Dialogue (Product/RM/PM)

### B1. Frontend
- Component: `frontend/src/modules/testPreparation/TestPreparationForm.tsx:1-141`
- Container: `frontend/src/modules/testPreparation/PreparationDialog.tsx:1-44`
- Props:
  ```ts
  interface Props {
    sample: { sampleId: number; category: string; assignedAnalystId?: number | null; assignedAnalystName?: string | null };
    onSaved: () => void;
  }
  ```
- Fields captured: `amount`, `unit` (`ml`/`gm`/`bottle`/`cap`/`25cm2`), `technique` (`PourPlate`/`Filtration`),
  `filtrationVolume` + `washingVolume` (if Filtration), `diluentTypeId`, `diluentMediaId` (if diluent type
  requires batch tracking), `neutralizerId`, and the vestigial `storageCondition`/`storageTimeHours` pair
  (Water-only, see A3).

### B2. Backend endpoint & data destination
- Route: `POST /api/sample-preparation` (`SamplePreparationController.cs:28-43`)
- DTO: `PrepareSampleHttpRequest(int SampleId, decimal Amount, string Unit, string Technique, decimal? FiltrationVolume, decimal? WashingVolume, int DiluentTypeId, int? DiluentMediaId, int NeutralizerId, string? StorageCondition, int? StorageTimeHours)`
- Validation (`SamplePreparationService.cs:26-77`): sample must exist; not already prepared (one row per
  sample, ever); analyst-assignment/segregation check; diluent type must exist; if diluent type requires
  batch tracking, diluent media must exist, be released, not out-of-stock/quarantine-failed, not expired; if
  Filtration, both volumes required; if Water, storage fields required (see A3 on why this is vestigial).
- Target table: `SamplePreparations` (`SamplePreparation.cs:6-29`) — `SampleId`, `Amount`, `Unit`,
  `Technique`, `FiltrationVolume`, `WashingVolume`, `DiluentTypeId`, `DiluentMediaId`, `NeutralizerId`,
  `PreparedByUserId`, `PreparedAt`. Plus `Sample.PreparationStatus → Ready` and (Water-only)
  `Sample.StorageCondition`/`StorageTimeHours`.

### B3. Versioning / audit today
- Goes through `MicroLimsDbContext.CaptureAuditEntries` automatically (Create on `SamplePreparation`, Update
  on `Sample.PreparationStatus`) — so it IS in the AuditLog today.
- **No in-table versioning.** `SamplePreparation` is 1-to-1 with `Sample` (strict one-time insert, never
  updated) — confirmed via `MicroLimsDbContextModelSnapshot.cs:4619`. This is different from what the new
  feature needs: the new feature needs the *config* (per-item, many-samples) versioned, and a *snapshot*
  frozen onto each sample — this current 1:1 table is closer to the snapshot half only.

### B4. E-signature today
**Entirely absent.** Plain MUI button, no password re-auth, no `ElectronicSignatures` write. This would be
wholly new for this flow.

### B5. Every place prep data is read back (all would need to switch to reading a snapshot)
1. `SampleSummaryService.cs:128-130,236-246` → `SampleSummaryDialog.tsx:215-248,1333` (sample detail view)
2. `SampleReportPage.tsx:195-221` (print/report view — amount, unit, technique, volumes, neutralizer,
   prepared-by/at)
3. `SampleCoaPage.tsx:258` (Certificate of Analysis — Test Date from `preparedAt`)
4. `ResultProjectionService.cs:106-139` `DeriveCountUnit` + `TestWorkflowEngine.cs:1202-1253` +
   `PathogenSessionService.cs:522` — all read `SamplePreparation.Unit` to compute CFU/g, CFU/ml, etc.
5. `ResultService.cs:27` — gates result entry on `SamplePreparations.AnyAsync(...)` existing at all
6. `KpiService.cs:406-480` + `AnalystKpiService.ts:240` — TAT/SLA stage-duration calc from `PreparedAt`
7. `SegregationOfDutiesGuard.cs:34` — blocks the preparer from reviewing/approving the same sample
8. `UserReferenceRegistry.cs:100` — blocks deleting a user referenced as `PreparedByUserId`
9. `auditEnumLabels.ts:44,110` — maps `Sample.PreparationStatus` enum for audit display

---

## C. Item Master Data (Products / RM / PM)

### C1. Confirmed: each SKU is its own entity
`Item.cs:7-20` — `Id`, `Name`, `Code`, `Category` (enum: `FinishedProduct`/`RawMaterial`/`PackagingMaterial`),
`SopNumber`, `IsActive`, `AssignedTests: List<SampleTest>`, `Specifications: List<Specification>`.
`Sample.ItemId` FKs directly to `Items.Id`. **Confirmed distinct per-SKU, not category-level.**

### C2. Laboratory Configuration module structure
`frontend/src/routes/menuConfig.ts:53-69`, group `LAB CONFIGURATION`:
Test Master, Organisms, Items, Media Configurations, Water, Environmental Monitoring, After Cleaning, Cause
of Testing, Diluents & Neutralizers, Equipment.

**Reference pattern — Media Configuration** (closest existing analogue for a new per-item config entity):
- Entity: `MediaConfiguration.cs:28-75` (`Id`, `Name`, `EvaluationType`, incubation hours, temp range,
  recovery %, child `List<MediaConfigurationChallenge> Challenges`); child entity
  `MediaConfigurationChallenge` (`Id`, `MediaConfigurationId`, `OrganismId`, `ChallengeRole`,
  `ExpectedDescription`, `InitialInoculum`).
- Controller: `MasterDataController.cs:748-1060` — standard CRUD (`GET`/`POST`/`PUT`/`DELETE
  /api/masterdata/media-configurations`).
- Frontend: `frontend/src/modules/laboratoryConfiguration/media` / `media-configurations`.
- Audit: automatic via `CaptureAuditEntries`; registered in `AuditTraceabilityService.cs:88-93` and
  `auditEnumLabels.ts:85-86`.

### C3. Reuse vs new entity
`Item` has zero preparation-step fields today. Extending `Item` with scalar columns would only support one
fixed protocol per item; since prep protocols realistically vary by parameters (technique, amount, unit,
diluent, neutralizer, wash volumes), a **new dedicated child/FK entity** (e.g.
`ItemPreparationConfiguration`) mirroring the `MediaConfiguration` pattern is the natural fit — not a
reuse-in-place of `Item`.

---

## D0. Existing "Pending Approval" / Draft-State / Notification Patterns

### D0.1 Reusable status-flag pattern: `ApprovalGateStatus`
`ApprovalGateStatus.cs:3-8` — `PendingReview` (0) / `Approved` (1) / `Rejected` (2). Already used on:
- `Media.ApprovalStatus` (`Media.cs:49`, defaults to `PendingReview`)
- `Cryovial.ApprovalStatus` (`Cryovial.cs:56`, defaults to `PendingReview`)

Service pattern for consuming it: `MediaReleaseService.cs:44-83`, `CryovialService.cs:124-139` — check
`ApprovalStatus == PendingReview`, gate downstream actions, list pending items for Section Head review.
**This is a direct, ready-to-reuse precedent** for "auto-created config awaiting Section Head approval" —
same enum, same consumption pattern, no new abstraction needed.

### D0.2 Section Head permission codes
18 fixed permission codes in `PermissionConstants.cs:8-39`. Relevant ones Section Head already holds
(`DbSeeder.cs:450-459`): `Items.Manage`, `MasterData.Manage`. Either could gate an "approve pending config"
action as-is, or a new granular code (`Items.Approve` / `Configuration.Approve`) could be added if strict
segregation between authoring and approving is desired — that's an FS decision, not a code gap.

### D0.3 Notification mechanism reuse
`DashboardNotificationService.cs:61-119` computes live queues per role (`ApprovalWaiting`, `ReviewWaiting`,
`MediaExpiry`, `IncubationReady`, `TestReturnedForRevision`), persists to `NotificationLog`, delivers
in-process, emails on `"error"` severity. `DashboardService.cs:178-203` exposes queue counts as dashboard
KPI tiles. Adding a `PendingPreparationConfigApproval` count (mirroring the existing `ApprovalWaiting` count
query shape) into both services is structurally a drop-in — no new notification architecture required.

---

## D. E-Signature Pattern (For Reuse)

### D.1 Existing implementation
- Trigger: `SignatureDialog.tsx:22-83` (fully generic — takes `meaningStatement: string`,
  `onConfirm: (password: string) => Promise<void>`).
- Backend: `ElectronicSignatureService.SignAsync` (`ElectronicSignatureService.cs:18-58`) — re-verifies
  password server-side with BCrypt; accepts arbitrary `entityType: string` / `entityId: int`; failed attempts
  logged to `AuditLogs` without touching lockout counters.
- Captured: `UserId`, `UserFullNameSnapshot`, `UsernameSnapshot`, `RoleSnapshot` (all point-in-time
  snapshots, not live joins), `MeaningOfSignature` (enum), `EntityType`, `EntityId`, `SignedAt`, `Comment?`,
  `IpAddress?`.
- Storage: `ElectronicSignatures` table (`ElectronicSignature.cs:12-31`) — append-only by design.
- Hooks: audited via `CaptureAuditEntries`; registered in `UserReferenceRegistry.cs:40` (blocks user
  deletion); read back in `SampleSummaryService`/`SampleSummaryDialog`/report PDFs.

### D.2 Generalizability for "Confirm Preparation"
Both the dialog and `SignAsync` are already generic (string `entityType`/`entityId` — no hardcoding to
Sample/TestOrder). **Only concrete gap:** `SignatureMeaning.cs:3-8` enum currently has only `Reviewed`,
`Approved`, `Rejected`, `RetestRequested`, `InvestigationOrdered` — would need one new member (e.g.
`PreparationConfirmed`) plus a label added to `auditEnumLabels.ts:46`. Everything else in this pattern is
reusable as-is.

---

## E. Audit Trail Pattern (For Reuse)

### E.1 Mechanism
`MicroLimsDbContext.CaptureAuditEntries` (`MicroLimsDbContext.cs:136-193`) runs on every `SaveChanges`:
walks `ChangeTracker.Entries()`, for Added/Modified/Deleted serializes full before/after JSON into
`AuditLogs.PreviousValue`/`NewValue`, stamps `Action` (`Create`/`Update`/`Delete`), `UserId`, UTC
`Timestamp`, `EntityName`, `EntityId`. This is automatic for any tracked entity — no manual instrumentation
needed to get change history; the manual work is only in the *display* registries below.

### E.2 Registries a new entity MUST be added to
1. **`UserReferenceRegistry.cs:35-115`** (backend, 72 entries) — any new `CreatedByUserId` /
   `ApprovedByUserId` / etc. on the new entity must be registered here (this was also the exact gap hit
   earlier this session with `Incubation.CompletedByUserId` — a known, recurring checklist item, not
   optional).
2. **`auditTypes.ts:62-84`** (`ENTITY_DISPLAY_NAMES`, frontend) — new entity needs a human-readable name here
   or it renders as its raw class name in audit search.
3. **`auditEnumLabels.ts:55-125`** (`AUDIT_ENUM_LABELS`, frontend) — any enum properties on the new entity
   need ordinal-array mappings here or audit history will show raw integers (this is the exact bug fixed
   earlier this session for other entities).
4. **`AuditTraceabilityService.cs:87-110`** (backend) — needs a branch connecting the new entity back to its
   parent `Item` (config) and/or `Sample` (snapshot) audit chain so it surfaces in each record's full
   traceability view, not just its own isolated log.

This is a 4-registry checklist, confirmed consistent with how Media Configuration was wired up.

---

## F. Snapshot Pattern Precedent

### F.1 `MediaAppearanceSnapshotService` — current (fixed) state
`MediaAppearanceSnapshotService.cs:23-60`:
```csharp
public async Task<string?> GetExpectedAppearanceSnapshotAsync(int materialId, int organismId, CancellationToken ct = default)
{
    var materialName = await _db.Materials.Where(m => m.Id == materialId).Select(m => m.MaterialName).FirstOrDefaultAsync(ct);
    if (materialName is null) { _logger.LogWarning(...); return null; }

    var expected = await _db.MediaConfigurationChallenges
        .Where(c => c.MediaConfiguration!.Name == materialName && c.OrganismId == organismId)
        .Select(c => c.ExpectedDescription)
        .FirstOrDefaultAsync(ct);

    if (expected is null) _logger.LogWarning(...);
    return expected;
}
```

### F.2 Cautionary history + current pattern
Previously: free-text `MaterialName` matching caused silent null lookups (typos/whitespace) with no loud
signal. Fixed pattern now in place, and the one to follow for the new prep-steps snapshot:
1. **Canonical FK linking**, not free-text matching.
2. **Snapshot-on-write**: at the exact triggering event (e.g. `TestWorkflowEngine.cs:1783-1796`), fetch the
   live config and stamp it onto the execution/result row (`WorkflowStepResult.ExpectedAppearanceSnapshot`).
3. **Immutability**: once written, never recalculated from master data — later config edits don't touch
   historical rows (this is the ALCOA+ "Original record" guarantee the new feature explicitly needs).
4. **Explicit logging** on a null/missing snapshot, with full diagnostic parameters.

### F.3 Direct application
For the new feature: at Start-Testing confirmation, fetch the item's active `ItemPreparationConfiguration`,
serialize/stamp the confirmed steps onto the sample's execution record (structured columns or JSON,
consistent with how `SamplePreparation` currently holds structured columns), and have every reader in B5
switch to that stamped field instead of re-querying the live config table.

---

## Open Questions / Ambiguities (blocking an accurate FS)

1. **Snapshot granularity.** `SamplePreparation` today is 1-to-1 per `Sample` (one shared
   dilution/neutralization set for all tests on that sample). Does the new per-item config need to support
   *different* preparation steps per test on the same item (e.g. TAMC vs E. coli vs Pseudomonas), or is one
   protocol per item sufficient? This changes whether `ItemPreparationConfiguration` is a single row per Item
   or a child collection like `MediaConfiguration.Challenges`.
2. **Fallback config reuse before approval.** If a second sample of the same unconfigured item arrives before
   the Section Head reviews the first auto-created (pending) config, does it reuse the pending config
   directly, or trigger manual entry again? The recon confirms `ApprovalGateStatus.PendingReview` items are
   already usable-while-pending elsewhere (Media, Cryovial) — but confirm this is the intended behavior here
   too before writing it into the FS.
3. **Section Head rejects/edits a pending auto-created config.** What happens to samples already tested and
   signed against it? Their snapshot stays correct/immutable by design (F.2 point 3) — but does the
   *configuration row* flip to `Rejected` and require a fresh first-run to re-seed it, or can the Section Head
   edit it in place and re-submit for approval?
4. **Legacy Water fields in the Product/RM/PM path.** `SamplePreparationService.cs:68-77` /
   `TestPreparationForm.tsx:120-131` still carry vestigial Water-only branches (A3) even though Water prep
   is fully handled elsewhere. Confirm with the FS whether these should be removed as part of this change
   (they sit in the exact code path being restructured) or left alone as unrelated cleanup.
5. **New vs reused permission code for approval gating** (D0.2) — Section Head already holds `Items.Manage`
   and `MasterData.Manage`; decide whether either is sufficient or a new dedicated code is warranted.
6. **New `SignatureMeaning` enum member** (D.2) needed for "Confirm Preparation" — naming/wording is an FS
   decision, not just a technical one (it's what displays in the audit trail and the physical/electronic
   record).
