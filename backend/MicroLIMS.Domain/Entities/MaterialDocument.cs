using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// A document (COA, SDS, etc.) attached to one received material lot.
// The relationship is Material 1 → 0..many MaterialDocument.
//
// Documents are never permanently deleted. Supersession creates a new
// Current document while marking the old one Superseded. Voiding
// retains the file and metadata but marks the record Voided.
//
// StorageKey is a server-generated path segment — never the original
// filename and never a user-controlled value. Physical storage paths
// are never returned to clients.
//
// Every insert/update is captured by MicroLimsDbContext.SaveChanges
// into AuditLog (Frozen Principle #5). File-read events are captured
// separately in MaterialDocumentAccessLog.
public class MaterialDocument
{
    public int Id { get; set; }

    // FK to Material (the received lot) — required, Restrict delete.
    public int MaterialId { get; set; }
    public Material Material { get; set; } = null!;

    public MaterialDocumentType DocumentType { get; set; }

    // OriginalFileName is the name the user uploaded. It is stored for
    // display only and never used as a filesystem path.
    public string OriginalFileName { get; set; } = string.Empty;

    // Server-generated storage key: material-documents/{materialId}/{documentId}{ext}
    public string StorageKey { get; set; } = string.Empty;

    public string FileExtension { get; set; } = string.Empty;   // e.g. ".pdf"
    public string ContentType { get; set; } = string.Empty;     // e.g. "application/pdf"
    public long FileSizeBytes { get; set; }
    public string ContentSha256 { get; set; } = string.Empty;   // hex-encoded

    public int UploadedByUserId { get; set; }
    public User? UploadedByUser { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public MaterialDocumentStatus Status { get; set; } = MaterialDocumentStatus.Current;

    // ---- Supersession chain ----
    // When this document is replaced, SupersededByDocumentId points to
    // the new document; this document is marked Superseded. The chain
    // is navigable forward only — do not resolve it recursively in queries.
    public int? SupersededByDocumentId { get; set; }
    public MaterialDocument? SupersededByDocument { get; set; }

    public DateTime? SupersededAt { get; set; }
    public int? SupersededByUserId { get; set; }      // id only — no nav property
    public string? SupersessionReason { get; set; }

    // ---- Void ----
    public DateTime? VoidedAt { get; set; }
    public int? VoidedByUserId { get; set; }           // id only — no nav property
    public string? VoidReason { get; set; }
}
