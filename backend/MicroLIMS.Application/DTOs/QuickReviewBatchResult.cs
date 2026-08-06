namespace MicroLIMS.Application.DTOs;

public record SkippedReview(int TestOrderId, string Reason);

public record QuickReviewBatchResult(List<int> Reviewed, List<SkippedReview> Skipped);
