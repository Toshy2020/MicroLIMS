using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Monitoring;

// One period of a frequency rule: when it is due. TargetDate is set for
// weekly rules with a sampling weekday and for each day of a daily rule.
public record PlannedPeriod(
    string PeriodKey,
    DateOnly? TargetDate,
    DateOnly? OriginalTargetDate,
    DateOnly WindowStart,
    DateOnly WindowEnd,
    bool MovedForNonWorkingDate);

// Turns a frequency rule into the periods of one plan year (spec 6.1, 6.2).
// Pure: no database, no clock. Weeks are lab weeks starting on
// MonitoringSettings.LabWeekStartDay; a week belongs to the year that holds
// its fourth day, so week 1 is the first lab week mostly inside the year.
// A monthly rule with a week of the month is due in that lab week (the Nth
// week starting in the month); with a sampling weekday it is due on that day of
// the week, otherwise on no fixed day.
// Nothing is ever planned on a non-working day: windows without a fixed day are
// trimmed to the first and last working day inside them, and tolerances around
// a target count working days, not calendar days.
public static class PlanWindowCalculator
{
    public static List<PlannedPeriod> Calculate(
        FrequencyRuleDto rule, int year, MonitoringSettings settings,
        int earlyToleranceDays, int lateToleranceDays, IReadOnlySet<DateOnly> nonWorkingDates)
    {
        var interval = Math.Max(1, rule.Interval ?? 1);
        return rule.Type switch
        {
            FrequencyType.Weekly => Weekly(rule, interval, year, settings, earlyToleranceDays, lateToleranceDays, nonWorkingDates),
            FrequencyType.Monthly => Monthly(rule, interval, year, settings, earlyToleranceDays, lateToleranceDays, nonWorkingDates),
            FrequencyType.Daily => Daily(year, settings, nonWorkingDates),
            _ => new List<PlannedPeriod>()
        };
    }

    public static DateOnly WeekStart(DateOnly date, DayOfWeek weekStartDay) =>
        date.AddDays(-(((int)date.DayOfWeek - (int)weekStartDay + 7) % 7));

    // The lab weeks of the year, in order. Week n is element n - 1.
    public static List<DateOnly> WeekStarts(int year, DayOfWeek weekStartDay)
    {
        var week = WeekStart(new DateOnly(year, 1, 1), weekStartDay);
        if (week.AddDays(3).Year != year) week = week.AddDays(7);

        var weeks = new List<DateOnly>();
        while (week.AddDays(3).Year == year)
        {
            weeks.Add(week);
            week = week.AddDays(7);
        }
        return weeks;
    }

    public static string WeekKey(int year, int weekNumber) => $"{year}-W{weekNumber:00}";

    // bit0 = Monday ... bit6 = Sunday.
    public static bool IsWorkingDay(DateOnly date, int workingWeekdays, IReadOnlySet<DateOnly> nonWorkingDates)
    {
        var bit = ((int)date.DayOfWeek + 6) % 7;
        return ((workingWeekdays >> bit) & 1) == 1 && !nonWorkingDates.Contains(date);
    }

    public static DateOnly NextWorkingDay(DateOnly date, int workingWeekdays, IReadOnlySet<DateOnly> nonWorkingDates)
    {
        var d = date;
        // Bounded so a calendar with no working day cannot loop forever.
        for (var i = 0; i < 400 && !IsWorkingDay(d, workingWeekdays, nonWorkingDates); i++)
            d = d.AddDays(1);
        return d;
    }

    // The working day on or before the date.
    public static DateOnly PreviousWorkingDay(DateOnly date, int workingWeekdays, IReadOnlySet<DateOnly> nonWorkingDates)
    {
        var d = date;
        for (var i = 0; i < 400 && !IsWorkingDay(d, workingWeekdays, nonWorkingDates); i++)
            d = d.AddDays(-1);
        return d;
    }

    // Moves by whole working days; a negative count moves back.
    public static DateOnly AddWorkingDays(DateOnly date, int days, int workingWeekdays, IReadOnlySet<DateOnly> nonWorkingDates)
    {
        var step = days < 0 ? -1 : 1;
        var left = Math.Abs(days);
        var d = date;
        for (var i = 0; i < 4000 && left > 0; i++)
        {
            d = d.AddDays(step);
            if (IsWorkingDay(d, workingWeekdays, nonWorkingDates)) left--;
        }
        return d;
    }

    // A window with no fixed day, cut back to the working days inside it.
    // If it holds no working day at all it collapses to the next working day.
    public static (DateOnly Start, DateOnly End) TrimToWorkingDays(
        DateOnly start, DateOnly end, int workingWeekdays, IReadOnlySet<DateOnly> nonWorkingDates)
    {
        var s = NextWorkingDay(start, workingWeekdays, nonWorkingDates);
        var e = PreviousWorkingDay(end, workingWeekdays, nonWorkingDates);
        return s <= e ? (s, e) : (s, s);
    }

    private static List<PlannedPeriod> Weekly(
        FrequencyRuleDto rule, int interval, int year, MonitoringSettings s,
        int early, int late, IReadOnlySet<DateOnly> nonWorking)
    {
        var result = new List<PlannedPeriod>();
        var weeks = WeekStarts(year, s.LabWeekStartDay);

        for (var i = 0; i < weeks.Count; i += interval)
        {
            var start = weeks[i];
            var key = WeekKey(year, i + 1);

            if (rule.SamplingWeekday is not DayOfWeek weekday)
            {
                var (from, to) = TrimToWorkingDays(start, start.AddDays(6), s.WorkingWeekdays, nonWorking);
                result.Add(new PlannedPeriod(key, null, null, from, to, false));
                continue;
            }

            var original = start.AddDays(((int)weekday - (int)s.LabWeekStartDay + 7) % 7);
            var target = NextWorkingDay(original, s.WorkingWeekdays, nonWorking);
            result.Add(new PlannedPeriod(key, target, original,
                AddWorkingDays(target, -early, s.WorkingWeekdays, nonWorking),
                AddWorkingDays(target, late, s.WorkingWeekdays, nonWorking), target != original));
        }
        return result;
    }

    // The Nth lab week that starts inside the month (n = 1..4).
    public static DateOnly WeekOfMonthStart(int year, int month, int weekOfMonth, DayOfWeek weekStartDay)
    {
        var first = new DateOnly(year, month, 1);
        var week = WeekStart(first, weekStartDay);
        if (week < first) week = week.AddDays(7);
        return week.AddDays(7 * (weekOfMonth - 1));
    }

    // One period per working day; days off are skipped, never moved.
    private static List<PlannedPeriod> Daily(int year, MonitoringSettings s, IReadOnlySet<DateOnly> nonWorking)
    {
        var result = new List<PlannedPeriod>();
        for (var d = new DateOnly(year, 1, 1); d.Year == year; d = d.AddDays(1))
        {
            if (!IsWorkingDay(d, s.WorkingWeekdays, nonWorking)) continue;
            result.Add(new PlannedPeriod($"D{d:yyyyMMdd}", d, d, d, d, false));
        }
        return result;
    }

    private static List<PlannedPeriod> Monthly(
        FrequencyRuleDto rule, int interval, int year, MonitoringSettings s,
        int early, int late, IReadOnlySet<DateOnly> nonWorking)
    {
        var result = new List<PlannedPeriod>();
        var anchor = interval > 1 ? (rule.AnchorMonth ?? 1) : 1;

        for (var month = 1; month <= 12; month++)
        {
            if (((month - anchor) % interval + interval) % interval != 0) continue;

            var key = $"{year}-{month:00}";
            var lastMonth = Math.Min(month + interval - 1, 12);
            var start = new DateOnly(year, month, 1);
            var end = new DateOnly(year, lastMonth, DateTime.DaysInMonth(year, lastMonth));
            if (rule.WeekOfMonth is int week)
            {
                // Pinned to one lab week of the first month; a week that runs past month end is cut there.
                var monthEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
                start = WeekOfMonthStart(year, month, week, s.LabWeekStartDay);
                end = new[] { start.AddDays(6), monthEnd }.Min();

                // With a sampling weekday it is due on that day of the pinned week, like a weekly rule.
                if (rule.SamplingWeekday is DayOfWeek weekday)
                {
                    var original = start.AddDays(((int)weekday - (int)s.LabWeekStartDay + 7) % 7);
                    var target = NextWorkingDay(original, s.WorkingWeekdays, nonWorking);
                    // A weekday that falls after the month ends leaves the whole pinned week open.
                    if (target <= monthEnd)
                    {
                        result.Add(new PlannedPeriod(key, target, original,
                            AddWorkingDays(target, -early, s.WorkingWeekdays, nonWorking),
                            AddWorkingDays(target, late, s.WorkingWeekdays, nonWorking), target != original));
                        continue;
                    }
                }
            }

            var (from, to) = TrimToWorkingDays(start, end, s.WorkingWeekdays, nonWorking);
            result.Add(new PlannedPeriod(key, null, null, from, to, false));
        }
        return result;
    }
}
