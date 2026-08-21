using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Audit log for view and download access events on controlled item documents.
public class ItemDocumentAccessLog
{
    public int Id { get; set; }

    public int DocumentId { get; set; }
    public ItemDocument Document { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public MaterialDocumentAccessAction Action { get; set; }
    public DateTime AccessedAt { get; set; } = DateTime.UtcNow;
}
