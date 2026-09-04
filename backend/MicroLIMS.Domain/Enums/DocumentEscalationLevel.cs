namespace MicroLIMS.Domain.Enums;

public enum DocumentEscalationLevel
{
    ApproachingDue = 1,  // e.g. T-3 days (Warning / Reminder)
    Due = 2,             // Due Date (Action Required)
    Overdue = 3,         // e.g. T+1 day (Overdue Escalation)
    CriticalOverdue = 4  // e.g. T+X days (Manager / QA Escalation)
}
