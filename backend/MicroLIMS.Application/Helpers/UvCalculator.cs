using MicroLIMS.Shared.Exceptions;
namespace MicroLIMS.Application.Helpers;

public static class UvCalculator
{
    public static decimal SpecificAbsorbanceAssay(decimal a, decimal totalVolumeMl, decimal specificAbsorbance, decimal weightG)
    {
        if (a <= 0m) throw new BusinessRuleException("Absorbance must be greater than zero.");
        if (totalVolumeMl <= 0m) throw new BusinessRuleException("Total volume must be greater than zero.");
        if (specificAbsorbance <= 0m) throw new BusinessRuleException("Specific absorbance must be greater than zero.");
        if (weightG <= 0m) throw new BusinessRuleException("Sample weight must be greater than zero.");

        return (a * totalVolumeMl) / (specificAbsorbance * weightG);
    }

    public static decimal SpecificAbsorbanceValue(decimal a, decimal totalVolumeMl, decimal weightG)
    {
        if (a <= 0m) throw new BusinessRuleException("Absorbance must be greater than zero.");
        if (totalVolumeMl <= 0m) throw new BusinessRuleException("Total volume must be greater than zero.");
        if (weightG <= 0m) throw new BusinessRuleException("Sample weight must be greater than zero.");

        return (a * totalVolumeMl) / (weightG * 100m);
    }

    public static decimal StandardComparison(
        decimal a,
        decimal stdA,
        decimal stdWeightG,
        decimal stdVolumeMl,
        decimal totalVolumeMl,
        decimal weightG,
        decimal purityPercent,
        decimal moisturePercent)
    {
        return StandardComparisonCalculator.CalculatePreparationAssay(
            responseTest: a,
            responseStd: stdA,
            actWtStd: stdWeightG,
            thWtStd: stdVolumeMl,
            thWtTest: totalVolumeMl,
            actWtTest: weightG,
            moisturePercent: moisturePercent,
            purityPercent: purityPercent);
    }

    public static bool WithinReference(decimal a, decimal referenceA) => a <= referenceA;
}
