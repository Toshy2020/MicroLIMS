using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.EntryDefaults;

public sealed record EntryDefault(
    string Field,
    int? Id,
    string? Text,
    string Label,
    DateTime LastUsedAtUtc);

public sealed record EntryDefaultsResponse(IReadOnlyList<EntryDefault> Defaults);

public sealed class EntryDefaultsService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly IncubatorEligibilityService _incubatorEligibility;
    private readonly ISystemSettingsReader _settings;
    private readonly TimeProvider _timeProvider;

    public EntryDefaultsService(
        IMicroLimsDbContext db,
        IUserSectionScopeService scope,
        IncubatorEligibilityService incubatorEligibility,
        ISystemSettingsReader settings,
        TimeProvider timeProvider)
    {
        _db = db;
        _scope = scope;
        _incubatorEligibility = incubatorEligibility;
        _settings = settings;
        _timeProvider = timeProvider;
    }

    public async Task<EntryDefaultsResponse> GetAsync(
        int userId,
        string context,
        int? testOrderId = null,
        int? stepMediaId = null,
        int? mediaProductId = null,
        int? solutionMasterId = null,
        string? category = null,
        CancellationToken ct = default)
    {
        if (!await _settings.GetBoolAsync(SystemSettingKeys.LastChoiceDefaultsEnabled, ct))
            return new EntryDefaultsResponse(Array.Empty<EntryDefault>());

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        return context switch
        {
            "incubation" => await GetIncubationDefaultsAsync(userId, testOrderId, stepMediaId, mediaProductId, nowUtc, ct),
            "mediaPreparation" => await GetMediaPreparationDefaultsAsync(userId, nowUtc, ct),
            "solutionPreparation" => await GetSolutionPreparationDefaultsAsync(userId, solutionMasterId, nowUtc, ct),
            "receiving" => await GetReceivingDefaultsAsync(userId, category, ct),
            _ => new EntryDefaultsResponse(Array.Empty<EntryDefault>())
        };
    }

    private async Task<EntryDefaultsResponse> GetIncubationDefaultsAsync(
        int userId,
        int? testOrderId,
        int? stepMediaId,
        int? mediaProductId,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var defaults = new List<EntryDefault>(2);

        int? targetSectionId = null;
        var stepMediaCandidates = new List<(int Id, int? MediaProductId, int? MaterialProductId)>();

        if (testOrderId.HasValue)
        {
            var orderInfo = await _db.TestOrders
                .AsNoTracking()
                .Where(o => o.Id == testOrderId.Value)
                .Select(o => new
                {
                    o.SectionId,
                    o.TestCode,
                    CompletedSteps = o.Incubations
                        .Where(i => i.CompletedAt != null)
                        .Select(i => i.StepNumber)
                        .ToList()
                })
                .FirstOrDefaultAsync(ct);

            if (orderInfo is not null)
            {
                targetSectionId = orderInfo.SectionId;
                var nextStepOrder = orderInfo.CompletedSteps.Count == 0
                    ? 1
                    : orderInfo.CompletedSteps.Max() + 1;

                var stepMedias = await _db.TestWorkflowStepMedias
                    .AsNoTracking()
                    .Where(sm => sm.TestWorkflowStep != null
                        && sm.TestWorkflowStep.TestDefinition != null
                        && sm.TestWorkflowStep.TestDefinition.Code == orderInfo.TestCode
                        && sm.TestWorkflowStep.StepOrder == nextStepOrder)
                    .OrderBy(sm => sm.DisplayOrder)
                    .ThenBy(sm => sm.Id)
                    .Select(sm => new
                    {
                        sm.Id,
                        sm.MediaProductId,
                        MaterialProductId = sm.Material != null ? sm.Material.MediaProductId : null
                    })
                    .ToListAsync(ct);

                foreach (var sm in stepMedias)
                    stepMediaCandidates.Add((sm.Id, sm.MediaProductId, sm.MaterialProductId));
            }
        }

        (int Id, int? MediaProductId, int? MaterialProductId)? explicitStepMedia = null;
        if (stepMediaId.HasValue)
        {
            var sm = await _db.TestWorkflowStepMedias
                .AsNoTracking()
                .Where(x => x.Id == stepMediaId.Value)
                .Select(x => new
                {
                    x.Id,
                    x.MediaProductId,
                    MaterialProductId = x.Material != null ? x.Material.MediaProductId : null
                })
                .FirstOrDefaultAsync(ct);

            if (sm is not null)
                explicitStepMedia = (sm.Id, sm.MediaProductId, sm.MaterialProductId);
        }

        var resolvedStepMediaId = explicitStepMedia?.Id
            ?? (mediaProductId.HasValue
                ? stepMediaCandidates.FirstOrDefault(c => (c.MediaProductId ?? c.MaterialProductId) == mediaProductId.Value).Id is var matchedId && matchedId != 0
                    ? matchedId
                    : stepMediaCandidates.FirstOrDefault().Id is var fallbackId && fallbackId != 0 ? fallbackId : (int?)null
                : stepMediaCandidates.FirstOrDefault().Id is var firstId && firstId != 0 ? firstId : (int?)null);

        var requiredProductIds = new HashSet<int>();
        if (mediaProductId.HasValue)
        {
            requiredProductIds.Add(mediaProductId.Value);
        }
        else if (explicitStepMedia is { } esm && (esm.MediaProductId ?? esm.MaterialProductId) is { } esmPid)
        {
            requiredProductIds.Add(esmPid);
        }
        else
        {
            foreach (var candidate in stepMediaCandidates)
            {
                if ((candidate.MediaProductId ?? candidate.MaterialProductId) is { } pid)
                    requiredProductIds.Add(pid);
            }
        }

        // 1. Media lot default
        if (requiredProductIds.Count > 0)
        {
            var lastLotUsage = await _db.Incubations
                .AsNoTracking()
                .Where(i => i.StartedByUserId == userId && i.MediaId != null)
                .OrderByDescending(i => i.StartedAt)
                .ThenByDescending(i => i.Id)
                .Select(i => new
                {
                    MediaId = i.MediaId!.Value,
                    i.StartedAt
                })
                .FirstOrDefaultAsync(ct);

            if (lastLotUsage is not null)
            {
                var lotDefault = await TryBuildValidMediaLotDefaultAsync(
                    userId,
                    lastLotUsage.MediaId,
                    lastLotUsage.StartedAt,
                    targetSectionId,
                    requiredProductIds,
                    explicitStepMedia,
                    nowUtc,
                    ct);
                if (lotDefault is not null)
                    defaults.Add(lotDefault);
            }
        }

        // 2. Incubator default
        var lastIncubatorUsage = await _db.Incubations
            .AsNoTracking()
            .Where(i => i.StartedByUserId == userId && i.IncubatorEquipmentId != null)
            .OrderByDescending(i => i.StartedAt)
            .ThenByDescending(i => i.Id)
            .Select(i => new
            {
                EquipmentId = i.IncubatorEquipmentId!.Value,
                i.StartedAt
            })
            .FirstOrDefaultAsync(ct);

        if (lastIncubatorUsage is not null)
        {
            var incDefault = await TryBuildValidEquipmentDefaultAsync(
                userId,
                lastIncubatorUsage.EquipmentId,
                EquipmentType.Incubator,
                "incubatorEquipmentId",
                lastIncubatorUsage.StartedAt,
                resolvedStepMediaId,
                nowUtc,
                ct);
            if (incDefault is not null)
                defaults.Add(incDefault);
        }

        return new EntryDefaultsResponse(defaults);
    }

    private async Task<EntryDefault?> TryBuildValidMediaLotDefaultAsync(
        int userId,
        int mediaId,
        DateTime lastUsedAtUtc,
        int? targetSectionId,
        HashSet<int> requiredProductIds,
        (int Id, int? MediaProductId, int? MaterialProductId)? explicitStepMedia,
        DateTime nowUtc,
        CancellationToken ct)
    {
        try
        {
            await _scope.EnsureMediaAccessAsync(userId, mediaId, ct, SectionAccess.Write);
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        var lot = await _db.Media
            .AsNoTracking()
            .Where(m => m.Id == mediaId)
            .Select(m => new
            {
                m.Id,
                m.LotNumber,
                m.IsReleasedForUse,
                m.Status,
                m.ExpiryDate,
                MaterialSectionId = m.Material != null ? (int?)m.Material.SectionId : null,
                MediaProductId = m.Material != null ? m.Material.MediaProductId : null,
                ProductName = m.Material != null && m.Material.MediaProduct != null
                    ? m.Material.MediaProduct.Name
                    : (m.Material != null ? m.Material.MaterialName : null)
            })
            .FirstOrDefaultAsync(ct);

        if (lot is null)
            return null;

        var mediaForRule = new Media
        {
            Id = lot.Id,
            LotNumber = lot.LotNumber,
            IsReleasedForUse = lot.IsReleasedForUse,
            Status = lot.Status,
            ExpiryDate = lot.ExpiryDate,
            Material = lot.MaterialSectionId.HasValue
                ? new Material { SectionId = lot.MaterialSectionId.Value }
                : null
        };

        try
        {
            BatchSelectMediaRules.EnsureMediaLotUsable(mediaForRule, nowUtc);
            if (targetSectionId.HasValue)
                SectionMediaRule.EnsureLot(mediaForRule, targetSectionId.Value);
        }
        catch (BusinessRuleException)
        {
            return null;
        }

        if (lot.MediaProductId is null || !requiredProductIds.Contains(lot.MediaProductId.Value))
            return null;

        if (explicitStepMedia is { } sm)
        {
            var stepMediaForMatch = new TestWorkflowStepMedia
            {
                Id = sm.Id,
                MediaProductId = sm.MediaProductId,
                Material = sm.MaterialProductId.HasValue
                    ? new Material { MediaProductId = sm.MaterialProductId.Value }
                    : null
            };
            if (!StepMediumMatcher.Matches(stepMediaForMatch, lot.MediaProductId))
                return null;
        }

        var label = string.IsNullOrWhiteSpace(lot.ProductName)
            ? lot.LotNumber
            : $"{lot.ProductName} — Lot {lot.LotNumber}";

        return new EntryDefault("mediaLotId", lot.Id, null, label, lastUsedAtUtc);
    }

    private async Task<EntryDefaultsResponse> GetMediaPreparationDefaultsAsync(
        int userId,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var lastMedia = await _db.Media
            .AsNoTracking()
            .Where(m => m.PreparedByUserId == userId && m.AutoclaveEquipmentId != null)
            .OrderByDescending(m => m.PreparedAt)
            .ThenByDescending(m => m.Id)
            .Select(m => new
            {
                EquipmentId = m.AutoclaveEquipmentId!.Value,
                m.PreparedAt
            })
            .FirstOrDefaultAsync(ct);

        if (lastMedia is null)
            return new EntryDefaultsResponse(Array.Empty<EntryDefault>());

        var autoclaveDefault = await TryBuildValidEquipmentDefaultAsync(
            userId,
            lastMedia.EquipmentId,
            EquipmentType.Autoclave,
            "autoclaveEquipmentId",
            lastMedia.PreparedAt,
            stepMediaIdForIncubator: null,
            nowUtc,
            ct);

        return autoclaveDefault is null
            ? new EntryDefaultsResponse(Array.Empty<EntryDefault>())
            : new EntryDefaultsResponse(new[] { autoclaveDefault });
    }

    private async Task<EntryDefaultsResponse> GetSolutionPreparationDefaultsAsync(
        int userId,
        int? solutionMasterId,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var baseQuery = _db.SolutionPreparations
            .AsNoTracking()
            .Where(p => p.StartedByUserId == userId && p.AutoclaveEquipmentId != null);

        var lastPrep = solutionMasterId.HasValue
            ? await baseQuery
                .Where(p => p.SolutionMasterId == solutionMasterId.Value)
                .OrderByDescending(p => p.StartedAt)
                .ThenByDescending(p => p.Id)
                .Select(p => new { EquipmentId = p.AutoclaveEquipmentId!.Value, p.StartedAt })
                .FirstOrDefaultAsync(ct)
              ?? await baseQuery
                .OrderByDescending(p => p.StartedAt)
                .ThenByDescending(p => p.Id)
                .Select(p => new { EquipmentId = p.AutoclaveEquipmentId!.Value, p.StartedAt })
                .FirstOrDefaultAsync(ct)
            : await baseQuery
                .OrderByDescending(p => p.StartedAt)
                .ThenByDescending(p => p.Id)
                .Select(p => new { EquipmentId = p.AutoclaveEquipmentId!.Value, p.StartedAt })
                .FirstOrDefaultAsync(ct);

        if (lastPrep is null)
            return new EntryDefaultsResponse(Array.Empty<EntryDefault>());

        var autoclaveDefault = await TryBuildValidEquipmentDefaultAsync(
            userId,
            lastPrep.EquipmentId,
            EquipmentType.Autoclave,
            "autoclaveEquipmentId",
            lastPrep.StartedAt,
            stepMediaIdForIncubator: null,
            nowUtc,
            ct);

        return autoclaveDefault is null
            ? new EntryDefaultsResponse(Array.Empty<EntryDefault>())
            : new EntryDefaultsResponse(new[] { autoclaveDefault });
    }

    private async Task<EntryDefault?> TryBuildValidEquipmentDefaultAsync(
        int userId,
        int equipmentId,
        EquipmentType expectedType,
        string fieldName,
        DateTime lastUsedAtUtc,
        int? stepMediaIdForIncubator,
        DateTime nowUtc,
        CancellationToken ct)
    {
        try
        {
            await _scope.EnsureEquipmentAccessAsync(userId, equipmentId, ct, SectionAccess.Write);
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        var eq = await _db.Equipment
            .AsNoTracking()
            .Where(e => e.Id == equipmentId && e.Type == expectedType)
            .Select(e => new
            {
                e.Id,
                e.Code,
                e.Name,
                e.CalibrationDueDate,
                e.EquipmentInventoryId
            })
            .FirstOrDefaultAsync(ct);

        if (eq is null)
            return null;

        // Calibration overdue today (Review Focus 4): never offered, whichever record carries the due date.
        if (new EquipmentInventory { CalibrationDueDate = eq.CalibrationDueDate }.IsCalibrationOverdueAt(nowUtc))
            return null;

        var codeUpper = eq.Code.ToUpper();
        var inv = eq.EquipmentInventoryId.HasValue
            ? await _db.EquipmentInventories
                .AsNoTracking()
                .Where(i => i.Id == eq.EquipmentInventoryId.Value)
                .Select(i => new { i.Status, i.CalibrationDueDate })
                .FirstOrDefaultAsync(ct)
            : await _db.EquipmentInventories
                .AsNoTracking()
                .Where(i => i.Code.ToUpper() == codeUpper)
                .Select(i => new { i.Status, i.CalibrationDueDate })
                .FirstOrDefaultAsync(ct);

        if (inv is not null)
        {
            if (!EquipmentInventoryService.IsInService(inv.Status))
                return null;

            if (new EquipmentInventory { CalibrationDueDate = inv.CalibrationDueDate }.IsCalibrationOverdueAt(nowUtc))
                return null;
        }

        if (expectedType == EquipmentType.Incubator && stepMediaIdForIncubator.HasValue)
        {
            try
            {
                await TestWorkflowSupport.RequireEligibleIncubatorAsync(
                    _incubatorEligibility,
                    stepMediaIdForIncubator.Value,
                    equipmentId,
                    ct);
            }
            catch (Exception ex) when (ex is WorkflowStepException or BusinessRuleException or NotFoundException)
            {
                return null;
            }
        }

        return new EntryDefault(fieldName, eq.Id, null, $"{eq.Code}: {eq.Name}", lastUsedAtUtc);
    }

    private async Task<EntryDefaultsResponse> GetReceivingDefaultsAsync(
        int userId,
        string? category,
        CancellationToken ct)
    {
        if (!TryParseSampleCategory(category, out var sampleCategory))
            return new EntryDefaultsResponse(Array.Empty<EntryDefault>());

        var lastSample = await _db.Samples
            .AsNoTracking()
            .Where(s => s.ReceivedByUserId == userId && s.Category == sampleCategory)
            .OrderByDescending(s => s.ReceivedAt)
            .ThenByDescending(s => s.Id)
            .Select(s => new
            {
                s.SampledBy,
                s.CauseOfTestingId,
                CauseName = s.CauseOfTesting != null ? s.CauseOfTesting.Name : null,
                CauseActive = s.CauseOfTesting != null && s.CauseOfTesting.IsActive,
                s.ReceivedAt
            })
            .FirstOrDefaultAsync(ct);

        if (lastSample is null)
            return new EntryDefaultsResponse(Array.Empty<EntryDefault>());

        var defaults = new List<EntryDefault>(2);
        if (!string.IsNullOrWhiteSpace(lastSample.SampledBy))
        {
            defaults.Add(new EntryDefault(
                "sampledBy",
                null,
                lastSample.SampledBy,
                lastSample.SampledBy,
                lastSample.ReceivedAt));
        }

        if (lastSample.CauseActive && !string.IsNullOrWhiteSpace(lastSample.CauseName))
        {
            defaults.Add(new EntryDefault(
                "causeOfTestingId",
                lastSample.CauseOfTestingId,
                null,
                lastSample.CauseName,
                lastSample.ReceivedAt));
        }

        return new EntryDefaultsResponse(defaults);
    }

    private static bool TryParseSampleCategory(string? raw, out SampleCategory category)
    {
        category = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        switch (raw.Trim().ToLowerInvariant())
        {
            case "product":
            case "finishedproduct":
                category = SampleCategory.FinishedProduct;
                return true;
            case "rm":
            case "rawmaterial":
                category = SampleCategory.RawMaterial;
                return true;
            case "pm":
            case "packagingmaterial":
                category = SampleCategory.PackagingMaterial;
                return true;
            case "water":
                category = SampleCategory.Water;
                return true;
            case "em":
            case "environmentalmonitoring":
                category = SampleCategory.EnvironmentalMonitoring;
                return true;
            case "ac":
            case "aftercleaning":
                category = SampleCategory.AfterCleaning;
                return true;
            default:
                return Enum.TryParse(raw.Trim(), ignoreCase: true, out category);
        }
    }
}
