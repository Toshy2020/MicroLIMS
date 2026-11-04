using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Related-substances calculation (Plan 3 task 3): pure, no I/O.
public class RelatedSubstancesEvaluatorTests
{
    private const int D = 1, A = 2, C = 3;

    private static Specification Row(RelatedSubstanceRow kind, decimal limit, int? impurityId = null, string name = "") => new()
    {
        Id = 100 + (int)kind * 10 + (impurityId ?? 0), RelatedSubstanceRow = kind, HplcMethodImpurityId = impurityId, ParameterName = name,
        LimitType = LimitType.NotMoreThan, UpperLimit = limit, UpperInclusive = true, ResultBasis = ResultBasis.PercentAsIs, Unit = "%",
    };

    private static HplcMethodImpurityResponse Imp(int id, string name, decimal cf) => new(id, id, name, null, cf);
    private static HplcReplicatePeakInput Peak(int? impurityId, decimal area, decimal? rrt = null) => new(impurityId, rrt, area);
    private static RsReplicate Rep(int no, decimal refArea, params HplcReplicatePeakInput[] peaks) => new(no, refArea, peaks);

    private static List<HplcAssayRow> Run(decimal refPct, decimal? disregard, IReadOnlyList<HplcMethodImpurityResponse> impurities,
        IReadOnlyList<RsReplicate> reps, params Specification[] specs) =>
        RelatedSubstancesEvaluator.Evaluate(7, "Sildenafil", new RelatedSubstancesInput(refPct, disregard, impurities, reps, specs));

    private static HplcAssayRow RowFor(List<HplcAssayRow> rows, RelatedSubstanceRow kind, int? impurityId = null) =>
        rows.Single(r => r.Spec.RelatedSubstanceRow == kind && r.Spec.HplcMethodImpurityId == impurityId);

    [Fact]
    public void Sildenafil_Example()
    {
        var rows = Run(0.1m, 0.05m, new[] { Imp(D, "Impurity D", 0.7m), Imp(A, "Impurity A", 1m) },
            new[] { Rep(1, 10000m, Peak(D, 12000m), Peak(A, 9000m), Peak(null, 6000m, 0.85m), Peak(null, 3000m, 1.4m)) },
            Row(RelatedSubstanceRow.Named, 0.15m, D, "Impurity D"), Row(RelatedSubstanceRow.Named, 0.15m, A, "Impurity A"),
            Row(RelatedSubstanceRow.UnspecifiedEach, 0.10m), Row(RelatedSubstanceRow.Total, 0.5m));

        Assert.Equal(4, rows.Count);
        // Named rows first (impurity order), then unspecified-each, total-of-others, total.
        Assert.Equal(new[] { RelatedSubstanceRow.Named, RelatedSubstanceRow.Named, RelatedSubstanceRow.UnspecifiedEach, RelatedSubstanceRow.Total },
            rows.Select(r => r.Spec.RelatedSubstanceRow!.Value).ToArray());
        Assert.All(rows, r =>
        {
            Assert.Equal(ResultBasis.PercentAsIs, r.Basis);
            Assert.Null(r.ReplicateNo);
            Assert.Equal(7, r.AnalyteId);
            Assert.Equal("%", r.Unit);
            Assert.Equal(ResultStatus.WithinLimits, r.Status);
        });

        var d = RowFor(rows, RelatedSubstanceRow.Named, D);
        Assert.Equal(0.08m, d.Value);
        Assert.Equal("0.08 %", d.Display);
        Assert.Equal("Impurity D", d.AnalyteName);
        Assert.Equal("0.09 %", RowFor(rows, RelatedSubstanceRow.Named, A).Display);
        var each = RowFor(rows, RelatedSubstanceRow.UnspecifiedEach);
        Assert.Equal("0.06 %", each.Display);
        var total = RowFor(rows, RelatedSubstanceRow.Total);
        Assert.Equal(0.23m, total.Value);
        Assert.Equal("0.23 %", total.Display);
    }

    [Fact]
    public void Paraben_Example()
    {
        var rows = Run(0.5m, 0.1m, new[] { Imp(A, "Impurity A", 1.4m), Imp(C, "Impurity C", 1m) },
            new[] { Rep(1, 20000m, Peak(A, 50000m), Peak(C, 4000m), Peak(null, 2000m)) },
            Row(RelatedSubstanceRow.Named, 3.0m, A, "Impurity A"), Row(RelatedSubstanceRow.UnspecifiedEach, 0.10m), Row(RelatedSubstanceRow.TotalOthers, 0.2m));

        var a = RowFor(rows, RelatedSubstanceRow.Named, A);
        Assert.Equal(1.75m, a.Value);
        Assert.Equal("1.75 %", a.Display);
        Assert.Equal(ResultStatus.WithinLimits, a.Status);
        // C has no row of its own: counted as an unspecified peak (0.10 = the disregard limit, so counted); 2000 -> 0.05 is ignored.
        var each = RowFor(rows, RelatedSubstanceRow.UnspecifiedEach);
        Assert.Equal("0.10 %", each.Display);
        Assert.Equal(ResultStatus.WithinLimits, each.Status);
        Assert.Equal("0.10 %", RowFor(rows, RelatedSubstanceRow.TotalOthers).Display);
    }

    [Fact]
    public void NoPeaks_AllNotDetected()
    {
        var rows = Run(0.1m, 0.05m, new[] { Imp(D, "Impurity D", 0.7m) },
            new[] { Rep(1, 10000m, Peak(D, 0m)), Rep(2, 10000m) },
            Row(RelatedSubstanceRow.Named, 0.15m, D, "Impurity D"), Row(RelatedSubstanceRow.UnspecifiedEach, 0.10m),
            Row(RelatedSubstanceRow.TotalOthers, 0.2m), Row(RelatedSubstanceRow.Total, 0.5m));

        Assert.Equal(4, rows.Count);
        Assert.All(rows, r =>
        {
            Assert.Equal("Not detected", r.Display);
            Assert.Equal(0m, r.Value);
            Assert.Equal(ResultStatus.WithinLimits, r.Status);
        });
    }

    [Fact]
    public void BelowDisregard_ShowsLessThan()
    {
        var rows = Run(0.1m, 0.05m, new[] { Imp(D, "Impurity D", 1m) },
            new[] { Rep(1, 10000m, Peak(D, 500m)) },
            Row(RelatedSubstanceRow.Named, 0.15m, D, "Impurity D"), Row(RelatedSubstanceRow.Total, 0.5m));

        var d = RowFor(rows, RelatedSubstanceRow.Named, D);
        Assert.Equal("< 0.05 %", d.Display);
        Assert.Equal(0m, d.Value);
        Assert.Equal("< 0.05 %", RowFor(rows, RelatedSubstanceRow.Total).Display);
    }

    [Fact]
    public void TwoReplicates_MeanOfReplicatePercents()
    {
        // D: 0.084 and 0.14 -> mean 0.112 -> 0.11. Unspecified: 0.06 and none -> 0.03.
        var rows = Run(0.1m, 0.05m, new[] { Imp(D, "Impurity D", 0.7m) },
            new[] { Rep(1, 10000m, Peak(D, 12000m), Peak(null, 6000m)), Rep(2, 10000m, Peak(D, 20000m)) },
            Row(RelatedSubstanceRow.Named, 0.15m, D, "Impurity D"), Row(RelatedSubstanceRow.UnspecifiedEach, 0.10m), Row(RelatedSubstanceRow.Total, 0.5m));

        Assert.Equal(0.11m, RowFor(rows, RelatedSubstanceRow.Named, D).Value);
        Assert.Equal("< 0.05 %", RowFor(rows, RelatedSubstanceRow.UnspecifiedEach).Display);
        Assert.Equal(0m, RowFor(rows, RelatedSubstanceRow.UnspecifiedEach).Value);
        // Total: (0.084 + 0.06) = 0.144 and 0.14 -> mean 0.142 -> 0.14.
        Assert.Equal("0.14 %", RowFor(rows, RelatedSubstanceRow.Total).Display);
    }

    [Fact]
    public void FinalMeanBelowDisregard_DisplaysLessThanDisregard_WithValueZero()
    {
        // Replicate 1 is 0.08% (counted) and replicate 2 is 0%, mean is 0.04% with disregard 0.05%.
        // The final mean is below the disregard limit: display "< 0.05 %" with value 0.
        var rows = Run(0.1m, 0.05m, new[] { Imp(D, "Impurity D", 1m) },
            new[] { Rep(1, 10000m, Peak(D, 8000m)), Rep(2, 10000m) },
            Row(RelatedSubstanceRow.Named, 0.15m, D, "Impurity D"));

        var d = Assert.Single(rows);
        Assert.Equal("< 0.05 %", d.Display);
        Assert.Equal(0m, d.Value);
        Assert.Equal(ResultStatus.WithinLimits, d.Status);
    }

    [Fact]
    public void DisregardLimitZero_RoundsToZeroWhileRawAboveZero_DisplaysLessThan001_WithValueZero()
    {
        // Disregard is 0. Raw value is 0.004% which rounds to 0.00%.
        // Should display "< 0.01 %" with value 0.
        var rows = Run(0.1m, 0m, new[] { Imp(D, "Impurity D", 1m) },
            new[] { Rep(1, 10000m, Peak(D, 400m)) },
            Row(RelatedSubstanceRow.Named, 0.15m, D, "Impurity D"));

        var d = Assert.Single(rows);
        Assert.Equal("< 0.01 %", d.Display);
        Assert.Equal(0m, d.Value);
        Assert.Equal(ResultStatus.WithinLimits, d.Status);
    }

    [Fact]
    public void TotalAndTotalOthers_AllDisregarded_DisplaysLessThanDisregard()
    {
        // Peak area > 0 entered for an unspecified peak (4000 area -> 0.04% < disregard 0.05%).
        // Both Total and TotalOthers final value is 0 because the peak was disregarded.
        // Because peak area > 0 was entered, they display "< 0.05 %", not "Not detected".
        var rows = Run(0.1m, 0.05m, Array.Empty<HplcMethodImpurityResponse>(),
            new[] { Rep(1, 10000m, Peak(null, 4000m, 1.2m)) },
            Row(RelatedSubstanceRow.TotalOthers, 0.20m), Row(RelatedSubstanceRow.Total, 0.50m));

        var others = RowFor(rows, RelatedSubstanceRow.TotalOthers);
        var total = RowFor(rows, RelatedSubstanceRow.Total);

        Assert.Equal("< 0.05 %", others.Display);
        Assert.Equal(0m, others.Value);
        Assert.Equal("< 0.05 %", total.Display);
        Assert.Equal(0m, total.Value);
    }

    [Fact]
    public void TotalAndTotalOthers_NoPeaksEnteredAtAll_DisplaysNotDetected()
    {
        // No peak area > 0 entered at all. Displays "Not detected".
        var rows = Run(0.1m, 0.05m, Array.Empty<HplcMethodImpurityResponse>(),
            new[] { Rep(1, 10000m) },
            Row(RelatedSubstanceRow.TotalOthers, 0.20m), Row(RelatedSubstanceRow.Total, 0.50m));

        var others = RowFor(rows, RelatedSubstanceRow.TotalOthers);
        var total = RowFor(rows, RelatedSubstanceRow.Total);

        Assert.Equal("Not detected", others.Display);
        Assert.Equal(0m, others.Value);
        Assert.Equal("Not detected", total.Display);
        Assert.Equal(0m, total.Value);
    }

    [Fact]
    public void OOS_WhenNamedAboveLimit()
    {
        var rows = Run(0.1m, 0.05m, new[] { Imp(A, "Impurity A", 1m) },
            new[] { Rep(1, 10000m, Peak(A, 20000m)) },
            Row(RelatedSubstanceRow.Named, 0.15m, A, "Impurity A"));

        var a = Assert.Single(rows);
        Assert.Equal("0.20 %", a.Display);
        Assert.Equal(ResultStatus.OutOfSpecification, a.Status);
    }

    [Fact]
    public void MissingKinds_StillEvaluatesTheRowsPresent_AndNamedWithoutRowIsInThePool()
    {
        // Only a Total row; impurity D has no Named row, so it counts as an unspecified peak (CF applied).
        var rows = Run(0.1m, 0.05m, new[] { Imp(D, "Impurity D", 0.7m) },
            new[] { Rep(1, 10000m, Peak(D, 12000m), Peak(null, 6000m)) },
            Row(RelatedSubstanceRow.Total, 0.5m));

        var total = Assert.Single(rows);
        Assert.Equal("0.14 %", total.Display); // 0.084 + 0.06 = 0.144
    }

    [Fact]
    public void BlankParameterName_DefaultsToTheKindLabel()
    {
        var rows = Run(0.1m, 0.05m, new[] { Imp(D, "Impurity D", 1m) },
            new[] { Rep(1, 10000m, Peak(D, 6000m)) },
            Row(RelatedSubstanceRow.Named, 0.15m, D), Row(RelatedSubstanceRow.UnspecifiedEach, 0.10m),
            Row(RelatedSubstanceRow.Total, 0.5m), Row(RelatedSubstanceRow.TotalOthers, 0.2m));

        Assert.Equal(new[] { "Impurity D", "Any unspecified impurity", "Total of others", "Total impurities" },
            rows.Select(r => r.AnalyteName).ToArray());
    }

    [Fact]
    public void TwoPeaksOfOneNamedImpurity_AreSummed()
    {
        // 3000 + 3000 at CF 1 -> 0.06 (each alone is 0.03, below the disregard limit).
        var rows = Run(0.1m, 0.05m, new[] { Imp(D, "Impurity D", 1m) },
            new[] { Rep(1, 10000m, Peak(D, 3000m), Peak(D, 3000m)) },
            Row(RelatedSubstanceRow.Named, 0.15m, D, "Impurity D"));

        Assert.Equal("0.06 %", Assert.Single(rows).Display);
    }

    [Fact]
    public void MidpointRounding_MeanOf0125_RoundsTo013()
    {
        // Midpoint rounding: a mean of 0.125 gives "0.13 %".
        var rows = Run(0.1m, 0.05m, new[] { Imp(D, "Impurity D", 1m) },
            new[] { Rep(1, 10000m, Peak(D, 12500m)) },
            Row(RelatedSubstanceRow.Named, 0.15m, D, "Impurity D"));

        var d = Assert.Single(rows);
        Assert.Equal(0.13m, d.Value);
        Assert.Equal("0.13 %", d.Display);
    }

    [Fact]
    public void NamedPeak_ExactlyAtDisregardLimit_Counts_AndJustBelow_ShowsLessThan()
    {
        // A Named peak exactly at the disregard limit counts, and one just below it shows "< x %".
        // Ref area 10000, ref % 0.1, disregard 0.05:
        // Area 5000 -> 0.05% (counted). Area 4990 -> 0.0499% (disregarded).
        var rowsAt = Run(0.1m, 0.05m, new[] { Imp(D, "Impurity D", 1m) },
            new[] { Rep(1, 10000m, Peak(D, 5000m)) },
            Row(RelatedSubstanceRow.Named, 0.15m, D, "Impurity D"));
        var dAt = Assert.Single(rowsAt);
        Assert.Equal(0.05m, dAt.Value);
        Assert.Equal("0.05 %", dAt.Display);

        var rowsBelow = Run(0.1m, 0.05m, new[] { Imp(D, "Impurity D", 1m) },
            new[] { Rep(1, 10000m, Peak(D, 4990m)) },
            Row(RelatedSubstanceRow.Named, 0.15m, D, "Impurity D"));
        var dBelow = Assert.Single(rowsBelow);
        Assert.Equal(0m, dBelow.Value);
        Assert.Equal("< 0.05 %", dBelow.Display);
    }

    [Fact]
    public void PeakWithAreaZero_NextToRealPeak_CalculatesCorrectly()
    {
        // A peak with area 0 next to a real peak.
        var rows = Run(0.1m, 0.05m, new[] { Imp(D, "Impurity D", 1m) },
            new[] { Rep(1, 10000m, Peak(D, 0m), Peak(D, 8000m)) },
            Row(RelatedSubstanceRow.Named, 0.15m, D, "Impurity D"), Row(RelatedSubstanceRow.Total, 0.5m));

        var d = RowFor(rows, RelatedSubstanceRow.Named, D);
        Assert.Equal(0.08m, d.Value);
        Assert.Equal("0.08 %", d.Display);

        var total = RowFor(rows, RelatedSubstanceRow.Total);
        Assert.Equal(0.08m, total.Value);
        Assert.Equal("0.08 %", total.Display);
    }

    [Fact]
    public void NullDisregardLimit_TreatedAsNoDisregard_AndRoundsToZeroShowsLessThan001()
    {
        // A null disregard limit means "no disregard limit".
        // Peak of 0.004% rounds to 0.00% -> "< 0.01 %" with value 0.
        var rowsSmall = Run(0.1m, null, new[] { Imp(D, "Impurity D", 1m) },
            new[] { Rep(1, 10000m, Peak(D, 400m)) },
            Row(RelatedSubstanceRow.Named, 0.15m, D, "Impurity D"));
        var dSmall = Assert.Single(rowsSmall);
        Assert.Equal(0m, dSmall.Value);
        Assert.Equal("< 0.01 %", dSmall.Display);

        // Peak of 0.08% is counted.
        var rowsNormal = Run(0.1m, null, new[] { Imp(D, "Impurity D", 1m) },
            new[] { Rep(1, 10000m, Peak(D, 8000m)) },
            Row(RelatedSubstanceRow.Named, 0.15m, D, "Impurity D"));
        var dNormal = Assert.Single(rowsNormal);
        Assert.Equal(0.08m, dNormal.Value);
        Assert.Equal("0.08 %", dNormal.Display);
    }
}
