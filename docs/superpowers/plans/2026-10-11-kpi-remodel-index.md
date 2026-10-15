# KPI and Performance Remodel - Plan Index

Spec: `docs/superpowers/specs/2026-10-11-kpi-performance-remodel-design.md`. Four slice plans, executed strictly in this order. Each slice ends with the app building, tests green and the old KPI page still working until S4 removes it.

| Order | Plan | Ships | Depends on | Migration |
|---|---|---|---|---|
| S1 | `2026-10-11-kpi-remodel-s1-workflow-profiles.md` | `WorkflowProfileVersions` (append-only, e-signed), resolver, `WorkflowProfileService`, `/api/kpi/workflow-profiles` API, `Kpi.ConfigureProfiles`, Workflow Profiles panel/dialog mounted from the old tab, legacy weight write path closed | none | `AddWorkflowProfileVersions` (table, immutability trigger, legacy import as Draft, permission SQL) |
| S2 | `2026-10-11-kpi-remodel-s2-kpi-registry-dashboard.md` | `IKpi` registry (11 KPIs), `/api/kpi/dashboard`, `/api/kpi/{key}/records`, `/api/kpi/definitions`, `AnalystStageTarget`, `Kpi.ViewTeam`, frontend types and service (no UI) | S1: `LoadResolverAsync`, `WorkflowProfileResolver`, `FixedTimeProvider`, `TestServiceFactory.WorkflowProfile` | `SeedKpiViewTeamPermission` (SQL only) |
| S3 | `2026-10-11-kpi-remodel-s3-workload-capacity.md` | `KpiSettings`, `AnalystAvailabilities`, capacity maths, `AnalystWorkloadService`, 7 routes under `/api/kpi` (workload, workload/tests, settings, availability), D8 own-row enforcement, frontend types and service (no UI) | S1 (resolver), S2 (`KpiQuery`, `KpiGap`, `KpiRecordPageDto`, `KpiConstants`, `KpiScope`, `AnalystStageTarget`) | `AddKpiSettingsAndAnalystAvailability` (two tables, no data step) |
| S4 | `2026-10-11-kpi-remodel-s4-kpi-page.md` | New KPI and Performance page (tiles, sections, workload table, Definitions and Records drawers, PDF export), Reports tab gated by `Kpi.View`; removal of `KpiController`, the old frontend KPI code, all `KpiService` methods except `GetOverdueAnalystStageSamplesAsync`, the legacy weight seed | S2, S3 (and S1 dialog) | none |

Migration order and names (EF stamps the time; each file name must sort after `20261011103646_AddMediaStorageEntriesAndCryovialStorageEquipment`): `AddWorkflowProfileVersions` (S1), `SeedKpiViewTeamPermission` (S2), `AddKpiSettingsAndAnalystAvailability` (S3). `DropLegacyWorkloadWeights` is deferred to the next release and is in no plan.

Permissions (identical in every layer: constant, `All`, `Enforced`, DbSeeder catalog + SectionHead grant, migration SQL, `RolePermissionSeedDataTests`, `PermissionEnforcementMigrationPostgresTests.AddedByLaterMigrations`, authorization matrix, `routes.ts`):

| Code | Default grants | Slice | SectionHead count | Total grants |
|---|---|---|---|---|
| `Kpi.ConfigureProfiles` | SystemAdministrator, SectionHead | S1 | 41 to 42 | 119 to 121 |
| `Kpi.ViewTeam` | SystemAdministrator, SectionHead | S2 | 42 to 43 | 121 to 123 |

Starting numbers 41 and 119 were confirmed against `RolePermissionSeedDataTests.cs` lines 116, 198, 208 on 2026-10-13.

## Review changes made on 2026-10-13 (cross-slice consistency pass)

1. S3 now uses S2's `AnalystStageTarget.DaysFor` for the analyst-stage deadline and `KpiScope.ParseCategory` for category parsing (both had been re-implemented locally). S3 `BuildQueryAsync` now trims blank filters like S2.
2. S1 Task 7 (close the legacy `PUT workload-weights`) must run AFTER Task 10, so the old page never calls a removed endpoint. Run order 1-6, 8, 9, 10, 7.
3. S4 used `TestServiceFactory.KpiRegistry(...)`; S2 provides `TestServiceFactory.BuildKpiRegistry(db, time)` (a method named `KpiRegistry` would shadow the type). S4 now uses the S2 name and no longer plans a factory change.
4. S4 file table said the `ReturnToAnalystTests` KPI method is deleted; Task 12 (and the contract) retarget it at the `right-first-time` KPI. Table corrected.
5. All four plans carry a repository state note (see the hazard section).
6. Checked and found consistent, no change needed: type, method, route and file names across slices; JSON field names between C# DTOs and the TypeScript types (S1, S2, S3 and what S4 imports); permission strings and role grants; no file created by two slices; migration names unique and in slice order; no placeholders left.

## Repository state and the migration/snapshot hazard

Written against `refactor/consolidation`. When the plans were written, ~41 unrelated files were dirty, including the untracked migration `20261011103646_AddMediaStorageEntriesAndCryovialStorageEquipment` and a modified `MicroLimsDbContextModelSnapshot.cs`. At review time (2026-10-13) commit `570cf02a` has committed that work, and `git status` shows only `AnalystKpiTab.tsx`, `AnalystKpiService.ts` and `reportingTypes.ts` modified (the uncommitted 2026-10-11 KPI page patch). The hazard is therefore currently dormant, but it can return, so every executor must:

- run `git status` before the first edit of a slice; if the snapshot, `IMicroLimsDbContext.cs`, `MicroLimsDbContext.cs`, `ServiceCollectionExtensions.cs`, `DbSeeder.cs`, `TestServiceFactory.cs`, `PermissionConstants.cs` or `routes.ts` carry someone else's hunks, either wait for that work to be committed or stage only KPI hunks with `git add -p`; never `git add -A`, `git add .` or `git commit -a`;
- check the generated migration `Up()` contains only KPI operations (no Media or Cryovial operations, no `AlterColumn` or `DropColumn` on foreign tables) and that its file name sorts after `20261011103646`;
- note the migration Designer files embed the full model, so an unrelated model change in the working tree ends up in them;
- recommended before S1: commit the existing KPI page patch (the three frontend files) on its own, because S1 edits `AnalystKpiTab.tsx` and `AnalystKpiService.ts` and S4 deletes all three.

## Commands

Backend (always `--artifacts-path`, because a running `MicroLIMS.API` locks the bin folders; run from `E:\MicroLIMS\MicroLIMS`):
```
dotnet build backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --artifacts-path "$TEMP/kpi-art"
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~Kpi|FullyQualifiedName~WorkflowProfile"
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art"
MICROLIMS_WRITE_AUTH_MATRIX=1 dotnet test backend/MicroLIMS.Tests --filter AuthorizationMatrixTests --artifacts-path "$TEMP/kpi-art"
MICROLIMS_TEST_POSTGRES=<value PostgresTestConfiguration.cs expects> dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~Postgres"
dotnet ef migrations add <Name> --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API --configuration Release
dotnet ef database update --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API --configuration Release
```
Without `MICROLIMS_TEST_POSTGRES` the `[PostgresFact]` tests skip; a skip is not a pass. Every slice that adds a migration (S1, S2, S3) must run the Postgres tests once, and S4 once more (the seed no longer writes weights).

Frontend (from `frontend`):
```
npx vitest run src/modules/kpi
npm test
npx tsc --noEmit -p tsconfig.json
npm run lint
```
Manual check: set `VITE_API_BASE_URL=https://localhost:65435` (the default `http://localhost:5000/api` does not match the backend launch profile).

Tools: no `grep` or `rg` on this machine; use the Grep tool or `git grep`. Every commit ends with `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.

## SPEC DEVIATIONS (from the shared contract; flag for the human)

1. **Tile 1 TAT.** End event is `Sample.ApprovedAt`, not `ReviewWorkflowEvent`. Population: samples with `ApprovalDecision == Approve` and `ApprovedAt` in period, excluding `OriginSampleId != null` retests. Reason: `ReviewWorkflowEvent` is polymorphic (Media and Cryovial write it too), has several events per multi-lab sample, and its `Timestamp` ignores `TimeProvider`.
2. **Decision-based rates (tiles 4 and 5).** Both use `SampleSectionSignoff.ApprovalDecision` and the decision time (signature time, falling back to `ApprovedAt`), not `ReviewWorkflowEvent`. Denominator: distinct samples with at least one decided signoff in period. `OOSInvestigation` is never written, so tile 5 counts samples decided in period with `OosGroupCode != null`. Tile 4 counts `RetestRetainedSample` and is a subset of tile 5 (the Definitions drawer says so). Retest clones are excluded.
3. **Tile 2 on time.** Same population as tile 1. The analyst stage starts at `SamplePreparation.PreparedAt`; samples without such a row (FP section, EM, retest clones) cannot be judged, are excluded and reported as a gap. Review is `SubmittedForReviewAt` to `ReviewedAt` and approval `ReviewedAt` to `ApprovedAt` per section signoff, each at most 24 calendar hours. On time only if every section meets both.
4. **"Tests approved in period" (tile 3 and S3).** `TestOrder.Status == Approved`, `!IsSuperseded`, with the section signoff `Status == Approved` and `ApprovedAt` in period. `ResultRecord.ApprovedAt` is sample-level, so it is not used. Count `TestOrder` rows. Profile resolution time is the signoff `ApprovedAt`.
5. **RFT.** Pathogen Observation tests cannot generate a `TestReturnEvent`; they are excluded and a gap is shown. Returns are matched by `TestOrderId`.
6. **Returns by cause.** `TestReturnEvent` has only free-text `Reason`, so the supporting KPI is `returns-by-test` (by test code, never by person); reason text appears only in records.
7. **WIP backlog (tile 6).** Counts `Pending` and `InProgress` only, as the spec says; In Review and In Approval appear only as sample-level `backlog-aging` stages. Aging anchor is `SamplePreparation.PreparedAt`, falling back to `Sample.ReceivedAt`. An unresolved target uses the 7-day fallback with a gap chip.
8. **Incubation reads (tile 7).** Adds `TestOrderId != null` to exclude media-challenge incubations. Stage-1 to stage-2 transfers count as reads (known limitation, documented in the Definition).
9. **EM excursion (tile 8).** Population: EM `ResultRecord` rows with level `WithinLimit`, `AlertLevel` or `ActionLevel`, by `ResultEnteredAt`, excluding `OriginSampleId != null`. `Location` filters `ResultRecord.SubjectName` here (elsewhere item name or water point code). Adverse-trend thresholds are constants in `KpiConstants`, not stored configuration (upgrade path: a settings table); a flag needs at least 5 results.
10. **Legacy import (spec 2.4).** Only `WorkloadWeights` rows with a matching `TestDefinition` are imported, and S1 additionally skips definitions whose workflow type is retired (they could never be signed). The legacy `INVESTIGATION` row has no definition and is skipped. The spec's "INVESTIGATION profile exists" is wrong (investigations are not a workflow type); S3 does not rely on it. No placeholder Draft type-default rows are seeded (`CK_..._AnyValue` forbids empty rows); the coverage endpoint and "Needs configuration" filter show the gaps instead.
11. **Profile versions are mutable only as Draft to Effective.** A DB trigger blocks everything else; `Version` (xmin) protects against double signing. `AddVersionAsync` inserts as Draft and flips to Effective in the same unit of work (the trigger forbids updating an Effective row, and attaching the signature is an update); externally it behaves as "creates an Effective row".
12. **WorkflowType drift.** The resolver reads `TestDefinition.WorkflowType` at calculation time, so re-typing a test moves its history between type defaults. Overrides by `TestCode` are unaffected. The spec gives no snapshot; flagged.
13. **Attribution (S3).** Analyst is `TestOrder.AssignedAnalystId` (final assignee). Rows are active `Role.Type == Analyst` users in scope plus any assignee with delivered units. Unassigned open tests are a gap, not attributed. Weighted on time inherits the sample's analyst-stage verdict (the spec's own ceiling).
14. **D8 vs default roles.** By default only Admin and SectionHead hold `Kpi.View`, so D8 matters only if an administrator grants `Kpi.View` to Reviewer or Analyst. Headline tiles stay lab-level aggregates for any `Kpi.View` holder.
15. **Counts.** `KpiController` has 15 actions today (spec says 14). S1 removes one, S4 removes the remaining 14.
16. **Existing bugs.** The old `UpdateWorkloadWeight` bound `weight` to a differently named property and `ClaimTypes.Name` is never in the JWT. The new APIs use identical field names on both sides (each pinned by a test) and resolve names from the database.
17. **Legacy tables** stay one release as read-only (entities, `DbSet`s and the two `UserReferenceRegistry` lines for `WorkloadWeight.ChangedByUserId` and `WorkloadWeightHistory.ChangedByUserId`; the contract said one line, the code has two). The drop migration is out of scope.
18. **Resolver date rule (added in review of S1).** The contract's rule 2 omitted `EffectiveFrom <= t` for the type default; spec 2.2 and D4 include it, so the resolver applies it to both steps.
19. **Ratio scale.** Every `%` KPI value and every workload ratio is a fraction (1.0 = 100%); the frontend formats it in one place (`utils/kpiFormat.ts`). `KpiQuery.To` and `KpiDashboardDto.To` are the exclusive upper bound (selected end date plus one day).
20. **Capacity is capped at today** (S3): a period ending in the future does not add capacity for days that have not happened. Rates are `null` below 5 tests with a `RATES_TOO_FEW_RECORDS` gap. When a category, location or test filter is set, utilisation and forward load are null with a `CAPACITY_NOT_FILTERED` gap.
21. **Additive members beyond the contract** (all additive): S1 `ResolveDefault`, `HasOverride`, `WorkflowProfileModelTests`; S2 `AnalystStageTarget`, `KpiCore`, `StubSectionScope`, `KpiTestData`, `TestServiceFactory.BuildKpiRegistry`, optional `ILogger`/scope parameters; S3 `AnalystWorkloadService.BuildQueryAsync`, `KpiSettingDefaults`, `KpiUserScope`, `ScratchPostgresDatabase`, three service registrations instead of four (`CapacityCalculator` is static); S4 `useGuardedFetch`, `kpiFilters.ts`, `kpiFixtures.ts`, `KpiDashboardFilterTests`, a Reports permission test and an optional `OverviewTab` prop.

## Unresolved items that need a human decision

1. **Per-workflow target in the dashboard "overdue" list.** Spec section 7 puts "per-workflow target in stage windows" in S2. S2 applies it to tile 2 and backlog aging but deliberately leaves `KpiService.GetOverdueAnalystStageSamplesAsync` (read by `DashboardService`) on the fixed 7-day rule, because changing its constructor breaks seven test sites and S4 prunes `KpiService` anyway. Decide whether the dashboard list must also honour profiles; the upgrade path is documented in S2 ("How the per-workflow target reaches DashboardService").
2. **Initial calibration.** Every imported weight is a Draft, so all weighted measures show "unweighted" gaps until a Section Head reviews and signs each one (and sets type defaults, which are not seeded). Someone must supply standard hours; `HoursPerAnalystDay = 6.0` is provisional.
3. **Retired-type tests.** A test whose definition still has a retired workflow type can never be weighted until it is re-typed (its legacy weight is not imported).
4. **OOS tile label.** The spec wants the OOS tile labelled "not an analyst penalty". The text is in the KPI Definition (hover and Definitions drawer) but there is no always-visible caption on the tile. Add one in S4 Task 2 if the hover text is not enough.
5. **Concurrent baselines** are not locked (S1 `ponytail:` note): two simultaneous first versions of one scope could both claim the baseline exception. Fine for one Section Head; add a partial unique index if it matters.
6. **Postgres runs** need `MICROLIMS_TEST_POSTGRES` (a connection string to a throw-away server); I did not set or invent one. A slice is not done until its Postgres tests have run un-skipped.
7. **Spec sections not delivered by design:** invalidated OOS rate, per-test submission timestamps, recovery rate and per-step weights (all spec section 11 "Deferred").
