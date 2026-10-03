using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Helpers;

public record HplcResidualSolventInput(int AnalyteId, string Name, decimal StandardConcentrationUgPerMl,
    decimal SampleSolutionVolumeMl, decimal StandardMeanResponse, IReadOnlyList<ResidualSolventReplicateInput> Reps, Specification PpmSpec);

// GC residual solvents: one judged mean-ppm row per solvent (spec 2026-10-03 §3.3). Pure - no I/O.
// HplcAssayReading.AssayPercent carries the replicate ppm (shared row shape with the assay path).
public static class HplcResidualSolventEvaluator
{
    public const string NotDetected = "Not detected";

    // Every replicate response is zero: report "Not detected" (value 0) instead of "0.0 ppm".
    public static bool IsNotDetected(HplcResidualSolventInput s) => s.Reps.All(r => r.Response == 0m);

    public static List<HplcAssayRow> Evaluate(IReadOnlyList<HplcResidualSolventInput> solvents) =>
        solvents.Select(s =>
        {
            var calc = ResidualSolventCalculator.Calculate(s.StandardConcentrationUgPerMl, s.SampleSolutionVolumeMl, s.StandardMeanResponse, s.Reps);
            var readings = calc.Replicates
                .Select(r => new HplcAssayReading(r.ReplicateNo, s.Reps.First(x => x.ReplicateNo == r.ReplicateNo).Response, r.Ppm))
                .ToList();
            var display = IsNotDetected(s) ? NotDetected : ResidualSolventCalculator.FormatPpm(calc.MeanPpm);
            return new HplcAssayRow(s.AnalyteId, s.Name, s.PpmSpec, ResultBasis.Ppm, null,
                calc.MeanPpm, display, "ppm",
                SpecificationEvaluator.Evaluate(s.PpmSpec, calc.MeanPpm), readings);
        }).ToList();
}
