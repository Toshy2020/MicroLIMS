using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.Responses;

namespace MicroLIMS.Application.Services;

public record SaveMaterialRequest(
    MaterialType MaterialType, string MaterialName, string ManufacturerName, string BatchNumber,
    DateTime ReceivingDate, DateTime? ExpiryDate, string? Code, string Location,
    decimal QuantityReceived, MaterialUnit? Unit, decimal? MinimumStockLevel, string? AtccNumber, int? OrganismId,
    int? MediaProductId = null, int? SectionId = null, decimal? Purity = null, string? CustomType = null,
    int? MaterialMasterEntryId = null, decimal? MoisturePercent = null, string? StorageCondition = null,
    int? ReferenceOrganismId = null);
// Unit null = the category's default unit (Other materials) or the type's default.
// CustomType is no longer accepted for new lots; the name and code of an Other
// material come from its master entry. StorageCondition null = the category's default
// (create) / unchanged (update); blank on update clears it. A new lyophilized microorganism lot
// is received against an active ReferenceOrganismId: its OrganismId, AtccNumber (ATCC strains),
// name and code are derived from the strain and any sent values are ignored.

// Type picker for one laboratory: its offered built-in types plus the custom type
// names already used in its stock register (history only; no new ones are accepted).
public record MaterialTypeOptions(IReadOnlyList<MaterialType> BuiltIn, IReadOnlyList<string> Custom);

// Materials Stock register (Inventory module) - dehydrated media, discs,
// ID kits/reagents, chemicals, indicators, reference buffers, disposable
// tools. Every Create/Update flows through IMicroLimsDbContext.SaveChanges,
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
    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly TimeProvider _time;

    public MaterialService(IMicroLimsDbContext db, IUserSectionScopeService scope, TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
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
        MaterialType.WorkingStandard => MaterialUnit.Gram,
        MaterialType.PrimaryStandard => MaterialUnit.Gram,
        _ => MaterialUnit.Piece
    };

    public static void ValidatePurity(MaterialType type, decimal? purity)
    {
        if (type is MaterialType.ReferenceStandard or MaterialType.PrimaryStandard)
        {
            if (!purity.HasValue)
                throw new InvalidOperationException(type == MaterialType.PrimaryStandard
                    ? "Purity is required for primary standards." : "Purity is required for reference standards.");
            if (purity.Value <= 0m || purity.Value > 100m)
                throw new InvalidOperationException("Purity must be greater than 0 and less than or equal to 100.");
        }
        else
        {
            if (purity.HasValue)
                throw new InvalidOperationException("Purity is only allowed for reference and primary standards.");
        }
    }

    // Chemical, Indicator, ReferenceStandard, PrimaryStandard and Other lots must reference a
    // MaterialMasterEntry (HPLC chain S1, spec 3.1); existing rows stay unlinked (D9).
    public static bool RequiresMasterEntry(MaterialType t) =>
        t is MaterialType.Chemical or MaterialType.Indicator or MaterialType.ReferenceStandard or MaterialType.PrimaryStandard
            or MaterialType.Other;

    // New lots of Other materials also need an entry (RequiresMasterEntry(Other) is true);
    // the six legacy named types are not received any more (see CreateAsync).
    private static MaterialMasterCategory CategoryFor(MaterialType t) => t switch
    {
        MaterialType.Indicator => MaterialMasterCategory.Indicator,
        MaterialType.ReferenceStandard => MaterialMasterCategory.ReferenceStandard,
        MaterialType.PrimaryStandard => MaterialMasterCategory.PrimaryStandard,
        _ when MaterialTypeRules.IsOtherMaterial(t) => MaterialMasterCategory.Other,
        _ => MaterialMasterCategory.Reagent
    };

    // Loads and checks the entry; allowInactive is true only when the lot already points at it.
    // An Other material's entry also needs an active category offered to the lot's section.
    private async Task<MaterialMasterEntry> ResolveMasterEntryAsync(int entryId, MaterialType type, int sectionId, bool allowInactive)
    {
        var entry = await _db.MaterialMasterEntries.Include(e => e.MaterialCategory).ThenInclude(c => c!.Sections)
            .FirstOrDefaultAsync(e => e.Id == entryId)
            ?? throw new InvalidOperationException($"Material master entry {entryId} not found.");
        if (!entry.IsActive && !allowInactive) throw new InvalidOperationException($"Master entry \"{entry.Code}\" is inactive.");
        if (entry.Category != CategoryFor(type)) throw new InvalidOperationException($"Master entry \"{entry.Code}\" is a {entry.Category}, not a {CategoryFor(type)}.");

        if (entry.Category == MaterialMasterCategory.Other && !allowInactive)
        {
            var category = entry.MaterialCategory
                ?? throw new InvalidOperationException($"Master entry \"{entry.Code}\" has no material category.");
            if (!category.IsActive)
                throw new InvalidOperationException($"Material category \"{category.Name}\" is inactive.");
            if (category.Sections.Count > 0 && category.Sections.All(s => s.SectionId != sectionId))
                throw new InvalidOperationException($"Material category \"{category.Name}\" is not available in this laboratory.");
        }
        return entry;
    }

    // Loads the strain; allowInactive is true only when the lot already points at it.
    private async Task<ReferenceOrganism> ResolveReferenceOrganismAsync(int id, bool allowInactive)
    {
        var strain = await _db.ReferenceOrganisms.Include(r => r.Organism).FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new InvalidOperationException($"Reference organism {id} not found.");
        if (!strain.IsActive && !allowInactive)
            throw new InvalidOperationException($"Reference organism \"{strain.Code}\" ({ReferenceOrganismNaming.NameOf(strain)}) is inactive.");
        return strain;
    }

    public static void ValidateMoisture(MaterialType type, decimal? moisture)
    {
        if (!moisture.HasValue) return;
        if (type != MaterialType.ReferenceStandard) throw new InvalidOperationException("Moisture content is only allowed for reference standards.");
        if (moisture.Value < 0m || moisture.Value >= 100m) throw new InvalidOperationException("Moisture content must be at least 0 and below 100.");
    }

    public async Task<List<MaterialResponse>> GetAllAsync(int currentUserId, MaterialType? type = null)
    {
        var scope = await _scope.GetReadableSectionIdsAsync(currentUserId);
        var query = _db.Materials.Include(m => m.Organism).Include(m => m.MediaProduct).Include(m => m.ReferenceOrganism)
            .Include(m => m.MaterialMasterEntry).ThenInclude(e => e!.MaterialCategory).AsQueryable();
        if (scope != null)
        {
            query = query.Where(m => scope.Contains(m.SectionId));
        }
        if (type.HasValue) query = query.Where(m => m.MaterialType == type.Value);
        return (await query.OrderBy(m => m.MaterialType).ThenBy(m => m.MaterialName).ToListAsync()).Select(MaterialResponse.From).ToList();
    }

    // Print/view list per Mohamed's spec: excludes Expired and Depleted rows.
    public async Task<List<MaterialResponse>> GetForPrintAsync(int currentUserId)
    {
        var all = await GetAllAsync(currentUserId);
        return all.Where(m => m.Status == StockStatus.InStock).ToList();
    }

    public async Task<MaterialResponse> CreateAsync(SaveMaterialRequest r, int currentUserId)
    {
        if (r.MaterialType == MaterialType.WorkingStandard)
            throw new InvalidOperationException("Working standards are created by approving a qualification, not received here.");
        if (MaterialTypeRules.IsLegacyNamedType(r.MaterialType))
            throw new InvalidOperationException("Receive this as an Other material: choose its master entry.");
        if (MaterialTypeRules.NormalizeCustomType(r.CustomType) != null)
            throw new InvalidOperationException("A free-text material type is no longer accepted: receive it as an Other material and choose its master entry.");

        int? mediaProductId = null;
        string materialName = r.MaterialName;
        string? code = r.Code;

        if (r.MaterialType == MaterialType.DehydratedMedia)
        {
            if (!r.MediaProductId.HasValue)
                throw new InvalidOperationException("Choose the configured media product for this dehydrated media.");

            var product = await _db.MediaProducts.FirstOrDefaultAsync(p => p.Id == r.MediaProductId.Value)
                ?? throw new InvalidOperationException($"Media product with ID {r.MediaProductId.Value} not found.");
            if (!product.IsActive)
                throw new InvalidOperationException($"Media product \"{product.Name}\" is inactive and can't be chosen for new lots.");

            mediaProductId = product.Id;
            materialName = product.Name;
            code = product.Code;
        }

        // Lyophilized microorganisms are received against a reference strain (no free-text organism):
        // the species, ATCC number, name and code of the lot all come from it.
        ReferenceOrganism? strain = null;
        var organismId = r.OrganismId;
        var atccNumber = r.AtccNumber;
        if (r.MaterialType == MaterialType.LyophilizedMicroorganism)
        {
            if (!r.ReferenceOrganismId.HasValue)
                throw new InvalidOperationException("Choose the reference organism (strain) for this lyophilized microorganism.");
            strain = await ResolveReferenceOrganismAsync(r.ReferenceOrganismId.Value, allowInactive: false);
            if (r.OrganismId.HasValue && r.OrganismId.Value != strain.OrganismId)
                throw new InvalidOperationException("The organism does not match the chosen reference organism.");
            organismId = strain.OrganismId;
            atccNumber = ReferenceOrganismNaming.AtccNumberOf(strain);
            materialName = ReferenceOrganismNaming.NameOf(strain);
            code = strain.Code;
        }
        else if (r.ReferenceOrganismId.HasValue)
        {
            throw new InvalidOperationException("A reference organism can only be chosen for lyophilized microorganisms.");
        }

        ValidatePurity(r.MaterialType, r.Purity);
        ValidateMoisture(r.MaterialType, r.MoisturePercent);

        var sectionId = await _scope.ResolveSectionForCreateAsync(currentUserId, r.SectionId);
        await EnsureTypeOfferedAsync(sectionId, r.MaterialType);

        int? masterEntryId = null;
        MaterialCategory? category = null;
        if (RequiresMasterEntry(r.MaterialType))
        {
            if (!r.MaterialMasterEntryId.HasValue)
                throw new InvalidOperationException("Choose the material master entry for this lot.");

            var entry = await ResolveMasterEntryAsync(r.MaterialMasterEntryId.Value, r.MaterialType, sectionId, allowInactive: false);
            masterEntryId = entry.Id;
            materialName = entry.Name;
            code = entry.Code;
            category = entry.MaterialCategory;
        }

        if (category is { ExpiryRequired: true } && !r.ExpiryDate.HasValue)
            throw new InvalidOperationException($"An expiry date is required for {category.Name} materials.");

        var storageCondition = NormalizeStorage(r.StorageCondition) ?? category?.DefaultStorageCondition;

        var entity = new Material
        {
            SectionId = sectionId,
            MaterialType = r.MaterialType,
            MediaProductId = mediaProductId,
            MaterialMasterEntryId = masterEntryId,
            MaterialName = materialName,
            ManufacturerName = r.ManufacturerName,
            BatchNumber = r.BatchNumber,
            ReceivingDate = r.ReceivingDate,
            ExpiryDate = r.ExpiryDate,
            Code = code,
            Location = r.Location,
            AtccNumber = atccNumber,
            OrganismId = organismId,
            ReferenceOrganismId = strain?.Id,
            ReferenceOrganism = strain,
            QuantityReceived = r.QuantityReceived,
            QuantityRemaining = r.QuantityReceived, // full balance at receipt
            Unit = r.Unit ?? category?.DefaultUnit ?? DefaultUnitFor(r.MaterialType),
            MinimumStockLevel = r.MinimumStockLevel,
            Purity = r.Purity,
            MoisturePercent = r.MoisturePercent,
            StorageCondition = storageCondition,
            // A category that requires approval before use holds the lot until a second person releases it.
            ApprovalStatus = category is { ApprovalBeforeUse: true } ? ApprovalGateStatus.PendingReview : null,
            CreatedByUserId = currentUserId,
            CreatedAt = _time.GetUtcNow().UtcDateTime,
            LastModifiedByUserId = currentUserId,
            LastModifiedAt = _time.GetUtcNow().UtcDateTime
        };
        _db.Materials.Add(entity);
        await _db.SaveChangesAsync();
        return MaterialResponse.From(entity);
    }

    private static string? NormalizeStorage(string? value)
    {
        var trimmed = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (trimmed is { Length: > 100 }) throw new InvalidOperationException("Storage condition can be at most 100 characters.");
        return trimmed;
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
            ?? throw new NotFoundException($"Material {id} not found.");
        RecordVersion.EnsureCurrent(_db, entity);

        if (entity.MaterialType == MaterialType.WorkingStandard || r.MaterialType == MaterialType.WorkingStandard)
            throw new InvalidOperationException("Working standards change only through their qualifications.");
        // A lot already received under a legacy named type keeps it (stored type kept); no lot moves onto one.
        if (r.MaterialType != entity.MaterialType && MaterialTypeRules.IsLegacyNamedType(r.MaterialType))
            throw new InvalidOperationException("Receive this as an Other material: choose its master entry.");

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
                if (!product.IsActive)
                    throw new InvalidOperationException($"Media product \"{product.Name}\" is inactive and can't be chosen for new lots.");

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

        // A lot with a reference strain keeps it (never cleared by an update without one); setting or
        // changing the strain re-derives the species, ATCC number, name and code. A lot without a
        // strain stays editable with the organism fields as before.
        var referenceOrganismId = entity.ReferenceOrganismId;
        var organismId = r.OrganismId;
        var atccNumber = r.AtccNumber;
        if (r.MaterialType == MaterialType.LyophilizedMicroorganism)
        {
            var requestedStrainId = r.ReferenceOrganismId ?? entity.ReferenceOrganismId;
            if (requestedStrainId.HasValue)
            {
                var sameStrain = requestedStrainId == entity.ReferenceOrganismId;
                var strain = await ResolveReferenceOrganismAsync(requestedStrainId.Value, allowInactive: sameStrain);
                if (!sameStrain && entity.ReferenceOrganismId.HasValue && await _db.Cryovials.AnyAsync(c => c.MaterialId == entity.Id))
                    throw new InvalidOperationException($"Cryovials have already been prepared from lot {entity.BatchNumber} - its reference organism can't be changed.");
                if (r.OrganismId.HasValue && r.OrganismId.Value != strain.OrganismId)
                    throw new InvalidOperationException("The organism does not match the chosen reference organism.");

                referenceOrganismId = strain.Id;
                organismId = strain.OrganismId;
                atccNumber = ReferenceOrganismNaming.AtccNumberOf(strain);
                if (sameStrain)
                {
                    materialName = entity.MaterialName;
                    code = entity.Code;
                }
                else
                {
                    materialName = ReferenceOrganismNaming.NameOf(strain);
                    code = strain.Code;
                }
            }
        }
        else if (r.ReferenceOrganismId.HasValue)
        {
            throw new InvalidOperationException("A reference organism can only be chosen for lyophilized microorganisms.");
        }
        else
        {
            referenceOrganismId = null;
        }

        ValidatePurity(r.MaterialType, r.Purity);
        ValidateMoisture(r.MaterialType, r.MoisturePercent);

        // An existing batch keeps its type even if the lab's list has since
        // changed; only a change of type is checked against the list.
        var typeChanged = r.MaterialType != entity.MaterialType;
        if (typeChanged) await EnsureTypeOfferedAsync(entity.SectionId, r.MaterialType);

        // The stored custom type of an old lot stays as it is; no new one is accepted.
        var requestedCustom = MaterialTypeRules.NormalizeCustomType(r.CustomType);
        if (requestedCustom != null && !string.Equals(requestedCustom, entity.CustomType, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A free-text material type is no longer accepted: choose a master entry of an Other category.");
        var customType = typeChanged ? null : entity.CustomType;

        var masterEntryId = entity.MaterialMasterEntryId;
        MaterialCategory? category = null;
        if (r.MaterialMasterEntryId.HasValue)
        {
            var sameEntry = r.MaterialMasterEntryId.Value == entity.MaterialMasterEntryId;
            var entry = await ResolveMasterEntryAsync(r.MaterialMasterEntryId.Value, r.MaterialType, entity.SectionId, allowInactive: sameEntry);
            category = entry.MaterialCategory;

            if (!sameEntry && entity.MaterialMasterEntryId.HasValue && MaterialTypeRules.IsOtherMaterial(r.MaterialType))
            {
                var currentCategoryId = await _db.MaterialMasterEntries.Where(e => e.Id == entity.MaterialMasterEntryId.Value)
                    .Select(e => e.MaterialCategoryId).FirstOrDefaultAsync();
                if (currentCategoryId != entry.MaterialCategoryId)
                    throw new InvalidOperationException("A lot can only move to another entry of the same material category.");
            }

            masterEntryId = entry.Id;
            // The lot's name/code are a snapshot of the entry at receipt: kept while it still points at the same entry.
            if (!sameEntry || !MaterialTypeRules.IsOtherMaterial(r.MaterialType))
            {
                materialName = entry.Name;
                code = entry.Code;
            }
            else
            {
                materialName = entity.MaterialName;
                code = entity.Code;
            }

            if (category is { ExpiryRequired: true } && !r.ExpiryDate.HasValue && (entity.ExpiryDate.HasValue || !sameEntry))
                throw new InvalidOperationException($"An expiry date is required for {category.Name} materials.");
        }
        else if (r.MaterialType == MaterialType.Other && typeChanged)
        {
            throw new InvalidOperationException("Choose the material master entry for this lot.");
        }
        // else: no entry on the request - keep the lot's existing link (or
        // none, for a legacy row created before S1). Never cleared here.

        var storageCondition = r.StorageCondition == null ? entity.StorageCondition : NormalizeStorage(r.StorageCondition);

        var receivedDelta = r.QuantityReceived - entity.QuantityReceived;

        entity.MaterialType = r.MaterialType;
        entity.CustomType = customType;
        entity.StorageCondition = storageCondition;
        entity.MediaProductId = mediaProductId;
        entity.MaterialMasterEntryId = masterEntryId;
        entity.MaterialName = materialName;
        entity.ManufacturerName = r.ManufacturerName;
        entity.BatchNumber = r.BatchNumber;
        entity.ReceivingDate = r.ReceivingDate;
        entity.ExpiryDate = r.ExpiryDate;
        entity.Code = code;
        entity.Location = r.Location;
        entity.AtccNumber = atccNumber;
        entity.OrganismId = organismId;
        entity.ReferenceOrganismId = referenceOrganismId;
        entity.QuantityReceived = r.QuantityReceived;
        entity.QuantityRemaining += receivedDelta;
        entity.Unit = r.Unit ?? entity.Unit;
        entity.MinimumStockLevel = r.MinimumStockLevel;
        entity.Purity = r.Purity;
        entity.MoisturePercent = r.MoisturePercent;
        entity.LastModifiedByUserId = currentUserId;
        entity.LastModifiedAt = _time.GetUtcNow().UtcDateTime;

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

    // The type must be one the lab's stock register offers for new lots.
    private async Task EnsureTypeOfferedAsync(int sectionId, MaterialType type)
    {
        var code = await _db.DocumentSections.Where(s => s.Id == sectionId).Select(s => s.Code).FirstOrDefaultAsync();
        if (!MaterialTypeRules.BuiltInTypesFor(code).Contains(type))
            throw new InvalidOperationException($"{MaterialTypeRules.LabelOf(type)} is not a material type of this laboratory.");
    }

    // SST picker: usable primary and working standards (in stock, not expired) in the caller's sections.
    public async Task<List<MaterialResponse>> GetUsableReferenceStandardsAsync(int currentUserId)
    {
        var scope = await _scope.GetReadableSectionIdsAsync(currentUserId);
        var query = _db.Materials.AsNoTracking().AsQueryable();
        if (scope != null)
        {
            query = query.Where(m => scope.Contains(m.SectionId));
        }
        query = query.Where(m => m.MaterialType == MaterialType.ReferenceStandard || m.MaterialType == MaterialType.WorkingStandard);

        var today = _time.GetUtcNow().UtcDateTime.Date;
        query = query.Where(m => m.QuantityRemaining > 0 && (!m.ExpiryDate.HasValue || m.ExpiryDate.Value.Date >= today)
            && (m.ApprovalStatus == null || m.ApprovalStatus == ApprovalGateStatus.Approved));

        return (await query.OrderBy(m => m.MaterialName).ThenBy(m => m.BatchNumber).ToListAsync()).Select(MaterialResponse.From).ToList();
    }

    // Consumption guard + decrement, called from MediaPreparationService.
    // Throws (no partial write - caller's SaveChanges hasn't happened
    // yet) if the material is expired, wrong type, awaiting release or rejected,
    // missing a required COA, or doesn't have enough remaining quantity.
    // A lot whose category does not track quantity is not decremented.
    public async Task<Material> ConsumeAsync(int materialId, MaterialType expectedType, decimal quantityUsed, int currentUserId)
    {
        await _scope.EnsureMaterialAccessAsync(currentUserId, materialId);

        var material = await _db.Materials.FindAsync(materialId)
            ?? throw new NotFoundException($"Material {materialId} not found.");

        if (material.MaterialType != expectedType)
            throw new InvalidOperationException($"Material {material.MaterialName} is not a {expectedType} item.");

        if (material.ExpiryDate.HasValue && material.ExpiryDate.Value.Date < _time.GetUtcNow().UtcDateTime.Date)
            throw new InvalidOperationException($"Material {material.MaterialName} (batch {material.BatchNumber}) is expired and cannot be used.");

        if (material.IsAwaitingRelease)
            throw new InvalidOperationException($"Lot {material.BatchNumber} of {material.MaterialName} is awaiting release.");
        if (material.IsRejected)
            throw new InvalidOperationException($"Lot {material.BatchNumber} of {material.MaterialName} was rejected.");

        var category = await MaterialCategoryRules.CategoryOfAsync(_db, material);

        // COA requirement: DehydratedMedia and LyophilizedMicroorganism always, and any lot
        // whose category requires one (legacy Supplement lots included), must have at least
        // one Current COA on file before they can be consumed.
        if (MaterialCategoryRules.IsCoaRequired(material.MaterialType, category))
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

        // Quantity-tracked is on by default; a category (e.g. disposable tools) can switch it off.
        if (category is { QuantityTracked: false })
            return material;

        if (material.QuantityRemaining < quantityUsed)
            throw new InvalidOperationException(
                $"Insufficient stock: {material.MaterialName} (batch {material.BatchNumber}) has {material.QuantityRemaining} {material.Unit} remaining, {quantityUsed} requested.");

        material.QuantityRemaining -= quantityUsed;
        material.LastModifiedByUserId = currentUserId;
        material.LastModifiedAt = _time.GetUtcNow().UtcDateTime;
        // Not saved here - the caller (e.g. MediaPreparationService.PrepareAsync)
        // saves this change in the same SaveChangesAsync as the new Media row,
        // so the consumption and the thing that consumed it commit atomically.
        return material;
    }
}
