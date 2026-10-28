using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Helpers;

// What a lot's material category (via its master entry) decides, in one place:
// the COA rule used by consumption and by the lot's COA eligibility banner.
public static class MaterialCategoryRules
{
    // COA required before use: dehydrated media and lyophilized organisms always;
    // otherwise the lot's category says so. Supplement is kept as a safety net for
    // legacy supplement lots that were never linked to a category.
    public static bool IsCoaRequired(MaterialType type, MaterialCategory? category) =>
        type is MaterialType.DehydratedMedia or MaterialType.LyophilizedMicroorganism or MaterialType.Supplement
        || category?.CoaRequired == true;

    // The category of an Other-material lot, or null when it has no entry or the entry has none.
    public static Task<MaterialCategory?> CategoryOfAsync(IMicroLimsDbContext db, Material material) =>
        material.MaterialMasterEntryId.HasValue
            ? db.MaterialMasterEntries.Where(e => e.Id == material.MaterialMasterEntryId.Value)
                .Select(e => e.MaterialCategory).FirstOrDefaultAsync()
            : Task.FromResult<MaterialCategory?>(null);
}
