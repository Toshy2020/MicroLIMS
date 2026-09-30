using System.Globalization;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Services;

namespace MicroLIMS.Application.Helpers;

public record HplcAssayAnalyteInput(
    int AnalyteId, string Name, decimal ThWtStdMg, decimal ThWtTestMg, AssayStandard Std,
    IReadOnlyList<AssayReplicateInput> Reps, Specification AssaySpec, Specification? AmountSpec);

public record HplcAssayReading(int ReplicateNo, decimal Response, decimal AssayPercent);

// One judged value: a reported assay % (mean, or one replicate on the
// individual basis) or an amount per unit.
public record HplcAssayRow(
    int AnalyteId, string AnalyteName, Specification Spec, ResultBasis Basis, int? ReplicateNo,
    decimal Value, string Display, string Unit, ResultStatus Status,
    IReadOnlyList<HplcAssayReading> Readings);

// Turns entered replicates into judged rows, exactly as they are stored on
// submission and shown as a preview while entering (HPLC chain S6, D2/D3).
// Pure - no I/O.
public static class HplcSampleAssayEvaluator
{
    public static List<HplcAssayRow> Evaluate(IReadOnlyList<HplcAssayAnalyteInput> analytes, ProductionStageRole? stageRole)
    {
        var rows = new List<HplcAssayRow>();
        foreach (var a in analytes)
        {
            var calc = HplcAssayCalculator.Calculate(a.AnalyteId, a.Name, a.ThWtStdMg, a.ThWtTestMg, a.Std, a.Reps);

            var readings = calc.Replicates
                .Select(r => new HplcAssayReading(r.ReplicateNo, a.Reps.First(x => x.ReplicateNo == r.ReplicateNo).Response, r.AssayPercent))
                .ToList();

            if (HplcAssayCalculator.IsIndividualBasis(stageRole))
            {
                foreach (var r in calc.Replicates)
                    rows.Add(new HplcAssayRow(a.AnalyteId, $"{a.Name} – replicate {r.ReplicateNo}", a.AssaySpec,
                        ResultBasis.PercentLabelClaim, r.ReplicateNo, r.AssayPercent, Percent(r.AssayPercent), "%",
                        SpecificationEvaluator.Evaluate(a.AssaySpec, r.AssayPercent),
                        readings.Where(x => x.ReplicateNo == r.ReplicateNo).ToList()));
                continue;
            }

            var mean = calc.MeanAssayPercent!.Value;
            rows.Add(new HplcAssayRow(a.AnalyteId, a.Name, a.AssaySpec, ResultBasis.PercentLabelClaim, null,
                mean, Percent(mean), "%", SpecificationEvaluator.Evaluate(a.AssaySpec, mean), readings));

            if (HplcAssayCalculator.JudgesAmountPerUnit(stageRole) && a.AmountSpec?.LabelClaim is decimal claim)
            {
                var amount = HplcAssayCalculator.AmountPerUnit(mean, claim);
                var unit = a.AmountSpec.LabelClaimUnit ?? string.Empty;
                rows.Add(new HplcAssayRow(a.AnalyteId, $"{a.Name} (amount per unit)", a.AmountSpec, ResultBasis.MgPerUnit, null,
                    amount, $"{amount.ToString("0.00", CultureInfo.InvariantCulture)} {unit}".Trim(), unit,
                    SpecificationEvaluator.Evaluate(a.AmountSpec, amount), Array.Empty<HplcAssayReading>()));
            }
        }
        return rows;
    }

    private static string Percent(decimal v) =>
        $"{Math.Round(v, 1, MidpointRounding.AwayFromZero).ToString("0.0", CultureInfo.InvariantCulture)} %";
}
