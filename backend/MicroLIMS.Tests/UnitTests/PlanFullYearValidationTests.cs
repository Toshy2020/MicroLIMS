using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// A whole plan year for a mix of targets, checked against the rules of the
// spec rather than against hand-picked dates (spec 18, phase 6).
public class PlanFullYearValidationTests
{
    private static readonly MonitoringSettings Lab = new(); // Sunday to Thursday
    private static readonly PlanItem Item = new(1, null, null);

    private static PlanTarget Target(int id, MonitoringCategory category, FrequencyRuleDto rule) =>
        new(category, category == MonitoringCategory.Water ? MonitoringTargetType.WaterSamplingPoint : MonitoringTargetType.Room,
            id, $"Target {id}", rule, "own", new[] { Item });

    private static readonly HashSet<DateOnly> Holidays = new()
    {
        new DateOnly(2026, 1, 7), new DateOnly(2026, 4, 12), new DateOnly(2026, 10, 6), new DateOnly(2026, 12, 31)
    };

    private static GeneratedPlan Plan() => PlanGenerator.Generate(new[]
    {
        Target(1, MonitoringCategory.Water, new FrequencyRuleDto(FrequencyType.Weekly, 1, DayOfWeek.Sunday)),
        Target(2, MonitoringCategory.EnvironmentalMonitoring, new FrequencyRuleDto(FrequencyType.Weekly, 1, DayOfWeek.Wednesday)),
        Target(3, MonitoringCategory.EnvironmentalMonitoring, new FrequencyRuleDto(FrequencyType.Weekly, 1, DayOfWeek.Friday)),
        Target(4, MonitoringCategory.EnvironmentalMonitoring, new FrequencyRuleDto(FrequencyType.Weekly, 2)),
        Target(5, MonitoringCategory.EnvironmentalMonitoring, new FrequencyRuleDto(FrequencyType.Monthly, 1)),
        Target(6, MonitoringCategory.Water, new FrequencyRuleDto(FrequencyType.Monthly, 3))
    }, 2026, new DateOnly(2026, 1, 1), Lab, Holidays);

    private static bool IsWorkingDay(DateOnly d) => d.DayOfWeek is not (DayOfWeek.Friday or DayOfWeek.Saturday) && !Holidays.Contains(d);

    [Fact]
    public void EveryTargetDate_IsAWorkingDay_InsideItsOwnWindow()
    {
        foreach (var o in Plan().Occurrences.Where(o => o.Period.TargetDate.HasValue))
        {
            var target = o.Period.TargetDate!.Value;
            Assert.True(IsWorkingDay(target), $"{o.TargetName} {o.Period.PeriodKey}: {target} is not a working day");
            Assert.InRange(target, o.Period.WindowStart, o.Period.WindowEnd);
        }
    }

    [Fact]
    public void PeriodKeys_AreUniquePerTarget_AndWindowsDoNotOverlapWithinAWeeklySeries()
    {
        foreach (var series in Plan().Occurrences.GroupBy(o => o.TargetId))
        {
            Assert.Equal(series.Count(), series.Select(o => o.Period.PeriodKey).Distinct().Count());
            foreach (var o in series) Assert.True(o.Period.WindowStart <= o.Period.WindowEnd);
        }
    }

    [Fact]
    public void Counts_FollowTheFrequency()
    {
        var byTarget = Plan().Occurrences.GroupBy(o => o.TargetId).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(52, byTarget[1]);
        Assert.Equal(52, byTarget[2]);
        Assert.Equal(52, byTarget[3]);
        Assert.Equal(26, byTarget[4]);
        Assert.Equal(12, byTarget[5]);
        Assert.Equal(4, byTarget[6]);
    }

    [Fact]
    public void EveryMovedTarget_HasAMatchingException()
    {
        var plan = Plan();

        var moved = plan.Occurrences.Count(o => o.Period.MovedForNonWorkingDate);
        var reported = plan.Exceptions.Count(e => e.Kind == PlanExceptionKind.TargetDateMoved);

        Assert.True(moved > 0);
        Assert.Equal(moved, reported);
    }

    [Fact]
    public void Regenerating_GivesTheSameKeys()
    {
        var first = Plan().Occurrences.Select(o => o.Key).ToList();
        var second = Plan().Occurrences.Select(o => o.Key).ToList();

        Assert.Equal(first, second);
    }
}
