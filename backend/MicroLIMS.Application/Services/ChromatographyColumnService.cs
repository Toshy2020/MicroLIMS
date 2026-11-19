using System.Globalization;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.Responses;

namespace MicroLIMS.Application.Services;

public record CreateChromatographyColumnRequest(
    string Code,
    string Name,
    string? SerialNumber = null,
    int? SectionId = null,
    List<int>? CompatibleEquipmentIds = null,
    string? UspDesignation = null);

public record UpdateChromatographyColumnRequest(
    string Code,
    string Name,
    string? SerialNumber = null,
    bool? IsActive = null,
    int? SectionId = null,
    List<int>? CompatibleEquipmentIds = null,
    string? UspDesignation = null);

public class ChromatographyColumnService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly TimeProvider _time;

    public ChromatographyColumnService(IMicroLimsDbContext db, IUserSectionScopeService scope, TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _db = db;
        _scope = scope;
    }

    private static string? NormalizeDesignation(string? value)
    {
        var d = value?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(d)) return null;
        if (d.Length > 10)
            throw new BusinessRuleException(
                MessageCodes.ColumnUspDesignationTooLong,
                "USP designation cannot exceed 10 characters.");
        return d;
    }

    public async Task<List<ChromatographyColumnResponse>> GetAllAsync(int currentUserId, bool? activeOnly = null, CancellationToken ct = default)
    {
        var scope = await _scope.GetReadableSectionIdsAsync(currentUserId, ct);
        var query = _db.ChromatographyColumns
            .AsNoTracking()
            .Include(c => c.Section)
            .Include(c => c.CompatibleEquipment)
            .AsQueryable();

        if (scope != null)
        {
            query = query.Where(c => scope.Contains(c.SectionId));
        }

        if (activeOnly.HasValue && activeOnly.Value)
        {
            query = query.Where(c => c.IsActive);
        }

        return (await query.OrderBy(c => c.Code).ToListAsync(ct)).Select(ChromatographyColumnResponse.From).ToList();
    }

    public async Task<ChromatographyColumnResponse> GetByIdAsync(int id, int currentUserId, CancellationToken ct = default)
    {
        await _scope.EnsureColumnAccessAsync(currentUserId, id, ct, access: SectionAccess.Read);

        var column = await _db.ChromatographyColumns
            .AsNoTracking()
            .Include(c => c.Section)
            .Include(c => c.CompatibleEquipment)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (column == null)
            throw new NotFoundException(
                MessageCodes.ColumnNotFound,
                $"Chromatography column {id} not found.",
                new Dictionary<string, string> { ["columnId"] = id.ToString(CultureInfo.InvariantCulture) });

        return ChromatographyColumnResponse.From(column);
    }

    public async Task<ChromatographyColumnResponse> CreateAsync(CreateChromatographyColumnRequest request, int currentUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new BusinessRuleException(
                MessageCodes.ColumnCodeRequired,
                "Column code is required.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BusinessRuleException(
                MessageCodes.ColumnNameRequired,
                "Column name is required.");

        var normalizedCode = request.Code.Trim().ToUpper();
        if (await _db.ChromatographyColumns.AnyAsync(c => c.Code == normalizedCode, ct))
            throw new BusinessRuleException(
                MessageCodes.ColumnCodeExists,
                $"Column code \"{normalizedCode}\" already exists.",
                new Dictionary<string, string> { ["code"] = normalizedCode });

        if (request.SerialNumber != null && request.SerialNumber.Length > 100)
            throw new BusinessRuleException(
                MessageCodes.ColumnSerialNumberTooLong,
                "Serial number cannot exceed 100 characters.");

        var sectionId = await _scope.ResolveSectionForCreateAsync(currentUserId, request.SectionId, ct);

        var compatibleEquipment = new List<Equipment>();
        if (request.CompatibleEquipmentIds != null && request.CompatibleEquipmentIds.Count > 0)
        {
            var distinctIds = request.CompatibleEquipmentIds.Distinct().ToList();
            compatibleEquipment = await _db.Equipment
                .Where(e => distinctIds.Contains(e.Id))
                .ToListAsync(ct);

            if (compatibleEquipment.Count != distinctIds.Count)
                throw new BusinessRuleException(
                    MessageCodes.ColumnCompatibleEquipmentNotFound,
                    "One or more specified compatible equipment items do not exist.");

            foreach (var eq in compatibleEquipment)
            {
                if (eq.Type != EquipmentType.Hplc && eq.Type != EquipmentType.Gc)
                    throw new BusinessRuleException(
                        MessageCodes.ColumnCompatibleEquipmentNotChromatography,
                        $"Equipment \"{eq.Code}\" is not an HPLC or GC instrument. Only chromatography equipment can be linked to a column.",
                        new Dictionary<string, string> { ["equipment"] = eq.Code });
                if (eq.SectionId != sectionId)
                    throw new BusinessRuleException(
                        MessageCodes.ColumnCompatibleEquipmentWrongSection,
                        $"Equipment \"{eq.Code}\" belongs to another section. Compatible equipment must belong to the same laboratory section as the column.",
                        new Dictionary<string, string> { ["equipment"] = eq.Code });
            }
        }

        var column = new ChromatographyColumn
        {
            Code = normalizedCode,
            Name = request.Name.Trim(),
            SerialNumber = request.SerialNumber?.Trim(),
            UspDesignation = NormalizeDesignation(request.UspDesignation),
            SectionId = sectionId,
            IsActive = true,
            CreatedByUserId = currentUserId,
            CreatedAt = _time.GetUtcNow().UtcDateTime,
            LastModifiedByUserId = currentUserId,
            LastModifiedAt = _time.GetUtcNow().UtcDateTime,
            CompatibleEquipment = compatibleEquipment
        };

        _db.ChromatographyColumns.Add(column);
        await _db.SaveChangesAsync(ct);

        return ChromatographyColumnResponse.From(column);
    }

    public async Task<ChromatographyColumnResponse> UpdateAsync(int id, UpdateChromatographyColumnRequest request, int currentUserId, CancellationToken ct = default)
    {
        await _scope.EnsureColumnAccessAsync(currentUserId, id, ct);

        var column = await _db.ChromatographyColumns
            .Include(c => c.CompatibleEquipment)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (column == null)
            throw new NotFoundException(
                MessageCodes.ColumnNotFound,
                $"Chromatography column {id} not found.",
                new Dictionary<string, string> { ["columnId"] = id.ToString(CultureInfo.InvariantCulture) });

        RecordVersion.EnsureCurrent(_db, column);

        if (string.IsNullOrWhiteSpace(request.Code))
            throw new BusinessRuleException(
                MessageCodes.ColumnCodeRequired,
                "Column code is required.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BusinessRuleException(
                MessageCodes.ColumnNameRequired,
                "Column name is required.");

        var normalizedCode = request.Code.Trim().ToUpper();
        if (await _db.ChromatographyColumns.AnyAsync(c => c.Code == normalizedCode && c.Id != id, ct))
            throw new BusinessRuleException(
                MessageCodes.ColumnCodeExists,
                $"Column code \"{normalizedCode}\" already exists.",
                new Dictionary<string, string> { ["code"] = normalizedCode });

        if (request.SerialNumber != null && request.SerialNumber.Length > 100)
            throw new BusinessRuleException(
                MessageCodes.ColumnSerialNumberTooLong,
                "Serial number cannot exceed 100 characters.");

        if (request.SectionId.HasValue && request.SectionId.Value != column.SectionId)
        {
            column.SectionId = await _scope.ResolveSectionForCreateAsync(currentUserId, request.SectionId, ct);
        }

        if (request.CompatibleEquipmentIds != null)
        {
            var distinctIds = request.CompatibleEquipmentIds.Distinct().ToList();
            var compatibleEquipment = await _db.Equipment
                .Where(e => distinctIds.Contains(e.Id))
                .ToListAsync(ct);

            if (compatibleEquipment.Count != distinctIds.Count)
                throw new BusinessRuleException(
                    MessageCodes.ColumnCompatibleEquipmentNotFound,
                    "One or more specified compatible equipment items do not exist.");

            foreach (var eq in compatibleEquipment)
            {
                if (eq.Type != EquipmentType.Hplc && eq.Type != EquipmentType.Gc)
                    throw new BusinessRuleException(
                        MessageCodes.ColumnCompatibleEquipmentNotChromatography,
                        $"Equipment \"{eq.Code}\" is not an HPLC or GC instrument. Only chromatography equipment can be linked to a column.",
                        new Dictionary<string, string> { ["equipment"] = eq.Code });
                if (eq.SectionId != column.SectionId)
                    throw new BusinessRuleException(
                        MessageCodes.ColumnCompatibleEquipmentWrongSection,
                        $"Equipment \"{eq.Code}\" belongs to another section. Compatible equipment must belong to the same laboratory section as the column.",
                        new Dictionary<string, string> { ["equipment"] = eq.Code });
            }

            column.CompatibleEquipment.Clear();
            column.CompatibleEquipment.AddRange(compatibleEquipment);
        }

        column.Code = normalizedCode;
        column.Name = request.Name.Trim();
        column.SerialNumber = request.SerialNumber?.Trim();
        column.UspDesignation = NormalizeDesignation(request.UspDesignation);
        if (request.IsActive.HasValue)
        {
            column.IsActive = request.IsActive.Value;
        }
        column.LastModifiedByUserId = currentUserId;
        column.LastModifiedAt = _time.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(ct);
        return ChromatographyColumnResponse.From(column);
    }

    public async Task<ChromatographyColumnResponse> DeactivateAsync(int id, int currentUserId, CancellationToken ct = default)
    {
        await _scope.EnsureColumnAccessAsync(currentUserId, id, ct);

        var column = await _db.ChromatographyColumns
            .Include(c => c.CompatibleEquipment)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (column == null)
            throw new NotFoundException(
                MessageCodes.ColumnNotFound,
                $"Chromatography column {id} not found.",
                new Dictionary<string, string> { ["columnId"] = id.ToString(CultureInfo.InvariantCulture) });

        column.IsActive = false;
        column.LastModifiedByUserId = currentUserId;
        column.LastModifiedAt = _time.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(ct);
        return ChromatographyColumnResponse.From(column);
    }
}
