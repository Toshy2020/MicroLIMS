using System.Globalization;
using System.Text.RegularExpressions;

namespace MicroLIMS.Application.Services;

public sealed record ParsedSpecLimit(
    decimal? Min,
    decimal? Max,
    string? Unit = null,
    string? MinRaw = null,
    string? MaxRaw = null)
{
    public static readonly ParsedSpecLimit None = new(null, null);

    public bool HasLimit => Min.HasValue || Max.HasValue;
    public bool IsRange => Min.HasValue && Max.HasValue;

    public bool IsExceededBy(decimal value) =>
        (Min.HasValue && value < Min.Value) ||
        (Max.HasValue && value > Max.Value);

    public void Deconstruct(out decimal? min, out decimal? max)
    {
        min = Min;
        max = Max;
    }

    public void Deconstruct(out decimal? min, out decimal? max, out string? unit)
    {
        min = Min;
        max = Max;
        unit = Unit;
    }
}

public static class SpecLimitParser
{
    // Regex for range: "90.0 - 110.0 %", "90-110", "90.0–110.0"
    // Supports ASCII hyphen (-), en dash (\u2013), em dash (\u2014), horizontal bar (\u2015), minus sign (\u2212)
    private static readonly Regex RangeRegex = new(
        @"^(?<min>(?:\d+(?:\.\d+)?|\.\d+))\s*[-–—―\u2212]\s*(?<max>(?:\d+(?:\.\d+)?|\.\d+))(?:\s*(?<unit>[^\d\s\.\-–—―\u2212].*))?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Regex for NMT (Not More Than): "NMT 100", "nmt 100.5 %", "NMT: 100", "NMT100"
    private static readonly Regex NmtRegex = new(
        @"^NMT\s*[:]?\s*(?<val>(?:\d+(?:\.\d+)?|\.\d+))(?:\s*(?<unit>[^\d\s\.\-–—―\u2212].*))?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // Regex for NLT (Not Less Than): "NLT 90", "nlt 90.0 %", "NLT: 90", "NLT90"
    private static readonly Regex NltRegex = new(
        @"^NLT\s*[:]?\s*(?<val>(?:\d+(?:\.\d+)?|\.\d+))(?:\s*(?<unit>[^\d\s\.\-–—―\u2212].*))?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // Regex for plain number: "100", "100.5", "100 %", "100 CFU/g"
    private static readonly Regex PlainNumberRegex = new(
        @"^(?<val>(?:\d+(?:\.\d+)?|\.\d+))(?:\s*(?<unit>[^\d\s\.\-–—―\u2212].*))?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static ParsedSpecLimit Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return ParsedSpecLimit.None;

        var text = input.Trim();

        // 0. Anything the pre-range code accepted as a plain number (e.g.
        // "1,000") keeps its old meaning - an upper bound - so no existing
        // limit silently stops being checked.
        if (decimal.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var legacy))
            return new ParsedSpecLimit(Min: null, Max: legacy, MaxRaw: text);

        // 1. Try Range: x-y, x - y, x–y
        var rangeMatch = RangeRegex.Match(text);
        if (rangeMatch.Success)
        {
            var minStr = rangeMatch.Groups["min"].Value;
            var maxStr = rangeMatch.Groups["max"].Value;
            if (decimal.TryParse(minStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var min) &&
                decimal.TryParse(maxStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var max))
            {
                if (min <= max)
                {
                    var unitGroup = rangeMatch.Groups["unit"];
                    var unit = unitGroup.Success && !string.IsNullOrWhiteSpace(unitGroup.Value)
                        ? unitGroup.Value.Trim()
                        : null;

                    return new ParsedSpecLimit(
                        Min: min,
                        Max: max,
                        Unit: unit,
                        MinRaw: minStr,
                        MaxRaw: maxStr);
                }
            }
            return ParsedSpecLimit.None;
        }

        // 2. Try NMT: NMT x
        var nmtMatch = NmtRegex.Match(text);
        if (nmtMatch.Success)
        {
            var valStr = nmtMatch.Groups["val"].Value;
            if (decimal.TryParse(valStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
            {
                var unitGroup = nmtMatch.Groups["unit"];
                var unit = unitGroup.Success && !string.IsNullOrWhiteSpace(unitGroup.Value)
                    ? unitGroup.Value.Trim()
                    : null;

                return new ParsedSpecLimit(
                    Min: null,
                    Max: val,
                    Unit: unit,
                    MinRaw: null,
                    MaxRaw: valStr);
            }
            return ParsedSpecLimit.None;
        }

        // 3. Try NLT: NLT x
        var nltMatch = NltRegex.Match(text);
        if (nltMatch.Success)
        {
            var valStr = nltMatch.Groups["val"].Value;
            if (decimal.TryParse(valStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
            {
                var unitGroup = nltMatch.Groups["unit"];
                var unit = unitGroup.Success && !string.IsNullOrWhiteSpace(unitGroup.Value)
                    ? unitGroup.Value.Trim()
                    : null;

                return new ParsedSpecLimit(
                    Min: val,
                    Max: null,
                    Unit: unit,
                    MinRaw: valStr,
                    MaxRaw: null);
            }
            return ParsedSpecLimit.None;
        }

        // 4. Try Plain Number: x (existing meaning: upper bound)
        var plainMatch = PlainNumberRegex.Match(text);
        if (plainMatch.Success)
        {
            var valStr = plainMatch.Groups["val"].Value;
            if (decimal.TryParse(valStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
            {
                var unitGroup = plainMatch.Groups["unit"];
                var unit = unitGroup.Success && !string.IsNullOrWhiteSpace(unitGroup.Value)
                    ? unitGroup.Value.Trim()
                    : null;

                return new ParsedSpecLimit(
                    Min: null,
                    Max: val,
                    Unit: unit,
                    MinRaw: null,
                    MaxRaw: valStr);
            }
            return ParsedSpecLimit.None;
        }

        // Unparseable non-empty limit: return no limit, never throw
        return ParsedSpecLimit.None;
    }

    public static (string status, string? exceeded) Compare(decimal value, string? alert, string? action, string? spec)
    {
        var specLimit = Parse(spec);
        if (specLimit.HasLimit && specLimit.IsExceededBy(value))
            return ("OutOfSpecification", "Specification");

        var actionLimit = Parse(action);
        if (actionLimit.HasLimit && actionLimit.IsExceededBy(value))
            return ("ActionLimitExceeded", "Action");

        var alertLimit = Parse(alert);
        if (alertLimit.HasLimit && alertLimit.IsExceededBy(value))
            return ("AlertLimitExceeded", "Alert");

        if (!specLimit.HasLimit && !actionLimit.HasLimit && !alertLimit.HasLimit)
            return ("LimitsNotConfigured", null);

        return ("WithinLimits", null);
    }

    public static string CompareAgainstLimits(decimal value, string? alert, string? action, string? spec) =>
        Compare(value, alert, action, spec).status;
}
