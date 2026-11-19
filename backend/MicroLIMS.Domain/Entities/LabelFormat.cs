using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Per-entity-type barcode label dimensions, layout, and default copies.
public class LabelFormat : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public LabelEntityType EntityType { get; set; } // unique
    public decimal WidthMm { get; set; } // 10-150
    public decimal HeightMm { get; set; } // 10-150
    public LabelLayout Layout { get; set; }
    public int DefaultCopies { get; set; } // 1-50
}
