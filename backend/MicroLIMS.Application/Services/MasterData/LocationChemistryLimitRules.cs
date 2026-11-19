using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Messages;
using System.Globalization;

namespace MicroLIMS.Application.Services.MasterData;

public static class LocationChemistryLimitRules
{
    private static readonly HashSet<LimitType> AllowedLimitTypes = new()
    {
        LimitType.Range,
        LimitType.NotMoreThan,
        LimitType.NotLessThan,
        LimitType.Qualitative
    };

    public static void Validate(
        LimitType? limitType,
        decimal? lowerLimit,
        decimal? upperLimit,
        string? expectedResultText)
    {
        if (limitType == null) return;

        if (!AllowedLimitTypes.Contains(limitType.Value))
            throw new BusinessRuleException(
                MessageCodes.MonitoringUnsupportedLimitType,
                $"Only Range, NotMoreThan, NotLessThan, and Qualitative limit types are supported. '{limitType}' is not supported.",
                new Dictionary<string, string> { ["limitType"] = limitType.ToString() });

        switch (limitType.Value)
        {
            case LimitType.Range:
                if (!lowerLimit.HasValue && !upperLimit.HasValue)
                    throw new BusinessRuleException(
                        MessageCodes.MonitoringRangeLimitNeedsBound,
                        "Range limit requires a lower limit, an upper limit, or both.");
                break;

            case LimitType.NotMoreThan:
                if (!upperLimit.HasValue)
                    throw new BusinessRuleException(
                        MessageCodes.MonitoringNotMoreThanNeedsUpper,
                        "Upper limit is required for Not More Than.");
                break;

            case LimitType.NotLessThan:
                if (!lowerLimit.HasValue)
                    throw new BusinessRuleException(
                        MessageCodes.MonitoringNotLessThanNeedsLower,
                        "Lower limit is required for Not Less Than.");
                break;

            case LimitType.Qualitative:
                if (string.IsNullOrWhiteSpace(expectedResultText))
                    throw new BusinessRuleException(
                        MessageCodes.MonitoringQualitativeNeedsExpectedResult,
                        "Expected result text is required for Qualitative.");
                break;
        }

        if (lowerLimit.HasValue && upperLimit.HasValue && lowerLimit.Value > upperLimit.Value)
            throw new BusinessRuleException(
                MessageCodes.SpecificationLowerLimitCannotExceedUpper,
                "Lower limit cannot be greater than upper limit.");
    }
}
