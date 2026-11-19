namespace MicroLIMS.Application.Services.Notifications;

public sealed record WorkingDay(DayOfWeek Day, bool IsWorkingDay, int StartMinute, int EndMinute);

/// <summary>
/// Pure working-time elapsed calculation in the laboratory's local time zone,
/// skipping closed hours, non-working weekdays, and calendar holidays/shutdowns.
/// </summary>
public static class WorkingTimeCalculator
{
    public const int MinutesPerDay = 1440;

    /// <summary>
    /// Returns true when at least one weekday is configured as a working day with a valid window.
    /// </summary>
    public static bool IsConfigured(IReadOnlyList<WorkingDay> week)
    {
        if (week is null || week.Count == 0)
            return false;

        foreach (var day in week)
        {
            if (day.IsWorkingDay && day.StartMinute >= 0 && day.EndMinute <= MinutesPerDay && day.StartMinute < day.EndMinute)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Working time elapsed between <paramref name="fromUtc"/> and <paramref name="toUtc"/>
    /// in <paramref name="zone"/>, honouring local clock times across DST transitions
    /// and excluding <paramref name="nonWorkingDates"/>.
    /// </summary>
    public static TimeSpan Elapsed(
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlyList<WorkingDay> week,
        IReadOnlySet<DateOnly> nonWorkingDates,
        TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var from = NormalizeUtc(fromUtc);
        var to = NormalizeUtc(toUtc);
        if (to <= from || !IsConfigured(week))
            return TimeSpan.Zero;

        var byDay = new WorkingDay?[7];
        foreach (var day in week)
        {
            var index = (int)day.Day;
            if (index is >= 0 and < 7)
                byDay[index] = day;
        }

        var fromLocal = TimeZoneInfo.ConvertTimeFromUtc(from, zone);
        var toLocal = TimeZoneInfo.ConvertTimeFromUtc(to, zone);
        var startDate = DateOnly.FromDateTime(fromLocal).AddDays(-1);
        var endDate = DateOnly.FromDateTime(toLocal).AddDays(1);

        var total = TimeSpan.Zero;
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (nonWorkingDates is not null && nonWorkingDates.Contains(date))
                continue;

            var schedule = byDay[(int)date.DayOfWeek];
            if (schedule is null || !schedule.IsWorkingDay)
                continue;

            if (schedule.StartMinute < 0 || schedule.EndMinute > MinutesPerDay || schedule.StartMinute >= schedule.EndMinute)
                continue;

            var windowStartUtc = LocalMinuteToUtc(date, schedule.StartMinute, zone);
            var windowEndUtc = LocalMinuteToUtc(date, schedule.EndMinute, zone);

            var overlapStart = from > windowStartUtc ? from : windowStartUtc;
            var overlapEnd = to < windowEndUtc ? to : windowEndUtc;
            if (overlapEnd > overlapStart)
                total += overlapEnd - overlapStart;
        }

        return total;
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private static DateTime LocalMinuteToUtc(DateOnly date, int minuteFromMidnight, TimeZoneInfo zone)
    {
        var local = minuteFromMidnight == MinutesPerDay
            ? date.AddDays(1).ToDateTime(TimeOnly.MinValue)
            : date.ToDateTime(TimeOnly.MinValue).AddMinutes(minuteFromMidnight);

        // Advance past spring-forward invalid gaps so ConvertTimeToUtc never throws.
        while (zone.IsInvalidTime(local))
            local = local.AddMinutes(1);

        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }
}
