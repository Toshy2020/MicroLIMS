using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

public partial class IcpRunService
{
    private const string MissingCalibrationReportReason = "Upload the Syngistix calibration report before confirming.";

    private static bool IsCurrentCalibrationReport(IcpEvidence e) =>
        e.Context == IcpEvidenceContext.Calibration && e.SupersededByEvidenceId == null;

    // First reason the calibration cannot be confirmed now, or null. Shared by
    // ConfirmCalibrationAsync and the DTO so the two never disagree.
    private static string? ConfirmBlockReason(IcpRun run)
    {
        var cal = run.Calibration;
        if (run.Status != IcpRunStatus.Open) return "The run is closed.";
        if (cal == null || cal.Status != IcpCalibrationStatus.Pending) return "The calibration has already been confirmed.";
        if (cal.CalibrationStandardMaterialId == null) return "Choose the calibration standard lot.";
        if (ReadSnapshot(run).RequireIcv && cal.IcvStandardMaterialId == null) return "Choose the ICV standard lot.";
        if (!run.Evidence.Any(IsCurrentCalibrationReport)) return MissingCalibrationReportReason;
        return null;
    }

    private static IcpCheckLimits LimitsOf(IcpMethodResponse m) => new(
        m.MinCorrelation, m.RequireBlank, m.BlankMaxMgPerL, m.RequireIcv,
        m.IcvNominalMgPerL, m.IcvRecoveryLowPercent, m.IcvRecoveryHighPercent);

    // Pure evaluation of every element against the snapshot's limits.
    private static Dictionary<int, IcpCalibrationElementVerdict> Evaluate(IcpCalibration cal, IcpMethodResponse snapshot)
    {
        var limits = LimitsOf(snapshot);
        return cal.Elements.ToDictionary(
            e => e.Id,
            e => IcpCalibrationEvaluator.Evaluate(new IcpCalibrationElementInput(e.Symbol, e.CorrelationR, e.BlankMgPerL, e.IcvMeasuredMgPerL), limits));
    }

    private static void ApplyVerdicts(IcpCalibration cal, Dictionary<int, IcpCalibrationElementVerdict> verdicts)
    {
        foreach (var e in cal.Elements)
        {
            var v = verdicts[e.Id];
            e.Passed = v.Passed;
            e.IcvRecoveryPercent = v.IcvRecoveryPercent;
            e.FailureReasons = v.FailureReasons.Count > 0 ? string.Join(" ", v.FailureReasons) : null;
        }
    }

    // Lot must be a usable ReferenceStandard lot of the method's standard entry, in this lab.
    private async Task<Material> RequireStandardLotAsync(int materialId, int entryId, string? entryCode, string which, int sectionId, CancellationToken ct)
    {
        var message = $"The {which} standard lot must be a usable lot of {entryCode}.";
        var lot = await _db.Materials.FirstOrDefaultAsync(m => m.Id == materialId, ct)
            ?? throw new InvalidOperationException(message);
        if (lot.MaterialType != MaterialType.ReferenceStandard || lot.SectionId != sectionId
            || !LotUsability.Check(lot, entryId, null, _clock.LabToday).Usable)
            throw new InvalidOperationException(message);
        return lot;
    }

    public async Task<IcpRunDto> SaveCalibrationAsync(int runId, SaveIcpCalibrationRequest r, int userId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        await EnsureAccessAsync(run, userId, ct);

        if (run.Status != IcpRunStatus.Open)
            throw new InvalidOperationException("The run is closed.");
        var cal = run.Calibration;
        if (cal == null || cal.Status != IcpCalibrationStatus.Pending)
            throw new InvalidOperationException("The calibration has already been confirmed.");

        var snapshot = ReadSnapshot(run);

        if (r.IcvStandardMaterialId.HasValue && !snapshot.RequireIcv)
            throw new InvalidOperationException("The ICV standard lot is only used when the method requires ICV.");

        // Validate everything before mutating anything.
        Material? calLot = null, icvLot = null;
        if (r.CalibrationStandardMaterialId is int calId)
            calLot = await RequireStandardLotAsync(calId, snapshot.CalibrationStandardEntryId, snapshot.CalibrationStandardEntryCode, "calibration", run.SectionId, ct);
        if (r.IcvStandardMaterialId is int icvId)
            icvLot = await RequireStandardLotAsync(icvId, snapshot.IcvStandardEntryId ?? 0, snapshot.IcvStandardEntryCode, "ICV", run.SectionId, ct);

        var rows = new List<(IcpCalibrationElement Row, SaveIcpCalibrationElementInput Input)>();
        foreach (var input in r.Elements ?? new List<SaveIcpCalibrationElementInput>())
        {
            var row = cal.Elements.FirstOrDefault(e => e.Id == input.IcpCalibrationElementId)
                ?? throw new InvalidOperationException($"Element {input.IcpCalibrationElementId} does not belong to this calibration.");
            rows.Add((row, input));
        }

        cal.CalibrationStandardMaterialId = calLot?.Id;
        cal.IcvStandardMaterialId = icvLot?.Id;
        foreach (var (row, input) in rows)
        {
            row.CorrelationR = input.CorrelationR;
            row.BlankMgPerL = input.BlankMgPerL;
            row.IcvMeasuredMgPerL = input.IcvMeasuredMgPerL;
        }
        ApplyVerdicts(cal, Evaluate(cal, snapshot));

        await _db.SaveChangesAsync(ct);
        return await GetRunAsync(runId, userId, ct);
    }

    public async Task<IcpRunDto> ConfirmCalibrationAsync(int runId, ConfirmIcpCalibrationRequest r, int userId, string? ip, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        await EnsureAccessAsync(run, userId, ct);

        var blocked = ConfirmBlockReason(run);
        if (blocked != null) throw new InvalidOperationException(blocked);
        var cal = run.Calibration!;

        var snapshot = ReadSnapshot(run);
        // The lots were checked at save; they may have expired or run out since.
        await RequireStandardLotAsync(cal.CalibrationStandardMaterialId!.Value, snapshot.CalibrationStandardEntryId, snapshot.CalibrationStandardEntryCode, "calibration", run.SectionId, ct);
        if (snapshot.RequireIcv)
            await RequireStandardLotAsync(cal.IcvStandardMaterialId!.Value, snapshot.IcvStandardEntryId ?? 0, snapshot.IcvStandardEntryCode, "ICV", run.SectionId, ct);

        var verdicts = Evaluate(cal, snapshot);

        // Signs first - a wrong password must leave nothing else persisted.
        var signature = await _signatures.SignAsync(
            userId, r.Password, SignatureMeaning.SuitabilityRunPerformed, nameof(IcpCalibration), cal.Id, r.Comment, ip);

        ApplyVerdicts(cal, verdicts);
        cal.Status = IcpCalibrationStatus.Confirmed;
        cal.ConfirmedByUserId = userId;
        cal.ConfirmedAt = _clock.UtcNow.UtcDateTime;
        cal.Signature = signature;

        await _db.SaveChangesAsync(ct);
        return await GetRunAsync(runId, userId, ct);
    }
}
