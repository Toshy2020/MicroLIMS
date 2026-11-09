using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

public partial class IcpRunService
{
    private const string MissingCalibrationReportReason = "Upload the Syngistix calibration report (the calibration curve) before confirming.";

    private static bool IsCurrentCalibrationReport(IcpEvidence e) =>
        e.Context == IcpEvidenceContext.Calibration && e.Kind == IcpEvidenceKind.CalibrationReport && e.SupersededByEvidenceId == null;

    // First reason the calibration cannot be confirmed now, or null. Shared by
    // ConfirmCalibrationAsync and the DTO so the two never disagree.
    private static string? ConfirmBlockReason(IcpRun run)
    {
        var cal = run.Calibration;
        if (run.Status != IcpRunStatus.Open) return "The run is closed.";
        if (cal == null || cal.Status != IcpCalibrationStatus.Pending) return "The calibration has already been confirmed.";
        if (cal.CalibrationStandardMaterialId == null) return "Choose the calibration standard lot.";
        if (cal.Elements.Count == 0) return "Add the elements calibrated on this run, with their r.";
        if (!run.Evidence.Any(IsCurrentCalibrationReport)) return MissingCalibrationReportReason;
        if (!cal.Elements.Any(e => e.Passed)) return "No element passes the calibration check (r), so the instrument is not ready for testing.";
        return null;
    }

    private static void ApplyVerdict(IcpCalibrationElement e)
    {
        var v = IcpCalibrationEvaluator.Evaluate(e.Symbol, e.CorrelationR);
        e.Passed = v.Passed;
        e.FailureReasons = v.FailureReason;
    }

    // Lot must be a usable ReferenceStandard lot in this lab.
    private async Task<Material> RequireStandardLotAsync(int materialId, int sectionId, CancellationToken ct)
    {
        const string message = "The calibration standard must be a usable reference standard lot of this laboratory.";
        var lot = await _db.Materials.FirstOrDefaultAsync(m => m.Id == materialId, ct)
            ?? throw new BusinessRuleException(message);
        if (lot.MaterialType != MaterialType.ReferenceStandard || lot.SectionId != sectionId
            || !LotUsability.Check(lot, lot.MaterialMasterEntryId ?? 0, null, _clock.LabToday).Usable)
            throw new BusinessRuleException(message);
        return lot;
    }

    // Saves the standard lot and the elements the analyst calibrated (symbol + r). The server judges r.
    public async Task<IcpRunDto> SaveCalibrationAsync(int runId, SaveIcpCalibrationRequest r, int userId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        await EnsureAccessAsync(run, userId, ct);

        if (run.Status != IcpRunStatus.Open)
            throw new BusinessRuleException("The run is closed.");
        var cal = run.Calibration;
        if (cal == null || cal.Status != IcpCalibrationStatus.Pending)
            throw new BusinessRuleException("The calibration has already been confirmed.");

        // Validate everything before mutating anything.
        Material? calLot = null;
        if (r.CalibrationStandardMaterialId is int calId)
            calLot = await RequireStandardLotAsync(calId, run.SectionId, ct);

        var inputs = new List<(string Symbol, decimal? R)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var input in r.Elements ?? new List<SaveIcpCalibrationElementInput>())
        {
            var symbol = IcpTestDefinitionRules.NormalizeSymbol(input.Symbol);
            if (!seen.Add(symbol))
                throw new BusinessRuleException($"Element \"{symbol}\" is listed more than once.");
            if (input.CorrelationR is < 0m or > 1m)
                throw new BusinessRuleException($"{symbol}: r must be between 0 and 1.");
            // numeric(8,6): round before judging so save-time and confirm-time verdicts match what is stored.
            inputs.Add((symbol, input.CorrelationR is decimal r6 ? Math.Round(r6, 6, MidpointRounding.AwayFromZero) : null));
        }

        cal.CalibrationStandardMaterialId = calLot?.Id;
        foreach (var gone in cal.Elements.Where(e => !seen.Contains(e.Symbol)).ToList())
        {
            cal.Elements.Remove(gone);
            _db.IcpCalibrationElements.Remove(gone);
        }
        foreach (var (symbol, rValue) in inputs)
        {
            var row = cal.Elements.FirstOrDefault(e => string.Equals(e.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
            if (row == null)
            {
                row = new IcpCalibrationElement { Symbol = symbol };
                cal.Elements.Add(row);
            }
            row.Symbol = symbol;
            row.CorrelationR = rValue;
            ApplyVerdict(row);
        }

        await _db.SaveChangesAsync(ct);
        return await GetRunAsync(runId, userId, ct);
    }

    // The e-signature states that the instrument is calibrated and ready for testing; it starts the validity clock.
    public async Task<IcpRunDto> ConfirmCalibrationAsync(int runId, ConfirmIcpCalibrationRequest r, int userId, string? ip, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        await EnsureAccessAsync(run, userId, ct);

        var blocked = ConfirmBlockReason(run);
        if (blocked != null) throw new BusinessRuleException(blocked);
        var cal = run.Calibration!;

        // The lot was checked at save; it may have expired or run out since.
        await RequireStandardLotAsync(cal.CalibrationStandardMaterialId!.Value, run.SectionId, ct);

        // Signs first - a wrong password must leave nothing else persisted.
        var signature = await _signatures.SignAsync(
            userId, r.Password, SignatureMeaning.SuitabilityRunPerformed, nameof(IcpCalibration), cal.Id, r.Comment, ip);

        foreach (var e in cal.Elements) ApplyVerdict(e);
        cal.Status = IcpCalibrationStatus.Confirmed;
        cal.ConfirmedByUserId = userId;
        cal.ConfirmedAt = _clock.UtcNow.UtcDateTime;
        cal.Signature = signature;

        await _db.SaveChangesAsync(ct);
        return await GetRunAsync(runId, userId, ct);
    }
}
