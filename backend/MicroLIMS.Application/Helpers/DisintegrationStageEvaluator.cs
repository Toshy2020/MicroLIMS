using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Helpers;

public record DisintegrationStageConfig(
    int Stage1Units = 6,
    int Stage2Units = 12,
    int MaxStage1Failures = 2,
    int MinPassTotal = 16);

public record DisintegrationStageEvaluationResult(
    int StageReached,
    DissolutionStageOutcome Outcome,
    int PassedCount,
    int TotalCount,
    decimal? LongestMinutes,
    IReadOnlyList<string> Reasons);

public static class DisintegrationStageEvaluator
{
    public static DisintegrationStageEvaluationResult Evaluate(
        IReadOnlyList<decimal?> unitMinutes,
        decimal limitMinutes,
        DisintegrationStageConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(unitMinutes);
        config ??= new DisintegrationStageConfig();

        if (limitMinutes <= 0m)
            throw new InvalidOperationException("Disintegration time limit must be greater than zero.");

        if (config.Stage1Units < 1 || config.Stage2Units < 1)
            throw new InvalidOperationException("Disintegration stage units must be greater than or equal to 1.");

        if (config.MaxStage1Failures < 0 || config.MaxStage1Failures >= config.Stage1Units)
            throw new InvalidOperationException($"Disintegration maximum Stage 1 failures must be between 0 and {config.Stage1Units - 1}.");

        int totalUnits = config.Stage1Units + config.Stage2Units;
        if (config.MinPassTotal < 1 || config.MinPassTotal > totalUnits)
            throw new InvalidOperationException($"Disintegration minimum pass total must be between 1 and {totalUnits}.");

        int count = unitMinutes.Count;
        if (count != config.Stage1Units && count != totalUnits)
            throw new InvalidOperationException($"Disintegration evaluation requires {config.Stage1Units} (Stage 1) or {totalUnits} (Stage 2) units, but received {count}.");

        if (unitMinutes.Any(u => u.HasValue && u.Value <= 0m))
            throw new InvalidOperationException("Disintegration time must be greater than zero.");

        int passedCount = unitMinutes.Count(u => u.HasValue && u.Value <= limitMinutes);
        int failures = count - passedCount;

        decimal? longestMinutes = unitMinutes
            .Where(u => u.HasValue)
            .Select(u => (decimal?)u!.Value)
            .DefaultIfEmpty(null)
            .Max();

        if (count == config.Stage1Units)
        {
            // Stage 1
            if (failures == 0)
            {
                return new DisintegrationStageEvaluationResult(
                    1, DissolutionStageOutcome.Complies, passedCount, count, longestMinutes, Array.Empty<string>());
            }

            if (failures <= config.MaxStage1Failures)
            {
                return new DisintegrationStageEvaluationResult(
                    1, DissolutionStageOutcome.NextStageRequired, passedCount, count, longestMinutes,
                    new[] { $"{failures} unit(s) failed to disintegrate within {limitMinutes} min. Proceed to Stage 2." });
            }

            return new DisintegrationStageEvaluationResult(
                1, DissolutionStageOutcome.DoesNotComply, passedCount, count, longestMinutes,
                new[] { $"{failures} unit(s) failed to disintegrate within {limitMinutes} min, exceeding maximum allowed for Stage 1 ({config.MaxStage1Failures})." });
        }

        // Stage 2
        if (passedCount >= config.MinPassTotal)
        {
            return new DisintegrationStageEvaluationResult(
                2, DissolutionStageOutcome.Complies, passedCount, count, longestMinutes, Array.Empty<string>());
        }

        return new DisintegrationStageEvaluationResult(
            2, DissolutionStageOutcome.DoesNotComply, passedCount, count, longestMinutes,
            new[] { $"{passedCount} of {count} units disintegrated within {limitMinutes} min, below the required {config.MinPassTotal} units." });
    }

    public static DisintegrationStageEvaluationResult Evaluate(
        IReadOnlyList<decimal?> unitMinutes,
        decimal limitMinutes,
        int stage1Units,
        int stage2Units = 12,
        int maxStage1Failures = 2,
        int minPassTotal = 16)
        => Evaluate(unitMinutes, limitMinutes, new DisintegrationStageConfig(stage1Units, stage2Units, maxStage1Failures, minPassTotal));
}
