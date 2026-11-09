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
public record GcOvenStepInput(decimal? RateCPerMin, decimal TemperatureC, decimal HoldMin);
public record HplcImpurityInput(int? Id, string Name, decimal? RelativeRetention, decimal? CorrectionFactor);
public record HplcAnalyteInput(int? Id, string Name, decimal? WavelengthNm, int StandardEntryId,
    decimal TheoreticalWeightStdMg, decimal TheoreticalWeightTestMg, int StandardInjections,
    decimal? SstMaxRsdPercent = null, decimal? SstMinResolution = null, decimal? SstMaxTailingFactor = null,
    decimal? SstMinTheoreticalPlates = null, decimal? SstMinRetentionFactor = null,
    decimal? SstMinSignalToNoise = null, decimal? SstMinPeakToValley = null, decimal? StandardDilution = null,
    decimal? StandardConcentrationUgPerMl = null,
    decimal? MacoLimit = null, MacoUnit? MacoUnit = null);

public record SaveHplcMethodRequest(
    string Name, string Abbreviation, DateTime EffectiveDate,
    string ColumnDesignation, decimal ColumnLengthMm, decimal ColumnInternalDiameterMm, decimal? ParticleSizeUm,
    decimal? ColumnTemperatureC, ElutionMode ElutionMode, decimal FlowRateMlPerMin,
    HplcDetectorType DetectorType, decimal InjectionVolumeUl, decimal RunTimeMin, int DiluentSolutionId,
    List<HplcMobilePhaseInput> MobilePhases, List<HplcGradientStepInput> GradientSteps, List<HplcAnalyteInput> Analytes,
    string? ColumnBrand = null, string? ColumnPartNumber = null, decimal? EquilibrationMin = null,
    int? SectionId = null, string? Reason = null,   // Reason required on update, ignored on create
    HplcTechnique Technique = HplcTechnique.Hplc, HplcResultMode ResultMode = HplcResultMode.Assay,
    decimal? FilmThicknessUm = null, CarrierGas? CarrierGas = null, decimal? SplitRatio = null,
    decimal? InletTemperatureC = null, decimal? DetectorTemperatureC = null,
    List<GcOvenStepInput>? OvenSteps = null,
    bool HeadspaceEnabled = false, decimal? HeadspaceEquilibrationTemperatureC = null,
    decimal? HeadspaceEquilibrationMin = null, decimal? HeadspaceTransferLineTemperatureC = null,
    decimal? SampleSolutionVolumeMl = null,
    decimal? ReferenceSolutionPercent = null, decimal? DisregardLimitPercent = null,
    List<HplcImpurityInput>? Impurities = null, string? InternalStandardName = null,
    int? ProductItemId = null, decimal? SwabAreaCm2 = null);

public record HplcMethodListItem(int Id, string Name, string Abbreviation, bool IsActive, int AnalyteCount, string SectionName, DateTime LastModifiedAt,
    HplcTechnique Technique = HplcTechnique.Hplc, HplcResultMode ResultMode = HplcResultMode.Assay);
public record HplcMethodHistoryEntry(DateTime At, string UserName, string Action, string? Reason, string? BeforeJson, string? AfterJson);

// HPLC method master (HPLC chain S3, spec 3.3): parameters, gradient, mobile
// phases + diluent, analytes with Th.Wt and SST criteria. Modelled on
// SolutionMasterService (S2): edit in place with a required reason, no
// versions (D7). Each edit writes one audit event whose single change is the
// whole method as JSON before -> after, so the history screen reads one list.
public partial class HplcMethodService
{
    private const string CreatedActionCode = "HplcMethod.Created";
    private const string UpdatedActionCode = "HplcMethod.Updated";
    private const string DeactivatedActionCode = "HplcMethod.Deactivated";
    private const string ActivatedActionCode = "HplcMethod.Activated";
    private const string RecordType = "HplcMethod";

    private static readonly JsonSerializerOptions JsonOptions = SnapshotJson.Options;

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

    public async Task<List<HplcMethodListItem>> GetAllAsync(int currentUserId, bool activeOnly = false, HplcTechnique? technique = null, CancellationToken ct = default)
    {
        var scope = await _scope.GetReadableSectionIdsAsync(currentUserId, ct);
        var query = _db.HplcMethods.Include(m => m.Section).Include(m => m.Analytes).AsNoTracking().AsQueryable();

        if (scope != null)
            query = query.Where(m => scope.Contains(m.SectionId));

        if (activeOnly)
            query = query.Where(m => m.IsActive);

        if (technique.HasValue)
            query = query.Where(m => m.Technique == technique.Value);

        return (await query.OrderBy(m => m.Name).ToListAsync(ct))
            .Select(m => new HplcMethodListItem(m.Id, m.Name, m.Abbreviation, m.IsActive, m.Analytes.Count, m.Section?.Name ?? string.Empty, m.LastModifiedAt, m.Technique, m.ResultMode))
            .ToList();
    }

    public async Task<HplcMethodResponse> GetByIdAsync(int id, int currentUserId, CancellationToken ct = default)
    {
        var method = await QueryWithIncludes().AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NotFoundException($"HPLC method {id} not found.");

        var scope = await _scope.GetReadableSectionIdsAsync(currentUserId, ct);
        if (scope != null && !scope.Contains(method.SectionId))
            throw new NotFoundException($"HPLC method {id} not found.");

        return HplcMethodResponse.From(method);
    }

    public async Task<HplcMethodResponse> CreateAsync(SaveHplcMethodRequest r, int currentUserId, CancellationToken ct = default)
    {
        var sectionId = await _scope.ResolveSectionForCreateAsync(currentUserId, r.SectionId, ct, SectionAccess.Configure);

        var abbr = await ValidateCommonAsync(r, sectionId, null, null, ct);

        if (await _db.HplcMethods.AnyAsync(m => m.SectionId == sectionId && m.Abbreviation == abbr, ct))
            throw new BusinessRuleException($"An HPLC method abbreviated \"{abbr}\" already exists.");

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
        ReplaceOvenSteps(method, r);
        ApplyAnalytes(method, r);
        ApplyImpurities(method, r);

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
            .Include(m => m.OvenSteps)
            .Include(m => m.Analytes)
            .Include(m => m.Impurities)
            .FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NotFoundException($"HPLC method {id} not found.");

        RecordVersion.EnsureCurrent(_db, method);

        if (r.Technique != method.Technique || r.ResultMode != method.ResultMode)
            throw new BusinessRuleException("The technique and result mode can't be changed after the method is created.");

        var normalizedIs = string.IsNullOrWhiteSpace(r.InternalStandardName) ? null : r.InternalStandardName.Trim();
        if (normalizedIs != null && (r.Technique != HplcTechnique.Hplc || r.ResultMode != HplcResultMode.Assay))
            throw new BusinessRuleException("An internal standard applies to HPLC assay methods only.");

        if (normalizedIs != method.InternalStandardName)
            throw new BusinessRuleException("The internal standard can't be changed after the method is created.");

        var alreadyUsedSolutionIds = new HashSet<int>(method.MobilePhases.Select(p => p.SolutionMasterId)) { method.DiluentSolutionId };
        var alreadyUsedStandardIds = new HashSet<int>(method.Analytes.Select(a => a.StandardEntryId));

        var abbr = await ValidateCommonAsync(r, method.SectionId, alreadyUsedSolutionIds, alreadyUsedStandardIds, ct);

        if (await _db.HplcMethods.AnyAsync(m => m.SectionId == method.SectionId && m.Abbreviation == abbr && m.Id != id, ct))
            throw new BusinessRuleException($"An HPLC method abbreviated \"{abbr}\" already exists.");

        // Never orphan a specification row - refuse before any child is touched.
        var incomingAnalyteIds = r.Analytes.Where(a => a.Id.HasValue).Select(a => a.Id!.Value).ToHashSet();
        foreach (var existingAnalyte in method.Analytes.Where(a => !incomingAnalyteIds.Contains(a.Id)))
        {
            if (await _db.Specifications.AnyAsync(s => s.HplcMethodAnalyteId == existingAnalyte.Id, ct))
                throw new BusinessRuleException($"Analyte \"{existingAnalyte.Name}\" is used by specifications; it can't be removed.");
            if (await _db.WorkingStandardQualifications.AnyAsync(w => w.HplcMethodAnalyteId == existingAnalyte.Id, ct))
                throw new BusinessRuleException($"Analyte \"{existingAnalyte.Name}\" is used by working standard qualifications; it can't be removed.");
        }

        var ownImpurityIds = method.Impurities.Select(i => i.Id).ToHashSet();
        var seenImpurityIds = new HashSet<int>();
        foreach (var impurityId in (r.Impurities ?? new()).Where(i => i.Id.HasValue).Select(i => i.Id!.Value))
        {
            if (!ownImpurityIds.Contains(impurityId))
                throw new BusinessRuleException($"Impurity {impurityId} does not belong to this method.");
            if (!seenImpurityIds.Add(impurityId))
                throw new BusinessRuleException("Each impurity can appear only once.");
        }

        // Same for impurities named by a related-substances specification row.
        var incomingImpurityIds = (r.Impurities ?? new()).Where(i => i.Id.HasValue).Select(i => i.Id!.Value).ToHashSet();
        var removedImpurityIds = method.Impurities.Where(i => !incomingImpurityIds.Contains(i.Id)).Select(i => i.Id).ToList();
        if (removedImpurityIds.Count > 0)
        {
            var usedImpurityId = await _db.Specifications
                .Where(s => s.HplcMethodImpurityId != null && removedImpurityIds.Contains(s.HplcMethodImpurityId.Value))
                .Select(s => s.HplcMethodImpurityId).FirstOrDefaultAsync(ct);
            if (usedImpurityId != null)
                throw new BusinessRuleException($"Impurity {method.Impurities.First(i => i.Id == usedImpurityId).Name} is used by a specification and cannot be removed.");

            // ... and by peaks recorded on samples.
            var peakImpurityId = await _db.HplcReplicatePeaks
                .Where(p => p.HplcMethodImpurityId != null && removedImpurityIds.Contains(p.HplcMethodImpurityId.Value))
                .Select(p => p.HplcMethodImpurityId).FirstOrDefaultAsync(ct);
            if (peakImpurityId != null)
                throw new BusinessRuleException($"Impurity {method.Impurities.First(i => i.Id == peakImpurityId).Name} has recorded peaks and cannot be removed.");

            // ... and by the snapshot of an open run, which validates entered peaks against it.
            var openSnapshots = await _db.HplcRuns
                .Where(run => run.HplcMethodId == id && run.Status == HplcRunStatus.Open)
                .Select(run => run.MethodSnapshotJson).ToListAsync(ct);
            foreach (var json in openSnapshots)
            {
                var snapshotImpurities = JsonSerializer.Deserialize<HplcMethodResponse>(json, JsonOptions)?.Impurities;
                var used = snapshotImpurities?.FirstOrDefault(i => removedImpurityIds.Contains(i.Id));
                if (used != null)
                    throw new BusinessRuleException($"Impurity {used.Name} is used by an open HPLC run and cannot be removed.");
            }
        }

        ApplyFields(method, r, abbr);
        ReplaceMobilePhases(method, r);
        ReplaceGradientSteps(method, r);
        ReplaceOvenSteps(method, r);
        ApplyAnalytes(method, r);
        ApplyImpurities(method, r);
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

        var scope = await _scope.GetConfigurableSectionIdsAsync(currentUserId, ct);
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

        var scope = await _scope.GetReadableSectionIdsAsync(currentUserId, ct);
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
            .Include(m => m.ProductItem)
            .Include(m => m.MobilePhases).ThenInclude(p => p.SolutionMaster)
            .Include(m => m.GradientSteps)
            .Include(m => m.OvenSteps)
            .Include(m => m.Analytes).ThenInclude(a => a.StandardEntry)
            .Include(m => m.Impurities);

    private static string ValidateReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleException("A reason is required.");
        var trimmed = reason.Trim();
        if (trimmed.Length > 500)
            throw new BusinessRuleException("Reason cannot exceed 500 characters.");
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
        method.Technique = r.Technique;
        method.ResultMode = r.ResultMode;
        method.FilmThicknessUm = r.FilmThicknessUm;
        method.CarrierGas = r.CarrierGas;
        method.SplitRatio = r.SplitRatio;
        method.InletTemperatureC = r.InletTemperatureC;
        method.DetectorTemperatureC = r.DetectorTemperatureC;
        method.HeadspaceEnabled = r.HeadspaceEnabled;
        method.HeadspaceEquilibrationTemperatureC = r.HeadspaceEquilibrationTemperatureC;
        method.HeadspaceEquilibrationMin = r.HeadspaceEquilibrationMin;
        method.HeadspaceTransferLineTemperatureC = r.HeadspaceTransferLineTemperatureC;
        if (r.ResultMode == HplcResultMode.CleaningResidue)
        {
            method.ProductItemId = r.ProductItemId;
            method.SwabAreaCm2 = r.SwabAreaCm2;
            method.SampleSolutionVolumeMl = r.SampleSolutionVolumeMl;
        }
        else
        {
            method.ProductItemId = null;
            method.SwabAreaCm2 = null;
            method.SampleSolutionVolumeMl = r.ResultMode == HplcResultMode.ResidualSolvents ? r.SampleSolutionVolumeMl : null;
        }
        method.ReferenceSolutionPercent = r.ReferenceSolutionPercent;
        method.DisregardLimitPercent = r.DisregardLimitPercent;
        method.InternalStandardName = string.IsNullOrWhiteSpace(r.InternalStandardName) ? null : r.InternalStandardName.Trim();
    }

    private void ReplaceOvenSteps(HplcMethod method, SaveHplcMethodRequest r)
    {
        _db.HplcMethodOvenSteps.RemoveRange(method.OvenSteps);
        method.OvenSteps.Clear();
        var no = 1;
        foreach (var os in r.OvenSteps ?? new List<GcOvenStepInput>())
        {
            method.OvenSteps.Add(new HplcMethodOvenStep
            {
                StepNo = no++,
                RateCPerMin = os.RateCPerMin,
                TemperatureC = os.TemperatureC,
                HoldMin = os.HoldMin
            });
        }
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
            analyte.StandardDilution = input.StandardDilution;
            if (r.ResultMode == HplcResultMode.CleaningResidue)
            {
                analyte.StandardConcentrationUgPerMl = input.StandardConcentrationUgPerMl;
                analyte.MacoLimit = input.MacoLimit;
                analyte.MacoUnit = input.MacoUnit;
            }
            else
            {
                analyte.StandardConcentrationUgPerMl = r.ResultMode == HplcResultMode.ResidualSolvents ? input.StandardConcentrationUgPerMl : null;
                analyte.MacoLimit = null;
                analyte.MacoUnit = null;
            }
        }

        var toRemove = method.Analytes.Where(a => a.Id != 0 && !keepIds.Contains(a.Id)).ToList();
        if (toRemove.Count > 0)
        {
            _db.HplcMethodAnalytes.RemoveRange(toRemove);
            foreach (var rem in toRemove)
                method.Analytes.Remove(rem);
        }
    }

    // Same pattern as ApplyAnalytes: matched by Id so unchanged impurities keep their Id.
    private void ApplyImpurities(HplcMethod method, SaveHplcMethodRequest r)
    {
        var incoming = r.Impurities ?? new List<HplcImpurityInput>();
        var existingById = method.Impurities.Where(i => i.Id != 0).ToDictionary(i => i.Id);
        var keepIds = new HashSet<int>();
        var order = 1;
        foreach (var input in incoming)
        {
            HplcMethodImpurity impurity;
            if (input.Id.HasValue && existingById.TryGetValue(input.Id.Value, out var existing))
            {
                impurity = existing;
                keepIds.Add(existing.Id);
            }
            else
            {
                impurity = new HplcMethodImpurity();
                method.Impurities.Add(impurity);
            }

            impurity.DisplayOrder = order++;
            impurity.Name = input.Name.Trim();
            impurity.RelativeRetention = input.RelativeRetention;
            impurity.CorrectionFactor = input.CorrectionFactor ?? 1m;
        }

        var toRemove = method.Impurities.Where(i => i.Id != 0 && !keepIds.Contains(i.Id)).ToList();
        if (toRemove.Count > 0)
        {
            _db.HplcMethodImpurities.RemoveRange(toRemove);
            foreach (var rem in toRemove)
                method.Impurities.Remove(rem);
        }
    }

    private static void ValidateRelatedSubstances(SaveHplcMethodRequest r)
    {
        if (r.Analytes.Any(a => a.TheoreticalWeightStdMg != 0m || a.TheoreticalWeightTestMg != 0m))
            throw new BusinessRuleException("Theoretical weights are not used in a related-substances method.");
        if (!(r.ReferenceSolutionPercent > 0m && r.ReferenceSolutionPercent <= 100m))
            throw new BusinessRuleException("Enter the reference solution concentration as % of the test solution (above 0, up to 100).");
        if (r.DisregardLimitPercent < 0m || r.DisregardLimitPercent > 100m)
            throw new BusinessRuleException("The disregard limit must be between 0 and 100 %.");

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var i in r.Impurities ?? new List<HplcImpurityInput>())
        {
            if (string.IsNullOrWhiteSpace(i.Name) || !names.Add(i.Name.Trim()))
                throw new BusinessRuleException("Each impurity needs a unique name.");
            if (i.Name.Trim().Length > 100)
                throw new BusinessRuleException("Impurity names are at most 100 characters.");
            if (i.CorrectionFactor <= 0m || i.CorrectionFactor > 100m)
                throw new BusinessRuleException("The correction factor must be greater than zero and at most 100.");
            if (i.RelativeRetention <= 0m || i.RelativeRetention > 100m)
                throw new BusinessRuleException("The relative retention must be greater than zero and at most 100.");
        }
    }

    private async Task ValidateCleaningResidueAsync(SaveHplcMethodRequest r, CancellationToken ct)
    {
        if (r.Technique != HplcTechnique.Hplc)
            throw new BusinessRuleException("Cleaning residue is available for HPLC methods only.");

        if (!r.ProductItemId.HasValue)
            throw new BusinessRuleException("A finished product item is required for a cleaning-residue method.");

        var item = await _db.Items.FirstOrDefaultAsync(i => i.Id == r.ProductItemId.Value, ct)
            ?? throw new BusinessRuleException($"Item {r.ProductItemId.Value} not found.");
        if (item.Category != SampleCategory.FinishedProduct)
            throw new BusinessRuleException("The cleaning-residue method must reference a Finished Product item.");

        if (!(r.SampleSolutionVolumeMl > 0m))
            throw new BusinessRuleException("Sample extraction volume (mL) must be greater than zero.");

        if (r.Analytes.Count < 1)
            throw new BusinessRuleException("At least one analyte is required.");

        var usesCm2 = false;
        foreach (var a in r.Analytes)
        {
            if (!(a.StandardConcentrationUgPerMl > 0m))
                throw new BusinessRuleException($"{a.Name}: standard concentration (µg/mL) is required.");
            if (!(a.MacoLimit > 0m))
                throw new BusinessRuleException($"{a.Name}: MACO limit must be greater than zero.");
            if (!a.MacoUnit.HasValue)
                throw new BusinessRuleException($"{a.Name}: MACO unit is required.");
            if (a.MacoUnit == MacoUnit.UgPerCm2)
                usesCm2 = true;
        }

        if (usesCm2 && !(r.SwabAreaCm2 > 0m))
            throw new BusinessRuleException("Swabbed area (cm²) must be greater than zero when an analyte uses µg/cm².");
    }

    private async Task<string> ValidateCommonAsync(
        SaveHplcMethodRequest r, int sectionId,
        HashSet<int>? alreadyUsedSolutionIds, HashSet<int>? alreadyUsedStandardIds,
        CancellationToken ct)
    {
        alreadyUsedSolutionIds ??= new HashSet<int>();
        alreadyUsedStandardIds ??= new HashSet<int>();

        var isName = string.IsNullOrWhiteSpace(r.InternalStandardName) ? null : r.InternalStandardName.Trim();
        if (isName != null)
        {
            if (r.Technique != HplcTechnique.Hplc || r.ResultMode != HplcResultMode.Assay)
                throw new BusinessRuleException("An internal standard applies to HPLC assay methods only.");
            if (isName.Length > 100)
                throw new BusinessRuleException("Internal standard name cannot exceed 100 characters.");
        }

        if (string.IsNullOrWhiteSpace(r.Name))
            throw new BusinessRuleException("Name is required.");

        if (string.IsNullOrWhiteSpace(r.Abbreviation))
            throw new BusinessRuleException("Abbreviation is required.");
        var abbr = r.Abbreviation.Trim().ToUpperInvariant();
        if (abbr.Length < 2 || abbr.Length > 20 || !System.Text.RegularExpressions.Regex.IsMatch(abbr, "^[A-Z0-9-]+$"))
            throw new BusinessRuleException("Abbreviation must be 2-20 uppercase letters, digits or hyphens.");

        if (string.IsNullOrWhiteSpace(r.ColumnDesignation))
            throw new BusinessRuleException("Column designation is required.");

        if (r.ColumnLengthMm <= 0m || r.ColumnInternalDiameterMm <= 0m || r.FlowRateMlPerMin <= 0m || r.InjectionVolumeUl <= 0m || r.RunTimeMin <= 0m)
            throw new BusinessRuleException("Column and run parameters must be greater than zero.");

        if (r.EquilibrationMin.HasValue && r.EquilibrationMin.Value < 0m)
            throw new BusinessRuleException("Equilibration time must be zero or more.");

        // Diluent - Review Focus: a diluent pointing at a Mobile Phase solution is refused.
        var diluent = await _db.SolutionMasters.FirstOrDefaultAsync(s => s.Id == r.DiluentSolutionId, ct)
            ?? throw new BusinessRuleException("Diluent solution not found.");
        if (diluent.SectionId != sectionId)
            throw new BusinessRuleException("The diluent solution belongs to another laboratory.");
        if (diluent.Type != SolutionType.Diluent)
            throw new BusinessRuleException("The diluent solution must be of type Diluent.");
        if (!diluent.IsActive && !alreadyUsedSolutionIds.Contains(diluent.Id))
            throw new BusinessRuleException("The diluent solution is inactive.");

        if (r.ResultMode == HplcResultMode.RelatedSubstances && r.Technique != HplcTechnique.Hplc)
            throw new BusinessRuleException("Related substances is available for HPLC methods only.");
        if (r.ResultMode == HplcResultMode.RelatedSubstances && r.Analytes?.Count != 1)
            throw new BusinessRuleException("A related-substances method has exactly one analyte: the principal peak.");
        if (r.ResultMode != HplcResultMode.RelatedSubstances
            && (r.Impurities?.Count > 0 || r.ReferenceSolutionPercent.HasValue || r.DisregardLimitPercent.HasValue))
            throw new BusinessRuleException("Impurities and reference-solution values belong to related-substances methods only.");

        if (r.Technique == HplcTechnique.Gc)
            ValidateGcSection(r);
        else
            await ValidateHplcSectionAsync(r, sectionId, alreadyUsedSolutionIds, ct);

        // Analytes
        if (r.Analytes.Count < 1)
            throw new BusinessRuleException("At least one analyte is required.");

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in r.Analytes)
        {
            if (string.IsNullOrWhiteSpace(a.Name))
                throw new BusinessRuleException("Analyte name is required.");
            if (!names.Add(a.Name.Trim()))
                throw new BusinessRuleException($"Analyte name \"{a.Name}\" is used more than once.");

            if (a.StandardInjections < 1)
                throw new BusinessRuleException("Standard injections must be at least 1.");

            if ((a.SstMaxRsdPercent.HasValue && a.SstMaxRsdPercent.Value <= 0m)
                || (a.SstMinResolution.HasValue && a.SstMinResolution.Value <= 0m)
                || (a.SstMaxTailingFactor.HasValue && a.SstMaxTailingFactor.Value <= 0m)
                || (a.SstMinTheoreticalPlates.HasValue && a.SstMinTheoreticalPlates.Value <= 0m)
                || (a.SstMinRetentionFactor.HasValue && a.SstMinRetentionFactor.Value <= 0m)
                || (a.SstMinSignalToNoise.HasValue && a.SstMinSignalToNoise.Value <= 0m)
                || (a.SstMinPeakToValley.HasValue && a.SstMinPeakToValley.Value <= 0m))
                throw new BusinessRuleException("System suitability criteria must be greater than zero when given.");

            var standard = await _db.MaterialMasterEntries.FirstOrDefaultAsync(e => e.Id == a.StandardEntryId, ct)
                ?? throw new BusinessRuleException($"Standard entry {a.StandardEntryId} not found.");
            if (standard.Category != MaterialMasterCategory.ReferenceStandard)
                throw new BusinessRuleException($"Standard entry \"{standard.Code}\" must be a reference standard.");
            if (!standard.IsActive && !alreadyUsedStandardIds.Contains(standard.Id))
                throw new BusinessRuleException($"Standard entry \"{standard.Code}\" is inactive.");
        }

        return abbr;
    }

    private async Task ValidateHplcSectionAsync(
        SaveHplcMethodRequest r, int sectionId, HashSet<int> alreadyUsedSolutionIds, CancellationToken ct)
    {
        if (!(r.ParticleSizeUm > 0m) || !(r.ColumnTemperatureC > 0m))
            throw new BusinessRuleException("Column and run parameters must be greater than zero.");

        if (r.DetectorType is not (HplcDetectorType.UV or HplcDetectorType.PDA or HplcDetectorType.FLD
            or HplcDetectorType.RI or HplcDetectorType.ELSD or HplcDetectorType.Other))
            throw new BusinessRuleException($"Detector {r.DetectorType} is not an HPLC detector.");

        if (r.ResultMode == HplcResultMode.ResidualSolvents)
            throw new BusinessRuleException("Residual solvents mode is only available for GC methods.");
        if (r.ResultMode == HplcResultMode.RelatedSubstances)
            ValidateRelatedSubstances(r);
        if (r.ResultMode == HplcResultMode.CleaningResidue)
            await ValidateCleaningResidueAsync(r, ct);
        else if (r.ProductItemId.HasValue || r.SwabAreaCm2.HasValue || r.Analytes.Any(a => a.MacoLimit.HasValue || a.MacoUnit.HasValue))
            throw new BusinessRuleException("Cleaning residue settings belong to cleaning residue methods only.");

        if (r.FilmThicknessUm.HasValue || r.CarrierGas.HasValue || r.SplitRatio.HasValue || r.InletTemperatureC.HasValue
            || r.DetectorTemperatureC.HasValue || (r.ResultMode != HplcResultMode.CleaningResidue && r.SampleSolutionVolumeMl.HasValue)
            || r.OvenSteps?.Count > 0 || r.HeadspaceEnabled
            || (r.ResultMode != HplcResultMode.CleaningResidue && r.Analytes.Any(a => a.StandardConcentrationUgPerMl.HasValue)))
            throw new BusinessRuleException("GC settings are not used on an HPLC method.");

        // Mobile phases
        if (r.MobilePhases.Count < 1)
            throw new BusinessRuleException("At least one mobile phase is required.");

        var channels = new HashSet<string>();
        foreach (var mp in r.MobilePhases)
        {
            var channel = mp.Channel?.Trim().ToUpperInvariant() ?? string.Empty;
            if (channel.Length != 1 || channel[0] < 'A' || channel[0] > 'D')
                throw new BusinessRuleException("Mobile phase channel must be A, B, C or D.");
            if (!channels.Add(channel))
                throw new BusinessRuleException($"Channel \"{channel}\" is used more than once.");
        }

        // Review Focus: a mobile phase channel pointing at a Diluent solution is refused.
        foreach (var mp in r.MobilePhases)
        {
            var solution = await _db.SolutionMasters.FirstOrDefaultAsync(s => s.Id == mp.SolutionMasterId, ct)
                ?? throw new BusinessRuleException($"Solution {mp.SolutionMasterId} not found.");
            if (solution.SectionId != sectionId)
                throw new BusinessRuleException($"Mobile phase solution \"{solution.Name}\" belongs to another laboratory.");
            if (solution.Type != SolutionType.MobilePhase)
                throw new BusinessRuleException($"Mobile phase solution \"{solution.Name}\" must be of type MobilePhase.");
            if (!solution.IsActive && !alreadyUsedSolutionIds.Contains(solution.Id))
                throw new BusinessRuleException($"Mobile phase solution \"{solution.Name}\" is inactive.");
        }

        // Ratios: all null, or all given and summing to 100 (isocratic only; must be null for gradient).
        var ratios = r.MobilePhases.Select(mp => mp.RatioPercent).ToList();
        var anyRatio = ratios.Any(x => x.HasValue);
        var allRatio = ratios.All(x => x.HasValue);
        if (r.ElutionMode == ElutionMode.Gradient)
        {
            if (anyRatio)
                throw new BusinessRuleException("Ratio percent is not used in gradient mode.");
        }
        else
        {
            if (anyRatio && !allRatio)
                throw new BusinessRuleException("Provide a ratio percent for every mobile phase channel, or none.");
            if (allRatio && ratios.Sum(x => x!.Value) != 100m)
                throw new BusinessRuleException("Mobile phase ratios must sum to 100%.");
        }

        // Gradient table
        if (r.ElutionMode == ElutionMode.Gradient)
        {
            if (r.GradientSteps.Count < 2)
                throw new BusinessRuleException("Gradient elution requires at least 2 steps.");

            decimal? previousTime = null;
            for (var i = 0; i < r.GradientSteps.Count; i++)
            {
                var step = r.GradientSteps[i];
                if (i == 0 && step.TimeMin != 0m)
                    throw new BusinessRuleException("The first gradient step must start at time 0.");
                if (previousTime.HasValue && step.TimeMin <= previousTime.Value)
                    throw new BusinessRuleException("Gradient step times must strictly increase.");
                previousTime = step.TimeMin;

                if (step.PercentA + step.PercentB + step.PercentC + step.PercentD != 100m)
                    throw new BusinessRuleException($"Gradient step at {step.TimeMin} min: %A+%B+%C+%D must equal 100.");

                if (!channels.Contains("A") && step.PercentA != 0m)
                    throw new BusinessRuleException("Gradient step uses channel A, which is not a configured mobile phase.");
                if (!channels.Contains("B") && step.PercentB != 0m)
                    throw new BusinessRuleException("Gradient step uses channel B, which is not a configured mobile phase.");
                if (!channels.Contains("C") && step.PercentC != 0m)
                    throw new BusinessRuleException("Gradient step uses channel C, which is not a configured mobile phase.");
                if (!channels.Contains("D") && step.PercentD != 0m)
                    throw new BusinessRuleException("Gradient step uses channel D, which is not a configured mobile phase.");
            }
        }
        else if (r.GradientSteps.Count > 0)
        {
            throw new BusinessRuleException("Isocratic elution must not have gradient steps.");
        }

        foreach (var a in r.Analytes)
        {
            if (!(a.WavelengthNm >= 190m && a.WavelengthNm <= 900m))
                throw new BusinessRuleException("Analyte wavelength must be between 190 and 900 nm.");

            if (r.ResultMode is not (HplcResultMode.RelatedSubstances or HplcResultMode.CleaningResidue)
                && (a.TheoreticalWeightStdMg <= 0m || a.TheoreticalWeightTestMg <= 0m))
                throw new BusinessRuleException("Theoretical weights must be greater than zero.");

            if (a.StandardDilution.HasValue && a.StandardDilution.Value <= 0m)
                throw new BusinessRuleException("Standard dilution must be greater than zero.");
        }
    }
}
