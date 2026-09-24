# Laboratory Separation — Microbiology and Physicochemical

Date: 2026-09-24 · Branch: `feat/fp-hplc-foundation` (local only — no push, no production changes)

## 1. Intent

Run the Microbiology Laboratory and the Physicochemical Laboratory (today "Finished Product Laboratory") as two separate labs inside one MicroLIMS: each has its own testing area, inventory and configuration, and neither lab's rules, queues or configuration can affect the other. A sample is still received once and may be tested by both labs.

Trigger: on 2026-09-24 FP results on a mixed sample never reached review, because a micro-only rule (a sample leaves Received only when an incubation starts) blocked the whole sample. Fixed in b42344b; this design removes the class of problem.

## 2. Decisions (user, 2026-09-24)

| # | Decision |
|---|---|
| D1 | One sample, split by lab (not one sample per lab). Built on the existing section model (approach 1): the two `DocumentSection` rows are the two labs. |
| D2 | A main **Receiving** area for a separate sample-receipt function, controlled by privileges, not a new role: `Samples.Receive`, `Samples.TrackAll`. |
| D3 | Main Receiving receives **FP, RM and PM** samples only, and the receiver must choose the **target laboratory** (Microbiology, Physicochemical, or both). |
| D4 | Each lab keeps **receiving inside its own workspace**; a sample received there loads only that lab's tests. **Water** and **After cleaning** are received only in the lab workspaces (both labs); **Environmental Monitoring** only in the Microbiology workspace. |
| D5 | A lab can be **added to an existing sample** (same reference). |
| D6 | Menu split into areas shown by privilege / lab membership. **Items** and **Receiving configuration** live in a separate **General Laboratory Configuration** area, Section Heads (and System Administrator) only. |
| D7 | Specifications stay on the shared item; each lab owns its own parameter rows. |
| D8 | Materials stock and equipment are per lab; **every equipment inventory asset belongs to exactly one lab**. |
| D9 | One lab rejecting does **not** close the other lab. The other lab may **Close testing** (signed, reason). Cancelled tests keep the exact stage they had reached, shown in the sample summary. |
| D10 | A rejected sample **does** get a combined CoA. |
| D11 | Rename "Finished Product Laboratory" → "Physicochemical Laboratory" everywhere users see it; internal code names stay. |

## 3. Receiving

### 3.1 Main Receiving page (Receiving area, `Samples.Receive`)
- Categories offered: Finished Product, Raw Material, Packaging Material. Water, EM and After-cleaning are not offered.
- Today's receive form plus a required **Target laboratory** choice (Microbiology / Physicochemical / both). Each option shows the count of the item's assigned tests (for the chosen production stage) that belong to that lab; a lab with none is disabled.
- Server: `ReceiveItemBasedSampleRequest` gains `TargetSectionIds` (required, ≥ 1). `ProductWorkflowEngine.ReceiveAsync` creates orders only for assigned tests whose Test Master section is in the target set. Refused: empty set; a target lab with no tests; category not FP/RM/PM on this endpoint path.
- The receipt audit entry records the target labs.

### 3.2 Receiving inside a lab workspace
- Same form with no lab choice — the workspace's lab is the target. Requires lab membership and the existing testing permission, not `Samples.Receive`.
- Server enforces the target: a caller may target only labs they are a member of (System Administrator: any). A lab user cannot create another lab's orders.
- Categories: Microbiology — FP, RM, PM, Water, EM, After cleaning. Physicochemical — FP, RM, PM (Water / After cleaning later, §11 Q1).

### 3.3 Add laboratory (`Samples.Receive`)
- Action on an existing sample: choose a lab not yet on the sample; signed, with reason. Creates that lab's orders (item's assigned tests for that lab) on the same sample and reference.
- Refused when the sample is Rejected, Voided or Cancelled, or the lab is already on the sample, or the lab has no tests for the item.
- An overall-Approved sample returns to In progress until the added lab finishes.

### 3.4 Tracking board (Receiving area, `Samples.TrackAll`)
- One row per sample: reference, product, batch, received at, overall status, and one column per lab: not requested / testing / under review / under approval / approved / rejected / retest requested / closed, with the queue it is waiting in.
- Filters: lab, overall status, date range, product. A row opens the sample summary, which shows per-test stages, including cancelled-at-stage.

## 4. Lab areas and navigation

| Area | Shown to | Contents |
|---|---|---|
| Receiving | `Samples.Receive` / `Samples.TrackAll` | Receive sample (FP/RM/PM) · Tracking board |
| Microbiology Laboratory | Micro members | Workspace (incl. receiving: FP/RM/PM/Water/EM/After cleaning) · My tasks · Review / approval queues · Media preparation & evaluation · Cryovials · Micro Test Master, organisms, media configurations, water, EM, after-cleaning, equipment configuration · Materials stock and equipment inventory (micro) · Micro dashboards |
| Physicochemical Laboratory | Physicochemical members | Workspace (incl. receiving: FP/RM/PM) · My tasks · Review / approval queues · Suitability runs · Calibration runs · Physicochemical Test Master, equation types, instruments, columns · Materials stock and equipment inventory (physicochemical) · Dashboards |
| General Laboratory Configuration | Section Heads, System Administrator | Items (with specifications) · Receiving configuration |

Unchanged, outside the lab areas: Document control, Reports, OOS tracking (already scoped by lab), Users/roles, Audit. A user in both labs sees both lab areas.

The menu is the UI reflection only; every list and by-id action is scoped and 403-guarded on the server (existing `IUserSectionScopeService`).

## 5. Cross-lab outcomes

### 5.1 States
Per lab (`SampleSectionSignoff`): Testing → Under review → Under approval → Approved / Rejected / Retest requested, plus new **Closed**.

Overall sample status:
1. Rejected — any lab Rejected (a Closed lab only exists on a rejected sample).
2. In progress — any lab still Testing / Under review / Under approval (reported per lab stage, as today's roll-up).
3. Retest requested — a lab waits on its retest.
4. Approved — every requested lab Approved.

A lab that was never requested has no column value ("not requested") and does not take part in the roll-up.

### 5.2 One lab rejects
- Overall status becomes Rejected at once.
- The other lab is **not** closed: it keeps its queue and live stage. The existing "Rejected closes the other sections' pending work" behaviour is removed.
- If it continues, its tests go through review and approval as normal and its verdict is recorded next to the rejection.

### 5.3 Close testing
- Available to the other lab's Section Head **only after another lab has rejected** the sample. (Outside that case, stopping work stays with the existing signed void / cancel.)
- Electronic signature and required reason (default "Sample rejected by <lab>").
- Each open test of the closing lab becomes **Cancelled**, recording the step and stage it had reached (e.g. "Incubating, stage 2"). Approved results are kept. The lab's sign-off becomes Closed. A `ReviewWorkflowEvent` and audit entry record who, why, when.
- The sample summary shows cancelled tests with their stage at closure.

### 5.4 Retest
Unchanged: a retest / new-sample request carries only the deciding lab's tests; the other lab continues.

### 5.5 CoA
- Single-lab CoA: available when that lab is Approved, also on a rejected sample (existing option).
- Combined CoA: available when every requested lab is final (Approved, Rejected or Closed). Conclusion: Rejected (naming the rejecting lab) if any lab rejected, else Approved. A closed lab's cancelled tests appear as "Not completed — testing closed" with their stage.

## 6. Inventory and configuration

| Master | Today | Change |
|---|---|---|
| Test Master (`TestDefinition.SectionId`) | per lab | menu placement |
| Equipment master (`Equipment.SectionId`) | per lab | menu placement; lists / edits scoped |
| Chromatography columns | per lab | Physicochemical area |
| Materials stock (`Material.SectionId`) | per lab | menu placement |
| **Equipment inventory** (`EquipmentInventory`) | no lab | add required `SectionId`; lists / edits scoped; back-fill from linked `Equipment`, unlinked assets listed for the user to assign before the column becomes required |
| **Specifications** | no lab | no column: owner = Test Master section of the row's `TestCode`; a Section Head may add / edit / delete only own-lab rows, other rows read-only |
| Media configurations, organisms, cryovials, EM | micro only | Microbiology area |
| Equation types, FP instruments | physicochemical only | Physicochemical area |
| Items, receiving configuration | shared | General Laboratory Configuration |

Equipment and instrument pickers in result entry offer only the test's own lab's equipment (verify in the isolation sweep).

## 7. Isolation sweep

Every place a micro rule acts on the whole sample moves to per-lab scope, each with a test where one lab finishes while the other is idle:
- Received → InTesting only on incubation (fixed b42344b).
- Preparation gate on mixed samples (partly fixed dc277a9 — re-check).
- Sample-level stage TAT, overdue flags, dashboard tiles still per sample (known follow-up from section work).
- CoA sections and the rejection roll-up (§5).

## 8. Rename

Data migration renames the `DocumentSection` "Finished Product Laboratory" → "Physicochemical Laboratory". User-visible text follows: menus, page titles ("Physicochemical Test Master", "Physicochemical Instruments"), CoA lab headings, dashboard labels. Unchanged: code names (`Fp…`, `WorkflowType` values), F.P / S.F production stages, the sample category "Finished Product".

## 9. Data changes

Each migration is applied to LIMSV2 only after a `pg_dump` backup in `E:/MicroLIMS/db-backups`.
1. `SectionSignoffStatus.Closed` (appended); on `TestOrder`: `CancelledAtStep`, `CancelledAtStage`, closing signature id; test order status Cancelled (appended if absent).
2. `EquipmentInventory.SectionId` (nullable, back-fill, then required once all rows are assigned).
3. Section rename; permissions `Samples.Receive`, `Samples.TrackAll` seeded and granted to System Administrator and Section Head roles.

## 10. Delivery (stop after each stage for user check)

1. Backend rules: rejection no longer closes the other lab, Close testing, overall status and CoA rules, add laboratory, lab-targeted receipt and category limits, privileges, isolation sweep.
2. Backend masters: equipment inventory lab, specification row ownership.
3. Frontend: four areas, Receiving page, tracking board, workspace receiving, add laboratory, Close testing, rename.
4. Browser end-to-end: a mixed sample where Physicochemical finishes while Microbiology is idle; review, approve and CoA of the physicochemical lab (completes the pending FP review / approve / CoA browser test); reject / close / combined CoA.

Every stage ends with the full Postgres suite (`bash .claude/scripts/run-postgres-tests.sh <artifacts>`).

## 11. Resolved questions

- Q1 (2026-09-24): physicochemical water / after-cleaning testing is LATER. Now: Water and After-cleaning receiving stays in the Microbiology workspace only; the Physicochemical workspace receives FP / RM / PM. Water / after-cleaning configuration stays micro.
- Q2 (2026-09-24): `Samples.Receive` and `Samples.TrackAll` are seeded for System Administrator and Section Head roles only.
