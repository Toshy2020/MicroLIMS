using System.Text.Json;
using MicroLIMS.Shared.Exceptions;
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
public record HplcMethodOptionDto(int Id, string Abbreviation, string Name, string ColumnDesignation, int EligibleTestOrderCount);

// ---- Start run ----

public record HplcMobilePhaseAssignmentInput(string Channel, int SolutionPreparationId);
public record StartHplcRunRequest(int EquipmentId, int HplcMethodId, int ChromatographyColumnId, List<HplcMobilePhaseAssignmentInput> MobilePhases);

// ---- SST ----

public record SaveSstAnalyteInput(
    int HplcSstAnalyteId, int StandardMaterialId, decimal StandardWeightMg, List<decimal> Responses,
    decimal? ReportedRsdPercent, decimal? Resolution, decimal? TailingFactor, decimal? TheoreticalPlates,
    decimal? RetentionFactor, decimal? SignalToNoise, decimal? PeakToValley);
public record SaveSstRequest(List<SaveSstAnalyteInput> Analytes);
public record ConfirmSstRequest(string Password, string? Comment);

// ---- Evidence ----

public record HplcEvidenceDto(
    int Id, int HplcRunId, int? HplcRunSampleId, HplcEvidenceContext Context, HplcEvidenceKind Kind,
    string FileName, string ContentType, int UploadedByUserId, string? UploadedByUserName, DateTime UploadedAt,
    bool IsCurrent, string? SupersedeReason);

// ---- Run detail ----

public record HplcRunMobilePhaseDto(int Id, string Channel, int SolutionPreparationId, string? SolutionPreparationCode, string SolutionMasterName, DateTime? ExpiresAt);
public record HplcSstInjectionDto(int InjectionNo, decimal Response);
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
    int Id, int TestOrderId, HplcRunSampleStatus Status,
    string SampleNumber, string? BatchNumber, string? ProductName, string TestCode, bool Submitted, bool IsDissolution);

public record HplcRunDto(
    int Id, uint Version, string Code, int SectionId,
    int EquipmentId, string EquipmentCode, string EquipmentName,
    int ChromatographyColumnId, string ChromatographyColumnCode,
    int HplcMethodId, string HplcMethodAbbreviation,
    int AnalystUserId, string? AnalystUserName, DateTime StartedAt, HplcRunStatus Status, DateTime? ClosedAt, string? CloseReason,
    List<HplcRunMobilePhaseDto> MobilePhases, HplcSstRecordDto? Sst, List<HplcRunSampleSummaryDto> Samples, List<HplcEvidenceDto> Evidence,
    bool CanConfirmSst, string? CanConfirmSstReason, bool CanAssignSamples, string? CanAssignSamplesReason);

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
    private static List<string> MissingSstEntries(HplcRun run)
    {
        var missing = new List<string>();
        if (run.Sst == null) return missing;
        var snapshot = JsonSerializer.Deserialize<HplcMethodResponse>(run.MethodSnapshotJson, JsonOptions);
        var methodById = snapshot?.Analytes.ToDictionary(a => a.Id) ?? new Dictionary<int, HplcMethodAnalyteResponse>();

        foreach (var a in run.Sst.Analytes.OrderBy(x => x.Id))
        {
            var fields = new List<string>();
            if (a.StandardMaterialId == null) fields.Add("reference standard lot");
            if (!(a.StandardWeightMg > 0)) fields.Add("actual standard weight");
            if (methodById.TryGetValue(a.HplcMethodAnalyteId, out var m))
            {
                var blank = Enumerable.Range(1, m.StandardInjections)
                    .Where(n => !(a.Injections.FirstOrDefault(i => i.InjectionNo == n)?.Response > 0)).ToList();
                if (blank.Count > 0) fields.Add($"injection {string.Join(", ", blank.Select(n => $"#{n}"))}");
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

    public async Task<List<HplcInstrumentDto>> GetInstrumentsAsync(int userId, CancellationToken ct = default)
    {
        var scope = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        var query = _db.Equipment.Where(e => e.Type == EquipmentType.Hplc).AsNoTracking();
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

    public async Task<List<HplcMethodOptionDto>> GetMethodOptionsAsync(int userId, CancellationToken ct = default)
    {
        var scope = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        var query = _db.HplcMethods.Where(m => m.IsActive).AsNoTracking();
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
            result.Add(new HplcMethodOptionDto(m.Id, m.Abbreviation, m.Name, m.ColumnDesignation, count));
        }

        return result;
    }

    private async Task<string?> DetermineUnavailableReasonAsync(Equipment equipment, CancellationToken ct)
    {
        var today = _clock.LabToday;
        if (equipment.CalibrationDueDate.HasValue && DateOnly.FromDateTime(equipment.CalibrationDueDate.Value) < today)
            return $"Calibration overdue since {equipment.CalibrationDueDate:yyyy-MM-dd}.";

        var codeUpper = equipment.Code.ToUpperInvariant();
        var inventory = await _db.EquipmentInventories.AsNoTracking()
            .Where(i => i.Code.ToUpper() == codeUpper)
            .FirstOrDefaultAsync(ct);

        if (inventory != null && (inventory.Status == EquipmentOperationalStatus.OutOfService || inventory.Status == EquipmentOperationalStatus.Retired))
            return $"Equipment is {inventory.Status}.";

        return null;
    }

    // ---- Start run ----

    public async Task<HplcRunDto> StartRunAsync(StartHplcRunRequest r, int userId, CancellationToken ct = default)
    {
        var equipment = await _db.Equipment.FirstOrDefaultAsync(e => e.Id == r.EquipmentId, ct)
            ?? throw new NotFoundException($"Equipment {r.EquipmentId} not found.");

        var scope = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        if (scope != null && !scope.Contains(equipment.SectionId))
            throw new NotFoundException($"Equipment {r.EquipmentId} not found.");

        if (equipment.Type != EquipmentType.Hplc)
            throw new InvalidOperationException($"\"{equipment.Name}\" is not an HPLC instrument.");

        var hasOpenRun = await _db.HplcRuns.AnyAsync(run => run.EquipmentId == equipment.Id && run.Status == HplcRunStatus.Open, ct);
        if (hasOpenRun)
            throw new InvalidOperationException($"\"{equipment.Name}\" already has an open run.");

        var unavailableReason = await DetermineUnavailableReasonAsync(equipment, ct);
        if (unavailableReason != null)
            throw new InvalidOperationException(unavailableReason);

        var method = await _db.HplcMethods
            .Include(m => m.Section)
            .Include(m => m.DiluentSolution)
            .Include(m => m.MobilePhases).ThenInclude(mp => mp.SolutionMaster)
            .Include(m => m.GradientSteps)
            .Include(m => m.Analytes).ThenInclude(a => a.StandardEntry)
            .FirstOrDefaultAsync(m => m.Id == r.HplcMethodId, ct)
            ?? throw new NotFoundException($"HPLC method {r.HplcMethodId} not found.");

        if (!method.IsActive)
            throw new InvalidOperationException($"HPLC method \"{method.Name}\" is inactive.");

        if (method.SectionId != equipment.SectionId)
            throw new InvalidOperationException("The HPLC method belongs to another laboratory.");

        var column = await _db.ChromatographyColumns.Include(c => c.CompatibleEquipment)
            .FirstOrDefaultAsync(c => c.Id == r.ChromatographyColumnId, ct)
            ?? throw new NotFoundException($"Column {r.ChromatographyColumnId} not found.");

        if (!column.IsActive)
            throw new InvalidOperationException($"Column \"{column.Name}\" is inactive.");

        if (!column.CompatibleEquipment.Any(e => e.Id == equipment.Id))
            throw new InvalidOperationException($"Column \"{column.Name}\" is not compatible with \"{equipment.Name}\".");

        if (string.IsNullOrWhiteSpace(column.UspDesignation))
            throw new InvalidOperationException($"Column {column.Code} has no USP designation; set it in the column master.");
        if (!string.Equals(column.UspDesignation.Trim(), method.ColumnDesignation?.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Column {column.Code} is USP {column.UspDesignation}; method {method.Abbreviation} requires {method.ColumnDesignation}.");

        var inputs = r.MobilePhases ?? new List<HplcMobilePhaseAssignmentInput>();
        if (inputs.Count != method.MobilePhases.Count)
            throw new InvalidOperationException("Select a prepared mobile phase for every channel this method uses.");

        var nowUtc = _clock.UtcNow.UtcDateTime;
        var mobilePhaseEntities = new List<HplcRunMobilePhase>();
        foreach (var channelDef in method.MobilePhases)
        {
            var input = inputs.FirstOrDefault(mp => string.Equals(mp.Channel, channelDef.Channel, StringComparison.OrdinalIgnoreCase));
            if (input == null)
                throw new InvalidOperationException($"Select a prepared mobile phase for channel {channelDef.Channel}.");

            var prep = await _db.SolutionPreparations.FirstOrDefaultAsync(p => p.Id == input.SolutionPreparationId, ct)
                ?? throw new NotFoundException($"Solution preparation {input.SolutionPreparationId} not found.");

            if (prep.Type != SolutionType.MobilePhase)
                throw new InvalidOperationException($"Channel {channelDef.Channel}: the selected preparation is not a mobile phase.");
            if (prep.SolutionMasterId != channelDef.SolutionMasterId)
                throw new InvalidOperationException($"Channel {channelDef.Channel}: the selected preparation is not of this channel's solution.");
            if (prep.HplcMethodId != method.Id)
                throw new InvalidOperationException($"Channel {channelDef.Channel}: the selected preparation was not prepared for this method.");
            if (SolutionPreparationResponse.EffectiveStatusOf(prep, nowUtc) != SolutionPreparationStatus.Prepared)
                throw new InvalidOperationException($"Channel {channelDef.Channel}: the selected preparation is not currently prepared.");

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
        await EnsureAccessAsync(run, userId, ct);
        return await BuildDtoAsync(run, ct);
    }

    // ---- SST ----

    public async Task<HplcRunDto> SaveSstAsync(int runId, SaveSstRequest r, int userId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        await EnsureAccessAsync(run, userId, ct);

        if (run.Status != HplcRunStatus.Open)
            throw new InvalidOperationException("The run is closed.");
        if (run.Sst == null || run.Sst.Status != HplcSstStatus.Pending)
            throw new InvalidOperationException("System suitability has already been confirmed.");

        var method = await _db.HplcMethods.Include(m => m.Analytes).FirstAsync(m => m.Id == run.HplcMethodId, ct);
        var methodAnalyteById = method.Analytes.ToDictionary(a => a.Id);
        var today = _clock.LabToday;

        foreach (var input in r.Analytes ?? new List<SaveSstAnalyteInput>())
        {
            var analyteRow = run.Sst.Analytes.FirstOrDefault(a => a.Id == input.HplcSstAnalyteId)
                ?? throw new InvalidOperationException($"Analyte {input.HplcSstAnalyteId} does not belong to this system suitability record.");

            if (!methodAnalyteById.TryGetValue(analyteRow.HplcMethodAnalyteId, out var methodAnalyte))
                throw new InvalidOperationException($"{analyteRow.AnalyteName}: method analyte not found.");

            if (input.StandardWeightMg <= 0)
                throw new InvalidOperationException($"{analyteRow.AnalyteName}: standard weight must be greater than zero.");

            var lot = await _db.Materials.FirstOrDefaultAsync(m => m.Id == input.StandardMaterialId, ct)
                ?? throw new InvalidOperationException($"{analyteRow.AnalyteName}: standard material not found.");

            if (lot.SectionId != run.SectionId)
                throw new InvalidOperationException($"{analyteRow.AnalyteName}: standard lot belongs to another laboratory.");

            var check = LotUsability.Check(lot, methodAnalyte.StandardEntryId, null, today);
            if (!check.Usable)
                throw new InvalidOperationException($"{analyteRow.AnalyteName}: lot {lot.BatchNumber}: {check.Reason}");

            if (!lot.Purity.HasValue)
                throw new InvalidOperationException($"Lot {lot.BatchNumber} has no purity recorded.");
            if (!lot.MoisturePercent.HasValue)
                throw new InvalidOperationException($"Lot {lot.BatchNumber} has no moisture content recorded.");

            analyteRow.StandardMaterialId = lot.Id;
            analyteRow.StandardWeightMg = input.StandardWeightMg;
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
            for (var i = 0; i < responses.Count; i++)
                analyteRow.Injections.Add(new HplcSstInjection { InjectionNo = i + 1, Response = responses[i] });

            // Shown while entering; Passed/FailureReasons stay untouched until confirmation.
            analyteRow.MeanResponse = responses.Count > 0 ? responses.Average() : null;
            analyteRow.ComputedRsdPercent = StandardComparisonCalculator.CalculatePreparationRsd(responses, null).RsdPercent;
        }

        await _db.SaveChangesAsync(ct);
        return await GetRunAsync(runId, userId, ct);
    }

    public async Task<HplcRunDto> ConfirmSstAsync(int runId, ConfirmSstRequest r, int userId, string? ip, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        await EnsureAccessAsync(run, userId, ct);

        if (run.Status != HplcRunStatus.Open)
            throw new InvalidOperationException("The run is closed.");
        if (run.Sst == null || run.Sst.Status != HplcSstStatus.Pending)
            throw new InvalidOperationException("System suitability has already been confirmed.");

        var missingEntries = MissingSstEntries(run);
        if (missingEntries.Count > 0)
            throw new InvalidOperationException(MissingSstReason(missingEntries));

        if (!run.Evidence.Any(IsCurrentSstStandardReport))
            throw new InvalidOperationException(MissingStandardReportReason);

        var nowUtc = _clock.UtcNow.UtcDateTime;
        foreach (var mp in run.MobilePhases)
        {
            if (mp.SolutionPreparation == null)
                throw new InvalidOperationException($"Channel {mp.Channel}: the selected preparation no longer exists.");
            if (SolutionPreparationResponse.EffectiveStatusOf(mp.SolutionPreparation, nowUtc) != SolutionPreparationStatus.Prepared)
                throw new InvalidOperationException($"Channel {mp.Channel}: the mobile phase preparation has expired since it was selected.");
        }

        var snapshot = JsonSerializer.Deserialize<HplcMethodResponse>(run.MethodSnapshotJson, JsonOptions)
            ?? throw new InvalidOperationException("The run's method snapshot could not be read.");
        var snapshotAnalyteById = snapshot.Analytes.ToDictionary(a => a.Id);

        // Pure evaluation only - no mutation yet (sign-first, see class comment).
        var outcomes = new Dictionary<int, SstOutcome>();
        foreach (var analyteRow in run.Sst.Analytes)
        {
            if (!snapshotAnalyteById.TryGetValue(analyteRow.HplcMethodAnalyteId, out var snapAnalyte))
                throw new InvalidOperationException($"{analyteRow.AnalyteName}: method analyte snapshot not found.");

            var criteria = new SstCriteria(
                snapAnalyte.SstMaxRsdPercent, snapAnalyte.SstMinResolution, snapAnalyte.SstMaxTailingFactor,
                snapAnalyte.SstMinTheoreticalPlates, snapAnalyte.SstMinRetentionFactor, snapAnalyte.SstMinSignalToNoise,
                snapAnalyte.SstMinPeakToValley, snapAnalyte.StandardInjections);

            var entered = new SstEntered(
                analyteRow.Injections.OrderBy(i => i.InjectionNo).Select(i => i.Response).ToList(),
                analyteRow.ReportedRsdPercent, analyteRow.Resolution, analyteRow.TailingFactor,
                analyteRow.TheoreticalPlates, analyteRow.RetentionFactor, analyteRow.SignalToNoise, analyteRow.PeakToValley);

            outcomes[analyteRow.Id] = HplcSstEvaluator.Evaluate(analyteRow.AnalyteName, criteria, entered);
        }

        var overallPassed = outcomes.Values.All(o => o.Passed);
        var combinedFailures = outcomes.Values.SelectMany(o => o.FailureReasons).ToList();

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

        run.Sst.Status = overallPassed ? HplcSstStatus.Passed : HplcSstStatus.Failed;
        run.Sst.FailureReasons = combinedFailures.Count > 0 ? string.Join(" ", combinedFailures) : null;
        run.Sst.ConfirmedByUserId = userId;
        run.Sst.ConfirmedAt = nowUtc;
        run.Sst.Signature = signature;

        await _db.SaveChangesAsync(ct);
        return await GetRunAsync(runId, userId, ct);
    }

    // ---- Evidence ----

    public async Task<HplcEvidenceDto> UploadEvidenceAsync(
        int runId, int? runSampleId, HplcEvidenceContext ctx, HplcEvidenceKind kind,
        string fileName, string contentType, byte[] content, int userId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        await EnsureAccessAsync(run, userId, ct);

        if (run.Status != HplcRunStatus.Open)
            throw new InvalidOperationException("The run is closed.");

        ValidateEvidenceFile(fileName, contentType, content);

        HplcRunSample? runSample = null;
        if (runSampleId.HasValue)
        {
            runSample = run.Samples.FirstOrDefault(s => s.Id == runSampleId.Value)
                ?? throw new NotFoundException($"Sample {runSampleId} not found on this run.");
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
            ?? throw new NotFoundException($"Evidence {evidenceId} not found.");

        await EnsureAccessAsync(old.HplcRun!, userId, ct);

        if (old.SupersededByEvidenceId.HasValue)
            throw new InvalidOperationException("This evidence has already been superseded.");
        if (old.HplcRun!.Status != HplcRunStatus.Open)
            throw new InvalidOperationException("The run is closed.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason is required.");

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
            ?? throw new NotFoundException($"Evidence {evidenceId} not found.");

        await EnsureAccessAsync(evidence.HplcRun!, userId, ct);

        var content = await _storage.ReadAsync(evidence.FilePath);
        return (content, evidence.ContentType, evidence.FileName);
    }

    public async Task<HplcDissolutionStandardResult> GetDissolutionStandardAsync(int testOrderId, int userId, CancellationToken ct = default)
    {
        await _scope.EnsureTestOrderAccessAsync(userId, testOrderId, ct);
        return await HplcDissolutionStandard.ResolveAsync(_db, testOrderId, ct);
    }

    // What a reviewer needs for one test order: the current sample evidence of
    // every run it was assigned to plus each such run's SST standard report.
    public async Task<List<HplcEvidenceDto>> GetTestOrderEvidenceAsync(int testOrderId, int userId, CancellationToken ct = default)
    {
        await _scope.EnsureTestOrderAccessAsync(userId, testOrderId, ct);

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
            throw new InvalidOperationException("The uploaded file is empty.");
        if (content.Length > MaxEvidenceBytes)
            throw new InvalidOperationException($"File exceeds the maximum allowed size of {MaxEvidenceBytes / 1024 / 1024} MB.");
        if (string.IsNullOrWhiteSpace(fileName))
            throw new InvalidOperationException("A file name is required.");

        var normalized = NormalizeContentType(contentType);
        if (!AllowedEvidenceContentTypes.Contains(normalized))
            throw new InvalidOperationException($"File type \"{normalized}\" is not supported. Allowed types: PDF, PNG, JPEG.");
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
            throw new InvalidOperationException("The run is closed.");

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
            ?? throw new NotFoundException($"Equipment {equipmentId} not found.");

        var scope = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        if (scope != null && !scope.Contains(equipment.SectionId))
            throw new NotFoundException($"Equipment {equipmentId} not found.");

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
            throw new InvalidOperationException("A reason is required.");
        var trimmed = reason.Trim();
        if (trimmed.Length > 500)
            throw new InvalidOperationException("Reason cannot exceed 500 characters.");
        return trimmed;
    }

    // ---- Shared loading / mapping ----

    private async Task EnsureAccessAsync(HplcRun run, int userId, CancellationToken ct)
    {
        var scope = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        if (scope != null && !scope.Contains(run.SectionId))
            throw new NotFoundException($"HPLC run {run.Id} not found.");
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
            ?? throw new NotFoundException($"HPLC run {id} not found.");

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
                a.Id, a.HplcMethodAnalyteId, a.AnalyteName, standardEntryByAnalyte.GetValueOrDefault(a.HplcMethodAnalyteId), a.StandardMaterialId, a.StandardMaterial?.BatchNumber,
                a.StandardPurityPercent, a.StandardMoisturePercent, a.StandardWeightMg,
                a.Injections.OrderBy(i => i.InjectionNo).Select(i => new HplcSstInjectionDto(i.InjectionNo, i.Response)).ToList(),
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

        return new HplcRunDto(
            run.Id, run.Version, run.Code, run.SectionId,
            run.EquipmentId, run.Equipment?.Code ?? string.Empty, run.Equipment?.Name ?? string.Empty,
            run.ChromatographyColumnId, run.ChromatographyColumn?.Code ?? string.Empty,
            run.HplcMethodId, run.HplcMethod?.Abbreviation ?? string.Empty,
            run.AnalystUserId, NameOf(run.AnalystUserId), run.StartedAt, run.Status, run.ClosedAt, run.CloseReason,
            mobilePhases, sstDto, samples, evidence,
            canConfirmSst, canConfirmSstReason, canAssignSamples, canAssignSamplesReason);
    }
}
