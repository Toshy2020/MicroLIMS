using System.Globalization;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

public record AddIcpCcvRequest(int IcpMethodElementId, decimal MeasuredMgPerL);
public record IcpElementStateDto(int IcpMethodElementId, string Symbol, bool Valid, string? Reason);

// One rule for "may this element be reported now", used by the run DTO, entry preview and submission.
public static class IcpElementAvailability
{
    public static List<IcpElementStateDto> Evaluate(IcpMethodResponse snapshot, IcpCalibration calibration,
        IReadOnlyList<IcpCcvReading> ccv, DateTime nowUtc, ILabClock? clock = null)
    {
        clock ??= LabClock.Default;
        var confirmed = calibration.Status == IcpCalibrationStatus.Confirmed && calibration.ConfirmedAt.HasValue;
        var expiresAt = confirmed ? calibration.ConfirmedAt!.Value.AddHours(snapshot.MaxCalibrationAgeHours) : (DateTime?)null;

        return snapshot.Elements.Select(el =>
        {
            var (valid, reason) = Check(el);
            return new IcpElementStateDto(el.Id, el.Symbol, valid, reason);
        }).ToList();

        (bool, string?) Check(IcpMethodElementResponse el)
        {
            if (!confirmed) return (false, "Calibration is not confirmed.");
            if (expiresAt < nowUtc)
                return (false, $"Calibration expired at {clock.ToLabLocal(expiresAt!.Value).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}.");
            var calEl = calibration.Elements.FirstOrDefault(e => e.IcpMethodElementId == el.Id);
            if (calEl is { Passed: false })
                return (false, $"Failed calibration: {calEl.FailureReasons}");
            var mine = ccv.Where(c => c.IcpMethodElementId == el.Id).ToList();
            var failed = mine.FirstOrDefault(c => !c.Passed);
            if (failed != null)
                return (false, $"CCV failed ({failed.RecoveryPercent.ToString("0.0", CultureInfo.InvariantCulture)}%).");
            if (snapshot.RequireCcv && !mine.Any(c => c.Passed)) return (false, "Needs a passing CCV.");
            return (true, null);
        }
    }
}
