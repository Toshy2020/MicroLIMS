using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record SaveMaterialRequest(
    MaterialType MaterialType, string MaterialName, string ManufacturerName, string BatchNumber,
    DateTime ReceivingDate, DateTime? ExpiryDate, string? Code, string Location,
    decimal QuantityReceived, MaterialUnit Unit, decimal? MinimumStockLevel, string? AtccNumber, int? OrganismId,
    int? MediaProductId = null, int? SectionId = null, decimal? Purity = null, string? CustomType = null);

// Type picker for one laboratory: its built-in types plus the custom type
// names already used in its stock register.
public record MaterialTypeOptions(IReadOnlyList<MaterialType> BuiltIn, IReadOnlyList<string> Custom);

// Materials Stock register (Inventory module) - dehydrated media, discs,
// ID kits/reagents, chemicals, indicators, reference buffers, disposable
// tools. Every Create/Update flows through MicroLimsDbContext.SaveChanges,
// which captures the full audit trail automatically (Frozen Principle #5)
// - this service only needs to stamp the fast-display LastModifiedBy/At
// fields.
//
// Consumption: MediaPreparationService.PrepareAsync calls ConsumeAsync
// to decrement stock when a dehydrated media lot is prepared from it -
// this is the only place QuantityRemaining should ever change after
// receiving.
public class MaterialService
{
    private readonly MicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;

    public MaterialService(MicroLimsDbContext db, IUserSectionScopeService scope)
    {
        _db = db;
        _scope = scope;
    }

    // Suggested default unit per material type - the analyst can still
    // override it from the full MaterialUnit dropdown on save.
    public static MaterialUnit DefaultUnitFor(MaterialType type) => type switch
    {
        MaterialType.DehydratedMedia => MaterialUnit.Gram,
        MaterialType.LyophilizedMicroorganism => MaterialUnit.Disc,
        MaterialType.Supplement => MaterialUnit.Milliliter,
        MaterialType.AntibioticDisc => MaterialUnit.Disc,
        MaterialType.IdentificationKit => MaterialUnit.Kit,
        MaterialType.IdentificationReagent => MaterialUnit.Milliliter,
        MaterialType.Chemical => MaterialUnit.Gram,
        MaterialType.Indicator => MaterialUnit.Piece,
        MaterialType.ReferenceBuffer => MaterialUnit.Bottle,
        MaterialType.DisposableTool => MaterialUnit.Piece,
        MaterialType.ReferenceStandard => MaterialUnit.Gram,
        _ => MaterialUnit.Piece
    };

    public static void ValidatePurity(MaterialType type, decimal? purity)
    {
        if (type == MaterialType.ReferenceStandard)
        {
            if (!purity.HasValue)
                throw new InvalidOperationException("Purity is required for reference standards.");
            if (purity.Value <= 0m || purity.Value > 100m)
                throw new InvalidOperationException("Purity must be greater than 0 and less than or equal to 100.");
        }
        else
        {
            if (purity.HasValue)
                throw new InvalidOperationException("Purity is only allowed for reference standards.");
        }
    }

    public async Task<List<Material>> GetAllAsync(int currentUserId, MaterialType? type = null)
    {
        var scope = await _scope.GetAccessibleSectionIdsAsync(currentUserId);
        var query = _db.Materials.Include(m => m.Organism).Include(m => m.MediaProduct).AsQueryable();
        if (scope != null)
        {
            query = query.Where(m => scope.Contains(m.SectionId));
        }
        if (type.HasValue) query = query.Where(m => m.MaterialType == type.Value);
        return await query.OrderBy(m => m.MaterialType).ThenBy(m => m.MaterialName).ToListAsync();
    }

    // Print/view list per Mohamed's spec: excludes Expired and Depleted rows.
    public async Task<List<Material>> GetForPrintAsync(int currentUserId)
    {
        var all = await GetAllAsync(currentUserId);
        return all.Where(m => m.Status == StockStatus.InStock).ToList();
    }

    public async Task<Material> CreateAsync(SaveMaterialRequest r, int currentUserId)
    {
        int? mediaProductId = null;
        string materialName = r.MaterialName;
        string? code = r.Code;

        if (r.MaterialType == MaterialType.DehydratedMedia)
        {
            if (!r.MediaProductId.HasValue)
                throw new InvalidOperationException("Choose the configured media product for this dehydrated media.");

            var product = await _db.MediaProducts.FirstOrDefaultAsync(p => p.Id == r.MediaProductId.Value)
                ?? throw new InvalidOperationException($"Media product with ID {r.MediaProductId.Value} not found.");

            mediaProductId = product.Id;
            materialName = product.Name;
            code = product.Code;
        }

        ValidatePurity(r.MaterialType, r.Purity);

        var sectionId = await _scope.ResolveSectionForCreateAsync(currentUserId, r.SectionId);
        var customType = await ResolveTypeAsync(sectionId, r.MaterialType, r.CustomType, checkAllowed: true);

        var entity = new Material
        {
            SectionId = sectionId,
            MaterialType = r.MaterialType,
            CustomType = customType,
            MediaProductId = mediaProductId,
            MaterialName = materialName,
            ManufacturerName = r.ManufacturerName,
            BatchNumber = r.BatchNumber,
            ReceivingDate = r.ReceivingDate,
            ExpiryDate = r.ExpiryDate,
            Code = code,
            Location = r.Location,
            AtccNumber = r.AtccNumber,
            OrganismId = r.OrganismId,
            QuantityReceived = r.QuantityReceived,
            QuantityRemaining = r.QuantityReceived, // full balance at receipt
            Unit = r.Unit,
            MinimumStockLevel = r.MinimumStockLevel,
            Purity = r.Purity,
            CreatedByUserId = currentUserId,
            CreatedAt = DateTime.UtcNow,
            LastModifiedByUserId = currentUserId,
            LastModifiedAt = DateTime.UtcNow
        };
        _db.Materials.Add(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    // Update covers catalog corrections (name, location, expiry, etc.)
    // and QuantityReceived is editable here for genuine receiving
    // corrections - but QuantityRemaining only moves via ConsumeAsync,
    // so an edit here re-bases the remaining balance by the same delta
    // rather than silently resetting consumption history.
    public async Task UpdateAsync(int id, SaveMaterialRequest r, int currentUserId)
    {
        await _scope.EnsureMaterialAccessAsync(currentUserId, id);

        var entity = await _db.Materials.FindAsync(id)
            ?? throw new InvalidOperationException($"Material {id} not found.");

        int? mediaProductId = null;
        string materialName = r.MaterialName;
        string? code = r.Code;

        if (r.MaterialType == MaterialType.DehydratedMedia)
        {
            if (!r.MediaProductId.HasValue)
                throw new InvalidOperationException("Choose the configured media product for this dehydrated media.");

            if (r.MediaProductId.Value != entity.MediaProductId)
            {
                // Linking a batch that has no product yet is always allowed -
                // media preparation refuses unlinked batches, so a legacy batch
                // that already has lots must still be linkable. Moving a linked
                // batch to a different product is not, once lots cite it.
                if (entity.MediaProductId != null && await _db.Media.AnyAsync(m => m.MaterialId == entity.Id))
                    throw new InvalidOperationException($"Lots have already been prepared from batch {entity.BatchNumber} - its media product can't be changed.");

                var product = await _db.MediaProducts.FirstOrDefaultAsync(p => p.Id == r.MediaProductId.Value)
                    ?? throw new InvalidOperationException($"Media product with ID {r.MediaProductId.Value} not found.");

                mediaProductId = product.Id;
                materialName = product.Name;
                code = product.Code;
            }
            else
            {
                mediaProductId = entity.MediaProductId;
                materialName = entity.MaterialName;
                code = entity.Code;
            }
        }

        ValidatePurity(r.MaterialType, r.Purity);

        // An existing batch keeps its type even if the lab's list has since
        // changed; only a change of type is checked against the list.
        var customType = await ResolveTypeAsync(entity.SectionId, r.MaterialType, r.CustomType,
            checkAllowed: r.MaterialType != entity.MaterialType);

        var receivedDelta = r.QuantityReceived - entity.QuantityReceived;

        entity.MaterialType = r.MaterialType;
        entity.CustomType = customType;
        entity.MediaProductId = mediaProductId;
        entity.MaterialName = materialName;
        entity.ManufacturerName = r.ManufacturerName;
        entity.BatchNumber = r.BatchNumber;
        entity.ReceivingDate = r.ReceivingDate;
        entity.ExpiryDate = r.ExpiryDate;
        entity.Code = code;
        entity.Location = r.Location;
        entity.AtccNumber = r.AtccNumber;
        entity.OrganismId = r.OrganismId;
        entity.QuantityReceived = r.QuantityReceived;
        entity.QuantityRemaining += receivedDelta;
        entity.Unit = r.Unit;
        entity.MinimumStockLevel = r.MinimumStockLevel;
        entity.Purity = r.Purity;
        entity.LastModifiedByUserId = currentUserId;
        entity.LastModifiedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task<MaterialTypeOptions> GetTypeOptionsAsync(int currentUserId, int? sectionId)
    {
        var id = await _scope.ResolveSectionForCreateAsync(currentUserId, sectionId);
        var code = await _db.DocumentSections.Where(s => s.Id == id).Select(s => s.Code).FirstOrDefaultAsync();
        var custom = await _db.Materials.AsNoTracking()
            .Where(m => m.SectionId == id && m.CustomType != null)
            .Select(m => m.CustomType!)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync();
        return new MaterialTypeOptions(MaterialTypeRules.BuiltInTypesFor(code), custom);
    }

    // Checks the type against the lab's list and returns the custom type
    // name to store: trimmed, and spelled like an existing one in the same
    // lab when it differs only by case, so "Solvent" and "solvent" stay one
    // type. Null unless the type is Other.
    private async Task<string?> ResolveTypeAsync(int sectionId, MaterialType type, string? customType, bool checkAllowed)
    {
        var custom = MaterialTypeRules.NormalizeCustomType(customType);
        if (custom != null && type != MaterialType.Other)
            throw new InvalidOperationException("A custom material type can only be saved with the type Other.");

        if (checkAllowed)
        {
            var code = await _db.DocumentSections.Where(s => s.Id == sectionId).Select(s => s.Code).FirstOrDefaultAsync();
            if (!MaterialTypeRules.BuiltInTypesFor(code).Contains(type))
                throw new InvalidOperationException($"{MaterialTypeRules.LabelOf(type)} is not a material type of this laboratory.");
        }

        if (custom == null) return null;
        if (custom.Length > MaterialTypeRules.CustomTypeMaxLength)
            throw new InvalidOperationException($"A material type name can be at most {MaterialTypeRules.CustomTypeMaxLength} characters.");
        if (Enum.GetValues<MaterialType>().Any(t =>
                string.Equals(MaterialTypeRules.LabelOf(t), custom, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t.ToString(), custom, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"'{custom}' is the name of a built-in material type - pick it from the list or use a different name.");

        var lower = custom.ToLower();
        var existing = await _db.Materials
            .Where(m => m.SectionId == sectionId && m.CustomType != null && m.CustomType.ToLower() == lower)
            .Select(m => m.CustomType)
            .FirstOrDefaultAsync();
        return existing ?? custom;
    }

    // Suitability Run picker (REQ-FP-012): usable (in stock, not expired) reference standards in the caller's sections.
    public async Task<List<Material>> GetUsableReferenceStandardsAsync(int currentUserId)
    {
        var scope = await _scope.GetAccessibleSectionIdsAsync(currentUserId);
        var query = _db.Materials.AsNoTracking().AsQueryable();
        if (scope != null)
        {
            query = query.Where(m => scope.Contains(m.SectionId));
        }
        query = query.Where(m => m.MaterialType == MaterialType.ReferenceStandard);

        var today = DateTime.UtcNow.Date;
        query = query.Where(m => m.QuantityRemaining > 0 && (!m.ExpiryDate.HasValue || m.ExpiryDate.Value.Date >= today));

        return await query.OrderBy(m => m.MaterialName).ThenBy(m => m.BatchNumber).ToListAsync();
    }

    // Material types that require at least one current COA before consumption.
    private static readonly HashSet<MaterialType> CoaRequiredTypes = new()
    {
        MaterialType.DehydratedMedia,
        MaterialType.LyophilizedMicroorganism,
        MaterialType.Supplement
    };

    // Consumption guard + decrement, called from MediaPreparationService.
    // Throws (no partial write - caller's SaveChanges hasn't happened
    // yet) if the material is expired, wrong type, missing a required COA,
    // or doesn't have enough remaining quantity.
    public async Task<Material> ConsumeAsync(int materialId, MaterialType expectedType, decimal quantityUsed, int currentUserId)
    {
        await _scope.EnsureMaterialAccessAsync(currentUserId, materialId);

        var material = await _db.Materials.FindAsync(materialId)
            ?? throw new InvalidOperationException($"Material {materialId} not found.");

        if (material.MaterialType != expectedType)
            throw new InvalidOperationException($"Material {material.MaterialName} is not a {expectedType} item.");

        if (material.ExpiryDate.HasValue && material.ExpiryDate.Value.Date < DateTime.UtcNow.Date)
            throw new InvalidOperationException($"Material {material.MaterialName} (batch {material.BatchNumber}) is expired and cannot be used.");

        // COA requirement: DehydratedMedia, LyophilizedMicroorganism, and Supplement
        // must have at least one Current COA on file before they can be consumed.
        if (CoaRequiredTypes.Contains(material.MaterialType))
        {
            var hasCurrentCoa = await _db.MaterialDocuments.AnyAsync(d =>
                d.MaterialId == materialId &&
                d.DocumentType == MaterialDocumentType.COA &&
                d.Status == MaterialDocumentStatus.Current);

            if (!hasCurrentCoa)
                throw new InvalidOperationException(
                    $"A current Certificate of Analysis (COA) is required before {material.MaterialName} " +
                    $"(batch {material.BatchNumber}) can be used. Please upload a valid COA in the Inventory module first.");
        }

        if (material.QuantityRemaining < quantityUsed)
            throw new InvalidOperationException(
                $"Insufficient stock: {material.MaterialName} (batch {material.BatchNumber}) has {material.QuantityRemaining} {material.Unit} remaining, {quantityUsed} requested.");

        material.QuantityRemaining -= quantityUsed;
        material.LastModifiedByUserId = currentUserId;
        material.LastModifiedAt = DateTime.UtcNow;
        // Not saved here - the caller (e.g. MediaPreparationService.PrepareAsync)
        // saves this change in the same SaveChangesAsync as the new Media row,
        // so the consumption and the thing that consumed it commit atomically.
        return material;
    }
}
