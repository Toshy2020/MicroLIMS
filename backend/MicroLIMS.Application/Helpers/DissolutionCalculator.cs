using System.Globalization;
using System.Text.Json;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Helpers;

public record DissolutionCalculationResult(
    decimal Mean,
    decimal ReportedValue,
    string ReportedDisplay,
    string ComparisonStatus,
    int StageReached,
    DissolutionStageOutcome Outcome,
    IReadOnlyList<string> Reasons,
    string CalculationJson,
    IReadOnlyList<DissolutionVesselData> Vessels);

public static class DissolutionCalculator
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

    public static decimal CalculateVesselPercent(
        decimal vesselArea,
        decimal standardMeanArea,
        decimal cs,
        decimal mediumVolumeMl,
        decimal dilutionFactor,
        decimal labelClaim)
    {
        if (vesselArea <= 0m)
            throw new InvalidOperationException("Vessel area must be greater than zero.");
        if (standardMeanArea <= 0m)
            throw new InvalidOperationException("Standard mean area must be greater than zero.");
        if (cs <= 0m)
            throw new InvalidOperationException("Standard concentration (C_s) must be greater than zero.");
        if (mediumVolumeMl <= 0m)
            throw new InvalidOperationException("Medium volume must be greater than zero.");
        if (dilutionFactor <= 0m)
            throw new InvalidOperationException("Dilution factor must be greater than zero.");
        if (labelClaim <= 0m)
            throw new InvalidOperationException("Label claim must be greater than zero.");

        // Multiply before divide: % = (A_u * C_s * V * DF * 100) / (A_s * LC)
        decimal numerator = vesselArea * cs * mediumVolumeMl * dilutionFactor * 100m;
        decimal denominator = standardMeanArea * labelClaim;
        return numerator / denominator;
    }

    public static string FormatReportedDisplay(decimal reportedValue)
    {
        decimal rounded = Math.Round(reportedValue, 0, MidpointRounding.AwayFromZero);
        return $"{rounded.ToString("0", CultureInfo.InvariantCulture)} %";
    }

    public static DissolutionCalculationResult Calculate(
        IReadOnlyList<(int Stage, int VesselIndex, decimal Area)> vesselInputs,
        DissolutionStandardData standardData,
        decimal mediumVolumeMl,
        decimal dilutionFactor,
        decimal labelClaim,
        decimal q,
        DissolutionOffsetsData offsets)
    {
        ArgumentNullException.ThrowIfNull(vesselInputs);
        ArgumentNullException.ThrowIfNull(standardData);
        ArgumentNullException.ThrowIfNull(offsets);

        var vessels = new List<DissolutionVesselData>(vesselInputs.Count);
        var vesselPercents = new List<decimal>(vesselInputs.Count);

        foreach (var input in vesselInputs)
        {
            decimal percent = CalculateVesselPercent(
                input.Area,
                standardData.StandardMeanArea,
                standardData.Cs,
                mediumVolumeMl,
                dilutionFactor,
                labelClaim);

            bool passed = input.Stage switch
            {
                1 => percent >= (q + offsets.S1Offset),
                2 => percent >= (q - offsets.S2MinOffset),
                3 => percent >= (q - offsets.S3MinOffset),
                _ => percent >= (q - offsets.S3MinOffset)
            };

            vessels.Add(new DissolutionVesselData(
                input.Stage,
                input.VesselIndex,
                input.Area,
                percent,
                passed));

            vesselPercents.Add(percent);
        }

        var eval = DissolutionStageEvaluator.Evaluate(
            vesselPercents,
            q,
            offsets.S1Offset,
            offsets.S2MinOffset,
            offsets.S3MinOffset,
            offsets.S3MaxBelowS2Min);

        string comparisonStatus = eval.Outcome switch
        {
            DissolutionStageOutcome.Complies => "WithinLimits",
            DissolutionStageOutcome.DoesNotComply => "OutOfSpecification",
            DissolutionStageOutcome.NextStageRequired => "NextStageRequired",
            _ => "OutOfSpecification"
        };

        string reportedDisplay = FormatReportedDisplay(eval.Mean);

        var calcData = new DissolutionCalculationData(
            Cs: standardData.Cs,
            Standard: standardData,
            V: mediumVolumeMl,
            Df: dilutionFactor,
            Lc: labelClaim,
            Q: q,
            Offsets: offsets,
            Vessels: vessels,
            Mean: eval.Mean,
            Outcome: eval.Outcome.ToString(),
            Reasons: eval.Reasons);

        string calculationJson = JsonSerializer.Serialize(calcData, JsonOptions);

        return new DissolutionCalculationResult(
            Mean: eval.Mean,
            ReportedValue: eval.Mean,
            ReportedDisplay: reportedDisplay,
            ComparisonStatus: comparisonStatus,
            StageReached: eval.StageReached,
            Outcome: eval.Outcome,
            Reasons: eval.Reasons,
            CalculationJson: calculationJson,
            Vessels: vessels);
    }
}
