using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Controlled document (SOP, Verification Report) attached to an Item configuration record.
// Documents are versioned and retained historically when superseded or voided.
public class ItemDocument
{
    public int Id { get; set; }

    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;

    public ItemDocumentType DocumentType { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ContentSha256 { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;
    public DateTime? EffectiveDate { get; set; }

    public int UploadedByUserId { get; set; }
    public User? UploadedByUser { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public MaterialDocumentStatus Status { get; set; } = MaterialDocumentStatus.Current;

    public int? SupersededByDocumentId { get; set; }
    public ItemDocument? SupersededByDocument { get; set; }
    public DateTime? SupersededAt { get; set; }
    public int? SupersededByUserId { get; set; }
    public string? SupersessionReason { get; set; }

    public DateTime? VoidedAt { get; set; }
    public int? VoidedByUserId { get; set; }
    public string? VoidReason { get; set; }
}
