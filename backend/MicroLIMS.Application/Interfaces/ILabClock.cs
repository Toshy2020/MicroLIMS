namespace MicroLIMS.Application.Interfaces;

public interface ILabClock
{
    DateTimeOffset UtcNow { get; }
    DateTime ToLabLocal(DateTime utcDateTime);
    DateOnly LabToday { get; }
    TimeZoneInfo LabTimeZone { get; }
}
