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

public record IcpActiveRunSummaryDto(int RunId, string Code, string MethodAbbreviation, string AnalystName, int SampleCount, IcpCalibrationStatus CalibrationStatus);
public record IcpInstrumentDto(int EquipmentId, string Code, string Name, string State, string? Reason, IcpActiveRunSummaryDto? ActiveRun);
public record IcpMethodOptionDto(int Id, string Abbreviation, string Name, IcpMethodMode Mode, int EligibleTestOrderCount);

// ---- Requests ----

public record StartIcpRunRequest(int EquipmentId, int IcpMethodId);
public record SaveIcpCalibrationElementInput(int IcpCalibrationElementId, decimal? CorrelationR, decimal? BlankMgPerL, decimal? IcvMeasuredMgPerL);
public record SaveIcpCalibrationRequest(int? CalibrationStandardMaterialId, int? IcvStandardMaterialId, List<SaveIcpCalibrationElementInput> Elements);
public record ConfirmIcpCalibrationRequest(string Password, string? Comment);

// ---- Run detail ----

public record IcpEvidenceDto(
    int Id, int IcpRunId, int? IcpRunSampleId, IcpEvidenceContext Context, IcpEvidenceKind Kind,
    string FileName, string ContentType, int UploadedByUserId, string? UploadedByUserName, DateTime UploadedAt,
    bool IsCurrent, string? SupersedeReason);

public record IcpCalibrationElementDto(
    int Id, int IcpMethodElementId, string Symbol,
    decimal? CorrelationR, decimal? BlankMgPerL, decimal? IcvMeasuredMgPerL, decimal? IcvRecoveryPercent,
    bool Passed, string? FailureReasons);

public record IcpCalibrationDto(
    int Id, string Code, IcpCalibrationStatus Status,
    int? CalibrationStandardMaterialId, string? CalibrationStandardLotLabel, string? CalibrationStandardCode,
    int? IcvStandardMaterialId, string? IcvStandardLotLabel, string? IcvStandardCode,
    int? ConfirmedByUserId, string? ConfirmedByUserName, DateTime? ConfirmedAt, DateTime? ExpiresAt,
    List<IcpCalibrationElementDto> Elements);

public record IcpCcvReadingDto(
    int Id, int IcpMethodElementId, string Symbol, decimal MeasuredMgPerL, decimal RecoveryPercent, bool Passed,
    int EnteredByUserId, string? EnteredByUserName, DateTime EnteredAt);

public record IcpRunSampleSummaryDto(
    int Id, int TestOrderId, IcpRunSampleStatus Status,
    string SampleNumber, string? BatchNumber, string? ProductName, string TestCode);

public record IcpRunDto(
    int Id, uint Version, string Code, int SectionId,
    int EquipmentId, string EquipmentCode, string EquipmentName,
    int IcpMethodId, string IcpMethodAbbreviation, IcpMethodResponse Method,
    int AnalystUserId, string? AnalystUserName, DateTime StartedAt, IcpRunStatus Status, DateTime? ClosedAt, string? CloseReason,
    IcpCalibrationDto Calibration, List<IcpCcvReadingDto> CcvReadings, List<IcpRunSampleSummaryDto> Samples, List<IcpEvidenceDto> Evidence,
    bool CanConfirmCalibration, string? CanConfirmCalibrationReason);

public record IcpRunListItem(int Id, string Code, string MethodAbbreviation, string AnalystUserName, DateTime StartedAt, IcpRunStatus Status, IcpCalibrationStatus CalibrationStatus, int SampleCount);

// ICP workspace (I2). Mirrors HplcRunService: instruments, runs with a method
// snapshot, one calibration record per run, samples, evidence. Sign-first on
// confirmation (SignAsync saves its own audit row on a failed password, so
// nothing else is mutated until it succeeds).
public partial class IcpRunService
{
    private const string InstrumentStateRunning = "Running";
    private const string InstrumentStateUnavailable = "Unavailable";
    private const string InstrumentStateAvailable = "Available";

    private static readonly JsonSerializerOptions JsonOptions = SnapshotJson.Options;

    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly IElectronicSignatureService _signatures;
    private readonly IFileStorageService _storage;
    private readonly ILabClock _clock;

    public IcpRunService(
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

    public async Task<List<IcpInstrumentDto>> GetInstrumentsAsync(int userId, CancellationToken ct = default)
    {
        var scope = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        var query = _db.Equipment.Where(e => e.Type == EquipmentType.IcpOes).AsNoTracking();
        if (scope != null) query = query.Where(e => scope.Contains(e.SectionId));
        var equipmentList = await query.OrderBy(e => e.Code).ToListAsync(ct);

        var equipmentIds = equipmentList.Select(e => e.Id).ToList();
        var openRuns = await _db.IcpRuns.AsNoTracking()
            .Include(r => r.IcpMethod)
            .Include(r => r.Calibration)
            .Include(r => r.Samples)
            .Where(r => equipmentIds.Contains(r.EquipmentId) && r.Status == IcpRunStatus.Open)
            .ToListAsync(ct);
        var runByEquipment = openRuns.ToDictionary(r => r.EquipmentId);

        var analystIds = openRuns.Select(r => r.AnalystUserId).Distinct().ToList();
        var analystNames = await _db.Users.Where(u => analystIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var result = new List<IcpInstrumentDto>();
        foreach (var eq in equipmentList)
        {
            if (runByEquipment.TryGetValue(eq.Id, out var run))
            {
                var summary = new IcpActiveRunSummaryDto(
                    run.Id, run.Code, run.IcpMethod?.Abbreviation ?? string.Empty,
                    analystNames.TryGetValue(run.AnalystUserId, out var analystName) ? analystName : "Unknown",
                    run.Samples.Count(s => s.Status == IcpRunSampleStatus.Assigned),
                    run.Calibration?.Status ?? IcpCalibrationStatus.Pending);
                result.Add(new IcpInstrumentDto(eq.Id, eq.Code, eq.Name, InstrumentStateRunning, null, summary));
                continue;
            }

            var unavailableReason = await DetermineUnavailableReasonAsync(eq, ct);
            result.Add(unavailableReason != null
                ? new IcpInstrumentDto(eq.Id, eq.Code, eq.Name, InstrumentStateUnavailable, unavailableReason, null)
                : new IcpInstrumentDto(eq.Id, eq.Code, eq.Name, InstrumentStateAvailable, null, null));
        }

        return result;
    }

    public async Task<List<IcpMethodOptionDto>> GetMethodOptionsAsync(int userId, CancellationToken ct = default)
    {
        var scope = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        var query = _db.IcpMethods.Where(m => m.IsActive).AsNoTracking();
        if (scope != null) query = query.Where(m => scope.Contains(m.SectionId));
        var methods = await query.OrderBy(m => m.Name).ToListAsync(ct);

        var methodIds = methods.Select(m => m.Id).ToList();
        var testDefs = await _db.TestDefinitions.AsNoTracking()
            .Where(t => t.WorkflowType == WorkflowType.IcpMethodAssay && t.IcpMethodId != null && methodIds.Contains(t.IcpMethodId.Value))
            .Select(t => new { t.Code, IcpMethodId = t.IcpMethodId!.Value })
            .ToListAsync(ct);
        var codesByMethod = testDefs.GroupBy(t => t.IcpMethodId).ToDictionary(g => g.Key, g => g.Select(x => x.Code).ToList());

        var result = new List<IcpMethodOptionDto>();
        foreach (var m in methods)
        {
            var count = 0;
            if (codesByMethod.TryGetValue(m.Id, out var codes) && codes.Count > 0)
            {
                count = await _db.TestOrders.AsNoTracking().CountAsync(o =>
                    codes.Contains(o.TestCode) && !o.IsSuperseded && o.CurrentStep != WorkflowStep.Ready
                    && o.Sample != null && o.Sample.Status != SampleStatus.Voided && o.Sample.Status != SampleStatus.Cancelled, ct);
            }
            result.Add(new IcpMethodOptionDto(m.Id, m.Abbreviation, m.Name, m.Mode, count));
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

    public async Task<IcpRunDto> StartRunAsync(StartIcpRunRequest r, int userId, CancellationToken ct = default)
    {
        var equipment = await _db.Equipment.FirstOrDefaultAsync(e => e.Id == r.EquipmentId, ct)
            ?? throw new NotFoundException($"Equipment {r.EquipmentId} not found.");

        var scope = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        if (scope != null && !scope.Contains(equipment.SectionId))
            throw new NotFoundException($"Equipment {r.EquipmentId} not found.");

        if (equipment.Type != EquipmentType.IcpOes)
            throw new InvalidOperationException($"\"{equipment.Name}\" is not an ICP-OES instrument.");

        var hasOpenRun = await _db.IcpRuns.AnyAsync(run => run.EquipmentId == equipment.Id && run.Status == IcpRunStatus.Open, ct);
        if (hasOpenRun)
            throw new InvalidOperationException($"\"{equipment.Name}\" already has an open run.");

        var unavailableReason = await DetermineUnavailableReasonAsync(equipment, ct);
        if (unavailableReason != null)
            throw new InvalidOperationException(unavailableReason);

        var method = await _db.IcpMethods
            .Include(m => m.Section)
            .Include(m => m.CalibrationStandardEntry)
            .Include(m => m.IcvStandardEntry)
            .Include(m => m.Elements)
            .FirstOrDefaultAsync(m => m.Id == r.IcpMethodId, ct)
            ?? throw new NotFoundException($"ICP method {r.IcpMethodId} not found.");

        if (!method.IsActive)
            throw new InvalidOperationException($"ICP method \"{method.Name}\" is inactive.");

        if (method.SectionId != equipment.SectionId)
            throw new InvalidOperationException("The ICP method belongs to another laboratory.");

        var snapshotJson = JsonSerializer.Serialize(IcpMethodResponse.From(method), JsonOptions);
        var nowUtc = _clock.UtcNow.UtcDateTime;
        var labLocal = _clock.ToLabLocal(nowUtc);
        var runCode = await SystemSuitabilityRunCode.NextAsync(_db.IcpRuns.Select(x => x.Code), method.Abbreviation, labLocal, "RUN", ct);
        var calCode = await SystemSuitabilityRunCode.NextAsync(_db.IcpCalibrations.Select(x => x.Code), method.Abbreviation, labLocal, "CAL", ct);

        var run = new IcpRun
        {
            SectionId = equipment.SectionId,
            Code = runCode,
            EquipmentId = equipment.Id,
            IcpMethodId = method.Id,
            MethodSnapshotJson = snapshotJson,
            AnalystUserId = userId,
            StartedAt = nowUtc,
            Status = IcpRunStatus.Open,
            Calibration = new IcpCalibration
            {
                Code = calCode,
                Status = IcpCalibrationStatus.Pending,
                Elements = method.Elements
                    .OrderBy(e => e.DisplayOrder)
                    .Select(e => new IcpCalibrationElement { IcpMethodElementId = e.Id, Symbol = e.Symbol })
                    .ToList(),
            },
        };

        _db.IcpRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        return await GetRunAsync(run.Id, userId, ct);
    }

    public async Task<IcpRunDto> GetRunAsync(int runId, int userId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        await EnsureAccessAsync(run, userId, ct);
        return await BuildDtoAsync(run, ct);
    }

    // ---- Abandon / history ----

    public async Task<IcpRunDto> AbandonRunAsync(int runId, string reason, int userId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        await EnsureAccessAsync(run, userId, ct);

        if (run.Status != IcpRunStatus.Open)
            throw new InvalidOperationException("The run is closed.");

        var trimmedReason = ValidateReason(reason);

        run.Status = IcpRunStatus.Abandoned;
        run.ClosedAt = _clock.UtcNow.UtcDateTime;
        run.CloseReason = trimmedReason;

        await _db.SaveChangesAsync(ct);
        return await GetRunAsync(runId, userId, ct);
    }

    public async Task<List<IcpRunListItem>> GetRunHistoryAsync(int equipmentId, int userId, CancellationToken ct = default)
    {
        var equipment = await _db.Equipment.FirstOrDefaultAsync(e => e.Id == equipmentId, ct)
            ?? throw new NotFoundException($"Equipment {equipmentId} not found.");

        var scope = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        if (scope != null && !scope.Contains(equipment.SectionId))
            throw new NotFoundException($"Equipment {equipmentId} not found.");

        var runs = await _db.IcpRuns.AsNoTracking()
            .Include(r => r.IcpMethod)
            .Include(r => r.Calibration)
            .Include(r => r.Samples)
            .Where(r => r.EquipmentId == equipmentId)
            .OrderByDescending(r => r.StartedAt)
            .ToListAsync(ct);

        var userIds = runs.Select(r => r.AnalystUserId).Distinct().ToList();
        var names = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return runs.Select(r => new IcpRunListItem(
            r.Id, r.Code, r.IcpMethod?.Abbreviation ?? string.Empty,
            names.TryGetValue(r.AnalystUserId, out var n) ? n : "Unknown",
            r.StartedAt, r.Status, r.Calibration?.Status ?? IcpCalibrationStatus.Pending,
            r.Samples.Count(s => s.Status == IcpRunSampleStatus.Assigned))).ToList();
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

    private async Task EnsureAccessAsync(IcpRun run, int userId, CancellationToken ct)
    {
        var scope = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        if (scope != null && !scope.Contains(run.SectionId))
            throw new NotFoundException($"ICP run {run.Id} not found.");
    }

    private async Task<IcpRun> LoadRunAsync(int id, CancellationToken ct) =>
        await _db.IcpRuns
            .Include(r => r.Section)
            .Include(r => r.Equipment)
            .Include(r => r.IcpMethod)
            .Include(r => r.Calibration).ThenInclude(c => c!.Elements)
            .Include(r => r.Calibration).ThenInclude(c => c!.CalibrationStandardMaterial)
            .Include(r => r.Calibration).ThenInclude(c => c!.IcvStandardMaterial)
            .Include(r => r.CcvReadings)
            .Include(r => r.Samples)
            .Include(r => r.Evidence)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException($"ICP run {id} not found.");

    private static IcpMethodResponse ReadSnapshot(IcpRun run) =>
        JsonSerializer.Deserialize<IcpMethodResponse>(run.MethodSnapshotJson, JsonOptions)
            ?? throw new InvalidOperationException("The run's method snapshot could not be read.");

    private async Task<IcpRunDto> BuildDtoAsync(IcpRun run, CancellationToken ct)
    {
        var snapshot = ReadSnapshot(run);
        var cal = run.Calibration!;

        var userIds = new HashSet<int> { run.AnalystUserId };
        if (cal.ConfirmedByUserId.HasValue) userIds.Add(cal.ConfirmedByUserId.Value);
        foreach (var e in run.Evidence) userIds.Add(e.UploadedByUserId);
        foreach (var c in run.CcvReadings) userIds.Add(c.EnteredByUserId);
        var names = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        string? NameOf(int? id) => id.HasValue && names.TryGetValue(id.Value, out var n) ? n : null;

        var calDto = new IcpCalibrationDto(
            cal.Id, cal.Code, cal.Status,
            cal.CalibrationStandardMaterialId, cal.CalibrationStandardMaterial?.LotLabel, snapshot.CalibrationStandardEntryCode,
            cal.IcvStandardMaterialId, cal.IcvStandardMaterial?.LotLabel, snapshot.IcvStandardEntryCode,
            cal.ConfirmedByUserId, NameOf(cal.ConfirmedByUserId), cal.ConfirmedAt,
            cal.ConfirmedAt?.AddHours(snapshot.MaxCalibrationAgeHours),
            cal.Elements.OrderBy(e => e.Id).Select(e => new IcpCalibrationElementDto(
                e.Id, e.IcpMethodElementId, e.Symbol, e.CorrelationR, e.BlankMgPerL, e.IcvMeasuredMgPerL,
                e.IcvRecoveryPercent, e.Passed, e.FailureReasons)).ToList());

        var ccv = run.CcvReadings.OrderBy(c => c.EnteredAt).ThenBy(c => c.Id).Select(c => new IcpCcvReadingDto(
            c.Id, c.IcpMethodElementId, c.Symbol, c.MeasuredMgPerL, c.RecoveryPercent, c.Passed,
            c.EnteredByUserId, NameOf(c.EnteredByUserId), c.EnteredAt)).ToList();

        var orderIds = run.Samples.Select(s => s.TestOrderId).ToList();
        var orders = orderIds.Count == 0 ? new Dictionary<int, TestOrder>() : await _db.TestOrders.AsNoTracking()
            .Include(o => o.Sample).ThenInclude(s => s!.Item)
            .Where(o => orderIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, ct);
        var samples = run.Samples.OrderBy(s => s.Id).Select(s =>
        {
            orders.TryGetValue(s.TestOrderId, out var o);
            return new IcpRunSampleSummaryDto(
                s.Id, s.TestOrderId, s.Status, o?.Sample?.ReferenceNumber ?? string.Empty,
                o?.Sample?.BatchNumber, o?.Sample?.Item?.Name, o?.TestCode ?? string.Empty);
        }).ToList();

        var evidence = run.Evidence.OrderByDescending(e => e.UploadedAt).Select(e => ToEvidenceDto(e, NameOf(e.UploadedByUserId))).ToList();

        var reason = ConfirmBlockReason(run);
        return new IcpRunDto(
            run.Id, run.Version, run.Code, run.SectionId,
            run.EquipmentId, run.Equipment?.Code ?? string.Empty, run.Equipment?.Name ?? string.Empty,
            run.IcpMethodId, run.IcpMethod?.Abbreviation ?? snapshot.Abbreviation, snapshot,
            run.AnalystUserId, NameOf(run.AnalystUserId), run.StartedAt, run.Status, run.ClosedAt, run.CloseReason,
            calDto, ccv, samples, evidence,
            reason == null, reason);
    }
}
