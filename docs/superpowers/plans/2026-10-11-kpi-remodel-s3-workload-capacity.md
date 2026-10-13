# KPI Remodel S3 - Work-Unit Analyst Measures and Capacity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.
> **Repository state note (checked 2026-10-13, see `2026-10-11-kpi-remodel-index.md`):** commit `570cf02a` has since committed the Media/Cryovial migration `20261011103646_...` and its snapshot hunks, so the "UNTRACKED migration / dirty snapshot" statements below describe the earlier state. Only `AnalystKpiTab.tsx`, `AnalystKpiService.ts` and `reportingTypes.ts` carry the uncommitted 2026-10-11 KPI page patch. Always re-run `git status` first; if shared files are dirty with someone else's hunks, the `git add -p` protocol in these plans still applies.

**Goal:** Give each analyst a capacity-versus-load picture (delivered units, utilisation, forward load, weighted on-time, weighted right-first-time) computed server-side from the S1 work-unit profiles, with per-section capacity settings and entered availability, and with each analyst able to read only their own row.

**Architecture:** Two small configuration tables (`KpiSettings` per section, `AnalystAvailabilities`) plus three Application services in `MicroLIMS.Application.Services.Kpi`: `KpiSettingsService` and `AnalystAvailabilityService` (CRUD with audit, no e-signature) and `AnalystWorkloadService` (pure read model over `TestOrder`, `SampleSectionSignoff`, `TestReturnEvent` and the S1 `WorkflowProfileResolver`). A new `KpiWorkloadController` exposes them under `Kpi.View`; writes add `Kpi.ConfigureProfiles`; the "other analysts" view needs `Kpi.ViewTeam` and is enforced in the service, not the UI.

**Tech Stack:** ASP.NET Core / EF Core (PostgreSQL, InMemory for unit tests), xUnit, React + TypeScript (types and service only), Vitest.

**Spec:** `docs/superpowers/specs/2026-10-11-kpi-performance-remodel-design.md` sections 4.2, 4.5, 6 and decisions D6, D8, D9; shared contract section 4 (slice S3) and SPEC DEVIATIONS 4, 5, 13, 14, 16.

**Depends on (must already be merged):** S1 (`WorkflowProfileVersion`, `WorkflowProfileResolver`, `WorkflowProfileService.LoadResolverAsync`, `PermissionConstants.KpiConfigureProfiles`, `FixedTimeProvider`, `TestServiceFactory.WorkflowProfile`) and S2 (`KpiQuery`, `KpiGap`, `KpiRecordDto`, `KpiRecordPageDto`, `KpiConstants`, `AnalystStageTarget`, `PermissionConstants.KpiViewTeam`, frontend `kpiTypes.ts`).

## Global Constraints

- Unit: 1 work unit = 1 standard analyst-hour. Capacity units for a period = sum over working days of `HoursPerAnalystDay x availability fraction` (fraction defaults to 1.0 on days with no availability row), so units and capacity compare directly.
- Defaults when a section has no `KpiSettings` row: `HoursPerAnalystDay = 6.0`, `WorkingWeekdays = 31` (Mon-Fri), `LoadHorizonDays = 5`; the result then carries `CapacityUsesDefaults = true`. Bounds: hours `0 < x <= 24` (numeric(4,2)), weekdays `1..127`, horizon `1..30`.
- Weekday bit for a date `d` is `1 << (((int)d.DayOfWeek + 6) % 7)` (bit0 = Monday ... bit6 = Sunday).
- An analyst uses the `KpiSetting` of their lowest-Id accessible section that has a row, else the defaults. Each analyst appears once even with several sections.
- `KpiConstants.MinSampleForRate = 5`: a weighted on-time or weighted right-first-time rate is `null` for an analyst with fewer than 5 tests behind it, and a gap says so. `KpiConstants.DefaultTargetDays = 7` is the fallback analyst-stage target.
- Every ratio (utilisation, forward load, weighted rates) is a fraction (1.0 = 100%), never a percentage.
- A test is *weighted* only if S1 resolves `WorkUnits`; unweighted tests are excluded from numerators and denominators and reported as a gap. They are never defaulted to 1.0.
- Delivered = `TestOrder.Status == Approved`, `!IsSuperseded`, `AssignedAnalystId != null`, whose section signoff has `Status == Approved`, `ApprovalDecision == Approve` and `ApprovedAt` inside the period. Profile resolution time is that signoff `ApprovedAt`; open tests resolve at `KpiQuery.Now`. Count `TestOrder` rows, never `ResultRecord` rows.
- Analyst = `TestOrder.AssignedAnalystId` (final assignee). Rows are `Role.Type == Analyst` and active users in the viewer's scope, plus any assignee with delivered units in the period. Unassigned open tests are a gap, never attributed.
- Observation tests are excluded from right-first-time (they cannot generate a `TestReturnEvent`); returns are matched by `TestOrderId`.
- Analyst-stage on-time: start = `SamplePreparation.PreparedAt`; end = the section signoff `SubmittedForReviewAt`; deadline = start + max over the sample's non-superseded, non-voided, non-cancelled tests in that section of `(resolved TargetAnalystDays ?? 7)`. A sample with no preparation row or no submit time is not judged and is reported as a gap.
- No ranking (D6): rows are sorted by `FullName` ascending with `StringComparer.OrdinalIgnoreCase` on the server; nothing sorts or colours by a performance value.
- Own numbers only (D8): without `Kpi.ViewTeam` the services return only the caller's row/records and throw `UnauthorizedAccessException` (403) for any other `analystId`/`userId`. Scope always comes from `IUserSectionScopeService`; `null` = unrestricted, empty list = sees nothing.
- Writes: `Kpi.ConfigureProfiles` (plus class-level `Kpi.View`), section access required, a reason is required, an `AuditActionCategory.Configuration` semantic event is recorded, no e-signature (neither is a weight). There is no delete: a correction is a PUT.
- Status codes: validation `InvalidOperationException` (400); `NotFoundException` 404; `UnauthorizedAccessException` 403; stale `If-Match` -> `DbUpdateConcurrencyException` 409. Request field names are identical in C# and TypeScript and a test pins them (SPEC DEVIATION 16).
- JSON camelCase, enums as strings, date-only fields `"yyyy-MM-dd"`, every response `ApiResponse<object>.Ok(...)`.
- User-id columns are named `...UserId` and registered in `UserReferenceRegistry` under `// KPI remodel - S3` with disposition `Blocks`.
- Repo rules: Domain/Application free of UI/DB concerns; stage ONLY the files a task names (`git add <explicit paths>`, never `git add -A`, `git add .` or `git commit -a`; about 41 unrelated files are dirty, including an UNTRACKED migration `20261011103646_AddMediaStorageEntriesAndCryovialStorageEquipment` and a modified `MicroLimsDbContextModelSnapshot.cs`).
- Tests are always run with `--artifacts-path` because `MicroLIMS.API` may be running and locks `bin`: `dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "<filter>"`.
- Every commit message ends with the footer line `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>` (given as a second `-m` below).
- ripgrep/grep are not installed: use the Grep tool, Glob or `git grep`.

### Shared-file staging protocol (applies to every commit step that lists a "shared file")

Shared files: `IMicroLimsDbContext.cs`, `MicroLimsDbContext.cs`, `MicroLimsDbContextModelSnapshot.cs`, `UserReferenceRegistry.cs`, `ServiceCollectionExtensions.cs`, `TestServiceFactory.cs`, `authorization-matrix.txt`. Before the first edit of such a file run `git status --short -- <file>`. If it is already ` M` (another feature's uncommitted hunks), do not `git add <file>`; either ask the user to commit the Media/Cryovial work first (preferred) or stage only your hunks with `git add -p <file>` in the user's terminal tab (`run_in_terminal`), then confirm with `git diff --cached -- <file>` that no Media/Cryovial/Cryovial-storage line is staged. The snapshot is always dirty-by-other-work today, so it always needs this treatment.

## Review Focus

The inputs most likely to bite a real user that the spec implies but does not spell out, most likely first. Each is pinned by a named test in the owning task.

1. **Analyst on full leave (or zero forward capacity) who still has open tests.** Expected: `ForwardLoad = null`, `ForwardLoadOverCapacity = true`, no divide-by-zero, no "0%". Pinned by `Row_OnFullLeaveWithOpenTests_ForwardLoadIsNullAndOverCapacity` (Task 5).
2. **Period that is weekend-only or ends in the future.** Expected: weekend-only gives capacity 0 and `Utilisation = null`; future days add no capacity (capacity is capped at today). Pinned by `Capacity_WeekendOnlyPeriod_IsZeroAndUtilisationIsNull` and `Capacity_PeriodEndingInFuture_CountsOnlyDaysUpToToday` (Task 5).
3. **Analyst who belongs to two sections, only one with a settings row.** Expected: one row; the lowest-Id accessible section *that has a row* supplies the hours. Pinned by `Analyst_InTwoSections_AppearsOnce_UsingLowestSectionWithSettings` (Task 5).
4. **Sample whose other tests have no resolvable target.** Expected: the 7-day fallback is used for those siblings and the sample deadline is the max, so an analyst is not marked late against a 5-day target while a 7-day test is still allowed. Pinned by `OnTime_SampleWithUnresolvedSiblingTarget_UsesSevenDayFallback` (Task 6).
5. **Section Head (or any `Kpi.View` holder) without `Kpi.ViewTeam` who is not an analyst.** Expected: an empty row list with `OnlyOwnRow = true`, never the whole team and never a 500. Pinned by `Workload_ViewerWithoutViewTeamWhoIsNotAnAnalyst_GetsNoRows` (Task 8).
8. **UTC window edges.** Expected: a signoff at exactly `from 00:00:00Z` is counted, one at exactly `to + 1 day 00:00:00Z` is not (half-open window). Pinned by `Delivered_IncludesExactlyFromMidnight_ExcludesExactlyToPlusOneDayMidnight` (Task 5).
9. **Two analysts on one sample and section.** Expected: both inherit the same sample-level on-time verdict, and the sample deadline uses the largest target across both analysts' tests. Pinned by `TwoAnalystsOnOneSample_BothInheritTheSampleOnTimeVerdict_AndSiblingTargetUsesBothTests` (Task 6).
10. **Retest clone sample (OriginSampleId set, no SamplePreparation).** Expected: it earns units like any test and is reported as unjudged for on time, not dropped. Pinned by `RetestCloneSample_EarnsUnits_AndIsReportedUnjudged` (Task 6).

6. **Profiles still Draft (every test unweighted).** Expected: `Utilisation = null` and `ForwardLoad = null` (shown as "-", with the unweighted gap), never 0%, because low output must not read as failure. Pinned by `Utilisation_WhenEveryDeliveredTestIsUnweighted_IsNullNotZero` and `ForwardLoad_WhenEveryOpenTestIsUnweighted_IsNullAndNotOverCapacity` (Task 5).
7. **Category, location or test filter set.** Expected: capacity is the analyst's whole availability, so `Utilisation`, `ForwardLoad` and `ForwardLoadOverCapacity` are null/false and a `CAPACITY_NOT_FILTERED` gap is raised; units are still shown. Pinned by `Filtered_UtilisationAndForwardLoadAreNull_WithCapacityNotFilteredGap` (Task 5).

## File Structure

| Path | Action | Responsibility |
|---|---|---|
| `backend/MicroLIMS.Domain/Entities/KpiSetting.cs` | Create | Per-section capacity settings row (versioned, edited via form). |
| `backend/MicroLIMS.Domain/Entities/AnalystAvailability.cs` | Create | One leave/training/secondment window for one analyst. |
| `backend/MicroLIMS.Persistence/Configurations/KpiSettingConfiguration.cs` | Create | Column precision, unique `SectionId`, FK Restrict, check constraints. |
| `backend/MicroLIMS.Persistence/Configurations/AnalystAvailabilityConfiguration.cs` | Create | Column types, `(UserId, FromDate)` index, FK Restrict, check constraints. |
| `backend/MicroLIMS.Persistence/Migrations/<ts>_AddKpiSettingsAndAnalystAvailability.cs` (+ Designer) | Create (generated) | Creates the two tables; `Down` drops them. |
| `backend/MicroLIMS.Persistence/Migrations/MicroLimsDbContextModelSnapshot.cs` | Modify (generated) | Snapshot; shared-file protocol applies. |
| `backend/MicroLIMS.Application/Abstractions/Persistence/IMicroLimsDbContext.cs` | Modify | Two new `DbSet`s. |
| `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs` | Modify | Two new `DbSet`s. |
| `backend/MicroLIMS.Application/Services/UserReferenceRegistry.cs` | Modify | Three `Blocks` entries. |
| `backend/MicroLIMS.Application/Services/Kpi/CapacityCalculator.cs` | Create | Pure working-day/capacity maths, `KpiSettingDefaults`, `KpiUserScope` helper. |
| `backend/MicroLIMS.Application/Services/Kpi/WorkloadDtos.cs` | Create | All S3 DTO/request records. |
| `backend/MicroLIMS.Application/Services/Kpi/KpiSettingsService.cs` | Create | Read/update per-section settings with audit and If-Match. |
| `backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs` | Create | List/create/correct availability with overlap rule, scope and audit. |
| `backend/MicroLIMS.Application/Services/Kpi/AnalystWorkloadService.cs` | Create | The workload read model and the records behind it; D8 enforcement. |
| `backend/MicroLIMS.API/Controllers/KpiWorkloadController.cs` | Create | Seven routes under `api/kpi`. |
| `backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs` | Modify | Register the three services. |
| `backend/MicroLIMS.Tests/TestServiceFactory.cs` | Modify | `AnalystWorkload`, `KpiSettings`, `AnalystAvailability` factories. |
| `backend/MicroLIMS.Tests/Fixtures/ScratchPostgresDatabase.cs` | Create | Throw-away Postgres database helper for the two Postgres tests. |
| `backend/MicroLIMS.Tests/UnitTests/Kpi/WorkloadTestData.cs` | Create | Shared seed/query helpers for S3 unit tests. |
| `backend/MicroLIMS.Tests/UnitTests/Kpi/CapacityCalculatorTests.cs` | Create | Working days, partial availability, horizon. |
| `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiSettingsModelTests.cs` | Create | Model-level checks that run without Postgres. |
| `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiSettingsServiceTests.cs` | Create | Settings service behaviour and request field names. |
| `backend/MicroLIMS.Tests/UnitTests/Kpi/AnalystAvailabilityServiceTests.cs` | Create | Availability rules, scope, audit. |
| `backend/MicroLIMS.Tests/UnitTests/Kpi/AnalystWorkloadServiceTests.cs` | Create | Units, capacity, load, rates, gaps, records. |
| `backend/MicroLIMS.Tests/UnitTests/Kpi/WorkloadOwnRowOnlyTests.cs` | Create | D8 through every endpoint. |
| `backend/MicroLIMS.Tests/IntegrationTests/KpiSettingsMigrationPostgresTests.cs` | Create | Constraints and Down on real Postgres. |
| `backend/MicroLIMS.Tests/IntegrationTests/AnalystWorkloadGoldenPostgresTests.cs` | Create | Hand-calculated golden dataset on real Postgres. |
| `backend/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt` | Modify (regenerated) | Seven new `KpiWorkloadController` lines. |
| `frontend/src/modules/kpi/types/workloadTypes.ts` | Create | TypeScript mirrors of the DTOs. |
| `frontend/src/modules/kpi/services/kpiWorkloadService.ts` | Create | Plain-object service over `apiClient`. |
| `frontend/src/modules/kpi/services/kpiWorkloadService.test.ts` | Create | Param mapping and `If-Match` pinned. |

---

### Task 1: Capacity maths

**Files:**
- Create: `backend/MicroLIMS.Application/Services/Kpi/CapacityCalculator.cs`
- Test: `backend/MicroLIMS.Tests/UnitTests/Kpi/CapacityCalculatorTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces (namespace `MicroLIMS.Application.Services.Kpi`):
  - `static class CapacityCalculator { int WeekdayBit(DateOnly d); int WorkingDays(DateOnly from, DateOnly toInclusive, int weekdayMask); decimal CapacityUnits(DateOnly from, DateOnly toInclusive, decimal hoursPerDay, int weekdayMask, IReadOnlyList<(DateOnly From, DateOnly To, decimal Fraction)> availability); DateOnly HorizonEnd(DateOnly start, int workingDays, int weekdayMask) }` (`WeekdayBit` and `HorizonEnd` are additions to the contract).
  - `static class KpiSettingDefaults { const decimal HoursPerAnalystDay = 6.0m; const int WorkingWeekdays = 31; const int LoadHorizonDays = 5; }`
  - `internal static class KpiUserScope { Task<bool> InScopeAsync(IUserSectionScopeService scope, int userId, IReadOnlyList<int>? viewerSectionIds, CancellationToken ct) }`

- [ ] **Step 1: Write the failing test**

Create `backend/MicroLIMS.Tests/UnitTests/Kpi/CapacityCalculatorTests.cs`:

```csharp
using MicroLIMS.Application.Services.Kpi;
using Xunit;

namespace MicroLIMS.Tests.UnitTests.Kpi;

// Monday 5 Oct 2026 .. Sunday 11 Oct 2026 is the reference week. Mask 31 = Mon-Fri, 63 = Mon-Sat, 64 = Sunday only.
public class CapacityCalculatorTests
{
    private const int MonFri = 31;
    private static readonly DateOnly Mon = new(2026, 10, 5);
    private static readonly DateOnly Sun = new(2026, 10, 11);
    private static readonly IReadOnlyList<(DateOnly From, DateOnly To, decimal Fraction)> NoLeave =
        Array.Empty<(DateOnly From, DateOnly To, decimal Fraction)>();

    private static (DateOnly From, DateOnly To, decimal Fraction) Leave(int fromDay, int toDay, decimal fraction) =>
        (new DateOnly(2026, 10, fromDay), new DateOnly(2026, 10, toDay), fraction);

    [Theory]
    [InlineData(5, 1)]   // Monday = bit0
    [InlineData(6, 2)]
    [InlineData(9, 16)]  // Friday = bit4
    [InlineData(10, 32)] // Saturday
    [InlineData(11, 64)] // Sunday = bit6
    public void WeekdayBit_MapsMondayToBit0_AndSundayToBit6(int day, int expected) =>
        Assert.Equal(expected, CapacityCalculator.WeekdayBit(new DateOnly(2026, 10, day)));

    [Fact]
    public void WorkingDays_FullWeek_MonToFri_IsFive() =>
        Assert.Equal(5, CapacityCalculator.WorkingDays(Mon, Sun, MonFri));

    [Fact]
    public void WorkingDays_SiteWorksSaturdays_IsSix() =>
        Assert.Equal(6, CapacityCalculator.WorkingDays(Mon, Sun, 63));

    [Fact]
    public void WorkingDays_WeekendOnlyRange_IsZero() =>
        Assert.Equal(0, CapacityCalculator.WorkingDays(new DateOnly(2026, 10, 10), Sun, MonFri));

    [Fact]
    public void WorkingDays_SundayOnlyWeek_CountsOneSunday() =>
        Assert.Equal(1, CapacityCalculator.WorkingDays(Mon, Sun, 64));

    [Fact]
    public void WorkingDays_EndBeforeStart_IsZero() =>
        Assert.Equal(0, CapacityCalculator.WorkingDays(Sun, Mon, MonFri));

    [Fact]
    public void CapacityUnits_FullWeekAtSixHours_IsThirty() =>
        Assert.Equal(30m, CapacityCalculator.CapacityUnits(Mon, Sun, 6.0m, MonFri, NoLeave));

    [Fact]
    public void CapacityUnits_HalfAvailableTuesdayAndWednesday_ReducesBySix() =>
        // Mon 6 + Tue 3 + Wed 3 + Thu 6 + Fri 6
        Assert.Equal(24m, CapacityCalculator.CapacityUnits(Mon, Sun, 6.0m, MonFri, new[] { Leave(6, 7, 0.5m) }));

    [Fact]
    public void CapacityUnits_ZeroFractionAllWeek_IsZero() =>
        Assert.Equal(0m, CapacityCalculator.CapacityUnits(Mon, Sun, 6.0m, MonFri, new[] { Leave(5, 11, 0m) }));

    [Fact]
    public void CapacityUnits_LeaveOutsideTheRange_IsIgnored() =>
        Assert.Equal(30m, CapacityCalculator.CapacityUnits(Mon, Sun, 6.0m, MonFri, new[] { Leave(12, 16, 0m) }));

    [Fact]
    public void CapacityUnits_LeaveOnANonWorkingDay_HasNoEffect() =>
        Assert.Equal(30m, CapacityCalculator.CapacityUnits(Mon, Sun, 6.0m, MonFri, new[] { Leave(10, 11, 0m) }));

    [Fact]
    public void CapacityUnits_LeaveStartingBeforeTheRange_CoversTheOverlap() =>
        // Leave Fri 2 Oct .. Tue 6 Oct at 0: Mon and Tue lost.
        Assert.Equal(18m, CapacityCalculator.CapacityUnits(Mon, Sun, 6.0m, MonFri,
            new[] { (new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 6), 0m) }));

    [Fact]
    public void HorizonEnd_FiveWorkingDaysFromWednesday_EndsNextTuesday() =>
        // Wed 14, Thu 15, Fri 16, Mon 19, Tue 20
        Assert.Equal(new DateOnly(2026, 10, 20), CapacityCalculator.HorizonEnd(new DateOnly(2026, 10, 14), 5, MonFri));

    [Fact]
    public void HorizonEnd_StartingOnAWeekend_CountsFromTheNextWorkingDay() =>
        Assert.Equal(new DateOnly(2026, 10, 12), CapacityCalculator.HorizonEnd(new DateOnly(2026, 10, 10), 1, MonFri));

    [Theory]
    [InlineData(0, 5)]   // no weekday selected
    [InlineData(31, 0)]  // no days requested
    public void HorizonEnd_InvalidInput_Throws(int mask, int days) =>
        Assert.Throws<ArgumentException>(() => CapacityCalculator.HorizonEnd(Mon, days, mask));
}
```

- [ ] **Step 2: Run it and see it fail**

Run: `dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~CapacityCalculatorTests"`
Expected: build FAILS with `CS0103`/`CS0246` "The name 'CapacityCalculator' does not exist" (type not created yet).

- [ ] **Step 3: Implement**

Create `backend/MicroLIMS.Application/Services/Kpi/CapacityCalculator.cs`:

```csharp
using MicroLIMS.Application.Interfaces;

namespace MicroLIMS.Application.Services.Kpi;

// Defaults used when a section has no KpiSettings row. The entity defaults in Domain repeat these literals
// because Domain cannot reference Application; a test pins that they agree.
public static class KpiSettingDefaults
{
    public const decimal HoursPerAnalystDay = 6.0m;
    public const int WorkingWeekdays = 31; // Mon-Fri
    public const int LoadHorizonDays = 5;
}

// Pure capacity maths (spec 4.5). 1 unit = 1 analyst-hour, so capacity and delivered units compare directly.
public static class CapacityCalculator
{
    // Bit0 = Monday ... bit6 = Sunday. DayOfWeek.Sunday is 0, so shift by 6 to land Monday on 0.
    public static int WeekdayBit(DateOnly d) => 1 << (((int)d.DayOfWeek + 6) % 7);

    public static int WorkingDays(DateOnly from, DateOnly toInclusive, int weekdayMask)
    {
        var n = 0;
        for (var d = from; d <= toInclusive; d = d.AddDays(1))
            if ((weekdayMask & WeekdayBit(d)) != 0) n++;
        return n;
    }

    // Availability windows for one user never overlap (rejected when saved), so the first match per day is the only match.
    public static decimal CapacityUnits(DateOnly from, DateOnly toInclusive, decimal hoursPerDay, int weekdayMask,
        IReadOnlyList<(DateOnly From, DateOnly To, decimal Fraction)> availability)
    {
        decimal total = 0;
        for (var d = from; d <= toInclusive; d = d.AddDays(1))
        {
            if ((weekdayMask & WeekdayBit(d)) == 0) continue;
            var fraction = 1m;
            foreach (var a in availability)
            {
                if (d >= a.From && d <= a.To) { fraction = a.Fraction; break; }
            }
            total += hoursPerDay * fraction;
        }
        return total;
    }

    // Last calendar day of a window that contains exactly `workingDays` working days, counting `start` itself.
    public static DateOnly HorizonEnd(DateOnly start, int workingDays, int weekdayMask)
    {
        if (workingDays < 1 || (weekdayMask & 127) == 0)
            throw new ArgumentException("A horizon needs at least one working day and at least one working weekday.");
        var counted = 0;
        for (var d = start; ; d = d.AddDays(1))
        {
            if ((weekdayMask & WeekdayBit(d)) != 0 && ++counted == workingDays) return d;
        }
    }
}

// A viewer may see an analyst only when their section scopes intersect. A null scope means unrestricted
// (system administrator); an administrator target has a null scope and is visible to everyone who may see anyone.
internal static class KpiUserScope
{
    public static async Task<bool> InScopeAsync(IUserSectionScopeService scope, int userId,
        IReadOnlyList<int>? viewerSectionIds, CancellationToken ct)
    {
        if (viewerSectionIds is null) return true;
        var theirs = await scope.GetAccessibleSectionIdsAsync(userId, ct);
        return theirs is null || theirs.Any(viewerSectionIds.Contains);
    }
}
```

- [ ] **Step 4: Run it and see it pass**

Run the Step 2 command. Expected: 20 passed (each `Theory` row counts), 0 failed.

- [ ] **Step 5: Commit**

```
git add backend/MicroLIMS.Application/Services/Kpi/CapacityCalculator.cs backend/MicroLIMS.Tests/UnitTests/Kpi/CapacityCalculatorTests.cs
git commit -m "feat(kpi): capacity maths for analyst workload" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Entities, configurations, DbSets, registry, migration

**Files:**
- Create: `backend/MicroLIMS.Domain/Entities/KpiSetting.cs`, `backend/MicroLIMS.Domain/Entities/AnalystAvailability.cs`
- Create: `backend/MicroLIMS.Persistence/Configurations/KpiSettingConfiguration.cs`, `backend/MicroLIMS.Persistence/Configurations/AnalystAvailabilityConfiguration.cs`
- Modify: `backend/MicroLIMS.Application/Abstractions/Persistence/IMicroLimsDbContext.cs` (next to the `WorkloadWeightHistory` DbSet, about line 95), `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs` (same place, about line 95), `backend/MicroLIMS.Application/Services/UserReferenceRegistry.cs` (end of the `All` list)
- Create (generated): migration `AddKpiSettingsAndAnalystAvailability` and its Designer; Modify (generated) `MicroLimsDbContextModelSnapshot.cs`
- Create: `backend/MicroLIMS.Tests/Fixtures/ScratchPostgresDatabase.cs`
- Test: `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiSettingsModelTests.cs`, `backend/MicroLIMS.Tests/IntegrationTests/KpiSettingsMigrationPostgresTests.cs`

**Interfaces:**
- Consumes: `IVersionedEntity`, `DocumentSection`, `User`, `MicroLimsDbContext`, `PostgresTestConfiguration` (`AdminConnectionString`, `NewTestDatabaseName()`, `BuildTestConnectionString(admin, name)`, `EnsureSafeToDrop(name, admin)`), `[PostgresFact]`.
- Produces: `KpiSetting`, `AnalystAvailability` (fields exactly as the contract); `IMicroLimsDbContext.KpiSettings` (`DbSet<KpiSetting>`), `IMicroLimsDbContext.AnalystAvailabilities` (`DbSet<AnalystAvailability>`); `ScratchPostgresDatabase.RunAsync(Func<string, Task>)` and `ScratchPostgresDatabase.NewContext(string connectionString)`.

- [ ] **Step 1: Record the shared-file baseline**

Run: `git status --short -- backend/MicroLIMS.Application/Abstractions/Persistence/IMicroLimsDbContext.cs backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs backend/MicroLIMS.Application/Services/UserReferenceRegistry.cs backend/MicroLIMS.Persistence/Migrations/MicroLimsDbContextModelSnapshot.cs backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs backend/MicroLIMS.Tests/TestServiceFactory.cs`
Note which are ` M`. Those need the shared-file staging protocol at commit time. Also run `ls backend/MicroLIMS.Persistence/Migrations | sort | tail -n 6` and confirm the newest migration is `20261011103646_AddMediaStorageEntriesAndCryovialStorageEquipment` or a later S1/S2 KPI migration (S1 `AddWorkflowProfileVersions`, S2 `SeedKpiViewTeamPermission`).

- [ ] **Step 2: Write the failing tests**

Create `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiSettingsModelTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests.Kpi;

// Model-level checks that run without Postgres. The check constraints themselves are exercised in KpiSettingsMigrationPostgresTests.
public class KpiSettingsModelTests
{
    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public void KpiSetting_HasUniqueSectionIndex_AndHoursPrecision()
    {
        using var db = NewDb();
        var et = db.Model.FindEntityType(typeof(KpiSetting))!;
        Assert.Contains(et.GetIndexes(), i => i.IsUnique && i.Properties.Single().Name == nameof(KpiSetting.SectionId));
        var hours = et.FindProperty(nameof(KpiSetting.HoursPerAnalystDay))!;
        Assert.Equal(4, hours.GetPrecision());
        Assert.Equal(2, hours.GetScale());
    }

    [Fact]
    public void AnalystAvailability_ReasonIsRequiredAndBounded_FractionIsNumeric32()
    {
        using var db = NewDb();
        var et = db.Model.FindEntityType(typeof(AnalystAvailability))!;
        var reason = et.FindProperty(nameof(AnalystAvailability.Reason))!;
        Assert.False(reason.IsNullable);
        Assert.Equal(500, reason.GetMaxLength());
        var fraction = et.FindProperty(nameof(AnalystAvailability.FractionAvailable))!;
        Assert.Equal(3, fraction.GetPrecision());
        Assert.Equal(2, fraction.GetScale());
    }

    [Fact]
    public void KpiSetting_EntityDefaults_AgreeWithKpiSettingDefaults()
    {
        var s = new KpiSetting();
        Assert.Equal(KpiSettingDefaults.HoursPerAnalystDay, s.HoursPerAnalystDay);
        Assert.Equal(KpiSettingDefaults.WorkingWeekdays, s.WorkingWeekdays);
        Assert.Equal(KpiSettingDefaults.LoadHorizonDays, s.LoadHorizonDays);
    }
}
```

Create `backend/MicroLIMS.Tests/Fixtures/ScratchPostgresDatabase.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Persistence.DbContext;
using Npgsql;

namespace MicroLIMS.Tests.Fixtures;

// A throw-away database for tests that need real Postgres constraints. The name is generated by
// PostgresTestConfiguration and validated before DROP, so a misconfigured environment cannot drop a real database.
public static class ScratchPostgresDatabase
{
    public static async Task RunAsync(Func<string, Task> body)
    {
        var admin = PostgresTestConfiguration.AdminConnectionString!;
        var name = PostgresTestConfiguration.NewTestDatabaseName();
        var cs = PostgresTestConfiguration.BuildTestConnectionString(admin, name);
        await Exec(admin, $"CREATE DATABASE \"{name}\"");
        try
        {
            await body(cs);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            PostgresTestConfiguration.EnsureSafeToDrop(name, admin);
            await Exec(admin, $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
        }
    }

    public static MicroLimsDbContext NewContext(string connectionString) =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseNpgsql(connectionString).Options);

    private static async Task Exec(string cs, string sql)
    {
        await using var c = new NpgsqlConnection(cs);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync();
    }
}
```

Create `backend/MicroLIMS.Tests/IntegrationTests/KpiSettingsMigrationPostgresTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

public class KpiSettingsMigrationPostgresTests
{
    private const string MigrationName = "AddKpiSettingsAndAnalystAvailability";

    private static async Task<bool> RejectedAsync(string cs, Action<MicroLimsDbContext> add)
    {
        await using var db = ScratchPostgresDatabase.NewContext(cs);
        add(db);
        try { await db.SaveChangesAsync(); return false; }
        catch (DbUpdateException) { return true; }
    }

    private static KpiSetting Setting(int sectionId, decimal hours = 6.0m, int mask = 31, int horizon = 5) => new()
    {
        SectionId = sectionId, HoursPerAnalystDay = hours, WorkingWeekdays = mask, LoadHorizonDays = horizon,
        UpdatedByUserId = 1, UpdatedAt = DateTime.UtcNow
    };

    private static AnalystAvailability Leave(DateOnly from, DateOnly to, decimal fraction) => new()
    {
        UserId = 1, FromDate = from, ToDate = to, FractionAvailable = fraction, Reason = "Training",
        EnteredByUserId = 1, EnteredAt = DateTime.UtcNow
    };

    [PostgresFact]
    public async Task Migration_EnforcesConstraints_AndDownDropsBothTables()
    {
        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            string before;
            await using (var db = ScratchPostgresDatabase.NewContext(cs))
            {
                var all = db.Database.GetMigrations().ToList();
                var target = all.Single(m => m.EndsWith("_" + MigrationName));
                before = all[all.IndexOf(target) - 1];
                await db.Database.MigrateAsync();

                // AddOrgSectionSegregation already inserted QC and MICRO into a migrated database; clear them so the explicit ids
                // and codes below cannot collide. Explicit ids on identity columns do not advance the sequence, which is fine here.
                await db.Database.ExecuteSqlRawAsync("DELETE FROM \"DocumentSections\"; DELETE FROM \"DocumentDepartments\";");

                db.DocumentDepartments.Add(new DocumentDepartment { Id = 1, Code = "QC", Name = "QC" });
                for (var i = 1; i <= 5; i++)
                    db.DocumentSections.Add(new DocumentSection { Id = i, Code = "S" + i, Name = "Section " + i, DepartmentId = 1 });
                db.Roles.Add(new Role { Id = 1, Type = RoleType.Analyst, Name = "Analyst" });
                db.Users.Add(new User { Id = 1, FullName = "A", Username = "a", RoleId = 1, PasswordHash = "x" });
                await db.SaveChangesAsync();
            }

            Assert.False(await RejectedAsync(cs, db => db.KpiSettings.Add(Setting(1))));
            Assert.True(await RejectedAsync(cs, db => db.KpiSettings.Add(Setting(1))));              // unique SectionId
            Assert.True(await RejectedAsync(cs, db => db.KpiSettings.Add(Setting(2, hours: 0m))));   // hours > 0
            Assert.True(await RejectedAsync(cs, db => db.KpiSettings.Add(Setting(3, hours: 24.5m)))); // hours <= 24
            Assert.True(await RejectedAsync(cs, db => db.KpiSettings.Add(Setting(4, mask: 0))));     // at least one weekday
            Assert.True(await RejectedAsync(cs, db => db.KpiSettings.Add(Setting(5, horizon: 31)))); // horizon <= 30

            var d = new DateOnly(2026, 10, 14);
            Assert.False(await RejectedAsync(cs, db => db.AnalystAvailabilities.Add(Leave(d, d.AddDays(2), 0.5m))));
            Assert.True(await RejectedAsync(cs, db => db.AnalystAvailabilities.Add(Leave(d.AddDays(2), d, 0.5m)))); // To >= From
            Assert.True(await RejectedAsync(cs, db => db.AnalystAvailabilities.Add(Leave(d, d, 1.5m))));            // fraction <= 1

            await using (var db = ScratchPostgresDatabase.NewContext(cs))
                await db.GetService<IMigrator>().MigrateAsync(before);

            await using var c = new NpgsqlConnection(cs);
            await c.OpenAsync();
            foreach (var table in new[] { "KpiSettings", "AnalystAvailabilities" })
            {
                await using var cmd = new NpgsqlCommand($"SELECT to_regclass('public.\"{table}\"')::text", c);
                Assert.True(await cmd.ExecuteScalarAsync() is null or DBNull, table + " should be gone after Down");
            }
        });
    }
}
```

- [ ] **Step 3: Run and see them fail**

Run: `dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~KpiSettingsModelTests|FullyQualifiedName~KpiSettingsMigrationPostgresTests"`
Expected: build FAILS (`KpiSetting`, `AnalystAvailability`, `db.KpiSettings` do not exist).

- [ ] **Step 4: Implement the entities**

Create `backend/MicroLIMS.Domain/Entities/KpiSetting.cs`:

```csharp
namespace MicroLIMS.Domain.Entities;

// Capacity assumptions for one laboratory section (spec 4.5). No row means the page falls back to the
// documented defaults and says so. The defaults below repeat KpiSettingDefaults in Application.
public class KpiSetting : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public int SectionId { get; set; }
    public DocumentSection? Section { get; set; }

    // 0 < x <= 24. Provisional until the Section Head confirms it.
    public decimal HoursPerAnalystDay { get; set; } = 6.0m;
    // bit0 = Monday ... bit6 = Sunday; 1..127. 31 = Mon-Fri.
    public int WorkingWeekdays { get; set; } = 31;
    // Working days looked ahead for forward load; 1..30.
    public int LoadHorizonDays { get; set; } = 5;

    public int UpdatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

Create `backend/MicroLIMS.Domain/Entities/AnalystAvailability.cs`:

```csharp
namespace MicroLIMS.Domain.Entities;

// Leave, training or secondment window for one analyst. Never deleted: a correction is an edit, and the
// automatic audit trail keeps the previous values. Windows of one user never overlap (enforced by the service).
public class AnalystAvailability
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }   // >= FromDate
    public decimal FractionAvailable { get; set; } // 0..1; 0 = absent, 0.5 = half-day
    public string Reason { get; set; } = string.Empty;
    public int EnteredByUserId { get; set; }
    public DateTime EnteredAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 5: Implement the configurations**

Create `backend/MicroLIMS.Persistence/Configurations/KpiSettingConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class KpiSettingConfiguration : IEntityTypeConfiguration<KpiSetting>
{
    public void Configure(EntityTypeBuilder<KpiSetting> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.HoursPerAnalystDay).HasPrecision(4, 2);

        builder.HasOne(s => s.Section)
            .WithMany()
            .HasForeignKey(s => s.SectionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => s.SectionId).IsUnique();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_KpiSettings_Hours", "\"HoursPerAnalystDay\" > 0 AND \"HoursPerAnalystDay\" <= 24");
            t.HasCheckConstraint("CK_KpiSettings_Weekdays", "\"WorkingWeekdays\" BETWEEN 1 AND 127");
            t.HasCheckConstraint("CK_KpiSettings_Horizon", "\"LoadHorizonDays\" BETWEEN 1 AND 30");
        });
    }
}
```

Create `backend/MicroLIMS.Persistence/Configurations/AnalystAvailabilityConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class AnalystAvailabilityConfiguration : IEntityTypeConfiguration<AnalystAvailability>
{
    public void Configure(EntityTypeBuilder<AnalystAvailability> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.FromDate).HasColumnType("date");
        builder.Property(a => a.ToDate).HasColumnType("date");
        builder.Property(a => a.FractionAvailable).HasPrecision(3, 2);
        builder.Property(a => a.Reason).IsRequired().HasMaxLength(500);

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.UserId, a.FromDate });

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_AnalystAvailabilities_Range", "\"ToDate\" >= \"FromDate\"");
            t.HasCheckConstraint("CK_AnalystAvailabilities_Fraction", "\"FractionAvailable\" BETWEEN 0 AND 1");
        });
    }
}
```

- [ ] **Step 6: Register the DbSets and the user columns**

Use Grep for `WorkloadWeightHistor` in both context files and add directly after that line.

In `backend/MicroLIMS.Application/Abstractions/Persistence/IMicroLimsDbContext.cs`:

```csharp
    DbSet<KpiSetting> KpiSettings { get; }
    DbSet<AnalystAvailability> AnalystAvailabilities { get; }
```

In `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs`:

```csharp
    public DbSet<KpiSetting> KpiSettings => Set<KpiSetting>();
    public DbSet<AnalystAvailability> AnalystAvailabilities => Set<AnalystAvailability>();
```

In `backend/MicroLIMS.Application/Services/UserReferenceRegistry.cs`, read the last 15 lines and add these lines inside the `All` list, after the last existing `// KPI remodel - S<n>` block (or after the last entry if S1/S2 have none), before the closing `};`:

```csharp
        // KPI remodel - S3
        new UserReferenceEntry(typeof(KpiSetting), nameof(KpiSetting.UpdatedByUserId), UserReferenceDisposition.Blocks, "No DB FK - KPI capacity settings provenance"),
        new UserReferenceEntry(typeof(AnalystAvailability), nameof(AnalystAvailability.UserId), UserReferenceDisposition.Blocks, "Analyst availability history; DB FK Restrict"),
        new UserReferenceEntry(typeof(AnalystAvailability), nameof(AnalystAvailability.EnteredByUserId), UserReferenceDisposition.Blocks, "No DB FK - availability entry provenance"),
```

- [ ] **Step 7: Run the unit tests and the registry guards**

Run: `dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~KpiSettingsModelTests|FullyQualifiedName~UserDeletionTests"`
Expected: all pass, including `UserReferenceRegistry_AccountsForEveryUserIdNamedPropertyInTheModel` and `UserReferenceRegistry_EveryEntryMatchesARealIntProperty`.

- [ ] **Step 8: Generate and review the migration**

Run: `dotnet ef migrations add AddKpiSettingsAndAnalystAvailability --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API --configuration Release`

Review checklist (use the Grep tool on the new `.cs`, not shell grep):
1. File name sorts after the newest existing migration: `ls backend/MicroLIMS.Persistence/Migrations | sort | tail -n 4` must list `..._AddKpiSettingsAndAnalystAvailability.cs` last (after `20261011103646_...` and after S1/S2 KPI migrations). If not, delete the generated files and the snapshot change (never `git checkout` the snapshot: it holds other work; remove only your own hunks) and regenerate.
2. Grep pattern `AlterColumn|DropColumn|DropTable|Media|Cryovial` in the new `.cs`: expect no match in `Up()`. `Up()` must contain exactly two `CreateTable` calls (`KpiSettings`, `AnalystAvailabilities`), their `CreateIndex` calls and the five check constraints; `xmin` columns appear as `table.Column<uint>(type: "xid", rowVersion: true, nullable: false)` only on `KpiSettings` (the versioned one) and on `AnalystAvailabilities` (global shadow token); `FromDate`/`ToDate` are `type: "date"`.
3. `Down()` drops `AnalystAvailabilities` then `KpiSettings`.

- [ ] **Step 9: Run the Postgres migration test**

Run: `MICROLIMS_TEST_POSTGRES=<value PostgresTestConfiguration.cs expects> dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~KpiSettingsMigrationPostgresTests"`
Expected: 1 passed (not skipped). If it is skipped, the environment variable is not set; set it before continuing.

- [ ] **Step 10: Commit**

Apply the shared-file staging protocol to `IMicroLimsDbContext.cs`, `MicroLimsDbContext.cs`, `UserReferenceRegistry.cs` and `MicroLimsDbContextModelSnapshot.cs`; confirm `git diff --cached` contains no Media/Cryovial lines.

```
git add backend/MicroLIMS.Domain/Entities/KpiSetting.cs backend/MicroLIMS.Domain/Entities/AnalystAvailability.cs backend/MicroLIMS.Persistence/Configurations/KpiSettingConfiguration.cs backend/MicroLIMS.Persistence/Configurations/AnalystAvailabilityConfiguration.cs backend/MicroLIMS.Persistence/Migrations/*_AddKpiSettingsAndAnalystAvailability.cs backend/MicroLIMS.Persistence/Migrations/*_AddKpiSettingsAndAnalystAvailability.Designer.cs backend/MicroLIMS.Tests/Fixtures/ScratchPostgresDatabase.cs backend/MicroLIMS.Tests/UnitTests/Kpi/KpiSettingsModelTests.cs backend/MicroLIMS.Tests/IntegrationTests/KpiSettingsMigrationPostgresTests.cs
git add -p backend/MicroLIMS.Application/Abstractions/Persistence/IMicroLimsDbContext.cs backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs backend/MicroLIMS.Application/Services/UserReferenceRegistry.cs backend/MicroLIMS.Persistence/Migrations/MicroLimsDbContextModelSnapshot.cs
git commit -m "feat(kpi): KpiSettings and AnalystAvailabilities tables" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```
(If those four shared files were clean before your edit, replace the `git add -p` line with a plain `git add` of the four paths.)


---

### Task 3: Settings DTOs, shared test data, and `KpiSettingsService`

**Files:**
- Create: `backend/MicroLIMS.Application/Services/Kpi/WorkloadDtos.cs`
- Create: `backend/MicroLIMS.Application/Services/Kpi/KpiSettingsService.cs`
- Modify: `backend/MicroLIMS.Tests/TestServiceFactory.cs` (add `using MicroLIMS.Application.Services.Kpi;` and a factory method after `ReviewGate`, about line 78)
- Create: `backend/MicroLIMS.Tests/UnitTests/Kpi/WorkloadTestData.cs`
- Test: `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiSettingsServiceTests.cs`

**Interfaces:**
- Consumes: Task 2 entities and DbSets; `IUserSectionScopeService.GetAccessibleSectionIdsAsync(int, CancellationToken)` -> `IReadOnlyList<int>?`; `IAuditEventService.RecordUserEventAsync(string actionCode, AuditActionCategory, string recordType, int? documentMasterId, int? documentRevisionId, string? reason, Guid? correlationId, string? sourceContext, IEnumerable<AuditFieldChange>? changes, string? entityId, CancellationToken, int? sampleId)`; `RecordVersion.EnsureCurrent(IMicroLimsDbContext, IVersionedEntity)`; `NotFoundException(string)`; S1 `WorkflowProfileVersion`, `WorkflowProfileStatus`, `db.WorkflowProfileVersions`; S2 `KpiQuery`, `KpiGap`, `KpiRecordPageDto`; S1 `FixedTimeProvider(DateTimeOffset)` in `MicroLIMS.Tests.Fixtures` (confirm its constructor in `backend/MicroLIMS.Tests/Fixtures/FixedTimeProvider.cs`; adapt the one `new FixedTimeProvider(...)` call site in `WorkloadTestData` if it differs).
- Produces: all S3 DTO records (contract section 4.3); `KpiSettingsService(IMicroLimsDbContext, IUserSectionScopeService, IAuditEventService, TimeProvider? = null)` with `Task<KpiSettingsDto> GetAsync(int sectionId, int userId, CancellationToken ct = default)`, `Task<KpiSettingsDto> UpdateAsync(int sectionId, UpdateKpiSettingsRequest r, int userId, CancellationToken ct = default)`, `const string SettingsChangedActionCode = "KpiSettingsChanged"`, `const string RecordType = "KpiSetting"`; `TestServiceFactory.KpiSettings(MicroLimsDbContext db, TimeProvider? time = null)`; `WorkloadTestData` helpers (below) used by Tasks 4-9.

- [ ] **Step 1: Create the shared test data**

Create `backend/MicroLIMS.Tests/UnitTests/Kpi/WorkloadTestData.cs`. "Now" is Wednesday 14 Oct 2026 12:00Z, the reference week is Mon 5 - Sun 11 Oct 2026 (five working days), and the seeded profiles are: CountTest 2 units / 5 days, Measurement 4 / 10, Observation 1 / 3, Gravimetric none (so `NOPROF` is unweighted).

```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;

namespace MicroLIMS.Tests.UnitTests.Kpi;

// Shared seed for the S3 workload tests. Every DateTime is UTC so the same data also loads into Postgres.
internal static class WorkloadTestData
{
    public const int Alice = 1, Bob = 2, Sam = 3, Dana = 4, Carol = 5;

    public static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
    public static readonly DateOnly WeekFrom = new(2026, 10, 5);
    public static readonly DateOnly WeekTo = new(2026, 10, 11);

    public static DateTime At(int day, int hour = 9) => new(2026, 10, day, hour, 0, 0, DateTimeKind.Utc);
    public static FixedTimeProvider Clock() => new(Now);

    public static MicroLimsDbContext NewDb()
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.CurrentUserId = Sam;
        return db;
    }

    // NOTE: on a MIGRATED Postgres database QC/MICRO already exist (AddOrgSectionSegregation). Postgres callers must run
    // DELETE FROM "DocumentSections"; DELETE FROM "DocumentDepartments"; before this (Task 9 does).
    // Sections 1 (Micro) and 2 (FP). Alice, Bob and Carol work in section 1, Dana in section 2, Sam (Section Head) in section 1.
    public static async Task SeedBaselineAsync(MicroLimsDbContext db)
    {
        db.DocumentDepartments.Add(new DocumentDepartment { Id = 1, Code = "QC", Name = "Quality Control" });
        db.DocumentSections.AddRange(
            new DocumentSection { Id = 1, Code = "MICRO", Name = "Microbiology", DepartmentId = 1 },
            new DocumentSection { Id = 2, Code = "FP", Name = "Finished Product", DepartmentId = 1 });
        db.Roles.AddRange(
            new Role { Id = 1, Type = RoleType.Analyst, Name = "Analyst" },
            new Role { Id = 2, Type = RoleType.SectionHead, Name = "Section Head" });
        db.Users.AddRange(
            NewUser(Alice, "Alice Analyst", 1), NewUser(Bob, "Bob Analyst", 1), NewUser(Sam, "Sam Head", 2),
            NewUser(Dana, "Dana Analyst", 1), NewUser(Carol, "carol analyst", 1));
        db.UserOrgMemberships.AddRange(
            Member(Alice, 1), Member(Bob, 1), Member(Sam, 1), Member(Dana, 2), Member(Carol, 1));
        db.CausesOfTesting.Add(new CauseOfTesting { Id = 1, Name = "Routine" });
        db.TestDefinitions.AddRange(
            Definition("TAMC", WorkflowType.CountTest), Definition("PH", WorkflowType.Measurement),
            Definition("OBS", WorkflowType.Observation), Definition("NOPROF", WorkflowType.Gravimetric));
        db.WorkflowProfileVersions.AddRange(
            Profile(null, WorkflowType.CountTest, 2m, 5m),
            Profile(null, WorkflowType.Measurement, 4m, 10m),
            Profile(null, WorkflowType.Observation, 1m, 3m));
        await db.SaveChangesAsync();
    }

    public static WorkflowProfileVersion Profile(string? testCode, WorkflowType type, decimal? units, decimal? target,
        DateOnly? effectiveFrom = null, WorkflowProfileStatus status = WorkflowProfileStatus.Effective) => new()
    {
        WorkflowType = type, TestCode = testCode, WorkUnits = units, TargetAnalystDays = target,
        EffectiveFrom = effectiveFrom ?? new DateOnly(2026, 1, 1), Status = status,
        ReasonForChange = "Seed", CreatedByUserId = Sam
    };

    // A delivered test: Approved order, assigned, with its section signoff approved inside the reference week unless told otherwise.
    // Order id == sample id unless sampleId is given (one test per sample keeps the on-time maths per test).
    public static async Task DeliverAsync(MicroLimsDbContext db, int id, string code, int analystId, DateTime approvedAt,
        DateTime? preparedAt = null, DateTime? submittedAt = null, bool returned = false, int? sampleId = null,
        int sectionId = 1, bool superseded = false, SectionSignoffStatus signoff = SectionSignoffStatus.Approved,
        bool noPreparation = false)
    {
        var sid = sampleId ?? id;
        await EnsureSampleAsync(db, sid);
        db.TestOrders.Add(new TestOrder
        {
            Id = id, SampleId = sid, TestCode = code, SectionId = sectionId,
            Status = ApprovalStatus.Approved, CurrentStep = WorkflowStep.Approved,
            AssignedAnalystId = analystId, IsSuperseded = superseded
        });
        if (!await db.SampleSectionSignoffs.AnyAsync(s => s.SampleId == sid && s.SectionId == sectionId))
        {
            db.SampleSectionSignoffs.Add(new SampleSectionSignoff
            {
                SampleId = sid, SectionId = sectionId, Status = signoff,
                SubmittedForReviewAt = submittedAt ?? approvedAt.AddHours(-2), ApprovedAt = approvedAt,
                ApprovalDecision = signoff == SectionSignoffStatus.Approved ? ApprovalDecision.Approve : ApprovalDecision.Reject
            });
        }
        if (!noPreparation && !await db.SamplePreparations.AnyAsync(p => p.SampleId == sid))
            db.SamplePreparations.Add(new SamplePreparation { SampleId = sid, PreparedByUserId = analystId, PreparedAt = preparedAt ?? At(5) });
        if (returned)
            db.TestReturnEvents.Add(new TestReturnEvent { TestOrderId = id, ReviewerUserId = Sam, AssignedAnalystId = analystId, ReturnedAt = approvedAt.AddHours(-30) });
        await db.SaveChangesAsync();
    }

    // An open test (Pending/InProgress). Its sample defaults to 1000 + id so it never collides with a delivered test's sample.
    public static async Task OpenAsync(MicroLimsDbContext db, int id, string code, int? analystId,
        ApprovalStatus status = ApprovalStatus.InProgress, int? sampleId = null, int sectionId = 1)
    {
        var sid = sampleId ?? 1000 + id;
        await EnsureSampleAsync(db, sid);
        db.TestOrders.Add(new TestOrder
        {
            Id = id, SampleId = sid, TestCode = code, SectionId = sectionId, Status = status,
            CurrentStep = WorkflowStep.Waiting, AssignedAnalystId = analystId
        });
        await db.SaveChangesAsync();
    }

    public static KpiQuery Query(DateOnly from, DateOnly to, int viewer = Sam, bool team = true, int? analystId = null,
        IReadOnlyList<int>? sections = null) =>
        new(from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            sections, null, null, null, analystId, Now.UtcDateTime, viewer, team);

    public static KpiQuery Week(int viewer = Sam, bool team = true, int? analystId = null) =>
        Query(WeekFrom, WeekTo, viewer, team, analystId);

    private static async Task EnsureSampleAsync(MicroLimsDbContext db, int sid)
    {
        if (await db.Samples.AnyAsync(s => s.Id == sid)) return;
        db.Samples.Add(new Sample
        {
            Id = sid, ReferenceNumber = $"R{sid:D5}", ControlNumber = $"FP-{sid}", Category = SampleCategory.FinishedProduct,
            CauseOfTestingId = 1, ReceivedByUserId = Sam, ReceivedAt = At(5, 8)
        });
        await db.SaveChangesAsync();
    }

    private static User NewUser(int id, string name, int roleId) =>
        new() { Id = id, FullName = name, Username = name.Replace(' ', '.').ToLowerInvariant(), RoleId = roleId, PasswordHash = "x" };

    private static UserOrgMembership Member(int userId, int sectionId) =>
        new() { UserId = userId, DepartmentId = 1, SectionId = sectionId };

    private static TestDefinition Definition(string code, WorkflowType type) =>
        new() { Code = code, DisplayName = code, SectionId = 1, WorkflowType = type, IsActive = true };
}
```

- [ ] **Step 2: Write the failing settings tests**

Create `backend/MicroLIMS.Tests/UnitTests/Kpi/KpiSettingsServiceTests.cs`:

```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Shared.Exceptions;
using Xunit;
using static MicroLIMS.Tests.UnitTests.Kpi.WorkloadTestData;

namespace MicroLIMS.Tests.UnitTests.Kpi;

public class KpiSettingsServiceTests
{
    private static UpdateKpiSettingsRequest Req(decimal hours = 8m, int mask = 31, int horizon = 3, string reason = "Site works Mon-Fri, 8 h days") =>
        new(hours, mask, horizon, reason);

    [Fact]
    public async Task Get_WithNoRow_ReturnsDocumentedDefaultsFlaggedAsDefault()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);

        var dto = await TestServiceFactory.KpiSettings(db, Clock()).GetAsync(1, Sam);

        Assert.True(dto.IsDefault);
        Assert.Equal(6.0m, dto.HoursPerAnalystDay);
        Assert.Equal(31, dto.WorkingWeekdays);
        Assert.Equal(5, dto.LoadHorizonDays);
    }

    [Fact]
    public async Task Update_CreatesTheRow_AndRecordsAConfigurationAuditEventWithTheReason()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);

        var dto = await TestServiceFactory.KpiSettings(db, Clock()).UpdateAsync(1, Req(), Sam);

        Assert.False(dto.IsDefault);
        Assert.Equal(8m, dto.HoursPerAnalystDay);
        var row = await db.KpiSettings.SingleAsync();
        Assert.Equal(Sam, row.UpdatedByUserId);
        Assert.Equal(Now.UtcDateTime, row.UpdatedAt);
        var audit = await db.AuditLogs.Include(a => a.Changes)
            .SingleAsync(a => a.ActionCode == KpiSettingsService.SettingsChangedActionCode);
        Assert.Equal("Site works Mon-Fri, 8 h days", audit.Reason);
        Assert.Equal("1", audit.EntityId);
        Assert.Contains(audit.Changes, c => c.FieldName == "HoursPerAnalystDay" && c.PreviousValue == "6.00" && c.NewValue == "8.00");
    }

    [Fact]
    public async Task Update_OfAnExistingRow_EditsInPlace()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        var service = TestServiceFactory.KpiSettings(db, Clock());
        await service.UpdateAsync(1, Req(), Sam);

        await service.UpdateAsync(1, Req(hours: 7.5m, horizon: 10, reason: "Revised after time study"), Sam);

        var row = await db.KpiSettings.SingleAsync();
        Assert.Equal(7.5m, row.HoursPerAnalystDay);
        Assert.Equal(10, row.LoadHorizonDays);
        Assert.Equal(2, await db.AuditLogs.CountAsync(a => a.ActionCode == KpiSettingsService.SettingsChangedActionCode));
    }

    [Theory]
    [InlineData(0.0, 31, 3, "r")]     // hours must be > 0
    [InlineData(24.5, 31, 3, "r")]    // hours must be <= 24
    [InlineData(6.123, 31, 3, "r")]   // at most two decimals
    [InlineData(6.0, 0, 3, "r")]      // at least one working weekday
    [InlineData(6.0, 128, 3, "r")]    // only seven weekday bits
    [InlineData(6.0, 31, 0, "r")]     // horizon >= 1
    [InlineData(6.0, 31, 31, "r")]    // horizon <= 30
    [InlineData(6.0, 31, 3, "  ")]    // a reason is required
    public async Task Update_RejectsInvalidValues(double hours, int mask, int horizon, string reason)
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TestServiceFactory.KpiSettings(db, Clock()).UpdateAsync(1, new((decimal)hours, mask, horizon, reason), Sam));
        Assert.Empty(db.KpiSettings);
    }

    [Fact]
    public async Task Update_WithAStaleIfMatchVersion_IsRefused()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        var service = TestServiceFactory.KpiSettings(db, Clock());
        await service.UpdateAsync(1, Req(), Sam);

        db.ExpectedVersion = 7; // the form was loaded at a version that no longer exists

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.UpdateAsync(1, Req(hours: 5m), Sam));
    }

    [Fact]
    public async Task Update_OfASectionOutsideTheCallersScope_IsForbidden()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);

        // Dana only works in section 2.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            TestServiceFactory.KpiSettings(db, Clock()).UpdateAsync(1, Req(), Dana));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            TestServiceFactory.KpiSettings(db, Clock()).GetAsync(1, Dana));
    }

    [Fact]
    public async Task Update_OfAnUnknownSection_IsNotFound()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            TestServiceFactory.KpiSettings(db, Clock()).UpdateAsync(99, Req(), Sam));
    }

    [Fact]
    public void UpdateRequest_SerialisesExactlyTheFieldNamesTheFrontendSends()
    {
        // The legacy weight update bound "weight" to a differently named property; pin the wire names.
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize(Req(), options);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(new[] { "hoursPerAnalystDay", "loadHorizonDays", "reason", "workingWeekdays" },
            doc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        var back = JsonSerializer.Deserialize<UpdateKpiSettingsRequest>(json, options)!;
        Assert.Equal(8m, back.HoursPerAnalystDay);
        Assert.Equal(31, back.WorkingWeekdays);
        Assert.Equal(3, back.LoadHorizonDays);
    }
}
```

- [ ] **Step 3: Run and see it fail**

Run: `dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~KpiSettingsServiceTests"`
Expected: build FAILS (`KpiSettingsService`, `UpdateKpiSettingsRequest`, `TestServiceFactory.KpiSettings` do not exist).

- [ ] **Step 4: Implement the DTOs**

Create `backend/MicroLIMS.Application/Services/Kpi/WorkloadDtos.cs`:

```csharp
namespace MicroLIMS.Application.Services.Kpi;

// Ratios (Utilisation, ForwardLoad, WeightedOnTime, WeightedRightFirstTime) are fractions: 1.0 = 100%.
// A rate is null when fewer than KpiConstants.MinSampleForRate tests stand behind it.
public sealed record AnalystWorkloadRowDto(int UserId, string FullName, decimal CapacityUnits, decimal DeliveredUnits, decimal? Utilisation,
    bool UtilisationCheckProfile, decimal OpenUnits, decimal ForwardCapacityUnits, decimal? ForwardLoad, bool ForwardLoadOverCapacity,
    decimal? WeightedOnTime, decimal? WeightedRightFirstTime, int DeliveredTests, int UnweightedTests, int UnweightedOpenTests, int TestsOnDefaultTarget);

public sealed record AnalystWorkloadDto(DateTime From, DateTime To, bool CapacityUsesDefaults, bool OnlyOwnRow,
    int UnassignedOpenTests, IReadOnlyList<AnalystWorkloadRowDto> Rows, IReadOnlyList<KpiGap> Gaps);

public sealed record KpiSettingsDto(int SectionId, decimal HoursPerAnalystDay, int WorkingWeekdays, int LoadHorizonDays, bool IsDefault, uint Version);

public sealed record UpdateKpiSettingsRequest(decimal HoursPerAnalystDay, int WorkingWeekdays, int LoadHorizonDays, string Reason);

public sealed record AnalystAvailabilityDto(int Id, int UserId, string UserName, DateOnly FromDate, DateOnly ToDate,
    decimal FractionAvailable, string Reason, string EnteredByName, DateTime EnteredAt);

public sealed record SaveAvailabilityRequest(int UserId, DateOnly FromDate, DateOnly ToDate, decimal FractionAvailable, string Reason);
```

- [ ] **Step 5: Implement the service and the factory method**

Create `backend/MicroLIMS.Application/Services/Kpi/KpiSettingsService.cs`:

```csharp
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.Kpi;

// Per-section capacity assumptions (spec 4.5). Edited in place (the automatic audit trail keeps the previous values);
// a reason is required and a semantic Configuration event is recorded. Not a weight, so no e-signature.
public class KpiSettingsService
{
    public const string SettingsChangedActionCode = "KpiSettingsChanged";
    public const string RecordType = "KpiSetting";

    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly IAuditEventService _audit;
    private readonly TimeProvider _time;

    public KpiSettingsService(IMicroLimsDbContext db, IUserSectionScopeService scope, IAuditEventService audit, TimeProvider? timeProvider = null)
    {
        _db = db;
        _scope = scope;
        _audit = audit;
        _time = timeProvider ?? TimeProvider.System;
    }

    public async Task<KpiSettingsDto> GetAsync(int sectionId, int userId, CancellationToken ct = default)
    {
        await EnsureSectionAccessAsync(sectionId, userId, ct);
        var row = await _db.KpiSettings.AsNoTracking().FirstOrDefaultAsync(s => s.SectionId == sectionId, ct);
        return row is null
            ? new KpiSettingsDto(sectionId, KpiSettingDefaults.HoursPerAnalystDay, KpiSettingDefaults.WorkingWeekdays,
                KpiSettingDefaults.LoadHorizonDays, true, 0)
            : ToDto(row);
    }

    public async Task<KpiSettingsDto> UpdateAsync(int sectionId, UpdateKpiSettingsRequest r, int userId, CancellationToken ct = default)
    {
        var reason = (r.Reason ?? string.Empty).Trim();
        if (reason.Length == 0) throw new InvalidOperationException("A reason for the change is required.");
        if (reason.Length > 500) throw new InvalidOperationException("The reason must be 500 characters or fewer.");
        if (r.HoursPerAnalystDay <= 0 || r.HoursPerAnalystDay > 24 || decimal.Round(r.HoursPerAnalystDay, 2) != r.HoursPerAnalystDay)
            throw new InvalidOperationException("Hours per analyst day must be greater than 0 and at most 24, with at most two decimals.");
        if (r.WorkingWeekdays is < 1 or > 127)
            throw new InvalidOperationException("Select at least one working weekday.");
        if (r.LoadHorizonDays is < 1 or > 30)
            throw new InvalidOperationException("The load horizon must be between 1 and 30 working days.");

        await EnsureSectionAccessAsync(sectionId, userId, ct);
        _db.CurrentUserId = userId;

        var row = await _db.KpiSettings.FirstOrDefaultAsync(s => s.SectionId == sectionId, ct);
        if (row is null)
        {
            row = new KpiSetting { SectionId = sectionId }; // entity defaults equal KpiSettingDefaults, so "previous" below is the default
            _db.KpiSettings.Add(row);
        }
        else
        {
            RecordVersion.EnsureCurrent(_db, row);
        }

        var changes = new[]
        {
            new AuditFieldChange("HoursPerAnalystDay", Fmt(row.HoursPerAnalystDay), Fmt(r.HoursPerAnalystDay)),
            new AuditFieldChange("WorkingWeekdays", Fmt(row.WorkingWeekdays), Fmt(r.WorkingWeekdays)),
            new AuditFieldChange("LoadHorizonDays", Fmt(row.LoadHorizonDays), Fmt(r.LoadHorizonDays))
        };

        row.HoursPerAnalystDay = r.HoursPerAnalystDay;
        row.WorkingWeekdays = r.WorkingWeekdays;
        row.LoadHorizonDays = r.LoadHorizonDays;
        row.UpdatedByUserId = userId;
        row.UpdatedAt = _time.GetUtcNow().UtcDateTime;

        // Saves the row and the event together.
        await _audit.RecordUserEventAsync(SettingsChangedActionCode, AuditActionCategory.Configuration, RecordType,
            reason: reason, changes: changes, entityId: sectionId.ToString(CultureInfo.InvariantCulture), cancellationToken: ct);
        return ToDto(row);
    }

    private async Task EnsureSectionAccessAsync(int sectionId, int userId, CancellationToken ct)
    {
        if (!await _db.DocumentSections.AnyAsync(s => s.Id == sectionId && s.IsActive, ct))
            throw new NotFoundException("Laboratory section not found.");
        var ids = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        if (ids is not null && !ids.Contains(sectionId))
            throw new UnauthorizedAccessException("You do not have access to this laboratory section.");
    }

    private static KpiSettingsDto ToDto(KpiSetting s) =>
        new(s.SectionId, s.HoursPerAnalystDay, s.WorkingWeekdays, s.LoadHorizonDays, false, s.Version);

    private static string Fmt(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    private static string Fmt(int v) => v.ToString(CultureInfo.InvariantCulture);
}
```

In `backend/MicroLIMS.Tests/TestServiceFactory.cs` add `using MicroLIMS.Application.Services.Kpi;` with the other usings, and insert after the `ReviewGate` method:

```csharp
    public static KpiSettingsService KpiSettings(MicroLimsDbContext db, TimeProvider? time = null) =>
        new(db, new UserSectionScopeService(db),
            new AuditEventService(db, new MicroLIMS.Persistence.Helpers.DatabaseSequenceHelper(db)), time);
```

- [ ] **Step 6: Run and see it pass**

Run the Step 3 command. Expected: 15 passed (8-row theory counts as 8), 0 failed.

- [ ] **Step 7: Commit**

Shared-file protocol for `TestServiceFactory.cs`.

```
git add backend/MicroLIMS.Application/Services/Kpi/WorkloadDtos.cs backend/MicroLIMS.Application/Services/Kpi/KpiSettingsService.cs backend/MicroLIMS.Tests/UnitTests/Kpi/WorkloadTestData.cs backend/MicroLIMS.Tests/UnitTests/Kpi/KpiSettingsServiceTests.cs
git add -p backend/MicroLIMS.Tests/TestServiceFactory.cs
git commit -m "feat(kpi): per-section capacity settings service" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 4: `AnalystAvailabilityService`

**Files:**
- Create: `backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs`
- Modify: `backend/MicroLIMS.Tests/TestServiceFactory.cs` (after the `KpiSettings` method from Task 3)
- Test: `backend/MicroLIMS.Tests/UnitTests/Kpi/AnalystAvailabilityServiceTests.cs`

**Interfaces:**
- Consumes: Task 1 `KpiUserScope.InScopeAsync`; Task 2 `AnalystAvailability`; Task 3 DTOs and `WorkloadTestData`; `UnitOfWork.RunAsync<T>(IMicroLimsDbContext, Func<Task<T>>)`.
- Produces: `AnalystAvailabilityService(IMicroLimsDbContext, IUserSectionScopeService, IAuditEventService, TimeProvider? = null)` with `Task<List<AnalystAvailabilityDto>> ListAsync(int viewerUserId, bool canViewTeam, int? userId, DateOnly? from, DateOnly? to, CancellationToken ct = default)`, `Task<AnalystAvailabilityDto> CreateAsync(SaveAvailabilityRequest r, int enteredByUserId, CancellationToken ct = default)`, `Task<AnalystAvailabilityDto> UpdateAsync(int id, SaveAvailabilityRequest r, int enteredByUserId, CancellationToken ct = default)`, `const string ChangedActionCode = "AnalystAvailabilityChanged"`; `TestServiceFactory.AnalystAvailability(MicroLimsDbContext db, TimeProvider? time = null)`.

- [ ] **Step 1: Write the failing tests**

Create `backend/MicroLIMS.Tests/UnitTests/Kpi/AnalystAvailabilityServiceTests.cs`:

```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Shared.Exceptions;
using Xunit;
using static MicroLIMS.Tests.UnitTests.Kpi.WorkloadTestData;

namespace MicroLIMS.Tests.UnitTests.Kpi;

public class AnalystAvailabilityServiceTests
{
    private static SaveAvailabilityRequest Req(int user = Alice, int fromDay = 14, int toDay = 16, decimal fraction = 0.5m, string reason = "Training course") =>
        new(user, new DateOnly(2026, 10, fromDay), new DateOnly(2026, 10, toDay), fraction, reason);

    [Fact]
    public async Task Create_SavesTheRow_NamesBothPeople_AndRecordsAConfigurationEvent()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);

        var dto = await TestServiceFactory.AnalystAvailability(db, Clock()).CreateAsync(Req(), Sam);

        Assert.Equal("Alice Analyst", dto.UserName);
        Assert.Equal("Sam Head", dto.EnteredByName);
        Assert.Equal(Now.UtcDateTime, dto.EnteredAt);
        var audit = await db.AuditLogs.SingleAsync(a => a.ActionCode == AnalystAvailabilityService.ChangedActionCode);
        Assert.Equal("Training course", audit.Reason);
        Assert.Equal(dto.Id.ToString(), audit.EntityId);
    }

    [Theory]
    [InlineData(14, 16, 1.5, "Training course")]  // fraction above 1
    [InlineData(14, 16, -0.1, "Training course")] // fraction below 0
    [InlineData(14, 16, 0.555, "Training course")] // more than two decimals
    [InlineData(16, 14, 0.5, "Training course")]  // ends before it starts
    [InlineData(14, 16, 0.5, "   ")]              // reason required
    public async Task Create_RejectsInvalidInput(int fromDay, int toDay, double fraction, string reason)
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TestServiceFactory.AnalystAvailability(db, Clock()).CreateAsync(Req(fromDay: fromDay, toDay: toDay, fraction: (decimal)fraction, reason: reason), Sam));
        Assert.Empty(db.AnalystAvailabilities);
    }

    [Fact]
    public async Task Create_ForAnUnknownUser_IsRejected()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TestServiceFactory.AnalystAvailability(db, Clock()).CreateAsync(Req(user: 999), Sam));
    }

    [Fact]
    public async Task Create_OverlappingAnExistingWindow_IsRejected_ButAdjacentIsAllowed()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        var service = TestServiceFactory.AnalystAvailability(db, Clock());
        await service.CreateAsync(Req(fromDay: 14, toDay: 16), Sam);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Req(fromDay: 16, toDay: 17), Sam));
        await service.CreateAsync(Req(fromDay: 17, toDay: 18), Sam); // starts the day after the first ends

        Assert.Equal(2, await db.AnalystAvailabilities.CountAsync());
    }

    [Fact]
    public async Task Create_ForAnAnalystOutsideTheEditorsSections_IsForbidden()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);

        // Sam works in section 1; Dana only in section 2.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            TestServiceFactory.AnalystAvailability(db, Clock()).CreateAsync(Req(user: Dana), Sam));
    }

    [Fact]
    public async Task Update_CorrectsTheRowInPlace_AndAuditsEachChange()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        var service = TestServiceFactory.AnalystAvailability(db, Clock());
        var created = await service.CreateAsync(Req(), Sam);

        var updated = await service.UpdateAsync(created.Id, Req(fraction: 0.25m, reason: "Correction: half day booked in error"), Sam);

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(0.25m, updated.FractionAvailable);
        Assert.Equal("Correction: half day booked in error", updated.Reason);
        Assert.Equal(1, await db.AnalystAvailabilities.CountAsync()); // never silently removed or duplicated
        Assert.Equal(2, await db.AuditLogs.CountAsync(a => a.ActionCode == AnalystAvailabilityService.ChangedActionCode));
    }

    [Fact]
    public async Task Update_CannotMoveAnEntryToAnotherAnalyst()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        var service = TestServiceFactory.AnalystAvailability(db, Clock());
        var created = await service.CreateAsync(Req(), Sam);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(created.Id, Req(user: Bob), Sam));
    }

    [Fact]
    public async Task Update_OfAnUnknownEntry_IsNotFound()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            TestServiceFactory.AnalystAvailability(db, Clock()).UpdateAsync(999, Req(), Sam));
    }

    [Fact]
    public async Task List_WithoutViewTeam_ReturnsOnlyTheCallersRows_AndRefusesAnotherUser()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        var service = TestServiceFactory.AnalystAvailability(db, Clock());
        await service.CreateAsync(Req(user: Alice), Sam);
        await service.CreateAsync(Req(user: Bob), Sam);

        var own = await service.ListAsync(Alice, canViewTeam: false, userId: null, from: null, to: null);

        Assert.Single(own);
        Assert.Equal(Alice, own[0].UserId);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ListAsync(Alice, canViewTeam: false, userId: Bob, from: null, to: null));
    }

    [Fact]
    public async Task List_WithViewTeam_IsLimitedToTheViewersSections()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        db.AnalystAvailabilities.AddRange(
            new AnalystAvailability { UserId = Alice, FromDate = new(2026, 10, 14), ToDate = new(2026, 10, 14), FractionAvailable = 0, Reason = "Leave", EnteredByUserId = Sam },
            new AnalystAvailability { UserId = Dana, FromDate = new(2026, 10, 14), ToDate = new(2026, 10, 14), FractionAvailable = 0, Reason = "Leave", EnteredByUserId = Sam });
        await db.SaveChangesAsync();
        var service = TestServiceFactory.AnalystAvailability(db, Clock());

        var rows = await service.ListAsync(Sam, canViewTeam: true, userId: null, from: null, to: null);

        Assert.Equal(new[] { Alice }, rows.Select(r => r.UserId)); // Dana works only in section 2
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ListAsync(Sam, canViewTeam: true, userId: Dana, from: null, to: null));
    }

    [Fact]
    public async Task List_FiltersByDateWindow()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        var service = TestServiceFactory.AnalystAvailability(db, Clock());
        await service.CreateAsync(Req(fromDay: 14, toDay: 16), Sam);
        await service.CreateAsync(Req(fromDay: 26, toDay: 27), Sam);

        var rows = await service.ListAsync(Sam, true, Alice, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 31));

        Assert.Single(rows);
        Assert.Equal(new DateOnly(2026, 10, 26), rows[0].FromDate);
    }

    [Fact]
    public void SaveRequest_SerialisesExactlyTheFieldNamesTheFrontendSends_AndDatesAsIsoStrings()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize(Req(), options);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(new[] { "fractionAvailable", "fromDate", "reason", "toDate", "userId" },
            doc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal("2026-10-14", doc.RootElement.GetProperty("fromDate").GetString());
        Assert.Equal(new DateOnly(2026, 10, 16), JsonSerializer.Deserialize<SaveAvailabilityRequest>(json, options)!.ToDate);
    }
}
```

- [ ] **Step 2: Run and see it fail**

Run: `dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~AnalystAvailabilityServiceTests"`
Expected: build FAILS (`AnalystAvailabilityService`, `TestServiceFactory.AnalystAvailability` missing).

- [ ] **Step 3: Implement**

Create `backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs`:

```csharp
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.Kpi;

// Leave / training / secondment windows that reduce an analyst's capacity (spec 4.5). Entered by the Section Head
// for analysts in their own sections; never deleted (a correction is an edit with a reason, audited); one
// analyst's windows never overlap, so a day has at most one fraction.
public class AnalystAvailabilityService
{
    public const string ChangedActionCode = "AnalystAvailabilityChanged";
    public const string RecordType = "AnalystAvailability";

    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly IAuditEventService _audit;
    private readonly TimeProvider _time;

    public AnalystAvailabilityService(IMicroLimsDbContext db, IUserSectionScopeService scope, IAuditEventService audit, TimeProvider? timeProvider = null)
    {
        _db = db;
        _scope = scope;
        _audit = audit;
        _time = timeProvider ?? TimeProvider.System;
    }

    // D8: without Kpi.ViewTeam only the caller's own rows are ever returned, enforced here.
    public async Task<List<AnalystAvailabilityDto>> ListAsync(int viewerUserId, bool canViewTeam, int? userId,
        DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        if (!canViewTeam && userId is int other && other != viewerUserId)
            throw new UnauthorizedAccessException("You may only view your own availability.");

        var viewerScope = canViewTeam ? await _scope.GetAccessibleSectionIdsAsync(viewerUserId, ct) : null;
        if (canViewTeam && userId is int asked && !await KpiUserScope.InScopeAsync(_scope, asked, viewerScope, ct))
            throw new UnauthorizedAccessException("That analyst is outside your laboratory sections.");

        var target = canViewTeam ? userId : viewerUserId;
        var query = _db.AnalystAvailabilities.AsNoTracking();
        if (target is int t) query = query.Where(a => a.UserId == t);
        if (from is DateOnly f) query = query.Where(a => a.ToDate >= f);
        if (to is DateOnly e) query = query.Where(a => a.FromDate <= e);
        var rows = await query.OrderBy(a => a.UserId).ThenBy(a => a.FromDate).ToListAsync(ct);

        if (canViewTeam && target is null)
        {
            var kept = new List<AnalystAvailability>();
            foreach (var g in rows.GroupBy(a => a.UserId))
                if (await KpiUserScope.InScopeAsync(_scope, g.Key, viewerScope, ct)) kept.AddRange(g);
            rows = kept;
        }
        return await ToDtosAsync(rows, ct);
    }

    public Task<AnalystAvailabilityDto> CreateAsync(SaveAvailabilityRequest r, int enteredByUserId, CancellationToken ct = default) =>
        UnitOfWork.RunAsync(_db, async () =>
        {
            var reason = Validate(r);
            await EnsureEditableAsync(r.UserId, enteredByUserId, ct);
            await EnsureNoOverlapAsync(r.UserId, r.FromDate, r.ToDate, exceptId: 0, ct);
            _db.CurrentUserId = enteredByUserId;

            var row = new AnalystAvailability
            {
                UserId = r.UserId, FromDate = r.FromDate, ToDate = r.ToDate, FractionAvailable = r.FractionAvailable,
                Reason = reason, EnteredByUserId = enteredByUserId, EnteredAt = _time.GetUtcNow().UtcDateTime
            };
            _db.AnalystAvailabilities.Add(row);
            await _db.SaveChangesAsync(ct); // the id is needed for the event

            await _audit.RecordUserEventAsync(ChangedActionCode, AuditActionCategory.Configuration, RecordType,
                reason: reason, entityId: row.Id.ToString(CultureInfo.InvariantCulture), cancellationToken: ct,
                changes: new[]
                {
                    new AuditFieldChange("UserId", null, Fmt(row.UserId)),
                    new AuditFieldChange("FromDate", null, Fmt(row.FromDate)),
                    new AuditFieldChange("ToDate", null, Fmt(row.ToDate)),
                    new AuditFieldChange("FractionAvailable", null, Fmt(row.FractionAvailable))
                });
            return (await ToDtosAsync(new List<AnalystAvailability> { row }, ct))[0];
        });

    public async Task<AnalystAvailabilityDto> UpdateAsync(int id, SaveAvailabilityRequest r, int enteredByUserId, CancellationToken ct = default)
    {
        var reason = Validate(r);
        var row = await _db.AnalystAvailabilities.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException("Availability entry not found.");
        if (row.UserId != r.UserId)
            throw new InvalidOperationException("An entry cannot be moved to another analyst; correct the dates or enter a new entry.");
        await EnsureEditableAsync(row.UserId, enteredByUserId, ct);
        await EnsureNoOverlapAsync(row.UserId, r.FromDate, r.ToDate, exceptId: id, ct);
        _db.CurrentUserId = enteredByUserId;

        var changes = new[]
        {
            new AuditFieldChange("FromDate", Fmt(row.FromDate), Fmt(r.FromDate)),
            new AuditFieldChange("ToDate", Fmt(row.ToDate), Fmt(r.ToDate)),
            new AuditFieldChange("FractionAvailable", Fmt(row.FractionAvailable), Fmt(r.FractionAvailable))
        };
        row.FromDate = r.FromDate;
        row.ToDate = r.ToDate;
        row.FractionAvailable = r.FractionAvailable;
        row.Reason = reason;
        row.EnteredByUserId = enteredByUserId;
        row.EnteredAt = _time.GetUtcNow().UtcDateTime;

        // Saves the edit and the event together.
        await _audit.RecordUserEventAsync(ChangedActionCode, AuditActionCategory.Configuration, RecordType,
            reason: reason, changes: changes, entityId: id.ToString(CultureInfo.InvariantCulture), cancellationToken: ct);
        return (await ToDtosAsync(new List<AnalystAvailability> { row }, ct))[0];
    }

    private static string Validate(SaveAvailabilityRequest r)
    {
        var reason = (r.Reason ?? string.Empty).Trim();
        if (reason.Length == 0) throw new InvalidOperationException("A reason is required.");
        if (reason.Length > 500) throw new InvalidOperationException("The reason must be 500 characters or fewer.");
        if (r.ToDate < r.FromDate) throw new InvalidOperationException("The end date must not be before the start date.");
        if (r.FractionAvailable < 0 || r.FractionAvailable > 1 || decimal.Round(r.FractionAvailable, 2) != r.FractionAvailable)
            throw new InvalidOperationException("Fraction available must be between 0 and 1 with at most two decimals.");
        return reason;
    }

    private async Task EnsureEditableAsync(int analystId, int editorId, CancellationToken ct)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == analystId, ct))
            throw new InvalidOperationException("The analyst does not exist.");
        var editorScope = await _scope.GetAccessibleSectionIdsAsync(editorId, ct);
        if (!await KpiUserScope.InScopeAsync(_scope, analystId, editorScope, ct))
            throw new UnauthorizedAccessException("You may only record availability for analysts in your laboratory sections.");
    }

    private async Task EnsureNoOverlapAsync(int userId, DateOnly from, DateOnly to, int exceptId, CancellationToken ct)
    {
        if (await _db.AnalystAvailabilities.AnyAsync(a => a.UserId == userId && a.Id != exceptId && a.FromDate <= to && a.ToDate >= from, ct))
            throw new InvalidOperationException("This period overlaps an existing availability entry for the analyst. Correct that entry instead.");
    }

    private async Task<List<AnalystAvailabilityDto>> ToDtosAsync(List<AnalystAvailability> rows, CancellationToken ct)
    {
        var ids = rows.Select(a => a.UserId).Concat(rows.Select(a => a.EnteredByUserId)).Distinct().ToList();
        var names = await _db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        string Name(int id) => names.TryGetValue(id, out var n) ? n : "(unknown user)";
        return rows.Select(a => new AnalystAvailabilityDto(a.Id, a.UserId, Name(a.UserId), a.FromDate, a.ToDate,
            a.FractionAvailable, a.Reason, Name(a.EnteredByUserId), a.EnteredAt)).ToList();
    }

    private static string Fmt(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string Fmt(decimal d) => d.ToString("0.00", CultureInfo.InvariantCulture);
    private static string Fmt(int i) => i.ToString(CultureInfo.InvariantCulture);
}
```

In `TestServiceFactory.cs` (after `KpiSettings`):

```csharp
    public static AnalystAvailabilityService AnalystAvailability(MicroLimsDbContext db, TimeProvider? time = null) =>
        new(db, new UserSectionScopeService(db),
            new AuditEventService(db, new MicroLIMS.Persistence.Helpers.DatabaseSequenceHelper(db)), time);
```

- [ ] **Step 4: Run and see it pass**

Run the Step 2 command. Expected: 16 passed, 0 failed.

- [ ] **Step 5: Commit**

```
git add backend/MicroLIMS.Application/Services/Kpi/AnalystAvailabilityService.cs backend/MicroLIMS.Tests/UnitTests/Kpi/AnalystAvailabilityServiceTests.cs
git add -p backend/MicroLIMS.Tests/TestServiceFactory.cs
git commit -m "feat(kpi): analyst availability service" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 5: `AnalystWorkloadService` - units, capacity, forward load

**Files:**
- Create: `backend/MicroLIMS.Application/Services/Kpi/AnalystWorkloadService.cs`
- Modify: `backend/MicroLIMS.Tests/TestServiceFactory.cs` (after `AnalystAvailability`)
- Test: `backend/MicroLIMS.Tests/UnitTests/Kpi/AnalystWorkloadServiceTests.cs`

**Interfaces:**
- Consumes: Tasks 1-4 (`CapacityCalculator`, `KpiSettingDefaults`, `KpiUserScope`, entities, DTOs, `WorkloadTestData`); S1 `WorkflowProfileService.LoadResolverAsync(CancellationToken)` -> `WorkflowProfileResolver`, `WorkflowProfileResolver.Resolve(string testCode, DateTime at)` -> `ResolvedProfile(decimal? WorkUnits, decimal? TargetAnalystDays, ...)` with `IsWeighted`/`IsTargetAware`; S1 `TestServiceFactory.WorkflowProfile(MicroLimsDbContext, TimeProvider?)`; S2 `KpiQuery(DateTime From, DateTime To, IReadOnlyList<int>? SectionIds, string? Category, string? Location, string? TestCode, int? AnalystId, DateTime Now, int ViewerUserId, bool CanViewTeam)`, `KpiGap(string Code, string Message, int Count)`, `KpiConstants`; existing `SectionReviewQueues.OrderIn(IReadOnlyCollection<int>?)` -> `Expression<Func<TestOrder, bool>>` (null = unrestricted).
- Produces: `AnalystWorkloadService(IMicroLimsDbContext, WorkflowProfileService, IUserSectionScopeService, TimeProvider? = null)` with `Task<KpiQuery> BuildQueryAsync(int userId, bool canViewTeam, DateOnly from, DateOnly to, string? category, string? location, string? testCode, int? analystId, CancellationToken ct = default)` (an addition to the contract: the controller needs it to build a `KpiQuery`), `Task<AnalystWorkloadDto> GetAsync(KpiQuery q, CancellationToken ct)`; `TestServiceFactory.AnalystWorkload(MicroLimsDbContext db, TimeProvider? time = null)`. After this task rates are `null` and `TestsOnDefaultTarget` is 0; Task 6 fills them.

- [ ] **Step 1: Write the failing tests**

Create `backend/MicroLIMS.Tests/UnitTests/Kpi/AnalystWorkloadServiceTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;
using static MicroLIMS.Tests.UnitTests.Kpi.WorkloadTestData;

namespace MicroLIMS.Tests.UnitTests.Kpi;

// Reference data: see WorkloadTestData. Now = Wed 14 Oct 2026 12:00Z; week under test = Mon 5 - Sun 11 Oct (5 working days).
// Profiles: CountTest (TAMC) 2 units / 5 days, Measurement (PH) 4 / 10, Observation (OBS) 1 / 3, Gravimetric (NOPROF) none.
public class AnalystWorkloadServiceTests
{
    private static Task<AnalystWorkloadDto> GetAsync(MicroLimsDbContext db, KpiQuery q) =>
        TestServiceFactory.AnalystWorkload(db, Clock()).GetAsync(q, default);

    private static AnalystWorkloadRowDto Row(AnalystWorkloadDto dto, int userId) => dto.Rows.Single(r => r.UserId == userId);

    private static KpiSetting Setting(int sectionId, decimal hours, int mask = 31, int horizon = 5) => new()
    {
        SectionId = sectionId, HoursPerAnalystDay = hours, WorkingWeekdays = mask, LoadHorizonDays = horizon,
        UpdatedByUserId = Sam, UpdatedAt = Now.UtcDateTime
    };

    private static AnalystAvailability Leave(int userId, DateOnly from, DateOnly to, decimal fraction) => new()
    {
        UserId = userId, FromDate = from, ToDate = to, FractionAvailable = fraction, Reason = "Leave", EnteredByUserId = Sam,
        EnteredAt = Now.UtcDateTime
    };

    [Fact]
    public async Task BuildQuery_ConvertsInclusiveDatesToAHalfOpenUtcWindow_AndTakesTheCallersSections()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);

        var q = await TestServiceFactory.AnalystWorkload(db, Clock())
            .BuildQueryAsync(Sam, true, WeekFrom, WeekTo, "FinishedProduct", null, null, null);

        Assert.Equal(new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc), q.From);
        Assert.Equal(new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc), q.To);
        Assert.Equal(new[] { 1 }, q.SectionIds);
        Assert.Equal(Now.UtcDateTime, q.Now);
        Assert.Equal("FinishedProduct", q.Category);
    }

    [Fact]
    public async Task BuildQuery_RejectsReversedDatesAndUnknownCategories()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        var service = TestServiceFactory.AnalystWorkload(db, Clock());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.BuildQueryAsync(Sam, true, WeekTo, WeekFrom, null, null, null, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.BuildQueryAsync(Sam, true, WeekFrom, WeekTo, "Nonsense", null, null, null));
    }

    [Fact]
    public async Task DeliveredUnits_SumResolvedUnits_AndUtilisationUsesTheDefaultCapacity()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        for (var i = 1; i <= 3; i++) await DeliverAsync(db, i, "TAMC", Alice, At(8, 12));
        await DeliverAsync(db, 4, "PH", Alice, At(8, 12));

        var dto = await GetAsync(db, Week(analystId: Alice));

        var row = Row(dto, Alice);
        Assert.Equal(10m, row.DeliveredUnits);           // 3 x 2 + 4
        Assert.Equal(4, row.DeliveredTests);
        Assert.Equal(30m, row.CapacityUnits);             // 5 working days x 6 h
        Assert.Equal(Math.Round(10m / 30m, 6), Math.Round(row.Utilisation!.Value, 6));
        Assert.False(row.UtilisationCheckProfile);
        Assert.True(dto.CapacityUsesDefaults);
        Assert.False(dto.OnlyOwnRow);
    }

    [Fact]
    public async Task Utilisation_Above100Percent_FlagsCheckProfile()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        for (var i = 1; i <= 16; i++) await DeliverAsync(db, i, "TAMC", Bob, At(8, 12)); // 32 units against 30

        var row = Row(await GetAsync(db, Week(analystId: Bob)), Bob);

        Assert.True(row.Utilisation > 1m);
        Assert.True(row.UtilisationCheckProfile);
    }

    [Fact]
    public async Task Unweighted_TestsAreExcludedFromUnits_AndReportedAsAGap()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        await DeliverAsync(db, 1, "NOPROF", Alice, At(8, 12));
        await DeliverAsync(db, 2, "TAMC", Alice, At(8, 12));
        await OpenAsync(db, 500, "NOPROF", Alice);

        var dto = await GetAsync(db, Week(analystId: Alice));

        var row = Row(dto, Alice);
        Assert.Equal(2m, row.DeliveredUnits);
        Assert.Equal(2, row.DeliveredTests);
        Assert.Equal(1, row.UnweightedTests);
        Assert.Equal(1, row.UnweightedOpenTests);
        Assert.Equal(0m, row.OpenUnits);
        Assert.Equal(2, dto.Gaps.Single(g => g.Code == "UNWEIGHTED_TESTS").Count);
    }

    [Fact] // Review Focus 6
    public async Task Utilisation_WhenEveryDeliveredTestIsUnweighted_IsNullNotZero()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        await DeliverAsync(db, 1, "NOPROF", Alice, At(8, 12));
        await DeliverAsync(db, 2, "NOPROF", Alice, At(8, 12));

        var row = Row(await GetAsync(db, Week(analystId: Alice)), Alice);

        Assert.Equal(2, row.DeliveredTests);
        Assert.Equal(0m, row.DeliveredUnits);
        Assert.Null(row.Utilisation);
        Assert.False(row.UtilisationCheckProfile);
    }

    [Fact] // Review Focus 6
    public async Task ForwardLoad_WhenEveryOpenTestIsUnweighted_IsNullAndNotOverCapacity()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        await OpenAsync(db, 500, "NOPROF", Alice);

        var row = Row(await GetAsync(db, Week(analystId: Alice)), Alice);

        Assert.Equal(1, row.UnweightedOpenTests);
        Assert.Null(row.ForwardLoad);
        Assert.False(row.ForwardLoadOverCapacity);
    }

    [Fact] // Review Focus 7
    public async Task Filtered_UtilisationAndForwardLoadAreNull_WithCapacityNotFilteredGap()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        await DeliverAsync(db, 1, "TAMC", Alice, At(8, 12));
        await OpenAsync(db, 500, "TAMC", Alice);

        var q = Week(analystId: Alice) with { TestCode = "TAMC" };
        var dto = await GetAsync(db, q);

        var row = Row(dto, Alice);
        Assert.Equal(2m, row.DeliveredUnits); // the filtered work is still shown as units
        Assert.Null(row.Utilisation);
        Assert.Null(row.ForwardLoad);
        Assert.False(row.ForwardLoadOverCapacity);
        Assert.Contains(dto.Gaps, g => g.Code == "CAPACITY_NOT_FILTERED");

        var unfiltered = await GetAsync(db, Week(analystId: Alice));
        Assert.DoesNotContain(unfiltered.Gaps, g => g.Code == "CAPACITY_NOT_FILTERED");
        Assert.NotNull(Row(unfiltered, Alice).Utilisation);
    }

    [Fact]
    public async Task Delivered_ExcludesSupersededRejectedAndOutOfWindowTests()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        await DeliverAsync(db, 1, "TAMC", Alice, At(8, 12));                                              // counted
        await DeliverAsync(db, 2, "TAMC", Alice, At(8, 12), superseded: true);                             // replaced by a retest
        await DeliverAsync(db, 3, "TAMC", Alice, At(8, 12), signoff: SectionSignoffStatus.Rejected);       // section rejected
        await DeliverAsync(db, 4, "TAMC", Alice, At(12, 9));                                               // after the period
        await DeliverAsync(db, 5, "TAMC", Alice, new DateTime(2026, 10, 4, 23, 59, 0, DateTimeKind.Utc));   // before the period

        var row = Row(await GetAsync(db, Week(analystId: Alice)), Alice);

        Assert.Equal(1, row.DeliveredTests);
        Assert.Equal(2m, row.DeliveredUnits);
    }

    [Fact]
    public async Task Profile_IsResolvedAtApprovalTime_NotToday_AndDraftsAreIgnored()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        db.WorkflowProfileVersions.Add(Profile("TAMC", WorkflowType.CountTest, 10m, null, new DateOnly(2026, 10, 10)));
        db.WorkflowProfileVersions.Add(Profile("TAMC", WorkflowType.CountTest, 99m, null, new DateOnly(2026, 10, 1), WorkflowProfileStatus.Draft));
        await db.SaveChangesAsync();
        await DeliverAsync(db, 1, "TAMC", Alice, At(8, 12));   // approved before the override: type default (2)
        await DeliverAsync(db, 2, "TAMC", Alice, At(11, 8));   // approved after the override: 10
        await OpenAsync(db, 500, "TAMC", Alice);               // open work is valued as of now: 10

        var row = Row(await GetAsync(db, Week(analystId: Alice)), Alice);

        Assert.Equal(12m, row.DeliveredUnits);
        Assert.Equal(10m, row.OpenUnits);
    }

    [Fact]
    public async Task Capacity_UsesTheSectionSettingsRow_AndSaysSo()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        db.KpiSettings.Add(Setting(1, hours: 8m, horizon: 3));
        await db.SaveChangesAsync();
        await OpenAsync(db, 500, "PH", Alice);
        await OpenAsync(db, 501, "TAMC", Alice, ApprovalStatus.Pending);

        var dto = await GetAsync(db, Week(analystId: Alice));

        var row = Row(dto, Alice);
        Assert.False(dto.CapacityUsesDefaults);
        Assert.Equal(40m, row.CapacityUnits);              // 5 days x 8 h
        Assert.Equal(24m, row.ForwardCapacityUnits);       // Wed, Thu, Fri x 8 h
        Assert.Equal(6m, row.OpenUnits);
        Assert.Equal(0.25m, row.ForwardLoad);
        Assert.False(row.ForwardLoadOverCapacity);
    }

    [Fact]
    public async Task ForwardLoad_ReflectsPartialAvailability_AndPastLeaveReducesPeriodCapacity()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        db.AnalystAvailabilities.Add(Leave(Alice, new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 16), 0.5m)); // Thu, Fri half days
        db.AnalystAvailabilities.Add(Leave(Alice, new DateOnly(2026, 10, 6), new DateOnly(2026, 10, 6), 0m));     // Tue off
        await db.SaveChangesAsync();
        await OpenAsync(db, 500, "PH", Alice);
        await OpenAsync(db, 501, "TAMC", Alice);

        var row = Row(await GetAsync(db, Week(analystId: Alice)), Alice);

        Assert.Equal(24m, row.CapacityUnits);              // 4 days x 6 h
        Assert.Equal(24m, row.ForwardCapacityUnits);       // Wed 6 + Thu 3 + Fri 3 + Mon 6 + Tue 6
        Assert.Equal(0.25m, row.ForwardLoad);
    }

    [Fact] // Review Focus 1
    public async Task Row_OnFullLeaveWithOpenTests_ForwardLoadIsNullAndOverCapacity()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        db.AnalystAvailabilities.Add(Leave(Alice, new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 20), 0m));
        await db.SaveChangesAsync();
        await OpenAsync(db, 500, "PH", Alice);
        await OpenAsync(db, 501, "TAMC", Alice);

        var row = Row(await GetAsync(db, Week(analystId: Alice)), Alice);

        Assert.Equal(0m, row.ForwardCapacityUnits);
        Assert.Null(row.ForwardLoad);
        Assert.True(row.ForwardLoadOverCapacity);
        Assert.Equal(6m, row.OpenUnits);
    }

    [Fact] // Review Focus 2
    public async Task Capacity_WeekendOnlyPeriod_IsZeroAndUtilisationIsNull()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);

        var row = Row(await GetAsync(db, Query(new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 11), analystId: Alice)), Alice);

        Assert.Equal(0m, row.CapacityUnits);
        Assert.Null(row.Utilisation);
        Assert.False(row.UtilisationCheckProfile);
    }

    [Fact] // Review Focus 2
    public async Task Capacity_PeriodEndingInFuture_CountsOnlyDaysUpToToday()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);

        // Mon 12 .. Sun 18 Oct, but today is Wed 14: only Mon, Tue and Wed have happened.
        var row = Row(await GetAsync(db, Query(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 18), analystId: Alice)), Alice);

        Assert.Equal(18m, row.CapacityUnits);
    }

    [Fact] // Review Focus 3
    public async Task Analyst_InTwoSections_AppearsOnce_UsingLowestSectionWithSettings()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = Alice, DepartmentId = 1, SectionId = 2 });
        db.KpiSettings.Add(Setting(2, hours: 4m)); // only section 2 has a row
        await db.SaveChangesAsync();

        var first = await GetAsync(db, Week());
        Assert.Equal(20m, Assert.Single(first.Rows, r => r.UserId == Alice).CapacityUnits); // 5 x 4 h

        db.KpiSettings.Add(Setting(1, hours: 8m)); // now the lowest section has one too
        await db.SaveChangesAsync();
        var second = await GetAsync(db, Week());
        Assert.Equal(40m, Assert.Single(second.Rows, r => r.UserId == Alice).CapacityUnits); // 5 x 8 h
    }

    [Fact]
    public async Task Rows_AreSortedByNameIgnoringCase_NotByPerformance()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        for (var i = 1; i <= 3; i++) await DeliverAsync(db, i, "TAMC", Carol, At(8, 12)); // the busiest analyst

        var dto = await GetAsync(db, Week());

        Assert.Equal(new[] { "Alice Analyst", "Bob Analyst", "carol analyst", "Dana Analyst" }, dto.Rows.Select(r => r.FullName));
    }

    [Fact]
    public async Task InactiveAnalyst_AppearsOnlyWhenTheyDeliveredUnits()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        db.Users.Add(new User { Id = 6, FullName = "Eve Inactive", Username = "eve", RoleId = 1, PasswordHash = "x", IsActive = false });
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = 6, DepartmentId = 1, SectionId = 1 });
        await db.SaveChangesAsync();

        Assert.DoesNotContain((await GetAsync(db, Week())).Rows, r => r.UserId == 6);

        await DeliverAsync(db, 1, "TAMC", 6, At(8, 12));
        Assert.Contains((await GetAsync(db, Week())).Rows, r => r.UserId == 6);
    }

    [Fact]
    public async Task UnassignedOpenTests_AreCountedAsAGap_NotAttributedToAnyone()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        await OpenAsync(db, 500, "TAMC", null);

        var dto = await GetAsync(db, Week());

        Assert.Equal(1, dto.UnassignedOpenTests);
        Assert.All(dto.Rows, r => Assert.Equal(0m, r.OpenUnits));
        Assert.Equal(1, dto.Gaps.Single(g => g.Code == "UNASSIGNED_OPEN").Count);
    }

    [Fact]
    public async Task SectionScope_LimitsTestsAndAnalysts_AndAnEmptyScopeSeesNothing()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        await DeliverAsync(db, 1, "TAMC", Alice, At(8, 12));                  // section 1
        await DeliverAsync(db, 2, "TAMC", Dana, At(8, 12), sectionId: 2);      // section 2

        var section2 = await GetAsync(db, Query(WeekFrom, WeekTo, sections: new[] { 2 }));
        var nobody = await GetAsync(db, Query(WeekFrom, WeekTo, sections: Array.Empty<int>()));

        var dana = Assert.Single(section2.Rows);
        Assert.Equal(Dana, dana.UserId);
        Assert.Equal(1, dana.DeliveredTests);
        Assert.Empty(nobody.Rows);
    }

    [Fact] // Review Focus 8
    public async Task Delivered_IncludesExactlyFromMidnight_ExcludesExactlyToPlusOneDayMidnight()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        await DeliverAsync(db, 1, "TAMC", Alice, At(5, 0));   // 2026-10-05T00:00:00Z: first instant of the window, counted
        await DeliverAsync(db, 2, "TAMC", Alice, At(12, 0));  // 2026-10-12T00:00:00Z: window end is exclusive, not counted
        await DeliverAsync(db, 3, "TAMC", Alice, new DateTime(2026, 10, 4, 23, 59, 59, DateTimeKind.Utc)); // one second before, not counted

        var row = Row(await GetAsync(db, Week(analystId: Alice)), Alice);

        Assert.Equal(1, row.DeliveredTests);
        Assert.Equal(2m, row.DeliveredUnits);
    }
}
```
- [ ] **Step 2: Add the factory method, then run and see the tests fail**

In `TestServiceFactory.cs` (after `AnalystAvailability`):

```csharp
    public static AnalystWorkloadService AnalystWorkload(MicroLimsDbContext db, TimeProvider? time = null) =>
        new(db, WorkflowProfile(db, time), new UserSectionScopeService(db), time);
```
(`WorkflowProfile(db, time)` is the S1 factory method.)

Run: `dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~AnalystWorkloadServiceTests"`
Expected: build FAILS (`AnalystWorkloadService` does not exist).

- [ ] **Step 3: Implement the service**

Create `backend/MicroLIMS.Application/Services/Kpi/AnalystWorkloadService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Kpi;

// Work-unit analyst measures (spec 4.2). Everything is measured on work ASSIGNED to the analyst. There is no
// ranking: rows are alphabetical. A viewer without Kpi.ViewTeam only ever receives their own row (D8); that is
// enforced here, on the server, for every method.
public class AnalystWorkloadService
{
    private readonly IMicroLimsDbContext _db;
    private readonly WorkflowProfileService _profiles;
    private readonly IUserSectionScopeService _scope;
    private readonly TimeProvider _time;

    public AnalystWorkloadService(IMicroLimsDbContext db, WorkflowProfileService profiles, IUserSectionScopeService scope,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _profiles = profiles;
        _scope = scope;
        _time = timeProvider ?? TimeProvider.System;
    }

    private sealed record DeliveredRow(int TestOrderId, int SampleId, int SectionId, string TestCode, int AnalystId,
        string ControlNumber, SampleCategory Category, DateTime ApprovedAt, DateTime? SubmittedAt);

    private sealed record OpenRow(int TestOrderId, int SampleId, string TestCode, int? AnalystId,
        string ControlNumber, SampleCategory Category);

    private sealed record Loaded(List<DeliveredRow> Delivered, List<OpenRow> Open, WorkflowProfileResolver Resolver);

    private sealed class Acc
    {
        public int DeliveredTests, UnweightedTests, UnweightedOpenTests;
        public decimal DeliveredUnits, OpenUnits;
    }

    // Inclusive calendar dates in, half-open UTC window out: [from 00:00Z, to + 1 day 00:00Z).
    public async Task<KpiQuery> BuildQueryAsync(int userId, bool canViewTeam, DateOnly from, DateOnly to, string? category,
        string? location, string? testCode, int? analystId, CancellationToken ct = default)
    {
        if (to < from) throw new InvalidOperationException("The end date must not be before the start date.");
        // Same blank handling as S2's KpiDashboardService.BuildQueryAsync: "" and whitespace mean "not set".
        static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        category = Blank(category); location = Blank(location); testCode = Blank(testCode);
        if (category is not null) KpiScope.ParseCategory(category);
        var sectionIds = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        return new KpiQuery(
            from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            sectionIds, category, location, testCode, analystId, _time.GetUtcNow().UtcDateTime, userId, canViewTeam);
    }

    public async Task<AnalystWorkloadDto> GetAsync(KpiQuery q, CancellationToken ct)
    {
        var analystId = EffectiveAnalystId(q);
        var data = await LoadAsync(q, analystId, ct);
        var acc = Accumulate(data, q.Now);

        // Rows: active analysts in the viewer's scope, plus any assignee who delivered units in the period.
        var ids = new HashSet<int>(acc.Keys);
        var analysts = _db.Users.AsNoTracking().Where(u => u.IsActive && u.Role!.Type == RoleType.Analyst);
        if (analystId is int only) analysts = analysts.Where(u => u.Id == only);
        foreach (var id in await analysts.Select(u => u.Id).ToListAsync(ct))
            if (await KpiUserScope.InScopeAsync(_scope, id, q.SectionIds, ct)) ids.Add(id);
        var idList = ids.ToList();

        var names = await _db.Users.AsNoTracking().Where(u => idList.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var settings = await _db.KpiSettings.AsNoTracking().ToListAsync(ct);

        var filtered = !string.IsNullOrWhiteSpace(q.Category) || !string.IsNullOrWhiteSpace(q.Location) || !string.IsNullOrWhiteSpace(q.TestCode);
        var today = DateOnly.FromDateTime(q.Now);
        var periodFrom = DateOnly.FromDateTime(q.From);
        var periodEnd = DateOnly.FromDateTime(q.To).AddDays(-1);
        var capacityEnd = periodEnd < today ? periodEnd : today; // days that have not happened yet add no capacity
        var leaveFrom = periodFrom < today ? periodFrom : today;
        var leave = (await _db.AnalystAvailabilities.AsNoTracking()
                .Where(a => idList.Contains(a.UserId) && a.ToDate >= leaveFrom).ToListAsync(ct))
            .ToLookup(a => a.UserId);

        var rows = new List<AnalystWorkloadRowDto>();
        var accs = new List<Acc>();
        var usesDefaults = false;
        foreach (var id in idList)
        {
            var a = acc.TryGetValue(id, out var found) ? found : new Acc();
            accs.Add(a);

            var sections = await _scope.GetAccessibleSectionIdsAsync(id, ct);
            var setting = settings.Where(s => sections is null || sections.Contains(s.SectionId)).OrderBy(s => s.SectionId).FirstOrDefault();
            usesDefaults |= setting is null;
            var hours = setting?.HoursPerAnalystDay ?? KpiSettingDefaults.HoursPerAnalystDay;
            var mask = setting?.WorkingWeekdays ?? KpiSettingDefaults.WorkingWeekdays;
            var horizon = setting?.LoadHorizonDays ?? KpiSettingDefaults.LoadHorizonDays;
            var windows = leave[id].Select(l => (From: l.FromDate, To: l.ToDate, Fraction: l.FractionAvailable)).ToList();

            var capacity = CapacityCalculator.CapacityUnits(periodFrom, capacityEnd, hours, mask, windows);
            var forwardCapacity = CapacityCalculator.CapacityUnits(today, CapacityCalculator.HorizonEnd(today, horizon, mask), hours, mask, windows);
            rows.Add(ToRow(id, names.GetValueOrDefault(id, "(unknown user)"), a, capacity, forwardCapacity, filtered));
        }

        var unassigned = analystId is null ? data.Open.Count(o => o.AnalystId is null) : 0;
        var sorted = rows.OrderBy(r => r.FullName, StringComparer.OrdinalIgnoreCase).ToList();
        var gaps = BuildGaps(accs, unassigned);
        // Capacity is the analyst's whole availability, so against a narrowed slice of work a ratio would read as under-load.
        if (filtered) gaps.Add(new KpiGap("CAPACITY_NOT_FILTERED", "Utilisation and load are shown for all work only; clear the category, location and test filters to see them", sorted.Count));
        return new AnalystWorkloadDto(q.From, q.To, usesDefaults, !q.CanViewTeam, unassigned, sorted, gaps);
    }

    private static AnalystWorkloadRowDto ToRow(int id, string name, Acc a, decimal capacity, decimal forwardCapacity, bool filtered)
    {
        // Unweighted work (e.g. profiles still Draft) contributes 0 units, which is "unknown", not "idle": show no ratio.
        var allDeliveredUnweighted = a.DeliveredTests > 0 && a.DeliveredUnits == 0 && a.UnweightedTests == a.DeliveredTests;
        var allOpenUnweighted = a.OpenUnits == 0 && a.UnweightedOpenTests > 0;
        decimal? utilisation = !filtered && !allDeliveredUnweighted && capacity > 0 ? a.DeliveredUnits / capacity : null;
        decimal? forwardLoad = !filtered && !allOpenUnweighted && forwardCapacity > 0 ? a.OpenUnits / forwardCapacity : null;
        // With no forward capacity at all, any open work is over capacity; a ratio would divide by zero.
        var over = !filtered && !allOpenUnweighted && (forwardCapacity > 0 ? a.OpenUnits / forwardCapacity > 1m : a.OpenUnits > 0);
        return new AnalystWorkloadRowDto(id, name, capacity, a.DeliveredUnits, utilisation, utilisation > 1m,
            a.OpenUnits, forwardCapacity, forwardLoad, over, null, null,
            a.DeliveredTests, a.UnweightedTests, a.UnweightedOpenTests, 0);
    }

    private static List<KpiGap> BuildGaps(IReadOnlyCollection<Acc> accs, int unassigned)
    {
        var gaps = new List<KpiGap>();
        var unweighted = accs.Sum(a => a.UnweightedTests + a.UnweightedOpenTests);
        if (unweighted > 0) gaps.Add(new KpiGap("UNWEIGHTED_TESTS", $"{unweighted} tests unweighted (no work-unit profile)", unweighted));
        if (unassigned > 0) gaps.Add(new KpiGap("UNASSIGNED_OPEN", $"{unassigned} open tests have no analyst assigned", unassigned));
        return gaps;
    }

    private static Dictionary<int, Acc> Accumulate(Loaded data, DateTime now)
    {
        var acc = new Dictionary<int, Acc>();
        Acc For(int id)
        {
            if (!acc.TryGetValue(id, out var a)) acc[id] = a = new Acc();
            return a;
        }

        foreach (var d in data.Delivered)
        {
            var a = For(d.AnalystId);
            a.DeliveredTests++;
            if (data.Resolver.Resolve(d.TestCode, d.ApprovedAt).WorkUnits is decimal units) a.DeliveredUnits += units;
            else a.UnweightedTests++;
        }
        foreach (var o in data.Open.Where(o => o.AnalystId != null))
        {
            var a = For(o.AnalystId!.Value);
            if (data.Resolver.Resolve(o.TestCode, now).WorkUnits is decimal units) a.OpenUnits += units;
            else a.UnweightedOpenTests++;
        }
        return acc;
    }

    private async Task<Loaded> LoadAsync(KpiQuery q, int? analystId, CancellationToken ct)
    {
        var resolver = await _profiles.LoadResolverAsync(ct);

        // Delivered: the test is approved and not superseded, and its own section's signoff approved it in the period.
        // The signoff (not ResultRecord.ApprovedAt) is used because ResultRecord.ApprovedAt is sample-level.
        var delivered = await Orders(q, analystId)
            .Where(t => t.Status == ApprovalStatus.Approved && !t.IsSuperseded && t.AssignedAnalystId != null)
            .Join(_db.SampleSectionSignoffs, t => new { t.SampleId, t.SectionId }, so => new { so.SampleId, so.SectionId }, (t, so) => new { t, so })
            .Where(x => x.so.Status == SectionSignoffStatus.Approved && x.so.ApprovalDecision == ApprovalDecision.Approve
                        && x.so.ApprovedAt >= q.From && x.so.ApprovedAt < q.To)
            .Select(x => new DeliveredRow(x.t.Id, x.t.SampleId, x.t.SectionId, x.t.TestCode, x.t.AssignedAnalystId!.Value,
                x.t.Sample!.ControlNumber, x.t.Sample.Category, x.so.ApprovedAt!.Value, x.so.SubmittedForReviewAt))
            .ToListAsync(ct);

        var open = await Orders(q, analystId)
            .Where(t => (t.Status == ApprovalStatus.Pending || t.Status == ApprovalStatus.InProgress) && !t.IsSuperseded)
            .Select(t => new OpenRow(t.Id, t.SampleId, t.TestCode, t.AssignedAnalystId, t.Sample!.ControlNumber, t.Sample.Category))
            .ToListAsync(ct);

        return new Loaded(delivered, open, resolver);
    }

    // D8: without Kpi.ViewTeam the only analyst a caller may look at is themselves.
    private static int? EffectiveAnalystId(KpiQuery q)
    {
        if (q.CanViewTeam) return q.AnalystId;
        if (q.AnalystId is int asked && asked != q.ViewerUserId)
            throw new UnauthorizedAccessException("You may only view your own workload figures.");
        return q.ViewerUserId;
    }

    private IQueryable<TestOrder> Orders(KpiQuery q, int? analystId)
    {
        var orders = _db.TestOrders.AsNoTracking().Where(SectionReviewQueues.OrderIn(q.SectionIds));
        if (analystId is int a) orders = orders.Where(t => t.AssignedAnalystId == a);
        if (!string.IsNullOrWhiteSpace(q.TestCode)) orders = orders.Where(t => t.TestCode == q.TestCode);
        if (q.Category is not null)
        {
            var category = KpiScope.ParseCategory(q.Category); // S2 helper: one parser for every KPI endpoint
            orders = orders.Where(t => t.Sample!.Category == category);
        }
        if (!string.IsNullOrWhiteSpace(q.Location))
        {
            var location = q.Location;
            // Same meaning as the legacy KPI filter: product name or water sampling point code.
            orders = orders.Where(t => _db.Samples.Any(s => s.Id == t.SampleId && (s.Item!.Name == location || s.WaterSamplingPoint!.Code == location)));
        }
        return orders;
    }
}
```

- [ ] **Step 4: Verify the location filter and run**

Verify the location filter still matches the legacy one: use the Grep tool for `s.Item!.Name == location` in `backend/MicroLIMS.Application/Services/KpiService.cs` and confirm the expression is identical.

Run the Step 2 test command. Expected: 21 passed, 0 failed.

- [ ] **Step 5: Commit**

```
git add backend/MicroLIMS.Application/Services/Kpi/AnalystWorkloadService.cs backend/MicroLIMS.Tests/UnitTests/Kpi/AnalystWorkloadServiceTests.cs
git add -p backend/MicroLIMS.Tests/TestServiceFactory.cs
git commit -m "feat(kpi): analyst workload units, utilisation and forward load" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Weighted on-time, weighted right-first-time, minimum sample and gaps

**Files:**
- Modify: `backend/MicroLIMS.Application/Services/Kpi/AnalystWorkloadService.cs` (six replacements below)
- Test: `backend/MicroLIMS.Tests/UnitTests/Kpi/AnalystWorkloadServiceTests.cs` (append tests)

**Interfaces:**
- Consumes: Task 5 service; `KpiConstants.MinSampleForRate` (= 5) and `KpiConstants.DefaultTargetDays` (= 7); `TestOrder`/`TestReturnEvent`/`SamplePreparation`/`SampleSectionSignoff.SubmittedForReviewAt`.
- Produces: `WeightedOnTime`, `WeightedRightFirstTime`, `TestsOnDefaultTarget` populated; gap codes `DEFAULT_TARGET`, `UNJUDGED_ON_TIME`, `OBSERVATION_RETURNS_NOT_TRACKED`, `RATES_TOO_FEW_RECORDS`.

- [ ] **Step 1: Append the failing tests**

Append inside the `AnalystWorkloadServiceTests` class (before the final `}`):

```csharp
    [Fact]
    public async Task Rates_AreHidden_BelowTheMinimumSample_AndTheGapSaysSo()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        for (var i = 1; i <= 4; i++) await DeliverAsync(db, i, "TAMC", Alice, At(8, 12)); // 4 < MinSampleForRate (5)

        var dto = await GetAsync(db, Week(analystId: Alice));

        var row = Row(dto, Alice);
        Assert.Null(row.WeightedOnTime);
        Assert.Null(row.WeightedRightFirstTime);
        Assert.Equal(1, dto.Gaps.Single(g => g.Code == "RATES_TOO_FEW_RECORDS").Count);
    }

    [Fact]
    public async Task WeightedOnTime_AndRightFirstTime_AreWeightedByUnits_NotByCount()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        // Prepared Mon 5 Oct 09:00. TAMC target 5 days -> deadline Sat 10 Oct 09:00; PH target 10 days.
        await DeliverAsync(db, 1, "TAMC", Bob, At(9, 12), returned: true); // submitted 9 Oct 10:00: on time, but returned
        await DeliverAsync(db, 2, "TAMC", Bob, At(9, 12));                  // on time
        await DeliverAsync(db, 3, "PH", Bob, At(9, 12));                    // on time (4 units)
        await DeliverAsync(db, 4, "TAMC", Bob, At(11, 12), submittedAt: At(11, 10)); // submitted after the deadline: late
        await DeliverAsync(db, 5, "TAMC", Bob, At(11, 12), submittedAt: At(11, 10)); // late

        var row = Row(await GetAsync(db, Week(analystId: Bob)), Bob);

        Assert.Equal(12m, row.DeliveredUnits);
        Assert.Equal(0.6667m, Math.Round(row.WeightedOnTime!.Value, 4));         // 8 of 12 units on time (count-based would be 0.6)
        Assert.Equal(0.8333m, Math.Round(row.WeightedRightFirstTime!.Value, 4)); // 10 of 12 units not returned (count-based would be 0.8)
    }

    [Fact]
    public async Task Observation_TestsAreExcludedFromRightFirstTime_AndReported()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        for (var i = 1; i <= 5; i++) await DeliverAsync(db, i, "TAMC", Bob, At(7, 12));
        await DeliverAsync(db, 6, "OBS", Bob, At(7, 12), returned: true); // a return event cannot normally exist for Observation

        var dto = await GetAsync(db, Week(analystId: Bob));

        var row = Row(dto, Bob);
        Assert.Equal(11m, row.DeliveredUnits);                // the observation test still earns its unit
        Assert.Equal(1m, row.WeightedRightFirstTime);          // 10 of 10 units across the five tracked tests
        Assert.Equal(1, dto.Gaps.Single(g => g.Code == "OBSERVATION_RETURNS_NOT_TRACKED").Count);
    }

    [Fact]
    public async Task OnTime_SampleWithoutAPreparationRow_IsNotJudged_AndReported()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        for (var i = 1; i <= 4; i++) await DeliverAsync(db, i, "TAMC", Alice, At(8, 12));
        await DeliverAsync(db, 5, "TAMC", Alice, At(8, 12), noPreparation: true);

        var dto = await GetAsync(db, Week(analystId: Alice));

        var row = Row(dto, Alice);
        Assert.Null(row.WeightedOnTime);                  // only 4 judged tests
        Assert.Equal(1m, row.WeightedRightFirstTime);     // all 5 tests are tracked for returns
        Assert.Equal(1, dto.Gaps.Single(g => g.Code == "UNJUDGED_ON_TIME").Count);
    }

    [Fact] // Review Focus 4
    public async Task OnTime_SampleWithUnresolvedSiblingTarget_UsesSevenDayFallback()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        for (var i = 1; i <= 5; i++)
        {
            // Submitted Sun 11 Oct 10:00: late against TAMC's own 5-day target (Sat 10 Oct 09:00), fine against the 7-day fallback (Mon 12 Oct 09:00).
            await DeliverAsync(db, i, "TAMC", Alice, At(11, 12), submittedAt: At(11, 10));
            await OpenAsync(db, 100 + i, "NOPROF", Bob, sampleId: i); // same sample and section, no resolvable target
        }

        var row = Row(await GetAsync(db, Week(analystId: Alice)), Alice);

        Assert.Equal(1m, row.WeightedOnTime);
    }

    [Fact]
    public async Task OnTime_WithoutASlowSibling_UsesTheResolvedFiveDayTarget()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        for (var i = 1; i <= 5; i++)
            await DeliverAsync(db, i, "TAMC", Alice, At(11, 12), submittedAt: At(11, 10));

        var row = Row(await GetAsync(db, Week(analystId: Alice)), Alice);

        Assert.Equal(0m, row.WeightedOnTime);
    }

    [Fact]
    public async Task DefaultTarget_CountsDeliveredTestsWithNoResolvedTarget()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        await DeliverAsync(db, 1, "NOPROF", Alice, At(8, 12));
        await DeliverAsync(db, 2, "TAMC", Alice, At(8, 12));

        var dto = await GetAsync(db, Week(analystId: Alice));

        Assert.Equal(1, Row(dto, Alice).TestsOnDefaultTarget);
        Assert.Equal(1, dto.Gaps.Single(g => g.Code == "DEFAULT_TARGET").Count);
    }

    [Fact] // Review Focus 9
    public async Task TwoAnalystsOnOneSample_BothInheritTheSampleOnTimeVerdict_AndSiblingTargetUsesBothTests()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        for (var i = 1; i <= 5; i++)
        {
            // One sample, one section: Alice's TAMC (5-day target) and Bob's PH (10-day target). Prepared Mon 5 Oct 09:00, submitted
            // Sun 11 Oct 10:00: late against 5 days (Sat 10 Oct) but inside the sample deadline, the max of its tests' targets (Mon 15 Oct).
            await DeliverAsync(db, i, "TAMC", Alice, At(11, 12), submittedAt: At(11, 10), sampleId: i);
            await DeliverAsync(db, 10 + i, "PH", Bob, At(11, 12), submittedAt: At(11, 10), sampleId: i);
        }

        var dto = await GetAsync(db, Week());

        Assert.Equal(1m, Row(dto, Alice).WeightedOnTime); // would be 0 if Alice were judged on her own 5-day test alone
        Assert.Equal(1m, Row(dto, Bob).WeightedOnTime);   // same verdict, inherited from the sample
    }

    [Fact] // Review Focus 10
    public async Task RetestCloneSample_EarnsUnits_AndIsReportedUnjudged()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        for (var i = 1; i <= 4; i++) await DeliverAsync(db, i, "TAMC", Alice, At(8, 12));
        await DeliverAsync(db, 5, "TAMC", Alice, At(8, 12), noPreparation: true); // a retest clone has no SamplePreparation
        (await db.Samples.SingleAsync(s => s.Id == 5)).OriginSampleId = 1;
        await db.SaveChangesAsync();

        var dto = await GetAsync(db, Week(analystId: Alice));

        var row = Row(dto, Alice);
        Assert.Equal(5, row.DeliveredTests);            // retests earn units like any test (spec 4.2)
        Assert.Equal(10m, row.DeliveredUnits);
        Assert.Null(row.WeightedOnTime);                // only 4 judged tests, so below the minimum sample
        Assert.Equal(1, dto.Gaps.Single(g => g.Code == "UNJUDGED_ON_TIME").Count);
    }
```

- [ ] **Step 2: Run and see them fail**

Run: `dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~AnalystWorkloadServiceTests"`
Expected: the nine new tests FAIL (rates are `null`, gap codes missing, `TestsOnDefaultTarget` is 0); the Task 5 tests still pass.

- [ ] **Step 3: Implement - replace six blocks in `AnalystWorkloadService.cs`**

(1) Replace the `Acc` class:

```csharp
    private sealed class Acc
    {
        public int DeliveredTests, UnweightedTests, UnweightedOpenTests, TestsOnDefaultTarget;
        public int JudgedTests, UnjudgedTests, RftTests, ObservationTests;
        public decimal DeliveredUnits, OpenUnits, JudgedUnits, OnTimeUnits, RftUnits, RftDenominator;
    }
```

(2) Replace the `Loaded` record:

```csharp
    private sealed record Loaded(List<DeliveredRow> Delivered, List<OpenRow> Open, WorkflowProfileResolver Resolver,
        Dictionary<(int SampleId, int SectionId), bool?> OnTime, HashSet<int> Returned, Dictionary<string, WorkflowType> Types);
```

(3) In `LoadAsync`, replace `return new Loaded(delivered, open, resolver);` with:

```csharp
        var sampleIds = delivered.Select(d => d.SampleId).Distinct().ToList();

        // The analyst-stage clock starts when the sample was prepared (assigned for testing).
        var prepared = (await _db.SamplePreparations.AsNoTracking()
                .Where(p => sampleIds.Contains(p.SampleId)).Select(p => new { p.SampleId, p.PreparedAt }).ToListAsync(ct))
            .GroupBy(p => p.SampleId).ToDictionary(g => g.Key, g => g.Min(p => p.PreparedAt));

        // Every live test of the delivered samples sets the deadline, including tests of other analysts and open ones.
        var siblings = (await _db.TestOrders.AsNoTracking()
                .Where(t => sampleIds.Contains(t.SampleId) && !t.IsSuperseded
                            && t.Status != ApprovalStatus.Voided && t.Status != ApprovalStatus.Cancelled)
                .Select(t => new { t.SampleId, t.SectionId, t.TestCode }).ToListAsync(ct))
            .ToLookup(t => (t.SampleId, t.SectionId), t => t.TestCode);

        var onTime = new Dictionary<(int SampleId, int SectionId), bool?>();
        foreach (var g in delivered.GroupBy(d => (d.SampleId, d.SectionId)))
        {
            var first = g.First();
            if (!prepared.TryGetValue(first.SampleId, out var start) || first.SubmittedAt is not DateTime submitted)
            {
                onTime[g.Key] = null; // not judgeable: no assignment time or no submit time
                continue;
            }
            // Deadline = start + the longest target among the section's tests; a test with no resolvable target counts as the
            // 7-day fallback. One rule for tile 2 (S2) and this table: S2's AnalystStageTarget (empty list -> 7 days).
            var days = AnalystStageTarget.DaysFor(siblings[g.Key], first.ApprovedAt, resolver).Days;
            onTime[g.Key] = submitted <= start.AddDays((double)days);
        }

        var orderIds = delivered.Select(d => d.TestOrderId).ToList();
        var returned = (await _db.TestReturnEvents.AsNoTracking().Where(r => orderIds.Contains(r.TestOrderId))
            .Select(r => r.TestOrderId).Distinct().ToListAsync(ct)).ToHashSet();

        var codes = delivered.Select(d => d.TestCode).Distinct().ToList();
        var types = await _db.TestDefinitions.AsNoTracking().Where(d => codes.Contains(d.Code))
            .ToDictionaryAsync(d => d.Code, d => d.WorkflowType, ct);

        return new Loaded(delivered, open, resolver, onTime, returned, types);
```

(4) In `Accumulate`, replace the first `foreach (var d in data.Delivered) { ... }` block with:

```csharp
        foreach (var d in data.Delivered)
        {
            var a = For(d.AnalystId);
            var profile = data.Resolver.Resolve(d.TestCode, d.ApprovedAt);
            a.DeliveredTests++;
            if (!profile.IsTargetAware) a.TestsOnDefaultTarget++;
            if (profile.WorkUnits is not decimal units)
            {
                a.UnweightedTests++;
                continue; // unweighted tests leave every numerator and denominator
            }
            a.DeliveredUnits += units;

            if (data.OnTime[(d.SampleId, d.SectionId)] is bool onTime)
            {
                a.JudgedTests++;
                a.JudgedUnits += units;
                if (onTime) a.OnTimeUnits += units;
            }
            else
            {
                a.UnjudgedTests++;
            }

            // Observation tests cannot generate a TestReturnEvent, so including them would overstate right-first-time.
            if (data.Types.TryGetValue(d.TestCode, out var type) && type == WorkflowType.Observation)
            {
                a.ObservationTests++;
            }
            else
            {
                a.RftTests++;
                a.RftDenominator += units;
                if (!data.Returned.Contains(d.TestOrderId)) a.RftUnits += units;
            }
        }
```

(5) Replace `ToRow`:

```csharp
    private static AnalystWorkloadRowDto ToRow(int id, string name, Acc a, decimal capacity, decimal forwardCapacity, bool filtered)
    {
        // Unweighted work (e.g. profiles still Draft) contributes 0 units, which is "unknown", not "idle": show no ratio.
        var allDeliveredUnweighted = a.DeliveredTests > 0 && a.DeliveredUnits == 0 && a.UnweightedTests == a.DeliveredTests;
        var allOpenUnweighted = a.OpenUnits == 0 && a.UnweightedOpenTests > 0;
        decimal? utilisation = !filtered && !allDeliveredUnweighted && capacity > 0 ? a.DeliveredUnits / capacity : null;
        decimal? forwardLoad = !filtered && !allOpenUnweighted && forwardCapacity > 0 ? a.OpenUnits / forwardCapacity : null;
        // With no forward capacity at all, any open work is over capacity; a ratio would divide by zero.
        var over = !filtered && !allOpenUnweighted && (forwardCapacity > 0 ? a.OpenUnits / forwardCapacity > 1m : a.OpenUnits > 0);
        // A rate from fewer than MinSampleForRate tests is noise (and, per person, pressure): hide it.
        decimal? onTime = a.JudgedTests >= KpiConstants.MinSampleForRate && a.JudgedUnits > 0 ? a.OnTimeUnits / a.JudgedUnits : null;
        decimal? rft = a.RftTests >= KpiConstants.MinSampleForRate && a.RftDenominator > 0 ? a.RftUnits / a.RftDenominator : null;
        return new AnalystWorkloadRowDto(id, name, capacity, a.DeliveredUnits, utilisation, utilisation > 1m,
            a.OpenUnits, forwardCapacity, forwardLoad, over, onTime, rft,
            a.DeliveredTests, a.UnweightedTests, a.UnweightedOpenTests, a.TestsOnDefaultTarget);
    }
```

(6) Replace `BuildGaps`:

```csharp
    private static List<KpiGap> BuildGaps(IReadOnlyCollection<Acc> accs, int unassigned)
    {
        var gaps = new List<KpiGap>();
        var unweighted = accs.Sum(a => a.UnweightedTests + a.UnweightedOpenTests);
        var defaultTarget = accs.Sum(a => a.TestsOnDefaultTarget);
        var unjudged = accs.Sum(a => a.UnjudgedTests);
        var observation = accs.Sum(a => a.ObservationTests);
        var hidden = accs.Count(a => a.JudgedTests is > 0 and < KpiConstants.MinSampleForRate
                                     || a.RftTests is > 0 and < KpiConstants.MinSampleForRate);
        if (unweighted > 0) gaps.Add(new KpiGap("UNWEIGHTED_TESTS", $"{unweighted} tests unweighted (no work-unit profile)", unweighted));
        if (defaultTarget > 0) gaps.Add(new KpiGap("DEFAULT_TARGET", $"{defaultTarget} tests on the default {KpiConstants.DefaultTargetDays}-day target", defaultTarget));
        if (unjudged > 0) gaps.Add(new KpiGap("UNJUDGED_ON_TIME", $"{unjudged} tests without an assignment time cannot be judged for on time", unjudged));
        if (observation > 0) gaps.Add(new KpiGap("OBSERVATION_RETURNS_NOT_TRACKED", $"{observation} observation tests not tracked for returns", observation));
        if (hidden > 0) gaps.Add(new KpiGap("RATES_TOO_FEW_RECORDS", $"Rates hidden for {hidden} analysts with fewer than {KpiConstants.MinSampleForRate} tests", hidden));
        if (unassigned > 0) gaps.Add(new KpiGap("UNASSIGNED_OPEN", $"{unassigned} open tests have no analyst assigned", unassigned));
        return gaps;
    }
```

- [ ] **Step 4: Run and see them pass**

Run the Step 2 command. Expected: 30 passed, 0 failed.

- [ ] **Step 5: Commit**

```
git add backend/MicroLIMS.Application/Services/Kpi/AnalystWorkloadService.cs backend/MicroLIMS.Tests/UnitTests/Kpi/AnalystWorkloadServiceTests.cs
git commit -m "feat(kpi): weighted on-time and right-first-time per analyst" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Records behind the figures (`GetTestsAsync`)

**Files:**
- Modify: `backend/MicroLIMS.Application/Services/Kpi/AnalystWorkloadService.cs` (insert one method before `EffectiveAnalystId`)
- Test: `backend/MicroLIMS.Tests/UnitTests/Kpi/AnalystWorkloadServiceTests.cs` (append tests)

**Interfaces:**
- Consumes: Tasks 5-6 `LoadAsync`/`Loaded`; S2 `KpiRecordDto(int? SampleId, int? TestOrderId, string? ControlNumber, string? TestCode, string? Category, string? AnalystName, DateTime? EventAt, decimal? Value, string? Unit, string Inclusion, string? Note)` and `KpiRecordPageDto(string Key, int Page, int PageSize, int Total, IReadOnlyList<KpiRecordDto> Items)`.
- Produces: `Task<KpiRecordPageDto> GetTestsAsync(KpiQuery q, int page, int pageSize, CancellationToken ct)`; page numbers start at 1, `pageSize` defaults to 50 and is capped at 200; `Key = "workload"`.

- [ ] **Step 1: Append the failing tests**

```csharp
    [Fact]
    public async Task GetTests_ListsDeliveredThenOpenTests_WithTheirInclusion()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        await DeliverAsync(db, 1, "TAMC", Alice, At(8, 12));
        await DeliverAsync(db, 2, "TAMC", Alice, At(9, 12));
        await DeliverAsync(db, 3, "NOPROF", Alice, At(7, 12));
        await OpenAsync(db, 500, "PH", Alice);

        var page = await TestServiceFactory.AnalystWorkload(db, Clock()).GetTestsAsync(Week(analystId: Alice), 1, 50, default);

        Assert.Equal("workload", page.Key);
        Assert.Equal(4, page.Total);
        Assert.Equal(new int?[] { 2, 1, 3, 500 }, page.Items.Select(i => i.TestOrderId));        // newest approval first, open last
        Assert.Equal(new[] { "Numerator", "Numerator", "Excluded", "Denominator" }, page.Items.Select(i => i.Inclusion));
        Assert.Equal(2m, page.Items[0].Value);
        Assert.Null(page.Items[2].Value);
        Assert.Contains("Unweighted", page.Items[2].Note);
        Assert.Contains("Alice Analyst", page.Items.Select(i => i.AnalystName));
    }

    [Theory]
    [InlineData(1000, 200)] // capped
    [InlineData(0, 50)]     // default
    public async Task GetTests_CapsAndDefaultsThePageSize(int requested, int expected)
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);

        var page = await TestServiceFactory.AnalystWorkload(db, Clock()).GetTestsAsync(Week(analystId: Alice), 1, requested, default);

        Assert.Equal(expected, page.PageSize);
    }

    [Fact]
    public async Task GetTests_SecondPage_ReturnsTheRemainder()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        for (var i = 1; i <= 4; i++) await DeliverAsync(db, i, "TAMC", Alice, At(8, 12));

        var page = await TestServiceFactory.AnalystWorkload(db, Clock()).GetTestsAsync(Week(analystId: Alice), 2, 3, default);

        Assert.Equal(4, page.Total);
        Assert.Single(page.Items);
    }
```

- [ ] **Step 2: Run and see them fail**

Run: `dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~AnalystWorkloadServiceTests"`
Expected: build FAILS (`GetTestsAsync` does not exist).

- [ ] **Step 3: Implement**

Insert before the line `    // D8: without Kpi.ViewTeam the only analyst a caller may look at is themselves.` in `AnalystWorkloadService.cs`:

```csharp
    // The tests behind the row figures, so any number can be traced to records. Same scope and D8 rules as GetAsync.
    public async Task<KpiRecordPageDto> GetTestsAsync(KpiQuery q, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = pageSize <= 0 ? 50 : Math.Min(pageSize, 200);
        var data = await LoadAsync(q, EffectiveAnalystId(q), ct);

        var ids = data.Delivered.Select(d => d.AnalystId)
            .Concat(data.Open.Where(o => o.AnalystId != null).Select(o => o.AnalystId!.Value)).Distinct().ToList();
        var names = await _db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        string? Name(int? id) => id is int i && names.TryGetValue(i, out var n) ? n : null;

        var items = new List<KpiRecordDto>();
        foreach (var d in data.Delivered.OrderByDescending(d => d.ApprovedAt).ThenBy(d => d.TestOrderId))
        {
            var profile = data.Resolver.Resolve(d.TestCode, d.ApprovedAt);
            var notes = new List<string>();
            if (profile.WorkUnits is null) notes.Add("Unweighted: no work-unit profile");
            if (!profile.IsTargetAware) notes.Add($"Default {KpiConstants.DefaultTargetDays}-day target");
            if (data.Returned.Contains(d.TestOrderId)) notes.Add("Returned to analyst");
            notes.Add(data.OnTime[(d.SampleId, d.SectionId)] switch
            {
                true => "On time",
                false => "Late",
                null => "On time not judged: no assignment time"
            });
            items.Add(new KpiRecordDto(d.SampleId, d.TestOrderId, d.ControlNumber, d.TestCode, d.Category.ToString(), Name(d.AnalystId),
                d.ApprovedAt, profile.WorkUnits, "units", profile.WorkUnits is null ? "Excluded" : "Numerator", string.Join("; ", notes)));
        }
        foreach (var o in data.Open.OrderBy(o => o.TestOrderId))
        {
            var profile = data.Resolver.Resolve(o.TestCode, q.Now);
            items.Add(new KpiRecordDto(o.SampleId, o.TestOrderId, o.ControlNumber, o.TestCode, o.Category.ToString(), Name(o.AnalystId),
                null, profile.WorkUnits, "units", profile.WorkUnits is null ? "Excluded" : "Denominator",
                profile.WorkUnits is null ? "Open and unweighted" : "Open: counts toward forward load"));
        }

        var pageItems = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new KpiRecordPageDto("workload", page, pageSize, items.Count, pageItems);
    }

```

- [ ] **Step 4: Run and see them pass**

Run the Step 2 command. Expected: 34 passed, 0 failed.

- [ ] **Step 5: Commit**

```
git add backend/MicroLIMS.Application/Services/Kpi/AnalystWorkloadService.cs backend/MicroLIMS.Tests/UnitTests/Kpi/AnalystWorkloadServiceTests.cs
git commit -m "feat(kpi): records behind the analyst workload figures" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 8: `KpiWorkloadController`, DI, authorization matrix, own-row-only tests

**Files:**
- Create: `backend/MicroLIMS.API/Controllers/KpiWorkloadController.cs`
- Modify: `backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs` (after the S1/S2 KPI registrations, which follow the `services.AddScoped<KpiService>();` line at about :97)
- Modify: `backend/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt` (regenerated)
- Test: `backend/MicroLIMS.Tests/UnitTests/Kpi/WorkloadOwnRowOnlyTests.cs`

**Interfaces:**
- Consumes: Tasks 3-7 services; `PermissionConstants.KpiView`, `PermissionConstants.KpiConfigureProfiles` (S1), `PermissionConstants.KpiViewTeam` (S2); `ApiResponse<object>.Ok(object)` (`MicroLIMS.Shared.Responses`, property `Data`).
- Produces: routes under `api/kpi` (class policy `Kpi.View`): `GET workload`, `GET workload/tests`, `GET settings/{sectionId}`, `PUT settings/{sectionId}` (+ `Kpi.ConfigureProfiles`, `If-Match` handled by `RecordVersionMiddleware`), `GET availability`, `POST availability` and `PUT availability/{id}` (+ `Kpi.ConfigureProfiles`). Action names: `GetWorkload`, `GetWorkloadTests`, `GetSettings`, `UpdateSettings`, `ListAvailability`, `CreateAvailability`, `UpdateAvailability`.

- [ ] **Step 1: Write the failing tests**

Create `backend/MicroLIMS.Tests/UnitTests/Kpi/WorkloadOwnRowOnlyTests.cs`:

```csharp
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;
using Xunit;
using static MicroLIMS.Tests.UnitTests.Kpi.WorkloadTestData;

namespace MicroLIMS.Tests.UnitTests.Kpi;

// D8: an analyst sees only their own numbers, enforced on the server. These tests go through the controller so the
// claim handling is covered too: "viewTeam" means the JWT carries the Kpi.ViewTeam permission claim.
public class WorkloadOwnRowOnlyTests
{
    private static KpiWorkloadController Controller(MicroLimsDbContext db, int userId, bool viewTeam)
    {
        var time = Clock();
        var controller = new KpiWorkloadController(
            TestServiceFactory.AnalystWorkload(db, time), TestServiceFactory.KpiSettings(db, time), TestServiceFactory.AnalystAvailability(db, time));
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
        if (viewTeam) claims.Add(new Claim("permission", PermissionConstants.KpiViewTeam));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
        };
        return controller;
    }

    private static T Data<T>(IActionResult result) => (T)((ApiResponse<object>)((OkObjectResult)result).Value!).Data!;

    private static async Task<MicroLimsDbContext> SeededAsync()
    {
        var db = NewDb();
        await SeedBaselineAsync(db);
        await DeliverAsync(db, 1, "TAMC", Alice, At(8, 12));
        await DeliverAsync(db, 2, "TAMC", Alice, At(8, 12));
        await DeliverAsync(db, 3, "TAMC", Bob, At(8, 12));
        await DeliverAsync(db, 4, "TAMC", Bob, At(8, 12));
        await OpenAsync(db, 500, "PH", Alice);
        await OpenAsync(db, 501, "PH", Bob);
        db.AnalystAvailabilities.AddRange(
            new AnalystAvailability { UserId = Alice, FromDate = new(2026, 10, 14), ToDate = new(2026, 10, 14), FractionAvailable = 0, Reason = "Leave", EnteredByUserId = Sam },
            new AnalystAvailability { UserId = Bob, FromDate = new(2026, 10, 15), ToDate = new(2026, 10, 15), FractionAvailable = 0, Reason = "Leave", EnteredByUserId = Sam });
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Workload_WithoutViewTeam_ReturnsOnlyTheCallersRow()
    {
        await using var db = await SeededAsync();

        var dto = Data<AnalystWorkloadDto>(await Controller(db, Alice, false).GetWorkload(WeekFrom, WeekTo, null, null, null, null));

        Assert.True(dto.OnlyOwnRow);
        Assert.Equal(Alice, Assert.Single(dto.Rows).UserId);
    }

    [Fact]
    public async Task Workload_WithoutViewTeam_AskingForAnotherAnalyst_IsForbidden()
    {
        await using var db = await SeededAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Controller(db, Alice, false).GetWorkload(WeekFrom, WeekTo, null, null, null, Bob));
    }

    [Fact]
    public async Task Workload_WithoutViewTeam_AskingForTheirOwnId_IsAllowed()
    {
        await using var db = await SeededAsync();

        var dto = Data<AnalystWorkloadDto>(await Controller(db, Alice, false).GetWorkload(WeekFrom, WeekTo, null, null, null, Alice));

        Assert.Equal(Alice, Assert.Single(dto.Rows).UserId);
    }

    [Fact]
    public async Task WorkloadTests_WithoutViewTeam_ListOnlyTheCallersTests_AndRefuseAnotherAnalyst()
    {
        await using var db = await SeededAsync();
        var controller = Controller(db, Alice, false);

        var page = Data<KpiRecordPageDto>(await controller.GetWorkloadTests(WeekFrom, WeekTo, null, null, null, null, 1, 50));

        Assert.Equal(3, page.Total); // two delivered + one open, all Alice's
        Assert.All(page.Items, i => Assert.Equal("Alice Analyst", i.AnalystName));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            controller.GetWorkloadTests(WeekFrom, WeekTo, null, null, null, Bob, 1, 50));
    }

    [Fact]
    public async Task Availability_WithoutViewTeam_ListsOnlyTheCallersRows_AndRefusesAnotherUser()
    {
        await using var db = await SeededAsync();
        var controller = Controller(db, Alice, false);

        var own = Data<List<AnalystAvailabilityDto>>(await controller.ListAvailability(null, null, null));

        Assert.Equal(Alice, Assert.Single(own).UserId);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.ListAvailability(Bob, null, null));
    }

    [Fact]
    public async Task WithViewTeam_TheViewerSeesEveryAnalystInTheirSections()
    {
        await using var db = await SeededAsync();
        var controller = Controller(db, Sam, true);

        var workload = Data<AnalystWorkloadDto>(await controller.GetWorkload(WeekFrom, WeekTo, null, null, null, null));
        var leave = Data<List<AnalystAvailabilityDto>>(await controller.ListAvailability(null, null, null));

        Assert.False(workload.OnlyOwnRow);
        Assert.Contains(workload.Rows, r => r.UserId == Alice);
        Assert.Contains(workload.Rows, r => r.UserId == Bob);
        Assert.DoesNotContain(workload.Rows, r => r.UserId == Dana); // section 2 only
        Assert.Equal(2, leave.Count);
    }

    [Fact] // Review Focus 5
    public async Task Workload_ViewerWithoutViewTeamWhoIsNotAnAnalyst_GetsNoRows()
    {
        await using var db = await SeededAsync();

        // Sam is a Section Head holding Kpi.View but not Kpi.ViewTeam, with no assigned tests.
        var dto = Data<AnalystWorkloadDto>(await Controller(db, Sam, false).GetWorkload(WeekFrom, WeekTo, null, null, null, null));

        Assert.True(dto.OnlyOwnRow);
        Assert.Empty(dto.Rows);
    }

    [Fact]
    public void EveryGetAction_IsCoveredByTheTestsAbove()
    {
        // A new read endpoint must be added here AND get an own-row-only test, so D8 cannot silently regress.
        var gets = typeof(KpiWorkloadController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<HttpGetAttribute>() is not null)
            .Select(m => m.Name)
            .OrderBy(n => n, StringComparer.Ordinal);

        Assert.Equal(new[] { "GetSettings", "GetWorkload", "GetWorkloadTests", "ListAvailability" }, gets);
    }
}
```

- [ ] **Step 2: Run and see it fail**

Run: `dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~WorkloadOwnRowOnlyTests"`
Expected: build FAILS (`KpiWorkloadController` does not exist).

- [ ] **Step 3: Implement the controller**

Create `backend/MicroLIMS.API/Controllers/KpiWorkloadController.cs`:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Analyst workload, capacity settings and availability (KPI remodel S3). Everyone with Kpi.View may call the reads,
// but the services return only the caller's own figures unless the JWT carries Kpi.ViewTeam (D8). Writes also need
// Kpi.ConfigureProfiles; the two policies intersect.
[ApiController]
[Route("api/kpi")]
[Authorize(Policy = PermissionConstants.KpiView)]
public class KpiWorkloadController : ControllerBase
{
    private readonly AnalystWorkloadService _workload;
    private readonly KpiSettingsService _settings;
    private readonly AnalystAvailabilityService _availability;

    public KpiWorkloadController(AnalystWorkloadService workload, KpiSettingsService settings, AnalystAvailabilityService availability)
    {
        _workload = workload;
        _settings = settings;
        _availability = availability;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    private bool CanViewTeam => User.HasClaim("permission", PermissionConstants.KpiViewTeam);
    private CancellationToken Ct => HttpContext.RequestAborted;

    [HttpGet("workload")]
    public async Task<IActionResult> GetWorkload(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] string? category,
        [FromQuery] string? location, [FromQuery] string? testCode, [FromQuery] int? analystId)
    {
        var q = await _workload.BuildQueryAsync(CurrentUserId, CanViewTeam, from, to, category, location, testCode, analystId, Ct);
        return Ok(ApiResponse<object>.Ok(await _workload.GetAsync(q, Ct)));
    }

    [HttpGet("workload/tests")]
    public async Task<IActionResult> GetWorkloadTests(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] string? category,
        [FromQuery] string? location, [FromQuery] string? testCode, [FromQuery] int? analystId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var q = await _workload.BuildQueryAsync(CurrentUserId, CanViewTeam, from, to, category, location, testCode, analystId, Ct);
        return Ok(ApiResponse<object>.Ok(await _workload.GetTestsAsync(q, page, pageSize, Ct)));
    }

    [HttpGet("settings/{sectionId:int}")]
    public async Task<IActionResult> GetSettings(int sectionId) =>
        Ok(ApiResponse<object>.Ok(await _settings.GetAsync(sectionId, CurrentUserId, Ct)));

    [Authorize(Policy = PermissionConstants.KpiConfigureProfiles)]
    [HttpPut("settings/{sectionId:int}")]
    public async Task<IActionResult> UpdateSettings(int sectionId, [FromBody] UpdateKpiSettingsRequest request) =>
        Ok(ApiResponse<object>.Ok(await _settings.UpdateAsync(sectionId, request, CurrentUserId, Ct)));

    [HttpGet("availability")]
    public async Task<IActionResult> ListAvailability([FromQuery] int? userId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to) =>
        Ok(ApiResponse<object>.Ok(await _availability.ListAsync(CurrentUserId, CanViewTeam, userId, from, to, Ct)));

    [Authorize(Policy = PermissionConstants.KpiConfigureProfiles)]
    [HttpPost("availability")]
    public async Task<IActionResult> CreateAvailability([FromBody] SaveAvailabilityRequest request) =>
        Ok(ApiResponse<object>.Ok(await _availability.CreateAsync(request, CurrentUserId, Ct)));

    [Authorize(Policy = PermissionConstants.KpiConfigureProfiles)]
    [HttpPut("availability/{id:int}")]
    public async Task<IActionResult> UpdateAvailability(int id, [FromBody] SaveAvailabilityRequest request) =>
        Ok(ApiResponse<object>.Ok(await _availability.UpdateAsync(id, request, CurrentUserId, Ct)));
}
```

In `ServiceCollectionExtensions.cs`, use Grep for `AddScoped<KpiService>` and add after the last KPI registration that S1/S2 added (keep the `using MicroLIMS.Application.Services.Kpi;` that S1 added; add it if missing):

```csharp
        services.AddScoped<KpiSettingsService>();
        services.AddScoped<AnalystAvailabilityService>();
        services.AddScoped<AnalystWorkloadService>();
```
(The contract says "four services"; `CapacityCalculator` is static, so three registrations are correct.)

- [ ] **Step 4: Run the controller tests**

Run the Step 2 command. Expected: 8 passed, 0 failed.

- [ ] **Step 5: See the authorization matrix fail, regenerate, review**

Run: `dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~AuthorizationMatrixTests|FullyQualifiedName~PermissionAuthorizationTests"`
Expected: `EffectiveAccess_MatchesTheReviewedMatrix` FAILS listing seven `KpiWorkloadController.*` lines missing from the file.

Regenerate: `MICROLIMS_WRITE_AUTH_MATRIX=1 dotnet test backend/MicroLIMS.Tests --filter AuthorizationMatrixTests --artifacts-path "$TEMP/kpi-art"`

Run `git diff -- backend/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt`. The diff must add exactly these seven lines and change nothing else (a reordering or any other changed line means another feature's controllers leaked in: stop and use the shared-file protocol):

```
KpiWorkloadController.CreateAvailability(SaveAvailabilityRequest): SystemAdministrator,SectionHead
KpiWorkloadController.GetSettings(Int32): SystemAdministrator,SectionHead
KpiWorkloadController.GetWorkload(DateOnly, DateOnly, String, String, String, Nullable`1): SystemAdministrator,SectionHead
KpiWorkloadController.GetWorkloadTests(DateOnly, DateOnly, String, String, String, Nullable`1, Int32, Int32): SystemAdministrator,SectionHead
KpiWorkloadController.ListAvailability(Nullable`1, Nullable`1, Nullable`1): SystemAdministrator,SectionHead
KpiWorkloadController.UpdateAvailability(Int32, SaveAvailabilityRequest): SystemAdministrator,SectionHead
KpiWorkloadController.UpdateSettings(Int32, UpdateKpiSettingsRequest): SystemAdministrator,SectionHead
```

Re-run the Step 5 test command. Expected: all pass.

- [ ] **Step 6: Commit**

```
git add backend/MicroLIMS.API/Controllers/KpiWorkloadController.cs backend/MicroLIMS.Tests/UnitTests/Kpi/WorkloadOwnRowOnlyTests.cs
git add -p backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs backend/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt
git commit -m "feat(kpi): workload, settings and availability endpoints" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Postgres golden dataset and full backend verification

**Files:**
- Test: `backend/MicroLIMS.Tests/IntegrationTests/AnalystWorkloadGoldenPostgresTests.cs`

**Interfaces:**
- Consumes: `ScratchPostgresDatabase`, `WorkloadTestData`, `TestServiceFactory.AnalystWorkload`, the migrated schema from Task 2, `[PostgresFact]`.
- Produces: nothing new (verification).

- [ ] **Step 1: Write the golden test**

The expected numbers are hand-calculated, independent of the service code. Analyst Bob, week Mon 5 - Sun 11 Oct 2026, now Wed 14 Oct 12:00Z, no settings row (6 h, Mon-Fri, horizon 5), profiles CountTest 2 u / 5 d, Measurement 4 u / 10 d:

| Test | Code | Approved | Submitted | Notes |
|---|---|---|---|---|
| 1 | TAMC | 9 Oct 12:00 | 9 Oct 10:00 | returned, on time (deadline 10 Oct 09:00) |
| 2 | TAMC | 9 Oct 12:00 | 9 Oct 10:00 | on time |
| 3 | PH | 9 Oct 12:00 | 9 Oct 10:00 | on time (deadline 15 Oct) |
| 4 | TAMC | 11 Oct 12:00 | 11 Oct 10:00 | late |
| 5 | TAMC | 11 Oct 12:00 | 11 Oct 10:00 | late |
| 6 | NOPROF | 9 Oct 12:00 | - | unweighted, default target |
| 7 | TAMC | 9 Oct 12:00 | - | superseded, ignored |
| 500 | PH | open, InProgress | | 4 open units |

Delivered units 2+2+4+2+2 = 12; delivered tests 6; capacity 5 x 6 = 30; utilisation 12/30 = 0.4; weighted on time 8/12; weighted RFT 10/12; open units 4, forward capacity 30, forward load 4/30; one unweighted test; one test on the default target.

Create `backend/MicroLIMS.Tests/IntegrationTests/AnalystWorkloadGoldenPostgresTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Tests.Fixtures;
using Xunit;
using static MicroLIMS.Tests.UnitTests.Kpi.WorkloadTestData;

namespace MicroLIMS.Tests.IntegrationTests;

// Runs the S3 read model on real Postgres (translation of the delivered/open queries, DateOnly columns, check
// constraints) against a hand-calculated dataset. Own throw-away database, so no other test's rows interfere.
public class AnalystWorkloadGoldenPostgresTests
{
    [PostgresFact]
    public async Task Workload_MatchesTheHandCalculatedGoldenDataset()
    {
        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            await using var db = ScratchPostgresDatabase.NewContext(cs);
            await db.Database.MigrateAsync();
            // AddOrgSectionSegregation already inserted QC and MICRO into a migrated database (PostgresTestFixture says the same);
            // SeedBaselineAsync inserts its own ids and codes, so clear the migration-seeded org rows first.
            await db.Database.ExecuteSqlRawAsync("DELETE FROM \"DocumentSections\"; DELETE FROM \"DocumentDepartments\";");
            await SeedBaselineAsync(db);

            await DeliverAsync(db, 1, "TAMC", Bob, At(9, 12), returned: true);
            await DeliverAsync(db, 2, "TAMC", Bob, At(9, 12));
            await DeliverAsync(db, 3, "PH", Bob, At(9, 12));
            await DeliverAsync(db, 4, "TAMC", Bob, At(11, 12), submittedAt: At(11, 10));
            await DeliverAsync(db, 5, "TAMC", Bob, At(11, 12), submittedAt: At(11, 10));
            await DeliverAsync(db, 6, "NOPROF", Bob, At(9, 12));
            await DeliverAsync(db, 7, "TAMC", Bob, At(9, 12), superseded: true);
            await OpenAsync(db, 500, "PH", Bob);

            var service = TestServiceFactory.AnalystWorkload(db, Clock());
            var dto = await service.GetAsync(Week(analystId: Bob), default);

            var row = Assert.Single(dto.Rows);
            Assert.Equal(Bob, row.UserId);
            Assert.Equal(30m, row.CapacityUnits);
            Assert.Equal(12m, row.DeliveredUnits);
            Assert.Equal(6, row.DeliveredTests);
            Assert.Equal(0.4m, row.Utilisation);
            Assert.False(row.UtilisationCheckProfile);
            Assert.Equal(0.6667m, Math.Round(row.WeightedOnTime!.Value, 4));
            Assert.Equal(0.8333m, Math.Round(row.WeightedRightFirstTime!.Value, 4));
            Assert.Equal(4m, row.OpenUnits);
            Assert.Equal(30m, row.ForwardCapacityUnits);
            Assert.Equal(0.1333m, Math.Round(row.ForwardLoad!.Value, 4));
            Assert.False(row.ForwardLoadOverCapacity);
            Assert.Equal(1, row.UnweightedTests);
            Assert.Equal(1, row.TestsOnDefaultTarget);
            Assert.True(dto.CapacityUsesDefaults);
            Assert.Equal(1, dto.Gaps.Single(g => g.Code == "UNWEIGHTED_TESTS").Count);

            var page = await service.GetTestsAsync(Week(analystId: Bob), 1, 50, default);
            Assert.Equal(7, page.Total); // 6 delivered + 1 open; the superseded test is not listed
        });
    }
}
```

- [ ] **Step 2: Run it on Postgres**

Run: `MICROLIMS_TEST_POSTGRES=<value PostgresTestConfiguration.cs expects> dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~AnalystWorkloadGoldenPostgresTests|FullyQualifiedName~KpiSettingsMigrationPostgresTests"`
Expected: 2 passed (not skipped). A duplicate-key or unique-code error on `DocumentDepartments`/`DocumentSections` means the `DELETE` of migration-seeded org rows is missing or a foreign key now references them (then switch to find-or-create by `Code` and use the returned ids). If the seed fails with a NOT NULL or foreign-key error on `Samples`/`TestOrders`, add the missing parent row or column to `WorkloadTestData.SeedBaselineAsync`/`EnsureSampleAsync` (this affects both InMemory and Postgres runs equally); do not weaken the assertions.

- [ ] **Step 3: Full backend verification for the slice**

Run, in this order, and expect green for each:
1. `dotnet build backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --artifacts-path "$TEMP/kpi-art"` (0 errors).
2. `dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~Kpi|FullyQualifiedName~WorkflowProfile|FullyQualifiedName~Workload|FullyQualifiedName~UserDeletionTests|FullyQualifiedName~Authorization"`.
3. `MICROLIMS_TEST_POSTGRES=<value> dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art"` (full suite with Postgres: `FreshDatabaseSeedPostgresTests` and the S1/S2 migration tests must still pass with the new migration in the chain).
4. `dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art"` (full suite without Postgres).

- [ ] **Step 4: Commit**

```
git add backend/MicroLIMS.Tests/IntegrationTests/AnalystWorkloadGoldenPostgresTests.cs
git commit -m "test(kpi): Postgres golden dataset for analyst workload" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Frontend types and service

**Files:**
- Create: `frontend/src/modules/kpi/types/workloadTypes.ts`
- Create: `frontend/src/modules/kpi/services/kpiWorkloadService.ts`
- Test: `frontend/src/modules/kpi/services/kpiWorkloadService.test.ts`

**Interfaces:**
- Consumes: `apiClient`, `ifMatch(version)` from `frontend/src/services/apiClient.ts`; S2 `frontend/src/modules/kpi/types/kpiTypes.ts` exporting `KpiGap` (`{ code:string; message:string; count:number }`) and `KpiRecordPage` (the mirror of `KpiRecordPageDto`; if S2 named it differently, import that name instead).
- Produces: `AnalystWorkloadRow`, `AnalystWorkload`, `KpiSettings`, `UpdateKpiSettingsRequest`, `AnalystAvailability`, `SaveAvailabilityRequest`, `WorkloadParams`; `kpiWorkloadService` with `workload(params)`, `workloadTests(params)` (`page` and `pageSize` live in `WorkloadParams`, the same shape as S2's `records(key, params)`), `getSettings(sectionId)`, `updateSettings(sectionId, version, req)`, `listAvailability(params?)`, `createAvailability(req)`, `updateAvailability(id, req)`. Nothing is mounted on a page in this slice (S4 does that).

- [ ] **Step 1: Write the failing test**

Create `frontend/src/modules/kpi/services/kpiWorkloadService.test.ts`:

```ts
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup } from "@testing-library/react";
import { apiClient } from "../../../services/apiClient";
import { kpiWorkloadService } from "./kpiWorkloadService";

vi.mock("../../../services/apiClient", () => ({
  apiClient: { get: vi.fn(), post: vi.fn(), put: vi.fn() },
  ifMatch: (version: number) => ({ headers: { "If-Match": `"${version}"` } }),
}));

const ok = (data: unknown) => ({ data: { success: true, data } }) as never;

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("kpiWorkloadService", () => {
  it("omits analystId and blank filters, so the server applies the caller's own scope", async () => {
    vi.mocked(apiClient.get).mockResolvedValue(ok({ rows: [] }));

    await kpiWorkloadService.workload({ from: "2026-10-05", to: "2026-10-11", analystId: null, category: "", location: undefined });

    expect(apiClient.get).toHaveBeenCalledWith("/kpi/workload", { params: { from: "2026-10-05", to: "2026-10-11" } });
  });

  it("sends analystId only when set", async () => {
    vi.mocked(apiClient.get).mockResolvedValue(ok({ rows: [] }));

    await kpiWorkloadService.workload({ from: "2026-10-05", to: "2026-10-11", analystId: 7 });

    expect(apiClient.get).toHaveBeenCalledWith("/kpi/workload", { params: { from: "2026-10-05", to: "2026-10-11", analystId: 7 } });
  });

  it("pages the tests behind the figures", async () => {
    vi.mocked(apiClient.get).mockResolvedValue(ok({ items: [], total: 0 }));

    await kpiWorkloadService.workloadTests({ from: "2026-10-05", to: "2026-10-11", analystId: 7, page: 2, pageSize: 25 });

    expect(apiClient.get).toHaveBeenCalledWith("/kpi/workload/tests", {
      params: { from: "2026-10-05", to: "2026-10-11", analystId: 7, page: 2, pageSize: 25 },
    });
  });

  it("sends the settings version as If-Match and the exact request field names", async () => {
    vi.mocked(apiClient.put).mockResolvedValue(ok({ sectionId: 3 }));
    const req = { hoursPerAnalystDay: 8, workingWeekdays: 31, loadHorizonDays: 3, reason: "Site works 8 h days" };

    await kpiWorkloadService.updateSettings(3, 4, req);

    expect(apiClient.put).toHaveBeenCalledWith("/kpi/settings/3", req, { headers: { "If-Match": '"4"' } });
    expect(Object.keys(req).sort()).toEqual(["hoursPerAnalystDay", "loadHorizonDays", "reason", "workingWeekdays"]);
  });

  it("lists availability without undefined params, and writes with POST and PUT", async () => {
    vi.mocked(apiClient.get).mockResolvedValue(ok([]));
    vi.mocked(apiClient.post).mockResolvedValue(ok({ id: 1 }));
    vi.mocked(apiClient.put).mockResolvedValue(ok({ id: 1 }));
    const req = { userId: 7, fromDate: "2026-10-14", toDate: "2026-10-16", fractionAvailable: 0.5, reason: "Training course" };

    await kpiWorkloadService.listAvailability({ from: "2026-10-01", userId: undefined });
    await kpiWorkloadService.createAvailability(req);
    await kpiWorkloadService.updateAvailability(1, req);

    expect(apiClient.get).toHaveBeenCalledWith("/kpi/availability", { params: { from: "2026-10-01" } });
    expect(apiClient.post).toHaveBeenCalledWith("/kpi/availability", req);
    expect(apiClient.put).toHaveBeenCalledWith("/kpi/availability/1", req);
  });
});
```

- [ ] **Step 2: Run and see it fail**

Run (from `frontend`): `npx vitest run src/modules/kpi/services/kpiWorkloadService.test.ts`
Expected: FAIL, `Failed to resolve import "./kpiWorkloadService"`.

- [ ] **Step 3: Implement**

Create `frontend/src/modules/kpi/types/workloadTypes.ts`:

```ts
import type { KpiGap } from "./kpiTypes";

// Ratios (utilisation, forwardLoad, weightedOnTime, weightedRightFirstTime) are fractions: 1 = 100%.
// A rate is null when fewer than five tests stand behind it.
export interface AnalystWorkloadRow {
  userId: number;
  fullName: string;
  capacityUnits: number;
  deliveredUnits: number;
  utilisation: number | null;
  utilisationCheckProfile: boolean;
  openUnits: number;
  forwardCapacityUnits: number;
  forwardLoad: number | null;
  forwardLoadOverCapacity: boolean;
  weightedOnTime: number | null;
  weightedRightFirstTime: number | null;
  deliveredTests: number;
  unweightedTests: number;
  unweightedOpenTests: number;
  testsOnDefaultTarget: number;
}

export interface AnalystWorkload {
  from: string;
  to: string;
  capacityUsesDefaults: boolean;
  onlyOwnRow: boolean;
  unassignedOpenTests: number;
  rows: AnalystWorkloadRow[];
  gaps: KpiGap[];
}

export interface KpiSettings {
  sectionId: number;
  hoursPerAnalystDay: number;
  workingWeekdays: number;
  loadHorizonDays: number;
  isDefault: boolean;
  version: number;
}

export interface UpdateKpiSettingsRequest {
  hoursPerAnalystDay: number;
  workingWeekdays: number;
  loadHorizonDays: number;
  reason: string;
}

export interface AnalystAvailability {
  id: number;
  userId: number;
  userName: string;
  fromDate: string;
  toDate: string;
  fractionAvailable: number;
  reason: string;
  enteredByName: string;
  enteredAt: string;
}

export interface SaveAvailabilityRequest {
  userId: number;
  fromDate: string;
  toDate: string;
  fractionAvailable: number;
  reason: string;
}
```

Create `frontend/src/modules/kpi/services/kpiWorkloadService.ts`:

```ts
import { apiClient, ifMatch } from "../../../services/apiClient";
import type { KpiRecordPage } from "../types/kpiTypes";
import type {
  AnalystAvailability,
  AnalystWorkload,
  KpiSettings,
  SaveAvailabilityRequest,
  UpdateKpiSettingsRequest,
} from "../types/workloadTypes";

interface Envelope<T> {
  success: boolean;
  data: T;
}

export interface WorkloadParams {
  from: string;
  to: string;
  category?: string | null;
  location?: string | null;
  testCode?: string | null;
  analystId?: number | null;
  page?: number;
  pageSize?: number;
}

// Null, undefined and blank values are not sent. In particular analystId is sent only when set: without
// Kpi.ViewTeam the server answers with the caller's own row either way, and with it, no analystId means the team.
const clean = (params: object): Record<string, unknown> =>
  Object.fromEntries(Object.entries(params).filter(([, v]) => v !== null && v !== undefined && v !== ""));

export const kpiWorkloadService = {
  async workload(params: WorkloadParams): Promise<AnalystWorkload> {
    const res = await apiClient.get<Envelope<AnalystWorkload>>("/kpi/workload", { params: clean(params) });
    return res.data.data;
  },

  // page and pageSize are sent only when set; the server defaults to page 1, size 50.
  async workloadTests(params: WorkloadParams): Promise<KpiRecordPage> {
    const res = await apiClient.get<Envelope<KpiRecordPage>>("/kpi/workload/tests", { params: clean(params) });
    return res.data.data;
  },

  async getSettings(sectionId: number): Promise<KpiSettings> {
    const res = await apiClient.get<Envelope<KpiSettings>>(`/kpi/settings/${sectionId}`);
    return res.data.data;
  },

  async updateSettings(sectionId: number, version: number, req: UpdateKpiSettingsRequest): Promise<KpiSettings> {
    const res = await apiClient.put<Envelope<KpiSettings>>(`/kpi/settings/${sectionId}`, req, ifMatch(version));
    return res.data.data;
  },

  async listAvailability(params: { userId?: number | null; from?: string; to?: string } = {}): Promise<AnalystAvailability[]> {
    const res = await apiClient.get<Envelope<AnalystAvailability[]>>("/kpi/availability", { params: clean(params) });
    return res.data.data;
  },

  async createAvailability(req: SaveAvailabilityRequest): Promise<AnalystAvailability> {
    const res = await apiClient.post<Envelope<AnalystAvailability>>("/kpi/availability", req);
    return res.data.data;
  },

  async updateAvailability(id: number, req: SaveAvailabilityRequest): Promise<AnalystAvailability> {
    const res = await apiClient.put<Envelope<AnalystAvailability>>(`/kpi/availability/${id}`, req);
    return res.data.data;
  },
};
```

- [ ] **Step 4: Run, type-check, lint**

From `frontend`:
1. `npx vitest run src/modules/kpi` - expected: all pass (5 new tests plus any S1/S2 tests).
2. `npx tsc --noEmit -p tsconfig.json` - expected: no errors (if `KpiRecordPage` is not exported by S2's `kpiTypes.ts`, import S2's actual name for the records page).
3. `npm run lint` - expected: no new errors in the three new files.
4. `npm test` - expected: whole suite green.

- [ ] **Step 5: Commit**

```
git add frontend/src/modules/kpi/types/workloadTypes.ts frontend/src/modules/kpi/services/kpiWorkloadService.ts frontend/src/modules/kpi/services/kpiWorkloadService.test.ts
git commit -m "feat(kpi): frontend types and service for analyst workload" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Reviewer notes: coverage, contract deviations, assumptions

- **Spec coverage.** 4.2: delivered units, utilisation + "check profile", forward load, weighted on time, weighted RFT, unweighted exclusion + gap, no ranking (Tasks 5-7). 4.5: `KpiSettings`, `AnalystAvailability`, capacity maths, default-settings banner data (`CapacityUsesDefaults`) (Tasks 1-5). Section 6: `Kpi.ViewTeam` server-side own-row enforcement across `workload`, `workload/tests`, `availability` (Tasks 4, 5, 8); `Kpi.ConfigureProfiles` on all writes (Task 8); matrix regenerated (Task 8). D6/D8/D9 and `MinSampleForRate = 5` (Tasks 5-6, 8). The three permissions' seed/migration layers are S1/S2 work; S3 adds no permission.
- **Contract deviations to flag to the human:** (1) `BuildQueryAsync` is added to `AnalystWorkloadService` and `HorizonEnd`/`WeekdayBit` to `CapacityCalculator`; (2) three service registrations, not four (`CapacityCalculator` is static); (3) rates below five tests are `null` with a `RATES_TOO_FEW_RECORDS` gap, because the row DTO has no "too few" flag; (4) all ratios are fractions (1.0 = 100%) - S2 tiles must agree; (5) capacity is capped at today so a period ending in the future does not under-report utilisation; (6) the analyst-stage deadline uses S2's `AnalystStageTarget.DaysFor` (one rule shared with tile 2 and backlog aging); only the surrounding sibling lookup is local to `AnalystWorkloadService.LoadAsync`; (7) `FixedTimeProvider(DateTimeOffset)` is assumed from S1.
