using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record UpdateIncubatorSetPointRequest(decimal NewSetPoint, string Reason);

public record IncubatorSetPointHistoryDto(
    int Id,
    int EquipmentId,
    decimal PreviousSetPoint,
    decimal NewSetPoint,
    string Reason,
    int ChangedByUserId,
    string ChangedByName,
    DateTime ChangedAt);

public record SaveAutoclaveProgramRequest(
    int? Id,
    int EquipmentId,
    string ProgramCode,
    string ProgramName,
    string LoadType,
    decimal Temperature,
    int CycleTimeMinutes,
    bool IsActive = true,
    string? Comment = null);

public record AutoclaveProgramDto(
    int Id,
    int EquipmentId,
    string AutoclaveCode,
    string AutoclaveName,
    string ProgramCode,
    string ProgramName,
    string LoadType,
    decimal Temperature,
    int CycleTimeMinutes,
    bool IsActive,
    int CreatedByUserId,
    DateTime CreatedAt,
    int LastModifiedByUserId,
    DateTime LastModifiedAt);

public record AutoclaveProgramHistoryDto(
    int Id,
    int AutoclaveProgramId,
    string Action,
    string ProgramCode,
    string PreviousProgramName,
    string NewProgramName,
    string PreviousLoadType,
    string NewLoadType,
    decimal PreviousTemperature,
    decimal NewTemperature,
    int PreviousCycleTimeMinutes,
    int NewCycleTimeMinutes,
    bool PreviousIsActive,
    bool NewIsActive,
    string Comment,
    int ChangedByUserId,
    string ChangedByName,
    DateTime ChangedAt);

public record EquipmentConfigurationSummaryDto(
    int Id,
    string Name,
    string Code,
    EquipmentType Type,
    string? Location,
    decimal? SetPointTemperature,
    DateTime? CalibrationDueDate,
    int? EquipmentInventoryId,
    string? InventoryStatus,
    string? InventoryLocation,
    int ConfiguredProgramCount,
    string? SerialNumber = null,
    string? ManufacturerName = null);

public class EquipmentConfigurationService
{
    private readonly MicroLimsDbContext _db;

    public EquipmentConfigurationService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<List<EquipmentConfigurationSummaryDto>> GetConfiguredEquipmentSummaryAsync()
    {
        var masterEquipment = await _db.Equipment.OrderBy(e => e.Type).ThenBy(e => e.Code).ToListAsync();
        var inventoryEquipment = await _db.EquipmentInventories.ToListAsync();
        var allPrograms = await _db.AutoclavePrograms.ToListAsync();
        var programCounts = allPrograms
            .GroupBy(p => p.EquipmentId)
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new List<EquipmentConfigurationSummaryDto>();

        foreach (var eq in masterEquipment)
        {
            var inv = inventoryEquipment.FirstOrDefault(i => string.Equals(i.Code, eq.Code, StringComparison.OrdinalIgnoreCase));
            int pCount = programCounts.GetValueOrDefault(eq.Id, 0);

            result.Add(new EquipmentConfigurationSummaryDto(
                eq.Id,
                eq.Name,
                eq.Code,
                eq.Type,
                eq.Location ?? inv?.Location,
                eq.SetPointTemperature,
                eq.CalibrationDueDate ?? inv?.CalibrationDueDate,
                inv?.Id,
                inv?.Status.ToString() ?? "InService",
                inv?.Location ?? eq.Location,
                pCount,
                inv?.SerialNumber,
                inv?.ManufacturerName
            ));
        }

        return result;
    }

    public async Task<Equipment> LinkInventoryEquipmentToMasterAsync(int inventoryEquipmentId, int userId)
    {
        var inv = await _db.EquipmentInventories.FindAsync(inventoryEquipmentId)
            ?? throw new InvalidOperationException($"Inventory equipment {inventoryEquipmentId} not found.");

        var existingMaster = await _db.Equipment.FirstOrDefaultAsync(e => string.Equals(e.Code, inv.Code, StringComparison.OrdinalIgnoreCase));
        if (existingMaster != null)
            return existingMaster;

        EquipmentType type = inv.InstrumentType.Contains("Incubator", StringComparison.OrdinalIgnoreCase) ? EquipmentType.Incubator
                           : inv.InstrumentType.Contains("Autoclave", StringComparison.OrdinalIgnoreCase) ? EquipmentType.Autoclave
                           : inv.InstrumentType.Contains("Cabinet", StringComparison.OrdinalIgnoreCase) ? EquipmentType.LafCabinet
                           : EquipmentType.Other;

        var master = new Equipment
        {
            Name = inv.InstrumentType,
            Code = inv.Code,
            Type = type,
            Location = inv.Location,
            CalibrationDueDate = inv.CalibrationDueDate,
            SetPointTemperature = type == EquipmentType.Incubator ? 32.5m : null
        };

        _db.Equipment.Add(master);
        await _db.SaveChangesAsync();
        return master;
    }

    public async Task<Equipment> UpdateIncubatorSetPointAsync(int equipmentId, UpdateIncubatorSetPointRequest r, int userId)
    {
        if (string.IsNullOrWhiteSpace(r.Reason))
            throw new InvalidOperationException("A reason explaining the change to the set point temperature is required.");

        var equipment = await _db.Equipment.FirstOrDefaultAsync(e => e.Id == equipmentId)
            ?? throw new InvalidOperationException($"Equipment {equipmentId} not found.");

        if (equipment.Type != EquipmentType.Incubator)
            throw new InvalidOperationException($"Equipment {equipment.Code} is not an incubator.");

        decimal prevSetPoint = equipment.SetPointTemperature ?? 0m;
        equipment.SetPointTemperature = r.NewSetPoint;

        var history = new IncubatorSetPointHistory
        {
            EquipmentId = equipment.Id,
            PreviousSetPoint = prevSetPoint,
            NewSetPoint = r.NewSetPoint,
            Reason = r.Reason.Trim(),
            ChangedByUserId = userId,
            ChangedAt = DateTime.UtcNow
        };
        _db.IncubatorSetPointHistories.Add(history);

        await _db.SaveChangesAsync();
        return equipment;
    }

    public async Task<List<IncubatorSetPointHistoryDto>> GetIncubatorSetPointHistoryAsync(int equipmentId)
    {
        var historyList = await _db.IncubatorSetPointHistories
            .Where(h => h.EquipmentId == equipmentId)
            .OrderByDescending(h => h.ChangedAt)
            .ToListAsync();

        var userIds = historyList.Select(h => h.ChangedByUserId).Distinct().ToList();
        var userMap = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        return historyList.Select(h => new IncubatorSetPointHistoryDto(
            h.Id,
            h.EquipmentId,
            h.PreviousSetPoint,
            h.NewSetPoint,
            h.Reason,
            h.ChangedByUserId,
            userMap.GetValueOrDefault(h.ChangedByUserId, "Laboratory Admin"),
            h.ChangedAt)).ToList();
    }

    public async Task<List<AutoclaveProgramDto>> GetAutoclaveProgramsAsync(int? equipmentId = null, bool? activeOnly = null)
    {
        var query = _db.AutoclavePrograms.Include(p => p.Equipment).AsQueryable();

        if (equipmentId.HasValue)
            query = query.Where(p => p.EquipmentId == equipmentId.Value);

        if (activeOnly.HasValue && activeOnly.Value)
            query = query.Where(p => p.IsActive);

        var list = await query.OrderBy(p => p.Equipment.Code).ThenBy(p => p.ProgramCode).ToListAsync();

        return list.Select(p => new AutoclaveProgramDto(
            p.Id,
            p.EquipmentId,
            p.Equipment.Code,
            p.Equipment.Name,
            p.ProgramCode,
            p.ProgramName,
            p.LoadType,
            p.Temperature,
            p.CycleTimeMinutes,
            p.IsActive,
            p.CreatedByUserId,
            p.CreatedAt,
            p.LastModifiedByUserId,
            p.LastModifiedAt)).ToList();
    }

    public async Task<AutoclaveProgramDto> SaveAutoclaveProgramAsync(SaveAutoclaveProgramRequest r, int userId)
    {
        var equipment = await _db.Equipment.FirstOrDefaultAsync(e => e.Id == r.EquipmentId && e.Type == EquipmentType.Autoclave)
            ?? throw new InvalidOperationException($"Autoclave equipment {r.EquipmentId} not found.");

        if (r.Id == null || r.Id == 0)
        {
            // Create
            if (await _db.AutoclavePrograms.AnyAsync(p => p.EquipmentId == r.EquipmentId && p.ProgramCode == r.ProgramCode))
                throw new InvalidOperationException($"Program code \"{r.ProgramCode}\" already exists on autoclave {equipment.Code}.");

            var program = new AutoclaveProgram
            {
                EquipmentId = r.EquipmentId,
                ProgramCode = r.ProgramCode.Trim().ToUpper(),
                ProgramName = r.ProgramName.Trim(),
                LoadType = r.LoadType.Trim(),
                Temperature = r.Temperature,
                CycleTimeMinutes = r.CycleTimeMinutes,
                IsActive = r.IsActive,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                LastModifiedByUserId = userId,
                LastModifiedAt = DateTime.UtcNow
            };
            _db.AutoclavePrograms.Add(program);

            var history = new AutoclaveProgramHistory
            {
                AutoclaveProgram = program,
                Action = "Created",
                ProgramCode = program.ProgramCode,
                PreviousProgramName = "",
                NewProgramName = program.ProgramName,
                PreviousLoadType = "",
                NewLoadType = program.LoadType,
                PreviousTemperature = 0,
                NewTemperature = program.Temperature,
                PreviousCycleTimeMinutes = 0,
                NewCycleTimeMinutes = program.CycleTimeMinutes,
                PreviousIsActive = false,
                NewIsActive = program.IsActive,
                Comment = r.Comment ?? "Initial program configuration",
                ChangedByUserId = userId,
                ChangedAt = DateTime.UtcNow
            };
            _db.AutoclaveProgramHistories.Add(history);

            await _db.SaveChangesAsync();

            return new AutoclaveProgramDto(
                program.Id, program.EquipmentId, equipment.Code, equipment.Name,
                program.ProgramCode, program.ProgramName, program.LoadType,
                program.Temperature, program.CycleTimeMinutes, program.IsActive,
                program.CreatedByUserId, program.CreatedAt, program.LastModifiedByUserId, program.LastModifiedAt);
        }
        else
        {
            // Update
            var program = await _db.AutoclavePrograms.FirstOrDefaultAsync(p => p.Id == r.Id.Value)
                ?? throw new InvalidOperationException($"Autoclave program {r.Id.Value} not found.");

            if (r.ProgramCode != program.ProgramCode && await _db.AutoclavePrograms.AnyAsync(p => p.EquipmentId == r.EquipmentId && p.ProgramCode == r.ProgramCode))
                throw new InvalidOperationException($"Program code \"{r.ProgramCode}\" already exists on autoclave {equipment.Code}.");

            var history = new AutoclaveProgramHistory
            {
                AutoclaveProgramId = program.Id,
                Action = "Updated",
                ProgramCode = r.ProgramCode,
                PreviousProgramName = program.ProgramName,
                NewProgramName = r.ProgramName,
                PreviousLoadType = program.LoadType,
                NewLoadType = r.LoadType,
                PreviousTemperature = program.Temperature,
                NewTemperature = r.Temperature,
                PreviousCycleTimeMinutes = program.CycleTimeMinutes,
                NewCycleTimeMinutes = r.CycleTimeMinutes,
                PreviousIsActive = program.IsActive,
                NewIsActive = r.IsActive,
                Comment = r.Comment ?? "Program configuration update",
                ChangedByUserId = userId,
                ChangedAt = DateTime.UtcNow
            };
            _db.AutoclaveProgramHistories.Add(history);

            program.ProgramCode = r.ProgramCode.Trim().ToUpper();
            program.ProgramName = r.ProgramName.Trim();
            program.LoadType = r.LoadType.Trim();
            program.Temperature = r.Temperature;
            program.CycleTimeMinutes = r.CycleTimeMinutes;
            program.IsActive = r.IsActive;
            program.LastModifiedByUserId = userId;
            program.LastModifiedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return new AutoclaveProgramDto(
                program.Id, program.EquipmentId, equipment.Code, equipment.Name,
                program.ProgramCode, program.ProgramName, program.LoadType,
                program.Temperature, program.CycleTimeMinutes, program.IsActive,
                program.CreatedByUserId, program.CreatedAt, program.LastModifiedByUserId, program.LastModifiedAt);
        }
    }

    public async Task SetAutoclaveProgramStatusAsync(int programId, bool isActive, string comment, int userId)
    {
        var program = await _db.AutoclavePrograms.Include(p => p.Equipment).FirstOrDefaultAsync(p => p.Id == programId)
            ?? throw new InvalidOperationException($"Autoclave program {programId} not found.");

        if (program.IsActive == isActive) return;

        var history = new AutoclaveProgramHistory
        {
            AutoclaveProgramId = program.Id,
            Action = "StatusChanged",
            ProgramCode = program.ProgramCode,
            PreviousProgramName = program.ProgramName,
            NewProgramName = program.ProgramName,
            PreviousLoadType = program.LoadType,
            NewLoadType = program.LoadType,
            PreviousTemperature = program.Temperature,
            NewTemperature = program.Temperature,
            PreviousCycleTimeMinutes = program.CycleTimeMinutes,
            NewCycleTimeMinutes = program.CycleTimeMinutes,
            PreviousIsActive = program.IsActive,
            NewIsActive = isActive,
            Comment = comment ?? (isActive ? "Activated program" : "Deactivated program"),
            ChangedByUserId = userId,
            ChangedAt = DateTime.UtcNow
        };
        _db.AutoclaveProgramHistories.Add(history);

        program.IsActive = isActive;
        program.LastModifiedByUserId = userId;
        program.LastModifiedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task<List<AutoclaveProgramHistoryDto>> GetAutoclaveProgramHistoryAsync(int programId)
    {
        var historyList = await _db.AutoclaveProgramHistories
            .Where(h => h.AutoclaveProgramId == programId)
            .OrderByDescending(h => h.ChangedAt)
            .ToListAsync();

        var userIds = historyList.Select(h => h.ChangedByUserId).Distinct().ToList();
        var userMap = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        return historyList.Select(h => new AutoclaveProgramHistoryDto(
            h.Id,
            h.AutoclaveProgramId,
            h.Action,
            h.ProgramCode,
            h.PreviousProgramName,
            h.NewProgramName,
            h.PreviousLoadType,
            h.NewLoadType,
            h.PreviousTemperature,
            h.NewTemperature,
            h.PreviousCycleTimeMinutes,
            h.NewCycleTimeMinutes,
            h.PreviousIsActive,
            h.NewIsActive,
            h.Comment,
            h.ChangedByUserId,
            userMap.GetValueOrDefault(h.ChangedByUserId, "Laboratory Admin"),
            h.ChangedAt)).ToList();
    }
}
