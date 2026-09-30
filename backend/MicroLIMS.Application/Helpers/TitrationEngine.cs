namespace MicroLIMS.Application.Helpers;

public record FactorEvaluation(decimal MeanFactor, decimal? RsdPercent, bool Passed, IReadOnlyList<string> FailureReasons);

// QC Analytical Engine - titration (USP Volumetric Solutions). Pure functions, no I/O.
public static class TitrationEngine
{
    public static decimal PrimaryStandardFactor(decimal standardWeightMg, decimal? purityPercent,
        decimal titrantVolumeMl, decimal blankMl, decimal equivalenceMgPerMl)
    {
        if (standardWeightMg <= 0m)
            throw new InvalidOperationException("Standard weight must be greater than zero.");
        if (purityPercent.HasValue && (purityPercent.Value <= 0m || purityPercent.Value > 100m))
            throw new InvalidOperationException("Purity must be greater than 0 and at most 100.");
        if (equivalenceMgPerMl <= 0m)
            throw new InvalidOperationException("Equivalence must be greater than zero.");
        if (blankMl < 0m)
            throw new InvalidOperationException("Blank must not be negative.");
        if (titrantVolumeMl - blankMl <= 0m)
            throw new InvalidOperationException("Titrant volume must be greater than the blank.");

        decimal purity = purityPercent ?? 100m;
        decimal netVolumeMl = titrantVolumeMl - blankMl;

        return (standardWeightMg * purity / 100m) / (netVolumeMl * equivalenceMgPerMl);
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
}
