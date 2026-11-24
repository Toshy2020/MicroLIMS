using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities.Quality;

public class QualityRecordStep
{
    public int Id { get; set; }
    public int RecordId { get; set; }
    public QualityStepKind Kind { get; set; }
    public int SignedByUserId { get; set; }
    public DateTime SignedAtUtc { get; set; }
    public int SignatureId { get; set; }
    public string? Comment { get; set; }
    public string SnapshotJson { get; set; } = "{}";
}

public class QualityAction : IVersionedEntity
{
    public int Id { get; set; }
    public int RecordId { get; set; }
    public QualityActionKind Kind { get; set; }
    public string Description { get; set; } = string.Empty;
    public int ResponsibleUserId { get; set; }
    public DateOnly DueDate { get; set; }
    public DateTime? DoneAtUtc { get; set; }
    public int? DoneByUserId { get; set; }
    public string? Evidence { get; set; }
    public int? EvidenceLinkId { get; set; }
    public uint Version { get; set; }
}

public class QualityRecordLink
{
    public int Id { get; set; }
    public int RecordId { get; set; }
    public QualityLinkTargetType TargetType { get; set; }
    public int? TargetId { get; set; }
    public string? NewRecordCode { get; set; }
    public bool IsAffectedByChange { get; set; }
    public int AddedByUserId { get; set; }
    public DateTime AddedAtUtc { get; set; }
    public DateTime? ChangedAtUtc { get; set; }
    public int? ChangeAuditLogId { get; set; }
}

public class QualityAttachment
{
    public int Id { get; set; }
    public int RecordId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ContentSha256 { get; set; } = string.Empty;
    public int UploadedByUserId { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public DateTime? VoidedAtUtc { get; set; }
    public int? VoidedByUserId { get; set; }
    public string? VoidReason { get; set; }
}

public class QualityComment
{
    public int Id { get; set; }
    public int RecordId { get; set; }
    public string Text { get; set; } = string.Empty;
    public int AuthorUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class QualityExtensionRequest : IVersionedEntity
{
    public int Id { get; set; }
    public int RecordId { get; set; }
    public int? ActionId { get; set; }
    public DateOnly RequestedDueDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int RequestedByUserId { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public QualityExtensionDecision? Decision { get; set; }
    public int? DecidedByUserId { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionComment { get; set; }
    public int? SignatureId { get; set; }
    public uint Version { get; set; }
}

public class QualityChecklistAnswer
{
    public int Id { get; set; }
    public int RecordId { get; set; }
    public string ItemKey { get; set; } = string.Empty;
    public string ItemText { get; set; } = string.Empty;
    public QualityChecklistAnswerValue Answer { get; set; }
    public string? Comment { get; set; }
    public int AnsweredByUserId { get; set; }
    public DateTime AnsweredAtUtc { get; set; }
}
