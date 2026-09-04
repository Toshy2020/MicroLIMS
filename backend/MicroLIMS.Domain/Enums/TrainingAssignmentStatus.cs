namespace MicroLIMS.Domain.Enums;

public enum TrainingAssignmentStatus
{
    NotAssigned = 1,
    Assigned = 2,
    Reading = 3,
    Acknowledged = 4,
    KafPending = 5,
    CompletedPassed = 6,
    Failed = 7,
    Overdue = 8,
    SupersededIncomplete = 9,
    TrainedOnSupersededOnly = 10,
    Cancelled = 11
}
