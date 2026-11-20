using System.Globalization;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

public enum ConfirmationResult
{
    Detected,
    NotDetected,
    Inconclusive
}

public class ConfirmationAgreementEvaluator
{
    public ConfirmationResult EvaluateAgreement(
        IReadOnlyCollection<ConfirmatoryPlateObservation> platesForLocation,
        int requiredMediaCount)
    {
        if (platesForLocation.Count != requiredMediaCount)
        {
            throw new BusinessRuleException(
                MessageCodes.PathogenConfirmatoryObservationCountMismatch,
                $"Expected {requiredMediaCount} confirmatory plate observation(s); got {platesForLocation.Count}.",
                new Dictionary<string, string>
                {
                    ["expected"] = requiredMediaCount.ToString(CultureInfo.InvariantCulture),
                    ["received"] = platesForLocation.Count.ToString(CultureInfo.InvariantCulture)
                });
        }

        var distinctObservations = platesForLocation
            .Select(p => p.Observation)
            .Distinct()
            .ToList();

        // 1. All media agree on conforming -> Detected (Presumptive Positive)
        if (distinctObservations.Count == 1 && distinctObservations[0] == GrowthObservation.GrowthConforming)
        {
            return ConfirmationResult.Detected;
        }

        // 2. All media agree on non-conforming or no growth -> Not Detected (Negative)
        if (distinctObservations.Count == 1 &&
            (distinctObservations[0] == GrowthObservation.NoGrowth ||
             distinctObservations[0] == GrowthObservation.GrowthNonConforming))
        {
            return ConfirmationResult.NotDetected;
        }

        // 3. Disagreement across configured media -> Inconclusive (Retest required)
        return ConfirmationResult.Inconclusive;
    }
}
