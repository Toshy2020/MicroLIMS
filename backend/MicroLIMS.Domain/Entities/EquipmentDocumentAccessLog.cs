using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Append-only access log for View and Download events on equipment documents.
// Excluded from recursive AuditLog capture.
public class EquipmentDocumentAccessLog
{
    public int Id { get; set; }

    public int DocumentId { get; set; }
    public int EquipmentInventoryId { get; set; }
    public int UserId { get; set; }
    public DateTime AccessedAt { get; set; } = DateTime.UtcNow;
    public EquipmentDocumentAccessAction Action { get; set; }
}
