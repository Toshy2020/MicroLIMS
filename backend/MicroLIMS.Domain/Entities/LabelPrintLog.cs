using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Append-only log of every barcode label print (enforced by PostgreSQL triggers).
// One row per record per print request; PrintNumber is 1 for first print and
// increments on each reprint.
public class LabelPrintLog
{
    public int Id { get; set; }
    public LabelEntityType EntityType { get; set; }
    public int EntityId { get; set; }
    public string Code { get; set; } = string.Empty; // max 250
    public int PrintNumber { get; set; } // 1 = first print
    public bool IsReprint { get; set; }
    public int Copies { get; set; }
    public int LabelFormatId { get; set; }
    public uint LabelFormatVersion { get; set; }
    public string? Reason { get; set; } // max 500
    public int PrintedByUserId { get; set; }
    public DateTime PrintedAt { get; set; } // UTC, set by the service
    public Guid BatchId { get; set; } // one per print request
}
