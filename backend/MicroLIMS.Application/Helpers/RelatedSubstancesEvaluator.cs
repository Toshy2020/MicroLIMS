using System.Globalization;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Helpers;

public record RsReplicate(int ReplicateNo, decimal ReferenceArea, IReadOnlyList<HplcReplicatePeakInput> Peaks);

public record RelatedSubstancesInput(
    decimal ReferenceSolutionPercent, decimal DisregardLimitPercent,
    IReadOnlyList<HplcMethodImpurityResponse> Impurities, IReadOnlyList<RsReplicate> Replicates, IReadOnlyList<Specification> Specs);

// One counted peak behind a replicate value (Label is the impurity name, or "Unspecified RRT x").
public record RsContribution(string Label, decimal Area, decimal Percent);
public record RsReplicateDetail(int ReplicateNo, decimal ReferenceArea, decimal Percent, IReadOnlyList<RsContribution> Contributions);
public record RsRowResult(HplcAssayRow Row, RelatedSubstanceRow Kind, int? ImpurityId, IReadOnlyList<RsReplicateDetail> Replicates);

// Related substances of an HPLC method (spec 2026-10-27): each peak's % is its area against the
// reference solution's principal-peak area, times the reference solution's % of the test solution
// and the impurity's correction factor; peaks below the disregard limit are ignored. Pure - no I/O.
public static class RelatedSubstancesEvaluator
{
    public const string NotDetected = "Not detected";

    private sealed record Peak(int? ImpurityId, string Label, decimal Area, decimal Percent, bool Counted);

    public static List<HplcAssayRow> Evaluate(int principalAnalyteId, string principalName, RelatedSubstancesInput input) =>
        Calculate(principalAnalyteId, principalName, input).Select(r => r.Row).ToList();

    public static List<RsRowResult> Calculate(int principalAnalyteId, string principalName, RelatedSubstancesInput input)
    {
        var impurities = input.Impurities.OrderBy(i => i.DisplayOrder).ToList();
        var specs = input.Specs.Where(s => s.RelatedSubstanceRow.HasValue && (s.RelatedSubstanceRow != RelatedSubstanceRow.Named || s.HplcMethodImpurityId.HasValue)).ToList();
        var namedIds = specs.Where(s => s.RelatedSubstanceRow == RelatedSubstanceRow.Named).Select(s => s.HplcMethodImpurityId!.Value).ToHashSet();

        // Per replicate: every entered peak with its %, and whether it counts.
        var reps = input.Replicates.OrderBy(r => r.ReplicateNo).Select(rep =>
        {
            if (rep.ReferenceArea <= 0m)
                throw new InvalidOperationException($"{principalName}: the reference solution area is required.");
            var peaks = rep.Peaks.Select(p =>
            {
                var impurity = p.HplcMethodImpurityId is int id ? impurities.FirstOrDefault(i => i.Id == id) : null;
                var pct = p.Area * (impurity?.CorrectionFactor ?? 1m) / rep.ReferenceArea * input.ReferenceSolutionPercent;
                var label = impurity?.Name ?? (p.RelativeRetention is decimal rrt ? $"Unspecified RRT {rrt.ToString("0.####", CultureInfo.InvariantCulture)}" : "Unspecified");
                return new Peak(impurity?.Id, label, p.Area, pct, p.Area > 0m && pct >= input.DisregardLimitPercent);
            }).ToList();
            return (Rep: rep, Peaks: peaks);
        }).ToList();

        bool IsPool(Peak p) => p.ImpurityId is null || !namedIds.Contains(p.ImpurityId.Value);
        int ImpurityIndex(int? id) { var ix = impurities.FindIndex(i => i.Id == id); return ix < 0 ? int.MaxValue : ix; }

        var results = new List<RsRowResult>();
        foreach (var spec in specs.OrderBy(s => KindOrder(s.RelatedSubstanceRow!.Value))
                     .ThenBy(s => ImpurityIndex(s.HplcMethodImpurityId)))
        {
            var kind = spec.RelatedSubstanceRow!.Value;
            var details = new List<RsReplicateDetail>();
            var hasSignal = false;

            foreach (var (rep, peaks) in reps)
            {
                var counted = new List<RsContribution>();
                decimal value;
                switch (kind)
                {
                    case RelatedSubstanceRow.Named:
                    {
                        // Two peaks of one impurity are one amount.
                        var own = peaks.Where(p => p.ImpurityId == spec.HplcMethodImpurityId).ToList();
                        var pct = own.Sum(p => p.Percent);
                        hasSignal |= own.Any(p => p.Area > 0m);
                        value = own.Any(p => p.Area > 0m) && pct >= input.DisregardLimitPercent ? pct : 0m;
                        if (value > 0m) counted.AddRange(own.Where(p => p.Area > 0m).Select(Contribution));
                        break;
                    }
                    case RelatedSubstanceRow.UnspecifiedEach:
                    {
                        var pool = peaks.Where(IsPool).ToList();
                        hasSignal |= pool.Any(p => p.Area > 0m);
                        var top = pool.Where(p => p.Counted).OrderByDescending(p => p.Percent).FirstOrDefault();
                        value = top?.Percent ?? 0m;
                        if (top != null) counted.Add(Contribution(top));
                        break;
                    }
                    case RelatedSubstanceRow.TotalOthers:
                    {
                        var pool = peaks.Where(IsPool).ToList();
                        hasSignal |= pool.Any(p => p.Area > 0m);
                        var inPool = pool.Where(p => p.Counted).ToList();
                        value = inPool.Sum(p => p.Percent);
                        counted.AddRange(inPool.Select(Contribution));
                        break;
                    }
                    default: // Total
                    {
                        hasSignal |= peaks.Any(p => p.Area > 0m);
                        var named = impurities.Where(i => namedIds.Contains(i.Id)).Select(i => peaks.Where(p => p.ImpurityId == i.Id).ToList())
                            .Where(g => g.Any(p => p.Area > 0m) && g.Sum(p => p.Percent) >= input.DisregardLimitPercent).SelectMany(g => g).Where(p => p.Area > 0m).ToList();
                        var pool = peaks.Where(p => IsPool(p) && p.Counted).ToList();
                        value = named.Sum(p => p.Percent) + pool.Sum(p => p.Percent);
                        counted.AddRange(named.Concat(pool).Select(Contribution));
                        break;
                    }
                }
                details.Add(new RsReplicateDetail(rep.ReplicateNo, rep.ReferenceArea, value, counted));
            }

            var mean = details.Count == 0 ? 0m : details.Average(d => d.Percent);
            var rounded = Math.Round(mean, 2, MidpointRounding.AwayFromZero);
            var display = !hasSignal ? NotDetected
                : mean == 0m ? $"< {input.DisregardLimitPercent.ToString("0.###", CultureInfo.InvariantCulture)} %"
                : $"{rounded.ToString("0.00", CultureInfo.InvariantCulture)} %";

            var name = string.IsNullOrWhiteSpace(spec.ParameterName)
                ? SpecificationService.RelatedSubstanceParameterName(kind, impurities.FirstOrDefault(i => i.Id == spec.HplcMethodImpurityId)?.Name)
                : spec.ParameterName;
            var readings = details.Select(d => new HplcAssayReading(d.ReplicateNo, d.ReferenceArea, d.Percent)).ToList();
            var row = new HplcAssayRow(principalAnalyteId, name, spec, ResultBasis.PercentAsIs, null,
                rounded, display, "%", SpecificationEvaluator.Evaluate(spec, rounded), readings);
            results.Add(new RsRowResult(row, kind, spec.HplcMethodImpurityId, details));
        }
        return results;
    }

    private static RsContribution Contribution(Peak p) => new(p.Label, p.Area, p.Percent);

    // Named rows first, then each, others, total.
    private static int KindOrder(RelatedSubstanceRow kind) => kind switch
    {
        RelatedSubstanceRow.Named => 0,
        RelatedSubstanceRow.UnspecifiedEach => 1,
        RelatedSubstanceRow.TotalOthers => 2,
        _ => 3,
    };
}
