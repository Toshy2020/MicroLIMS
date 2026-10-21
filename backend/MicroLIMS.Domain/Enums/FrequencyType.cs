namespace MicroLIMS.Domain.Enums;

// Persisted as int; append only.
public enum FrequencyType
{
    Weekly,
    Monthly,
    // Every working day of the lab week; no interval, weekday or anchor.
    Daily
}
