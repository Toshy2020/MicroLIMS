using System.Globalization;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Responses;

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
// IncubationStepRecorder.RecordCountTestAsync so the save and the preview use
// the same code. Pure: no database, no clock. Behaviour is pinned by
// CountResultCharacterisationPostgresTests and CountResultCalculatorTests,
// including CONF-201 (ASCII digits only, or TNTC/Uncountable) and CONF-202
// (refuse TNTC and Uncountable together).
public static class CountResultCalculator
{
    public const string InvalidReadingMessage = "Enter a whole number using digits only, or TNTC or Uncountable.";
    public const string BothNonNumericFlagsMessage = "A reading set cannot contain both TNTC and Uncountable. Choose one.";

    public static bool IsTntc(string reading) =>
        reading.Equals("TNTC", StringComparison.OrdinalIgnoreCase);

    public static bool IsUncountable(string reading) =>
        reading.Equals("Uncountable", StringComparison.OrdinalIgnoreCase);

    public static bool IsNonNumericFlag(string reading) =>
        IsTntc(reading) || IsUncountable(reading);

    // True when any reading is the word TNTC or Uncountable (case-insensitive);
    // such a result is never calculated and always needs review.
    public static bool HasNonNumericReading(IReadOnlyList<string> rawPlateReadings) =>
        rawPlateReadings.Any(IsNonNumericFlag);

    // CONF-201: plate counts accept ASCII digits only ('0'-'9', non-empty, no separators, signs, spaces, or decimals).
    public static bool IsParsable(string reading) =>
        !string.IsNullOrEmpty(reading) &&
        reading.All(char.IsAsciiDigit) &&
        decimal.TryParse(reading, NumberStyles.None, CultureInfo.InvariantCulture, out _);

    public static IReadOnlyList<FieldError> ValidateReadings(IReadOnlyList<string> rawPlateReadings)
    {
        var errors = new List<FieldError>();
        bool hasTntc = false;
        bool hasUncountable = false;

        for (var i = 0; i < rawPlateReadings.Count; i++)
        {
            var reading = rawPlateReadings[i];
            if (IsTntc(reading))
            {
                hasTntc = true;
            }
            else if (IsUncountable(reading))
            {
                hasUncountable = true;
            }
            else if (!IsParsable(reading))
            {
                errors.Add(new FieldError("rawPlateReadings", InvalidReadingMessage, i));
            }
        }

        if (hasTntc && hasUncountable)
        {
            errors.Add(new FieldError("rawPlateReadings", BothNonNumericFlagsMessage));
        }

        return errors;
    }

    public static void Validate(IReadOnlyList<string> rawPlateReadings)
    {
        var errors = ValidateReadings(rawPlateReadings);
        if (errors.Count > 0)
        {
            throw new FieldValidationException(errors[0].Message, errors);
        }
    }

    // Throws FieldValidationException for readings that fail CONF-201 (digits-only
    // or TNTC/Uncountable) or CONF-202 (both TNTC and Uncountable together).
    // Callers guarantee at least one reading.
    public static CountCalculationResult Calculate(CountCalculationInput input)
    {
        Validate(input.RawPlateReadings);

        if (HasNonNumericReading(input.RawPlateReadings))
        {
            var nonNumericValue = input.RawPlateReadings.Any(IsTntc) ? "TNTC" : "Uncountable";

            return new CountCalculationResult(nonNumericValue, null, null, ResultStatus.RequiresReview,
                Array.Empty<string>(), nonNumericValue);
        }

        var numericReadings = input.RawPlateReadings
            .Select(r => decimal.Parse(r, NumberStyles.None, CultureInfo.InvariantCulture))
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
