using System.Text.Json;
using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.Responses;

namespace MicroLIMS.Application.Services;

public record HplcMobilePhaseInput(string Channel, int SolutionMasterId, decimal? RatioPercent);
public record HplcGradientStepInput(decimal TimeMin, decimal PercentA, decimal PercentB, decimal PercentC, decimal PercentD);
public record HplcAnalyteInput(int? Id, string Name, decimal WavelengthNm, int StandardEntryId,
    decimal TheoreticalWeightStdMg, decimal TheoreticalWeightTestMg, int StandardInjections,
    decimal? SstMaxRsdPercent = null, decimal? SstMinResolution = null, decimal? SstMaxTailingFactor = null,
    decimal? SstMinTheoreticalPlates = null, decimal? SstMinRetentionFactor = null,
    decimal? SstMinSignalToNoise = null, decimal? SstMinPeakToValley = null);

public record SaveHplcMethodRequest(
    string Name, string Abbreviation, DateTime EffectiveDate,
    string ColumnDesignation, decimal ColumnLengthMm, decimal ColumnInternalDiameterMm, decimal ParticleSizeUm,
    decimal ColumnTemperatureC, ElutionMode ElutionMode, decimal FlowRateMlPerMin,
    HplcDetectorType DetectorType, decimal InjectionVolumeUl, decimal RunTimeMin, int DiluentSolutionId,
    List<HplcMobilePhaseInput> MobilePhases, List<HplcGradientStepInput> GradientSteps, List<HplcAnalyteInput> Analytes,
    string? ColumnBrand = null, string? ColumnPartNumber = null, decimal? EquilibrationMin = null,
    int? SectionId = null, string? Reason = null);   // Reason required on update, ignored on create

public record HplcMethodListItem(int Id, string Name, string Abbreviation, bool IsActive, int AnalyteCount, string SectionName, DateTime LastModifiedAt);
public record HplcMethodHistoryEntry(DateTime At, string UserName, string Action, string? Reason, string? BeforeJson, string? AfterJson);

// HPLC method master (HPLC chain S3, spec 3.3): parameters, gradient, mobile
// phases + diluent, analytes with Th.Wt and SST criteria. Modelled on
// SolutionMasterService (S2): edit in place with a required reason, no
// versions (D7). Each edit writes one audit event whose single change is the
// whole method as JSON before -> after, so the history screen reads one list.
public class HplcMethodService
{
    private const string CreatedActionCode = "HplcMethod.Created";
    private const string UpdatedActionCode = "HplcMethod.Updated";
    private const string DeactivatedActionCode = "HplcMethod.Deactivated";
    private const string ActivatedActionCode = "HplcMethod.Activated";
    private const string RecordType = "HplcMethod";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly IAuditEventService _auditEventService;
    private readonly TimeProvider _time;

    public HplcMethodService(
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

    public async Task<List<HplcMethodListItem>> GetAllAsync(int currentUserId, bool activeOnly = false, CancellationToken ct = default)
    {
        var scope = await _scope.GetAccessibleSectionIdsAsync(currentUserId, ct);
        var query = _db.HplcMethods.Include(m => m.Section).Include(m => m.Analytes).AsNoTracking().AsQueryable();

        if (scope != null)
            query = query.Where(m => scope.Contains(m.SectionId));

        if (activeOnly)
            query = query.Where(m => m.IsActive);

        return (await query.OrderBy(m => m.Name).ToListAsync(ct))
            .Select(m => new HplcMethodListItem(m.Id, m.Name, m.Abbreviation, m.IsActive, m.Analytes.Count, m.Section?.Name ?? string.Empty, m.LastModifiedAt))
            .ToList();
    }

    public async Task<HplcMethodResponse> GetByIdAsync(int id, int currentUserId, CancellationToken ct = default)
    {
        var method = await QueryWithIncludes().AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NotFoundException($"HPLC method {id} not found.");

        var scope = await _scope.GetAccessibleSectionIdsAsync(currentUserId, ct);
        if (scope != null && !scope.Contains(method.SectionId))
            throw new NotFoundException($"HPLC method {id} not found.");

        return HplcMethodResponse.From(method);
    }

    public async Task<HplcMethodResponse> CreateAsync(SaveHplcMethodRequest r, int currentUserId, CancellationToken ct = default)
    {
        var sectionId = await _scope.ResolveSectionForCreateAsync(currentUserId, r.SectionId, ct);

        var abbr = await ValidateCommonAsync(r, sectionId, null, null, ct);

        if (await _db.HplcMethods.AnyAsync(m => m.SectionId == sectionId && m.Abbreviation == abbr, ct))
            throw new InvalidOperationException($"An HPLC method abbreviated \"{abbr}\" already exists.");

        var method = new HplcMethod
        {
            SectionId = sectionId,
            IsActive = true,
            CreatedByUserId = currentUserId,
            CreatedAt = _time.GetUtcNow().UtcDateTime,
            LastModifiedByUserId = currentUserId,
            LastModifiedAt = _time.GetUtcNow().UtcDateTime,
        };
        ApplyFields(method, r, abbr);
        ReplaceMobilePhases(method, r);
        ReplaceGradientSteps(method, r);
        ApplyAnalytes(method, r);

        _db.HplcMethods.Add(method);
        await _db.SaveChangesAsync(ct);

        var response = await GetByIdAsync(method.Id, currentUserId, ct);
        var after = JsonSerializer.Serialize(response, JsonOptions);

        await _auditEventService.RecordUserEventAsync(
            CreatedActionCode,
            AuditActionCategory.Configuration,
            RecordType,
            entityId: method.Id.ToString(),
            changes: new[] { new AuditFieldChange("Method", null, after) },
            cancellationToken: ct);

        return response;
    }

    public async Task<HplcMethodResponse> UpdateAsync(int id, SaveHplcMethodRequest r, int currentUserId, CancellationToken ct = default)
    {
        var reason = ValidateReason(r.Reason);

        var before = JsonSerializer.Serialize(await GetByIdAsync(id, currentUserId, ct), JsonOptions);

        var method = await _db.HplcMethods
            .Include(m => m.MobilePhases)
            .Include(m => m.GradientSteps)
            .Include(m => m.Analytes)
            .FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NotFoundException($"HPLC method {id} not found.");

        RecordVersion.EnsureCurrent(_db, method);

        var alreadyUsedSolutionIds = new HashSet<int>(method.MobilePhases.Select(p => p.SolutionMasterId)) { method.DiluentSolutionId };
        var alreadyUsedStandardIds = new HashSet<int>(method.Analytes.Select(a => a.StandardEntryId));

        var abbr = await ValidateCommonAsync(r, method.SectionId, alreadyUsedSolutionIds, alreadyUsedStandardIds, ct);

        if (await _db.HplcMethods.AnyAsync(m => m.SectionId == method.SectionId && m.Abbreviation == abbr && m.Id != id, ct))
            throw new InvalidOperationException($"An HPLC method abbreviated \"{abbr}\" already exists.");

        // Never orphan a specification row - refuse before any child is touched.
        var incomingAnalyteIds = r.Analytes.Where(a => a.Id.HasValue).Select(a => a.Id!.Value).ToHashSet();
        foreach (var existingAnalyte in method.Analytes.Where(a => !incomingAnalyteIds.Contains(a.Id)))
        {
            if (await _db.Specifications.AnyAsync(s => s.HplcMethodAnalyteId == existingAnalyte.Id, ct))
                throw new InvalidOperationException($"Analyte \"{existingAnalyte.Name}\" is used by specifications; it can't be removed.");
        }

        ApplyFields(method, r, abbr);
        ReplaceMobilePhases(method, r);
        ReplaceGradientSteps(method, r);
        ApplyAnalytes(method, r);
        method.LastModifiedByUserId = currentUserId;
        method.LastModifiedAt = _time.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(ct);

        var response = await GetByIdAsync(id, currentUserId, ct);
        var after = JsonSerializer.Serialize(response, JsonOptions);

        await _auditEventService.RecordUserEventAsync(
            UpdatedActionCode,
            AuditActionCategory.Configuration,
            RecordType,
            reason: reason,
            entityId: id.ToString(),
            changes: new[] { new AuditFieldChange("Method", before, after) },
            cancellationToken: ct);

        return response;
    }

    public async Task<HplcMethodResponse> SetActiveAsync(int id, bool isActive, string reason, int currentUserId, CancellationToken ct = default)
    {
        var trimmedReason = ValidateReason(reason);

        var before = JsonSerializer.Serialize(await GetByIdAsync(id, currentUserId, ct), JsonOptions);

        var method = await _db.HplcMethods.FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NotFoundException($"HPLC method {id} not found.");

        var scope = await _scope.GetAccessibleSectionIdsAsync(currentUserId, ct);
        if (scope != null && !scope.Contains(method.SectionId))
            throw new NotFoundException($"HPLC method {id} not found.");

        method.IsActive = isActive;
        method.LastModifiedByUserId = currentUserId;
        method.LastModifiedAt = _time.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(ct);

        var response = await GetByIdAsync(id, currentUserId, ct);
        var after = JsonSerializer.Serialize(response, JsonOptions);

        await _auditEventService.RecordUserEventAsync(
            isActive ? ActivatedActionCode : DeactivatedActionCode,
            AuditActionCategory.Configuration,
            RecordType,
            reason: trimmedReason,
            entityId: id.ToString(),
            changes: new[] { new AuditFieldChange("Method", before, after) },
            cancellationToken: ct);

        return response;
    }

    public async Task<List<HplcMethodHistoryEntry>> GetHistoryAsync(int id, int currentUserId, CancellationToken ct = default)
    {
        var method = await _db.HplcMethods.FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NotFoundException($"HPLC method {id} not found.");

        var scope = await _scope.GetAccessibleSectionIdsAsync(currentUserId, ct);
        if (scope != null && !scope.Contains(method.SectionId))
            throw new NotFoundException($"HPLC method {id} not found.");

        var logs = await _db.AuditLogs
            .Include(a => a.Changes)
            .Where(a => a.EntityName == RecordType && a.EntityId == id.ToString())
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(ct);

        var userIds = logs.Where(l => l.UserId.HasValue).Select(l => l.UserId!.Value).Distinct().ToList();
        var userNames = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return logs.Select(l =>
        {
            var change = l.Changes.FirstOrDefault();
            var userName = l.UserId.HasValue && userNames.TryGetValue(l.UserId.Value, out var name) ? name : "Unknown";
            return new HplcMethodHistoryEntry(l.Timestamp, userName, l.ActionCode ?? l.Action, l.Reason, change?.PreviousValue, change?.NewValue);
        }).ToList();
    }

    private IQueryable<HplcMethod> QueryWithIncludes() =>
        _db.HplcMethods
            .Include(m => m.Section)
            .Include(m => m.DiluentSolution)
            .Include(m => m.MobilePhases).ThenInclude(p => p.SolutionMaster)
            .Include(m => m.GradientSteps)
            .Include(m => m.Analytes).ThenInclude(a => a.StandardEntry);

    private static string ValidateReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason is required.");
        var trimmed = reason.Trim();
        if (trimmed.Length > 500)
            throw new InvalidOperationException("Reason cannot exceed 500 characters.");
        return trimmed;
    }

    private static void ApplyFields(HplcMethod method, SaveHplcMethodRequest r, string abbreviation)
    {
        method.Name = r.Name.Trim();
        method.Abbreviation = abbreviation;
        method.EffectiveDate = r.EffectiveDate;
        method.ColumnDesignation = r.ColumnDesignation.Trim();
        method.ColumnLengthMm = r.ColumnLengthMm;
        method.ColumnInternalDiameterMm = r.ColumnInternalDiameterMm;
        method.ParticleSizeUm = r.ParticleSizeUm;
        method.ColumnBrand = r.ColumnBrand;
        method.ColumnPartNumber = r.ColumnPartNumber;
        method.ColumnTemperatureC = r.ColumnTemperatureC;
        method.ElutionMode = r.ElutionMode;
        method.EquilibrationMin = r.EquilibrationMin;
        method.FlowRateMlPerMin = r.FlowRateMlPerMin;
        method.DetectorType = r.DetectorType;
        method.InjectionVolumeUl = r.InjectionVolumeUl;
        method.RunTimeMin = r.RunTimeMin;
        method.DiluentSolutionId = r.DiluentSolutionId;
    }

    private void ReplaceMobilePhases(HplcMethod method, SaveHplcMethodRequest r)
    {
        _db.HplcMethodMobilePhases.RemoveRange(method.MobilePhases);
        method.MobilePhases.Clear();
        foreach (var mp in r.MobilePhases)
        {
            method.MobilePhases.Add(new HplcMethodMobilePhase
            {
                Channel = mp.Channel.Trim().ToUpperInvariant(),
                SolutionMasterId = mp.SolutionMasterId,
                RatioPercent = mp.RatioPercent
            });
        }
    }

    private void ReplaceGradientSteps(HplcMethod method, SaveHplcMethodRequest r)
    {
        _db.HplcMethodGradientSteps.RemoveRange(method.GradientSteps);
        method.GradientSteps.Clear();
        foreach (var gs in r.GradientSteps)
        {
            method.GradientSteps.Add(new HplcMethodGradientStep
            {
                TimeMin = gs.TimeMin,
                PercentA = gs.PercentA,
                PercentB = gs.PercentB,
                PercentC = gs.PercentC,
                PercentD = gs.PercentD
            });
        }
    }

    // Matched by Id so unchanged analytes keep their Id (specification rows
    // stay linked); rows with no matching Id are added; rows missing from
    // the request are removed (the caller has already refused this when a
    // specification references them).
    private void ApplyAnalytes(HplcMethod method, SaveHplcMethodRequest r)
    {
        var existingById = method.Analytes.Where(a => a.Id != 0).ToDictionary(a => a.Id);
        var keepIds = new HashSet<int>();
        var order = 1;
        foreach (var input in r.Analytes)
        {
            HplcMethodAnalyte analyte;
            if (input.Id.HasValue && existingById.TryGetValue(input.Id.Value, out var existing))
            {
                analyte = existing;
                keepIds.Add(existing.Id);
            }
            else
            {
                analyte = new HplcMethodAnalyte();
                method.Analytes.Add(analyte);
            }

            analyte.DisplayOrder = order++;
            analyte.Name = input.Name.Trim();
            analyte.WavelengthNm = input.WavelengthNm;
            analyte.StandardEntryId = input.StandardEntryId;
            analyte.TheoreticalWeightStdMg = input.TheoreticalWeightStdMg;
            analyte.TheoreticalWeightTestMg = input.TheoreticalWeightTestMg;
            analyte.StandardInjections = input.StandardInjections;
            analyte.SstMaxRsdPercent = input.SstMaxRsdPercent;
            analyte.SstMinResolution = input.SstMinResolution;
            analyte.SstMaxTailingFactor = input.SstMaxTailingFactor;
            analyte.SstMinTheoreticalPlates = input.SstMinTheoreticalPlates;
            analyte.SstMinRetentionFactor = input.SstMinRetentionFactor;
            analyte.SstMinSignalToNoise = input.SstMinSignalToNoise;
            analyte.SstMinPeakToValley = input.SstMinPeakToValley;
        }

        var toRemove = method.Analytes.Where(a => a.Id != 0 && !keepIds.Contains(a.Id)).ToList();
        if (toRemove.Count > 0)
        {
            _db.HplcMethodAnalytes.RemoveRange(toRemove);
            foreach (var rem in toRemove)
                method.Analytes.Remove(rem);
        }
    }

    private async Task<string> ValidateCommonAsync(
        SaveHplcMethodRequest r, int sectionId,
        HashSet<int>? alreadyUsedSolutionIds, HashSet<int>? alreadyUsedStandardIds,
        CancellationToken ct)
    {
        alreadyUsedSolutionIds ??= new HashSet<int>();
        alreadyUsedStandardIds ??= new HashSet<int>();

        if (string.IsNullOrWhiteSpace(r.Name))
            throw new InvalidOperationException("Name is required.");

        if (string.IsNullOrWhiteSpace(r.Abbreviation))
            throw new InvalidOperationException("Abbreviation is required.");
        var abbr = r.Abbreviation.Trim().ToUpperInvariant();
        if (abbr.Length < 2 || abbr.Length > 20 || !System.Text.RegularExpressions.Regex.IsMatch(abbr, "^[A-Z0-9-]+$"))
            throw new InvalidOperationException("Abbreviation must be 2-20 uppercase letters, digits or hyphens.");

        if (string.IsNullOrWhiteSpace(r.ColumnDesignation))
            throw new InvalidOperationException("Column designation is required.");

        if (r.ColumnLengthMm <= 0m || r.ColumnInternalDiameterMm <= 0m || r.ParticleSizeUm <= 0m || r.ColumnTemperatureC <= 0m
            || r.FlowRateMlPerMin <= 0m || r.InjectionVolumeUl <= 0m || r.RunTimeMin <= 0m)
            throw new InvalidOperationException("Column and run parameters must be greater than zero.");

        if (r.EquilibrationMin.HasValue && r.EquilibrationMin.Value < 0m)
            throw new InvalidOperationException("Equilibration time must be zero or more.");

        // Diluent - Review Focus: a diluent pointing at a Mobile Phase solution is refused.
        var diluent = await _db.SolutionMasters.FirstOrDefaultAsync(s => s.Id == r.DiluentSolutionId, ct)
            ?? throw new InvalidOperationException("Diluent solution not found.");
        if (diluent.SectionId != sectionId)
            throw new InvalidOperationException("The diluent solution belongs to another laboratory.");
        if (diluent.Type != SolutionType.Diluent)
            throw new InvalidOperationException("The diluent solution must be of type Diluent.");
        if (!diluent.IsActive && !alreadyUsedSolutionIds.Contains(diluent.Id))
            throw new InvalidOperationException("The diluent solution is inactive.");

        // Mobile phases
        if (r.MobilePhases.Count < 1)
            throw new InvalidOperationException("At least one mobile phase is required.");

        var channels = new HashSet<string>();
        foreach (var mp in r.MobilePhases)
        {
            var channel = mp.Channel?.Trim().ToUpperInvariant() ?? string.Empty;
            if (channel.Length != 1 || channel[0] < 'A' || channel[0] > 'D')
                throw new InvalidOperationException("Mobile phase channel must be A, B, C or D.");
            if (!channels.Add(channel))
                throw new InvalidOperationException($"Channel \"{channel}\" is used more than once.");
        }

        // Review Focus: a mobile phase channel pointing at a Diluent solution is refused.
        foreach (var mp in r.MobilePhases)
        {
            var solution = await _db.SolutionMasters.FirstOrDefaultAsync(s => s.Id == mp.SolutionMasterId, ct)
                ?? throw new InvalidOperationException($"Solution {mp.SolutionMasterId} not found.");
            if (solution.SectionId != sectionId)
                throw new InvalidOperationException($"Mobile phase solution \"{solution.Name}\" belongs to another laboratory.");
            if (solution.Type != SolutionType.MobilePhase)
                throw new InvalidOperationException($"Mobile phase solution \"{solution.Name}\" must be of type MobilePhase.");
            if (!solution.IsActive && !alreadyUsedSolutionIds.Contains(solution.Id))
                throw new InvalidOperationException($"Mobile phase solution \"{solution.Name}\" is inactive.");
        }

        // Ratios: all null, or all given and summing to 100 (isocratic only; must be null for gradient).
        var ratios = r.MobilePhases.Select(mp => mp.RatioPercent).ToList();
        var anyRatio = ratios.Any(x => x.HasValue);
        var allRatio = ratios.All(x => x.HasValue);
        if (r.ElutionMode == ElutionMode.Gradient)
        {
            if (anyRatio)
                throw new InvalidOperationException("Ratio percent is not used in gradient mode.");
        }
        else
        {
            if (anyRatio && !allRatio)
                throw new InvalidOperationException("Provide a ratio percent for every mobile phase channel, or none.");
            if (allRatio && ratios.Sum(x => x!.Value) != 100m)
                throw new InvalidOperationException("Mobile phase ratios must sum to 100%.");
        }

        // Gradient table
        if (r.ElutionMode == ElutionMode.Gradient)
        {
            if (r.GradientSteps.Count < 2)
                throw new InvalidOperationException("Gradient elution requires at least 2 steps.");

            decimal? previousTime = null;
            for (var i = 0; i < r.GradientSteps.Count; i++)
            {
                var step = r.GradientSteps[i];
                if (i == 0 && step.TimeMin != 0m)
                    throw new InvalidOperationException("The first gradient step must start at time 0.");
                if (previousTime.HasValue && step.TimeMin <= previousTime.Value)
                    throw new InvalidOperationException("Gradient step times must strictly increase.");
                previousTime = step.TimeMin;

                if (step.PercentA + step.PercentB + step.PercentC + step.PercentD != 100m)
                    throw new InvalidOperationException($"Gradient step at {step.TimeMin} min: %A+%B+%C+%D must equal 100.");

                if (!channels.Contains("A") && step.PercentA != 0m)
                    throw new InvalidOperationException("Gradient step uses channel A, which is not a configured mobile phase.");
                if (!channels.Contains("B") && step.PercentB != 0m)
                    throw new InvalidOperationException("Gradient step uses channel B, which is not a configured mobile phase.");
                if (!channels.Contains("C") && step.PercentC != 0m)
                    throw new InvalidOperationException("Gradient step uses channel C, which is not a configured mobile phase.");
                if (!channels.Contains("D") && step.PercentD != 0m)
                    throw new InvalidOperationException("Gradient step uses channel D, which is not a configured mobile phase.");
            }
        }
        else if (r.GradientSteps.Count > 0)
        {
            throw new InvalidOperationException("Isocratic elution must not have gradient steps.");
        }

        // Analytes
        if (r.Analytes.Count < 1)
            throw new InvalidOperationException("At least one analyte is required.");

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in r.Analytes)
        {
            if (string.IsNullOrWhiteSpace(a.Name))
                throw new InvalidOperationException("Analyte name is required.");
            if (!names.Add(a.Name.Trim()))
                throw new InvalidOperationException($"Analyte name \"{a.Name}\" is used more than once.");

            if (a.WavelengthNm < 190m || a.WavelengthNm > 900m)
                throw new InvalidOperationException("Analyte wavelength must be between 190 and 900 nm.");

            if (a.TheoreticalWeightStdMg <= 0m || a.TheoreticalWeightTestMg <= 0m)
                throw new InvalidOperationException("Theoretical weights must be greater than zero.");

            if (a.StandardInjections < 1)
                throw new InvalidOperationException("Standard injections must be at least 1.");

            if ((a.SstMaxRsdPercent.HasValue && a.SstMaxRsdPercent.Value <= 0m)
                || (a.SstMinResolution.HasValue && a.SstMinResolution.Value <= 0m)
                || (a.SstMaxTailingFactor.HasValue && a.SstMaxTailingFactor.Value <= 0m)
                || (a.SstMinTheoreticalPlates.HasValue && a.SstMinTheoreticalPlates.Value <= 0m)
                || (a.SstMinRetentionFactor.HasValue && a.SstMinRetentionFactor.Value <= 0m)
                || (a.SstMinSignalToNoise.HasValue && a.SstMinSignalToNoise.Value <= 0m)
                || (a.SstMinPeakToValley.HasValue && a.SstMinPeakToValley.Value <= 0m))
                throw new InvalidOperationException("System suitability criteria must be greater than zero when given.");

            var standard = await _db.MaterialMasterEntries.FirstOrDefaultAsync(e => e.Id == a.StandardEntryId, ct)
                ?? throw new InvalidOperationException($"Standard entry {a.StandardEntryId} not found.");
            if (standard.SectionId != sectionId)
                throw new InvalidOperationException($"Standard entry \"{standard.Code}\" belongs to another laboratory.");
            if (standard.Category != MaterialMasterCategory.ReferenceStandard)
                throw new InvalidOperationException($"Standard entry \"{standard.Code}\" must be a reference standard.");
            if (!standard.IsActive && !alreadyUsedStandardIds.Contains(standard.Id))
                throw new InvalidOperationException($"Standard entry \"{standard.Code}\" is inactive.");
        }

        return abbr;
    }
}
