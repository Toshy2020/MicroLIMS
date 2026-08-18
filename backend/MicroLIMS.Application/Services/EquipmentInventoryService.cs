using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record SaveEquipmentInventoryRequest(
    string InstrumentType,
    string ManufacturerName,
    string? SerialNumber,
    string? FirmwareVersion,
    string Code,
    string Location,
    DateTime? CalibrationDueDate,
    EquipmentOperationalStatus Status,
    string? StatusChangeComment = null);

public record EquipmentStatusHistoryDto(
    int Id,
    int EquipmentInventoryId,
    EquipmentOperationalStatus PreviousStatus,
    EquipmentOperationalStatus NewStatus,
    string Comment,
    int ChangedByUserId,
    string ChangedByName,
    DateTime ChangedAt);

// Equipment register (Inventory module) - every instrument in the
// Microbiology lab with serial number, firmware, and calibration due
// date.
public class EquipmentInventoryService
{
    private readonly MicroLimsDbContext _db;

    public EquipmentInventoryService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<List<EquipmentInventory>> GetAllAsync() =>
        await _db.EquipmentInventories.OrderBy(e => e.InstrumentType).ThenBy(e => e.Code).ToListAsync();

    public async Task<EquipmentInventory?> GetByIdAsync(int id) =>
        await _db.EquipmentInventories.FirstOrDefaultAsync(e => e.Id == id);

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
            InstrumentType = r.InstrumentType,
            ManufacturerName = r.ManufacturerName,
            SerialNumber = r.SerialNumber,
            FirmwareVersion = r.FirmwareVersion,
            Code = r.Code,
            Location = r.Location,
            CalibrationDueDate = r.CalibrationDueDate,
            Status = r.Status,
            CreatedByUserId = currentUserId,
            CreatedAt = DateTime.UtcNow,
            LastModifiedByUserId = currentUserId,
            LastModifiedAt = DateTime.UtcNow
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

        // If operational status is changing, enforce mandatory comment and record immutable status history.
        if (r.Status != entity.Status)
        {
            if (string.IsNullOrWhiteSpace(r.StatusChangeComment))
                throw new InvalidOperationException("A comment explaining the reason for changing the operational status is required.");

            var history = new EquipmentStatusHistory
            {
                EquipmentInventoryId = entity.Id,
                PreviousStatus = entity.Status,
                NewStatus = r.Status,
                Comment = r.StatusChangeComment.Trim(),
                ChangedByUserId = currentUserId,
                ChangedAt = DateTime.UtcNow
            };
            _db.EquipmentStatusHistories.Add(history);
            entity.Status = r.Status;
        }

        entity.InstrumentType = r.InstrumentType;
        entity.ManufacturerName = r.ManufacturerName;
        entity.SerialNumber = r.SerialNumber;
        entity.FirmwareVersion = r.FirmwareVersion;
        entity.Code = r.Code;
        entity.Location = r.Location;
        entity.CalibrationDueDate = r.CalibrationDueDate;
        entity.LastModifiedByUserId = currentUserId;
        entity.LastModifiedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task<List<EquipmentStatusHistoryDto>> GetStatusHistoryAsync(int equipmentId)
    {
        var equipmentExists = await _db.EquipmentInventories.AnyAsync(e => e.Id == equipmentId);
        if (!equipmentExists)
            throw new InvalidOperationException($"Equipment {equipmentId} not found.");

        var historyList = await _db.EquipmentStatusHistories
            .Where(h => h.EquipmentInventoryId == equipmentId)
            .OrderByDescending(h => h.ChangedAt)
            .ToListAsync();

        var userIds = historyList.Select(h => h.ChangedByUserId).Distinct().ToList();
        var userMap = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        return historyList.Select(h => new EquipmentStatusHistoryDto(
            h.Id,
            h.EquipmentInventoryId,
            h.PreviousStatus,
            h.NewStatus,
            h.Comment,
            h.ChangedByUserId,
            userMap.GetValueOrDefault(h.ChangedByUserId, "Unknown"),
            h.ChangedAt)).ToList();
    }
}
