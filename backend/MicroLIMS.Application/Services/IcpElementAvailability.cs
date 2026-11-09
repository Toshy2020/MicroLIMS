using System.Globalization;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

public record IcpElementStateDto(int IcpTestElementId, string Symbol, bool Valid, string? Reason);

// One rule for "may this element be reported now", used by the entry preview and submission.
public static class IcpElementAvailability
{
    // When a confirmed calibration stops being usable; null while it is not confirmed.
    public static DateTime? ExpiresAt(IcpCalibration calibration) =>
        calibration.Status == IcpCalibrationStatus.Confirmed && calibration.ConfirmedAt.HasValue
            ? calibration.ConfirmedAt.Value.AddHours(IcpDefaults.CalibrationValidHours) : null;

    public static List<IcpElementStateDto> Evaluate(IEnumerable<TestDefinitionIcpElement> elements, IcpCalibration calibration,
        DateTime nowUtc, ILabClock? clock = null)
    {
        clock ??= LabClock.Default;
        var expiresAt = ExpiresAt(calibration);

        return elements.OrderBy(e => e.DisplayOrder).Select(el =>
        {
            var (valid, reason) = Check(el);
            return new IcpElementStateDto(el.Id, el.Symbol, valid, reason);
        }).ToList();

        (bool, string?) Check(TestDefinitionIcpElement el)
        {
            if (expiresAt is null) return (false, "Calibration is not confirmed.");
            if (expiresAt < nowUtc)
                return (false, $"Calibration expired at {clock.ToLabLocal(expiresAt.Value).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}.");
            var calEl = calibration.Elements.FirstOrDefault(e => string.Equals(e.Symbol, el.Symbol, StringComparison.OrdinalIgnoreCase));
            if (calEl == null) return (false, $"{el.Symbol} was not calibrated on this run.");
            if (!calEl.Passed) return (false, $"Failed calibration: {calEl.FailureReasons}");
            return (true, null);
        }
    }
}
