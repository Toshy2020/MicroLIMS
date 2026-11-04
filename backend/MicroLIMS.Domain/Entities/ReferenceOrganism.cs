using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// A reference strain (e.g. E. coli ATCC 8739): a culture collection and strain
// number under an Organism species. Lyophilized stock lots and cryovials
// reference it; the species stays the unit for media challenge specs and
// test-step target organisms. App-wide: every lab can see and use every
// strain, OwnerSectionId is the only lab that can edit it. Code is unique
// across the app and carries the ORG- prefix; once a stock lot references the
// strain its code changes only by a signed code change. Deactivated, never
// deleted, once referenced.
public class ReferenceOrganism : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public string Code { get; set; } = string.Empty;
    public int OrganismId { get; set; }
    public Organism? Organism { get; set; }
    public CultureCollection CultureCollection { get; set; }
    // Required unless CultureCollection is Other.
    public string? StrainNumber { get; set; }
    // Owner lab
    public int OwnerSectionId { get; set; }
    public DocumentSection? OwnerSection { get; set; }
    public bool IsActive { get; set; } = true;

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int LastModifiedByUserId { get; set; }
    public DateTime LastModifiedAt { get; set; }
}
