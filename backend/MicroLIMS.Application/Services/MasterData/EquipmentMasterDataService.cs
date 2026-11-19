using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Services.MasterData;

// Master data: equipment. Behind EquipmentMasterDataController
// (api/masterdata/...); the controller only maps HTTP to these calls.
public class EquipmentMasterDataService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly MasterDataBatchService _batch;
    private readonly ChangeReasonGuard _reasonGuard;

    public EquipmentMasterDataService(
        IMicroLimsDbContext db,
        IUserSectionScopeService scope,
        MasterDataBatchService? batchService = null,
        ChangeReasonGuard? reasonGuard = null)
    {
        _db = db;
        _scope = scope;
        _batch = batchService ?? new MasterDataBatchService(db);
        _reasonGuard = reasonGuard ?? new ChangeReasonGuard(db, new MasterDataUsageService(db, scope));
    }

    public async Task<List<EquipmentResponse>> GetEquipmentAsync(int currentUserId, EquipmentType? type)
    {
        var scope = await _scope.GetReadableSectionIdsAsync(currentUserId);
        var query = _db.Equipment.AsNoTracking().Include(e => e.Section).AsQueryable();
        if (scope != null) query = query.Where(e => scope.Contains(e.SectionId));
        if (type.HasValue) query = query.Where(e => e.Type == type.Value);
        return (await query.ToListAsync()).Select(EquipmentResponse.From).ToList();
    }

    public async Task<EquipmentResponse> GetEquipmentByIdAsync(int currentUserId, int id)
    {
        await _scope.EnsureEquipmentAccessAsync(currentUserId, id, access: SectionAccess.Read);
        var eq = await _db.Equipment.AsNoTracking()
            .Include(e => e.Section)
            .Include(e => e.CompatibleColumns)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (eq == null)
            throw new NotFoundException($"Equipment {id} not found.");
        return EquipmentResponse.From(eq);
    }

    private async Task ValidateEquipmentInventoryLinkAsync(
        int currentUserId,
        int? equipmentInventoryId,
        int? excludeEquipmentId,
        CancellationToken cancellationToken)
    {
        if (!equipmentInventoryId.HasValue)
            return;

        var assetId = equipmentInventoryId.Value;
        await _scope.EnsureEquipmentInventoryAccessAsync(currentUserId, assetId, cancellationToken, SectionAccess.Read);

        var assetExists = await _db.EquipmentInventories
            .AsNoTracking()
            .AnyAsync(a => a.Id == assetId, cancellationToken);
        if (!assetExists)
            throw new NotFoundException($"Equipment inventory asset {assetId} not found.");

        var alreadyLinked = await _db.Equipment
            .AsNoTracking()
            .AnyAsync(e => e.EquipmentInventoryId == assetId && (excludeEquipmentId == null || e.Id != excludeEquipmentId.Value), cancellationToken);
        if (alreadyLinked)
            throw new BusinessRuleException("This asset is already linked to another instrument.");
    }

    public async Task<EquipmentResponse> CreateEquipmentAsync(int currentUserId, CreateEquipmentRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BusinessRuleException("Name is required.");
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new BusinessRuleException("Code is required.");
        if (await _db.Equipment.AnyAsync(e => e.Code == request.Code.Trim(), cancellationToken))
            throw new BusinessRuleException($"Equipment code \"{request.Code}\" already exists.");

        if (!string.IsNullOrWhiteSpace(request.Vendor) && request.Vendor.Length > 100)
            throw new BusinessRuleException("Vendor cannot exceed 100 characters.");

        if (request.Type == EquipmentType.Hplc)
        {
            if (!request.CdsSoftware.HasValue)
                throw new BusinessRuleException("CDS Software is required for HPLC equipment.");
        }
        else if (request.Type == EquipmentType.IcpOes)
        {
            if (!request.CdsSoftware.HasValue)
                throw new BusinessRuleException("CDS Software is required for ICP-OES equipment.");
        }
        else
        {
            if (request.CdsSoftware.HasValue)
                throw new BusinessRuleException("CDS Software is only allowed for HPLC and ICP-OES equipment.");
        }

        var sectionId = await _scope.ResolveSectionForCreateAsync(currentUserId, request.SectionId, cancellationToken);
        await ValidateEquipmentInventoryLinkAsync(currentUserId, request.EquipmentInventoryId, excludeEquipmentId: null, cancellationToken);

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
            SectionId = sectionId,
            EquipmentInventoryId = request.EquipmentInventoryId
        };
        _db.Equipment.Add(entity);
        if (_db.CurrentUserId is null && currentUserId > 0)
            _db.CurrentUserId = currentUserId;
        await _db.SaveChangesAsync(cancellationToken);
        return EquipmentResponse.From(entity);
    }

    public Task<IReadOnlyList<EquipmentResponse>> CreateEquipmentBatchAsync(
        int currentUserId,
        IReadOnlyList<CreateEquipmentRequest>? requests,
        CancellationToken ct = default) =>
        _batch.ExecuteAsync(
            requests,
            (row, index, token) => ValidateCreateEquipmentRowAsync(currentUserId, row, index, token),
            row => CreateEquipmentAsync(currentUserId, row, ct),
            getCode: row => row.Code,
            codeField: "code",
            getExistingCodesAsync: async (codes, token) =>
            {
                var trimmed = codes.Select(c => c.Trim()).ToList();
                var existing = await _db.Equipment
                    .AsNoTracking()
                    .Where(e => trimmed.Contains(e.Code))
                    .Select(e => e.Code)
                    .ToListAsync(token);
                return new HashSet<string>(existing.Select(c => c.Trim()), StringComparer.Ordinal);
            },
            ct: ct);

    private async Task<IReadOnlyList<FieldError>> ValidateCreateEquipmentRowAsync(
        int currentUserId,
        CreateEquipmentRequest request,
        int row,
        CancellationToken ct)
    {
        var errors = new List<FieldError>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add(new FieldError("name", "Name is required.", row));
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            errors.Add(new FieldError("code", "Code is required.", row));
        }

        try
        {
            await _scope.ResolveSectionForCreateAsync(currentUserId, request.SectionId, ct);
        }
        catch (Exception ex) when (ex is BusinessRuleException or UnauthorizedAccessException)
        {
            errors.Add(new FieldError("sectionId", ex.Message, row));
        }

        if (request.EquipmentInventoryId is int assetId)
        {
            try
            {
                await _scope.EnsureEquipmentInventoryAccessAsync(currentUserId, assetId, ct, SectionAccess.Read);
            }
            catch (Exception ex) when (ex is BusinessRuleException or UnauthorizedAccessException)
            {
                errors.Add(new FieldError("equipmentInventoryId", ex.Message, row));
            }

            if (assetId <= 0 || !await _db.EquipmentInventories.AsNoTracking().AnyAsync(a => a.Id == assetId, ct))
            {
                errors.Add(new FieldError("equipmentInventoryId", $"Equipment inventory asset {assetId} not found.", row));
            }
        }

        return errors;
    }

    public async Task<EquipmentResponse> UpdateEquipmentAsync(
        int currentUserId,
        int id,
        UpdateEquipmentRequest request,
        string? changeReason = null,
        CancellationToken cancellationToken = default)
    {
        await _scope.EnsureEquipmentAccessAsync(currentUserId, id, cancellationToken);

        var entity = await _db.Equipment.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Equipment {id} not found.");

        RecordVersion.EnsureCurrent(_db, entity);
        await _reasonGuard.EnsureAsync(MasterRecordType.Equipment, id, ChangeKind.Edit, changeReason, currentUserId, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BusinessRuleException("Name is required.");
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new BusinessRuleException("Code is required.");
        if (await _db.Equipment.AnyAsync(e => e.Code == request.Code.Trim() && e.Id != id, cancellationToken))
            throw new BusinessRuleException($"Equipment code \"{request.Code}\" already exists.");

        if (!string.IsNullOrWhiteSpace(request.Vendor) && request.Vendor.Length > 100)
            throw new BusinessRuleException("Vendor cannot exceed 100 characters.");

        if (request.Type == EquipmentType.Hplc)
        {
            if (!request.CdsSoftware.HasValue)
                throw new BusinessRuleException("CDS Software is required for HPLC equipment.");
        }
        else if (request.Type == EquipmentType.IcpOes)
        {
            if (!request.CdsSoftware.HasValue)
                throw new BusinessRuleException("CDS Software is required for ICP-OES equipment.");
        }
        else
        {
            if (request.CdsSoftware.HasValue)
                throw new BusinessRuleException("CDS Software is only allowed for HPLC and ICP-OES equipment.");
        }

        if (request.SectionId.HasValue && request.SectionId.Value != entity.SectionId)
        {
            entity.SectionId = await _scope.ResolveSectionForCreateAsync(currentUserId, request.SectionId, cancellationToken);
        }

        await ValidateEquipmentInventoryLinkAsync(currentUserId, request.EquipmentInventoryId, excludeEquipmentId: id, cancellationToken);

        entity.Name = request.Name.Trim();
        entity.Code = request.Code.Trim();
        entity.Type = request.Type;
        entity.Location = request.Location;
        entity.SetPointTemperature = request.SetPointTemperature;
        entity.CalibrationDueDate = request.CalibrationDueDate;
        entity.Vendor = request.Vendor?.Trim();
        entity.CdsSoftware = request.CdsSoftware;
        entity.ConnectionSettings = request.ConnectionSettings;
        entity.EquipmentInventoryId = request.EquipmentInventoryId;

        if (_db.CurrentUserId is null && currentUserId > 0)
            _db.CurrentUserId = currentUserId;
        await _db.SaveChangesAsync(cancellationToken);
        return EquipmentResponse.From(entity);
    }
}
