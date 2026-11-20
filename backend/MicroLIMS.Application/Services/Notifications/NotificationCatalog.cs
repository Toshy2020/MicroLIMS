namespace MicroLIMS.Application.Services.Notifications;

public sealed record NotificationTypeInfo(string Type, string DefaultSeverity, bool EmailAllowed, string TitleTemplate);

public static class NotificationCatalog
{
    private static readonly NotificationTypeInfo[] All =
    [
        // Spec 5.2 catalogue types
        new("ReviewWaiting", "info", false, "Sample awaiting review"),
        new("ApprovalWaiting", "info", false, "Sample awaiting approval"),
        new("TestReturned", "warning", false, "Test returned to analyst"),
        new("TestAssigned", "info", false, "Test assigned"),
        new("IncubationReady", "info", false, "Incubation ready to read"),
        new("MediaExpiry", "warning", true, "Media lot expiring or expired"),
        new("SolutionExpiry", "warning", true, "Solution lot expiring or expired"),
        new("StrainExpiry", "warning", true, "Reference strain expiring or expired"),
        new("CalibrationDue", "warning", true, "Equipment calibration due"),
        new("ItemOverdue", "warning", true, "Item overdue"),
        new("Escalation", "error", true, "Escalation triggered"),
        new("DocumentTask", "info", false, "Document task assigned"),
        new("TrainingAssigned", "info", false, "Training assigned"),
        new("DirectMessage", "info", false, "New direct message"),
        new("Discussion", "info", false, "Discussion activity"),
        new("ConfigurationApprovalWaiting", "info", false, "Configuration awaiting approval"),
        new("CompetencyExpiry", "warning", false, "Analyst qualification expiring"),

        // Existing types written by Application services
        new("PendingPreparationConfigApproval", "info", false, "Preparation configuration awaiting approval"),
        new("TestReturnedForRevision", "warning", false, "Test returned for revision"),
        new("TestReturnedForBiochemical", "warning", false, "Test returned for biochemical confirmation"),
        new("DiscussionPostUpdated", "info", false, "Discussion post updated"),
        new("DiscussionComment", "info", false, "New discussion comment"),
        new("DocumentReassigned", "info", false, "Document responsibility reassigned"),
        new("ReassignmentOverdue", "warning", false, "Document reassignment overdue"),
        new("MonitoringOverdue", "warning", false, "Monitoring occurrence overdue"),
        new("MonitoringDeviationsOpen", "warning", false, "Open monitoring deviations"),
        new("MonitoringDueThisMonth", "warning", false, "Monitoring due this month"),
        new("EquipmentCalibrationOverdue", "warning", false, "Equipment calibration task overdue"),
        new("EquipmentPmOverdue", "warning", false, "Equipment preventive maintenance overdue"),
    ];

    public static IReadOnlyDictionary<string, NotificationTypeInfo> Types { get; } =
        All.ToDictionary(t => t.Type, StringComparer.Ordinal);

    public static NotificationTypeInfo Get(string type)
    {
        if (string.IsNullOrWhiteSpace(type) || !Types.TryGetValue(type, out var info))
            throw new ArgumentException($"Unknown notification type '{type}'.", nameof(type));
        return info;
    }

    public static NotificationTypeInfo? TryGet(string? type) =>
        !string.IsNullOrWhiteSpace(type) && Types.TryGetValue(type, out var info) ? info : null;
}
