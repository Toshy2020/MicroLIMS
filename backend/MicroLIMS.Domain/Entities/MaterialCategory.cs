using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// A kind of "Other Materials" stock (Supplement, Antibiotic Disc, PPE, ...),
// maintained as data. It carries the behaviour the old MaterialType enum
// values hard-coded: default unit, COA before use, expiry, quantity tracking,
// default storage, release before use, label and the sections it is offered
// to. The materials themselves are MaterialMasterEntry rows (Category = Other)
// pointing at one category. Created and changed only by a Section Head with a
// reason and electronic signature; deactivated, never deleted. CodePrefix
// (2-5 upper letters) is the start of its entries' codes ("SUP-") and is
// frozen once any entry uses it.
public class MaterialCategory : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CodePrefix { get; set; } = string.Empty;
    public MaterialUnit DefaultUnit { get; set; }
    public bool CoaRequired { get; set; }
    public bool ExpiryRequired { get; set; } = true;
    public bool QuantityTracked { get; set; } = true;
    public string? DefaultStorageCondition { get; set; }
    // A received lot stays unusable until a second person releases it.
    public bool ApprovalBeforeUse { get; set; }
    public bool LabelRequired { get; set; }
    public bool IsActive { get; set; } = true;

    // No rows = offered to every section.
    public List<MaterialCategorySection> Sections { get; set; } = new();

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int LastModifiedByUserId { get; set; }
    public DateTime LastModifiedAt { get; set; }
}

public class MaterialCategorySection
{
    public int MaterialCategoryId { get; set; }
    public MaterialCategory? MaterialCategory { get; set; }
    public int SectionId { get; set; }
    public DocumentSection? Section { get; set; }
}
