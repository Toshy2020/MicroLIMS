# Functional Specification: Config-Driven Preparation (Product / RM / PM)

**Version:** 1.0
**Date:** 2026-09-01
**Status:** Approved for build sequencing
**Based on:** Recon findings, 2026-09-01 (`docs/superpowers/reports/2026-09-01-config-driven-preparation-recon.md`)
**Scope:** Product / RM / PM sample types only. Water, EM, After Cleaning are explicitly unaffected.

---

## 1. Overview

Today, Product/RM/PM samples move from **Needs Preparation → Start Testing** via a manual-entry
dialogue (`TestPreparationForm`) where the analyst types in preparation parameters (amount, unit,
technique, dilution/filtration volumes, diluent, neutralizer) fresh, every single sample.

This feature moves preparation to a **configure-once, confirm-many** model:
- Preparation parameters become per-item master data (`ItemPreparationConfiguration`), configured
  once under Laboratory Configuration → Items.
- At Start Testing, the analyst sees the pre-configured steps and **confirms** them with an
  e-signature. The confirm action is immutable — no edit/deviation path in this dialogue.
- The confirmed steps are **snapshotted** onto the sample's preparation record at the moment of
  confirmation, so later edits to the item's config never retroactively alter historical records
  (ALCOA+ "Original record" requirement).
- **Fallback:** if an item has no configuration yet, the existing manual-entry dialogue opens.
  Whatever the analyst enters and signs becomes the item's new configuration, but flagged
  `PendingReview`. **Testing is never blocked by this** — the sample proceeds immediately.
  Subsequent samples of the same item before Section Head review reuse the same pending config
  (no repeated manual entry).
- Section Head reviews pending configs on the Item Configuration page and either approves or
  edits-in-place and resubmits. Rejected/edited configs do not retroactively affect samples
  already tested against the prior snapshot.

---

## 2. Design Decisions

| # | Decision |
|---|----------|
| D1 | `ItemPreparationConfiguration` is **one row per item** — a single protocol per item, not per-test-per-item. Matches current live behavior (`SamplePreparation` is already 1-shared-protocol-per-sample across all tests on that sample). |
| D2 | A config in `PendingReview` status is **usable immediately** by any sample of that item, not just the one that created it. |
| D3 | Section Head **edits in place** and resubmits for approval, rather than rejecting and forcing a fresh first-run re-seed. |
| D4 | Vestigial Water-only fields (`storageCondition`/`storageTimeHours`) are **removed** from `TestPreparationForm.tsx`/`SamplePreparationService.cs` (dead code in the exact path being rebuilt) and **relocated** to the actual Water flow — captured in the Select Location dialogue (`WaterPreparationForm`) and written via `WaterWorkflowEngine.PrepareAsync`. The underlying `Sample.StorageCondition`/`StorageTimeHours` columns are unchanged (they already exist per recon B2) — only the capture point moves to where Water samples are genuinely prepared. |
| D5 | Approval-gating reuses the existing **`Items.Manage`** permission code — no new permission code added. |
| D6 | New `SignatureMeaning` enum member: **`PreparationConfirmed`**, display label **"Preparation Steps Confirmed as Configured."** |
| D7 | Confirm-only: the confirm dialogue has **no edit affordance**. If the configured steps are wrong, that's a Section Head config correction (via Item Configuration), not an analyst-side override. |

---

## 3. Data Model

### 3.1 New entity: `ItemPreparationConfiguration`

One row per `Item` (Product/RM/PM only).

| Field | Type | Notes |
|---|---|---|
| `Id` | int | PK |
| `ItemId` | int | FK → `Items.Id`, unique (one config per item) |
| `Amount` | decimal | |
| `Unit` | string | `ml`/`gm`/`bottle`/`cap`/`25cm2` |
| `Technique` | string | `PourPlate`/`Filtration` |
| `FiltrationVolume` | decimal? | required if Technique = Filtration |
| `WashingVolume` | decimal? | required if Technique = Filtration |
| `DiluentTypeId` | int | FK → `DiluentTypes.Id` |
| `DiluentMediaId` | int? | FK → `Media.Id`, required if diluent type requires batch tracking |
| `NeutralizerId` | int | FK → `Neutralizers.Id` |
| `ApprovalStatus` | `ApprovalGateStatus` | `PendingReview` (default on fallback-created) / `Approved` / `Rejected` |
| `CreatedByUserId` | int | FK → `Users.Id` — analyst (fallback path) or Section Head (direct config) |
| `CreatedAt` | DateTime | UTC |
| `ApprovedByUserId` | int? | FK → `Users.Id` |
| `ApprovedAt` | DateTime? | UTC |

No child collection (per D1 — single protocol per item). Field set is a direct mirror of the
existing `SamplePreparation` fields (minus `SampleId`/`PreparedByUserId`/`PreparedAt`, minus the
removed Water-only fields per D4).

Register in `UserReferenceRegistry.cs` for `CreatedByUserId` and `ApprovedByUserId`.

### 3.2 `SamplePreparation` — snapshot fields

`SamplePreparation` remains the 1:1-per-sample execution record. Add:

| Field | Type | Notes |
|---|---|---|
| `SourceConfigurationId` | int? | FK → `ItemPreparationConfiguration.Id`. Null if created via manual fallback entry (in which case this row *becomes* the seed for a new config, not a snapshot of an existing one). |
| `WasConfirmedFromConfig` | bool | `true` if the analyst went through the confirm-only flow; `false` if manual fallback entry. |

All existing `SamplePreparation` columns (`Amount`, `Unit`, `Technique`, etc.) continue to be
populated at write time — this **is** the snapshot. No separate JSON blob; structured columns
copied from the config (or entered manually, in the fallback case) at confirmation/submission time,
per the snapshot pattern in recon section F.

### 3.3 `SignatureMeaning` enum

Add `PreparationConfirmed` to `SignatureMeaning.cs`. Add corresponding label to
`auditEnumLabels.ts:46`: **"Preparation Steps Confirmed as Configured."**

---

## 4. Workflow Changes

### 4.1 Branching logic — `PreparationDialog.tsx`

Current branching (recon A2) is by `sample.category`. Add a fifth condition for the default
(Product/RM/PM) branch that checks the item's config state:

```
if (sample.category is FinishedProduct/RawMaterial/PackagingMaterial):
    if Item has ItemPreparationConfiguration (any ApprovalStatus):
        → ConfirmPreparationForm (new component, read-only display + e-sig confirm)
    else:
        → TestPreparationForm (existing manual-entry form, unchanged except D4 field removal)
        → on submit, backend creates ItemPreparationConfiguration(ApprovalStatus=PendingReview)
          seeded from the submitted values, in the same transaction as the SamplePreparation write
```

Note: a `PendingReview` config still routes to `ConfirmPreparationForm`, not back to manual entry
(per D2) — "has a configuration" is the branch condition, approval status is irrelevant to routing.

### 4.2 New component: `ConfirmPreparationForm`

- Read-only display of the item's current `ItemPreparationConfiguration` values (same fields as
  `TestPreparationForm`, styled as static text/labels, not inputs).
- "Confirm" button → opens `SignatureDialog` (existing, generic component — recon D.1) with
  `meaningStatement` = "Preparation Steps Confirmed as Configured", `entityType` =
  `"SamplePreparation"`.
- On signature success: backend creates the `SamplePreparation` row with all fields copied from
  the `ItemPreparationConfiguration` (snapshot), `SourceConfigurationId` set,
  `WasConfirmedFromConfig = true`, and writes the `ElectronicSignatures` row with
  `MeaningOfSignature = PreparationConfirmed`. Same transaction as today's `PrepareAsync` — sets
  `Sample.PreparationStatus = Ready`, auto-assigns analyst to `Waiting` TestOrders (existing
  behavior, unchanged).
- No edit affordance anywhere in this component (D7).

### 4.3 Fallback path — existing `TestPreparationForm`

Unchanged UI/UX except:
- Remove `storageCondition`/`storageTimeHours` fields and validation (D4).
- On submit, backend now also creates the `ItemPreparationConfiguration` row
  (`ApprovalStatus = PendingReview`, `CreatedByUserId` = submitting analyst) alongside the existing
  `SamplePreparation` write, in the same transaction. `SamplePreparation.SourceConfigurationId`
  points to this newly created config; `WasConfirmedFromConfig = false`.
- **This submission still requires the e-signature** (it did not before — recon B4 confirms
  e-signature is currently absent from this form entirely). Same `PreparationConfirmed` meaning
  applies — the analyst is simultaneously confirming their own manual entry and authoring the
  item's new config.

### 4.4 Water Select Location dialogue — field relocation (D4)

Two changes, both confined to the Water flow, which otherwise remains untouched:

- **Frontend:** `WaterPreparationForm` gains `storageCondition` (select/text, same value set as
  today's vestigial field) and `storageTimeHours` (numeric) inputs. Exact placement/layout within
  the existing Select Location dialogue is a build-time UI decision, not specified here.
- **Backend:** `WaterController.Prepare` → `WaterWorkflowEngine.PrepareAsync` DTO gains these two
  fields; `Sample.StorageCondition`/`Sample.StorageTimeHours` get set here instead of never being
  populated (no new columns — these already exist on `Sample` per recon B2).

**Pre-build check required:** the original recon did not detail `WaterPreparationForm`'s current
field set or the `WaterWorkflowEngine.PrepareAsync` DTO shape (Water was out of scope at the time).
Before implementing this section, confirm via a short targeted recon: (a) whether
`storageCondition`/`storageTimeHours` are already required/optional elsewhere in the Water
validation logic, (b) whether making them required at this new capture point breaks any existing
Water samples mid-flow, (c) exact DTO/component names and line references to edit.

---

## 5. Item Configuration Page (Laboratory Configuration → Items)

Reference: existing tab pattern (Overview / Assigned Tests / Specifications / Documents &
Attachments / Audit History).

### 5.1 New tab: "Preparation Configuration"

- If no config exists: empty state, "No preparation configuration set. One will be created
  automatically from the first analyst confirmation, or configure manually below." + manual entry
  form (Section Head can pre-configure proactively, bypassing the fallback path entirely).
- If config exists: display all fields (Section 3.1), an **Edit** action (available regardless of
  `ApprovalStatus`), and a status badge:
  - `PendingReview` → amber badge, "Pending Approval" + **Approve** button (gated on `Items.Manage`,
    per D5).
  - `Approved` → green badge, "Approved by {ApprovedByUserName} on {ApprovedAt}".
  - Editing an `Approved` config and resubmitting sets it back to `PendingReview` (D3 — no separate
    "Rejected" terminal state needed for this flow; edit-and-resubmit covers it).

### 5.2 Overview tab — Configuration Summary block

Add a row consistent with existing `Assigned Tests (6) →` / `Specifications (6) →` pattern:

```
Preparation Steps:   Configured · Pending Approval →     (or "Configured · Approved →" / "Not configured →")
```

### 5.3 Dashboard notification tile

Per recon D0.3: add `PendingPreparationConfigApproval` count to `DashboardNotificationService.cs`
and `DashboardService.cs`, mirroring the existing `ApprovalWaiting` query shape. Surfaces as a
dashboard KPI tile for Section Head, same pattern as existing pending-review queues.

---

## 6. Snapshot Read-Site Migration

All 9 sites identified in recon B5 currently read live from `SamplePreparation` — this is
**unchanged**, since `SamplePreparation` itself is already being written as a snapshot copy
(Section 3.2). No reader needs to be redirected to a different table. The only change these sites
need is tolerance for the two new nullable/boolean fields (`SourceConfigurationId`,
`WasConfirmedFromConfig`), which none of the 9 current readers touch — **confirm this during build
that none of them break on the new columns being present but unused by their logic.**

No read-site logic changes required. This significantly de-risks the build relative to a
"redirect all readers to a new table" approach.

---

## 7. Audit Trail — 4-Registry Checklist

Per recon E.2, apply to `ItemPreparationConfiguration`:

1. `UserReferenceRegistry.cs` — register `CreatedByUserId`, `ApprovedByUserId`.
2. `auditTypes.ts` (`ENTITY_DISPLAY_NAMES`) — add `"ItemPreparationConfiguration"` → "Preparation
   Configuration".
3. `auditEnumLabels.ts` (`AUDIT_ENUM_LABELS`) — add `ApprovalStatus` enum mapping (reuse existing
   `ApprovalGateStatus` labels if already mapped elsewhere, e.g. from Media/Cryovial — confirm and
   reuse rather than duplicate).
4. `AuditTraceabilityService.cs` — add branch connecting `ItemPreparationConfiguration` to its
   parent `Item`, and connecting each `SamplePreparation.SourceConfigurationId` back to the config
   it was snapshotted from, so a sample's full traceability view shows both its own snapshot and a
   link to the config version it came from.

`CaptureAuditEntries` requires no manual instrumentation (automatic on `SaveChanges`) — only the
above 4 display/linking registries need explicit entries.

---

## 8. Permissions

No new permission code. `Items.Manage` gates the "Approve" action on the Item Configuration page
(D5). No change to who can trigger the fallback manual-entry path — same as who can currently
access `TestPreparationForm` today (analyst assigned to the sample, existing
`SegregationOfDutiesGuard` check unchanged).

---

## 9. Out of Scope / Explicitly Deferred

- No per-test-within-item granularity (D1 — revisit only if real-world need for differing prep by
  test type on the same item emerges).
- No formal "Rejected" terminal state distinct from edit-and-resubmit (D3).
- No change to Water/EM/After Cleaning **workflow status logic**. The one exception is the
  StorageCondition/StorageTimeHours capture relocation into Water's Select Location dialogue
  (Section 4.4) — this is a field-capture addition, not a workflow change; Water's status
  transitions, gating, and the dialogue's core purpose (location selection) are unaffected.
- No change to `SegregationOfDutiesGuard` logic beyond what already exists.
- Category-string normalization inconsistency (recon A3 point 1) — noted, not addressed by this
  feature; separate cleanup item if ever prioritized.

---

## 10. Build Sequencing (proposed)

1. `ItemPreparationConfiguration` entity + migration + `UserReferenceRegistry` + audit registries
   (Section 3.1, 7).
2. `SamplePreparation` snapshot field additions (Section 3.2) — additive migration, no breaking
   change to existing 9 read sites.
3. `SignatureMeaning.PreparationConfirmed` + label (Section 3.3).
4. Backend: config CRUD endpoints (Item Configuration page), approve endpoint (`Items.Manage`
   gated), updated `SamplePreparationService.PrepareAsync` to branch on config existence and handle
   both the confirm-flow write and the fallback-seeds-config write (Section 4).
5. Frontend: `ConfirmPreparationForm` component; `PreparationDialog.tsx` fifth branch; Item
   Configuration page new tab + Overview summary row + dashboard tile (Section 4.2, 5).
6. D4 cleanup — remove vestigial fields from `TestPreparationForm.tsx`/`SamplePreparationService.cs`,
   bundle into step 4/5, same files being touched.
7. **Targeted mini-recon** on `WaterPreparationForm`/`WaterWorkflowEngine.PrepareAsync` (Section 4.4
   pre-build check) — do this before step 8, not in parallel, since it may surface constraints that
   change the field relocation design.
8. Water Select Location dialogue changes (Section 4.4) — add `storageCondition`/`storageTimeHours`
   capture, informed by step 7's findings.
9. Regression check on the 9 B5 read sites (Section 6) — confirm no breakage, no logic changes
   expected.
