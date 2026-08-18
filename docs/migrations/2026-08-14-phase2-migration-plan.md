# Phase 2 Migration Plan: Operational/Historical Data — Dev → Neon Production

**Status: SQL GENERATED AND VALIDATED, revision 3. Not executed against Neon. Neon was not
connected to at any point (no credentials in this environment). The source `LIMSV2` database
was only ever read with `SELECT` — never modified.** This revision reflects your final approved
user mapping, the generated SQL, and full validation results on a disposable local database.

Companion files:
- `2026-08-14-phase2-source-data-export.json` — full raw export of the exact Phase 2 dataset
- `2026-08-14-phase2-migration.sql` — the migration script (validated, see results below)

Phase 1 (Departments, Organisms, MediaTypes, Materials, Media, Equipment,
EquipmentInventories, Items, Specifications, TestDefinitions, TestWorkflowSteps,
TestWorkflowStepMedias, WaterSamplingPoints, Rooms) is assumed complete and already committed
to Neon — this plan only adds new tables on top of that.

---

## ✅ Final approved user mapping (your explicit instruction, applied exactly as given)

| Source | Neon target | How resolved in SQL |
|---|---|---|
| Source `Users.Id 1` (`admin`) | Neon `Users.Id 1` (`admin`) | Literal `1`, pre-flight-verified (`Id=1 AND Username='admin'`, aborts otherwise) |
| Source `Users.Id 4` (`MMA`) | Neon `Users.Id 2` (`MMA`) | Literal `2`, pre-flight-verified (`Id=2 AND Username='MMA'`, aborts otherwise) |
| Source `Users.Id 5` (`MMASH`) | Neon `Users.Id 5` (`MMASH`) | Literal `5`, pre-flight-verified (`Id=5 AND Username='MMASH'`, aborts otherwise) |
| Source `Users.Id 7` (`MMAAN`) | **New** Neon historical user, `Username='MMAAN'` | Created only if missing (`NOT EXISTS` on Username), then resolved by that Username at every use site |
| Source `Users.Id 10` (`Amal Hamdy`) | **New** Neon historical user, `Username='Amal Hamdy'` | Created only if missing, resolved by Username |
| Source `PreparedByUserId = 0` (invalid sentinel) | Neon `Users.Id 1` (`admin`) | Literal `1` — documented in the SQL and below as **unattributed/system placeholder, not evidence admin performed the action** |

All six cases are implemented in one place — a session-local SQL function
(`pg_temp.phase2_map_user`, auto-dropped when the session ends) — so every source-UserId →
Neon-UserId decision is documented with an inline comment exactly where it's applied, per your
"document every intentional transformation" requirement. None of `PasswordHash`,
`RefreshTokens`, `PasswordResetTokens`, `PasswordHistories`, or login/security history is read
from the source database anywhere in this script.

### New historical-attribution-only Neon users

Both created with: `PasswordHash = 'MIGRATED_HISTORICAL_NO_LOGIN'` (a fixed marker, not a real
or guessed password — cannot authenticate against any bcrypt check), `IsActive = FALSE` (no
login required, per your instruction), `MustChangePassword = TRUE`, `RoleId` resolved by
`Roles.Type` business key (Type is a fixed system enum seeded identically in every environment
by `DbSeeder` — confirmed by reading `DbSeeder.cs`):

| Username | FullName | RoleId resolved via | Source Role |
|---|---|---|---|
| `MMAAN` | Mohamed Mahmoud | `Roles.Type = 3` (Analyst) | matches source `RoleId=4` → Type=3 |
| `Amal Hamdy` | Amal Hamdy | `Roles.Type = 1` (Section Head) | matches source `RoleId=2` → Type=1 |

Both inserts are guarded by `WHERE NOT EXISTS (SELECT 1 FROM "Users" WHERE "Username" = ...)` —
created once, never duplicated, never overwritten on re-run.

---

## ✅ MediaEvaluations 19-row subset — confirmed by you, implemented as specified

You confirmed migrating exactly the 19 `MediaEvaluations` rows required by the 46 authorized
`MediaEvaluationChallenges` (excluding unused `Id=14`, the unfinished evaluation nothing
references). Implemented exactly as analyzed: `MediaEvaluations` insert is scoped to those 19
rows only; validation confirms `Id=14` was not migrated.

---

## Source row counts (Phase 2 scope)

| Table | Total source rows | Rows in scope for migration |
|---|---:|---:|
| MediaChallengeSpecs | 33 | 33 (all) |
| Machines | 7 | 7 (all) |
| MachineParts | 25 | 25 (all) |
| MachinePartConfigurations | 17 | 17 (all) |
| Cryovials | 15 | 15 (all) |
| ThawEvents | 15 | 15 (all) |
| IdentityConfirmationEntries | 17 | 17 (all) |
| Incubations | 105 | **46** (`TestOrderId IS NULL AND StepName = 'MediaEvaluation'` only) |
| MediaEvaluations | 20 | **19** (see flag above — excludes unfinished `Id=14`) |
| MediaEvaluationChallenges | 46 | 46 (all — all 46 already fall inside the narrow Incubations subset) |
| ConfirmatoryMediaSelections | 0 | 0 — no records created, per your decision 6 |
| ConfirmatoryPlateObservations | 0 | 0 — no records created, per your decision 7 |

**Media table:** not touched. Already fully migrated in Phase 1 (23/23 rows), per your
decision 1.

---

## Dependency map

```
Organisms (Phase 1, already in Neon)
Materials (Phase 1, already in Neon)
Media (Phase 1, already in Neon)
Equipment (Phase 1, already in Neon)
Users (existing Neon Users, plus proposed new historical-attribution-only Users — see mapping below)
        |
        v
MediaChallengeSpecs .......... needs: Organisms                              (no User refs)
Machines ...................... no dependencies                              (no User refs)
  -> MachineParts .............. needs: Machines                             (no User refs)
      -> MachinePartConfigurations . needs: MachineParts                     (no User refs)
Cryovials ...................... needs: Materials, Organisms, Users          (PreparedByUserId, ApprovedByUserId)
  -> ThawEvents ................ needs: Cryovials, Users                     (ThawedByUserId)
  -> IdentityConfirmationEntries  needs: Cryovials, Media, Equipment         (no User refs)
Incubations (46-row subset) .... needs: Media, Equipment                     (StartedByUserId is NULL for all 46 -- no User refs in practice)
  -> MediaEvaluations (19-row subset) . needs: Media, Users                  (CompletedByUserId)
      -> MediaEvaluationChallenges .... needs: MediaEvaluations, Cryovials (nullable), Incubations, Organisms, Users  (ReadByUserId)
```

`MediaChallengeSpecs` and the `Machines`/`MachineParts`/`MachinePartConfigurations` chain are
fully independent of everything else in this batch — they only need Phase-1 data already in
Neon.

The `Cryovials` chain and the `Incubations`/`MediaEvaluations`/`MediaEvaluationChallenges`
chain are independent of *each other* except where `MediaEvaluationChallenges.CryovialId`
links across (41 of 46 rows; 5 have `CryovialId = NULL`) — meaning `Cryovials` must be inserted
before `MediaEvaluationChallenges`, even though `Cryovials` doesn't otherwise depend on the
evaluation chain.

**Confirmed:** every `Materials`/`Media`/`Organisms`/`Equipment` reference across all Phase 2
data resolves with **zero orphans** against what Phase 1 already migrated (verified by
LEFT JOIN checks). No Phase 1 gaps block Phase 2.

---

## Source users referenced by Phase 2 records (reference table — mapping now resolved above)

The separate-identity concern from revision 2 is resolved — see "Final approved user mapping"
at the top. This table is kept as the reference record of every source UserId Phase 2 touches:

| Source UserId | Source Username | Source FullName | Referencing table(s) | Ref. record count | Approved Neon target |
|---:|---|---|---|---:|---|
| 1 | `admin` | System Administrator | MediaEvaluationChallenges (8), MediaEvaluations (5), ThawEvents (13) | 26 | Neon `Id 1` (`admin`) |
| 4 | `MMA` | Mohamed Mahmoud (Email: `toshy2020@gmail.com`) | MediaEvaluationChallenges (22), MediaEvaluations (10), Cryovials.PreparedByUserId (1), Cryovials.ApprovedByUserId (1) | 34 | Neon `Id 2` (`MMA`) |
| 5 | `MMASH` | Mohamed MAhmoud *(sic — source has a typo capitalization, carried as-is)* | MediaEvaluationChallenges (2), MediaEvaluations (1), Cryovials.PreparedByUserId (2), Cryovials.ApprovedByUserId (1), ThawEvents (2) | 8 | Neon `Id 5` (`MMASH`) |
| 7 | `MMAAN` | Mohamed Mahmoud | MediaEvaluationChallenges (14), MediaEvaluations (3) | 17 | New Neon historical user (`MMAAN`) |
| 10 | `Amal Hamdy` | Amal Hamdy | Cryovials.ApprovedByUserId (1) | 1 | New Neon historical user (`Amal Hamdy`) |
| **0** *(invalid)* | — *(not a real Users.Id — sentinel/bug value, same pattern flagged in Phase 1 for `Media.PreparedByUserId`)* | — | Cryovials.PreparedByUserId (12 of 15 rows) | 12 | Neon `Id 1` (`admin`), documented as unattributed/sentinel |

These five source identities (plus the invalid-`0` sentinel) are the complete set of
User-references anywhere in Phase 2 data. No others exist in this batch. Validated: the
disposable-database test run's actual attribution counts (see "Validation results" below)
reconcile exactly against the "Ref. record count" column above.

### Notes on the source `Users` table itself

- Source `Users.Id = 4, 5, 6, 7` (`MMA`, `MMASH`, `MMAR`, `MMAAN`) all have `FullName = "Mohamed
  Mahmoud"` (one with a typo, "MAhmoud") and different `RoleId` values (1, 2, 3, 4
  respectively) — these look like the same person's multi-role test accounts (one per role,
  for testing RBAC), not four different lab staff. `Id = 6` (`MMAR`) isn't referenced by any
  Phase 2 data, so it isn't in the table above.
- Source `Users.Id = 3` has `Username = "user"` but `FullName = "MMA"` — looks like a stray/
  mislabeled test account. Not referenced by any Phase 2 data, so no action needed, just
  flagging the oddity.
- Not every source user with a real name is referenced by Phase 2 data — `Nadeen Mohamed` (11),
  `Ahmed Shawky` (12), `Mazen Asharaf` (13) don't appear in any of the tables in this batch, so
  they're excluded from the table above (nothing to migrate for them here).

### `PreparedByUserId = 0` Cryovials (12 of 15 rows) — resolved, per your instruction 6

`0` was never a valid `Users.Id` in the source database (Ids start at 1) — the same
invalid-sentinel pattern Phase 1 found in `Media.PreparedByUserId`. Per your instruction 6,
this stays mapped to Neon `Id 1` (`admin`) as an unattributed/sentinel case — this is not
"historical attribution" being erased (there was never a real historical user behind `0` to
begin with). Implemented in `pg_temp.phase2_map_user` with an inline comment stating exactly
this — that the mapping is a NOT-NULL placeholder, not evidence admin performed the action.
Validated: all 12 affected Cryovials rows resolve to Neon `admin` in the test run.

---

## Data-quality warnings (informational — not auto-corrected)

1. **Cryovials organism mismatches.** A few Cryovials have an `OrganismId` that doesn't match
   their source `Materials.OrganismId` — e.g. Cryovial `Id=2` (`Code="8739/01/26"`) is made
   from `MaterialId=10` (that Material's own organism is *Escherichia coli*), but the Cryovial
   itself is tagged `OrganismId=21` (*Burkholderia cenocepacia*). Several other rows show the
   same pattern. Carried through as-is; not something I'll silently "fix" to match the parent
   Material.
2. **Cryovial `Id=11`** references `OrganismId=17`, the placeholder organism from Phase 1
   (`ScientificName = "..."`). FK resolves fine (already migrated), just an odd source value.
3. **`MediaEvaluationChallenges.Id in scope for MediaEvaluationId=16`** has two challenge rows
   for the *same organism* under the same evaluation, four different organisms each duplicated
   (Organism 2, 3, 4, 5 each appear twice with `ChallengeRole = NULL`). Could be a legitimate
   repeat reading or a genuine double-entry — I can't tell which from the data alone. Both
   copies are kept as-is per the "don't drop data without sign-off" approach used in Phase 1
   (same spirit as the Macconkey duplicate).
4. **`Cryovials.PreparedByUserId = 0`** — see the decision block above; this is a real
   data-quality gap affecting 80% of Cryovial rows.
5. **Source `Users` table has duplicate-person, multi-role test accounts** (Ids 4/5/6/7, all
   "Mohamed Mahmoud") — see notes above. Not a blocker, just context for the mapping decision.

---

## Exact insert order (as implemented in the SQL)

0. **Pre-flight guards** — verify Neon `Users` Id/Username triples (1/admin, 2/MMA, 5/MMASH),
   Phase-1 tables non-empty (Organisms/Materials/Media/Equipment), and both required Roles
   (Type=1 Section Head, Type=3 Analyst) exist. Aborts the whole transaction before any writes
   if any check fails.
1. **`pg_temp.phase2_map_user` function** — created (session-local, self-documenting).
2. **Users** — create `MMAAN` and `Amal Hamdy` historical accounts if missing.
3. **MediaChallengeSpecs** — needs only Phase-1 `Organisms`.
4. **Machines** — independent.
5. **MachineParts** — needs `Machines`.
6. **MachinePartConfigurations** — needs `MachineParts`.
7. **Cryovials** — needs Phase-1 `Materials`/`Organisms` + step 2's `Users`.
8. **ThawEvents** — needs step 7 `Cryovials` + step 2 `Users`.
9. **IdentityConfirmationEntries** — needs step 7 `Cryovials` + Phase-1 `Media`/`Equipment`.
10. **Incubations (46-row subset)** — needs Phase-1 `Media`/`Equipment` only.
11. **MediaEvaluations (19-row subset)** — needs Phase-1 `Media` + step 2 `Users`.
12. **MediaEvaluationChallenges (46 rows)** — needs step 11 `MediaEvaluations`, step 7
    `Cryovials` (nullable), step 10 `Incubations`, Phase-1 `Organisms`, step 2 `Users`.
13. **Sequence resets** for all 10 Phase-2 tables plus `Users`.

---

## Conflict / idempotency strategy (as implemented)

Every INSERT guarded by `WHERE NOT EXISTS (... "Id" = ...)` (never overwrite), plus a
business-key guard wherever a reliable natural key exists. Every candidate key was checked
against the actual data for real duplicates before use:

| Table | Business key used for guard + FK resolution | Verified unique in source? |
|---|---|---|
| MediaChallengeSpecs | (`MaterialName`, `OrganismId` resolved via `Organisms.ScientificName`, `EvaluationType`, `ChallengeRole`) | Yes, no duplicates found |
| Machines | `Name` | Yes |
| MachineParts | (`MachineId` resolved via `Machines.Name`, `Name`) | Yes |
| MachinePartConfigurations | (`MachinePartId` resolved via Machine+Part name pair, `TestCode`) | Yes |
| Cryovials | `Code`, plus a Materials-identity verification (`MaterialName`+`BatchNumber` match at the literal `MaterialId`) since Materials has no reliable business key (same caveat as Phase 1) | Yes, no duplicates |
| ThawEvents | (`CryovialId`, `ThawedAt`) | Yes, no duplicates |
| IdentityConfirmationEntries | (`CryovialId`, `MediaId`, `IncubationStart`) | Yes, no duplicates |
| Incubations (subset) | (`MediaId`, `StepName`, `StartedAt`) | Yes, no duplicates |
| MediaEvaluations (subset) | (`MediaId` resolved via `Media.LotNumber`, `EvaluationType`) | Yes, no duplicates in the 19-row subset |
| MediaEvaluationChallenges | (`MediaEvaluationId`, `IncubationId`) *(not `OrganismId`+`ChallengeRole` — that pair has real duplicates, see warning #3 above)* | Yes, no duplicates on this pair |
| Users (new historical accounts) | `Username` (`MMAAN`, `Amal Hamdy`) — safe here specifically because these are newly-minted accounts this script itself creates, not an assumption that an existing username implies identity | Confirmed via validation: idempotent on re-run, no duplicates |

`Id` values are preserved from source where possible (all these tables use plain integer
identity PKs), sequences reset afterward, and the post-migration verification query compares
actual vs. expected counts.

---

## Validation results

Performed the full validation sequence you specified, entirely on a disposable local database
(`microlims_migration_test`, created then dropped — never the source `LIMSV2` DB, never Neon):

1. **Fresh schema** — created the test database, applied the current EF Core migrations
   (`dotnet ef database update`), confirmed identical schema to Phase 1's validated run.
2. **Seeded a Neon simulation** — the 4 system `Roles` (matching `DbSeeder`) and 5 `Users` rows
   deliberately laid out to match your approved mapping exactly: `Id 1=admin`, `Id 2=MMA`,
   `Id 3`/`Id 4` = unrelated Neon-native accounts (to prove the script doesn't touch them),
   `Id 5=MMASH`.
3. **Applied Phase 1** (`2026-08-14-master-data-migration.sql`) — required dependency, ran
   clean, all 14 tables matched expected counts (as previously validated).
4. **Applied Phase 2** — first attempt caught two real bugs (documented, both fixed before
   the final script):
   - An all-`NULL` VALUES column (`Incubations.CompletedAt`, `NULL` in all 46 rows) was
     defaulting to Postgres `text` type instead of `timestamptz`, since no row in that column
     carried an explicit cast. Fixed by always casting `NULL` literals explicitly
     (`NULL::timestamptz`, `NULL::numeric`, `NULL::boolean`, `NULL::text`) rather than only
     casting non-null values.
   - That same fix caused `pg_temp.phase2_map_user`'s `integer` parameter to stop matching
     (the now-explicitly-numeric-typed columns no longer implicitly converted to a plain
     `integer` function argument). Fixed by changing the function parameter to `numeric`.
   - After both fixes, the script ran clean.
5. **Row counts — exact match on first run:**

   | Table | Expected | Actual |
   |---|---:|---:|
   | MediaChallengeSpecs | 33 | 33 |
   | Machines | 7 | 7 |
   | MachineParts | 25 | 25 |
   | MachinePartConfigurations | 17 | 17 |
   | Cryovials | 15 | 15 |
   | ThawEvents | 15 | 15 |
   | IdentityConfirmationEntries | 17 | 17 |
   | Incubations | 46 | 46 |
   | MediaEvaluations | 19 | 19 |
   | MediaEvaluationChallenges | 46 | 46 |
   | Historical users created | 2 | 2 |

6. **Foreign-key integrity** — zero orphans checked across every table (`MediaChallengeSpecs`→
   Organisms, `MachineParts`→Machines, `MachinePartConfigurations`→MachineParts, `Cryovials`→
   Materials+Organisms, `ThawEvents`→Cryovials, `IdentityConfirmationEntries`→Cryovials+Media+
   Equipment, `Incubations`→Media+Equipment, `MediaEvaluations`→Media, `MediaEvaluationChallenges`
   →MediaEvaluations+Incubations+Organisms, and the nullable `MediaEvaluationChallenges.CryovialId`).
7. **User attribution — verified by joining each audit column back to `Users.Username` and
   reconciling totals against the approved-mapping table above:**

   | Neon identity | Cryovials.Prepared | Cryovials.Approved | ThawEvents | MediaEvaluations | MediaEvaluationChallenges | **Total** | **Expected** |
   |---|---:|---:|---:|---:|---:|---:|---:|
   | `admin` (Id 1) | 12 *(the `0`-sentinel rows)* | 0 | 13 | 5 | 8 | **38** | 26 real + 12 sentinel = 38 ✅ |
   | `MMA` (Id 2) | 1 | 1 | 0 | 10 | 22 | **34** | 34 ✅ |
   | `MMASH` (Id 5) | 2 | 1 | 2 | 1 | 2 | **8** | 8 ✅ |
   | `MMAAN` (new historical) | 0 | 0 | 0 | 3 | 14 | **17** | 17 ✅ |
   | `Amal Hamdy` (new historical) | 0 | 1 | 0 | 0 | 0 | **1** | 1 ✅ |

   Every total reconciles exactly with the analysis. The two new historical users were created
   with `PasswordHash = 'MIGRATED_HISTORICAL_NO_LOGIN'` (confirmed — not a real or copied hash)
   and `IsActive = FALSE`.
8. **Scope guards confirmed:**
   - `Incubations`: exactly 46 rows, zero outside `TestOrderId IS NULL AND StepName =
     'MediaEvaluation'` — no unrelated Incubations leaked in.
   - `MediaEvaluations.Id = 14` (the unused/unfinished evaluation): confirmed **not** migrated
     (0 rows).
   - `ConfirmatoryMediaSelections` / `ConfirmatoryPlateObservations`: confirmed still 0 rows —
     script creates nothing there, as instructed.
9. **Idempotency — ran the entire script a second time against the same (now-populated)
   database:**
   - All 10 table counts identical to the first run (zero duplicate rows).
   - Zero errors.
   - `Users` count unchanged at 7 (5 original + 2 historical) — no duplicate historical users
     created.
   - Spot-checked the 5 original Neon-simulated `Users` rows (Id 1–5, including their
     `PasswordHash` values) — byte-for-byte unchanged after the second run, confirming nothing
     pre-existing was modified.
10. **Neon:** not connected to at any point — no credentials in this environment, and the
    instruction was explicit not to touch it regardless.
11. **Source `LIMSV2`:** only ever queried with `SELECT` throughout this entire session.

Disposable test database dropped after validation completed.

---

## Summary

Everything requested has been produced and validated:
- ✅ Phase 2 SQL: `2026-08-14-phase2-migration.sql`
- ✅ Phase 2 plan (this document, revision 3)
- ✅ Source export: `2026-08-14-phase2-source-data-export.json`
- ✅ Disposable-database rebuild, full Phase 2 execution, and re-execution — all results above

**Stopping here per your instruction. Nothing has been executed against Neon. Awaiting your
review and approval before any real execution.**
