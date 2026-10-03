using System.Globalization;

namespace MicroLIMS.Application.Helpers;

public record ResidualSolventReplicateInput(int ReplicateNo, decimal SampleWeightMg, decimal Response);
public record ResidualSolventReplicateResult(int ReplicateNo, decimal Ppm);
public record ResidualSolventResult(IReadOnlyList<ResidualSolventReplicateResult> Replicates, decimal MeanPpm);

// GC residual solvents (USP <467> quantitation, spec 2026-10-03 §3.3). Pure - no I/O.
// ppm (µg/g) = (C_std [µg/mL] × V [mL] / W [g]) × (r_u / r̄_std)
public static class ResidualSolventCalculator
{
    public static ResidualSolventResult Calculate(decimal standardConcentrationUgPerMl, decimal sampleSolutionVolumeMl,
        decimal standardMeanResponse, IReadOnlyList<ResidualSolventReplicateInput> reps)
    {
        if (standardConcentrationUgPerMl <= 0m) throw new InvalidOperationException("The standard concentration must be greater than zero.");
        if (sampleSolutionVolumeMl <= 0m) throw new InvalidOperationException("The sample solution volume must be greater than zero.");
        if (standardMeanResponse <= 0m) throw new InvalidOperationException("The standard mean response must be greater than zero.");
        if (reps.Count == 0) throw new InvalidOperationException("At least one replicate is required.");

        var results = reps.Select(rep =>
        {
            if (rep.SampleWeightMg <= 0m) throw new InvalidOperationException($"Replicate {rep.ReplicateNo}: the sample weight must be greater than zero.");
            var ppm = standardConcentrationUgPerMl * sampleSolutionVolumeMl / (rep.SampleWeightMg / 1000m) * (rep.Response / standardMeanResponse);
            return new ResidualSolventReplicateResult(rep.ReplicateNo, ppm);
        }).ToList();

        return new ResidualSolventResult(results, results.Average(r => r.Ppm));
    }

    public static string FormatPpm(decimal v) =>
        $"{Math.Round(v, 1, MidpointRounding.AwayFromZero).ToString("0.0", CultureInfo.InvariantCulture)} ppm";
}
