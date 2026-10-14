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
    public void WorkingDays_NonMonFriWeek_AcrossWeekBoundaries_SunThu_CountsTenDaysOverTwoWeeks()
    {
        // Sun-Thu week: Sun (bit 6 = 64), Mon (1), Tue (2), Wed (4), Thu (8) => mask = 79.
        // Range: Sun 4 Oct 2026 to Thu 15 Oct 2026 (12 calendar days: Sun-Thu (5) + Fri-Sat (0) + Sun-Thu (5) = 10 working days).
        var startSun = new DateOnly(2026, 10, 4);
        var endThu = new DateOnly(2026, 10, 15);
        const int sunThuMask = 79;
        Assert.Equal(10, CapacityCalculator.WorkingDays(startSun, endThu, sunThuMask));
    }

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
    public void CapacityUnits_OverlappingAvailabilityRows_FirstMatchPerDayWins()
    {
        // Plan rule: first match per day is the only match (rows never overlap in DB, but pure maths takes first).
        var overlaps = new[]
        {
            Leave(6, 7, 0.5m), // Tue 6 .. Wed 7 at 0.5
            Leave(6, 8, 0m)    // Tue 6 .. Thu 8 at 0.0 (overlaps Tue & Wed; covers Thu)
        };
        // Mon (6) + Tue (6*0.5=3) + Wed (6*0.5=3) + Thu (0) + Fri (6) = 18m
        Assert.Equal(18m, CapacityCalculator.CapacityUnits(Mon, Sun, 6.0m, MonFri, overlaps));
    }

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
