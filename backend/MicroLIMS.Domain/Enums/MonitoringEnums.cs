namespace MicroLIMS.Domain.Enums;

// Persisted as int; append only.
public enum MonitoringCategory
{
    Water,
    EnvironmentalMonitoring,
    AfterCleaning
}

// What an occurrence is about. Exactly one target id is set on the row.
public enum MonitoringTargetType
{
    WaterSamplingPoint,
    Room,
    Machine
}

public enum PlanRevisionStatus
{
    Draft,
    Approved,
    Superseded
}

public enum OccurrenceExceptionState
{
    None,
    Skipped,
    Retired
}

public enum OccurrenceEventType
{
    TargetDateChanged,
    WindowExtended,
    Skipped,
    Retired,
    Linked,
    LinkRefused,
    Created
}
