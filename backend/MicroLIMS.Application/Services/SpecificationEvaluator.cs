using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

public static class SpecificationEvaluator
{
    public static string Evaluate(Specification spec, decimal value)
    {
        ArgumentNullException.ThrowIfNull(spec);

        switch (spec.LimitType)
        {
            case LimitType.Range:
            {
                if (!spec.LowerLimit.HasValue && !spec.UpperLimit.HasValue)
                    return "LimitsNotConfigured";

                if (spec.LowerLimit.HasValue)
                {
                    bool outOfLower = spec.LowerInclusive ? value < spec.LowerLimit.Value : value <= spec.LowerLimit.Value;
                    if (outOfLower)
                        return "OutOfSpecification";
                }

                if (spec.UpperLimit.HasValue)
                {
                    bool outOfUpper = spec.UpperInclusive ? value > spec.UpperLimit.Value : value >= spec.UpperLimit.Value;
                    if (outOfUpper)
                        return "OutOfSpecification";
                }

                return "WithinLimits";
            }

            case LimitType.NotMoreThan:
            {
                if (!spec.UpperLimit.HasValue)
                    return "LimitsNotConfigured";

                bool outOfUpper = spec.UpperInclusive ? value > spec.UpperLimit.Value : value >= spec.UpperLimit.Value;
                return outOfUpper ? "OutOfSpecification" : "WithinLimits";
            }

            case LimitType.NotLessThan:
            {
                if (!spec.LowerLimit.HasValue)
                    return "LimitsNotConfigured";

                bool outOfLower = spec.LowerInclusive ? value < spec.LowerLimit.Value : value <= spec.LowerLimit.Value;
                return outOfLower ? "OutOfSpecification" : "WithinLimits";
            }

            case LimitType.TargetWithTolerance:
            {
                if (!spec.Target.HasValue || !spec.Tolerance.HasValue)
                    return "LimitsNotConfigured";

                decimal tol = spec.ToleranceMode == ToleranceMode.Percent
                    ? (spec.Target.Value * spec.Tolerance.Value / 100m)
                    : spec.Tolerance.Value;

                decimal min = spec.Target.Value - tol;
                decimal max = spec.Target.Value + tol;

                if (value < min || value > max)
                    return "OutOfSpecification";

                return "WithinLimits";
            }

            case LimitType.CountTiered:
                return SpecLimitParser.CompareAgainstLimits(value, spec.AlertLimit, spec.ActionLimit, spec.SpecLimit);

            case LimitType.Qualitative:
            case LimitType.PresenceAbsence:
            case LimitType.MultiStage:
            case LimitType.DissolutionQ:
            case LimitType.DisintegrationTime:
            case LimitType.WeightVariation:
                throw new InvalidOperationException($"Specification limit type '{spec.LimitType}' is not numeric and cannot be evaluated against a numeric value.");

            default:
                throw new InvalidOperationException($"Unsupported specification limit type '{spec.LimitType}'.");
        }
    }
}
