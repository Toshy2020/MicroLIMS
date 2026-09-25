using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Helpers;

public record DissolutionStageEvaluationResult(
    int StageReached,
    DissolutionStageOutcome Outcome,
    decimal Mean,
    IReadOnlyList<string> Reasons);

public static class DissolutionStageEvaluator
{
    public static DissolutionStageEvaluationResult Evaluate(
        IReadOnlyList<decimal> vesselPercentages,
        decimal q,
        decimal s1Offset = 5m,
        decimal s2MinOffset = 15m,
        decimal s3MinOffset = 25m,
        decimal s3MaxBelowS2Min = 2m)
    {
        ArgumentNullException.ThrowIfNull(vesselPercentages);

        int count = vesselPercentages.Count;
        if (count != 6 && count != 12 && count != 24)
            throw new InvalidOperationException($"Dissolution evaluation requires 6 (Stage 1), 12 (Stage 2), or 24 (Stage 3) units, but received {count}.");

        if (q <= 0m || q > 100m)
            throw new InvalidOperationException("Q must be greater than 0 and less than or equal to 100.");

        if (s1Offset < 0m || s2MinOffset < 0m || s3MinOffset < 0m || s3MaxBelowS2Min < 0m)
            throw new InvalidOperationException("Dissolution stage offsets must be greater than or equal to zero.");

        decimal sum = 0m;
        for (int i = 0; i < count; i++)
        {
            sum += vesselPercentages[i];
        }
        decimal mean = sum / count;

        if (count == 6)
        {
            // Stage 1 (6 units): Each unit is not less than Q + S1Offset
            if (vesselPercentages.All(v => v >= q + s1Offset))
            {
                return new DissolutionStageEvaluationResult(1, DissolutionStageOutcome.Complies, mean, Array.Empty<string>());
            }

            // USP <711>: continue through the stages unless the results conform at S1 or S2,
            // even when S3 can no longer be met (lab decision 2026-09-19).
            var belowS1 = vesselPercentages.Where(v => v < q + s1Offset).ToList();
            return new DissolutionStageEvaluationResult(1, DissolutionStageOutcome.NextStageRequired, mean,
                new[] { $"{belowS1.Count} unit(s) below Q + {s1Offset}% ({q + s1Offset}%). Proceed to Stage 2." });
        }

        if (count == 12)
        {
            // Stage 2 (12 units): Average of 12 units >= Q, and no unit is less than Q - S2MinOffset
            if (mean >= q && vesselPercentages.All(v => v >= q - s2MinOffset))
            {
                return new DissolutionStageEvaluationResult(2, DissolutionStageOutcome.Complies, mean, Array.Empty<string>());
            }

            var countBelowS2Min = vesselPercentages.Count(v => v < q - s2MinOffset);
            var reasons = new List<string>();
            if (mean < q) reasons.Add($"Average of 12 units ({mean}%) is below Q ({q}%).");
            if (countBelowS2Min > 0) reasons.Add($"{countBelowS2Min} unit(s) below Q - {s2MinOffset}% ({q - s2MinOffset}%).");
            reasons.Add("Proceed to Stage 3.");
            return new DissolutionStageEvaluationResult(2, DissolutionStageOutcome.NextStageRequired, mean, reasons);
        }

        // Stage 3 (24 units): Average of 24 units >= Q, at most S3MaxBelowS2Min units < Q - S2MinOffset, none < Q - S3MinOffset
        var s3Reasons = new List<string>();
        if (mean < q)
        {
            s3Reasons.Add($"Average of 24 units ({mean}%) is below Q ({q}%).");
        }

        var s3BelowS2Min = vesselPercentages.Count(v => v < q - s2MinOffset);
        if (s3BelowS2Min > s3MaxBelowS2Min)
        {
            s3Reasons.Add($"{s3BelowS2Min} unit(s) below Q - {s2MinOffset}% ({q - s2MinOffset}%), exceeding maximum allowed ({s3MaxBelowS2Min}).");
        }

        var s3BelowS3Min = vesselPercentages.Count(v => v < q - s3MinOffset);
        if (s3BelowS3Min > 0)
        {
            s3Reasons.Add($"{s3BelowS3Min} unit(s) below Q - {s3MinOffset}% ({q - s3MinOffset}%).");
        }

        if (s3Reasons.Count == 0)
        {
            return new DissolutionStageEvaluationResult(3, DissolutionStageOutcome.Complies, mean, Array.Empty<string>());
        }

        return new DissolutionStageEvaluationResult(3, DissolutionStageOutcome.DoesNotComply, mean, s3Reasons);
    }
}
