using MicroLIMS.Application.DTOs;

namespace MicroLIMS.Application.Interfaces;

// Periodic access review (EU GMP Annex 11 s.12, 21 CFR 11.10(d)(g)).
// Signing rule: the signer needs Access.Review (checked at the endpoint) and
// cannot sign a review that is about their own access - one where a finding
// concerns their own account, or the report flags their own account. Anyone
// else holding the permission, including the person who generated it, may sign.
public interface IAccessReviewService
{
    Task<AccessReviewDetailDto> GenerateAsync(string? periodLabel, string? reason, int actingUserId);
    Task<IReadOnlyList<AccessReviewSummaryDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<AccessReviewDetailDto> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<AccessReviewFindingDto> AddFindingAsync(int reviewId, SaveAccessReviewFindingRequest request, int actingUserId);
    Task<AccessReviewFindingDto> UpdateFindingAsync(int reviewId, int findingId, SaveAccessReviewFindingRequest request, int actingUserId);
    Task<AccessReviewDetailDto> SignAsync(int reviewId, string? password, string? conclusion, int actingUserId, string? ipAddress);
    // Records an ACCESS_REVIEW_EXPORTED audit event.
    Task<AccessReviewExportResult> ExportCsvAsync(int reviewId, int actingUserId);
}
