using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Helpers;

public record FactorEvaluation(decimal MeanFactor, decimal? RsdPercent, bool Passed, IReadOnlyList<string> FailureReasons);

// QC Analytical Engine - titration (USP Volumetric Solutions). Pure functions, no I/O.
public static class TitrationEngine
{
    public static decimal PrimaryStandardFactor(decimal standardWeightMg, decimal purityPercent,
        decimal titrantVolumeMl, decimal blankMl, decimal equivalenceMgPerMl)
    {
        if (standardWeightMg <= 0m)
            throw new InvalidOperationException("Standard weight must be greater than zero.");
        if (purityPercent <= 0m || purityPercent > 100m)
            throw new InvalidOperationException("Purity must be greater than 0 and at most 100.");
        if (equivalenceMgPerMl <= 0m)
            throw new InvalidOperationException("Equivalence must be greater than zero.");
        if (blankMl < 0m)
            throw new InvalidOperationException("Blank must not be negative.");
        if (titrantVolumeMl - blankMl <= 0m)
            throw new InvalidOperationException("Titrant volume must be greater than the blank.");

        decimal netVolumeMl = titrantVolumeMl - blankMl;

        return (standardWeightMg * purityPercent / 100m) / (netVolumeMl * equivalenceMgPerMl);
    }

    public static decimal AgainstVolumetricSolutionFactor(decimal referenceVolumeMl, decimal referenceFactor,
        decimal referenceNominalStrength, decimal titrantVolumeMl, decimal blankMl, decimal nominalStrength)
    {
        if (referenceVolumeMl <= 0m)
            throw new InvalidOperationException("Reference volume must be greater than zero.");
        if (referenceFactor <= 0m)
            throw new InvalidOperationException("Reference factor must be greater than zero.");
        if (referenceNominalStrength <= 0m)
            throw new InvalidOperationException("Reference strength must be greater than zero.");
        if (nominalStrength <= 0m)
            throw new InvalidOperationException("Titrant strength must be greater than zero.");
        if (blankMl < 0m)
            throw new InvalidOperationException("Blank must not be negative.");
        if (titrantVolumeMl - blankMl <= 0m)
            throw new InvalidOperationException("Titrant volume must be greater than the blank.");

        decimal netVolumeMl = titrantVolumeMl - blankMl;

        return (referenceVolumeMl * referenceFactor * referenceNominalStrength) / (netVolumeMl * nominalStrength);
    }

    public static FactorEvaluation Evaluate(IReadOnlyList<decimal> factors, decimal factorMin, decimal factorMax, decimal maxRsdPercent)
    {
        if (factors == null || factors.Count == 0)
            throw new InvalidOperationException("At least one replicate is required.");

        decimal mean = factors.Average();
        decimal? rsdPercent = null;

        var failureReasons = new List<string>();

        for (int i = 0; i < factors.Count; i++)
        {
            decimal factor = factors[i];
            if (factor < factorMin || factor > factorMax)
            {
                failureReasons.Add(
                    $"Replicate {i + 1}: factor {factor:0.0000} outside {factorMin:0.0000}–{factorMax:0.0000}.");
            }
        }

        if (factors.Count > 1)
        {
            decimal sumSquaredDiffs = 0m;
            foreach (var factor in factors)
            {
                decimal diff = factor - mean;
                sumSquaredDiffs += diff * diff;
            }

            decimal variance = sumSquaredDiffs / (factors.Count - 1);
            decimal stdDev = DecimalMath.Sqrt(variance, 10);
            decimal rsd = mean == 0m ? 0m : (stdDev / mean) * 100m;
            rsdPercent = rsd;

            if (rsd > maxRsdPercent)
            {
                failureReasons.Add($"RSD {rsd:0.00}% exceeds {maxRsdPercent:0.00}%.");
            }
        }

        bool passed = failureReasons.Count == 0;

        return new FactorEvaluation(mean, rsdPercent, passed, failureReasons);
    }

    // ---- Titration assay (spec 4 + amendments A1) -------------------------------------------

    public record SeriesSummary(decimal Mean, decimal? RsdPercent, bool RsdExceeded);

    // Ph. Eur. nonaqueous volume correction: V' = V x (1 + (Tstd - Tt) x k).
    public static decimal CorrectVolume(decimal volumeMl, decimal standardizationTempC, decimal titrationTempC, decimal coefficient) =>
        volumeMl * (1m + (standardizationTempC - titrationTempC) * coefficient);

    // Net titrant volume: direct = V - B, residual (back titration with a blank) = B - V. Must be positive.
    public static decimal NetVolume(bool residual, decimal volumeMl, decimal blankMl)
    {
        decimal net = residual ? blankMl - volumeMl : volumeMl - blankMl;
        if (net <= 0m)
            throw new InvalidOperationException(residual
                ? "The blank volume must be greater than the sample titrant volume."
                : "The titrant volume must be greater than the blank volume.");
        return net;
    }

    // mg analyte = net x N x f x F. Karl Fischer: pass N = nominal mg/mL, F = 1.
    public static decimal MgDirect(decimal netVolumeMl, decimal nominalStrength, decimal factor, decimal equivalencyFactor)
    {
        RequirePositive(nominalStrength, "Titrant strength");
        RequirePositive(factor, "Titrant factor");
        RequirePositive(equivalencyFactor, "Equivalency factor");
        return netVolumeMl * nominalStrength * factor * equivalencyFactor;
    }

    // Residual without a blank: (Vex x Nex x fex - V' x N x f) x F; must be positive.
    public static decimal MgResidualNoBlank(decimal excessVolumeMl, decimal excessStrength, decimal excessFactor,
        decimal backVolumeMl, decimal backStrength, decimal backFactor, decimal equivalencyFactor)
    {
        RequirePositive(excessVolumeMl, "Excess volume");
        RequirePositive(excessStrength, "Excess titrant strength");
        RequirePositive(excessFactor, "Excess titrant factor");
        RequirePositive(backStrength, "Titrant strength");
        RequirePositive(backFactor, "Titrant factor");
        RequirePositive(equivalencyFactor, "Equivalency factor");
        decimal mEq = excessVolumeMl * excessStrength * excessFactor - backVolumeMl * backStrength * backFactor;
        if (mEq <= 0m)
            throw new InvalidOperationException("The back-titrated amount exceeds the excess titrant added.");
        return mEq * equivalencyFactor;
    }

    // Relative method: K_s = W_std x P/100 x (100 - MC)/100 / net_std  [mg analyte per mL titrant].
    public static decimal StandardK(decimal standardWeightMg, decimal purityPercent, decimal moisturePercent, decimal standardNetVolumeMl)
    {
        RequirePositive(standardWeightMg, "Standard weight");
        RequirePositive(standardNetVolumeMl, "Standard net titre");
        if (purityPercent <= 0m || purityPercent > 100m)
            throw new InvalidOperationException("Standard purity must be greater than 0 and at most 100.");
        if (moisturePercent < 0m || moisturePercent >= 100m)
            throw new InvalidOperationException("Standard moisture must be at least 0 and below 100.");
        return standardWeightMg * purityPercent / 100m * (100m - moisturePercent) / 100m / standardNetVolumeMl;
    }

    public static decimal MgRelative(decimal netVolumeMl, decimal k)
    {
        RequirePositive(k, "Standard factor K");
        return netVolumeMl * k;
    }

    // Replicate result from mg analyte, per spec basis. avgUnitWeightMg / labelClaimMg only where the basis needs them.
    public static decimal ApplyBasis(decimal mg, decimal sampleWeightMg, ResultBasis basis,
        decimal? lossPercent, decimal? averageUnitWeightMg, decimal? labelClaimMg)
    {
        RequirePositive(sampleWeightMg, "Sample weight");
        decimal pct = mg / sampleWeightMg * 100m;
        switch (basis)
        {
            case ResultBasis.PercentAsIs:
                return pct;
            case ResultBasis.PercentDriedBasis:
            case ResultBasis.PercentAnhydrousBasis:
                if (!lossPercent.HasValue || lossPercent < 0m || lossPercent >= 100m)
                    throw new InvalidOperationException("Loss on drying / water % (0 to below 100) is required for a dried or anhydrous basis.");
                return pct * 100m / (100m - lossPercent.Value);
            case ResultBasis.PercentLabelClaim:
                if (!averageUnitWeightMg.HasValue || averageUnitWeightMg <= 0m)
                    throw new InvalidOperationException("Average unit weight is required for this result basis.");
                if (!labelClaimMg.HasValue || labelClaimMg <= 0m)
                    throw new InvalidOperationException("Label claim is required for this result basis.");
                return mg / sampleWeightMg * averageUnitWeightMg.Value / labelClaimMg.Value * 100m;
            case ResultBasis.MgPerUnit:
                if (!averageUnitWeightMg.HasValue || averageUnitWeightMg <= 0m)
                    throw new InvalidOperationException("Average unit weight is required for this result basis.");
                return mg / sampleWeightMg * averageUnitWeightMg.Value;
            default:
                throw new InvalidOperationException($"Result basis {basis} is not supported for titration.");
        }
    }

    // Mean and sample-SD RSD % of the replicate results; RSD over the configured maximum flags review.
    public static SeriesSummary Summarize(IReadOnlyList<decimal> results, decimal? maxRsdPercent)
    {
        if (results == null || results.Count == 0)
            throw new InvalidOperationException("At least one replicate is required.");
        decimal mean = results.Average();
        decimal? rsd = null;
        if (results.Count >= 2 && mean != 0m)
        {
            decimal ss = results.Sum(r => (r - mean) * (r - mean));
            rsd = DecimalMath.Sqrt(ss / (results.Count - 1), 10) / Math.Abs(mean) * 100m;
        }
        return new SeriesSummary(mean, rsd, rsd.HasValue && maxRsdPercent.HasValue && rsd.Value > maxRsdPercent.Value);
    }

    private static void RequirePositive(decimal v, string what)
    {
        if (v <= 0m) throw new InvalidOperationException($"{what} must be greater than zero.");
    }
}
