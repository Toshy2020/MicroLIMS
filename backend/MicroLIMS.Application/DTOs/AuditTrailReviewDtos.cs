using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs;

public record StartAuditTrailReviewRequest(
    string Title,
    DateTime PeriodFromUtc,
    DateTime PeriodToUtc,
    AuditReviewScopeKind ScopeKind,
    string? EntityName = null,
    string? RecordId = null);

public record AuditTrailReviewFindingDto(
    int Id,
    int ReviewId,
    int AuditLogId,
    string Note,
    AuditFindingStatus Status,
    string? ClosedComment,
    int? ClosedByUserId,
    string? ClosedByName,
    DateTime? ClosedAtUtc,
    int RecordedByUserId,
    string? RecordedByName,
    DateTime RecordedAtUtc);

public record AuditTrailReviewListItemDto(
    int Id,
    string Title,
    DateTime PeriodFromUtc,
    DateTime PeriodToUtc,
    AuditReviewScopeKind ScopeKind,
    string? ScopeEntityName,
    string? ScopeRecordId,
    int CutoffAuditLogId,
    int EntryCount,
    int NewerEntriesCount,
    int FindingCount,
    int OpenFindingCount,
    int StartedByUserId,
    string? StartedByName,
    DateTime StartedAtUtc,
    AuditTrailReviewStatus Status,
    AuditTrailReviewOutcome? Outcome,
    DateTime? SignedAtUtc,
    string? SignerFullName,
    string? SignatureMeaningText,
    uint Version);

public record AuditTrailReviewDto(
    int Id,
    string Title,
    DateTime PeriodFromUtc,
    DateTime PeriodToUtc,
    AuditReviewScopeKind ScopeKind,
    string? ScopeEntityName,
    string? ScopeRecordId,
    int CutoffAuditLogId,
    int EntryCount,
    int NewerEntriesCount,
    int StartedByUserId,
    string? StartedByName,
    DateTime StartedAtUtc,
    AuditTrailReviewStatus Status,
    AuditTrailReviewOutcome? Outcome,
    string? Conclusion,
    DateTime? SignedAtUtc,
    int? ElectronicSignatureId,
    string? SignerFullName,
    string? SignerUsername,
    string? SignatureMeaningText,
    uint Version,
    IReadOnlyList<AuditTrailReviewFindingDto> Findings);
