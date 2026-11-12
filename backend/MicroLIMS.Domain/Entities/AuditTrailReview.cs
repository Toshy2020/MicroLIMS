using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Signed periodic audit trail review (EU GMP Annex 11 s.9, 21 CFR 11.10(e)).
// Scope and CutoffAuditLogId are fixed at start so entries written later never
// silently enter the review. Once Signed (Status = 1), a database trigger blocks
// UPDATE and DELETE, and blocks TRUNCATE always.
public class AuditTrailReview : IVersionedEntity
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime PeriodFromUtc { get; set; }
    public DateTime PeriodToUtc { get; set; }
    public AuditReviewScopeKind ScopeKind { get; set; }
    public string? ScopeEntityName { get; set; }
    public string? ScopeRecordId { get; set; }
    public int CutoffAuditLogId { get; set; }
    public int EntryCount { get; set; }
    public int StartedByUserId { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public AuditTrailReviewStatus Status { get; set; } = AuditTrailReviewStatus.InProgress;
    public AuditTrailReviewOutcome? Outcome { get; set; }
    public string? Conclusion { get; set; }
    public DateTime? SignedAtUtc { get; set; }
    public int? ElectronicSignatureId { get; set; }
    public ElectronicSignature? ElectronicSignature { get; set; }
    public uint Version { get; set; }

    public List<AuditTrailReviewFinding> Findings { get; set; } = new();
}

// One reviewer observation about a specific AuditLog row inside the review's
// fixed scope. Never deleted or truncated (DB trigger); editable only once to
// transition from Open to Closed with comment, actor, and timestamp.
public class AuditTrailReviewFinding
{
    public int Id { get; set; }
    public int ReviewId { get; set; }
    public AuditTrailReview? Review { get; set; }
    public int AuditLogId { get; set; }
    public string Note { get; set; } = string.Empty;
    public AuditFindingStatus Status { get; set; } = AuditFindingStatus.Open;
    public string? ClosedComment { get; set; }
    public int? ClosedByUserId { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public int RecordedByUserId { get; set; }
    public DateTime RecordedAtUtc { get; set; }
}
