using System.Globalization;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Helpers;

// One judged value of an ICP sample: a spec row of one element. Value and Status are null when there is
// nothing to judge; Problem then says why.
public record IcpSampleRow(
    Specification Spec, int ElementId, string Symbol, IcpTestMode Mode,
    decimal? Value, string Display, string Unit, ResultStatus? Status, string? Problem);

// Turns the reported concentrations into judged rows, exactly as shown in the preview and stored on
// submission. Assay: % of the test's target, judged by the item's range. Impurity: the ppm itself, judged
// by the item's not-more-than limit (0 = not detected). Pure - no I/O.
public static class IcpSampleEvaluator
{
    public static List<IcpSampleRow> Evaluate(
        TestDefinition definition, IReadOnlyList<IcpElementStateDto> states, IReadOnlyList<Specification> specs,
        IReadOnlyList<IcpSampleConcentration> concentrations, decimal? actualWeightG)
    {
        var mode = definition.IcpMode ?? IcpTestMode.Assay;
        var rows = new List<IcpSampleRow>();

        foreach (var spec in specs.OrderBy(s => s.DisplayOrder).ThenBy(s => s.Id))
        {
            // Callers gate on IcpSampleEntryContext.EntryProblem, which rejects unlinked or foreign-element specs.
            var element = definition.IcpElements.FirstOrDefault(e => e.Id == spec.IcpTestElementId)
                ?? throw new InvalidOperationException($"Specification \"{spec.ParameterName}\" is not linked to an element of this test.");
            var unit = mode == IcpTestMode.Assay ? "%" : "ppm";

            IcpSampleRow Problem(string text) => new(spec, element.Id, element.Symbol, mode, null, string.Empty, unit, null, text);

            var state = states.FirstOrDefault(s => s.IcpTestElementId == element.Id);
            if (state is { Valid: false })
                { rows.Add(Problem($"{element.Symbol}: {state.Reason}")); continue; }

            var conc = concentrations.FirstOrDefault(c => c.IcpTestElementId == element.Id);
            if (conc == null)
                { rows.Add(Problem($"{element.Symbol}: enter the result from the instrument report.")); continue; }

            decimal value;
            if (mode == IcpTestMode.Assay)
            {
                if (actualWeightG is not > 0m)
                    { rows.Add(Problem("Enter the actual weight of the sample.")); continue; }
                if (element.TargetPpm is not > 0m || definition.IcpTheoreticalAmount is not > 0m)
                    { rows.Add(Problem($"{element.Symbol}: the test has no target concentration or theoretical amount.")); continue; }
                value = IcpContentCalculator.AssayPercent(
                    conc.ResultPpm, element.TargetPpm.Value, definition.IcpTheoreticalAmount.Value,
                    definition.IcpSampleForm == IcpSampleForm.Liquid ? definition.IcpSpecificGravity : null, actualWeightG.Value);
            }
            else
                value = conc.ResultPpm;

            var judged = SpecificationEvaluator.Evaluate(spec, value);
            rows.Add(new IcpSampleRow(spec, element.Id, element.Symbol, mode, value, Format(value, mode), unit, judged, null));
        }
        return rows;
    }

    private static string Format(decimal v, IcpTestMode mode) => mode == IcpTestMode.Assay
        ? $"{Math.Round(v, 1, MidpointRounding.AwayFromZero).ToString("0.0", CultureInfo.InvariantCulture)} %"
        : $"{Math.Round(v, 4, MidpointRounding.AwayFromZero).ToString("0.####", CultureInfo.InvariantCulture)} ppm";
}
