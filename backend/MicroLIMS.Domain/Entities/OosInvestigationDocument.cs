using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// A controlled document (Lab Investigation Report) attached to an OOS investigation chain.
// Follows the same controlled document lifecycle as EquipmentDocument/MaterialDocument:
// - Keyed by OosGroupCode (e.g. OOS0826001).
// - Never overwritten or permanently deleted through normal operations.
// - Supersession creates a new Current document while marking the old one Superseded.
// - Voiding retains the file and metadata for audit purposes while marking the record Voided.
// - StorageKey is server-generated (oos-investigations/{oosGroupCode}/{documentId}{ext}) and never exposed directly to clients.
public class OosInvestigationDocument
{
    public int Id { get; set; }

    // Grouping key shared by all samples in an OOS chain.
    public string OosGroupCode { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ContentSha256 { get; set; } = string.Empty;

    public int UploadedByUserId { get; set; }
    public User? UploadedByUser { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public MaterialDocumentStatus Status { get; set; } = MaterialDocumentStatus.Current;

    // ---- Supersession chain ----
    public int? SupersededByDocumentId { get; set; }
    public OosInvestigationDocument? SupersededByDocument { get; set; }

    public DateTime? SupersededAt { get; set; }
    public int? SupersededByUserId { get; set; }
    public string? SupersessionReason { get; set; }

    // ---- Void ----
    public DateTime? VoidedAt { get; set; }
    public int? VoidedByUserId { get; set; }
    public string? VoidReason { get; set; }
}
