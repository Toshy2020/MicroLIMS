using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// A controlled document (e.g. Calibration Certificate) attached to an equipment record.
// Follows the same controlled document lifecycle as Material COA:
// - Never overwritten or permanently deleted through normal operations.
// - Supersession creates a new Current document while marking the old one Superseded.
// - Voiding retains the file and metadata for audit purposes while marking the record Voided.
// - StorageKey is server-generated (e.g. equipment-documents/{equipmentId}/{documentId}{ext})
//   and never exposed directly to clients.
public class EquipmentDocument
{
    public int Id { get; set; }

    // FK to the equipment instrument record — Restrict delete.
    public int EquipmentInventoryId { get; set; }
    public EquipmentInventory EquipmentInventory { get; set; } = null!;

    public EquipmentDocumentType DocumentType { get; set; }

    // OriginalFileName is the display filename from the user's upload.
    public string OriginalFileName { get; set; } = string.Empty;

    // Server-generated storage key: equipment-documents/{equipmentId}/{documentId}{ext}
    public string StorageKey { get; set; } = string.Empty;

    public string FileExtension { get; set; } = string.Empty;   // e.g. ".pdf"
    public string ContentType { get; set; } = string.Empty;     // e.g. "application/pdf"
    public long FileSizeBytes { get; set; }
    public string ContentSha256 { get; set; } = string.Empty;   // hex-encoded SHA-256 hash

    public int UploadedByUserId { get; set; }
    public User? UploadedByUser { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public MaterialDocumentStatus Status { get; set; } = MaterialDocumentStatus.Current;

    // ---- Supersession chain ----
    public int? SupersededByDocumentId { get; set; }
    public EquipmentDocument? SupersededByDocument { get; set; }

    public DateTime? SupersededAt { get; set; }
    public int? SupersededByUserId { get; set; }
    public string? SupersessionReason { get; set; }

    // ---- Void ----
    public DateTime? VoidedAt { get; set; }
    public int? VoidedByUserId { get; set; }
    public string? VoidReason { get; set; }
}
