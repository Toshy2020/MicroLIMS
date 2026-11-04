using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// One pass of a background worker. Operational data, NOT a GxP record, and
// excluded from the automatic audit capture (a row per pass would be noise).
// Manual triggers are audited separately in the Security Audit Trail with
// actor and reason. Append-mostly: inserted at start, updated once at finish,
// never deleted by the application.
public class BackgroundJobRun
{
    public const int MaxErrorLength = 1000;
    public const int MaxReasonLength = 500;

    public long Id { get; set; }
    public string JobName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public BackgroundJobOutcome Outcome { get; set; } = BackgroundJobOutcome.Running;
    public int? ItemsProcessed { get; set; }
    public string? Error { get; set; }
    public BackgroundJobTrigger TriggeredBy { get; set; } = BackgroundJobTrigger.Schedule;
    public int? TriggeredByUserId { get; set; }
    public string? Reason { get; set; }
}
