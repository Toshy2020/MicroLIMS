using System.Globalization;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Workflows;

// What the calculator needs, all of it already loaded by the caller:
// the plate readings as typed, the dilution factor in force (after the
// direct-count "force to 1" rule), the unit the sample category defaults
// to, and the limits/unit configured for this item or water sampling point
// (null when none is configured; they are only read for numeric results).
public sealed record CountCalculationInput(
    IReadOnlyList<string> RawPlateReadings,
    decimal DilutionFactor,
    string DefaultUnit,
    string? ConfiguredUnit = null,
    string? AlertLimit = null,
    string? ActionLimit = null,
    string? SpecLimit = null);

// NonNumericValue is "TNTC" or "Uncountable" when a non-numeric reading
// decided the result (Average/Calculated are then null), otherwise null.
public sealed record CountCalculationResult(
    string Reported,
    decimal? Average,
    decimal? Calculated,
    ResultStatus Status,
    IReadOnlyList<string> Messages,
    string? NonNumericValue = null);

// The arithmetic, reporting and status of a plate-count result, moved out of
// IncubationStepRecorder.RecordCountTestAsync unchanged so the save and the
// preview use the same code. Pure: no database, no clock. Behaviour is pinned
// by CountResultCharacterisationPostgresTests and CountResultCalculatorTests,
// including rules that are open laboratory questions (fractional/negative
// readings accepted, thousands separator parsed, first of TNTC/Uncountable
// wins, status on the unrounded value, "<" threshold is the dilution factor).
public static class CountResultCalculator
{
    // True when any reading is the word TNTC or Uncountable (case-insensitive);
    // such a result is never calculated and always needs review.
    public static bool HasNonNumericReading(IReadOnlyList<string> rawPlateReadings) =>
        rawPlateReadings.Any(r => r.Equals("TNTC", StringComparison.OrdinalIgnoreCase) ||
                                  r.Equals("Uncountable", StringComparison.OrdinalIgnoreCase));

    // A reading the numeric branch cannot parse (it would throw FormatException).
    public static bool IsParsable(string reading) =>
        decimal.TryParse(reading, NumberStyles.Number, CultureInfo.InvariantCulture, out _);

    // Throws FormatException for a reading that is neither TNTC/Uncountable
    // nor a number, exactly as the save path always has. Callers guarantee
    // at least one reading.
    public static CountCalculationResult Calculate(CountCalculationInput input)
    {
        if (HasNonNumericReading(input.RawPlateReadings))
        {
            var nonNumericRaw = input.RawPlateReadings
                .First(r => !decimal.TryParse(r, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                .Trim();

            var nonNumericValue = nonNumericRaw.Equals("TNTC", StringComparison.OrdinalIgnoreCase) ? "TNTC" : "Uncountable";

            return new CountCalculationResult(nonNumericValue, null, null, ResultStatus.RequiresReview,
                Array.Empty<string>(), nonNumericValue);
        }

        var numericReadings = input.RawPlateReadings
            .Select(r => decimal.Parse(r, CultureInfo.InvariantCulture))
            .ToList();

        var average = numericReadings.Average();
        var calculated = average * input.DilutionFactor;
        var lowerLimit = input.DilutionFactor;

        var effectiveUnit = !string.IsNullOrWhiteSpace(input.ConfiguredUnit)
            ? (input.ConfiguredUnit.StartsWith("CFU/", StringComparison.OrdinalIgnoreCase) ? input.ConfiguredUnit : $"CFU/{input.ConfiguredUnit}")
            : input.DefaultUnit;

        // A CFU count is a whole-number concept - round to the nearest
        // integer for the reported display, same as
        // RecordBatchResultsAsync already does for EM/After Cleaning.
        // The full-precision value is still returned in Average/Calculated;
        // only the reported display is rounded.
        var reported = calculated < lowerLimit
            ? $"<{Math.Round(lowerLimit, MidpointRounding.AwayFromZero)} {effectiveUnit}"
            : $"{Math.Round(calculated, MidpointRounding.AwayFromZero)} {effectiveUnit}";

        var (status, _) = SpecLimitParser.Compare(calculated, input.AlertLimit, input.ActionLimit, input.SpecLimit);

        return new CountCalculationResult(reported, average, calculated, status, Array.Empty<string>());
    }
}
