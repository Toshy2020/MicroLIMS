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
    public static List<HplcAssayRow> Evaluate(IReadOnlyList<HplcResidualSolventInput> solvents) =>
        solvents.Select(s =>
        {
            var calc = ResidualSolventCalculator.Calculate(s.StandardConcentrationUgPerMl, s.SampleSolutionVolumeMl, s.StandardMeanResponse, s.Reps);
            var readings = calc.Replicates
                .Select(r => new HplcAssayReading(r.ReplicateNo, s.Reps.First(x => x.ReplicateNo == r.ReplicateNo).Response, r.Ppm))
                .ToList();
            return new HplcAssayRow(s.AnalyteId, s.Name, s.PpmSpec, ResultBasis.Ppm, null,
                calc.MeanPpm, ResidualSolventCalculator.FormatPpm(calc.MeanPpm), "ppm",
                SpecificationEvaluator.Evaluate(s.PpmSpec, calc.MeanPpm), readings);
        }).ToList();
}
