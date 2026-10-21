using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Monitoring;

// A structured frequency as sent to and returned by the master-data
// endpoints. Type = null means "no rule".
public record FrequencyRuleDto(FrequencyType? Type, int? Interval = null, DayOfWeek? SamplingWeekday = null, int? AnchorMonth = null, int? WeekOfMonth = null);

public enum FrequencySource
{
    None,
    Own,
    ParentRoom,
    Department
}

public record EffectiveFrequency(FrequencyRuleDto? Rule, FrequencySource Source);

public static class FrequencyRules
{
    public static FrequencyRuleDto? From(IHasFrequency e) =>
        e.FrequencyType is null
            ? null
            : new FrequencyRuleDto(e.FrequencyType, e.FrequencyInterval, e.SamplingWeekday, e.FrequencyAnchorMonth, e.FrequencyWeekOfMonth);

    // Returns the first problem with the rule, or null when it is valid.
    // A rule with no Type is the "clear" form and is always valid.
    public static string? Validate(FrequencyRuleDto rule, bool weekdayRequired = false)
    {
        if (rule.Type is null) return null;
        var interval = rule.Interval ?? 1;
        if (rule.Type == FrequencyType.Daily)
        {
            if (interval != 1) return "A daily frequency cannot have an interval.";
            if (rule.SamplingWeekday is not null) return "A daily frequency cannot have a sampling weekday.";
            if (rule.AnchorMonth is not null) return "A daily frequency cannot have an anchor month.";
            if (rule.WeekOfMonth is not null) return "A daily frequency cannot have a week of the month.";
            return null;
        }
        if (interval < 1 || interval > 52)
            return "Frequency interval must be between 1 and 52.";
        if (rule.Type == FrequencyType.Weekly)
        {
            if (rule.AnchorMonth is not null) return "A weekly frequency cannot have an anchor month.";
            if (rule.WeekOfMonth is not null) return "A weekly frequency cannot have a week of the month.";
            if (weekdayRequired && rule.SamplingWeekday is null) return "A weekly frequency needs a sampling weekday.";
        }
        else
        {
            if (interval > 12) return "A monthly interval cannot exceed 12 months.";
            if (rule.AnchorMonth is < 1 or > 12) return "Anchor month must be between 1 and 12.";
            if (rule.AnchorMonth is not null && interval == 1) return "An anchor month only applies to every-N-months rules.";
            if (rule.WeekOfMonth is < 1 or > 4) return "Week of the month must be between 1 and 4.";
        }
        return null;
    }

    // Writes the rule onto the entity. A null dto leaves the entity alone;
    // a dto with no Type clears it.
    public static void Apply(IHasFrequency target, FrequencyRuleDto? rule)
    {
        if (rule is null) return;
        if (rule.Type is null)
        {
            target.FrequencyType = null;
            target.FrequencyInterval = null;
            target.SamplingWeekday = null;
            target.FrequencyAnchorMonth = null;
            target.FrequencyWeekOfMonth = null;
            return;
        }
        target.FrequencyType = rule.Type;
        target.FrequencyInterval = rule.Interval ?? 1;
        target.SamplingWeekday = rule.SamplingWeekday;
        target.FrequencyAnchorMonth = rule.AnchorMonth;
        target.FrequencyWeekOfMonth = rule.WeekOfMonth;
    }

    // A Room's own rule overrides its parent room's (for an LAF), which
    // overrides its department's.
    public static EffectiveFrequency Resolve(IHasFrequency? own, IHasFrequency? department, IHasFrequency? parentRoom = null)
    {
        if (own?.FrequencyType is not null) return new(From(own), FrequencySource.Own);
        if (parentRoom?.FrequencyType is not null) return new(From(parentRoom), FrequencySource.ParentRoom);
        if (department?.FrequencyType is not null) return new(From(department), FrequencySource.Department);
        return new(null, FrequencySource.None);
    }

    // "Weekly, Sunday", "Daily, working days", "Monthly, week 2", "Every 3 months", "Every 3 months from March".
    public static string Describe(FrequencyRuleDto rule)
    {
        if (rule.Type is null) return string.Empty;
        if (rule.Type == FrequencyType.Daily) return "Daily, working days";
        var n = rule.Interval ?? 1;
        var text = rule.Type == FrequencyType.Weekly
            ? (n == 1 ? "Weekly" : $"Every {n} weeks")
            : (n == 1 ? "Monthly" : $"Every {n} months");
        if (rule.Type == FrequencyType.Monthly && n > 1 && rule.AnchorMonth is int a)
            text += $" from {System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(a)}";
        if (rule.SamplingWeekday is DayOfWeek d) text += $", {d}";
        if (rule.WeekOfMonth is int w) text += $", week {w}";
        return text;
    }
}
