using MicroLIMS.Application.Services;

namespace MicroLIMS.Application.Helpers;

// Fixed site rule for working standard qualification (spec 2026-10-03, D2/D6).
public static class WorkingStandardRules
{
    public const int Replicates = 6;
    public const decimal MaxRsdPercent = 2.0m;
    public const int ValidityMonths = 12;
    public const int DueSoonDays = 30;
}

public record WorkingStandardResult(
    IReadOnlyList<decimal> ReplicateAssayPercents, decimal MeanAssayPercent, decimal? RsdPercent,
    decimal PotencyPercent, bool Passed, string? FailureReasons);

// Pure: per-replicate as-is assay % (from HplcAssayCalculator) -> mean, RSD,
// dried-basis potency (D5) and pass/fail.
public static class WorkingStandardCalculator
{
    public static bool PassesRsd(decimal rsdPercent) =>
        Math.Round(rsdPercent, 2, MidpointRounding.AwayFromZero) <= WorkingStandardRules.MaxRsdPercent;

    public static decimal DriedBasisPotency(decimal meanAssayPercent, decimal moisturePercent)
    {
        if (moisturePercent < 0m || moisturePercent >= 100m)
            throw new InvalidOperationException("Moisture content must be at least 0 and below 100.");
        return Math.Round(meanAssayPercent * 100m / (100m - moisturePercent), 3, MidpointRounding.AwayFromZero);
    }

    public static WorkingStandardResult Evaluate(IReadOnlyList<decimal> replicateAssayPercents, decimal moisturePercent)
    {
        ArgumentNullException.ThrowIfNull(replicateAssayPercents);
        if (replicateAssayPercents.Count == 0)
            throw new InvalidOperationException("At least one replicate is required.");

        var mean = replicateAssayPercents.Average();
        var rsd = SystemSuitabilityService.CalculateStandardRsd(replicateAssayPercents);
        var potency = DriedBasisPotency(mean, moisturePercent);

        var reasons = new List<string>();
        if (replicateAssayPercents.Count != WorkingStandardRules.Replicates)
            reasons.Add($"Exactly {WorkingStandardRules.Replicates} replicates are required ({replicateAssayPercents.Count} entered).");
        if (rsd.HasValue && !PassesRsd(rsd.Value))
            reasons.Add($"RSD {Math.Round(rsd.Value, 2, MidpointRounding.AwayFromZero):0.00} % is above {WorkingStandardRules.MaxRsdPercent:0.0} %.");

        return new WorkingStandardResult(replicateAssayPercents.ToList(), mean, rsd, potency,
            reasons.Count == 0, reasons.Count == 0 ? null : string.Join(" ", reasons));
    }
}
