using MicroLIMS.Shared.Exceptions;
using System.Globalization;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Helpers;

public record CleaningResidueReplicateInput(int ReplicateNo, decimal Response);
public record CleaningResidueReplicateResult(int ReplicateNo, decimal Value);
public record CleaningResidueResult(IReadOnlyList<CleaningResidueReplicateResult> Replicates, decimal MeanValue);

public record HplcCleaningResidueInput(
    int AnalyteId,
    string Name,
    decimal StandardConcentrationUgPerMl,
    decimal SampleSolutionVolumeMl,
    decimal? SwabAreaCm2,
    decimal MacoLimit,
    MacoUnit MacoUnit,
    decimal StandardMeanResponse,
    IReadOnlyList<CleaningResidueReplicateInput> Reps,
    Specification Spec);

// After-cleaning HPLC cleaning residue (spec 2026-11-07). Pure - no I/O.
// µg/mL = sample response ÷ mean standard response × standard concentration (µg/mL)
// µg/swab = µg/mL × extraction volume (mL)
// µg/cm² = µg/swab ÷ swabbed area (cm²)
public static class CleaningResidueCalculator
{
    public const string NotDetected = "Not detected";

    public static bool IsNotDetected(HplcCleaningResidueInput input) => input.Reps.All(r => r.Response == 0m);

    public static CleaningResidueResult Calculate(
        decimal standardConcentrationUgPerMl,
        decimal sampleSolutionVolumeMl,
        decimal? swabAreaCm2,
        MacoUnit macoUnit,
        decimal standardMeanResponse,
        IReadOnlyList<CleaningResidueReplicateInput> reps)
    {
        if (standardConcentrationUgPerMl <= 0m)
            throw new BusinessRuleException("The standard concentration must be greater than zero.");
        if (sampleSolutionVolumeMl <= 0m)
            throw new BusinessRuleException("The extraction volume must be greater than zero.");
        if (standardMeanResponse <= 0m)
            throw new BusinessRuleException("The standard mean response must be greater than zero.");
        if (macoUnit == MacoUnit.UgPerCm2 && swabAreaCm2 is not > 0m)
            throw new BusinessRuleException("Swabbed area (cm²) must be greater than zero when unit is µg/cm².");
        if (reps.Count == 0)
            throw new BusinessRuleException("At least one replicate is required.");

        var results = reps.Select(rep =>
        {
            if (rep.Response < 0m)
                throw new BusinessRuleException($"Replicate {rep.ReplicateNo}: the response must be zero or more.");

            var ugPerMl = (rep.Response / standardMeanResponse) * standardConcentrationUgPerMl;
            var ugPerSwab = ugPerMl * sampleSolutionVolumeMl;
            var val = macoUnit == MacoUnit.UgPerCm2
                ? ugPerSwab / swabAreaCm2!.Value
                : ugPerSwab;

            return new CleaningResidueReplicateResult(rep.ReplicateNo, val);
        }).ToList();

        var mean = results.Average(r => r.Value);
        return new CleaningResidueResult(results, mean);
    }

    public static string FormatValue(decimal v, string unitText) =>
        $"{Math.Round(v, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture)} {unitText}";

    public static string FormatValue(decimal v, MacoUnit unit) =>
        FormatValue(v, UnitText(unit));

    public static string UnitText(MacoUnit unit) => unit switch
    {
        MacoUnit.UgPerSwab => "µg/swab",
        MacoUnit.UgPerCm2 => "µg/cm²",
        _ => unit.ToString()
    };

    public static List<HplcAssayRow> Evaluate(IReadOnlyList<HplcCleaningResidueInput> inputs) =>
        inputs.Select(input =>
        {
            var calc = Calculate(
                input.StandardConcentrationUgPerMl,
                input.SampleSolutionVolumeMl,
                input.SwabAreaCm2,
                input.MacoUnit,
                input.StandardMeanResponse,
                input.Reps);

            var readings = calc.Replicates
                .Select(r => new HplcAssayReading(r.ReplicateNo, input.Reps.First(x => x.ReplicateNo == r.ReplicateNo).Response, r.Value))
                .ToList();

            var unitText = UnitText(input.MacoUnit);
            var display = IsNotDetected(input) ? NotDetected : FormatValue(calc.MeanValue, unitText);

            return new HplcAssayRow(
                input.AnalyteId,
                input.Name,
                input.Spec,
                ResultBasis.PercentLabelClaim,
                null,
                calc.MeanValue,
                display,
                unitText,
                SpecificationEvaluator.Evaluate(input.Spec, calc.MeanValue),
                readings);
        }).ToList();
}
