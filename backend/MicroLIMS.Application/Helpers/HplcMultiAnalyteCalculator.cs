using System.Globalization;
using System.Text.Json;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Helpers;

public record HplcMultiAnalyteCalculationResult(
    decimal Cs,
    IReadOnlyList<HplcMultiAnalytePreparationData> Preparations,
    decimal Mpu,
    decimal Result,
    decimal? PercentLabelClaim,
    decimal? PreparationRsdPercent,
    bool RsdExceeded,
    string? ReviewReason,
    decimal ReportedValue,
    string ReportedDisplay,
    string ComparisonStatus,
    string CalculationJson,
    HplcMultiAnalyteCalculationData CalculationData);

public static class HplcMultiAnalyteCalculator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static decimal CalculateCs(decimal standardWeightMg, decimal standardPurityPercent, decimal standardDilution)
    {
        if (standardWeightMg <= 0m)
            throw new InvalidOperationException("Standard weight must be greater than zero.");
        if (standardPurityPercent <= 0m)
            throw new InvalidOperationException("Standard purity must be greater than zero.");
        if (standardDilution <= 0m)
            throw new InvalidOperationException("Standard dilution must be greater than zero.");

        return (standardWeightMg * (standardPurityPercent / 100m)) / standardDilution;
    }

    public static decimal CalculateInjectionAmount(
        decimal area,
        decimal standardMeanArea,
        decimal cs,
        decimal sampleDilutionMl,
        decimal unitAmount,
        decimal sampleAmount)
    {
        if (area <= 0m)
            throw new InvalidOperationException("Injection area must be greater than zero.");
        if (standardMeanArea <= 0m)
            throw new InvalidOperationException("Standard mean area must be greater than zero.");
        if (cs <= 0m)
            throw new InvalidOperationException("Standard concentration (C_s) must be greater than zero.");
        if (sampleDilutionMl <= 0m)
            throw new InvalidOperationException("Sample dilution must be greater than zero.");
        if (unitAmount <= 0m)
            throw new InvalidOperationException("Unit amount must be greater than zero.");
        if (sampleAmount <= 0m)
            throw new InvalidOperationException("Sample amount must be greater than zero.");

        // amount (mg per unit) = (A_u / A_s) * C_s * D_sample * UnitAmount / SampleAmount
        // Multiply before divide in decimal arithmetic to preserve precision
        return (area * cs * sampleDilutionMl * unitAmount) / (standardMeanArea * sampleAmount);
    }

    public static (decimal? RsdPercent, bool RsdExceeded, string? ReviewReason) CalculatePreparationRsd(
        IReadOnlyList<decimal> preparationMeans,
        decimal? maxPreparationRsdPercent)
    {
        if (preparationMeans == null || preparationMeans.Count < 2)
            return (null, false, null);

        decimal mean = preparationMeans.Average();
        if (mean == 0m)
            return (0m, false, null);

        int n = preparationMeans.Count;
        decimal sumSquaredDiffs = 0m;
        foreach (var val in preparationMeans)
        {
            decimal diff = val - mean;
            sumSquaredDiffs += diff * diff;
        }

        decimal variance = sumSquaredDiffs / (n - 1);
        decimal stdDev = DecimalMath.Sqrt(variance, 10);
        decimal rsd = (stdDev / mean) * 100m;

        bool rsdExceeded = maxPreparationRsdPercent.HasValue && rsd > maxPreparationRsdPercent.Value;
        string? reviewReason = rsdExceeded
            ? $"Preparation RSD {Math.Round(rsd, 2, MidpointRounding.AwayFromZero):0.00}% exceeds maximum allowed {Math.Round(maxPreparationRsdPercent!.Value, 2, MidpointRounding.AwayFromZero):0.00}%."
            : null;

        return (rsd, rsdExceeded, reviewReason);
    }

    public static HplcMultiAnalyteCalculationResult Calculate(
        string analyteName,
        int testAnalyteId,
        int systemSuitabilityRunAnalyteId,
        decimal standardWeightMg,
        decimal standardPurityPercent,
        decimal standardDilution,
        decimal standardMeanArea,
        decimal unitAmount,
        string unitAmountSource,
        SampleMatrix sampleMatrix,
        Specification spec,
        IReadOnlyList<HplcPreparationInput> preparations,
        IReadOnlyList<HplcAreaInput> areas,
        decimal? maxPreparationRsdPercent)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(preparations);
        ArgumentNullException.ThrowIfNull(areas);

        if (spec.ResultBasis != ResultBasis.MgPerUnit && spec.ResultBasis != ResultBasis.PercentLabelClaim)
            throw new InvalidOperationException($"Result basis '{spec.ResultBasis}' is not allowed for HPLC Multi-Analyte specifications (must be MgPerUnit or PercentLabelClaim).");

        if (spec.ConversionFactor <= 0m)
            throw new InvalidOperationException("Conversion factor must be greater than zero.");

        decimal cs = CalculateCs(standardWeightMg, standardPurityPercent, standardDilution);

        var prepCalculations = new List<HplcMultiAnalytePreparationData>();

        for (int p = 1; p <= preparations.Count; p++)
        {
            var prep = preparations[p - 1];
            var prepAreas = areas.Where(a => a.PreparationIndex == p).OrderBy(a => a.InjectionIndex).ToList();

            var injectionCalculations = new List<HplcMultiAnalyteInjectionData>();
            foreach (var inj in prepAreas)
            {
                decimal injAmount = CalculateInjectionAmount(
                    inj.Area,
                    standardMeanArea,
                    cs,
                    prep.SampleDilutionMl,
                    unitAmount,
                    prep.SampleAmount);

                decimal injConverted = injAmount * spec.ConversionFactor;

                injectionCalculations.Add(new HplcMultiAnalyteInjectionData(
                    PreparationIndex: p,
                    InjectionIndex: inj.InjectionIndex,
                    Area: inj.Area,
                    AmountPerUnit: injAmount,
                    ConvertedAmount: injConverted));
            }

            decimal prepMeanAmount = injectionCalculations.Select(i => i.AmountPerUnit).Average();
            decimal prepMeanConverted = injectionCalculations.Select(i => i.ConvertedAmount).Average();

            prepCalculations.Add(new HplcMultiAnalytePreparationData(
                PreparationIndex: p,
                SampleAmount: prep.SampleAmount,
                SampleDilutionMl: prep.SampleDilutionMl,
                Injections: injectionCalculations,
                MeanAmountPerUnit: prepMeanAmount,
                MeanConvertedAmount: prepMeanConverted));
        }

        decimal mpu = prepCalculations.Select(p => p.MeanAmountPerUnit).Average();
        decimal result = mpu * spec.ConversionFactor;

        decimal? percentLabelClaim = null;
        if (spec.ResultBasis == ResultBasis.PercentLabelClaim)
        {
            if (!spec.LabelClaim.HasValue || spec.LabelClaim.Value <= 0m)
                throw new InvalidOperationException($"Label claim required to compute PercentLabelClaim for specification '{spec.ParameterName}'.");

            percentLabelClaim = (result / spec.LabelClaim.Value) * 100m;
        }
        else if (spec.LabelClaim.HasValue && spec.LabelClaim.Value > 0m)
        {
            percentLabelClaim = (result / spec.LabelClaim.Value) * 100m;
        }

        var (rsdPercent, rsdExceeded, reviewReason) = CalculatePreparationRsd(
            prepCalculations.Select(p => p.MeanAmountPerUnit).ToList(),
            maxPreparationRsdPercent);

        decimal reportedValue = spec.ResultBasis == ResultBasis.PercentLabelClaim
            ? percentLabelClaim!.Value
            : result;

        var rounded = Math.Round(reportedValue, 1, MidpointRounding.AwayFromZero).ToString("0.0", CultureInfo.InvariantCulture);
        string reportedDisplay = spec.ResultBasis == ResultBasis.PercentLabelClaim
            ? $"{rounded} %"
            : $"{rounded} {(!string.IsNullOrWhiteSpace(spec.Unit) ? spec.Unit : (!string.IsNullOrWhiteSpace(spec.LabelClaimUnit) ? spec.LabelClaimUnit : "mg"))}";

        string status = SpecificationEvaluator.Evaluate(spec, reportedValue);
        if (rsdExceeded)
        {
            status = "RequiresReview";
        }

        var calcData = new HplcMultiAnalyteCalculationData(
            AnalyteName: analyteName,
            TestAnalyteId: testAnalyteId,
            SystemSuitabilityRunAnalyteId: systemSuitabilityRunAnalyteId,
            StandardWeightMg: standardWeightMg,
            StandardPurityPercent: standardPurityPercent,
            StandardDilution: standardDilution,
            StandardMeanArea: standardMeanArea,
            Cs: cs,
            UnitAmountSource: unitAmountSource,
            UnitAmount: unitAmount,
            SampleMatrix: sampleMatrix,
            Preparations: prepCalculations,
            Mpu: mpu,
            ConversionFactor: spec.ConversionFactor,
            Result: result,
            LabelClaim: spec.LabelClaim,
            LabelClaimUnit: spec.LabelClaimUnit,
            PercentLabelClaim: percentLabelClaim,
            PreparationRsdPercent: rsdPercent,
            MaxPreparationRsdPercent: maxPreparationRsdPercent,
            RsdExceeded: rsdExceeded,
            ReviewReason: reviewReason);

        string calculationJson = JsonSerializer.Serialize(calcData, JsonOptions);

        return new HplcMultiAnalyteCalculationResult(
            Cs: cs,
            Preparations: prepCalculations,
            Mpu: mpu,
            Result: result,
            PercentLabelClaim: percentLabelClaim,
            PreparationRsdPercent: rsdPercent,
            RsdExceeded: rsdExceeded,
            ReviewReason: reviewReason,
            ReportedValue: reportedValue,
            ReportedDisplay: reportedDisplay,
            ComparisonStatus: status,
            CalculationJson: calculationJson,
            CalculationData: calcData);
    }
}
