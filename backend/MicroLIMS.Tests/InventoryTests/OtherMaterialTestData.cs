using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Tests.InventoryTests;

// Categories and entries of the Other Materials group, written straight to the context.
public static class OtherMaterialTestData
{
    public static async Task<MaterialCategory> CreateCategoryAsync(
        MicroLimsDbContext db, string name, string prefix, Action<MaterialCategory>? configure = null, params int[] sectionIds)
    {
        var category = new MaterialCategory
        {
            Name = name, CodePrefix = prefix, DefaultUnit = MaterialUnit.Piece, ExpiryRequired = true, QuantityTracked = true, IsActive = true,
            CreatedAt = DateTime.UtcNow, LastModifiedAt = DateTime.UtcNow,
            Sections = sectionIds.Select(id => new MaterialCategorySection { SectionId = id }).ToList()
        };
        configure?.Invoke(category);
        db.MaterialCategories.Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    public static async Task<MaterialMasterEntry> CreateEntryAsync(
        MicroLimsDbContext db, MaterialCategory category, string name, string code, int ownerSectionId)
    {
        var entry = new MaterialMasterEntry
        {
            SectionId = ownerSectionId, Code = code, Name = name, Category = MaterialMasterCategory.Other,
            MaterialCategoryId = category.Id, BaseUnit = category.DefaultUnit, IsActive = true,
            CreatedAt = DateTime.UtcNow, LastModifiedAt = DateTime.UtcNow
        };
        db.MaterialMasterEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }
}
