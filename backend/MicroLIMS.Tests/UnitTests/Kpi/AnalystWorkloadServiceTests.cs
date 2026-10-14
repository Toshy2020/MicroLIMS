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
