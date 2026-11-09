using MicroLIMS.Domain.Enums;

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
            throw new InvalidOperationException($"Only Range, NotMoreThan, NotLessThan, and Qualitative limit types are supported. '{limitType}' is not supported.");

        switch (limitType.Value)
        {
            case LimitType.Range:
                if (!lowerLimit.HasValue && !upperLimit.HasValue)
                    throw new InvalidOperationException("Range limit requires a lower limit, an upper limit, or both.");
                break;

            case LimitType.NotMoreThan:
                if (!upperLimit.HasValue)
                    throw new InvalidOperationException("Upper limit is required for Not More Than.");
                break;

            case LimitType.NotLessThan:
                if (!lowerLimit.HasValue)
                    throw new InvalidOperationException("Lower limit is required for Not Less Than.");
                break;

            case LimitType.Qualitative:
                if (string.IsNullOrWhiteSpace(expectedResultText))
                    throw new InvalidOperationException("Expected result text is required for Qualitative.");
                break;
        }

        if (lowerLimit.HasValue && upperLimit.HasValue && lowerLimit.Value > upperLimit.Value)
            throw new InvalidOperationException("Lower limit cannot be greater than upper limit.");
    }
}
