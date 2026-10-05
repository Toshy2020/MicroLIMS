using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Helpers;

namespace MicroLIMS.Application.Services;

public partial class IcpRunService
{
    // Readings are append-only: there is deliberately no edit or delete.
    public async Task<IcpRunDto> AddCcvReadingAsync(int runId, AddIcpCcvRequest r, int userId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        await EnsureAccessAsync(run, userId, ct);
        if (run.Status != IcpRunStatus.Open)
            throw new InvalidOperationException("The run is closed.");

        var snapshot = ReadSnapshot(run);
        if (!snapshot.RequireCcv)
            throw new InvalidOperationException("This method does not use CCV.");

        var nowUtc = _clock.UtcNow.UtcDateTime;
        var cal = run.Calibration!;
        if (!(IcpElementAvailability.ExpiresAt(cal, snapshot.MaxCalibrationAgeHours) >= nowUtc))
            throw new InvalidOperationException("Confirm a valid calibration before entering CCV.");

        var element = snapshot.Elements.FirstOrDefault(e => e.Id == r.IcpMethodElementId)
            ?? throw new InvalidOperationException("The element does not belong to this method.");
        if (r.MeasuredMgPerL < 0m)
            throw new InvalidOperationException("The measured value cannot be negative.");

        var (recovery, passed) = IcpCalibrationEvaluator.EvaluateCcv(
            r.MeasuredMgPerL, snapshot.CcvNominalMgPerL!.Value, snapshot.CcvRecoveryLowPercent!.Value, snapshot.CcvRecoveryHighPercent!.Value);

        _db.IcpCcvReadings.Add(new IcpCcvReading
        {
            IcpRunId = run.Id, IcpMethodElementId = element.Id, Symbol = element.Symbol,
            MeasuredMgPerL = r.MeasuredMgPerL, RecoveryPercent = recovery, Passed = passed,
            EnteredByUserId = userId, EnteredAt = nowUtc,
        });
        await _db.SaveChangesAsync(ct);
        return await GetRunAsync(runId, userId, ct);
    }
}
