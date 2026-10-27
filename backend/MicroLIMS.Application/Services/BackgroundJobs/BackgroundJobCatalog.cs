namespace MicroLIMS.Application.Services.BackgroundJobs;

public enum BackgroundJobClassification
{
    // Prunes or monitors operational data that is not a GxP record. Administrators may run it by hand.
    Housekeeping = 0,
    // Changes GxP state, or sends external notifications. Status only: it runs on its schedule and nobody triggers it.
    Gxp = 1
}

public sealed record BackgroundJobDefinition(
    string Name,
    BackgroundJobClassification Classification,
    string Description,
    string Schedule,
    string ClassificationReason,
    // False for high-frequency jobs: an idle, successful scheduled pass leaves no row
    // (it would be thousands of empty rows a week). Failures, passes that processed
    // something and every manual run are always recorded.
    bool RecordIdleScheduledPasses = true);

// The one place that decides which background jobs an administrator may run by hand
// (decision Q10: housekeeping only). A test fails when a registered worker is missing
// here. When unsure, classify as Gxp: it is the safe direction.
public static class BackgroundJobCatalog
{
    public static readonly IReadOnlyList<BackgroundJobDefinition> All = new[]
    {
        new BackgroundJobDefinition(
            "SessionTokenCleanupWorker", BackgroundJobClassification.Housekeeping,
            "Deletes long-dead refresh and password-reset tokens.",
            "Every 24 hours (SessionTokenCleanup:IntervalHours)",
            "Touches only RefreshTokens and PasswordResetTokens, which nothing references; no GxP record, audit trail or signature is read or changed. Safe to repeat."),
        new BackgroundJobDefinition(
            "ErrorLogRetentionWorker", BackgroundJobClassification.Housekeeping,
            "Prunes incidents and error logs past their retention period.",
            "Every 24 hours (ErrorMonitoring:Retention:IntervalHours)",
            "Prunes technical error-monitoring tables that are explicitly outside the GxP record set. Safe to repeat."),
        new BackgroundJobDefinition(
            "DatabaseHealthMonitorWorker", BackgroundJobClassification.Housekeeping,
            "Polls PostgreSQL for slow and blocked queries and reports them as incidents.",
            "Every 60 seconds (ErrorMonitoring:DatabaseHealth:PollIntervalSeconds)",
            "A read-only health check; its only output is technical error-monitoring incidents. Safe to repeat.",
            RecordIdleScheduledPasses: false),
        new BackgroundJobDefinition(
            "CriticalAlertWorker", BackgroundJobClassification.Gxp,
            "Emails an alert for Critical incidents that have not been alerted yet.",
            "Every 60 seconds (ErrorMonitoring:CriticalAlerts:PollIntervalSeconds), only when enabled",
            "Sends outbound email and stamps Incident.AlertedAtUtc, so a manual pass could send or suppress an alert out of band. Not on the approved housekeeping list; status only.",
            RecordIdleScheduledPasses: false),
        new BackgroundJobDefinition(
            "SolutionPreparationExpiryWorker", BackgroundJobClassification.Gxp,
            "Flips due Solution Preparation records to Expired.",
            "Every 5 minutes",
            "Changes the status of GxP solution preparation records.",
            RecordIdleScheduledPasses: false),
        new BackgroundJobDefinition(
            "DocumentEffectiveDateWorker", BackgroundJobClassification.Gxp,
            "Activates document revisions that reached their effective date, generates periodic review tasks and training escalations, and notifies Section Heads of overdue reassignments.",
            "Every 60 minutes (DocumentControl:EffectiveDateWorkerIntervalMinutes)",
            "Moves controlled-document revisions into effect, creates review and training tasks and sends notifications; effective dates must follow the schedule, not an administrator. The reassignment reminder only notifies; it never reassigns."),
        new BackgroundJobDefinition(
            "MonitoringDeviationWorker", BackgroundJobClassification.Gxp,
            "Opens deviations for missed monitoring occurrences and over-limit results.",
            "Every 60 minutes (MonitoringDeviations:IntervalMinutes)",
            "Opens GxP deviations and follow-up sampling occurrences."),
        new BackgroundJobDefinition(
            "EquipmentOverdueWorker", BackgroundJobClassification.Gxp,
            "Takes instruments with overdue calibration or maintenance out of service.",
            "Every 60 minutes (EquipmentOverdue:IntervalMinutes)",
            "Changes equipment status, which gates whether an instrument may be used for GxP work.")
    };

    public static BackgroundJobDefinition? Find(string? name) =>
        All.FirstOrDefault(j => string.Equals(j.Name, name, StringComparison.OrdinalIgnoreCase));
}
