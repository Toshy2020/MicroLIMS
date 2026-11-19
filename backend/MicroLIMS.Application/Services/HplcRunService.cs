using System.Globalization;
using System.Text.Json;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Abstractions.Storage;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.Responses;

namespace MicroLIMS.Application.Services;

// ---- Instruments / methods ----

public record HplcActiveRunSummaryDto(int RunId, string Code, string MethodAbbreviation, string AnalystName, int SampleCount, HplcSstStatus SstStatus);
public record HplcInstrumentDto(int EquipmentId, string Code, string Name, string State, string? Reason, HplcActiveRunSummaryDto? ActiveRun);
public static class HplcTechniqueEquipment
{
    public static EquipmentType For(HplcTechnique t) => t == HplcTechnique.Gc ? EquipmentType.Gc : EquipmentType.Hplc;
    public static string Label(HplcTechnique t) => t == HplcTechnique.Gc ? "GC" : "HPLC";
}

public record HplcMethodOptionDto(int Id, string Abbreviation, string Name, string ColumnDesignation, int EligibleTestOrderCount,
    HplcTechnique Technique = HplcTechnique.Hplc, HplcResultMode ResultMode = HplcResultMode.Assay);

// ---- Start run ----

public record HplcMobilePhaseAssignmentInput(string Channel, int SolutionPreparationId);
public record StartHplcRunRequest(int EquipmentId, int HplcMethodId, int ChromatographyColumnId, List<HplcMobilePhaseAssignmentInput> MobilePhases);

// ---- SST ----

public record SaveSstAnalyteInput(
    int HplcSstAnalyteId, int StandardMaterialId, decimal StandardWeightMg, List<decimal> Responses,
    decimal? ReportedRsdPercent, decimal? Resolution, decimal? TailingFactor, decimal? TheoreticalPlates,
    decimal? RetentionFactor, decimal? SignalToNoise, decimal? PeakToValley,
    List<decimal>? InternalStandardResponses = null);
public record SaveSstRequest(List<SaveSstAnalyteInput> Analytes);
public record ConfirmSstRequest(string Password, string? Comment);

// ---- Evidence ----

public record HplcEvidenceDto(
    int Id, int HplcRunId, int? HplcRunSampleId, HplcEvidenceContext Context, HplcEvidenceKind Kind,
    string FileName, string ContentType, int UploadedByUserId, string? UploadedByUserName, DateTime UploadedAt,
    bool IsCurrent, string? SupersedeReason);

// ---- Run detail ----

public record HplcRunMobilePhaseDto(int Id, string Channel, int SolutionPreparationId, string? SolutionPreparationCode, string SolutionMasterName, DateTime? ExpiresAt);
public record HplcSstInjectionDto(int InjectionNo, decimal Response, decimal? InternalStandardResponse = null);
public record HplcSstAnalyteDto(
    int Id, int HplcMethodAnalyteId, string AnalyteName, int StandardEntryId,
    int? StandardMaterialId, string? StandardMaterialBatch, decimal? StandardPurityPercent, decimal? StandardMoisturePercent, decimal? StandardWeightMg,
    List<HplcSstInjectionDto> Injections,
    decimal? ReportedRsdPercent, decimal? Resolution, decimal? TailingFactor, decimal? TheoreticalPlates,
    decimal? RetentionFactor, decimal? SignalToNoise, decimal? PeakToValley,
    decimal? MeanResponse, decimal? ComputedRsdPercent, bool Passed, string? FailureReasons);
public record HplcSstRecordDto(
    int Id, string Code, HplcSstStatus Status, string? FailureReasons,
    int? ConfirmedByUserId, string? ConfirmedByUserName, DateTime? ConfirmedAt,
    List<HplcSstAnalyteDto> Analytes);
public record HplcRunSampleSummaryDto(
    int Id, int? TestOrderId, HplcRunSampleStatus Status,
    string SampleNumber, string? BatchNumber, string? ProductName, string TestCode, bool Submitted, bool IsDissolution,
    int? WorkingStandardQualificationId = null, string? SubjectName = null);

public record HplcRunDto(
    int Id, uint Version, string Code, int SectionId,
    int EquipmentId, string EquipmentCode, string EquipmentName,
    int ChromatographyColumnId, string ChromatographyColumnCode,
    int HplcMethodId, string HplcMethodAbbreviation,
    int AnalystUserId, string? AnalystUserName, DateTime StartedAt, HplcRunStatus Status, DateTime? ClosedAt, string? CloseReason,
    List<HplcRunMobilePhaseDto> MobilePhases, HplcSstRecordDto? Sst, List<HplcRunSampleSummaryDto> Samples, List<HplcEvidenceDto> Evidence,
    bool CanConfirmSst, string? CanConfirmSstReason, bool CanAssignSamples, string? CanAssignSamplesReason,
    // From the run's method snapshot, not the live method.
    HplcResultMode ResultMode = HplcResultMode.Assay, string? InternalStandardName = null);

public record HplcRunListItem(int Id, string Code, string MethodAbbreviation, string AnalystUserName, DateTime StartedAt, HplcRunStatus Status, HplcSstStatus SstStatus, DateTime? ClosedAt);

// HPLC Workspace (HPLC chain S6, spec 5) - Part A: instruments, runs with a
// method snapshot (D7), prepared mobile phases, one System Suitability
// record per run (analyte rows + injections), and evidence. Sample
// assignment, replicate entry and submission are Part B, built on top of
// this service (HplcRunSample/HplcSampleReplicate rows already exist in the
// model; their write paths live in HplcRunService.Samples.cs).
//
// Sign-first pattern throughout (ConfirmSstAsync): ElectronicSignatureService
// .SignAsync SAVES its own audit row immediately on a failed password, so
// every mutation happens only after signing succeeds - same ordering as
// SolutionPreparationService.CompleteAsync and TitrantStandardizationService
// .StandardizeAsync.
public partial class HplcRunService
{
    private const string InstrumentStateRunning = "Running";
    private const string InstrumentStateUnavailable = "Unavailable";
    private const string InstrumentStateAvailable = "Available";

    // Matches the MaterialDocuments:MaxFileSizeBytes default (25 MB) - the
    // limit other upload endpoints use (ServiceCollectionExtensions.cs).
    private const long MaxEvidenceBytes = 26_214_400;
    private static readonly HashSet<string> AllowedEvidenceContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "image/png", "image/jpeg"
    };

    private static readonly JsonSerializerOptions JsonOptions = SnapshotJson.Options;

    private const string MissingStandardReportReason = "Upload the standard report before confirming system suitability.";

    // A blank SST entry would be signed as a permanent failure, so confirmation
    // waits until every required value is in. Returns "Analyte: field, field" lines.
    // Only an assay weighs its standard and corrects it for purity and moisture; residual-solvent and
    // related-substances methods need just the lot (their calculations use no standard weight).
    private static bool StandardIsWeighed(HplcResultMode mode) => mode switch
    {
        HplcResultMode.Assay => true,
        HplcResultMode.ResidualSolvents or HplcResultMode.RelatedSubstances => false,
        _ => throw new BusinessRuleException(
            MessageCodes.HplcUnsupportedResultMode,
            $"Unsupported result mode {mode}.",
            new Dictionary<string, string> { ["mode"] = mode.ToString() }),
    };

    private static List<string> MissingSstEntries(HplcRun run)
    {
        var missing = new List<string>();
        if (run.Sst == null) return missing;
        var snapshot = JsonSerializer.Deserialize<HplcMethodResponse>(run.MethodSnapshotJson, JsonOptions);
        var methodById = snapshot?.Analytes.ToDictionary(a => a.Id) ?? new Dictionary<int, HplcMethodAnalyteResponse>();
        var weighsStandard = StandardIsWeighed(snapshot?.ResultMode ?? HplcResultMode.Assay);
        var hasIs = !string.IsNullOrWhiteSpace(snapshot?.InternalStandardName);

        foreach (var a in run.Sst.Analytes.OrderBy(x => x.Id))
        {
            var fields = new List<string>();
            if (a.StandardMaterialId == null) fields.Add("reference standard lot");
            if (weighsStandard && !(a.StandardWeightMg > 0)) fields.Add("actual standard weight");
            if (methodById.TryGetValue(a.HplcMethodAnalyteId, out var m))
            {
                var blank = Enumerable.Range(1, m.StandardInjections)
                    .Where(n => !(a.Injections.FirstOrDefault(i => i.InjectionNo == n)?.Response > 0)).ToList();
                if (blank.Count > 0) fields.Add($"injection {string.Join(", ", blank.Select(n => $"#{n}"))}");
                if (hasIs)
                {
                    var blankIs = Enumerable.Range(1, m.StandardInjections)
                        .Where(n => !(a.Injections.FirstOrDefault(i => i.InjectionNo == n)?.InternalStandardResponse > 0)).ToList();
                    if (blankIs.Count > 0) fields.Add($"internal standard injection {string.Join(", ", blankIs.Select(n => $"#{n}"))}");
                }
                if (m.SstMinResolution.HasValue && a.Resolution == null) fields.Add("resolution");
                if (m.SstMaxTailingFactor.HasValue && a.TailingFactor == null) fields.Add("tailing factor");
                if (m.SstMinTheoreticalPlates.HasValue && a.TheoreticalPlates == null) fields.Add("theoretical plates");
                if (m.SstMinRetentionFactor.HasValue && a.RetentionFactor == null) fields.Add("retention factor");
                if (m.SstMinSignalToNoise.HasValue && a.SignalToNoise == null) fields.Add("signal-to-noise");
                if (m.SstMinPeakToValley.HasValue && a.PeakToValley == null) fields.Add("peak-to-valley");
            }
            if (fields.Count > 0) missing.Add($"{a.AnalyteName}: {string.Join(", ", fields)}");
        }
        return missing;
    }

    private static string MissingSstReason(List<string> missing) =>
        $"Complete these entries and save before confirming. {string.Join("; ", missing)}.";

    private static bool IsCurrentSstStandardReport(HplcEvidence e) =>
        e.Context == HplcEvidenceContext.Sst && e.Kind == HplcEvidenceKind.StandardReport && e.SupersededByEvidenceId == null;

    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly IElectronicSignatureService _signatures;
    private readonly IFileStorageService _storage;
    private readonly ILabClock _clock;

    public HplcRunService(
        IMicroLimsDbContext db,
        IUserSectionScopeService scope,
        IElectronicSignatureService signatures,
        IFileStorageService storage,
        ILabClock? clock = null)
    {
        _db = db;
        _scope = scope;
        _signatures = signatures;
        _storage = storage;
        _clock = clock ?? LabClock.Default;
    }

    // ---- Instruments / methods ----

    public async Task<List<HplcInstrumentDto>> GetInstrumentsAsync(int userId, HplcTechnique technique = HplcTechnique.Hplc, CancellationToken ct = default)
    {
        var scope = await _scope.GetReadableSectionIdsAsync(userId, ct);
        var query = _db.Equipment.Where(e => e.Type == HplcTechniqueEquipment.For(technique)).AsNoTracking();
        if (scope != null) query = query.Where(e => scope.Contains(e.SectionId));
        var equipmentList = await query.OrderBy(e => e.Code).ToListAsync(ct);

        var equipmentIds = equipmentList.Select(e => e.Id).ToList();
        var openRuns = await _db.HplcRuns.AsNoTracking()
            .Include(r => r.HplcMethod)
            .Include(r => r.Sst)
            .Include(r => r.Samples)
            .Where(r => equipmentIds.Contains(r.EquipmentId) && r.Status == HplcRunStatus.Open)
            .ToListAsync(ct);
        var runByEquipment = openRuns.ToDictionary(r => r.EquipmentId);

        var analystIds = openRuns.Select(r => r.AnalystUserId).Distinct().ToList();
        var analystNames = await _db.Users.Where(u => analystIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var result = new List<HplcInstrumentDto>();
        foreach (var eq in equipmentList)
        {
            if (runByEquipment.TryGetValue(eq.Id, out var run))
            {
                var summary = new HplcActiveRunSummaryDto(
                    run.Id, run.Code, run.HplcMethod?.Abbreviation ?? string.Empty,
                    analystNames.TryGetValue(run.AnalystUserId, out var analystName) ? analystName : "Unknown",
                    run.Samples.Count(s => s.Status == HplcRunSampleStatus.Assigned),
                    run.Sst?.Status ?? HplcSstStatus.Pending);
                result.Add(new HplcInstrumentDto(eq.Id, eq.Code, eq.Name, InstrumentStateRunning, null, summary));
                continue;
            }

            var unavailableReason = await DetermineUnavailableReasonAsync(eq, ct);
            if (unavailableReason != null)
            {
                result.Add(new HplcInstrumentDto(eq.Id, eq.Code, eq.Name, InstrumentStateUnavailable, unavailableReason, null));
                continue;
            }

            result.Add(new HplcInstrumentDto(eq.Id, eq.Code, eq.Name, InstrumentStateAvailable, null, null));
        }

        return result;
    }

    public async Task<List<HplcMethodOptionDto>> GetMethodOptionsAsync(int userId, HplcTechnique technique = HplcTechnique.Hplc, CancellationToken ct = default)
    {
        var scope = await _scope.GetReadableSectionIdsAsync(userId, ct);
        var query = _db.HplcMethods.Where(m => m.IsActive && m.Technique == technique).AsNoTracking();
        if (scope != null) query = query.Where(m => scope.Contains(m.SectionId));
        var methods = await query.OrderBy(m => m.Name).ToListAsync(ct);

        var methodIds = methods.Select(m => m.Id).ToList();
        var testDefs = await _db.TestDefinitions.AsNoTracking()
            .Where(t => t.WorkflowType == WorkflowType.HplcMethodAssay && t.HplcMethodId != null && methodIds.Contains(t.HplcMethodId.Value))
            .Select(t => new { t.Code, HplcMethodId = t.HplcMethodId!.Value })
            .ToListAsync(ct);
        var codesByMethod = testDefs.GroupBy(t => t.HplcMethodId).ToDictionary(g => g.Key, g => g.Select(x => x.Code).ToList());

        var result = new List<HplcMethodOptionDto>();
        foreach (var m in methods)
        {
            var count = 0;
            if (codesByMethod.TryGetValue(m.Id, out var codes) && codes.Count > 0)
            {
                count = await _db.TestOrders.AsNoTracking().CountAsync(o =>
                    codes.Contains(o.TestCode) && !o.IsSuperseded && o.CurrentStep != WorkflowStep.Ready
                    && o.Sample != null && o.Sample.Status != SampleStatus.Voided && o.Sample.Status != SampleStatus.Cancelled, ct);
            }
            result.Add(new HplcMethodOptionDto(m.Id, m.Abbreviation, m.Name, m.ColumnDesignation, count, m.Technique, m.ResultMode));
        }

        return result;
    }

    private async Task<string?> DetermineUnavailableReasonAsync(Equipment equipment, CancellationToken ct) =>
        (await DetermineUnavailableAsync(equipment, ct))?.Reason;

    private async Task<(string Code, string Reason, Dictionary<string, string>? Params)?> DetermineUnavailableAsync(
        Equipment equipment, CancellationToken ct)
    {
        var today = _clock.LabToday;
        if (equipment.CalibrationDueDate.HasValue && DateOnly.FromDateTime(equipment.CalibrationDueDate.Value) < today)
        {
            var date = equipment.CalibrationDueDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return (MessageCodes.HplcCalibrationOverdue,
                $"Calibration overdue since {equipment.CalibrationDueDate:yyyy-MM-dd}.",
                new Dictionary<string, string> { ["date"] = date });
        }

        var codeUpper = equipment.Code.ToUpperInvariant();
        var inventory = await _db.EquipmentInventories.AsNoTracking()
            .Where(i => i.Code.ToUpper() == codeUpper)
            .FirstOrDefaultAsync(ct);

        if (inventory != null && (inventory.Status == EquipmentOperationalStatus.OutOfService || inventory.Status == EquipmentOperationalStatus.Retired))
        {
            return (MessageCodes.HplcEquipmentUnavailable,
                $"Equipment is {inventory.Status}.",
                new Dictionary<string, string> { ["status"] = inventory.Status.ToString() });
        }

        return null;
    }

    // ---- Start run ----

    public async Task<HplcRunDto> StartRunAsync(StartHplcRunRequest r, int userId, CancellationToken ct = default)
    {
        var equipment = await _db.Equipment.FirstOrDefaultAsync(e => e.Id == r.EquipmentId, ct)
            ?? throw new NotFoundException(
                MessageCodes.HplcEquipmentNotFound,
                $"Equipment {r.EquipmentId} not found.",
                new Dictionary<string, string> { ["equipmentId"] = r.EquipmentId.ToString(CultureInfo.InvariantCulture) });

        var scope = await _scope.GetMemberSectionIdsAsync(userId, ct);
        if (scope != null && !scope.Contains(equipment.SectionId))
            throw new NotFoundException(
                MessageCodes.HplcEquipmentNotFound,
                $"Equipment {r.EquipmentId} not found.",
                new Dictionary<string, string> { ["equipmentId"] = r.EquipmentId.ToString(CultureInfo.InvariantCulture) });

        var hasOpenRun = await _db.HplcRuns.AnyAsync(run => run.EquipmentId == equipment.Id && run.Status == HplcRunStatus.Open, ct);
        if (hasOpenRun)
            throw new BusinessRuleException(
                MessageCodes.HplcEquipmentHasOpenRun,
                $"\"{equipment.Name}\" already has an open run.",
                new Dictionary<string, string> { ["equipment"] = equipment.Name });

        var unavailable = await DetermineUnavailableAsync(equipment, ct);
        if (unavailable != null)
        {
            var (code, reason, parameters) = unavailable.Value;
            throw new BusinessRuleException(code, reason, parameters);
        }

        var method = await _db.HplcMethods
            .Include(m => m.Section)
            .Include(m => m.DiluentSolution)
            .Include(m => m.MobilePhases).ThenInclude(mp => mp.SolutionMaster)
            .Include(m => m.GradientSteps)
            .Include(m => m.OvenSteps)
            .Include(m => m.Analytes).ThenInclude(a => a.StandardEntry)
            .Include(m => m.Impurities)
            .FirstOrDefaultAsync(m => m.Id == r.HplcMethodId, ct)
            ?? throw new NotFoundException(
                MessageCodes.HplcMethodNotFound,
                $"HPLC method {r.HplcMethodId} not found.",
                new Dictionary<string, string> { ["methodId"] = r.HplcMethodId.ToString(CultureInfo.InvariantCulture) });

        if (equipment.Type != HplcTechniqueEquipment.For(method.Technique))
            throw new BusinessRuleException(
                MessageCodes.HplcEquipmentWrongTechnique,
                $"\"{equipment.Name}\" is not a {HplcTechniqueEquipment.Label(method.Technique)} instrument; method {method.Abbreviation} needs one.",
                new Dictionary<string, string>
                {
                    ["equipment"] = equipment.Name,
                    ["technique"] = HplcTechniqueEquipment.Label(method.Technique),
                    ["method"] = method.Abbreviation
                });

        if (!method.IsActive)
            throw new BusinessRuleException(
                MessageCodes.HplcMethodInactive,
                $"HPLC method \"{method.Name}\" is inactive.",
                new Dictionary<string, string> { ["method"] = method.Name });

        if (method.SectionId != equipment.SectionId)
            throw new BusinessRuleException(
                MessageCodes.HplcMethodWrongLaboratory,
                "The HPLC method belongs to another laboratory.");

        var column = await _db.ChromatographyColumns.Include(c => c.CompatibleEquipment)
            .FirstOrDefaultAsync(c => c.Id == r.ChromatographyColumnId, ct)
            ?? throw new NotFoundException(
                MessageCodes.HplcColumnNotFound,
                $"Column {r.ChromatographyColumnId} not found.",
                new Dictionary<string, string> { ["columnId"] = r.ChromatographyColumnId.ToString(CultureInfo.InvariantCulture) });

        if (!column.IsActive)
            throw new BusinessRuleException(
                MessageCodes.HplcColumnInactive,
                $"Column \"{column.Name}\" is inactive.",
                new Dictionary<string, string> { ["column"] = column.Name });

        if (!column.CompatibleEquipment.Any(e => e.Id == equipment.Id))
            throw new BusinessRuleException(
                MessageCodes.HplcColumnIncompatibleWithEquipment,
                $"Column \"{column.Name}\" is not compatible with \"{equipment.Name}\".",
                new Dictionary<string, string> { ["column"] = column.Name, ["equipment"] = equipment.Name });

        if (string.IsNullOrWhiteSpace(column.UspDesignation))
            throw new BusinessRuleException(
                MessageCodes.HplcColumnMissingUspDesignation,
                $"Column {column.Code} has no USP designation; set it in the column master.",
                new Dictionary<string, string> { ["column"] = column.Code });
        if (!string.Equals(column.UspDesignation.Trim(), method.ColumnDesignation?.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException(
                MessageCodes.HplcColumnWrongUspDesignation,
                $"Column {column.Code} is USP {column.UspDesignation}; method {method.Abbreviation} requires {method.ColumnDesignation}.",
                new Dictionary<string, string>
                {
                    ["column"] = column.Code,
                    ["actual"] = column.UspDesignation,
                    ["method"] = method.Abbreviation,
                    ["required"] = method.ColumnDesignation ?? string.Empty
                });

        var inputs = r.MobilePhases ?? new List<HplcMobilePhaseAssignmentInput>();
        if (inputs.Count != method.MobilePhases.Count)
            throw new BusinessRuleException(
                MessageCodes.HplcMobilePhaseRequiredForAllChannels,
                "Select a prepared mobile phase for every channel this method uses.");

        var nowUtc = _clock.UtcNow.UtcDateTime;
        var mobilePhaseEntities = new List<HplcRunMobilePhase>();
        foreach (var channelDef in method.MobilePhases)
        {
            var input = inputs.FirstOrDefault(mp => string.Equals(mp.Channel, channelDef.Channel, StringComparison.OrdinalIgnoreCase));
            if (input == null)
                throw new BusinessRuleException(
                    MessageCodes.HplcMobilePhaseRequiredForChannel,
                    $"Select a prepared mobile phase for channel {channelDef.Channel}.",
                    new Dictionary<string, string> { ["channel"] = channelDef.Channel });

            var prep = await _db.SolutionPreparations.FirstOrDefaultAsync(p => p.Id == input.SolutionPreparationId, ct)
                ?? throw new NotFoundException(
                    MessageCodes.HplcSolutionPreparationNotFound,
                    $"Solution preparation {input.SolutionPreparationId} not found.",
                    new Dictionary<string, string> { ["preparationId"] = input.SolutionPreparationId.ToString(CultureInfo.InvariantCulture) });

            if (prep.Type != SolutionType.MobilePhase)
                throw new BusinessRuleException(
                    MessageCodes.HplcChannelPreparationNotMobilePhase,
                    $"Channel {channelDef.Channel}: the selected preparation is not a mobile phase.",
                    new Dictionary<string, string> { ["channel"] = channelDef.Channel });
            if (prep.SolutionMasterId != channelDef.SolutionMasterId)
                throw new BusinessRuleException(
                    MessageCodes.HplcChannelPreparationWrongSolution,
                    $"Channel {channelDef.Channel}: the selected preparation is not of this channel's solution.",
                    new Dictionary<string, string> { ["channel"] = channelDef.Channel });
            if (prep.HplcMethodId != method.Id)
                throw new BusinessRuleException(
                    MessageCodes.HplcChannelPreparationWrongMethod,
                    $"Channel {channelDef.Channel}: the selected preparation was not prepared for this method.",
                    new Dictionary<string, string> { ["channel"] = channelDef.Channel });
            if (SolutionPreparationResponse.EffectiveStatusOf(prep, nowUtc) != SolutionPreparationStatus.Prepared)
                throw new BusinessRuleException(
                    MessageCodes.HplcChannelPreparationNotPrepared,
                    $"Channel {channelDef.Channel}: the selected preparation is not currently prepared.",
                    new Dictionary<string, string> { ["channel"] = channelDef.Channel });

            mobilePhaseEntities.Add(new HplcRunMobilePhase { Channel = channelDef.Channel, SolutionPreparationId = prep.Id });
        }

        var snapshotJson = JsonSerializer.Serialize(HplcMethodResponse.From(method), JsonOptions);
        var labLocal = _clock.ToLabLocal(nowUtc);
        var runCode = await SystemSuitabilityRunCode.NextAsync(_db.HplcRuns.Select(x => x.Code), method.Abbreviation, labLocal, "RUN", ct);
        var sstCode = await SystemSuitabilityRunCode.NextAsync(
            _db.HplcSstRecords.Select(x => x.Code),
            method.Abbreviation, labLocal, "S.S", ct);

        var run = new HplcRun
        {
            SectionId = equipment.SectionId,
            Code = runCode,
            EquipmentId = equipment.Id,
            ChromatographyColumnId = column.Id,
            HplcMethodId = method.Id,
            MethodSnapshotJson = snapshotJson,
            AnalystUserId = userId,
            StartedAt = nowUtc,
            Status = HplcRunStatus.Open,
            MobilePhases = mobilePhaseEntities,
            Sst = new HplcSstRecord
            {
                Code = sstCode,
                Status = HplcSstStatus.Pending,
                Analytes = method.Analytes
                    .OrderBy(a => a.DisplayOrder)
                    .Select(a => new HplcSstAnalyte { HplcMethodAnalyteId = a.Id, AnalyteName = a.Name })
                    .ToList(),
            },
        };

        _db.HplcRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        return await GetRunAsync(run.Id, userId, ct);
    }

    // ---- Run detail ----

    public async Task<HplcRunDto> GetRunAsync(int runId, int userId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        await EnsureAccessAsync(run, userId, ct, SectionAccess.Read);
        return await BuildDtoAsync(run, ct);
    }

    // ---- SST ----

    public async Task<HplcRunDto> SaveSstAsync(int runId, SaveSstRequest r, int userId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        await EnsureAccessAsync(run, userId, ct);

        if (run.Status != HplcRunStatus.Open)
            throw new BusinessRuleException(MessageCodes.HplcRunClosed, "The run is closed.");
        if (run.Sst == null || run.Sst.Status != HplcSstStatus.Pending)
            throw new BusinessRuleException(MessageCodes.HplcSstAlreadyConfirmed, "System suitability has already been confirmed.");

        var method = await _db.HplcMethods.Include(m => m.Analytes).FirstAsync(m => m.Id == run.HplcMethodId, ct);
        var methodAnalyteById = method.Analytes.ToDictionary(a => a.Id);
        var today = _clock.LabToday;
        var snapshot = JsonSerializer.Deserialize<HplcMethodResponse>(run.MethodSnapshotJson, JsonOptions);
        var weighsStandard = StandardIsWeighed(snapshot?.ResultMode ?? HplcResultMode.Assay);
        var hasIs = !string.IsNullOrWhiteSpace(snapshot?.InternalStandardName);

        if (!hasIs && (r.Analytes ?? new()).Any(a => a.InternalStandardResponses != null && a.InternalStandardResponses.Count > 0))
            throw new BusinessRuleException(MessageCodes.HplcMethodHasNoInternalStandard, "This method has no internal standard.");

        foreach (var input in r.Analytes ?? new List<SaveSstAnalyteInput>())
        {
            var analyteRow = run.Sst.Analytes.FirstOrDefault(a => a.Id == input.HplcSstAnalyteId)
                ?? throw new BusinessRuleException(
                    MessageCodes.HplcSstAnalyteNotInRecord,
                    $"Analyte {input.HplcSstAnalyteId} does not belong to this system suitability record.",
                    new Dictionary<string, string> { ["analyteId"] = input.HplcSstAnalyteId.ToString(CultureInfo.InvariantCulture) });

            if (!methodAnalyteById.TryGetValue(analyteRow.HplcMethodAnalyteId, out var methodAnalyte))
                throw new BusinessRuleException(
                    MessageCodes.HplcMethodAnalyteNotFound,
                    $"{analyteRow.AnalyteName}: method analyte not found.",
                    new Dictionary<string, string> { ["analyte"] = analyteRow.AnalyteName });

            if (weighsStandard && input.StandardWeightMg <= 0)
                throw new BusinessRuleException(
                    MessageCodes.HplcStandardWeightMustBePositive,
                    $"{analyteRow.AnalyteName}: standard weight must be greater than zero.",
                    new Dictionary<string, string> { ["analyte"] = analyteRow.AnalyteName });

            var lot = await _db.Materials.FirstOrDefaultAsync(m => m.Id == input.StandardMaterialId, ct)
                ?? throw new BusinessRuleException(
                    MessageCodes.HplcStandardMaterialNotFound,
                    $"{analyteRow.AnalyteName}: standard material not found.",
                    new Dictionary<string, string> { ["analyte"] = analyteRow.AnalyteName });

            if (lot.SectionId != run.SectionId)
                throw new BusinessRuleException(
                    MessageCodes.HplcStandardLotWrongLaboratory,
                    $"{analyteRow.AnalyteName}: standard lot belongs to another laboratory.",
                    new Dictionary<string, string> { ["analyte"] = analyteRow.AnalyteName });

            var check = LotUsability.Check(lot, methodAnalyte.StandardEntryId, null, today);
            if (!check.Usable)
                throw new BusinessRuleException(
                    MessageCodes.HplcStandardLotUnusable,
                    $"{analyteRow.AnalyteName}: lot {lot.LotLabel}: {check.Reason}",
                    new Dictionary<string, string>
                    {
                        ["analyte"] = analyteRow.AnalyteName,
                        ["lot"] = lot.LotLabel,
                        ["reason"] = check.Reason ?? string.Empty
                    });

            if (weighsStandard)
            {
                if (!lot.Purity.HasValue)
                    throw new BusinessRuleException(
                        MessageCodes.HplcLotMissingPurity,
                        $"Lot {lot.LotLabel} has no purity recorded.",
                        new Dictionary<string, string> { ["lot"] = lot.LotLabel });
                if (!lot.MoisturePercent.HasValue)
                    throw new BusinessRuleException(
                        MessageCodes.HplcLotMissingMoisture,
                        $"Lot {lot.LotLabel} has no moisture content recorded.",
                        new Dictionary<string, string> { ["lot"] = lot.LotLabel });
            }

            analyteRow.StandardMaterialId = lot.Id;
            analyteRow.StandardWeightMg = weighsStandard && input.StandardWeightMg > 0 ? input.StandardWeightMg : null;
            analyteRow.StandardPurityPercent = lot.Purity;
            analyteRow.StandardMoisturePercent = lot.MoisturePercent;
            analyteRow.ReportedRsdPercent = input.ReportedRsdPercent;
            analyteRow.Resolution = input.Resolution;
            analyteRow.TailingFactor = input.TailingFactor;
            analyteRow.TheoreticalPlates = input.TheoreticalPlates;
            analyteRow.RetentionFactor = input.RetentionFactor;
            analyteRow.SignalToNoise = input.SignalToNoise;
            analyteRow.PeakToValley = input.PeakToValley;

            _db.HplcSstInjections.RemoveRange(analyteRow.Injections);
            analyteRow.Injections.Clear();
            var responses = input.Responses ?? new List<decimal>();
            var isResponses = input.InternalStandardResponses;
            if (hasIs && (responses.Count > 0 || (isResponses != null && isResponses.Count > 0)))
            {
                if (isResponses == null || isResponses.Count != responses.Count || isResponses.Any(x => x <= 0m))
                    throw new BusinessRuleException(
                        MessageCodes.HplcInternalStandardResponseRequired,
                        $"{analyteRow.AnalyteName}: enter an internal standard response (above zero) for every injection.",
                        new Dictionary<string, string> { ["analyte"] = analyteRow.AnalyteName });
            }

            for (var i = 0; i < responses.Count; i++)
            {
                analyteRow.Injections.Add(new HplcSstInjection
                {
                    InjectionNo = i + 1,
                    Response = responses[i],
                    InternalStandardResponse = isResponses != null && i < isResponses.Count ? isResponses[i] : null
                });
            }

            // Shown while entering; Passed/FailureReasons stay untouched until confirmation.
            var effectiveResponses = hasIs && isResponses != null
                ? responses.Zip(isResponses, (resp, isResp) => HplcResponse.ForSst(analyteRow.AnalyteName, resp, isResp, hasIs)).ToList()
                : responses;
            analyteRow.MeanResponse = effectiveResponses.Count > 0 ? effectiveResponses.Average() : null;
            analyteRow.ComputedRsdPercent = StandardComparisonCalculator.CalculatePreparationRsd(effectiveResponses, null).RsdPercent;
        }

        await _db.SaveChangesAsync(ct);
        return await GetRunAsync(runId, userId, ct);
    }

    // Confirming SST (pass or fail - the standard was weighed either way)
    // deducts each analyte's standard weight from its stock lot, primary
    // reference standard or working standard alike. Weights are summed per
    // lot, converted from mg to the lot's unit (g / kg only), validated
    // BEFORE signing, and applied in the same SaveChanges as the SST status.
    public async Task<HplcRunDto> ConfirmSstAsync(int runId, ConfirmSstRequest r, int userId, string? ip, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        await EnsureAccessAsync(run, userId, ct);

        if (run.Status != HplcRunStatus.Open)
            throw new BusinessRuleException(MessageCodes.HplcRunClosed, "The run is closed.");
        if (run.Sst == null || run.Sst.Status != HplcSstStatus.Pending)
            throw new BusinessRuleException(MessageCodes.HplcSstAlreadyConfirmed, "System suitability has already been confirmed.");

        var missingEntries = MissingSstEntries(run);
        if (missingEntries.Count > 0)
            throw new BusinessRuleException(
                MessageCodes.HplcSstEntriesIncomplete,
                MissingSstReason(missingEntries),
                new Dictionary<string, string> { ["entries"] = string.Join("; ", missingEntries) });

        if (!run.Evidence.Any(IsCurrentSstStandardReport))
            throw new BusinessRuleException(MessageCodes.HplcSstStandardReportRequired, MissingStandardReportReason);

        var nowUtc = _clock.UtcNow.UtcDateTime;
        foreach (var mp in run.MobilePhases)
        {
            if (mp.SolutionPreparation == null)
                throw new BusinessRuleException(
                    MessageCodes.HplcChannelPreparationMissing,
                    $"Channel {mp.Channel}: the selected preparation no longer exists.",
                    new Dictionary<string, string> { ["channel"] = mp.Channel });
            if (SolutionPreparationResponse.EffectiveStatusOf(mp.SolutionPreparation, nowUtc) != SolutionPreparationStatus.Prepared)
                throw new BusinessRuleException(
                    MessageCodes.HplcChannelPreparationExpired,
                    $"Channel {mp.Channel}: the mobile phase preparation has expired since it was selected.",
                    new Dictionary<string, string> { ["channel"] = mp.Channel });
        }

        var snapshot = JsonSerializer.Deserialize<HplcMethodResponse>(run.MethodSnapshotJson, JsonOptions)
            ?? throw new BusinessRuleException(MessageCodes.HplcMethodSnapshotUnreadable, "The run's method snapshot could not be read.");
        var snapshotAnalyteById = snapshot.Analytes.ToDictionary(a => a.Id);

        // Pure evaluation only - no mutation yet (sign-first, see class comment).
        var outcomes = new Dictionary<int, SstOutcome>();
        foreach (var analyteRow in run.Sst.Analytes)
        {
            if (!snapshotAnalyteById.TryGetValue(analyteRow.HplcMethodAnalyteId, out var snapAnalyte))
                throw new BusinessRuleException(
                    MessageCodes.HplcMethodAnalyteSnapshotNotFound,
                    $"{analyteRow.AnalyteName}: method analyte snapshot not found.",
                    new Dictionary<string, string> { ["analyte"] = analyteRow.AnalyteName });

            var criteria = new SstCriteria(
                snapAnalyte.SstMaxRsdPercent, snapAnalyte.SstMinResolution, snapAnalyte.SstMaxTailingFactor,
                snapAnalyte.SstMinTheoreticalPlates, snapAnalyte.SstMinRetentionFactor, snapAnalyte.SstMinSignalToNoise,
                snapAnalyte.SstMinPeakToValley, snapAnalyte.StandardInjections);

            var hasIs = !string.IsNullOrWhiteSpace(snapshot.InternalStandardName);
            var effectiveResponses = analyteRow.Injections.OrderBy(i => i.InjectionNo)
                .Select(i => HplcResponse.ForSst(analyteRow.AnalyteName, i.Response, i.InternalStandardResponse, hasIs))
                .ToList();

            var entered = new SstEntered(
                effectiveResponses,
                analyteRow.ReportedRsdPercent, analyteRow.Resolution, analyteRow.TailingFactor,
                analyteRow.TheoreticalPlates, analyteRow.RetentionFactor, analyteRow.SignalToNoise, analyteRow.PeakToValley);

            outcomes[analyteRow.Id] = HplcSstEvaluator.Evaluate(analyteRow.AnalyteName, criteria, entered);
        }

        var overallPassed = outcomes.Values.All(o => o.Passed);
        var combinedFailures = outcomes.Values.SelectMany(o => o.FailureReasons).ToList();

        var lotDeductions = await PlanSstStockDeductionsAsync(run, snapshotAnalyteById, ct);

        // Signs first - a wrong password must leave nothing else persisted
        // (ElectronicSignatureService.SignAsync saves its own failure audit
        // row immediately). Only after this succeeds does anything below
        // mutate tracked state.
        var signature = await _signatures.SignAsync(
            userId, r.Password, SignatureMeaning.SuitabilityRunPerformed, nameof(HplcSstRecord), run.Sst.Id, r.Comment, ip);

        foreach (var analyteRow in run.Sst.Analytes)
        {
            var outcome = outcomes[analyteRow.Id];
            analyteRow.MeanResponse = outcome.MeanResponse;
            analyteRow.ComputedRsdPercent = outcome.ComputedRsdPercent;
            analyteRow.Passed = outcome.Passed;
            analyteRow.FailureReasons = outcome.FailureReasons.Count > 0 ? string.Join(" ", outcome.FailureReasons) : null;
        }

        foreach (var (lot, quantity) in lotDeductions)
        {
            lot.QuantityRemaining -= quantity;
            lot.LastModifiedByUserId = userId;
            lot.LastModifiedAt = nowUtc;
        }

        run.Sst.Status = overallPassed ? HplcSstStatus.Passed : HplcSstStatus.Failed;
        run.Sst.FailureReasons = combinedFailures.Count > 0 ? string.Join(" ", combinedFailures) : null;
        run.Sst.ConfirmedByUserId = userId;
        run.Sst.ConfirmedAt = nowUtc;
        run.Sst.Signature = signature;

        await _db.SaveChangesAsync(ct);
        return await GetRunAsync(runId, userId, ct);
    }

    private async Task<List<(Material Lot, decimal Quantity)>> PlanSstStockDeductionsAsync(
        HplcRun run, Dictionary<int, HplcMethodAnalyteResponse> snapshotAnalyteById, CancellationToken ct)
    {
        var plan = new List<(Material Lot, decimal Quantity)>();
        var today = _clock.LabToday;
        var groups = run.Sst!.Analytes
            .Where(a => a.StandardMaterialId.HasValue && a.StandardWeightMg.HasValue)
            .GroupBy(a => a.StandardMaterialId!.Value);
        foreach (var g in groups)
        {
            var lot = await _db.Materials.FirstOrDefaultAsync(m => m.Id == g.Key, ct)
                ?? throw new BusinessRuleException(
                    MessageCodes.HplcStandardMaterialNotFound,
                    $"{g.First().AnalyteName}: standard material not found.",
                    new Dictionary<string, string> { ["analyte"] = g.First().AnalyteName });
            var mg = g.Sum(a => a.StandardWeightMg!.Value);
            var quantity = lot.Unit switch
            {
                MaterialUnit.Gram => mg / 1000m,
                MaterialUnit.Kilogram => mg / 1_000_000m,
                _ => throw new BusinessRuleException(
                    MessageCodes.HplcLotUnitIncompatibleWithMg,
                    $"Lot {lot.LotLabel} is stocked in {lot.Unit}; a standard weight in mg cannot be deducted from it.",
                    new Dictionary<string, string> { ["lot"] = lot.LotLabel, ["unit"] = lot.Unit.ToString() }),
            };
            foreach (var a in g)
            {
                var check = LotUsability.Check(lot, snapshotAnalyteById[a.HplcMethodAnalyteId].StandardEntryId, quantity, today);
                if (!check.Usable)
                    throw new BusinessRuleException(
                        MessageCodes.HplcStandardLotUnusable,
                        $"{a.AnalyteName}: lot {lot.LotLabel}: {check.Reason}",
                        new Dictionary<string, string>
                        {
                            ["analyte"] = a.AnalyteName,
                            ["lot"] = lot.LotLabel,
                            ["reason"] = check.Reason ?? string.Empty
                        });
            }
            plan.Add((lot, quantity));
        }
        return plan;
    }

    // ---- Evidence ----

    public async Task<HplcEvidenceDto> UploadEvidenceAsync(
        int runId, int? runSampleId, HplcEvidenceContext ctx, HplcEvidenceKind kind,
        string fileName, string contentType, byte[] content, int userId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        await EnsureAccessAsync(run, userId, ct);

        if (run.Status != HplcRunStatus.Open)
            throw new BusinessRuleException(MessageCodes.HplcRunClosed, "The run is closed.");

        ValidateEvidenceFile(fileName, contentType, content);

        HplcRunSample? runSample = null;
        if (runSampleId.HasValue)
        {
            runSample = run.Samples.FirstOrDefault(s => s.Id == runSampleId.Value)
                ?? throw new NotFoundException(
                    MessageCodes.HplcRunSampleNotFound,
                    $"Sample {runSampleId} not found on this run.",
                    new Dictionary<string, string> { ["runSampleId"] = runSampleId.Value.ToString(CultureInfo.InvariantCulture) });
        }

        var evidence = new HplcEvidence
        {
            HplcRunId = run.Id,
            HplcRunSampleId = runSample?.Id,
            Context = ctx,
            Kind = kind,
            FilePath = "pending",
            FileName = Path.GetFileName(fileName).Trim(),
            ContentType = NormalizeContentType(contentType),
            UploadedByUserId = userId,
            UploadedAt = _clock.UtcNow.UtcDateTime,
        };

        _db.HplcEvidences.Add(evidence);
        await _db.SaveChangesAsync(ct);

        var key = $"hplc-evidence/{run.Id}/{evidence.Id}{Path.GetExtension(evidence.FileName)}";
        evidence.FilePath = await _storage.SaveAsync(key, content);
        await _db.SaveChangesAsync(ct);

        var name = await _db.Users.Where(u => u.Id == userId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        return ToEvidenceDto(evidence, name);
    }

    public async Task<HplcEvidenceDto> SupersedeEvidenceAsync(
        int evidenceId, string reason, string fileName, string contentType, byte[] content, int userId, CancellationToken ct = default)
    {
        var old = await _db.HplcEvidences.Include(e => e.HplcRun).FirstOrDefaultAsync(e => e.Id == evidenceId, ct)
            ?? throw new NotFoundException(
                MessageCodes.HplcEvidenceNotFound,
                $"Evidence {evidenceId} not found.",
                new Dictionary<string, string> { ["evidenceId"] = evidenceId.ToString(CultureInfo.InvariantCulture) });

        await EnsureAccessAsync(old.HplcRun!, userId, ct);

        if (old.SupersededByEvidenceId.HasValue)
            throw new BusinessRuleException(MessageCodes.HplcEvidenceAlreadySuperseded, "This evidence has already been superseded.");
        if (old.HplcRun!.Status != HplcRunStatus.Open)
            throw new BusinessRuleException(MessageCodes.HplcRunClosed, "The run is closed.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleException(MessageCodes.HplcReasonRequired, "A reason is required.");

        ValidateEvidenceFile(fileName, contentType, content);

        var newEvidence = new HplcEvidence
        {
            HplcRunId = old.HplcRunId,
            HplcRunSampleId = old.HplcRunSampleId,
            Context = old.Context,
            Kind = old.Kind,
            FilePath = "pending",
            FileName = Path.GetFileName(fileName).Trim(),
            ContentType = NormalizeContentType(contentType),
            UploadedByUserId = userId,
            UploadedAt = _clock.UtcNow.UtcDateTime,
        };
        _db.HplcEvidences.Add(newEvidence);
        old.SupersedeReason = reason.Trim();

        await _db.SaveChangesAsync(ct);

        old.SupersededByEvidenceId = newEvidence.Id;
        var key = $"hplc-evidence/{newEvidence.HplcRunId}/{newEvidence.Id}{Path.GetExtension(newEvidence.FileName)}";
        newEvidence.FilePath = await _storage.SaveAsync(key, content);

        await _db.SaveChangesAsync(ct);

        var name = await _db.Users.Where(u => u.Id == userId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        return ToEvidenceDto(newEvidence, name);
    }

    public async Task<(byte[] Content, string ContentType, string FileName)> DownloadEvidenceAsync(int evidenceId, int userId, CancellationToken ct = default)
    {
        var evidence = await _db.HplcEvidences.Include(e => e.HplcRun).FirstOrDefaultAsync(e => e.Id == evidenceId, ct)
            ?? throw new NotFoundException(
                MessageCodes.HplcEvidenceNotFound,
                $"Evidence {evidenceId} not found.",
                new Dictionary<string, string> { ["evidenceId"] = evidenceId.ToString(CultureInfo.InvariantCulture) });

        await EnsureAccessAsync(evidence.HplcRun!, userId, ct, SectionAccess.Read);

        var content = await _storage.ReadAsync(evidence.FilePath);
        return (content, evidence.ContentType, evidence.FileName);
    }

    public async Task<HplcDissolutionStandardResult> GetDissolutionStandardAsync(int testOrderId, int userId, CancellationToken ct = default)
    {
        await _scope.EnsureTestOrderAccessAsync(userId, testOrderId, ct, access: SectionAccess.Read);
        return await HplcDissolutionStandard.ResolveAsync(_db, testOrderId, ct);
    }

    // What a reviewer needs for one test order: the current sample evidence of
    // every run it was assigned to plus each such run's SST standard report.
    public async Task<List<HplcEvidenceDto>> GetTestOrderEvidenceAsync(int testOrderId, int userId, CancellationToken ct = default)
    {
        await _scope.EnsureTestOrderAccessAsync(userId, testOrderId, ct, access: SectionAccess.Read);

        var runSamples = await _db.HplcRunSamples.AsNoTracking()
            .Where(s => s.TestOrderId == testOrderId).Select(s => new { s.Id, s.HplcRunId }).ToListAsync(ct);
        var sampleIds = runSamples.Select(s => s.Id).ToList();
        var runIds = runSamples.Select(s => s.HplcRunId).Distinct().ToList();

        var rows = await _db.HplcEvidences.AsNoTracking()
            .Where(e => e.SupersededByEvidenceId == null
                && ((e.HplcRunSampleId != null && sampleIds.Contains(e.HplcRunSampleId.Value))
                    || (runIds.Contains(e.HplcRunId) && e.Context == HplcEvidenceContext.Sst && e.Kind == HplcEvidenceKind.StandardReport)))
            .OrderByDescending(e => e.UploadedAt).ThenByDescending(e => e.Id)
            .ToListAsync(ct);

        var uploaderIds = rows.Select(e => e.UploadedByUserId).Distinct().ToList();
        var names = await _db.Users.Where(u => uploaderIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        return rows.Select(e => ToEvidenceDto(e, names.GetValueOrDefault(e.UploadedByUserId))).ToList();
    }

    private static void ValidateEvidenceFile(string fileName, string contentType, byte[] content)
    {
        if (content == null || content.Length == 0)
            throw new BusinessRuleException(MessageCodes.HplcEvidenceFileEmpty, "The uploaded file is empty.");
        if (content.Length > MaxEvidenceBytes)
            throw new BusinessRuleException(
                MessageCodes.HplcEvidenceFileTooLarge,
                $"File exceeds the maximum allowed size of {MaxEvidenceBytes / 1024 / 1024} MB.",
                new Dictionary<string, string> { ["maxMb"] = (MaxEvidenceBytes / 1024 / 1024).ToString(CultureInfo.InvariantCulture) });
        if (string.IsNullOrWhiteSpace(fileName))
            throw new BusinessRuleException(MessageCodes.HplcEvidenceFileNameRequired, "A file name is required.");

        var normalized = NormalizeContentType(contentType);
        if (!AllowedEvidenceContentTypes.Contains(normalized))
            throw new BusinessRuleException(
                MessageCodes.HplcEvidenceFileTypeUnsupported,
                $"File type \"{normalized}\" is not supported. Allowed types: PDF, PNG, JPEG.",
                new Dictionary<string, string> { ["contentType"] = normalized });
    }

    private static string NormalizeContentType(string contentType) => contentType.Split(';')[0].Trim().ToLowerInvariant();

    private static HplcEvidenceDto ToEvidenceDto(HplcEvidence e, string? uploaderName) => new(
        e.Id, e.HplcRunId, e.HplcRunSampleId, e.Context, e.Kind, e.FileName, e.ContentType,
        e.UploadedByUserId, uploaderName, e.UploadedAt, e.SupersededByEvidenceId == null, e.SupersedeReason);

    // ---- Abandon / history ----

    public async Task<HplcRunDto> AbandonRunAsync(int runId, string reason, int userId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        await EnsureAccessAsync(run, userId, ct);

        if (run.Status != HplcRunStatus.Open)
            throw new BusinessRuleException(MessageCodes.HplcRunClosed, "The run is closed.");

        var trimmedReason = ValidateReason(reason);

        run.Status = HplcRunStatus.Abandoned;
        run.ClosedAt = _clock.UtcNow.UtcDateTime;
        run.CloseReason = trimmedReason;

        await _db.SaveChangesAsync(ct);
        return await GetRunAsync(runId, userId, ct);
    }

    public async Task<List<HplcRunListItem>> GetRunHistoryAsync(int equipmentId, int userId, CancellationToken ct = default)
    {
        var equipment = await _db.Equipment.FirstOrDefaultAsync(e => e.Id == equipmentId, ct)
            ?? throw new NotFoundException(
                MessageCodes.HplcEquipmentNotFound,
                $"Equipment {equipmentId} not found.",
                new Dictionary<string, string> { ["equipmentId"] = equipmentId.ToString(CultureInfo.InvariantCulture) });

        var scope = await _scope.GetReadableSectionIdsAsync(userId, ct);
        if (scope != null && !scope.Contains(equipment.SectionId))
            throw new NotFoundException(
                MessageCodes.HplcEquipmentNotFound,
                $"Equipment {equipmentId} not found.",
                new Dictionary<string, string> { ["equipmentId"] = equipmentId.ToString(CultureInfo.InvariantCulture) });

        var runs = await _db.HplcRuns.AsNoTracking()
            .Include(r => r.HplcMethod)
            .Include(r => r.Sst)
            .Where(r => r.EquipmentId == equipmentId)
            .OrderByDescending(r => r.StartedAt)
            .ToListAsync(ct);

        var userIds = runs.Select(r => r.AnalystUserId).Distinct().ToList();
        var names = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return runs.Select(r => new HplcRunListItem(
            r.Id, r.Code, r.HplcMethod?.Abbreviation ?? string.Empty,
            names.TryGetValue(r.AnalystUserId, out var n) ? n : "Unknown",
            r.StartedAt, r.Status, r.Sst?.Status ?? HplcSstStatus.Pending, r.ClosedAt)).ToList();
    }

    private static string ValidateReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleException(MessageCodes.HplcReasonRequired, "A reason is required.");
        var trimmed = reason.Trim();
        if (trimmed.Length > 500)
            throw new BusinessRuleException(MessageCodes.HplcReasonTooLong, "Reason cannot exceed 500 characters.");
        return trimmed;
    }

    // ---- Shared loading / mapping ----

    private async Task EnsureAccessAsync(HplcRun run, int userId, CancellationToken ct, SectionAccess access = SectionAccess.Write)
    {
        var scope = await _scope.GetSectionIdsAsync(userId, access, ct);
        if (scope != null && !scope.Contains(run.SectionId))
            throw new NotFoundException(
                MessageCodes.HplcRunNotFound,
                $"HPLC run {run.Id} not found.",
                new Dictionary<string, string> { ["runId"] = run.Id.ToString(CultureInfo.InvariantCulture) });
    }

    private async Task<HplcRun> LoadRunAsync(int id, CancellationToken ct) =>
        await _db.HplcRuns
            .Include(r => r.Section)
            .Include(r => r.Equipment)
            .Include(r => r.ChromatographyColumn)
            .Include(r => r.HplcMethod)
            .Include(r => r.MobilePhases).ThenInclude(mp => mp.SolutionPreparation).ThenInclude(p => p!.SolutionMaster)
            .Include(r => r.Sst).ThenInclude(s => s!.Analytes).ThenInclude(a => a.Injections)
            .Include(r => r.Sst).ThenInclude(s => s!.Analytes).ThenInclude(a => a.StandardMaterial)
            .Include(r => r.Samples)
            .Include(r => r.Evidence)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException(
                MessageCodes.HplcRunNotFound,
                $"HPLC run {id} not found.",
                new Dictionary<string, string> { ["runId"] = id.ToString(CultureInfo.InvariantCulture) });

    private async Task<HplcRunDto> BuildDtoAsync(HplcRun run, CancellationToken ct)
    {
        var userIds = new HashSet<int> { run.AnalystUserId };
        if (run.Sst?.ConfirmedByUserId.HasValue == true) userIds.Add(run.Sst.ConfirmedByUserId.Value);
        foreach (var e in run.Evidence) userIds.Add(e.UploadedByUserId);
        var names = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        string? NameOf(int? id) => id.HasValue && names.TryGetValue(id.Value, out var n) ? n : null;

        var standardEntryByAnalyte = new Dictionary<int, int>();
        if (run.Sst != null)
        {
            var analyteIds = run.Sst.Analytes.Select(a => a.HplcMethodAnalyteId).ToList();
            standardEntryByAnalyte = await _db.HplcMethodAnalytes.AsNoTracking()
                .Where(a => analyteIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.StandardEntryId, ct);
        }

        var sstDto = run.Sst == null ? null : new HplcSstRecordDto(
            run.Sst.Id, run.Sst.Code, run.Sst.Status, run.Sst.FailureReasons,
            run.Sst.ConfirmedByUserId, NameOf(run.Sst.ConfirmedByUserId), run.Sst.ConfirmedAt,
            run.Sst.Analytes.OrderBy(a => a.Id).Select(a => new HplcSstAnalyteDto(
                a.Id, a.HplcMethodAnalyteId, a.AnalyteName, standardEntryByAnalyte.GetValueOrDefault(a.HplcMethodAnalyteId), a.StandardMaterialId, a.StandardMaterial?.LotLabel,
                a.StandardPurityPercent, a.StandardMoisturePercent, a.StandardWeightMg,
                a.Injections.OrderBy(i => i.InjectionNo).Select(i => new HplcSstInjectionDto(i.InjectionNo, i.Response, i.InternalStandardResponse)).ToList(),
                a.ReportedRsdPercent, a.Resolution, a.TailingFactor, a.TheoreticalPlates, a.RetentionFactor, a.SignalToNoise, a.PeakToValley,
                a.MeanResponse, a.ComputedRsdPercent, a.Passed, a.FailureReasons)).ToList());

        var mobilePhases = run.MobilePhases.Select(mp => new HplcRunMobilePhaseDto(
            mp.Id, mp.Channel, mp.SolutionPreparationId, mp.SolutionPreparation?.Code,
            mp.SolutionPreparation?.SolutionMaster?.Name ?? string.Empty, mp.SolutionPreparation?.ExpiresAt)).ToList();

        var samples = await BuildSampleSummariesAsync(run, ct);

        var evidence = run.Evidence.OrderByDescending(e => e.UploadedAt).Select(e => new HplcEvidenceDto(
            e.Id, e.HplcRunId, e.HplcRunSampleId, e.Context, e.Kind, e.FileName, e.ContentType,
            e.UploadedByUserId, NameOf(e.UploadedByUserId), e.UploadedAt, e.SupersededByEvidenceId == null, e.SupersedeReason)).ToList();

        var hasStandardReport = run.Evidence.Any(IsCurrentSstStandardReport);
        var missingSst = run.Sst?.Status == HplcSstStatus.Pending ? MissingSstEntries(run) : new List<string>();
        var canConfirmSst = run.Status == HplcRunStatus.Open && run.Sst?.Status == HplcSstStatus.Pending
            && missingSst.Count == 0 && hasStandardReport;
        string? canConfirmSstReason = run.Status != HplcRunStatus.Open
            ? "The run is closed."
            : run.Sst?.Status != HplcSstStatus.Pending
                ? "System suitability has already been confirmed."
                : missingSst.Count > 0
                    ? MissingSstReason(missingSst)
                    : !hasStandardReport
                        ? MissingStandardReportReason
                        : null;

        var canAssignSamples = run.Status == HplcRunStatus.Open && run.Sst?.Status == HplcSstStatus.Passed;
        string? canAssignSamplesReason = canAssignSamples
            ? null
            : run.Status != HplcRunStatus.Open
                ? "The run is closed."
                : "Sample assignment is locked until system suitability passes.";

        var runSnapshot = JsonSerializer.Deserialize<HplcMethodResponse>(run.MethodSnapshotJson, JsonOptions);

        return new HplcRunDto(
            run.Id, run.Version, run.Code, run.SectionId,
            run.EquipmentId, run.Equipment?.Code ?? string.Empty, run.Equipment?.Name ?? string.Empty,
            run.ChromatographyColumnId, run.ChromatographyColumn?.Code ?? string.Empty,
            run.HplcMethodId, run.HplcMethod?.Abbreviation ?? string.Empty,
            run.AnalystUserId, NameOf(run.AnalystUserId), run.StartedAt, run.Status, run.ClosedAt, run.CloseReason,
            mobilePhases, sstDto, samples, evidence,
            canConfirmSst, canConfirmSstReason, canAssignSamples, canAssignSamplesReason,
            runSnapshot?.ResultMode ?? HplcResultMode.Assay,
            string.IsNullOrWhiteSpace(runSnapshot?.InternalStandardName) ? null : runSnapshot!.InternalStandardName);
    }
}
