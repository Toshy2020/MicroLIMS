using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Interfaces;

public interface IAuditTrailReviewService
{
    Task<AuditTrailReviewDto> StartAsync(StartAuditTrailReviewRequest request, int actingUserId, CancellationToken ct = default);
    Task<PagedResult<AuditLogDto>> GetEntriesAsync(int reviewId, int page, int pageSize, CancellationToken ct = default);
    Task<AuditTrailReviewFindingDto> AddFindingAsync(int reviewId, int auditLogId, string note, int actingUserId, CancellationToken ct = default);
    Task<AuditTrailReviewDto> SignAsync(int reviewId, string password, string conclusion, uint expectedVersion, int actingUserId, string? ipAddress, CancellationToken ct = default);
    Task<AuditTrailReviewFindingDto> CloseFindingAsync(int findingId, string comment, int actingUserId, CancellationToken ct = default);
    Task<IReadOnlyList<AuditTrailReviewListItemDto>> ListAsync(int page, int pageSize, CancellationToken ct = default);
    Task<AuditTrailReviewDto> GetAsync(int id, CancellationToken ct = default);
}
