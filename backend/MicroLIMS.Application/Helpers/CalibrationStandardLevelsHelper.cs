using System.Globalization;

namespace MicroLIMS.Application.Helpers;

// AAS and ICP-OES calibration curves are built from a fixed set of standard
// concentrations (mg/L), configurable per test (D-A4). This parses the
// comma-separated Test Master input into a sorted, de-duplicated list and
// produces the normalised string used for storage/display (e.g. "1, 5").
public static class CalibrationStandardLevelsHelper
{
    public static (List<decimal> Levels, string Normalized) ParseAndValidate(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("Standard levels cannot be empty.");

        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
            throw new InvalidOperationException("At least two standard levels are required.");

        var levels = new List<decimal>();
        foreach (var part in parts)
        {
            if (!decimal.TryParse(part, NumberStyles.Number, CultureInfo.InvariantCulture, out var level))
                throw new InvalidOperationException($"\"{part}\" is not a valid standard level.");

            if (level <= 0m)
                throw new InvalidOperationException($"Standard level {level.ToString(CultureInfo.InvariantCulture)} must be greater than zero.");

            levels.Add(level);
        }

        if (levels.Distinct().Count() != levels.Count)
            throw new InvalidOperationException("Standard levels must be distinct.");

        levels.Sort();

        var normalized = string.Join(", ", levels.Select(l => l.ToString(CultureInfo.InvariantCulture)));
        return (levels, normalized);
    }
}
