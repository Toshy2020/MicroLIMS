using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Monitoring;

// What a person sees for an occurrence (spec 10). Derived at read time from
// the window, the exception state and the tests linked to it; never stored.
public enum OccurrenceStatus
{
    Upcoming,
    Open,
    DueToday,
    PartlyPerformed,
    InTesting,
    AwaitingReview,
    AwaitingApproval,
    Performed,
    PerformedExcursion,
    Overdue,
    Missed,
    Skipped,
    Retired
}

// How far one expected item has got. Ordered: a higher value is further on.
public enum ItemProgress
{
    None,
    InTesting,
    AwaitingReview,
    AwaitingApproval,
    Performed,
    PerformedExcursion
}

public record OccurrenceStatusInput(
    DateOnly WindowStart,
    DateOnly WindowEnd,
    DateOnly? TargetDate,
    OccurrenceExceptionState ExceptionState,
    int GraceDays,
    DateOnly Today,
    IReadOnlyList<ItemProgress> Items,
    // Lab date of the latest sample that fulfilled an item, if any.
    DateOnly? LatestFulfilledOn);

public record OccurrenceStatusResult(
    OccurrenceStatus Status, bool Late, int ItemsTotal, int ItemsLinked, int ItemsPerformed, DateOnly? FulfilledOn = null);

// Pure rules for the status table; no database, no clock.
public static class OccurrenceStatusCalculator
{
    public static OccurrenceStatusResult Calculate(OccurrenceStatusInput i)
    {
        var total = i.Items.Count;
        var linked = i.Items.Count(p => p != ItemProgress.None);
        var performed = i.Items.Count(p => p >= ItemProgress.Performed);
        var late = linked > 0 && i.LatestFulfilledOn is DateOnly on && on > i.WindowEnd;

        OccurrenceStatusResult Result(OccurrenceStatus s) => new(s, late, total, linked, performed, linked > 0 ? i.LatestFulfilledOn : null);

        if (i.ExceptionState == OccurrenceExceptionState.Retired) return Result(OccurrenceStatus.Retired);
        if (i.ExceptionState == OccurrenceExceptionState.Skipped) return Result(OccurrenceStatus.Skipped);

        if (total > 0 && linked == total)
        {
            if (performed == total)
                return Result(i.Items.Any(p => p == ItemProgress.PerformedExcursion)
                    ? OccurrenceStatus.PerformedExcursion
                    : OccurrenceStatus.Performed);

            // Everything is under way: report the slowest item.
            return Result(i.Items.Where(p => p < ItemProgress.Performed).Min() switch
            {
                ItemProgress.InTesting => OccurrenceStatus.InTesting,
                ItemProgress.AwaitingReview => OccurrenceStatus.AwaitingReview,
                _ => OccurrenceStatus.AwaitingApproval
            });
        }

        var byDate = ByDate(i);
        if (linked > 0 && byDate is OccurrenceStatus.Upcoming or OccurrenceStatus.Open or OccurrenceStatus.DueToday)
            return Result(OccurrenceStatus.PartlyPerformed);
        return Result(byDate);
    }

    private static OccurrenceStatus ByDate(OccurrenceStatusInput i)
    {
        if (i.Today < i.WindowStart) return OccurrenceStatus.Upcoming;
        if (i.Today <= i.WindowEnd)
            return i.TargetDate == i.Today ? OccurrenceStatus.DueToday : OccurrenceStatus.Open;
        return i.Today <= i.WindowEnd.AddDays(i.GraceDays) ? OccurrenceStatus.Overdue : OccurrenceStatus.Missed;
    }
}

// The facts about a location that decide which expected item it fulfils.
public record LocationFacts(int? RoomTestConfigurationId, int? MachinePartConfigurationId, int? WaterSamplingPointId, string TestCode);

public static class OccurrenceItemMatcher
{
    public static bool Matches(MonitoringOccurrence occurrence, MonitoringOccurrenceItem item, LocationFacts location) =>
        occurrence.TargetType switch
        {
            MonitoringTargetType.Room =>
                item.RoomTestConfigurationId.HasValue && item.RoomTestConfigurationId == location.RoomTestConfigurationId,
            // An item with no part is "the machine itself": any part sampled at the session fulfils it.
            MonitoringTargetType.Machine =>
                item.MachinePartConfigurationId.HasValue
                    ? item.MachinePartConfigurationId == location.MachinePartConfigurationId
                    : location.MachinePartConfigurationId.HasValue,
            _ =>
                location.WaterSamplingPointId.HasValue && location.WaterSamplingPointId == occurrence.WaterSamplingPointId
                && item.TestCode != null && item.TestCode == location.TestCode
        };
}

public static class OccurrenceProgressRules
{
    // A voided or cancelled test fulfils nothing. A rejected, retested or
    // out-of-limit result still counts as performed: the sampling was done
    // and its excursion is handled in the result workflow (spec 10, N2).
    public static ItemProgress For(ApprovalStatus status, WorkflowStep step, bool superseded, ResultStatus? result)
    {
        if (status is ApprovalStatus.Voided or ApprovalStatus.Cancelled) return ItemProgress.None;
        if (superseded || status is ApprovalStatus.Rejected or ApprovalStatus.RetestRequested)
            return ItemProgress.PerformedExcursion;
        if (status == ApprovalStatus.Approved)
            return result is ResultStatus.AlertLimitExceeded or ResultStatus.ActionLimitExceeded
                or ResultStatus.OutOfSpecification or ResultStatus.Detected
                ? ItemProgress.PerformedExcursion
                : ItemProgress.Performed;
        return step switch
        {
            WorkflowStep.Ready => ItemProgress.AwaitingReview,
            WorkflowStep.Reviewed or WorkflowStep.Approved => ItemProgress.AwaitingApproval,
            _ => ItemProgress.InTesting
        };
    }
}
