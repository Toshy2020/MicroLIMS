using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs.Responses;

public class ReferenceOrganismResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public string Code { get; init; } = null!;
    public int OrganismId { get; init; }
    public string? OrganismScientificName { get; init; }
    public CultureCollection CultureCollection { get; init; }
    public string? StrainNumber { get; init; }
    // "{species} {collection} {strain number}", computed for display.
    public string Name { get; init; } = null!;
    public int OwnerSectionId { get; init; }
    public string? OwnerSectionCode { get; init; }
    public string? OwnerSectionName { get; init; }
    public bool IsActive { get; init; }
    // Decided by the server from the user's configurable sections (owner lab).
    public bool CanEdit { get; init; }
    // Any stock lot or cryovial references it: the code is frozen and changes only by signed code change.
    public bool HasStock { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime LastModifiedAt { get; init; }

    public static ReferenceOrganismResponse From(ReferenceOrganism e, bool canEdit = false, bool hasStock = false) => new()
    {
        Id = e.Id,
        Version = e.Version,
        Code = e.Code,
        OrganismId = e.OrganismId,
        OrganismScientificName = e.Organism?.ScientificName,
        CultureCollection = e.CultureCollection,
        StrainNumber = e.StrainNumber,
        Name = ReferenceOrganismNaming.NameOf(e),
        OwnerSectionId = e.OwnerSectionId,
        OwnerSectionCode = e.OwnerSection?.Code,
        OwnerSectionName = e.OwnerSection?.Name,
        IsActive = e.IsActive,
        CanEdit = canEdit,
        HasStock = hasStock,
        CreatedAt = e.CreatedAt,
        LastModifiedAt = e.LastModifiedAt,
    };
}
