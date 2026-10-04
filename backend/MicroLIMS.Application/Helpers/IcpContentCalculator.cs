namespace MicroLIMS.Application.Helpers;

public record IcpReplicateInput(int ReplicateNo, decimal SampleAmount, decimal VolumeMl, decimal DilutionFactor, decimal SolutionMgPerL);
public record IcpReplicateContent(int ReplicateNo, decimal ContentPerAmount, bool BelowLoq, bool OverRange);
public record IcpElementContent(
    IReadOnlyList<IcpReplicateContent> Replicates,
    decimal? MeanContentPerAmount,      // µg per g (or per mL); null when every replicate is <LOQ
    bool AllBelowLoq, bool SomeBelowLoq, bool AnyOverRange,
    decimal? MgPerUnit, decimal? PercentLabelClaim);

// ICP element content. Pure - no I/O, no rounding (display rounding is the recorder's job).
// content = C × V × DF / W   (mg/L × mL = µg; / g -> µg/g)
// mg/unit = mean content × unitAmount / 1000 × conversionFactor ; %LC = mg/unit / labelClaim × 100
// C < lowest level -> BelowLoq ; C > highest level -> OverRange (C equal to a level is in range)
public static class IcpContentCalculator
{
    public static IcpElementContent Calculate(IReadOnlyList<IcpReplicateInput> reps, decimal lowestLevel, decimal highestLevel,
        decimal conversionFactor, decimal? unitAmount, decimal? labelClaim)
    {
        if (reps.Count == 0) throw new InvalidOperationException("At least one replicate is required.");
        if (reps.Any(r => r.SampleAmount <= 0m || r.VolumeMl <= 0m || r.DilutionFactor < 1m))
            throw new InvalidOperationException("Sample amount, volume and dilution must be positive (dilution at least 1).");

        var results = reps.Select(r => new IcpReplicateContent(
            r.ReplicateNo,
            r.SolutionMgPerL * r.VolumeMl * r.DilutionFactor / r.SampleAmount,
            r.SolutionMgPerL < lowestLevel,
            r.SolutionMgPerL > highestLevel)).ToList();

        var measured = results.Where(r => !r.BelowLoq).ToList();
        decimal? mean = measured.Count > 0 ? measured.Average(r => r.ContentPerAmount) : null;
        decimal? mgPerUnit = mean is not null && unitAmount > 0m ? mean * unitAmount / 1000m * conversionFactor : null;
        decimal? pct = mgPerUnit is not null && labelClaim > 0m ? mgPerUnit / labelClaim * 100m : null;

        return new IcpElementContent(results, mean, measured.Count == 0, measured.Count is > 0 && measured.Count < results.Count,
            results.Any(r => r.OverRange), mgPerUnit, pct);
    }
}
