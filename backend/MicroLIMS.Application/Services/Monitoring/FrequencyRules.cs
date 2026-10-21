using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Monitoring;

// A structured frequency as sent to and returned by the master-data
// endpoints. Type = null means "no rule".
public record FrequencyRuleDto(FrequencyType? Type, int? Interval = null, DayOfWeek? SamplingWeekday = null, int? AnchorMonth = null);

public enum FrequencySource
{
    None,
    Own,
    Department
}

public record EffectiveFrequency(FrequencyRuleDto? Rule, FrequencySource Source);

public static class FrequencyRules
{
    public static FrequencyRuleDto? From(IHasFrequency e) =>
        e.FrequencyType is null
            ? null
            : new FrequencyRuleDto(e.FrequencyType, e.FrequencyInterval, e.SamplingWeekday, e.FrequencyAnchorMonth);

    // Returns the first problem with the rule, or null when it is valid.
    // A rule with no Type is the "clear" form and is always valid.
    public static string? Validate(FrequencyRuleDto rule, bool weekdayRequired = false)
    {
        if (rule.Type is null) return null;
        var interval = rule.Interval ?? 1;
        if (interval < 1 || interval > 52)
            return "Frequency interval must be between 1 and 52.";
        if (rule.Type == FrequencyType.Weekly)
        {
            if (rule.AnchorMonth is not null) return "A weekly frequency cannot have an anchor month.";
            if (weekdayRequired && rule.SamplingWeekday is null) return "A weekly frequency needs a sampling weekday.";
        }
        else
        {
            if (interval > 12) return "A monthly interval cannot exceed 12 months.";
            if (rule.AnchorMonth is < 1 or > 12) return "Anchor month must be between 1 and 12.";
            if (rule.AnchorMonth is not null && interval == 1) return "An anchor month only applies to every-N-months rules.";
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
            return;
        }
        target.FrequencyType = rule.Type;
        target.FrequencyInterval = rule.Interval ?? 1;
        target.SamplingWeekday = rule.SamplingWeekday;
        target.FrequencyAnchorMonth = rule.AnchorMonth;
    }

    // A Room's own rule overrides its department's.
    public static EffectiveFrequency Resolve(IHasFrequency? own, IHasFrequency? department)
    {
        if (own?.FrequencyType is not null) return new(From(own), FrequencySource.Own);
        if (department?.FrequencyType is not null) return new(From(department), FrequencySource.Department);
        return new(null, FrequencySource.None);
    }
}
