using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record SaveMaterialRequest(
    MaterialType MaterialType, string MaterialName, string ManufacturerName, string BatchNumber,
    DateTime ReceivingDate, DateTime? ExpiryDate, string? Code, string Location,
    decimal QuantityReceived, MaterialUnit Unit, decimal? MinimumStockLevel);

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

    public MaterialService(MicroLimsDbContext db)
    {
        _db = db;
    }

    // Suggested default unit per material type - the analyst can still
    // override it from the full MaterialUnit dropdown on save.
    public static MaterialUnit DefaultUnitFor(MaterialType type) => type switch
    {
        MaterialType.DehydratedMedia => MaterialUnit.Gram,
        MaterialType.Supplement => MaterialUnit.Milliliter,
        MaterialType.AntibioticDisc => MaterialUnit.Disc,
        MaterialType.IdentificationKit => MaterialUnit.Kit,
        MaterialType.IdentificationReagent => MaterialUnit.Milliliter,
        MaterialType.Chemical => MaterialUnit.Gram,
        MaterialType.Indicator => MaterialUnit.Piece,
        MaterialType.ReferenceBuffer => MaterialUnit.Bottle,
        MaterialType.DisposableTool => MaterialUnit.Piece,
        _ => MaterialUnit.Piece
    };

    public async Task<List<Material>> GetAllAsync(MaterialType? type = null)
    {
        var query = _db.Materials.AsQueryable();
        if (type.HasValue) query = query.Where(m => m.MaterialType == type.Value);
        return await query.OrderBy(m => m.MaterialType).ThenBy(m => m.MaterialName).ToListAsync();
    }

    // Print/view list per Mohamed's spec: excludes Expired and Depleted rows.
    public async Task<List<Material>> GetForPrintAsync()
    {
        var all = await GetAllAsync();
        return all.Where(m => m.Status == StockStatus.InStock).ToList();
    }

    public async Task<Material> CreateAsync(SaveMaterialRequest r, int currentUserId)
    {
        var entity = new Material
        {
            MaterialType = r.MaterialType, MaterialName = r.MaterialName, ManufacturerName = r.ManufacturerName,
            BatchNumber = r.BatchNumber, ReceivingDate = r.ReceivingDate, ExpiryDate = r.ExpiryDate,
            Code = r.Code, Location = r.Location,
            QuantityReceived = r.QuantityReceived, QuantityRemaining = r.QuantityReceived, // full balance at receipt
            Unit = r.Unit, MinimumStockLevel = r.MinimumStockLevel,
            CreatedByUserId = currentUserId, CreatedAt = DateTime.UtcNow,
            LastModifiedByUserId = currentUserId, LastModifiedAt = DateTime.UtcNow
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
        var entity = await _db.Materials.FindAsync(id)
            ?? throw new InvalidOperationException($"Material {id} not found.");

        var receivedDelta = r.QuantityReceived - entity.QuantityReceived;

        entity.MaterialType = r.MaterialType;
        entity.MaterialName = r.MaterialName;
        entity.ManufacturerName = r.ManufacturerName;
        entity.BatchNumber = r.BatchNumber;
        entity.ReceivingDate = r.ReceivingDate;
        entity.ExpiryDate = r.ExpiryDate;
        entity.Code = r.Code;
        entity.Location = r.Location;
        entity.QuantityReceived = r.QuantityReceived;
        entity.QuantityRemaining += receivedDelta;
        entity.Unit = r.Unit;
        entity.MinimumStockLevel = r.MinimumStockLevel;
        entity.LastModifiedByUserId = currentUserId;
        entity.LastModifiedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    // Consumption guard + decrement, called from MediaPreparationService.
    // Throws (no partial write - caller's SaveChanges hasn't happened
    // yet) if the material is expired, wrong type, or doesn't have
    // enough remaining quantity.
    public async Task<Material> ConsumeAsync(int materialId, MaterialType expectedType, decimal quantityUsed, int currentUserId)
    {
        var material = await _db.Materials.FindAsync(materialId)
            ?? throw new InvalidOperationException($"Material {materialId} not found.");

        if (material.MaterialType != expectedType)
            throw new InvalidOperationException($"Material {material.MaterialName} is not a {expectedType} item.");

        if (material.ExpiryDate.HasValue && material.ExpiryDate.Value.Date < DateTime.UtcNow.Date)
            throw new InvalidOperationException($"Material {material.MaterialName} (batch {material.BatchNumber}) is expired and cannot be used.");

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
