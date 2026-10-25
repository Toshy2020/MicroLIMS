using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// A document (supplier CoA, ...) attached to one received sample. Never
// deleted: voiding keeps the file and metadata. StorageKey is server-generated
// (sample-documents/{sampleId}/{id}{ext}), never the uploaded file name.
public class SampleDocument
{
    public int Id { get; set; }

    public int SampleId { get; set; }
    public Sample Sample { get; set; } = null!;

    public SampleDocumentType DocumentType { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ContentSha256 { get; set; } = string.Empty;

    public int UploadedByUserId { get; set; }
    public User? UploadedByUser { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public SampleDocumentStatus Status { get; set; } = SampleDocumentStatus.Current;

    public DateTime? VoidedAt { get; set; }
    public int? VoidedByUserId { get; set; }
    public string? VoidReason { get; set; }
}
