using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Monitoring;

// A test an occurrence will require. Exactly one field is set.
public record PlanItem(int? RoomTestConfigurationId, int? MachinePartConfigurationId, string? TestCode);

// A target as the generator sees it: already loaded, with its effective rule resolved.
public record PlanTarget(
    MonitoringCategory Category,
    MonitoringTargetType Type,
    int Id,
    string Name,
    FrequencyRuleDto? Rule,
    string RuleSource,
    IReadOnlyList<PlanItem> Items);

public record GeneratedOccurrence(
    MonitoringCategory Category,
    MonitoringTargetType TargetType,
    int TargetId,
    string TargetName,
    PlannedPeriod Period,
    string FrequencySnapshot,
    IReadOnlyList<PlanItem> Items)
{
    public (MonitoringTargetType, int, string, int) Key => (TargetType, TargetId, Period.PeriodKey, 1);
}

public enum PlanExceptionKind
{
    NoFrequency,
    WeeklyWaterNoWeekday,
    InvalidFrequency,
    NoTests,
    TargetDateMoved
}

public record PlanException(PlanExceptionKind Kind, MonitoringCategory Category, string Target, string Message);

public record GeneratedPlan(List<GeneratedOccurrence> Occurrences, List<PlanException> Exceptions);

// Resolves each target's rule into occurrences and reports the targets it
// could not plan (spec 6.3). Pure: callers load the targets and settings.
public static class PlanGenerator
{
    public static GeneratedPlan Generate(
        IEnumerable<PlanTarget> targets, int year, DateOnly effectiveFrom,
        MonitoringSettings settings, IReadOnlySet<DateOnly> nonWorkingDates)
    {
        var occurrences = new List<GeneratedOccurrence>();
        var exceptions = new List<PlanException>();

        foreach (var target in targets)
        {
            if (target.Rule?.Type is null)
            {
                exceptions.Add(new(PlanExceptionKind.NoFrequency, target.Category, target.Name, "No frequency is set."));
                continue;
            }

            var problem = FrequencyRules.Validate(target.Rule, weekdayRequired: target.Category == MonitoringCategory.Water);
            if (problem is not null)
            {
                var kind = problem.Contains("weekday", StringComparison.OrdinalIgnoreCase)
                    ? PlanExceptionKind.WeeklyWaterNoWeekday
                    : PlanExceptionKind.InvalidFrequency;
                exceptions.Add(new(kind, target.Category, target.Name, problem));
                continue;
            }

            if (target.Items.Count == 0)
            {
                exceptions.Add(new(PlanExceptionKind.NoTests, target.Category, target.Name, "No tests are configured, so there is nothing to plan."));
                continue;
            }

            var (early, late) = Tolerances(settings, target.Category);
            var snapshot = $"{FrequencyRules.Describe(target.Rule)} ({target.RuleSource})";

            foreach (var period in PlanWindowCalculator.Calculate(target.Rule, year, settings, early, late, nonWorkingDates))
            {
                if (period.WindowEnd < effectiveFrom) continue;

                occurrences.Add(new GeneratedOccurrence(target.Category, target.Type, target.Id, target.Name, period, snapshot, target.Items));
                if (period.MovedForNonWorkingDate)
                    exceptions.Add(new(PlanExceptionKind.TargetDateMoved, target.Category, target.Name,
                        $"{period.OriginalTargetDate:yyyy-MM-dd} is not a working day; moved to {period.TargetDate:yyyy-MM-dd}."));
            }
        }

        return new GeneratedPlan(occurrences, exceptions);
    }

    public static (int Early, int Late) Tolerances(MonitoringSettings s, MonitoringCategory category) => category switch
    {
        MonitoringCategory.Water => (s.WaterEarlyToleranceDays, s.WaterLateToleranceDays),
        MonitoringCategory.EnvironmentalMonitoring => (s.EmEarlyToleranceDays, s.EmLateToleranceDays),
        _ => (s.AcEarlyToleranceDays, s.AcLateToleranceDays)
    };
}
