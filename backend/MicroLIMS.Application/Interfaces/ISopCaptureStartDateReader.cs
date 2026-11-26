namespace MicroLIMS.Application.Interfaces;

/// <summary>
/// Reads the UTC instant when SOP revision capture on test orders was installed.
/// Pre-capture test orders display this date as "Not recorded (before {date})".
/// </summary>
public interface ISopCaptureStartDateReader
{
    Task<DateTime?> GetStartDateUtcAsync(CancellationToken cancellationToken = default);
}
