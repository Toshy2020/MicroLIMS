# KPI Remodel S2 - KPI Registry and Dashboard Contract Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.
> **Repository state note (checked 2026-10-13, see `2026-10-11-kpi-remodel-index.md`):** commit `570cf02a` has since committed the Media/Cryovial migration `20261011103646_...` and its snapshot hunks, so the "UNTRACKED migration / dirty snapshot" statements below describe the earlier state. Only `AnalystKpiTab.tsx`, `AnalystKpiService.ts` and `reportingTypes.ts` carry the uncommitted 2026-10-11 KPI page patch. Always re-run `git status` first; if shared files are dirty with someone else's hunks, the `git add -p` protocol in these plans still applies.

**Goal:** Add a backend KPI registry (eleven `IKpi` classes) and one dashboard/records/definitions API that returns the eight headline tiles plus three supporting breakdowns under a single, server-scoped filter, each with explicit empty, partial, prior-period and error states.

**Architecture:** Each KPI is a small sealed class behind `IKpi` that loads a minimal projection, computes a `KpiCore` (value, numerator, denominator, N, gaps, series) and lets `KpiScope.ComputeAsync` add the equal-length prior period, status and "too few records" flag. `KpiRegistry` collects the classes; `KpiDashboardService` builds the `KpiQuery` (UTC half-open window, section scope from `IUserSectionScopeService`, D8 analyst rule), runs every KPI in isolation and returns one `KpiDashboardDto`; `KpiDashboardController` exposes `/api/kpi/dashboard`, `/api/kpi/{key}/records`, `/api/kpi/definitions`. The per-workflow analyst-stage target (S1 profiles, 7-day fallback, gap data) lives in one helper, `AnalystStageTarget`, used by tile 2 and backlog aging.

**Tech Stack:** ASP.NET Core 10 + EF Core (Npgsql; InMemory for unit tests), xUnit, React + TypeScript (types and service only), Vitest.

**Spec:** `docs/superpowers/specs/2026-10-11-kpi-performance-remodel-design.md` - section 3 (registry, `KpiQuery`/`KpiResult`, `/dashboard`, `/{key}/records`, error and empty states), section 4.1 tiles 1-8, section 4.3 (per-workflow analyst target, 7-day fallback, gap chip), section 4.4 (backlog aging), D7, D8, the `Kpi.ViewTeam` permission of section 6, and the unit/integration test list of section 8. Names, signatures and routes follow the shared contract (S2 = contract section 3). Depends on S1 (`WorkflowProfileService.LoadResolverAsync`, `WorkflowProfileResolver`, `FixedTimeProvider`, `TestServiceFactory.WorkflowProfile`, `WorkflowProfileVersion`).

---

## Global Constraints

- **Preconditions.** S1 is merged on this branch: `WorkflowProfileVersions` DbSet, `WorkflowProfileService.LoadResolverAsync(CancellationToken)`, `WorkflowProfileResolver.Resolve(string testCode, DateTime at)` returning `ResolvedProfile(WorkUnits, TargetAnalystDays, WorkUnitsVersionId, TargetVersionId)` with `IsWeighted`/`IsTargetAware`, `backend/MicroLIMS.Tests/Fixtures/FixedTimeProvider.cs` (`new FixedTimeProvider(DateTimeOffset)`), and `TestServiceFactory.WorkflowProfile(MicroLimsDbContext, TimeProvider?)`. Task 1 step 1 checks this and stops if anything is missing.
- `KpiConstants`: `MinSampleForRate = 5`, `DefaultTargetDays = 7`, `EmAlertRateAdverse = 0.025m`, `EmActionRateAdverse = 0.0015m`, `IncubationGraceHours = 4`. Review and Approval stage limits are 24 h each (calendar hours, inclusive).
- **Units of the numbers.** Every `%` KPI carries a **fraction** in `Value` (1.0 = 100%, rounded to 4 decimals); the frontend formats it. `days` values are rounded to 2 decimals. `tests` values are counts. (S3 and S4 already agree: "all ratios are fractions".)
- **Period.** The service turns inclusive `yyyy-MM-dd` `from`/`to` into the half-open UTC window `[from 00:00Z, to+1d 00:00Z)`. `KpiQuery.To` and `KpiDashboardDto.To` are therefore the **exclusive** bound. `PriorPeriod()` is `[From-(To-From), From)`.
- **Prior.** `KpiResult.Prior` is `null` unless the prior period's `N > 0`. Snapshot and breakdown KPIs (`wip-backlog`, `backlog-aging`, `stage-tat`, `returns-by-test`) never carry a prior.
- **Status.** `NoData` = `N == 0` for a non-count KPI (`Value` is then `null`, never 0% or 100%). `Partial` = at least one gap. Count KPIs (`wip-backlog`, `backlog-aging`, `returns-by-test`) are `Ok` with `Value = 0` when empty. `TooFewRecords` = `Unit == "%"` and `N < MinSampleForRate`.
- **D5.** A test whose `WorkUnits` does not resolve is excluded and reported as a gap. It is never defaulted to 1.0. A test whose `TargetAnalystDays` does not resolve uses the 7-day fallback and is reported as a gap (`DEFAULT_TARGET`).
- **D4.** Profiles are resolved at the **approval time** of the test (the section signoff's `ApprovedAt`, the sample's `ApprovedAt` for tile 2, `Now` for open work). A profile version added later never changes an earlier result.
- **D6.** No ranking, no leaderboard, no sort by a performance value. `returns-by-test` groups by test code, never by person. Series are ordered by time or by a fixed stage order.
- **D8 (server side).** `!canViewTeam` and `analystId` set and different from the caller -> `UnauthorizedAccessException` (403). `!canViewTeam` and `analystId == null` -> the headline KPIs are lab aggregates, unchanged. Analyst names in record lists are shown only when `canViewTeam` or the analyst is the caller.
- **Section scope.** `SectionIds == null` means unrestricted (administrator); an **empty list means see nothing** and must yield `NoData`, never an unfiltered result.
- **Errors.** A KPI that throws becomes `KpiEntryDto { Result = null, Error = "<fixed safe sentence>" }`, is logged with the exception, and never turns into zeros. The sentence never contains the exception text.
- Wire format: camelCase, enums as strings, `ApiResponse<object>.Ok(...)`, errors read `response.data.message`; validation throws `InvalidOperationException` (400), `NotFoundException` (404), `UnauthorizedAccessException` (403).
- Existing `/api/kpi/*` endpoints, `KpiService` and the old page stay untouched in S2 (S4 removes them). New route names must not collide with them (Task 11 checks).
- **Git hygiene.** About 41 unrelated files are dirty, including an UNTRACKED migration `20261011103646_AddMediaStorageEntriesAndCryovialStorageEquipment` and a modified `MicroLimsDbContextModelSnapshot.cs`. Stage ONLY the paths a task names with `git add <explicit path>`. Never `git add -A`, `git add .` or `git commit -a`. For shared files (`ServiceCollectionExtensions.cs`, `PermissionConstants.cs`, `DbSeeder.cs`, `routes.ts`, `TestServiceFactory.cs`, `RolePermissionSeedDataTests.cs`, `PermissionEnforcementMigrationPostgresTests.cs`, `authorization-matrix.txt`) run `git diff -- <file>` first and, if any hunk is not yours, use `git add -p <file>` and answer `y` only to your hunks. Never revert other work.
- **Tests** run with the artifacts-path form from `E:\MicroLIMS\MicroLIMS` (a running `MicroLIMS.API` locks the bin folders): `dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "<filter>"`. `grep`/`rg` are not installed: use the Grep tool or `git grep`. Postgres tests skip unless `MICROLIMS_TEST_POSTGRES` is set (see `backend/MicroLIMS.Tests/Fixtures/PostgresTestConfiguration.cs` for the expected value).
- Every commit message ends with a blank line and `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.
- Match surrounding code style; comments explain WHY, sparingly. Deliberate simplifications carry a `ponytail:` comment naming the ceiling.

## Review Focus

Inputs the spec implies but a first implementation would not test, most likely first. Each is pinned by a named test in the owning task.

1. **A new section, a quiet month, or the first day of the period.** No approved samples, no decisions, no EM results: every rate/TAT tile must say `NoData` with `Value = null` and no prior, the page still gets all eleven entries (not a 500, not 0%/100%), and count KPIs say 0. Pinned: Task 4 `SampleTat_EmptyPeriod_IsNoData_NotZero`; Task 7 `DecisionRates_EmptyPeriod_IsNoData`; Task 11 `Dashboard_EmptyDatabase_EveryRealKpiIsNoDataOrZero`.
2. **Profiles not configured yet (S1 imports are all Draft).** Right-first-time must be `NoData` plus an "n tests unweighted" gap, not 100% and not a weight of 1.0; on-time must fall back to the 7-day rule and show "n tests on the default 7-day target". Pinned: Task 6 `RightFirstTime_NoProfiles_IsNoData_NotOneHundredPercent`; Task 5 `OnTime_NoProfiles_UsesSevenDayFallback_AndReportsGap`.
3. **Rows that carry a timestamp but are not approvals.** A Reject signoff also has `ApprovedAt`; a retest clone sample has its own `ReceivedAt`; a superseded test can show `Approved`; a row exactly on the period end belongs to the next period. None may count. Pinned: Task 4 `SampleTat_MedianAndP90_ForApprovedSamplesOnly`; Task 6 `RightFirstTime_WeightsByUnits_ExcludesObservationAndUnweighted`; Task 4 `Prior_BoundaryRecord_BelongsToCurrentPeriodNotPrior`.
4. **A viewer whose scope covers only one lab of a two-lab sample, or none.** Tiles 4 and 6 judge the sample on the viewer's sections only. Tile 1, tile 2's population and stage-tat use the **whole-sample** rule: `Sample.ApprovedAt` is stamped only when the last open section approves, so a lab-1-only viewer sees the turnaround to the other lab's approval and the sample lands in that later period (documented in the Definition text, pinned by a test, per-section TAT is a follow-up). An empty scope list sees nothing; `null` sees everything. Pinned: Task 7 `Retest_SectionScope_UsesInScopeSignoffsOnly`; Task 4 `SampleTat_SectionScope_NullSeesAll_EmptySeesNothing`, `SampleTat_TwoLabSample_UsesWholeSampleApproval_ForAnyScope`; Task 2 `Scope_NullStaysNull_EmptyStaysEmpty`.
6. **Retest clones in tiles 4 and 5.** A retest clone (`OriginSampleId` set) copies the origin's `OosGroupCode`; counting it would double one OOS event and inflate the retest-rate denominator. Tiles 4 and 5 exclude clones, as tiles 1 and 2 do. Pinned: Task 7 `OosRate_OriginAndCloneInSameGroup_CountOnce`.
7. **Retest decisions in production carry a signature but no `ApprovedAt`.** `SampleApprovalService` stamps `ApprovedAt` only on Approve, Reject and chain resolution; a `RetestRetainedSample` or `NewSampleRequest` decision sets only the signature. Tiles 4 and 5 therefore take the decision time from `ApprovalSignature.SignedAt` (fallback `ApprovedAt`). Pinned: Task 7 `Retest_SignatureWithoutApprovedAt_CountsInSignedPeriod`.
5. **A profile version added after the work was approved** (or an `INVESTIGATION`/retired type edit) must not rewrite earlier results (D4), and a viewer without `Kpi.ViewTeam` must not see other analysts' names or numbers (D8) through the records endpoint either. Pinned: Task 6 `RightFirstTime_ProfileAddedAfterApproval_DoesNotRewriteEarlierTests`; Task 2 `Records_HideOtherAnalystNames_WithoutViewTeam`; Task 10 `ReturnsByTest_GroupsByTestCode_NeverByPerson`.

## File Structure

| Path | Action | Single responsibility |
|---|---|---|
| `backend/MicroLIMS.Application/Services/Kpi/KpiTypes.cs` | Create | Enums, `KpiQuery` (+`PriorPeriod`), `KpiResult` and every DTO of contract 3.2, `KpiConstants` |
| `backend/MicroLIMS.Application/Services/Kpi/IKpi.cs` | Create | The KPI interface |
| `backend/MicroLIMS.Application/Services/Kpi/KpiScope.cs` | Create | `KpiCore`, section/category/location/analyst filters, percentile and rate maths, prior-period wrapper, paging, shared decision-rate loader |
| `backend/MicroLIMS.Application/Services/Kpi/AnalystStageTarget.cs` | Create | Per-workflow analyst-stage target (max of resolved targets, 7-day fallback, default count) |
| `backend/MicroLIMS.Application/Services/Kpi/KpiRegistry.cs` | Create | Ordered, key-unique list of `IKpi` |
| `backend/MicroLIMS.Application/Services/Kpi/KpiDashboardService.cs` | Create | Query building, scope, D8, isolation of failing KPIs, records paging, definitions |
| `backend/MicroLIMS.Application/Services/Kpi/{SampleTat,OnTimeAllStages,RightFirstTime,RetestRate,OosRate,WipBacklog,IncubationReadsOnTime,EmExcursionRate,StageTat,BacklogAging,ReturnsByTest}Kpi.cs` | Create (11) | One KPI each |
| `backend/MicroLIMS.API/Controllers/KpiDashboardController.cs` | Create | HTTP surface, `canViewTeam` from the `permission` claim |
| `backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs` | Modify | Register the 11 `IKpi`, `KpiRegistry`, `KpiDashboardService` |
| `backend/MicroLIMS.Shared/Constants/PermissionConstants.cs` | Modify | `KpiViewTeam` const, `All`, `Enforced` |
| `backend/MicroLIMS.Persistence/Seed/DbSeeder.cs` | Modify | Catalog tuple + SectionHead grant |
| `backend/MicroLIMS.Persistence/Migrations/<ts>_SeedKpiViewTeamPermission.cs` (+Designer) | Create | Idempotent permission SQL |
| `backend/MicroLIMS.Tests/TestServiceFactory.cs` | Modify | `KpiDashboard(db, time, scope)` |
| `backend/MicroLIMS.Tests/Fixtures/StubSectionScope.cs` | Create | `IUserSectionScopeService` stub returning chosen section ids |
| `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiTestData.cs` | Create | In-memory fixture builders and the fixed clock constants |
| `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiMathTests.cs` | Create | Percentile, rate, monthly series |
| `backend/MicroLIMS.Tests/UnitTests/Kpi/AnalystStageTargetTests.cs` | Create | Target rules |
| `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiTileTests.cs` | Create | Per-KPI tests with fixed fixtures |
| `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiPriorPeriodTests.cs` | Create | Prior-period maths and boundaries |
| `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiScopeAuthorizationTests.cs` | Create | Dashboard service: scope, D8, isolation, paging, definitions |
| `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiRegistryTests.cs` | Create | The registry table of contract 3.3 and the empty dashboard |
| `backend/MicroLIMS.Tests/WorkflowTests/KpiFilteringTests.cs` | Modify | One regression test pinning the 7-day rule for `DashboardService` |
| `backend/MicroLIMS.Tests/IntegrationTests/KpiViewTeamPermissionMigrationPostgresTests.cs` | Create | Permission SQL up/down/idempotent |
| `backend/MicroLIMS.Tests/IntegrationTests/KpiDashboardGoldenPostgresTests.cs` | Create | Hand-calculated golden dataset on PostgreSQL |
| `backend/MicroLIMS.Tests/UnitTests/RolePermissionSeedDataTests.cs`, `IntegrationTests/PermissionEnforcementMigrationPostgresTests.cs`, `ArchitectureTests/authorization-matrix.txt` | Modify | New permission and new endpoints |
| `frontend/src/routes/routes.ts` | Modify | `KPI_VIEW_TEAM` |
| `frontend/src/modules/kpi/types/kpiTypes.ts`, `services/kpiDashboardService.ts` (+ `.test.ts`) | Create | Wire types and the three calls (no UI) |

Added to the contract's list (flag to the human): `AnalystStageTarget.cs`, `KpiMathTests.cs`, `KpiRegistryTests.cs`, `StubSectionScope.cs`, `KpiTestData.cs`, the `KpiCore` record and the optional `ILogger` / `IUserSectionScopeService` constructor/factory parameters (all additive, defaults keep the contract signatures valid).

## How the per-workflow target reaches `DashboardService` (decision)

`DashboardService` calls only `KpiService.GetOverdueAnalystStageSamplesAsync` (DashboardService.cs:567). **S2 keeps that method on the fixed 7-day rule** and pins it with a test (Task 3). Reasons: (1) at S2 ship time every S1 profile is Draft, so resolving targets would return the same 7 days anyway; (2) giving `KpiService` a `WorkflowProfileService` dependency changes its constructor, which breaks `new KpiService(db)` at seven test sites and `TestServiceFactory.Kpi`, for a dashboard "attention" list that only needs "stuck right now"; (3) S4 deletes every `KpiService` method except this one, so the per-workflow rule belongs in the new `AnalystStageTarget` helper, not in code that is about to be pruned. Upgrade path (not in this plan): add an optional `WorkflowProfileResolver? resolver` argument to `GetOverdueAnalystStageSamplesAsync` and have `DashboardService` pass `await profiles.LoadResolverAsync()`; `AnalystStageTarget.DaysFor` is the function to call.

---

### Task 1: Shared types, maths and scope helpers

**Files:**
- Create: `backend/MicroLIMS.Application/Services/Kpi/KpiTypes.cs`
- Create: `backend/MicroLIMS.Application/Services/Kpi/IKpi.cs`
- Create: `backend/MicroLIMS.Application/Services/Kpi/KpiScope.cs`
- Test: `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiMathTests.cs`
- Test: `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiPriorPeriodTests.cs`

**Interfaces:**
- Consumes: `SectionReviewQueues.OrderIn/SampleIn(IReadOnlyCollection<int>?)` (existing, `MicroLIMS.Application.Services`), `IMicroLimsDbContext` (`Users`, `Samples`, `TestOrders`, `SampleSectionSignoffs`, `ResultRecords`, `Incubations`), enums `SampleCategory`, `ApprovalDecision`, `WorkflowType`.
- Produces (namespace `MicroLIMS.Application.Services.Kpi`; exact contract names):
  - `KpiDirection`, `KpiGroup`, `KpiStatus`, `KpiQuery` (+`PriorPeriod()`), `KpiPrior`, `KpiSeriesPoint`, `KpiGap`, `KpiResult`, `KpiRecordDto`, `KpiRecordPageDto`, `KpiEntryDto`, `KpiDashboardDto`, `KpiDefinitionDto`, `KpiDefinitionsDto`, `ProfileInForceDto`, `KpiConstants`, `IKpi`.
  - `KpiCore` record: `(decimal? Value, decimal? Numerator, decimal? Denominator, int N, IReadOnlyList<KpiGap> Gaps, IReadOnlyList<KpiSeriesPoint> Series, IReadOnlyDictionary<string, decimal?> Extras, IReadOnlyList<string> Flags, bool IsCount = false)` with statics `NoGaps`, `NoSeries`, `NoFlags`, `NoExtras`, `Nothing(IReadOnlyList<KpiGap>? gaps = null, bool isCount = false)`.
  - `KpiScope` statics: `ResultIn`, `IncubationIn`, `SignoffIn`, `ParseCategory`, `FilterSamples`, `FilterOrders`, `ApprovedSamples`, `Days`, `Rate`, `Percentile`, `MonthlySeries`, `Gaps`, `ToResult`, `ComputeAsync`, `Page`, `UserNamesAsync`, `AnalystLabel`, and (Task 7) `DecidedSample`, `LoadDecidedSamplesAsync`, `DecisionRateAsync`.

- [ ] **Step 1: Check the preconditions**

Run from `E:\MicroLIMS\MicroLIMS`:
```
ls backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileResolver.cs backend/MicroLIMS.Tests/Fixtures/FixedTimeProvider.cs
git grep -n "LoadResolverAsync\|public static WorkflowProfileService WorkflowProfile" -- backend
```
Expected: all three files exist; `LoadResolverAsync` is declared in `WorkflowProfileService.cs` and `WorkflowProfile(` in `TestServiceFactory.cs`. If anything is missing, STOP: S1 is not merged.

Also confirm the exact names the tests below rely on (Grep tool, one call each): `DbSet<WorkflowProfileVersion> WorkflowProfileVersions` in `IMicroLimsDbContext.cs`; `DbSet<TestReturnEvent> TestReturnEvents`; the `ResultRecord` property names `NumericValue`, `Unit`, `ReferenceNumber`, `SourceTable`, `SourceId` in `backend/MicroLIMS.Domain/Entities/ResultRecord.cs`. The code below assumes `NumericValue` is `decimal?` and `Unit` is `string?`; if not, adapt the one projection in Task 9.

- [ ] **Step 2: Write the failing math and prior-period tests**

Create `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiMathTests.cs`:
```csharp
using MicroLIMS.Application.Services.Kpi;
using Xunit;

namespace MicroLIMS.Tests.UnitTests.Kpi;

public class KpiMathTests
{
    [Fact]
    public void Percentile_InterpolatesLinearly_LikeExcelPercentileInc()
    {
        var values = new[] { 11m, 2m, 5m, 4m };
        Assert.Equal(4.5m, KpiScope.Percentile(values, 0.5m));
        Assert.Equal(9.2m, KpiScope.Percentile(values, 0.9m));
    }

    [Fact]
    public void Percentile_SingleValue_IsThatValue_AndEmptyIsNull()
    {
        Assert.Equal(7m, KpiScope.Percentile(new[] { 7m }, 0.9m));
        Assert.Null(KpiScope.Percentile(Array.Empty<decimal>(), 0.5m));
    }

    [Fact]
    public void Rate_ZeroDenominator_IsNull_NotZero()
    {
        Assert.Null(KpiScope.Rate(0m, 0m));
        Assert.Equal(0.6667m, KpiScope.Rate(2m, 3m));
        Assert.Equal(0m, KpiScope.Rate(0m, 3m));
    }

    [Fact]
    public void Days_RoundsToTwoDecimals()
    {
        var from = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(0.5m, KpiScope.Days(from, from.AddHours(12)));
        Assert.Equal(1.04m, KpiScope.Days(from, from.AddHours(25)));
    }

    [Fact]
    public void MonthlySeries_GroupsByCalendarMonth_InTimeOrder()
    {
        var series = KpiScope.MonthlySeries(new (DateTime, decimal, decimal)[]
        {
            (new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc), 1m, 1m),
            (new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc), 0m, 1m),
            (new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc), 0m, 1m),
        });
        Assert.Equal(new[] { "2026-09", "2026-10" }, series.Select(p => p.Label));
        Assert.Equal(0m, series[0].Value);
        Assert.Equal(0.5m, series[1].Value);
        Assert.Equal(2, series[1].N);
    }

    [Fact]
    public void ToResult_StatusRules()
    {
        var gap = new KpiGap("X", "1 thing", 1);
        var noData = KpiScope.ToResult(KpiCore.Nothing(), null, "%");
        Assert.Equal(KpiStatus.NoData, noData.Status);
        Assert.Null(noData.Value);
        Assert.Null(noData.Prior);

        var partial = KpiScope.ToResult(new KpiCore(0.5m, 1, 2, 2, new[] { gap }, KpiCore.NoSeries, KpiCore.NoExtras, KpiCore.NoFlags), null, "%");
        Assert.Equal(KpiStatus.Partial, partial.Status);
        Assert.True(partial.TooFewRecords);

        var count = KpiScope.ToResult(KpiCore.Nothing(isCount: true), null, "tests");
        Assert.Equal(KpiStatus.Ok, count.Status);
        Assert.Equal(0m, count.Value);
        Assert.False(count.TooFewRecords);
    }
}
```

Create `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiPriorPeriodTests.cs`:
```csharp
using MicroLIMS.Application.Services.Kpi;
using Xunit;

namespace MicroLIMS.Tests.UnitTests.Kpi;

public class KpiPriorPeriodTests
{
    private static KpiQuery Q(DateTime from, DateTime to) =>
        new(from, to, null, null, null, null, null, to, 1, true);

    [Fact]
    public void PriorPeriod_IsEqualLengthWindowEndingAtFrom()
    {
        var q = Q(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
        var prior = q.PriorPeriod();
        Assert.Equal(new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc), prior.From);
        Assert.Equal(q.From, prior.To);
        Assert.Equal(q.To - q.From, prior.To - prior.From);
    }

    [Fact]
    public void PriorPeriod_OneDayPeriod_IsThePreviousDay()
    {
        var q = Q(new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc));
        var prior = q.PriorPeriod();
        Assert.Equal(new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc), prior.From);
        Assert.Equal(new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc), prior.To);
    }

    [Fact]
    public void PriorPeriod_KeepsEveryOtherField()
    {
        var q = new KpiQuery(DateTime.UtcNow.AddDays(-5), DateTime.UtcNow, new[] { 3 }, "Water", "P1", "TAMC", 9, DateTime.UtcNow, 4, false);
        var prior = q.PriorPeriod();
        Assert.Equal(q.SectionIds, prior.SectionIds);
        Assert.Equal((q.Category, q.Location, q.TestCode, q.AnalystId, q.Now, q.ViewerUserId, q.CanViewTeam),
            (prior.Category, prior.Location, prior.TestCode, prior.AnalystId, prior.Now, prior.ViewerUserId, prior.CanViewTeam));
    }
}
```

- [ ] **Step 3: Run, expect a compile failure**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~KpiMathTests|FullyQualifiedName~KpiPriorPeriodTests"
```
Expected: build error `The type or namespace name 'KpiScope' / 'KpiQuery' does not exist in the namespace 'MicroLIMS.Application.Services.Kpi'`.

- [ ] **Step 4: Implement the types**

Create `backend/MicroLIMS.Application/Services/Kpi/KpiTypes.cs`:
```csharp
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Kpi;

public enum KpiDirection { HigherIsBetter, LowerIsBetter, Neutral }
public enum KpiGroup { Headline, Flow, Quality, Micro, Workload }
public enum KpiStatus { Ok, NoData, Partial }

// From/To is the half-open UTC window [From, To). Now is read once by the
// service so every KPI in one response shares a single clock.
public sealed record KpiQuery(DateTime From, DateTime To, IReadOnlyList<int>? SectionIds, string? Category, string? Location,
    string? TestCode, int? AnalystId, DateTime Now, int ViewerUserId, bool CanViewTeam)
{
    // The equal-length window immediately before this one: [From-(To-From), From).
    public KpiQuery PriorPeriod()
    {
        var length = To - From;
        return this with { From = From - length, To = From };
    }
}

public sealed record KpiPrior(decimal? Value, int N, decimal? Numerator, decimal? Denominator);
public sealed record KpiSeriesPoint(string Label, string? Group, DateTime? At, decimal? Value, int N, decimal? Secondary = null);
public sealed record KpiGap(string Code, string Message, int Count);

public sealed record KpiResult(decimal? Value, decimal? Numerator, decimal? Denominator, int N, KpiPrior? Prior, KpiStatus Status,
    bool TooFewRecords, IReadOnlyList<KpiGap> Gaps, IReadOnlyList<KpiSeriesPoint> Series,
    IReadOnlyDictionary<string, decimal?> Extras, IReadOnlyList<string> Flags);

// Inclusion: "Numerator" | "Denominator" | "Excluded".
public sealed record KpiRecordDto(int? SampleId, int? TestOrderId, string? ControlNumber, string? TestCode, string? Category,
    string? AnalystName, DateTime? EventAt, decimal? Value, string? Unit, string Inclusion, string? Note);
public sealed record KpiRecordPageDto(string Key, int Page, int PageSize, int Total, IReadOnlyList<KpiRecordDto> Items);
public sealed record KpiEntryDto(string Key, string Name, string Unit, KpiDirection Direction, KpiGroup Group, int Order, KpiResult? Result, string? Error);
public sealed record KpiDashboardDto(DateTime From, DateTime To, DateTime PriorFrom, DateTime PriorTo, DateTime GeneratedAt, IReadOnlyList<KpiEntryDto> Kpis);
public sealed record KpiDefinitionDto(string Key, string Name, string Unit, KpiDirection Direction, KpiGroup Group, int Order, string Definition, string ClockEvents, string Population, string Source);
public sealed record ProfileInForceDto(WorkflowType WorkflowType, string? TestCode, decimal? WorkUnits, decimal? TargetAnalystDays, DateOnly EffectiveFrom, int VersionId);
public sealed record KpiDefinitionsDto(IReadOnlyList<KpiDefinitionDto> Kpis, IReadOnlyList<ProfileInForceDto> ProfilesInForce);

public static class KpiConstants
{
    public const int MinSampleForRate = 5;
    public const int DefaultTargetDays = 7;
    public const decimal EmAlertRateAdverse = 0.025m;
    public const decimal EmActionRateAdverse = 0.0015m;
    public const int IncubationGraceHours = 4;
}
```

Create `backend/MicroLIMS.Application/Services/Kpi/IKpi.cs`:
```csharp
namespace MicroLIMS.Application.Services.Kpi;

public interface IKpi
{
    string Key { get; }
    string Name { get; }
    string Unit { get; }   // "days", "%", "tests", "units"
    KpiDirection Direction { get; }
    KpiGroup Group { get; }
    int Order { get; }
    string Definition { get; }
    string ClockEvents { get; }
    string Population { get; }
    string Source { get; }
    Task<KpiResult> ComputeAsync(KpiQuery query, CancellationToken ct);
    Task<KpiRecordPageDto> GetRecordsAsync(KpiQuery query, int page, int pageSize, CancellationToken ct);
}
```

- [ ] **Step 5: Implement `KpiScope`**

Create `backend/MicroLIMS.Application/Services/Kpi/KpiScope.cs`:
```csharp
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Kpi;

// What one KPI computed for ONE window, before the prior period, status and
// "too few records" flag are added by KpiScope.ComputeAsync.
public sealed record KpiCore(decimal? Value, decimal? Numerator, decimal? Denominator, int N,
    IReadOnlyList<KpiGap> Gaps, IReadOnlyList<KpiSeriesPoint> Series,
    IReadOnlyDictionary<string, decimal?> Extras, IReadOnlyList<string> Flags, bool IsCount = false)
{
    public static readonly IReadOnlyList<KpiGap> NoGaps = Array.Empty<KpiGap>();
    public static readonly IReadOnlyList<KpiSeriesPoint> NoSeries = Array.Empty<KpiSeriesPoint>();
    public static readonly IReadOnlyList<string> NoFlags = Array.Empty<string>();
    public static readonly IReadOnlyDictionary<string, decimal?> NoExtras = new Dictionary<string, decimal?>();

    // Nothing to measure. A count KPI still reports 0; every other KPI reports NoData with a null value.
    public static KpiCore Nothing(IReadOnlyList<KpiGap>? gaps = null, bool isCount = false) =>
        new(isCount ? 0m : null, null, null, 0, gaps ?? NoGaps, NoSeries, NoExtras, NoFlags, isCount);
}

public static class KpiScope
{
    // ---- section filters (null = unrestricted; an empty list matches nothing) ----

    public static Expression<Func<ResultRecord, bool>> ResultIn(IReadOnlyCollection<int>? sectionIds) =>
        sectionIds is null ? r => true : r => sectionIds.Contains(r.TestOrder!.SectionId);

    // Media-challenge incubations have no TestOrder; they are never an analyst reading.
    public static Expression<Func<Incubation, bool>> IncubationIn(IReadOnlyCollection<int>? sectionIds) =>
        sectionIds is null ? i => i.TestOrderId != null : i => i.TestOrder != null && sectionIds.Contains(i.TestOrder.SectionId);

    public static Expression<Func<SampleSectionSignoff, bool>> SignoffIn(IReadOnlyCollection<int>? sectionIds) =>
        sectionIds is null ? x => true : x => sectionIds.Contains(x.SectionId);

    public static SampleCategory? ParseCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return null;
        if (Enum.TryParse<SampleCategory>(category.Trim(), true, out var parsed) && Enum.IsDefined(parsed)) return parsed;
        throw new InvalidOperationException($"Unknown sample category '{category}'.");
    }

    // Sample-level filters. Location keeps the legacy meaning (item name or water point code).
    public static IQueryable<Sample> FilterSamples(IQueryable<Sample> samples, KpiQuery q)
    {
        samples = samples.Where(SectionReviewQueues.SampleIn(q.SectionIds));
        var category = ParseCategory(q.Category);
        if (category.HasValue)
        {
            var c = category.Value;
            samples = samples.Where(s => s.Category == c);
        }
        var location = q.Location;
        if (!string.IsNullOrWhiteSpace(location))
            samples = samples.Where(s => s.Item!.Name == location || s.WaterSamplingPoint!.Code == location);
        var testCode = q.TestCode;
        if (!string.IsNullOrWhiteSpace(testCode))
            samples = samples.Where(s => s.TestOrders.Any(t => t.TestCode == testCode));
        if (q.AnalystId.HasValue)
        {
            var a = q.AnalystId.Value;
            samples = samples.Where(s => s.TestOrders.Any(t => t.AssignedAnalystId == a));
        }
        return samples;
    }

    public static IQueryable<TestOrder> FilterOrders(IQueryable<TestOrder> orders, KpiQuery q)
    {
        orders = orders.Where(SectionReviewQueues.OrderIn(q.SectionIds));
        var category = ParseCategory(q.Category);
        if (category.HasValue)
        {
            var c = category.Value;
            orders = orders.Where(t => t.Sample!.Category == c);
        }
        var location = q.Location;
        if (!string.IsNullOrWhiteSpace(location))
            orders = orders.Where(t => t.Sample!.Item!.Name == location || t.Sample!.WaterSamplingPoint!.Code == location);
        var testCode = q.TestCode;
        if (!string.IsNullOrWhiteSpace(testCode))
            orders = orders.Where(t => t.TestCode == testCode);
        if (q.AnalystId.HasValue)
        {
            var a = q.AnalystId.Value;
            orders = orders.Where(t => t.AssignedAnalystId == a);
        }
        return orders;
    }

    // Samples with an Approve decision whose ApprovedAt falls in the window.
    // Retest clones (OriginSampleId set) are excluded: their ReceivedAt is the
    // spin-off time, so their "turnaround" is not the lab's turnaround.
    // A Reject also stamps ApprovedAt, which is why ApprovalDecision is checked.
    public static IQueryable<Sample> ApprovedSamples(IMicroLimsDbContext db, KpiQuery q)
    {
        var from = q.From;
        var to = q.To;
        return FilterSamples(db.Samples.AsNoTracking()
            .Where(s => s.ApprovalDecision == ApprovalDecision.Approve
                && s.ApprovedAt != null && s.ApprovedAt >= from && s.ApprovedAt < to
                && s.OriginSampleId == null), q);
    }

    // ---- maths ----

    public static decimal Days(DateTime from, DateTime to) => Math.Round((decimal)(to - from).TotalDays, 2);

    public static decimal? Rate(decimal numerator, decimal denominator) =>
        denominator == 0 ? null : Math.Round(numerator / denominator, 4);

    // Linear interpolation between closest ranks (Excel PERCENTILE.INC).
    public static decimal? Percentile(IReadOnlyCollection<decimal> values, decimal p)
    {
        if (values.Count == 0) return null;
        var sorted = values.OrderBy(v => v).ToArray();
        var rank = p * (sorted.Length - 1);
        var lo = (int)Math.Floor(rank);
        var hi = Math.Min(lo + 1, sorted.Length - 1);
        return Math.Round(sorted[lo] + (sorted[hi] - sorted[lo]) * (rank - lo), 2);
    }

    public static IReadOnlyList<KpiSeriesPoint> MonthlySeries(IEnumerable<(DateTime At, decimal Num, decimal Den)> items) =>
        items.GroupBy(i => new DateTime(i.At.Year, i.At.Month, 1, 0, 0, 0, DateTimeKind.Utc))
            .OrderBy(g => g.Key)
            .Select(g => new KpiSeriesPoint(g.Key.ToString("yyyy-MM"), null, g.Key, Rate(g.Sum(x => x.Num), g.Sum(x => x.Den)), g.Count()))
            .ToList();

    public static IReadOnlyList<KpiGap> Gaps(params (string Code, string Message, int Count)[] gaps) =>
        gaps.Where(g => g.Count > 0).Select(g => new KpiGap(g.Code, g.Message, g.Count)).ToList();

    // ---- result assembly ----

    public static KpiResult ToResult(KpiCore cur, KpiCore? prior, string unit)
    {
        var status = cur.N == 0 && !cur.IsCount ? KpiStatus.NoData
            : cur.Gaps.Count > 0 ? KpiStatus.Partial
            : KpiStatus.Ok;
        // The spec shows a prior only when it has records of its own.
        var priorDto = prior is { N: > 0 } ? new KpiPrior(prior.Value, prior.N, prior.Numerator, prior.Denominator) : null;
        return new KpiResult(cur.Value, cur.Numerator, cur.Denominator, cur.N, priorDto, status,
            unit == "%" && cur.N < KpiConstants.MinSampleForRate, cur.Gaps, cur.Series, cur.Extras, cur.Flags);
    }

    public static async Task<KpiResult> ComputeAsync(KpiQuery q, string unit,
        Func<KpiQuery, CancellationToken, Task<KpiCore>> core, CancellationToken ct, bool withPrior = true)
    {
        var current = await core(q, ct);
        // ponytail: the prior pass also builds its record list; fine at lab scale, split Core from Records if a KPI gets slow.
        KpiCore? prior = withPrior ? await core(q.PriorPeriod(), ct) : null;
        return ToResult(current, prior, unit);
    }

    public static KpiRecordPageDto Page(string key, IReadOnlyList<KpiRecordDto> all, int page, int pageSize)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);
        return new KpiRecordPageDto(key, page, pageSize, all.Count, all.Skip((page - 1) * pageSize).Take(pageSize).ToList());
    }

    // ---- names ----

    public static async Task<Dictionary<int, string>> UserNamesAsync(IMicroLimsDbContext db, IEnumerable<int?> ids, CancellationToken ct)
    {
        var set = ids.Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToList();
        if (set.Count == 0) return new Dictionary<int, string>();
        return await db.Users.AsNoTracking().Where(u => set.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    // D8: another analyst's name is only shown to someone who may see the team.
    public static string? AnalystLabel(KpiQuery q, int? analystId, IReadOnlyDictionary<int, string> names) =>
        analystId is { } id && (q.CanViewTeam || id == q.ViewerUserId) && names.TryGetValue(id, out var name) ? name : null;
}
```

- [ ] **Step 6: Run, expect pass**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~KpiMathTests|FullyQualifiedName~KpiPriorPeriodTests"
```
Expected: all PASS. (If `SectionReviewQueues` is not found, add `using MicroLIMS.Application.Services;` - it is in the parent namespace, so it resolves without a using.)

- [ ] **Step 7: Commit**

```
git add backend/MicroLIMS.Application/Services/Kpi/KpiTypes.cs backend/MicroLIMS.Application/Services/Kpi/IKpi.cs backend/MicroLIMS.Application/Services/Kpi/KpiScope.cs backend/MicroLIMS.Tests/UnitTests/Kpi/KpiMathTests.cs backend/MicroLIMS.Tests/UnitTests/Kpi/KpiPriorPeriodTests.cs
git commit -m "feat(kpi): KPI types, IKpi, scope helpers and maths

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Registry and dashboard service

**Files:**
- Create: `backend/MicroLIMS.Application/Services/Kpi/KpiRegistry.cs`
- Create: `backend/MicroLIMS.Application/Services/Kpi/KpiDashboardService.cs`
- Create: `backend/MicroLIMS.Tests/Fixtures/StubSectionScope.cs`
- Test: `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiScopeAuthorizationTests.cs`

**Interfaces:**
- Consumes: Task 1 types; `IUserSectionScopeService.GetAccessibleSectionIdsAsync(int, CancellationToken)` -> `IReadOnlyList<int>?` (`MicroLIMS.Application.Interfaces`); `NotFoundException(string)` (`MicroLIMS.Shared.Exceptions`); `IMicroLimsDbContext.WorkflowProfileVersions`; `WorkflowProfileStatus` (S1).
- Produces:
  - `KpiRegistry(IEnumerable<IKpi> all)`: `All` (ordered by `Order` then `Key`), `Find(string key)` (case-insensitive); throws `InvalidOperationException` on a duplicate key.
  - `KpiDashboardService(IMicroLimsDbContext db, KpiRegistry registry, IUserSectionScopeService scope, TimeProvider? timeProvider = null, ILogger<KpiDashboardService>? logger = null)` with `GetDashboardAsync`, `GetRecordsAsync`, `GetDefinitionsAsync` exactly as contract 3.2.
  - Test stub `StubSectionScope(Func<int, IReadOnlyList<int>?>)`.

- [ ] **Step 1: Write the failing tests**

Create `backend/MicroLIMS.Tests/Fixtures/StubSectionScope.cs`:
```csharp
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;

namespace MicroLIMS.Tests.Fixtures;

// Only GetAccessibleSectionIdsAsync matters to the KPI service; the rest of
// the interface is guard calls the KPI code never makes.
internal sealed class StubSectionScope : IUserSectionScopeService
{
    private readonly Func<int, IReadOnlyList<int>?> _ids;
    public StubSectionScope(Func<int, IReadOnlyList<int>?> ids) => _ids = ids;

    public Task<IReadOnlyList<int>?> GetAccessibleSectionIdsAsync(int userId, CancellationToken ct = default) => Task.FromResult(_ids(userId));
    public Task<IReadOnlyList<WorkspaceArea>> GetPhyschemAreasAsync(int userId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<int> ResolveSectionForCreateAsync(int userId, int? requestedSectionId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task EnsureTestOrderAccessAsync(int userId, int testOrderId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task EnsureTestOrdersAccessAsync(int userId, IEnumerable<int> testOrderIds, CancellationToken ct = default) => throw new NotSupportedException();
    public Task EnsureSampleAccessAsync(int userId, int sampleId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task EnsureIncubationAccessAsync(int userId, int incubationId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task EnsurePathogenSampleAccessAsync(int userId, int sampleId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task EnsureMaterialAccessAsync(int userId, int materialId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task EnsureMediaAccessAsync(int userId, int mediaId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task EnsureCryovialAccessAsync(int userId, int cryovialId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task EnsureMediaEvaluationAccessAsync(int userId, int evaluationId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task EnsureMediaEvaluationChallengeAccessAsync(int userId, int challengeId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task EnsureEquipmentAccessAsync(int userId, int equipmentId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task EnsureEquipmentInventoryAccessAsync(int userId, int inventoryId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task EnsureColumnAccessAsync(int userId, int columnId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task EnsureOosGroupAccessAsync(int userId, string oosGroupCode, CancellationToken ct = default) => throw new NotSupportedException();
}
```
(If the interface has gained or lost members since this plan was written, the compiler lists them; adjust the stub, nothing else.)

Create `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiScopeAuthorizationTests.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.UnitTests.Kpi;

public class KpiScopeAuthorizationTests
{
    private static readonly DateOnly From = new(2026, 10, 1);
    private static readonly DateOnly To = new(2026, 10, 31);
    private static readonly FixedTimeProvider Clock = new(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));

    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    // Records every query it is asked to compute.
    private sealed class FakeKpi : IKpi
    {
        public List<KpiQuery> Seen { get; } = new();
        public Func<KpiQuery, KpiResult>? Behaviour { get; init; }
        public Func<KpiQuery, int, int, KpiRecordPageDto>? Records { get; init; }
        public FakeKpi(string key, int order, KpiGroup group = KpiGroup.Headline) { Key = key; Order = order; Group = group; }
        public string Key { get; }
        public string Name => Key;
        public string Unit => "%";
        public KpiDirection Direction => KpiDirection.Neutral;
        public KpiGroup Group { get; }
        public int Order { get; }
        public string Definition => "def " + Key;
        public string ClockEvents => "clock";
        public string Population => "pop";
        public string Source => "src";
        public Task<KpiResult> ComputeAsync(KpiQuery q, CancellationToken ct)
        {
            Seen.Add(q);
            return Task.FromResult(Behaviour?.Invoke(q) ?? Ok());
        }
        public Task<KpiRecordPageDto> GetRecordsAsync(KpiQuery q, int page, int pageSize, CancellationToken ct)
        {
            Seen.Add(q);
            return Task.FromResult(Records?.Invoke(q, page, pageSize) ?? new KpiRecordPageDto(Key, page, pageSize, 0, Array.Empty<KpiRecordDto>()));
        }
        public static KpiResult Ok() => KpiScope.ToResult(new KpiCore(0.5m, 1, 2, 6, KpiCore.NoGaps, KpiCore.NoSeries, KpiCore.NoExtras, KpiCore.NoFlags), null, "%");
    }

    private static KpiDashboardService Service(MicroLimsDbContext db, Func<int, IReadOnlyList<int>?> scope, params IKpi[] kpis) =>
        new(db, new KpiRegistry(kpis), new StubSectionScope(scope), Clock);

    [Fact]
    public async Task Dashboard_ReturnsEveryRegisteredKpi_OrderedByOrder_WithHalfOpenWindow()
    {
        await using var db = NewDb();
        var b = new FakeKpi("b", 2);
        var a = new FakeKpi("a", 1);
        var dto = await Service(db, _ => null, b, a).GetDashboardAsync(1, true, From, To, null, null, null, null);

        Assert.Equal(new[] { "a", "b" }, dto.Kpis.Select(k => k.Key));
        Assert.Equal(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), dto.From);
        Assert.Equal(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), dto.To);   // exclusive: to + 1 day
        Assert.Equal(new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc), dto.PriorFrom);
        Assert.Equal(dto.From, dto.PriorTo);
        Assert.Equal(new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc), dto.GeneratedAt);
        Assert.Equal(dto.GeneratedAt, a.Seen.Single().Now);
    }

    [Fact]
    public async Task FailingKpi_BecomesErrorEntry_AndOthersStillCompute()
    {
        await using var db = NewDb();
        var bad = new FakeKpi("bad", 1) { Behaviour = _ => throw new InvalidOperationException("Npgsql: relation \"Secret\" does not exist") };
        var good = new FakeKpi("good", 2);
        var dto = await Service(db, _ => null, bad, good).GetDashboardAsync(1, true, From, To, null, null, null, null);

        var badEntry = dto.Kpis.Single(k => k.Key == "bad");
        Assert.Null(badEntry.Result);
        Assert.False(string.IsNullOrWhiteSpace(badEntry.Error));
        Assert.DoesNotContain("Secret", badEntry.Error);     // never leak the exception text
        Assert.NotNull(dto.Kpis.Single(k => k.Key == "good").Result);
    }

    [Fact]
    public async Task NonTeamViewer_AskingForAnotherAnalyst_Throws()
    {
        await using var db = NewDb();
        var svc = Service(db, _ => null, new FakeKpi("a", 1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.GetDashboardAsync(5, false, From, To, null, null, null, 9));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.GetRecordsAsync("a", 5, false, From, To, null, null, null, 9, 1, 50));
    }

    [Fact]
    public async Task NonTeamViewer_OwnAnalystId_IsPassedThrough()
    {
        await using var db = NewDb();
        var kpi = new FakeKpi("a", 1);
        await Service(db, _ => null, kpi).GetDashboardAsync(5, false, From, To, null, null, null, 5);
        Assert.Equal(5, kpi.Seen.Single().AnalystId);
    }

    [Fact]
    public async Task NonTeamViewer_NoAnalystId_GetsLabLevelFigures()
    {
        await using var db = NewDb();
        var kpi = new FakeKpi("a", 1);
        await Service(db, _ => null, kpi).GetDashboardAsync(5, false, From, To, null, null, null, null);
        var q = kpi.Seen.Single();
        Assert.Null(q.AnalystId);
        Assert.False(q.CanViewTeam);
        Assert.Equal(5, q.ViewerUserId);
    }

    [Fact]
    public async Task TeamViewer_MayAskForAnyAnalyst()
    {
        await using var db = NewDb();
        var kpi = new FakeKpi("a", 1);
        await Service(db, _ => null, kpi).GetDashboardAsync(5, true, From, To, null, null, null, 9);
        Assert.Equal(9, kpi.Seen.Single().AnalystId);
    }

    [Fact]
    public async Task Scope_NullStaysNull_EmptyStaysEmpty()
    {
        await using var db = NewDb();
        var kpi = new FakeKpi("a", 1);
        var svc = Service(db, id => id == 1 ? null : id == 2 ? Array.Empty<int>() : new[] { 4, 7 }, kpi);

        await svc.GetDashboardAsync(1, true, From, To, null, null, null, null);
        await svc.GetDashboardAsync(2, true, From, To, null, null, null, null);
        await svc.GetDashboardAsync(3, true, From, To, null, null, null, null);

        Assert.Null(kpi.Seen[0].SectionIds);                     // administrator: unrestricted
        Assert.NotNull(kpi.Seen[1].SectionIds);                  // no sections: must not collapse to "no filter"
        Assert.Empty(kpi.Seen[1].SectionIds!);
        Assert.Equal(new[] { 4, 7 }, kpi.Seen[2].SectionIds);
    }

    [Theory]
    [InlineData("2026-10-31", "2026-10-01")]
    public async Task Dates_FromAfterTo_IsRejected(string from, string to)
    {
        await using var db = NewDb();
        var svc = Service(db, _ => null, new FakeKpi("a", 1));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.GetDashboardAsync(1, true, DateOnly.Parse(from), DateOnly.Parse(to), null, null, null, null));
        Assert.Contains("start date", ex.Message);
    }

    [Fact]
    public async Task Dates_Missing_AreRejected()
    {
        await using var db = NewDb();
        var svc = Service(db, _ => null, new FakeKpi("a", 1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GetDashboardAsync(1, true, default, default, null, null, null, null));
    }

    [Fact]
    public async Task UnknownCategory_IsRejected_AndKnownCategoryIsPassedThrough()
    {
        await using var db = NewDb();
        var kpi = new FakeKpi("a", 1);
        var svc = Service(db, _ => null, kpi);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GetDashboardAsync(1, true, From, To, "Nonsense", null, null, null));
        await svc.GetDashboardAsync(1, true, From, To, "water", null, null, null);
        Assert.Equal("water", kpi.Seen.Single().Category);   // KPIs parse it with ParseCategory (case-insensitive)
    }

    [Fact]
    public async Task Records_PageStartsAtOne_AndSizeIsCappedAt200()
    {
        await using var db = NewDb();
        var kpi = new FakeKpi("a", 1);
        var svc = Service(db, _ => null, kpi);
        var page = await svc.GetRecordsAsync("a", 1, true, From, To, null, null, null, null, 0, 5000);
        Assert.Equal(1, page.Page);
        Assert.Equal(200, page.PageSize);
        var def = await svc.GetRecordsAsync("a", 1, true, From, To, null, null, null, null, 2, 0);
        Assert.Equal(50, def.PageSize);
    }

    [Fact]
    public async Task Records_UnknownKey_IsNotFound()
    {
        await using var db = NewDb();
        var svc = Service(db, _ => null, new FakeKpi("a", 1));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.GetRecordsAsync("nope", 1, true, From, To, null, null, null, null, 1, 50));
    }

    [Fact]
    public async Task Records_HideOtherAnalystNames_WithoutViewTeam()
    {
        // The KPI decides the label with KpiScope.AnalystLabel; the service must hand it CanViewTeam = false.
        await using var db = NewDb();
        var kpi = new FakeKpi("a", 1);
        await Service(db, _ => null, kpi).GetRecordsAsync("a", 5, false, From, To, null, null, null, null, 1, 50);
        var q = kpi.Seen.Single();
        Assert.False(q.CanViewTeam);
        var names = new Dictionary<int, string> { [5] = "Me", [9] = "Other" };
        Assert.Equal("Me", KpiScope.AnalystLabel(q, 5, names));
        Assert.Null(KpiScope.AnalystLabel(q, 9, names));
        Assert.Equal("Other", KpiScope.AnalystLabel(q with { CanViewTeam = true }, 9, names));
    }

    [Fact]
    public async Task Definitions_ProfilesInForce_AreResolvedAtTo()
    {
        await using var db = NewDb();
        db.WorkflowProfileVersions.AddRange(
            Version(1, WorkflowType.CountTest, null, 2m, 5m, new DateOnly(2026, 1, 1)),
            Version(2, WorkflowType.CountTest, null, 3m, null, new DateOnly(2026, 10, 10)),   // units only: target stays 5
            Version(3, WorkflowType.CountTest, null, 9m, 9m, new DateOnly(2026, 12, 1)),      // after "to": ignored
            Version(4, WorkflowType.CountTest, "TAMC", null, 14m, new DateOnly(2026, 2, 1)),
            Version(5, WorkflowType.Titration, null, 1m, null, new DateOnly(2026, 2, 1), WorkflowProfileStatus.Draft));
        await db.SaveChangesAsync();

        var defs = await Service(db, _ => null, new FakeKpi("a", 1)).GetDefinitionsAsync(From, To);

        Assert.Equal("a", defs.Kpis.Single().Key);
        Assert.Equal("def a", defs.Kpis.Single().Definition);
        Assert.Equal(2, defs.ProfilesInForce.Count);                // Draft and future rows are not in force
        var typeDefault = defs.ProfilesInForce.Single(p => p.TestCode == null);
        Assert.Equal(3m, typeDefault.WorkUnits);
        Assert.Equal(5m, typeDefault.TargetAnalystDays);
        Assert.Equal(2, typeDefault.VersionId);
        Assert.Equal(new DateOnly(2026, 10, 10), typeDefault.EffectiveFrom);
        var over = defs.ProfilesInForce.Single(p => p.TestCode == "TAMC");
        Assert.Null(over.WorkUnits);
        Assert.Equal(14m, over.TargetAnalystDays);
    }

    private static WorkflowProfileVersion Version(int id, WorkflowType type, string? code, decimal? units, decimal? target,
        DateOnly from, WorkflowProfileStatus status = WorkflowProfileStatus.Effective) => new()
    {
        Id = id, WorkflowType = type, TestCode = code, WorkUnits = units, TargetAnalystDays = target,
        EffectiveFrom = from, Status = status, ReasonForChange = "seed", CreatedByUserId = 1
    };
}
```
Note for the executor: `FakeKpi.Behaviour` uses `throw` in a lambda expression body; that is valid C# (`_ => throw ...`).

- [ ] **Step 2: Run, expect a compile failure**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~KpiScopeAuthorizationTests"
```
Expected: build error `'KpiRegistry' / 'KpiDashboardService' could not be found`.

- [ ] **Step 3: Implement the registry**

Create `backend/MicroLIMS.Application/Services/Kpi/KpiRegistry.cs`:
```csharp
namespace MicroLIMS.Application.Services.Kpi;

public class KpiRegistry
{
    public KpiRegistry(IEnumerable<IKpi> all)
    {
        var list = all.OrderBy(k => k.Order).ThenBy(k => k.Key, StringComparer.Ordinal).ToList();
        var duplicate = list.GroupBy(k => k.Key, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"KPI key '{duplicate.Key}' is registered more than once.");
        All = list;
    }

    public IReadOnlyList<IKpi> All { get; }

    public IKpi? Find(string key) =>
        All.FirstOrDefault(k => string.Equals(k.Key, key, StringComparison.OrdinalIgnoreCase));
}
```

- [ ] **Step 4: Implement the dashboard service**

Create `backend/MicroLIMS.Application/Services/Kpi/KpiDashboardService.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.Kpi;

public class KpiDashboardService
{
    private const string FailedMessage = "This figure could not be calculated. The error has been logged.";

    private readonly IMicroLimsDbContext _db;
    private readonly KpiRegistry _registry;
    private readonly IUserSectionScopeService _scope;
    private readonly TimeProvider _time;
    private readonly ILogger<KpiDashboardService> _logger;

    public KpiDashboardService(IMicroLimsDbContext db, KpiRegistry registry, IUserSectionScopeService scope,
        TimeProvider? timeProvider = null, ILogger<KpiDashboardService>? logger = null)
    {
        _db = db;
        _registry = registry;
        _scope = scope;
        _time = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<KpiDashboardService>.Instance;
    }

    public async Task<KpiDashboardDto> GetDashboardAsync(int userId, bool canViewTeam, DateOnly from, DateOnly to,
        string? category, string? location, string? testCode, int? analystId, CancellationToken ct = default)
    {
        var q = await BuildQueryAsync(userId, canViewTeam, from, to, category, location, testCode, analystId, ct);
        var entries = new List<KpiEntryDto>();
        // Sequential on purpose: one DbContext, and one failing KPI must not take the page down.
        foreach (var kpi in _registry.All)
        {
            try
            {
                var result = await kpi.ComputeAsync(q, ct);
                entries.Add(new KpiEntryDto(kpi.Key, kpi.Name, kpi.Unit, kpi.Direction, kpi.Group, kpi.Order, result, null));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "KPI {Key} failed for window {From:o} - {To:o}", kpi.Key, q.From, q.To);
                entries.Add(new KpiEntryDto(kpi.Key, kpi.Name, kpi.Unit, kpi.Direction, kpi.Group, kpi.Order, null, FailedMessage));
            }
        }

        var prior = q.PriorPeriod();
        return new KpiDashboardDto(q.From, q.To, prior.From, prior.To, q.Now, entries);
    }

    public async Task<KpiRecordPageDto> GetRecordsAsync(string key, int userId, bool canViewTeam, DateOnly from, DateOnly to,
        string? category, string? location, string? testCode, int? analystId, int page, int pageSize, CancellationToken ct = default)
    {
        var kpi = _registry.Find(key) ?? throw new NotFoundException($"Unknown KPI '{key}'.");
        var q = await BuildQueryAsync(userId, canViewTeam, from, to, category, location, testCode, analystId, ct);
        return await kpi.GetRecordsAsync(q, Math.Max(page, 1), pageSize <= 0 ? 50 : Math.Min(pageSize, 200), ct);
    }

    public async Task<KpiDefinitionsDto> GetDefinitionsAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        ValidateDates(from, to);
        var kpis = _registry.All
            .Select(k => new KpiDefinitionDto(k.Key, k.Name, k.Unit, k.Direction, k.Group, k.Order, k.Definition, k.ClockEvents, k.Population, k.Source))
            .ToList();

        var versions = await _db.WorkflowProfileVersions.AsNoTracking()
            .Where(v => v.Status == WorkflowProfileStatus.Effective && v.EffectiveFrom <= to)
            .ToListAsync(ct);

        // Each field is resolved on its own (units and target can come from different rows of one scope).
        var inForce = versions.GroupBy(v => (v.WorkflowType, v.TestCode)).Select(g =>
        {
            var ordered = g.OrderByDescending(v => v.EffectiveFrom).ThenByDescending(v => v.Id).ToList();
            var units = ordered.FirstOrDefault(v => v.WorkUnits.HasValue);
            var target = ordered.FirstOrDefault(v => v.TargetAnalystDays.HasValue);
            var latest = new[] { units, target }.Where(v => v is not null).OrderByDescending(v => v!.EffectiveFrom).ThenByDescending(v => v!.Id).First()!;
            return new ProfileInForceDto(g.Key.WorkflowType, g.Key.TestCode, units?.WorkUnits, target?.TargetAnalystDays, latest.EffectiveFrom, latest.Id);
        })
        .OrderBy(p => p.WorkflowType).ThenBy(p => p.TestCode, StringComparer.Ordinal)
        .ToList();

        return new KpiDefinitionsDto(kpis, inForce);
    }

    private static void ValidateDates(DateOnly from, DateOnly to)
    {
        if (from == default || to == default)
            throw new InvalidOperationException("A start date and an end date are required.");
        if (from > to)
            throw new InvalidOperationException("The start date must not be after the end date.");
    }

    private async Task<KpiQuery> BuildQueryAsync(int userId, bool canViewTeam, DateOnly from, DateOnly to,
        string? category, string? location, string? testCode, int? analystId, CancellationToken ct)
    {
        ValidateDates(from, to);
        // D8: without Kpi.ViewTeam a caller may only ask about themselves.
        if (!canViewTeam && analystId.HasValue && analystId.Value != userId)
            throw new UnauthorizedAccessException("You can only view your own analyst figures.");
        KpiScope.ParseCategory(category);   // reject an unknown category up front, as a 400

        var sections = await _scope.GetAccessibleSectionIdsAsync(userId, ct);   // null = unrestricted, empty = nothing
        var now = _time.GetUtcNow().UtcDateTime;
        return new KpiQuery(
            from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            sections, Blank(category), Blank(location), Blank(testCode), analystId, now, userId, canViewTeam);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
```

- [ ] **Step 5: Run, expect pass**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~KpiScopeAuthorizationTests"
```
Expected: all PASS. If `WorkflowProfileStatus` is not found, check its namespace (`MicroLIMS.Domain.Enums`) in S1; the service already imports it.

- [ ] **Step 6: Commit**

```
git add backend/MicroLIMS.Application/Services/Kpi/KpiRegistry.cs backend/MicroLIMS.Application/Services/Kpi/KpiDashboardService.cs backend/MicroLIMS.Tests/Fixtures/StubSectionScope.cs backend/MicroLIMS.Tests/UnitTests/Kpi/KpiScopeAuthorizationTests.cs
git commit -m "feat(kpi): KPI registry and dashboard service (scope, D8, isolation)

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Per-workflow analyst-stage target

**Files:**
- Create: `backend/MicroLIMS.Application/Services/Kpi/AnalystStageTarget.cs`
- Test: `backend/MicroLIMS.Tests/UnitTests/Kpi/AnalystStageTargetTests.cs`
- Modify (test only): `backend/MicroLIMS.Tests/WorkflowTests/KpiFilteringTests.cs` (append one test inside the existing class, before its closing brace)

**Interfaces:**
- Consumes: `WorkflowProfileResolver.Resolve(string testCode, DateTime at)` -> `ResolvedProfile` (`TargetAnalystDays`, `IsTargetAware`); the resolver is built in tests with `new WorkflowProfileResolver(IEnumerable<WorkflowProfileVersion>, IReadOnlyDictionary<string, WorkflowType>)` (S1 contract 2.3); `KpiConstants.DefaultTargetDays`.
- Produces:
  - `public sealed record AnalystStageTargetResult(decimal Days, int TestsOnDefault, int TestsTotal)`
  - `public static class AnalystStageTarget { public static AnalystStageTargetResult DaysFor(IEnumerable<string> testCodes, DateTime at, WorkflowProfileResolver resolver); }`

Rule (spec 4.3): the analyst-stage deadline is `AssignedAt + max(TargetAnalystDays of the sample's assigned tests)`. A test whose target does not resolve uses `KpiConstants.DefaultTargetDays` and is counted in `TestsOnDefault`. No tests at all -> 7 days, `TestsTotal = 0`.

- [ ] **Step 1: Write the failing tests**

Create `backend/MicroLIMS.Tests/UnitTests/Kpi/AnalystStageTargetTests.cs`:
```csharp
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests.Kpi;

public class AnalystStageTargetTests
{
    private static readonly DateTime At = new(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);

    private static WorkflowProfileVersion Row(int id, WorkflowType type, string? code, decimal? target) => new()
    {
        Id = id, WorkflowType = type, TestCode = code, WorkUnits = 1m, TargetAnalystDays = target,
        EffectiveFrom = new DateOnly(2026, 1, 1), Status = WorkflowProfileStatus.Effective, ReasonForChange = "seed", CreatedByUserId = 1
    };

    private static WorkflowProfileResolver Resolver(params WorkflowProfileVersion[] rows) =>
        new(rows, new Dictionary<string, WorkflowType>
        {
            ["TAMC"] = WorkflowType.CountTest, ["TYMC"] = WorkflowType.CountTest, ["HPLC1"] = WorkflowType.HplcMethodAssay
        });

    [Fact]
    public void DaysFor_TakesMaxOfResolvedTargets()
    {
        var resolver = Resolver(Row(1, WorkflowType.CountTest, null, 5m), Row(2, WorkflowType.CountTest, "TYMC", 14m));
        var r = AnalystStageTarget.DaysFor(new[] { "TAMC", "TYMC" }, At, resolver);
        Assert.Equal((14m, 0, 2), (r.Days, r.TestsOnDefault, r.TestsTotal));
    }

    [Fact]
    public void DaysFor_UnresolvedTestsFallBackToSevenDays_AndAreCounted()
    {
        var resolver = Resolver(Row(1, WorkflowType.CountTest, null, 5m));
        var r = AnalystStageTarget.DaysFor(new[] { "TAMC", "HPLC1" }, At, resolver);
        // HPLC1 has no profile: 7 days, which beats TAMC's 5.
        Assert.Equal((7m, 1, 2), (r.Days, r.TestsOnDefault, r.TestsTotal));
    }

    [Fact]
    public void DaysFor_ResolvedTargetBelowSeven_IsNotRaisedToTheFallback()
    {
        var resolver = Resolver(Row(1, WorkflowType.CountTest, null, 3m));
        var r = AnalystStageTarget.DaysFor(new[] { "TAMC" }, At, resolver);
        Assert.Equal((3m, 0, 1), (r.Days, r.TestsOnDefault, r.TestsTotal));
    }

    [Fact]
    public void DaysFor_NoTests_IsSevenDays()
    {
        var r = AnalystStageTarget.DaysFor(Array.Empty<string>(), At, Resolver());
        Assert.Equal((7m, 0, 0), (r.Days, r.TestsOnDefault, r.TestsTotal));
    }

    [Fact]
    public void DaysFor_UsesTheVersionInForceAtTheGivenTime()
    {
        var later = Row(2, WorkflowType.CountTest, null, 20m);
        later.EffectiveFrom = new DateOnly(2026, 10, 20);
        var resolver = Resolver(Row(1, WorkflowType.CountTest, null, 5m), later);
        Assert.Equal(5m, AnalystStageTarget.DaysFor(new[] { "TAMC" }, At, resolver).Days);
        Assert.Equal(20m, AnalystStageTarget.DaysFor(new[] { "TAMC" }, At.AddDays(11), resolver).Days);
    }
}
```

- [ ] **Step 2: Run, expect a compile failure**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~AnalystStageTargetTests"
```
Expected: `'AnalystStageTarget' does not exist`.

- [ ] **Step 3: Implement**

Create `backend/MicroLIMS.Application/Services/Kpi/AnalystStageTarget.cs`:
```csharp
namespace MicroLIMS.Application.Services.Kpi;

public sealed record AnalystStageTargetResult(decimal Days, int TestsOnDefault, int TestsTotal);

// Spec 4.3: the analyst-stage deadline is AssignedAt + the LONGEST target of
// the sample's assigned tests. A test with no resolvable target falls back to
// the old 7-day constant and is counted so the tile can show a gap chip.
// `at` is the approval time (D4): a profile added later never moves it.
public static class AnalystStageTarget
{
    public static AnalystStageTargetResult DaysFor(IEnumerable<string> testCodes, DateTime at, WorkflowProfileResolver resolver)
    {
        decimal max = 0m;
        int onDefault = 0, total = 0;
        foreach (var code in testCodes)
        {
            total++;
            var target = resolver.Resolve(code, at).TargetAnalystDays;
            if (target is null) onDefault++;
            max = Math.Max(max, target ?? KpiConstants.DefaultTargetDays);
        }
        return new AnalystStageTargetResult(total == 0 ? KpiConstants.DefaultTargetDays : max, onDefault, total);
    }
}
```

- [ ] **Step 4: Run, expect pass**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~AnalystStageTargetTests"
```
Expected: all PASS.

- [ ] **Step 5: Pin the `DashboardService` decision (7-day rule stays)**

Open `backend/MicroLIMS.Tests/WorkflowTests/KpiFilteringTests.cs`, read the existing `GetOverdueAnalystStageSamplesAsync` test (search for it; it seeds `Role`, `User`, `Sample`, `TestOrder`, `SamplePreparation` and calls `new KpiService(db)`), and append this test next to it, reusing the file's own `NewDb()` and its usings (add `using MicroLIMS.Domain.Entities;`/`Enums` only if missing):
```csharp
    // S2 decision: DashboardService's overdue list keeps the fixed 7-day rule.
    // A 14-day profile target (used by the new KPI tiles) must NOT stretch it.
    [Fact]
    public async Task GetOverdueAnalystStageSamplesAsync_StaysOnSevenDayRule_EvenWithALongerProfileTarget()
    {
        await using var db = NewDb();
        var now = DateTime.UtcNow;
        db.Roles.Add(new Role { Id = 1, Type = RoleType.Analyst, Name = "Analyst" });
        db.Users.Add(new User { Id = 1, FullName = "A", Username = "a", RoleId = 1, PasswordHash = "x" });
        var sample = new Sample { Id = 1, Category = SampleCategory.FinishedProduct, ControlNumber = "FP-900", Status = SampleStatus.InTesting };
        sample.TestOrders.Add(new TestOrder { Id = 1, TestCode = "TAMC", AssignedAnalystId = 1, Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Running });
        db.Samples.Add(sample);
        db.SamplePreparations.Add(new SamplePreparation { SampleId = 1, PreparedAt = now.AddDays(-8), PreparedByUserId = 1 });
        db.WorkflowProfileVersions.Add(new WorkflowProfileVersion
        {
            WorkflowType = WorkflowType.CountTest, TestCode = null, TargetAnalystDays = 14m, WorkUnits = 1m,
            EffectiveFrom = new DateOnly(2026, 1, 1), Status = WorkflowProfileStatus.Effective, ReasonForChange = "seed", CreatedByUserId = 1
        });
        await db.SaveChangesAsync();

        var overdue = await new KpiService(db).GetOverdueAnalystStageSamplesAsync(now.AddDays(-30), now.AddDays(1));

        Assert.Equal(new[] { 1 }, overdue.Select(o => o.SampleId));
    }
```
Run:
```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~KpiFilteringTests"
```
Expected: PASS (it documents current behaviour, so it passes immediately; it fails only if someone wires profiles into `KpiService` later). If the file's helpers differ (for example no `NewDb()`), copy the seeding style of the neighbouring overdue test.

- [ ] **Step 6: Commit**

```
git add backend/MicroLIMS.Application/Services/Kpi/AnalystStageTarget.cs backend/MicroLIMS.Tests/UnitTests/Kpi/AnalystStageTargetTests.cs backend/MicroLIMS.Tests/WorkflowTests/KpiFilteringTests.cs
git commit -m "feat(kpi): per-workflow analyst-stage target helper; pin 7-day rule for DashboardService

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Test fixtures and tile 1 - Sample TAT (`sample-tat`)

**Files:**
- Create: `backend/MicroLIMS.Application/Services/Kpi/SampleTatKpi.cs`
- Create: `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiTestData.cs`
- Create: `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiTileTests.cs`
- Modify (append a class): `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiPriorPeriodTests.cs`

**Interfaces:**
- Consumes: Task 1 `KpiScope.ApprovedSamples`, `Days`, `Percentile`, `ComputeAsync`, `Page`; `KpiCore`.
- Produces:
  - `public sealed class SampleTatKpi : IKpi` with ctor `(IMicroLimsDbContext db)`; key `sample-tat`, unit `days`, `LowerIsBetter`, `Headline`, order 1. `Value` = median days; `Extras["p90"]`; `N` = samples.
  - `KpiTestData` (internal, namespace `MicroLIMS.Tests.UnitTests.Kpi`): `Utc(month, day, hour = 0, minute = 0)` (year 2026), `Now` (15 Oct 12:00Z), `PeriodFrom` (1 Oct), `PeriodTo` (1 Nov), `NewDb()`, `Query(sections, category, location, testCode, analystId, canViewTeam, viewer, from, to)`, `Analyst(db, id, name)`, `Definition(db, code, type, sectionId = 1)`, `Profile(db, type, testCode, units, target, from)`, `ApprovedSample(db, id, received, approved, testCode = "TAMC", sectionId = 1, category = FinishedProduct, originSampleId = null, analyst = 1, decision = Approve, testStatus = Approved, superseded = false)` (adds the sample and one test with id `id * 10`), `Test(db, sample, id, code, sectionId = 1, status = Approved, step = Approved, analyst = 1, superseded = false)`, `Signoff(db, sampleId, sectionId, submitted, reviewed, approved, status = Approved, decision = Approve)`, `Prepared(db, sampleId, at)`, `Timeline(db, sampleId, prepared, submitted, reviewed, approved, sectionId = 1)`, `Return(db, testOrderId, at, reason = "re-count")`.

- [ ] **Step 1: Write the fixtures and the failing tile 1 tests**

Create `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiTestData.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Tests.UnitTests.Kpi;

// Fixed-clock fixtures for the KPI tests. October 2026 is the period under
// test and "now" is 15 Oct 12:00 UTC; the prior period is 31 Aug - 30 Sep.
// KPIs read the clock from KpiQuery.Now, so no wall-clock value reaches them.
internal static class KpiTestData
{
    public static DateTime Utc(int month, int day, int hour = 0, int minute = 0) =>
        new(2026, month, day, hour, minute, 0, DateTimeKind.Utc);

    public static readonly DateTime Now = Utc(10, 15, 12);
    public static readonly DateTime PeriodFrom = Utc(10, 1);
    public static readonly DateTime PeriodTo = Utc(11, 1);

    public static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public static KpiQuery Query(IReadOnlyList<int>? sections = null, string? category = null, string? location = null,
        string? testCode = null, int? analystId = null, bool canViewTeam = true, int viewer = 1,
        DateTime? from = null, DateTime? to = null) =>
        new(from ?? PeriodFrom, to ?? PeriodTo, sections, category, location, testCode, analystId, Now, viewer, canViewTeam);

    public static void Analyst(MicroLimsDbContext db, int id, string name) =>
        db.Users.Add(new User { Id = id, FullName = name, Username = $"user{id}", PasswordHash = "x", RoleId = 1 });

    public static void Definition(MicroLimsDbContext db, string code, WorkflowType type, int sectionId = 1) =>
        db.TestDefinitions.Add(new TestDefinition { Code = code, DisplayName = code, SectionId = sectionId, WorkflowType = type });

    public static void Profile(MicroLimsDbContext db, WorkflowType type, string? testCode, decimal? units, decimal? target, DateOnly from) =>
        db.WorkflowProfileVersions.Add(new WorkflowProfileVersion
        {
            WorkflowType = type, TestCode = testCode, WorkUnits = units, TargetAnalystDays = target,
            EffectiveFrom = from, Status = WorkflowProfileStatus.Effective, ReasonForChange = "seed", CreatedByUserId = 1
        });

    // A sample with its final decision and one test (id = sample id * 10).
    public static Sample ApprovedSample(MicroLimsDbContext db, int id, DateTime received, DateTime approved,
        string testCode = "TAMC", int sectionId = 1, SampleCategory category = SampleCategory.FinishedProduct,
        int? originSampleId = null, int? analyst = 1, ApprovalDecision decision = ApprovalDecision.Approve,
        ApprovalStatus testStatus = ApprovalStatus.Approved, bool superseded = false)
    {
        var sample = new Sample
        {
            Id = id, ControlNumber = $"CN-{id}", ReferenceNumber = $"REF-{id}", Category = category,
            ReceivedAt = received, ApprovedAt = approved, ApprovalDecision = decision,
            Status = SampleStatus.Approved, OriginSampleId = originSampleId
        };
        db.Samples.Add(sample);
        Test(db, sample, id * 10, testCode, sectionId, testStatus, WorkflowStep.Approved, analyst, superseded);
        return sample;
    }

    public static TestOrder Test(MicroLimsDbContext db, Sample sample, int id, string code, int sectionId = 1,
        ApprovalStatus status = ApprovalStatus.Approved, WorkflowStep step = WorkflowStep.Approved,
        int? analyst = 1, bool superseded = false)
    {
        var test = new TestOrder
        {
            Id = id, SampleId = sample.Id, Sample = sample, TestCode = code, SectionId = sectionId,
            Status = status, CurrentStep = step, AssignedAnalystId = analyst, IsSuperseded = superseded
        };
        db.TestOrders.Add(test);
        return test;
    }

    // signedAt: the decision signature's SignedAt. Production writes it for every decision, including
    // retest decisions that leave ApprovedAt null; leave it null for fixtures that do not care.
    public static SampleSectionSignoff Signoff(MicroLimsDbContext db, int sampleId, int sectionId, DateTime? submitted,
        DateTime? reviewed, DateTime? approved, SectionSignoffStatus status = SectionSignoffStatus.Approved,
        ApprovalDecision? decision = ApprovalDecision.Approve, DateTime? signedAt = null)
    {
        var signoff = new SampleSectionSignoff
        {
            SampleId = sampleId, SectionId = sectionId, Status = status, SubmittedForReviewAt = submitted,
            ReviewedAt = reviewed, ApprovedAt = approved, ApprovalDecision = decision
        };
        if (signedAt is { } at)
            signoff.ApprovalSignature = new ElectronicSignature
            {
                UserId = 2, UserFullNameSnapshot = "Head", UsernameSnapshot = "head", RoleSnapshot = "SectionHead",
                MeaningOfSignature = SignatureMeaning.RetestRequested, EntityType = "Sample", EntityId = sampleId, SignedAt = at
            };
        db.SampleSectionSignoffs.Add(signoff);
        return signoff;
    }

    public static void Prepared(MicroLimsDbContext db, int sampleId, DateTime at) =>
        db.SamplePreparations.Add(new SamplePreparation { SampleId = sampleId, PreparedAt = at, PreparedByUserId = 1, Technique = "PourPlate" });

    public static void Timeline(MicroLimsDbContext db, int sampleId, DateTime prepared, DateTime submitted,
        DateTime reviewed, DateTime approved, int sectionId = 1)
    {
        Prepared(db, sampleId, prepared);
        Signoff(db, sampleId, sectionId, submitted, reviewed, approved);
    }

    public static void Return(MicroLimsDbContext db, int testOrderId, DateTime at, string? reason = "re-count") =>
        db.TestReturnEvents.Add(new TestReturnEvent { TestOrderId = testOrderId, ReviewerUserId = 2, AssignedAnalystId = 1, Reason = reason, ReturnedAt = at });
}
```

Create `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiTileTests.cs` (this file collects one test class per KPI; later tasks append classes at the end of the file, and every using they need is already here):
```csharp
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;
using static MicroLIMS.Tests.UnitTests.Kpi.KpiTestData;

namespace MicroLIMS.Tests.UnitTests.Kpi;

public class SampleTatKpiTests
{
    // In period: 2, 4, 6, 10 days and a 0.5-day sample approved exactly at the period start.
    // Not counted: a retest clone, a Reject (which also stamps ApprovedAt), a sample approved
    // exactly at the exclusive period end, and a September sample (prior period).
    private static void Seed(MicroLimsDbContext db)
    {
        ApprovedSample(db, 1, Utc(10, 1), Utc(10, 3));
        ApprovedSample(db, 2, Utc(10, 1), Utc(10, 5));
        ApprovedSample(db, 3, Utc(10, 2), Utc(10, 8));
        ApprovedSample(db, 4, Utc(10, 1), Utc(10, 11));
        ApprovedSample(db, 8, Utc(9, 30, 12), Utc(10, 1));
        ApprovedSample(db, 5, Utc(10, 2), Utc(10, 6), originSampleId: 1);
        ApprovedSample(db, 6, Utc(10, 1), Utc(10, 6), decision: ApprovalDecision.Reject);
        ApprovedSample(db, 7, Utc(10, 20), Utc(11, 1));
        ApprovedSample(db, 9, Utc(9, 10), Utc(9, 20));
    }

    private static Task<KpiResult> Run(MicroLimsDbContext db, KpiQuery q) => new SampleTatKpi(db).ComputeAsync(q, default);

    [Fact]
    public async Task SampleTat_MedianAndP90_ForApprovedSamplesOnly()
    {
        await using var db = NewDb();
        Seed(db);
        await db.SaveChangesAsync();

        var r = await Run(db, Query());

        Assert.Equal(4m, r.Value);                  // median of 0.5, 2, 4, 6, 10
        Assert.Equal(8.4m, r.Extras["p90"]);        // 6 + 0.6 * (10 - 6)
        Assert.Equal(5, r.N);
        Assert.Equal(KpiStatus.Ok, r.Status);
        Assert.False(r.TooFewRecords);              // unit is days, not a rate
        Assert.NotNull(r.Prior);
        Assert.Equal(10m, r.Prior!.Value);
        Assert.Equal(1, r.Prior.N);
    }

    [Fact]
    public async Task SampleTat_EmptyPeriod_IsNoData_NotZero()
    {
        await using var db = NewDb();
        var r = await Run(db, Query());

        Assert.Equal(KpiStatus.NoData, r.Status);
        Assert.Null(r.Value);
        Assert.Equal(0, r.N);
        Assert.Null(r.Prior);
        Assert.False(r.Extras.ContainsKey("p90"));
    }

    [Fact]
    public async Task SampleTat_SingleSample_MedianEqualsP90()
    {
        await using var db = NewDb();
        ApprovedSample(db, 1, Utc(10, 1), Utc(10, 4, 12));
        await db.SaveChangesAsync();

        var r = await Run(db, Query());

        Assert.Equal(3.5m, r.Value);
        Assert.Equal(3.5m, r.Extras["p90"]);
        Assert.Equal(1, r.N);
    }

    [Fact]
    public async Task SampleTat_SectionScope_NullSeesAll_EmptySeesNothing()
    {
        await using var db = NewDb();
        ApprovedSample(db, 1, Utc(10, 1), Utc(10, 3), sectionId: 1);
        ApprovedSample(db, 2, Utc(10, 1), Utc(10, 5), sectionId: 2);
        await db.SaveChangesAsync();

        Assert.Equal(2, (await Run(db, Query())).N);                                   // null = unrestricted
        var lab1 = await Run(db, Query(sections: new[] { 1 }));
        Assert.Equal(1, lab1.N);
        Assert.Equal(2m, lab1.Value);
        var none = await Run(db, Query(sections: Array.Empty<int>()));                 // empty = nothing, not "no filter"
        Assert.Equal(KpiStatus.NoData, none.Status);
        Assert.Equal(0, none.N);
        Assert.Equal(KpiStatus.NoData, (await Run(db, Query(sections: new[] { 3 }))).Status);
    }

    [Fact]
    public async Task SampleTat_TwoLabSample_UsesWholeSampleApproval_ForAnyScope()
    {
        // Lab 1 approved on day 3, lab 2 on day 10. Sample.ApprovedAt is stamped by the last section (day 10),
        // so a lab-1-only viewer sees the same 10 days. This is the documented whole-sample rule.
        await using var db = NewDb();
        var sample = ApprovedSample(db, 1, Utc(10, 1), Utc(10, 11), sectionId: 1);
        Test(db, sample, 11, "TYMC", sectionId: 2);
        Signoff(db, 1, 1, null, null, Utc(10, 4));
        Signoff(db, 1, 2, null, null, Utc(10, 11));
        await db.SaveChangesAsync();

        Assert.Equal(10m, (await Run(db, Query())).Value);
        var lab1 = await Run(db, Query(sections: new[] { 1 }));
        Assert.Equal(1, lab1.N);
        Assert.Equal(10m, lab1.Value);
    }

    [Fact]
    public async Task SampleTat_Filters_Category_TestCode_AndAnalyst()
    {
        await using var db = NewDb();
        ApprovedSample(db, 1, Utc(10, 1), Utc(10, 3), testCode: "TAMC", category: SampleCategory.Water, analyst: 7);
        ApprovedSample(db, 2, Utc(10, 1), Utc(10, 5), testCode: "TYMC", category: SampleCategory.FinishedProduct, analyst: 8);
        await db.SaveChangesAsync();

        Assert.Equal(2m, (await Run(db, Query(category: "water"))).Value);
        Assert.Equal(4m, (await Run(db, Query(testCode: "TYMC"))).Value);
        Assert.Equal(2m, (await Run(db, Query(analystId: 7))).Value);
        Assert.Equal(KpiStatus.NoData, (await Run(db, Query(category: "GPT"))).Status);
    }

    [Fact]
    public async Task SampleTat_Records_ArePagedInApprovalOrder()
    {
        await using var db = NewDb();
        Seed(db);
        await db.SaveChangesAsync();
        var kpi = new SampleTatKpi(db);

        var page = await kpi.GetRecordsAsync(Query(), 1, 2, default);

        Assert.Equal("sample-tat", page.Key);
        Assert.Equal(5, page.Total);
        Assert.Equal(new[] { 8, 1 }, page.Items.Select(i => i.SampleId!.Value));   // by ApprovedAt: the boundary sample first
        Assert.Equal(0.5m, page.Items[0].Value);
        Assert.Equal("days", page.Items[0].Unit);
        Assert.All(page.Items, i => Assert.Equal("Denominator", i.Inclusion));
        Assert.Single((await kpi.GetRecordsAsync(Query(), 3, 2, default)).Items);   // the third page holds the last record
    }
}
```

Append to `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiPriorPeriodTests.cs` (after the existing class; add `using static MicroLIMS.Tests.UnitTests.Kpi.KpiTestData;` to the top of the file):
```csharp
public class KpiPriorWiringTests
{
    [Fact]
    public async Task Prior_OnlyShownWhenPriorNIsAboveZero()
    {
        await using var db = NewDb();
        ApprovedSample(db, 1, Utc(10, 1), Utc(10, 3));
        await db.SaveChangesAsync();
        var kpi = new SampleTatKpi(db);

        Assert.Null((await kpi.ComputeAsync(Query(), default)).Prior);     // nothing before: no prior, not a prior of zero

        ApprovedSample(db, 2, Utc(9, 10), Utc(9, 20));
        await db.SaveChangesAsync();
        var withPrior = (await kpi.ComputeAsync(Query(), default)).Prior;
        Assert.NotNull(withPrior);
        Assert.Equal(1, withPrior!.N);
    }

    [Fact]
    public async Task Prior_BoundaryRecord_BelongsToCurrentPeriodNotPrior()
    {
        // Approved exactly at the start of October: current period (From is inclusive), not the prior one (To is exclusive).
        await using var db = NewDb();
        ApprovedSample(db, 1, Utc(9, 30), Utc(10, 1));
        await db.SaveChangesAsync();

        var r = await new SampleTatKpi(db).ComputeAsync(Query(), default);

        Assert.Equal(1, r.N);
        Assert.Null(r.Prior);
    }

    [Fact]
    public async Task Prior_OneDayPeriod_ComparesWithThePreviousDay()
    {
        await using var db = NewDb();
        ApprovedSample(db, 1, Utc(10, 11), Utc(10, 12, 9));    // approved on the 12th: 1.375 days
        ApprovedSample(db, 2, Utc(10, 10), Utc(10, 11, 18));   // approved on the 11th, the prior day: 1.75 days
        await db.SaveChangesAsync();

        var r = await new SampleTatKpi(db).ComputeAsync(Query(from: Utc(10, 12), to: Utc(10, 13)), default);

        Assert.Equal(1, r.N);
        Assert.Equal(1.38m, r.Value);
        Assert.Equal(1, r.Prior!.N);
        Assert.Equal(1.75m, r.Prior.Value);
    }
}
```

- [ ] **Step 2: Run, expect a compile failure**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~UnitTests.Kpi"
```
Expected: build error `'SampleTatKpi' could not be found`.

- [ ] **Step 3: Implement tile 1**

Create `backend/MicroLIMS.Application/Services/Kpi/SampleTatKpi.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Services.Kpi;

// Tile 1. End event is Sample.ApprovedAt, not ReviewWorkflowEvent: that table is shared with Media and
// Cryovial, holds one event per section of a multi-lab sample, and its Timestamp ignores TimeProvider.
public sealed class SampleTatKpi : IKpi
{
    private readonly IMicroLimsDbContext _db;
    public SampleTatKpi(IMicroLimsDbContext db) => _db = db;

    public string Key => "sample-tat";
    public string Name => "Sample turnaround time";
    public string Unit => "days";
    public KpiDirection Direction => KpiDirection.LowerIsBetter;
    public KpiGroup Group => KpiGroup.Headline;
    public int Order => 1;
    public string Definition => "Median calendar days from receipt to final approval, with the 90th percentile alongside. Percentiles are linearly interpolated.";
    public string ClockEvents => "Start: Sample.ReceivedAt. End: Sample.ApprovedAt (decision Approve).";
    public string Population => "Samples approved in the period, excluding retest samples created from an earlier sample, within your sections and the chosen filters. A sample tested by several laboratories counts as approved when the last laboratory approves, so a single-laboratory view still shows the time to that final approval. The record list shows every counted sample.";
    public string Source => "Sample";

    public Task<KpiResult> ComputeAsync(KpiQuery q, CancellationToken ct) =>
        KpiScope.ComputeAsync(q, Unit, async (qq, c) => (await LoadAsync(qq, c)).Core, ct);

    public async Task<KpiRecordPageDto> GetRecordsAsync(KpiQuery q, int page, int pageSize, CancellationToken ct) =>
        KpiScope.Page(Key, (await LoadAsync(q, ct)).Records, page, pageSize);

    private async Task<(KpiCore Core, List<KpiRecordDto> Records)> LoadAsync(KpiQuery q, CancellationToken ct)
    {
        var samples = await KpiScope.ApprovedSamples(_db, q)
            .Select(s => new { s.Id, s.ControlNumber, s.Category, s.ReceivedAt, ApprovedAt = s.ApprovedAt!.Value })
            .ToListAsync(ct);
        if (samples.Count == 0) return (KpiCore.Nothing(), new List<KpiRecordDto>());

        var rows = samples.OrderBy(s => s.ApprovedAt).ThenBy(s => s.Id)
            .Select(s => (Sample: s, Days: KpiScope.Days(s.ReceivedAt, s.ApprovedAt))).ToList();
        var days = rows.Select(r => r.Days).ToList();
        var core = new KpiCore(KpiScope.Percentile(days, 0.5m), null, null, days.Count, KpiCore.NoGaps, KpiCore.NoSeries,
            new Dictionary<string, decimal?> { ["p90"] = KpiScope.Percentile(days, 0.9m) }, KpiCore.NoFlags);
        var records = rows.Select(r => new KpiRecordDto(r.Sample.Id, null, r.Sample.ControlNumber, null, r.Sample.Category.ToString(),
            null, r.Sample.ApprovedAt, r.Days, "days", "Denominator", null)).ToList();
        return (core, records);
    }
}
```

- [ ] **Step 4: Run, expect pass**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~UnitTests.Kpi"
```
Expected: all PASS. (`Prior_OneDayPeriod...` relies on `Math.Round(1.375m, 2)` = 1.38, banker's rounding to the even digit; 1.375 is exactly representable so this is stable.)

- [ ] **Step 5: Commit**

```
git add backend/MicroLIMS.Application/Services/Kpi/SampleTatKpi.cs backend/MicroLIMS.Tests/UnitTests/Kpi/KpiTestData.cs backend/MicroLIMS.Tests/UnitTests/Kpi/KpiTileTests.cs backend/MicroLIMS.Tests/UnitTests/Kpi/KpiPriorPeriodTests.cs
git commit -m "feat(kpi): sample TAT tile with fixed-clock fixtures and prior-period tests

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Tile 2 - On time, all stages (`on-time-all-stages`)

**Files:**
- Create: `backend/MicroLIMS.Application/Services/Kpi/OnTimeAllStagesKpi.cs`
- Modify (append a class): `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiTileTests.cs`

**Interfaces:**
- Consumes: Task 3 `AnalystStageTarget.DaysFor(IEnumerable<string>, DateTime, WorkflowProfileResolver)`; S1 `WorkflowProfileService.LoadResolverAsync`; Task 1 helpers; `SectionReviewQueues.OrderIn`.
- Produces: `public sealed class OnTimeAllStagesKpi : IKpi`, ctor `(IMicroLimsDbContext db, WorkflowProfileService profiles)`; key `on-time-all-stages`, unit `%`, `HigherIsBetter`, `Headline`, order 2. Gap codes: `NO_ASSIGNMENT_TIME`, `INCOMPLETE_TIMELINE`, `DEFAULT_TARGET`.

Rule (contract deviation 3): population = tile 1's samples. The analyst stage starts at `SamplePreparation.PreparedAt` and must end (`SampleSectionSignoff.SubmittedForReviewAt`) within `max(target)` days of it; Review = `SubmittedForReviewAt` to `ReviewedAt` <= 24 h; Approval = `ReviewedAt` to `ApprovedAt` <= 24 h; every in-scope section must meet every stage it has timestamps for; all limits inclusive.

- [ ] **Step 1: Write the failing tests**

Append to `KpiTileTests.cs`:
```csharp
public class OnTimeAllStagesKpiTests
{
    private static OnTimeAllStagesKpi Kpi(MicroLimsDbContext db) =>
        new(db, TestServiceFactory.WorkflowProfile(db, new FixedTimeProvider(new DateTimeOffset(Now))));

    private static void Profiles(MicroLimsDbContext db)
    {
        Definition(db, "TAMC", WorkflowType.CountTest);
        Definition(db, "TYMC", WorkflowType.CountTest);
        Definition(db, "UNPROFILED", WorkflowType.HplcMethodAssay);
        Profile(db, WorkflowType.CountTest, null, 2m, 5m, new DateOnly(2026, 1, 1));         // type default: 5 days
        Profile(db, WorkflowType.CountTest, "TYMC", null, 14m, new DateOnly(2026, 1, 1));    // override: 14 days
    }

    // With the profiles above: 1, 4, 5, 7 on time; 2, 3 late; 6 cannot be judged (no preparation record).
    private static void Samples(MicroLimsDbContext db)
    {
        ApprovedSample(db, 1, Utc(10, 1), Utc(10, 6, 8), "TAMC");
        Timeline(db, 1, Utc(10, 1, 8), Utc(10, 5, 8), Utc(10, 5, 20), Utc(10, 6, 8));        // 4 d, 12 h, 12 h
        ApprovedSample(db, 2, Utc(10, 1), Utc(10, 7, 12), "TAMC");
        Timeline(db, 2, Utc(10, 1, 8), Utc(10, 7, 8), Utc(10, 7, 10), Utc(10, 7, 12));       // 6 d > 5 d: late
        ApprovedSample(db, 3, Utc(10, 1), Utc(10, 13, 10), "TYMC");
        Timeline(db, 3, Utc(10, 1, 8), Utc(10, 12, 8), Utc(10, 13, 9), Utc(10, 13, 10));     // 11 d <= 14 d, but review 25 h: late
        ApprovedSample(db, 4, Utc(10, 1), Utc(10, 8, 8), "TAMC");
        Timeline(db, 4, Utc(10, 1, 8), Utc(10, 6, 8), Utc(10, 7, 8), Utc(10, 8, 8));         // exactly 5 d, 24 h, 24 h: on time
        var five = ApprovedSample(db, 5, Utc(10, 1), Utc(10, 11, 8), "TAMC");
        Test(db, five, 51, "TYMC");                                                           // the longest target (14 d) governs
        Timeline(db, 5, Utc(10, 1, 8), Utc(10, 10, 8), Utc(10, 10, 20), Utc(10, 11, 8));     // 9 d
        ApprovedSample(db, 6, Utc(10, 1), Utc(10, 9, 8), "TAMC");
        Signoff(db, 6, 1, Utc(10, 8, 8), Utc(10, 8, 12), Utc(10, 9, 8));                     // no SamplePreparation row
        ApprovedSample(db, 7, Utc(10, 1), Utc(10, 7, 14), "UNPROFILED");
        Timeline(db, 7, Utc(10, 1, 8), Utc(10, 7, 8), Utc(10, 7, 12), Utc(10, 7, 14));       // 6 d <= default 7 d
    }

    [Fact]
    public async Task OnTime_AllStages_BoundariesAreInclusive_AndMaxTargetRuleApplies()
    {
        await using var db = NewDb();
        Profiles(db);
        Samples(db);
        await db.SaveChangesAsync();

        var r = await Kpi(db).ComputeAsync(Query(), default);

        Assert.Equal(0.6667m, r.Value);
        Assert.Equal(4m, r.Numerator);
        Assert.Equal(6m, r.Denominator);
        Assert.Equal(6, r.N);
        Assert.False(r.TooFewRecords);
        Assert.Equal(KpiStatus.Partial, r.Status);
        Assert.Equal(1, r.Gaps.Single(g => g.Code == "NO_ASSIGNMENT_TIME").Count);
        Assert.Equal(1, r.Gaps.Single(g => g.Code == "DEFAULT_TARGET").Count);   // only UNPROFILED fell back
        Assert.Null(r.Prior);
    }

    [Fact]
    public async Task OnTime_NoProfiles_UsesSevenDayFallback_AndReportsGap()
    {
        await using var db = NewDb();
        Samples(db);                                   // no definitions, no profiles at all
        await db.SaveChangesAsync();

        var r = await Kpi(db).ComputeAsync(Query(), default);

        // 7 days for everybody: 1, 2, 4, 7 on time; 3 (review 25 h) and 5 (9 d) late.
        Assert.Equal(0.6667m, r.Value);
        var gap = r.Gaps.Single(g => g.Code == "DEFAULT_TARGET");
        Assert.Equal(7, gap.Count);                    // one per judged test (sample 5 has two)
        Assert.Equal("7 tests on the default 7-day target", gap.Message);
        Assert.Equal(KpiStatus.Partial, r.Status);
    }

    [Fact]
    public async Task OnTime_SampleWithoutPreparation_IsExcludedAndReported()
    {
        await using var db = NewDb();
        Profiles(db);
        Samples(db);
        await db.SaveChangesAsync();

        var page = await Kpi(db).GetRecordsAsync(Query(), 1, 50, default);

        var excluded = Assert.Single(page.Items, i => i.Inclusion == "Excluded");
        Assert.Equal(6, excluded.SampleId);
        Assert.Contains("assignment", excluded.Note);
        Assert.Equal(4, page.Items.Count(i => i.Inclusion == "Numerator"));
        Assert.Equal(2, page.Items.Count(i => i.Inclusion == "Denominator"));
    }

    [Fact]
    public async Task OnTime_NothingApproved_IsNoData()
    {
        await using var db = NewDb();
        var r = await Kpi(db).ComputeAsync(Query(), default);
        Assert.Equal(KpiStatus.NoData, r.Status);
        Assert.Null(r.Value);
    }

    [Fact]
    public async Task OnTime_OnlyUnjudgeableSamples_IsNoDataWithTheGap()
    {
        await using var db = NewDb();
        ApprovedSample(db, 1, Utc(10, 1), Utc(10, 6));          // no preparation, no signoff
        await db.SaveChangesAsync();

        var r = await Kpi(db).ComputeAsync(Query(), default);

        Assert.Equal(KpiStatus.NoData, r.Status);
        Assert.Null(r.Value);
        Assert.Equal(1, r.Gaps.Single(g => g.Code == "NO_ASSIGNMENT_TIME").Count);
    }

    [Fact]
    public async Task OnTime_OutOfScopeSection_IsNotJudged()
    {
        // Lab 2 was late, but a viewer who only sees lab 1 is judged on lab 1 alone.
        await using var db = NewDb();
        Definition(db, "TAMC", WorkflowType.CountTest);
        Profile(db, WorkflowType.CountTest, null, 2m, 5m, new DateOnly(2026, 1, 1));
        var sample = ApprovedSample(db, 1, Utc(10, 1), Utc(10, 12, 8), "TAMC");
        Test(db, sample, 11, "TAMC", sectionId: 2);
        Prepared(db, 1, Utc(10, 1, 8));
        Signoff(db, 1, 1, Utc(10, 3, 8), Utc(10, 3, 12), Utc(10, 3, 14));
        Signoff(db, 1, 2, Utc(10, 11, 8), Utc(10, 11, 12), Utc(10, 12, 8));      // 10 d: late for lab 2
        await db.SaveChangesAsync();

        Assert.Equal(1m, (await Kpi(db).ComputeAsync(Query(sections: new[] { 1 }), default)).Value);
        Assert.Equal(0m, (await Kpi(db).ComputeAsync(Query(), default)).Value);
    }
}
```

- [ ] **Step 2: Run, expect a compile failure**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~OnTimeAllStagesKpiTests"
```
Expected: `'OnTimeAllStagesKpi' could not be found`.

- [ ] **Step 3: Implement**

Create `backend/MicroLIMS.Application/Services/Kpi/OnTimeAllStagesKpi.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Services.Kpi;

// Tile 2. Replaces the old max-across-sections stage window: each section is judged on its own timestamps.
public sealed class OnTimeAllStagesKpi : IKpi
{
    // Review and approval SLAs, in calendar hours.
    private static readonly TimeSpan StageLimit = TimeSpan.FromHours(24);

    private readonly IMicroLimsDbContext _db;
    private readonly WorkflowProfileService _profiles;

    public OnTimeAllStagesKpi(IMicroLimsDbContext db, WorkflowProfileService profiles)
    {
        _db = db;
        _profiles = profiles;
    }

    public string Key => "on-time-all-stages";
    public string Name => "On time, all stages";
    public string Unit => "%";
    public KpiDirection Direction => KpiDirection.HigherIsBetter;
    public KpiGroup Group => KpiGroup.Headline;
    public int Order => 2;
    public string Definition => "Share of approved samples that met every stage target: analyst stage within the longest per-workflow target of the sample's assigned tests (7 days where no target is configured), then review within 24 h and approval within 24 h, for every section you can see.";
    public string ClockEvents => "Analyst stage: SamplePreparation.PreparedAt to the section's submit-for-review time. Review: submit to reviewed. Approval: reviewed to approved. Review and approval are calendar hours, all limits inclusive.";
    public string Population => "Samples approved in the period (as for turnaround). Samples with no preparation record (finished-product physicochemical, environmental monitoring, retest clones) cannot be judged and are reported as a gap.";
    public string Source => "Sample, SampleSectionSignoff, SamplePreparation, TestOrder, workflow profile versions";

    public Task<KpiResult> ComputeAsync(KpiQuery q, CancellationToken ct) =>
        KpiScope.ComputeAsync(q, Unit, async (qq, c) => (await LoadAsync(qq, c)).Core, ct);

    public async Task<KpiRecordPageDto> GetRecordsAsync(KpiQuery q, int page, int pageSize, CancellationToken ct) =>
        KpiScope.Page(Key, (await LoadAsync(q, ct)).Records, page, pageSize);

    private async Task<(KpiCore Core, List<KpiRecordDto> Records)> LoadAsync(KpiQuery q, CancellationToken ct)
    {
        var samples = await KpiScope.ApprovedSamples(_db, q)
            .Select(s => new { s.Id, s.ControlNumber, s.Category, ApprovedAt = s.ApprovedAt!.Value })
            .ToListAsync(ct);
        if (samples.Count == 0) return (KpiCore.Nothing(), new List<KpiRecordDto>());

        var ids = samples.Select(s => s.Id).ToList();
        var prepared = await _db.SamplePreparations.AsNoTracking().Where(p => ids.Contains(p.SampleId))
            .ToDictionaryAsync(p => p.SampleId, p => p.PreparedAt, ct);
        var signoffs = (await _db.SampleSectionSignoffs.AsNoTracking().Where(KpiScope.SignoffIn(q.SectionIds))
                .Where(x => ids.Contains(x.SampleId) && x.SubmittedForReviewAt != null)
                .Select(x => new { x.SampleId, Submitted = x.SubmittedForReviewAt!.Value, x.ReviewedAt, x.ApprovedAt })
                .ToListAsync(ct))
            .ToLookup(x => x.SampleId);
        var codes = (await _db.TestOrders.AsNoTracking().Where(SectionReviewQueues.OrderIn(q.SectionIds))
                .Where(t => ids.Contains(t.SampleId) && !t.IsSuperseded && t.AssignedAnalystId != null)
                .Select(t => new { t.SampleId, t.TestCode })
                .ToListAsync(ct))
            .ToLookup(t => t.SampleId, t => t.TestCode);
        var resolver = await _profiles.LoadResolverAsync(ct);

        int noAssignment = 0, noTimeline = 0, defaultTests = 0, evaluated = 0, onTime = 0;
        var records = new List<KpiRecordDto>();
        var monthly = new List<(DateTime At, decimal Num, decimal Den)>();

        foreach (var s in samples.OrderBy(s => s.ApprovedAt).ThenBy(s => s.Id))
        {
            KpiRecordDto Rec(string inclusion, string? note) =>
                new(s.Id, null, s.ControlNumber, null, s.Category.ToString(), null, s.ApprovedAt, null, null, inclusion, note);

            if (!prepared.TryGetValue(s.Id, out var preparedAt))
            {
                noAssignment++;
                records.Add(Rec("Excluded", "No assignment time (no preparation record)"));
                continue;
            }
            var sectionRows = signoffs[s.Id].ToList();
            if (sectionRows.Count == 0)
            {
                noTimeline++;
                records.Add(Rec("Excluded", "No review timeline recorded"));
                continue;
            }

            // The profile in force when the sample was approved (D4), not today's.
            var target = AnalystStageTarget.DaysFor(codes[s.Id], s.ApprovedAt, resolver);
            defaultTests += target.TestsOnDefault;
            var deadline = preparedAt.AddDays((double)target.Days);

            var ok = sectionRows.All(x =>
                x.Submitted <= deadline
                && (x.ReviewedAt is null || x.ReviewedAt.Value - x.Submitted <= StageLimit)
                && (x.ReviewedAt is null || x.ApprovedAt is null || x.ApprovedAt.Value - x.ReviewedAt.Value <= StageLimit));
            evaluated++;
            if (ok) onTime++;
            monthly.Add((s.ApprovedAt, ok ? 1m : 0m, 1m));
            records.Add(Rec(ok ? "Numerator" : "Denominator", ok ? null : "At least one stage exceeded its limit"));
        }

        var gaps = KpiScope.Gaps(
            ("NO_ASSIGNMENT_TIME", $"{noAssignment} samples without an assignment time", noAssignment),
            ("INCOMPLETE_TIMELINE", $"{noTimeline} samples without a review timeline", noTimeline),
            ("DEFAULT_TARGET", $"{defaultTests} tests on the default 7-day target", defaultTests));

        if (evaluated == 0) return (KpiCore.Nothing(gaps), records);
        var core = new KpiCore(KpiScope.Rate(onTime, evaluated), onTime, evaluated, evaluated, gaps,
            KpiScope.MonthlySeries(monthly), KpiCore.NoExtras, KpiCore.NoFlags);
        return (core, records);
    }
}
```

- [ ] **Step 4: Run, expect pass**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~OnTimeAllStagesKpiTests"
```
Expected: all PASS. If one fails, re-derive from the fixture comments: with profiles, sample 5 has TAMC (5 d) and TYMC (14 d) so its limit is 14 d and 9 d passes; sample 7 falls back to 7 d and 6 d passes; sample 4 sits exactly on every limit (5 d, 24 h, 24 h) and must pass.

- [ ] **Step 5: Commit**

```
git add backend/MicroLIMS.Application/Services/Kpi/OnTimeAllStagesKpi.cs backend/MicroLIMS.Tests/UnitTests/Kpi/KpiTileTests.cs
git commit -m "feat(kpi): on-time-all-stages tile with per-workflow target and gap data

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Tile 3 - Right first time (`right-first-time`)

**Files:**
- Create: `backend/MicroLIMS.Application/Services/Kpi/RightFirstTimeKpi.cs`
- Modify (append a class): `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiTileTests.cs`

**Interfaces:**
- Consumes: S1 `WorkflowProfileService.LoadResolverAsync`, `ResolvedProfile.WorkUnits`; `TestServiceFactory.WorkflowProfile`; Task 1 helpers; `IMicroLimsDbContext.TestReturnEvents`, `TestDefinitions`.
- Produces: `public sealed class RightFirstTimeKpi : IKpi`, ctor `(IMicroLimsDbContext db, WorkflowProfileService profiles)`; key `right-first-time`, unit `%`, `HigherIsBetter`, `Headline`, order 3. `Numerator` = units of approved tests never returned, `Denominator` = units of approved weighted tests, `N` = weighted tests. Gap codes `UNWEIGHTED_TESTS`, `OBSERVATION_NOT_TRACKED`. Series: monthly rate.

Rule (deviations 4, 5): a test counts when `TestOrder.Status == Approved`, `!IsSuperseded` and its section's signoff is `Approved` with `ApprovedAt` in the period; the profile is resolved at that signoff time; returns are matched by `TestOrderId`; Observation tests are excluded (they cannot generate a `TestReturnEvent`) and reported; tests without resolvable units are excluded and reported.

- [ ] **Step 1: Write the failing tests**

Append to `KpiTileTests.cs`:
```csharp
public class RightFirstTimeKpiTests
{
    private static RightFirstTimeKpi Kpi(MicroLimsDbContext db) =>
        new(db, TestServiceFactory.WorkflowProfile(db, new FixedTimeProvider(new DateTimeOffset(Now))));

    private static void Definitions(MicroLimsDbContext db)
    {
        Definition(db, "TAMC", WorkflowType.CountTest);
        Definition(db, "HPLC1", WorkflowType.HplcMethodAssay);
        Definition(db, "PATH", WorkflowType.Observation);
        Definition(db, "NOPROF", WorkflowType.Titration);
    }

    private static void Profiles(MicroLimsDbContext db)
    {
        Profile(db, WorkflowType.CountTest, null, 2m, null, new DateOnly(2026, 1, 1));
        Profile(db, WorkflowType.HplcMethodAssay, null, 4m, null, new DateOnly(2026, 1, 1));
        Profile(db, WorkflowType.Observation, null, 1m, null, new DateOnly(2026, 1, 1));   // configured, still excluded
    }

    private static void Approve(MicroLimsDbContext db, int sampleId, DateTime at) => Signoff(db, sampleId, 1, null, null, at);

    // Counted: 1 (2 u), 2 (2 u, returned), 3 (4 u), 4 (4 u, returned twice) -> 6 of 12 units.
    // Reported gaps: 5 (observation), 6 (no profile). Ignored: 7 (superseded), 8 (Rejected).
    // Prior period: 9 (2 u, never returned).
    private static void Seed(MicroLimsDbContext db)
    {
        ApprovedSample(db, 1, Utc(10, 1), Utc(10, 3), "TAMC");    Approve(db, 1, Utc(10, 3));
        ApprovedSample(db, 2, Utc(10, 1), Utc(10, 4), "TAMC");    Approve(db, 2, Utc(10, 4));
        ApprovedSample(db, 3, Utc(10, 1), Utc(10, 5), "HPLC1");   Approve(db, 3, Utc(10, 5));
        ApprovedSample(db, 4, Utc(10, 1), Utc(10, 6), "HPLC1");   Approve(db, 4, Utc(10, 6));
        ApprovedSample(db, 5, Utc(10, 1), Utc(10, 7), "PATH");    Approve(db, 5, Utc(10, 7));
        ApprovedSample(db, 6, Utc(10, 1), Utc(10, 8), "NOPROF");  Approve(db, 6, Utc(10, 8));
        ApprovedSample(db, 7, Utc(10, 1), Utc(10, 8), "TAMC", superseded: true);  Approve(db, 7, Utc(10, 8));
        ApprovedSample(db, 8, Utc(10, 1), Utc(10, 9), "TAMC", decision: ApprovalDecision.Reject, testStatus: ApprovalStatus.Rejected);
        Signoff(db, 8, 1, null, null, Utc(10, 9), SectionSignoffStatus.Rejected, ApprovalDecision.Reject);   // a Reject also stamps ApprovedAt
        ApprovedSample(db, 9, Utc(9, 10), Utc(9, 20), "TAMC");    Approve(db, 9, Utc(9, 20));
        Return(db, 20, Utc(10, 3, 12));
        Return(db, 40, Utc(10, 4, 12));
        Return(db, 40, Utc(10, 5, 12));
    }

    [Fact]
    public async Task RightFirstTime_WeightsByUnits_ExcludesObservationAndUnweighted()
    {
        await using var db = NewDb();
        Definitions(db);
        Profiles(db);
        Seed(db);
        await db.SaveChangesAsync();

        var r = await Kpi(db).ComputeAsync(Query(), default);

        Assert.Equal(0.5m, r.Value);
        Assert.Equal(6m, r.Numerator);
        Assert.Equal(12m, r.Denominator);
        Assert.Equal(4, r.N);                      // tests, not units: 4 < 5, so the value is flagged as too few
        Assert.True(r.TooFewRecords);
        Assert.Equal(KpiStatus.Partial, r.Status);
        Assert.Equal(1, r.Gaps.Single(g => g.Code == "OBSERVATION_NOT_TRACKED").Count);
        Assert.Equal(1, r.Gaps.Single(g => g.Code == "UNWEIGHTED_TESTS").Count);
        Assert.Equal("2026-10", Assert.Single(r.Series).Label);
        Assert.Equal(0.5m, r.Series[0].Value);
        Assert.Equal(1m, r.Prior!.Value);          // September: 2 u of 2 u
        Assert.Equal(1, r.Prior.N);
    }

    [Fact]
    public async Task RightFirstTime_NoProfiles_IsNoData_NotOneHundredPercent()
    {
        await using var db = NewDb();
        Definitions(db);        // definitions, but no profile versions at all
        Seed(db);
        await db.SaveChangesAsync();

        var r = await Kpi(db).ComputeAsync(Query(), default);

        Assert.Equal(KpiStatus.NoData, r.Status);
        Assert.Null(r.Value);                      // never defaulted to a weight of 1.0
        Assert.Equal(0, r.N);
        var gap = r.Gaps.Single(g => g.Code == "UNWEIGHTED_TESTS");
        Assert.Equal(5, gap.Count);                // samples 1, 2, 3, 4, 6
        Assert.Equal("5 tests unweighted", gap.Message);
    }

    [Fact]
    public async Task RightFirstTime_ProfileAddedAfterApproval_DoesNotRewriteEarlierTests()
    {
        await using var db = NewDb();
        Definitions(db);
        Profiles(db);
        Seed(db);
        Profile(db, WorkflowType.CountTest, null, 8m, null, new DateOnly(2026, 10, 20));   // after every approval above
        await db.SaveChangesAsync();

        var r = await Kpi(db).ComputeAsync(Query(), default);

        Assert.Equal(12m, r.Denominator);          // still 2 u per count test, as in force when approved
        Assert.Equal(0.5m, r.Value);
    }

    [Fact]
    public async Task RightFirstTime_TestApprovedAfterANewVersion_UsesTheNewVersion()
    {
        await using var db = NewDb();
        Definitions(db);
        Profile(db, WorkflowType.CountTest, null, 2m, null, new DateOnly(2026, 1, 1));
        Profile(db, WorkflowType.CountTest, null, 6m, null, new DateOnly(2026, 10, 5));
        ApprovedSample(db, 1, Utc(10, 1), Utc(10, 3), "TAMC"); Approve(db, 1, Utc(10, 3));    // 2 u
        ApprovedSample(db, 2, Utc(10, 1), Utc(10, 6), "TAMC"); Approve(db, 2, Utc(10, 6));    // 6 u
        await db.SaveChangesAsync();

        var r = await Kpi(db).ComputeAsync(Query(), default);

        Assert.Equal(8m, r.Denominator);
    }

    [Fact]
    public async Task RightFirstTime_Records_ShowReturnedAndExcludedTests_WithoutOtherAnalystNames()
    {
        await using var db = NewDb();
        Analyst(db, 1, "Ann Analyst");
        Definitions(db);
        Profiles(db);
        Seed(db);
        await db.SaveChangesAsync();

        var all = await Kpi(db).GetRecordsAsync(Query(), 1, 50, default);
        Assert.Equal(6, all.Total);                                           // 4 counted + observation + unweighted
        Assert.Equal("Ann Analyst", all.Items.First().AnalystName);
        Assert.Equal(2, all.Items.Count(i => i.Inclusion == "Excluded"));
        Assert.Equal(2, all.Items.Count(i => i.Inclusion == "Numerator"));
        Assert.Equal(2, all.Items.Count(i => i.Inclusion == "Denominator"));

        var other = await Kpi(db).GetRecordsAsync(Query(canViewTeam: false, viewer: 99), 1, 50, default);
        Assert.All(other.Items, i => Assert.Null(i.AnalystName));             // D8: not mine, not shown
    }

    [Fact]
    public async Task RightFirstTime_EmptyPeriod_IsNoData()
    {
        await using var db = NewDb();
        var r = await Kpi(db).ComputeAsync(Query(), default);
        Assert.Equal(KpiStatus.NoData, r.Status);
        Assert.Null(r.Value);
        Assert.Empty(r.Gaps);
    }
}
```

- [ ] **Step 2: Run, expect a compile failure**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~RightFirstTimeKpiTests"
```
Expected: `'RightFirstTimeKpi' could not be found`.

- [ ] **Step 3: Implement**

Create `backend/MicroLIMS.Application/Services/Kpi/RightFirstTimeKpi.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Kpi;

// Tile 3. ResultRecord.ApprovedAt is sample-level (null for a lab that closed earlier), so approval time comes
// from the section signoff, and tests are counted as TestOrder rows (a ResultRecord can have many rows per test).
public sealed class RightFirstTimeKpi : IKpi
{
    private readonly IMicroLimsDbContext _db;
    private readonly WorkflowProfileService _profiles;

    public RightFirstTimeKpi(IMicroLimsDbContext db, WorkflowProfileService profiles)
    {
        _db = db;
        _profiles = profiles;
    }

    public string Key => "right-first-time";
    public string Name => "Right first time";
    public string Unit => "%";
    public KpiDirection Direction => KpiDirection.HigherIsBetter;
    public KpiGroup Group => KpiGroup.Headline;
    public int Order => 3;
    public string Definition => "Work units of approved tests that were never returned to the analyst, divided by the work units of all approved tests. Tests without a work-unit profile are left out and listed as a gap; they are never given a default weight.";
    public string ClockEvents => "A test is counted in the period in which its section's sign-off was approved. Its work units are those in force on that date. A return is any TestReturnEvent for the test.";
    public string Population => "Non-superseded tests with status Approved whose section sign-off was approved in the period. Pathogen observation tests are excluded because a send-back there is not recorded as a return.";
    public string Source => "TestOrder, SampleSectionSignoff, TestReturnEvent, workflow profile versions";

    public Task<KpiResult> ComputeAsync(KpiQuery q, CancellationToken ct) =>
        KpiScope.ComputeAsync(q, Unit, async (qq, c) => (await LoadAsync(qq, c)).Core, ct);

    public async Task<KpiRecordPageDto> GetRecordsAsync(KpiQuery q, int page, int pageSize, CancellationToken ct) =>
        KpiScope.Page(Key, (await LoadAsync(q, ct)).Records, page, pageSize);

    private async Task<(KpiCore Core, List<KpiRecordDto> Records)> LoadAsync(KpiQuery q, CancellationToken ct)
    {
        var from = q.From;
        var to = q.To;
        var signoffs = await _db.SampleSectionSignoffs.AsNoTracking().Where(KpiScope.SignoffIn(q.SectionIds))
            .Where(x => x.Status == SectionSignoffStatus.Approved && x.ApprovedAt != null && x.ApprovedAt >= from && x.ApprovedAt < to)
            .Select(x => new { x.SampleId, x.SectionId, ApprovedAt = x.ApprovedAt!.Value })
            .ToListAsync(ct);
        if (signoffs.Count == 0) return (KpiCore.Nothing(), new List<KpiRecordDto>());

        var sampleIds = signoffs.Select(s => s.SampleId).Distinct().ToList();
        var orders = await KpiScope.FilterOrders(_db.TestOrders.AsNoTracking()
                .Where(t => sampleIds.Contains(t.SampleId) && t.Status == ApprovalStatus.Approved && !t.IsSuperseded), q)
            .Select(t => new { t.Id, t.SampleId, t.SectionId, t.TestCode, t.AssignedAnalystId, t.Sample!.ControlNumber, t.Sample.Category })
            .ToListAsync(ct);

        var approvedAt = signoffs.GroupBy(s => (s.SampleId, s.SectionId)).ToDictionary(g => g.Key, g => g.Max(s => s.ApprovedAt));
        var orderIds = orders.Select(o => o.Id).ToList();
        var returned = (await _db.TestReturnEvents.AsNoTracking().Where(e => orderIds.Contains(e.TestOrderId))
            .Select(e => e.TestOrderId).Distinct().ToListAsync(ct)).ToHashSet();
        var codes = orders.Select(o => o.TestCode).Distinct().ToList();
        var types = await _db.TestDefinitions.AsNoTracking().Where(d => codes.Contains(d.Code))
            .ToDictionaryAsync(d => d.Code, d => d.WorkflowType, ct);
        var names = await KpiScope.UserNamesAsync(_db, orders.Select(o => o.AssignedAnalystId), ct);
        var resolver = await _profiles.LoadResolverAsync(ct);

        decimal num = 0m, den = 0m;
        int counted = 0, observation = 0, unweighted = 0;
        var records = new List<KpiRecordDto>();
        var monthly = new List<(DateTime At, decimal Num, decimal Den)>();

        foreach (var o in orders.OrderBy(o => o.Id))
        {
            if (!approvedAt.TryGetValue((o.SampleId, o.SectionId), out var at)) continue;   // no matching approved sign-off
            KpiRecordDto Rec(string inclusion, decimal? units, string? note) =>
                new(o.SampleId, o.Id, o.ControlNumber, o.TestCode, o.Category.ToString(),
                    KpiScope.AnalystLabel(q, o.AssignedAnalystId, names), at, units, "units", inclusion, note);

            if (types.TryGetValue(o.TestCode, out var type) && type == WorkflowType.Observation)
            {
                observation++;
                records.Add(Rec("Excluded", null, "Observation test: send-backs are not recorded as returns"));
                continue;
            }
            var units = resolver.Resolve(o.TestCode, at).WorkUnits;
            if (units is null)
            {
                unweighted++;
                records.Add(Rec("Excluded", null, "No work-unit profile in force on the approval date"));
                continue;
            }

            var wasReturned = returned.Contains(o.Id);
            counted++;
            den += units.Value;
            if (!wasReturned) num += units.Value;
            monthly.Add((at, wasReturned ? 0m : units.Value, units.Value));
            records.Add(Rec(wasReturned ? "Denominator" : "Numerator", units, wasReturned ? "Returned to the analyst at least once" : null));
        }

        var gaps = KpiScope.Gaps(
            ("UNWEIGHTED_TESTS", $"{unweighted} tests unweighted", unweighted),
            ("OBSERVATION_NOT_TRACKED", $"{observation} observation tests not tracked for returns", observation));
        if (counted == 0) return (KpiCore.Nothing(gaps), records);
        return (new KpiCore(KpiScope.Rate(num, den), num, den, counted, gaps, KpiScope.MonthlySeries(monthly), KpiCore.NoExtras, KpiCore.NoFlags), records);
    }
}
```

- [ ] **Step 4: Run, expect pass**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~RightFirstTimeKpiTests"
```
Expected: all PASS.

- [ ] **Step 5: Commit**

```
git add backend/MicroLIMS.Application/Services/Kpi/RightFirstTimeKpi.cs backend/MicroLIMS.Tests/UnitTests/Kpi/KpiTileTests.cs
git commit -m "feat(kpi): right-first-time tile weighted by work units

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Tiles 4 and 5 - Retest rate and OOS rate (`retest-rate`, `oos-rate`)

**Files:**
- Modify: `backend/MicroLIMS.Application/Services/Kpi/KpiScope.cs` (add the shared decision loader inside the static class, before its closing brace)
- Create: `backend/MicroLIMS.Application/Services/Kpi/RetestRateKpi.cs`
- Create: `backend/MicroLIMS.Application/Services/Kpi/OosRateKpi.cs`
- Modify (append classes): `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiTileTests.cs`

**Interfaces:**
- Consumes: Task 1 `KpiScope.FilterSamples`, `SignoffIn`, `Rate`, `MonthlySeries`, `ComputeAsync`, `Page`.
- Produces:
  - `KpiScope.DecidedSample(int SampleId, string ControlNumber, SampleCategory Category, DateTime DecidedAt, bool Retest, bool Oos, string? OosGroupCode)` (public sealed record, nested in `KpiScope`), `KpiScope.LoadDecidedSamplesAsync(IMicroLimsDbContext, KpiQuery, CancellationToken)`, `KpiScope.DecisionRateAsync(IMicroLimsDbContext, KpiQuery, Func<DecidedSample,bool> inNumerator, Func<DecidedSample,string?> note, CancellationToken)` -> `(KpiCore Core, List<KpiRecordDto> Records)`.
  - `RetestRateKpi` / `OosRateKpi : IKpi`, ctor `(IMicroLimsDbContext db)`; keys `retest-rate`, `oos-rate`; unit `%`; `LowerIsBetter`; `Headline`; orders 4 and 5.

Rules (contract deviation 2, refined here after checking `SampleApprovalService`): a signoff is decided when `ApprovalDecision != null`. Its decision time is `ApprovalSignature.SignedAt` (written for every decision), falling back to `ApprovedAt`: production stamps `ApprovedAt` only for Approve, Reject and chain resolution, so a `RetestRetainedSample` or `NewSampleRequest` decision has a signature but a null `ApprovedAt`. `ReviewWorkflowEvent` is not used. Retest clones (`OriginSampleId != null`) are excluded because they copy the origin's `OosGroupCode`. The denominator is distinct non-clone samples with at least one decided signoff in the period (within the viewer's sections). Flag to the human: contract deviation 2 says `ApprovedAt` is the decision time; this plan uses the signature time instead. Tile 4 numerator: a decided signoff is `RetestRetainedSample`. Tile 5 numerator: the sample has `OosGroupCode != null` (the `OosTrackingService` definition; nothing ever writes the `OOSInvestigation` decision, so the spec's literal formula would always be 0). Tile 4 is therefore a subset of tile 5; the Definition text says so.

- [ ] **Step 1: Write the failing tests**

Append to `KpiTileTests.cs`:
```csharp
public class DecisionRateKpiTests
{
    // Mirrors SampleApprovalService: every decision writes the signature (SignedAt = decision time);
    // ApprovedAt is stamped only by Approve and Reject. RetestRetainedSample and NewSampleRequest leave
    // ApprovedAt null and the signoff in RetestRequested until the retest resolves.
    private static Sample Decided(MicroLimsDbContext db, int id, ApprovalDecision decision, DateTime at, string? oos = null,
        int section = 1, int? originSampleId = null)
    {
        var sample = ApprovedSample(db, id, Utc(10, 1), at, decision: decision, sectionId: section, originSampleId: originSampleId);
        sample.OosGroupCode = oos;
        DecidedSignoff(db, id, section, decision, at);
        return sample;
    }

    private static void DecidedSignoff(MicroLimsDbContext db, int sampleId, int section, ApprovalDecision decision, DateTime at)
    {
        var stamped = decision is ApprovalDecision.Approve or ApprovalDecision.Reject;
        Signoff(db, sampleId, section, null, null, stamped ? at : null,
            decision == ApprovalDecision.Approve ? SectionSignoffStatus.Approved
                : decision == ApprovalDecision.Reject ? SectionSignoffStatus.Rejected
                : SectionSignoffStatus.RetestRequested,
            decision, signedAt: at);
    }

    // Seven distinct decided samples in October:
    // 1, 2, 5 Approve | 3 Reject | 4 RetestRetainedSample (OOS-1) | 6 NewSampleRequest (OOS-2)
    // 7 is a two-lab sample: lab 1 approved, lab 2 asked for a retest (OOS-3).
    // Not counted: 9 decided exactly at the exclusive period end; 10, the retest clone of 4 (same OOS-1
    // group, approved in October). Prior period: 8 (Approve).
    private static void Seed(MicroLimsDbContext db)
    {
        Decided(db, 1, ApprovalDecision.Approve, Utc(10, 3));
        Decided(db, 2, ApprovalDecision.Approve, Utc(10, 4));
        Decided(db, 3, ApprovalDecision.Reject, Utc(10, 5));
        Decided(db, 4, ApprovalDecision.RetestRetainedSample, Utc(10, 6), "OOS-1");
        Decided(db, 5, ApprovalDecision.Approve, Utc(10, 7));
        Decided(db, 6, ApprovalDecision.NewSampleRequest, Utc(10, 8), "OOS-2");
        var seven = Decided(db, 7, ApprovalDecision.Approve, Utc(10, 9), "OOS-3");
        Test(db, seven, 71, "TAMC", sectionId: 2);
        DecidedSignoff(db, 7, 2, ApprovalDecision.RetestRetainedSample, Utc(10, 9));
        Decided(db, 10, ApprovalDecision.Approve, Utc(10, 12), "OOS-1", originSampleId: 4);
        Decided(db, 9, ApprovalDecision.Approve, Utc(11, 1));
        Decided(db, 8, ApprovalDecision.Approve, Utc(9, 20));
    }

    [Fact]
    public async Task Retest_SignatureWithoutApprovedAt_CountsInSignedPeriod()
    {
        // Production shape of a retest decision: signature written, ApprovedAt null.
        await using var db = NewDb();
        Decided(db, 1, ApprovalDecision.RetestRetainedSample, Utc(10, 6), "OOS-1");
        Decided(db, 2, ApprovalDecision.RetestRetainedSample, Utc(9, 28), "OOS-2");
        await db.SaveChangesAsync();

        var r = await new RetestRateKpi(db).ComputeAsync(Query(), default);

        Assert.Equal((1m, 1m), (r.Numerator!.Value, r.Denominator!.Value));   // sample 1, signed 6 Oct
        Assert.Equal(1, r.Prior!.N);                                          // sample 2, signed 28 Sep
        Assert.Equal(1m, r.Prior.Value);
    }

    [Fact]
    public async Task OosRate_OriginAndCloneInSameGroup_CountOnce()
    {
        await using var db = NewDb();
        Decided(db, 1, ApprovalDecision.RetestRetainedSample, Utc(10, 6), "OOS-1");
        Decided(db, 2, ApprovalDecision.Approve, Utc(10, 12), "OOS-1", originSampleId: 1);   // the clone
        await db.SaveChangesAsync();

        var oos = await new OosRateKpi(db).ComputeAsync(Query(), default);

        Assert.Equal((1m, 1m), (oos.Numerator!.Value, oos.Denominator!.Value));
        Assert.Equal(1, oos.N);
    }

    [Fact]
    public async Task RetestAndOos_Rates_CountDistinctDecidedSamples()
    {
        await using var db = NewDb();
        Seed(db);
        await db.SaveChangesAsync();

        var retest = await new RetestRateKpi(db).ComputeAsync(Query(), default);
        Assert.Equal((2m, 7m), (retest.Numerator!.Value, retest.Denominator!.Value));   // samples 4 and 7
        Assert.Equal(0.2857m, retest.Value);
        Assert.Equal(7, retest.N);
        Assert.False(retest.TooFewRecords);
        Assert.Equal(0m, retest.Prior!.Value);                                           // September: 0 of 1
        Assert.Equal(1, retest.Prior.N);

        var oos = await new OosRateKpi(db).ComputeAsync(Query(), default);
        Assert.Equal(3m, oos.Numerator);                                                 // samples 4, 6, 7 carry an OOS group
        Assert.Equal(0.4286m, oos.Value);
        Assert.Equal(KpiStatus.Ok, oos.Status);
    }

    [Fact]
    public async Task Retest_SectionScope_UsesInScopeSignoffsOnly()
    {
        await using var db = NewDb();
        Seed(db);
        await db.SaveChangesAsync();

        // A lab-1-only viewer never sees lab 2's retest decision on sample 7.
        var lab1 = await new RetestRateKpi(db).ComputeAsync(Query(sections: new[] { 1 }), default);
        Assert.Equal(1m, lab1.Numerator);                 // sample 4 only
        Assert.Equal(7m, lab1.Denominator);

        var lab2 = await new RetestRateKpi(db).ComputeAsync(Query(sections: new[] { 2 }), default);
        Assert.Equal(1m, lab2.Value);                     // sample 7, decided by lab 2
        Assert.Equal(1, lab2.N);
        Assert.True(lab2.TooFewRecords);

        var none = await new RetestRateKpi(db).ComputeAsync(Query(sections: Array.Empty<int>()), default);
        Assert.Equal(KpiStatus.NoData, none.Status);
    }

    [Fact]
    public async Task DecisionRates_EmptyPeriod_IsNoData()
    {
        await using var db = NewDb();
        foreach (IKpi kpi in new IKpi[] { new RetestRateKpi(db), new OosRateKpi(db) })
        {
            var r = await kpi.ComputeAsync(Query(), default);
            Assert.Equal(KpiStatus.NoData, r.Status);
            Assert.Null(r.Value);                         // not 0% and not 100%
            Assert.Equal(0, r.N);
        }
    }

    [Fact]
    public async Task DecisionRates_Records_MarkNumeratorAndDenominator()
    {
        await using var db = NewDb();
        Seed(db);
        await db.SaveChangesAsync();

        var page = await new RetestRateKpi(db).GetRecordsAsync(Query(), 1, 50, default);

        Assert.Equal(7, page.Total);
        Assert.Equal(new[] { 4, 7 }, page.Items.Where(i => i.Inclusion == "Numerator").Select(i => i.SampleId!.Value));
        Assert.Equal(5, page.Items.Count(i => i.Inclusion == "Denominator"));
    }
}
```

- [ ] **Step 2: Run, expect a compile failure**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~DecisionRateKpiTests"
```
Expected: `'RetestRateKpi' could not be found`.

- [ ] **Step 3: Add the shared loader to `KpiScope`**

In `backend/MicroLIMS.Application/Services/Kpi/KpiScope.cs`, insert before the final closing brace of `KpiScope`:
```csharp
    // ---- decision-based rates (tiles 4 and 5) ----

    public sealed record DecidedSample(int SampleId, string ControlNumber, SampleCategory Category, DateTime DecidedAt,
        bool Retest, bool Oos, string? OosGroupCode);

    // One entry per sample that has at least one decided sign-off in the window and section scope.
    // The decision time is the decision signature's SignedAt: SampleApprovalService writes it for every
    // decision, but stamps ApprovedAt only for Approve, Reject and chain resolution (a retest or
    // new-sample decision leaves it null until the retest resolves). ApprovedAt is the fallback for
    // signoffs without a signature. A Reject also stamps ApprovedAt, so the decision itself marks "decided".
    // Retest clones (OriginSampleId set) are excluded: they copy the origin's OosGroupCode, so counting
    // them would count one OOS event twice and inflate the retest-rate denominator.
    public static async Task<List<DecidedSample>> LoadDecidedSamplesAsync(IMicroLimsDbContext db, KpiQuery q, CancellationToken ct)
    {
        var start = q.From;
        var end = q.To;
        var rows = await (
                from so in db.SampleSectionSignoffs.AsNoTracking().Where(SignoffIn(q.SectionIds))
                where so.ApprovalDecision != null
                let decidedAt = so.ApprovalSignature != null ? so.ApprovalSignature.SignedAt : so.ApprovedAt
                where decidedAt != null && decidedAt >= start && decidedAt < end
                join s in FilterSamples(db.Samples.AsNoTracking().Where(x => x.OriginSampleId == null), q) on so.SampleId equals s.Id
                select new { s.Id, s.ControlNumber, s.Category, s.OosGroupCode, Decision = so.ApprovalDecision!.Value, At = decidedAt!.Value })
            .ToListAsync(ct);

        return rows.GroupBy(r => r.Id)
            .Select(g =>
            {
                var first = g.First();
                return new DecidedSample(g.Key, first.ControlNumber, first.Category, g.Max(x => x.At),
                    g.Any(x => x.Decision == ApprovalDecision.RetestRetainedSample), first.OosGroupCode != null, first.OosGroupCode);
            })
            .OrderBy(d => d.DecidedAt).ThenBy(d => d.SampleId)
            .ToList();
    }

    public static async Task<(KpiCore Core, List<KpiRecordDto> Records)> DecisionRateAsync(IMicroLimsDbContext db, KpiQuery q,
        Func<DecidedSample, bool> inNumerator, Func<DecidedSample, string?> note, CancellationToken ct)
    {
        var decided = await LoadDecidedSamplesAsync(db, q, ct);
        if (decided.Count == 0) return (KpiCore.Nothing(), new List<KpiRecordDto>());

        var num = decided.Count(inNumerator);
        var core = new KpiCore(Rate(num, decided.Count), num, decided.Count, decided.Count, KpiCore.NoGaps,
            MonthlySeries(decided.Select(d => (d.DecidedAt, inNumerator(d) ? 1m : 0m, 1m))), KpiCore.NoExtras, KpiCore.NoFlags);
        var records = decided.Select(d => new KpiRecordDto(d.SampleId, null, d.ControlNumber, null, d.Category.ToString(), null,
            d.DecidedAt, null, null, inNumerator(d) ? "Numerator" : "Denominator", note(d))).ToList();
        return (core, records);
    }
```

- [ ] **Step 4: Implement the two KPIs**

Create `backend/MicroLIMS.Application/Services/Kpi/RetestRateKpi.cs`:
```csharp
using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Services.Kpi;

// Tile 4. A lab-quality signal, not an analyst measure.
public sealed class RetestRateKpi : IKpi
{
    private readonly IMicroLimsDbContext _db;
    public RetestRateKpi(IMicroLimsDbContext db) => _db = db;

    public string Key => "retest-rate";
    public string Name => "Retest rate";
    public string Unit => "%";
    public KpiDirection Direction => KpiDirection.LowerIsBetter;
    public KpiGroup Group => KpiGroup.Headline;
    public int Order => 4;
    public string Definition => "Samples with a 'retest retained sample' decision, divided by all samples that received an approval decision in the period. It is a subset of the OOS rate. It describes the laboratory, not an individual analyst.";
    public string ClockEvents => "The decision time is when the section sign-off decision was electronically signed.";
    public string Population => "Distinct samples with at least one decided section sign-off (approve, reject, retest, new sample) in the period, within your sections. Retest samples created from an earlier sample are not counted again.";
    public string Source => "SampleSectionSignoff, Sample";

    public Task<KpiResult> ComputeAsync(KpiQuery q, CancellationToken ct) =>
        KpiScope.ComputeAsync(q, Unit, async (qq, c) => (await LoadAsync(qq, c)).Core, ct);

    public async Task<KpiRecordPageDto> GetRecordsAsync(KpiQuery q, int page, int pageSize, CancellationToken ct) =>
        KpiScope.Page(Key, (await LoadAsync(q, ct)).Records, page, pageSize);

    private Task<(KpiCore Core, List<KpiRecordDto> Records)> LoadAsync(KpiQuery q, CancellationToken ct) =>
        KpiScope.DecisionRateAsync(_db, q, d => d.Retest, d => d.Retest ? "Decision: retest of retained sample" : null, ct);
}
```

Create `backend/MicroLIMS.Application/Services/Kpi/OosRateKpi.cs`:
```csharp
using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Services.Kpi;

// Tile 5. Nothing writes the OOSInvestigation decision, so the OOS definition used by OosTrackingService
// (the sample carries an OosGroupCode) is used instead.
public sealed class OosRateKpi : IKpi
{
    private readonly IMicroLimsDbContext _db;
    public OosRateKpi(IMicroLimsDbContext db) => _db = db;

    public string Key => "oos-rate";
    public string Name => "OOS rate";
    public string Unit => "%";
    public KpiDirection Direction => KpiDirection.LowerIsBetter;
    public KpiGroup Group => KpiGroup.Headline;
    public int Order => 5;
    public string Definition => "Samples that belong to an out-of-specification group, divided by all samples that received an approval decision in the period. The retest rate is a subset of this figure. This is a laboratory quality signal, not an analyst penalty.";
    public string ClockEvents => "The decision time is when the section sign-off decision was electronically signed.";
    public string Population => "Distinct samples with at least one decided section sign-off in the period, within your sections. Retest samples created from an earlier sample are not counted again, so one OOS event counts once. The OOS group is recorded on the sample, so a sample whose OOS arose in another laboratory is still counted.";
    public string Source => "SampleSectionSignoff, Sample (OosGroupCode)";

    public Task<KpiResult> ComputeAsync(KpiQuery q, CancellationToken ct) =>
        KpiScope.ComputeAsync(q, Unit, async (qq, c) => (await LoadAsync(qq, c)).Core, ct);

    public async Task<KpiRecordPageDto> GetRecordsAsync(KpiQuery q, int page, int pageSize, CancellationToken ct) =>
        KpiScope.Page(Key, (await LoadAsync(q, ct)).Records, page, pageSize);

    // ponytail: OosGroupCode is sample-level, so a lab-1-only viewer still sees a lab-2 OOS; scope it per section if that matters.
    private Task<(KpiCore Core, List<KpiRecordDto> Records)> LoadAsync(KpiQuery q, CancellationToken ct) =>
        KpiScope.DecisionRateAsync(_db, q, d => d.Oos, d => d.OosGroupCode, ct);
}
```

- [ ] **Step 5: Run, expect pass**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~DecisionRateKpiTests"
```
Expected: all PASS. The tuple assertion `Assert.Equal((2m, 7m), (retest.Numerator!.Value, retest.Denominator!.Value))` compares `(decimal, decimal)` with `(decimal, decimal)` and is fine.

- [ ] **Step 6: Commit**

```
git add backend/MicroLIMS.Application/Services/Kpi/KpiScope.cs backend/MicroLIMS.Application/Services/Kpi/RetestRateKpi.cs backend/MicroLIMS.Application/Services/Kpi/OosRateKpi.cs backend/MicroLIMS.Tests/UnitTests/Kpi/KpiTileTests.cs
git commit -m "feat(kpi): retest rate and OOS rate tiles from sign-off decisions

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Tile 6 WIP backlog and backlog aging (`wip-backlog`, `backlog-aging`)

**Files:**
- Create: `backend/MicroLIMS.Application/Services/Kpi/WipBacklogKpi.cs`
- Create: `backend/MicroLIMS.Application/Services/Kpi/BacklogAgingKpi.cs`
- Modify (append classes): `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiTileTests.cs`

**Interfaces:**
- Consumes: Task 1 helpers; Task 3 `AnalystStageTarget` is NOT used here (aging uses a single test's target, see below); S1 `WorkflowProfileService.LoadResolverAsync`; `SectionReviewQueues.Candidates(IQueryable<Sample>, SectionSignoffStatus, IReadOnlyCollection<int>?)`.
- Produces:
  - `WipBacklogKpi : IKpi`, ctor `(IMicroLimsDbContext db)`; key `wip-backlog`; unit `tests`; `Neutral`; `Headline`; order 6. Count KPI (`Value` = open tests, `Ok` with 0 when empty), series = one point per `WorkflowStep` (Waiting, Running, Incubating). No prior (a snapshot cannot be reconstructed for a past date).
  - `BacklogAgingKpi : IKpi`, ctor `(IMicroLimsDbContext db, WorkflowProfileService profiles)`; key `backlog-aging`; unit `tests`; `Neutral`; `Flow`; order 21. Series: twelve points (stage x bucket) with `Group` = stage (`Testing`, `Incubation`, `In Review`, `In Approval`) and `Label` = `within50` | `50to100` | `over100`. Gap `DEFAULT_TARGET`.

Rules: open = `Pending` or `InProgress`, not superseded, as of `query.Now` (the period does not apply). Testing/Incubation age is `Now - anchor` where anchor = `SamplePreparation.PreparedAt`, falling back to `Sample.ReceivedAt`; the target is that test's own `TargetAnalystDays` resolved at `Now`, else 7 days (counted as a gap). In Review / In Approval are sample-level gates taken from `SectionReviewQueues.Candidates`: anchor = earliest in-scope sign-off `SubmittedForReviewAt` (review) or `ReviewedAt` (approval), else `ReceivedAt`; target = 1 day (the 24 h SLA). Bucket = ratio `age / target`: `<= 0.5` within50, `<= 1.0` 50to100, otherwise over100.

- [ ] **Step 1: Write the failing tests**

Append to `KpiTileTests.cs`:
```csharp
public class BacklogKpiTests
{
    private static void Open(MicroLimsDbContext db, int id, string code, ApprovalStatus status, WorkflowStep step,
        DateTime received, DateTime? prepared, bool superseded = false, int sectionId = 1)
    {
        var sample = new Sample
        {
            Id = id, ControlNumber = $"CN-{id}", ReferenceNumber = $"REF-{id}", Category = SampleCategory.FinishedProduct,
            ReceivedAt = received, Status = SampleStatus.InTesting
        };
        db.Samples.Add(sample);
        Test(db, sample, id * 10, code, sectionId, status, step, 1, superseded);
        if (prepared.HasValue) Prepared(db, id, prepared.Value);
    }

    private static void Gate(MicroLimsDbContext db, int id, SampleStatus sampleStatus, SectionSignoffStatus signoffStatus,
        DateTime submitted, DateTime? reviewed)
    {
        var sample = new Sample
        {
            Id = id, ControlNumber = $"CN-{id}", ReferenceNumber = $"REF-{id}", Category = SampleCategory.FinishedProduct,
            ReceivedAt = Utc(10, 1), Status = sampleStatus
        };
        db.Samples.Add(sample);
        Test(db, sample, id * 10, "TAMC", 1, ApprovalStatus.ResultEntered, WorkflowStep.Ready);
        Signoff(db, id, 1, submitted, reviewed, null, signoffStatus, null);
    }

    // Now = 15 Oct 12:00. TAMC target 4 days (profile); HPLC1 has no target (7-day fallback).
    // Open: 1 (1 d), 2 (2 d), 3 incubating (4 d), 4 HPLC1 (14 d), 5 unprepared, received 3 d ago.
    // Ignored: 6 superseded, 7 ResultEntered, 8 Approved.
    // Gates: 9 under review for 12 h, 10 under approval for 3 days.
    private static void Seed(MicroLimsDbContext db)
    {
        Definition(db, "TAMC", WorkflowType.CountTest);
        Definition(db, "HPLC1", WorkflowType.HplcMethodAssay);
        Profile(db, WorkflowType.CountTest, null, 2m, 4m, new DateOnly(2026, 1, 1));
        Open(db, 1, "TAMC", ApprovalStatus.Pending, WorkflowStep.Waiting, Utc(10, 14), Utc(10, 14, 12));
        Open(db, 2, "TAMC", ApprovalStatus.InProgress, WorkflowStep.Running, Utc(10, 13), Utc(10, 13, 12));
        Open(db, 3, "TAMC", ApprovalStatus.InProgress, WorkflowStep.Incubating, Utc(10, 11), Utc(10, 11, 12));
        Open(db, 4, "HPLC1", ApprovalStatus.Pending, WorkflowStep.Waiting, Utc(10, 1), Utc(10, 1, 12));
        Open(db, 5, "TAMC", ApprovalStatus.Pending, WorkflowStep.Waiting, Utc(10, 12, 12), null);
        Open(db, 6, "TAMC", ApprovalStatus.InProgress, WorkflowStep.Running, Utc(10, 1), Utc(10, 1), superseded: true);
        Open(db, 7, "TAMC", ApprovalStatus.ResultEntered, WorkflowStep.Ready, Utc(10, 1), Utc(10, 1));
        Open(db, 8, "TAMC", ApprovalStatus.Approved, WorkflowStep.Approved, Utc(10, 1), Utc(10, 1));
        Gate(db, 9, SampleStatus.UnderReview, SectionSignoffStatus.UnderReview, Utc(10, 15), null);
        Gate(db, 10, SampleStatus.UnderApproval, SectionSignoffStatus.UnderApproval, Utc(10, 11), Utc(10, 12, 12));
    }

    private static BacklogAgingKpi Aging(MicroLimsDbContext db) =>
        new(db, TestServiceFactory.WorkflowProfile(db, new FixedTimeProvider(new DateTimeOffset(Now))));

    [Fact]
    public async Task WipBacklog_CountsPendingAndInProgress_NotSuperseded()
    {
        await using var db = NewDb();
        Seed(db);
        await db.SaveChangesAsync();

        var r = await new WipBacklogKpi(db).ComputeAsync(Query(), default);

        Assert.Equal(5m, r.Value);
        Assert.Equal(5, r.N);
        Assert.Equal(KpiStatus.Ok, r.Status);
        Assert.False(r.TooFewRecords);
        Assert.Equal(new[] { "Waiting", "Running", "Incubating" }, r.Series.Select(p => p.Label));
        Assert.Equal(new decimal?[] { 3m, 1m, 1m }, r.Series.Select(p => p.Value));
    }

    [Fact]
    public async Task WipBacklog_HasNoPrior_AndEmptyIsZeroNotNoData()
    {
        await using var db = NewDb();
        Seed(db);
        await db.SaveChangesAsync();
        var kpi = new WipBacklogKpi(db);

        Assert.Null((await kpi.ComputeAsync(Query(), default)).Prior);

        var empty = await kpi.ComputeAsync(Query(sections: new[] { 2 }), default);       // a section with no open work
        Assert.Equal(KpiStatus.Ok, empty.Status);
        Assert.Equal(0m, empty.Value);
        Assert.Empty(empty.Series);
    }

    [Fact]
    public async Task BacklogAging_BucketsRelativeToOwnTarget_BoundariesInclusive()
    {
        await using var db = NewDb();
        Seed(db);
        await db.SaveChangesAsync();

        var r = await Aging(db).ComputeAsync(Query(), default);

        var cells = r.Series.ToDictionary(p => $"{p.Group}/{p.Label}", p => p.Value);
        Assert.Equal(12, cells.Count);                                  // every stage x bucket is present, zero included
        Assert.Equal(2m, cells["Testing/within50"]);                    // 1 d of 4 = 0.25; 2 d of 4 = 0.50 (boundary stays in)
        Assert.Equal(1m, cells["Testing/50to100"]);                     // unprepared sample: 3 d since receipt, 0.75
        Assert.Equal(1m, cells["Testing/over100"]);                     // HPLC1: 14 d of the 7-day fallback = 2.0
        Assert.Equal(1m, cells["Incubation/50to100"]);                  // 4 d of 4 = 1.00 (boundary stays in)
        Assert.Equal(1m, cells["In Review/within50"]);                  // 12 h of 24 h
        Assert.Equal(1m, cells["In Approval/over100"]);                 // 3 days of 24 h
        Assert.Equal(7m, r.Value);
        Assert.Equal(7, r.N);
        Assert.Null(r.Prior);
        Assert.Equal(KpiStatus.Partial, r.Status);
        var gap = Assert.Single(r.Gaps);
        Assert.Equal(("DEFAULT_TARGET", 1), (gap.Code, gap.Count));     // only the HPLC1 test had no target
    }

    [Fact]
    public async Task BacklogAging_Empty_IsOkWithZeroCells()
    {
        await using var db = NewDb();
        var r = await Aging(db).ComputeAsync(Query(), default);
        Assert.Equal(KpiStatus.Ok, r.Status);
        Assert.Equal(0m, r.Value);
        Assert.Equal(12, r.Series.Count);
        Assert.All(r.Series, p => Assert.Equal(0m, p.Value));
    }

    [Fact]
    public async Task BacklogAging_Records_NameTheBucket()
    {
        await using var db = NewDb();
        Seed(db);
        await db.SaveChangesAsync();

        var page = await Aging(db).GetRecordsAsync(Query(), 1, 50, default);

        Assert.Equal(7, page.Total);
        var hplc = Assert.Single(page.Items, i => i.TestCode == "HPLC1");
        Assert.Equal("Testing: over100", hplc.Note);
        Assert.Equal(2m, hplc.Value);
        Assert.Equal("x target", hplc.Unit);
    }
}
```

- [ ] **Step 2: Run, expect a compile failure**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~BacklogKpiTests"
```
Expected: `'WipBacklogKpi' could not be found`.

- [ ] **Step 3: Implement the WIP tile**

Create `backend/MicroLIMS.Application/Services/Kpi/WipBacklogKpi.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Kpi;

// Tile 6. A snapshot at query.Now: the period does not apply and there is no prior (history is not reconstructable).
// Tests waiting for review or approval are sample-level and appear in backlog-aging, not here.
public sealed class WipBacklogKpi : IKpi
{
    private readonly IMicroLimsDbContext _db;
    public WipBacklogKpi(IMicroLimsDbContext db) => _db = db;

    public string Key => "wip-backlog";
    public string Name => "WIP backlog";
    public string Unit => "tests";
    public KpiDirection Direction => KpiDirection.Neutral;
    public KpiGroup Group => KpiGroup.Headline;
    public int Order => 6;
    public string Definition => "Tests that are open right now: waiting, running or incubating. Tests that have a result and wait for review or approval are shown in the backlog aging section instead.";
    public string ClockEvents => "A snapshot at the time of the request; the chosen period does not apply.";
    public string Population => "Test orders with status Pending or InProgress that are not superseded, within your sections and the chosen filters.";
    public string Source => "TestOrder";

    public Task<KpiResult> ComputeAsync(KpiQuery q, CancellationToken ct) =>
        KpiScope.ComputeAsync(q, Unit, async (qq, c) => (await LoadAsync(qq, c)).Core, ct, withPrior: false);

    public async Task<KpiRecordPageDto> GetRecordsAsync(KpiQuery q, int page, int pageSize, CancellationToken ct) =>
        KpiScope.Page(Key, (await LoadAsync(q, ct)).Records, page, pageSize);

    private async Task<(KpiCore Core, List<KpiRecordDto> Records)> LoadAsync(KpiQuery q, CancellationToken ct)
    {
        var tests = await KpiScope.FilterOrders(_db.TestOrders.AsNoTracking()
                .Where(t => !t.IsSuperseded && (t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress)), q)
            .Select(t => new { t.Id, t.SampleId, t.TestCode, t.CurrentStep, t.AssignedAnalystId, t.Sample!.ControlNumber, t.Sample.Category })
            .ToListAsync(ct);
        var names = await KpiScope.UserNamesAsync(_db, tests.Select(t => t.AssignedAnalystId), ct);

        var series = tests.GroupBy(t => t.CurrentStep).OrderBy(g => g.Key)
            .Select(g => new KpiSeriesPoint(g.Key.ToString(), null, null, g.Count(), g.Count())).ToList();
        var core = new KpiCore(tests.Count, null, null, tests.Count, KpiCore.NoGaps, series, KpiCore.NoExtras, KpiCore.NoFlags, IsCount: true);
        var records = tests.OrderBy(t => t.SampleId).ThenBy(t => t.Id)
            .Select(t => new KpiRecordDto(t.SampleId, t.Id, t.ControlNumber, t.TestCode, t.Category.ToString(),
                KpiScope.AnalystLabel(q, t.AssignedAnalystId, names), null, null, null, "Denominator", t.CurrentStep.ToString()))
            .ToList();
        return (core, records);
    }
}
```

- [ ] **Step 4: Implement backlog aging**

Create `backend/MicroLIMS.Application/Services/Kpi/BacklogAgingKpi.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Kpi;

// Spec 4.4: age is shown relative to each item's own target, because targets differ by workflow.
public sealed class BacklogAgingKpi : IKpi
{
    private static readonly string[] Stages = { "Testing", "Incubation", "In Review", "In Approval" };
    private static readonly string[] Buckets = { "within50", "50to100", "over100" };

    // Review and approval are held to the 24 h SLA.
    private const decimal GateTargetDays = 1m;

    private readonly IMicroLimsDbContext _db;
    private readonly WorkflowProfileService _profiles;

    public BacklogAgingKpi(IMicroLimsDbContext db, WorkflowProfileService profiles)
    {
        _db = db;
        _profiles = profiles;
    }

    public string Key => "backlog-aging";
    public string Name => "Backlog aging";
    public string Unit => "tests";
    public KpiDirection Direction => KpiDirection.Neutral;
    public KpiGroup Group => KpiGroup.Flow;
    public int Order => 21;
    public string Definition => "Open work grouped by how much of its own target it has used: within 50%, 50 to 100%, or over 100%. Targets differ by workflow, so age in days alone would mislead.";
    public string ClockEvents => "Testing and incubation: now minus the preparation time (the receipt time if there is none), against the test's analyst-stage target (7 days where none is configured). In review: now minus submit-for-review, against 24 h. In approval: now minus review completed, against 24 h.";
    public string Population => "Open tests (waiting, running, incubating) and the samples currently waiting for review or approval in your sections. A snapshot at the time of the request.";
    public string Source => "TestOrder, SamplePreparation, Sample, SampleSectionSignoff, workflow profile versions";

    public Task<KpiResult> ComputeAsync(KpiQuery q, CancellationToken ct) =>
        KpiScope.ComputeAsync(q, Unit, async (qq, c) => (await LoadAsync(qq, c)).Core, ct, withPrior: false);

    public async Task<KpiRecordPageDto> GetRecordsAsync(KpiQuery q, int page, int pageSize, CancellationToken ct) =>
        KpiScope.Page(Key, (await LoadAsync(q, ct)).Records, page, pageSize);

    private static decimal Ratio(TimeSpan age, decimal targetDays) => Math.Max(0m, (decimal)age.TotalDays) / targetDays;

    private static string Bucket(decimal ratio) => ratio <= 0.5m ? "within50" : ratio <= 1m ? "50to100" : "over100";

    private async Task<(KpiCore Core, List<KpiRecordDto> Records)> LoadAsync(KpiQuery q, CancellationToken ct)
    {
        var resolver = await _profiles.LoadResolverAsync(ct);
        var items = new List<(string Stage, string Bucket, KpiRecordDto Record)>();
        var defaultTests = 0;

        var tests = await KpiScope.FilterOrders(_db.TestOrders.AsNoTracking()
                .Where(t => !t.IsSuperseded && (t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress)), q)
            .Select(t => new { t.Id, t.SampleId, t.TestCode, t.CurrentStep, t.AssignedAnalystId, t.Sample!.ControlNumber, t.Sample.Category, t.Sample.ReceivedAt })
            .ToListAsync(ct);
        var sampleIds = tests.Select(t => t.SampleId).Distinct().ToList();
        var prepared = await _db.SamplePreparations.AsNoTracking().Where(p => sampleIds.Contains(p.SampleId))
            .ToDictionaryAsync(p => p.SampleId, p => p.PreparedAt, ct);
        var names = await KpiScope.UserNamesAsync(_db, tests.Select(t => t.AssignedAnalystId), ct);

        foreach (var t in tests.OrderBy(t => t.Id))
        {
            var anchor = prepared.TryGetValue(t.SampleId, out var preparedAt) ? preparedAt : t.ReceivedAt;
            var target = resolver.Resolve(t.TestCode, q.Now).TargetAnalystDays;
            if (target is null) defaultTests++;
            var ratio = Ratio(q.Now - anchor, target ?? KpiConstants.DefaultTargetDays);
            var stage = t.CurrentStep == WorkflowStep.Incubating ? "Incubation" : "Testing";
            var bucket = Bucket(ratio);
            items.Add((stage, bucket, new KpiRecordDto(t.SampleId, t.Id, t.ControlNumber, t.TestCode, t.Category.ToString(),
                KpiScope.AnalystLabel(q, t.AssignedAnalystId, names), anchor, Math.Round(ratio, 2), "x target", "Denominator", $"{stage}: {bucket}")));
        }

        await AddGateAsync(q, SectionSignoffStatus.UnderReview, "In Review", items, ct);
        await AddGateAsync(q, SectionSignoffStatus.UnderApproval, "In Approval", items, ct);

        var series = new List<KpiSeriesPoint>();
        foreach (var stage in Stages)
            foreach (var bucket in Buckets)
            {
                var count = items.Count(i => i.Stage == stage && i.Bucket == bucket);
                series.Add(new KpiSeriesPoint(bucket, stage, null, count, count));
            }

        var gaps = KpiScope.Gaps(("DEFAULT_TARGET", $"{defaultTests} tests on the default 7-day target", defaultTests));
        var core = new KpiCore(items.Count, null, null, items.Count, gaps, series, KpiCore.NoExtras, KpiCore.NoFlags, IsCount: true);
        return (core, items.Select(i => i.Record).ToList());
    }

    // Sample-level gates reuse the review/approval queue definition so this view and the queues agree.
    private async Task AddGateAsync(KpiQuery q, SectionSignoffStatus status, string stage,
        List<(string Stage, string Bucket, KpiRecordDto Record)> items, CancellationToken ct)
    {
        var samples = await SectionReviewQueues.Candidates(KpiScope.FilterSamples(_db.Samples.AsNoTracking(), q), status, q.SectionIds)
            .Select(s => new { s.Id, s.ControlNumber, s.Category, s.ReceivedAt })
            .ToListAsync(ct);
        if (samples.Count == 0) return;

        var ids = samples.Select(s => s.Id).ToList();
        var rows = (await _db.SampleSectionSignoffs.AsNoTracking().Where(KpiScope.SignoffIn(q.SectionIds))
                .Where(x => ids.Contains(x.SampleId) && x.Status == status)
                .Select(x => new { x.SampleId, x.SubmittedForReviewAt, x.ReviewedAt })
                .ToListAsync(ct))
            .ToLookup(x => x.SampleId);

        foreach (var s in samples.OrderBy(s => s.Id))
        {
            var anchors = rows[s.Id]
                .Select(x => status == SectionSignoffStatus.UnderReview ? x.SubmittedForReviewAt : x.ReviewedAt)
                .Where(a => a.HasValue).Select(a => a!.Value).ToList();
            var anchor = anchors.Count > 0 ? anchors.Min() : s.ReceivedAt;
            var ratio = Ratio(q.Now - anchor, GateTargetDays);
            var bucket = Bucket(ratio);
            items.Add((stage, bucket, new KpiRecordDto(s.Id, null, s.ControlNumber, null, s.Category.ToString(), null,
                anchor, Math.Round(ratio, 2), "x target", "Denominator", $"{stage}: {bucket}")));
        }
    }
}
```

- [ ] **Step 5: Run, expect pass**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~BacklogKpiTests"
```
Expected: all PASS. If a gate sample is missing, check `SectionReviewQueues.Candidates` semantics (a sign-off row with the status, or the sample's own status with a test that has no sign-off row); the fixture satisfies the first form.

- [ ] **Step 6: Commit**

```
git add backend/MicroLIMS.Application/Services/Kpi/WipBacklogKpi.cs backend/MicroLIMS.Application/Services/Kpi/BacklogAgingKpi.cs backend/MicroLIMS.Tests/UnitTests/Kpi/KpiTileTests.cs
git commit -m "feat(kpi): WIP backlog tile and backlog aging relative to own target

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Tiles 7 and 8 - Incubation reads on time, EM excursion rate

**Files:**
- Create: `backend/MicroLIMS.Application/Services/Kpi/IncubationReadsOnTimeKpi.cs`
- Create: `backend/MicroLIMS.Application/Services/Kpi/EmExcursionRateKpi.cs`
- Modify (append classes): `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiTileTests.cs`

**Interfaces:**
- Consumes: Task 1 helpers incl. `KpiScope.IncubationIn`, `ResultIn`; `KpiConstants.IncubationGraceHours/EmAlertRateAdverse/EmActionRateAdverse/MinSampleForRate`; `ResultRecord` (`Category`, `SubjectName`, `TestCode`, `ResultLevel`, `ResultEnteredAt`, `NumericValue`, `Unit`, `ControlNumber`, `SampleId`, `TestOrderId`, `TestOrder`).
- Produces:
  - `IncubationReadsOnTimeKpi : IKpi`, ctor `(IMicroLimsDbContext db)`; key `incubation-reads-on-time`; `%`; `HigherIsBetter`; `Headline`; order 7. Gap `NO_EXPECTED_READING`.
  - `EmExcursionRateKpi : IKpi`, ctor `(IMicroLimsDbContext db)`; key `em-excursion-rate`; `%`; `LowerIsBetter`; `Headline`; order 8. `Extras["alertRate"]`, `Extras["actionRate"]` (fractions of all leveled results); flag `"AdverseTrend"`; series: monthly excursion rate with `Secondary` = that month's action rate.

Rules: tile 7 (deviation 8) - incubation windows with `CompletedAt` in the period and a `TestOrderId` (media-challenge incubations are excluded); on time = `CompletedAt <= ExpectedReadingAt + IncubationGraceHours`; windows without `ExpectedReadingAt` are excluded and reported; stage 1 to stage 2 transfers count as reads (documented limitation). Tile 8 (deviation 9) - `ResultRecord` rows of category EnvironmentalMonitoring with level `WithinLimit`, `AlertLevel` or `ActionLevel` entered in the period, excluding rows of retest clone samples (`OriginSampleId != null`, whose locations are re-cloned); `Value` = (alert + action) / all; `alertRate` counts `AlertLevel` only and `actionRate` `ActionLevel` only; `AdverseTrend` when `N >= MinSampleForRate` and (`alertRate > 2.5%` or `actionRate > 0.15%`, strictly above); `Location` filters `ResultRecord.SubjectName`.

- [ ] **Step 1: Write the failing tests**

Append to `KpiTileTests.cs`:
```csharp
public class IncubationReadsOnTimeKpiTests
{
    private static void Inc(MicroLimsDbContext db, int id, int? orderId, DateTime? completed, DateTime? expected) =>
        db.Incubations.Add(new Incubation
        {
            Id = id, TestOrderId = orderId, StepNumber = 1, StepName = "Incubation",
            StartedAt = Utc(10, 1), CompletedAt = completed, ExpectedReadingAt = expected
        });

    // Tests 10-70 come from the samples; test 70 belongs to lab 2.
    private static void Seed(MicroLimsDbContext db)
    {
        for (var i = 1; i <= 6; i++) ApprovedSample(db, i, Utc(10, 1), Utc(10, 9));
        ApprovedSample(db, 7, Utc(10, 1), Utc(10, 9), sectionId: 2);
        Inc(db, 1, 10, Utc(10, 5, 10), Utc(10, 5, 10));          // exactly on the expected time: on time
        Inc(db, 2, 20, Utc(10, 5, 14), Utc(10, 5, 10));          // exactly the 4 h grace: on time
        Inc(db, 3, 30, Utc(10, 5, 14, 1), Utc(10, 5, 10));       // one minute past the grace: late
        Inc(db, 4, 40, Utc(10, 6), null);                         // no expected time: reported, not judged
        Inc(db, 5, null, Utc(10, 6), Utc(10, 6));                 // media-challenge incubation: not an analyst reading
        Inc(db, 6, 60, null, Utc(10, 6));                         // still running: not completed yet
        Inc(db, 7, 70, Utc(10, 7), Utc(10, 7));                   // lab 2, on time
        Inc(db, 8, 10, Utc(11, 1), Utc(11, 1));                   // completed exactly at the exclusive period end
    }

    [Fact]
    public async Task IncubationReads_GraceBoundary_AndExclusions()
    {
        await using var db = NewDb();
        Seed(db);
        await db.SaveChangesAsync();

        var r = await new IncubationReadsOnTimeKpi(db).ComputeAsync(Query(), default);

        Assert.Equal(3m, r.Numerator);
        Assert.Equal(4m, r.Denominator);
        Assert.Equal(0.75m, r.Value);
        Assert.Equal(4, r.N);
        Assert.True(r.TooFewRecords);
        Assert.Equal(KpiStatus.Partial, r.Status);
        Assert.Equal(1, r.Gaps.Single(g => g.Code == "NO_EXPECTED_READING").Count);
        Assert.Null(r.Prior);
    }

    [Fact]
    public async Task IncubationReads_SectionScope_AndEmpty()
    {
        await using var db = NewDb();
        Seed(db);
        await db.SaveChangesAsync();
        var kpi = new IncubationReadsOnTimeKpi(db);

        var lab1 = await kpi.ComputeAsync(Query(sections: new[] { 1 }), default);
        Assert.Equal(0.6667m, lab1.Value);                   // 2 of 3
        var none = await kpi.ComputeAsync(Query(sections: Array.Empty<int>()), default);
        Assert.Equal(KpiStatus.NoData, none.Status);
        Assert.Null(none.Value);
    }

    [Fact]
    public async Task IncubationReads_Records_ListEveryJudgedWindow()
    {
        await using var db = NewDb();
        Seed(db);
        await db.SaveChangesAsync();

        var page = await new IncubationReadsOnTimeKpi(db).GetRecordsAsync(Query(), 1, 50, default);

        Assert.Equal(5, page.Total);                          // 4 judged + the one without an expected time
        Assert.Equal(3, page.Items.Count(i => i.Inclusion == "Numerator"));
        Assert.Equal(1, page.Items.Count(i => i.Inclusion == "Denominator"));
        Assert.Equal(1, page.Items.Count(i => i.Inclusion == "Excluded"));
    }
}

public class EmExcursionRateKpiTests
{
    private static int _recordId;

    private static void Em(MicroLimsDbContext db, ResultLevel level, DateTime at, int sampleId = 1, int orderId = 10,
        string subject = "Room A", SampleCategory category = SampleCategory.EnvironmentalMonitoring) =>
        db.ResultRecords.Add(new ResultRecord
        {
            SampleId = sampleId, TestOrderId = orderId, Category = category, SubjectName = subject, TestCode = "EM_CP",
            ResultLevel = level, ResultEnteredAt = at, SourceTable = "SampleLocation", SourceId = Interlocked.Increment(ref _recordId),
            ReferenceNumber = "R"
        });

    private static void Many(MicroLimsDbContext db, ResultLevel level, int count, DateTime at)
    {
        for (var i = 0; i < count; i++) Em(db, level, at);
    }

    // Sample 1 (lab 1, original) and sample 2 (a retest clone of sample 1).
    private static void Samples(MicroLimsDbContext db)
    {
        ApprovedSample(db, 1, Utc(10, 1), Utc(10, 9), "EM_CP", category: SampleCategory.EnvironmentalMonitoring);
        ApprovedSample(db, 2, Utc(10, 1), Utc(10, 9), "EM_CP", category: SampleCategory.EnvironmentalMonitoring, originSampleId: 1);
    }

    [Fact]
    public async Task EmExcursion_RatesAndAdverseTrendFlag()
    {
        await using var db = NewDb();
        Samples(db);
        Many(db, ResultLevel.WithinLimit, 96, Utc(10, 5));
        Many(db, ResultLevel.AlertLevel, 3, Utc(10, 6));
        Em(db, ResultLevel.ActionLevel, Utc(10, 7));
        // Left out of both numerator and denominator:
        Many(db, ResultLevel.LimitsNotConfigured, 5, Utc(10, 5));
        Many(db, ResultLevel.NotApplicable, 5, Utc(10, 5));
        Em(db, ResultLevel.ActionLevel, Utc(10, 8), sampleId: 2, orderId: 20);                          // retest clone re-counts a location
        Em(db, ResultLevel.AlertLevel, Utc(10, 8), category: SampleCategory.Water);                      // not EM
        Em(db, ResultLevel.AlertLevel, Utc(11, 1));                                                      // exactly at the exclusive end
        Many(db, ResultLevel.WithinLimit, 20, Utc(9, 10));                                               // prior period, no excursions
        await db.SaveChangesAsync();

        var r = await new EmExcursionRateKpi(db).ComputeAsync(Query(), default);

        Assert.Equal(0.04m, r.Value);
        Assert.Equal(4m, r.Numerator);
        Assert.Equal(100m, r.Denominator);
        Assert.Equal(100, r.N);
        Assert.False(r.TooFewRecords);
        Assert.Equal(0.03m, r.Extras["alertRate"]);
        Assert.Equal(0.01m, r.Extras["actionRate"]);
        Assert.Equal(new[] { "AdverseTrend" }, r.Flags);
        var month = Assert.Single(r.Series);
        Assert.Equal(("2026-10", 100), (month.Label, month.N));
        Assert.Equal(0.04m, month.Value);
        Assert.Equal(0.01m, month.Secondary);
        Assert.Equal(0m, r.Prior!.Value);
        Assert.Equal(20, r.Prior.N);
    }

    [Fact]
    public async Task EmExcursion_RateBelowOrExactlyAtThreshold_IsNotAdverse()
    {
        await using var db = NewDb();
        Samples(db);
        Many(db, ResultLevel.WithinLimit, 195, Utc(10, 5));
        Many(db, ResultLevel.AlertLevel, 5, Utc(10, 6));          // 5 / 200 = 2.5% exactly: the threshold is "above"
        await db.SaveChangesAsync();

        var r = await new EmExcursionRateKpi(db).ComputeAsync(Query(), default);

        Assert.Equal(0.025m, r.Extras["alertRate"]);
        Assert.Empty(r.Flags);
    }

    [Fact]
    public async Task EmExcursion_FewerThanFiveResults_DoesNotFlag_AndSaysTooFew()
    {
        await using var db = NewDb();
        Samples(db);
        Many(db, ResultLevel.WithinLimit, 2, Utc(10, 5));
        Em(db, ResultLevel.AlertLevel, Utc(10, 6));
        await db.SaveChangesAsync();

        var r = await new EmExcursionRateKpi(db).ComputeAsync(Query(), default);

        Assert.Equal(0.3333m, r.Value);
        Assert.True(r.TooFewRecords);
        Assert.Empty(r.Flags);                                    // one alert in three results is not a trend
    }

    [Fact]
    public async Task EmExcursion_Location_FiltersOnSubjectName_AndOtherCategoryIsNoData()
    {
        await using var db = NewDb();
        Samples(db);
        Many(db, ResultLevel.WithinLimit, 5, Utc(10, 5));
        Em(db, ResultLevel.AlertLevel, Utc(10, 6), subject: "Room B");
        await db.SaveChangesAsync();
        var kpi = new EmExcursionRateKpi(db);

        Assert.Equal(0m, (await kpi.ComputeAsync(Query(location: "Room A"), default)).Value);
        Assert.Equal(1m, (await kpi.ComputeAsync(Query(location: "Room B"), default)).Value);
        Assert.Equal(KpiStatus.NoData, (await kpi.ComputeAsync(Query(category: "Water"), default)).Status);
        Assert.Equal(KpiStatus.NoData, (await kpi.ComputeAsync(Query(sections: Array.Empty<int>()), default)).Status);
    }

    [Fact]
    public async Task EmExcursion_EmptyPeriod_IsNoData()
    {
        await using var db = NewDb();
        var r = await new EmExcursionRateKpi(db).ComputeAsync(Query(), default);
        Assert.Equal(KpiStatus.NoData, r.Status);
        Assert.Null(r.Value);
        Assert.Empty(r.Flags);
    }
}
```

- [ ] **Step 2: Run, expect a compile failure**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~IncubationReadsOnTimeKpiTests|FullyQualifiedName~EmExcursionRateKpiTests"
```
Expected: `'IncubationReadsOnTimeKpi' could not be found`.

- [ ] **Step 3: Implement tile 7**

Create `backend/MicroLIMS.Application/Services/Kpi/IncubationReadsOnTimeKpi.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Services.Kpi;

// Tile 7.
public sealed class IncubationReadsOnTimeKpi : IKpi
{
    private readonly IMicroLimsDbContext _db;
    public IncubationReadsOnTimeKpi(IMicroLimsDbContext db) => _db = db;

    public string Key => "incubation-reads-on-time";
    public string Name => "Incubation reads on time";
    public string Unit => "%";
    public KpiDirection Direction => KpiDirection.HigherIsBetter;
    public KpiGroup Group => KpiGroup.Headline;
    public int Order => 7;
    public string Definition => "Share of completed incubation windows that were read no later than the expected reading time plus a 4 hour grace.";
    public string ClockEvents => "Expected: Incubation.ExpectedReadingAt (the end of the declared incubation window). Actual: Incubation.CompletedAt. On time when actual <= expected + 4 h.";
    public string Population => "Incubation windows of tests, completed in the period, within your sections. Windows with no expected time are listed as a gap. Media-challenge incubations are excluded. A transfer from stage 1 to stage 2 also completes a window and counts as a read (known limitation).";
    public string Source => "Incubation, TestOrder";

    public Task<KpiResult> ComputeAsync(KpiQuery q, CancellationToken ct) =>
        KpiScope.ComputeAsync(q, Unit, async (qq, c) => (await LoadAsync(qq, c)).Core, ct);

    public async Task<KpiRecordPageDto> GetRecordsAsync(KpiQuery q, int page, int pageSize, CancellationToken ct) =>
        KpiScope.Page(Key, (await LoadAsync(q, ct)).Records, page, pageSize);

    private async Task<(KpiCore Core, List<KpiRecordDto> Records)> LoadAsync(KpiQuery q, CancellationToken ct)
    {
        var from = q.From;
        var to = q.To;
        var orderIds = KpiScope.FilterOrders(_db.TestOrders.AsNoTracking(), q).Select(t => (int?)t.Id);
        var rows = await _db.Incubations.AsNoTracking().Where(KpiScope.IncubationIn(q.SectionIds))
            .Where(i => i.CompletedAt != null && i.CompletedAt >= from && i.CompletedAt < to && orderIds.Contains(i.TestOrderId))
            .Select(i => new
            {
                i.Id, TestOrderId = i.TestOrderId!.Value, i.TestOrder!.SampleId, i.TestOrder.TestCode, i.StepName,
                ControlNumber = i.TestOrder.Sample!.ControlNumber, i.TestOrder.Sample.Category,
                CompletedAt = i.CompletedAt!.Value, i.ExpectedReadingAt
            })
            .ToListAsync(ct);
        if (rows.Count == 0) return (KpiCore.Nothing(), new List<KpiRecordDto>());

        int judged = 0, onTime = 0, noExpected = 0;
        var records = new List<KpiRecordDto>();
        var monthly = new List<(DateTime At, decimal Num, decimal Den)>();
        foreach (var r in rows.OrderBy(r => r.CompletedAt).ThenBy(r => r.Id))
        {
            KpiRecordDto Rec(string inclusion, string? note) =>
                new(r.SampleId, r.TestOrderId, r.ControlNumber, r.TestCode, r.Category.ToString(), null, r.CompletedAt, null, null, inclusion, note);

            if (r.ExpectedReadingAt is null)
            {
                noExpected++;
                records.Add(Rec("Excluded", $"{r.StepName}: no expected reading time recorded"));
                continue;
            }
            var ok = r.CompletedAt <= r.ExpectedReadingAt.Value.AddHours(KpiConstants.IncubationGraceHours);
            judged++;
            if (ok) onTime++;
            monthly.Add((r.CompletedAt, ok ? 1m : 0m, 1m));
            records.Add(Rec(ok ? "Numerator" : "Denominator", ok ? null : $"{r.StepName}: read after the grace period"));
        }

        var gaps = KpiScope.Gaps(("NO_EXPECTED_READING", $"{noExpected} incubation windows without an expected reading time", noExpected));
        if (judged == 0) return (KpiCore.Nothing(gaps), records);
        return (new KpiCore(KpiScope.Rate(onTime, judged), onTime, judged, judged, gaps, KpiScope.MonthlySeries(monthly), KpiCore.NoExtras, KpiCore.NoFlags), records);
    }
}
```

- [ ] **Step 4: Implement tile 8**

Create `backend/MicroLIMS.Application/Services/Kpi/EmExcursionRateKpi.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Kpi;

// Tile 8. The adverse-trend thresholds are constants (KpiConstants), not stored configuration.
// ponytail: make them per-section settings if a site needs different limits.
public sealed class EmExcursionRateKpi : IKpi
{
    private static readonly ResultLevel[] Leveled = { ResultLevel.WithinLimit, ResultLevel.AlertLevel, ResultLevel.ActionLevel };

    private readonly IMicroLimsDbContext _db;
    public EmExcursionRateKpi(IMicroLimsDbContext db) => _db = db;

    public string Key => "em-excursion-rate";
    public string Name => "EM excursion rate";
    public string Unit => "%";
    public KpiDirection Direction => KpiDirection.LowerIsBetter;
    public KpiGroup Group => KpiGroup.Headline;
    public int Order => 8;
    public string Definition => "Environmental monitoring results at alert or action level, divided by all environmental monitoring results that have a level. The alert-level and action-level rates are also shown separately. A trend flag appears when the alert-level rate is above 2.5% or the action-level rate is above 0.15%, with at least 5 results.";
    public string ClockEvents => "The result entry time (ResultRecord.ResultEnteredAt) places a result in the period.";
    public string Population => "Environmental monitoring results at within-limit, alert or action level in your sections. Results without a level (detected/absent, limits not configured) and results of retest samples that re-count a location are excluded. The location filter matches the monitored location (room or point).";
    public string Source => "ResultRecord";

    public Task<KpiResult> ComputeAsync(KpiQuery q, CancellationToken ct) =>
        KpiScope.ComputeAsync(q, Unit, async (qq, c) => (await LoadAsync(qq, c)).Core, ct);

    public async Task<KpiRecordPageDto> GetRecordsAsync(KpiQuery q, int page, int pageSize, CancellationToken ct) =>
        KpiScope.Page(Key, (await LoadAsync(q, ct)).Records, page, pageSize);

    private async Task<(KpiCore Core, List<KpiRecordDto> Records)> LoadAsync(KpiQuery q, CancellationToken ct)
    {
        var category = KpiScope.ParseCategory(q.Category);
        if (category.HasValue && category.Value != SampleCategory.EnvironmentalMonitoring)
            return (KpiCore.Nothing(), new List<KpiRecordDto>());

        var from = q.From;
        var to = q.To;
        var leveled = Leveled;
        var query = _db.ResultRecords.AsNoTracking().Where(KpiScope.ResultIn(q.SectionIds))
            .Where(r => r.Category == SampleCategory.EnvironmentalMonitoring && leveled.Contains(r.ResultLevel)
                && r.ResultEnteredAt >= from && r.ResultEnteredAt < to)
            .Where(r => !_db.Samples.Any(s => s.Id == r.SampleId && s.OriginSampleId != null));
        var location = q.Location;
        if (!string.IsNullOrWhiteSpace(location)) query = query.Where(r => r.SubjectName == location);
        var testCode = q.TestCode;
        if (!string.IsNullOrWhiteSpace(testCode)) query = query.Where(r => r.TestCode == testCode);
        if (q.AnalystId.HasValue)
        {
            var analystId = q.AnalystId.Value;
            query = query.Where(r => r.TestOrder!.AssignedAnalystId == analystId);
        }

        var rows = await query
            .Select(r => new { r.Id, r.SampleId, r.TestOrderId, r.ControlNumber, r.TestCode, r.SubjectName, r.ResultLevel, r.ResultEnteredAt, r.NumericValue, r.Unit })
            .ToListAsync(ct);
        if (rows.Count == 0) return (KpiCore.Nothing(), new List<KpiRecordDto>());

        var alert = rows.Count(r => r.ResultLevel == ResultLevel.AlertLevel);
        var action = rows.Count(r => r.ResultLevel == ResultLevel.ActionLevel);
        var total = rows.Count;
        var alertRate = KpiScope.Rate(alert, total);
        var actionRate = KpiScope.Rate(action, total);

        var flags = new List<string>();
        if (total >= KpiConstants.MinSampleForRate
            && (alertRate > KpiConstants.EmAlertRateAdverse || actionRate > KpiConstants.EmActionRateAdverse))
            flags.Add("AdverseTrend");

        var series = rows
            .GroupBy(r => new DateTime(r.ResultEnteredAt.Year, r.ResultEnteredAt.Month, 1, 0, 0, 0, DateTimeKind.Utc))
            .OrderBy(g => g.Key)
            .Select(g => new KpiSeriesPoint(g.Key.ToString("yyyy-MM"), null, g.Key,
                KpiScope.Rate(g.Count(r => r.ResultLevel != ResultLevel.WithinLimit), g.Count()), g.Count(),
                KpiScope.Rate(g.Count(r => r.ResultLevel == ResultLevel.ActionLevel), g.Count())))
            .ToList();

        var core = new KpiCore(KpiScope.Rate(alert + action, total), alert + action, total, total, KpiCore.NoGaps, series,
            new Dictionary<string, decimal?> { ["alertRate"] = alertRate, ["actionRate"] = actionRate }, flags);
        var records = rows.OrderBy(r => r.ResultEnteredAt).ThenBy(r => r.Id)
            .Select(r => new KpiRecordDto(r.SampleId, r.TestOrderId, r.ControlNumber, r.TestCode, SampleCategory.EnvironmentalMonitoring.ToString(), null,
                r.ResultEnteredAt, r.NumericValue, r.Unit, r.ResultLevel == ResultLevel.WithinLimit ? "Denominator" : "Numerator",
                $"{r.SubjectName}: {r.ResultLevel}"))
            .ToList();
        return (core, records);
    }
}
```

- [ ] **Step 5: Run, expect pass**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~IncubationReadsOnTimeKpiTests|FullyQualifiedName~EmExcursionRateKpiTests"
```
Expected: all PASS. If the `ResultRecord` initialiser in the test does not compile (a required property named differently), fix the object initialiser using the property names confirmed in Task 1 step 1; the assertions do not change. If `r.NumericValue`/`r.Unit` have other types, adjust only the `KpiRecordDto` projection.

- [ ] **Step 6: Commit**

```
git add backend/MicroLIMS.Application/Services/Kpi/IncubationReadsOnTimeKpi.cs backend/MicroLIMS.Application/Services/Kpi/EmExcursionRateKpi.cs backend/MicroLIMS.Tests/UnitTests/Kpi/KpiTileTests.cs
git commit -m "feat(kpi): incubation-reads-on-time and EM excursion tiles

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Supporting KPIs - Stage TAT and Returns by test

**Files:**
- Create: `backend/MicroLIMS.Application/Services/Kpi/StageTatKpi.cs`
- Create: `backend/MicroLIMS.Application/Services/Kpi/ReturnsByTestKpi.cs`
- Modify (append classes): `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiTileTests.cs`

**Interfaces:**
- Consumes: Task 1 helpers; `IMicroLimsDbContext.Incubations`, `TestReturnEvents`.
- Produces:
  - `StageTatKpi : IKpi`, ctor `(IMicroLimsDbContext db)`; key `stage-tat`; unit `days`; `LowerIsBetter`; `Flow`; order 20. A breakdown KPI: `Value` is `null` by design and the answer is `Series` (one point per stage in the fixed order Testing, Incubation, Review, Approval; `Value` = median days, `Secondary` = P90, `N` = observations). `N` = approved samples. No prior.
  - `ReturnsByTestKpi : IKpi`, ctor `(IMicroLimsDbContext db)`; key `returns-by-test`; unit `tests`; `Neutral`; `Quality`; order 30. Count KPI; series = one point per test code (alphabetical, never by person). The free-text reason appears only in records.

- [ ] **Step 1: Write the failing tests**

Append to `KpiTileTests.cs`:
```csharp
public class SupportingKpiTests
{
    private static void Inc(MicroLimsDbContext db, int id, int orderId, DateTime start, DateTime end) =>
        db.Incubations.Add(new Incubation { Id = id, TestOrderId = orderId, StepNumber = 1, StepName = "Incubation", StartedAt = start, CompletedAt = end });

    [Fact]
    public async Task StageTat_MedianAndP90_PerStage()
    {
        await using var db = NewDb();
        ApprovedSample(db, 1, Utc(10, 1), Utc(10, 4, 8));
        Timeline(db, 1, Utc(10, 1, 8), Utc(10, 3, 8), Utc(10, 3, 20), Utc(10, 4, 8));      // testing 2 d, review 0.5 d, approval 0.5 d
        Inc(db, 1, 10, Utc(10, 1, 10), Utc(10, 2, 10));                                     // incubation 1 d
        ApprovedSample(db, 2, Utc(10, 2), Utc(10, 8, 8));
        Timeline(db, 2, Utc(10, 2, 8), Utc(10, 6, 8), Utc(10, 7, 8), Utc(10, 8, 8));        // testing 4 d, review 1 d, approval 1 d
        Inc(db, 2, 20, Utc(10, 2, 10), Utc(10, 5, 10));                                     // incubation 3 d
        await db.SaveChangesAsync();

        var r = await new StageTatKpi(db).ComputeAsync(Query(), default);

        Assert.Null(r.Value);                                  // a breakdown: read the series
        Assert.Equal(KpiStatus.Ok, r.Status);
        Assert.Equal(2, r.N);
        Assert.Equal(new[] { "Testing", "Incubation", "Review", "Approval" }, r.Series.Select(p => p.Label));
        Assert.Equal(new decimal?[] { 3m, 2m, 0.75m, 0.75m }, r.Series.Select(p => p.Value));
        Assert.Equal(new decimal?[] { 3.8m, 2.8m, 0.95m, 0.95m }, r.Series.Select(p => p.Secondary));
        Assert.All(r.Series, p => Assert.Equal(2, p.N));
        Assert.Null(r.Prior);
    }

    [Fact]
    public async Task StageTat_SampleWithoutPreparation_StillContributesReviewAndApproval()
    {
        await using var db = NewDb();
        ApprovedSample(db, 1, Utc(10, 1), Utc(10, 4, 8));
        Signoff(db, 1, 1, Utc(10, 3, 8), Utc(10, 3, 20), Utc(10, 4, 8));
        await db.SaveChangesAsync();

        var r = await new StageTatKpi(db).ComputeAsync(Query(), default);

        var byStage = r.Series.ToDictionary(p => p.Label);
        Assert.Equal(0, byStage["Testing"].N);                 // no clock start, so no testing time
        Assert.Null(byStage["Testing"].Value);
        Assert.Equal(1, byStage["Review"].N);
        Assert.Equal(1, byStage["Approval"].N);
    }

    [Fact]
    public async Task StageTat_EmptyPeriod_IsNoData()
    {
        await using var db = NewDb();
        var r = await new StageTatKpi(db).ComputeAsync(Query(), default);
        Assert.Equal(KpiStatus.NoData, r.Status);
        Assert.Empty(r.Series);
    }

    [Fact]
    public async Task ReturnsByTest_GroupsByTestCode_NeverByPerson()
    {
        await using var db = NewDb();
        Analyst(db, 1, "Ann Analyst");
        ApprovedSample(db, 1, Utc(10, 1), Utc(10, 9), "TAMC");
        ApprovedSample(db, 2, Utc(10, 1), Utc(10, 9), "TAMC");
        ApprovedSample(db, 3, Utc(10, 1), Utc(10, 9), "TAMC", analyst: 7);
        ApprovedSample(db, 4, Utc(10, 1), Utc(10, 9), "HPLC1");
        Return(db, 10, Utc(10, 3), "wrong dilution factor");
        Return(db, 20, Utc(10, 4));
        Return(db, 30, Utc(10, 5));
        Return(db, 40, Utc(10, 6));
        Return(db, 10, Utc(11, 1));                              // exactly at the exclusive end
        Return(db, 10, Utc(9, 30));                              // before the period
        await db.SaveChangesAsync();
        var kpi = new ReturnsByTestKpi(db);

        var r = await kpi.ComputeAsync(Query(), default);

        Assert.Equal(4m, r.Value);
        Assert.Equal(KpiStatus.Ok, r.Status);
        Assert.Equal(new[] { "HPLC1", "TAMC" }, r.Series.Select(p => p.Label));      // alphabetical, not by count
        Assert.Equal(new decimal?[] { 1m, 3m }, r.Series.Select(p => p.Value));
        Assert.All(r.Series, p => Assert.Null(p.Group));
        Assert.Null(r.Prior);

        var records = await kpi.GetRecordsAsync(Query(), 1, 50, default);
        Assert.Equal(4, records.Total);
        Assert.Contains(records.Items, i => i.Note == "wrong dilution factor");     // the reason text lives in records only
        Assert.Equal("Ann Analyst", records.Items.First(i => i.TestOrderId == 10).AnalystName);

        var hidden = await kpi.GetRecordsAsync(Query(canViewTeam: false, viewer: 99), 1, 50, default);
        Assert.All(hidden.Items, i => Assert.Null(i.AnalystName));                  // D8
    }

    [Fact]
    public async Task ReturnsByTest_NoReturns_IsZeroNotNoData_AndScopeApplies()
    {
        await using var db = NewDb();
        ApprovedSample(db, 1, Utc(10, 1), Utc(10, 9), "TAMC", sectionId: 1);
        Return(db, 10, Utc(10, 3));
        await db.SaveChangesAsync();
        var kpi = new ReturnsByTestKpi(db);

        var other = await kpi.ComputeAsync(Query(sections: new[] { 2 }), default);
        Assert.Equal(KpiStatus.Ok, other.Status);
        Assert.Equal(0m, other.Value);
        Assert.Empty(other.Series);
        Assert.Equal(1m, (await kpi.ComputeAsync(Query(sections: new[] { 1 }), default)).Value);
    }
}
```

- [ ] **Step 2: Run, expect a compile failure**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~SupportingKpiTests"
```
Expected: `'StageTatKpi' could not be found`.

- [ ] **Step 3: Implement stage TAT**

Create `backend/MicroLIMS.Application/Services/Kpi/StageTatKpi.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Services.Kpi;

// Where the time goes. Population is tile 1's samples. Incubation happens inside the testing stage, so the
// stages overlap; they are four separate measurements, not slices that add up.
public sealed class StageTatKpi : IKpi
{
    private static readonly string[] Stages = { "Testing", "Incubation", "Review", "Approval" };

    private readonly IMicroLimsDbContext _db;
    public StageTatKpi(IMicroLimsDbContext db) => _db = db;

    public string Key => "stage-tat";
    public string Name => "Time in each stage";
    public string Unit => "days";
    public KpiDirection Direction => KpiDirection.LowerIsBetter;
    public KpiGroup Group => KpiGroup.Flow;
    public int Order => 20;
    public string Definition => "Median and 90th percentile days spent in testing, incubation, review and approval for samples approved in the period. This is a breakdown: read the stage values, there is no single headline number. Incubation runs inside testing, so the stages overlap.";
    public string ClockEvents => "Testing: preparation to the latest section submit-for-review. Incubation: start to completion of each incubation window. Review: submit to reviewed (longest section). Approval: reviewed to approved (longest section).";
    public string Population => "Samples approved in the period (as for turnaround). A stage with no recorded clock for a sample contributes nothing for that sample.";
    public string Source => "Sample, SamplePreparation, SampleSectionSignoff, Incubation";

    public Task<KpiResult> ComputeAsync(KpiQuery q, CancellationToken ct) =>
        KpiScope.ComputeAsync(q, Unit, async (qq, c) => (await LoadAsync(qq, c)).Core, ct, withPrior: false);

    public async Task<KpiRecordPageDto> GetRecordsAsync(KpiQuery q, int page, int pageSize, CancellationToken ct) =>
        KpiScope.Page(Key, (await LoadAsync(q, ct)).Records, page, pageSize);

    private async Task<(KpiCore Core, List<KpiRecordDto> Records)> LoadAsync(KpiQuery q, CancellationToken ct)
    {
        var samples = await KpiScope.ApprovedSamples(_db, q)
            .Select(s => new { s.Id, s.ControlNumber, s.Category, ApprovedAt = s.ApprovedAt!.Value })
            .ToListAsync(ct);
        if (samples.Count == 0) return (KpiCore.Nothing(), new List<KpiRecordDto>());

        var ids = samples.Select(s => s.Id).ToList();
        var prepared = await _db.SamplePreparations.AsNoTracking().Where(p => ids.Contains(p.SampleId))
            .ToDictionaryAsync(p => p.SampleId, p => p.PreparedAt, ct);
        var signoffs = (await _db.SampleSectionSignoffs.AsNoTracking().Where(KpiScope.SignoffIn(q.SectionIds))
                .Where(x => ids.Contains(x.SampleId))
                .Select(x => new { x.SampleId, x.SubmittedForReviewAt, x.ReviewedAt, x.ApprovedAt })
                .ToListAsync(ct))
            .ToLookup(x => x.SampleId);
        var incubations = await _db.Incubations.AsNoTracking().Where(KpiScope.IncubationIn(q.SectionIds))
            .Where(i => i.CompletedAt != null && ids.Contains(i.TestOrder!.SampleId))
            .Select(i => new { i.TestOrder!.SampleId, i.StartedAt, CompletedAt = i.CompletedAt!.Value })
            .ToListAsync(ct);

        var byStage = Stages.ToDictionary(s => s, _ => new List<(int SampleId, decimal Days)>());
        foreach (var s in samples)
        {
            var rows = signoffs[s.Id].ToList();
            var submitted = rows.Where(x => x.SubmittedForReviewAt != null).Select(x => x.SubmittedForReviewAt!.Value).ToList();
            if (prepared.TryGetValue(s.Id, out var preparedAt) && submitted.Count > 0)
                byStage["Testing"].Add((s.Id, KpiScope.Days(preparedAt, submitted.Max())));

            var review = rows.Where(x => x.SubmittedForReviewAt != null && x.ReviewedAt != null)
                .Select(x => KpiScope.Days(x.SubmittedForReviewAt!.Value, x.ReviewedAt!.Value)).ToList();
            if (review.Count > 0) byStage["Review"].Add((s.Id, review.Max()));

            var approval = rows.Where(x => x.ReviewedAt != null && x.ApprovedAt != null)
                .Select(x => KpiScope.Days(x.ReviewedAt!.Value, x.ApprovedAt!.Value)).ToList();
            if (approval.Count > 0) byStage["Approval"].Add((s.Id, approval.Max()));
        }
        foreach (var i in incubations)
            byStage["Incubation"].Add((i.SampleId, KpiScope.Days(i.StartedAt, i.CompletedAt)));

        var series = Stages.Select(stage =>
        {
            var days = byStage[stage].Select(x => x.Days).ToList();
            return new KpiSeriesPoint(stage, null, null, KpiScope.Percentile(days, 0.5m), days.Count, KpiScope.Percentile(days, 0.9m));
        }).ToList();

        var control = samples.ToDictionary(s => s.Id);
        var records = Stages
            .SelectMany(stage => byStage[stage].Select(x => (Stage: stage, x.SampleId, x.Days)))
            .OrderBy(x => x.SampleId).ThenBy(x => Array.IndexOf(Stages, x.Stage))
            .Select(x => new KpiRecordDto(x.SampleId, null, control[x.SampleId].ControlNumber, null, control[x.SampleId].Category.ToString(),
                null, control[x.SampleId].ApprovedAt, x.Days, "days", "Denominator", x.Stage))
            .ToList();

        // Value stays null: the answer is the per-stage series.
        return (new KpiCore(null, null, null, samples.Count, KpiCore.NoGaps, series, KpiCore.NoExtras, KpiCore.NoFlags), records);
    }
}
```

- [ ] **Step 4: Implement returns by test**

Create `backend/MicroLIMS.Application/Services/Kpi/ReturnsByTestKpi.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Services.Kpi;

// TestReturnEvent only has a free-text reason, so returns cannot be grouped by cause. They are grouped by test
// code (D6: never by person); the reason text is shown in the record list only.
public sealed class ReturnsByTestKpi : IKpi
{
    private readonly IMicroLimsDbContext _db;
    public ReturnsByTestKpi(IMicroLimsDbContext db) => _db = db;

    public string Key => "returns-by-test";
    public string Name => "Returns by test";
    public string Unit => "tests";
    public KpiDirection Direction => KpiDirection.Neutral;
    public KpiGroup Group => KpiGroup.Quality;
    public int Order => 30;
    public string Definition => "Number of times a test was returned to the analyst in the period, grouped by test code in alphabetical order. A return is a reviewer sending a result back; it is a process signal and is never grouped by person.";
    public string ClockEvents => "The time of the return (TestReturnEvent.ReturnedAt) places it in the period.";
    public string Population => "Return events for tests in your sections and the chosen filters. The reviewer's reason is shown in the record list only.";
    public string Source => "TestReturnEvent, TestOrder";

    public Task<KpiResult> ComputeAsync(KpiQuery q, CancellationToken ct) =>
        KpiScope.ComputeAsync(q, Unit, async (qq, c) => (await LoadAsync(qq, c)).Core, ct, withPrior: false);

    public async Task<KpiRecordPageDto> GetRecordsAsync(KpiQuery q, int page, int pageSize, CancellationToken ct) =>
        KpiScope.Page(Key, (await LoadAsync(q, ct)).Records, page, pageSize);

    private async Task<(KpiCore Core, List<KpiRecordDto> Records)> LoadAsync(KpiQuery q, CancellationToken ct)
    {
        var from = q.From;
        var to = q.To;
        var orderIds = KpiScope.FilterOrders(_db.TestOrders.AsNoTracking(), q).Select(t => t.Id);
        var rows = await _db.TestReturnEvents.AsNoTracking()
            .Where(e => e.ReturnedAt >= from && e.ReturnedAt < to && orderIds.Contains(e.TestOrderId))
            .Select(e => new
            {
                e.Id, e.TestOrderId, e.TestOrder!.SampleId, e.TestOrder.TestCode, e.Reason, e.ReturnedAt, e.AssignedAnalystId,
                ControlNumber = e.TestOrder.Sample!.ControlNumber, e.TestOrder.Sample.Category
            })
            .ToListAsync(ct);
        var names = await KpiScope.UserNamesAsync(_db, rows.Select(r => r.AssignedAnalystId), ct);

        var series = rows.GroupBy(r => r.TestCode).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new KpiSeriesPoint(g.Key, null, null, g.Count(), g.Count())).ToList();
        var core = new KpiCore(rows.Count, null, null, rows.Count, KpiCore.NoGaps, series, KpiCore.NoExtras, KpiCore.NoFlags, IsCount: true);
        var records = rows.OrderBy(r => r.ReturnedAt).ThenBy(r => r.Id)
            .Select(r => new KpiRecordDto(r.SampleId, r.TestOrderId, r.ControlNumber, r.TestCode, r.Category.ToString(),
                KpiScope.AnalystLabel(q, r.AssignedAnalystId, names), r.ReturnedAt, 1m, "returns", "Denominator", r.Reason))
            .ToList();
        return (core, records);
    }
}
```

- [ ] **Step 5: Run, expect pass**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~SupportingKpiTests"
```
Expected: all PASS. Then run the whole KPI unit group once to catch cross-test interference: `--filter "FullyQualifiedName~UnitTests.Kpi"` (expected: all PASS).

- [ ] **Step 6: Commit**

```
git add backend/MicroLIMS.Application/Services/Kpi/StageTatKpi.cs backend/MicroLIMS.Application/Services/Kpi/ReturnsByTestKpi.cs backend/MicroLIMS.Tests/UnitTests/Kpi/KpiTileTests.cs
git commit -m "feat(kpi): stage TAT and returns-by-test supporting KPIs

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 11: `Kpi.ViewTeam` permission, controller, DI, test factory and authorization matrix

**Files:**
- Modify: `backend/MicroLIMS.Shared/Constants/PermissionConstants.cs`
- Modify: `backend/MicroLIMS.Persistence/Seed/DbSeeder.cs`
- Create: `backend/MicroLIMS.API/Controllers/KpiDashboardController.cs`
- Modify: `backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs` (after the `KpiService` registration, ~line 97, and after S1's `WorkflowProfileService` line)
- Modify: `backend/MicroLIMS.Tests/TestServiceFactory.cs`
- Modify: `backend/MicroLIMS.Tests/UnitTests/RolePermissionSeedDataTests.cs`
- Modify: `backend/MicroLIMS.Tests/IntegrationTests/PermissionEnforcementMigrationPostgresTests.cs`
- Modify: `backend/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt` (regenerated)
- Modify: `frontend/src/routes/routes.ts`
- Test: `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiRegistryTests.cs`, `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiDashboardControllerTests.cs`

**Interfaces:**
- Consumes: Tasks 1-10 (all eleven KPI classes, `KpiRegistry`, `KpiDashboardService`); S1 `TestServiceFactory.WorkflowProfile`, `PermissionConstants.KpiConfigureProfiles` (the pattern to copy); `ApiResponse<object>.Ok` (`MicroLIMS.Shared.Responses`).
- Produces:
  - `PermissionConstants.KpiViewTeam = "Kpi.ViewTeam"` (All + Enforced), seeded and granted to SystemAdministrator and SectionHead; frontend `PERMISSIONS.KPI_VIEW_TEAM`.
  - `KpiDashboardController` (`[Route("api/kpi")]`, class-level `[Authorize(Policy = PermissionConstants.KpiView)]`): `GetDashboard`, `GetRecords`, `GetDefinitions`.
  - `TestServiceFactory.BuildKpiRegistry(MicroLimsDbContext db, TimeProvider? time = null)` -> `KpiRegistry`; `TestServiceFactory.KpiDashboard(MicroLimsDbContext db, TimeProvider? time = null, IUserSectionScopeService? scope = null)` -> `KpiDashboardService`.

- [ ] **Step 1: Confirm the permission baselines**

Run the baseline first so the numbers you edit are the real ones:
```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~RolePermissionSeedDataTests|FullyQualifiedName~PermissionAuthorizationTests|FullyQualifiedName~AuthorizationMatrixTests"
```
Expected: PASS. Read `RolePermissionSeedDataTests.cs` (the `SectionHead_HoldsExactlyTheCatalogSet` `expected` array and its `Assert.Equal(N, codes.Count)`, and the two total-grant assertions in `TotalGrantCount_MatchesTheCatalog` and the idempotency test). With S1 merged they read 42 and 121; the contract's increments for this slice are SectionHead +1 and total +2. If your numbers differ from 42/121, keep the +1/+2 rule and say so in the commit message.

- [ ] **Step 2: Write the failing tests**

Edit `RolePermissionSeedDataTests.cs`: add `PermissionConstants.KpiViewTeam` to the SectionHead `expected` array; raise the SectionHead count by 1 (42 -> 43); raise both total-grant literals by 2 (121 -> 123) and extend the explanatory comment with `Kpi.ViewTeam (S2)`. Edit `PermissionEnforcementMigrationPostgresTests.cs`: append `PermissionConstants.KpiViewTeam` to `AddedByLaterMigrations`.

Create `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiRegistryTests.cs`:
```csharp
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Tests.Fixtures;
using Xunit;
using static MicroLIMS.Tests.UnitTests.Kpi.KpiTestData;

namespace MicroLIMS.Tests.UnitTests.Kpi;

public class KpiRegistryTests
{
    private static readonly DateOnly From = new(2026, 10, 1);
    private static readonly DateOnly To = new(2026, 10, 31);
    private static readonly FixedTimeProvider Clock = new(new DateTimeOffset(Now));

    [Fact]
    public void Registry_HasTheElevenKpis_WithGroupsOrdersUnitsAndDirections()
    {
        using var db = NewDb();
        var registry = TestServiceFactory.BuildKpiRegistry(db, Clock);

        var expected = new (string Key, KpiGroup Group, int Order, string Unit, KpiDirection Direction)[]
        {
            ("sample-tat", KpiGroup.Headline, 1, "days", KpiDirection.LowerIsBetter),
            ("on-time-all-stages", KpiGroup.Headline, 2, "%", KpiDirection.HigherIsBetter),
            ("right-first-time", KpiGroup.Headline, 3, "%", KpiDirection.HigherIsBetter),
            ("retest-rate", KpiGroup.Headline, 4, "%", KpiDirection.LowerIsBetter),
            ("oos-rate", KpiGroup.Headline, 5, "%", KpiDirection.LowerIsBetter),
            ("wip-backlog", KpiGroup.Headline, 6, "tests", KpiDirection.Neutral),
            ("incubation-reads-on-time", KpiGroup.Headline, 7, "%", KpiDirection.HigherIsBetter),
            ("em-excursion-rate", KpiGroup.Headline, 8, "%", KpiDirection.LowerIsBetter),
            ("stage-tat", KpiGroup.Flow, 20, "days", KpiDirection.LowerIsBetter),
            ("backlog-aging", KpiGroup.Flow, 21, "tests", KpiDirection.Neutral),
            ("returns-by-test", KpiGroup.Quality, 30, "tests", KpiDirection.Neutral),
        };
        Assert.Equal(expected, registry.All.Select(k => (k.Key, k.Group, k.Order, k.Unit, k.Direction)));
    }

    [Fact]
    public void Registry_EveryKpiExplainsItself_ForTheDefinitionsDrawer()
    {
        using var db = NewDb();
        Assert.All(TestServiceFactory.BuildKpiRegistry(db, Clock).All, k =>
        {
            Assert.False(string.IsNullOrWhiteSpace(k.Name), k.Key);
            Assert.False(string.IsNullOrWhiteSpace(k.Definition), k.Key);
            Assert.False(string.IsNullOrWhiteSpace(k.ClockEvents), k.Key);
            Assert.False(string.IsNullOrWhiteSpace(k.Population), k.Key);
            Assert.False(string.IsNullOrWhiteSpace(k.Source), k.Key);
        });
    }

    [Fact]
    public void Registry_DuplicateKey_IsRejected_AndFindIsCaseInsensitive()
    {
        using var db = NewDb();
        var one = new SampleTatKpi(db);
        Assert.Throws<InvalidOperationException>(() => new KpiRegistry(new IKpi[] { one, new SampleTatKpi(db) }));
        Assert.Same(one, new KpiRegistry(new IKpi[] { one }).Find("SAMPLE-TAT"));
        Assert.Null(new KpiRegistry(new IKpi[] { one }).Find("nope"));
    }

    [Fact]
    public async Task Dashboard_EmptyDatabase_EveryRealKpiIsNoDataOrZero()
    {
        await using var db = NewDb();
        var svc = TestServiceFactory.KpiDashboard(db, Clock, new StubSectionScope(_ => null));

        var dto = await svc.GetDashboardAsync(1, true, From, To, null, null, null, null);

        Assert.Equal(11, dto.Kpis.Count);
        Assert.All(dto.Kpis, k => { Assert.Null(k.Error); Assert.NotNull(k.Result); });
        var counts = new[] { "wip-backlog", "backlog-aging", "returns-by-test" };
        foreach (var entry in dto.Kpis.Where(k => !counts.Contains(k.Key)))
        {
            Assert.Equal(KpiStatus.NoData, entry.Result!.Status);   // never 0% / 100% / 0 days
            Assert.Null(entry.Result.Value);
            Assert.Null(entry.Result.Prior);
        }
        foreach (var entry in dto.Kpis.Where(k => counts.Contains(k.Key)))
        {
            Assert.Equal(KpiStatus.Ok, entry.Result!.Status);
            Assert.Equal(0m, entry.Result.Value);
        }
    }

    [Fact]
    public async Task Dashboard_EmptyScope_SeesNothing_EvenWithData()
    {
        await using var db = NewDb();
        ApprovedSample(db, 1, Utc(10, 1), Utc(10, 3));
        await db.SaveChangesAsync();

        var unrestricted = await TestServiceFactory.KpiDashboard(db, Clock, new StubSectionScope(_ => null))
            .GetDashboardAsync(1, true, From, To, null, null, null, null);
        var nothing = await TestServiceFactory.KpiDashboard(db, Clock, new StubSectionScope(_ => Array.Empty<int>()))
            .GetDashboardAsync(1, true, From, To, null, null, null, null);

        Assert.Equal(1, unrestricted.Kpis.Single(k => k.Key == "sample-tat").Result!.N);
        Assert.Equal(KpiStatus.NoData, nothing.Kpis.Single(k => k.Key == "sample-tat").Result!.Status);
        Assert.Equal(0m, nothing.Kpis.Single(k => k.Key == "wip-backlog").Result!.Value);
    }
}
```

Create `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiDashboardControllerTests.cs`:
```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.UnitTests.Kpi;

public class KpiDashboardControllerTests
{
    private static readonly DateOnly From = new(2026, 10, 1);
    private static readonly DateOnly To = new(2026, 10, 31);

    private sealed class CapturingKpi : IKpi
    {
        public KpiQuery? Seen { get; private set; }
        public int Page { get; private set; }
        public int PageSize { get; private set; }
        public string Key => "cap";
        public string Name => "cap";
        public string Unit => "%";
        public KpiDirection Direction => KpiDirection.Neutral;
        public KpiGroup Group => KpiGroup.Headline;
        public int Order => 1;
        public string Definition => "d";
        public string ClockEvents => "c";
        public string Population => "p";
        public string Source => "s";
        public Task<KpiResult> ComputeAsync(KpiQuery q, CancellationToken ct)
        {
            Seen = q;
            return Task.FromResult(KpiScope.ToResult(KpiCore.Nothing(), null, Unit));
        }
        public Task<KpiRecordPageDto> GetRecordsAsync(KpiQuery q, int page, int pageSize, CancellationToken ct)
        {
            Seen = q;
            Page = page;
            PageSize = pageSize;
            return Task.FromResult(new KpiRecordPageDto(Key, page, pageSize, 0, Array.Empty<KpiRecordDto>()));
        }
    }

    private static (KpiDashboardController Controller, CapturingKpi Kpi) Build(params string[] permissions)
    {
        var kpi = new CapturingKpi();
        var service = new KpiDashboardService(KpiTestData.NewDb(), new KpiRegistry(new IKpi[] { kpi }),
            new StubSectionScope(_ => null), new FixedTimeProvider(new DateTimeOffset(KpiTestData.Now)));
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "5") };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var controller = new KpiDashboardController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
            }
        };
        return (controller, kpi);
    }

    [Fact]
    public async Task ViewTeamClaim_SetsCanViewTeam_AndUserIdComesFromTheToken()
    {
        var (controller, kpi) = Build(PermissionConstants.KpiView, PermissionConstants.KpiViewTeam);
        var result = await controller.GetDashboard(From, To, null, null, null, 9, default);

        Assert.IsType<OkObjectResult>(result);
        Assert.True(kpi.Seen!.CanViewTeam);
        Assert.Equal(5, kpi.Seen.ViewerUserId);
        Assert.Equal(9, kpi.Seen.AnalystId);
    }

    [Fact]
    public async Task WithoutViewTeamClaim_AnotherAnalyst_IsForbidden_OnBothEndpoints()
    {
        var (controller, _) = Build(PermissionConstants.KpiView);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.GetDashboard(From, To, null, null, null, 9, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.GetRecords("cap", From, To, null, null, null, 9, 1, 50, default));
    }

    [Fact]
    public async Task WithoutViewTeamClaim_NoAnalystId_StillGetsLabLevelFigures()
    {
        var (controller, kpi) = Build(PermissionConstants.KpiView);
        await controller.GetDashboard(From, To, null, null, null, null, default);

        Assert.False(kpi.Seen!.CanViewTeam);
        Assert.Null(kpi.Seen.AnalystId);
    }

    [Fact]
    public async Task Records_PassTheKeyAndPaging()
    {
        var (controller, kpi) = Build(PermissionConstants.KpiView);
        var result = Assert.IsType<OkObjectResult>(await controller.GetRecords("cap", From, To, null, null, null, null, 2, 5000, default));
        Assert.NotNull(result.Value);
        Assert.Equal(2, kpi.Page);
        Assert.Equal(200, kpi.PageSize);                 // capped by the service
        Assert.Equal(5, kpi.Seen!.ViewerUserId);
    }
}
```

- [ ] **Step 3: Run, expect a compile failure**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~KpiRegistryTests|FullyQualifiedName~KpiDashboardControllerTests|FullyQualifiedName~RolePermissionSeedDataTests"
```
Expected: build errors `'PermissionConstants' does not contain a definition for 'KpiViewTeam'`, `'TestServiceFactory' does not contain a definition for 'BuildKpiRegistry'`, `'KpiDashboardController' could not be found`.

- [ ] **Step 4: Add the permission in the backend**

`backend/MicroLIMS.Shared/Constants/PermissionConstants.cs`: next to `KpiView` (line ~84) add
```csharp
    // KPI remodel S2: see other analysts' figures. Without it the server returns a viewer's own analyst figures only (D8).
    public const string KpiViewTeam = "Kpi.ViewTeam";
```
and add `KpiViewTeam` to the `All` list (beside `KpiView`) and to the `Enforced` set (beside `KpiView`). Keep S1's `KpiConfigureProfiles` entries untouched.

`backend/MicroLIMS.Persistence/Seed/DbSeeder.cs` (method `SeedPermissionsAndGrants`): in the `catalog` array beside the `KpiView` tuple (line ~490) add
```csharp
            (PermissionConstants.KpiViewTeam, "See the KPI and workload figures of other analysts"),
```
and in the SectionHead grant list beside `PermissionConstants.KpiView` (line ~543) add `PermissionConstants.KpiViewTeam,`. SystemAdministrator receives it through `PermissionConstants.All`. Do not touch the Reviewer or Analyst lists.

Run `git diff -- backend/MicroLIMS.Persistence/Seed/DbSeeder.cs backend/MicroLIMS.Shared/Constants/PermissionConstants.cs` and confirm only KPI hunks are present before staging later.

- [ ] **Step 5: Add the controller**

Create `backend/MicroLIMS.API/Controllers/KpiDashboardController.cs`:
```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// The user id and the Kpi.ViewTeam grant come from the token. The service enforces scope and D8; nothing here
// trusts a query string.
[ApiController]
[Route("api/kpi")]
[Authorize(Policy = PermissionConstants.KpiView)]
public class KpiDashboardController : ControllerBase
{
    private readonly KpiDashboardService _dashboard;

    public KpiDashboardController(KpiDashboardService dashboard) => _dashboard = dashboard;

    private int CurrentUserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    private bool CanViewTeam => User.HasClaim("permission", PermissionConstants.KpiViewTeam);

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromQuery] string? category, [FromQuery] string? location, [FromQuery] string? testCode,
        [FromQuery] int? analystId, CancellationToken ct) =>
        Ok(ApiResponse<object>.Ok(await _dashboard.GetDashboardAsync(
            CurrentUserId, CanViewTeam, from, to, category, location, testCode, analystId, ct)));

    [HttpGet("{key}/records")]
    public async Task<IActionResult> GetRecords(
        [FromRoute] string key, [FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromQuery] string? category, [FromQuery] string? location, [FromQuery] string? testCode,
        [FromQuery] int? analystId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        Ok(ApiResponse<object>.Ok(await _dashboard.GetRecordsAsync(
            key, CurrentUserId, CanViewTeam, from, to, category, location, testCode, analystId, page, pageSize, ct)));

    [HttpGet("definitions")]
    public async Task<IActionResult> GetDefinitions([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        Ok(ApiResponse<object>.Ok(await _dashboard.GetDefinitionsAsync(from, to, ct)));
}
```
Before building, check the new routes do not collide with the legacy controller (a collision only shows up at runtime as an ambiguous-match 500). Grep `backend/MicroLIMS.API/Controllers/KpiController.cs` for `Http(Get|Put)\("` and confirm none of the templates is `dashboard`, `definitions`, or ends in `/records`. (The 15 legacy actions are `analysts`, `workload-weights`, `completion-stats`, `delay-tracking`, and similar single-segment names.)

- [ ] **Step 6: Register in DI and add the test factory**

`ServiceCollectionExtensions.cs`: add `using MicroLIMS.Application.Services.Kpi;` if S1 has not, and after the `services.AddScoped<KpiService>();` line (and S1's `WorkflowProfileService` line) add
```csharp
        services.AddScoped<IKpi, SampleTatKpi>();
        services.AddScoped<IKpi, OnTimeAllStagesKpi>();
        services.AddScoped<IKpi, RightFirstTimeKpi>();
        services.AddScoped<IKpi, RetestRateKpi>();
        services.AddScoped<IKpi, OosRateKpi>();
        services.AddScoped<IKpi, WipBacklogKpi>();
        services.AddScoped<IKpi, IncubationReadsOnTimeKpi>();
        services.AddScoped<IKpi, EmExcursionRateKpi>();
        services.AddScoped<IKpi, StageTatKpi>();
        services.AddScoped<IKpi, BacklogAgingKpi>();
        services.AddScoped<IKpi, ReturnsByTestKpi>();
        services.AddScoped<KpiRegistry>();
        services.AddScoped<KpiDashboardService>();
```
`KpiDashboardService` has an optional `TimeProvider` and `ILogger` parameter; the container supplies the logger and falls back to the defaults it can not resolve (check `TimeProvider` is registered the way `KpiService` gets it; if the container cannot resolve `TimeProvider`, the optional parameter default `null` is used).

`backend/MicroLIMS.Tests/TestServiceFactory.cs`: add `using MicroLIMS.Application.Services.Kpi;` and, after the `Dashboard(...)` method, add (the method is not named `KpiRegistry`, which would shadow the type inside this class):
```csharp
    public static KpiRegistry BuildKpiRegistry(MicroLimsDbContext db, TimeProvider? time = null)
    {
        var profiles = WorkflowProfile(db, time);
        return new KpiRegistry(new IKpi[]
        {
            new SampleTatKpi(db), new OnTimeAllStagesKpi(db, profiles), new RightFirstTimeKpi(db, profiles),
            new RetestRateKpi(db), new OosRateKpi(db), new WipBacklogKpi(db), new IncubationReadsOnTimeKpi(db),
            new EmExcursionRateKpi(db), new StageTatKpi(db), new BacklogAgingKpi(db, profiles), new ReturnsByTestKpi(db)
        });
    }

    public static KpiDashboardService KpiDashboard(MicroLimsDbContext db, TimeProvider? time = null, IUserSectionScopeService? scope = null) =>
        new(db, BuildKpiRegistry(db, time), scope ?? new UserSectionScopeService(db), time);
```
`frontend/src/routes/routes.ts`: in `PERMISSIONS`, under `KPI_VIEW: "Kpi.View",` add `KPI_VIEW_TEAM: "Kpi.ViewTeam",` (keep S1's `KPI_CONFIGURE_PROFILES`).

- [ ] **Step 7: Run, expect pass; regenerate the matrix**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~KpiRegistryTests|FullyQualifiedName~KpiDashboardControllerTests|FullyQualifiedName~RolePermissionSeedDataTests|FullyQualifiedName~PermissionAuthorizationTests"
```
Expected: PASS (`EveryEnforcedCode_IsCheckedSomewhere` passes because the controller references `PermissionConstants.KpiViewTeam`). `AuthorizationMatrixTests.EffectiveAccess_MatchesTheReviewedMatrix` now FAILS with three added lines. Regenerate and review:
```
MICROLIMS_WRITE_AUTH_MATRIX=1 dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter AuthorizationMatrixTests
git diff -- backend/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt
```
Expected diff: exactly three added lines, `KpiDashboardController.GetDashboard(...)`, `KpiDashboardController.GetRecords(...)`, `KpiDashboardController.GetDefinitions(...)`, each ending `: SystemAdministrator,SectionHead`, plus S1's lines if S1 has not yet regenerated them. Any other change means a policy was altered by mistake: stop and fix. Then run the matrix test again without the env var (expected PASS).

Optional route smoke check (needs the API running in another terminal, `https://localhost:65435`): `curl -k -s -o /dev/null -w "%{http_code}" https://localhost:65435/api/kpi/definitions?from=2026-10-01&to=2026-10-31` must print `401` (unauthenticated), not `500` (ambiguous route).

- [ ] **Step 8: Commit**

For each shared file run `git diff -- <file>`; if there are hunks that are not yours, use `git add -p <file>`.
```
git add backend/MicroLIMS.Shared/Constants/PermissionConstants.cs backend/MicroLIMS.Persistence/Seed/DbSeeder.cs backend/MicroLIMS.API/Controllers/KpiDashboardController.cs backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs backend/MicroLIMS.Tests/TestServiceFactory.cs backend/MicroLIMS.Tests/UnitTests/RolePermissionSeedDataTests.cs backend/MicroLIMS.Tests/IntegrationTests/PermissionEnforcementMigrationPostgresTests.cs backend/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt backend/MicroLIMS.Tests/UnitTests/Kpi/KpiRegistryTests.cs backend/MicroLIMS.Tests/UnitTests/Kpi/KpiDashboardControllerTests.cs frontend/src/routes/routes.ts
git commit -m "feat(kpi): Kpi.ViewTeam permission, dashboard controller and DI

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 12: Migration `SeedKpiViewTeamPermission`

**Files:**
- Create: `backend/MicroLIMS.Persistence/Migrations/<timestamp>_SeedKpiViewTeamPermission.cs` (+ `.Designer.cs`, generated)
- Test: `backend/MicroLIMS.Tests/IntegrationTests/KpiViewTeamPermissionMigrationPostgresTests.cs`

**Interfaces:**
- Consumes: Task 11 (`PermissionConstants.KpiViewTeam`); the idempotent-SQL pattern of `20261003105411_AddWorkingStandards.cs` (lines 239-262); `PostgresTestFixture.CreateDbContext()`.
- Produces: migration class `MicroLIMS.Persistence.Migrations.SeedKpiViewTeamPermission`: `Up` inserts the permission (enforced) and grants it to every role whose `Type` is 0 or 1; `Down` removes grants then the permission. No schema change.

Why a migration at all: `DbSeeder` inserts grants only when `RolePermissions` is empty, so an existing database would get the permission row but no role would hold it. Join on `Roles."Type"` so custom roles of those base types are included, as every earlier permission migration does.

- [ ] **Step 1: Check the hazard, then write the failing test**

```
git status --short backend/MicroLIMS.Persistence
git diff --stat -- backend/MicroLIMS.Persistence/Migrations/MicroLimsDbContextModelSnapshot.cs
```
Note the snapshot's current diff size (it carries the uncommitted Media/Cryovial work; also S1's hunk if S1 is not yet committed). This migration has no model change, so it must add **nothing** to the snapshot.

Create `backend/MicroLIMS.Tests/IntegrationTests/KpiViewTeamPermissionMigrationPostgresTests.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Migrations;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// The seed grants nothing on an established database, so this migration is what gives existing
// Section Heads and administrators the permission.
[Collection("PostgresDatabaseCollection")]
public class KpiViewTeamPermissionMigrationPostgresTests
{
    private const string Code = PermissionConstants.KpiViewTeam;
    private readonly PostgresTestFixture _fixture;

    public KpiViewTeamPermissionMigrationPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static async Task RunAsync(MicroLimsDbContext db, IEnumerable<MigrationOperation> operations)
    {
        foreach (var op in operations.Cast<SqlOperation>())
            await db.Database.ExecuteSqlRawAsync(op.Sql);
    }

    private static Task UpAsync(MicroLimsDbContext db) => RunAsync(db, new SeedKpiViewTeamPermission().UpOperations);
    private static Task DownAsync(MicroLimsDbContext db) => RunAsync(db, new SeedKpiViewTeamPermission().DownOperations);

    [PostgresFact]
    public async Task Up_GrantsAdminsAndSectionHeads_IsIdempotent_AndDownRemovesIt()
    {
        await using var db = _fixture.CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync();

        // The state of an existing database before this migration.
        await db.Database.ExecuteSqlRawAsync(
            @"DELETE FROM ""RolePermissions"" WHERE ""PermissionId"" IN (SELECT ""Id"" FROM ""Permissions"" WHERE ""Code"" = {0});
              DELETE FROM ""Permissions"" WHERE ""Code"" = {0};", new object[] { Code });
        var customSectionHead = new Role { Name = "QC Lead (custom)", Type = RoleType.SectionHead, IsSystemRole = false, IsActive = true };
        var customAnalyst = new Role { Name = "QC Trainee (custom)", Type = RoleType.Analyst, IsSystemRole = false, IsActive = true };
        db.Roles.AddRange(customSectionHead, customAnalyst);
        await db.SaveChangesAsync();

        await UpAsync(db);

        var permission = await db.Permissions.SingleAsync(p => p.Code == Code);
        Assert.True(permission.IsEnforced);
        Assert.False(string.IsNullOrWhiteSpace(permission.Description));

        var roleIds = await db.RolePermissions.Where(rp => rp.Permission!.Code == Code).Select(rp => rp.RoleId).ToListAsync();
        var types = await db.Roles.Where(r => roleIds.Contains(r.Id)).Select(r => r.Type).Distinct().OrderBy(t => t).ToListAsync();
        Assert.Equal(new[] { RoleType.SystemAdministrator, RoleType.SectionHead }, types);   // not Reviewer, not Analyst
        Assert.Contains(customSectionHead.Id, roleIds);
        Assert.DoesNotContain(customAnalyst.Id, roleIds);

        var grants = await db.RolePermissions.CountAsync();
        var permissions = await db.Permissions.CountAsync();
        await UpAsync(db);
        Assert.Equal(grants, await db.RolePermissions.CountAsync());
        Assert.Equal(permissions, await db.Permissions.CountAsync());

        await DownAsync(db);
        Assert.False(await db.Permissions.AnyAsync(p => p.Code == Code));
        Assert.False(await db.RolePermissions.AnyAsync(rp => rp.Permission!.Code == Code));

        await tx.RollbackAsync();
    }

    [PostgresFact]
    public async Task Up_EnforcesARowThatAlreadyExistedUnenforced()
    {
        await using var db = _fixture.CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync();

        // DbSeeder may have inserted the row first with the code's own flag; either way it ends up enforced.
        await db.Database.ExecuteSqlRawAsync(
            @"UPDATE ""Permissions"" SET ""IsEnforced"" = FALSE WHERE ""Code"" = {0};", new object[] { Code });
        await UpAsync(db);

        Assert.True((await db.Permissions.SingleAsync(p => p.Code == Code)).IsEnforced);
        await tx.RollbackAsync();
    }
}
```

- [ ] **Step 2: Run, expect a compile failure**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~KpiViewTeamPermissionMigrationPostgresTests"
```
Expected: `'SeedKpiViewTeamPermission' could not be found`.

- [ ] **Step 3: Generate the migration**

```
dotnet ef migrations add SeedKpiViewTeamPermission --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API --configuration Release
```
Then verify, before editing:
1. The new file name starts with a timestamp **greater than** `20261011103646` (it will, unless the machine clock is wrong; if not, delete the two generated files and fix the clock).
2. `Up()` and `Down()` are empty. If `Up()` contains any `CreateTable`, `AddColumn`, `AlterColumn` or a Media/Cryovial operation, the snapshot carried another feature's change that is not yet reflected in a migration: delete the generated files, ask for the Media/Cryovial migration to be committed first, and re-run.
3. `git diff --stat -- backend/MicroLIMS.Persistence/Migrations/MicroLimsDbContextModelSnapshot.cs` is the same as the figure you noted in step 1 (an empty migration must not change the snapshot). If it changed, restore only your accidental lines with `git add -p`/`git checkout -p`, never the whole file.

- [ ] **Step 4: Fill `Up` and `Down`**

Replace the generated empty bodies in `<timestamp>_SeedKpiViewTeamPermission.cs` with:
```csharp
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DbSeeder seeds Kpi.ViewTeam on a fresh database; these idempotent statements cover existing ones.
            // RoleType: 0 = SystemAdministrator, 1 = SectionHead
            migrationBuilder.Sql(@"
                INSERT INTO ""Permissions"" (""Code"", ""Description"", ""IsEnforced"")
                SELECT 'Kpi.ViewTeam', 'See the KPI and workload figures of other analysts', TRUE
                WHERE NOT EXISTS (SELECT 1 FROM ""Permissions"" WHERE ""Code"" = 'Kpi.ViewTeam');");
            migrationBuilder.Sql(@"UPDATE ""Permissions"" SET ""IsEnforced"" = TRUE WHERE ""Code"" = 'Kpi.ViewTeam';");
            migrationBuilder.Sql(@"
                INSERT INTO ""RolePermissions"" (""RoleId"", ""PermissionId"")
                SELECT r.""Id"", p.""Id""
                FROM ""Roles"" r CROSS JOIN ""Permissions"" p
                WHERE r.""Type"" IN (0, 1) AND p.""Code"" = 'Kpi.ViewTeam'
                  AND NOT EXISTS (SELECT 1 FROM ""RolePermissions"" rp
                                  WHERE rp.""RoleId"" = r.""Id"" AND rp.""PermissionId"" = p.""Id"");");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM ""RolePermissions""
                WHERE ""PermissionId"" IN (SELECT ""Id"" FROM ""Permissions"" WHERE ""Code"" = 'Kpi.ViewTeam');");
            migrationBuilder.Sql(@"DELETE FROM ""Permissions"" WHERE ""Code"" = 'Kpi.ViewTeam';");
        }
```
Keep the generated class attributes, namespace and `using` lines as generated. Leave the generated `.Designer.cs` as is.

- [ ] **Step 5: Run the Postgres tests, expect pass**

```
MICROLIMS_TEST_POSTGRES=<the value PostgresTestConfiguration.cs expects> dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~KpiViewTeamPermissionMigrationPostgresTests|FullyQualifiedName~PermissionEnforcementMigrationPostgresTests|FullyQualifiedName~FreshDatabaseSeedPostgresTests"
```
Expected: all PASS (the fixture applies every migration, including this one, to a fresh database). Without the env var the tests skip; a slice that adds a migration must run them once.

- [ ] **Step 6: Commit**

Stage only the two migration files and the test (not the snapshot):
```
git add backend/MicroLIMS.Persistence/Migrations/*_SeedKpiViewTeamPermission.cs backend/MicroLIMS.Persistence/Migrations/*_SeedKpiViewTeamPermission.Designer.cs backend/MicroLIMS.Tests/IntegrationTests/KpiViewTeamPermissionMigrationPostgresTests.cs
git status --short backend/MicroLIMS.Persistence | head
git commit -m "feat(kpi): migration seeding Kpi.ViewTeam for existing databases

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```
Note for the reviewer: the generated Designer file embeds the full model, which currently includes the uncommitted Media/Cryovial entities. If that work is still uncommitted when this is merged, the Designer is correct only once that migration is merged too; prefer committing the Media/Cryovial work first.

---

### Task 13: Golden-dataset integration test on PostgreSQL

**Files:**
- Create: `backend/MicroLIMS.Tests/IntegrationTests/KpiDashboardGoldenPostgresTests.cs`

**Interfaces:**
- Consumes: `TestServiceFactory.KpiDashboard(db, time, scope)`; `StubSectionScope`; `FixedTimeProvider`; `PostgresTestFixture` (`CreateDbContext()`, `SeededUserId`, `SeededDepartmentId`); `[PostgresFact]`; every entity listed in the seed.
- Produces: one `[PostgresFact]` that seeds a hand-calculated dataset through EF into two private lab sections (so rows from other tests in the shared database cannot leak in) and asserts the dashboard figures, the records page and the section isolation.

Isolation rules for the shared database: use test codes `KPIG_*` and sample references `KPIG-*`; resolve profiles with **test-code overrides only** (a type default row would be global); scope every query with `SectionIds = [lab.Id]`. `WorkflowProfileVersions` rows are immutable once Effective (a DB trigger), so the test never updates or deletes them; the fixture database is dropped at the end of the run.

- [ ] **Step 1: Write the test**

Create `backend/MicroLIMS.Tests/IntegrationTests/KpiDashboardGoldenPostgresTests.cs`:
```csharp
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Dashboard figures against a dataset small enough to calculate by hand. Period: 1-31 Oct 2026, "now" 15 Oct 12:00 UTC.
//
// Lab samples (profiles: KPIG_TAMC = 2 units / 5 days, KPIG_GRAV = 4 units / 10 days)
//   G0  received  9/10 08:00, approved  9/20 08:00, TAMC           -> prior period
//   G1  received 10/01 08:00, approved 10/03 08:00, TAMC  (2.0 d)  prepared 10/01 09:00, submit 10/02 09:00, review 10/02 15:00
//   G2  received 10/02 08:00, approved 10/06 08:00, GRAV  (4.0 d)  prepared 10/02 09:00, submit 10/04 09:00, review 10/05 12:00 (27 h: late); test returned once
//   G3  received 10/03 08:00, approved 10/08 08:00, TAMC  (5.0 d)  prepared 10/03 09:00, submit 10/07 09:00, review 10/07 21:00
//   G4  retest decision signed 10/06 (signature only, ApprovedAt null), OOS group, its TAMC test superseded
//   G5  received 10/01 08:00, approved 10/12 08:00, TAMC (11.0 d)  no preparation record
//   G6  open GRAV test (running), prepared 10/12 09:00
//   E1  environmental monitoring: 6 within, 1 alert, 1 action
// Other lab: GX approved 10/04 (3.0 d)
// Incubations on G1's and G3's tests: one read on time, one 10 h late. One return event on G2's test.
[Collection("PostgresDatabaseCollection")]
public class KpiDashboardGoldenPostgresTests
{
    private readonly PostgresTestFixture _fixture;
    public KpiDashboardGoldenPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static DateTime Utc(int month, int day, int hour = 0) => new(2026, month, day, hour, 0, 0, DateTimeKind.Utc);

    [PostgresFact]
    public async Task Dashboard_MatchesHandCalculatedGoldenDataset()
    {
        await using var db = _fixture.CreateDbContext();
        var user = _fixture.SeededUserId;

        var lab = new DocumentSection { Name = "KPI golden lab", Code = "KPIG-A", DepartmentId = _fixture.SeededDepartmentId, IsActive = true };
        var otherLab = new DocumentSection { Name = "KPI golden other", Code = "KPIG-B", DepartmentId = _fixture.SeededDepartmentId, IsActive = true };
        var cause = new CauseOfTesting { Name = "KPIG routine", IsActive = true };
        db.AddRange(lab, otherLab, cause);
        await db.SaveChangesAsync();

        db.TestDefinitions.AddRange(
            new TestDefinition { Code = "KPIG_TAMC", DisplayName = "KPIG count", SectionId = lab.Id, WorkflowType = WorkflowType.CountTest },
            new TestDefinition { Code = "KPIG_GRAV", DisplayName = "KPIG gravimetric", SectionId = lab.Id, WorkflowType = WorkflowType.Gravimetric });
        db.WorkflowProfileVersions.AddRange(
            Profile(WorkflowType.CountTest, "KPIG_TAMC", 2m, 5m, user),
            Profile(WorkflowType.Gravimetric, "KPIG_GRAV", 4m, 10m, user));

        var samples = new Dictionary<string, Sample>();
        void AddSample(string tag, SampleCategory category, DateTime received, DateTime? approved, ApprovalDecision? decision,
            SampleStatus status, string? oos = null)
        {
            var s = new Sample
            {
                ReferenceNumber = $"KPIG-{tag}", ControlNumber = $"KPIG-CN-{tag}", SampledBy = "kpi", Category = category,
                CauseOfTestingId = cause.Id, ReceivedByUserId = user, ReceivedAt = received, ApprovedAt = approved,
                ApprovalDecision = decision, Status = status, OosGroupCode = oos
            };
            samples[tag] = s;
            db.Samples.Add(s);
        }
        AddSample("G0", SampleCategory.FinishedProduct, Utc(9, 10, 8), Utc(9, 20, 8), ApprovalDecision.Approve, SampleStatus.Approved);
        AddSample("G1", SampleCategory.FinishedProduct, Utc(10, 1, 8), Utc(10, 3, 8), ApprovalDecision.Approve, SampleStatus.Approved);
        AddSample("G2", SampleCategory.FinishedProduct, Utc(10, 2, 8), Utc(10, 6, 8), ApprovalDecision.Approve, SampleStatus.Approved);
        AddSample("G3", SampleCategory.FinishedProduct, Utc(10, 3, 8), Utc(10, 8, 8), ApprovalDecision.Approve, SampleStatus.Approved);
        AddSample("G4", SampleCategory.FinishedProduct, Utc(10, 4, 8), null, ApprovalDecision.RetestRetainedSample, SampleStatus.RetestRequested, "KPIG-OOS1");
        AddSample("G5", SampleCategory.FinishedProduct, Utc(10, 1, 8), Utc(10, 12, 8), ApprovalDecision.Approve, SampleStatus.Approved);
        AddSample("G6", SampleCategory.FinishedProduct, Utc(10, 12, 8), null, null, SampleStatus.InTesting);
        AddSample("E1", SampleCategory.EnvironmentalMonitoring, Utc(10, 5, 8), null, null, SampleStatus.InTesting);
        AddSample("GX", SampleCategory.FinishedProduct, Utc(10, 1, 8), Utc(10, 4, 8), ApprovalDecision.Approve, SampleStatus.Approved);
        await db.SaveChangesAsync();

        var tests = new Dictionary<string, TestOrder>();
        void AddTest(string tag, string code, int sectionId, ApprovalStatus status, WorkflowStep step, bool superseded = false)
        {
            var t = new TestOrder
            {
                SampleId = samples[tag].Id, TestCode = code, SectionId = sectionId, Status = status, CurrentStep = step,
                AssignedAnalystId = user, IsSuperseded = superseded
            };
            tests[tag] = t;
            db.TestOrders.Add(t);
        }
        AddTest("G0", "KPIG_TAMC", lab.Id, ApprovalStatus.Approved, WorkflowStep.Approved);
        AddTest("G1", "KPIG_TAMC", lab.Id, ApprovalStatus.Approved, WorkflowStep.Approved);
        AddTest("G2", "KPIG_GRAV", lab.Id, ApprovalStatus.Approved, WorkflowStep.Approved);
        AddTest("G3", "KPIG_TAMC", lab.Id, ApprovalStatus.Approved, WorkflowStep.Approved);
        AddTest("G4", "KPIG_TAMC", lab.Id, ApprovalStatus.Approved, WorkflowStep.Approved, superseded: true);
        AddTest("G5", "KPIG_TAMC", lab.Id, ApprovalStatus.Approved, WorkflowStep.Approved);
        AddTest("G6", "KPIG_GRAV", lab.Id, ApprovalStatus.InProgress, WorkflowStep.Running);
        AddTest("E1", "KPIG_EM", lab.Id, ApprovalStatus.ResultEntered, WorkflowStep.Ready);
        AddTest("GX", "KPIG_TAMC", otherLab.Id, ApprovalStatus.Approved, WorkflowStep.Approved);
        await db.SaveChangesAsync();

        void Prepared(string tag, DateTime at) =>
            db.SamplePreparations.Add(new SamplePreparation { SampleId = samples[tag].Id, PreparedAt = at, PreparedByUserId = user, Technique = "PourPlate" });
        // signedAt: the decision signature, as SampleApprovalService writes it for every decision.
        void Signoff(string tag, int sectionId, DateTime? submitted, DateTime? reviewed, DateTime? approved, SectionSignoffStatus status,
            ApprovalDecision decision, DateTime? signedAt = null) =>
            db.SampleSectionSignoffs.Add(new SampleSectionSignoff
            {
                SampleId = samples[tag].Id, SectionId = sectionId, Status = status, SubmittedForReviewAt = submitted,
                ReviewedAt = reviewed, ApprovedAt = approved, ApprovalDecision = decision,
                ApprovalSignature = signedAt is { } at
                    ? new ElectronicSignature
                    {
                        UserId = user, UserFullNameSnapshot = "KPIG", UsernameSnapshot = "kpig", RoleSnapshot = "SectionHead",
                        MeaningOfSignature = SignatureMeaning.RetestRequested, EntityType = "Sample", EntityId = samples[tag].Id, SignedAt = at
                    }
                    : null
            });
        Signoff("G0", lab.Id, null, null, Utc(9, 20, 8), SectionSignoffStatus.Approved, ApprovalDecision.Approve);
        Prepared("G1", Utc(10, 1, 9));
        Signoff("G1", lab.Id, Utc(10, 2, 9), Utc(10, 2, 15), Utc(10, 3, 8), SectionSignoffStatus.Approved, ApprovalDecision.Approve);
        Prepared("G2", Utc(10, 2, 9));
        Signoff("G2", lab.Id, Utc(10, 4, 9), Utc(10, 5, 12), Utc(10, 6, 8), SectionSignoffStatus.Approved, ApprovalDecision.Approve);
        Prepared("G3", Utc(10, 3, 9));
        Signoff("G3", lab.Id, Utc(10, 7, 9), Utc(10, 7, 21), Utc(10, 8, 8), SectionSignoffStatus.Approved, ApprovalDecision.Approve);
        // Production shape of a retest decision: signature written, ApprovedAt stays null.
        Signoff("G4", lab.Id, null, null, null, SectionSignoffStatus.RetestRequested, ApprovalDecision.RetestRetainedSample, signedAt: Utc(10, 6, 8));
        Signoff("G5", lab.Id, Utc(10, 11, 8), Utc(10, 11, 12), Utc(10, 12, 8), SectionSignoffStatus.Approved, ApprovalDecision.Approve);
        Prepared("G6", Utc(10, 12, 9));
        Signoff("GX", otherLab.Id, null, null, Utc(10, 4, 8), SectionSignoffStatus.Approved, ApprovalDecision.Approve);

        db.Incubations.AddRange(
            new Incubation { TestOrderId = tests["G1"].Id, StepNumber = 1, StepName = "Incubation", StartedAt = Utc(10, 1, 10), CompletedAt = Utc(10, 5, 10), ExpectedReadingAt = Utc(10, 5, 10) },
            new Incubation { TestOrderId = tests["G3"].Id, StepNumber = 1, StepName = "Incubation", StartedAt = Utc(10, 3, 10), CompletedAt = Utc(10, 6, 20), ExpectedReadingAt = Utc(10, 6, 10) });
        db.TestReturnEvents.Add(new TestReturnEvent
        {
            TestOrderId = tests["G2"].Id, ReviewerUserId = user, AssignedAnalystId = user, Reason = "KPIG re-count", ReturnedAt = Utc(10, 5, 10)
        });

        string[] levels = { "W", "W", "W", "W", "W", "W", "A", "X" };
        for (var i = 0; i < levels.Length; i++)
        {
            db.ResultRecords.Add(new ResultRecord
            {
                SampleId = samples["E1"].Id, TestOrderId = tests["E1"].Id, Category = SampleCategory.EnvironmentalMonitoring,
                SubjectName = "KPIG Room A", TestCode = "KPIG_EM", TestDisplayName = "KPIG plate",
                ResultLevel = levels[i] == "W" ? ResultLevel.WithinLimit : levels[i] == "A" ? ResultLevel.AlertLevel : ResultLevel.ActionLevel,
                ResultEnteredAt = Utc(10, 6, 8), SourceTable = "KpigLocation", SourceId = 9_000_000 + i, ReferenceNumber = "KPIG-E1",
                // UpdatedAt is a non-nullable DateTime with no initializer: left unset it is Kind=Unspecified,
                // which Npgsql rejects for timestamptz.
                UpdatedAt = Utc(10, 6, 8), ResultEnteredByUserId = user, ResultEnteredByName = "KPIG", SampleStatus = SampleStatus.InTesting
            });
        }
        await db.SaveChangesAsync();
        // If a NOT NULL or FK violation is reported for a row above, add that column to the seed row; set every
        // non-nullable DateTime explicitly with Kind=Utc (Npgsql rejects Unspecified for timestamptz). Keep the numbers.

        var clock = new FixedTimeProvider(new DateTimeOffset(Utc(10, 15, 12)));
        var service = TestServiceFactory.KpiDashboard(db, clock, new StubSectionScope(_ => new[] { lab.Id }));

        var dto = await service.GetDashboardAsync(user, true, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), null, null, null, null);

        Assert.Equal(Utc(11, 1), dto.To);
        Assert.Equal(Utc(8, 31), dto.PriorFrom);
        Assert.All(dto.Kpis, k => Assert.Null(k.Error));
        KpiResult R(string key) => dto.Kpis.Single(k => k.Key == key).Result!;

        // Tile 1: [2, 4, 5, 11] days
        var tat = R("sample-tat");
        Assert.Equal(4.5m, tat.Value);
        Assert.Equal(9.2m, tat.Extras["p90"]);
        Assert.Equal(4, tat.N);
        Assert.Equal(10m, tat.Prior!.Value);                  // G0, September
        Assert.Equal(1, tat.Prior.N);

        // Tile 2: G1 and G3 on time, G2 late (review 27 h); G5 has no preparation record
        var onTime = R("on-time-all-stages");
        Assert.Equal(0.6667m, onTime.Value);
        Assert.Equal(3, onTime.N);
        Assert.Equal(1, onTime.Gaps.Single(g => g.Code == "NO_ASSIGNMENT_TIME").Count);
        Assert.DoesNotContain(onTime.Gaps, g => g.Code == "DEFAULT_TARGET");

        // Tile 3: units 2 + 2 + 2 of 2 + 4 + 2 + 2 (G2 was returned)
        var rft = R("right-first-time");
        Assert.Equal(6m, rft.Numerator);
        Assert.Equal(10m, rft.Denominator);
        Assert.Equal(0.6m, rft.Value);
        Assert.Equal(4, rft.N);
        Assert.Equal(KpiStatus.Ok, rft.Status);
        Assert.Equal(1m, rft.Prior!.Value);                   // G0: 2 of 2 units

        // Tiles 4 and 5: five decided samples, one retest (G4), one OOS group (G4)
        Assert.Equal(0.2m, R("retest-rate").Value);
        Assert.Equal(0.2m, R("oos-rate").Value);
        Assert.Equal(5, R("retest-rate").N);
        Assert.Equal(0m, R("retest-rate").Prior!.Value);

        // Tile 6 and aging: G6's running test, 3.125 days of a 10-day target
        Assert.Equal(1m, R("wip-backlog").Value);
        var aging = R("backlog-aging");
        Assert.Equal(1m, aging.Series.Single(p => p.Group == "Testing" && p.Label == "within50").Value);
        Assert.Equal(1m, aging.Value);

        // Tile 7: one read on time, one 10 h late
        var incubation = R("incubation-reads-on-time");
        Assert.Equal(0.5m, incubation.Value);
        Assert.Equal(2, incubation.N);

        // Tile 8: 2 excursions of 8 results
        var em = R("em-excursion-rate");
        Assert.Equal(0.25m, em.Value);
        Assert.Equal(0.125m, em.Extras["alertRate"]);
        Assert.Equal(0.125m, em.Extras["actionRate"]);
        Assert.Contains("AdverseTrend", em.Flags);

        // Supporting KPIs
        var stage = R("stage-tat");
        Assert.Equal(2m, stage.Series.Single(p => p.Label == "Testing").Value);   // [1, 2, 4] days, G5 has no clock start
        Assert.Equal(3, stage.Series.Single(p => p.Label == "Testing").N);
        Assert.Equal(2, stage.Series.Single(p => p.Label == "Incubation").N);
        var returns = R("returns-by-test");
        Assert.Equal(1m, returns.Value);
        Assert.Equal("KPIG_GRAV", Assert.Single(returns.Series).Label);

        // Records trace back to the figure
        var page = await service.GetRecordsAsync("sample-tat", user, true, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), null, null, null, null, 1, 2);
        Assert.Equal(4, page.Total);
        Assert.Equal(new[] { "KPIG-CN-G1", "KPIG-CN-G2" }, page.Items.Select(i => i.ControlNumber));

        // Section isolation: the other lab sees only its own sample
        var otherService = TestServiceFactory.KpiDashboard(db, clock, new StubSectionScope(_ => new[] { otherLab.Id }));
        var otherDto = await otherService.GetDashboardAsync(user, true, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), null, null, null, null);
        var otherTat = otherDto.Kpis.Single(k => k.Key == "sample-tat").Result!;
        Assert.Equal(3m, otherTat.Value);
        Assert.Equal(1, otherTat.N);
    }

    private static WorkflowProfileVersion Profile(WorkflowType type, string testCode, decimal units, decimal target, int userId) => new()
    {
        WorkflowType = type, TestCode = testCode, WorkUnits = units, TargetAnalystDays = target,
        EffectiveFrom = new DateOnly(2026, 1, 1), Status = WorkflowProfileStatus.Effective,
        ReasonForChange = "KPI golden dataset", CreatedByUserId = userId
    };
}
```

- [ ] **Step 2: Run without Postgres, expect a skip; then with Postgres**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~KpiDashboardGoldenPostgresTests"
```
Expected without the env var: 1 skipped (the project must build). Then:
```
MICROLIMS_TEST_POSTGRES=<the value PostgresTestConfiguration.cs expects> dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~KpiDashboardGoldenPostgresTests"
```
Expected: PASS. If the first run reports a database error (`null value in column`, a foreign key, a check constraint), the message names the column: add it to the failing seed row (for example `TestDisplayName`, `ResultKind`, a `Round`, or an `ItemId` for a finished-product sample) and re-run. Do not change an expected number to make a run pass: each is derived in the header comment; if a number really differs, debug the KPI.

- [ ] **Step 3: Commit**

```
git add backend/MicroLIMS.Tests/IntegrationTests/KpiDashboardGoldenPostgresTests.cs
git commit -m "test(kpi): hand-calculated golden dataset for the dashboard on PostgreSQL

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 14: Frontend types and service (no UI)

**Files:**
- Create: `frontend/src/modules/kpi/types/kpiTypes.ts`
- Create: `frontend/src/modules/kpi/services/kpiDashboardService.ts`
- Test: `frontend/src/modules/kpi/services/kpiDashboardService.test.ts`

**Interfaces:**
- Consumes: `apiClient` from `frontend/src/services/apiClient.ts` (named export); the wire shapes of Task 1 (camelCase, enums as strings, dates as ISO strings).
- Produces (S3 and S4 import these exact names): all types below, `KpiQueryParams`, `KpiRecordParams`, and
  `kpiDashboardService = { dashboard(params), records(key, params), definitions(from, to) }` returning `KpiDashboard`, `KpiRecordPage`, `KpiDefinitions`.

- [ ] **Step 1: Write the failing test**

Create `frontend/src/modules/kpi/services/kpiDashboardService.test.ts`:
```ts
import { afterEach, describe, expect, it, vi } from "vitest";
import { apiClient } from "../../../services/apiClient";
import { kpiDashboardService } from "./kpiDashboardService";

vi.mock("../../../services/apiClient", () => ({
  apiClient: { get: vi.fn() },
}));

afterEach(() => vi.clearAllMocks());

describe("kpiDashboardService", () => {
  it("sends only the filters that are set and unwraps the envelope", async () => {
    const dashboard = { from: "2026-10-01T00:00:00Z", kpis: [] };
    vi.mocked(apiClient.get).mockResolvedValue({ data: { success: true, data: dashboard } } as never);

    const result = await kpiDashboardService.dashboard({
      from: "2026-10-01", to: "2026-10-31", category: "Water", location: "", testCode: undefined,
    });

    expect(result).toBe(dashboard);
    expect(apiClient.get).toHaveBeenCalledWith("/kpi/dashboard", {
      params: { from: "2026-10-01", to: "2026-10-31", category: "Water" },
    });
  });

  it("sends analystId only when it is set", async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: { success: true, data: {} } } as never);

    await kpiDashboardService.dashboard({ from: "2026-10-01", to: "2026-10-31", analystId: 7 });
    expect(vi.mocked(apiClient.get).mock.calls[0][1]).toEqual({ params: { from: "2026-10-01", to: "2026-10-31", analystId: 7 } });

    await kpiDashboardService.dashboard({ from: "2026-10-01", to: "2026-10-31" });
    expect(vi.mocked(apiClient.get).mock.calls[1][1]).toEqual({ params: { from: "2026-10-01", to: "2026-10-31" } });
  });

  it("requests the records of one KPI with paging", async () => {
    const page = { key: "right-first-time", page: 2, pageSize: 25, total: 60, items: [] };
    vi.mocked(apiClient.get).mockResolvedValue({ data: { success: true, data: page } } as never);

    const result = await kpiDashboardService.records("right-first-time", {
      from: "2026-10-01", to: "2026-10-31", testCode: "TAMC", page: 2, pageSize: 25,
    });

    expect(result).toBe(page);
    expect(apiClient.get).toHaveBeenCalledWith("/kpi/right-first-time/records", {
      params: { from: "2026-10-01", to: "2026-10-31", testCode: "TAMC", page: 2, pageSize: 25 },
    });
  });

  it("requests the definitions for the chosen period", async () => {
    const defs = { kpis: [], profilesInForce: [] };
    vi.mocked(apiClient.get).mockResolvedValue({ data: { success: true, data: defs } } as never);

    expect(await kpiDashboardService.definitions("2026-10-01", "2026-10-31")).toBe(defs);
    expect(apiClient.get).toHaveBeenCalledWith("/kpi/definitions", { params: { from: "2026-10-01", to: "2026-10-31" } });
  });
});
```

- [ ] **Step 2: Run, expect failure**

From `E:\MicroLIMS\MicroLIMS\frontend`:
```
npx vitest run src/modules/kpi/services/kpiDashboardService.test.ts
```
Expected: FAIL, `Failed to resolve import "./kpiDashboardService"`.

- [ ] **Step 3: Implement**

Create `frontend/src/modules/kpi/types/kpiTypes.ts`:
```ts
// Mirrors backend/MicroLIMS.Application/Services/Kpi/KpiTypes.cs (camelCase, enums as strings).
// Ratios for unit "%" are fractions (1 = 100%); the page formats them.
export type KpiDirection = "HigherIsBetter" | "LowerIsBetter" | "Neutral";
export type KpiGroup = "Headline" | "Flow" | "Quality" | "Micro" | "Workload";
export type KpiStatus = "Ok" | "NoData" | "Partial";

export interface KpiPrior { value: number | null; n: number; numerator: number | null; denominator: number | null }
export interface KpiSeriesPoint { label: string; group: string | null; at: string | null; value: number | null; n: number; secondary: number | null }
export interface KpiGap { code: string; message: string; count: number }
export interface KpiResult {
  value: number | null; numerator: number | null; denominator: number | null; n: number; prior: KpiPrior | null;
  status: KpiStatus; tooFewRecords: boolean; gaps: KpiGap[]; series: KpiSeriesPoint[];
  extras: Record<string, number | null>; flags: string[];
}
export interface KpiEntry {
  key: string; name: string; unit: string; direction: KpiDirection; group: KpiGroup; order: number;
  result: KpiResult | null; error: string | null;
}
// `to` and `priorTo` are exclusive upper bounds (the selected end date plus one day, 00:00 UTC).
export interface KpiDashboard { from: string; to: string; priorFrom: string; priorTo: string; generatedAt: string; kpis: KpiEntry[] }
export interface KpiDefinition {
  key: string; name: string; unit: string; direction: KpiDirection; group: KpiGroup; order: number;
  definition: string; clockEvents: string; population: string; source: string;
}
export interface ProfileInForce { workflowType: string; testCode: string | null; workUnits: number | null; targetAnalystDays: number | null; effectiveFrom: string; versionId: number }
export interface KpiDefinitions { kpis: KpiDefinition[]; profilesInForce: ProfileInForce[] }
export interface KpiRecord {
  sampleId: number | null; testOrderId: number | null; controlNumber: string | null; testCode: string | null; category: string | null;
  analystName: string | null; eventAt: string | null; value: number | null; unit: string | null;
  inclusion: "Numerator" | "Denominator" | "Excluded"; note: string | null;
}
export interface KpiRecordPage { key: string; page: number; pageSize: number; total: number; items: KpiRecord[] }
```

Create `frontend/src/modules/kpi/services/kpiDashboardService.ts`:
```ts
import { apiClient } from "../../../services/apiClient";
import type { KpiDashboard, KpiDefinitions, KpiRecordPage } from "../types/kpiTypes";

export interface KpiQueryParams {
  from: string; // yyyy-MM-dd, inclusive
  to: string;   // yyyy-MM-dd, inclusive
  category?: string;
  location?: string;
  testCode?: string;
  analystId?: number;
}

export interface KpiRecordParams extends KpiQueryParams {
  page?: number;
  pageSize?: number;
}

interface Envelope<T> { success: boolean; data: T }

// Send only what the user set: the server reads a missing analystId as "lab level".
function clean(params: object): Record<string, unknown> {
  return Object.fromEntries(Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== ""));
}

export const kpiDashboardService = {
  dashboard: (params: KpiQueryParams) =>
    apiClient.get<Envelope<KpiDashboard>>("/kpi/dashboard", { params: clean(params) }).then((r) => r.data.data),

  records: (key: string, params: KpiRecordParams) =>
    apiClient.get<Envelope<KpiRecordPage>>(`/kpi/${encodeURIComponent(key)}/records`, { params: clean(params) }).then((r) => r.data.data),

  definitions: (from: string, to: string) =>
    apiClient.get<Envelope<KpiDefinitions>>("/kpi/definitions", { params: { from, to } }).then((r) => r.data.data),
};
```

- [ ] **Step 4: Run, expect pass; type-check and lint**

```
npx vitest run src/modules/kpi/services/kpiDashboardService.test.ts
npx tsc --noEmit -p tsconfig.json
npm run lint
```
Expected: 4 tests PASS; `tsc` exits 0; lint shows no new errors for the new files.

- [ ] **Step 5: Commit**

From `E:\MicroLIMS\MicroLIMS`:
```
git add frontend/src/modules/kpi/types/kpiTypes.ts frontend/src/modules/kpi/services/kpiDashboardService.ts frontend/src/modules/kpi/services/kpiDashboardService.test.ts
git commit -m "feat(kpi): frontend types and service for the KPI dashboard contract

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Slice verification (run before calling S2 done)

From `E:\MicroLIMS\MicroLIMS`:
```
dotnet build backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --artifacts-path "$TEMP/kpi-art"
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art"
MICROLIMS_TEST_POSTGRES=<value PostgresTestConfiguration.cs expects> dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~Postgres"
```
Expected: build succeeds; the full suite is green (with the Postgres variable unset the `[PostgresFact]` tests are skipped, never failed); the Postgres run is green, including `KpiViewTeamPermissionMigrationPostgresTests`, `KpiDashboardGoldenPostgresTests`, `PermissionEnforcementMigrationPostgresTests` and `FreshDatabaseSeedPostgresTests`.

Frontend, from `E:\MicroLIMS\MicroLIMS\frontend`:
```
npx vitest run src/modules/kpi
npx tsc --noEmit -p tsconfig.json
npm run lint
```
Expected: green. Spec coverage check: section 3.1 (Tasks 1, 2), 3.2 (Tasks 2, 11, 14), 3.3 (Tasks 1, 2, 4-10), 4.1 tiles 1-8 (Tasks 4-9), 4.3 (Tasks 3, 5, 8), 4.4 (Task 8), server-side scope and D8 (Tasks 2, 11), section 8 unit and Postgres tests (all tasks, 13).

## Notes for the human reviewer (deviations and limits)

- **Contract additions (additive):** `KpiCore` record and `KpiScope` helpers beyond the two filter helpers; `AnalystStageTarget`; `KpiDashboardService` takes an optional `ILogger`; `TestServiceFactory.KpiDashboard` takes an optional scope service and there is a `BuildKpiRegistry` helper (a method called `KpiRegistry` would shadow the type inside the factory class); test-only `StubSectionScope`.
- **`To` is exclusive** in `KpiQuery` and `KpiDashboardDto` (selected end date + 1 day). S4's fixtures already assume this.
- **Percent values are fractions**; unit `%`. `TooFewRecords` follows the contract literally (`N < 5` for any `%` KPI, including `N = 0`, where the status is `NoData` anyway).
- **Spec deviations carried out, as the contract directs:** tile 1 ends at `Sample.ApprovedAt` and excludes retest clones; tiles 4 and 5 are decision-based on `SampleSectionSignoff` (tile 5 uses `OosGroupCode`, since nothing writes `OOSInvestigation`, and tile 4 is a subset of tile 5); tile 2 starts the analyst stage at `PreparedAt` and reports unjudgeable samples as a gap; tile 3 uses signoff approval time and `TestOrder` counts and excludes Observation tests; tile 6 counts Pending/InProgress only, with review/approval backlog in `backlog-aging`; tile 7 excludes media-challenge incubations; tile 8 filters `Location` on `ResultRecord.SubjectName`, uses constants for the adverse-trend limits and only flags with at least 5 results.
- **Choices the spec left open:** `OosGroupCode` is sample-level, so a lab-1-only viewer still counts a lab-2 OOS (commented in `OosRateKpi`); the alert-level rate counts `AlertLevel` only and the action-level rate `ActionLevel` only; `wip-backlog`, `backlog-aging`, `stage-tat` and `returns-by-test` carry no prior; `stage-tat` has `Value = null` by design (read the series); `returns-by-test` series are ordered by test code, never by count or person; analyst names in record lists are hidden from viewers without `Kpi.ViewTeam` (D8).
- **`DashboardService`** keeps the 7-day rule through `KpiService.GetOverdueAnalystStageSamplesAsync` (see the section above and the pinning test in Task 3).
- **Migration hazard:** the `SeedKpiViewTeamPermission` Designer file embeds the full model including the uncommitted Media/Cryovial work; commit that work first if possible. The migration must not touch the model snapshot.
- **Test clock:** KPI classes read the clock from `KpiQuery.Now` (set once by the service from `TimeProvider`), so unit tests fix time with `KpiTestData.Now` and the dashboard-level tests with `FixedTimeProvider`. Review-workflow event timestamps are deliberately not used anywhere, because they ignore `TimeProvider`.
