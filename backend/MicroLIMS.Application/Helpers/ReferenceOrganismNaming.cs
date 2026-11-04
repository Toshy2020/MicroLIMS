using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Helpers;

// How a reference strain reads on screens and in lot snapshots:
// "{species} {collection} {strain number}", e.g. "Escherichia coli ATCC 8739".
// A strain from collection Other has no collection word: "{species} {strain number}",
// or "{species}" alone when it has no number.
public static class ReferenceOrganismNaming
{
    public const string CodePrefix = "ORG-";

    public static string NameOf(string scientificName, CultureCollection collection, string? strainNumber)
    {
        var parts = new List<string> { scientificName };
        if (collection != CultureCollection.Other) parts.Add(collection.ToString());
        if (!string.IsNullOrWhiteSpace(strainNumber)) parts.Add(strainNumber.Trim());
        return string.Join(' ', parts);
    }

    public static string NameOf(ReferenceOrganism strain) =>
        NameOf(strain.Organism?.ScientificName ?? string.Empty, strain.CultureCollection, strain.StrainNumber);

    // The ATCC number a lot or cryovial snapshot keeps for existing consumers: the strain number of an ATCC strain.
    public static string? AtccNumberOf(ReferenceOrganism strain) =>
        strain.CultureCollection == CultureCollection.ATCC ? strain.StrainNumber : null;
}
