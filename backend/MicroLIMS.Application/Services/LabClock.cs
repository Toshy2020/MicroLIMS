using Microsoft.Extensions.Configuration;
using MicroLIMS.Application.Interfaces;

namespace MicroLIMS.Application.Services;

public class LabClock : ILabClock
{
    private static readonly Lazy<LabClock> _defaultInstance = new(() =>
        new LabClock(TimeProvider.System, ResolveTimeZone("Africa/Cairo")));

    public static LabClock Default => _defaultInstance.Value;

    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _timeZone;

    public LabClock(TimeProvider timeProvider, IConfiguration configuration)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        var configuredZone = configuration["Lab:TimeZoneId"] ?? "Africa/Cairo";
        _timeZone = ResolveTimeZone(configuredZone);
    }

    public LabClock(TimeProvider timeProvider, TimeZoneInfo timeZone)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _timeZone = timeZone ?? throw new ArgumentNullException(nameof(timeZone));
    }

    public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

    public TimeZoneInfo LabTimeZone => _timeZone;

    public DateTime ToLabLocal(DateTime utcDateTime)
    {
        var utc = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, _timeZone);
    }

    public DateOnly LabToday => DateOnly.FromDateTime(ToLabLocal(_timeProvider.GetUtcNow().UtcDateTime));

    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            // Never guess: run codes and expiry checks would silently use the wrong date.
            throw new InvalidOperationException(
                $"Lab time zone '{timeZoneId}' (and fallback 'Egypt Standard Time') was not found on this server. Set Lab:TimeZoneId to a valid time zone id.");
        }
    }
}
