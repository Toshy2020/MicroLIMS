using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// A structured sampling frequency, stored as plain columns on the target so
// the automatic audit trail records each change. All null = no rule. On a
// Room the same columns are an override of the department's rule.
public interface IHasFrequency
{
    FrequencyType? FrequencyType { get; set; }
    // Every N weeks or N months; >= 1.
    int? FrequencyInterval { get; set; }
    // Required for weekly Water points, optional for EM.
    DayOfWeek? SamplingWeekday { get; set; }
    // 1..12; only for every-N-months rules.
    int? FrequencyAnchorMonth { get; set; }
    // 1..4; pins a monthly rule to the Nth lab week of the month.
    int? FrequencyWeekOfMonth { get; set; }
}
