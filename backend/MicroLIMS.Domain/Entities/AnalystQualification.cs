using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class AnalystQualification : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public int UserId { get; set; }
    public int TestDefinitionId { get; set; }
    public QualificationBasis Basis { get; set; }
    public string Evidence { get; set; } = string.Empty;
    public int? EvidenceSampleId { get; set; }
    public DateOnly QualifiedOn { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public int SignedByUserId { get; set; }
    public int SignatureId { get; set; }
    public ElectronicSignature Signature { get; set; } = null!;
    public DateTime? WithdrawnOn { get; set; }
    public string? WithdrawnReason { get; set; }
    public int? WithdrawnByUserId { get; set; }
    public int? WithdrawnSignatureId { get; set; }
    public ElectronicSignature? WithdrawnSignature { get; set; }
    public List<QualificationAttachment> Attachments { get; set; } = new();

    public bool IsCurrentOn(DateOnly labToday) =>
        WithdrawnOn is null && (ExpiresOn is null || labToday <= ExpiresOn);
}

public class QualificationAttachment
{
    public int Id { get; set; }
    public int AnalystQualificationId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ContentSha256 { get; set; } = string.Empty;
    public int UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; }
}
