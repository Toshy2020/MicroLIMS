using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class PlanWindowCalculatorTests
{
    private static readonly MonitoringSettings Lab = new(); // Sunday start, Sunday to Thursday
    private static readonly IReadOnlySet<DateOnly> NoHolidays = new HashSet<DateOnly>();

    private static List<PlannedPeriod> Run(FrequencyRuleDto rule, IReadOnlySet<DateOnly>? holidays = null, int early = 0, int late = 2) =>
        PlanWindowCalculator.Calculate(rule, 2026, Lab, early, late, holidays ?? NoHolidays);

    [Fact]
    public void LabWeeks_2026_StartOnSunday_WeekOneIsJan4_And52Weeks()
    {
        var weeks = PlanWindowCalculator.WeekStarts(2026, DayOfWeek.Sunday);

        Assert.Equal(new DateOnly(2026, 1, 4), weeks[0]);
        Assert.Equal(52, weeks.Count);
        Assert.Equal(new DateOnly(2026, 10, 25), weeks[42]);
        Assert.Equal("2026-W43", PlanWindowCalculator.WeekKey(2026, 43));
    }

    [Fact]
    public void WeeklyOnSunday_HasATargetEachWeek_WithTheLateTolerance()
    {
        var periods = Run(new FrequencyRuleDto(FrequencyType.Weekly, 1, DayOfWeek.Sunday));

        Assert.Equal(52, periods.Count);
        var first = periods[0];
        Assert.Equal(new DateOnly(2026, 1, 4), first.TargetDate);
        Assert.Equal(new DateOnly(2026, 1, 4), first.WindowStart);
        Assert.Equal(new DateOnly(2026, 1, 6), first.WindowEnd);
        Assert.False(first.MovedForNonWorkingDate);
    }

    [Fact]
    public void WeeklyOnWednesday_TargetsTheWednesdayOfTheLabWeek()
    {
        var first = Run(new FrequencyRuleDto(FrequencyType.Weekly, 1, DayOfWeek.Wednesday))[0];

        Assert.Equal(new DateOnly(2026, 1, 7), first.TargetDate);
    }

    [Fact]
    public void TargetOnANonWorkingWeekday_MovesToTheNextWorkingDay_AndTheWindowMoves()
    {
        // Friday is not a working day in a Sunday-to-Thursday week.
        var first = Run(new FrequencyRuleDto(FrequencyType.Weekly, 1, DayOfWeek.Friday))[0];

        Assert.Equal(new DateOnly(2026, 1, 9), first.OriginalTargetDate);
        Assert.Equal(new DateOnly(2026, 1, 11), first.TargetDate);
        Assert.Equal(new DateOnly(2026, 1, 13), first.WindowEnd);
        Assert.True(first.MovedForNonWorkingDate);
    }

    [Fact]
    public void TargetOnAHoliday_MovesToTheNextWorkingDay()
    {
        var holidays = new HashSet<DateOnly> { new(2026, 1, 4) };

        var first = Run(new FrequencyRuleDto(FrequencyType.Weekly, 1, DayOfWeek.Sunday), holidays)[0];

        Assert.Equal(new DateOnly(2026, 1, 5), first.TargetDate);
        Assert.Equal(new DateOnly(2026, 1, 4), first.OriginalTargetDate);
        Assert.Equal(new DateOnly(2026, 1, 7), first.WindowEnd);
    }

    [Fact]
    public void EarlyTolerance_OpensTheWindowBeforeTheTarget()
    {
        var first = Run(new FrequencyRuleDto(FrequencyType.Weekly, 1, DayOfWeek.Sunday), early: 1)[0];

        // One working day before Sunday 4 January is Thursday 1 January: Friday and Saturday are skipped.
        Assert.Equal(new DateOnly(2026, 1, 1), first.WindowStart);
    }

    [Fact]
    public void WeeklyWithoutAWeekday_IsAFlexibleLabWeekWindow_UnaffectedByHolidays()
    {
        var holidays = new HashSet<DateOnly> { new(2026, 1, 5) };

        var first = Run(new FrequencyRuleDto(FrequencyType.Weekly, 1), holidays)[0];

        Assert.Null(first.TargetDate);
        // The lab week runs Sunday to Saturday, but only Sunday to Thursday are working days.
        Assert.Equal(new DateOnly(2026, 1, 4), first.WindowStart);
        Assert.Equal(new DateOnly(2026, 1, 8), first.WindowEnd);
        Assert.Equal("2026-W01", first.PeriodKey);
    }

    [Fact]
    public void EveryTwoWeeks_CountsFromTheFirstWeekOfTheYear()
    {
        var periods = Run(new FrequencyRuleDto(FrequencyType.Weekly, 2));

        Assert.Equal(26, periods.Count);
        Assert.Equal("2026-W01", periods[0].PeriodKey);
        Assert.Equal("2026-W03", periods[1].PeriodKey);
    }

    [Fact]
    public void Monthly_IsTheWholeCalendarMonth()
    {
        var periods = Run(new FrequencyRuleDto(FrequencyType.Monthly, 1));

        Assert.Equal(12, periods.Count);
        Assert.Equal("2026-02", periods[1].PeriodKey);
        Assert.Equal(new DateOnly(2026, 2, 1), periods[1].WindowStart);
        // 28 February is a Saturday, so the window closes on Thursday the 26th.
        Assert.Equal(new DateOnly(2026, 2, 26), periods[1].WindowEnd);
        Assert.Null(periods[1].TargetDate);
    }

    [Fact]
    public void EveryThreeMonths_CountsFromJanuary_AndCoversTheWholeQuarter()
    {
        var periods = Run(new FrequencyRuleDto(FrequencyType.Monthly, 3));

        Assert.Equal(new[] { "2026-01", "2026-04", "2026-07", "2026-10" }, periods.Select(p => p.PeriodKey));
        Assert.Equal(new DateOnly(2026, 3, 31), periods[0].WindowEnd);
        Assert.Equal(new DateOnly(2026, 12, 31), periods[3].WindowEnd);
    }

    [Fact]
    public void EveryThreeMonths_WithAnAnchorMonth_StartsThere()
    {
        var periods = Run(new FrequencyRuleDto(FrequencyType.Monthly, 3, null, 2));

        Assert.Equal(new[] { "2026-02", "2026-05", "2026-08", "2026-11" }, periods.Select(p => p.PeriodKey));
    }

    [Fact]
    public void Generating_IsRepeatable_SoPeriodKeysNeverChange()
    {
        var rule = new FrequencyRuleDto(FrequencyType.Weekly, 1, DayOfWeek.Tuesday);

        Assert.Equal(Run(rule).Select(p => p.PeriodKey), Run(rule).Select(p => p.PeriodKey));
        Assert.Equal(Run(rule).Count, Run(rule).Select(p => p.PeriodKey).Distinct().Count());
    }

    [Fact]
    public void Generator_ReportsTargetsItCannotPlan_AndSkipsPeriodsBeforeTheEffectiveDate()
    {
        var items = new[] { new PlanItem(null, null, "WT_TAMC") };
        var targets = new[]
        {
            new PlanTarget(MonitoringCategory.Water, MonitoringTargetType.WaterSamplingPoint, 1, "SP-101",
                new FrequencyRuleDto(FrequencyType.Weekly, 1), "own", items),
            new PlanTarget(MonitoringCategory.Water, MonitoringTargetType.WaterSamplingPoint, 2, "SP-102", null, "none", items),
            new PlanTarget(MonitoringCategory.EnvironmentalMonitoring, MonitoringTargetType.Room, 3, "Dispensing",
                new FrequencyRuleDto(FrequencyType.Monthly, 1), "from department OSD II", Array.Empty<PlanItem>()),
            new PlanTarget(MonitoringCategory.EnvironmentalMonitoring, MonitoringTargetType.Room, 4, "Compression 2",
                new FrequencyRuleDto(FrequencyType.Monthly, 1), "from department OSD II", new[] { new PlanItem(10, null, null) })
        };

        var plan = PlanGenerator.Generate(targets, 2026, new DateOnly(2026, 7, 1), Lab, NoHolidays);

        Assert.Contains(plan.Exceptions, e => e.Kind == PlanExceptionKind.WeeklyWaterNoWeekday && e.Target == "SP-101");
        Assert.Contains(plan.Exceptions, e => e.Kind == PlanExceptionKind.NoFrequency && e.Target == "SP-102");
        Assert.Contains(plan.Exceptions, e => e.Kind == PlanExceptionKind.NoTests && e.Target == "Dispensing");
        var room = plan.Occurrences.Where(o => o.TargetId == 4).ToList();
        Assert.Equal(6, room.Count);
        Assert.Equal("2026-07", room[0].Period.PeriodKey);
        Assert.Equal("Monthly (from department OSD II)", room[0].FrequencySnapshot);
        Assert.DoesNotContain(plan.Occurrences, o => o.TargetId == 1);
    }

    [Fact]
    public void Daily_HasOneDayWindowPerWorkingDay_SkippingDaysOff()
    {
        var holiday = new DateOnly(2026, 1, 5); // a Monday
        var periods = Run(new FrequencyRuleDto(FrequencyType.Daily, 1), new HashSet<DateOnly> { holiday });

        // 2026: 261 Sunday-to-Thursday days, less the holiday.
        Assert.Equal(260, periods.Count);
        Assert.DoesNotContain(periods, p => p.TargetDate == holiday);
        Assert.DoesNotContain(periods, p => p.TargetDate!.Value.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday);
        Assert.All(periods, p => { Assert.Equal(p.TargetDate, p.WindowStart); Assert.Equal(p.TargetDate, p.WindowEnd); });
        Assert.Equal(periods.Count, periods.Select(p => p.PeriodKey).Distinct().Count());
        Assert.False(periods.Any(p => p.MovedForNonWorkingDate));
    }

    [Theory]
    [InlineData(2026, 1, 11, 15)]  // Jan 1 is a Thursday, so week 1 starts Sunday Jan 4; the week's Saturday is not a working day
    [InlineData(2026, 2, 8, 12)]   // Feb 1 is a Sunday
    public void MonthlyWithWeekOfMonth_IsDueInThatLabWeek_WithNoTargetDay(int year, int month, int startDay, int endDay)
    {
        var rule = new FrequencyRuleDto(FrequencyType.Monthly, 1, null, null, 2);

        var period = Run(rule).Single(p => p.PeriodKey == $"{year}-{month:00}");

        Assert.Null(period.TargetDate);
        Assert.Equal(new DateOnly(year, month, startDay), period.WindowStart);
        Assert.Equal(new DateOnly(year, month, endDay), period.WindowEnd);
    }

    [Fact]
    public void MonthlyWeekFour_IsCutAtMonthEnd()
    {
        var period = Run(new FrequencyRuleDto(FrequencyType.Monthly, 1, null, null, 4)).Single(p => p.PeriodKey == "2026-12");

        // Dec 2026: Dec 1 is a Tuesday, week 1 starts Dec 6, week 4 starts Dec 27 and would end Jan 2.
        Assert.Equal(new DateOnly(2026, 12, 27), period.WindowStart);
        Assert.Equal(new DateOnly(2026, 12, 31), period.WindowEnd);
    }

    [Fact]
    public void LateTolerance_CountsWorkingDays_SoAThursdayTargetClosesOnMonday()
    {
        var first = Run(new FrequencyRuleDto(FrequencyType.Weekly, 1, DayOfWeek.Thursday))[0];

        Assert.Equal(new DateOnly(2026, 1, 8), first.TargetDate);
        // Friday and Saturday are skipped: Thursday + 2 working days is Monday.
        Assert.Equal(new DateOnly(2026, 1, 12), first.WindowEnd);
    }

    [Fact]
    public void MonthlyWithWeekAndWeekday_IsDueOnThatDayOfThePinnedWeek()
    {
        // October 2026: week 4 starts Sunday 25 October, so Thursday is the 29th.
        var rule = new FrequencyRuleDto(FrequencyType.Monthly, 1, DayOfWeek.Thursday, null, 4);

        var period = Run(rule).Single(p => p.PeriodKey == "2026-10");

        Assert.Equal(new DateOnly(2026, 10, 29), period.TargetDate);
        Assert.Equal(new DateOnly(2026, 10, 29), period.WindowStart);
        Assert.Equal(new DateOnly(2026, 11, 2), period.WindowEnd);
        Assert.False(period.MovedForNonWorkingDate);
    }

    [Fact]
    public void MonthlyWithWeekAndAFridayWeekday_MovesToTheNextWorkingDay()
    {
        var rule = new FrequencyRuleDto(FrequencyType.Monthly, 1, DayOfWeek.Friday, null, 2);

        var period = Run(rule).Single(p => p.PeriodKey == "2026-03");

        // Week 2 of March 2026 starts Sunday 8 March; Friday the 13th is moved to Sunday the 15th.
        Assert.Equal(new DateOnly(2026, 3, 13), period.OriginalTargetDate);
        Assert.Equal(new DateOnly(2026, 3, 15), period.TargetDate);
        Assert.True(period.MovedForNonWorkingDate);
    }

    [Fact]
    public void MonthlyWithWeekAndAWeekdayAfterMonthEnd_LeavesTheWholePinnedWeekOpen()
    {
        // December 2026: week 4 starts Sunday 27 December and its Saturday would be 2 January.
        var rule = new FrequencyRuleDto(FrequencyType.Monthly, 1, DayOfWeek.Saturday, null, 4);

        var period = Run(rule).Single(p => p.PeriodKey == "2026-12");

        Assert.Null(period.TargetDate);
        Assert.Equal(new DateOnly(2026, 12, 27), period.WindowStart);
        Assert.Equal(new DateOnly(2026, 12, 31), period.WindowEnd);
    }

    [Fact]
    public void ANonWorkingDayAtTheEndOfAFlexibleWindow_ShortensIt()
    {
        var holidays = new HashSet<DateOnly> { new(2026, 1, 8) }; // the Thursday of week 1

        var first = Run(new FrequencyRuleDto(FrequencyType.Weekly, 1), holidays)[0];

        Assert.Equal(new DateOnly(2026, 1, 7), first.WindowEnd);
    }

    public static IEnumerable<object[]> EveryKindOfRule() => new[]
    {
        new object[] { new FrequencyRuleDto(FrequencyType.Weekly, 1) },
        new object[] { new FrequencyRuleDto(FrequencyType.Weekly, 1, DayOfWeek.Thursday) },
        new object[] { new FrequencyRuleDto(FrequencyType.Weekly, 2, DayOfWeek.Saturday) },
        new object[] { new FrequencyRuleDto(FrequencyType.Monthly, 1) },
        new object[] { new FrequencyRuleDto(FrequencyType.Monthly, 3, null, 2) },
        new object[] { new FrequencyRuleDto(FrequencyType.Monthly, 1, null, null, 3) },
        new object[] { new FrequencyRuleDto(FrequencyType.Monthly, 1, DayOfWeek.Thursday, null, 4) },
        new object[] { new FrequencyRuleDto(FrequencyType.Daily) }
    };

    [Theory]
    [MemberData(nameof(EveryKindOfRule))]
    public void NoWindowOrTargetIsEverPlannedOnAWeekend(FrequencyRuleDto rule)
    {
        var holidays = new HashSet<DateOnly> { new(2026, 4, 12), new(2026, 10, 6) };

        foreach (var p in Run(rule, holidays, early: 1, late: 2))
        {
            var days = new List<DateOnly> { p.WindowStart, p.WindowEnd };
            if (p.TargetDate.HasValue) days.Add(p.TargetDate.Value);
            foreach (var d in days)
            {
                Assert.True(d.DayOfWeek is not (DayOfWeek.Friday or DayOfWeek.Saturday), $"{p.PeriodKey}: {d} is a weekend day");
                Assert.DoesNotContain(d, holidays);
            }
            Assert.True(p.WindowStart <= p.WindowEnd);
        }
    }
}
