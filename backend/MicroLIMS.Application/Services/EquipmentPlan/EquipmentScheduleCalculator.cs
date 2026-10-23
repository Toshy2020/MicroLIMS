using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.EquipmentPlan;

// Pure schedule calculator for equipment calibration and preventive maintenance.
// Reuses PlanWindowCalculator working-day helpers.
public static class EquipmentScheduleCalculator
{
    public static DateOnly AddInterval(DateOnly from, int value, IntervalUnit unit) =>
        unit switch
        {
            IntervalUnit.Weeks => from.AddDays(7 * value),
            IntervalUnit.Months => from.AddMonths(value),
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown interval unit.")
        };

    // Raw due = certificateNextDue ?? AddInterval(lastActual, ...); then working-day move.
    public static DateOnly DueAfter(
        DateOnly lastActual,
        int value,
        IntervalUnit unit,
        DateOnly? certificateNextDue,
        DateOnly today,
        int workingWeekdays,
        IReadOnlySet<DateOnly> nonWorking)
    {
        var raw = certificateNextDue ?? AddInterval(lastActual, value, unit);
        return AdjustWorkingDay(raw, lastActual, today, workingWeekdays, nonWorking);
    }

    // Every due date inside [from, yearEnd] chained from lastActual; first may use certificateNextDue.
    public static List<DateOnly> ProjectYear(
        DateOnly lastActual,
        int value,
        IntervalUnit unit,
        DateOnly? certificateNextDue,
        int year,
        DateOnly from,
        DateOnly today,
        int workingWeekdays,
        IReadOnlySet<DateOnly> nonWorking)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Interval value must be positive.");

        var yearEnd = new DateOnly(year, 12, 31);
        var result = new List<DateOnly>();

        var raw = certificateNextDue ?? AddInterval(lastActual, value, unit);

        while (raw <= yearEnd)
        {
            var dueDate = AdjustWorkingDay(raw, lastActual, today, workingWeekdays, nonWorking);
            if (dueDate >= from && dueDate <= yearEnd)
            {
                result.Add(dueDate);
            }

            raw = AddInterval(raw, value, unit);
        }

        return result;
    }

    public static string Describe(int value, IntervalUnit unit) =>
        unit switch
        {
            IntervalUnit.Weeks => value == 1 ? "Weekly" : $"Every {value} weeks",
            IntervalUnit.Months => value == 1 ? "Monthly" : $"Every {value} months",
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown interval unit.")
        };

    // Moves non-working day to previous working day unless that is before today or last actual date.
    private static DateOnly AdjustWorkingDay(
        DateOnly raw,
        DateOnly lastActual,
        DateOnly today,
        int workingWeekdays,
        IReadOnlySet<DateOnly> nonWorking)
    {
        if (PlanWindowCalculator.IsWorkingDay(raw, workingWeekdays, nonWorking))
            return raw;

        var prev = PlanWindowCalculator.PreviousWorkingDay(raw, workingWeekdays, nonWorking);
        if (prev < today || prev <= lastActual)
            return PlanWindowCalculator.NextWorkingDay(raw, workingWeekdays, nonWorking);

        return prev;
    }
}
