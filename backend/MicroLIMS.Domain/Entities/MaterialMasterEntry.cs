using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Defines WHAT a lab chemical is (reagent, indicator or reference standard).
// Lots, quantities and expiry stay in Material (the stock register), which
// references this entry. Deactivated, never deleted.
public class MaterialMasterEntry : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public int SectionId { get; set; }
    public DocumentSection? Section { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public MaterialMasterCategory Category { get; set; }
    public string? Grade { get; set; }
    // Supplier, or pharmacopoeial source for standards (USP, EP, BP, In-house)
    public string? Source { get; set; }
    public MaterialUnit BaseUnit { get; set; }
    public bool IsActive { get; set; } = true;

    // Indicator only (all optional)
    public string? WorkingConcentration { get; set; }
    public string? Solvent { get; set; }
    public decimal? TransitionRangeFrom { get; set; }
    public decimal? TransitionRangeTo { get; set; }
    public string? ColourChange { get; set; }
    public string? IndicatorUse { get; set; }

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int LastModifiedByUserId { get; set; }
    public DateTime LastModifiedAt { get; set; }
}
