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
    Created,
    // A plan revision moved a not-yet-started occurrence to new dates.
    Rescheduled
}

// Why a deviation was opened. Persisted as int; append only.
public enum DeviationKind
{
    // The window and its grace period passed with an expected test not done.
    MissedOccurrence,
    // An approved result was over its action limit, out of specification or "detected".
    ActionLimitExceeded
}

public enum DeviationStatus
{
    Open,
    Closed
}

// How a deviation was closed. Persisted as int; append only.
public enum DeviationClosure
{
    // Missed work that fell before the plan went live. The occurrence is skipped.
    NotApplicableBeforeGoLive,
    // Missed work that is explained and accepted. The occurrence stays as it happened.
    Justified,
    // An excursion that was assessed; any follow-up sampling is done or not needed.
    Investigated
}
