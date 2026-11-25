using System.ComponentModel.DataAnnotations;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs;

public record StartAuditTrailReviewRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string Title,
    DateTime PeriodFromUtc,
    DateTime PeriodToUtc,
    AuditReviewScopeKind ScopeKind,
    [property: MaxLength(150)] string? EntityName = null,
    [property: MaxLength(100)] string? RecordId = null);

public record AddAuditTrailReviewFindingRequest(
    [property: Range(1, int.MaxValue)] int AuditLogId,
    [property: Required, StringLength(2000, MinimumLength = 1)] string Note);

public record SignAuditTrailReviewRequest(
    [property: Required] string Password,
    [property: Required, StringLength(2000, MinimumLength = 1)] string Conclusion);

public record CloseAuditTrailReviewFindingRequest(
    [property: Required, StringLength(2000, MinimumLength = 1)] string Comment);

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
    DateTime RecordedAtUtc)
{
    // The database permits only the Open-to-Closed edit, so status is the source-raise version token.
    public uint Version => (uint)Status;
}

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
