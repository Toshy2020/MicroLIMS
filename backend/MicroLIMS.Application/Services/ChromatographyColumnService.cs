using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record CreateChromatographyColumnRequest(
    string Code,
    string Name,
    string? SerialNumber = null,
    int? SectionId = null,
    List<int>? CompatibleEquipmentIds = null);

public record UpdateChromatographyColumnRequest(
    string Code,
    string Name,
    string? SerialNumber = null,
    bool? IsActive = null,
    int? SectionId = null,
    List<int>? CompatibleEquipmentIds = null);

public class ChromatographyColumnService
{
    private readonly MicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;

    public ChromatographyColumnService(MicroLimsDbContext db, IUserSectionScopeService scope)
    {
        _db = db;
        _scope = scope;
    }

    public async Task<List<ChromatographyColumn>> GetAllAsync(int currentUserId, bool? activeOnly = null, CancellationToken ct = default)
    {
        var scope = await _scope.GetAccessibleSectionIdsAsync(currentUserId, ct);
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

        return await query.OrderBy(c => c.Code).ToListAsync(ct);
    }

    public async Task<ChromatographyColumn> GetByIdAsync(int id, int currentUserId, CancellationToken ct = default)
    {
        await _scope.EnsureColumnAccessAsync(currentUserId, id, ct);

        var column = await _db.ChromatographyColumns
            .AsNoTracking()
            .Include(c => c.Section)
            .Include(c => c.CompatibleEquipment)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (column == null)
            throw new InvalidOperationException($"Chromatography column {id} not found.");

        return column;
    }

    public async Task<ChromatographyColumn> CreateAsync(CreateChromatographyColumnRequest request, int currentUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new InvalidOperationException("Column code is required.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Column name is required.");

        var normalizedCode = request.Code.Trim().ToUpper();
        if (await _db.ChromatographyColumns.AnyAsync(c => c.Code == normalizedCode, ct))
            throw new InvalidOperationException($"Column code \"{normalizedCode}\" already exists.");

        if (request.SerialNumber != null && request.SerialNumber.Length > 100)
            throw new InvalidOperationException("Serial number cannot exceed 100 characters.");

        var sectionId = await _scope.ResolveSectionForCreateAsync(currentUserId, request.SectionId, ct);

        var compatibleEquipment = new List<Equipment>();
        if (request.CompatibleEquipmentIds != null && request.CompatibleEquipmentIds.Count > 0)
        {
            var distinctIds = request.CompatibleEquipmentIds.Distinct().ToList();
            compatibleEquipment = await _db.Equipment
                .Where(e => distinctIds.Contains(e.Id))
                .ToListAsync(ct);

            if (compatibleEquipment.Count != distinctIds.Count)
                throw new InvalidOperationException("One or more specified compatible equipment items do not exist.");

            foreach (var eq in compatibleEquipment)
            {
                if (eq.Type != EquipmentType.Hplc)
                    throw new InvalidOperationException($"Equipment \"{eq.Code}\" is not an HPLC instrument. Only HPLC equipment can be linked to a chromatography column.");
                if (eq.SectionId != sectionId)
                    throw new InvalidOperationException($"Equipment \"{eq.Code}\" belongs to another section. Compatible equipment must belong to the same laboratory section as the column.");
            }
        }

        var column = new ChromatographyColumn
        {
            Code = normalizedCode,
            Name = request.Name.Trim(),
            SerialNumber = request.SerialNumber?.Trim(),
            SectionId = sectionId,
            IsActive = true,
            CreatedByUserId = currentUserId,
            CreatedAt = DateTime.UtcNow,
            LastModifiedByUserId = currentUserId,
            LastModifiedAt = DateTime.UtcNow,
            CompatibleEquipment = compatibleEquipment
        };

        _db.ChromatographyColumns.Add(column);
        await _db.SaveChangesAsync(ct);

        return column;
    }

    public async Task<ChromatographyColumn> UpdateAsync(int id, UpdateChromatographyColumnRequest request, int currentUserId, CancellationToken ct = default)
    {
        await _scope.EnsureColumnAccessAsync(currentUserId, id, ct);

        var column = await _db.ChromatographyColumns
            .Include(c => c.CompatibleEquipment)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (column == null)
            throw new InvalidOperationException($"Chromatography column {id} not found.");

        if (string.IsNullOrWhiteSpace(request.Code))
            throw new InvalidOperationException("Column code is required.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Column name is required.");

        var normalizedCode = request.Code.Trim().ToUpper();
        if (await _db.ChromatographyColumns.AnyAsync(c => c.Code == normalizedCode && c.Id != id, ct))
            throw new InvalidOperationException($"Column code \"{normalizedCode}\" already exists.");

        if (request.SerialNumber != null && request.SerialNumber.Length > 100)
            throw new InvalidOperationException("Serial number cannot exceed 100 characters.");

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
                throw new InvalidOperationException("One or more specified compatible equipment items do not exist.");

            foreach (var eq in compatibleEquipment)
            {
                if (eq.Type != EquipmentType.Hplc)
                    throw new InvalidOperationException($"Equipment \"{eq.Code}\" is not an HPLC instrument. Only HPLC equipment can be linked to a chromatography column.");
                if (eq.SectionId != column.SectionId)
                    throw new InvalidOperationException($"Equipment \"{eq.Code}\" belongs to another section. Compatible equipment must belong to the same laboratory section as the column.");
            }

            column.CompatibleEquipment.Clear();
            column.CompatibleEquipment.AddRange(compatibleEquipment);
        }

        column.Code = normalizedCode;
        column.Name = request.Name.Trim();
        column.SerialNumber = request.SerialNumber?.Trim();
        if (request.IsActive.HasValue)
        {
            column.IsActive = request.IsActive.Value;
        }
        column.LastModifiedByUserId = currentUserId;
        column.LastModifiedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return column;
    }

    public async Task<ChromatographyColumn> DeactivateAsync(int id, int currentUserId, CancellationToken ct = default)
    {
        await _scope.EnsureColumnAccessAsync(currentUserId, id, ct);

        var column = await _db.ChromatographyColumns
            .Include(c => c.CompatibleEquipment)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (column == null)
            throw new InvalidOperationException($"Chromatography column {id} not found.");

        column.IsActive = false;
        column.LastModifiedByUserId = currentUserId;
        column.LastModifiedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return column;
    }
}
