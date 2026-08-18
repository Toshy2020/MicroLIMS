using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Append-only log of every file access (View/Download) against a material
// document. Generic EF change-tracking captures state mutations in AuditLog
// automatically, but file-read events produce no DB writes and therefore
// require explicit recording here.
//
// This table is excluded from MicroLimsDbContext.CaptureAuditEntries
// to avoid recursive audit-of-audit entries.
public class MaterialDocumentAccessLog
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public int MaterialId { get; set; }
    public int UserId { get; set; }
    public DateTime AccessedAt { get; set; } = DateTime.UtcNow;
    public MaterialDocumentAccessAction Action { get; set; }
}
