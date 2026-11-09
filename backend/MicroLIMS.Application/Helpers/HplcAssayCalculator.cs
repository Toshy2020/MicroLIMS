using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Helpers;

public record AssayReplicateInput(int ReplicateNo, decimal ActualWeightMg, decimal Response);
public record AssayStandard(decimal MeanResponse, decimal StandardWeightMg, decimal PurityPercent, decimal MoisturePercent);
public record AssayReplicateResult(int ReplicateNo, decimal AssayPercent);
public record AssayAnalyteResult(
    int HplcMethodAnalyteId,
    string AnalyteName,
    IReadOnlyList<AssayReplicateResult> Replicates,
    decimal? MeanAssayPercent);

// HPLC sample assay calculator. Reuses StandardComparisonCalculator.CalculatePreparationAssay for the
// per-replicate % assay formula - pure function, no I/O.
public static class HplcAssayCalculator
{
    public static AssayAnalyteResult Calculate(
        int analyteId,
        string name,
        decimal thWtStdMg,
        decimal thWtTestMg,
        AssayStandard std,
        IReadOnlyList<AssayReplicateInput> reps)
    {
        ArgumentNullException.ThrowIfNull(std);
        ArgumentNullException.ThrowIfNull(reps);
        if (reps.Count == 0)
            throw new BusinessRuleException("At least one replicate is required.");

        var results = new List<AssayReplicateResult>();
        foreach (var rep in reps)
        {
            decimal assayPercent = StandardComparisonCalculator.CalculatePreparationAssay(
                rep.Response,
                std.MeanResponse,
                std.StandardWeightMg,
                thWtStdMg,
                thWtTestMg,
                rep.ActualWeightMg,
                std.MoisturePercent,
                std.PurityPercent);

            results.Add(new AssayReplicateResult(rep.ReplicateNo, assayPercent));
        }

        decimal? mean = results.Count > 0 ? results.Average(r => r.AssayPercent) : null;

        return new AssayAnalyteResult(analyteId, name, results, mean);
    }

    public static decimal AmountPerUnit(decimal assayPercent, decimal labelClaim) => assayPercent * labelClaim / 100m;

    public static decimal ApplyBasis(decimal assayPercent, ResultBasis? basis, decimal? moisturePercent)
    {
        if (basis is ResultBasis.PercentDriedBasis or ResultBasis.PercentAnhydrousBasis)
        {
            if (!moisturePercent.HasValue || moisturePercent < 0m || moisturePercent >= 100m)
                throw new BusinessRuleException("Loss on drying / water % (0 to below 100) is required for a dried or anhydrous basis.");
            return Math.Round(assayPercent * 100m / (100m - moisturePercent.Value), 2, MidpointRounding.AwayFromZero);
        }
        return assayPercent;
    }

    public static bool IsIndividualBasis(ProductionStageRole? role) => role == ProductionStageRole.Bulk;

    public static bool JudgesAmountPerUnit(ProductionStageRole? role) => role == ProductionStageRole.Finished;
}
