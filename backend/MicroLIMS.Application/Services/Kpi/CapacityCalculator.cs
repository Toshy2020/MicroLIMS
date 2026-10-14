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
