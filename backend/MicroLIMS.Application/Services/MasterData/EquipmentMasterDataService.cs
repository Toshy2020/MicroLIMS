using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.MasterData;

// Master data: equipment. Behind EquipmentMasterDataController
// (api/masterdata/...); the controller only maps HTTP to these calls.
public class EquipmentMasterDataService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;

    public EquipmentMasterDataService(IMicroLimsDbContext db, IUserSectionScopeService scope)
    {
        _db = db;
        _scope = scope;
    }

    public async Task<object> GetEquipmentAsync(int currentUserId, EquipmentType? type)
    {
        var scope = await _scope.GetAccessibleSectionIdsAsync(currentUserId);
        var query = _db.Equipment.AsNoTracking().Include(e => e.Section).AsQueryable();
        if (scope != null) query = query.Where(e => scope.Contains(e.SectionId));
        if (type.HasValue) query = query.Where(e => e.Type == type.Value);
        return await query.ToListAsync();
    }

    public async Task<object> GetEquipmentByIdAsync(int currentUserId, int id)
    {
        await _scope.EnsureEquipmentAccessAsync(currentUserId, id);
        var eq = await _db.Equipment.AsNoTracking()
            .Include(e => e.Section)
            .Include(e => e.CompatibleColumns)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (eq == null)
            throw new NotFoundException($"Equipment {id} not found.");
        return eq;
    }

    public async Task<object> CreateEquipmentAsync(int currentUserId, CreateEquipmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Name is required.");
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new InvalidOperationException("Code is required.");
        if (await _db.Equipment.AnyAsync(e => e.Code == request.Code.Trim()))
            throw new InvalidOperationException($"Equipment code \"{request.Code}\" already exists.");

        if (!string.IsNullOrWhiteSpace(request.Vendor) && request.Vendor.Length > 100)
            throw new InvalidOperationException("Vendor cannot exceed 100 characters.");

        if (request.Type == EquipmentType.Hplc)
        {
            if (!request.CdsSoftware.HasValue)
                throw new InvalidOperationException("CDS Software is required for HPLC equipment.");
        }
        else if (request.Type == EquipmentType.IcpOes)
        {
            if (!request.CdsSoftware.HasValue)
                throw new InvalidOperationException("CDS Software is required for ICP-OES equipment.");
        }
        else
        {
            if (request.CdsSoftware.HasValue)
                throw new InvalidOperationException("CDS Software is only allowed for HPLC and ICP-OES equipment.");
        }

        var sectionId = await _scope.ResolveSectionForCreateAsync(currentUserId, request.SectionId);

        var entity = new Equipment
        {
            Name = request.Name.Trim(),
            Code = request.Code.Trim(),
            Type = request.Type,
            Location = request.Location,
            SetPointTemperature = request.SetPointTemperature,
            CalibrationDueDate = request.CalibrationDueDate,
            Vendor = request.Vendor?.Trim(),
            CdsSoftware = request.CdsSoftware,
            ConnectionSettings = request.ConnectionSettings,
            SectionId = sectionId
        };
        _db.Equipment.Add(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    public async Task<object> UpdateEquipmentAsync(int currentUserId, int id, UpdateEquipmentRequest request)
    {
        await _scope.EnsureEquipmentAccessAsync(currentUserId, id);

        var entity = await _db.Equipment.FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new NotFoundException($"Equipment {id} not found.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Name is required.");
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new InvalidOperationException("Code is required.");
        if (await _db.Equipment.AnyAsync(e => e.Code == request.Code.Trim() && e.Id != id))
            throw new InvalidOperationException($"Equipment code \"{request.Code}\" already exists.");

        if (!string.IsNullOrWhiteSpace(request.Vendor) && request.Vendor.Length > 100)
            throw new InvalidOperationException("Vendor cannot exceed 100 characters.");

        if (request.Type == EquipmentType.Hplc)
        {
            if (!request.CdsSoftware.HasValue)
                throw new InvalidOperationException("CDS Software is required for HPLC equipment.");
        }
        else if (request.Type == EquipmentType.IcpOes)
        {
            if (!request.CdsSoftware.HasValue)
                throw new InvalidOperationException("CDS Software is required for ICP-OES equipment.");
        }
        else
        {
            if (request.CdsSoftware.HasValue)
                throw new InvalidOperationException("CDS Software is only allowed for HPLC and ICP-OES equipment.");
        }

        if (request.SectionId.HasValue && request.SectionId.Value != entity.SectionId)
        {
            entity.SectionId = await _scope.ResolveSectionForCreateAsync(currentUserId, request.SectionId);
        }

        entity.Name = request.Name.Trim();
        entity.Code = request.Code.Trim();
        entity.Type = request.Type;
        entity.Location = request.Location;
        entity.SetPointTemperature = request.SetPointTemperature;
        entity.CalibrationDueDate = request.CalibrationDueDate;
        entity.Vendor = request.Vendor?.Trim();
        entity.CdsSoftware = request.CdsSoftware;
        entity.ConnectionSettings = request.ConnectionSettings;

        await _db.SaveChangesAsync();
        return entity;
    }
}
