using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.Responses;

namespace MicroLIMS.Application.Services;

public record SaveMaterialMasterEntryRequest(
    string Code, string Name, MaterialMasterCategory Category, string? Grade, string? Source,
    MaterialUnit BaseUnit, int? SectionId = null,
    string? WorkingConcentration = null, string? Solvent = null,
    decimal? TransitionRangeFrom = null, decimal? TransitionRangeTo = null,
    string? ColourChange = null, string? IndicatorUse = null);

// Reagent / Indicator / Reference Standard master (HPLC chain S1, spec 3.1).
// Chemical stock lots (Material) reference an entry here; lots snapshot the
// entry's name/code at receipt so a later edit here doesn't retroactively
// change a lot's record.
public class MaterialMasterService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly TimeProvider _time;

    public MaterialMasterService(IMicroLimsDbContext db, IUserSectionScopeService scope, TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _db = db;
        _scope = scope;
    }

    public async Task<List<MaterialMasterEntryResponse>> GetAllAsync(int currentUserId, MaterialMasterCategory? category = null, bool activeOnly = false, CancellationToken ct = default)
    {
        var scope = await _scope.GetAccessibleSectionIdsAsync(currentUserId, ct);
        var query = _db.MaterialMasterEntries.AsNoTracking().Include(e => e.Section).AsQueryable();

        if (scope != null)
        {
            query = query.Where(e => scope.Contains(e.SectionId));
        }

        if (category.HasValue)
        {
            query = query.Where(e => e.Category == category.Value);
        }

        if (activeOnly)
        {
            query = query.Where(e => e.IsActive);
        }

        return (await query.OrderBy(e => e.Code).ToListAsync(ct)).Select(MaterialMasterEntryResponse.From).ToList();
    }

    public async Task<MaterialMasterEntryResponse> CreateAsync(SaveMaterialMasterEntryRequest r, int currentUserId, CancellationToken ct = default)
    {
        Validate(r);

        var sectionId = await _scope.ResolveSectionForCreateAsync(currentUserId, r.SectionId, ct);
        var normalizedCode = r.Code.Trim().ToUpper();

        if (await _db.MaterialMasterEntries.AnyAsync(e => e.SectionId == sectionId && e.Code == normalizedCode, ct))
            throw new InvalidOperationException($"Material master entry code \"{normalizedCode}\" already exists.");

        var entry = new MaterialMasterEntry
        {
            SectionId = sectionId,
            Code = normalizedCode,
            Name = r.Name.Trim(),
            Category = r.Category,
            Grade = r.Grade?.Trim(),
            Source = r.Source?.Trim(),
            BaseUnit = r.BaseUnit,
            IsActive = true,
            WorkingConcentration = r.WorkingConcentration?.Trim(),
            Solvent = r.Solvent?.Trim(),
            TransitionRangeFrom = r.TransitionRangeFrom,
            TransitionRangeTo = r.TransitionRangeTo,
            ColourChange = r.ColourChange?.Trim(),
            IndicatorUse = r.IndicatorUse?.Trim(),
            CreatedByUserId = currentUserId,
            CreatedAt = _time.GetUtcNow().UtcDateTime,
            LastModifiedByUserId = currentUserId,
            LastModifiedAt = _time.GetUtcNow().UtcDateTime
        };

        _db.MaterialMasterEntries.Add(entry);
        await _db.SaveChangesAsync(ct);

        return MaterialMasterEntryResponse.From(entry);
    }

    public async Task<MaterialMasterEntryResponse> UpdateAsync(int id, SaveMaterialMasterEntryRequest r, int currentUserId, CancellationToken ct = default)
    {
        Validate(r);

        var entry = await LoadWithAccessAsync(id, currentUserId, ct);
        RecordVersion.EnsureCurrent(_db, entry);

        var normalizedCode = r.Code.Trim().ToUpper();
        if (await _db.MaterialMasterEntries.AnyAsync(e => e.SectionId == entry.SectionId && e.Code == normalizedCode && e.Id != id, ct))
            throw new InvalidOperationException($"Material master entry code \"{normalizedCode}\" already exists.");

        if (r.Category != entry.Category && await _db.Materials.AnyAsync(m => m.MaterialMasterEntryId == id, ct))
            throw new InvalidOperationException("This entry has linked stock lots; its category can't change.");

        entry.Code = normalizedCode;
        entry.Name = r.Name.Trim();
        entry.Category = r.Category;
        entry.Grade = r.Grade?.Trim();
        entry.Source = r.Source?.Trim();
        entry.BaseUnit = r.BaseUnit;
        entry.WorkingConcentration = r.WorkingConcentration?.Trim();
        entry.Solvent = r.Solvent?.Trim();
        entry.TransitionRangeFrom = r.TransitionRangeFrom;
        entry.TransitionRangeTo = r.TransitionRangeTo;
        entry.ColourChange = r.ColourChange?.Trim();
        entry.IndicatorUse = r.IndicatorUse?.Trim();
        entry.LastModifiedByUserId = currentUserId;
        entry.LastModifiedAt = _time.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(ct);
        return MaterialMasterEntryResponse.From(entry);
    }

    public async Task<MaterialMasterEntryResponse> SetActiveAsync(int id, bool isActive, int currentUserId, CancellationToken ct = default)
    {
        var entry = await LoadWithAccessAsync(id, currentUserId, ct);

        entry.IsActive = isActive;
        entry.LastModifiedByUserId = currentUserId;
        entry.LastModifiedAt = _time.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(ct);
        return MaterialMasterEntryResponse.From(entry);
    }

    private async Task<MaterialMasterEntry> LoadWithAccessAsync(int id, int currentUserId, CancellationToken ct)
    {
        var entry = await _db.MaterialMasterEntries.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException($"Material master entry {id} not found.");

        var scope = await _scope.GetAccessibleSectionIdsAsync(currentUserId, ct);
        if (scope != null && !scope.Contains(entry.SectionId))
            throw new NotFoundException($"Material master entry {id} not found.");

        return entry;
    }

    private static void Validate(SaveMaterialMasterEntryRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Code)) throw new InvalidOperationException("Code is required.");
        if (string.IsNullOrWhiteSpace(r.Name)) throw new InvalidOperationException("Name is required.");
        var hasIndicatorFields = r.WorkingConcentration != null || r.Solvent != null || r.TransitionRangeFrom != null
            || r.TransitionRangeTo != null || r.ColourChange != null || r.IndicatorUse != null;
        if (hasIndicatorFields && r.Category != MaterialMasterCategory.Indicator)
            throw new InvalidOperationException("Indicator fields are only allowed for indicators.");
        if (r.TransitionRangeFrom > r.TransitionRangeTo)
            throw new InvalidOperationException("Transition range 'from' must not be above 'to'.");
    }
}
