using System.Text.RegularExpressions;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Monitoring;

// Reads the free-text TestingFrequency the master data used to carry
// ("Weekly", "Daily", "Quarterly", "Every 2 months") into a structured rule.
// Returns null for text it cannot map (e.g. "when needed"), so the caller leaves
// the structured rule empty for a person to set.
public static class LegacyFrequencyParser
{
    private static readonly Regex Every = new(
        @"^(?:every\s+)?(\d{1,2})[\s-]*(week|wk|month|mo)s?(?:ly)?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static FrequencyRuleDto? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim();

        switch (t.ToLowerInvariant())
        {
            case "daily": return new(FrequencyType.Daily, 1);
            case "weekly": return new(FrequencyType.Weekly, 1);
            case "fortnightly":
            case "biweekly":
            case "bi-weekly": return new(FrequencyType.Weekly, 2);
            case "monthly": return new(FrequencyType.Monthly, 1);
            case "bimonthly":
            case "bi-monthly": return new(FrequencyType.Monthly, 2);
            case "quarterly": return new(FrequencyType.Monthly, 3);
            case "semiannually":
            case "semi-annually":
            case "biannually": return new(FrequencyType.Monthly, 6);
            case "annually":
            case "yearly": return new(FrequencyType.Monthly, 12);
        }

        var m = Every.Match(t);
        if (!m.Success) return null;
        var n = int.Parse(m.Groups[1].Value);
        if (n < 1) return null;
        var weekly = m.Groups[2].Value.StartsWith("w", StringComparison.OrdinalIgnoreCase);
        if (weekly ? n > 52 : n > 12) return null;
        return new(weekly ? FrequencyType.Weekly : FrequencyType.Monthly, n);
    }
}
