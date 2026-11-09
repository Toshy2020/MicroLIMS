using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.Responses;

namespace MicroLIMS.Application.Services;

// Kind Material uses MaterialMasterEntryId; PreparedSolution uses SourceSolutionMasterId;
// Text uses Text only (no quantity or unit).
public record SolutionComponentInput(
    int? MaterialMasterEntryId, decimal? Quantity, SolutionComponentUnit? Unit,
    SolutionComponentKind Kind = SolutionComponentKind.Material,
    int? SourceSolutionMasterId = null, string? Text = null);

public record SaveSolutionMasterRequest(
    string Name, SolutionType Type, int ShelfLifeValue, ShelfLifeUnit ShelfLifeUnit, string StorageCondition,
    decimal FinalVolumeMl, string Instructions, List<SolutionComponentInput> Components,
    decimal? PhTarget = null, decimal? PhTolerance = null, int? PhAdjustingEntryId = null, int? SectionId = null,
    decimal? NominalStrength = null, TitrantStrengthUnit? StrengthUnit = null, StandardizationMode? StandardizationMode = null,
    int? StandardEntryId = null, decimal? EquivalenceMgPerMl = null, int? ReferenceSolutionId = null,
    bool BlankRequired = false, int? ReplicateCount = null, decimal? FactorMin = null, decimal? FactorMax = null,
    decimal? MaxRsdPercent = null, int? ValidityDays = null,
    string? Reason = null,   // required on update, ignored on create
    bool SterilizationRequired = false, bool ReleaseRequired = false);

// Solution master for Mobile Phases, Diluents, Reagents, Neutralizers and Titrants (HPLC chain S2,
// spec 3.2). Recipe components reference active Material master entries of
// the same section; a component whose entry has since been deactivated is
// still allowed to be kept unchanged (deactivation only blocks new picks).
// Edited in place with a reason recorded through IAuditEventService; no
// versions (D7). Titrant standardization fields only apply when Type ==
// Titrant. The sterilization / release flags and prepared-solution
// components only apply to the general types (Diluent, Reagent, Neutralizer).
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
        var scope = await _scope.GetReadableSectionIdsAsync(currentUserId, ct);
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

        var scope = await _scope.GetReadableSectionIdsAsync(currentUserId, ct);
        if (scope != null && !scope.Contains(solution.SectionId))
            throw new NotFoundException($"Solution master {id} not found.");

        return SolutionMasterResponse.From(solution);
    }

    public async Task<SolutionMasterResponse> CreateAsync(SaveSolutionMasterRequest r, int currentUserId, CancellationToken ct = default)
    {
        var sectionId = await _scope.ResolveSectionForCreateAsync(currentUserId, r.SectionId, ct, SectionAccess.Configure);

        await ValidateCommonAsync(r, sectionId, null, new HashSet<int>(), new HashSet<int>(), ct);
        await ValidateTitrantAsync(r, sectionId, null, ct);

        var trimmedName = r.Name.Trim();
        if (await _db.SolutionMasters.AnyAsync(s => s.SectionId == sectionId && s.Name == trimmedName, ct))
            throw new BusinessRuleException($"A solution named \"{trimmedName}\" already exists.");

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

        AddComponents(solution, r);

        _db.SolutionMasters.Add(solution);
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(solution.Id, currentUserId, ct);
    }

    public async Task<SolutionMasterResponse> UpdateAsync(int id, SaveSolutionMasterRequest r, int currentUserId, CancellationToken ct = default)
    {
        var reason = ValidateReason(r.Reason);

        var solution = await _db.SolutionMasters.Include(s => s.Components).FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException($"Solution master {id} not found.");

        var scope = await _scope.GetConfigurableSectionIdsAsync(currentUserId, ct);
        if (scope != null && !scope.Contains(solution.SectionId))
            throw new NotFoundException($"Solution master {id} not found.");

        RecordVersion.EnsureCurrent(_db, solution);

        var alreadyUsed = new HashSet<int>(solution.Components.Where(c => c.MaterialMasterEntryId.HasValue).Select(c => c.MaterialMasterEntryId!.Value));
        var alreadyUsedSources = new HashSet<int>(solution.Components.Where(c => c.SourceSolutionMasterId.HasValue).Select(c => c.SourceSolutionMasterId!.Value));
        if (solution.PhAdjustingEntryId.HasValue) alreadyUsed.Add(solution.PhAdjustingEntryId.Value);
        if (solution.StandardEntryId.HasValue) alreadyUsed.Add(solution.StandardEntryId.Value);

        await ValidateCommonAsync(r, solution.SectionId, id, alreadyUsed, alreadyUsedSources, ct);
        await ValidateTitrantAsync(r, solution.SectionId, id, ct);

        var trimmedName = r.Name.Trim();
        if (await _db.SolutionMasters.AnyAsync(s => s.SectionId == solution.SectionId && s.Name == trimmedName && s.Id != id, ct))
            throw new BusinessRuleException($"A solution named \"{trimmedName}\" already exists.");

        var flagChanges = new List<AuditFieldChange>();
        if (solution.SterilizationRequired != r.SterilizationRequired)
            flagChanges.Add(new AuditFieldChange("SterilizationRequired", solution.SterilizationRequired.ToString(), r.SterilizationRequired.ToString()));
        if (solution.ReleaseRequired != r.ReleaseRequired)
            flagChanges.Add(new AuditFieldChange("ReleaseRequired", solution.ReleaseRequired.ToString(), r.ReleaseRequired.ToString()));

        ApplyFields(solution, r);
        solution.LastModifiedByUserId = currentUserId;
        solution.LastModifiedAt = _time.GetUtcNow().UtcDateTime;

        _db.SolutionComponents.RemoveRange(solution.Components);
        solution.Components.Clear();
        AddComponents(solution, r);

        await _db.SaveChangesAsync(ct);

        await _auditEventService.RecordUserEventAsync(
            UpdatedActionCode,
            AuditActionCategory.Configuration,
            RecordType,
            reason: reason,
            changes: flagChanges.Count > 0 ? flagChanges : null,
            entityId: id.ToString(),
            cancellationToken: ct);

        return await GetByIdAsync(id, currentUserId, ct);
    }

    public async Task<SolutionMasterResponse> SetActiveAsync(int id, bool isActive, string reason, int currentUserId, CancellationToken ct = default)
    {
        var trimmedReason = ValidateReason(reason);

        var solution = await _db.SolutionMasters.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException($"Solution master {id} not found.");

        var scope = await _scope.GetConfigurableSectionIdsAsync(currentUserId, ct);
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
            .Include(s => s.Components).ThenInclude(c => c.MaterialMasterEntry)
            .Include(s => s.Components).ThenInclude(c => c.SourceSolutionMaster);

    private static string ValidateReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleException("A reason is required.");
        var trimmed = reason.Trim();
        if (trimmed.Length > 500)
            throw new BusinessRuleException("Reason cannot exceed 500 characters.");
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

        solution.SterilizationRequired = r.SterilizationRequired;
        solution.ReleaseRequired = r.ReleaseRequired;
    }

    private static void AddComponents(SolutionMaster solution, SaveSolutionMasterRequest r)
    {
        var order = 1;
        foreach (var c in r.Components)
        {
            var isText = c.Kind == SolutionComponentKind.Text;
            solution.Components.Add(new SolutionComponent
            {
                Order = order++,
                Kind = c.Kind,
                MaterialMasterEntryId = c.Kind == SolutionComponentKind.Material ? c.MaterialMasterEntryId : null,
                SourceSolutionMasterId = c.Kind == SolutionComponentKind.PreparedSolution ? c.SourceSolutionMasterId : null,
                Text = isText ? c.Text!.Trim() : null,
                Quantity = isText ? null : c.Quantity,
                Unit = isText ? null : c.Unit
            });
        }
    }

    private async Task ValidateCommonAsync(
        SaveSolutionMasterRequest r, int sectionId, int? selfId,
        IReadOnlySet<int> alreadyUsedEntryIds, IReadOnlySet<int> alreadyUsedSourceIds, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) throw new BusinessRuleException("Name is required.");
        if (r.ShelfLifeValue <= 0) throw new BusinessRuleException("Shelf life must be greater than zero.");
        if (r.FinalVolumeMl <= 0) throw new BusinessRuleException("Final volume must be greater than zero.");
        if (string.IsNullOrWhiteSpace(r.StorageCondition)) throw new BusinessRuleException("Storage condition is required.");
        if (string.IsNullOrWhiteSpace(r.Instructions)) throw new BusinessRuleException("Instructions are required.");

        if (!r.Type.IsGeneral() && (r.SterilizationRequired || r.ReleaseRequired))
            throw new BusinessRuleException("Sterilization and release settings are only allowed for diluents, reagents and neutralizers.");

        if (r.Components.Count < 1 || r.Components.All(c => c.Kind == SolutionComponentKind.Text))
            throw new BusinessRuleException("At least one component is required.");

        foreach (var c in r.Components)
        {
            switch (c.Kind)
            {
                case SolutionComponentKind.Material:
                    if (!c.MaterialMasterEntryId.HasValue)
                        throw new BusinessRuleException("A material component needs a material master entry.");
                    if (c.SourceSolutionMasterId.HasValue || c.Text != null)
                        throw new BusinessRuleException("A material component cannot carry a source solution or text.");
                    RequireQuantity(c);
                    break;
                case SolutionComponentKind.PreparedSolution:
                    if (!r.Type.IsGeneral())
                        throw new BusinessRuleException("Prepared solution components are only allowed for diluents, reagents and neutralizers.");
                    if (!c.SourceSolutionMasterId.HasValue)
                        throw new BusinessRuleException("A prepared solution component needs a source solution.");
                    if (c.MaterialMasterEntryId.HasValue || c.Text != null)
                        throw new BusinessRuleException("A prepared solution component cannot carry a material entry or text.");
                    RequireQuantity(c);
                    break;
                case SolutionComponentKind.Text:
                    if (string.IsNullOrWhiteSpace(c.Text))
                        throw new BusinessRuleException("Text is required for a text line.");
                    if (c.Text.Trim().Length > 200)
                        throw new BusinessRuleException("A text line cannot exceed 200 characters.");
                    if (c.MaterialMasterEntryId.HasValue || c.SourceSolutionMasterId.HasValue || c.Quantity.HasValue || c.Unit.HasValue)
                        throw new BusinessRuleException("A text line cannot carry a material entry, source solution, quantity or unit.");
                    break;
                default:
                    throw new BusinessRuleException($"Unknown component kind '{c.Kind}'.");
            }
        }

        var componentEntryIds = r.Components.Where(c => c.Kind == SolutionComponentKind.Material).Select(c => c.MaterialMasterEntryId!.Value).ToList();
        if (componentEntryIds.Distinct().Count() != componentEntryIds.Count)
            throw new BusinessRuleException("The same component entry cannot be listed twice.");

        var entries = await _db.MaterialMasterEntries
            .Where(e => componentEntryIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, ct);

        foreach (var entryId in componentEntryIds)
        {
            if (!entries.TryGetValue(entryId, out var entry))
                throw new BusinessRuleException($"Material master entry {entryId} not found.");
            if (!entry.IsActive && !alreadyUsedEntryIds.Contains(entryId))
                throw new BusinessRuleException($"Component entry \"{entry.Code}\" is inactive.");
        }

        await ValidateSourcesAsync(r, sectionId, selfId, alreadyUsedSourceIds, ct);

        var hasPhFields = r.PhTolerance.HasValue || r.PhAdjustingEntryId.HasValue;
        if (hasPhFields && !r.PhTarget.HasValue)
            throw new BusinessRuleException("A pH target is required when pH tolerance or an adjusting reagent is set.");

        if (r.PhTarget.HasValue && (r.PhTarget.Value < 0 || r.PhTarget.Value > 14))
            throw new BusinessRuleException("pH target must be between 0 and 14.");

        if (r.PhTolerance.HasValue && r.PhTolerance.Value <= 0)
            throw new BusinessRuleException("pH tolerance must be greater than zero.");
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
                throw new BusinessRuleException("Standardization settings are only allowed for titrants.");
            return;
        }

        if (!r.NominalStrength.HasValue || r.NominalStrength.Value <= 0 || !r.StrengthUnit.HasValue)
            throw new BusinessRuleException("Nominal strength and its unit are required for titrants.");

        if (!r.StandardizationMode.HasValue)
            throw new BusinessRuleException("A standardization mode is required for titrants.");

        if (!r.ReplicateCount.HasValue || r.ReplicateCount.Value < 1)
            throw new BusinessRuleException("Replicate count must be at least 1.");

        if (!r.FactorMin.HasValue || !r.FactorMax.HasValue || r.FactorMin.Value <= 0 || r.FactorMax.Value <= 0 || r.FactorMin.Value > r.FactorMax.Value)
            throw new BusinessRuleException("Factor min and max must both be greater than zero, with min not above max.");

        if (!r.MaxRsdPercent.HasValue || r.MaxRsdPercent.Value <= 0)
            throw new BusinessRuleException("Max RSD % must be greater than zero.");

        if (!r.ValidityDays.HasValue || r.ValidityDays.Value < 0)
            throw new BusinessRuleException("Validity days must be zero or more.");

        if (r.StandardizationMode == StandardizationMode.PrimaryStandard)
        {
            if (r.ReferenceSolutionId.HasValue)
                throw new BusinessRuleException("Reference solution is only used in 'against volumetric solution' mode.");

            if (!r.StandardEntryId.HasValue)
                throw new BusinessRuleException("A standard entry is required for primary standard mode.");

            if (!r.EquivalenceMgPerMl.HasValue || r.EquivalenceMgPerMl.Value <= 0)
                throw new BusinessRuleException("Equivalence (mg/mL) must be greater than zero.");

            var standard = await _db.MaterialMasterEntries.FirstOrDefaultAsync(e => e.Id == r.StandardEntryId.Value, ct)
                ?? throw new BusinessRuleException("Standard entry not found.");

            if (standard.Category != MaterialMasterCategory.PrimaryStandard)
                throw new BusinessRuleException("The primary standard must be a Primary Standard entry in Reagents & Standards.");
        }
        else
        {
            if (r.StandardEntryId.HasValue || r.EquivalenceMgPerMl.HasValue)
                throw new BusinessRuleException("Standard entry and equivalence are only used in primary standard mode.");

            if (!r.ReferenceSolutionId.HasValue)
                throw new BusinessRuleException("A reference titrant solution is required.");

            if (selfId.HasValue && r.ReferenceSolutionId.Value == selfId.Value)
                throw new BusinessRuleException("A titrant cannot reference itself.");

            var reference = await _db.SolutionMasters.FirstOrDefaultAsync(s => s.Id == r.ReferenceSolutionId.Value, ct)
                ?? throw new BusinessRuleException("Reference solution not found.");

            if (reference.SectionId != sectionId || reference.Type != SolutionType.Titrant || !reference.IsActive)
                throw new BusinessRuleException("The reference solution must be an active titrant in this laboratory.");
        }
    }

    private static void RequireQuantity(SolutionComponentInput c)
    {
        if (!c.Quantity.HasValue || c.Quantity.Value <= 0)
            throw new BusinessRuleException("Component quantity must be greater than zero.");
        if (!c.Unit.HasValue)
            throw new BusinessRuleException("Component unit is required.");
    }

    // Prepared-solution lines: the source must be an active recipe of this
    // laboratory, not the recipe itself, listed once, and must not lead back to
    // this recipe through its own prepared-solution lines (no cycles).
    private async Task ValidateSourcesAsync(
        SaveSolutionMasterRequest r, int sectionId, int? selfId, IReadOnlySet<int> alreadyUsedSourceIds, CancellationToken ct)
    {
        var sourceIds = r.Components.Where(c => c.Kind == SolutionComponentKind.PreparedSolution).Select(c => c.SourceSolutionMasterId!.Value).ToList();
        if (sourceIds.Count == 0) return;

        if (sourceIds.Distinct().Count() != sourceIds.Count)
            throw new BusinessRuleException("The same source solution cannot be listed twice.");

        if (selfId.HasValue && sourceIds.Contains(selfId.Value))
            throw new BusinessRuleException("A solution cannot use itself as a component.");

        var sources = await _db.SolutionMasters.Where(s => sourceIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);
        foreach (var sourceId in sourceIds)
        {
            if (!sources.TryGetValue(sourceId, out var source))
                throw new BusinessRuleException($"Source solution {sourceId} not found.");
            if (source.SectionId != sectionId)
                throw new BusinessRuleException($"Source solution \"{source.Name}\" belongs to another laboratory.");
            if (!source.IsActive && !alreadyUsedSourceIds.Contains(sourceId))
                throw new BusinessRuleException($"Source solution \"{source.Name}\" is inactive.");
        }

        if (!selfId.HasValue) return;   // a recipe that does not exist yet cannot be reached from another

        var edges = await _db.SolutionComponents
            .Where(c => c.Kind == SolutionComponentKind.PreparedSolution && c.SolutionMaster!.SectionId == sectionId && c.SolutionMasterId != selfId.Value)
            .Select(c => new { c.SolutionMasterId, Source = c.SourceSolutionMasterId!.Value })
            .ToListAsync(ct);
        var graph = edges.GroupBy(e => e.SolutionMasterId).ToDictionary(g => g.Key, g => g.Select(e => e.Source).ToList());

        var seen = new HashSet<int>();
        var stack = new Stack<int>(sourceIds);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (!seen.Add(node) || !graph.TryGetValue(node, out var next)) continue;
            if (next.Contains(selfId.Value))
                throw new BusinessRuleException("This would create a circular reference between solutions.");
            foreach (var n in next) stack.Push(n);
        }
    }
}
