# Plan — Adopt the new CLAUDE.md (AI Engineering & Quality Constitution)

**Goal:** Replace the current `CLAUDE.md` (agy-delegation focused) with the new
engineering/GMP constitution, and close the gaps where the new file describes
things the codebase does not yet have, so that every rule in it is *true* or
*enforced* — never aspirational.

**Out of scope:** the agy delegation sections. They are dropped from `CLAUDE.md`
by decision of the project owner; `.claude/commands/` (`/delegate`, `/research`,
`/review`) stay in place untouched and are used on demand.

---

## Gap analysis (verified against `main` @ f1018ac)

| # | New-file claim | Reality in repo | Action |
|---|---|---|---|
| G1 | §3.3.0 regulated-entity marker (`IRegulatedEntity` / `[Regulated]`) | None. Instead **everything is audited by default** in `MicroLimsDbContext.SaveChangesAsync`, with an explicit opt-out list `AuditCaptureExcludedTypes` (each entry justified) and `[AuditRedacted]` for secrets | **Rewrite the rule to describe the real mechanism** (Phase 0). Do *not* build an opt-in marker — opt-out is safer (a new entity cannot be forgotten). |
| G2 | §5.1 `backend/Directory.Build.props` + `Directory.Packages.props` | Absent; 26 `PackageReference` versions spread across 5 `.csproj` | Build it (Phase 1) |
| G3 | §5.4 "`EnableRetryOnFailure` is active" | **Not enabled** (`Program.cs:30` is plain `UseNpgsql`). 8 `BeginTransaction` call sites are not wrapped in an execution strategy | Make rule conditional now (Phase 0); decide on enabling (Phase 4) |
| G4 | §7.4 post-migration test asserts triggers exist via `pg_trigger` | `AppendOnlyAuditHardeningPostgresTests` checks UPDATE/DELETE are *rejected*, but nothing asserts the trigger *catalogue* | Add test (Phase 2) |
| G5 | §5.4 pagination `Math.Clamp(pageSize, 1, 100)` | 28 `pageSize` parameters; ~8 clamp, some use `Math.Min` only, KPI/Reporting/Discussion paths unverified | Audit + architecture test (Phase 2) |
| G6 | §2 Postgres command uses `Database=microlims_test` | Fixture expects a **maintenance** DB and creates `microlims_test_<32hex>` itself (as CI does with `Database=postgres`) | Fix the command (Phase 0) |
| G7 | §4.5 MSW for HTTP isolation | MSW not installed; tests mock at `apiClient` boundary | Reword to "`apiClient` boundary mocks (MSW optional)" (Phase 0) |
| G8 | §6.1 `types/` folder | No `frontend/src/types/`; types live in `services/` and modules | Reword (Phase 0) |
| G9 | §4.5 / §4.6 / §6.1 frontend test & typing rules | 72 test files use `fireEvent`, 108 use `vi.mock` (many legitimately at `apiClient`), 325 `any` occurrences | Legacy backlog — ratchet, don't mass-rewrite (Phase 3) |
| G10 | §2 / §4.2 agents must run `[PostgresFact]` | CI does (`ci.yml` two-run gate). Cloud agent container has **no .NET SDK**; Postgres 16 binaries present but no running server | Environment setup script (Phase 2) |

Already true (no action): net10.0 + Nullable + ImplicitUsings in all 7 projects;
frontend versions; `[PostgresFact]`; `ClockUsageTests` (covers `DateTime(Offset).Now/UtcNow/Today`);
`AuditCredentialRedactionTests`; `RecordVersionMiddleware`; `refreshPromise`;
`AppErrorBoundary`; rate limiter after authentication; no nested transactions in
`SaveChangesAsync`; CI `has-pending-model-changes` check.

---

## Phase 0 — Adopt the file, corrected (docs only, 1 PR, low risk)

Replace `CLAUDE.md` with the new constitution, with these edits:

1. **§3.3.0** → "All entities tracked by `MicroLimsDbContext` are audited automatically.
   Exclusions live only in `MicroLimsDbContext.AuditCaptureExcludedTypes`, each with a
   justification comment; adding an entry requires explicit user approval and must
   not exclude any type on the regulated default list. Secret fields are marked
   `[AuditRedacted]` and covered by `AuditCredentialRedactionTests`."
2. **§5.4 execution strategy** → prefix with "When `EnableRetryOnFailure` is enabled
   (currently it is **not**; see `Program.cs`) …" and add: "Do not enable it as a
   side effect of another task."
3. **§5.1** → "Centralize … (introduced by Phase 1; until then keep versions
   consistent across `.csproj` files)". Removed once Phase 1 merges.
4. **§2** Postgres command → `Database=postgres` (maintenance DB); note the fixture
   creates and drops its own `microlims_test_<hex>` database, mirroring `ci.yml`.
5. **§4.5** → "`apiClient` boundary mocks (or MSW if introduced)".
6. **§6.1** → "in the module's `types.ts` or `services/`".
7. **§6 / §4.5–4.6** → add "applies to new and modified code; legacy files are
   migrated when touched (see Phase 3)" so agents don't start mass rewrites.
8. Fix header repo link casing (`toshy2020/microlims`).

**Verify:** docs-only; CI green. **Done when:** every factual statement in the file
matches the repo.

## Phase 1 — Central build & package management (1 PR, low–medium risk)

1. `backend/Directory.Build.props`: `TargetFramework=net10.0`, `Nullable=enable`,
   `ImplicitUsings=enable`, `LangVersion` if set; remove duplicates from each `.csproj`.
2. `backend/Directory.Packages.props` with `ManagePackageVersionsCentrally=true`;
   move all 26 versions; `.csproj` keep `<PackageReference Include="…" />` only.
   Resolve any version drift between projects to a single version (list in PR).
3. **`backend/Dockerfile`**: `COPY Directory.Build.props Directory.Packages.props ./`
   *before* `dotnet restore` (it currently copies only `.csproj` files — restore
   would break otherwise).
4. Check `.github/workflows/add-migration.yml` and `scripts/` for restore steps that
   copy individual files.

**Verify:** `dotnet restore/build/test` (both CI runs), `docker build backend`,
`dotnet ef migrations has-pending-model-changes` (must report none — no model change).
Remove the Phase-0 caveat in §5.1.

## Phase 2 — Enforcement tests & agent environment (2–3 PRs, low risk)

1. **Trigger catalogue test (G4):** new `[PostgresFact]` `AppendOnlyTriggerCatalogPostgresTests`
   — after the fixture applies all migrations, query `pg_trigger` joined to
   `pg_class` and assert every protected table (single shared list with
   `AppendOnlyAuditHardeningPostgresTests.AppendOnlyTables`) has its BEFORE
   UPDATE/DELETE trigger, enabled (`tgenabled <> 'D'`). Mutation check: dropping a
   trigger in a scratch migration must fail the test.
2. **Pagination (G5):** audit the 28 `pageSize` sites; introduce one helper
   (e.g. `Paging.Clamp(page, pageSize)` in `MicroLIMS.Shared`) and use it
   everywhere; add an architecture test that flags `.Skip(`/`.Take(` on a
   `pageSize` not passed through the helper (regex-based like `ClockUsageTests`).
   Behaviour change for callers sending >100 → document in PR.
3. **Agent environment (G10)** — environment *setting*, not repo code: add to the
   cloud environment's setup script: install .NET 10 SDK, start the local
   PostgreSQL (16 present; 18 preferred to match CI), create a `microlims_ci`
   role, and export `MICROLIMS_TEST_POSTGRES`. Configured by the owner under
   environment settings → Setup script. Without it agents must report Postgres
   tests as "not executed" per §7.5.

## Phase 3 — Frontend legacy backlog ratchet (ongoing, low risk)

No big-bang rewrite. Instead:
1. Record baselines (`fireEvent` files: 72, `vi.mock` files: 108, `any`: 325).
2. Add a tiny CI script that counts these and fails if any count **rises**
   (ratchet), lowering the baseline as files are migrated.
3. Triage `vi.mock` usages once: classify each as *boundary* (allowed) vs.
   *child component/hook stub* (violation, §4.5); fix violations in batches by module.
4. Migrate `fireEvent` → `userEvent` and remove `any` whenever a file is touched.

## Phase 4 — Execution strategy decision (optional, medium–high risk)

Decision for the owner. **Recommendation: keep retry disabled** for now — the
system is a single-database deployment, transient-failure retries are not a
stated requirement, and enabling it touches every transaction in regulated
workflows. If later required:
1. Enable `EnableRetryOnFailure` in `AddMicroLimsDbContext`.
2. Wrap all 8 `BeginTransaction` sites (`MicroLimsDbContext` ×2, `UnitOfWork`,
   `MasterDataWorkbookService`, `PeriodicReviewService`,
   `DocumentAcknowledgementService`, `DocumentEffectiveDateService`) in
   `CreateExecutionStrategy().ExecuteAsync`, moving any external side effects
   (email, storage, PDF) after commit and making delegates replay-safe.
3. `[PostgresFact]` tests that inject a transient failure and prove no double
   audit rows / double state changes.
4. Remove the Phase-0 conditional wording in §5.4.

---

## Order & effort

| Phase | PRs | Effort | Risk | Depends on |
|---|---|---|---|---|
| 0 Adopt corrected file | 1 | ~1 h | Low | — |
| 1 Central props | 1 | ~2–3 h | Low–Med | 0 |
| 2 Tests + environment | 2–3 | ~1 day | Low | 0 |
| 3 Frontend ratchet | ongoing | small per PR | Low | 0 |
| 4 Retry strategy | 1 | 1–2 days | Med–High | owner decision |

Each phase is verified with the full CLAUDE.md §2 command set, and the PR reports
executed vs. not-executed checks with exact pass/fail/skip counts (§7.5).
