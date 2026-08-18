# Operational/Historical Data Inventory: Old LIMSV2 → Neon (Phase 2 discovery)

**Status: READ-ONLY INVENTORY. No SQL, no migration script, no writes of any kind were
performed. Both databases were only queried with `SELECT`.**

This covers the 10 items you listed as not included in the completed master-data migration.
Table names below are the actual Postgres table names in the old `LIMSV2` database (confirmed
by querying `information_schema`, not guessed from code).

| # | You said | Actual table | Rows |
|---|---|---|---:|
| 1 | Media Challenge Specs | `MediaChallengeSpecs` | 33 |
| 2 | Media Preparation / Prepared Media records | *see note below* | — |
| 3 | Media Evaluations | `MediaEvaluations` | 20 |
| 4 | Media Evaluation Challenges | `MediaEvaluationChallenges` | 46 |
| 5 | Cryovial batches | `Cryovials` | 15 |
| 6 | Thaw Events | `ThawEvents` | 15 |
| 7 | Identity Confirmation Entries | `IdentityConfirmationEntries` | 17 |
| 8 | Confirmatory Media Selections | `ConfirmatoryMediaSelections` | **0** |
| 9 | Confirmatory Plate Observations | `ConfirmatoryPlateObservations` | **0** |
| 10 | After Cleaning config | `Machines` (7), `MachineParts` (25), `MachinePartConfigurations` (17) | — |

## ⚠️ Please resolve before I go further

**Item 2 is ambiguous — I did not guess.** There is no table literally named "Media
Preparation" in the database. Two candidates:
- **The `Media` table itself** — this stores prepared-media lot records (LotNumber,
  PreparedAt, PreparedByUserId, ApprovalStatus, etc.) and **was already fully migrated** in
  the master-data pass (23/23 rows). If this is what you mean, it's already done.
- **`MediaUsages`** — a table that logs when a prepared Media lot is *consumed* by a
  TestOrder (`MediaId`, `TestOrderId`, `UsedAt`, `UsedByUserId`). This table **currently has 0
  rows** in the source database.

**Items 8 and 9 currently have 0 rows in the source database.** If your screenshots show
records for Confirmatory Media Selections / Confirmatory Plate Observations, they may be from
a different environment/snapshot than the `LIMSV2` database I have local access to (confirmed
via `Host=localhost;Port=5432;Database=LIMSV2`), or those screens may write to a table I
haven't matched correctly. Please confirm which database/environment the screenshots came
from before I build a migration plan for these two — right now there is nothing to migrate.

Everything below documents what does exist, in full, so the rest of the dependency map is
ready regardless of how items 2/8/9 get resolved.

---

## 1. MediaChallengeSpecs — 33 rows

**Columns:** `Id` (PK), `MaterialName` (text — **not a FK**, just a free-text label), `EvaluationType`
(int: 0/1/2 seen — 12/18/3 rows respectively), `ChallengeRole` (int, nullable), `ExpectedDescription`
(text, nullable), `OrganismId` (int, NOT NULL).

**Foreign keys:** `OrganismId → Organisms.Id` only. That's it — no FK to `Materials` despite the
column name; `MaterialName` is descriptive text and can drift out of sync with the real
`Materials` table (e.g. it says `"MacConkey agar"` rather than pointing at a specific
`Materials.Id`).

**Dependencies:** `Organisms` — already fully migrated in phase 1 (all 33 rows' `OrganismId`
values resolve with zero orphans, verified).

**User references:** none.

**Seed vs. real:** This looks like **reference/config data**, not transactional history — it's
a static challenge-organism panel per media class (e.g. "for XLD Agar, `Escherichia coli`
challenges as the negative control, expect black-centered colonies"). No timestamps, no
"who/when" columns, no link to any specific test run. Structurally this belongs with the phase-1
master-data set, not the historical/operational set — **recommend treating it the same way
as `TestWorkflowStepMedias` was in phase 1** (business-key resolved via `Organisms.ScientificName`,
straightforward, no dependency on TestOrders/Samples/Incubations at all).

---

## 3. MediaEvaluations — 20 rows

**Columns:** `Id` (PK), `MediaId` (NOT NULL), `EvaluationType` (int), `Status` (int),
`Outcome` (int, nullable), `AssignedAt` (timestamptz), `CompletedAt` (timestamptz, nullable),
`CompletedByUserId` (int, nullable).

**Foreign keys:** `MediaId → Media.Id`. All 20 rows resolve with zero orphans against the
23 `Media` rows already migrated in phase 1 — this table is safe from that angle.

**Dependencies:** `Media` (already migrated). No dependency on TestOrders/Samples/Incubations.

**User references:** `CompletedByUserId` — values seen: `1, 4, 5, 7, null`. Same "don't
blindly copy" concern as phase 1: these are dev-DB `Users.Id` values (1=admin, 4=MMA, 5=MMASH,
7=MMAAN) that won't mean the same thing in Neon.

**Seed vs. real:** This is a GPT (growth-promotion test) evaluation record per prepared Media
lot — genuine lab activity (each row has real `AssignedAt`/`CompletedAt` timestamps spanning
Aug 1–9, 2026). A few evaluations do target Media lots prepared from the phase-1 "seed"
Material (`Materials.Id = 4`, "Tryptic Soy Agar" / manufacturer "Seed Data" / batch
"SEED-0001") — e.g. Media Id 9 ("TSA/04/26"). Nothing distinguishes these at the DB level;
they're not flagged differently from genuine lab-prepared evaluations, just worth knowing the
underlying Media lot itself traces back to demo/seed data for a few rows.

---

## 4. MediaEvaluationChallenges — 46 rows

**Columns:** `Id` (PK), `MediaEvaluationId` (NOT NULL), `CryovialId` (nullable — 41 of 46 set, 5
null), `ChallengeRole` (nullable), `InitialInoculum` (text), `IncubationId` (**NOT NULL — all
46 rows**), `OldMediaCount`/`NewMediaCount`/`RecoveryPercent` (numeric, nullable),
`GrowthObserved` (bool, nullable), `ObservedDescription`/`ExpectedDescription` (text),
`IsTurbid` (bool, nullable), `Outcome` (int, nullable), `ReadAt` (timestamptz, nullable),
`ReadByUserId` (int, nullable), `OrganismId` (NOT NULL).

**Foreign keys:**
- `MediaEvaluationId → MediaEvaluations.Id` (item 3, above)
- `CryovialId → Cryovials.Id` (item 5, nullable)
- `IncubationId → Incubations.Id` (**not in your list — see "additional dependency" below**)
- `OrganismId → Organisms.Id` (already migrated)

**Dependencies — the important finding:** every single one of the 46 rows has a non-null
`IncubationId`. `Incubations` was correctly excluded from your master-data migration as
transactional data (105 rows total, mostly tied to real `TestOrders`/`Samples`). **However**,
I checked all 46 `Incubations` rows this table actually points at, and every one of them is a
**self-contained, standalone row**: `TestOrderId IS NULL`, `StepName = 'MediaEvaluation'`,
`ParentIncubationId IS NULL`. None of them link onward to `TestOrders`, `Samples`, or
`WorkflowStepResults`. So the real dependency is narrow and safe: **this specific 46-row
subset of `Incubations`** (`WHERE "TestOrderId" IS NULL AND "StepName" = 'MediaEvaluation'`),
not the whole table. Those 46 rows themselves only reference `Equipment.IncubatorEquipmentId`
(already migrated) and `Media.MediaId` (already migrated) — no further chain.

**User references:** `ReadByUserId` — values seen: `1, 4, 5, 7`. Same admin-substitution
question as phase 1.

**Seed vs. real:** Real evaluation-challenge readings tied to the 20 MediaEvaluations above —
same seed/real mix as item 3 (a few trace back to the phase-1 seed Material via their parent
MediaEvaluation, most don't).

---

## 5. Cryovials — 15 rows

**Columns:** `Id` (PK), `Code`, `VialsRemaining`, `MaterialId` (NOT NULL), `ManufacturerName`,
`ExpiryDate`, `NumberOfVialsPrepared`, `StorageCondition`, `PhysicalCheckText`,
`ApprovalStatus`, `IsDestroyed`, `OrganismNameSnapshot`, `PreparedAt`, `OrganismId` (NOT NULL),
`PreparedByUserId` (NOT NULL), `ApprovedAt` (nullable), `ApprovedByUserId` (nullable).

**Foreign keys:** `MaterialId → Materials.Id`, `OrganismId → Organisms.Id`. Both resolve with
zero orphans against phase-1 migrated data.

**Dependencies:** `Materials`, `Organisms` (both already migrated). Self-contained otherwise.

**User references:** `PreparedByUserId` (NOT NULL — values `0, 4, 5`; note `0` is not a valid
user, same pattern as phase-1 `Media.PreparedByUserId`) and `ApprovedByUserId` (nullable —
values `4, 5, 10, null`). Note `10` = "Amal Hamdy" in the full Users table (see §User mapping
below) — a named individual, not one of the initials-only dev accounts.

**Seed vs. real:** Genuine lab data — 15 distinct culture-stock vials with real dates, some
already `IsDestroyed = true` (3 of 15). Two data-quality observations worth a human eyeballing
(not something I'll silently fix):
- Cryovial `Id=2` (`Code = "8739/01/26"`) has `MaterialId=10` (Material's own `OrganismId=1`,
  Escherichia coli) but the Cryovial's own `OrganismId=21` (Burkholderia cenocepacia) — a
  mismatch between the vial's stated organism and its source Material's organism. Same
  pattern on a couple of other rows.
- Cryovial `Id=11` references `OrganismId=17`, which is the placeholder organism row from
  phase 1 (`ScientificName = "..."`). FK will resolve fine (it's already migrated), just an
  odd source-data placeholder to be aware of.

---

## 6. ThawEvents — 15 rows

**Columns:** `Id` (PK), `CryovialId` (NOT NULL), `ThawedAt`, `ThawedByUserId` (NOT NULL),
`Notes` (nullable).

**Foreign keys:** `CryovialId → Cryovials.Id` (item 5, above). All resolve.

**Dependencies:** `Cryovials` only.

**User references:** `ThawedByUserId` — values seen: `1, 5`.

**Seed vs. real:** Straightforward real log entries (one Cryovial, `Id=2`/`Id=6`, was thawed
twice — consistent with normal lab use, not a data-quality flag).

---

## 7. IdentityConfirmationEntries — 17 rows

**Columns:** `Id` (PK), `CryovialId` (NOT NULL), `MediaId` (NOT NULL),
`IncubatorEquipmentId` (NOT NULL), `IncubationStart`, `IncubationEnd`, `ObservationText`.

**Foreign keys:** `CryovialId → Cryovials.Id`, `MediaId → Media.Id`,
`IncubatorEquipmentId → Equipment.Id`. All three resolve against already-migrated data
(`Cryovials` is item 5 in this same batch; `Media` and `Equipment` are phase-1 master data).

**Dependencies:** `Cryovials`, `Media`, `Equipment`. **No User reference at all** — this is
the one table in this batch with zero User-mapping concern.

**Seed vs. real:** Genuine identity-confirmation observations (colony morphology / catalase
notes) tied to real Cryovial/Media pairs.

---

## 8. ConfirmatoryMediaSelections — 0 rows

**Columns (schema only, no data to assess):** `Id` (PK), `WorkflowStepResultId` (NOT NULL),
`MaterialId` (NOT NULL), `MediaId` (NOT NULL), `EquipmentId` (NOT NULL), `WasAnalystAdded`.

**Foreign keys:** `WorkflowStepResultId → WorkflowStepResults.Id` (**not in your list, and
itself 0 rows in source right now**), plus `Materials`/`Media`/`Equipment` (already migrated).
No User columns.

**Nothing to migrate today.** If rows appear later, note the `WorkflowStepResults` dependency —
that table is itself downstream of `Incubations` + `TestOrders`, i.e. deep transactional data,
not something this batch's approach (business-key resolution) can cleanly handle. Flag for a
dedicated look if/when it's populated.

---

## 9. ConfirmatoryPlateObservations — 0 rows

**Columns (schema only):** `Id` (PK), `WorkflowStepResultId` (NOT NULL), `MaterialId` (NOT
NULL), `Observation` (int), `ExpectedAppearanceSnapshot` (nullable), `RecordedByUserId` (NOT
NULL), `RecordedAtUtc`.

**Foreign keys:** same `WorkflowStepResultId → WorkflowStepResults.Id` dependency as item 8,
plus `Materials`. **User reference:** `RecordedByUserId` (would apply once populated).

**Nothing to migrate today** — same status and same caveat as item 8.

---

## 10. After Cleaning configuration — Machines (7), MachineParts (25), MachinePartConfigurations (17)

**Machines** — `Id` (PK), `Name`. No FKs, no User columns. 7 rows, all distinct names (CTX,
CAM, PG, OSD I, Fette, CMb4D, ACG), no duplicates.

**MachineParts** — `Id` (PK), `MachineId` (NOT NULL), `Name`. FK: `MachineId → Machines.Id`.
25 rows, `(MachineId, Name)` pairs confirmed unique (no duplicates) — usable as a business key.

**MachinePartConfigurations** — `Id` (PK), `MachinePartId` (NOT NULL), `TestType`, `TestCode`,
`AlertLimit`, `ActionLimit`, `SpecLimit`, `IsPathogenTest`. FK: `MachinePartId → MachineParts.Id`.
17 rows, `(MachinePartId, TestCode)` pairs confirmed unique — usable as a business key. This is
structurally identical to phase 1's `Items` + `Specifications` pattern (a parent entity with
child spec-limit rows keyed by TestCode).

**Dependencies:** self-contained three-level chain: `Machines → MachineParts →
MachinePartConfigurations`. No User references anywhere in this group. No link to
`Materials`/`Media`/`Organisms`/etc. This is **pure reference/config data**, same category as
phase 1's `Departments`/`Rooms`, and — like `MediaChallengeSpecs` above — arguably should have
been in the phase-1 master-data batch rather than this operational-data batch. It's the
cleanest, lowest-risk group in this entire inventory.

---

## Cross-cutting: User references and how they should map to Neon

The old DB's `Users` table (13 rows — more complete than what phase 1 saw, since phase 1 never
needed to read `Users` directly) is:

| Id | Username |
|---:|---|
| 1 | admin |
| 2 | smoketest.analyst |
| 3 | user |
| 4 | MMA |
| 5 | MMASH |
| 6 | MMAR |
| 7 | MMAAN |
| 8 | test.reviewer |
| 9 | dashqa.analyst |
| 10 | Amal Hamdy |
| 11 | Nadeen Mohamed |
| 12 | Ahmed Shawky |
| 13 | Mazen Asharaf |

Columns referencing this table across the 8 populated tables above:
`MediaEvaluations.CompletedByUserId`, `MediaEvaluationChallenges.ReadByUserId`,
`Cryovials.PreparedByUserId`, `Cryovials.ApprovedByUserId`, `ThawEvents.ThawedByUserId` (and,
once populated, `ConfirmatoryPlateObservations.RecordedByUserId`). None of these are
DB-enforced foreign keys (same loose-int pattern as phase 1's `Materials`/`Media` audit
columns) — Postgres won't stop a bad value, so nothing here is "safe by construction."

Real named users now appear (10=Amal Hamdy, and 11/12/13 exist too, unused in this batch but
present in `Users`), not just the initials-only accounts from phase 1. This makes the
**same decision phase 1 flagged more consequential**: do you want to keep collapsing everyone
to Neon's `admin` (phase 1's default), or is it worth provisioning matching Neon accounts for
at least the named/active users (Amal Hamdy, MMA, MMASH, MMAAN) so this historical attribution
is meaningful? Worth deciding before I draft the phase-2 script, since it affects every table
in this batch.

---

## Additional dependent tables required (summary)

| Table needed | Why | Scope needed |
|---|---|---|
| `Incubations` | `MediaEvaluationChallenges.IncubationId` (all 46 rows) | **Not the whole table** — only the 46 standalone rows where `TestOrderId IS NULL AND StepName = 'MediaEvaluation'`. Confirmed no further chain (no `ParentIncubationId`, no `TestOrderId`). |
| `WorkflowStepResults` | `ConfirmatoryMediaSelections`/`ConfirmatoryPlateObservations` | Currently moot — both tables are empty. Would become relevant only if those tables get populated, and `WorkflowStepResults` is itself downstream of `Incubations`+`TestOrders` (deep transactional data, out of scope for this batch's approach). |

Everything else (`Materials`, `Media`, `MediaTypes`, `Equipment`, `Organisms`) needed by this
batch was already fully migrated in phase 1, and I verified zero orphaned foreign keys against
that phase-1 data for every populated table above.

---

## Suggested batching for the phase-2 migration plan (not built yet, pending your sign-off)

**Batch A — pure config, no dependency questions (do first, same pattern as phase 1):**
`MediaChallengeSpecs`, `Machines` → `MachineParts` → `MachinePartConfigurations`.

**Batch B — Cryovial lineage (depends only on phase-1 data):**
`Cryovials` → `ThawEvents`, `IdentityConfirmationEntries`.

**Batch C — Media evaluation lineage (needs the narrow 46-row Incubations subset):**
The 46-row `Incubations` subset → `MediaEvaluations` → `MediaEvaluationChallenges`.

**Batch D — on hold:** `ConfirmatoryMediaSelections`, `ConfirmatoryPlateObservations`,
`MediaUsages` — 0 rows each; nothing to do until you confirm whether the screenshots point at
a different environment, or these are simply not populated yet.

**Item 2 clarification needed** before Batch anything is assigned to it.

I have not written any SQL or migration script for this batch, and made no changes to either
database. Let me know how you want items 2/8/9 resolved and the User-attribution question
answered, and I'll draft the phase-2 dependency-ordered migration plan and script the same way
phase 1 was done (business-key resolution, idempotent guards, validated on a disposable test
database before you review).
