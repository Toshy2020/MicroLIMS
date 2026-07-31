using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record SaveEquipmentInventoryRequest(
    string InstrumentType, string ManufacturerName, string? SerialNumber, string? FirmwareVersion,
    string Code, string Location, DateTime? CalibrationDueDate, EquipmentOperationalStatus Status);

// Equipment register (Inventory module) - every instrument in the
// Microbiology lab with serial number, firmware, and calibration due
// date. Scoped to Microbiology only per Mohamed's confirmed scope
// (Physiochemical/R&D/Sampling instruments from the source list are out
// of scope for this module). See EquipmentInventory.cs for why this is
// a separate entity from the workflow-linked Equipment master data.
public class EquipmentInventoryService
{
    private readonly MicroLimsDbContext _db;

    public EquipmentInventoryService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<List<EquipmentInventory>> GetAllAsync() =>
        await _db.EquipmentInventories.OrderBy(e => e.InstrumentType).ThenBy(e => e.Code).ToListAsync();

    // Print/view list per Mohamed's spec: excludes retired/out-of-service
    // instruments (the equivalent of "expired/out of stock" for equipment).
    public async Task<List<EquipmentInventory>> GetForPrintAsync() =>
        await _db.EquipmentInventories
            .Where(e => e.Status == EquipmentOperationalStatus.InService)
            .OrderBy(e => e.InstrumentType).ThenBy(e => e.Code)
            .ToListAsync();

    public async Task<EquipmentInventory> CreateAsync(SaveEquipmentInventoryRequest r, int currentUserId)
    {
        if (await _db.EquipmentInventories.AnyAsync(e => e.Code == r.Code))
            throw new InvalidOperationException($"Equipment code \"{r.Code}\" already exists.");

        var entity = new EquipmentInventory
        {
            InstrumentType = r.InstrumentType, ManufacturerName = r.ManufacturerName, SerialNumber = r.SerialNumber,
            FirmwareVersion = r.FirmwareVersion, Code = r.Code, Location = r.Location,
            CalibrationDueDate = r.CalibrationDueDate, Status = r.Status,
            CreatedByUserId = currentUserId, CreatedAt = DateTime.UtcNow,
            LastModifiedByUserId = currentUserId, LastModifiedAt = DateTime.UtcNow
        };
        _db.EquipmentInventories.Add(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(int id, SaveEquipmentInventoryRequest r, int currentUserId)
    {
        var entity = await _db.EquipmentInventories.FindAsync(id)
            ?? throw new InvalidOperationException($"Equipment {id} not found.");

        if (r.Code != entity.Code && await _db.EquipmentInventories.AnyAsync(e => e.Code == r.Code))
            throw new InvalidOperationException($"Equipment code \"{r.Code}\" already exists.");

        entity.InstrumentType = r.InstrumentType;
        entity.ManufacturerName = r.ManufacturerName;
        entity.SerialNumber = r.SerialNumber;
        entity.FirmwareVersion = r.FirmwareVersion;
        entity.Code = r.Code;
        entity.Location = r.Location;
        entity.CalibrationDueDate = r.CalibrationDueDate;
        entity.Status = r.Status;
        entity.LastModifiedByUserId = currentUserId;
        entity.LastModifiedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }
}
