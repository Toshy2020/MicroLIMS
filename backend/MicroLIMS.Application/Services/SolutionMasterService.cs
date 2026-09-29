using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.Responses;

namespace MicroLIMS.Application.Services;

public record SolutionComponentInput(int MaterialMasterEntryId, decimal Quantity, SolutionComponentUnit Unit);

public record SaveSolutionMasterRequest(
    string Name, SolutionType Type, int ShelfLifeValue, ShelfLifeUnit ShelfLifeUnit, string StorageCondition,
    decimal FinalVolumeMl, string Instructions, List<SolutionComponentInput> Components,
    decimal? PhTarget = null, decimal? PhTolerance = null, int? PhAdjustingEntryId = null, int? SectionId = null,
    decimal? NominalStrength = null, TitrantStrengthUnit? StrengthUnit = null, StandardizationMode? StandardizationMode = null,
    int? StandardEntryId = null, decimal? EquivalenceMgPerMl = null, int? ReferenceSolutionId = null,
    bool BlankRequired = false, int? ReplicateCount = null, decimal? FactorMin = null, decimal? FactorMax = null,
    decimal? MaxRsdPercent = null, int? ValidityDays = null,
    string? Reason = null);   // required on update, ignored on create

// Solution master for Mobile Phases, Diluents and Titrants (HPLC chain S2,
// spec 3.2). Recipe components reference active Material master entries of
// the same section; a component whose entry has since been deactivated is
// still allowed to be kept unchanged (deactivation only blocks new picks).
// Edited in place with a reason recorded through IAuditEventService; no
// versions (D7). Titrant standardization fields only apply when Type ==
// Titrant.
public class SolutionMasterService
{
    private const string UpdatedActionCode = "SolutionMaster.Updated";
    private const string DeactivatedActionCode = "SolutionMaster.Deactivated";
    private const string ActivatedActionCode = "SolutionMaster.Activated";
    private const string RecordType = "SolutionMaster";

    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly IAuditEventService _auditEventService;
    private readonly TimeProvider _time;

    public SolutionMasterService(
        IMicroLimsDbContext db,
        IUserSectionScopeService scope,
        IAuditEventService auditEventService,
        TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _db = db;
        _scope = scope;
        _auditEventService = auditEventService;
    }

    public async Task<List<SolutionMasterResponse>> GetAllAsync(int currentUserId, SolutionType? type = null, bool activeOnly = false, CancellationToken ct = default)
    {
        var scope = await _scope.GetAccessibleSectionIdsAsync(currentUserId, ct);
        var query = QueryWithIncludes().AsNoTracking().AsQueryable();

        if (scope != null)
        {
            query = query.Where(s => scope.Contains(s.SectionId));
        }

        if (type.HasValue)
        {
            query = query.Where(s => s.Type == type.Value);
        }

        if (activeOnly)
        {
            query = query.Where(s => s.IsActive);
        }

        return (await query.OrderBy(s => s.Name).ToListAsync(ct)).Select(SolutionMasterResponse.From).ToList();
    }

    public async Task<SolutionMasterResponse> GetByIdAsync(int id, int currentUserId, CancellationToken ct = default)
    {
        var solution = await QueryWithIncludes().AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException($"Solution master {id} not found.");

        var scope = await _scope.GetAccessibleSectionIdsAsync(currentUserId, ct);
        if (scope != null && !scope.Contains(solution.SectionId))
            throw new NotFoundException($"Solution master {id} not found.");

        return SolutionMasterResponse.From(solution);
    }

    public async Task<SolutionMasterResponse> CreateAsync(SaveSolutionMasterRequest r, int currentUserId, CancellationToken ct = default)
    {
        var sectionId = await _scope.ResolveSectionForCreateAsync(currentUserId, r.SectionId, ct);

        await ValidateCommonAsync(r, sectionId, new HashSet<int>(), ct);
        await ValidateTitrantAsync(r, sectionId, null, ct);

        var trimmedName = r.Name.Trim();
        if (await _db.SolutionMasters.AnyAsync(s => s.SectionId == sectionId && s.Name == trimmedName, ct))
            throw new InvalidOperationException($"A solution named \"{trimmedName}\" already exists.");

        var solution = new SolutionMaster
        {
            SectionId = sectionId,
            IsActive = true,
            CreatedByUserId = currentUserId,
            CreatedAt = _time.GetUtcNow().UtcDateTime,
            LastModifiedByUserId = currentUserId,
            LastModifiedAt = _time.GetUtcNow().UtcDateTime,
        };
        ApplyFields(solution, r);

        var order = 1;
        foreach (var c in r.Components)
        {
            solution.Components.Add(new SolutionComponent
            {
                Order = order++,
                MaterialMasterEntryId = c.MaterialMasterEntryId,
                Quantity = c.Quantity,
                Unit = c.Unit
            });
        }

        _db.SolutionMasters.Add(solution);
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(solution.Id, currentUserId, ct);
    }

    public async Task<SolutionMasterResponse> UpdateAsync(int id, SaveSolutionMasterRequest r, int currentUserId, CancellationToken ct = default)
    {
        var reason = ValidateReason(r.Reason);

        var solution = await _db.SolutionMasters.Include(s => s.Components).FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException($"Solution master {id} not found.");

        var scope = await _scope.GetAccessibleSectionIdsAsync(currentUserId, ct);
        if (scope != null && !scope.Contains(solution.SectionId))
            throw new NotFoundException($"Solution master {id} not found.");

        RecordVersion.EnsureCurrent(_db, solution);

        var alreadyUsed = new HashSet<int>(solution.Components.Select(c => c.MaterialMasterEntryId));
        if (solution.PhAdjustingEntryId.HasValue) alreadyUsed.Add(solution.PhAdjustingEntryId.Value);
        if (solution.StandardEntryId.HasValue) alreadyUsed.Add(solution.StandardEntryId.Value);

        await ValidateCommonAsync(r, solution.SectionId, alreadyUsed, ct);
        await ValidateTitrantAsync(r, solution.SectionId, id, ct);

        var trimmedName = r.Name.Trim();
        if (await _db.SolutionMasters.AnyAsync(s => s.SectionId == solution.SectionId && s.Name == trimmedName && s.Id != id, ct))
            throw new InvalidOperationException($"A solution named \"{trimmedName}\" already exists.");

        ApplyFields(solution, r);
        solution.LastModifiedByUserId = currentUserId;
        solution.LastModifiedAt = _time.GetUtcNow().UtcDateTime;

        _db.SolutionComponents.RemoveRange(solution.Components);
        solution.Components.Clear();
        var order = 1;
        foreach (var c in r.Components)
        {
            solution.Components.Add(new SolutionComponent
            {
                Order = order++,
                MaterialMasterEntryId = c.MaterialMasterEntryId,
                Quantity = c.Quantity,
                Unit = c.Unit
            });
        }

        await _db.SaveChangesAsync(ct);

        await _auditEventService.RecordUserEventAsync(
            UpdatedActionCode,
            AuditActionCategory.Configuration,
            RecordType,
            reason: reason,
            entityId: id.ToString(),
            cancellationToken: ct);

        return await GetByIdAsync(id, currentUserId, ct);
    }

    public async Task<SolutionMasterResponse> SetActiveAsync(int id, bool isActive, string reason, int currentUserId, CancellationToken ct = default)
    {
        var trimmedReason = ValidateReason(reason);

        var solution = await _db.SolutionMasters.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException($"Solution master {id} not found.");

        var scope = await _scope.GetAccessibleSectionIdsAsync(currentUserId, ct);
        if (scope != null && !scope.Contains(solution.SectionId))
            throw new NotFoundException($"Solution master {id} not found.");

        solution.IsActive = isActive;
        solution.LastModifiedByUserId = currentUserId;
        solution.LastModifiedAt = _time.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(ct);

        await _auditEventService.RecordUserEventAsync(
            isActive ? ActivatedActionCode : DeactivatedActionCode,
            AuditActionCategory.Configuration,
            RecordType,
            reason: trimmedReason,
            entityId: id.ToString(),
            cancellationToken: ct);

        return await GetByIdAsync(id, currentUserId, ct);
    }

    private IQueryable<SolutionMaster> QueryWithIncludes() =>
        _db.SolutionMasters
            .Include(s => s.Section)
            .Include(s => s.PhAdjustingEntry)
            .Include(s => s.StandardEntry)
            .Include(s => s.ReferenceSolution)
            .Include(s => s.Components).ThenInclude(c => c.MaterialMasterEntry);

    private static string ValidateReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason is required.");
        var trimmed = reason.Trim();
        if (trimmed.Length > 500)
            throw new InvalidOperationException("Reason cannot exceed 500 characters.");
        return trimmed;
    }

    private static void ApplyFields(SolutionMaster solution, SaveSolutionMasterRequest r)
    {
        solution.Name = r.Name.Trim();
        solution.Type = r.Type;
        solution.ShelfLifeValue = r.ShelfLifeValue;
        solution.ShelfLifeUnit = r.ShelfLifeUnit;
        solution.StorageCondition = r.StorageCondition.Trim();
        solution.FinalVolumeMl = r.FinalVolumeMl;
        solution.PhTarget = r.PhTarget;
        solution.PhTolerance = r.PhTolerance;
        solution.PhAdjustingEntryId = r.PhAdjustingEntryId;
        solution.Instructions = r.Instructions.Trim();

        solution.NominalStrength = r.NominalStrength;
        solution.StrengthUnit = r.StrengthUnit;
        solution.StandardizationMode = r.StandardizationMode;
        solution.StandardEntryId = r.StandardEntryId;
        solution.EquivalenceMgPerMl = r.EquivalenceMgPerMl;
        solution.ReferenceSolutionId = r.ReferenceSolutionId;
        solution.BlankRequired = r.BlankRequired;
        solution.ReplicateCount = r.ReplicateCount;
        solution.FactorMin = r.FactorMin;
        solution.FactorMax = r.FactorMax;
        solution.MaxRsdPercent = r.MaxRsdPercent;
        solution.ValidityDays = r.ValidityDays;
    }

    private async Task ValidateCommonAsync(SaveSolutionMasterRequest r, int sectionId, IReadOnlySet<int> alreadyUsedEntryIds, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) throw new InvalidOperationException("Name is required.");
        if (r.ShelfLifeValue <= 0) throw new InvalidOperationException("Shelf life must be greater than zero.");
        if (r.FinalVolumeMl <= 0) throw new InvalidOperationException("Final volume must be greater than zero.");
        if (string.IsNullOrWhiteSpace(r.StorageCondition)) throw new InvalidOperationException("Storage condition is required.");
        if (string.IsNullOrWhiteSpace(r.Instructions)) throw new InvalidOperationException("Instructions are required.");

        if (r.Components.Count < 1)
            throw new InvalidOperationException("At least one component is required.");

        if (r.Components.Any(c => c.Quantity <= 0))
            throw new InvalidOperationException("Component quantity must be greater than zero.");

        var componentEntryIds = r.Components.Select(c => c.MaterialMasterEntryId).ToList();
        if (componentEntryIds.Distinct().Count() != componentEntryIds.Count)
            throw new InvalidOperationException("The same component entry cannot be listed twice.");

        var entries = await _db.MaterialMasterEntries
            .Where(e => componentEntryIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, ct);

        foreach (var entryId in componentEntryIds)
        {
            if (!entries.TryGetValue(entryId, out var entry))
                throw new InvalidOperationException($"Material master entry {entryId} not found.");
            if (entry.SectionId != sectionId)
                throw new InvalidOperationException($"Component entry \"{entry.Code}\" belongs to another laboratory.");
            if (!entry.IsActive && !alreadyUsedEntryIds.Contains(entryId))
                throw new InvalidOperationException($"Component entry \"{entry.Code}\" is inactive.");
        }

        var hasPhFields = r.PhTolerance.HasValue || r.PhAdjustingEntryId.HasValue;
        if (hasPhFields && !r.PhTarget.HasValue)
            throw new InvalidOperationException("A pH target is required when pH tolerance or an adjusting reagent is set.");

        if (r.PhTarget.HasValue && (r.PhTarget.Value < 0 || r.PhTarget.Value > 14))
            throw new InvalidOperationException("pH target must be between 0 and 14.");

        if (r.PhTolerance.HasValue && r.PhTolerance.Value <= 0)
            throw new InvalidOperationException("pH tolerance must be greater than zero.");
    }

    private async Task ValidateTitrantAsync(SaveSolutionMasterRequest r, int sectionId, int? selfId, CancellationToken ct)
    {
        var hasTitrantFields = r.NominalStrength.HasValue || r.StrengthUnit.HasValue || r.StandardizationMode.HasValue
            || r.StandardEntryId.HasValue || r.EquivalenceMgPerMl.HasValue || r.ReferenceSolutionId.HasValue
            || r.BlankRequired || r.ReplicateCount.HasValue || r.FactorMin.HasValue || r.FactorMax.HasValue
            || r.MaxRsdPercent.HasValue || r.ValidityDays.HasValue;

        if (r.Type != SolutionType.Titrant)
        {
            if (hasTitrantFields)
                throw new InvalidOperationException("Standardization settings are only allowed for titrants.");
            return;
        }

        if (!r.NominalStrength.HasValue || r.NominalStrength.Value <= 0 || !r.StrengthUnit.HasValue)
            throw new InvalidOperationException("Nominal strength and its unit are required for titrants.");

        if (!r.StandardizationMode.HasValue)
            throw new InvalidOperationException("A standardization mode is required for titrants.");

        if (!r.ReplicateCount.HasValue || r.ReplicateCount.Value < 1)
            throw new InvalidOperationException("Replicate count must be at least 1.");

        if (!r.FactorMin.HasValue || !r.FactorMax.HasValue || r.FactorMin.Value <= 0 || r.FactorMax.Value <= 0 || r.FactorMin.Value > r.FactorMax.Value)
            throw new InvalidOperationException("Factor min and max must both be greater than zero, with min not above max.");

        if (!r.MaxRsdPercent.HasValue || r.MaxRsdPercent.Value <= 0)
            throw new InvalidOperationException("Max RSD % must be greater than zero.");

        if (!r.ValidityDays.HasValue || r.ValidityDays.Value < 0)
            throw new InvalidOperationException("Validity days must be zero or more.");

        if (r.StandardizationMode == StandardizationMode.PrimaryStandard)
        {
            if (r.ReferenceSolutionId.HasValue)
                throw new InvalidOperationException("Reference solution is only used in 'against volumetric solution' mode.");

            if (!r.StandardEntryId.HasValue)
                throw new InvalidOperationException("A standard entry is required for primary standard mode.");

            if (!r.EquivalenceMgPerMl.HasValue || r.EquivalenceMgPerMl.Value <= 0)
                throw new InvalidOperationException("Equivalence (mg/mL) must be greater than zero.");

            var standard = await _db.MaterialMasterEntries.FirstOrDefaultAsync(e => e.Id == r.StandardEntryId.Value, ct)
                ?? throw new InvalidOperationException("Standard entry not found.");

            if (standard.SectionId != sectionId)
                throw new InvalidOperationException("The standard entry belongs to another laboratory.");

            if (standard.Category != MaterialMasterCategory.ReferenceStandard && standard.Category != MaterialMasterCategory.Reagent)
                throw new InvalidOperationException("The standard entry must be a reference standard or reagent.");
        }
        else
        {
            if (r.StandardEntryId.HasValue || r.EquivalenceMgPerMl.HasValue)
                throw new InvalidOperationException("Standard entry and equivalence are only used in primary standard mode.");

            if (!r.ReferenceSolutionId.HasValue)
                throw new InvalidOperationException("A reference titrant solution is required.");

            if (selfId.HasValue && r.ReferenceSolutionId.Value == selfId.Value)
                throw new InvalidOperationException("A titrant cannot reference itself.");

            var reference = await _db.SolutionMasters.FirstOrDefaultAsync(s => s.Id == r.ReferenceSolutionId.Value, ct)
                ?? throw new InvalidOperationException("Reference solution not found.");

            if (reference.SectionId != sectionId || reference.Type != SolutionType.Titrant || !reference.IsActive)
                throw new InvalidOperationException("The reference solution must be an active titrant in this laboratory.");
        }
    }
}
