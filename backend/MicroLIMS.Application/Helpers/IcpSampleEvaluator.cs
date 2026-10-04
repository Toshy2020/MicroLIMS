using System.Globalization;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Helpers;

// One judged value of an ICP sample: a spec row (MgPerKg / MgPerUnit / PercentLabelClaim) of one element.
// Value and Status are null when there is nothing to judge; Problem then says why.
public record IcpSampleRow(
    Specification Spec, int ElementId, string Symbol, ResultBasis Basis,
    decimal? Value, string Display, string Unit, ResultStatus? Status, string? Problem);

// Turns entered replicates into judged rows, exactly as shown in the preview and (Task 7) stored on submission.
// Pure - no I/O.
public static class IcpSampleEvaluator
{
    public static List<IcpSampleRow> Evaluate(
        IcpMethodResponse snapshot, IReadOnlyList<IcpElementStateDto> states, IReadOnlyList<Specification> specs,
        IReadOnlyList<IcpSampleReplicate> replicates, decimal? unitAmount)
    {
        var levels = CalibrationStandardLevelsHelper.ParseAndValidate(snapshot.StandardLevelsMgPerL).Levels;
        var lowest = levels.Min();
        var highest = levels.Max();
        var rows = new List<IcpSampleRow>();

        foreach (var spec in specs.Where(s => s.IcpMethodElementId != null).OrderBy(s => s.DisplayOrder).ThenBy(s => s.Id))
        {
            var element = snapshot.Elements.FirstOrDefault(e => e.Id == spec.IcpMethodElementId);
            if (element == null) continue;
            var basis = spec.ResultBasis ?? ResultBasis.MgPerKg;

            IcpSampleRow Problem(string text) => new(spec, element.Id, element.Symbol, basis, null, string.Empty, UnitOf(spec, basis), null, text);

            var state = states.FirstOrDefault(s => s.IcpMethodElementId == element.Id);
            if (state is { Valid: false })
                { rows.Add(Problem($"{element.Symbol}: {state.Reason}")); continue; }

            var inputs = new List<IcpReplicateInput>();
            foreach (var rep in replicates.OrderBy(r => r.ReplicateNo))
            {
                var conc = rep.Concentrations.FirstOrDefault(c => c.IcpMethodElementId == element.Id);
                if (conc == null) break;
                inputs.Add(new IcpReplicateInput(rep.ReplicateNo, rep.SampleAmount, rep.VolumeMl, rep.DilutionFactor, conc.SolutionMgPerL));
            }
            if (inputs.Count == 0 || inputs.Count != replicates.Count)
                { rows.Add(Problem($"{element.Symbol}: enter the replicates.")); continue; }

            var needsUnit = basis is ResultBasis.MgPerUnit or ResultBasis.PercentLabelClaim;
            if (needsUnit && unitAmount is not > 0m)
                { rows.Add(Problem("Enter the unit amount (average unit weight or dose).")); continue; }
            if (basis == ResultBasis.PercentLabelClaim && spec.LabelClaim is not > 0m)
                { rows.Add(Problem($"{element.Symbol}: the specification has no label claim.")); continue; }

            var calc = IcpContentCalculator.Calculate(inputs, lowest, highest, element.ConversionFactor, unitAmount, spec.LabelClaim);

            var over = calc.Replicates.FirstOrDefault(r => r.OverRange);
            if (over != null)
                { rows.Add(Problem($"{element.Symbol}: replicate {over.ReplicateNo} is above the top standard - dilute and re-measure.")); continue; }

            var unit = UnitOf(spec, basis);
            if (calc.AllBelowLoq)
            {
                var status = spec.LimitType == LimitType.NotMoreThan ? ResultStatus.WithinLimits : ResultStatus.RequiresReview;
                rows.Add(new IcpSampleRow(spec, element.Id, element.Symbol, basis, null, "<LOQ", unit, status, null));
                continue;
            }

            var value = basis switch
            {
                ResultBasis.MgPerUnit => calc.MgPerUnit!.Value,
                ResultBasis.PercentLabelClaim => calc.PercentLabelClaim!.Value,
                _ => calc.MeanContentPerAmount!.Value,
            };
            var display = Format(value, basis, unit);
            var judged = SpecificationEvaluator.Evaluate(spec, value);
            if (calc.SomeBelowLoq)
                { display += " (some replicates <LOQ)"; judged = ResultStatus.RequiresReview; }
            rows.Add(new IcpSampleRow(spec, element.Id, element.Symbol, basis, value, display, unit, judged, null));
        }
        return rows;
    }

    private static string UnitOf(Specification spec, ResultBasis basis) => basis switch
    {
        ResultBasis.MgPerUnit => spec.LabelClaimUnit ?? string.Empty,
        ResultBasis.PercentLabelClaim => "%",
        _ => "µg/g",
    };

    private static string Format(decimal v, ResultBasis basis, string unit)
    {
        var (places, pattern) = basis switch
        {
            ResultBasis.MgPerUnit => (3, "0.000"),
            ResultBasis.PercentLabelClaim => (1, "0.0"),
            _ => (2, "0.00"),
        };
        var text = Math.Round(v, places, MidpointRounding.AwayFromZero).ToString(pattern, CultureInfo.InvariantCulture);
        return $"{text} {unit}".Trim();
    }
}
