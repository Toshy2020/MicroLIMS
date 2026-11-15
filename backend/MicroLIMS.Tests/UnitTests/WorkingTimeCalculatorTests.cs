using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Notifications;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class WorkingTimeCalculatorTests
{
    private static readonly TimeZoneInfo CairoZone = LabClock.ResolveTimeZone("Africa/Cairo");
    private static readonly TimeZoneInfo LondonZone = LabClock.ResolveTimeZone("Europe/London");

    // Sun-Thu 08:00-16:00, Fri-Sat off
    private static readonly IReadOnlyList<WorkingDay> CairoWeek =
    [
        new(DayOfWeek.Sunday, true, 8 * 60, 16 * 60),
        new(DayOfWeek.Monday, true, 8 * 60, 16 * 60),
        new(DayOfWeek.Tuesday, true, 8 * 60, 16 * 60),
        new(DayOfWeek.Wednesday, true, 8 * 60, 16 * 60),
        new(DayOfWeek.Thursday, true, 8 * 60, 16 * 60),
        new(DayOfWeek.Friday, false, 0, 0),
        new(DayOfWeek.Saturday, false, 0, 0),
    ];

    private static readonly IReadOnlySet<DateOnly> NoHolidays = new HashSet<DateOnly>();

    private static DateTime LocalToUtc(TimeZoneInfo zone, int year, int month, int day, int hour, int minute) =>
        TimeZoneInfo.ConvertTimeToUtc(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified), zone);

    [Fact]
    public void SameDay_WithinHours()
    {
        var from = LocalToUtc(CairoZone, 2026, 11, 8, 9, 30);  // Sunday 09:30
        var to = LocalToUtc(CairoZone, 2026, 11, 8, 12, 45);   // Sunday 12:45

        var elapsed = WorkingTimeCalculator.Elapsed(from, to, CairoWeek, NoHolidays, CairoZone);

        Assert.Equal(TimeSpan.FromMinutes(3 * 60 + 15), elapsed);
    }

    [Fact]
    public void StartsBeforeOpening_CountsFromOpening()
    {
        var from = LocalToUtc(CairoZone, 2026, 11, 8, 6, 0);   // Sunday 06:00 (before 08:00)
        var to = LocalToUtc(CairoZone, 2026, 11, 8, 10, 30);   // Sunday 10:30

        var elapsed = WorkingTimeCalculator.Elapsed(from, to, CairoWeek, NoHolidays, CairoZone);

        Assert.Equal(TimeSpan.FromMinutes(2 * 60 + 30), elapsed);
    }

    [Fact]
    public void EndsAfterClosing_CountsToClosing()
    {
        var from = LocalToUtc(CairoZone, 2026, 11, 8, 14, 0);  // Sunday 14:00
        var to = LocalToUtc(CairoZone, 2026, 11, 8, 19, 0);    // Sunday 19:00 (after 16:00)

        var elapsed = WorkingTimeCalculator.Elapsed(from, to, CairoWeek, NoHolidays, CairoZone);

        Assert.Equal(TimeSpan.FromHours(2), elapsed);
    }

    [Fact]
    public void AcrossNight_SkipsClosedHours()
    {
        var from = LocalToUtc(CairoZone, 2026, 11, 8, 15, 0);  // Sunday 15:00
        var to = LocalToUtc(CairoZone, 2026, 11, 9, 9, 30);    // Monday 09:30

        var elapsed = WorkingTimeCalculator.Elapsed(from, to, CairoWeek, NoHolidays, CairoZone);

        Assert.Equal(TimeSpan.FromMinutes(2 * 60 + 30), elapsed); // 1h Sun + 1h30m Mon
    }

    [Fact]
    public void AcrossWeekend_SkipsFridayAndSaturday()
    {
        var from = LocalToUtc(CairoZone, 2026, 11, 12, 12, 0); // Thursday 12:00
        var to = LocalToUtc(CairoZone, 2026, 11, 15, 10, 0);   // Sunday 10:00

        var elapsed = WorkingTimeCalculator.Elapsed(from, to, CairoWeek, NoHolidays, CairoZone);

        Assert.Equal(TimeSpan.FromHours(6), elapsed); // 4h Thu + 2h Sun
    }

    [Fact]
    public void AcrossNonWorkingDate_SkipsIt()
    {
        var holidays = new HashSet<DateOnly> { new(2026, 11, 15) }; // Sunday holiday
        var from = LocalToUtc(CairoZone, 2026, 11, 12, 15, 0);      // Thursday 15:00
        var to = LocalToUtc(CairoZone, 2026, 11, 16, 15, 0);        // Monday 15:00

        var elapsed = WorkingTimeCalculator.Elapsed(from, to, CairoWeek, holidays, CairoZone);

        Assert.Equal(TimeSpan.FromHours(8), elapsed); // 1h Thu + 7h Mon (Fri, Sat, Sun skipped)
    }

    [Fact]
    public void EightHoursFromThursday1500_IsReachedSunday1500()
    {
        var from = LocalToUtc(CairoZone, 2026, 11, 12, 15, 0); // Thursday 15:00
        var to = LocalToUtc(CairoZone, 2026, 11, 15, 15, 0);   // Sunday 15:00

        var elapsed = WorkingTimeCalculator.Elapsed(from, to, CairoWeek, NoHolidays, CairoZone);

        Assert.Equal(TimeSpan.FromHours(8), elapsed); // 1h Thu + 7h Sun
    }

    [Fact]
    public void AcrossDaylightSavingChange_UsesLocalClockTimes()
    {
        // Europe/London 2026-03-29 springs forward at 01:00 UTC (GMT +0 -> BST +1).
        // Saturday 2026-03-28 15:00 GMT is 15:00Z; Sunday 2026-03-29 15:00 BST is 14:00Z.
        IReadOnlyList<WorkingDay> week =
        [
            new(DayOfWeek.Sunday, true, 8 * 60, 16 * 60),
            new(DayOfWeek.Monday, true, 8 * 60, 16 * 60),
            new(DayOfWeek.Tuesday, false, 0, 0),
            new(DayOfWeek.Wednesday, false, 0, 0),
            new(DayOfWeek.Thursday, false, 0, 0),
            new(DayOfWeek.Friday, false, 0, 0),
            new(DayOfWeek.Saturday, true, 8 * 60, 16 * 60),
        ];

        var springFrom = new DateTime(2026, 3, 28, 15, 0, 0, DateTimeKind.Utc); // 15:00 GMT
        var springTo = new DateTime(2026, 3, 29, 14, 0, 0, DateTimeKind.Utc);   // 15:00 BST

        var springElapsed = WorkingTimeCalculator.Elapsed(springFrom, springTo, week, NoHolidays, LondonZone);
        Assert.Equal(TimeSpan.FromHours(8), springElapsed); // 1h Sat + 7h Sun in local clock time

        // Europe/London 2026-10-25 falls back at 01:00 UTC (BST +1 -> GMT +0).
        // Saturday 2026-10-24 15:00 BST is 14:00Z; Sunday 2026-10-25 15:00 GMT is 15:00Z.
        var fallFrom = new DateTime(2026, 10, 24, 14, 0, 0, DateTimeKind.Utc); // 15:00 BST
        var fallTo = new DateTime(2026, 10, 25, 15, 0, 0, DateTimeKind.Utc);   // 15:00 GMT

        var fallElapsed = WorkingTimeCalculator.Elapsed(fallFrom, fallTo, week, NoHolidays, LondonZone);
        Assert.Equal(TimeSpan.FromHours(8), fallElapsed); // 1h Sat + 7h Sun in local clock time
    }

    [Fact]
    public void ToBeforeFrom_Zero()
    {
        var from = LocalToUtc(CairoZone, 2026, 11, 8, 12, 0);
        var to = LocalToUtc(CairoZone, 2026, 11, 8, 10, 0);

        Assert.Equal(TimeSpan.Zero, WorkingTimeCalculator.Elapsed(from, to, CairoWeek, NoHolidays, CairoZone));
        Assert.Equal(TimeSpan.Zero, WorkingTimeCalculator.Elapsed(from, from, CairoWeek, NoHolidays, CairoZone));
    }

    [Fact]
    public void NoWorkingDays_IsConfiguredFalse_ElapsedZero()
    {
        IReadOnlyList<WorkingDay> none = Enum.GetValues<DayOfWeek>()
            .Select(d => new WorkingDay(d, false, 0, 0))
            .ToList();

        Assert.False(WorkingTimeCalculator.IsConfigured(none));
        Assert.False(WorkingTimeCalculator.IsConfigured(Array.Empty<WorkingDay>()));
        Assert.True(WorkingTimeCalculator.IsConfigured(CairoWeek));

        var from = LocalToUtc(CairoZone, 2026, 11, 8, 8, 0);
        var to = LocalToUtc(CairoZone, 2026, 11, 8, 16, 0);
        Assert.Equal(TimeSpan.Zero, WorkingTimeCalculator.Elapsed(from, to, none, NoHolidays, CairoZone));
    }
}
