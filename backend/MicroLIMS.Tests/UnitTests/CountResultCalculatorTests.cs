using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// The same inputs and expected values as CountResultCharacterisationPostgresTests,
// run through the pure calculator with no database. Both files must agree: the
// calculator is what the save path and the preview endpoint share. Values that
// are open laboratory questions (fractional/negative readings, "1,000", first of
// TNTC/Uncountable, status on the unrounded value, "<" threshold = DF) are pinned
// as they are today.
public class CountResultCalculatorTests
{
    private const string Water = "CFU/mL";

    private static object?[] Row(string name, string[] readings, decimal df, string defaultUnit, string? configuredUnit,
        string? alert, string? action, string? spec,
        string reported, decimal? average, decimal? calculated, ResultStatus status, string? nonNumeric = null) =>
        new object?[] { name, readings, df, defaultUnit, configuredUnit, alert, action, spec, reported, average, calculated, status, nonNumeric };

    public static IEnumerable<object?[]> Cases()
    {
        // Direct count (water), DF 1
        yield return Row("water two plates", new[] { "12", "14" }, 1m, Water, null, null, null, null, "13 CFU/mL", 13m, 13m, ResultStatus.LimitsNotConfigured);
        // Product with dilution
        yield return Row("product dilution", new[] { "12", "14" }, 10m, "CFU/unit", "g", "200", "500", "1000", "130 CFU/g", 13m, 130m, ResultStatus.WithinLimits);
        yield return Row("RM unit ml", new[] { "12", "14" }, 10m, "CFU/unit", "ml", "", "", "1000", "130 CFU/ml", 13m, 130m, ResultStatus.WithinLimits);
        yield return Row("overridden DF 100", new[] { "12", "14" }, 100m, "CFU/unit", "g", "", "", "100000", "1300 CFU/g", 13m, 1300m, ResultStatus.WithinLimits);
        // Item-less non-direct sample: client DF as typed, default unit
        yield return Row("itemless GPT", new[] { "12", "14" }, 10m, "CFU/unit", null, null, null, null, "130 CFU/unit", 13m, 130m, ResultStatus.LimitsNotConfigured);
        // Non-numeric
        yield return Row("TNTC", new[] { "TNTC" }, 1m, Water, null, "10", "50", "100", "TNTC", null, null, ResultStatus.RequiresReview, "TNTC");
        yield return Row("Uncountable", new[] { "Uncountable" }, 1m, Water, null, "10", "50", "100", "Uncountable", null, null, ResultStatus.RequiresReview, "Uncountable");
        yield return Row("tntc lower case", new[] { "tntc" }, 1m, Water, null, null, null, null, "TNTC", null, null, ResultStatus.RequiresReview, "TNTC");
        yield return Row("numbers around TNTC", new[] { "12", "TNTC", "14" }, 1m, Water, null, null, null, null, "TNTC", null, null, ResultStatus.RequiresReview, "TNTC");
        yield return Row("Uncountable then TNTC", new[] { "Uncountable", "TNTC" }, 1m, Water, null, null, null, null, "Uncountable", null, null, ResultStatus.RequiresReview, "Uncountable");
        yield return Row("TNTC then Uncountable", new[] { "TNTC", "Uncountable" }, 1m, Water, null, null, null, null, "TNTC", null, null, ResultStatus.RequiresReview, "TNTC");
        // Rounding
        yield return Row("midpoint on even below", new[] { "12", "13" }, 1m, Water, null, null, null, null, "13 CFU/mL", 12.5m, 12.5m, ResultStatus.LimitsNotConfigured);
        yield return Row("midpoint on odd below", new[] { "13", "14" }, 1m, Water, null, null, null, null, "14 CFU/mL", 13.5m, 13.5m, ResultStatus.LimitsNotConfigured);
        yield return Row("just below midpoint", new[] { "12.4" }, 1m, Water, null, null, null, null, "12 CFU/mL", 12.4m, 12.4m, ResultStatus.LimitsNotConfigured);
        yield return Row("just above midpoint", new[] { "12.6" }, 1m, Water, null, null, null, null, "13 CFU/mL", 12.6m, 12.6m, ResultStatus.LimitsNotConfigured);
        yield return Row("midpoint after dilution", new[] { "5" }, 0.5m, "CFU/unit", "g", "", "", "1000", "3 CFU/g", 5m, 2.5m, ResultStatus.WithinLimits);
        yield return Row("repeating average", new[] { "1", "1", "2" }, 10m, "CFU/unit", "g", null, null, "1000", "13 CFU/g", 4m / 3m, 4m / 3m * 10m, ResultStatus.WithinLimits);
        yield return Row("fractional reading accepted", new[] { "12.5" }, 1m, Water, null, null, null, null, "13 CFU/mL", 12.5m, 12.5m, ResultStatus.LimitsNotConfigured);
        yield return Row("negative reading accepted", new[] { "-3" }, 1m, Water, null, null, null, null, "<1 CFU/mL", -3m, -3m, ResultStatus.LimitsNotConfigured);
        yield return Row("thousands separator parsed", new[] { "1,000" }, 1m, Water, null, null, null, null, "1000 CFU/mL", 1000m, 1000m, ResultStatus.LimitsNotConfigured);
        // Below limit
        yield return Row("zero colonies water", new[] { "0" }, 1m, Water, null, "10", "50", "100", "<1 CFU/mL", 0m, 0m, ResultStatus.WithinLimits);
        yield return Row("average below one", new[] { "0", "1" }, 1m, Water, null, null, null, null, "<1 CFU/mL", 0.5m, 0.5m, ResultStatus.LimitsNotConfigured);
        yield return Row("product below DF", new[] { "0", "0" }, 10m, "CFU/unit", "g", "", "", "1000", "<10 CFU/g", 0m, 0m, ResultStatus.WithinLimits);
        yield return Row("product half a colony", new[] { "0", "1" }, 10m, "CFU/unit", "g", "", "", "1000", "<10 CFU/g", 0.5m, 5m, ResultStatus.WithinLimits);
        yield return Row("calculated equals DF", new[] { "1" }, 10m, "CFU/unit", "g", "", "", "1000", "10 CFU/g", 1m, 10m, ResultStatus.WithinLimits);
        yield return Row("fractional DF threshold rounded", new[] { "0" }, 2.5m, "CFU/unit", "g", "", "", "1000", "<3 CFU/g", 0m, 0m, ResultStatus.WithinLimits);
        // Unit selection
        yield return Row("unit without prefix", new[] { "13" }, 1m, Water, "100mL", null, null, null, "13 CFU/100mL", 13m, 13m, ResultStatus.LimitsNotConfigured);
        yield return Row("unit with prefix", new[] { "13" }, 1m, Water, "CFU/100mL", null, null, null, "13 CFU/100mL", 13m, 13m, ResultStatus.LimitsNotConfigured);
        yield return Row("unit prefix case-insensitive", new[] { "13" }, 1m, Water, "cfu/g", null, null, null, "13 cfu/g", 13m, 13m, ResultStatus.LimitsNotConfigured);
        yield return Row("unit CFU/25cm2", new[] { "13" }, 1m, "CFU/unit", "CFU/25cm2", "", "", "", "13 CFU/25cm2", 13m, 13m, ResultStatus.LimitsNotConfigured);
        // Status tiers (water limits 10 / 50 / 100)
        yield return Row("tier within", new[] { "5" }, 1m, Water, null, "10", "50", "100", "5 CFU/mL", 5m, 5m, ResultStatus.WithinLimits);
        yield return Row("tier at alert limit", new[] { "10" }, 1m, Water, null, "10", "50", "100", "10 CFU/mL", 10m, 10m, ResultStatus.WithinLimits);
        yield return Row("tier alert", new[] { "12" }, 1m, Water, null, "10", "50", "100", "12 CFU/mL", 12m, 12m, ResultStatus.AlertLimitExceeded);
        yield return Row("tier action", new[] { "60" }, 1m, Water, null, "10", "50", "100", "60 CFU/mL", 60m, 60m, ResultStatus.ActionLimitExceeded);
        yield return Row("tier OOS", new[] { "150" }, 1m, Water, null, "10", "50", "100", "150 CFU/mL", 150m, 150m, ResultStatus.OutOfSpecification);
        yield return Row("status on unrounded value", new[] { "100.4" }, 1m, Water, null, "10", "50", "100", "100 CFU/mL", 100.4m, 100.4m, ResultStatus.OutOfSpecification);
        yield return Row("product action tier", new[] { "60" }, 10m, "CFU/unit", "g", "NMT 100", "500", "1000", "600 CFU/g", 60m, 600m, ResultStatus.ActionLimitExceeded);
        yield return Row("product OOS tier", new[] { "101" }, 10m, "CFU/unit", "g", "NMT 100", "500", "1000", "1010 CFU/g", 101m, 1010m, ResultStatus.OutOfSpecification);
        yield return Row("blank limits", new[] { "50" }, 10m, "CFU/unit", "g", "", "", "", "500 CFU/g", 50m, 500m, ResultStatus.LimitsNotConfigured);
        yield return Row("no configuration", new[] { "500" }, 1m, Water, null, null, null, null, "500 CFU/mL", 500m, 500m, ResultStatus.LimitsNotConfigured);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Calculate_MatchesSavedValues_ForEachCharacterisedCase(
        string name, string[] readings, decimal df, string defaultUnit, string? configuredUnit,
        string? alert, string? action, string? spec,
        string reported, decimal? average, decimal? calculated, ResultStatus status, string? nonNumeric)
    {
        var result = CountResultCalculator.Calculate(new CountCalculationInput(readings, df, defaultUnit, configuredUnit, alert, action, spec));

        Assert.True(reported == result.Reported, $"{name}: reported '{result.Reported}'");
        Assert.True(average == result.Average, $"{name}: average {result.Average}");
        Assert.True(calculated == result.Calculated, $"{name}: calculated {result.Calculated}");
        Assert.True(status == result.Status, $"{name}: status {result.Status}");
        Assert.True(nonNumeric == result.NonNumericValue, $"{name}: non-numeric value");
        Assert.Empty(result.Messages);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("TNTC ")]
    [InlineData("N/A")]
    public void Calculate_UnrecognisedWord_ThrowsFormatException(string word)
    {
        Assert.Throws<FormatException>(() =>
            CountResultCalculator.Calculate(new CountCalculationInput(new[] { word }, 1m, Water)));
    }
}
